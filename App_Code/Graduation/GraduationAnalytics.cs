using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Analysis — the figures behind the screen.
//
//  Everything the analysis page shows is computed here, from
//  acad_graduands, which is the system of record for who has graduated.
//
//  TWO FACTS ABOUT THIS DATA SHAPE THE WHOLE FILE, and the page it
//  replaces got both wrong.
//
//  1. THE CLASS OF AWARD USES TWO VOCABULARIES, split exactly by award
//     level. Certificates and diplomas are classed "Class I
//     (Distinction) / Class II (Credit) / Class III (Pass)"; bachelors
//     and above are classed "First Class (Honours) / Second Class Upper
//     / Second Class Lower / Third Class (Pass)". Measured across all
//     1,624 rows, not one crosses over. They are different scales for
//     different awards and must never share a table.
//
//  2. A CONVOCATION IS NOT AN ACADEMIC YEAR. The 13th graduation
//     ceremony carries 592 graduands drawn from TEN different academic
//     years; the 12th carries 704 from seven. People are capped at the
//     next ceremony after they complete, whenever that is. Both lenses
//     are legitimate, so both are offered - and which one is in force is
//     always stated.
//
//  The convocation column is free text: "12th graduation ceremony",
//  "MRU 12TH GRADUATION" and "12TH GRADUATION CEREMONY" are one
//  ceremony. It is reduced to its ordinal for grouping and NEVER written
//  back; the data is left exactly as found.
// =====================================================================
public static class GraduationAnalytics
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    /// <summary>
    /// Reduces the free-text convocation to its ordinal number, in SQL.
    ///
    /// Strips the words and punctuation people have typed around it over thirteen ceremonies and
    /// keeps the digits. Rows with no digits at all - there is one, "h graduation ceremony" -
    /// come back NULL and are counted as "not recorded" rather than silently dropped.
    /// </summary>
    private const string CEREMONY_EXPR =
        "CASE WHEN TRIM(LEADING '0' FROM REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(" +
        "REPLACE(REPLACE(REPLACE(REPLACE(UPPER(IFNULL(g.convocation,'')),'MRU',''),'GRADUATION','')," +
        "'CEREMONY',''),'TH',''),'ST',''),'ND',''),'RD',''),' ',''),'-',''),'.','')) REGEXP '^[0-9]+$' " +
        "THEN CAST(TRIM(LEADING '0' FROM REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(" +
        "REPLACE(REPLACE(REPLACE(REPLACE(UPPER(IFNULL(g.convocation,'')),'MRU',''),'GRADUATION','')," +
        "'CEREMONY',''),'TH',''),'ST',''),'ND',''),'RD',''),' ',''),'-',''),'.','')) AS UNSIGNED) " +
        "ELSE NULL END";

    /// <summary>Award levels, in the order a Senate paper reads them.</summary>
    public static string LevelName(int code)
    {
        switch (code)
        {
            case 1: return "Certificate";
            case 2: return "Diploma";
            case 3: return "Bachelor's degree";
            case 4: return "Master's degree";
            case 5: return "Postgraduate";
            default: return "Not classified";
        }
    }

    /// <summary>
    /// True for the best class an award level offers. Both vocabularies, because the two live
    /// side by side in one column and a "top class share" that only understood one of them would
    /// silently report zero for every diploma.
    /// </summary>
    private static bool IsTop(string degclass)
    {
        string c = (degclass ?? "").ToUpperInvariant();
        return c.Contains("FIRST CLASS") || c.Contains("CLASS I (") || c.Contains("DISTINCTION");
    }

    public class Filter
    {
        /// <summary>"year" or "ceremony" - which lens is in force.</summary>
        public string lens = "year";
        public string acadYear = "";
        public string ceremony = "";       // the ordinal, as text
        public string faculty = "";
        public string department = "";
        public string programme = "";
        public string level = "";          // levelCode, as text
    }

    public static Filter Parse(string json)
    {
        var f = new Filter();
        if (string.IsNullOrEmpty(json)) return f;
        try
        {
            var d = J.Deserialize<Dictionary<string, object>>(json);
            string v = GraduationBootstrap.S(d, "lens"); if (v != "") f.lens = v;
            f.acadYear = GraduationBootstrap.S(d, "acadYear");
            f.ceremony = GraduationBootstrap.S(d, "ceremony");
            f.faculty = GraduationBootstrap.S(d, "faculty");
            f.department = GraduationBootstrap.S(d, "department");
            f.programme = GraduationBootstrap.S(d, "programme");
            f.level = GraduationBootstrap.S(d, "level");
        }
        catch { }
        return f;
    }

    /// <summary>
    /// The WHERE every query on this page shares, so the headline, the tables and the written
    /// summary cannot disagree about which population they describe.
    /// </summary>
    private static string Where(MarksScope scope, Filter f, Dictionary<string, object> p)
    {
        var w = new StringBuilder(" WHERE 1=1 ");

        if (f.lens == "ceremony")
        {
            if (f.ceremony == "none") w.Append(" AND (" + CEREMONY_EXPR + ") IS NULL ");
            else if (f.ceremony != "")
            { w.Append(" AND (" + CEREMONY_EXPR + ")=@cer "); p["@cer"] = f.ceremony; }
        }
        else if (f.acadYear != "")
        { w.Append(" AND g.acadyear=@ay "); p["@ay"] = f.acadYear; }

        if (f.faculty != "") { w.Append(" AND p.faculty_code=@fac "); p["@fac"] = f.faculty; }
        int dep;
        if (f.department != "" && int.TryParse(f.department, out dep))
        { w.Append(" AND p.department_id=@dep "); p["@dep"] = dep; }
        if (f.programme != "") { w.Append(" AND g.progcode=@prog "); p["@prog"] = f.programme; }
        int lvl;
        if (f.level != "" && int.TryParse(f.level, out lvl))
        { w.Append(" AND IFNULL(p.levelCode,0)=@lvl "); p["@lvl"] = lvl; }

        w.Append(scope.ProgFilter("g", "progcode"));
        return w.ToString();
    }

    private const string FROM =
        " FROM acad_graduands g LEFT JOIN acad_programme p ON p.progcode=g.progcode " +
        " LEFT JOIN acad_faculty fc ON TRIM(fc.faculty_code)=TRIM(p.faculty_code) ";

    private static MySqlCommand Cmd(string sql, MySqlConnection c, Dictionary<string, object> p)
    {
        var cmd = new MySqlCommand(sql, c);
        foreach (KeyValuePair<string, object> kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);
        return cmd;
    }

    private static string S(MySqlDataReader r, int i)
    { return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)); }
    private static int I(MySqlDataReader r, int i)
    { return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i)); }
    private static double D(MySqlDataReader r, int i)
    { return r.IsDBNull(i) ? 0.0 : Convert.ToDouble(r.GetValue(i)); }

    /// <summary>The dropdowns, drawn only from values that actually exist in the data.</summary>
    public static object Lists(MarksScope scope)
    {
        var years = new List<string>();
        var ceremonies = new List<object>();
        var faculties = new List<object>();
        var departments = new List<object>();
        var programmes = new List<object>();
        var levels = new List<object>();

        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();
            string pf = scope.ProgFilter("g", "progcode");

            using (var cmd = new MySqlCommand(
                "SELECT DISTINCT g.acadyear FROM acad_graduands g " +
                "WHERE g.acadyear REGEXP '^[0-9]{4}/[0-9]{4}$'" + pf +
                " ORDER BY g.acadyear DESC", c))
            using (var r = cmd.ExecuteReader()) while (r.Read()) years.Add(S(r, 0));

            // Ordinals, with the raw spellings behind each so the label can say how many ways it
            // has been written - a data-quality fact the reader should not have to dig for.
            using (var cmd = new MySqlCommand(
                "SELECT " + CEREMONY_EXPR + " cer, COUNT(*) n, COUNT(DISTINCT g.convocation) spellings " +
                "FROM acad_graduands g WHERE 1=1" + pf +
                " GROUP BY cer ORDER BY cer IS NULL, cer DESC", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    bool none = r.IsDBNull(0);
                    ceremonies.Add(new
                    {
                        v = none ? "none" : S(r, 0),
                        t = (none ? "No ceremony recorded" : Ordinal(I(r, 0)) + " graduation ceremony")
                            + "  (" + I(r, 1) + ")"
                    });
                }

            using (var cmd = new MySqlCommand(
                "SELECT TRIM(fc.faculty_code), fc.faculty_name FROM acad_faculty fc " +
                "WHERE EXISTS (SELECT 1 FROM acad_graduands g JOIN acad_programme p ON p.progcode=g.progcode " +
                "              WHERE TRIM(p.faculty_code)=TRIM(fc.faculty_code)" + pf + ") " +
                "ORDER BY fc.faculty_name", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) faculties.Add(new { v = S(r, 0), t = S(r, 1) });

            using (var cmd = new MySqlCommand(
                "SELECT d.ID, d.dept_name, TRIM(IFNULL(d.faculty_code,'')) FROM hrm_departments d " +
                "WHERE EXISTS (SELECT 1 FROM acad_graduands g JOIN acad_programme p ON p.progcode=g.progcode " +
                "              WHERE p.department_id=d.ID" + pf + ") ORDER BY d.dept_name", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    departments.Add(new { v = S(r, 0), t = S(r, 1), fac = S(r, 2) });

            using (var cmd = new MySqlCommand(
                "SELECT g.progcode, IFNULL(NULLIF(p.progname,''), g.progcode), " +
                " TRIM(IFNULL(p.faculty_code,'')), IFNULL(p.department_id,0), COUNT(*) " +
                FROM + " WHERE 1=1" + pf +
                " GROUP BY g.progcode, p.progname, p.faculty_code, p.department_id ORDER BY 2", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    programmes.Add(new
                    {
                        v = S(r, 0),
                        t = S(r, 1) + "  (" + I(r, 4) + ")",
                        fac = S(r, 2),
                        dep = S(r, 3)
                    });

            using (var cmd = new MySqlCommand(
                "SELECT IFNULL(p.levelCode,0) lvl, COUNT(*) " + FROM + " WHERE 1=1" + pf +
                " GROUP BY lvl ORDER BY lvl", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    levels.Add(new { v = S(r, 0), t = LevelName(I(r, 0)) + "  (" + I(r, 1) + ")" });
        }

        return new
        {
            years = years,
            ceremonies = ceremonies,
            faculties = faculties,
            departments = departments,
            programmes = programmes,
            levels = levels
        };
    }

    public static string Ordinal(int n)
    {
        int last2 = n % 100, last = n % 10;
        string suf = (last2 >= 11 && last2 <= 13) ? "th"
                   : last == 1 ? "st" : last == 2 ? "nd" : last == 3 ? "rd" : "th";
        return n.ToString(CultureInfo.InvariantCulture) + suf;
    }

    /// <summary>Everything the page draws, in one round trip.</summary>
    public static string Analyse(string configJson)
    {
        MarksScope scope;
        try { scope = MarksScopeResolver.Resolve(); }
        catch { return GraduationBootstrap.Denied(); }
        return Analyse(scope, configJson);
    }

    /// <summary>
    /// The same, for a caller that has already resolved the scope - the page resolves it once
    /// for the first paint and would otherwise pay for it twice.
    /// </summary>
    public static string Analyse(MarksScope scope, string configJson)
    {
        try
        {
            if (scope == null || !scope.HasAccess) return GraduationBootstrap.Denied();

            Filter f = Parse(configJson);
            var p = new Dictionary<string, object>();
            string where = Where(scope, f, p);

            var levels = new List<object>();
            var faculties = new List<object>();
            var programmes = new List<object>();
            var trend = new List<object>();
            var spread = new List<object>();
            var notes = new List<string>();

            int total = 0, women = 0, men = 0, top = 0, progCount = 0, facCount = 0, cerCount = 0;
            double avgCgpa = 0, minCgpa = 0, maxCgpa = 0;
            int trReady = 0, trPrinted = 0, trPicked = 0, ceReady = 0, cePrinted = 0, cePicked = 0;

            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();

                // ── headline ──
                using (var cmd = Cmd(
                    "SELECT COUNT(*), SUM(UPPER(g.gender)='FEMALE'), SUM(UPPER(g.gender)='MALE'), " +
                    " AVG(NULLIF(g.cgpa,0)), MIN(NULLIF(g.cgpa,0)), MAX(g.cgpa), " +
                    " COUNT(DISTINCT g.progcode), COUNT(DISTINCT TRIM(IFNULL(p.faculty_code,''))), " +
                    " COUNT(DISTINCT " + CEREMONY_EXPR + "), " +
                    " SUM(UPPER(IFNULL(g.degclass,'')) LIKE '%FIRST CLASS%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%CLASS I (%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%DISTINCTION%'), " +
                    " SUM(g.trans_status='Ready'), SUM(g.trans_status='Printed'), SUM(g.trans_status='Picked'), " +
                    " SUM(g.cert_status='Ready'), SUM(g.cert_status='Printed'), SUM(g.cert_status='Picked') " +
                    FROM + where, c, p))
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        total = I(r, 0); women = I(r, 1); men = I(r, 2);
                        avgCgpa = D(r, 3); minCgpa = D(r, 4); maxCgpa = D(r, 5);
                        progCount = I(r, 6); facCount = I(r, 7); cerCount = I(r, 8);
                        top = I(r, 9);
                        trReady = I(r, 10); trPrinted = I(r, 11); trPicked = I(r, 12);
                        ceReady = I(r, 13); cePrinted = I(r, 14); cePicked = I(r, 15);
                    }

                // ── by award level, each in its own vocabulary ──
                var byLevel = new List<int[]>();
                var levelClasses = new Dictionary<int, List<object>>();
                using (var cmd = Cmd(
                    "SELECT IFNULL(p.levelCode,0) lvl, IFNULL(NULLIF(TRIM(g.degclass),''),'(not recorded)') cls, " +
                    " COUNT(*), AVG(NULLIF(g.cgpa,0)) " + FROM + where +
                    " GROUP BY lvl, cls ORDER BY lvl, COUNT(*) DESC", c, p))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        int lvl = I(r, 0);
                        if (!levelClasses.ContainsKey(lvl)) levelClasses[lvl] = new List<object>();
                        levelClasses[lvl].Add(new
                        {
                            name = S(r, 1),
                            n = I(r, 2),
                            avgCgpa = Math.Round(D(r, 3), 2),
                            top = IsTop(S(r, 1))
                        });
                    }

                using (var cmd = Cmd(
                    "SELECT IFNULL(p.levelCode,0) lvl, COUNT(*), AVG(NULLIF(g.cgpa,0)), " +
                    " SUM(UPPER(g.gender)='FEMALE'), SUM(UPPER(g.gender)='MALE') " +
                    FROM + where + " GROUP BY lvl ORDER BY lvl", c, p))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        int lvl = I(r, 0);
                        levels.Add(new
                        {
                            level = lvl,
                            name = LevelName(lvl),
                            n = I(r, 1),
                            avgCgpa = Math.Round(D(r, 2), 2),
                            women = I(r, 3),
                            men = I(r, 4),
                            classes = levelClasses.ContainsKey(lvl) ? levelClasses[lvl] : new List<object>()
                        });
                    }

                // ── by faculty ──
                using (var cmd = Cmd(
                    "SELECT IFNULL(NULLIF(TRIM(fc.faculty_name),''),'(no faculty recorded)') fac, " +
                    " COUNT(*), AVG(NULLIF(g.cgpa,0)), SUM(UPPER(g.gender)='FEMALE'), " +
                    " SUM(UPPER(g.gender)='MALE'), COUNT(DISTINCT g.progcode), " +
                    " SUM(UPPER(IFNULL(g.degclass,'')) LIKE '%FIRST CLASS%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%CLASS I (%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%DISTINCTION%') " +
                    FROM + where + " GROUP BY fac ORDER BY COUNT(*) DESC", c, p))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        faculties.Add(new
                        {
                            name = S(r, 0), n = I(r, 1), avgCgpa = Math.Round(D(r, 2), 2),
                            women = I(r, 3), men = I(r, 4), progs = I(r, 5), top = I(r, 6)
                        });

                // ── by programme ──
                using (var cmd = Cmd(
                    "SELECT g.progcode, IFNULL(NULLIF(p.progname,''), g.progcode), " +
                    " IFNULL(NULLIF(TRIM(fc.faculty_name),''),'') , IFNULL(p.levelCode,0), " +
                    " COUNT(*), AVG(NULLIF(g.cgpa,0)), SUM(UPPER(g.gender)='FEMALE'), " +
                    " SUM(UPPER(g.gender)='MALE'), " +
                    " SUM(UPPER(IFNULL(g.degclass,'')) LIKE '%FIRST CLASS%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%CLASS I (%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%DISTINCTION%') " +
                    FROM + where +
                    " GROUP BY g.progcode, p.progname, fc.faculty_name, p.levelCode " +
                    " ORDER BY COUNT(*) DESC, 2", c, p))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        programmes.Add(new
                        {
                            code = S(r, 0), name = S(r, 1), faculty = S(r, 2),
                            level = LevelName(I(r, 3)), n = I(r, 4),
                            avgCgpa = Math.Round(D(r, 5), 2),
                            women = I(r, 6), men = I(r, 7), top = I(r, 8)
                        });

                // ── the trend, which deliberately IGNORES the year/ceremony narrowing: a series
                //    that only ever showed the selected year would be a single bar.
                var p2 = new Dictionary<string, object>();
                var w2 = new StringBuilder(" WHERE g.acadyear REGEXP '^[0-9]{4}/[0-9]{4}$' ");
                if (f.faculty != "") { w2.Append(" AND p.faculty_code=@fac "); p2["@fac"] = f.faculty; }
                int dep2;
                if (f.department != "" && int.TryParse(f.department, out dep2))
                { w2.Append(" AND p.department_id=@dep "); p2["@dep"] = dep2; }
                if (f.programme != "") { w2.Append(" AND g.progcode=@prog "); p2["@prog"] = f.programme; }
                int lvl2;
                if (f.level != "" && int.TryParse(f.level, out lvl2))
                { w2.Append(" AND IFNULL(p.levelCode,0)=@lvl "); p2["@lvl"] = lvl2; }
                w2.Append(scope.ProgFilter("g", "progcode"));

                using (var cmd = Cmd(
                    "SELECT g.acadyear, COUNT(*), AVG(NULLIF(g.cgpa,0)), " +
                    " SUM(UPPER(g.gender)='FEMALE'), SUM(UPPER(g.gender)='MALE'), " +
                    " SUM(UPPER(IFNULL(g.degclass,'')) LIKE '%FIRST CLASS%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%CLASS I (%' " +
                    "     OR UPPER(IFNULL(g.degclass,'')) LIKE '%DISTINCTION%') " +
                    FROM + w2 + " GROUP BY g.acadyear ORDER BY g.acadyear", c, p2))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        trend.Add(new
                        {
                            year = S(r, 0), n = I(r, 1), avgCgpa = Math.Round(D(r, 2), 2),
                            women = I(r, 3), men = I(r, 4), top = I(r, 5)
                        });

                // ── where this ceremony's graduands completed. The whole point of separating the
                //    two lenses, so it is shown whenever the ceremony lens is in force.
                if (f.lens == "ceremony")
                {
                    using (var cmd = Cmd(
                        "SELECT IFNULL(NULLIF(g.acadyear,''),'(not recorded)'), COUNT(*) " +
                        FROM + where + " GROUP BY 1 ORDER BY 1 DESC", c, p))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) spread.Add(new { year = S(r, 0), n = I(r, 1) });
                }

                // ── what the reader should not have to discover for themselves ──
                notes.AddRange(Notes(c, scope, where, p));
            }

            string lensLabel = f.lens == "ceremony"
                ? (f.ceremony == "none" ? "Graduands with no ceremony recorded"
                   : f.ceremony == "" ? "Every ceremony"
                   : "The " + Ordinal(int.Parse(f.ceremony)) + " graduation ceremony")
                : (f.acadYear == "" ? "Every academic year" : "Academic year " + f.acadYear);

            return J.Serialize(new
            {
                success = true,
                scopeLabel = scope.Label,
                roleNote = scope.RoleNote,
                lensLabel = lensLabel,
                headline = new
                {
                    graduands = total, women = women, men = men,
                    womenPct = total > 0 ? (int)Math.Round(women * 100.0 / total) : 0,
                    avgCgpa = Math.Round(avgCgpa, 2),
                    minCgpa = Math.Round(minCgpa, 2), maxCgpa = Math.Round(maxCgpa, 2),
                    programmes = progCount, faculties = facCount, ceremonies = cerCount,
                    top = top,
                    topPct = total > 0 ? (int)Math.Round(top * 100.0 / total) : 0
                },
                documents = new
                {
                    transReady = trReady, transPrinted = trPrinted, transPicked = trPicked,
                    certReady = ceReady, certPrinted = cePrinted, certPicked = cePicked
                },
                levels = levels,
                faculties = faculties,
                programmes = programmes,
                trend = trend,
                spread = spread,
                notes = notes
            });
        }
        catch (Exception ex)
        {
            return J.Serialize(new { success = false, message = ex.Message });
        }
    }

    // =================================================================
    //  The written summary.
    //
    //  Composed from the SAME queries the page draws, so a sentence and
    //  the table above it can never disagree. Every figure is one that
    //  came back from the database; nothing is rounded for convenience,
    //  interpolated, or carried over from a previous run. Where a figure
    //  is not available the sentence says so rather than guessing, and
    //  the awkward parts - a duplicate, a missing year, a class spelt
    //  two ways - are stated rather than smoothed over.
    //
    //  It is meant to be pasted into a Senate or Council paper, which is
    //  why it reads as prose and carries its own provenance line.
    // =================================================================

    public class Para
    {
        public string head = "";
        public List<string> lines = new List<string>();
        public Para(string h) { head = h; }
        public Para Add(string t) { if (!string.IsNullOrEmpty(t)) lines.Add(t); return this; }
    }

    private static string N(int v) { return v.ToString("#,##0", CultureInfo.InvariantCulture); }
    private static string N2(double v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

    private static string Pct(int part, int whole)
    {
        if (whole <= 0) return "NOT AVAILABLE";
        return ((int)Math.Round(part * 100.0 / whole)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>The brief, as ordered paragraphs the page renders and the PDF prints.</summary>
    public static string Summarise(string configJson)
    {
        MarksScope scope;
        try { scope = MarksScopeResolver.Resolve(); }
        catch { return GraduationBootstrap.Denied(); }
        return Summarise(scope, configJson);
    }

    public static string Summarise(MarksScope scope, string configJson)
    {
        try
        {
            if (scope == null || !scope.HasAccess) return GraduationBootstrap.Denied();

            string raw = Analyse(scope, configJson);
            var d = J.Deserialize<Dictionary<string, object>>(raw);
            object ok;
            if (!d.TryGetValue("success", out ok) || !Convert.ToBoolean(ok)) return raw;

            Filter f = Parse(configJson);
            var H = (Dictionary<string, object>)d["headline"];
            int total = Convert.ToInt32(H["graduands"]);
            string lens = Convert.ToString(d["lensLabel"]);

            var paras = new List<Para>();

            // ── 1. what this covers ──
            var p1 = new Para("What this covers");
            if (total == 0)
            {
                p1.Add("No graduand on record matches this selection (" + lens + "), so there is " +
                       "nothing to summarise. Widen the filters and generate it again.");
                paras.Add(p1);
                return J.Serialize(new { success = true, title = "Graduation summary",
                                         subtitle = lens, scopeLabel = scope.Label, paragraphs = paras });
            }

            p1.Add(lens + ", covering " + N(total) + " graduand" + (total == 1 ? "" : "s") +
                   " across " + N(Convert.ToInt32(H["programmes"])) + " programme" +
                   (Convert.ToInt32(H["programmes"]) == 1 ? "" : "s") + " in " +
                   N(Convert.ToInt32(H["faculties"])) + " facult" +
                   (Convert.ToInt32(H["faculties"]) == 1 ? "y" : "ies") + ".");
            p1.Add("Produced for " + scope.Label + " on " +
                   DateTime.Now.ToString("dddd, d MMMM yyyy 'at' HH:mm") +
                   ", from acad_graduands, which is the system of record for who has graduated.");
            if (f.lens == "ceremony" && Convert.ToInt32(H["ceremonies"]) > 0)
                p1.Add("A convocation is not an academic year: people are capped at the next " +
                       "ceremony after they complete, whenever that falls. The spread of " +
                       "completion years behind this ceremony is set out below.");
            paras.Add(p1);

            // ── 2. the awards ──
            var lv = (System.Collections.ArrayList)d["levels"];
            var p2 = new Para("The awards");
            if (lv.Count > 1)
                p2.Add("These graduands hold " + lv.Count + " different levels of award, and the " +
                       "class of award means a different thing at each: certificates and diplomas " +
                       "are classed Class I to Class III, bachelor's degrees and above First to " +
                       "Third Class. They are reported separately below for that reason, and must " +
                       "not be added together.");
            foreach (Dictionary<string, object> L in lv)
            {
                int n = Convert.ToInt32(L["n"]);
                var cls = (System.Collections.ArrayList)L["classes"];
                var sb = new StringBuilder();
                sb.Append(Convert.ToString(L["name"])).Append(": ").Append(N(n))
                  .Append(n == 1 ? " graduand" : " graduands");
                double ac = Convert.ToDouble(L["avgCgpa"]);
                if (ac > 0) sb.Append(", mean CGPA ").Append(N2(ac));
                sb.Append(". ");
                var bits = new List<string>();
                foreach (Dictionary<string, object> cc in cls)
                {
                    int cn = Convert.ToInt32(cc["n"]);
                    bits.Add(Convert.ToString(cc["name"]) + " " + N(cn) + " (" + Pct(cn, n) + ")");
                }
                sb.Append(string.Join("; ", bits.ToArray())).Append(".");
                p2.Add(sb.ToString());
            }
            paras.Add(p2);

            // ── 3. who they are ──
            int women = Convert.ToInt32(H["women"]), men = Convert.ToInt32(H["men"]);
            var p3 = new Para("Who they are");
            if (women + men > 0)
            {
                p3.Add(N(women) + " women and " + N(men) + " men \u2014 " + Pct(women, total) +
                       " of this cohort is female" +
                       (women + men < total ? ", with " + N(total - women - men) +
                        " whose gender is not recorded" : "") + ".");
                if (women > men)
                    p3.Add("Women outnumber men here. That holds across every academic year on " +
                           "record at this university, not only this selection.");
            }
            else p3.Add("Gender is NOT AVAILABLE IN SYSTEM for this selection.");

            double avg = Convert.ToDouble(H["avgCgpa"]);
            if (avg > 0)
                p3.Add("Mean CGPA " + N2(avg) + ", ranging from " + N2(Convert.ToDouble(H["minCgpa"])) +
                       " to " + N2(Convert.ToDouble(H["maxCgpa"])) + ".");
            int top = Convert.ToInt32(H["top"]);
            p3.Add(N(top) + " graduand" + (top == 1 ? "" : "s") + " \u2014 " + Pct(top, total) +
                   " \u2014 took the highest class their award offers (First Class, or Class I " +
                   "Distinction for a certificate or diploma).");
            paras.Add(p3);

            // ── 4. where they came from ──
            var facs = (System.Collections.ArrayList)d["faculties"];
            if (facs.Count > 0)
            {
                var p4 = new Para("Where they came from");
                foreach (Dictionary<string, object> F in facs)
                {
                    int n = Convert.ToInt32(F["n"]);
                    var sb = new StringBuilder();
                    sb.Append(Convert.ToString(F["name"])).Append(": ").Append(N(n))
                      .Append(" (").Append(Pct(n, total)).Append(" of the cohort) from ")
                      .Append(Convert.ToInt32(F["progs"])).Append(" programme")
                      .Append(Convert.ToInt32(F["progs"]) == 1 ? "" : "s");
                    double fa = Convert.ToDouble(F["avgCgpa"]);
                    if (fa > 0) sb.Append(", mean CGPA ").Append(N2(fa));
                    sb.Append(", ").Append(Pct(Convert.ToInt32(F["women"]), n)).Append(" women.");
                    p4.Add(sb.ToString());
                }
                paras.Add(p4);
            }

            // ── 5. the largest programmes ──
            var progs = (System.Collections.ArrayList)d["programmes"];
            if (progs.Count > 0)
            {
                var p5 = new Para(progs.Count <= 8 ? "By programme" : "The largest programmes");
                int shown = 0;
                foreach (Dictionary<string, object> P in progs)
                {
                    if (shown++ >= 8) break;
                    int n = Convert.ToInt32(P["n"]);
                    double pa = Convert.ToDouble(P["avgCgpa"]);
                    p5.Add(Convert.ToString(P["name"]) + " (" + Convert.ToString(P["code"]) + "): " +
                           N(n) + (n == 1 ? " graduand" : " graduands") +
                           (pa > 0 ? ", mean CGPA " + N2(pa) : "") + ", " +
                           N(Convert.ToInt32(P["top"])) + " in the top class.");
                }
                if (progs.Count > 8)
                    p5.Add("A further " + N(progs.Count - 8) + " programme" +
                           (progs.Count - 8 == 1 ? "" : "s") + " contributed the remainder; the " +
                           "full table is on the screen this was generated from.");
                paras.Add(p5);
            }

            // ── 6. the ceremony spread ──
            var spread = (System.Collections.ArrayList)d["spread"];
            if (spread.Count > 0)
            {
                var p6 = new Para("When these graduands completed");
                var bits = new List<string>();
                foreach (Dictionary<string, object> Sp in spread)
                    bits.Add(Convert.ToString(Sp["year"]) + ": " + N(Convert.ToInt32(Sp["n"])));
                p6.Add("This ceremony draws from " + spread.Count + " academic year" +
                       (spread.Count == 1 ? "" : "s") + " \u2014 " +
                       string.Join("; ", bits.ToArray()) + ".");
                paras.Add(p6);
            }

            // ── 7. documents ──
            var doc = (Dictionary<string, object>)d["documents"];
            int tp = Convert.ToInt32(doc["transPrinted"]), tk = Convert.ToInt32(doc["transPicked"]);
            int cp = Convert.ToInt32(doc["certPrinted"]), ck = Convert.ToInt32(doc["certPicked"]);
            var p7 = new Para("Documents");
            p7.Add("Transcripts: " + N(tp) + " printed and " + N(tk) + " collected, of " + N(total) +
                   ". " + N(total - tp - tk) + " not yet printed.");
            p7.Add("Certificates: " + N(cp) + " printed and " + N(ck) + " collected, of " + N(total) +
                   ". " + N(total - cp - ck) + " not yet printed.");
            paras.Add(p7);

            // ── 8. the caveats, never omitted ──
            var notes = (System.Collections.ArrayList)d["notes"];
            var p8 = new Para("What to be careful of in these figures");
            if (notes.Count == 0)
                p8.Add("Nothing in this selection is missing an academic year, a class of award, a " +
                       "gender or a CGPA, and no student number appears twice. The figures above " +
                       "need no qualification.");
            else foreach (object o in notes) p8.Add(Convert.ToString(o));
            paras.Add(p8);

            return J.Serialize(new
            {
                success = true,
                title = "Graduation summary",
                subtitle = lens,
                scopeLabel = scope.Label,
                generated = DateTime.Now.ToString("d MMM yyyy HH:mm"),
                paragraphs = paras
            });
        }
        catch (Exception ex)
        {
            return J.Serialize(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Findings about the data itself, for the selection in force.
    ///
    /// Stated on the page rather than left to be discovered. A number nobody has told you is
    /// approximate is a number you will quote in a meeting.
    /// </summary>
    private static List<string> Notes(MySqlConnection c, MarksScope scope, string where,
                                      Dictionary<string, object> p)
    {
        var outp = new List<string>();
        try
        {
            using (var cmd = Cmd(
                "SELECT SUM(TRIM(IFNULL(g.acadyear,''))='') , " +
                " SUM(TRIM(IFNULL(g.convocation,'')) IN ('','-')), " +
                " SUM(UPPER(TRIM(IFNULL(g.degclass,'')))='N/A'), " +
                " SUM(TRIM(IFNULL(g.gender,''))=''), " +
                " SUM(IFNULL(g.cgpa,0)<=0), " +
                " COUNT(*)-COUNT(DISTINCT g.regno) " +
                FROM + where, c, p))
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    int noYear = I(r, 0), noCer = I(r, 1), naClass = I(r, 2),
                        noSex = I(r, 3), noCgpa = I(r, 4), dupes = I(r, 5);

                    if (noYear > 0)
                        outp.Add(noYear + (noYear == 1 ? " graduand has" : " graduands have") +
                                 " no academic year recorded, so they appear under every ceremony but in no year.");
                    if (noCer > 0)
                        outp.Add(noCer + (noCer == 1 ? " graduand has" : " graduands have") +
                                 " no convocation recorded and cannot be attributed to a ceremony.");
                    if (naClass > 0)
                        outp.Add(naClass + (naClass == 1 ? " graduand carries" : " graduands carry") +
                                 " a class of award of \"N/A\" and is excluded from the class distribution.");
                    if (noSex > 0)
                        outp.Add(noSex + " with no gender recorded, so the split below does not sum to the total.");
                    if (noCgpa > 0)
                        outp.Add(noCgpa + " with no CGPA, excluded from every average on this page.");
                    if (dupes > 0)
                        outp.Add(dupes + (dupes == 1 ? " student number appears" : " student numbers appear") +
                                 " more than once on the graduation list. That is a Registrar decision, not a display fault.");
                }

            // Spelling variants of the same class, which quietly split a distribution in two.
            using (var cmd = Cmd(
                "SELECT COUNT(DISTINCT g.degclass) FROM acad_graduands g " +
                "LEFT JOIN acad_programme p ON p.progcode=g.progcode " +
                "LEFT JOIN acad_faculty fc ON TRIM(fc.faculty_code)=TRIM(p.faculty_code) " +
                where.Substring(where.IndexOf("WHERE")) +
                " AND UPPER(REPLACE(REPLACE(g.degclass,' ',''),'-','')) IN " +
                " (SELECT UPPER(REPLACE(REPLACE(g2.degclass,' ',''),'-','')) FROM acad_graduands g2 " +
                "  GROUP BY UPPER(REPLACE(REPLACE(g2.degclass,' ',''),'-','')) " +
                "  HAVING COUNT(DISTINCT g2.degclass) > 1)", c, p))
            {
                object o = cmd.ExecuteScalar();
                int variants = o == null || o == DBNull.Value ? 0 : Convert.ToInt32(o);
                if (variants > 1)
                    outp.Add("The same class of award is spelt " + variants + " different ways in this " +
                             "selection, so one class is split across more than one row wherever the " +
                             "distribution is broken down.");
            }
        }
        catch { /* a note is a courtesy; never fail the page over one */ }
        return outp;
    }
}
