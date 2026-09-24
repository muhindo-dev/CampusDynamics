using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Web.Script.Serialization;
using System.Web.Services;

// =====================================================================
//  Graduation Analysis.
//
//  Rebuilt. What stood here was a DevExpress/GridView page in its own
//  "ga-" idiom with a navy banner, bootstrap colours, a chart that was
//  never built and a PDF button that called window.print(). It had no
//  scope resolution at all - a Dean saw the whole university - and its
//  Excel exports produced empty files, because they called gv.DataBind()
//  on a postback where nothing had rebound the data.
//
//  It now works the way the rest of the Graduation module works: the
//  g- design system, SidebarMaster, MarksScopeResolver, a first paint
//  rendered into the page rather than fetched, GET-driven state, and the
//  same branded export writers.
//
//  All the arithmetic lives in GraduationAnalytics. This file is the
//  screen: it resolves scope once, hands it down, and streams files.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationAnalysis : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    /// <summary>The first paint, rendered into the page. See the other four screens.</summary>
    public string BootJson = "null";
    public string DataJson = "null";
    public string DefaultYear = "";

    protected void Page_Load(object sender, EventArgs e)
    {
        string fmt = Request.Form["gradExport"];
        if (!string.IsNullOrEmpty(fmt)) { Export(fmt); return; }
        Prime();
    }

    /// <summary>
    /// Everything the opening view needs, in the page itself.
    ///
    /// The current academic year is selected on arrival, resolved the same way the rest of the
    /// module resolves it - through AcademicYearHelper, with the result checked against the
    /// years that actually have graduands, because acad_graduands holds years that helper has
    /// never heard of and a default nobody can see data for is worse than no default.
    /// </summary>
    private void Prime()
    {
        try
        {
            MarksScope sc = MarksScopeResolver.Resolve();
            if (!sc.HasAccess)
            {
                BootJson = GraduationBootstrap.Denied();
                return;
            }

            object lists = GraduationAnalytics.Lists(sc);
            var years = new List<string>();
            try
            {
                var t = lists.GetType().GetProperty("years").GetValue(lists, null) as List<string>;
                if (t != null) years = t;
            }
            catch { }

            string want = "";
            try { want = AcademicYearHelper.GetCurrentAcademicYear(); }
            catch { want = ""; }
            // The current year only if somebody has actually graduated in it; otherwise the most
            // recent year that has anybody, which is what a reader expects to land on.
            DefaultYear = (want != "" && years.Contains(want)) ? want
                        : (years.Count > 0 ? years[0] : "");

            BootJson = GraduationBootstrap.ForScriptBlock(J.Serialize(new
            {
                success = true,
                hasAccess = true,
                scopeLabel = sc.Label,
                roleNote = sc.RoleNote,
                currentYear = DefaultYear,
                lists = lists
            }));

            // And the opening figures, so the page paints with data rather than a spinner.
            string cfg = J.Serialize(new { lens = "year", acadYear = DefaultYear });
            DataJson = GraduationBootstrap.ForScriptBlock(GraduationAnalytics.Analyse(sc, cfg));
        }
        catch (Exception ex)
        {
            BootJson = J.Serialize(new { success = false, message = ex.Message });
        }
    }

    [WebMethod(EnableSession = true)]
    public static string Analyse(string configJson)
    { return GraduationAnalytics.Analyse(configJson); }

    [WebMethod(EnableSession = true)]
    public static string Summarise(string configJson)
    { return GraduationAnalytics.Summarise(configJson); }

    // ─────────────────────────────────────────────────────────────────
    //  Exports, through the module's own writers so a file from this
    //  page is branded exactly like one from the graduation list.
    // ─────────────────────────────────────────────────────────────────
    private void Export(string fmt)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        string cfgJson = Request.Form["gradConfig"] ?? "";
        string raw = GraduationAnalytics.Analyse(scope, cfgJson);
        var d = J.Deserialize<Dictionary<string, object>>(raw);
        object ok;
        if (!d.TryGetValue("success", out ok) || !Convert.ToBoolean(ok)) return;

        GraduationAnalytics.Filter f = GraduationAnalytics.Parse(cfgJson);
        string lens = Convert.ToString(d["lensLabel"]);
        var H = (Dictionary<string, object>)d["headline"];

        var cover = new List<KeyValuePair<string, string>>();
        cover.Add(new KeyValuePair<string, string>("Population", lens));
        cover.Add(new KeyValuePair<string, string>("Graduands", Convert.ToString(H["graduands"])));
        cover.Add(new KeyValuePair<string, string>("Programmes", Convert.ToString(H["programmes"])));
        cover.Add(new KeyValuePair<string, string>("Faculties", Convert.ToString(H["faculties"])));
        cover.Add(new KeyValuePair<string, string>("Mean CGPA", Convert.ToString(H["avgCgpa"])));
        cover.Add(new KeyValuePair<string, string>("Women",
            Convert.ToString(H["women"]) + "  (" + Convert.ToString(H["womenPct"]) + "%)"));

        string file = GraduationExport.FileName("graduation-analysis",
            f.lens == "ceremony" ? ("ceremony-" + f.ceremony) : f.acadYear);

        if (fmt == "summary")
        {
            SendSummary(scope, cfgJson, lens, cover, file);
            return;
        }

        GraduationExport.Sheet prog = SheetOf(d, "programmes",
            new[] { "Code", "Programme", "Faculty", "Award level", "Graduands",
                    "Women", "Men", "Mean CGPA", "Top class" },
            new[] { "code", "name", "faculty", "level", "n", "women", "men", "avgCgpa", "top" },
            new[] { 4, 5, 6, 7, 8 });

        var sheets = new List<GraduationExport.Sheet>();
        sheets.Add(prog);
        sheets.Add(SheetOf(d, "faculties",
            new[] { "Faculty", "Graduands", "Programmes", "Women", "Men", "Mean CGPA", "Top class" },
            new[] { "name", "n", "progs", "women", "men", "avgCgpa", "top" },
            new[] { 1, 2, 3, 4, 5, 6 }));
        sheets.Add(LevelSheet(d));
        sheets.Add(SheetOf(d, "trend",
            new[] { "Academic year", "Graduands", "Women", "Men", "Mean CGPA", "Top class" },
            new[] { "year", "n", "women", "men", "avgCgpa", "top" },
            new[] { 1, 2, 3, 4, 5 }));

        var notes = new GraduationExport.Sheet();
        notes.Name = "Data notes";
        notes.Columns = new[] { "What to be careful of" };
        foreach (object n in (System.Collections.ArrayList)d["notes"])
            notes.Rows.Add(new[] { Convert.ToString(n) });
        if (notes.Rows.Count == 0)
            notes.Rows.Add(new[] { "Nothing in this selection is missing a year, a class, a gender or a CGPA." });
        sheets.Add(notes);

        if (fmt == "csv")
            GraduationExport.Csv(Response, file, "Graduation Analysis", scope.Label, cover,
                                 prog.Columns, prog.Rows);
        else if (fmt == "xls")
            GraduationExport.Workbook(Response, file, "Graduation Analysis", scope.Label, cover, sheets);
        else
            GraduationPdf.Send(Response, file, "Graduation Analysis",
                               f.lens == "ceremony" ? "" : f.acadYear, scope.Label, cover,
                               GraduationExport.PdfCols(prog),
                               GraduationExport.ToTable(prog, null), "", false, null);
    }

    /// <summary>A list of objects in the payload, as a branded sheet.</summary>
    private static GraduationExport.Sheet SheetOf(Dictionary<string, object> d, string key,
                                                  string[] heads, string[] fields, int[] numeric)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = char.ToUpperInvariant(key[0]) + key.Substring(1);
        sh.Columns = heads;
        foreach (int i in numeric) sh.NumericColumns.Add(i);
        foreach (Dictionary<string, object> row in (System.Collections.ArrayList)d[key])
        {
            var cells = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                object v;
                cells[i] = row.TryGetValue(fields[i], out v) && v != null ? Convert.ToString(v) : "";
            }
            sh.Rows.Add(cells);
        }
        return sh;
    }

    /// <summary>
    /// The class distribution, one row per level and class.
    ///
    /// Kept as a single flat sheet with the level on every row rather than one sheet per level,
    /// because the whole point is that the two vocabularies do not mix - and a reader has to be
    /// able to see that in one place.
    /// </summary>
    private static GraduationExport.Sheet LevelSheet(Dictionary<string, object> d)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "Class of award";
        sh.Columns = new[] { "Award level", "Class of award", "Graduands",
                             "Share of the level", "Mean CGPA" };
        sh.NumericColumns.Add(2);
        sh.NumericColumns.Add(4);
        foreach (Dictionary<string, object> L in (System.Collections.ArrayList)d["levels"])
        {
            int ln = Convert.ToInt32(L["n"]);
            foreach (Dictionary<string, object> c in (System.Collections.ArrayList)L["classes"])
            {
                int cn = Convert.ToInt32(c["n"]);
                sh.Rows.Add(new[]
                {
                    Convert.ToString(L["name"]), Convert.ToString(c["name"]),
                    cn.ToString(CultureInfo.InvariantCulture),
                    ln > 0 ? Math.Round(cn * 100.0 / ln).ToString(CultureInfo.InvariantCulture) + "%" : "-",
                    Convert.ToString(c["avgCgpa"])
                });
            }
        }
        return sh;
    }

    /// <summary>The written brief, as a document rather than a table.</summary>
    private void SendSummary(MarksScope scope, string cfgJson, string lens,
                             List<KeyValuePair<string, string>> cover, string file)
    {
        string raw = GraduationAnalytics.Summarise(scope, cfgJson);
        var d = J.Deserialize<Dictionary<string, object>>(raw);
        object ok;
        if (!d.TryGetValue("success", out ok) || !Convert.ToBoolean(ok)) return;

        var sh = new GraduationExport.Sheet();
        sh.Name = "Summary";
        sh.Columns = new[] { "Section", "Finding" };
        foreach (Dictionary<string, object> para in (System.Collections.ArrayList)d["paragraphs"])
        {
            string head = Convert.ToString(para["head"]);
            bool first = true;
            foreach (object line in (System.Collections.ArrayList)para["lines"])
            {
                sh.Rows.Add(new[] { first ? head : "", Convert.ToString(line) });
                first = false;
            }
        }

        var cols = new List<GraduationPdf.PCol>();
        cols.Add(new GraduationPdf.PCol("C0", "Section", 120));
        cols.Add(new GraduationPdf.PCol("C1", "Finding", 430));

        GraduationPdf.Send(Response, file + "-summary", "Graduation Summary", lens,
                           scope.Label, cover, cols,
                           GraduationExport.ToTable(sh, null), "", false, null);
    }
}
