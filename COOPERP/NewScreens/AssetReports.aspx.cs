using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Web;
using System.Web.Services;

// Fixed Assets: reports (plan 4.6 and 7). Choose a report, set its filters, preview it, export it.
public partial class COOPERP_NewScreens_AssetReports : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, FaAccess.Reports);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["faFormat"])) { Export(); return; }
        using (var c = FaDb.Open())
        {
            var boot = new Dictionary<string, object>();
            boot["rights"] = FaApi.RightsJson();
            boot["reports"] = FaReports.CatalogueJson();
            boot["campuses"] = FaAssets.Campuses(c);
            boot["departments"] = FaAssets.Departments(c);
            boot["tree"] = FaCategories.Tree(c, false);
            var years = FaAssets.FinYears(c);
            if (!years.Contains(FaFinYear.Of(DateTime.Today))) years.Insert(0, FaFinYear.Of(DateTime.Today));
            boot["finYears"] = years;
            boot["runs"] = FaDepreciation.Runs(c);
            boot["today"] = DateTime.Today.ToString("yyyy-MM-dd");
            boot["fyStart"] = FaFinYear.Start(DateTime.Today).ToString("yyyy-MM-dd");
            boot["lastYear"] = FaFinYear.Of(FaFinYear.Start(DateTime.Today).AddDays(-1));
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        try
        {
            string key = Request.Form["faReport"] ?? "";
            if (FaReports.Find(key) == null) { Response.StatusCode = 400; Response.Write("Unknown report."); Response.End(); return; }
            var d = FaReports.Run(key, FaJson.Parse(Request.Form["faConfig"]), Request.Form["faGroup"]);
            FaReports.Send(Response, d, Request.Form["faFormat"], Request.Form["faCols"]);
        }
        catch (Exception ex)
        {
            FaLog.Error("AssetReports.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain"; Response.Write("The report could not be produced. Go back and check the filters."); Response.End();
        }
    }

    [WebMethod(EnableSession = true)]
    public static string PreviewReport(string report, string configJson, string groupBy)
    {
        return FaApi.Read(FaAccess.Reports, delegate
        {
            if (FaReports.Find(report) == null) return FaJson.Fail("Choose a report.");
            FaReportData d;
            try { d = FaReports.Run(report, FaJson.Parse(configJson), groupBy); }
            catch (ArgumentException ex) { return FaJson.Fail(ex.Message); }
            var cols = new List<object>(); var idx = new List<int>();
            for (int i = 0; i < d.Cols.Count; i++) if (d.Cols[i].On && !d.Cols[i].Blank) { idx.Add(i); cols.Add(new { t = d.Cols[i].Header, num = d.Cols[i].Numeric }); }
            var rows = new List<object>();
            var sums = new decimal[idx.Count];
            for (int r = 0; r < d.Rows.Count; r++)
            {
                var cells = new List<string>();
                for (int j = 0; j < idx.Count; j++)
                {
                    object v = d.Rows[r][idx[j]];
                    if (v is decimal) { cells.Add(FaFmt.Money((decimal)v)); if (d.Cols[idx[j]].Sum) sums[j] += (decimal)v; }
                    else cells.Add(Convert.ToString(v, CultureInfo.InvariantCulture));
                }
                if (r < 100) rows.Add(new { g = d.Groups == null ? "" : d.Groups[r], c = cells });
            }
            var totals = new List<string>();
            bool anySum = false;
            for (int j = 0; j < idx.Count; j++) { bool s = d.Cols[idx[j]].Sum; anySum |= s; totals.Add(s ? FaFmt.Money(sums[j]) : ""); }
            return FaJson.Ser(new { success = true, title = d.Title, subtitle = d.Subtitle, cover = d.Cover, cols = cols, rows = rows, total = d.Rows.Count,
                                    totals = anySum && d.ShowTotals ? totals : null, grouped = d.Groups != null });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string report, string configJson, string groupBy)
    {
        return FaApi.Read(FaAccess.Reports, delegate { return FaJson.Ser(new { success = true, count = FaReports.Count(report, FaJson.Parse(configJson), groupBy) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchAssets(string q)
    {
        return FaApi.Read(FaAccess.Reports, delegate
        {
            var f = new Dictionary<string, object>(); f["q"] = q ?? ""; f["status"] = "ALL";
            var l = new List<object>();
            if ((q ?? "").Trim().Length >= 2)
            {
                int t; decimal a, b;
                using (var c = FaDb.Open())
                    foreach (DataRow r in FaAssets.Query(c, f, 1, 15, out t, out a, out b).Rows)
                        l.Add(new { id = FaDb.I(r["id"]), name = FaDb.S(r["asset_no"]) + ", " + FaDb.S(r["name"]), assetNo = FaDb.S(r["asset_no"]), title = FaDb.S(r["name"]) });
            }
            return FaJson.Ser(new { success = true, rows = l });
        });
    }
}
