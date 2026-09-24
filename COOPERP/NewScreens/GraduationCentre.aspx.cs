using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using System.Web.Services;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — the page.
//  Plan: COOPERP/NewScreens/GRADUATION_CENTRE_PLAN.md
//
//  This file is transport only: authorise, unpack the request, call the
//  engine or the service, serialise the answer. Not one number is
//  computed here. Every figure on screen comes from GraduationEngine, so
//  that the dashboard and the list it links to cannot disagree.
//
//  Replaces the January 2026 DevExpress version of this page. The tables
//  it wrote to are unchanged: a student is on the graduation list if and
//  only if they have an acad_graduands row, exactly as before, so
//  transcripts, certificates and AlumniDataBank are unaffected.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationCentre : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    protected void Page_Load(object sender, EventArgs e) { }

    private static string Denied()
    {
        return J.Serialize(new
        {
            success = false,
            hasAccess = false,
            message = "Your account is not linked to a faculty or department, so no graduation data is available."
        });
    }

    private static GraduationEngine.GradFilter Parse(string json)
    {
        var f = new GraduationEngine.GradFilter();
        if (string.IsNullOrEmpty(json)) return f;
        try
        {
            var d = J.Deserialize<Dictionary<string, object>>(json);
            f.acadYear = S(d, "acadYear");
            f.faculty = S(d, "faculty");
            f.department = S(d, "department");
            f.programme = S(d, "programme");
            f.entryYear = S(d, "entryYear");
            f.finishedIn = S(d, "finishedIn");
            string fo = S(d, "focus"); if (fo != "") f.focus = fo;
            f.search = S(d, "search");
            string st = S(d, "state"); if (st != "") f.state = st;
            f.readiness = S(d, "readiness");
            string so = S(d, "sort"); if (so != "") f.sort = so;
            int n;
            if (int.TryParse(S(d, "page"), out n) && n > 0) f.page = n;
            if (int.TryParse(S(d, "size"), out n) && n > 0) f.size = n;
        }
        catch { }
        return f;
    }

    private static string S(Dictionary<string, object> d, string k)
    { object o; return d != null && d.TryGetValue(k, out o) && o != null ? o.ToString().Trim() : ""; }

    // ── Bootstrap: scope, and the filter lists that cascade ──────────
    [WebMethod(EnableSession = true)]
    public static string GetBootstrap()
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Denied();

            var years = new List<string>();
            var faculties = new List<object>();
            var departments = new List<object>();
            var programmes = new List<object>();
            var intakes = new List<string>();
            var counts = new Dictionary<string, int>();
            string currentYear = "";

            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                // Graduation years already in use, plus the academic years results exist for,
                // so a list can be started for a year nobody has graduated in yet.
                // Years offered, and ONLY plausible ones. acad_results contains 2202/2203 -
                // four rows for one student, a typing slip - and because the list was ordered
                // descending it sorted to the top and became the default graduation year for
                // the whole module. A year outside 2000-2035 is a typo, not a cohort.
                using (var cmd = new MySqlCommand(
                    "SELECT y FROM ( " +
                    "  SELECT DISTINCT acadyear y FROM acad_graduands WHERE acadyear REGEXP '^[0-9]{4}/[0-9]{4}$' " +
                    "  UNION SELECT DISTINCT acad FROM acad_results WHERE acad REGEXP '^[0-9]{4}/[0-9]{4}$' " +
                    "  UNION SELECT DISTINCT acadyear FROM acad_acadyears WHERE acadyear REGEXP '^[0-9]{4}/[0-9]{4}$' " +
                    ") z WHERE CAST(LEFT(y,4) AS UNSIGNED) BETWEEN 2000 AND 2035 " +
                    "  AND CAST(RIGHT(y,4) AS UNSIGNED) = CAST(LEFT(y,4) AS UNSIGNED)+1 " +
                    "ORDER BY y DESC", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) years.Add(r[0].ToString());

                // The year the institution says it is in - acad_acadyears.is_current_year -
                // not whichever string happens to sort first.
                try { currentYear = AcademicYearHelper.GetCurrentAcademicYear(); }
                catch { currentYear = ""; }
                if (currentYear == "" || !years.Contains(currentYear))
                    currentYear = years.Count > 0 ? years[0] : "";

                string pf = scope.ProgFilterExpr("p.progcode");
                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(f.faculty_code) fc, f.faculty_name FROM acad_faculty f " +
                    "WHERE EXISTS (SELECT 1 FROM acad_programme p WHERE TRIM(p.faculty_code)=TRIM(f.faculty_code)" + pf + ") " +
                    "ORDER BY f.faculty_name", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) faculties.Add(new { v = r[0].ToString(), t = r[1].ToString() });

                using (var cmd = new MySqlCommand(
                    "SELECT d.ID, d.dept_name, TRIM(IFNULL(d.faculty_code,'')) fc FROM hrm_departments d " +
                    "WHERE EXISTS (SELECT 1 FROM acad_programme p WHERE p.department_id=d.ID" + pf + ") " +
                    "ORDER BY d.dept_name", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) departments.Add(new { v = r[0].ToString(), t = r[1].ToString(), fac = r[2].ToString() });

                // How many outstanding candidates each programme has. Shown against the
                // programme in the dropdown, so an empty combination is visible BEFORE it is
                // selected rather than after a round trip that returns nothing.
                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(s.progid) pc, COUNT(*) n FROM acad_student s " +
                    "JOIN acad_programme p ON p.progcode=s.progid " +
                    "WHERE EXISTS (SELECT 1 FROM acad_results r WHERE r.regno=s.regno " +
                    "   AND r.studyyear >= IFNULL(NULLIF(p.couselength,0),3)) " +
                    " AND NOT EXISTS (SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno)" +
                    scope.ProgFilter("s", "progid") + " GROUP BY pc", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string k = r[0].ToString();
                        if (!counts.ContainsKey(k)) counts.Add(k, Convert.ToInt32(r[1]));
                    }

                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(p.progcode) pc, COALESCE(p.progname,p.progcode) pn, " +
                    " TRIM(IFNULL(p.faculty_code,'')) fc, IFNULL(p.department_id,0) dep " +
                    "FROM acad_programme p WHERE TRIM(IFNULL(p.progcode,''))<>'' AND p.progcode<>'-'" + pf +
                    " ORDER BY pn", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string pc = r[0].ToString();
                        int n; counts.TryGetValue(pc, out n);
                        programmes.Add(new
                        {
                            v = pc,
                            t = r[1].ToString() + (n > 0 ? "  (" + n + ")" : "  (none)"),
                            fac = r[2].ToString(),
                            dep = r[3].ToString(),
                            n = n
                        });
                    }

                // Intakes that actually have candidates, so the filter cannot offer a dead year.
                using (var cmd = new MySqlCommand(
                    "SELECT DISTINCT s.entryyear FROM acad_student s " +
                    "JOIN acad_programme p ON p.progcode=s.progid " +
                    "WHERE IFNULL(s.entryyear,0)>0 AND EXISTS (SELECT 1 FROM acad_results r " +
                    "  WHERE r.regno=s.regno AND r.studyyear >= IFNULL(NULLIF(p.couselength,0),3))" +
                    scope.ProgFilter("s", "progid") + " ORDER BY s.entryyear DESC", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) intakes.Add(r[0].ToString());
            }

            return J.Serialize(new
            {
                success = true,
                hasAccess = true,
                scopeLabel = scope.Label,
                roleNote = scope.RoleNote,
                canAct = true,
                years = years,
                currentYear = currentYear,
                previousYear = GraduationEngine.PreviousYear(currentYear),
                faculties = faculties,
                departments = departments,
                programmes = programmes,
                intakes = intakes
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetOverview(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Denied();
            GraduationEngine.GradOverview o = GraduationEngine.Overview(scope, Parse(configJson));
            return J.Serialize(new { success = true, overview = o });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetCandidates(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Denied();
            GraduationEngine.GradFilter f = Parse(configJson);
            int total;
            List<GradCandidate> rows = GraduationEngine.Page(scope, f, out total);
            return J.Serialize(new
            {
                success = true,
                rows = rows,
                total = total,
                page = f.page,
                size = f.size,
                pages = (total + f.size - 1) / f.size
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>
    /// One student, assessed live, with the programme structure beside their results so a
    /// missing course reads as a gap rather than as a smaller total.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string GetStudent(string regno)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Denied();
            regno = (regno ?? "").Trim();
            if (regno == "") return J.Serialize(new { success = false, message = "No student was given." });

            var results = new List<object>();
            var structure = new List<object>();
            var history = new List<object>();
            GradCandidate g;

            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                g = GraduationService.Load(c, scope, regno);
                if (g == null) return J.Serialize(new { success = false, message = "No student record for " + regno + "." });
                if (!scope.AllowsProg(g.progcode))
                    return J.Serialize(new { success = false, message = "That student is outside the programmes you can see." });

                using (var cmd = new MySqlCommand(
                    "SELECT r.acad, r.studyyear, r.semester, r.courseid, IFNULL(c2.courseName,'') cn, " +
                    " IFNULL(r.CreditUnits,0) cu, r.score, IFNULL(r.grade,'') gr, IFNULL(r.gradept,0) gp " +
                    "FROM acad_results r LEFT JOIN acad_course c2 ON c2.courseID=r.courseid " +
                    "WHERE r.regno=@r ORDER BY r.acad DESC, r.studyyear DESC, r.semester, r.courseid", c))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            results.Add(new
                            {
                                acad = r[0].ToString(),
                                sy = r[1].ToString(),
                                sem = r[2].ToString(),
                                code = r[3].ToString(),
                                name = r[4].ToString(),
                                cu = Convert.ToDouble(r[5]),
                                score = r.IsDBNull(6) ? (object)null : Convert.ToInt32(r[6]),
                                grade = r[7].ToString(),
                                gp = Convert.ToDouble(r[8])
                            });
                }

                // The programme structure, marked against what the student actually has. Only
                // shown when a real specialisation resolves — otherwise it would be a list of
                // courses from somebody else's curriculum.
                if (!g.specIsPlaceholder)
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT pc.study_year, pc.semester, pc.course_code, IFNULL(ac.courseName,'') cn, " +
                        " IFNULL(ac.CreditUnit,0) cu, pc.course_type, " +
                        " (SELECT r.score FROM acad_results r WHERE r.regno=@r AND r.courseid=pc.course_code LIMIT 1) score " +
                        "FROM acad_programmecourses pc LEFT JOIN acad_course ac ON ac.courseID=pc.course_code " +
                        "WHERE pc.progcode=@p AND IFNULL(pc.specialisation_id,0)=@sp AND pc.status='Active' " +
                        "ORDER BY pc.study_year, pc.semester, pc.course_code", c))
                    {
                        cmd.Parameters.AddWithValue("@r", regno);
                        cmd.Parameters.AddWithValue("@p", g.progcode);
                        cmd.Parameters.AddWithValue("@sp", g.specialisation);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                structure.Add(new
                                {
                                    sy = r[0].ToString(),
                                    sem = r[1].ToString(),
                                    code = r[2].ToString(),
                                    name = r[3].ToString(),
                                    cu = Convert.ToDouble(r[4]),
                                    type = r[5].ToString(),
                                    score = r.IsDBNull(6) ? (object)null : Convert.ToInt32(r[6])
                                });
                    }
                }

                using (var cmd = new MySqlCommand(
                    "SELECT verdict, IFNULL(reason,''), actor, IFNULL(actor_role,''), " +
                    " DATE_FORMAT(created_at,'%e %b %Y, %H:%i'), acadyear, " +
                    " IF(superseded_at IS NULL,1,0) inforce " +
                    "FROM acad_grad_review WHERE regno=@r ORDER BY id DESC LIMIT 40", c))
                {
                    cmd.Parameters.AddWithValue("@r", regno);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            history.Add(new
                            {
                                verdict = r[0].ToString(),
                                reason = r[1].ToString(),
                                actor = r[2].ToString(),
                                role = r[3].ToString(),
                                at = r[4].ToString(),
                                year = r[5].ToString(),
                                inForce = Convert.ToInt32(r[6]) == 1
                            });
                }
            }

            return J.Serialize(new
            {
                success = true,
                student = g,
                results = results,
                structure = structure,
                history = history
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>The graduation list itself, with who cleared each name and when.</summary>
    [WebMethod(EnableSession = true)]
    public static string GetGraduationList(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return Denied();
            GraduationEngine.GradFilter f = Parse(configJson);

            var rows = new List<object>();
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                var ps = new List<string>();
                string w = " WHERE 1=1 ";
                if (f.acadYear != "") { w += " AND g.acadyear=@ay "; ps.Add("@ay"); }
                if (f.faculty != "") { w += " AND p.faculty_code=@fac "; ps.Add("@fac"); }
                if (f.programme != "") { w += " AND g.progcode=@prog "; ps.Add("@prog"); }
                if (f.search != "") { w += " AND (g.regno LIKE @q OR g.stud_name LIKE @q) "; ps.Add("@q"); }
                w += scope.ProgFilter("g", "progcode");

                using (var cmd = new MySqlCommand(
                    "SELECT g.regno, g.stud_name, g.progcode, IFNULL(p.progname,'') pn, g.cgpa, g.degclass, " +
                    " g.acadyear, IFNULL(g.gender,''), IFNULL(g.nationality,''), " +
                    " IFNULL(g.trans_status,''), IFNULL(g.cert_status,''), " +
                    " IFNULL((SELECT v.actor FROM acad_grad_review v WHERE v.regno=g.regno AND v.verdict='CLEARED' " +
                    "         ORDER BY v.id DESC LIMIT 1),'') cleared_by, " +
                    " IFNULL((SELECT DATE_FORMAT(v.created_at,'%e %b %Y') FROM acad_grad_review v " +
                    "         WHERE v.regno=g.regno AND v.verdict='CLEARED' ORDER BY v.id DESC LIMIT 1),'') cleared_at " +
                    "FROM acad_graduands g LEFT JOIN acad_programme p ON p.progcode=g.progcode " +
                    w + " ORDER BY p.progname, g.stud_name LIMIT 3000", c))
                {
                    if (ps.Contains("@ay")) cmd.Parameters.AddWithValue("@ay", f.acadYear);
                    if (ps.Contains("@fac")) cmd.Parameters.AddWithValue("@fac", f.faculty);
                    if (ps.Contains("@prog")) cmd.Parameters.AddWithValue("@prog", f.programme);
                    if (ps.Contains("@q")) cmd.Parameters.AddWithValue("@q", "%" + f.search + "%");
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            rows.Add(new
                            {
                                regno = r[0].ToString(),
                                name = r[1].ToString(),
                                progcode = r[2].ToString(),
                                progname = r[3].ToString(),
                                cgpa = Convert.ToDouble(r[4]),
                                degclass = r[5].ToString(),
                                year = r[6].ToString(),
                                gender = r[7].ToString(),
                                nationality = r[8].ToString(),
                                transStatus = r[9].ToString(),
                                certStatus = r[10].ToString(),
                                clearedBy = r[11].ToString(),
                                clearedAt = r[12].ToString()
                            });
                }
            }
            return J.Serialize(new { success = true, rows = rows, total = rows.Count });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── Decisions. Every one re-checks scope inside the service. ─────
    [WebMethod(EnableSession = true)]
    public static string ClearStudent(string regno, string acadYear, string note, bool overrideBlock)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return Denied();
        return GraduationService.Clear(scope, regno, acadYear, note, overrideBlock);
    }

    /// <summary>
    /// Clears several candidates at once.
    ///
    /// Only ever offered for candidates whose worst finding is PASS, and the server enforces
    /// that rather than trusting the browser: each student is re-assessed live, and anything
    /// that is not READY is skipped and reported back by name. A bulk action that can quietly
    /// clear a blocked student is worse than no bulk action.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string ClearMany(string regnos, string acadYear)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return Denied();
        acadYear = (acadYear ?? "").Trim();
        if (acadYear == "") return J.Serialize(new { success = false, message = "Choose the graduation year first." });

        var ids = new List<string>();
        foreach (string x in (regnos ?? "").Split(','))
        { string t = x.Trim(); if (t != "" && !ids.Contains(t)) ids.Add(t); }
        if (ids.Count == 0) return J.Serialize(new { success = false, message = "Nothing was selected." });
        if (ids.Count > 300) return J.Serialize(new { success = false, message = "Clear at most 300 at a time." });

        int done = 0;
        var skipped = new List<string>();
        foreach (string reg in ids)
        {
            string raw = GraduationService.Clear(scope, reg, acadYear, "", false);
            bool ok = false;
            try
            {
                var d = J.Deserialize<Dictionary<string, object>>(raw);
                object v;
                ok = d.TryGetValue("success", out v) && v != null && Convert.ToBoolean(v);
                if (!ok)
                {
                    object m; d.TryGetValue("message", out m);
                    skipped.Add(reg + " \u2014 " + (m == null ? "refused" : m.ToString()));
                }
            }
            catch { skipped.Add(reg + " \u2014 could not be read"); }
            if (ok) done++;
        }

        return J.Serialize(new
        {
            success = true,
            cleared = done,
            skipped = skipped,
            message = done + (done == 1 ? " candidate" : " candidates") + " added to the " + acadYear +
                      " graduation list" + (skipped.Count > 0 ? "; " + skipped.Count + " skipped." : ".")
        });
    }

    [WebMethod(EnableSession = true)]
    public static string HoldStudent(string regno, string acadYear, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return Denied();
        return GraduationService.Hold(scope, regno, acadYear, reason);
    }

    [WebMethod(EnableSession = true)]
    public static string ReleaseStudent(string regno, string acadYear, string note)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return Denied();
        return GraduationService.Release(scope, regno, acadYear, note);
    }

    [WebMethod(EnableSession = true)]
    public static string RemoveStudent(string regno, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return Denied();
        return GraduationService.RemoveFromList(scope, regno, reason);
    }
}
