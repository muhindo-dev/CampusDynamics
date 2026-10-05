using System;
using System.Web;

/// <summary>
/// API v2 - Performance Appraisal: RETIRED (Oct 2026).
///
/// This endpoint was never used by any client, was written against columns that do not
/// exist, and had authorisation holes (any staff token could create sessions or read any
/// appraisal record). Every action now answers HTTP 410 Gone with a fixed JSON body.
/// Appraisals are handled by the staff portal (eportal) and the eadmin NewScreens pages.
/// The previous implementation is in git history if it is ever needed for reference.
/// See COOPERP/APPRAISAL_STABILISATION_PLAN_2026-10.md (P0 item 3).
/// </summary>
public partial class API_v2_appraisal : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        // CORS preflight still answered the normal way so browsers see the 410 body.
        if (ApiHelper.HandleCors(Request, Response)) return;

        Response.Clear();
        Response.StatusCode = 410;
        Response.TrySkipIisCustomErrors = true;
        Response.ContentType = "application/json";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Write("{\"success\":false,\"error\":\"Appraisal API retired; use the staff portal\"}");
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }
}
