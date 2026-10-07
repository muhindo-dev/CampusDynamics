using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger: periods and close (plan sections 0.6, 0.7 and 3).
//  A sign-off is a record of who reviewed a month or a year and what the
//  automatic checklist showed at that moment. It does not block posting
//  (open question Q5); later postings into a signed-off period are flagged
//  by Finance Warnings (W08) and need a reason on an adjusting entry.
//  Keys: month "yyyy-MM"; year "FY:" + start date.
// =====================================================================

public static class GlPeriods
{
    public class Period { public string Key, Kind, Label; public DateTime From, To; public GlCalc.FinYear Year; }

    public static Period Resolve(MySqlConnection c, string key)
    {
        var years = GlCalc.Years(c);
        key = (key ?? "").Trim();
        if (key.StartsWith("FY:"))
        {
            GlCalc.FinYear y = years.FirstOrDefault(v => "FY:" + GlFmt.Iso(v.Start) == key);
            if (y == null) throw new GlRefusal("Unknown financial year.");
            return new Period { Key = key, Kind = "YEAR", Label = "Financial year " + y.Label + " (" + y.DateLabel + ")", From = y.Start, To = y.End, Year = y };
        }
        DateTime m;
        if (!DateTime.TryParseExact(key + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out m)) throw new GlRefusal("Unknown period.");
        return new Period { Key = key, Kind = "MONTH", Label = m.ToString("MMMM yyyy", CultureInfo.InvariantCulture), From = m, To = m.AddMonths(1).AddDays(-1), Year = GlCalc.YearOf(years, m) };
    }

    public class Item { public string code, title, status, detail; public decimal amount; public long count; public GlFix fix; }

    /// <summary>The automatic checklist for a period, computed now from the read-only connection.</summary>
    public static List<Item> Checklist(Period p)
    {
        var l = new List<Item>();
        GlCalc.Snapshot s = GlCalc.Get(false);
        DateTime to = p.To > DateTime.Today ? DateTime.Today : p.To;
        string f = GlFmt.Iso(p.From), t = GlFmt.Iso(p.To);
        using (var c = GlDb.Read())
        {
            // 1 Debit equals credit in the period
            GlCalc.Totals mv = GlCalc.Total(s, p.From, p.To, "WHOLE");
            l.Add(new Item { code = "BALANCED", title = "Debit equals credit for lines dated in the period", status = mv.Diff == 0 ? "pass" : "fail", amount = mv.Diff, count = mv.Lines,
                detail = mv.Diff == 0 ? GlFmt.Plural(mv.Lines, "line", "lines") + " in balance." : "Debit " + GlFmt.Money(mv.Dr) + " against credit " + GlFmt.Money(mv.Cr) + ".", fix = new GlFix("report", "Trial balance with cause analysis", GlReports.ReportLink("R01", "from", f, "to", t)) });

            // 2 Unbalanced vouchers first dated in the period
            long nvd; int nvl;
            var ub = GlCalc.UnbalancedVouchers(p.From, p.To, "WHOLE", out nvd, out nvl);
            l.Add(new Item { code = "VOUCHERS", title = "Every voucher in the period balances", status = ub.Count == 0 ? "pass" : "fail", count = ub.Count, amount = ub.Sum(v => Math.Abs(v.Diff)),
                detail = ub.Count == 0 ? "" : GlFmt.Plural(ub.Count, "voucher does", "vouchers do") + " not balance within the period. Complete them with adjusting entries.", fix = new GlFix("report", "Unbalanced vouchers", GlReports.ReportLink("R18", "from", f, "to", t)) });

            // 3 Codes not in the chart and not mapped
            var unm = s.Agg.Where(g => g.Day >= GlCalc.DayInt(p.From) && g.Day <= GlCalc.DayInt(p.To) && s.Accounts[g.Key].Kind == "UNMAPPED").ToList();
            l.Add(new Item { code = "UNMAPPED", title = "Every code used is in the chart or mapped", status = unm.Count == 0 ? "pass" : "fail", count = unm.Sum(g => (long)g.N),
                detail = unm.Count == 0 ? "" : string.Join(", ", unm.Select(g => g.Key).Distinct().Take(10)), fix = new GlFix("report", "Chart of accounts", GlReports.ReportLink("R20", "status", "unmapped")) });

            // 4 Journals still pending, dated in the period
            DataRow pj = GlDb.Table(c, null, "SELECT COUNT(*) FROM fin_journalnumbers WHERE PostStatus='Pending' AND journalDate BETWEEN @f AND @t", "@f", p.From, "@t", p.To).Rows[0];
            long pending = GlDb.L(pj[0]);
            l.Add(new Item { code = "JOURNALS", title = "No journal dated in the period is still pending", status = pending == 0 ? "pass" : "fail", count = pending,
                detail = pending == 0 ? "" : "Approve or void them in the journal screen.", fix = new GlFix("screen", "Classic journal entries", "JournalEntries.aspx") });

            // 5 Control accounts agree with their subsidiary ledgers at the period end
            var bad = GlCalc.ControlPairs(s).Where(pp => !pp.Student).Select(pp => new { pp, d = (GlCalc.Balance(s, pp.Sub.Key, to) - GlCalc.Balance(s, pp.Control.Key, to)) * pp.Sign }).Where(x => x.d != 0).ToList();
            l.Add(new Item { code = "CONTROLS", title = "Control accounts agree with their subsidiary ledgers at the period end", status = bad.Count == 0 ? "pass" : "fail", count = bad.Count, amount = bad.Sum(x => Math.Abs(x.d)),
                detail = string.Join("; ", bad.Select(x => x.pp.Control.Code + " differs by " + GlFmt.Money(x.d))), fix = new GlFix("report", "Control account reconciliations", GlReports.ReportLink("R17", "to", GlFmt.Iso(to))) });

            // 6 Suspense and plug balances at the period end
            var plugs = s.Accounts.Values.Where(GlChecks.IsSuspense).Select(a => new { a, b = GlCalc.Balance(s, a.Key, to) }).Where(x => x.b != 0).ToList();
            l.Add(new Item { code = "SUSPENSE", title = "No balance is parked in a suspense or plug account", status = plugs.Count == 0 ? "pass" : "fail", count = plugs.Count, amount = plugs.Sum(x => Math.Abs(x.b)),
                detail = string.Join("; ", plugs.Select(x => x.a.Code + " " + GlFmt.Money(x.b))), fix = new GlFix("report", "Account statement", GlReports.ReportLink("R02", "account", plugs.Count > 0 ? plugs[0].a.Key : "", "to", GlFmt.Iso(to))) });

            // 7 Lines recorded late for this period
            int grace = GlSettings.Int("late_posting_grace_days", 30);
            DataRow lr = GlDb.Table(c, null, "SELECT COUNT(*), IFNULL(SUM(transaction_amount),0) FROM fin_ledger WHERE transactionDate BETWEEN @f AND @t AND timeLog > @late", "@f", p.From, "@t", p.To, "@late", p.To.AddDays(grace + 1)).Rows[0];
            long late = GlDb.L(lr[0]);
            l.Add(new Item { code = "LATE", title = "Lines were recorded within " + grace + " days of the period end", status = late == 0 ? "pass" : (p.To.AddDays(grace) > DateTime.Today ? "info" : "fail"), count = late, amount = GlDb.M(lr[1]),
                detail = late == 0 ? "" : GlFmt.Plural(late, "line was", "lines were") + " recorded later than that. Figures reported for the period may have changed.", fix = new GlFix("report", "Audit trail", GlReports.ReportLink("R16")) });

            // 8 Postings after the last sign-off of this period
            DataRow so = GlDb.Table(c, null, "SELECT created_at FROM gl_period_signoff WHERE period_key=@k AND action='SIGNED_OFF' ORDER BY id DESC LIMIT 1", "@k", p.Key).Rows.Cast<DataRow>().FirstOrDefault();
            if (so != null)
            {
                DateTime at = GlDb.D(so[0]).Value;
                long after = GlDb.L(GlDb.Scalar(c, null, "SELECT COUNT(*) FROM fin_ledger WHERE transactionDate BETWEEN @f AND @t AND timeLog > @a", "@f", p.From, "@t", p.To, "@a", at));
                l.Add(new Item { code = "AFTER", title = "Nothing was posted into the period after it was last signed off", status = after == 0 ? "pass" : "fail", count = after,
                    detail = after == 0 ? "Signed off " + GlFmt.When(at) + "." : GlFmt.Plural(after, "line was", "lines were") + " posted after the sign-off on " + GlFmt.When(at) + ".", fix = new GlFix("screen", "Finance Warnings", "AccountsWarnings.aspx?rule=W08") });
            }

            // Year only: income and expenditure closed to retained earnings
            if (p.Kind == "YEAR")
            {
                long ie = 0; int e = GlCalc.DayInt(p.To);
                foreach (GlAgg g in s.Agg) if (g.Day <= e) { GlAcct a = s.Accounts[g.Key]; if ((a.Kind == "CHART" || a.Kind == "PROVISIONAL") && (a.Category == "Income" || a.Category == "Expense")) ie += g.Dr - g.Cr; }
                l.Add(new Item { code = "CLOSE", title = "Income and expenditure were closed to retained earnings at the year end", status = ie == 0 ? "pass" : "fail", amount = Math.Abs(ie),
                    detail = ie == 0 ? "" : "Income and expense accounts hold a net " + (ie < 0 ? "surplus" : "deficit") + " of " + GlFmt.Money(Math.Abs(ie)) + " at " + GlFmt.Date(p.To) + ". Prepare the closing entry below.", fix = null });
                long drafts = GlDb.L(GlDb.Scalar(c, null, "SELECT COUNT(*) FROM gl_adjustment WHERE purpose='Year-end closing entry' AND entry_date=@d AND status IN ('DRAFT','PENDING','POSTED')", "@d", p.To));
                if (drafts > 0) l.Add(new Item { code = "CLOSE_DRAFT", title = "A closing entry for this year exists", status = "info", count = drafts, detail = "See Adjusting entries.", fix = new GlFix("screen", "Adjusting entries", "AccountsAdjustments.aspx") });
            }
        }
        return l;
    }

    public static List<object> History(MySqlConnection c, string key)
    {
        return GlDb.Table(c, null, "SELECT action, IFNULL(note,''), actor, IFNULL(actor_role,''), created_at, IFNULL(checklist_json,'') FROM gl_period_signoff WHERE period_key=@k ORDER BY id DESC", "@k", key).Rows.Cast<DataRow>()
            .Select(r =>
            {
                int fails = 0; try { fails = GlDb.S(r[5]).Split(new[] { "\"status\":\"fail\"" }, StringSplitOptions.None).Length - 1; } catch { }
                return (object)new { action = GlDb.S(r[0]), note = GlDb.S(r[1]), by = GlDb.S(r[2]), role = GlDb.S(r[3]), when = GlFmt.When(r[4]), fails };
            }).ToList();
    }

    /// <summary>Records a review, a sign-off or a reopening, with the checklist computed now (never taken from the browser).</summary>
    public static void Record(string key, string action, string note)
    {
        if (action != "REVIEWED" && action != "SIGNED_OFF" && action != "REOPENED") throw new GlRefusal("Choose review, sign off or reopen.");
        note = (note ?? "").Trim();
        Period p; using (var c = GlDb.Read()) p = Resolve(c, key);
        if (p.From > DateTime.Today) throw new GlRefusal("This period has not started.");
        var items = Checklist(p);
        int fails = items.Count(i => i.status == "fail");
        string last;
        using (var c = GlDb.Read()) last = GlDb.S(GlDb.Scalar(c, null, "SELECT action FROM gl_period_signoff WHERE period_key=@k ORDER BY id DESC LIMIT 1", "@k", key));
        if (action == "SIGNED_OFF" && last == "SIGNED_OFF") throw new GlRefusal("This period is already signed off. Reopen it first.");
        if (action == "REOPENED" && last != "SIGNED_OFF") throw new GlRefusal("Only a signed-off period can be reopened.");
        if (action == "SIGNED_OFF" && p.To >= DateTime.Today) throw new GlRefusal("A period can be signed off only after it has ended.");
        if (action == "SIGNED_OFF" && fails > 0 && note.Length < 30)
            throw new GlRefusal(GlFmt.Plural(fails, "check fails", "checks fail") + ". To sign off anyway, explain in at least 30 characters why the figures can be accepted.");
        if (action != "REVIEWED" && note.Length < 10) throw new GlRefusal("Write a note of at least 10 characters.");
        using (var w = GlDb.Write())
        using (var tx = w.BeginTransaction())
        {
            long id = GlDb.Insert(w, tx, "INSERT INTO gl_period_signoff (period_key, period_kind, action, checklist_json, note, actor, actor_role, created_at) VALUES (@k,@kind,@a,@j,@n,@u,@r,NOW())",
                "@k", key, "@kind", p.Kind, "@a", action, "@j", GlAudit.Cut(FaJson.Ser(items), 60000), "@n", note == "" ? (object)DBNull.Value : GlAudit.Cut(note, 2000),
                "@u", GlAudit.Cut(GlAccess.User(), 100), "@r", GlAccess.Role());
            GlAudit.Write(w, tx, "PERIOD", key, action, last == "" ? null : new { last }, new { action, fails, checks = items.Count }, note);
            tx.Commit();
        }
    }
}
