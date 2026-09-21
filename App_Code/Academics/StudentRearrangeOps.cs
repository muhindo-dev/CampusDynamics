using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using MySql.Data.MySqlClient;

/// <summary>
/// Student Course Rearrangement — the write path: save, reverse, logs, dashboard.
///
/// Everything a save does happens in ONE transaction on ONE connection. Both schemas live on
/// the same MySQL server, so the registration table (portal), the results tables (main), the
/// append-only log and — when the officer asks for it — the billing call are all covered by
/// the same commit. If the log write fails, the change it describes fails with it.
/// </summary>
public static partial class StudentRearrangeService
{
    // Applied in dependency order rather than the order the officer happened to click:
    // a semester must exist before a course can move into it, and a course must exist
    // before its marks can be changed. Deletes go last so nothing is removed that a
    // later operation still needs.
    private static readonly string[] OpOrder = { "REGSEM", "ADD", "MOVE", "MARK", "DELETE" };

    private static string GS(Dictionary<string, object> d, string k)
    {
        object v; if (d == null || !d.TryGetValue(k, out v) || v == null) return "";
        return Convert.ToString(v, CultureInfo.InvariantCulture).Trim();
    }
    private static int GI(Dictionary<string, object> d, string k)
    {
        string s = GS(d, k); int n;
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out n) ? n : 0;
    }
    private static bool GB(Dictionary<string, object> d, string k)
    {
        string s = GS(d, k).ToLowerInvariant();
        return s == "true" || s == "1" || s == "yes";
    }
    private static bool Has(Dictionary<string, object> d, string k)
    {
        object v; return d != null && d.TryGetValue(k, out v) && v != null && Convert.ToString(v).Length > 0;
    }

    // NCHE 2015, the same scale MarksControllerShared applies when it publishes. Duplicated
    // rather than shared only because the originals are private; the values are identical and
    // a divergence would show up immediately in the grade column.
    private static string GradeOf(int score)
    {
        if (score >= 80) return "A";  if (score >= 75) return "B+"; if (score >= 70) return "B";
        if (score >= 65) return "C+"; if (score >= 60) return "C";  if (score >= 55) return "D+";
        if (score >= 50) return "D";  return "F";
    }
    private static double PointOf(string grade)
    {
        switch ((grade ?? "").Trim().ToUpperInvariant())
        {
            case "A":  return 5.0; case "B+": return 4.5; case "B":  return 4.0;
            case "C+": return 3.5; case "C":  return 3.0; case "D+": return 2.5;
            case "D":  return 2.0; default:   return 0.0;
        }
    }

    private class OpResult
    {
        public string summary;      // plain language, for the review modal and the logs page
        public bool applied;
        public string error;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SAVE — one transaction, everything re-validated, nothing trusted
    // ═════════════════════════════════════════════════════════════════════════

    public static string Save(long sessionId, string clientOpId, string opsJson, string checksum)
    {
        if (!CanUse())
        {
            NoteAttempt("save", "DENIED", "Role " + ActorRole() + " is not permitted", null, sessionId);
            return Err("You do not have permission to save rearrangements.");
        }
        clientOpId = (clientOpId ?? "").Trim();
        if (clientOpId.Length < 8)
            return Err("The save could not be identified. Reload the page and try again.");

        List<object> raw;
        try { raw = Json.Deserialize<List<object>>(opsJson ?? "[]"); }
        catch { return Err("The list of changes could not be read."); }
        if (raw == null || raw.Count == 0) return Err("There are no changes to save.");

        var ops = new List<Dictionary<string, object>>();
        foreach (var o in raw)
        {
            var d = o as Dictionary<string, object>;
            if (d != null) ops.Add(d);
        }
        ops.Sort(delegate (Dictionary<string, object> a, Dictionary<string, object> b)
        {
            int ia = Array.IndexOf(OpOrder, GS(a, "op").ToUpperInvariant());
            int ib = Array.IndexOf(OpOrder, GS(b, "op").ToUpperInvariant());
            if (ia < 0) ia = 99; if (ib < 0) ib = 99;
            return ia.CompareTo(ib);
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();

            var sess = ReadSession(c, null, sessionId);
            if (sess == null) return Err("That rearrangement session was not found.");
            if (!string.Equals(sess.actor, Actor(), StringComparison.OrdinalIgnoreCase) && !RoleAccessService.IsAdmin())
            {
                NoteAttempt("save", "DENIED", "Session belongs to " + sess.actor, sess.regno, sessionId);
                return Err("That session belongs to another officer.");
            }

            // ── Idempotency. The unique index on client_op_id is the guarantee; a replay
            //    collides and replays the stored answer instead of doing the work twice. ──
            string replay = FindReplay(c, null, clientOpId);
            if (replay != null)
            {
                NoteAttempt("save", "DUPLICATE", "Replayed client_op_id " + clientOpId, sess.regno, sessionId);
                return replay;
            }

            SetAuditContext(c, null, "Rearrangement " + sess.sref + ": " + sess.reason);

            using (var t = c.BeginTransaction())
            {
                try
                {
                    // ── Optimistic lock. Anything else touching this student since the
                    //    workspace loaded invalidates the whole batch. ──
                    string now = CurrentChecksum(c, t, sess.regno);
                    if (!string.IsNullOrEmpty(checksum) && now != checksum)
                    {
                        t.Rollback();
                        NoteAttempt("save", "CONFLICT", "Checksum " + checksum + " != " + now, sess.regno, sessionId);
                        return Err("This student's record was changed by someone else while you were working. " +
                                   "Nothing has been saved. Reload the page to see the current record, then redo your changes.");
                    }

                    long batchId = InsertBatch(c, t, sess, clientOpId, ops.Count);

                    var done = new List<object>();
                    int seq = 0;
                    foreach (var op in ops)
                    {
                        seq++;
                        string kind = GS(op, "op").ToUpperInvariant();
                        OpResult r;
                        switch (kind)
                        {
                            case "REGSEM": r = DoRegisterSemester(c, t, sess, batchId, seq, op); break;
                            case "RETERM": r = DoReterm(c, t, sess, batchId, seq, op); break;
                            case "ADD":    r = DoAdd(c, t, sess, batchId, seq, op); break;
                            case "MOVE":   r = DoMove(c, t, sess, batchId, seq, op); break;
                            case "MARK":   r = DoMark(c, t, sess, batchId, seq, op); break;
                            case "DELETE": r = DoDelete(c, t, sess, batchId, seq, op); break;
                            default:       r = new OpResult { applied = false, error = "Unknown change type '" + kind + "'." }; break;
                        }

                        if (!r.applied)
                        {
                            // One failure rolls back the whole batch. The officer is told which
                            // change failed and why, by position, so they can find it.
                            t.Rollback();
                            NoteAttempt("save", "BLOCKED", "Change " + seq + ": " + r.error, sess.regno, sessionId);
                            return Json.Serialize(new
                            {
                                success = false,
                                failedAt = seq,
                                message = "Change " + seq + " of " + ops.Count + " could not be applied, so nothing was saved:\n\n" + r.error
                            });
                        }
                        done.Add(new { seq = seq, op = kind, summary = r.summary });
                    }

                    // ── Downstream recalculation, logged with old and new values ──
                    double cgpaBefore = Cgpa(c, t, sess.regno);
                    var gpaMoves = Recalculate(c, t, sess.regno);
                    double cgpaAfter = Cgpa(c, t, sess.regno);

                    LogEntry(c, t, sess, batchId, ++seq, "RECALC", "campus_dynamics", "acad_results", "regno",
                             sess.regno, null,
                             Json.Serialize(new { cgpa = cgpaBefore }),
                             Json.Serialize(new { cgpa = cgpaAfter, semesterGpa = gpaMoves }),
                             "Recalculated after " + ops.Count + " change(s)", false, null, null, null);

                    var result = new
                    {
                        success = true,
                        batchId = batchId,
                        applied = ops.Count,
                        changes = done,
                        recalculated = new { cgpaBefore, cgpaAfter, semesters = gpaMoves },
                        message = ops.Count + " change(s) saved."
                    };
                    string resultJson = Json.Serialize(result);

                    using (var cmd = Cmd("UPDATE campus_dynamics.acad_rearrange_batch " +
                                         "SET result_json=@j, ops_count=@n, duration_ms=@d WHERE id=@i", c, t))
                    {
                        cmd.Parameters.AddWithValue("@j", resultJson);
                        cmd.Parameters.AddWithValue("@n", ops.Count);
                        cmd.Parameters.AddWithValue("@d", (int)sw.ElapsedMilliseconds);
                        cmd.Parameters.AddWithValue("@i", batchId);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = Cmd("UPDATE campus_dynamics.acad_rearrange_session " +
                                         "SET status='SAVED', last_saved_at=NOW(), ops_applied=ops_applied+@n WHERE id=@i", c, t))
                    {
                        cmd.Parameters.AddWithValue("@n", ops.Count);
                        cmd.Parameters.AddWithValue("@i", sessionId);
                        cmd.ExecuteNonQuery();
                    }

                    t.Commit();
                    return resultJson;
                }
                catch (Exception ex)
                {
                    try { t.Rollback(); } catch { }
                    NoteAttempt("save", "BLOCKED", ex.Message, sess.regno, sessionId);
                    return Err("Nothing was saved. The database rejected the batch: " + ex.Message);
                }
            }
        }
    }

    private static string FindReplay(MySqlConnection c, MySqlTransaction t, string clientOpId)
    {
        using (var cmd = Cmd("SELECT result_json FROM campus_dynamics.acad_rearrange_batch " +
                             "WHERE client_op_id=@k LIMIT 1", c, t))
        {
            cmd.Parameters.AddWithValue("@k", clientOpId);
            object o = cmd.ExecuteScalar();
            if (o == null || o == DBNull.Value) return null;
            string s = Convert.ToString(o);
            return string.IsNullOrEmpty(s) ? Err("That save has already been applied.") : s;
        }
    }

    private static long InsertBatch(MySqlConnection c, MySqlTransaction t, SessionInfo sess,
                                    string clientOpId, int opsCount)
    {
        using (var cmd = Cmd(
            "INSERT INTO campus_dynamics.acad_rearrange_batch " +
            "(session_id, client_op_id, regno, actor_user, actor_role, actor_ip, applied_at, ops_count, status) " +
            "VALUES (@s,@k,@r,@u,@ro,@ip,NOW(),@n,'APPLIED')", c, t))
        {
            cmd.Parameters.AddWithValue("@s", sess.id);
            cmd.Parameters.AddWithValue("@k", clientOpId);
            cmd.Parameters.AddWithValue("@r", sess.regno);
            cmd.Parameters.AddWithValue("@u", Actor());
            cmd.Parameters.AddWithValue("@ro", ActorRole());
            cmd.Parameters.AddWithValue("@ip", ClientIp());
            cmd.Parameters.AddWithValue("@n", opsCount);
            cmd.ExecuteNonQuery();
            return cmd.LastInsertedId;
        }
    }

    /// <summary>The append-only log write. Same connection, same transaction as the change it
    /// describes — if this fails, the change fails.</summary>
    private static long LogEntry(MySqlConnection c, MySqlTransaction t, SessionInfo sess, long batchId,
                                 int seq, string opType, string db, string table, string pkCol, string pkVal,
                                 string course, string beforeJson, string afterJson, string opReason,
                                 bool isOverride, string overrideKind, string overrideReason, string lockStatus,
                                 long reversesLogId = 0)
    {
        using (var cmd = Cmd(
            "INSERT INTO campus_dynamics.acad_rearrange_log " +
            "(session_id, batch_id, op_seq, regno, op_type, db_name, table_name, pk_column, pk_value, course_code, " +
            " before_json, after_json, session_reason, op_reason, is_override, override_kind, override_reason, " +
            " lock_status, actor_user, actor_name, actor_role, actor_ip, actor_agent, performed_at, reverses_log_id) " +
            "VALUES (@s,@b,@q,@rg,@op,@db,@tb,@pc,@pv,@cc,@bj,@aj,@sr,@or,@io,@ok,@orr,@ls,@au,@an,@ar,@ip,@ua,NOW(),@rev)", c, t))
        {
            cmd.Parameters.AddWithValue("@s", sess.id);
            cmd.Parameters.AddWithValue("@b", batchId);
            cmd.Parameters.AddWithValue("@q", seq);
            cmd.Parameters.AddWithValue("@rg", sess.regno);
            cmd.Parameters.AddWithValue("@op", opType);
            cmd.Parameters.AddWithValue("@db", db);
            cmd.Parameters.AddWithValue("@tb", table);
            cmd.Parameters.AddWithValue("@pc", pkCol);
            cmd.Parameters.AddWithValue("@pv", pkVal);
            cmd.Parameters.AddWithValue("@cc", (object)course ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bj", (object)beforeJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@aj", (object)afterJson ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sr", Trunc(sess.reason, 1000));
            cmd.Parameters.AddWithValue("@or", (object)Trunc(opReason, 1000) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@io", isOverride ? 1 : 0);
            cmd.Parameters.AddWithValue("@ok", (object)overrideKind ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@orr", (object)Trunc(overrideReason, 1000) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ls", (object)lockStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@au", Actor());
            cmd.Parameters.AddWithValue("@an", ActorName());
            cmd.Parameters.AddWithValue("@ar", ActorRole());
            cmd.Parameters.AddWithValue("@ip", ClientIp());
            cmd.Parameters.AddWithValue("@ua", UserAgent());
            // Written at INSERT time, never stamped afterwards: the log's own trigger blocks
            // every UPDATE, which is exactly the guarantee we want and had to design around.
            cmd.Parameters.AddWithValue("@rev", reversesLogId > 0 ? (object)reversesLogId : DBNull.Value);
            cmd.ExecuteNonQuery();
            return cmd.LastInsertedId;
        }
    }

    /// <summary>Recomputes the load-time checksum from what is in the database right now.</summary>
    private static string CurrentChecksum(MySqlConnection c, MySqlTransaction t, string regno)
    {
        var rows = new List<CourseRow>();
        using (var cmd = Cmd(
            "SELECT cr.ID, cr.courseID, cr.acad_year, cr.semester, cr.course_status, " +
            "  COALESCE(cr.mark_stage,''), cr.provisional_course_work_marks, cr.provisional_exam_marks, " +
            "  cr.provisional_total_marks, COALESCE(rs.ID,0), COALESCE(rs.studyyear,0), rs.score, COALESCE(rs.grade,'') " +
            "FROM campus_dynamics_portal.acad_course_registration cr " +
            "LEFT JOIN campus_dynamics.acad_results rs ON rs.regno=cr.regno AND rs.courseid=cr.courseID " +
            "WHERE cr.regno=@r", c, t))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var cw = new CourseRow();
                    cw.regId = I(r, 0); cw.course = S(r, 1); cw.acadYear = S(r, 2); cw.semester = I(r, 3);
                    cw.courseStatus = S(r, 4); cw.markStage = S(r, 5);
                    cw.cw = NI(r, 6); cw.exam = NI(r, 7); cw.total = NI(r, 8);
                    cw.resultId = I(r, 9); cw.studyYear = I(r, 10); cw.score = NI(r, 11); cw.grade = S(r, 12);
                    rows.Add(cw);
                }
        }
        // The workspace fills a missing studyyear from the semester registration; the checksum
        // must be computed on the same basis or it would never match.
        var byTerm = new Dictionary<string, int>();
        using (var cmd = Cmd("SELECT acad_year, semester, studyyear FROM campus_dynamics.acad_registration " +
                             "WHERE regno=@r ORDER BY studyyear, semester", c, t))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string k = S(r, 0) + "|" + I(r, 1);
                    if (!byTerm.ContainsKey(k)) byTerm[k] = I(r, 2);
                }
        }
        foreach (var cw in rows)
        {
            if (cw.studyYear > 0) continue;
            int sy; if (byTerm.TryGetValue(cw.acadYear + "|" + cw.semester, out sy)) cw.studyYear = sy;
        }
        return Checksum(rows);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Shared validation
    // ═════════════════════════════════════════════════════════════════════════

    private class RegRow
    {
        public int id, semester, studyYear, resultId;
        public string course, acadYear, courseStatus, progId, markStage, grade;
        public object cw, exam, total, score;
    }

    private static RegRow LoadReg(MySqlConnection c, MySqlTransaction t, string regno, int regId)
    {
        using (var cmd = Cmd(
            "SELECT cr.ID, cr.courseID, cr.acad_year, cr.semester, cr.course_status, cr.prog_id, " +
            "  COALESCE(cr.mark_stage,''), cr.provisional_course_work_marks, cr.provisional_exam_marks, " +
            "  cr.provisional_total_marks, COALESCE(rs.ID,0), COALESCE(rs.studyyear,0), rs.score, COALESCE(rs.grade,'') " +
            "FROM campus_dynamics_portal.acad_course_registration cr " +
            "LEFT JOIN campus_dynamics.acad_results rs ON rs.regno=cr.regno AND rs.courseid=cr.courseID " +
            "WHERE cr.ID=@i AND cr.regno=@r LIMIT 1", c, t))
        {
            cmd.Parameters.AddWithValue("@i", regId);
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                var x = new RegRow();
                x.id = I(r, 0); x.course = S(r, 1); x.acadYear = S(r, 2); x.semester = I(r, 3);
                x.courseStatus = S(r, 4); x.progId = S(r, 5); x.markStage = S(r, 6);
                x.cw = NI(r, 7); x.exam = NI(r, 8); x.total = NI(r, 9);
                x.resultId = I(r, 10); x.studyYear = I(r, 11); x.score = NI(r, 12); x.grade = S(r, 13);
                return x;
            }
        }
    }

    private static string StatusOf(RegRow x)
    {
        try { return ResultsStatusService.GetStatus(x.course, x.progId, x.acadYear, x.semester, x.studyYear, 0, ""); }
        catch { return ResultsStatusService.STATUS_DRAFT; }
    }

    /// <summary>The results status gate. Blocked at or above SUBMITTED unless the officer is
    /// AR, Dean or super admin AND has typed an override reason. A HOD is never offered it.</summary>
    private static string CheckLock(RegRow x, Dictionary<string, object> op, out bool isOverride, out string status)
    {
        isOverride = false;
        status = StatusOf(x);
        if (status == ResultsStatusService.STATUS_DRAFT) return null;

        string ov = GS(op, "overrideReason");
        if (!CanOverrideLock())
            return "These results are at status " + status + " and are locked. Your role (" +
                   (ActorRole().Length > 0 ? ActorRole() : "unknown") +
                   ") may not override a results lock — only the Academic Registrar, a Dean or a system administrator may.";
        if (ov.Length < MinOverrideReason)
            return "These results are at status " + status + ". To override the lock you must type a reason of at least " +
                   MinOverrideReason + " characters (you typed " + ov.Length + ").";

        isOverride = true;
        return null;
    }

    /// <summary>
    /// Resolves the academic year of a (studyyear, semester) for this student.
    ///
    /// A semester the officer can SEE must be usable. The workspace draws a slot whenever
    /// anything sits in it, so a semester can be on screen holding courses while having no
    /// acad_registration row — and demanding that row refused moves into semesters that were
    /// visibly there, which is a worse answer than reading the year off what is already in the
    /// slot.
    ///
    /// The chain widens acad_GetResultsAcademicYear’s own fallback (registration, then
    /// transcript rows) with the other places the answer is already written down, and ends at a
    /// caller-supplied default so a move is never blocked for want of a label. Whichever step
    /// answers is reported back, because everything past the first is an inference and the log
    /// should say which one was used.
    /// </summary>
    private static string AcadYearOf(MySqlConnection c, MySqlTransaction t, string regno,
                                     int studyYear, int semester, string fallback, out string basis)
    {
        basis = "";
        string[][] probes = new string[][] {
            new string[] { "registration",
                "SELECT MIN(acad_year) FROM campus_dynamics.acad_registration " +
                "WHERE regno=@r AND studyyear=@y AND semester=@s AND IFNULL(acad_year,'') NOT IN ('','-')" },
            new string[] { "the published results already in that semester",
                "SELECT MIN(acad) FROM campus_dynamics.acad_results " +
                "WHERE regno=@r AND studyyear=@y AND semester=@s AND IFNULL(acad,'') NOT IN ('','-')" },
            new string[] { "the transcript rows for that semester",
                "SELECT MIN(acad) FROM campus_dynamics.acad_transcript_results " +
                "WHERE regno=@r AND studyyear=@y AND semester=@s AND IFNULL(acad,'') NOT IN ('','-')" },
            new string[] { "the other semesters in that year of study",
                "SELECT MIN(acad_year) FROM campus_dynamics.acad_registration " +
                "WHERE regno=@r AND studyyear=@y AND IFNULL(acad_year,'') NOT IN ('','-')" }
        };

        for (int i = 0; i < probes.Length; i++)
        {
            using (var cmd = Cmd(probes[i][1], c, t))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.Parameters.AddWithValue("@y", studyYear);
                cmd.Parameters.AddWithValue("@s", semester);
                object o = cmd.ExecuteScalar();
                string v = o == null || o == DBNull.Value ? "" : Convert.ToString(o).Trim();
                if (v.Length > 0) { basis = probes[i][0]; return v; }
            }
        }

        fallback = (fallback ?? "").Trim();
        if (fallback.Length > 0) { basis = "the course’s existing academic year"; return fallback; }
        return "";
    }

    private static bool SemesterIsClosed(MySqlConnection c, MySqlTransaction t, string regno, int studyYear, int semester)
    {
        // Closed == holds any FINAL_PUBLISHED result. No calendar was invented; that was the ruling.
        using (var cmd = Cmd(
            "SELECT COUNT(*) FROM campus_dynamics.acad_results " +
            "WHERE regno=@r AND studyyear=@y AND semester=@s", c, t))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            cmd.Parameters.AddWithValue("@y", studyYear);
            cmd.Parameters.AddWithValue("@s", semester);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) return false;
        }
        using (var cmd = Cmd(
            "SELECT cr.courseID, cr.prog_id, cr.acad_year, cr.semester " +
            "FROM campus_dynamics_portal.acad_course_registration cr " +
            "JOIN campus_dynamics.acad_results rs ON rs.regno=cr.regno AND rs.courseid=cr.courseID " +
            "WHERE cr.regno=@r AND rs.studyyear=@y AND rs.semester=@s LIMIT 20", c, t))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            cmd.Parameters.AddWithValue("@y", studyYear);
            cmd.Parameters.AddWithValue("@s", semester);
            var probe = new List<string[]>();
            using (var r = cmd.ExecuteReader())
                while (r.Read()) probe.Add(new[] { S(r, 0), S(r, 1), S(r, 2), Convert.ToString(I(r, 3)) });
            foreach (var p in probe)
            {
                try
                {
                    if (ResultsStatusService.GetStatus(p[0], p[1], p[2], int.Parse(p[3]), studyYear, 0, "")
                        == ResultsStatusService.STATUS_FINAL_PUBLISHED) return true;
                }
                catch { }
            }
        }
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  The operations
    // ═════════════════════════════════════════════════════════════════════════

    // MRU runs semesters 1-3 and study years 1-5 (8 allows headroom for a long programme).
    // This checks the target is a term that can exist, which is a far lighter test than
    // demanding the student already holds it.
    private const int MaxSemester = 3;
    private const int MaxStudyYear = 8;

    private static string TermShapeError(int studyYear, int semester)
    {
        if (studyYear < 1 || studyYear > MaxStudyYear)
            return "Year " + studyYear + " is not a year of study this institution runs (1 to " +
                   MaxStudyYear + ").";
        if (semester < 1 || semester > MaxSemester)
            return "Semester " + semester + " does not exist here — semesters run 1 to " +
                   MaxSemester + ".";
        return null;
    }

    private static OpResult DoMove(MySqlConnection c, MySqlTransaction t, SessionInfo sess,
                                   long batchId, int seq, Dictionary<string, object> op)
    {
        var res = new OpResult();
        int regId = GI(op, "regId"), toYear = GI(op, "toYear"), toSem = GI(op, "toSem");
        string reason = GS(op, "reason");

        var x = LoadReg(c, t, sess.regno, regId);
        if (x == null) { res.error = "That course registration is no longer on the student's record."; return res; }
        if (toYear <= 0 || toSem <= 0) { res.error = "The destination year and semester are missing."; return res; }
        string shape = TermShapeError(toYear, toSem);
        if (shape != null) { res.error = shape; return res; }
        if (x.studyYear == toYear && x.semester == toSem)
        {
            // Nothing to do is not the same as a failure. Failing here threw away every other
            // change in the batch because one instruction happened to be redundant.
            res.applied = true;
            res.summary = x.course + " was already in Year " + toYear + " Semester " + toSem + " — left as it is";
            return res;
        }

        bool isOverride; string lockStatus;
        string lockErr = CheckLock(x, op, out isOverride, out lockStatus);
        if (lockErr != null) { res.error = lockErr; return res; }

        // Last resort is the course’s own academic year: moving between semesters of the same
        // year of study keeps it, and it is never worse than refusing the move outright.
        string yearBasis;
        string toAcad = AcadYearOf(c, t, sess.regno, toYear, toSem, x.acadYear, out yearBasis);
        if (toAcad.Length == 0)
        {
            res.error = "Nothing on this student’s record says which academic year Year " + toYear +
                        " Semester " + toSem + " is. Register that semester in this sitting, then move " +
                        x.course + " into it.";
            return res;
        }

        // acad_course_registration is UNIQUE(regno, courseID, acad_year, semester, course_status)
        using (var cmd = Cmd(
            "SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration " +
            "WHERE regno=@r AND courseID=@c AND acad_year=@a AND semester=@s AND course_status=@st AND ID<>@i", c, t))
        {
            cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@c", x.course);
            cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@s", toSem);
            cmd.Parameters.AddWithValue("@st", x.courseStatus); cmd.Parameters.AddWithValue("@i", x.id);
            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            {
                res.error = "The student already holds " + x.course + " (" + x.courseStatus + ") in " +
                            toAcad + " Semester " + toSem + ", and a student cannot hold the same course " +
                            "twice in one term. Remove or move the copy already sitting there first, in " +
                            "this same sitting, and both changes will be saved together.";
                return res;
            }
        }

        var beforeReg = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", x.id);

        using (var cmd = Cmd("UPDATE campus_dynamics_portal.acad_course_registration " +
                             "SET acad_year=@a, semester=@s WHERE ID=@i", c, t))
        {
            cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@s", toSem);
            cmd.Parameters.AddWithValue("@i", x.id);
            cmd.ExecuteNonQuery();
        }
        var afterReg = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", x.id);

        LogEntry(c, t, sess, batchId, seq, "MOVE", "campus_dynamics_portal", "acad_course_registration", "ID",
                 Convert.ToString(x.id), x.course, Json.Serialize(beforeReg), Json.Serialize(afterReg),
                 reason, isOverride, isOverride ? "STATUS_LOCK" : null,
                 isOverride ? GS(op, "overrideReason") : null, lockStatus);

        // The published result and the transcript snapshot carry the term too. The retake flag
        // is deliberately untouched: a move never converts a retake into a first sitting.
        if (x.resultId > 0)
        {
            var b2 = ReadRow(c, t, "campus_dynamics.acad_results", "ID", x.resultId);
            using (var cmd = Cmd("UPDATE campus_dynamics.acad_results " +
                                 "SET acad=@a, semester=@s, studyyear=@y WHERE ID=@i", c, t))
            {
                cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@s", toSem);
                cmd.Parameters.AddWithValue("@y", toYear); cmd.Parameters.AddWithValue("@i", x.resultId);
                cmd.ExecuteNonQuery();
            }
            var a2 = ReadRow(c, t, "campus_dynamics.acad_results", "ID", x.resultId);
            LogEntry(c, t, sess, batchId, seq, "MOVE", "campus_dynamics", "acad_results", "ID",
                     Convert.ToString(x.resultId), x.course, Json.Serialize(b2), Json.Serialize(a2),
                     reason, isOverride, isOverride ? "STATUS_LOCK" : null,
                     isOverride ? GS(op, "overrideReason") : null, lockStatus);
        }

        var trIds = new List<int>();
        using (var cmd = Cmd("SELECT ID FROM campus_dynamics.acad_transcript_results " +
                             "WHERE regno=@r AND courseid=@c", c, t))
        {
            cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@c", x.course);
            using (var r = cmd.ExecuteReader()) while (r.Read()) trIds.Add(I(r, 0));
        }
        foreach (int tid in trIds)
        {
            var b3 = ReadRow(c, t, "campus_dynamics.acad_transcript_results", "ID", tid);
            using (var cmd = Cmd("UPDATE campus_dynamics.acad_transcript_results " +
                                 "SET acad=@a, semester=@s, studyyear=@y WHERE ID=@i", c, t))
            {
                cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@s", toSem);
                cmd.Parameters.AddWithValue("@y", toYear); cmd.Parameters.AddWithValue("@i", tid);
                cmd.ExecuteNonQuery();
            }
            var a3 = ReadRow(c, t, "campus_dynamics.acad_transcript_results", "ID", tid);
            LogEntry(c, t, sess, batchId, seq, "MOVE", "campus_dynamics", "acad_transcript_results", "ID",
                     Convert.ToString(tid), x.course, Json.Serialize(b3), Json.Serialize(a3),
                     reason, isOverride, null, null, lockStatus);
        }

        res.applied = true;
        res.summary = x.course + " moved from Year " + x.studyYear + " Semester " + x.semester +
                      " to Year " + toYear + " Semester " + toSem + " (" + toAcad +
                      (yearBasis == "registration" ? "" : ", academic year taken from " + yearBasis) + ")" +
                      (isOverride ? " (results lock overridden at " + lockStatus + ")" : "");
        return res;
    }

    private static OpResult DoMark(MySqlConnection c, MySqlTransaction t, SessionInfo sess,
                                   long batchId, int seq, Dictionary<string, object> op)
    {
        var res = new OpResult();
        int regId = GI(op, "regId");
        string field = GS(op, "field").ToLowerInvariant();
        string reason = GS(op, "reason");

        if (reason.Length < MinOpReason)
        { res.error = "A mark change needs its own typed reason of at least " + MinOpReason + " characters."; return res; }
        if (field != "cw" && field != "exam")
        { res.error = "Only the coursework and exam marks can be changed here."; return res; }

        var x = LoadReg(c, t, sess.regno, regId);
        if (x == null) { res.error = "That course registration is no longer on the student's record."; return res; }

        bool isOverride; string lockStatus;
        string lockErr = CheckLock(x, op, out isOverride, out lockStatus);
        if (lockErr != null) { res.error = lockErr; return res; }

        int? newVal = null;
        if (Has(op, "value"))
        {
            int v = GI(op, "value");
            // MRU enters marks already on their final scale: coursework out of 40, exam out of 60.
            int cap = field == "cw" ? 40 : 60;
            if (v < 0 || v > cap)
            { res.error = (field == "cw" ? "Coursework" : "Exam") + " must be between 0 and " + cap + ". You entered " + v + "."; return res; }
            newVal = v;
        }

        var before = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", x.id);
        object oldVal = field == "cw" ? x.cw : x.exam;

        string col = field == "cw" ? "provisional_course_work_marks" : "provisional_exam_marks";
        using (var cmd = Cmd("UPDATE campus_dynamics_portal.acad_course_registration SET " + col + "=@v, " +
                             "provisional_total_marks = COALESCE(" +
                             (field == "cw" ? "@v" : "provisional_course_work_marks") + ",0) + COALESCE(" +
                             (field == "cw" ? "provisional_exam_marks" : "@v") + ",0) " +
                             "WHERE ID=@i", c, t))
        {
            cmd.Parameters.AddWithValue("@v", newVal.HasValue ? (object)newVal.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@i", x.id);
            cmd.ExecuteNonQuery();
        }
        var after = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", x.id);

        LogEntry(c, t, sess, batchId, seq, "MARK_CHANGE", "campus_dynamics_portal", "acad_course_registration",
                 "ID", Convert.ToString(x.id), x.course, Json.Serialize(before), Json.Serialize(after),
                 reason, isOverride, isOverride ? "STATUS_LOCK" : null,
                 isOverride ? GS(op, "overrideReason") : null, lockStatus);

        // A published result must follow the mark, or the transcript and the sheet disagree.
        if (x.resultId > 0)
        {
            int total = 0;
            object tv = after.ContainsKey("provisional_total_marks") ? after["provisional_total_marks"] : null;
            if (tv != null) total = Convert.ToInt32(tv);
            string grade = GradeOf(total);
            double pt = PointOf(grade);

            var b2 = ReadRow(c, t, "campus_dynamics.acad_results", "ID", x.resultId);
            using (var cmd = Cmd("UPDATE campus_dynamics.acad_results SET score=@s, grade=@g, gradept=@p WHERE ID=@i", c, t))
            {
                cmd.Parameters.AddWithValue("@s", total);
                cmd.Parameters.AddWithValue("@g", grade);
                cmd.Parameters.AddWithValue("@p", pt);
                cmd.Parameters.AddWithValue("@i", x.resultId);
                cmd.ExecuteNonQuery();
            }
            var a2 = ReadRow(c, t, "campus_dynamics.acad_results", "ID", x.resultId);
            LogEntry(c, t, sess, batchId, seq, "MARK_CHANGE", "campus_dynamics", "acad_results", "ID",
                     Convert.ToString(x.resultId), x.course, Json.Serialize(b2), Json.Serialize(a2),
                     reason, isOverride, isOverride ? "STATUS_LOCK" : null,
                     isOverride ? GS(op, "overrideReason") : null, lockStatus);
        }

        res.applied = true;
        res.summary = x.course + " " + (field == "cw" ? "coursework" : "exam") + " mark changed from " +
                      (oldVal == null ? "blank" : Convert.ToString(oldVal)) + " to " +
                      (newVal.HasValue ? newVal.Value.ToString() : "blank") + ", reason: " + reason +
                      (isOverride ? " (results lock overridden at " + lockStatus + ")" : "");
        return res;
    }

    private static OpResult DoDelete(MySqlConnection c, MySqlTransaction t, SessionInfo sess,
                                     long batchId, int seq, Dictionary<string, object> op)
    {
        var res = new OpResult();
        int regId = GI(op, "regId");
        string reason = GS(op, "reason");

        if (reason.Length < MinOpReason)
        { res.error = "Removing a course registration needs its own typed reason of at least " + MinOpReason + " characters."; return res; }

        var x = LoadReg(c, t, sess.regno, regId);
        if (x == null) { res.error = "That course registration is no longer on the student's record."; return res; }

        bool isOverride; string lockStatus;
        string lockErr = CheckLock(x, op, out isOverride, out lockStatus);
        if (lockErr != null) { res.error = lockErr; return res; }

        // A published result is its own guard, separate from the results-status lock: a row
        // can be unlocked (DRAFT) and still carry a published result. CheckLock says nothing
        // about that case, so this needs its own authorisation rather than borrowing one.
        string orphanOverride = GS(op, "overrideReason");
        if (x.resultId > 0)
        {
            if (!CanOverrideLock())
            {
                res.error = x.course + " has a published result (" + (x.score == null ? "no score" : Convert.ToString(x.score)) +
                            ", grade " + x.grade + "). Removing the registration would leave that result orphaned. " +
                            "Only the Academic Registrar, a Dean or a system administrator may do that.";
                return res;
            }
            if (orphanOverride.Length < MinOverrideReason)
            {
                res.error = x.course + " has a published result (" + (x.score == null ? "no score" : Convert.ToString(x.score)) +
                            ", grade " + x.grade + "). Removing the registration would leave that result orphaned. " +
                            "Type a reason of at least " + MinOverrideReason + " characters to authorise it.";
                return res;
            }
            isOverride = true;
            if (lockStatus == ResultsStatusService.STATUS_DRAFT) lockStatus = "ORPHAN_RESULT";
        }

        // "Soft delete" without a flag column: the complete row is written into the log — which
        // is append only — inside the same transaction that removes it, and reversal re-inserts
        // it under its original primary key. Nothing is lost and nothing existing was altered.
        var before = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", x.id);

        LogEntry(c, t, sess, batchId, seq, "DELETE", "campus_dynamics_portal", "acad_course_registration",
                 "ID", Convert.ToString(x.id), x.course, Json.Serialize(before), null,
                 reason, isOverride,
                 isOverride ? (lockStatus == "ORPHAN_RESULT" ? "ORPHAN_RESULT" : "STATUS_LOCK") : null,
                 isOverride ? orphanOverride : null, lockStatus);

        using (var cmd = Cmd("DELETE FROM campus_dynamics_portal.acad_course_registration WHERE ID=@i", c, t))
        { cmd.Parameters.AddWithValue("@i", x.id); cmd.ExecuteNonQuery(); }

        res.applied = true;
        res.summary = x.course + " registration removed from Year " + x.studyYear + " Semester " + x.semester +
                      ", reason: " + reason + " (archived and reversible)";
        return res;
    }

    private static OpResult DoAdd(MySqlConnection c, MySqlTransaction t, SessionInfo sess,
                                  long batchId, int seq, Dictionary<string, object> op)
    {
        var res = new OpResult();
        string course = GS(op, "course").ToUpperInvariant();
        int toYear = GI(op, "toYear"), toSem = GI(op, "toSem");
        string reason = GS(op, "reason");

        if (course.Length == 0) { res.error = "Choose a course to add."; return res; }
        if (toYear <= 0 || toSem <= 0) { res.error = "Choose the year and semester to add the course into."; return res; }
        string shapeA = TermShapeError(toYear, toSem);
        if (shapeA != null) { res.error = shapeA; return res; }

        string latest = "";
        using (var cmd = Cmd("SELECT MAX(acad_year) FROM campus_dynamics.acad_registration " +
                             "WHERE regno=@r AND IFNULL(acad_year,'') NOT IN ('','-')", c, t))
        {
            cmd.Parameters.AddWithValue("@r", sess.regno);
            object o = cmd.ExecuteScalar();
            latest = o == null || o == DBNull.Value ? "" : Convert.ToString(o).Trim();
        }

        string yearBasis;
        string toAcad = AcadYearOf(c, t, sess.regno, toYear, toSem, latest, out yearBasis);
        if (toAcad.Length == 0)
        {
            res.error = "Nothing on this student’s record says which academic year Year " + toYear +
                        " Semester " + toSem + " is. Register that semester first, then add the course.";
            return res;
        }

        string prog = "";
        using (var cmd = Cmd("SELECT COALESCE(progid,'') FROM campus_dynamics.acad_student WHERE regno=@r LIMIT 1", c, t))
        { cmd.Parameters.AddWithValue("@r", sess.regno); object o = cmd.ExecuteScalar(); prog = o == null ? "" : Convert.ToString(o); }

        using (var cmd = Cmd("SELECT COUNT(*) FROM campus_dynamics.acad_course WHERE courseID=@c", c, t))
        {
            cmd.Parameters.AddWithValue("@c", course);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            { res.error = course + " is not in the course catalogue."; return res; }
        }

        using (var cmd = Cmd(
            "SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration " +
            "WHERE regno=@r AND courseID=@c AND acad_year=@a AND semester=@s AND course_status='Normal'", c, t))
        {
            cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@c", course);
            cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@s", toSem);
            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            { res.error = "The student already holds " + course + " in " + toAcad + " Semester " + toSem + "."; return res; }
        }

        string sessName = "";
        using (var cmd = Cmd("SELECT COALESCE(stud_session,'') FROM campus_dynamics_portal.acad_course_registration " +
                             "WHERE regno=@r AND IFNULL(stud_session,'')<>'' LIMIT 1", c, t))
        { cmd.Parameters.AddWithValue("@r", sess.regno); object o = cmd.ExecuteScalar(); sessName = o == null ? "" : Convert.ToString(o); }

        long newId;
        using (var cmd = Cmd(
            "INSERT INTO campus_dynamics_portal.acad_course_registration " +
            "(regno, courseID, acad_year, semester, course_status, prog_id, stud_session) " +
            "VALUES (@r,@c,@a,@s,'Normal',@p,@ss)", c, t))
        {
            cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@c", course);
            cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@s", toSem);
            cmd.Parameters.AddWithValue("@p", prog); cmd.Parameters.AddWithValue("@ss", sessName);
            cmd.ExecuteNonQuery();
            newId = cmd.LastInsertedId;
        }
        var after = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", newId);

        LogEntry(c, t, sess, batchId, seq, "ADD", "campus_dynamics_portal", "acad_course_registration",
                 "ID", Convert.ToString(newId), course, null, Json.Serialize(after),
                 reason, false, null, null, null);

        res.applied = true;
        res.summary = course + " registered into Year " + toYear + " Semester " + toSem + " (" + toAcad +
                      (yearBasis == "registration" ? "" : ", academic year taken from " + yearBasis) + ")";
        return res;
    }

    private static OpResult DoRegisterSemester(MySqlConnection c, MySqlTransaction t, SessionInfo sess,
                                               long batchId, int seq, Dictionary<string, object> op)
    {
        var res = new OpResult();
        int toYear = GI(op, "toYear"), toSem = GI(op, "toSem");
        string acadYear = GS(op, "acadYear");
        string reason = GS(op, "reason");
        bool bill = GB(op, "bill");

        if (toYear <= 0 || toSem <= 0) { res.error = "Choose the year of study and semester to register."; return res; }
        string shapeR = TermShapeError(toYear, toSem);
        if (shapeR != null) { res.error = shapeR; return res; }
        if (acadYear.Length == 0) { res.error = "Choose the academic year for the new semester."; return res; }

        using (var cmd = Cmd("SELECT COUNT(*) FROM campus_dynamics.acad_registration " +
                             "WHERE regno=@r AND acad_year=@a AND semester=@s AND studyyear=@y", c, t))
        {
            cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@a", acadYear);
            cmd.Parameters.AddWithValue("@s", toSem); cmd.Parameters.AddWithValue("@y", toYear);
            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
            { res.error = "The student is already registered for " + acadYear + " Year " + toYear + " Semester " + toSem + "."; return res; }
        }

        long newId;
        try
        {
            // registeredBy carries the officer's username: acad_registration has a BEFORE INSERT
            // trigger that rejects blank, '-' and automatic-looking attribution outright.
            using (var cmd = Cmd(
                "INSERT INTO campus_dynamics.acad_registration " +
                "(regno, acad_year, semester, regstatus, studyyear, registeredBy) " +
                "VALUES (@r,@a,@s,'REGISTERED',@y,@u)", c, t))
            {
                cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@a", acadYear);
                cmd.Parameters.AddWithValue("@s", toSem); cmd.Parameters.AddWithValue("@y", toYear);
                cmd.Parameters.AddWithValue("@u", Actor());
                cmd.ExecuteNonQuery();
                newId = cmd.LastInsertedId;
            }
        }
        catch (MySqlException mex)
        {
            res.error = "The database refused the semester registration: " + mex.Message;
            return res;
        }

        var after = ReadRow(c, t, "campus_dynamics.acad_registration", "ID", newId);

        // Billing is never silent. It happens only because the officer ticked the box, and the
        // choice is recorded either way. It runs inside this transaction, so a billing failure
        // takes the whole batch down rather than leaving a half-billed semester.
        string billOutcome = "not requested";
        if (bill)
        {
            try
            {
                using (var cmd = Cmd("CALL campus_dynamics_accounts.fin_AutoBillOnRegistration(@r,@a,@s,@u)", c, t))
                {
                    cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@a", acadYear);
                    cmd.Parameters.AddWithValue("@s", toSem); cmd.Parameters.AddWithValue("@u", Actor());
                    cmd.ExecuteNonQuery();
                }
                billOutcome = "billing requested and applied";
            }
            catch (MySqlException mex)
            {
                res.error = "The semester registration was rolled back because billing failed: " + mex.Message;
                return res;
            }
        }

        LogEntry(c, t, sess, batchId, seq, "REGISTER_SEMESTER", "campus_dynamics", "acad_registration",
                 "ID", Convert.ToString(newId), null, null,
                 Json.Serialize(new { row = after, billing = billOutcome, billRequested = bill }),
                 reason, false, null, null, null);

        res.applied = true;
        res.summary = "Registered into " + acadYear + " Year " + toYear + " Semester " + toSem +
                      " (" + billOutcome + ")";
        return res;
    }

    // ══════════════════════════════════════════════════════════════════
    //  RETERM — the academic year a semester sits in
    // ══════════════════════════════════════════════════════════════════
    //
    // MOVE takes one course to another (year, semester) and reads the academic year off the
    // destination. This is the other axis: the (year, semester) stays put and the ACADEMIC YEAR
    // under it changes — for a whole semester at once, courses, results and transcript together.
    //
    // A semester and its courses carry the academic year in four places. Changing one and not
    // the rest is how a record ends up contradicting itself, so all four move in one
    // transaction or none of them do:
    //
    //   acad_registration.acad_year          the (studyyear, semester) -> year authority
    //   acad_course_registration.acad_year   every course sitting in that block
    //   acad_results.acad                    their published results
    //   acad_transcript_results.acad         the transcript snapshot
    //
    // Scope is "semester" or "year". A year of study normally sits inside ONE academic year,
    // so re-terming a single semester out of a year that has more than one leaves the year
    // straddling two — the caller is told, and can send scope=year to take the whole thing.
    //
    // What it deliberately does NOT touch: fees. fin_studentfeestracking rows are stamped with
    // their own acadyear, and re-stamping them is a finance decision, not a records one. The
    // billing that would be left behind is counted, reported and written into the log so the
    // gap is visible rather than silent.

    private static OpResult DoReterm(MySqlConnection c, MySqlTransaction t, SessionInfo sess,
                                     long batchId, int seq, Dictionary<string, object> op)
    {
        var res = new OpResult();
        int studyYear = GI(op, "studyYear"), semester = GI(op, "semester");
        string toAcad = GS(op, "toAcad");
        string scope  = GS(op, "scope").ToLowerInvariant() == "year" ? "year" : "semester";
        string reason = GS(op, "reason");

        if (studyYear <= 0) { res.error = "Which year of study is being re-termed was not supplied."; return res; }
        if (scope == "semester")
        {
            if (semester <= 0) { res.error = "Which semester is being re-termed was not supplied."; return res; }
            string shape = TermShapeError(studyYear, semester);
            if (shape != null) { res.error = shape; return res; }
        }
        if (toAcad.Length == 0) { res.error = "Choose the academic year to move this into."; return res; }

        // V1 — the target must be a real academic year, not a typed string.
        using (var cmd = Cmd("SELECT COUNT(*) FROM campus_dynamics.acad_acadyears WHERE acadyear=@a", c, t))
        {
            cmd.Parameters.AddWithValue("@a", toAcad);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            { res.error = "\"" + toAcad + "\" is not an academic year on file."; return res; }
        }

        // The semesters this touches, and the year each is in now.
        var blocks = new List<int[]>();          // semester
        var fromBySem = new Dictionary<int, string>();
        using (var cmd = Cmd(
            "SELECT semester, acad_year FROM campus_dynamics.acad_registration " +
            "WHERE regno=@r AND studyyear=@y " + (scope == "semester" ? "AND semester=@s " : "") +
            "ORDER BY semester", c, t))
        {
            cmd.Parameters.AddWithValue("@r", sess.regno);
            cmd.Parameters.AddWithValue("@y", studyYear);
            if (scope == "semester") cmd.Parameters.AddWithValue("@s", semester);
            using (var r = cmd.ExecuteReader())
                while (r.Read()) { int sm = I(r, 0); blocks.Add(new int[] { sm }); fromBySem[sm] = S(r, 1); }
        }
        if (blocks.Count == 0)
        {
            res.error = scope == "year"
                ? "Year " + studyYear + " has no semester registration to re-term."
                : "Year " + studyYear + " Semester " + semester + " has no semester registration, so there " +
                  "is no academic year on it to change. Register the semester first, in this same sitting.";
            return res;
        }

        // V2 — already there. Doing nothing is not a failure; refusing would throw away the
        // rest of the batch over a redundant instruction.
        bool anyDifferent = false;
        foreach (var b in blocks) if (fromBySem[b[0]] != toAcad) anyDifferent = true;
        if (!anyDifferent)
        {
            res.applied = true;
            res.summary = (scope == "year" ? "Year " + studyYear : "Year " + studyYear + " Semester " + semester) +
                          " was already in " + toAcad + " — left as it is";
            return res;
        }

        // V3 — the target must not already hold this (studyyear, semester) for this student.
        // acad_registration has no unique key, so nothing but this check stands in the way of a
        // duplicate semester registration.
        foreach (var b in blocks)
        {
            int sm = b[0];
            if (fromBySem[sm] == toAcad) continue;
            using (var cmd = Cmd("SELECT COUNT(*) FROM campus_dynamics.acad_registration " +
                                 "WHERE regno=@r AND acad_year=@a AND semester=@s AND studyyear=@y", c, t))
            {
                cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@a", toAcad);
                cmd.Parameters.AddWithValue("@s", sm); cmd.Parameters.AddWithValue("@y", studyYear);
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                {
                    res.error = "The student already has a registration for " + toAcad + " Year " + studyYear +
                                " Semester " + sm + ". Re-terming into it would create a second one for the " +
                                "same term. Remove or re-term that registration first, in this same sitting.";
                    return res;
                }
            }
        }

        // V4 — chronology. A later year of study may not land in an earlier academic year than
        // an earlier one, and vice versa. This is the contradiction that makes a transcript
        // read as nonsense, and it is cheap to catch here.
        string chrono = ChronologyError(c, t, sess.regno, studyYear, toAcad, scope, blocks);
        if (chrono != null) { res.error = chrono; return res; }

        // The courses that will travel, and the lock state of each.
        var regIds = new List<int>();
        foreach (var b in blocks)
        {
            int sm = b[0]; string fromA = fromBySem[sm];
            if (fromA == toAcad) continue;
            using (var cmd = Cmd("SELECT ID FROM campus_dynamics_portal.acad_course_registration " +
                                 "WHERE regno=@r AND acad_year=@a AND semester=@s", c, t))
            {
                cmd.Parameters.AddWithValue("@r", sess.regno);
                cmd.Parameters.AddWithValue("@a", fromA); cmd.Parameters.AddWithValue("@s", sm);
                using (var r = cmd.ExecuteReader()) while (r.Read()) regIds.Add(I(r, 0));
            }
        }

        // V5 — the unique key on acad_course_registration, checked for every travelling course
        // BEFORE anything is written, so the batch fails with a sentence rather than a
        // constraint violation halfway through.
        bool anyOverride = false; string worstLock = null;
        var rows = new List<RegRow>();
        var lockOf = new Dictionary<int, string>();   // RegRow has no lock field; CheckLock reports it
        foreach (int rid in regIds)
        {
            var x = LoadReg(c, t, sess.regno, rid);
            if (x == null) continue;
            rows.Add(x);

            using (var cmd = Cmd(
                "SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration " +
                "WHERE regno=@r AND courseID=@c AND acad_year=@a AND semester=@s AND course_status=@st AND ID<>@i", c, t))
            {
                cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@c", x.course);
                cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@s", x.semester);
                cmd.Parameters.AddWithValue("@st", x.courseStatus); cmd.Parameters.AddWithValue("@i", x.id);
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                {
                    res.error = "Re-terming into " + toAcad + " would collide on " + x.course + ": the student " +
                                "already holds it (" + x.courseStatus + ") in " + toAcad + " Semester " + x.semester +
                                ", and the same course cannot be held twice in one term. Deal with that copy " +
                                "first, in this same sitting.";
                    return res;
                }
            }

            // V6 — published results are locked exactly as they are for a move.
            bool ovr; string st2;
            string lockErr = CheckLock(x, op, out ovr, out st2);
            if (lockErr != null) { res.error = lockErr + " (" + x.course + ")"; return res; }
            lockOf[x.id] = st2;
            if (ovr) { anyOverride = true; worstLock = st2; }
        }

        // Billing that will be left behind. Counted before the change, while the old year is
        // still on the registration rows.
        int billRows = 0; double billValue = 0;
        try
        {
            foreach (var b in blocks)
            {
                int sm = b[0]; string fromA = fromBySem[sm];
                if (fromA == toAcad) continue;
                using (var cmd = Cmd(
                    "SELECT COUNT(*), IFNULL(SUM(amount),0) FROM campus_dynamics_accounts.fin_studentfeestracking " +
                    "WHERE regno=@r AND acadyear=@a AND semester=@s AND post_status='Posted'", c, t))
                {
                    cmd.Parameters.AddWithValue("@r", sess.regno);
                    cmd.Parameters.AddWithValue("@a", fromA); cmd.Parameters.AddWithValue("@s", sm);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) { billRows += I(r, 0); billValue += r.IsDBNull(1) ? 0 : Convert.ToDouble(r.GetValue(1)); }
                }
            }
        }
        catch { billRows = -1; }   // finance schema unreachable: say so rather than imply zero

        // ── apply ─────────────────────────────────────────────────────────────
        int semsMoved = 0, coursesMoved = 0, resultsMoved = 0, transcriptsMoved = 0;

        foreach (var b in blocks)
        {
            int sm = b[0]; string fromA = fromBySem[sm];
            if (fromA == toAcad) continue;

            long regRowId = 0;
            using (var cmd = Cmd("SELECT ID FROM campus_dynamics.acad_registration " +
                                 "WHERE regno=@r AND studyyear=@y AND semester=@s AND acad_year=@a LIMIT 1", c, t))
            {
                cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@y", studyYear);
                cmd.Parameters.AddWithValue("@s", sm); cmd.Parameters.AddWithValue("@a", fromA);
                object v = cmd.ExecuteScalar();
                if (v != null && v != DBNull.Value) regRowId = Convert.ToInt64(v);
            }
            if (regRowId == 0) continue;

            var beforeSem = ReadRow(c, t, "campus_dynamics.acad_registration", "ID", regRowId);
            using (var cmd = Cmd("UPDATE campus_dynamics.acad_registration SET acad_year=@a WHERE ID=@i", c, t))
            {
                cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@i", regRowId);
                cmd.ExecuteNonQuery();
            }
            var afterSem = ReadRow(c, t, "campus_dynamics.acad_registration", "ID", regRowId);
            LogEntry(c, t, sess, batchId, seq, "RETERM", "campus_dynamics", "acad_registration", "ID",
                     Convert.ToString(regRowId), null, Json.Serialize(beforeSem), Json.Serialize(afterSem),
                     reason, anyOverride, anyOverride ? "STATUS_LOCK" : null,
                     anyOverride ? GS(op, "overrideReason") : null, worstLock);
            semsMoved++;
        }

        foreach (var x in rows)
        {
            var b1 = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", x.id);
            using (var cmd = Cmd("UPDATE campus_dynamics_portal.acad_course_registration " +
                                 "SET acad_year=@a WHERE ID=@i", c, t))
            {
                cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@i", x.id);
                cmd.ExecuteNonQuery();
            }
            var a1 = ReadRow(c, t, "campus_dynamics_portal.acad_course_registration", "ID", x.id);
            LogEntry(c, t, sess, batchId, seq, "RETERM", "campus_dynamics_portal", "acad_course_registration",
                     "ID", Convert.ToString(x.id), x.course, Json.Serialize(b1), Json.Serialize(a1),
                     reason, anyOverride, anyOverride ? "STATUS_LOCK" : null,
                     anyOverride ? GS(op, "overrideReason") : null, LockOf(lockOf, x.id));
            coursesMoved++;

            if (x.resultId > 0)
            {
                var b2 = ReadRow(c, t, "campus_dynamics.acad_results", "ID", x.resultId);
                using (var cmd = Cmd("UPDATE campus_dynamics.acad_results SET acad=@a WHERE ID=@i", c, t))
                {
                    cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@i", x.resultId);
                    cmd.ExecuteNonQuery();
                }
                var a2 = ReadRow(c, t, "campus_dynamics.acad_results", "ID", x.resultId);
                LogEntry(c, t, sess, batchId, seq, "RETERM", "campus_dynamics", "acad_results", "ID",
                         Convert.ToString(x.resultId), x.course, Json.Serialize(b2), Json.Serialize(a2),
                         reason, anyOverride, null, null, LockOf(lockOf, x.id));
                resultsMoved++;
            }

            var trIds = new List<int>();
            using (var cmd = Cmd("SELECT ID FROM campus_dynamics.acad_transcript_results " +
                                 "WHERE regno=@r AND courseid=@c AND semester=@s", c, t))
            {
                cmd.Parameters.AddWithValue("@r", sess.regno); cmd.Parameters.AddWithValue("@c", x.course);
                cmd.Parameters.AddWithValue("@s", x.semester);
                using (var r = cmd.ExecuteReader()) while (r.Read()) trIds.Add(I(r, 0));
            }
            foreach (int tid in trIds)
            {
                var b3 = ReadRow(c, t, "campus_dynamics.acad_transcript_results", "ID", tid);
                using (var cmd = Cmd("UPDATE campus_dynamics.acad_transcript_results SET acad=@a WHERE ID=@i", c, t))
                {
                    cmd.Parameters.AddWithValue("@a", toAcad); cmd.Parameters.AddWithValue("@i", tid);
                    cmd.ExecuteNonQuery();
                }
                var a3 = ReadRow(c, t, "campus_dynamics.acad_transcript_results", "ID", tid);
                LogEntry(c, t, sess, batchId, seq, "RETERM", "campus_dynamics", "acad_transcript_results", "ID",
                         Convert.ToString(tid), x.course, Json.Serialize(b3), Json.Serialize(a3),
                         reason, anyOverride, null, null, LockOf(lockOf, x.id));
                transcriptsMoved++;
            }
        }

        string billNote = billRows < 0
            ? ", fee records could not be checked"
            : billRows == 0
                ? ", no fee records affected"
                : ", " + billRows + " fee record" + (billRows == 1 ? "" : "s") + " (UGX " +
                  billValue.ToString("#,##0") + ") stay under the old academic year and were NOT moved";

        LogEntry(c, t, sess, batchId, seq, "RETERM", "campus_dynamics", "acad_registration", "SUMMARY",
                 studyYear + "/" + (scope == "year" ? "*" : semester.ToString()), null, null,
                 Json.Serialize(new
                 {
                     scope, studyYear, semester = (scope == "year" ? 0 : semester),
                     toAcadYear = toAcad, semestersMoved = semsMoved, coursesMoved,
                     resultsMoved, transcriptsMoved,
                     feeRowsLeftBehind = billRows, feeValueLeftBehind = billValue
                 }),
                 reason, anyOverride, anyOverride ? "STATUS_LOCK" : null,
                 anyOverride ? GS(op, "overrideReason") : null, worstLock);

        res.applied = true;
        res.summary = (scope == "year" ? "Year " + studyYear + " (all " + semsMoved + " semesters)"
                                       : "Year " + studyYear + " Semester " + semester) +
                      " moved to " + toAcad + " — " + coursesMoved + " course" + (coursesMoved == 1 ? "" : "s") +
                      ", " + resultsMoved + " result" + (resultsMoved == 1 ? "" : "s") +
                      ", " + transcriptsMoved + " transcript row" + (transcriptsMoved == 1 ? "" : "s") +
                      billNote + (anyOverride ? " (results lock overridden at " + worstLock + ")" : "");
        return res;
    }

    /// <summary>
    /// Refuses a re-term that would put the student's years of study out of chronological
    /// order — Year 2 starting before Year 1, or Year 3 before Year 2.
    ///
    /// Academic years sort correctly as strings here because they are all "YYYY/YYYY", so the
    /// first four characters order them. Only the years of study either side are consulted:
    /// gaps (a dead year) are normal and are not an error, and neither is two years of study
    /// sharing one academic year, which happens whenever a student repeats.
    /// </summary>
    private static string LockOf(Dictionary<int, string> map, int id)
    {
        string v; return map.TryGetValue(id, out v) ? v : null;
    }

    private static string ChronologyError(MySqlConnection c, MySqlTransaction t, string regno,
                                          int studyYear, string toAcad, string scope, List<int[]> blocks)
    {
        string below = null, above = null; int belowY = 0, aboveY = 0;
        using (var cmd = Cmd(
            "SELECT studyyear, MIN(acad_year), MAX(acad_year) FROM campus_dynamics.acad_registration " +
            "WHERE regno=@r AND studyyear<>@y AND IFNULL(acad_year,'') NOT IN ('','-') " +
            "GROUP BY studyyear ORDER BY studyyear", c, t))
        {
            cmd.Parameters.AddWithValue("@r", regno); cmd.Parameters.AddWithValue("@y", studyYear);
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    int sy = I(r, 0);
                    if (sy < studyYear) { below = S(r, 2); belowY = sy; }          // nearest below: last wins
                    else if (above == null) { above = S(r, 1); aboveY = sy; }      // nearest above: first wins
                }
        }

        if (below != null && string.Compare(toAcad, below, StringComparison.Ordinal) < 0)
            return "That would put Year " + studyYear + " in " + toAcad + ", before Year " + belowY +
                   " which is in " + below + ". A later year of study cannot start in an earlier " +
                   "academic year. Re-term Year " + belowY + " first if the whole record has slipped.";

        if (above != null && string.Compare(toAcad, above, StringComparison.Ordinal) > 0)
            return "That would put Year " + studyYear + " in " + toAcad + ", after Year " + aboveY +
                   " which is in " + above + ". An earlier year of study cannot start in a later " +
                   "academic year. Re-term Year " + aboveY + " first if the whole record has slipped.";

        return null;
    }
}
