using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using MySql.Data.MySqlClient;

/// <summary>
/// Student Course Rearrangement — reversal, the log reader and the dashboard.
///
/// Reversal restores the before-snapshot, but only after checking that the record still looks
/// the way this module left it. If someone has worked on it since, the reversal is refused and
/// says exactly which fields differ — overwriting newer work silently would be worse than the
/// mistake being undone.
///
/// A reversal is itself an ordinary log entry, so it can be reversed in turn. Nothing is ever
/// stamped onto the original row: the log is append only, and "has been reversed" is derived
/// from the existence of a later entry whose reverses_log_id points at it.
/// </summary>
public static partial class StudentRearrangeService
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Reversal
    // ═════════════════════════════════════════════════════════════════════════

    public static string ReverseEntry(long logId, string reason)
    {
        return DoReverse(new List<long> { logId }, 0, reason, "entry");
    }

    /// <summary>Reverses everything a whole SITTING did — which may be several saves — newest
    /// first, in one transaction. The brief asks for the session, not the batch, and a session
    /// can hold more than one save.</summary>
    public static string ReverseSession(long sessionId, string reason)
    {
        if (!CanUse()) { NoteAttempt("reverse", "DENIED", "Role " + ActorRole(), null, sessionId); return Err("Not permitted."); }
        var ids = new List<long>();
        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            // Skip anything already undone, so re-running after a partial reversal is harmless.
            using (var cmd = Cmd("SELECT l.id FROM campus_dynamics.acad_rearrange_log l " +
                                 "WHERE l.session_id=@s AND l.op_type NOT IN ('RECALC','REVERSAL') " +
                                 "  AND NOT EXISTS(SELECT 1 FROM campus_dynamics.acad_rearrange_log x " +
                                 "                 WHERE x.reverses_log_id=l.id) " +
                                 "ORDER BY l.id DESC", c, null))
            {
                cmd.Parameters.AddWithValue("@s", sessionId);
                using (var r = cmd.ExecuteReader()) while (r.Read()) ids.Add(Convert.ToInt64(r[0]));
            }
        }
        if (ids.Count == 0) return Err("That session has nothing left to reverse.");
        return DoReverse(ids, 0, reason, "session");
    }

    /// <summary>Reverses everything a single save did, newest first, in one transaction.</summary>
    public static string ReverseBatch(long batchId, string reason)
    {
        if (!CanUse()) { NoteAttempt("reverse", "DENIED", "Role " + ActorRole(), null, 0); return Err("Not permitted."); }
        var ids = new List<long>();
        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            using (var cmd = Cmd("SELECT id FROM campus_dynamics.acad_rearrange_log " +
                                 "WHERE batch_id=@b AND op_type<>'RECALC' ORDER BY id DESC", c, null))
            {
                cmd.Parameters.AddWithValue("@b", batchId);
                using (var r = cmd.ExecuteReader()) while (r.Read()) ids.Add(Convert.ToInt64(r[0]));
            }
        }
        if (ids.Count == 0) return Err("That batch has nothing that can be reversed.");
        return DoReverse(ids, batchId, reason, "batch");
    }

    private static string DoReverse(List<long> logIds, long sourceBatchId, string reason, string kind)
    {
        if (!CanUse())
        {
            NoteAttempt("reverse", "DENIED", "Role " + ActorRole() + " is not permitted", null, 0);
            return Err("You do not have permission to reverse rearrangements.");
        }
        reason = (reason ?? "").Trim();
        if (reason.Length < MinOpReason)
        {
            NoteAttempt("reverse", "VALIDATION", "Reason too short", null, 0);
            return Err("A reversal needs its own typed reason of at least " + MinOpReason + " characters.");
        }

        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            SetAuditContext(c, null, "Reversal: " + reason);

            using (var t = c.BeginTransaction())
            {
                try
                {
                    var entries = new List<Dictionary<string, object>>();
                    foreach (long id in logIds)
                    {
                        var e = ReadLogRow(c, t, id);
                        if (e == null) { t.Rollback(); return Err("Log entry " + id + " was not found."); }
                        entries.Add(e);
                    }

                    // Already reversed? Derived, never stamped.
                    foreach (var e in entries)
                    {
                        long id = Convert.ToInt64(e["id"]);
                        using (var cmd = Cmd("SELECT COUNT(*) FROM campus_dynamics.acad_rearrange_log " +
                                             "WHERE reverses_log_id=@i", c, t))
                        {
                            cmd.Parameters.AddWithValue("@i", id);
                            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                            {
                                t.Rollback();
                                NoteAttempt("reverse", "BLOCKED", "Entry " + id + " already reversed", null, 0);
                                return Err("Log entry " + id + " has already been reversed. " +
                                           "Reverse the reversal instead if you want it back.");
                            }
                        }
                    }

                    string regno = Convert.ToString(entries[0]["regno"]);
                    var sess = ReverseSession(c, t, regno, reason);
                    long batchId = InsertBatch(c, t, sess, "rev-" + Guid.NewGuid().ToString("N"), entries.Count);
                    using (var cmd = Cmd("UPDATE campus_dynamics.acad_rearrange_batch " +
                                         "SET status='REVERSAL', reverses_batch_id=@o WHERE id=@r", c, t))
                    {
                        cmd.Parameters.AddWithValue("@o", sourceBatchId > 0 ? (object)sourceBatchId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@r", batchId);
                        cmd.ExecuteNonQuery();
                    }

                    int seq = 0;
                    var done = new List<object>();
                    foreach (var e in entries)
                    {
                        seq++;
                        string err;
                        string summary = ReverseOne(c, t, sess, batchId, seq, e, reason, out err);
                        if (err != null)
                        {
                            t.Rollback();
                            NoteAttempt("reverse", "BLOCKED", err, regno, 0);
                            return Json.Serialize(new { success = false, message = err });
                        }
                        done.Add(new { logId = Convert.ToInt64(e["id"]), summary });
                    }

                    double cgpaBefore = Cgpa(c, t, regno);
                    var gpaMoves = Recalculate(c, t, regno);
                    double cgpaAfter = Cgpa(c, t, regno);
                    LogEntry(c, t, sess, batchId, ++seq, "RECALC", "campus_dynamics", "acad_results", "regno",
                             regno, null, Json.Serialize(new { cgpa = cgpaBefore }),
                             Json.Serialize(new { cgpa = cgpaAfter, semesterGpa = gpaMoves }),
                             "Recalculated after reversal", false, null, null, null);

                    var result = new
                    {
                        success = true,
                        reversed = entries.Count,
                        changes = done,
                        recalculated = new { cgpaBefore, cgpaAfter, semesters = gpaMoves },
                        message = entries.Count + " change(s) reversed."
                    };
                    string rj = Json.Serialize(result);
                    using (var cmd = Cmd("UPDATE campus_dynamics.acad_rearrange_batch SET result_json=@j WHERE id=@i", c, t))
                    { cmd.Parameters.AddWithValue("@j", rj); cmd.Parameters.AddWithValue("@i", batchId); cmd.ExecuteNonQuery(); }

                    t.Commit();
                    return rj;
                }
                catch (Exception ex)
                {
                    try { t.Rollback(); } catch { }
                    NoteAttempt("reverse", "BLOCKED", ex.Message, null, 0);
                    return Err("Nothing was reversed: " + ex.Message);
                }
            }
        }
    }

    /// <summary>A reversal is work too, so it gets its own session row: same actor, same
    /// acknowledgement trail, reason recorded.</summary>
    private static SessionInfo ReverseSession(MySqlConnection c, MySqlTransaction t, string regno, string reason)
    {
        string sref = NextRef(c, t);
        long id;
        using (var cmd = Cmd(
            "INSERT INTO campus_dynamics.acad_rearrange_session " +
            "(session_ref, regno, student_name, prog_id, reason, acknowledged, acknowledged_text, " +
            " actor_user, actor_name, actor_role, actor_ip, actor_agent, opened_at, status) " +
            "VALUES (@ref,@rg,'','',@rs,1,@ak,@au,@an,@ar,@ip,@ua,NOW(),'SAVED')", c, t))
        {
            cmd.Parameters.AddWithValue("@ref", sref);
            cmd.Parameters.AddWithValue("@rg", regno);
            cmd.Parameters.AddWithValue("@rs", Trunc("Reversal: " + reason, 1000));
            cmd.Parameters.AddWithValue("@ak", AckText);
            cmd.Parameters.AddWithValue("@au", Actor());
            cmd.Parameters.AddWithValue("@an", ActorName());
            cmd.Parameters.AddWithValue("@ar", ActorRole());
            cmd.Parameters.AddWithValue("@ip", ClientIp());
            cmd.Parameters.AddWithValue("@ua", UserAgent());
            cmd.ExecuteNonQuery();
            id = cmd.LastInsertedId;
        }
        var s = new SessionInfo();
        s.id = id; s.sref = sref; s.regno = regno; s.reason = "Reversal: " + reason;
        s.actor = Actor(); s.status = "SAVED";
        return s;
    }

    private static Dictionary<string, object> ReadLogRow(MySqlConnection c, MySqlTransaction t, long id)
    {
        using (var cmd = Cmd(
            "SELECT id, session_id, batch_id, regno, op_type, db_name, table_name, pk_column, pk_value, " +
            " course_code, before_json, after_json FROM campus_dynamics.acad_rearrange_log WHERE id=@i LIMIT 1", c, t))
        {
            cmd.Parameters.AddWithValue("@i", id);
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                var d = new Dictionary<string, object>();
                d["id"] = Convert.ToInt64(r[0]); d["session_id"] = Convert.ToInt64(r[1]);
                d["batch_id"] = Convert.ToInt64(r[2]); d["regno"] = S(r, 3); d["op_type"] = S(r, 4);
                d["db_name"] = S(r, 5); d["table_name"] = S(r, 6); d["pk_column"] = S(r, 7);
                d["pk_value"] = S(r, 8); d["course_code"] = S(r, 9);
                d["before_json"] = r.IsDBNull(10) ? null : Convert.ToString(r[10]);
                d["after_json"] = r.IsDBNull(11) ? null : Convert.ToString(r[11]);
                return d;
            }
        }
    }

    private static string ReverseOne(MySqlConnection c, MySqlTransaction t, SessionInfo sess, long batchId,
                                     int seq, Dictionary<string, object> e, string reason, out string err)
    {
        err = null;
        long logId = Convert.ToInt64(e["id"]);
        string op = Convert.ToString(e["op_type"]);
        string db = Convert.ToString(e["db_name"]);
        string table = Convert.ToString(e["table_name"]);
        string pkCol = Convert.ToString(e["pk_column"]);
        string pkVal = Convert.ToString(e["pk_value"]);
        string course = Convert.ToString(e["course_code"]);
        string qualified = db + "." + table;

        if (op == "RECALC") return "Recalculation is derived; it is redone at the end of this reversal.";

        Dictionary<string, object> before = null, after = null;
        try
        {
            if (e["before_json"] != null) before = Json.Deserialize<Dictionary<string, object>>(Convert.ToString(e["before_json"]));
            if (e["after_json"] != null) after = Json.Deserialize<Dictionary<string, object>>(Convert.ToString(e["after_json"]));
        }
        catch { err = "Log entry " + logId + " has a snapshot that cannot be read, so it cannot be reversed."; return null; }

        var current = ReadRow(c, t, qualified, pkCol, pkVal);

        // ── ADD is undone by removing the row it created ──
        if (op == "ADD")
        {
            if (current == null) { err = "Log entry " + logId + ": the row it added is already gone."; return null; }
            string drift = Drift(after, current);
            if (drift != null) { err = "Log entry " + logId + " cannot be reversed: " + drift; return null; }
            LogEntry(c, t, sess, batchId, seq, "REVERSAL", db, table, pkCol, pkVal, course,
                     Json.Serialize(current), null, reason, false, null, null, null, logId);
            using (var cmd = Cmd("DELETE FROM " + qualified + " WHERE " + pkCol + "=@p", c, t))
            { cmd.Parameters.AddWithValue("@p", pkVal); cmd.ExecuteNonQuery(); }
            return "Removed " + (course ?? table) + " that had been added";
        }

        // ── REGISTER_SEMESTER: after_json wraps the row under "row" ──
        if (op == "REGISTER_SEMESTER")
        {
            if (current == null) { err = "Log entry " + logId + ": that semester registration is already gone."; return null; }
            LogEntry(c, t, sess, batchId, seq, "REVERSAL", db, table, pkCol, pkVal, course,
                     Json.Serialize(current), null, reason, false, null, null, null, logId);
            using (var cmd = Cmd("DELETE FROM " + qualified + " WHERE " + pkCol + "=@p", c, t))
            { cmd.Parameters.AddWithValue("@p", pkVal); cmd.ExecuteNonQuery(); }
            return "Removed the semester registration. NOTE: any fee billing raised with it is NOT reversed here.";
        }

        // ── DELETE is undone by putting the archived row back under its original key ──
        if (op == "DELETE")
        {
            if (current != null) { err = "Log entry " + logId + ": a row already occupies " + pkCol + "=" + pkVal + "."; return null; }
            if (before == null) { err = "Log entry " + logId + " has no archived row to restore."; return null; }
            if (!ReInsert(c, t, qualified, before))
            { err = "Log entry " + logId + ": the archived row could not be restored."; return null; }
            var restored = ReadRow(c, t, qualified, pkCol, pkVal);
            LogEntry(c, t, sess, batchId, seq, "REVERSAL", db, table, pkCol, pkVal, course,
                     null, Json.Serialize(restored), reason, false, null, null, null, logId);
            return "Restored " + (course ?? table) + " that had been removed";
        }

        // ── MOVE and MARK_CHANGE are undone by writing the before values back ──
        if (current == null) { err = "Log entry " + logId + ": the row it changed no longer exists."; return null; }
        if (before == null) { err = "Log entry " + logId + " has no before-snapshot."; return null; }

        string d2 = Drift(after, current);
        if (d2 != null) { err = "Log entry " + logId + " cannot be reversed: " + d2; return null; }

        var sets = new List<string>();
        var cmd2 = Cmd("", c, t);
        int i = 0;
        foreach (var kv in before)
        {
            if (string.Equals(kv.Key, pkCol, StringComparison.OrdinalIgnoreCase)) continue;
            sets.Add("`" + kv.Key + "`=@v" + i);
            cmd2.Parameters.AddWithValue("@v" + i, kv.Value ?? DBNull.Value);
            i++;
        }
        if (sets.Count == 0) { err = "Log entry " + logId + " has nothing to restore."; return null; }
        cmd2.CommandText = "UPDATE " + qualified + " SET " + string.Join(", ", sets.ToArray()) +
                           " WHERE " + pkCol + "=@pk";
        cmd2.Parameters.AddWithValue("@pk", pkVal);
        using (cmd2) cmd2.ExecuteNonQuery();

        var nowRow = ReadRow(c, t, qualified, pkCol, pkVal);
        LogEntry(c, t, sess, batchId, seq, "REVERSAL", db, table, pkCol, pkVal, course,
                 Json.Serialize(current), Json.Serialize(nowRow), reason, false, null, null, null, logId);
        return "Restored " + (course ?? table) + " to its earlier values";
    }

    /// <summary>Compares what this module left behind against what is there now. Returns a
    /// plain description of the first differences found, or null when the record is untouched.</summary>
    private static string Drift(Dictionary<string, object> expected, Dictionary<string, object> current)
    {
        if (expected == null || current == null) return null;
        var diffs = new List<string>();
        foreach (var kv in expected)
        {
            object nowv;
            if (!current.TryGetValue(kv.Key, out nowv)) continue;
            string a = kv.Value == null ? "" : Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
            string b = nowv == null ? "" : Convert.ToString(nowv, CultureInfo.InvariantCulture);
            if (a == b) continue;
            // DateTime round-trips through JSON in a different shape; compare as dates.
            DateTime da, dbb;
            if (DateTime.TryParse(a, out da) && DateTime.TryParse(b, out dbb) && da == dbb) continue;
            diffs.Add(kv.Key + " is now '" + b + "' but this entry left it as '" + a + "'");
            if (diffs.Count >= 4) break;
        }
        if (diffs.Count == 0) return null;
        return "the record has changed since — " + string.Join("; ", diffs.ToArray()) +
               ". Reversing would overwrite that newer work, so it has been stopped.";
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Logs
    // ═════════════════════════════════════════════════════════════════════════

    public static string Logs(string from, string to, string actor, string role, string regno,
                              string opType, string reversed, string q, int page, int pageSize)
    {
        if (!CanUse()) { NoteAttempt("logs", "DENIED", "Role " + ActorRole(), null, 0); return Err("Not permitted."); }
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var w = new StringBuilder(" WHERE 1=1 ");
        var ps = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(from)) { w.Append(" AND l.performed_at >= @from "); ps.Add(new MySqlParameter("@from", from + " 00:00:00")); }
        if (!string.IsNullOrEmpty(to))   { w.Append(" AND l.performed_at <= @to ");   ps.Add(new MySqlParameter("@to", to + " 23:59:59")); }
        if (!string.IsNullOrEmpty(actor)){ w.Append(" AND l.actor_user = @actor ");   ps.Add(new MySqlParameter("@actor", actor)); }
        if (!string.IsNullOrEmpty(role)) { w.Append(" AND l.actor_role = @role ");    ps.Add(new MySqlParameter("@role", role)); }
        if (!string.IsNullOrEmpty(regno)){ w.Append(" AND l.regno = @regno ");        ps.Add(new MySqlParameter("@regno", regno)); }
        if (!string.IsNullOrEmpty(opType)){ w.Append(" AND l.op_type = @op ");        ps.Add(new MySqlParameter("@op", opType)); }
        if (!string.IsNullOrEmpty(q))
        {
            w.Append(" AND (l.session_reason LIKE @q OR l.op_reason LIKE @q OR l.override_reason LIKE @q OR l.course_code LIKE @q) ");
            ps.Add(new MySqlParameter("@q", "%" + q + "%"));
        }
        // "Reversed" is derived, never stored.
        if (reversed == "1") w.Append(" AND EXISTS(SELECT 1 FROM campus_dynamics.acad_rearrange_log x WHERE x.reverses_log_id=l.id) ");
        else if (reversed == "0") w.Append(" AND NOT EXISTS(SELECT 1 FROM campus_dynamics.acad_rearrange_log x WHERE x.reverses_log_id=l.id) ");

        var rows = new List<object>();
        int total = 0;
        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            using (var cmd = Cmd("SELECT COUNT(*) FROM campus_dynamics.acad_rearrange_log l" + w, c, null))
            {
                foreach (var p in ps) cmd.Parameters.Add(Clone(p));
                total = Convert.ToInt32(cmd.ExecuteScalar());
            }
            using (var cmd = Cmd(
                "SELECT l.id, l.session_id, l.batch_id, l.regno, l.op_type, l.table_name, l.pk_value, l.course_code, " +
                " l.op_reason, l.session_reason, l.is_override, l.override_kind, l.override_reason, l.lock_status, " +
                " l.actor_user, l.actor_name, l.actor_role, l.actor_ip, " +
                " DATE_FORMAT(l.performed_at,'%d %b %Y %H:%i') at_txt, l.reverses_log_id, " +
                " EXISTS(SELECT 1 FROM campus_dynamics.acad_rearrange_log x WHERE x.reverses_log_id=l.id) is_reversed, " +
                " COALESCE(s.session_ref,'') sref " +
                "FROM campus_dynamics.acad_rearrange_log l " +
                "LEFT JOIN campus_dynamics.acad_rearrange_session s ON s.id=l.session_id" + w +
                " ORDER BY l.id DESC LIMIT @off,@ps", c, null))
            {
                foreach (var p in ps) cmd.Parameters.Add(Clone(p));
                cmd.Parameters.AddWithValue("@off", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@ps", pageSize);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        rows.Add(new
                        {
                            id = Convert.ToInt64(r[0]), sessionId = Convert.ToInt64(r[1]), batchId = Convert.ToInt64(r[2]),
                            regno = S(r, 3), opType = S(r, 4), table = S(r, 5), pk = S(r, 6), course = S(r, 7),
                            opReason = S(r, 8), sessionReason = S(r, 9), isOverride = I(r, 10) == 1,
                            overrideKind = S(r, 11), overrideReason = S(r, 12), lockStatus = S(r, 13),
                            actor = S(r, 14), actorName = S(r, 15), role = S(r, 16), ip = S(r, 17),
                            at = S(r, 18), reversesLogId = r.IsDBNull(19) ? 0 : Convert.ToInt64(r[19]),
                            isReversed = I(r, 20) == 1, sref = S(r, 21)
                        });
            }
        }
        int pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return Json.Serialize(new { success = true, rows, total, page, pages, pageSize });
    }

    private static MySqlParameter Clone(MySqlParameter p)
    { return new MySqlParameter(p.ParameterName, p.Value); }

    /// <summary>One entry with a readable field-by-field comparison, not a JSON dump.</summary>
    public static string LogDetail(long id)
    {
        if (!CanUse()) return Err("Not permitted.");
        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            var e = ReadLogRow(c, null, id);
            if (e == null) return Err("That log entry was not found.");

            Dictionary<string, object> before = null, after = null;
            try
            {
                if (e["before_json"] != null) before = Json.Deserialize<Dictionary<string, object>>(Convert.ToString(e["before_json"]));
                if (e["after_json"] != null) after = Json.Deserialize<Dictionary<string, object>>(Convert.ToString(e["after_json"]));
            }
            catch { }

            var fields = new List<object>();
            var keys = new List<string>();
            if (before != null) foreach (var k in before.Keys) if (!keys.Contains(k)) keys.Add(k);
            if (after != null) foreach (var k in after.Keys) if (!keys.Contains(k)) keys.Add(k);
            foreach (var k in keys)
            {
                object bv = before != null && before.ContainsKey(k) ? before[k] : null;
                object av = after != null && after.ContainsKey(k) ? after[k] : null;
                string bs = bv == null ? "" : Convert.ToString(bv, CultureInfo.InvariantCulture);
                string as_ = av == null ? "" : Convert.ToString(av, CultureInfo.InvariantCulture);
                fields.Add(new { field = k, before = bs, after = as_, changed = bs != as_ });
            }

            object meta = null;
            using (var cmd = Cmd(
                "SELECT l.id, l.regno, l.op_type, l.table_name, l.pk_value, l.course_code, l.op_reason, " +
                " l.session_reason, l.is_override, l.override_kind, l.override_reason, l.lock_status, " +
                " l.actor_user, l.actor_name, l.actor_role, l.actor_ip, l.actor_agent, " +
                " DATE_FORMAT(l.performed_at,'%d %b %Y %H:%i:%s'), l.batch_id, l.session_id, " +
                " COALESCE(s.session_ref,''), " +
                " EXISTS(SELECT 1 FROM campus_dynamics.acad_rearrange_log x WHERE x.reverses_log_id=l.id) " +
                "FROM campus_dynamics.acad_rearrange_log l " +
                "LEFT JOIN campus_dynamics.acad_rearrange_session s ON s.id=l.session_id WHERE l.id=@i", c, null))
            {
                cmd.Parameters.AddWithValue("@i", id);
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                        meta = new
                        {
                            id = Convert.ToInt64(r[0]), regno = S(r, 1), opType = S(r, 2), table = S(r, 3),
                            pk = S(r, 4), course = S(r, 5), opReason = S(r, 6), sessionReason = S(r, 7),
                            isOverride = I(r, 8) == 1, overrideKind = S(r, 9), overrideReason = S(r, 10),
                            lockStatus = S(r, 11), actor = S(r, 12), actorName = S(r, 13), role = S(r, 14),
                            ip = S(r, 15), agent = S(r, 16), at = S(r, 17),
                            batchId = Convert.ToInt64(r[18]), sessionId = Convert.ToInt64(r[19]),
                            sref = S(r, 20), isReversed = I(r, 21) == 1
                        };
            }
            return Json.Serialize(new { success = true, entry = meta, fields });
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Dashboard
    // ═════════════════════════════════════════════════════════════════════════

    public static string Dashboard(int days)
    {
        if (!CanUse()) { NoteAttempt("dashboard", "DENIED", "Role " + ActorRole(), null, 0); return Err("Not permitted."); }
        if (days < 1 || days > 365) days = 30;

        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();

            var byOp = new List<object>();
            using (var cmd = Cmd("SELECT op_type, COUNT(*) FROM campus_dynamics.acad_rearrange_log " +
                                 "WHERE performed_at >= DATE_SUB(NOW(), INTERVAL @d DAY) GROUP BY op_type ORDER BY 2 DESC", c, null))
            { cmd.Parameters.AddWithValue("@d", days);
              using (var r = cmd.ExecuteReader()) while (r.Read()) byOp.Add(new { opType = S(r, 0), count = I(r, 1) }); }

            var byActor = new List<object>();
            using (var cmd = Cmd("SELECT actor_user, COALESCE(actor_name,''), COALESCE(actor_role,''), COUNT(*) " +
                                 "FROM campus_dynamics.acad_rearrange_log " +
                                 "WHERE performed_at >= DATE_SUB(NOW(), INTERVAL @d DAY) " +
                                 "GROUP BY actor_user, actor_name, actor_role ORDER BY 4 DESC LIMIT 20", c, null))
            { cmd.Parameters.AddWithValue("@d", days);
              using (var r = cmd.ExecuteReader()) while (r.Read())
                  byActor.Add(new { actor = S(r, 0), name = S(r, 1), role = S(r, 2), count = I(r, 3) }); }

            var recent = new List<object>();
            using (var cmd = Cmd(
                "SELECT l.id, l.regno, l.op_type, COALESCE(l.course_code,''), COALESCE(l.actor_name, l.actor_user), " +
                " COALESCE(l.actor_role,''), DATE_FORMAT(l.performed_at,'%d %b %Y %H:%i'), " +
                " COALESCE(NULLIF(l.op_reason,''), l.session_reason), l.is_override " +
                "FROM campus_dynamics.acad_rearrange_log l " +
                "WHERE l.op_type<>'RECALC' ORDER BY l.id DESC LIMIT 25", c, null))
            { using (var r = cmd.ExecuteReader()) while (r.Read())
                  recent.Add(new { id = Convert.ToInt64(r[0]), regno = S(r, 1), opType = S(r, 2), course = S(r, 3),
                                   actor = S(r, 4), role = S(r, 5), at = S(r, 6), reason = S(r, 7),
                                   isOverride = I(r, 8) == 1 }); }

            var sessions = new List<object>();
            using (var cmd = Cmd(
                "SELECT s.id, s.session_ref, s.regno, COALESCE(s.student_name,''), " +
                " COALESCE(s.actor_name, s.actor_user), COALESCE(s.actor_role,''), " +
                " DATE_FORMAT(s.opened_at,'%d %b %Y %H:%i'), s.reason, s.status, s.ops_applied " +
                "FROM campus_dynamics.acad_rearrange_session s ORDER BY s.id DESC LIMIT 15", c, null))
            { using (var r = cmd.ExecuteReader()) while (r.Read())
                  sessions.Add(new { id = Convert.ToInt64(r[0]), sref = S(r, 1), regno = S(r, 2), student = S(r, 3),
                                     actor = S(r, 4), role = S(r, 5), at = S(r, 6), reason = S(r, 7),
                                     status = S(r, 8), ops = I(r, 9) }); }

            var blocked = new List<object>();
            using (var cmd = Cmd(
                "SELECT id, DATE_FORMAT(at_time,'%d %b %Y %H:%i'), COALESCE(actor_user,''), COALESCE(actor_role,''), " +
                " COALESCE(regno,''), action, outcome, COALESCE(detail,'') " +
                "FROM campus_dynamics.acad_rearrange_attempt ORDER BY id DESC LIMIT 25", c, null))
            { using (var r = cmd.ExecuteReader()) while (r.Read())
                  blocked.Add(new { id = Convert.ToInt64(r[0]), at = S(r, 1), actor = S(r, 2), role = S(r, 3),
                                    regno = S(r, 4), action = S(r, 5), outcome = S(r, 6), detail = S(r, 7) }); }

            int totalOps = 0, totalSessions = 0, totalOverrides = 0, totalReversals = 0;
            using (var cmd = Cmd(
                "SELECT (SELECT COUNT(*) FROM campus_dynamics.acad_rearrange_log WHERE performed_at >= DATE_SUB(NOW(), INTERVAL @d DAY) AND op_type<>'RECALC'), " +
                "       (SELECT COUNT(*) FROM campus_dynamics.acad_rearrange_session WHERE opened_at >= DATE_SUB(NOW(), INTERVAL @d DAY)), " +
                "       (SELECT COUNT(*) FROM campus_dynamics.acad_rearrange_log WHERE is_override=1 AND performed_at >= DATE_SUB(NOW(), INTERVAL @d DAY)), " +
                "       (SELECT COUNT(*) FROM campus_dynamics.acad_rearrange_log WHERE op_type='REVERSAL' AND performed_at >= DATE_SUB(NOW(), INTERVAL @d DAY))", c, null))
            {
                cmd.Parameters.AddWithValue("@d", days);
                using (var r = cmd.ExecuteReader())
                    if (r.Read()) { totalOps = I(r, 0); totalSessions = I(r, 1); totalOverrides = I(r, 2); totalReversals = I(r, 3); }
            }

            return Json.Serialize(new
            {
                success = true, days,
                actor = new { user = Actor(), name = ActorName(), role = ActorRole() },
                totals = new { ops = totalOps, sessions = totalSessions, overrides = totalOverrides, reversals = totalReversals },
                byOp, byActor, recent, sessions, blocked
            });
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Course picker, filter options, CSV export
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Courses available to add. The student's own programme curriculum first — that is the
    /// valid set — with a catalogue-wide fallback so a genuinely off-curriculum correction is
    /// still possible, clearly marked as such.
    ///
    /// Each row also says whether the student ALREADY holds that course in the destination
    /// term. The unique key would reject such an add anyway; telling the officer up front is
    /// better than letting them build a batch that fails at save time.
    /// </summary>
    public static string SearchCourses(string q, string progId, string regno, string acadYear, int semester)
    {
        if (!CanUse()) return Err("Not permitted.");
        q = (q ?? "").Trim();
        progId = (progId ?? "").Trim();
        regno = (regno ?? "").Trim();
        acadYear = (acadYear ?? "").Trim();
        var rows = new List<object>();
        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            using (var cmd = Cmd(
                "SELECT co.courseID, COALESCE(NULLIF(co.courseName,''),co.courseID) nm, " +
                "  COALESCE(co.CreditUnit,0) cu, " +
                "  COALESCE(pc.study_year,0) cy, COALESCE(pc.semester,0) cs, " +
                "  (pc.course_code IS NOT NULL) on_curriculum, " +
                // Held in the destination term - the same shape the unique key uses.
                "  EXISTS(SELECT 1 FROM campus_dynamics_portal.acad_course_registration hr " +
                "         WHERE hr.regno=@r AND hr.courseID=co.courseID " +
                "           AND hr.acad_year=@ay AND hr.semester=@sem) held_here, " +
                // Held anywhere at all - worth knowing before adding a second copy elsewhere.
                "  (SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration ha " +
                "   WHERE ha.regno=@r AND ha.courseID=co.courseID) held_any " +
                "FROM campus_dynamics.acad_course co " +
                "LEFT JOIN campus_dynamics.acad_programmecourses pc " +
                "       ON pc.course_code=co.courseID AND pc.progcode=@p " +
                "WHERE (@q='' OR co.courseID LIKE @like OR co.courseName LIKE @like) " +
                "ORDER BY held_here, (pc.course_code IS NULL), co.courseID LIMIT 40", c, null))
            {
                cmd.Parameters.AddWithValue("@p", progId);
                cmd.Parameters.AddWithValue("@r", regno);
                cmd.Parameters.AddWithValue("@ay", acadYear);
                cmd.Parameters.AddWithValue("@sem", semester);
                cmd.Parameters.AddWithValue("@q", q);
                cmd.Parameters.AddWithValue("@like", "%" + q + "%");
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        rows.Add(new
                        {
                            course = S(r, 0), title = S(r, 1),
                            creditUnits = r.IsDBNull(2) ? 0 : Convert.ToDouble(r[2]),
                            curriculum = new { year = I(r, 3), semester = I(r, 4) },
                            onCurriculum = I(r, 5) == 1,
                            heldHere = I(r, 6) == 1,
                            heldElsewhere = I(r, 7)
                        });
            }
        }
        return Json.Serialize(new { success = true, rows });
    }

    /// <summary>
    /// Finds a student by number, entry number or name, so the officer can open a session from
    /// what they actually have in front of them instead of looking a number up elsewhere first.
    ///
    /// Scoped exactly as OpenSession is: a HOD cannot find a student outside their department
    /// here any more than they could open one.
    /// </summary>
    public static string SearchStudents(string q)
    {
        if (!CanUse()) return Err("Not permitted.");
        q = (q ?? "").Trim();
        if (q.Length < 2) return Json.Serialize(new { success = true, rows = new List<object>() });

        MarksScope scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess) return Json.Serialize(new { success = true, rows = new List<object>() });

        var rows = new List<object>();
        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            using (var cmd = Cmd(
                "SELECT s.regno, COALESCE(NULLIF(TRIM(s.entryno),''), s.regno) eno, " +
                "  TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) nm, " +
                "  COALESCE(s.progid,'') prog, COALESCE(NULLIF(p.progname,''),s.progid) pn, " +
                "  COALESCE(s.stud_status,'') st, " +
                "  (SELECT COUNT(*) FROM campus_dynamics_portal.acad_course_registration cr " +
                "   WHERE cr.regno=s.regno) regs " +
                "FROM campus_dynamics.acad_student s " +
                "LEFT JOIN campus_dynamics.acad_programme p ON p.progcode=s.progid " +
                "WHERE (s.regno LIKE @like OR TRIM(IFNULL(s.entryno,'')) LIKE @like " +
                "   OR CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')) LIKE @like)" +
                scope.ProgFilter("s", "progid") +
                " ORDER BY s.regno LIMIT 15", c, null))
            {
                cmd.Parameters.AddWithValue("@like", "%" + q + "%");
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        rows.Add(new
                        {
                            regno = S(r, 0), entryno = S(r, 1), name = S(r, 2),
                            prog = S(r, 3), progName = S(r, 4), status = S(r, 5),
                            registrations = I(r, 6)
                        });
            }
        }
        return Json.Serialize(new { success = true, rows });
    }

    /// <summary>Distinct values actually present in the log, so the filter dropdowns only
    /// ever offer something that will return rows.</summary>
    public static string Filters()
    {
        if (!CanUse()) return Err("Not permitted.");
        var actors = new List<object>(); var roles = new List<string>(); var ops = new List<string>();
        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            using (var cmd = Cmd("SELECT DISTINCT actor_user, COALESCE(actor_name,'') FROM campus_dynamics.acad_rearrange_log ORDER BY 1 LIMIT 200", c, null))
            using (var r = cmd.ExecuteReader()) while (r.Read()) actors.Add(new { user = S(r, 0), name = S(r, 1) });
            using (var cmd = Cmd("SELECT DISTINCT COALESCE(actor_role,'') FROM campus_dynamics.acad_rearrange_log WHERE COALESCE(actor_role,'')<>'' ORDER BY 1", c, null))
            using (var r = cmd.ExecuteReader()) while (r.Read()) roles.Add(S(r, 0));
            using (var cmd = Cmd("SELECT DISTINCT op_type FROM campus_dynamics.acad_rearrange_log ORDER BY 1", c, null))
            using (var r = cmd.ExecuteReader()) while (r.Read()) ops.Add(S(r, 0));
        }
        return Json.Serialize(new { success = true, actors, roles, ops,
                                    actor = new { user = Actor(), name = ActorName(), role = ActorRole() } });
    }

    /// <summary>Exports the filtered set. Same WHERE the grid uses, so what downloads is what
    /// was on screen.</summary>
    public static void ExportCsv(System.Web.HttpResponse resp, System.Web.HttpRequest req)
    {
        if (!CanUse()) { resp.StatusCode = 403; resp.Write("Not permitted."); resp.End(); return; }

        string json = Logs(req.QueryString["from"], req.QueryString["to"], req.QueryString["actor"],
                           req.QueryString["role"], req.QueryString["regno"], req.QueryString["opType"],
                           req.QueryString["reversed"], req.QueryString["q"], 1, 5000);

        var sb = new StringBuilder("LogId,When,Student,Operation,Course,Table,RecordId,Actor,Name,Role,IP,Override,LockStatus,Reversed,OperationReason,SessionReason\r\n");
        try
        {
            var top = Json.Deserialize<Dictionary<string, object>>(json);
            var rows = top.ContainsKey("rows") ? top["rows"] as System.Collections.ArrayList : null;
            if (rows != null)
                foreach (var o in rows)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    sb.Append(Csv(GS(d, "id"))).Append(',').Append(Csv(GS(d, "at"))).Append(',')
                      .Append(Csv(GS(d, "regno"))).Append(',').Append(Csv(GS(d, "opType"))).Append(',')
                      .Append(Csv(GS(d, "course"))).Append(',').Append(Csv(GS(d, "table"))).Append(',')
                      .Append(Csv(GS(d, "pk"))).Append(',').Append(Csv(GS(d, "actor"))).Append(',')
                      .Append(Csv(GS(d, "actorName"))).Append(',').Append(Csv(GS(d, "role"))).Append(',')
                      .Append(Csv(GS(d, "ip"))).Append(',').Append(Csv(GS(d, "isOverride"))).Append(',')
                      .Append(Csv(GS(d, "lockStatus"))).Append(',').Append(Csv(GS(d, "isReversed"))).Append(',')
                      .Append(Csv(GS(d, "opReason"))).Append(',').Append(Csv(GS(d, "sessionReason")))
                      .Append("\r\n");
                }
        }
        catch (Exception ex) { sb.Append("export failed,").Append(Csv(ex.Message)).Append("\r\n"); }

        resp.Clear();
        resp.ContentType = "text/csv";
        resp.AddHeader("Content-Disposition",
            "attachment; filename=rearrangement_log_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv");
        resp.Write(sb.ToString());
        resp.End();
    }

    private static string Csv(string s)
    {
        if (s == null) return "";
        if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
