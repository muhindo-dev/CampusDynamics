using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_AppraisalDashboard : System.Web.UI.Page
{
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

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            LoadSessionFilter();
            LoadDashboard();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SESSION FILTER DROPDOWN
    // ═══════════════════════════════════════════════════════════════════
    private void LoadSessionFilter()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT session_id, session_title, status,
                     DATE_FORMAT(period_start,'%d %b %Y') AS ps,
                     DATE_FORMAT(period_end,'%d %b %Y') AS pe
              FROM appraisal_sessions
              ORDER BY FIELD(status,'ACTIVE','DRAFT','CLOSED','ARCHIVED'), created_at DESC");

        StringBuilder sb = new StringBuilder();
        sb.Append("<option value='0'>All Sessions</option>");
        foreach (DataRow r in dt.Rows)
        {
            int sid = Convert.ToInt32(r["session_id"]);
            string label = string.Format("{0} ({1}) — {2} to {3}",
                HttpUtility.HtmlEncode(r["session_title"].ToString()),
                r["status"].ToString(),
                r["ps"].ToString(),
                r["pe"].ToString());
            sb.AppendFormat("<option value='{0}'{1}>{2}</option>",
                sid,
                sid == QsSession ? " selected" : "",
                label);
        }
        litSessionOptions.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MAIN DASHBOARD LOAD
    // ═══════════════════════════════════════════════════════════════════
    private void LoadDashboard()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                LoadKpis(conn);
                LoadUnassignedReviewers(conn);
                LoadPipeline(conn);
                LoadActionRequired(conn);
                LoadCategoryBreakdown(conn);
                LoadDepartmentBreakdown(conn);
                LoadStatusBreakdown(conn);
                LoadRecentActivity(conn);
                LoadOverdueAlerts(conn);
            }
        }
        catch (Exception ex)
        {
            litAlerts.Text = "<div class='pa-alert pa-alert--error'>Error loading dashboard: " +
                HttpUtility.HtmlEncode(ex.Message) + "</div>";
        }
    }

    private string SessionFilter(string alias)
    {
        if (QsSession > 0)
            return string.Format(" AND {0}.session_id = {1}", alias, QsSession);
        return "";
    }

    // Counts are counts of appraisal_records for the session(s) - the same population
    // AppraisalView, AppraisalReports and the session report show. (A VALID-contract
    // filter used to be applied here only, so the dashboard disagreed with every
    // other screen; 116 active employees have no VALID contract row at all.)
    private const string ValidContractFilter = "";

    // ═══════════════════════════════════════════════════════════════════
    //  KPI CARDS
    // ═══════════════════════════════════════════════════════════════════
    private void LoadKpis(MySqlConnection conn)
    {
        string sessionClause = QsSession > 0 ? " AND ar.session_id = " + QsSession : "";
        string baseWhere = " WHERE 1=1" + ValidContractFilter + sessionClause;

        string sql = string.Format(
            @"SELECT
                COUNT(*)                                                                          AS total,
                SUM(CASE WHEN ar.status = 'COMPLETED'               THEN 1 ELSE 0 END)           AS needs_hr,
                SUM(CASE WHEN ar.status = 'HR_REVIEWED'             THEN 1 ELSE 0 END)           AS hr_reviewed,
                SUM(CASE WHEN ar.status IN ('EMPLOYEE_SUBMITTED',
                          'SUPERVISOR_IN_PROGRESS')                  THEN 1 ELSE 0 END)           AS sup_stage,
                SUM(CASE WHEN ar.status IN ('EMPLOYEE_IN_PROGRESS',
                          'RETURNED')                                THEN 1 ELSE 0 END)           AS emp_stage,
                SUM(CASE WHEN ar.status = 'PENDING'                 THEN 1 ELSE 0 END)           AS not_started,
                ROUND(AVG(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED')
                               THEN ar.final_percentage END), 1)                                  AS avg_score
              FROM appraisal_records ar
              {0}", baseWhere);

        DataTable dt = ExecuteQuery(conn, sql);
        DataRow r = dt.Rows[0];

        int total      = SafeInt(r["total"]);
        int notStart   = SafeInt(r["not_started"]);
        int hrDone     = SafeInt(r["hr_reviewed"]);
        int needsHr    = SafeInt(r["needs_hr"]);
        int supDone    = needsHr + hrDone; // COMPLETED (awaiting HR) + HR_REVIEWED
        double pctDone = total > 0 ? Math.Round((double)supDone / total * 100, 1) : 0;

        litKpiTotal.Text      = total.ToString("N0");
        litKpiNeedsHr.Text    = SafeInt(r["needs_hr"]).ToString("N0");
        litKpiHrReviewed.Text = hrDone.ToString("N0");
        litKpiSupStage.Text   = SafeInt(r["sup_stage"]).ToString("N0");
        litKpiEmpStage.Text   = SafeInt(r["emp_stage"]).ToString("N0");
        litKpiNotStarted.Text = notStart.ToString("N0");
        litKpiAvgScore.Text   = (r["avg_score"] != null && r["avg_score"] != DBNull.Value)
            ? Convert.ToDecimal(r["avg_score"]).ToString("F1") + "%" : "—";
        litKpiCompletionPct.Text = pctDone.ToString("F0") + "%";

        string overdueWhere = " WHERE ar.status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED')" +
                              ValidContractFilter +
                              (QsSession > 0 ? " AND ar.session_id = " + QsSession : "") +
                              " AND s.deadline < CURDATE()";
        DataTable dtOver = ExecuteQuery(conn,
            @"SELECT COUNT(*) AS cnt
              FROM appraisal_records ar
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id" +
            overdueWhere);
        litKpiOverdue.Text = SafeInt(dtOver.Rows[0]["cnt"]).ToString("N0");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UNASSIGNED REVIEWERS - open records nobody can review until HR assigns one
    // ═══════════════════════════════════════════════════════════════════
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
            "<div class='pa-banner'>" +
            "<div class='pa-banner__icon'>" +
            "<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>" +
            "<path d='M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2'/><circle cx='8.5' cy='7' r='4'/>" +
            "<line x1='18' y1='8' x2='23' y2='13'/><line x1='23' y1='8' x2='18' y2='13'/></svg>" +
            "</div>" +
            "<div class='pa-banner__body'>" +
            "<div class='pa-banner__title'>{0} open appraisal{1} with no reviewer</div>" +
            "<div class='pa-banner__sub'>No reviewer could be resolved (supervisor / department head). HR must assign one before these can be reviewed.</div>" +
            "</div>" +
            "<a href='{2}' class='pa-banner__btn'>Assign reviewers &rarr;</a>" +
            "</div>",
            cnt, cnt == 1 ? "" : "s", link);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PIPELINE (7 workflow stages)
    // ═══════════════════════════════════════════════════════════════════
    private void LoadPipeline(MySqlConnection conn)
    {
        string baseWhere = " WHERE 1=1" + ValidContractFilter +
                           (QsSession > 0 ? " AND ar.session_id = " + QsSession : "");

        string sql = string.Format(
            @"SELECT
                SUM(CASE WHEN ar.status = 'PENDING'                THEN 1 ELSE 0 END) AS s_pending,
                SUM(CASE WHEN ar.status = 'EMPLOYEE_IN_PROGRESS'   THEN 1 ELSE 0 END) AS s_emp_prog,
                SUM(CASE WHEN ar.status = 'RETURNED'               THEN 1 ELSE 0 END) AS s_returned,
                SUM(CASE WHEN ar.status = 'EMPLOYEE_SUBMITTED'     THEN 1 ELSE 0 END) AS s_emp_sub,
                SUM(CASE WHEN ar.status = 'SUPERVISOR_IN_PROGRESS' THEN 1 ELSE 0 END) AS s_sup_prog,
                SUM(CASE WHEN ar.status = 'COMPLETED'              THEN 1 ELSE 0 END) AS s_completed,
                SUM(CASE WHEN ar.status = 'HR_REVIEWED'            THEN 1 ELSE 0 END) AS s_hr_reviewed
              FROM appraisal_records ar
              {0}", baseWhere);

        DataTable dt = ExecuteQuery(conn, sql);
        if (dt.Rows.Count == 0) return;
        DataRow r = dt.Rows[0];

        litPipeNotStarted.Text   = SafeInt(r["s_pending"]).ToString("N0");
        litPipeEmpProg.Text      = SafeInt(r["s_emp_prog"]).ToString("N0");
        litPipeReturned.Text     = SafeInt(r["s_returned"]).ToString("N0");
        litPipeEmpSubmitted.Text = SafeInt(r["s_emp_sub"]).ToString("N0");
        litPipeSupProg.Text      = SafeInt(r["s_sup_prog"]).ToString("N0");
        litPipeCompleted.Text    = SafeInt(r["s_completed"]).ToString("N0");
        litPipeHrReviewed.Text   = SafeInt(r["s_hr_reviewed"]).ToString("N0");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ACTION REQUIRED — completed records waiting for HR review
    // ═══════════════════════════════════════════════════════════════════
    private void LoadActionRequired(MySqlConnection conn)
    {
        string sessionWhere = ValidContractFilter + (QsSession > 0 ? " AND ar.session_id = " + QsSession : "");
        string deptExpr = BuildDepartmentSqlExpression(conn, "e");

        // The reviewer lives in appraisal_records.reviewer_id (there is no supervisor_id).
        string supervisorJoin   = "LEFT JOIN hrm_employee sup ON sup.empID = ar.reviewer_id";
        string supervisorSelect = "IFNULL(sup.emp_name, 'Unassigned')";

        string sql = string.Format(
            @"SELECT ar.record_id, e.emp_name, e.EMP_CODE,
                     {0} AS department,
                     s.session_title,
                     {1} AS supervisor_name,
                     ar.updated_at,
                     ar.final_percentage,
                     IFNULL(appraisal_classify(ar.final_percentage), IFNULL(ar.classification,'')) AS classification,
                     DATEDIFF(CURDATE(), ar.updated_at) AS days_waiting
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              {2}
              WHERE ar.status = 'COMPLETED' {3}
              ORDER BY ar.updated_at ASC
              LIMIT 25", deptExpr, supervisorSelect, supervisorJoin, sessionWhere);

        DataTable dt = ExecuteQuery(conn, sql);
        int count = dt.Rows.Count;

        if (count > 0)
        {
            litActionBanner.Text = string.Format(
                "<div class='pa-banner'>" +
                "<div class='pa-banner__icon'>" +
                "<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>" +
                "<path d='M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z'/>" +
                "<line x1='12' y1='9' x2='12' y2='13'/><line x1='12' y1='17' x2='12.01' y2='17'/></svg>" +
                "</div>" +
                "<div class='pa-banner__body'>" +
                "<div class='pa-banner__title'>{0} appraisal{1} awaiting HR review</div>" +
                "<div class='pa-banner__sub'>Completed by supervisors — ready for final HR assessment.</div>" +
                "</div>" +
                "<a href='AppraisalView.aspx?status=COMPLETED' class='pa-banner__btn'>Open HR Review Queue &rarr;</a>" +
                "</div>",
                count, count == 1 ? "" : "s");
        }
        else
        {
            litActionBanner.Text = "";
        }

        StringBuilder sb = new StringBuilder();
        if (count == 0)
        {
            sb.Append("<tr><td colspan='8' style='text-align:center;color:#888;padding:24px;font-size:13px;'>" +
                      "&#10003; No records awaiting HR review — all caught up!</td></tr>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                int days = SafeInt(r["days_waiting"]);
                string daysClass = days > 14 ? "pa-ar-days--warn" : "pa-ar-days--ok";
                string daysText  = days == 0 ? "Today" : days + "d";
                string score = (r["final_percentage"] != null && r["final_percentage"] != DBNull.Value)
                    ? Convert.ToDecimal(r["final_percentage"]).ToString("F1") + "%" : "—";
                string classification = SafeStr(r["classification"]);

                sb.Append("<tr>");
                sb.AppendFormat(
                    "<td><div class='pa-ar-table__emp'>{0}</div><div class='pa-ar-table__code'>{1}</div></td>",
                    HttpUtility.HtmlEncode(SafeStr(r["emp_name"])),
                    HttpUtility.HtmlEncode(SafeStr(r["EMP_CODE"])));
                sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                    HttpUtility.HtmlEncode(SafeStr(r["department"])));
                sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                    HttpUtility.HtmlEncode(SafeStr(r["session_title"])));
                sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                    HttpUtility.HtmlEncode(SafeStr(r["supervisor_name"])));
                sb.AppendFormat("<td class='pa-num' style='font-weight:700;'>{0}</td>", score);
                sb.AppendFormat("<td><span class='pa-badge pa-badge--completed'>{0}</span></td>",
                    string.IsNullOrEmpty(classification) ? "—" : HttpUtility.HtmlEncode(classification));
                sb.AppendFormat("<td class='pa-ar-days {0}'>{1}</td>", daysClass, daysText);
                sb.AppendFormat("<td style='text-align:center;'>" +
                    "<a href='AppraisalView.aspx?rid={0}' class='pa-ar-btn'>Review</a></td>",
                    SafeInt(r["record_id"]));
                sb.Append("</tr>");
            }
        }
        litActionRequired.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CATEGORY BREAKDOWN (Academic / Administrative / Support)
    // ═══════════════════════════════════════════════════════════════════
    private void LoadCategoryBreakdown(MySqlConnection conn)
    {
        string sessionWhere = ValidContractFilter + (QsSession > 0 ? " AND ar.session_id = " + QsSession : "");

        string sql = string.Format(
            @"SELECT
                ar.staff_category,
                COUNT(*) AS total,
                SUM(CASE WHEN ar.status = 'HR_REVIEWED'                              THEN 1 ELSE 0 END) AS hr_done,
                SUM(CASE WHEN ar.status = 'COMPLETED'                                THEN 1 ELSE 0 END) AS awaiting_hr,
                SUM(CASE WHEN ar.status IN ('EMPLOYEE_IN_PROGRESS','RETURNED',
                          'EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS')              THEN 1 ELSE 0 END) AS active,
                SUM(CASE WHEN ar.status = 'PENDING'                                  THEN 1 ELSE 0 END) AS pending,
                ROUND(AVG(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED')
                               THEN ar.final_percentage END), 1) AS avg_score
              FROM appraisal_records ar
              WHERE 1=1 {0}
              GROUP BY ar.staff_category
              ORDER BY FIELD(ar.staff_category,'ACADEMIC','ADMINISTRATIVE','SUPPORT')", sessionWhere);

        DataTable dt = ExecuteQuery(conn, sql);

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<tr><td colspan='8' style='text-align:center;color:#999;padding:20px;'>No appraisal data available</td></tr>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                int total    = SafeInt(r["total"]);
                int hrDone   = SafeInt(r["hr_done"]);
                int awaiting = SafeInt(r["awaiting_hr"]);
                int active   = SafeInt(r["active"]);
                int pending  = SafeInt(r["pending"]);
                double pct = total > 0 ? Math.Round((double)hrDone / total * 100, 1) : 0;
                string avg = (r["avg_score"] != null && r["avg_score"] != DBNull.Value)
                    ? Convert.ToDecimal(r["avg_score"]).ToString("F1") + "%" : "—";

                sb.Append("<tr>");
                sb.AppendFormat("<td><strong>{0}</strong></td>", HttpUtility.HtmlEncode(SafeStr(r["staff_category"])));
                sb.AppendFormat("<td class='pa-num'>{0}</td>", total);
                sb.AppendFormat("<td class='pa-num'>{0}</td>", hrDone);
                sb.AppendFormat("<td class='pa-num'>{0}</td>", awaiting);
                sb.AppendFormat("<td class='pa-num'>{0}</td>", active);
                sb.AppendFormat("<td class='pa-num'>{0}</td>", pending);
                sb.AppendFormat("<td>{0}</td>", BuildMiniProgress(hrDone, total));
                sb.AppendFormat("<td class='pa-num'>{0}</td>", avg);
                sb.Append("</tr>");
            }
        }
        litCategoryRows.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DEPARTMENT BREAKDOWN
    // ═══════════════════════════════════════════════════════════════════
    private void LoadDepartmentBreakdown(MySqlConnection conn)
    {
        string sessionWhere = ValidContractFilter + (QsSession > 0 ? " AND ar.session_id = " + QsSession : "");
        string departmentExpr = BuildDepartmentSqlExpression(conn, "e");

        string sql = string.Format(
            @"SELECT
                {0} AS department,
                COUNT(*) AS total,
                SUM(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN 1 ELSE 0 END) AS completed,
                SUM(CASE WHEN ar.status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED') THEN 1 ELSE 0 END) AS outstanding,
                ROUND(AVG(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED')
                               THEN ar.final_percentage END), 1) AS avg_score
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              WHERE 1=1 {1}
              GROUP BY {0}
              ORDER BY COUNT(*) DESC
              LIMIT 20", departmentExpr, sessionWhere);

        DataTable dt = ExecuteQuery(conn, sql);

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<tr><td colspan='6' style='text-align:center;color:#999;padding:20px;'>No data</td></tr>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                int total = SafeInt(r["total"]);
                int comp  = SafeInt(r["completed"]);
                string avg = (r["avg_score"] != null && r["avg_score"] != DBNull.Value)
                    ? Convert.ToDecimal(r["avg_score"]).ToString("F1") + "%" : "—";

                sb.Append("<tr>");
                sb.AppendFormat("<td>{0}</td>", HttpUtility.HtmlEncode(SafeStr(r["department"])));
                sb.AppendFormat("<td class='pa-num'>{0}</td>", total);
                sb.AppendFormat("<td class='pa-num'>{0}</td>", comp);
                sb.AppendFormat("<td class='pa-num'>{0}</td>", SafeInt(r["outstanding"]));
                sb.AppendFormat("<td>{0}</td>", BuildMiniProgress(comp, total));
                sb.AppendFormat("<td class='pa-num'>{0}</td>", avg);
                sb.Append("</tr>");
            }
        }
        litDeptRows.Text = sb.ToString();
    }

    private string BuildDepartmentSqlExpression(MySqlConnection conn, string employeeAlias)
    {
        if (ColumnExists(conn, "hrm_employee", "department"))
            return string.Format("IFNULL(NULLIF(TRIM({0}.department),''), 'Unassigned')", employeeAlias);

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
                    "LIMIT 1)),''), 'Unassigned')",
                    deptColumn, employeeAlias, sortColumn);
        }

        return "'Unassigned'";
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

    // ═══════════════════════════════════════════════════════════════════
    //  STATUS DISTRIBUTION
    // ═══════════════════════════════════════════════════════════════════
    private void LoadStatusBreakdown(MySqlConnection conn)
    {
        string sessionWhere = ValidContractFilter + (QsSession > 0 ? " AND ar.session_id = " + QsSession : "");

        string sql = string.Format(
            @"SELECT ar.status, COUNT(*) AS cnt
              FROM appraisal_records ar
              WHERE 1=1 {0}
              GROUP BY ar.status
              ORDER BY FIELD(ar.status,'PENDING','EMPLOYEE_IN_PROGRESS','RETURNED',
                  'EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS','COMPLETED','HR_REVIEWED','CANCELLED')",
            sessionWhere);

        DataTable dt = ExecuteQuery(conn, sql);

        int grandTotal = 0;
        foreach (DataRow r in dt.Rows) grandTotal += SafeInt(r["cnt"]);

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<div class='pa-status-empty'>No appraisal records found</div>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                string status = SafeStr(r["status"]);
                int cnt = SafeInt(r["cnt"]);
                double pct = grandTotal > 0 ? Math.Round((double)cnt / grandTotal * 100, 1) : 0;
                string color = GetStatusColor(status);
                string label = FormatStatusLabel(status);

                sb.Append("<div class='pa-sbar'>");
                sb.AppendFormat("<span class='pa-sbar__lbl'>{0}</span>", label);
                sb.AppendFormat("<span class='pa-sbar__n'>{0}</span>", cnt);
                sb.AppendFormat("<div class='pa-sbar__track'>" +
                    "<div class='pa-sbar__fill' style='width:{0}%;background:{1};'></div></div>", pct, color);
                sb.AppendFormat("<span class='pa-sbar__pct'>{0}%</span>", pct);
                sb.Append("</div>");
            }
        }
        litStatusBars.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RECENT ACTIVITY
    // ═══════════════════════════════════════════════════════════════════
    private void LoadRecentActivity(MySqlConnection conn)
    {
        string sessionWhere = ValidContractFilter + (QsSession > 0 ? " AND ar.session_id = " + QsSession : "");
        string deptExpr = BuildDepartmentSqlExpression(conn, "e");

        string sql = string.Format(
            @"SELECT ar.record_id, ar.status, ar.updated_at, ar.final_percentage,
                     e.emp_name, e.EMP_CODE,
                     {0} AS department,
                     s.session_title
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE 1=1 {1}
              ORDER BY ar.updated_at DESC
              LIMIT 15", deptExpr, sessionWhere);

        DataTable dt = ExecuteQuery(conn, sql);

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<tr><td colspan='7' style='text-align:center;color:#999;padding:20px;'>No activity yet</td></tr>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                string status = SafeStr(r["status"]);
                int rid = SafeInt(r["record_id"]);

                sb.Append("<tr>");
                sb.AppendFormat(
                    "<td><strong>{0}</strong><br/><span style='font-size:10px;color:#999;'>{1}</span></td>",
                    HttpUtility.HtmlEncode(SafeStr(r["emp_name"])),
                    HttpUtility.HtmlEncode(SafeStr(r["EMP_CODE"])));
                sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                    HttpUtility.HtmlEncode(SafeStr(r["department"])));
                sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                    HttpUtility.HtmlEncode(SafeStr(r["session_title"])));
                sb.AppendFormat("<td><span class='pa-badge pa-badge--{0}'>{1}</span></td>",
                    GetRecordBadgeModifier(status), FormatStatusLabel(status));

                string score = (r["final_percentage"] != null && r["final_percentage"] != DBNull.Value)
                    ? Convert.ToDecimal(r["final_percentage"]).ToString("F1") + "%" : "—";
                sb.AppendFormat("<td class='pa-num'>{0}</td>", score);
                sb.AppendFormat("<td style='font-size:11px;color:#888;white-space:nowrap;'>{0}</td>",
                    FormatTimeAgo(r["updated_at"]));
                sb.AppendFormat("<td style='text-align:center;'>" +
                    "<a href='AppraisalView.aspx?rid={0}' class='pa-ar-btn'>View</a></td>", rid);
                sb.Append("</tr>");
            }
        }
        litRecentRows.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  OVERDUE ALERTS
    // ═══════════════════════════════════════════════════════════════════
    private void LoadOverdueAlerts(MySqlConnection conn)
    {
        string sessionWhere = ValidContractFilter + (QsSession > 0 ? " AND ar.session_id = " + QsSession : "");

        string sql = string.Format(
            @"SELECT s.session_title, s.deadline,
                     COUNT(*) AS cnt,
                     DATEDIFF(CURDATE(), s.deadline) AS days_overdue
              FROM appraisal_records ar
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE ar.status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED')
                AND s.deadline < CURDATE()
                {0}
              GROUP BY s.session_id
              ORDER BY s.deadline ASC
              LIMIT 10", sessionWhere);

        DataTable dt = ExecuteQuery(conn, sql);

        StringBuilder sb = new StringBuilder();
        if (dt.Rows.Count == 0)
        {
            sb.Append("<div class='pa-no-alerts'>");
            sb.Append("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='20 6 9 17 4 12'/></svg>");
            sb.Append(" No overdue appraisals — all on track!");
            sb.Append("</div>");
        }
        else
        {
            foreach (DataRow r in dt.Rows)
            {
                int daysOver = SafeInt(r["days_overdue"]);
                string urgency = daysOver > 30 ? "critical" : daysOver > 14 ? "high" : "medium";
                sb.AppendFormat("<div class='pa-alert-item pa-alert-item--{0}'>", urgency);
                sb.AppendFormat("<div class='pa-alert-item__title'>{0}</div>",
                    HttpUtility.HtmlEncode(SafeStr(r["session_title"])));
                sb.AppendFormat("<div class='pa-alert-item__detail'>{0} outstanding &middot; {1} days overdue &middot; Deadline: {2}</div>",
                    SafeInt(r["cnt"]), daysOver, FormatDateDisplay(r["deadline"]));
                sb.Append("</div>");
            }
        }
        litAlerts.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private string BuildMiniProgress(int completed, int total)
    {
        if (total == 0) return "<span style='color:#bbb;font-size:10px;'>—</span>";
        double pct = Math.Round((double)completed / total * 100, 1);
        string color = pct >= 75 ? "#1a7a4a" : pct >= 40 ? "#d97706" : pct > 0 ? "#174DA4" : "#e0e5ed";
        return string.Format(
            "<div class='pa-prog'>" +
            "<div class='pa-prog__track'><div class='pa-prog__fill' style='width:{0}%;background:{1};'></div></div>" +
            "<span class='pa-prog__txt'>{0}%</span>" +
            "</div>",
            pct, color);
    }

    private string GetStatusColor(string status)
    {
        switch (status.ToUpper())
        {
            case "PENDING":                return "#6c757d";
            case "EMPLOYEE_IN_PROGRESS":   return "#17a2b8";
            case "RETURNED":               return "#ea580c";
            case "EMPLOYEE_SUBMITTED":     return "#007bff";
            case "SUPERVISOR_IN_PROGRESS": return "#f59e0b";
            case "COMPLETED":              return "#92400e";
            case "HR_REVIEWED":            return "#0d9488";
            case "CANCELLED":              return "#dc3545";
            default:                       return "#adb5bd";
        }
    }

    private string FormatStatusLabel(string status)
    {
        switch (status.ToUpper())
        {
            case "PENDING":                return "Not Started";
            case "EMPLOYEE_IN_PROGRESS":   return "Employee In Progress";
            case "RETURNED":               return "Returned to Employee";
            case "EMPLOYEE_SUBMITTED":     return "Employee Submitted";
            case "SUPERVISOR_IN_PROGRESS": return "Supervisor Reviewing";
            case "COMPLETED":              return "Awaiting HR";
            case "HR_REVIEWED":            return "HR Reviewed";
            case "CANCELLED":              return "Cancelled";
            default:                       return status;
        }
    }

    private string GetRecordBadgeModifier(string status)
    {
        switch (status.ToUpper())
        {
            case "PENDING":                return "pending";
            case "EMPLOYEE_IN_PROGRESS":   return "emp-prog";
            case "RETURNED":               return "returned";
            case "EMPLOYEE_SUBMITTED":     return "emp-done";
            case "SUPERVISOR_IN_PROGRESS": return "sup-prog";
            case "COMPLETED":              return "completed";
            case "HR_REVIEWED":            return "hr-reviewed";
            case "CANCELLED":              return "cancelled";
            default:                       return "pending";
        }
    }

    private string FormatTimeAgo(object val)
    {
        if (val == null || val == DBNull.Value) return "—";
        DateTime dt;
        if (!DateTime.TryParse(val.ToString(), out dt)) return val.ToString();
        TimeSpan diff = DateTime.Now - dt;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return string.Format("{0}m ago", (int)diff.TotalMinutes);
        if (diff.TotalHours < 24) return string.Format("{0}h ago", (int)diff.TotalHours);
        if (diff.TotalDays < 7) return string.Format("{0}d ago", (int)diff.TotalDays);
        return dt.ToString("dd MMM yyyy");
    }

    private string FormatDateDisplay(object val)
    {
        if (val == null || val == DBNull.Value) return "—";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt))
            return dt.ToString("dd MMM yyyy");
        return val.ToString();
    }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private string SafeStr(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DATA ACCESS
    // ═══════════════════════════════════════════════════════════════════
    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }

    private DataTable ExecuteQuery(MySqlConnection conn, string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            if (parms != null)
                foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
        }
        return dt;
    }
}
