using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Appraisal reports: the single analysis page. Completion by category, department and session,
/// one classification table, and the appraisal records export (HrExport .xlsx with Records,
/// Summary by department and Classification sheets; .csv of Records).
/// Counts cover every appraisal_records row matching the filters. Named rows (the Records sheet)
/// are only appraisals that have reached HR: Awaiting HR and HR reviewed, or Cancelled when that
/// status is chosen. The record list itself lives on the Appraisals page.
/// </summary>
public partial class COOPERP_NewScreens_AppraisalReports : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsSession { get { int v; return int.TryParse(Request.QueryString["sid"] ?? "0", out v) && v > 0 ? v : 0; } }
    private string QsDepartment { get { return (Request.QueryString["dept"] ?? "").Trim(); } }
    private string QsCategory { get { return (Request.QueryString["cat"] ?? "").Trim().ToUpperInvariant(); } }
    private string QsStatus { get { return (Request.QueryString["st"] ?? "").Trim().ToUpperInvariant(); } }

    private static readonly string[][] Statuses = new string[][] {
        new string[] { "PENDING", "Not started" },
        new string[] { "EMPLOYEE_IN_PROGRESS", "In progress" },
        new string[] { "RETURNED", "Returned" },
        new string[] { "EMPLOYEE_SUBMITTED", "Submitted" },
        new string[] { "SUPERVISOR_IN_PROGRESS", "With supervisor" },
        new string[] { "COMPLETED", "Awaiting HR" },
        new string[] { "HR_REVIEWED", "HR reviewed" },
        new string[] { "CANCELLED", "Cancelled" }
    };

    private static readonly string[][] Bands = new string[][] {
        new string[] { "Exceptional", "90 to 100", "ok" },
        new string[] { "Above expectations", "75 to 89.99", "ok" },
        new string[] { "Satisfactory", "60 to 74.99", "info" },
        new string[] { "Development needed", "50 to 59.99", "warn" },
        new string[] { "Unsatisfactory", "Below 50", "bad" }
    };

    /// <summary>Export link that keeps the current filters.</summary>
    protected string ExportLink(string fmt)
    {
        StringBuilder sb = new StringBuilder("AppraisalReports.aspx?action=export&amp;fmt=" + fmt);
        if (QsSession > 0) sb.Append("&amp;sid=" + QsSession);
        if (QsDepartment != "") sb.Append("&amp;dept=" + HttpUtility.UrlEncode(QsDepartment));
        if (QsCategory != "") sb.Append("&amp;cat=" + HttpUtility.UrlEncode(QsCategory));
        if (QsStatus != "") sb.Append("&amp;st=" + HttpUtility.UrlEncode(QsStatus));
        return sb.ToString();
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!HrAccess.RequireHr(false)) return;

        string action = (Request.QueryString["action"] ?? Request.QueryString["ajax"] ?? "").Trim().ToLowerInvariant();
        if (action == "export" || action == "exportcsv")
        {
            string fmt = action == "exportcsv" ? "csv" : (Request.QueryString["fmt"] ?? "xlsx").ToLowerInvariant();
            Export(fmt == "csv" ? "csv" : "xlsx");
            return;
        }

        if (!IsPostBack)
        {
            try
            {
                LoadFilters();
                LoadReport();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("AppraisalReports: " + ex);
                litError.Text = "<div class='hr-notice hr-notice--bad'>The report could not be loaded. Refresh the page or try again later.</div>";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FILTERS
    // ═══════════════════════════════════════════════════════════════════
    private void LoadFilters()
    {
        DataTable dtSess = ExecuteQuery(
            @"SELECT session_id, session_title, status FROM appraisal_sessions
              ORDER BY FIELD(status,'ACTIVE','DRAFT','CLOSED','ARCHIVED'), created_at DESC");
        StringBuilder sb = new StringBuilder("<option value='0'>All sessions</option>");
        foreach (DataRow r in dtSess.Rows)
        {
            int sid = Convert.ToInt32(r["session_id"]);
            sb.AppendFormat("<option value='{0}'{1}>{2} ({3})</option>", sid, sid == QsSession ? " selected" : "",
                Enc(SafeStr(r["session_title"])), Word(SafeStr(r["status"])));
        }
        litSessionOptions.Text = sb.ToString();

        string deptExpr = DeptExpr();
        DataTable dtDept = ExecuteQuery(string.Format(
            @"SELECT DISTINCT {0} AS dept FROM appraisal_records ar
              LEFT JOIN hrm_employee e ON e.empID = ar.employee_id ORDER BY dept", deptExpr));
        sb = new StringBuilder("<option value=''>All departments</option>");
        foreach (DataRow r in dtDept.Rows)
        {
            string d = SafeStr(r["dept"]);
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", HttpUtility.HtmlAttributeEncode(d), d == QsDepartment ? " selected" : "", Enc(d));
        }
        litDeptOptions.Text = sb.ToString();

        sb = new StringBuilder("<option value=''>All categories</option>");
        foreach (string c in new string[] { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" })
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", c, c == QsCategory ? " selected" : "", CategoryWord(c));
        litCatOptions.Text = sb.ToString();

        sb = new StringBuilder("<option value=''>All statuses</option>");
        foreach (string[] st in Statuses)
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>", st[0], st[0] == QsStatus ? " selected" : "", st[1]);
        litStatusOptions.Text = sb.ToString();

        if (QsSession > 0)
            litSessionReport.Text = "<a class='hr-btn hr-btn--inverse' href='AppraisalSessionReport.aspx?sid=" + QsSession + "' target='_blank' rel='noopener'>Session report</a>";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SQL
    // ═══════════════════════════════════════════════════════════════════
    private string _deptExpr;
    private string DeptExpr()
    {
        if (_deptExpr == null)
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                _deptExpr = BuildDepartmentSqlExpression(conn, "e");
            }
        return _deptExpr;
    }

    private string Where(List<MySqlParameter> parms)
    {
        StringBuilder sb = new StringBuilder(" WHERE 1=1");
        if (QsSession > 0) { sb.Append(" AND ar.session_id = @sid"); parms.Add(new MySqlParameter("@sid", QsSession)); }
        if (QsDepartment != "") { sb.Append(" AND " + DeptExpr() + " = @dept"); parms.Add(new MySqlParameter("@dept", QsDepartment)); }
        if (QsCategory != "") { sb.Append(" AND ar.staff_category = @cat"); parms.Add(new MySqlParameter("@cat", QsCategory)); }
        if (QsStatus != "") { sb.Append(" AND ar.status = @st"); parms.Add(new MySqlParameter("@st", QsStatus)); }
        return sb.ToString();
    }

    private const string Measures =
        @"COUNT(*) AS total,
          SUM(ar.status IN ('EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS','COMPLETED','HR_REVIEWED')) AS submitted,
          SUM(ar.status IN ('COMPLETED','HR_REVIEWED')) AS completed,
          SUM(ar.status = 'HR_REVIEWED') AS hr_reviewed,
          AVG(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN ar.final_percentage END) AS avg_score";

    private const string From =
        @" FROM appraisal_records ar
           LEFT JOIN hrm_employee e ON e.empID = ar.employee_id
           INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id";

    private DataTable Grouped(string keyExpr, string orderBy)
    {
        List<MySqlParameter> parms = new List<MySqlParameter>();
        string sql = "SELECT " + keyExpr + " AS k, " + Measures + From + Where(parms) +
                     " GROUP BY " + keyExpr + " ORDER BY " + orderBy;
        return ExecuteQuery(sql, parms.ToArray());
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SCREEN
    // ═══════════════════════════════════════════════════════════════════
    private void LoadReport()
    {
        List<MySqlParameter> parms = new List<MySqlParameter>();
        DataTable dtAll = ExecuteQuery("SELECT " + Measures + From + Where(parms), parms.ToArray());
        DataRow a = dtAll.Rows[0];
        int total = SafeInt(a["total"]), completed = SafeInt(a["completed"]);
        litKpiTotal.Text = total.ToString("N0");
        litKpiSubmitted.Text = SafeInt(a["submitted"]).ToString("N0");
        litKpiCompleted.Text = completed.ToString("N0");
        litKpiRate.Text = total > 0 ? Pct(completed, total) + "% of appraisals" : "";
        litKpiHr.Text = SafeInt(a["hr_reviewed"]).ToString("N0");
        litKpiAvg.Text = Dec(a["avg_score"]);

        litCatRows.Text = Rows(Grouped("ar.staff_category", "FIELD(ar.staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT')"), true, a);
        litDeptRows.Text = Rows(Grouped(DeptExpr(), "k"), false, a);
        litSessionRows.Text = Rows(Grouped("s.session_title", "MIN(s.created_at) DESC"), false, a);

        // Classification
        parms = new List<MySqlParameter>();
        DataTable dtC = ExecuteQuery(
            "SELECT appraisal_classify(ar.final_percentage) AS band, COUNT(*) AS n" + From + Where(parms) +
            " AND ar.status IN ('COMPLETED','HR_REVIEWED') AND ar.final_percentage IS NOT NULL GROUP BY band", parms.ToArray());
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int scored = 0;
        foreach (DataRow r in dtC.Rows) { counts[SafeStr(r["band"])] = SafeInt(r["n"]); scored += SafeInt(r["n"]); }
        StringBuilder sb = new StringBuilder("<tbody>");
        foreach (string[] b in Bands)
        {
            int n = counts.ContainsKey(b[0]) ? counts[b[0]] : 0;
            sb.AppendFormat("<tr><td><span class='hr-badge hr-badge--{0}'>{1}</span></td><td>{2}</td><td class='hr-num'>{3}</td><td class='hr-num'>{4}</td></tr>",
                b[2], b[0], b[1], n.ToString("N0"), scored > 0 ? Pct(n, scored) : "");
        }
        sb.AppendFormat("</tbody><tfoot><tr><td colspan='2'>Scored appraisals</td><td class='hr-num'>{0}</td><td class='hr-num'></td></tr></tfoot>", scored.ToString("N0"));
        litClassRows.Text = sb.ToString();

        List<string> q = new List<string>();
        if (QsSession > 0) q.Add("sid=" + QsSession);
        if (QsCategory != "") q.Add("cat=" + QsCategory);
        if (QsStatus == "COMPLETED" || QsStatus == "HR_REVIEWED" || QsStatus == "CANCELLED") q.Add("status=" + QsStatus);
        litRecordsLink.Text = "<a class='hr-btn hr-btn--secondary hr-btn--sm' href='AppraisalView.aspx" +
            (q.Count > 0 ? "?" + string.Join("&amp;", q.ToArray()) : "") + "'>View the appraisals</a>";
    }

    private string Rows(DataTable dt, bool category, DataRow totals)
    {
        StringBuilder sb = new StringBuilder("<tbody>");
        if (dt.Rows.Count == 0) sb.Append("<tr><td colspan='7' class='hr-empty'>No appraisals match the filters.</td></tr>");
        foreach (DataRow r in dt.Rows)
        {
            string key = SafeStr(r["k"]);
            sb.AppendFormat("<tr><td>{0}</td>{1}</tr>", category ? CategoryWord(key) : Enc(key == "" ? "Not recorded" : key), Cells(r));
        }
        sb.Append("</tbody>");
        if (dt.Rows.Count > 1) sb.AppendFormat("<tfoot><tr><td>Total</td>{0}</tr></tfoot>", Cells(totals));
        return sb.ToString();
    }

    private static string Cells(DataRow r)
    {
        int total = SafeInt(r["total"]), completed = SafeInt(r["completed"]);
        return string.Format("<td class='hr-num'>{0}</td><td class='hr-num'>{1}</td><td class='hr-num'>{2}</td><td class='hr-num'>{3}</td><td class='hr-num'>{4}</td><td class='hr-num'>{5}</td>",
            total.ToString("N0"), SafeInt(r["submitted"]).ToString("N0"), completed.ToString("N0"), SafeInt(r["hr_reviewed"]).ToString("N0"),
            total > 0 ? Pct(completed, total) : "", Dec(r["avg_score"]));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EXPORT (HrExport)
    // ═══════════════════════════════════════════════════════════════════
    private void Export(string fmt)
    {
        HrExport.Report rep = new HrExport.Report("Appraisal records", "appraisal-records");
        rep.PreparedBy = Session["ScreenName"] != null ? Session["ScreenName"].ToString() : HrAccess.Username();

        if (QsSession > 0)
        {
            DataTable dtS = ExecuteQuery("SELECT session_title FROM appraisal_sessions WHERE session_id = @sid", new MySqlParameter("@sid", QsSession));
            rep.AddScope("Session", dtS.Rows.Count > 0 ? SafeStr(dtS.Rows[0]["session_title"]) : QsSession.ToString());
        }
        rep.AddScope("Department", QsDepartment);
        rep.AddScope("Category", QsCategory != "" ? CategoryWord(QsCategory) : "");
        rep.AddScope("Status", QsStatus != "" ? StatusWord(QsStatus) : "");

        // Records
        List<MySqlParameter> parms = new List<MySqlParameter>();
        DataTable dt = ExecuteQuery(
            @"SELECT IFNULL(e.EMP_CODE,'') AS emp_code,
                     IFNULL(e.emp_name, CONCAT('Staff record not found (ID ', ar.employee_id, ')')) AS emp_name,
                     ar.staff_category, " + DeptExpr() + @" AS department, s.session_title,
                     IFNULL(rev.emp_name,'') AS supervisor, ar.status,
                     ar.section_b_self_total, ar.raw_score, ar.final_percentage, ar.classification,
                     ar.hr_status, ar.hr_overall_rating, ar.hr_recommendation,
                     ar.employee_submitted_at, ar.supervisor_submitted_at, ar.hr_submitted_at, ar.employee_ack" +
            From + " LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id" + Where(parms) +
            (QsStatus == "CANCELLED" ? "" : " AND ar.status IN ('COMPLETED','HR_REVIEWED')") +
            " ORDER BY s.created_at DESC, FIELD(ar.staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT'), emp_name",
            parms.ToArray());

        HrExport.Sheet rec = rep.NewSheet("Records");
        rec.Add("Staff No").Add("Name").Add("Category").Add("Department").Add("Session").Add("Supervisor").Add("Status")
           .Add("Self total", HrExport.Kind.Decimal).Add("Supervisor total", HrExport.Kind.Decimal)
           .Add("Score %", HrExport.Kind.Percent).Add("Classification")
           .Add("HR rating", HrExport.Kind.Number).Add("HR recommendation")
           .Add("Submitted", HrExport.Kind.Date).Add("Supervisor completed", HrExport.Kind.Date)
           .Add("HR reviewed", HrExport.Kind.Date).Add("Acknowledgement");

        Dictionary<string, int> bandCount = new Dictionary<string, int>();
        int scoredAll = 0;
        foreach (DataRow r in dt.Rows)
        {
            string st = SafeStr(r["status"]).ToUpperInvariant();
            bool done = st == "COMPLETED" || st == "HR_REVIEWED";
            bool hrDone = st == "HR_REVIEWED";
            object pct = r["final_percentage"];
            bool scored = done && pct != DBNull.Value;
            string band = scored ? Classify(Convert.ToDecimal(pct)) : "";
            string ack = SafeStr(r["employee_ack"]).ToUpperInvariant();
            int hrRating = SafeInt(r["hr_overall_rating"]);

            rec.Row(SafeStr(r["emp_code"]), SafeStr(r["emp_name"]), CategoryWord(SafeStr(r["staff_category"])),
                SafeStr(r["department"]), SafeStr(r["session_title"]),
                SafeStr(r["supervisor"]) != "" ? SafeStr(r["supervisor"]) : "Not assigned",
                StatusWord(st),
                r["section_b_self_total"], r["raw_score"], done ? pct : null, band,
                hrDone && hrRating > 0 ? (object)hrRating : null,
                hrDone ? RecommendationWord(SafeStr(r["hr_recommendation"])) : "",
                r["employee_submitted_at"], r["supervisor_submitted_at"], hrDone ? r["hr_submitted_at"] : null,
                ack == "AGREE" ? "Agrees" : ack == "DISAGREE" ? "Disagrees" : "");

            if (scored) { scoredAll++; if (!bandCount.ContainsKey(band)) bandCount[band] = 0; bandCount[band]++; }
        }

        // Summary by department
        HrExport.Sheet sum = rep.NewSheet("Summary by department");
        sum.Title = "Appraisal summary by department";
        sum.Add("Department").Add("Records", HrExport.Kind.Number, true).Add("Submitted", HrExport.Kind.Number, true)
           .Add("Completed", HrExport.Kind.Number, true).Add("HR reviewed", HrExport.Kind.Number, true)
           .Add("Completion %", HrExport.Kind.Percent).Add("Average score %", HrExport.Kind.Percent);
        // Counts by department cover every matching appraisal (no names).
        foreach (DataRow g in Grouped(DeptExpr(), "k").Rows)
        {
            int total = SafeInt(g["total"]), completed = SafeInt(g["completed"]);
            sum.Row(SafeStr(g["k"]), total, SafeInt(g["submitted"]), completed, SafeInt(g["hr_reviewed"]),
                total > 0 ? (object)Math.Round((decimal)completed * 100m / total, 1) : null,
                g["avg_score"] != DBNull.Value ? (object)Math.Round(Convert.ToDecimal(g["avg_score"]), 1) : null);
        }

        // Classification
        HrExport.Sheet cls = rep.NewSheet("Classification");
        cls.Title = "Appraisal classification (scored appraisals completed by the supervisor)";
        cls.Add("Band").Add("Count", HrExport.Kind.Number, true).Add("% of scored", HrExport.Kind.Percent);
        foreach (string[] b in Bands)
        {
            int n = bandCount.ContainsKey(b[0]) ? bandCount[b[0]] : 0;
            cls.Row(b[0], n, scoredAll > 0 ? (object)Math.Round((decimal)n * 100m / scoredAll, 1) : null);
        }

        if (fmt == "csv") HrExport.SendCsv(Response, rep, 0);
        else HrExport.SendXlsx(Response, rep);
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VOCABULARY AND HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string StatusWord(string s)
    {
        foreach (string[] st in Statuses) if (st[0] == (s ?? "").ToUpperInvariant()) return st[1];
        return s ?? "";
    }

    private static string Classify(decimal d)
    {
        if (d >= 90) return "Exceptional";
        if (d >= 75) return "Above expectations";
        if (d >= 60) return "Satisfactory";
        if (d >= 50) return "Development needed";
        return "Unsatisfactory";
    }

    private static string RecommendationWord(string rec)
    {
        switch ((rec ?? "").ToUpperInvariant())
        {
            case "CONFIRM":          return "Confirm appointment";
            case "EXTEND_PROBATION": return "Extend probation";
            case "PIP":              return "Performance improvement plan";
            case "PROMOTE":          return "Promote";
            case "OTHER":            return "Other";
            default:                 return rec ?? "";
        }
    }

    private static string CategoryWord(string c)
    {
        switch ((c ?? "").ToUpperInvariant())
        {
            case "ACADEMIC": return "Academic";
            case "ADMINISTRATIVE": return "Administrative";
            case "SUPPORT": return "Support";
            default: return c == "" ? "Not recorded" : HttpUtility.HtmlEncode(c);
        }
    }

    private static string Word(string s)
    {
        s = (s ?? "").Trim();
        return s.Length == 0 ? "" : s.Substring(0, 1).ToUpperInvariant() + s.Substring(1).ToLowerInvariant();
    }

    private static string Pct(int n, int of)
    {
        return of > 0 ? ((decimal)n * 100m / of).ToString("0.0", CultureInfo.InvariantCulture) : "";
    }

    private static string Dec(object v)
    {
        decimal d;
        if (v == null || v == DBNull.Value || !decimal.TryParse(v.ToString(), out d)) return "";
        return d.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string Enc(string s) { return HttpUtility.HtmlEncode(HrExport.Clean(s ?? "")); }

    private static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        decimal d;
        return decimal.TryParse(val.ToString(), out d) ? (int)d : 0;
    }

    private static string SafeStr(object val) { return val == null || val == DBNull.Value ? "" : val.ToString(); }

    private string BuildDepartmentSqlExpression(MySqlConnection conn, string employeeAlias)
    {
        if (ColumnExists(conn, "hrm_employee", "department"))
            return string.Format("IFNULL(NULLIF(TRIM({0}.department),''), 'Not recorded')", employeeAlias);

        if (TableExists(conn, "hrm_emp_contracts") && TableExists(conn, "hrm_departments") && ColumnExists(conn, "hrm_emp_contracts", "departmentID"))
        {
            string deptColumn = ColumnExists(conn, "hrm_departments", "dept_name")
                ? "dept_name" : (ColumnExists(conn, "hrm_departments", "department") ? "department" : "");
            string sortColumn = ColumnExists(conn, "hrm_emp_contracts", "contractStart")
                ? "contractStart" : (ColumnExists(conn, "hrm_emp_contracts", "created_at") ? "created_at" : "empID");
            if (!string.IsNullOrEmpty(deptColumn))
                return string.Format(
                    "IFNULL(NULLIF(TRIM((SELECT d.{0} FROM hrm_emp_contracts c LEFT JOIN hrm_departments d ON c.departmentID = d.ID WHERE c.empID = {1}.empID ORDER BY (CASE WHEN IFNULL(c.contractStatus,'')='VALID' THEN 0 ELSE 1 END), c.{2} DESC LIMIT 1)),''), 'Not recorded')",
                    deptColumn, employeeAlias, sortColumn);
        }
        return "'Not recorded'";
    }

    private bool TableExists(MySqlConnection conn, string tableName)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t", conn))
        {
            cmd.Parameters.AddWithValue("@t", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    private bool ColumnExists(MySqlConnection conn, string tableName, string columnName)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c", conn))
        {
            cmd.Parameters.AddWithValue("@t", tableName);
            cmd.Parameters.AddWithValue("@c", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }
}
