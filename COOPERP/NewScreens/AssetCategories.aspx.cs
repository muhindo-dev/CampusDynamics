using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Services;

// Fixed Assets: categories and sub-categories (plan 4.2). Two-level tree, drag and drop, inherited defaults.
public partial class COOPERP_NewScreens_AssetCategories : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, FaAccess.Categories);
    }

    [WebMethod(EnableSession = true)]
    public static string GetTree()
    {
        return FaApi.Read(FaAccess.Categories, delegate
        {
            using (var c = FaDb.Open())
                return FaJson.Ser(new
                {
                    success = true, canManage = FaAccess.Can(FaAccess.CategoriesManage), nodes = FaCategories.Tree(c, false), glAccounts = FaCategories.GlAccounts(c),
                    defaults = new { capThreshold = FaSettings.Dec("cap_threshold_default", 0m), revalueMonths = FaSettings.Int("revalue_months_default", 12) }
                });
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SaveNode(string json)
    {
        return FaApi.Write(FaAccess.CategoriesManage, delegate
        {
            int id;
            string e = FaCategories.Save(FaJson.Parse(json), out id);
            return e == null ? FaJson.Ser(new { success = true, id = id }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SetActive(int id, bool active, string reason)
    {
        return FaApi.Write(FaAccess.CategoriesManage, delegate
        {
            string e = FaCategories.SetActive(id, active, reason);
            return e == null ? FaJson.Ser(new { success = true }) : FaJson.Fail(e);
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SaveLayout(string opsJson)
    {
        return FaApi.Write(FaAccess.CategoriesManage, delegate
        {
            var arr = FaJson.J.DeserializeObject(opsJson) as object[];
            var ops = new List<Dictionary<string, object>>();
            if (arr != null) foreach (object o in arr) { var d = o as Dictionary<string, object>; if (d != null) ops.Add(d); }
            int changed;
            string e = FaCategories.SaveLayout(ops, out changed);
            return e == null ? FaJson.Ser(new { success = true, changed = changed }) : FaJson.Fail(e);
        });
    }
}
