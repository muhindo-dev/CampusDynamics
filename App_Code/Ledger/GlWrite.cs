using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;

// =====================================================================
//  General Ledger: the only writer (plan section 1).
//  Every method is one transaction on the module's own tables with one
//  gl_audit row. None of these touches a finance table; posting an
//  adjusting entry (GlAdjust) is the single exception, and it only adds.
// =====================================================================

public static class GlWrite
{
    private static string Need(string reason, int min)
    {
        reason = (reason ?? "").Trim();
        if (reason.Length < min) throw new GlRefusal("Give a reason of at least " + min + " characters. It is kept with the record.");
        return reason.Length > 1000 ? reason.Substring(0, 1000) : reason;
    }

    private static void Event(MySqlConnection c, MySqlTransaction tx, long id, string type, string detail, long? count, decimal? amount)
    {
        GlDb.Exec(c, tx, "INSERT INTO gl_warning_event (warning_id, event_type, detail, record_count, amount, actor, created_at) VALUES (@i,@t,@d,@n,@a,@u,NOW())",
            "@i", id, "@t", type, "@d", GlAudit.Cut(detail, 2000), "@n", count.HasValue ? (object)(int)Math.Min(count.Value, int.MaxValue) : DBNull.Value,
            "@a", amount.HasValue ? (object)amount.Value : DBNull.Value, "@u", GlAudit.Cut(GlAccess.User(), 100));
    }

    private static DataRow LockWarning(MySqlConnection c, MySqlTransaction tx, long id)
    {
        DataRow r = GlDb.Table(c, tx, "SELECT id, rule_code, scope_key, title, status, record_count, amount, assigned_to FROM gl_warning WHERE id = @i FOR UPDATE", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
        if (r == null) throw new GlRefusal("That warning no longer exists. Reload the page.");
        return r;
    }

    private static void Done() { System.Web.HttpRuntime.Cache.Remove("gl:badge"); }

    // ── Warnings ────────────────────────────────────────────────────

    /// <summary>Accepts a warning as known, with a reason. It stops counting towards the health score until it grows.</summary>
    public static void Acknowledge(long id, string reason)
    {
        reason = Need(reason, 15);
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow w = LockWarning(c, tx, id);
            string st = GlDb.S(w["status"]);
            if (st == "FIXED") throw new GlRefusal("This warning is fixed; there is nothing to acknowledge.");
            if (st == "ACKNOWLEDGED") throw new GlRefusal("This warning is already acknowledged.");
            GlDb.Exec(c, tx, "UPDATE gl_warning SET status='ACKNOWLEDGED', ack_by=@u, ack_at=NOW(), ack_reason=@r, ack_count=record_count, ack_amount=amount WHERE id=@i",
                "@u", GlAudit.Cut(GlAccess.User(), 100), "@r", reason, "@i", id);
            Event(c, tx, id, "ACKNOWLEDGED", reason, GlDb.L(w["record_count"]), GlDb.M(w["amount"]));
            GlAudit.Write(c, tx, "WARNING", id.ToString(CultureInfo.InvariantCulture), "ACKNOWLEDGE", new { status = st }, new { status = "ACKNOWLEDGED", count = GlDb.L(w["record_count"]), amount = GlDb.M(w["amount"]) }, reason);
            tx.Commit();
        }
        Done();
    }

    /// <summary>Withdraws an acknowledgement: the warning is open again.</summary>
    public static void Reopen(long id, string reason)
    {
        reason = Need(reason, 10);
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow w = LockWarning(c, tx, id);
            string st = GlDb.S(w["status"]);
            if (st != "ACKNOWLEDGED") throw new GlRefusal("Only an acknowledged warning can be reopened.");
            GlDb.Exec(c, tx, "UPDATE gl_warning SET status='OPEN' WHERE id=@i", "@i", id);
            Event(c, tx, id, "REOPENED", reason, null, null);
            GlAudit.Write(c, tx, "WARNING", id.ToString(CultureInfo.InvariantCulture), "REOPEN", new { status = st }, new { status = "OPEN" }, reason);
            tx.Commit();
        }
        Done();
    }

    public static void Assign(long id, string user, string note)
    {
        user = (user ?? "").Trim();
        if (user.Length > 100) user = user.Substring(0, 100);
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow w = LockWarning(c, tx, id);
            if (user != "" && GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM campus_dynamics.my_aspnet_users WHERE name = @u", "@u", user)) == 0)
                throw new GlRefusal("No user named " + user + " was found.");
            string before = GlDb.S(w["assigned_to"]);
            GlDb.Exec(c, tx, "UPDATE gl_warning SET assigned_to=@u, assigned_at=IF(@u IS NULL, NULL, NOW()) WHERE id=@i", "@u", user == "" ? (object)DBNull.Value : user, "@i", id);
            Event(c, tx, id, "ASSIGNED", (user == "" ? "Unassigned" : "Assigned to " + user) + (string.IsNullOrEmpty(note) ? "" : ". " + note.Trim()), null, null);
            GlAudit.Write(c, tx, "WARNING", id.ToString(CultureInfo.InvariantCulture), "ASSIGN", new { assigned = before }, new { assigned = user }, note);
            tx.Commit();
        }
    }

    public static void Note(long id, string text)
    {
        text = Need(text, 5);
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            LockWarning(c, tx, id);
            Event(c, tx, id, "NOTE", text, null, null);
            GlAudit.Write(c, tx, "WARNING", id.ToString(CultureInfo.InvariantCulture), "NOTE", null, new { note = text }, null);
            tx.Commit();
        }
    }

    // ── Record-level acceptance (one voucher, one duplicate group, one line) ──

    public static readonly string[] RecordRules = { "W01", "W05", "W09", "W15", "W18" };

    public static long AckRecord(string rule, string key, string reason, decimal? amount)
    {
        if (!RecordRules.Contains(rule)) throw new GlRefusal("Records of this rule cannot be accepted one by one.");
        key = (key ?? "").Trim();
        if (key == "" || key.Length > 200) throw new GlRefusal("Choose a record.");
        reason = Need(reason, 15);
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            long n = GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM gl_record_ack WHERE rule_code=@r AND record_key=@k AND is_active=1 FOR UPDATE", "@r", rule, "@k", key));
            if (n > 0) throw new GlRefusal("This record is already accepted.");
            long id = GlDb.Insert(c, tx, "INSERT INTO gl_record_ack (rule_code, record_key, reason, amount, is_active, created_by, created_at) VALUES (@r,@k,@why,@a,1,@u,NOW())",
                "@r", rule, "@k", key, "@why", reason, "@a", amount.HasValue ? (object)amount.Value : DBNull.Value, "@u", GlAudit.Cut(GlAccess.User(), 100));
            GlAudit.Write(c, tx, "RECORD_ACK", id.ToString(CultureInfo.InvariantCulture), "ACCEPT", null, new { rule, key, amount }, reason);
            tx.Commit();
            return id;
        }
    }

    public static void RevokeRecordAck(long id, string reason)
    {
        reason = Need(reason, 10);
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            DataRow r = GlDb.Table(c, tx, "SELECT rule_code, record_key, is_active FROM gl_record_ack WHERE id=@i FOR UPDATE", "@i", id).Rows.Cast<DataRow>().FirstOrDefault();
            if (r == null || GlDb.I(r[2]) == 0) throw new GlRefusal("That acceptance is no longer active.");
            GlDb.Exec(c, tx, "UPDATE gl_record_ack SET is_active=0, revoked_by=@u, revoked_at=NOW(), revoked_reason=@why WHERE id=@i", "@u", GlAudit.Cut(GlAccess.User(), 100), "@why", reason, "@i", id);
            GlAudit.Write(c, tx, "RECORD_ACK", id.ToString(CultureInfo.InvariantCulture), "REVOKE", new { rule = GlDb.S(r[0]), key = GlDb.S(r[1]) }, null, reason);
            tx.Commit();
        }
    }

    public static DataRow RecordAck(MySqlConnection c, string rule, string key)
    {
        return GlDb.Table(c, null, "SELECT id, reason, created_by, created_at FROM gl_record_ack WHERE rule_code=@r AND record_key=@k AND is_active=1", "@r", rule, "@k", key).Rows.Cast<DataRow>().FirstOrDefault();
    }

    // ── Presentation mapping of codes missing from the chart ────────

    public static readonly string[] MapCategories = { "Assets", "Liabilities", "Equity", "Income", "Expense", "Suspense", "Unclassified" };

    /// <summary>Sets or confirms how a code missing from the chart is presented. Never changes fin_subaccounts.</summary>
    public static void MapAccount(string code, string name, string category, string subcategory, bool confirm, string reason)
    {
        code = (code ?? "").Trim(); name = (name ?? "").Trim(); subcategory = (subcategory ?? "").Trim();
        if (code == "" || code.StartsWith("SUB:")) throw new GlRefusal("Choose a code missing from the chart.");
        if (!MapCategories.Contains(category)) throw new GlRefusal("Choose a category.");
        if (name.Length < 3) throw new GlRefusal("Give the account a name.");
        reason = Need(reason, 15);
        using (var c = GlDb.Write())
        using (var tx = c.BeginTransaction())
        {
            if (GlDb.L(GlDb.Scalar(c, tx, "SELECT COUNT(*) FROM fin_subaccounts WHERE AccountCode=@c", "@c", code)) > 0)
                throw new GlRefusal(code + " is in the chart of accounts; its classification comes from the chart.");
            DataRow b = GlDb.Table(c, tx, "SELECT account_name, category, subcategory, is_provisional FROM gl_account_map WHERE accountcode=@c FOR UPDATE", "@c", code).Rows.Cast<DataRow>().FirstOrDefault();
            object before = b == null ? null : (object)new { name = GlDb.S(b[0]), category = GlDb.S(b[1]), subcategory = GlDb.S(b[2]), provisional = GlDb.I(b[3]) == 1 };
            string u = GlAudit.Cut(GlAccess.User(), 100);
            if (b == null)
                GlDb.Exec(c, tx, "INSERT INTO gl_account_map (accountcode, account_name, category, subcategory, is_provisional, basis, confirmed_by, confirmed_at, created_by, created_at) VALUES (@c,@n,@cat,@sub,@p,@why,@cb,@ca,@u,NOW())",
                    "@c", code, "@n", GlAudit.Cut(name, 150), "@cat", category, "@sub", subcategory == "" ? (object)DBNull.Value : GlAudit.Cut(subcategory, 100), "@p", confirm ? 0 : 1, "@why", GlAudit.Cut(reason, 300),
                    "@cb", confirm ? (object)u : DBNull.Value, "@ca", confirm ? (object)DateTime.Now : DBNull.Value, "@u", u);
            else
                GlDb.Exec(c, tx, "UPDATE gl_account_map SET account_name=@n, category=@cat, subcategory=@sub, is_provisional=@p, basis=@why, confirmed_by=IF(@p=0,@u,NULL), confirmed_at=IF(@p=0,NOW(),NULL), updated_by=@u, updated_at=NOW() WHERE accountcode=@c",
                    "@c", code, "@n", GlAudit.Cut(name, 150), "@cat", category, "@sub", subcategory == "" ? (object)DBNull.Value : GlAudit.Cut(subcategory, 100), "@p", confirm ? 0 : 1, "@why", GlAudit.Cut(reason, 300), "@u", u);
            GlAudit.Write(c, tx, "ACCOUNT_MAP", code, b == null ? "CREATE" : confirm ? "CONFIRM" : "CHANGE", before, new { name, category, subcategory, provisional = !confirm }, reason);
            tx.Commit();
        }
        GlCalc.Forget();
    }
}
