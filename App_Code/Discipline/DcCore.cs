using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: shared plumbing (eadmin).
//  Plan: COOPERP/docs/disciplinary-plan.md
//
//  Database helpers and the JSON envelope are the Fixed Assets ones
//  (FaDb, FaJson), which are generic. This file adds:
//  DcAccess    slugs, who is signed in, case visibility and case rights
//  DcApi       the wrapper every PageMethod body runs through
//  DcAudit     dc_audit + acad_activity_log inside the caller's transaction
//  DcSettings  dc_settings key/value table
//  DcSeq       case, incident and letter numbers
//  DcPaths     private file storage outside the web roots
//  DcFmt       labels as they appear on screen and paper
//  DcLog       unexpected errors to a server log
// =====================================================================

public static class DcAccess
{
    public const string Parent = "discipline";
    public const string Dashboard = "discipline.dashboard";
    public const string Records = "discipline.records";
    public const string Updates = "discipline.updates";
    public const string Settings = "discipline.settings";
    public const string Reports = "discipline.reports";
    public const string Report = "discipline.report";
    public const string Manage = "discipline.manage";
    public const string ManageExam = "discipline.manage_exam";
    public const string ManageAll = "discipline.manage_all";
    public const string ViewAll = "discipline.view_all";
    public const string Hearing = "discipline.hearing";
    public const string Decide = "discipline.decide";
    public const string Appeal = "discipline.appeal";
    public const string Restrict = "discipline.restrict";
    public const string Restricted = "discipline.restricted";
    public const string SettingsManage = "discipline.settings_manage";

    public static string Username() { return FaAccess.Username(); }
    public static string Role() { return FaAccess.Role(); }
    public static string Ip() { return FaAccess.Ip(); }
    public static bool Can(string slug) { return FaAccess.Can(slug); }

    public static bool IsAdmin()
    {
        try { if (Username() == "") return false; Can(Parent); return RoleAccessService.IsAdmin(); }
        catch { return false; }
    }

    /// <summary>Any of the slugs.</summary>
    public static bool CanAny(params string[] slugs)
    {
        foreach (string s in slugs) if (Can(s)) return true;
        return false;
    }

    private const string CtxKey = "dc_scope_cache";

    /// <summary>
    /// What the signed-in user may see, worked out once per request.
    /// Plan 8.2: restricted cases only with discipline.restricted, admin, or as this year's committee chair;
    /// otherwise all (manage_all, view_all), exam types (manage_exam), own faculty or department (manage),
    /// and always the cases the user reported or is the officer for.
    /// </summary>
    public class Scope
    {
        public string User = "";
        public bool Admin, All, Exam, Area, SeeRestricted;
        public List<string> ProgCodes = new List<string>();
        public string AreaLabel = "";
    }

    public static Scope Current()
    {
        HttpContext x = HttpContext.Current;
        if (x != null && x.Items[CtxKey] is Scope) return (Scope)x.Items[CtxKey];
        var s = new Scope();
        s.User = Username();
        if (s.User != "")
        {
            s.Admin = IsAdmin();
            s.All = s.Admin || Can(ManageAll) || Can(ViewAll);
            s.Exam = Can(ManageExam);
            s.SeeRestricted = s.Admin || Can(Restricted) || IsCommitteeChair(s.User);
            if (!s.All && Can(Manage))
            {
                try
                {
                    MarksScope m = MarksScopeResolver.Resolve();
                    if (m.IsAdmin || m.AllowedProgCodes == null) s.All = true;
                    else if (m.AllowedProgCodes.Count > 0) { s.Area = true; s.ProgCodes = m.AllowedProgCodes; s.AreaLabel = m.Label; }
                }
                catch { }
            }
        }
        if (x != null) x.Items[CtxKey] = s;
        return s;
    }

    private static bool IsCommitteeChair(string user)
    {
        try
        {
            using (var c = FaDb.Open())
                return FaDb.Scalar(c, null,
                    "SELECT 1 FROM dc_committee_member WHERE is_active=1 AND panel_role='CHAIR' AND acad_year=@y AND username=@u LIMIT 1",
                    "@y", DcSeq.AcadYear(), "@u", user) != null;
        }
        catch { return false; }
    }

    private static string InList(List<string> l)
    {
        var sb = new StringBuilder();
        foreach (string p in l) { if (sb.Length > 0) sb.Append(','); sb.Append('\'').Append((p ?? "").Trim().Replace("\\", "").Replace("'", "''")).Append('\''); }
        return sb.ToString();
    }

    /// <summary>
    /// SQL fragment (starting with " AND ") that limits dc_case alias <paramref name="a"/> to what the user may see.
    /// Every list, count, search, chart, feed and export uses this, so a hidden case is never counted.
    /// The user name is passed as @dcMe, which the caller must add (see <see cref="MeParam"/>).
    /// </summary>
    public static string CaseFilter(string a)
    {
        Scope s = Current();
        if (s.User == "") return " AND 1=0 ";
        var sb = new StringBuilder();
        if (!s.SeeRestricted) sb.Append(" AND ").Append(a).Append(".is_restricted=0");
        if (!s.All)
        {
            var or = new List<string>();
            or.Add(a + ".reported_by=@dcMe");
            or.Add(a + ".case_officer=@dcMe");
            if (s.Exam) or.Add(a + ".case_type_id IN (SELECT id FROM dc_case_type WHERE is_exam_related=1)");
            if (s.Area && s.ProgCodes.Count > 0) or.Add(a + ".progcode IN (" + InList(s.ProgCodes) + ")");
            sb.Append(" AND (").Append(string.Join(" OR ", or.ToArray())).Append(")");
        }
        return sb.ToString();
    }

    public const string MeParam = "@dcMe";

    /// <summary>The case row if the user may see it, else null ("not found" to the caller, so a hidden case is not revealed).</summary>
    public static DataRow VisibleCase(MySqlConnection c, MySqlTransaction tx, int caseId, bool forUpdate)
    {
        DataTable t = FaDb.Table(c, tx,
            "SELECT c.*, t.name AS type_name, t.code AS type_code, t.is_exam_related FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id " +
            "WHERE c.id=@id" + CaseFilter("c") + (forUpdate ? " FOR UPDATE" : ""), "@id", caseId, MeParam, Username());
        return t.Rows.Count == 0 ? null : t.Rows[0];
    }

    /// <summary>May the user manage (change status, add findings to) this visible case?</summary>
    public static bool CanManageCase(DataRow cs)
    {
        Scope s = Current();
        if (s.Admin || Can(ManageAll)) return true;
        if (s.Exam && FaDb.I(cs["is_exam_related"]) == 1) return true;
        if (Can(Manage) && (s.All || (s.Area && s.ProgCodes.Contains(FaDb.S(cs["progcode"]).Trim())))) return true;
        string me = Username();
        return me != "" && string.Equals(FaDb.S(cs["case_officer"]), me, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The reporter of a case may add notes, statements and evidence to it.</summary>
    public static bool IsReporter(DataRow cs)
    {
        string me = Username();
        return me != "" && string.Equals(FaDb.S(cs["reported_by"]), me, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Records who opened a restricted case (plan D15). Never stops the request.</summary>
    public static void LogRestrictedAccess(MySqlConnection c, DataRow cs, string what)
    {
        if (cs == null || FaDb.I(cs["is_restricted"]) != 1) return;
        try
        {
            FaDb.Exec(c, null, "INSERT INTO dc_access_log (case_id, viewer, viewer_role, what, ip_address, viewed_at) VALUES (@c,@u,@r,@w,@ip,NOW())",
                "@c", FaDb.I(cs["id"]), "@u", Username(), "@r", Role(), "@w", what, "@ip", Ip());
        }
        catch (Exception ex) { DcLog.Error("access log", ex); }
    }

    public static object RightsJson()
    {
        Scope s = Current();
        return new
        {
            admin = s.Admin, all = s.All, seeRestricted = s.SeeRestricted,
            report = Can(Report), manage = Can(Manage) || Can(ManageAll) || Can(ManageExam) || s.Admin,
            manageAll = Can(ManageAll) || s.Admin, hearing = Can(Hearing), decide = Can(Decide), appeal = Can(Appeal),
            restrict = Can(Restrict), restricted = s.SeeRestricted, settings = Can(Settings), settingsManage = Can(SettingsManage),
            reports = Can(Reports), records = Can(Records), dashboard = Can(Dashboard), updates = Can(Updates)
        };
    }
}

/// <summary>
/// Every PageMethod body runs through here: permission (and the anti-forgery token for writes) first, then the work.
/// An unexpected error is logged and the user sees a plain sentence.
/// </summary>
public static class DcApi
{
    public static string Read(string slug, Func<string> work) { return Run(slug, false, work); }
    public static string Write(string slug, Func<string> work) { return Run(slug, true, work); }

    private static string Run(string slug, bool write, Func<string> work)
    {
        string d = write ? FaAccess.DenyWrite(slug) : FaAccess.Deny(slug);
        if (d != null) return d;
        try { return work(); }
        catch (DcRefusal r) { return FaJson.Fail(r.Message); }
        catch (MySqlException mx)
        {
            // A guard trigger refusing a change (SQLSTATE 45000) carries a plain sentence meant for people.
            if (mx.Number == 1644) return FaJson.Fail(mx.Message);
            DcLog.Error("DcApi " + slug, mx);
            return FaJson.Fail("Something went wrong and nothing was changed. Try again, and tell MIS if it keeps happening.");
        }
        catch (Exception ex)
        {
            DcLog.Error("DcApi " + slug, ex);
            return FaJson.Fail("Something went wrong and nothing was changed. Try again, and tell MIS if it keeps happening.");
        }
    }

    public static string Ok(object extra)
    {
        var d = new Dictionary<string, object>();
        d["success"] = true;
        if (extra != null)
            foreach (var p in extra.GetType().GetProperties()) d[p.Name] = p.GetValue(extra, null);
        return FaJson.Ser(d);
    }
}

/// <summary>A rule refused the action; the message is shown to the user as is.</summary>
public class DcRefusal : Exception
{
    public DcRefusal(string message) : base(message) { }
}

public static class DcAudit
{
    /// <summary>One dc_audit row (and one activity-log line) inside the caller's transaction. No try/catch: no audit, no change.</summary>
    public static void Write(MySqlConnection c, MySqlTransaction tx, string entity, long entityId, int? caseId,
                             string action, object before, object after, string reason, string summary)
    {
        string actor = DcAccess.Username(); if (actor == "") actor = "system";
        FaDb.Exec(c, tx,
            "INSERT INTO dc_audit (entity, entity_id, case_id, action, before_json, after_json, reason, actor, actor_role, interface, ip_address, created_at) " +
            "VALUES (@e,@id,@c,@a,@b,@af,@r,@u,@role,'EADMIN',@ip,NOW())",
            "@e", entity, "@id", entityId, "@c", caseId.HasValue ? (object)caseId.Value : DBNull.Value,
            "@a", Cut(action, 40), "@b", before == null ? (object)DBNull.Value : Cut(FaJson.Ser(before), 60000),
            "@af", after == null ? (object)DBNull.Value : Cut(FaJson.Ser(after), 60000),
            "@r", string.IsNullOrEmpty(reason) ? (object)DBNull.Value : Cut(reason, 1000),
            "@u", actor, "@role", DcAccess.Role(), "@ip", DcAccess.Ip());
        if (string.IsNullOrEmpty(summary)) return;
        FaDb.Exec(c, tx,
            "INSERT INTO acad_activity_log (user_id, page_function, par, comments, access_date) VALUES (@u,'Student Discipline',@p,@cm,NOW())",
            "@u", Cut(actor, 100), "@p", Cut(summary, 300), "@cm", Cut(action + (string.IsNullOrEmpty(reason) ? "" : ": " + reason), 200));
    }

    public static string Cut(string s, int n) { s = s ?? ""; return s.Length > n ? s.Substring(0, n) : s; }
}

public static class DcSettings
{
    private const string Key = "dc_settings_cache";

    public static Dictionary<string, string> All()
    {
        HttpContext x = HttpContext.Current;
        if (x != null && x.Items[Key] is Dictionary<string, string>) return (Dictionary<string, string>)x.Items[Key];
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var c = FaDb.Open())
                foreach (DataRow r in FaDb.Table(c, null, "SELECT setting_key, setting_value FROM dc_settings").Rows)
                    d[FaDb.S(r[0])] = FaDb.S(r[1]);
        }
        catch { }
        if (x != null) x.Items[Key] = d;
        return d;
    }

    public static void Forget() { HttpContext x = HttpContext.Current; if (x != null) x.Items.Remove(Key); }
    public static string Get(string key, string dflt) { string v; return All().TryGetValue(key, out v) && v.Trim() != "" ? v.Trim() : dflt; }
    public static int Int(string key, int dflt) { int v; return int.TryParse(Get(key, ""), out v) ? v : dflt; }

    public static int AppealWindowDays { get { return Math.Max(1, Int("appeal_window_days", 14)); } }
    public static string ContactOffice { get { return Get("contact_office", "Office of the Dean of Students"); } }
    public static string ContactDetails { get { return Get("contact_details", ""); } }
    public static string AppellateAuthority { get { return Get("appellate_authority", "University Appeals Committee"); } }
    public static string LetterOffice { get { return Get("letter_office", "Office of the Academic Registrar"); } }
}

public static class DcSeq
{
    /// <summary>The current academic year, "2026/2027".</summary>
    public static string AcadYear()
    {
        try { string y = AcademicYearHelper.GetCurrentAcademicYear(); if (!string.IsNullOrEmpty(y)) return y.Trim(); }
        catch { }
        int s = DateTime.Today.Month >= 8 ? DateTime.Today.Year : DateTime.Today.Year - 1;
        return s + "/" + (s + 1);
    }

    /// <summary>"2026/2027" to "2026-27".</summary>
    public static string Short(string acadYear)
    {
        string y = (acadYear ?? "").Trim();
        if (y.Length == 9 && y[4] == '/') return y.Substring(0, 4) + "-" + y.Substring(7, 2);
        return y.Replace("/", "-");
    }

    /// <summary>Next number in a sequence, claimed under a row lock inside the caller's transaction.</summary>
    public static int Next(MySqlConnection c, MySqlTransaction tx, string key)
    {
        FaDb.Exec(c, tx, "INSERT IGNORE INTO dc_sequence (seq_key, next_no) VALUES (@k, 1)", "@k", key);
        int n = FaDb.I(FaDb.Scalar(c, tx, "SELECT next_no FROM dc_sequence WHERE seq_key=@k FOR UPDATE", "@k", key));
        if (n < 1) n = 1;
        FaDb.Exec(c, tx, "UPDATE dc_sequence SET next_no=@n WHERE seq_key=@k", "@n", n + 1, "@k", key);
        return n;
    }

    public static string CaseNo(MySqlConnection c, MySqlTransaction tx, string acadYear)
    {
        string y = Short(acadYear);
        for (int i = 0; i < 50; i++)
        {
            string no = "DC/" + y + "/" + Next(c, tx, "CASE:" + y).ToString("0000", CultureInfo.InvariantCulture);
            if (FaDb.Scalar(c, tx, "SELECT 1 FROM dc_case WHERE case_no=@n", "@n", no) == null) return no;
        }
        throw new DcRefusal("A case number could not be issued. Try again.");
    }

    public static string IncidentNo(MySqlConnection c, MySqlTransaction tx, string acadYear)
    {
        string y = Short(acadYear);
        for (int i = 0; i < 50; i++)
        {
            string no = "DI/" + y + "/" + Next(c, tx, "INCIDENT:" + y).ToString("0000", CultureInfo.InvariantCulture);
            if (FaDb.Scalar(c, tx, "SELECT 1 FROM dc_incident WHERE incident_no=@n", "@n", no) == null) return no;
        }
        throw new DcRefusal("An incident number could not be issued. Try again.");
    }
}

public static class DcPaths
{
    /// <summary>E:\...\Campus Dynamics MRU\Data_Private\Disciplinary\{caseId}, beside the appraisal evidence, outside both web roots.</summary>
    public static string CaseFolder(int caseId)
    {
        string root = Root();
        string dir = Path.Combine(root, caseId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string Root()
    {
        string app = HttpContext.Current != null ? HttpContext.Current.Server.MapPath("~/") : AppDomain.CurrentDomain.BaseDirectory;
        string parent = Directory.GetParent(app.TrimEnd('\\', '/')).FullName;
        string dir = Path.Combine(Path.Combine(parent, "Data_Private"), "Disciplinary");
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public static class DcFmt
{
    public const string University = "Muteesa I Royal University";

    public static string Date(object o) { DateTime? d = FaDb.D(o); return d.HasValue ? d.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : ""; }
    public static string When(object o) { DateTime? d = FaDb.D(o); return d.HasValue ? d.Value.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : ""; }
    public static string LongDate(object o) { DateTime? d = FaDb.D(o); return d.HasValue ? d.Value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) : ""; }
    public static string Time(object o) { DateTime? d = FaDb.D(o); return d.HasValue ? d.Value.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant() : ""; }
    public static string Iso(object o) { DateTime? d = FaDb.D(o); return d.HasValue ? d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : ""; }
    public static string IsoDT(object o) { DateTime? d = FaDb.D(o); return d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) : ""; }
    public static string Money(object o) { return FaFmt.Money(o); }

    public static readonly string[] Statuses = { "REPORTED", "UNDER_INVESTIGATION", "HEARING_SCHEDULED", "SUMMONED", "HEARD", "DECIDED", "UNDER_APPEAL", "APPEAL_DECIDED", "CLOSED", "WITHDRAWN" };

    public static string Status(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "REPORTED": return "Reported";
            case "UNDER_INVESTIGATION": return "Under investigation";
            case "HEARING_SCHEDULED": return "Hearing scheduled";
            case "SUMMONED": return "Summoned";
            case "HEARD": return "Heard";
            case "DECIDED": return "Decided";
            case "UNDER_APPEAL": return "Under appeal";
            case "APPEAL_DECIDED": return "Appeal decided";
            case "CLOSED": return "Closed";
            case "WITHDRAWN": return "Withdrawn";
            default: return s ?? "";
        }
    }

    public static string Severity(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "MINOR": return "Minor";
            case "SERIOUS": return "Serious";
            case "GROSS": return "Gross";
            default: return s ?? "";
        }
    }

    public static string Effect(string e)
    {
        switch ((e ?? "").ToUpperInvariant())
        {
            case "PORTAL_BLOCK": return "Portal access blocked";
            case "RESULTS_WITHHELD": return "Results withheld";
            case "SUSPENSION": return "Suspended";
            case "EXPULSION": return "Expelled";
            case "GRADUATION_BAR": return "Barred from graduation";
            case "CANCEL_PAPER": return "Paper result cancelled";
            case "CANCEL_SEMESTER": return "Semester results cancelled";
            case "FINE": return "Fine";
            case "RESTITUTION": return "Restitution";
            default: return e ?? "";
        }
    }

    /// <summary>Only the effects that restrict the student (for banners): fines and cancellations are actions, not restrictions.</summary>
    public static string Restrictions(string set)
    {
        var l = new List<string>();
        foreach (string e in (set ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string x = e.Trim();
            if (x == "PORTAL_BLOCK" || x == "RESULTS_WITHHELD" || x == "SUSPENSION" || x == "EXPULSION" || x == "GRADUATION_BAR") l.Add(Effect(x));
        }
        return string.Join(", ", l.ToArray());
    }

    public static string Effects(string set)
    {
        var l = new List<string>();
        foreach (string e in (set ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) l.Add(Effect(e.Trim()));
        return string.Join(", ", l.ToArray());
    }

    public static string SanctionStatus(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "ACTIVE": return "In force";
            case "LIFTED": return "Lifted";
            case "EXPIRED": return "Ended";
            case "VARIED": return "Varied";
            case "SET_ASIDE": return "Set aside";
            default: return s ?? "";
        }
    }

    public static string Source(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "INTERIM": return "Interim measure";
            case "DECISION": return "Decision";
            case "APPEAL": return "Appeal decision";
            default: return s ?? "";
        }
    }

    public static string EntryType(string t)
    {
        switch ((t ?? "").ToUpperInvariant())
        {
            case "CASE_OPENED": return "Case opened";
            case "NOTE": return "Note";
            case "FINDING": return "Finding";
            case "STATEMENT": return "Statement";
            case "EVIDENCE": return "Evidence";
            case "INTERIM_MEASURE": return "Interim measure";
            case "HEARING_SCHEDULED": return "Hearing scheduled";
            case "SUMMON": return "Summons";
            case "ADJOURNED": return "Hearing adjourned";
            case "HEARING_HELD": return "Hearing held";
            case "DECISION": return "Decision";
            case "APPEAL_LODGED": return "Appeal lodged";
            case "APPEAL_DECISION": return "Appeal decision";
            case "SANCTION_VARIED": return "Sanction varied";
            case "SANCTION_LIFTED": return "Sanction lifted";
            case "BLOCK_APPLIED": return "Portal block applied";
            case "BLOCK_LIFTED": return "Portal block lifted";
            case "STATUS_CHANGE": return "Status change";
            case "STUDENT_NOTIFIED": return "Student notified";
            case "LETTER_ISSUED": return "Letter issued";
            case "MARKS_ACTION": return "Marks action";
            case "FEES_ACTION": return "Fees action";
            case "CORRECTION": return "Correction";
            case "CASE_CLOSED": return "Case closed";
            case "CASE_WITHDRAWN": return "Case withdrawn";
            case "SYSTEM": return "System";
            default: return t ?? "";
        }
    }

    public static string PanelRole(string r)
    {
        switch ((r ?? "").ToUpperInvariant())
        {
            case "CHAIR": return "Chair";
            case "MEMBER": return "Member";
            case "SECRETARY": return "Secretary";
            case "STUDENT_REP": return "Student representative";
            case "APPELLATE": return "Appellate authority";
            default: return r ?? "";
        }
    }

    public static string Plural(long n, string one, string many) { return FaFmt.Plural(n, one, many); }

    /// <summary>A name inside a sentence: "Examination malpractice" becomes "examination malpractice", but "ICT" stays "ICT".</summary>
    public static string Lc(string s)
    {
        s = s ?? "";
        if (s.Length == 0) return s;
        if (s.Length > 1 && char.IsUpper(s[1])) return s;
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}

public static class DcLog
{
    public static void Error(string where, Exception ex)
    {
        try
        {
            string dir = HttpContext.Current != null ? HttpContext.Current.Server.MapPath("~/App_Data/Discipline") : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data\\Discipline");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "errors.log"),
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + where + " " + (HttpContext.Current != null ? DcAccess.Username() : "") + " " + ex + Environment.NewLine);
        }
        catch { }
    }
}
