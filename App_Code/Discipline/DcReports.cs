using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Student Disciplinary module: reports R1 to R11 (plan section 9).
//  Each report produces rows once; the same rows feed the on-screen
//  preview, the PDF, the Excel workbook and the CSV. Every query goes
//  through the case visibility filter, so restricted cases appear only
//  for people who may see them (the cover says so).
// =====================================================================
public class DcCol
{
    public string Key, Header; public bool Numeric, Sum, On = true; public float Width;
    public DcCol(string key, string header, float width) { Key = key; Header = header; Width = width; }
    public DcCol Num() { Numeric = true; Sum = true; return this; }
    public DcCol Count() { Numeric = true; return this; }
    public DcCol Off() { On = false; return this; }
}

public class DcReportData
{
    public string Key, Title, Subtitle, FileWhat;
    public List<KeyValuePair<string, string>> Cover = new List<KeyValuePair<string, string>>();
    public List<DcCol> Cols = new List<DcCol>();
    public List<object[]> Rows = new List<object[]>();
    public List<string> Groups;
    public bool ShowTotals = true, PageBreakPerGroup;
    public string FootNote, Noun = "case", NounPlural = "cases";
    public string[] Signatories;
    public List<string> Prose;
    public bool? Landscape;
    public int? CaseId;
}

public static class DcReports
{
    public class Def
    {
        public string Key, Title, Description;
        public string[] Filters;   // period, year, campus, faculty, type, severity, status, date, hearingRange, case, version
        public string[] Groups;
        public List<DcCol> Cols;
    }

    private static List<DcCol> L(params DcCol[] c) { return new List<DcCol>(c); }

    public static List<Def> Catalogue()
    {
        return new List<Def> {
            new Def { Key = "register", Title = "Disciplinary register", Description = "Every case reported in the period or academic year, with its status, decision and sanctions.",
                Filters = new[] { "period", "year", "campus", "faculty", "type", "severity", "status" }, Groups = new[] { "", "faculty", "campus", "type", "status" },
                Cols = L(new DcCol("case_no", "Case no", 80), new DcCol("reported", "Reported", 62), new DcCol("student", "Student", 120), new DcCol("regno", "Reg no", 90),
                         new DcCol("programme", "Programme", 120), new DcCol("campus", "Campus", 60).Off(), new DcCol("type", "Type", 100), new DcCol("severity", "Severity", 50),
                         new DcCol("status", "Status", 70), new DcCol("decided", "Decided", 62), new DcCol("sanctions", "Sanctions", 140), new DcCol("officer", "Officer", 70).Off()) },
            new Def { Key = "by_type", Title = "Cases by type and severity", Description = "Counts and outcomes for each type of offence, and the average days from report to decision.",
                Filters = new[] { "period", "year", "campus", "faculty" }, Groups = new[] { "" },
                Cols = L(new DcCol("type", "Type", 150), new DcCol("severity", "Severity", 60), new DcCol("opened", "Opened", 50).Num(), new DcCol("decided", "Decided", 50).Num(),
                         new DcCol("cleared", "Dismissed or acquitted", 70).Num(), new DcCol("open", "Still open", 50).Num(), new DcCol("avg", "Average days to decision", 70).Count()) },
            new Def { Key = "by_area", Title = "Cases by faculty, department and programme", Description = "Cases in each programme by stage, grouped by faculty.",
                Filters = new[] { "period", "year", "campus", "type" }, Groups = new[] { "faculty" },
                Cols = L(new DcCol("department", "Department", 120), new DcCol("programme", "Programme", 150), new DcCol("reported", "Awaiting hearing", 55).Num(),
                         new DcCol("heard", "Awaiting decision", 55).Num(), new DcCol("decided", "Decided", 50).Num(), new DcCol("appeal", "Under appeal", 50).Num(),
                         new DcCol("closed", "Closed", 45).Num(), new DcCol("withdrawn", "Withdrawn", 50).Num(), new DcCol("total", "Total", 45).Num()) },
            new Def { Key = "under_sanction", Title = "Students currently under sanction", Description = "Every sanction or interim measure in force today.",
                Filters = new[] { "campus", "faculty", "effect" }, Groups = new[] { "", "sanction", "campus" },
                Cols = L(new DcCol("student", "Student", 130), new DcCol("regno", "Reg no", 90), new DcCol("programme", "Programme", 130), new DcCol("case_no", "Case no", 80),
                         new DcCol("sanction", "Sanction", 110), new DcCol("source", "Basis", 70), new DcCol("from", "From", 62), new DcCol("to", "To", 62), new DcCol("effects", "Effects", 140)) },
            new Def { Key = "hearings", Title = "Hearing schedule", Description = "Hearings in a date range, by day.",
                Filters = new[] { "hearingRange", "campus", "faculty" }, Groups = new[] { "date" },
                Cols = L(new DcCol("time", "Time", 50), new DcCol("venue", "Venue", 100), new DcCol("case_no", "Case no", 85), new DcCol("student", "Student", 130),
                         new DcCol("regno", "Reg no", 90), new DcCol("type", "Type", 110), new DcCol("panel", "Panel", 150)) },
            new Def { Key = "summons", Title = "Summon list", Description = "Students summoned for one hearing day, by campus, in the layout of the Registrar's memo.",
                Filters = new[] { "date" }, Groups = new[] { "campus" },
                Cols = L(new DcCol("student", "Name of student", 160), new DcCol("regno", "Reg no", 100), new DcCol("programme", "Programme", 150),
                         new DcCol("case_no", "Case no", 85), new DcCol("time", "Time", 50), new DcCol("venue", "Venue", 100)) },
            new Def { Key = "appeals", Title = "Appeals register", Description = "Every appeal lodged in the period, with its outcome.",
                Filters = new[] { "period", "year", "campus", "faculty" }, Groups = new[] { "" },
                Cols = L(new DcCol("case_no", "Case no", 80), new DcCol("student", "Student", 120), new DcCol("regno", "Reg no", 90), new DcCol("lodged", "Lodged", 62),
                         new DcCol("window", "Within window", 50), new DcCol("grounds", "Grounds", 200), new DcCol("outcome", "Outcome", 70), new DcCol("decided", "Decided", 62),
                         new DcCol("by", "Decided by", 70)) },
            new Def { Key = "statement", Title = "Case statement", Description = "One case in full: the committee copy has every entry; the student copy has only what the student may see.",
                Filters = new[] { "case", "version" }, Groups = new[] { "" },
                Cols = L(new DcCol("date", "Date", 80), new DcCol("entry", "Entry", 90), new DcCol("details", "Details", 330), new DcCol("by", "Recorded by", 80)) },
            new Def { Key = "senate", Title = "Senate summary", Description = "The academic year in figures, with a written summary for Senate.",
                Filters = new[] { "year" }, Groups = new[] { "section" },
                Cols = L(new DcCol("item", "Item", 260), new DcCol("count", "Cases", 60).Count(), new DcCol("share", "Share", 60)) },
            new Def { Key = "follow_ups", Title = "Follow-ups owed", Description = "Cancellations, fines and restitution decided but not yet carried out by the marks office or the Bursar.",
                Filters = new[] { "campus", "faculty", "kind" }, Groups = new[] { "", "kind" },
                Cols = L(new DcCol("case_no", "Case no", 80), new DcCol("student", "Student", 130), new DcCol("regno", "Reg no", 90), new DcCol("sanction", "Sanction", 120),
                         new DcCol("kind", "Owed by", 70), new DcCol("amount", "Amount (UGX)", 70).Num(), new DcCol("detail", "Course or semester", 110), new DcCol("since", "Decided", 62)) },
            new Def { Key = "undelivered", Title = "Notices not delivered", Description = "Portal notices unread after 7 days, and emails that failed or had no address.",
                Filters = new[] { "campus", "faculty" }, Groups = new[] { "" },
                Cols = L(new DcCol("case_no", "Case no", 80), new DcCol("student", "Student", 120), new DcCol("regno", "Reg no", 90), new DcCol("notice", "Notice", 160),
                         new DcCol("sent", "Sent", 70), new DcCol("read", "Read on portal", 70), new DcCol("email", "Email", 60), new DcCol("to", "Email address", 120), new DcCol("error", "Error", 120).Off()) }
        };
    }

    public static Def Find(string key) { foreach (Def d in Catalogue()) if (d.Key == key) return d; return null; }

    public static object CatalogueJson()
    {
        var l = new List<object>();
        foreach (Def d in Catalogue())
        {
            var cols = new List<object>(); foreach (DcCol c in d.Cols) cols.Add(new { k = c.Key, t = c.Header, on = c.On });
            var groups = new List<object>(); foreach (string g in d.Groups) groups.Add(new { k = g, t = GroupLabel(g) });
            l.Add(new { key = d.Key, title = d.Title, description = d.Description, filters = d.Filters, groups = groups, cols = cols });
        }
        return l;
    }

    private static string GroupLabel(string g)
    {
        switch (g)
        {
            case "": return "No grouping";
            case "faculty": return "Faculty";
            case "campus": return "Campus";
            case "type": return "Case type";
            case "status": return "Status";
            case "sanction": return "Sanction";
            case "date": return "Date";
            case "section": return "Section";
            case "kind": return "Owed by";
            default: return g;
        }
    }

    // ── Filters shared by the case-based reports ─────────────────────

    private static string CaseWhere(Dictionary<string, object> f, List<object> p, List<KeyValuePair<string, string>> cover, MySqlConnection c, string dateCol)
    {
        var sb = new StringBuilder(" WHERE 1=1");
        sb.Append(DcAccess.CaseFilter("c")); p.Add(DcAccess.MeParam); p.Add(DcAccess.Username());
        string v;
        if ((v = FaJson.Str(f, "year")) != "") { sb.Append(" AND c.acad_year=@yr"); p.Add("@yr"); p.Add(v); cover.Add(Kv("Academic year", v)); }
        DateTime? from = FaJson.Date(f, "from"), to = FaJson.Date(f, "to");
        if (dateCol != null && (from.HasValue || to.HasValue))
        {
            if (from.HasValue) { sb.Append(" AND " + dateCol + ">=@from"); p.Add("@from"); p.Add(from.Value); }
            if (to.HasValue) { sb.Append(" AND " + dateCol + "<@to"); p.Add("@to"); p.Add(to.Value.AddDays(1)); }
            cover.Add(Kv("Period", (from.HasValue ? DcFmt.Date(from) : "the start") + " to " + (to.HasValue ? DcFmt.Date(to) : "today")));
        }
        if ((v = FaJson.Str(f, "campus")) != "")
        {
            sb.Append(" AND c.campus_id=@campus"); p.Add("@campus"); p.Add(FaJson.Int(f, "campus"));
            cover.Add(Kv("Campus", DcLookups.Title(FaDb.S(FaDb.Scalar(c, null, "SELECT campus_name FROM acad_campuses WHERE ID=@i", "@i", FaJson.Int(f, "campus"))))));
        }
        if ((v = FaJson.Str(f, "faculty")) != "")
        {
            sb.Append(" AND c.faculty_code=@fac"); p.Add("@fac"); p.Add(v);
            cover.Add(Kv("Faculty", DcLookups.Title(FaDb.S(FaDb.Scalar(c, null, "SELECT faculty_name FROM acad_faculty WHERE faculty_code=@f", "@f", v)))));
        }
        if ((v = FaJson.Str(f, "type")) != "")
        {
            sb.Append(" AND c.case_type_id=@type"); p.Add("@type"); p.Add(FaJson.Int(f, "type"));
            cover.Add(Kv("Case type", FaDb.S(FaDb.Scalar(c, null, "SELECT name FROM dc_case_type WHERE id=@i", "@i", FaJson.Int(f, "type")))));
        }
        if ((v = FaJson.Str(f, "severity")) != "") { sb.Append(" AND c.severity=@sev"); p.Add("@sev"); p.Add(v); cover.Add(Kv("Severity", DcFmt.Severity(v))); }
        if ((v = FaJson.Str(f, "status")) != "")
        {
            if (v == "open") { sb.Append(" AND c.status NOT IN ('CLOSED','WITHDRAWN')"); cover.Add(Kv("Status", "Open cases")); }
            else { sb.Append(" AND c.status=@st"); p.Add("@st"); p.Add(v); cover.Add(Kv("Status", DcFmt.Status(v))); }
        }
        return sb.ToString();
    }

    private static KeyValuePair<string, string> Kv(string k, string v) { return new KeyValuePair<string, string>(k, v); }

    private static string Visibility()
    {
        return DcAccess.Current().SeeRestricted ? "Includes restricted cases (the exporter may see them)" : "Restricted cases are not included";
    }

    // ── Run ──────────────────────────────────────────────────────────

    public static DcReportData Run(string key, Dictionary<string, object> f, string groupBy)
    {
        Def def = Find(key);
        if (def == null) throw new DcRefusal("Choose a report.");
        var d = new DcReportData { Key = key, Title = def.Title, FileWhat = key.Replace('_', '-') };
        foreach (DcCol col in def.Cols) d.Cols.Add(new DcCol(col.Key, col.Header, col.Width) { Numeric = col.Numeric, Sum = col.Sum, On = col.On });
        if (Array.IndexOf(def.Groups, groupBy ?? "") < 0) groupBy = def.Groups[0];
        using (var c = FaDb.Open())
        {
            var p = new List<object>();
            switch (key)
            {
                case "register": Register(c, d, f, groupBy); break;
                case "by_type": ByType(c, d, f); break;
                case "by_area": ByArea(c, d, f); break;
                case "under_sanction": UnderSanction(c, d, f, groupBy); break;
                case "hearings": Hearings(c, d, f); break;
                case "summons": Summons(c, d, f); break;
                case "appeals": Appeals(c, d, f); break;
                case "statement": Statement(c, d, f); break;
                case "senate": Senate(c, d, f); break;
                case "follow_ups": FollowUps(c, d, f, groupBy); break;
                case "undelivered": Undelivered(c, d, f); break;
            }
        }
        if (key != "statement") d.Cover.Add(Kv("Visibility", Visibility()));
        return d;
    }

    public static int Count(string key, Dictionary<string, object> f, string groupBy)
    {
        try { return Run(key, f, groupBy).Rows.Count; } catch (DcRefusal) { return 0; }
    }

    private static string Grp(DataRow r, string groupBy)
    {
        switch (groupBy)
        {
            case "faculty": return FaDb.S(r["faculty_name"]) == "" ? "Faculty not recorded" : DcLookups.Title(FaDb.S(r["faculty_name"]));
            case "campus": return FaDb.S(r["campus_name"]) == "" ? "Campus not recorded" : DcLookups.Title(FaDb.S(r["campus_name"]));
            case "type": return FaDb.S(r["type_name"]);
            case "status": return DcFmt.Status(FaDb.S(r["status"]));
            default: return "";
        }
    }

    private static string OrderFor(string groupBy)
    {
        switch (groupBy)
        {
            case "faculty": return "faculty_name, ";
            case "campus": return "campus_name, ";
            case "type": return "type_name, ";
            case "status": return "FIELD(c.status,'REPORTED','UNDER_INVESTIGATION','HEARING_SCHEDULED','SUMMONED','HEARD','DECIDED','UNDER_APPEAL','APPEAL_DECIDED','CLOSED','WITHDRAWN'), ";
            default: return "";
        }
    }

    private static void Register(MySqlConnection c, DcReportData d, Dictionary<string, object> f, string groupBy)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, "c.created_at");
        DataTable t = FaDb.Table(c, null,
            "SELECT c.*, t.name AS type_name, IFNULL(pr.progname, c.progcode) AS progname, IFNULL(cp.campus_name,'') AS campus_name, IFNULL(fa.faculty_name,'') AS faculty_name, " +
            "(SELECT GROUP_CONCAT(st.name ORDER BY s.id SEPARATOR ', ') FROM dc_sanction s JOIN dc_sanction_type st ON st.id=s.sanction_type_id " +
            "  WHERE s.case_id=c.id AND s.source<>'INTERIM' AND s.status NOT IN ('VARIED','SET_ASIDE')) AS sanctions " +
            "FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id LEFT JOIN acad_programme pr ON pr.progcode=c.progcode " +
            "LEFT JOIN acad_campuses cp ON cp.ID=c.campus_id LEFT JOIN acad_faculty fa ON fa.faculty_code=c.faculty_code" + w + " ORDER BY " + OrderFor(groupBy) + "c.id", p.ToArray());
        if (groupBy != "") d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] { FaDb.S(r["case_no"]), DcFmt.Date(r["created_at"]), FaDb.S(r["student_name"]), FaDb.S(r["regno"]), FaDb.S(r["progname"]),
                DcLookups.Title(FaDb.S(r["campus_name"])), FaDb.S(r["type_name"]), DcFmt.Severity(FaDb.S(r["severity"])), DcFmt.Status(FaDb.S(r["status"])),
                DcFmt.Date(r["decided_at"]), FaDb.S(r["sanctions"]), FaDb.S(r["case_officer"]) });
            if (d.Groups != null) d.Groups.Add(Grp(r, groupBy));
        }
        d.Landscape = true;
    }

    private static void ByType(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, "c.created_at");
        DataTable t = FaDb.Table(c, null,
            "SELECT t.name, c.severity, COUNT(*) opened, SUM(c.decided_at IS NOT NULL) decided, " +
            "SUM(EXISTS(SELECT 1 FROM dc_entry e WHERE e.case_id=c.id AND e.entry_type='DECISION' AND (e.title LIKE '%dismissed%' OR e.title LIKE '%acquitted%'))) cleared, " +
            "SUM(c.status NOT IN ('CLOSED','WITHDRAWN')) open_n, AVG(CASE WHEN c.decided_at IS NOT NULL THEN DATEDIFF(c.decided_at, c.created_at) END) avg_days " +
            "FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id" + w + " GROUP BY t.id, t.name, c.severity ORDER BY t.sort_order, t.name, FIELD(c.severity,'MINOR','SERIOUS','GROSS')", p.ToArray());
        foreach (DataRow r in t.Rows)
            d.Rows.Add(new object[] { FaDb.S(r[0]), DcFmt.Severity(FaDb.S(r[1])), FaDb.M(r[2]), FaDb.M(r[3]), FaDb.M(r[4]), FaDb.M(r[5]),
                r[6] == DBNull.Value ? "" : Math.Round(FaDb.M(r[6]), 1).ToString("0.0", CultureInfo.InvariantCulture) });
    }

    private static void ByArea(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, "c.created_at");
        DataTable t = FaDb.Table(c, null,
            "SELECT IFNULL(fa.faculty_name,'') AS faculty_name, IFNULL(dp.dept_name,'Department not recorded') AS dept, IFNULL(pr.progname, IFNULL(c.progcode,'Programme not recorded')) AS prog, " +
            "SUM(c.status IN ('REPORTED','UNDER_INVESTIGATION','HEARING_SCHEDULED','SUMMONED')) a, SUM(c.status='HEARD') b, SUM(c.status IN ('DECIDED','APPEAL_DECIDED')) dd, " +
            "SUM(c.status='UNDER_APPEAL') ap, SUM(c.status='CLOSED') cl, SUM(c.status='WITHDRAWN') wd, COUNT(*) tot " +
            "FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id LEFT JOIN acad_programme pr ON pr.progcode=c.progcode LEFT JOIN hrm_departments dp ON dp.ID=c.department_id " +
            "LEFT JOIN acad_faculty fa ON fa.faculty_code=c.faculty_code" + w + " GROUP BY 1,2,3 ORDER BY 1,2,3", p.ToArray());
        d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] { FaDb.S(r["dept"]), FaDb.S(r["prog"]), FaDb.M(r["a"]), FaDb.M(r["b"]), FaDb.M(r["dd"]), FaDb.M(r["ap"]), FaDb.M(r["cl"]), FaDb.M(r["wd"]), FaDb.M(r["tot"]) });
            d.Groups.Add(FaDb.S(r["faculty_name"]) == "" ? "Faculty not recorded" : DcLookups.Title(FaDb.S(r["faculty_name"])));
        }
        d.Noun = "programme"; d.NounPlural = "programmes"; d.Landscape = true;
    }

    private static void UnderSanction(MySqlConnection c, DcReportData d, Dictionary<string, object> f, string groupBy)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, null);
        string eff = FaJson.Str(f, "effect");
        if (eff != "") { w += " AND FIND_IN_SET(@eff, s.effects)>0"; p.Add("@eff"); p.Add(eff); d.Cover.Add(Kv("Effect", DcFmt.Effect(eff))); }
        d.Subtitle = "as at " + DateTime.Today.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        DataTable t = FaDb.Table(c, null,
            "SELECT c.case_no, c.student_name, c.regno, IFNULL(pr.progname, c.progcode) AS progname, st.name AS sanction, s.source, s.starts_on, s.ends_on, s.effects, " +
            "IFNULL(cp.campus_name,'') AS campus_name FROM dc_sanction s JOIN dc_case c ON c.id=s.case_id JOIN dc_case_type t ON t.id=c.case_type_id " +
            "JOIN dc_sanction_type st ON st.id=s.sanction_type_id LEFT JOIN acad_programme pr ON pr.progcode=c.progcode LEFT JOIN acad_campuses cp ON cp.ID=c.campus_id" + w +
            " AND s.status='ACTIVE' AND s.starts_on<=CURDATE() AND (s.ends_on IS NULL OR s.ends_on>=CURDATE()) ORDER BY " +
            (groupBy == "sanction" ? "st.sort_order, " : groupBy == "campus" ? "campus_name, " : "") + "c.student_name, s.id", p.ToArray());
        if (groupBy != "") d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] { FaDb.S(r["student_name"]), FaDb.S(r["regno"]), FaDb.S(r["progname"]), FaDb.S(r["case_no"]), FaDb.S(r["sanction"]),
                DcFmt.Source(FaDb.S(r["source"])), DcFmt.Date(r["starts_on"]), r["ends_on"] == DBNull.Value ? "Open" : DcFmt.Date(r["ends_on"]), DcFmt.Effects(FaDb.S(r["effects"])) });
            if (d.Groups != null) d.Groups.Add(groupBy == "sanction" ? FaDb.S(r["sanction"]) : DcLookups.Title(FaDb.S(r["campus_name"])));
        }
        d.Noun = "sanction"; d.NounPlural = "sanctions"; d.Landscape = true;
    }

    private static void Hearings(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, null);
        DateTime from = FaJson.Date(f, "from") ?? DateTime.Today, to = FaJson.Date(f, "to") ?? DateTime.Today.AddDays(30);
        p.Add("@hf"); p.Add(from); p.Add("@ht"); p.Add(to.AddDays(1));
        d.Subtitle = DcFmt.Date(from) + " to " + DcFmt.Date(to);
        DataTable t = FaDb.Table(c, null,
            "SELECT h.scheduled_at, h.venue, h.panel, h.status AS hstatus, c.case_no, c.student_name, c.regno, t.name AS type_name FROM dc_hearing h JOIN dc_case c ON c.id=h.case_id " +
            "JOIN dc_case_type t ON t.id=c.case_type_id" + w + " AND h.status IN ('SCHEDULED','HELD') AND h.scheduled_at>=@hf AND h.scheduled_at<@ht ORDER BY h.scheduled_at, c.case_no", p.ToArray());
        d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] { DcFmt.Time(r["scheduled_at"]), FaDb.S(r["venue"]), FaDb.S(r["case_no"]), FaDb.S(r["student_name"]), FaDb.S(r["regno"]),
                FaDb.S(r["type_name"]) + (FaDb.S(r["hstatus"]) == "HELD" ? " (held)" : ""), FaDb.S(r["panel"]) });
            d.Groups.Add(FaDb.D(r["scheduled_at"]).Value.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture));
        }
        d.Noun = "hearing"; d.NounPlural = "hearings"; d.Landscape = true;
    }

    private static void Summons(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, null);
        DateTime day = FaJson.Date(f, "date") ?? DateTime.Today;
        p.Add("@d0"); p.Add(day); p.Add("@d1"); p.Add(day.AddDays(1));
        d.Subtitle = day.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
        DataTable t = FaDb.Table(c, null,
            "SELECT h.scheduled_at, h.venue, c.case_no, c.student_name, c.regno, IFNULL(pr.progname, c.progcode) AS progname, IFNULL(cp.campus_name,'') AS campus_name " +
            "FROM dc_hearing h JOIN dc_case c ON c.id=h.case_id JOIN dc_case_type t ON t.id=c.case_type_id LEFT JOIN acad_programme pr ON pr.progcode=c.progcode " +
            "LEFT JOIN acad_campuses cp ON cp.ID=c.campus_id" + w + " AND h.status='SCHEDULED' AND c.status='SUMMONED' AND h.scheduled_at>=@d0 AND h.scheduled_at<@d1 " +
            "ORDER BY campus_name, h.scheduled_at, c.student_name", p.ToArray());
        d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            d.Rows.Add(new object[] { FaDb.S(r["student_name"]).ToUpperInvariant(), FaDb.S(r["regno"]), FaDb.S(r["progname"]), FaDb.S(r["case_no"]),
                DcFmt.Time(r["scheduled_at"]), FaDb.S(r["venue"]) });
            d.Groups.Add(FaDb.S(r["campus_name"]) == "" ? "Campus not recorded" : DcLookups.Title(FaDb.S(r["campus_name"])));
        }
        d.Signatories = new[] { "Academic Registrar" };
        d.FootNote = "The students listed are summoned to appear before the Students Disciplinary Committee at the time and venue shown. Each has been sent a summons letter on the student portal.";
        d.Noun = "student"; d.NounPlural = "students";
    }

    private static void Appeals(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, "a.lodged_at");
        DataTable t = FaDb.Table(c, null,
            "SELECT a.*, c.case_no, c.student_name, c.regno FROM dc_appeal a JOIN dc_case c ON c.id=a.case_id JOIN dc_case_type t ON t.id=c.case_type_id" + w + " ORDER BY a.lodged_at", p.ToArray());
        foreach (DataRow r in t.Rows)
        {
            string g = FaDb.S(r["grounds"]); if (g.Length > 220) g = g.Substring(0, 217) + "...";
            string st = FaDb.S(r["status"]);
            d.Rows.Add(new object[] { FaDb.S(r["case_no"]), FaDb.S(r["student_name"]), FaDb.S(r["regno"]), DcFmt.Date(r["lodged_at"]),
                FaDb.I(r["within_window"]) == 1 ? "Yes" : "No (accepted late)", g,
                st == "LODGED" ? "Pending" : st == "UPHELD" ? "Upheld" : st == "DISMISSED" ? "Dismissed" : st == "VARIED" ? "Varied" : "Withdrawn",
                DcFmt.Date(r["decided_at"]), FaDb.S(r["decided_by"]) });
        }
        d.Noun = "appeal"; d.NounPlural = "appeals"; d.Landscape = true;
    }

    private static void Statement(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        int caseId = FaJson.Int(f, "caseId");
        DataRow cs = DcAccess.VisibleCase(c, null, caseId, false);
        if (cs == null) throw new DcRefusal("Choose a case you may see.");
        DcAccess.LogRestrictedAccess(c, cs, "EXPORT");
        bool student = FaJson.Str(f, "version") == "student";
        d.CaseId = caseId;
        d.Title = student ? "Case statement (student copy)" : "Case statement (committee copy)";
        d.Subtitle = FaDb.S(cs["case_no"]);
        d.FileWhat = "statement-" + FaDb.S(cs["case_no"]).Replace("/", "-");
        DataRow inc = FaDb.Table(c, null, "SELECT * FROM dc_incident WHERE id=@i", "@i", FaDb.I(cs["incident_id"])).Rows[0];
        d.Cover.Add(Kv("Student", FaDb.S(cs["student_name"]) + ", " + FaDb.S(cs["regno"])));
        d.Cover.Add(Kv("Programme", FaDb.S(FaDb.Scalar(c, null, "SELECT progname FROM acad_programme WHERE progcode=@p", "@p", FaDb.S(cs["progcode"]))) ));
        d.Cover.Add(Kv("Case", FaDb.S(cs["type_name"]) + ", " + DcFmt.Severity(FaDb.S(cs["severity"])).ToLowerInvariant() + ". Incident " + DcFmt.When(inc["occurred_at"]) +
                       (FaDb.S(inc["place"]) == "" ? "" : " at " + FaDb.S(inc["place"]))));
        d.Cover.Add(Kv("Status", DcFmt.Status(FaDb.S(cs["status"])) + (FaDb.D(cs["appeal_deadline"]).HasValue ? ". Appeal deadline " + DcFmt.Date(cs["appeal_deadline"]) : "")));
        if (!student && FaDb.I(cs["is_restricted"]) == 1) d.Cover.Add(Kv("Confidential", "Restricted case. Opening and export are logged."));
        DataTable t = FaDb.Table(c, null, "SELECT * FROM dc_entry WHERE case_id=@c" + (student ? " AND student_visible=1" : "") + " ORDER BY entry_at, id", "@c", caseId);
        foreach (DataRow r in t.Rows)
        {
            if (student && FaDb.S(r["entry_type"]) == "STUDENT_NOTIFIED") continue;
            string body = FaDb.S(r["body"]);
            d.Rows.Add(new object[] { DcFmt.When(r["entry_at"]), DcFmt.EntryType(FaDb.S(r["entry_type"])),
                FaDb.S(r["title"]) + (body == "" ? "" : "\n" + body), student ? (FaDb.S(r["interface"]) == "SYSTEM" ? "System" : "University") : FaDb.S(r["recorded_by"]) });
        }
        d.Noun = "entry"; d.NounPlural = "entries";
        d.Signatories = student ? new[] { "Issued by (Office of the Dean of Students)" } : new[] { "Secretary to the Committee", "Chair of the Committee" };
        d.FootNote = student ? "This copy shows every entry the student may see: decisions, sanctions, hearings, letters and appeals." :
                               "Committee copy: every entry in the case file, including internal entries. Confidential.";
    }

    private static void FollowUps(MySqlConnection c, DcReportData d, Dictionary<string, object> f, string groupBy)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, null);
        string kind = FaJson.Str(f, "kind");
        if (kind == "marks") { w += " AND (FIND_IN_SET('CANCEL_PAPER',s.effects)>0 OR FIND_IN_SET('CANCEL_SEMESTER',s.effects)>0)"; d.Cover.Add(Kv("Owed by", "Marks office")); }
        if (kind == "fees") { w += " AND (FIND_IN_SET('FINE',s.effects)>0 OR FIND_IN_SET('RESTITUTION',s.effects)>0)"; d.Cover.Add(Kv("Owed by", "Bursar")); }
        DataTable t = FaDb.Table(c, null,
            "SELECT c.case_no, c.student_name, c.regno, st.name AS sanction, s.effects, s.amount, s.course_code, s.acad_year, s.semester, s.created_at FROM dc_sanction s " +
            "JOIN dc_case c ON c.id=s.case_id JOIN dc_case_type t ON t.id=c.case_type_id JOIN dc_sanction_type st ON st.id=s.sanction_type_id" + w +
            " AND s.follow_up='PENDING' AND s.status IN ('ACTIVE','EXPIRED') ORDER BY s.created_at", p.ToArray());
        if (groupBy != "") d.Groups = new List<string>();
        foreach (DataRow r in t.Rows)
        {
            string e = "," + FaDb.S(r["effects"]) + ",";
            string owed = e.Contains(",FINE,") || e.Contains(",RESTITUTION,") ? "Bursar" : "Marks office";
            string detail = FaDb.S(r["course_code"]) + (FaDb.S(r["acad_year"]) == "" ? "" : (FaDb.S(r["course_code"]) == "" ? "" : ", ") + FaDb.S(r["acad_year"]) + " semester " + FaDb.I(r["semester"]));
            d.Rows.Add(new object[] { FaDb.S(r["case_no"]), FaDb.S(r["student_name"]), FaDb.S(r["regno"]), FaDb.S(r["sanction"]), owed,
                r["amount"] == DBNull.Value ? 0m : FaDb.M(r["amount"]), detail, DcFmt.Date(r["created_at"]) });
            if (d.Groups != null) d.Groups.Add(owed);
        }
        d.Noun = "action"; d.NounPlural = "actions";
    }

    private static void Undelivered(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, null);
        DataTable t = FaDb.Table(c, null,
            "SELECT c.case_no, c.student_name, c.regno, n.title, n.created_at, n.portal_read_at, n.email_status, n.email_to, n.email_error FROM dc_notification n " +
            "JOIN dc_case c ON c.id=n.case_id JOIN dc_case_type t ON t.id=c.case_type_id" + w +
            " AND ((n.portal_read_at IS NULL AND n.created_at < DATE_SUB(NOW(), INTERVAL 7 DAY)) OR n.email_status IN ('FAILED','NO_ADDRESS')) ORDER BY n.created_at", p.ToArray());
        foreach (DataRow r in t.Rows)
        {
            string es = FaDb.S(r["email_status"]);
            d.Rows.Add(new object[] { FaDb.S(r["case_no"]), FaDb.S(r["student_name"]), FaDb.S(r["regno"]), FaDb.S(r["title"]), DcFmt.Date(r["created_at"]),
                r["portal_read_at"] == DBNull.Value ? "Not read" : DcFmt.Date(r["portal_read_at"]),
                es == "SENT" ? "Sent" : es == "FAILED" ? "Failed" : es == "NO_ADDRESS" ? "No address" : es == "PENDING" ? "Waiting" : "Not sent",
                FaDb.S(r["email_to"]), FaDb.S(r["email_error"]) });
        }
        d.Noun = "notice"; d.NounPlural = "notices"; d.Landscape = true;
    }

    // ── Senate summary (R9): figures plus a written summary ───────────

    private static void Senate(MySqlConnection c, DcReportData d, Dictionary<string, object> f)
    {
        string year = FaJson.Str(f, "year"); if (year == "") { year = DcSeq.AcadYear(); f["year"] = year; }
        var p = new List<object>();
        string w = CaseWhere(f, p, d.Cover, c, null);
        d.Subtitle = year;
        const string B = " FROM dc_case c JOIN dc_case_type t ON t.id=c.case_type_id ";
        int total = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(*)" + B + w, p.ToArray()));
        d.Groups = new List<string>();
        Action<string, string, int> add = (sec, item, n) =>
        {
            bool share = total > 0 && sec != "Overview" && sec != "Appeals";
            d.Rows.Add(new object[] { item, n.ToString("#,##0", CultureInfo.InvariantCulture), share ? (100.0 * n / total).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "" });
            d.Groups.Add(sec);
        };
        add("Overview", "Cases reported", total);
        DataRow k = FaDb.Table(c, null, "SELECT SUM(c.status NOT IN ('CLOSED','WITHDRAWN')) o, SUM(c.decided_at IS NOT NULL) dd, SUM(c.status='WITHDRAWN') wd, " +
            "COUNT(DISTINCT c.regno) students, COUNT(DISTINCT c.incident_id) incidents, AVG(CASE WHEN c.decided_at IS NOT NULL THEN DATEDIFF(c.decided_at, c.created_at) END) avg_days" + B + w, p.ToArray()).Rows[0];
        add("Overview", "Students involved", FaDb.I(k["students"]));
        add("Overview", "Incidents", FaDb.I(k["incidents"]));
        add("Overview", "Decided", FaDb.I(k["dd"]));
        add("Overview", "Withdrawn", FaDb.I(k["wd"]));
        add("Overview", "Still open", FaDb.I(k["o"]));
        var byType = new List<KeyValuePair<string, int>>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT t.name, COUNT(*)" + B + w + " GROUP BY t.id, t.name ORDER BY COUNT(*) DESC, t.name", p.ToArray()).Rows)
        { add("By type of offence", FaDb.S(r[0]), FaDb.I(r[1])); byType.Add(new KeyValuePair<string, int>(FaDb.S(r[0]), FaDb.I(r[1]))); }
        foreach (DataRow r in FaDb.Table(c, null, "SELECT c.severity, COUNT(*)" + B + w + " GROUP BY c.severity ORDER BY FIELD(c.severity,'MINOR','SERIOUS','GROSS')", p.ToArray()).Rows)
            add("By severity", DcFmt.Severity(FaDb.S(r[0])), FaDb.I(r[1]));
        var byFac = new List<KeyValuePair<string, int>>();
        foreach (DataRow r in FaDb.Table(c, null, "SELECT IFNULL(fa.faculty_name,'Not recorded'), COUNT(*)" + B + " LEFT JOIN acad_faculty fa ON fa.faculty_code=c.faculty_code" + w + " GROUP BY 1 ORDER BY 2 DESC", p.ToArray()).Rows)
        { add("By faculty", DcLookups.Title(FaDb.S(r[0])), FaDb.I(r[1])); byFac.Add(new KeyValuePair<string, int>(DcLookups.Title(FaDb.S(r[0])), FaDb.I(r[1]))); }
        var outcomes = new List<KeyValuePair<string, int>>();
        foreach (DataRow r in FaDb.Table(c, null,
            "SELECT st.name, COUNT(DISTINCT s.case_id) FROM dc_sanction s JOIN dc_case c ON c.id=s.case_id JOIN dc_case_type t ON t.id=c.case_type_id JOIN dc_sanction_type st ON st.id=s.sanction_type_id" + w +
            " AND s.source IN ('DECISION','APPEAL') AND s.status NOT IN ('SET_ASIDE') GROUP BY st.id, st.name ORDER BY st.sort_order", p.ToArray()).Rows)
        { add("Sanctions decided", FaDb.S(r[0]), FaDb.I(r[1])); outcomes.Add(new KeyValuePair<string, int>(FaDb.S(r[0]), FaDb.I(r[1]))); }
        int cleared = FaDb.I(FaDb.Scalar(c, null, "SELECT COUNT(DISTINCT c.id)" + B + " JOIN dc_entry e ON e.case_id=c.id AND e.entry_type='DECISION' AND (e.title LIKE '%dismissed%' OR e.title LIKE '%acquitted%')" + w, p.ToArray()));
        add("Sanctions decided", "Dismissed or acquitted", cleared);
        DataRow ap = FaDb.Table(c, null, "SELECT COUNT(*) n, SUM(a.status='UPHELD') up, SUM(a.status='DISMISSED') dis, SUM(a.status='VARIED') var, SUM(a.status='LODGED') pend " +
            "FROM dc_appeal a JOIN dc_case c ON c.id=a.case_id JOIN dc_case_type t ON t.id=c.case_type_id" + w, p.ToArray()).Rows[0];
        add("Appeals", "Appeals lodged", FaDb.I(ap["n"]));
        add("Appeals", "Upheld (decision set aside)", FaDb.I(ap["up"]));
        add("Appeals", "Varied", FaDb.I(ap["var"]));
        add("Appeals", "Dismissed (decision stands)", FaDb.I(ap["dis"]));
        add("Appeals", "Still pending", FaDb.I(ap["pend"]));

        // The written summary: plain sentences built from the figures, no judgement words beyond what the numbers show.
        var prose = new List<string>();
        if (total == 0) prose.Add("No disciplinary cases were recorded for " + year + ".");
        else
        {
            prose.Add(DcFmt.Plural(total, "disciplinary case was", "disciplinary cases were") + " recorded in " + year + ", involving " +
                      DcFmt.Plural(FaDb.I(k["students"]), "student", "students") + " in " + DcFmt.Plural(FaDb.I(k["incidents"]), "incident", "incidents") + ". " +
                      DcFmt.Plural(FaDb.I(k["dd"]), "case has", "cases have") + " been decided, " + FaDb.I(k["wd"]) + " withdrawn and " + FaDb.I(k["o"]) + (FaDb.I(k["o"]) == 1 ? " remains" : " remain") + " open" +
                      (k["avg_days"] == DBNull.Value ? "." : "; a decision took " + Math.Round(FaDb.M(k["avg_days"]), 0).ToString(CultureInfo.InvariantCulture) + " days on average from the report."));
            if (byType.Count > 0)
            {
                var top = new List<string>();
                for (int i = 0; i < Math.Min(3, byType.Count); i++) top.Add(DcFmt.Lc(byType[i].Key) + " (" + byType[i].Value + ")");
                prose.Add("The most frequent offences were " + Join(top) + ".");
            }
            if (byFac.Count > 0) prose.Add(byFac[0].Key + " recorded the most cases (" + byFac[0].Value + ")" + (byFac.Count > 1 ? ", followed by " + byFac[1].Key + " (" + byFac[1].Value + ")." : "."));
            if (outcomes.Count > 0 || cleared > 0)
            {
                var o = new List<string>();
                foreach (var kv in outcomes) o.Add(DcFmt.Lc(kv.Key) + " in " + DcFmt.Plural(kv.Value, "case", "cases"));
                prose.Add("Sanctions decided: " + (o.Count == 0 ? "none" : Join(o)) + (cleared > 0 ? ". " + DcFmt.Plural(cleared, "case was", "cases were") + " dismissed or ended in acquittal." : "."));
            }
            int an = FaDb.I(ap["n"]);
            prose.Add(an == 0 ? "No appeals were lodged." : DcFmt.Plural(an, "appeal was", "appeals were") + " lodged: " + FaDb.I(ap["up"]) + " upheld, " + FaDb.I(ap["var"]) + " varied, " +
                      FaDb.I(ap["dis"]) + " dismissed and " + FaDb.I(ap["pend"]) + " pending.");
        }
        d.Prose = prose;
        d.FootNote = string.Join("\n\n", prose.ToArray());
        d.Signatories = new[] { "Dean of Students", "Academic Registrar" };
        d.ShowTotals = false; d.Noun = "line"; d.NounPlural = "lines";
    }

    private static string Join(List<string> l)
    {
        if (l.Count == 0) return "";
        if (l.Count == 1) return l[0];
        return string.Join(", ", l.GetRange(0, l.Count - 1).ToArray()) + " and " + l[l.Count - 1];
    }

    // ── Output ───────────────────────────────────────────────────────

    private static List<int> Pick(DcReportData d, string colKeys, bool pdf)
    {
        var idx = new List<int>();
        var want = new HashSet<string>((colKeys ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        for (int i = 0; i < d.Cols.Count; i++)
            if (want.Count == 0 ? d.Cols[i].On : want.Contains(d.Cols[i].Key)) idx.Add(i);
        if (idx.Count == 0) for (int i = 0; i < d.Cols.Count; i++) idx.Add(i);
        return idx;
    }

    private static string Text(object v, bool numeric)
    {
        if (v == null) return "";
        if (v is decimal) return numeric ? FaFmt.Money((decimal)v) : ((decimal)v).ToString(CultureInfo.InvariantCulture);
        return Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    public static void Send(HttpResponse resp, DcReportData d, string format, string colKeys)
    {
        string fileBase = DcExport.FileName(d.FileWhat);
        bool pdf = format == "pdf";
        var idx = Pick(d, colKeys, pdf);
        if (d.CaseId.HasValue) { }
        if (!pdf)
        {
            var sh = new DcExport.Sheet();
            sh.Name = d.Title; sh.Subtitle = d.Subtitle ?? "";
            var heads = new List<string>();
            if (d.Groups != null) heads.Add("Group");
            foreach (int i in idx) heads.Add(d.Cols[i].Header);
            sh.Columns = heads.ToArray();
            int off = d.Groups != null ? 1 : 0;
            for (int j = 0; j < idx.Count; j++) if (d.Cols[idx[j]].Numeric) sh.NumericColumns.Add(j + off);
            var sums = new decimal[idx.Count];
            for (int r = 0; r < d.Rows.Count; r++)
            {
                var cells = new List<string>();
                if (d.Groups != null) cells.Add(d.Groups[r]);
                for (int j = 0; j < idx.Count; j++)
                {
                    object v = d.Rows[r][idx[j]];
                    cells.Add(Text(v, d.Cols[idx[j]].Numeric));
                    if (d.Cols[idx[j]].Sum && v is decimal) sums[j] += (decimal)v;
                }
                sh.Rows.Add(cells.ToArray());
            }
            bool anySum = false; foreach (int i in idx) if (d.Cols[i].Sum) anySum = true;
            if (anySum && d.ShowTotals)
            {
                var tot = new List<string>();
                if (d.Groups != null) tot.Add("Total");
                bool labelled = d.Groups != null;
                for (int j = 0; j < idx.Count; j++)
                {
                    if (d.Cols[idx[j]].Sum) tot.Add(FaFmt.Num(sums[j]));
                    else if (!labelled) { tot.Add("Total"); labelled = true; }
                    else tot.Add("");
                }
                sh.Totals = tot.ToArray();
            }
            var cover = new List<KeyValuePair<string, string>>(d.Cover);
            if (d.Prose != null) for (int i = 0; i < d.Prose.Count; i++) cover.Add(Kv(i == 0 ? "Summary" : "", d.Prose[i]));
            if (format == "csv") DcExport.Csv(resp, fileBase, d.Title, d.Subtitle, cover, sh);
            else DcExport.Workbook(resp, fileBase, d.Title, d.Subtitle, cover, new List<DcExport.Sheet> { sh });
            return;
        }

        var spec = new DcPdf.Spec();
        spec.FileBase = fileBase; spec.Title = d.Title; spec.Subtitle = d.Subtitle; spec.Cover = d.Cover;
        spec.Grouped = d.Groups != null; spec.PageBreakPerGroup = d.PageBreakPerGroup;
        if (d.Signatories != null) spec.Signatories = d.Signatories;
        if (d.FootNote != null) spec.FootNote = d.FootNote;
        spec.ShowTotals = d.ShowTotals; spec.Landscape = d.Landscape;
        spec.Noun = d.Noun; spec.NounPlural = d.NounPlural;
        var t = new DataTable();
        foreach (int i in idx)
        {
            var c = d.Cols[i];
            t.Columns.Add("F" + i, c.Sum ? typeof(decimal) : typeof(string));
            spec.Cols.Add(new DcPdf.PCol("F" + i, c.Header, c.Width, c.Numeric, c.Sum));
        }
        if (spec.Grouped) t.Columns.Add(DcPdf.GROUP_COL, typeof(string));
        for (int r = 0; r < d.Rows.Count; r++)
        {
            DataRow row = t.NewRow();
            foreach (int i in idx)
            {
                object v = d.Rows[r][i];
                if (d.Cols[i].Sum) row["F" + i] = v is decimal ? (object)Math.Round((decimal)v, 0) : 0m;
                else row["F" + i] = Text(v, d.Cols[i].Numeric);
            }
            if (spec.Grouped) row[DcPdf.GROUP_COL] = d.Groups[r];
            t.Rows.Add(row);
        }
        spec.Rows = t;
        DcPdf.Send(resp, spec);
    }
}
