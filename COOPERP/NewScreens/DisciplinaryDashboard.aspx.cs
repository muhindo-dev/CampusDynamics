using System;
using System.Collections.Generic;
using System.Web.Services;

// Student Disciplinary module: dashboard (plan 5.1). Figures and charts only; every figure opens the records filtered to it.
public partial class COOPERP_NewScreens_DisciplinaryDashboard : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, DcAccess.Dashboard);
        using (var c = FaDb.Open())
        {
            var boot = new Dictionary<string, object>();
            boot["rights"] = DcAccess.RightsJson();
            boot["campuses"] = DcLookups.Campuses(c);
            boot["faculties"] = DcLookups.Faculties(c);
            boot["types"] = DcLookups.CaseTypes(c, false);
            boot["years"] = DcLookups.AcadYears(c);
            boot["today"] = DateTime.Today.ToString("yyyy-MM-dd");
            boot["from"] = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11).ToString("yyyy-MM-dd");
            BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
        }
    }

    [WebMethod(EnableSession = true)]
    public static string GetDashboard(string filterJson)
    {
        return DcApi.Read(DcAccess.Dashboard, delegate { return FaJson.Ser(DcDashboard.Build(FaJson.Parse(filterJson))); });
    }
}
