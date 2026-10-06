<%@ WebHandler Language="C#" Class="NewScreens_FaUpload" %>

using System;
using System.Web;
using System.Web.SessionState;

// Fixed Assets: attaches a file to an asset (and optionally one of its records).
// POST multipart: assetId, recordId (optional), kind, file. Header X-CSRF-Token.
public class NewScreens_FaUpload : IHttpHandler, IRequiresSessionState
{
    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { ctx.Response.StatusCode = 405; ctx.Response.Write(FaJson.Fail("Use POST.")); return; }
        string denied = FaAccess.DenyWrite(FaAccess.Edit);
        if (denied != null) { ctx.Response.StatusCode = 403; ctx.Response.Write(denied); return; }
        try
        {
            int assetId; int.TryParse(ctx.Request.Form["assetId"] ?? "", out assetId);
            long rid; long? recordId = long.TryParse(ctx.Request.Form["recordId"] ?? "", out rid) && rid > 0 ? (long?)rid : null;
            string kind = ctx.Request.Form["kind"] ?? "OTHER";
            HttpPostedFile file = ctx.Request.Files.Count > 0 ? ctx.Request.Files[0] : null;
            int attId;
            string err = FaFiles.Save(assetId, recordId, kind, file, out attId);
            ctx.Response.Write(err == null ? FaJson.Ser(new { success = true, id = attId }) : FaJson.Fail(err));
        }
        catch (Exception ex)
        {
            FaLog.Error("FaUpload", ex);
            ctx.Response.Write(FaJson.Fail("The file could not be saved."));
        }
    }
}
