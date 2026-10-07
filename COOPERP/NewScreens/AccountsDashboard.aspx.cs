using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

// General Ledger: Accounts Dashboard (plan section 3). Every figure comes from GlCalc, the same
// calculation the reports use, and every figure opens the report it came from.
public partial class COOPERP_NewScreens_AccountsDashboard : System.Web.UI.Page
{
    protected string BootJson = "{}";

    protected void Page_Load(object sender, EventArgs e)
    {
        RoleAccessService.RequireSlug(this, GlAccess.Dashboard);
        var d = new Dictionary<string, object>();
        try
        {
            GlWarnings.EnsureFresh(GlAccess.User());
            d = Build();
        }
        catch (Exception ex)
        {
            GlLog.Error("AccountsDashboard.Load", ex);
            d["error"] = ex is GlRefusal ? ex.Message : "The dashboard could not be loaded. Reload the page, and tell MIS if it keeps happening.";
        }
        d["rights"] = GlAccess.RightsJson();
        BootJson = FaJson.Ser(d).Replace("<", "\\u003c");
    }

    private static string R(string code, params string[] kv) { return GlReports.ReportLink(code, kv); }

    private static Dictionary<string, object> Build()
    {
        var d = new Dictionary<string, object>();
        GlCalc.Snapshot s = GlCalc.Get(false);
        DateTime today = DateTime.Today;
        using (var c = GlDb.Read())
        {
            var years = GlCalc.Years(c);
            GlCalc.FinYear y = GlCalc.YearOf(years, today) ?? years.LastOrDefault();
            DateTime from = y != null ? y.Start : new DateTime(today.Year, 1, 1);
            string f = GlFmt.Iso(from), t = GlFmt.Iso(today);
            d["period"] = new { label = y != null ? y.Label : "", from = GlFmt.Date(from), to = GlFmt.Date(today) };
            d["built"] = GlFmt.When(s.BuiltAt);

            // Cash and bank: the cash book's closing balances.
            var cash = GlCalc.CashAccounts(s);
            var banks = cash.Select(a => new { code = a.Code, name = a.Name, bal = GlCalc.Balance(s, a.Key, today) }).Where(b => b.bal != 0).OrderByDescending(b => b.bal).ToList();
            long cashTotal = cash.Sum(a => GlCalc.Balance(s, a.Key, today));

            // Income and expenditure this year: R04 with the same dates.
            var ie = GlCalc.IncomeExpenditure(s, from, today);
            // IncomeExpenditure already returns income and expenditure as positive figures.
            long income = ie.Where(kv => kv.Key.Category == "Income").Sum(kv => kv.Value), expense = ie.Where(kv => kv.Key.Category == "Expense").Sum(kv => kv.Value);

            // Receivables: the canonical figure (R11), with the ledger shown beside it, never added.
            decimal canon = -GlDb.M(GlDb.Scalar(c, null, "SELECT IFNULL(SUM(total_balance),0) FROM fin_student_balance_cache"));
            long ledgerStudents = GlCalc.Balance(s, "SUB:STUDENTS", today);

            // Payables: supplier lines (R10) and the control account.
            long supplier = -GlCalc.Balance(s, "SUB:Supplier", today);
            GlCalc.ControlPair sp = GlCalc.ControlPairs(s).FirstOrDefault(p => p.Sub.Key == "SUB:Supplier");
            long control = sp == null ? 0 : -GlCalc.Balance(s, sp.Control.Key, today);

            GlCalc.Totals tb = GlCalc.Total(s, null, today, "WHOLE");
            GlWarnings.Summary ws = GlWarnings.GetSummary(c);

            d["tiles"] = new object[]
            {
                new { label = "Cash and bank", value = GlFmt.Money(cashTotal), sub = GlFmt.Plural(banks.Count, "account", "accounts") + " with a balance", link = R("R06", "from", f, "to", t), warn = false },
                new { label = "Owed by students", value = GlFmt.Money(canon), sub = "Canonical balance. Ledger student lines: " + GlFmt.Money(ledgerStudents), link = R("R11"), warn = Math.Abs(canon - ledgerStudents) > 1000 },
                new { label = "Owed to suppliers", value = GlFmt.Money(supplier), sub = sp == null ? "Supplier lines" : "Supplier lines. " + sp.Control.Code + " shows " + GlFmt.Money(control), link = R("R10"), warn = supplier != control },
                new { label = "Income this year", value = GlFmt.Money(income), sub = (y != null ? y.Label + ", " : "") + "to " + GlFmt.Date(today), link = R("R04", "from", f, "to", t), warn = false },
                new { label = "Expenditure this year", value = GlFmt.Money(expense), sub = (y != null ? y.Label + ", " : "") + "to " + GlFmt.Date(today), link = R("R04", "from", f, "to", t), warn = false },
                new { label = income - expense >= 0 ? "Surplus this year" : "Deficit this year", value = GlFmt.Money(income - expense), sub = "Income less expenditure", link = R("R04", "from", f, "to", t), warn = false },
                new { label = "Trial balance difference", value = GlFmt.Money(tb.Diff), sub = tb.Diff == 0 ? "Debit equals credit" : "Explained by unbalanced vouchers", link = R("R01", "to", t), warn = tb.Diff != 0 },
                new { label = "Open critical warnings", value = ws.Critical.ToString(CultureInfo.InvariantCulture), sub = ws.Open + " open in all, health " + (ws.Health < 0 ? "not measured" : ws.Health + " of 100"), link = "AccountsWarnings.aspx", warn = ws.Critical > 0 }
            };
            d["health"] = ws.Health;
            d["banks"] = banks.Select(b => new { b.code, b.name, bal = GlFmt.Money(b.bal), raw = b.bal, link = "AccountsAccount.aspx?code=" + Uri.EscapeDataString(b.code) }).ToList();

            // Monthly income and expenditure for the year.
            var months = new List<object>();
            if (y != null)
                for (DateTime m = new DateTime(from.Year, from.Month, 1); m <= today; m = m.AddMonths(1))
                {
                    DateTime a = m < from ? from : m, b = m.AddMonths(1).AddDays(-1) > today ? today : m.AddMonths(1).AddDays(-1);
                    var mv = GlCalc.IncomeExpenditure(s, a, b);
                    long mi = mv.Where(kv => kv.Key.Category == "Income").Sum(kv => kv.Value), me = mv.Where(kv => kv.Key.Category == "Expense").Sum(kv => kv.Value);
                    months.Add(new { m = m.ToString("MMM", CultureInfo.InvariantCulture), label = m.ToString("MMM yyyy", CultureInfo.InvariantCulture), income = mi, expense = me, incomeText = GlFmt.Money(mi), expenseText = GlFmt.Money(me), link = R("R04", "from", GlFmt.Iso(a), "to", GlFmt.Iso(b)) });
                }
            d["months"] = months;

            // Expenditure by sub-category: R09.
            var bySub = ie.Where(kv => kv.Key.Category == "Expense").GroupBy(kv => string.IsNullOrEmpty(kv.Key.SubCategory) ? "(none)" : kv.Key.SubCategory)
                .Select(g => new { name = g.Key, amount = g.Sum(kv => kv.Value) }).OrderByDescending(g => g.amount).ToList();
            d["spend"] = bySub.Select(g => new { g.name, amount = GlFmt.Money(g.amount), share = expense == 0 ? 0 : Math.Round(g.amount * 100.0 / expense, 1), link = R("R09", "from", f, "to", t, "by", "subcategory") }).ToList();
            d["topExpense"] = ie.Where(kv => kv.Key.Category == "Expense").OrderByDescending(kv => kv.Value).Take(8)
                .Select(kv => new { code = kv.Key.Code, name = kv.Key.Name, amount = GlFmt.Money(kv.Value), link = "AccountsAccount.aspx?code=" + Uri.EscapeDataString(kv.Key.Key) + "&from=" + f + "&to=" + t }).ToList();

            // Warning trend and the worst open warnings.
            d["trend"] = GlDb.Table(c, null, "SELECT open_total, health_score FROM (SELECT id, open_total, health_score FROM gl_warning_run WHERE finished_at IS NOT NULL ORDER BY id DESC LIMIT 30) z ORDER BY id").Rows.Cast<DataRow>()
                .Select(r => new { open = GlDb.I(r[0]), health = GlDb.I(r[1]) }).ToList();
            d["warnings"] = GlDb.Table(c, null, "SELECT id, rule_code, title, severity, amount FROM gl_warning WHERE status IN ('OPEN','REAPPEARED') ORDER BY FIELD(severity,'CRITICAL','HIGH','MEDIUM','INFO'), amount DESC LIMIT 6").Rows.Cast<DataRow>()
                .Select(r => new { id = GlDb.L(r[0]), rule = GlDb.S(r[1]), title = GlDb.S(r[2]), severity = GlDb.S(r[3]), amount = GlFmt.Money(GlDb.M(r[4])) }).ToList();
            d["lastRun"] = ws.LastRun.HasValue ? GlFmt.When(ws.LastRun.Value) : "";
        }
        return d;
    }
}
