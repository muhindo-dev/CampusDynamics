using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Business-logic manager for payslip lifecycle: query, approve, reject, batch operations.
/// Used by HRPayslips.aspx and any future controllers / API endpoints.
/// </summary>
public static class HRPayslipManager
{
    // ─────────────────────────────────────────────────────────────────
    // Infrastructure
    // ─────────────────────────────────────────────────────────────────

    private static string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private static DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        var dt = new DataTable();
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (var p in parms) cmd.Parameters.Add(p);
                using (var da = new MySqlDataAdapter(cmd)) da.Fill(dt);
            }
        }
        return dt;
    }

    private static int ExecuteNonQuery(string sql, params MySqlParameter[] parms)
    {
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (var p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    private static decimal SafeDecimal(object val)
    {
        if (val == null || val == DBNull.Value) return 0m;
        decimal d;
        return decimal.TryParse(val.ToString(), out d) ? d : 0m;
    }

    private static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int i;
        return int.TryParse(val.ToString(), out i) ? i : 0;
    }

    private static string SafeStr(object val)
    {
        return (val == null || val == DBNull.Value) ? "" : val.ToString();
    }

    // ─────────────────────────────────────────────────────────────────
    // Query helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns payslips filtered by any combination of payrollId, empId, status,
    /// monthName (e.g. "MARCH"), year, or search (matches emp_name / EMP_CODE).
    /// Pass null / empty string to omit a filter.
    /// </summary>
    public static DataTable GetPayslips(
        string payrollId  = null,
        string empId      = null,
        string status     = null,
        string monthName  = null,
        string year       = null,
        string search     = null)
    {
        var sb    = new StringBuilder();
        var parms = new List<MySqlParameter>();

        // Decide whether we need the employee join (always needed for display + search)
        sb.Append(
            "SELECT ps.ID, ps.payroll_id, p.payroll_title, ps.empID, " +
            "e.EMP_CODE, e.emp_name, " +
            "ps.payroll_month_name, ps.payroll_year, " +
            "ps.basic_pay, ps.total_allowances, ps.total_deductions, " +
            "ps.paye, ps.nssf, ps.kabaka_contribution, ps.local_tax, " +
            "ps.gross_salary, ps.net_salary, ps.status, ps.date_generated, " +
            "ps.approved_by, ps.allowance_details, ps.deduction_details " +
            "FROM hrm_payslips ps " +
            "JOIN hrm_payroll p  ON p.ID  = ps.payroll_id " +
            "JOIN hrm_employee e ON e.empID = ps.empID " +
            "WHERE 1=1");

        if (!string.IsNullOrEmpty(payrollId))
        {
            sb.Append(" AND ps.payroll_id = @payroll");
            parms.Add(new MySqlParameter("@payroll", payrollId));
        }
        if (!string.IsNullOrEmpty(empId))
        {
            sb.Append(" AND ps.empID = @emp");
            parms.Add(new MySqlParameter("@emp", empId));
        }
        if (!string.IsNullOrEmpty(status))
        {
            sb.Append(" AND ps.status = @status");
            parms.Add(new MySqlParameter("@status", status));
        }
        if (!string.IsNullOrEmpty(monthName))
        {
            sb.Append(" AND ps.payroll_month_name = @month");
            parms.Add(new MySqlParameter("@month", monthName.ToUpper()));
        }
        if (!string.IsNullOrEmpty(year))
        {
            sb.Append(" AND ps.payroll_year = @year");
            parms.Add(new MySqlParameter("@year", year));
        }
        if (!string.IsNullOrEmpty(search))
        {
            sb.Append(" AND (e.emp_name LIKE @search OR e.EMP_CODE LIKE @search)");
            parms.Add(new MySqlParameter("@search", "%" + search + "%"));
        }

        sb.Append(" ORDER BY ps.payroll_year DESC, ps.payroll_month DESC, e.emp_name");

        return ExecuteQuery(sb.ToString(), parms.ToArray());
    }

    /// <summary>Returns a single payslip row by ID (includes employee and payroll data).</summary>
    public static DataRow GetPayslip(int id)
    {
        DataTable dt = ExecuteQuery(
            "SELECT ps.*, e.emp_name, e.EMP_CODE, p.payroll_title " +
            "FROM hrm_payslips ps " +
            "JOIN hrm_employee e ON e.empID = ps.empID " +
            "JOIN hrm_payroll  p ON p.ID    = ps.payroll_id " +
            "WHERE ps.ID = @id",
            new MySqlParameter("@id", id));
        return (dt != null && dt.Rows.Count > 0) ? dt.Rows[0] : null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Stats
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a DataRow with columns: total, pending, approved, rejected.
    /// Same optional filters as GetPayslips.
    /// </summary>
    public static DataRow GetPayslipStats(
        string payrollId = null,
        string empId     = null,
        string status    = null,
        string monthName = null,
        string year      = null,
        string search    = null)
    {
        var sb    = new StringBuilder();
        var parms = new List<MySqlParameter>();

        bool needsEmpJoin = !string.IsNullOrEmpty(empId) || !string.IsNullOrEmpty(search);

        sb.Append(
            "SELECT " +
            "COUNT(*) AS total, " +
            "SUM(CASE WHEN ps.status='PENDING'  THEN 1 ELSE 0 END) AS pending, " +
            "SUM(CASE WHEN ps.status='APPROVED' THEN 1 ELSE 0 END) AS approved, " +
            "SUM(CASE WHEN ps.status='REJECTED' THEN 1 ELSE 0 END) AS rejected, " +
            "IFNULL(SUM(ps.net_salary), 0) AS total_net " +
            "FROM hrm_payslips ps ");

        if (needsEmpJoin)
            sb.Append("JOIN hrm_employee e ON e.empID = ps.empID ");

        sb.Append("WHERE 1=1");

        if (!string.IsNullOrEmpty(payrollId))
        {
            sb.Append(" AND ps.payroll_id = @payroll");
            parms.Add(new MySqlParameter("@payroll", payrollId));
        }
        if (!string.IsNullOrEmpty(empId))
        {
            sb.Append(" AND ps.empID = @emp");
            parms.Add(new MySqlParameter("@emp", empId));
        }
        if (!string.IsNullOrEmpty(status))
        {
            sb.Append(" AND ps.status = @status");
            parms.Add(new MySqlParameter("@status", status));
        }
        if (!string.IsNullOrEmpty(monthName))
        {
            sb.Append(" AND ps.payroll_month_name = @month");
            parms.Add(new MySqlParameter("@month", monthName.ToUpper()));
        }
        if (!string.IsNullOrEmpty(year))
        {
            sb.Append(" AND ps.payroll_year = @year");
            parms.Add(new MySqlParameter("@year", year));
        }
        if (!string.IsNullOrEmpty(search))
        {
            sb.Append(" AND (e.emp_name LIKE @search OR e.EMP_CODE LIKE @search)");
            parms.Add(new MySqlParameter("@search", "%" + search + "%"));
        }

        DataTable dt = ExecuteQuery(sb.ToString(), parms.ToArray());
        return (dt != null && dt.Rows.Count > 0) ? dt.Rows[0] : null;
    }

    // ─────────────────────────────────────────────────────────────────
    // Dropdown helpers
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Returns ID + payroll_title for all payrolls (for filter dropdowns).</summary>
    public static DataTable GetPayrollsForDropdown()
    {
        return ExecuteQuery(
            "SELECT ID, payroll_title, payroll_year, payroll_month " +
            "FROM hrm_payroll " +
            "ORDER BY payroll_year DESC, payroll_month DESC");
    }

    /// <summary>Returns employees that have at least one payslip (for filter dropdowns).</summary>
    public static DataTable GetEmployeesWithPayslips()
    {
        return ExecuteQuery(
            "SELECT DISTINCT e.empID, e.emp_name, IFNULL(e.EMP_CODE,'') AS EMP_CODE " +
            "FROM hrm_payslips ps " +
            "JOIN hrm_employee e ON e.empID = ps.empID " +
            "ORDER BY e.emp_name");
    }

    // ─────────────────────────────────────────────────────────────────
    // Approve / Reject
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Approves a single payslip.
    /// Returns null on success, or an error string on failure.
    /// </summary>
    public static string ApprovePayslip(int id, string approvedBy)
    {
        try
        {
            int rows = ExecuteNonQuery(
                "UPDATE hrm_payslips " +
                "SET status='APPROVED', approved_by=@user, date_approved=NOW(), rejection_reason=NULL " +
                "WHERE ID=@id AND status != 'APPROVED'",
                new MySqlParameter("@user", approvedBy),
                new MySqlParameter("@id",   id));

            if (rows == 0)
                return "Payslip could not be approved (already approved or not found).";

            return null; // success
        }
        catch (Exception ex)
        {
            return "Error approving payslip: " + ex.Message;
        }
    }

    /// <summary>
    /// Rejects a single payslip with an optional reason.
    /// Returns null on success, or an error string on failure.
    /// </summary>
    public static string RejectPayslip(int id, string reason, string rejectedBy)
    {
        try
        {
            int rows = ExecuteNonQuery(
                "UPDATE hrm_payslips " +
                "SET status='REJECTED', rejection_reason=@reason, approved_by=NULL, date_approved=NULL " +
                "WHERE ID=@id",
                new MySqlParameter("@reason", reason ?? ""),
                new MySqlParameter("@id",     id));

            if (rows == 0)
                return "Payslip not found or could not be updated.";

            return null; // success
        }
        catch (Exception ex)
        {
            return "Error rejecting payslip: " + ex.Message;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Batch operations
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Approves multiple payslips in a single connection.
    /// Returns the number of rows updated.
    /// Throws on DB error.
    /// </summary>
    public static int BatchApprovePayslips(IEnumerable<int> ids, string approvedBy)
    {
        int updated = 0;
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            foreach (int id in ids)
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE hrm_payslips " +
                    "SET status='APPROVED', approved_by=@user, date_approved=NOW(), rejection_reason=NULL " +
                    "WHERE ID=@id AND status != 'APPROVED'", conn))
                {
                    cmd.Parameters.AddWithValue("@user", approvedBy);
                    cmd.Parameters.AddWithValue("@id",   id);
                    updated += cmd.ExecuteNonQuery();
                }
            }
        }
        return updated;
    }

    /// <summary>
    /// Rejects multiple payslips in a single connection.
    /// Returns the number of rows updated.
    /// Throws on DB error.
    /// </summary>
    public static int BatchRejectPayslips(IEnumerable<int> ids, string reason, string rejectedBy)
    {
        int updated = 0;
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            foreach (int id in ids)
            {
                using (var cmd = new MySqlCommand(
                    "UPDATE hrm_payslips " +
                    "SET status='REJECTED', rejection_reason=@reason, approved_by=NULL, date_approved=NULL " +
                    "WHERE ID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@reason", reason ?? ("Batch rejected by " + rejectedBy));
                    cmd.Parameters.AddWithValue("@id",     id);
                    updated += cmd.ExecuteNonQuery();
                }
            }
        }
        return updated;
    }

    // ─────────────────────────────────────────────────────────────────
    // Payslip HTML generator (print/view)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates the full HTML for a single payslip detail view.
    /// Returns an HTML string ready to inject into a modal or print window.
    /// Returns an error paragraph on failure.
    /// </summary>
    public static string GeneratePayslipHtml(int payslipID)
    {
        try
        {
            DataTable dt = ExecuteQuery(
                "SELECT ps.*, e.emp_name, e.EMP_CODE, p.payroll_title " +
                "FROM hrm_payslips ps " +
                "JOIN hrm_employee e ON e.empID = ps.empID " +
                "JOIN hrm_payroll  p ON p.ID    = ps.payroll_id " +
                "WHERE ps.ID = @id",
                new MySqlParameter("@id", payslipID));

            if (dt == null || dt.Rows.Count == 0)
                return "<p style='color:#dc3545;font-size:12px;padding:16px;'>Payslip not found.</p>";

            DataRow r = dt.Rows[0];

            string empName      = SafeStr(r["emp_name"]);
            string empCode      = SafeStr(r["EMP_CODE"]);
            string payrollTitle = SafeStr(r["payroll_title"]);
            string monthName    = SafeStr(r["payroll_month_name"]);
            string year         = SafeStr(r["payroll_year"]);
            decimal basicPay    = SafeDecimal(r["basic_pay"]);
            decimal grossSalary = SafeDecimal(r["gross_salary"]);
            decimal totalAllow  = SafeDecimal(r["total_allowances"]);
            decimal totalDed    = SafeDecimal(r["total_deductions"]);
            decimal paye        = SafeDecimal(r["paye"]);
            decimal nssf        = SafeDecimal(r["nssf"]);
            decimal kabaka      = SafeDecimal(r["kabaka_contribution"]);
            decimal localTax    = SafeDecimal(r["local_tax"]);
            decimal netSalary   = SafeDecimal(r["net_salary"]);
            string status       = SafeStr(r["status"]);
            string approvedBy   = SafeStr(r["approved_by"]);
            string allowDet     = SafeStr(r["allowance_details"]);
            string dedDet       = SafeStr(r["deduction_details"]);

            DateTime dateGenerated = DateTime.MinValue;
            if (r["date_generated"] != DBNull.Value)
                DateTime.TryParse(r["date_generated"].ToString(), out dateGenerated);

            var html = new StringBuilder();
            html.Append("<div class=\"payslip-print\">");

            // ── Header ────────────────────────────────────────────────────
            html.Append("<div class=\"payslip-print__header\">");
            html.Append("<h2>Muteesa I Royal University</h2>");
            html.AppendFormat("<h3>PAYSLIP &mdash; {0} {1}</h3>", monthName, year);
            html.Append("<div class=\"payslip-emp-info\">");
            html.AppendFormat("<div><strong>Employee:</strong> {0}</div>",
                HttpUtility.HtmlEncode(empName));
            html.AppendFormat("<div><strong>Staff #:</strong> {0}</div>",
                HttpUtility.HtmlEncode(empCode));
            html.AppendFormat("<div><strong>Payroll:</strong> {0}</div>",
                HttpUtility.HtmlEncode(payrollTitle));
            html.AppendFormat("<div><strong>Generated:</strong> {0}</div>",
                dateGenerated != DateTime.MinValue ? dateGenerated.ToString("dd MMM yyyy HH:mm") : "—");
            html.Append("</div>"); // payslip-emp-info

            // Status badge
            string badgeClass = status == "APPROVED" ? "ps-badge--approved"
                              : status == "REJECTED"  ? "ps-badge--rejected"
                              :                         "ps-badge--pending";
            html.AppendFormat("<div><span class=\"ps-badge {0}\">{1}</span></div>",
                badgeClass, status);
            if (!string.IsNullOrEmpty(approvedBy))
                html.AppendFormat("<div class=\"payslip-approved-by\">Approved by: {0}</div>",
                    HttpUtility.HtmlEncode(approvedBy));
            html.Append("</div>"); // payslip-print__header

            // ── Earnings table ────────────────────────────────────────────
            html.Append("<div class=\"payslip-section\">");
            html.Append("<h4>Earnings</h4>");
            html.Append("<table class=\"payslip-table\">");
            html.Append("<thead><tr><th>Description</th><th>Amount (UGX)</th></tr></thead>");
            html.Append("<tbody>");
            html.AppendFormat("<tr><td>Basic Pay</td><td class=\"text-right\">{0}</td></tr>",
                basicPay.ToString("N0"));

            // Ad-hoc / named allowances from pipe-delimited allowance_details
            if (!string.IsNullOrEmpty(allowDet))
            {
                string[] items = allowDet.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string item in items)
                {
                    string[] parts = item.Split(':');
                    if (parts.Length == 2)
                    {
                        decimal amt;
                        string amtStr = decimal.TryParse(parts[1], out amt) ? amt.ToString("N0") : parts[1];
                        html.AppendFormat("<tr><td>{0}</td><td class=\"text-right\">{1}</td></tr>",
                            HttpUtility.HtmlEncode(parts[0].Trim()), amtStr);
                    }
                }
            }

            html.AppendFormat(
                "<tr class=\"payslip-subtotal\"><td><strong>Gross Salary</strong></td>" +
                "<td class=\"text-right\"><strong>{0}</strong></td></tr>",
                grossSalary.ToString("N0"));
            html.Append("</tbody></table>");
            html.Append("</div>"); // earnings section

            // ── Deductions table ──────────────────────────────────────────
            html.Append("<div class=\"payslip-section\">");
            html.Append("<h4>Deductions</h4>");
            html.Append("<table class=\"payslip-table\">");
            html.Append("<thead><tr><th>Description</th><th>Amount (UGX)</th></tr></thead>");
            html.Append("<tbody>");

            // Statutory deductions
            if (paye > 0)
                html.AppendFormat("<tr><td>PAYE</td><td class=\"text-right\">{0}</td></tr>",
                    paye.ToString("N0"));
            if (nssf > 0)
                html.AppendFormat("<tr><td>NSSF (Employee 5%)</td><td class=\"text-right\">{0}</td></tr>",
                    nssf.ToString("N0"));
            if (kabaka > 0)
                html.AppendFormat("<tr><td>Kabaka Contribution (1%)</td><td class=\"text-right\">{0}</td></tr>",
                    kabaka.ToString("N0"));
            if (localTax > 0)
                html.AppendFormat("<tr><td>Local Service Tax</td><td class=\"text-right\">{0}</td></tr>",
                    localTax.ToString("N0"));

            // Ad-hoc / named deductions from pipe-delimited deduction_details
            if (!string.IsNullOrEmpty(dedDet))
            {
                string[] items = dedDet.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string item in items)
                {
                    string[] parts = item.Split(':');
                    if (parts.Length == 2)
                    {
                        decimal amt;
                        string amtStr = decimal.TryParse(parts[1], out amt) ? amt.ToString("N0") : parts[1];
                        html.AppendFormat("<tr><td>{0}</td><td class=\"text-right\">{1}</td></tr>",
                            HttpUtility.HtmlEncode(parts[0].Trim()), amtStr);
                    }
                }
            }

            html.AppendFormat(
                "<tr class=\"payslip-subtotal\"><td><strong>Total Deductions</strong></td>" +
                "<td class=\"text-right\"><strong>{0}</strong></td></tr>",
                totalDed.ToString("N0"));
            html.Append("</tbody></table>");
            html.Append("</div>"); // deductions section

            // ── Net salary footer ─────────────────────────────────────────
            html.Append("<div class=\"payslip-net\">");
            html.AppendFormat(
                "<div class=\"payslip-net__label\">NET SALARY</div>" +
                "<div class=\"payslip-net__amount\">UGX {0}</div>",
                netSalary.ToString("N0"));
            html.Append("</div>");

            html.Append("</div>"); // payslip-print

            return html.ToString();
        }
        catch (Exception ex)
        {
            return "<p style='color:#dc3545;font-size:12px;padding:16px;'>Error loading payslip: "
                   + HttpUtility.HtmlEncode(ex.Message) + "</p>";
        }
    }
}
