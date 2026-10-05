using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_AppraisalReports : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ─── Query-string helpers ──────────────────────────────────────────
    private int QsSession
    {
        get
        {
            int v;
            return int.TryParse(Request.QueryString["sid"] ?? "0", out v) && v > 0 ? v : 0;
        }
    }

    private string QsDepartment
    {
        get { return (Request.QueryString["dept"] ?? "").Trim(); }
    }

    private string QsCategory
    {
        get { return (Request.QueryString["cat"] ?? "").Trim(); }
    }

    private string QsStatus
    {
        get { return (Request.QueryString["st"] ?? "").Trim(); }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        // ── AJAX CSV export ──
        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (ajax == "exportcsv")
        {
            // The ?ajax= path runs here, BEFORE SidebarMaster's login check, so it must
            // gate itself or the full staff score list is downloadable anonymously.
            if (!IsCallerAuthenticated())
            {
                Response.Clear();
                Response.ContentType = "text/plain";
                Response.Write("Your session has expired. Please sign in again, then retry the export.");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }
            HandleExportCsv();
            return;
        }

        if (!IsPostBack)
        {
            LoadFilters();
            LoadReport();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FILTER DROPDOWNS
    // ═══════════════════════════════════════════════════════════════════
    private void LoadFilters()
    {
        // ── Session filter ──
        DataTable dtSess = ExecuteQuery(
            @"SELECT session_id, session_title, status,
                     DATE_FORMAT(period_start,'%d %b %Y') AS ps,
                     DATE_FORMAT(period_end,'%d %b %Y') AS pe
              FROM appraisal_sessions
              ORDER BY FIELD(status,'ACTIVE','DRAFT','CLOSED','ARCHIVED'), created_at DESC");

        StringBuilder sbSess = new StringBuilder();
        sbSess.Append("<option value='0'>All Sessions</option>");
        foreach (DataRow r in dtSess.Rows)
        {
            int sid = Convert.ToInt32(r["session_id"]);
            string label = string.Format("{0} ({1}) \u2014 {2} to {3}",
                HttpUtility.HtmlEncode(r["session_title"].ToString()),
                r["status"].ToString(),
                r["ps"].ToString(),
                r["pe"].ToString());
            sbSess.AppendFormat("<option value='{0}'{1}>{2}</option>",
                sid,
                sid == QsSession ? " selected" : "",
                label);
        }
        litSessionOptions.Text = sbSess.ToString();

        // ── Department filter ──
                string deptExpr = GetDepartmentSelectExpression("e");
                DataTable dtDept = ExecuteQuery(string.Format(
                        @"SELECT DISTINCT {0} AS dept
                            FROM appraisal_records ar
                            INNER JOIN hrm_employee e ON e.empID = ar.employee_id
                            ORDER BY dept", deptExpr));

        StringBuilder sbDept = new StringBuilder();
        sbDept.Append("<option value=''>All Departments</option>");
        foreach (DataRow r in dtDept.Rows)
        {
            string dept = SafeStr(r["dept"]);
            sbDept.AppendFormat("<option value='{0}'{1}>{2}</option>",
                HttpUtility.HtmlAttributeEncode(dept),
                dept == QsDepartment ? " selected" : "",
                HttpUtility.HtmlEncode(dept));
        }
        litDeptOptions.Text = sbDept.ToString();

        // ── Category filter ──
        StringBuilder sbCat = new StringBuilder();
        sbCat.Append("<option value=''>All Categories</option>");
        string[] cats = new string[] { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" };
        foreach (string c in cats)
        {
            sbCat.AppendFormat("<option value='{0}'{1}>{2}</option>",
                c,
                c == QsCategory ? " selected" : "",
                c.Substring(0, 1) + c.Substring(1).ToLower());
        }
        litCatOptions.Text = sbCat.ToString();

        // ── Status filter ──
        StringBuilder sbSt = new StringBuilder();
        sbSt.Append("<option value=''>All Statuses</option>");
        string[][] statuses = new string[][] {
            new string[] { "PENDING", "Not Started" },
            new string[] { "EMPLOYEE_IN_PROGRESS", "Employee In Progress" },
            new string[] { "RETURNED", "Returned to Employee" },
            new string[] { "EMPLOYEE_SUBMITTED", "Employee Submitted" },
            new string[] { "SUPERVISOR_IN_PROGRESS", "Supervisor Reviewing" },
            new string[] { "COMPLETED", "Awaiting HR Review" },
            new string[] { "HR_REVIEWED", "HR Reviewed" },
            new string[] { "CANCELLED", "Cancelled" }
        };
        foreach (string[] st in statuses)
        {
            sbSt.AppendFormat("<option value='{0}'{1}>{2}</option>",
                st[0],
                st[0] == QsStatus ? " selected" : "",
                st[1]);
        }
        litStatusOptions.Text = sbSt.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BUILD WHERE CLAUSE
    // ═══════════════════════════════════════════════════════════════════
    private string BuildWhereClause(string arAlias, string empAlias, MySqlConnection conn)
    {
        StringBuilder sb = new StringBuilder(" WHERE 1=1");
        string deptExpr = BuildDepartmentSqlExpression(conn, empAlias);
        if (QsSession > 0)
            sb.AppendFormat(" AND {0}.session_id = {1}", arAlias, QsSession);
        if (!string.IsNullOrEmpty(QsDepartment))
            sb.AppendFormat(" AND {0} = @dept", deptExpr);
        if (!string.IsNullOrEmpty(QsCategory))
            sb.AppendFormat(" AND {0}.staff_category = @cat", arAlias);
        if (!string.IsNullOrEmpty(QsStatus))
            sb.AppendFormat(" AND {0}.status = @st", arAlias);
        return sb.ToString();
    }

    private void AddFilterParams(MySqlCommand cmd)
    {
        if (!string.IsNullOrEmpty(QsDepartment))
            cmd.Parameters.AddWithValue("@dept", QsDepartment);
        if (!string.IsNullOrEmpty(QsCategory))
            cmd.Parameters.AddWithValue("@cat", QsCategory);
        if (!string.IsNullOrEmpty(QsStatus))
            cmd.Parameters.AddWithValue("@st", QsStatus);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MAIN REPORT LOAD
    // ═══════════════════════════════════════════════════════════════════
    private void LoadReport()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                LoadSummaryKpis(conn);
                LoadScoreDistribution(conn);
                LoadDepartmentSummary(conn);
                LoadDetailedRecords(conn);
            }
        }
        catch (Exception ex)
        {
            litError.Text = "<div class='pa-alert pa-alert--error'>Error loading report: " +
                HttpUtility.HtmlEncode(ex.Message) + "</div>";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SUMMARY KPIs
    // ═══════════════════════════════════════════════════════════════════
    private void LoadSummaryKpis(MySqlConnection conn)
    {
        string where = BuildWhereClause("ar", "e", conn);
        string sql = string.Format(
            @"SELECT
                COUNT(*)                                                                                    AS total,
                SUM(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN 1 ELSE 0 END)                  AS completed,
                SUM(CASE WHEN ar.status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED') THEN 1 ELSE 0 END)   AS outstanding,
                ROUND(AVG(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN ar.final_percentage END),1) AS avg_score,
                ROUND(MIN(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN ar.final_percentage END),1) AS min_score,
                ROUND(MAX(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN ar.final_percentage END),1) AS max_score
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              {0}", where);

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            AddFilterParams(cmd);
            DataTable dt = new DataTable();
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }

            if (dt.Rows.Count == 0) return;
            DataRow r = dt.Rows[0];

            int total     = SafeInt(r["total"]);
            int completed = SafeInt(r["completed"]);
            int outstanding = SafeInt(r["outstanding"]);
            double pct = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0;

            litKpiTotal.Text      = total.ToString("N0");
            litKpiCompleted.Text  = completed.ToString("N0");
            litKpiOutstanding.Text = outstanding.ToString("N0");
            litKpiRate.Text       = pct.ToString("F1") + "%";
            litKpiAvg.Text        = FormatScore(r["avg_score"]);
            litKpiMin.Text        = FormatScore(r["min_score"]);
            litKpiMax.Text        = FormatScore(r["max_score"]);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SCORE DISTRIBUTION (Classification bands)
    // ═══════════════════════════════════════════════════════════════════
    private void LoadScoreDistribution(MySqlConnection conn)
    {
        string where = BuildWhereClause("ar", "e", conn);
        string sql = string.Format(
            @"SELECT
                SUM(CASE WHEN appraisal_classify(ar.final_percentage) = 'Exceptional'        THEN 1 ELSE 0 END) AS c_exc,
                SUM(CASE WHEN appraisal_classify(ar.final_percentage) = 'Above Expectations' THEN 1 ELSE 0 END) AS c_above,
                SUM(CASE WHEN appraisal_classify(ar.final_percentage) = 'Satisfactory'       THEN 1 ELSE 0 END) AS c_sat,
                SUM(CASE WHEN appraisal_classify(ar.final_percentage) = 'Development Needed' THEN 1 ELSE 0 END) AS c_dev,
                SUM(CASE WHEN appraisal_classify(ar.final_percentage) = 'Unsatisfactory'     THEN 1 ELSE 0 END) AS c_unsat,
                COUNT(ar.final_percentage)                                                                     AS total_completed
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              {0}
              AND ar.status IN ('COMPLETED','HR_REVIEWED')", where);

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            AddFilterParams(cmd);
            DataTable dt = new DataTable();
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }

            if (dt.Rows.Count == 0) return;
            DataRow r = dt.Rows[0];

            // Denominator = scored COMPLETED + HR_REVIEWED records (the WHERE below
            // restricts to both), so the bands always add up to 100%.
            int totalComp   = SafeInt(r["total_completed"]);
            int exc         = SafeInt(r["c_exc"]);
            int above       = SafeInt(r["c_above"]);
            int sat         = SafeInt(r["c_sat"]);
            int dev         = SafeInt(r["c_dev"]);
            int unsat       = SafeInt(r["c_unsat"]);

            StringBuilder sb = new StringBuilder();

            if (totalComp == 0)
            {
                sb.Append("<div style='text-align:center;color:#999;padding:24px;font-size:13px;'>No completed appraisals to display</div>");
            }
            else
            {
                // Canonical scale (SQL appraisal_classify): 90 / 75 / 60 / 50
                AppendDistBar(sb, "Exceptional (\u226590%)", exc, totalComp, "#28a745");
                AppendDistBar(sb, "Above Expectations (75\u201389%)", above, totalComp, "#17a2b8");
                AppendDistBar(sb, "Satisfactory (60\u201374%)", sat, totalComp, "#174DA4");
                AppendDistBar(sb, "Development Needed (50\u201359%)", dev, totalComp, "#f59e0b");
                AppendDistBar(sb, "Unsatisfactory (&lt;50%)", unsat, totalComp, "#dc3545");
            }

            litDistBars.Text = sb.ToString();
        }
    }

    private void AppendDistBar(StringBuilder sb, string label, int count, int total, string color)
    {
        double pct = total > 0 ? Math.Round((double)count / total * 100, 1) : 0;
        sb.Append("<div class='pa-dist-row'>");
        sb.AppendFormat("<span class='pa-dist-row__label'>{0}</span>", label);
        sb.AppendFormat("<span class='pa-dist-row__count'>{0}</span>", count);
        sb.AppendFormat("<div class='pa-dist-row__bar'><div class='pa-dist-row__fill' style='width:{0}%;background:{1};'></div></div>", pct, color);
        sb.AppendFormat("<span class='pa-dist-row__pct'>{0}%</span>", pct);
        sb.Append("</div>");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DEPARTMENT SUMMARY TABLE
    // ═══════════════════════════════════════════════════════════════════
    private void LoadDepartmentSummary(MySqlConnection conn)
    {
                string where = BuildWhereClause("ar", "e", conn);
                string deptExpr = BuildDepartmentSqlExpression(conn, "e");
        string sql = string.Format(
            @"SELECT
                                {0} AS department,
                COUNT(*) AS total,
                SUM(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN 1 ELSE 0 END) AS completed,
                SUM(CASE WHEN ar.status NOT IN ('COMPLETED','HR_REVIEWED','CANCELLED') THEN 1 ELSE 0 END) AS outstanding,
                ROUND(AVG(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN ar.final_percentage END),1) AS avg_score,
                ROUND(MIN(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN ar.final_percentage END),1) AS min_score,
                ROUND(MAX(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN ar.final_percentage END),1) AS max_score
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
                            {1}
                            GROUP BY {0}
                            ORDER BY COUNT(*) DESC", deptExpr, where);

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            AddFilterParams(cmd);
            DataTable dt = new DataTable();
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }

            StringBuilder sb = new StringBuilder();
            if (dt.Rows.Count == 0)
            {
                sb.Append("<tr><td colspan='7' style='text-align:center;color:#999;padding:20px;'>No data available</td></tr>");
            }
            else
            {
                foreach (DataRow r in dt.Rows)
                {
                    int total = SafeInt(r["total"]);
                    int comp  = SafeInt(r["completed"]);
                    double pct = total > 0 ? Math.Round((double)comp / total * 100, 1) : 0;

                    sb.Append("<tr>");
                    sb.AppendFormat("<td><strong>{0}</strong></td>", HttpUtility.HtmlEncode(SafeStr(r["department"])));
                    sb.AppendFormat("<td class='pa-num'>{0}</td>", total);
                    sb.AppendFormat("<td class='pa-num'>{0}</td>", comp);
                    sb.AppendFormat("<td class='pa-num'>{0}</td>", SafeInt(r["outstanding"]));
                    sb.AppendFormat("<td>{0}</td>", BuildMiniProgress(comp, total));
                    sb.AppendFormat("<td class='pa-num'>{0}</td>", FormatScore(r["avg_score"]));
                    sb.AppendFormat("<td class='pa-num'>{0} / {1}</td>",
                        FormatScoreShort(r["min_score"]),
                        FormatScoreShort(r["max_score"]));
                    sb.Append("</tr>");
                }
            }
            litDeptRows.Text = sb.ToString();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DETAILED RECORDS TABLE
    // ═══════════════════════════════════════════════════════════════════
    private void LoadDetailedRecords(MySqlConnection conn)
    {
                string where = BuildWhereClause("ar", "e", conn);
                string deptExpr = BuildDepartmentSqlExpression(conn, "e");
        string sql = string.Format(
            @"SELECT
                ar.record_id, ar.status, ar.staff_category,
                ar.final_percentage, ar.classification,
                ar.employee_submitted_at, ar.supervisor_submitted_at,
                e.emp_name, e.EMP_CODE,
                                {0} AS department,
                s.session_title,
                sup.emp_name AS supervisor_name
              FROM appraisal_records ar
              INNER JOIN hrm_employee e   ON e.empID = ar.employee_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              LEFT  JOIN hrm_employee sup ON sup.empID = ar.reviewer_id
                            {1}
              ORDER BY
                FIELD(ar.status,'HR_REVIEWED','COMPLETED','SUPERVISOR_IN_PROGRESS','EMPLOYEE_SUBMITTED',
                      'EMPLOYEE_IN_PROGRESS','RETURNED','PENDING','CANCELLED'),
                ar.final_percentage DESC,
                e.emp_name ASC
                            LIMIT 500", deptExpr, where);

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            AddFilterParams(cmd);
            DataTable dt = new DataTable();
            using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }

            litRecordCount.Text = dt.Rows.Count.ToString("N0");

            StringBuilder sb = new StringBuilder();
            if (dt.Rows.Count == 0)
            {
                sb.Append("<tr><td colspan='9' style='text-align:center;color:#999;padding:24px;'>No records match the selected filters</td></tr>");
            }
            else
            {
                int rowNum = 0;
                foreach (DataRow r in dt.Rows)
                {
                    rowNum++;
                    string status = SafeStr(r["status"]);
                    string score = FormatScore(r["final_percentage"]);
                    string classif = Classify(r["final_percentage"], SafeStr(r["classification"]));
                    if (string.IsNullOrEmpty(classif)) classif = "\u2014";

                    sb.Append("<tr>");
                    sb.AppendFormat("<td class='pa-num' style='color:#aaa;'>{0}</td>", rowNum);
                    sb.AppendFormat("<td><strong>{0}</strong><br/><span style='font-size:10px;color:#999;'>{1}</span></td>",
                        HttpUtility.HtmlEncode(SafeStr(r["emp_name"])),
                        HttpUtility.HtmlEncode(SafeStr(r["EMP_CODE"])));
                    sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                        HttpUtility.HtmlEncode(SafeStr(r["department"])));
                    sb.AppendFormat("<td><span class='pa-cat-badge pa-cat-badge--{0}'>{1}</span></td>",
                        SafeStr(r["staff_category"]).ToLower(),
                        FormatCategory(SafeStr(r["staff_category"])));
                    sb.AppendFormat("<td><span class='pa-rec-badge pa-rec-badge--{0}'>{1}</span></td>",
                        GetRecordBadgeModifier(status), FormatStatusLabel(status));
                    sb.AppendFormat("<td class='pa-num' style='font-weight:700;{0}'>{1}</td>",
                        GetScoreStyle(r["final_percentage"]), score);
                    sb.AppendFormat("<td style='font-size:11px;'>{0}</td>", HttpUtility.HtmlEncode(classif));
                    sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                        HttpUtility.HtmlEncode(SafeStr(r["supervisor_name"])));
                    sb.AppendFormat("<td style='font-size:11px;'>{0}</td>",
                        HttpUtility.HtmlEncode(SafeStr(r["session_title"])));
                    sb.Append("</tr>");
                }
            }
            litRecordRows.Text = sb.ToString();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CSV EXPORT
    // ═══════════════════════════════════════════════════════════════════
    private void HandleExportCsv()
    {
        try
        {
            DataTable dt;
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                string deptExpr = BuildDepartmentSqlExpression(conn, "e");
                string where = BuildWhereClause("ar", "e", conn);
                string sql = string.Format(
                    @"SELECT
                        e.EMP_CODE AS 'Employee Code',
                        e.emp_name AS 'Employee Name',
                        {0} AS 'Department',
                        ar.staff_category AS 'Category',
                        ar.status AS 'Status',
                        ar.final_percentage AS 'Score (%)',
                        IFNULL(appraisal_classify(ar.final_percentage), ar.classification) AS 'Classification',
                        sup.emp_name AS 'Supervisor',
                        s.session_title AS 'Session',
                        DATE_FORMAT(ar.employee_submitted_at,'%Y-%m-%d %H:%i') AS 'Employee Submitted',
                        DATE_FORMAT(ar.supervisor_submitted_at,'%Y-%m-%d %H:%i') AS 'Supervisor Submitted',
                        DATE_FORMAT(ar.created_at,'%Y-%m-%d %H:%i') AS 'Created'
                      FROM appraisal_records ar
                      INNER JOIN hrm_employee e   ON e.empID = ar.employee_id
                      INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
                      LEFT  JOIN hrm_employee sup ON sup.empID = ar.reviewer_id
                      {1}
                      ORDER BY {0}, e.emp_name", deptExpr, where);

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    AddFilterParams(cmd);
                    dt = new DataTable();
                    using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
                }
            }

            // Build filename
            string fileLabel = "AppraisalReport";
            if (QsSession > 0) fileLabel += "_Session" + QsSession;
            fileLabel += "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            Response.Clear();
            Response.ContentType = "text/csv";
            Response.AddHeader("Content-Disposition",
                string.Format("attachment; filename={0}.csv", fileLabel));

            StringBuilder sb = new StringBuilder();

            // Header row
            List<string> headers = new List<string>();
            foreach (DataColumn col in dt.Columns)
                headers.Add(CsvEscape(col.ColumnName));
            sb.AppendLine(string.Join(",", headers.ToArray()));

            // Data rows
            foreach (DataRow r in dt.Rows)
            {
                List<string> vals = new List<string>();
                foreach (DataColumn col in dt.Columns)
                {
                    string val = (r[col] == null || r[col] == DBNull.Value) ? "" : r[col].ToString();
                    vals.Add(CsvEscape(val));
                }
                sb.AppendLine(string.Join(",", vals.ToArray()));
            }

            Response.Write(sb.ToString());
            Response.End();
        }
        catch (System.Threading.ThreadAbortException)
        {
            // Response.End() throws this — safe to ignore
        }
        catch (Exception ex)
        {
            Response.Clear();
            Response.ContentType = "text/plain";
            Response.Write("Export failed: " + ex.Message);
            Response.End();
        }
    }

    private static string CsvEscape(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
            return "\"" + val.Replace("\"", "\"\"") + "\"";
        return val;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private string BuildMiniProgress(int completed, int total)
    {
        if (total == 0) return "<span style='color:#bbb;font-size:11px;'>\u2014</span>";
        double pct = Math.Round((double)completed / total * 100, 1);
        string color = pct >= 75 ? "#28a745" : pct >= 40 ? "#f59e0b" : pct > 0 ? "#174DA4" : "#dee2e6";
        return string.Format(
            "<div class='pa-mini-prog'>" +
            "<div class='pa-mini-prog__bar' style='width:{0}%;background:{1}'></div>" +
            "</div>" +
            "<span class='pa-mini-prog__text'>{0}%</span>",
            pct, color);
    }

    private string FormatScore(object val)
    {
        if (val == null || val == DBNull.Value) return "\u2014";
        decimal d;
        if (decimal.TryParse(val.ToString(), out d))
            return d.ToString("F1") + "%";
        return "\u2014";
    }

    private string FormatScoreShort(object val)
    {
        if (val == null || val == DBNull.Value) return "\u2014";
        decimal d;
        if (decimal.TryParse(val.ToString(), out d))
            return d.ToString("F1");
        return "\u2014";
    }

    private string GetScoreStyle(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        decimal d;
        if (!decimal.TryParse(val.ToString(), out d)) return "";
        if (d >= 90) return "color:#28a745;";
        if (d >= 75) return "color:#17a2b8;";
        if (d >= 60) return "color:#174DA4;";
        if (d >= 50) return "color:#f59e0b;";
        return "color:#dc3545;";
    }

    /// <summary>
    /// The one classification scale (mirrors SQL appraisal_classify): 90 Exceptional,
    /// 75 Above Expectations, 60 Satisfactory, 50 Development Needed, else Unsatisfactory.
    /// Computed from the percentage so older records stored under retired labels read the same.
    /// </summary>
    private static string Classify(object pct, string fallback)
    {
        if (pct == null || pct == DBNull.Value) return fallback ?? "";
        decimal d;
        if (!decimal.TryParse(pct.ToString(), out d)) return fallback ?? "";
        if (d >= 90) return "Exceptional";
        if (d >= 75) return "Above Expectations";
        if (d >= 60) return "Satisfactory";
        if (d >= 50) return "Development Needed";
        return "Unsatisfactory";
    }

    /// <summary>Forms ticket OR Session["username"] (see eadmin anonymous ?action= fix).</summary>
    private bool IsCallerAuthenticated()
    {
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(User.Identity.Name)) return true;
        }
        catch { }
        try
        {
            if (Session != null)
            {
                object u = Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    private string FormatCategory(string cat)
    {
        if (string.IsNullOrEmpty(cat)) return "\u2014";
        return cat.Substring(0, 1) + cat.Substring(1).ToLower();
    }

    private string FormatStatusLabel(string status)
    {
        switch (status.ToUpper())
        {
            case "PENDING":                return "Not Started";
            case "EMPLOYEE_IN_PROGRESS":   return "Emp. In Progress";
            case "RETURNED":               return "Returned";
            case "EMPLOYEE_SUBMITTED":     return "Emp. Submitted";
            case "SUPERVISOR_IN_PROGRESS": return "Sup. Reviewing";
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

    private string GetDepartmentSelectExpression(string employeeAlias)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            return BuildDepartmentSqlExpression(conn, employeeAlias);
        }
    }

    private string BuildDepartmentSqlExpression(MySqlConnection conn, string employeeAlias)
    {
        if (ColumnExists(conn, "hrm_employee", "department"))
        {
            return string.Format("IFNULL(NULLIF(TRIM({0}.department),''), 'Unassigned')", employeeAlias);
        }

        if (TableExists(conn, "hrm_emp_contracts") && TableExists(conn, "hrm_departments") && ColumnExists(conn, "hrm_emp_contracts", "departmentID"))
        {
            string deptColumn = ColumnExists(conn, "hrm_departments", "dept_name")
                ? "dept_name"
                : (ColumnExists(conn, "hrm_departments", "department") ? "department" : "");

            string sortColumn = ColumnExists(conn, "hrm_emp_contracts", "contractStart")
                ? "contractStart"
                : (ColumnExists(conn, "hrm_emp_contracts", "created_at") ? "created_at" : "empID");

            if (!string.IsNullOrEmpty(deptColumn))
            {
                return string.Format(
                    "IFNULL(NULLIF(TRIM((SELECT d.{0} FROM hrm_emp_contracts c LEFT JOIN hrm_departments d ON c.departmentID = d.ID WHERE c.empID = {1}.empID ORDER BY (CASE WHEN IFNULL(c.contractStatus,'')='VALID' THEN 0 ELSE 1 END), c.{2} DESC LIMIT 1)),''), 'Unassigned')",
                    deptColumn,
                    employeeAlias,
                    sortColumn);
            }
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
}
