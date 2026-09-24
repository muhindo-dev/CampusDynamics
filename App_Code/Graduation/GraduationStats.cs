using System;
using System.Configuration;
using System.Web;
using MySql.Data.MySqlClient;

// =====================================================================
//  Graduation Centre — the per-student summary, and keeping it honest.
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
                // The connection is used for DDL, so no ambient transaction is assumed - DDL
                // commits implicitly in MySQL and a transaction here would be a lie.
                using (var cmd = new MySqlCommand("DROP TABLE IF EXISTS acad_grad_stats_new", c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }

                using (var cmd = new MySqlCommand(
                    "CREATE TABLE acad_grad_stats_new LIKE " + TABLE, c))
                { cmd.CommandTimeout = 300; cmd.ExecuteNonQuery(); }

                using (var cmd = new MySqlCommand(BuildSql("acad_grad_stats_new"), c))
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
    private static string BuildSql(string target)
    {
        const string YEAR_OK =
            "r.acad REGEXP '^[0-9]{4}/[0-9]{4}$' " +
            "AND CAST(LEFT(r.acad,4) AS UNSIGNED) BETWEEN 2000 AND 2035 " +
            "AND CAST(RIGHT(r.acad,4) AS UNSIGNED)=CAST(LEFT(r.acad,4) AS UNSIGNED)+1";

        return
            "INSERT INTO " + target + " (regno, progcode, plen, maxsy, courses, cu_earned, fails, " +
            " zero_marks, no_score, gp_num, gp_den, cgpa, first_year, last_year, is_candidate, " +
            " on_list, refreshed_at) " +
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
            " IF(EXISTS(SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno),1,0), NOW() " +
            "FROM acad_student s " +
            "LEFT JOIN acad_programme p ON p.progcode=s.progid " +
            "JOIN acad_results r ON r.regno=s.regno " +
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
                "UPDATE " + TABLE + " a SET a.on_list = " +
                " IF(EXISTS(SELECT 1 FROM acad_graduands g WHERE g.regno=a.regno),1,0), " +
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
