using System;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// People overview: four KPIs, one "Needs action" list and the contracts ending soon.
/// "Current contract" is hr_current_contract_id(empID) everywhere, and "ending" means
/// a VALID current contract whose end date is today or within the next 90 days.
/// </summary>
public partial class COOPERP_NewScreens_HRDashboard : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // Current contract per employee (one row per employee, cid NULL when there is none).
    private const string CurrentSql =
        "SELECT e.empID, hr_current_contract_id(e.empID) AS cid FROM hrm_employee e";

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!HrAccess.RequireHr(false)) return;
        if (!IsPostBack) LoadDashboard();
    }

    private void LoadDashboard()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                LoadKpis(conn);
                LoadNeedsAction(conn);
                LoadEndingSoon(conn);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("HRDashboard: " + ex);
            litError.Text = "<div class='hr-notice hr-notice--bad'>The overview could not be loaded. Refresh the page, and contact MIS if it keeps happening.</div>";
        }
    }

    private void LoadKpis(MySqlConnection conn)
    {
        DataTable dt = Query(conn,
            @"SELECT
                SUM(CASE WHEN c.contractStatus = 'VALID' AND (c.contractEnd IS NULL OR c.contractEnd >= CURDATE()) THEN 1 ELSE 0 END) AS active_staff,
                SUM(CASE WHEN c.contractStatus = 'VALID' AND c.contractEnd BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 90 DAY) THEN 1 ELSE 0 END) AS ending
              FROM (" + CurrentSql + @") x
              LEFT JOIN hrm_emp_contracts c ON c.ID = x.cid");
        if (dt.Rows.Count > 0)
        {
            litActiveStaff.Text = N(dt.Rows[0]["active_staff"]);
            litEnding.Text = N(dt.Rows[0]["ending"]);
        }

        litRenewalsHr.Text = SafeScalar(conn, "SELECT COUNT(*) FROM hr_contract_renewals WHERE status = 'AWAITING_HR'").ToString("N0");

        DataTable lv = Query(conn,
            @"SELECT SUM(status IN ('SUBMITTED','HOD_APPROVED','HR_APPROVED')) AS pending,
                     SUM(status = 'HOD_APPROVED') AS with_hr
              FROM hrm_leave_applications WHERE is_active = 1");
        if (lv.Rows.Count > 0)
        {
            litLeavePending.Text = N(lv.Rows[0]["pending"]);
            litLeaveWithHr.Text = N(lv.Rows[0]["with_hr"]);
        }
    }

    private void LoadNeedsAction(MySqlConnection conn)
    {
        StringBuilder rows = new StringBuilder();

        long leaveHr = SafeScalar(conn, "SELECT COUNT(*) FROM hrm_leave_applications WHERE is_active = 1 AND status = 'HOD_APPROVED'");
        AddAction(rows, "Leave applications awaiting HR", leaveHr, "LeaveApplications.aspx?status=HOD_APPROVED");

        long leaveVc = SafeScalar(conn, "SELECT COUNT(*) FROM hrm_leave_applications WHERE is_active = 1 AND status = 'HR_APPROVED'");
        AddAction(rows, "Leave applications with the Vice Chancellor", leaveVc, "LeaveApplications.aspx?status=HR_APPROVED");

        long renewHr = SafeScalar(conn, "SELECT COUNT(*) FROM hr_contract_renewals WHERE status = 'AWAITING_HR'");
        AddAction(rows, "Renewal applications awaiting HR verification", renewHr, "ContractRenewals.aspx");

        long noApp = SafeScalar(conn,
            @"SELECT COUNT(*) FROM (" + CurrentSql + @") x
              JOIN hrm_emp_contracts c ON c.ID = x.cid
              WHERE c.contractStatus = 'VALID'
                AND c.contractEnd BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 90 DAY)
                AND NOT EXISTS (SELECT 1 FROM hr_contract_renewals a WHERE a.contract_id = c.ID AND a.status <> 'WITHDRAWN')");
        AddAction(rows, "Contracts ending in 90 days with no renewal application", noApp, "ContractRenewals.aspx?tab=expiring&win=90&noapp=1");

        long noValid = SafeScalar(conn,
            @"SELECT COUNT(*) FROM (" + CurrentSql + @") x
              LEFT JOIN hrm_emp_contracts c ON c.ID = x.cid
              WHERE NOT (IFNULL(c.contractStatus,'') = 'VALID' AND (c.contractEnd IS NULL OR c.contractEnd >= CURDATE()))");
        AddAction(rows, "Staff without a valid contract", noValid, "HREmployees.aspx?status=NOVALID");

        long slips = SafeScalar(conn, "SELECT COUNT(*) FROM hrm_payslips WHERE status = 'PENDING'");
        AddAction(rows, "Payslips awaiting approval", slips, "HRPayslips.aspx");

        long runs = SafeScalar(conn, "SELECT COUNT(*) FROM hrm_payroll WHERE payroll_status = 'PENDING'");
        AddAction(rows, "Payroll runs not yet processed", runs, "HRPayroll.aspx");

        long cfg = SafeScalar(conn, "SELECT COUNT(*) FROM hrm_config");
        if (cfg == 0)
            rows.Append("<tr><td>Payroll and tax settings are not set</td><td class='hr-num'></td>" +
                        "<td class='hr-right'><a class='hr-btn hr-btn--link' href='HRConfig.aspx'>Open</a></td></tr>");

        if (rows.Length == 0)
        {
            litNeedsAction.Text = "<div class='hr-empty'>Nothing needs action.</div>";
            return;
        }
        litNeedsAction.Text =
            "<div class='hr-table-wrap'><table class='hr-table'><thead><tr><th>Item</th><th class='hr-num'>Count</th><th></th></tr></thead><tbody>" +
            rows + "</tbody></table></div>";
    }

    private static void AddAction(StringBuilder sb, string label, long count, string url)
    {
        if (count <= 0) return;
        sb.Append("<tr><td>").Append(HttpUtility.HtmlEncode(label)).Append("</td>")
          .Append("<td class='hr-num'><strong>").Append(count.ToString("N0")).Append("</strong></td>")
          .Append("<td class='hr-right'><a class='hr-btn hr-btn--link' href='").Append(HttpUtility.HtmlAttributeEncode(url)).Append("'>Open</a></td></tr>");
    }

    private void LoadEndingSoon(MySqlConnection conn)
    {
        DataTable dt = Query(conn,
            @"SELECT e.EMP_CODE, e.emp_name, d.dept_name, j.jobname, c.contractEnd,
                     DATEDIFF(c.contractEnd, CURDATE()) AS days_left
              FROM (" + CurrentSql + @") x
              JOIN hrm_employee e ON e.empID = x.empID
              JOIN hrm_emp_contracts c ON c.ID = x.cid
              LEFT JOIN hrm_departments d ON d.ID = c.departmentID
              LEFT JOIN hrm_jobs j ON j.ID = c.jobID
              WHERE c.contractStatus = 'VALID'
                AND c.contractEnd BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 90 DAY)
              ORDER BY c.contractEnd ASC, e.emp_name ASC
              LIMIT 12");

        if (dt.Rows.Count == 0)
        {
            litEndingSoon.Text = "<div class='hr-empty'>No contracts end in the next 90 days.</div>";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("<table class='hr-table'><thead><tr><th>Staff</th><th>Department</th><th>Ends</th><th class='hr-num'>Days left</th></tr></thead><tbody>");
        foreach (DataRow r in dt.Rows)
        {
            int days = Convert.ToInt32(r["days_left"]);
            string kind = days <= 30 ? "bad" : "warn";
            sb.Append("<tr><td>").Append(Enc(r["emp_name"]))
              .Append("<span class='hr-sub'>").Append(Enc(r["EMP_CODE"]))
              .Append(string.IsNullOrEmpty(Str(r["jobname"])) ? "" : ", " + Enc(r["jobname"])).Append("</span></td>")
              .Append("<td>").Append(Enc(r["dept_name"])).Append("</td>")
              .Append("<td style='white-space:nowrap'>").Append(Convert.ToDateTime(r["contractEnd"]).ToString("d MMM yyyy", CultureInfo.InvariantCulture)).Append("</td>")
              .Append("<td class='hr-num'><span class='hr-badge hr-badge--").Append(kind).Append("'>").Append(days).Append(days == 1 ? " day" : " days").Append("</span></td></tr>");
        }
        sb.Append("</tbody></table>");
        litEndingSoon.Text = sb.ToString();
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private long SafeScalar(MySqlConnection conn, string sql)
    {
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                object v = cmd.ExecuteScalar();
                long n;
                return v == null || v == DBNull.Value ? 0 : (long.TryParse(v.ToString(), out n) ? n : 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("HRDashboard count failed: " + ex.Message);
            return 0;
        }
    }

    private DataTable Query(MySqlConnection conn, string sql)
    {
        DataTable dt = new DataTable();
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
            da.Fill(dt);
        return dt;
    }

    private static string N(object v)
    {
        long n;
        return v != null && v != DBNull.Value && long.TryParse(v.ToString(), out n) ? n.ToString("N0") : "0";
    }

    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string Enc(object v) { return HttpUtility.HtmlEncode(HrExport.Clean(Str(v))); }
}
