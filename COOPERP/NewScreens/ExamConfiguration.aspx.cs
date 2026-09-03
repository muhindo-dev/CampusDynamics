using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Examinations configuration — the switches that govern what lecturers may do.
///
/// Deadlines are NOT here. acad_deadlines already answers "by when", per campus,
/// year, semester and study system, and MarksDeadlineService already reads it.
/// This screen answers the question nothing could answer before: "is this permitted
/// at all, here". Previously the only way to stop coursework entry was to backdate a
/// deadline for the whole university.
///
/// Every switch can be set globally and then overridden for a campus, a faculty or a
/// single programme, optionally for one academic year and semester. The most specific
/// rule wins. The screen shows which rule is actually in force for a chosen scope, so
/// an administrator can answer "why can this lecturer not enter marks" without
/// reading the table.
/// </summary>
public partial class COOPERP_NewScreens_ExamConfiguration : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    private string Actor()
    {
        try
        {
            if (Session != null && Session["username"] != null && Session["username"].ToString().Trim() != "")
                return Session["username"].ToString().Trim();
        }
        catch { }
        try { if (User != null && User.Identity != null && User.Identity.IsAuthenticated) return User.Identity.Name; }
        catch { }
        return "admin";
    }

    /// <summary>
    /// True when the request carries a signed-in staff session. Same gate as the other
    /// consoles: these switches can stop every lecturer in the university entering
    /// marks, so the endpoints are not left open.
    /// </summary>
    private bool Authed()
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

    protected void Page_Load(object sender, EventArgs e)
    {
        string action = Request.QueryString["action"] ?? Request.Form["action"];
        if (!string.IsNullOrEmpty(action))
        {
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Cache.SetCacheability(HttpCacheability.NoCache);

            // 200 with success:false, never 401 — FormsAuthenticationModule turns a 401
            // into a redirect to the login page and the caller gets HTML where it
            // expected JSON.
            if (!Authed())
            {
                Response.Write("{\"success\":false,\"message\":\"Your session has expired. Please sign in again, then retry.\"}");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }

            try
            {
                if (action == "List") Response.Write(HandleList());
                else if (action == "Scoped") Response.Write(HandleScoped());
                else if (action == "Save") Response.Write(HandleSave());
                else if (action == "SaveMany") Response.Write(HandleSaveMany());
                else if (action == "Delete") Response.Write(HandleDelete());
                else if (action == "RemoveScoped") Response.Write(HandleRemoveScoped());
                else if (action == "Effective") Response.Write(HandleEffective());
                else Response.Write("{\"success\":false,\"message\":\"Unknown action.\"}");
            }
            catch (Exception ex)
            {
                Response.Write(new JavaScriptSerializer().Serialize(new { success = false, message = ex.Message }));
            }
            try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
            return;
        }
    }

    // ── the catalogue ───────────────────────────────────────────────────────────
    //
    // Held here rather than in the database so a setting cannot exist without a
    // human explanation of what it does. The database stores VALUES; this describes
    // what the values mean.
    private class Setting
    {
        public string Key, Group, Title, Help, Type;

        /// <summary>
        /// The window this setting is one end of, or null. The two ends are shown as a
        /// single From/To row rather than two unrelated fields, because a start date on
        /// its own is not a decision anybody makes.
        /// </summary>
        public string Window, WindowRole;

        public Setting(string k, string g, string t, string h, string ty)
        { Key = k; Group = g; Title = t; Help = h; Type = ty; }

        public Setting InWindow(string w, string role) { Window = w; WindowRole = role; return this; }
    }

    /// <summary>
    /// The From/To pairs, declared here so the browser never has to infer a pairing from
    /// the shape of a key. Order matches the catalogue.
    /// </summary>
    private class WindowDef
    {
        public string Id, Title, Help, Opens, Closes;
        public WindowDef(string id, string t, string h, string o, string c)
        { Id = id; Title = t; Help = h; Opens = o; Closes = c; }
    }

    private static readonly List<WindowDef> Windows = new List<WindowDef>
    {
        new WindowDef("coursework", "Coursework entry window",
            "When lecturers may type coursework marks. Leave either side empty for no limit on that side.",
            ExamConfig.CourseworkOpens, ExamConfig.CourseworkCloses),
        new WindowDef("exam", "Final exam entry window",
            "When lecturers may type final exam marks. Leave either side empty for no limit on that side.",
            ExamConfig.ExamOpens, ExamConfig.ExamCloses)
    };

    private static readonly List<Setting> Catalogue = new List<Setting>
    {
        // The schedule comes first because it is what an administrator normally wants,
        // and because leaving it blank is a valid, common answer.
        new Setting(ExamConfig.CourseworkOpens, "When mark entry runs",
            "Coursework entry opens",
            "Lecturers cannot type coursework marks before this. Leave empty for no start limit.", "DATETIME")
            .InWindow("coursework", "opens"),
        new Setting(ExamConfig.CourseworkCloses, "When mark entry runs",
            "Coursework entry closes",
            "Lecturers cannot type coursework marks after this. Leave empty for no end limit.", "DATETIME")
            .InWindow("coursework", "closes"),
        new Setting(ExamConfig.ExamOpens, "When mark entry runs",
            "Final exam entry opens",
            "Leave empty for no start limit.", "DATETIME")
            .InWindow("exam", "opens"),
        new Setting(ExamConfig.ExamCloses, "When mark entry runs",
            "Final exam entry closes",
            "Leave empty for no end limit.", "DATETIME")
            .InWindow("exam", "closes"),

        new Setting(ExamConfig.CourseworkEntryEnabled, "What lecturers may do",
            "Coursework mark entry",
            "When off, lecturers cannot open or type coursework marks. This is separate from the deadline — use it to close entry without pretending a date has passed.", "BOOL"),
        new Setting(ExamConfig.ExamEntryEnabled, "What lecturers may do",
            "Final exam mark entry",
            "When off, lecturers cannot open or type final exam marks.", "BOOL"),
        new Setting(ExamConfig.PracticalEntryEnabled, "What lecturers may do",
            "Practical mark entry",
            "For programmes that assess a practical component separately.", "BOOL"),
        new Setting(ExamConfig.ExcelUploadEnabled, "What lecturers may do",
            "Upload marks from Excel",
            "When off, marks must be typed in. Useful when a spreadsheet template has changed and old copies are still circulating.", "BOOL"),
        new Setting(ExamConfig.EditAfterSubmit, "What lecturers may do",
            "Edit a marksheet after submitting it",
            "Normally off: once a sheet is submitted it belongs to the approval chain. Turn on briefly when a department is correcting a batch.", "BOOL"),
        new Setting(ExamConfig.AllowBlankSubmit, "What lecturers may do",
            "Allow submission with marks missing",
            "Normally off, so an absent student is recorded deliberately rather than by leaving a box empty.", "BOOL"),

        new Setting(ExamConfig.SplitCoursework, "How a mark is made up",
            "Coursework percentage", "Default share of the final mark for new marksheets.", "INT"),
        new Setting(ExamConfig.SplitExam, "How a mark is made up",
            "Final exam percentage", "Default share of the final mark for new marksheets.", "INT"),
        new Setting(ExamConfig.SplitPractical, "How a mark is made up",
            "Practical percentage", "Left at zero for programmes with no practical component.", "INT"),
        new Setting(ExamConfig.MarksTotal, "How a mark is made up",
            "Total the shares must add to", "The three percentages above are checked against this.", "INT"),
        new Setting(ExamConfig.MaxScore, "How a mark is made up",
            "Highest mark allowed for one component", "A guard against a slipped decimal point.", "INT"),
        new Setting(ExamConfig.AllowDecimal, "How a mark is made up",
            "Allow decimal marks", "When off, only whole numbers may be entered.", "BOOL"),

        new Setting(ExamConfig.ResultsVisible, "What students may see",
            "Students can view published results",
            "Turn off to hold results back from the portal while a board of examiners is sitting.", "BOOL"),
        new Setting(ExamConfig.ResultsHideOnBalance, "What students may see",
            "Hide results from students who owe fees",
            "A policy decision, not a technical one. Off by default.", "BOOL"),
    };

    private string HandleList()
    {
        var js = new JavaScriptSerializer();
        var rows = new List<object>();

        var overrides = new Dictionary<string, List<object>>();
        using (var c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (var cmd = new MySqlCommand(
                "SELECT id, config_key, scope_type, scope_value, acad_year, semester, config_value, " +
                "       IFNULL(notes,''), IFNULL(updated_by,''), IFNULL(DATE_FORMAT(updated_at,'%e %b %Y'),'') " +
                "FROM acad_exam_config WHERE is_active = 1 " +
                "ORDER BY config_key, FIELD(scope_type,'GLOBAL','SESSION','CAMPUS','FACULTY','PROGRAMME'), scope_value", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string key = r.GetString(1);
                    if (!overrides.ContainsKey(key)) overrides[key] = new List<object>();
                    overrides[key].Add(new
                    {
                        id = r.GetInt32(0),
                        scopeType = r.GetString(2),
                        scopeValue = r.GetString(3),
                        acadYear = r.GetString(4),
                        semester = r.GetInt32(5),
                        value = r.GetString(6),
                        notes = r.GetString(7),
                        updatedBy = r.GetString(8),
                        updatedAt = r.GetString(9)
                    });
                }
        }

        foreach (Setting s in Catalogue)
            rows.Add(new
            {
                key = s.Key,
                group = s.Group,
                title = s.Title,
                help = s.Help,
                type = s.Type,
                window = s.Window,
                windowRole = s.WindowRole,
                rules = overrides.ContainsKey(s.Key) ? overrides[s.Key] : new List<object>()
            });

        var wins = new List<object>();
        foreach (WindowDef w in Windows)
            wins.Add(new { id = w.Id, title = w.Title, help = w.Help, opens = w.Opens, closes = w.Closes });

        return js.Serialize(new { success = true, settings = rows, windows = wins, scopes = LoadScopes() });
    }

    /// <summary>
    /// Everything the scope pickers need: the campuses, faculties and programmes a rule
    /// can be attached to, and the academic years. Years come from acad_acadyears rather
    /// than being typed, because a mistyped year silently creates a rule that matches
    /// nothing and looks like the setting simply did not work.
    /// </summary>
    private object LoadScopes()
    {
        var campuses = new List<object>();
        var faculties = new List<object>();
        var programmes = new List<object>();
        var sessions = new List<object>();
        var years = new List<object>();
        string currentYear = "";
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                using (var cmd = new MySqlCommand("SELECT ID, campus_name FROM acad_campuses WHERE ID > 0 ORDER BY ID", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) campuses.Add(new { v = r.GetValue(0).ToString(), t = r.GetValue(1).ToString() });

                using (var cmd = new MySqlCommand(
                    "SELECT faculty_code, faculty_name FROM acad_faculty WHERE faculty_code <> '00' ORDER BY faculty_name", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) faculties.Add(new { v = r.GetString(0), t = r.GetString(1) });

                using (var cmd = new MySqlCommand("SELECT progcode, progname FROM acad_programme ORDER BY progname", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) programmes.Add(new { v = r.GetString(0), t = r.GetString(1) });

                // Study sessions (DAY / WEEKEND / INSERVICE / EVENING). In-service is a
                // delivery mode rather than a programme — every programme that runs it also
                // runs day or weekend — so it can only be targeted as a session.
                using (var cmd = new MySqlCommand(
                    "SELECT UPPER(TRIM(Session)) FROM acad_studysessions ORDER BY Session", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string sv = r.GetString(0);
                        if (!string.IsNullOrEmpty(sv)) sessions.Add(new { v = sv, t = sv });
                    }

                // semester_count travels with the year so the semester picker can offer
                // exactly the semesters that year actually runs.
                using (var cmd = new MySqlCommand(
                    "SELECT acadyear, is_current_year, semester_count FROM acad_acadyears " +
                    "WHERE status = 'Active' ORDER BY acadyear DESC", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string y = r.GetString(0).Trim();
                        bool cur = string.Equals(r.GetString(1), "Yes", StringComparison.OrdinalIgnoreCase);
                        int sc = r.IsDBNull(2) ? 2 : r.GetInt32(2);
                        if (sc < 1) sc = 1; if (sc > 3) sc = 3;
                        if (cur) currentYear = y;
                        years.Add(new { v = y, t = y + (cur ? "  (current)" : ""), semesters = sc, current = cur });
                    }
            }
        }
        catch { }
        return new
        {
            campuses = campuses,
            faculties = faculties,
            programmes = programmes,
            sessions = sessions,
            years = years,
            currentYear = currentYear
        };
    }

    private string HandleSave()
    {
        var js = new JavaScriptSerializer();
        Func<string, string> F = k => (Request.Form[k] ?? "").Trim();

        string key = F("key");
        string scopeType = F("scopeType").ToUpperInvariant();
        string scopeValue = F("scopeValue");
        string acadYear = F("acadYear");
        string value = F("value");
        string notes = F("notes");
        int semester; if (!int.TryParse(F("semester"), out semester)) semester = 0;

        Setting def = Catalogue.Find(x => x.Key == key);
        if (def == null) return js.Serialize(new { success = false, message = "Unknown setting." });
        if (scopeType != "GLOBAL" && scopeType != "CAMPUS" && scopeType != "FACULTY" && scopeType != "PROGRAMME" && scopeType != "SESSION")
            return js.Serialize(new { success = false, message = "Choose a valid scope." });
        if (scopeType == "GLOBAL") scopeValue = "";
        else if (scopeValue == "")
            return js.Serialize(new { success = false, message = "Choose which " + scopeType.ToLowerInvariant() + " this applies to." });

        string bad;
        value = NormaliseValue(def, value, out bad);
        if (bad != null) return js.Serialize(new { success = false, message = bad });

        if (def.Type == "DATETIME" && value != "")
        {
            string other = OppositeWindowKey(key);
            if (other != null)
            {
                // A window that closes before it opens can never be open, and would read
                // on screen as though it were configured correctly. Refused here rather
                // than tolerated later.
                DateTime dt = DateTime.ParseExact(value, ExamConfig.DateTimeFormat,
                    System.Globalization.CultureInfo.InvariantCulture);
                DateTime? o = ReadRaw(other, scopeType, scopeValue, acadYear, semester);
                bool thisIsOpening = key.EndsWith(".opens", StringComparison.OrdinalIgnoreCase);
                DateTime opens = thisIsOpening ? dt : (o ?? DateTime.MinValue);
                DateTime closes = thisIsOpening ? (o ?? DateTime.MaxValue) : dt;
                if (o.HasValue && closes <= opens)
                    return js.Serialize(new { success = false, message =
                        "That would close entry before it opens (" +
                        opens.ToString("d MMM yyyy, h:mm tt") + " to " + closes.ToString("d MMM yyyy, h:mm tt") +
                        "). Check the other date first." });
            }
        }

        using (var c = new MySqlConnection(ConnStr))
        {
            c.Open();
            string before = null;
            using (var cmd = new MySqlCommand(
                "SELECT config_value FROM acad_exam_config WHERE config_key=@k AND scope_type=@st AND scope_value=@sv " +
                "AND acad_year=@y AND semester=@s LIMIT 1", c))
            {
                cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@st", scopeType);
                cmd.Parameters.AddWithValue("@sv", scopeValue); cmd.Parameters.AddWithValue("@y", acadYear);
                cmd.Parameters.AddWithValue("@s", semester);
                object o = cmd.ExecuteScalar();
                if (o != null && o != DBNull.Value) before = o.ToString();
            }

            using (var cmd = new MySqlCommand(
                "INSERT INTO acad_exam_config (config_key,scope_type,scope_value,acad_year,semester,value_type,config_value,notes,updated_by,updated_at,is_active) " +
                "VALUES (@k,@st,@sv,@y,@s,@vt,@v,@n,@by,NOW(),1) " +
                "ON DUPLICATE KEY UPDATE config_value=VALUES(config_value), notes=VALUES(notes), " +
                "  value_type=VALUES(value_type), updated_by=VALUES(updated_by), updated_at=NOW(), is_active=1", c))
            {
                cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@st", scopeType);
                cmd.Parameters.AddWithValue("@sv", scopeValue); cmd.Parameters.AddWithValue("@y", acadYear);
                cmd.Parameters.AddWithValue("@s", semester); cmd.Parameters.AddWithValue("@vt", def.Type);
                cmd.Parameters.AddWithValue("@v", value); cmd.Parameters.AddWithValue("@n", notes);
                cmd.Parameters.AddWithValue("@by", Actor());
                cmd.ExecuteNonQuery();
            }

            Log(c, key, scopeType, scopeValue, acadYear, semester, before, value, before == null ? "CREATE" : "SET");
        }

        return js.Serialize(new { success = true, message = "Saved." });
    }

    /// <summary>
    /// Checks a value against its declared type and returns it in the one form the
    /// database stores. Shared by the single save and the batch save so the two can
    /// never drift apart — a rule accepted by one route and refused by the other would
    /// be worse than either rule on its own.
    ///
    /// Sets <paramref name="error"/> to a sentence for the operator, or null when the
    /// value is good. An empty DATETIME is legitimate and means "no limit".
    /// </summary>
    private static string NormaliseValue(Setting def, string value, out string error)
    {
        error = null;
        value = (value ?? "").Trim();

        if (def.Type == "BOOL")
        {
            string v = value.ToUpperInvariant();
            if (v == "TRUE" || v == "YES" || v == "ON" || v == "1") return "1";
            if (v == "FALSE" || v == "NO" || v == "OFF" || v == "0") return "0";
            error = "\"" + def.Title + "\" is either on or off.";
            return value;
        }

        if (def.Type == "DATETIME")
        {
            if (value == "") return "";
            DateTime dt;
            if (!DateTime.TryParseExact(value, ExamConfig.DateTimeFormat,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out dt)
             && !DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm",   // what datetime-local sends
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out dt))
            {
                error = "\"" + def.Title + "\" needs a date and time in the form yyyy-MM-dd HH:mm.";
                return value;
            }
            // Stored in one format only, so nothing downstream has to guess.
            return dt.ToString(ExamConfig.DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (def.Type == "INT")
        {
            int n;
            if (!int.TryParse(value, out n)) { error = "\"" + def.Title + "\" needs a whole number."; return value; }
            if (n < 0 || n > 1000) { error = "\"" + def.Title + "\" is outside the sensible range (0 to 1000)."; return value; }
            return n.ToString();
        }

        return value;
    }

    /// <summary>
    /// The "all campuses / all faculties / all programmes" choice in the scope picker.
    /// Not a stored scope_value — it is expanded into one real row per target before
    /// anything is written, so the table never contains a magic word.
    /// </summary>
    public const string AllToken = "__ALL__";

    /// <summary>
    /// Plural of a scope type for a sentence shown to an operator. Appending "s" gives
    /// "facultys" and "campuss", which reads as a fault in the screen.
    /// </summary>
    private static string Plural(string scopeType)
    {
        switch ((scopeType ?? "").ToUpperInvariant())
        {
            case "CAMPUS": return "campuses";
            case "FACULTY": return "faculties";
            case "PROGRAMME": return "programmes";
            default: return "scopes";
        }
    }

    /// <summary>
    /// The scope values a save or read touches. One entry for a single target, one per
    /// campus/faculty/programme for the "all" choice, and a single empty string for the
    /// university-wide scope, which has no target of its own.
    ///
    /// Applying to every faculty is NOT the same as applying university-wide: faculty
    /// rules beat campus rules, so "all faculties" is the only way to override a
    /// campus-level rule everywhere at once.
    /// </summary>
    private List<string> TargetsFor(string scopeType, string scopeValue)
    {
        var outv = new List<string>();
        if (scopeType == "GLOBAL") { outv.Add(""); return outv; }
        if (scopeValue != AllToken) { outv.Add(scopeValue); return outv; }

        string sql =
            scopeType == "CAMPUS" ? "SELECT ID FROM acad_campuses WHERE ID > 0 ORDER BY ID" :
            scopeType == "FACULTY" ? "SELECT faculty_code FROM acad_faculty WHERE faculty_code <> '00' ORDER BY faculty_name" :
            scopeType == "SESSION" ? "SELECT UPPER(TRIM(Session)) FROM acad_studysessions ORDER BY Session" :
                                     "SELECT progcode FROM acad_programme ORDER BY progname";
        using (var c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (var cmd = new MySqlCommand(sql, c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string v = Convert.ToString(r.GetValue(0));
                    if (!string.IsNullOrEmpty(v)) outv.Add(v);
                }
        }
        return outv;
    }

    /// <summary>
    /// Turns the four scope fields into an ExamConfig.Scope for resolution. A PROGRAMME
    /// scope goes through ScopeForProgramme so its faculty is filled in and a
    /// faculty-level rule is seen; the others are built directly, because a campus rule
    /// has no faculty to derive.
    /// </summary>
    private static ExamConfig.Scope ScopeFrom(string scopeType, string scopeValue, string acadYear, int semester)
    {
        switch ((scopeType ?? "").ToUpperInvariant())
        {
            case "PROGRAMME": return ExamConfig.ScopeForProgramme(scopeValue, "", acadYear, semester);
            case "SESSION": return new ExamConfig.Scope().ForSession(scopeValue).ForPeriod(acadYear, semester);
            case "FACULTY": return new ExamConfig.Scope().ForFaculty(scopeValue).ForPeriod(acadYear, semester);
            case "CAMPUS": return new ExamConfig.Scope().ForCampus(scopeValue).ForPeriod(acadYear, semester);
            default: return new ExamConfig.Scope().ForPeriod(acadYear, semester);
        }
    }

    /// <summary>
    /// action=Scoped — what the form shows. For one exact scope, each setting reports
    /// the value stored AT that scope (null when nothing is stored there), and the value
    /// that would apply anyway through the fallback chain, with its source.
    ///
    /// The distinction is the whole point of the screen: a field showing "60" because a
    /// Dean set it here is a different fact from a field showing "60" inherited from the
    /// university, and an operator who cannot tell them apart will edit the wrong one.
    /// </summary>
    private string HandleScoped()
    {
        var js = new JavaScriptSerializer();
        Func<string, string> F = k => (Request.Form[k] ?? Request.QueryString[k] ?? "").Trim();

        string scopeType = F("scopeType").ToUpperInvariant();
        if (scopeType == "") scopeType = "GLOBAL";
        string scopeValue = scopeType == "GLOBAL" ? "" : F("scopeValue");
        string acadYear = F("acadYear");
        int semester; if (!int.TryParse(F("semester"), out semester)) semester = 0;

        if (scopeType != "GLOBAL" && scopeType != "CAMPUS" && scopeType != "FACULTY" && scopeType != "PROGRAMME" && scopeType != "SESSION")
            return js.Serialize(new { success = false, message = "Choose a valid scope." });
        if (scopeType != "GLOBAL" && scopeValue == "")
            return js.Serialize(new { success = false, message = "Choose which " + scopeType.ToLowerInvariant() + " these settings apply to." });

        List<string> targets = TargetsFor(scopeType, scopeValue);
        if (targets.Count == 0)
            return js.Serialize(new { success = false, message = "There are no " + Plural(scopeType) + " to apply these settings to." });
        bool isAll = (scopeValue == AllToken);

        // Everything stored at this scope for every target, in one query.
        // key -> (target -> value)
        var own = new Dictionary<string, Dictionary<string, string>>();
        using (var c = new MySqlConnection(ConnStr))
        {
            c.Open();
            var ps = new List<string>();
            using (var cmd = new MySqlCommand())
            {
                cmd.Connection = c;
                for (int i = 0; i < targets.Count; i++)
                {
                    ps.Add("@t" + i);
                    cmd.Parameters.AddWithValue("@t" + i, targets[i]);
                }
                cmd.CommandText =
                    "SELECT config_key, scope_value, config_value FROM acad_exam_config " +
                    "WHERE scope_type=@st AND acad_year=@y AND semester=@s AND is_active=1 " +
                    "AND scope_value IN (" + string.Join(",", ps.ToArray()) + ")";
                cmd.Parameters.AddWithValue("@st", scopeType);
                cmd.Parameters.AddWithValue("@y", acadYear);
                cmd.Parameters.AddWithValue("@s", semester);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = r.GetString(0);
                        if (!own.ContainsKey(k)) own[k] = new Dictionary<string, string>();
                        own[k][r.GetString(1)] = r.GetString(2);
                    }
            }
        }

        // For a single target, resolution runs at that target. For "all", it runs one
        // level out — the honest answer to "what applies where none of them has a rule".
        ExamConfig.Scope sc = ScopeFrom(scopeType, isAll ? "" : scopeValue, acadYear, semester);

        var rows = new List<object>();
        foreach (Setting s in Catalogue)
        {
            string src;
            string eff = ExamConfig.Resolve(s.Key, sc, out src) ?? "";

            Dictionary<string, string> here = own.ContainsKey(s.Key) ? own[s.Key] : new Dictionary<string, string>();
            int have = here.Count;

            // Mixed means the targets disagree, or only some of them have a rule. It is
            // shown rather than hidden: picking one of the values to display would tell
            // the operator something untrue about the others.
            bool allSame = true;
            string common = null;
            foreach (var v in here.Values)
            {
                if (common == null) common = v;
                else if (common != v) { allSame = false; break; }
            }
            bool isOwn = have > 0 && have == targets.Count && allSame;
            bool mixed = have > 0 && !isOwn;

            rows.Add(new
            {
                key = s.Key,
                group = s.Group,
                title = s.Title,
                help = s.Help,
                type = s.Type,
                window = s.Window,
                windowRole = s.WindowRole,
                isOwn = isOwn,
                mixed = mixed,
                setOn = have,                        // how many targets carry a rule
                targets = targets.Count,
                value = isOwn ? common : eff,        // the control shows what applies
                effective = eff,
                source = src ?? ""
            });
        }

        var wins = new List<object>();
        foreach (WindowDef w in Windows)
            wins.Add(new { id = w.Id, title = w.Title, help = w.Help, opens = w.Opens, closes = w.Closes });

        // The base row is the floor everything else falls back to, and it is the one
        // scope where "inherited" is not a possibility.
        bool isBase = scopeType == "GLOBAL" && acadYear == "" && semester == 0;
        return js.Serialize(new
        {
            success = true,
            settings = rows,
            windows = wins,
            faculty = sc.Faculty ?? "",
            isBase = isBase,
            isAll = isAll,
            targets = targets.Count
        });
    }

    /// <summary>
    /// action=SaveMany — writes every changed field of the form at one scope, in one
    /// transaction.
    ///
    /// Saving field by field looked simpler and was wrong: setting an opening date of
    /// 1 September while the stored closing date is still 1 August would be refused as
    /// "closes before it opens", even though the operator was in the middle of changing
    /// both. The pair is therefore checked against the state the form is asking for, not
    /// against the state it is replacing.
    /// </summary>
    private string HandleSaveMany()
    {
        var js = new JavaScriptSerializer();
        Func<string, string> F = k => (Request.Form[k] ?? "").Trim();

        string scopeType = F("scopeType").ToUpperInvariant();
        if (scopeType == "") scopeType = "GLOBAL";
        string scopeValue = scopeType == "GLOBAL" ? "" : F("scopeValue");
        string acadYear = F("acadYear");
        string notes = F("notes");
        int semester; if (!int.TryParse(F("semester"), out semester)) semester = 0;

        if (scopeType != "GLOBAL" && scopeType != "CAMPUS" && scopeType != "FACULTY" && scopeType != "PROGRAMME" && scopeType != "SESSION")
            return js.Serialize(new { success = false, message = "Choose a valid scope." });
        if (scopeType != "GLOBAL" && scopeValue == "")
            return js.Serialize(new { success = false, message = "Choose which " + scopeType.ToLowerInvariant() + " these settings apply to." });

        List<Dictionary<string, string>> items;
        try { items = new JavaScriptSerializer().Deserialize<List<Dictionary<string, string>>>(F("items")); }
        catch { return js.Serialize(new { success = false, message = "The changes could not be read. Reload the page and try again." }); }
        if (items == null || items.Count == 0)
            return js.Serialize(new { success = false, message = "There is nothing to save." });
        if (items.Count > Catalogue.Count)
            return js.Serialize(new { success = false, message = "More changes were submitted than there are settings." });

        // 1. Check every value against its type before writing any of them.
        var pending = new List<KeyValuePair<Setting, string>>();
        var byKey = new Dictionary<string, string>();
        foreach (var it in items)
        {
            string k = it.ContainsKey("key") ? (it["key"] ?? "").Trim() : "";
            string v = it.ContainsKey("value") ? (it["value"] ?? "") : "";
            Setting def = Catalogue.Find(x => x.Key == k);
            if (def == null) return js.Serialize(new { success = false, message = "Unknown setting \"" + k + "\"." });
            if (byKey.ContainsKey(k)) return js.Serialize(new { success = false, message = "\"" + def.Title + "\" was submitted twice." });

            string bad;
            string norm = NormaliseValue(def, v, out bad);
            if (bad != null) return js.Serialize(new { success = false, message = bad });
            pending.Add(new KeyValuePair<Setting, string>(def, norm));
            byKey[k] = norm;
        }

        List<string> targets = TargetsFor(scopeType, scopeValue);
        if (targets.Count == 0)
            return js.Serialize(new { success = false, message = "There are no " + Plural(scopeType) + " to apply these settings to." });
        bool isAll = (scopeValue == AllToken);

        // 2. Check each window against the state being ASKED FOR: the submitted value
        //    where there is one, otherwise what is already stored. Checked per target,
        //    because two targets can hold different dates and only one of them may be
        //    about to become backwards.
        foreach (string t in targets)
        {
            foreach (WindowDef w in Windows)
            {
                DateTime? opens = byKey.ContainsKey(w.Opens)
                    ? ParseStored(byKey[w.Opens]) : ReadRaw(w.Opens, scopeType, t, acadYear, semester);
                DateTime? closes = byKey.ContainsKey(w.Closes)
                    ? ParseStored(byKey[w.Closes]) : ReadRaw(w.Closes, scopeType, t, acadYear, semester);

                if (opens.HasValue && closes.HasValue && closes.Value <= opens.Value)
                    return js.Serialize(new { success = false, message =
                        w.Title + " would close before it opens (" +
                        opens.Value.ToString("d MMM yyyy, h:mm tt") + " to " +
                        closes.Value.ToString("d MMM yyyy, h:mm tt") + ")" +
                        (isAll ? " for " + scopeType.ToLowerInvariant() + " " + t : "") + ". Check both dates." });
            }
        }

        // 3. Write. All of it or none of it: a half-applied window is a rule nobody
        //    chose, and half a bulk apply is worse than none.
        int written = 0;
        using (var c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                try
                {
                    foreach (string target in targets)
                    foreach (var p in pending)
                    {
                        Setting def = p.Key; string value = p.Value;

                        string before = null;
                        using (var cmd = new MySqlCommand(
                            "SELECT config_value FROM acad_exam_config WHERE config_key=@k AND scope_type=@st " +
                            "AND scope_value=@sv AND acad_year=@y AND semester=@s LIMIT 1", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@k", def.Key); cmd.Parameters.AddWithValue("@st", scopeType);
                            cmd.Parameters.AddWithValue("@sv", target); cmd.Parameters.AddWithValue("@y", acadYear);
                            cmd.Parameters.AddWithValue("@s", semester);
                            object o = cmd.ExecuteScalar();
                            if (o != null && o != DBNull.Value) before = o.ToString();
                        }

                        using (var cmd = new MySqlCommand(
                            "INSERT INTO acad_exam_config (config_key,scope_type,scope_value,acad_year,semester,value_type,config_value,notes,updated_by,updated_at,is_active) " +
                            "VALUES (@k,@st,@sv,@y,@s,@vt,@v,@n,@by,NOW(),1) " +
                            "ON DUPLICATE KEY UPDATE config_value=VALUES(config_value), notes=VALUES(notes), " +
                            "  value_type=VALUES(value_type), updated_by=VALUES(updated_by), updated_at=NOW(), is_active=1", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@k", def.Key); cmd.Parameters.AddWithValue("@st", scopeType);
                            cmd.Parameters.AddWithValue("@sv", target); cmd.Parameters.AddWithValue("@y", acadYear);
                            cmd.Parameters.AddWithValue("@s", semester); cmd.Parameters.AddWithValue("@vt", def.Type);
                            cmd.Parameters.AddWithValue("@v", value); cmd.Parameters.AddWithValue("@n", notes);
                            cmd.Parameters.AddWithValue("@by", Actor());
                            cmd.ExecuteNonQuery();
                        }

                        LogTx(c, tx, def.Key, scopeType, target, acadYear, semester,
                              before, value, before == null ? "CREATE" : "SET");
                        written++;
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { }
                    return js.Serialize(new { success = false, message = "Nothing was saved: " + ex.Message });
                }
            }
        }

        string msg = pending.Count == 1 ? "1 setting saved" : pending.Count + " settings saved";
        if (isAll) msg += " for all " + targets.Count + " " + Plural(scopeType);
        return js.Serialize(new { success = true, written = written, message = msg + "." });
    }

    /// <summary>A stored yyyy-MM-dd HH:mm, or null for empty. Already normalised.</summary>
    private static DateTime? ParseStored(string v)
    {
        v = (v ?? "").Trim();
        if (v == "") return null;
        DateTime d;
        if (DateTime.TryParseExact(v, ExamConfig.DateTimeFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d)) return d;
        return null;
    }

    /// <summary>The other half of a window pair, or null when the key is not one.</summary>
    private static string OppositeWindowKey(string key)
    {
        if (key == ExamConfig.CourseworkOpens) return ExamConfig.CourseworkCloses;
        if (key == ExamConfig.CourseworkCloses) return ExamConfig.CourseworkOpens;
        if (key == ExamConfig.ExamOpens) return ExamConfig.ExamCloses;
        if (key == ExamConfig.ExamCloses) return ExamConfig.ExamOpens;
        return null;
    }

    /// <summary>
    /// The value stored at EXACTLY this scope — not resolved. The pair check must
    /// compare like with like: a global closing date has no business vetoing a
    /// programme-level opening date, because at the programme level the pair is the
    /// programme's own two rows.
    /// </summary>
    private DateTime? ReadRaw(string key, string scopeType, string scopeValue, string acadYear, int semester)
    {
        try
        {
            using (var c = new MySqlConnection(ConnStr))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT config_value FROM acad_exam_config WHERE config_key=@k AND scope_type=@st " +
                    "AND scope_value=@sv AND acad_year=@y AND semester=@s AND is_active=1 LIMIT 1", c))
                {
                    cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@st", scopeType);
                    cmd.Parameters.AddWithValue("@sv", scopeValue); cmd.Parameters.AddWithValue("@y", acadYear);
                    cmd.Parameters.AddWithValue("@s", semester);
                    object o = cmd.ExecuteScalar();
                    if (o == null || o == DBNull.Value) return null;
                    string v = o.ToString().Trim();
                    if (v == "") return null;
                    DateTime d;
                    if (DateTime.TryParseExact(v, ExamConfig.DateTimeFormat,
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out d)) return d;
                    return null;
                }
            }
        }
        catch { return null; }
    }

    private string HandleDelete()
    {
        var js = new JavaScriptSerializer();
        int id; if (!int.TryParse((Request.Form["id"] ?? "").Trim(), out id) || id <= 0)
            return js.Serialize(new { success = false, message = "Missing rule id." });

        using (var c = new MySqlConnection(ConnStr))
        {
            c.Open();
            string key = "", st = "", sv = "", yr = "", val = ""; int sem = 0;
            using (var cmd = new MySqlCommand(
                "SELECT config_key, scope_type, scope_value, acad_year, semester, config_value FROM acad_exam_config WHERE id=@i", c))
            {
                cmd.Parameters.AddWithValue("@i", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return js.Serialize(new { success = false, message = "That rule no longer exists." });
                    key = r.GetString(0); st = r.GetString(1); sv = r.GetString(2);
                    yr = r.GetString(3); sem = r.GetInt32(4); val = r.GetString(5);
                }
            }

            // The base row — university-wide, any year, any semester — is the floor every
            // other rule falls back to. Removing it would leave the code silently using
            // its compiled-in default, which is not something anybody would find by
            // looking at this screen.
            //
            // A university-wide rule for ONE year or semester is not that floor; it is an
            // override like any other and must be removable, or an operator who sets a
            // rule for 2026/2027 semester 1 could never take it off again.
            if (st == "GLOBAL" && yr == "" && sem == 0)
                return js.Serialize(new { success = false, message = "The university-wide value cannot be removed — change it instead. Rules for a particular campus, faculty, programme, year or semester can be removed." });

            using (var cmd = new MySqlCommand("DELETE FROM acad_exam_config WHERE id=@i", c))
            { cmd.Parameters.AddWithValue("@i", id); cmd.ExecuteNonQuery(); }

            Log(c, key, st, sv, yr, sem, val, null, "DELETE");
        }
        return js.Serialize(new { success = true, message = "Override removed. The wider rule now applies." });
    }

    /// <summary>
    /// action=RemoveScoped — take one setting off the scope the form is editing.
    ///
    /// The form removes by scope rather than by row id because "all faculties" is not
    /// one row, it is one row per faculty, and an id-based remove would silently clear
    /// only the first of them.
    /// </summary>
    private string HandleRemoveScoped()
    {
        var js = new JavaScriptSerializer();
        Func<string, string> F = k => (Request.Form[k] ?? "").Trim();

        string key = F("key");
        string scopeType = F("scopeType").ToUpperInvariant();
        if (scopeType == "") scopeType = "GLOBAL";
        string scopeValue = scopeType == "GLOBAL" ? "" : F("scopeValue");
        string acadYear = F("acadYear");
        int semester; if (!int.TryParse(F("semester"), out semester)) semester = 0;

        Setting def = Catalogue.Find(x => x.Key == key);
        if (def == null) return js.Serialize(new { success = false, message = "Unknown setting." });
        if (scopeType != "GLOBAL" && scopeType != "CAMPUS" && scopeType != "FACULTY" && scopeType != "PROGRAMME" && scopeType != "SESSION")
            return js.Serialize(new { success = false, message = "Choose a valid scope." });

        // Same floor as HandleDelete: the university-wide row for every year and every
        // semester is what everything else falls back to.
        if (scopeType == "GLOBAL" && acadYear == "" && semester == 0)
            return js.Serialize(new { success = false, message = "The university-wide value cannot be removed — change it instead." });

        List<string> targets = TargetsFor(scopeType, scopeValue);
        if (targets.Count == 0) return js.Serialize(new { success = false, message = "Nothing to remove." });

        int removed = 0;
        using (var c = new MySqlConnection(ConnStr))
        {
            c.Open();
            using (MySqlTransaction tx = c.BeginTransaction())
            {
                try
                {
                    foreach (string t in targets)
                    {
                        string before = null;
                        using (var cmd = new MySqlCommand(
                            "SELECT config_value FROM acad_exam_config WHERE config_key=@k AND scope_type=@st " +
                            "AND scope_value=@sv AND acad_year=@y AND semester=@s LIMIT 1", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@st", scopeType);
                            cmd.Parameters.AddWithValue("@sv", t); cmd.Parameters.AddWithValue("@y", acadYear);
                            cmd.Parameters.AddWithValue("@s", semester);
                            object o = cmd.ExecuteScalar();
                            if (o != null && o != DBNull.Value) before = o.ToString();
                        }
                        if (before == null) continue;

                        using (var cmd = new MySqlCommand(
                            "DELETE FROM acad_exam_config WHERE config_key=@k AND scope_type=@st " +
                            "AND scope_value=@sv AND acad_year=@y AND semester=@s", c, tx))
                        {
                            cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@st", scopeType);
                            cmd.Parameters.AddWithValue("@sv", t); cmd.Parameters.AddWithValue("@y", acadYear);
                            cmd.Parameters.AddWithValue("@s", semester);
                            removed += cmd.ExecuteNonQuery();
                        }
                        LogTx(c, tx, key, scopeType, t, acadYear, semester, before, null, "DELETE");
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { }
                    return js.Serialize(new { success = false, message = "Nothing was removed: " + ex.Message });
                }
            }
        }

        if (removed == 0) return js.Serialize(new { success = true, message = "There was nothing set here to remove." });
        return js.Serialize(new { success = true, removed = removed, message =
            removed == 1 ? "Rule removed. The wider setting applies again."
                         : removed + " rules removed. The wider setting applies again." });
    }

    /// <summary>
    /// action=Effective — what is actually in force for one programme/campus/period,
    /// and which rule decided it. This is the answer to "why can this lecturer not
    /// enter marks", which is otherwise a table-reading exercise.
    /// </summary>
    private string HandleEffective()
    {
        var js = new JavaScriptSerializer();
        Func<string, string> F = k => (Request.Form[k] ?? Request.QueryString[k] ?? "").Trim();
        int sem; if (!int.TryParse(F("semester"), out sem)) sem = 0;

        var scope = ExamConfig.ScopeForProgramme(F("programme"), F("campus"), F("acadYear"), sem);
        var rows = new List<object>();
        foreach (Setting s in Catalogue)
        {
            string src;
            string v = ExamConfig.Resolve(s.Key, scope, out src);
            rows.Add(new { key = s.Key, title = s.Title, group = s.Group, type = s.Type, value = v ?? "", source = src });
        }
        return js.Serialize(new { success = true, faculty = scope.Faculty, settings = rows });
    }

    private void Log(MySqlConnection c, string key, string st, string sv, string yr, int sem,
                     string before, string after, string action)
    {
        LogTx(c, null, key, st, sv, yr, sem, before, after, action);
    }

    /// <summary>
    /// The audit write, optionally inside a caller's transaction. A batch save must log
    /// on the same transaction as the change, or a rolled-back write would leave an
    /// audit trail for something that never happened.
    /// </summary>
    private void LogTx(MySqlConnection c, MySqlTransaction tx, string key, string st, string sv, string yr, int sem,
                       string before, string after, string action)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "INSERT INTO acad_exam_config_log (config_key,scope_type,scope_value,acad_year,semester,old_value,new_value,action,actor) " +
                "VALUES (@k,@st,@sv,@y,@s,@o,@n,@a,@by)", c, tx))
            {
                cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@st", st);
                cmd.Parameters.AddWithValue("@sv", sv); cmd.Parameters.AddWithValue("@y", yr);
                cmd.Parameters.AddWithValue("@s", sem);
                cmd.Parameters.AddWithValue("@o", (object)before ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@n", (object)after ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@a", action); cmd.Parameters.AddWithValue("@by", Actor());
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* the change is already made; never fail on the audit write */ }
    }
}
