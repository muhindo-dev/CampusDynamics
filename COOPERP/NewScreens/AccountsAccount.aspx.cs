using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web.Services;

// General Ledger: account card (plan section 3). Balance and movement by month, the statement,
// members of a subsidiary ledger, warnings on the account, and the presentation mapping for codes
// missing from the chart.
public partial class COOPERP_NewScreens_AccountsAccount : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GlAccess.Account);
        var boot = new Dictionary<string, object>();
        try
        {
            boot = Card(Request.QueryString["code"], Request.QueryString["m"], Request.QueryString["from"], Request.QueryString["to"]);
        }
        catch (Exception ex)
        {
            if (!(ex is GlRefusal)) GlLog.Error("AccountsAccount.Load", ex);
            boot["error"] = ex is GlRefusal ? ex.Message : "The account could not be loaded. Reload the page, and tell MIS if it keeps happening.";
        }
        boot["rights"] = GlAccess.RightsJson();
        BootJson = FaJson.Ser(boot).Replace("<", "\\u003c");
    }

    private static Dictionary<string, object> Card(string code, string member, string fromS, string toS)
    {
        var d = new Dictionary<string, object>();
        GlCalc.Snapshot s = GlCalc.Get(false);
        GlAcct a;
        if (string.IsNullOrEmpty(code) || !s.Accounts.TryGetValue(code.Trim(), out a)) throw new GlRefusal("Choose an account from a report or search for one.");
        member = (member ?? "").Trim();
        DateTime to, from;
        if (!DateTime.TryParseExact(toS ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out to)) to = DateTime.Today;
        List<GlCalc.FinYear> years;
        using (var c = GlDb.Read()) years = GlCalc.Years(c);
        GlCalc.FinYear y = GlCalc.YearOf(years, to);
        if (!DateTime.TryParseExact(fromS ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out from)) from = y != null ? y.Start : new DateTime(to.Year, 1, 1);
        if (from > to) from = to;

        d["account"] = new
        {
            key = a.Key, code = a.Code, name = a.Name, kind = a.Kind, kindText = a.KindText, category = a.Category, subcategory = a.SubCategory ?? "",
            main = a.MainCode ?? "", mainName = a.MainName ?? "", control = a.ControlCode ?? "", controlName = !string.IsNullOrEmpty(a.ControlCode) && s.Accounts.ContainsKey(a.ControlCode) ? s.Accounts[a.ControlCode].Name : "",
            contra = a.Contra, debitNatural = a.DebitNatural
        };
        d["member"] = member;
        d["from"] = GlFmt.Iso(from); d["to"] = GlFmt.Iso(to);
        d["years"] = years.OrderByDescending(v => v.Start).Select(v => new { v = GlFmt.Iso(v.Start) + "|" + GlFmt.Iso(v.End), t = v.Label + " (" + v.DateLabel + ")" }).ToList();

        // Summary and months (whole key from the snapshot; a member from SQL).
        var months = new List<object>();
        long open = 0, dr = 0, cr = 0, lines = 0;
        if (member == "")
        {
            int f = GlCalc.DayInt(from), t = GlCalc.DayInt(to);
            var byMonth = new SortedDictionary<int, long[]>();
            foreach (GlAgg g in s.Agg)
            {
                if (!string.Equals(g.Key, a.Key, StringComparison.OrdinalIgnoreCase) || g.Day > t) continue;
                if (g.Day < f) { open += g.Dr - g.Cr; continue; }
                int m = g.Day / 100; long[] v;
                if (!byMonth.TryGetValue(m, out v)) { v = new long[3]; byMonth[m] = v; }
                v[0] += g.Dr; v[1] += g.Cr; v[2] += g.N; dr += g.Dr; cr += g.Cr; lines += g.N;
            }
            long run = open;
            foreach (var kv in byMonth)
            {
                run += kv.Value[0] - kv.Value[1];
                DateTime ms = new DateTime(kv.Key / 100, kv.Key % 100, 1);
                months.Add(new { m = ms.ToString("MMM yyyy", CultureInfo.InvariantCulture), from = GlFmt.Iso(ms < from ? from : ms), to = GlFmt.Iso(ms.AddMonths(1).AddDays(-1) > to ? to : ms.AddMonths(1).AddDays(-1)), dr = GlFmt.Money(kv.Value[0]), cr = GlFmt.Money(kv.Value[1]), net = GlFmt.Money(kv.Value[0] - kv.Value[1]), bal = GlFmt.Money(run), n = GlFmt.Count(kv.Value[2]) });
            }
        }
        else
        {
            var prm = new List<object>();
            string w = GlReports.KeyWhere(a.Key, member, prm);
            var p2 = new List<object>(prm); p2.AddRange(new object[] { "@f", from, "@t", to });
            using (var c = GlDb.Read())
            {
                open = GlDb.L(GlDb.Scalar(c, null, "SELECT IFNULL(SUM(" + GlCalc.NetExpr + "),0) FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode WHERE " + w + " AND l.transactionDate < @f", p2.ToArray()));
                long run = open;
                foreach (DataRow r in GlDb.Table(c, null, "SELECT DATE_FORMAT(l.transactionDate,'%Y-%m') m, SUM(" + GlCalc.DrExpr + "), SUM(" + GlCalc.CrExpr + "), COUNT(*) FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode WHERE " + w +
                                                    " AND l.transactionDate BETWEEN @f AND @t GROUP BY m ORDER BY m", p2.ToArray()).Rows)
                {
                    long mdr = GlDb.L(r[1]), mcr = GlDb.L(r[2]); run += mdr - mcr; dr += mdr; cr += mcr; lines += GlDb.L(r[3]);
                    DateTime ms = DateTime.ParseExact(GlDb.S(r[0]) + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    months.Add(new { m = ms.ToString("MMM yyyy", CultureInfo.InvariantCulture), from = GlFmt.Iso(ms < from ? from : ms), to = GlFmt.Iso(ms.AddMonths(1).AddDays(-1) > to ? to : ms.AddMonths(1).AddDays(-1)), dr = GlFmt.Money(mdr), cr = GlFmt.Money(mcr), net = GlFmt.Money(mdr - mcr), bal = GlFmt.Money(run), n = GlFmt.Count(GlDb.L(r[3])) });
                }
                d["memberName"] = MemberName(c, a.Key, member);
            }
        }
        long close = open + dr - cr;
        d["summary"] = new { opening = GlFmt.Money(open), dr = GlFmt.Money(dr), cr = GlFmt.Money(cr), closing = GlFmt.Money(close), lines = GlFmt.Count(lines), side = close == 0 ? "" : close > 0 ? "debit" : "credit" };
        d["months"] = months;

        using (var c = GlDb.Read())
        {
            // Mapping for codes missing from the chart.
            DataRow mp = GlDb.Table(c, null, "SELECT account_name, category, IFNULL(subcategory,''), is_provisional, IFNULL(basis,''), IFNULL(confirmed_by,''), confirmed_at, created_by, created_at, IFNULL(updated_by,''), updated_at FROM gl_account_map WHERE accountcode=@c", "@c", a.Code).Rows.Cast<DataRow>().FirstOrDefault();
            if (mp != null)
                d["mapping"] = new { name = GlDb.S(mp[0]), category = GlDb.S(mp[1]), subcategory = GlDb.S(mp[2]), provisional = GlDb.I(mp[3]) == 1, basis = GlDb.S(mp[4]), confirmedBy = GlDb.S(mp[5]), confirmedAt = GlFmt.When(mp[6]), createdBy = GlDb.S(mp[7]), createdAt = GlFmt.When(mp[8]), updatedBy = GlDb.S(mp[9]), updatedAt = GlFmt.When(mp[10]) };
            d["categories"] = GlWrite.MapCategories;
            d["subcategories"] = s.Accounts.Values.Where(v => v.Kind == "CHART").Select(v => new { c = v.Category, s = v.SubCategory ?? "" }).Distinct().OrderBy(v => v.c).ThenBy(v => v.s).ToList();

            // Warnings that name this account.
            d["warnings"] = GlDb.Table(c, null,
                "SELECT id, rule_code, title, severity, status, amount FROM gl_warning WHERE (scope_key = @c OR scope_key = @k) AND status <> 'FIXED' ORDER BY FIELD(severity,'CRITICAL','HIGH','MEDIUM','INFO')", "@c", a.Code, "@k", a.Key).Rows.Cast<DataRow>()
                .Select(r => new { id = GlDb.L(r[0]), rule = GlDb.S(r[1]), title = GlDb.S(r[2]), severity = GlDb.S(r[3]), status = GlDb.S(r[4]), amount = GlFmt.Money(GlDb.M(r[5])) }).ToList();
        }
        return d;
    }

    private static string MemberName(MySql.Data.MySqlClient.MySqlConnection c, string key, string member)
    {
        if (key == "SUB:STUDENTS")
            return GlDb.S(GlDb.Scalar(c, null, "SELECT TRIM(CONCAT(IFNULL(firstname,''),' ',IFNULL(othername,''))) FROM campus_dynamics.acad_student WHERE regno=@r", "@r", member));
        if (key == "SUB:Supplier")
            return GlDb.S(GlDb.Scalar(c, null, "SELECT SupplierName FROM inv_supplierdetails WHERE TRIM(SupplierCode)=@r LIMIT 1", "@r", member));
        return "";
    }

    [WebMethod(EnableSession = true)]
    public static string LoadStatement(string code, string member, string from, string to, int page, int size, string search)
    {
        return GlApi.Read(GlAccess.Account, delegate
        {
            var p = new Dictionary<string, object> { { "account", code }, { "member", member ?? "" }, { "from", from }, { "to", to } };
            var x = GlReports.Ctx(p, page, size, -1, false, search, false);
            GlResult r = GlReports.Run("R02", x);
            return FaJson.Ser(GlReports.ToJson(r, x));
        });
    }

    [WebMethod(EnableSession = true)]
    public static string LoadMembers(string code, string to, int page, int size, int sort, bool desc, string search)
    {
        return GlApi.Read(GlAccess.Account, delegate
        {
            GlCalc.Snapshot s = GlCalc.Get(false);
            GlAcct a;
            if (!s.Accounts.TryGetValue(code ?? "", out a) || a.Kind != "SUBLEDGER") throw new GlRefusal("Only a subsidiary ledger has members.");
            DateTime asAt; if (!DateTime.TryParseExact(to ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out asAt)) asAt = DateTime.Today;
            var x = GlReports.Ctx(new Dictionary<string, object>(), page, size, sort, desc, search, false);
            var r = new GlResult { Noun = "member", NounPlural = "members", Basis = "Each code inside " + a.Name.ToLowerInvariant() + ", with its balance as at " + GlFmt.Date(asAt) + " (debit positive)." };
            r.Cols.Add(new GlCol("m", a.Key == "SUB:STUDENTS" ? "Registration number" : a.Key == "SUB:Supplier" ? "Supplier code" : "Code", "code", 90));
            r.Cols.Add(new GlCol("name", "Name", "text", 180));
            r.Cols.Add(new GlCol("n", "Lines", "count", 50).Summed());
            r.Cols.Add(new GlCol("last", "Last line", "date", 70));
            r.Cols.Add(new GlCol("dr", "Debit", "money", 85)); r.Cols.Add(new GlCol("cr", "Credit", "money", 85)); r.Cols.Add(new GlCol("bal", "Balance", "money", 90));
            var prm = new List<object>();
            string w = GlReports.KeyWhere(a.Key, null, prm);
            prm.AddRange(new object[] { "@t", asAt });
            using (var c = GlDb.Read())
            {
                var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (a.Key == "SUB:STUDENTS")
                    foreach (DataRow q in GlDb.Table(c, null, "SELECT regno, TRIM(CONCAT(IFNULL(firstname,''),' ',IFNULL(othername,''))) FROM campus_dynamics.acad_student").Rows) names[GlDb.S(q[0])] = GlDb.S(q[1]);
                else if (a.Key == "SUB:Supplier")
                    foreach (DataRow q in GlDb.Table(c, null, "SELECT TRIM(SupplierCode), SupplierName FROM inv_supplierdetails").Rows) names[GlDb.S(q[0])] = GlDb.S(q[1]);
                foreach (DataRow q in GlDb.Table(c, null, "SELECT l.accountcode, COUNT(*), MAX(l.transactionDate), SUM(" + GlCalc.DrExpr + "), SUM(" + GlCalc.CrExpr + ") FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode WHERE " + w +
                                                    " AND l.transactionDate <= @t GROUP BY l.accountcode", prm.ToArray()).Rows)
                {
                    string m = GlDb.S(q[0]); string nm; names.TryGetValue(m, out nm);
                    decimal mdr = GlDb.M(q[3]), mcr = GlDb.M(q[4]);
                    r.Add("AccountsAccount.aspx?code=" + Uri.EscapeDataString(a.Key) + "&m=" + Uri.EscapeDataString(m), "", m, nm ?? "", GlDb.L(q[1]), GlDb.D(q[2]), mdr, mcr, mdr - mcr);
                }
            }
            if (sort < 0) { x.SortCol = 6; x.Desc = true; }
            GlReports.Finish(r, x);
            return FaJson.Ser(GlReports.ToJson(r, x));
        });
    }

    [WebMethod(EnableSession = true)]
    public static string SaveMapping(string code, string name, string category, string subcategory, bool confirm, string reason)
    {
        return GlApi.Write(GlAccess.WarningsManage, delegate
        {
            GlWrite.MapAccount(code, name, category, subcategory, confirm, reason);
            return GlApi.Ok(new { message = confirm ? "Mapping confirmed." : "Mapping saved as provisional." });
        });
    }
}
