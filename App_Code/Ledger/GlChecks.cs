using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================
//  General Ledger: checks shown in the Checks panel of every report.
//  A check never changes anything. Each one says pass or fail, how much
//  and how many, the likely cause, and where to go to fix it.
//  The trial balance difference is decomposed so the causes add up to it.
// =====================================================================

public class GlFix
{
    public string kind;     // report | screen | wizard | ack
    public string label;
    public string target;   // report code, URL or warning code
    public GlFix(string kind, string label, string target) { this.kind = kind; this.label = label; this.target = target; }
}

public class GlCheck
{
    public string code, title, status, cause;   // status: pass | fail | info
    public decimal amount;
    public long count;
    public GlFix fix;
    public List<GlCheck> parts;                  // decomposition rows that add up to amount
    public string amountText { get { return GlFmt.Money(amount); } }
}

public static class GlChecks
{
    public static string BasisText(string basis)
    {
        return basis == "CHART"
            ? "Chart accounts only: general-ledger codes in the chart, plus codes missing from the chart that are provisionally mapped. Student, supplier and other subsidiary lines are left out."
            : "Whole ledger (fin_ledger only): each general-ledger account, plus each subsidiary ledger family (students, faculty fee lines, suppliers, staff advances, gratuity, sponsors) as one line. Fee tracking is never added.";
    }

    /// <summary>Checks for the trial balance closing at <paramref name="to"/> (everything up to and including that day).</summary>
    public static List<GlCheck> TrialBalance(GlCalc.Snapshot s, DateTime? from, DateTime to, string basis)
    {
        var l = new List<GlCheck>();
        GlCalc.Totals closing = GlCalc.Total(s, null, to, basis);
        l.Add(Difference("TB_CLOSING", "Closing balances: total debit equals total credit", null, to, basis, closing.Diff));
        if (from.HasValue)
        {
            GlCalc.Totals mv = GlCalc.Total(s, from, to, basis);
            l.Add(Difference("TB_MOVEMENT", "Movement in the range: debit equals credit", from, to, basis, mv.Diff));
        }

        // Codes used but missing from the chart.
        var unm = s.Accounts.Values.Where(a => a.Kind == "UNMAPPED").ToList();
        var unmKeys = new HashSet<string>(unm.Select(a => a.Key), StringComparer.OrdinalIgnoreCase);
        long unmLines = s.Agg.Where(g => g.Day <= GlCalc.DayInt(to) && unmKeys.Contains(g.Key)).Sum(g => (long)g.N);
        l.Add(new GlCheck
        {
            code = "UNMAPPED", title = "Every code used is in the chart or mapped", status = unmLines == 0 ? "pass" : "fail",
            count = unmLines, amount = unm.Sum(a => Math.Abs(GlCalc.Balance(s, a.Key, to))),
            cause = unmLines == 0 ? "" : "Lines carry codes that are neither in fin_subaccounts nor in the provisional map. They are shown as Unclassified.",
            fix = new GlFix("report", "Chart of accounts report", "R20")
        });
        var prov = s.Accounts.Values.Where(a => a.Kind == "PROVISIONAL").ToList();
        var provKeys = new HashSet<string>(prov.Select(a => a.Key), StringComparer.OrdinalIgnoreCase);
        var usedKeys = new HashSet<string>(s.Agg.Where(g => g.Day <= GlCalc.DayInt(to) && provKeys.Contains(g.Key)).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
        long provLines = s.Agg.Where(g => g.Day <= GlCalc.DayInt(to) && provKeys.Contains(g.Key)).Sum(g => (long)g.N);
        if (provLines > 0)
            l.Add(new GlCheck
            {
                code = "PROVISIONAL", title = "Codes missing from the chart are shown with a provisional mapping", status = "info",
                count = usedKeys.Count, amount = prov.Sum(a => Math.Abs(GlCalc.Balance(s, a.Key, to))),
                cause = GlFmt.Plural(provLines, "line uses", "lines use") + " codes that the chart migration removed (for example AC6007 Functional Fees income). They are classified for presentation only until the Bursar confirms them.",
                fix = new GlFix("report", "Review mappings", "R20")
            });

        // Plug and suspense balances.
        foreach (GlAcct a in s.Accounts.Values.Where(IsSuspense))
        {
            long b = GlCalc.Balance(s, a.Key, to);
            if (b == 0) continue;
            l.Add(new GlCheck
            {
                code = "PLUG:" + a.Code, title = "Plug account " + a.Code + " carries a balance", status = "fail", amount = b, count = 1,
                cause = "A repair script posted differences here instead of correcting the vouchers. The balance hides real errors.",
                fix = new GlFix("screen", "Open the account", "AccountsAccount.aspx?code=" + Uri.EscapeDataString(a.Code))
            });
        }
        return l;
    }

    public static bool IsSuspense(GlAcct a)
    {
        string c = (a.Code ?? "").ToUpperInvariant(), n = (a.Name ?? "").ToUpperInvariant();
        return a.Category == "Suspense" || c.Contains("RECONCILE") || n.Contains("SUSPENSE") || n.Contains("RECONCILIATION DIFF");
    }

    /// <summary>A difference check with its decomposition by voucher pattern; the parts add up to the difference exactly.</summary>
    public static GlCheck Difference(string code, string title, DateTime? from, DateTime to, string basis, long diff)
    {
        var c = new GlCheck { code = code, title = title, amount = diff, status = diff == 0 ? "pass" : "fail", parts = new List<GlCheck>() };
        if (diff == 0) return c;
        long nvd; int nvl;
        List<GlVoucherIssue> v = GlCalc.UnbalancedVouchers(from, to, basis, out nvd, out nvl);
        c.count = v.Count;
        foreach (var g in v.GroupBy(x => x.Pattern).OrderByDescending(g => Math.Abs(g.Sum(x => x.Diff))))
            c.parts.Add(new GlCheck
            {
                code = g.Key, title = GlCalc.PatternText(g.Key), status = "fail", count = g.Count(), amount = g.Sum(x => x.Diff),
                cause = GlCalc.PatternCause(g.Key), fix = new GlFix("report", "List the vouchers", "R18")
            });
        if (nvd != 0)
            c.parts.Add(new GlCheck { code = "NO_VOUCHER", title = GlCalc.PatternText("NO_VOUCHER"), status = "fail", count = nvl, amount = nvd, cause = GlCalc.PatternCause("NO_VOUCHER"), fix = new GlFix("report", "List the vouchers", "R18") });
        decimal sum = c.parts.Sum(p => p.amount);
        c.cause = sum == diff
            ? "The difference is fully explained by the " + GlFmt.Plural(c.count, "voucher", "vouchers") + " that do not balance" + (nvd != 0 ? " and the lines with no voucher number" : "") + ". The parts below add up to it exactly."
            : "The parts below add up to " + GlFmt.Money(sum) + ", not " + GlFmt.Money(diff) + ". Tell MIS: the analysis has missed a pattern.";
        c.fix = new GlFix("report", "Unbalanced vouchers report", "R18");
        return c;
    }
}
