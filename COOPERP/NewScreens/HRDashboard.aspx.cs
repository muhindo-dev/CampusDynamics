using System;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_HRDashboard : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
            LoadDashboard();
    }

    private void LoadDashboard()
    {
        lblCurrentPeriod.Text = DateTime.Now.ToString("MMMM yyyy");

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                LoadKpis(conn);
                LoadRecentPayrolls(conn);
                LoadExpiringContracts(conn);
                LoadPendingPayslips(conn);
                LoadAlerts(conn);
            }
        }
        catch (Exception ex)
        {
            litAlerts.Text = "<div class='hr-alert hr-alert--error'>Error loading dashboard: " +
                System.Web.HttpUtility.HtmlEncode(ex.Message) + "</div>";
        }
    }

    // -------------------------------------------------------------------------
    // KPI cards
    // -------------------------------------------------------------------------

    private void LoadKpis(MySqlConnection conn)
    {
        // Active employees (those with at least one VALID contract)
        lblTotalEmployees.Text = Scalar(conn,
            "SELECT COUNT(DISTINCT e.empID) FROM hrm_employee e INNER JOIN hrm_emp_contracts c ON c.empID = e.empID WHERE c.contractStatus = 'VALID'").ToString("N0");

        // Valid contracts
        lblActiveContracts.Text = Scalar(conn,
            "SELECT COUNT(*) FROM hrm_emp_contracts WHERE contractStatus = 'VALID' AND contractEnd >= CURDATE()").ToString("N0");

        // Expiring within 30 days
        lblExpiringContracts.Text = Scalar(conn,
            "SELECT COUNT(*) FROM hrm_emp_contracts WHERE contractStatus = 'VALID' AND contractEnd BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)").ToString("N0");

        // Pending payslips
        lblPendingPayslips.Text = Scalar(conn,
            "SELECT COUNT(*) FROM hrm_payslips WHERE status = 'PENDING'").ToString("N0");

        // Payroll runs this year
        lblPayrollsThisYear.Text = Scalar(conn,
            "SELECT COUNT(*) FROM hrm_payroll WHERE payroll_year = @yr",
            new MySqlParameter("@yr", DateTime.Now.Year)).ToString("N0");

        // Net pay current month
        decimal netMonth = ScalarDecimal(conn,
            "SELECT COALESCE(SUM(ps.net_salary),0) FROM hrm_payslips ps " +
            "INNER JOIN hrm_payroll pr ON pr.ID = ps.payroll_id " +
            "WHERE ps.payroll_month = @m AND ps.payroll_year = @y AND ps.status = 'APPROVED'",
            new MySqlParameter("@m", DateTime.Now.Month),
            new MySqlParameter("@y", DateTime.Now.Year));
        lblNetPayThisMonth.Text = FormatUGX(netMonth);

        // Pending allowances
        lblPendingAllowances.Text = Scalar(conn,
            "SELECT COUNT(*) FROM hrm_allowance_records WHERE status = 'PENDING'").ToString("N0");

        // Pending deductions
        lblPendingDeductions.Text = Scalar(conn,
            "SELECT COUNT(*) FROM hrm_deduction_records WHERE status = 'PENDING'").ToString("N0");
    }

    // -------------------------------------------------------------------------
    // Recent payroll runs
    // -------------------------------------------------------------------------

    private void LoadRecentPayrolls(MySqlConnection conn)
    {
        DataTable dt = Query(conn,
            @"SELECT payroll_title, payroll_month, payroll_year, payroll_status,
                     COALESCE(total_amount,0) AS total_amount, payroll_date
              FROM hrm_payroll
              ORDER BY payroll_year DESC, ID DESC
              LIMIT 8");

        if (dt.Rows.Count == 0)
        {
            litRecentPayrolls.Text = "<div class='hr-empty'>No payroll runs yet.</div>";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("<table class='hr-table'><thead><tr>");
        sb.Append("<th>Title</th><th>Period</th><th>Total</th><th>Status</th></tr></thead><tbody>");

        foreach (DataRow r in dt.Rows)
        {
            string status = r["payroll_status"].ToString();
            sb.AppendFormat("<tr><td>{0}</td><td>{1} {2}</td><td style='font-weight:600;'>{3}</td><td>{4}</td></tr>",
                System.Web.HttpUtility.HtmlEncode(r["payroll_title"].ToString()),
                FormatMonth(r["payroll_month"].ToString()),
                r["payroll_year"],
                FormatUGX(r["total_amount"]),
                StatusBadge(status));
        }

        sb.Append("</tbody></table>");
        litRecentPayrolls.Text = sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Expiring contracts
    // -------------------------------------------------------------------------

    private void LoadExpiringContracts(MySqlConnection conn)
    {
        DataTable dt = Query(conn,
            @"SELECT e.emp_name, e.EMP_CODE, c.contractEnd, c.contractStatus
              FROM hrm_emp_contracts c
              INNER JOIN hrm_employee e ON e.empID = c.empID
              WHERE c.contractStatus = 'VALID'
                AND c.contractEnd BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)
              ORDER BY c.contractEnd ASC
              LIMIT 10");

        if (dt.Rows.Count == 0)
        {
            litExpiringContracts.Text = "<div class='hr-empty'>No contracts expiring in the next 30 days.</div>";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("<table class='hr-table'><thead><tr><th>Employee</th><th>Code</th><th>Expires</th></tr></thead><tbody>");

        foreach (DataRow r in dt.Rows)
        {
            DateTime expiry = Convert.ToDateTime(r["contractEnd"]);
            int daysLeft = (expiry - DateTime.Today).Days;
            string badge = daysLeft <= 7
                ? "<span class='hr-badge hr-badge--expired'>" + daysLeft + " days</span>"
                : "<span class='hr-badge hr-badge--expiring'>" + daysLeft + " days</span>";

            sb.AppendFormat("<tr><td>{0}</td><td style='color:#888;'>{1}</td><td>{2} {3}</td></tr>",
                System.Web.HttpUtility.HtmlEncode(r["emp_name"].ToString()),
                System.Web.HttpUtility.HtmlEncode(r["EMP_CODE"].ToString()),
                expiry.ToString("dd MMM yyyy"),
                badge);
        }

        sb.Append("</tbody></table>");
        litExpiringContracts.Text = sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Pending payslips
    // -------------------------------------------------------------------------

    private void LoadPendingPayslips(MySqlConnection conn)
    {
        DataTable dt = Query(conn,
            @"SELECT e.emp_name, ps.payroll_month_name, ps.payroll_year, ps.net_salary
              FROM hrm_payslips ps
              INNER JOIN hrm_employee e ON e.empID = ps.empID
              WHERE ps.status = 'PENDING'
              ORDER BY ps.payroll_year DESC, ps.payroll_month DESC, e.emp_name
              LIMIT 10");

        if (dt.Rows.Count == 0)
        {
            litPendingPayslips.Text = "<div class='hr-empty'>No payslips pending approval.</div>";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("<table class='hr-table'><thead><tr><th>Employee</th><th>Period</th><th>Net Pay</th></tr></thead><tbody>");

        foreach (DataRow r in dt.Rows)
        {
            sb.AppendFormat("<tr><td>{0}</td><td>{1} {2}</td><td style='font-weight:600;'>{3}</td></tr>",
                System.Web.HttpUtility.HtmlEncode(r["emp_name"].ToString()),
                r["payroll_month_name"],
                r["payroll_year"],
                FormatUGX(r["net_salary"]));
        }

        sb.Append("</tbody></table>");
        litPendingPayslips.Text = sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Alerts
    // -------------------------------------------------------------------------

    private void LoadAlerts(MySqlConnection conn)
    {
        StringBuilder sb = new StringBuilder();

        // Pending payslips
        long pendingSlips = Scalar(conn, "SELECT COUNT(*) FROM hrm_payslips WHERE status = 'PENDING'");
        if (pendingSlips > 0)
            sb.AppendFormat("<div class='hr-alert hr-alert--warn'><strong>{0} payslip(s)</strong> are awaiting approval. <a href='HRPayslips.aspx' style='color:inherit;font-weight:600;'>Review now</a></div>", pendingSlips);

        // Expiring contracts
        long expiring = Scalar(conn,
            "SELECT COUNT(*) FROM hrm_emp_contracts WHERE contractStatus='VALID' AND contractEnd BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)");
        if (expiring > 0)
            sb.AppendFormat("<div class='hr-alert hr-alert--warn'><strong>{0} contract(s)</strong> expire within 30 days. <a href='HRContracts.aspx' style='color:inherit;font-weight:600;'>View contracts</a></div>", expiring);

        // Contract renewals: applications waiting for HR, and contracts ending within 3 months with no application
        try
        {
            long renewalsWithHr = Scalar(conn, "SELECT COUNT(*) FROM hr_contract_renewals WHERE status = 'AWAITING_HR'");
            long expiringNoApp = Scalar(conn,
                @"SELECT COUNT(*) FROM (SELECT e.empID, hr_current_contract_id(e.empID) AS cid FROM hrm_employee e
                                         WHERE IFNULL(e.employment_status,'ACTIVE') = 'ACTIVE') x
                  JOIN hrm_emp_contracts c ON c.ID = x.cid
                  WHERE c.contractStatus NOT IN ('TERMINATED','RESIGNED')
                    AND c.contractEnd BETWEEN DATE_SUB(CURDATE(), INTERVAL 60 DAY) AND DATE_ADD(CURDATE(), INTERVAL 90 DAY)
                    AND NOT EXISTS (SELECT 1 FROM hr_contract_renewals a WHERE a.contract_id = c.ID AND a.status <> 'WITHDRAWN')");
            if (renewalsWithHr > 0 || expiringNoApp > 0)
                sb.AppendFormat("<div class='hr-alert hr-alert--warn'>Contract renewals: <strong>{0}</strong> application(s) awaiting HR verification &middot; <strong>{1}</strong> contract(s) ending within 3 months with no application. <a href='ContractRenewals.aspx' style='color:inherit;font-weight:600;'>Open Contract Renewals</a></div>",
                    renewalsWithHr, expiringNoApp);
        }
        catch { /* renewal tables missing - keep the dashboard working */ }

        // Pending payroll runs (not yet processed)
        long pendingPayrolls = Scalar(conn, "SELECT COUNT(*) FROM hrm_payroll WHERE payroll_status = 'PENDING'");
        if (pendingPayrolls > 0)
            sb.AppendFormat("<div class='hr-alert hr-alert--info'><strong>{0} payroll run(s)</strong> are pending processing. <a href='HRPayroll.aspx' style='color:inherit;font-weight:600;'>Go to Payroll</a></div>", pendingPayrolls);

        // Pending allowances
        long pendingAllow = Scalar(conn, "SELECT COUNT(*) FROM hrm_allowance_records WHERE status = 'PENDING'");
        if (pendingAllow > 0)
            sb.AppendFormat("<div class='hr-alert hr-alert--info'><strong>{0} pending allowance(s)</strong> will be included in next payroll run. <a href='HRAllowances.aspx' style='color:inherit;font-weight:600;'>View</a></div>", pendingAllow);

        // Pending deductions
        long pendingDed = Scalar(conn, "SELECT COUNT(*) FROM hrm_deduction_records WHERE status = 'PENDING'");
        if (pendingDed > 0)
            sb.AppendFormat("<div class='hr-alert hr-alert--info'><strong>{0} pending deduction(s)</strong> will be included in next payroll run. <a href='HRDeductions.aspx' style='color:inherit;font-weight:600;'>View</a></div>", pendingDed);

        // Check hrm_config exists and is configured
        try
        {
            long cfgCount = Scalar(conn, "SELECT COUNT(*) FROM hrm_config");
            if (cfgCount == 0)
                sb.Append("<div class='hr-alert hr-alert--error'><strong>Payroll configuration not set.</strong> <a href='HRConfig.aspx' style='color:inherit;font-weight:600;'>Configure now</a></div>");
            else
                sb.Append("<div class='hr-alert hr-alert--ok'><strong>Payroll configuration</strong> is active.</div>");
        }
        catch
        {
            sb.Append("<div class='hr-alert hr-alert--error'><strong>hrm_config table missing.</strong> <a href='HRConfig.aspx' style='color:inherit;font-weight:600;'>Open HRConfig to auto-create it</a></div>");
        }

        if (sb.Length == 0)
            sb.Append("<div class='hr-alert hr-alert--ok'>No outstanding alerts. Everything is in order.</div>");

        litAlerts.Text = sb.ToString();
    }

    // -------------------------------------------------------------------------
    // DB helpers
    // -------------------------------------------------------------------------

    private long Scalar(MySqlConnection conn, string sql, params MySqlParameter[] parms)
    {
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            if (parms != null)
                foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            object val = cmd.ExecuteScalar();
            if (val == null || val == DBNull.Value) return 0;
            long result;
            return long.TryParse(val.ToString(), out result) ? result : 0;
        }
    }

    private decimal ScalarDecimal(MySqlConnection conn, string sql, params MySqlParameter[] parms)
    {
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            if (parms != null)
                foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            object val = cmd.ExecuteScalar();
            if (val == null || val == DBNull.Value) return 0m;
            decimal result;
            return decimal.TryParse(val.ToString(), out result) ? result : 0m;
        }
    }

    private DataTable Query(MySqlConnection conn, string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            if (parms != null)
                foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd))
                da.Fill(dt);
        }
        return dt;
    }

    // -------------------------------------------------------------------------
    // Format helpers
    // -------------------------------------------------------------------------

    private string FormatUGX(object val)
    {
        decimal amount = 0m;
        if (val != null && val != DBNull.Value)
            decimal.TryParse(val.ToString(), out amount);

        if (amount >= 1000000000m)
            return string.Format("UGX {0:F1}B", amount / 1000000000m);
        if (amount >= 1000000m)
            return string.Format("UGX {0:F1}M", amount / 1000000m);
        return string.Format("UGX {0:N0}", amount);
    }

    private string StatusBadge(string status)
    {
        switch ((status ?? "").ToUpper())
        {
            case "PENDING":   return "<span class='hr-badge hr-badge--pending'>Pending</span>";
            case "PROCESSED": return "<span class='hr-badge hr-badge--processed'>Processed</span>";
            case "CANCELLED": return "<span class='hr-badge hr-badge--cancelled'>Cancelled</span>";
            default:          return "<span class='hr-badge'>" + System.Web.HttpUtility.HtmlEncode(status) + "</span>";
        }
    }

    private string FormatMonth(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        int num;
        if (int.TryParse(raw, out num) && num >= 1 && num <= 12)
            return new DateTime(2000, num, 1).ToString("MMMM").ToUpper();
        return raw.ToUpper();
    }
}
