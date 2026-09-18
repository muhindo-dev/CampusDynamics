using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public static class MarksControllerShared
{
    private const int DefaultPageSize = 100;
    private const string StatusPending = "pending";
    private const string StatusApproved = "approved";
    private const string StatusRejected = "rejected";
    private const string StatusPublished = "published";
    private const string StatusNotEntered = "not_entered";
    private const string ActionUnpublish = "unpublish";
    private const string ActionReprocess = "reprocess";

    private sealed class ProvisionalActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal SemesterGpa { get; set; }
        public decimal Cgpa { get; set; }
        public string AwardClass { get; set; }
    }

    public static string ConnectionString
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ── Role-based data scope (admin=all, dean=faculty, HOD=department) ──
    // Resolved once per request and cached so every shared method enforces the
    // same scope without re-querying. See MarksScopeResolver.
    private static MarksScope CurrentScope()
    {
        HttpContext ctx = HttpContext.Current;
        if (ctx != null && ctx.Items["__marksScope"] is MarksScope)
            return (MarksScope)ctx.Items["__marksScope"];
        MarksScope s = MarksScopeResolver.Resolve();
        if (ctx != null) ctx.Items["__marksScope"] = s;
        return s;
    }

    // SQL predicate restricting <alias>.prog_id to the current user's scope ("" for admin).
    private static string ScopeFilter(string alias) { return CurrentScope().ProgFilter(alias); }

    // True if a registration row (by id) is within the current user's scope. Mutations
    // call this so a user cannot act on records outside their faculty/department.
    private static bool RecordInScope(MySqlConnection conn, int id)
    {
        MarksScope s = CurrentScope();
        if (s.IsAdmin || s.AllowedProgCodes == null) return true;
        string prog = "";
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COALESCE(prog_id,'') FROM campus_dynamics_portal.acad_course_registration WHERE id=@id LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@id", id);
            object v = cmd.ExecuteScalar();
            if (v != null && v != DBNull.Value) prog = v.ToString().Trim();
        }
        return s.AllowedProgCodes.Contains(prog);
    }

    // Transaction-aware variant for use inside the central action processors.
    private static bool RecordInScope(MySqlConnection conn, MySqlTransaction tx, int id)
    {
        MarksScope s = CurrentScope();
        if (s.IsAdmin || s.AllowedProgCodes == null) return true;
        string prog = "";
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COALESCE(prog_id,'') FROM campus_dynamics_portal.acad_course_registration WHERE id=@id LIMIT 1", conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", id);
            object v = cmd.ExecuteScalar();
            if (v != null && v != DBNull.Value) prog = v.ToString().Trim();
        }
        return s.AllowedProgCodes.Contains(prog);
    }

    private const string SCOPE_DENIED = "{\"success\":false,\"message\":\"You can only act on records within your faculty/department.\"}";

    public static void EnsureProvisionalColumns(MySqlConnection conn)
    {
        const string schema = "campus_dynamics_portal";
        const string table = "acad_course_registration";
        try { EnsureNullableColumn(conn, schema, table, "provisional_course_work_marks", "INT NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_exam_marks", "INT NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_total_marks", "INT NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_status", "VARCHAR(20) NULL DEFAULT 'pending'"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_review_comments", "TEXT NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_reviewed_by", "VARCHAR(150) NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_marks_review_date", "DATETIME NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_submitted_by", "VARCHAR(150) NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_published_by", "VARCHAR(150) NULL"); } catch { }
        try { EnsureNullableColumn(conn, schema, table, "provisional_published_date", "DATETIME NULL"); } catch { }
    }

    // Student and course are two different questions — "show me this student" and "show me this
    // paper" — and one box could only ever answer them with an OR, which quietly makes
    // "MRU2024001696" and "BIT2201B" together impossible to ask for. The second box is optional
    // so the three pending/published controllers keep their single field untouched.
    public static void LoadFilters(HttpRequest request, MySqlConnection conn,
        DropDownList ddlYear, DropDownList ddlSemester, DropDownList ddlStatus, DropDownList ddlProg,
        DropDownList ddlLecturer, DropDownList ddlPageSize, TextBox txtSearch, AdminMarksPageKind kind)
    {
        LoadFilters(request, conn, ddlYear, ddlSemester, ddlStatus, ddlProg, ddlLecturer, ddlPageSize, txtSearch, null, kind);
    }

    public static void LoadFilters(HttpRequest request, MySqlConnection conn,
        DropDownList ddlYear, DropDownList ddlSemester, DropDownList ddlStatus, DropDownList ddlProg,
        DropDownList ddlLecturer, DropDownList ddlPageSize, TextBox txtSearch, TextBox txtCourse, AdminMarksPageKind kind)
    {
        ddlYear.Items.Clear();
        ddlYear.Items.Add(new ListItem("All Years", ""));
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT DISTINCT acad_year FROM campus_dynamics_portal.acad_course_registration WHERE acad_year IS NOT NULL AND acad_year <> '' ORDER BY acad_year DESC", conn))
        using (MySqlDataReader rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
                ddlYear.Items.Add(new ListItem(rdr.GetString(0)));
        }

        ddlProg.Items.Clear();
        ddlProg.Items.Add(new ListItem("All Programmes", ""));
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT DISTINCT p.progcode, COALESCE(p.progname, p.progcode) FROM acad_programme p INNER JOIN campus_dynamics_portal.acad_course_registration cr ON cr.prog_id = p.progcode WHERE 1=1" + ScopeFilter("cr") + " ORDER BY 2", conn))
        using (MySqlDataReader rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
                ddlProg.Items.Add(new ListItem(rdr.GetString(1), rdr.GetString(0)));
        }

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
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    string lname = rdr.IsDBNull(1) ? "" : rdr.GetString(1).Trim();
                    if (!string.IsNullOrEmpty(lname))
                        ddlLecturer.Items.Add(new ListItem(lname, rdr.GetString(0)));
                }
            }
        }
        catch { }

        ConfigureStatusDropdown(ddlStatus, kind);

        string qYear = request.QueryString["year"] ?? "";
        string qSem = request.QueryString["sem"] ?? "";
        string qStatus = GetDefaultStatus(kind, request.QueryString["status"] ?? "");
        string qProg = request.QueryString["prog"] ?? "";
        string qLect = request.QueryString["lect"] ?? "";
        string qSearch = request.QueryString["q"] ?? "";
        string qPs = request.QueryString["ps"] ?? "";

        SafeSelect(ddlYear, qYear);
        SafeSelect(ddlSemester, qSem);
        SafeSelect(ddlStatus, qStatus);
        SafeSelect(ddlProg, qProg);
        SafeSelect(ddlLecturer, qLect);
        SafeSelect(ddlPageSize, qPs);
        txtSearch.Text = qSearch;
        if (txtCourse != null) txtCourse.Text = request.QueryString["qc"] ?? "";
    }

    public static void LoadStats(MySqlConnection conn, Literal litStatTotal, Literal litPending, Literal litApproved, Literal litRejected, Literal litPublished, Literal litNotEntered)
    {
        string sql = @"
            SELECT
                COUNT(*) AS cnt_total,
                SUM(CASE WHEN provisional_course_work_marks IS NULL AND provisional_exam_marks IS NULL THEN 1 ELSE 0 END) AS cnt_not_entered,
                SUM(CASE WHEN (provisional_course_work_marks IS NOT NULL OR provisional_exam_marks IS NOT NULL)
                              AND COALESCE(provisional_marks_status,'pending') = 'pending' THEN 1 ELSE 0 END) AS cnt_pending,
                SUM(CASE WHEN provisional_marks_status = 'approved' THEN 1 ELSE 0 END) AS cnt_approved,
                SUM(CASE WHEN provisional_marks_status = 'rejected' THEN 1 ELSE 0 END) AS cnt_rejected,
                SUM(CASE WHEN provisional_marks_status = 'published' THEN 1 ELSE 0 END) AS cnt_published
            FROM campus_dynamics_portal.acad_course_registration cr WHERE 1=1" + ScopeFilter("cr");

        using (MySqlCommand cmd = new MySqlCommand(sql, conn))
        using (MySqlDataReader rdr = cmd.ExecuteReader())
        {
            if (rdr.Read())
            {
                litStatTotal.Text = (rdr.IsDBNull(0) ? 0 : Convert.ToInt32(rdr[0])).ToString();
                litNotEntered.Text = (rdr.IsDBNull(1) ? 0 : Convert.ToInt32(rdr[1])).ToString();
                litPending.Text = (rdr.IsDBNull(2) ? 0 : Convert.ToInt32(rdr[2])).ToString();
                litApproved.Text = (rdr.IsDBNull(3) ? 0 : Convert.ToInt32(rdr[3])).ToString();
                litRejected.Text = (rdr.IsDBNull(4) ? 0 : Convert.ToInt32(rdr[4])).ToString();
                litPublished.Text = (rdr.IsDBNull(5) ? 0 : Convert.ToInt32(rdr[5])).ToString();
            }
        }
    }

    public static void BindGrid(HttpRequest request, MySqlConnection conn,
        DropDownList ddlYear, DropDownList ddlSemester, DropDownList ddlStatus, DropDownList ddlProg,
        DropDownList ddlLecturer, DropDownList ddlPageSize, TextBox txtSearch,
        Literal litRows, Literal litFrom, Literal litTo, Literal litTotal, Literal litTotal2,
        Literal litPage, Literal litPageCount, Literal litPager, Literal litPager2,
        AdminMarksPageKind kind)
    {
        BindGrid(request, conn, ddlYear, ddlSemester, ddlStatus, ddlProg, ddlLecturer, ddlPageSize,
                 txtSearch, null, litRows, litFrom, litTo, litTotal, litTotal2,
                 litPage, litPageCount, litPager, litPager2, kind);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  TRACK - who last touched the coursework or the exam mark.
    //
    //  Read from acad_provisional_marks_audit, which a database trigger writes
    //  on every change to acad_course_registration. The trigger is the only
    //  honest place for it: eleven code paths across eadmin and the portal
    //  write these two columns, and instrumenting eleven call sites is how you
    //  get ten of them right.
    //
    //  The filter is read straight from the query string rather than through a
    //  new control parameter, so the other pages that share BindGrid are
    //  untouched by it.
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nothing when the audit is intact; a warning naming what is missing when it is not.
    ///
    /// The whole trail rests on database triggers. If one is dropped - by a restore, a schema
    /// tool, or somebody tidying - marks stop being recorded and the Track column simply shows
    /// dashes, which looks exactly like "nothing was changed". That is the worst possible
    /// failure for an audit: silent, and indistinguishable from good news. So the page that
    /// depends on it checks, on every load, and says so.
    ///
    /// Fails quiet in the other direction: if the health view itself cannot be read, no scare
    /// banner is shown. An unreadable check is not evidence of a missing guard.
    /// </summary>
    public static string BuildAuditHealthWarning(MySqlConnection conn)
    {
        try
        {
            List<string> missing = new List<string>();
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT guard, protects FROM campus_dynamics_portal.v_marks_audit_health WHERE status <> 'ok'", conn))
            {
                cmd.CommandTimeout = 15;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) missing.Add(r["protects"].ToString() + " (" + r["guard"].ToString() + ")");
            }
            if (missing.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='pm-auditwarn'><div>");
            sb.AppendFormat("<b>Mark tracking is not fully active &mdash; {0} guard{1} missing</b>",
                            missing.Count, missing.Count == 1 ? " is" : "s are");
            // The guards live across two migrations, so the instruction names the folder
            // rather than one file: being told to run the wrong file is worse than being told
            // to run both, and both are safe to re-apply.
            sb.Append("<p>Changes covered by the missing guard are <strong>not being recorded</strong>, and the Track column will show nothing for them. Re-apply <code>CampusDynamics_Portal/sql/marks_audit/</code> (10, then 11, then 12) &mdash; all three are safe to run again.</p><p>");
            for (int i = 0; i < missing.Count; i++)
            {
                if (i > 0) sb.Append(" &middot; ");
                sb.Append(HtmlEnc(missing[i]));
            }
            sb.Append("</p></div></div>");
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    public const string ChangeFilterCw   = "cw";     // coursework changed at least once
    public const string ChangeFilterExam = "exam";   // exam mark changed at least once
    public const string ChangeFilterAny  = "any";    // either
    public const string ChangeFilterNone = "none";   // never changed

    /// <summary>The "mark changes" filter, normalised. Anything unrecognised means no filter.</summary>
    public static string ReadChangeFilter(HttpRequest request)
    {
        string v = request != null ? (request.QueryString["chg"] ?? "").Trim().ToLowerInvariant() : "";
        if (v == ChangeFilterCw || v == ChangeFilterExam || v == ChangeFilterAny || v == ChangeFilterNone) return v;
        return "";
    }

    /// <summary>
    /// The WHERE fragment for that filter. Built from constants only — nothing the caller
    /// typed ever reaches the SQL.
    /// </summary>
    private static string ChangeFilterSql(string chg)
    {
        if (chg == ChangeFilterCw)
            return " AND EXISTS (SELECT 1 FROM campus_dynamics_portal.acad_provisional_marks_audit a WHERE a.reg_id = cr.id AND a.changed_cw = 1)";
        if (chg == ChangeFilterExam)
            return " AND EXISTS (SELECT 1 FROM campus_dynamics_portal.acad_provisional_marks_audit a WHERE a.reg_id = cr.id AND a.changed_exam = 1)";
        if (chg == ChangeFilterAny)
            return " AND EXISTS (SELECT 1 FROM campus_dynamics_portal.acad_provisional_marks_audit a WHERE a.reg_id = cr.id)";
        if (chg == ChangeFilterNone)
            return " AND NOT EXISTS (SELECT 1 FROM campus_dynamics_portal.acad_provisional_marks_audit a WHERE a.reg_id = cr.id)";
        return "";
    }

    public static void BindGrid(HttpRequest request, MySqlConnection conn,
        DropDownList ddlYear, DropDownList ddlSemester, DropDownList ddlStatus, DropDownList ddlProg,
        DropDownList ddlLecturer, DropDownList ddlPageSize, TextBox txtSearch, TextBox txtCourse,
        Literal litRows, Literal litFrom, Literal litTo, Literal litTotal, Literal litTotal2,
        Literal litPage, Literal litPageCount, Literal litPager, Literal litPager2,
        AdminMarksPageKind kind)
    {
        int page = 1;
        if (!string.IsNullOrEmpty(request.QueryString["pg"])) int.TryParse(request.QueryString["pg"], out page);
        if (page < 1) page = 1;

        int pageSize = DefaultPageSize;
        if (!string.IsNullOrEmpty(ddlPageSize.SelectedValue)) int.TryParse(ddlPageSize.SelectedValue, out pageSize);
        if (pageSize < 1) pageSize = DefaultPageSize;

        string year = ddlYear.SelectedValue;
        string sem = ddlSemester.SelectedValue;
        string status = GetEffectiveStatus(kind, ddlStatus.SelectedValue);
        string prog = ddlProg.SelectedValue;
        string lect = ddlLecturer.SelectedValue;
        string search = txtSearch.Text.Trim();
        string courseTerm = txtCourse != null ? txtCourse.Text.Trim() : "";
        string courseCol = GetCourseColumnExpression(conn, "cr");

        if (string.IsNullOrEmpty(courseCol))
        {
            litRows.Text = "<tr><td colspan='" + (kind == AdminMarksPageKind.AllMarks ? 15 : 14) + "' style='padding:16px;color:#b42318;font-size:11px;'>Course column not found on acad_course_registration.</td></tr>";
            litFrom.Text = litTo.Text = litTotal.Text = litTotal2.Text = "0";
            litPage.Text = litPageCount.Text = "1";
            litPager.Text = litPager2.Text = "";
            return;
        }

        bool useStatusParam = false;
        StringBuilder where = BuildGridWhere(kind, status, ref useStatusParam);

        // Role-based scope: dean → faculty, HOD → department, admin → all.
        where.Append(ScopeFilter("cr"));

        if (!string.IsNullOrEmpty(year)) where.Append(" AND cr.acad_year = @year");
        if (!string.IsNullOrEmpty(sem)) where.Append(" AND cr.semester = @sem");
        if (!string.IsNullOrEmpty(prog)) where.Append(" AND cr.prog_id = @prog");
        // When the page supplies a course box, the student box stops matching course codes —
        // otherwise typing a reg number would still pull in courses whose code happened to
        // contain it, and the two boxes could not be combined. Entry number is included
        // because it is what staff read off paper.
        if (!string.IsNullOrEmpty(search))
        {
            if (txtCourse != null)
                where.Append(" AND (cr.regno LIKE @q OR COALESCE(s.entryno,'') LIKE @q OR TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @q)");
            else
                where.Append(" AND (cr.regno LIKE @q OR " + courseCol + " LIKE @q OR TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) LIKE @q)");
        }
        if (!string.IsNullOrEmpty(courseTerm))
            where.Append(" AND (" + courseCol + " LIKE @qc OR COALESCE(c.courseName,'') LIKE @qc)");
        if (!string.IsNullOrEmpty(lect))
            where.Append(" AND EXISTS (SELECT 1 FROM acad_programmecourses pc2 WHERE pc2.lecturer_id = @lect AND pc2.course_code = " + courseCol + " AND pc2.progcode = cr.prog_id)");
        // Sits beside the lecturer filter on purpose: together they answer "which of
        // this lecturer's marks were changed after the fact, and by whom".
        where.Append(ChangeFilterSql(ReadChangeFilter(request)));

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
        using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) " + joins + " " + where.ToString(), conn))
        {
            AddParams(cmd, year, sem, prog, status, useStatusParam, lect, search, courseTerm);
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
                   END AS prov_status,
                   (SELECT CONCAT_WS('~|~', a.performed_by,
                                     DATE_FORMAT(a.created_at,'%Y-%m-%d %H:%i'),
                                     a.changed_cw, a.changed_exam, a.action_type,
                                     IFNULL(a.old_cw,''), IFNULL(a.new_cw,''),
                                     IFNULL(a.old_exam,''), IFNULL(a.new_exam,''))
                      FROM campus_dynamics_portal.acad_provisional_marks_audit a
                     WHERE a.reg_id = cr.id
                     ORDER BY a.id DESC LIMIT 1) AS last_change,
                   (SELECT COUNT(*) FROM campus_dynamics_portal.acad_provisional_marks_audit a2
                     WHERE a2.reg_id = cr.id) AS change_count
            " + joins + " " + where.ToString() + @"
            ORDER BY cr.id DESC
            LIMIT @offset, @pageSize";

        StringBuilder sb = new StringBuilder();
        using (MySqlCommand cmd = new MySqlCommand(dataSql, conn))
        {
            AddParams(cmd, year, sem, prog, status, useStatusParam, lect, search, courseTerm);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@pageSize", pageSize);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
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
                    string lastChange = rdr.IsDBNull(rdr.GetOrdinal("last_change")) ? null : rdr["last_change"].ToString();
                    int changeCount = rdr.IsDBNull(rdr.GetOrdinal("change_count")) ? 0 : Convert.ToInt32(rdr["change_count"]);

                    string pillCss = "pm-pill--pending";
                    string rowCss = "row--pending";
                    switch (provStatus)
                    {
                        case StatusApproved: pillCss = "pm-pill--approved"; rowCss = "row--approved"; break;
                        case StatusRejected: pillCss = "pm-pill--rejected"; rowCss = "row--rejected"; break;
                        case StatusPublished: pillCss = "pm-pill--published"; rowCss = "row--published"; break;
                        case StatusNotEntered: pillCss = "pm-pill--not_entered"; rowCss = string.Empty; break;
                    }

                    Func<string, string> markCell = delegate(string v)
                    {
                        return v != null ? "<span class='pm-mark'>" + HtmlEnc(v) + "</span>" : "<span class='pm-mark pm-mark--na'>&mdash;</span>";
                    };

                    sb.AppendFormat("<tr class='{0}'>", rowCss);
                    sb.AppendFormat("<td class='col-sel pm-center'><input type='checkbox' class='pm-row-chk pm-row-sel' value='{0}' title='Select for batch action' /></td>", id);
                    sb.AppendFormat("<td class='col-regno' title='{0}'><span class='pm-code pm-ellipsis'>{0}</span></td>", HtmlEnc(regno));
                    sb.AppendFormat("<td class='col-student' title='{0}'><span class='pm-ellipsis'>{0}</span></td>", HtmlEnc(studentName));
                    // Code on top, title beneath it in small muted type. The name was only ever a
                    // tooltip, which is invisible on a phone and useless when scanning a list —
                    // a row could not be read without already knowing what the code meant.
                    // Suppressed when the name is just the code repeated (no catalogue entry).
                    string courseNameLine =
                        (!string.IsNullOrEmpty(courseName) &&
                         !string.Equals(courseName.Trim(), (courseID ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                            ? "<span class='pm-subname'>" + HtmlEnc(courseName) + "</span>" : "";
                    sb.AppendFormat("<td class='col-course' title='{0} — {1}'><span class='pm-code pm-ellipsis'>{0}</span>{2}{3}</td>",
                        HtmlEnc(courseID), HtmlEnc(courseName), rtBadge, courseNameLine);
                    sb.AppendFormat("<td class='col-prog pm-muted'><span class='pm-ellipsis'>{0}</span></td>", HtmlEnc(progId));
                    sb.AppendFormat("<td class='col-yr pm-muted'>{0}</td>", HtmlEnc(acadYear));
                    sb.AppendFormat("<td class='col-sem pm-muted'><strong>Yr {0}, Sem {1}</strong></td>", HtmlEnc(studyYear), HtmlEnc(semester));
                    sb.AppendFormat("<td class='col-mark pm-center'>{0}</td>", markCell(cwMarks));
                    sb.AppendFormat("<td class='col-mark pm-center'>{0}</td>", markCell(exMarks));
                    sb.AppendFormat("<td class='col-mark pm-center'>{0}</td>", markCell(totMarks));
                    sb.AppendFormat("<td class='col-pub pm-center'><span class='pm-mark'>{0}</span></td>", HtmlEnc(pubMark));
                    sb.AppendFormat("<td class='col-grade pm-center'><span class='pm-mark'>{0}</span></td>", HtmlEnc(pubGrade));
                    sb.AppendFormat("<td class='col-status pm-center'><span class='pm-pill {0}'>{1}</span></td>", pillCss, HtmlEnc(provStatus.Replace("_", " ")));
                    // Only AllMarks carries a Track column in its header. BindGrid is shared with
                    // three other screens whose tables are fourteen columns wide, and emitting a
                    // fifteenth cell into those would shift every row out of line with its header.
                    if (kind == AdminMarksPageKind.AllMarks)
                        sb.AppendFormat("<td class='col-track'>{0}</td>", BuildTrackCell(lastChange, changeCount));
                                        sb.AppendFormat(@"<td class='col-act pm-center'>
  <div class='pm-row-wrap'>
    <button type='button' class='pm-row-trigger' onclick='toggleRowMenu(this)' title='Actions' aria-label='Open row actions'>&#8942;</button>
        <div class='pm-row-menu'>{1}</div>
  </div>
</td></tr>", id, BuildRowActions(kind, id));
                }
            }
        }

        if (sb.Length == 0)
            sb.AppendFormat("<tr><td colspan='{0}' style='padding:14px;text-align:center;color:#6b7280;font-size:11px;'>No records found for the selected filters.</td></tr>",
                            kind == AdminMarksPageKind.AllMarks ? 15 : 14);

        litRows.Text = sb.ToString();
        int displayFrom = total == 0 ? 0 : offset + 1;
        int displayTo = Math.Min(offset + pageSize, total);
        litFrom.Text = displayFrom.ToString();
        litTo.Text = displayTo.ToString();
        litTotal.Text = total.ToString();
        litTotal2.Text = total.ToString();
        litPage.Text = page.ToString();
        litPageCount.Text = pageCount.ToString();
        string pagerHtml = BuildPager(page, pageCount, year, sem, prog, status, lect, search, pageSize.ToString(), courseTerm);
        litPager.Text = pagerHtml;
        litPager2.Text = pagerHtml;
    }

    public static string GetProvisionalRecord(int id)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                if (!RecordInScope(conn, id)) return SCOPE_DENIED;
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
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                            return js.Serialize(new { success = false, message = "Record not found." });

                        Dictionary<string, object> record = new Dictionary<string, object>();
                        record["id"] = Convert.ToInt32(rdr["id"]);
                        record["regno"] = rdr["regno"].ToString();
                        record["courseID"] = rdr["courseID"].ToString();
                        record["prog_id"] = rdr["prog_id"].ToString();
                        record["study_year"] = rdr["study_year"].ToString();
                        record["acad_year"] = rdr["acad_year"].ToString();
                        record["semester"] = rdr["semester"].ToString();
                        record["student_name"] = rdr["student_name"].ToString().Trim();
                        record["provisional_course_work_marks"] = rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? null : (object)Convert.ToInt32(rdr["provisional_course_work_marks"]);
                        record["provisional_exam_marks"] = rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks")) ? null : (object)Convert.ToInt32(rdr["provisional_exam_marks"]);
                        record["provisional_total_marks"] = rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks")) ? null : (object)Convert.ToInt32(rdr["provisional_total_marks"]);
                        record["provisional_marks_status"] = rdr["provisional_marks_status"].ToString();
                        record["provisional_marks_review_comments"] = rdr["provisional_marks_review_comments"].ToString();
                        record["provisional_marks_reviewed_by"] = rdr["provisional_marks_reviewed_by"].ToString();
                        record["submitted_by"] = rdr["submitted_by"].ToString();
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

    public static string GetRecordDetails(int id)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                if (!RecordInScope(conn, id)) return SCOPE_DENIED;
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

                // Declared outside the reader: the history needs another command on this
                // same connection, which cannot run while a reader is open.
                Dictionary<string, object> record = new Dictionary<string, object>();

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                            return js.Serialize(new { success = false, message = "Record not found." });

                        record["id"] = Convert.ToInt32(rdr["id"]);
                        record["regno"] = rdr["regno"].ToString();
                        record["student_name"] = rdr["student_name"].ToString().Trim();
                        record["courseID"] = rdr["courseID"].ToString();
                        record["course_name"] = rdr["course_name"].ToString();
                        record["prog_id"] = rdr["prog_id"].ToString();
                        record["study_year"] = rdr["study_year"].ToString();
                        record["acad_year"] = rdr["acad_year"].ToString();
                        record["semester"] = rdr["semester"].ToString();
                        record["lecturer_name"] = rdr["lecturer_name"].ToString().Trim();
                        record["submitted_by"] = rdr["submitted_by"].ToString();
                        record["provisional_course_work_marks"] = rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? null : (object)Convert.ToInt32(rdr["provisional_course_work_marks"]);
                        record["provisional_exam_marks"] = rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks")) ? null : (object)Convert.ToInt32(rdr["provisional_exam_marks"]);
                        record["provisional_total_marks"] = rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks")) ? null : (object)Convert.ToInt32(rdr["provisional_total_marks"]);
                        record["provisional_marks_status"] = rdr["provisional_marks_status"].ToString();
                        record["provisional_marks_review_comments"] = rdr["provisional_marks_review_comments"].ToString();
                        record["provisional_marks_reviewed_by"] = rdr["provisional_marks_reviewed_by"].ToString();
                        record["provisional_marks_review_date"] = rdr["provisional_marks_review_date"].ToString();
                        record["provisional_published_by"] = rdr["provisional_published_by"].ToString();
                        record["provisional_published_date"] = rdr["provisional_published_date"].ToString();
                    }
                }

                record["history"] = GetMarkHistory(conn, id,
                    record.ContainsKey("regno") ? (string)record["regno"] : "",
                    record.ContainsKey("courseID") ? (string)record["courseID"] : "",
                    record.ContainsKey("acad_year") ? (string)record["acad_year"] : "",
                    record.ContainsKey("semester") ? (string)record["semester"] : "");

                return js.Serialize(new { success = true, record = record });
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    /// <summary>
    /// Every recorded change to this registration's coursework and exam marks, newest first.
    ///
    /// Matched on the registration id AND on the natural key, because several hundred entries
    /// recovered from the old activity log describe registrations that no longer carry the same
    /// row id. Dropping those would quietly shorten the history of exactly the oldest records,
    /// which is where a trail is most often wanted.
    /// </summary>
    private static List<Dictionary<string, object>> GetMarkHistory(
        MySqlConnection conn, int id, string regno, string courseId, string acadYear, string semester)
    {
        List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
        const string sql = @"
            SELECT a.id, a.action_type, a.changed_cw, a.changed_exam,
                   a.old_cw, a.new_cw, a.old_exam, a.new_exam,
                   a.old_total, a.new_total, a.old_status, a.new_status,
                   a.performed_by, a.source_page, a.change_reason, a.ip_address,
                   DATE_FORMAT(a.created_at, '%Y-%m-%d %H:%i:%s') AS at_
              FROM campus_dynamics_portal.acad_provisional_marks_audit a
             WHERE a.reg_id = @id
                OR (@rg <> '' AND a.regno = @rg AND a.course_id = @cs
                    AND a.acad_year = @ay AND a.semester = @sm)
             ORDER BY a.created_at DESC, a.id DESC
             LIMIT 200";
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@rg", regno ?? "");
                cmd.Parameters.AddWithValue("@cs", courseId ?? "");
                cmd.Parameters.AddWithValue("@ay", acadYear ?? "");
                cmd.Parameters.AddWithValue("@sm", semester ?? "");
                using (MySqlDataReader r = cmd.ExecuteReader())
                {
                    Dictionary<long, bool> seen = new Dictionary<long, bool>();
                    while (r.Read())
                    {
                        long rid = Convert.ToInt64(r["id"]);
                        if (seen.ContainsKey(rid)) continue;      // the OR can match one row twice
                        seen[rid] = true;

                        Dictionary<string, object> h = new Dictionary<string, object>();
                        h["at"] = r["at_"].ToString();
                        h["action"] = r["action_type"].ToString();
                        h["by"] = r["performed_by"].ToString();
                        h["source"] = Str(r, "source_page");
                        h["reason"] = Str(r, "change_reason");
                        h["ip"] = Str(r, "ip_address");
                        h["cw"] = Convert.ToInt32(r["changed_cw"]) == 1;
                        h["ex"] = Convert.ToInt32(r["changed_exam"]) == 1;
                        h["oldCw"] = NumOrNull(r, "old_cw");
                        h["newCw"] = NumOrNull(r, "new_cw");
                        h["oldExam"] = NumOrNull(r, "old_exam");
                        h["newExam"] = NumOrNull(r, "new_exam");
                        h["oldTotal"] = NumOrNull(r, "old_total");
                        h["newTotal"] = NumOrNull(r, "new_total");
                        h["oldStatus"] = Str(r, "old_status");
                        h["newStatus"] = Str(r, "new_status");
                        list.Add(h);
                    }
                }
            }
        }
        catch
        {
            // A history that will not read must never stop the details opening. The panel
            // says so for itself when the list comes back empty.
        }
        return list;
    }

    private static object NumOrNull(MySqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : (object)Convert.ToInt32(r[i]);
    }

    private static string Str(MySqlDataReader r, string col)
    {
        int i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? "" : r[i].ToString();
    }

    public static string ReviewProvisionalMarks(int id, string action, string comment)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();
        string normalizedAction = NormalizeStatus(action);

        if (normalizedAction != StatusRejected && normalizedAction != StatusPublished)
            return js.Serialize(new { success = false, message = "Invalid action." });
        if (normalizedAction == StatusRejected && string.IsNullOrWhiteSpace(comment))
            return js.Serialize(new { success = false, message = "Comment required for rejection." });

        try
        {
            string reviewer = MarksAuthorizationService.GetCurrentUser();
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    ProvisionalActionResult result = ProcessProvisionalAction(conn, tx, id, normalizedAction, reviewer, comment);
                    if (!result.Success)
                    {
                        tx.Rollback();
                        return js.Serialize(new { success = false, message = result.Message });
                    }

                    tx.Commit();
                    return js.Serialize(new { success = true, message = result.Message });
                }
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    public static string PublishMarks(int id)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();

        try
        {
            string publisher = MarksAuthorizationService.GetCurrentUser();
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    ProvisionalActionResult result = ProcessProvisionalAction(conn, tx, id, StatusPublished, publisher, "");
                    if (!result.Success)
                    {
                        tx.Rollback();
                        return js.Serialize(new { success = false, message = result.Message });
                    }

                    tx.Commit();
                    return js.Serialize(new
                    {
                        success = true,
                        message = result.Message,
                        semesterGpa = result.SemesterGpa.ToString("F2"),
                        cgpa = result.Cgpa.ToString("F2"),
                        awardClass = result.AwardClass
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    /// <summary>
    /// Reverse a published record back to pending state and remove corresponding final-result row.
    /// The operation also regenerates semester GPA and cumulative CGPA within the same transaction.
    /// </summary>
    public static string UnpublishMark(int id, string comment)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();

        try
        {
            string actor = MarksAuthorizationService.GetCurrentUser();
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    ProvisionalActionResult result = ProcessUnpublishAction(conn, tx, id, actor, comment);
                    if (!result.Success)
                    {
                        tx.Rollback();
                        return js.Serialize(new { success = false, message = result.Message });
                    }

                    tx.Commit();
                    return js.Serialize(new
                    {
                        success = true,
                        message = result.Message,
                        semesterGpa = result.SemesterGpa.ToString("F2"),
                        cgpa = result.Cgpa.ToString("F2"),
                        awardClass = result.AwardClass
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    /// <summary>
    /// Re-run publish pipeline for a published record to regenerate score/grade/GPA/CGPA consistently.
    /// </summary>
    public static string ReprocessPublishedMark(int id)
    {
        return PublishMarks(id);
    }

    /// <summary>
    /// Publish a single provisional row to acad_results within an existing transaction.
    /// Used by the staged workflow's PUBLISH stage (StageAdvanceService) so final
    /// results are produced by the exact same engine as the legacy publish path.
    /// </summary>
    public static bool PublishSingle(MySqlConnection conn, MySqlTransaction tx, int id, string actor)
    {
        ProvisionalActionResult r = ProcessProvisionalAction(conn, tx, id, StatusPublished, actor, "");
        return r != null && r.Success;
    }

    /// <summary>
    /// Batch variant: publishes one row but defers the semester-GPA rewrite into
    /// <paramref name="deferGpa"/> so the whole batch can settle GPAs once per
    /// student/semester at the end. Also reports WHY a row was skipped, which the old
    /// bool-only signature threw away — the publish console now surfaces those reasons
    /// instead of silently reporting a smaller number than the preview promised.
    /// </summary>
    public static bool PublishSingle(MySqlConnection conn, MySqlTransaction tx, int id, string actor,
                                     MarksGpaDeferral deferGpa, out string reason)
    {
        ProvisionalActionResult r = ProcessProvisionalAction(conn, tx, id, StatusPublished, actor, "", deferGpa);
        reason = (r == null) ? "Unknown error" : r.Message;
        return r != null && r.Success;
    }

    /// <summary>
    /// Resolve every schema-shape constant the publish path needs, and run any one-off
    /// self-heal DDL, BEFORE a batch opens its first transaction. ALTER TABLE implicitly
    /// commits in MySQL, so this must never happen inside the row loop. Returns the
    /// acad_results credit-units column name for the caller's GPA flush.
    /// </summary>
    public static string PrepareForBatchPublish(MySqlConnection conn)
    {
        GetCourseColumnExpression(conn, "cr");
        ResolveCourseCreditUnitsColumn(conn);
        string resultsCreditCol = ResolveResultsCreditUnitsColumn(conn);
        EnsureResultCommentTextNullable(conn);
        return string.IsNullOrEmpty(resultsCreditCol) ? "CreditUnits" : resultsCreditCol;
    }

    public static string SaveAdminMarks(int id, int? cw, int? exam, int? total, string note)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();
        try
        {
            if (cw == null && exam == null && total == null)
                return js.Serialize(new { success = false, message = "At least one mark value is required." });

            // Total rule, enforced here as well as in the browser: when BOTH course work and
            // exam are present the total is their sum, unless a block figure was deliberately
            // typed. The client only sends a total when the operator overrode it — a total it
            // merely displayed is sent as null — so a value arriving here is an override and is
            // honoured. Recomputing server-side matters because the browser is not the only
            // caller and a stale total must never be able to overwrite a corrected mark.
            int? computedTotal = total;
            if (computedTotal == null && cw != null && exam != null)
                computedTotal = cw.Value + exam.Value;

            if (cw != null && (cw.Value < 0 || cw.Value > 100))
                return js.Serialize(new { success = false, message = "Course work must be between 0 and 100." });
            if (exam != null && (exam.Value < 0 || exam.Value > 100))
                return js.Serialize(new { success = false, message = "Exam marks must be between 0 and 100." });
            if (computedTotal != null && (computedTotal.Value < 0 || computedTotal.Value > 100))
                return js.Serialize(new { success = false,
                    message = "Total works out to " + computedTotal.Value + ", which is outside 0–100. Check the course work and exam marks." });

            string actor = MarksAuthorizationService.GetCurrentUser();
            string reviewComment = string.IsNullOrWhiteSpace(note) ? "Admin mark override" : note.Trim();

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                if (!RecordInScope(conn, id)) return SCOPE_DENIED;

                bool wasPublished = IsPublished(conn, id);

                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    // Editing a PUBLISHED mark used to change the provisional figures and set the
                    // status back to pending while leaving the old value live in acad_results —
                    // so the student's statement kept showing the mark that had just been
                    // corrected. The published result is withdrawn first, in the same
                    // transaction, which also recomputes the semester GPA and CGPA.
                    if (wasPublished)
                    {
                        ProvisionalActionResult un = ProcessUnpublishAction(conn, tx, id, actor,
                            "Withdrawn for an admin mark correction" + (string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim()));
                        if (!un.Success) { tx.Rollback(); return js.Serialize(new { success = false, message = un.Message }); }
                    }

                    // Name the actor for the audit trigger BEFORE the write, on this same
                    // connection. Without it the trigger records the change honestly but
                    // anonymously, as 'system'.
                    SetMarkAuditContext(conn, tx, actor, "AllMarks:edit-marks",
                        string.IsNullOrWhiteSpace(note) ? "Admin mark correction" : note.Trim());

                    string sql = @"UPDATE campus_dynamics_portal.acad_course_registration
                                   SET provisional_course_work_marks = @cw,
                                       provisional_exam_marks = @exam,
                                       provisional_total_marks = @total,
                                       provisional_marks_status = @pending,
                                       provisional_marks_reviewed_by = @actor,
                                       provisional_marks_review_comments = @comment,
                                       provisional_marks_review_date = NOW()
                                   WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@cw", (object)cw ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@exam", (object)exam ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@total", (object)computedTotal ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@pending", StatusPending);
                        cmd.Parameters.AddWithValue("@actor", actor);
                        cmd.Parameters.AddWithValue("@comment", reviewComment);
                        cmd.Parameters.AddWithValue("@id", id);
                        if (cmd.ExecuteNonQuery() == 0)
                        { tx.Rollback(); return js.Serialize(new { success = false, message = "Record not found." }); }
                    }

                    // Both components now present → the mark is ENTERED and ready for the HOD;
                    // otherwise it drops back to NOT_ENTERED. Derived, never assumed.
                    MarkStageSync.Sync(conn, tx, id, actor, reviewComment);
                    tx.Commit();
                }

                return js.Serialize(new
                {
                    success = true,
                    message = wasPublished
                        ? "Marks saved. The published result was withdrawn and the GPA recomputed — the record must be taken back through capture, approval and publishing."
                        : "Marks saved. Status reset to pending for re-review."
                });
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    public static string ResetToPending(int id)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();
        try
        {
            string actor = MarksAuthorizationService.GetCurrentUser();
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                if (!RecordInScope(conn, id)) return SCOPE_DENIED;

                // Resetting a PUBLISHED record has to withdraw the published result too, or the
                // student keeps seeing a mark the console has just sent back to the start.
                if (IsPublished(conn, id))
                    return UnpublishMark(id, "Reset to pending by " + actor);

                string sql = @"UPDATE campus_dynamics_portal.acad_course_registration
                               SET provisional_marks_status = @pending,
                                   provisional_marks_review_comments = CONCAT(COALESCE(provisional_marks_review_comments,''), ' [Reset by admin: ', @actor, ']'),
                                   provisional_marks_reviewed_by = @actor,
                                   provisional_marks_review_date = NOW()
                               WHERE id = @id";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@pending", StatusPending);
                    cmd.Parameters.AddWithValue("@actor", actor);
                    cmd.Parameters.AddWithValue("@id", id);
                    if (cmd.ExecuteNonQuery() == 0)
                        return js.Serialize(new { success = false, message = "Record not found." });
                }
                MarkStageSync.Sync(conn, id, actor, "");
            }
            return js.Serialize(new { success = true, message = "Record reset to pending — it re-enters the journey at the lecturer's stage." });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    /// <summary>True when the record currently carries a published provisional status.</summary>
    private static bool IsPublished(MySqlConnection conn, int id)
    {
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT LOWER(COALESCE(provisional_marks_status,'')) FROM campus_dynamics_portal.acad_course_registration WHERE id=@id LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                object v = cmd.ExecuteScalar();
                return v != null && v != DBNull.Value && string.Equals(v.ToString(), StatusPublished, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { return false; }
    }

    /// <summary>
    /// Force-set a single provisional record to any status.
    /// 'published' → full pipeline (writes to acad_results, recomputes GPA).
    /// 'approved' / 'rejected' / 'pending' → direct status UPDATE only.
    /// </summary>
    public static string ForceSetStatus(int id, string newStatus, string comment)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();
        string normalizedStatus = NormalizeStatus(newStatus);

        if (normalizedStatus != StatusPending && normalizedStatus != StatusApproved &&
            normalizedStatus != StatusRejected && normalizedStatus != StatusPublished)
            return js.Serialize(new { success = false, message = "Invalid status. Choose: pending, approved, rejected, or published." });
        if (normalizedStatus == StatusRejected && string.IsNullOrWhiteSpace(comment))
            return js.Serialize(new { success = false, message = "A comment is required when setting status to Rejected." });

        try
        {
            string actor = MarksAuthorizationService.GetCurrentUser();

            if (normalizedStatus == StatusPublished)
            {
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    using (MySqlTransaction tx = conn.BeginTransaction())
                    {
                        ProvisionalActionResult result = ProcessProvisionalAction(conn, tx, id, StatusPublished, actor, "");
                        if (!result.Success) { tx.Rollback(); return js.Serialize(new { success = false, message = result.Message }); }
                        tx.Commit();
                        return js.Serialize(new { success = true, message = result.Message });
                    }
                }
            }

            // pending / approved / rejected — direct UPDATE
            string sql;
            if (normalizedStatus == StatusPending)
                sql = @"UPDATE campus_dynamics_portal.acad_course_registration
                        SET provisional_marks_status           = @status,
                            provisional_marks_reviewed_by      = @actor,
                            provisional_marks_review_date      = NOW(),
                            provisional_marks_review_comments  = CONCAT(COALESCE(provisional_marks_review_comments,''), ' [Forced to pending by ', @actor, CASE WHEN @comment<>'' THEN CONCAT(': ',@comment) ELSE '' END, ']')
                        WHERE id = @id";
            else if (normalizedStatus == StatusApproved)
                sql = @"UPDATE campus_dynamics_portal.acad_course_registration
                        SET provisional_marks_status           = @status,
                            provisional_marks_reviewed_by      = @actor,
                            provisional_marks_review_date      = NOW(),
                            provisional_marks_review_comments  = CASE WHEN @comment<>'' THEN @comment ELSE COALESCE(provisional_marks_review_comments,'') END
                        WHERE id = @id";
            else // rejected
                sql = @"UPDATE campus_dynamics_portal.acad_course_registration
                        SET provisional_marks_status           = @status,
                            provisional_marks_reviewed_by      = @actor,
                            provisional_marks_review_date      = NOW(),
                            provisional_marks_review_comments  = @comment
                        WHERE id = @id";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                if (!RecordInScope(conn, id)) return SCOPE_DENIED;
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@status",  normalizedStatus);
                    cmd.Parameters.AddWithValue("@actor",   actor);
                    cmd.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(comment) ? "" : comment.Trim());
                    cmd.Parameters.AddWithValue("@id",      id);
                    if (cmd.ExecuteNonQuery() == 0)
                        return js.Serialize(new { success = false, message = "Record not found." });
                }
                // Status moved OFF 'published' → clear the stale publication stamps so the
                // record's fields stay consistent with its new status (cascade completeness).
                using (MySqlCommand clr = new MySqlCommand(
                    "UPDATE campus_dynamics_portal.acad_course_registration " +
                    "SET provisional_published_by=NULL, provisional_published_date=NULL WHERE id=@id", conn))
                { clr.Parameters.AddWithValue("@id", id); clr.ExecuteNonQuery(); }

                // …and bring the staged journey with it. Without this the stage consoles and the
                // student's Mark Status Check keep reporting the stage this record has just been
                // taken back out of.
                MarkStageSync.Sync(conn, id, actor, comment);
            }
            return js.Serialize(new { success = true, message = "Status updated to " + normalizedStatus + "." });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    /// <summary>Bulk version of ForceSetStatus.</summary>
    public static string BulkForceSetStatus(int[] ids, string newStatus, string comment)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();

        if (ids == null || ids.Length == 0)
            return js.Serialize(new { success = false, message = "No records supplied." });

        string normalizedStatus = NormalizeStatus(newStatus);
        if (normalizedStatus != StatusPending && normalizedStatus != StatusApproved &&
            normalizedStatus != StatusRejected && normalizedStatus != StatusPublished)
            return js.Serialize(new { success = false, message = "Invalid status." });
        if (normalizedStatus == StatusRejected && string.IsNullOrWhiteSpace(comment))
            return js.Serialize(new { success = false, message = "Comment required when setting status to Rejected." });

        try
        {
            string actor = MarksAuthorizationService.GetCurrentUser();
            int affected = 0, failed = 0;
            string firstError = string.Empty;

            // Build the direct-update SQL once (reused for all non-publish statuses)
            string directSql = string.Empty;
            if (normalizedStatus == StatusPending)
                directSql = @"UPDATE campus_dynamics_portal.acad_course_registration
                              SET provisional_marks_status           = @status,
                                  provisional_marks_reviewed_by      = @actor,
                                  provisional_marks_review_date      = NOW(),
                                  provisional_marks_review_comments  = CONCAT(COALESCE(provisional_marks_review_comments,''), ' [Bulk forced to pending by ', @actor, ']')
                              WHERE id = @id";
            else if (normalizedStatus == StatusApproved)
                directSql = @"UPDATE campus_dynamics_portal.acad_course_registration
                              SET provisional_marks_status           = @status,
                                  provisional_marks_reviewed_by      = @actor,
                                  provisional_marks_review_date      = NOW(),
                                  provisional_marks_review_comments  = CASE WHEN @comment<>'' THEN @comment ELSE COALESCE(provisional_marks_review_comments,'') END
                              WHERE id = @id";
            else if (normalizedStatus == StatusRejected)
                directSql = @"UPDATE campus_dynamics_portal.acad_course_registration
                              SET provisional_marks_status           = @status,
                                  provisional_marks_reviewed_by      = @actor,
                                  provisional_marks_review_date      = NOW(),
                                  provisional_marks_review_comments  = @comment
                              WHERE id = @id";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                foreach (int id in ids)
                {
                    if (id <= 0) continue;
                    if (!RecordInScope(conn, id)) { failed++; if (string.IsNullOrEmpty(firstError)) firstError = "Some records are outside your faculty/department."; continue; }
                    using (MySqlTransaction tx = conn.BeginTransaction())
                    {
                        try
                        {
                            if (normalizedStatus == StatusPublished)
                            {
                                ProvisionalActionResult r = ProcessProvisionalAction(conn, tx, id, StatusPublished, actor, "");
                                if (r.Success) { tx.Commit(); affected++; }
                                else { tx.Rollback(); failed++; if (string.IsNullOrEmpty(firstError)) firstError = r.Message; }
                            }
                            else
                            {
                                using (MySqlCommand cmd = new MySqlCommand(directSql, conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@status",  normalizedStatus);
                                    cmd.Parameters.AddWithValue("@actor",   actor);
                                    cmd.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(comment) ? "" : comment.Trim());
                                    cmd.Parameters.AddWithValue("@id",      id);
                                    cmd.ExecuteNonQuery();
                                }
                                // Same transaction, so the stage can never be committed apart
                                // from the status it is derived from.
                                MarkStageSync.Sync(conn, tx, id, actor, comment);
                                tx.Commit();
                                affected++;
                            }
                        }
                        catch (Exception ex)
                        {
                            try { tx.Rollback(); } catch { }
                            failed++;
                            if (string.IsNullOrEmpty(firstError)) firstError = ex.Message;
                        }
                    }
                }
            }

            string msg = string.Format("{0} record(s) set to {1}.", affected, normalizedStatus);
            if (failed > 0) msg += string.Format(" {0} skipped (errors).", failed);
            if (!string.IsNullOrEmpty(firstError)) msg += " First error: " + firstError;
            return js.Serialize(new { success = affected > 0, message = msg, affected = affected, failed = failed });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    public static string BulkAction(int[] ids, string action, string comment)
    {
        string connStr = ConnectionString;
        JavaScriptSerializer js = new JavaScriptSerializer();

        if (ids == null || ids.Length == 0)
            return js.Serialize(new { success = false, message = "No records supplied." });

        string normalizedAction = NormalizeStatus(action);
        if (normalizedAction != StatusApproved && normalizedAction != StatusRejected &&
            normalizedAction != StatusPublished && normalizedAction != ActionUnpublish && normalizedAction != ActionReprocess)
            return js.Serialize(new { success = false, message = "Invalid action." });
        if (normalizedAction == StatusRejected && string.IsNullOrWhiteSpace(comment))
            return js.Serialize(new { success = false, message = "Comment required for bulk rejection." });

        try
        {
            string actor = MarksAuthorizationService.GetCurrentUser();
            int affected = 0;
            int failed = 0;
            string firstError = string.Empty;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();

                for (int i = 0; i < ids.Length; i++)
                {
                    if (ids[i] <= 0) continue;
                    if (!RecordInScope(conn, ids[i])) { failed++; if (string.IsNullOrEmpty(firstError)) firstError = "Some records are outside your faculty/department."; continue; }
                    using (MySqlTransaction tx = conn.BeginTransaction())
                    {
                        ProvisionalActionResult result;
                        if (normalizedAction == ActionUnpublish)
                            result = ProcessUnpublishAction(conn, tx, ids[i], actor, comment);
                        else if (normalizedAction == ActionReprocess)
                            result = ProcessProvisionalAction(conn, tx, ids[i], StatusPublished, actor, comment);
                        else if (normalizedAction == StatusApproved)
                        {
                            // approved is a simple status stamp — not handled by ProcessProvisionalAction
                            using (MySqlCommand cmd = new MySqlCommand(@"
                                UPDATE campus_dynamics_portal.acad_course_registration
                                SET provisional_marks_status      = 'approved',
                                    provisional_marks_reviewed_by = @actor,
                                    provisional_marks_review_date = NOW(),
                                    provisional_marks_review_comments = CASE WHEN @comment<>'' THEN @comment ELSE COALESCE(provisional_marks_review_comments,'') END
                                WHERE id = @id", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@actor",   actor);
                                cmd.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(comment) ? "" : comment.Trim());
                                cmd.Parameters.AddWithValue("@id",      ids[i]);
                                cmd.ExecuteNonQuery();
                            }
                            tx.Commit();
                            affected++;
                            continue;
                        }
                        else
                            result = ProcessProvisionalAction(conn, tx, ids[i], normalizedAction, actor, comment);
                        if (result.Success)
                        {
                            tx.Commit();
                            affected++;
                        }
                        else
                        {
                            tx.Rollback();
                            failed++;
                            if (string.IsNullOrEmpty(firstError)) firstError = result.Message;
                        }
                    }
                }
            }

            string msg = affected + " record(s) " + normalizedAction + " successfully.";
            if (failed > 0)
                msg += " " + failed + " failed." + (string.IsNullOrEmpty(firstError) ? "" : " First error: " + firstError);

            return js.Serialize(new { success = affected > 0, message = msg, affected = affected, failed = failed });
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    public static string PreviewBatchWorkflow(string action, string scope, int[] ids, string year, string sem, string prog, AdminMarksPageKind kind)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        string connStr = ConnectionString;
        string normalizedAction = NormalizeStatus(action);
        string normalizedScope = NormalizeStatus(scope);

        if (normalizedAction != StatusPublished)
            return js.Serialize(new { success = false, message = "Invalid workflow action." });
        if (normalizedScope != "selected" && normalizedScope != "programme")
            return js.Serialize(new { success = false, message = "Invalid workflow scope." });

        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                string courseCol = GetCourseColumnExpression(conn, "cr");
                if (string.IsNullOrEmpty(courseCol))
                    return js.Serialize(new { success = false, message = "Course column not found on acad_course_registration." });

                List<int> targetIds = ResolveWorkflowTargetIds(conn, normalizedAction, normalizedScope, ids, year, sem, prog, kind);
                if (targetIds.Count == 0)
                    return js.Serialize(new { success = true, count = 0, rows = new object[0], message = "No eligible records found for this workflow." });

                string idList = string.Join(",", targetIds.ToArray());
                string previewSql = @"
                    SELECT cr.id, cr.regno, " + courseCol + @" AS courseID, COALESCE(cr.prog_id,'') AS prog_id,
                           cr.acad_year, cr.semester,
                           COALESCE(cr.provisional_total_marks,'') AS total_marks,
                           COALESCE(cr.provisional_marks_status,'pending') AS status
                    FROM campus_dynamics_portal.acad_course_registration cr
                    WHERE cr.id IN (" + idList + @")
                    ORDER BY cr.id DESC
                    LIMIT 30";

                List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
                using (MySqlCommand cmd = new MySqlCommand(previewSql, conn))
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        Dictionary<string, object> row = new Dictionary<string, object>();
                        row["id"] = Convert.ToInt32(rdr["id"]);
                        row["regno"] = rdr["regno"].ToString();
                        row["courseID"] = rdr["courseID"].ToString();
                        row["prog_id"] = rdr["prog_id"].ToString();
                        row["acad_year"] = rdr["acad_year"].ToString();
                        row["semester"] = rdr["semester"].ToString();
                        row["total_marks"] = rdr["total_marks"].ToString();
                        row["status"] = rdr["status"].ToString();
                        rows.Add(row);
                    }
                }

                return js.Serialize(new { success = true, count = targetIds.Count, rows = rows, message = "Preview ready." });
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    public static string ExecuteBatchWorkflow(string action, string scope, int[] ids, string year, string sem, string prog, string comment, AdminMarksPageKind kind)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        string connStr = ConnectionString;
        string normalizedAction = NormalizeStatus(action);
        string normalizedScope = NormalizeStatus(scope);

        if (normalizedAction != StatusPublished)
            return js.Serialize(new { success = false, message = "Invalid workflow action." });
        if (normalizedScope != "selected" && normalizedScope != "programme")
            return js.Serialize(new { success = false, message = "Invalid workflow scope." });

        try
        {
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                List<int> targetIds = ResolveWorkflowTargetIds(conn, normalizedAction, normalizedScope, ids, year, sem, prog, kind);
                if (targetIds.Count == 0)
                    return js.Serialize(new { success = false, message = "No eligible records found for execution." });

                int affected = 0;
                int failed = 0;
                string firstError = string.Empty;

                for (int i = 0; i < targetIds.Count; i++)
                {
                    using (MySqlTransaction tx = conn.BeginTransaction())
                    {
                        ProvisionalActionResult result = ProcessProvisionalAction(conn, tx, targetIds[i], normalizedAction, MarksAuthorizationService.GetCurrentUser(), comment);
                        if (result.Success)
                        {
                            tx.Commit();
                            affected++;
                        }
                        else
                        {
                            tx.Rollback();
                            failed++;
                            if (string.IsNullOrEmpty(firstError)) firstError = result.Message;
                        }
                    }
                }

                string msg = affected + " record(s) processed.";
                if (failed > 0)
                    msg += " " + failed + " failed." + (string.IsNullOrEmpty(firstError) ? "" : " First error: " + firstError);

                return js.Serialize(new { success = affected > 0, message = msg, affected = affected, failed = failed, total = targetIds.Count });
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { success = false, message = "Error: " + ex.Message });
        }
    }

    private static void ConfigureStatusDropdown(DropDownList ddlStatus, AdminMarksPageKind kind)
    {
        ddlStatus.Items.Clear();
        ddlStatus.Enabled = true;

        switch (kind)
        {
            case AdminMarksPageKind.AllMarks:
                ddlStatus.Items.Add(new ListItem("All Statuses", ""));
                ddlStatus.Items.Add(new ListItem("Not Entered", StatusNotEntered));
                ddlStatus.Items.Add(new ListItem("Pending", StatusPending));
                ddlStatus.Items.Add(new ListItem("Rejected", StatusRejected));
                ddlStatus.Items.Add(new ListItem("Published", StatusPublished));
                break;
            case AdminMarksPageKind.Published:
                ddlStatus.Items.Add(new ListItem("Published Only", StatusPublished));
                ddlStatus.Enabled = false;
                break;
            case AdminMarksPageKind.PendingCoursework:
                ddlStatus.Items.Add(new ListItem("Missing Coursework Only", ""));
                ddlStatus.Enabled = false;
                break;
            case AdminMarksPageKind.PendingExam:
                ddlStatus.Items.Add(new ListItem("Missing Exam Only", ""));
                ddlStatus.Enabled = false;
                break;
            default:
                ddlStatus.Items.Add(new ListItem("Pending Publish Only", StatusPending));
                ddlStatus.Enabled = false;
                break;
        }
    }

    private static string GetDefaultStatus(AdminMarksPageKind kind, string queryStatus)
    {
        switch (kind)
        {
            case AdminMarksPageKind.AllMarks:
                return NormalizeStatus(queryStatus);
            case AdminMarksPageKind.Published:
                return StatusPublished;
            case AdminMarksPageKind.ProvisionalPending:
                return StatusPending;
            default:
                return string.Empty;
        }
    }

    private static string GetEffectiveStatus(AdminMarksPageKind kind, string selectedStatus)
    {
        switch (kind)
        {
            case AdminMarksPageKind.ProvisionalPending:
                return StatusPending;
            case AdminMarksPageKind.Published:
                return StatusPublished;
            case AdminMarksPageKind.AllMarks:
                return NormalizeStatus(selectedStatus);
            default:
                return string.Empty;
        }
    }

    private static StringBuilder BuildGridWhere(AdminMarksPageKind kind, string status, ref bool useStatusParam)
    {
        useStatusParam = false;
        switch (kind)
        {
            case AdminMarksPageKind.AllMarks:
                if (status == StatusNotEntered)
                    return new StringBuilder("WHERE cr.provisional_course_work_marks IS NULL AND cr.provisional_exam_marks IS NULL");
                if (!string.IsNullOrEmpty(status))
                {
                    useStatusParam = true;
                    return new StringBuilder("WHERE COALESCE(cr.provisional_marks_status,'pending') = @status");
                }
                return new StringBuilder("WHERE 1=1");
            case AdminMarksPageKind.Published:
                return new StringBuilder("WHERE COALESCE(cr.provisional_marks_status,'pending') = 'published'");
            case AdminMarksPageKind.PendingCoursework:
                return new StringBuilder("WHERE cr.provisional_course_work_marks IS NULL");
            case AdminMarksPageKind.PendingExam:
                return new StringBuilder("WHERE cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NULL");
            default:
                return new StringBuilder("WHERE (cr.provisional_course_work_marks IS NOT NULL OR cr.provisional_exam_marks IS NOT NULL) AND COALESCE(cr.provisional_marks_status,'pending') = 'pending'");
        }
    }

    private static void AddParams(MySqlCommand cmd, string year, string sem, string prog, string status, bool useStatusParam, string lect, string search)
    {
        AddParams(cmd, year, sem, prog, status, useStatusParam, lect, search, "");
    }

    private static void AddParams(MySqlCommand cmd, string year, string sem, string prog, string status, bool useStatusParam, string lect, string search, string courseTerm)
    {
        if (!string.IsNullOrEmpty(year)) cmd.Parameters.AddWithValue("@year", year);
        if (!string.IsNullOrEmpty(sem)) cmd.Parameters.AddWithValue("@sem", sem);
        if (!string.IsNullOrEmpty(prog)) cmd.Parameters.AddWithValue("@prog", prog);
        if (useStatusParam && !string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("@status", status);
        if (!string.IsNullOrEmpty(lect)) cmd.Parameters.AddWithValue("@lect", lect);
        if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@q", "%" + search + "%");
        if (!string.IsNullOrEmpty(courseTerm)) cmd.Parameters.AddWithValue("@qc", "%" + courseTerm + "%");
    }

    private static string BuildPager(int page, int pageCount, string year, string sem, string prog, string status, string lect, string q, string ps, string qc)
    {
        StringBuilder sb = new StringBuilder();
        int startP = Math.Max(1, page - 3);
        int endP = Math.Min(pageCount, page + 3);

        Func<int, string> url = delegate(int p)
        {
            return string.Format("?pg={0}&year={1}&sem={2}&prog={3}&status={4}&lect={5}&ps={6}&q={7}&qc={8}",
                p,
                Uri.EscapeDataString(year ?? string.Empty),
                Uri.EscapeDataString(sem ?? string.Empty),
                Uri.EscapeDataString(prog ?? string.Empty),
                Uri.EscapeDataString(status ?? string.Empty),
                Uri.EscapeDataString(lect ?? string.Empty),
                Uri.EscapeDataString(ps ?? string.Empty),
                Uri.EscapeDataString(q ?? string.Empty),
                Uri.EscapeDataString(qc ?? string.Empty));
        };

        if (page > 1) sb.AppendFormat("<a href='{0}'>&laquo;</a>", url(page - 1));
        for (int i = startP; i <= endP; i++)
        {
            if (i == page) sb.AppendFormat("<span class='active'>{0}</span>", i);
            else sb.AppendFormat("<a href='{0}'>{1}</a>", url(i), i);
        }
        if (page < pageCount) sb.AppendFormat("<a href='{0}'>&raquo;</a>", url(page + 1));
        return sb.ToString();
    }

    private static List<int> ResolveWorkflowTargetIds(MySqlConnection conn, string action, string scope, int[] ids, string year, string sem, string prog, AdminMarksPageKind kind)
    {
        List<int> targetIds = new List<int>();
        if (scope == "selected")
        {
            if (ids != null)
            {
                for (int i = 0; i < ids.Length; i++)
                    if (ids[i] > 0 && RecordInScope(conn, ids[i])) targetIds.Add(ids[i]);
            }
            return targetIds;
        }

        StringBuilder sql = new StringBuilder("SELECT cr.id FROM campus_dynamics_portal.acad_course_registration cr WHERE 1=1");
        sql.Append(ScopeFilter("cr")); // role-based scope

        switch (kind)
        {
            case AdminMarksPageKind.ProvisionalPending:
                sql.Append(" AND (cr.provisional_course_work_marks IS NOT NULL OR cr.provisional_exam_marks IS NOT NULL) AND COALESCE(cr.provisional_marks_status,'pending')='pending'");
                break;
            case AdminMarksPageKind.Published:
                sql.Append(" AND COALESCE(cr.provisional_marks_status,'pending')='published'");
                break;
            case AdminMarksPageKind.PendingCoursework:
                sql.Append(" AND cr.provisional_course_work_marks IS NULL");
                break;
            case AdminMarksPageKind.PendingExam:
                sql.Append(" AND cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NULL");
                break;
        }

        sql.Append(" AND cr.provisional_course_work_marks IS NOT NULL AND cr.provisional_exam_marks IS NOT NULL AND cr.provisional_total_marks IS NOT NULL AND COALESCE(cr.provisional_marks_status,'pending') IN ('pending','approved','published')");

        if (!string.IsNullOrEmpty(year)) sql.Append(" AND cr.acad_year=@year");
        if (!string.IsNullOrEmpty(sem)) sql.Append(" AND cr.semester=@sem");
        if (!string.IsNullOrEmpty(prog)) sql.Append(" AND cr.prog_id=@prog");

        using (MySqlCommand cmd = new MySqlCommand(sql.ToString(), conn))
        {
            if (!string.IsNullOrEmpty(year)) cmd.Parameters.AddWithValue("@year", year);
            if (!string.IsNullOrEmpty(sem)) cmd.Parameters.AddWithValue("@sem", sem);
            if (!string.IsNullOrEmpty(prog)) cmd.Parameters.AddWithValue("@prog", prog);
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                    targetIds.Add(Convert.ToInt32(rdr["id"]));
            }
        }

        return targetIds;
    }

        /// <summary>
        /// The Track cell: who last moved a mark on this row, when, and which mark it was.
        ///
        /// Two tags rather than prose, because the column is read by scanning a page of rows,
        /// not by reading one: CW and EX light up only for the mark that actually moved, and
        /// the arrow carries the old and new values so the common question — "changed from
        /// what to what" — is answered without opening anything.
        ///
        /// A row with no entry says so plainly. "Never changed" and "changed by somebody we
        /// cannot name" are different facts and must not look the same.
        /// </summary>
        private static string BuildTrackCell(string packed, int changeCount)
        {
            if (string.IsNullOrEmpty(packed))
                return "<span class='pm-track pm-track--none' title='No change has been recorded for this row'>&mdash;</span>";

            string[] p = packed.Split(new string[] { "~|~" }, StringSplitOptions.None);
            string who    = p.Length > 0 ? p[0] : "";
            string when   = p.Length > 1 ? p[1] : "";
            bool cw       = p.Length > 2 && p[2] == "1";
            bool ex       = p.Length > 3 && p[3] == "1";
            string action = p.Length > 4 ? p[4] : "";
            string oldCw  = p.Length > 5 ? p[5] : "";
            string newCw  = p.Length > 6 ? p[6] : "";
            string oldEx  = p.Length > 7 ? p[7] : "";
            string newEx  = p.Length > 8 ? p[8] : "";

            if (string.IsNullOrEmpty(who)) who = "system";

            StringBuilder t = new StringBuilder();
            t.Append("<div class='pm-track'>");

            t.Append("<div class='pm-track__tags'>");
            if (cw) t.AppendFormat("<span class='pm-chg pm-chg--cw' title='Coursework {0} &rarr; {1}'>CW {0}&rarr;{1}</span>",
                                   HtmlEnc(Dash(oldCw)), HtmlEnc(Dash(newCw)));
            if (ex) t.AppendFormat("<span class='pm-chg pm-chg--ex' title='Exam {0} &rarr; {1}'>EX {0}&rarr;{1}</span>",
                                   HtmlEnc(Dash(oldEx)), HtmlEnc(Dash(newEx)));
            if (!cw && !ex) t.Append("<span class='pm-chg pm-chg--other'>entered</span>");
            t.Append("</div>");

            string more = changeCount > 1 ? string.Format(" <span class='pm-track__n' title='{0} changes recorded in total'>+{1}</span>",
                                                          changeCount, changeCount - 1) : "";
            // MIGRATE means the entry was recovered from the old activity log rather than
            // recorded first-hand. Saying so is the difference between a trail and a guess.
            string src = action == "MIGRATE" ? "<span class='pm-track__hist' title='Recovered from the activity log, before first-hand recording began'>log</span>" : "";

            t.AppendFormat("<div class='pm-track__who' title='{0}'>{1}{2}{3}</div>",
                           HtmlEnc(who + " on " + when), HtmlEnc(Shorten(who, 16)), more, src);
            t.AppendFormat("<div class='pm-track__when'>{0}</div>", HtmlEnc(when));
            t.Append("</div>");
            return t.ToString();
        }

        private static string Dash(string v) { return string.IsNullOrEmpty(v) ? "\u2013" : v; }

        private static string Shorten(string v, int n)
        {
            if (string.IsNullOrEmpty(v)) return "";
            v = v.Trim();
            return v.Length <= n ? v : v.Substring(0, n - 1) + "\u2026";
        }

        private static string BuildRowActions(AdminMarksPageKind kind, int id)
        {
                if (kind == AdminMarksPageKind.Published)
                {
                        return string.Format(@"
            <button type='button' class='pm-row-menu__item act--edit' onclick='closeMenuThen(this,function(){{openEdit({0});}})'>&#9998; Edit Marks</button>
            <button type='button' class='pm-row-menu__item act--approve' onclick='closeMenuThen(this,function(){{openDetails({0});}})'>&#9432; View Details</button>
            <div class='pm-row-menu__sep'></div>
            <button type='button' class='pm-row-menu__item act--publish' onclick='closeMenuThen(this,function(){{reprocessMark({0});}})'>&#10227; Reprocess</button>
            <button type='button' class='pm-row-menu__item act--reset' onclick='closeMenuThen(this,function(){{unpublishMark({0});}})'>&#8634; Unpublish</button>", id);
                }

                string baseItems = string.Format(@"
            <button type='button' class='pm-row-menu__item act--edit' onclick='closeMenuThen(this,function(){{openEdit({0});}})'>&#9998; Edit Marks</button>
            <button type='button' class='pm-row-menu__item act--approve' onclick='closeMenuThen(this,function(){{openDetails({0});}})'>&#9432; View Details</button>
            <button type='button' class='pm-row-menu__item act--approve' onclick='closeMenuThen(this,function(){{openReview({0});}})'>&#10003; Review</button>
            <div class='pm-row-menu__sep'></div>
            <button type='button' class='pm-row-menu__item act--publish' onclick='closeMenuThen(this,function(){{openPublish({0});}})'>&#8679; Publish</button>
            <button type='button' class='pm-row-menu__item act--reset' onclick='closeMenuThen(this,function(){{resetToPending({0});}})'>&#8635; Reset</button>", id);

                if (kind == AdminMarksPageKind.AllMarks)
                    baseItems += string.Format(@"
            <div class='pm-row-menu__sep'></div>
            <button type='button' class='pm-row-menu__item' style='color:#7c3aed;' onclick='closeMenuThen(this,function(){{openSetStatus({0});}})'>&#9654; Set Status&hellip;</button>
            <button type='button' class='pm-row-menu__item' style='color:#c62828;' onclick='closeMenuThen(this,function(){{openDeleteReg({0});}})'>&#10006; Delete Registration</button>", id);

                return baseItems;
        }

    /// <summary>
    /// Centralized transition engine for provisional row actions.
    /// Single-row APIs and batch APIs both call this method to guarantee identical rules.
    /// </summary>
    private static ProvisionalActionResult ProcessProvisionalAction(MySqlConnection conn, MySqlTransaction tx, int id, string normalizedAction, string actor, string comment)
    {
        return ProcessProvisionalAction(conn, tx, id, normalizedAction, actor, comment, null);
    }

    /// <summary>
    /// Core single-record processor. When <paramref name="deferGpa"/> is supplied (batch
    /// publishing), the per-row semester-GPA rewrite and the CGPA aggregate are skipped and
    /// the student's semester is queued for a single recompute at the end of the batch —
    /// see MarksGpaDeferral. Passing null preserves the original per-row behaviour exactly,
    /// which is what every interactive single-record caller still does.
    /// </summary>
    private static ProvisionalActionResult ProcessProvisionalAction(MySqlConnection conn, MySqlTransaction tx, int id, string normalizedAction, string actor, string comment, MarksGpaDeferral deferGpa)
    {
        ProvisionalActionResult result = new ProvisionalActionResult { Success = false, Message = "Unable to process action." };

        if (normalizedAction != StatusRejected && normalizedAction != StatusPublished)
        {
            result.Message = "Invalid action.";
            return result;
        }

        // Role-based scope: a dean/HOD cannot act on records outside their faculty/department.
        if (!RecordInScope(conn, tx, id))
        {
            result.Message = "Out of scope: this record is not in your faculty/department.";
            return result;
        }

        string courseCol = GetCourseColumnExpression(conn, "cr");
        if (string.IsNullOrEmpty(courseCol))
        {
            result.Message = "Course column not found on acad_course_registration.";
            return result;
        }

        string regno = string.Empty;
        string courseId = string.Empty;
        string acadYear = string.Empty;
        string semester = string.Empty;
        string provStatus = string.Empty;
        int? cw = null;
        int? exam = null;
        int? total = null;
        int studyYear = 0;

        string fetchSql = @"
            SELECT cr.regno,
                   " + courseCol + @" AS courseID,
                   cr.acad_year,
                   cr.semester,
                   cr.provisional_course_work_marks,
                   cr.provisional_exam_marks,
                   cr.provisional_total_marks,
                   COALESCE(cr.provisional_marks_status,'pending') AS prov_status,
                   COALESCE((SELECT MAX(r2.studyyear)
                             FROM acad_registration r2
                             WHERE r2.regno = cr.regno
                               AND r2.acad_year = cr.acad_year
                               AND r2.semester = cr.semester), 0) AS study_year
            FROM campus_dynamics_portal.acad_course_registration cr
            WHERE cr.id = @id
            LIMIT 1";

        using (MySqlCommand cmd = new MySqlCommand(fetchSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", id);
            using (MySqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (!rdr.Read())
                {
                    result.Message = "Record not found.";
                    return result;
                }

                regno = rdr["regno"].ToString();
                courseId = rdr["courseID"].ToString();
                acadYear = rdr["acad_year"].ToString();
                semester = rdr["semester"].ToString();
                provStatus = NormalizeStatus(rdr["prov_status"].ToString());
                cw = rdr.IsDBNull(rdr.GetOrdinal("provisional_course_work_marks")) ? (int?)null : Convert.ToInt32(rdr["provisional_course_work_marks"]);
                exam = rdr.IsDBNull(rdr.GetOrdinal("provisional_exam_marks")) ? (int?)null : Convert.ToInt32(rdr["provisional_exam_marks"]);
                total = rdr.IsDBNull(rdr.GetOrdinal("provisional_total_marks")) ? (int?)null : Convert.ToInt32(rdr["provisional_total_marks"]);
                studyYear = rdr.IsDBNull(rdr.GetOrdinal("study_year")) ? 0 : Convert.ToInt32(rdr["study_year"]);
            }
        }

        if (normalizedAction == StatusRejected)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                result.Message = "Comment required for rejection.";
                return result;
            }

            using (MySqlCommand cmd = new MySqlCommand(@"
                UPDATE campus_dynamics_portal.acad_course_registration
                SET provisional_marks_status = @status,
                    provisional_marks_review_comments = @comment,
                    provisional_marks_reviewed_by = @reviewer,
                    provisional_marks_review_date = NOW()
                WHERE id = @id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@status", StatusRejected);
                cmd.Parameters.AddWithValue("@comment", comment.Trim());
                cmd.Parameters.AddWithValue("@reviewer", actor);
                cmd.Parameters.AddWithValue("@id", id);
                if (cmd.ExecuteNonQuery() <= 0)
                {
                    result.Message = "Rejection update failed.";
                    return result;
                }
            }

            // Rejection sends the mark back down the journey; the reason is stored where the
            // stage consoles and the student's Mark Status Check look for it.
            MarkStageSync.Sync(conn, tx, id, actor, comment);

            result.Success = true;
            result.Message = "Marks rejected and returned to the lecturer's stage.";
            return result;
        }

        // Publish path (classic aligned)
        // - Grade scale: A..E (5-point model)
        // - Grade points + Credit Units are written to acad_results
        // - Semester GPA is recalculated and propagated to all rows of that semester context
        // - CGPA is recomputed cumulatively for messaging/classification context
        // Require at least one mark value to publish; individual components are optional
        if (!cw.HasValue && !exam.HasValue && !total.HasValue)
        {
            result.Message = "At least one mark value must be present before publishing.";
            return result;
        }
        // No status gate — admin may publish from any state (pending, approved, rejected, or republish)

        // Use stored total if available; otherwise sum whatever components exist
        int finalTotal = total.HasValue ? total.Value : (cw ?? 0) + (exam ?? 0);
        total = finalTotal;

        string grade = ComputeGrade(total.Value);
        decimal gradePt = ComputeGradePoint(grade);

        int creditUnits = 3;
        string courseCreditCol = ResolveCourseCreditUnitsColumn(conn);
        if (!string.IsNullOrEmpty(courseCreditCol))
        {
            using (MySqlCommand cuCmd = new MySqlCommand(
                "SELECT COALESCE(NULLIF(" + courseCreditCol + ",0),3) FROM acad_course WHERE courseID=@courseID LIMIT 1", conn, tx))
            {
                cuCmd.Parameters.AddWithValue("@courseID", courseId);
                object cuObj = cuCmd.ExecuteScalar();
                if (cuObj != null && cuObj != DBNull.Value)
                {
                    int parsed;
                    if (int.TryParse(cuObj.ToString(), out parsed) && parsed > 0) creditUnits = parsed;
                }
            }
        }

        string resultsCreditCol = ResolveResultsCreditUnitsColumn(conn);

        EnsureResultCommentTextNullable(conn);

        // ── Step 1: Read any existing result for (regno, courseid) ──────────────────
        // We must do this BEFORE writing so we can:
        //   (a) preserve original acad/semester/studyyear placement — the index only covers
        //       (regno, courseid), so without this the UPDATE would silently relocate the
        //       result to the provisional record's semester, corrupting the transcript;
        //   (b) detect when a mark is being reduced (overwrite guard);
        //   (c) write a proper audit trail in result_comment.
        int? priorScore = null;
        string priorGrade = null;
        string effectiveAcad     = acadYear;
        int    effectiveSemester = ParseIntSafe(semester, 0);
        int    effectiveStudyYr  = studyYear;

        // The term this publish is actually for, kept because the block below overwrites the
        // "effective" values with whatever term an existing result already sits in.
        string requestedAcad     = acadYear;
        int    requestedSemester = ParseIntSafe(semester, 0);

        using (MySqlCommand chk = new MySqlCommand(@"
            SELECT score, grade, acad, semester, COALESCE(studyyear,0) AS studyyear
            FROM acad_results
            WHERE regno = @regno AND UPPER(TRIM(courseid)) = UPPER(TRIM(@courseid))
            LIMIT 1", conn, tx))
        {
            chk.Parameters.AddWithValue("@regno",    regno);
            chk.Parameters.AddWithValue("@courseid", courseId);
            using (MySqlDataReader rdr = chk.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (rdr.Read())
                {
                    priorScore = rdr.IsDBNull(0) ? (int?)null : Convert.ToInt32(rdr[0]);
                    priorGrade = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                    // Preserve original academic placement — never relocate an existing result
                    if (!rdr.IsDBNull(2) && !string.IsNullOrEmpty(rdr.GetString(2)))
                        effectiveAcad = rdr.GetString(2);
                    if (!rdr.IsDBNull(3))
                        effectiveSemester = Convert.ToInt32(rdr[3]);
                    int sy = rdr.IsDBNull(4) ? 0 : Convert.ToInt32(rdr[4]);
                    if (sy > 0) effectiveStudyYr = sy;
                }
            }
        }

        // ── Step 1b: Refuse to publish one term's mark on top of another ─────────────
        // acad_results is UNIQUE on (regno, courseid) alone: one result per course code per
        // student, carrying the latest grade. That is deliberate — it is what lets a retake
        // show the course once with its new mark (see RetakeService, which snapshots the
        // original into acad_retake_registrations and republishes into this same row).
        //
        // The hole is that a SECOND ORDINARY REGISTRATION of the same course gets the same
        // treatment without any of the safeguards: no snapshot, no notice. The UPSERT lands on
        // the earlier term's row, the older mark is overwritten with nowhere to recover it
        // from, and the term being published shows the student nothing. 113 registrations
        // since 2023/2024 are in that state, and only 9 of 246 repeats are flagged as retakes,
        // so this is mostly ordinary repeat registrations, not the retake feature.
        //
        // Refuse loudly. A blocked publish with an explanation is recoverable; a silent
        // overwrite destroys one mark and hides another, and the student is the one who
        // finds out.
        if (priorScore.HasValue
            && (!string.Equals((effectiveAcad ?? "").Trim(), (requestedAcad ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
                || effectiveSemester != requestedSemester))
        {
            result.Message = string.Format(
                "Cannot publish: {0} already has a published result for {1} under {2} semester {3} " +
                "(score {4}{5}). A result is stored once per course code, so publishing this {6} " +
                "semester {7} mark would overwrite that one and this term would still show the " +
                "student nothing. Choose the right route: if the student is genuinely sitting {1} " +
                "again, register it through Retake Registration, which keeps the original mark and " +
                "replaces the grade properly. If this registration is in the wrong term or is a " +
                "duplicate, fix it in the Course Correction Centre. Only then publish.",
                regno, courseId,
                string.IsNullOrEmpty(effectiveAcad) ? "?" : effectiveAcad, effectiveSemester,
                priorScore.Value, string.IsNullOrEmpty(priorGrade) ? "" : " / " + priorGrade,
                string.IsNullOrEmpty(requestedAcad) ? "?" : requestedAcad, requestedSemester);
            return result;
        }

        // ── Step 2: Build audit comment ──────────────────────────────────────────────
        string overwriteNote = "";
        if (priorScore.HasValue)
        {
            if (priorScore.Value != total.Value)
                overwriteNote = string.Format(" [Overwrite: was {0}/{1}]", priorScore.Value, priorGrade ?? "?");
            else
                overwriteNote = " [Re-publish: score unchanged]";
        }
        string finalComment = "Published from provisional marks by " + actor + overwriteNote;

        // Attribute this final-result change for the acad_results audit trigger (who changed what).
        SetMarkAuditContext(conn, tx, actor, "ProvisionalMarks:publish", finalComment);

        // ── Step 3: Atomic UPSERT — acad/semester/studyyear NOT in UPDATE clause ─────
        // This means existing rows keep their original placement in the transcript.
        // Only score/grade/gradept/CU/comment are ever updated.
        using (MySqlCommand upsert = new MySqlCommand(@"
            INSERT INTO acad_results
                (regno, courseid, acad, semester, studyyear, score, grade, gradept, " + resultsCreditCol + @", result_comment)
            VALUES
                (@regno, @courseid, @acad, @semester, @studyyear, @score, @grade, @gradept, @cu, @comment)
            ON DUPLICATE KEY UPDATE
                score          = VALUES(score),
                grade          = VALUES(grade),
                gradept        = VALUES(gradept),
                " + resultsCreditCol + @" = VALUES(" + resultsCreditCol + @"),
                result_comment = VALUES(result_comment)", conn, tx))
        {
            upsert.Parameters.AddWithValue("@regno",    regno);
            upsert.Parameters.AddWithValue("@courseid", courseId);
            upsert.Parameters.AddWithValue("@acad",     effectiveAcad);
            upsert.Parameters.AddWithValue("@semester", effectiveSemester);
            upsert.Parameters.AddWithValue("@studyyear",effectiveStudyYr <= 0 ? (object)DBNull.Value : effectiveStudyYr);
            upsert.Parameters.AddWithValue("@score",    total.Value);
            upsert.Parameters.AddWithValue("@grade",    grade);
            upsert.Parameters.AddWithValue("@gradept",  gradePt);
            upsert.Parameters.AddWithValue("@cu",       creditUnits);
            upsert.Parameters.AddWithValue("@comment",  finalComment);
            upsert.ExecuteNonQuery();
        }

        // Carry overwrite note through to the caller's success message
        string overwriteWarning = (priorScore.HasValue && priorScore.Value != total.Value)
            ? string.Format(" WARNING: previous score {0} ({1}) overwritten with {2}.", priorScore.Value, priorGrade ?? "?", total.Value)
            : "";

        // Batch mode: queue this student's semester for ONE recompute after the batch and
        // skip the CGPA aggregate entirely (it is only ever used for the message below, and
        // is never persisted). Interactive mode (deferGpa == null) behaves exactly as before.
        decimal semesterGpa = 0m, cgpa = 0m;
        string awardClass = "";
        if (deferGpa != null)
        {
            deferGpa.Touch(regno, acadYear, ParseIntSafe(semester, 0));
        }
        else
        {
            semesterGpa = ComputeSemesterGpa(conn, tx, regno, acadYear, ParseIntSafe(semester, 0));
            cgpa = ComputeStudentCgpa(conn, tx, regno);
            awardClass = ComputeAwardClass(cgpa);

            using (MySqlCommand cmd = new MySqlCommand(@"
                UPDATE acad_results
                SET gpa = @gpa
                WHERE regno = @regno
                  AND acad = @acad
                  AND semester = @semester", conn, tx))
            {
                cmd.Parameters.AddWithValue("@gpa", semesterGpa);
                cmd.Parameters.AddWithValue("@regno", regno);
                cmd.Parameters.AddWithValue("@acad", acadYear);
                cmd.Parameters.AddWithValue("@semester", ParseIntSafe(semester, 0));
                cmd.ExecuteNonQuery();
            }
        }

        using (MySqlCommand cmd = new MySqlCommand(@"
            UPDATE campus_dynamics_portal.acad_course_registration
            SET provisional_total_marks = @total,
                provisional_marks_status = @published,
                provisional_published_by = @publisher,
                provisional_published_date = NOW()
            WHERE id = @id", conn, tx))
        {
            cmd.Parameters.AddWithValue("@total", total.Value);
            cmd.Parameters.AddWithValue("@published", StatusPublished);
            cmd.Parameters.AddWithValue("@publisher", actor);
            cmd.Parameters.AddWithValue("@id", id);
            if (cmd.ExecuteNonQuery() <= 0)
            {
                result.Message = "Provisional publish status update failed.";
                return result;
            }
        }

        // The staged journey reaches PUBLISHED at exactly the moment acad_results does —
        // same transaction, so a student can never hold a result the stage says is unpublished.
        MarkStageSync.Sync(conn, tx, id, actor, "");

        // ── Retake handling (best-effort; never blocks a publish) ────────────────────
        // If this registration is a RETAKE, flag the published result as a retake (so the
        // transcript marks it RT) and complete the independent retake tracking record with
        // the new marks. The original attempt is preserved in the retake snapshot + audit.
        try
        {
            bool isRetakeReg = false;
            using (MySqlCommand rcmd = new MySqlCommand(
                @"SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration
                  WHERE id=@id AND (UPPER(TRIM(IFNULL(course_status,'')))='RETAKE'
                                 OR UPPER(TRIM(IFNULL(registration_type,'')))='RT')", conn, tx))
            {
                rcmd.Parameters.AddWithValue("@id", id);
                isRetakeReg = Convert.ToInt32(rcmd.ExecuteScalar()) > 0;
            }
            if (isRetakeReg)
            {
                using (MySqlCommand f = new MySqlCommand(
                    "UPDATE acad_results SET is_retake=1 WHERE regno=@r AND UPPER(TRIM(courseid))=UPPER(TRIM(@c))", conn, tx))
                {
                    f.Parameters.AddWithValue("@r", regno);
                    f.Parameters.AddWithValue("@c", courseId);
                    f.ExecuteNonQuery();
                }
                using (MySqlCommand u = new MySqlCommand(
                    @"UPDATE campus_dynamics_portal.acad_retake_registrations
                      SET status='COMPLETED', new_total=@t, new_grade=@g, new_gradept=@gp
                      WHERE course_reg_id=@id", conn, tx))
                {
                    u.Parameters.AddWithValue("@t", total.Value);
                    u.Parameters.AddWithValue("@g", grade);
                    u.Parameters.AddWithValue("@gp", gradePt);
                    u.Parameters.AddWithValue("@id", id);
                    u.ExecuteNonQuery();
                }
            }
        }
        catch { /* retake bookkeeping is non-critical to the result write */ }

        result.Success = true;
        result.SemesterGpa = semesterGpa;
        result.Cgpa = cgpa;
        result.AwardClass = awardClass;
        // In batch mode the GPA figures are not computed here (they are rewritten once per
        // student/semester after the batch), so don't report a misleading "0.00".
        result.Message = deferGpa != null
            ? ("Marks published to final results." + overwriteWarning)
            : ("Marks published to final results. Semester GPA " + semesterGpa.ToString("F2") + ", CGPA " + cgpa.ToString("F2") + " (" + awardClass + ")." + overwriteWarning);
        return result;
    }

    private static ProvisionalActionResult ProcessUnpublishAction(MySqlConnection conn, MySqlTransaction tx, int id, string actor, string comment)
    {
        ProvisionalActionResult result = new ProvisionalActionResult { Success = false, Message = "Unable to unpublish record." };

        if (!RecordInScope(conn, tx, id))
        {
            result.Message = "Out of scope: this record is not in your faculty/department.";
            return result;
        }

        string courseCol = GetCourseColumnExpression(conn, "cr");
        if (string.IsNullOrEmpty(courseCol))
        {
            result.Message = "Course column not found on acad_course_registration.";
            return result;
        }

        string regno = string.Empty;
        string courseId = string.Empty;
        string acadYear = string.Empty;
        string semester = string.Empty;
        string provStatus = string.Empty;

        using (MySqlCommand cmd = new MySqlCommand(@"
            SELECT cr.regno,
                   " + courseCol + @" AS courseID,
                   cr.acad_year,
                   cr.semester,
                   COALESCE(cr.provisional_marks_status,'pending') AS prov_status
            FROM campus_dynamics_portal.acad_course_registration cr
            WHERE cr.id = @id
            LIMIT 1", conn, tx))
        {
            cmd.Parameters.AddWithValue("@id", id);
            using (MySqlDataReader rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (!rdr.Read())
                {
                    result.Message = "Record not found.";
                    return result;
                }

                regno = rdr["regno"].ToString();
                courseId = rdr["courseID"].ToString();
                acadYear = rdr["acad_year"].ToString();
                semester = rdr["semester"].ToString();
                provStatus = NormalizeStatus(rdr["prov_status"].ToString());
            }
        }

        if (provStatus != StatusPublished)
        {
            result.Message = "Only published records can be unpublished.";
            return result;
        }

        // Attribute this final-result deletion for the acad_results audit trigger.
        SetMarkAuditContext(conn, tx, actor, "ProvisionalMarks:unpublish",
            "Unpublished by " + actor + (string.IsNullOrEmpty(comment) ? "" : (": " + comment)));

        using (MySqlCommand del = new MySqlCommand(@"
            DELETE FROM acad_results
            WHERE regno = @regno
              AND courseid = @courseid
              AND acad = @acad
              AND semester = @semester", conn, tx))
        {
            del.Parameters.AddWithValue("@regno", regno);
            del.Parameters.AddWithValue("@courseid", courseId);
            del.Parameters.AddWithValue("@acad", acadYear);
            del.Parameters.AddWithValue("@semester", ParseIntSafe(semester, 0));
            del.ExecuteNonQuery();
        }

        string note = string.IsNullOrWhiteSpace(comment)
            ? "[Unpublished by " + actor + "]"
            : "[Unpublished by " + actor + ": " + comment.Trim() + "]";

        using (MySqlCommand upd = new MySqlCommand(@"
            UPDATE campus_dynamics_portal.acad_course_registration
            SET provisional_marks_status = @pending,
                provisional_published_by = NULL,
                provisional_published_date = NULL,
                provisional_marks_review_comments = CONCAT(COALESCE(provisional_marks_review_comments,''), ' ', @note),
                provisional_marks_reviewed_by = @actor,
                provisional_marks_review_date = NOW()
            WHERE id = @id", conn, tx))
        {
            upd.Parameters.AddWithValue("@pending", StatusPending);
            upd.Parameters.AddWithValue("@note", note);
            upd.Parameters.AddWithValue("@actor", actor);
            upd.Parameters.AddWithValue("@id", id);
            if (upd.ExecuteNonQuery() <= 0)
            {
                result.Message = "Unpublish update failed.";
                return result;
            }
        }

        // The result row has just been deleted, so the stage must come back down with it and
        // release its publish/approve/capture back-references.
        MarkStageSync.Sync(conn, tx, id, actor, comment);

        int semesterInt = ParseIntSafe(semester, 0);
        decimal semesterGpa = ComputeSemesterGpa(conn, tx, regno, acadYear, semesterInt);
        decimal cgpa = ComputeStudentCgpa(conn, tx, regno);
        string awardClass = ComputeAwardClass(cgpa);

        using (MySqlCommand gpaUpd = new MySqlCommand(@"
            UPDATE acad_results
            SET gpa = @gpa
            WHERE regno = @regno
              AND acad = @acad
              AND semester = @semester", conn, tx))
        {
            gpaUpd.Parameters.AddWithValue("@gpa", semesterGpa);
            gpaUpd.Parameters.AddWithValue("@regno", regno);
            gpaUpd.Parameters.AddWithValue("@acad", acadYear);
            gpaUpd.Parameters.AddWithValue("@semester", semesterInt);
            gpaUpd.ExecuteNonQuery();
        }

        result.Success = true;
        result.SemesterGpa = semesterGpa;
        result.Cgpa = cgpa;
        result.AwardClass = awardClass;
        result.Message = "Record unpublished and moved back to pending. Semester GPA " + semesterGpa.ToString("F2") + ", CGPA " + cgpa.ToString("F2") + " (" + awardClass + ").";
        return result;
    }

    /// <summary>Weighted semester GPA: SUM(gradept*CU)/SUM(CU)</summary>
    private static decimal ComputeSemesterGpa(MySqlConnection conn, MySqlTransaction tx, string regno, string acadYear, int semester)
    {
        string resultsCreditCol = ResolveResultsCreditUnitsColumn(conn);
        using (MySqlCommand cmd = new MySqlCommand(@"
            SELECT ROUND(SUM(COALESCE(gradept,0) * COALESCE(NULLIF(" + resultsCreditCol + @",0),3)) /
                         NULLIF(SUM(COALESCE(NULLIF(" + resultsCreditCol + @",0),3)), 0), 2)
            FROM acad_results
            WHERE regno = @regno AND acad = @acad AND semester = @semester", conn, tx))
        {
            cmd.Parameters.AddWithValue("@regno", regno);
            cmd.Parameters.AddWithValue("@acad", acadYear);
            cmd.Parameters.AddWithValue("@semester", semester);
            object value = cmd.ExecuteScalar();
            if (value == null || value == DBNull.Value) return 0m;
            return Convert.ToDecimal(value);
        }
    }

    /// <summary>Cumulative CGPA across all results for the student.</summary>
    private static decimal ComputeStudentCgpa(MySqlConnection conn, MySqlTransaction tx, string regno)
    {
        string resultsCreditCol = ResolveResultsCreditUnitsColumn(conn);
        using (MySqlCommand cmd = new MySqlCommand(@"
            SELECT ROUND(SUM(COALESCE(gradept,0) * COALESCE(NULLIF(" + resultsCreditCol + @",0),3)) /
                         NULLIF(SUM(COALESCE(NULLIF(" + resultsCreditCol + @",0),3)), 0), 2)
            FROM acad_results
            WHERE regno = @regno", conn, tx))
        {
            cmd.Parameters.AddWithValue("@regno", regno);
            object value = cmd.ExecuteScalar();
            if (value == null || value == DBNull.Value) return 0m;
            return Convert.ToDecimal(value);
        }
    }

    /// <summary>Classification thresholds aligned to ResultsUpdates/NewStudentInfo (4.40/3.60/2.80/2.00).</summary>
    private static string ComputeAwardClass(decimal cgpa)
    {
        if (cgpa >= 4.40m) return "FIRST CLASS";
        if (cgpa >= 3.60m) return "SECOND CLASS UPPER";
        if (cgpa >= 2.80m) return "SECOND CLASS LOWER";
        if (cgpa >= 2.00m) return "PASS";
        return "RETAKE";
    }

    private static string NormalizeStatus(string status)
    {
        return (status ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static int ParseIntSafe(string value, int fallback)
    {
        int parsed;
        if (!int.TryParse(value, out parsed)) return fallback;
        return parsed;
    }

    // Records who/where/why for the acad_results audit trigger, keyed by CONNECTION_ID().
    // Connector-safe (normal params). Best-effort — must never break the mark write.
    private static void SetMarkAuditContext(MySqlConnection conn, MySqlTransaction tx, string actor, string source, string reason)
    {
        try
        {
            string ip;
            try { ip = MarksAuthorizationService.GetClientIP(); } catch { ip = "unknown"; }
            using (MySqlCommand cmd = new MySqlCommand(
                "REPLACE INTO campus_dynamics.mark_audit_context (conn_id, actor, source, reason, ip, set_at) " +
                "VALUES (CONNECTION_ID(), @a, @s, @r, @ip, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("@a", (actor ?? "").Length > 90 ? actor.Substring(0, 90) : (actor ?? ""));
                cmd.Parameters.AddWithValue("@s", (source ?? "").Length > 100 ? source.Substring(0, 100) : (source ?? ""));
                cmd.Parameters.AddWithValue("@r", (reason ?? "").Length > 200 ? reason.Substring(0, 200) : (reason ?? ""));
                cmd.Parameters.AddWithValue("@ip", ip);
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* attribution is best-effort */ }
    }

    // MRU / NCHE grading scale (see the transcript "KEY TO GRADES"):
    //   80-100 A 5.0 | 75-79 B+ 4.5 | 70-74 B 4.0 | 65-69 C+ 3.5
    //   60-64 C 3.0 | 55-59 D+ 2.5 | 50-54 D 2.0 | 0-49 F 0.0
    // There is NO A+, A-, B-, C-, D- or E in this scheme.
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

    private static decimal ComputeGradePoint(string grade)
    {
        switch ((grade ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "A":  return 5.0m;
            case "B+": return 4.5m;
            case "B":  return 4.0m;
            case "C+": return 3.5m;
            case "C":  return 3.0m;
            case "D+": return 2.5m;
            case "D":  return 2.0m;
            default:   return 0.0m;   // F
        }
    }

    private static string HtmlEnc(string s)
    {
        return HttpUtility.HtmlEncode(s ?? string.Empty);
    }

    /// <summary>
    /// One-shot schema self-heal for acad_results.result_comment.
    ///
    /// DANGER this guards against: an ALTER TABLE performs an IMPLICIT COMMIT in MySQL.
    /// This used to be invoked from inside the per-row publish loop, so if the ALTER had
    /// ever fired mid-batch it would have committed a half-finished publish and silently
    /// destroyed the atomicity of the surrounding transaction. RunOnce keeps the check
    /// (and any DDL) to a single execution per AppDomain, and callers now invoke it
    /// before opening a transaction — never inside one.
    /// </summary>
    private static void EnsureResultCommentTextNullable(MySqlConnection conn)
    {
        MarksSchemaCache.RunOnce("acad_results.result_comment.textnull", delegate
        {
            using (MySqlCommand cmd = new MySqlCommand(@"
                SELECT DATA_TYPE, IS_NULLABLE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA='campus_dynamics'
                  AND TABLE_NAME='acad_results'
                  AND COLUMN_NAME='result_comment'
                LIMIT 1", conn))
            using (MySqlDataReader rdr = cmd.ExecuteReader())
            {
                if (!rdr.Read()) return;
                string dataType = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0).ToLowerInvariant();
                string isNullable = rdr.IsDBNull(1) ? "YES" : rdr.GetString(1).ToUpperInvariant();
                if (dataType == "text" && isNullable == "YES") return;
            }

            using (MySqlCommand alter = new MySqlCommand("ALTER TABLE campus_dynamics.acad_results MODIFY COLUMN result_comment TEXT NULL", conn))
            {
                alter.ExecuteNonQuery();
            }
        });
    }

    private static void SafeSelect(DropDownList ddl, string val)
    {
        ListItem item = ddl.Items.FindByValue(val);
        if (item != null) item.Selected = true;
    }

    // Cached — see MarksSchemaCache. Previously fired twice per published row.
    private static bool ColumnExists(MySqlConnection conn, string schema, string table, string column)
    {
        return MarksSchemaCache.ColumnExists(conn, schema, table, column);
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

    // ═══════════════════════════════════════════════════════════════════════════
    //  ADMIN COURSE-REGISTRATION MANAGEMENT (AllMarksController)
    //  Register a student to a course, or delete a registration — mirroring EXACTLY
    //  the eportal lecturer/admin procedure (CampusDynamics_Portal
    //  AdminCourseRegistrationController.EnrollStudentToLecturerCourse / RunInsert):
    //  same table, same course_status='REGULAR', same schema-aware columns, same
    //  duplicate + course-exists guards, relying on the DB defaults so a new row
    //  auto-starts mark_stage=NOT_ENTERED / provisional_marks_status=not_entered.
    //  All actions are scope-gated (admin=all, dean=faculty, HOD=department) and the
    //  controller logs every create/delete into acad_marks_action_log.
    // ═══════════════════════════════════════════════════════════════════════════

    // Register a student to a course for a given academic year + semester.
    public static string CreateRegistration(string regno, string courseID, string acadYear, int semester)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        regno = (regno ?? "").Trim();
        courseID = (courseID ?? "").Trim().ToUpperInvariant();
        acadYear = (acadYear ?? "").Trim();
        if (string.IsNullOrEmpty(regno) || string.IsNullOrEmpty(courseID) || string.IsNullOrEmpty(acadYear) || semester < 1 || semester > 3)
            return js.Serialize(new { success = false, message = "Reg No, Course, Academic Year and a valid Semester (1-3) are required." });

        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();

            // Resolve the student + their programme (the registration is stamped with prog_id, exactly like eportal).
            string progId = "", studName = "";
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT COALESCE(progid,'') AS prog_id, NULLIF(TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))),'') AS nm FROM acad_student WHERE regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (!rdr.Read()) return js.Serialize(new { success = false, message = "Student not found: " + regno + "." });
                    progId = rdr["prog_id"].ToString().Trim();
                    studName = rdr["nm"] == DBNull.Value ? "" : rdr["nm"].ToString();
                }
            }

            // Scope: a dean/HOD may only register students within their faculty/department; admin = any.
            MarksScope scope = CurrentScope();
            if (!scope.AllowsProg(progId))
                return js.Serialize(new { success = false, message = "This student's programme is outside your faculty/department scope." });

            // Course must exist (eportal check; TRIM-tolerant because some acad_course.courseID rows carry stray spaces).
            using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_course WHERE TRIM(courseID)=@c", conn))
            {
                cmd.Parameters.AddWithValue("@c", courseID);
                if (Convert.ToInt32(cmd.ExecuteScalar()) <= 0)
                    return js.Serialize(new { success = false, message = "Course not found: " + courseID + "." });
            }

            string courseCol = GetCourseColumnExpression(conn, "");
            if (string.IsNullOrEmpty(courseCol))
                return js.Serialize(new { success = false, message = "Course column not found on acad_course_registration." });

            // No duplicate registration for the same period (TRIM-tolerant for the same reason).
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration WHERE regno=@r AND TRIM(" + courseCol + ")=@c AND acad_year=@a AND semester=@s", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.Parameters.AddWithValue("@c", courseID);
                cmd.Parameters.AddWithValue("@a", acadYear);
                cmd.Parameters.AddWithValue("@s", semester);
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    return js.Serialize(new { success = false, message = "Student is already registered for this course in the selected period." });
            }

            // Schema-aware INSERT — identical shape to eportal RunInsert. mark_stage / provisional_marks_status
            // are intentionally omitted so the column defaults (NOT_ENTERED / not_entered) apply.
            bool hasCreatedDate = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "created_date");
            bool hasStudSession = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "stud_session");
            string cols = "regno," + courseCol + ",prog_id,acad_year,semester,course_status";
            string vals = "@r,@c,@p,@a,@s,'REGULAR'";
            if (hasStudSession) { cols += ",stud_session"; vals += ",@ss"; }
            if (hasCreatedDate) { cols += ",created_date"; vals += ",NOW()"; }
            string sql = "INSERT INTO campus_dynamics_portal.acad_course_registration (" + cols + ") VALUES(" + vals + ")";

            long newId;
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.Parameters.AddWithValue("@c", courseID);
                cmd.Parameters.AddWithValue("@p", progId);
                cmd.Parameters.AddWithValue("@a", acadYear);
                cmd.Parameters.AddWithValue("@s", semester);
                if (hasStudSession) cmd.Parameters.AddWithValue("@ss", "Day");
                cmd.ExecuteNonQuery();
                newId = cmd.LastInsertedId;
            }

            return js.Serialize(new
            {
                success = true,
                message = "Registered " + regno + (studName != "" ? " (" + studName + ")" : "") + " to " + courseID + " for " + acadYear + ", Semester " + semester + ".",
                id = newId,
                regno = regno,
                courseID = courseID
            });
        }
    }

    // Delete a single course-registration row. Blocked when the record is already PUBLISHED /
    // has a row in acad_results (final results + GPA are derived from acad_results, so deleting the
    // registration alone would orphan the transcript). Scope-gated like every other mutation.
    public static string DeleteRegistration(int id) { return DeleteRegistration(id, false); }

    /// <summary>
    /// Delete a course registration. If it has PUBLISHED / final results in acad_results the
    /// delete is blocked (returns canForce=true) UNLESS force=true, in which case it CASCADES:
    /// removes the published acad_results row(s), deletes the registration, and recomputes the
    /// student's semester GPA — all in one audited transaction (the acad_results audit trigger
    /// captures the removed final results, so this stays recoverable).
    /// </summary>
    public static string DeleteRegistration(int id, bool force)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        if (id <= 0) return js.Serialize(new { success = false, message = "Invalid record id." });

        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            if (!RecordInScope(conn, id)) return SCOPE_DENIED;

            string courseCol = GetCourseColumnExpression(conn, "");
            if (string.IsNullOrEmpty(courseCol))
                return js.Serialize(new { success = false, message = "Course column not found on acad_course_registration." });

            string regno = "", courseID = "", acadYear = "", provStatus = "", markStage = "";
            int semester = 0;
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT regno, " + courseCol + " AS courseID, COALESCE(acad_year,'') AS acad_year, COALESCE(semester,0) AS semester, " +
                "LOWER(COALESCE(provisional_marks_status,'')) AS pstat, UPPER(COALESCE(mark_stage,'')) AS mstage " +
                "FROM campus_dynamics_portal.acad_course_registration WHERE id=@id LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (!rdr.Read()) return js.Serialize(new { success = false, message = "Registration record not found (id " + id + ")." });
                    regno = rdr["regno"].ToString().Trim();
                    courseID = rdr["courseID"].ToString().Trim();
                    acadYear = rdr["acad_year"].ToString().Trim();
                    semester = Convert.ToInt32(rdr["semester"]);
                    provStatus = rdr["pstat"].ToString().Trim();
                    markStage = rdr["mstage"].ToString().Trim();
                }
            }

            bool isPublished = provStatus == StatusPublished || markStage == "PUBLISHED";
            bool hasResult = false;
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM acad_results WHERE TRIM(regno)=@r AND TRIM(courseid)=@c AND TRIM(acad)=@a AND semester=@s", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.Parameters.AddWithValue("@c", courseID);
                cmd.Parameters.AddWithValue("@a", acadYear);
                cmd.Parameters.AddWithValue("@s", semester);
                hasResult = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            if (isPublished || hasResult)
            {
                if (!force)
                    return js.Serialize(new
                    {
                        success = false,
                        canForce = true,
                        message = "This registration has PUBLISHED / final results in acad_results. Deleting it will ALSO remove the published result and recompute the student's GPA. Tick “Force delete” to proceed."
                    });

                // ── FORCE cascade: remove the published result(s), delete the registration,
                //    recompute semester GPA — all audited, in one transaction. ──
                string actor = MarksAuthorizationService.GetCurrentUser();
                using (MySqlTransaction tx = conn.BeginTransaction())
                {
                    try
                    {
                        // Attribute the acad_results deletion so the audit trigger records who/why.
                        SetMarkAuditContext(conn, tx, actor, "AllMarks:force-delete-registration",
                            "Force-deleted registration + published result by " + actor);

                        int delResults;
                        using (MySqlCommand del = new MySqlCommand(
                            "DELETE FROM acad_results WHERE TRIM(regno)=@r AND TRIM(courseid)=@c AND TRIM(acad)=@a AND semester=@s", conn, tx))
                        {
                            del.Parameters.AddWithValue("@r", regno);
                            del.Parameters.AddWithValue("@c", courseID);
                            del.Parameters.AddWithValue("@a", acadYear);
                            del.Parameters.AddWithValue("@s", semester);
                            delResults = del.ExecuteNonQuery();
                        }

                        int delReg;
                        using (MySqlCommand del = new MySqlCommand(
                            "DELETE FROM campus_dynamics_portal.acad_course_registration WHERE id=@id", conn, tx))
                        {
                            del.Parameters.AddWithValue("@id", id);
                            delReg = del.ExecuteNonQuery();
                        }
                        if (delReg <= 0)
                        {
                            tx.Rollback();
                            return js.Serialize(new { success = false, message = "No record was deleted (it may already have been removed)." });
                        }

                        // Recompute the semester GPA off the remaining results and stamp it on them.
                        decimal semesterGpa = ComputeSemesterGpa(conn, tx, regno, acadYear, semester);
                        decimal cgpa = ComputeStudentCgpa(conn, tx, regno);
                        using (MySqlCommand gpaUpd = new MySqlCommand(
                            "UPDATE acad_results SET gpa=@g WHERE regno=@r AND acad=@a AND semester=@s", conn, tx))
                        {
                            gpaUpd.Parameters.AddWithValue("@g", semesterGpa);
                            gpaUpd.Parameters.AddWithValue("@r", regno);
                            gpaUpd.Parameters.AddWithValue("@a", acadYear);
                            gpaUpd.Parameters.AddWithValue("@s", semester);
                            gpaUpd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return js.Serialize(new
                        {
                            success = true,
                            message = "Force-deleted registration and " + delResults + " published result row(s): " + regno + " - " + courseID +
                                      " (" + acadYear + ", Semester " + semester + "). Recomputed Semester GPA " + semesterGpa.ToString("F2") +
                                      ", CGPA " + cgpa.ToString("F2") + ".",
                            regno = regno,
                            courseID = courseID
                        });
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        return js.Serialize(new { success = false, message = "Force delete failed: " + ex.Message });
                    }
                }
            }

            int rows;
            using (MySqlCommand cmd = new MySqlCommand("DELETE FROM campus_dynamics_portal.acad_course_registration WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                rows = cmd.ExecuteNonQuery();
            }
            if (rows <= 0) return js.Serialize(new { success = false, message = "No record was deleted (it may already have been removed)." });

            return js.Serialize(new
            {
                success = true,
                message = "Deleted registration: " + regno + " - " + courseID + " (" + acadYear + ", Semester " + semester + ").",
                regno = regno,
                courseID = courseID
            });
        }
    }

    // Student search for the "Register Student to Course" picker (scope-filtered). Mirrors eportal SearchStudents.
    public static string RegSearchStudents(string q)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        q = (q ?? "").Trim();
        List<object> list = new List<object>();
        if (q.Length < 2) return js.Serialize(new { success = true, students = list });

        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string scopeFilter = CurrentScope().ProgFilter("s", "progid");
            string sql =
                "SELECT s.regno, IFNULL(s.entryno,'') AS entryno, " +
                "NULLIF(TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))),'') AS stud_name, " +
                "IFNULL(s.stud_status,'') AS stud_status, IFNULL(s.progid,'') AS prog_id, " +
                "CONCAT(IFNULL(s.progid,''), CASE WHEN p.progname IS NOT NULL THEN CONCAT(' - ', p.progname) ELSE '' END) AS prog_name " +
                "FROM acad_student s LEFT JOIN acad_programme p ON p.progcode=s.progid " +
                "WHERE (s.regno LIKE @q OR s.entryno LIKE @q " +
                "  OR CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,'')) LIKE @q " +
                "  OR CONCAT(IFNULL(s.othername,''),' ',IFNULL(s.firstname,'')) LIKE @q " +
                "  OR s.email LIKE @q OR s.national_id LIKE @q)" + scopeFilter +
                " ORDER BY CASE WHEN UPPER(s.regno)=@qx THEN 0 WHEN UPPER(IFNULL(s.entryno,''))=@qx THEN 1 " +
                "               WHEN UPPER(s.regno) LIKE @qstart THEN 2 ELSE 3 END, s.regno ASC LIMIT 15";
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@q", "%" + q + "%");
                cmd.Parameters.AddWithValue("@qx", q.ToUpperInvariant());
                cmd.Parameters.AddWithValue("@qstart", q.ToUpperInvariant() + "%");
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                        list.Add(new
                        {
                            regno = rdr["regno"].ToString(),
                            entryno = rdr["entryno"].ToString(),
                            name = rdr["stud_name"] == DBNull.Value ? "" : rdr["stud_name"].ToString(),
                            status = rdr["stud_status"].ToString(),
                            prog_id = rdr["prog_id"].ToString(),
                            prog = rdr["prog_name"].ToString()
                        });
                }
            }
        }
        return js.Serialize(new { success = true, students = list });
    }

    // Course search for the "Register Student to Course" picker. Mirrors eportal SearchCourses.
    public static string RegSearchCourses(string q)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        q = (q ?? "").Trim();
        List<object> list = new List<object>();
        if (q.Length < 2) return js.Serialize(new { success = true, courses = list });

        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT UPPER(TRIM(courseID)) AS course_code, IFNULL(courseName,'') AS course_name " +
                "FROM acad_course WHERE courseID LIKE @q OR courseName LIKE @q " +
                "ORDER BY CASE WHEN UPPER(TRIM(courseID)) LIKE @qstart THEN 0 ELSE 1 END, courseID ASC LIMIT 30", conn))
            {
                cmd.Parameters.AddWithValue("@q", "%" + q + "%");
                cmd.Parameters.AddWithValue("@qstart", q.ToUpperInvariant() + "%");
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                        list.Add(new { code = rdr["course_code"].ToString(), name = rdr["course_name"].ToString() });
                }
            }
        }
        return js.Serialize(new { success = true, courses = list });
    }

    // Student context for the register modal: name, programme, scope flag + existing registrations by period.
    public static string GetStudentRegSummary(string regno)
    {
        JavaScriptSerializer js = new JavaScriptSerializer();
        regno = (regno ?? "").Trim();
        if (string.IsNullOrEmpty(regno)) return js.Serialize(new { success = false, message = "Registration number required." });

        using (MySqlConnection conn = new MySqlConnection(ConnectionString))
        {
            conn.Open();
            string name = "", prog = "", progId = "";
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT NULLIF(TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))),'') AS nm, IFNULL(s.progid,'') AS prog_id, " +
                "CONCAT(IFNULL(p.progcode,''),' - ',IFNULL(p.progname,'')) AS prog_name " +
                "FROM acad_student s LEFT JOIN acad_programme p ON p.progcode=s.progid WHERE s.regno=@r LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (!rdr.Read()) return js.Serialize(new { success = false, message = "Student not found." });
                    name = rdr["nm"] == DBNull.Value ? "" : rdr["nm"].ToString();
                    progId = rdr["prog_id"].ToString().Trim();
                    prog = rdr["prog_name"].ToString().Trim();
                }
            }

            bool inScope = CurrentScope().AllowsProg(progId);
            List<object> existing = new List<object>();
            string courseCol = GetCourseColumnExpression(conn, "cr");
            if (!string.IsNullOrEmpty(courseCol))
            {
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT COALESCE(cr.acad_year,'') AS acad_year, COALESCE(cr.semester,0) AS semester, " +
                    "GROUP_CONCAT(DISTINCT " + courseCol + " ORDER BY " + courseCol + " SEPARATOR ', ') AS courses, COUNT(*) AS n " +
                    "FROM campus_dynamics_portal.acad_course_registration cr WHERE cr.regno=@r " +
                    "GROUP BY cr.acad_year, cr.semester ORDER BY cr.acad_year DESC, cr.semester DESC LIMIT 40", conn))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            existing.Add(new
                            {
                                acad_year = rdr["acad_year"].ToString(),
                                semester = rdr["semester"].ToString(),
                                courses = rdr["courses"] == DBNull.Value ? "" : rdr["courses"].ToString(),
                                n = Convert.ToInt32(rdr["n"])
                            });
                    }
                }
            }
            return js.Serialize(new { success = true, regno = regno, name = name, prog = prog, prog_id = progId, in_scope = inScope, existing = existing });
        }
    }

    private static string GetCourseColumnExpression(MySqlConnection conn, string alias)
    {
        bool hasCourseId = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "courseID");
        bool hasCourseCode = ColumnExists(conn, "campus_dynamics_portal", "acad_course_registration", "course_code");

        string prefix = string.IsNullOrEmpty(alias) ? string.Empty : alias + ".";
        if (hasCourseId) return prefix + "courseID";
        if (hasCourseCode) return prefix + "course_code";
        return null;
    }

    private static string ResolveCourseCreditUnitsColumn(MySqlConnection conn)
    {
        string[] candidates = new string[] { "CreditUnits", "creditunits", "credit_units", "creditunit", "credit_unit", "cu" };
        for (int i = 0; i < candidates.Length; i++)
        {
            string existing = GetActualColumnName(conn, "campus_dynamics", "acad_course", candidates[i]);
            if (!string.IsNullOrEmpty(existing)) return existing;
        }
        return null;
    }

    private static string ResolveResultsCreditUnitsColumn(MySqlConnection conn)
    {
        string[] candidates = new string[] { "CreditUnits", "creditunits", "credit_units", "creditunit", "credit_unit", "cu" };
        for (int i = 0; i < candidates.Length; i++)
        {
            string existing = GetActualColumnName(conn, "campus_dynamics", "acad_results", candidates[i]);
            if (!string.IsNullOrEmpty(existing)) return existing;
        }

        try { EnsureNullableColumn(conn, "campus_dynamics", "acad_results", "CreditUnits", "INT NULL"); } catch { }
        return "CreditUnits";
    }

    // Cached: schema shape cannot change mid-request, and this used to fire per published row.
    private static string GetActualColumnName(MySqlConnection conn, string schema, string table, string columnCandidate)
    {
        return MarksSchemaCache.ActualColumnName(conn, schema, table, columnCandidate);
    }

}
