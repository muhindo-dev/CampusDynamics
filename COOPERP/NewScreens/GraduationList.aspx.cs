using System;
using System.Collections.Generic;
using System.Globalization;
using System.Configuration;
using System.Web.Script.Serialization;
using System.Web.Services;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre, the graduation list.
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
        /// <summary>Position within the student's own programme, the way a graduation list is
        /// read out and signed off. Filled in once the rows are in their final order.</summary>
        public int seq;
    }

    /// <summary>Ceiling on one export. The largest list on record is 599 names.</summary>
    private const int EXPORT_CAP = 20000;

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
            // Matched as a prefix so "First" catches "First Class Honours" and "First Class" alike.
            if (f.award != "") { w += " AND g.degclass LIKE @aw "; names.Add("@aw"); }
            w += scope.ProgFilter("g", "progcode");

            string order;
            switch (f.sort)
            {
                case "cgpa": order = " ORDER BY g.cgpa DESC, g.stud_name "; break;
                case "class": order = " ORDER BY g.degclass, g.cgpa DESC, g.stud_name "; break;
                case "regno": order = " ORDER BY g.regno "; break;
                case "name": order = " ORDER BY g.stud_name "; break;
                default: order = " ORDER BY p.progname, g.stud_name "; break;
            }

            using (var cmd = new MySqlCommand(
                "SELECT g.regno, g.stud_name, g.progcode, IFNULL(p.progname,''), IFNULL(p.faculty_code,''), " +
                " g.cgpa, g.degclass, g.acadyear, IFNULL(g.gender,''), IFNULL(g.nationality,''), " +
                " IFNULL(g.trans_status,''), IFNULL(g.cert_status,''), " +
                " IFNULL((SELECT v.actor FROM acad_grad_review v WHERE v.regno=g.regno AND v.verdict='CLEARED' " +
                "         ORDER BY v.id DESC LIMIT 1),''), " +
                " IFNULL((SELECT DATE_FORMAT(v.created_at,'%e %b %Y') FROM acad_grad_review v " +
                "         WHERE v.regno=g.regno AND v.verdict='CLEARED' ORDER BY v.id DESC LIMIT 1),'') " +
                "FROM acad_graduands g LEFT JOIN acad_programme p ON p.progcode=g.progcode " +
                w + order + " LIMIT " + (EXPORT_CAP + 1), c))
            {
                if (names.Contains("@ay")) cmd.Parameters.AddWithValue("@ay", f.acadYear);
                if (names.Contains("@fac")) cmd.Parameters.AddWithValue("@fac", f.faculty);
                if (names.Contains("@dep")) cmd.Parameters.AddWithValue("@dep", int.Parse(f.department));
                if (names.Contains("@prog")) cmd.Parameters.AddWithValue("@prog", f.programme);
                if (names.Contains("@q")) cmd.Parameters.AddWithValue("@q", "%" + f.search + "%");
                if (names.Contains("@aw")) cmd.Parameters.AddWithValue("@aw", f.award + "%");
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

    /// <summary>The export column catalogue, so the dialog's checkboxes and the workbook's
    /// columns come from one list.</summary>
    public string ColsJson = "[]";

    private void Prime()
    {
        try
        {
            MarksScope sc = MarksScopeResolver.Resolve();
            StatsAge = GraduationStats.Freshness();
            ColsJson = GraduationBootstrap.ForScriptBlock(J.Serialize(GraduationExport.Catalogue(Catalogue())));
            if (sc.HasAccess)
                BootJson = GraduationBootstrap.ForScriptBlock(GraduationBootstrap.Bootstrap(WITH_COUNTS));
        }
        catch { BootJson = "null"; }
    }

    // =================================================================
    //  The column catalogue, one list behind both the export dialog's
    //  checkboxes and the workbook that comes back.
    // =================================================================
    private const string ID = "Identity", AW = "The award", GV = "Who signed it off", DOC = "Documents";

    private static List<GraduationExport.Col<Row>> Catalogue()
    {
        var c = new List<GraduationExport.Col<Row>>();
        c.Add(new GraduationExport.Col<Row>("seq", "#", ID, true, F_seq));
        c.Add(new GraduationExport.Col<Row>("regno", "Student Number", ID, F_regno));
        c.Add(new GraduationExport.Col<Row>("name", "Name", ID, F_name));
        c.Add(new GraduationExport.Col<Row>("progcode", "Programme Code", ID, F_progcode));
        c.Add(new GraduationExport.Col<Row>("progname", "Programme", ID, F_progname));
        c.Add(new GraduationExport.Col<Row>("faculty", "Faculty", ID, F_faculty));
        c.Add(new GraduationExport.Col<Row>("gender", "Gender", ID, F_gender));
        c.Add(new GraduationExport.Col<Row>("nat", "Nationality", ID, F_nat).Off());

        c.Add(new GraduationExport.Col<Row>("cgpa", "CGPA", AW, true, F_cgpa));
        c.Add(new GraduationExport.Col<Row>("class", "Class of Award", AW, F_class));
        c.Add(new GraduationExport.Col<Row>("year", "Graduation Year", AW, F_year));

        c.Add(new GraduationExport.Col<Row>("by", "Cleared By", GV, F_by));
        c.Add(new GraduationExport.Col<Row>("on", "Cleared On", GV, F_on));

        c.Add(new GraduationExport.Col<Row>("trans", "Transcript", DOC, F_trans).Off());
        c.Add(new GraduationExport.Col<Row>("cert", "Certificate", DOC, F_cert).Off());
        return c;
    }

    private static string F_seq(Row r) { return r.seq.ToString(CultureInfo.InvariantCulture); }
    private static string F_regno(Row r) { return r.regno; }
    private static string F_name(Row r) { return r.name; }
    private static string F_progcode(Row r) { return r.progcode; }
    private static string F_progname(Row r) { return r.progname; }
    private static string F_faculty(Row r) { return r.faculty; }
    private static string F_gender(Row r) { return r.gender; }
    private static string F_nat(Row r) { return r.nationality; }
    private static string F_cgpa(Row r) { return r.cgpa.ToString("F2", CultureInfo.InvariantCulture); }
    private static string F_class(Row r) { return r.degclass; }
    private static string F_year(Row r) { return r.year; }
    private static string F_by(Row r) { return r.clearedBy == "" ? "(before this module)" : r.clearedBy; }
    private static string F_on(Row r) { return r.clearedAt; }
    private static string F_trans(Row r) { return r.transStatus; }
    private static string F_cert(Row r) { return r.certStatus; }

    protected void Page_Load(object sender, EventArgs e)
    {
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) { Prime(); return; }

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");

        // The export dialog's "order by" is authoritative for a file, whatever the screen was
        // showing. Fetch already knows how to order; it just needs telling.
        if (f.orderBy != "") f.sort = f.orderBy;
        List<Row> rows = Fetch(scope, f);

        bool truncated = rows.Count > EXPORT_CAP;
        if (truncated) rows.RemoveRange(EXPORT_CAP, rows.Count - EXPORT_CAP);
        GraduationExport.Truncation = truncated
            ? ("More than " + EXPORT_CAP.ToString(CultureInfo.InvariantCulture) +
               " names matched. This file carries the first " +
               EXPORT_CAP.ToString(CultureInfo.InvariantCulture) + ".")
            : null;

        // Grouping is a stable second pass over the already-ordered rows, so the chosen order
        // survives inside each programme.
        if (f.groupBy != "") GroupSort(rows, f.groupBy);

        // Numbered within programme, the way a graduation list is read out and signed off. Done
        // after both, because the number means "nth in this programme on this list".
        string lastProg = null;
        int n = 0;
        foreach (Row r in rows)
        {
            string key = r.progname == "" ? r.progcode : r.progname;
            if (key != lastProg) { lastProg = key; n = 0; }
            r.seq = ++n;
        }

        GraduationExport.Sheet sheet = GraduationExport.Build(
            "Graduation list", f.acadYear == "" ? "all years" : f.acadYear,
            Catalogue(), rows, Request.Form["gradCols"]);

        var sheets = new List<GraduationExport.Sheet> { sheet };
        string want = Request.Form["gradSheets"] ?? "";
        if (GraduationExport.Wants(want, "byprog")) sheets.Add(ByProgramme(rows));
        if (GraduationExport.Wants(want, "byclass")) sheets.Add(ByClass(rows));
        if (GraduationExport.Wants(want, "bywho")) sheets.Add(ByApprover(rows));

        var cover = GraduationBootstrap.CoverOf(f, "");
        cover.Add(new KeyValuePair<string, string>("Names on this list",
            rows.Count.ToString(CultureInfo.InvariantCulture)));
        cover.Add(new KeyValuePair<string, string>("Ordered by", OrderLabel(f.sort)));
        string file = GraduationExport.FileName("graduation-list", f.acadYear);
        try
        {
            if (fmt == "csv")
                GraduationExport.Csv(Response, file, "Graduation List", scope.Label, cover,
                                     sheet.Columns, sheet.Rows);
            else if (fmt == "xls")
                GraduationExport.Workbook(Response, file, "Graduation List", scope.Label, cover, sheets);
            else
            {
                GraduationExport.Sheet pdfSheet = GraduationExport.WithoutGroupColumns(sheet, f.groupBy);
                GraduationPdf.Send(Response, file, "Graduation List", f.acadYear, scope.Label, cover,
                                   GraduationExport.PdfCols(pdfSheet),
                                   GraduationExport.ToTable(pdfSheet, GroupValues(rows, f.groupBy)),
                                   f.groupBy == "" ? "" : GraduationExport.GROUP_COL,
                                   true, GraduationExport.Truncation);
            }
        }
        finally { GraduationExport.Truncation = null; }
    }

    private static string OrderLabel(string by)
    {
        switch ((by ?? "").ToLowerInvariant())
        {
            case "regno": return "Student number";
            case "cgpa": return "CGPA, highest first";
            case "class": return "Class of award";
            case "name": return "Name";
            default: return "Programme, then name";
        }
    }

    /// <summary>
    /// A stable second pass that gathers the rows into groups without disturbing the order
    /// chosen inside them. List.Sort is unstable, so the position after the first sort is
    /// carried as the tie-breaker.
    /// </summary>
    private static void GroupSort(List<Row> rows, string groupBy)
    {
        var pos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++) if (!pos.ContainsKey(rows[i].regno)) pos[rows[i].regno] = i;
        bool fac = groupBy == "fac";
        rows.Sort(delegate(Row a, Row b)
        {
            string ka = fac ? a.faculty : (a.progname == "" ? a.progcode : a.progname);
            string kb = fac ? b.faculty : (b.progname == "" ? b.progcode : b.progname);
            int d = string.Compare(ka, kb, StringComparison.OrdinalIgnoreCase);
            if (d != 0) return d;
            int ia, ib;
            pos.TryGetValue(a.regno, out ia);
            pos.TryGetValue(b.regno, out ib);
            return ia.CompareTo(ib);
        });
    }

    private static List<string> GroupValues(List<Row> rows, string groupBy)
    {
        if (groupBy == "") return null;
        var l = new List<string>();
        foreach (Row r in rows)
            l.Add(groupBy == "fac"
                ? (r.faculty == "" ? "(no faculty recorded)" : r.faculty)
                : (r.progname == "" ? r.progcode : r.progname + "   (" + r.progcode + ")"));
        return l;
    }

    /// <summary>The way a Senate paper opens: how many from each programme, and how they fell.</summary>
    private static GraduationExport.Sheet ByProgramme(List<Row> rows)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "By programme";
        sh.Columns = new[] { "Programme", "Faculty", "Graduands", "First Class / Distinction", "Other classes" };
        for (int i = 2; i <= 4; i++) sh.NumericColumns.Add(i);
        var tally = new Dictionary<string, int[]>();
        var fac = new Dictionary<string, string>();
        var order = new List<string>();
        foreach (Row r in rows)
        {
            string key = r.progname == "" ? r.progcode : r.progname;
            if (!tally.ContainsKey(key)) { tally[key] = new int[2]; fac[key] = r.faculty; order.Add(key); }
            tally[key][0]++;
            if (Top(r.degclass)) tally[key][1]++;
        }
        order.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string k in order)
            sh.Rows.Add(new[] { k, fac[k],
                tally[k][0].ToString(CultureInfo.InvariantCulture),
                tally[k][1].ToString(CultureInfo.InvariantCulture),
                (tally[k][0] - tally[k][1]).ToString(CultureInfo.InvariantCulture) });
        return sh;
    }

    private static bool Top(string cls)
    {
        return cls.IndexOf("First", StringComparison.OrdinalIgnoreCase) >= 0
            || cls.IndexOf("Distinction", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>The class distribution, the table that always gets asked for.</summary>
    private static GraduationExport.Sheet ByClass(List<Row> rows)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "By class of award";
        sh.Columns = new[] { "Class of Award", "Graduands", "Share" };
        sh.NumericColumns.Add(1);
        var tally = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (Row r in rows)
        {
            string k = r.degclass == "" ? "(not recorded)" : r.degclass;
            if (!tally.ContainsKey(k)) { tally[k] = 0; order.Add(k); }
            tally[k]++;
        }
        order.Sort(delegate(string a, string x) { return tally[x].CompareTo(tally[a]); });
        foreach (string k in order)
            sh.Rows.Add(new[] { k, tally[k].ToString(CultureInfo.InvariantCulture),
                rows.Count > 0 ? Math.Round(tally[k] * 100.0 / rows.Count).ToString(CultureInfo.InvariantCulture) + "%" : "-" });
        sh.Rows.Add(new[] { "Total", rows.Count.ToString(CultureInfo.InvariantCulture), "100%" });
        return sh;
    }

    /// <summary>
    /// Who approved whom, one of the questions this module was built to answer, and one
    /// nothing in the system could answer before it.
    /// </summary>
    private static GraduationExport.Sheet ByApprover(List<Row> rows)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "Who cleared whom";
        sh.Columns = new[] { "Cleared By", "Names Cleared" };
        sh.NumericColumns.Add(1);
        var tally = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (Row r in rows)
        {
            string k = r.clearedBy == "" ? "(cleared before this module existed)" : r.clearedBy;
            if (!tally.ContainsKey(k)) { tally[k] = 0; order.Add(k); }
            tally[k]++;
        }
        order.Sort(delegate(string a, string x) { return tally[x].CompareTo(tally[a]); });
        foreach (string k in order)
            sh.Rows.Add(new[] { k, tally[k].ToString(CultureInfo.InvariantCulture) });
        return sh;
    }

    /// <summary>How many names an export would carry, before the user commits to it.</summary>
    [WebMethod(EnableSession = true)]
    public static string CountExport(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            GraduationEngine.GradFilter f = GraduationBootstrap.Parse(configJson);
            int n = Fetch(scope, f).Count;
            return J.Serialize(new { success = true, total = n, note = "",
                                     capped = n > EXPORT_CAP ? EXPORT_CAP : 0 });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
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

    /// <summary>Reasons worth offering for THIS candidate. See GraduationReasons.</summary>
    [WebMethod(EnableSession = true)]
    public static string HoldReasons(string regno) { return GraduationReasons.For(regno); }

    [WebMethod(EnableSession = true)]
    public static string RemoveStudent(string regno, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.RemoveFromList(scope, regno, reason);
    }
}
