using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger (Expenditure and Accounts rebuild): shared plumbing.
//  Plan: COOPERP/docs/expenditure-accounts-plan.md
//
//  GlDb      two connections. Read() uses the read-only account cd_gl_ro and
//            serves every report, check and warning. Write() is used only by
//            GlWrite for the module's own tables and posted adjusting entries.
//  GlAccess  slugs and permission checks.
//  GlApi     the wrapper every PageMethod runs through.
//  GlAudit   gl_audit, always inside the caller's transaction.
//  GlFmt     money, dates and labels, the same everywhere.
// =====================================================================

public static class GlDb
{
    public static string ReadConnString()
    {
        var cs = ConfigurationManager.ConnectionStrings["accountsReadOnlyConnectionString"];
        if (cs == null || string.IsNullOrEmpty(cs.ConnectionString))
            throw new GlRefusal("The read-only finance connection is not configured (accountsReadOnlyConnectionString). Ask MIS.");
        return cs.ConnectionString;
    }

    public static string WriteConnString()
    {
        return ConfigurationManager.ConnectionStrings["accountsConnectionString"].ConnectionString;
    }

    /// <summary>Read-only connection for reports, checks and warnings. MySQL refuses any write on it.</summary>
    public static MySqlConnection Read()
    {
        var c = new MySqlConnection(ReadConnString());
        c.Open();
        return c;
    }

    /// <summary>Read-write connection, used only by GlWrite.</summary>
    public static MySqlConnection Write()
    {
        var c = new MySqlConnection(WriteConnString());
        c.Open();
        return c;
    }

    public static MySqlCommand Cmd(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nv)
    {
        var cmd = new MySqlCommand(sql, c, tx);
        cmd.CommandTimeout = 300;
        for (int i = 0; i + 1 < nv.Length; i += 2) cmd.Parameters.AddWithValue((string)nv[i], nv[i + 1] ?? DBNull.Value);
        return cmd;
    }

    public static DataTable Table(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nv)
    {
        using (var cmd = Cmd(c, tx, sql, nv))
        using (var da = new MySqlDataAdapter(cmd)) { var t = new DataTable(); da.Fill(t); return t; }
    }

    public static object Scalar(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nv)
    {
        using (var cmd = Cmd(c, tx, sql, nv)) { object o = cmd.ExecuteScalar(); return o == DBNull.Value ? null : o; }
    }

    public static int Exec(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nv)
    {
        using (var cmd = Cmd(c, tx, sql, nv)) return cmd.ExecuteNonQuery();
    }

    public static long Insert(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nv)
    {
        using (var cmd = Cmd(c, tx, sql, nv)) { cmd.ExecuteNonQuery(); return cmd.LastInsertedId; }
    }

    public static string S(object o) { return FaDb.S(o); }
    public static int I(object o) { return FaDb.I(o); }
    public static long L(object o) { return FaDb.L(o); }
    public static decimal M(object o) { return FaDb.M(o); }
    public static DateTime? D(object o) { return FaDb.D(o); }
}

/// <summary>A rule refused the action; the message is shown as is.</summary>
public class GlRefusal : Exception
{
    public GlRefusal(string message) : base(message) { }
}

public static class GlAccess
{
    public const string Parent = "accounts.gl";
    public const string Dashboard = "accounts.gl.dashboard";
    public const string Reports = "accounts.gl.reports";
    public const string Warnings = "accounts.gl.warnings";
    public const string WarningsManage = "accounts.gl.warnings_manage";
    public const string Adjust = "accounts.gl.adjust";
    public const string AdjustApprove = "accounts.gl.adjust_approve";
    public const string Periods = "accounts.gl.periods";
    public const string PeriodsManage = "accounts.gl.periods_manage";
    public const string Account = "accounts.gl.account";
    public const string Voucher = "accounts.gl.voucher";

    public static string User() { return FaAccess.Username(); }
    public static string Role() { return FaAccess.Role(); }
    public static string Ip() { return FaAccess.Ip(); }
    public static bool Can(string slug) { return FaAccess.Can(slug); }

    public static object RightsJson()
    {
        return new
        {
            dashboard = Can(Dashboard), reports = Can(Reports), warnings = Can(Warnings), manage = Can(WarningsManage),
            adjust = Can(Adjust), approve = Can(AdjustApprove), periods = Can(Periods), periodsManage = Can(PeriodsManage),
            user = User()
        };
    }
}

/// <summary>Every PageMethod body runs through here: permission (and the anti-forgery token for writes) first.</summary>
public static class GlApi
{
    public static string Read(string slug, Func<string> work) { return Run(slug, false, work); }
    public static string Write(string slug, Func<string> work) { return Run(slug, true, work); }

    private static string Run(string slug, bool write, Func<string> work)
    {
        string d = write ? FaAccess.DenyWrite(slug) : FaAccess.Deny(slug);
        if (d != null) return d;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { return work(); }
        catch (GlRefusal r) { return FaJson.Fail(r.Message); }
        catch (MySqlException mx)
        {
            if (mx.Number == 1644) return FaJson.Fail(mx.Message);
            GlLog.Error("GlApi " + slug, mx);
            return FaJson.Fail(mx.Number == 1142 ? "The read-only finance account was asked to write. Nothing was changed; tell MIS."
                                                  : "The figures could not be produced. Try again, and tell MIS if it keeps happening.");
        }
        catch (Exception ex)
        {
            GlLog.Error("GlApi " + slug, ex);
            return FaJson.Fail("Something went wrong and nothing was changed. Try again, and tell MIS if it keeps happening.");
        }
    }

    /// <summary>Serialises an anonymous object with success=true merged in.</summary>
    public static string Ok(object o)
    {
        var d = new Dictionary<string, object>();
        d["success"] = true;
        if (o != null)
        {
            var dict = o as IDictionary<string, object>;
            if (dict != null) foreach (var kv in dict) d[kv.Key] = kv.Value;
            else foreach (var p in o.GetType().GetProperties()) d[p.Name] = p.GetValue(o, null);
        }
        return FaJson.Ser(d);
    }
}

public static class GlAudit
{
    /// <summary>One gl_audit row inside the caller's transaction. If the audit cannot be written, the change must not be.</summary>
    public static void Write(MySqlConnection c, MySqlTransaction tx, string entity, string entityId, string action, object before, object after, string reason)
    {
        WriteAs(GlAccess.User(), c, tx, entity, entityId, action, before, after, reason);
    }

    /// <summary>As <see cref="Write"/>, for work with no signed-in user (the scheduled detection run).</summary>
    public static void WriteAs(string actor, MySqlConnection c, MySqlTransaction tx, string entity, string entityId, string action, object before, object after, string reason)
    {
        if (string.IsNullOrEmpty(actor)) actor = "system";
        GlDb.Exec(c, tx,
            "INSERT INTO gl_audit (entity, entity_id, action, before_json, after_json, reason, actor, actor_role, ip_address, created_at) VALUES (@e,@i,@a,@b,@af,@r,@u,@role,@ip,NOW())",
            "@e", Cut(entity, 30), "@i", Cut(entityId, 60), "@a", Cut(action, 40),
            "@b", before == null ? (object)DBNull.Value : Cut(FaJson.Ser(before), 60000),
            "@af", after == null ? (object)DBNull.Value : Cut(FaJson.Ser(after), 60000),
            "@r", string.IsNullOrEmpty(reason) ? (object)DBNull.Value : Cut(reason, 2000),
            "@u", Cut(actor, 100), "@role", GlAccess.Role(), "@ip", GlAccess.Ip());
    }

    public static string Cut(string s, int n) { s = s ?? ""; return s.Length > n ? s.Substring(0, n) : s; }
}

public static class GlSettings
{
    private const string Key = "gl_settings_cache";

    public static Dictionary<string, string> All()
    {
        HttpContext x = HttpContext.Current;
        if (x != null && x.Items[Key] is Dictionary<string, string>) return (Dictionary<string, string>)x.Items[Key];
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try { using (var c = GlDb.Read()) foreach (DataRow r in GlDb.Table(c, null, "SELECT setting_key, setting_value FROM gl_settings").Rows) d[GlDb.S(r[0])] = GlDb.S(r[1]); }
        catch { }
        if (x != null) x.Items[Key] = d;
        return d;
    }

    public static decimal Dec(string key, decimal dflt)
    {
        string v; decimal n;
        return All().TryGetValue(key, out v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out n) ? n : dflt;
    }
    public static int Int(string key, int dflt) { return (int)Dec(key, dflt); }
}

public static class GlFmt
{
    public const string University = "Muteesa I Royal University";
    public const string Office = "Office of the University Bursar";

    /// <summary>1,234,567 and (1,234,567) for negatives, everywhere: screen, PDF and Excel display.</summary>
    public static string Money(decimal v)
    {
        decimal r = Math.Round(v, 0, MidpointRounding.AwayFromZero);
        string s = Math.Abs(r).ToString("#,##0", CultureInfo.InvariantCulture);
        return r < 0 ? "(" + s + ")" : s;
    }
    public static string Date(DateTime? d) { return d.HasValue ? d.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : ""; }
    public static string Date(object o) { return Date(GlDb.D(o)); }
    public static string Iso(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
    public static string Iso(object o) { DateTime? d = GlDb.D(o); return d.HasValue ? Iso(d.Value) : ""; }
    public static string When(object o) { DateTime? d = GlDb.D(o); return d.HasValue ? d.Value.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : ""; }
    public static string Count(long n) { return n.ToString("#,##0", CultureInfo.InvariantCulture); }
    public static string Plural(long n, string one, string many) { return Count(n) + " " + (n == 1 ? one : many); }
}

public static class GlLog
{
    public static void Error(string where, Exception ex)
    {
        try
        {
            string dir = HttpContext.Current != null ? HttpContext.Current.Server.MapPath("~/App_Data/Ledger") : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data\\Ledger");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "errors.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + where + " " + (HttpContext.Current != null ? GlAccess.User() : "") + " " + ex + Environment.NewLine);
        }
        catch { }
    }
}
