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
/// Appraisal overview: four KPIs, the HR review queue and the sessions past their deadline.
/// Population: every appraisal_records row of the selected session (or of all sessions).
/// Analysis (category, department, classification) lives on AppraisalReports.
/// </summary>
public partial class COOPERP_NewScreens_AppraisalDashboard : System.Web.UI.Page
{
    private const int QueueRows = 25;

    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsSession
    {
        get
        {
            int v;
            return int.TryParse(Request.QueryString["sid"] ?? "0", out v) && v > 0 ? v : 0;
        }
    }

    /// <summary>Link to the Appraisals list, keeping the selected session.</summary>
    protected string ViewLink(string status)
    {
        List<string> q = new List<string>();
        if (QsSession > 0) q.Add("sid=" + QsSession);
        if (!string.IsNullOrEmpty(status)) q.Add("status=" + status);
        return "AppraisalView.aspx" + (q.Count > 0 ? "?" + string.Join("&amp;", q.ToArray()) : "");
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!HrAccess.RequireHr(false)) return;
        if (!IsPostBack)
        {
            LoadSessionFilter();
            LoadDashboard();
        }
    }

    private void LoadSessionFilter()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT session_id, session_title, status
              FROM appraisal_sessions
              ORDER BY FIELD(status,'ACTIVE','DRAFT','CLOSED','ARCHIVED'), created_at DESC");

        StringBuilder sb = new StringBuilder();
        sb.Append("<option value='0'>All sessions</option>");
        foreach (DataRow r in dt.Rows)
        {
            int sid = Convert.ToInt32(r["session_id"]);
            sb.AppendFormat("<option value='{0}'{1}>{2} ({3})</option>",
                sid, sid == QsSession ? " selected" : "",
                Enc(r["session_title"]), SessionStatusWord(SafeStr(r["status"])));
        }
        litSessionOptions.Text = sb.ToString();
    }

    private void LoadDashboard()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                LoadKpis(conn);
                LoadUnassignedReviewers(conn);
                LoadActionRequired(conn);
                LoadOverdue(conn);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("AppraisalDashboard: " + ex);
            litUnassignedBanner.Text = "<div class='hr-notice hr-notice--bad'>The overview could not be loaded. Refresh the page or try again later.</div>";
        }
    }

    private string SessionWhere(string alias)
    {
        return QsSession > 0 ? " AND " + alias + ".session_id = " + QsSession : "";
    }

    // ── KPIs ───────────────────────────────────────────────────────────
    private void LoadKpis(MySqlConnection conn)
    {
        DataTable dt = ExecuteQuery(conn,
            @"SELECT COUNT(*) AS total,
                     SUM(ar.status = 'PENDING') AS not_started,
                     SUM(ar.status IN ('EMPLOYEE_IN_PROGRESS','RETURNED')) AS emp_stage,
                     SUM(ar.status IN ('EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS')) AS sup_stage,
                     SUM(ar.status = 'COMPLETED') AS needs_hr,
                     SUM(ar.status = 'HR_REVIEWED') AS hr_reviewed
              FROM appraisal_records ar
              WHERE 1=1" + SessionWhere("ar"));
        DataRow r = dt.Rows[0];
        litKpiTotal.Text      = SafeInt(r["total"]).ToString("N0");
        litKpiNotStarted.Text = SafeInt(r["not_started"]).ToString("N0");
        litKpiEmpStage.Text   = SafeInt(r["emp_stage"]).ToString("N0") + " in progress";
        litKpiSupStage.Text   = SafeInt(r["sup_stage"]).ToString("N0");
        litKpiNeedsHr.Text    = SafeInt(r["needs_hr"]).ToString("N0");
        litKpiHrReviewed.Text = SafeInt(r["hr_reviewed"]).ToString("N0") + " HR reviewed";
    }

    // ── Open records with no supervisor ───────────────────────────────
    private void LoadUnassignedReviewers(MySqlConnection conn)
    {
        string sql =
            @"SELECT COUNT(*) AS cnt
              FROM appraisal_records ar
              WHERE (ar.reviewer_id IS NULL OR ar.reviewer_id = 0)
                AND ar.status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED')" +
            (QsSession > 0 ? " AND ar.session_id = " + QsSession
                           : " AND ar.session_id IN (SELECT session_id FROM appraisal_sessions WHERE status = 'ACTIVE')");
        int cnt = SafeInt(ExecuteQuery(conn, sql).Rows[0]["cnt"]);
        if (cnt == 0) { litUnassignedBanner.Text = ""; return; }

        int linkSid = QsSession;
        if (linkSid == 0)
        {
            DataTable dtAct = ExecuteQuery(conn, "SELECT session_id FROM appraisal_sessions WHERE status = 'ACTIVE' ORDER BY created_at DESC LIMIT 1");
            if (dtAct.Rows.Count > 0) linkSid = SafeInt(dtAct.Rows[0]["session_id"]);
        }
        string link = "AppraisalView.aspx?rev=none&amp;ps=200" + (linkSid > 0 ? "&amp;sid=" + linkSid : "");
        litUnassignedBanner.Text = string.Format(
            "<div class='hr-notice hr-notice--warn'>{0} open appraisal{1} {2} no supervisor. <a href='{3}'>Assign supervisors</a></div>",
            cnt.ToString("N0"), cnt == 1 ? "" : "s", cnt == 1 ? "has" : "have", link);
    }

    // ── HR review queue (count is a real count, not the LIMIT) ─────────
    private void LoadActionRequired(MySqlConnection conn)
    {
        string where = " WHERE ar.status = 'COMPLETED'" + SessionWhere("ar");
        int total = SafeInt(ExecuteQuery(conn, "SELECT COUNT(*) AS c FROM appraisal_records ar" + where).Rows[0]["c"]);

        string deptExpr = BuildDepartmentSqlExpression(conn, "e");
        DataTable dt = ExecuteQuery(conn, string.Format(
            @"SELECT ar.record_id, e.emp_name, e.EMP_CODE,
                     {0} AS department,
                     s.session_title,
                     sup.emp_name AS supervisor_name,
                     ar.final_percentage,
                     IFNULL(appraisal_classify(ar.final_percentage), IFNULL(ar.classification,'')) AS classification,
                     DATEDIFF(CURDATE(), IFNULL(ar.supervisor_submitted_at, ar.updated_at)) AS days_waiting
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              LEFT JOIN hrm_employee sup ON sup.empID = ar.reviewer_id
              {1}
              ORDER BY IFNULL(ar.supervisor_submitted_at, ar.updated_at) ASC
              LIMIT {2}", deptExpr, where, QueueRows));

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<tr><td colspan='8' class='hr-empty'>No appraisals are awaiting HR review.</td></tr>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                int days = SafeInt(r["days_waiting"]);
                sb.Append("<tr>");
                sb.AppendFormat("<td>{0}<span class='hr-sub'>{1}</span></td>", Enc(r["emp_name"]), Enc(r["EMP_CODE"]));
                sb.AppendFormat("<td>{0}</td>", Enc(r["department"]));
                sb.AppendFormat("<td>{0}</td>", Enc(r["session_title"]));
                string sup = SafeStr(r["supervisor_name"]);
                sb.AppendFormat("<td>{0}</td>", sup == "" ? "<span class='hr-muted'>Not assigned</span>" : Enc(sup));
                sb.AppendFormat("<td class='hr-num'>{0}</td>", Pct(r["final_percentage"]));
                sb.AppendFormat("<td>{0}</td>", ClassBadge(SafeStr(r["classification"])));
                sb.AppendFormat("<td class='hr-num'>{0}</td>", days > 14 ? "<span style='color:var(--hr-bad);font-weight:600;'>" + days + "</span>" : days.ToString());
                sb.AppendFormat("<td class='hr-right'><a class='hr-btn hr-btn--secondary hr-btn--sm' href='AppraisalView.aspx?rid={0}'>Review</a></td>", SafeInt(r["record_id"]));
                sb.Append("</tr>");
            }
        }
        litActionRequired.Text = sb.ToString();
        litActionFoot.Text = total > 0
            ? string.Format("<div class='hr-card__foot'><span>Showing 1 to {0} of {1}, oldest first</span><a href='{2}'>View all</a></div>",
                dt.Rows.Count, total.ToString("N0"), ViewLink("COMPLETED"))
            : "";
    }

    // ── Sessions past their deadline ───────────────────────────────────
    private void LoadOverdue(MySqlConnection conn)
    {
        DataTable dt = ExecuteQuery(conn,
            @"SELECT s.session_id, s.session_title, s.deadline,
                     COUNT(*) AS cnt,
                     DATEDIFF(CURDATE(), s.deadline) AS days_overdue
              FROM appraisal_records ar
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE ar.status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED')
                AND s.deadline < CURDATE()" + SessionWhere("ar") + @"
              GROUP BY s.session_id, s.session_title, s.deadline
              ORDER BY s.deadline ASC");

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<tr><td colspan='5' class='hr-empty'>No session is past its deadline with work outstanding.</td></tr>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr>");
                sb.AppendFormat("<td>{0}</td>", Enc(r["session_title"]));
                sb.AppendFormat("<td>{0}</td>", FormatDate(r["deadline"]));
                sb.AppendFormat("<td class='hr-num'>{0}</td>", SafeInt(r["days_overdue"]).ToString("N0"));
                sb.AppendFormat("<td class='hr-num'>{0}</td>", SafeInt(r["cnt"]).ToString("N0"));
                sb.AppendFormat("<td class='hr-right'><a class='hr-btn hr-btn--secondary hr-btn--sm' href='AppraisalView.aspx?sid={0}'>Open</a></td>", SafeInt(r["session_id"]));
                sb.Append("</tr>");
            }
        }
        litAlerts.Text = sb.ToString();
    }

    private string BuildDepartmentSqlExpression(MySqlConnection conn, string employeeAlias)
    {
        if (ColumnExists(conn, "hrm_employee", "department"))
            return string.Format("IFNULL(NULLIF(TRIM({0}.department),''), 'Not recorded')", employeeAlias);

        if (TableExists(conn, "hrm_emp_contracts") && TableExists(conn, "hrm_departments") &&
            ColumnExists(conn, "hrm_emp_contracts", "departmentID"))
        {
            string deptColumn = ColumnExists(conn, "hrm_departments", "dept_name")
                ? "dept_name"
                : (ColumnExists(conn, "hrm_departments", "department") ? "department" : "");
            string sortColumn = ColumnExists(conn, "hrm_emp_contracts", "contractStart")
                ? "contractStart"
                : (ColumnExists(conn, "hrm_emp_contracts", "created_at") ? "created_at" : "empID");

            if (!string.IsNullOrEmpty(deptColumn))
                return string.Format(
                    "IFNULL(NULLIF(TRIM((SELECT d.{0} FROM hrm_emp_contracts c " +
                    "LEFT JOIN hrm_departments d ON c.departmentID = d.ID " +
                    "WHERE c.empID = {1}.empID " +
                    "ORDER BY (CASE WHEN IFNULL(c.contractStatus,'')='VALID' THEN 0 ELSE 1 END), c.{2} DESC " +
                    "LIMIT 1)),''), 'Not recorded')",
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

    // ── Vocabulary ─────────────────────────────────────────────────────
    private static string SessionStatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "ACTIVE": return "Active";
            case "DRAFT": return "Draft";
            case "CLOSED": return "Closed";
            case "ARCHIVED": return "Archived";
            default: return s;
        }
    }

    /// <summary>Classification band as a badge: ok, ok, info, warn, bad.</summary>
    private static string ClassBadge(string c)
    {
        string k = (c ?? "").Trim().ToLowerInvariant();
        if (k == "") return "<span class='hr-muted'>Not scored</span>";
        string kind = "neutral", word = c;
        if (k.StartsWith("exceptional")) { kind = "ok"; word = "Exceptional"; }
        else if (k.StartsWith("above")) { kind = "ok"; word = "Above expectations"; }
        else if (k.StartsWith("satisf")) { kind = "info"; word = "Satisfactory"; }
        else if (k.StartsWith("develop") || k.StartsWith("needs")) { kind = "warn"; word = "Development needed"; }
        else if (k.StartsWith("unsatisf") || k.StartsWith("poor")) { kind = "bad"; word = "Unsatisfactory"; }
        return "<span class='hr-badge hr-badge--" + kind + "'>" + HttpUtility.HtmlEncode(word) + "</span>";
    }

    private static string Pct(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        return Convert.ToDecimal(v).ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        return DateTime.TryParse(val.ToString(), out dt) ? dt.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "";
    }

    private static string Enc(object v) { return HttpUtility.HtmlEncode(HrExport.Clean(SafeStr(v))); }

    private static int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private static string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    // ── Data access ────────────────────────────────────────────────────
    private DataTable ExecuteQuery(string sql)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            return ExecuteQuery(conn, sql);
        }
    }

    private DataTable ExecuteQuery(MySqlConnection conn, string sql)
    {
        DataTable dt = new DataTable();
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
        return dt;
    }
}
