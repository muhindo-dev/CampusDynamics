using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;

// Fixed Assets: dashboard (plan 4.1). Figures and charts only; every figure opens the register filtered to it.
public partial class COOPERP_NewScreens_AssetsDashboard : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, FaAccess.Dashboard);
        using (var c = FaDb.Open())
        {
            var boot = new Dictionary<string, object>();
            boot["rights"] = FaApi.RightsJson();
            boot["campuses"] = FaAssets.Campuses(c);
            boot["tree"] = FaCategories.Tree(c, false);
            boot["today"] = DateTime.Today.ToString("yyyy-MM-dd");
            boot["fyStart"] = FaFinYear.Start(DateTime.Today).ToString("yyyy-MM-dd");
            boot["currentFinYear"] = FaFinYear.Of(DateTime.Today);
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    [WebMethod(EnableSession = true)]
    public static string GetDashboard(string filterJson)
    {
        return FaApi.Read(FaAccess.Dashboard, delegate { return FaJson.Ser(FaDashboard.Build(FaJson.Parse(filterJson))); });
    }
}
