using System;
using System.Configuration;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// Who may approve a student for graduation, or put one on hold.
///
/// Scope and right are two different questions and were previously answered by one check.
/// MarksScope says WHICH students you can see; anyone it granted a scope to could also decide,
/// and that included ordinary departmental staff, whose scope exists so they can look at their
/// department's marks. Seeing a candidate and signing them off are not the same authority.
///
/// The decision belongs to three people and no one else: the head of the department that
/// taught the programme, the dean of its faculty, and the Academic Registrar. The system
/// administrator keeps it too, because an administrator who cannot unblock the process is not
/// an administrator, and every decision is recorded against a name either way.
///
/// Dean and head of department come from MarksScope, which derives them from the faculty and
/// department records rather than from role assignments, so they stay true even where RBAC has
/// not been filled in. The Registrar comes from the role, because it is not a scope at all.
/// </summary>
public static class GraduationRights
{
    public const string ROLE_REGISTRAR = "registrar";

    /// <summary>May the signed-in user approve, hold, release or remove?</summary>
    public static bool CanDecide(MarksScope scope)
    {
        string ignored;
        return CanDecide(scope, out ignored);
    }

    /// <summary>
    /// As above, and says why not. The reason is shown to the user, so it names what they
    /// would have to be rather than only what they are not.
    /// </summary>
    public static bool CanDecide(MarksScope scope, out string why)
    {
        why = "";

        if (scope != null && scope.IsAdmin) return true;

        string note = scope == null ? "" : (scope.RoleNote ?? "").Trim();
        if (string.Equals(note, "Dean", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(note, "Head of Department", StringComparison.OrdinalIgnoreCase)) return true;

        if (IsRegistrar()) return true;

        why = "Only a head of department, a dean or the Academic Registrar can approve a " +
              "student for graduation or place one on hold. " +
              (note == "" ? "Your account carries none of those roles."
                          : "Your account is recorded as " + note + ".") +
              " You can still review candidates and read the evidence.";
        return false;
    }

    /// <summary>A short label for what the right rests on, written into the decision record.</summary>
    public static string RightNote(MarksScope scope)
    {
        if (scope != null && scope.IsAdmin) return "Administrator";
        string note = scope == null ? "" : (scope.RoleNote ?? "").Trim();
        if (string.Equals(note, "Dean", StringComparison.OrdinalIgnoreCase)) return "Dean";
        if (string.Equals(note, "Head of Department", StringComparison.OrdinalIgnoreCase)) return "Head of Department";
        if (IsRegistrar()) return "Academic Registrar";
        return note;
    }

    /// <summary>
    /// The session caches one role per user (the query behind it takes the lowest role id), so
    /// a registrar who is also something else can be cached as the other thing. The session is
    /// checked first because it is free, and the user's role assignments are only read when it
    /// does not already answer yes.
    /// </summary>
    private static bool IsRegistrar()
    {
        try
        {
            if (string.Equals(RoleAccessService.GetRoleCode(), ROLE_REGISTRAR,
                              StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { }

        string user = CurrentUser();
        if (user == "") return false;

        try
        {
            var cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
            if (cs == null) return false;
            using (var c = new MySqlConnection(cs.ConnectionString))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM sys_user_roles ur " +
                    "JOIN sys_roles r ON r.id = ur.role_id " +
                    "WHERE ur.username = @u AND ur.is_active = 1 AND r.is_active = 1 " +
                    "  AND (ur.expires_at IS NULL OR ur.expires_at > NOW()) " +
                    "  AND r.role_code = @rc", c))
                {
                    cmd.Parameters.AddWithValue("@u", user);
                    cmd.Parameters.AddWithValue("@rc", ROLE_REGISTRAR);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }
        catch { return false; }
    }

    private static string CurrentUser()
    {
        try
        {
            HttpContext h = HttpContext.Current;
            if (h == null) return "";
            if (h.Session != null)
            {
                string s = Convert.ToString(h.Session["username"] ?? h.Session["usernm"] ?? "").Trim();
                if (s != "") return s;
            }
            if (h.User != null && h.User.Identity != null && h.User.Identity.IsAuthenticated)
                return (h.User.Identity.Name ?? "").Trim();
        }
        catch { }
        return "";
    }
}
