using System;
using System.Web.Services;
using System.Web.UI;

/// <summary>
/// Student Course Rearrangement — the log.
///
/// Read endpoints are gated exactly like the write ones: who changed whose marks, and why, is
/// not public information inside the institution either.
/// </summary>
public partial class COOPERP_NewScreens_StudentRearrangeLogs : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, StudentRearrangeService.SlugLogs);

        if (string.Equals(Request.QueryString["export"], "csv", StringComparison.OrdinalIgnoreCase))
            StudentRearrangeService.ExportCsv(Response, Request);
    }

    [WebMethod(EnableSession = true)]
    public static string Logs(string from, string to, string actor, string role, string regno,
                              string opType, string reversed, string q, int page, int pageSize)
    {
        return StudentRearrangeService.Logs(from, to, actor, role, regno, opType, reversed, q, page, pageSize);
    }

    [WebMethod(EnableSession = true)]
    public static string LogDetail(long id) { return StudentRearrangeService.LogDetail(id); }

    [WebMethod(EnableSession = true)]
    public static string ReverseEntry(long logId, string reason)
    {
        return StudentRearrangeService.ReverseEntry(logId, reason);
    }

    [WebMethod(EnableSession = true)]
    public static string ReverseBatch(long batchId, string reason)
    {
        return StudentRearrangeService.ReverseBatch(batchId, reason);
    }

    [WebMethod(EnableSession = true)]
    public static string ReverseSession(long sessionId, string reason)
    {
        return StudentRearrangeService.ReverseSession(sessionId, reason);
    }

    [WebMethod(EnableSession = true)]
    public static string Filters() { return StudentRearrangeService.Filters(); }
}
