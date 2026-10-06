using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

// =====================================================================
//  Fixed Assets module: shared plumbing.
//  Plan: COOPERP/docs/fixed-assets-plan.md
//
//  FaDb        connection and small query helpers (campus_dynamics)
//  FaAccess    slugs, who is signed in, permission checks for PageMethods
//  FaJson      the {success,...} envelope every PageMethod returns
//  FaAudit     fa_audit + acad_activity_log, always inside the caller's transaction
//  FaFinYear   financial year (1 August to 31 July) and month arithmetic
//  FaSettings  fa_settings key/value table
//  FaFmt       money, dates and labels as they appear on screen and paper
// =====================================================================

public static class FaDb
{
    public static string ConnString()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    public static MySqlConnection Open()
    {
        var c = new MySqlConnection(ConnString());
        c.Open();
        return c;
    }

    public static MySqlCommand Cmd(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nameValue)
    {
        var cmd = new MySqlCommand(sql, c, tx);
        cmd.CommandTimeout = 300;
        for (int i = 0; i + 1 < nameValue.Length; i += 2)
            cmd.Parameters.AddWithValue((string)nameValue[i], nameValue[i + 1] ?? DBNull.Value);
        return cmd;
    }

    public static DataTable Table(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nameValue)
    {
        using (var cmd = Cmd(c, tx, sql, nameValue))
        using (var da = new MySqlDataAdapter(cmd))
        {
            var t = new DataTable();
            da.Fill(t);
            return t;
        }
    }

    public static object Scalar(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nameValue)
    {
        using (var cmd = Cmd(c, tx, sql, nameValue))
        {
            object o = cmd.ExecuteScalar();
            return o == DBNull.Value ? null : o;
        }
    }

    public static int Exec(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nameValue)
    {
        using (var cmd = Cmd(c, tx, sql, nameValue)) return cmd.ExecuteNonQuery();
    }

    public static long Insert(MySqlConnection c, MySqlTransaction tx, string sql, params object[] nameValue)
    {
        using (var cmd = Cmd(c, tx, sql, nameValue))
        {
            cmd.ExecuteNonQuery();
            return cmd.LastInsertedId;
        }
    }

    // Typed readers that tolerate DBNull.
    public static string S(object o) { return o == null || o == DBNull.Value ? "" : Convert.ToString(o, CultureInfo.InvariantCulture); }
    public static int I(object o) { if (o == null || o == DBNull.Value) return 0; try { return Convert.ToInt32(o, CultureInfo.InvariantCulture); } catch { return 0; } }
    public static long L(object o) { if (o == null || o == DBNull.Value) return 0; try { return Convert.ToInt64(o, CultureInfo.InvariantCulture); } catch { return 0; } }
    public static decimal M(object o) { if (o == null || o == DBNull.Value) return 0m; try { return Convert.ToDecimal(o, CultureInfo.InvariantCulture); } catch { return 0m; } }
    public static decimal? MN(object o) { if (o == null || o == DBNull.Value) return null; try { return Convert.ToDecimal(o, CultureInfo.InvariantCulture); } catch { return null; } }
    public static int? IN(object o) { if (o == null || o == DBNull.Value) return null; try { return Convert.ToInt32(o, CultureInfo.InvariantCulture); } catch { return null; } }
    public static DateTime? D(object o)
    {
        if (o == null || o == DBNull.Value) return null;
        if (o is DateTime) { DateTime d = (DateTime)o; return d.Year < 1900 ? (DateTime?)null : d; }
        DateTime x;
        return DateTime.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out x) ? (DateTime?)x : null;
    }
    public static object DbVal(object o) { return o ?? DBNull.Value; }
    public static object NullIfEmpty(string s) { s = (s ?? "").Trim(); return s == "" ? (object)DBNull.Value : s; }
}

public static class FaAccess
{
    public const string Parent = "accounts.assets";
    public const string Dashboard = "accounts.assets.dashboard";
    public const string Categories = "accounts.assets.categories";
    public const string Register = "accounts.assets.register";
    public const string Records = "accounts.assets.records";
    public const string Reports = "accounts.assets.reports";
    public const string Edit = "accounts.assets.edit";
    public const string Value = "accounts.assets.value";
    public const string Transfer = "accounts.assets.transfer";
    public const string Dispose = "accounts.assets.dispose";
    public const string CategoriesManage = "accounts.assets.categories_manage";
    public const string Import = "accounts.assets.import";
    public const string YearLock = "accounts.assets.yearlock";

    public static string Username()
    {
        HttpContext x = HttpContext.Current;
        try
        {
            if (x != null && x.Session != null && x.Session["username"] != null)
            {
                string u = x.Session["username"].ToString().Trim();
                if (u != "") return u.Length > 100 ? u.Substring(0, 100) : u;
            }
        }
        catch { }
        return "";
    }

    public static string Role()
    {
        try { string r = RoleAccessService.GetRoleCode(); return r.Length > 40 ? r.Substring(0, 40) : r; }
        catch { return ""; }
    }

    public static string Ip()
    {
        try
        {
            HttpContext x = HttpContext.Current;
            string ip = x == null ? "" : (x.Request.UserHostAddress ?? "");
            return ip.Length > 45 ? ip.Substring(0, 45) : ip;
        }
        catch { return ""; }
    }

    /// <summary>True when the signed-in user holds the slug. Loads the slug set if the session lost it.</summary>
    public static bool Can(string slug)
    {
        string u = Username();
        if (u == "") return false;
        try
        {
            HttpContext x = HttpContext.Current;
            if (string.IsNullOrEmpty(x.Session["access_slugs"] as string)) RoleAccessService.LoadUserAccess(u);
            return RoleAccessService.CanAccess(slug);
        }
        catch { return false; }
    }

    /// <summary>
    /// Guard for the first line of every PageMethod. eadmin PageMethods run even for anonymous
    /// callers, so the method itself must refuse. Returns null when the caller may continue,
    /// otherwise the JSON to return.
    /// </summary>
    public static string Deny(string slug)
    {
        if (Username() == "") return FaJson.Denied("Your session has ended. Sign in again.");
        if (!Can(slug)) return FaJson.Denied("You do not have permission for this action.");
        return null;
    }

    /// <summary>Like <see cref="Deny"/>, and also checks the anti-forgery token for writes.</summary>
    public static string DenyWrite(string slug)
    {
        string d = Deny(slug);
        if (d != null) return d;
        try
        {
            if (!MarksAntiForgeryService.ValidateRequest())
                return FaJson.Denied("Security check failed. Reload the page and try again.");
        }
        catch { return FaJson.Denied("Security check failed. Reload the page and try again."); }
        return null;
    }
}

public static class FaJson
{
    private static JavaScriptSerializer _j;
    public static JavaScriptSerializer J
    {
        get
        {
            if (_j == null) { var j = new JavaScriptSerializer(); j.MaxJsonLength = int.MaxValue; j.RecursionLimit = 200; _j = j; }
            return _j;
        }
    }
    public static string Ser(object o) { return J.Serialize(o); }
    public static string Fail(string message) { return Ser(new { success = false, message = message }); }
    public static string Denied(string message) { return Ser(new { success = false, denied = true, message = message }); }
    public static Dictionary<string, object> Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, object>();
        var d = J.DeserializeObject(json) as Dictionary<string, object>;
        return d ?? new Dictionary<string, object>();
    }

    // Typed readers over a parsed JSON object.
    public static string Str(Dictionary<string, object> d, string k)
    {
        object o; if (d == null || !d.TryGetValue(k, out o) || o == null) return "";
        return Convert.ToString(o, CultureInfo.InvariantCulture).Trim();
    }
    public static int Int(Dictionary<string, object> d, string k)
    {
        string s = Str(d, k); int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
    }
    public static int? IntN(Dictionary<string, object> d, string k)
    {
        string s = Str(d, k); int v; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? (int?)v : null;
    }
    public static decimal? Dec(Dictionary<string, object> d, string k)
    {
        string s = Str(d, k).Replace(",", "").Replace(" ", "");
        decimal v; return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out v) ? (decimal?)v : null;
    }
    public static bool Bool(Dictionary<string, object> d, string k)
    {
        string s = Str(d, k).ToLowerInvariant(); return s == "true" || s == "1" || s == "yes" || s == "on";
    }
    public static DateTime? Date(Dictionary<string, object> d, string k)
    {
        string s = Str(d, k); if (s == "") return null;
        DateTime v;
        string[] f = { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "d MMM yyyy", "dd/MM/yyyy", "d/M/yyyy" };
        if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out v)) return v.Date;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out v) ? (DateTime?)v.Date : null;
    }
    public static List<int> IntList(Dictionary<string, object> d, string k)
    {
        var l = new List<int>();
        object o; if (d == null || !d.TryGetValue(k, out o) || o == null) return l;
        var arr = o as System.Collections.IEnumerable;
        if (arr == null || o is string)
        {
            foreach (string p in Convert.ToString(o, CultureInfo.InvariantCulture).Split(','))
            { int v; if (int.TryParse(p.Trim(), out v) && !l.Contains(v)) l.Add(v); }
            return l;
        }
        foreach (object x in arr) { int v; if (int.TryParse(Convert.ToString(x, CultureInfo.InvariantCulture), out v) && !l.Contains(v)) l.Add(v); }
        return l;
    }
    public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k)
    {
        object o; if (d == null || !d.TryGetValue(k, out o)) return new Dictionary<string, object>();
        return (o as Dictionary<string, object>) ?? new Dictionary<string, object>();
    }
}

public static class FaAudit
{
    /// <summary>
    /// Writes one fa_audit row and one acad_activity_log line inside the caller's transaction.
    /// Not wrapped in try/catch on purpose: if the audit cannot be written, the change must not be.
    /// </summary>
    public static void Write(MySqlConnection c, MySqlTransaction tx, string entity, long entityId, long? assetId,
                             string action, object before, object after, string reason, string summary)
    {
        Write(c, tx, entity, entityId, assetId, action, before, after, reason, summary, true);
    }

    /// <summary>As above; logActivity=false skips the acad_activity_log line (bulk operations log one line for the batch).</summary>
    public static void Write(MySqlConnection c, MySqlTransaction tx, string entity, long entityId, long? assetId,
                             string action, object before, object after, string reason, string summary, bool logActivity)
    {
        string actor = FaAccess.Username(); if (actor == "") actor = "system";
        FaDb.Exec(c, tx,
            "INSERT INTO fa_audit (entity, entity_id, asset_id, action, before_json, after_json, reason, actor, actor_role, ip_address, created_at) " +
            "VALUES (@e,@id,@a,@act,@b,@af,@r,@u,@role,@ip,NOW())",
            "@e", entity, "@id", entityId, "@a", assetId.HasValue ? (object)assetId.Value : DBNull.Value,
            "@act", Cut(action, 40), "@b", before == null ? (object)DBNull.Value : Cut(FaJson.Ser(before), 60000),
            "@af", after == null ? (object)DBNull.Value : Cut(FaJson.Ser(after), 60000),
            "@r", string.IsNullOrEmpty(reason) ? (object)DBNull.Value : Cut(reason, 1000),
            "@u", actor, "@role", FaAccess.Role(), "@ip", FaAccess.Ip());

        // The system-wide activity log: one plain line. Columns are VARCHAR(300) and (200) under strict mode.
        if (!logActivity) return;
        FaDb.Exec(c, tx,
            "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u,'Fixed Assets',@p,@cm,NOW())",
            "@u", Cut(actor, 100), "@p", Cut(summary ?? "", 300), "@cm", Cut(action + (string.IsNullOrEmpty(reason) ? "" : ": " + reason), 200));
    }

    /// <summary>Field-by-field difference of two flat dictionaries, for the before/after columns.</summary>
    public static void Diff(Dictionary<string, object> before, Dictionary<string, object> after,
                            out Dictionary<string, object> b, out Dictionary<string, object> a)
    {
        b = new Dictionary<string, object>(); a = new Dictionary<string, object>();
        foreach (var kv in after)
        {
            object old; before.TryGetValue(kv.Key, out old);
            string os = Norm(old), ns = Norm(kv.Value);
            if (os != ns) { b[kv.Key] = old; a[kv.Key] = kv.Value; }
        }
    }

    private static string Norm(object o)
    {
        if (o == null || o == DBNull.Value) return "";
        if (o is DateTime) return ((DateTime)o).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (o is decimal) return ((decimal)o).ToString("0.##", CultureInfo.InvariantCulture);
        if (o is double) return ((double)o).ToString("0.##", CultureInfo.InvariantCulture);
        return Convert.ToString(o, CultureInfo.InvariantCulture).Trim();
    }

    public static string Cut(string s, int n) { s = s ?? ""; return s.Length > n ? s.Substring(0, n) : s; }
}

public static class FaFinYear
{
    public static int StartMonth { get { int m = FaSettings.Int("fy_start_month", 8); return m < 1 || m > 12 ? 8 : m; } }

    /// <summary>"2025/2026" for any date from 1 Aug 2025 to 31 Jul 2026 (with the default start month).</summary>
    public static string Of(DateTime d)
    {
        int y = d.Month >= StartMonth ? d.Year : d.Year - 1;
        if (StartMonth == 1) return y.ToString(CultureInfo.InvariantCulture);
        return y.ToString(CultureInfo.InvariantCulture) + "/" + (y + 1).ToString(CultureInfo.InvariantCulture);
    }

    public static DateTime Start(DateTime d)
    {
        int y = d.Month >= StartMonth ? d.Year : d.Year - 1;
        return new DateTime(y, StartMonth, 1);
    }

    public static DateTime End(DateTime d) { return Start(d).AddYears(1).AddDays(-1); }

    public static DateTime StartOfLabel(string label)
    {
        int y; if (label == null || label.Length < 4 || !int.TryParse(label.Substring(0, 4), out y)) y = DateTime.Today.Year;
        return new DateTime(y, StartMonth, 1);
    }
    public static DateTime EndOfLabel(string label) { return StartOfLabel(label).AddYears(1).AddDays(-1); }

    public static DateTime MonthStart(DateTime d) { return new DateTime(d.Year, d.Month, 1); }
    public static DateTime MonthEnd(DateTime d) { return new DateTime(d.Year, d.Month, 1).AddMonths(1).AddDays(-1); }

    /// <summary>Whole months from the month of a to the month of b, inclusive of both. 0 if b is before a.</summary>
    public static int MonthsInclusive(DateTime a, DateTime b)
    {
        int n = (b.Year - a.Year) * 12 + (b.Month - a.Month) + 1;
        return n < 0 ? 0 : n;
    }

    /// <summary>Financial years from the first to the last, oldest first.</summary>
    public static List<string> Range(DateTime from, DateTime to)
    {
        var l = new List<string>();
        DateTime s = Start(from);
        while (s <= to) { l.Add(Of(s)); s = s.AddYears(1); }
        return l;
    }

    public static bool IsLocked(MySqlConnection c, MySqlTransaction tx, string finYear)
    {
        object o = FaDb.Scalar(c, tx, "SELECT is_locked FROM fa_year_lock WHERE fin_year=@y", "@y", finYear);
        return FaDb.I(o) == 1;
    }
}

public static class FaSettings
{
    private const string Key = "fa_settings_cache";

    private static Dictionary<string, string> All()
    {
        HttpContext x = HttpContext.Current;
        if (x != null && x.Items[Key] is Dictionary<string, string>) return (Dictionary<string, string>)x.Items[Key];
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var c = FaDb.Open())
                foreach (DataRow r in FaDb.Table(c, null, "SELECT setting_key, setting_value FROM fa_settings").Rows)
                    d[FaDb.S(r[0])] = FaDb.S(r[1]);
        }
        catch { }
        if (x != null) x.Items[Key] = d;
        return d;
    }

    public static string Get(string key, string dflt) { string v; return All().TryGetValue(key, out v) && v != "" ? v : dflt; }
    public static int Int(string key, int dflt) { int v; return int.TryParse(Get(key, ""), out v) ? v : dflt; }
    public static decimal Dec(string key, decimal dflt)
    { decimal v; return decimal.TryParse(Get(key, ""), NumberStyles.Number, CultureInfo.InvariantCulture, out v) ? v : dflt; }
}

public static class FaFmt
{
    public const string University = "Muteesa I Royal University";
    public const string Office = "Office of the University Bursar";

    public static string Money(decimal v) { return Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture); }
    public static string Money(object o) { return o == null || o == DBNull.Value ? "" : Money(FaDb.M(o)); }
    public static string Num(decimal v) { return Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture); }
    public static string Date(DateTime? d) { return d.HasValue ? d.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : ""; }
    public static string Date(object o) { return Date(FaDb.D(o)); }
    public static string Iso(DateTime? d) { return d.HasValue ? d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : ""; }
    public static string Iso(object o) { return Iso(FaDb.D(o)); }

    public static readonly string[] Statuses = { "IN_USE", "IN_STORE", "UNDER_REPAIR", "LOST", "DISPOSED", "WRITTEN_OFF", "VOID" };

    public static string Status(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "IN_USE": return "In use";
            case "IN_STORE": return "In store";
            case "UNDER_REPAIR": return "Under repair";
            case "LOST": return "Lost";
            case "DISPOSED": return "Disposed";
            case "WRITTEN_OFF": return "Written off";
            case "VOID": return "Void";
            default: return s ?? "";
        }
    }

    public static string RecordType(string t)
    {
        switch ((t ?? "").ToUpperInvariant())
        {
            case "ACQUISITION": return "Acquisition";
            case "OPENING": return "Opening balance";
            case "DEPRECIATION": return "Depreciation";
            case "REVALUATION": return "Revaluation";
            case "APPRECIATION": return "Appreciation";
            case "TRANSFER": return "Transfer";
            case "STATUS": return "Status change";
            case "MAINTENANCE": return "Maintenance";
            case "DISPOSAL": return "Disposal";
            case "VERIFICATION": return "Verification";
            case "ESTIMATE": return "Change of estimate";
            case "VOID": return "Void";
            case "REVERSAL": return "Reversal";
            default: return t ?? "";
        }
    }

    public static string Method(string m)
    {
        switch ((m ?? "").ToUpperInvariant())
        {
            case "SL": return "Straight line";
            case "RB": return "Reducing balance";
            case "NONE": return "Not depreciated";
            default: return m ?? "";
        }
    }

    public static string Disposal(string m)
    {
        switch ((m ?? "").ToUpperInvariant())
        {
            case "SALE": return "Sale";
            case "DONATION": return "Donation";
            case "SCRAP": return "Scrapping";
            case "WRITE_OFF": return "Write-off";
            case "TRADE_IN": return "Trade-in";
            case "TRANSFER_OUT": return "Transfer out";
            default: return m ?? "";
        }
    }

    public static string Condition(string c)
    {
        switch ((c ?? "").ToUpperInvariant())
        {
            case "GOOD": return "Good";
            case "FAIR": return "Fair";
            case "POOR": return "Poor";
            case "UNSERVICEABLE": return "Unserviceable";
            default: return c ?? "";
        }
    }

    public static string Plural(long n, string one, string many) { return n.ToString("#,##0", CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many); }
}

/// <summary>
/// Every PageMethod body runs through here: permission (and, for writes, the anti-forgery token) first,
/// then the work; an unexpected error is logged and the user sees a plain sentence, never internals.
/// </summary>
public static class FaApi
{
    public static string Read(string slug, Func<string> work) { return Run(slug, false, work); }
    public static string Write(string slug, Func<string> work) { return Run(slug, true, work); }

    private static string Run(string slug, bool write, Func<string> work)
    {
        string d = write ? FaAccess.DenyWrite(slug) : FaAccess.Deny(slug);
        if (d != null) return d;
        try { return work(); }
        catch (Exception ex)
        {
            FaLog.Error("FaApi " + slug, ex);
            return FaJson.Fail("Something went wrong and nothing was changed. Try again, and tell MIS if it keeps happening.");
        }
    }

    /// <summary>The action permissions of the signed-in user, for showing and hiding buttons (the server still checks each action).</summary>
    public static Dictionary<string, bool> Rights()
    {
        var r = new Dictionary<string, bool>();
        string[] s = { FaAccess.Edit, FaAccess.Value, FaAccess.Transfer, FaAccess.Dispose, FaAccess.CategoriesManage, FaAccess.Import,
                       FaAccess.YearLock, FaAccess.Reports, FaAccess.Records, FaAccess.Register, FaAccess.Dashboard, FaAccess.Categories };
        foreach (string x in s) r[x] = FaAccess.Can(x);
        return r;
    }

    public static object RightsJson()
    {
        var r = Rights();
        return new { edit = r[FaAccess.Edit], value = r[FaAccess.Value], transfer = r[FaAccess.Transfer], dispose = r[FaAccess.Dispose],
                     categories = r[FaAccess.CategoriesManage], import = r[FaAccess.Import], yearLock = r[FaAccess.YearLock],
                     reports = r[FaAccess.Reports], records = r[FaAccess.Records], register = r[FaAccess.Register], dashboard = r[FaAccess.Dashboard] };
    }
}
