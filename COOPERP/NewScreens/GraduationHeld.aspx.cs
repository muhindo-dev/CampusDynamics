using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Web.Services;

// =====================================================================
//  Graduation Centre — held candidates.
//
//  The queue nobody had before: students a reviewer stopped, with the
//  reason, oldest first. An independent page carrying only its own
//  endpoints.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationHeld : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    protected void Page_Load(object sender, EventArgs e)
    {
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) return;

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");
        f.state = "held";
        f.page = 1;
        f.size = 2000;
        int total;
        List<GradCandidate> rows = GraduationEngine.Page(scope, f, out total);

        var sheet = new GraduationExport.Sheet();
        sheet.Name = "Held";
        sheet.Subtitle = "oldest hold first";
        sheet.Columns = new[] { "Student Number", "Name", "Programme Code", "Programme", "Faculty",
                                "Reason Held", "Held By", "Held On", "Still Blocked",
                                "Credits Earned", "Credits Required", "CGPA", "Failed Papers" };
        foreach (int i in new[] { 9, 10, 11, 12 }) sheet.NumericColumns.Add(i);

        foreach (GradCandidate g in rows)
            sheet.Rows.Add(new[]
            {
                g.regno, g.name, g.progcode, g.progname, g.faculty,
                g.holdReason, g.holdActor, g.holdAt,
                // The column a Registrar actually acts on: a hold whose reason no longer
                // applies is a student waiting for nothing.
                g.readiness == "BLOCKED" ? "Yes" : "No - nothing is blocking them any more",
                Math.Round(g.cuEarned).ToString(),
                g.cuSource == "NONE" ? "" : Math.Round(g.cuRequired).ToString(),
                g.cgpa > 0 ? g.cgpa.ToString("F2") : "",
                g.failedPapers.ToString()
            });

        var cover = GraduationBootstrap.CoverOf(f, "Held candidates");
        cover.Add(new KeyValuePair<string, string>("Holds listed", rows.Count.ToString()));
        string file = GraduationExport.FileName("graduation-held", f.acadYear);
        if (fmt == "csv")
            GraduationExport.Csv(Response, file, "Held Candidates", scope.Label, cover, sheet.Columns, sheet.Rows);
        else
            GraduationExport.Workbook(Response, file, "Held Candidates", scope.Label, cover,
                                      new List<GraduationExport.Sheet> { sheet });
    }

    [WebMethod(EnableSession = true)]
    public static string GetBootstrap() { return GraduationBootstrap.Bootstrap(false); }

    [WebMethod(EnableSession = true)]
    public static string GetHeld(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            GraduationEngine.GradFilter f = GraduationBootstrap.Parse(configJson);
            f.state = "held";
            int total;
            List<GradCandidate> rows = GraduationEngine.Page(scope, f, out total);
            return J.Serialize(new { success = true, rows = rows, total = total });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetStudent(string regno) { return GraduationStudent.Detail(regno); }

    [WebMethod(EnableSession = true)]
    public static string ReleaseStudent(string regno, string acadYear, string note)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Release(scope, regno, acadYear, note);
    }

    [WebMethod(EnableSession = true)]
    public static string HoldStudent(string regno, string acadYear, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Hold(scope, regno, acadYear, reason);
    }

    [WebMethod(EnableSession = true)]
    public static string ClearStudent(string regno, string acadYear, string note, bool overrideBlock)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Clear(scope, regno, acadYear, note, overrideBlock);
    }
}
