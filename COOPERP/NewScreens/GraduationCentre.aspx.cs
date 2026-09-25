using System;
using System.Globalization;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Web.Services;

// =====================================================================
//  Graduation Centre, Overview.
//
//  One of four independent pages. It carries only the two endpoints it
//  uses; the candidate queue, the list and the held queue are separate
//  pages with separate code, so nothing here is loaded to serve them.
//
//  No arithmetic happens in this file. Every figure comes from
//  GraduationEngine, which is also what the Candidates page reads, so a
//  KPI and the list it links into cannot disagree.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationCentre : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    /// <summary>
    /// The first paint, rendered into the page.
    ///
    /// The module used to open by firing GetBootstrap, waiting, then firing GetOverview and
    /// waiting again - two sequential round trips before anything appeared, each carrying
    /// PageMethod overhead and each taking the per-session lock in turn. Both answers are
    /// available at render time, so the page now ships with them and paints immediately.
    /// AJAX is kept for what it is good at: the filter changes that come afterwards.
    /// </summary>
    public string BootJson = "null";
    public string DataJson = "null";
    public string StatsAge = "";


    protected void Page_Load(object sender, EventArgs e)
    {
        // Exports are a form post rather than an AJAX call, so the browser saves the file in
        // the normal way with the name the server chose.
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) { Prime(); return; }

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");
        GraduationEngine.GradOverview o = GraduationEngine.Overview(scope, f);

        // Which tabs the user asked for in the dialog. An empty list means they arrived some
        // other way, so fall back to what the button used to produce.
        string want = Request.Form["gradSheets"] ?? "";
        bool none = want.Trim() == "";
        var sheets = new List<GraduationExport.Sheet>();

        if (none || GraduationExport.Wants(want, "summary")) sheets.Add(SummarySheet(o));
        if (none || GraduationExport.Wants(want, "byfac")) sheets.Add(ByFaculty(o));
        if (none || GraduationExport.Wants(want, "byprog")) sheets.Add(ByProgramme(o));
        if ((none || GraduationExport.Wants(want, "integrity")) && o.integrity.Count > 0)
            sheets.Add(IntegritySheet(o));
        if (sheets.Count == 0) sheets.Add(SummarySheet(o));

        var cover = GraduationBootstrap.CoverOf(f, o.focusLabel);
        string file = GraduationExport.FileName("graduation-overview", f.acadYear);
        if (fmt == "csv")
        {
            // A CSV is one sheet, and the one worth having flat is the programme table.
            GraduationExport.Sheet flat = ByProgramme(o);
            GraduationExport.Csv(Response, file, "Graduation Overview", scope.Label, cover,
                                 flat.Columns, flat.Rows);
        }
        else if (fmt == "xls")
            GraduationExport.Workbook(Response, file, "Graduation Overview", scope.Label, cover, sheets);
        else
        {
            // The overview has no student rows, so the document is the programme table with the
            // headline figures folded into the certification block above it.
            GraduationExport.Sheet flat = ByProgramme(o);
            cover.Add(new KeyValuePair<string, string>("Candidates", N(o.candidates)));
            cover.Add(new KeyValuePair<string, string>("Ready", N(o.ready)));
            cover.Add(new KeyValuePair<string, string>("Blocked", N(o.blocked)));
            cover.Add(new KeyValuePair<string, string>("On hold", N(o.held)));
            cover.Add(new KeyValuePair<string, string>("On the list", N(o.listed)));
            GraduationPdf.Send(Response, file, "Graduation Overview", f.acadYear, scope.Label, cover,
                               GraduationExport.PdfCols(flat),
                               GraduationExport.ToTable(flat, null), "", false, null);
        }
    }

    private static string N(int v) { return v.ToString(CultureInfo.InvariantCulture); }

    /// <summary>The five figures the page opens with, plus what is stopping the rest.</summary>
    private static GraduationExport.Sheet SummarySheet(GraduationEngine.GradOverview o)
    {
        var s = new GraduationExport.Sheet();
        s.Name = "Summary";
        s.Subtitle = o.focusLabel;
        s.Columns = new[] { "Measure", "Candidates" };
        s.NumericColumns.Add(1);
        s.Rows.Add(new[] { "Candidates in scope", N(o.candidates) });
        s.Rows.Add(new[] { "Ready - nothing outstanding", N(o.ready) });
        s.Rows.Add(new[] { "Blocked by at least one check", N(o.blocked) });
        s.Rows.Add(new[] { "On hold", N(o.held) });
        s.Rows.Add(new[] { "On the graduation list", N(o.listed) });
        if (o.backlogEarlier > 0)
            s.Rows.Add(new[] { "Finished earlier and still on no list", N(o.backlogEarlier) });
        s.Rows.Add(new[] { "", "" });
        s.Rows.Add(new[] { "WHAT IS STOPPING THEM", "" });
        foreach (GraduationEngine.GC2 b in o.blockers) s.Rows.Add(new[] { b.name, N(b.count) });
        return s;
    }

    private static GraduationExport.Sheet ByProgramme(GraduationEngine.GradOverview o)
    {
        var s = new GraduationExport.Sheet();
        s.Name = "By programme";
        s.Columns = new[] { "Code", "Programme", "Faculty", "Candidates", "On a list",
                            "On hold", "Failed papers", "To review" };
        for (int i = 3; i <= 7; i++) s.NumericColumns.Add(i);
        foreach (GraduationEngine.ProgProgress p in o.programmes)
        {
            int left = p.candidates - p.listed - p.held; if (left < 0) left = 0;
            s.Rows.Add(new[] { p.progcode, p.progname, p.faculty,
                N(p.candidates), N(p.listed), N(p.held), N(p.blocked), N(left) });
        }
        return s;
    }

    /// <summary>
    /// The same table rolled up to faculties, which is the level a Dean or a VC reads. Built
    /// from the programme rows rather than a second query, so the two tabs always agree.
    /// </summary>
    private static GraduationExport.Sheet ByFaculty(GraduationEngine.GradOverview o)
    {
        var s = new GraduationExport.Sheet();
        s.Name = "By faculty";
        s.Columns = new[] { "Faculty", "Programmes", "Candidates", "On a list", "On hold",
                            "Failed papers", "To review" };
        for (int i = 1; i <= 6; i++) s.NumericColumns.Add(i);

        var tally = new Dictionary<string, int[]>();
        var order = new List<string>();
        foreach (GraduationEngine.ProgProgress p in o.programmes)
        {
            string k = p.faculty == "" ? "(none recorded)" : p.faculty;
            if (!tally.ContainsKey(k)) { tally[k] = new int[5]; order.Add(k); }
            int[] t = tally[k];
            t[0]++; t[1] += p.candidates; t[2] += p.listed; t[3] += p.held; t[4] += p.blocked;
        }
        order.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string k in order)
        {
            int[] t = tally[k];
            int left = t[1] - t[2] - t[3]; if (left < 0) left = 0;
            s.Rows.Add(new[] { k, N(t[0]), N(t[1]), N(t[2]), N(t[3]), N(t[4]), N(left) });
        }
        return s;
    }

    private static GraduationExport.Sheet IntegritySheet(GraduationEngine.GradOverview o)
    {
        var s = new GraduationExport.Sheet();
        s.Name = "Data integrity";
        s.Columns = new[] { "Finding" };
        foreach (string x in o.integrity) s.Rows.Add(new[] { x });
        return s;
    }

    /// <summary>
    /// What the overview export would cover. There are no per-student rows here, so this
    /// reports the population the summaries are drawn from rather than a row count.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string CountExport(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            GraduationEngine.GradFilter f = GraduationBootstrap.Parse(configJson);
            GraduationEngine.GradOverview o = GraduationEngine.Overview(scope, f);
            return J.Serialize(new
            {
                success = true,
                total = o.programmes.Count,
                capped = 0,
                note = "Summarising " + N(o.candidates) + " candidates across " +
                       N(o.programmes.Count) + " programmes."
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    /// <summary>Builds the first paint into the page. See BootJson.</summary>
    private void Prime()
    {
        try
        {
            MarksScope sc = MarksScopeResolver.Resolve();
            StatsAge = GraduationStats.Freshness();
            if (!sc.HasAccess) return;

            BootJson = GraduationBootstrap.ForScriptBlock(GraduationBootstrap.Bootstrap(false));

            // The filter the page will open on, resolved here so the figures shipped with the
            // page are the figures its controls will show.
            var f = new GraduationEngine.GradFilter();
            f.acadYear = Q("year");
            f.focus = Q("focus") == "" ? "cycle" : Q("focus");
            f.faculty = Q("faculty");
            f.department = Q("dept");
            f.programme = Q("prog");
            if (f.acadYear == "")
            {
                try { f.acadYear = AcademicYearHelper.GetCurrentAcademicYear(); } catch { }
            }
            DataJson = GraduationBootstrap.ForScriptBlock(
                J.Serialize(new { success = true, overview = GraduationEngine.Overview(sc, f) }));
        }
        catch { BootJson = "null"; DataJson = "null"; }
    }

    private string Q(string k) { return (Request.QueryString[k] ?? "").Trim(); }

    [WebMethod(EnableSession = true)]
    public static string GetBootstrap() { return GraduationBootstrap.Bootstrap(false); }

    /// <summary>
    /// Rebuilds the per-student summary. Roughly five seconds over the whole results table,
    /// paid deliberately and on demand rather than silently on every page load.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string RefreshStats()
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationStats.Rebuild();
    }

    [WebMethod(EnableSession = true)]
    public static string GetOverview(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            return J.Serialize(new
            {
                success = true,
                overview = GraduationEngine.Overview(scope, GraduationBootstrap.Parse(configJson))
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }
}
