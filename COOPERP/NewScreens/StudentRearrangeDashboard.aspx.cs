using System;
using System.Web.Services;
using System.Web.UI;

/// <summary>Student Course Rearrangement — recent activity, counts, sessions and refusals.</summary>
public partial class COOPERP_NewScreens_StudentRearrangeDashboard : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, StudentRearrangeService.SlugDashboard);
    }

    [WebMethod(EnableSession = true)]
    public static string Dashboard(int days) { return StudentRearrangeService.Dashboard(days); }
}
