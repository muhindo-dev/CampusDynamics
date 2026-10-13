using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation dates, set once per graduation list year.
//
//  graduation_date  the ceremony date on the CERTIFICATE   -> acad_graduands.grad_date
//  completion_date  the date of completion on the TRANSCRIPT -> acad_graduands.comp_date
//
//  Saving writes the dates onto every graduand of that year, so every
//  renderer that already reads acad_graduands (the PDF certificate and
//  transcript, the classic XtraReports, the batch stored procedures) shows
//  them without being changed. Document generation re-applies them for a
//  student added to the list afterwards (ApplyToStudent).
//
//  A completion date entered on one student (acad_student.completion_date,
//  NewStudentInfo quick-edit) still wins: it is a deliberate correction for
//  that person, the year's date is the default for everyone else.
//
//  Who may set them: an administrator, the Academic Registrar, the Graduation
//  Officer. Every change is logged with the before and after values.
// =====================================================================
public static class GraduationDates
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();
    private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    private static string Conn() { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    public static bool CanSet()
    {
        try
        {
            if (RoleAccessService.IsAdmin()) return true;
            string user = User();
            if (user == "") return false;
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM sys_user_roles ur JOIN sys_roles r ON r.id=ur.role_id " +
                    "WHERE ur.username=@u AND ur.is_active=1 AND r.is_active=1 " +
                    "  AND (ur.expires_at IS NULL OR ur.expires_at>NOW()) " +
                    "  AND r.role_code IN ('admin','registrar','graduation_officer')", c))
                {
                    cmd.Parameters.AddWithValue("@u", user);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }
        catch { return false; }
    }

    private static string User()
    {
        var h = HttpContext.Current;
        if (h == null) return "";
        string u = h.Session != null ? Convert.ToString(h.Session["username"] ?? "").Trim() : "";
        if (u == "" && h.User != null && h.User.Identity != null && h.User.Identity.IsAuthenticated) u = h.User.Identity.Name ?? "";
        return u;
    }

    private static string D(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToDateTime(o).ToString("yyyy-MM-dd", IC); }
    private static string Long(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToDateTime(o).ToString("d MMMM yyyy", IC); }

    /// <summary>The settings for every list year, newest first, with how many graduands each covers.</summary>
    public static string List()
    {
        var rows = new List<object>();
        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();
            using (var cmd = new MySqlCommand(
                "SELECT y.acadyear, d.graduation_date, d.completion_date, IFNULL(d.is_current,0), IFNULL(d.updated_by,''), " +
                "       d.updated_at, y.n, y.mismatch_grad, y.mismatch_comp " +
                "FROM (SELECT g.acadyear, COUNT(*) n, " +
                "        SUM(dd.graduation_date IS NOT NULL AND NOT (g.grad_date <=> dd.graduation_date)) mismatch_grad, " +
                "        SUM(dd.completion_date IS NOT NULL AND NOT (g.comp_date <=> dd.completion_date) " +
                "            AND (s.completion_date IS NULL OR s.completion_date='0000-00-00')) mismatch_comp " +
                "      FROM acad_graduands g LEFT JOIN acad_grad_dates dd ON dd.acadyear=g.acadyear " +
                "      LEFT JOIN acad_student s ON s.regno=g.regno " +
                "      WHERE g.acadyear REGEXP '^[0-9]{4}/[0-9]{4}$' GROUP BY g.acadyear) y " +
                "LEFT JOIN acad_grad_dates d ON d.acadyear=y.acadyear ORDER BY y.acadyear DESC", c))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    rows.Add(new
                    {
                        year = r.GetString(0), graduationDate = D(r[1]), completionDate = D(r[2]),
                        graduationLong = Long(r[1]), completionLong = Long(r[2]),
                        isCurrent = Convert.ToInt32(r[3]) == 1, by = r.GetString(4),
                        at = r.IsDBNull(5) ? "" : Convert.ToDateTime(r[5]).ToString("d MMM yyyy HH:mm", IC),
                        graduands = Convert.ToInt32(r[6]),
                        pending = (r.IsDBNull(7) ? 0 : Convert.ToInt32(r[7])) + (r.IsDBNull(8) ? 0 : Convert.ToInt32(r[8]))
                    });
        }
        int overrides = 0;
        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM acad_graduands g JOIN acad_student s ON s.regno=g.regno " +
                "WHERE s.completion_date IS NOT NULL AND s.completion_date<>'0000-00-00'", c))
                overrides = Convert.ToInt32(cmd.ExecuteScalar());
        }
        return J.Serialize(new { success = true, canSet = CanSet(), years = rows, studentOverrides = overrides });
    }

    /// <summary>Set the dates for one list year and write them onto its graduands.</summary>
    public static string Save(string acadYear, string graduationDate, string completionDate, bool makeCurrent)
    {
        if (!CanSet()) return J.Serialize(new { success = false, message = "Only an administrator, the Academic Registrar or the Graduation Officer may set graduation dates." });
        acadYear = (acadYear ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(acadYear, "^[0-9]{4}/[0-9]{4}$"))
            return J.Serialize(new { success = false, message = "Choose the graduation year." });

        DateTime? gd = null, cd = null; DateTime t;
        if (!string.IsNullOrWhiteSpace(graduationDate))
        {
            if (!DateTime.TryParseExact(graduationDate.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out t))
                return J.Serialize(new { success = false, message = "The graduation date is not a valid date." });
            gd = t;
        }
        if (!string.IsNullOrWhiteSpace(completionDate))
        {
            if (!DateTime.TryParseExact(completionDate.Trim(), "yyyy-MM-dd", IC, DateTimeStyles.None, out t))
                return J.Serialize(new { success = false, message = "The completion date is not a valid date." });
            cd = t;
        }
        if (gd.HasValue && (gd.Value.Year < 2000 || gd.Value.Year > DateTime.Today.Year + 2))
            return J.Serialize(new { success = false, message = "The graduation date looks wrong (" + gd.Value.ToString("d MMM yyyy", IC) + ")." });
        if (cd.HasValue && (cd.Value.Year < 2000 || cd.Value.Year > DateTime.Today.Year + 2))
            return J.Serialize(new { success = false, message = "The completion date looks wrong (" + cd.Value.ToString("d MMM yyyy", IC) + ")." });
        if (gd.HasValue && cd.HasValue && cd.Value > gd.Value)
            return J.Serialize(new { success = false, message = "The date of completion (" + cd.Value.ToString("d MMM yyyy", IC) +
                ") comes after the graduation (" + gd.Value.ToString("d MMM yyyy", IC) + "). Students complete before they graduate." });

        string user = User();
        int touched = 0;
        using (var c = new MySqlConnection(Conn()))
        {
            c.Open();
            using (var tx = c.BeginTransaction())
            {
                try
                {
                    object oldG = null, oldC = null;
                    using (var cmd = new MySqlCommand("SELECT graduation_date, completion_date FROM acad_grad_dates WHERE acadyear=@y FOR UPDATE", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@y", acadYear);
                        using (var r = cmd.ExecuteReader()) if (r.Read()) { oldG = r[0]; oldC = r[1]; }
                    }
                    if (makeCurrent)
                        using (var cmd = new MySqlCommand("UPDATE acad_grad_dates SET is_current=0 WHERE acadyear<>@y", c, tx))
                        { cmd.Parameters.AddWithValue("@y", acadYear); cmd.ExecuteNonQuery(); }
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO acad_grad_dates (acadyear, graduation_date, completion_date, is_current, updated_by, updated_at) " +
                        "VALUES (@y,@g,@c,@cur,@u,NOW()) ON DUPLICATE KEY UPDATE graduation_date=@g, completion_date=@c, " +
                        "is_current=IF(@cur=1,1,is_current), updated_by=@u, updated_at=NOW()", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@y", acadYear);
                        cmd.Parameters.AddWithValue("@g", gd.HasValue ? (object)gd.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@c", cd.HasValue ? (object)cd.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@cur", makeCurrent ? 1 : 0);
                        cmd.Parameters.AddWithValue("@u", user);
                        cmd.ExecuteNonQuery();
                    }
                    touched = ApplyYear(c, tx, acadYear);
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO acad_grad_dates_log (acadyear, old_graduation, new_graduation, old_completion, new_completion, graduands_updated, actor, created_at) " +
                        "VALUES (@y,@og,@g,@oc,@c,@n,@u,NOW())", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@y", acadYear);
                        cmd.Parameters.AddWithValue("@og", oldG ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@g", gd.HasValue ? (object)gd.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@oc", oldC ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@c", cd.HasValue ? (object)cd.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@n", touched);
                        cmd.Parameters.AddWithValue("@u", user);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u,'Graduation dates',@y,@m,NOW())", c, tx))
                    {
                        cmd.Parameters.AddWithValue("@u", user.Length > 100 ? user.Substring(0, 100) : user);
                        cmd.Parameters.AddWithValue("@y", acadYear);
                        cmd.Parameters.AddWithValue("@m", "graduation " + (gd.HasValue ? gd.Value.ToString("yyyy-MM-dd", IC) : "-") +
                                                          ", completion " + (cd.HasValue ? cd.Value.ToString("yyyy-MM-dd", IC) : "-") + ", " + touched + " graduands");
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    try { tx.Rollback(); } catch { }
                    return J.Serialize(new { success = false, message = "Nothing was saved: " + ex.Message });
                }
            }
        }
        return J.Serialize(new { success = true, updated = touched,
            message = "Dates for the " + acadYear + " graduation saved and applied to " + touched + " graduand" + (touched == 1 ? "" : "s") + "." });
    }

    private const string APPLY =
        "UPDATE acad_graduands g JOIN acad_grad_dates d ON d.acadyear=g.acadyear " +
        "LEFT JOIN acad_student s ON s.regno=g.regno " +
        "SET g.grad_date = COALESCE(d.graduation_date, g.grad_date), " +
        "    g.comp_date = CASE WHEN s.completion_date IS NOT NULL AND s.completion_date<>'0000-00-00' THEN s.completion_date " +
        "                       WHEN d.completion_date IS NOT NULL THEN d.completion_date ELSE g.comp_date END ";

    private static int ApplyYear(MySqlConnection c, MySqlTransaction tx, string acadYear)
    {
        int n;
        using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM acad_graduands WHERE acadyear=@y", c, tx))
        { cmd.Parameters.AddWithValue("@y", acadYear); n = Convert.ToInt32(cmd.ExecuteScalar()); }
        using (var cmd = new MySqlCommand(APPLY + "WHERE g.acadyear=@y", c, tx))
        { cmd.Parameters.AddWithValue("@y", acadYear); cmd.CommandTimeout = 120; cmd.ExecuteNonQuery(); }
        return n;
    }

    /// <summary>
    /// Called while a document is generated, after the completion default has been computed: the
    /// student's list year's dates (if set) replace the defaults on their graduand row.
    /// </summary>
    public static void ApplyToStudent(MySqlConnection c, string regno)
    {
        try
        {
            using (var cmd = new MySqlCommand(APPLY + "WHERE TRIM(g.regno)=TRIM(@r)", c))
            { cmd.Parameters.AddWithValue("@r", regno ?? ""); cmd.ExecuteNonQuery(); }
        }
        catch { }
    }
}
