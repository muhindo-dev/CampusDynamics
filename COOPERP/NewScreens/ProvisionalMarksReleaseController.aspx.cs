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

public partial class COOPERP_NewScreens_ProvisionalMarksReleaseController : System.Web.UI.Page
{
    private const int DefaultPageSize = 50;

    private string ConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

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

    private static void EnsureProvisionalColumns(MySqlConnection conn)
    {
        const string schema = "campus_dynamics_portal";
        const string table = "acad_course_registration";
        try { EnsureNullableColumn(conn, schema, table, "provisional_course_work_marks", "INT NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_exam_marks", "INT NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_total_marks", "INT NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_status", "VARCHAR(20) NULL DEFAULT 'pending'"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_published_by", "VARCHAR(150) NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_published_date", "DATETIME NULL"); } catch { }
    }

    private void LoadFilters(MySqlConnection conn)
    {
        ddlYear.Items.Clear();
        ddlYear.Items.Add(new ListItem("All Years", ""));
        using (MySqlCommand cmd = new MySqlCommand("SELECT DISTINCT acad_year FROM campus_dynamics_portal.acad_course_registration WHERE acad_year IS NOT NULL AND acad_year <> '' ORDER BY acad_year DESC", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
                ddlYear.Items.Add(new ListItem(rdr.GetString(0)));
        }

        ddlProg.Items.Clear();
        ddlProg.Items.Add(new ListItem("All Programmes", ""));
        using (MySqlCommand cmd = new MySqlCommand("SELECT DISTINCT p.progcode, COALESCE(p.progname,p.progcode) AS pnm FROM acad_programme p INNER JOIN campus_dynamics_portal.acad_course_registration cr ON cr.prog_id=p.progcode ORDER BY pnm", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
                ddlProg.Items.Add(new ListItem(rdr.GetString(1), rdr.GetString(0)));
        }

        SafeSelect(ddlYear, Request.QueryString["year"] ?? "");
        SafeSelect(ddlSemester, Request.QueryString["sem"] ?? "");
        SafeSelect(ddlStatus, Request.QueryString["status"] ?? "");
        SafeSelect(ddlProg, Request.QueryString["prog"] ?? "");
        SafeSelect(ddlPageSize, Request.QueryString["size"] ?? "");
        txtSearch.Text = Request.QueryString["q"] ?? "";
    }

    private void LoadStats(MySqlConnection conn)
    {
        string sql = @"
            SELECT
                SUM(CASE WHEN COALESCE(provisional_marks_status,'pending') <> 'published' THEN 1 ELSE 0 END) AS unreleased,
                SUM(CASE WHEN COALESCE(provisional_marks_status,'pending') IN ('approved','pending') AND provisional_course_work_marks IS NOT NULL AND provisional_exam_marks IS NOT NULL THEN 1 ELSE 0 END) AS approved_ready,
                SUM(CASE WHEN COALESCE(provisional_marks_status,'pending') = 'rejected' OR provisional_course_work_marks IS NULL OR provisional_exam_marks IS NULL THEN 1 ELSE 0 END) AS needs_attention,
                SUM(CASE WHEN COALESCE(provisional_marks_status,'pending') = 'published' THEN 1 ELSE 0 END) AS published_cnt
            FROM campus_dynamics_portal.acad_course_registration
            WHERE provisional_total_marks IS NOT NULL" + ActiveStudentFilter.Clause("regno");

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        using (var rdr = cmd.ExecuteReader())
        {
            if (rdr.Read())
            {
                litUnreleased.Text = (rdr.IsDBNull(0) ? 0 : Convert.ToInt32(rdr[0])).ToString("N0");
                litApprovedReady.Text = (rdr.IsDBNull(1) ? 0 : Convert.ToInt32(rdr[1])).ToString("N0");
                litNeedsAttention.Text = (rdr.IsDBNull(2) ? 0 : Convert.ToInt32(rdr[2])).ToString("N0");
                litPublished.Text = (rdr.IsDBNull(3) ? 0 : Convert.ToInt32(rdr[3])).ToString("N0");
            }
        }
    }

    private void BindGrid(MySqlConnection conn)
    {
        int page = ParseInt(Request.QueryString["pg"], 1, 1, 1000000);
        int pageSize = ParseInt(Request.QueryString["size"], ParseInt(ddlPageSize.SelectedValue, DefaultPageSize, 1, 200), 1, 200);
        string year = ddlYear.SelectedValue;
        string sem = ddlSemester.SelectedValue;
        string status = ddlStatus.SelectedValue;
        string prog = ddlProg.SelectedValue;
        string q = txtSearch.Text.Trim();
        string courseCol = GetCourseColumnExpression(conn, "cr");

        if (string.IsNullOrEmpty(courseCol))
        {
            litRows.Text = "<tr><td colspan='14' class='rl-empty'>Course column not found on acad_course_registration.</td></tr>";
            SetMeta(0, 0, 0, 1, 1);
            litPager.Text = "";
            return;
        }

        StringBuilder where = BuildUnreleasedWhere(year, sem, status, prog, q, courseCol);
        // The mark LIST + its count show ALL marks for everyone (alumni + active) — no filter.
        // Only the funnel stats (LoadStats) are narrowed to active students.

        int total;
        using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration cr " + where.ToString(), conn))
        {
            AddFilterParams(cmd, year, sem, status, prog, q);
            total = Convert.ToInt32(cmd.ExecuteScalar());
        }

        int pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > pageCount) page = pageCount;
        int offset = (page - 1) * pageSize;

        string sql = @"
                 SELECT cr.id, cr.regno, " + courseCol + @" AS courseID,
                     COALESCE(cr.prog_id,'') AS prog_id,
                   cr.acad_year, cr.semester,
                     cr.provisional_course_work_marks,
                     cr.provisional_exam_marks,
                   cr.provisional_total_marks,
                     COALESCE(cr.provisional_marks_status,'pending') AS prov_status,
                     COALESCE(cr.provisional_submitted_by,'') AS submitted_by,
                     COALESCE(cr.provisional_marks_reviewed_by,'') AS reviewed_by,
                     cr.provisional_marks_review_date,
                     COALESCE(cr.provisional_marks_review_comments,'') AS review_comments
            FROM campus_dynamics_portal.acad_course_registration cr
            " + where.ToString() + @"
            ORDER BY cr.id DESC
            LIMIT @offset, @size";

        var sb = new StringBuilder();
        int sn = offset + 1;

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        {
            AddFilterParams(cmd, year, sem, status, prog, q);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@size", pageSize);

            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    int id = Convert.ToInt32(rdr["id"]);
                    string regno = rdr["regno"].ToString();
                    string courseID = rdr["courseID"].ToString();
                    string progId = rdr["prog_id"].ToString();
                    string acadYear = rdr["acad_year"].ToString();
                    string semester = rdr["semester"].ToString();
                    string cw = rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? "-" : rdr["provisional_course_work_marks"].ToString();
                    string exam = rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks")) ? "-" : rdr["provisional_exam_marks"].ToString();
                    string totalMarks = rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks")) ? "-" : rdr["provisional_total_marks"].ToString();
                    string statusVal = rdr["prov_status"].ToString();
                    string submittedBy = rdr["submitted_by"].ToString();
                    string reviewedBy = rdr["reviewed_by"].ToString();
                    string reviewDate = rdr.IsDBNull(rdr.GetOrdinal("provisional_marks_review_date")) ? "" : Convert.ToDateTime(rdr["provisional_marks_review_date"]).ToString("dd MMM yyyy HH:mm");
                    string reviewComments = rdr["review_comments"].ToString();

                    bool complete = rdr["provisional_course_work_marks"] != DBNull.Value
                                    && rdr["provisional_exam_marks"] != DBNull.Value
                                    && rdr["provisional_total_marks"] != DBNull.Value;
                    bool canPublish = complete && (string.Equals(statusVal, "approved", StringComparison.OrdinalIgnoreCase)
                                                   || string.Equals(statusVal, "pending", StringComparison.OrdinalIgnoreCase));
                    string statusCss = "rl-pill--pending";
                    if (statusVal == "approved") statusCss = "rl-pill--approved";
                    else if (statusVal == "rejected") statusCss = "rl-pill--rejected";

                    string completionText = complete ? "Complete" : "Incomplete";
                    string completionCss = complete ? "pm-pill pm-pill--ok" : "pm-pill pm-pill--warn";
                    string reviewedDisplay = string.IsNullOrEmpty(reviewedBy) ? "-" : (reviewedBy + (string.IsNullOrEmpty(reviewDate) ? "" : "<br/><span class='rl-muted'>" + HtmlEnc(reviewDate) + "</span>"));
                    string quickSearch = (regno + " " + courseID + " " + progId + " " + submittedBy + " " + reviewedBy + " " + reviewComments).Trim();

                    sb.AppendFormat("<tr data-search='{0}'>", HtmlEnc(quickSearch));
                    sb.AppendFormat("<td class='rl-center'><input type='checkbox' class='pm-row-check' data-id='{0}' {1} /></td>", id, canPublish ? "" : "disabled='disabled'");
                    sb.AppendFormat("<td class='rl-center rl-muted'>{0}</td>", sn++);
                    sb.AppendFormat("<td><span class='rl-code'>{0}</span></td>", HtmlEnc(regno));
                    sb.AppendFormat("<td><span class='rl-code'>{0}</span><br/><span class='rl-muted'>{1}</span></td>", HtmlEnc(courseID), HtmlEnc(progId));
                    sb.AppendFormat("<td class='rl-center'>{0}<br/><span class='rl-muted'>Sem {1}</span></td>", HtmlEnc(acadYear), HtmlEnc(semester));
                    sb.AppendFormat("<td class='rl-center'>{0}</td>", HtmlEnc(cw));
                    sb.AppendFormat("<td class='rl-center'>{0}</td>", HtmlEnc(exam));
                    sb.AppendFormat("<td class='rl-center' style='font-weight:700;color:#05275C;'>{0}</td>", HtmlEnc(totalMarks));
                    sb.AppendFormat("<td class='rl-center'><span class='{0}'>{1}</span></td>", completionCss, completionText);
                    sb.AppendFormat("<td class='rl-center'><span class='rl-pill {0}'>{1}</span></td>", statusCss, HtmlEnc(statusVal));
                    sb.AppendFormat("<td class='rl-muted'>{0}</td>", string.IsNullOrEmpty(submittedBy) ? "-" : HtmlEnc(submittedBy));
                    sb.AppendFormat("<td class='rl-muted'>{0}</td>", reviewedDisplay);
                    sb.AppendFormat("<td class='rl-muted'>{0}</td>", string.IsNullOrEmpty(reviewComments) ? "-" : HtmlEnc(reviewComments));
                    sb.AppendFormat("<td class='rl-center'><div class='rl-actions'><button type='button' class='rl-btn rl-btn--sm rl-btn--primary' {1} onclick='publishSingle({0})'>{2}</button></div></td>", id, canPublish ? "" : "disabled='disabled'", canPublish ? "Release" : "Locked");
                    sb.Append("</tr>");
                }
            }
        }

        if (sn == offset + 1)
            sb.Append("<tr><td colspan='14' class='rl-empty'>No unreleased provisional marks found for the selected filter.</td></tr>");

        litRows.Text = sb.ToString();

        int from = total == 0 ? 0 : offset + 1;
        int to = Math.Min(offset + pageSize, total);
        SetMeta(from, to, total, page, pageCount);
        litPager.Text = BuildPager(page, pageCount, year, sem, status, prog, q, pageSize);
    }

    private void SetMeta(int from, int to, int total, int page, int pageCount)
    {
        litFrom.Text = from.ToString();
        litTo.Text = to.ToString();
        litTotal.Text = total.ToString();
        litTotal2.Text = total.ToString();
        litPage.Text = page.ToString();
        litPageCount.Text = pageCount.ToString();
    }

    private static StringBuilder BuildUnreleasedWhere(string year, string sem, string status, string prog, string q, string courseCol)
    {
        StringBuilder where = new StringBuilder("WHERE (cr.provisional_course_work_marks IS NOT NULL OR cr.provisional_exam_marks IS NOT NULL OR cr.provisional_total_marks IS NOT NULL) AND COALESCE(cr.provisional_marks_status,'pending') <> 'published'");
        if (!string.IsNullOrEmpty(year)) where.Append(" AND cr.acad_year=@year");
        if (!string.IsNullOrEmpty(sem)) where.Append(" AND cr.semester=@sem");
        if (!string.IsNullOrEmpty(status))
        {
            if (string.Equals(status, "incomplete", StringComparison.OrdinalIgnoreCase))
                where.Append(" AND (cr.provisional_course_work_marks IS NULL OR cr.provisional_exam_marks IS NULL OR cr.provisional_total_marks IS NULL)");
            else if (string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase))
                where.Append(" AND cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL AND cr.provisional_total_marks IS NOT NULL AND COALESCE(cr.provisional_marks_status,'pending') IN ('approved','pending')");
            else
                where.Append(" AND COALESCE(cr.provisional_marks_status,'pending')=@status");
        }
        if (!string.IsNullOrEmpty(prog)) where.Append(" AND cr.prog_id=@prog");
        if (!string.IsNullOrEmpty(q)) where.Append(" AND (cr.regno LIKE @q OR " + courseCol + " LIKE @q OR COALESCE(cr.provisional_submitted_by,'') LIKE @q OR COALESCE(cr.provisional_marks_reviewed_by,'') LIKE @q OR COALESCE(cr.provisional_marks_review_comments,'') LIKE @q)");
        return where;
    }

    private static void AddFilterParams(MySqlCommand cmd, string year, string sem, string status, string prog, string q)
    {
        if (!string.IsNullOrEmpty(year)) cmd.Parameters.AddWithValue("@year", year);
        if (!string.IsNullOrEmpty(sem)) cmd.Parameters.AddWithValue("@sem", sem);
        if (!string.IsNullOrEmpty(status)
            && !string.Equals(status, "incomplete", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase))
            cmd.Parameters.AddWithValue("@status", status);
        if (!string.IsNullOrEmpty(prog)) cmd.Parameters.AddWithValue("@prog", prog);
        if (!string.IsNullOrEmpty(q)) cmd.Parameters.AddWithValue("@q", "%" + q + "%");
    }

    private static int ParseInt(string value, int fallback, int min, int max)
    {
        int v;
        if (!int.TryParse(value, out v)) v = fallback;
        if (v < min) v = min;
        if (v > max) v = max;
        return v;
    }

    private static string BuildPager(int page, int pageCount, string year, string sem, string status, string prog, string q, int size)
    {
        var sb = new StringBuilder();
        int start = Math.Max(1, page - 3);
        int end = Math.Min(pageCount, page + 3);

        if (page > 1)
            sb.AppendFormat("<a href='?pg={0}&year={1}&sem={2}&status={3}&prog={4}&size={5}&q={6}'>&laquo;</a>",
                page - 1, Uri.EscapeDataString(year), Uri.EscapeDataString(sem), Uri.EscapeDataString(status), Uri.EscapeDataString(prog), size, Uri.EscapeDataString(q));

        for (int i = start; i <= end; i++)
        {
            if (i == page) sb.AppendFormat("<span class='active'>{0}</span>", i);
            else sb.AppendFormat("<a href='?pg={0}&year={1}&sem={2}&status={3}&prog={4}&size={5}&q={6}'>{0}</a>",
                i, Uri.EscapeDataString(year), Uri.EscapeDataString(sem), Uri.EscapeDataString(status), Uri.EscapeDataString(prog), size, Uri.EscapeDataString(q));
        }

        if (page < pageCount)
            sb.AppendFormat("<a href='?pg={0}&year={1}&sem={2}&status={3}&prog={4}&size={5}&q={6}'>&raquo;</a>",
                page + 1, Uri.EscapeDataString(year), Uri.EscapeDataString(sem), Uri.EscapeDataString(status), Uri.EscapeDataString(prog), size, Uri.EscapeDataString(q));

        return sb.ToString();
    }

    [WebMethod]
    public static string PublishSelectedMarks(int[] ids)
    {
        var js = new JavaScriptSerializer();
        if (ids == null || ids.Length == 0)
            return js.Serialize(new { success = false, message = "No records selected." });

        try
        {
            string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            string publisher = MarksAuthorizationService.GetCurrentUser();
            int released = 0;
            int skipped = 0;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                foreach (int id in ids)
                {
                    string reason;
                    if (PublishRecordById(conn, id, publisher, out reason)) released++;
                    else skipped++;
                }
            }

            return js.Serialize(new { success = true, released = released, skipped = skipped });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    [WebMethod]
    public static string PublishBatchByFilter(string year, string sem, string status, string prog, string size, string q)
    {
        var js = new JavaScriptSerializer();

        try
        {
            string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            string publisher = MarksAuthorizationService.GetCurrentUser();
            int released = 0;
            int skipped = 0;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string courseCol = GetCourseColumnExpression(conn, "cr");
                if (string.IsNullOrEmpty(courseCol))
                    return js.Serialize(new { success = false, message = "Course column not found." });

                StringBuilder where = BuildUnreleasedWhere(year ?? "", sem ?? "", status ?? "", prog ?? "", q ?? "", courseCol);
                string sql = "SELECT cr.id FROM campus_dynamics_portal.acad_course_registration cr " + where.ToString();

                var ids = new List<int>();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    AddFilterParams(cmd, year ?? "", sem ?? "", status ?? "", prog ?? "", q ?? "");
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            ids.Add(Convert.ToInt32(rdr["id"]));
                    }
                }

                foreach (int id in ids)
                {
                    string reason;
                    if (PublishRecordById(conn, id, publisher, out reason)) released++;
                    else skipped++;
                }
            }

            return js.Serialize(new { success = true, released = released, skipped = skipped });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    [WebMethod]
    public static string PublishSpecificRecord(string regno, string courseID, string acadYear, string semester)
    {
        var js = new JavaScriptSerializer();
        if (string.IsNullOrWhiteSpace(regno) || string.IsNullOrWhiteSpace(courseID) || string.IsNullOrWhiteSpace(acadYear) || string.IsNullOrWhiteSpace(semester))
            return js.Serialize(new { success = false, message = "Reg No, course, academic year and semester are required." });

        try
        {
            string connStr = WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            string publisher = MarksAuthorizationService.GetCurrentUser();

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string courseCol = GetCourseColumnExpression(conn, "");
                if (string.IsNullOrEmpty(courseCol))
                    return js.Serialize(new { success = false, message = "Course column not found." });

                string sql = "SELECT id FROM campus_dynamics_portal.acad_course_registration WHERE regno=@regno AND " + courseCol + "=@courseid AND acad_year=@acad AND semester=@sem ORDER BY id DESC LIMIT 1";
                int id = 0;
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@regno", regno.Trim());
                    cmd.Parameters.AddWithValue("@courseid", courseID.Trim());
                    cmd.Parameters.AddWithValue("@acad", acadYear.Trim());
                    cmd.Parameters.AddWithValue("@sem", semester.Trim());
                    object obj = cmd.ExecuteScalar();
                    if (obj == null || obj == DBNull.Value)
                        return js.Serialize(new { success = false, message = "No matching provisional record found." });
                    id = Convert.ToInt32(obj);
                }

                string reason;
                if (!PublishRecordById(conn, id, publisher, out reason))
                    return js.Serialize(new { success = false, message = reason });
            }

            return js.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    [WebMethod]
    public static string RepairReleasedMarks(string regno)
    {
        var js = new JavaScriptSerializer();
        try
        {
            string connStr = System.Web.Configuration.WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            int repaired = 0;
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                // Find acad_results rows for this student with NULL studyyear or NULL gradept
                string findSql = @"SELECT ar.ID, ar.courseid, ar.acad, ar.semester, ar.score, ar.grade,
                                          ar.progid
                                   FROM acad_results ar
                                   WHERE TRIM(ar.regno) = TRIM(@reg)
                                     AND (ar.studyyear IS NULL OR ar.gradept IS NULL OR ar.gradept = 0)";
                List<ResultFixRow> toFix = new List<ResultFixRow>();
                using (MySqlCommand cmd = new MySqlCommand(findSql, conn))
                {
                    cmd.Parameters.AddWithValue("@reg", regno);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            int rid    = Convert.ToInt32(rdr["ID"]);
                            string cid = rdr["courseid"].ToString();
                            string acd = rdr["acad"].ToString();
                            string sem = rdr["semester"].ToString();
                            int sc     = rdr.IsDBNull(rdr.GetOrdinal("score")) ? 0 : Convert.ToInt32(rdr["score"]);
                            string grd = rdr.IsDBNull(rdr.GetOrdinal("grade")) ? "" : rdr["grade"].ToString();
                            string prg = rdr.IsDBNull(rdr.GetOrdinal("progid")) ? "" : rdr["progid"].ToString();
                            ResultFixRow fx = new ResultFixRow();
                            fx.id = rid; fx.courseid = cid; fx.acad = acd; fx.sem = sem;
                            fx.score = sc; fx.grade = grd; fx.progid = prg;
                            toFix.Add(fx);
                        }
                    }
                }

                foreach (var row in toFix)
                {
                    // Get studyyear from acad_registration
                    int studyYr = 1;
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT COALESCE(MAX(studyyear),1) FROM acad_registration WHERE TRIM(regno)=TRIM(@reg) AND acad_year=@acd AND semester=@sem", conn))
                    {
                        cmd.Parameters.AddWithValue("@reg", regno);
                        cmd.Parameters.AddWithValue("@acd", row.acad);
                        cmd.Parameters.AddWithValue("@sem", row.sem);
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) studyYr = Convert.ToInt32(v);
                    }

                    // Get CreditUnits
                    decimal cu = 3;
                    using (MySqlCommand cmd = new MySqlCommand("SELECT COALESCE(CreditUnit,3) FROM acad_course WHERE courseID=@crs LIMIT 1", conn))
                    {
                        cmd.Parameters.AddWithValue("@crs", row.courseid);
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) cu = Convert.ToDecimal(v);
                    }

                    double gradept;
                    string grade = ResolveGradeAndPoint(row.progid, row.score, out gradept);
                    if (string.IsNullOrEmpty(grade)) grade = row.grade;

                    using (MySqlCommand cmd = new MySqlCommand(
                        "UPDATE acad_results SET studyyear=@yr, gradept=@gp, CreditUnits=@cu, grade=@grd WHERE ID=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@yr",  studyYr);
                        cmd.Parameters.AddWithValue("@gp",  gradept);
                        cmd.Parameters.AddWithValue("@cu",  cu);
                        cmd.Parameters.AddWithValue("@grd", grade);
                        cmd.Parameters.AddWithValue("@id",  row.id);
                        cmd.ExecuteNonQuery();
                        repaired++;
                    }
                }
            }
            return js.Serialize(new { success = true, repaired = repaired });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    private static bool PublishRecordById(MySqlConnection conn, int id, string publisher, out string reason)
    {
        reason = "";
        string courseCol = GetCourseColumnExpression(conn, "");
        if (string.IsNullOrEmpty(courseCol))
        {
            reason = "Course column not found.";
            return false;
        }

        string regno = "";
        string courseID = "";
        string acadYear = "";
        string semester = "";
        string progId = "";
        string status = "";
        int? cw = null;
        int? exam = null;
        int? total = null;
        int studyYear = 1;

        string fetchSql = @"SELECT regno, " + courseCol + @" AS courseID, acad_year, semester,
                       COALESCE(prog_id,'') AS prog_id,
                       COALESCE((
                           SELECT MAX(r2.studyyear)
                           FROM acad_registration r2
                           WHERE TRIM(r2.regno) = TRIM(cr.regno)
                             AND r2.acad_year = cr.acad_year
                             AND r2.semester = cr.semester
                       ), 1) AS studyyear,
                       provisional_course_work_marks,
                       provisional_exam_marks,
                       provisional_total_marks,
                       COALESCE(provisional_marks_status,'pending') AS status
                            FROM campus_dynamics_portal.acad_course_registration cr
                            WHERE id=@id LIMIT 1";

        using (MySqlCommand cmd = new MySqlCommand(fetchSql, conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            using (var rdr = cmd.ExecuteReader())
            {
                if (!rdr.Read())
                {
                    reason = "Record not found.";
                    return false;
                }

                regno = rdr["regno"].ToString();
                courseID = rdr["courseID"].ToString();
                acadYear = rdr["acad_year"].ToString();
                semester = rdr["semester"].ToString();
                progId = rdr["prog_id"].ToString();
                status = rdr["status"].ToString();
                studyYear = rdr.IsDBNull(rdr.GetOrdinal("studyyear")) ? 1 : Convert.ToInt32(rdr["studyyear"]);
                cw = rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? (int?)null : Convert.ToInt32(rdr["provisional_course_work_marks"]);
                exam = rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks")) ? (int?)null : Convert.ToInt32(rdr["provisional_exam_marks"]);
                total = rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks")) ? (int?)null : Convert.ToInt32(rdr["provisional_total_marks"]);
            }
        }

        if (status == "published")
        {
            reason = "Already published.";
            return false;
        }

        if (!string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Only approved or pending provisional marks can be published.";
            return false;
        }

        if (!cw.HasValue || !exam.HasValue)
        {
            reason = "Cannot publish: coursework and exam marks must both be entered.";
            return false;
        }

        if (cw.Value < 0 || cw.Value > 40 || exam.Value < 0 || exam.Value > 60)
        {
            reason = "Cannot publish: CW/Exam marks are out of allowed range.";
            return false;
        }

        int publishScore = cw.Value + exam.Value;
        if (publishScore > 100)
        {
            reason = "Cannot publish: total marks exceed 100.";
            return false;
        }

        if (!total.HasValue)
        {
            total = publishScore;
        }

        double gradept;
        string grade = ResolveGradeAndPoint(progId, publishScore, out gradept);
        EnsureResultCommentTextNullable(conn);
        string finalComment = "Released by " + publisher;

        // Fetch CreditUnits from acad_course; default to 3 if not found
        decimal creditUnits = 3;
        using (MySqlCommand cmd = new MySqlCommand("SELECT COALESCE(CreditUnit, 3) FROM acad_course WHERE courseID=@crs LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@crs", courseID);
            object cu = cmd.ExecuteScalar();
            if (cu != null && cu != DBNull.Value) creditUnits = Convert.ToDecimal(cu);
        }

        string updateSql = @"UPDATE acad_results
                             SET score=@score, grade=@grade, gradept=@gradept, CreditUnits=@cu,
                                 course_work=@cw, exam_total=@exam,
                                 studyyear=@studyyear, progid=@progid, result_comment=@comment
                             WHERE regno=@regno AND courseid=@courseid AND acad=@acad AND semester=@sem";
        int updated;
        using (MySqlCommand cmd = new MySqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("@regno", regno);
            cmd.Parameters.AddWithValue("@courseid", courseID);
            cmd.Parameters.AddWithValue("@acad", acadYear);
            cmd.Parameters.AddWithValue("@sem", semester);
            cmd.Parameters.AddWithValue("@score", publishScore);
            cmd.Parameters.AddWithValue("@grade", grade);
            cmd.Parameters.AddWithValue("@gradept", gradept);
            cmd.Parameters.AddWithValue("@cu", creditUnits);
            cmd.Parameters.AddWithValue("@cw", cw.HasValue ? (object)cw.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@exam", exam.HasValue ? (object)exam.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@studyyear", studyYear);
            cmd.Parameters.AddWithValue("@progid", progId);
            cmd.Parameters.AddWithValue("@comment", finalComment);
            updated = cmd.ExecuteNonQuery();
        }

        if (updated == 0)
        {
            string insertSql = @"INSERT INTO acad_results
                                 (regno, courseid, acad, semester, studyyear, progid,
                                  course_work, exam_total, score, grade, gradept, CreditUnits, result_comment)
                                 VALUES
                                 (@regno, @courseid, @acad, @sem, @studyyear, @progid,
                                  @cw, @exam, @score, @grade, @gradept, @cu, @comment)";
            using (MySqlCommand cmd = new MySqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@regno", regno);
                cmd.Parameters.AddWithValue("@courseid", courseID);
                cmd.Parameters.AddWithValue("@acad", acadYear);
                cmd.Parameters.AddWithValue("@sem", semester);
                cmd.Parameters.AddWithValue("@studyyear", studyYear);
                cmd.Parameters.AddWithValue("@progid", progId);
                cmd.Parameters.AddWithValue("@cw", cw.HasValue ? (object)cw.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@exam", exam.HasValue ? (object)exam.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@score", publishScore);
                cmd.Parameters.AddWithValue("@grade", grade);
                cmd.Parameters.AddWithValue("@gradept", gradept);
                cmd.Parameters.AddWithValue("@cu", creditUnits);
                cmd.Parameters.AddWithValue("@comment", finalComment);
                cmd.ExecuteNonQuery();
            }
        }

        string markSql = @"UPDATE campus_dynamics_portal.acad_course_registration
                           SET provisional_total_marks=@total, provisional_marks_status='published', provisional_published_by=@publisher, provisional_published_date=NOW()
                           WHERE id=@id";
        using (MySqlCommand cmd = new MySqlCommand(markSql, conn))
        {
            cmd.Parameters.AddWithValue("@publisher", publisher);
            cmd.Parameters.AddWithValue("@total", publishScore);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        return true;
    }

    private static string ResolveGradeAndPoint(string progId, int score, out double gradePoint)
    {
        gradePoint = 0.0;
        try
        {
            List<MarksSheetService.GradeBoundary> scale = MarksSheetService.GetGradingScale(progId ?? "");
            if (scale != null && scale.Count > 0)
            {
                string g = MarksSheetService.ComputeGrade(score, scale);
                // Find matching grade point from scale
                foreach (var b in scale)
                    if (string.Equals(b.Grade, g, StringComparison.OrdinalIgnoreCase))
                    { gradePoint = b.GradePoint; break; }
                return g;
            }
        }
        catch { }

        string fallbackGrade = ComputeGradeFallback(score);
        gradePoint = ComputeGradePointFallback(fallbackGrade);
        return fallbackGrade;
    }

    private static double ComputeGradePointFallback(string grade)
    {
        switch (grade)
        {
            case "A":  return 5.0;
            case "B+": return 4.5;
            case "B":  return 4.0;
            case "C+": return 3.5;
            case "C":  return 3.0;
            case "D+": return 2.5;
            case "D":  return 2.0;
            default:   return 0.0;   // F
        }
    }

    // MRU / NCHE scale: 80 A · 75 B+ · 70 B · 65 C+ · 60 C · 55 D+ · 50 D · <50 F (no A-/B-/C-).
    private static string ComputeGradeFallback(int score)
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

    private static bool ColumnExists(MySqlConnection conn, string schema, string table, string column)
    {
        using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@s AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
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
        using (MySqlCommand cmd = new MySqlCommand("ALTER TABLE " + schema + "." + table + " ADD COLUMN " + column + " " + sqlType, conn))
            cmd.ExecuteNonQuery();
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

    private static string GetCourseColumnExpression(MySqlConnection conn, string alias)
    {
        bool hasCourseId = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "courseID");
        bool hasCourseCode = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "course_code");

        string p = string.IsNullOrEmpty(alias) ? "" : alias + ".";
        if (hasCourseId) return p + "courseID";
        if (hasCourseCode) return p + "course_code";
        return null;
    }

    private static string HtmlEnc(string s)
    {
        return HttpUtility.HtmlEncode(s ?? "");
    }

    private static void SafeSelect(DropDownList ddl, string value)
    {
        ListItem li = ddl.Items.FindByValue(value);
        if (li != null) li.Selected = true;
    }

    /// <summary>
    /// Row carrier for RepairStudentResults. This was a C# 7 named tuple, which this site
    /// cannot compile - the ASP.NET build here is C# 5, so the page returned CS1031
    /// "Type expected" and 500'd for every visitor. Keep it a plain class.
    /// </summary>
    private class ResultFixRow
    {
        public int id;
        public string courseid;
        public string acad;
        public string sem;
        public int score;
        public string grade;
        public string progid;
    }
}
