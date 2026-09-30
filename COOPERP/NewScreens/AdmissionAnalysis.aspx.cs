using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;
using System.Web.Services;
using System.Web.Script.Services;
using MySql.Data.MySqlClient;

/// <summary>
/// Admission Analysis — read-only aggregate analytics over admissions
/// (acad_applicant_choices primary choice ∩ acad_applications). Groups the
/// applicant pipeline (pending / admitted / registered / rejected / withdrawn)
/// by programme, faculty, entry year, session and source, honouring GET filters.
/// Server-rendered payload (window.__AA_INIT) — no AJAX on load.
/// </summary>
public partial class COOPERP_NewScreens_AdmissionAnalysis : System.Web.UI.Page
{
    public string InitJson = "{\"ok\":false,\"error\":\"not loaded\"}";

    private string ConnStr { get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; } }
    private MySqlConnection Open() { var c = new MySqlConnection(ConnStr); c.Open(); return c; }
    private static long L(object v) { if (v == null || v == DBNull.Value) return 0; long r; return long.TryParse(Convert.ToString(v), out r) ? r : 0; }
    private static string S(object v) { return (v == null || v == DBNull.Value) ? "" : Convert.ToString(v); }
    private static MySqlParameter P(string n, object v) { return new MySqlParameter(n, v ?? DBNull.Value); }

    private static bool ColumnExists(MySqlConnection conn, string table, string col)
    {
        using (var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
        {
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", col);
            object o = cmd.ExecuteScalar();
            return o != null && Convert.ToInt64(o) > 0;
        }
    }

    // ── STATUS BUCKETS ────────────────────────────────────────────────────
    //
    // One CASE, one bucket per applicant. Written this way on purpose: the previous version
    // counted each status with its own SUM(), which let a row fall into two buckets or none,
    // and both happened. Across the whole table the columns summed to 6,371 against a total of
    // 6,385 — 14 applicants with an adm_status the page did not know about (8 and 9) were in
    // the total and in nothing else. With a single CASE the parts always add to the whole.
    //
    // "Registered" now means the applicant ACTUALLY ENROLLED for a semester. It used to mean
    // "stud_reg_no is filled in" — but that column holds the entry number (26/U/BSAF/0004/M/DAY)
    // which is issued AT ADMISSION, so 6,121 of 6,123 admitted applicants had one and the page
    // reported 6,150 registered. The true figure is 4,476. The difference, 1,698, is the number
    // admitted who never took up the place — which is the single most useful number here and
    // was invisible.
    //
    // Withdrawal is recorded on the application, not the choice: adm_status=3 has no rows at
    // all, while ten applicants carry app_status='WITHDRAWN' and were being shown as pending.
    private const string BucketCase =
        "CASE " +
        "  WHEN a.app_status='WITHDRAWN' OR c.adm_status=3 THEN 'withdrawn' " +
        "  WHEN a.app_status='REJECTED'  OR c.adm_status=2 THEN 'rejected' " +
        "  WHEN er.rg IS NOT NULL                          THEN 'registered' " +
        "  WHEN c.adm_status=1                             THEN 'admitted' " +
        "  WHEN c.adm_status=0                             THEN 'pending' " +
        "  ELSE 'other' END ";

    private const string StatCols =
        "COUNT(*) total, " +
        "SUM(" + BucketCase + "='pending') pending, " +
        "SUM(" + BucketCase + "='admitted') admitted, " +
        "SUM(" + BucketCase + "='registered') registered, " +
        "SUM(" + BucketCase + "='rejected') rejected, " +
        "SUM(" + BucketCase + "='withdrawn') withdrawn, " +
        "SUM(" + BucketCase + "='other') other ";

    // The enrolled set is joined once as a derived table rather than tested per row with a
    // correlated EXISTS — the correlated form did not return inside two minutes.
    private const string BaseJoin =
        " FROM acad_applicant_choices c " +
        "JOIN acad_applications a ON a.stud_entry_no=c.stud_entry_no " +
        "LEFT JOIN acad_programme p ON p.progcode=c.prog_id " +
        "LEFT JOIN acad_faculty f ON f.faculty_code=p.faculty_code " +
        "LEFT JOIN (SELECT DISTINCT TRIM(regno) rg FROM acad_registration " +
        "            WHERE regstatus IN ('REGISTERED','LATE REGISTERED','CLEARED')) er " +
        "       ON er.rg = TRIM(a.stud_entry_no) " +
        "LEFT JOIN acad_campuses cm ON cm.ID = CAST(NULLIF(TRIM(IFNULL(a.stud_campus,'')),'') AS UNSIGNED) ";

    protected void Page_Load(object sender, EventArgs e)
    {
        string year    = (Request.QueryString["year"] ?? "").Trim();
        string session = (Request.QueryString["session"] ?? "").Trim();
        string source  = (Request.QueryString["source"] ?? "").Trim();
        string fac     = (Request.QueryString["fac"] ?? "").Trim();
        string prog    = (Request.QueryString["prog"] ?? "").Trim();
        string campus  = (Request.QueryString["campus"] ?? "").Trim();

        var ser = new JavaScriptSerializer(); ser.MaxJsonLength = int.MaxValue;
        try { InitJson = ser.Serialize(BuildData(year, session, source, fac, prog, campus)); }
        catch (Exception ex) { InitJson = ser.Serialize(new { ok = false, error = "Server error: " + ex.Message }); }
    }

    private List<Dictionary<string, object>> Query(MySqlConnection conn, string sql, List<MySqlParameter> ps)
    {
        var rows = new List<Dictionary<string, object>>();
        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.CommandTimeout = 60;
            if (ps != null) foreach (var p in ps) cmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var d = new Dictionary<string, object>();
                    for (int i = 0; i < r.FieldCount; i++) d[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                    rows.Add(d);
                }
        }
        return rows;
    }

    private static object Stat(Dictionary<string, object> d)
    {
        long total = L(d["total"]), pending = L(d["pending"]), admitted = L(d["admitted"]),
             registered = L(d["registered"]), rejected = L(d["rejected"]), withdrawn = L(d["withdrawn"]),
             other = d.ContainsKey("other") ? L(d["other"]) : 0;
        return new { total, pending, admitted, registered, rejected, withdrawn, other };
    }

    private object BuildData(string year, string session, string source, string fac, string prog, string campus)
    {
        using (var conn = Open())
        {
            bool hasUserId = ColumnExists(conn, "acad_applications", "applicant_user_id");

            var where = new List<string> { "c.Choice=1" };
            var ps = new List<MySqlParameter>();
            if (year != "") { where.Add("a.stud_entry_year=@year"); ps.Add(P("@year", year)); }
            if (session != "") { where.Add("c.adm_session=@sess"); ps.Add(P("@sess", session)); }
            if (fac != "") { where.Add("p.faculty_code=@fac"); ps.Add(P("@fac", fac)); }
            if (prog != "") { where.Add("c.prog_id=@prog"); ps.Add(P("@prog", prog)); }
            // stud_campus is free text — '01', '02', '1', '2', '00' and NULL all occur — so it is
            // compared as a number, not a string, or '01' and '1' would be two different campuses.
            if (campus != "") { where.Add("CAST(NULLIF(TRIM(IFNULL(a.stud_campus,'')),'') AS UNSIGNED)=@campus"); ps.Add(P("@campus", campus)); }
            if (hasUserId && source == "ONLINE") where.Add("a.applicant_user_id IS NOT NULL");
            else if (hasUserId && source == "WALKIN") where.Add("a.applicant_user_id IS NULL");
            string wc = " WHERE " + string.Join(" AND ", where.ToArray());

            // ── KPIs (single row) ──
            var kpiRows = Query(conn, "SELECT " + StatCols + BaseJoin + wc, ps);
            object kpis = kpiRows.Count > 0 ? Stat(kpiRows[0]) : new { total = 0, pending = 0, admitted = 0, registered = 0, rejected = 0, withdrawn = 0 };

            // ── By programme (the core: programme totals) ──
            var byProg = new List<object>();
            foreach (var d in Query(conn, "SELECT c.prog_id code, COALESCE(p.progname,c.prog_id) name, COALESCE(p.faculty_code,'') facCode, COALESCE(f.faculty_name,'') facName, " +
                "COUNT(DISTINCT NULLIF(TRIM(c.adm_session),'')) sessions, " + StatCols + BaseJoin + wc + " GROUP BY c.prog_id ORDER BY total DESC", ps))
                byProg.Add(new { code = S(d["code"]), name = S(d["name"]), facCode = S(d["facCode"]), facName = S(d["facName"]), sessions = L(d["sessions"]), stat = Stat(d) });

            // ── By faculty ──
            var byFaculty = new List<object>();
            foreach (var d in Query(conn, "SELECT COALESCE(p.faculty_code,'') code, COALESCE(f.faculty_name,'(Unassigned)') name, " +
                "COUNT(DISTINCT NULLIF(TRIM(c.adm_session),'')) sessions, " + StatCols + BaseJoin + wc + " GROUP BY p.faculty_code ORDER BY total DESC", ps))
                byFaculty.Add(new { code = S(d["code"]), name = S(d["name"]), sessions = L(d["sessions"]), stat = Stat(d) });

            // ── By campus ──
            var byCampus = new List<object>();
            foreach (var d in Query(conn, "SELECT COALESCE(cm.campus_name, CONCAT('(campus ', IFNULL(NULLIF(TRIM(a.stud_campus),''),'not recorded'), ')')) name, " +
                "COUNT(DISTINCT NULLIF(TRIM(c.adm_session),'')) sessions, " + StatCols + BaseJoin + wc +
                " GROUP BY name ORDER BY total DESC", ps))
                byCampus.Add(new { name = S(d["name"]), sessions = L(d["sessions"]), stat = Stat(d) });

            // ── By entry year ──
            var byYear = new List<object>();
            foreach (var d in Query(conn, "SELECT COALESCE(a.stud_entry_year,0) yr, " +
                "COUNT(DISTINCT NULLIF(TRIM(c.adm_session),'')) sessions, " + StatCols + BaseJoin + wc + " GROUP BY a.stud_entry_year ORDER BY yr DESC", ps))
                byYear.Add(new { name = S(d["yr"]), sessions = L(d["sessions"]), stat = Stat(d) });

            // ── By intake cohort: entry year AND session together ──
            // The Admissions Office reports by INTAKE, and an intake is a year and a session
            // together — "2026 Weekend" is a cohort with its own admitted, enrolled and
            // retention figures. By Year and By Session each collapse one half of that away, so
            // neither of them could answer the question actually being asked, and the two could
            // not be combined afterwards either: 2026 Weekend cannot be derived from a 2026 row
            // and a Weekend row. It has to be grouped once, on both.
            var byCohort = new List<object>();
            foreach (var d in Query(conn, "SELECT COALESCE(a.stud_entry_year,'') yr, " +
                "COALESCE(NULLIF(TRIM(c.adm_session),''),'(none)') sess, " + StatCols + BaseJoin + wc +
                " GROUP BY a.stud_entry_year, sess ORDER BY yr DESC, sess", ps))
                byCohort.Add(new
                {
                    name = S(d["yr"]) + " \u00b7 " + S(d["sess"]),
                    yr   = S(d["yr"]),
                    sess = S(d["sess"]),
                    stat = Stat(d)
                });

            // ── By session ──
            var bySession = new List<object>();
            foreach (var d in Query(conn, "SELECT COALESCE(NULLIF(c.adm_session,''),'(None)') sess, " + StatCols + BaseJoin + wc + " GROUP BY c.adm_session ORDER BY total DESC", ps))
                bySession.Add(new { name = S(d["sess"]), stat = Stat(d) });

            // ── By source (online vs walk-in) ──
            var bySource = new List<object>();
            if (hasUserId)
                foreach (var d in Query(conn, "SELECT CASE WHEN a.applicant_user_id IS NOT NULL THEN 'Online Portal' ELSE 'Walk-in / Manual' END src, " + StatCols + BaseJoin + wc + " GROUP BY (a.applicant_user_id IS NOT NULL) ORDER BY total DESC", ps))
                    bySource.Add(new { name = S(d["src"]), stat = Stat(d) });

            // ── Filter option lists ──
            var faculties = new List<object>();
            foreach (var d in Query(conn, "SELECT faculty_code, faculty_name FROM acad_faculty ORDER BY faculty_name", null))
                faculties.Add(new { code = S(d["faculty_code"]), name = S(d["faculty_name"]) });
            var programmes = new List<object>();
            foreach (var d in Query(conn, "SELECT progcode, progname FROM acad_programme ORDER BY progname", null))
                programmes.Add(new { code = S(d["progcode"]), name = S(d["progname"]) });
            var years = new List<object>();
            foreach (var d in Query(conn, "SELECT DISTINCT stud_entry_year yr FROM acad_applications WHERE stud_entry_year IS NOT NULL AND stud_entry_year<>'' ORDER BY yr DESC", null))
                years.Add(S(d["yr"]));
            var sessions = new List<object>();
            foreach (var d in Query(conn, "SELECT DISTINCT adm_session s FROM acad_applicant_choices WHERE adm_session IS NOT NULL AND adm_session<>'' ORDER BY s", null))
                sessions.Add(S(d["s"]));
            var campuses = new List<object>();
            foreach (var d in Query(conn, "SELECT ID, campus_name FROM acad_campuses ORDER BY ID", null))
                campuses.Add(new { code = S(d["ID"]), name = S(d["campus_name"]) });

            // Applications that carry no primary choice are invisible to every figure above,
            // because the whole analysis hangs off acad_applicant_choices.Choice=1. Counting
            // them here means the page can say so instead of quietly under-reporting.
            long notAnalysed = 0;
            var na = Query(conn, "SELECT COUNT(*) n FROM acad_applications a " +
                "WHERE NOT EXISTS (SELECT 1 FROM acad_applicant_choices c WHERE c.stud_entry_no=a.stud_entry_no AND c.choice=1)", null);
            if (na.Count > 0) notAnalysed = L(na[0]["n"]);

            return new {
                ok = true,
                filters = new { year, session, source, fac, prog, campus, hasSource = hasUserId },
                kpis = kpis,
                byProg = byProg,
                byFaculty = byFaculty,
                byCampus = byCampus,
                byYear = byYear,
                byCohort = byCohort,
                bySession = bySession,
                bySource = bySource,
                notAnalysed = notAnalysed,
                lists = new { faculties, programmes, years, sessions, campuses }
            };
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RECONCILIATION — every ACTIVE student should have an admission record.
    //  Canonical link: acad_applications.stud_entry_no == acad_student.regno.
    //  Back-fills acad_applications + primary acad_applicant_choices for active
    //  students in [minYear,maxYear] that have none. Idempotent. Long reg-nos
    //  (> char(15)) can't fit the PK and are reported as flagged, not touched.
    // ════════════════════════════════════════════════════════════════════════
    private static string ConnStrS { get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; } }
    private static MySqlConnection OpenS() { var c = new MySqlConnection(ConnStrS); c.Open(); return c; }

    private class Orphan { public string regno, entryno, name, progid, session, intake, status; public int year, campus; }

    private static List<Orphan> OrphanRows(MySqlConnection conn, int minYear, int maxYear)
    {
        var list = new List<Orphan>();
        string sql =
            "SELECT s.regno, IFNULL(s.entryno,'') entryno, TRIM(CONCAT(IFNULL(s.firstname,''),' ',IFNULL(s.othername,''))) name, " +
            "IFNULL(s.progid,'') progid, IFNULL(s.entryyear,0) yr, IFNULL(s.studsesion,'') sess, IFNULL(s.intake,'') intake, " +
            "IFNULL(s.studCampus,1) campus, IFNULL(s.new_status,'') nstatus " +
            "FROM acad_student s " +
            "WHERE s.stud_status='ACTIVE' AND s.entryyear BETWEEN @min AND @max " +
            "AND NOT EXISTS(SELECT 1 FROM acad_applications a WHERE a.stud_entry_no=s.regno OR a.stud_reg_no=s.entryno OR a.stud_entry_no=s.entryno) " +
            "ORDER BY s.entryyear DESC, s.regno";
        using (var cmd = new MySqlCommand(sql, conn))
        {
            cmd.CommandTimeout = 120;
            cmd.Parameters.AddWithValue("@min", minYear);
            cmd.Parameters.AddWithValue("@max", maxYear);
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    list.Add(new Orphan {
                        regno = S(r["regno"]).Trim(), entryno = S(r["entryno"]).Trim(), name = S(r["name"]).Trim(),
                        progid = S(r["progid"]).Trim(), year = (int)L(r["yr"]), session = S(r["sess"]).Trim(),
                        intake = S(r["intake"]).Trim(), campus = (int)L(r["campus"]), status = S(r["nstatus"]).Trim()
                    });
        }
        return list;
    }

    private static string MapSession(string s)
    {
        s = (s ?? "").Trim().ToUpperInvariant();
        if (s == "DAY" || s == "WEEKEND" || s == "INSERVICE" || s == "EVENING") return s;
        if (s.Contains("WK") || s.Contains("WEEK")) return "WEEKEND";
        if (s.Contains("INSRV") || s.Contains("SERVICE")) return "INSERVICE";
        if (s.Contains("EVEN")) return "EVENING";
        return "DAY";
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public static object ReconcilePreview(int minYear, int maxYear)
    {
        try
        {
            using (var conn = OpenS())
            {
                var orphans = OrphanRows(conn, minYear, maxYear);
                int backfill = 0; var byYear = new Dictionary<int, int>();
                var flagged = new List<object>();
                foreach (var o in orphans)
                {
                    if (o.regno.Length <= 15) { backfill++; if (!byYear.ContainsKey(o.year)) byYear[o.year] = 0; byYear[o.year]++; }
                    else flagged.Add(new { o.regno, o.entryno, o.name, o.progid, o.year, o.status });
                }
                var yrs = new List<object>();
                foreach (var kv in byYear) yrs.Add(new { year = kv.Key, count = kv.Value });
                return new { ok = true, minYear, maxYear, orphans = orphans.Count, backfillable = backfill, flagged = flagged.Count, byYear = yrs, flaggedList = flagged };
            }
        }
        catch (Exception ex) { return new { ok = false, error = ex.Message }; }
    }

    [WebMethod]
    [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
    public static object ReconcileRun(int minYear, int maxYear)
    {
        try
        {
            using (var conn = OpenS())
            {
                bool hasIntake = ColumnExists(conn, "acad_applications", "stud_intake");
                bool hasCampus = ColumnExists(conn, "acad_applications", "stud_campus");
                bool hasSubmitted = ColumnExists(conn, "acad_applications", "app_submitted_at");
                bool hasUpdated = ColumnExists(conn, "acad_applications", "app_last_updated_at");

                var orphans = OrphanRows(conn, minYear, maxYear);
                int inserted = 0; var flagged = new List<object>();

                using (var tx = conn.BeginTransaction())
                {
                    foreach (var o in orphans)
                    {
                        if (o.regno.Length > 15) { flagged.Add(new { o.regno, o.entryno, o.name, o.year }); continue; }

                        var cols = new List<string> { "stud_entry_no", "stud_reg_no", "stud_name", "stud_entry_year", "app_status" };
                        var vals = new List<string> { "@e", "@r", "@n", "@y", "'ADMITTED'" };
                        if (hasIntake) { cols.Add("stud_intake"); vals.Add("@intake"); }
                        if (hasCampus) { cols.Add("stud_campus"); vals.Add("@campus"); }
                        if (hasSubmitted) { cols.Add("app_submitted_at"); vals.Add("NOW()"); }
                        if (hasUpdated) { cols.Add("app_last_updated_at"); vals.Add("NOW()"); }

                        string appSql = "INSERT INTO acad_applications (" + string.Join(",", cols.ToArray()) + ") VALUES (" + string.Join(",", vals.ToArray()) + ")";
                        using (var cmd = new MySqlCommand(appSql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@e", o.regno);
                            cmd.Parameters.AddWithValue("@r", string.IsNullOrEmpty(o.entryno) ? "-" : o.entryno);
                            cmd.Parameters.AddWithValue("@n", o.name);
                            cmd.Parameters.AddWithValue("@y", o.year.ToString());
                            if (hasIntake) cmd.Parameters.AddWithValue("@intake", string.IsNullOrEmpty(o.intake) ? "AUGUST" : o.intake);
                            if (hasCampus) cmd.Parameters.AddWithValue("@campus", o.campus);
                            cmd.ExecuteNonQuery();
                        }

                        // primary choice (guarded against a stray existing choice)
                        string chSql =
                            "INSERT INTO acad_applicant_choices (stud_entry_no, choice, prog_id, adm_status, adm_session, sub_comb, choice_reg_no, adm_comments) " +
                            "SELECT @e,1,@p,1,@sess,13,@cr,'Back-filled by admission reconciliation' FROM DUAL " +
                            "WHERE NOT EXISTS(SELECT 1 FROM acad_applicant_choices WHERE stud_entry_no=@e AND choice=1)";
                        using (var cmd = new MySqlCommand(chSql, conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@e", o.regno);
                            cmd.Parameters.AddWithValue("@p", o.progid);
                            cmd.Parameters.AddWithValue("@sess", MapSession(o.session));
                            cmd.Parameters.AddWithValue("@cr", string.IsNullOrEmpty(o.entryno) ? o.regno : o.entryno);
                            cmd.ExecuteNonQuery();
                        }
                        inserted++;
                    }
                    tx.Commit();
                }
                return new { ok = true, inserted, flaggedSkipped = flagged.Count, flaggedList = flagged };
            }
        }
        catch (Exception ex) { return new { ok = false, error = ex.Message }; }
    }
}
