using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using System.Web.Services;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — Candidates.
//
//  The working queue: who looks ready, the evidence behind each one, and
//  the two decisions a reviewer can take. An independent page carrying
//  only its own endpoints.
//
//  Every write re-checks scope inside GraduationService. The buttons a
//  browser does or does not draw are not access control.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationCandidates : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    protected void Page_Load(object sender, EventArgs e)
    {
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) return;

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");
        f.state = "pending";
        f.page = 1;
        f.size = 5000;        // an export is the whole queue, not the page on screen
        int total;
        List<GradCandidate> rows = GraduationEngine.Page(scope, f, out total);

        var sheet = new GraduationExport.Sheet();
        sheet.Name = "Candidates";
        sheet.Subtitle = f.acadYear == "" ? "" : ("for " + f.acadYear);
        sheet.Columns = new[] { "Student Number", "Name", "Programme Code", "Programme", "Faculty",
                                "Intake", "Reached Year", "Credits Earned", "Credits Required",
                                "Credit Source", "CGPA", "Class of Award", "Failed Papers",
                                "Zero Marks", "Unmarked", "Readiness", "Status" };
        foreach (int i in new[] { 5, 6, 7, 8, 10, 12, 13, 14 }) sheet.NumericColumns.Add(i);

        foreach (GradCandidate g in rows)
        {
            if (f.readiness == "ready" && g.readiness != "READY") continue;
            if (f.readiness == "warn" && g.readiness != "WARN") continue;
            if (f.readiness == "blocked" && g.readiness != "BLOCKED") continue;
            sheet.Rows.Add(new[]
            {
                g.regno, g.name, g.progcode, g.progname, g.faculty,
                g.entryyear, g.maxStudyYear.ToString(),
                Math.Round(g.cuEarned).ToString(),
                g.cuSource == "NONE" ? "" : Math.Round(g.cuRequired).ToString(),
                g.cuSource == "STRUCTURE" ? "Programme structure"
                    : g.cuSource == "DECLARED" ? "Declared minimum" : "Not assessable",
                g.cgpa > 0 ? g.cgpa.ToString("F2") : "",
                g.degClass,
                g.failedPapers.ToString(), g.zeroMarks.ToString(), g.missingScores.ToString(),
                g.readiness == "READY" ? "Ready" : g.readiness == "WARN" ? "Needs a look" : "Blocked",
                g.graduatedYear != "" ? ("On the " + g.graduatedYear + " list")
                    : (g.holdReason != "" ? "Held" : "Under review")
            });
        }

        var cover = GraduationBootstrap.CoverOf(f, f.focus == "cycle" ? "The graduating cycle" : "Everyone not yet graduated");
        string file = GraduationExport.FileName("graduation-candidates", f.acadYear);
        if (fmt == "csv")
            GraduationExport.Csv(Response, file, "Graduation Candidates", scope.Label, cover, sheet.Columns, sheet.Rows);
        else
            GraduationExport.Workbook(Response, file, "Graduation Candidates", scope.Label, cover,
                                      new List<GraduationExport.Sheet> { sheet });
    }

    [WebMethod(EnableSession = true)]
    public static string GetBootstrap() { return GraduationBootstrap.Bootstrap(true); }

    [WebMethod(EnableSession = true)]
    public static string GetCandidates(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            GraduationEngine.GradFilter f = GraduationBootstrap.Parse(configJson);
            f.state = "pending";
            int total;
            List<GradCandidate> rows = GraduationEngine.Page(scope, f, out total);
            return J.Serialize(new
            {
                success = true, rows = rows, total = total, page = f.page, size = f.size,
                pages = (total + f.size - 1) / f.size
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetStudent(string regno)
    { return GraduationStudent.Detail(regno); }

    [WebMethod(EnableSession = true)]
    public static string ClearStudent(string regno, string acadYear, string note, bool overrideBlock)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Clear(scope, regno, acadYear, note, overrideBlock);
    }

    [WebMethod(EnableSession = true)]
    public static string HoldStudent(string regno, string acadYear, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Hold(scope, regno, acadYear, reason);
    }

    [WebMethod(EnableSession = true)]
    public static string ReleaseStudent(string regno, string acadYear, string note)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Release(scope, regno, acadYear, note);
    }

    /// <summary>
    /// Clears several at once. Offered only for candidates whose worst finding is PASS, and the
    /// server enforces that rather than trusting the browser: each is re-assessed live, anything
    /// not READY is skipped and named back. A bulk action that can quietly clear a blocked
    /// student is worse than no bulk action.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string ClearMany(string regnos, string acadYear)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        acadYear = (acadYear ?? "").Trim();
        if (acadYear == "") return J.Serialize(new { success = false, message = "Choose the graduation year first." });

        var ids = new List<string>();
        foreach (string x in (regnos ?? "").Split(','))
        { string t = x.Trim(); if (t != "" && !ids.Contains(t)) ids.Add(t); }
        if (ids.Count == 0) return J.Serialize(new { success = false, message = "Nothing was selected." });
        if (ids.Count > 300) return J.Serialize(new { success = false, message = "Clear at most 300 at a time." });

        int done = 0;
        var skipped = new List<string>();
        foreach (string reg in ids)
        {
            string raw = GraduationService.Clear(scope, reg, acadYear, "", false);
            bool ok = false;
            try
            {
                var d = J.Deserialize<Dictionary<string, object>>(raw);
                object v;
                ok = d.TryGetValue("success", out v) && v != null && Convert.ToBoolean(v);
                if (!ok)
                {
                    object m; d.TryGetValue("message", out m);
                    skipped.Add(reg + " — " + (m == null ? "refused" : m.ToString()));
                }
            }
            catch { skipped.Add(reg + " — could not be read"); }
            if (ok) done++;
        }

        return J.Serialize(new
        {
            success = true,
            cleared = done,
            skipped = skipped,
            message = done + (done == 1 ? " candidate" : " candidates") + " added to the " + acadYear +
                      " graduation list" + (skipped.Count > 0 ? "; " + skipped.Count + " skipped." : ".")
        });
    }
}
