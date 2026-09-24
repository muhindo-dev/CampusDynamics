using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — the eligibility engine.
//  Plan: COOPERP/NewScreens/GRADUATION_CENTRE_PLAN.md
//
//  One source of arithmetic. Every number the interface shows about a
//  candidate is computed here and carried on the candidate; the UI
//  renders what it is given and never re-derives anything. A screen that
//  computes a credit total in one place and a different one two panels
//  away is how a wrong name reaches a graduation list.
//
//  This class knows nothing about pages, requests or JSON.
// =====================================================================

/// <summary>One automated check's result on one student, with the numbers behind it.</summary>
public class GradFinding
{
    public string code = "";     // C1 .. C8
    public string name = "";     // what the check is called on screen
    public string level = "";    // PASS | WARN | BLOCK | NA
    public string detail = "";   // a sentence a Registrar can act on
}

/// <summary>A student the engine believes is at the end of their programme. Not a decision.</summary>
public class GradCandidate
{
    public string regno = "", name = "", progcode = "", progname = "", faculty = "", department = "";
    public string entryyear = "", specialisation = "", specName = "";
    public string nationality = "", gender = "";   // copied onto the graduand row when cleared
    public bool specIsPlaceholder = false;
    public int levelCode = 3, progLength = 3, maxStudyYear = 0;
    public string firstYear = "", lastYear = "";

    public double cuEarned = 0, cuRequired = 0;
    public string cuSource = "NONE";          // STRUCTURE | DECLARED | NONE
    public string cuSourceLabel = "";
    public double cgpa = 0;
    public string degClass = "";

    public int failedPapers = 0, zeroMarks = 0, missingScores = 0, coursesTaken = 0;

    // C4 — how many courses the programme structure requires that the student has no result
    // for at all. Only meaningful when a real specialisation resolves, hence the flag: a
    // structure belonging to somebody else's curriculum is worse than no structure.
    public bool coverageChecked = false;
    public int coverageMissing = 0, coverageRequired = 0;

    // C5 — marks sitting in the portal pipeline below PUBLISHED. Not a fault of the student's,
    // but a candidate whose marks are still with a Dean is not finished being assessed.
    public int unpubEntered = 0, unpubCaptured = 0, unpubApproved = 0;
    public int UnpubTotal { get { return unpubEntered + unpubCaptured + unpubApproved; } }

    public string graduatedYear = "";          // non-empty => already on a graduation list
    public string holdReason = "", holdActor = "", holdAt = "";
    public string clearedActor = "", clearedAt = "";

    public string readiness = "READY";         // READY | WARN | BLOCKED
    public List<GradFinding> findings = new List<GradFinding>();
}

public static class GraduationEngine
{
    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    // The year pattern every academic-year column in this database is supposed to match.
    private const string YEAR_RX = "'^[0-9]{4}/[0-9]{4}$'";

    /// <summary>
    /// A well-formed year that is also a believable one. `acad_results` holds `2202/2203` — four
    /// rows, one student, a transposed digit — and because these are CHARACTER columns it sorts
    /// above every real year. Ranking or comparing against it silently exiles a genuine
    /// candidate: MRU2021001253 is a BED(P) student who reached year 3 with 53 results, and that
    /// single slip put their "last year sat" in the twenty-third century.
    ///
    /// So every comparison and every MIN/MAX over a year uses this, not the bare pattern.
    /// </summary>
    private const string YEAR_OK =
        " REGEXP '^[0-9]{4}/[0-9]{4}$' AND CAST(LEFT({0},4) AS UNSIGNED) BETWEEN 2000 AND 2035 " +
        " AND CAST(RIGHT({0},4) AS UNSIGNED) = CAST(LEFT({0},4) AS UNSIGNED)+1 ";

    /// <summary>The "is a believable year" test for a given column.</summary>
    private static string YearOk(string col)
    { return col + string.Format(YEAR_OK, col); }

    // ── The thresholds, in one place ──────────────────────────────────
    //  Plan §10 asked whether these are the right lines. They are policy, not arithmetic, so
    //  they live here as named constants rather than scattered through the checks: changing
    //  Senate's mind should be a one-line edit, not an archaeology exercise.

    /// <summary>Below this share of the required credits, a structure-derived shortfall blocks.</summary>
    public const double CREDIT_BLOCK_RATIO = 0.90;

    /// <summary>The pass mark. At or above it a paper counts toward earned credit.</summary>
    public const int PASS_MARK = 50;

    /// <summary>Below this CGPA no award class exists in acad_gs_award, at any level.</summary>
    public const double CGPA_FLOOR = 2.0;

    /// <summary>A structure variant with fewer courses than this is a stub, not a curriculum.</summary>
    public const int STRUCTURE_MIN_COURSES = 20;

    // ── Award bands ──────────────────────────────────────────────────
    //  Read from acad_gs_award rather than hardcoded, so a Senate change to the award scale
    //  takes effect without a code change. Every student in the database is on grading
    //  system 1; the classic acad_GetDegClass is NOT used because it is called elsewhere with
    //  SUBSTRING(progid,3,1) against acad_level values like 'Bachelors' and can only return
    //  'N|A'. See the plan, §0.3.
    private class Band { public double lo, hi; public string award = ""; public string level = ""; }
    private static List<Band> _bands;
    private static DateTime _bandsAt = DateTime.MinValue;

    private static List<Band> Bands(MySqlConnection c)
    {
        if (_bands != null && (DateTime.UtcNow - _bandsAt).TotalMinutes < 30) return _bands;
        var list = new List<Band>();
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT lowerlim, upperlim, award, acad_level FROM acad_gs_award WHERE gsID=1", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var b = new Band();
                    b.lo = Convert.ToDouble(r[0]); b.hi = Convert.ToDouble(r[1]);
                    b.award = r[2].ToString(); b.level = r[3].ToString();
                    list.Add(b);
                }
        }
        catch { }
        _bands = list; _bandsAt = DateTime.UtcNow;
        return list;
    }

    public static string LevelName(int levelCode)
    {
        switch (levelCode)
        {
            case 1: return "Certificate";
            case 2: return "Diploma";
            case 4: return "Masters";
            case 5: return "Postgraduate";
            default: return "Bachelors";
        }
    }

    private static string AwardFor(MySqlConnection c, double cgpa, int levelCode)
    {
        if (cgpa <= 0) return "";
        string lvl = LevelName(levelCode);
        foreach (Band b in Bands(c))
            if (b.level == lvl && cgpa >= b.lo && cgpa <= b.hi) return b.award;
        return cgpa >= CGPA_FLOOR ? "" : "Below award";
    }

    // ── Required credits ─────────────────────────────────────────────
    //  The weakest data in the system, so the rule is conservative and the SOURCE travels with
    //  the number everywhere it is displayed. See the plan, §3.3/§3.4 — the original
    //  "median across specialisations" rule was discarded after measuring the variants:
    //  BAED alone has 47 of them ranging 15 to 211 credits.
    private class Req { public double cu; public string src = "NONE"; public string label = ""; }
    private static Dictionary<string, Req> _declared;
    private static Dictionary<string, Req> _structure;   // key: progcode|specid
    private static DateTime _reqAt = DateTime.MinValue;

    /// <summary>Is a declared minimum credible for this level and length?</summary>
    private static bool DeclaredCredible(double cu, int levelCode, int years)
    {
        if (cu <= 0) return false;
        // Bands observed across the real programmes. They exist to reject the junk rows in
        // acad_programme whose progcode is actually a COURSE code (BEE1101, HRP 1101, …),
        // every one of which carries mincredit = 3. Without this, such a student would read
        // as "3 credits required, 90 earned" and sail through.
        switch (levelCode)
        {
            case 1: return cu >= 20 && cu <= 40;     // Certificate
            case 2: return cu >= 40 && cu <= 130;    // Diploma
            case 4: return cu >= 30 && cu <= 60;     // Masters
            case 5: return cu >= 40 && cu <= 200;    // Postgraduate
            default: return cu >= 100 && cu <= 220;  // Bachelors
        }
    }

    private static void LoadRequirements(MySqlConnection c)
    {
        if (_declared != null && (DateTime.UtcNow - _reqAt).TotalMinutes < 30) return;
        var dec = new Dictionary<string, Req>(StringComparer.OrdinalIgnoreCase);
        var str = new Dictionary<string, Req>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT TRIM(progcode), IFNULL(mincredit,0), IFNULL(levelCode,3), " +
                "IFNULL(NULLIF(couselength,0),3) FROM acad_programme WHERE TRIM(IFNULL(progcode,''))<>''", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string pc = r[0].ToString();
                    double cu = Convert.ToDouble(r[1]);
                    int lvl = Convert.ToInt32(r[2]); int yrs = Convert.ToInt32(r[3]);
                    if (DeclaredCredible(cu, lvl, yrs))
                    {
                        var q = new Req(); q.cu = cu; q.src = "DECLARED";
                        q.label = "the programme's declared minimum";
                        if (!dec.ContainsKey(pc)) dec.Add(pc, q);
                    }
                }

            // A structure variant only counts when it is a real curriculum, not a stub. Twenty
            // courses is the floor: below that the "specialisation" is an incomplete mapping,
            // and several carry two.
            using (var cmd = new MySqlCommand(
                "SELECT TRIM(pc.progcode) pc, IFNULL(pc.specialisation_id,0) sp, " +
                " SUM(IFNULL(ac.CreditUnit,0)) cu, COUNT(*) n " +
                "FROM acad_programmecourses pc LEFT JOIN acad_course ac ON ac.courseID=pc.course_code " +
                "WHERE pc.status='Active' GROUP BY 1, 2 HAVING n >= " + STRUCTURE_MIN_COURSES + " AND cu > 0", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var q = new Req();
                    q.cu = Convert.ToDouble(r[2]); q.src = "STRUCTURE";
                    q.label = "the programme structure for this specialisation";
                    string key = r[0].ToString() + "|" + r[1].ToString();
                    if (!str.ContainsKey(key)) str.Add(key, q);
                }
        }
        catch { }
        _declared = dec; _structure = str; _reqAt = DateTime.UtcNow;
    }

    private static Req RequiredFor(MySqlConnection c, string progcode, string specialisation, int levelCode)
    {
        LoadRequirements(c);
        progcode = (progcode ?? "").Trim();

        // A real specialisation gives the strongest answer. '13' is the placeholder 30,009 of
        // 33,253 students carry, and '0' is "none recorded" — neither identifies a curriculum.
        string sp = (specialisation ?? "").Trim();
        if (sp != "" && sp != "0" && sp != "13")
        {
            Req s;
            if (_structure.TryGetValue(progcode + "|" + sp, out s)) return s;
        }
        Req d;
        if (_declared.TryGetValue(progcode, out d)) return d;

        var none = new Req(); none.cu = 0; none.src = "NONE";
        none.label = "no credible requirement is recorded for this programme";
        return none;
    }

    // ── Filters ──────────────────────────────────────────────────────
    public class GradFilter
    {
        public string acadYear = "";      // the graduation year being compiled
        public string faculty = "";
        public string department = "";
        public string programme = "";
        public string entryYear = "";
        public string finishedIn = "";    // '' = any; else the academic year they last sat

        /// <summary>
        /// "cycle" (the default) narrows to the people actually being graduated in the selected
        /// year — those who finished in it or the year before. "all" opens it to everyone who
        /// has ever reached a final year and never graduated, which is the historical backlog.
        ///
        /// The default matters: without it the queue opens on 14,548 students, 13,460 of whom
        /// finished before last year, and the 997 people a Registrar is compiling a list for
        /// this week are lost in it.
        /// </summary>
        public string focus = "cycle";
        public string state = "pending";  // pending | held | listed | all
        public string readiness = "";     // '' | ready | warn | blocked
        /// <summary>Class of award, for the graduation list. Matched as a prefix, so "First"
        /// catches both "First Class Honours" and "First Class".</summary>
        public string award = "";
        public string search = "";
        public string sort = "regno";
        public int page = 1;
        public int size = 50;
    }

    /// <summary>"2026/2027" -> "2025/2026". Empty for anything that is not a plausible year.</summary>
    public static string PreviousYear(string acad)
    {
        acad = (acad ?? "").Trim();
        if (acad.Length != 9 || acad[4] != '/') return "";
        int a, b;
        if (!int.TryParse(acad.Substring(0, 4), out a)) return "";
        if (!int.TryParse(acad.Substring(5, 4), out b)) return "";
        if (a < 2000 || a > 2035 || b != a + 1) return "";
        return (a - 1).ToString(CultureInfo.InvariantCulture) + "/" + a.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The SQL that narrows a candidate set to the cycle graduating in <paramref name="acadYear"/>.
    ///
    /// Expressed as "has a result in Y or Y-1, and none after Y" rather than a correlated MAX(),
    /// because the two EXISTS clauses seek straight into Index_UNQ(regno, …) and stop at the
    /// first row, while the MAX form reads every result the student has.
    /// </summary>
    private static string CycleClause(GradFilter f, Dictionary<string, object> p)
    {
        if (f.focus != "cycle" || f.acadYear == "") return "";
        string prev = PreviousYear(f.acadYear);
        if (prev == "") return "";
        p["@cy1"] = f.acadYear;
        p["@cy0"] = prev;
        // last_year is stored, already restricted to believable years, so "finished in Y or
        // Y-1" is now a column comparison instead of two correlated EXISTS over acad_results.
        return " AND a.last_year IN (@cy1,@cy0) ";
    }

    private static string RS(MySqlDataReader r, int i)
    { return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)); }
    private static double RD(MySqlDataReader r, int i)
    { return r.IsDBNull(i) ? 0.0 : Convert.ToDouble(r.GetValue(i)); }
    private static int RI(MySqlDataReader r, int i)
    { return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i)); }

    /// <summary>
    /// Phase 1 of the list: which students are candidates, in order, for one page.
    ///
    /// Candidacy is ONE rule, validated against every graduation year on record: the student has
    /// reached the final year of their own programme. Expressed as a semi-join rather than
    /// MAX(studyyear) GROUP BY regno, because the aggregate form costs 10.3s over the whole
    /// table while this costs 0.010s and answers the same question.
    /// </summary>
    private static string CandidateSql(MarksScope scope, GradFilter f, Dictionary<string, object> p, bool countOnly)
    {
        // Everything hangs off acad_grad_stats: is_candidate, on_list and last_year are all
        // stored, so identifying a candidate is an index lookup rather than an aggregate over
        // 639,185 result rows. See GraduationStats for why, and for what stays live.
        var w = new StringBuilder();
        w.Append(" FROM " + GraduationStats.TABLE + " a ");
        w.Append(" JOIN acad_student s ON s.regno=a.regno ");
        w.Append(" JOIN acad_programme p ON p.progcode=s.progid ");
        w.Append(" WHERE a.is_candidate=1 ");

        if (f.state == "pending")
            w.Append(" AND a.on_list=0 ");
        else if (f.state == "listed")
            w.Append(" AND EXISTS (SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno" +
                     (f.acadYear == "" ? "" : " AND g.acadyear=@ay") + ") ");
        else if (f.state == "held")
            w.Append(" AND EXISTS (SELECT 1 FROM acad_grad_review v WHERE v.regno=s.regno " +
                     " AND v.verdict='HELD' AND v.superseded_at IS NULL) ");

        if (f.acadYear != "") p["@ay"] = f.acadYear;
        if (f.faculty != "") { w.Append(" AND p.faculty_code=@fac "); p["@fac"] = f.faculty; }
        int dep;
        if (f.department != "" && int.TryParse(f.department, out dep))
        { w.Append(" AND p.department_id=@dep "); p["@dep"] = dep; }
        if (f.programme != "") { w.Append(" AND s.progid=@prog "); p["@prog"] = f.programme; }
        if (f.entryYear != "") { w.Append(" AND s.entryyear=@ey "); p["@ey"] = f.entryYear; }
        // The focus is only meaningful for people not yet on a list; the graduation list and
        // the held queue are already narrow by definition.
        if (f.state == "pending") w.Append(CycleClause(f, p));

        if (f.finishedIn != "") { w.Append(" AND a.last_year=@fin "); p["@fin"] = f.finishedIn; }
        if (f.search != "")
        {
            w.Append(" AND (s.regno LIKE @q OR CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,'')) LIKE @q) ");
            p["@q"] = "%" + f.search + "%";
        }
        w.Append(scope.ProgFilter("s", "progid"));

        if (countOnly) return "SELECT COUNT(*) " + w;

        string order;
        if (f.state == "held")
        {
            // Oldest hold first. A hold nobody revisits is a student who quietly never
            // graduates, so the queue has to surface the ones that have been waiting longest
            // rather than whoever happens to sort first by student number.
            order = " ORDER BY (SELECT MIN(v2.created_at) FROM acad_grad_review v2 " +
                    " WHERE v2.regno=s.regno AND v2.verdict='HELD' AND v2.superseded_at IS NULL) ASC, s.regno ";
        }
        else switch (f.sort)
        {
            case "name": order = " ORDER BY nm "; break;
            case "prog": order = " ORDER BY p.progname, s.regno "; break;
            case "entry": order = " ORDER BY s.entryyear DESC, s.regno "; break;
            default: order = " ORDER BY s.regno "; break;
        }
        return "SELECT s.regno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) nm, " +
               " TRIM(s.progid) progid, IFNULL(p.progname,'') progname, IFNULL(p.faculty_code,'') fac, " +
               " IFNULL(p.department_id,0) dep, IFNULL(s.entryyear,'') ey, IFNULL(s.specialisation,'') sp, " +
               " IFNULL(p.levelCode,3) lvl, IFNULL(NULLIF(p.couselength,0),3) plen, " +
               // carried from the summary, so the page needs no second pass over acad_results
               " a.maxsy, a.courses, a.cu_earned, a.fails, a.zero_marks, a.no_score, a.cgpa, " +
               " IFNULL(a.first_year,''), IFNULL(a.last_year,'') " +
               w + order + " LIMIT " + f.size + " OFFSET " + ((f.page < 1 ? 0 : f.page - 1) * f.size);
    }

    /// <summary>
    /// A page of candidates, fully assessed. Two phases: identify and page the students
    /// (0.010s), then aggregate their results by explicit id list (0.082s for 100). The
    /// alternative — aggregating the whole results table — costs 10.3s and is what makes this
    /// kind of screen unusable.
    /// </summary>
    public static List<GradCandidate> Page(MarksScope scope, GradFilter f, out int total)
    {
        var list = new List<GradCandidate>();
        total = 0;
        if (scope == null || !scope.HasAccess) return list;
        if (f.size < 1 || f.size > 200) f.size = 50;

        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();
            var p = new Dictionary<string, object>();
            using (var cmd = new MySqlCommand(CandidateSql(scope, f, p, true), c))
            {
                foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                object o = cmd.ExecuteScalar();
                total = o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
            }
            if (total == 0) return list;

            var byReg = new Dictionary<string, GradCandidate>(StringComparer.OrdinalIgnoreCase);
            var p2 = new Dictionary<string, object>();
            using (var cmd = new MySqlCommand(CandidateSql(scope, f, p2, false), c))
            {
                foreach (KeyValuePair<string, object> kv in p2) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        var g = new GradCandidate();
                        g.regno = RS(r, 0); g.name = RS(r, 1); g.progcode = RS(r, 2);
                        g.progname = RS(r, 3); g.faculty = RS(r, 4); g.department = RS(r, 5);
                        g.entryyear = RS(r, 6); g.specialisation = RS(r, 7);
                        g.levelCode = RI(r, 8); g.progLength = RI(r, 9);
                        g.maxStudyYear = RI(r, 10); g.coursesTaken = RI(r, 11);
                        g.cuEarned = RD(r, 12); g.failedPapers = RI(r, 13);
                        g.zeroMarks = RI(r, 14); g.missingScores = RI(r, 15);
                        g.cgpa = RD(r, 16); g.firstYear = RS(r, 17); g.lastYear = RS(r, 18);
                        g.specIsPlaceholder = (g.specialisation == "" || g.specialisation == "0" || g.specialisation == "13");
                        list.Add(g);
                        if (!byReg.ContainsKey(g.regno)) byReg.Add(g.regno, g);
                    }
            }
            if (list.Count == 0) return list;

            Enrich(c, list, byReg);
            foreach (GradCandidate g in list) Assess(c, g);
        }
        return list;
    }

    /// <summary>
    /// Every candidate matching the filter, not just one page of them.
    ///
    /// This exists because the export handlers used to ask <see cref="Page"/> for five thousand
    /// rows and silently receive fifty. Page opens with a guard - "if (f.size &lt; 1 || f.size &gt; 200)
    /// f.size = 50" - which is right for a screen and wrong for a file: at the module's own default
    /// view there are 996 pending candidates, and the workbook contained 50 of them with a cover
    /// sheet that said so as though it were the whole answer.
    ///
    /// Rather than raise that guard and let one query assemble an unbounded IN list, this walks
    /// Page in chunks of 200. Every query keeps exactly the shape it was tuned for, and the caller
    /// gets an explicit truncated flag when the cap is reached so the file can say so in words
    /// instead of stopping quietly.
    /// </summary>
    /// <param name="cap">Hard ceiling on rows returned. 0 or less means the default 20,000.</param>
    /// <param name="total">How many rows matched the filter, before the cap.</param>
    /// <param name="truncated">True when the cap stopped the walk short of total.</param>
    public static List<GradCandidate> All(MarksScope scope, GradFilter f, int cap,
                                          out int total, out bool truncated)
    {
        const int CHUNK = 200;
        var all = new List<GradCandidate>();
        total = 0;
        truncated = false;
        if (scope == null || !scope.HasAccess) return all;
        if (cap <= 0) cap = 20000;

        // Page mutates f.size and f.page, and the caller's filter is reused afterwards for the
        // cover sheet, so walk a copy.
        GradFilter w = Copy(f);
        w.size = CHUNK;

        for (int pageNo = 1; ; pageNo++)
        {
            w.page = pageNo;
            w.size = CHUNK;                 // Page clamps in place; reset it every turn
            int t;
            List<GradCandidate> chunk = Page(scope, w, out t);
            if (pageNo == 1) total = t;
            if (chunk.Count == 0) break;

            all.AddRange(chunk);
            if (all.Count >= cap)
            {
                if (all.Count > cap) all.RemoveRange(cap, all.Count - cap);
                truncated = all.Count < total;
                break;
            }
            if (all.Count >= total) break;
            // Defensive: a filter that somehow never exhausts must not spin forever.
            if (pageNo > (cap / CHUNK) + 2) { truncated = all.Count < total; break; }
        }
        return all;
    }

    /// <summary>A field-for-field copy, so a walk cannot disturb the caller's filter.</summary>
    public static GradFilter Copy(GradFilter f)
    {
        var c = new GradFilter();
        if (f == null) return c;
        c.acadYear = f.acadYear; c.faculty = f.faculty; c.department = f.department;
        c.programme = f.programme; c.entryYear = f.entryYear; c.finishedIn = f.finishedIn;
        c.focus = f.focus; c.state = f.state; c.readiness = f.readiness;
        c.search = f.search; c.sort = f.sort; c.page = f.page; c.size = f.size;
        return c;
    }

    /// <summary>
    /// Readiness (READY / WARN / BLOCKED) is decided in C# by <see cref="Assess"/>, not in SQL, so
    /// it cannot be part of the WHERE clause and cannot be counted by the database. Applying it
    /// here - in one place - is what keeps the count shown in the export dialog equal to the number
    /// of rows that actually land in the file.
    /// </summary>
    public static List<GradCandidate> FilterByReadiness(List<GradCandidate> rows, string readiness)
    {
        if (rows == null) return new List<GradCandidate>();
        string want = (readiness ?? "").Trim().ToLowerInvariant();
        if (want == "") return rows;
        string code = want == "ready" ? "READY" : want == "warn" ? "WARN" : want == "blocked" ? "BLOCKED" : "";
        if (code == "") return rows;
        var outp = new List<GradCandidate>();
        foreach (GradCandidate g in rows) if (g.readiness == code) outp.Add(g);
        return outp;
    }

    /// <summary>Phase 2: the results arithmetic for exactly the students on this page.</summary>
    private static void Enrich(MySqlConnection c, List<GradCandidate> list, Dictionary<string, GradCandidate> byReg)
    {
        string inList = InList(list);

        // The results arithmetic already arrived with the list query, out of the summary, so
        // there is no second pass over acad_results here at all. What remains is the two small
        // membership lookups below.

        // Already on a graduation list?
        using (var cmd = new MySqlCommand(
            "SELECT regno, acadyear FROM acad_graduands WHERE regno IN (" + inList + ")", c))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                GradCandidate g;
                if (byReg.TryGetValue(RS(r, 0), out g)) g.graduatedYear = RS(r, 1);
            }

        // C4 — required courses with no result, for the students on this page whose
        // specialisation is real. Set-based: one query for the page, not one per student.
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT s.regno, COUNT(*) req, " +
                " SUM(NOT EXISTS(SELECT 1 FROM acad_results r WHERE r.regno=s.regno AND r.courseid=pc.course_code)) missing " +
                "FROM acad_student s " +
                "JOIN acad_programmecourses pc ON pc.progcode=s.progid " +
                " AND pc.specialisation_id=CAST(s.specialisation AS UNSIGNED) AND pc.status='Active' " +
                "WHERE s.regno IN (" + inList + ") " +
                " AND TRIM(IFNULL(s.specialisation,'')) NOT IN ('','0','13') " +
                "GROUP BY s.regno", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    GradCandidate g;
                    if (!byReg.TryGetValue(RS(r, 0), out g)) continue;
                    g.coverageChecked = true;
                    g.coverageRequired = RI(r, 1);
                    g.coverageMissing = RI(r, 2);
                }
        }
        catch { /* coverage is advisory; never let it take the whole list down */ }

        // C5 — marks still in the portal pipeline.
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT cr.regno, SUM(cr.mark_stage='ENTERED'), SUM(cr.mark_stage='CAPTURED'), " +
                " SUM(cr.mark_stage='APPROVED') " +
                "FROM campus_dynamics_portal.acad_course_registration cr " +
                "WHERE cr.regno IN (" + inList + ") AND cr.mark_stage IN ('ENTERED','CAPTURED','APPROVED') " +
                "GROUP BY cr.regno", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    GradCandidate g;
                    if (!byReg.TryGetValue(RS(r, 0), out g)) continue;
                    g.unpubEntered = RI(r, 1); g.unpubCaptured = RI(r, 2); g.unpubApproved = RI(r, 3);
                }
        }
        catch { }

        // The verdict in force, if any.
        using (var cmd = new MySqlCommand(
            "SELECT regno, verdict, IFNULL(reason,''), actor, DATE_FORMAT(created_at,'%e %b %Y') " +
            "FROM acad_grad_review WHERE superseded_at IS NULL AND regno IN (" + inList + ")", c))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                GradCandidate g;
                if (!byReg.TryGetValue(RS(r, 0), out g)) continue;
                if (RS(r, 1) == "HELD")
                { g.holdReason = RS(r, 2); g.holdActor = RS(r, 3); g.holdAt = RS(r, 4); }
                else if (RS(r, 1) == "CLEARED")
                { g.clearedActor = RS(r, 3); g.clearedAt = RS(r, 4); }
            }
    }

    /// <summary>
    /// Student numbers are quoted into an IN list rather than parameterised, because MySQL has
    /// no array parameter. They come from the database one query earlier, never from a request,
    /// and the escape below is belt-and-braces on top of that.
    /// </summary>
    private static string InList(List<GradCandidate> list)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('\'').Append(MySqlHelper.EscapeString(list[i].regno)).Append('\'');
        }
        return sb.Length == 0 ? "''" : sb.ToString();
    }

    // ── Overview ──────────────────────────────────────────
    public class ProgProgress
    {
        public string progcode = "", progname = "", faculty = "";
        public int candidates = 0, listed = 0, held = 0, blocked = 0;
    }

    public class GradOverview
    {
        public int candidates = 0;       // reached final year, not on any list
        public int ready = 0;            // nothing blocking
        public int blocked = 0;          // at least one BLOCK
        public int held = 0;             // an open hold
        public int listed = 0;           // on the graduation list for the selected year
        public int listedAllYears = 0;
        public int backlogEarlier = 0;   // finished before the selected year and still not listed
        public List<GC2> blockers = new List<GC2>();
        public List<ProgProgress> programmes = new List<ProgProgress>();
        public List<string> integrity = new List<string>();

        /// <summary>What the numbers above actually cover, in words, for the page to print.</summary>
        public string focusLabel = "";
        public string focusFrom = "", focusTo = "";
    }
    public class GC2 { public string name = ""; public int count = 0; }

    /// <summary>The cycle narrowing for the per-programme table, kept in step with the counts.</summary>
    private static string extra2(GradFilter f, Dictionary<string, object> p)
    { return CycleClause(f, p); }

    private static void AddGc(List<GC2> l, string n, int v)
    { var g = new GC2(); g.name = n; g.count = v; l.Add(g); }

    /// <summary>
    /// The four numbers at the top of the module, the blocker breakdown beneath them, and the
    /// per-programme progress table.
    ///
    /// Computed set-based. Running the per-candidate assessment over 14,548 students to draw a
    /// dashboard would be exactly the mistake the Results Exporter was making, and every figure
    /// here has to reconcile with the list it links to, so the SAME candidacy predicate is used.
    /// </summary>
    public static GradOverview Overview(MarksScope scope, GradFilter f)
    {
        var o = new GradOverview();
        if (scope == null || !scope.HasAccess) return o;

        string prevYr = PreviousYear(f.acadYear);
        if (f.focus == "cycle" && prevYr != "")
        {
            o.focusFrom = prevYr; o.focusTo = f.acadYear;
            o.focusLabel = "Finishing in " + prevYr + " or " + f.acadYear;
        }
        else o.focusLabel = "Everyone who has reached a final year and never graduated";

        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();

            // The candidacy predicate, shared with the list so the two cannot disagree — and
            // now a stored column rather than an aggregate. See GraduationStats.
            string from =
                " FROM " + GraduationStats.TABLE + " a " +
                " JOIN acad_student s ON s.regno=a.regno " +
                " JOIN acad_programme p ON p.progcode=s.progid " +
                " LEFT JOIN (SELECT DISTINCT regno, 1 held FROM acad_grad_review " +
                "            WHERE verdict='HELD' AND superseded_at IS NULL) h ON h.regno=a.regno " +
                " WHERE a.is_candidate=1 ";

            var p = new Dictionary<string, object>();
            var extra = new StringBuilder();
            if (f.faculty != "") { extra.Append(" AND p.faculty_code=@fac "); p["@fac"] = f.faculty; }
            int dep;
            if (f.department != "" && int.TryParse(f.department, out dep))
            { extra.Append(" AND p.department_id=@dep "); p["@dep"] = dep; }
            if (f.programme != "") { extra.Append(" AND s.progid=@prog "); p["@prog"] = f.programme; }
            extra.Append(scope.ProgFilter("s", "progid"));

            string where = from + extra;
            string notListed = " AND a.on_list=0 " + CycleClause(f, p);

            // One pass over the outstanding candidates, bucketed by what is wrong with them.
            // The buckets PARTITION the pool: a student who both fails a paper and sits below
            // the CGPA floor is one blocked student, not two, and Ready is what is left.
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*), SUM(a.fails>0), SUM(a.cgpa<2.0), SUM(h.held IS NOT NULL), " +
                " SUM(a.fails>0 OR a.cgpa<2.0 OR h.held IS NOT NULL), " +
                " SUM(NOT (a.fails>0 OR a.cgpa<2.0 OR h.held IS NOT NULL) " +
                "     AND (a.zero_marks>0 OR a.no_score>0)) " +
                where + notListed, c))
            {
                foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        o.candidates = RI(r, 0);
                        int blkFail = RI(r, 1), blkCgpa = RI(r, 2);
                        o.held = RI(r, 3);
                        o.blocked = RI(r, 4);
                        int warnMarks = RI(r, 5);
                        o.ready = o.candidates - o.blocked - warnMarks;
                        if (o.ready < 0) o.ready = 0;
                        // The blocker table overlaps on purpose — it answers "how many would this
                        // one problem release", not "how do they partition".
                        AddGc(o.blockers, "Failed papers (1-49)", blkFail);
                        AddGc(o.blockers, "CGPA below " + N(CGPA_FLOOR, 1), blkCgpa);
                        AddGc(o.blockers, "Marks of zero, or no mark at all", warnMarks);
                        AddGc(o.blockers, "Held by a reviewer", o.held);
                    }
            }

            // On the graduation list for the selected year.
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM acad_graduands g JOIN acad_student s ON s.regno=g.regno " +
                "JOIN acad_programme p ON p.progcode=s.progid WHERE 1=1 " +
                (f.acadYear == "" ? "" : " AND g.acadyear=@ay ") + extra, c))
            {
                if (f.acadYear != "") cmd.Parameters.AddWithValue("@ay", f.acadYear);
                foreach (KeyValuePair<string, object> kv in p)
                    if (kv.Key != "@cy1" && kv.Key != "@cy0") cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                object v = cmd.ExecuteScalar();
                o.listed = v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
            }

            // Per-programme progress. A Dean's week before Senate is spent in this table.
            using (var cmd = new MySqlCommand(
                "SELECT a.progcode, MAX(IFNULL(p.progname,'')), MAX(IFNULL(p.faculty_code,'')), " +
                " COUNT(*), SUM(a.on_list), SUM(h.held IS NOT NULL), SUM(a.fails>0) " +
                where + extra2(f, p) + " GROUP BY a.progcode ORDER BY 4 DESC LIMIT 60", c))
            {
                foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        var pp = new ProgProgress();
                        pp.progcode = RS(r, 0); pp.progname = RS(r, 1); pp.faculty = RS(r, 2);
                        pp.candidates = RI(r, 3); pp.listed = RI(r, 4); pp.held = RI(r, 5);
                        pp.blocked = RI(r, 6);
                        o.programmes.Add(pp);
                    }
            }

            // Data-integrity notices. Reported, never auto-corrected: taking a name off a
            // graduation list is a Registrar's decision, not a script's.
            try
            {
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM acad_graduands g " +
                    "JOIN " + GraduationStats.TABLE + " a ON a.regno=g.regno " +
                    "JOIN acad_student s ON s.regno=g.regno " +
                    "JOIN acad_programme p ON p.progcode=s.progid " +
                    "WHERE a.is_candidate=0" + extra, c))
                {
                    foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                    object v = cmd.ExecuteScalar();
                    int bad = v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
                    if (bad > 0)
                        o.integrity.Add(bad + " name" + (bad == 1 ? " is" : "s are") +
                            " already on a graduation list without having reached the final year of their programme.");
                }
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM (SELECT regno FROM acad_graduands GROUP BY regno HAVING COUNT(*)>1) x", c))
                {
                    object v = cmd.ExecuteScalar();
                    int dup = v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
                    if (dup > 0)
                        o.integrity.Add(dup + " student" + (dup == 1 ? " appears" : "s appear") +
                            " on a graduation list more than once.");
                }

                // Results filed under a year that cannot exist. The module now ignores such a
                // year when it ranks or compares, so nobody is hidden by one — but the mark is
                // still filed in the wrong place and only a human can say where it belongs.
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(DISTINCT r.regno), COUNT(*), GROUP_CONCAT(DISTINCT r.acad ORDER BY r.acad SEPARATOR ', ') " +
                    "FROM acad_results r WHERE NOT (" + YearOk("r.acad") + ")", c))
                using (var rr = cmd.ExecuteReader())
                    if (rr.Read() && !rr.IsDBNull(0) && Convert.ToInt32(rr[0]) > 0)
                        o.integrity.Add(Convert.ToInt32(rr[1]) + " result" + (Convert.ToInt32(rr[1]) == 1 ? " is" : "s are") +
                            " filed under an academic year that cannot exist (" + rr[2] +
                            "), affecting " + Convert.ToInt32(rr[0]) + " student" +
                            (Convert.ToInt32(rr[0]) == 1 ? "" : "s") +
                            ". Those years are ignored when ranking, so nobody is hidden by them, " +
                            "but the marks are still filed in the wrong year.");
            }
            catch { }
        }
        return o;
    }

    // ── The checks ───────────────────────────────────────────────────
    private static void Add(GradCandidate g, string code, string name, string level, string detail)
    {
        var f = new GradFinding();
        f.code = code; f.name = name; f.level = level; f.detail = detail;
        g.findings.Add(f);
    }

    private static string N(double d, int dp)
    { return d.ToString("F" + dp, CultureInfo.InvariantCulture); }

    /// <summary>
    /// Runs every check and settles the readiness. BLOCK anywhere means not ready; the order
    /// of the findings is the order a human should read them, so the membership facts (already
    /// graduated, already held) come first — the list must never offer an action that
    /// contradicts something further down the panel.
    /// </summary>
    public static void Assess(MySqlConnection c, GradCandidate g)
    {
        g.findings.Clear();
        Req req = RequiredFor(c, g.progcode, g.specialisation, g.levelCode);
        g.cuRequired = req.cu; g.cuSource = req.src; g.cuSourceLabel = req.label;
        g.degClass = AwardFor(c, g.cgpa, g.levelCode);

        // C1 — already on a graduation list
        if (g.graduatedYear != "")
            Add(g, "C1", "Already on a graduation list", "BLOCK",
                "This student is already on the " + g.graduatedYear + " graduation list.");
        else
            Add(g, "C1", "Already on a graduation list", "PASS", "Not on any graduation list.");

        // C8 — an open hold
        if (g.holdReason != "")
            Add(g, "C8", "Held by a reviewer", "BLOCK",
                "Held by " + g.holdActor + " on " + g.holdAt + ": " + g.holdReason);

        // C2 — outstanding papers.
        //
        //  A mark of exactly zero is treated as a GAP, not a failure, and that distinction was
        //  measured rather than assumed. Of the 522 students who really graduated in 2024/2025,
        //  15 carry a zero and only 2 carry a mark between 1 and 49. Across the whole results
        //  table there are 5,380 zeros against 6,793 real fails. Blocking on zero would have
        //  stopped fifteen legitimate graduands; blocking on 1-49 stops two, and both of those
        //  genuinely sat and failed.
        //
        //  It is still shown, still counted, and still keeps the student out of READY. It just
        //  does not masquerade as a failed paper, because the fix for the two is different: one
        //  needs a mark entered, the other needs a retake.
        if (g.failedPapers > 0)
            Add(g, "C2", "Outstanding papers", "BLOCK",
                g.failedPapers + (g.failedPapers == 1 ? " paper is" : " papers are") +
                " marked between 1 and 49 — below the pass mark.");
        else if (g.zeroMarks > 0)
            Add(g, "C2", "Outstanding papers", "WARN",
                g.zeroMarks + " course" + (g.zeroMarks == 1 ? " is" : "s are") +
                " recorded as zero. That is usually a paper never marked rather than a paper failed — check before clearing.");
        else if (g.missingScores > 0)
            Add(g, "C2", "Outstanding papers", "WARN",
                g.missingScores + " course" + (g.missingScores == 1 ? " has" : "s have") + " no score at all.");
        else
            Add(g, "C2", "Outstanding papers", "PASS", "Every course carries a passing mark.");

        // C3 — credits
        if (g.cuSource == "NONE")
            Add(g, "C3", "Credits earned", "NA",
                "Cannot be assessed — " + req.label + ". " + N(g.cuEarned, 0) + " credits earned.");
        else if (g.cuEarned >= g.cuRequired)
            Add(g, "C3", "Credits earned", "PASS",
                N(g.cuEarned, 0) + " of " + N(g.cuRequired, 0) + " credits, against " + req.label + ".");
        else
        {
            double shortBy = g.cuRequired - g.cuEarned;
            // Only a real curriculum is trusted enough to stop a graduation on a credit count.
            string lvl = (g.cuSource == "STRUCTURE" && g.cuEarned < g.cuRequired * CREDIT_BLOCK_RATIO) ? "BLOCK" : "WARN";
            Add(g, "C3", "Credits earned", lvl,
                "Short by " + N(shortBy, 0) + " credits — " + N(g.cuEarned, 0) + " of " +
                N(g.cuRequired, 0) + ", against " + req.label + ".");
        }

        // C4 — coverage against the programme structure.
        //
        //  Never blocks. A gap here can mean a genuinely unsat course, but it can equally mean
        //  the student took an equivalent under a different code, or that the curriculum on
        //  file has moved on since their intake. It is a prompt to look at the structure panel,
        //  not a verdict.
        if (!g.coverageChecked)
            Add(g, "C4", "Programme coverage", "NA",
                "Cannot be checked \u2014 this student carries no real specialisation, so there is no " +
                "curriculum to match their courses against.");
        else if (g.coverageMissing > 0)
            Add(g, "C4", "Programme coverage", "WARN",
                g.coverageMissing + " of " + g.coverageRequired + " courses in their curriculum have no " +
                "result on record. Check the structure below \u2014 an equivalent may have been taken " +
                "under another code.");
        else
            Add(g, "C4", "Programme coverage", "PASS",
                "All " + g.coverageRequired + " courses in their curriculum have a result.");

        // C5 — marks still moving through the pipeline.
        if (g.UnpubTotal > 0)
        {
            var bits = new List<string>();
            if (g.unpubEntered > 0) bits.Add(g.unpubEntered + " with the lecturer");
            if (g.unpubCaptured > 0) bits.Add(g.unpubCaptured + " with the head of department");
            if (g.unpubApproved > 0) bits.Add(g.unpubApproved + " approved but not published");
            Add(g, "C5", "Marks not yet published", "WARN",
                g.UnpubTotal + " course" + (g.UnpubTotal == 1 ? " is" : "s are") +
                " still in the marks pipeline \u2014 " + string.Join(", ", bits.ToArray()) +
                ". Their final position may change.");
        }
        else
            Add(g, "C5", "Marks not yet published", "PASS", "Nothing outstanding in the marks pipeline.");

        // C6 — reached the final year (the candidacy rule itself, restated as evidence)
        if (g.maxStudyYear >= g.progLength)
            Add(g, "C6", "Programme duration", "PASS",
                "Reached year " + g.maxStudyYear + " of a " + g.progLength + "-year programme" +
                (g.firstYear != "" ? " (" + g.firstYear + " to " + g.lastYear + ")." : "."));
        else
            Add(g, "C6", "Programme duration", "BLOCK",
                "Only reached year " + g.maxStudyYear + " of a " + g.progLength + "-year programme.");

        // C7 — CGPA floor
        if (g.cgpa <= 0)
            Add(g, "C7", "Class of award", "WARN", "CGPA could not be computed from the marks on record.");
        else if (g.cgpa < CGPA_FLOOR)
            Add(g, "C7", "Class of award", "BLOCK",
                "CGPA " + N(g.cgpa, 2) + " is below " + N(CGPA_FLOOR, 1) +
                ", the floor below which acad_gs_award maps no class at any level.");
        else
            Add(g, "C7", "Class of award", "PASS",
                "CGPA " + N(g.cgpa, 2) + " — " + (g.degClass == "" ? "no class mapped" : g.degClass) + ".");

        // Readiness is the worst finding. NA is not a pass and not a block: it is a question.
        bool block = false, warn = false;
        foreach (GradFinding f in g.findings)
        {
            if (f.level == "BLOCK") block = true;
            else if (f.level == "WARN" || f.level == "NA") warn = true;
        }
        g.readiness = block ? "BLOCKED" : (warn ? "WARN" : "READY");
    }
}
