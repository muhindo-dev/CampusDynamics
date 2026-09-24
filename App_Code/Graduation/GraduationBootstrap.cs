using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — the filter lists every page starts from.
//
//  The four pages are independent, but they must offer the SAME years,
//  faculties, departments and programmes, resolved through the same
//  scope. Duplicating that in four code-behinds would guarantee they
//  drift apart, so it lives here and each page's GetBootstrap is one
//  line.
// =====================================================================
public static class GraduationBootstrap
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    public static string Denied()
    {
        return J.Serialize(new
        {
            success = false,
            hasAccess = false,
            message = "Your account is not linked to a faculty or department, so no graduation data is available."
        });
    }

    /// <summary>Unpacks a filter posted by any of the pages.</summary>
    public static GraduationEngine.GradFilter Parse(string json)
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
            f.search = S(d, "search");
            f.readiness = S(d, "readiness");
            string v = S(d, "state"); if (v != "") f.state = v;
            v = S(d, "focus"); if (v != "") f.focus = v;
            v = S(d, "sort"); if (v != "") f.sort = v;
            int n;
            if (int.TryParse(S(d, "page"), out n) && n > 0) f.page = n;
            if (int.TryParse(S(d, "size"), out n) && n > 0) f.size = n;
        }
        catch { }
        return f;
    }

    public static string S(Dictionary<string, object> d, string k)
    { object o; return d != null && d.TryGetValue(k, out o) && o != null ? o.ToString().Trim() : ""; }

    /// <summary>
    /// Years, faculties, departments and programmes for the signed-in user's scope.
    ///
    /// Years are filtered to believable ones. acad_results holds 2202/2203 and four other
    /// malformed years across eleven students; because these are character columns the worst of
    /// them sorted above every real year and used to become the module's default.
    /// </summary>
    public static string Bootstrap(bool withCounts)
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

                try { currentYear = AcademicYearHelper.GetCurrentAcademicYear(); }
                catch { currentYear = ""; }
                if (currentYear == "" || !years.Contains(currentYear))
                    currentYear = years.Count > 0 ? years[0] : "";

                string pf = scope.ProgFilterExpr("p.progcode");

                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(f.faculty_code), f.faculty_name FROM acad_faculty f " +
                    "WHERE EXISTS (SELECT 1 FROM acad_programme p WHERE TRIM(p.faculty_code)=TRIM(f.faculty_code)" + pf + ") " +
                    "ORDER BY f.faculty_name", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) faculties.Add(new { v = r[0].ToString(), t = r[1].ToString() });

                using (var cmd = new MySqlCommand(
                    "SELECT d.ID, d.dept_name, TRIM(IFNULL(d.faculty_code,'')) FROM hrm_departments d " +
                    "WHERE EXISTS (SELECT 1 FROM acad_programme p WHERE p.department_id=d.ID" + pf + ") " +
                    "ORDER BY d.dept_name", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        departments.Add(new { v = r[0].ToString(), t = r[1].ToString(), fac = r[2].ToString() });

                // Outstanding candidates per programme, so an empty combination is visible in
                // the dropdown BEFORE it is chosen rather than after a round trip.
                if (withCounts)
                {
                    using (var cmd = new MySqlCommand(
                        "SELECT TRIM(s.progid), COUNT(*) FROM acad_student s " +
                        "JOIN acad_programme p ON p.progcode=s.progid " +
                        "WHERE EXISTS (SELECT 1 FROM acad_results r WHERE r.regno=s.regno " +
                        "   AND r.studyyear >= IFNULL(NULLIF(p.couselength,0),3)) " +
                        " AND NOT EXISTS (SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno)" +
                        scope.ProgFilter("s", "progid") + " GROUP BY 1", c))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            string k = r[0].ToString();
                            if (!counts.ContainsKey(k)) counts.Add(k, Convert.ToInt32(r[1]));
                        }
                }

                using (var cmd = new MySqlCommand(
                    "SELECT TRIM(p.progcode), COALESCE(p.progname,p.progcode), " +
                    " TRIM(IFNULL(p.faculty_code,'')), IFNULL(p.department_id,0) " +
                    "FROM acad_programme p WHERE TRIM(IFNULL(p.progcode,''))<>'' AND p.progcode<>'-'" + pf +
                    " ORDER BY 2", c))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        string pc = r[0].ToString();
                        int n; counts.TryGetValue(pc, out n);
                        programmes.Add(new
                        {
                            v = pc,
                            t = r[1].ToString() + (withCounts ? (n > 0 ? "  (" + n + ")" : "  (none)") : ""),
                            fac = r[2].ToString(),
                            dep = r[3].ToString()
                        });
                    }

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

    /// <summary>The filter spelled out for an export cover sheet.</summary>
    public static List<KeyValuePair<string, string>> CoverOf(GraduationEngine.GradFilter f, string focusLabel)
    {
        var l = new List<KeyValuePair<string, string>>();
        l.Add(new KeyValuePair<string, string>("Graduation year", f.acadYear == "" ? "All years" : f.acadYear));
        if (focusLabel != "") l.Add(new KeyValuePair<string, string>("Population", focusLabel));
        l.Add(new KeyValuePair<string, string>("Faculty", f.faculty == "" ? "All" : f.faculty));
        l.Add(new KeyValuePair<string, string>("Department", f.department == "" ? "All" : f.department));
        l.Add(new KeyValuePair<string, string>("Programme", f.programme == "" ? "All" : f.programme));
        if (f.entryYear != "") l.Add(new KeyValuePair<string, string>("Intake", f.entryYear));
        if (f.finishedIn != "") l.Add(new KeyValuePair<string, string>("Finished in", f.finishedIn));
        if (f.readiness != "") l.Add(new KeyValuePair<string, string>("Readiness", f.readiness));
        if (f.search != "") l.Add(new KeyValuePair<string, string>("Search", f.search));
        return l;
    }
}
