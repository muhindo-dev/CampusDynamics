using System;
using System.Web.Services;
using System.Web.UI;

/// <summary>
/// Student Course Rearrangement — the workspace.
///
/// Two independent gates, on purpose. RequireSlug stops the PAGE being opened; the CanUse()
/// check inside every method stops the ENDPOINT being called directly. Hiding a menu item is
/// not access control, and a PageMethod is reachable whether or not its page rendered.
/// </summary>
public partial class COOPERP_NewScreens_StudentRearrangeManage : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, StudentRearrangeService.SlugManage);
    }

    [WebMethod(EnableSession = true)]
    public static string OpenSession(string regno, string reason, bool acknowledged)
    {
        return StudentRearrangeService.OpenSession(regno, reason, acknowledged);
    }

    [WebMethod(EnableSession = true)]
    public static string LoadWorkspace(long sessionId)
    {
        return StudentRearrangeService.LoadWorkspace(sessionId);
    }

    [WebMethod(EnableSession = true)]
    public static string Save(long sessionId, string clientOpId, string opsJson, string checksum)
    {
        return StudentRearrangeService.Save(sessionId, clientOpId, opsJson, checksum);
    }

    /// <summary>Course picker for the "add a course" control. Read endpoint — gated too.</summary>
    [WebMethod(EnableSession = true)]
    public static string SearchCourses(string q, string progId)
    {
        return StudentRearrangeService.SearchCourses(q, progId);
    }
}
