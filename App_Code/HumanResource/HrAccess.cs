using System;
using System.Configuration;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Who may use the HR module. One rule for every HR screen and every ?action= / ?ajax= handler:
/// signed in, and admin, or the RBAC role admin / hr_manager, or a legacy HR / administrator role.
/// Content pages run Page_Init / Page_Load before SidebarMaster's login check, so each HR
/// handler must call <see cref="RequireHr"/> itself before doing anything.
/// </summary>
public static class HrAccess
{
    private const string CacheKey = "hr_access_ok";

    public static string Username()
    {
        HttpContext ctx = HttpContext.Current;
        try
        {
            if (ctx != null && ctx.Session != null && ctx.Session["username"] != null)
            {
                string u = ctx.Session["username"].ToString().Trim();
                if (u != "") return u;
            }
        }
        catch { }
        try
        {
            if (ctx != null && ctx.User != null && ctx.User.Identity != null && ctx.User.Identity.IsAuthenticated)
                return ctx.User.Identity.Name ?? "";
        }
        catch { }
        return "";
    }

    public static bool IsSignedIn() { return Username() != ""; }

    public static bool IsHr()
    {
        HttpContext ctx = HttpContext.Current;
        string u = Username();
        if (u == "") return false;
        if (ctx != null && ctx.Items[CacheKey] != null) return (bool)ctx.Items[CacheKey];
        bool ok = false;
        try
        {
            if (RoleAccessService.IsAdmin()) ok = true;
            if (!ok)
            {
                using (MySqlConnection c = new MySqlConnection(ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString))
                {
                    c.Open();
                    using (MySqlCommand cmd = new MySqlCommand(
                        @"SELECT
                            (SELECT COUNT(*) FROM sys_user_roles ur JOIN sys_roles r ON r.id = ur.role_id
                              WHERE ur.username = @u AND ur.is_active = 1 AND r.is_active = 1
                                AND (ur.expires_at IS NULL OR ur.expires_at > NOW())
                                AND r.role_code IN ('admin','hr_manager'))
                          + (SELECT COUNT(*) FROM my_aspnet_users mu
                               JOIN my_aspnet_usersinroles mur ON mur.userId = mu.id
                               JOIN my_aspnet_roles mr ON mr.id = mur.roleId
                              WHERE mu.name = @u
                                AND mr.name IN ('Administrator','System Admin','Human Resource','Human Resource Manager'))", c))
                    {
                        cmd.Parameters.AddWithValue("@u", u);
                        ok = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
        }
        catch { ok = false; }
        if (ctx != null) ctx.Items[CacheKey] = ok;
        return ok;
    }

    /// <summary>
    /// Stops the request unless the caller is signed in and has HR access.
    /// JSON callers get {"success":false,...} with 401/403; page requests are redirected to login.
    /// Returns true when the caller may continue.
    /// </summary>
    public static bool RequireHr(bool json)
    {
        HttpContext ctx = HttpContext.Current;
        if (ctx == null) return false;
        bool signedIn = IsSignedIn();
        if (signedIn && IsHr()) return true;
        HttpResponse r = ctx.Response;
        r.Clear();
        if (json)
        {
            // 403 for both: a 401 makes forms authentication append its login redirect to the JSON
            r.StatusCode = 403;
            r.ContentType = "application/json";
            r.Write(signedIn
                ? "{\"success\":false,\"ok\":false,\"message\":\"You do not have access to the HR module.\",\"error\":\"You do not have access to the HR module.\"}"
                : "{\"success\":false,\"ok\":false,\"message\":\"Your session has expired. Please sign in again.\",\"error\":\"Your session has expired. Please sign in again.\"}");
        }
        else
        {
            r.StatusCode = signedIn ? 403 : 302;
            if (!signedIn) r.RedirectLocation = VirtualPathUtility.ToAbsolute("~/Default.aspx");
            else r.Write("You do not have access to the HR module.");
        }
        r.Flush();
        ctx.ApplicationInstance.CompleteRequest();
        r.End();
        return false;
    }
}
