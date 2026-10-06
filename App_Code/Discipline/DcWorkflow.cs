using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: the case file and the status machine
//  (plan section 4). Every action is one transaction: the case row is
//  locked, the rule checked, the entry, sanction, letter and notice
//  written, the audit row added, then committed. Nothing is deleted or
//  edited; corrections are new entries (safety rules 1 and 2).
// =====================================================================

public static class DcEntries
{
    /// <summary>Appends one entry to the case file and moves the case's "last update". Returns the entry id.</summary>
    public static long Add(MySqlConnection c, MySqlTransaction tx, int caseId, string type, string title, string body, bool visible,
                           string statusBefore, string statusAfter, long? sanctionId, long? correctsId, object details, string opId, DateTime? entryAt)
    {
        long id = FaDb.Insert(c, tx,
            "INSERT INTO dc_entry (case_id, entry_type, entry_at, title, body, details_json, student_visible, status_before, status_after, sanction_id, corrects_entry_id, " +
            "recorded_by, recorded_role, recorded_at, interface, ip_address, client_op_id) " +
            "VALUES (@c,@t,@at,@ti,@b,@d,@v,@sb,@sa,@s,@co,@u,@r,NOW(),'EADMIN',@ip,@op)",
            "@c", caseId, "@t", type, "@at", entryAt.HasValue ? entryAt.Value : DateTime.Now, "@ti", DcAudit.Cut(title, 250),
            "@b", FaDb.NullIfEmpty(body), "@d", details == null ? (object)DBNull.Value : DcAudit.Cut(FaJson.Ser(details), 60000),
            "@v", visible ? 1 : 0, "@sb", FaDb.NullIfEmpty(statusBefore), "@sa", FaDb.NullIfEmpty(statusAfter),
            "@s", sanctionId.HasValue ? (object)sanctionId.Value : DBNull.Value, "@co", correctsId.HasValue ? (object)correctsId.Value : DBNull.Value,
            "@u", DcAccess.Username() == "" ? "system" : DcAccess.Username(), "@r", DcAccess.Role(), "@ip", DcAccess.Ip(),
            "@op", string.IsNullOrEmpty(opId) ? (object)DBNull.Value : DcAudit.Cut(opId, 40));
        FaDb.Exec(c, tx, "UPDATE dc_case SET last_entry_at=NOW() WHERE id=@c", "@c", caseId);
        DcAudit.Write(c, tx, "ENTRY", id, caseId, "ADD", null, new { type = type, title = title, visible = visible }, null, null);
        return id;
    }
}

public static class DcSanctions
{
    public static readonly string[] FollowEffects = { "CANCEL_PAPER", "CANCEL_SEMESTER", "FINE", "RESTITUTION" };

    public class Spec
    {
        public int TypeId;
        public decimal? Amount;
        public DateTime? From, To;
        public string Course = "", AcadYear = "", Terms = "";
        public int? Semester;
    }

    public static Spec Parse(Dictionary<string, object> d)
    {
        var s = new Spec();
        s.TypeId = FaJson.Int(d, "typeId");
        s.Amount = FaJson.Dec(d, "amount");
        s.From = FaJson.Date(d, "from");
        s.To = FaJson.Date(d, "to");
        s.Course = FaJson.Str(d, "course").ToUpperInvariant();
        s.AcadYear = FaJson.Str(d, "acadYear");
        s.Semester = FaJson.IntN(d, "semester");
        s.Terms = FaJson.Str(d, "terms");
        return s;
    }

    public static DataRow Type(MySqlConnection c, MySqlTransaction tx, int id)
    {
        DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_sanction_type WHERE id=@id", "@id", id);
        return t.Rows.Count == 0 ? null : t.Rows[0];
    }

    public static DataRow TypeByCode(MySqlConnection c, MySqlTransaction tx, string code)
    {
        DataTable t = FaDb.Table(c, tx, "SELECT * FROM dc_sanction_type WHERE code=@c AND is_active=1", "@c", code);
        return t.Rows.Count == 0 ? null : t.Rows[0];
    }

    /// <summary>Checks the fields a sanction type needs. Returns a sentence when something is missing.</summary>
    public static string Validate(DataRow type, Spec s, bool interim)
    {
        string name = FaDb.S(type["name"]);
        if (FaDb.I(type["is_active"]) != 1) return name + " is no longer in use.";
        if (FaDb.I(type["needs_amount"]) == 1 && (!s.Amount.HasValue || s.Amount.Value <= 0)) return "Give the amount for " + DcFmt.Lc(name) + ".";
        if (s.Amount.HasValue && s.Amount.Value > 100000000m) return "The amount for " + DcFmt.Lc(name) + " is too large.";
        string dates = FaDb.S(type["needs_dates"]);
        if (dates != "NONE" && !s.From.HasValue) return "Give the date " + DcFmt.Lc(name) + " starts.";
        if (dates == "FROM_TO" && !s.To.HasValue && !interim) return "Give the date " + DcFmt.Lc(name) + " ends.";
        if (s.From.HasValue && s.To.HasValue && s.To.Value < s.From.Value) return "The end date of " + DcFmt.Lc(name) + " is before its start.";
        if (s.From.HasValue && s.From.Value < DateTime.Today.AddYears(-1)) return "The start date of " + DcFmt.Lc(name) + " is too far in the past.";
        if (FaDb.I(type["needs_course"]) == 1 && s.Course == "") return "Give the course code whose result is cancelled.";
        if (FaDb.I(type["needs_semester"]) == 1 && (s.AcadYear == "" || !s.Semester.HasValue || s.Semester.Value < 1)) return "Give the academic year and semester for " + DcFmt.Lc(name) + ".";
        if (s.Terms.Length > 1000) return "Keep the terms of " + DcFmt.Lc(name) + " under 1,000 characters.";
        return null;
    }

    /// <summary>Records a sanction or interim measure. Effects are copied from the type, so later edits to the type do not change it.</summary>
    public static long Create(MySqlConnection c, MySqlTransaction tx, DataRow cs, DataRow type, string source, Spec s, long? replacesId)
    {
        string effects = FaDb.S(type["effects"]);
        bool follow = false;
        foreach (string e in FollowEffects) if (("," + effects + ",").Contains("," + e + ",")) follow = true;
        DateTime from = s.From.HasValue ? s.From.Value : DateTime.Today;
        long id = FaDb.Insert(c, tx,
            "INSERT INTO dc_sanction (case_id, regno, sanction_type_id, effects, source, amount, starts_on, ends_on, course_code, acad_year, semester, terms, status, follow_up, replaces_id, created_by, created_at) " +
            "VALUES (@c,@r,@t,@e,@src,@a,@f,@to,@cc,@y,@sem,@terms,'ACTIVE',@fu,@rep,@u,NOW())",
            "@c", FaDb.I(cs["id"]), "@r", FaDb.S(cs["regno"]), "@t", FaDb.I(type["id"]), "@e", effects, "@src", source,
            "@a", s.Amount.HasValue ? (object)s.Amount.Value : DBNull.Value, "@f", from, "@to", s.To.HasValue ? (object)s.To.Value : DBNull.Value,
            "@cc", FaDb.NullIfEmpty(DcAudit.Cut(s.Course, 25)), "@y", FaDb.NullIfEmpty(DcAudit.Cut(s.AcadYear, 9)),
            "@sem", s.Semester.HasValue && s.Semester.Value > 0 ? (object)s.Semester.Value : DBNull.Value, "@terms", FaDb.NullIfEmpty(s.Terms),
            "@fu", follow ? "PENDING" : "NONE", "@rep", replacesId.HasValue ? (object)replacesId.Value : DBNull.Value, "@u", DcAccess.Username());
        DcAudit.Write(c, tx, "SANCTION", id, FaDb.I(cs["id"]), "CREATE", null,
            new { type = FaDb.S(type["code"]), source = source, effects = effects, amount = s.Amount, from = from.ToString("yyyy-MM-dd"), to = s.To.HasValue ? s.To.Value.ToString("yyyy-MM-dd") : "", course = s.Course, terms = s.Terms },
            null, FaDb.S(type["name"]) + " recorded in " + FaDb.S(cs["case_no"]) + " (" + DcFmt.Source(source).ToLowerInvariant() + ")");
        return id;
    }

    public static void End(MySqlConnection c, MySqlTransaction tx, DataRow sanction, string status, string reason, long entryId)
    {
        int n = FaDb.Exec(c, tx,
            "UPDATE dc_sanction SET status=@s, ended_at=NOW(), ended_by=@u, ended_reason=@r, ended_entry_id=@e WHERE id=@id AND status='ACTIVE'",
            "@s", status, "@u", DcAccess.Username(), "@r", DcAudit.Cut(reason, 1000), "@e", entryId, "@id", FaDb.I(sanction["id"]));
        if (n != 1) throw new DcRefusal("That sanction has already ended. Reload the case.");
        DcAudit.Write(c, tx, "SANCTION", FaDb.I(sanction["id"]), FaDb.I(sanction["case_id"]), status == "LIFTED" ? "LIFT" : status,
            new { status = "ACTIVE" }, new { status = status }, reason, null);
    }

    /// <summary>"Suspension from 1 Nov 2026 to 30 Jun 2027", for letters and notices.</summary>
    public static string Describe(DataRow s, string typeName)
    {
        var sb = new StringBuilder(typeName);
        if (s["amount"] != DBNull.Value) sb.Append(" of UGX ").Append(DcFmt.Money(s["amount"]));
        if (FaDb.S(s["course_code"]) != "") sb.Append(" (").Append(FaDb.S(s["course_code"])).Append(FaDb.S(s["acad_year"]) == "" ? "" : ", " + FaDb.S(s["acad_year"]) + " semester " + FaDb.I(s["semester"])).Append(")");
        else if (FaDb.S(s["acad_year"]) != "") sb.Append(" (").Append(FaDb.S(s["acad_year"])).Append(" semester ").Append(FaDb.I(s["semester"])).Append(")");
        DateTime? f = FaDb.D(s["starts_on"]), t = FaDb.D(s["ends_on"]);
        string needs = s.Table.Columns.Contains("needs_dates") ? FaDb.S(s["needs_dates"]) : "FROM";
        if (needs != "NONE" && f.HasValue)
        {
            if (t.HasValue) sb.Append(" from ").Append(DcFmt.Date(f)).Append(" to ").Append(DcFmt.Date(t));
            else sb.Append(" with effect from ").Append(DcFmt.Date(f));
        }
        if (FaDb.S(s["terms"]) != "") sb.Append(". ").Append(FaDb.S(s["terms"]).TrimEnd('.'));
        return sb.ToString();
    }
}

public static class DcWorkflow
{
    private static readonly string[] PreDecision = { "REPORTED", "UNDER_INVESTIGATION", "HEARING_SCHEDULED", "SUMMONED", "HEARD" };
    private static bool In(string s, params string[] l) { return Array.IndexOf(l, s) >= 0; }

    // ── What the user can do on this case now (buttons; the server re-checks each one) ──
    public static object Actions(DataRow cs)
    {
        string st = FaDb.S(cs["status"]);
        bool manage = DcAccess.CanManageCase(cs);
        bool reporter = DcAccess.IsReporter(cs);
        bool hearing = DcAccess.Can(DcAccess.Hearing) || DcAccess.IsAdmin();
        bool decide = DcAccess.Can(DcAccess.Decide) || DcAccess.IsAdmin();
        bool appeal = DcAccess.Can(DcAccess.Appeal) || DcAccess.IsAdmin();
        bool restrict = DcAccess.Can(DcAccess.Restrict) || DcAccess.IsAdmin();
        bool open = !In(st, "CLOSED", "WITHDRAWN");
        bool minor = FaDb.S(cs["severity"]) == "MINOR";
        DateTime? deadline = FaDb.D(cs["appeal_deadline"]);
        return new
        {
            addNote = open && (manage || reporter) || (!open && manage),
            addFinding = manage,
            investigate = manage && st == "REPORTED",
            schedule = hearing && In(st, "REPORTED", "UNDER_INVESTIGATION"),
            summon = hearing && In(st, "REPORTED", "UNDER_INVESTIGATION", "HEARING_SCHEDULED"),
            adjourn = hearing && In(st, "HEARING_SCHEDULED", "SUMMONED"),
            recordHearing = decide && st == "SUMMONED",
            decide = decide && (st == "HEARD" || (minor && In(st, "REPORTED", "UNDER_INVESTIGATION"))),
            lodgeAppeal = manage && st == "DECIDED",
            lodgeLate = appeal,
            appealOpen = st == "DECIDED" && deadline.HasValue && deadline.Value >= DateTime.Today,
            decideAppeal = appeal && st == "UNDER_APPEAL",
            measure = restrict && (In(st, PreDecision) || st == "UNDER_APPEAL"),
            lift = restrict,
            vary = restrict,
            followUp = manage,
            letter = hearing || decide,
            notify = manage,
            close = manage && In(st, "DECIDED", "APPEAL_DECIDED"),
            withdraw = restrict && In(st, PreDecision),
            officer = manage && open,
            setRestricted = DcAccess.Current().SeeRestricted && (DcAccess.Can(DcAccess.Restricted) || DcAccess.IsAdmin()),
            recommend = open && (reporter || manage) && !restrict && In(st, PreDecision)
        };
    }

    public static List<object> SanctionChoices(MySqlConnection c, int caseTypeId)
    {
        var defs = new HashSet<int>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT sanction_type_id FROM dc_case_type_sanction WHERE case_type_id=@t AND is_active=1", "@t", caseTypeId).Rows) defs.Add(FaDb.I(r[0]));
        var l = new List<object>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT * FROM dc_sanction_type WHERE is_active=1 ORDER BY sort_order, name").Rows)
        {
            var d = new Dictionary<string, object>();
            foreach (var p in DcLookups.SanctionTypeJson(r).GetType().GetProperties()) d[p.Name] = p.GetValue(DcLookups.SanctionTypeJson(r), null);
            d["isDefault"] = defs.Contains(FaDb.I(r["id"]));
            l.Add(d);
        }
        return l;
    }

    // ── Shared steps ──────────────────────────────────────────────────

    private class Ctx : IDisposable
    {
        public MySqlConnection C;
        public MySqlTransaction Tx;
        public DataRow Case;
        public string Status { get { return FaDb.S(Case["status"]); } }
        public int Id { get { return FaDb.I(Case["id"]); } }
        public string Regno { get { return FaDb.S(Case["regno"]); } }
        public string No { get { return FaDb.S(Case["case_no"]); } }
        public List<long> Notices = new List<long>();
        public void Dispose() { try { if (Tx != null) Tx.Dispose(); } catch { } try { C.Dispose(); } catch { } }
    }

    /// <summary>Opens a transaction and locks the case, refusing when the user cannot see it or someone else changed it first.</summary>
    private static Ctx Begin(Dictionary<string, object> d)
    {
        int caseId = FaJson.Int(d, "caseId");
        var x = new Ctx();
        x.C = FaDb.Open();
        x.Tx = x.C.BeginTransaction();
        x.Case = DcAccess.VisibleCase(x.C, x.Tx, caseId, true);
        if (x.Case == null) { x.Dispose(); throw new DcRefusal("That case was not found."); }
        int? v = FaJson.IntN(d, "version");
        if (v.HasValue && v.Value > 0 && v.Value != FaDb.I(x.Case["row_version"]))
        { x.Dispose(); throw new DcRefusal("Someone else changed this case a moment ago. Reload it and try again."); }
        string op = FaJson.Str(d, "opId");
        if (op != "" && FaDb.Scalar(x.C, x.Tx, "SELECT 1 FROM dc_entry WHERE client_op_id=@o", "@o", DcAudit.Cut(op, 40)) != null)
        { x.Dispose(); throw new DcRefusal("This was already saved."); }
        return x;
    }

    private static object Commit(Ctx x, object result)
    {
        x.Tx.Commit();
        x.Tx = null;
        DcNotify.SendAsync(x.Notices);
        return result;
    }

    /// <summary>Changes case columns, bumps the version and audits the difference.</summary>
    private static void UpdateCase(Ctx x, Dictionary<string, object> sets, string action, string reason)
    {
        var sb = new StringBuilder("UPDATE dc_case SET ");
        var p = new List<object>();
        var before = new Dictionary<string, object>();
        foreach (var kv in sets)
        {
            sb.Append(kv.Key).Append("=@").Append(kv.Key).Append(", ");
            p.Add("@" + kv.Key); p.Add(kv.Value ?? DBNull.Value);
            before[kv.Key] = x.Case[kv.Key] == DBNull.Value ? null : x.Case[kv.Key];
        }
        sb.Append("updated_by=@ub, updated_at=NOW(), row_version=row_version+1 WHERE id=@cid");
        p.Add("@ub"); p.Add(DcAccess.Username()); p.Add("@cid"); p.Add(x.Id);
        FaDb.Exec(x.C, x.Tx, sb.ToString(), p.ToArray());
        DcAudit.Write(x.C, x.Tx, "CASE", x.Id, x.Id, action, before, sets, reason,
            "Disciplinary case " + x.No + ": " + action.ToLowerInvariant().Replace('_', ' '));
        foreach (var kv in sets) x.Case[kv.Key] = kv.Value ?? DBNull.Value;
    }

    private static void SetStatus(Ctx x, string to, string reason, Dictionary<string, object> extra)
    {
        var sets = extra ?? new Dictionary<string, object>();
        sets["status"] = to;
        UpdateCase(x, sets, "STATUS:" + to, reason);
    }

    private static string Need(Dictionary<string, object> d, string key, int min, string what)
    {
        string v = FaJson.Str(d, key);
        if (v.Length < min) throw new DcRefusal(what + (min > 1 ? " (at least " + min + " characters)." : "."));
        return v;
    }

    private static void Require(bool ok, string message) { if (!ok) throw new DcRefusal(message); }

    // ── Entries ───────────────────────────────────────────────────────

    /// <summary>NOTE, FINDING, STATEMENT, EVIDENCE or CORRECTION. Returns the entry id so files can be attached to it.</summary>
    public static object AddEntry(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            string type = FaJson.Str(d, "type").ToUpperInvariant();
            bool manage = DcAccess.CanManageCase(x.Case), reporter = DcAccess.IsReporter(x.Case);
            bool open = !In(x.Status, "CLOSED", "WITHDRAWN");
            string body = Need(d, "body", 5, "Write the entry");
            string title = FaJson.Str(d, "title");
            bool visible = FaJson.Bool(d, "visible");
            long? corrects = null;
            switch (type)
            {
                case "NOTE": case "STATEMENT": case "EVIDENCE":
                    Require(manage || (reporter && open), "You can add to this case only while it is open and you reported it, or with permission to manage it.");
                    break;
                case "FINDING":
                    Require(manage, "Only an officer managing the case can record a finding.");
                    break;
                case "CORRECTION":
                    int cid = FaJson.Int(d, "correctsId");
                    DataTable o = FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_entry WHERE id=@e AND case_id=@c", "@e", cid, "@c", x.Id);
                    Require(o.Rows.Count == 1, "Choose the entry to correct.");
                    Require(manage || string.Equals(FaDb.S(o.Rows[0]["recorded_by"]), DcAccess.Username(), StringComparison.OrdinalIgnoreCase),
                            "Only the person who made the entry, or an officer managing the case, can correct it.");
                    Require(FaDb.S(o.Rows[0]["interface"]) != "SYSTEM" || manage, "Only an officer managing the case can correct a system entry.");
                    corrects = cid;
                    visible = FaDb.I(o.Rows[0]["student_visible"]) == 1;
                    if (title == "") title = "Correction to: " + FaDb.S(o.Rows[0]["title"]);
                    break;
                default:
                    throw new DcRefusal("Choose the kind of entry.");
            }
            if (title == "") title = DcFmt.EntryType(type);
            long id = DcEntries.Add(x.C, x.Tx, x.Id, type, title, body, visible, null, null, null, corrects, null, FaJson.Str(d, "opId"), FaJson.Date(d, "date"));
            if (visible && FaJson.Bool(d, "notify"))
                x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "OTHER", "New entry in case " + x.No, title, null));
            return Commit(x, new { entryId = id });
        }
    }

    public static object Investigate(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.CanManageCase(x.Case), "You do not have permission to manage this case.");
            Require(x.Status == "REPORTED", "Only a reported case can be placed under investigation.");
            string note = Need(d, "note", 5, "Say what will be investigated");
            var sets = new Dictionary<string, object>();
            string officer = FaJson.Str(d, "officer");
            if (officer != "") sets["case_officer"] = DcAudit.Cut(officer, 100);
            SetStatus(x, "UNDER_INVESTIGATION", note, sets);
            DcEntries.Add(x.C, x.Tx, x.Id, "STATUS_CHANGE", "Placed under investigation", note + (officer == "" ? "" : "\nCase officer: " + officer), false, "REPORTED", "UNDER_INVESTIGATION", null, null, null, FaJson.Str(d, "opId"), null);
            return Commit(x, null);
        }
    }

    public static object SetOfficer(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.CanManageCase(x.Case), "You do not have permission to manage this case.");
            string officer = Need(d, "officer", 2, "Choose the case officer");
            var sets = new Dictionary<string, object>(); sets["case_officer"] = DcAudit.Cut(officer, 100);
            UpdateCase(x, sets, "OFFICER", FaJson.Str(d, "note"));
            DcEntries.Add(x.C, x.Tx, x.Id, "NOTE", "Case officer assigned", "Case officer: " + FaJson.Str(d, "officerName") + " (" + officer + ")" + (FaJson.Str(d, "note") == "" ? "" : "\n" + FaJson.Str(d, "note")), false, null, null, null, null, null, FaJson.Str(d, "opId"), null);
            return Commit(x, null);
        }
    }

    public static object SetRestricted(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Restricted) || DcAccess.IsAdmin(), "Only an officer who may see restricted cases can change this.");
            bool on = FaJson.Bool(d, "restricted");
            string reason = Need(d, "reason", 10, "Give the reason");
            var sets = new Dictionary<string, object>(); sets["is_restricted"] = on ? 1 : 0;
            UpdateCase(x, sets, on ? "RESTRICT" : "UNRESTRICT", reason);
            DcEntries.Add(x.C, x.Tx, x.Id, "NOTE", on ? "Case marked restricted" : "Case no longer restricted", reason, false, null, null, null, null, null, FaJson.Str(d, "opId"), null);
            return Commit(x, null);
        }
    }

    // ── Hearings ──────────────────────────────────────────────────────

    private static DateTime HearingAt(Dictionary<string, object> d)
    {
        DateTime at;
        if (!DateTime.TryParse(FaJson.Str(d, "at"), CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) throw new DcRefusal("Give the date and time of the hearing.");
        if (at < DateTime.Now.AddHours(-1)) throw new DcRefusal("The hearing date has already passed.");
        if (at > DateTime.Today.AddYears(1)) throw new DcRefusal("The hearing date is more than a year away.");
        return at;
    }

    /// <summary>Schedules a hearing; with summon=true also summons the student (letter and notice) in the same step.</summary>
    public static object ScheduleHearing(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Hearing) || DcAccess.IsAdmin(), "You do not have permission to schedule hearings.");
            bool summon = FaJson.Bool(d, "summon");
            Require(In(x.Status, "REPORTED", "UNDER_INVESTIGATION") || (summon && x.Status == "HEARING_SCHEDULED"),
                    x.Status == "HEARING_SCHEDULED" || x.Status == "SUMMONED" ? "A hearing is already scheduled. Adjourn it to change the date." : "A hearing cannot be scheduled at this stage.");
            if (x.Status == "HEARING_SCHEDULED") return Commit(x, SummonInside(x, d));
            DateTime at = HearingAt(d);
            string venue = Need(d, "venue", 2, "Give the venue");
            string panel = FaJson.Str(d, "panel");
            long hid = FaDb.Insert(x.C, x.Tx,
                "INSERT INTO dc_hearing (case_id, scheduled_at, venue, panel, status, created_by, created_at) VALUES (@c,@a,@v,@p,'SCHEDULED',@u,NOW())",
                "@c", x.Id, "@a", at, "@v", DcAudit.Cut(venue, 200), "@p", FaDb.NullIfEmpty(DcAudit.Cut(panel, 1000)), "@u", DcAccess.Username());
            DcAudit.Write(x.C, x.Tx, "HEARING", hid, x.Id, "SCHEDULE", null, new { at = at.ToString("yyyy-MM-dd HH:mm"), venue = venue, panel = panel }, null, null);
            string before = x.Status;
            var sets = new Dictionary<string, object>(); sets["hearing_at"] = at; sets["hearing_venue"] = DcAudit.Cut(venue, 200);
            SetStatus(x, "HEARING_SCHEDULED", null, sets);
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, "HEARING_SCHEDULED", "Hearing scheduled for " + at.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture),
                "Venue: " + venue + (panel == "" ? "" : "\nPanel: " + panel), false, before, "HEARING_SCHEDULED", null, null, null, FaJson.Str(d, "opId"), null);
            FaDb.Exec(x.C, x.Tx, "UPDATE dc_hearing SET scheduled_entry_id=@e WHERE id=@h", "@e", eid, "@h", hid);
            object res = null;
            if (summon) res = SummonInside(x, d);
            return Commit(x, res);
        }
    }

    public static object Summon(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Hearing) || DcAccess.IsAdmin(), "You do not have permission to summon students.");
            if (In(x.Status, "REPORTED", "UNDER_INVESTIGATION"))
            {
                x.Dispose();
                d["summon"] = true;
                return ScheduleHearing(d);
            }
            Require(x.Status == "HEARING_SCHEDULED", "The student can be summoned only once a hearing is scheduled.");
            return Commit(x, SummonInside(x, d));
        }
    }

    /// <summary>Issues the summons for the scheduled hearing. Minimum notice is 3 days unless a reason is given.</summary>
    private static object SummonInside(Ctx x, Dictionary<string, object> d)
    {
        DataTable h = FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_hearing WHERE case_id=@c AND status='SCHEDULED' ORDER BY id DESC LIMIT 1", "@c", x.Id);
        Require(h.Rows.Count == 1, "Schedule the hearing first.");
        DateTime at = FaDb.D(h.Rows[0]["scheduled_at"]).Value;
        int minDays = DcSettings.Int("summon_notice_days", 3);
        string shortReason = FaJson.Str(d, "shortNoticeReason");
        if ((at.Date - DateTime.Today).TotalDays < minDays && shortReason.Length < 10)
            throw new DcRefusal("The hearing is less than " + minDays + " days away. Give the reason for the short notice (at least 10 characters).");
        string before = x.Status;
        SetStatus(x, "SUMMONED", shortReason, null);
        var f = DcLetters.Fields(x.C, x.Tx, x.Case);
        f["hearing_date"] = at.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
        f["hearing_time"] = at.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();
        f["hearing_venue"] = FaDb.S(h.Rows[0]["venue"]);
        long letterId; long letterEntry;
        DcLetters.Issue(x.C, x.Tx, x.Case, "SUMMON", f, out letterId, out letterEntry);
        DcEntries.Add(x.C, x.Tx, x.Id, "SUMMON", "Summoned to a hearing on " + at.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture),
            "Venue: " + FaDb.S(h.Rows[0]["venue"]) + "." + (shortReason == "" ? "" : "\nShort notice: " + shortReason), true, before, "SUMMONED", null, null,
            new { hearingId = FaDb.I(h.Rows[0]["id"]), letterId = letterId }, null, null);
        x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "SUMMON", "You are summoned to a disciplinary hearing",
            "You are required to appear before the Students Disciplinary Committee on " + at.ToString("dddd d MMMM yyyy 'at' h:mm tt", CultureInfo.InvariantCulture) +
            " in " + FaDb.S(h.Rows[0]["venue"]) + " (case " + x.No + "). Your summons letter is on My Disciplinary Cases.", letterId));
        return new { letterId = letterId };
    }

    public static object Adjourn(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Hearing) || DcAccess.IsAdmin(), "You do not have permission to adjourn hearings.");
            Require(In(x.Status, "HEARING_SCHEDULED", "SUMMONED"), "There is no scheduled hearing to adjourn.");
            string reason = Need(d, "reason", 10, "Give the reason for the adjournment");
            DateTime at = HearingAt(d);
            string venue = Need(d, "venue", 2, "Give the venue");
            DataTable h = FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_hearing WHERE case_id=@c AND status='SCHEDULED' ORDER BY id DESC LIMIT 1 FOR UPDATE", "@c", x.Id);
            if (h.Rows.Count == 1)
            {
                FaDb.Exec(x.C, x.Tx, "UPDATE dc_hearing SET status='ADJOURNED', updated_by=@u, updated_at=NOW() WHERE id=@h", "@u", DcAccess.Username(), "@h", FaDb.I(h.Rows[0]["id"]));
                DcAudit.Write(x.C, x.Tx, "HEARING", FaDb.I(h.Rows[0]["id"]), x.Id, "ADJOURN", new { status = "SCHEDULED" }, new { status = "ADJOURNED" }, reason, null);
            }
            string panel = FaJson.Str(d, "panel"); if (panel == "" && h.Rows.Count == 1) panel = FaDb.S(h.Rows[0]["panel"]);
            long hid = FaDb.Insert(x.C, x.Tx,
                "INSERT INTO dc_hearing (case_id, scheduled_at, venue, panel, status, created_by, created_at) VALUES (@c,@a,@v,@p,'SCHEDULED',@u,NOW())",
                "@c", x.Id, "@a", at, "@v", DcAudit.Cut(venue, 200), "@p", FaDb.NullIfEmpty(DcAudit.Cut(panel, 1000)), "@u", DcAccess.Username());
            DcAudit.Write(x.C, x.Tx, "HEARING", hid, x.Id, "SCHEDULE", null, new { at = at.ToString("yyyy-MM-dd HH:mm"), venue = venue }, reason, null);
            bool wasSummoned = x.Status == "SUMMONED";
            var sets = new Dictionary<string, object>(); sets["hearing_at"] = at; sets["hearing_venue"] = DcAudit.Cut(venue, 200);
            UpdateCase(x, sets, "ADJOURN", reason);
            DcEntries.Add(x.C, x.Tx, x.Id, "ADJOURNED", "Hearing adjourned to " + at.ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture),
                "Venue: " + venue + ".\nReason: " + reason, wasSummoned, x.Status, x.Status, null, null, new { hearingId = hid }, FaJson.Str(d, "opId"), null);
            object res = null;
            if (wasSummoned)
            {
                // The student was told of the old date: a new summons goes out.
                SetStatus(x, "HEARING_SCHEDULED", null, null);
                // The adjournment itself explains a new date that is close; its reason serves as the short-notice reason.
                string given = FaJson.Str(d, "shortNoticeReason");
                d["shortNoticeReason"] = "Adjourned: " + reason.TrimEnd('.') + (given == "" ? "" : ". " + given);
                res = SummonInside(x, d);
            }
            return Commit(x, res);
        }
    }

    public static object RecordHearing(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Decide) || DcAccess.IsAdmin(), "You do not have permission to record hearings.");
            Require(x.Status == "SUMMONED", "Only a hearing the student was summoned to can be recorded.");
            string att = FaJson.Str(d, "attendance").ToUpperInvariant();
            Require(In(att, "PRESENT", "ABSENT", "REPRESENTED"), "Say whether the student attended.");
            string panel = Need(d, "panel", 3, "List the panel members present");
            string minutes = Need(d, "minutes", 20, "Summarise the hearing");
            DataTable h = FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_hearing WHERE case_id=@c AND status='SCHEDULED' ORDER BY id DESC LIMIT 1 FOR UPDATE", "@c", x.Id);
            Require(h.Rows.Count == 1, "The scheduled hearing was not found.");
            DateTime? when = FaDb.D(h.Rows[0]["scheduled_at"]);
            Require(when.HasValue && when.Value <= DateTime.Now.AddHours(12), "The hearing has not taken place yet. Adjourn it if the date changed.");
            string before = x.Status;
            SetStatus(x, "HEARD", null, null);
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, "HEARING_HELD", "Hearing held on " + DcFmt.When(when),
                "Student: " + (att == "PRESENT" ? "present" : att == "ABSENT" ? "absent" : "represented") + ".\nPanel present: " + panel + "\n\n" + minutes,
                false, before, "HEARD", null, null, new { hearingId = FaDb.I(h.Rows[0]["id"]), attendance = att }, FaJson.Str(d, "opId"), when);
            FaDb.Exec(x.C, x.Tx, "UPDATE dc_hearing SET status='HELD', student_attended=@a, panel=@p, outcome_entry_id=@e, updated_by=@u, updated_at=NOW() WHERE id=@h",
                "@a", att == "ABSENT" ? 0 : 1, "@p", DcAudit.Cut(panel, 1000), "@e", eid, "@u", DcAccess.Username(), "@h", FaDb.I(h.Rows[0]["id"]));
            DcAudit.Write(x.C, x.Tx, "HEARING", FaDb.I(h.Rows[0]["id"]), x.Id, "HELD", new { status = "SCHEDULED" }, new { status = "HELD", attendance = att }, null, null);
            return Commit(x, new { entryId = eid });
        }
    }

    // ── Decisions and sanctions ───────────────────────────────────────

    private static List<KeyValuePair<DataRow, DcSanctions.Spec>> ReadSanctions(Ctx x, Dictionary<string, object> d, bool interim, out string outcome)
    {
        var l = new List<KeyValuePair<DataRow, DcSanctions.Spec>>();
        outcome = "";
        object so;
        if (d.TryGetValue("sanctions", out so) && so is System.Collections.IEnumerable && !(so is string))
            foreach (object o in (System.Collections.IEnumerable)so)
            {
                var sd = o as Dictionary<string, object>;
                if (sd == null) continue;
                DcSanctions.Spec s = DcSanctions.Parse(sd);
                DataRow t = DcSanctions.Type(x.C, x.Tx, s.TypeId);
                Require(t != null, "A chosen sanction no longer exists.");
                string err = DcSanctions.Validate(t, s, interim);
                if (err != null) throw new DcRefusal(err);
                string oc = FaDb.S(t["outcome"]);
                if (oc != "SANCTION") outcome = oc;
                l.Add(new KeyValuePair<DataRow, DcSanctions.Spec>(t, s));
            }
        Require(l.Count > 0, "Choose at least one sanction, or dismissal or acquittal.");
        Require(outcome == "" || l.Count == 1, "Dismissal or acquittal cannot be combined with a sanction.");
        Require(l.Count <= 10, "Record at most 10 sanctions in one decision.");
        return l;
    }

    private static string SanctionList(Ctx x, List<long> ids)
    {
        if (ids.Count == 0) return "none";
        var parts = new List<string>();
        foreach (long id in ids)
        {
            DataRow r = FaDb.Table(x.C, x.Tx, "SELECT s.*, t.name, t.needs_dates FROM dc_sanction s JOIN dc_sanction_type t ON t.id=s.sanction_type_id WHERE s.id=@i", "@i", id).Rows[0];
            parts.Add(DcSanctions.Describe(r, FaDb.S(r["name"])));
        }
        return string.Join("; ", parts.ToArray());
    }

    private static void EndInterim(Ctx x, string reason, long entryId)
    {
        foreach (DataRow s in FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_sanction WHERE case_id=@c AND source='INTERIM' AND status='ACTIVE' FOR UPDATE", "@c", x.Id).Rows)
            DcSanctions.End(x.C, x.Tx, s, "LIFTED", reason, entryId);
    }

    public static object RecordDecision(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Decide) || DcAccess.IsAdmin(), "Only the Disciplinary Committee can record a decision.");
            bool minor = FaDb.S(x.Case["severity"]) == "MINOR";
            Require(x.Status == "HEARD" || (minor && In(x.Status, "REPORTED", "UNDER_INVESTIGATION")),
                    minor ? "A decision can be recorded once the case is under investigation or heard." : "A serious or gross case can be decided only after a hearing.");
            string findings = Need(d, "findings", 20, "Record the findings");
            string text = Need(d, "decision", 20, "Write the decision");
            DateTime decided = FaJson.Date(d, "decidedOn") ?? DateTime.Today;
            Require(decided <= DateTime.Today && decided >= DateTime.Today.AddMonths(-6), "The decision date must be within the last six months.");
            string outcome;
            var list = ReadSanctions(x, d, false, out outcome);

            string before = x.Status;
            int window = DcSettings.AppealWindowDays;
            DateTime deadline = DateTime.Today.AddDays(window);
            var sets = new Dictionary<string, object>();
            sets["decided_at"] = decided; sets["decision_summary"] = DcAudit.Cut(text, 1000); sets["notified_at"] = DateTime.Now; sets["appeal_deadline"] = deadline;
            SetStatus(x, "DECIDED", null, sets);

            long decisionEntry = DcEntries.Add(x.C, x.Tx, x.Id, "DECISION",
                outcome == "DISMISSAL" ? "Decision: case dismissed" : outcome == "ACQUITTAL" ? "Decision: acquitted" : "Decision recorded",
                "Findings: " + findings + "\n\nDecision: " + text, true, before, "DECIDED", null, null, new { decidedOn = decided.ToString("yyyy-MM-dd"), outcome = outcome }, FaJson.Str(d, "opId"), decided == DateTime.Today ? DateTime.Now : decided.AddHours(12));
            EndInterim(x, "Replaced by the decision of " + decided.ToString("d MMM yyyy", CultureInfo.InvariantCulture), decisionEntry);

            var ids = new List<long>();
            DataRow suspension = null;
            foreach (var kv in list)
            {
                if (FaDb.S(kv.Key["outcome"]) != "SANCTION") continue;
                long sid = DcSanctions.Create(x.C, x.Tx, x.Case, kv.Key, "DECISION", kv.Value, null);
                ids.Add(sid);
                if (("," + FaDb.S(kv.Key["effects"]) + ",").Contains(",SUSPENSION,"))
                    suspension = FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_sanction WHERE id=@i", "@i", sid).Rows[0];
            }
            string sanctionsText = outcome == "DISMISSAL" ? "None. The case is dismissed." : outcome == "ACQUITTAL" ? "None. You are found not responsible." : SanctionList(x, ids);

            var f = DcLetters.Fields(x.C, x.Tx, x.Case);
            f["decision_text"] = text;
            f["sanctions"] = sanctionsText;
            f["appeal_deadline"] = deadline.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
            long letterId, le;
            DcLetters.Issue(x.C, x.Tx, x.Case, "DECISION", f, out letterId, out le);
            if (suspension != null)
            {
                f["suspension_from"] = DcFmt.LongDate(suspension["starts_on"]);
                f["suspension_to"] = suspension["ends_on"] == DBNull.Value ? "further notice" : DcFmt.LongDate(suspension["ends_on"]);
                long l2, e2;
                DcLetters.Issue(x.C, x.Tx, x.Case, "SUSPENSION", f, out l2, out e2);
            }
            x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "DECISION", "Decision in your disciplinary case",
                "The Students Disciplinary Committee has decided case " + x.No + ". Sanctions: " + sanctionsText +
                (outcome == "" ? " You may appeal by " + deadline.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + " through My Disciplinary Cases." : ""), letterId));
            return Commit(x, new { sanctions = ids.Count, letterId = letterId });
        }
    }

    /// <summary>Interim measure from the new-case form. Applies with discipline.restrict; otherwise recorded as a recommendation.</summary>
    public static void InterimOnCreate(MySqlConnection c, MySqlTransaction tx, DataRow cs, string code, string reason, DateTime? to)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code == "") return;
        DataRow t = DcSanctions.TypeByCode(c, tx, code);
        if (t == null || FaDb.I(t["allowed_interim"]) != 1) throw new DcRefusal("That interim measure is not available.");
        if ((reason ?? "").Trim().Length < 10) throw new DcRefusal("Give the reason for " + DcFmt.Lc(FaDb.S(t["name"])) + " (at least 10 characters).");
        if (DcAccess.Can(DcAccess.Restrict) || DcAccess.IsAdmin())
        {
            var s = new DcSanctions.Spec { TypeId = FaDb.I(t["id"]), From = DateTime.Today, To = to };
            ApplyInterimInside(c, tx, cs, t, s, reason, null);
        }
        else
        {
            DcEntries.Add(c, tx, FaDb.I(cs["id"]), "INTERIM_MEASURE", "Recommended: " + DcFmt.Lc(FaDb.S(t["name"])) + " pending a decision",
                reason + "\nThis is a recommendation. An officer with permission to apply interim measures must confirm it.", false, null, null, null, null,
                new { recommended = true, code = code }, null, null);
        }
    }

    private static long ApplyInterimInside(MySqlConnection c, MySqlTransaction tx, DataRow cs, DataRow type, DcSanctions.Spec s, string reason, List<long> notices)
    {
        int caseId = FaDb.I(cs["id"]);
        bool block = ("," + FaDb.S(type["effects"]) + ",").Contains(",PORTAL_BLOCK,");
        if (FaDb.Scalar(c, tx, "SELECT 1 FROM dc_sanction WHERE case_id=@c AND sanction_type_id=@t AND status='ACTIVE' AND source='INTERIM'", "@c", caseId, "@t", FaDb.I(type["id"])) != null)
            throw new DcRefusal(FaDb.S(type["name"]) + " is already in force on this case.");
        long sid = DcSanctions.Create(c, tx, cs, type, "INTERIM", s, null);
        string until = s.To.HasValue ? " until " + s.To.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : " until the case is decided";
        long eid = DcEntries.Add(c, tx, caseId, block ? "BLOCK_APPLIED" : "INTERIM_MEASURE",
            (block ? "Portal access blocked" : FaDb.S(type["name"])) + " (interim)" + until, "Reason: " + reason, true, null, null, sid, null,
            new { code = FaDb.S(type["code"]) }, null, null);
        FaDb.Exec(c, tx, "UPDATE dc_sanction SET applied_entry_id=@e WHERE id=@s", "@e", eid, "@s", sid);
        long n = DcNotify.Queue(c, tx, caseId, FaDb.S(cs["regno"]), block ? "BLOCK_APPLIED" : "OTHER",
            block ? "Your portal access is restricted" : FaDb.S(type["name"]) + " pending a decision",
            (block ? "Your access to the student portal is restricted" : FaDb.S(type["name"]) + " applies") + until + " under case " + FaDb.S(cs["case_no"]) +
            ". Reason: " + reason + " Contact the " + DcSettings.ContactOffice + ".", null);
        if (notices != null) notices.Add(n); else DcNotify.SendAsync(new List<long> { n });
        return sid;
    }

    public static object ApplyMeasure(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Restrict) || DcAccess.IsAdmin(), "You do not have permission to apply interim measures.");
            Require(In(x.Status, PreDecision) || x.Status == "UNDER_APPEAL", "An interim measure applies only while a case is undecided or under appeal.");
            DataRow t = DcSanctions.TypeByCode(x.C, x.Tx, FaJson.Str(d, "code"));
            Require(t != null && FaDb.I(t["allowed_interim"]) == 1, "Choose an interim measure.");
            string reason = Need(d, "reason", 10, "Give the reason");
            var s = new DcSanctions.Spec { TypeId = FaDb.I(t["id"]), From = FaJson.Date(d, "from") ?? DateTime.Today, To = FaJson.Date(d, "to") };
            Require(s.From.Value >= DateTime.Today.AddDays(-7), "An interim measure cannot start more than a week in the past.");
            Require(!s.To.HasValue || s.To.Value >= s.From.Value, "The end date is before the start.");
            ApplyInterimInside(x.C, x.Tx, x.Case, t, s, reason, x.Notices);
            UpdateCase(x, new Dictionary<string, object>(), "INTERIM", reason);
            return Commit(x, null);
        }
    }

    public static object EndSanction(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Restrict) || DcAccess.IsAdmin(), "You do not have permission to lift sanctions.");
            string reason = Need(d, "reason", 10, "Give the reason for lifting it");
            DataTable t = FaDb.Table(x.C, x.Tx, "SELECT s.*, t.name, t.needs_dates FROM dc_sanction s JOIN dc_sanction_type t ON t.id=s.sanction_type_id WHERE s.id=@s AND s.case_id=@c FOR UPDATE",
                "@s", FaJson.Int(d, "sanctionId"), "@c", x.Id);
            Require(t.Rows.Count == 1, "That sanction was not found on this case.");
            DataRow s = t.Rows[0];
            Require(FaDb.S(s["status"]) == "ACTIVE", "That sanction has already ended.");
            bool block = ("," + FaDb.S(s["effects"]) + ",").Contains(",PORTAL_BLOCK,");
            string desc = DcSanctions.Describe(s, FaDb.S(s["name"]));
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, block ? "BLOCK_LIFTED" : "SANCTION_LIFTED", FaDb.S(s["name"]) + " lifted", "Lifted: " + desc + "\nReason: " + reason,
                true, null, null, FaDb.I(s["id"]), null, null, FaJson.Str(d, "opId"), null);
            DcSanctions.End(x.C, x.Tx, s, "LIFTED", reason, eid);
            UpdateCase(x, new Dictionary<string, object>(), "LIFT", reason);
            long? letterId = null;
            if (FaJson.Bool(d, "letter"))
            {
                var f = DcLetters.Fields(x.C, x.Tx, x.Case);
                f["sanction_lifted"] = desc; f["lift_reason"] = reason;
                long lid, le; DcLetters.Issue(x.C, x.Tx, x.Case, "LIFTING", f, out lid, out le); letterId = lid;
            }
            x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, block ? "BLOCK_LIFTED" : "SANCTION_LIFTED", FaDb.S(s["name"]) + " lifted",
                "The " + DcFmt.Lc(FaDb.S(s["name"])) + " in case " + x.No + " was lifted on " + DateTime.Today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) + ". Reason: " + reason, letterId));
            return Commit(x, null);
        }
    }

    /// <summary>Ends a sanction as VARIED and records its replacement with the new terms (never edits the old row).</summary>
    public static object VarySanction(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Restrict) || DcAccess.IsAdmin(), "You do not have permission to vary sanctions.");
            string reason = Need(d, "reason", 10, "Give the reason for the variation");
            DataTable t = FaDb.Table(x.C, x.Tx, "SELECT s.*, t.name, t.needs_dates FROM dc_sanction s JOIN dc_sanction_type t ON t.id=s.sanction_type_id WHERE s.id=@s AND s.case_id=@c FOR UPDATE",
                "@s", FaJson.Int(d, "sanctionId"), "@c", x.Id);
            Require(t.Rows.Count == 1, "That sanction was not found on this case.");
            DataRow old = t.Rows[0];
            Require(FaDb.S(old["status"]) == "ACTIVE", "Only a sanction in force can be varied.");
            DataRow type = DcSanctions.Type(x.C, x.Tx, FaDb.I(old["sanction_type_id"]));
            var s = new DcSanctions.Spec
            {
                TypeId = FaDb.I(type["id"]),
                Amount = FaJson.Str(d, "amount") != "" ? FaJson.Dec(d, "amount") : FaDb.MN(old["amount"]),
                From = FaJson.Date(d, "from") ?? FaDb.D(old["starts_on"]),
                To = FaJson.Str(d, "to") != "" ? FaJson.Date(d, "to") : (FaJson.Bool(d, "openEnded") ? null : FaDb.D(old["ends_on"])),
                Course = FaDb.S(old["course_code"]), AcadYear = FaDb.S(old["acad_year"]), Semester = FaDb.IN(old["semester"]),
                Terms = FaJson.Str(d, "terms") != "" ? FaJson.Str(d, "terms") : FaDb.S(old["terms"])
            };
            if (FaJson.Bool(d, "openEnded")) s.To = null;
            string err = DcSanctions.Validate(type, s, FaDb.S(old["source"]) == "INTERIM");
            if (err != null) throw new DcRefusal(err);
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, "SANCTION_VARIED", FaDb.S(old["name"]) + " varied",
                "Was: " + DcSanctions.Describe(old, FaDb.S(old["name"])) + "\nReason: " + reason, true, null, null, FaDb.I(old["id"]), null, null, FaJson.Str(d, "opId"), null);
            DcSanctions.End(x.C, x.Tx, old, "VARIED", reason, eid);
            long nid = DcSanctions.Create(x.C, x.Tx, x.Case, type, FaDb.S(old["source"]), s, FaDb.I(old["id"]));
            FaDb.Exec(x.C, x.Tx, "UPDATE dc_sanction SET applied_entry_id=@e WHERE id=@s", "@e", eid, "@s", nid);
            if (FaDb.S(old["follow_up"]) == "DONE")
                FaDb.Exec(x.C, x.Tx, "UPDATE dc_sanction SET follow_up='DONE', follow_up_ref=@r WHERE id=@s", "@r", FaDb.S(old["follow_up_ref"]), "@s", nid);
            DataRow nr = FaDb.Table(x.C, x.Tx, "SELECT s.*, t.name, t.needs_dates FROM dc_sanction s JOIN dc_sanction_type t ON t.id=s.sanction_type_id WHERE s.id=@s", "@s", nid).Rows[0];
            string now = DcSanctions.Describe(nr, FaDb.S(nr["name"]));
            UpdateCase(x, new Dictionary<string, object>(), "VARY", reason);
            x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "OTHER", FaDb.S(old["name"]) + " varied",
                "The " + DcFmt.Lc(FaDb.S(old["name"])) + " in case " + x.No + " has been varied. It is now: " + now + ". Reason: " + reason, null));
            return Commit(x, null);
        }
    }

    /// <summary>The marks office or the Bursar records the action taken for a cancellation, fine or restitution.</summary>
    public static object FollowUpDone(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.CanManageCase(x.Case), "You do not have permission to manage this case.");
            string reference = Need(d, "reference", 3, "Give the reference (receipt, invoice or marks change)");
            string note = FaJson.Str(d, "note");
            DataTable t = FaDb.Table(x.C, x.Tx, "SELECT s.*, t.name, t.needs_dates FROM dc_sanction s JOIN dc_sanction_type t ON t.id=s.sanction_type_id WHERE s.id=@s AND s.case_id=@c FOR UPDATE",
                "@s", FaJson.Int(d, "sanctionId"), "@c", x.Id);
            Require(t.Rows.Count == 1, "That sanction was not found on this case.");
            DataRow s = t.Rows[0];
            Require(FaDb.S(s["follow_up"]) == "PENDING", "No action is owed for that sanction.");
            string eff = "," + FaDb.S(s["effects"]) + ",";
            bool marks = eff.Contains(",CANCEL_PAPER,") || eff.Contains(",CANCEL_SEMESTER,");
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, marks ? "MARKS_ACTION" : "FEES_ACTION",
                (marks ? "Results cancelled as decided: " : "Billed as decided: ") + DcFmt.Lc(FaDb.S(s["name"])),
                DcSanctions.Describe(s, FaDb.S(s["name"])) + "\nReference: " + reference + (note == "" ? "" : "\n" + note), true, null, null, FaDb.I(s["id"]), null, null, FaJson.Str(d, "opId"), null);
            FaDb.Exec(x.C, x.Tx, "UPDATE dc_sanction SET follow_up='DONE', follow_up_ref=@r WHERE id=@s", "@r", DcAudit.Cut(reference, 150), "@s", FaDb.I(s["id"]));
            DcAudit.Write(x.C, x.Tx, "SANCTION", FaDb.I(s["id"]), x.Id, "FOLLOW_UP", new { follow_up = "PENDING" }, new { follow_up = "DONE", reference = reference }, note,
                "Disciplinary follow-up recorded in " + x.No + ": " + reference);
            return Commit(x, new { entryId = eid });
        }
    }

    // ── Appeals ───────────────────────────────────────────────────────

    public static object LodgeAppeal(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.CanManageCase(x.Case) || DcAccess.Can(DcAccess.Appeal) || DcAccess.IsAdmin(), "You do not have permission to lodge an appeal on the student's behalf.");
            Require(x.Status == "DECIDED", "An appeal can be lodged only against a decided case.");
            string grounds = Need(d, "grounds", 20, "Give the grounds of appeal");
            DateTime? deadline = FaDb.D(x.Case["appeal_deadline"]);
            bool within = deadline.HasValue && DateTime.Today <= deadline.Value;
            string late = FaJson.Str(d, "lateReason");
            if (!within)
            {
                Require(DcAccess.Can(DcAccess.Appeal) || DcAccess.IsAdmin(), "The appeal window closed on " + DcFmt.Date(deadline) + ". Only the appellate authority can accept a late appeal.");
                Require(late.Length >= 10, "Give the reason for accepting a late appeal (at least 10 characters).");
            }
            string before = x.Status;
            SetStatus(x, "UNDER_APPEAL", late, null);
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, "APPEAL_LODGED", "Appeal lodged" + (within ? "" : " after the window (accepted)"),
                "Grounds: " + grounds + (within ? "" : "\nLate appeal accepted: " + late) + "\nLodged at the office on the student's behalf.", true, before, "UNDER_APPEAL", null, null, null, FaJson.Str(d, "opId"), null);
            long aid = FaDb.Insert(x.C, x.Tx,
                "INSERT INTO dc_appeal (case_id, regno, lodged_at, lodged_via, lodged_by, grounds, within_window, late_reason, status, lodged_entry_id) VALUES (@c,@r,NOW(),'EADMIN',@u,@g,@w,@l,'LODGED',@e)",
                "@c", x.Id, "@r", x.Regno, "@u", DcAccess.Username(), "@g", grounds, "@w", within ? 1 : 0, "@l", FaDb.NullIfEmpty(late), "@e", eid);
            DcAudit.Write(x.C, x.Tx, "APPEAL", aid, x.Id, "LODGE", null, new { within = within }, late, "Appeal lodged in " + x.No);
            x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "APPEAL_RECEIVED", "Your appeal has been received",
                "Your appeal in case " + x.No + " has been received and will be considered by the " + DcSettings.AppellateAuthority + ". The sanctions stay in force until it is decided.", null));
            return Commit(x, new { appealId = aid });
        }
    }

    public static object DecideAppeal(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Appeal) || DcAccess.IsAdmin(), "Only the appellate authority can decide an appeal.");
            Require(x.Status == "UNDER_APPEAL", "There is no appeal to decide.");
            string outcome = FaJson.Str(d, "outcome").ToUpperInvariant();
            Require(In(outcome, "UPHELD", "DISMISSED", "VARIED", "WITHDRAWN"), "Choose the outcome of the appeal.");
            string text = Need(d, "decision", 20, "Write the appeal decision");
            DataTable at = FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_appeal WHERE case_id=@c AND status='LODGED' ORDER BY id DESC LIMIT 1 FOR UPDATE", "@c", x.Id);
            Require(at.Rows.Count == 1, "The lodged appeal was not found.");
            string ignore;
            List<KeyValuePair<DataRow, DcSanctions.Spec>> list = outcome == "VARIED" ? ReadSanctions(x, d, false, out ignore) : null;

            string before = x.Status;
            SetStatus(x, "APPEAL_DECIDED", null, null);
            string title = outcome == "UPHELD" ? "Appeal upheld: the decision is set aside" : outcome == "DISMISSED" ? "Appeal dismissed: the decision stands"
                         : outcome == "VARIED" ? "Appeal allowed in part: sanctions varied" : "Appeal withdrawn";
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, "APPEAL_DECISION", title, text, true, before, "APPEAL_DECIDED", null, null, new { outcome = outcome }, FaJson.Str(d, "opId"), null);
            if (outcome == "UPHELD" || outcome == "VARIED")
                foreach (DataRow s in FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_sanction WHERE case_id=@c AND source IN ('DECISION','INTERIM') AND status='ACTIVE' FOR UPDATE", "@c", x.Id).Rows)
                    DcSanctions.End(x.C, x.Tx, s, outcome == "UPHELD" ? "SET_ASIDE" : "VARIED", "Appeal " + outcome.ToLowerInvariant() + ": " + DcAudit.Cut(text, 200), eid);
            var ids = new List<long>();
            if (list != null)
                foreach (var kv in list)
                    if (FaDb.S(kv.Key["outcome"]) == "SANCTION") ids.Add(DcSanctions.Create(x.C, x.Tx, x.Case, kv.Key, "APPEAL", kv.Value, null));
            FaDb.Exec(x.C, x.Tx, "UPDATE dc_appeal SET status=@s, decided_at=CURDATE(), decided_by=@u, decision_text=@t, decision_entry_id=@e WHERE id=@a",
                "@s", outcome, "@u", DcAccess.Username(), "@t", text, "@e", eid, "@a", FaDb.I(at.Rows[0]["id"]));
            DcAudit.Write(x.C, x.Tx, "APPEAL", FaDb.I(at.Rows[0]["id"]), x.Id, "DECIDE", new { status = "LODGED" }, new { status = outcome }, text, "Appeal decided in " + x.No + ": " + outcome.ToLowerInvariant());

            string inForce;
            if (outcome == "UPHELD") inForce = "None. The decision has been set aside.";
            else
            {
                var live = new List<long>();
                foreach (DataRow r in FaDb.Table(x.C, x.Tx, "SELECT id FROM dc_sanction WHERE case_id=@c AND status='ACTIVE' ORDER BY id", "@c", x.Id).Rows) live.Add(FaDb.L(r[0]));
                inForce = SanctionList(x, live);
            }
            var f = DcLetters.Fields(x.C, x.Tx, x.Case);
            f["decision_text"] = text; f["sanctions"] = inForce;
            long letterId, le;
            DcLetters.Issue(x.C, x.Tx, x.Case, "APPEAL_DECISION", f, out letterId, out le);
            x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "APPEAL_OUTCOME", "Decision on your appeal",
                title + " (case " + x.No + "). Sanctions now in force: " + inForce, letterId));
            return Commit(x, new { letterId = letterId });
        }
    }

    // ── Letters, notices, closing ─────────────────────────────────────

    public static object IssueLetter(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Hearing) || DcAccess.Can(DcAccess.Decide) || DcAccess.IsAdmin(), "You do not have permission to issue letters.");
            string code = FaJson.Str(d, "template").ToUpperInvariant();
            if (code == "CLEARANCE")
            {
                // A clearance letter states that nothing is pending: refuse it when that is not true.
                DcClearanceResult cl = DcClearance.Check(x.C, x.Tx, x.Regno);
                int others = FaDb.I(FaDb.Scalar(x.C, x.Tx, "SELECT COUNT(*) FROM dc_case WHERE regno=@r AND status NOT IN ('CLOSED','WITHDRAWN')", "@r", x.Regno));
                bool pendingFollow = FaDb.Scalar(x.C, x.Tx, "SELECT 1 FROM dc_sanction WHERE regno=@r AND follow_up='PENDING' AND status IN ('ACTIVE','EXPIRED') LIMIT 1", "@r", x.Regno) != null;
                Require(others == 0 && cl.Effects.Count == 0 && !pendingFollow,
                        "A clearance letter cannot be issued: the student has an open case, a sanction in force or an action still owed.");
            }
            var f = DcLetters.Fields(x.C, x.Tx, x.Case);
            var extra = FaJson.Obj(d, "fields");
            foreach (var kv in extra) f[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
            long letterId, eid;
            DcLetters.Issue(x.C, x.Tx, x.Case, code, f, out letterId, out eid);
            bool studentCopy = FaDb.I(FaDb.Scalar(x.C, x.Tx, "SELECT student_visible FROM dc_letter WHERE id=@l", "@l", letterId)) == 1;
            if (studentCopy)
                x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "LETTER", "A letter has been issued to you",
                    "A letter in case " + x.No + " has been issued: " + FaDb.S(FaDb.Scalar(x.C, x.Tx, "SELECT subject FROM dc_letter WHERE id=@l", "@l", letterId)).ToLowerInvariant() + ". It is on My Disciplinary Cases.", letterId));
            UpdateCase(x, new Dictionary<string, object>(), "LETTER", code);
            return Commit(x, new { letterId = letterId });
        }
    }

    public static object NotifyAgain(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.CanManageCase(x.Case), "You do not have permission to manage this case.");
            DataTable n = FaDb.Table(x.C, x.Tx, "SELECT * FROM dc_notification WHERE id=@n AND case_id=@c", "@n", FaJson.Int(d, "noticeId"), "@c", x.Id);
            Require(n.Rows.Count == 1, "That notice was not found.");
            DataRow r = n.Rows[0];
            x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, FaDb.S(r["kind"]), FaDb.S(r["title"]), FaDb.S(r["message"]), (r["letter_id"] == DBNull.Value ? (long?)null : FaDb.L(r["letter_id"]))));
            UpdateCase(x, new Dictionary<string, object>(), "NOTIFY", "Notice sent again: " + FaDb.S(r["title"]));
            return Commit(x, null);
        }
    }

    public static object Close(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.CanManageCase(x.Case), "You do not have permission to manage this case.");
            Require(In(x.Status, "DECIDED", "APPEAL_DECIDED"), "Only a decided case can be closed.");
            string note = FaJson.Str(d, "note");
            if (x.Status == "DECIDED")
            {
                DateTime? deadline = FaDb.D(x.Case["appeal_deadline"]);
                bool cleared = FaDb.Scalar(x.C, x.Tx, "SELECT 1 FROM dc_sanction WHERE case_id=@c AND source='DECISION' LIMIT 1", "@c", x.Id) == null;
                if (deadline.HasValue && deadline.Value >= DateTime.Today && !cleared)
                    Require(note.Length >= 10, "The appeal window is open until " + DcFmt.Date(deadline) + ". To close now, record why (for example, the student waived the appeal in writing).");
            }
            string before = x.Status;
            var sets = new Dictionary<string, object>(); sets["closed_at"] = DateTime.Now;
            SetStatus(x, "CLOSED", note, sets);
            DcEntries.Add(x.C, x.Tx, x.Id, "CASE_CLOSED", "Case closed", (note == "" ? "Closed." : note) +
                "\nAny sanction with an end date stays in force until that date.", true, before, "CLOSED", null, null, null, FaJson.Str(d, "opId"), null);
            return Commit(x, null);
        }
    }

    public static object Withdraw(Dictionary<string, object> d)
    {
        using (Ctx x = Begin(d))
        {
            Require(DcAccess.Can(DcAccess.Restrict) || DcAccess.IsAdmin(), "You do not have permission to withdraw cases.");
            Require(In(x.Status, PreDecision), "Only an undecided case can be withdrawn.");
            string reason = Need(d, "reason", 10, "Give the reason for withdrawing the case");
            string before = x.Status;
            var sets = new Dictionary<string, object>(); sets["closed_at"] = DateTime.Now;
            SetStatus(x, "WITHDRAWN", reason, sets);
            long eid = DcEntries.Add(x.C, x.Tx, x.Id, "CASE_WITHDRAWN", "Case withdrawn", "Reason: " + reason, true, before, "WITHDRAWN", null, null, null, FaJson.Str(d, "opId"), null);
            EndInterim(x, "Case withdrawn: " + reason, eid);
            foreach (DataRow h in FaDb.Table(x.C, x.Tx, "SELECT id FROM dc_hearing WHERE case_id=@c AND status='SCHEDULED'", "@c", x.Id).Rows)
            {
                FaDb.Exec(x.C, x.Tx, "UPDATE dc_hearing SET status='CANCELLED', updated_by=@u, updated_at=NOW() WHERE id=@h", "@u", DcAccess.Username(), "@h", FaDb.I(h[0]));
                DcAudit.Write(x.C, x.Tx, "HEARING", FaDb.I(h[0]), x.Id, "CANCEL", new { status = "SCHEDULED" }, new { status = "CANCELLED" }, reason, null);
            }
            x.Notices.Add(DcNotify.Queue(x.C, x.Tx, x.Id, x.Regno, "OTHER", "Your disciplinary case has been withdrawn",
                "Case " + x.No + " has been withdrawn. Any interim measure in it has been lifted.", null));
            return Commit(x, null);
        }
    }
}
