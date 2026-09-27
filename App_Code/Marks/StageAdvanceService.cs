using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Definition of one stage transition (capture / approve / publish). All three
/// share identical mechanics, so one engine (StageAdvanceService) drives them.
/// </summary>
public class StageDef
{
    public string Name;          // "CAPTURE" | "APPROVE" | "PUBLISH"
    public string RecordTable;   // mark_capture_records | mark_approve_records | mark_publish_records
    public string RecordCol;     // capture_record_id | approve_record_id | publish_record_id
    public string FromStage;     // ENTERED | CAPTURED | APPROVED
    public string ToStage;       // CAPTURED | APPROVED | PUBLISHED
    public bool   WritesResults; // true only for PUBLISH (writes acad_results)
}

/// <summary>
/// Session-based, scope-aware stage advance engine:
///   CreateDraft → Preview → Commit (snapshot + advance + back-reference) / Cancel / Return.
/// Every query is restricted by MarksScope (admin=all, dean=faculty, HOD=department).
/// </summary>
public static class StageAdvanceService
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    public static readonly StageDef Capture = new StageDef {
        Name = "CAPTURE", RecordTable = "mark_capture_records", RecordCol = "capture_record_id",
        FromStage = MarkStage.ENTERED, ToStage = MarkStage.CAPTURED, WritesResults = false };
    public static readonly StageDef Approve = new StageDef {
        Name = "APPROVE", RecordTable = "mark_approve_records", RecordCol = "approve_record_id",
        FromStage = MarkStage.CAPTURED, ToStage = MarkStage.APPROVED, WritesResults = false };
    public static readonly StageDef Publish = new StageDef {
        Name = "PUBLISH", RecordTable = "mark_publish_records", RecordCol = "publish_record_id",
        FromStage = MarkStage.APPROVED, ToStage = MarkStage.PUBLISHED, WritesResults = true };

    public static StageDef ByName(string name)
    {
        string n = (name ?? "").Trim().ToUpperInvariant();
        if (n == "APPROVE") return Approve;
        if (n == "PUBLISH") return Publish;
        return Capture;
    }

    // Pass mark (NCHE 2015): a total below this is a failure (grade F).
    private const int PASS_MARK = 50;

    // Failure-filter fragment. EXCLUDE drops marks below the pass mark (nulls — no mark yet —
    // are kept, never silently dropped). INCLUDE (default / anything else) = no fail filter.
    private static string FailClause(string failMode)
    {
        if (string.Equals((failMode ?? "").Trim(), "EXCLUDE", StringComparison.OrdinalIgnoreCase))
            return " AND (cr.provisional_total_marks IS NULL OR cr.provisional_total_marks >= " + PASS_MARK + ") ";
        return "";
    }

    // ── Match WHERE for rows currently in FromStage, within scope + chosen params ──
    private static string BuildWhere(StageDef def, MarksScope scope, MySqlCommand cmd,
        string progId, int yearOfStudy, string acadYear, int semester, string failMode)
    {
        var sb = new StringBuilder(" WHERE cr.mark_stage = @from ");
        cmd.Parameters.AddWithValue("@from", def.FromStage);
        sb.Append(scope.ProgFilter("cr"));
        if (!string.IsNullOrEmpty(progId)) { sb.Append(" AND cr.prog_id = @prog "); cmd.Parameters.AddWithValue("@prog", progId); }
        if (!string.IsNullOrEmpty(acadYear)) { sb.Append(" AND cr.acad_year = @ay "); cmd.Parameters.AddWithValue("@ay", acadYear); }
        if (semester > 0) { sb.Append(" AND cr.semester = @sem "); cmd.Parameters.AddWithValue("@sem", semester); }
        if (yearOfStudy > 0)
        {
            sb.Append(" AND EXISTS (SELECT 1 FROM acad_registration r2 WHERE r2.regno=cr.regno " +
                      "AND r2.acad_year=cr.acad_year AND r2.semester=cr.semester AND r2.studyyear=@yos) ");
            cmd.Parameters.AddWithValue("@yos", yearOfStudy);
        }
        sb.Append(FailClause(failMode));   // no parameters — literal pass mark
        return sb.ToString();
    }

    // ── 1) Create a DRAFT record stamped with scope + params ─────────────────
    public static int CreateDraft(StageDef def, MarksScope scope, string actor, string actorName, string role,
        string progId, int yearOfStudy, string acadYear, int semester, bool all, string notes, string failMode)
    {
        // normalise: only 'EXCLUDE' changes behaviour; everything else = INCLUDE (default).
        string fm = string.Equals((failMode ?? "").Trim(), "EXCLUDE", StringComparison.OrdinalIgnoreCase) ? "EXCLUDE" : "INCLUDE";
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();
            string paramsJson = Json.Serialize(new {
                progId = progId, yearOfStudy = yearOfStudy, acadYear = acadYear, semester = semester, all = all, failMode = fm });
            using (var cmd = new MySqlCommand(
                "INSERT INTO campus_dynamics_portal." + def.RecordTable +
                " (status, performed_by, performed_by_name, performed_by_role, scope_faculty_code, scope_department_id," +
                "  param_prog_id, param_year_of_study, param_acad_year, param_semester, param_all, param_fail_mode, params_json, notes, created_at)" +
                " VALUES ('DRAFT',@by,@nm,@role,@fac,@dept,@prog,@yos,@ay,@sem,@all,@fm,@pj,@notes,NOW())", conn))
            {
                cmd.Parameters.AddWithValue("@by", actor ?? "");
                cmd.Parameters.AddWithValue("@nm", actorName ?? "");
                cmd.Parameters.AddWithValue("@role", role ?? "");
                cmd.Parameters.AddWithValue("@fac", (scope.FacultyCodes.Count > 0) ? (object)scope.FacultyCodes[0] : DBNull.Value);
                cmd.Parameters.AddWithValue("@dept", (scope.DepartmentIds.Count > 0) ? (object)scope.DepartmentIds[0] : DBNull.Value);
                cmd.Parameters.AddWithValue("@prog", string.IsNullOrEmpty(progId) ? (object)DBNull.Value : progId);
                cmd.Parameters.AddWithValue("@yos", yearOfStudy > 0 ? (object)yearOfStudy : DBNull.Value);
                cmd.Parameters.AddWithValue("@ay", string.IsNullOrEmpty(acadYear) ? (object)DBNull.Value : acadYear);
                cmd.Parameters.AddWithValue("@sem", semester > 0 ? (object)semester : DBNull.Value);
                cmd.Parameters.AddWithValue("@all", all ? 1 : 0);
                cmd.Parameters.AddWithValue("@fm", fm);
                cmd.Parameters.AddWithValue("@pj", paramsJson);
                cmd.Parameters.AddWithValue("@notes", notes ?? "");
                cmd.ExecuteNonQuery();
                return (int)cmd.LastInsertedId;
            }
        }
    }

    // Load a draft record's params back into locals.
    private static bool LoadRecord(MySqlConnection conn, StageDef def, int recordId,
        out string status, out string progId, out int yos, out string acadYear, out int semester, out string failMode)
    {
        status = ""; progId = ""; yos = 0; acadYear = ""; semester = 0; failMode = "INCLUDE";
        using (var cmd = new MySqlCommand(
            "SELECT status, IFNULL(param_prog_id,''), IFNULL(param_year_of_study,0), IFNULL(param_acad_year,''), IFNULL(param_semester,0), IFNULL(param_fail_mode,'INCLUDE') " +
            "FROM campus_dynamics_portal." + def.RecordTable + " WHERE id=@id LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("@id", recordId);
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return false;
                status = r.GetString(0); progId = r.GetString(1); yos = r.GetInt32(2);
                acadYear = r.GetString(3); semester = r.GetInt32(4); failMode = r.GetString(5);
                return true;
            }
        }
    }

    // ── 2) Preview — count + per-programme breakdown + stats snapshot (no writes) ──
    public static object Preview(StageDef def, MarksScope scope, int recordId)
    {
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();
            string status, progId, acadYear, failMode; int yos, semester;
            if (!LoadRecord(conn, def, recordId, out status, out progId, out yos, out acadYear, out semester, out failMode))
                return new { success = false, message = "Record not found." };

            object stats = MatchStats(conn, def, scope, progId, yos, acadYear, semester, failMode);
            List<object> rows = MatchBreakdown(conn, def, scope, progId, yos, acadYear, semester, failMode);
            // Exact COUNT(*) — not the sum of the (LIMIT 200) breakdown groups, which
            // under-reported any selection spanning more than 200 programme/term combinations.
            int count = MatchCount(conn, def, scope, progId, yos, acadYear, semester, failMode);

            // Persist the preview snapshot onto the record.
            using (var up = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                " SET preview_count=@c, stats_snapshot_json=@s WHERE id=@id", conn))
            {
                up.Parameters.AddWithValue("@c", count);
                up.Parameters.AddWithValue("@s", Json.Serialize(stats));
                up.Parameters.AddWithValue("@id", recordId);
                up.ExecuteNonQuery();
            }
            return new { success = true, recordId = recordId, count = count, fromStage = def.FromStage,
                         toStage = def.ToStage, breakdown = rows, stats = stats };
        }
    }

    private static object MatchStats(MySqlConnection conn, StageDef def, MarksScope scope,
        string progId, int yos, string acadYear, int semester, string failMode)
    {
        using (var cmd = new MySqlCommand("", conn))
        {
            string where = BuildWhere(def, scope, cmd, progId, yos, acadYear, semester, failMode);

            // Publishing overwrites: a course already carrying a result gets the new mark, and
            // where that result was filed under another term it is moved here. Both are correct
            // and neither should be a surprise, so the preview counts them before the operator
            // commits rather than leaving them to be discovered in the per-row log afterwards.
            //
            // Only PUBLISH writes results, so only PUBLISH pays for the join. CONVERT sits on
            // the registration side: acad_results is the side being looked up and its columns
            // have to stay bare or Index_UNQ(regno, courseid) is not used. The two tables are
            // in different databases with different collations, which is what makes this
            // necessary at all.
            bool pub = def.WritesResults;
            string join = pub
                ? " LEFT JOIN acad_results ar ON ar.regno = CONVERT(cr.regno USING utf8)" +
                  " AND ar.courseid = CONVERT(cr.courseID USING utf8) "
                : "";
            string extra = pub
                ? ", SUM(ar.regno IS NOT NULL) replacing" +
                  ", SUM(ar.regno IS NOT NULL AND (TRIM(ar.acad) <> TRIM(CONVERT(cr.acad_year USING utf8))" +
                  "                             OR ar.semester <> cr.semester)) takeovers" +
                  ", SUM(ar.score IS NOT NULL AND cr.provisional_total_marks < ar.score) lowered"
                : ", 0 replacing, 0 takeovers, 0 lowered";

            cmd.CommandText =
                "SELECT COUNT(*) c, COUNT(DISTINCT cr.regno) studs, COUNT(DISTINCT cr.prog_id) progs," +
                " ROUND(AVG(cr.provisional_total_marks),1) avg_total," +
                " SUM(cr.provisional_total_marks>=50) passes, SUM(cr.provisional_total_marks>=80) distinctions," +
                " SUM(cr.provisional_total_marks<50) fails" + extra +
                " FROM " + MarkStage.REG + " cr " + join + where;
            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    int c = ToI(r["c"]); int passes = ToI(r["passes"]);
                    return new {
                        marks = c, students = ToI(r["studs"]), programmes = ToI(r["progs"]),
                        avgTotal = r["avg_total"] == DBNull.Value ? 0 : Convert.ToDouble(r["avg_total"]),
                        passes = passes, passRate = c > 0 ? Math.Round((double)passes * 100 / c, 1) : 0,
                        distinctions = ToI(r["distinctions"]), fails = ToI(r["fails"]),
                        replacing = ToI(r["replacing"]), takeovers = ToI(r["takeovers"]),
                        lowered = ToI(r["lowered"]) };
                }
            }
        }
        return new { marks = 0, students = 0, programmes = 0, avgTotal = 0, passes = 0, passRate = 0,
                     distinctions = 0, fails = 0, replacing = 0, takeovers = 0, lowered = 0 };
    }

    /// <summary>
    /// Exact number of rows the session will act on.
    ///
    /// The count used to be derived by summing MatchBreakdown's per-programme rows, but that
    /// query carries LIMIT 200 on the GROUP BY — so any selection spanning more than 200
    /// (programme, year, semester) combinations under-reported both the preview figure the
    /// operator approves and the progress-bar total. A plain COUNT(*) cannot drift.
    /// </summary>
    private static int MatchCount(MySqlConnection conn, StageDef def, MarksScope scope,
        string progId, int yos, string acadYear, int semester, string failMode)
    {
        using (var cmd = new MySqlCommand("", conn))
        {
            string where = BuildWhere(def, scope, cmd, progId, yos, acadYear, semester, failMode);
            cmd.CommandText = "SELECT COUNT(*) FROM " + MarkStage.REG + " cr " + where;
            object v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
        }
    }

    private static List<object> MatchBreakdown(MySqlConnection conn, StageDef def, MarksScope scope,
        string progId, int yos, string acadYear, int semester, string failMode)
    {
        var list = new List<object>();
        using (var cmd = new MySqlCommand("", conn))
        {
            string where = BuildWhere(def, scope, cmd, progId, yos, acadYear, semester, failMode);
            cmd.CommandText =
                "SELECT cr.prog_id, COALESCE(NULLIF(p.progname,''),cr.prog_id) pname, cr.acad_year, cr.semester," +
                " COUNT(*) cnt, COUNT(DISTINCT cr.regno) studs, SUM(cr.provisional_total_marks<50) fails" +
                " FROM " + MarkStage.REG + " cr LEFT JOIN acad_programme p ON p.progcode=cr.prog_id " + where +
                " GROUP BY cr.prog_id, pname, cr.acad_year, cr.semester ORDER BY cnt DESC LIMIT 200";
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var d = new Dictionary<string, object>();
                    d["prog_id"] = S(r["prog_id"]); d["prog_name"] = S(r["pname"]);
                    d["acad_year"] = S(r["acad_year"]); d["semester"] = S(r["semester"]);
                    d["count"] = ToI(r["cnt"]); d["students"] = ToI(r["studs"]); d["fails"] = ToI(r["fails"]);
                    list.Add(d);
                }
        }
        return list;
    }

    // ── 3) Commit — advance matched rows, snapshot, back-reference, finalise ──
    //
    // Reworked to be chunked, ordered, resumable and deadlock-tolerant. See MarksBatchRunner
    // for the reasoning; the short version is that this used to be one enormous transaction
    // holding every lock until the very end, and a single deadlock threw the whole run away.
    //
    // Non-PUBLISH stages (capture / approve) stay a single set-based UPDATE — they touch one
    // column on one table and are already fast and short-lived.
    public static object Commit(StageDef def, MarksScope scope, int recordId, string actor)
    {
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();
            MarksBatchRunner.PrepareSession(conn);

            string status, progId, acadYear, failMode; int yos, semester;
            if (!LoadRecord(conn, def, recordId, out status, out progId, out yos, out acadYear, out semester, out failMode))
                return new { success = false, message = "Record not found." };
            if (status == "COMMITTED") return new { success = false, message = "This session is already committed." };
            if (status == "CANCELLED") return new { success = false, message = "This session was cancelled." };

            // Claim the session ATOMICALLY. The old code read `status` here and only opened
            // its transaction afterwards, so a double-clicked Commit (or a retry after the
            // browser gave up on a slow run) could get two workers past this check and
            // publish everything twice. Whoever flips DRAFT→RUNNING wins; everyone else is
            // told to watch instead. A run whose worker died leaves a stale RUNNING, so a
            // heartbeat older than the grace period may be taken over.
            if (!ClaimSession(conn, def, recordId, actor))
                return new { success = false, alreadyRunning = true,
                             message = "This session is already being committed (started moments ago). Watch its progress rather than starting it again." };

            // snapshot BEFORE advancing (captures the state that is being moved)
            object stats = MatchStats(conn, def, scope, progId, yos, acadYear, semester, failMode);
            List<object> breakdown = MatchBreakdown(conn, def, scope, progId, yos, acadYear, semester, failMode);
            int expected = MatchCount(conn, def, scope, progId, yos, acadYear, semester, failMode);
            SetProgress(conn, def, recordId, 0, expected);

            int affected = 0, skipped = 0;
            var skipReasons = new Dictionary<string, int>();
            string failure = null;

            try
            {
                if (def.WritesResults)
                {
                    // Resolve schema shape + run any one-off DDL BEFORE the first transaction:
                    // an ALTER implicitly commits in MySQL and would silently break atomicity
                    // if it fired mid-batch.
                    string creditCol = MarksControllerShared.PrepareForBatchPublish(conn);

                    // Only one publish batch may run at a time, cluster-wide.
                    if (!MarksBatchRunner.TryAcquire(conn, PublishLockName, 5))
                    {
                        ReleaseSession(conn, def, recordId, "DRAFT", null);
                        return new { success = false, busy = true,
                                     message = "Another publish batch is running right now. Please wait for it to finish, then commit this session." };
                    }

                    try
                    {
                        affected = PublishInChunks(conn, def, scope, recordId, actor, progId, yos, acadYear,
                                                   semester, failMode, creditCol, skipReasons, out skipped);
                    }
                    finally
                    {
                        MarksBatchRunner.Release(conn, PublishLockName);
                    }
                }
                else
                {
                    // Capture / approve: one set-based UPDATE, still ordered for lock stability.
                    MarksBatchRunner.RunChunkWithRetry(conn, delegate(MySqlTransaction tx)
                    {
                        using (var cmd = new MySqlCommand("", conn, tx))
                        {
                            string where = BuildWhere(def, scope, cmd, progId, yos, acadYear, semester, failMode);
                            cmd.CommandText =
                                "UPDATE " + MarkStage.REG + " cr SET cr.mark_stage=@to, cr.provisional_marks_status=@legacy," +
                                " cr." + def.RecordCol + "=@rid, cr.mark_stage_changed_at=NOW(), cr.mark_stage_changed_by=@by " + where +
                                " ORDER BY cr.id";
                            cmd.Parameters.AddWithValue("@to", def.ToStage);
                            cmd.Parameters.AddWithValue("@legacy", MarkStage.LegacyStatus(def.ToStage));
                            cmd.Parameters.AddWithValue("@rid", recordId);
                            cmd.Parameters.AddWithValue("@by", actor ?? "");
                            affected = cmd.ExecuteNonQuery();
                        }
                    });
                    SetProgress(conn, def, recordId, affected, expected);
                }
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }

            // Finalise. Work already committed by earlier chunks is KEPT and recorded — a
            // late failure no longer discards everything that succeeded, and re-running the
            // session simply picks up the rows that are still at the source stage.
            string finalStatus = failure == null ? "COMMITTED" : "FAILED";
            var summary = new Dictionary<string, object>();
            summary["fromStage"] = def.FromStage;
            summary["toStage"] = def.ToStage;
            summary["breakdown"] = breakdown;
            summary["expected"] = expected;
            summary["advanced"] = affected;
            summary["skipped"] = skipped;
            if (skipReasons.Count > 0) summary["skipReasons"] = skipReasons;
            if (failure != null) summary["error"] = failure;

            // affected_count ACCUMULATES across runs. A session that stopped part-way and was
            // resumed published the sum of both runs, and the audit record must say so;
            // overwriting it would under-report what actually reached students' records.
            // progress_done/total describe only the run just finished (i.e. the remaining
            // work it was asked to do), which is what the progress bar was tracking.
            using (var fin = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                " SET status=@st, committed_at=NOW(), affected_count=COALESCE(affected_count,0)+@n," +
                " summary_json=@sum, stats_snapshot_json=@stats," +
                " progress_done=@n, progress_total=@tot, last_error=@err, heartbeat_at=NOW()," +
                " performed_by=COALESCE(performed_by,@by) WHERE id=@id", conn))
            {
                fin.Parameters.AddWithValue("@st", finalStatus);
                fin.Parameters.AddWithValue("@n", affected);
                fin.Parameters.AddWithValue("@tot", expected);
                fin.Parameters.AddWithValue("@sum", Json.Serialize(summary));
                fin.Parameters.AddWithValue("@stats", Json.Serialize(stats));
                fin.Parameters.AddWithValue("@err", (object)failure ?? DBNull.Value);
                fin.Parameters.AddWithValue("@by", actor ?? "");
                fin.Parameters.AddWithValue("@id", recordId);
                fin.ExecuteNonQuery();
            }

            if (failure != null)
                return new { success = false, recordId = recordId, affected = affected, skipped = skipped,
                             partial = affected > 0,
                             message = "Publish stopped after " + affected + " of " + expected + " mark(s): " + failure +
                                       (affected > 0 ? " Work already committed has been kept — re-run this session to continue from where it stopped." : "") };

            return new { success = true, recordId = recordId, affected = affected, skipped = skipped,
                         expected = expected, skipReasons = skipReasons,
                         fromStage = def.FromStage, toStage = def.ToStage, stats = stats,
                         message = BuildOutcomeMessage(def, affected, expected, skipped) };
        }
    }

    private const string PublishLockName = "cd_marks_publish_batch";

    private static string BuildOutcomeMessage(StageDef def, int affected, int expected, int skipped)
    {
        string msg = affected.ToString("N0") + " mark(s) advanced to " + MarkStage.Label(def.ToStage) + ".";
        if (skipped > 0)
            msg += " " + skipped.ToString("N0") + " could not be advanced (see session details).";
        return msg;
    }

    /// <summary>
    /// Publish every matching row, in ascending id order, in short transactions.
    ///
    /// Paging is by an id WATERMARK rather than OFFSET. That matters for two reasons:
    /// published rows leave the source stage, so an OFFSET would skip over rows as the
    /// queue shrinks under it; and a row that cannot be published stays at the source
    /// stage, so a naive "re-select the queue" loop would meet it again forever. Walking
    /// strictly forward past the highest id seen visits every row exactly once and
    /// terminates.
    ///
    /// Ascending primary-key order is also what makes concurrent runs deadlock-free: locks
    /// are always acquired in the same sequence.
    /// </summary>
    private static int PublishInChunks(MySqlConnection conn, StageDef def, MarksScope scope, int recordId,
        string actor, string progId, int yos, string acadYear, int semester, string failMode,
        string creditCol, Dictionary<string, int> skipReasons, out int skipped)
    {
        int affected = 0; int localSkipped = 0; int afterId = 0;

        while (true)
        {
            var ids = new List<int>();
            using (var cmd = new MySqlCommand("", conn))
            {
                string where = BuildWhere(def, scope, cmd, progId, yos, acadYear, semester, failMode);
                cmd.CommandText = "SELECT cr.id FROM " + MarkStage.REG + " cr " + where +
                                  " AND cr.id > @afterId ORDER BY cr.id LIMIT " + MarksBatchRunner.ChunkSize;
                cmd.Parameters.AddWithValue("@afterId", afterId);
                using (var r = cmd.ExecuteReader()) while (r.Read()) ids.Add(Convert.ToInt32(r[0]));
            }
            if (ids.Count == 0) break;

            int chunkAffected = 0, chunkSkipped = 0;
            var chunkReasons = new Dictionary<string, int>();
            var gpa = new MarksGpaDeferral();

            MarksBatchRunner.RunChunkWithRetry(conn, delegate(MySqlTransaction tx)
            {
                // Reset per attempt — a retried chunk must not double-count.
                chunkAffected = 0; chunkSkipped = 0; chunkReasons.Clear(); gpa.Clear();

                foreach (int id in ids)
                {
                    string reason;
                    if (MarksControllerShared.PublishSingle(conn, tx, id, actor, gpa, out reason))
                    {
                        StampRow(conn, tx, def, recordId, actor, id);
                        chunkAffected++;
                    }
                    else
                    {
                        chunkSkipped++;
                        string key = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
                        chunkReasons[key] = (chunkReasons.ContainsKey(key) ? chunkReasons[key] : 0) + 1;
                    }
                }

                // Settle every touched student/semester GPA once, inside the same
                // transaction, in a deterministic order.
                gpa.Flush(conn, tx, creditCol);
            });

            affected += chunkAffected;
            localSkipped += chunkSkipped;
            foreach (var kv in chunkReasons)
                skipReasons[kv.Key] = (skipReasons.ContainsKey(kv.Key) ? skipReasons[kv.Key] : 0) + kv.Value;

            afterId = ids[ids.Count - 1];

            // Publish progress between chunks so the console can show a live count.
            BumpProgress(conn, def, recordId, affected);
        }

        skipped = localSkipped;
        return affected;
    }

    // ── Session claim / progress plumbing ────────────────────────────────────

    /// <summary>A RUNNING session whose heartbeat is older than this is treated as abandoned.</summary>
    private const int StaleRunningMinutes = 5;

    /// <summary>
    /// Atomically move a session to RUNNING. Returns false when someone else already holds
    /// it. This is the single guard against a double-submitted Commit publishing everything
    /// twice: the UPDATE's WHERE clause is evaluated under a row lock, so exactly one caller
    /// can observe the claimable state and change it.
    ///
    /// Claimable states:
    ///   DRAFT / NULL  — a fresh session.
    ///   FAILED        — a run that stopped part-way. Re-running it is the Resume path:
    ///                   rows already published have left the source stage, so the match
    ///                   set naturally contains only the remainder.
    ///   stale RUNNING — a worker killed mid-run (app-pool recycle, dropped connection)
    ///                   would otherwise strand the session in RUNNING for ever, so a
    ///                   heartbeat older than the grace period may be taken over.
    /// COMMITTED is deliberately absent: a finished session must never re-publish.
    /// </summary>
    private static bool ClaimSession(MySqlConnection conn, StageDef def, int recordId, string actor)
    {
        using (var cmd = new MySqlCommand(
            "UPDATE campus_dynamics_portal." + def.RecordTable +
            " SET status='RUNNING', heartbeat_at=NOW(), last_error=NULL," +
            "     performed_by=COALESCE(NULLIF(performed_by,''),@by)" +
            " WHERE id=@id AND (status='DRAFT' OR status IS NULL OR status='FAILED'" +
            "                   OR (status='RUNNING' AND (heartbeat_at IS NULL OR heartbeat_at < NOW() - INTERVAL " +
            StaleRunningMinutes + " MINUTE)))", conn))
        {
            cmd.Parameters.AddWithValue("@by", actor ?? "");
            cmd.Parameters.AddWithValue("@id", recordId);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    /// <summary>Hand a claimed session back (used when we bail out before doing any work).</summary>
    private static void ReleaseSession(MySqlConnection conn, StageDef def, int recordId, string toStatus, string error)
    {
        try
        {
            using (var cmd = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                " SET status=@st, last_error=@err WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@st", toStatus);
                cmd.Parameters.AddWithValue("@err", (object)error ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", recordId);
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    private static void SetProgress(MySqlConnection conn, StageDef def, int recordId, int done, int total)
    {
        try
        {
            using (var cmd = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                " SET progress_done=@d, progress_total=@t, heartbeat_at=NOW() WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@d", done);
                cmd.Parameters.AddWithValue("@t", total);
                cmd.Parameters.AddWithValue("@id", recordId);
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* progress reporting must never abort a run */ }
    }

    /// <summary>
    /// Between-chunk progress + heartbeat. The heartbeat is what tells a later caller
    /// whether a RUNNING session is alive or abandoned.
    /// </summary>
    private static void BumpProgress(MySqlConnection conn, StageDef def, int recordId, int done)
    {
        try
        {
            using (var cmd = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                " SET progress_done=@d, heartbeat_at=NOW() WHERE id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@d", done);
                cmd.Parameters.AddWithValue("@id", recordId);
                cmd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    /// <summary>Live progress for the console's polling loop.</summary>
    public static object Progress(StageDef def, int recordId)
    {
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(
                "SELECT status, IFNULL(progress_done,0), IFNULL(progress_total,0), IFNULL(affected_count,0)," +
                " IFNULL(last_error,''), IFNULL(TIMESTAMPDIFF(SECOND, heartbeat_at, NOW()), 999999)" +
                " FROM campus_dynamics_portal." + def.RecordTable + " WHERE id=@id LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", recordId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return new { success = false, message = "Session not found." };
                    string st = r.GetString(0);
                    int done = r.GetInt32(1), total = r.GetInt32(2), aff = r.GetInt32(3);
                    string err = r.GetString(4);
                    int since = r.GetInt32(5);
                    bool running = st == "RUNNING" && since <= StaleRunningMinutes * 60;
                    return new {
                        success = true, recordId = recordId, status = st, running = running,
                        done = done, total = total, affected = aff,
                        stalled = st == "RUNNING" && !running,
                        error = err,
                        percent = total > 0 ? (int)Math.Min(100, Math.Round(done * 100.0 / total)) : 0
                    };
                }
            }
        }
    }

    private static void StampRow(MySqlConnection conn, MySqlTransaction tx, StageDef def, int recordId, string actor, int id)
    {
        using (var cmd = new MySqlCommand("UPDATE " + MarkStage.REG +
            " SET mark_stage=@to, provisional_marks_status=@legacy, " + def.RecordCol + "=@rid," +
            " mark_stage_changed_at=NOW(), mark_stage_changed_by=@by WHERE id=@id", conn, tx))
        {
            cmd.Parameters.AddWithValue("@to", def.ToStage);
            cmd.Parameters.AddWithValue("@legacy", MarkStage.LegacyStatus(def.ToStage));
            cmd.Parameters.AddWithValue("@rid", recordId);
            cmd.Parameters.AddWithValue("@by", actor ?? "");
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public static object Cancel(StageDef def, int recordId)
    {
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                " SET status='CANCELLED' WHERE id=@id AND status='DRAFT'", conn))
            {
                cmd.Parameters.AddWithValue("@id", recordId);
                int n = cmd.ExecuteNonQuery();
                return new { success = n > 0, message = n > 0 ? "Session cancelled." : "Only draft sessions can be cancelled." };
            }
        }
    }

    // ── Send-back: return specific marks one stage down (with reason) ─────────
    public static object ReturnMarks(StageDef def, MarksScope scope, int[] ids, string reason, string actor)
    {
        if (ids == null || ids.Length == 0) return new { success = false, message = "No marks selected." };
        if (string.IsNullOrEmpty(reason)) return new { success = false, message = "A reason is required to send marks back." };
        string prev = MarkStage.PrevStage(def.FromStage); // one below the stage's source
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();
            MarksBatchRunner.PrepareSession(conn);

            // Ascending id order for the same lock-ordering reason as AdvanceMarks, and
            // chunked into short retried transactions instead of N loose autocommit
            // statements (which left a half-finished send-back behind on any error).
            var ordered = new List<int>(ids);
            ordered.Sort();

            int affected = 0;
            for (int start = 0; start < ordered.Count; start += MarksBatchRunner.ChunkSize)
            {
                int end = Math.Min(start + MarksBatchRunner.ChunkSize, ordered.Count);
                var slice = ordered.GetRange(start, end - start);
                int chunkAffected = 0;

                MarksBatchRunner.RunChunkWithRetry(conn, delegate(MySqlTransaction tx)
                {
                    chunkAffected = 0;
                    foreach (int id in slice)
                    {
                        if (!scope.IsAdmin)
                        {
                            // scope guard: only return marks within the user's scope + current FromStage
                            using (var chk = new MySqlCommand("SELECT COALESCE(prog_id,'') FROM " + MarkStage.REG + " WHERE id=@id AND mark_stage=@from LIMIT 1", conn, tx))
                            {
                                chk.Parameters.AddWithValue("@id", id);
                                chk.Parameters.AddWithValue("@from", def.FromStage);
                                object v = chk.ExecuteScalar();
                                if (v == null || v == DBNull.Value || !scope.AllowsProg(v.ToString().Trim())) continue;
                            }
                        }
                        using (var cmd = new MySqlCommand("UPDATE " + MarkStage.REG +
                            " SET mark_stage=@prev, provisional_marks_status=@legacy, mark_returned_reason=@reason," +
                            " mark_stage_changed_at=NOW(), mark_stage_changed_by=@by WHERE id=@id AND mark_stage=@from", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@prev", prev);
                            cmd.Parameters.AddWithValue("@legacy", MarkStage.LegacyStatus(prev));
                            cmd.Parameters.AddWithValue("@reason", reason);
                            cmd.Parameters.AddWithValue("@by", actor ?? "");
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.Parameters.AddWithValue("@from", def.FromStage);
                            chunkAffected += cmd.ExecuteNonQuery();
                        }
                    }
                });
                affected += chunkAffected;
            }

            return new { success = affected > 0, affected = affected,
                         message = affected + " mark(s) returned to " + MarkStage.Label(prev) + "." };
        }
    }

    // ── Advance specific marks one stage UP (the forward counterpart of ReturnMarks) ──
    // Advances the selected acad_course_registration rows from FromStage → ToStage, guarded by
    // scope + FromStage, with a COMMITTED audit record (so it appears in Session history / CSV).
    // For PUBLISH (WritesResults) each row is written to acad_results via PublishSingle, exactly
    // like the wizard Commit path.
    public static object AdvanceMarks(StageDef def, MarksScope scope, int[] ids, string notes,
        string actor, string actorName, string role)
    {
        if (ids == null || ids.Length == 0) return new { success = false, message = "No marks selected." };
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();

            // 1) Audit record (DRAFT) — records who advanced how many, by explicit selection.
            int recordId;
            using (var cmd = new MySqlCommand(
                "INSERT INTO campus_dynamics_portal." + def.RecordTable +
                " (status, performed_by, performed_by_name, performed_by_role, scope_faculty_code, scope_department_id," +
                "  param_all, param_fail_mode, params_json, notes, created_at)" +
                " VALUES ('DRAFT',@by,@nm,@role,@fac,@dept,0,'INCLUDE',@pj,@notes,NOW())", conn))
            {
                cmd.Parameters.AddWithValue("@by", actor ?? "");
                cmd.Parameters.AddWithValue("@nm", actorName ?? "");
                cmd.Parameters.AddWithValue("@role", role ?? "");
                cmd.Parameters.AddWithValue("@fac", (scope.FacultyCodes.Count > 0) ? (object)scope.FacultyCodes[0] : DBNull.Value);
                cmd.Parameters.AddWithValue("@dept", (scope.DepartmentIds.Count > 0) ? (object)scope.DepartmentIds[0] : DBNull.Value);
                cmd.Parameters.AddWithValue("@pj", Json.Serialize(new { mode = "SELECTED", selectedIds = ids.Length }));
                cmd.Parameters.AddWithValue("@notes", string.IsNullOrEmpty(notes) ? ("Advanced " + ids.Length + " selected mark(s).") : notes);
                cmd.ExecuteNonQuery();
                recordId = (int)cmd.LastInsertedId;
            }

            // Lock ordering: the ids arrive in whatever order the browser's checkboxes were
            // rendered/ticked. Two admins advancing overlapping selections in different
            // orders is a textbook deadlock. Sorting ascending gives every caller — and the
            // wizard's chunked publisher, which also walks ascending — one common order.
            var ordered = new List<int>(ids);
            ordered.Sort();

            int affected = 0, skipped = 0;
            var skipReasons = new Dictionary<string, int>();
            string failure = null;

            // Publishing writes results and recomputes GPAs, so it takes the same
            // cluster-wide mutex and the same one-off schema prep as the wizard path.
            string creditCol = null;
            bool holdingLock = false;
            if (def.WritesResults)
            {
                creditCol = MarksControllerShared.PrepareForBatchPublish(conn);
                if (!MarksBatchRunner.TryAcquire(conn, PublishLockName, 5))
                {
                    using (var c = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                        " SET status='CANCELLED', last_error='Publish already running' WHERE id=@id", conn))
                    { c.Parameters.AddWithValue("@id", recordId); c.ExecuteNonQuery(); }
                    return new { success = false, busy = true,
                                 message = "Another publish batch is running right now. Please wait for it to finish and try again." };
                }
                holdingLock = true;
            }

            try
            {
                // Same chunking as the wizard: short transactions, retried on deadlock, so a
                // large hand-picked selection cannot hold the whole table hostage either.
                for (int start = 0; start < ordered.Count; start += MarksBatchRunner.ChunkSize)
                {
                    int end = Math.Min(start + MarksBatchRunner.ChunkSize, ordered.Count);
                    var slice = ordered.GetRange(start, end - start);

                    int cAff = 0, cSkip = 0;
                    var cReasons = new Dictionary<string, int>();
                    var gpa = new MarksGpaDeferral();

                    MarksBatchRunner.RunChunkWithRetry(conn, delegate(MySqlTransaction tx)
                    {
                        cAff = 0; cSkip = 0; cReasons.Clear(); gpa.Clear();

                        foreach (int id in slice)
                        {
                            // Guard: row must currently be at FromStage AND within the actor's scope.
                            using (var chk = new MySqlCommand(
                                "SELECT COALESCE(prog_id,'') FROM " + MarkStage.REG + " WHERE id=@id AND mark_stage=@from LIMIT 1", conn, tx))
                            {
                                chk.Parameters.AddWithValue("@id", id);
                                chk.Parameters.AddWithValue("@from", def.FromStage);
                                object v = chk.ExecuteScalar();
                                if (v == null || v == DBNull.Value) { cSkip++; Bump(cReasons, "Already moved on, or no longer at " + MarkStage.Label(def.FromStage)); continue; }
                                if (!scope.IsAdmin && !scope.AllowsProg(v.ToString().Trim())) { cSkip++; Bump(cReasons, "Outside your faculty/department"); continue; }
                            }

                            if (def.WritesResults)
                            {
                                string reason;
                                if (MarksControllerShared.PublishSingle(conn, tx, id, actor, gpa, out reason))
                                { StampRow(conn, tx, def, recordId, actor, id); cAff++; }
                                else { cSkip++; Bump(cReasons, string.IsNullOrEmpty(reason) ? "Unknown" : reason); }
                            }
                            else
                            {
                                StampRow(conn, tx, def, recordId, actor, id); cAff++;
                            }
                        }

                        gpa.Flush(conn, tx, creditCol);
                    });

                    affected += cAff; skipped += cSkip;
                    foreach (var kv in cReasons) skipReasons[kv.Key] = (skipReasons.ContainsKey(kv.Key) ? skipReasons[kv.Key] : 0) + kv.Value;
                    BumpProgress(conn, def, recordId, affected);
                }
            }
            catch (Exception ex) { failure = ex.Message; }
            finally { if (holdingLock) MarksBatchRunner.Release(conn, PublishLockName); }

            // Chunks that already committed are kept even if a later one failed, so partial
            // progress is never silently thrown away.
            string finalStatus = failure != null ? (affected > 0 ? "COMMITTED" : "FAILED") : (affected > 0 ? "COMMITTED" : "CANCELLED");
            var sum = new Dictionary<string, object>();
            sum["fromStage"] = def.FromStage; sum["toStage"] = def.ToStage; sum["mode"] = "SELECTED";
            sum["selected"] = ids.Length; sum["advanced"] = affected; sum["skipped"] = skipped;
            if (skipReasons.Count > 0) sum["skipReasons"] = skipReasons;
            if (failure != null) sum["error"] = failure;

            using (var fin = new MySqlCommand("UPDATE campus_dynamics_portal." + def.RecordTable +
                " SET status=@st, committed_at=NOW(), affected_count=@n, summary_json=@sum," +
                " progress_done=@n, progress_total=@tot, last_error=@err, heartbeat_at=NOW() WHERE id=@id", conn))
            {
                fin.Parameters.AddWithValue("@st", finalStatus);
                fin.Parameters.AddWithValue("@n", affected);
                fin.Parameters.AddWithValue("@tot", ids.Length);
                fin.Parameters.AddWithValue("@sum", Json.Serialize(sum));
                fin.Parameters.AddWithValue("@err", (object)failure ?? DBNull.Value);
                fin.Parameters.AddWithValue("@id", recordId);
                fin.ExecuteNonQuery();
            }

            if (failure != null)
                return new { success = affected > 0, affected = affected, skipped = skipped, partial = affected > 0,
                             message = "Stopped after " + affected + " of " + ids.Length + " mark(s): " + failure };

            if (affected == 0)
                return new { success = false, affected = 0, skipped = skipped, skipReasons = skipReasons,
                             message = "No eligible marks were advanced — they may already have moved on, or are outside your scope." };

            return new { success = true, affected = affected, skipped = skipped, recordId = recordId,
                         skipReasons = skipReasons,
                         message = affected + " mark(s) advanced to " + MarkStage.Label(def.ToStage) + "." + (skipped > 0 ? (" " + skipped + " skipped.") : "") };
        }
    }

    /// <summary>Tally a skip reason so the console can explain a shortfall instead of just showing a smaller number.</summary>
    private static void Bump(Dictionary<string, int> d, string key)
    {
        if (string.IsNullOrEmpty(key)) key = "Unknown";
        d[key] = (d.ContainsKey(key) ? d[key] : 0) + 1;
    }

    private static int ToI(object v) { return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v); }
    private static string S(object v) { return v == null || v == DBNull.Value ? "" : v.ToString(); }
}
