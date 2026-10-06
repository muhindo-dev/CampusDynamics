using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Payslips: review queue by payroll run, approve and reject (single and batch with a shared
/// reason), and the payslip print (single, or every payslip of a run, one per page).
/// Approve and reject are refused when the payslip's run is not pending (approved runs are locked).
/// </summary>
public partial class COOPERP_NewScreens_HRPayslips : System.Web.UI.Page
{
    private const int PageSize = 50;
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    private class UserError : Exception { public UserError(string m) : base(m) { } }

    // =================================================================
    //  Infrastructure
    // =================================================================

    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private static MySqlParameter P(string name, object value) { return new MySqlParameter(name, value ?? DBNull.Value); }

    private DataTable Q(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, c))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) da.Fill(dt);
            }
        }
        return dt;
    }

    private int X(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, c))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    private static decimal Dec(object v)
    {
        if (v == null || v == DBNull.Value) return 0m;
        decimal d;
        return decimal.TryParse(v.ToString(), NumberStyles.Any, IC, out d) ? d : 0m;
    }

    private static int Int(object v)
    {
        if (v == null || v == DBNull.Value) return 0;
        int i;
        return int.TryParse(v.ToString(), out i) ? i : 0;
    }

    private static string Str(object v) { return v == null || v == DBNull.Value ? "" : v.ToString().Trim(); }
    private static string E(object s) { return HttpUtility.HtmlEncode(HrExport.Clean(Str(s))); }
    private static string Money(object v) { return Dec(v).ToString("#,##0", IC); }
    private static string Trunc(string s, int n) { s = s ?? ""; return s.Length <= n ? s : s.Substring(0, n); }
    private static string Title(string s) { return IC.TextInfo.ToTitleCase((s ?? "").Trim().ToLowerInvariant()); }

    private static string Period(object monthName, object year)
    {
        return (Title(Str(monthName)) + " " + Str(year)).Trim();
    }

    private static string Clean(object v)
    {
        string s = Str(v);
        return s == "-" || s == "0" ? "" : s;
    }

    private string CurrentUser() { return HrAccess.Username(); }

    private void Log(string what, string detail)
    {
        try
        {
            X("INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u, 'HR Payslips', @p, @c, NOW())",
                P("@u", Trunc(CurrentUser(), 100)), P("@p", Trunc(detail, 300)), P("@c", Trunc(what, 200)));
        }
        catch { }
    }

    private static string Badge(string status)
    {
        string s = (status ?? "").ToUpperInvariant();
        string kind = s == "APPROVED" ? "ok" : s == "REJECTED" ? "bad" : "warn";
        string word = s == "APPROVED" ? "Approved" : s == "REJECTED" ? "Rejected" : "Pending";
        return "<span class=\"hr-badge hr-badge--" + kind + "\">" + word + "</span>";
    }

    // =================================================================
    //  Page load and dispatch
    // =================================================================

    protected void Page_Load(object sender, EventArgs e)
    {
        string action = Request.QueryString["action"];
        if (!string.IsNullOrEmpty(action))
        {
            bool file = action == "print";
            if (!HrAccess.RequireHr(!file)) return;
            if (file)
            {
                HandlePrint();
                Response.End();
                return;
            }
            string json;
            try { json = HandleAction(action); }
            catch (UserError ue) { json = Result(false, ue.Message); }
            catch (Exception ex)
            {
                Log("Error", action + ": " + ex.Message);
                json = Result(false, "The action could not be completed. Please try again.");
            }
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(json);
            Response.End();
            return;
        }

        if (!HrAccess.RequireHr(false)) return;
        try { Render(); }
        catch (Exception ex)
        {
            Log("Error", "Page load: " + ex.Message);
            litBody.Text = "<div class=\"hr-notice hr-notice--bad\">Payslips could not be loaded. Please refresh the page.</div>";
        }
    }

    private static string Result(bool ok, string message)
    {
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["ok"] = ok;
        d["message"] = message;
        return new JavaScriptSerializer().Serialize(d);
    }

    private string HandleAction(string action)
    {
        if (Request.HttpMethod != "POST") throw new UserError("Invalid request.");
        List<int> ids = new List<int>();
        foreach (string s in (Request.Form["ids"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int v;
            if (int.TryParse(s.Trim(), out v) && v > 0 && !ids.Contains(v)) ids.Add(v);
        }
        if (ids.Count == 0) throw new UserError("Select at least one payslip.");
        string reason = (Request.Form["reason"] ?? "").Trim();

        if (action == "approve") return Apply(ids, true, "");
        if (action == "reject")
        {
            if (reason == "") throw new UserError("Enter the reason for rejecting.");
            if (reason.Length > 500) throw new UserError("The reason can have at most 500 characters.");
            return Apply(ids, false, reason);
        }
        throw new UserError("Unknown action.");
    }

    /// <summary>Approves or rejects payslips whose run is pending. Locked or cancelled runs are skipped.</summary>
    private string Apply(List<int> ids, bool approve, string reason)
    {
        int done = 0, locked = 0, unchanged = 0;
        string user = CurrentUser();
        using (MySqlConnection c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                foreach (int id in ids)
                {
                    string runStatus = "";
                    using (MySqlCommand cmd = new MySqlCommand("SELECT p.payroll_status FROM hrm_payslips ps JOIN hrm_payroll p ON p.ID = ps.payroll_id WHERE ps.ID=@id", c, tx))
                    {
                        cmd.Parameters.Add(P("@id", id));
                        object o = cmd.ExecuteScalar();
                        runStatus = o == null || o == DBNull.Value ? "" : o.ToString();
                    }
                    if (runStatus != "PENDING") { locked++; continue; }

                    string sql = approve
                        ? "UPDATE hrm_payslips SET status='APPROVED', approved_by=@u, date_approved=NOW(), rejection_reason=NULL WHERE ID=@id AND status<>'APPROVED'"
                        : "UPDATE hrm_payslips SET status='REJECTED', rejection_reason=@r, approved_by=NULL, date_approved=NULL WHERE ID=@id";
                    using (MySqlCommand cmd = new MySqlCommand(sql, c, tx))
                    {
                        cmd.Parameters.Add(P("@id", id));
                        if (approve) cmd.Parameters.Add(P("@u", user)); else cmd.Parameters.Add(P("@r", reason));
                        if (cmd.ExecuteNonQuery() > 0) done++; else unchanged++;
                    }
                }
                tx.Commit();
            }
        }
        Log(approve ? "Payslips approved" : "Payslips rejected", done + " payslip(s): " + Trunc(string.Join(",", ids.ConvertAll<string>(delegate (int i) { return i.ToString(IC); }).ToArray()), 200)
            + (approve ? "" : ". Reason: " + reason));

        string msg = done + (done == 1 ? " payslip " : " payslips ") + (approve ? "approved." : "rejected.");
        if (locked > 0) msg += " " + locked + " skipped because the payroll run is locked or cancelled.";
        if (unchanged > 0 && approve) msg += " " + unchanged + " already approved.";
        if (done == 0 && locked > 0 && ids.Count == locked)
            throw new UserError("This payroll run is locked or cancelled. Its payslips cannot be changed.");
        return Result(true, msg);
    }

    // =================================================================
    //  Screen
    // =================================================================

    private const string IcoPrint = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><polyline points=\"6 9 6 2 18 2 18 9\"/><path d=\"M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2\"/><rect x=\"6\" y=\"14\" width=\"12\" height=\"8\"/></svg>";

    private string Url(string run, string status, string q, int page)
    {
        List<string> parts = new List<string>();
        if (run != "") parts.Add("payroll_id=" + HttpUtility.UrlEncode(run));
        if (status != "") parts.Add("status=" + status);
        if (q != "") parts.Add("q=" + HttpUtility.UrlEncode(q));
        if (page > 1) parts.Add("page=" + page);
        return "HRPayslips.aspx" + (parts.Count > 0 ? "?" + string.Join("&", parts.ToArray()) : "");
    }

    private void Render()
    {
        DataTable runs = Q(@"SELECT p.ID, p.payroll_title, p.payroll_month, p.payroll_year, p.payroll_status, p.payroll_date,
                                    (SELECT COUNT(*) FROM hrm_payslips ps WHERE ps.payroll_id = p.ID) AS slips
                             FROM hrm_payroll p ORDER BY p.payroll_year DESC, CAST(p.payroll_month AS UNSIGNED) DESC, p.ID DESC");
        Dictionary<string, int> titleCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow r in runs.Rows)
        {
            string t = Str(r["payroll_title"]);
            titleCount[t] = titleCount.ContainsKey(t) ? titleCount[t] + 1 : 1;
        }

        string run = Request.QueryString["payroll_id"];
        if (run == null)
        {
            run = "";
            foreach (DataRow r in runs.Rows)
                if (Str(r["payroll_status"]) != "CANCELLED" && Int(r["slips"]) > 0) { run = Str(r["ID"]); break; }
        }
        else if (run != "all") run = Int(run) > 0 ? Int(run).ToString(IC) : "all";
        string runFilter = run == "all" ? "" : run;

        string status = (Request.QueryString["status"] ?? "").ToUpperInvariant();
        if (status != "PENDING" && status != "APPROVED" && status != "REJECTED") status = "";
        string q = (Request.QueryString["q"] ?? "").Trim();
        if (q.Length > 60) q = q.Substring(0, 60);
        int page = Math.Max(1, Int(Request.QueryString["page"]));

        DataRow runRow = null;
        foreach (DataRow r in runs.Rows) if (Str(r["ID"]) == runFilter) { runRow = r; break; }
        string runStatus = runRow == null ? "" : Str(runRow["payroll_status"]);
        bool editable = runRow == null || runStatus == "PENDING";

        StringBuilder h = new StringBuilder();

        // KPIs for the selected run
        List<MySqlParameter> kp = new List<MySqlParameter>();
        string kWhere = "";
        if (runFilter != "") { kWhere = " WHERE payroll_id=@run"; kp.Add(P("@run", runFilter)); }
        DataTable k = Q("SELECT COUNT(*) AS n, COALESCE(SUM(status='PENDING'),0) AS p, COALESCE(SUM(status='APPROVED'),0) AS a, COALESCE(SUM(status='REJECTED'),0) AS r FROM hrm_payslips" + kWhere, kp.ToArray());
        string runArg = run == "" ? "all" : run;
        h.Append("<div class=\"hr-kpis\">");
        Kpi(h, "Payslips", Int(k.Rows[0]["n"]), Url(runArg, "", q, 1), false);
        Kpi(h, "Pending", Int(k.Rows[0]["p"]), Url(runArg, "PENDING", q, 1), Int(k.Rows[0]["p"]) > 0 && editable);
        Kpi(h, "Approved", Int(k.Rows[0]["a"]), Url(runArg, "APPROVED", q, 1), false);
        Kpi(h, "Rejected", Int(k.Rows[0]["r"]), Url(runArg, "REJECTED", q, 1), false);
        h.Append("</div>");

        // Filters
        h.Append("<div class=\"hr-filters\">");
        h.Append("<div class=\"hr-filter hr-filter--grow\"><label for=\"fRun\">Payroll run</label><select id=\"fRun\" class=\"hr-select\" onchange=\"applyFilters()\">");
        h.Append("<option value=\"all\"").Append(runFilter == "" ? " selected" : "").Append(">All payroll runs</option>");
        foreach (DataRow r in runs.Rows)
        {
            string st = Str(r["payroll_status"]);
            string suffix = st == "PROCESSED" ? " (approved)" : st == "CANCELLED" ? " (cancelled)" : "";
            if (titleCount[Str(r["payroll_title"])] > 1)
            {
                DateTime made;
                if (DateTime.TryParse(Str(r["payroll_date"]), out made)) suffix = ", created " + made.ToString("d MMM yyyy", IC) + suffix;
            }
            h.Append("<option value=\"").Append(Int(r["ID"])).Append("\"").Append(Str(r["ID"]) == runFilter ? " selected" : "").Append(">")
             .Append(E(Str(r["payroll_title"]) + suffix)).Append("</option>");
        }
        h.Append("</select></div>");
        h.Append("<div class=\"hr-filter\"><label for=\"fStatus\">Status</label><select id=\"fStatus\" class=\"hr-select\" onchange=\"applyFilters()\">");
        string[,] sts = { { "", "All statuses" }, { "PENDING", "Pending" }, { "APPROVED", "Approved" }, { "REJECTED", "Rejected" } };
        for (int i = 0; i < sts.GetLength(0); i++)
            h.Append("<option value=\"").Append(sts[i, 0]).Append("\"").Append(sts[i, 0] == status ? " selected" : "").Append(">").Append(sts[i, 1]).Append("</option>");
        h.Append("</select></div>");
        h.Append("<div class=\"hr-filter hr-filter--grow\"><label for=\"fQ\">Employee</label><input type=\"text\" id=\"fQ\" class=\"hr-input\" placeholder=\"Name or staff no\" value=\"")
         .Append(HttpUtility.HtmlAttributeEncode(q)).Append("\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();applyFilters();}\" /></div>");
        h.Append("<div class=\"hr-filters__actions\"><button type=\"button\" class=\"hr-btn hr-btn--primary\" onclick=\"applyFilters()\">Search</button>")
         .Append("<a class=\"hr-btn hr-btn--secondary\" href=\"HRPayslips.aspx\">Reset</a></div>");
        h.Append("</div>");

        if (runRow != null && runStatus == "PROCESSED")
            h.Append("<div class=\"hr-notice hr-notice--ok\">This payroll run is approved and locked. Its payslips cannot be changed.</div>");
        else if (runRow != null && runStatus == "CANCELLED")
            h.Append("<div class=\"hr-notice hr-notice--warn\">This payroll run is cancelled. Its payslips cannot be changed.</div>");

        // List
        List<MySqlParameter> lp = new List<MySqlParameter>();
        string where = " WHERE 1=1";
        if (runFilter != "") { where += " AND ps.payroll_id=@run"; lp.Add(P("@run", runFilter)); }
        if (status != "") { where += " AND ps.status=@st"; lp.Add(P("@st", status)); }
        if (q != "") { where += " AND (e.emp_name LIKE @q OR e.EMP_CODE LIKE @q)"; lp.Add(P("@q", "%" + q + "%")); }

        DataTable cnt = Q("SELECT COUNT(*) AS n FROM hrm_payslips ps JOIN hrm_employee e ON e.empID = ps.empID" + where, lp.ToArray());
        int total = Int(cnt.Rows[0]["n"]);
        int pages = Math.Max(1, (total + PageSize - 1) / PageSize);
        if (page > pages) page = pages;

        List<MySqlParameter> lp2 = new List<MySqlParameter>();
        foreach (MySqlParameter p in lp) lp2.Add(P(p.ParameterName, p.Value));
        DataTable dt = Q(@"
            SELECT ps.ID, ps.payroll_id, ps.status, ps.rejection_reason, ps.basic_pay, ps.total_allowances, ps.gross_salary,
                   ps.total_deductions, ps.net_salary, ps.payroll_month_name, ps.payroll_year,
                   e.EMP_CODE, e.emp_name, p.payroll_status, p.payroll_title,
                   IFNULL((SELECT d.dept_name FROM hrm_emp_contracts c JOIN hrm_departments d ON d.ID = c.departmentID
                           WHERE c.empID = ps.empID ORDER BY c.ID DESC LIMIT 1),'') AS dept_name
            FROM hrm_payslips ps
            JOIN hrm_employee e ON e.empID = ps.empID
            JOIN hrm_payroll p ON p.ID = ps.payroll_id" + where + @"
            ORDER BY ps.payroll_year DESC, ps.payroll_month DESC, e.emp_name
            LIMIT " + PageSize + " OFFSET " + ((page - 1) * PageSize), lp2.ToArray());

        bool anySelectable = false;
        foreach (DataRow r in dt.Rows) if (Str(r["payroll_status"]) == "PENDING") { anySelectable = true; break; }

        h.Append("<div class=\"hr-card\"><div class=\"hr-card__head\"><div class=\"hr-card__title\">")
         .Append(runRow != null ? E(runRow["payroll_title"]) : "All payroll runs").Append("</div><div class=\"hr-row\">")
         .Append("<span class=\"hr-card__meta\">").Append(total).Append(total == 1 ? " payslip" : " payslips").Append("</span>");
        if (runRow != null && Int(k.Rows[0]["n"]) > 0)
            h.Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" target=\"_blank\" href=\"HRPayslips.aspx?action=print&amp;run=").Append(runFilter).Append("\">")
             .Append(IcoPrint).Append("Print all payslips</a>")
             .Append("<a class=\"hr-btn hr-btn--secondary hr-btn--sm\" href=\"HRPayroll.aspx?run=").Append(runFilter).Append("\">Open payroll run</a>");
        h.Append("</div></div>");

        h.Append("<div class=\"hr-bulk\" id=\"bulkBar\"><span id=\"bulkCount\">0 selected</span><span class=\"hr-spacer\"></span>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"approveSelected()\">Approve</button>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"rejectSelected()\">Reject</button>")
         .Append("<button type=\"button\" class=\"hr-btn hr-btn--inverse hr-btn--sm\" onclick=\"clearSelection()\">Clear</button></div>");

        if (dt.Rows.Count == 0)
        {
            h.Append("<div class=\"hr-empty\">No payslips match the filters.</div>");
        }
        else
        {
            h.Append("<div class=\"hr-table-wrap\"><table class=\"hr-table\"><thead><tr><th style=\"width:32px\">");
            if (anySelectable) h.Append("<input type=\"checkbox\" id=\"chkAll\" onclick=\"selectAll(this)\" aria-label=\"Select all\" />");
            h.Append("</th><th>Staff no</th><th>Name</th><th>Department</th>");
            if (runFilter == "") h.Append("<th>Period</th>");
            h.Append("<th class=\"hr-num\">Basic</th><th class=\"hr-num\">Allowances</th><th class=\"hr-num\">Gross</th><th class=\"hr-num\">Deductions</th><th class=\"hr-num\">Net pay</th><th>Status</th><th></th></tr></thead><tbody>");
            decimal tb = 0, ta = 0, tg = 0, td = 0, tn = 0;
            foreach (DataRow r in dt.Rows)
            {
                int id = Int(r["ID"]);
                string st = Str(r["status"]);
                bool open = Str(r["payroll_status"]) == "PENDING";
                h.Append("<tr><td>");
                if (open) h.Append("<input type=\"checkbox\" class=\"row-chk\" value=\"").Append(id).Append("\" onclick=\"updateBulk()\" aria-label=\"Select\" />");
                h.Append("</td><td>").Append(E(r["EMP_CODE"])).Append("</td><td>").Append(E(r["emp_name"])).Append("</td><td>").Append(E(r["dept_name"])).Append("</td>");
                if (runFilter == "") h.Append("<td>").Append(E(Period(r["payroll_month_name"], r["payroll_year"]))).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["basic_pay"])).Append("</td><td class=\"hr-num\">").Append(Money(r["total_allowances"])).Append("</td>");
                h.Append("<td class=\"hr-num\">").Append(Money(r["gross_salary"])).Append("</td><td class=\"hr-num\">").Append(Money(r["total_deductions"])).Append("</td>");
                h.Append("<td class=\"hr-num\"><strong>").Append(Money(r["net_salary"])).Append("</strong></td><td>").Append(Badge(st));
                string why = Str(r["rejection_reason"]);
                if (st == "REJECTED" && why != "") h.Append("<span class=\"hr-sub\">").Append(E(why)).Append("</span>");
                h.Append("</td><td class=\"hr-right\" style=\"white-space:nowrap\">");
                h.Append("<a class=\"hr-btn hr-btn--link\" target=\"_blank\" href=\"HRPayslips.aspx?action=print&amp;id=").Append(id).Append("\">View</a>");
                if (open && st != "APPROVED") h.Append(" <button type=\"button\" class=\"hr-btn hr-btn--secondary hr-btn--sm\" onclick=\"approveOne(").Append(id).Append(")\">Approve</button>");
                if (open && st != "REJECTED") h.Append(" <button type=\"button\" class=\"hr-btn hr-btn--danger hr-btn--sm\" onclick=\"rejectOne(").Append(id).Append(")\">Reject</button>");
                h.Append("</td></tr>");
                tb += Dec(r["basic_pay"]); ta += Dec(r["total_allowances"]); tg += Dec(r["gross_salary"]); td += Dec(r["total_deductions"]); tn += Dec(r["net_salary"]);
            }
            h.Append("</tbody>");
            if (pages == 1)
            {
                h.Append("<tfoot><tr><td></td><td colspan=\"").Append(runFilter == "" ? 4 : 3).Append("\">Total (").Append(dt.Rows.Count).Append(")</td>");
                foreach (decimal v in new[] { tb, ta, tg, td, tn }) h.Append("<td class=\"hr-num\">").Append(v.ToString("#,##0", IC)).Append("</td>");
                h.Append("<td></td><td></td></tr></tfoot>");
            }
            h.Append("</table></div>");
        }

        h.Append("<div class=\"hr-card__foot\"><span>Amounts in UGX").Append(pages > 1 ? ". Page " + page + " of " + pages : "").Append("</span>");
        if (pages > 1)
        {
            h.Append("<div class=\"hr-pager\">");
            h.Append("<button type=\"button\"").Append(page <= 1 ? " disabled" : "").Append(" onclick=\"location.href='").Append(HttpUtility.JavaScriptStringEncode(Url(runArg, status, q, page - 1))).Append("'\">Previous</button>");
            h.Append("<button type=\"button\"").Append(page >= pages ? " disabled" : "").Append(" onclick=\"location.href='").Append(HttpUtility.JavaScriptStringEncode(Url(runArg, status, q, page + 1))).Append("'\">Next</button>");
            h.Append("</div>");
        }
        h.Append("</div></div>");
        litBody.Text = h.ToString();
    }

    private static void Kpi(StringBuilder h, string label, int value, string url, bool alert)
    {
        h.Append("<a class=\"hr-kpi").Append(alert ? " hr-kpi--alert" : "").Append("\" href=\"").Append(HttpUtility.HtmlAttributeEncode(url)).Append("\"><div class=\"hr-kpi__label\">")
         .Append(label).Append("</div><div class=\"hr-kpi__value\">").Append(value.ToString("#,##0", IC)).Append("</div></a>");
    }

    // =================================================================
    //  Payslip print
    // =================================================================

    private void HandlePrint()
    {
        int id = Int(Request.QueryString["id"]);
        int run = Int(Request.QueryString["run"]);
        try
        {
            string where = id > 0 ? "ps.ID = @k" : "ps.payroll_id = @k";
            DataTable dt = Q(@"
                SELECT ps.*, e.EMP_CODE, e.emp_name, e.nssf_no, e.tin, e.bankAccount, IFNULL(b.bank_name,'') AS bank_name,
                       IFNULL(d.dept_name,'') AS dept_name, IFNULL(j.jobname,'') AS jobname, p.payroll_title
                FROM hrm_payslips ps
                JOIN hrm_employee e ON e.empID = ps.empID
                JOIN hrm_payroll p ON p.ID = ps.payroll_id
                LEFT JOIN hrm_emp_contracts c ON c.ID = (SELECT MAX(c2.ID) FROM hrm_emp_contracts c2 WHERE c2.empID = ps.empID)
                LEFT JOIN hrm_departments d ON d.ID = c.departmentID
                LEFT JOIN hrm_jobs j ON j.ID = c.jobID
                LEFT JOIN banks b ON b.bank_id = e.bankID
                WHERE " + where + " ORDER BY e.emp_name", P("@k", id > 0 ? id : run));
            if (dt.Rows.Count == 0)
            {
                Response.Clear(); Response.ContentType = "text/plain"; Response.Write("Payslip not found.");
                return;
            }

            string period = Period(dt.Rows[0]["payroll_month_name"], dt.Rows[0]["payroll_year"]);
            StringBuilder body = new StringBuilder();
            body.Append("<style>")
                .Append(".slip2{width:100%;border-collapse:collapse;margin-top:12px}.slip2>tbody>tr>td{width:50%;vertical-align:top;padding:0}")
                .Append(".slip2>tbody>tr>td:first-child{padding-right:10px}.slip2>tbody>tr>td:last-child{padding-left:10px}")
                .Append(".net{display:flex;justify-content:space-between;align-items:center;border:1.5px solid #1a1a2e;padding:9px 12px;margin-top:16px;font-size:11pt}")
                .Append(".net strong{font-size:13pt}.words{margin-top:6px;font-size:9.5pt}.cg{margin-top:18px;font-size:8.5pt;color:#555;text-align:center}")
                .Append("</style>");
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (i > 0) body.Append("<div class=\"pb\"></div>").Append(Letterhead(period));
                body.Append(SlipBody(dt.Rows[i]));
            }

            HrDocument.Options o = new HrDocument.Options();
            o.Reference = "Period: " + period;
            o.BackUrl = "HRPayslips.aspx?payroll_id=" + Int(dt.Rows[0]["payroll_id"]);
            Response.Clear();
            Response.ContentType = "text/html";
            Response.Write(HrDocument.Page("Payslip", body.ToString(), o));
            Log("Payslip print", id > 0 ? "Payslip " + id : "Run " + run + " (" + dt.Rows.Count + " payslips)");
        }
        catch (Exception ex)
        {
            Log("Error", "Payslip print: " + ex.Message);
            Response.Clear(); Response.ContentType = "text/plain"; Response.Write("The payslip could not be produced. Please try again.");
        }
    }

    /// <summary>Letterhead for the second and later payslips of a bulk print (same markup and classes as HrDocument.Page).</summary>
    private string Letterhead(string period)
    {
        string crest = Request.Url.GetLeftPart(UriPartial.Authority) + VirtualPathUtility.ToAbsolute("~/COOPERP/images/mru-crest.png");
        return "<div class=\"lh\"><img src=\"" + HttpUtility.HtmlAttributeEncode(crest) + "\" alt=\"\" /><div class=\"lh__t\"><div class=\"lh__u\">MUTEESA I ROYAL UNIVERSITY</div>" +
               "<div class=\"lh__o\">Human Resource Office</div></div><div class=\"lh__sp\"></div></div><div class=\"ttl\">PAYSLIP</div>" +
               "<div class=\"ref\">" + HrDocument.E("Period: " + period) + "</div>";
    }

    private static List<string[]> Details(string raw)
    {
        List<string[]> rows = new List<string[]>();
        foreach (string item in (raw ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = item.Split(new[] { ':' }, 2);
            if (parts.Length != 2) continue;
            decimal amt;
            decimal.TryParse(parts[1].Trim(), NumberStyles.Any, IC, out amt);
            rows.Add(new[] { HrDocument.E(Title(parts[0])), HrDocument.Money(amt) });
        }
        return rows;
    }

    private string SlipBody(DataRow r)
    {
        StringBuilder b = new StringBuilder();
        b.Append(HrDocument.Meta(
            "Staff no", Str(r["EMP_CODE"]), "Name", Str(r["emp_name"]),
            "Position", Title(Str(r["jobname"])), "Department", Title(Str(r["dept_name"])),
            "Bank", Str(r["bank_name"]), "Account no", Clean(r["bankAccount"]),
            "NSSF no", Clean(r["nssf_no"]), "TIN", Clean(r["tin"])));

        List<string[]> earn = new List<string[]>();
        earn.Add(new[] { "Basic pay", HrDocument.Money(r["basic_pay"]) });
        earn.AddRange(Details(Str(r["allowance_details"])));

        List<string[]> ded = new List<string[]>();
        ded.Add(new[] { "PAYE", HrDocument.Money(r["paye"]) });
        ded.Add(new[] { "NSSF (employee)", HrDocument.Money(r["nssf"]) });
        if (Dec(r["local_tax"]) != 0) ded.Add(new[] { "Local service tax", HrDocument.Money(r["local_tax"]) });
        if (Dec(r["kabaka_contribution"]) != 0) ded.Add(new[] { "Kabaka contribution", HrDocument.Money(r["kabaka_contribution"]) });
        ded.AddRange(Details(Str(r["deduction_details"])));

        b.Append("<table class=\"slip2\"><tbody><tr><td>");
        b.Append(HrDocument.Table(new[] { "Earnings", "Amount (UGX)" }, earn, new[] { false, true }, new[] { "Gross pay", HrDocument.Money(r["gross_salary"]) }));
        b.Append("</td><td>");
        b.Append(HrDocument.Table(new[] { "Deductions", "Amount (UGX)" }, ded, new[] { false, true }, new[] { "Total deductions", HrDocument.Money(r["total_deductions"]) }));
        b.Append("</td></tr></tbody></table>");

        decimal net = Math.Round(Dec(r["net_salary"]), 0, MidpointRounding.AwayFromZero);
        b.Append("<div class=\"net\"><span>Net pay</span><strong>UGX ").Append(net.ToString("#,##0", IC)).Append("</strong></div>");
        b.Append("<div class=\"words\">Amount in words: ").Append(HrDocument.E(AmountInWords(net))).Append("</div>");
        b.Append("<div class=\"cg\">Computer generated payslip. ").Append(HrDocument.E(Str(r["payroll_title"]))).Append(".</div>");
        return b.ToString();
    }

    private static readonly string[] Ones = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
    private static readonly string[] Tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

    private static string Words(long n)
    {
        if (n < 20) return Ones[n];
        if (n < 100) return Tens[n / 10] + (n % 10 > 0 ? "-" + Ones[n % 10] : "");
        if (n < 1000) return Ones[n / 100] + " hundred" + (n % 100 > 0 ? " and " + Words(n % 100) : "");
        string[] units = { "thousand", "million", "billion" };
        long[] sizes = { 1000L, 1000000L, 1000000000L };
        for (int i = sizes.Length - 1; i >= 0; i--)
        {
            if (n >= sizes[i])
            {
                long rest = n % sizes[i];
                return Words(n / sizes[i]) + " " + units[i] + (rest == 0 ? "" : (rest < 100 ? " and " : " ") + Words(rest));
            }
        }
        return n.ToString(IC);
    }

    /// <summary>"One million two hundred thousand Uganda shillings only".</summary>
    public static string AmountInWords(decimal amount)
    {
        long n = (long)Math.Abs(amount);
        string w = Words(n);
        return char.ToUpperInvariant(w[0]) + w.Substring(1) + " Uganda shillings only";
    }
}
