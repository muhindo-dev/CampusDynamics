using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger: the report engine (plan sections 1 and 4).
//  A report is a definition: parameters, columns, a Run that returns rows
//  (with a drill link per row) and its checks. The engine applies search,
//  sort, paging and totals the same way for every report, and the export
//  writers take the very same rows, so the screen, PDF, Excel and CSV agree.
// =====================================================================

public class GlParam
{
    public string Key, Label, Type, Help;      // Type: date | select | bool | account | text
    public string Default;                     // resolved per request by the definition's Defaults
    public bool Required;
    public List<object> Options;               // { v, t }
    public string Role;                        // from | to | asat | year (the year select fills from and to)
    public GlParam(string key, string label, string type) { Key = key; Label = label; Type = type; }
}

public class GlCol
{
    public string Key, Header, Kind;           // Kind: text | code | money | count | date | pct
    public bool Sum, Hidden;
    public float Width;
    public GlCol(string key, string header, string kind, float width) { Key = key; Header = header; Kind = kind; Width = width; Sum = kind == "money"; }
    public GlCol NoSum() { Sum = false; return this; }
    public GlCol Summed() { Sum = true; return this; }
    public bool Numeric { get { return Kind == "money" || Kind == "count" || Kind == "pct"; } }
}

public class GlResult
{
    public string Title, Subtitle, Basis, Noun = "line", NounPlural = "lines", FileWhat;
    public List<GlCol> Cols = new List<GlCol>();
    public List<object[]> Rows = new List<object[]>();
    public List<string> Links = new List<string>();      // one per row, or null entries
    public List<string> Kinds = new List<string>();      // one per row: "" | h (heading) | s (subtotal) | t (total)
    public bool Structured;                              // a statement: no search, sort or paging
    public bool Paged;                                   // the report paged itself (TotalRows is set)
    public long TotalRows;
    public object[] Totals;                              // when null the engine sums the Sum columns
    public bool NoTotals;
    public List<GlCheck> Checks = new List<GlCheck>();
    public List<string> Notes = new List<string>();
    public List<KeyValuePair<string, string>> Cover = new List<KeyValuePair<string, string>>();
    public string[] Signatories = { "Prepared by (Accountant)", "Checked by (Finance Officer)", "Approved by (University Bursar)" };
    public long Ms;

    public void Add(string link, string kind, params object[] cells) { Rows.Add(cells); Links.Add(link); Kinds.Add(kind ?? ""); }
}

public class GlRunCtx
{
    public Dictionary<string, string> P = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public int Page = 1, Size = 50;
    public int SortCol = -1;
    public bool Desc, Export;
    public string Search = "";

    private GlCalc.Snapshot _s;
    private MySqlConnection _c;
    private List<GlCalc.FinYear> _y;
    public GlCalc.Snapshot S { get { if (_s == null) _s = GlCalc.Get(false); return _s; } }
    public MySqlConnection C { get { if (_c == null) _c = GlDb.Read(); return _c; } }
    public List<GlCalc.FinYear> Years { get { if (_y == null) _y = GlCalc.Years(C); return _y; } }
    public void Close() { if (_c != null) { _c.Dispose(); _c = null; } }

    public string Str(string k) { string v; return P.TryGetValue(k, out v) ? (v ?? "").Trim() : ""; }
    public bool Bool(string k) { string v = Str(k).ToLowerInvariant(); return v == "1" || v == "true" || v == "yes" || v == "on"; }
    public DateTime? Date(string k)
    {
        DateTime d;
        return DateTime.TryParseExact(Str(k), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : (DateTime?)null;
    }
    public DateTime DateOr(string k, DateTime dflt) { return Date(k) ?? dflt; }

    /// <summary>The range from the parameters; to defaults to today, from to the start of to's financial year.</summary>
    public void Range(out DateTime from, out DateTime to)
    {
        to = DateOr("to", DateTime.Today);
        GlCalc.FinYear y = GlCalc.YearOf(Years, to);
        from = DateOr("from", y != null ? y.Start : new DateTime(to.Year, 1, 1));
        if (from > to) throw new GlRefusal("The start date is after the end date.");
    }
    public string RangeText(DateTime from, DateTime to) { return GlFmt.Date(from) + " to " + GlFmt.Date(to); }
}

public class GlReportDef
{
    public string Code, Group, Title, Description, Noun;
    public List<GlParam> Params = new List<GlParam>();
    public Func<GlRunCtx, GlResult> Run;
    public Action<GlRunCtx, List<GlParam>> Defaults;     // fills Default and Options per request
}

public static partial class GlReports
{
    private static List<GlReportDef> _defs;
    private static readonly object DefsGate = new object();

    public static List<GlReportDef> Catalogue()
    {
        if (_defs == null) lock (DefsGate) if (_defs == null) _defs = Build();
        return _defs;
    }

    public static GlReportDef Find(string code) { return Catalogue().FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase)); }

    private static List<GlReportDef> Build()
    {
        var l = new List<GlReportDef>();
        l.Add(R01()); l.Add(R02()); l.Add(R03()); l.Add(R04()); l.Add(R05()); l.Add(R06()); l.Add(R07());
        l.Add(R08()); l.Add(R09()); l.Add(R10()); l.Add(R11()); l.Add(R12()); l.Add(R13()); l.Add(R14());
        l.Add(R15()); l.Add(R16()); l.Add(R17()); l.Add(R18()); l.Add(R19()); l.Add(R20());
        return l;
    }

    public static readonly string[] GroupOrder = { "Statements", "Ledgers and vouchers", "Cash and bank", "Receivables and payables", "Spending", "Control and audit" };

    // ── Common parameters ───────────────────────────────────────────

    public static GlParam PYear() { return new GlParam("year", "Financial year", "select") { Role = "year", Help = "Fills the dates below. Choose Custom to type your own." }; }
    public static GlParam PFrom() { return new GlParam("from", "From", "date") { Role = "from" }; }
    public static GlParam PTo(string label = "To") { return new GlParam("to", label, "date") { Role = "to", Required = true }; }
    public static GlParam PBasis()
    {
        return new GlParam("basis", "Basis", "select")
        {
            Default = "WHOLE", Help = "Whole ledger shows each subsidiary ledger as one line. Chart only leaves them out.",
            Options = new List<object> { new { v = "WHOLE", t = "Whole ledger" }, new { v = "CHART", t = "Chart accounts only" } }
        };
    }
    public static GlParam PAccount(bool required) { return new GlParam("account", "Account", "account") { Required = required, Help = "Type a code or name. Student, supplier and fee lines are listed as subsidiary ledgers." }; }

    /// <summary>Default dates and the financial year list, shared by most reports.</summary>
    public static void PeriodDefaults(GlRunCtx x, List<GlParam> ps)
    {
        GlCalc.FinYear cur = GlCalc.YearOf(x.Years, DateTime.Today) ?? x.Years.LastOrDefault();
        foreach (GlParam p in ps)
        {
            if (p.Role == "year")
            {
                p.Options = new List<object> { new { v = "", t = "Custom dates" } };
                foreach (GlCalc.FinYear y in x.Years.OrderByDescending(y => y.Start))
                    p.Options.Add(new { v = GlFmt.Iso(y.Start) + "|" + GlFmt.Iso(y.End), t = y.Label + " (" + y.DateLabel + ")" + (y.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase) ? ", closed" : "") });
                p.Default = cur == null ? "" : GlFmt.Iso(cur.Start) + "|" + GlFmt.Iso(cur.End);
            }
            else if (p.Role == "from" && p.Default == null) p.Default = cur == null ? GlFmt.Iso(new DateTime(DateTime.Today.Year, 1, 1)) : GlFmt.Iso(cur.Start);
            else if ((p.Role == "to" || p.Role == "asat") && p.Default == null) p.Default = GlFmt.Iso(DateTime.Today);
        }
    }

    // ── Catalogue for the page ──────────────────────────────────────

    public static List<object> CatalogueJson()
    {
        var l = new List<object>();
        var x = new GlRunCtx();
        try
        {
            foreach (GlReportDef d in Catalogue().OrderBy(d => Array.IndexOf(GroupOrder, d.Group)).ThenBy(d => d.Code))
            {
                var ps = d.Params.Select(p => new GlParam(p.Key, p.Label, p.Type) { Help = p.Help, Default = p.Default, Required = p.Required, Options = p.Options == null ? null : new List<object>(p.Options), Role = p.Role }).ToList();
                PeriodDefaults(x, ps);
                if (d.Defaults != null) d.Defaults(x, ps);
                l.Add(new
                {
                    code = d.Code, group = d.Group, title = d.Title, description = d.Description,
                    @params = ps.Select(p => new { key = p.Key, label = p.Label, type = p.Type, help = p.Help, dflt = p.Default ?? "", required = p.Required, options = p.Options, role = p.Role ?? "" }).ToList()
                });
            }
        }
        finally { x.Close(); }
        return l;
    }

    // ── Running ─────────────────────────────────────────────────────

    public static GlResult Run(string code, GlRunCtx x)
    {
        GlReportDef d = Find(code);
        if (d == null) throw new GlRefusal("Choose a report.");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        GlResult r;
        try { r = d.Run(x); }
        finally { x.Close(); }
        if (string.IsNullOrEmpty(r.Title)) r.Title = d.Title;
        if (string.IsNullOrEmpty(r.FileWhat)) r.FileWhat = d.Title.ToLowerInvariant().Replace(" and ", "-").Replace(" ", "-");
        while (r.Links.Count < r.Rows.Count) r.Links.Add(null);
        while (r.Kinds.Count < r.Rows.Count) r.Kinds.Add("");
        if (!r.Paged && !r.Structured) Shape(r, x);
        else if (!r.Paged) r.TotalRows = r.Rows.Count;
        if (r.Totals == null && !r.NoTotals) r.Totals = SumTotals(r);
        r.Ms = sw.ElapsedMilliseconds;
        return r;
    }

    /// <summary>Shapes a result built outside a report definition (account card lists) exactly as Run does.</summary>
    public static GlResult Finish(GlResult r, GlRunCtx x)
    {
        while (r.Links.Count < r.Rows.Count) r.Links.Add(null);
        while (r.Kinds.Count < r.Rows.Count) r.Kinds.Add("");
        if (!r.Paged && !r.Structured) Shape(r, x); else if (!r.Paged) r.TotalRows = r.Rows.Count;
        if (r.Totals == null && !r.NoTotals) r.Totals = SumTotals(r);
        return r;
    }

    /// <summary>Search, sort and page an in-memory result. Totals are taken over every matching row, before paging.</summary>
    private static void Shape(GlResult r, GlRunCtx x)
    {
        var idx = Enumerable.Range(0, r.Rows.Count).ToList();
        string q = (x.Search ?? "").Trim();
        if (q != "")
        {
            string ql = q.ToLowerInvariant();
            idx = idx.Where(i =>
            {
                for (int c = 0; c < r.Cols.Count; c++)
                {
                    object v = c < r.Rows[i].Length ? r.Rows[i][c] : null;
                    if (v == null) continue;
                    string s = r.Cols[c].Numeric ? Convert.ToString(v, CultureInfo.InvariantCulture) : (v is DateTime ? GlFmt.Date((DateTime)v) : Convert.ToString(v, CultureInfo.InvariantCulture));
                    if (s.ToLowerInvariant().Contains(ql)) return true;
                }
                return false;
            }).ToList();
        }
        if (x.SortCol >= 0 && x.SortCol < r.Cols.Count)
        {
            int c = x.SortCol;
            Comparison<int> cmp = (a, b) => Compare(r.Rows[a].Length > c ? r.Rows[a][c] : null, r.Rows[b].Length > c ? r.Rows[b][c] : null);
            idx.Sort((a, b) => { int k = cmp(a, b); if (x.Desc) k = -k; return k != 0 ? k : a.CompareTo(b); });
        }
        var rows = idx.Select(i => r.Rows[i]).ToList();
        var links = idx.Select(i => r.Links[i]).ToList();
        var kinds = idx.Select(i => r.Kinds[i]).ToList();
        r.TotalRows = rows.Count;
        if (r.Totals == null && !r.NoTotals) { r.Rows = rows; r.Kinds = kinds; r.Totals = SumTotals(r); }
        if (!x.Export)
        {
            int size = Math.Max(1, Math.Min(500, x.Size)), page = Math.Max(1, x.Page);
            int skip = (page - 1) * size;
            rows = rows.Skip(skip).Take(size).ToList(); links = links.Skip(skip).Take(size).ToList(); kinds = kinds.Skip(skip).Take(size).ToList();
        }
        r.Rows = rows; r.Links = links; r.Kinds = kinds;
    }

    private static int Compare(object a, object b)
    {
        if (a == null || a is DBNull) return b == null || b is DBNull ? 0 : 1;
        if (b == null || b is DBNull) return -1;
        if (a is DateTime && b is DateTime) return ((DateTime)a).CompareTo((DateTime)b);
        if (IsNum(a) && IsNum(b)) return Convert.ToDecimal(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(b, CultureInfo.InvariantCulture));
        return string.Compare(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNum(object o) { return o is decimal || o is long || o is int || o is double; }

    private static object[] SumTotals(GlResult r)
    {
        if (!r.Cols.Any(c => c.Sum)) return null;
        var t = new object[r.Cols.Count];
        for (int c = 0; c < r.Cols.Count; c++)
        {
            if (!r.Cols[c].Sum) continue;
            decimal s = 0;
            for (int i = 0; i < r.Rows.Count; i++)
            {
                if (r.Kinds.Count > i && r.Kinds[i] != "") continue;
                object v = c < r.Rows[i].Length ? r.Rows[i][c] : null;
                if (v != null && IsNum(v)) s += Convert.ToDecimal(v, CultureInfo.InvariantCulture);
            }
            t[c] = s;
        }
        return t;
    }

    // ── Formatting for the screen and the files ─────────────────────

    public static string Cell(GlCol col, object v)
    {
        if (v == null || v is DBNull) return "";
        switch (col.Kind)
        {
            case "money": return IsNum(v) ? GlFmt.Money(Convert.ToDecimal(v, CultureInfo.InvariantCulture)) : Convert.ToString(v, CultureInfo.InvariantCulture);
            case "count": return IsNum(v) ? GlFmt.Count(Convert.ToInt64(v, CultureInfo.InvariantCulture)) : Convert.ToString(v, CultureInfo.InvariantCulture);
            case "pct": return IsNum(v) ? Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("0.0", CultureInfo.InvariantCulture) + "%" : Convert.ToString(v, CultureInfo.InvariantCulture);
            case "date": return v is DateTime ? GlFmt.Date((DateTime)v) : Convert.ToString(v, CultureInfo.InvariantCulture);
            default: return v is DateTime ? GlFmt.Date((DateTime)v) : Convert.ToString(v, CultureInfo.InvariantCulture);
        }
    }

    public static object ToJson(GlResult r, GlRunCtx x)
    {
        var vis = Enumerable.Range(0, r.Cols.Count).Where(i => !r.Cols[i].Hidden).ToList();
        var rows = new List<object>();
        for (int i = 0; i < r.Rows.Count; i++)
            rows.Add(new { c = vis.Select(j => Cell(r.Cols[j], j < r.Rows[i].Length ? r.Rows[i][j] : null)).ToList(), k = r.Kinds[i], l = r.Links[i] });
        return new
        {
            success = true, title = r.Title, subtitle = r.Subtitle, basis = r.Basis, noun = r.Noun, nounPlural = r.NounPlural,
            cols = vis.Select(j => new { k = r.Cols[j].Key, t = r.Cols[j].Header, kind = r.Cols[j].Kind, idx = j, sortable = !r.Structured && !r.Paged }).ToList(),
            rows = rows, total = r.TotalRows, page = x.Page, size = x.Size, structured = r.Structured, paged = r.Paged || !r.Structured,
            totals = r.Totals == null ? null : vis.Select(j => r.Totals[j] == null ? "" : Cell(r.Cols[j], r.Totals[j])).ToList(),
            checks = r.Checks, notes = r.Notes, cover = r.Cover.Select(kv => new[] { kv.Key, kv.Value }).ToList(), ms = r.Ms,
            failing = r.Checks.Count(c => c.status == "fail")
        };
    }

    public static GlRunCtx Ctx(Dictionary<string, object> p, int page, int size, int sortCol, bool desc, string search, bool export)
    {
        var x = new GlRunCtx { Page = page, Size = size, SortCol = sortCol, Desc = desc, Search = search ?? "", Export = export };
        if (p != null) foreach (var kv in p) x.P[kv.Key] = kv.Value == null ? "" : Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
        return x;
    }

    /// <summary>The parameters as printed on covers: label and value, skipping empty ones.</summary>
    public static List<KeyValuePair<string, string>> CoverOf(GlReportDef d, GlRunCtx x)
    {
        var l = new List<KeyValuePair<string, string>>();
        foreach (GlParam p in d.Params)
        {
            if (p.Role == "year") continue;
            string v = x.Str(p.Key);
            if (v == "") continue;
            if (p.Type == "date") { DateTime? dt = x.Date(p.Key); if (dt.HasValue) v = GlFmt.Date(dt.Value); }
            else if (p.Type == "bool") v = x.Bool(p.Key) ? "Yes" : "No";
            else if (p.Type == "account") { GlAcct a; if (x.S.Accounts.TryGetValue(v, out a)) v = a.Code + " " + a.Name; }
            else if (p.Options != null)
            {
                foreach (object o in p.Options)
                {
                    var pv = o.GetType().GetProperty("v"); var pt = o.GetType().GetProperty("t");
                    if (pv != null && Convert.ToString(pv.GetValue(o, null)) == v) { v = Convert.ToString(pt.GetValue(o, null)); break; }
                }
            }
            l.Add(new KeyValuePair<string, string>(p.Label, v));
        }
        if (!string.IsNullOrEmpty(x.Search)) l.Add(new KeyValuePair<string, string>("Search", x.Search));
        return l;
    }

    // ── Account search (typeahead) ──────────────────────────────────

    public static List<object> SearchAccounts(string q, int max)
    {
        GlCalc.Snapshot s = GlCalc.Get(false);
        q = (q ?? "").Trim().ToLowerInvariant();
        var used = new HashSet<string>(s.Agg.Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
        return s.Accounts.Values
            .Where(a => q == "" || (a.Code ?? "").ToLowerInvariant().Contains(q) || (a.Name ?? "").ToLowerInvariant().Contains(q))
            .OrderBy(a => (a.Code ?? "").ToLowerInvariant().StartsWith(q) ? 0 : 1).ThenBy(a => a.Kind == "SUBLEDGER" ? 0 : 1).ThenBy(a => a.Code)
            .Take(max)
            .Select(a => (object)new { id = a.Key, name = a.Code + " " + a.Name, code = a.Code, title = a.Name, kind = a.KindText, category = a.Category, used = used.Contains(a.Key) })
            .ToList();
    }

    // ── Last-used values and saved filters (the module's own tables) ──

    public static Dictionary<string, object> LastUsed(string user)
    {
        var d = new Dictionary<string, object>();
        using (var c = GlDb.Read())
            foreach (DataRow r in GlDb.Table(c, null, "SELECT report_key, params_json FROM gl_user_param WHERE username = @u", "@u", user).Rows)
            {
                try { d[GlDb.S(r[0])] = FaJson.Parse(GlDb.S(r[1])); } catch { }
            }
        return d;
    }

    /// <summary>Remembers the parameters a user last ran a report with. A convenience, not an audited change.</summary>
    public static void RememberParams(string user, string report, string json)
    {
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(report) || json == null || json.Length > 8000) return;
        try
        {
            using (var w = GlDb.Write())
                GlDb.Exec(w, null, "INSERT INTO gl_user_param (username, report_key, params_json, updated_at) VALUES (@u,@r,@j,NOW()) ON DUPLICATE KEY UPDATE params_json=VALUES(params_json), updated_at=NOW()",
                    "@u", GlAudit.Cut(user, 100), "@r", GlAudit.Cut(report, 40), "@j", json);
        }
        catch (Exception ex) { GlLog.Error("RememberParams", ex); }
    }

    public static List<object> SavedFilters(string user)
    {
        var l = new List<object>();
        using (var c = GlDb.Read())
            foreach (DataRow r in GlDb.Table(c, null, "SELECT id, report_key, name, params_json, created_at FROM gl_saved_filter WHERE username = @u AND is_active = 1 ORDER BY report_key, name", "@u", user).Rows)
            {
                object p; try { p = FaJson.Parse(GlDb.S(r[3])); } catch { p = null; }
                l.Add(new { id = GlDb.I(r[0]), report = GlDb.S(r[1]), name = GlDb.S(r[2]), @params = p, created = GlFmt.When(r[4]) });
            }
        return l;
    }

    public static int SaveFilter(string user, string report, string name, string json)
    {
        name = (name ?? "").Trim();
        if (name.Length < 2) throw new GlRefusal("Give the filter a name of at least two characters.");
        if (Find(report) == null) throw new GlRefusal("Choose a report.");
        if (json == null || json.Length > 8000) throw new GlRefusal("These filters are too long to save.");
        using (var w = GlDb.Write())
        using (var tx = w.BeginTransaction())
        {
            long n = GlDb.L(GlDb.Scalar(w, tx, "SELECT COUNT(*) FROM gl_saved_filter WHERE username=@u AND report_key=@r AND name=@n AND is_active=1", "@u", user, "@r", report, "@n", name));
            if (n > 0) throw new GlRefusal("You already have a filter with this name for this report.");
            long id = GlDb.Insert(w, tx, "INSERT INTO gl_saved_filter (username, report_key, name, params_json, is_active, created_at) VALUES (@u,@r,@n,@j,1,NOW())",
                "@u", GlAudit.Cut(user, 100), "@r", report, "@n", GlAudit.Cut(name, 100), "@j", json);
            GlAudit.Write(w, tx, "SAVED_FILTER", id.ToString(CultureInfo.InvariantCulture), "CREATE", null, new { report, name, @params = json }, null);
            tx.Commit();
            return (int)id;
        }
    }

    public static void RemoveFilter(string user, int id)
    {
        using (var w = GlDb.Write())
        using (var tx = w.BeginTransaction())
        {
            DataRow r = GlDb.Table(w, tx, "SELECT report_key, name FROM gl_saved_filter WHERE id=@i AND username=@u AND is_active=1 FOR UPDATE", "@i", id, "@u", user).Rows.Cast<DataRow>().FirstOrDefault();
            if (r == null) throw new GlRefusal("That filter no longer exists.");
            GlDb.Exec(w, tx, "UPDATE gl_saved_filter SET is_active=0 WHERE id=@i", "@i", id);
            GlAudit.Write(w, tx, "SAVED_FILTER", id.ToString(CultureInfo.InvariantCulture), "REMOVE", new { report = GlDb.S(r[0]), name = GlDb.S(r[1]) }, null, null);
            tx.Commit();
        }
    }

    // ── Shared helpers for definitions ──────────────────────────────

    public static string AccountLink(string key, DateTime? from, DateTime? to)
    {
        return "AccountsAccount.aspx?code=" + Uri.EscapeDataString(key ?? "") + (from.HasValue ? "&from=" + GlFmt.Iso(from.Value) : "") + (to.HasValue ? "&to=" + GlFmt.Iso(to.Value) : "");
    }
    public static string VoucherLink(long v) { return "AccountsVoucher.aspx?v=" + v.ToString(CultureInfo.InvariantCulture); }
    public static string ReportLink(string code, params string[] kv)
    {
        var sb = new StringBuilder("AccountsReports.aspx?r=" + code);
        for (int i = 0; i + 1 < kv.Length; i += 2) if (!string.IsNullOrEmpty(kv[i + 1])) sb.Append("&").Append(kv[i]).Append("=").Append(Uri.EscapeDataString(kv[i + 1]));
        sb.Append("&run=1");
        return sb.ToString();
    }

    public static GlCheck Info(string code, string title, string cause) { return new GlCheck { code = code, title = title, status = "info", cause = cause }; }
    public static GlCheck Pass(string code, string title) { return new GlCheck { code = code, title = title, status = "pass" }; }
    public static GlCheck Fail(string code, string title, decimal amount, long count, string cause, GlFix fix)
    {
        return new GlCheck { code = code, title = title, status = "fail", amount = amount, count = count, cause = cause, fix = fix };
    }
}
