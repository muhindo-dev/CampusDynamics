using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.Services;
using System.Web.UI;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  RESULTS EXPORT CENTRE — management module to export marks + results
//  statistics for external representation (Senate / Council / NCHE /
//  external examiners). Scope-enforced via MarksScopeResolver.
//
//  Authoritative source = campus_dynamics.acad_results (published,
//  GPA-bearing). Grading follows NCHE 2015 (acad_gs_details / acad_gs_award);
//  CGPA via the stored fn acad_CGPAFinder (retake-aware, matches transcripts).
//
//  PERF: CHAR join keys use plain '=' (NOT TRIM) — '='/DISTINCT ignore
//  trailing pad and stay index-backed. Dirty years filtered by regex,
//  legacy grades normalised via score bands.
//
//  Preview + statistics are served via AJAX PageMethods; file downloads
//  run as a classic postback (Response.End) reading the same config JSON,
//  so preview and export always share identical scope.
// =====================================================================
public partial class COOPERP_NewScreens_ResultsExporter : Page
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static string Conn() { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    protected void Page_Load(object sender, EventArgs e)
    {
        // Export runs on a form post carrying hdnExportAction (independent of
        // IsPostBack detection, since the master form disables ViewState).
        string act = Request.Form["hdnExportAction"];
        if (!string.IsNullOrEmpty(act))
        {
            string cfg = Request.Form["hdnConfig"] ?? "";
            try { RunExport(act, cfg); }
            catch (System.Threading.ThreadAbortException) { /* expected after Response.End */ }
            catch (Exception ex) { ShowError(ex.Message); }
        }
    }

    // ================================================================
    //  Config model
    // ================================================================
    private class Cfg
    {
        public string mode;      // marks | summary | course_stats | prog_stats
        public string source;    // published | approved | captured | entered
        public string acadYear;  public string semester;  public string studyYear;
        public string faculty;   public string department; public string programme;  public string course;
        public string retakes;   // all | exclude | only
        public string grade;     public string passfail;   // all | pass | fail
        public string format;    // xlsx | csv
        public List<int> hidden; // column indices to omit from exports
    }

    private static string Sv(Dictionary<string, object> d, string k)
    {
        object v; if (d != null && d.TryGetValue(k, out v) && v != null) return Convert.ToString(v).Trim(); return "";
    }
    private static Cfg Parse(string json)
    {
        Cfg c = new Cfg();
        c.mode = "marks"; c.source = "published"; c.acadYear = ""; c.semester = ""; c.studyYear = ""; c.faculty = "";
        c.department = ""; c.programme = ""; c.course = ""; c.retakes = "all"; c.grade = ""; c.passfail = "all"; c.format = "xlsx";
        c.hidden = new List<int>();
        if (string.IsNullOrEmpty(json)) return c;
        try
        {
            Dictionary<string, object> d = Json.Deserialize<Dictionary<string, object>>(json);
            string m = Sv(d, "mode"); if (m != "") c.mode = m;
            string src = Sv(d, "source"); if (src != "") c.source = src.ToLowerInvariant();
            c.acadYear = Sv(d, "acadYear"); c.semester = Sv(d, "semester"); c.studyYear = Sv(d, "studyYear");
            c.faculty = Sv(d, "faculty"); c.department = Sv(d, "department"); c.programme = Sv(d, "programme"); c.course = Sv(d, "course");
            string rt = Sv(d, "retakes"); if (rt != "") c.retakes = rt;
            c.grade = Sv(d, "grade"); string pf = Sv(d, "passfail"); if (pf != "") c.passfail = pf;
            string ft = Sv(d, "format"); if (ft != "") c.format = ft;
            object hv;
            if (d.TryGetValue("hidden", out hv) && (hv is System.Collections.IEnumerable) && !(hv is string))
                foreach (object o in (System.Collections.IEnumerable)hv)
                { int hi; if (int.TryParse(Convert.ToString(o), out hi)) c.hidden.Add(hi); }
        }
        catch { }
        return c;
    }

    // Build the WHERE clause + parameters shared by every published-results query.
    private static string BuildWhere(Cfg c, MarksScope scope, Dictionary<string, object> p)
    {
        StringBuilder w = new StringBuilder(" WHERE 1=1 ");
        if (c.acadYear != "") { w.Append(" AND r.acad=@acadYear "); p["@acadYear"] = c.acadYear; }
        else w.Append(" AND r.acad REGEXP '^[0-9]{4}/[0-9]{4}$' ");
        if (c.semester != "") { w.Append(" AND r.semester=@semester "); p["@semester"] = c.semester; }
        if (c.studyYear != "") { w.Append(" AND r.studyyear=@studyYear "); p["@studyYear"] = c.studyYear; }
        if (c.faculty != "") { w.Append(" AND p.faculty_code=@faculty "); p["@faculty"] = c.faculty; }
        int depId;
        if (c.department != "" && int.TryParse(c.department, out depId)) { w.Append(" AND p.department_id=@department "); p["@department"] = depId; }
        if (c.programme != "") { w.Append(" AND r.progid=@programme "); p["@programme"] = c.programme; }
        if (c.course != "") { w.Append(" AND r.courseid=@course "); p["@course"] = c.course; }
        if (c.retakes == "exclude") w.Append(" AND IFNULL(r.is_retake,0)=0 ");
        else if (c.retakes == "only") w.Append(" AND r.is_retake=1 ");
        if (c.grade != "") { w.Append(" AND r.grade=@grade "); p["@grade"] = c.grade; }
        if (c.passfail == "pass") w.Append(" AND r.score>=50 ");
        else if (c.passfail == "fail") w.Append(" AND r.score<50 ");
        w.Append(scope.ProgFilter("r", "progid"));
        return w.ToString();
    }

    // Which lookup tables a given query actually reads. Every one of these is an eq_ref
    // probe PER ROW: on a single academic year that is ~59,000 probes each, and the stats
    // aggregate used to pay for three of them while selecting nothing from any.
    [Flags]
    private enum J { None = 0, Student = 1, Prog = 2, Faculty = 4, Course = 8 }

    // ---- Source resolution (published vs. staged/unpublished pipeline) -----------
    //  Published  -> campus_dynamics.acad_results (authoritative, GPA-bearing).
    //  Approved / Captured / Entered -> campus_dynamics_portal.acad_course_registration
    //  at that mark_stage. Those rows carry raw marks only (provisional_total_marks)
    //  and NO grade/grade-point/credit-units, so we derive them on the fly (NCHE 2015)
    //  and shape the result into the exact same column names acad_results exposes, so
    //  every downstream query (grid, stats, breakdown) is source-agnostic.
    private static string StageFor(string source)
    {
        switch ((source ?? "").ToLowerInvariant())
        {
            case "entered":  return "ENTERED";
            case "captured": return "CAPTURED";
            case "approved": return "APPROVED";
            default:         return null;   // published
        }
    }
    private static bool IsPublished(Cfg c) { return StageFor(c.source) == null; }

    // NCHE 2015 grade letter / grade point derived from a score SQL expression.
    private static string GradeCase(string s)
    {
        return "CASE WHEN " + s + ">=80 THEN 'A' WHEN " + s + ">=75 THEN 'B+' WHEN " + s + ">=70 THEN 'B' " +
               "WHEN " + s + ">=65 THEN 'C+' WHEN " + s + ">=60 THEN 'C' WHEN " + s + ">=55 THEN 'D+' " +
               "WHEN " + s + ">=50 THEN 'D' ELSE 'F' END";
    }
    private static string GptCase(string s)
    {
        return "CASE WHEN " + s + ">=80 THEN 5.0 WHEN " + s + ">=75 THEN 4.5 WHEN " + s + ">=70 THEN 4.0 " +
               "WHEN " + s + ">=65 THEN 3.5 WHEN " + s + ">=60 THEN 3.0 WHEN " + s + ">=55 THEN 2.5 " +
               "WHEN " + s + ">=50 THEN 2.0 ELSE 0.0 END";
    }

    // FROM clause for the selected source, exposing the SAME logical columns as acad_results:
    // regno, courseid, progid, acad, semester, studyyear, score, grade, gradept, gpa, CreditUnits, is_retake.
    private static string FromFor(Cfg c, J need)
    {
        // A faculty or department filter is written against acad_programme, so that join has
        // to be present whether or not the SELECT list mentions it; and acad_faculty is only
        // reachable through it.
        if (c.faculty != "" || c.department != "") need |= J.Prog;
        if ((need & J.Faculty) != 0) need |= J.Prog;

        StringBuilder j = new StringBuilder();
        if ((need & J.Student) != 0) j.Append("LEFT JOIN acad_student s ON s.regno=r.regno ");
        if ((need & J.Prog) != 0)    j.Append("LEFT JOIN acad_programme p ON p.progcode=r.progid ");
        if ((need & J.Faculty) != 0) j.Append("LEFT JOIN acad_faculty f ON f.faculty_code=p.faculty_code ");
        if ((need & J.Course) != 0)  j.Append("LEFT JOIN acad_course c ON c.courseID=r.courseid ");

        string stage = StageFor(c.source);
        if (stage == null) return " FROM acad_results r " + j;   // published
        const string SC = "cr.provisional_total_marks";
        return
            " FROM ( SELECT cr.regno, cr.courseID courseid, cr.prog_id progid, cr.acad_year acad, cr.semester, " +
            "   (SELECT MIN(pc.study_year) FROM acad_programmecourses pc WHERE pc.progcode=cr.prog_id AND pc.course_code=cr.courseID) studyyear, " +
            "   " + SC + " score, " + GradeCase(SC) + " grade, " + GptCase(SC) + " gradept, " + GptCase(SC) + " gpa, " +
            "   IFNULL(ac.CreditUnit,0) CreditUnits, " +
            "   IF(cr.registration_type='RETAKE' OR cr.retake_registration_id IS NOT NULL,1,0) is_retake " +
            "  FROM campus_dynamics_portal.acad_course_registration cr " +
            "  LEFT JOIN acad_course ac ON ac.courseID=cr.courseID " +
            "  WHERE cr.mark_stage='" + stage + "' AND cr.provisional_total_marks IS NOT NULL ) r " + j;
    }
    private static string SourceLabel(string source)
    {
        switch ((source ?? "").ToLowerInvariant())
        {
            case "entered":  return "Entered (Lecturer) — pending capture";
            case "captured": return "Captured (HOD) — pending approval";
            case "approved": return "Approved (Dean) — pending publish";
            default:         return "Published results";
        }
    }

    // Derived NCHE grade bands (clean; ignores legacy dirty grades).
    private const string GRADE_COLS =
        " SUM(r.score>=80) g_A, SUM(r.score BETWEEN 75 AND 79) g_Bp, SUM(r.score BETWEEN 70 AND 74) g_B, " +
        " SUM(r.score BETWEEN 65 AND 69) g_Cp, SUM(r.score BETWEEN 60 AND 64) g_C, " +
        " SUM(r.score BETWEEN 55 AND 59) g_Dp, SUM(r.score BETWEEN 50 AND 54) g_D, SUM(r.score<50) g_F ";

    // ================================================================
    //  Result grid (columns + rows) — identical for preview and export
    // ================================================================
    private class Grid { public string[] Cols; public List<string[]> Rows; public long Total; }

    private static void AddP(MySqlCommand cmd, Dictionary<string, object> p)
    { foreach (KeyValuePair<string, object> kv in p) if (!cmd.Parameters.Contains(kv.Key)) cmd.Parameters.AddWithValue(kv.Key, kv.Value); }

    private static string RS(MySqlDataReader r, int i) { return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)); }
    private static long RL(MySqlDataReader r, int i) { return r.IsDBNull(i) ? 0L : Convert.ToInt64(r.GetValue(i)); }
    private static double RD(MySqlDataReader r, int i) { return r.IsDBNull(i) ? 0.0 : Convert.ToDouble(r.GetValue(i)); }

    private static string StudentName()
    { return "TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,'')))"; }

    private static Grid BuildGrid(MySqlConnection conn, Cfg c, MarksScope scope, int limit)
    {
        Grid g = new Grid(); g.Rows = new List<string[]>();
        Dictionary<string, object> p = new Dictionary<string, object>();
        string where = BuildWhere(c, scope, p);
        string lim = limit > 0 ? (" LIMIT " + limit) : "";
        string from =
            c.mode == "summary"      ? FromFor(c, J.Student | J.Prog | J.Faculty) :
            c.mode == "course_stats" ? FromFor(c, J.Course) :
            c.mode == "prog_stats"   ? FromFor(c, J.Prog | J.Faculty) :
                                       FromFor(c, J.Student | J.Prog | J.Course);

        if (c.mode == "summary")
        {
            // Published uses the authoritative CGPA fn; unpublished stages have no stored
            // GPA, so CGPA is the indicative credit-weighted grade-point over the scope.
            string cgpaExpr = IsPublished(c)
                ? "ROUND(acad_CGPAFinder(r.regno),2)"
                : "ROUND(SUM(r.CreditUnits*r.gradept)/NULLIF(SUM(r.CreditUnits),0),2)";
            g.Cols = new string[] { "Reg No", "Student Name", "Programme", "Faculty", "Level", "Year Courses", "Year Credits", "Passed", "Failed", "Mean Score", "CGPA", "Class" };
            string sql = "SELECT r.regno, MAX(" + StudentName() + ") nm, MAX(p.progname) prog, MAX(f.faculty_name) fac, " +
                " MAX(IFNULL(p.levelCode,3)) lvl, COUNT(*) crs, ROUND(SUM(r.CreditUnits),0) cu, " +
                " SUM(r.score>=50) passed, SUM(r.score<50) failed, ROUND(AVG(r.score),1) mean, " +
                " " + cgpaExpr + " cgpa " + from + where + " GROUP BY r.regno ORDER BY cgpa DESC" + lim;
            using (MySqlCommand cmd = new MySqlCommand(sql, conn)) { AddP(cmd, p); using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read()) {
                    int lvl = (int)RL(r, 4); double cgpa = RD(r, 10);
                    g.Rows.Add(new string[] { RS(r,0), RS(r,1), RS(r,2), RS(r,3), LevelName(lvl), RL(r,5).ToString(), RL(r,6).ToString(), RL(r,7).ToString(), RL(r,8).ToString(), Num(RD(r,9),1), Num(cgpa,2), AwardClass(cgpa, lvl) });
                } }
        }
        else if (c.mode == "course_stats")
        {
            g.Cols = new string[] { "Course Code", "Course Name", "Enrolled", "Passed", "Pass Rate %", "Mean Score", "A", "B+", "B", "C+", "C", "D+", "D", "F" };
            string sql = "SELECT r.courseid, MAX(c.courseName) cn, COUNT(*) enr, SUM(r.score>=50) pas, " +
                " ROUND(100*SUM(r.score>=50)/COUNT(*),1) pr, ROUND(AVG(r.score),1) mean, " + GRADE_COLS +
                from + where + " GROUP BY r.courseid ORDER BY enr DESC" + lim;
            using (MySqlCommand cmd = new MySqlCommand(sql, conn)) { AddP(cmd, p); using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                    g.Rows.Add(new string[] { RS(r,0), RS(r,1), RL(r,2).ToString(), RL(r,3).ToString(), Num(RD(r,4),1), Num(RD(r,5),1),
                        RL(r,6).ToString(), RL(r,7).ToString(), RL(r,8).ToString(), RL(r,9).ToString(), RL(r,10).ToString(), RL(r,11).ToString(), RL(r,12).ToString(), RL(r,13).ToString() }); }
        }
        else if (c.mode == "prog_stats")
        {
            g.Cols = new string[] { "Programme", "Faculty", "Students", "Records", "Mean Score", "Pass Rate %", "Mean GP", "A", "B+", "B", "C+", "C", "D+", "D", "F" };
            string sql = "SELECT MAX(p.progname) prog, MAX(f.faculty_name) fac, COUNT(DISTINCT r.regno) st, COUNT(*) rec, " +
                " ROUND(AVG(r.score),1) mean, ROUND(100*SUM(r.score>=50)/COUNT(*),1) pr, ROUND(AVG(r.gradept),2) mgp, " + GRADE_COLS +
                from + where + " GROUP BY r.progid ORDER BY st DESC" + lim;
            using (MySqlCommand cmd = new MySqlCommand(sql, conn)) { AddP(cmd, p); using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                    g.Rows.Add(new string[] { RS(r,0), RS(r,1), RL(r,2).ToString(), RL(r,3).ToString(), Num(RD(r,4),1), Num(RD(r,5),1), Num(RD(r,6),2),
                        RL(r,7).ToString(), RL(r,8).ToString(), RL(r,9).ToString(), RL(r,10).ToString(), RL(r,11).ToString(), RL(r,12).ToString(), RL(r,13).ToString(), RL(r,14).ToString() }); }
        }
        else // marks (detailed)
        {
            g.Cols = new string[] { "Reg No", "Student Name", "Programme", "Study Year", "Semester", "Course Code", "Course Name", "CU", "Score", "Grade", "Grade Point", "Sem GPA", "Retake" };
            string sql = "SELECT r.regno, " + StudentName() + " nm, p.progname prog, r.studyyear sy, r.semester sem, r.courseid, " +
                " c.courseName cn, r.CreditUnits cu, r.score, r.grade, r.gradept, r.gpa, IFNULL(r.is_retake,0) rt " +
                from + where + " ORDER BY r.regno, r.semester, r.courseid" + lim;
            using (MySqlCommand cmd = new MySqlCommand(sql, conn)) { AddP(cmd, p); using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                    g.Rows.Add(new string[] { RS(r,0), RS(r,1), RS(r,2), RS(r,3), RS(r,4), RS(r,5), RS(r,6), Num(RD(r,7),1), RL(r,8).ToString(), RS(r,9), Num(RD(r,10),1), Num(RD(r,11),2), RL(r,12)==1?"RETAKE":"" }); }
        }
        if (limit <= 0) g.Total = g.Rows.Count;   // export fetches all rows in scope
        return g;
    }

    // Distinct total for the mode (still used by print/export, which build stats anyway).
    private static long TotalFor(string mode, Stats s)
    {
        if (mode == "summary") return s.students;
        if (mode == "course_stats") return s.courses;
        if (mode == "prog_stats") return s.programmes;
        return s.records;
    }

    /// <summary>
    /// The one number the preview needs, without building the analytics that used to carry it.
    ///
    /// COUNT(DISTINCT regno) over a year measured 0.76s; the same answer as a derived
    /// GROUP BY measured 0.10s, because the grouping runs in index order instead of
    /// building a distinct-value temp table. For detailed marks it is a plain COUNT(*),
    /// which the index answers in about a millisecond.
    /// </summary>
    private static long CountFor(MySqlConnection conn, Cfg c, MarksScope scope)
    {
        Dictionary<string, object> p = new Dictionary<string, object>();
        string where = BuildWhere(c, scope, p);
        string from = FromFor(c, J.None);
        string sql;
        if (c.mode == "summary")           sql = "SELECT COUNT(*) FROM (SELECT 1" + from + where + " GROUP BY r.regno) x";
        else if (c.mode == "course_stats") sql = "SELECT COUNT(*) FROM (SELECT 1" + from + where + " GROUP BY r.courseid) x";
        else if (c.mode == "prog_stats")   sql = "SELECT COUNT(*) FROM (SELECT 1" + from + where + " GROUP BY r.progid) x";
        else                               sql = "SELECT COUNT(*)" + from + where;
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddP(cmd, p);
                object o = cmd.ExecuteScalar();
                return o == null || o == DBNull.Value ? 0L : Convert.ToInt64(o);
            }
        }
        catch { return 0L; }
    }

    // Staged marks-submission pipeline for the selected scope (portal table).
    private static SubStats BuildSubmissionStats(MySqlConnection conn, Cfg c, MarksScope scope)
    {
        SubStats ss = new SubStats();
        Dictionary<string, object> p = new Dictionary<string, object>();
        StringBuilder w = new StringBuilder(" WHERE 1=1 ");
        if (c.acadYear != "") { w.Append(" AND cr.acad_year=@acadYear "); p["@acadYear"] = c.acadYear; }
        if (c.semester != "") { w.Append(" AND cr.semester=@semester "); p["@semester"] = c.semester; }
        if (c.faculty != "") { w.Append(" AND p.faculty_code=@faculty "); p["@faculty"] = c.faculty; }
        int depId;
        if (c.department != "" && int.TryParse(c.department, out depId)) { w.Append(" AND p.department_id=@department "); p["@department"] = depId; }
        if (c.programme != "") { w.Append(" AND cr.prog_id=@programme "); p["@programme"] = c.programme; }
        w.Append(scope.ProgFilter("cr", "prog_id"));
        // The submission pipeline (not-entered/entered/…) counts only active (onboarded)
        // students, so it matches the stage consoles. The RESULTS export itself is left
        // unfiltered — external reps (Senate/Council/NCHE) need finalists/alumni too.
        // The JOIN form, not the EXISTS one: as the driver of an aggregate, EXISTS makes
        // MySQL walk the whole registration table and probe the user table once per row.
        // See ActiveStudentFilter.Join — 19.65s vs 0.178s for identical numbers elsewhere.
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT COUNT(*) total, " +
                " SUM(TRIM(IFNULL(cr.mark_stage,'')) IN ('','NOT_ENTERED')) ne, " +
                " SUM(cr.mark_stage='ENTERED') en, SUM(cr.mark_stage='CAPTURED') ca, " +
                " SUM(cr.mark_stage='APPROVED') ap, SUM(cr.mark_stage='PUBLISHED') pu " +
                "FROM campus_dynamics_portal.acad_course_registration cr " +
                ActiveStudentFilter.Join("cr", "regno") +
                "LEFT JOIN acad_programme p ON p.progcode=cr.prog_id" + w.ToString(), conn))
            {
                AddP(cmd, p);
                using (MySqlDataReader r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        ss.total = RL(r, 0); ss.notEntered = RL(r, 1); ss.entered = RL(r, 2);
                        ss.captured = RL(r, 3); ss.approved = RL(r, 4); ss.published = RL(r, 5);
                    }
            }
        }
        catch { }
        ss.pctPublished = ss.total > 0 ? Math.Round(ss.published * 100.0 / ss.total, 1) : 0;
        return ss;
    }

    // Drop user-deselected columns from the grid (export/print only).
    private static void ApplyHidden(Grid g, List<int> hidden)
    {
        if (hidden == null || hidden.Count == 0 || g.Cols == null) return;
        HashSet<int> h = new HashSet<int>(hidden);
        List<int> keep = new List<int>();
        for (int i = 0; i < g.Cols.Length; i++) if (!h.Contains(i)) keep.Add(i);
        if (keep.Count == g.Cols.Length || keep.Count == 0) return;   // never drop everything
        string[] nc = new string[keep.Count];
        for (int i = 0; i < keep.Count; i++) nc[i] = g.Cols[keep[i]];
        g.Cols = nc;
        for (int rr = 0; rr < g.Rows.Count; rr++)
        {
            string[] row = g.Rows[rr]; string[] nr = new string[keep.Count];
            for (int i = 0; i < keep.Count; i++) nr[i] = keep[i] < row.Length ? row[keep[i]] : "";
            g.Rows[rr] = nr;
        }
    }

    // ================================================================
    //  Statistics (headline + grade distribution) over the filtered set
    // ================================================================
    public class GC { public string name; public long count; }
    public class Perf { public string regno; public string name; public string programme; public double gpa; public double mean; }
    public class Stats
    {
        public long records; public long students; public long courses; public long programmes;
        public double meanScore; public double passRate; public double meanGp;
        public List<GC> grades; public List<GC> classes;
        public List<Perf> topPerformers; public List<Perf> bottomPerformers;
    }
    // Staged marks-submission pipeline (from the portal registration table).
    public class SubStats
    {
        public long total; public long notEntered; public long entered; public long captured; public long approved; public long published;
        public double pctPublished;
    }

    private static Stats BuildStats(MySqlConnection conn, Cfg c, MarksScope scope)
    {
        Dictionary<string, object> p = new Dictionary<string, object>();
        string where = BuildWhere(c, scope, p);
        Stats s = new Stats(); s.grades = new List<GC>(); s.classes = new List<GC>();
        long gA = 0, gBp = 0, gB = 0, gCp = 0, gC = 0, gDp = 0, gD = 0, gF = 0;

        // COUNT(DISTINCT regno) is deliberately NOT here. Students have four figures more
        // cardinality than courses or programmes, and asking for the three distincts together
        // measured 1.14s against 0.35s for these two; the student count falls out of the
        // per-student pass below for nothing, because that pass already groups by regno.
        // This query reads no column outside acad_results, so it joins nothing.
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) rec, COUNT(DISTINCT r.courseid) crs, COUNT(DISTINCT r.progid) pg, " +
            " ROUND(AVG(r.score),1) mean, ROUND(100*SUM(r.score>=50)/COUNT(*),1) pr, ROUND(AVG(r.gradept),2) mgp, " +
            GRADE_COLS + FromFor(c, J.None) + where, conn))
        {
            AddP(cmd, p);
            using (MySqlDataReader r = cmd.ExecuteReader())
                if (r.Read())
                {
                    s.records = RL(r, 0); s.courses = RL(r, 1); s.programmes = RL(r, 2);
                    s.meanScore = RD(r, 3); s.passRate = RD(r, 4); s.meanGp = RD(r, 5);
                    gA = RL(r, 6); gBp = RL(r, 7); gB = RL(r, 8); gCp = RL(r, 9); gC = RL(r, 10); gDp = RL(r, 11); gD = RL(r, 12); gF = RL(r, 13);
                }
        }
        AddGC(s.grades, "A", gA); AddGC(s.grades, "B+", gBp); AddGC(s.grades, "B", gB); AddGC(s.grades, "C+", gCp);
        AddGC(s.grades, "C", gC); AddGC(s.grades, "D+", gDp); AddGC(s.grades, "D", gD); AddGC(s.grades, "F", gF);

        // One per-student pass (streamed) → class-of-degree buckets + top/bottom
        // performers. Indicative CGPA = weighted GPA over results in the selected scope.
        long c1 = 0, c2 = 0, c3 = 0, c4 = 0, c5 = 0;
        s.topPerformers = new List<Perf>(); s.bottomPerformers = new List<Perf>();
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT r.regno, MAX(" + StudentName() + ") nm, MAX(p.progname) prog, " +
            " SUM(r.CreditUnits*r.gradept)/NULLIF(SUM(r.CreditUnits),0) g, ROUND(AVG(r.score),1) mn " +
            FromFor(c, J.Student | J.Prog) + where + " GROUP BY r.regno", conn))
        {
            AddP(cmd, p);
            using (MySqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                {
                    s.students++;                      // every student in scope, GPA or not
                    if (r.IsDBNull(3)) continue;       // no credits — cannot be classed or ranked
                    double gp = RD(r, 3);
                    if (gp >= 4.4) c1++; else if (gp >= 3.6) c2++; else if (gp >= 2.8) c3++; else if (gp >= 2.0) c4++; else c5++;
                    Perf pf = new Perf(); pf.regno = RS(r, 0); pf.name = RS(r, 1); pf.programme = RS(r, 2); pf.gpa = gp; pf.mean = RD(r, 4);
                    InsertCapped(s.topPerformers, pf, 8, true);
                    InsertCapped(s.bottomPerformers, pf, 8, false);
                }
        }
        AddGC(s.classes, "First", c1); AddGC(s.classes, "Second Upper", c2);
        AddGC(s.classes, "Second Lower", c3); AddGC(s.classes, "Pass", c4); AddGC(s.classes, "Fail", c5);
        return s;
    }
    /// <summary>
    /// Keeps the best (or worst) <paramref name="cap"/> entries seen so far. Insertion into an
    /// already-ordered list of at most eight, rather than appending and re-sorting the list on
    /// every one of several thousand students.
    /// </summary>
    private static void InsertCapped(List<Perf> list, Perf pf, int cap, bool highest)
    {
        // Cheapest possible rejection: once full, most students do not belong in the list.
        if (list.Count >= cap)
        {
            Perf edge = list[list.Count - 1];
            if (highest ? pf.gpa <= edge.gpa : pf.gpa >= edge.gpa) return;
        }
        int at = list.Count;
        for (int i = 0; i < list.Count; i++)
            if (highest ? pf.gpa > list[i].gpa : pf.gpa < list[i].gpa) { at = i; break; }
        list.Insert(at, pf);
        if (list.Count > cap) list.RemoveAt(list.Count - 1);
    }
    private static void AddGC(List<GC> list, string n, long v) { GC gc = new GC(); gc.name = n; gc.count = v; list.Add(gc); }

    // ================================================================
    //  Contextual performance breakdown (adapts to the current filter)
    //   • a programme is selected      → performance per COURSE UNIT in it
    //   • a faculty/department selected → performance per PROGRAMME in scope
    //   • nothing narrowed             → performance per FACULTY
    //  Always computed on the fly against the selected source.
    // ================================================================
    // Serialised to JSON for the preview AND reused verbatim in every export path.
    public class Breakdown { public string level; public string title; public string[] columns; public List<string[]> rows; }

    private static Breakdown BuildBreakdown(MySqlConnection conn, Cfg c, MarksScope scope)
    {
        Dictionary<string, object> p = new Dictionary<string, object>();
        string where = BuildWhere(c, scope, p);
        string level, title, sql; string[] cols; string from;
        List<string[]> rows = new List<string[]>();

        if (c.programme != "")
        {
            level = "course"; title = "Performance by Course Unit"; from = FromFor(c, J.Course);
            cols = new string[] { "Course Code", "Course Unit", "Enrolled", "Pass %", "Mean", "Mean GP" };
            sql = "SELECT r.courseid, MAX(c.courseName) cn, COUNT(*) enr, " +
                  " ROUND(100*SUM(r.score>=50)/COUNT(*),1) pr, ROUND(AVG(r.score),1) mean, ROUND(AVG(r.gradept),2) mgp " +
                  from + where + " GROUP BY r.courseid ORDER BY enr DESC LIMIT 500";
        }
        else if (c.faculty != "" || c.department != "")
        {
            level = "programme"; title = "Performance by Programme"; from = FromFor(c, J.Prog);
            cols = new string[] { "Programme", "Students", "Records", "Pass %", "Mean", "Mean GP" };
            sql = "SELECT MAX(p.progname) prog, COUNT(DISTINCT r.regno) st, COUNT(*) rec, " +
                  " ROUND(100*SUM(r.score>=50)/COUNT(*),1) pr, ROUND(AVG(r.score),1) mean, ROUND(AVG(r.gradept),2) mgp " +
                  from + where + " GROUP BY r.progid ORDER BY st DESC LIMIT 500";
        }
        else
        {
            level = "faculty"; title = "Performance by Faculty"; from = FromFor(c, J.Prog | J.Faculty);
            cols = new string[] { "Faculty", "Students", "Records", "Pass %", "Mean", "Mean GP" };
            sql = "SELECT MAX(f.faculty_name) fac, COUNT(DISTINCT r.regno) st, COUNT(*) rec, " +
                  " ROUND(100*SUM(r.score>=50)/COUNT(*),1) pr, ROUND(AVG(r.score),1) mean, ROUND(AVG(r.gradept),2) mgp " +
                  from + where + " GROUP BY p.faculty_code ORDER BY st DESC LIMIT 500";
        }

        try
        {
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                AddP(cmd, p);
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        if (level == "course")
                            rows.Add(new string[] { RS(r,0), RS(r,1), RL(r,2).ToString(), Num(RD(r,3),1), Num(RD(r,4),1), Num(RD(r,5),2) });
                        else
                            rows.Add(new string[] { RS(r,0), RL(r,1).ToString(), RL(r,2).ToString(), Num(RD(r,3),1), Num(RD(r,4),1), Num(RD(r,5),2) });
                    }
            }
        }
        catch { }
        Breakdown bd = new Breakdown();
        bd.level = level; bd.title = title; bd.columns = cols; bd.rows = rows;
        return bd;
    }

    // ================================================================
    //  PageMethods
    // ================================================================
    [WebMethod(EnableSession = true)]
    public static string GetFilterOptions()
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess)
                return Json.Serialize(new { success = true, hasAccess = false, scopeLabel = scope.Label, roleNote = scope.RoleNote });

            List<object> years = new List<object>(); List<object> faculties = new List<object>();
            List<object> departments = new List<object>();
            List<object> programmes = new List<object>(); string currentYear = "";
            HashSet<string> yearSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string pf = scope.ProgFilter("p", "progcode");

            using (MySqlConnection conn = new MySqlConnection(Conn()))
            {
                conn.Open();
                // Years present in EITHER published results or the staged pipeline, so the
                // current academic year is selectable even before it has published results.
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT acad FROM ( " +
                    "  SELECT DISTINCT acad FROM acad_results WHERE acad REGEXP '^[0-9]{4}/[0-9]{4}$' " +
                    "  UNION SELECT DISTINCT acad_year FROM campus_dynamics_portal.acad_course_registration WHERE acad_year REGEXP '^[0-9]{4}/[0-9]{4}$' " +
                    ") t ORDER BY acad DESC", conn))
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) { string v = RS(r, 0); if (v != "" && yearSet.Add(v)) years.Add(new { value = v, text = v }); }

                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT TRIM(f.faculty_code) fc, f.faculty_name FROM acad_faculty f " +
                    "WHERE TRIM(f.faculty_code)<>'00' ORDER BY f.faculty_name", conn))
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) { string v = RS(r, 0); if (v != "") faculties.Add(new { value = v, text = RS(r, 1) }); }

                // Departments that carry at least one in-scope programme.
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT d.ID, d.dept_name, TRIM(IFNULL(d.faculty_code,'')) fc FROM hrm_departments d " +
                    "WHERE d.dept_name IS NOT NULL AND TRIM(d.dept_name)<>'' " +
                    "  AND EXISTS (SELECT 1 FROM acad_programme p WHERE p.department_id=d.ID AND TRIM(p.progcode)<>''" + pf + ") " +
                    "ORDER BY d.dept_name", conn))
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) { string v = RS(r, 0); if (v != "") departments.Add(new { value = v, text = RS(r, 1), faculty = RS(r, 2) }); }

                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT TRIM(p.progcode) pc, COALESCE(p.progname,p.progcode) pn, TRIM(IFNULL(p.faculty_code,'')) fc, IFNULL(p.department_id,0) dep " +
                    "FROM acad_programme p WHERE TRIM(p.progcode) NOT IN ('','-')" + pf + " ORDER BY pn", conn))
                using (MySqlDataReader r = cmd.ExecuteReader())
                    while (r.Read()) { string v = RS(r, 0); if (v != "") programmes.Add(new { value = v, text = RS(r, 1), faculty = RS(r, 2), department = RS(r, 3) }); }

                // Default = the institution's authoritative current academic year
                // (acad_acadyears.is_current_year='Yes'); fall back to the most-populated year.
                try { currentYear = AcademicYearHelper.GetCurrentAcademicYear(); } catch { currentYear = ""; }
                if (string.IsNullOrEmpty(currentYear))
                    using (MySqlCommand cmd = new MySqlCommand(
                        "SELECT acad FROM acad_results WHERE acad REGEXP '^[0-9]{4}/[0-9]{4}$' GROUP BY acad ORDER BY COUNT(*) DESC LIMIT 1", conn))
                    { object o = cmd.ExecuteScalar(); if (o != null && o != DBNull.Value) currentYear = Convert.ToString(o); }
            }

            // Make sure the current year is an available option even if it has no data yet.
            currentYear = (currentYear ?? "").Trim();
            if (currentYear != "" && AcademicYearHelper.IsValidFormat(currentYear) && yearSet.Add(currentYear))
                years.Insert(0, new { value = currentYear, text = currentYear });
            return Json.Serialize(new
            {
                success = true, hasAccess = true, scopeLabel = scope.Label, roleNote = scope.RoleNote,
                currentYear = currentYear, years = years, faculties = faculties, departments = departments, programmes = programmes
            });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// The preview: the rows that were asked for, and how many of them there are. Nothing else.
    ///
    /// This used to build the KPI block, the grade and class-of-degree charts, the top and
    /// bottom performers, the marks-submission pipeline and the contextual breakdown BEFORE
    /// returning a single row of the table this page exists to show. Five passes over the same
    /// data, measured at 2.3s of SQL on one academic year, paid in full every time any filter
    /// changed - and paid by everyone, including the people who only ever wanted the export.
    ///
    /// Those five are now <see cref="GetInsights"/>, fetched once, and only while the panel
    /// that displays them is open.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string GetPreview(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Json.Serialize(new { success = false, message = "You do not have access to results data." });
            Cfg c = Parse(configJson);
            Grid g;
            using (MySqlConnection conn = new MySqlConnection(Conn()))
            {
                conn.Open();
                g = BuildGrid(conn, c, scope, 100);
                g.Total = CountFor(conn, c, scope);
            }
            return Json.Serialize(new
            {
                success = true, mode = c.mode, source = c.source, sourceLabel = SourceLabel(c.source),
                columns = g.Cols, rows = g.Rows, total = g.Total, previewCount = g.Rows.Count,
                scopeLabel = scope.Label, roleNote = scope.RoleNote
            });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// The analytics: KPIs, grade bands, class of degree, best and weakest performers, the
    /// staged submission pipeline and the contextual breakdown. Asked for on its own, so that
    /// a page whose job is filtering and exporting does not pay for a dashboard nobody opened.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string GetInsights(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Json.Serialize(new { success = false, message = "You do not have access to results data." });
            Cfg c = Parse(configJson);
            Stats stats; SubStats sub; object breakdown;
            using (MySqlConnection conn = new MySqlConnection(Conn()))
            {
                conn.Open();
                stats = BuildStats(conn, c, scope);
                sub = BuildSubmissionStats(conn, c, scope);
                breakdown = BuildBreakdown(conn, c, scope);
            }
            return Json.Serialize(new { success = true, stats = stats, submission = sub, breakdown = breakdown });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // Branded print-optimised HTML (browser → Save as PDF). Capped for print sanity.
    [WebMethod(EnableSession = true)]
    public static string GetPrintHtml(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Json.Serialize(new { success = false, message = "No access." });
            Cfg c = Parse(configJson);
            Stats stats; Grid g; Breakdown bd;
            using (MySqlConnection conn = new MySqlConnection(Conn()))
            {
                conn.Open();
                stats = BuildStats(conn, c, scope);
                bd = BuildBreakdown(conn, c, scope);
                g = BuildGrid(conn, c, scope, 3000);   // print cap
                ApplyHidden(g, c.hidden);
            }
            StringBuilder h = new StringBuilder(1024 * 64);
            h.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Results — ").Append(HtmlEsc(ModeLabel(c.mode))).Append("</title><style>");
            h.Append("@page{size:A4 landscape;margin:12mm}body{font-family:-apple-system,Segoe UI,Roboto,sans-serif;color:#1a1a2e;margin:0;padding:14px;font-size:10px}");
            h.Append(".pb{position:sticky;top:0;background:#05275C;color:#fff;padding:8px 12px;display:flex;justify-content:space-between;align-items:center;margin:-14px -14px 12px}");
            h.Append(".pb button{background:#fff;color:#05275C;border:0;padding:6px 14px;font-weight:600;cursor:pointer}");
            h.Append(".hd{text-align:center;border-bottom:2px solid #05275C;padding-bottom:6px;margin-bottom:8px}");
            h.Append(".hd .u{font-size:16px;font-weight:700;color:#05275C}.hd .t{font-size:12px;font-weight:700;color:#174DA4}.hd .s{font-size:9px;color:#555}");
            h.Append(".kpis{display:flex;gap:8px;margin:8px 0;flex-wrap:wrap}.kpi{border:1px solid #e0e5ed;padding:6px 10px;min-width:90px}.kpi b{display:block;font-size:15px;color:#05275C}.kpi span{font-size:8px;text-transform:uppercase;color:#888}");
            h.Append("table{width:100%;border-collapse:collapse;font-size:9px;margin-bottom:6px}th{background:#05275C;color:#fff;padding:4px 6px;text-align:left}td{padding:3px 6px;border-bottom:1px solid #eef0f4}tr:nth-child(even) td{background:#f7f9fc}");
            h.Append(".bkt{font-size:11px;font-weight:700;color:#05275C;margin:14px 0 5px;border-bottom:1.5px solid #05275C;padding-bottom:3px}");
            h.Append("table.bk{page-break-inside:auto}");
            h.Append("@media print{.pb{display:none}}");
            h.Append("</style></head><body>");
            h.Append("<div class='pb'><span>Results Export Centre — ").Append(HtmlEsc(ModeLabel(c.mode))).Append("</span><button onclick='window.print()'>Print / Save PDF</button></div>");
            h.Append("<div class='hd'><div class='u'>MUTEESA I ROYAL UNIVERSITY</div><div class='t'>").Append(HtmlEsc(ModeLabel(c.mode))).Append("</div>");
            h.Append("<div class='s'>").Append(HtmlEsc(SourceLabel(c.source))).Append(" · ").Append(HtmlEsc(scope.Label ?? "")).Append(" · ")
             .Append(c.acadYear == "" ? "All years" : HtmlEsc(c.acadYear))
             .Append(c.semester == "" ? "" : " · Sem " + HtmlEsc(c.semester))
             .Append(" · Generated ").Append(DateTime.Now.ToString("dd MMM yyyy HH:mm")).Append("</div></div>");
            h.Append("<div class='kpis'>");
            Kpi(h, stats.records, "Records"); Kpi(h, stats.students, "Students"); Kpi(h, stats.courses, "Courses");
            h.Append("<div class='kpi'><b>").Append(Num(stats.meanScore, 1)).Append("</b><span>Mean Score</span></div>");
            h.Append("<div class='kpi'><b>").Append(Num(stats.passRate, 1)).Append("%</b><span>Pass Rate</span></div>");
            h.Append("<div class='kpi'><b>").Append(Num(stats.meanGp, 2)).Append("</b><span>Mean GP</span></div></div>");
            // Contextual performance breakdown (per faculty / programme / course unit)
            if (bd != null && bd.columns != null && bd.rows != null && bd.rows.Count > 0)
            {
                h.Append("<div class='bkt'>").Append(HtmlEsc(bd.title)).Append("</div>");
                h.Append("<table class='bk'><thead><tr>");
                foreach (string col in bd.columns) h.Append("<th>").Append(HtmlEsc(col)).Append("</th>");
                h.Append("</tr></thead><tbody>");
                foreach (string[] row in bd.rows)
                {
                    h.Append("<tr>");
                    foreach (string v in row) h.Append("<td>").Append(HtmlEsc(v)).Append("</td>");
                    h.Append("</tr>");
                }
                h.Append("</tbody></table>");
            }
            h.Append("<div class='bkt'>").Append(HtmlEsc(ModeLabel(c.mode))).Append("</div>");
            h.Append("<table><thead><tr>");
            foreach (string col in g.Cols) h.Append("<th>").Append(HtmlEsc(col)).Append("</th>");
            h.Append("</tr></thead><tbody>");
            foreach (string[] row in g.Rows)
            {
                h.Append("<tr>");
                foreach (string v in row) h.Append("<td>").Append(HtmlEsc(v)).Append("</td>");
                h.Append("</tr>");
            }
            h.Append("</tbody></table>");
            if (g.Total > g.Rows.Count) h.Append("<p style='font-size:8px;color:#888'>Showing ").Append(g.Rows.Count).Append(" of ").Append(g.Total).Append(" rows (print capped). Use Excel/CSV export for the full set.</p>");
            h.Append("</body></html>");
            return Json.Serialize(new { success = true, html = h.ToString() });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }
    private static void Kpi(StringBuilder h, long v, string lbl) { h.Append("<div class='kpi'><b>").Append(v).Append("</b><span>").Append(lbl).Append("</span></div>"); }
    private static string HtmlEsc(string s) { return XmlEsc(s); }

    // ================================================================
    //  Export (postback → stream file)
    // ================================================================
    private void RunExport(string action, string configJson)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) { ShowError("You do not have access to results data."); return; }
        Cfg c = Parse(configJson);
        Grid g; Stats stats; Breakdown bd;
        using (MySqlConnection conn = new MySqlConnection(Conn()))
        {
            conn.Open();
            stats = BuildStats(conn, c, scope);
            bd = BuildBreakdown(conn, c, scope);   // same contextual breakdown shown in the preview
            g = BuildGrid(conn, c, scope, 0);   // 0 = all rows in scope
            ApplyHidden(g, c.hidden);
            LogExport(conn, c, g.Total, action);
        }
        string baseName = "Results_" + c.mode + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
        if (action == "csv") StreamCsv(g, bd, baseName);
        else StreamXlsx(g, stats, bd, c, scope, baseName);
    }

    // Traceability: record every external-representation export.
    private static void LogExport(MySqlConnection conn, Cfg c, long total, string fmt)
    {
        try
        {
            string user = "";
            HttpContext ctx = HttpContext.Current;
            if (ctx != null && ctx.Session != null && ctx.Session["username"] != null) user = ctx.Session["username"].ToString();
            string par = ModeLabel(c.mode) + " [" + (fmt == "csv" ? "CSV" : "Excel") + "]";
            if (par.Length > 300) par = par.Substring(0, 300);
            string cmt = "src=" + c.source + "; yr=" + (c.acadYear == "" ? "all" : c.acadYear) + "; sem=" + (c.semester == "" ? "all" : c.semester) +
                "; fac=" + (c.faculty == "" ? "all" : c.faculty) + "; dept=" + (c.department == "" ? "all" : c.department) + "; prog=" + (c.programme == "" ? "all" : c.programme) +
                "; rows=" + total;
            if (cmt.Length > 200) cmt = cmt.Substring(0, 200);
            using (MySqlCommand cmd = new MySqlCommand(
                "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) " +
                "VALUES (@u, 'Results Export', @par, @cmt, NOW())", conn))
            {
                cmd.Parameters.AddWithValue("@u", user);
                cmd.Parameters.AddWithValue("@par", par);
                cmd.Parameters.AddWithValue("@cmt", cmt);
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* never let logging break an export */ }
    }

    private void StreamCsv(Grid g, Breakdown bd, string fileName)
    {
        StringBuilder sb = new StringBuilder(1024 * 64);
        List<string> hs = new List<string>();
        foreach (string h in g.Cols) hs.Add(CsvEsc(h));
        sb.AppendLine(string.Join(",", hs.ToArray()));
        foreach (string[] row in g.Rows)
        {
            List<string> vs = new List<string>();
            foreach (string v in row) vs.Add(CsvEsc(v));
            sb.AppendLine(string.Join(",", vs.ToArray()));
        }
        // Contextual performance breakdown appended as a second labelled section.
        if (bd != null && bd.columns != null && bd.rows != null && bd.rows.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(CsvEsc(bd.title));
            List<string> bh = new List<string>();
            foreach (string h in bd.columns) bh.Add(CsvEsc(h));
            sb.AppendLine(string.Join(",", bh.ToArray()));
            foreach (string[] row in bd.rows)
            {
                List<string> vs = new List<string>();
                foreach (string v in row) vs.Add(CsvEsc(v));
                sb.AppendLine(string.Join(",", vs.ToArray()));
            }
        }
        Response.Clear();
        Response.ContentType = "text/csv";
        Response.Charset = "UTF-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + ".csv\"");
        Response.BinaryWrite(Encoding.UTF8.GetPreamble());
        Response.Write(sb.ToString());
        Response.Flush();
        Response.End();
    }

    // Multi-sheet SpreadsheetML (Cover + Data), branded, no external deps.
    private void StreamXlsx(Grid g, Stats stats, Breakdown bd, Cfg c, MarksScope scope, string fileName)
    {
        StringBuilder sb = new StringBuilder(1024 * 128);
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" " +
            "xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" " +
            "xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine("<Styles>");
        sb.AppendLine("<Style ss:ID=\"sTitle\"><Font ss:Bold=\"1\" ss:Size=\"14\" ss:Color=\"#05275C\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sSub\"><Font ss:Size=\"9\" ss:Color=\"#555555\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sHdr\"><Font ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/><Interior ss:Color=\"#05275C\" ss:Pattern=\"Solid\"/><Alignment ss:Vertical=\"Center\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sLbl\"><Font ss:Bold=\"1\" ss:Color=\"#174DA4\"/></Style>");
        sb.AppendLine("<Style ss:ID=\"sCell\"><Font ss:Size=\"10\"/></Style>");
        sb.AppendLine("</Styles>");

        // ---- Cover sheet ----
        sb.AppendLine("<Worksheet ss:Name=\"Cover\"><Table>");
        Row(sb, "sTitle", "Muteesa I Royal University");
        Row(sb, "sSub", "Results Export Centre — " + ModeLabel(c.mode));
        Row(sb, "sSub", "Scope: " + (scope.Label ?? "") + "  ·  Generated: " + DateTime.Now.ToString("ddd, dd MMM yyyy HH:mm"));
        Blank(sb);
        Kv(sb, "Source", SourceLabel(c.source));
        Kv(sb, "Academic Year", c.acadYear == "" ? "All (clean)" : c.acadYear);
        Kv(sb, "Semester", c.semester == "" ? "All" : c.semester);
        Kv(sb, "Study Year", c.studyYear == "" ? "All" : c.studyYear);
        Kv(sb, "Faculty", c.faculty == "" ? "All" : c.faculty);
        Kv(sb, "Department", c.department == "" ? "All" : c.department);
        Kv(sb, "Programme", c.programme == "" ? "All" : c.programme);
        Kv(sb, "Course", c.course == "" ? "All" : c.course);
        Kv(sb, "Retakes", c.retakes);
        Kv(sb, "Grade filter", c.grade == "" ? "All" : c.grade);
        Kv(sb, "Pass/Fail", c.passfail);
        Blank(sb);
        Kv(sb, "Records", stats.records.ToString());
        Kv(sb, "Distinct students", stats.students.ToString());
        Kv(sb, "Distinct courses", stats.courses.ToString());
        Kv(sb, "Mean score", Num(stats.meanScore, 1));
        Kv(sb, "Pass rate %", Num(stats.passRate, 1));
        Kv(sb, "Mean grade point", Num(stats.meanGp, 2));
        Blank(sb);
        Row(sb, "sLbl", "Grade Distribution");
        foreach (GC gc in stats.grades) Kv(sb, gc.name, gc.count.ToString());
        Blank(sb);
        Row(sb, "sLbl", "Class of Degree (indicative, selected scope)");
        if (stats.classes != null) foreach (GC gc in stats.classes) Kv(sb, gc.name, gc.count.ToString());
        Blank(sb);
        Row(sb, "sLbl", "Top Performers (by GPA, selected scope)");
        if (stats.topPerformers != null) foreach (Perf pf in stats.topPerformers) Kv(sb, pf.regno + " · " + pf.name, Num(pf.gpa, 2));
        sb.AppendLine("</Table></Worksheet>");

        // ---- Performance breakdown sheet (per faculty / programme / course unit) ----
        if (bd != null && bd.columns != null && bd.rows != null && bd.rows.Count > 0)
        {
            sb.AppendLine("<Worksheet ss:Name=\"Performance\"><Table>");
            Row(sb, "sTitle", "Muteesa I Royal University");
            Row(sb, "sSub", (bd.title ?? "Performance breakdown") + "  ·  " + SourceLabel(c.source));
            Row(sb, "sSub", "Scope: " + (scope.Label ?? "") + "  ·  Generated: " + DateTime.Now.ToString("ddd, dd MMM yyyy HH:mm"));
            Blank(sb);
            sb.Append("<Row>");
            foreach (string h in bd.columns) sb.Append("<Cell ss:StyleID=\"sHdr\"><Data ss:Type=\"String\">").Append(XmlEsc(h)).Append("</Data></Cell>");
            sb.AppendLine("</Row>");
            foreach (string[] row in bd.rows)
            {
                sb.Append("<Row>");
                foreach (string v in row)
                {
                    bool num = IsNumeric(v);
                    sb.Append("<Cell ss:StyleID=\"sCell\"><Data ss:Type=\"").Append(num ? "Number" : "String").Append("\">")
                      .Append(XmlEsc(num ? v : SafeCell(v))).Append("</Data></Cell>");
                }
                sb.AppendLine("</Row>");
            }
            sb.AppendLine("</Table></Worksheet>");
        }

        // ---- Data sheet ----
        sb.AppendLine("<Worksheet ss:Name=\"Data\"><Table>");
        sb.Append("<Row>");
        foreach (string h in g.Cols) sb.Append("<Cell ss:StyleID=\"sHdr\"><Data ss:Type=\"String\">").Append(XmlEsc(h)).Append("</Data></Cell>");
        sb.AppendLine("</Row>");
        foreach (string[] row in g.Rows)
        {
            sb.Append("<Row>");
            foreach (string v in row)
            {
                bool num = IsNumeric(v);
                sb.Append("<Cell ss:StyleID=\"sCell\"><Data ss:Type=\"").Append(num ? "Number" : "String").Append("\">")
                  .Append(XmlEsc(num ? v : SafeCell(v))).Append("</Data></Cell>");
            }
            sb.AppendLine("</Row>");
        }
        sb.AppendLine("</Table></Worksheet>");
        sb.AppendLine("</Workbook>");

        Response.Clear();
        Response.ContentType = "application/vnd.ms-excel";
        Response.Charset = "UTF-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.AddHeader("Content-Disposition", "attachment; filename=\"" + fileName + ".xls\"");
        Response.BinaryWrite(Encoding.UTF8.GetPreamble());
        Response.Write(sb.ToString());
        Response.Flush();
        Response.End();
    }

    // ---- SpreadsheetML helpers ----
    private static void Row(StringBuilder sb, string style, string text)
    { sb.Append("<Row><Cell ss:StyleID=\"").Append(style).Append("\"><Data ss:Type=\"String\">").Append(XmlEsc(text)).Append("</Data></Cell></Row>"); }
    private static void Blank(StringBuilder sb) { sb.Append("<Row></Row>"); }
    private static void Kv(StringBuilder sb, string k, string v)
    { sb.Append("<Row><Cell ss:StyleID=\"sLbl\"><Data ss:Type=\"String\">").Append(XmlEsc(k)).Append("</Data></Cell>")
        .Append("<Cell ss:StyleID=\"sCell\"><Data ss:Type=\"String\">").Append(XmlEsc(SafeCell(v))).Append("</Data></Cell></Row>"); }

    private static bool IsNumeric(string v)
    {
        if (string.IsNullOrEmpty(v)) return false;
        double d; return double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out d);
    }
    private static string Num(double d, int dp) { return d.ToString("F" + dp, CultureInfo.InvariantCulture); }
    private static string XmlEsc(string s)
    { if (string.IsNullOrEmpty(s)) return ""; return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;"); }
    // Formula-injection safety for external files.
    private static string SafeCell(string v)
    { if (string.IsNullOrEmpty(v)) return ""; char f = v[0]; if (f == '=' || f == '+' || f == '-' || f == '@') return "'" + v; return v; }
    private static string CsvEsc(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        v = SafeCell(v);
        if (v.IndexOf(',') >= 0 || v.IndexOf('"') >= 0 || v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0)
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    // ---- grading helpers (match acad_gs_award) ----
    private static string LevelName(int lvl)
    { switch (lvl) { case 1: return "Certificate"; case 2: return "Diploma"; case 4: return "Masters"; case 5: return "Postgraduate"; default: return "Bachelors"; } }
    private static string AwardClass(double cgpa, int lvl)
    {
        bool dipCert = (lvl == 1 || lvl == 2);
        if (cgpa >= 4.4) return dipCert ? "Class I (Distinction)" : "First Class (Honours)";
        if (dipCert) { if (cgpa >= 2.8) return "Class II (Credit)"; if (cgpa >= 2.0) return "Class III (Pass)"; return "Fail"; }
        if (cgpa >= 3.6) return "Second Class Upper";
        if (cgpa >= 2.8) return "Second Class Lower";
        if (cgpa >= 2.0) return "Third Class (Pass)";
        return "Fail";
    }
    private static string ModeLabel(string m)
    {
        if (m == "summary") return "Student Results Summary";
        if (m == "course_stats") return "Course Performance Statistics";
        if (m == "prog_stats") return "Programme / Faculty Statistics";
        return "Marks Sheet (detailed)";
    }

    private void ShowError(string msg)
    {
        string js = "alert('Export failed: " + (msg ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", " ").Replace("\n", " ") + "');";
        ScriptManager.RegisterStartupScript(this, GetType(), "expErr", js, true);
    }
}
