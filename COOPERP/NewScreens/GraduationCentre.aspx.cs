using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Web.Services;

// =====================================================================
//  Graduation Centre — Overview.
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

    protected void Page_Load(object sender, EventArgs e)
    {
        // Exports are a form post rather than an AJAX call, so the browser saves the file in
        // the normal way with the name the server chose.
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) return;

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");
        GraduationEngine.GradOverview o = GraduationEngine.Overview(scope, f);

        var sheets = new List<GraduationExport.Sheet>();

        var blockers = new GraduationExport.Sheet();
        blockers.Name = "Summary";
        blockers.Subtitle = o.focusLabel;
        blockers.Columns = new[] { "Measure", "Candidates" };
        blockers.NumericColumns.Add(1);
        blockers.Rows.Add(new[] { "Candidates in scope", o.candidates.ToString() });
        blockers.Rows.Add(new[] { "Ready", o.ready.ToString() });
        blockers.Rows.Add(new[] { "Blocked", o.blocked.ToString() });
        blockers.Rows.Add(new[] { "Held", o.held.ToString() });
        blockers.Rows.Add(new[] { "On the graduation list", o.listed.ToString() });
        foreach (GraduationEngine.GC2 b in o.blockers)
            blockers.Rows.Add(new[] { b.name, b.count.ToString() });
        sheets.Add(blockers);

        var prog = new GraduationExport.Sheet();
        prog.Name = "By programme";
        prog.Columns = new[] { "Code", "Programme", "Faculty", "Candidates", "On a list", "Held", "Failed papers", "To review" };
        for (int i = 3; i <= 7; i++) prog.NumericColumns.Add(i);
        foreach (GraduationEngine.ProgProgress p in o.programmes)
        {
            int left = p.candidates - p.listed - p.held; if (left < 0) left = 0;
            prog.Rows.Add(new[] { p.progcode, p.progname, p.faculty,
                p.candidates.ToString(), p.listed.ToString(), p.held.ToString(),
                p.blocked.ToString(), left.ToString() });
        }
        sheets.Add(prog);

        if (o.integrity.Count > 0)
        {
            var ig = new GraduationExport.Sheet();
            ig.Name = "Data integrity";
            ig.Columns = new[] { "Finding" };
            foreach (string s in o.integrity) ig.Rows.Add(new[] { s });
            sheets.Add(ig);
        }

        var cover = GraduationBootstrap.CoverOf(f, o.focusLabel);
        string file = GraduationExport.FileName("graduation-overview", f.acadYear);
        if (fmt == "csv")
            GraduationExport.Csv(Response, file, "Graduation Overview", scope.Label, cover,
                                 prog.Columns, prog.Rows);
        else
            GraduationExport.Workbook(Response, file, "Graduation Overview", scope.Label, cover, sheets);
    }

    [WebMethod(EnableSession = true)]
    public static string GetBootstrap() { return GraduationBootstrap.Bootstrap(false); }

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
