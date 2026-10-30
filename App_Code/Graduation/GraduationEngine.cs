using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre, the eligibility engine.
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
    public string code = "";     // C1 .. C10
    public string name = "";     // what the check is called on screen
    public string level = "";    // PASS | WARN | BLOCK | NA | INFO (INFO never affects readiness)
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
    public string cuSource = "NONE";          // DECLARED | NONE (STRUCTURE is no longer used for C3)
    public string cuSourceLabel = "";

    // ── Credits as C3 measures them. Every screen and every export shows THESE, never its own
    //    arithmetic on cuEarned: the bar, the "short by" column and the finding must agree. ──
    /// <summary>Recorded exemptions and credit transfers (acad_student_exemptions).</summary>
    public double cuExempt = 0;
    /// <summary>Credits counted twice because one course is on record under two spellings.</summary>
    public double cuDuplicate = 0;
    /// <summary>What C3 measures: cuEarned - cuDuplicate + cuExempt.</summary>
    public double cuCounted = 0;
    /// <summary>How far below the requirement, 0 when met or when there is no requirement.</summary>
    public double cuShort = 0;
    /// <summary>cuCounted as a share of cuRequired, 0-100, for the bar.</summary>
    public int cuPct = 0;
    /// <summary>C3's verdict (PASS | WARN | BLOCK | NA), so the bar is coloured by the engine.</summary>
    public string cuLevel = "NA";
    /// <summary>The programme's full declared minimum, before any proration.</summary>
    public double cuDeclared = 0;
    /// <summary>True when the requirement was reduced because the record starts after year 1.</summary>
    public bool cuProrated = false;

    public double cgpa = 0;
    public string degClass = "";

    public int failedPapers = 0, zeroMarks = 0, missingScores = 0, coursesTaken = 0;

    /// <summary>Retakes registered and not yet marked: the result row is blanked until they are.</summary>
    public int pendingRetakes = 0;
    public string pendingRetakeCodes = "";    // live only, for the evidence panel

    /// <summary>
    /// One course on record under two spellings of its code with the same title ("BCU 2203" and
    /// "BCU2203", both Curriculum Evaluation). duplicateFails are failing marks that a pass of the
    /// same course supersedes, or the second copy of a paper failed twice.
    /// </summary>
    public int duplicates = 0, duplicateFails = 0;
    public string duplicateNote = "";         // live only

    /// <summary>The first study year with results. Above 1 means admitted into a later year, or
    /// the earlier years are missing; a person has to say which.</summary>
    public int firstStudyYear = 1;
    /// <summary>Bit (studyyear-1)*3 + (semester-1) is set when that term has a result.</summary>
    public int termMask = 0;
    /// <summary>Passed final-year components: 1 research project, 2 school practice, 4 internship.</summary>
    public int capstoneMask = 0;
    /// <summary>Live only: final-year components registered in the portal with no mark entered.</summary>
    public string capstoneUnmarked = "";

    /// <summary>
    /// The graduation record this student had before the list was emptied on 25 September 2026
    /// (acad_graduands_prior), when it is for the programme they are on now. A record for a
    /// different programme is a previous award, not this one, and is carried in priorOtherNote.
    /// </summary>
    public string priorYear = "", priorProg = "", priorOtherNote = "";

    /// <summary>True when the term mask, retakes, duplicates and capstones above were actually
    /// read (live, or from a summary rebuilt with them). False means "unknown", never "none".</summary>
    public bool detailKnown = false;

    // C4, how many courses the programme structure requires that the student has no result
    // for at all. Only meaningful when a real specialisation resolves, hence the flag: a
    // structure belonging to somebody else's curriculum is worse than no structure.
    public bool coverageChecked = false;
    public int coverageMissing = 0, coverageRequired = 0;

    // C5, marks sitting in the portal pipeline below PUBLISHED. Not a fault of the student's,
    // but a candidate whose marks are still with a Dean is not finished being assessed.
    public int unpubEntered = 0, unpubCaptured = 0, unpubApproved = 0;
    public int UnpubTotal { get { return unpubEntered + unpubCaptured + unpubApproved; } }

    public string graduatedYear = "";          // non-empty => already on a graduation list
    public string holdReason = "", holdActor = "", holdAt = "";
    public string clearedActor = "", clearedAt = "";

    // C9, student discipline (Disciplinary module). Filled set-based by Enrich for a page of
    // candidates, or one at a time by Assess when a single student is loaded.
    public bool dcChecked = false;
    public string dcLevel = "", dcDetail = "";

    public string readiness = "READY";         // READY | WARN | BLOCKED
    /// <summary>
    /// Blocked by something other than the hold itself. The hold is a BLOCK (C8), so a held
    /// student's readiness is always BLOCKED and cannot answer "is anything still wrong?"; the
    /// Held tab's "nothing is blocking them any more" filter read it and so never matched anyone.
    /// </summary>
    public bool blockedApartFromHold = false;
    public List<GradFinding> findings = new List<GradFinding>();
}

public static class GraduationEngine
{
    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    // The year pattern every academic-year column in this database is supposed to match.
    private const string YEAR_RX = "'^[0-9]{4}/[0-9]{4}$'";

    /// <summary>
    /// A well-formed year that is also a believable one. `acad_results` holds `2202/2203`, four
    /// rows, one student, a transposed digit: and because these are CHARACTER columns it sorts
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

    /// <summary>
    /// Below this share of the (prorated) declared minimum, a credit shortfall blocks; between it
    /// and 100% it warns. Measured on 31 Oct 2026 against the 2,007 people who really graduated
    /// (1,607 records from before 25 Sep 2026 plus the 400 approved for 2026/2027): at 0.90 it
    /// blocked 43 of them, at 0.80 it blocks 17, every one a record missing a fifth or more of
    /// its courses (12 credits of 48, 44 of 131). The old rule, 90% of a summed "structure",
    /// blocked 109 and warned on 552.
    /// </summary>
    public const double CREDIT_BLOCK_RATIO = 0.80;

    /// <summary>The pass mark. At or above it a paper counts toward earned credit.</summary>
    public const int PASS_MARK = 50;

    /// <summary>Below this CGPA no award class exists in acad_gs_award, at any level.</summary>
    public const double CGPA_FLOOR = 2.0;

    /// <summary>
    /// A final-year component (research project, school practice, internship) is required of a
    /// programme's candidates only when at least this share of the programme's own graduates have
    /// passed one. On the graduates measured, each component sits at 0% or at 97-100% for every
    /// programme, so the line separates cleanly; 0.95 leaves BMC's 92% research and BED(P)'s 42%
    /// internship unchecked, which is right.
    /// </summary>
    public const double CAPSTONE_PREVALENCE = 0.95;

    /// <summary>Fewer graduates than this and a programme's pattern is not evidence of anything.</summary>
    public const int CAPSTONE_MIN_GRADUATES = 8;

    /// <summary>
    /// The curriculum era C10 is measured on and applied to: graduates and candidates whose
    /// results reach this year or later. Records that end earlier (the 2008-2011 backlog) were
    /// taught other curricula, and many of their course titles were never digitised, so a
    /// missing "research project" there says nothing.
    /// </summary>
    public const string CAPSTONE_FROM_YEAR = "2021/2022";

    /// <summary>
    /// Term-by-term completeness is checked only for programmes that run two semesters a year.
    /// Session programmes (BED(P), BED(S)) and DPE record a third term unevenly: 35-60% of their
    /// students have only term 1 in a year. A programme where at least this share of students
    /// carry a third-term result is treated as one of those.
    /// </summary>
    public const double THIRD_TERM_SHARE = 0.05;

    // The course titles that name each final-year component. Matched in SQL, case-insensitive,
    // and used by the stats rebuild, the live read and the prevalence measure alike, so the three
    // cannot disagree. "Research methods" is a taught course and is deliberately not matched.
    public const string CAP_RESEARCH_RX =
        "RESEARCH +(PROJECT|REPORT|PAPER)|DISSERTATION|THESIS|PROJECT +WORK|FINAL +(YEAR +)?PROJECT|RESEARCH *$";
    public const string CAP_SCHOOL_RX = "(SCHOOL|TEACHING) +PRACTICE";
    public const string CAP_INTERN_RX =
        "INTERNSHIP|INDUSTRIAL +(TRAINING|ATTACHMENT)|FIELD +(ATTACHMENT|PLACEMENT|WORK)|FIELDWORK|PRACTICUM";
    public const int CAP_RESEARCH = 1, CAP_SCHOOL = 2, CAP_INTERN = 4;

    public static string CapName(int bit)
    {
        return bit == CAP_RESEARCH ? "a research project or dissertation"
             : bit == CAP_SCHOOL ? "school practice"
             : "an internship or industrial training";
    }

    /// <summary>The SQL that turns one result row into its capstone bits (0 when none or not passed).</summary>
    public static string CapBitsSql(string r, string c)
    {
        return "(IF(" + r + ".score>=" + PASS_MARK + " AND " + c + ".courseName REGEXP '" + CAP_RESEARCH_RX + "'," + CAP_RESEARCH + ",0)" +
               " | IF(" + r + ".score>=" + PASS_MARK + " AND " + c + ".courseName REGEXP '" + CAP_SCHOOL_RX + "'," + CAP_SCHOOL + ",0)" +
               " | IF(" + r + ".score>=" + PASS_MARK + " AND " + c + ".courseName REGEXP '" + CAP_INTERN_RX + "'," + CAP_INTERN + ",0))";
    }

    /// <summary>
    /// The courses that are final-year components, with their bits: the titles are matched once
    /// per course (about 6,900) rather than once per result (650,000), which is what kept the
    /// summary rebuild near its old cost. Join on courseID and keep the bits where score >= 50.
    /// </summary>
    public static string CapCoursesSql()
    {
        return "SELECT courseID, (IF(courseName REGEXP '" + CAP_RESEARCH_RX + "'," + CAP_RESEARCH + ",0)" +
               " | IF(courseName REGEXP '" + CAP_SCHOOL_RX + "'," + CAP_SCHOOL + ",0)" +
               " | IF(courseName REGEXP '" + CAP_INTERN_RX + "'," + CAP_INTERN + ",0)) bits " +
               "FROM acad_course HAVING bits<>0";
    }

    /// <summary>The SQL that turns one result row into its term bit; 0 for a year or term out of range.</summary>
    public static string TermBitSql(string r)
    {
        return "(CASE WHEN " + r + ".studyyear BETWEEN 1 AND 8 AND " + r + ".semester BETWEEN 1 AND 3 " +
               "THEN 1 << ((" + r + ".studyyear-1)*3 + " + r + ".semester-1) ELSE 0 END)";
    }

    /// <summary>The bits for semesters 1 and 2 of every year from <paramref name="firstYear"/> to <paramref name="lastYear"/>.</summary>
    public static int SemesterBits(int firstYear, int lastYear)
    {
        int m = 0;
        for (int y = Math.Max(1, firstYear); y <= Math.Min(8, lastYear); y++)
            m |= 3 << ((y - 1) * 3);
        return m;
    }

    /// <summary>"year 2 semester 1, year 3 semester 2" for the semesters a mask is missing.</summary>
    private static string MissingTerms(int have, int firstYear, int lastYear)
    {
        var bits = new List<string>();
        for (int y = Math.Max(1, firstYear); y <= Math.Min(8, lastYear); y++)
            for (int s = 1; s <= 2; s++)
                if ((have & (1 << ((y - 1) * 3 + s - 1))) == 0) bits.Add("year " + y + " semester " + s);
        return string.Join(", ", bits.ToArray());
    }

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

    /// <summary>
    /// acad_gs_award.acad_level for a programme's levelCode. 4 is the postgraduate diplomas (PGD,
    /// PGDEMA, PGDPAM) and 5 the masters (MBA, MEMA, MMC, ...): that is what the programme table
    /// holds, and what the transcript and certificate printer has always used. This said the
    /// reverse until 31 Oct 2026; the two bands are numerically identical, so no class changed.
    /// </summary>
    public static string LevelName(int levelCode)
    {
        switch (levelCode)
        {
            case 1: return "Certificate";
            case 2: return "Diploma";
            case 4: return "Postgraduate";
            case 5: return "Masters";
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
    //  The source travels with the number everywhere it is displayed.
    //
    //  The requirement is the programme's DECLARED minimum (acad_programme.mincredit), prorated
    //  when the record starts after year 1. Measured on 31 Oct 2026 against 2,007 real graduates:
    //  for students who started in year 1 the median earned matches the declared figure almost
    //  exactly (BBA 119/119, BCE 176/175, DCE 115/115, HEC 30/30, BSAF 119/120), and students
    //  who joined in a later year earn it in proportion to the years they sat (BSAF 81 of
    //  120 x 2/3, BEE 140 of 183 x 3/4).
    //
    //  The summed programme STRUCTURE was the requirement for a real specialisation until then,
    //  and it is not one: 22 structures add two or more curricula together, 20 list one course
    //  twice, both religion tracks sit side by side as "core", and totals run from 72 credits
    //  (BAED Economics and Luganda) to 856 (BED(S) Agriculture). It falsely blocked 109 of the
    //  2,007 graduates. It is still shown, for reference, in C4 and the structure panel.
    private class Req
    {
        public double cu, full;
        public string src = "NONE", label = "";
        public bool prorated;
    }

    /// <summary>What the module knows about one programme, read once and kept for 30 minutes.</summary>
    private class ProgFacts
    {
        public double declared;            // acad_programme.mincredit, as recorded
        public bool credible;
        public int levelCode = 3, length = 3;
        /// <summary>Two semesters a year, so term-by-term completeness can be checked.</summary>
        public bool semesterPattern = true;
        /// <summary>Capstone bits that the programme's own graduates have, and the counts behind it.</summary>
        public int capRequired = 0, capGraduates = 0;
        public int[] capHave = new int[3];
    }
    private static Dictionary<string, ProgFacts> _facts;
    private static DateTime _factsAt = DateTime.MinValue;
    private static readonly object _factsLock = new object();

    /// <summary>
    /// Whether acad_grad_stats carries the term, retake and capstone columns yet. Until a rebuild
    /// has filled them they read as zero, and a zero term mask would read as "no semester sat":
    /// so a list built from an unfilled summary skips those checks rather than block everyone.
    /// The evidence panel reads live and always runs them.
    /// </summary>
    private static bool _statsDetailed = false;

    /// <summary>
    /// The credible range of a declared minimum for a level. Bands observed across the real
    /// programmes. They exist to reject the junk rows in acad_programme whose progcode is
    /// actually a COURSE code (BEE1101, HRP 1101, …), every one of which carries mincredit = 3,
    /// and figures like MEMA's 506, which is fourteen 4-credit courses plus a dissertation
    /// recorded at 450 credits.
    /// </summary>
    private static void CredibleBand(int levelCode, out int lo, out int hi)
    {
        switch (levelCode)
        {
            case 1: lo = 20; hi = 40; break;     // Certificate
            case 2: lo = 40; hi = 130; break;    // Diploma
            case 4: lo = 30; hi = 60; break;     // Postgraduate diploma
            case 5: lo = 40; hi = 120; break;    // Masters
            default: lo = 100; hi = 220; break;  // Bachelors
        }
    }

    private static bool DeclaredCredible(double cu, int levelCode)
    {
        int lo, hi;
        CredibleBand(levelCode, out lo, out hi);
        return cu > 0 && cu >= lo && cu <= hi;
    }

    /// <summary>The same test in SQL, over acad_programme aliased <paramref name="p"/>.</summary>
    private static string CredibleSql(string p)
    {
        var sb = new StringBuilder("(CASE IFNULL(" + p + ".levelCode,3) ");
        foreach (int lvl in new[] { 1, 2, 4, 5 })
        {
            int lo, hi; CredibleBand(lvl, out lo, out hi);
            sb.Append("WHEN " + lvl + " THEN IFNULL(" + p + ".mincredit,0) BETWEEN " + lo + " AND " + hi + " ");
        }
        int blo, bhi; CredibleBand(3, out blo, out bhi);
        sb.Append("ELSE IFNULL(" + p + ".mincredit,0) BETWEEN " + blo + " AND " + bhi + " END)");
        return sb.ToString();
    }

    /// <summary>Rounding as MySQL's ROUND does it, so the SQL counts and C# agree at .5.</summary>
    private static double Round0(double d) { return Math.Round(d, 0, MidpointRounding.AwayFromZero); }

    private static string LevelNoun(int levelCode)
    {
        switch (levelCode)
        {
            case 1: return "certificate";
            case 2: return "diploma";
            case 4: return "postgraduate diploma";
            case 5: return "masters";
            default: return "bachelors";
        }
    }

    private static void LoadFacts(MySqlConnection c)
    {
        if (_facts != null && (DateTime.UtcNow - _factsAt).TotalMinutes < 30) return;
        var f = new Dictionary<string, ProgFacts>(StringComparer.OrdinalIgnoreCase);
        bool detailed = false;
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT TRIM(progcode), IFNULL(mincredit,0), IFNULL(levelCode,3), " +
                " IFNULL(NULLIF(couselength,0),3), IFNULL(study_system,'Semester') " +
                "FROM acad_programme WHERE TRIM(IFNULL(progcode,''))<>''", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var p = new ProgFacts();
                    p.declared = Convert.ToDouble(r[1]);
                    p.levelCode = Convert.ToInt32(r[2]);
                    p.length = Math.Max(1, (int)Math.Round(Convert.ToDouble(r[3])));
                    p.credible = DeclaredCredible(p.declared, p.levelCode);
                    p.semesterPattern = !string.Equals(r[4].ToString().Trim(), "Session", StringComparison.OrdinalIgnoreCase);
                    if (!f.ContainsKey(r[0].ToString())) f.Add(r[0].ToString(), p);
                }

            // The rest comes from the summary, so it costs an index read rather than a pass over
            // acad_results. A summary not yet rebuilt with these columns says nothing here.
            using (var cmd = new MySqlCommand(
                "SELECT EXISTS(SELECT 1 FROM " + GraduationStats.TABLE + " WHERE term_mask<>0)", c))
                detailed = Convert.ToInt32(cmd.ExecuteScalar()) == 1;

            if (detailed)
            {
                // A programme whose students carry third-term results runs sessions or terms, not
                // two semesters, whatever acad_programme.study_system says (DPE and decd say
                // "Semester" and record three terms a year).
                int third = 0;
                for (int y = 1; y <= 8; y++) third |= 1 << ((y - 1) * 3 + 2);
                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(progcode), COUNT(*), SUM((term_mask & " + third + ")<>0) " +
                    "FROM " + GraduationStats.TABLE + " GROUP BY 1", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        ProgFacts p;
                        if (!f.TryGetValue(r[0].ToString(), out p)) continue;
                        int n = Convert.ToInt32(r[1]), n3 = Convert.ToInt32(r[2]);
                        if (n > 0 && n3 >= n * THIRD_TERM_SHARE) p.semesterPattern = false;
                    }

                // Final-year components the programme's own graduates have. Graduates of THIS
                // programme only, recent ones only (results to 2021/2022 or later), because a
                // component is a property of the current curriculum, not of the student number.
                string grads = "SELECT regno, progcode FROM acad_graduands";
                if (GraduationStats.PriorTableExists(c))
                    grads += " UNION SELECT regno, progcode FROM " + GraduationStats.PRIOR_TABLE;
                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(a.progcode), COUNT(*), SUM((a.cap_mask & 1)<>0), SUM((a.cap_mask & 2)<>0), " +
                    " SUM((a.cap_mask & 4)<>0) " +
                    "FROM (" + grads + ") g JOIN " + GraduationStats.TABLE + " a ON a.regno=g.regno " +
                    " AND TRIM(a.progcode)=TRIM(g.progcode) " +
                    "WHERE a.last_year >= '" + CAPSTONE_FROM_YEAR + "' GROUP BY 1", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        ProgFacts p;
                        if (!f.TryGetValue(r[0].ToString(), out p)) continue;
                        p.capGraduates = Convert.ToInt32(r[1]);
                        for (int i = 0; i < 3; i++) p.capHave[i] = Convert.ToInt32(r[2 + i]);
                        if (p.capGraduates < CAPSTONE_MIN_GRADUATES) continue;
                        for (int i = 0; i < 3; i++)
                            if (p.capHave[i] >= p.capGraduates * CAPSTONE_PREVALENCE) p.capRequired |= 1 << i;
                    }
            }
        }
        catch { /* facts are advisory; an unreadable one leaves the defaults, which check less */ }
        lock (_factsLock) { _facts = f; _statsDetailed = detailed; _factsAt = DateTime.UtcNow; }
    }

    // ── A record that starts after year 1 ───────────────────────────
    //  Either the student was admitted into a later year (a diploma holder into year 2 of BCE),
    //  or the earlier years are missing. The entry year tells them apart: someone examined first
    //  in the year they joined had no earlier year here to miss. On 31 Oct 2026 that held for
    //  all 52 later-entry students approved for 2026/2027 and 82 of 83 earlier graduates, while
    //  of 254 candidates first examined AFTER the year they joined, the missing years matched the
    //  gap exactly in 235: joined 2021, first results in year 3 in 2023/2024.
    public const string ENTRY_YEAR1 = "", ENTRY_LATER = "LATER", ENTRY_GAP = "GAP", ENTRY_UNKNOWN = "UNKNOWN";

    public static string EntryPattern(GradCandidate g)
    {
        if (g.firstStudyYear <= 1) return ENTRY_YEAR1;
        int joined, first;
        string ey = (g.entryyear ?? "").Trim(), fy = (g.firstYear ?? "").Trim();
        if (ey.Length != 4 || !int.TryParse(ey, out joined) || joined < 1990 || joined > 2035) return ENTRY_UNKNOWN;
        if (fy.Length < 4 || !int.TryParse(fy.Substring(0, 4), out first)) return ENTRY_UNKNOWN;
        return first <= joined ? ENTRY_LATER : ENTRY_GAP;
    }

    /// <summary>The same classification in SQL, over acad_grad_stats a and acad_student s.</summary>
    private static string EntryGapSql()
    {
        return "(a.first_sy > 1 AND TRIM(IFNULL(s.entryyear,'')) REGEXP '^[0-9]{4}$' " +
               " AND CAST(TRIM(s.entryyear) AS UNSIGNED) BETWEEN 1990 AND 2035 AND a.first_year IS NOT NULL " +
               " AND CAST(LEFT(a.first_year,4) AS UNSIGNED) > CAST(TRIM(s.entryyear) AS UNSIGNED))";
    }
    private static string EntryUnknownSql()
    {
        return "(a.first_sy > 1 AND NOT (TRIM(IFNULL(s.entryyear,'')) REGEXP '^[0-9]{4}$' " +
               " AND CAST(TRIM(s.entryyear) AS UNSIGNED) BETWEEN 1990 AND 2035 AND a.first_year IS NOT NULL))";
    }

    private static ProgFacts Facts(MySqlConnection c, string progcode)
    {
        LoadFacts(c);
        ProgFacts p;
        if (_facts.TryGetValue((progcode ?? "").Trim(), out p)) return p;
        return new ProgFacts { credible = false };
    }

    private static Req RequiredFor(MySqlConnection c, GradCandidate g)
    {
        ProgFacts p = Facts(c, g.progcode);
        var q = new Req();
        q.full = p.declared;
        if (!p.credible)
        {
            q.src = "NONE";
            q.label = p.declared > 0
                ? "the minimum on file (" + N(p.declared, 0) + " credits) is not credible for a " +
                  LevelNoun(g.levelCode) + " programme and needs correcting in the programme settings"
                : "no minimum is declared for this programme in the programme settings";
            return q;
        }

        // Prorate for a student admitted into a later year: they sat (L - first + 1) of L years.
        // Not when the earlier years are missing (EntryPattern GAP): those credits were owed here.
        int len = Math.Max(1, g.progLength);
        int first = EntryPattern(g) == ENTRY_GAP ? 1 : Math.Max(1, Math.Min(g.firstStudyYear, len));
        int span = len - first + 1;
        q.src = "DECLARED";
        q.prorated = span < len;
        q.cu = q.prorated ? Round0(p.declared * span / len) : p.declared;
        q.label = q.prorated
            ? "the programme's declared minimum of " + N(p.declared, 0) + " credits, prorated to years " +
              first + " to " + len + (EntryPattern(g) == ENTRY_LATER
                  ? " for a student admitted into year " + first
                  : " because the record starts in year " + first)
            : "the programme's declared minimum of " + N(p.declared, 0) + " credits";
        return q;
    }

    // ── Filters ──────────────────────────────────────────────────────
    public class GradFilter
    {
        public string acadYear = "";      // the graduation year being compiled
        public string faculty = "";
        public string department = "";
        public string programme = "";
        /// <summary>acad_campuses.ID of the student's campus (acad_student.studCampus); '' = all.</summary>
        public string campus = "";
        public string entryYear = "";
        public string finishedIn = "";    // '' = any; else the academic year they last sat

        /// <summary>
        /// "cycle" (the default) narrows to the people actually being graduated in the selected
        /// year, those who finished in it or the year before. "all" opens it to everyone who
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

        // ── Export presentation. These never touch the WHERE clause; they decide how the rows
        //    that came back are ordered and grouped in the file. ──────────────────────────
        /// <summary>'' = none; 'prog' = one section per programme; 'fac' = per faculty.</summary>
        public string groupBy = "prog";
        /// <summary>name | regno | cgpa | class | prog</summary>
        public string orderBy = "name";
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
        int cmp;
        if (f.campus != "" && int.TryParse(f.campus, out cmp)) { w.Append(" AND s.studCampus=@campus "); p["@campus"] = cmp; }
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
               " IFNULL(a.first_year,''), IFNULL(a.last_year,''), " +
               " a.first_sy, a.term_mask, a.pend_rt, a.dup_n, a.dup_cu, a.dup_fail, a.cap_mask " +
               w + order + " LIMIT " + f.size + " OFFSET " + ((f.page < 1 ? 0 : f.page - 1) * f.size);
    }

    /// <summary>
    /// A page of candidates, fully assessed. Two phases: identify and page the students
    /// (0.010s), then aggregate their results by explicit id list (0.082s for 100). The
    /// alternative: aggregating the whole results table, costs 10.3s and is what makes this
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
            GraduationStats.EnsureSchema(c);
            var p = new Dictionary<string, object>();
            using (var cmd = new MySqlCommand(CandidateSql(scope, f, p, true), c))
            {
                foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                object o = cmd.ExecuteScalar();
                total = o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
            }
            if (total == 0) return list;

            // Read before the reader opens: one connection carries one open reader at a time.
            bool detailed = StatsDetailed(c);
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
                        g.firstStudyYear = Math.Max(1, RI(r, 19)); g.termMask = RI(r, 20);
                        g.pendingRetakes = RI(r, 21); g.duplicates = RI(r, 22);
                        g.cuDuplicate = RD(r, 23); g.duplicateFails = RI(r, 24); g.capstoneMask = RI(r, 25);
                        g.detailKnown = detailed;
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
        c.programme = f.programme; c.campus = f.campus; c.entryYear = f.entryYear; c.finishedIn = f.finishedIn;
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

        // C1, the graduation records as they stood before the list was emptied on 25 Sep 2026.
        foreach (PriorRecord pr in PriorRecords(c, inList))
        {
            GradCandidate g;
            if (byReg.TryGetValue(pr.regno, out g)) ApplyPrior(g, pr);
        }

        // C3, recorded exemptions and credit transfers, one query for the page.
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT regno, SUM(credit_units) FROM acad_student_exemptions " +
                "WHERE is_active=1 AND regno IN (" + inList + ") GROUP BY regno", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    GradCandidate g;
                    if (byReg.TryGetValue(RS(r, 0), out g)) g.cuExempt = RD(r, 1);
                }
        }
        catch { /* the exemptions table is optional; no table means no exemptions */ }

        // C4, courses mapped to the specialisation with no result, for the students on this page
        // whose specialisation is real. Set-based: one query for the page, not one per student.
        try
        {
            using (var cmd = new MySqlCommand(CoverageSql("s.regno IN (" + inList + ")"), c))
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

        // C5, marks still in the portal pipeline.
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

        // C9: disciplinary restrictions, one query for the whole page (only students with
        // something to report come back). DcClearance fails open on its own errors.
        var regnos = new List<string>();
        foreach (GradCandidate g in list) { regnos.Add(g.regno); g.dcChecked = true; }
        foreach (KeyValuePair<string, DcClearanceResult> kv in DcClearance.CheckMany(c, regnos))
        {
            GradCandidate g;
            if (byReg.TryGetValue(kv.Key, out g)) DcClearance.ForGraduation(kv.Value, out g.dcLevel, out g.dcDetail);
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

    /// <summary>Whether the summary carries the term, retake, duplicate and capstone columns yet.</summary>
    public static bool StatsDetailed(MySqlConnection c)
    {
        LoadFacts(c);
        return _statsDetailed;
    }

    // ── Earlier graduation records ───────────────────────────────────
    //  acad_graduands was emptied at 12:10 on 25 September 2026, 23 minutes after a reset that
    //  had deliberately kept its 1,607 historical rows. Nothing about the module's own records
    //  depends on why. What matters is that without them C1 could not see that someone had
    //  graduated: 190 of the people in the 2026/2027 queue already had, 243 holds were placed
    //  with the reason "she graduated" or "he graduated", and 10 were approved a second time.
    //  The dump taken before the wipe is loaded, read-only, into acad_graduands_prior.
    public class PriorRecord { public string regno = "", acadYear = "", progcode = ""; }

    /// <summary>Earlier records for the given quoted IN list; empty when the table is absent.</summary>
    public static List<PriorRecord> PriorRecords(MySqlConnection c, string inList)
    {
        var list = new List<PriorRecord>();
        if (!GraduationStats.PriorTableExists(c)) return list;
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT regno, TRIM(IFNULL(acadyear,'')), TRIM(IFNULL(progcode,'')) FROM " +
                GraduationStats.PRIOR_TABLE + " WHERE regno IN (" + inList + ") ORDER BY acadyear DESC", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var p = new PriorRecord();
                    p.regno = RS(r, 0); p.acadYear = RS(r, 1); p.progcode = RS(r, 2);
                    list.Add(p);
                }
        }
        catch { }
        return list;
    }

    /// <summary>The same, for one student.</summary>
    public static List<PriorRecord> PriorRecords(MySqlConnection c, GradCandidate g)
    {
        return PriorRecords(c, "'" + MySqlHelper.EscapeString(g.regno) + "'");
    }

    /// <summary>
    /// An earlier record for the programme the student is on now means they have graduated from
    /// it. One for a different programme is a previous award: seven PGD graduates of 2024/2025
    /// are MBA candidates now, under the same student number, and that is a new graduation.
    /// </summary>
    public static void ApplyPrior(GradCandidate g, PriorRecord pr)
    {
        string yr = pr.acadYear == "" ? "an earlier year" : pr.acadYear;
        if (string.Equals(pr.progcode, (g.progcode ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
        {
            if (g.priorYear == "") { g.priorYear = yr; g.priorProg = pr.progcode; }
        }
        else
        {
            string note = "Graduated in " + yr + " from " + (pr.progcode == "" ? "another programme" : pr.progcode) + ".";
            if (g.priorOtherNote.IndexOf(note, StringComparison.Ordinal) < 0)
                g.priorOtherNote = (g.priorOtherNote + " " + note).Trim();
        }
    }

    /// <summary>
    /// C4: core courses mapped to the student's specialisation that have no result. A mapped
    /// course counts as covered when the student holds it, the course it was merged into, or a
    /// course with the same title under another code, and each code is counted once however many
    /// curricula list it. Even so it stays informational: the mappings put options side by side
    /// (both religion tracks as "core"), and on the 270 graduates of 2025/2026 onward with a real
    /// specialisation it still finds a "gap" in 141.
    /// </summary>
    public static string CoverageSql(string where)
    {
        // Three NOT EXISTS rather than one with ORs: the first two seek (regno, courseid) and stop,
        // so the title scan only runs for courses not found by code. 155 ms to 110 for 50 students.
        return
            "SELECT s.regno, COUNT(DISTINCT pc.course_code) req, " +
            " COUNT(DISTINCT CASE WHEN " +
            "   NOT EXISTS(SELECT 1 FROM acad_results r WHERE r.regno=s.regno AND r.courseid=pc.course_code) " +
            "   AND NOT EXISTS(SELECT 1 FROM acad_results r WHERE r.regno=s.regno AND r.courseid=ac.merged_into) " +
            "   AND NOT EXISTS(SELECT 1 FROM acad_results r JOIN acad_course rc ON rc.courseID=r.courseid " +
            "     WHERE r.regno=s.regno AND ac.courseName IS NOT NULL AND rc.courseName=ac.courseName) " +
            "   THEN pc.course_code END) missing " +
            "FROM acad_student s " +
            "JOIN acad_programmecourses pc ON pc.progcode=s.progid " +
            " AND pc.specialisation_id=CAST(s.specialisation AS UNSIGNED) AND pc.status='Active' " +
            " AND pc.course_type='CORE' " +
            "LEFT JOIN acad_course ac ON ac.courseID=pc.course_code " +
            "WHERE " + where + " AND TRIM(IFNULL(s.specialisation,'')) NOT IN ('','0','13') " +
            "GROUP BY s.regno";
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

    /// <summary>
    /// The automated checks as SQL over the summary (a = acad_grad_stats, s = acad_student,
    /// p = acad_programme, x = the exemptions join, mp = the marks pipeline), so the Centre's
    /// counts are decided by the same rules as the list. Assess is the authority; this is its
    /// set-based shadow and must change with it. What the summary cannot see, the coverage note
    /// (C4, informational anyway) and a disciplinary warning short of a bar, it does not count.
    /// </summary>
    private class CheckSql
    {
        public string fails = "0", retakes = "0", terms = "0", earlier = "0", credits = "0", capstone = "0", cgpa = "0";
        public string warn = "0";
        public string Block
        {
            get
            {
                return "(" + fails + " OR " + retakes + " OR " + terms + " OR " + earlier + " OR " + credits +
                       " OR " + capstone + " OR " + cgpa + ")";
            }
        }
    }

    private static CheckSql BuildCheckSql(MySqlConnection c)
    {
        LoadFacts(c);
        var k = new CheckSql();
        bool detail = _statsDetailed;
        string counted = "(a.cu_earned" + (detail ? " - a.dup_cu" : "") + " + IFNULL(x.cu,0))";
        string first = "LEAST(GREATEST(a.first_sy,1), a.plen)";
        // Prorated for a later-year admission, never when the earlier years are missing.
        string prorateFrom = "IF(" + EntryGapSql() + ", 1, " + first + ")";
        string required = detail
            ? "ROUND(CAST(IFNULL(p.mincredit,0) AS DECIMAL(10,4)) * (a.plen - " + prorateFrom + " + 1) / a.plen)"
            : "IFNULL(p.mincredit,0)";
        string credible = CredibleSql("p");

        k.fails = detail ? "(a.fails - a.dup_fail) > 0" : "a.fails > 0";
        k.cgpa = "(a.cgpa > 0 AND a.cgpa < " + N(CGPA_FLOOR, 1) + ")";
        k.credits = "(" + credible + " AND " + counted + " < " + N(CREDIT_BLOCK_RATIO, 2) + " * " + required + ")";

        if (detail)
        {
            k.retakes = "a.pend_rt > 0";
            k.earlier = EntryGapSql();
            // Missing semesters, for two-semester programmes: the bits for semesters 1 and 2 of
            // every year from the first to the last, 3 << 3(y-1) summed, is 3(8^L - 8^(f-1))/7.
            var notSem = new List<string>();
            foreach (KeyValuePair<string, ProgFacts> kv in _facts)
                if (!kv.Value.semesterPattern) notSem.Add("'" + MySqlHelper.EscapeString(kv.Key) + "'");
            string expected = "((3 * ((1 << (3*a.plen)) - (1 << (3*(" + first + "-1))))) DIV 7)";
            k.terms = "(a.plen BETWEEN 1 AND 8" +
                      (notSem.Count > 0 ? " AND TRIM(a.progcode) NOT IN (" + string.Join(",", notSem.ToArray()) + ")" : "") +
                      " AND (a.term_mask & " + expected + ") <> " + expected + ")";

            var caps = new List<string>();
            foreach (int bit in new[] { CAP_RESEARCH, CAP_SCHOOL, CAP_INTERN })
            {
                var progs = new List<string>();
                foreach (KeyValuePair<string, ProgFacts> kv in _facts)
                    if ((kv.Value.capRequired & bit) != 0) progs.Add("'" + MySqlHelper.EscapeString(kv.Key) + "'");
                if (progs.Count > 0)
                    caps.Add("(TRIM(a.progcode) IN (" + string.Join(",", progs.ToArray()) + ") AND (a.cap_mask & " + bit + ") = 0)");
            }
            if (caps.Count > 0)
                k.capstone = "(a.last_year >= '" + CAPSTONE_FROM_YEAR + "' AND (" + string.Join(" OR ", caps.ToArray()) + "))";
        }

        // Needs checking: a zero or a missing mark, no CGPA, marks still in the pipeline, an entry
        // year that cannot place a later-year start, a credit requirement that cannot be assessed
        // or is short but above the block line, or a course on record twice.
        k.warn = "(a.zero_marks > 0 OR (a.no_score" + (detail ? " - a.pend_rt" : "") + ") > 0 OR a.cgpa <= 0 " +
                 "OR mp.regno IS NOT NULL OR NOT " + credible + " OR " + counted + " < " + required +
                 (detail ? " OR " + EntryUnknownSql() + " OR a.dup_n > 0" : "") + ")";
        return k;
    }

    /// <summary>The joins BuildCheckSql's expressions expect: exemptions (x) and the marks pipeline (mp).</summary>
    private static string CheckJoins(MySqlConnection c)
    {
        string x = GraduationStats.TableExists(c, "acad_student_exemptions")
            ? " LEFT JOIN (SELECT regno, SUM(credit_units) cu FROM acad_student_exemptions " +
              "            WHERE is_active=1 GROUP BY regno) x ON x.regno=a.regno "
            : " LEFT JOIN (SELECT NULL regno, 0 cu) x ON 1=0 ";
        // 220 rows across 151 students on 31 Oct 2026, indexed on mark_stage, same collation.
        string mp =
            " LEFT JOIN (SELECT regno FROM campus_dynamics_portal.acad_course_registration " +
            "            WHERE mark_stage IN ('ENTERED','CAPTURED','APPROVED') GROUP BY regno) mp ON mp.regno=a.regno ";
        return x + mp;
    }

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
            GraduationStats.EnsureSchema(c);
            CheckSql k = BuildCheckSql(c);

            // The candidacy predicate, shared with the list so the two cannot disagree: and
            // now a stored column rather than an aggregate. See GraduationStats.
            string from =
                " FROM " + GraduationStats.TABLE + " a " +
                " JOIN acad_student s ON s.regno=a.regno " +
                " JOIN acad_programme p ON p.progcode=s.progid " +
                " LEFT JOIN (SELECT DISTINCT regno, 1 held FROM acad_grad_review " +
                "            WHERE verdict='HELD' AND superseded_at IS NULL) h ON h.regno=a.regno " +
                " LEFT JOIN (" + DcClearance.GraduationBlockSql + ") dcb ON dcb.regno=a.regno " +
                CheckJoins(c) +
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
            string blocked = "(" + k.Block + " OR h.held IS NOT NULL OR dcb.regno IS NOT NULL)";
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*), SUM(" + k.fails + "), SUM(" + k.cgpa + "), SUM(h.held IS NOT NULL), " +
                " SUM(" + blocked + "), SUM(NOT " + blocked + " AND " + k.warn + "), SUM(dcb.regno IS NOT NULL), " +
                " SUM(" + k.retakes + "), SUM(" + k.terms + "), SUM(" + k.credits + "), SUM(" + k.capstone + "), " +
                " SUM(a.zero_marks>0 OR a.no_score>0) " +
                where + notListed, c))
            {
                foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        o.candidates = RI(r, 0);
                        o.held = RI(r, 3);
                        o.blocked = RI(r, 4);
                        int needsChecking = RI(r, 5);
                        o.ready = o.candidates - o.blocked - needsChecking;
                        if (o.ready < 0) o.ready = 0;
                        // The blocker table overlaps on purpose, it answers "how many would this
                        // one problem release", not "how do they partition".
                        AddGc(o.blockers, "Failed papers (1-49)", RI(r, 1));
                        AddGc(o.blockers, "Retakes awaiting a mark", RI(r, 7));
                        AddGc(o.blockers, "A semester with no results", RI(r, 8));
                        AddGc(o.blockers, "Credits below " + N(CREDIT_BLOCK_RATIO * 100, 0) + "% of the minimum", RI(r, 9));
                        AddGc(o.blockers, "Research, school practice or internship missing", RI(r, 10));
                        AddGc(o.blockers, "CGPA below " + N(CGPA_FLOOR, 1), RI(r, 2));
                        AddGc(o.blockers, "Marks of zero, or no mark at all", RI(r, 11));
                        AddGc(o.blockers, "On hold", o.held);
                        AddGc(o.blockers, "Disciplinary sanction", RI(r, 6));
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
                " COUNT(*), SUM(a.on_list), SUM(h.held IS NOT NULL), " +
                " SUM(a.on_list=0 AND (" + k.Block + " OR dcb.regno IS NOT NULL)) " +
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

                // Approved again after graduating from the same programme in an earlier year.
                if (GraduationStats.PriorTableExists(c))
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(DISTINCT g.regno), GROUP_CONCAT(DISTINCT CONCAT(g.regno,' (',TRIM(pr.acadyear),')') " +
                        "  ORDER BY g.regno SEPARATOR ', ') " +
                        "FROM acad_graduands g JOIN " + GraduationStats.PRIOR_TABLE + " pr ON pr.regno=g.regno " +
                        " AND TRIM(pr.progcode)=TRIM(g.progcode) AND TRIM(pr.acadyear)<>TRIM(g.acadyear) " +
                        "JOIN acad_student s ON s.regno=g.regno JOIN acad_programme p ON p.progcode=s.progid " +
                        "WHERE 1=1" + (f.acadYear == "" ? "" : " AND g.acadyear=@ay") + extra, c))
                    {
                        if (f.acadYear != "") cmd.Parameters.AddWithValue("@ay", f.acadYear);
                        foreach (KeyValuePair<string, object> kv in p)
                            if (kv.Key != "@cy1" && kv.Key != "@cy0") cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                        using (var rr = cmd.ExecuteReader())
                            if (rr.Read() && !rr.IsDBNull(0) && Convert.ToInt32(rr[0]) > 0)
                            {
                                int n = Convert.ToInt32(rr[0]);
                                o.integrity.Add(n + (n == 1 ? " student on this list had" : " students on this list had") +
                                    " already graduated from the same programme in an earlier year, on the records " +
                                    "kept from before 25 September 2026: " + rr[1] + ". Take them off this list " +
                                    "unless the earlier record is the mistake.");
                            }
                    }

                // Results filed under a year that cannot exist. The module now ignores such a
                // year when it ranks or compares, so nobody is hidden by one, but the mark is
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

    private static string Plural(int n, string one, string many)
    { return n + " " + (n == 1 ? one : many); }

    /// <summary>Active exempted / transferred credits for one student; 0 if none or the table is absent.</summary>
    public static double ExemptedCredits(MySqlConnection c, string regno)
    {
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT IFNULL(SUM(credit_units),0) FROM acad_student_exemptions WHERE regno=@r AND is_active=1", c))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? 0 : Convert.ToDouble(v);
            }
        }
        catch { return 0; }
    }

    /// <summary>
    /// Runs every check and settles the readiness. BLOCK anywhere means not ready; the order
    /// of the findings is the order a human should read them, so the membership facts (already
    /// graduated, already held) come first, the list must never offer an action that
    /// contradicts something further down the panel.
    /// </summary>
    public static void Assess(MySqlConnection c, GradCandidate g)
    {
        g.findings.Clear();
        ProgFacts pf = Facts(c, g.progcode);
        if (g.firstStudyYear < 1) g.firstStudyYear = 1;
        Req req = RequiredFor(c, g);
        g.cuRequired = req.cu; g.cuSource = req.src; g.cuSourceLabel = req.label;
        g.cuDeclared = req.full; g.cuProrated = req.prorated;
        g.degClass = AwardFor(c, g.cgpa, g.levelCode);

        // C1: already graduated. The live list first; then the records kept from before the list
        // was emptied on 25 Sep 2026, which count only for the programme the student is on now.
        if (g.graduatedYear != "" && g.priorYear != "" && g.priorYear != g.graduatedYear)
            Add(g, "C1", "Already graduated", "BLOCK",
                "On the " + g.graduatedYear + " graduation list, and had already graduated from " + g.priorProg +
                " in " + g.priorYear + " on the records kept from before 25 September 2026. One of the two is a mistake.");
        else if (g.graduatedYear != "")
            Add(g, "C1", "Already graduated", "BLOCK",
                "This student is already on the " + g.graduatedYear + " graduation list.");
        else if (g.priorYear != "")
            Add(g, "C1", "Already graduated", "BLOCK",
                "Graduated from " + g.priorProg + " in " + g.priorYear + ", on the graduation records kept from before " +
                "the list was emptied on 25 September 2026. Approve only to restore that record, for " + g.priorYear + ".");
        else
            Add(g, "C1", "Already graduated", "PASS",
                "Not on any graduation list." + (g.priorOtherNote == "" ? "" : " " + g.priorOtherNote + " That was a different award."));

        // C8: an open hold
        if (g.holdReason != "")
            Add(g, "C8", "On hold", "BLOCK",
                "Put on hold by " + g.holdActor + " on " + g.holdAt + ": " + g.holdReason);

        // C9: student discipline. A graduation bar, suspension, expulsion, or a results
        // cancellation the marks office has not yet carried out, blocks and cannot be
        // overridden (GraduationService.Clear refuses it). An open case without a bar warns.
        if (!g.dcChecked)
        {
            DcClearance.ForGraduation(DcClearance.Check(c, null, g.regno), out g.dcLevel, out g.dcDetail);
            g.dcChecked = true;
        }
        if (g.dcLevel == "BLOCK")
            Add(g, "C9", "Disciplinary", "BLOCK", g.dcDetail);
        else if (g.dcLevel == "WARN")
            Add(g, "C9", "Disciplinary", "WARN", g.dcDetail);
        else
            Add(g, "C9", "Disciplinary", "PASS", "No disciplinary case or sanction stands in the way.");

        // C2, outstanding papers.
        //
        //  A mark of exactly zero is treated as a GAP, not a failure, and that distinction was
        //  measured rather than assumed. Of the 522 students who really graduated in 2024/2025,
        //  15 carry a zero and only 2 carry a mark between 1 and 49. Across the whole results
        //  table there are 5,380 zeros against 6,793 real fails. Blocking on zero would have
        //  stopped fifteen legitimate graduands; blocking on 1-49 stops two, and both of those
        //  genuinely sat and failed.
        //
        //  A registered retake BLOCKS. Registering one blanks the result row until the retake is
        //  marked (RetakeService), so before this the student who failed and registered the retake
        //  read better than one who failed and did not: one "no score" warning instead of a block.
        //
        //  A course on record twice under two spellings of its code, with the same title, is one
        //  course: a fail that a pass of the same course supersedes is not counted.
        int fails = g.detailKnown ? Math.Max(0, g.failedPapers - g.duplicateFails) : g.failedPapers;
        int retakes = g.detailKnown ? g.pendingRetakes : 0;
        int noScore = Math.Max(0, g.missingScores - retakes);
        if (fails > 0 || retakes > 0)
        {
            var bits = new List<string>();
            if (fails > 0)
                bits.Add(Plural(fails, "paper is", "papers are") + " marked between 1 and 49, below the pass mark.");
            if (retakes > 0)
                bits.Add(Plural(retakes, "retake is", "retakes are") + " registered and not yet marked" +
                         (g.pendingRetakeCodes == "" ? "" : " (" + g.pendingRetakeCodes + ")") + ".");
            Add(g, "C2", "Outstanding papers", "BLOCK", string.Join(" ", bits.ToArray()));
        }
        else if (g.zeroMarks > 0)
            Add(g, "C2", "Outstanding papers", "WARN",
                g.zeroMarks + " course" + (g.zeroMarks == 1 ? " is" : "s are") +
                " recorded as zero. That is usually a paper never marked rather than a paper failed, check before approving.");
        else if (noScore > 0)
            Add(g, "C2", "Outstanding papers", "WARN",
                noScore + " course" + (noScore == 1 ? " has" : "s have") + " no score at all.");
        else
            Add(g, "C2", "Outstanding papers", "PASS", "Every course carries a passing mark.");

        // C2, continued: the same course on record twice. Not a fault of the student's, but the
        // transcript prints both and the CGPA counts both, so the record is fixed before approval.
        if (g.detailKnown && g.duplicates > 0)
            Add(g, "C2", "Course recorded twice", "WARN",
                (g.duplicateNote != ""
                    ? g.duplicateNote
                    : Plural(g.duplicates, "course is", "courses are") + " on record under two spellings of the same code.") +
                " The credits are counted once here, but the transcript and the CGPA count both until the record is fixed.");

        // C3, credits. Recorded exemptions and credit transfers (Student Exemptions screen,
        // acad_student_exemptions) count toward the total, so an upgrader admitted into a later
        // year no longer shows a false shortfall (Examinations Board, Matter 17). They are named
        // in the finding so the Registrar can see what was counted.
        double dupCu = g.detailKnown ? g.cuDuplicate : 0;
        g.cuCounted = Math.Max(0, g.cuEarned - dupCu) + g.cuExempt;
        var extras = new List<string>();
        if (g.cuExempt > 0) extras.Add("including " + N(g.cuExempt, 0) + " exempted or transferred");
        if (dupCu > 0) extras.Add(N(dupCu, 0) + " counted twice on the record left out");
        string note = extras.Count == 0 ? "" : " (" + string.Join("; ", extras.ToArray()) + ")";
        g.cuShort = 0; g.cuPct = 0;
        if (g.cuSource == "NONE")
        {
            g.cuLevel = "NA";
            Add(g, "C3", "Credits earned", "NA",
                "Cannot be assessed: " + req.label + ". " + N(g.cuCounted, 0) + " credits earned" + note + ".");
        }
        else
        {
            g.cuPct = g.cuRequired > 0 ? (int)Math.Min(100, Math.Floor(g.cuCounted * 100 / g.cuRequired)) : 100;
            if (g.cuCounted >= g.cuRequired)
            {
                g.cuLevel = "PASS";
                Add(g, "C3", "Credits earned", "PASS",
                    N(g.cuCounted, 0) + " of " + N(g.cuRequired, 0) + " credits" + note + ", against " + req.label + ".");
            }
            else
            {
                g.cuShort = g.cuRequired - g.cuCounted;
                g.cuLevel = g.cuCounted < CREDIT_BLOCK_RATIO * g.cuRequired ? "BLOCK" : "WARN";
                Add(g, "C3", "Credits earned", g.cuLevel,
                    "Short by " + N(g.cuShort, 0) + " credits, " + N(g.cuCounted, 0) + " of " +
                    N(g.cuRequired, 0) + note + ", against " + req.label + "." +
                    (g.cuLevel == "BLOCK"
                        ? " Below " + N(CREDIT_BLOCK_RATIO * 100, 0) + "% of the minimum, which on this record means whole courses are missing."
                        : (fails == 0 && retakes == 0 && noScore == 0 && g.zeroMarks == 0
                            ? " Every course on record is passed: if none is missing, check the minimum in the programme " +
                              "settings and the credit units of the courses taken."
                            : "")));
            }
        }

        // C10, the final-year components this programme's own graduates all have: a research
        // project, school practice, an internship. Asked of a programme only where at least 95%
        // of its recent graduates have passed one, so it measures the curriculum as it is taught,
        // not a list written here. These are the commonest reasons reviewers put on holds by hand.
        if (g.detailKnown && pf.capRequired != 0 &&
            string.CompareOrdinal(g.lastYear ?? "", CAPSTONE_FROM_YEAR) >= 0)
        {
            var missing = new List<string>();
            var evidence = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                int bit = 1 << i;
                if ((pf.capRequired & bit) == 0) continue;
                if ((g.capstoneMask & bit) == 0)
                {
                    missing.Add(CapName(bit));
                    evidence.Add(pf.capHave[i] + " of " + pf.capGraduates);
                }
            }
            if (missing.Count > 0)
                Add(g, "C10", "Final-year components", "BLOCK",
                    "No pass on record for " + string.Join(" or ", missing.ToArray()) + ". " +
                    (missing.Count == 1 ? "It is" : "Each is") + " part of every " + g.progcode +
                    " degree: passed by " + string.Join(" and ", evidence.ToArray()) +
                    " of the programme's recent graduates. " +
                    (g.capstoneUnmarked != ""
                        ? g.capstoneUnmarked + (g.capstoneUnmarked.IndexOf(',') > 0 ? " are" : " is") +
                          " registered with no mark entered yet."
                        : "A mark still in the pipeline (C5) counts once it is published."));
            else
                Add(g, "C10", "Final-year components", "PASS",
                    "Has passed every final-year component " + g.progcode + " graduates have.");
        }

        // C4, coverage against the programme structure. Informational only (31 Oct 2026): the
        // structures map options side by side, both religion tracks as "core" for instance, and
        // even matching by title a "gap" shows on 141 of 270 real recent graduates. It is the
        // pointer to the structure panel; C3 and C10 are the checks that decide.
        if (!g.coverageChecked)
            Add(g, "C4", "Programme coverage", "INFO",
                "Not checked: this student carries no real specialisation, so there is no " +
                "curriculum mapping to compare their courses with.");
        else if (g.coverageMissing > 0)
            Add(g, "C4", "Programme coverage", "INFO",
                g.coverageMissing + " of " + g.coverageRequired + " core courses mapped to this specialisation have no " +
                "result under that code, its merged code or the same title. For reference: the mappings list options " +
                "side by side, so this alone is not a shortfall. The structure is below.");
        else
            Add(g, "C4", "Programme coverage", "PASS",
                "All " + g.coverageRequired + " core courses mapped to this specialisation have a result.");

        // C5, marks still moving through the pipeline.
        if (g.UnpubTotal > 0)
        {
            var bits = new List<string>();
            if (g.unpubEntered > 0) bits.Add(g.unpubEntered + " with the lecturer");
            if (g.unpubCaptured > 0) bits.Add(g.unpubCaptured + " with the head of department");
            if (g.unpubApproved > 0) bits.Add(g.unpubApproved + " approved but not published");
            Add(g, "C5", "Marks not yet published", "WARN",
                g.UnpubTotal + " course" + (g.UnpubTotal == 1 ? " is" : "s are") +
                " still in the marks pipeline: " + string.Join(", ", bits.ToArray()) +
                ". Their final position may change.");
        }
        else
            Add(g, "C5", "Marks not yet published", "PASS", "Nothing outstanding in the marks pipeline.");

        // C6, the programme completed: the final year reached (the candidacy rule itself, restated
        // as evidence) and, for two-semester programmes, every semester from the first year on the
        // record to the last. Measured against the 1,373 real graduates of semester programmes:
        // 5 lack the final semester and 8 have a gap, and the DPE share of both was why three-term
        // programmes are left out. A record that starts after year 1 is a question, not a fault.
        int len = Math.Max(1, g.progLength);
        string span = g.firstYear != "" ? " (" + g.firstYear + " to " + g.lastYear + ")" : "";
        string entry = g.detailKnown ? EntryPattern(g) : ENTRY_YEAR1;
        int fsy = Math.Min(g.firstStudyYear, len);
        string earlier = entry != ENTRY_GAP ? "" :
            "No results for year" + (fsy > 2 ? "s 1 to " + (fsy - 1) : " 1") + ", although the student joined in " +
            g.entryyear.Trim() + " and was first examined in " + g.firstYear + ", in year " + fsy +
            ". Those years are missing from the record, so the credits are measured against the whole programme.";
        string admitted = entry == ENTRY_LATER ? " Admitted into year " + fsy + " in " + g.entryyear.Trim() + "." : "";
        string unknown = entry != ENTRY_UNKNOWN ? "" :
            "Nothing before year " + fsy + ", and the entry year on the record (" +
            (g.entryyear.Trim() == "" ? "blank" : g.entryyear.Trim()) + ") cannot say whether the student was admitted " +
            "into year " + fsy + ". If they were not, the earlier years are missing. Credits are measured against years " +
            fsy + " to " + len + ".";
        if (g.maxStudyYear < len)
            Add(g, "C6", "Programme completed", "BLOCK",
                "Only reached year " + g.maxStudyYear + " of a " + len + "-year programme.");
        else if (g.detailKnown && pf.semesterPattern && len <= 8)
        {
            int want = SemesterBits(fsy, len);
            int finalBit = 1 << ((len - 1) * 3 + 1);
            if ((g.termMask & finalBit) == 0)
                Add(g, "C6", "Programme completed", "BLOCK",
                    "Has not sat the final semester: no results for year " + len + " semester 2" +
                    ((g.termMask & want & ~finalBit) != (want & ~finalBit)
                        ? ", nor for " + MissingTerms(g.termMask | finalBit, fsy, len) : "") + "." +
                    (earlier == "" ? "" : " " + earlier));
            else if ((g.termMask & want) != want)
                Add(g, "C6", "Programme completed", "BLOCK",
                    "A whole semester has no results: " + MissingTerms(g.termMask, fsy, len) +
                    ". If marks for it exist they are in the wrong term or not yet published." +
                    (earlier == "" ? "" : " " + earlier));
            else if (earlier != "")
                Add(g, "C6", "Programme completed", "BLOCK", earlier);
            else if (unknown != "")
                Add(g, "C6", "Programme completed", "WARN",
                    "Every semester from year " + fsy + " to year " + len + " has results" + span + ". " + unknown);
            else
                Add(g, "C6", "Programme completed", "PASS",
                    "Every semester " + (fsy > 1 ? "from year " + fsy + " to year " + len : "of a " + len + "-year programme") +
                    " has results" + span + "." + admitted);
        }
        else if (earlier != "")
            Add(g, "C6", "Programme completed", "BLOCK", earlier);
        else if (unknown != "")
            Add(g, "C6", "Programme completed", "WARN",
                "Reached year " + g.maxStudyYear + " of a " + len + "-year programme" + span + ". " + unknown);
        else
            Add(g, "C6", "Programme completed", "PASS",
                "Reached year " + g.maxStudyYear + " of a " + len + "-year programme" + span + "." + admitted +
                (g.detailKnown && !pf.semesterPattern ? " Terms are not checked one by one: this programme runs sessions or three terms a year." : ""));

        // C7, CGPA floor
        if (g.cgpa <= 0)
            Add(g, "C7", "Class of award", "WARN", "CGPA could not be computed from the marks on record.");
        else if (g.cgpa < CGPA_FLOOR)
            Add(g, "C7", "Class of award", "BLOCK",
                "CGPA " + N(g.cgpa, 2) + " is below " + N(CGPA_FLOOR, 1) +
                ", the floor below which acad_gs_award maps no class at any level.");
        else
            Add(g, "C7", "Class of award", "PASS",
                "CGPA " + N(g.cgpa, 2) + ", " + (g.degClass == "" ? "no class mapped" : g.degClass) + ".");

        // Readiness is the worst finding. NA is not a pass and not a block: it is a question.
        // INFO is neither: it is there to be read, and decides nothing.
        bool block = false, warn = false, other = false;
        foreach (GradFinding f in g.findings)
        {
            if (f.level == "BLOCK") { block = true; if (f.code != "C8") other = true; }
            else if (f.level == "WARN" || f.level == "NA") warn = true;
        }
        g.readiness = block ? "BLOCKED" : (warn ? "WARN" : "READY");
        g.blockedApartFromHold = other;
    }
}
