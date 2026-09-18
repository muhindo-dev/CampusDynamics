using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Marks Audit Trail — the read-only record of who moved a student's marks.
///
/// Two views, both driven entirely by the query string so any filtered result is a
/// link you can bookmark, share in an email, or back out of:
///
///   ?view=changes   (default) — the structured ledger written by the database
///                   triggers on campus_dynamics_portal.acad_provisional_marks_audit.
///                   Every coursework / exam / total movement, old value to new
///                   value, whoever did it and wherever they did it from.
///
///   ?view=activity  — the older free-text log in campus_dynamics.acad_activity_log.
///                   It predates the triggers and is kept because it is the only
///                   record of what happened before they existed. Administrators
///                   only: its rows carry no programme code, so there is no honest
///                   way to scope them to a dean or a head of department.
///
/// Both views are read-only. Nothing on this page writes anything anywhere.
/// </summary>
public partial class COOPERP_NewScreens_MarksAuditTrail : System.Web.UI.Page
{
    // ────────────────────────────────────────────────────────────────────
    //  Constants
    // ────────────────────────────────────────────────────────────────────

    private const string AuditTable = "campus_dynamics_portal.acad_provisional_marks_audit";
    private const int MaxExportRows = 20000;

    /// <summary>The acad_activity_log page_function values that concern marks.</summary>
    private const string MarksInClause =
        "('Capture Results','Results Capture','Faculty Exam Results Editor'," +
        "'Results Approval Cancel','Results Management','Results Auto Pass'," +
        "'Mark Request Approve','Mark Request Reject','Mark Request Force Close'," +
        "'Mark Request Reopen','Mark Request Marks Update','Mark Request Batch'," +
        "'Marks Published to Results')";

    /// <summary>Friendly label for each legacy page_function, in dropdown order.</summary>
    private static readonly string[][] ActivityActions = new string[][]
    {
        new string[] { "Faculty Exam Results Editor", "Marks edited" },
        new string[] { "Results Capture",             "Marks captured (faculty)" },
        new string[] { "Capture Results",             "Marks captured (old system)" },
        new string[] { "Results Management",          "Results management" },
        new string[] { "Results Approval Cancel",     "Approval cancelled" },
        new string[] { "Results Auto Pass",           "Automatic pass" },
        new string[] { "Marks Published to Results",  "Marks published" },
        new string[] { "Mark Request Marks Update",   "Mark request: marks updated" },
        new string[] { "Mark Request Approve",        "Mark request: approved" },
        new string[] { "Mark Request Reject",         "Mark request: rejected" },
        new string[] { "Mark Request Reopen",         "Mark request: reopened" },
        new string[] { "Mark Request Force Close",    "Mark request: force closed" },
        new string[] { "Mark Request Batch",          "Mark request: batch action" }
    };

    private static readonly int[] PageSizes = new int[] { 25, 50, 100, 200 };

    // ────────────────────────────────────────────────────────────────────
    //  Request state
    // ────────────────────────────────────────────────────────────────────

    private string _view = "changes";
    private string _range = "365";     // all | today | 7 | 30 | 365 | "" when a custom date was typed
    private string _from = "";         // yyyy-MM-dd, "" = open ended
    private string _to = "";
    private string _who = "";
    private string _chg = "";
    private string _src = "";
    private string _act = "";
    private string _q = "";
    private int _page = 1;
    private int _pageSize = 50;
    private MarksScope _scope;

    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Page lifecycle
    // ────────────────────────────────────────────────────────────────────

    protected void Page_Load(object sender, EventArgs e)
    {
        ReadRequest();

        // Same gate as the rest of the marks module: administrators see everything,
        // deans and heads of department see their own programmes, nobody else gets in.
        _scope = MarksScopeResolver.Resolve();
        if (!_scope.HasAccess)
        {
            RenderDenied();
            return;
        }

        // The activity log has no programme column, so it cannot be narrowed to a
        // faculty. Rather than leak every faculty's history to a dean, it stays shut.
        if (_view == "activity" && !_scope.IsAdmin)
        {
            _view = "changes";
            litNotice.Text =
                "<div class='mat-note mat-note--info'>The older <b>activity log</b> is open to administrators only. " +
                "Its entries are free text with no programme code, so there is no reliable way to limit them to your " +
                "faculty. The <b>mark changes</b> view below is complete for every programme in your scope.</div>";
        }

        RenderTabs();
        RenderHeaderSub();

        if (string.Equals(Request.QueryString["export"], "csv", StringComparison.OrdinalIgnoreCase))
        {
            ExportCsv();
            return;
        }

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // If a database guard has gone missing, changes are silently not being
                // recorded. That has to shout, because a quiet audit trail looks identical
                // to a clean one.
                litHealth.Text = MarksControllerShared.BuildAuditHealthWarning(conn);

                if (_view == "activity")
                {
                    pnlChanges.Visible = false;
                    pnlActivity.Visible = true;
                    RenderActivity(conn);
                }
                else
                {
                    pnlChanges.Visible = true;
                    pnlActivity.Visible = false;
                    RenderChanges(conn);
                }
            }
        }
        catch (Exception ex)
        {
            litNotice.Text += "<div class='mat-note mat-note--error'><b>The audit trail could not be loaded.</b><br />"
                            + Enc(ex.Message) + "</div>";
        }
    }

    private void ReadRequest()
    {
        System.Collections.Specialized.NameValueCollection qs = Request.QueryString;

        _view = Low(qs["view"]) == "activity" ? "activity" : "changes";
        _who = Trim(qs["who"], 90);
        _chg = Low(qs["chg"]);
        _src = Trim(qs["src"], 100);
        _act = Trim(qs["act"], 60);
        _q = Trim(qs["q"], 60);

        if (_chg != "cw" && _chg != "exam" && _chg != "both") _chg = "";

        // A typed date always wins over a preset range.
        string from = Trim(qs["from"], 10);
        string to = Trim(qs["to"], 10);
        if (from.Length > 0 || to.Length > 0)
        {
            _from = ValidDate(from);
            _to = ValidDate(to);
            _range = "";
        }
        else
        {
            _range = Low(qs["r"]);
            if (_range != "all" && _range != "today" && _range != "7" && _range != "30" && _range != "365")
                _range = "365";
            ApplyRange();
        }

        int.TryParse(qs["p"], out _page);
        if (_page < 1) _page = 1;

        int ps;
        int.TryParse(qs["ps"], out ps);
        _pageSize = 50;
        for (int i = 0; i < PageSizes.Length; i++) if (PageSizes[i] == ps) _pageSize = ps;
    }

    private void ApplyRange()
    {
        DateTime today = DateTime.Today;
        switch (_range)
        {
            case "all": _from = ""; _to = ""; break;
            case "today": _from = Ymd(today); _to = Ymd(today); break;
            case "7": _from = Ymd(today.AddDays(-6)); _to = Ymd(today); break;
            case "30": _from = Ymd(today.AddDays(-29)); _to = Ymd(today); break;
            // This calendar year is the default: a month of marks activity is often
            // empty out of term, and an audit trail that opens empty teaches people
            // to distrust it.
            default: _from = Ymd(new DateTime(today.Year, 1, 1)); _to = Ymd(today); break;
        }
    }

    private void RenderDenied()
    {
        pnlChanges.Visible = false;
        pnlActivity.Visible = false;
        litHeadSub.Text = "Read-only record of every change made to a student's marks.";
        litNotice.Text =
            "<div class='mat-note mat-note--error'><b>You do not have access to the marks audit trail.</b><br />" +
            "This page is open to administrators, deans, heads of department, the registrar and exam officers. " +
            "If you believe you should be one of them, ask the system administrator to check your role.</div>";
    }

    private void RenderHeaderSub()
    {
        string sub = _view == "activity"
            ? "The original free-text log of results operations, kept for the period before automatic tracking began."
            : "Every coursework and exam mark that has moved, with the value before, the value after, who moved it and where from.";
        if (!_scope.IsAdmin && _scope.Label != null && _scope.Label.Length > 0)
            sub += " Showing " + Enc(_scope.Label) + ".";
        litHeadSub.Text = sub;

        litExportBtn.Text = "<a href='" + Enc(Url("export", "csv", "p", null)) + "' class='mat-btn mat-btn--inv mat-btn--sm'>"
            + "<svg width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' "
            + "stroke-linecap='round' stroke-linejoin='round'><path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/>"
            + "<polyline points='7 10 12 15 17 10'/><line x1='12' y1='15' x2='12' y2='3'/></svg>Download CSV</a>";
    }

    private void RenderTabs()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("<a href='").Append(Enc(UrlForView("changes")))
          .Append(_view == "changes" ? "' class='mat-tab mat-tab--active'>" : "' class='mat-tab'>")
          .Append("Mark changes</a>");

        if (_scope.IsAdmin)
        {
            sb.Append("<a href='").Append(Enc(UrlForView("activity")))
              .Append(_view == "activity" ? "' class='mat-tab mat-tab--active'>" : "' class='mat-tab'>")
              .Append("Activity log <span class='mat-tab__n'>legacy</span></a>");
        }
        litTabs.Text = sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════
    //  VIEW 1 — structured mark changes
    // ════════════════════════════════════════════════════════════════════

    private void RenderChanges(MySqlConnection conn)
    {
        string where = ChangesWhere();

        // ── headline numbers for the selected period ──
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) n, IFNULL(SUM(a.changed_cw),0) cw, IFNULL(SUM(a.changed_exam),0) ex, " +
            "       COUNT(DISTINCT a.regno) st, COUNT(DISTINCT a.performed_by) ppl " +
            "FROM " + AuditTable + " a " + where, conn))
        {
            BindChangeParams(cmd);
            cmd.CommandTimeout = 45;
            using (MySqlDataReader r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    litKpiTotal.Text = N(r["n"]);
                    litKpiCw.Text = N(r["cw"]);
                    litKpiExam.Text = N(r["ex"]);
                    litKpiStudents.Text = N(r["st"]);
                    litKpiStaff.Text = N(r["ppl"]) + " member" + (Num(r["ppl"]) == 1 ? "" : "s") + " of staff involved";
                }
            }
        }

        litKpiTotalSub.Text = PeriodLabel() + " &middot; " + N(ScalarLong(conn,
            "SELECT COUNT(*) FROM " + AuditTable + " a WHERE 1=1 " + ScopeSql())) + " on record in total";

        LoadChangeFilterOptions(conn);
        RenderQuickRanges(litQuickC, "changes");
        litCFrom.Text = Enc(_from);
        litCTo.Text = Enc(_to);
        litCQ.Text = Enc(_q);
        litCPsOpts.Text = PageSizeOptions();

        // ── the page of rows ──
        long total = ScalarLong2(conn, "SELECT COUNT(*) FROM " + AuditTable + " a " + where);
        int pageCount = PageCount(total);
        if (_page > pageCount) _page = pageCount;

        List<ChangeRow> rows = new List<ChangeRow>();
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT a.id, a.reg_id, a.regno, a.course_id, a.prog_id, a.acad_year, a.semester, " +
            "       a.action_type, a.source_table, a.changed_cw, a.changed_exam, " +
            "       a.old_cw, a.new_cw, a.old_exam, a.new_exam, a.old_total, a.new_total, " +
            "       a.old_status, a.new_status, a.performed_by, a.source_page, a.change_reason, " +
            "       a.ip_address, a.created_at " +
            "FROM " + AuditTable + " a " + where +
            " ORDER BY a.id DESC LIMIT " + _pageSize + " OFFSET " + ((_page - 1) * _pageSize), conn))
        {
            BindChangeParams(cmd);
            cmd.CommandTimeout = 45;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read()) rows.Add(ReadChangeRow(r));
        }

        // Names are resolved for this page of rows only, one lookup per kind. Joining them
        // in the main query would drag the whole 15,000-row table through a cross-database
        // join on mismatched collations for the sake of at most 200 labels.
        Dictionary<string, string> students = LookupStudents(conn, rows);
        Dictionary<string, string> courses = LookupCourses(conn, rows);
        Dictionary<string, Staff> staff = LookupStaff(conn, rows);

        StringBuilder sb = new StringBuilder();
        if (rows.Count == 0)
        {
            sb.Append("<tr><td colspan='9'><div class='mat-empty'><b>No mark changes here</b>")
              .Append(HasChangeFilter()
                  ? "Nothing matches these filters. Try widening the period, or clear the filters to start again."
                  : "No coursework or exam mark has been altered in this period.")
              .Append("</div></td></tr>");
        }
        else
        {
            for (int i = 0; i < rows.Count; i++)
                sb.Append(ChangeRowHtml(rows[i], students, courses, staff));
        }
        litCRows.Text = sb.ToString();

        litCMeta.Text = N(total) + (total == 1 ? " change" : " changes");
        RenderPager(litCPager, litCPagerInfo, total, pageCount, "changes");
    }

    private bool HasChangeFilter()
    {
        return _who.Length > 0 || _chg.Length > 0 || _src.Length > 0 || _q.Length > 0
            || _from.Length > 0 || _to.Length > 0;
    }

    private string ChangesWhere()
    {
        StringBuilder sb = new StringBuilder(" WHERE 1=1 ");
        if (_from.Length > 0) sb.Append(" AND a.created_at >= @dfrom ");
        if (_to.Length > 0) sb.Append(" AND a.created_at < @dto ");
        if (_who.Length > 0) sb.Append(" AND a.performed_by = @who ");
        if (_q.Length > 0) sb.Append(" AND (a.regno LIKE @q OR a.course_id LIKE @q OR a.performed_by LIKE @q) ");

        if (_chg == "cw") sb.Append(" AND a.changed_cw = 1 ");
        else if (_chg == "exam") sb.Append(" AND a.changed_exam = 1 ");
        else if (_chg == "both") sb.Append(" AND a.changed_cw = 1 AND a.changed_exam = 1 ");

        if (_src == "__none__") sb.Append(" AND (a.source_page IS NULL OR a.source_page = '') ");
        else if (_src.Length > 0) sb.Append(" AND a.source_page = @src ");

        sb.Append(ScopeSql());
        return sb.ToString();
    }

    /// <summary>Programme restriction for the viewer. Empty for an administrator.</summary>
    private string ScopeSql()
    {
        return _scope.ProgFilter("a", "prog_id");
    }

    private void BindChangeParams(MySqlCommand cmd)
    {
        if (_from.Length > 0) cmd.Parameters.AddWithValue("@dfrom", DateTime.Parse(_from, CultureInfo.InvariantCulture));
        if (_to.Length > 0) cmd.Parameters.AddWithValue("@dto", DateTime.Parse(_to, CultureInfo.InvariantCulture).AddDays(1));
        if (_who.Length > 0) cmd.Parameters.AddWithValue("@who", _who);
        if (_q.Length > 0) cmd.Parameters.AddWithValue("@q", "%" + _q + "%");
        if (_src.Length > 0 && _src != "__none__") cmd.Parameters.AddWithValue("@src", _src);
    }

    private void LoadChangeFilterOptions(MySqlConnection conn)
    {
        // Staff who have actually changed something — sorted by how much, so the busiest
        // names are at the top of the list rather than buried alphabetically.
        StringBuilder who = new StringBuilder("<option value=''>Anyone</option>");
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT a.performed_by, COUNT(*) n FROM " + AuditTable + " a WHERE 1=1 " + ScopeSql() +
            " GROUP BY a.performed_by ORDER BY n DESC LIMIT 300", conn))
        {
            cmd.CommandTimeout = 30;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string v = Str(r["performed_by"]);
                    if (v.Length == 0) continue;
                    who.Append("<option value='").Append(Enc(v)).Append("'")
                       .Append(v == _who ? " selected='selected'" : "").Append(">")
                       .Append(Enc(v)).Append(" (").Append(N(r["n"])).Append(")</option>");
                }
        }
        litCWhoOpts.Text = who.ToString();

        litCChgOpts.Text =
            Opt("", "Any change", _chg) +
            Opt("cw", "Coursework mark", _chg) +
            Opt("exam", "Exam mark", _chg) +
            Opt("both", "Both at once", _chg);

        StringBuilder src = new StringBuilder("<option value=''>Anywhere</option>");
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT IFNULL(a.source_page,'') sp, COUNT(*) n FROM " + AuditTable + " a WHERE 1=1 " + ScopeSql() +
            " GROUP BY sp ORDER BY n DESC LIMIT 100", conn))
        {
            cmd.CommandTimeout = 30;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string v = Str(r["sp"]);
                    string value = v.Length == 0 ? "__none__" : v;
                    src.Append("<option value='").Append(Enc(value)).Append("'")
                       .Append(value == _src ? " selected='selected'" : "").Append(">")
                       .Append(Enc(SourceLabel(v) + " - " + SourceDetail(v))).Append(" (").Append(N(r["n"])).Append(")</option>");
                }
        }
        litCSrcOpts.Text = src.ToString();
    }

    private sealed class ChangeRow
    {
        public long Id;
        public int RegId;
        public string Regno, CourseId, ProgId, AcadYear, Semester, ActionType, SourceTable;
        public bool ChangedCw, ChangedExam;
        public string OldCw, NewCw, OldExam, NewExam, OldTotal, NewTotal, OldStatus, NewStatus;
        public string By, Source, Reason, Ip;
        public DateTime At;
    }

    private static ChangeRow ReadChangeRow(MySqlDataReader r)
    {
        ChangeRow c = new ChangeRow();
        c.Id = Convert.ToInt64(r["id"]);
        c.RegId = Convert.ToInt32(r["reg_id"]);
        c.Regno = Str(r["regno"]);
        c.CourseId = Str(r["course_id"]);
        c.ProgId = Str(r["prog_id"]);
        c.AcadYear = Str(r["acad_year"]);
        c.Semester = Str(r["semester"]);
        c.ActionType = Str(r["action_type"]);
        c.SourceTable = Str(r["source_table"]);
        c.ChangedCw = Convert.ToInt32(r["changed_cw"]) == 1;
        c.ChangedExam = Convert.ToInt32(r["changed_exam"]) == 1;
        c.OldCw = Str(r["old_cw"]); c.NewCw = Str(r["new_cw"]);
        c.OldExam = Str(r["old_exam"]); c.NewExam = Str(r["new_exam"]);
        c.OldTotal = Str(r["old_total"]); c.NewTotal = Str(r["new_total"]);
        c.OldStatus = Str(r["old_status"]); c.NewStatus = Str(r["new_status"]);
        c.By = Str(r["performed_by"]);
        c.Source = Str(r["source_page"]);
        c.Reason = Str(r["change_reason"]);
        c.Ip = Str(r["ip_address"]);
        c.At = r["created_at"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["created_at"]);
        return c;
    }

    private string ChangeRowHtml(ChangeRow c, Dictionary<string, string> students,
                                 Dictionary<string, string> courses, Dictionary<string, Staff> staff)
    {
        string sname = Get(students, c.Regno);
        string ctitle = Get(courses, c.CourseId);
        Staff st = staff.ContainsKey(c.By.ToLowerInvariant()) ? staff[c.By.ToLowerInvariant()] : null;

        StringBuilder sb = new StringBuilder();
        sb.Append("<tr>");

        sb.Append("<td class='mat-when'><span class='mat-when__d'>").Append(Enc(c.At.ToString("dd MMM yy")))
          .Append("</span><span class='mat-when__t'>").Append(Enc(c.At.ToString("HH:mm"))).Append("</span></td>");

        sb.Append("<td><span class='mat-who'>").Append(Enc(st != null && st.Name.Length > 0 ? st.Name : c.By)).Append("</span>");
        if (st != null && st.Name.Length > 0 && !st.Name.Equals(c.By, StringComparison.OrdinalIgnoreCase))
            sb.Append("<span class='mat-who__sub'>").Append(Enc(c.By)).Append("</span>");
        else if (st != null && st.Code.Length > 0)
            sb.Append("<span class='mat-who__sub'>").Append(Enc(st.Code)).Append("</span>");
        sb.Append("</td>");

        sb.Append("<td><span class='mat-code'>").Append(Enc(c.Regno)).Append("</span>");
        if (sname.Length > 0) sb.Append("<span class='mat-sub'>").Append(Enc(sname)).Append("</span>");
        sb.Append("</td>");

        sb.Append("<td><span class='mat-code'>").Append(Enc(c.CourseId)).Append("</span><span class='mat-sub'>")
          .Append(Enc(CourseContext(c))).Append("</span></td>");

        sb.Append("<td>").Append(Move(c.ChangedCw, c.OldCw, c.NewCw)).Append("</td>");
        sb.Append("<td>").Append(Move(c.ChangedExam, c.OldExam, c.NewExam)).Append("</td>");
        sb.Append("<td>").Append(Move(c.OldTotal != c.NewTotal, c.OldTotal, c.NewTotal)).Append("</td>");

        // A MIGRATE row was rebuilt from the old free-text log rather than written by the
        // trigger as it happened. It is good evidence, but it is not the same evidence, and
        // the difference belongs on the row rather than buried in the detail panel.
        string srcNote = Enc(SourceDetail(c.Source));
        if (c.ActionType == "MIGRATE") srcNote += " &middot; reconstructed";
        sb.Append("<td><span class='mat-badge ").Append(SourceClass(c.Source)).Append("'>")
          .Append(Enc(SourceLabel(c.Source))).Append("</span><span class='mat-sub'>")
          .Append(srcNote).Append("</span></td>");

        sb.Append("<td><button type='button' class='mat-btn mat-btn--ghost mat-btn--sm' onclick='matShowDetail(this)'")
          .Append(A("id", c.Id.ToString(CultureInfo.InvariantCulture)))
          .Append(A("reg", c.RegId.ToString(CultureInfo.InvariantCulture)))
          .Append(A("regno", c.Regno))
          .Append(A("sname", sname))
          .Append(A("course", c.CourseId))
          .Append(A("ctitle", ctitle))
          .Append(A("prog", c.ProgId))
          .Append(A("year", c.AcadYear))
          .Append(A("sem", c.Semester.Length > 0 ? "Semester " + c.Semester : ""))
          .Append(A("act", ActionLabel(c.ActionType)))
          .Append(A("table", c.SourceTable))
          .Append(A("ocw", c.OldCw)).Append(A("ncw", c.NewCw))
          .Append(A("oex", c.OldExam)).Append(A("nex", c.NewExam))
          .Append(A("otot", c.OldTotal)).Append(A("ntot", c.NewTotal))
          .Append(A("ostat", c.OldStatus)).Append(A("nstat", c.NewStatus))
          .Append(A("by", st != null && st.Name.Length > 0 ? st.Name : c.By))
          .Append(A("staff", st == null ? "" : Join(st.Code, st.Dept)))
          .Append(A("at", c.At == DateTime.MinValue ? "" : c.At.ToString("dddd d MMMM yyyy 'at' HH:mm:ss")))
          .Append(A("src", Join(SourceLabel(c.Source), SourceDetail(c.Source))))
          .Append(A("ip", c.Ip))
          .Append(A("reason", c.Reason))
          .Append(">Details</button></td>");

        sb.Append("</tr>");
        return sb.ToString();
    }

    private static string CourseContext(ChangeRow c)
    {
        string s = c.AcadYear;
        if (c.Semester.Length > 0) s = (s.Length > 0 ? s + " - " : "") + "Sem " + c.Semester;
        if (s.Length == 0 && c.ProgId.Length > 0) s = c.ProgId;
        else if (c.ProgId.Length > 0) s = c.ProgId + " - " + s;
        return s;
    }

    /// <summary>One "before to after" cell. Empty when that mark did not move.</summary>
    private static string Move(bool changed, string oldV, string newV)
    {
        if (!changed) return "<span class='mat-nil'>&mdash;</span>";

        string cls = "mat-mv--set";
        double a, b;
        if (oldV.Length > 0 && newV.Length > 0
            && double.TryParse(oldV, NumberStyles.Any, CultureInfo.InvariantCulture, out a)
            && double.TryParse(newV, NumberStyles.Any, CultureInfo.InvariantCulture, out b))
            cls = b > a ? "mat-mv--up" : (b < a ? "mat-mv--down" : "mat-mv--set");

        return "<span class='mat-mv " + cls + "'>"
             + (oldV.Length == 0 ? "<span class='mat-mv__unset'>not set</span>" : "<span class='mat-mv__o'>" + Enc(oldV) + "</span>")
             + "<span class='mat-mv__a'>&rarr;</span>"
             + (newV.Length == 0 ? "<span class='mat-mv__unset'>cleared</span>" : "<span class='mat-mv__n'>" + Enc(newV) + "</span>")
             + "</span>";
    }

    // ── source_page vocabulary ──────────────────────────────────────────

    private static string SourceLabel(string src)
    {
        if (src == null || src.Length == 0) return "Not recorded";
        if (src.StartsWith("Portal:", StringComparison.OrdinalIgnoreCase)) return "Student portal";
        if (src.StartsWith("ODEL:", StringComparison.OrdinalIgnoreCase)) return "ODEL";
        if (src.StartsWith("API/v2:", StringComparison.OrdinalIgnoreCase)) return "Staff API";
        if (src.StartsWith("MarkEntry:", StringComparison.OrdinalIgnoreCase)) return "eAdmin";
        if (src.StartsWith("MarkRequests:", StringComparison.OrdinalIgnoreCase)) return "Mark request";
        if (src.StartsWith("ProvisionalMarks:", StringComparison.OrdinalIgnoreCase)) return "eAdmin";
        if (src.StartsWith("RetakeController:", StringComparison.OrdinalIgnoreCase)) return "eAdmin";
        if (src.IndexOf("Mark Request", StringComparison.OrdinalIgnoreCase) >= 0) return "Mark request";
        if (src.IndexOf("Faculty Exam Results Editor", StringComparison.OrdinalIgnoreCase) >= 0) return "eAdmin";
        return "Other";
    }

    private static string SourceClass(string src)
    {
        switch (SourceLabel(src))
        {
            case "Student portal": return "mat-badge--green";
            case "ODEL": return "mat-badge--amber";
            case "Staff API": return "mat-badge--red";
            case "Mark request": return "mat-badge--blue";
            case "eAdmin": return "mat-badge--navy";
            default: return "mat-badge--grey";
        }
    }

    /// <summary>The part after the colon, spelled out in words.</summary>
    private static string SourceDetail(string src)
    {
        if (src == null || src.Length == 0) return "before tracking began";

        switch (src)
        {
            case "Portal:lecturer-marks-edit": return "lecturer edited a mark";
            case "Portal:lecturer-marks-clear": return "lecturer cleared the marks";
            case "Portal:retake-registration": return "cleared for a retake";
            case "ODEL:push-coursework": return "coursework pushed from ODEL";
            case "MarkEntry:sheet": return "mark-entry sheet";
            case "MarkRequests:apply-to-registration": return "mark request applied";
            case "MarkRequests:sync-marks": return "mark request synced";
            case "ProvisionalMarks:admin-override": return "administrator override";
            case "RetakeController:rollback": return "retake rolled back";
            case "Faculty Exam Results Editor": return "faculty results editor";
            case "Mark Request Marks Update": return "mark request update";
        }

        int i = src.IndexOf(':');
        if (i >= 0 && i < src.Length - 1) return src.Substring(i + 1).Replace('-', ' ').Replace('_', ' ');
        return src;
    }

    private static string ActionLabel(string t)
    {
        switch ((t ?? "").ToUpperInvariant())
        {
            case "INSERT": return "Marks entered for the first time";
            case "UPDATE": return "Existing marks changed";
            case "DELETE": return "Registration carrying marks deleted";
            case "MIGRATE": return "Reconstructed from the older activity log";
            default: return t;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  VIEW 2 — legacy activity log
    // ════════════════════════════════════════════════════════════════════

    private void RenderActivity(MySqlConnection conn)
    {
        string where = ActivityWhere();

        RenderQuickRanges(litQuickA, "activity");
        litAFrom.Text = Enc(_from);
        litATo.Text = Enc(_to);
        litAQ.Text = Enc(_q);
        litAPsOpts.Text = PageSizeOptions();
        LoadActivityFilterOptions(conn);

        long total = ScalarLong2(conn, "SELECT COUNT(*) FROM acad_activity_log a " + where);
        int pageCount = PageCount(total);
        if (_page > pageCount) _page = pageCount;

        List<ActivityRow> rows = new List<ActivityRow>();
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT a.logid, a.user_id, a.page_function, a.par, a.access_date " +
            "FROM acad_activity_log a " + where +
            " ORDER BY a.access_date DESC, a.logid DESC LIMIT " + _pageSize + " OFFSET " + ((_page - 1) * _pageSize), conn))
        {
            BindActivityParams(cmd);
            cmd.CommandTimeout = 90;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    ActivityRow a = new ActivityRow();
                    a.LogId = Convert.ToInt64(r["logid"]);
                    a.User = Str(r["user_id"]);
                    a.Action = Str(r["page_function"]);
                    a.Par = Str(r["par"]);
                    a.At = r["access_date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["access_date"]);
                    a.Regno = ExtractRegno(a.Par);
                    a.Ip = ExtractIp(a.Par);
                    rows.Add(a);
                }
        }

        Dictionary<string, string> students = LookupStudentsA(conn, rows);
        Dictionary<string, Staff> staff = LookupStaffA(conn, rows);

        StringBuilder sb = new StringBuilder();
        if (rows.Count == 0)
        {
            sb.Append("<tr><td colspan='6'><div class='mat-empty'><b>Nothing logged here</b>")
              .Append("No results operation was recorded in this period.</div></td></tr>");
        }
        else
        {
            for (int i = 0; i < rows.Count; i++)
            {
                ActivityRow a = rows[i];
                Staff st = staff.ContainsKey(a.User.ToLowerInvariant()) ? staff[a.User.ToLowerInvariant()] : null;
                string sname = Get(students, a.Regno);

                sb.Append("<tr>");
                sb.Append("<td class='mat-when'><span class='mat-when__d'>").Append(Enc(a.At.ToString("dd MMM yy")))
                  .Append("</span><span class='mat-when__t'>").Append(Enc(a.At.ToString("HH:mm"))).Append("</span></td>");

                sb.Append("<td><span class='mat-who'>").Append(Enc(st != null && st.Name.Length > 0 ? st.Name : a.User)).Append("</span>");
                if (st != null && st.Name.Length > 0)
                    sb.Append("<span class='mat-who__sub'>").Append(Enc(Join(a.User, st.Dept))).Append("</span>");
                sb.Append("</td>");

                sb.Append("<td><span class='mat-badge ").Append(ActivityClass(a.Action)).Append("'>")
                  .Append(Enc(ActivityLabel(a.Action))).Append("</span></td>");

                sb.Append("<td>");
                if (a.Regno.Length > 0)
                {
                    sb.Append("<span class='mat-code'>").Append(Enc(a.Regno)).Append("</span>");
                    if (sname.Length > 0) sb.Append("<span class='mat-sub'>").Append(Enc(sname)).Append("</span>");
                }
                else sb.Append("<span class='mat-nil'>&mdash;</span>");
                sb.Append("</td>");

                sb.Append("<td style='white-space:normal;line-height:1.5'>").Append(Enc(CleanPar(a.Par))).Append("</td>");
                sb.Append("<td>").Append(a.Ip.Length > 0 ? "<span class='mat-code'>" + Enc(a.Ip) + "</span>" : "<span class='mat-nil'>&mdash;</span>").Append("</td>");
                sb.Append("</tr>");
            }
        }
        litARows.Text = sb.ToString();
        litAMeta.Text = N(total) + (total == 1 ? " entry" : " entries");
        RenderPager(litAPager, litAPagerInfo, total, pageCount, "activity");
    }

    private sealed class ActivityRow
    {
        public long LogId;
        public string User, Action, Par, Regno, Ip;
        public DateTime At;
    }

    private string ActivityWhere()
    {
        StringBuilder sb = new StringBuilder(" WHERE a.page_function IN " + MarksInClause + " ");
        if (_from.Length > 0) sb.Append(" AND a.access_date >= @dfrom ");
        if (_to.Length > 0) sb.Append(" AND a.access_date < @dto ");
        if (_who.Length > 0) sb.Append(" AND a.user_id = @who ");
        if (_act.Length > 0) sb.Append(" AND a.page_function = @act ");
        if (_q.Length > 0) sb.Append(" AND (a.par LIKE @q OR a.user_id LIKE @q) ");
        return sb.ToString();
    }

    private void BindActivityParams(MySqlCommand cmd)
    {
        if (_from.Length > 0) cmd.Parameters.AddWithValue("@dfrom", DateTime.Parse(_from, CultureInfo.InvariantCulture));
        if (_to.Length > 0) cmd.Parameters.AddWithValue("@dto", DateTime.Parse(_to, CultureInfo.InvariantCulture).AddDays(1));
        if (_who.Length > 0) cmd.Parameters.AddWithValue("@who", _who);
        if (_act.Length > 0) cmd.Parameters.AddWithValue("@act", _act);
        if (_q.Length > 0) cmd.Parameters.AddWithValue("@q", "%" + _q + "%");
    }

    private void LoadActivityFilterOptions(MySqlConnection conn)
    {
        StringBuilder who = new StringBuilder("<option value=''>Anyone</option>");
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT a.user_id, COUNT(*) n FROM acad_activity_log a WHERE a.page_function IN " + MarksInClause +
            " GROUP BY a.user_id ORDER BY n DESC LIMIT 300", conn))
        {
            cmd.CommandTimeout = 90;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string v = Str(r["user_id"]);
                    if (v.Length == 0) continue;
                    who.Append("<option value='").Append(Enc(v)).Append("'")
                       .Append(v == _who ? " selected='selected'" : "").Append(">")
                       .Append(Enc(v)).Append(" (").Append(N(r["n"])).Append(")</option>");
                }
        }
        litAWhoOpts.Text = who.ToString();

        StringBuilder act = new StringBuilder("<option value=''>All actions</option>");
        for (int i = 0; i < ActivityActions.Length; i++)
            act.Append(Opt(ActivityActions[i][0], ActivityActions[i][1], _act));
        litAActOpts.Text = act.ToString();
    }

    private static string ActivityLabel(string pf)
    {
        for (int i = 0; i < ActivityActions.Length; i++)
            if (ActivityActions[i][0] == pf) return ActivityActions[i][1];
        return pf;
    }

    private static string ActivityClass(string pf)
    {
        switch (pf)
        {
            case "Faculty Exam Results Editor": return "mat-badge--amber";
            case "Results Approval Cancel": return "mat-badge--red";
            case "Results Auto Pass": return "mat-badge--green";
            case "Capture Results":
            case "Results Capture": return "mat-badge--blue";
            case "Marks Published to Results": return "mat-badge--navy";
            default:
                return (pf ?? "").StartsWith("Mark Request", StringComparison.OrdinalIgnoreCase)
                     ? "mat-badge--blue" : "mat-badge--grey";
        }
    }

    /// <summary>The reg. number buried in the free-text par field, if there is one.</summary>
    private static string ExtractRegno(string par)
    {
        if (par == null || par.Length == 0) return "";
        string s = After(par, "Marks for ");
        if (s == null) s = After(par, "Student: ");
        if (s == null) return "";
        int end = s.IndexOfAny(new char[] { ',', ' ', ';' });
        if (end > 0) s = s.Substring(0, end);
        return s.Trim();
    }

    private static string ExtractIp(string par)
    {
        string s = After(par, "IP Address: ");
        if (s == null) return "";
        int end = s.IndexOfAny(new char[] { ',', ' ', ';' });
        if (end > 0) s = s.Substring(0, end);
        return s.Trim();
    }

    /// <summary>The par text with the IP stripped off — it already has its own column.</summary>
    private static string CleanPar(string par)
    {
        if (par == null) return "";
        int i = par.IndexOf("IP Address:", StringComparison.OrdinalIgnoreCase);
        if (i > 0) par = par.Substring(0, i);
        return par.Trim().TrimEnd(',', ';', '-').Trim();
    }

    // ════════════════════════════════════════════════════════════════════
    //  Name resolution (one lookup per page of rows)
    // ════════════════════════════════════════════════════════════════════

    private sealed class Staff
    {
        public string Name = "", Code = "", Dept = "";
    }

    private Dictionary<string, string> LookupStudents(MySqlConnection conn, List<ChangeRow> rows)
    {
        List<string> keys = new List<string>();
        for (int i = 0; i < rows.Count; i++) keys.Add(rows[i].Regno);
        return StudentNames(conn, keys);
    }

    private Dictionary<string, string> LookupStudentsA(MySqlConnection conn, List<ActivityRow> rows)
    {
        List<string> keys = new List<string>();
        for (int i = 0; i < rows.Count; i++) keys.Add(rows[i].Regno);
        return StudentNames(conn, keys);
    }

    private Dictionary<string, string> StudentNames(MySqlConnection conn, List<string> regnos)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string inList = InList(regnos);
        if (inList.Length == 0) return map;

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT regno, CONCAT(IFNULL(firstname,''),' ',IFNULL(othername,'')) nm " +
                "FROM acad_student WHERE regno IN (" + inList + ")", conn))
            {
                cmd.CommandTimeout = 30;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = Str(r["regno"]);
                        if (k.Length > 0 && !map.ContainsKey(k)) map[k] = Str(r["nm"]).Trim();
                    }
            }
        }
        catch { }
        return map;
    }

    private Dictionary<string, string> LookupCourses(MySqlConnection conn, List<ChangeRow> rows)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        List<string> keys = new List<string>();
        for (int i = 0; i < rows.Count; i++) keys.Add(rows[i].CourseId);
        string inList = InList(keys);
        if (inList.Length == 0) return map;

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT courseID, courseName FROM acad_course WHERE courseID IN (" + inList + ")", conn))
            {
                cmd.CommandTimeout = 30;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = Str(r["courseID"]);
                        if (k.Length > 0 && !map.ContainsKey(k)) map[k] = Str(r["courseName"]).Trim();
                    }
            }
        }
        catch { }
        return map;
    }

    private Dictionary<string, Staff> LookupStaff(MySqlConnection conn, List<ChangeRow> rows)
    {
        List<string> keys = new List<string>();
        for (int i = 0; i < rows.Count; i++) keys.Add(rows[i].By);
        return StaffNames(conn, keys);
    }

    private Dictionary<string, Staff> LookupStaffA(MySqlConnection conn, List<ActivityRow> rows)
    {
        List<string> keys = new List<string>();
        for (int i = 0; i < rows.Count; i++) keys.Add(rows[i].User);
        return StaffNames(conn, keys);
    }

    /// <summary>
    /// performed_by holds whatever the writing page knew about the person — sometimes the
    /// login name, sometimes the display name already. Both are matched, so the trail reads
    /// as a person rather than as a login.
    /// </summary>
    private Dictionary<string, Staff> StaffNames(MySqlConnection conn, List<string> names)
    {
        Dictionary<string, Staff> map = new Dictionary<string, Staff>(StringComparer.OrdinalIgnoreCase);
        string inList = InList(names);
        if (inList.Length == 0) return map;

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT e.usernames, e.emp_name, e.EMP_CODE, IFNULL(d.dept_name,'') dept " +
                "FROM hrm_employee e " +
                "LEFT JOIN hrm_emp_contracts c ON c.empID = e.empID AND c.ID = " +
                "     (SELECT MAX(c2.ID) FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID) " +
                "LEFT JOIN hrm_departments d ON d.ID = c.departmentID " +
                "WHERE e.usernames IN (" + inList + ") OR e.emp_name IN (" + inList + ")", conn))
            {
                cmd.CommandTimeout = 30;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        Staff s = new Staff();
                        s.Name = Str(r["emp_name"]).Trim();
                        s.Code = Str(r["EMP_CODE"]).Trim();
                        s.Dept = Str(r["dept"]).Trim();

                        string u = Str(r["usernames"]).Trim().ToLowerInvariant();
                        if (u.Length > 0 && !map.ContainsKey(u)) map[u] = s;
                        string n = s.Name.ToLowerInvariant();
                        if (n.Length > 0 && !map.ContainsKey(n)) map[n] = s;
                    }
            }
        }
        catch { }
        return map;
    }

    // ════════════════════════════════════════════════════════════════════
    //  CSV export
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds the whole file before touching the response, so a query that fails ends as a
    /// readable message rather than half a spreadsheet.
    /// </summary>
    private void ExportCsv()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                if (_view == "activity") ExportActivity(conn); else ExportChanges(conn);
            }
        }
        catch (System.Threading.ThreadAbortException)
        {
            throw;      // Response.End() finishing normally, not a failure
        }
        catch (Exception ex)
        {
            Response.Clear();
            Response.ContentType = "text/plain; charset=utf-8";
            Response.Write("The export could not be produced.\r\n\r\n" + ex.Message);
            Response.End();
        }
    }

    private void ExportChanges(MySqlConnection conn)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Entry,Recorded at,Changed by,Source,IP address,Student,Name,Course,Programme," +
                       "Academic year,Semester,Operation,Coursework before,Coursework after," +
                       "Exam before,Exam after,Total before,Total after,Status before,Status after,Reason");

        List<ChangeRow> rows = new List<ChangeRow>();
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT a.id, a.reg_id, a.regno, a.course_id, a.prog_id, a.acad_year, a.semester, " +
            "       a.action_type, a.source_table, a.changed_cw, a.changed_exam, " +
            "       a.old_cw, a.new_cw, a.old_exam, a.new_exam, a.old_total, a.new_total, " +
            "       a.old_status, a.new_status, a.performed_by, a.source_page, a.change_reason, " +
            "       a.ip_address, a.created_at " +
            "FROM " + AuditTable + " a " + ChangesWhere() +
            " ORDER BY a.id DESC LIMIT " + MaxExportRows, conn))
        {
            BindChangeParams(cmd);
            cmd.CommandTimeout = 180;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read()) rows.Add(ReadChangeRow(r));
        }

        Dictionary<string, string> students = LookupStudents(conn, rows);

        for (int i = 0; i < rows.Count; i++)
        {
            ChangeRow c = rows[i];
            csv.Append(C(c.Id.ToString(CultureInfo.InvariantCulture)))
               .Append(C(c.At == DateTime.MinValue ? "" : c.At.ToString("yyyy-MM-dd HH:mm:ss")))
               .Append(C(c.By)).Append(C(c.Source)).Append(C(c.Ip))
               .Append(C(c.Regno)).Append(C(Get(students, c.Regno)))
               .Append(C(c.CourseId)).Append(C(c.ProgId)).Append(C(c.AcadYear)).Append(C(c.Semester))
               .Append(C(c.ActionType))
               .Append(C(c.ChangedCw ? c.OldCw : "")).Append(C(c.ChangedCw ? c.NewCw : ""))
               .Append(C(c.ChangedExam ? c.OldExam : "")).Append(C(c.ChangedExam ? c.NewExam : ""))
               .Append(C(c.OldTotal)).Append(C(c.NewTotal))
               .Append(C(c.OldStatus)).Append(C(c.NewStatus))
               .Append(CLast(c.Reason));
        }
        SendCsv(csv.ToString(), "MarkChanges");
    }

    private void ExportActivity(MySqlConnection conn)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Entry,Recorded at,User,Action,Student,Detail,IP address");

        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT a.logid, a.user_id, a.page_function, a.par, a.access_date " +
            "FROM acad_activity_log a " + ActivityWhere() +
            " ORDER BY a.access_date DESC, a.logid DESC LIMIT " + MaxExportRows, conn))
        {
            BindActivityParams(cmd);
            cmd.CommandTimeout = 180;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string par = Str(r["par"]);
                    DateTime at = r["access_date"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["access_date"]);
                    csv.Append(C(Str(r["logid"])))
                       .Append(C(at == DateTime.MinValue ? "" : at.ToString("yyyy-MM-dd HH:mm:ss")))
                       .Append(C(Str(r["user_id"])))
                       .Append(C(ActivityLabel(Str(r["page_function"]))))
                       .Append(C(ExtractRegno(par)))
                       .Append(C(CleanPar(par)))
                       .Append(CLast(ExtractIp(par)));
                }
        }
        SendCsv(csv.ToString(), "MarksActivityLog");
    }

    private void SendCsv(string body, string prefix)
    {
        string name = prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
        Response.Clear();
        Response.ContentType = "text/csv; charset=utf-8";
        Response.AddHeader("Content-Disposition", "attachment; filename=" + name);
        Response.ContentEncoding = Encoding.UTF8;
        Response.Write("\uFEFF");   // the byte-order mark, so Excel opens it as UTF-8
        Response.Write(body);
        Response.End();
    }

    private static string C(string s) { return Csv(s) + ","; }
    private static string CLast(string s) { return Csv(s) + "\r\n"; }

    private static string Csv(string s)
    {
        if (s == null) return "";
        s = s.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");
        // A leading =, +, - or @ makes a spreadsheet treat the cell as a formula.
        if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0) s = "'" + s;
        return "\"" + s + "\"";
    }

    // ════════════════════════════════════════════════════════════════════
    //  Shared chrome: quick ranges, page size, pager, URLs
    // ════════════════════════════════════════════════════════════════════

    private void RenderQuickRanges(System.Web.UI.WebControls.Literal target, string view)
    {
        string[][] ranges = new string[][]
        {
            new string[] { "today", "Today" },
            new string[] { "7",     "Last 7 days" },
            new string[] { "30",    "Last 30 days" },
            new string[] { "365",   "This year" },
            new string[] { "all",   "Everything" }
        };

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < ranges.Length; i++)
        {
            bool on = _range == ranges[i][0];
            sb.Append("<a href='").Append(Enc(Url("r", ranges[i][0], "from", null, "to", null, "p", null)))
              .Append(on ? "' class='mat-pill mat-pill--active'>" : "' class='mat-pill'>")
              .Append(Enc(ranges[i][1])).Append("</a>");
        }
        if (_range.Length == 0)
            sb.Append("<span class='mat-pill mat-pill--active'>").Append(Enc(PeriodLabel())).Append("</span>");
        target.Text = sb.ToString();
    }

    private string PeriodLabel()
    {
        if (_from.Length == 0 && _to.Length == 0) return "All time";
        if (_from.Length > 0 && _from == _to) return DateTime.Parse(_from, CultureInfo.InvariantCulture).ToString("d MMM yyyy");
        if (_from.Length > 0 && _to.Length > 0)
            return DateTime.Parse(_from, CultureInfo.InvariantCulture).ToString("d MMM yyyy") + " to " +
                   DateTime.Parse(_to, CultureInfo.InvariantCulture).ToString("d MMM yyyy");
        if (_from.Length > 0) return "From " + DateTime.Parse(_from, CultureInfo.InvariantCulture).ToString("d MMM yyyy");
        return "Up to " + DateTime.Parse(_to, CultureInfo.InvariantCulture).ToString("d MMM yyyy");
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

    private int PageCount(long total)
    {
        int n = (int)((total + _pageSize - 1) / _pageSize);
        return n < 1 ? 1 : n;
    }

    private void RenderPager(System.Web.UI.WebControls.Literal pager,
                             System.Web.UI.WebControls.Literal info, long total, int pageCount, string view)
    {
        long first = total == 0 ? 0 : (long)(_page - 1) * _pageSize + 1;
        long last = Math.Min((long)_page * _pageSize, total);
        info.Text = total == 0
            ? "Nothing to show"
            : "Showing " + N(first) + " to " + N(last) + " of " + N(total) + " &middot; page " + _page + " of " + pageCount;

        if (pageCount <= 1) { pager.Text = ""; return; }

        StringBuilder sb = new StringBuilder();
        sb.Append(PageLink(1, "&laquo; First", _page > 1));
        sb.Append(PageLink(_page - 1, "&lsaquo; Previous", _page > 1));

        int start = Math.Max(1, _page - 2);
        int end = Math.Min(pageCount, start + 4);
        start = Math.Max(1, end - 4);
        for (int i = start; i <= end; i++)
            sb.Append("<a href='").Append(Enc(Url("p", i.ToString(CultureInfo.InvariantCulture))))
              .Append(i == _page ? "' class='mat-pg mat-pg--active'>" : "' class='mat-pg'>")
              .Append(i).Append("</a>");

        sb.Append(PageLink(_page + 1, "Next &rsaquo;", _page < pageCount));
        sb.Append(PageLink(pageCount, "Last &raquo;", _page < pageCount));
        pager.Text = sb.ToString();
    }

    private string PageLink(int page, string label, bool enabled)
    {
        if (!enabled) return "<span class='mat-pg mat-pg--off'>" + label + "</span>";
        return "<a href='" + Enc(Url("p", page.ToString(CultureInfo.InvariantCulture))) + "' class='mat-pg'>" + label + "</a>";
    }

    /// <summary>
    /// The current URL with some parameters replaced. A null value drops the parameter.
    /// Everything the viewer has already chosen is carried across, so changing the page
    /// never quietly resets a filter.
    /// </summary>
    private string Url(params string[] overrides)
    {
        Dictionary<string, string> p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        p["view"] = _view;
        if (_range.Length > 0) p["r"] = _range;
        else { if (_from.Length > 0) p["from"] = _from; if (_to.Length > 0) p["to"] = _to; }
        if (_who.Length > 0) p["who"] = _who;
        if (_chg.Length > 0) p["chg"] = _chg;
        if (_src.Length > 0) p["src"] = _src;
        if (_act.Length > 0) p["act"] = _act;
        if (_q.Length > 0) p["q"] = _q;
        if (_pageSize != 50) p["ps"] = _pageSize.ToString(CultureInfo.InvariantCulture);
        if (_page > 1) p["p"] = _page.ToString(CultureInfo.InvariantCulture);

        for (int i = 0; i + 1 < overrides.Length; i += 2)
        {
            string k = overrides[i], v = overrides[i + 1];
            if (v == null) p.Remove(k); else p[k] = v;
        }

        StringBuilder sb = new StringBuilder("MarksAuditTrail.aspx?");
        bool firstPair = true;
        foreach (KeyValuePair<string, string> kv in p)
        {
            if (!firstPair) sb.Append("&");
            sb.Append(HttpUtility.UrlEncode(kv.Key)).Append("=").Append(HttpUtility.UrlEncode(kv.Value));
            firstPair = false;
        }
        return sb.ToString();
    }

    /// <summary>Switching tabs keeps the period but drops filters that mean nothing in the other view.</summary>
    private string UrlForView(string view)
    {
        string keep = "MarksAuditTrail.aspx?view=" + view;
        if (_range.Length > 0) keep += "&r=" + HttpUtility.UrlEncode(_range);
        else
        {
            if (_from.Length > 0) keep += "&from=" + HttpUtility.UrlEncode(_from);
            if (_to.Length > 0) keep += "&to=" + HttpUtility.UrlEncode(_to);
        }
        if (_q.Length > 0) keep += "&q=" + HttpUtility.UrlEncode(_q);
        return keep;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Small helpers
    // ════════════════════════════════════════════════════════════════════

    private static string Opt(string value, string label, string current)
    {
        return "<option value='" + Enc(value) + "'" + (value == current ? " selected='selected'" : "") + ">"
             + Enc(label) + "</option>";
    }

    /// <summary>A data- attribute, safe to drop straight into a tag.</summary>
    private static string A(string name, string value)
    {
        return " data-" + name + "=\"" + Enc(value) + "\"";
    }

    private static string Enc(string s)
    {
        if (s == null) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");
    }

    private static string Str(object o)
    {
        return (o == null || o == DBNull.Value) ? "" : o.ToString().Trim();
    }

    private static string Trim(string s, int max)
    {
        s = (s ?? "").Trim();
        return s.Length > max ? s.Substring(0, max) : s;
    }

    private static string Low(string s) { return (s ?? "").Trim().ToLowerInvariant(); }

    private static string Ymd(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

    /// <summary>Accepts yyyy-MM-dd and nothing else; anything odd becomes no filter at all.</summary>
    private static string ValidDate(string s)
    {
        DateTime d;
        if (DateTime.TryParseExact((s ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out d)) return Ymd(d);
        return "";
    }

    private static string N(object o)
    {
        if (o == null || o == DBNull.Value) return "0";
        try { return Convert.ToInt64(o).ToString("N0"); } catch { return o.ToString(); }
    }

    private static long Num(object o)
    {
        if (o == null || o == DBNull.Value) return 0;
        try { return Convert.ToInt64(o); } catch { return 0; }
    }

    private static string Get(Dictionary<string, string> map, string key)
    {
        if (map == null || key == null || key.Length == 0) return "";
        return map.ContainsKey(key) ? map[key] : "";
    }

    private static string Join(string a, string b)
    {
        a = (a ?? "").Trim(); b = (b ?? "").Trim();
        if (a.Length == 0) return b;
        if (b.Length == 0) return a;
        return a + " - " + b;
    }

    /// <summary>Quoted, de-duplicated IN list. Values come from the database; quotes are escaped anyway.</summary>
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

    private long ScalarLong(MySqlConnection conn, string sql)
    {
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.CommandTimeout = 45;
                object v = cmd.ExecuteScalar();
                return (v == null || v == DBNull.Value) ? 0 : Convert.ToInt64(v);
            }
        }
        catch { return 0; }
    }

    /// <summary>Same, but for the filtered counts that need the bound parameters.</summary>
    private long ScalarLong2(MySqlConnection conn, string sql)
    {
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (_view == "activity") BindActivityParams(cmd); else BindChangeParams(cmd);
                cmd.CommandTimeout = 90;
                object v = cmd.ExecuteScalar();
                return (v == null || v == DBNull.Value) ? 0 : Convert.ToInt64(v);
            }
        }
        catch { return 0; }
    }

    private static string After(string src, string marker)
    {
        if (src == null) return null;
        int i = src.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        return src.Substring(i + marker.Length);
    }
}
