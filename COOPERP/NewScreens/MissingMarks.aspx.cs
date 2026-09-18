using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Missing Marks — the students who cannot see a mark they should be able to see.
///
/// Built 2026-09-18 after complaints about lost marks. Every class below was found by
/// investigation, and each one was invisible until a student happened to complain:
///
///   A  Overwritten by another term. acad_results holds one row per (student, course) —
///      deliberately, so a retake shows the course once with its new grade. But an ordinary
///      second registration of the same course gets the same treatment with none of the
///      safeguards: it lands on the earlier term's row, overwrites that mark, and leaves this
///      term showing nothing. Publishing now refuses this (MarksControllerShared), so the list
///      only shrinks.
///   B  Marked published, no result row at all.
///   C  Marks entered and never published — approved by the Dean and then left.
///   D  The result was removed and the marks never re-entered — a marks reset or a course
///      deletion that nobody closed out.
///
/// The classes are mutually exclusive, so a student appears once with one thing to do.
/// Read-only: this page diagnoses, it never writes.
/// </summary>
public partial class COOPERP_NewScreens_MissingMarks : Page
{
    private const string ClsOverwritten = "A";
    private const string ClsNoResult    = "B";
    private const string ClsUnpublished = "C";
    private const string ClsRemoved     = "D";

    private static readonly int[] PageSizes = new int[] { 25, 50, 100, 200 };
    private const int DefaultPageSize = 50;

    private string _year = "";
    private string _sem = "";
    private string _prog = "";
    private string _cls = ClsOverwritten;
    private string _q = "";
    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    private MarksScope _scope;
    private bool _unrestricted;

    private static string ConnStr { get { return MarksConfiguration.ConnStr; } }

    // ────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────────────────────────────

    protected void Page_Load(object sender, EventArgs e)
    {
        ReadRequest();

        _scope = MarksScopeResolver.Resolve();
        _unrestricted = _scope.IsAdmin;
        bool allowed = _scope.IsAdmin
                    || (_scope.AllowedProgCodes != null && _scope.AllowedProgCodes.Count > 0);
        if (!allowed)
        {
            try { allowed = MarksAuthorizationService.CanViewAudit(); if (allowed) _unrestricted = true; }
            catch { }
        }
        if (!allowed) { RenderDenied(); return; }

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                if (_year.Length == 0) _year = LatestYear(conn);
                LoadStatusVocabulary(conn);

                if (string.Equals(Request["export"], "csv", StringComparison.OrdinalIgnoreCase))
                {
                    ExportCsv(conn);
                    return;
                }

                RenderHeader();
                LoadFilters(conn);
                RenderClasses(conn);
                RenderTable(conn);
            }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            litNotice.Text += "<div class='mm-note mm-note--error'><b>The report could not be built.</b><br />"
                            + Enc(ex.Message) + "</div>";
        }
    }

    private void ReadRequest()
    {
        System.Collections.Specialized.NameValueCollection qs = Request.QueryString;
        _year = Trim(qs["year"], 25);
        _sem = Trim(qs["sem"], 2);
        _prog = Trim(qs["prog"], 25);
        _q = Trim(qs["q"], 40);

        _cls = (qs["cls"] ?? "").Trim().ToUpperInvariant();
        if (_cls != ClsOverwritten && _cls != ClsNoResult && _cls != ClsUnpublished && _cls != ClsRemoved)
            _cls = ClsOverwritten;

        if (_sem != "1" && _sem != "2" && _sem != "3") _sem = "";

        int.TryParse(qs["p"], out _page);
        if (_page < 1) _page = 1;

        int ps;
        int.TryParse(qs["ps"], out ps);
        _pageSize = DefaultPageSize;
        for (int i = 0; i < PageSizes.Length; i++) if (PageSizes[i] == ps) _pageSize = ps;
    }

    private void RenderDenied()
    {
        pnlMain.Visible = false;
        litHeadSub.Text = "Students who cannot see a mark they should be able to see.";
        litNotice.Text =
            "<div class='mm-note mm-note--error'><b>You do not have access to this report.</b><br />" +
            "It is open to administrators, deans, heads of department, the registrar and exam officers.</div>";
    }

    private void RenderHeader()
    {
        string sub = "Every student who is missing a mark right now, and what closes it. "
                   + "Found by reconciling what the marks screens hold against what the student's results page can read.";
        if (!_unrestricted && _scope.Label != null && _scope.Label.Length > 0)
            sub += " Showing " + Enc(_scope.Label) + ".";
        litHeadSub.Text = sub;

        litExportBtn.Text = "<a href='" + Enc(Url("export", "csv", "p", null)) + "' class='mm-btn mm-btn--inv mm-btn--sm'>"
            + "<svg width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' "
            + "stroke-linecap='round' stroke-linejoin='round'><path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/>"
            + "<polyline points='7 10 12 15 17 10'/><line x1='12' y1='15' x2='12' y2='3'/></svg>Download CSV</a>";
    }

    // ────────────────────────────────────────────────────────────────────
    //  The four detections
    // ────────────────────────────────────────────────────────────────────

    private const string ResultHere =
        "EXISTS (SELECT 1 FROM acad_results r WHERE r.regno = cr.regno AND r.courseid = cr.courseID " +
        "        AND r.acad = cr.acad_year AND r.semester = cr.semester)";

    private const string ResultAnywhere =
        "EXISTS (SELECT 1 FROM acad_results r2 WHERE r2.regno = cr.regno AND r2.courseid = cr.courseID)";

    /// <summary>
    /// The WHERE for one class. The four are mutually exclusive by construction: A and B need a
    /// published registration carrying marks, C needs marks that were never published, and D
    /// needs a registration whose marks are gone altogether.
    /// </summary>
    private string ClassWhere(string cls)
    {
        StringBuilder sb = new StringBuilder(" WHERE 1=1 ");
        if (_year.Length > 0) sb.Append(" AND cr.acad_year = @year ");
        if (_sem.Length > 0) sb.Append(" AND cr.semester = @sem ");
        if (_prog.Length > 0) sb.Append(" AND cr.prog_id = @prog ");
        if (_q.Length > 0) sb.Append(" AND (cr.regno LIKE @q OR cr.courseID LIKE @q) ");
        sb.Append(_scope.ProgFilter("cr", "prog_id"));

        if (cls == ClsOverwritten)
            sb.Append(" AND cr.provisional_marks_status = 'published' AND cr.provisional_total_marks IS NOT NULL ")
              .Append(" AND NOT ").Append(ResultHere).Append(" AND ").Append(ResultAnywhere);
        else if (cls == ClsNoResult)
            sb.Append(" AND cr.provisional_marks_status = 'published' AND cr.provisional_total_marks IS NOT NULL ")
              .Append(" AND NOT ").Append(ResultAnywhere);
        else if (cls == ClsUnpublished)
            sb.Append(" AND cr.provisional_total_marks IS NOT NULL ").Append(_notPublishedSql);
        else
            // The audit side carries about a thousand deletions; the registration side carries
            // seven hundred thousand rows. Starting from the audit turns a table scan into a
            // handful of index seeks — see ClassFrom.
            sb.Append(" AND cr.provisional_total_marks IS NULL ")
              .Append(" AND a.action_type = 'DELETE' AND a.old_total > 0 ")
              .Append(" AND NOT ").Append(ResultAnywhere);

        return sb.ToString();
    }

    /// <summary>
    /// The FROM for one class. Only D changes shape: it is a question about deletions, so it
    /// reads from the audit and joins the registration, not the other way round.
    /// </summary>
    private static string ClassFrom(string cls)
    {
        if (cls == ClsRemoved)
            return " FROM acad_marks_audit a " +
                   " JOIN campus_dynamics_portal.acad_course_registration cr " + OrderHint +
                   "   ON cr.regno = a.regno AND cr.courseID = a.course_id " +
                   "  AND cr.acad_year = a.acad_year AND cr.semester = a.semester ";
        return " FROM campus_dynamics_portal.acad_course_registration cr " + OrderHint;
    }

    /// <summary>
    /// Without this the optimiser sees "ORDER BY regno, courseID LIMIT 25", picks the index that
    /// already delivers that order, and walks 700,000 rows looking for the forty that match:
    /// 10.3 seconds against 0.38 with the filter applied first. The hint only bars that index
    /// from being used FOR ORDERING — it is still free to use it for lookups and joins.
    /// </summary>
    private const string OrderHint = " IGNORE INDEX FOR ORDER BY (Index_UNQ, PRIMARY) ";

    /// <summary>
    /// "Anything but published", written so the index can be used. An inequality on the leading
    /// column of idx_acr_pubstatus_term forces a scan; a list of the values that actually occur
    /// is a set of index ranges. The list is read from the data rather than hard-coded, so a
    /// status invented later is still counted — and if that read fails we fall back to the
    /// inequality, which is slower but never wrong.
    /// </summary>
    private string _notPublishedSql = " AND IFNULL(cr.provisional_marks_status,'') <> 'published' ";

    private void LoadStatusVocabulary(MySqlConnection conn)
    {
        try
        {
            List<string> vals = new List<string>();
            bool sawNull = false;
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT DISTINCT provisional_marks_status FROM campus_dynamics_portal.acad_course_registration", conn))
            {
                cmd.CommandTimeout = 60;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        if (r.IsDBNull(0)) { sawNull = true; continue; }
                        string v = Str(r[0]);
                        if (v.Length == 0) { sawNull = true; continue; }
                        if (!string.Equals(v, "published", StringComparison.OrdinalIgnoreCase)) vals.Add(v);
                    }
            }
            if (vals.Count == 0 && !sawNull) return;

            StringBuilder sb = new StringBuilder(" AND (");
            if (vals.Count > 0)
                sb.Append("cr.provisional_marks_status IN (").Append(InList(vals)).Append(")");
            if (sawNull)
                sb.Append(vals.Count > 0 ? " OR " : "")
                  .Append("cr.provisional_marks_status IS NULL OR cr.provisional_marks_status = ''");
            sb.Append(") ");
            _notPublishedSql = sb.ToString();
        }
        catch { }
    }

    private void Bind(MySqlCommand cmd)
    {
        if (_year.Length > 0) cmd.Parameters.AddWithValue("@year", _year);
        if (_sem.Length > 0) cmd.Parameters.AddWithValue("@sem", _sem);
        if (_prog.Length > 0) cmd.Parameters.AddWithValue("@prog", _prog);
        if (_q.Length > 0) cmd.Parameters.AddWithValue("@q", "%" + _q + "%");
    }

    private long CountOf(MySqlConnection conn, string cls)
    {
        try
        {
            // DISTINCT only where it is needed: a registration can carry more than one
            // deletion in the audit, but nothing else here can produce the same row twice,
            // and COUNT(DISTINCT) on a large set costs a materialisation.
            using (MySqlCommand cmd = new MySqlCommand(
                (cls == ClsRemoved ? "SELECT COUNT(DISTINCT cr.ID) " : "SELECT COUNT(*) ")
                + ClassFrom(cls) + ClassWhere(cls), conn))
            {
                Bind(cmd);
                cmd.CommandTimeout = 120;
                object v = cmd.ExecuteScalar();
                return (v == null || v == DBNull.Value) ? 0 : Convert.ToInt64(v);
            }
        }
        catch { return -1; }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Rendering
    // ────────────────────────────────────────────────────────────────────

    private sealed class Info
    {
        public string Title, Sub, Fix;
        public bool Loss;
    }

    private static Info Describe(string cls)
    {
        Info i = new Info();
        if (cls == ClsOverwritten)
        {
            i.Title = "Overwritten by another term";
            i.Sub = "the mark landed on this course's earlier attempt";
            i.Fix = "This mark was written onto the student's earlier attempt at the same course, which holds only one result. "
                  + "If the student is genuinely sitting it again, put it through Retake Registration. If the registration is "
                  + "in the wrong term or is a duplicate, fix it in the Course Correction Centre.";
            i.Loss = true;
        }
        else if (cls == ClsNoResult)
        {
            i.Title = "Published, but no result row";
            i.Sub = "the publish never reached the results table";
            i.Fix = "Publish the mark again from All Marks. If it refuses, the message says which term is holding the result.";
        }
        else if (cls == ClsUnpublished)
        {
            i.Title = "Entered, never published";
            i.Sub = "the mark exists but was never released";
            i.Fix = "Nothing is lost. Move it through the remaining stages and publish it.";
        }
        else
        {
            i.Title = "Removed, never re-entered";
            i.Sub = "a reset or deletion nobody closed out";
            i.Fix = "The mark was deliberately cleared and no replacement was entered. The lecturer needs to enter it again, "
                  + "or the registration should be removed if the student is not taking the course.";
        }
        return i;
    }

    private void RenderClasses(MySqlConnection conn)
    {
        string[] order = new string[] { ClsOverwritten, ClsNoResult, ClsUnpublished, ClsRemoved };
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < order.Length; i++)
        {
            string cls = order[i];
            Info info = Describe(cls);
            long n = CountOf(conn, cls);

            sb.Append("<a class='mm-class").Append(cls == _cls ? " mm-class--on" : "")
              .Append(info.Loss ? " mm-class--loss" : "").Append("' href='")
              .Append(Enc(Url("cls", cls, "p", null))).Append("'>")
              .Append("<div class='mm-class__n'>").Append(n < 0 ? "?" : N(n)).Append("</div>")
              .Append("<div class='mm-class__t'>").Append(Enc(info.Title)).Append("</div>")
              .Append("<div class='mm-class__s'>").Append(Enc(info.Sub)).Append("</div></a>");
        }
        litClasses.Text = sb.ToString();

        Info active = Describe(_cls);
        litCardTitle.Text = Enc(active.Title);
    }

    private void RenderTable(MySqlConnection conn)
    {
        long total = CountOf(conn, _cls);
        if (total < 0) total = 0;
        int pageCount = (int)((total + _pageSize - 1) / _pageSize);
        if (pageCount < 1) pageCount = 1;
        if (_page > pageCount) _page = pageCount;

        List<string[]> rows = new List<string[]>();
        List<string> regnos = new List<string>(), courses = new List<string>();

        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT cr.ID, cr.regno, cr.courseID, cr.acad_year, cr.semester, cr.prog_id, " +
            "       cr.provisional_course_work_marks cw, cr.provisional_exam_marks ex, " +
            "       cr.provisional_total_marks tot, IFNULL(cr.provisional_marks_status,'') st, " +
            "       IFNULL(cr.mark_stage,'') stg, " +
            (_cls == ClsOverwritten
                ? "(SELECT CONCAT(r.acad,' Sem ',r.semester,' holds ',IFNULL(r.score,'-'),' / ',IFNULL(r.grade,'-')) " +
                  " FROM acad_results r WHERE r.regno=cr.regno AND r.courseid=cr.courseID LIMIT 1) extra "
                : _cls == ClsRemoved
                ? "(SELECT CONCAT('was ',a.old_total,' / ',IFNULL(a.old_grade,'-'),', removed ',DATE(a.created_at), " +
                  "        ' by ',a.performed_by) FROM acad_marks_audit a WHERE a.action_type='DELETE' AND a.old_total>0 " +
                  "  AND a.regno=cr.regno AND a.course_id=cr.courseID AND a.acad_year=cr.acad_year " +
                  "  AND a.semester=cr.semester ORDER BY a.audit_id DESC LIMIT 1) extra "
                : "'' extra ") +
            ClassFrom(_cls) + ClassWhere(_cls) +
            (_cls == ClsRemoved ? " GROUP BY cr.ID " : " ") +
            " ORDER BY cr.regno, cr.courseID LIMIT " + _pageSize +
            " OFFSET " + ((_page - 1) * _pageSize), conn))
        {
            Bind(cmd);
            cmd.CommandTimeout = 120;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string[] row = new string[] {
                        Str(r["regno"]), Str(r["courseID"]), Str(r["acad_year"]), Str(r["semester"]),
                        Str(r["prog_id"]), Str(r["cw"]), Str(r["ex"]), Str(r["tot"]),
                        Str(r["st"]), Str(r["stg"]), Str(r["extra"])
                    };
                    rows.Add(row);
                    if (row[0].Length > 0 && !regnos.Contains(row[0])) regnos.Add(row[0]);
                    if (row[1].Length > 0 && !courses.Contains(row[1])) courses.Add(row[1]);
                }
        }

        Dictionary<string, string> names = Lookup(conn, regnos,
            "SELECT regno k, CONCAT(IFNULL(firstname,''),' ',IFNULL(othername,'')) v FROM acad_student WHERE regno IN (");
        Dictionary<string, string> titles = Lookup(conn, courses,
            "SELECT courseID k, courseName v FROM acad_course WHERE courseID IN (");

        Info info = Describe(_cls);
        StringBuilder sb = new StringBuilder();

        if (rows.Count == 0)
        {
            sb.Append("<tr><td colspan='6'><div class='mm-empty'><b>Nothing here</b>")
              .Append("No student in this period is affected by this. That is the number you want.")
              .Append("</div></td></tr>");
        }
        else
        {
            for (int i = 0; i < rows.Count; i++)
            {
                string[] x = rows[i];
                sb.Append("<tr>");
                sb.Append("<td><span class='mm-code'>").Append(Enc(x[0])).Append("</span>");
                string nm = Get(names, x[0]);
                if (nm.Length > 0) sb.Append("<span class='mm-sub'>").Append(Enc(nm)).Append("</span>");
                sb.Append("</td>");

                sb.Append("<td><span class='mm-code'>").Append(Enc(x[1])).Append("</span>");
                string ti = Get(titles, x[1]);
                if (ti.Length > 0) sb.Append("<span class='mm-sub'>").Append(Enc(Clip(ti, 42))).Append("</span>");
                sb.Append("</td>");

                sb.Append("<td>").Append(Enc(x[2])).Append("<span class='mm-sub'>Sem ").Append(Enc(x[3]));
                if (x[4].Length > 0) sb.Append(" &middot; ").Append(Enc(x[4]));
                sb.Append("</span></td>");

                sb.Append("<td>");
                if (x[7].Length > 0)
                    sb.Append("<b>").Append(Enc(x[7])).Append("</b><span class='mm-sub'>CW ").Append(Enc(Dash(x[5])))
                      .Append(" &middot; Exam ").Append(Enc(Dash(x[6]))).Append("</span>");
                else
                    sb.Append("<span class='mm-nil'>&mdash;</span>");
                if (x[8].Length > 0)
                    sb.Append("<span class='mm-sub'>").Append(Enc(x[8])).Append("</span>");
                sb.Append("</td>");

                sb.Append("<td><span class='mm-what'>").Append(Enc(info.Title)).Append("</span>");
                if (x[10].Length > 0)
                    sb.Append("<span class='mm-what__sub'>").Append(Enc(x[10])).Append("</span>");
                sb.Append("</td>");

                sb.Append("<td><div class='mm-fix'>").Append(Enc(info.Fix)).Append("</div></td>");
                sb.Append("</tr>");
            }
        }

        litRows.Text = sb.ToString();
        litMeta.Text = N(total) + (total == 1 ? " student" : " students");
        litPsOpts.Text = PageSizeOptions();
        RenderPager(total, pageCount);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Filters
    // ────────────────────────────────────────────────────────────────────

    private string LatestYear(MySqlConnection conn)
    {
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT MAX(acad_year) FROM campus_dynamics_portal.acad_course_registration", conn))
            {
                cmd.CommandTimeout = 60;
                object v = cmd.ExecuteScalar();
                return (v == null || v == DBNull.Value) ? "" : v.ToString().Trim();
            }
        }
        catch { return ""; }
    }

    private void LoadFilters(MySqlConnection conn)
    {
        litQ.Text = Enc(_q);

        StringBuilder years = new StringBuilder();
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT DISTINCT acad_year FROM campus_dynamics_portal.acad_course_registration " +
                "WHERE acad_year IS NOT NULL AND acad_year <> '' ORDER BY acad_year DESC LIMIT 40", conn))
            {
                cmd.CommandTimeout = 60;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string v = Str(r[0]);
                        years.Append("<option value='").Append(Enc(v)).Append("'")
                             .Append(v == _year ? " selected='selected'" : "").Append(">")
                             .Append(Enc(v)).Append("</option>");
                    }
            }
        }
        catch { }
        litYearOpts.Text = years.ToString();

        litSemOpts.Text = Opt("", "Every semester", _sem) + Opt("1", "Semester 1", _sem)
                        + Opt("2", "Semester 2", _sem) + Opt("3", "Semester 3", _sem);

        StringBuilder progs = new StringBuilder("<option value=''>Every programme</option>");
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT DISTINCT cr.prog_id FROM campus_dynamics_portal.acad_course_registration cr " +
                "WHERE cr.acad_year = @year AND cr.prog_id IS NOT NULL AND cr.prog_id <> '' " +
                _scope.ProgFilter("cr", "prog_id") + " ORDER BY cr.prog_id LIMIT 300", conn))
            {
                cmd.Parameters.AddWithValue("@year", _year);
                cmd.CommandTimeout = 60;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string v = Str(r[0]);
                        progs.Append("<option value='").Append(Enc(v)).Append("'")
                             .Append(v == _prog ? " selected='selected'" : "").Append(">")
                             .Append(Enc(v)).Append("</option>");
                    }
            }
        }
        catch { }
        litProgOpts.Text = progs.ToString();
    }

    private string PageSizeOptions()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < PageSizes.Length; i++)
            sb.Append("<option value='").Append(PageSizes[i]).Append("'")
              .Append(PageSizes[i] == _pageSize ? " selected='selected'" : "").Append(">")
              .Append(PageSizes[i]).Append("</option>");
        return sb.ToString();
    }

    private void RenderPager(long total, int pageCount)
    {
        long first = total == 0 ? 0 : (long)(_page - 1) * _pageSize + 1;
        long last = Math.Min((long)_page * _pageSize, total);
        litPagerInfo.Text = total == 0
            ? "Nothing to show"
            : "Showing " + N(first) + " to " + N(last) + " of " + N(total) + " &middot; page " + _page + " of " + pageCount;

        if (pageCount <= 1) { litPager.Text = ""; return; }

        StringBuilder sb = new StringBuilder();
        sb.Append(PageLink(1, "&laquo; First", _page > 1));
        sb.Append(PageLink(_page - 1, "&lsaquo; Previous", _page > 1));
        int start = Math.Max(1, _page - 2);
        int end = Math.Min(pageCount, start + 4);
        start = Math.Max(1, end - 4);
        for (int i = start; i <= end; i++)
            sb.Append("<a href='").Append(Enc(Url("p", i.ToString(CultureInfo.InvariantCulture))))
              .Append(i == _page ? "' class='mm-pg mm-pg--active'>" : "' class='mm-pg'>").Append(i).Append("</a>");
        sb.Append(PageLink(_page + 1, "Next &rsaquo;", _page < pageCount));
        sb.Append(PageLink(pageCount, "Last &raquo;", _page < pageCount));
        litPager.Text = sb.ToString();
    }

    private string PageLink(int page, string label, bool enabled)
    {
        if (!enabled) return "<span class='mm-pg mm-pg--off'>" + label + "</span>";
        return "<a href='" + Enc(Url("p", page.ToString(CultureInfo.InvariantCulture))) + "' class='mm-pg'>" + label + "</a>";
    }

    private string Url(params string[] overrides)
    {
        Dictionary<string, string> p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_cls != ClsOverwritten) p["cls"] = _cls;
        if (_year.Length > 0) p["year"] = _year;
        if (_sem.Length > 0) p["sem"] = _sem;
        if (_prog.Length > 0) p["prog"] = _prog;
        if (_q.Length > 0) p["q"] = _q;
        if (_pageSize != DefaultPageSize) p["ps"] = _pageSize.ToString(CultureInfo.InvariantCulture);
        if (_page > 1) p["p"] = _page.ToString(CultureInfo.InvariantCulture);

        for (int i = 0; i + 1 < overrides.Length; i += 2)
        {
            string k = overrides[i], v = overrides[i + 1];
            if (v == null) p.Remove(k); else p[k] = v;
        }
        if (p.ContainsKey("cls") && p["cls"] == ClsOverwritten) p.Remove("cls");

        StringBuilder sb = new StringBuilder("MissingMarks.aspx");
        bool firstPair = true;
        foreach (KeyValuePair<string, string> kv in p)
        {
            sb.Append(firstPair ? "?" : "&");
            sb.Append(HttpUtility.UrlEncode(kv.Key)).Append("=").Append(HttpUtility.UrlEncode(kv.Value));
            firstPair = false;
        }
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────────────────
    //  CSV
    // ────────────────────────────────────────────────────────────────────

    private void ExportCsv(MySqlConnection conn)
    {
        Info info = Describe(_cls);
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Class,Student,Name,Course,Course title,Academic year,Semester,Programme," +
                       "Coursework,Exam,Total,Status,Stage,Detail,What closes it");

        List<string[]> rows = new List<string[]>();
        List<string> regnos = new List<string>(), courses = new List<string>();

        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT cr.ID, cr.regno, cr.courseID, cr.acad_year, cr.semester, cr.prog_id, " +
            "       cr.provisional_course_work_marks cw, cr.provisional_exam_marks ex, " +
            "       cr.provisional_total_marks tot, IFNULL(cr.provisional_marks_status,'') st, " +
            "       IFNULL(cr.mark_stage,'') stg " +
            ClassFrom(_cls) + ClassWhere(_cls) +
            (_cls == ClsRemoved ? " GROUP BY cr.ID " : " ") +
            " ORDER BY cr.regno, cr.courseID LIMIT 20000", conn))
        {
            Bind(cmd);
            cmd.CommandTimeout = 180;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string[] row = new string[] {
                        Str(r["regno"]), Str(r["courseID"]), Str(r["acad_year"]), Str(r["semester"]),
                        Str(r["prog_id"]), Str(r["cw"]), Str(r["ex"]), Str(r["tot"]),
                        Str(r["st"]), Str(r["stg"])
                    };
                    rows.Add(row);
                    if (row[0].Length > 0 && !regnos.Contains(row[0])) regnos.Add(row[0]);
                    if (row[1].Length > 0 && !courses.Contains(row[1])) courses.Add(row[1]);
                }
        }

        Dictionary<string, string> names = Lookup(conn, regnos,
            "SELECT regno k, CONCAT(IFNULL(firstname,''),' ',IFNULL(othername,'')) v FROM acad_student WHERE regno IN (");
        Dictionary<string, string> titles = Lookup(conn, courses,
            "SELECT courseID k, courseName v FROM acad_course WHERE courseID IN (");

        for (int i = 0; i < rows.Count; i++)
        {
            string[] x = rows[i];
            csv.Append(C(info.Title)).Append(C(x[0])).Append(C(Get(names, x[0])))
               .Append(C(x[1])).Append(C(Get(titles, x[1])))
               .Append(C(x[2])).Append(C(x[3])).Append(C(x[4]))
               .Append(C(x[5])).Append(C(x[6])).Append(C(x[7]))
               .Append(C(x[8])).Append(C(x[9])).Append(C(""))
               .Append(CLast(info.Fix));
        }

        string name = "MissingMarks_" + _cls + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
        Response.Clear();
        Response.ContentType = "text/csv; charset=utf-8";
        Response.AddHeader("Content-Disposition", "attachment; filename=" + name);
        Response.ContentEncoding = Encoding.UTF8;
        Response.Write("\uFEFF");   // byte-order mark, so Excel opens it as UTF-8
        Response.Write(csv.ToString());
        Response.End();
    }

    private static string C(string s) { return Csv(s) + ","; }
    private static string CLast(string s) { return Csv(s) + "\r\n"; }

    private static string Csv(string s)
    {
        if (s == null) return "";
        s = s.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");
        if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
        return "\"" + s + "\"";
    }

    // ────────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────────

    private Dictionary<string, string> Lookup(MySqlConnection conn, List<string> keys, string sqlPrefix)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string inList = InList(keys);
        if (inList.Length == 0) return map;
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(sqlPrefix + inList + ")", conn))
            {
                cmd.CommandTimeout = 60;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = Str(r["k"]);
                        if (k.Length > 0 && !map.ContainsKey(k)) map[k] = Str(r["v"]).Trim();
                    }
            }
        }
        catch { }
        return map;
    }

    private static string InList(List<string> values)
    {
        Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            string v = (values[i] ?? "").Trim();
            if (v.Length == 0 || seen.ContainsKey(v)) continue;
            seen[v] = true;
            if (sb.Length > 0) sb.Append(",");
            sb.Append("'").Append(v.Replace("\\", "\\\\").Replace("'", "''")).Append("'");
        }
        return sb.ToString();
    }

    private static string Opt(string value, string label, string current)
    {
        return "<option value='" + Enc(value) + "'" + (value == current ? " selected='selected'" : "") + ">"
             + Enc(label) + "</option>";
    }

    private static string Enc(string s)
    {
        if (s == null) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");
    }

    private static string Str(object o) { return (o == null || o == DBNull.Value) ? "" : o.ToString().Trim(); }

    private static string Trim(string s, int max)
    {
        s = (s ?? "").Trim();
        return s.Length > max ? s.Substring(0, max) : s;
    }

    private static string N(object o)
    {
        if (o == null || o == DBNull.Value) return "0";
        try { return Convert.ToInt64(o).ToString("N0"); } catch { return o.ToString(); }
    }

    private static string Get(Dictionary<string, string> map, string key)
    {
        if (map == null || key == null || key.Length == 0) return "";
        return map.ContainsKey(key) ? map[key] : "";
    }

    private static string Dash(string s) { return (s == null || s.Trim().Length == 0) ? "-" : s.Trim(); }

    private static string Clip(string s, int max)
    {
        s = (s ?? "").Trim();
        return s.Length > max ? s.Substring(0, max) + "..." : s;
    }
}
