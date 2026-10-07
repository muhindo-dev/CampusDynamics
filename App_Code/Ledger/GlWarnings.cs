using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;

// =====================================================================
//  Finance Warnings: detection rules W01-W22 (plan section 5).
//
//  Detection is SELECT-only on the read-only connection. The only
//  writes are to the module's own tables (gl_warning, gl_warning_event,
//  gl_warning_run, gl_warning_trend, gl_audit), never to finance data.
//
//  Status:  OPEN -> ACKNOWLEDGED (with a reason) -> FIXED (no longer found)
//           FIXED -> REAPPEARED (found again)
//           ACKNOWLEDGED -> REAPPEARED (grew beyond what was acknowledged)
//  Health = 100 - (15 x critical + 6 x high + 2 x medium), counted per
//  rule (its worst open scope), floor 0. Acknowledged warnings do not count.
// =====================================================================

public class GlRec
{
    public string key, date, reference, account, text, link;
    public decimal amount;
    public decimal? amount2;
    public long n;
}

public class GlFinding
{
    public string Rule, Scope = "", Title, Severity, Cause, Where;
    public long Count;
    public decimal Amount;
    public List<GlRec> Records;          // full list when it is small enough to keep in memory; else null
    public List<GlRec> Sample;           // what is stored with the warning (top 20)
    public bool RecordAck;               // records can be accepted one by one
}

public class GlRule
{
    public string Code, Title, Severity, Detection, Cause;
    public List<GlFix> Fixes = new List<GlFix>();
    public bool RecordAck;
    public Func<GlWarnCtx, List<GlFinding>> Detect;
}

public class GlWarnCtx
{
    public GlCalc.Snapshot S;
    public MySqlConnection C;
    public List<GlCalc.FinYear> Years;
    public Dictionary<string, HashSet<string>> Acks = new Dictionary<string, HashSet<string>>();
    public DateTime End;                              // the last day with postings (or today)
    public Dictionary<string, object> Shared = new Dictionary<string, object>();
    public HashSet<string> Acked(string rule)
    {
        HashSet<string> h;
        return Acks.TryGetValue(rule, out h) ? h : new HashSet<string>();
    }
}

public static class GlWarnings
{
    public const string Critical = "CRITICAL", High = "HIGH", Medium = "MEDIUM", Info = "INFO";
    private static int _running;

    public static string SeverityText(string s)
    {
        switch (s) { case Critical: return "Critical"; case High: return "High"; case Medium: return "Medium"; default: return "Information"; }
    }

    public static int Weight(string s) { return s == Critical ? 15 : s == High ? 6 : s == Medium ? 2 : 0; }

    private static int SevRank(string s) { return s == Critical ? 4 : s == High ? 3 : s == Medium ? 2 : 1; }

    // ── Rule catalogue ──────────────────────────────────────────────

    private static List<GlRule> _rules;
    public static List<GlRule> Rules { get { if (_rules == null) _rules = BuildRules(); return _rules; } }
    public static GlRule Rule(string code) { return Rules.FirstOrDefault(r => r.Code == code); }

    private static GlRule R(string code, string sev, string title, string detection, string cause, bool recordAck, Func<GlWarnCtx, List<GlFinding>> f, params GlFix[] fixes)
    {
        return new GlRule { Code = code, Severity = sev, Title = title, Detection = detection, Cause = cause, RecordAck = recordAck, Detect = f, Fixes = fixes.ToList() };
    }

    private static List<GlRule> BuildRules()
    {
        return new List<GlRule>
        {
            R("W01", Critical, "Unbalanced vouchers", "Vouchers whose debit and credit lines differ.",
              "A posting wrote one side only, or the voucher number was reused for unrelated lines.", true, W01,
              new GlFix("report", "List unbalanced vouchers", "R18"), new GlFix("wizard", "Start an adjusting entry", "AccountsAdjustments.aspx?new=1")),
            R("W02", Critical, "Trial balance does not balance", "Total debit minus total credit of the whole ledger is not zero.",
              "Explained in full by the unbalanced vouchers (W01) and lines with no voucher number.", false, W02,
              new GlFix("report", "Trial balance with cause analysis", "R01")),
            R("W03", Critical, "Codes used but missing from the chart", "Lines whose code is not in the chart of accounts.",
              "The chart migration removed codes that postings still use.", false, W03,
              new GlFix("report", "Chart of accounts report", "R20")),
            R("W04", Medium, "Lines with no voucher number or a zero amount", "voucherNo empty or 0; amount 0.",
              "Imports that were never numbered, or placeholder lines.", false, W04,
              new GlFix("report", "Journal and voucher listing", "R03")),
            R("W05", High, "Voucher numbers reused", "One voucher number used on more than one date or by more than one source.",
              "At least four number sequences write into the same voucher number column.", true, W05,
              new GlFix("report", "Journal and voucher listing", "R03")),
            R("W06", High, "Year closed without a roll forward", "A year marked Closed whose income and expense were never closed to retained earnings.",
              "The year was closed by setting a flag; no closing entries were posted.", false, W06,
              new GlFix("screen", "Periods and close", "AccountsPeriods.aspx")),
            R("W07", High, "Lines outside any financial year", "Lines dated outside every range in the financial year table.",
              "The year table does not cover the oldest postings.", false, W07,
              new GlFix("report", "Period close summary", "R14")),
            R("W08", High, "Postings into a closed or signed-off period", "Lines recorded more than the grace days after their year ended, or after their month was signed off.",
              "Late imports, migrations and repairs dated into old periods.", false, W08,
              new GlFix("report", "Audit trail", "R16")),
            R("W09", Medium, "Possible duplicate postings", "Lines identical in account, side, amount, date and particulars.",
              "A repeated import or a double entry. Some repeats are genuine.", true, W09,
              new GlFix("report", "Duplicate postings report", "R19"), new GlFix("wizard", "Start an adjusting entry", "AccountsAdjustments.aspx?new=1")),
            R("W10", Critical, "Student receivables disagree between sources", "The canonical student balance against the ledger student lines, student by student.",
              "Two fee stores, missing ledger mirrors and no receivables control account.", false, W10,
              new GlFix("report", "Control account reconciliations", "R17"), new GlFix("report", "Receivables ageing", "R11")),
            R("W11", High, "Control account differs from its subsidiary ledger", "Trade payables against supplier lines; salary advances against advance lines.",
              "Subsidiary postings skip the control account.", false, W11,
              new GlFix("report", "Control account reconciliations", "R17")),
            R("W12", Medium, "Requisitions not carried through to payment", "Approved but unpaid; or marked posted with no ledger lines.",
              "The requisition workflow never posts to the ledger.", false, W12,
              new GlFix("report", "Requisition to payment trace", "R12")),
            R("W13", Info, "Payments without a requisition", "Bank or cash credits in vouchers that debit an expense, with no requisition referring to them.",
              "Spending is paid through classic journals and vouchers; requisitions are not used.", false, W13,
              new GlFix("report", "Payment voucher register", "R13")),
            R("W14", Medium, "Balance on the unexpected side", "Asset or expense with a credit balance; liability, equity or income with a debit balance. Contra accounts excluded.",
              "Misposting, a missing entry, or a closing entry never made.", false, W14,
              new GlFix("report", "Account statement", "R02")),
            R("W15", High, "Students billed twice for the same item and term", "More than one bill for the same student, item, year and semester in fee tracking.",
              "Double billing by a repeated run or a manual bill.", true, W15,
              new GlFix("report", "Receivables ageing", "R11")),
            R("W16", Info, "Backup and repair tables in the live finance schema", "Tables whose names mark them as copies or backups.",
              "Repairs left copies behind. They slow backups and confuse reporting.", false, W16),
            R("W17", Medium, "Expense accounts with no budget", "Expense accounts with movement in the current year and no budget line.",
              "Budgets have not been loaded for the year.", false, W17,
              new GlFix("report", "Budget against actual", "R08")),
            R("W18", Medium, "Large or unusual postings", "A line above its account's mean plus 4 standard deviations, and above the large-posting floor.",
              "A keying error, or a genuine large item that should be reviewed.", true, W18,
              new GlFix("report", "General ledger", "R02")),
            R("W19", High, "Ledger lines edited or deleted after posting", "Rows in edit_ledger and fin_deleted_ledger.",
              "Direct database maintenance and repair scripts.", false, W19,
              new GlFix("report", "Audit trail", "R16")),
            R("W20", High, "Plug or suspense accounts carrying a balance", "AC-RECONCILE-DIFF and accounts named suspense or difference.",
              "Repair scripts parked differences instead of correcting the vouchers.", false, W20,
              new GlFix("report", "Account statement", "R02"), new GlFix("wizard", "Start an adjusting entry", "AccountsAdjustments.aspx?new=1")),
            R("W21", Medium, "Financial year table inconsistent", "A label that does not match its dates; overlapping or missing ranges; an open year already ended.",
              "Manual edits to the year table.", false, W21,
              new GlFix("screen", "Periods and close", "AccountsPeriods.aspx")),
            R("W22", Medium, "Journals pending for too long", "Journals in Pending status for more than the set number of days.",
              "Approval was never done, or the journal was abandoned.", false, W22,
              new GlFix("screen", "Classic journal entries", "JournalEntries.aspx")),
        };
    }

    // ── Small helpers for rules ─────────────────────────────────────

    private static GlFinding F(GlWarnCtx x, string rule, string scope, string severity, string title, long count, decimal amount, string cause, string where, List<GlRec> recs)
    {
        GlRule r = Rule(rule);
        var f = new GlFinding { Rule = rule, Scope = scope ?? "", Severity = severity ?? r.Severity, Title = title, Count = count, Amount = amount, Cause = cause, Where = where, Records = recs, RecordAck = r.RecordAck };
        if (recs != null) f.Sample = recs.OrderByDescending(a => Math.Abs(a.amount)).Take(20).ToList();
        return f;
    }

    private static List<GlFinding> One(GlFinding f) { return f == null ? new List<GlFinding>() : new List<GlFinding> { f }; }
    private static List<GlFinding> None() { return new List<GlFinding>(); }

    private static string CategoryArticle(string c)
    {
        switch (c) { case "Assets": return "An asset"; case "Liabilities": return "A liability"; case "Equity": return "An equity"; case "Income": return "An income"; case "Expense": return "An expense"; default: return "This"; }
    }

    private static string AccountLink(string code) { return "AccountsAccount.aspx?code=" + Uri.EscapeDataString(code ?? ""); }
    private static string VoucherLink(long v) { return "AccountsVoucher.aspx?v=" + v.ToString(CultureInfo.InvariantCulture); }
    private static string B(decimal v) { return GlFmt.Money(v); }

    private static List<GlVoucherIssue> Unbalanced(GlWarnCtx x)
    {
        object o;
        if (x.Shared.TryGetValue("ub", out o)) return (List<GlVoucherIssue>)o;
        long nvd; int nvl;
        var l = GlCalc.UnbalancedVouchers(null, x.End, "WHOLE", out nvd, out nvl);
        x.Shared["ub"] = l; x.Shared["nvd"] = nvd; x.Shared["nvl"] = nvl;
        return l;
    }

    // ── Rules ───────────────────────────────────────────────────────

    private static List<GlFinding> W01(GlWarnCtx x)
    {
        var acked = x.Acked("W01");
        var l = Unbalanced(x).Where(v => !acked.Contains(v.VoucherNo.ToString(CultureInfo.InvariantCulture))).ToList();
        if (l.Count == 0) return None();
        var recs = l.Select(v => new GlRec
        {
            key = v.VoucherNo.ToString(CultureInfo.InvariantCulture), date = GlFmt.Iso(v.First), reference = "Voucher " + v.VoucherNo,
            account = GlCalc.PatternText(v.Pattern), text = v.Source + ", " + GlFmt.Plural(v.Lines, "line", "lines"), amount = v.Diff, link = VoucherLink(v.VoucherNo)
        }).ToList();
        var parts = l.GroupBy(v => v.Pattern).OrderByDescending(g => g.Count())
            .Select(g => GlCalc.PatternText(g.Key).Split('(')[0].Trim() + ": " + GlFmt.Count(g.Count()) + " (net " + B(g.Sum(v => v.Diff)) + ")");
        return One(F(x, "W01", "", null, GlFmt.Plural(l.Count, "voucher does", "vouchers do") + " not balance",
            l.Count, l.Sum(v => Math.Abs(v.Diff)), string.Join("; ", parts) + ". Amount is the sum of each voucher's difference, ignoring sign.", "Whole ledger, all dates", recs));
    }

    private static List<GlFinding> W02(GlWarnCtx x)
    {
        GlCalc.Totals t = GlCalc.Total(x.S, null, x.End, "WHOLE");
        if (t.Diff == 0) return None();
        var recs = new List<GlRec>();
        foreach (GlCalc.FinYear y in x.Years)
        {
            GlCalc.Totals ty = GlCalc.Total(x.S, y.Start, y.End, "WHOLE");
            recs.Add(new GlRec { key = y.Label, date = GlFmt.Iso(y.Start), reference = y.Label, account = y.DateLabel, text = "Debit " + B(ty.Dr) + ", credit " + B(ty.Cr), amount = ty.Diff, link = "AccountsReports.aspx?r=R01&from=" + GlFmt.Iso(y.Start) + "&to=" + GlFmt.Iso(y.End) });
        }
        if (x.Years.Count > 0)
        {
            GlCalc.Totals pre = GlCalc.Total(x.S, null, x.Years[0].Start.AddDays(-1), "WHOLE");
            if (pre.Dr != 0 || pre.Cr != 0) recs.Insert(0, new GlRec { key = "before", reference = "Before " + GlFmt.Date(x.Years[0].Start), account = "No financial year", text = "Debit " + B(pre.Dr) + ", credit " + B(pre.Cr), amount = pre.Diff });
        }
        int n = Unbalanced(x).Count;
        long nvd = (long)x.Shared["nvd"];
        return One(F(x, "W02", "", null, "The trial balance is out by " + B(t.Diff), n, Math.Abs(t.Diff),
            "Debit " + B(t.Dr) + " against credit " + B(t.Cr) + ". The difference equals the net of " + GlFmt.Plural(n, "unbalanced voucher", "unbalanced vouchers") + (nvd != 0 ? " plus " + B(nvd) + " on lines with no voucher number" : "") + ".",
            "Whole ledger, all dates", recs));
    }

    private static List<GlFinding> W03(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        var used = x.S.Agg.GroupBy(g => g.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => new { n = g.Sum(a => (long)a.N), b = g.Sum(a => a.Dr - a.Cr) }, StringComparer.OrdinalIgnoreCase);
        foreach (string kind in new[] { "UNMAPPED", "PROVISIONAL" })
        {
            var accts = x.S.Accounts.Values.Where(a => a.Kind == kind && used.ContainsKey(a.Key)).ToList();
            if (accts.Count == 0) continue;
            if (kind == "PROVISIONAL")
            {
                var confirmed = new HashSet<string>(GlDb.Table(x.C, null, "SELECT accountcode FROM gl_account_map WHERE is_provisional = 0").Rows.Cast<DataRow>().Select(r => GlDb.S(r[0])), StringComparer.OrdinalIgnoreCase);
                accts = accts.Where(a => !confirmed.Contains(a.Code)).ToList();
                if (accts.Count == 0) continue;
            }
            var recs = accts.Select(a => new GlRec { key = a.Code, reference = a.Code, account = a.Name, text = a.Category + ", " + GlFmt.Plural(used[a.Key].n, "line", "lines"), amount = used[a.Key].b, link = AccountLink(a.Code) }).ToList();
            long lines = accts.Sum(a => used[a.Key].n);
            res.Add(kind == "UNMAPPED"
                ? F(x, "W03", "UNMAPPED", Critical, GlFmt.Plural(accts.Count, "code is", "codes are") + " used but neither in the chart nor mapped", lines, recs.Sum(r => Math.Abs(r.amount)),
                    GlFmt.Plural(lines, "line", "lines") + " are shown as Unclassified in every statement until the code is mapped.", "Chart of accounts", recs)
                : F(x, "W03", "PROVISIONAL", High, GlFmt.Plural(accts.Count, "code missing", "codes missing") + " from the chart, mapped provisionally and not yet confirmed", lines, recs.Sum(r => Math.Abs(r.amount)),
                    "The chart migration removed codes still in use (for example AC6007 Functional Fees income). Statements show them with a suggested category; the Bursar confirms each one (open question Q2).", "Chart of accounts", recs));
        }
        return res;
    }

    private static List<GlFinding> W04(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        foreach (var k in new[] { "NO_VOUCHER", "ZERO" })
        {
            string where = k == "NO_VOUCHER" ? "(voucherNo IS NULL OR voucherNo = 0)" : "transaction_amount = 0";
            var recs = GlDb.Table(x.C, null, "SELECT TID, transactionDate, accountcode, particulars, " + GlCalc.NetBare + ", IFNULL(voucherNo,0) FROM fin_ledger WHERE " + where + " ORDER BY TID LIMIT 5000")
                .Rows.Cast<DataRow>().Select(r => new GlRec { key = GlDb.S(r[0]), date = GlFmt.Iso(r[1]), reference = "Line " + GlDb.S(r[0]), account = GlDb.S(r[2]), text = GlDb.S(r[3]), amount = GlDb.M(r[4]), link = GlDb.L(r[5]) > 0 ? VoucherLink(GlDb.L(r[5])) : AccountLink(GlDb.S(r[2])) }).ToList();
            if (recs.Count == 0) continue;
            res.Add(k == "NO_VOUCHER"
                ? F(x, "W04", k, null, GlFmt.Plural(recs.Count, "line has", "lines have") + " no voucher number", recs.Count, recs.Sum(r => Math.Abs(r.amount)), "Lines imported without a number cannot be traced to a source document.", "fin_ledger", recs)
                : F(x, "W04", k, Info, GlFmt.Plural(recs.Count, "line has", "lines have") + " a zero amount", recs.Count, 0, "Zero lines change nothing but clutter statements.", "fin_ledger", recs));
        }
        return res;
    }

    private static List<GlFinding> W05(GlWarnCtx x)
    {
        var acked = x.Acked("W05");
        var recs = new List<GlRec>();
        using (var cmd = GlDb.Cmd(x.C, null,
            "SELECT voucherNo, MIN(transactionDate), COUNT(DISTINCT transactionDate), COUNT(DISTINCT IFNULL(source_system,'')), GROUP_CONCAT(DISTINCT IFNULL(source_system,'(none)') SEPARATOR ', '), " +
            "SUM(IF(transactionType='DR',transaction_amount,0)), COUNT(*) FROM fin_ledger WHERE voucherNo > 0 GROUP BY voucherNo " +
            "HAVING COUNT(DISTINCT transactionDate) > 1 OR COUNT(DISTINCT IFNULL(source_system,'')) > 1"))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                string k = Convert.ToString(r[0], CultureInfo.InvariantCulture);
                if (acked.Contains(k)) continue;
                recs.Add(new GlRec { key = k, date = GlFmt.Iso(r[1]), reference = "Voucher " + k, account = GlFmt.Plural(Convert.ToInt64(r[2]), "date", "dates") + ", " + GlFmt.Plural(Convert.ToInt64(r[3]), "source", "sources"), text = Convert.ToString(r[4]) + ", " + GlFmt.Plural(Convert.ToInt64(r[6]), "line", "lines"), amount = Convert.ToDecimal(r[5]), link = VoucherLink(Convert.ToInt64(r[0])) });
            }
        if (recs.Count == 0) return None();
        return One(F(x, "W05", "", null, GlFmt.Plural(recs.Count, "voucher number is", "voucher numbers are") + " shared by unrelated postings", recs.Count, recs.Sum(a => a.amount),
            "Amount is the debit total of the affected vouchers. A shared number makes a voucher look unbalanced or balanced by accident, and breaks the audit trail.", "Whole ledger", recs));
    }

    private static List<GlFinding> W06(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        foreach (GlCalc.FinYear y in x.Years.Where(y => y.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase)))
        {
            int e = GlCalc.DayInt(y.End);
            long ie = 0;
            foreach (GlAgg g in x.S.Agg) if (g.Day <= e) { GlAcct a = x.S.Accounts[g.Key]; if (a.Category == "Income" || a.Category == "Expense") ie += g.Dr - g.Cr; }
            if (ie == 0) continue;
            res.Add(F(x, "W06", y.Label, null, y.Label + " is marked Closed, but its income and expenditure were never closed to retained earnings", 1, Math.Abs(ie),
                "Income and expense accounts still hold a net " + (ie < 0 ? "surplus of " + B(-ie) : "deficit of " + B(ie)) + " at " + GlFmt.Date(y.End) + ". Closing would move it to retained earnings with an adjusting entry; Periods and close prepares it as a draft.",
                y.DateLabel, null));
        }
        return res;
    }

    private static List<GlFinding> W07(GlWarnCtx x)
    {
        var recs = new List<GlRec>();
        foreach (DataRow r in GlDb.Table(x.C, null,
            "SELECT DATE_FORMAT(l.transactionDate,'%Y-%m') m, COUNT(*), SUM(l.transaction_amount), SUM(" + GlCalc.NetExpr + ") FROM fin_ledger l " +
            "WHERE NOT EXISTS (SELECT 1 FROM fin_financial_years y WHERE l.transactionDate BETWEEN y.start_date AND y.end_date) GROUP BY m ORDER BY m").Rows)
            recs.Add(new GlRec { key = GlDb.S(r[0]), date = GlDb.S(r[0]) + "-01", reference = GlDb.S(r[0]), account = GlFmt.Plural(GlDb.L(r[1]), "line", "lines"), text = "Net " + B(GlDb.M(r[3])), amount = GlDb.M(r[2]), n = GlDb.L(r[1]), link = "AccountsReports.aspx?r=R03&from=" + GlDb.S(r[0]) + "-01" });
        if (recs.Count == 0) return None();
        long n = recs.Sum(a => a.n);
        return One(F(x, "W07", "", null, GlFmt.Plural(n, "line falls", "lines fall") + " outside every financial year", n, recs.Sum(a => a.amount),
            "Amount is the total of the lines' amounts. Year reports cannot include them until the year table covers their dates (open question Q1).", "fin_financial_years", recs));
    }

    private static List<GlFinding> W08(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        int grace = GlSettings.Int("late_posting_grace_days", 30);
        foreach (GlCalc.FinYear y in x.Years)
        {
            if (DateTime.Today <= y.End.AddDays(grace)) continue;
            var recs = GlDb.Table(x.C, null,
                "SELECT DATE(timeLog) d, COUNT(*), SUM(transaction_amount), GROUP_CONCAT(DISTINCT IFNULL(source_system,'(none)') SEPARATOR ', '), GROUP_CONCAT(DISTINCT teller SEPARATOR ', ') " +
                "FROM fin_ledger WHERE transactionDate BETWEEN @a AND @b AND timeLog > @late GROUP BY d ORDER BY d",
                "@a", y.Start, "@b", y.End, "@late", y.End.AddDays(grace + 1)).Rows.Cast<DataRow>()
                .Select(r => new GlRec { key = GlFmt.Iso(r[0]), date = GlFmt.Iso(r[0]), reference = "Recorded " + GlFmt.Date(r[0]), account = GlFmt.Plural(GlDb.L(r[1]), "line", "lines"), text = GlAudit.Cut(GlDb.S(r[3]) + "; by " + GlDb.S(r[4]), 200), amount = GlDb.M(r[2]), n = GlDb.L(r[1]) }).ToList();
            if (recs.Count == 0) continue;
            long n = recs.Sum(a => a.n);
            res.Add(F(x, "W08", "LATE:" + y.Label, null, GlFmt.Plural(n, "line", "lines") + " dated in " + y.Label + " recorded after the year ended", n, recs.Sum(a => a.amount),
                "Recorded more than " + grace + " days after " + GlFmt.Date(y.End) + ". Figures already reported for the year have changed since.", y.DateLabel, recs));
        }
        // Months and years signed off: lines recorded after the sign-off.
        foreach (DataRow so in GlDb.Table(x.C, null,
            "SELECT p.period_key, p.period_kind, p.created_at FROM gl_period_signoff p WHERE p.action='SIGNED_OFF' AND p.id = (SELECT MAX(q.id) FROM gl_period_signoff q WHERE q.period_key = p.period_key)").Rows)
        {
            string key = GlDb.S(so[0]); DateTime at = GlDb.D(so[2]).Value; DateTime a, b;
            if (!PeriodRange(x, key, GlDb.S(so[1]), out a, out b)) continue;
            DataRow r = GlDb.Table(x.C, null, "SELECT COUNT(*), IFNULL(SUM(transaction_amount),0) FROM fin_ledger WHERE transactionDate BETWEEN @a AND @b AND timeLog > @at", "@a", a, "@b", b, "@at", at).Rows[0];
            long n = GlDb.L(r[0]);
            if (n == 0) continue;
            res.Add(F(x, "W08", "SIGNOFF:" + key, null, GlFmt.Plural(n, "line", "lines") + " posted into " + key + " after it was signed off", n, GlDb.M(r[1]),
                "The period was signed off on " + GlFmt.When(at) + ". Its signed-off figures no longer match.", key, null));
        }
        return res;
    }

    public static bool PeriodRange(GlWarnCtx x, string key, string kind, out DateTime a, out DateTime b)
    {
        a = b = DateTime.MinValue;
        if (kind == "MONTH")
        {
            DateTime m;
            if (!DateTime.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out m)) return false;
            a = m; b = m.AddMonths(1).AddDays(-1); return true;
        }
        GlCalc.FinYear y = x.Years.FirstOrDefault(v => v.Label == key || "FY" + v.Label == key);
        if (y == null) return false;
        a = y.Start; b = y.End; return true;
    }

    private static List<GlFinding> W09(GlWarnCtx x)
    {
        var acked = x.Acked("W09");
        var recs = new List<GlRec>();
        using (var cmd = GlDb.Cmd(x.C, null,
            "SELECT MD5(CONCAT_WS('|', accountcode, transactionType, transaction_amount, transactionDate, IFNULL(particulars,''))) k, accountcode, transactionType, transaction_amount, transactionDate, particulars, COUNT(*) n, MIN(voucherNo) " +
            "FROM fin_ledger WHERE transaction_amount > 0 GROUP BY accountcode, transactionType, transaction_amount, transactionDate, particulars HAVING n > 1"))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
            {
                string k = r.GetString(0);
                if (acked.Contains(k)) continue;
                long n = Convert.ToInt64(r[6]); decimal amt = Convert.ToDecimal(r[3]);
                recs.Add(new GlRec { key = k, date = GlFmt.Iso(r[4]), reference = GlFmt.Count(n) + " copies, " + Convert.ToString(r[2]), account = Convert.ToString(r[1]), text = Convert.ToString(r[5]), amount = (n - 1) * amt, amount2 = amt, link = AccountLink(Convert.ToString(r[1])) });
            }
        if (recs.Count == 0) return None();
        return One(F(x, "W09", "", null, GlFmt.Plural(recs.Count, "group", "groups") + " of identical lines", recs.Count, recs.Sum(a => a.amount),
            "Amount is the value of the extra copies. Accept genuine repeats one by one; reverse real duplicates with an adjusting entry.", "Whole ledger", recs));
    }

    private static List<GlFinding> W10(GlWarnCtx x)
    {
        DataRow t = GlDb.Table(x.C, null,
            "SELECT (SELECT IFNULL(-SUM(total_balance),0) FROM fin_student_balance_cache), (SELECT MAX(updated_at) FROM fin_student_balance_cache), " +
            "(SELECT IFNULL(SUM(" + GlCalc.NetBare + "),0) FROM fin_ledger WHERE account_type IN ('Student','-')), " +
            "(SELECT IFNULL(SUM(IF(trans_type='Bill',amount,-amount)),0) FROM fin_studentfeestracking WHERE post_status='Posted')").Rows[0];
        decimal canon = GlDb.M(t[0]), ledger = GlDb.M(t[2]), tracking = GlDb.M(t[3]);
        var recs = GlDb.Table(x.C, null,
            "SELECT c.regno, -c.total_balance canon, IFNULL(x.b,0) ledger FROM fin_student_balance_cache c LEFT JOIN " +
            "(SELECT accountcode, SUM(" + GlCalc.NetBare + ") b FROM fin_ledger WHERE account_type IN ('Student','-') GROUP BY accountcode) x ON x.accountcode = c.regno " +
            "WHERE ABS(IFNULL(x.b,0) + c.total_balance) >= 1000").Rows.Cast<DataRow>()
            .Select(r => new GlRec { key = GlDb.S(r[0]), reference = GlDb.S(r[0]), account = "Canonical " + B(GlDb.M(r[1])), text = "Ledger student lines " + B(GlDb.M(r[2])), amount = GlDb.M(r[2]) - GlDb.M(r[1]), amount2 = GlDb.M(r[1]), link = AccountLink("SUB:STUDENTS") + "&m=" + Uri.EscapeDataString(GlDb.S(r[0])) }).ToList();
        if (recs.Count == 0 && Math.Abs(ledger - canon) < 1000) return None();
        return One(F(x, "W10", "", null, "Student receivables: " + GlFmt.Plural(recs.Count, "student differs", "students differ") + " between the canonical balance and the ledger", recs.Count, recs.Sum(a => Math.Abs(a.amount)),
            "Owed by students: canonical " + B(canon) + " (refreshed " + GlFmt.When(t[1]) + "), ledger student lines " + B(ledger) + ", fee tracking " + B(tracking) + ". The three are shown side by side and never added. There is no receivables control account.",
            "Students", recs));
    }

    private static List<GlFinding> W11(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        foreach (GlCalc.ControlPair p in GlCalc.ControlPairs(x.S))
        {
            if (p.Student) continue;   // students have no control account: W10
            long cb = GlCalc.Balance(x.S, p.Control.Key, x.End) * p.Sign, sb = GlCalc.Balance(x.S, p.Sub.Key, x.End) * p.Sign;
            if (Math.Abs(sb - cb) < 1) continue;
            var recs = new List<GlRec>
            {
                new GlRec { key = p.Control.Code, reference = p.Control.Code, account = p.Control.Name, text = "Control account balance", amount = cb, link = AccountLink(p.Control.Code) },
                new GlRec { key = p.Sub.Key, reference = p.Sub.Key, account = p.Sub.Name, text = "Subsidiary ledger balance", amount = sb, link = AccountLink(p.Sub.Key) }
            };
            res.Add(F(x, "W11", p.Control.Code, null, p.Control.Code + " " + p.Control.Name + " differs from its subsidiary ledger by " + B(sb - cb), 1, Math.Abs(sb - cb),
                "Control " + B(cb) + " against the subsidiary ledger " + B(sb) + (p.Sign < 0 ? " (owed, credit positive)" : "") + ". Postings to the subsidiary ledger do not pass through the control account.", p.Control.Code, recs));
        }
        return res;
    }

    public static readonly string[] ReqApproved = { "BURSAR_APPROVED", "VC_PENDING", "PROCUREMENT", "FINANCE", "PENDING_PAYMENT", "APPROVED" };

    private static List<GlFinding> W12(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        DataTable t = GlDb.Table(x.C, null,
            "SELECT ID, req_number, title, status, total_amount, IFNULL(ledger_ref,''), IFNULL(ledger_posted,0), IFNULL(payment_ref,''), submitted_at, department FROM campus_dynamics.sys_requisitions");
        var appr = new List<GlRec>(); var posted = new List<GlRec>();
        foreach (DataRow r in t.Rows)
        {
            string st = GlDb.S(r[3]).ToUpperInvariant(), lref = GlDb.S(r[5]);
            var rec = new GlRec { key = GlDb.S(r[0]), date = GlFmt.Iso(r[8]), reference = GlDb.S(r[1]), account = GlDb.S(r[9]), text = GlDb.S(r[2]) + " (" + st + ")", amount = GlDb.M(r[4]), link = "RequisitionDetail.aspx?id=" + GlDb.S(r[0]) };
            if (ReqApproved.Contains(st) && GlDb.S(r[7]) == "") appr.Add(rec);
            if (GlDb.I(r[6]) == 1 || st == "LEDGER_POSTED")
            {
                long n = lref == "" ? 0 : GlDb.L(GlDb.Scalar(x.C, null, "SELECT COUNT(*) FROM fin_ledger WHERE RefNo = @r OR folio = @r", "@r", lref));
                if (n == 0) posted.Add(rec);
            }
        }
        if (appr.Count > 0)
            res.Add(F(x, "W12", "APPROVED_NOT_PAID", Medium, GlFmt.Plural(appr.Count, "requisition is", "requisitions are") + " approved but not paid", appr.Count, appr.Sum(a => a.amount),
                "Approved spending is waiting with no payment reference.", "Requisitions", appr));
        if (posted.Count > 0)
            res.Add(F(x, "W12", "POSTED_NO_LEDGER", High, GlFmt.Plural(posted.Count, "requisition is", "requisitions are") + " marked posted with no ledger lines", posted.Count, posted.Sum(a => a.amount),
                "The requisition screens set a Posted flag and an invented reference; nothing reaches the ledger.", "Requisitions", posted));
        return res;
    }

    private static List<GlFinding> W13(GlWarnCtx x)
    {
        var cash = GlCalc.CashAccounts(x.S).Select(a => a.Code).ToList();
        var exp = x.S.Accounts.Values.Where(a => (a.Kind == "CHART" || a.Kind == "PROVISIONAL") && a.Category == "Expense").Select(a => a.Code).ToList();
        if (cash.Count == 0 || exp.Count == 0) return None();
        var p = new List<object>(); var cin = new List<string>(); var ein = new List<string>();
        for (int i = 0; i < cash.Count; i++) { cin.Add("@c" + i); p.Add("@c" + i); p.Add(cash[i]); }
        for (int i = 0; i < exp.Count; i++) { ein.Add("@e" + i); p.Add("@e" + i); p.Add(exp[i]); }
        var refs = new HashSet<string>(GlDb.Table(x.C, null, "SELECT ledger_ref FROM campus_dynamics.sys_requisitions WHERE IFNULL(ledger_ref,'') <> ''").Rows.Cast<DataRow>().Select(r => GlDb.S(r[0])), StringComparer.OrdinalIgnoreCase);
        var recs = new List<GlRec>();
        foreach (DataRow r in GlDb.Table(x.C, null,
            "SELECT DATE_FORMAT(b.transactionDate,'%Y-%m') m, COUNT(*), SUM(b.transaction_amount), GROUP_CONCAT(DISTINCT IFNULL(b.RefNo,'') SEPARATOR '|') FROM fin_ledger b " +
            "WHERE b.transactionType='CR' AND b.voucherNo > 0 AND b.accountcode IN (" + string.Join(",", cin) + ") " +
            "AND EXISTS (SELECT 1 FROM fin_ledger e WHERE e.voucherNo = b.voucherNo AND e.transactionType='DR' AND e.accountcode IN (" + string.Join(",", ein) + ")) GROUP BY m ORDER BY m",
            p.ToArray()).Rows)
        {
            if (refs.Count > 0 && GlDb.S(r[3]).Split('|').Any(refs.Contains)) continue;
            recs.Add(new GlRec { key = GlDb.S(r[0]), date = GlDb.S(r[0]) + "-01", reference = GlDb.S(r[0]), account = GlFmt.Plural(GlDb.L(r[1]), "payment line", "payment lines"), text = "Bank or cash paid for expenses", amount = GlDb.M(r[2]), n = GlDb.L(r[1]), link = "AccountsReports.aspx?r=R13&from=" + GlDb.S(r[0]) + "-01" });
        }
        if (recs.Count == 0) return None();
        long n = recs.Sum(a => a.n);
        return One(F(x, "W13", "", null, GlFmt.Plural(n, "expense payment has", "expense payments have") + " no requisition", n, recs.Sum(a => a.amount),
            "Every bank or cash payment for an expense was made without a requisition, so approved spending and paid spending cannot be matched.", "Bank and cash accounts", recs));
    }

    private static List<GlFinding> W14(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        decimal tol = GlSettings.Dec("side_tolerance", 1000);
        var bal = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        int e = GlCalc.DayInt(x.End);
        foreach (GlAgg g in x.S.Agg) if (g.Day <= e) { long v; bal.TryGetValue(g.Key, out v); bal[g.Key] = v + g.Dr - g.Cr; }
        foreach (var kv in bal)
        {
            GlAcct a = x.S.Accounts[kv.Key];
            if ((a.Kind != "CHART" && a.Kind != "PROVISIONAL") || a.Contra || GlChecks.IsSuspense(a)) continue;
            if (a.Category == "Unclassified" || a.Category == "Suspense") continue;
            bool wrong = a.DebitNatural ? kv.Value < -tol : kv.Value > tol;
            if (!wrong) continue;
            string side = kv.Value > 0 ? "debit" : "credit";
            res.Add(F(x, "W14", a.Code, null, a.Code + " " + a.Name + " has a " + side + " balance of " + B(Math.Abs(kv.Value)), 1, Math.Abs(kv.Value),
                CategoryArticle(a.Category) + " account normally has a " + (a.DebitNatural ? "debit" : "credit") + " balance." + (a.Code == "AC7008" ? " Retained earnings carries the debit because no year has been closed to it." : ""),
                a.Category + ", " + a.SubCategory, new List<GlRec> { new GlRec { key = a.Code, reference = a.Code, account = a.Name, text = a.KindText, amount = kv.Value, link = AccountLink(a.Code) } }));
        }
        return res;
    }

    private static List<GlFinding> W15(GlWarnCtx x)
    {
        var acked = x.Acked("W15");
        var recs = new List<GlRec>();
        foreach (DataRow r in GlDb.Table(x.C, null,
            "SELECT regno, item_code, acadyear, semester, COUNT(*) n, SUM(amount) total, MAX(amount) one, MIN(trans_date), GROUP_CONCAT(DISTINCT detail SEPARATOR '; ') " +
            "FROM fin_studentfeestracking WHERE trans_type='Bill' AND post_status='Posted' AND IFNULL(detail,'') NOT LIKE '%Reversal%' " +
            "GROUP BY regno, item_code, acadyear, semester HAVING n > 1 AND total > one").Rows)
        {
            string k = GlDb.S(r[0]) + "|" + GlDb.S(r[1]) + "|" + GlDb.S(r[2]) + "|" + GlDb.S(r[3]);
            if (acked.Contains(k)) continue;
            recs.Add(new GlRec { key = k, date = GlFmt.Iso(r[7]), reference = GlDb.S(r[0]), account = "Item " + GlDb.S(r[1]) + ", " + GlDb.S(r[2]) + " semester " + GlDb.S(r[3]), text = GlFmt.Count(GlDb.L(r[4])) + " bills: " + GlAudit.Cut(GlDb.S(r[8]), 150), amount = GlDb.M(r[5]) - GlDb.M(r[6]), amount2 = GlDb.M(r[5]), link = AccountLink("SUB:STUDENTS") + "&m=" + Uri.EscapeDataString(GlDb.S(r[0])) });
        }
        if (recs.Count == 0) return None();
        return One(F(x, "W15", "", null, GlFmt.Plural(recs.Count, "student was", "students were") + " billed more than once for the same item and term", recs.Count, recs.Sum(a => a.amount),
            "Amount is the value of the extra bills. Check each against the student statement; reverse a real double bill through the billing screens.", "Fee tracking", recs));
    }

    private static List<GlFinding> W16(GlWarnCtx x)
    {
        var recs = GlDb.Table(x.C, null,
            "SELECT table_name, IFNULL(table_rows,0), create_time FROM information_schema.tables WHERE table_schema = 'campus_dynamics_accounts' " +
            "AND table_name REGEXP '(_bak|_backup|backup_|_bk|_old|_copy|_tmp|_before|_snapshot|_20[0-9]{2})' ORDER BY table_rows DESC").Rows.Cast<DataRow>()
            .Select(r => new GlRec { key = GlDb.S(r[0]), date = GlFmt.Iso(r[2]), reference = GlDb.S(r[0]), account = GlFmt.Plural(GlDb.L(r[1]), "row", "rows"), text = "Created " + GlFmt.Date(r[2]), amount = 0 }).ToList();
        if (recs.Count == 0) return None();
        return One(F(x, "W16", "", null, GlFmt.Plural(recs.Count, "backup or repair table is", "backup or repair tables are") + " in the live finance database", recs.Count, 0,
            "MIS can move them to an archive schema once approved (open question Q7). Nothing here deletes them.", "campus_dynamics_accounts", recs));
    }

    private static List<GlFinding> W17(GlWarnCtx x)
    {
        GlCalc.FinYear y = GlCalc.YearOf(x.Years, DateTime.Today) ?? x.Years.LastOrDefault();
        if (y == null) return None();
        var budgeted = new HashSet<string>(GlDb.Table(x.C, null, "SELECT DISTINCT TRIM(item_code) FROM fin_budget WHERE budget_year IN (@l, @i)", "@l", y.Label, "@i", y.Id.ToString(CultureInfo.InvariantCulture)).Rows.Cast<DataRow>().Select(r => GlDb.S(r[0])), StringComparer.OrdinalIgnoreCase);
        var mv = GlCalc.Movement(x.S, y.Start, y.End);
        var recs = new List<GlRec>();
        foreach (var kv in mv)
        {
            GlAcct a = x.S.Accounts[kv.Key];
            if ((a.Kind != "CHART" && a.Kind != "PROVISIONAL") || a.Category != "Expense" || kv.Value == 0 || budgeted.Contains(a.Code)) continue;
            recs.Add(new GlRec { key = a.Code, reference = a.Code, account = a.Name, text = "Spent in " + y.Label, amount = kv.Value, link = AccountLink(a.Code) });
        }
        if (recs.Count == 0) return None();
        return One(F(x, "W17", y.Label, null, GlFmt.Plural(recs.Count, "expense account has", "expense accounts have") + " spending in " + y.Label + " and no budget", recs.Count, recs.Sum(a => a.amount),
            budgeted.Count == 0 ? "No budget has been recorded for " + y.Label + " at all (fin_budget is empty)." : "These accounts have no budget line for the year.", y.DateLabel, recs));
    }

    private static List<GlFinding> W18(GlWarnCtx x)
    {
        var acked = x.Acked("W18");
        decimal floor = GlSettings.Dec("large_posting_floor", 10000000), sigma = GlSettings.Dec("large_posting_sigma", 4);
        var recs = new List<GlRec>();
        foreach (DataRow r in GlDb.Table(x.C, null,
            "SELECT l.TID, l.transactionDate, l.accountcode, l.particulars, l.transaction_amount, l.transactionType, l.voucherNo, a.mu FROM fin_ledger l JOIN " +
            "(SELECT accountcode, AVG(transaction_amount) mu, STDDEV_POP(transaction_amount) sd, COUNT(*) n FROM fin_ledger GROUP BY accountcode HAVING n >= 20) a ON a.accountcode = l.accountcode " +
            "WHERE l.transaction_amount > a.mu + @s * a.sd AND l.transaction_amount > @f ORDER BY l.transaction_amount DESC", "@s", sigma, "@f", floor).Rows)
        {
            string k = GlDb.S(r[0]);
            if (acked.Contains(k)) continue;
            recs.Add(new GlRec { key = k, date = GlFmt.Iso(r[1]), reference = "Voucher " + GlDb.S(r[6]), account = GlDb.S(r[2]), text = GlDb.S(r[5]) + " " + GlDb.S(r[3]) + " (account average " + B(GlDb.M(r[7])) + ")", amount = GlDb.M(r[4]), link = VoucherLink(GlDb.L(r[6])) });
        }
        if (recs.Count == 0) return None();
        return One(F(x, "W18", "", null, GlFmt.Plural(recs.Count, "posting is", "postings are") + " unusually large for its account", recs.Count, recs.Sum(a => a.amount),
            "Above " + B(floor) + " and more than " + sigma.ToString("0", CultureInfo.InvariantCulture) + " standard deviations above the account's average line. Review each and accept the genuine ones.", "Whole ledger", recs));
    }

    private static List<GlFinding> W19(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        var e = GlDb.Table(x.C, null,
            "SELECT DATE(trigger_date) d, IFNULL(triggered_by,'unknown'), COUNT(*), SUM(ABS(CAST(IFNULL(new_transaction_amount,0) AS SIGNED) - CAST(IFNULL(old_transaction_amount,0) AS SIGNED))) FROM edit_ledger GROUP BY d, 2 ORDER BY d DESC").Rows.Cast<DataRow>()
            .Select(r => new GlRec { key = GlFmt.Iso(r[0]) + "|" + GlDb.S(r[1]), date = GlFmt.Iso(r[0]), reference = r[0] is DBNull ? "Undated" : GlFmt.Date(r[0]), account = GlFmt.Plural(GlDb.L(r[2]), "row edited", "rows edited"), text = "By " + GlDb.S(r[1]), amount = GlDb.M(r[3]), link = "AccountsReports.aspx?r=R16&kind=edits" }).ToList();
        if (e.Count > 0)
        {
            long n = GlDb.L(GlDb.Scalar(x.C, null, "SELECT COUNT(*) FROM edit_ledger"));
            res.Add(F(x, "W19", "EDITED", null, GlFmt.Plural(n, "posted ledger line was", "posted ledger lines were") + " edited in place", n, e.Sum(a => a.amount),
                "Amount is the total change in value. Rows with no date or actor were edited before the edit trigger was repaired in August 2026.", "edit_ledger", e));
        }
        var d = GlDb.Table(x.C, null,
            "SELECT DATE(delete_date) d, IFNULL(deleted_by,'unknown'), COUNT(*), SUM(transaction_amount) FROM fin_deleted_ledger GROUP BY d, 2 ORDER BY d DESC").Rows.Cast<DataRow>()
            .Select(r => new GlRec { key = GlFmt.Iso(r[0]) + "|" + GlDb.S(r[1]), date = GlFmt.Iso(r[0]), reference = r[0] is DBNull ? "Undated" : GlFmt.Date(r[0]), account = GlFmt.Plural(GlDb.L(r[2]), "row deleted", "rows deleted"), text = "By " + GlDb.S(r[1]), amount = GlDb.M(r[3]), link = "AccountsReports.aspx?r=R16&kind=deletions" }).ToList();
        if (d.Count > 0)
        {
            long n = GlDb.L(GlDb.Scalar(x.C, null, "SELECT COUNT(*) FROM fin_deleted_ledger"));
            res.Add(F(x, "W19", "DELETED", null, GlFmt.Plural(n, "ledger line was", "ledger lines were") + " deleted from the live ledger", n, d.Sum(a => a.amount),
                "Bulk deletions run directly on the database, mostly by the dbmanager and root accounts. The deleted rows are kept in fin_deleted_ledger.", "fin_deleted_ledger", d));
        }
        return res;
    }

    private static List<GlFinding> W20(GlWarnCtx x)
    {
        var res = new List<GlFinding>();
        foreach (GlAcct a in x.S.Accounts.Values.Where(GlChecks.IsSuspense))
        {
            long b = GlCalc.Balance(x.S, a.Key, x.End);
            if (b == 0) continue;
            long n = x.S.Agg.Where(g => string.Equals(g.Key, a.Key, StringComparison.OrdinalIgnoreCase)).Sum(g => (long)g.N);
            res.Add(F(x, "W20", a.Code, null, a.Code + " " + a.Name + " carries a " + (b > 0 ? "debit" : "credit") + " balance of " + B(Math.Abs(b)), n, Math.Abs(b),
                "A repair script parked differences here. The balance hides the vouchers that are really wrong; clear it voucher by voucher (open question Q4).", a.Code,
                new List<GlRec> { new GlRec { key = a.Code, reference = a.Code, account = a.Name, text = GlFmt.Plural(n, "line", "lines"), amount = b, link = AccountLink(a.Code) } }));
        }
        return res;
    }

    private static List<GlFinding> W21(GlWarnCtx x)
    {
        var recs = new List<GlRec>();
        var ys = x.Years.OrderBy(y => y.Start).ToList();
        for (int i = 0; i < ys.Count; i++)
        {
            GlCalc.FinYear y = ys[i];
            int first;
            string lab = (y.Label ?? "").Trim();
            if (lab.Length >= 4 && int.TryParse(lab.Substring(0, 4), out first) && first != y.Start.Year)
                recs.Add(new GlRec { key = "LABEL:" + y.Id, reference = y.Label, account = y.DateLabel, text = "The label says " + lab + " but the year starts in " + y.Start.Year + ".", amount = 0 });
            if (i > 0 && y.Start <= ys[i - 1].End)
                recs.Add(new GlRec { key = "OVERLAP:" + y.Id, reference = y.Label, account = y.DateLabel, text = "Overlaps " + ys[i - 1].Label + " (" + ys[i - 1].DateLabel + ").", amount = 0 });
            if (i > 0 && y.Start > ys[i - 1].End.AddDays(1))
                recs.Add(new GlRec { key = "GAP:" + y.Id, reference = y.Label, account = y.DateLabel, text = "Gap after " + ys[i - 1].Label + ": " + GlFmt.Date(ys[i - 1].End.AddDays(1)) + " to " + GlFmt.Date(y.Start.AddDays(-1)) + " is in no year.", amount = 0 });
            if (y.Status.Equals("Open", StringComparison.OrdinalIgnoreCase) && y.End < DateTime.Today && !ys.Skip(i + 1).Any())
                recs.Add(new GlRec { key = "ENDED:" + y.Id, reference = y.Label, account = y.DateLabel, text = "Open year already ended, and no later year exists.", amount = 0 });
            if ((y.End - y.Start).TotalDays > 400)
                recs.Add(new GlRec { key = "LONG:" + y.Id, reference = y.Label, account = y.DateLabel, text = "Longer than a year (" + ((int)(y.End - y.Start).TotalDays + 1) + " days).", amount = 0 });
        }
        if (recs.Count == 0) return None();
        return One(F(x, "W21", "", null, "The financial year table has " + GlFmt.Plural(recs.Count, "problem", "problems"), recs.Count, 0,
            "Year reports follow this table. Correcting it is a decision for the Bursar (open question Q1).", "fin_financial_years", recs));
    }

    private static List<GlFinding> W22(GlWarnCtx x)
    {
        int days = GlSettings.Int("pending_journal_days", 30);
        var recs = GlDb.Table(x.C, null,
            "SELECT j.JournalNo, j.journalDate, j.Teller, j.journalParticulars, IFNULL(d.dr,0) FROM fin_journalnumbers j " +
            "LEFT JOIN (SELECT journal_no, SUM(transaction_amount) dr FROM fin_journal_details WHERE transactionType='DR' GROUP BY journal_no) d ON d.journal_no = j.JournalNo " +
            "WHERE j.PostStatus = 'Pending' AND j.journalDate < @d ORDER BY j.journalDate", "@d", DateTime.Today.AddDays(-days)).Rows.Cast<DataRow>()
            .Select(r => new GlRec { key = GlDb.S(r[0]), date = GlFmt.Iso(r[1]), reference = "Journal " + GlDb.S(r[0]).Trim(), account = "By " + GlDb.S(r[2]), text = GlDb.S(r[3]), amount = GlDb.M(r[4]), link = "JournalEntries.aspx" }).ToList();
        if (recs.Count == 0) return None();
        return One(F(x, "W22", "", null, GlFmt.Plural(recs.Count, "journal has", "journals have") + " been pending for more than " + days + " days", recs.Count, recs.Sum(a => a.amount),
            "Approve or void them in the journal screen. Amount is the debit total of their lines.", "fin_journalnumbers", recs));
    }

    // ── The run ─────────────────────────────────────────────────────

    public class RunResult
    {
        public int RunId, Rules, Open, Critical, High, Medium, Health, Found, New, Reappeared, Fixed;
        public long Ms;
        public List<string> Errors = new List<string>();
    }

    public static bool IsRunning { get { return _running != 0; } }

    /// <summary>
    /// Runs every rule (or one) and records the results. Returns null when a run is already in progress.
    /// trigger: MANUAL | AUTO | SCHEDULE.
    /// </summary>
    public static RunResult Run(string trigger, string actor, string onlyRule = null)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return null;
        try { return RunCore(trigger, string.IsNullOrEmpty(actor) ? "system" : actor, onlyRule); }
        finally { Interlocked.Exchange(ref _running, 0); }
    }

    /// <summary>Detection only: what the rules find now, with nothing recorded. Used for View records.</summary>
    public static List<GlFinding> DetectOnly(string rule)
    {
        GlRule r = Rule(rule);
        if (r == null) throw new GlRefusal("Unknown rule " + rule + ".");
        using (var c = GlDb.Read())
        {
            GlWarnCtx x = Ctx(c, false);
            return r.Detect(x);
        }
    }

    private static GlWarnCtx Ctx(MySqlConnection c, bool force)
    {
        var x = new GlWarnCtx { C = c, S = GlCalc.Get(force), Years = GlCalc.Years(c) };
        x.End = GlCalc.FromDayInt(Math.Max(x.S.MaxDay, GlCalc.DayInt(DateTime.Today)));
        foreach (DataRow r in GlDb.Table(c, null, "SELECT rule_code, record_key FROM gl_record_ack WHERE is_active = 1").Rows)
        {
            string rc = GlDb.S(r[0]);
            if (!x.Acks.ContainsKey(rc)) x.Acks[rc] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            x.Acks[rc].Add(GlDb.S(r[1]));
        }
        return x;
    }

    private static RunResult RunCore(string trigger, string actor, string onlyRule)
    {
        var res = new RunResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using (var w = GlDb.Write())
        {
            res.RunId = (int)GlDb.Insert(w, null, "INSERT INTO gl_warning_run (started_at, started_by, trigger_kind) VALUES (NOW(), @u, @t)", "@u", GlAudit.Cut(actor, 100), "@t", trigger);
            var found = new List<GlFinding>();
            var ranOk = new HashSet<string>();
            using (var c = GlDb.Read())
            {
                GlWarnCtx x = Ctx(c, trigger != "AUTO");
                foreach (GlRule r in Rules)
                {
                    if (onlyRule != null && r.Code != onlyRule) continue;
                    try { found.AddRange(r.Detect(x)); ranOk.Add(r.Code); res.Rules++; }
                    catch (Exception ex) { res.Errors.Add(r.Code + ": " + ex.Message); GlLog.Error("GlWarnings " + r.Code, ex); }
                }
            }
            res.Found = found.Count;

            using (var tx = w.BeginTransaction())
            {
                var existing = new Dictionary<string, DataRow>();
                foreach (DataRow r in GlDb.Table(w, tx, "SELECT id, rule_code, scope_key, status, record_count, amount, IFNULL(ack_count,0), IFNULL(ack_amount,0) FROM gl_warning FOR UPDATE").Rows)
                    existing[GlDb.S(r[1]) + "|" + GlDb.S(r[2])] = r;
                var seen = new HashSet<string>();
                foreach (GlFinding f in found)
                {
                    string k = f.Rule + "|" + f.Scope;
                    if (!seen.Add(k)) continue;
                    string sample = f.Sample == null ? null : GlAudit.Cut(FaJson.Ser(f.Sample), 60000);
                    DataRow ex;
                    if (!existing.TryGetValue(k, out ex))
                    {
                        long id = GlDb.Insert(w, tx,
                            "INSERT INTO gl_warning (rule_code, scope_key, title, severity, status, record_count, amount, sample_json, cause, where_text, first_detected, last_seen) " +
                            "VALUES (@r,@s,@t,@sev,'OPEN',@n,@a,@j,@c,@w,NOW(),NOW())",
                            "@r", f.Rule, "@s", GlAudit.Cut(f.Scope, 120), "@t", GlAudit.Cut(f.Title, 250), "@sev", f.Severity, "@n", (int)Math.Min(f.Count, int.MaxValue), "@a", f.Amount,
                            "@j", sample, "@c", GlAudit.Cut(f.Cause, 1000), "@w", GlAudit.Cut(f.Where, 300));
                        Event(w, tx, id, "DETECTED", f.Title, f.Count, f.Amount, actor);
                        res.New++;
                    }
                    else
                    {
                        long id = GlDb.L(ex[0]); string st = GlDb.S(ex[3]);
                        string next = st;
                        if (st == "FIXED") { next = "REAPPEARED"; Event(w, tx, id, "REAPPEARED", f.Title, f.Count, f.Amount, actor); res.Reappeared++; }
                        else if (st == "ACKNOWLEDGED" && (f.Count > GlDb.L(ex[6]) || Math.Abs(f.Amount) > Math.Abs(GlDb.M(ex[7])) + 0.5m))
                        {
                            next = "REAPPEARED";
                            Event(w, tx, id, "REAPPEARED", "Grew beyond what was acknowledged (" + GlFmt.Count(GlDb.L(ex[6])) + " records, " + B(GlDb.M(ex[7])) + ") to " + GlFmt.Count(f.Count) + " records, " + B(f.Amount) + ".", f.Count, f.Amount, actor);
                            res.Reappeared++;
                        }
                        GlDb.Exec(w, tx,
                            "UPDATE gl_warning SET title=@t, severity=@sev, status=@st, record_count=@n, amount=@a, sample_json=@j, cause=@c, where_text=@w, last_seen=NOW(), fixed_at=IF(@st='FIXED',fixed_at,NULL) WHERE id=@id",
                            "@t", GlAudit.Cut(f.Title, 250), "@sev", f.Severity, "@st", next, "@n", (int)Math.Min(f.Count, int.MaxValue), "@a", f.Amount, "@j", sample,
                            "@c", GlAudit.Cut(f.Cause, 1000), "@w", GlAudit.Cut(f.Where, 300), "@id", id);
                    }
                    GlDb.Exec(w, tx, "INSERT INTO gl_warning_trend (run_id, rule_code, scope_key, record_count, amount) VALUES (@run,@r,@s,@n,@a)",
                        "@run", res.RunId, "@r", f.Rule, "@s", GlAudit.Cut(f.Scope, 120), "@n", (int)Math.Min(f.Count, int.MaxValue), "@a", f.Amount);
                }
                // Not found this run (and the rule ran cleanly): fixed.
                foreach (var kv in existing)
                {
                    string rule = kv.Key.Split('|')[0];
                    if (seen.Contains(kv.Key) || !ranOk.Contains(rule)) continue;
                    string st = GlDb.S(kv.Value[3]);
                    if (st == "FIXED") continue;
                    long id = GlDb.L(kv.Value[0]);
                    GlDb.Exec(w, tx, "UPDATE gl_warning SET status='FIXED', fixed_at=NOW(), record_count=0, amount=0 WHERE id=@id", "@id", id);
                    Event(w, tx, id, "FIXED", "No longer found by the detection run.", 0, 0, actor);
                    res.Fixed++;
                }
                Score(w, tx, res);
                res.Ms = sw.ElapsedMilliseconds;
                GlDb.Exec(w, tx,
                    "UPDATE gl_warning_run SET finished_at=NOW(), duration_ms=@ms, rules_run=@rr, open_total=@o, critical_open=@c, high_open=@h, medium_open=@m, health_score=@hs, errors=@e WHERE id=@id",
                    "@ms", (int)res.Ms, "@rr", res.Rules, "@o", res.Open, "@c", res.Critical, "@h", res.High, "@m", res.Medium, "@hs", res.Health,
                    "@e", res.Errors.Count == 0 ? (object)DBNull.Value : GlAudit.Cut(string.Join("\n", res.Errors), 60000), "@id", res.RunId);
                GlAudit.WriteAs(actor, w, tx, "WARNING_RUN", res.RunId.ToString(CultureInfo.InvariantCulture), "RUN", null,
                    new { trigger, rules = res.Rules, found = res.Found, added = res.New, reappeared = res.Reappeared, fixedCount = res.Fixed, health = res.Health, ms = res.Ms, errors = res.Errors }, null);
                tx.Commit();
            }
        }
        return res;
    }

    private static void Event(MySqlConnection w, MySqlTransaction tx, long id, string type, string detail, long count, decimal amount, string actor)
    {
        GlDb.Exec(w, tx, "INSERT INTO gl_warning_event (warning_id, event_type, detail, record_count, amount, actor, created_at) VALUES (@i,@t,@d,@n,@a,@u,NOW())",
            "@i", id, "@t", type, "@d", GlAudit.Cut(detail, 2000), "@n", (int)Math.Min(count, int.MaxValue), "@a", amount, "@u", GlAudit.Cut(actor, 100));
    }

    /// <summary>Health score over open warnings: each rule counts once, at the severity of its worst open scope.</summary>
    private static void Score(MySqlConnection c, MySqlTransaction tx, RunResult res)
    {
        int[] h = Health(c, tx);
        res.Open = h[0]; res.Critical = h[1]; res.High = h[2]; res.Medium = h[3]; res.Health = h[4];
    }

    /// <summary>[open warnings, critical rules, high rules, medium rules, score]. Each rule counts once, at its worst open severity.</summary>
    public static int[] Health(MySqlConnection c, MySqlTransaction tx)
    {
        var worst = new Dictionary<string, string>();
        int open = 0;
        foreach (DataRow r in GlDb.Table(c, tx, "SELECT rule_code, severity FROM gl_warning WHERE status IN ('OPEN','REAPPEARED')").Rows)
        {
            string rc = GlDb.S(r[0]), sev = GlDb.S(r[1]); string cur;
            open++;
            if (!worst.TryGetValue(rc, out cur) || SevRank(sev) > SevRank(cur)) worst[rc] = sev;
        }
        int cr = worst.Values.Count(v => v == Critical), hi = worst.Values.Count(v => v == High), me = worst.Values.Count(v => v == Medium);
        return new[] { open, cr, hi, me, Math.Max(0, 100 - (15 * cr + 6 * hi + 2 * me)) };
    }

    /// <summary>Last run time, or null if never run.</summary>
    public static DateTime? LastRun(MySqlConnection c)
    {
        return GlDb.D(GlDb.Scalar(c, null, "SELECT MAX(finished_at) FROM gl_warning_run WHERE finished_at IS NOT NULL"));
    }

    /// <summary>Starts detection in the background when the last run is older than the scheduled interval (called when the warnings page or dashboard opens). Never blocks the page.</summary>
    public static void EnsureFresh(string actor)
    {
        DateTime? last;
        using (var c = GlDb.Read()) last = LastRun(c);
        int hours = GlSettings.Int("detection_interval_hours", 6);
        if (last.HasValue && last.Value > DateTime.Now.AddHours(-hours)) return;
        if (IsRunning) return;
        ThreadPool.QueueUserWorkItem(_ => { try { Run("AUTO", actor); } catch (Exception ex) { GlLog.Error("GlWarnings auto", ex); } });
    }

    // ── Scheduled run (same pattern as BillingReconciliationJob) ────

    private static Timer _timer;
    private static readonly object _timerLock = new object();

    /// <summary>Arms a timer that runs detection every interval. Called from Application_Start; does no blocking work.</summary>
    public static void EnsureScheduled()
    {
        lock (_timerLock)
        {
            if (_timer != null) return;
            _timer = new Timer(_ =>
            {
                try
                {
                    DateTime? last;
                    using (var c = GlDb.Read()) last = LastRun(c);
                    int hours = 6;
                    try { using (var c = GlDb.Read()) { object v = GlDb.Scalar(c, null, "SELECT setting_value FROM gl_settings WHERE setting_key='detection_interval_hours'"); int h; if (v != null && int.TryParse(Convert.ToString(v), out h) && h > 0) hours = h; } }
                    catch { }
                    if (!last.HasValue || last.Value <= DateTime.Now.AddHours(-hours).AddMinutes(5)) Run("SCHEDULE", "scheduler");
                }
                catch (Exception ex) { GlLog.Error("GlWarnings schedule", ex); }
            }, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));
        }
    }

    // ── Counts for the sidebar badge and dashboard ─────────────────

    public class Summary { public int Open, Critical, High, Medium, Info, Acknowledged, FixedThisMonth, Health; public DateTime? LastRun, OldestOpen; public bool Running; }

    public static Summary GetSummary(MySqlConnection c)
    {
        var s = new Summary { Running = IsRunning };
        foreach (DataRow r in GlDb.Table(c, null, "SELECT status, severity, COUNT(*), MIN(first_detected) FROM gl_warning GROUP BY status, severity").Rows)
        {
            string st = GlDb.S(r[0]), sev = GlDb.S(r[1]); int n = GlDb.I(r[2]);
            if (st == "OPEN" || st == "REAPPEARED")
            {
                s.Open += n;
                if (sev == Critical) s.Critical += n; else if (sev == High) s.High += n; else if (sev == Medium) s.Medium += n; else s.Info += n;
                DateTime? d = GlDb.D(r[3]);
                if (d.HasValue && (!s.OldestOpen.HasValue || d < s.OldestOpen)) s.OldestOpen = d;
            }
            else if (st == "ACKNOWLEDGED") s.Acknowledged += n;
        }
        s.FixedThisMonth = GlDb.I(GlDb.Scalar(c, null, "SELECT COUNT(*) FROM gl_warning WHERE status='FIXED' AND fixed_at >= @m", "@m", new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)));
        s.LastRun = LastRun(c);
        s.Health = s.LastRun.HasValue ? Health(c, null)[4] : -1;
        return s;
    }
}
