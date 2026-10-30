using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre, the per-student summary, and keeping it honest.
//
//  Reading acad_results on every page load meant aggregating 639,185
//  rows to draw five numbers: 4.7s for the overview alone, 8.7s for a
//  first paint. acad_grad_stats holds the answer per student, so the
//  same screens read ~20,800 indexed rows instead.
//
//  The bargain is explicit and one-sided in the right direction:
//    * LISTS AND COUNTS read the summary, and the page says how old it
//      is with a Refresh beside it;
//    * DECISIONS - the evidence panel, clear, hold, release - recompute
//      the one student live from acad_results, which costs under a
//      millisecond. No graduation decision is ever taken on a cached
//      number.
// =====================================================================
public static class GraduationStats
{
    private static string Conn()
    { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }

    /// <summary>The table name, so a typo can only happen in one place.</summary>
    public const string TABLE = "acad_grad_stats";

    /// <summary>
    /// The graduation records as they stood before acad_graduands was emptied at 12:10 on
    /// 25 September 2026, loaded read-only from COOPERP/backups. Nothing writes to it.
    /// </summary>
    public const string PRIOR_TABLE = "acad_graduands_prior";

    private static readonly Dictionary<string, bool> _tables = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private static readonly object _tablesLock = new object();

    /// <summary>Does a table exist in the current schema? Asked once per table per app domain.</summary>
    public static bool TableExists(MySqlConnection c, string table)
    {
        lock (_tablesLock) { bool known; if (_tables.TryGetValue(table, out known)) return known; }
        bool ok = false;
        try
        {
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t", c))
            {
                cmd.Parameters.AddWithValue("@t", table);
                ok = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }
        catch { return false; }   // unknown is not cached: ask again next time
        lock (_tablesLock) { _tables[table] = ok; }
        return ok;
    }

    public static bool PriorTableExists(MySqlConnection c) { return TableExists(c, PRIOR_TABLE); }

    /// <summary>
    /// The columns added on 31 Oct 2026, in the order they were added. EnsureSchema adds any that
    /// are missing, so code deployed before the table was altered, or a table restored from an
    /// older backup, cannot take the module down with "unknown column".
    /// </summary>
    private static readonly string[][] COLUMNS =
    {
        new[] { "first_sy",  "INT NOT NULL DEFAULT 0 COMMENT 'first study year with results'" },
        new[] { "term_mask", "INT NOT NULL DEFAULT 0 COMMENT 'bit (studyyear-1)*3+(semester-1) per term with results'" },
        new[] { "pend_rt",   "INT NOT NULL DEFAULT 0 COMMENT 'retakes registered, result blanked, not yet marked'" },
        new[] { "dup_n",     "INT NOT NULL DEFAULT 0 COMMENT 'courses on record twice: same code bar spaces, same title'" },
        new[] { "dup_cu",    "DOUBLE NOT NULL DEFAULT 0 COMMENT 'credits counted twice by those duplicates'" },
        new[] { "dup_fail",  "INT NOT NULL DEFAULT 0 COMMENT 'fails superseded by, or duplicating, the other copy'" },
        new[] { "cap_mask",  "INT NOT NULL DEFAULT 0 COMMENT 'passed final-year components: 1 research, 2 school practice, 4 internship'" },
    };
    private static bool _schemaOk = false;

    public static void EnsureSchema(MySqlConnection c)
    {
        if (_schemaOk) return;
        try
        {
            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new MySqlCommand(
                "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@t", c))
            {
                cmd.Parameters.AddWithValue("@t", TABLE);
                using (var r = cmd.ExecuteReader()) while (r.Read()) have.Add(r[0].ToString());
            }
            if (have.Count == 0) return;   // no table at all: nothing to add to
            var add = new List<string>();
            foreach (string[] col in COLUMNS)
                if (!have.Contains(col[0])) add.Add("ADD COLUMN " + col[0] + " " + col[1]);
            if (add.Count > 0)
                using (var cmd = new MySqlCommand("ALTER TABLE " + TABLE + " " + string.Join(", ", add.ToArray()), c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }
            _schemaOk = true;
        }
        catch { /* the next caller tries again; a query that needs a column will say which */ }
    }

    /// <summary>When the summary was last rebuilt, or DateTime.MinValue if never.</summary>
    public static DateTime RefreshedAt()
    {
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                using (var cmd = new MySqlCommand("SELECT MAX(refreshed_at) FROM " + TABLE, c))
                {
                    object o = cmd.ExecuteScalar();
                    if (o != null && o != DBNull.Value) return Convert.ToDateTime(o);
                }
            }
        }
        catch { }
        return DateTime.MinValue;
    }

    /// <summary>Is there anything in it at all? Governs the fallback to reading live.</summary>
    public static bool IsPopulated()
    {
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                using (var cmd = new MySqlCommand("SELECT 1 FROM " + TABLE + " LIMIT 1", c))
                    return cmd.ExecuteScalar() != null;
            }
        }
        catch { }
        return false;
    }

    public static string Freshness()
    {
        DateTime t = RefreshedAt();
        if (t == DateTime.MinValue) return "never built";
        TimeSpan age = DateTime.Now - t;
        if (age.TotalMinutes < 2) return "just now";
        if (age.TotalMinutes < 60) return (int)age.TotalMinutes + " min ago";
        if (age.TotalHours < 24) return (int)age.TotalHours + (age.TotalHours < 2 ? " hour ago" : " hours ago");
        return t.ToString("d MMM yyyy HH:mm");
    }

    /// <summary>
    /// Rebuilds the whole summary. One aggregate pass over acad_results grouped by student:
    /// about five seconds, paid once rather than on every page load.
    ///
    /// Written into a staging table and swapped in by rename, so readers never see a truncated
    /// or half-built table. RENAME TABLE is atomic and blocks only for its own duration.
    /// </summary>
    public static string Rebuild()
    {
        var started = DateTime.Now;
        int rows = 0;
        try
        {
            using (var c = new MySqlConnection(Conn()))
            {
                c.Open();
                // The new table is cloned from the live one, so it must carry every column first.
                _schemaOk = false;
                EnsureSchema(c);

                // The connection is used for DDL, so no ambient transaction is assumed - DDL
                // commits implicitly in MySQL and a transaction here would be a lie.
                using (var cmd = new MySqlCommand("DROP TABLE IF EXISTS acad_grad_stats_new", c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }

                using (var cmd = new MySqlCommand(
                    "CREATE TABLE acad_grad_stats_new LIKE " + TABLE, c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }

                using (var cmd = new MySqlCommand(BuildSql("acad_grad_stats_new", PriorTableExists(c)), c))
                { cmd.CommandTimeout = 600; rows = cmd.ExecuteNonQuery(); }

                using (var cmd = new MySqlCommand(
                    "DROP TABLE IF EXISTS acad_grad_stats_old", c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }

                using (var cmd = new MySqlCommand(
                    "RENAME TABLE " + TABLE + " TO acad_grad_stats_old, " +
                    "acad_grad_stats_new TO " + TABLE, c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }

                using (var cmd = new MySqlCommand("DROP TABLE IF EXISTS acad_grad_stats_old", c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }
            }
        }
        catch (Exception ex)
        {
            return new System.Web.Script.Serialization.JavaScriptSerializer()
                .Serialize(new { success = false, message = "Rebuild failed: " + ex.Message });
        }

        double secs = Math.Round((DateTime.Now - started).TotalSeconds, 1);
        return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new
        {
            success = true,
            rows = rows,
            seconds = secs,
            freshness = Freshness(),
            message = "Rebuilt " + rows + " student records in " + secs + "s."
        });
    }

    /// <summary>
    /// The one aggregate pass. Year comparisons use the believability rule rather than the bare
    /// four-digit pattern: acad_results holds 0/1, 2022/2024, 20222/2023, 2023/204 and 2202/2203
    /// across eleven students, and on a character column the worst of them sorts above every
    /// real year.
    /// </summary>
    /// <summary>
    /// "Already graduated", as the queues mean it: on the live list, or on the records kept from
    /// before 25 Sep 2026 for the programme the student is on now. An earlier award in another
    /// programme (PGD, then MBA) leaves the student a candidate for this one.
    /// </summary>
    public static string OnListSql(string student, bool withPrior)
    {
        return "IF(EXISTS(SELECT 1 FROM acad_graduands g WHERE g.regno=" + student + ".regno)" +
               (withPrior
                   ? " OR EXISTS(SELECT 1 FROM " + PRIOR_TABLE + " pr WHERE pr.regno=" + student + ".regno " +
                     " AND TRIM(pr.progcode)=TRIM(" + student + ".progid))"
                   : "") + ",1,0)";
    }

    /// <summary>
    /// Courses held twice under two spellings of one code with the same title ("BCU 2203" and
    /// "BCU2203", Curriculum Evaluation), one row per student. Measured on the candidates: 422
    /// pairs differ only by a space, but 412 of them are different courses ("BECD 3202" is Infant
    /// and Toddler Care, "BECD3202" Administration and Management), so the title must match too.
    /// That leaves 10: 26 credits counted twice, and one fail standing against a pass.
    /// </summary>
    public static string DuplicatesSql(string where)
    {
        return
            "SELECT a.regno, COUNT(*) n, " +
            " SUM(IF(a.score>=50 AND b.score>=50, LEAST(IFNULL(a.CreditUnits,0),IFNULL(b.CreditUnits,0)),0)) cu, " +
            " SUM(LEAST(IFNULL(a.score,0),IFNULL(b.score,0)) BETWEEN 1 AND 49) fl, " +
            " GROUP_CONCAT(CONCAT(a.courseid,' and ',b.courseid,' (',TRIM(ca.courseName),'), marked ', " +
            "   IFNULL(a.score,'-'),' and ',IFNULL(b.score,'-')) SEPARATOR '; ') descr " +
            "FROM acad_results a " +
            "JOIN acad_results b ON b.regno=a.regno AND b.courseid=REPLACE(REPLACE(a.courseid,' ',''),CHAR(9),'') " +
            " AND b.courseid<>a.courseid " +
            "JOIN acad_course ca ON ca.courseID=a.courseid " +
            "JOIN acad_course cb ON cb.courseID=b.courseid " +
            "WHERE (a.courseid LIKE '% %' OR a.courseid LIKE CONCAT('%',CHAR(9),'%')) " +
            " AND UPPER(TRIM(ca.courseName))=UPPER(TRIM(cb.courseName)) " + where +
            " GROUP BY a.regno";
    }

    private static string BuildSql(string target, bool withPrior)
    {
        const string YEAR_OK =
            "r.acad REGEXP '^[0-9]{4}/[0-9]{4}$' " +
            "AND CAST(LEFT(r.acad,4) AS UNSIGNED) BETWEEN 2000 AND 2035 " +
            "AND CAST(RIGHT(r.acad,4) AS UNSIGNED)=CAST(LEFT(r.acad,4) AS UNSIGNED)+1";

        return
            "INSERT INTO " + target + " (regno, progcode, plen, maxsy, courses, cu_earned, fails, " +
            " zero_marks, no_score, gp_num, gp_den, cgpa, first_year, last_year, is_candidate, " +
            " on_list, refreshed_at, first_sy, term_mask, pend_rt, dup_n, dup_cu, dup_fail, cap_mask) " +
            "SELECT s.regno, TRIM(s.progid), IFNULL(NULLIF(p.couselength,0),3), " +
            " IFNULL(MAX(r.studyyear),0), COUNT(r.courseid), " +
            " IFNULL(SUM(CASE WHEN r.score>=50 THEN IFNULL(r.CreditUnits,0) ELSE 0 END),0), " +
            " IFNULL(SUM(r.score>0 AND r.score<50),0), IFNULL(SUM(r.score=0),0), " +
            " IFNULL(SUM(r.score IS NULL),0), " +
            " IFNULL(SUM(IFNULL(r.CreditUnits,0)*IFNULL(r.gradept,0)),0), " +
            " IFNULL(SUM(IFNULL(r.CreditUnits,0)),0), " +
            " IFNULL(ROUND(SUM(IFNULL(r.CreditUnits,0)*IFNULL(r.gradept,0))" +
            "   / NULLIF(SUM(IFNULL(r.CreditUnits,0)),0),2),0), " +
            " MIN(CASE WHEN " + YEAR_OK + " THEN r.acad END), " +
            " MAX(CASE WHEN " + YEAR_OK + " THEN r.acad END), " +
            " IF(IFNULL(MAX(r.studyyear),0) >= IFNULL(NULLIF(p.couselength,0),3),1,0), " +
            " " + OnListSql("s", withPrior) + ", NOW(), " +
            " IFNULL(MIN(CASE WHEN r.studyyear BETWEEN 1 AND 8 THEN r.studyyear END),0), " +
            " IFNULL(BIT_OR(" + GraduationEngine.TermBitSql("r") + "),0), " +
            " IFNULL(SUM(r.is_retake=1 AND r.score IS NULL),0), " +
            " IFNULL(MAX(d.n),0), IFNULL(MAX(d.cu),0), IFNULL(MAX(d.fl),0), " +
            " IFNULL(BIT_OR(IF(r.score>=" + GraduationEngine.PASS_MARK + ", cp.bits, 0)),0) " +
            "FROM acad_student s " +
            "LEFT JOIN acad_programme p ON p.progcode=s.progid " +
            "JOIN acad_results r ON r.regno=s.regno " +
            "LEFT JOIN (" + GraduationEngine.CapCoursesSql() + ") cp ON cp.courseID=r.courseid " +
            "LEFT JOIN (" + DuplicatesSql("") + ") d ON d.regno=s.regno " +
            "GROUP BY s.regno";
    }

    /// <summary>
    /// Brings ONE student's summary row back in line with acad_results.
    ///
    /// Called the moment a student is cleared, held or released, so the queue they just left
    /// does not still show them a refresh later. Costs about a millisecond - it touches one
    /// student's results, not the table.
    /// </summary>
    public static void Touch(MySqlConnection c, MySqlTransaction tx, string regno)
    {
        regno = (regno ?? "").Trim();
        if (regno == "") return;
        try
        {
            using (var cmd = new MySqlCommand(
                "UPDATE " + TABLE + " a JOIN acad_student s ON s.regno=a.regno SET a.on_list = " +
                " " + OnListSql("s", PriorTableExists(c)) + ", " +
                " a.refreshed_at = a.refreshed_at " +
                "WHERE a.regno=@r", c, tx))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* the summary is a cache; never let it fail a decision */ }
    }
}
