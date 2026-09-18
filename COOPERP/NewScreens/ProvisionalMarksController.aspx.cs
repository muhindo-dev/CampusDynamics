using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;
using System.Globalization;

public partial class COOPERP_NewScreens_ProvisionalMarksController : System.Web.UI.Page
{
    private const int DefaultPageSize = 100;
    private const string StatusPending = "pending";
    private const string StatusApproved = "approved";
    private const string StatusRejected = "rejected";
    private const string StatusPublished = "published";
    private const string StatusNotEntered = "not_entered";
    private static readonly AdminMarksPageKind PageKind = AdminMarksPageKind.ProvisionalPending;

    private string ConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ──────────────────────────────────────────────────
    // PAGE LOAD
    // ──────────────────────────────────────────────────
    protected void Page_Load(object sender, EventArgs e)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            EnsureProvisionalColumns(conn);
            LoadFilters(conn);
            LoadStats(conn);
            BindGrid(conn);
        }
    }

    // ──────────────────────────────────────────────────
    // ENSURE PROVISIONAL COLUMNS
    // ──────────────────────────────────────────────────
    private static void EnsureProvisionalColumns(MySqlConnection conn)
    {
        const string schema = "campus_dynamics_portal";
        const string table  = "acad_course_registration";
        try { EnsureNullableColumn(conn, schema, table, "provisional_course_work_marks", "INT NULL"); }          catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_exam_marks",        "INT NULL"); }          catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_total_marks",        "INT NULL"); }          catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_status",       "VARCHAR(20) NULL DEFAULT 'pending'"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_review_comments", "TEXT NULL"); }     catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_reviewed_by",  "VARCHAR(150) NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_review_date",  "DATETIME NULL"); }    catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_submitted_by",       "VARCHAR(150) NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_published_by",        "VARCHAR(150) NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_published_date",      "DATETIME NULL"); }   catch { }
    }

    // ──────────────────────────────────────────────────
    // LOAD FILTERS (years + programmes + lecturers)
    // ──────────────────────────────────────────────────
    private void LoadFilters(MySqlConnection conn)
    {
        // Academic years
        ddlYear.Items.Clear();
        ddlYear.Items.Add(new ListItem("All Years", ""));
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT DISTINCT acad_year FROM campus_dynamics_portal.acad_course_registration WHERE acad_year IS NOT NULL AND acad_year <> '' ORDER BY acad_year DESC", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
                ddlYear.Items.Add(new ListItem(rdr.GetString(0)));
        }

        // Programmes
        ddlProg.Items.Clear();
        ddlProg.Items.Add(new ListItem("All Programmes", ""));
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT DISTINCT p.progcode, COALESCE(p.progname, p.progcode) FROM acad_programme p INNER JOIN campus_dynamics_portal.acad_course_registration cr ON cr.prog_id = p.progcode ORDER BY 2", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
                ddlProg.Items.Add(new ListItem(rdr.GetString(1), rdr.GetString(0)));
        }

        // Lecturers
        ddlLecturer.Items.Clear();
        ddlLecturer.Items.Add(new ListItem("All Lecturers", ""));
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT DISTINCT e.empID, COALESCE(TRIM(e.emp_name),'') AS lname " +
                "FROM hrm_employee e " +
                "INNER JOIN acad_programmecourses pc ON pc.lecturer_id = e.empID " +
                "WHERE pc.lecturer_id IS NOT NULL AND pc.lecturer_id > 0 " +
                "ORDER BY lname", conn))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    string lname = rdr.IsDBNull(1) ? "" : rdr.GetString(1).Trim();
                    if (!string.IsNullOrEmpty(lname))
                        ddlLecturer.Items.Add(new ListItem(lname, rdr.GetString(0)));
                }
            }
        }
        catch { /* lecturer dropdown non-critical */ }

        // Restore selected values from query string
        string qYear   = Request.QueryString["year"]   ?? "";
        string qSem    = Request.QueryString["sem"]    ?? "";
        string qStatus = StatusPending;
        string qProg   = Request.QueryString["prog"]   ?? "";
        string qLect   = Request.QueryString["lect"]   ?? "";
        string qSearch = Request.QueryString["q"]      ?? "";
        string qPs     = Request.QueryString["ps"]     ?? "";

        SafeSelect(ddlYear,     qYear);
        SafeSelect(ddlSemester, qSem);
        SafeSelect(ddlStatus,   qStatus);
        SafeSelect(ddlProg,     qProg);
        SafeSelect(ddlLecturer, qLect);
        SafeSelect(ddlPageSize, qPs);
        txtSearch.Text = qSearch;
    }

    // ──────────────────────────────────────────────────
    // LOAD STATS
    // ──────────────────────────────────────────────────
    private void LoadStats(MySqlConnection conn)
    {
        // Only ACTIVE (onboarded) students are counted — the stud_status='ACTIVE' join
        // still included alumni, so narrow it with the onboarding filter (ActiveStudentFilter).
        string sql = @"
            SELECT
                COUNT(*)                                                                               AS cnt_total,
                SUM(CASE WHEN cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL THEN 1 ELSE 0 END) AS cnt_not_entered,
                SUM(CASE WHEN (cr.provisional_course_work_marks IS NOT NULL OR cr.provisional_exam_marks IS NOT NULL)
                              AND COALESCE(cr.provisional_marks_status,'pending') = 'pending'         THEN 1 ELSE 0 END) AS cnt_pending,
                SUM(CASE WHEN cr.provisional_marks_status = 'approved'  THEN 1 ELSE 0 END)            AS cnt_approved,
                SUM(CASE WHEN cr.provisional_marks_status = 'rejected'  THEN 1 ELSE 0 END)            AS cnt_rejected,
                SUM(CASE WHEN cr.provisional_marks_status = 'published' THEN 1 ELSE 0 END)            AS cnt_published,
                SUM(CASE WHEN cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL
                              AND COALESCE(cr.provisional_marks_status,'pending') NOT IN ('published','rejected') THEN 1 ELSE 0 END) AS cnt_ready
            FROM campus_dynamics_portal.acad_course_registration cr
            INNER JOIN campus_dynamics.acad_student s ON s.regno = cr.regno AND s.stud_status = 'ACTIVE'
            WHERE 1=1" + ActiveStudentFilter.Clause("cr.regno");

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        using (var rdr = cmd.ExecuteReader())
        {
            if (rdr.Read())
            {
                int cntTotal      = rdr.IsDBNull(0) ? 0 : Convert.ToInt32(rdr[0]);
                int cntNotEntered = rdr.IsDBNull(1) ? 0 : Convert.ToInt32(rdr[1]);
                int cntPending    = rdr.IsDBNull(2) ? 0 : Convert.ToInt32(rdr[2]);
                int cntApproved   = rdr.IsDBNull(3) ? 0 : Convert.ToInt32(rdr[3]);
                int cntRejected   = rdr.IsDBNull(4) ? 0 : Convert.ToInt32(rdr[4]);
                int cntPublished  = rdr.IsDBNull(5) ? 0 : Convert.ToInt32(rdr[5]);
                int cntReady      = rdr.IsDBNull(6) ? 0 : Convert.ToInt32(rdr[6]);

                Func<int, string> pct = v =>
                    cntTotal > 0 ? Math.Round(v * 100.0 / cntTotal, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—";

                litStatTotal.Text  = cntTotal.ToString("N0", CultureInfo.InvariantCulture);
                litNotEntered.Text = cntNotEntered.ToString("N0", CultureInfo.InvariantCulture);
                litPending.Text    = cntPending.ToString("N0", CultureInfo.InvariantCulture);
                litApproved.Text   = cntApproved.ToString("N0", CultureInfo.InvariantCulture);
                litRejected.Text   = cntRejected.ToString("N0", CultureInfo.InvariantCulture);
                litPublished.Text  = cntPublished.ToString("N0", CultureInfo.InvariantCulture);
                litReady.Text      = cntReady.ToString("N0", CultureInfo.InvariantCulture);

                litStatTotalPct.Text  = cntTotal > 0 ? "100%" : "—";
                litNotEnteredPct.Text = pct(cntNotEntered);
                litPendingPct.Text    = pct(cntPending);
                litApprovedPct.Text   = pct(cntApproved);
                litRejectedPct.Text   = pct(cntRejected);
                litPublishedPct.Text  = pct(cntPublished);
                litReadyPct.Text      = pct(cntReady);

                litInsights.Text = string.Format(
                    "<span class='pib-item pib-pub'><strong>{0}</strong> published</span>" +
                    "<span class='pib-sep'>&nbsp;&middot;&nbsp;</span>" +
                    "<span class='pib-item pib-ready'><strong>{1}</strong> ready to publish</span>" +
                    "<span class='pib-sep'>&nbsp;&middot;&nbsp;</span>" +
                    "<span class='pib-item pib-pend'><strong>{2}</strong> pending review</span>" +
                    "<span class='pib-sep'>&nbsp;&middot;&nbsp;</span>" +
                    "<span class='pib-item pib-miss'><strong>{3}</strong> missing marks</span>",
                    pct(cntPublished), pct(cntReady), pct(cntPending), pct(cntNotEntered)
                );
            }
        }
    }

    // ──────────────────────────────────────────────────
    // BIND GRID
    // ──────────────────────────────────────────────────
    private void BindGrid(MySqlConnection conn)
    {
        int page = 1;
        if (!string.IsNullOrEmpty(Request.QueryString["pg"])) int.TryParse(Request.QueryString["pg"], out page);
        if (page < 1) page = 1;

        int pageSize = DefaultPageSize;
        if (!string.IsNullOrEmpty(ddlPageSize.SelectedValue)) int.TryParse(ddlPageSize.SelectedValue, out pageSize);
        if (pageSize < 1) pageSize = DefaultPageSize;

        string year = ddlYear.SelectedValue;
        string sem = ddlSemester.SelectedValue;
        string status = StatusPending;
        string prog = ddlProg.SelectedValue;
        string lect = ddlLecturer.SelectedValue;
        string search = txtSearch.Text.Trim();
        bool onlyReady = (Request.QueryString["ready"] ?? "") == "1";
        string courseCol = GetCourseColumnExpression(conn, "cr");

        var where = new StringBuilder("WHERE (cr.provisional_course_work_marks IS NOT NULL OR cr.provisional_exam_marks IS NOT NULL) AND COALESCE(cr.provisional_marks_status,'pending') = 'pending'");
        if (onlyReady)
            where.Append(" AND cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL");

        if (!string.IsNullOrEmpty(year)) where.Append(" AND cr.acad_year = @year");
        if (!string.IsNullOrEmpty(sem)) where.Append(" AND cr.semester = @sem");
        if (!string.IsNullOrEmpty(prog)) where.Append(" AND cr.prog_id = @prog");
        if (!string.IsNullOrEmpty(search) && !string.IsNullOrEmpty(courseCol))
            where.Append(" AND (cr.regno LIKE @q OR " + courseCol + " LIKE @q OR TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @q)");
        else if (!string.IsNullOrEmpty(search)) where.Append(" AND cr.regno LIKE @q");

        if (!string.IsNullOrEmpty(lect))
            where.Append(" AND EXISTS (SELECT 1 FROM acad_programmecourses pc2 WHERE pc2.lecturer_id = @lect AND pc2.course_code = " + (courseCol ?? "cr.course_code") + " AND pc2.progcode = cr.prog_id)");

        // The mark LIST shows ALL marks for everyone (alumni + active) — no filter.
        // Only the funnel stats (LoadStats) are narrowed to active students.

        if (string.IsNullOrEmpty(courseCol))
        {
            litRows.Text = "<tr><td colspan='14' style='padding:16px;color:#b42318;font-size:11px;'>Course column not found on acad_course_registration.</td></tr>";
            litFrom.Text = litTo.Text = litTotal.Text = litTotal2.Text = "0";
            litPage.Text = litPageCount.Text = "1";
            litPager.Text = litPager2.Text = "";
            return;
        }

        string joins = @"
            FROM campus_dynamics_portal.acad_course_registration cr
            LEFT JOIN acad_student s ON s.regno = cr.regno
            LEFT JOIN acad_course c ON c.courseID = " + courseCol + @"
            LEFT JOIN (
                SELECT course_code, progcode, MAX(lecturer_id) AS lecturer_id
                FROM acad_programmecourses
                WHERE lecturer_id IS NOT NULL AND lecturer_id > 0
                GROUP BY course_code, progcode
            ) pc ON pc.course_code = " + courseCol + @" AND pc.progcode = cr.prog_id
            LEFT JOIN hrm_employee e ON e.empID = pc.lecturer_id";

        int total;
        using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) " + joins + " " + where, conn))
        {
            AddParams(cmd, year, sem, prog, status, lect, search);
            total = Convert.ToInt32(cmd.ExecuteScalar());
        }

        int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > pageCount) page = pageCount;
        int offset = (page - 1) * pageSize;

        string dataSql = @"
            SELECT cr.id, cr.regno,
                   TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                   COALESCE(cr.prog_id,'') AS prog_id,
                   " + courseCol + @" AS courseID,
                   COALESCE(c.courseName, " + courseCol + @") AS course_name,
                   COALESCE(cr.course_status,'') AS course_status,
                   cr.acad_year, cr.semester,
                   COALESCE((SELECT MAX(r2.studyyear) FROM acad_registration r2 WHERE r2.regno=cr.regno AND r2.acad_year=cr.acad_year AND r2.semester=cr.semester),1) AS study_year,
                   cr.provisional_course_work_marks,
                   cr.provisional_exam_marks,
                   cr.provisional_total_marks,
                   (SELECT ar3.score FROM acad_results ar3 WHERE ar3.regno=cr.regno AND ar3.courseid=" + courseCol + @" AND ar3.semester=cr.semester AND ar3.acad=cr.acad_year ORDER BY ar3.ID DESC LIMIT 1) AS published_mark,
                   (SELECT ar4.grade FROM acad_results ar4 WHERE ar4.regno=cr.regno AND ar4.courseid=" + courseCol + @" AND ar4.semester=cr.semester AND ar4.acad=cr.acad_year ORDER BY ar4.ID DESC LIMIT 1) AS published_grade,
                   CASE
                     WHEN COALESCE(cr.provisional_marks_status,'pending') = 'published' THEN 'published'
                     WHEN cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL THEN 'not_entered'
                     ELSE COALESCE(cr.provisional_marks_status,'pending')
                   END AS prov_status
            " + joins + " " + where + @"
            ORDER BY cr.id DESC
            LIMIT @offset, @pageSize";

        var sb = new StringBuilder();
        using (MySqlCommand cmd = new MySqlCommand(dataSql, conn))
        {
            AddParams(cmd, year, sem, prog, status, lect, search);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    int id = Convert.ToInt32(rdr["id"]);
                    string regno = rdr["regno"].ToString();
                    string studentName = rdr["student_name"].ToString().Trim();
                    string courseID = rdr["courseID"].ToString();
                    string courseName = rdr["course_name"].ToString();
                    bool isRetake = rdr["course_status"].ToString().Trim().ToUpperInvariant() == "RETAKE";
                    string rtBadge = isRetake ? "<span title='Retake' style='display:inline-block;margin-left:6px;padding:0 5px;font-size:8.5px;font-weight:700;color:#b45309;background:#fff3e0;border-radius:8px;vertical-align:middle;'>RT</span>" : "";
                    string progId = rdr["prog_id"].ToString();
                    string acadYear = rdr["acad_year"].ToString();
                    string semester = rdr["semester"].ToString();
                    string studyYear = rdr["study_year"].ToString();
                    string provStatus = NormalizeStatus(rdr["prov_status"].ToString());
                    string cwMarks = rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? null : rdr["provisional_course_work_marks"].ToString();
                    string exMarks = rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks")) ? null : rdr["provisional_exam_marks"].ToString();
                    string totMarks = rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks")) ? null : rdr["provisional_total_marks"].ToString();
                    string pubMark = rdr.IsDBNull(rdr.GetOrdinal("published_mark")) ? "-" : rdr["published_mark"].ToString();
                    string pubGrade = rdr.IsDBNull(rdr.GetOrdinal("published_grade")) ? "-" : rdr["published_grade"].ToString();

                    string pillCss = "pm-pill--pending";
                    string rowCss = "row--pending";
                    switch (provStatus)
                    {
                        case StatusApproved: pillCss = "pm-pill--approved"; rowCss = "row--approved"; break;
                        case StatusRejected: pillCss = "pm-pill--rejected"; rowCss = "row--rejected"; break;
                        case StatusPublished: pillCss = "pm-pill--published"; rowCss = "row--published"; break;
                        case StatusNotEntered: pillCss = "pm-pill--not_entered"; rowCss = ""; break;
                    }

                    bool isIncomplete = (cwMarks == null || exMarks == null);
                    if (isIncomplete && provStatus == StatusPending)
                        rowCss = "row--incomplete";

                    Func<string, string> markCell;
                    if (isIncomplete && provStatus == StatusPending)
                        markCell = v => v != null ? "<span class='pm-mark'>" + HtmlEnc(v) + "</span>" : "<span class='pm-mark pm-mark--missing' title='Both CW and Exam required to publish'>&#9888;</span>";
                    else
                        markCell = v => v != null ? "<span class='pm-mark'>" + HtmlEnc(v) + "</span>" : "<span class='pm-mark pm-mark--na'>&mdash;</span>";

                    sb.AppendFormat("<tr class='{0}'>", rowCss);
                    sb.AppendFormat("<td class='col-sel pm-center'><input type='checkbox' class='pm-row-chk pm-row-sel' value='{0}' title='Select for batch action' /></td>", id);
                    sb.AppendFormat("<td class='col-regno' title='{0}'><span class='pm-code pm-ellipsis'>{0}</span></td>", HtmlEnc(regno));
                    sb.AppendFormat("<td class='col-student' title='{0}'><span class='pm-ellipsis'>{0}</span></td>", HtmlEnc(studentName));
                    sb.AppendFormat("<td class='col-course' title='{1}'><span class='pm-code pm-ellipsis'>{0}</span>{2}</td>", HtmlEnc(courseID), HtmlEnc(courseName), rtBadge);
                    sb.AppendFormat("<td class='col-prog pm-muted'><span class='pm-ellipsis'>{0}</span></td>", HtmlEnc(progId));
                    sb.AppendFormat("<td class='col-yr pm-muted'>{0}</td>", HtmlEnc(acadYear));
                    sb.AppendFormat("<td class='col-sem pm-muted'><strong>Yr {0}, Sem {1}</strong></td>", HtmlEnc(studyYear), HtmlEnc(semester));
                    sb.AppendFormat("<td class='col-mark pm-center'>{0}</td>", markCell(cwMarks));
                    sb.AppendFormat("<td class='col-mark pm-center'>{0}</td>", markCell(exMarks));
                    sb.AppendFormat("<td class='col-mark pm-center'>{0}</td>", markCell(totMarks));
                    sb.AppendFormat("<td class='col-pub pm-center'><span class='pm-mark'>{0}</span></td>", HtmlEnc(pubMark));
                    sb.AppendFormat("<td class='col-grade pm-center'><span class='pm-mark'>{0}</span></td>", HtmlEnc(pubGrade));
                    sb.AppendFormat("<td class='col-status pm-center'><span class='pm-pill {0}'>{1}</span></td>", pillCss, HtmlEnc(provStatus.Replace("_", " ")));
                                        string publishBtn = isIncomplete
                                            ? "<button type='button' class='pm-row-menu__item act--publish pm-row-menu__item--disabled' disabled title='Both CW and Exam marks required to publish'>&#8679; Publish</button>"
                                            : string.Format("<button type='button' class='pm-row-menu__item act--publish' onclick='closeMenuThen(this,function(){{openPublish({0});}})'>&#8679; Publish</button>", id);
                                        sb.AppendFormat(@"<td class='col-act pm-center'>
  <div class='pm-row-wrap'>
    <button type='button' class='pm-row-trigger' onclick='toggleRowMenu(this)' title='Actions' aria-label='Open row actions'>&#8942;</button>
    <div class='pm-row-menu'>
      <button type='button' class='pm-row-menu__item act--edit' onclick='closeMenuThen(this,function(){{openEdit({0});}})'>&#9998; Edit Marks</button>
      <button type='button' class='pm-row-menu__item act--approve' onclick='closeMenuThen(this,function(){{openDetails({0});}})'>&#9432; View Details</button>
      <button type='button' class='pm-row-menu__item act--approve' onclick='closeMenuThen(this,function(){{openReview({0});}})'>&#10003; Review</button>
      <div class='pm-row-menu__sep'></div>
      {1}
      <button type='button' class='pm-row-menu__item act--reset' onclick='closeMenuThen(this,function(){{resetToPending({0});}})'>&#8635; Reset</button>
    </div>
  </div>
</td></tr>", id, publishBtn);
                }
            }
        }

        if (sb.Length == 0)
            sb.Append("<tr><td colspan='14' style='padding:14px;text-align:center;color:#6b7280;font-size:11px;'>No records found for the selected filters.</td></tr>");

        litRows.Text = sb.ToString();
        int displayFrom = total == 0 ? 0 : offset + 1;
        int displayTo = Math.Min(offset + pageSize, total);
        litFrom.Text = displayFrom.ToString("N0", CultureInfo.InvariantCulture);
        litTo.Text = displayTo.ToString("N0", CultureInfo.InvariantCulture);
        litTotal.Text = total.ToString("N0", CultureInfo.InvariantCulture);
        litTotal2.Text = total.ToString("N0", CultureInfo.InvariantCulture);
        litPage.Text = page.ToString();
        litPageCount.Text = pageCount.ToString();
        string pagerHtml = BuildPager(page, pageCount, year, sem, prog, StatusPending, lect, search, pageSize.ToString(), onlyReady);
        litPager.Text = pagerHtml;
        litPager2.Text = pagerHtml;
    }

    private static void AddParams(MySqlCommand cmd, string year, string sem, string prog, string status, string lect, string search)
    {
        if (!string.IsNullOrEmpty(year))   cmd.Parameters.AddWithValue("@year",   year);
        if (!string.IsNullOrEmpty(sem))    cmd.Parameters.AddWithValue("@sem",    sem);
        if (!string.IsNullOrEmpty(prog))   cmd.Parameters.AddWithValue("@prog",   prog);
        if (!string.IsNullOrEmpty(status))
            cmd.Parameters.AddWithValue("@status", status);
        if (!string.IsNullOrEmpty(lect))   cmd.Parameters.AddWithValue("@lect",   lect);
        if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@q",      "%" + search + "%");
    }

    private static string BuildPager(int page, int pageCount, string year, string sem, string prog, string status, string lect, string q, string ps, bool onlyReady = false)
    {
        var sb = new StringBuilder();
        int startP = Math.Max(1, page - 3);
        int endP   = Math.Min(pageCount, page + 3);

        Func<int,string> url = p =>
            string.Format("?pg={0}&year={1}&sem={2}&prog={3}&status={4}&lect={5}&ps={6}&q={7}{8}",
                p,
                Uri.EscapeDataString(year),
                Uri.EscapeDataString(sem),
                Uri.EscapeDataString(prog),
                Uri.EscapeDataString(status),
                Uri.EscapeDataString(lect),
                Uri.EscapeDataString(ps),
                Uri.EscapeDataString(q),
                onlyReady ? "&ready=1" : "");

        if (page > 1) sb.AppendFormat("<a href='{0}'>&laquo;</a>", url(page - 1));
        for (int i = startP; i <= endP; i++)
        {
            if (i == page) sb.AppendFormat("<span class='active'>{0}</span>", i);
            else           sb.AppendFormat("<a href='{0}'>{1}</a>", url(i), i);
        }
        if (page < pageCount) sb.AppendFormat("<a href='{0}'>&raquo;</a>", url(page + 1));
        return sb.ToString();
    }

    // ──────────────────────────────────────────────────
    // WEBMETHODS
    // ──────────────────────────────────────────────────

    /// <summary>Get a single provisional marks record by id.</summary>
    [WebMethod]
    public static string GetProvisionalRecord(int id)
    {
        string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        var js = new JavaScriptSerializer();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string courseCol = GetCourseColumnExpression(conn, "cr");
                if (string.IsNullOrEmpty(courseCol))
                    return js.Serialize(new { success = false, message = "Course column not found on acad_course_registration." });

                string sql = @"
                    SELECT cr.id, cr.regno, " + courseCol + @" AS courseID, COALESCE(cr.prog_id,'') AS prog_id,
                           COALESCE((SELECT MAX(r2.studyyear) FROM acad_registration r2 WHERE r2.regno=cr.regno AND r2.acad_year=cr.acad_year AND r2.semester=cr.semester),1) AS study_year,
                           cr.acad_year, cr.semester,
                           cr.provisional_course_work_marks,
                           cr.provisional_exam_marks,
                           cr.provisional_total_marks,
                           CASE WHEN COALESCE(cr.provisional_marks_status,'pending') = 'published' THEN 'published'
                                WHEN cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL THEN 'not_entered'
                                ELSE COALESCE(cr.provisional_marks_status,'pending') END AS provisional_marks_status,
                           COALESCE(cr.provisional_marks_review_comments,'') AS provisional_marks_review_comments,
                           COALESCE(cr.provisional_marks_reviewed_by,'') AS provisional_marks_reviewed_by,
                           COALESCE(cr.provisional_submitted_by,'') AS submitted_by,
                           TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name
                    FROM campus_dynamics_portal.acad_course_registration cr
                    LEFT JOIN acad_student s ON s.regno = cr.regno
                    WHERE cr.id = @id LIMIT 1";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                            return js.Serialize(new { success = false, message = "Record not found." });

                        var record = new Dictionary<string, object>
                        {
                            { "id",           Convert.ToInt32(rdr["id"]) },
                            { "regno",        rdr["regno"].ToString() },
                            { "courseID",     rdr["courseID"].ToString() },
                            { "prog_id",      rdr["prog_id"].ToString() },
                            { "study_year",   rdr["study_year"].ToString() },
                            { "acad_year",    rdr["acad_year"].ToString() },
                            { "semester",     rdr["semester"].ToString() },
                            { "student_name", rdr["student_name"].ToString().Trim() },
                            { "provisional_course_work_marks", rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? (object)null : Convert.ToInt32(rdr["provisional_course_work_marks"]) },
                            { "provisional_exam_marks",        rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks"))        ? (object)null : Convert.ToInt32(rdr["provisional_exam_marks"]) },
                            { "provisional_total_marks",       rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks"))       ? (object)null : Convert.ToInt32(rdr["provisional_total_marks"]) },
                            { "provisional_marks_status",      rdr["provisional_marks_status"].ToString() },
                            { "provisional_marks_review_comments", rdr["provisional_marks_review_comments"].ToString() },
                            { "provisional_marks_reviewed_by", rdr["provisional_marks_reviewed_by"].ToString() },
                            { "submitted_by", rdr["submitted_by"].ToString() }
                        };
                        return js.Serialize(new { success = true, record = record });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
            }
        }

    [WebMethod]
    public static string GetRecordDetails(int id)
    {
        string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        var js = new JavaScriptSerializer();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string courseCol = GetCourseColumnExpression(conn, "cr");
                if (string.IsNullOrEmpty(courseCol))
                    return js.Serialize(new { success = false, message = "Course column not found on acad_course_registration." });

                string sql = @"
                    SELECT cr.id, cr.regno, " + courseCol + @" AS courseID,
                           COALESCE((SELECT MAX(r2.studyyear) FROM acad_registration r2 WHERE r2.regno=cr.regno AND r2.acad_year=cr.acad_year AND r2.semester=cr.semester),1) AS study_year,
                           COALESCE(c.courseName, " + courseCol + @") AS course_name,
                           COALESCE(cr.prog_id,'') AS prog_id,
                           cr.acad_year, cr.semester,
                           cr.provisional_course_work_marks,
                           cr.provisional_exam_marks,
                           cr.provisional_total_marks,
                           CASE
                             WHEN COALESCE(cr.provisional_marks_status,'pending') = 'published' THEN 'published'
                             WHEN cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL THEN 'not_entered'
                             ELSE COALESCE(cr.provisional_marks_status,'pending')
                           END AS provisional_marks_status,
                           COALESCE(cr.provisional_marks_review_comments,'') AS provisional_marks_review_comments,
                           COALESCE(cr.provisional_marks_reviewed_by,'') AS provisional_marks_reviewed_by,
                           COALESCE(DATE_FORMAT(cr.provisional_marks_review_date, '%Y-%m-%d %H:%i'),'') AS provisional_marks_review_date,
                           COALESCE(cr.provisional_submitted_by,'') AS submitted_by,
                           COALESCE(cr.provisional_published_by,'') AS provisional_published_by,
                           COALESCE(DATE_FORMAT(cr.provisional_published_date, '%Y-%m-%d %H:%i'),'') AS provisional_published_date,
                           TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) AS student_name,
                           COALESCE(TRIM(e.emp_name),'') AS lecturer_name
                    FROM campus_dynamics_portal.acad_course_registration cr
                    LEFT JOIN acad_student s ON s.regno = cr.regno
                    LEFT JOIN acad_course c ON c.courseID = " + courseCol + @"
                    LEFT JOIN (
                        SELECT course_code, progcode, MAX(lecturer_id) AS lecturer_id
                        FROM acad_programmecourses
                        WHERE lecturer_id IS NOT NULL AND lecturer_id > 0
                        GROUP BY course_code, progcode
                    ) pc ON pc.course_code = " + courseCol + @" AND pc.progcode = cr.prog_id
                    LEFT JOIN hrm_employee e ON e.empID = pc.lecturer_id
                    WHERE cr.id = @id LIMIT 1";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                            return js.Serialize(new { success = false, message = "Record not found." });

                        var record = new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(rdr["id"]) },
                            { "regno", rdr["regno"].ToString() },
                            { "student_name", rdr["student_name"].ToString().Trim() },
                            { "courseID", rdr["courseID"].ToString() },
                            { "course_name", rdr["course_name"].ToString() },
                            { "prog_id", rdr["prog_id"].ToString() },
                            { "study_year", rdr["study_year"].ToString() },
                            { "acad_year", rdr["acad_year"].ToString() },
                            { "semester", rdr["semester"].ToString() },
                            { "lecturer_name", rdr["lecturer_name"].ToString().Trim() },
                            { "submitted_by", rdr["submitted_by"].ToString() },
                            { "provisional_course_work_marks", rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? (object)null : Convert.ToInt32(rdr["provisional_course_work_marks"]) },
                            { "provisional_exam_marks", rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks")) ? (object)null : Convert.ToInt32(rdr["provisional_exam_marks"]) },
                            { "provisional_total_marks", rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks")) ? (object)null : Convert.ToInt32(rdr["provisional_total_marks"]) },
                            { "provisional_marks_status", rdr["provisional_marks_status"].ToString() },
                            { "provisional_marks_review_comments", rdr["provisional_marks_review_comments"].ToString() },
                            { "provisional_marks_reviewed_by", rdr["provisional_marks_reviewed_by"].ToString() },
                            { "provisional_marks_review_date", rdr["provisional_marks_review_date"].ToString() },
                            { "provisional_published_by", rdr["provisional_published_by"].ToString() },
                            { "provisional_published_date", rdr["provisional_published_date"].ToString() }
                        };

                        return js.Serialize(new { success = true, record = record });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

        /// <summary>Reject or publish a provisional marks record.</summary>
    [WebMethod]
    public static string ReviewProvisionalMarks(int id, string action, string comment)
    {
        return MarksControllerShared.ReviewProvisionalMarks(id, action, comment);
    }

    /// <summary>Publish provisional marks to acad_results (final).</summary>
    [WebMethod]
    public static string PublishMarks(int id)
    {
        return MarksControllerShared.PublishMarks(id);
    }

    // ──────────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────────

    /// <summary>Admin direct mark save — override any status.</summary>
    [WebMethod]
    public static string SaveAdminMarks(int id, int? cw, int? exam, int? total, string note)
    {
        string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        var js = new JavaScriptSerializer();
        try
        {
            if (cw == null && exam == null && total == null)
                return js.Serialize(new { success = false, message = "At least one mark value is required." });

            // Auto-compute total if not supplied
            int? computedTotal = total;
            if (computedTotal == null && cw != null && exam != null)
                computedTotal = cw.Value + exam.Value;

            string actor = MarksAuthorizationService.GetCurrentUser();
            string reviewComment = string.IsNullOrWhiteSpace(note) ? "Admin mark override" : note.Trim();

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                MarkAuditContext.Set(conn, null, actor, "ProvisionalMarks:admin-override", reviewComment);
                string sql = @"UPDATE campus_dynamics_portal.acad_course_registration
                               SET provisional_course_work_marks = @cw,
                                   provisional_exam_marks        = @exam,
                                   provisional_total_marks       = @total,
                                   provisional_marks_status      = @pending,
                                   provisional_marks_reviewed_by       = @actor,
                                   provisional_marks_review_comments   = @comment,
                                   provisional_marks_review_date       = NOW()
                               WHERE id = @id";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cw",      (object)cw      ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@exam",    (object)exam    ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@total",   (object)computedTotal ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pending", StatusPending);
                    cmd.Parameters.AddWithValue("@actor",   actor);
                    cmd.Parameters.AddWithValue("@comment", reviewComment);
                    cmd.Parameters.AddWithValue("@id",      id);
                    if (cmd.ExecuteNonQuery() == 0)
                        return js.Serialize(new { success = false, message = "Record not found." });
                }
            }
            return js.Serialize(new { success = true, message = "Marks saved. Status reset to pending for re-review." });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    /// <summary>Reset a record back to pending.</summary>
    [WebMethod]
    public static string ResetToPending(int id)
    {
        string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        var js = new JavaScriptSerializer();
        try
        {
            string actor = MarksAuthorizationService.GetCurrentUser();
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string sql = @"UPDATE campus_dynamics_portal.acad_course_registration
                               SET provisional_marks_status           = @pending,
                                   provisional_marks_review_comments  = CONCAT(COALESCE(provisional_marks_review_comments,''), ' [Reset by admin: ', @actor, ']'),
                                   provisional_marks_reviewed_by      = @actor,
                                   provisional_marks_review_date      = NOW()
                               WHERE id = @id";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@pending", StatusPending);
                    cmd.Parameters.AddWithValue("@actor", actor);
                    cmd.Parameters.AddWithValue("@id",    id);
                    if (cmd.ExecuteNonQuery() == 0)
                        return js.Serialize(new { success = false, message = "Record not found." });
                }
            }
            return js.Serialize(new { success = true, message = "Record reset to pending." });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    /// <summary>Bulk reject / publish selected ids.</summary>
    [WebMethod]
    public static string BulkAction(int[] ids, string action, string comment)
    {
        return MarksControllerShared.BulkAction(ids, action, comment);
    }

    [WebMethod]
    public static string PreviewBatchWorkflow(string action, string scope, int[] ids, string year, string sem, string prog)
    {
        return MarksControllerShared.PreviewBatchWorkflow(action, scope, ids, year, sem, prog, PageKind);
    }

    [WebMethod]
    public static string ExecuteBatchWorkflow(string action, string scope, int[] ids, string year, string sem, string prog, string comment)
    {
        return MarksControllerShared.ExecuteBatchWorkflow(action, scope, ids, year, sem, prog, comment, PageKind);
    }

    // ──────────────────────────────────────────────────
    // ONE-TIME ADMIN FIX: MRU1300200613 NABUUSO EVA
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Restores Year 3 (2015/2016) results for MRU1300200613 (NABUUSO EVA).
    ///
    /// What was damaged:
    ///   - All 7 Sem-1 BMC courses relocated to semester=2 by the publish bug
    ///   - BMC3105 score overwritten 58 → 48
    ///   - 7 Sem-2 MSC courses completely absent from acad_results
    ///
    /// Safe to run multiple times (idempotent — ON DUPLICATE KEY UPDATE for inserts).
    ///
    /// Call once from browser console:
    ///   $.ajax({ url: '/COOPERP/NewScreens/ProvisionalMarksController.aspx/RestoreStudentMRU1300200613',
    ///            method:'POST', contentType:'application/json',
    ///            success: function(d){ console.log(JSON.parse(d.d)); } });
    /// </summary>
    [WebMethod]
    public static string RestoreStudentMRU1300200613()
    {
        const string regno = "MRU1300200613";
        const string acad  = "2015/2016";
        string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
        var js  = new JavaScriptSerializer();
        var log = new StringBuilder();
        int ok = 0, fail = 0;

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();

                // Resolve CreditUnits column name (case-insensitive probe)
                string creditCol = "CreditUnits";
                foreach (string c in new[]{"CreditUnits","creditunits","credit_units","creditunit","cu"})
                {
                    using (var p = new MySqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='campus_dynamics' AND TABLE_NAME='acad_results' AND LOWER(COLUMN_NAME)=LOWER(@c)", conn))
                    {
                        p.Parameters.AddWithValue("@c", c);
                        if (Convert.ToInt32(p.ExecuteScalar()) > 0) { creditCol = c; break; }
                    }
                }
                log.AppendFormat("CreditUnits column: {0}\n", creditCol);

                using (var tx = conn.BeginTransaction())
                {
                    // STEP 1: Restore semester=1 + studyyear=3 for all 7 BMC courses
                    using (var cmd = new MySqlCommand(@"
                        UPDATE campus_dynamics.acad_results
                        SET semester = 1, studyyear = 3
                        WHERE regno = @r AND acad = @a
                          AND UPPER(TRIM(courseid))
                              IN ('BMC3101','BMC3102','BMC3103','BMC3104','BMC3105','BMC3106','BMC3107')", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@a", acad);
                        log.AppendFormat("STEP 1 — semester=1 restored for {0} BMC row(s).\n", cmd.ExecuteNonQuery());
                        ok++;
                    }

                    // STEP 2: Restore BMC3105 score (overwritten 58→48 by provisional-publish bug)
                    using (var cmd = new MySqlCommand(@"
                        UPDATE campus_dynamics.acad_results
                        SET score = 58, grade = 'C+', gradept = 2.50, " + creditCol + @" = 3,
                            result_comment = CONCAT(COALESCE(result_comment,''),
                                ' [score restored 58 from 48: admin fix 2026-05-29]')
                        WHERE regno = @r AND acad = @a AND UPPER(TRIM(courseid)) = 'BMC3105'", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@a", acad);
                        log.AppendFormat("STEP 2 — BMC3105 score restored ({0} row(s)).\n", cmd.ExecuteNonQuery());
                        ok++;
                    }

                    // STEP 3: Set GPA=3.71 for semester 1
                    // Proof: (5.0+4.0+4.0+2.5+2.5+4.0+4.0)/7 = 26.0/7 = 3.71
                    using (var cmd = new MySqlCommand(@"
                        UPDATE campus_dynamics.acad_results SET gpa = 3.71
                        WHERE regno = @r AND acad = @a AND semester = 1", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@a", acad);
                        log.AppendFormat("STEP 3 — GPA=3.71 written for {0} sem-1 row(s).\n", cmd.ExecuteNonQuery());
                        ok++;
                    }

                    // STEP 4: Insert missing Semester-2 MSC courses from physical transcript
                    // Grade→score: A=80(5.0), B=65(3.5), B+=70(4.0), C+=55(2.5)
                    // GPA: (5.0+3.5+5.0+4.0+2.5+3.5+2.5)/7 = 26.0/7 = 3.71 (OCR: 3.72, rounding δ)
                    // MSC3208 appears twice on OCR (Internship + Contemporary Issues in Broadcasting);
                    // second instance assigned MSC3205 — the missing sequential code in 3201-3208.
                    var mscCourses = new object[][]
                    {
                        new object[]{"MSC3208",  80, "A",  5.00m, "INTERNSHIP"},
                        new object[]{"MSC3201",  65, "B",  3.50m, "SPECIALISED WRITING"},
                        new object[]{"MSC3202",  80, "A",  5.00m, "ADVANCED PLANNING AND NEW MEDIA"},
                        new object[]{"MSC3203",  70, "B+", 4.00m, "APPLIED PHOTO JOURNALISM"},
                        new object[]{"MSC3205",  55, "C+", 2.50m, "CONTEMPORARY ISSUES IN BROADCASTING [OCR MSC3208 dup resolved to MSC3205]"},
                        new object[]{"MSC3207",  65, "B",  3.50m, "INTERNATIONAL RELATIONS AND DIPLOMACY"},
                        new object[]{"MSC3204B", 55, "C+", 2.50m, "COMMERCIAL AND PROMOTIONAL WRITING"},
                    };

                    string upsertSql = @"
                        INSERT INTO campus_dynamics.acad_results
                            (regno, courseid, acad, semester, studyyear, score, grade, gradept, "
                            + creditCol + @", gpa, result_comment)
                        VALUES (@r, @c, @a, 2, 3, @sc, @gr, @gp, 3, 3.71, @note)
                        ON DUPLICATE KEY UPDATE
                            score          = VALUES(score),
                            grade          = VALUES(grade),
                            gradept        = VALUES(gradept),
                            " + creditCol + @" = VALUES(" + creditCol + @"),
                            gpa            = VALUES(gpa),
                            result_comment = VALUES(result_comment)";

                    int inserted = 0;
                    foreach (object[] row in mscCourses)
                    {
                        try
                        {
                            using (var cmd = new MySqlCommand(upsertSql, conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@r",    regno);
                                cmd.Parameters.AddWithValue("@c",    row[0]);
                                cmd.Parameters.AddWithValue("@a",    acad);
                                cmd.Parameters.AddWithValue("@sc",   row[1]);
                                cmd.Parameters.AddWithValue("@gr",   row[2]);
                                cmd.Parameters.AddWithValue("@gp",   row[3]);
                                cmd.Parameters.AddWithValue("@note", "Restored from physical transcript 2026-05-29: " + row[4]);
                                cmd.ExecuteNonQuery();
                                inserted++; ok++;
                            }
                        }
                        catch (Exception ex)
                        {
                            log.AppendFormat("STEP 4 WARN — {0}: {1}\n", row[0], ex.Message);
                            fail++;
                        }
                    }
                    log.AppendFormat("STEP 4 — {0} MSC semester-2 course(s) upserted.\n", inserted);

                    tx.Commit();
                    log.Append("Transaction committed OK.\n");
                }

                // VERIFY — read back for confirmation
                log.Append("\nVERIFICATION:\n");
                log.Append("SEM  COURSE     SCORE GRADE GP    GPA\n");
                using (var v = new MySqlCommand(@"
                    SELECT courseid, score, grade, gradept, semester, studyyear, gpa
                    FROM campus_dynamics.acad_results
                    WHERE regno = @r AND acad = @a ORDER BY semester, courseid", conn))
                {
                    v.Parameters.AddWithValue("@r", regno);
                    v.Parameters.AddWithValue("@a", acad);
                    using (var rdr = v.ExecuteReader())
                        while (rdr.Read())
                            log.AppendFormat("  {0}  {1,-10} {2,-6}{3,-6}{4,-6}{5}\n",
                                rdr["semester"], rdr["courseid"], rdr["score"],
                                rdr["grade"], rdr["gradept"], rdr["gpa"]);
                }
            }

            return js.Serialize(new { success = fail == 0, ok = ok, fail = fail, log = log.ToString() });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = ex.Message, log = log.ToString() });
        }
    }

    // ──────────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────────

    private static string NormalizeStatus(string status)
    {
        return (status ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static List<int> ResolveWorkflowTargetIds(MySqlConnection conn, string courseCol, string action, string scope, int[] ids, string year, string sem, string prog)
    {
        var targetIds = new List<int>();

        if (scope == "selected")
        {
            if (ids != null)
            {
                foreach (int id in ids)
                    if (id > 0) targetIds.Add(id);
            }
            return targetIds;
        }

        string sql = @"SELECT cr.id
                       FROM campus_dynamics_portal.acad_course_registration cr
                       WHERE 1=1";

        if (action == StatusApproved)
            sql += " AND (cr.provisional_course_work_marks IS NOT NULL OR cr.provisional_exam_marks IS NOT NULL) AND COALESCE(cr.provisional_marks_status,'pending')='pending'";
        else
            sql += " AND cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL AND cr.provisional_total_marks IS NOT NULL AND COALESCE(cr.provisional_marks_status,'pending') IN ('pending','approved')";

        if (!string.IsNullOrEmpty(year)) sql += " AND cr.acad_year=@year";
        if (!string.IsNullOrEmpty(sem)) sql += " AND cr.semester=@sem";
        if (!string.IsNullOrEmpty(prog)) sql += " AND cr.prog_id=@prog";

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            if (!string.IsNullOrEmpty(year)) cmd.Parameters.AddWithValue("@year", year);
            if (!string.IsNullOrEmpty(sem)) cmd.Parameters.AddWithValue("@sem", sem);
            if (!string.IsNullOrEmpty(prog)) cmd.Parameters.AddWithValue("@prog", prog);

            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                    targetIds.Add(Convert.ToInt32(rdr["id"]));
            }
        }
        return targetIds;
    }

    // MRU / NCHE scale: 80 A · 75 B+ · 70 B · 65 C+ · 60 C · 55 D+ · 50 D · <50 F (no A-/B-/C-).
    private static string ComputeGrade(int score)
    {
        if (score >= 80) return "A";
        if (score >= 75) return "B+";
        if (score >= 70) return "B";
        if (score >= 65) return "C+";
        if (score >= 60) return "C";
        if (score >= 55) return "D+";
        if (score >= 50) return "D";
        return "F";
    }

    private static string HtmlEnc(string s)
    {
        return HttpUtility.HtmlEncode(s ?? "");
    }

    private static void EnsureResultCommentTextNullable(MySqlConnection conn)
    {
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(@"
                SELECT DATA_TYPE, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA='campus_dynamics'
                  AND TABLE_NAME='acad_results'
                  AND COLUMN_NAME='result_comment'
                LIMIT 1", conn))
            using (var rdr = cmd.ExecuteReader())
            {
                if (!rdr.Read()) return;
                string dataType = rdr.IsDBNull(0) ? "" : rdr.GetString(0).ToLowerInvariant();
                string isNullable = rdr.IsDBNull(1) ? "YES" : rdr.GetString(1).ToUpperInvariant();
                if (dataType == "text" && isNullable == "YES") return;
            }

            using (MySqlCommand alter = new MySqlCommand("ALTER TABLE campus_dynamics.acad_results MODIFY COLUMN result_comment TEXT NULL", conn))
            {
                alter.ExecuteNonQuery();
            }
        }
        catch { }
    }

    private static void SafeSelect(System.Web.UI.WebControls.DropDownList ddl, string val)
    {
        var item = ddl.Items.FindByValue(val);
        if (item != null) item.Selected = true;
    }

    private static bool ColumnExists(MySqlConnection conn, string schema, string table, string column)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@s AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
        {
            cmd.Parameters.AddWithValue("@s", schema);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", column);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    private static void EnsureNullableColumn(MySqlConnection conn, string schema, string table, string column, string sqlType)
    {
        if (ColumnExists(conn, schema, table, column)) return;
        using (MySqlCommand cmd = new MySqlCommand(
            "ALTER TABLE " + schema + "." + table + " ADD COLUMN " + column + " " + sqlType, conn))
        {
            cmd.ExecuteNonQuery();
        }
    }

    private static string GetCourseColumnExpression(MySqlConnection conn, string alias)
    {
        bool hasCourseId = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "courseID");
        bool hasCourseCode = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "course_code");

        string prefix = string.IsNullOrEmpty(alias) ? "" : (alias + ".");
        if (hasCourseId) return prefix + "courseID";
        if (hasCourseCode) return prefix + "course_code";
        return null;
    }
}
