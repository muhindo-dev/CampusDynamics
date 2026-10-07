using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger: adjusting entries (plan sections 2 and 3).
//  The ONLY place this work adds rows to fin_ledger. Nothing is edited or
//  deleted: a mistake is corrected by another adjusting entry.
//
//  Modes
//    BALANCED  a standalone entry, debit equals credit, under its own
//              voucher number (base + adjustment id; no classic sequence
//              reaches that range, so the number is never shared).
//    COMPLETE  lines added under an existing unbalanced voucher so that it
//              balances exactly. This is what reduces the trial balance
//              difference; a balanced entry cannot.
//
//  Flow  DRAFT -> PENDING (maker submits) -> POSTED (a different person
//        with the approve slug) or REJECTED; DRAFT/PENDING -> CANCELLED.
//  Posting is one InnoDB transaction: lock, re-validate, check sign-off,
//  insert, assert row counts and balance, audit, commit (or roll back).
// =====================================================================

public class GlAdjLine
{
    public string key, member, side, particulars;
    public long amount;
}

public static class GlAdjust
{
    public static readonly string[] Purposes =
    {
        "Complete an unbalanced voucher", "Reverse a duplicate posting", "Clear the suspense account", "Reclassify between accounts",
        "Year-end closing entry", "Reverse an earlier adjusting entry", "Other correction"
    };

    private static readonly Dictionary<string, string> SubLedgerType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "SUB:STUDENTS", "Student" }, { "SUB:FOEFees", "FOEFees" }, { "SUB:FSTEADFees", "FSTEADFees" }, { "SUB:FBMFees", "FBMFees" }, { "SUB:FSSAHFees", "FSSAHFees" },
        { "SUB:BursaryFees", "BursaryFees" }, { "SUB:Supplier", "Supplier" }, { "SUB:SalaryAdvance", "Salary Advance" }, { "SUB:Gratuity", "Gratuity" }, { "SUB:Sponsor", "Sponsor" }
    };

    public static long VoucherBase { get { return (long)GlSettings.Dec("adjust_voucher_base", 900000000); } }

    // ── Resolving a line to the ledger's code and account type ──────

    public class Resolved { public string Code, Type, Key, Display; }

    public static Resolved Resolve(GlCalc.Snapshot s, MySqlConnection c, MySqlTransaction tx, string key, string member)
    {
        GlAcct a;
        if (string.IsNullOrEmpty(key) || !s.Accounts.TryGetValue(key, out a)) throw new GlRefusal("Choose an account for every line.");
        if (a.Kind == "UNMAPPED") throw new GlRefusal(a.Code + " is not in the chart and not mapped. Map it on its account card first.");
        if (a.Kind != "SUBLEDGER") return new Resolved { Code = a.Code, Type = "Chart Account", Key = a.Key, Display = a.Code + " " + a.Name };
        member = (member ?? "").Trim();
        if (member == "") throw new GlRefusal("A line on " + a.Name.ToLowerInvariant() + " needs the member: a registration number or supplier code.");
        string type = SubLedgerType[a.Key];
        long known = GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM fin_ledger WHERE accountcode=@m AND account_type IN (@t, IF(@t='Student','-',@t)) LIMIT 1", "@m", member, "@t", type));
        if (known == 0 && a.Key == "SUB:STUDENTS") known = GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM campus_dynamics.acad_student WHERE regno=@m", "@m", member));
        if (known == 0 && a.Key == "SUB:Supplier") known = GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM inv_supplierdetails WHERE TRIM(SupplierCode)=@m", "@m", member));
        if (known == 0) throw new GlRefusal(member + " is not a known member of " + a.Name.ToLowerInvariant() + ".");
        return new Resolved { Code = member, Type = type, Key = a.Key, Display = member + " (" + a.Name + ")" };
    }

    private static List<GlAdjLine> ParseLines(object o)
    {
        var l = new List<GlAdjLine>();
        var arr = o as object[] ?? (o as System.Collections.ArrayList ?? new System.Collections.ArrayList()).ToArray();
        foreach (object item in arr)
        {
            var d = item as Dictionary<string, object>; if (d == null) continue;
            long amt; long.TryParse((FaJson.Str(d, "amount") ?? "").Replace(",", "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out amt);
            var line = new GlAdjLine { key = (FaJson.Str(d, "key") ?? "").Trim(), member = (FaJson.Str(d, "member") ?? "").Trim(), side = (FaJson.Str(d, "side") ?? "").Trim().ToUpperInvariant(), amount = amt, particulars = (FaJson.Str(d, "particulars") ?? "").Trim() };
            if (line.key == "" && line.amount == 0 && line.particulars == "") continue;
            l.Add(line);
        }
        return l;
    }

    private static void ValidateLines(List<GlAdjLine> lines)
    {
        if (lines.Count == 0) throw new GlRefusal("Add at least one line.");
        if (lines.Count > 200) throw new GlRefusal("An adjusting entry has at most 200 lines.");
        for (int i = 0; i < lines.Count; i++)
        {
            GlAdjLine l = lines[i];
            if (l.side != "DR" && l.side != "CR") throw new GlRefusal("Line " + (i + 1) + ": choose debit or credit.");
            if (l.amount <= 0) throw new GlRefusal("Line " + (i + 1) + ": the amount must be a whole number of shillings above zero.");
            if (l.amount > 100000000000L) throw new GlRefusal("Line " + (i + 1) + ": the amount is too large.");
            if (l.particulars.Length < 5) throw new GlRefusal("Line " + (i + 1) + ": write particulars of at least 5 characters.");
            if (l.particulars.Length > 300) l.particulars = l.particulars.Substring(0, 300);
        }
    }

    // ── Numbering ───────────────────────────────────────────────────

    private static string YearTag(List<GlCalc.FinYear> years, DateTime d)
    {
        GlCalc.FinYear y = GlCalc.YearOf(years, d);
        return y != null ? y.Start.Year + "-" + (y.End.Year % 100).ToString("00", CultureInfo.InvariantCulture) : d.Year.ToString(CultureInfo.InvariantCulture);
    }

    private static string NextNo(MySqlConnection c, MySqlTransaction tx, string tag)
    {
        GlDb.Exec(c, tx, "INSERT INTO gl_sequence (seq_key, next_no) VALUES (@k, LAST_INSERT_ID(1)) ON DUPLICATE KEY UPDATE next_no = LAST_INSERT_ID(next_no + 1)", "@k", "ADJ/" + tag);
        long n = GlDb.L(GlDb.Scalar(c, tx, "SELECT LAST_INSERT_ID()"));
        return "ADJ/" + tag + "/" + n.ToString("0000", CultureInfo.InvariantCulture);
    }

    // ── Voucher state (for COMPLETE mode) ──────────────────────────

    /// <summary>Debit less credit of every line under a voucher number now (locked when inside a transaction).</summary>
    public static long VoucherDiff(MySqlConnection c, MySqlTransaction tx, long v, bool lockRows, out int lines)
    {
        DataRow r = GlDb.Table(c, tx, "SELECT COUNT(*), IFNULL(SUM(" + GlCalc.NetBare + "),0) FROM fin_ledger WHERE voucherNo = @v" + (lockRows ? " FOR UPDATE" : ""), "@v", v).Rows[0];
        lines = GlDb.I(r[0]);
        return GlDb.L(r[1]);
    }

    private static void CheckModeRule(MySqlConnection c, MySqlTransaction tx, string mode, long? voucher, List<GlAdjLine> lines, bool lockRows)
    {
        long dr = lines.Where(l => l.side == "DR").Sum(l => l.amount), cr = lines.Where(l => l.side == "CR").Sum(l => l.amount);
        if (mode == "BALANCED")
        {
            if (lines.Count < 2) throw new GlRefusal("A balanced entry needs at least two lines.");
            if (dr != cr) throw new GlRefusal("Debit (" + GlFmt.Money(dr) + ") must equal credit (" + GlFmt.Money(cr) + ").");
        }
        else
        {
            if (!voucher.HasValue || voucher.Value <= 0) throw new GlRefusal("Choose the voucher this entry completes.");
            int n; long diff = VoucherDiff(c, tx, voucher.Value, lockRows, out n);
            if (n == 0) throw new GlRefusal("Voucher " + voucher.Value + " has no lines in the ledger.");
            if (diff == 0) throw new GlRefusal("Voucher " + voucher.Value + " already balances. Use a balanced entry instead.");
            if (dr - cr != -diff) throw new GlRefusal("Voucher " + voucher.Value + " is out by " + GlFmt.Money(diff) + ". The lines must add " + (diff > 0 ? "a net credit" : "a net debit") + " of exactly " + GlFmt.Money(Math.Abs(diff)) + "; they add " + GlFmt.Money(dr - cr) + " (debit less credit).");
        }
    }

    // ── Reading ─────────────────────────────────────────────────────

    public static object Get(long id)
    {
        GlCalc.Snapshot s = GlCalc.Get(false);
        using (var c = GlDb.Read())
        {
            DataRow h = GlDb.Table(c, null, "SELECT * FROM gl_adjustment WHERE id=@i", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
            if (h == null) throw new GlRefusal("That adjusting entry does not exist.");
            var lines = GlDb.Table(c, null, "SELECT line_no, accountcode, account_type, dr_cr, amount, particulars, ledger_tid FROM gl_adjustment_line WHERE adj_id=@i ORDER BY line_no", "@i", id).Rows.Cast<DataRow>()
                .Select(r =>
                {
                    string code = GlDb.S(r[1]), type = GlDb.S(r[2]);
                    string key = type == "Chart Account" ? code : SubLedgerType.Where(kv => kv.Value == type).Select(kv => kv.Key).FirstOrDefault() ?? code;
                    GlAcct a; s.Accounts.TryGetValue(key, out a);
                    bool sub = type != "Chart Account";
                    return new { key, member = sub ? code : "", name = a == null ? code : (sub ? a.Name : a.Code + " " + a.Name), side = GlDb.S(r[3]), amount = GlDb.L(r[4]), amountText = GlFmt.Money(GlDb.M(r[4])), particulars = GlDb.S(r[5]), ledgerTid = GlDb.L(r[6]) };
                }).ToList();
            var audit = GlDb.Table(c, null, "SELECT action, actor, created_at, IFNULL(reason,'') FROM gl_audit WHERE entity='ADJUSTMENT' AND entity_id=@i ORDER BY id", "@i", id.ToString(CultureInfo.InvariantCulture)).Rows.Cast<DataRow>()
                .Select(r => new { action = GlDb.S(r[0]), by = GlDb.S(r[1]), when = GlFmt.When(r[2]), reason = GlDb.S(r[3]) }).ToList();
            return new
            {
                id = GlDb.L(h["id"]), no = GlDb.S(h["adj_no"]), status = GlDb.S(h["status"]), mode = GlDb.S(h["mode"]), purpose = GlDb.S(h["purpose"]), reason = GlDb.S(h["reason"]),
                entryDate = GlFmt.Iso(h["entry_date"]), entryDateText = GlFmt.Date(h["entry_date"]), warningId = h["warning_id"] is DBNull ? (long?)null : GlDb.L(h["warning_id"]),
                voucher = h["related_voucher"] is DBNull ? (long?)null : GlDb.L(h["related_voucher"]), total = GlFmt.Money(GlDb.M(h["total_amount"])),
                createdBy = GlDb.S(h["created_by"]), createdAt = GlFmt.When(h["created_at"]), submittedAt = GlFmt.When(h["submitted_at"]),
                decidedBy = GlDb.S(h["decided_by"]), decidedAt = GlFmt.When(h["decided_at"]), decisionNote = GlDb.S(h["decision_note"]), overrideNote = GlDb.S(h["override_note"]),
                postedVoucher = h["posted_voucher"] is DBNull ? (long?)null : GlDb.L(h["posted_voucher"]), postedAt = GlFmt.When(h["posted_at"]), version = GlDb.I(h["row_version"]),
                lines, audit, mine = string.Equals(GlDb.S(h["created_by"]), GlAccess.User(), StringComparison.OrdinalIgnoreCase)
            };
        }
    }

    public static List<object> List(string status)
    {
        using (var c = GlDb.Read())
            return GlDb.Table(c, null,
                "SELECT id, adj_no, status, mode, purpose, entry_date, total_amount, created_by, created_at, IFNULL(decided_by,''), posted_voucher, related_voucher, (SELECT COUNT(*) FROM gl_adjustment_line l WHERE l.adj_id = a.id) " +
                "FROM gl_adjustment a" + (string.IsNullOrEmpty(status) ? "" : " WHERE status = @s") + " ORDER BY id DESC LIMIT 500", "@s", status).Rows.Cast<DataRow>()
                .Select(r => (object)new
                {
                    id = GlDb.L(r[0]), no = GlDb.S(r[1]), status = GlDb.S(r[2]), mode = GlDb.S(r[3]), purpose = GlDb.S(r[4]), entryDate = GlFmt.Date(r[5]), total = GlFmt.Money(GlDb.M(r[6])),
                    by = GlDb.S(r[7]), created = GlFmt.When(r[8]), decidedBy = GlDb.S(r[9]), posted = r[10] is DBNull ? "" : GlDb.S(r[10]), voucher = r[11] is DBNull ? "" : GlDb.S(r[11]), lines = GlDb.I(r[12])
                }).ToList();
    }

    /// <summary>Effect of the lines on each account, on the voucher they complete and on the trial balance difference. Nothing is written.</summary>
    public static object Preview(string mode, long? voucher, string entryDate, object linesObj)
    {
        GlCalc.Snapshot s = GlCalc.Get(false);
        var lines = ParseLines(linesObj);
        var rows = new List<object>(); var problems = new List<string>();
        using (var c = GlDb.Read())
        {
            foreach (var g in lines.Where(l => l.key != "").GroupBy(l => l.key + "|" + l.member))
            {
                GlAdjLine f = g.First(); GlAcct a; s.Accounts.TryGetValue(f.key, out a);
                long now;
                if (a != null && a.Kind == "SUBLEDGER" && f.member != "")
                {
                    var prm = new List<object>();
                    string w = GlReports.KeyWhere(f.key, f.member, prm);
                    now = GlDb.L(GlDb.Scalar(c, null, "SELECT IFNULL(SUM(" + GlCalc.NetExpr + "),0) FROM fin_ledger l LEFT JOIN fin_subaccounts s ON s.AccountCode=l.accountcode WHERE " + w, prm.ToArray()));
                }
                else now = GlCalc.Balance(s, f.key, new DateTime(2099, 12, 31));
                long move = g.Sum(l => l.side == "DR" ? l.amount : l.side == "CR" ? -l.amount : 0);
                rows.Add(new { account = a == null ? f.key : (a.Kind == "SUBLEDGER" ? f.member + " (" + a.Name + ")" : a.Code + " " + a.Name), now = GlFmt.Money(now), move = GlFmt.Money(move), after = GlFmt.Money(now + move) });
            }
            long dr = lines.Where(l => l.side == "DR").Sum(l => l.amount), cr = lines.Where(l => l.side == "CR").Sum(l => l.amount);
            GlCalc.Totals tb = GlCalc.Total(s, null, new DateTime(2099, 12, 31), "WHOLE");
            object v = null;
            if (mode == "COMPLETE" && voucher.HasValue)
            {
                int n; long diff = VoucherDiff(c, null, voucher.Value, false, out n);
                v = new { no = voucher.Value, lines = n, now = GlFmt.Money(diff), after = GlFmt.Money(diff + dr - cr), balances = diff + dr - cr == 0 };
                if (diff + dr - cr != 0) problems.Add("Voucher " + voucher.Value + " would still be out by " + GlFmt.Money(diff + dr - cr) + ".");
            }
            else if (dr != cr) problems.Add("Debit and credit differ by " + GlFmt.Money(dr - cr) + ".");
            return new { rows, dr = GlFmt.Money(dr), cr = GlFmt.Money(cr), diff = GlFmt.Money(dr - cr), tbNow = GlFmt.Money(tb.Diff), tbAfter = GlFmt.Money(tb.Diff + dr - cr), voucher = v, problems };
        }
    }

    // ── Writing ─────────────────────────────────────────────────────

    /// <summary>Creates or updates a draft. Returns its id.</summary>
    public static long SaveDraft(Dictionary<string, object> d)
    {
        long id = FaJson.Int(d, "id");
        int version = FaJson.Int(d, "version");
        string mode = FaJson.Str(d, "mode") == "COMPLETE" ? "COMPLETE" : "BALANCED";
        string purpose = (FaJson.Str(d, "purpose") ?? "").Trim(), reason = (FaJson.Str(d, "reason") ?? "").Trim();
        DateTime? entry = FaJson.Date(d, "entryDate");
        int? warningId = FaJson.IntN(d, "warningId"); int? voucher = FaJson.IntN(d, "voucher");
        if (purpose.Length < 5) throw new GlRefusal("Choose or describe the purpose.");
        if (reason.Length < 20) throw new GlRefusal("Explain the reason in at least 20 characters. The approver and the auditors read it.");
        if (!entry.HasValue) throw new GlRefusal("Choose the entry date.");
        if (entry.Value.Date > DateTime.Today) throw new GlRefusal("The entry date cannot be in the future.");
        if (mode == "COMPLETE" && (!voucher.HasValue || voucher.Value <= 0)) throw new GlRefusal("Choose the voucher this entry completes.");
        var lines = ParseLines(d.ContainsKey("lines") ? d["lines"] : null);
        ValidateLines(lines);
        GlCalc.Snapshot s = GlCalc.Get(false);
        string user = GlAccess.User();
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            var years = GlCalc.Years(c);
            var resolved = lines.Select(l => Resolve(s, c, tx, l.key, l.member)).ToList();
            long total = Math.Max(lines.Where(l => l.side == "DR").Sum(l => l.amount), lines.Where(l => l.side == "CR").Sum(l => l.amount));
            object before = null;
            if (id == 0)
            {
                string no = NextNo(c, tx, YearTag(years, entry.Value));
                id = GlDb.Insert(c, tx,
                    "INSERT INTO gl_adjustment (adj_no, status, mode, purpose, reason, entry_date, warning_id, related_voucher, total_amount, created_by, created_at, row_version) VALUES (@n,'DRAFT',@m,@p,@r,@d,@w,@v,@t,@u,NOW(),1)",
                    "@n", no, "@m", mode, "@p", GlAudit.Cut(purpose, 250), "@r", GlAudit.Cut(reason, 2000), "@d", entry.Value, "@w", warningId.HasValue ? (object)warningId.Value : DBNull.Value,
                    "@v", voucher.HasValue ? (object)voucher.Value : DBNull.Value, "@t", total, "@u", GlAudit.Cut(user, 100));
            }
            else
            {
                DataRow h = GlDb.Table(c, tx, "SELECT status, created_by, row_version, purpose, reason, entry_date, mode, total_amount FROM gl_adjustment WHERE id=@i FOR UPDATE", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
                if (h == null) throw new GlRefusal("That adjusting entry does not exist.");
                if (GlDb.S(h[0]) != "DRAFT") throw new GlRefusal("Only a draft can be changed.");
                if (!string.Equals(GlDb.S(h[1]), user, StringComparison.OrdinalIgnoreCase)) throw new GlRefusal("Only the person who started the draft can change it.");
                if (GlDb.I(h[2]) != version) throw new GlRefusal("Someone changed this draft since you opened it. Reload it.");
                before = new { purpose = GlDb.S(h[3]), reason = GlDb.S(h[4]), entryDate = GlFmt.Iso(h[5]), mode = GlDb.S(h[6]), total = GlDb.M(h[7]) };
                GlDb.Exec(c, tx, "UPDATE gl_adjustment SET mode=@m, purpose=@p, reason=@r, entry_date=@d, warning_id=@w, related_voucher=@v, total_amount=@t, row_version=row_version+1 WHERE id=@i",
                    "@m", mode, "@p", GlAudit.Cut(purpose, 250), "@r", GlAudit.Cut(reason, 2000), "@d", entry.Value, "@w", warningId.HasValue ? (object)warningId.Value : DBNull.Value,
                    "@v", voucher.HasValue ? (object)voucher.Value : DBNull.Value, "@t", total, "@i", id);
                GlDb.Exec(c, tx, "DELETE FROM gl_adjustment_line WHERE adj_id=@i", "@i", id);
            }
            for (int i = 0; i < lines.Count; i++)
                GlDb.Exec(c, tx, "INSERT INTO gl_adjustment_line (adj_id, line_no, accountcode, account_type, dr_cr, amount, particulars) VALUES (@a,@n,@c,@t,@s,@m,@p)",
                    "@a", id, "@n", i + 1, "@c", resolved[i].Code, "@t", resolved[i].Type, "@s", lines[i].side, "@m", lines[i].amount, "@p", lines[i].particulars);
            GlAudit.Write(c, tx, "ADJUSTMENT", id.ToString(CultureInfo.InvariantCulture), before == null ? "CREATE" : "EDIT", before,
                new { mode, purpose, entryDate = GlFmt.Iso(entry.Value), voucher, total, lines = lines.Select((l, i) => new { account = resolved[i].Code, type = resolved[i].Type, l.side, l.amount }).ToList() }, reason);
            tx.Commit();
        }
        return id;
    }

    private static DataRow Lock(MySqlConnection c, MySqlTransaction tx, long id, int version)
    {
        DataRow h = GlDb.Table(c, tx, "SELECT * FROM gl_adjustment WHERE id=@i FOR UPDATE", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
        if (h == null) throw new GlRefusal("That adjusting entry does not exist.");
        if (version > 0 && GlDb.I(h["row_version"]) != version) throw new GlRefusal("This entry changed since you opened it. Reload it.");
        return h;
    }

    /// <summary>Stored lines as posted: member holds the ledger code and key holds the ledger account type.</summary>
    private static List<GlAdjLine> LinesOf(MySqlConnection c, MySqlTransaction tx, long id)
    {
        return GlDb.Table(c, tx, "SELECT accountcode, account_type, dr_cr, amount, particulars FROM gl_adjustment_line WHERE adj_id=@i ORDER BY line_no", "@i", id).Rows.Cast<DataRow>()
            .Select(r => new GlAdjLine { member = GlDb.S(r[0]), key = GlDb.S(r[1]), side = GlDb.S(r[2]), amount = GlDb.L(r[3]), particulars = GlDb.S(r[4]) }).ToList();
    }

    public static void Submit(long id, int version)
    {
        string user = GlAccess.User();
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow h = Lock(c, tx, id, version);
            if (GlDb.S(h["status"]) != "DRAFT") throw new GlRefusal("Only a draft can be submitted.");
            if (!string.Equals(GlDb.S(h["created_by"]), user, StringComparison.OrdinalIgnoreCase)) throw new GlRefusal("Only the person who started the draft can submit it.");
            var lines = LinesOf(c, tx, id);
            CheckModeRule(c, tx, GlDb.S(h["mode"]), h["related_voucher"] is DBNull ? (long?)null : GlDb.L(h["related_voucher"]), lines, false);
            GlDb.Exec(c, tx, "UPDATE gl_adjustment SET status='PENDING', submitted_at=NOW(), row_version=row_version+1 WHERE id=@i", "@i", id);
            GlAudit.Write(c, tx, "ADJUSTMENT", id.ToString(CultureInfo.InvariantCulture), "SUBMIT", new { status = "DRAFT" }, new { status = "PENDING" }, null);
            tx.Commit();
        }
    }

    public static void ReturnToDraft(long id, int version, string reason)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length < 10) throw new GlRefusal("Give a reason of at least 10 characters.");
        string user = GlAccess.User();
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow h = Lock(c, tx, id, version);
            if (GlDb.S(h["status"]) != "PENDING") throw new GlRefusal("Only a submitted entry can be taken back.");
            if (!string.Equals(GlDb.S(h["created_by"]), user, StringComparison.OrdinalIgnoreCase)) throw new GlRefusal("Only the person who submitted it can take it back.");
            GlDb.Exec(c, tx, "UPDATE gl_adjustment SET status='DRAFT', submitted_at=NULL, row_version=row_version+1 WHERE id=@i", "@i", id);
            GlAudit.Write(c, tx, "ADJUSTMENT", id.ToString(CultureInfo.InvariantCulture), "TAKE_BACK", new { status = "PENDING" }, new { status = "DRAFT" }, reason);
            tx.Commit();
        }
    }

    public static void Cancel(long id, int version, string reason)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length < 10) throw new GlRefusal("Give a reason of at least 10 characters.");
        string user = GlAccess.User();
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow h = Lock(c, tx, id, version);
            string st = GlDb.S(h["status"]);
            if (st != "DRAFT" && st != "PENDING") throw new GlRefusal("Only a draft or a submitted entry can be cancelled.");
            if (!string.Equals(GlDb.S(h["created_by"]), user, StringComparison.OrdinalIgnoreCase)) throw new GlRefusal("Only the person who started it can cancel it. An approver rejects instead.");
            GlDb.Exec(c, tx, "UPDATE gl_adjustment SET status='CANCELLED', decision_note=@r, row_version=row_version+1 WHERE id=@i", "@r", GlAudit.Cut(reason, 1000), "@i", id);
            GlAudit.Write(c, tx, "ADJUSTMENT", id.ToString(CultureInfo.InvariantCulture), "CANCEL", new { status = st }, new { status = "CANCELLED" }, reason);
            tx.Commit();
        }
    }

    public static void Reject(long id, int version, string reason)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length < 15) throw new GlRefusal("Give the maker a reason of at least 15 characters.");
        string user = GlAccess.User();
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow h = Lock(c, tx, id, version);
            if (GlDb.S(h["status"]) != "PENDING") throw new GlRefusal("Only a submitted entry can be rejected.");
            if (string.Equals(GlDb.S(h["created_by"]), user, StringComparison.OrdinalIgnoreCase)) throw new GlRefusal("You started this entry, so you cannot decide on it. Cancel it instead.");
            GlDb.Exec(c, tx, "UPDATE gl_adjustment SET status='REJECTED', decided_by=@u, decided_at=NOW(), decision_note=@r, row_version=row_version+1 WHERE id=@i", "@u", GlAudit.Cut(user, 100), "@r", GlAudit.Cut(reason, 1000), "@i", id);
            GlAudit.Write(c, tx, "ADJUSTMENT", id.ToString(CultureInfo.InvariantCulture), "REJECT", new { status = "PENDING" }, new { status = "REJECTED" }, reason);
            tx.Commit();
        }
    }

    /// <summary>The signed-off period (month or year) that contains a date, if its latest action is a sign-off.</summary>
    public static string SignedOffPeriod(MySqlConnection c, MySqlTransaction tx, DateTime d)
    {
        var years = GlCalc.Years(c);
        GlCalc.FinYear y = GlCalc.YearOf(years, d);
        var keys = new List<string> { d.ToString("yyyy-MM", CultureInfo.InvariantCulture) };
        if (y != null) keys.Add("FY:" + GlFmt.Iso(y.Start));
        foreach (string k in keys)
        {
            DataRow r = GlDb.Table(c, tx, "SELECT action, created_at, actor FROM gl_period_signoff WHERE period_key=@k ORDER BY id DESC LIMIT 1", "@k", k).Rows.Cast<DataRow>().FirstOrDefault();
            if (r != null && GlDb.S(r[0]) == "SIGNED_OFF") return (k.StartsWith("FY:") ? "the financial year " + y.Label : d.ToString("MMMM yyyy", CultureInfo.InvariantCulture)) + " (signed off by " + GlDb.S(r[2]) + " on " + GlFmt.Date(r[1]) + ")";
        }
        return null;
    }

    public class PostResult { public long Voucher; public int Lines; public long LedgerBefore, LedgerAfter; public bool DryRun; public string No; }

    /// <summary>
    /// Approves and posts. A different person from the maker, holding the approve slug. One transaction:
    /// lock, re-validate, sign-off check, insert, assert counts and balance, audit; rolled back when dryRun.
    /// </summary>
    public static PostResult Approve(long id, int version, string note, string overrideNote, bool dryRun)
    {
        string user = GlAccess.User();
        GlCalc.Snapshot s = GlCalc.Get(false);
        var res = new PostResult { DryRun = dryRun };
        using (var c = GlDb.Write())
        {
            using (var tx = c.BeginTransaction())
            {
                DataRow h = Lock(c, tx, id, version);
                res.No = GlDb.S(h["adj_no"]);
                if (GlDb.S(h["status"]) != "PENDING") throw new GlRefusal("Only a submitted entry can be approved.");
                if (string.Equals(GlDb.S(h["created_by"]), user, StringComparison.OrdinalIgnoreCase)) throw new GlRefusal("You started this entry, so you cannot approve it. Another approver must.");
                string mode = GlDb.S(h["mode"]);
                long? related = h["related_voucher"] is DBNull ? (long?)null : GlDb.L(h["related_voucher"]);
                DateTime entry = GlDb.D(h["entry_date"]).Value;
                var lines = LinesOf(c, tx, id);
                if (lines.Count == 0) throw new GlRefusal("The entry has no lines.");

                // Re-validate every line against the chart and the members as they are now.
                foreach (GlAdjLine l in lines)
                {
                    string key = l.key == "Chart Account" ? l.member : SubLedgerType.Where(kv => kv.Value == l.key).Select(kv => kv.Key).First();
                    Resolve(s, c, tx, key, l.key == "Chart Account" ? null : l.member);
                    if (l.amount <= 0 || (l.side != "DR" && l.side != "CR")) throw new GlRefusal("A line has an invalid side or amount.");
                }

                string signed = SignedOffPeriod(c, tx, entry);
                overrideNote = (overrideNote ?? "").Trim();
                if (signed != null && overrideNote.Length < 15) throw new GlRefusal("The entry date falls in " + signed + ". To post into it, record why in at least 15 characters.");

                // The voucher number, and the rule that must hold for it.
                long v;
                if (mode == "COMPLETE")
                {
                    v = related.Value;
                    CheckModeRule(c, tx, mode, v, lines, true);
                }
                else
                {
                    CheckModeRule(c, tx, mode, null, lines, false);
                    v = VoucherBase + id;
                    if (GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM fin_ledger WHERE voucherNo=@v", "@v", v)) > 0) throw new GlRefusal("Voucher number " + v + " is already in use. Tell MIS.");
                }
                res.Voucher = v;
                int vBefore; long diffBefore = VoucherDiff(c, tx, v, true, out vBefore);
                res.LedgerBefore = GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM fin_ledger"));

                // Insert: new rows only, one per line.
                var tids = new List<long>();
                foreach (GlAdjLine l in lines)
                {
                    long tid = GlDb.Insert(c, tx,
                        "INSERT INTO fin_ledger (accountcode, account_type, transactionType, transaction_amount, particulars, voucherNo, RefNo, transactionDate, teller, timeLog, folio, source_system, trans_currency, actual_amount, forex_rate, ugx_amount) " +
                        "VALUES (@c,@t,@s,@a,@p,@v,@ref,@d,@u,NOW(),@ref,'GL_ADJUST','UGX',0,1,1)",
                        "@c", l.member, "@t", l.key, "@s", l.side, "@a", l.amount, "@p", GlAudit.Cut(l.particulars + " [" + res.No + "]", 350), "@v", v, "@ref", res.No, "@d", entry, "@u", GlAudit.Cut(user, 45));
                    tids.Add(tid);
                }

                // Assertions: exactly these rows were added, and the voucher now satisfies its rule.
                res.LedgerAfter = GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM fin_ledger"));
                int vAfter; long diffAfter = VoucherDiff(c, tx, v, false, out vAfter);
                long added = GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM fin_ledger WHERE voucherNo=@v AND RefNo=@ref AND source_system='GL_ADJUST'", "@v", v, "@ref", res.No));
                if (res.LedgerAfter - res.LedgerBefore != lines.Count || vAfter - vBefore != lines.Count || added != lines.Count)
                    throw new GlRefusal("The row count check failed (expected " + lines.Count + " new lines). Nothing was posted. Tell MIS.");
                if (diffAfter != 0)
                    throw new GlRefusal("After posting, voucher " + v + " would be out by " + GlFmt.Money(diffAfter) + ". Nothing was posted.");
                res.Lines = lines.Count;

                for (int i = 0; i < tids.Count; i++)
                    GlDb.Exec(c, tx, "UPDATE gl_adjustment_line SET ledger_tid=@t WHERE adj_id=@a AND line_no=@n", "@t", tids[i], "@a", id, "@n", i + 1);
                GlDb.Exec(c, tx, "UPDATE gl_adjustment SET status='POSTED', decided_by=@u, decided_at=NOW(), decision_note=@n, override_note=@o, posted_voucher=@v, posted_at=NOW(), row_version=row_version+1 WHERE id=@i",
                    "@u", GlAudit.Cut(user, 100), "@n", string.IsNullOrEmpty(note) ? (object)DBNull.Value : GlAudit.Cut(note, 1000), "@o", overrideNote == "" ? (object)DBNull.Value : GlAudit.Cut(overrideNote, 1000), "@v", v, "@i", id);
                GlAudit.Write(c, tx, "ADJUSTMENT", id.ToString(CultureInfo.InvariantCulture), "APPROVE_POST", new { status = "PENDING", voucherDiff = diffBefore, voucherLines = vBefore, ledgerRows = res.LedgerBefore },
                    new { status = "POSTED", voucher = v, voucherDiff = diffAfter, voucherLines = vAfter, ledgerRows = res.LedgerAfter, ledgerTids = tids, signedOffPeriod = signed, overrideNote }, note);

                if (dryRun) tx.Rollback(); else tx.Commit();
            }
        }
        if (!dryRun) { GlCalc.Forget(); System.Web.HttpRuntime.Cache.Remove("gl:badge"); }
        return res;
    }

    // ── Prepared drafts ─────────────────────────────────────────────

    /// <summary>A draft that reverses a posted adjusting entry, line by line.</summary>
    public static long DraftReversal(long id)
    {
        Dictionary<string, object> src;
        using (var c = GlDb.Read())
        {
            DataRow h = GlDb.Table(c, null, "SELECT adj_no, status, mode, related_voucher FROM gl_adjustment WHERE id=@i", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
            if (h == null || GlDb.S(h[1]) != "POSTED") throw new GlRefusal("Only a posted entry can be reversed.");
            var lines = LinesOf(c, null, id).Select(l => (object)new Dictionary<string, object>
            {
                { "key", l.key == "Chart Account" ? l.member : SubLedgerType.Where(kv => kv.Value == l.key).Select(kv => kv.Key).First() },
                { "member", l.key == "Chart Account" ? "" : l.member }, { "side", l.side == "DR" ? "CR" : "DR" }, { "amount", l.amount.ToString(CultureInfo.InvariantCulture) },
                { "particulars", "Reversal of " + GlDb.S(h[0]) + ": " + l.particulars }
            }).ToArray();
            src = new Dictionary<string, object>
            {
                { "mode", GlDb.S(h[2]) }, { "voucher", h[3] is DBNull ? null : (object)GlDb.I(h[3]) }, { "purpose", "Reverse an earlier adjusting entry" },
                { "reason", "Reverses " + GlDb.S(h[0]) + " in full. State why the original entry was wrong before submitting." },
                { "entryDate", GlFmt.Iso(DateTime.Today) }, { "lines", lines }
            };
        }
        if (GlDb.S(src["mode"]) == "COMPLETE") src["mode"] = "BALANCED";
        return SaveDraft(src);
    }

    /// <summary>
    /// The year-end closing entry for a financial year, as a draft: every income and expense account's balance at the year end
    /// (all history up to that day, since no year was ever closed) moved to retained earnings.
    /// </summary>
    public static long DraftYearEnd(GlCalc.FinYear y, string retainedEarnings)
    {
        GlCalc.Snapshot s = GlCalc.Get(true);
        GlAcct re;
        if (!s.Accounts.TryGetValue(retainedEarnings, out re) || re.Kind != "CHART" || re.Category != "Equity") throw new GlRefusal("Choose the retained earnings account (an equity account in the chart).");
        var bal = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        int e = GlCalc.DayInt(y.End);
        foreach (GlAgg g in s.Agg)
        {
            if (g.Day > e) continue;
            GlAcct a = s.Accounts[g.Key];
            if ((a.Kind != "CHART" && a.Kind != "PROVISIONAL") || (a.Category != "Income" && a.Category != "Expense")) continue;
            long v; bal.TryGetValue(g.Key, out v); bal[g.Key] = v + g.Dr - g.Cr;
        }
        var lines = new List<object>();
        long net = 0;
        foreach (var kv in bal.Where(kv => kv.Value != 0).OrderBy(kv => kv.Key))
        {
            GlAcct a = s.Accounts[kv.Key];
            lines.Add(new Dictionary<string, object> { { "key", a.Key }, { "member", "" }, { "side", kv.Value > 0 ? "CR" : "DR" }, { "amount", Math.Abs(kv.Value).ToString(CultureInfo.InvariantCulture) }, { "particulars", "Year-end close " + y.Label + ": " + a.Name } });
            net += kv.Value;
        }
        if (lines.Count == 0) throw new GlRefusal("Income and expense accounts are already at nil at " + GlFmt.Date(y.End) + ".");
        if (lines.Count > 199) throw new GlRefusal("Too many accounts for one entry (" + lines.Count + "). Tell MIS.");
        lines.Add(new Dictionary<string, object> { { "key", re.Key }, { "member", "" }, { "side", net > 0 ? "DR" : "CR" }, { "amount", Math.Abs(net).ToString(CultureInfo.InvariantCulture) },
            { "particulars", "Year-end close " + y.Label + ": " + (net > 0 ? "deficit" : "surplus") + " to retained earnings" } });
        return SaveDraft(new Dictionary<string, object>
        {
            { "mode", "BALANCED" }, { "purpose", "Year-end closing entry" }, { "entryDate", GlFmt.Iso(y.End) }, { "lines", lines.ToArray() },
            { "reason", "Closes income and expenditure for " + y.Label + " (" + y.DateLabel + ") to " + re.Code + " " + re.Name + ". No year has been closed before, so this includes every earlier balance. Prepared by Periods and close; review before submitting." }
        });
    }
}
