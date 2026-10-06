using System;
using System.Web.Services;

// Student Disciplinary module: case types, sanctions, letter templates, committee and settings (plan 5.5).
public partial class COOPERP_NewScreens_DisciplinarySettings : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, DcAccess.Settings);
    }

    private static string Ok(object o)
    {
        var d = new System.Collections.Generic.Dictionary<string, object>();
        d["success"] = true;
        foreach (var p in o.GetType().GetProperties()) d[p.Name] = p.GetValue(o, null);
        return FaJson.Ser(d);
    }

    [WebMethod(EnableSession = true)]
    public static string Load()
    {
        return DcApi.Read(DcAccess.Settings, delegate { return Ok(DcAdmin.Load()); });
    }

    [WebMethod(EnableSession = true)]
    public static string Save(string what, string json)
    {
        return DcApi.Write(DcAccess.SettingsManage, delegate
        {
            var d = FaJson.Parse(json);
            switch (what)
            {
                case "caseType": return Ok(DcAdmin.SaveCaseType(d));
                case "sanctionType": return Ok(DcAdmin.SaveSanctionType(d));
                case "template": return Ok(DcAdmin.SaveTemplate(d));
                case "member": return Ok(DcAdmin.SaveMember(d));
                case "setting": return Ok(DcAdmin.SaveSetting(d));
                default: return FaJson.Fail("Unknown setting.");
            }
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SearchStaff(string q)
    {
        return DcApi.Read(DcAccess.Settings, delegate { using (var c = FaDb.Open()) return FaJson.Ser(new { success = true, rows = DcLookups.SearchStaff(c, q) }); });
    }
}
