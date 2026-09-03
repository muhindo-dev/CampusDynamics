using System;
using System.Collections.Generic;
using System.Configuration;
using MySql.Data.MySqlClient;

/// <summary>
/// Reads the examinations policy switches held in acad_exam_config.
///
/// WHAT THIS IS NOT. It is not another deadline mechanism. acad_deadlines already
/// answers "by when" per campus, year, semester and study system, and
/// MarksDeadlineService already reads it. This answers a different question — "is
/// this permitted at all, here" — which nothing could answer before: the only way
/// to close mark entry was to backdate a deadline, and there was no way to close it
/// for one faculty without closing it for everybody.
///
/// RESOLUTION. Most specific wins, scope before period:
///     PROGRAMME > FACULTY > CAMPUS > GLOBAL
///     exact year+semester > that year, any semester > any period
/// A Registrar sets the rule once, a Dean narrows it where it needs narrowing, and
/// nobody has to remember to put the global one back.
///
/// FAILURE. Every read falls back to the caller's default, and a database that
/// cannot be reached is treated as "no override configured". A policy table that is
/// briefly unavailable must not lock every lecturer out of mark entry, so the
/// defaults passed in by callers are always the permissive, current behaviour.
///
/// This file is duplicated in the portal (CampusDynamics_Portal/App_Code/Exams/)
/// and the two copies must stay byte-identical — the portal is where the lecturer
/// actually types marks, so it has to reach the same answer as the console that
/// sets the switch.
/// </summary>
public static class ExamConfig
{
    // ── keys, named once so a typo is a compile error rather than a silent default ──
    public const string CourseworkEntryEnabled = "coursework.entry.enabled";
    public const string ExamEntryEnabled       = "exam.entry.enabled";
    public const string PracticalEntryEnabled  = "practical.entry.enabled";
    public const string ExcelUploadEnabled     = "marks.excel.upload.enabled";
    public const string EditAfterSubmit        = "marks.edit.after.submit";
    public const string AllowBlankSubmit       = "marks.allow.blank.submit";
    public const string SplitCoursework        = "marks.split.coursework";
    public const string SplitExam              = "marks.split.exam";
    public const string SplitPractical         = "marks.split.practical";
    public const string MarksTotal             = "marks.total";
    public const string MaxScore               = "marks.max.score";
    public const string AllowDecimal           = "marks.allow.decimal";
    public const string ResultsVisible         = "results.students.visible";
    public const string ResultsHideOnBalance   = "results.hide.on.balance";

    public const string CourseworkOpens        = "coursework.entry.opens";
    public const string CourseworkCloses       = "coursework.entry.closes";
    public const string ExamOpens              = "exam.entry.opens";
    public const string ExamCloses             = "exam.entry.closes";

    /// <summary>
    /// The one format a date-and-time setting is ever stored in. Fixed and
    /// culture-invariant on purpose: this server has produced locale parsing faults
    /// before, and a window that silently fails to parse would either lock everybody
    /// out or let everybody in, depending on which way the code happened to fall.
    /// </summary>
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Whether one kind of mark entry is open, and if not, why — in words a lecturer
    /// can act on.
    ///
    /// Three separate controls can each close entry, and they are NOT interchangeable:
    /// the switch is somebody deciding by hand, the window is the published schedule,
    /// and acad_deadlines is the older per-campus closing date that is still honoured.
    /// Most restrictive wins, and the reason names which one it was — telling a
    /// lecturer "the deadline has passed" when in fact the window has not opened sends
    /// them to the registry about the wrong thing.
    /// </summary>
    public class EntryWindow
    {
        public bool IsOpen;
        public bool SwitchedOff;          // closed by hand
        public bool BeforeStart;          // the schedule has not begun
        public bool AfterEnd;             // the schedule has finished
        public DateTime? Opens;           // null = no start limit
        public DateTime? Closes;          // null = no end limit
        public string Reason = "";        // why it is closed, empty when open
        public string Summary = "";       // the one line the portal shows at the top

        public string OpensText { get { return Opens.HasValue ? Opens.Value.ToString("d MMMM yyyy, h:mm tt") : ""; } }
        public string ClosesText { get { return Closes.HasValue ? Closes.Value.ToString("d MMMM yyyy, h:mm tt") : ""; } }

        /// <summary>
        /// A window with no limits, for the case where the policy could not be read at
        /// all. Note that a plain <c>new EntryWindow()</c> is CLOSED, because IsOpen is
        /// false by default — use this whenever the intent is "we could not tell, so do
        /// not stand in the way". Mark entry is still governed by acad_deadlines and by
        /// the global lock; an unreachable configuration table must never become a
        /// stricter rule than no configuration at all.
        /// </summary>
        public static EntryWindow Unrestricted()
        {
            EntryWindow w = new EntryWindow();
            w.IsOpen = true;
            w.Summary = "Open. No dates have been set for this period.";
            return w;
        }
    }

    /// <summary>
    /// Resolves the window for coursework or exam entry. Pass the enabled key and its
    /// two window keys; everything else is derived.
    ///
    /// Anything that cannot be read or parsed is treated as "no limit". A settings
    /// table that is briefly unavailable, or a value somebody typed wrongly, must
    /// never be a stricter rule than no settings at all.
    /// </summary>
    public static EntryWindow GetWindow(string enabledKey, string opensKey, string closesKey, Scope scope)
    {
        var w = new EntryWindow();
        DateTime now = DateTime.Now;

        w.SwitchedOff = !IsEnabled(enabledKey, scope, true);
        w.Opens = GetDateTime(opensKey, scope);
        w.Closes = GetDateTime(closesKey, scope);

        // A window entered backwards is meaningless and would close entry for ever.
        // Treat it as unset rather than acting on it; the console refuses to save one,
        // so this only catches a value edited directly in the database.
        if (w.Opens.HasValue && w.Closes.HasValue && w.Closes.Value <= w.Opens.Value)
        { w.Opens = null; w.Closes = null; }

        w.BeforeStart = w.Opens.HasValue && now < w.Opens.Value;
        w.AfterEnd = w.Closes.HasValue && now > w.Closes.Value;
        w.IsOpen = !w.SwitchedOff && !w.BeforeStart && !w.AfterEnd;

        if (w.SwitchedOff)
            w.Reason = "Mark entry has been closed by the Academic Registrar. This is not a deadline.";
        else if (w.BeforeStart)
            w.Reason = "Mark entry has not opened yet. It opens on " + w.OpensText + ".";
        else if (w.AfterEnd)
            w.Reason = "Mark entry closed on " + w.ClosesText + ".";

        if (w.IsOpen)
        {
            if (w.Closes.HasValue)
            {
                w.Summary = "Open until " + w.ClosesText + " — " + TimeLeft(w.Closes.Value - now) + ".";
            }
            else if (w.Opens.HasValue) w.Summary = "Open since " + w.OpensText + ". No closing date set.";
            else w.Summary = "Open. No dates have been set for this period.";
        }
        else w.Summary = w.Reason;

        return w;
    }

    /// <summary>
    /// How much of a window is left, in words. Carries the second unit while the
    /// deadline is close, because truncating to one unit misreports by up to a whole
    /// unit — 4 days 23 hours would read as "4 days left" and a lecturer planning
    /// around it would be a day out. Never rounds up: the figure shown is never more
    /// time than actually remains.
    /// </summary>
    private static string TimeLeft(TimeSpan left)
    {
        if (left.TotalSeconds <= 0) return "closing now";

        // Deadlines are stored to the minute, so the seconds in this span are an
        // artefact of when the page happened to be rendered. Without this, a window
        // closing in exactly one day reads "23 hours 59 minutes left" a heartbeat
        // after it is set. Rounding up to the stored precision costs under a minute
        // of accuracy and keeps every figure on the unit the Registrar typed.
        left = TimeSpan.FromMinutes(Math.Ceiling(left.TotalMinutes));

        if (left.TotalHours < 1) return Unit(left.Minutes, "minute") + " left";

        if (left.TotalDays < 1)
            return left.Minutes == 0
                ? Unit(left.Hours, "hour") + " left"
                : Unit(left.Hours, "hour") + " " + Unit(left.Minutes, "minute") + " left";

        int days = (int)left.TotalDays;
        if (days >= 7 || left.Hours == 0) return Unit(days, "day") + " left";
        return Unit(days, "day") + " " + Unit(left.Hours, "hour") + " left";
    }

    private static string Unit(int n, string noun)
    {
        return n + " " + noun + (n == 1 ? "" : "s");
    }

    /// <summary>The coursework entry window for this scope.</summary>
    public static EntryWindow CourseworkWindow(Scope scope)
    { return GetWindow(CourseworkEntryEnabled, CourseworkOpens, CourseworkCloses, scope); }

    /// <summary>The final exam entry window for this scope.</summary>
    public static EntryWindow ExamWindow(Scope scope)
    { return GetWindow(ExamEntryEnabled, ExamOpens, ExamCloses, scope); }

    /// <summary>
    /// A stored date and time, or null when unset or unreadable. Parsed with the
    /// invariant culture against the single stored format, then a tolerant fallback
    /// for a value that was hand-edited — but never with the server's own culture,
    /// which is what turns 03/09 into March somewhere and September somewhere else.
    /// </summary>
    public static DateTime? GetDateTime(string key, Scope scope)
    {
        string v = Resolve(key, scope);
        if (v == null) return null;
        v = v.Trim();
        if (v == "") return null;

        DateTime d;
        if (DateTime.TryParseExact(v, DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d)) return d;
        if (DateTime.TryParseExact(v, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d)) return d;
        if (DateTime.TryParseExact(v, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d)) return d;
        if (DateTime.TryParse(v, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d)) return d;
        return null;                      // unreadable counts as "no limit"
    }

    /// <summary>
    /// Where a setting is being asked about. Any field may be left empty; an empty
    /// field simply cannot match an override at that level.
    /// </summary>
    public class Scope
    {
        public string Campus = "";
        public string Faculty = "";
        public string Programme = "";
        public string AcadYear = "";
        public int Semester = 0;

        /// <summary>
        /// Study session (DAY / WEEKEND / INSERVICE / EVENING), taken from the teaching
        /// allocation or the student's course registration.
        ///
        /// This exists because in-service is a MODE, not a programme: every programme that
        /// has in-service students also has day or weekend students, so a PROGRAMME-scoped
        /// override cannot open mark entry for the in-service cohort without also opening it
        /// for the day cohort of the same programme.
        /// </summary>
        public string Session = "";

        public static Scope Global() { return new Scope(); }
        public Scope ForProgramme(string p) { Programme = (p ?? "").Trim(); return this; }
        public Scope ForSession(string s) { Session = (s ?? "").Trim(); return this; }
        public Scope ForFaculty(string f) { Faculty = (f ?? "").Trim(); return this; }
        public Scope ForCampus(string c) { Campus = (c ?? "").Trim(); return this; }
        public Scope ForPeriod(string year, int sem) { AcadYear = (year ?? "").Trim(); Semester = sem; return this; }
    }

    /// <summary>
    /// Builds a full scope from what a lecturer's screen actually has to hand.
    ///
    /// The faculty is looked up from the programme rather than asked for, because no
    /// portal screen carries it in session — and without it a Dean's faculty-wide
    /// override would silently fail to reach the very people it was meant for. The
    /// lookup is cached for the life of the process: programmes do not move faculty
    /// during a request, and this runs on every mark-entry page load.
    /// </summary>
    public static Scope ScopeForProgramme(string programme, string campus, string acadYear, int semester)
    {
        return ScopeForProgramme(programme, campus, acadYear, semester, null);
    }

    /// <summary>
    /// As above, and also carries the study session so that a SESSION-scoped rule
    /// (e.g. "in-service may enter marks this year") can reach the right cohort.
    /// Callers that genuinely have no session should use the four-argument overload;
    /// an empty session simply cannot match a SESSION override.
    /// </summary>
    public static Scope ScopeForProgramme(string programme, string campus, string acadYear, int semester, string session)
    {
        var sc = new Scope();
        sc.Programme = (programme ?? "").Trim();
        sc.Campus = (campus ?? "").Trim();
        sc.AcadYear = (acadYear ?? "").Trim();
        sc.Semester = semester;
        sc.Session = NormaliseSession(session);
        sc.Faculty = FacultyOf(sc.Programme);
        return sc;
    }

    /// <summary>
    /// Study session as stored varies in case and spacing across screens
    /// ("InService", "IN SERVICE", "inservice"). Fold it to the canonical form used in
    /// acad_studysessions so a SESSION rule matches regardless of which screen asked.
    /// </summary>
    public static string NormaliseSession(string session)
    {
        string v = (session ?? "").Trim();
        if (v == "" || v == "-") return "";
        string flat = v.Replace(" ", "").Replace("-", "").Replace("_", "").ToUpperInvariant();
        switch (flat)
        {
            case "INSERVICE": case "INSRV": case "INSERV": return "INSERVICE";
            case "DAY":       case "FULLTIME":             return "DAY";
            case "WEEKEND":   case "WKD":                  return "WEEKEND";
            case "EVENING":   case "EVE":                  return "EVENING";
            default: return v.ToUpperInvariant();
        }
    }

    private static readonly Dictionary<string, string> FacultyCache =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string FacultyOf(string programme)
    {
        if (string.IsNullOrEmpty(programme)) return "";
        lock (FacultyCache)
        {
            string hit;
            if (FacultyCache.TryGetValue(programme, out hit)) return hit;
        }
        string code = "";
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT IFNULL(faculty_code,'') FROM acad_programme WHERE progcode = @p LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@p", programme);
                    object v = cmd.ExecuteScalar();
                    code = (v == null || v == DBNull.Value) ? "" : v.ToString().Trim();
                }
            }
        }
        catch { code = ""; }
        lock (FacultyCache) { FacultyCache[programme] = code; }
        return code;
    }

    private static string ConnStr
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["ExamConfig.ConnStr"];
            if (cs != null && !string.IsNullOrEmpty(cs.ConnectionString)) return cs.ConnectionString;

            // The portal's own vacConnectionString points at campus_dynamics_portal,
            // which is NOT where this table lives. Its second connection is.
            cs = ConfigurationManager.ConnectionStrings["campus_dynamics_portalConnectionString"];
            if (cs != null && cs.ConnectionString.IndexOf("database=campus_dynamics;", StringComparison.OrdinalIgnoreCase) >= 0)
                return cs.ConnectionString;

            cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
            if (cs != null && cs.ConnectionString.IndexOf("database=campus_dynamics;", StringComparison.OrdinalIgnoreCase) >= 0)
                return cs.ConnectionString;

            if (cs != null) return cs.ConnectionString;
            return "server=localhost;User Id=root;password=24thdecember1977;database=campus_dynamics;charset=utf8";
        }
    }

    // ── reads ──────────────────────────────────────────────────────────────────

    public static bool IsEnabled(string key, Scope scope, bool fallback)
    {
        string v = Resolve(key, scope);
        if (v == null) return fallback;
        v = v.Trim().ToUpperInvariant();
        return v == "1" || v == "TRUE" || v == "YES" || v == "ON";
    }

    public static int GetInt(string key, Scope scope, int fallback)
    {
        string v = Resolve(key, scope);
        int n;
        return (v != null && int.TryParse(v.Trim(), out n)) ? n : fallback;
    }

    public static decimal GetDecimal(string key, Scope scope, decimal fallback)
    {
        string v = Resolve(key, scope);
        decimal d;
        return (v != null && decimal.TryParse(v.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out d)) ? d : fallback;
    }

    public static string GetText(string key, Scope scope, string fallback)
    {
        string v = Resolve(key, scope);
        return v == null ? fallback : v;
    }

    /// <summary>
    /// The winning value for this key in this scope, or null when nothing is
    /// configured. Public so a screen can show an operator WHICH rule is in force.
    /// </summary>
    public static string Resolve(string key, Scope scope)
    {
        string src;
        return Resolve(key, scope, out src);
    }

    /// <summary>As Resolve, and also reports which row won, e.g. "PROGRAMME:BIT (2025/2026 S1)".</summary>
    public static string Resolve(string key, Scope scope, out string source)
    {
        source = "";
        key = (key ?? "").Trim();
        if (key == "") return null;
        if (scope == null) scope = Scope.Global();

        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();

                // Every row that COULD apply, ordered so the first one is the winner.
                // Ordering in SQL rather than in C# keeps the precedence rule in one
                // place, where it can be read alongside the query that uses it.
                using (var cmd = new MySqlCommand(
                    "SELECT scope_type, scope_value, acad_year, semester, config_value " +
                    "FROM acad_exam_config " +
                    "WHERE config_key = @k AND is_active = 1 " +
                    "  AND ( scope_type = 'GLOBAL' " +
                    "     OR (scope_type = 'CAMPUS'    AND scope_value = @campus    AND @campus    <> '') " +
                    "     OR (scope_type = 'FACULTY'   AND scope_value = @faculty   AND @faculty   <> '') " +
                    "     OR (scope_type = 'SESSION'   AND scope_value = @session   AND @session   <> '') " +
                    "     OR (scope_type = 'PROGRAMME' AND scope_value = @programme AND @programme <> '') ) " +
                    "  AND (acad_year = '' OR acad_year = @year) " +
                    "  AND (semester  = 0  OR semester  = @sem) " +
                    // SESSION sits directly above GLOBAL: it targets a delivery mode across the
                    // whole university, so it is broader than any campus/faculty/programme
                    // decision but narrower than "everybody".
                    "ORDER BY FIELD(scope_type,'PROGRAMME','FACULTY','CAMPUS','SESSION','GLOBAL'), " +
                    "         (acad_year <> '') DESC, (semester <> 0) DESC " +
                    "LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@k", key);
                    cmd.Parameters.AddWithValue("@campus", scope.Campus ?? "");
                    cmd.Parameters.AddWithValue("@faculty", scope.Faculty ?? "");
                    cmd.Parameters.AddWithValue("@programme", scope.Programme ?? "");
                    cmd.Parameters.AddWithValue("@session", scope.Session ?? "");
                    cmd.Parameters.AddWithValue("@year", scope.AcadYear ?? "");
                    cmd.Parameters.AddWithValue("@sem", scope.Semester);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return null;
                        string st = r.GetString(0);
                        string sv = r.GetString(1);
                        string yr = r.GetString(2);
                        int sm = r.GetInt32(3);
                        source = st + (sv == "" ? "" : ":" + sv)
                               + (yr == "" && sm == 0 ? "" : " (" + (yr == "" ? "any year" : yr) + (sm == 0 ? "" : " S" + sm) + ")");
                        return r.IsDBNull(4) ? "" : r.GetString(4);
                    }
                }
            }
        }
        catch
        {
            // Unreadable policy must never be stricter than no policy.
            return null;
        }
    }

    /// <summary>
    /// Every setting that applies in this scope, for a screen that wants to show the
    /// effective policy in one go rather than asking key by key.
    /// </summary>
    public static Dictionary<string, string> ResolveAll(Scope scope)
    {
        var outp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var keys = new List<string>();
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                using (var cmd = new MySqlCommand("SELECT DISTINCT config_key FROM acad_exam_config WHERE is_active = 1", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) keys.Add(r.GetString(0));
            }
            foreach (string k in keys)
            {
                string v = Resolve(k, scope);
                if (v != null) outp[k] = v;
            }
        }
        catch { }
        return outp;
    }
}
