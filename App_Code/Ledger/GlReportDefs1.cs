using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger reports R01-R07: statements, ledgers, cash.
//  Every figure comes from GlCalc (the snapshot) or from a SELECT on the
//  read-only connection using the same classification.
// =====================================================================

public static partial class GlReports
{
    // ── Ledger line filter for one key (the same rule as GlCalc.KeyExpr) ──

    private static readonly Dictionary<string, string[]> SubTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "SUB:STUDENTS", new[] { "Student", "-" } },
        { "SUB:FOEFees", new[] { "FOEFees" } }, { "SUB:FSTEADFees", new[] { "FSTEADFees" } }, { "SUB:FBMFees", new[] { "FBMFees" } },
        { "SUB:FSSAHFees", new[] { "FSSAHFees" } }, { "SUB:BursaryFees", new[] { "BursaryFees" } },
        { "SUB:Supplier", new[] { "Supplier" } }, { "SUB:SalaryAdvance", new[] { "Salary Advance" } },
        { "SUB:Gratuity", new[] { "Gratuity" } }, { "SUB:Sponsor", new[] { "Sponsor" } }
    };

    private const string AllSubTypes = "('Student','-','FOEFees','FSTEADFees','FBMFees','FSSAHFees','BursaryFees','Supplier','Salary Advance','Gratuity','Sponsor')";

    /// <summary>WHERE fragment selecting exactly the lines of one key (fin_ledger l LEFT JOIN fin_subaccounts s). Adds its parameters to prm.</summary>
    public static string KeyWhere(string key, string member, List<object> prm)
    {
        string[] types;
        string w;
        if (SubTypes.TryGetValue(key, out types))
        {
            var names = new List<string>();
            for (int i = 0; i < types.Length; i++) { names.Add("@kt" + i); prm.Add("@kt" + i); prm.Add(types[i]); }
            w = "s.AccountCode IS NULL AND l.account_type IN (" + string.Join(",", names) + ")";
            if (!string.IsNullOrEmpty(member)) { w += " AND l.accountcode = @km"; prm.Add("@km"); prm.Add(member); }
        }
        else
        {
            w = "l.accountcode = @kc AND (s.AccountCode IS NOT NULL OR l.account_type NOT IN " + AllSubTypes + ")";
            prm.Add("@kc"); prm.Add(key);
        }
        return w;
    }

    private static GlAcct Acct(GlRunCtx x, string key)
    {
        GlAcct a;
        if (string.IsNullOrEmpty(key) || !x.S.Accounts.TryGetValue(key, out a)) throw new GlRefusal("Choose an account.");
        return a;
    }

    private static string TypeShort(GlAcct a)
    {
        switch (a.Kind) { case "CHART": return "Chart"; case "PROVISIONAL": return "Provisional"; case "SUBLEDGER": return "Subsidiary"; default: return "Not mapped"; }
    }

    // ── R01 Trial balance ───────────────────────────────────────────

    private static GlReportDef R01()
    {
        var d = new GlReportDef
        {
            Code = "R01", Group = "Statements", Title = "Trial Balance",
            Description = "Opening, movement and closing balance of every account, with the cause analysis of any difference.", Noun = "account"
        };
        d.Params.Add(PYear());
        d.Params.Add(new GlParam("from", "From", "date") { Role = "from", Help = "Leave blank for closing balances only." });
        d.Params.Add(PTo("To (as at)"));
        d.Params.Add(PBasis());
        d.Params.Add(new GlParam("level", "Level", "select") { Default = "account", Options = new List<object> { new { v = "account", t = "Account" }, new { v = "category", t = "Category and sub-category" } } });
        d.Params.Add(new GlParam("category", "Category", "select") { Default = "", Options = CategoryOptions() });
        d.Params.Add(new GlParam("zero", "Include accounts with no balance or movement", "bool") { Default = "0" });
        d.Run = x =>
        {
            DateTime to = x.DateOr("to", DateTime.Today);
            DateTime? from = x.Date("from");
            if (from.HasValue && from > to) throw new GlRefusal("The start date is after the end date.");
            string basis = x.Str("basis") == "CHART" ? "CHART" : "WHOLE";
            string cat = x.Str("category");
            bool byCat = x.Str("level") == "category", zero = x.Bool("zero");
            var tb = GlCalc.TrialBalance(x.S, from, to, basis);
            if (zero && !byCat)
            {
                var have = new HashSet<string>(tb.Select(t => t.A.Key), StringComparer.OrdinalIgnoreCase);
                foreach (GlAcct a in x.S.Accounts.Values.Where(a => !have.Contains(a.Key) && (basis == "WHOLE" || a.Kind == "CHART" || a.Kind == "PROVISIONAL")))
                    tb.Add(new GlTbRow { A = a });
                tb = tb.OrderBy(t => Array.IndexOf(GlCalc.CategoryOrder, t.A.Category)).ThenBy(t => t.A.Kind == "SUBLEDGER" ? 1 : 0).ThenBy(t => t.A.Code).ToList();
            }
            if (cat != "") tb = tb.Where(t => t.A.Category == cat).ToList();
            if (!zero) tb = tb.Where(t => t.OpenDr != t.OpenCr || t.MovDr != 0 || t.MovCr != 0).ToList();

            var r = new GlResult { Basis = GlChecks.BasisText(basis), Noun = byCat ? "group" : "account", NounPlural = byCat ? "groups" : "accounts" };
            r.Subtitle = from.HasValue ? GlFmt.Date(from.Value) + " to " + GlFmt.Date(to) : "As at " + GlFmt.Date(to);
            r.Cols.Add(new GlCol("code", byCat ? "Category" : "Code", "code", 70));
            r.Cols.Add(new GlCol("name", byCat ? "Sub-category" : "Account", "text", 190));
            r.Cols.Add(new GlCol("type", byCat ? "Accounts" : "Type", byCat ? "count" : "text", 60).NoSum());
            r.Cols.Add(new GlCol("odr", "Opening debit", "money", 80) { Hidden = !from.HasValue });
            r.Cols.Add(new GlCol("ocr", "Opening credit", "money", 80) { Hidden = !from.HasValue });
            r.Cols.Add(new GlCol("mdr", from.HasValue ? "Movement debit" : "Total debit", "money", 80));
            r.Cols.Add(new GlCol("mcr", from.HasValue ? "Movement credit" : "Total credit", "money", 80));
            r.Cols.Add(new GlCol("cdr", "Closing debit", "money", 80));
            r.Cols.Add(new GlCol("ccr", "Closing credit", "money", 80));
            if (!byCat)
            {
                foreach (GlTbRow t in tb)
                {
                    long o = t.Opening, c = t.Closing;
                    r.Add(AccountLink(t.A.Key, from, to), "", t.A.Code, t.A.Name, TypeShort(t.A),
                        (decimal)Math.Max(o, 0), (decimal)Math.Max(-o, 0), (decimal)t.MovDr, (decimal)t.MovCr, (decimal)Math.Max(c, 0), (decimal)Math.Max(-c, 0));
                }
            }
            else
            {
                foreach (var g in tb.GroupBy(t => t.A.Category + "|" + (t.A.SubCategory ?? "")))
                {
                    string[] k = g.Key.Split('|');
                    long o = g.Sum(t => t.Opening), c = g.Sum(t => t.Closing);
                    r.Add(ReportLink("R01", "level", "account", "category", k[0], "from", from.HasValue ? GlFmt.Iso(from.Value) : "", "to", GlFmt.Iso(to), "basis", basis), "",
                        k[0], k[1] == "" ? "(none)" : k[1], (long)g.Count(),
                        (decimal)Math.Max(o, 0), (decimal)Math.Max(-o, 0), (decimal)g.Sum(t => t.MovDr), (decimal)g.Sum(t => t.MovCr), (decimal)Math.Max(c, 0), (decimal)Math.Max(-c, 0));
                }
            }
            if (cat == "") r.Checks.AddRange(GlChecks.TrialBalance(x.S, from, to, basis));
            else r.Checks.Add(Info("CATEGORY", "Showing " + cat + " only", "The totals do not balance on their own because only one category is shown. Clear the category to check the trial balance."));
            return r;
        };
        return d;
    }

    private static List<object> CategoryOptions()
    {
        var l = new List<object> { new { v = "", t = "All categories" } };
        foreach (string c in GlCalc.CategoryOrder) l.Add(new { v = c, t = c });
        return l;
    }

    // ── R02 General ledger and account statement ────────────────────

    private static GlReportDef R02()
    {
        var d = new GlReportDef
        {
            Code = "R02", Group = "Ledgers and vouchers", Title = "General Ledger and Account Statement",
            Description = "Every line of one account in a period, with the opening balance and a running balance.", Noun = "line"
        };
        d.Params.Add(PAccount(true));
        d.Params.Add(new GlParam("member", "Member", "text") { Help = "For a subsidiary ledger: a registration number or supplier code. Leave blank for the whole ledger." });
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Run = x => Statement(x, x.Str("account"), x.Str("member"), false);
        return d;
    }

    /// <summary>R02 and R07: lines of one key in a range, paged in SQL, with a running balance.</summary>
    private static GlResult Statement(GlRunCtx x, string key, string member, bool cashBook)
    {
        GlAcct a = Acct(x, key);
        DateTime from, to; x.Range(out from, out to);
        var prm = new List<object>();
        string where = KeyWhere(a.Key, member, prm) + " AND l.transactionDate BETWEEN @f AND @t";
        prm.AddRange(new object[] { "@f", from, "@t", to });
        string search = (x.Search ?? "").Trim();
        string swhere = where;
        var sprm = new List<object>(prm);
        if (search != "") { swhere += " AND (l.particulars LIKE @q OR CAST(l.voucherNo AS CHAR) = @qv OR l.accountcode = @qv)"; sprm.AddRange(new object[] { "@q", "%" + search + "%", "@qv", search }); }
        const string FROM = " FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode WHERE ";

        var oprm = new List<object>(); string owhere = KeyWhere(a.Key, member, oprm) + " AND l.transactionDate < @f"; oprm.AddRange(new object[] { "@f", from });
        long opening = GlDb.L(GlDb.Scalar(x.C, null, "SELECT IFNULL(SUM(" + GlCalc.NetExpr + "),0)" + FROM + owhere, oprm.ToArray()));
        DataRow tot = GlDb.Table(x.C, null, "SELECT COUNT(*), IFNULL(SUM(" + GlCalc.DrExpr + "),0), IFNULL(SUM(" + GlCalc.CrExpr + "),0)" + FROM + swhere, sprm.ToArray()).Rows[0];
        long n = GlDb.L(tot[0]); decimal tdr = GlDb.M(tot[1]), tcr = GlDb.M(tot[2]);

        int size = x.Export ? 500000 : Math.Max(1, Math.Min(500, x.Size)), page = x.Export ? 1 : Math.Max(1, x.Page);
        long offset = (long)(page - 1) * size;
        long before = 0;
        if (search == "" && offset > 0)
        {
            var bprm = new List<object>(sprm); bprm.AddRange(new object[] { "@off", offset });
            before = GlDb.L(GlDb.Scalar(x.C, null, "SELECT IFNULL(SUM(v),0) FROM (SELECT " + GlCalc.NetExpr + " v" + FROM + swhere + " ORDER BY l.transactionDate, l.TID LIMIT @off) z", bprm.ToArray()));
        }
        var pprm = new List<object>(sprm); pprm.AddRange(new object[] { "@off", offset, "@n", size });
        DataTable lines = GlDb.Table(x.C, null,
            "SELECT l.TID, l.transactionDate, l.voucherNo, l.particulars, IFNULL(l.source_system,''), l.accountcode, l.transactionType, CAST(l.transaction_amount AS SIGNED), l.teller, IFNULL(l.RefNo,'')" +
            FROM + swhere + " ORDER BY l.transactionDate, l.TID LIMIT @off, @n", pprm.ToArray());

        bool sub = a.Kind == "SUBLEDGER";
        var r = new GlResult { Paged = true, TotalRows = n, Noun = "line", NounPlural = "lines" };
        r.Title = cashBook ? "Cash Book" : "General Ledger and Account Statement";
        r.Subtitle = a.Code + " " + a.Name + (string.IsNullOrEmpty(member) ? "" : ", member " + member) + ", " + x.RangeText(from, to);
        r.Basis = "Lines of " + a.Code + " (" + a.KindText.ToLowerInvariant() + ") in fin_ledger. " + (search == "" ? "Balance runs from the opening balance." : "A search is applied, so no running balance is shown.");
        r.Cols.Add(new GlCol("date", "Date", "date", 60));
        r.Cols.Add(new GlCol("voucher", "Voucher", "code", 55));
        if (sub) r.Cols.Add(new GlCol("member", a.Key == "SUB:Supplier" ? "Supplier" : "Member", "code", 75));
        r.Cols.Add(new GlCol("particulars", "Particulars", "text", 200));
        r.Cols.Add(new GlCol("source", "Source", "text", 70));
        r.Cols.Add(new GlCol("dr", cashBook ? "Receipts" : "Debit", "money", 75));
        r.Cols.Add(new GlCol("cr", cashBook ? "Payments" : "Credit", "money", 75));
        r.Cols.Add(new GlCol("bal", "Balance", "money", 80).NoSum());
        Func<object[], object[]> shape = cells => sub ? cells : cells.Where((c, i) => i != 2).ToArray();
        long run = opening + before;
        if (page == 1 && search == "")
            r.Add(null, "s", shape(new object[] { from, null, null, "Opening balance", null, null, null, (decimal)opening }));
        foreach (DataRow l in lines.Rows)
        {
            long amt = GlDb.L(l[7]); bool dr = GlDb.S(l[6]) == "DR";
            run += dr ? amt : -amt;
            r.Add(GlDb.L(l[2]) > 0 ? VoucherLink(GlDb.L(l[2])) : null, "",
                shape(new object[] { GlDb.D(l[1]), GlDb.L(l[2]) == 0 ? "" : GlDb.S(l[2]), GlDb.S(l[5]), GlDb.S(l[3]), GlDb.S(l[4]), dr ? (object)(decimal)amt : null, dr ? null : (object)(decimal)amt, search == "" ? (object)(decimal)run : null }));
        }
        long closing = opening + (long)(tdr - tcr);
        if (search == "" && offset + size >= n)
            r.Add(null, "s", shape(new object[] { to, null, null, "Closing balance", null, null, null, (decimal)closing }));
        var t = new object[r.Cols.Count];
        int di = r.Cols.FindIndex(c => c.Key == "dr"), ci = r.Cols.FindIndex(c => c.Key == "cr"), bi = r.Cols.FindIndex(c => c.Key == "bal");
        t[di] = tdr; t[ci] = tcr; if (search == "") t[bi] = (decimal)closing;
        r.Totals = t;
        r.Cover.Add(new KeyValuePair<string, string>("Opening balance", GlFmt.Money(opening)));
        r.Cover.Add(new KeyValuePair<string, string>("Closing balance", GlFmt.Money(closing)));

        // Checks
        if (a.Kind == "PROVISIONAL" || a.Kind == "UNMAPPED")
            r.Checks.Add(Fail("NOT_IN_CHART", a.Code + " is not in the chart of accounts", Math.Abs(closing), n,
                a.Kind == "PROVISIONAL" ? "Shown under a provisional mapping (" + a.Category + "). The Bursar confirms or changes it on the account card." : "Not mapped: statements show it as Unclassified.",
                new GlFix("screen", "Open the account card", AccountLink(a.Key, from, to))));
        else r.Checks.Add(Pass("IN_CHART", a.Kind == "SUBLEDGER" ? "Subsidiary ledger: lines are grouped by their account type" : "The account is in the chart of accounts"));
        var vprm = new List<object>(prm);
        var vouchers = new HashSet<long>(GlDb.Table(x.C, null, "SELECT DISTINCT l.voucherNo" + FROM + where + " AND l.voucherNo > 0", vprm.ToArray()).Rows.Cast<DataRow>().Select(rw => GlDb.L(rw[0])));
        long nvd; int nvl;
        var bad = GlCalc.UnbalancedVouchers(null, new DateTime(2099, 12, 31), "WHOLE", out nvd, out nvl).Where(v => vouchers.Contains(v.VoucherNo)).ToList();
        if (bad.Count == 0) r.Checks.Add(Pass("VOUCHERS", "Every voucher behind these lines balances"));
        else r.Checks.Add(Fail("VOUCHERS", GlFmt.Plural(bad.Count, "voucher", "vouchers") + " behind these lines do not balance", bad.Sum(v => Math.Abs(v.Diff)), bad.Count,
            "Their other side is missing or carries a different number. Open a voucher from the list to see its lines.", new GlFix("report", "Unbalanced vouchers", ReportLink("R18", "from", GlFmt.Iso(from), "to", GlFmt.Iso(to)))));
        if (cashBook) r.Checks.Add(Info("NO_STATEMENT", "No bank statement is stored", "Bank reconciliation is not possible until statement lines are imported (plan, phase 2)."));
        return r;
    }

    // ── R03 Journal and voucher listing ─────────────────────────────

    private static GlReportDef R03()
    {
        var d = new GlReportDef
        {
            Code = "R03", Group = "Ledgers and vouchers", Title = "Journal and Voucher Listing",
            Description = "Every voucher number used in a period: lines, debit, credit and whether it balances.", Noun = "voucher"
        };
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Params.Add(new GlParam("source", "Source", "select") { Default = "" });
        d.Params.Add(new GlParam("teller", "Posted by", "text") { Help = "Part of the user name." });
        d.Params.Add(new GlParam("status", "Balance", "select") { Default = "", Options = new List<object> { new { v = "", t = "All vouchers" }, new { v = "unbalanced", t = "Only unbalanced" }, new { v = "balanced", t = "Only balanced" } } });
        d.Defaults = (x, ps) =>
        {
            var p = ps.First(q => q.Key == "source");
            p.Options = new List<object> { new { v = "", t = "All sources" } };
            foreach (DataRow r in GlDb.Table(x.C, null, "SELECT IFNULL(source_system,'(none)') s, COUNT(*) FROM fin_ledger GROUP BY s ORDER BY 2 DESC").Rows)
                p.Options.Add(new { v = GlDb.S(r[0]), t = GlDb.S(r[0]) + " (" + GlFmt.Count(GlDb.L(r[1])) + " lines)" });
        };
        d.Run = x =>
        {
            DateTime from, to; x.Range(out from, out to);
            var prm = new List<object> { "@f", from, "@t", to };
            string w = "voucherNo > 0 AND transactionDate BETWEEN @f AND @t";
            string src = x.Str("source"), teller = x.Str("teller"), st = x.Str("status");
            string having = "";
            if (src != "") { having += " AND SUM(IFNULL(source_system,'(none)') = @src) > 0"; prm.AddRange(new object[] { "@src", src }); }
            if (teller != "") { having += " AND SUM(teller LIKE @tl) > 0"; prm.AddRange(new object[] { "@tl", "%" + teller + "%" }); }
            if (st == "unbalanced") having += " AND dr <> cr"; else if (st == "balanced") having += " AND dr = cr";
            var r = new GlResult { Noun = "voucher", NounPlural = "vouchers", Subtitle = x.RangeText(from, to), Basis = "Lines in the period grouped by voucher number. A number used on several dates is one row." };
            r.Cols.Add(new GlCol("v", "Voucher", "code", 55)); r.Cols.Add(new GlCol("d0", "First date", "date", 60)); r.Cols.Add(new GlCol("d1", "Last date", "date", 60));
            r.Cols.Add(new GlCol("n", "Lines", "count", 40).Summed()); r.Cols.Add(new GlCol("dr", "Debit", "money", 80)); r.Cols.Add(new GlCol("cr", "Credit", "money", 80));
            r.Cols.Add(new GlCol("diff", "Difference", "money", 80)); r.Cols.Add(new GlCol("src", "Sources", "text", 90)); r.Cols.Add(new GlCol("by", "Posted by", "text", 90));
            long unb = 0; decimal unbAmt = 0;
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT voucherNo, MIN(transactionDate), MAX(transactionDate), COUNT(*), SUM(IF(transactionType='DR', CAST(transaction_amount AS SIGNED), 0)) dr, SUM(IF(transactionType='CR', CAST(transaction_amount AS SIGNED), 0)) cr, " +
                "GROUP_CONCAT(DISTINCT IFNULL(source_system,'(none)') SEPARATOR ', '), GROUP_CONCAT(DISTINCT teller SEPARATOR ', ') FROM fin_ledger WHERE " + w +
                " GROUP BY voucherNo HAVING 1=1" + having + " ORDER BY MIN(transactionDate), voucherNo", prm.ToArray()).Rows)
            {
                decimal dr = GlDb.M(q[4]), cr = GlDb.M(q[5]);
                if (dr != cr) { unb++; unbAmt += Math.Abs(dr - cr); }
                r.Add(VoucherLink(GlDb.L(q[0])), "", GlDb.S(q[0]), GlDb.D(q[1]), GlDb.D(q[2]), GlDb.L(q[3]), dr, cr, dr - cr, GlAudit.Cut(GlDb.S(q[6]), 120), GlAudit.Cut(GlDb.S(q[7]), 120));
            }
            if (unb == 0) r.Checks.Add(Pass("BALANCED", "Every voucher listed balances"));
            else r.Checks.Add(Fail("UNBALANCED", GlFmt.Plural(unb, "voucher does", "vouchers do") + " not balance", unbAmt, unb, "Amount is the sum of their differences, ignoring sign.", new GlFix("report", "Unbalanced vouchers", ReportLink("R18", "from", GlFmt.Iso(from), "to", GlFmt.Iso(to)))));
            long nov = GlDb.L(GlDb.Scalar(x.C, null, "SELECT COUNT(*) FROM fin_ledger WHERE (voucherNo IS NULL OR voucherNo = 0) AND transactionDate BETWEEN @f AND @t", "@f", from, "@t", to));
            if (nov > 0) r.Checks.Add(Fail("NO_VOUCHER", GlFmt.Plural(nov, "line has", "lines have") + " no voucher number in the period", 0, nov, "They are not listed here. See Finance Warnings W04.", new GlFix("screen", "Finance Warnings", "AccountsWarnings.aspx?rule=W04")));
            return r;
        };
        return d;
    }

    // ── R04 Income and expenditure ──────────────────────────────────

    private static GlReportDef R04()
    {
        var d = new GlReportDef
        {
            Code = "R04", Group = "Statements", Title = "Income and Expenditure",
            Description = "Income and expenditure for a period by account or category, with an optional comparison period.", Noun = "account"
        };
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Params.Add(new GlParam("compare", "Compare with", "select") { Default = "prior", Options = new List<object> { new { v = "", t = "No comparison" }, new { v = "prior", t = "The same dates a year earlier" } } });
        d.Params.Add(new GlParam("level", "Level", "select") { Default = "account", Options = new List<object> { new { v = "account", t = "Account" }, new { v = "category", t = "Sub-category" } } });
        d.Run = x =>
        {
            DateTime from, to; x.Range(out from, out to);
            bool cmp = x.Str("compare") == "prior", byCat = x.Str("level") == "category";
            var cur = GlCalc.Movement(x.S, from, to);
            var prv = cmp ? GlCalc.Movement(x.S, from.AddYears(-1), to.AddYears(-1)) : new Dictionary<string, long>();
            var r = new GlResult { Structured = true, Noun = "account", NounPlural = "accounts", Subtitle = x.RangeText(from, to) + (cmp ? ", compared with " + x.RangeText(from.AddYears(-1), to.AddYears(-1)) : "") };
            r.Basis = "Movement in the period on income and expense accounts: chart accounts plus codes provisionally mapped to Income or Expense. Income is credit less debit; expenditure is debit less credit.";
            r.Cols.Add(new GlCol("code", byCat ? "" : "Code", "code", 60) { Hidden = byCat });
            r.Cols.Add(new GlCol("name", byCat ? "Sub-category" : "Account", "text", 220));
            r.Cols.Add(new GlCol("cur", "This period", "money", 90));
            r.Cols.Add(new GlCol("prv", "Comparison", "money", 90) { Hidden = !cmp });
            r.Cols.Add(new GlCol("chg", "Change", "money", 90) { Hidden = !cmp });
            decimal ti = 0, tip = 0, te = 0, tep = 0;
            foreach (string sec in new[] { "Income", "Expense" })
            {
                int sign = sec == "Income" ? -1 : 1;
                var keys = x.S.Accounts.Values.Where(a => a.Category == sec && (a.Kind == "CHART" || a.Kind == "PROVISIONAL") && (cur.ContainsKey(a.Key) || prv.ContainsKey(a.Key)))
                    .OrderBy(a => a.SubCategory).ThenBy(a => a.Code).ToList();
                r.Add(null, "h", null, sec == "Income" ? "Income" : "Expenditure", null, null, null);
                decimal sc = 0, sp = 0;
                IEnumerable<IGrouping<string, GlAcct>> groups = byCat ? keys.GroupBy(a => a.SubCategory ?? "") : keys.GroupBy(a => a.Key);
                foreach (var g in groups)
                {
                    decimal c = g.Sum(a => { long v; return cur.TryGetValue(a.Key, out v) ? (decimal)(sign * v) : 0m; });
                    decimal p = g.Sum(a => { long v; return prv.TryGetValue(a.Key, out v) ? (decimal)(sign * v) : 0m; });
                    if (c == 0 && p == 0) continue;
                    GlAcct first = g.First();
                    r.Add(byCat ? ReportLink("R04", "from", GlFmt.Iso(from), "to", GlFmt.Iso(to), "compare", cmp ? "prior" : "", "level", "account") : AccountLink(first.Key, from, to), "",
                        byCat ? "" : first.Code, byCat ? (g.Key == "" ? "(none)" : g.Key) : first.Name + (first.Kind == "PROVISIONAL" ? " (provisional)" : ""), c, cmp ? (object)p : null, cmp ? (object)(c - p) : null);
                    sc += c; sp += p;
                }
                r.Add(null, "s", null, sec == "Income" ? "Total income" : "Total expenditure", sc, cmp ? (object)sp : null, cmp ? (object)(sc - sp) : null);
                if (sec == "Income") { ti = sc; tip = sp; } else { te = sc; tep = sp; }
            }
            r.Add(null, "t", null, ti - te >= 0 ? "Surplus for the period" : "Deficit for the period", ti - te, cmp ? (object)(tip - tep) : null, cmp ? (object)((ti - te) - (tip - tep)) : null);
            r.NoTotals = true;

            // Checks
            var wrong = new List<string>(); decimal wrongAmt = 0;
            foreach (var kv in cur)
            {
                GlAcct a = x.S.Accounts[kv.Key];
                if (a.Kind != "CHART" && a.Kind != "PROVISIONAL") continue;
                if ((a.Category == "Income" && kv.Value > 0) || (a.Category == "Expense" && kv.Value < 0 && !a.Contra)) { wrong.Add(a.Code); wrongAmt += Math.Abs(kv.Value); }
            }
            if (wrong.Count == 0) r.Checks.Add(Pass("SIDE", "Every income and expense account moved on its usual side"));
            else r.Checks.Add(Fail("SIDE", GlFmt.Plural(wrong.Count, "account moved", "accounts moved") + " on the unexpected side in the period", wrongAmt, wrong.Count,
                "An income account with net debits or an expense account with net credits: " + string.Join(", ", wrong.Take(12)) + (wrong.Count > 12 ? " and others" : "") + ". Usually a reversal or a misposting.", new GlFix("report", "Trial balance", ReportLink("R01", "from", GlFmt.Iso(from), "to", GlFmt.Iso(to)))));
            var prov = cur.Where(kv => x.S.Accounts[kv.Key].Kind == "PROVISIONAL" && (x.S.Accounts[kv.Key].Category == "Income" || x.S.Accounts[kv.Key].Category == "Expense")).ToList();
            if (prov.Count > 0)
                r.Checks.Add(new GlCheck { code = "PROVISIONAL", title = "Included under a provisional mapping", status = "info", amount = prov.Sum(kv => Math.Abs(kv.Value)), count = prov.Count,
                    cause = string.Join(", ", prov.Select(kv => kv.Key)) + " are missing from the chart and are classified provisionally. AC6007 is Functional Fees income.", fix = new GlFix("report", "Chart of accounts", ReportLink("R20")) });
            var uncl = cur.Where(kv => { GlAcct a = x.S.Accounts[kv.Key]; return a.Category == "Unclassified" || a.Category == "Suspense"; }).ToList();
            if (uncl.Count > 0)
                r.Checks.Add(Fail("UNCLASSIFIED", "Movement on unclassified or suspense codes is not in this statement", uncl.Sum(kv => Math.Abs(kv.Value)), uncl.Count,
                    "Codes: " + string.Join(", ", uncl.Select(kv => kv.Key).Take(15)) + ". Some may be income or expense; map them to include them.", new GlFix("report", "Chart of accounts", ReportLink("R20", "status", "unclassified"))));
            return r;
        };
        return d;
    }

    // ── R05 Statement of financial position ─────────────────────────

    private static GlReportDef R05()
    {
        var d = new GlReportDef
        {
            Code = "R05", Group = "Statements", Title = "Statement of Financial Position",
            Description = "Assets, liabilities and equity as at a date, with the accumulated surplus and anything not classified shown openly.", Noun = "account"
        };
        d.Params.Add(new GlParam("to", "As at", "date") { Role = "asat", Required = true });
        d.Params.Add(new GlParam("cto", "Compare with (as at)", "date") { Help = "Optional." });
        d.Run = x =>
        {
            DateTime to = x.DateOr("to", DateTime.Today); DateTime? cto = x.Date("cto");
            GlCalc.Position p = GlCalc.FinancialPosition(x.S, to), q = cto.HasValue ? GlCalc.FinancialPosition(x.S, cto.Value) : null;
            var qd = q == null ? new Dictionary<string, long>() : q.Lines.ToDictionary(kv => kv.Key.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            var r = new GlResult { Structured = true, NoTotals = true, Subtitle = "As at " + GlFmt.Date(to) + (cto.HasValue ? ", compared with " + GlFmt.Date(cto.Value) : "") };
            r.Basis = "Closing balances of the whole ledger. No year has ever been closed to retained earnings, so all income less expenditure to date is shown as the accumulated surplus. Student, supplier and fee subsidiary ledgers appear as their own lines because they have no control accounts.";
            r.Cols.Add(new GlCol("code", "Code", "code", 70)); r.Cols.Add(new GlCol("name", "Account", "text", 220));
            r.Cols.Add(new GlCol("amt", "Amount", "money", 95)); r.Cols.Add(new GlCol("cmp", "Comparison", "money", 95) { Hidden = !cto.HasValue });
            Func<string, long> qv = k => { long v; return qd.TryGetValue(k, out v) ? v : 0; };
            foreach (var sec in new[] { new[] { "Assets", "Assets", "Total assets" }, new[] { "Liabilities", "Liabilities", "Total liabilities" }, new[] { "Equity", "Equity", "Total equity" } })
            {
                r.Add(null, "h", null, sec[1], null, null);
                var lines = p.Lines.Where(kv => kv.Key.Category == sec[0]).ToList();
                var keys = new HashSet<string>(lines.Select(kv => kv.Key.Key), StringComparer.OrdinalIgnoreCase);
                if (q != null) lines.AddRange(q.Lines.Where(kv => kv.Key.Category == sec[0] && !keys.Contains(kv.Key.Key)).Select(kv => new KeyValuePair<GlAcct, long>(kv.Key, 0)));
                foreach (var kv in lines.OrderBy(kv => kv.Key.Kind == "SUBLEDGER" ? 1 : 0).ThenBy(kv => kv.Key.Code))
                {
                    if (kv.Value == 0 && qv(kv.Key.Key) == 0) continue;
                    r.Add(AccountLink(kv.Key.Key, null, to), "", kv.Key.Kind == "SUBLEDGER" ? "" : kv.Key.Code, kv.Key.Name + (kv.Key.Kind == "PROVISIONAL" ? " (provisional)" : ""), (decimal)kv.Value, cto.HasValue ? (object)(decimal)qv(kv.Key.Key) : null);
                }
                long tot = sec[0] == "Assets" ? p.Assets : sec[0] == "Liabilities" ? p.Liabilities : p.Equity;
                long qtot = q == null ? 0 : (sec[0] == "Assets" ? q.Assets : sec[0] == "Liabilities" ? q.Liabilities : q.Equity);
                if (sec[0] == "Equity")
                {
                    r.Add(ReportLink("R04", "to", GlFmt.Iso(to)), "", "", "Accumulated surplus not yet closed to retained earnings", (decimal)p.Surplus, cto.HasValue ? (object)(decimal)q.Surplus : null);
                    tot += p.Surplus; if (q != null) qtot += q.Surplus;
                }
                r.Add(null, "s", null, sec[2], (decimal)tot, cto.HasValue ? (object)(decimal)qtot : null);
            }
            r.Add(null, "t", null, "Liabilities and equity", (decimal)(p.Liabilities + p.Equity + p.Surplus), q != null ? (object)(decimal)(q.Liabilities + q.Equity + q.Surplus) : null);
            r.Add(null, "h", null, "Not classified (debit positive)", null, null);
            foreach (var kv in p.Lines.Where(kv => kv.Key.Category == "Unclassified" || kv.Key.Category == "Suspense").OrderBy(kv => kv.Key.Code))
                if (kv.Value != 0) r.Add(AccountLink(kv.Key.Key, null, to), "", kv.Key.Code, kv.Key.Name, (decimal)kv.Value, cto.HasValue ? (object)(decimal)qv(kv.Key.Key) : null);
            r.Add(null, "s", null, "Total not classified", (decimal)p.Unclassified, q != null ? (object)(decimal)q.Unclassified : null);
            r.Add(null, "t", null, "Difference: assets and not classified, less liabilities and equity", (decimal)p.Difference, q != null ? (object)(decimal)q.Difference : null);

            GlCheck diff = GlChecks.Difference("SFP_DIFF", "Assets equal liabilities plus equity", null, to, "WHOLE", p.Difference);
            r.Checks.Add(diff);
            if (p.Unclassified != 0)
                r.Checks.Add(Fail("UNCLASSIFIED", "Balances not classified", Math.Abs(p.Unclassified), p.Lines.Count(kv => kv.Key.Category == "Unclassified" || kv.Key.Category == "Suspense"),
                    "Codes missing from the chart and mapped as Unclassified, plus the suspense account. Map them so they fall under assets, liabilities or equity.", new GlFix("report", "Chart of accounts", ReportLink("R20"))));
            r.Checks.Add(Info("NO_CLOSE", "No year has been closed", "Retained earnings (AC7008) holds only what was posted to it directly. The accumulated surplus line is all income less all expenditure to date."));
            return r;
        };
        return d;
    }

    // ── R06 Cash flow (direct) ──────────────────────────────────────

    private static GlReportDef R06()
    {
        var d = new GlReportDef
        {
            Code = "R06", Group = "Cash and bank", Title = "Cash Flow",
            Description = "Receipts and payments through bank and cash accounts, by what the other side of each voucher was.", Noun = "line"
        };
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Run = x =>
        {
            DateTime from, to; x.Range(out from, out to);
            var cash = GlCalc.CashAccounts(x.S);
            if (cash.Count == 0) throw new GlRefusal("No bank or cash accounts were found in the chart.");
            var cashSet = new HashSet<string>(cash.Select(a => a.Code), StringComparer.OrdinalIgnoreCase);
            var prm = new List<object> { "@f", from, "@t", to }; var names = new List<string>();
            for (int i = 0; i < cash.Count; i++) { names.Add("@c" + i); prm.Add("@c" + i); prm.Add(cash[i].Code); }
            string inCash = "(" + string.Join(",", names) + ")";
            // Counterpart of each voucher and day: the non-cash key with the largest amount.
            var counter = new Dictionary<string, string>();
            var best = new Dictionary<string, long>();
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT l.voucherNo, l.transactionDate, " + GlCalc.KeyExpr + " k, SUM(CAST(l.transaction_amount AS SIGNED)) a FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode " +
                "JOIN (SELECT DISTINCT voucherNo FROM fin_ledger WHERE accountcode IN " + inCash + " AND transactionDate BETWEEN @f AND @t AND voucherNo > 0) v ON v.voucherNo = l.voucherNo " +
                "WHERE l.transactionDate BETWEEN @f AND @t AND l.accountcode NOT IN " + inCash + " GROUP BY l.voucherNo, l.transactionDate, k", prm.ToArray()).Rows)
            {
                string vk = GlDb.S(q[0]) + "|" + GlFmt.Iso(q[1]); long a = GlDb.L(q[3]); long b;
                if (!best.TryGetValue(vk, out b) || a > b) { best[vk] = a; counter[vk] = GlDb.S(q[2]); }
            }
            var inflow = new Dictionary<string, decimal[]>(); var outflow = new Dictionary<string, decimal[]>();
            decimal unidentified = 0, total = 0;
            foreach (DataRow q in GlDb.Table(x.C, null,
                "SELECT l.voucherNo, l.transactionDate, l.transactionType, CAST(l.transaction_amount AS SIGNED) FROM fin_ledger l WHERE l.accountcode IN " + inCash + " AND l.transactionDate BETWEEN @f AND @t", prm.ToArray()).Rows)
            {
                string vk = GlDb.S(q[0]) + "|" + GlFmt.Iso(q[1]); string ck; string label;
                if (GlDb.L(q[0]) > 0 && counter.TryGetValue(vk, out ck))
                {
                    GlAcct a = x.S.Accounts[ck];
                    label = a.Kind == "SUBLEDGER" ? a.Name : a.Category + ": " + (string.IsNullOrEmpty(a.SubCategory) ? a.Name : a.SubCategory);
                }
                else
                {
                    label = "Unidentified (no other side in the voucher on the same day)";
                    unidentified += GlDb.M(q[3]);
                }
                bool dr = GlDb.S(q[2]) == "DR";
                var dict = dr ? inflow : outflow; decimal[] v;
                if (!dict.TryGetValue(label, out v)) { v = new decimal[2]; dict[label] = v; }
                v[0] += GlDb.M(q[3]); v[1]++;
                total += GlDb.M(q[3]);
            }
            long open = cash.Sum(a => GlCalc.Balance(x.S, a.Key, from.AddDays(-1))), close = cash.Sum(a => GlCalc.Balance(x.S, a.Key, to));
            var r = new GlResult { Structured = true, NoTotals = true, Subtitle = x.RangeText(from, to), Noun = "group", NounPlural = "groups" };
            r.Basis = "Bank and cash accounts are the chart's Cash and Bank Balances (" + string.Join(", ", cash.Select(a => a.Code)) + "). Each cash line is classified by the largest other line in the same voucher on the same day.";
            r.Cols.Add(new GlCol("what", "Item", "text", 260)); r.Cols.Add(new GlCol("n", "Lines", "count", 50).NoSum()); r.Cols.Add(new GlCol("amt", "Amount", "money", 100));
            r.Add(null, "s", "Opening bank and cash", null, (decimal)open);
            decimal ti = 0, to2 = 0;
            r.Add(null, "h", "Receipts", null, null);
            foreach (var kv in inflow.OrderByDescending(kv => kv.Value[0])) { r.Add(null, "", kv.Key, (long)kv.Value[1], kv.Value[0]); ti += kv.Value[0]; }
            r.Add(null, "s", "Total receipts", null, ti);
            r.Add(null, "h", "Payments", null, null);
            foreach (var kv in outflow.OrderByDescending(kv => kv.Value[0])) { r.Add(null, "", kv.Key, (long)kv.Value[1], -kv.Value[0]); to2 += kv.Value[0]; }
            r.Add(null, "s", "Total payments", null, -to2);
            r.Add(null, "s", "Net movement", null, ti - to2);
            r.Add(null, "t", "Closing bank and cash", null, (decimal)close);
            if (open + (long)(ti - to2) == close) r.Checks.Add(Pass("MOVE", "Opening plus net movement equals closing bank and cash"));
            else r.Checks.Add(Fail("MOVE", "Opening plus net movement does not equal closing", Math.Abs(open + ti - to2 - close), 0, "Tell MIS: the cash lines and the balances disagree.", null));
            if (unidentified == 0) r.Checks.Add(Pass("UNIDENTIFIED", "Every cash line has an identified other side"));
            else r.Checks.Add(Fail("UNIDENTIFIED", "Cash lines with no other side in their voucher", unidentified, 0,
                (total == 0 ? 0 : Math.Round(unidentified * 100 / total, 1)).ToString("0.0", CultureInfo.InvariantCulture) + "% of cash movement by value. Collections and payments posted the bank side under a different voucher number (see Finance Warnings W01).",
                new GlFix("report", "Unbalanced vouchers", ReportLink("R18", "from", GlFmt.Iso(from), "to", GlFmt.Iso(to)))));
            return r;
        };
        return d;
    }

    // ── R07 Cash book ───────────────────────────────────────────────

    private static GlReportDef R07()
    {
        var d = new GlReportDef { Code = "R07", Group = "Cash and bank", Title = "Cash Book", Description = "Receipts and payments of one bank or cash account with a running balance.", Noun = "line" };
        d.Params.Add(new GlParam("account", "Bank or cash account", "select") { Required = true });
        d.Params.Add(PYear()); d.Params.Add(PFrom()); d.Params.Add(PTo());
        d.Defaults = (x, ps) =>
        {
            var p = ps.First(q => q.Key == "account");
            var cash = GlCalc.CashAccounts(x.S);
            p.Options = cash.Select(a => (object)new { v = a.Code, t = a.Code + " " + a.Name }).ToList();
            if (cash.Count > 0) p.Default = cash[0].Code;
        };
        d.Run = x =>
        {
            string k = x.Str("account");
            if (!GlCalc.CashAccounts(x.S).Any(a => a.Code == k)) throw new GlRefusal("Choose a bank or cash account.");
            return Statement(x, k, null, true);
        };
        return d;
    }
}
