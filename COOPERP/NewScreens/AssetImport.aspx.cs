using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Services;

// Fixed Assets: bulk import from Excel (plan 4.4). Template, upload, validation report, commit all or nothing.
public partial class COOPERP_NewScreens_AssetImport : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        // The upload is a multipart POST, so it is handled here rather than as a PageMethod.
        if (Request.QueryString["ajax"] == "validate")
        {
            Response.ContentType = "application/json";
            string denied = FaAccess.DenyWrite(FaAccess.Import);
            if (denied != null) { Response.StatusCode = 403; Response.Write(denied); Response.End(); return; }
            try
            {
                HttpPostedFile f = Request.Files.Count > 0 ? Request.Files[0] : null;
                if (f == null || f.ContentLength == 0) Response.Write(FaJson.Fail("Choose the file to import."));
                else if (f.ContentLength > 15 * 1024 * 1024) Response.Write(FaJson.Fail("The file is larger than 15 MB. Split it into smaller files."));
                else
                {
                    byte[] bytes; using (var ms = new MemoryStream()) { f.InputStream.CopyTo(ms); bytes = ms.ToArray(); }
                    Response.Write(FaJson.Ser(FaImport.Validate(Path.GetFileName(f.FileName), bytes)));
                }
            }
            catch (Exception ex)
            {
                FaLog.Error("AssetImport.Validate", ex);
                Response.Write(FaJson.Fail("The file could not be checked. Make sure it is the template saved as .xlsx or CSV."));
            }
            Response.End();
            return;
        }

        RoleAccessService.RequireSlug(this, FaAccess.Import);
        if (Request.QueryString["template"] == "1")
        {
            byte[] x; using (var c = FaDb.Open()) x = FaImport.Template(c);
            Response.Clear();
            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            Response.AddHeader("Content-Disposition", "attachment; filename=\"MRU-asset-import-template.xlsx\"");
            Response.BinaryWrite(x);
            Response.End();
            return;
        }
        using (var c = FaDb.Open())
            BootJson = FaJson.Ser(new { rights = FaApi.RightsJson(), recent = FaImport.Recent(c), maxRows = FaImport.MaxRows }).Replace("<", "\\u003c");
    }

    [WebMethod(EnableSession = true)]
    public static string Commit(long batchId)
    {
        return FaApi.Write(FaAccess.Import, delegate
        {
            int created;
            string e = FaImport.Commit(batchId, out created);
            return e == null ? FaJson.Ser(new { success = true, created = created }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string Abandon(long batchId)
    {
        return FaApi.Write(FaAccess.Import, delegate
        {
            string e = FaImport.Abandon(batchId);
            return e == null ? FaJson.Ser(new { success = true }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string Recent()
    {
        return FaApi.Read(FaAccess.Import, delegate
        {
            List<object> l; using (var c = FaDb.Open()) l = FaImport.Recent(c);
            return FaJson.Ser(new { success = true, rows = l });
        });
    }
}
