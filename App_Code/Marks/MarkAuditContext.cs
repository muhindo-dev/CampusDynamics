using System;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Names the person behind a mark change, for the database triggers that record it.
///
/// The triggers on acad_results and acad_course_registration capture every change no matter
/// which code wrote it — that is the point of putting the audit in the database. What a
/// trigger cannot know is WHO: by the time it runs, all it has is a connection. So the
/// application says so first, on that same connection, and the trigger reads it back.
///
/// Call Set() once after opening a connection that is about to touch marks. The trigger's
/// freshness guard is 60 seconds, which covers every write in a normal request, so this does
/// not need repeating per statement.
///
/// Everything here is best-effort and swallows its own errors. Attribution must never be the
/// reason a mark cannot be saved; an unattributed change is still recorded, as 'system'.
/// </summary>
public static class MarkAuditContext
{
    /// <summary>Declares the actor for every mark write on this connection for the next 60 seconds.</summary>
    public static void Set(MySqlConnection conn, MySqlTransaction tx, string source, string reason)
    {
        Set(conn, tx, ResolveActor(), source, reason);
    }

    /// <summary>The same, when the caller already knows who to name (a batch run, an API key).</summary>
    public static void Set(MySqlConnection conn, MySqlTransaction tx, string actor, string source, string reason)
    {
        if (conn == null) return;
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "REPLACE INTO campus_dynamics.mark_audit_context (conn_id, actor, source, reason, ip, set_at) " +
                "VALUES (CONNECTION_ID(), @a, @s, @r, @ip, NOW())", conn, tx))
            {
                cmd.Parameters.AddWithValue("@a", Clip(actor, 90));
                cmd.Parameters.AddWithValue("@s", Clip(source, 100));
                cmd.Parameters.AddWithValue("@r", Clip(reason, 200));
                cmd.Parameters.AddWithValue("@ip", Clip(ClientIp(), 45));
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    /// <summary>
    /// Who is doing this. The marks service knows first; otherwise the session, which is what
    /// the older pages set. A blank answer is left blank rather than filled with a guess —
    /// 'system' is an honest record, somebody else's name is not.
    /// </summary>
    public static string ResolveActor()
    {
        try
        {
            string u = MarksAuthorizationService.GetCurrentUser();
            if (!string.IsNullOrEmpty(u) && u.Trim() != "") return u.Trim();
        }
        catch { }
        try
        {
            HttpContext c = HttpContext.Current;
            if (c != null && c.Session != null)
            {
                foreach (string key in new string[] { "username", "usernm", "ScreenName", "regno" })
                {
                    object v = c.Session[key];
                    if (v != null && v.ToString().Trim() != "") return v.ToString().Trim();
                }
            }
            if (c != null && c.User != null && c.User.Identity != null
                && c.User.Identity.IsAuthenticated && !string.IsNullOrEmpty(c.User.Identity.Name))
                return c.User.Identity.Name.Trim();
        }
        catch { }
        return "";
    }

    private static string ClientIp()
    {
        try
        {
            HttpContext c = HttpContext.Current;
            if (c == null || c.Request == null) return "";
            string ip = c.Request.Headers["X-Forwarded-For"];
            if (!string.IsNullOrEmpty(ip) && ip.IndexOf(',') > 0) ip = ip.Split(',')[0];
            if (string.IsNullOrEmpty(ip)) ip = c.Request.UserHostAddress;
            return (ip ?? "").Trim();
        }
        catch { return ""; }
    }

    private static string Clip(string v, int n)
    {
        v = (v ?? "").Trim();
        return v.Length <= n ? v : v.Substring(0, n);
    }
}
