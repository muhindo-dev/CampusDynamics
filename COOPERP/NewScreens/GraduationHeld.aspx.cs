using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
using System.Web.Services;

// =====================================================================
//  Graduation Centre, held candidates.
//
//  The queue nobody had before: students a reviewer stopped, with the
//  reason, oldest first. An independent page carrying only its own
//  endpoints.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationHeld : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

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
    /// columns come from one list rather than two that can drift apart.</summary>
    public string ColsJson = "[]";

    /// <summary>Ceiling on one export. Holds are a small queue by nature; this only exists so a
    /// pathological filter cannot try to build a file nobody wants.</summary>
    private const int EXPORT_CAP = 20000;

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
    //  The column catalogue, declared once, used by both the export
    //  dialog's checkboxes and the sheet that comes back.
    // =================================================================
    private const string ID = "Identity", HD = "The hold", PR = "Where they stand";

    private static List<GraduationExport.Col<GradCandidate>> Catalogue()
    {
        var c = new List<GraduationExport.Col<GradCandidate>>();
        c.Add(new GraduationExport.Col<GradCandidate>("regno", "Student Number", ID, F_regno));
        c.Add(new GraduationExport.Col<GradCandidate>("name", "Name", ID, F_name));
        c.Add(new GraduationExport.Col<GradCandidate>("progcode", "Programme Code", ID, F_progcode));
        c.Add(new GraduationExport.Col<GradCandidate>("progname", "Programme", ID, F_progname));
        c.Add(new GraduationExport.Col<GradCandidate>("faculty", "Faculty", ID, F_faculty));
        c.Add(new GraduationExport.Col<GradCandidate>("intake", "Intake", ID, F_intake).Off());

        c.Add(new GraduationExport.Col<GradCandidate>("why", "Reason Held", HD, F_why));
        c.Add(new GraduationExport.Col<GradCandidate>("who", "Held By", HD, F_who));
        c.Add(new GraduationExport.Col<GradCandidate>("when", "Held On", HD, F_when));
        c.Add(new GraduationExport.Col<GradCandidate>("still", "Still Blocked", HD, F_still));
        c.Add(new GraduationExport.Col<GradCandidate>("blockers", "What Is Blocking Them", HD, F_blockers));

        c.Add(new GraduationExport.Col<GradCandidate>("cuE", "Credits Earned", PR, true, F_cuE));
        c.Add(new GraduationExport.Col<GradCandidate>("cuR", "Credits Required", PR, true, F_cuR));
        c.Add(new GraduationExport.Col<GradCandidate>("cgpa", "CGPA", PR, true, F_cgpa));
        c.Add(new GraduationExport.Col<GradCandidate>("class", "Class of Award", PR, F_class).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("fails", "Failed Papers", PR, true, F_fails));
        c.Add(new GraduationExport.Col<GradCandidate>("last", "Last Sat", PR, F_last).Off());
        return c;
    }

    private static string F_regno(GradCandidate g) { return g.regno; }
    private static string F_name(GradCandidate g) { return g.name; }
    private static string F_progcode(GradCandidate g) { return g.progcode; }
    private static string F_progname(GradCandidate g) { return g.progname; }
    private static string F_faculty(GradCandidate g) { return g.faculty; }
    private static string F_intake(GradCandidate g) { return g.entryyear; }
    private static string F_why(GradCandidate g) { return g.holdReason; }
    private static string F_who(GradCandidate g) { return g.holdActor; }
    private static string F_when(GradCandidate g) { return g.holdAt; }
    /// <summary>The column a Registrar actually acts on: a hold whose reason no longer applies
    /// is a student waiting for nothing.</summary>
    private static string F_still(GradCandidate g)
    { return g.readiness == "BLOCKED" ? "Yes" : "No - nothing is blocking them any more"; }
    private static string F_blockers(GradCandidate g)
    {
        var sb = new StringBuilder();
        foreach (GradFinding fd in g.findings)
        {
            if (fd.level != "BLOCK") continue;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(fd.detail);
        }
        return sb.ToString();
    }
    private static string F_cuE(GradCandidate g)
    { return Math.Round(g.cuEarned).ToString(CultureInfo.InvariantCulture); }
    private static string F_cuR(GradCandidate g)
    { return g.cuSource == "NONE" ? "" : Math.Round(g.cuRequired).ToString(CultureInfo.InvariantCulture); }
    private static string F_cgpa(GradCandidate g)
    { return g.cgpa > 0 ? g.cgpa.ToString("F2", CultureInfo.InvariantCulture) : ""; }
    private static string F_class(GradCandidate g) { return g.degClass; }
    private static string F_fails(GradCandidate g)
    { return g.failedPapers.ToString(CultureInfo.InvariantCulture); }
    private static string F_last(GradCandidate g) { return g.lastYear; }

    protected void Page_Load(object sender, EventArgs e)
    {
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) { Prime(); return; }

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");
        f.state = "held";

        // Page() clamps any size above 200 back to 50, so asking it for 2,000 rows returned
        // fifty and said nothing. All() walks it in chunks instead and reports honestly when it
        // has to stop.
        bool onePage = (Request.Form["gradRows"] ?? "all") == "page";
        int total;
        bool truncated = false;
        List<GradCandidate> rows;
        if (onePage)
        {
            int size;
            if (!int.TryParse(Request.Form["gradPageSize"] ?? "", out size) || size < 1 || size > 200) size = 50;
            f.page = 1; f.size = size;
            rows = GraduationEngine.Page(scope, f, out total);
        }
        else rows = GraduationEngine.All(scope, f, EXPORT_CAP, out total, out truncated);

        GraduationExport.Truncation = truncated
            ? ("The filters matched " + total.ToString(CultureInfo.InvariantCulture) +
               " held candidates. This file carries the first " +
               rows.Count.ToString(CultureInfo.InvariantCulture) + ".")
            : null;

        GraduationExport.Sheet sheet = GraduationExport.Build(
            "Held", "oldest hold first", Catalogue(), rows, Request.Form["gradCols"]);

        var sheets = new List<GraduationExport.Sheet> { sheet };
        if (GraduationExport.Wants(Request.Form["gradSheets"] ?? "", "stale")) sheets.Add(Stale(rows));

        var cover = GraduationBootstrap.CoverOf(f, "Held candidates");
        cover.Add(new KeyValuePair<string, string>("Holds listed",
            rows.Count.ToString(CultureInfo.InvariantCulture)));
        string file = GraduationExport.FileName("graduation-held", f.acadYear);
        try
        {
            if (fmt == "csv")
                GraduationExport.Csv(Response, file, "Held Candidates", scope.Label, cover,
                                     sheet.Columns, sheet.Rows);
            else if (fmt == "xls")
                GraduationExport.Workbook(Response, file, "Held Candidates", scope.Label, cover, sheets);
            else
            {
                var groups = new List<string>();
                foreach (GradCandidate g in rows)
                    groups.Add(g.progname == "" ? g.progcode : g.progname + "   (" + g.progcode + ")");
                GraduationExport.Sheet pdfSheet = GraduationExport.WithoutGroupColumns(sheet, "prog");
                GraduationPdf.Send(Response, file, "Held Candidates", f.acadYear, scope.Label, cover,
                                   GraduationExport.PdfCols(pdfSheet),
                                   GraduationExport.ToTable(pdfSheet, groups),
                                   GraduationExport.GROUP_COL, true, GraduationExport.Truncation);
            }
        }
        finally { GraduationExport.Truncation = null; }
    }

    /// <summary>
    /// The holds that can be lifted today: nothing is blocking these students any more, so the
    /// hold is the only thing standing between them and a list. This is the tab worth opening.
    /// </summary>
    private static GraduationExport.Sheet Stale(List<GradCandidate> rows)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "Holds that can be lifted";
        sh.Columns = new[] { "Student Number", "Name", "Programme", "Reason Held", "Held By", "Held On" };
        foreach (GradCandidate g in rows)
            if (g.readiness != "BLOCKED")
                sh.Rows.Add(new[] { g.regno, g.name, g.progname == "" ? g.progcode : g.progname,
                                    g.holdReason, g.holdActor, g.holdAt });
        if (sh.Rows.Count == 0)
            sh.Rows.Add(new[] { "Every hold in this selection still has something blocking it", "", "", "", "", "" });
        return sh;
    }

    /// <summary>How many rows an export would hold, answered before the user commits to it.</summary>
    [WebMethod(EnableSession = true)]
    public static string CountExport(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            GraduationEngine.GradFilter f = GraduationBootstrap.Parse(configJson);
            f.state = "held";
            f.page = 1;
            f.size = 1;
            int total;
            GraduationEngine.Page(scope, f, out total);
            return J.Serialize(new { success = true, total = total, note = "",
                                     capped = total > EXPORT_CAP ? EXPORT_CAP : 0 });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
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

    /// <summary>Reasons worth offering for THIS candidate. See GraduationReasons.</summary>
    [WebMethod(EnableSession = true)]
    public static string HoldReasons(string regno) { return GraduationReasons.For(regno); }

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
