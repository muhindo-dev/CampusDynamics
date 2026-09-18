using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using MySql.Data.MySqlClient;

/// <summary>
/// Admin Action Log — what administrators and managers did in the marks module.
///
/// Reads campus_dynamics.acad_marks_action_log, which every marks screen writes through
/// MarksActionLogger: who, which screen, which action, the outcome, how long it took, the
/// address it came from, and a small JSON context.
///
/// The context records a registration reference (`id`, or `ids` for a bulk action), never the
/// student. So the log on its own says "published #153586" and nothing a human can act on.
/// This page resolves those references against acad_course_registration and shows the student,
/// the course, the term and the marks as they stand now — which also makes the log searchable
/// by reg. number, student name and course code for the first time.
///
/// Read-only. Nothing here writes anything anywhere.
/// GET drives every view; ?ajax=detail&amp;id= returns one record in full; ?export=csv downloads.
/// </summary>
public partial class COOPERP_NewScreens_MarksActionLog : Page
{
    // ────────────────────────────────────────────────────────────────────
    //  Vocabulary
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Actions that only read. Everything else is treated as a change: an audit screen that
    /// guesses wrong should over-report, never hide. Verified against the dispatchers in
    /// AllMarksController, DeadlineManager, AuditCentre and CourseCorrectionCentre —
    /// "unlocks", for instance, only lists pending unlock requests.
    /// </summary>
    private static readonly string[] ViewOnlyActions = new string[]
    {
        "view_record", "view_details", "dropdowns", "init", "list", "get",
        "unlocks", "logs", "summary", "search", "stage_drift_count", "stats", "feed", "detail"
    };

    private static readonly string[] ViewOnlyPrefixes = new string[] { "batch_preview:", "view_", "preview" };

    private static readonly int[] PageSizes = new int[] { 25, 50, 100, 200 };

    /// <summary>How many log rows may be pulled into memory before the student resolution and
    /// the free-text search run. The table grows by roughly 150 rows a month.</summary>
    private const int CandidateCap = 20000;

    /// <summary>Registrations listed inside one detail panel. A bulk action can name hundreds.</summary>
    private const int DetailRecordCap = 60;

    // ────────────────────────────────────────────────────────────────────
    //  Request state
    // ────────────────────────────────────────────────────────────────────

    private string _range = "365";
    private string _from = "";
    private string _to = "";
    private string _who = "";
    private string _act = "";
    private string _screen = "";
    private string _out = "";
    private string _imp = "";          // changes | views | ""
    private string _q = "";
    private int _page = 1;
    private int _pageSize = 50;

    private MarksScope _scope;
    private bool _unrestricted;        // sees every programme
    private bool _truncated;           // the candidate cap was hit

    private static string ConnStr { get { return MarksConfiguration.ConnStr; } }

    // ────────────────────────────────────────────────────────────────────
    //  Page lifecycle
    // ────────────────────────────────────────────────────────────────────

    protected void Page_Load(object sender, EventArgs e)
    {
        ReadRequest();

        if (!ResolveAccess())
        {
            string ajax = Low(Request["ajax"]);
            if (ajax.Length > 0) { WriteJson("{\"ok\":false,\"message\":\"Access denied.\"}"); return; }
            RenderDenied();
            return;
        }

        if (Low(Request["ajax"]) == "detail") { WriteDetail(); return; }

        RenderHeader();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                List<Row> rows = LoadRows(conn);
                ResolveRegistrations(conn, rows);
                rows = ApplyMemoryFilters(rows);

                if (string.Equals(Request["export"], "csv", StringComparison.OrdinalIgnoreCase))
                {
                    ExportCsv(rows);
                    return;
                }

                LoadFilterOptions(conn);
                RenderQuickRanges();
                litFrom.Text = Enc(_from);
                litTo.Text = Enc(_to);
                litQ.Text = Enc(_q);
                litPsOpts.Text = PageSizeOptions();

                RenderStats(conn, rows);
                RenderTable(rows);
            }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            litNotice.Text += "<div class='mal-note mal-note--error'><b>The action log could not be loaded.</b><br />"
                            + Enc(ex.Message) + "</div>";
        }
    }

    private void ReadRequest()
    {
        System.Collections.Specialized.NameValueCollection qs = Request.QueryString;

        _who = Trim(qs["who"], 100);
        _act = Trim(qs["act"], 50);
        _screen = Trim(qs["screen"], 100);
        _out = Trim(qs["out"], 30);
        _imp = Low(qs["imp"]);
        _q = Trim(qs["q"], 80);
        if (_imp != "changes" && _imp != "views") _imp = "";

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
            default: _from = Ymd(new DateTime(today.Year, 1, 1)); _to = Ymd(today); break;
        }
    }

    /// <summary>
    /// Who may open this page, and how much of it they see.
    ///   RBAC administrator            → everything
    ///   dean / head of department     → only their own programmes; rows that name no
    ///                                   programme at all stay hidden
    ///   legacy audit role, no scope   → everything (this is how the page was reached before)
    ///   anyone else                   → nothing
    /// </summary>
    private bool ResolveAccess()
    {
        _scope = MarksScopeResolver.Resolve();

        if (_scope.IsAdmin) { _unrestricted = true; return true; }

        if (_scope.AllowedProgCodes != null && _scope.AllowedProgCodes.Count > 0)
        {
            _unrestricted = false;
            return true;
        }

        bool legacy = false;
        try { legacy = MarksAuthorizationService.CanViewAudit(); } catch { }
        if (legacy) { _unrestricted = true; return true; }

        return false;
    }

    private void RenderDenied()
    {
        pnlMain.Visible = false;
        litHeadSub.Text = "What administrators and managers did in the marks module.";
        litNotice.Text =
            "<div class='mal-note mal-note--error'><b>You do not have access to the admin action log.</b><br />" +
            "This page is open to administrators, deans, heads of department, the registrar and exam officers. " +
            "If you believe you should be one of them, ask the system administrator to check your role.</div>";
    }

    private void RenderHeader()
    {
        string sub = "Every action an administrator or manager took on marks: who, on which screen, " +
                     "to which student, and whether it went through.";
        if (!_unrestricted && _scope.Label != null && _scope.Label.Length > 0)
            sub += " Showing " + Enc(_scope.Label) + ".";
        litHeadSub.Text = sub;

        litExportBtn.Text = "<a href='" + Enc(Url("export", "csv", "p", null)) + "' class='mal-btn mal-btn--inv mal-btn--sm'>"
            + "<svg width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' "
            + "stroke-linecap='round' stroke-linejoin='round'><path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/>"
            + "<polyline points='7 10 12 15 17 10'/><line x1='12' y1='15' x2='12' y2='3'/></svg>Download CSV</a>";
    }

    // ════════════════════════════════════════════════════════════════════
    //  The rows
    // ════════════════════════════════════════════════════════════════════

    private sealed class Row
    {
        public long Id;
        public DateTime At;
        public string Who = "", Screen = "", Action = "", Outcome = "", Ip = "", Raw = "", Corr = "";
        public int Duration;
        public Dictionary<string, string> Ctx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<int> RegIds = new List<int>();
        public string CtxProg = "";        // programme named directly by the context
        public string BatchRef = "";       // CC-yyyymmdd-nnnn written by the Course Correction Centre
    }

    /// <summary>
    /// A Course Correction Centre batch. Those actions record a reference rather than a list of
    /// registrations, so the students they moved live in acad_correction_row and have to be
    /// fetched separately — otherwise the biggest changes on the log are the ones that show no
    /// student at all.
    /// </summary>
    private sealed class Batch
    {
        public string Ref = "", Operation = "", Status = "";
        public int StudentsAffected, RowsApplied;
        public string ReversedBy = "", ReversedAt = "", ReverseRef = "", Reason = "", ScopeLabel = "";
        public string TermLabel = "";
        public List<string[]> Rows = new List<string[]>();   // regno, course, verdict
        public List<string> Regnos = new List<string>();
        public Dictionary<string, bool> Seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, Batch> _batches = new Dictionary<string, Batch>(StringComparer.OrdinalIgnoreCase);

    private sealed class Reg
    {
        public int Id;
        public string Regno = "", Course = "", Prog = "", Year = "", Semester = "";
        public string Cw = "", Exam = "", Total = "", Status = "";
        public string StudentName = "", CourseName = "";
    }

    private Dictionary<int, Reg> _regs = new Dictionary<int, Reg>();

    private List<Row> LoadRows(MySqlConnection conn)
    {
        List<Row> rows = new List<Row>();
        StringBuilder where = new StringBuilder(" WHERE 1=1 ");
        if (_from.Length > 0) where.Append(" AND l.created_at >= @dfrom ");
        if (_to.Length > 0) where.Append(" AND l.created_at < @dto ");
        if (_who.Length > 0) where.Append(" AND l.username = @who ");
        if (_act.Length > 0) where.Append(" AND l.action = @act ");
        if (_screen.Length > 0) where.Append(" AND l.page = @screen ");
        if (_out.Length > 0) where.Append(" AND l.outcome = @out ");
        where.Append(ImpactSql());

        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT l.id, l.created_at, l.username, l.page, l.action, l.outcome, " +
            "       l.duration_ms, l.ip_address, l.context_json, l.correlation_id " +
            "FROM acad_marks_action_log l " + where +
            " ORDER BY l.id DESC LIMIT " + (CandidateCap + 1), conn))
        {
            BindRowParams(cmd);
            cmd.CommandTimeout = 60;
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    Row x = new Row();
                    x.Id = Convert.ToInt64(r["id"]);
                    x.At = r["created_at"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["created_at"]);
                    x.Who = Str(r["username"]);
                    x.Screen = Str(r["page"]);
                    x.Action = Str(r["action"]);
                    x.Outcome = Str(r["outcome"]);
                    x.Duration = r["duration_ms"] == DBNull.Value ? 0 : Convert.ToInt32(r["duration_ms"]);
                    x.Ip = Str(r["ip_address"]);
                    x.Raw = Str(r["context_json"]);
                    x.Corr = Str(r["correlation_id"]);
                    ParseContext(x);
                    rows.Add(x);
                }
        }

        if (rows.Count > CandidateCap)
        {
            rows.RemoveRange(CandidateCap, rows.Count - CandidateCap);
            _truncated = true;
        }
        return rows;
    }

    private void BindRowParams(MySqlCommand cmd)
    {
        if (_from.Length > 0) cmd.Parameters.AddWithValue("@dfrom", DateTime.Parse(_from, CultureInfo.InvariantCulture));
        if (_to.Length > 0) cmd.Parameters.AddWithValue("@dto", DateTime.Parse(_to, CultureInfo.InvariantCulture).AddDays(1));
        if (_who.Length > 0) cmd.Parameters.AddWithValue("@who", _who);
        if (_act.Length > 0) cmd.Parameters.AddWithValue("@act", _act);
        if (_screen.Length > 0) cmd.Parameters.AddWithValue("@screen", _screen);
        if (_out.Length > 0) cmd.Parameters.AddWithValue("@out", _out);
    }

    /// <summary>Built from constants only — nothing the caller typed reaches the SQL.</summary>
    private string ImpactSql()
    {
        if (_imp != "changes" && _imp != "views") return "";

        StringBuilder list = new StringBuilder();
        for (int i = 0; i < ViewOnlyActions.Length; i++)
        {
            if (i > 0) list.Append(",");
            list.Append("'").Append(ViewOnlyActions[i]).Append("'");
        }
        StringBuilder pre = new StringBuilder();
        for (int i = 0; i < ViewOnlyPrefixes.Length; i++)
            pre.Append(_imp == "views" ? " OR " : " AND ")
               .Append("l.action ").Append(_imp == "views" ? "LIKE" : "NOT LIKE")
               .Append(" '").Append(ViewOnlyPrefixes[i]).Append("%'");

        if (_imp == "views")
            return " AND (l.action IN (" + list + ")" + pre + ") ";
        return " AND (l.action NOT IN (" + list + ")" + pre + ") ";
    }

    private static bool IsViewOnly(string action)
    {
        string a = (action ?? "").Trim();
        for (int i = 0; i < ViewOnlyActions.Length; i++)
            if (string.Equals(a, ViewOnlyActions[i], StringComparison.OrdinalIgnoreCase)) return true;
        for (int i = 0; i < ViewOnlyPrefixes.Length; i++)
            if (a.StartsWith(ViewOnlyPrefixes[i], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ── the context blob ────────────────────────────────────────────────

    /// <summary>
    /// The context is always a flat object of string values written by MarksActionLogger, so a
    /// small reader beats pulling in a JSON library. Anything it cannot read is left alone and
    /// still shown raw in the detail panel.
    /// </summary>
    private static void ParseContext(Row x)
    {
        string s = x.Raw;
        if (s == null || s.Length < 2) return;

        int i = 0;
        while (i < s.Length)
        {
            int ks = s.IndexOf('"', i);
            if (ks < 0) break;
            int ke = FindQuoteEnd(s, ks + 1);
            if (ke < 0) break;
            string key = Unescape(s.Substring(ks + 1, ke - ks - 1));

            int colon = s.IndexOf(':', ke + 1);
            if (colon < 0) break;

            int vs = colon + 1;
            while (vs < s.Length && (s[vs] == ' ' || s[vs] == '\t')) vs++;
            if (vs >= s.Length) break;

            string val;
            if (s[vs] == '"')
            {
                int ve = FindQuoteEnd(s, vs + 1);
                if (ve < 0) break;
                val = Unescape(s.Substring(vs + 1, ve - vs - 1));
                i = ve + 1;
            }
            else
            {
                int ve = vs;
                while (ve < s.Length && s[ve] != ',' && s[ve] != '}') ve++;
                val = s.Substring(vs, ve - vs).Trim();
                i = ve;
            }

            if (key.Length > 0 && !x.Ctx.ContainsKey(key)) x.Ctx[key] = val;
        }

        // The registration references, however they were written.
        AddIds(x, Get(x.Ctx, "id"));
        AddIds(x, Get(x.Ctx, "ids"));
        AddIds(x, Get(x.Ctx, "regId"));

        x.CtxProg = First(Get(x.Ctx, "prog"), Get(x.Ctx, "programme"), Get(x.Ctx, "progcode"));

        foreach (KeyValuePair<string, string> kv in x.Ctx)
        {
            string found = FindBatchRef(kv.Value);
            if (found.Length > 0) { x.BatchRef = found; break; }
        }
    }

    /// <summary>A correction reference reads CC-yyyymmdd-nnnn and is buried in free text.</summary>
    private static string FindBatchRef(string s)
    {
        if (s == null || s.Length < 16) return "";
        int i = s.IndexOf("CC-", StringComparison.OrdinalIgnoreCase);
        while (i >= 0)
        {
            if (i + 16 <= s.Length)
            {
                string c = s.Substring(i, 16);
                bool ok = c[11] == '-';
                for (int k = 3; ok && k < 11; k++) if (!char.IsDigit(c[k])) ok = false;
                for (int k = 12; ok && k < 16; k++) if (!char.IsDigit(c[k])) ok = false;
                if (ok) return c.ToUpperInvariant();
            }
            i = s.IndexOf("CC-", i + 1, StringComparison.OrdinalIgnoreCase);
        }
        return "";
    }

    private static int FindQuoteEnd(string s, int from)
    {
        for (int i = from; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (s[i] == '"') return i;
        }
        return -1;
    }

    private static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        return s.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", " ").Replace("\\r", " ").Replace("\\t", " ");
    }

    private static void AddIds(Row x, string raw)
    {
        if (raw == null || raw.Length == 0) return;
        string[] parts = raw.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            int v;
            if (int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)
                && v > 0 && !x.RegIds.Contains(v))
                x.RegIds.Add(v);
        }
    }

    // ── resolving registrations to students ─────────────────────────────

    private void ResolveRegistrations(MySqlConnection conn, List<Row> rows)
    {
        ResolveBatches(conn, rows);

        List<int> ids = new List<int>();
        Dictionary<int, bool> seen = new Dictionary<int, bool>();
        for (int i = 0; i < rows.Count; i++)
            for (int j = 0; j < rows[i].RegIds.Count; j++)
            {
                int id = rows[i].RegIds[j];
                if (!seen.ContainsKey(id)) { seen[id] = true; ids.Add(id); }
            }
        if (ids.Count == 0) return;

        // Registrations first, keyed by primary key — cheap however many there are.
        for (int off = 0; off < ids.Count; off += 1000)
        {
            StringBuilder inList = new StringBuilder();
            for (int i = off; i < ids.Count && i < off + 1000; i++)
            {
                if (inList.Length > 0) inList.Append(",");
                inList.Append(ids[i].ToString(CultureInfo.InvariantCulture));
            }
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT ID, regno, courseID, prog_id, acad_year, semester, " +
                "       provisional_course_work_marks cw, provisional_exam_marks ex, " +
                "       provisional_total_marks tot, provisional_marks_status st " +
                "FROM campus_dynamics_portal.acad_course_registration WHERE ID IN (" + inList + ")", conn))
            {
                cmd.CommandTimeout = 60;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        Reg g = new Reg();
                        g.Id = Convert.ToInt32(r["ID"]);
                        g.Regno = Str(r["regno"]);
                        g.Course = Str(r["courseID"]);
                        g.Prog = Str(r["prog_id"]);
                        g.Year = Str(r["acad_year"]);
                        g.Semester = Str(r["semester"]);
                        g.Cw = Str(r["cw"]); g.Exam = Str(r["ex"]); g.Total = Str(r["tot"]);
                        g.Status = Str(r["st"]);
                        _regs[g.Id] = g;
                    }
            }
        }

        // Then the names, in one pass each. Joining these into the query above would be a
        // cross-database join on mismatched collations, which throws away the indexes.
        List<string> regnos = new List<string>(), courses = new List<string>();
        foreach (KeyValuePair<int, Reg> kv in _regs)
        {
            if (kv.Value.Regno.Length > 0) regnos.Add(kv.Value.Regno);
            if (kv.Value.Course.Length > 0) courses.Add(kv.Value.Course);
        }

        Dictionary<string, string> names = Lookup(conn, regnos,
            "SELECT regno k, CONCAT(IFNULL(firstname,''),' ',IFNULL(othername,'')) v FROM acad_student WHERE regno IN (");
        Dictionary<string, string> titles = Lookup(conn, courses,
            "SELECT courseID k, courseName v FROM acad_course WHERE courseID IN (");

        foreach (KeyValuePair<int, Reg> kv in _regs)
        {
            kv.Value.StudentName = Val(names, kv.Value.Regno);
            kv.Value.CourseName = Val(titles, kv.Value.Course);
        }
    }

    /// <summary>The students each referenced correction batch actually moved.</summary>
    private void ResolveBatches(MySqlConnection conn, List<Row> rows)
    {
        List<string> refs = new List<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            string br = rows[i].BatchRef;
            if (br.Length > 0 && !_batches.ContainsKey(br)) { _batches[br] = new Batch(); _batches[br].Ref = br; refs.Add(br); }
        }
        if (refs.Count == 0) return;

        string inList = InList(refs);
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT batch_ref, operation, status, students_affected, rows_applied, " +
                "       IFNULL(reversed_by,'') reversed_by, reversed_at, IFNULL(reverse_batch_ref,'') reverse_ref, " +
                "       IFNULL(reason,'') reason, IFNULL(scope_label,'') scope_label, " +
                "       IFNULL(source_year,'') source_year, IFNULL(target_year,'') target_year, " +
                "       IFNULL(source_semester,0) source_semester, IFNULL(target_semester,0) target_semester " +
                "FROM acad_correction_batch WHERE batch_ref IN (" + inList + ")", conn))
            {
                cmd.CommandTimeout = 45;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = Str(r["batch_ref"]);
                        if (!_batches.ContainsKey(k)) continue;
                        Batch b = _batches[k];
                        b.Operation = Str(r["operation"]);
                        b.Status = Str(r["status"]);
                        b.StudentsAffected = r["students_affected"] == DBNull.Value ? 0 : Convert.ToInt32(r["students_affected"]);
                        b.RowsApplied = r["rows_applied"] == DBNull.Value ? 0 : Convert.ToInt32(r["rows_applied"]);
                        b.ReversedBy = Str(r["reversed_by"]);
                        b.ReversedAt = r["reversed_at"] == DBNull.Value ? "" : Convert.ToDateTime(r["reversed_at"]).ToString("d MMM yyyy HH:mm");
                        b.ReverseRef = Str(r["reverse_ref"]);
                        b.Reason = Str(r["reason"]);
                        b.ScopeLabel = Str(r["scope_label"]);
                        b.TermLabel = TermMove(Str(r["source_year"]), Str(r["source_semester"]),
                                               Str(r["target_year"]), Str(r["target_semester"]));
                    }
            }

            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT b.batch_ref, IFNULL(r.regno,'') regno, IFNULL(r.course_code,'') course_code, " +
                "       IFNULL(r.verdict,'') verdict " +
                "FROM acad_correction_batch b JOIN acad_correction_row r ON r.batch_id = b.id " +
                "WHERE b.batch_ref IN (" + inList + ") ORDER BY b.batch_ref, r.regno LIMIT 5000", conn))
            {
                cmd.CommandTimeout = 60;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = Str(r["batch_ref"]);
                        if (!_batches.ContainsKey(k)) continue;
                        Batch b = _batches[k];
                        string regno = Str(r["regno"]), course = Str(r["course_code"]);
                        if (regno.Length > 0 && !b.Regnos.Contains(regno)) b.Regnos.Add(regno);

                        // acad_correction_row records one row per table the batch touched, so the
                        // same student appears several times. The panel wants students, not tables.
                        string key = regno + "|" + course;
                        if (b.Seen.ContainsKey(key)) continue;
                        b.Seen[key] = true;
                        b.Rows.Add(new string[] { regno, course, Str(r["verdict"]) });
                    }
            }

            // Resolved here rather than in the detail panel, so that typing a student's name
            // into the search box finds the correction that moved them.
            List<string> everyone = new List<string>();
            foreach (KeyValuePair<string, Batch> kv in _batches)
                for (int i = 0; i < kv.Value.Regnos.Count; i++)
                    if (!everyone.Contains(kv.Value.Regnos[i])) everyone.Add(kv.Value.Regnos[i]);

            Dictionary<string, string> names = StudentNames(conn, everyone);
            foreach (KeyValuePair<string, Batch> kv in _batches)
                for (int i = 0; i < kv.Value.Regnos.Count; i++)
                {
                    string nm = Val(names, kv.Value.Regnos[i]);
                    if (nm.Length > 0) kv.Value.Names[kv.Value.Regnos[i]] = nm;
                }
        }
        catch { }
    }

    private Batch BatchOf(Row x)
    {
        if (x.BatchRef.Length == 0) return null;
        Batch b;
        return _batches.TryGetValue(x.BatchRef, out b) ? b : null;
    }

    private Dictionary<string, string> StudentNames(MySqlConnection conn, List<string> regnos)
    {
        return Lookup(conn, regnos,
            "SELECT regno k, CONCAT(IFNULL(firstname,''),' ',IFNULL(othername,'')) v FROM acad_student WHERE regno IN (");
    }

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

    // ── filters that need the resolved student ──────────────────────────

    /// <summary>
    /// Programme scope and the free-text search both need to know which student an action
    /// touched, and that is only known after the registration references are resolved — so
    /// both run here rather than in the SQL.
    /// </summary>
    private List<Row> ApplyMemoryFilters(List<Row> rows)
    {
        string q = _q.ToLowerInvariant();
        List<Row> kept = new List<Row>();

        for (int i = 0; i < rows.Count; i++)
        {
            Row x = rows[i];
            if (!_unrestricted && !InScope(x)) continue;
            if (q.Length > 0 && !Matches(x, q)) continue;
            kept.Add(x);
        }
        return kept;
    }

    private bool InScope(Row x)
    {
        if (_scope.AllowedProgCodes == null || _scope.AllowedProgCodes.Count == 0) return false;

        if (x.CtxProg.Length > 0 && _scope.AllowsProg(x.CtxProg)) return true;

        for (int i = 0; i < x.RegIds.Count; i++)
        {
            Reg g;
            if (_regs.TryGetValue(x.RegIds[i], out g) && g.Prog.Length > 0 && _scope.AllowsProg(g.Prog))
                return true;
        }
        return false;
    }

    private bool Matches(Row x, string q)
    {
        if (Has(x.Who, q) || Has(x.Screen, q) || Has(x.Action, q) || Has(x.Ip, q) || Has(x.Raw, q))
            return true;
        if (Has(Describe(x), q)) return true;

        for (int i = 0; i < x.RegIds.Count; i++)
        {
            Reg g;
            if (!_regs.TryGetValue(x.RegIds[i], out g)) continue;
            if (Has(g.Regno, q) || Has(g.StudentName, q) || Has(g.Course, q)
                || Has(g.CourseName, q) || Has(g.Prog, q) || Has(g.Year, q))
                return true;
        }

        Batch b = BatchOf(x);
        if (b != null)
        {
            if (Has(b.Ref, q) || Has(b.ScopeLabel, q) || Has(b.Operation, q)) return true;
            for (int i = 0; i < b.Rows.Count; i++)
                if (Has(b.Rows[i][0], q) || Has(b.Rows[i][1], q)
                    || Has(Val(b.Names, b.Rows[i][0]), q)) return true;
        }
        return false;
    }

    private static bool Has(string haystack, string needleLower)
    {
        return haystack != null && haystack.Length > 0
            && haystack.ToLowerInvariant().IndexOf(needleLower, StringComparison.Ordinal) >= 0;
    }

    // ════════════════════════════════════════════════════════════════════
    //  Saying what happened, in words
    // ════════════════════════════════════════════════════════════════════

    /// <summary>One sentence a reader can act on, built from the action code and its context.</summary>
    private string Describe(Row x)
    {
        string a = x.Action ?? "";
        string tail = After(a, ":");
        int n = Count(x);

        switch (a)
        {
            case "edit_marks": return "Edited the marks";
            case "publish": return "Published the mark";
            case "unpublish": return "Withdrew the published mark";
            case "reset_to_pending": return "Sent the mark back to pending";
            case "delete_registration":
                return string.Equals(Get(x.Ctx, "force"), "True", StringComparison.OrdinalIgnoreCase)
                     ? "Force-deleted the course registration" : "Deleted the course registration";
            case "create_registration": return "Registered a student for a course";
            case "view_record": return "Opened a mark record";
            case "view_details": return "Opened the record details";
            case "dropdowns": return "Loaded the filter lists";
            case "init": return "Opened the screen";
            case "list": return "Listed the records";
            case "get": return "Opened one record";
            case "unlocks": return "Listed the pending unlock requests";
            case "logs": return "Opened the logs";
            case "summary": return "Loaded the summary";
            case "search": return "Ran a search";
            case "save": return "Saved a deadline";
            case "toggle": return "Switched a deadline on or off";
            case "review": return "Decided an unlock request";
            case "stage_drift_count": return "Checked for stage drift";
            case "stage_drift_repair": return "Repaired stage drift";
            case "COURSE_TRANSFER": return "Moved a course" + Between(x, "source", "target");
            case "TERM_TRANSFER": return "Moved a term" + Between(x, "sourceTerm", "targetTerm");
            case "CORRECTION_REVERSE": return "Reversed a correction";
            case "STUDENT_RECORD_REVERSE": return "Reversed one student's correction";
            case "SESSION_SECURITY": return "Session security event";
            case "IDLE_TIMEOUT": return "Signed out after going idle";
        }

        if (a.StartsWith("bulk_force_status:", StringComparison.OrdinalIgnoreCase))
            return "Forced " + Plural(n, "mark") + " to " + Words(After(a, ":"));
        if (a.StartsWith("bulk:", StringComparison.OrdinalIgnoreCase))
            return Cap(Past(tail)) + " " + Plural(n, "mark") + " in bulk";
        if (a.StartsWith("force_status:", StringComparison.OrdinalIgnoreCase))
            return "Forced the status to " + Words(tail);
        if (a.StartsWith("review:", StringComparison.OrdinalIgnoreCase))
            return "Reviewed the mark: " + Words(tail);
        if (a.StartsWith("batch_preview:", StringComparison.OrdinalIgnoreCase))
            return "Previewed a batch " + Words(tail);
        if (a.StartsWith("batch_execute:", StringComparison.OrdinalIgnoreCase))
            return "Ran a batch " + Words(tail) + " over " + Plural(n, "mark");

        return Words(a.Length > 0 ? a : "unknown action");
    }

    /// <summary>The supporting line under the sentence: the numbers the action carried.</summary>
    private string Detail(Row x)
    {
        List<string> bits = new List<string>();
        string a = x.Action ?? "";

        if (a == "edit_marks")
        {
            bits.Add("coursework " + Dash(Get(x.Ctx, "cw")));
            bits.Add("exam " + Dash(Get(x.Ctx, "exam")));
            bits.Add("total " + Dash(Get(x.Ctx, "total")));
        }
        if (a == "COURSE_TRANSFER" || a == "TERM_TRANSFER")
        {
            string prog = Get(x.Ctx, "programme");
            if (prog.Length > 0) bits.Add(prog);
            string result = Get(x.Ctx, "result");
            if (result.Length > 0) bits.Add(result);
        }
        if (a.StartsWith("batch_", StringComparison.OrdinalIgnoreCase))
        {
            string scope = Get(x.Ctx, "scope");
            if (scope.Length > 0) bits.Add(scope);
            string term = Join(Get(x.Ctx, "year"), Sem(Get(x.Ctx, "sem")));
            if (term.Length > 0) bits.Add(term);
            string prog = Get(x.Ctx, "prog");
            if (prog.Length > 0) bits.Add(prog);
        }

        string note = First(Get(x.Ctx, "note"), Get(x.Ctx, "comment"), Get(x.Ctx, "reason"));
        if (note.Length > 0) bits.Add("\"" + Clip(note, 60) + "\"");

        string err = Get(x.Ctx, "error");
        if (err.Length > 0) bits.Add(Clip(err, 80));

        string screen = x.Screen.Length > 0 ? Words(x.Screen) : "";
        string line = string.Join(" - ", bits.ToArray());
        if (screen.Length > 0) line = line.Length > 0 ? screen + " - " + line : screen;
        return line;
    }

    private string Between(Row x, string a, string b)
    {
        string s = Get(x.Ctx, a), t = Get(x.Ctx, b);
        if (s.Length == 0 && t.Length == 0) return "";
        return ": " + (s.Length > 0 ? s : "?") + " to " + (t.Length > 0 ? t : "?");
    }

    /// <summary>How many records the action says it touched.</summary>
    private static int Count(Row x)
    {
        int n;
        if (int.TryParse(Get(x.Ctx, "count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0)
            return n;
        return x.RegIds.Count;
    }

    private static string Past(string verb)
    {
        string v = (verb ?? "").Trim().ToLowerInvariant();
        if (v.Length == 0) return "Changed";
        if (v.EndsWith("ed")) return v;
        if (v.EndsWith("e")) return v + "d";
        return v + "ed";
    }

    // ════════════════════════════════════════════════════════════════════
    //  Rendering
    // ════════════════════════════════════════════════════════════════════

    private void RenderStats(MySqlConnection conn, List<Row> rows)
    {
        int changes = 0, views = 0, problems = 0;
        Dictionary<string, bool> students = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, bool> touched = new Dictionary<int, bool>();
        Dictionary<string, bool> people = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < rows.Count; i++)
        {
            Row x = rows[i];
            if (IsViewOnly(x.Action)) views++; else changes++;
            if (!string.Equals(x.Outcome, "success", StringComparison.OrdinalIgnoreCase)) problems++;
            if (x.Who.Length > 0) people[x.Who] = true;
            for (int j = 0; j < x.RegIds.Count; j++)
            {
                touched[x.RegIds[j]] = true;
                Reg g;
                if (_regs.TryGetValue(x.RegIds[j], out g) && g.Regno.Length > 0) students[g.Regno] = true;
            }

            Batch b = BatchOf(x);
            if (b != null)
                for (int j = 0; j < b.Regnos.Count; j++) students[b.Regnos[j]] = true;
        }

        litKpiActions.Text = N(rows.Count);
        litKpiActionsSub.Text = PeriodLabel() + " &middot; " + N(ScalarLong(conn,
            "SELECT COUNT(*) FROM acad_marks_action_log")) + " on record in total";

        litKpiChanges.Text = N(changes);
        litKpiChangesSub.Text = N(views) + " were only looking";

        litKpiStudents.Text = N(students.Count);
        litKpiStudentsSub.Text = N(touched.Count) + " registration" + (touched.Count == 1 ? "" : "s")
                               + " referenced by " + N(people.Count) + " "
                               + (people.Count == 1 ? "person" : "people");

        litKpiProblems.Text = N(problems);
        litKpiProblemsSub.Text = problems == 0 ? "everything succeeded" : "refused or failed";

        if (_truncated)
            litNotice.Text += "<div class='mal-note mal-note--warn'><b>Only the most recent "
                + N(CandidateCap) + " actions in this period were examined.</b> "
                + "Narrow the period to be sure you are seeing everything.</div>";
    }

    private void RenderTable(List<Row> rows)
    {
        int total = rows.Count;
        int pageCount = PageCount(total);
        if (_page > pageCount) _page = pageCount;

        int start = (_page - 1) * _pageSize;
        StringBuilder sb = new StringBuilder();

        if (total == 0)
        {
            sb.Append("<tr><td colspan='7'><div class='mal-empty'><b>Nothing to show</b>")
              .Append(HasFilter()
                  ? "No action matches these filters. Try widening the period, or clear the filters to start again."
                  : "No administrator action was recorded in this period.")
              .Append("</div></td></tr>");
        }
        else
        {
            for (int i = start; i < total && i < start + _pageSize; i++)
                sb.Append(RowHtml(rows[i]));
        }

        litRows.Text = sb.ToString();
        litMeta.Text = N(total) + (total == 1 ? " action" : " actions");
        RenderPager(total, pageCount);
    }

    private string RowHtml(Row x)
    {
        StringBuilder sb = new StringBuilder("<tr>");

        sb.Append("<td class='mal-when'><span class='mal-when__d'>").Append(Enc(x.At.ToString("dd MMM yy")))
          .Append("</span><span class='mal-when__t'>").Append(Enc(x.At.ToString("HH:mm"))).Append("</span></td>");

        sb.Append("<td><span class='mal-who'>").Append(Enc(x.Who.Length > 0 ? x.Who : "(not recorded)")).Append("</span>");
        if (x.Ip.Length > 0) sb.Append("<span class='mal-who__sub'>").Append(Enc(x.Ip)).Append("</span>");
        sb.Append("</td>");

        string detail = Detail(x);
        sb.Append("<td><span class='mal-what'>").Append(Enc(Describe(x))).Append("</span>");
        if (detail.Length > 0) sb.Append("<span class='mal-what__sub'>").Append(Enc(detail)).Append("</span>");
        sb.Append("</td>");

        sb.Append("<td>").Append(StudentCell(x)).Append("</td>");
        sb.Append("<td>").Append(CourseCell(x)).Append("</td>");

        sb.Append("<td><span class='mal-badge ").Append(OutcomeClass(x.Outcome)).Append("'>")
          .Append(Enc(OutcomeLabel(x.Outcome))).Append("</span>");
        if (x.Duration >= 800)
            sb.Append("<span class='mal-sub mal-dur mal-dur--slow'>").Append(N(x.Duration)).Append(" ms</span>");
        sb.Append("</td>");

        sb.Append("<td><button type='button' class='mal-btn mal-btn--ghost mal-btn--sm' onclick='malOpen(this)' data-id=\"")
          .Append(x.Id.ToString(CultureInfo.InvariantCulture)).Append("\">Details</button></td>");

        return sb.Append("</tr>").ToString();
    }

    private string StudentCell(Row x)
    {
        List<Reg> found = Resolved(x);

        if (found.Count == 0)
        {
            string regno = Get(x.Ctx, "regno");
            if (regno.Length > 0) return "<span class='mal-code'>" + Enc(regno) + "</span>";

            Batch b = BatchOf(x);
            if (b != null && b.Regnos.Count > 0)
            {
                StringBuilder bs = new StringBuilder();
                if (b.Regnos.Count == 1)
                {
                    bs.Append("<span class='mal-code'>").Append(Enc(b.Regnos[0])).Append("</span>");
                    string nm = Val(b.Names, b.Regnos[0]);
                    if (nm.Length > 0) bs.Append("<span class='mal-sub'>").Append(Enc(nm)).Append("</span>");
                }
                else
                    bs.Append("<span class='mal-many'>").Append(N(b.Regnos.Count)).Append(" students</span>")
                      .Append("<span class='mal-sub'>").Append(Enc(b.Regnos[0])).Append(" and ")
                      .Append(N(b.Regnos.Count - 1)).Append(" more</span>");
                return bs.ToString();
            }
            if (b != null && b.StudentsAffected > 0)
                return "<span class='mal-many'>" + N(b.StudentsAffected) + " students</span>"
                     + "<span class='mal-sub'>" + Enc(b.Ref) + "</span>";

            if (x.RegIds.Count > 0)
                return "<span class='mal-nil'>registration #" + Enc(x.RegIds[0].ToString(CultureInfo.InvariantCulture))
                     + "</span><span class='mal-sub'>no longer on file</span>";
            return "<span class='mal-nil'>&mdash;</span>";
        }

        if (found.Count == 1)
        {
            Reg g = found[0];
            StringBuilder sb = new StringBuilder("<span class='mal-code'>").Append(Enc(g.Regno)).Append("</span>");
            if (g.StudentName.Length > 0) sb.Append("<span class='mal-sub'>").Append(Enc(g.StudentName)).Append("</span>");
            return sb.ToString();
        }

        Dictionary<string, bool> distinct = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < found.Count; i++) if (found[i].Regno.Length > 0) distinct[found[i].Regno] = true;

        StringBuilder m = new StringBuilder("<span class='mal-many'>")
            .Append(N(distinct.Count)).Append(distinct.Count == 1 ? " student" : " students").Append("</span>");
        m.Append("<span class='mal-sub'>").Append(Enc(found[0].Regno));
        if (found.Count > 1) m.Append(" and ").Append(N(found.Count - 1)).Append(" more");
        m.Append("</span>");
        return m.ToString();
    }

    private string CourseCell(Row x)
    {
        List<Reg> found = Resolved(x);

        if (found.Count == 0)
        {
            string c = Get(x.Ctx, "course");
            string t = Join(Get(x.Ctx, "year"), Sem(Get(x.Ctx, "sem")));
            if (c.Length > 0)
                return "<span class='mal-code'>" + Enc(c) + "</span>"
                     + (t.Length > 0 ? "<span class='mal-sub'>" + Enc(t) + "</span>" : "");
            string src = Get(x.Ctx, "source"), tgt = Get(x.Ctx, "target");
            if (src.Length > 0 || tgt.Length > 0)
            {
                StringBuilder cs = new StringBuilder();
                if (src.Length > 0) cs.Append("<span class='mal-code'>").Append(Enc(src)).Append("</span>");
                if (tgt.Length > 0)
                    cs.Append(src.Length > 0 ? " " : "").Append("<span class='mal-code'>").Append(Enc(tgt)).Append("</span>");
                if (x.CtxProg.Length > 0) cs.Append("<span class='mal-sub'>").Append(Enc(x.CtxProg)).Append("</span>");
                return cs.ToString();
            }
            if (x.CtxProg.Length > 0)
                return "<span class='mal-code'>" + Enc(x.CtxProg) + "</span>"
                     + (t.Length > 0 ? "<span class='mal-sub'>" + Enc(t) + "</span>" : "");
            return "<span class='mal-nil'>&mdash;</span>";
        }

        Dictionary<string, bool> codes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < found.Count; i++) if (found[i].Course.Length > 0) codes[found[i].Course] = true;

        Reg g = found[0];
        StringBuilder sb = new StringBuilder("<span class='mal-code'>").Append(Enc(g.Course)).Append("</span>");
        if (codes.Count > 1)
            sb.Append("<span class='mal-sub'>and ").Append(N(codes.Count - 1)).Append(" other course")
              .Append(codes.Count == 2 ? "" : "s").Append("</span>");
        else
        {
            string term = Join(Join(g.Prog, g.Year), Sem(g.Semester));
            if (term.Length > 0) sb.Append("<span class='mal-sub'>").Append(Enc(term)).Append("</span>");
        }
        return sb.ToString();
    }

    private List<Reg> Resolved(Row x)
    {
        List<Reg> found = new List<Reg>();
        for (int i = 0; i < x.RegIds.Count; i++)
        {
            Reg g;
            if (_regs.TryGetValue(x.RegIds[i], out g)) found.Add(g);
        }
        return found;
    }

    private bool HasFilter()
    {
        return _who.Length > 0 || _act.Length > 0 || _screen.Length > 0 || _out.Length > 0
            || _imp.Length > 0 || _q.Length > 0 || _from.Length > 0 || _to.Length > 0;
    }

    // ── filter options ──────────────────────────────────────────────────

    private void LoadFilterOptions(MySqlConnection conn)
    {
        litWhoOpts.Text = GroupOptions(conn, "username", _who, "Anyone", null);
        litScreenOpts.Text = GroupOptions(conn, "page", _screen, "Every screen", "screen");
        litActOpts.Text = GroupOptions(conn, "action", _act, "Every action", "action");
        litOutOpts.Text = GroupOptions(conn, "outcome", _out, "Any outcome", "outcome");

        litImpOpts.Text =
            Opt("", "Anything", _imp) +
            Opt("changes", "Changed something", _imp) +
            Opt("views", "Only looked", _imp);
    }

    /// <summary>
    /// The column name is a compile-time constant on every call, never anything a caller typed.
    /// Options carry their counts so an empty filter is obvious before it is applied.
    /// </summary>
    private string GroupOptions(MySqlConnection conn, string column, string current, string allLabel, string labelKind)
    {
        StringBuilder sb = new StringBuilder("<option value=''>" + Enc(allLabel) + "</option>");
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT l." + column + " k, COUNT(*) n FROM acad_marks_action_log l " +
                "WHERE l." + column + " IS NOT NULL AND l." + column + " <> '' " +
                "GROUP BY l." + column + " ORDER BY n DESC LIMIT 200", conn))
            {
                cmd.CommandTimeout = 30;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string v = Str(r["k"]);
                        if (v.Length == 0) continue;
                        string label = v;
                        if (labelKind == "action") label = ActionOptionLabel(v);
                        else if (labelKind == "outcome") label = OutcomeLabel(v);
                        else if (labelKind == "screen") label = Words(v);
                        sb.Append("<option value='").Append(Enc(v)).Append("'")
                          .Append(v == current ? " selected='selected'" : "").Append(">")
                          .Append(Enc(label)).Append(" (").Append(N(r["n"])).Append(")</option>");
                    }
            }
        }
        catch { }

        // A filter the viewer arrived with must stay selectable even if it fell off the list.
        if (current.Length > 0 && sb.ToString().IndexOf("value='" + Enc(current) + "'", StringComparison.Ordinal) < 0)
            sb.Append("<option value='").Append(Enc(current)).Append("' selected='selected'>")
              .Append(Enc(current)).Append("</option>");

        return sb.ToString();
    }

    /// <summary>Dropdown label for an action code — no context to draw on, so it stays generic.</summary>
    private static string ActionOptionLabel(string a)
    {
        string tail = After(a, ":");
        if (a.StartsWith("bulk_force_status:", StringComparison.OrdinalIgnoreCase)) return "Bulk force status: " + Words(tail);
        if (a.StartsWith("bulk:", StringComparison.OrdinalIgnoreCase)) return "Bulk " + Words(tail);
        if (a.StartsWith("force_status:", StringComparison.OrdinalIgnoreCase)) return "Force status: " + Words(tail);
        if (a.StartsWith("review:", StringComparison.OrdinalIgnoreCase)) return "Review: " + Words(tail);
        if (a.StartsWith("batch_preview:", StringComparison.OrdinalIgnoreCase)) return "Batch preview: " + Words(tail);
        if (a.StartsWith("batch_execute:", StringComparison.OrdinalIgnoreCase)) return "Batch run: " + Words(tail);

        switch (a)
        {
            case "edit_marks": return "Edit marks";
            case "publish": return "Publish a mark";
            case "reset_to_pending": return "Reset to pending";
            case "delete_registration": return "Delete a registration";
            case "create_registration": return "Create a registration";
            case "view_record": return "Open a record";
            case "view_details": return "Open record details";
            case "unlocks": return "List unlock requests";
            case "dropdowns": return "Load filter lists";
            default: return Words(a);
        }
    }

    private static string OutcomeLabel(string o)
    {
        switch ((o ?? "").ToLowerInvariant())
        {
            case "success": return "Went through";
            case "error": return "Error";
            case "auth_fail": return "Not allowed";
            case "validation_fail": return "Refused";
            case "locked": return "Locked";
            case "idle_timeout": return "Idle timeout";
            case "": return "Not recorded";
            default: return Words(o);
        }
    }

    private static string OutcomeClass(string o)
    {
        switch ((o ?? "").ToLowerInvariant())
        {
            case "success": return "mal-badge--green";
            case "error": return "mal-badge--red";
            case "auth_fail": return "mal-badge--red";
            case "validation_fail": return "mal-badge--amber";
            case "locked": return "mal-badge--amber";
            default: return "mal-badge--grey";
        }
    }

    // ── chrome ──────────────────────────────────────────────────────────

    private void RenderQuickRanges()
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
              .Append(on ? "' class='mal-pill mal-pill--active'>" : "' class='mal-pill'>")
              .Append(Enc(ranges[i][1])).Append("</a>");
        }
        if (_range.Length == 0)
            sb.Append("<span class='mal-pill mal-pill--active'>").Append(Enc(PeriodLabel())).Append("</span>");
        litQuick.Text = sb.ToString();
    }

    private string PeriodLabel()
    {
        if (_from.Length == 0 && _to.Length == 0) return "All time";
        if (_from.Length > 0 && _from == _to) return D(_from);
        if (_from.Length > 0 && _to.Length > 0) return D(_from) + " to " + D(_to);
        if (_from.Length > 0) return "From " + D(_from);
        return "Up to " + D(_to);
    }

    private static string D(string ymd)
    {
        return DateTime.Parse(ymd, CultureInfo.InvariantCulture).ToString("d MMM yyyy");
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

    private int PageCount(int total)
    {
        int n = (total + _pageSize - 1) / _pageSize;
        return n < 1 ? 1 : n;
    }

    private void RenderPager(int total, int pageCount)
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
              .Append(i == _page ? "' class='mal-pg mal-pg--active'>" : "' class='mal-pg'>")
              .Append(i).Append("</a>");

        sb.Append(PageLink(_page + 1, "Next &rsaquo;", _page < pageCount));
        sb.Append(PageLink(pageCount, "Last &raquo;", _page < pageCount));
        litPager.Text = sb.ToString();
    }

    private string PageLink(int page, string label, bool enabled)
    {
        if (!enabled) return "<span class='mal-pg mal-pg--off'>" + label + "</span>";
        return "<a href='" + Enc(Url("p", page.ToString(CultureInfo.InvariantCulture))) + "' class='mal-pg'>" + label + "</a>";
    }

    /// <summary>The current URL with some parameters replaced; a null value drops one.</summary>
    private string Url(params string[] overrides)
    {
        Dictionary<string, string> p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_range.Length > 0) p["r"] = _range;
        else { if (_from.Length > 0) p["from"] = _from; if (_to.Length > 0) p["to"] = _to; }
        if (_who.Length > 0) p["who"] = _who;
        if (_act.Length > 0) p["act"] = _act;
        if (_screen.Length > 0) p["screen"] = _screen;
        if (_out.Length > 0) p["out"] = _out;
        if (_imp.Length > 0) p["imp"] = _imp;
        if (_q.Length > 0) p["q"] = _q;
        if (_pageSize != 50) p["ps"] = _pageSize.ToString(CultureInfo.InvariantCulture);
        if (_page > 1) p["p"] = _page.ToString(CultureInfo.InvariantCulture);

        for (int i = 0; i + 1 < overrides.Length; i += 2)
        {
            string k = overrides[i], v = overrides[i + 1];
            if (v == null) p.Remove(k); else p[k] = v;
        }

        StringBuilder sb = new StringBuilder("MarksActionLog.aspx");
        bool firstPair = true;
        foreach (KeyValuePair<string, string> kv in p)
        {
            sb.Append(firstPair ? "?" : "&");
            sb.Append(HttpUtility.UrlEncode(kv.Key)).Append("=").Append(HttpUtility.UrlEncode(kv.Value));
            firstPair = false;
        }
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════
    //  One record in full (?ajax=detail&id=)
    // ════════════════════════════════════════════════════════════════════

    private void WriteDetail()
    {
        long id;
        long.TryParse((Request["id"] ?? "").Trim(), out id);
        if (id <= 0) { WriteJson("{\"ok\":false,\"message\":\"No record was asked for.\"}"); return; }

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                Row x = null;
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT l.id, l.created_at, l.username, l.page, l.action, l.outcome, " +
                    "       l.duration_ms, l.ip_address, l.context_json, l.correlation_id " +
                    "FROM acad_marks_action_log l WHERE l.id = @id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.CommandTimeout = 30;
                    using (MySqlDataReader r = cmd.ExecuteReader())
                        if (r.Read())
                        {
                            x = new Row();
                            x.Id = Convert.ToInt64(r["id"]);
                            x.At = r["created_at"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["created_at"]);
                            x.Who = Str(r["username"]);
                            x.Screen = Str(r["page"]);
                            x.Action = Str(r["action"]);
                            x.Outcome = Str(r["outcome"]);
                            x.Duration = r["duration_ms"] == DBNull.Value ? 0 : Convert.ToInt32(r["duration_ms"]);
                            x.Ip = Str(r["ip_address"]);
                            x.Raw = Str(r["context_json"]);
                            x.Corr = Str(r["correlation_id"]);
                            ParseContext(x);
                        }
                }

                if (x == null) { WriteJson("{\"ok\":false,\"message\":\"That record no longer exists.\"}"); return; }

                List<Row> one = new List<Row>(); one.Add(x);
                ResolveRegistrations(conn, one);

                if (!_unrestricted && !InScope(x))
                { WriteJson("{\"ok\":false,\"message\":\"That record is outside your faculty.\"}"); return; }

                WriteJson(DetailJson(conn, x));
            }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            WriteJson("{\"ok\":false,\"message\":" + J(ex.Message) + "}");
        }
    }

    private string DetailJson(MySqlConnection conn, Row x)
    {
        StringBuilder sb = new StringBuilder("{\"ok\":true,\"row\":{");
        sb.Append("\"id\":").Append(x.Id);
        sb.Append(",\"what\":").Append(J(Describe(x)));
        sb.Append(",\"who\":").Append(J(x.Who.Length > 0 ? x.Who : "(not recorded)"));
        sb.Append(",\"staff\":").Append(J(StaffNote(conn, x.Who)));
        sb.Append(",\"at\":").Append(J(x.At == DateTime.MinValue ? "" : x.At.ToString("dddd d MMMM yyyy 'at' HH:mm:ss")));
        sb.Append(",\"screen\":").Append(J(Words(x.Screen)));
        sb.Append(",\"action\":").Append(J(x.Action));
        sb.Append(",\"outcomeLabel\":").Append(J(OutcomeLabel(x.Outcome)));
        sb.Append(",\"ip\":").Append(J(x.Ip));
        sb.Append(",\"duration\":").Append(J(x.Duration > 0 ? N(x.Duration) + " ms" : ""));
        sb.Append(",\"corr\":").Append(J(x.Corr));
        sb.Append(",\"raw\":").Append(J(Pretty(x.Raw)));
        sb.Append("}");

        // the registrations behind the action, then the students a correction batch moved
        List<Reg> found = Resolved(x);
        Batch b = BatchOf(x);
        int totalRecords = found.Count + (b == null ? 0 : b.Rows.Count);

        sb.Append(",\"records\":[");
        int shown = 0;
        for (int i = 0; i < found.Count && shown < DetailRecordCap; i++, shown++)
        {
            Reg g = found[i];
            if (shown > 0) sb.Append(",");
            sb.Append("{\"regno\":").Append(J(g.Regno));
            sb.Append(",\"name\":").Append(J(g.StudentName));
            sb.Append(",\"course\":").Append(J(g.Course));
            sb.Append(",\"courseName\":").Append(J(g.CourseName));
            sb.Append(",\"term\":").Append(J(Join(Join(g.Prog, g.Year), Sem(g.Semester))));
            sb.Append(",\"marks\":").Append(J(MarksNow(g)));
            sb.Append(",\"status\":").Append(J(g.Status));
            sb.Append("}");
        }
        if (b != null)
        {
            for (int i = 0; i < b.Rows.Count && shown < DetailRecordCap; i++, shown++)
            {
                if (shown > 0) sb.Append(",");
                sb.Append("{\"regno\":").Append(J(b.Rows[i][0]));
                sb.Append(",\"name\":").Append(J(Val(b.Names, b.Rows[i][0])));
                sb.Append(",\"course\":").Append(J(b.Rows[i][1]));
                sb.Append(",\"courseName\":").Append(J(""));
                sb.Append(",\"term\":").Append(J(b.TermLabel));
                sb.Append(",\"marks\":").Append(J(""));
                sb.Append(",\"status\":").Append(J(b.Rows[i][2]));
                sb.Append("}");
            }
        }
        sb.Append("]");
        if (totalRecords > shown) sb.Append(",\"moreRecords\":").Append(totalRecords);

        int missing = x.RegIds.Count - found.Count;
        if (missing > 0)
            sb.Append(",\"missing\":").Append(J(missing + (missing == 1
                ? " registration named by this action no longer exists."
                : " registrations named by this action no longer exist.")
                + " That is expected for a deletion."));

        if (b != null)
        {
            string note = "Correction " + b.Ref
                        + (b.Operation.Length > 0 ? " (" + Words(b.Operation) + ")" : "")
                        + ": " + N(b.StudentsAffected) + " student" + (b.StudentsAffected == 1 ? "" : "s") + ", "
                        + N(b.RowsApplied) + " row" + (b.RowsApplied == 1 ? "" : "s") + " applied.";
            if (b.ScopeLabel.Length > 0) note += " Run for: " + b.ScopeLabel + ".";
            if (b.ReversedBy.Length > 0)
                note += " Reversed by " + b.ReversedBy
                      + (b.ReversedAt.Length > 0 ? " on " + b.ReversedAt : "")
                      + (b.ReverseRef.Length > 0 ? " as " + b.ReverseRef : "") + ".";
            else if (b.Status.Length > 0 && !string.Equals(b.Status, "APPLIED", StringComparison.OrdinalIgnoreCase))
                note += " Status: " + b.Status + ".";
            sb.Append(",\"batchNote\":").Append(J(note));
        }

        if (totalRecords == 0 && x.RegIds.Count == 0)
            sb.Append(",\"noRecordsNote\":").Append(J(
                "This action did not name a student registration. " +
                (x.CtxProg.Length > 0 ? "It applied to programme " + x.CtxProg + "." : "It was a screen-level action.")));

        // the raw context, spelled out
        sb.Append(",\"context\":[");
        bool firstKv = true;
        foreach (KeyValuePair<string, string> kv in x.Ctx)
        {
            if (kv.Value == null || kv.Value.Length == 0) continue;
            if (!firstKv) sb.Append(",");
            sb.Append("{\"k\":").Append(J(CtxLabel(kv.Key))).Append(",\"v\":").Append(J(kv.Value)).Append("}");
            firstKv = false;
        }
        sb.Append("]}");
        return sb.ToString();
    }

    /// <summary>"2025/2026 Sem 1 to 2024/2025 Sem 2", with whichever halves were recorded.</summary>
    private static string TermMove(string sy, string ss, string ty, string ts)
    {
        string a = Join(sy, ss == "0" ? "" : Sem(ss));
        string b = Join(ty, ts == "0" ? "" : Sem(ts));
        if (a.Length == 0 && b.Length == 0) return "";
        if (a.Length == 0) return b;
        if (b.Length == 0) return a;
        return a == b ? a : a + " to " + b;
    }

    private static string MarksNow(Reg g)
    {
        if (g.Cw.Length == 0 && g.Exam.Length == 0 && g.Total.Length == 0) return "not entered";
        return "CW " + Dash(g.Cw) + " / Exam " + Dash(g.Exam) + " / Total " + Dash(g.Total);
    }

    private string StaffNote(MySqlConnection conn, string who)
    {
        if (who == null || who.Trim().Length == 0) return "";
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT e.EMP_CODE, IFNULL(d.dept_name,'') dept FROM hrm_employee e " +
                "LEFT JOIN hrm_emp_contracts c ON c.empID = e.empID AND c.ID = " +
                "     (SELECT MAX(c2.ID) FROM hrm_emp_contracts c2 WHERE c2.empID = e.empID) " +
                "LEFT JOIN hrm_departments d ON d.ID = c.departmentID " +
                "WHERE e.usernames = @w OR e.emp_name = @w LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@w", who.Trim());
                cmd.CommandTimeout = 20;
                using (MySqlDataReader r = cmd.ExecuteReader())
                    if (r.Read()) return Join(Str(r["EMP_CODE"]), Str(r["dept"]));
            }
        }
        catch { }
        return "";
    }

    private static string CtxLabel(string k)
    {
        switch ((k ?? "").ToLowerInvariant())
        {
            case "id": return "Registration";
            case "ids": return "Registrations";
            case "count": return "How many";
            case "actor": return "Recorded actor";
            case "status": return "New status";
            case "comment": return "Comment";
            case "note": return "Note";
            case "reason": return "Reason";
            case "cw": return "Coursework entered";
            case "exam": return "Exam entered";
            case "total": return "Total entered";
            case "force": return "Forced";
            case "error": return "Error";
            case "regno": return "Student";
            case "course": return "Course";
            case "year": return "Academic year";
            case "sem": return "Semester";
            case "prog": return "Programme";
            case "programme": return "Programme";
            case "scope": return "Applied to";
            case "source": return "From";
            case "target": return "To";
            case "sourceterm": return "From term";
            case "targetterm": return "To term";
            case "result": return "Result";
            default: return Words(k);
        }
    }

    private static string Pretty(string json)
    {
        if (json == null) return "";
        return json.Replace(",\"", ",\n \"").Replace("{\"", "{\n \"").Replace("\"}", "\"\n}");
    }

    private void WriteJson(string body)
    {
        Response.Clear();
        Response.ContentType = "application/json; charset=utf-8";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Write(body);
        Response.End();
    }

    // ════════════════════════════════════════════════════════════════════
    //  CSV
    // ════════════════════════════════════════════════════════════════════

    private void ExportCsv(List<Row> rows)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine("Entry,When,Who,IP address,Screen,Action code,What happened,Effect,Outcome," +
                       "Students,Student names,Courses,Term,Registrations,Duration ms,Context");

        for (int i = 0; i < rows.Count; i++)
        {
            Row x = rows[i];
            List<Reg> found = Resolved(x);

            List<string> regnos = new List<string>(), names = new List<string>(), courses = new List<string>();
            for (int j = 0; j < found.Count; j++)
            {
                if (found[j].Regno.Length > 0 && !regnos.Contains(found[j].Regno)) regnos.Add(found[j].Regno);
                if (found[j].StudentName.Length > 0 && !names.Contains(found[j].StudentName)) names.Add(found[j].StudentName);
                if (found[j].Course.Length > 0 && !courses.Contains(found[j].Course)) courses.Add(found[j].Course);
            }
            Batch bx = BatchOf(x);
            if (bx != null)
                for (int j = 0; j < bx.Regnos.Count; j++)
                    if (!regnos.Contains(bx.Regnos[j])) regnos.Add(bx.Regnos[j]);

            string term = found.Count > 0 ? Join(Join(found[0].Prog, found[0].Year), Sem(found[0].Semester))
                                          : (bx != null ? bx.Ref : "");
            StringBuilder ids = new StringBuilder();
            for (int j = 0; j < x.RegIds.Count; j++)
            {
                if (ids.Length > 0) ids.Append(" ");
                ids.Append(x.RegIds[j].ToString(CultureInfo.InvariantCulture));
            }

            csv.Append(C(x.Id.ToString(CultureInfo.InvariantCulture)))
               .Append(C(x.At == DateTime.MinValue ? "" : x.At.ToString("yyyy-MM-dd HH:mm:ss")))
               .Append(C(x.Who)).Append(C(x.Ip)).Append(C(x.Screen)).Append(C(x.Action))
               .Append(C(Describe(x) + (Detail(x).Length > 0 ? " - " + Detail(x) : "")))
               .Append(C(IsViewOnly(x.Action) ? "viewed" : "changed"))
               .Append(C(OutcomeLabel(x.Outcome)))
               .Append(C(string.Join(" ", regnos.ToArray())))
               .Append(C(string.Join("; ", names.ToArray())))
               .Append(C(string.Join(" ", courses.ToArray())))
               .Append(C(term))
               .Append(C(ids.ToString()))
               .Append(C(x.Duration.ToString(CultureInfo.InvariantCulture)))
               .Append(CLast(x.Raw));
        }

        string name = "AdminActionLog_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv";
        Response.Clear();
        Response.ContentType = "text/csv; charset=utf-8";
        Response.AddHeader("Content-Disposition", "attachment; filename=" + name);
        Response.ContentEncoding = Encoding.UTF8;
        Response.Write("\uFEFF");   // the byte-order mark, so Excel opens it as UTF-8
        Response.Write(csv.ToString());
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
    //  Small helpers
    // ════════════════════════════════════════════════════════════════════

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

    /// <summary>JSON string literal, safe inside a script-free JSON response.</summary>
    private static string J(string s)
    {
        return HttpUtility.JavaScriptStringEncode(s ?? "", true);
    }

    private static string Str(object o) { return (o == null || o == DBNull.Value) ? "" : o.ToString().Trim(); }

    private static string Trim(string s, int max)
    {
        s = (s ?? "").Trim();
        return s.Length > max ? s.Substring(0, max) : s;
    }

    private static string Low(string s) { return (s ?? "").Trim().ToLowerInvariant(); }

    private static string Ymd(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

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

    private static string Get(Dictionary<string, string> map, string key)
    {
        if (map == null || key == null) return "";
        return map.ContainsKey(key) ? (map[key] ?? "") : "";
    }

    private static string Val(Dictionary<string, string> map, string key)
    {
        if (map == null || key == null || key.Length == 0) return "";
        return map.ContainsKey(key) ? map[key] : "";
    }

    private static string First(params string[] options)
    {
        for (int i = 0; i < options.Length; i++)
            if (options[i] != null && options[i].Trim().Length > 0) return options[i].Trim();
        return "";
    }

    private static string Join(string a, string b)
    {
        a = (a ?? "").Trim(); b = (b ?? "").Trim();
        if (a.Length == 0) return b;
        if (b.Length == 0) return a;
        return a + " - " + b;
    }

    private static string Sem(string s)
    {
        s = (s ?? "").Trim();
        return s.Length == 0 ? "" : "Sem " + s;
    }

    private static string Dash(string s) { return (s == null || s.Trim().Length == 0) ? "-" : s.Trim(); }

    private static string Clip(string s, int max)
    {
        s = (s ?? "").Trim();
        return s.Length > max ? s.Substring(0, max) + "..." : s;
    }

    private static string Plural(int n, string noun)
    {
        return N(n) + " " + noun + (n == 1 ? "" : "s");
    }

    private static string Cap(string s)
    {
        if (s == null || s.Length == 0) return "";
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    /// <summary>"batch_execute" / "AllMarksController" / "validation_fail" as ordinary words.</summary>
    private static string Words(string s)
    {
        if (s == null || s.Length == 0) return "";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '_' || c == ':' || c == '-') { sb.Append(' '); continue; }
            // AllMarksController -> All Marks Controller
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(s[i - 1]) && s[i - 1] != ' ') sb.Append(' ');
            sb.Append(c);
        }
        string t = sb.ToString().Trim();
        while (t.Contains("  ")) t = t.Replace("  ", " ");
        return t.Length == 0 ? "" : char.ToUpperInvariant(t[0]) + t.Substring(1);
    }

    private static string After(string s, string marker)
    {
        if (s == null) return "";
        int i = s.IndexOf(marker, StringComparison.Ordinal);
        return (i < 0 || i + marker.Length >= s.Length) ? "" : s.Substring(i + marker.Length);
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
}
