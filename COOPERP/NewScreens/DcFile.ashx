<%@ WebHandler Language="C#" Class="NewScreens_DcFile" %>

using System;
using System.Web;
using System.Web.SessionState;

// Student Disciplinary module: streams a case attachment (?a=) or an issued letter (?l=) to a signed-in
// officer who may see the case. A case the user cannot see answers "not found", as everywhere else.
public class NewScreens_DcFile : IHttpHandler, IReadOnlySessionState
{
    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext ctx)
    {
        if (FaAccess.Deny(DcAccess.Records) != null) { ctx.Response.StatusCode = 403; return; }
        bool inline = ctx.Request.QueryString["view"] == "1";
        int id;
        try
        {
            if (int.TryParse(ctx.Request.QueryString["l"] ?? "", out id) && id > 0) { if (!DcLetters.Stream(ctx, id, inline)) ctx.Response.StatusCode = 404; return; }
            if (int.TryParse(ctx.Request.QueryString["a"] ?? "", out id) && id > 0) { if (!DcFiles.Stream(ctx, id, inline)) ctx.Response.StatusCode = 404; return; }
            ctx.Response.StatusCode = 400;
        }
        catch (Exception ex) { DcLog.Error("DcFile", ex); ctx.Response.StatusCode = 500; }
    }
}
