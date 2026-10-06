using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Web.Services;

// Student Disciplinary module: reports R1 to R11 (plan section 9). Choose a report, set its filters, preview it, export it.
public partial class COOPERP_NewScreens_DisciplinaryReports : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, DcAccess.Reports);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["faFormat"])) { Export(); return; }
        using (var c = FaDb.Open())
        {
            var boot = new Dictionary<string, object>();
            boot["reports"] = DcReports.CatalogueJson();
            boot["campuses"] = DcLookups.Campuses(c);
            boot["faculties"] = DcLookups.Faculties(c);
            boot["types"] = DcLookups.CaseTypes(c, false);
            boot["years"] = DcLookups.AcadYears(c);
            boot["year"] = DcSeq.AcadYear();
            boot["today"] = DateTime.Today.ToString("yyyy-MM-dd");
            boot["in30"] = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd");
            boot["yearStart"] = new DateTime(DateTime.Today.Month >= 8 ? DateTime.Today.Year : DateTime.Today.Year - 1, 8, 1).ToString("yyyy-MM-dd");
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        try
        {
            var d = DcReports.Run(Request.Form["faReport"] ?? "", FaJson.Parse(Request.Form["faConfig"]), Request.Form["faGroup"]);
            DcReports.Send(Response, d, Request.Form["faFormat"], Request.Form["faCols"]);
        }
        catch (DcRefusal r) { Response.Clear(); Response.ContentType = "text/plain"; Response.Write(r.Message); Response.End(); }
        catch (Exception ex)
        {
            DcLog.Error("DisciplinaryReports.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain"; Response.Write("The report could not be produced. Go back and check the filters."); Response.End();
        }
    }

    [WebMethod(EnableSession = true)]
    public static string PreviewReport(string report, string configJson, string groupBy)
    {
        return DcApi.Read(DcAccess.Reports, delegate
        {
            DcReportData d = DcReports.Run(report, FaJson.Parse(configJson), groupBy);
            var cols = new List<object>(); var idx = new List<int>();
            for (int i = 0; i < d.Cols.Count; i++) if (d.Cols[i].On) { idx.Add(i); cols.Add(new { t = d.Cols[i].Header, num = d.Cols[i].Numeric }); }
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
                if (r < 200) rows.Add(new { g = d.Groups == null ? "" : d.Groups[r], c = cells });
            }
            var totals = new List<string>(); bool anySum = false;
            for (int j = 0; j < idx.Count; j++) { bool s = d.Cols[idx[j]].Sum; anySum |= s; totals.Add(s ? FaFmt.Money(sums[j]) : ""); }
            return FaJson.Ser(new { success = true, title = d.Title, subtitle = d.Subtitle, cover = d.Cover, cols = cols, rows = rows, total = d.Rows.Count,
                                    totals = anySum && d.ShowTotals ? totals : null, grouped = d.Groups != null, prose = d.Prose });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string report, string configJson, string groupBy)
    {
        return DcApi.Read(DcAccess.Reports, delegate { return FaJson.Ser(new { success = true, count = DcReports.Count(report, FaJson.Parse(configJson), groupBy) }); });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchCases(string q)
    {
        return DcApi.Read(DcAccess.Reports, delegate
        {
            var l = new List<object>();
            if ((q ?? "").Trim().Length >= 2)
            {
                int t; var f = new Dictionary<string, object>(); f["q"] = q.Trim();
                using (var c = FaDb.Open())
                    foreach (DataRow r in DcCases.Query(c, f, 1, 15, out t).Rows)
                        l.Add(new { id = FaDb.I(r["id"]), name = FaDb.S(r["case_no"]) + ", " + FaDb.S(r["student_name"]), sub = FaDb.S(r["type_name"]) + ", " + DcFmt.Status(FaDb.S(r["status"])) });
            }
            return FaJson.Ser(new { success = true, rows = l });
        });
    }
}
