<%@ WebHandler Language="C#" Class="NewScreens_DcUpload" %>

using System;
using System.Web;
using System.Web.SessionState;

// Student Disciplinary module: attaches a file to a case (and optionally one of its entries).
// POST multipart: caseId, entryId (optional), kind, description, visible, file. Header X-CSRF-Token.
public class NewScreens_DcUpload : IHttpHandler, IRequiresSessionState
{
    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { ctx.Response.StatusCode = 405; ctx.Response.Write(FaJson.Fail("Use POST.")); return; }
        string denied = FaAccess.DenyWrite(DcAccess.Records);
        if (denied != null) { ctx.Response.StatusCode = 403; ctx.Response.Write(denied); return; }
        try
        {
            int caseId; int.TryParse(ctx.Request.Form["caseId"] ?? "", out caseId);
            int eid; int? entryId = int.TryParse(ctx.Request.Form["entryId"] ?? "", out eid) && eid > 0 ? (int?)eid : null;
            HttpPostedFile file = ctx.Request.Files.Count > 0 ? ctx.Request.Files[0] : null;
            int attId;
            string v = (ctx.Request.Form["visible"] ?? "").ToLowerInvariant();
            string err = DcFiles.Save(caseId, entryId, ctx.Request.Form["kind"] ?? "EVIDENCE", ctx.Request.Form["description"] ?? "", v == "true" || v == "1", file, out attId);
            ctx.Response.Write(err == null ? FaJson.Ser(new { success = true, id = attId }) : FaJson.Fail(err));
        }
        catch (Exception ex)
        {
            DcLog.Error("DcUpload", ex);
            ctx.Response.Write(FaJson.Fail("The file could not be saved."));
        }
    }
}
