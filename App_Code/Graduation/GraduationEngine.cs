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
        return cgpa >= 2.0 ? "" : "Below award";
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
                "WHERE pc.status='Active' GROUP BY 1, 2 HAVING n >= 20 AND cu > 0", c))
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
        public string state = "pending";  // pending | held | listed | all
        public string readiness = "";     // '' | ready | warn | blocked
        public string search = "";
        public string sort = "regno";
        public int page = 1;
        public int size = 50;
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
        var w = new StringBuilder();
        w.Append(" FROM acad_student s JOIN acad_programme p ON p.progcode=s.progid ");
        w.Append(" WHERE EXISTS (SELECT 1 FROM acad_results r WHERE r.regno=s.regno ");
        w.Append("   AND r.studyyear >= IFNULL(NULLIF(p.couselength,0),3)) ");

        if (f.state == "pending")
            w.Append(" AND NOT EXISTS (SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno) ");
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
        if (f.finishedIn != "")
        {
            w.Append(" AND (SELECT MAX(r3.acad) FROM acad_results r3 WHERE r3.regno=s.regno " +
                     " AND r3.acad REGEXP " + YEAR_RX + ")=@fin ");
            p["@fin"] = f.finishedIn;
        }
        if (f.search != "")
        {
            w.Append(" AND (s.regno LIKE @q OR CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,'')) LIKE @q) ");
            p["@q"] = "%" + f.search + "%";
        }
        w.Append(scope.ProgFilter("s", "progid"));

        if (countOnly) return "SELECT COUNT(*) " + w;

        string order;
        switch (f.sort)
        {
            case "name": order = " ORDER BY nm "; break;
            case "prog": order = " ORDER BY p.progname, s.regno "; break;
            case "entry": order = " ORDER BY s.entryyear DESC, s.regno "; break;
            default: order = " ORDER BY s.regno "; break;
        }
        return "SELECT s.regno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) nm, " +
               " TRIM(s.progid) progid, IFNULL(p.progname,'') progname, IFNULL(p.faculty_code,'') fac, " +
               " IFNULL(p.department_id,0) dep, IFNULL(s.entryyear,'') ey, IFNULL(s.specialisation,'') sp, " +
               " IFNULL(p.levelCode,3) lvl, IFNULL(NULLIF(p.couselength,0),3) plen " +
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

    /// <summary>Phase 2: the results arithmetic for exactly the students on this page.</summary>
    private static void Enrich(MySqlConnection c, List<GradCandidate> list, Dictionary<string, GradCandidate> byReg)
    {
        string inList = InList(list);

        // Results aggregate. Credits count a course ONCE and only when passed; acad_results
        // carries UNIQUE(regno,courseid) so a repeat overwrites rather than duplicating, but
        // the GROUP BY makes that explicit rather than relying on it.
        using (var cmd = new MySqlCommand(
            "SELECT r.regno, MAX(r.studyyear) maxsy, COUNT(*) taken, " +
            " SUM(CASE WHEN r.score>=50 THEN IFNULL(r.CreditUnits,0) ELSE 0 END) cu_pass, " +
            " SUM(r.score>0 AND r.score<50) failed, SUM(r.score=0) zeros, SUM(r.score IS NULL) noscore, " +
            " SUM(IFNULL(r.CreditUnits,0)*IFNULL(r.gradept,0)) gpnum, SUM(IFNULL(r.CreditUnits,0)) gpden, " +
            " MIN(CASE WHEN r.acad REGEXP " + YEAR_RX + " THEN r.acad END) firstyr, " +
            " MAX(CASE WHEN r.acad REGEXP " + YEAR_RX + " THEN r.acad END) lastyr " +
            "FROM acad_results r WHERE r.regno IN (" + inList + ") GROUP BY r.regno", c))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                GradCandidate g;
                if (!byReg.TryGetValue(RS(r, 0), out g)) continue;
                g.maxStudyYear = RI(r, 1); g.coursesTaken = RI(r, 2);
                g.cuEarned = RD(r, 3); g.failedPapers = RI(r, 4);
                g.zeroMarks = RI(r, 5); g.missingScores = RI(r, 6);
                double num = RD(r, 7), den = RD(r, 8);
                g.cgpa = den > 0 ? Math.Round(num / den, 2) : 0;
                g.firstYear = RS(r, 9); g.lastYear = RS(r, 10);
            }

        // Already on a graduation list?
        using (var cmd = new MySqlCommand(
            "SELECT regno, acadyear FROM acad_graduands WHERE regno IN (" + inList + ")", c))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                GradCandidate g;
                if (byReg.TryGetValue(RS(r, 0), out g)) g.graduatedYear = RS(r, 1);
            }

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
    }
    public class GC2 { public string name = ""; public int count = 0; }

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

        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();

            // The candidacy predicate, shared with the list so the two cannot disagree.
            string cand =
                " FROM acad_student s JOIN acad_programme p ON p.progcode=s.progid " +
                " WHERE EXISTS (SELECT 1 FROM acad_results r WHERE r.regno=s.regno " +
                "   AND r.studyyear >= IFNULL(NULLIF(p.couselength,0),3)) ";
            var p = new Dictionary<string, object>();
            var extra = new StringBuilder();
            if (f.faculty != "") { extra.Append(" AND p.faculty_code=@fac "); p["@fac"] = f.faculty; }
            int dep;
            if (f.department != "" && int.TryParse(f.department, out dep))
            { extra.Append(" AND p.department_id=@dep "); p["@dep"] = dep; }
            if (f.programme != "") { extra.Append(" AND s.progid=@prog "); p["@prog"] = f.programme; }
            extra.Append(scope.ProgFilter("s", "progid"));
            string where = cand + extra;
            string notListed = " AND NOT EXISTS (SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno) ";

            // One pass over the outstanding candidates, bucketed by what is wrong with them.
            // The per-student figures are computed in a derived table rather than by joining
            // acad_results, which would multiply a student by their number of courses.
            using (var cmd = new MySqlCommand(
                    // Counted so the buckets PARTITION the pool: a student who both fails a
                    // paper and sits below the CGPA floor is one blocked student, not two, and
                    // Ready is what is left after blocked and warned are taken out.
                    "SELECT COUNT(*) total, SUM(fails>0) blk_fail, SUM(cgpa<2.0) blk_cgpa, " +
                    " SUM(is_held) held, " +
                    " SUM(fails>0 OR cgpa<2.0 OR is_held) blocked_any, " +
                    " SUM(NOT (fails>0 OR cgpa<2.0 OR is_held) AND (zeros>0 OR nulls>0)) warn_marks " +
                    "FROM ( SELECT s.regno, " +
                    "  (SELECT COUNT(*) FROM acad_results r2 WHERE r2.regno=s.regno AND r2.score>0 AND r2.score<50) fails, " +
                    "  (SELECT COUNT(*) FROM acad_results r3 WHERE r3.regno=s.regno AND r3.score=0) zeros, " +
                    "  (SELECT COUNT(*) FROM acad_results r4 WHERE r4.regno=s.regno AND r4.score IS NULL) nulls, " +
                    "  IFNULL((SELECT SUM(IFNULL(r5.CreditUnits,0)*IFNULL(r5.gradept,0))/NULLIF(SUM(IFNULL(r5.CreditUnits,0)),0) " +
                    "          FROM acad_results r5 WHERE r5.regno=s.regno),0) cgpa, " +
                    "  EXISTS(SELECT 1 FROM acad_grad_review v WHERE v.regno=s.regno AND v.verdict='HELD' AND v.superseded_at IS NULL) is_held " +
                    where + notListed + " ) z", c))
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
                        // The blocker table overlaps on purpose - a student can appear in two
                        // rows of it - because the question it answers is "how many candidates
                        // would this one problem release", not "how do they partition".
                        AddGc(o.blockers, "Failed papers (1-49)", blkFail);
                        AddGc(o.blockers, "CGPA below 2.0", blkCgpa);
                        AddGc(o.blockers, "Marks of zero, or no mark at all", warnMarks);
                        AddGc(o.blockers, "Held by a reviewer", o.held);
                    }
            }

            // On the list for the selected year, and overall.
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM acad_graduands g JOIN acad_student s ON s.regno=g.regno " +
                "JOIN acad_programme p ON p.progcode=s.progid WHERE 1=1 " +
                (f.acadYear == "" ? "" : " AND g.acadyear=@ay ") + extra, c))
            {
                if (f.acadYear != "") cmd.Parameters.AddWithValue("@ay", f.acadYear);
                foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                object v = cmd.ExecuteScalar();
                o.listed = v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
            }

            // Per-programme progress. A Dean's week before Senate is spent in this table.
            using (var cmd = new MySqlCommand(
                "SELECT TRIM(s.progid) pc, MAX(IFNULL(p.progname,'')) pn, MAX(IFNULL(p.faculty_code,'')) fc, " +
                " COUNT(*) cand, " +
                " SUM(EXISTS(SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno)) listed, " +
                " SUM(EXISTS(SELECT 1 FROM acad_grad_review v WHERE v.regno=s.regno AND v.verdict='HELD' AND v.superseded_at IS NULL)) held, " +
                " SUM((SELECT COUNT(*) FROM acad_results r2 WHERE r2.regno=s.regno AND r2.score>0 AND r2.score<50)>0) blocked " +
                where + " GROUP BY pc ORDER BY cand DESC LIMIT 60", c))
            {
                foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        var pp = new ProgProgress();
                        pp.progcode = RS(r, 0); pp.progname = RS(r, 1); pp.faculty = RS(r, 2);
                        pp.candidates = RI(r, 3); pp.listed = RI(r, 4); pp.held = RI(r, 5); pp.blocked = RI(r, 6);
                        o.programmes.Add(pp);
                    }
            }

            // Data-integrity notices. Reported, never auto-corrected: taking a name off a
            // graduation list is a Registrar's decision, not a script's.
            try
            {
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM acad_graduands g JOIN acad_student s ON s.regno=g.regno " +
                    "JOIN acad_programme p ON p.progcode=s.progid " +
                    "WHERE IFNULL((SELECT MAX(r.studyyear) FROM acad_results r WHERE r.regno=g.regno),0) " +
                    " < IFNULL(NULLIF(p.couselength,0),3)" + extra, c))
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
            string lvl = (g.cuSource == "STRUCTURE" && g.cuEarned < g.cuRequired * 0.90) ? "BLOCK" : "WARN";
            Add(g, "C3", "Credits earned", lvl,
                "Short by " + N(shortBy, 0) + " credits — " + N(g.cuEarned, 0) + " of " +
                N(g.cuRequired, 0) + ", against " + req.label + ".");
        }

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
        else if (g.cgpa < 2.0)
            Add(g, "C7", "Class of award", "BLOCK",
                "CGPA " + N(g.cgpa, 2) + " is below 2.0, the floor for any award.");
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
