using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Web.Services;

// Fixed Assets: the register (plan 4.3). List, detail, form, records, batch actions, attachments, exports.
public partial class COOPERP_NewScreens_Assets : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, FaAccess.Register);
        if (Request.HttpMethod == "POST" && !string.IsNullOrEmpty(Request.Form["faFormat"])) { Export(); return; }

        using (var c = FaDb.Open())
        {
            var boot = new Dictionary<string, object>();
            boot["rights"] = FaApi.RightsJson();
            boot["campuses"] = FaAssets.Campuses(c);
            boot["departments"] = FaAssets.Departments(c);
            boot["rooms"] = FaAssets.Rooms(c);
            boot["tree"] = FaCategories.Tree(c, false);
            boot["finYears"] = FaAssets.FinYears(c);
            boot["today"] = DateTime.Today.ToString("yyyy-MM-dd");
            boot["currentFinYear"] = FaFinYear.Of(DateTime.Today);
            var reg = FaReports.Find("register");
            var cols = new List<object>(); foreach (var col in reg.Cols) cols.Add(new { k = col.Key, t = col.Header, on = col.On });
            boot["registerCols"] = cols;
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    private void Export()
    {
        if (!MarksAntiForgeryService.ValidateRequest()) { Response.StatusCode = 403; Response.Write("Security check failed. Reload the page."); Response.End(); return; }
        string report = Request.Form["faReport"] ?? "register";
        var cfg = FaJson.Parse(Request.Form["faConfig"]);
        try
        {
            if (report == "labels")
            {
                var prm = new List<object>();
                string where = FaAssets.Where(cfg, prm);
                DataTable t;
                using (var c = FaDb.Open())
                    t = FaDb.Table(c, null, "SELECT a.asset_no, a.name" + FaAssets.ListFrom + where + FaAssets.OrderBy(cfg) + " LIMIT 2000", prm.ToArray());
                FaPdf.Labels(Response, t, FaExport.FileName("tags"));
                return;
            }
            if (report != "register" && report != "history" && report != "custody") report = "register";
            if (report == "register" && !FaAccess.Can(FaAccess.Register)) { Response.StatusCode = 403; return; }
            var d = FaReports.Run(report, cfg, Request.Form["faGroup"]);
            FaReports.Send(Response, d, Request.Form["faFormat"], Request.Form["faCols"]);
        }
        catch (Exception ex)
        {
            FaLog.Error("Assets.Export", ex);
            Response.Clear(); Response.ContentType = "text/plain";
            Response.Write("The export could not be produced. Go back and try again.");
            Response.End();
        }
    }

    // ─────────────────────────── Reading ───────────────────────────

    [WebMethod(EnableSession = true)]
    public static string GetAssets(string configJson)
    {
        return FaApi.Read(FaAccess.Register, delegate
        {
            var f = FaJson.Parse(configJson);
            int page = Math.Max(1, FaJson.Int(f, "page")), size = FaJson.Int(f, "size");
            if (size <= 0 || size > 200) size = 50;
            int total; decimal cost, value;
            var rows = new List<object>();
            using (var c = FaDb.Open())
                foreach (DataRow r in FaAssets.Query(c, f, page, size, out total, out cost, out value).Rows) rows.Add(FaAssets.ListItem(r));
            return FaJson.Ser(new { success = true, rows = rows, total = total, page = page, size = size, totals = new { cost = cost, value = value } });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string GetIds(string configJson)
    {
        return FaApi.Read(FaAccess.Register, delegate
        {
            var f = FaJson.Parse(configJson);
            var prm = new List<object>();
            string where = FaAssets.Where(f, prm);
            var ids = new List<int>();
            using (var c = FaDb.Open())
                foreach (DataRow r in FaDb.Table(c, null, "SELECT a.id" + FaAssets.ListFrom + where + " LIMIT 301", prm.ToArray()).Rows) ids.Add(FaDb.I(r[0]));
            return FaJson.Ser(new { success = true, ids = ids });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string GetAsset(int id)
    {
        return FaApi.Read(FaAccess.Register, delegate
        {
            object d;
            using (var c = FaDb.Open()) d = FaAssets.Detail(c, id, FaApi.Rights());
            if (d == null) return FaJson.Fail("That asset no longer exists.");
            return FaJson.Ser(new { success = true, data = d });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string NextAssetNo(int subCategoryId)
    {
        return FaApi.Read(FaAccess.Edit, delegate
        {
            string n; using (var c = FaDb.Open()) n = FaAssets.Preview(c, null, subCategoryId);
            return FaJson.Ser(new { success = true, assetNo = n });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchStaff(string q)
    {
        return FaApi.Read(FaAccess.Register, delegate
        {
            List<object> l; using (var c = FaDb.Open()) l = FaAssets.SearchStaff(c, q);
            return FaJson.Ser(new { success = true, rows = l });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchSuppliers(string q)
    {
        return FaApi.Read(FaAccess.Register, delegate
        {
            List<object> l; using (var c = FaDb.Open()) l = FaAssets.SearchSuppliers(c, q);
            return FaJson.Ser(new { success = true, rows = l });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string report, string configJson, string groupBy)
    {
        return FaApi.Read(FaAccess.Register, delegate
        {
            var f = FaJson.Parse(configJson);
            if (report == "labels") { int t; decimal a, b; using (var c = FaDb.Open()) FaAssets.Query(c, f, 1, 1, out t, out a, out b); return FaJson.Ser(new { success = true, count = Math.Min(t, 2000) }); }
            return FaJson.Ser(new { success = true, count = FaReports.Count(string.IsNullOrEmpty(report) ? "register" : report, f, groupBy) });
        });
    }

    // ─────────────────────────── Writing ───────────────────────────

    [WebMethod(EnableSession = true)]
    public static string SaveAsset(string json)
    {
        return FaApi.Write(FaAccess.Edit, delegate
        {
            var d = FaJson.Parse(json);
            var warnings = new List<string>();
            if (FaJson.Int(d, "id") > 0)
            {
                string err = FaAssets.Update(d, warnings);
                return err == null ? FaJson.Ser(new { success = true, id = FaJson.Int(d, "id"), warnings = warnings }) : FaJson.Fail(err);
            }
            int id; string no;
            string e = FaAssets.Create(d, FaJson.Str(d, "clientOpId"), out id, out no, warnings);
            return e == null ? FaJson.Ser(new { success = true, id = id, assetNo = no, warnings = warnings }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string AddRecord(string json)
    {
        // The specific permission for the record type is checked inside FaRecords.Add.
        return FaApi.Write(FaAccess.Register, delegate
        {
            var r = FaRecords.Add(FaJson.Parse(json));
            if (r.NeedsCatchUp)
                return FaJson.Ser(new { success = false, needsCatchUp = true, months = r.CatchUpMonths, amount = r.CatchUpAmount,
                    message = "Depreciation of UGX " + FaFmt.Money(r.CatchUpAmount) + " for " + FaFmt.Plural(r.CatchUpMonths, "month", "months") + " is due first." });
            if (r.Error != null) return FaJson.Fail(r.Error);
            return FaJson.Ser(new { success = true, recordId = r.RecordId, warnings = r.Warnings });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string ReverseRecord(long recordId, string reason, string clientOpId)
    {
        return FaApi.Write(FaAccess.Register, delegate
        {
            string e = FaRecords.Reverse(recordId, reason, clientOpId);
            return e == null ? FaJson.Ser(new { success = true }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string Batch(string json)
    {
        return FaApi.Write(FaAccess.Transfer, delegate { return FaJson.Ser(FaRecords.Batch(FaJson.Parse(json))); });
    }

    [WebMethod(EnableSession = true)]
    public static string RemoveAttachment(int id, string reason)
    {
        return FaApi.Write(FaAccess.Edit, delegate
        {
            string e = FaFiles.Remove(id, reason);
            return e == null ? FaJson.Ser(new { success = true }) : FaJson.Fail(e);
        });
    }
}
