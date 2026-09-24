using System;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using System.Web.Services;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — Candidates.
//
//  The working queue: who looks ready, the evidence behind each one, and
//  the two decisions a reviewer can take. An independent page carrying
//  only its own endpoints.
//
//  Every write re-checks scope inside GraduationService. The buttons a
//  browser does or does not draw are not access control.
// =====================================================================
public partial class COOPERP_NewScreens_GraduationCandidates : System.Web.UI.Page
{
    private static readonly JavaScriptSerializer J = new JavaScriptSerializer();

    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    private const bool WITH_COUNTS = true;

    /// <summary>
    /// The filter lists, rendered into the page rather than fetched.
    ///
    /// This page used to open by firing GetBootstrap and waiting before it could even draw its
    /// controls. The answer is available at render time, so it ships with the page. AJAX is
    /// kept for the filter changes that follow.
    /// </summary>
    public string BootJson = "null";
    public string StatsAge = "";

    /// <summary>The export column catalogue, so the dialog builds its checkboxes from the same
    /// list the workbook is built from rather than a copy that can drift out of step.</summary>
    public string ColsJson = "[]";

    /// <summary>Ceiling on one export. 20,000 covers the whole backlog of 14,547 with room to
    /// spare, and still stops a runaway filter from building a file nobody wants.</summary>
    private const int EXPORT_CAP = 20000;

    private void Prime()
    {
        try
        {
            MarksScope sc = MarksScopeResolver.Resolve();
            StatsAge = GraduationStats.Freshness();
            ColsJson = GraduationBootstrap.ForScriptBlock(J.Serialize(GraduationExport.Catalogue(Catalogue())));
            if (sc.HasAccess)
                BootJson = GraduationBootstrap.ForScriptBlock(GraduationBootstrap.Bootstrap(WITH_COUNTS));
        }
        catch { BootJson = "null"; }
    }

    // =================================================================
    //  The column catalogue.
    //
    //  Declared once and used twice: it builds the checkboxes in the
    //  export dialog, and it builds the sheet that comes back. There is
    //  no second list to keep in step, so the dialog can never offer a
    //  column the workbook does not produce.
    // =================================================================
    private const string ID = "Identity", AC = "Academic", PR = "Progress", DC = "Decision";

    private static List<GraduationExport.Col<GradCandidate>> Catalogue()
    {
        var c = new List<GraduationExport.Col<GradCandidate>>();
        c.Add(new GraduationExport.Col<GradCandidate>("regno", "Student Number", ID, F_regno));
        c.Add(new GraduationExport.Col<GradCandidate>("name", "Name", ID, F_name));
        c.Add(new GraduationExport.Col<GradCandidate>("progcode", "Programme Code", ID, F_progcode));
        c.Add(new GraduationExport.Col<GradCandidate>("progname", "Programme", ID, F_progname));
        c.Add(new GraduationExport.Col<GradCandidate>("faculty", "Faculty", ID, F_faculty));
        c.Add(new GraduationExport.Col<GradCandidate>("spec", "Specialisation", ID, F_spec).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("intake", "Intake", ID, F_intake));

        c.Add(new GraduationExport.Col<GradCandidate>("yr", "Year Reached", AC, true, F_yr));
        c.Add(new GraduationExport.Col<GradCandidate>("plen", "Programme Length", AC, true, F_plen).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("level", "Award Level", AC, F_level).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("first", "First Sat", AC, F_first).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("last", "Last Sat", AC, F_last));
        c.Add(new GraduationExport.Col<GradCandidate>("courses", "Courses Taken", AC, true, F_courses).Off());

        c.Add(new GraduationExport.Col<GradCandidate>("cuE", "Credits Earned", PR, true, F_cuE));
        c.Add(new GraduationExport.Col<GradCandidate>("cuR", "Credits Required", PR, true, F_cuR));
        c.Add(new GraduationExport.Col<GradCandidate>("cuS", "Credits Short", PR, true, F_cuS).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("cuSrc", "Credit Source", PR, F_cuSrc).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("cgpa", "CGPA", PR, true, F_cgpa));
        c.Add(new GraduationExport.Col<GradCandidate>("class", "Class of Award", PR, F_class));
        c.Add(new GraduationExport.Col<GradCandidate>("fails", "Failed Papers", PR, true, F_fails));
        c.Add(new GraduationExport.Col<GradCandidate>("zero", "Zero Marks", PR, true, F_zero).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("unmarked", "Unmarked", PR, true, F_unmarked).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("unpub", "Marks Not Yet Published", PR, true, F_unpub).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("cover", "Required Courses With No Result", PR, true, F_cover).Off());

        c.Add(new GraduationExport.Col<GradCandidate>("ready", "Readiness", DC, F_ready));
        c.Add(new GraduationExport.Col<GradCandidate>("status", "Status", DC, F_status));
        c.Add(new GraduationExport.Col<GradCandidate>("blockers", "What Is Blocking Them", DC, F_blockers));
        c.Add(new GraduationExport.Col<GradCandidate>("warnings", "Points To Check", DC, F_warnings).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("holdWhy", "Reason Held", DC, F_holdWhy).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("holdWho", "Held By", DC, F_holdWho).Off());
        c.Add(new GraduationExport.Col<GradCandidate>("holdWhen", "Held On", DC, F_holdWhen).Off());
        return c;
    }

    // Named methods rather than lambdas, so the catalogue above reads as a table.
    private static string F_regno(GradCandidate g) { return g.regno; }
    private static string F_name(GradCandidate g) { return g.name; }
    private static string F_progcode(GradCandidate g) { return g.progcode; }
    private static string F_progname(GradCandidate g) { return g.progname; }
    private static string F_faculty(GradCandidate g) { return g.faculty; }
    private static string F_spec(GradCandidate g) { return g.specIsPlaceholder ? "" : g.specialisation; }
    private static string F_intake(GradCandidate g) { return g.entryyear; }
    private static string F_yr(GradCandidate g) { return g.maxStudyYear.ToString(CultureInfo.InvariantCulture); }
    private static string F_plen(GradCandidate g) { return g.progLength.ToString(CultureInfo.InvariantCulture); }
    private static string F_level(GradCandidate g) { return GraduationEngine.LevelName(g.levelCode); }
    private static string F_first(GradCandidate g) { return g.firstYear; }
    private static string F_last(GradCandidate g) { return g.lastYear; }
    private static string F_courses(GradCandidate g) { return g.coursesTaken.ToString(CultureInfo.InvariantCulture); }
    private static string F_cuE(GradCandidate g) { return Math.Round(g.cuEarned).ToString(CultureInfo.InvariantCulture); }
    private static string F_cuR(GradCandidate g)
    { return g.cuSource == "NONE" ? "" : Math.Round(g.cuRequired).ToString(CultureInfo.InvariantCulture); }
    private static string F_cuS(GradCandidate g)
    {
        if (g.cuSource == "NONE") return "";
        double d = g.cuRequired - g.cuEarned;
        return (d > 0 ? Math.Round(d) : 0).ToString(CultureInfo.InvariantCulture);
    }
    private static string F_cuSrc(GradCandidate g)
    {
        return g.cuSource == "STRUCTURE" ? "Programme structure"
             : g.cuSource == "DECLARED" ? "Declared minimum" : "Not assessable";
    }
    private static string F_cgpa(GradCandidate g)
    { return g.cgpa > 0 ? g.cgpa.ToString("F2", CultureInfo.InvariantCulture) : ""; }
    private static string F_class(GradCandidate g) { return g.degClass; }
    private static string F_fails(GradCandidate g) { return g.failedPapers.ToString(CultureInfo.InvariantCulture); }
    private static string F_zero(GradCandidate g) { return g.zeroMarks.ToString(CultureInfo.InvariantCulture); }
    private static string F_unmarked(GradCandidate g) { return g.missingScores.ToString(CultureInfo.InvariantCulture); }
    private static string F_unpub(GradCandidate g) { return g.UnpubTotal.ToString(CultureInfo.InvariantCulture); }
    private static string F_cover(GradCandidate g)
    { return g.coverageChecked ? g.coverageMissing.ToString(CultureInfo.InvariantCulture) : ""; }
    private static string F_ready(GradCandidate g)
    { return g.readiness == "READY" ? "Ready" : g.readiness == "WARN" ? "Needs a look" : "Blocked"; }
    private static string F_status(GradCandidate g)
    {
        return g.graduatedYear != "" ? ("On the " + g.graduatedYear + " list")
             : (g.holdReason != "" ? "Held" : "Under review");
    }
    private static string F_blockers(GradCandidate g) { return Findings(g, "BLOCK"); }
    private static string F_warnings(GradCandidate g) { return Findings(g, "WARN"); }
    private static string F_holdWhy(GradCandidate g) { return g.holdReason; }
    private static string F_holdWho(GradCandidate g) { return g.holdActor; }
    private static string F_holdWhen(GradCandidate g) { return g.holdAt; }

    /// <summary>
    /// The reasons, in one cell. A spreadsheet of blocked candidates is only useful if it says
    /// what to go and fix, and "Blocked" on its own says nothing.
    /// </summary>
    private static string Findings(GradCandidate g, string level)
    {
        var sb = new StringBuilder();
        foreach (GradFinding fd in g.findings)
        {
            if (fd.level != level) continue;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(fd.detail);
        }
        return sb.ToString();
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        string fmt = Request.Form["gradExport"];
        if (string.IsNullOrEmpty(fmt)) { Prime(); return; }

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return;

        GraduationEngine.GradFilter f = GraduationBootstrap.Parse(Request.Form["gradConfig"] ?? "");
        f.state = ListState(f);

        // "Only what is on screen" means exactly the page the user was looking at; anything
        // else means the whole result set. The previous build asked Page() for 5,000 rows and
        // received 50, because Page() clamps any size above 200 back down to its default. At
        // the module's own opening view that turned 996 candidates into a 50-row workbook whose
        // cover sheet reported 50 as though it were the answer.
        bool onePage = (Request.Form["gradRows"] ?? "all") == "page";
        List<GradCandidate> rows;
        int total;
        bool truncated = false;

        if (onePage)
        {
            int size;
            if (!int.TryParse(Request.Form["gradPageSize"] ?? "", out size) || size < 1 || size > 200) size = 50;
            f.size = size;
            rows = GraduationEngine.Page(scope, f, out total);
        }
        else
        {
            rows = GraduationEngine.All(scope, f, EXPORT_CAP, out total, out truncated);
        }

        // Readiness is decided in C#, not SQL, so it has to be applied to the rows themselves.
        int matched = rows.Count;
        rows = GraduationEngine.FilterByReadiness(rows, f.readiness);

        GraduationExport.Truncation = truncated
            ? ("The filters matched " + total.ToString(CultureInfo.InvariantCulture) +
               " candidates. This file carries the first " + matched.ToString(CultureInfo.InvariantCulture) +
               ", which is the export ceiling of " + EXPORT_CAP.ToString(CultureInfo.InvariantCulture) +
               " rows. Narrow by faculty, programme or intake to get the rest.")
            : null;

        List<GraduationExport.Col<GradCandidate>> cat = Catalogue();
        // Ordered and grouped before anything is written, so the PDF, the workbook and the CSV
        // are the same list in the same order rather than three different answers.
        Order(rows, f.orderBy);
        if (f.groupBy != "") GroupSort(rows, f.groupBy);

        GraduationExport.Sheet sheet = GraduationExport.Build(
            "Candidates",
            f.acadYear == "" ? "" : ("for " + f.acadYear),
            cat, rows, Request.Form["gradCols"]);

        var sheets = new List<GraduationExport.Sheet>();
        sheets.Add(sheet);

        string want = Request.Form["gradSheets"] ?? "";
        if (GraduationExport.Wants(want, "byprog")) sheets.Add(ByProgramme(rows));
        if (GraduationExport.Wants(want, "byready")) sheets.Add(ByReadiness(rows));
        if (GraduationExport.Wants(want, "blockers")) sheets.Add(ByBlocker(rows));

        var cover = GraduationBootstrap.CoverOf(f,
            f.state == "listed" ? "Already on the graduation list"
            : f.state == "all" ? "On the list and not yet on it"
            : (f.focus == "cycle" ? "The graduating cycle" : "Everyone not yet graduated"));
        cover.Add(new KeyValuePair<string, string>("Candidates in this file",
            rows.Count.ToString(CultureInfo.InvariantCulture)));
        cover.Add(new KeyValuePair<string, string>("Ordered by", OrderLabel(f.orderBy)));

        string file = GraduationExport.FileName("graduation-candidates", f.acadYear);
        try
        {
            if (fmt == "csv")
                GraduationExport.Csv(Response, file, "Graduation Candidates", scope.Label, cover,
                                     sheet.Columns, sheet.Rows);
            else if (fmt == "xls")
                GraduationExport.Workbook(Response, file, "Graduation Candidates", scope.Label, cover, sheets);
            else
            {
                GraduationExport.Sheet pdfSheet = GraduationExport.WithoutGroupColumns(sheet, f.groupBy);
                GraduationPdf.Send(Response, file, "Graduation Candidates", f.acadYear, scope.Label,
                                   cover, GraduationExport.PdfCols(pdfSheet),
                                   GraduationExport.ToTable(pdfSheet, GroupValues(rows, f.groupBy)),
                                   f.groupBy == "" ? "" : GraduationExport.GROUP_COL,
                                   true, GraduationExport.Truncation);
            }
        }
        finally { GraduationExport.Truncation = null; }
    }

    /// <summary>
    /// Which population the list is showing.
    ///
    /// This page used to force "pending" everywhere - on_list = 0 - so a student already on a
    /// graduation list could not be looked at here at all. That is right as a default, because
    /// the queue is work still to be done, but it is wrong as the only option: a Registrar
    /// checking what is already on the 2026/2027 list, or looking for somebody who should not
    /// be on it, had nowhere to do that.
    ///
    /// A whitelist rather than a pass-through, so nothing unexpected reaches the engine. The
    /// engine already understood all three; only this page was insisting.
    /// </summary>
    private static string ListState(GraduationEngine.GradFilter f)
    {
        string v = (f.state ?? "").Trim().ToLowerInvariant();
        return (v == "listed" || v == "all") ? v : "pending";
    }

    // ─────────────────────────────────────────────────────────────────
    //  Ordering and grouping for the file.
    //
    //  Done in C# on the rows already read, not in SQL. Readiness, class
    //  of award and the credit figures are all computed after the query,
    //  so "sort by performance" is not something the database can do.
    // ─────────────────────────────────────────────────────────────────
    private static void Order(List<GradCandidate> rows, string by)
    {
        Comparison<GradCandidate> cmp;
        switch ((by ?? "").ToLowerInvariant())
        {
            case "regno":
                cmp = delegate(GradCandidate a, GradCandidate b)
                { return string.Compare(a.regno, b.regno, StringComparison.OrdinalIgnoreCase); };
                break;
            case "cgpa":
                // Highest first — "sort by performance" means the best at the top.
                cmp = delegate(GradCandidate a, GradCandidate b)
                {
                    int d = b.cgpa.CompareTo(a.cgpa);
                    return d != 0 ? d : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                };
                break;
            case "class":
                cmp = delegate(GradCandidate a, GradCandidate b)
                {
                    int d = ClassRank(a.degClass).CompareTo(ClassRank(b.degClass));
                    if (d != 0) return d;
                    d = b.cgpa.CompareTo(a.cgpa);
                    return d != 0 ? d : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                };
                break;
            case "prog":
                cmp = delegate(GradCandidate a, GradCandidate b)
                {
                    int d = string.Compare(a.progname, b.progname, StringComparison.OrdinalIgnoreCase);
                    return d != 0 ? d : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                };
                break;
            default:    // by name
                cmp = delegate(GradCandidate a, GradCandidate b)
                {
                    int d = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                    return d != 0 ? d : string.Compare(a.regno, b.regno, StringComparison.OrdinalIgnoreCase);
                };
                break;
        }
        rows.Sort(cmp);
    }

    /// <summary>Best class first. Anything unrecognised sorts last rather than in the middle.</summary>
    private static int ClassRank(string c)
    {
        c = (c ?? "").ToLowerInvariant();
        if (c.Contains("first") || c.Contains("distinction")) return 0;
        if (c.Contains("upper")) return 1;
        if (c.Contains("lower")) return 2;
        if (c.Contains("credit")) return 3;
        if (c.Contains("pass")) return 4;
        return 9;
    }

    private static string OrderLabel(string by)
    {
        switch ((by ?? "").ToLowerInvariant())
        {
            case "regno": return "Student number";
            case "cgpa": return "CGPA, highest first";
            case "class": return "Class of award";
            case "prog": return "Programme, then name";
            default: return "Name";
        }
    }

    /// <summary>
    /// A stable sort that keeps the chosen order inside each group. List.Sort is unstable, so
    /// the group key is compared first and the already-sorted position second.
    /// </summary>
    private static void GroupSort(List<GradCandidate> rows, string groupBy)
    {
        var pos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++) if (!pos.ContainsKey(rows[i].regno)) pos[rows[i].regno] = i;
        bool fac = groupBy == "fac";
        rows.Sort(delegate(GradCandidate a, GradCandidate b)
        {
            string ka = fac ? a.faculty : (a.progname == "" ? a.progcode : a.progname);
            string kb = fac ? b.faculty : (b.progname == "" ? b.progcode : b.progname);
            int d = string.Compare(ka, kb, StringComparison.OrdinalIgnoreCase);
            if (d != 0) return d;
            int ia, ib;
            pos.TryGetValue(a.regno, out ia);
            pos.TryGetValue(b.regno, out ib);
            return ia.CompareTo(ib);
        });
    }

    /// <summary>The group heading for each row, in row order.</summary>
    private static List<string> GroupValues(List<GradCandidate> rows, string groupBy)
    {
        if (groupBy == "") return null;
        var l = new List<string>();
        foreach (GradCandidate g in rows)
            l.Add(groupBy == "fac"
                ? (g.faculty == "" ? "(no faculty recorded)" : g.faculty)
                : (g.progname == "" ? g.progcode : g.progname + "   (" + g.progcode + ")"));
        return l;
    }

    /// <summary>How the queue divides across programmes — the tab a Dean opens first.</summary>
    private static GraduationExport.Sheet ByProgramme(List<GradCandidate> rows)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "By programme";
        sh.Columns = new[] { "Programme Code", "Programme", "Faculty", "Candidates",
                             "Ready", "Needs a look", "Blocked" };
        for (int i = 3; i <= 6; i++) sh.NumericColumns.Add(i);

        var keys = new List<string>();
        var tally = new Dictionary<string, int[]>();
        var label = new Dictionary<string, string[]>();
        foreach (GradCandidate g in rows)
        {
            string k = g.progcode == "" ? "(none)" : g.progcode;
            if (!tally.ContainsKey(k))
            {
                tally[k] = new int[4];
                label[k] = new[] { g.progcode, g.progname, g.faculty };
                keys.Add(k);
            }
            tally[k][0]++;
            if (g.readiness == "READY") tally[k][1]++;
            else if (g.readiness == "WARN") tally[k][2]++;
            else tally[k][3]++;
        }
        keys.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string k in keys)
            sh.Rows.Add(new[] { label[k][0], label[k][1], label[k][2],
                tally[k][0].ToString(CultureInfo.InvariantCulture),
                tally[k][1].ToString(CultureInfo.InvariantCulture),
                tally[k][2].ToString(CultureInfo.InvariantCulture),
                tally[k][3].ToString(CultureInfo.InvariantCulture) });
        return sh;
    }

    private static GraduationExport.Sheet ByReadiness(List<GradCandidate> rows)
    {
        int ready = 0, warn = 0, blocked = 0, held = 0;
        foreach (GradCandidate g in rows)
        {
            if (g.holdReason != "") held++;
            if (g.readiness == "READY") ready++;
            else if (g.readiness == "WARN") warn++;
            else blocked++;
        }
        var sh = new GraduationExport.Sheet();
        sh.Name = "By readiness";
        sh.Columns = new[] { "Readiness", "Candidates", "Share" };
        sh.NumericColumns.Add(1);
        int n = rows.Count;
        sh.Rows.Add(Pc("Ready — nothing outstanding", ready, n));
        sh.Rows.Add(Pc("Needs a look", warn, n));
        sh.Rows.Add(Pc("Blocked", blocked, n));
        sh.Rows.Add(Pc("Of those, held by a reviewer", held, n));
        sh.Rows.Add(new[] { "Total", n.ToString(CultureInfo.InvariantCulture), "100%" });
        return sh;
    }

    private static string[] Pc(string label, int v, int n)
    {
        return new[] { label, v.ToString(CultureInfo.InvariantCulture),
            n > 0 ? (Math.Round(v * 100.0 / n)).ToString(CultureInfo.InvariantCulture) + "%" : "–" };
    }

    /// <summary>Which checks are stopping people, and how many each one stops.</summary>
    private static GraduationExport.Sheet ByBlocker(List<GradCandidate> rows)
    {
        var sh = new GraduationExport.Sheet();
        sh.Name = "Why they are blocked";
        sh.Columns = new[] { "Check", "Candidates Blocked" };
        sh.NumericColumns.Add(1);
        var tally = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (GradCandidate g in rows)
            foreach (GradFinding fd in g.findings)
            {
                if (fd.level != "BLOCK") continue;
                if (!tally.ContainsKey(fd.name)) { tally[fd.name] = 0; order.Add(fd.name); }
                tally[fd.name]++;
            }
        order.Sort(delegate(string a, string b) { return tally[b].CompareTo(tally[a]); });
        foreach (string k in order)
            sh.Rows.Add(new[] { k, tally[k].ToString(CultureInfo.InvariantCulture) });
        if (sh.Rows.Count == 0) sh.Rows.Add(new[] { "Nothing is blocking anyone in this selection", "0" });
        return sh;
    }

    /// <summary>
    /// Largest candidate set for which the readiness filter is resolved exactly rather than
    /// estimated. Measured on live data: reading and assessing 996 candidates costs about
    /// 0.75s, while the whole 14,547-strong backlog costs 13s — far too long to hold a dialog
    /// open for. Below the threshold the user gets the true number; above it, a plain warning.
    /// </summary>
    private const int EXACT_COUNT_LIMIT = 3000;

    /// <summary>
    /// How many rows an export would really contain, answered before the user commits to it.
    ///
    /// The SQL count is exact for every filter except readiness, which is decided per student
    /// in C# after the rows are read — so a "Blocked" export of the 2026/2027 cycle matches 996
    /// candidates in SQL and writes 297. Showing 996 and delivering 297 is the kind of small
    /// dishonesty that makes people stop trusting a screen, so where it is affordable the real
    /// figure is computed, and where it is not, the discrepancy is stated.
    /// </summary>
    /// <summary>
    /// Holds a selection under one shared reason.
    ///
    /// Mirrors ClearMany: one student at a time through GraduationService.Hold, so scope is
    /// re-checked per student and every hold lands in acad_grad_review as its own row. A batch
    /// decision must be exactly as accountable as an individual one — the only thing shared is
    /// the wording.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string HoldMany(string regnos, string acadYear, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        acadYear = (acadYear ?? "").Trim();
        if (acadYear == "") return J.Serialize(new { success = false, message = "Choose the graduation year first." });
        reason = (reason ?? "").Trim();
        if (reason.Length < 10)
            return J.Serialize(new { success = false, message = "A reason of at least 10 characters is needed." });

        var ids = new List<string>();
        foreach (string x in (regnos ?? "").Split(','))
        { string t = x.Trim(); if (t != "" && !ids.Contains(t)) ids.Add(t); }
        if (ids.Count == 0) return J.Serialize(new { success = false, message = "Nothing was selected." });
        if (ids.Count > 300) return J.Serialize(new { success = false, message = "Hold at most 300 at a time." });

        int done = 0;
        var skipped = new List<string>();
        foreach (string reg in ids)
        {
            string raw = GraduationService.Hold(scope, reg, acadYear, reason);
            bool ok = false;
            try
            {
                var d = J.Deserialize<Dictionary<string, object>>(raw);
                object v;
                ok = d.TryGetValue("success", out v) && v != null && Convert.ToBoolean(v);
                if (!ok)
                {
                    object m; d.TryGetValue("message", out m);
                    skipped.Add(reg + " \u2014 " + (m == null ? "refused" : m.ToString()));
                }
            }
            catch { skipped.Add(reg + " \u2014 could not be read"); }
            if (ok) done++;
        }

        return J.Serialize(new
        {
            success = true,
            held = done,
            skipped = skipped,
            message = done + (done == 1 ? " candidate was" : " candidates were") + " held" +
                      (skipped.Count > 0 ? "; " + skipped.Count + " skipped." : ".")
        });
    }

    /// <summary>Reasons worth offering for THIS candidate. See GraduationReasons.</summary>
    [WebMethod(EnableSession = true)]
    public static string HoldReasons(string regno) { return GraduationReasons.For(regno); }

    /// <summary>
    /// Finds any student in scope, candidate or not, so a reviewer can bring somebody into the
    /// queue the engine did not. See GraduationStudent.Search for why that is allowed.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string FindStudent(string q, string acadYear)
    { return GraduationStudent.Search(q, acadYear); }

    [WebMethod(EnableSession = true)]
    public static string CountExport(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            GraduationEngine.GradFilter f = GraduationBootstrap.Parse(configJson);
            f.state = ListState(f);
            f.page = 1;
            f.size = 1;
            int total;
            GraduationEngine.Page(scope, f, out total);

            string note = "";
            int shown = total;

            if (f.readiness != "")
            {
                if (total <= EXACT_COUNT_LIMIT)
                {
                    int t2; bool tr;
                    List<GradCandidate> rows = GraduationEngine.All(scope, f, EXACT_COUNT_LIMIT, out t2, out tr);
                    shown = GraduationEngine.FilterByReadiness(rows, f.readiness).Count;
                    if (shown != total)
                        note = "Of " + total.ToString(CultureInfo.InvariantCulture) + " candidates matching the other filters.";
                }
                else
                    note = "Readiness is assessed as each record is read, so the file will hold fewer than this.";
            }

            // 14,547 rows took thirteen seconds to assemble when measured. Someone who chooses
            // that should know before they wait, not while they are waiting.
            if (shown > 5000)
                note = (note == "" ? "" : note + " ") +
                       "A file this size takes roughly " +
                       Math.Max(1, (int)Math.Round(shown / 1100.0)).ToString(CultureInfo.InvariantCulture) +
                       " seconds to build.";

            return J.Serialize(new
            {
                success = true,
                total = shown,
                capped = shown > EXPORT_CAP ? EXPORT_CAP : 0,
                note = note
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetBootstrap() { return GraduationBootstrap.Bootstrap(true); }

    [WebMethod(EnableSession = true)]
    public static string GetCandidates(string configJson)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!scope.HasAccess) return GraduationBootstrap.Denied();
            GraduationEngine.GradFilter f = GraduationBootstrap.Parse(configJson);
            f.state = ListState(f);
            int total;
            List<GradCandidate> rows = GraduationEngine.Page(scope, f, out total);
            return J.Serialize(new
            {
                success = true, rows = rows, total = total, page = f.page, size = f.size,
                pages = (total + f.size - 1) / f.size
            });
        }
        catch (Exception ex) { return J.Serialize(new { success = false, message = ex.Message }); }
    }

    [WebMethod(EnableSession = true)]
    public static string GetStudent(string regno)
    { return GraduationStudent.Detail(regno); }

    [WebMethod(EnableSession = true)]
    public static string ClearStudent(string regno, string acadYear, string note, bool overrideBlock)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Clear(scope, regno, acadYear, note, overrideBlock);
    }

    [WebMethod(EnableSession = true)]
    public static string HoldStudent(string regno, string acadYear, string reason)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Hold(scope, regno, acadYear, reason);
    }

    [WebMethod(EnableSession = true)]
    public static string ReleaseStudent(string regno, string acadYear, string note)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        return GraduationService.Release(scope, regno, acadYear, note);
    }

    /// <summary>
    /// Clears several at once. Offered only for candidates whose worst finding is PASS, and the
    /// server enforces that rather than trusting the browser: each is re-assessed live, anything
    /// not READY is skipped and named back. A bulk action that can quietly clear a blocked
    /// student is worse than no bulk action.
    /// </summary>
    [WebMethod(EnableSession = true)]
    public static string ClearMany(string regnos, string acadYear)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return GraduationBootstrap.Denied();
        acadYear = (acadYear ?? "").Trim();
        if (acadYear == "") return J.Serialize(new { success = false, message = "Choose the graduation year first." });

        var ids = new List<string>();
        foreach (string x in (regnos ?? "").Split(','))
        { string t = x.Trim(); if (t != "" && !ids.Contains(t)) ids.Add(t); }
        if (ids.Count == 0) return J.Serialize(new { success = false, message = "Nothing was selected." });
        if (ids.Count > 300) return J.Serialize(new { success = false, message = "Clear at most 300 at a time." });

        int done = 0;
        var skipped = new List<string>();
        foreach (string reg in ids)
        {
            string raw = GraduationService.Clear(scope, reg, acadYear, "", false);
            bool ok = false;
            try
            {
                var d = J.Deserialize<Dictionary<string, object>>(raw);
                object v;
                ok = d.TryGetValue("success", out v) && v != null && Convert.ToBoolean(v);
                if (!ok)
                {
                    object m; d.TryGetValue("message", out m);
                    skipped.Add(reg + " — " + (m == null ? "refused" : m.ToString()));
                }
            }
            catch { skipped.Add(reg + " — could not be read"); }
            if (ok) done++;
        }

        return J.Serialize(new
        {
            success = true,
            cleared = done,
            skipped = skipped,
            message = done + (done == 1 ? " candidate" : " candidates") + " added to the " + acadYear +
                      " graduation list" + (skipped.Count > 0 ? "; " + skipped.Count + " skipped." : ".")
        });
    }
}
