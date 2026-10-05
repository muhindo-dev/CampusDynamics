using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_AppraisalSessionReport : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private int QsSid
    {
        get
        {
            int v;
            return int.TryParse(Request.QueryString["sid"] ?? "0", out v) && v > 0 ? v : 0;
        }
    }

    // ─── Per-category aggregate ────────────────────────────────────────────
    private class CatStats
    {
        public int     Total, Completed, Pending, InProgress, Cancelled, ScoreCount;
        public decimal SumScore;
        public decimal? HighScore, LowScore;
        public int     Exceptional, AboveExp, Satisfactory, DevNeeded, Unsatisfactory, NotScored;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        // Standalone page (no SidebarMaster), so nothing else checks the login:
        // without this the whole staff roster with scores was readable anonymously.
        if (!IsCallerAuthenticated())
        {
            Response.Redirect("~/Default.aspx?ReturnUrl=" + HttpUtility.UrlEncode(Request.RawUrl), true);
            return;
        }
        if (QsSid <= 0) { ShowError("Invalid or missing session ID."); return; }
        if (!IsPostBack)
        {
            try
            {
                DataRow sessRow = LoadSession(QsSid);
                if (sessRow == null) { ShowError("Appraisal session not found."); return; }

                DataTable dtRecs = LoadRecords(QsSid);
                litTitle.Text   = HttpUtility.HtmlEncode(SafeStr(sessRow["session_title"])) + " — Session Report";
                litContent.Text = BuildReport(sessRow, dtRecs);
            }
            catch (Exception ex)
            {
                ShowError("Error loading report: " + HttpUtility.HtmlEncode(ex.Message));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DATA LOADING
    // ═══════════════════════════════════════════════════════════════════════
    private DataRow LoadSession(int sid)
    {
        DataTable dt = ExQ(
            @"SELECT s.*,
                     DATE_FORMAT(s.period_start,'%d %b %Y') AS ps_fmt,
                     DATE_FORMAT(s.period_end,'%d %b %Y')   AS pe_fmt,
                     DATE_FORMAT(s.deadline,'%d %b %Y')     AS dl_fmt,
                     DATE_FORMAT(s.created_at,'%d %b %Y')   AS created_fmt,
                     IFNULL(e.emp_name,'System')             AS created_by_name
              FROM appraisal_sessions s
              LEFT JOIN hrm_employee e ON e.empID = s.created_by
              WHERE s.session_id = @sid",
            P("@sid", sid));

        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    private DataTable LoadRecords(int sid)
    {
        const string order =
            @"ORDER BY ar.staff_category,
                       FIELD(ar.status,'HR_REVIEWED','COMPLETED','EMPLOYEE_SUBMITTED',
                             'SUPERVISOR_IN_PROGRESS','EMPLOYEE_IN_PROGRESS','RETURNED','PENDING','CANCELLED'),
                       ar.final_percentage DESC,
                       e.emp_name";

        try
        {
            return ExQ(string.Format(
                @"SELECT ar.*,
                         e.emp_name, e.EMP_CODE,
                         IFNULL(e.emp_email,'')       AS emp_email,
                         IFNULL(e.emp_phone,'')       AS emp_phone,
                         IFNULL(d.dept_name,'')       AS department,
                         IFNULL(j.jobname,'')         AS designation,
                         IFNULL(rev.emp_name,'Not Assigned') AS live_reviewer_name
                  FROM appraisal_records ar
                  INNER JOIN hrm_employee e ON e.empID = ar.employee_id
                  LEFT JOIN hrm_emp_contracts c ON c.ID = (
                      SELECT c2.ID FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID
                      ORDER BY (CASE WHEN c2.contractStatus='VALID' THEN 0 ELSE 1 END),
                               c2.contractStart DESC LIMIT 1)
                  LEFT JOIN hrm_departments d  ON d.ID = c.departmentID
                  LEFT JOIN hrm_jobs j         ON j.ID = c.jobID
                  LEFT JOIN hrm_employee rev   ON rev.empID = ar.reviewer_id
                  WHERE ar.session_id = @sid {0}", order),
                P("@sid", sid));
        }
        catch
        {
            // Fallback without contract tables
            return ExQ(string.Format(
                @"SELECT ar.*,
                         e.emp_name, e.EMP_CODE,
                         IFNULL(e.emp_email,'') AS emp_email,
                         IFNULL(e.emp_phone,'') AS emp_phone,
                         '' AS department,
                         IFNULL(e.EmpType,'') AS designation,
                         IFNULL(rev.emp_name,'Not Assigned') AS live_reviewer_name
                  FROM appraisal_records ar
                  INNER JOIN hrm_employee e  ON e.empID = ar.employee_id
                  LEFT JOIN  hrm_employee rev ON rev.empID = ar.reviewer_id
                  WHERE ar.session_id = @sid {0}", order),
                P("@sid", sid));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  REPORT BUILDER
    // ═══════════════════════════════════════════════════════════════════════
    private string BuildReport(DataRow sess, DataTable dtRecs)
    {
        // ── Aggregate in one pass ─────────────────────────────────────────
        int total = dtRecs.Rows.Count;
        int completed = 0, pending = 0, inProgress = 0, cancelled = 0;
        int exceptional = 0, aboveExp = 0, satisfactory = 0, devNeeded = 0, unsatisfactory = 0, notScored = 0;
        int scoreCount = 0;
        decimal sumScore = 0m;
        decimal? highScore = null, lowScore = null;
        var catMap = new Dictionary<string, CatStats>(StringComparer.OrdinalIgnoreCase);

        foreach (DataRow r in dtRecs.Rows)
        {
            string st  = SafeStr(r["status"]).ToUpper();
            string cat = SafeStr(r["staff_category"]).ToUpper();
            if (string.IsNullOrEmpty(cat)) cat = "OTHER";

            if (!catMap.ContainsKey(cat)) catMap[cat] = new CatStats();
            CatStats cs = catMap[cat];
            cs.Total++;

            bool isDone      = (st == "COMPLETED" || st == "HR_REVIEWED");
            bool isCancelled = (st == "CANCELLED");
            bool isPending   = (st == "PENDING");

            if      (isDone)      { completed++;   cs.Completed++; }
            else if (isCancelled) { cancelled++;   cs.Cancelled++; }
            else if (isPending)   { pending++;     cs.Pending++; }
            else                  { inProgress++;  cs.InProgress++; }

            if (isDone && r["final_percentage"] != DBNull.Value)
            {
                decimal pct = Convert.ToDecimal(r["final_percentage"]);
                sumScore  += pct;  scoreCount++;
                cs.SumScore += pct; cs.ScoreCount++;

                if (!highScore.HasValue || pct > highScore.Value) highScore = pct;
                if (!lowScore.HasValue  || pct < lowScore.Value)  lowScore  = pct;
                if (!cs.HighScore.HasValue || pct > cs.HighScore.Value) cs.HighScore = pct;
                if (!cs.LowScore.HasValue  || pct < cs.LowScore.Value)  cs.LowScore  = pct;

                // One scale (SQL appraisal_classify): 90 / 75 / 60 / 50
                switch (Classify(pct))
                {
                    case "Exceptional":        exceptional++;    cs.Exceptional++;    break;
                    case "Above Expectations": aboveExp++;       cs.AboveExp++;       break;
                    case "Satisfactory":       satisfactory++;   cs.Satisfactory++;   break;
                    case "Development Needed": devNeeded++;      cs.DevNeeded++;      break;
                    default:                   unsatisfactory++; cs.Unsatisfactory++; break;
                }
            }
            else if (isDone)
            {
                notScored++; cs.NotScored++;
            }
        }

        decimal avgScore = scoreCount > 0 ? Math.Round(sumScore / scoreCount, 1) : 0m;
        double  compRate = total > 0 ? Math.Round((double)completed / total * 100.0, 1) : 0;
        int     outstand = total - completed - cancelled;

        string sessTitle = SafeStr(sess["session_title"]);
        string psFmt     = SafeStr(sess["ps_fmt"]);
        string peFmt     = SafeStr(sess["pe_fmt"]);
        string dlFmt     = SafeStr(sess["dl_fmt"]);
        string createdBy = SafeStr(sess["created_by_name"]);
        string createdAt = SafeStr(sess["created_fmt"]);
        string status    = SafeStr(sess["status"]).ToUpper();
        string sessCats  = SafeStr(sess["target_categories"]).Replace(",", ", ");
        string sessDesc  = SafeStr(sess["session_description"]);

        string generated = DateTime.Now.ToString("dd MMM yyyy HH:mm");

        var html = new StringBuilder();

        // ── University Header ─────────────────────────────────────────────
        html.Append("<div class='uni-header'>");
        html.AppendFormat(
            "<img class='uni-header__logo' src='{0}' alt='MRU' onerror=\"this.style.display='none'\" />",
            HttpUtility.HtmlEncode(ResolveUrl("~/COOPERP/images/welcomelogo.png")));
        html.Append("<div class='uni-header__text'>");
        html.Append("<div class='uni-header__name'>Muteesa I Royal University</div>");
        html.Append("<div class='uni-header__motto'>Knowledge for Service</div>");
        html.Append("<div class='uni-header__sub'>Staff Performance Appraisal System</div>");
        html.Append("</div>");
        html.AppendFormat(
            "<div class='uni-header__ref'>Session Report<br/>Generated:<br/>{0}</div>",
            DateTime.Now.ToString("dd MMM yyyy"));
        html.Append("</div>");
        html.Append("<div class='uni-address-bar'>P.O. Box 1 Masaka / Kampala &bull; www.mru.ac.ug &bull; Muteesa I Royal University</div>");

        // ── Report Title Block ────────────────────────────────────────────
        html.Append("<div class='rep-title-block'>");
        html.Append("<div class='rep-title'>Appraisal Session Report</div>");
        html.AppendFormat("<div class='rep-subtitle'>{0}</div>", Enc(sessTitle));
        html.AppendFormat(
            "<div class='rep-meta'>Period: {0} &ndash; {1} &nbsp;&bull;&nbsp; Deadline: {2} &nbsp;&bull;&nbsp; Generated: {3}</div>",
            Enc(psFmt), Enc(peFmt), Enc(dlFmt), generated);
        html.Append("</div>");

        // ── SECTION 1: Executive Summary ──────────────────────────────────
        html.Append("<div class='rep-sec-header'>1. Executive Summary</div>");
        html.Append("<div class='stat-grid avoid-break'>");
        html.Append(StatCard(total.ToString("N0"),
            "Total Appraisals", "#174DA4"));
        html.Append(StatCard(completed.ToString("N0") + " <small style='font-size:10pt'>(" + compRate.ToString("F0") + "%)</small>",
            "Completed", "#16a34a"));
        html.Append(StatCard(outstand.ToString("N0"),
            "Outstanding", outstand > 0 ? "#d97706" : "#9ca3af"));
        html.Append(StatCard(scoreCount > 0 ? avgScore.ToString("F1") + "%" : "N/A",
            "Average Score", "#2563eb"));
        html.Append("</div>");

        // ── SECTION 2: Session Details ────────────────────────────────────
        html.Append("<div class='rep-sec-header'>2. Session Details</div>");
        html.Append("<table class='bio-grid avoid-break'>");
        html.Append(BioRow("Session Title", Enc(sessTitle), "Status", SessionStatusBadge(status)));
        html.Append(BioRow("Period", Enc(psFmt) + " &ndash; " + Enc(peFmt), "Deadline", Enc(dlFmt)));
        html.Append(BioRow("Target Categories", Enc(sessCats), "Created By", Enc(createdBy) + " on " + Enc(createdAt)));
        if (!string.IsNullOrWhiteSpace(sessDesc))
            html.AppendFormat("<tr><td class='label'>Description</td><td colspan='3'>{0}</td></tr>", Enc(sessDesc));
        html.Append("</table>");

        // ── SECTION 3: Completion Overview ───────────────────────────────
        html.Append("<div class='rep-sec-header'>3. Completion Overview</div>");
        html.Append("<div class='avoid-break'>");

        if (total > 0)
        {
            double doneW   = (double)completed  / total * 100;
            double progW   = (double)inProgress / total * 100;
            double pendW   = (double)pending    / total * 100;
            double cancelW = (double)cancelled  / total * 100;

            html.Append("<div class='stacked-bar-wrap'>");
            html.Append("<div class='stacked-bar'>");
            if (doneW   > 0) html.AppendFormat("<div class='seg' style='width:{0:F1}%;background:#16a34a' title='Completed: {1}'></div>", doneW,   completed);
            if (progW   > 0) html.AppendFormat("<div class='seg' style='width:{0:F1}%;background:#2563eb' title='In Progress: {1}'></div>", progW,  inProgress);
            if (pendW   > 0) html.AppendFormat("<div class='seg' style='width:{0:F1}%;background:#d1d5db' title='Pending: {1}'></div>", pendW,      pending);
            if (cancelW > 0) html.AppendFormat("<div class='seg' style='width:{0:F1}%;background:#fca5a5' title='Cancelled: {1}'></div>", cancelW,  cancelled);
            html.Append("</div>");
            html.Append("</div>");
        }

        html.Append("<div class='bar-chart'>");
        html.Append(BarRow("Completed",    completed,   total, "#16a34a"));
        html.Append(BarRow("In Progress",  inProgress,  total, "#2563eb"));
        html.Append(BarRow("Pending",      pending,     total, "#9ca3af"));
        html.Append(BarRow("Cancelled",    cancelled,   total, "#f87171"));
        html.Append("</div>");
        html.Append("</div>");

        // ── SECTION 4: Performance Distribution ──────────────────────────
        string scoredNote = scoreCount > 0
            ? scoreCount + " scored appraisal" + (scoreCount == 1 ? "" : "s")
            : "No scored appraisals yet";
        html.AppendFormat(
            "<div class='rep-sec-header'>4. Performance Distribution" +
            "<span class='rep-sec-note'>{0}</span></div>", scoredNote);
        html.Append("<div class='avoid-break'>");
        html.Append("<div class='class-chart'>");
        html.Append(ClassRow("Exceptional",        "&#8805;90%",   exceptional,    scoreCount, "#16a34a"));
        html.Append(ClassRow("Above Expectations", "75&ndash;89%", aboveExp,       scoreCount, "#2563eb"));
        html.Append(ClassRow("Satisfactory",       "60&ndash;74%", satisfactory,   scoreCount, "#0891b2"));
        html.Append(ClassRow("Development Needed", "50&ndash;59%", devNeeded,      scoreCount, "#d97706"));
        html.Append(ClassRow("Unsatisfactory",     "&lt;50%",      unsatisfactory, scoreCount, "#dc2626"));
        html.Append("</div>");

        if (scoreCount > 0)
        {
            html.Append("<div class='score-stats-row'>");
            html.Append(ScoreStat("Average Score",    avgScore.ToString("F1") + "%"));
            html.Append(ScoreStat("Highest Score",    highScore.HasValue ? highScore.Value.ToString("F1") + "%" : "N/A"));
            html.Append(ScoreStat("Lowest Score",     lowScore.HasValue  ? lowScore.Value.ToString("F1")  + "%" : "N/A"));
            html.Append(ScoreStat("Not Yet Scored",   notScored.ToString("N0")));
            html.Append("</div>");
        }
        html.Append("</div>");

        // ── SECTION 5: Category Breakdown ─────────────────────────────────
        html.Append("<div class='rep-sec-header'>5. Category Breakdown</div>");
        html.Append("<table class='print-table avoid-break'>");
        html.Append("<thead><tr>");
        html.Append("<th>Category</th><th>Total</th><th>Completed</th><th>Outstanding</th>");
        html.Append("<th>Rate</th><th>Avg Score</th><th>High</th><th>Low</th>");
        html.Append("</tr></thead><tbody>");

        string[] catOrder = { "ACADEMIC", "ADMINISTRATIVE", "SUPPORT" };
        foreach (string cat in catOrder)
        {
            if (!catMap.ContainsKey(cat)) continue;
            AppendCatRow(html, cat, catMap[cat]);
        }
        foreach (var kvp in catMap)
        {
            if (Array.IndexOf(catOrder, kvp.Key) >= 0) continue;
            AppendCatRow(html, kvp.Key, kvp.Value);
        }
        html.Append("</tbody></table>");

        // ── SECTION 6: Employee Roster ────────────────────────────────────
        html.Append("<div class='rep-sec-header avoid-break'>6. Complete Employee Roster</div>");
        html.Append("<table class='print-table roster-table'>");
        html.Append("<thead><tr>");
        html.Append("<th style='width:18pt'>#</th>");
        html.Append("<th>Employee</th>");
        html.Append("<th>Department</th>");
        html.Append("<th>Designation</th>");
        html.Append("<th>Supervisor</th>");
        html.Append("<th>Status</th>");
        html.Append("<th style='text-align:right;width:48pt'>Score</th>");
        html.Append("<th style='width:70pt'>Classification</th>");
        html.Append("</tr></thead><tbody>");

        string lastCat2 = null;
        int rowNum = 0;
        foreach (DataRow r in dtRecs.Rows)
        {
            string cat = SafeStr(r["staff_category"]).ToUpper();
            if (cat != lastCat2)
            {
                string catLabel = string.IsNullOrEmpty(cat) ? "OTHER" : cat;
                html.AppendFormat(
                    "<tr class='roster-cat-header'><td colspan='8'>{0} STAFF</td></tr>",
                    HttpUtility.HtmlEncode(catLabel));
                lastCat2 = cat;
                rowNum = 0;
            }
            rowNum++;

            string st    = SafeStr(r["status"]).ToUpper();
            bool   isDone = (st == "COMPLETED" || st == "HR_REVIEWED");
            bool   hasScore = isDone && r["final_percentage"] != DBNull.Value;

            string scoreStr = hasScore
                ? Convert.ToDecimal(r["final_percentage"]).ToString("F1") + "%"
                : "&mdash;";
            string classStr = hasScore
                ? Classify(Convert.ToDecimal(r["final_percentage"]))
                : (isDone && r["classification"] != DBNull.Value ? SafeStr(r["classification"]) : "");
            string scoreColor = hasScore ? ClassColor(Convert.ToDecimal(r["final_percentage"])) : "#9ca3af";
            string classBg    = hasScore ? ClassColor(Convert.ToDecimal(r["final_percentage"])) + "22" : "#f3f4f6";
            string classColor2= hasScore ? ClassColor(Convert.ToDecimal(r["final_percentage"])) : "#9ca3af";

            html.AppendFormat("<tr><td style='text-align:center;color:#888;font-size:7.5pt'>{0}</td>", rowNum);
            html.AppendFormat(
                "<td><strong>{0}</strong><br/><span style='font-size:7pt;color:#888'>{1}</span></td>",
                Enc(SafeStr(r["emp_name"])), Enc(SafeStr(r["EMP_CODE"])));
            html.AppendFormat("<td>{0}</td>", Enc(SafeStr(r["department"])));
            html.AppendFormat("<td>{0}</td>", Enc(SafeStr(r["designation"])));
            html.AppendFormat("<td>{0}</td>", Enc(SafeStr(r["live_reviewer_name"])));
            html.AppendFormat(
                "<td><span class='st-badge st-{0}'>{1}</span></td>",
                StatusBadgeMod(st), FmtStatus(st));
            html.AppendFormat(
                "<td style='text-align:right;font-weight:{0};color:{1}'>{2}</td>",
                isDone ? "700" : "400", scoreColor, scoreStr);
            html.AppendFormat(
                "<td><span class='cls-badge' style='background:{0};color:{1}'>{2}</span></td>",
                classBg, classColor2,
                string.IsNullOrEmpty(classStr) ? "&mdash;" : Enc(classStr));
            html.Append("</tr>");
        }

        if (dtRecs.Rows.Count == 0)
            html.Append("<tr><td colspan='8' style='text-align:center;color:#999;padding:16pt'>No appraisal records found for this session.</td></tr>");

        html.Append("</tbody></table>");

        // ── SECTION 7: Outstanding Items ──────────────────────────────────
        bool hasOutstanding = false;
        foreach (DataRow r in dtRecs.Rows)
        {
            string st = SafeStr(r["status"]).ToUpper();
            if (st != "COMPLETED" && st != "HR_REVIEWED" && st != "CANCELLED")
            { hasOutstanding = true; break; }
        }

        int nextSec = 7;
        if (hasOutstanding)
        {
            html.Append("<div class='page-break'></div>");
            html.AppendFormat("<div class='rep-sec-header'>{0}. Outstanding / Pending Actions</div>", nextSec++);
            html.Append("<table class='print-table'>");
            html.Append("<thead><tr><th>#</th><th>Employee</th><th>Category</th><th>Current Status</th><th>Required Next Action</th></tr></thead><tbody>");

            int outNum = 0;
            foreach (DataRow r in dtRecs.Rows)
            {
                string st = SafeStr(r["status"]).ToUpper();
                if (st == "COMPLETED" || st == "HR_REVIEWED" || st == "CANCELLED") continue;
                outNum++;
                html.AppendFormat("<tr><td style='text-align:center;color:#888'>{0}</td>", outNum);
                html.AppendFormat("<td><strong>{0}</strong></td>", Enc(SafeStr(r["emp_name"])));
                html.AppendFormat("<td>{0}</td>", Enc(SafeStr(r["staff_category"])));
                html.AppendFormat(
                    "<td><span class='st-badge st-{0}'>{1}</span></td>",
                    StatusBadgeMod(st), FmtStatus(st));
                html.AppendFormat("<td>{0}</td>", Enc(NextAction(st)));
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");
        }

        // ── Sign-off ──────────────────────────────────────────────────────
        html.AppendFormat("<div class='rep-sec-header'>{0}. Sign-off &amp; Certification</div>", nextSec);
        html.Append("<div class='sig-grid avoid-break'>");
        html.Append(SigBlock("Head of Human Resources",    "Prepared and authorised by HR", DateTime.Now.ToString("dd MMM yyyy")));
        html.Append(SigBlock("Deputy Vice Chancellor (A)", "Reviewed and noted", ""));
        html.Append(SigBlock("Vice Chancellor",            "Approved", ""));
        html.Append("</div>");

        // ── Footer ────────────────────────────────────────────────────────
        html.AppendFormat(
            "<div class='rep-footer'>Muteesa I Royal University &bull; Staff Appraisal System &bull; Confidential &bull; " +
            "Report generated {0} &bull; Session ID: {1}</div>",
            generated, QsSid);

        return html.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HELPERS — category table row
    // ═══════════════════════════════════════════════════════════════════════
    private void AppendCatRow(StringBuilder html, string cat, CatStats cs)
    {
        double cRate = cs.Total > 0 ? Math.Round((double)cs.Completed / cs.Total * 100.0, 1) : 0;
        decimal cAvg = cs.ScoreCount > 0 ? Math.Round(cs.SumScore / cs.ScoreCount, 1) : 0m;
        string catLabel = cat.Length > 0
            ? cat.Substring(0, 1) + cat.Substring(1).ToLower()
            : cat;
        html.Append("<tr>");
        html.AppendFormat("<td><strong>{0}</strong></td>", Enc(catLabel));
        html.AppendFormat("<td>{0}</td>", cs.Total);
        html.AppendFormat("<td>{0}</td>", cs.Completed);
        html.AppendFormat("<td>{0}</td>", cs.Total - cs.Completed - cs.Cancelled);
        html.AppendFormat("<td>{0:F1}%</td>", cRate);
        html.AppendFormat("<td>{0}</td>", cs.ScoreCount > 0 ? cAvg.ToString("F1") + "%" : "&mdash;");
        html.AppendFormat("<td>{0}</td>", cs.HighScore.HasValue ? cs.HighScore.Value.ToString("F1") + "%" : "&mdash;");
        html.AppendFormat("<td>{0}</td>", cs.LowScore.HasValue  ? cs.LowScore.Value.ToString("F1")  + "%" : "&mdash;");
        html.Append("</tr>");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COMPONENT RENDERERS
    // ═══════════════════════════════════════════════════════════════════════
    private string StatCard(string val, string label, string accentColor)
    {
        return string.Format(
            "<div class='stat-card' style='border-top-color:{2}'>" +
            "<div class='stat-val' style='color:{2}'>{0}</div>" +
            "<div class='stat-label'>{1}</div></div>",
            val, label, accentColor);
    }

    private string BioRow(string lbl1, string val1, string lbl2, string val2)
    {
        return string.Format(
            "<tr><td class='label'>{0}</td><td>{1}</td><td class='label'>{2}</td><td>{3}</td></tr>",
            lbl1, val1, lbl2, val2);
    }

    private string BarRow(string label, int count, int total, string color)
    {
        double pct = total > 0 ? Math.Round((double)count / total * 100.0, 1) : 0;
        return string.Format(
            "<div class='bar-row'>" +
            "<div class='bar-label'>{0}</div>" +
            "<div class='bar-track'><div class='bar-fill' style='width:{1:F1}%;background:{2}'></div></div>" +
            "<div class='bar-count'>{3} <span class='bar-pct'>({4:F1}%)</span></div>" +
            "</div>",
            label, pct, color, count, pct);
    }

    private string ClassRow(string label, string range, int count, int scoreCount, string color)
    {
        double pct = scoreCount > 0 ? Math.Round((double)count / scoreCount * 100.0, 1) : 0;
        return string.Format(
            "<div class='class-row'>" +
            "<div class='class-label'><span class='class-dot' style='background:{0}'></span>{1}</div>" +
            "<div class='class-range'>{2}</div>" +
            "<div class='class-track'><div class='class-fill' style='width:{3:F1}%;background:{0}'></div></div>" +
            "<div class='class-count'>{4} <span class='bar-pct'>({5:F1}%)</span></div>" +
            "</div>",
            color, label, range, pct, count, pct);
    }

    private string ScoreStat(string label, string val)
    {
        return string.Format(
            "<div class='score-stat'><span class='ss-label'>{0}</span><span class='ss-val'>{1}</span></div>",
            label, val);
    }

    private string SigBlock(string name, string title, string dateStr)
    {
        return string.Format(
            "<div class='sig-item'>" +
            "<div class='sig-name'>{0}</div>" +
            "<div class='sig-title'>{1}</div>" +
            "<div class='sig-line'></div>" +
            "<div class='sig-date'>{2}</div>" +
            "</div>",
            Enc(name), Enc(title),
            string.IsNullOrEmpty(dateStr) ? "Date: ________________" : "Date: " + Enc(dateStr));
    }

    private string SessionStatusBadge(string status)
    {
        string color, bg;
        if      (status == "ACTIVE")    { color = "#065f46"; bg = "#d1fae5"; }
        else if (status == "DRAFT")     { color = "#92400e"; bg = "#fef3c7"; }
        else if (status == "CLOSED")    { color = "#1e40af"; bg = "#dbeafe"; }
        else if (status == "ARCHIVED")  { color = "#374151"; bg = "#e5e7eb"; }
        else                            { color = "#6b7280"; bg = "#f3f4f6"; }
        return string.Format(
            "<span style='display:inline-block;padding:2pt 7pt;font-size:8pt;font-weight:700;background:{0};color:{1}'>{2}</span>",
            bg, color, Enc(status));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UTILITY
    // ═══════════════════════════════════════════════════════════════════════
    private string ClassColor(decimal pct)
    {
        if (pct >= 90) return "#16a34a";
        if (pct >= 75) return "#2563eb";
        if (pct >= 60) return "#0891b2";
        if (pct >= 50) return "#d97706";
        return "#dc2626";
    }

    /// <summary>The one classification scale - mirrors SQL appraisal_classify().</summary>
    private static string Classify(decimal pct)
    {
        if (pct >= 90) return "Exceptional";
        if (pct >= 75) return "Above Expectations";
        if (pct >= 60) return "Satisfactory";
        if (pct >= 50) return "Development Needed";
        return "Unsatisfactory";
    }

    /// <summary>Forms ticket OR Session["username"] (same rule as the other eadmin screens).</summary>
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

    private string StatusBadgeMod(string st)
    {
        if (st == "COMPLETED"   || st == "HR_REVIEWED")            return "done";
        if (st == "EMPLOYEE_SUBMITTED" || st == "SUPERVISOR_IN_PROGRESS") return "sub";
        if (st == "EMPLOYEE_IN_PROGRESS" || st == "RETURNED")       return "prog";
        if (st == "CANCELLED")                                       return "can";
        return "pend";
    }

    private string FmtStatus(string st)
    {
        switch (st)
        {
            case "PENDING":                 return "Pending";
            case "EMPLOYEE_IN_PROGRESS":    return "In Progress";
            case "RETURNED":                return "Returned";
            case "EMPLOYEE_SUBMITTED":      return "Emp. Submitted";
            case "SUPERVISOR_IN_PROGRESS":  return "Supervisor Review";
            case "COMPLETED":               return "Completed";
            case "HR_REVIEWED":             return "HR Reviewed";
            case "CANCELLED":               return "Cancelled";
            default:                        return st;
        }
    }

    private string NextAction(string st)
    {
        switch (st)
        {
            case "PENDING":                return "Employee must begin appraisal";
            case "EMPLOYEE_IN_PROGRESS":   return "Employee must complete and submit";
            case "RETURNED":               return "Employee must address comments and resubmit";
            case "EMPLOYEE_SUBMITTED":     return "Supervisor must begin review";
            case "SUPERVISOR_IN_PROGRESS": return "Supervisor must complete and submit";
            default:                       return "&mdash;";
        }
    }

    private string Enc(string s)    { return HttpUtility.HtmlEncode(s ?? ""); }
    private string SafeStr(object o) { return (o == null || o == DBNull.Value) ? "" : o.ToString().Trim(); }

    private MySqlParameter P(string name, object val) { return new MySqlParameter(name, val); }

    private DataTable ExQ(string sql, params MySqlParameter[] parms)
    {
        using (var conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql, conn))
            {
                foreach (var p in parms) cmd.Parameters.Add(p);
                var dt = new DataTable();
                using (var da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
                return dt;
            }
        }
    }

    private void ShowError(string msg)
    {
        litContent.Text = string.Format(
            "<div style='padding:40pt;text-align:center;'>" +
            "<div style='font-size:16pt;color:#dc2626;font-weight:bold;margin-bottom:10pt;'>" +
            "<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' style='vertical-align:-3px;margin-right:6px'>" +
            "<path d='M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z'/><line x1='12' y1='9' x2='12' y2='13'/><line x1='12' y1='17' x2='12.01' y2='17'/></svg>Error</div>" +
            "<div style='font-size:10pt;color:#555;'>{0}</div>" +
            "</div>", msg);
    }
}
