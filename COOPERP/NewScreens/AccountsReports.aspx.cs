using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Services;

// General Ledger: the report engine screen (plan sections 3 and 4). Read only: every
// figure comes through GlReports on the read-only database account.
public partial class COOPERP_NewScreens_AccountsReports : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GlAccess.Reports);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["glFormat"])) { Export(); return; }
        var boot = new Dictionary<string, object>();
        try
        {
            boot["reports"] = GlReports.CatalogueJson();
            boot["rights"] = GlAccess.RightsJson();
            boot["lastUsed"] = GlReports.LastUsed(GlAccess.User());
            boot["saved"] = GlReports.SavedFilters(GlAccess.User());
        }
        catch (Exception ex)
        {
            GlLog.Error("AccountsReports.Load", ex);
            boot["error"] = ex is GlRefusal ? ex.Message : "The report list could not be loaded. Reload the page, and tell MIS if it keeps happening.";
        }
        BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        try
        {
            string code = Request.Form["glReport"] ?? "";
            if (GlReports.Find(code) == null) { Response.StatusCode = 400; Response.Write("Unknown report."); Response.End(); return; }
            int sort; int.TryParse(Request.Form["glSort"], out sort);
            var x = GlReports.Ctx(FaJson.Parse(Request.Form["glParams"]), 1, 50, string.IsNullOrEmpty(Request.Form["glSort"]) ? -1 : sort, Request.Form["glDesc"] == "1", Request.Form["glSearch"], true);
            GlResult r = GlReports.Run(code, x);
            r.Cover.InsertRange(0, GlReports.CoverOf(GlReports.Find(code), x));
            var keep = new HashSet<string>((Request.Form["glCols"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            string fmt = Request.Form["glFormat"];
            if (fmt == "pdf" && r.Rows.Count > GlExport.MaxRowsPdf) fmt = "xls";
            if (r.Rows.Count > GlExport.MaxRowsFile) r.Rows = r.Rows.Take(GlExport.MaxRowsFile).ToList();
            if (fmt == "pdf") GlPdf.Send(Response, r, keep);
            else if (fmt == "csv") GlExport.Csv(Response, r, keep);
            else GlExport.Workbook(Response, r, keep);
        }
        catch (Exception ex)
        {
            GlLog.Error("AccountsReports.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain";
            Response.Write(ex is GlRefusal ? ex.Message : "The file could not be produced. Go back and check the parameters.");
            Response.End();
        }
    }

    [WebMethod(EnableSession = true)]
    public static string RunReport(string report, string paramsJson, int page, int size, int sort, bool desc, string search)
    {
        return GlApi.Read(GlAccess.Reports, delegate
        {
            var p = FaJson.Parse(paramsJson);
            var x = GlReports.Ctx(p, page, size, sort, desc, search, false);
            GlResult r = GlReports.Run(report, x);
            GlReports.RememberParams(GlAccess.User(), report, paramsJson);
            return FaJson.Ser(GlReports.ToJson(r, x));
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchAccounts(string q)
    {
        return GlApi.Read(GlAccess.Reports, delegate { return GlApi.Ok(new { rows = GlReports.SearchAccounts(q, 20) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string LoadFreshFigures()
    {
        return GlApi.Read(GlAccess.Reports, delegate
        {
            GlCalc.Forget();
            GlCalc.Snapshot s = GlCalc.Get(true);
            return GlApi.Ok(new { built = GlFmt.When(s.BuiltAt), ms = s.BuildMs });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SaveFilter(string report, string name, string paramsJson)
    {
        return GlApi.Write(GlAccess.Reports, delegate
        {
            int id = GlReports.SaveFilter(GlAccess.User(), report, name, paramsJson);
            return GlApi.Ok(new { id, saved = GlReports.SavedFilters(GlAccess.User()) });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string RemoveFilter(int id)
    {
        return GlApi.Write(GlAccess.Reports, delegate
        {
            GlReports.RemoveFilter(GlAccess.User(), id);
            return GlApi.Ok(new { saved = GlReports.SavedFilters(GlAccess.User()) });
        });
    }
}
