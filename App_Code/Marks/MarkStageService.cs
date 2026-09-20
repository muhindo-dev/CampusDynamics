using System;
using System.Configuration;
using MySql.Data.MySqlClient;

/// <summary>
/// Canonical lifecycle stages for a mark, plus helpers shared by the staged
/// publishing workflow (capture → approve → publish). See
/// COOPERP/MARKS_STAGED_WORKFLOW_PLAN.md.
///   NOT_ENTERED → ENTERED → CAPTURED (HOD) → APPROVED (Dean) → PUBLISHED (Senate)
/// The authoritative field is acad_course_registration.mark_stage; the legacy
/// provisional_marks_status is kept in sync for backward compatibility.
/// </summary>
public static class MarkStage
{
    public const string NOT_ENTERED = "NOT_ENTERED";
    public const string ENTERED     = "ENTERED";
    public const string CAPTURED    = "CAPTURED";
    public const string APPROVED    = "APPROVED";
    public const string PUBLISHED   = "PUBLISHED";

    // The enrolment/provisional-marks table lives in the portal DB; referenced
    // fully-qualified so a campus_dynamics connection can reach it.
    public const string REG = "campus_dynamics_portal.acad_course_registration";

    public static string ConnStr
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["vacConnectionString"];
            return cs != null ? cs.ConnectionString
                              : "Server=localhost;Database=campus_dynamics;Uid=root;Pwd=24thdecember1977;";
        }
    }

    /// <summary>Legacy provisional_marks_status kept in sync with the stage.</summary>
    public static string LegacyStatus(string stage)
    {
        if (stage == PUBLISHED) return "published";
        if (stage == APPROVED)  return "approved";
        if (stage == CAPTURED)  return "captured";
        if (stage == ENTERED)   return "pending";
        return "not_entered";
    }

    public static string Label(string stage)
    {
        if (stage == PUBLISHED) return "Published (Senate)";
        if (stage == APPROVED)  return "Approved (Dean)";
        if (stage == CAPTURED)  return "Captured (HOD)";
        if (stage == ENTERED)   return "Entered (Lecturer)";
        if (stage == NOT_ENTERED) return "Not entered";
        return stage;
    }

    /// <summary>Stage immediately below the given one (for send-back).</summary>
    public static string PrevStage(string stage)
    {
        if (stage == PUBLISHED) return APPROVED;
        if (stage == APPROVED)  return CAPTURED;
        if (stage == CAPTURED)  return ENTERED;
        if (stage == ENTERED)   return NOT_ENTERED;
        return NOT_ENTERED;
    }

    // ── Idempotent schema self-heal (safe to call on Page_Load) ──────────────
    // The self-heal below costs ~25 information_schema.COLUMNS probes - 365ms measured -
    // and it used to run on every stage-console page load, ahead of the queue and the funnel
    // on a serialised session lock. The schema cannot change underneath a running worker, so
    // once per process is enough; an app-pool recycle re-checks. Only a clean pass sets the
    // flag, so a failed self-heal still retries on the next request.
    private static volatile bool _schemaChecked;
    private static readonly object _schemaLock = new object();

    public static void EnsureSchema(MySqlConnection conn)
    {
        if (_schemaChecked) return;
        lock (_schemaLock)
        {
            if (_schemaChecked) return;
            EnsureSchemaCore(conn);
        }
    }

    private static void EnsureSchemaCore(MySqlConnection conn)
    {
        try
        {
            AddCol(conn, "mark_stage",            "VARCHAR(20) NULL");
            AddCol(conn, "capture_record_id",     "INT NULL");
            AddCol(conn, "approve_record_id",     "INT NULL");
            AddCol(conn, "publish_record_id",     "INT NULL");
            AddCol(conn, "mark_stage_changed_at", "DATETIME NULL");
            AddCol(conn, "mark_stage_changed_by", "VARCHAR(150) NULL");
            AddCol(conn, "mark_returned_reason",  "TEXT NULL");
            EnsureRecordTable(conn, "mark_capture_records");
            EnsureRecordTable(conn, "mark_approve_records");
            EnsureRecordTable(conn, "mark_publish_records");
            // self-heal newer session params on already-existing record tables
            AddRecCol(conn, "mark_capture_records", "param_fail_mode", "VARCHAR(12) NULL");
            AddRecCol(conn, "mark_approve_records", "param_fail_mode", "VARCHAR(12) NULL");
            AddRecCol(conn, "mark_publish_records", "param_fail_mode", "VARCHAR(12) NULL");
            // Live-progress + crash-recovery columns for the chunked commit engine.
            // progress_done/total drive the console's progress bar; heartbeat_at lets a
            // later caller tell a live RUNNING session from one whose worker died; and
            // last_error keeps the reason a partial run stopped.
            string[] tables = new string[] { "mark_capture_records", "mark_approve_records", "mark_publish_records" };
            for (int i = 0; i < tables.Length; i++)
            {
                AddRecCol(conn, tables[i], "progress_done",  "INT NULL");
                AddRecCol(conn, tables[i], "progress_total", "INT NULL");
                AddRecCol(conn, tables[i], "heartbeat_at",   "DATETIME NULL");
                AddRecCol(conn, tables[i], "last_error",     "TEXT NULL");
            }
            _schemaChecked = true;   // a clean pass only - a failure retries next request
        }
        catch { /* never break a page on self-heal */ }
    }

    private static void AddRecCol(MySqlConnection conn, string tbl, string col, string def)
    {
        using (var c = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='campus_dynamics_portal' " +
            "AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
        {
            c.Parameters.AddWithValue("@t", tbl);
            c.Parameters.AddWithValue("@c", col);
            if (Convert.ToInt32(c.ExecuteScalar()) > 0) return;
        }
        using (var a = new MySqlCommand("ALTER TABLE campus_dynamics_portal." + tbl + " ADD COLUMN " + col + " " + def, conn))
            a.ExecuteNonQuery();
    }

    private static void AddCol(MySqlConnection conn, string col, string def)
    {
        using (var c = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='campus_dynamics_portal' " +
            "AND TABLE_NAME='acad_course_registration' AND COLUMN_NAME=@c", conn))
        {
            c.Parameters.AddWithValue("@c", col);
            if (Convert.ToInt32(c.ExecuteScalar()) > 0) return;
        }
        using (var a = new MySqlCommand("ALTER TABLE campus_dynamics_portal.acad_course_registration ADD COLUMN " + col + " " + def, conn))
            a.ExecuteNonQuery();
    }

    private static void EnsureRecordTable(MySqlConnection conn, string tbl)
    {
        using (var c = new MySqlCommand(
            "CREATE TABLE IF NOT EXISTS campus_dynamics_portal." + tbl + " (" +
            " id INT PRIMARY KEY AUTO_INCREMENT, status VARCHAR(15) NULL," +
            " performed_by VARCHAR(150) NULL, performed_by_name VARCHAR(200) NULL, performed_by_role VARCHAR(40) NULL," +
            " scope_faculty_code VARCHAR(10) NULL, scope_department_id INT NULL," +
            " param_prog_id VARCHAR(25) NULL, param_year_of_study INT NULL, param_acad_year VARCHAR(25) NULL," +
            " param_semester INT NULL, param_all TINYINT NULL, param_fail_mode VARCHAR(12) NULL, params_json TEXT NULL," +
            " preview_count INT NULL, affected_count INT NULL, summary_json TEXT NULL, stats_snapshot_json TEXT NULL," +
            " notes TEXT NULL, is_migration TINYINT NULL DEFAULT 0," +
            " progress_done INT NULL, progress_total INT NULL, heartbeat_at DATETIME NULL, last_error TEXT NULL," +
            " created_at DATETIME NULL DEFAULT CURRENT_TIMESTAMP, committed_at DATETIME NULL" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8", conn))
            c.ExecuteNonQuery();
    }
}
