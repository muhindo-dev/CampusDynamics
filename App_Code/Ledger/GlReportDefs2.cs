using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger reports R08-R20: spending, receivables and payables,
//  requisitions, periods, movement, audit trail, reconciliations,
//  unbalanced vouchers, duplicates and the chart of accounts.
// =====================================================================

public static partial class GlReports
{
    // ── R08 Budget against actual ───────────────────────────────────

    private static GlReportDef R08()
    {
        var d = new GlReportDef { Code = "R08", Group = "Spending", Title = "Budget against Actual", Description = "Budget, actual spending and variance for each expense account in a financial year.", Noun = "account" };
        d.Params.Add(PYear());
        d.Params.Add(new GlParam("subcategory", "Sub-category", "select") { Default = "" });
        d.Defaults = (x, ps) =>
        {
            var p = ps.First(q => q.Key == "subcategory");
            p.Options = new List<object> { new { v = "", t = "All expense sub-categories" } };
            foreach (string s in x.S.Accounts.Values.Where(a => a.Category == "Expense" && a.Kind == "CHART").Select(a => a.SubCategory ?? "").Distinct().OrderBy(s => s)) p.Options.Add(new { v = s, t = s == "" ? "(none)" : s });
        };
        d.Run = x =>
        {
            string yv = x.Str("year");
            GlCalc.FinYear y = null;
            if (yv.Contains("|")) { DateTime a; if (DateTime.TryParseExact(yv.Split('|')[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out a)) y = GlCalc.YearOf(x.Years, a); }
            if (y == null) y = GlCalc.YearOf(x.Years, DateTime.Today) ?? x.Years.LastOrDefault();
            if (y == null) throw new GlRefusal("Choose a financial year.");
            string sub = x.Str("subcategory");
            var budget = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow q in GlDb.Table(x.C, null, "SELECT TRIM(item_code), SUM(planned_amount) FROM fin_budget WHERE budget_year IN (@l, @i) GROUP BY TRIM(item_code)", "@l", y.Label, "@i", y.Id.ToString(CultureInfo.InvariantCulture)).Rows)
                budget[GlDb.S(q[0])] = GlDb.M(q[1]);
            var mv = GlCalc.Movement(x.S, y.Start, y.End);
            var r = new GlResult { Subtitle = y.Label + " (" + y.DateLabel + ")", Noun = "account", NounPlural = "accounts", Basis = "Actual is the movement (debit less credit) on each expense account in the year. Budget is fin_budget for the year, matched by account code." };
            r.Cols.Add(new GlCol("code", "Code", "code", 60)); r.Cols.Add(new GlCol("name", "Account", "text", 200)); r.Cols.Add(new GlCol("sub", "Sub-category", "text", 120));
            r.Cols.Add(new GlCol("bud", "Budget", "money", 85)); r.Cols.Add(new GlCol("act", "Actual", "money", 85)); r.Cols.Add(new GlCol("var", "Variance", "money", 85)); r.Cols.Add(new GlCol("pct", "Used", "pct", 50).NoSum());
            int noBudget = 0; decimal noBudgetAmt = 0;
            foreach (GlAcct a in x.S.Accounts.Values.Where(a => a.Category == "Expense" && (a.Kind == "CHART" || a.Kind == "PROVISIONAL") && (sub == "" || a.SubCategory == sub)).OrderBy(a => a.SubCategory).ThenBy(a => a.Code))
            {
                long act; mv.TryGetValue(a.Key, out act); decimal b; bool hasB = budget.TryGetValue(a.Code, out b);
                if (act == 0 && !hasB) continue;
                if (!hasB && act != 0) { noBudget++; noBudgetAmt += act; }
                r.Add(AccountLink(a.Key, y.Start, y.End), "", a.Code, a.Name, a.SubCategory, hasB ? (object)b : null, (decimal)act, hasB ? (object)(b - act) : null, hasB && b != 0 ? (object)Math.Round(act * 100m / b, 1) : null);
            }
            if (budget.Count == 0) r.Checks.Add(Fail("NO_BUDGET", "No budget has been recorded for " + y.Label, 0, 0, "fin_budget holds no lines for the year, so no variance can be shown. Loading budgets is planned for phase 2 (open question Q9).", null));
            if (noBudget > 0) r.Checks.Add(Fail("NO_LINE", GlFmt.Plural(noBudget, "account has", "accounts have") + " spending and no budget line", noBudgetAmt, noBudget, "See Finance Warnings W17.", new GlFix("screen", "Finance Warnings", "AccountsWarnings.aspx?rule=W17")));
            else if (budget.Count > 0) r.Checks.Add(Pass("NO_LINE", "Every account with spending has a budget line"));
            return r;
        };
        return d;
    }

    // ── R09 Expenditure analysis ────────────────────────────────────

    private static GlReportDef R09()
    {
        var d = new GlReportDef { Code = "R09", Group = "Spending", Title = "Expenditure Analysis", Description = "Spending in a period by account, sub-category, month or supplier.", Noun = "group" };
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Params.Add(new GlParam("by", "Analyse by", "select") { Default = "subcategory", Options = new List<object> { new { v = "subcategory", t = "Sub-category" }, new { v = "account", t = "Account" }, new { v = "month", t = "Month" }, new { v = "supplier", t = "Supplier" } } });
        d.Run = x =>
        {
            DateTime from, to; x.Range(out from, out to);
            string by = x.Str("by"); if (by == "") by = "subcategory";
            var exp = new HashSet<string>(x.S.Accounts.Values.Where(a => a.Category == "Expense" && (a.Kind == "CHART" || a.Kind == "PROVISIONAL")).Select(a => a.Key), StringComparer.OrdinalIgnoreCase);
            var groups = new Dictionary<string, decimal[]>(); var links = new Dictionary<string, string>();
            int f = GlCalc.DayInt(from), t = GlCalc.DayInt(to);
            decimal total = 0;
            if (by != "supplier")
            {
                foreach (GlAgg g in x.S.Agg)
                {
                    if (g.Day < f || g.Day > t || !exp.Contains(g.Key)) continue;
                    GlAcct a = x.S.Accounts[g.Key]; string k;
                    if (by == "account") { k = a.Code + " " + a.Name; links[k] = AccountLink(a.Key, from, to); }
                    else if (by == "month") { DateTime m = GlCalc.FromDayInt(g.Day); k = m.ToString("yyyy-MM", CultureInfo.InvariantCulture); }
                    else k = string.IsNullOrEmpty(a.SubCategory) ? "(none)" : a.SubCategory;
                    decimal[] v; if (!groups.TryGetValue(k, out v)) { v = new decimal[2]; groups[k] = v; }
                    v[0] += g.Dr - g.Cr; v[1] += g.N; total += g.Dr - g.Cr;
                }
            }
            else
            {
                var names = GlDb.Table(x.C, null, "SELECT TRIM(SupplierCode), SupplierName FROM inv_supplierdetails").Rows.Cast<DataRow>().GroupBy(q => GlDb.S(q[0])).ToDictionary(g => g.Key, g => GlDb.S(g.First()[1]));
                var prm = new List<object> { "@f", from, "@t", to }; var en = new List<string>(); int i = 0;
                foreach (string k in exp) { en.Add("@e" + i); prm.Add("@e" + i); prm.Add(k); i++; }
                foreach (DataRow q in GlDb.Table(x.C, null,
                    "SELECT IFNULL(sp.accountcode,'') sup, SUM(" + GlCalc.NetExpr + "), COUNT(*) FROM fin_ledger l " +
                    "LEFT JOIN (SELECT voucherNo, transactionDate, MIN(accountcode) accountcode FROM fin_ledger WHERE account_type='Supplier' AND transactionDate BETWEEN @f AND @t AND voucherNo > 0 GROUP BY voucherNo, transactionDate) sp " +
                    "ON sp.voucherNo = l.voucherNo AND sp.transactionDate = l.transactionDate " +
                    "WHERE l.transactionDate BETWEEN @f AND @t AND l.accountcode IN (" + string.Join(",", en) + ") GROUP BY sup", prm.ToArray()).Rows)
                {
                    string code = GlDb.S(q[0]); string nm;
                    string k = code == "" ? "(no supplier on the voucher)" : code + " " + (names.TryGetValue(code, out nm) ? nm : "(name not on file)");
                    if (code != "") links[k] = AccountLink("SUB:Supplier", from, to) + "&m=" + Uri.EscapeDataString(code);
                    groups[k] = new[] { GlDb.M(q[1]), GlDb.M(q[2]) }; total += GlDb.M(q[1]);
                }
            }
            var r = new GlResult { Subtitle = x.RangeText(from, to), Noun = "group", NounPlural = "groups", Basis = "Movement (debit less credit) on expense accounts in the period." + (by == "supplier" ? " The supplier is the supplier line in the same voucher on the same day, if any." : "") };
            r.Cols.Add(new GlCol("g", by == "account" ? "Account" : by == "month" ? "Month" : by == "supplier" ? "Supplier" : "Sub-category", "text", 240));
            r.Cols.Add(new GlCol("n", "Lines", "count", 50).Summed()); r.Cols.Add(new GlCol("amt", "Amount", "money", 100)); r.Cols.Add(new GlCol("share", "Share", "pct", 50).NoSum());
            foreach (var kv in (by == "month" ? groups.OrderBy(kv => kv.Key) : groups.OrderByDescending(kv => kv.Value[0])))
            {
                string link; links.TryGetValue(kv.Key, out link);
                r.Add(link, "", kv.Key, (long)kv.Value[1], kv.Value[0], total == 0 ? 0m : Math.Round(kv.Value[0] * 100m / total, 1));
            }
            decimal ie = GlCalc.IncomeExpenditure(x.S, from, to).Where(kv => kv.Key.Category == "Expense").Sum(kv => (decimal)kv.Value);
            if (ie == total) r.Checks.Add(Pass("AGREES", "Total agrees with expenditure in the Income and Expenditure statement (" + GlFmt.Money(total) + ")"));
            else r.Checks.Add(Fail("AGREES", "Total differs from the Income and Expenditure statement", Math.Abs(ie - total), 0, "Income and Expenditure shows " + GlFmt.Money(ie) + ". Tell MIS.", null));
            r.Checks.Add(Info("NO_DEPT", "Department and campus are not recorded on ledger lines", "Spending can be analysed by account, month and supplier only. Recording a cost centre needs the posting screens to capture it (plan, phase 2)."));
            return r;
        };
        return d;
    }

    // ── R10 Payables ageing ─────────────────────────────────────────

    private static GlReportDef R10()
    {
        var d = new GlReportDef { Code = "R10", Group = "Receivables and payables", Title = "Payables Ageing", Description = "What is owed to each supplier as at a date, by age.", Noun = "supplier" };
        d.Params.Add(new GlParam("to", "As at", "date") { Role = "asat", Required = true });
        d.Params.Add(new GlParam("owing", "Only suppliers owed money", "bool") { Default = "1" });
        d.Run = x =>
        {
            DateTime to = x.DateOr("to", DateTime.Today); bool owing = x.Bool("owing");
            var names = GlDb.Table(x.C, null, "SELECT TRIM(SupplierCode), SupplierName FROM inv_supplierdetails").Rows.Cast<DataRow>().GroupBy(q => GlDb.S(q[0])).ToDictionary(g => g.Key, g => GlDb.S(g.First()[1]));
            var credits = new Dictionary<string, List<KeyValuePair<DateTime, decimal>>>(); var debits = new Dictionary<string, decimal>();
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT l.accountcode, l.transactionDate, l.transactionType, CAST(l.transaction_amount AS SIGNED) FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode " +
                "WHERE s.AccountCode IS NULL AND l.account_type = 'Supplier' AND l.transactionDate <= @t ORDER BY l.transactionDate, l.TID", "@t", to).Rows)
            {
                string c = GlDb.S(q[0]);
                if (GlDb.S(q[2]) == "CR") { if (!credits.ContainsKey(c)) credits[c] = new List<KeyValuePair<DateTime, decimal>>(); credits[c].Add(new KeyValuePair<DateTime, decimal>(GlDb.D(q[1]).Value, GlDb.M(q[3]))); }
                else { decimal v; debits.TryGetValue(c, out v); debits[c] = v + GlDb.M(q[3]); }
            }
            var r = new GlResult { Subtitle = "As at " + GlFmt.Date(to), Noun = "supplier", NounPlural = "suppliers", Basis = "Supplier lines in fin_ledger. Payments are applied to the oldest invoices first; what remains is aged from the invoice date." };
            r.Cols.Add(new GlCol("code", "Code", "code", 45)); r.Cols.Add(new GlCol("name", "Supplier", "text", 200));
            r.Cols.Add(new GlCol("b0", "0 to 30 days", "money", 80)); r.Cols.Add(new GlCol("b1", "31 to 60", "money", 80)); r.Cols.Add(new GlCol("b2", "61 to 90", "money", 80)); r.Cols.Add(new GlCol("b3", "Over 90", "money", 80));
            r.Cols.Add(new GlCol("tot", "Owed", "money", 90));
            int leftOut = 0; decimal leftAmt = 0;
            foreach (string c in credits.Keys.Union(debits.Keys).OrderBy(c => c))
            {
                List<KeyValuePair<DateTime, decimal>> inv; credits.TryGetValue(c, out inv); inv = inv ?? new List<KeyValuePair<DateTime, decimal>>();
                decimal paid; debits.TryGetValue(c, out paid);
                var b = new decimal[4]; decimal owed = inv.Sum(i => i.Value) - paid;
                decimal left = paid;
                foreach (var i in inv)
                {
                    decimal rem = i.Value; decimal use = Math.Min(rem, left); rem -= use; left -= use;
                    if (rem <= 0) continue;
                    int age = (int)(to - i.Key).TotalDays;
                    b[age <= 30 ? 0 : age <= 60 ? 1 : age <= 90 ? 2 : 3] += rem;
                }
                if (owing && owed <= 0) { if (owed < 0) { leftOut++; leftAmt += owed; } continue; }
                if (owed == 0 && inv.Count == 0) continue;
                string nm; names.TryGetValue(c, out nm);
                r.Add(AccountLink("SUB:Supplier", null, to) + "&m=" + Uri.EscapeDataString(c), "", c, nm ?? "(name not on file)", b[0], b[1], b[2], b[3], owed);
            }
            var pair = GlCalc.ControlPairs(x.S).FirstOrDefault(p => p.Sub.Key == "SUB:Supplier");
            if (pair != null)
            {
                long ctl = -GlCalc.Balance(x.S, pair.Control.Key, to), sub = -GlCalc.Balance(x.S, "SUB:Supplier", to);
                if (ctl == sub) r.Checks.Add(Pass("CONTROL", "Supplier lines agree with " + pair.Control.Code));
                else r.Checks.Add(Fail("CONTROL", "Supplier lines differ from " + pair.Control.Code + " " + pair.Control.Name, Math.Abs(sub - ctl), 1,
                    "Supplier lines owe " + GlFmt.Money(sub) + "; the control account shows " + GlFmt.Money(ctl) + ". Supplier postings do not pass through the control account.", new GlFix("report", "Control account reconciliations", ReportLink("R17", "to", GlFmt.Iso(to)))));
            }
            if (!owing) r.Checks.Add(Info("ALL", "Suppliers in credit are included", "A negative amount owed means the supplier was paid more than invoiced on the ledger."));
            else if (leftOut > 0) r.Checks.Add(new GlCheck { code = "LEFTOUT", title = GlFmt.Plural(leftOut, "supplier paid more than invoiced is", "suppliers paid more than invoiced are") + " left out of this list", status = "info", amount = leftAmt, count = leftOut,
                cause = "Together they reduce what the supplier ledger owes from the listed " + GlFmt.Money(r.Rows.Sum(q => (decimal)q[6])) + " to " + GlFmt.Money(r.Rows.Sum(q => (decimal)q[6]) + leftAmt) + ". Untick the option to list them." });
            return r;
        };
        return d;
    }

    // ── R11 Receivables ageing and student fees position ───────────

    private static GlReportDef R11()
    {
        var d = new GlReportDef { Code = "R11", Group = "Receivables and payables", Title = "Receivables Ageing and Student Fees Position", Description = "What each student owes on the canonical balance, how old it is, and how the ledger compares.", Noun = "student" };
        d.Params.Add(new GlParam("status", "Students", "select") { Default = "owing", Options = new List<object> { new { v = "owing", t = "Owing" }, new { v = "credit", t = "In credit" }, new { v = "all", t = "All" } } });
        d.Params.Add(new GlParam("programme", "Programme", "text") { Help = "Part of the programme code or name." });
        d.Run = x =>
        {
            string st = x.Str("status"), prog = x.Str("programme").ToLowerInvariant();
            var stu = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT s.regno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))), IFNULL(p.progcode,''), IFNULL(p.progname,'') FROM campus_dynamics.acad_student s LEFT JOIN campus_dynamics.acad_programme p ON p.progcode = s.progid").Rows)
                stu[GlDb.S(q[0])] = new[] { GlDb.S(q[1]), GlDb.S(q[2]), GlDb.S(q[3]) };
            var ledger = GlDb.Table(x.C, null, "SELECT accountcode, SUM(" + GlCalc.NetBare + ") FROM fin_ledger WHERE account_type IN ('Student','-') GROUP BY accountcode").Rows.Cast<DataRow>()
                .ToDictionary(q => GlDb.S(q[0]), q => GlDb.M(q[1]), StringComparer.OrdinalIgnoreCase);
            var bills = new Dictionary<string, List<KeyValuePair<DateTime, decimal>>>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow q in GlDb.Table(x.C, null, "SELECT regno, trans_date, amount FROM fin_studentfeestracking WHERE trans_type='Bill' AND post_status='Posted' ORDER BY regno, trans_date DESC").Rows)
            {
                string k = GlDb.S(q[0]); if (!bills.ContainsKey(k)) bills[k] = new List<KeyValuePair<DateTime, decimal>>();
                DateTime? bd = GlDb.D(q[1]); if (bd.HasValue) bills[k].Add(new KeyValuePair<DateTime, decimal>(bd.Value, GlDb.M(q[2])));
            }
            DateTime cacheAt = GlDb.D(GlDb.Scalar(x.C, null, "SELECT MAX(updated_at) FROM fin_student_balance_cache")) ?? DateTime.Now;
            var r = new GlResult { Subtitle = "Canonical balances refreshed " + GlFmt.When(cacheAt), Noun = "student", NounPlural = "students" };
            r.Basis = "Owed is the canonical student balance (fin_student_balance_cache: billed less paid, de-duplicated across both fee stores), the figure on student statements. Age is the date of the oldest bill still unpaid, taking payments against the oldest bills first. The ledger column is shown for comparison and is never added to it.";
            r.Cols.Add(new GlCol("reg", "Registration number", "code", 90)); r.Cols.Add(new GlCol("name", "Student", "text", 150)); r.Cols.Add(new GlCol("prog", "Programme", "code", 60));
            r.Cols.Add(new GlCol("billed", "Billed", "money", 80)); r.Cols.Add(new GlCol("paid", "Paid", "money", 80)); r.Cols.Add(new GlCol("owed", "Owed", "money", 80));
            r.Cols.Add(new GlCol("since", "Oldest unpaid bill", "date", 65)); r.Cols.Add(new GlCol("age", "Age", "text", 55));
            r.Cols.Add(new GlCol("ledger", "Ledger balance", "money", 80)); r.Cols.Add(new GlCol("diff", "Ledger less owed", "money", 80));
            decimal tCanon = 0, tLedger = 0; int differ = 0, skipped = 0; decimal skippedAmt = 0;
            foreach (DataRow q in GlDb.Table(x.C, null, "SELECT regno, total_billed, total_paid, total_balance FROM fin_student_balance_cache").Rows)
            {
                string k = GlDb.S(q[0]); decimal owed = -GlDb.M(q[3]);
                if (st == "owing" && owed <= 0) { skipped++; skippedAmt += owed; continue; }
                if (st == "credit" && owed >= 0) { skipped++; skippedAmt += owed; continue; }
                string[] s; stu.TryGetValue(k, out s); s = s ?? new[] { "(not in student records)", "", "" };
                if (prog != "" && !(s[1] + " " + s[2]).ToLowerInvariant().Contains(prog)) continue;
                DateTime? since = null;
                if (owed > 0)
                {
                    List<KeyValuePair<DateTime, decimal>> bl; decimal left = owed;
                    if (bills.TryGetValue(k, out bl)) foreach (var b in bl) { since = b.Key; left -= b.Value; if (left <= 0) break; }
                }
                string age = "";
                if (since.HasValue) { int days = (int)(DateTime.Today - since.Value).TotalDays; age = days <= 30 ? "0 to 30" : days <= 90 ? "31 to 90" : days <= 180 ? "91 to 180" : days <= 365 ? "181 to 365" : "Over a year"; }
                decimal lb; ledger.TryGetValue(k, out lb);
                if (Math.Abs(lb - owed) >= 1000) differ++;
                tCanon += owed; tLedger += lb;
                r.Add(AccountLink("SUB:STUDENTS", null, null) + "&m=" + Uri.EscapeDataString(k), "", k, s[0], s[1], GlDb.M(q[1]), GlDb.M(q[2]), owed, since, age, lb, lb - owed);
            }
            decimal tracking = GlDb.M(GlDb.Scalar(x.C, null, "SELECT IFNULL(SUM(IF(trans_type='Bill',amount,-amount)),0) FROM fin_studentfeestracking WHERE post_status='Posted'"));
            decimal allLedger = GlDb.M(GlDb.Scalar(x.C, null, "SELECT IFNULL(SUM(" + GlCalc.NetBare + "),0) FROM fin_ledger WHERE account_type IN ('Student','-')"));
            decimal allCanon = -GlDb.M(GlDb.Scalar(x.C, null, "SELECT IFNULL(SUM(total_balance),0) FROM fin_student_balance_cache"));
            r.Checks.Add(new GlCheck { code = "BASES", title = "Three figures for what students owe (shown side by side, never added)", status = "info", amount = allCanon,
                cause = "Canonical " + GlFmt.Money(allCanon) + "; ledger student lines " + GlFmt.Money(allLedger) + "; fee tracking " + GlFmt.Money(tracking) + ". The canonical figure is the one used on student statements (open question Q3).",
                fix = new GlFix("report", "Control account reconciliations", ReportLink("R17")) });
            if (skipped > 0 && prog == "")
                r.Checks.Add(new GlCheck { code = "LEFTOUT", title = GlFmt.Plural(skipped, "student is", "students are") + " left out by the Students filter", status = "info", amount = skippedAmt, count = skipped,
                    cause = "The listed amount owed (" + GlFmt.Money(tCanon) + ") and the students left out (" + GlFmt.Money(skippedAmt) + ") make up the canonical total of " + GlFmt.Money(tCanon + skippedAmt) + "." });
            if (differ == 0) r.Checks.Add(Pass("DIFFER", "The ledger agrees with the canonical balance for every student listed"));
            else r.Checks.Add(Fail("DIFFER", GlFmt.Plural(differ, "student listed differs", "students listed differ") + " between the ledger and the canonical balance", Math.Abs(tLedger - tCanon), differ,
                "Bills or payments recorded in one fee store and not mirrored in the other. See Finance Warnings W10.", new GlFix("screen", "Finance Warnings", "AccountsWarnings.aspx?rule=W10")));
            return r;
        };
        return d;
    }

    // ── R12 Requisition to payment trace ────────────────────────────

    private static GlReportDef R12()
    {
        var d = new GlReportDef { Code = "R12", Group = "Spending", Title = "Requisition to Payment Trace", Description = "Each requisition through approval to payment, and whether a ledger entry exists for it.", Noun = "requisition" };
        d.Params.Add(new GlParam("status", "Status", "select") { Default = "" });
        d.Defaults = (x, ps) =>
        {
            var p = ps.First(q => q.Key == "status");
            p.Options = new List<object> { new { v = "", t = "All statuses" } };
            foreach (DataRow q in GlDb.Table(x.C, null, "SELECT status, COUNT(*) FROM campus_dynamics.sys_requisitions GROUP BY status ORDER BY status").Rows)
                p.Options.Add(new { v = GlDb.S(q[0]), t = GlDb.S(q[0]) + " (" + GlDb.S(q[1]) + ")" });
        };
        d.Run = x =>
        {
            string st = x.Str("status");
            var r = new GlResult { Noun = "requisition", NounPlural = "requisitions", Basis = "campus_dynamics.sys_requisitions. A ledger entry is looked for by the requisition's ledger reference in fin_ledger.RefNo and folio." };
            r.Cols.Add(new GlCol("no", "Requisition", "code", 80)); r.Cols.Add(new GlCol("title", "Title", "text", 150)); r.Cols.Add(new GlCol("dept", "Department", "text", 90));
            r.Cols.Add(new GlCol("amt", "Amount", "money", 80)); r.Cols.Add(new GlCol("st", "Status", "text", 80)); r.Cols.Add(new GlCol("sub", "Submitted", "date", 60));
            r.Cols.Add(new GlCol("sup", "Supervisor", "date", 60)); r.Cols.Add(new GlCol("bur", "Bursar", "date", 60)); r.Cols.Add(new GlCol("vc", "VC", "date", 60)); r.Cols.Add(new GlCol("fin", "Finance", "date", 60));
            r.Cols.Add(new GlCol("pay", "Payment reference", "text", 80)); r.Cols.Add(new GlCol("led", "Ledger lines", "count", 45).NoSum());
            int approvedUnpaid = 0, postedNone = 0; decimal au = 0, pn = 0;
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT ID, req_number, title, department, total_amount, status, submitted_at, supervisor_at, bursar_at, vc_at, finance_at, IFNULL(payment_ref,''), IFNULL(ledger_ref,''), IFNULL(ledger_posted,0) FROM campus_dynamics.sys_requisitions" +
                (st == "" ? "" : " WHERE status = @s") + " ORDER BY submitted_at DESC", "@s", st).Rows)
            {
                string lref = GlDb.S(q[12]);
                long n = lref == "" ? 0 : GlDb.L(GlDb.Scalar(x.C, null, "SELECT COUNT(*) FROM fin_ledger WHERE RefNo = @r OR folio = @r", "@r", lref));
                string status = GlDb.S(q[5]).ToUpperInvariant();
                if (GlWarnings.ReqApproved.Contains(status) && GlDb.S(q[11]) == "") { approvedUnpaid++; au += GlDb.M(q[4]); }
                if ((GlDb.I(q[13]) == 1 || status == "LEDGER_POSTED") && n == 0) { postedNone++; pn += GlDb.M(q[4]); }
                r.Add("RequisitionDetail.aspx?id=" + GlDb.S(q[0]), "", GlDb.S(q[1]), GlDb.S(q[2]), GlDb.S(q[3]), GlDb.M(q[4]), GlDb.S(q[5]), GlDb.D(q[6]), GlDb.D(q[7]), GlDb.D(q[8]), GlDb.D(q[9]), GlDb.D(q[10]), GlDb.S(q[11]), n);
            }
            r.Checks.Add(approvedUnpaid == 0 ? Pass("UNPAID", "No approved requisition is waiting for payment") : Fail("UNPAID", GlFmt.Plural(approvedUnpaid, "requisition is", "requisitions are") + " approved but not paid", au, approvedUnpaid, "Approved spending with no payment reference.", null));
            r.Checks.Add(postedNone == 0 ? Pass("POSTED", "No requisition is marked posted without ledger lines") : Fail("POSTED", GlFmt.Plural(postedNone, "requisition is", "requisitions are") + " marked posted with no ledger lines", pn, postedNone, "The requisition screens set a flag and an invented reference; nothing reaches the ledger.", null));
            r.Checks.Add(new GlCheck { code = "NOREQ", title = "Payments made without a requisition", status = "info", cause = "Every expense payment in the ledger was made through classic journals and vouchers. See the payment voucher register and Finance Warnings W13.", fix = new GlFix("report", "Payment voucher register", ReportLink("R13")) });
            return r;
        };
        return d;
    }

    // ── R13 Payment voucher register ────────────────────────────────

    private static GlReportDef R13()
    {
        var d = new GlReportDef { Code = "R13", Group = "Spending", Title = "Payment Voucher Register", Description = "Bank and cash payments for expenses in a period, with the accounts charged.", Noun = "payment" };
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Params.Add(new GlParam("bank", "Paid from", "select") { Default = "" });
        d.Defaults = (x, ps) =>
        {
            var p = ps.First(q => q.Key == "bank");
            p.Options = new List<object> { new { v = "", t = "Any bank or cash account" } };
            foreach (GlAcct a in GlCalc.CashAccounts(x.S)) p.Options.Add(new { v = a.Code, t = a.Code + " " + a.Name });
        };
        d.Run = x =>
        {
            DateTime from, to; x.Range(out from, out to);
            var cash = GlCalc.CashAccounts(x.S).Select(a => a.Code).ToList();
            string bank = x.Str("bank"); if (bank != "") cash = cash.Where(c => c == bank).ToList();
            var exp = x.S.Accounts.Values.Where(a => (a.Kind == "CHART" || a.Kind == "PROVISIONAL") && a.Category == "Expense").Select(a => a.Code).ToList();
            var r = new GlResult { Subtitle = x.RangeText(from, to), Noun = "payment", NounPlural = "payments", Basis = "Credit lines on bank and cash accounts in vouchers that debit an expense account on the same day." };
            r.Cols.Add(new GlCol("d", "Date", "date", 60)); r.Cols.Add(new GlCol("v", "Voucher", "code", 55)); r.Cols.Add(new GlCol("bank", "Paid from", "code", 60));
            r.Cols.Add(new GlCol("exp", "Charged to", "text", 150)); r.Cols.Add(new GlCol("p", "Particulars", "text", 200)); r.Cols.Add(new GlCol("amt", "Amount", "money", 85)); r.Cols.Add(new GlCol("req", "Requisition", "text", 70));
            if (cash.Count == 0 || exp.Count == 0) return r;
            var prm = new List<object> { "@f", from, "@t", to }; var cn = new List<string>(); var en = new List<string>();
            for (int i = 0; i < cash.Count; i++) { cn.Add("@c" + i); prm.Add("@c" + i); prm.Add(cash[i]); }
            for (int i = 0; i < exp.Count; i++) { en.Add("@e" + i); prm.Add("@e" + i); prm.Add(exp[i]); }
            var refs = new HashSet<string>(GlDb.Table(x.C, null, "SELECT ledger_ref FROM campus_dynamics.sys_requisitions WHERE IFNULL(ledger_ref,'') <> ''").Rows.Cast<DataRow>().Select(q => GlDb.S(q[0])), StringComparer.OrdinalIgnoreCase);
            int noReq = 0; decimal noReqAmt = 0;
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT b.transactionDate, b.voucherNo, b.accountcode, e.codes, b.particulars, CAST(b.transaction_amount AS SIGNED), IFNULL(b.RefNo,'') FROM fin_ledger b " +
                "JOIN (SELECT voucherNo, transactionDate, GROUP_CONCAT(DISTINCT accountcode ORDER BY accountcode SEPARATOR ', ') codes FROM fin_ledger WHERE transactionType='DR' AND accountcode IN (" + string.Join(",", en) + ") " +
                "AND transactionDate BETWEEN @f AND @t AND voucherNo > 0 GROUP BY voucherNo, transactionDate) e ON e.voucherNo = b.voucherNo AND e.transactionDate = b.transactionDate " +
                "WHERE b.transactionType='CR' AND b.accountcode IN (" + string.Join(",", cn) + ") AND b.transactionDate BETWEEN @f AND @t ORDER BY b.transactionDate, b.voucherNo", prm.ToArray()).Rows)
            {
                bool hasReq = refs.Contains(GlDb.S(q[6]));
                if (!hasReq) { noReq++; noReqAmt += GlDb.M(q[5]); }
                string codes = GlDb.S(q[3]);
                string first = codes.Split(',')[0].Trim(); GlAcct ea;
                string charged = x.S.Accounts.TryGetValue(first, out ea) ? codes + " " + ea.Name : codes;
                r.Add(VoucherLink(GlDb.L(q[1])), "", GlDb.D(q[0]), GlDb.S(q[1]), GlDb.S(q[2]), charged, GlDb.S(q[4]), GlDb.M(q[5]), hasReq ? GlDb.S(q[6]) : "None");
            }
            if (noReq == 0) r.Checks.Add(Pass("REQ", "Every payment listed has a requisition"));
            else r.Checks.Add(Fail("REQ", GlFmt.Plural(noReq, "payment has", "payments have") + " no requisition", noReqAmt, noReq, "Approved spending and paid spending cannot be matched until payments are made from requisitions (plan, phase 2).", new GlFix("report", "Requisition trace", ReportLink("R12"))));
            return r;
        };
        return d;
    }

    // ── R14 Period close summary ────────────────────────────────────

    private static GlReportDef R14()
    {
        var d = new GlReportDef { Code = "R14", Group = "Control and audit", Title = "Period Close Summary", Description = "Each month of a financial year (or each year): lines, debit, credit, difference, unbalanced vouchers and sign-off.", Noun = "period" };
        d.Params.Add(new GlParam("year", "Financial year", "select") { Default = "" });
        d.Defaults = (x, ps) =>
        {
            var p = ps.First(q => q.Key == "year");
            GlCalc.FinYear cur = GlCalc.YearOf(x.Years, DateTime.Today) ?? x.Years.LastOrDefault();
            p.Options = new List<object> { new { v = "all", t = "Every year" } };
            foreach (GlCalc.FinYear y in x.Years.OrderByDescending(y => y.Start)) p.Options.Add(new { v = y.Id.ToString(CultureInfo.InvariantCulture), t = y.Label + " (" + y.DateLabel + ")" });
            p.Default = cur == null ? "all" : cur.Id.ToString(CultureInfo.InvariantCulture);
        };
        d.Run = x =>
        {
            string yv = x.Str("year");
            var periods = new List<Tuple<string, DateTime, DateTime>>();
            GlCalc.FinYear year = x.Years.FirstOrDefault(y => y.Id.ToString(CultureInfo.InvariantCulture) == yv);
            if (year == null)
            {
                if (x.Years.Count > 0 && x.S.MinDay < GlCalc.DayInt(x.Years[0].Start)) periods.Add(Tuple.Create("Before " + x.Years[0].Label, GlCalc.FromDayInt(x.S.MinDay), x.Years[0].Start.AddDays(-1)));
                foreach (GlCalc.FinYear y in x.Years) periods.Add(Tuple.Create(y.Label, y.Start, y.End));
            }
            else for (DateTime m = new DateTime(year.Start.Year, year.Start.Month, 1); m <= year.End; m = m.AddMonths(1))
                    periods.Add(Tuple.Create(m.ToString("MMM yyyy", CultureInfo.InvariantCulture), m < year.Start ? year.Start : m, m.AddMonths(1).AddDays(-1) > year.End ? year.End : m.AddMonths(1).AddDays(-1)));
            long nvd; int nvl;
            var ub = GlCalc.UnbalancedVouchers(null, new DateTime(2099, 12, 31), "WHOLE", out nvd, out nvl);
            var signed = GlDb.Table(x.C, null, "SELECT p.period_key, p.action, p.actor, p.created_at FROM gl_period_signoff p WHERE p.id = (SELECT MAX(q.id) FROM gl_period_signoff q WHERE q.period_key = p.period_key)").Rows.Cast<DataRow>()
                .ToDictionary(q => GlDb.S(q[0]), q => GlDb.S(q[1]) == "SIGNED_OFF" ? "Signed off " + GlFmt.Date(q[3]) + " by " + GlDb.S(q[2]) : GlDb.S(q[1]) == "REVIEWED" ? "Reviewed " + GlFmt.Date(q[3]) : "Reopened " + GlFmt.Date(q[3]));
            var r = new GlResult { Subtitle = year == null ? "Every year" : year.Label + " (" + year.DateLabel + ")", Noun = "period", NounPlural = "periods", Basis = "Whole ledger. Unbalanced vouchers are counted in the period of their first date." };
            r.Cols.Add(new GlCol("p", "Period", "text", 90)); r.Cols.Add(new GlCol("n", "Lines", "count", 55).Summed()); r.Cols.Add(new GlCol("dr", "Debit", "money", 95)); r.Cols.Add(new GlCol("cr", "Credit", "money", 95));
            r.Cols.Add(new GlCol("diff", "Difference", "money", 90)); r.Cols.Add(new GlCol("ub", "Unbalanced vouchers", "count", 60).Summed()); r.Cols.Add(new GlCol("so", "Sign-off", "text", 140));
            foreach (var p in periods)
            {
                GlCalc.Totals t = GlCalc.Total(x.S, p.Item2, p.Item3, "WHOLE");
                int n = ub.Count(v => v.First >= p.Item2 && v.First <= p.Item3);
                GlCalc.FinYear py = year == null ? x.Years.FirstOrDefault(v => v.Label == p.Item1) : null;
                string key = year == null ? (py != null ? "FY:" + GlFmt.Iso(py.Start) : p.Item1) : p.Item2.ToString("yyyy-MM", CultureInfo.InvariantCulture), so;
                r.Add(ReportLink("R01", "from", GlFmt.Iso(p.Item2), "to", GlFmt.Iso(p.Item3)), "", p.Item1, t.Lines, (decimal)t.Dr, (decimal)t.Cr, (decimal)t.Diff, (long)n, signed.TryGetValue(key, out so) ? so : "Not signed off");
            }
            foreach (GlFinding f in GlWarnings.DetectOnly("W06").Where(f => year == null || f.Scope == year.Label))
                r.Checks.Add(Fail("ROLL", f.Title, f.Amount, 1, f.Cause, new GlFix("screen", "Periods and close", "AccountsPeriods.aspx")));
            if (year != null && year.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase) && !r.Checks.Any(c => c.code == "ROLL")) r.Checks.Add(Pass("ROLL", "The year's income and expenditure were closed"));
            if (year != null && !year.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase)) r.Checks.Add(Info("OPEN", year.Label + " is open", "Closing it records a sign-off on the Periods and close screen and prepares the closing entry as a draft adjusting entry."));
            GlCalc.Totals all = GlCalc.Total(x.S, periods.Count > 0 ? (DateTime?)periods[0].Item2 : null, periods.Count > 0 ? periods[periods.Count - 1].Item3 : DateTime.Today, "WHOLE");
            r.Checks.Add(all.Diff == 0 ? Pass("BAL", "Debit equals credit across the periods shown") : Fail("BAL", "Debit and credit differ across the periods shown", all.Diff, ub.Count(v => periods.Count > 0 && v.First >= periods[0].Item2 && v.First <= periods[periods.Count - 1].Item3),
                "The difference is explained by the unbalanced vouchers; see the trial balance cause analysis.", new GlFix("report", "Unbalanced vouchers", ReportLink("R18", "from", periods.Count > 0 ? GlFmt.Iso(periods[0].Item2) : "", "to", periods.Count > 0 ? GlFmt.Iso(periods[periods.Count - 1].Item3) : ""))));
            foreach (GlFinding f in GlWarnings.DetectOnly("W08").Where(f => year == null || f.Scope.EndsWith(year.Label) || f.Scope.StartsWith("SIGNOFF:" + year.Start.Year)))
                r.Checks.Add(Fail("LATE", f.Title, f.Amount, f.Count, f.Cause, new GlFix("report", "Audit trail", ReportLink("R16"))));
            return r;
        };
        return d;
    }

    // ── R15 Account movement over time ──────────────────────────────

    private static GlReportDef R15()
    {
        var d = new GlReportDef { Code = "R15", Group = "Ledgers and vouchers", Title = "Account Movement over Time", Description = "Monthly movement or month-end balance of one account, or of every account in a category.", Noun = "account" };
        d.Params.Add(PAccount(false));
        d.Params.Add(new GlParam("category", "Or a category", "select") { Default = "Expense", Options = CategoryOptions() });
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Params.Add(new GlParam("measure", "Show", "select") { Default = "movement", Options = new List<object> { new { v = "movement", t = "Movement in each month" }, new { v = "balance", t = "Balance at each month end" } } });
        d.Run = x =>
        {
            DateTime from, to; x.Range(out from, out to);
            var months = new List<DateTime>();
            for (DateTime m = new DateTime(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1)) months.Add(m);
            if (months.Count > 24) throw new GlRefusal("Choose a period of 24 months or less.");
            string key = x.Str("account"), cat = x.Str("category"); bool bal = x.Str("measure") == "balance";
            List<GlAcct> accts = key != "" ? new List<GlAcct> { Acct(x, key) } :
                x.S.Accounts.Values.Where(a => (cat == "" || a.Category == cat)).OrderBy(a => a.Kind == "SUBLEDGER" ? 1 : 0).ThenBy(a => a.Code).ToList();
            var r = new GlResult { Subtitle = x.RangeText(from, to), Noun = "account", NounPlural = "accounts", Basis = (bal ? "Balance (debit less credit) at each month end" : "Movement (debit less credit) in each month") + ", whole ledger." };
            r.Cols.Add(new GlCol("code", "Code", "code", 60)); r.Cols.Add(new GlCol("name", "Account", "text", 160));
            foreach (DateTime m in months) r.Cols.Add(new GlCol("m" + m.ToString("yyyyMM"), m.ToString("MMM yy", CultureInfo.InvariantCulture), "money", 62) { Sum = true });
            if (!bal) r.Cols.Add(new GlCol("tot", "Total", "money", 75));
            var keys = new HashSet<string>(accts.Select(a => a.Key), StringComparer.OrdinalIgnoreCase);
            var mv = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase); var open = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            int f = GlCalc.DayInt(months[0]), t = GlCalc.DayInt(to);
            foreach (GlAgg g in x.S.Agg)
            {
                if (!keys.Contains(g.Key) || g.Day > t) continue;
                if (g.Day < f) { long o; open.TryGetValue(g.Key, out o); open[g.Key] = o + g.Dr - g.Cr; continue; }
                long[] v; if (!mv.TryGetValue(g.Key, out v)) { v = new long[months.Count]; mv[g.Key] = v; }
                DateTime dd = GlCalc.FromDayInt(g.Day); int i = (dd.Year - months[0].Year) * 12 + dd.Month - months[0].Month;
                if (i >= 0 && i < months.Count) v[i] += g.Dr - g.Cr;
            }
            foreach (GlAcct a in accts)
            {
                long[] v; mv.TryGetValue(a.Key, out v); long o; open.TryGetValue(a.Key, out o);
                if (v == null && (!bal || o == 0)) continue;
                v = v ?? new long[months.Count];
                var cells = new List<object> { a.Code, a.Name }; long run = o;
                foreach (long m in v) { run += m; cells.Add((decimal)(bal ? run : m)); }
                if (!bal) cells.Add((decimal)v.Sum());
                r.Add(AccountLink(a.Key, from, to), "", cells.ToArray());
            }
            r.Checks.Add(Info("SIGN", "Debit balances are positive", "Income and liability accounts usually show negative figures (credit balances)."));
            return r;
        };
        return d;
    }

    // ── R16 Audit trail ─────────────────────────────────────────────

    private static GlReportDef R16()
    {
        var d = new GlReportDef { Code = "R16", Group = "Control and audit", Title = "Audit Trail", Description = "Ledger lines edited or deleted after posting, and every change made through these General Ledger screens.", Noun = "row" };
        d.Params.Add(new GlParam("kind", "Show", "select") { Default = "deletions", Options = new List<object> { new { v = "deletions", t = "Lines deleted from the ledger" }, new { v = "edits", t = "Lines edited after posting" }, new { v = "gl", t = "Changes made in these screens" } } });
        d.Params.Add(new GlParam("from", "From", "date") { Role = "from", Default = GlFmt.Iso(new DateTime(2026, 1, 1)) });
        d.Params.Add(PTo());
        d.Params.Add(new GlParam("user", "By", "text") { Help = "Part of the user name." });
        d.Params.Add(new GlParam("undated", "Include rows with no date", "bool") { Default = "1" });
        d.Run = x =>
        {
            DateTime to = x.DateOr("to", DateTime.Today), from = x.DateOr("from", new DateTime(2000, 1, 1));
            string kind = x.Str("kind"), user = x.Str("user"); bool undated = x.Bool("undated");
            string sql, when, who;
            if (kind == "edits")
            {
                when = "trigger_date"; who = "IFNULL(triggered_by,'')";
                sql = "SELECT trigger_date, IFNULL(triggered_by,''), 'Edited', accountcode, voucherNo, CONCAT(IFNULL(old_transactionType,''),' ',IFNULL(old_transaction_amount,'')), CONCAT(IFNULL(new_transactionType,''),' ',IFNULL(new_transaction_amount,'')), " +
                      "CAST(IFNULL(new_transaction_amount,0) AS SIGNED) - CAST(IFNULL(old_transaction_amount,0) AS SIGNED), particulars, transactionDate FROM edit_ledger";
            }
            else if (kind == "gl")
            {
                when = "created_at"; who = "actor";
                sql = "SELECT created_at, actor, CONCAT(entity,' ',action), entity_id, NULL, LEFT(IFNULL(before_json,''),300), LEFT(IFNULL(after_json,''),300), NULL, IFNULL(reason,''), NULL FROM gl_audit";
            }
            else
            {
                when = "delete_date"; who = "IFNULL(deleted_by,'')";
                sql = "SELECT delete_date, IFNULL(deleted_by,''), 'Deleted', accountcode, voucherNo, CONCAT(transactionType,' ',transaction_amount), '', CAST(transaction_amount AS SIGNED), particulars, transactionDate FROM fin_deleted_ledger";
            }
            string w = " WHERE ((" + when + " >= @f AND " + when + " < @t1)" + (undated ? " OR " + when + " IS NULL" : "") + ")";
            var prm = new List<object> { "@f", from, "@t1", to.AddDays(1) };
            if (user != "") { w += " AND " + who + " LIKE @u"; prm.AddRange(new object[] { "@u", "%" + user + "%" }); }
            if (!string.IsNullOrEmpty(x.Search)) { w += " AND (" + (kind == "gl" ? "entity_id LIKE @q OR reason LIKE @q" : "accountcode LIKE @q OR particulars LIKE @q OR CAST(voucherNo AS CHAR) = @qv") + ")"; prm.AddRange(new object[] { "@q", "%" + x.Search + "%", "@qv", x.Search }); }
            string table = kind == "edits" ? "edit_ledger" : kind == "gl" ? "gl_audit" : "fin_deleted_ledger";
            long count = GlDb.L(GlDb.Scalar(x.C, null, "SELECT COUNT(*) FROM " + table + w, prm.ToArray()));
            int size = x.Export ? 500000 : Math.Max(1, Math.Min(500, x.Size)); long off = x.Export ? 0 : (long)(Math.Max(1, x.Page) - 1) * size;
            var pprm = new List<object>(prm); pprm.AddRange(new object[] { "@off", off, "@n", size });
            DataTable rows = GlDb.Table(x.C, null, sql + w + " ORDER BY " + when + " DESC LIMIT @off, @n", pprm.ToArray());
            var r = new GlResult { Paged = true, TotalRows = count, NoTotals = true, Subtitle = GlFmt.Date(from) + " to " + GlFmt.Date(to), Noun = "row", NounPlural = "rows" };
            r.Basis = kind == "edits" ? "edit_ledger: one row per change to a posted ledger line, kept by the database trigger." : kind == "gl" ? "gl_audit: every change made through the General Ledger screens. Append-only." : "fin_deleted_ledger: a copy of every line deleted from fin_ledger.";
            r.Cols.Add(new GlCol("when", "When", "date", 65)); r.Cols.Add(new GlCol("who", "By", "text", 70)); r.Cols.Add(new GlCol("what", "What", "text", 60));
            r.Cols.Add(new GlCol("acct", kind == "gl" ? "Record" : "Account", "code", 70)); r.Cols.Add(new GlCol("v", "Voucher", "code", 50) { Hidden = kind == "gl" });
            r.Cols.Add(new GlCol("before", "Before", "text", kind == "gl" ? 160 : 80)); r.Cols.Add(new GlCol("after", "After", "text", kind == "gl" ? 160 : 80));
            r.Cols.Add(new GlCol("amt", kind == "edits" ? "Change" : "Amount", "money", 75) { Hidden = kind == "gl" });
            r.Cols.Add(new GlCol("p", kind == "gl" ? "Reason" : "Particulars", "text", 160)); r.Cols.Add(new GlCol("d", "Line date", "date", 60) { Hidden = kind == "gl" });
            foreach (DataRow q in rows.Rows)
            {
                long v = GlDb.L(q[4]);
                r.Add(v > 0 ? VoucherLink(v) : null, "", q[0] is DBNull ? null : (object)GlDb.D(q[0]), GlDb.S(q[1]) == "" ? "unknown" : GlDb.S(q[1]), GlDb.S(q[2]), GlDb.S(q[3]), v > 0 ? GlDb.S(q[4]) : "", GlDb.S(q[5]), GlDb.S(q[6]), q[7] is DBNull ? null : (object)GlDb.M(q[7]), GlDb.S(q[8]), q[9] is DBNull ? null : (object)GlDb.D(q[9]));
            }
            if (kind == "edits")
            {
                long nb = GlDb.L(GlDb.Scalar(x.C, null, "SELECT COUNT(*) FROM edit_ledger WHERE trigger_date IS NULL OR IFNULL(triggered_by,'') = ''"));
                r.Checks.Add(nb == 0 ? Pass("ACTOR", "Every edit records who made it and when") : Fail("ACTOR", GlFmt.Plural(nb, "edit has", "edits have") + " no date or no actor", 0, nb, "Made before the edit trigger was repaired in August 2026. Who made them cannot be shown.", null));
            }
            if (kind == "deletions" || kind == "")
            {
                var bulk = GlDb.Table(x.C, null, "SELECT DATE(delete_date) d, IFNULL(deleted_by,'unknown'), COUNT(*), SUM(transaction_amount) FROM fin_deleted_ledger GROUP BY d, 2 HAVING COUNT(*) >= 1000 ORDER BY COUNT(*) DESC").Rows.Cast<DataRow>().ToList();
                if (bulk.Count > 0)
                    r.Checks.Add(Fail("BULK", GlFmt.Plural(bulk.Count, "day saw", "days saw") + " bulk deletions of 1,000 lines or more", bulk.Sum(q => GlDb.M(q[3])), bulk.Sum(q => GlDb.L(q[2])),
                        string.Join("; ", bulk.Take(6).Select(q => (q[0] is DBNull ? "undated" : GlFmt.Date(q[0])) + " by " + GlDb.S(q[1]) + ": " + GlFmt.Count(GlDb.L(q[2])) + " lines")) + ". Deleting posted lines changes reported figures; corrections should be adjusting entries.",
                        new GlFix("screen", "Finance Warnings", "AccountsWarnings.aspx?rule=W19")));
            }
            return r;
        };
        return d;
    }

    // ── R17 Control account reconciliations ─────────────────────────

    private static GlReportDef R17()
    {
        var d = new GlReportDef { Code = "R17", Group = "Receivables and payables", Title = "Control Account Reconciliations", Description = "Each control account against its subsidiary ledger, and the three student receivable figures side by side.", Noun = "control" };
        d.Params.Add(new GlParam("to", "As at", "date") { Role = "asat", Required = true });
        d.Run = x =>
        {
            DateTime to = x.DateOr("to", DateTime.Today);
            var r = new GlResult { Subtitle = "As at " + GlFmt.Date(to), Noun = "control", NounPlural = "controls", NoTotals = true, Basis = "Control accounts come from the chart's ledger-type link (fin_subaccounts.collectionLedgerType), balance-sheet accounts only. Liabilities are shown credit positive." };
            r.Cols.Add(new GlCol("ctl", "Control account", "text", 170)); r.Cols.Add(new GlCol("sub", "Subsidiary ledger", "text", 170));
            r.Cols.Add(new GlCol("cb", "Control balance", "money", 90)); r.Cols.Add(new GlCol("sb", "Subsidiary balance", "money", 90)); r.Cols.Add(new GlCol("diff", "Difference", "money", 90)); r.Cols.Add(new GlCol("note", "Note", "text", 200));
            int bad = 0; decimal badAmt = 0;
            foreach (GlCalc.ControlPair p in GlCalc.ControlPairs(x.S))
            {
                long sb = GlCalc.Balance(x.S, p.Sub.Key, to) * p.Sign;
                if (p.Student)
                {
                    decimal canon = -GlDb.M(GlDb.Scalar(x.C, null, "SELECT IFNULL(SUM(total_balance),0) FROM fin_student_balance_cache"));
                    decimal tracking = GlDb.M(GlDb.Scalar(x.C, null, "SELECT IFNULL(SUM(IF(trans_type='Bill',amount,-amount)),0) FROM fin_studentfeestracking WHERE post_status='Posted' AND trans_date < @t", "@t", to.AddDays(1)));
                    r.Add(AccountLink(p.Sub.Key, null, to), "", "None (no receivables control account)", p.Sub.Name, null, (decimal)sb, null,
                        "Canonical balance today " + GlFmt.Money(canon) + "; fee tracking " + GlFmt.Money(tracking) + ". Never added together.");
                    continue;
                }
                long cb = GlCalc.Balance(x.S, p.Control.Key, to) * p.Sign;
                if (cb != sb) { bad++; badAmt += Math.Abs(sb - cb); }
                r.Add(AccountLink(p.Control.Key, null, to), "", p.Control.Code + " " + p.Control.Name, p.Sub.Name, (decimal)cb, (decimal)sb, (decimal)(sb - cb), cb == sb ? "Agrees" : "Subsidiary postings do not pass through the control account");
            }
            r.Checks.Add(bad == 0 ? Pass("CONTROLS", "Every control account agrees with its subsidiary ledger")
                                  : Fail("CONTROLS", GlFmt.Plural(bad, "control account differs", "control accounts differ") + " from the subsidiary ledger", badAmt, bad, "Amount is the sum of the differences, ignoring sign. See Finance Warnings W11.", new GlFix("screen", "Finance Warnings", "AccountsWarnings.aspx?rule=W11")));
            r.Checks.Add(Info("STUDENTS", "Student receivables have no control account", "A control account is recommended (open question Q3). Until then the subsidiary ledger, the canonical balance and fee tracking are shown separately."));
            return r;
        };
        return d;
    }

    // ── R18 Unbalanced vouchers ─────────────────────────────────────

    private static GlReportDef R18()
    {
        var d = new GlReportDef { Code = "R18", Group = "Control and audit", Title = "Unbalanced Vouchers", Description = "Vouchers whose debit and credit differ, by pattern. Their differences add up to the trial balance difference.", Noun = "voucher" };
        d.Params.Add(PYear());
        d.Params.Add(new GlParam("from", "From", "date") { Role = "from", Help = "Leave blank for every voucher up to the end date." });
        d.Params.Add(PTo());
        d.Params.Add(new GlParam("pattern", "Pattern", "select") { Default = "", Options = new List<object> { new { v = "", t = "Every pattern" }, new { v = "DEBIT_ONLY", t = "Debit only" }, new { v = "CREDIT_ONLY", t = "Credit only" }, new { v = "REUSED", t = "Number reused" }, new { v = "UNEQUAL", t = "Two-sided, unequal" }, new { v = "SUBLEDGER_SIDE", t = "Other side on a subsidiary ledger (chart basis)" } } });
        d.Params.Add(PBasis());
        d.Defaults = (x, ps) => { ps.First(p => p.Key == "from").Default = ""; ps.First(p => p.Key == "year").Default = ""; };
        d.Run = x =>
        {
            DateTime to = x.DateOr("to", DateTime.Today); DateTime? from = x.Date("from");
            string basis = x.Str("basis") == "CHART" ? "CHART" : "WHOLE", pat = x.Str("pattern");
            long nvd; int nvl;
            var all = GlCalc.UnbalancedVouchers(from, to, basis, out nvd, out nvl);
            var acked = new HashSet<string>(GlDb.Table(x.C, null, "SELECT record_key FROM gl_record_ack WHERE rule_code='W01' AND is_active=1").Rows.Cast<DataRow>().Select(q => GlDb.S(q[0])));
            var r = new GlResult { Subtitle = (from.HasValue ? x.RangeText(from.Value, to) : "Up to " + GlFmt.Date(to)), Basis = GlChecks.BasisText(basis) + " Only lines dated in the range count.", Noun = "voucher", NounPlural = "vouchers" };
            r.Cols.Add(new GlCol("v", "Voucher", "code", 55)); r.Cols.Add(new GlCol("d0", "First date", "date", 60)); r.Cols.Add(new GlCol("d1", "Last date", "date", 60));
            r.Cols.Add(new GlCol("pat", "Pattern", "text", 120)); r.Cols.Add(new GlCol("src", "Source", "text", 80)); r.Cols.Add(new GlCol("n", "Lines", "count", 40).Summed());
            r.Cols.Add(new GlCol("dr", "Debit", "money", 80)); r.Cols.Add(new GlCol("cr", "Credit", "money", 80)); r.Cols.Add(new GlCol("diff", "Difference", "money", 80)); r.Cols.Add(new GlCol("ack", "Accepted", "text", 50));
            foreach (GlVoucherIssue v in all.Where(v => pat == "" || v.Pattern == pat).OrderBy(v => v.First).ThenBy(v => v.VoucherNo))
                r.Add(VoucherLink(v.VoucherNo), "", v.VoucherNo.ToString(CultureInfo.InvariantCulture), v.First, v.Last, GlCalc.PatternText(v.Pattern).Split('(')[0].Trim(), v.Source, (long)v.Lines, (decimal)v.Dr, (decimal)v.Cr, (decimal)v.Diff, acked.Contains(v.VoucherNo.ToString(CultureInfo.InvariantCulture)) ? "Yes" : "");
            GlCalc.Totals t = GlCalc.Total(x.S, from, to, basis);
            r.Checks.Add(GlChecks.Difference("TB", "These vouchers explain the trial balance difference for the range", from, to, basis, t.Diff));
            if (pat != "") r.Checks.Add(Info("FILTER", "Filtered to one pattern", "The listed total is part of the difference only."));
            return r;
        };
        return d;
    }

    // ── R19 Duplicate postings ──────────────────────────────────────

    private static GlReportDef R19()
    {
        var d = new GlReportDef { Code = "R19", Group = "Control and audit", Title = "Duplicate Postings", Description = "Groups of ledger lines identical in account, side, amount, date and particulars.", Noun = "group" };
        d.Params.Add(new GlParam("from", "From", "date") { Role = "from" });
        d.Params.Add(PTo());
        d.Params.Add(new GlParam("accepted", "Include groups accepted as genuine", "bool") { Default = "0" });
        d.Defaults = (x, ps) => { ps.First(p => p.Key == "from").Default = ""; };
        d.Run = x =>
        {
            DateTime to = x.DateOr("to", DateTime.Today); DateTime from = x.DateOr("from", new DateTime(1900, 1, 1)); bool inclAcc = x.Bool("accepted");
            var acked = new HashSet<string>(GlDb.Table(x.C, null, "SELECT record_key FROM gl_record_ack WHERE rule_code='W09' AND is_active=1").Rows.Cast<DataRow>().Select(q => GlDb.S(q[0])));
            var r = new GlResult { Subtitle = x.Date("from").HasValue ? x.RangeText(from, to) : "Up to " + GlFmt.Date(to), Noun = "group", NounPlural = "groups", Basis = "Whole ledger, lines with an amount above zero. The extra amount is the value of every copy after the first." };
            r.Cols.Add(new GlCol("acct", "Account", "code", 75)); r.Cols.Add(new GlCol("d", "Date", "date", 60)); r.Cols.Add(new GlCol("side", "Side", "text", 30));
            r.Cols.Add(new GlCol("amt", "Amount", "money", 80).NoSum()); r.Cols.Add(new GlCol("p", "Particulars", "text", 220)); r.Cols.Add(new GlCol("n", "Copies", "count", 40));
            r.Cols.Add(new GlCol("extra", "Extra amount", "money", 85)); r.Cols.Add(new GlCol("ack", "Accepted", "text", 50));
            int accepted = 0; decimal accAmt = 0;
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT MD5(CONCAT_WS('|', accountcode, transactionType, transaction_amount, transactionDate, IFNULL(particulars,''))) k, accountcode, transactionType, CAST(transaction_amount AS SIGNED), transactionDate, particulars, COUNT(*) n " +
                "FROM fin_ledger WHERE transaction_amount > 0 AND transactionDate BETWEEN @f AND @t GROUP BY accountcode, transactionType, transaction_amount, transactionDate, particulars HAVING n > 1 ORDER BY transactionDate", "@f", from, "@t", to).Rows)
            {
                bool isAck = acked.Contains(GlDb.S(q[0])); long n = GlDb.L(q[6]); decimal amt = GlDb.M(q[3]);
                if (isAck) { accepted++; accAmt += (n - 1) * amt; if (!inclAcc) continue; }
                r.Add(AccountLink(GlDb.S(q[1]), GlDb.D(q[4]), GlDb.D(q[4])), "", GlDb.S(q[1]), GlDb.D(q[4]), GlDb.S(q[2]), amt, GlDb.S(q[5]), n, (n - 1) * amt, isAck ? "Yes" : "");
            }
            if (accepted > 0) r.Checks.Add(new GlCheck { code = "ACCEPTED", title = GlFmt.Plural(accepted, "group was", "groups were") + " accepted as genuine", status = "info", amount = accAmt, count = accepted, cause = inclAcc ? "They are included and marked." : "They are left out of this list and its total." });
            r.Checks.Add(r.Rows.Count == 0 ? Pass("DUPS", "No unexplained duplicate lines") : Fail("DUPS", GlFmt.Plural(r.Rows.Count, "group", "groups") + " of identical lines", r.Rows.Sum(q => (decimal)q[6]), r.Rows.Count,
                "Accept genuine repeats on the Finance Warnings page (W09); reverse real duplicates with an adjusting entry.", new GlFix("screen", "Finance Warnings", "AccountsWarnings.aspx?rule=W09")));
            return r;
        };
        return d;
    }

    // ── R20 Chart of accounts ───────────────────────────────────────

    private static GlReportDef R20()
    {
        var d = new GlReportDef { Code = "R20", Group = "Control and audit", Title = "Chart of Accounts", Description = "Every account: in the chart, provisionally mapped, not mapped, unused, and the subsidiary ledgers.", Noun = "account" };
        d.Params.Add(new GlParam("status", "Status", "select") { Default = "", Options = new List<object> { new { v = "", t = "Every account" }, new { v = "chart", t = "In the chart" }, new { v = "provisional", t = "Missing from the chart, mapped provisionally" }, new { v = "unmapped", t = "Missing from the chart, not mapped" }, new { v = "unclassified", t = "Classified as Unclassified or Suspense" }, new { v = "unused", t = "In the chart, never used" }, new { v = "subledger", t = "Subsidiary ledgers" } } });
        d.Run = x =>
        {
            string st = x.Str("status");
            var stats = x.S.Agg.GroupBy(g => g.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => new { n = g.Sum(a => (long)a.N), b = g.Sum(a => a.Dr - a.Cr), last = g.Max(a => a.Day) }, StringComparer.OrdinalIgnoreCase);
            var map = GlDb.Table(x.C, null, "SELECT accountcode, is_provisional, IFNULL(confirmed_by,''), IFNULL(basis,'') FROM gl_account_map").Rows.Cast<DataRow>().ToDictionary(q => GlDb.S(q[0]), q => q, StringComparer.OrdinalIgnoreCase);
            var r = new GlResult { Noun = "account", NounPlural = "accounts", Basis = "fin_mainaccounts and fin_subaccounts, the provisional map (gl_account_map) and every code used in fin_ledger." };
            r.Cols.Add(new GlCol("code", "Code", "code", 75)); r.Cols.Add(new GlCol("name", "Name", "text", 200)); r.Cols.Add(new GlCol("cat", "Category", "text", 70)); r.Cols.Add(new GlCol("sub", "Sub-category", "text", 120));
            r.Cols.Add(new GlCol("main", "Main account", "code", 60)); r.Cols.Add(new GlCol("st", "Status", "text", 110)); r.Cols.Add(new GlCol("n", "Lines", "count", 50).Summed());
            r.Cols.Add(new GlCol("last", "Last used", "date", 60)); r.Cols.Add(new GlCol("bal", "Balance", "money", 85).NoSum());
            int unused = 0, unmapped = 0, prov = 0;
            foreach (GlAcct a in x.S.Accounts.Values.OrderBy(a => Array.IndexOf(GlCalc.CategoryOrder, a.Category)).ThenBy(a => a.Kind == "SUBLEDGER" ? 1 : 0).ThenBy(a => a.Code))
            {
                var s = stats.ContainsKey(a.Key) ? stats[a.Key] : null;
                string status = a.Kind == "CHART" ? (s == null ? "Never used" : "In the chart") : a.Kind == "SUBLEDGER" ? "Subsidiary ledger" : a.Kind == "UNMAPPED" ? "Not mapped" :
                                (map.ContainsKey(a.Code) && GlDb.I(map[a.Code][1]) == 0 ? "Mapping confirmed" : "Provisional mapping");
                if (a.Kind == "CHART" && s == null) unused++;
                if (a.Kind == "UNMAPPED") unmapped++;
                if (status == "Provisional mapping" && s != null) prov++;
                bool keep = st == "" || (st == "chart" && a.Kind == "CHART") || (st == "provisional" && a.Kind == "PROVISIONAL") || (st == "unmapped" && a.Kind == "UNMAPPED")
                            || (st == "unused" && a.Kind == "CHART" && s == null) || (st == "subledger" && a.Kind == "SUBLEDGER") || (st == "unclassified" && (a.Category == "Unclassified" || a.Category == "Suspense"));
                if (!keep) continue;
                r.Add(AccountLink(a.Key, null, null), "", a.Code, a.Name, a.Category, a.SubCategory, a.MainCode, status, s == null ? 0L : s.n, s == null ? null : (object)GlCalc.FromDayInt(s.last), s == null ? 0m : (decimal)s.b);
            }
            var dupNames = x.S.Accounts.Values.Where(a => a.Kind == "CHART").GroupBy(a => (a.Name ?? "").Trim().ToUpperInvariant()).Where(g => g.Count() > 1).ToList();
            r.Checks.Add(unmapped == 0 ? Pass("UNMAPPED", "Every code used is in the chart or mapped") : Fail("UNMAPPED", GlFmt.Plural(unmapped, "code is", "codes are") + " used but not mapped", 0, unmapped, "Shown as Unclassified. Map them on the account card.", null));
            if (prov > 0) r.Checks.Add(new GlCheck { code = "PROVISIONAL", title = GlFmt.Plural(prov, "code in use is", "codes in use are") + " mapped provisionally and not yet confirmed", status = "info", count = prov, cause = "The Bursar confirms or changes each mapping on the account card (open question Q2).", fix = new GlFix("report", "Show them", ReportLink("R20", "status", "provisional")) });
            r.Checks.Add(dupNames.Count == 0 ? Pass("DUPNAMES", "No two chart accounts share a name") : Fail("DUPNAMES", GlFmt.Plural(dupNames.Count, "name is", "names are") + " used by more than one chart account", 0, dupNames.Count, string.Join("; ", dupNames.Select(g => g.Key + " (" + string.Join(", ", g.Select(a => a.Code)) + ")")), null));
            if (unused > 0) r.Checks.Add(new GlCheck { code = "UNUSED", title = GlFmt.Plural(unused, "chart account has", "chart accounts have") + " never been used", status = "info", count = unused, cause = "They may be retired from the chart once the Bursar agrees.", fix = new GlFix("report", "Show them", ReportLink("R20", "status", "unused")) });
            return r;
        };
        return d;
    }
}
