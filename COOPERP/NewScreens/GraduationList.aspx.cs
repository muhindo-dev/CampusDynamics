using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using System.Web.Services;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — the graduation list.
//
//  The output of the module: the acad_graduands rows for a year, with
//  who cleared each name and when, which nothing recorded before this.
//  An independent page carrying only its own endpoints.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationList : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    private class Row
    {
        public string regno = "", name = "", progcode = "", progname = "", faculty = "";
        public double cgpa;
        public string degclass = "", year = "", gender = "", nationality = "";
        public string transStatus = "", certStatus = "", clearedBy = "", clearedAt = "";
    }

    private static List<Row> Fetch(MarksScope scope, GraduationEngine.GradFilter f)
    {
        var rows = new List<Row>();
        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();
            string w = " WHERE 1=1 ";
            var names = new List<string>();
            if (f.acadYear != "") { w += " AND g.acadyear=@ay "; names.Add("@ay"); }
            if (f.faculty != "") { w += " AND p.faculty_code=@fac "; names.Add("@fac"); }
            int dep;
            if (f.department != "" && int.TryParse(f.department, out dep)) { w += " AND p.department_id=@dep "; names.Add("@dep"); }
            if (f.programme != "") { w += " AND g.progcode=@prog "; names.Add("@prog"); }
            if (f.search != "") { w += " AND (g.regno LIKE @q OR g.stud_name LIKE @q) "; names.Add("@q"); }
            w += scope.ProgFilter("g", "progcode");

            using (var cmd = new MySqlCommand(
                "SELECT g.regno, g.stud_name, g.progcode, IFNULL(p.progname,''), IFNULL(p.faculty_code,''), " +
                " g.cgpa, g.degclass, g.acadyear, IFNULL(g.gender,''), IFNULL(g.nationality,''), " +
                " IFNULL(g.trans_status,''), IFNULL(g.cert_status,''), " +
                " IFNULL((SELECT v.actor FROM acad_grad_review v WHERE v.regno=g.regno AND v.verdict='CLEARED' " +
                "         ORDER BY v.id DESC LIMIT 1),''), " +
                " IFNULL((SELECT DATE_FORMAT(v.created_at,'%e %b %Y') FROM acad_grad_review v " +
                "         WHERE v.regno=g.regno AND v.verdict='CLEARED' ORDER BY v.id DESC LIMIT 1),'') " +
                "FROM acad_graduands g LEFT JOIN acad_programme p ON p.progcode=g.progcode " +
                w + " ORDER BY p.progname, g.stud_name LIMIT 5000", c))
            {
                if (names.Contains("@ay")) cmd.Parameters.AddWithValue("@ay", f.acadYear);
                if (names.Contains("@fac")) cmd.Parameters.AddWithValue("@fac", f.faculty);
                if (names.Contains("@dep")) cmd.Parameters.AddWithValue("@dep", int.Parse(f.department));
                if (names.Contains("@prog")) cmd.Parameters.AddWithValue("@prog", f.programme);
                if (names.Contains("@q")) cmd.Parameters.AddWithValue("@q", "%" + f.search + "%");
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        var x = new Row();
                        x.regno = r[0].ToString(); x.name = r[1].ToString(); x.progcode = r[2].ToString();
                        x.progname = r[3].ToString(); x.faculty = r[4].ToString();
                        x.cgpa = Convert.ToDouble(r[5]); x.degclass = r[6].ToString(); x.year = r[7].ToString();
                        x.gender = r[8].ToString(); x.nationality = r[9].ToString();
                        x.transStatus = r[10].ToString(); x.certStatus = r[11].ToString();
                        x.clearedBy = r[12].ToString(); x.clearedAt = r[13].ToString();
                        rows.Add(x);
                    }
            }
        }
        return rows;
    }

    private const bool WITH_COUNTS = false;

    /// <summary>
    /// The filter lists, rendered into the page rather than fetched.
    ///
    /// This page used to open by firing GetBootstrap and waiting before it could even draw its
    /// controls. The answer is available at render time, so it ships with the page. AJAX is
    /// kept for the filter changes that follow.
    /// </summary>
    public string BootJson = "null";
    public string StatsAge = "";

    private void Prime()
    {
        try
        {
            MarksScope sc = MarksScopeResolver.Resolve();
            StatsAge = GraduationStats.Freshness();
            if (sc.HasAccess)
                BootJson = GraduationBootstrap.ForScriptBlock(GraduationBootstrap.Bootstrap(WITH_COUNTS));
        }
        catch { BootJson = "null"; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) { Prime(); return; }

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");
        List<Row> rows = Fetch(scope, f);

        var sheet = new GraduationExport.Sheet();
        sheet.Name = "Graduation list";
        sheet.Subtitle = f.acadYear == "" ? "all years" : f.acadYear;
        sheet.Columns = new[] { "#", "Student Number", "Name", "Programme Code", "Programme", "Faculty",
                                "CGPA", "Class of Award", "Gender", "Nationality", "Graduation Year",
                                "Cleared By", "Cleared On", "Transcript", "Certificate" };
        sheet.NumericColumns.Add(0);
        sheet.NumericColumns.Add(6);

        // Numbered within programme, the way a graduation list is read out and signed off.
        string lastProg = null;
        int n = 0;
        foreach (Row r in rows)
        {
            string key = r.progname == "" ? r.progcode : r.progname;
            if (key != lastProg) { lastProg = key; n = 0; }
            n++;
            sheet.Rows.Add(new[]
            {
                n.ToString(), r.regno, r.name, r.progcode, r.progname, r.faculty,
                r.cgpa.ToString("F2"), r.degclass, r.gender, r.nationality, r.year,
                r.clearedBy == "" ? "(before this module)" : r.clearedBy, r.clearedAt,
                r.transStatus, r.certStatus
            });
        }

        // A second tab that summarises the list the way a Senate paper opens.
        var by = new GraduationExport.Sheet();
        by.Name = "By programme";
        by.Columns = new[] { "Programme", "Candidates", "First Class / Distinction", "Other classes" };
        for (int i = 1; i <= 3; i++) by.NumericColumns.Add(i);
        var tally = new Dictionary<string, int[]>();
        var order = new List<string>();
        foreach (Row r in rows)
        {
            string key = r.progname == "" ? r.progcode : r.progname;
            if (!tally.ContainsKey(key)) { tally[key] = new int[2]; order.Add(key); }
            tally[key][0]++;
            if (r.degclass.IndexOf("First", StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.degclass.IndexOf("Distinction", StringComparison.OrdinalIgnoreCase) >= 0)
                tally[key][1]++;
        }
        order.Sort();
        foreach (string k in order)
            by.Rows.Add(new[] { k, tally[k][0].ToString(), tally[k][1].ToString(),
                                (tally[k][0] - tally[k][1]).ToString() });

        var cover = GraduationBootstrap.CoverOf(f, "");
        cover.Add(new KeyValuePair<string, string>("Names on this list", rows.Count.ToString()));
        string file = GraduationExport.FileName("graduation-list", f.acadYear);
        if (fmt == "csv")
            GraduationExport.Csv(Response, file, "Graduation List", scope.Label, cover, sheet.Columns, sheet.Rows);
        else
            GraduationExport.Workbook(Response, file, "Graduation List", scope.Label, cover,
                                      new List<GraduationExport.Sheet> { sheet, by });
    }

    [WebMethod(EnableSession = true)]
    public static string GetBootstrap() { return GraduationBootstrap.Bootstrap(false); }

    [WebMethod(EnableSession = true)]
    public static string GetList(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            List<Row> rows = Fetch(scope, GraduationBootstrap.Parse(configJson));
            return J.Serialize(new { success = true, rows = rows, total = rows.Count });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetStudent(string regno) { return GraduationStudent.Detail(regno); }

    [WebMethod(EnableSession = true)]
    public static string RemoveStudent(string regno, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.RemoveFromList(scope, regno, reason);
    }
}
