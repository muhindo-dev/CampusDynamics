using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre, the filter lists every page starts from.
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

    /// <summary>
    /// Makes a JSON payload safe to emit inside a &lt;script&gt; block.
    ///
    /// A programme name or a student name containing "&lt;/script&gt;" would otherwise close the
    /// block early and everything after it would be parsed as HTML. The sequences below cannot
    /// appear inside a JSON string except as literal text, and escaping them this way is still
    /// valid JSON, so the browser parses exactly the same object.
    /// </summary>
    public static string ForScriptBlock(string json)
    {
        if (string.IsNullOrEmpty(json)) return "null";
        // NOTE the doubled backslash: in C# source the six characters that spell a
        // unicode escape ARE the character itself, which would make this a no-op. What
        // is wanted is the literal two-character text that JSON reads as an escape.
        return json.Replace("<", "\\u003c").Replace(">", "\\u003e").Replace("&", "\\u0026");
    }

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
            f.award = S(d, "award");
            string gb = S(d, "groupBy"); if (gb != "") f.groupBy = gb;
            string ob = S(d, "orderBy"); if (ob != "") f.orderBy = ob;
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
                        // From the summary: 916ms against acad_results became immeasurable.
                        "SELECT a.progcode, COUNT(*) FROM " + GraduationStats.TABLE + " a " +
                        "WHERE a.is_candidate=1 AND a.on_list=0" +
                        scope.ProgFilter("a", "progcode") + " GROUP BY a.progcode", c))
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
                    // From the summary: 862ms became 25ms.
                    "SELECT DISTINCT s.entryyear FROM acad_student s " +
                    "JOIN " + GraduationStats.TABLE + " a ON a.regno=s.regno " +
                    "WHERE a.is_candidate=1 AND IFNULL(s.entryyear,0)>0" +
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

    /// <summary>
    /// The filter spelled out for an export cover sheet.
    ///
    /// A cover sheet exists so the file can be defended in a meeting, and "Faculty: 01" defends
    /// nothing. Codes are resolved to the names a reader recognises; the code is kept in brackets
    /// so the file can still be tied back to the system that produced it.
    /// </summary>
    public static List<KeyValuePair<string, string>> CoverOf(GraduationEngine.GradFilter f, string focusLabel)
    {
        var l = new List<KeyValuePair<string, string>>();
        l.Add(new KeyValuePair<string, string>("Graduation year", f.acadYear == "" ? "All years" : f.acadYear));
        if (focusLabel != "") l.Add(new KeyValuePair<string, string>("Population", focusLabel));

        Names n = Resolve(f);
        l.Add(new KeyValuePair<string, string>("Faculty", f.faculty == "" ? "All faculties" : n.faculty));
        l.Add(new KeyValuePair<string, string>("Department", f.department == "" ? "All departments" : n.department));
        l.Add(new KeyValuePair<string, string>("Programme", f.programme == "" ? "All programmes" : n.programme));

        if (f.entryYear != "") l.Add(new KeyValuePair<string, string>("Intake", f.entryYear));
        if (f.finishedIn != "") l.Add(new KeyValuePair<string, string>("Last sat a paper in", f.finishedIn));
        if (f.readiness != "")
            l.Add(new KeyValuePair<string, string>("Readiness",
                f.readiness == "ready" ? "Ready - nothing outstanding"
                : f.readiness == "warn" ? "Needs checking"
                : f.readiness == "blocked" ? "Blocked by at least one check" : f.readiness));
        if (f.award != "") l.Add(new KeyValuePair<string, string>("Class of award", f.award));
        if (f.search != "") l.Add(new KeyValuePair<string, string>("Search", f.search));
        return l;
    }

    private class Names { public string faculty = "", department = "", programme = ""; }

    /// <summary>Codes to names, in one round trip, and never at the cost of the export itself.</summary>
    private static Names Resolve(GraduationEngine.GradFilter f)
    {
        var n = new Names();
        n.faculty = f.faculty; n.department = f.department; n.programme = f.programme;
        if (f.faculty == "" && f.department == "" && f.programme == "") return n;
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                if (f.faculty != "")
                    n.faculty = Label(c, "SELECT faculty_name FROM acad_faculty WHERE TRIM(faculty_code)=@v",
                                      f.faculty, f.faculty);
                int dep;
                if (f.department != "" && int.TryParse(f.department, out dep))
                    n.department = Label(c, "SELECT dept_name FROM hrm_departments WHERE ID=@v",
                                         f.department, f.department);
                if (f.programme != "")
                    n.programme = Label(c, "SELECT progname FROM acad_programme WHERE TRIM(progcode)=@v",
                                        f.programme, f.programme);
            }
        }
        catch { /* a cover sheet is never worth failing an export over */ }
        return n;
    }

    private static string Label(MySqlConnection c, string sql, string val, string code)
    {
        using (var cmd = new MySqlCommand(sql, c))
        {
            cmd.Parameters.AddWithValue("@v", val);
            object o = cmd.ExecuteScalar();
            string name = o == null || o == DBNull.Value ? "" : o.ToString().Trim();
            return name == "" ? code : name + "  (" + code + ")";
        }
    }
}
