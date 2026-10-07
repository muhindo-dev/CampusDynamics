using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Caching;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger: THE calculation layer (plan section 1).
//  Every screen, report, dashboard tile, check and export takes its
//  figures from here, so a figure is the same wherever it is shown.
//
//  The ledger (fin_ledger only; fee tracking is never added to it) is
//  aggregated once by "key" and day:
//    - a general-ledger code in the chart is its own key;
//    - student lines, faculty fee lines, supplier lines and the other
//      subsidiary ledgers collapse into one key per family (SUB:...);
//    - codes missing from the chart keep their own key and are shown
//      as provisional (gl_account_map) or unmapped.
//  Amounts are transaction_amount (no line is in another currency).
// =====================================================================

public class GlAcct
{
    public string Key, Code, Name, Category, SubCategory, MainCode, MainName;
    public string Kind;          // CHART | PROVISIONAL | SUBLEDGER | UNMAPPED
    public string ControlCode;   // for a subsidiary ledger: the chart account it belongs under, if any
    public bool Contra;          // accumulated depreciation and similar: credit balance expected on an asset
    public bool DebitNatural { get { return Category == "Assets" || Category == "Expense"; } }
    public string KindText
    {
        get
        {
            switch (Kind)
            {
                case "CHART": return "In the chart";
                case "PROVISIONAL": return "Missing from the chart, mapped provisionally";
                case "SUBLEDGER": return "Subsidiary ledger";
                default: return "Missing from the chart, not mapped";
            }
        }
    }
}

public class GlAgg
{
    public string Key;
    public int Day;              // yyyymmdd
    public long Dr, Cr;
    public int N;
}

public class GlTbRow
{
    public GlAcct A;
    public long OpenDr, OpenCr, MovDr, MovCr, Lines;
    public long Opening { get { return OpenDr - OpenCr; } }
    public long Closing { get { return OpenDr - OpenCr + MovDr - MovCr; } }
}

public class GlVoucherIssue
{
    public long VoucherNo;
    public long Dr, Cr;
    public int Lines, Dates, Sources;
    public string Pattern;       // CREDIT_ONLY | DEBIT_ONLY | REUSED | UNEQUAL | SUBLEDGER_SIDE
    public DateTime First, Last;
    public string Source;
    public long Diff { get { return Dr - Cr; } }
}

public static class GlCalc
{
    public static readonly string[] StudentTypes = { "Student", "-" };
    public static readonly string[] FeeTypes = { "FOEFees", "FSTEADFees", "FBMFees", "FSSAHFees", "BursaryFees" };
    public static readonly string[] OtherSubTypes = { "Supplier", "Salary Advance", "Gratuity", "Sponsor" };

    /// <summary>The one classification expression. Needs fin_ledger as l and LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode.</summary>
    public const string KeyExpr =
        "CASE WHEN s.AccountCode IS NOT NULL THEN l.accountcode " +
        "WHEN l.account_type IN ('Student','-') THEN 'SUB:STUDENTS' " +
        "WHEN l.account_type IN ('FOEFees','FSTEADFees','FBMFees','FSSAHFees','BursaryFees') THEN CONCAT('SUB:', l.account_type) " +
        "WHEN l.account_type IN ('Supplier','Salary Advance','Gratuity','Sponsor') THEN CONCAT('SUB:', REPLACE(l.account_type,' ','')) " +
        "ELSE l.accountcode END";

    // transaction_amount is BIGINT UNSIGNED: any subtraction or negation must be on a SIGNED cast, or MySQL raises
    // error 1690 part-way through the result and the MySql.Data 6.6 connector hangs instead of throwing.
    /// <summary>KeyExpr in C#: the key of one ledger line, for lines read row by row (the voucher page).</summary>
    public static string KeyOf(Snapshot s, string code, string accountType)
    {
        code = (code ?? "").Trim(); accountType = (accountType ?? "").Trim();
        GlAcct a;
        if (s.Accounts.TryGetValue(code, out a) && a.Kind == "CHART") return code;
        if (StudentTypes.Contains(accountType)) return "SUB:STUDENTS";
        if (FeeTypes.Contains(accountType)) return "SUB:" + accountType;
        if (OtherSubTypes.Contains(accountType)) return "SUB:" + accountType.Replace(" ", "");
        return code;
    }

    public const string DrExpr = "IF(l.transactionType='DR', CAST(l.transaction_amount AS SIGNED), 0)";
    public const string CrExpr = "IF(l.transactionType='CR', CAST(l.transaction_amount AS SIGNED), 0)";
    /// <summary>Debit positive, credit negative, for a table aliased l.</summary>
    public const string NetExpr = "IF(l.transactionType='DR', 1, -1) * CAST(l.transaction_amount AS SIGNED)";
    /// <summary>As NetExpr for an unaliased fin_ledger.</summary>
    public const string NetBare = "IF(transactionType='DR', 1, -1) * CAST(transaction_amount AS SIGNED)";

    private static readonly object Gate = new object();

    // ── Ledger fingerprint and the aggregate cache ──────────────────

    /// <summary>Count and highest id of fin_ledger: a new posting changes it, so cached figures refresh at once.</summary>
    public static string Fingerprint(MySqlConnection c)
    {
        DataRow r = GlDb.Table(c, null, "SELECT COUNT(*), IFNULL(MAX(TID),0) FROM fin_ledger").Rows[0];
        return GlDb.L(r[0]) + ":" + GlDb.L(r[1]);
    }

    private const string CachePrefix = "gl:agg:";

    public class Snapshot
    {
        public string Fingerprint;
        public DateTime BuiltAt;
        public long BuildMs;
        public List<GlAgg> Agg;
        public Dictionary<string, GlAcct> Accounts;
        public int MinDay, MaxDay;
    }

    /// <summary>The aggregate, built at most once per ledger change and kept for 5 minutes.</summary>
    public static Snapshot Get(bool forceRefresh)
    {
        using (var c = GlDb.Read())
        {
            string fp = Fingerprint(c);
            string key = CachePrefix + fp;
            Snapshot s = forceRefresh ? null : HttpRuntime.Cache[key] as Snapshot;
            if (s != null) return s;
            lock (Gate)
            {
                s = forceRefresh ? null : HttpRuntime.Cache[key] as Snapshot;
                if (s != null) return s;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                s = new Snapshot { Fingerprint = fp, BuiltAt = DateTime.Now, Agg = new List<GlAgg>(70000) };
                using (var cmd = GlDb.Cmd(c, null,
                    "SELECT " + KeyExpr + " k, l.transactionDate d, SUM(" + DrExpr + ") dr, SUM(" + CrExpr + ") cr, COUNT(*) n " +
                    "FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode GROUP BY k, d"))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        DateTime d = r.IsDBNull(1) ? new DateTime(1900, 1, 1) : r.GetDateTime(1);
                        s.Agg.Add(new GlAgg { Key = r.GetString(0), Day = DayInt(d), Dr = Convert.ToInt64(r[2]), Cr = Convert.ToInt64(r[3]), N = Convert.ToInt32(r[4]) });
                    }
                }
                s.MinDay = s.Agg.Count == 0 ? DayInt(DateTime.Today) : s.Agg.Min(a => a.Day);
                s.MaxDay = s.Agg.Count == 0 ? DayInt(DateTime.Today) : s.Agg.Max(a => a.Day);
                s.Accounts = LoadAccounts(c, s.Agg);
                s.BuildMs = sw.ElapsedMilliseconds;
                HttpRuntime.Cache.Insert(key, s, null, DateTime.UtcNow.AddMinutes(5), Cache.NoSlidingExpiration);
                return s;
            }
        }
    }

    public static int DayInt(DateTime d) { return d.Year * 10000 + d.Month * 100 + d.Day; }
    public static DateTime FromDayInt(int d) { return new DateTime(d / 10000, (d / 100) % 100, d % 100); }

    // ── Accounts: chart, provisional map, subsidiary families, unmapped ──

    private static bool IsContraName(string n)
    {
        n = (n ?? "").ToUpperInvariant();
        return n.Contains("DEPN") || n.Contains("DEPRECIATION") || n.Contains("DEPRC") || n.Contains("ACC. DEP") || n.Contains("ACC DEP") || n.Contains("ACCM") || n.Contains("ACCUM")
            || n.Contains("AMORTISATION") || n.Contains("AMORTIZATION") || n.Contains("PROVISION FOR") || n.Contains("IMPAIRMENT");
    }

    private static Dictionary<string, GlAcct> LoadAccounts(MySqlConnection c, List<GlAgg> agg)
    {
        var d = new Dictionary<string, GlAcct>(StringComparer.OrdinalIgnoreCase);
        var controlByType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow r in GlDb.Table(c, null,
            "SELECT s.AccountCode, s.AccountName, s.MainAccountCode, s.collectionLedgerType, m.AccountName main_name, m.GeneralCategory, m.SubCategory " +
            "FROM fin_subaccounts s LEFT JOIN fin_mainaccounts m ON m.AccountCode = s.MainAccountCode").Rows)
        {
            string code = GlDb.S(r[0]).Trim();
            var a = new GlAcct
            {
                Key = code, Code = code, Name = GlDb.S(r[1]).Trim(), MainCode = GlDb.S(r[2]).Trim(), MainName = GlDb.S(r[4]).Trim(),
                Category = NormCategory(GlDb.S(r[5])), SubCategory = GlDb.S(r[6]).Trim(), Kind = "CHART"
            };
            a.Contra = a.Category == "Assets" && IsContraName(a.Name);
            d[code] = a;
            // A control account must sit on the balance sheet: the chart also links "Supplier" to AC2028 Printing and
            // Stationery (an expense), which is a chart error and is ignored here.
            string ct = GlDb.S(r[3]).Trim();
            if (ct != "" && ct != "Chart Account" && (a.Category == "Assets" || a.Category == "Liabilities") && !controlByType.ContainsKey(ct)) controlByType[ct] = code;
        }
        foreach (DataRow r in GlDb.Table(c, null, "SELECT accountcode, account_name, category, subcategory FROM gl_account_map").Rows)
        {
            string code = GlDb.S(r[0]).Trim();
            if (d.ContainsKey(code)) continue;
            d[code] = new GlAcct { Key = code, Code = code, Name = GlDb.S(r[1]), Category = GlDb.S(r[2]), SubCategory = GlDb.S(r[3]), MainCode = "", MainName = "", Kind = "PROVISIONAL" };
        }
        AddSub(d, "SUB:STUDENTS", "Student fee accounts (subsidiary ledger)", "Assets", "TRADE AND OTHER RECEIVABLES", null);
        foreach (string t in FeeTypes)
        {
            string ctl; controlByType.TryGetValue(t, out ctl);
            AddSub(d, "SUB:" + t, t.Replace("Fees", "") + " student fee lines (subsidiary ledger)", "Assets", "TRADE AND OTHER RECEIVABLES", ctl);
        }
        string sup; controlByType.TryGetValue("Supplier", out sup);
        AddSub(d, "SUB:Supplier", "Supplier accounts (subsidiary ledger)", "Liabilities", "CURRENT LIABILITIES", sup);
        string adv; controlByType.TryGetValue("Salary Advance", out adv);
        AddSub(d, "SUB:SalaryAdvance", "Staff salary advance accounts (subsidiary ledger)", "Assets", "TRADE AND OTHER RECEIVABLES", adv);
        string gra; controlByType.TryGetValue("Gratuity", out gra);
        AddSub(d, "SUB:Gratuity", "Staff gratuity accounts (subsidiary ledger)", "Liabilities", "NON-CURRENT LIABILITIES", gra);
        AddSub(d, "SUB:Sponsor", "Sponsor accounts (subsidiary ledger)", "Liabilities", "CURRENT LIABILITIES", null);
        foreach (GlAgg g in agg)
            if (!d.ContainsKey(g.Key))
                d[g.Key] = new GlAcct { Key = g.Key, Code = g.Key, Name = "Code " + g.Key + " (not in the chart)", Category = "Unclassified", SubCategory = "UNCLASSIFIED", MainCode = "", MainName = "", Kind = "UNMAPPED" };
        return d;
    }

    private static void AddSub(Dictionary<string, GlAcct> d, string key, string name, string cat, string sub, string control)
    {
        d[key] = new GlAcct { Key = key, Code = key, Name = name, Category = cat, SubCategory = sub, MainCode = control ?? "", MainName = "", Kind = "SUBLEDGER", ControlCode = control };
    }

    public static string NormCategory(string c)
    {
        string x = (c ?? "").Trim().ToLowerInvariant();
        if (x.StartsWith("asset")) return "Assets";
        if (x.StartsWith("liabilit")) return "Liabilities";
        if (x.StartsWith("equit")) return "Equity";
        if (x.StartsWith("income") || x.StartsWith("revenue")) return "Income";
        if (x.StartsWith("expen")) return "Expense";
        return "Unclassified";
    }

    public static readonly string[] CategoryOrder = { "Assets", "Liabilities", "Equity", "Income", "Expense", "Suspense", "Unclassified" };

    // ── Trial balance and balances ──────────────────────────────────

    /// <summary>
    /// Trial balance from <paramref name="from"/> to <paramref name="to"/> inclusive: opening (everything before from),
    /// movement, closing. Basis WHOLE = every key; CHART = only chart and provisional accounts.
    /// </summary>
    public static List<GlTbRow> TrialBalance(Snapshot s, DateTime? from, DateTime to, string basis)
    {
        int f = from.HasValue ? DayInt(from.Value) : 0, t = DayInt(to);
        var rows = new Dictionary<string, GlTbRow>(StringComparer.OrdinalIgnoreCase);
        foreach (GlAgg g in s.Agg)
        {
            if (g.Day > t) continue;
            GlAcct a = s.Accounts[g.Key];
            if (basis == "CHART" && a.Kind != "CHART" && a.Kind != "PROVISIONAL") continue;
            GlTbRow r;
            if (!rows.TryGetValue(g.Key, out r)) { r = new GlTbRow { A = a }; rows[g.Key] = r; }
            if (g.Day < f) { r.OpenDr += g.Dr; r.OpenCr += g.Cr; }
            else { r.MovDr += g.Dr; r.MovCr += g.Cr; r.Lines += g.N; }
        }
        return rows.Values.OrderBy(r => Array.IndexOf(CategoryOrder, r.A.Category)).ThenBy(r => r.A.Kind == "SUBLEDGER" ? 1 : 0).ThenBy(r => r.A.Code).ToList();
    }

    /// <summary>Balance (DR minus CR) of one key up to and including a day.</summary>
    public static long Balance(Snapshot s, string key, DateTime asAt)
    {
        int t = DayInt(asAt); long b = 0;
        foreach (GlAgg g in s.Agg) if (g.Day <= t && string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase)) b += g.Dr - g.Cr;
        return b;
    }

    /// <summary>Movement (DR minus CR) per key in a range.</summary>
    public static Dictionary<string, long> Movement(Snapshot s, DateTime from, DateTime to)
    {
        int f = DayInt(from), t = DayInt(to);
        var d = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (GlAgg g in s.Agg)
        {
            if (g.Day < f || g.Day > t) continue;
            long v; d.TryGetValue(g.Key, out v); d[g.Key] = v + g.Dr - g.Cr;
        }
        return d;
    }

    public class Totals { public long Dr, Cr; public long Diff { get { return Dr - Cr; } } public long Lines; }

    public static Totals Total(Snapshot s, DateTime? from, DateTime to, string basis)
    {
        var t = new Totals();
        int f = from.HasValue ? DayInt(from.Value) : 0, e = DayInt(to);
        foreach (GlAgg g in s.Agg)
        {
            if (g.Day < f || g.Day > e) continue;
            GlAcct a = s.Accounts[g.Key];
            if (basis == "CHART" && a.Kind != "CHART" && a.Kind != "PROVISIONAL") continue;
            t.Dr += g.Dr; t.Cr += g.Cr; t.Lines += g.N;
        }
        return t;
    }

    // ── Statements ──────────────────────────────────────────────────

    /// <summary>Income and expenditure for a range: income shown positive (CR minus DR), expense positive (DR minus CR).</summary>
    public static List<KeyValuePair<GlAcct, long>> IncomeExpenditure(Snapshot s, DateTime from, DateTime to)
    {
        var mv = Movement(s, from, to);
        var l = new List<KeyValuePair<GlAcct, long>>();
        foreach (var kv in mv)
        {
            GlAcct a = s.Accounts[kv.Key];
            if (a.Category == "Income") l.Add(new KeyValuePair<GlAcct, long>(a, -kv.Value));
            else if (a.Category == "Expense") l.Add(new KeyValuePair<GlAcct, long>(a, kv.Value));
        }
        return l.OrderBy(x => Array.IndexOf(CategoryOrder, x.Key.Category)).ThenBy(x => x.Key.Code).ToList();
    }

    public class Position
    {
        public List<KeyValuePair<GlAcct, long>> Lines = new List<KeyValuePair<GlAcct, long>>();
        public long Assets, Liabilities, Equity, Surplus, Unclassified;
        /// <summary>
        /// Assets plus unclassified (both debit-signed) minus liabilities, equity and accumulated surplus.
        /// Equals the whole-ledger trial balance difference to the shilling.
        /// </summary>
        public long Difference { get { return Assets + Unclassified - Liabilities - Equity - Surplus; } }
    }

    /// <summary>
    /// Statement of financial position as at a date. No year has ever been closed to retained earnings, so the
    /// accumulated surplus (all income less all expense to date) is shown as its own equity line. Suspense and
    /// unclassified balances are shown, not hidden, as "not classified".
    /// </summary>
    public static Position FinancialPosition(Snapshot s, DateTime asAt)
    {
        var p = new Position();
        int t = DayInt(asAt);
        var bal = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (GlAgg g in s.Agg)
        {
            if (g.Day > t) continue;
            long v; bal.TryGetValue(g.Key, out v); bal[g.Key] = v + g.Dr - g.Cr;
        }
        foreach (var kv in bal)
        {
            GlAcct a = s.Accounts[kv.Key];
            switch (a.Category)
            {
                case "Assets": p.Assets += kv.Value; p.Lines.Add(new KeyValuePair<GlAcct, long>(a, kv.Value)); break;
                case "Liabilities": p.Liabilities += -kv.Value; p.Lines.Add(new KeyValuePair<GlAcct, long>(a, -kv.Value)); break;
                case "Equity": p.Equity += -kv.Value; p.Lines.Add(new KeyValuePair<GlAcct, long>(a, -kv.Value)); break;
                case "Income": p.Surplus += -kv.Value; break;
                case "Expense": p.Surplus -= kv.Value; break;
                default: p.Unclassified += kv.Value; p.Lines.Add(new KeyValuePair<GlAcct, long>(a, kv.Value)); break;
            }
        }
        p.Lines = p.Lines.OrderBy(x => Array.IndexOf(CategoryOrder, x.Key.Category)).ThenBy(x => x.Key.Kind == "SUBLEDGER" ? 1 : 0).ThenBy(x => x.Key.Code).ToList();
        return p;
    }

    /// <summary>Bank and cash accounts: children of the chart's "CASH AND BANK BALANCES" category (AC1300).</summary>
    public static List<GlAcct> CashAccounts(Snapshot s)
    {
        return s.Accounts.Values.Where(a => a.Kind == "CHART" && (a.MainCode == "AC1300" || (a.SubCategory ?? "").ToUpperInvariant().Contains("CASH AND BANK"))).OrderBy(a => a.Code).ToList();
    }

    public class ControlPair
    {
        public GlAcct Control, Sub;
        public int Sign;          // 1 for asset controls (debit positive), -1 for liability controls (credit positive)
        public bool Student;      // student receivables: no control account exists
    }

    /// <summary>Each subsidiary ledger family with the chart account that controls it (from fin_subaccounts.collectionLedgerType). W11 and R17 both use this.</summary>
    public static List<ControlPair> ControlPairs(Snapshot s)
    {
        var l = new List<ControlPair>();
        foreach (GlAcct sub in s.Accounts.Values.Where(a => a.Kind == "SUBLEDGER").OrderBy(a => a.Key))
        {
            GlAcct ctl = null;
            if (!string.IsNullOrEmpty(sub.ControlCode)) s.Accounts.TryGetValue(sub.ControlCode, out ctl);
            bool student = sub.Key == "SUB:STUDENTS";
            if (ctl == null && !student) continue;
            l.Add(new ControlPair { Control = ctl, Sub = sub, Sign = sub.Category == "Liabilities" ? -1 : 1, Student = student });
        }
        return l;
    }

    // ── Financial years ─────────────────────────────────────────────

    public class FinYear { public int Id; public string Label, Status; public DateTime Start, End; public string DateLabel { get { return GlFmt.Date(Start) + " to " + GlFmt.Date(End); } } }

    public static List<FinYear> Years(MySqlConnection c)
    {
        var l = new List<FinYear>();
        foreach (DataRow r in GlDb.Table(c, null, "SELECT id, finacial_Year, start_date, end_date, status FROM fin_financial_years ORDER BY start_date").Rows)
        {
            DateTime? a = GlDb.D(r[2]), b = GlDb.D(r[3]);
            if (!a.HasValue || !b.HasValue) continue;
            l.Add(new FinYear { Id = GlDb.I(r[0]), Label = GlDb.S(r[1]), Start = a.Value, End = b.Value, Status = GlDb.S(r[4]) });
        }
        return l;
    }

    public static FinYear YearOf(List<FinYear> years, DateTime d)
    {
        foreach (FinYear y in years) if (d >= y.Start && d <= y.End) return y;
        return null;
    }

    // ── Voucher analysis (the cause of a trial balance difference) ───

    private const string VoucherCachePrefix = "gl:vch:";

    /// <summary>
    /// Every voucher whose lines inside the range do not balance, with its pattern. On basis CHART only chart and
    /// provisional lines count, and a voucher whose other side sits on a subsidiary ledger is called SUBLEDGER_SIDE.
    /// The differences add up exactly to the range's DR minus CR (plus lines with no voucher number, returned separately).
    /// </summary>
    public static List<GlVoucherIssue> UnbalancedVouchers(DateTime? from, DateTime to, string basis, out long noVoucherDiff, out int noVoucherLines)
    {
        string fp; using (var c0 = GlDb.Read()) fp = Fingerprint(c0);
        string key = VoucherCachePrefix + fp + ":" + (from.HasValue ? GlFmt.Iso(from.Value) : "start") + ":" + GlFmt.Iso(to) + ":" + basis;
        var cached = HttpRuntime.Cache[key] as object[];
        if (cached != null) { noVoucherDiff = (long)cached[1]; noVoucherLines = (int)cached[2]; return (List<GlVoucherIssue>)cached[0]; }

        bool chart = basis == "CHART";
        // The same rule as the aggregate: a mapped code counts as a chart line only when the line is not a subsidiary-ledger line.
        string inChart = "(s.AccountCode IS NOT NULL OR (g.accountcode IS NOT NULL AND LEFT(" + KeyExpr + ",4) <> 'SUB:'))";
        string range = (from.HasValue ? " AND l.transactionDate >= @f" : "") + " AND l.transactionDate <= @t";
        var list = new List<GlVoucherIssue>();
        using (var c = GlDb.Read())
        {
            string sql =
                "SELECT l.voucherNo, SUM(" + (chart ? "IF(" + inChart + "," + DrExpr + ",0)" : DrExpr) + ") dr, SUM(" + (chart ? "IF(" + inChart + "," + CrExpr + ",0)" : CrExpr) + ") cr, " +
                "COUNT(*) n, SUM(l.transactionType='DR') drn, SUM(l.transactionType='CR') crn, COUNT(DISTINCT l.transactionDate) nd, COUNT(DISTINCT IFNULL(l.source_system,'')) ns, " +
                "MIN(l.transactionDate) d0, MAX(l.transactionDate) d1, MAX(IFNULL(l.source_system,'(none)')) src, SUM(NOT " + inChart + ") subn " +
                "FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode LEFT JOIN gl_account_map g ON g.accountcode = l.accountcode " +
                "WHERE l.voucherNo > 0" + range + " GROUP BY l.voucherNo HAVING dr <> cr";
            using (var cmd = GlDb.Cmd(c, null, sql, "@f", from.HasValue ? (object)from.Value : DBNull.Value, "@t", to))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var v = new GlVoucherIssue
                    {
                        VoucherNo = Convert.ToInt64(r[0]), Dr = Convert.ToInt64(r[1]), Cr = Convert.ToInt64(r[2]), Lines = Convert.ToInt32(r[3]),
                        Dates = Convert.ToInt32(r[6]), Sources = Convert.ToInt32(r[7]), First = r.GetDateTime(8), Last = r.GetDateTime(9), Source = r.GetString(10)
                    };
                    int drn = Convert.ToInt32(r[4]), crn = Convert.ToInt32(r[5]), subn = Convert.ToInt32(r[11]);
                    // One-sided first, then reused numbers, then unequal: the order used by the audit (section 4.3).
                    if (chart && subn > 0) v.Pattern = "SUBLEDGER_SIDE";
                    else if (crn == 0) v.Pattern = "DEBIT_ONLY";
                    else if (drn == 0) v.Pattern = "CREDIT_ONLY";
                    else if (v.Dates > 1 || v.Sources > 1) v.Pattern = "REUSED";
                    else v.Pattern = "UNEQUAL";
                    list.Add(v);
                }
            DataRow nv = GlDb.Table(c, null,
                "SELECT IFNULL(SUM(" + (chart ? "IF(" + inChart + "," + DrExpr + "-" + CrExpr + ",0)" : DrExpr + "-" + CrExpr) + "),0), COUNT(*) " +
                "FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode LEFT JOIN gl_account_map g ON g.accountcode = l.accountcode " +
                "WHERE (l.voucherNo IS NULL OR l.voucherNo = 0)" + range, "@f", from.HasValue ? (object)from.Value : DBNull.Value, "@t", to).Rows[0];
            noVoucherDiff = Convert.ToInt64(nv[0]); noVoucherLines = Convert.ToInt32(nv[1]);
        }
        HttpRuntime.Cache.Insert(key, new object[] { list, noVoucherDiff, noVoucherLines }, null, DateTime.UtcNow.AddMinutes(5), Cache.NoSlidingExpiration);
        return list;
    }

    public static string PatternText(string p)
    {
        switch (p)
        {
            case "CREDIT_ONLY": return "Credit-only vouchers (the debit side was never posted under the same number)";
            case "DEBIT_ONLY": return "Debit-only vouchers (the credit side was never posted under the same number)";
            case "REUSED": return "Voucher number reused for unrelated postings (several dates or sources)";
            case "UNEQUAL": return "Two-sided vouchers whose debit and credit differ";
            case "SUBLEDGER_SIDE": return "Other side posted to a subsidiary ledger (students, suppliers, fee lines) only";
            case "NO_VOUCHER": return "Lines with no voucher number";
            default: return p;
        }
    }

    public static string PatternCause(string p)
    {
        switch (p)
        {
            case "CREDIT_ONLY": return "Collections or restores wrote the student or account credit without the matching bank or control debit in the same voucher.";
            case "DEBIT_ONLY": return "Billing wrote the student debit without the matching income credit in the same voucher, or the income line carries a different number.";
            case "REUSED": return "At least four number sequences write into fin_ledger.voucherNo, so unrelated postings share a number.";
            case "UNEQUAL": return "Both sides exist but the amounts differ: an edited line, a partial reversal or a keying error.";
            case "SUBLEDGER_SIDE": return "Students, suppliers and fee lines are kept as subsidiary accounts with no control account, so on the chart-only basis their side is missing.";
            case "NO_VOUCHER": return "Imported lines that were never numbered.";
            default: return "";
        }
    }

    /// <summary>Forgets every cached figure (the Refresh button).</summary>
    public static void Forget()
    {
        var keys = new List<string>();
        var en = HttpRuntime.Cache.GetEnumerator();
        while (en.MoveNext()) { string k = en.Key as string; if (k != null && k.StartsWith("gl:")) keys.Add(k); }
        foreach (string k in keys) HttpRuntime.Cache.Remove(k);
    }
}
