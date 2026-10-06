<%@ WebHandler Language="C#" Class="NewScreens_FaFile" %>

using System;
using System.Web;
using System.Web.SessionState;

// Fixed Assets: streams an asset attachment to a signed-in user who may view the register.
public class NewScreens_FaFile : IHttpHandler, IReadOnlySessionState
{
    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext ctx)
    {
        if (FaAccess.Deny(FaAccess.Register) != null) { ctx.Response.StatusCode = 403; return; }
        int id;
        if (!int.TryParse(ctx.Request.QueryString["id"] ?? "", out id) || id <= 0) { ctx.Response.StatusCode = 400; return; }
        FaFiles.Stream(ctx, id, ctx.Request.QueryString["view"] == "1");
    }
}
