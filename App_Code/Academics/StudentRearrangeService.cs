using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Student Course Rearrangement — all server logic.
///
/// The shape of this module is set by four facts about the data, each read from the live
/// database rather than assumed:
///
///  1. The transcript groups on STUDY YEAR then SEMESTER. The academic year is derived, by
///     acad_GetResultsAcademicYear(regno, studyyear, semester), from acad_registration. So a
///     "move" is a change of (studyyear, semester), and the academic year follows from the
///     semester registration — which is why a move into a semester the student does not hold
///     is refused rather than guessed at.
///
///  2. acad_course_registration is UNIQUE(regno, courseID, acad_year, semester, course_status)
///     but acad_results is UNIQUE(regno, courseid) with no term at all. A move the first table
///     accepts, the second can reject. Both are checked before either is written.
///
///  3. acad_results.gpa is a CACHED per-row semester GPA on all 648k rows, and
///     acad_transcript_results is a derived snapshot. Both go stale on a move and are
///     recomputed here, using the same expression the credit-units backfill used.
///
///  4. acad_registration carries a BEFORE INSERT trigger that rejects unattributed or
///     automatic inserts, so a semester registration made here carries the officer's username.
///
/// Reversal, snapshots and the cross-database transaction are CourseCorrectionService's, not a
/// second implementation: one reversal path in the system. This service owns the session, the
/// per-student workspace, the append-only log and the recalculation.
/// </summary>
public static partial class StudentRearrangeService
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public const string SlugManage    = "academics.rearrange.manage";
    public const string SlugLogs      = "academics.rearrange.logs";
    public const string SlugDashboard = "academics.rearrange.dashboard";

    private const string SourcePage = "StudentRearrange";
    public const int MinSessionReason = 30;
    public const int MinOpReason      = 10;
    public const int MinOverrideReason = 20;   // the brief's floor for a lock override

    public const string AckText =
        "I understand that this action will be recorded under my name, with my user account, " +
        "date, time and IP address, and that it can be audited and reversed.";

    // ═════════════════════════════════════════════════════════════════════════
    //  Access
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>HOD, Dean, AR and super admin only. Checked on EVERY entry point,
    /// read ones included — a hidden menu item is not access control.</summary>
    public static bool CanUse()
    {
        return RoleAccessService.CanAccess(SlugManage)
            || RoleAccessService.CanAccess(SlugLogs)
            || RoleAccessService.CanAccess(SlugDashboard);
    }

    /// <summary>Overriding a results status lock is for AR, Dean and super admin.
    /// Never for a HOD — that was an explicit ruling, so it is a rule here and not a
    /// question of which menu items someone happens to hold.</summary>
    public static bool CanOverrideLock()
    {
        string r = (RoleAccessService.GetRoleCode() ?? "").ToLowerInvariant();
        return r == "admin" || r == "registrar" || r == "dean";
    }

    public static string Actor()
    {
        var ctx = HttpContext.Current; var s = ctx != null ? ctx.Session : null;
        if (s == null) return "system";
        return (s["username"] as string) ?? (s["ScreenName"] as string) ?? "system";
    }

    public static string ActorName()
    {
        var ctx = HttpContext.Current; var s = ctx != null ? ctx.Session : null;
        if (s == null) return "";
        return (s["ScreenName"] as string) ?? (s["usernm"] as string) ?? Actor();
    }

    public static string ActorRole() { return RoleAccessService.GetRoleCode() ?? ""; }

    public static string ClientIp()
    {
        var ctx = HttpContext.Current;
        if (ctx == null || ctx.Request == null) return "";
        string fwd = ctx.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
        if (!string.IsNullOrEmpty(fwd)) return fwd.Split(',')[0].Trim();
        return ctx.Request.UserHostAddress ?? "";
    }

    public static string UserAgent()
    {
        var ctx = HttpContext.Current;
        if (ctx == null || ctx.Request == null) return "";
        return Trunc(ctx.Request.UserAgent ?? "", 400);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Plumbing
    // ═════════════════════════════════════════════════════════════════════════

    public static string ConnStr() { return CourseCorrectionService.ConnStr(); }

    private static MySqlCommand Cmd(string sql, MySqlConnection c, MySqlTransaction t)
    {
        var cmd = new MySqlCommand(sql, c);
        if (t != null) cmd.Transaction = t;
        cmd.CommandTimeout = 180;
        return cmd;
    }

    private static string S(IDataRecord r, int i) { return r.IsDBNull(i) ? "" : Convert.ToString(r[i]).Trim(); }
    private static int I(IDataRecord r, int i) { return r.IsDBNull(i) ? 0 : Convert.ToInt32(r[i]); }
    private static object NI(IDataRecord r, int i) { return r.IsDBNull(i) ? null : (object)Convert.ToInt32(r[i]); }
    private static string Trunc(string s, int n)
    { if (string.IsNullOrEmpty(s)) return s; return s.Length <= n ? s : s.Substring(0, n); }

    private static string Err(string m) { return Json.Serialize(new { success = false, message = m }); }

    /// <summary>Records a refusal. The dashboard surfaces these: a run of DENIED rows against
    /// one account is worth more than a run of successful ones.</summary>
    public static void NoteAttempt(string action, string outcome, string detail, string regno, long sessionId)
    {
        try
        {
            using (var c = new MySqlConnection(ConnStr()))
            {
                c.Open();
                using (var cmd = Cmd(
                    "INSERT INTO campus_dynamics.acad_rearrange_attempt " +
                    "(at_time, actor_user, actor_role, actor_ip, regno, session_id, action, outcome, detail) " +
                    "VALUES (NOW(), @u, @r, @i, @g, @s, @a, @o, @d)", c, null))
                {
                    cmd.Parameters.AddWithValue("@u", Actor());
                    cmd.Parameters.AddWithValue("@r", ActorRole());
                    cmd.Parameters.AddWithValue("@i", ClientIp());
                    cmd.Parameters.AddWithValue("@g", (object)regno ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@s", sessionId > 0 ? (object)sessionId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@a", Trunc(action, 60));
                    cmd.Parameters.AddWithValue("@o", Trunc(outcome, 30));
                    cmd.Parameters.AddWithValue("@d", Trunc(detail, 600));
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch { /* never let bookkeeping break the refusal it is describing */ }
    }

    private static void SetAuditContext(MySqlConnection c, MySqlTransaction t, string reason)
    {
        try
        {
            using (var cmd = Cmd(
                "INSERT INTO campus_dynamics.mark_audit_context (conn_id, actor, source, reason, ip, set_at) " +
                "VALUES (CONNECTION_ID(), @a, @s, @r, @i, NOW()) " +
                "ON DUPLICATE KEY UPDATE actor=VALUES(actor), source=VALUES(source), " +
                "reason=VALUES(reason), ip=VALUES(ip), set_at=NOW()", c, t))
            {
                cmd.Parameters.AddWithValue("@a", Actor());
                cmd.Parameters.AddWithValue("@s", SourcePage);
                cmd.Parameters.AddWithValue("@r", Trunc(reason, 200));
                cmd.Parameters.AddWithValue("@i", ClientIp());
                cmd.ExecuteNonQuery();
            }
        }
        catch { /* attribution is best effort and must never stop the work */ }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Session
    // ═════════════════════════════════════════════════════════════════════════

    public static string OpenSession(string regno, string reason, bool acknowledged)
    {
        if (!CanUse())
        {
            NoteAttempt("open_session", "DENIED", "Role " + ActorRole() + " is not permitted", regno, 0);
            return Err("You do not have permission to use Student Course Rearrangement.");
        }

        regno = (regno ?? "").Trim();
        reason = (reason ?? "").Trim();

        if (regno.Length == 0)
            return Err("Enter the student number.");
        if (reason.Length < MinSessionReason)
        {
            NoteAttempt("open_session", "VALIDATION",
                "Reason too short (" + reason.Length + " of " + MinSessionReason + ")", regno, 0);
            return Err("Give a written reason of at least " + MinSessionReason +
                       " characters. You typed " + reason.Length + ".");
        }
        if (!acknowledged)
        {
            NoteAttempt("open_session", "VALIDATION", "Acknowledgement not given", regno, 0);
            return Err("You must tick the acknowledgement before the student record can be loaded.");
        }

        var scope = MarksScopeResolver.Resolve();
        if (!scope.HasAccess)
            return Err("You do not have a marks-management scope, so no student record can be opened.");

        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();

            string name = "", prog = "";
            using (var cmd = Cmd(
                "SELECT TRIM(CONCAT(COALESCE(firstname,''),' ',COALESCE(othername,''))), COALESCE(progid,'') " +
                "FROM campus_dynamics.acad_student WHERE regno=@r LIMIT 1", c, null))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var r = cmd.ExecuteReader())
                    if (r.Read()) { name = S(r, 0); prog = S(r, 1); }
            }
            if (name.Length == 0 && prog.Length == 0)
            {
                NoteAttempt("open_session", "VALIDATION", "No such student", regno, 0);
                return Err("No student found with number " + regno + ".");
            }

            // Scope gate on the student's own programme — a HOD may not open a student
            // outside their department even to look.
            if (!scope.IsAdmin && scope.AllowedProgCodes != null &&
                !scope.AllowedProgCodes.Contains(prog))
            {
                NoteAttempt("open_session", "DENIED", "Student programme " + prog + " outside scope", regno, 0);
                return Err("This student is on programme " + prog + ", which is outside your scope (" + scope.Label + ").");
            }

            long id;
            string sref;
            using (var t = c.BeginTransaction())
            {
                sref = NextRef(c, t);
                using (var cmd = Cmd(
                    "INSERT INTO campus_dynamics.acad_rearrange_session " +
                    "(session_ref, regno, student_name, prog_id, reason, acknowledged, acknowledged_text, " +
                    " actor_user, actor_name, actor_role, actor_ip, actor_agent, opened_at, status) " +
                    "VALUES (@ref,@rg,@nm,@pg,@rs,1,@ak,@au,@an,@ar,@ip,@ua,NOW(),'OPEN')", c, t))
                {
                    cmd.Parameters.AddWithValue("@ref", sref);
                    cmd.Parameters.AddWithValue("@rg", regno);
                    cmd.Parameters.AddWithValue("@nm", name);
                    cmd.Parameters.AddWithValue("@pg", prog);
                    cmd.Parameters.AddWithValue("@rs", Trunc(reason, 1000));
                    cmd.Parameters.AddWithValue("@ak", AckText);
                    cmd.Parameters.AddWithValue("@au", Actor());
                    cmd.Parameters.AddWithValue("@an", ActorName());
                    cmd.Parameters.AddWithValue("@ar", ActorRole());
                    cmd.Parameters.AddWithValue("@ip", ClientIp());
                    cmd.Parameters.AddWithValue("@ua", UserAgent());
                    cmd.ExecuteNonQuery();
                    id = cmd.LastInsertedId;
                }
                t.Commit();
            }
            return Json.Serialize(new { success = true, sessionId = id, sessionRef = sref, regno = regno });
        }
    }

    private static string NextRef(MySqlConnection c, MySqlTransaction t)
    {
        string day = DateTime.Now.ToString("yyyyMMdd");
        int n = 0;
        using (var cmd = Cmd("SELECT COUNT(*) FROM campus_dynamics.acad_rearrange_session " +
                             "WHERE session_ref LIKE @p", c, t))
        {
            cmd.Parameters.AddWithValue("@p", "SR-" + day + "-%");
            n = Convert.ToInt32(cmd.ExecuteScalar());
        }
        return "SR-" + day + "-" + (n + 1).ToString("D4");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Workspace — deliberately ONE call
    //
    //  These PageMethods are EnableSession=true, so ASP.NET serialises them on an
    //  exclusive session lock: several parallel requests would queue behind one
    //  another anyway. Everything the workspace needs therefore comes back in a
    //  single round trip.
    // ═════════════════════════════════════════════════════════════════════════

    public static string LoadWorkspace(long sessionId)
    {
        if (!CanUse()) { NoteAttempt("load", "DENIED", "Role " + ActorRole(), null, sessionId); return Err("Not permitted."); }

        using (var c = new MySqlConnection(ConnStr()))
        {
            c.Open();
            var sess = ReadSession(c, null, sessionId);
            if (sess == null) return Err("That rearrangement session was not found.");
            if (!string.Equals(sess.actor, Actor(), StringComparison.OrdinalIgnoreCase) && !RoleAccessService.IsAdmin())
                return Err("That session belongs to another officer.");

            string regno = sess.regno;

            // ── header ──
            object header;
            using (var cmd = Cmd(
                "SELECT TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) nm, " +
                " COALESCE(s.progid,'') prog, COALESCE(NULLIF(p.progname,''),s.progid) progname, " +
                " COALESCE(NULLIF(TRIM(s.entryno),''), s.regno) entryno, COALESCE(s.photofile,'') photo, " +
                " COALESCE(s.stud_status,'') status, COALESCE(s.entryyear,0) entryyear " +
                "FROM campus_dynamics.acad_student s " +
                "LEFT JOIN campus_dynamics.acad_programme p ON p.progcode=s.progid " +
                "WHERE s.regno=@r LIMIT 1", c, null))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return Err("Student record not found.");
                    header = new
                    {
                        regno = regno, name = S(r, 0), prog = S(r, 1), progName = S(r, 2),
                        entryno = S(r, 3), photo = S(r, 4), status = S(r, 5), entryYear = I(r, 6)
                    };
                }
            }

            // ── semester registrations: the (studyyear, semester) -> acad_year authority ──
            var sems = new List<object>();
            var semKeys = new HashSet<string>();
            // (acad_year|semester) -> studyyear, so a registration with no published result
            // can still be placed in the right group.
            var semTerms = new List<KeyValuePair<string, int>>();
            using (var cmd = Cmd(
                "SELECT ID, acad_year, semester, studyyear, regstatus, COALESCE(registeredBy,'') " +
                "FROM campus_dynamics.acad_registration WHERE regno=@r " +
                "ORDER BY studyyear, semester, acad_year", c, null))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        int sy = I(r, 3), sm = I(r, 2);
                        semKeys.Add(sy + "/" + sm);
                        semTerms.Add(new KeyValuePair<string, int>(S(r, 1) + "|" + sm, sy));
                        sems.Add(new
                        {
                            id = I(r, 0), acadYear = S(r, 1), semester = sm, studyYear = sy,
                            status = S(r, 4), registeredBy = S(r, 5)
                        });
                    }
            }

            // ── the courses, grouped the way the transcript groups them ──
            var courses = new List<CourseRow>();
            using (var cmd = Cmd(
                "SELECT cr.ID, cr.courseID, COALESCE(NULLIF(co.courseName,''),cr.courseID) title, " +
                "  cr.acad_year, cr.semester, cr.course_status, cr.registration_type, cr.lecturer_status, " +
                "  COALESCE(cr.mark_stage,'') mark_stage, cr.prog_id, " +
                "  cr.provisional_course_work_marks, cr.provisional_exam_marks, cr.provisional_total_marks, " +
                "  rs.ID rid, rs.studyyear, rs.score, rs.grade, rs.gradept, rs.gpa, rs.CreditUnits, " +
                "  COALESCE(rs.is_retake,0) is_retake, " +
                "  COALESCE((SELECT pc.study_year FROM campus_dynamics.acad_programmecourses pc " +
                "            WHERE pc.progcode=cr.prog_id AND pc.course_code=cr.courseID LIMIT 1),0) cur_year, " +
                "  COALESCE((SELECT pc2.semester FROM campus_dynamics.acad_programmecourses pc2 " +
                "            WHERE pc2.progcode=cr.prog_id AND pc2.course_code=cr.courseID LIMIT 1),0) cur_sem, " +
                "  COALESCE((SELECT pc3.CreditUnit FROM campus_dynamics.acad_course pc3 " +
                "            WHERE pc3.courseID=cr.courseID LIMIT 1),0) cat_cu " +
                "FROM campus_dynamics_portal.acad_course_registration cr " +
                "LEFT JOIN campus_dynamics.acad_course co ON co.courseID=cr.courseID " +
                "LEFT JOIN campus_dynamics.acad_results rs " +
                "       ON rs.regno=cr.regno AND rs.courseid=cr.courseID " +
                "WHERE cr.regno=@r " +
                "ORDER BY cr.acad_year, cr.semester, cr.courseID", c, null))
            {
                cmd.Parameters.AddWithValue("@r", regno);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        var cw = new CourseRow();
                        cw.regId       = I(r, 0);
                        cw.course      = S(r, 1);
                        cw.title       = S(r, 2);
                        cw.acadYear    = S(r, 3);
                        cw.semester    = I(r, 4);
                        cw.courseStatus= S(r, 5);
                        cw.regType     = S(r, 6);
                        cw.lecStatus   = S(r, 7);
                        cw.markStage   = S(r, 8);
                        cw.progId      = S(r, 9);
                        cw.cw          = NI(r, 10);
                        cw.exam        = NI(r, 11);
                        cw.total       = NI(r, 12);
                        cw.resultId    = I(r, 13);
                        cw.studyYear   = I(r, 14);
                        cw.score       = NI(r, 15);
                        cw.grade       = S(r, 16);
                        cw.gradePt     = r.IsDBNull(17) ? (object)null : Convert.ToDouble(r[17]);
                        cw.gpa         = r.IsDBNull(18) ? (object)null : Convert.ToDouble(r[18]);
                        cw.creditUnits = r.IsDBNull(19) ? (object)null : Convert.ToDouble(r[19]);
                        cw.isRetake    = I(r, 20) == 1;
                        cw.curYear     = I(r, 21);
                        cw.curSem      = I(r, 22);
                        if (cw.creditUnits == null && !r.IsDBNull(23)) cw.creditUnits = Convert.ToDouble(r[23]);
                        courses.Add(cw);
                    }
            }

            // A registration with no published result has no studyyear of its own. Infer it from
            // the semester registration that owns its academic year + semester, so it lands in
            // the right group instead of collapsing into "Year 0".
            var byTerm = new Dictionary<string, int>();
            foreach (var st in semTerms)
                if (!byTerm.ContainsKey(st.Key)) byTerm[st.Key] = st.Value;
            foreach (var cw in courses)
            {
                if (cw.studyYear > 0) continue;
                int sy;
                if (byTerm.TryGetValue(cw.acadYear + "|" + cw.semester, out sy)) cw.studyYear = sy;
            }

            // ── the results status lock, per (course, programme, term) ──
            var lockCache = new Dictionary<string, string>();
            foreach (var cw in courses)
            {
                string key = cw.course + "|" + cw.progId + "|" + cw.acadYear + "|" + cw.semester + "|" + cw.studyYear;
                string st;
                if (!lockCache.TryGetValue(key, out st))
                {
                    try { st = ResultsStatusService.GetStatus(cw.course, cw.progId, cw.acadYear, cw.semester, cw.studyYear, 0, ""); }
                    catch { st = ResultsStatusService.STATUS_DRAFT; }
                    lockCache[key] = st;
                }
                cw.lockStatus = st ?? ResultsStatusService.STATUS_DRAFT;
                cw.locked = cw.lockStatus != ResultsStatusService.STATUS_DRAFT;
            }

            // A semester counts as CLOSED when it holds any FINAL_PUBLISHED result. No calendar
            // was invented for this; that was the ruling.
            var closed = new HashSet<string>();
            foreach (var cw in courses)
                if (cw.lockStatus == ResultsStatusService.STATUS_FINAL_PUBLISHED)
                    closed.Add(cw.studyYear + "/" + cw.semester);

            // The academic years the institution actually runs, newest first, so registering a
            // semester is a choice from real values rather than a free-text box that accepts
            // "2026/27" and fails later.
            var acadYears = new List<string>();
            using (var cmd = Cmd(
                "SELECT acad_year FROM campus_dynamics.acad_registration " +
                "WHERE IFNULL(acad_year,'') NOT IN ('','-') " +
                "GROUP BY acad_year ORDER BY acad_year DESC LIMIT 12", c, null))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) acadYears.Add(S(r, 0));

            var rows = new List<object>();
            foreach (var cw in courses) rows.Add(cw.ToJson());

            return Json.Serialize(new
            {
                success = true,
                session = new
                {
                    id = sess.id, sref = sess.sref, reason = sess.reason,
                    openedAt = sess.openedAt, status = sess.status
                },
                actor = new { user = Actor(), name = ActorName(), role = ActorRole(), ip = ClientIp() },
                canOverrideLock = CanOverrideLock(),
                student = header,
                semesters = sems,
                acadYears = acadYears,
                maxSemester = 3, maxStudyYear = 8,
                closedSemesters = new List<string>(closed),
                courses = rows,
                checksum = Checksum(courses),
                minOpReason = MinOpReason,
                minOverrideReason = MinOverrideReason
            });
        }
    }

    private class CourseRow
    {
        public int regId, semester, studyYear, resultId, curYear, curSem;
        public string course, title, acadYear, courseStatus, regType, lecStatus, markStage, progId, grade, lockStatus;
        public object cw, exam, total, score, gradePt, gpa, creditUnits;
        public bool isRetake, locked;

        public object ToJson()
        {
            return new
            {
                regId, course, title, acadYear, semester, studyYear,
                courseStatus, regType, lecStatus, markStage, progId,
                cw, exam, total, resultId, score, grade, gradePt, gpa, creditUnits,
                isRetake, lockStatus, locked,
                curriculum = new { year = curYear, semester = curSem }
            };
        }
    }

    /// <summary>Optimistic lock. Every field a rearrangement can change goes into the hash, so
    /// if anyone else has touched this student between load and save the checksum will not
    /// match and the save is refused rather than silently overwriting their work. There is no
    /// version column on these tables and the brief forbids adding one, so the row's own
    /// contents are the version.</summary>
    private static string Checksum(List<CourseRow> rows)
    {
        var sb = new StringBuilder();
        rows.Sort(delegate (CourseRow a, CourseRow b) { return a.regId.CompareTo(b.regId); });
        foreach (var r in rows)
            sb.Append(r.regId).Append('|').Append(r.course).Append('|').Append(r.acadYear).Append('|')
              .Append(r.semester).Append('|').Append(r.studyYear).Append('|').Append(r.courseStatus).Append('|')
              .Append(r.markStage).Append('|').Append(Num(r.cw)).Append('|').Append(Num(r.exam)).Append('|')
              .Append(Num(r.total)).Append('|').Append(r.resultId).Append('|').Append(Num(r.score)).Append('|')
              .Append(r.grade).Append(';');
        using (var sha = SHA1.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())))
                   .Replace("-", "").Substring(0, 20);
    }

    private static string Num(object o)
    {
        if (o == null) return "-";
        return Convert.ToString(o, CultureInfo.InvariantCulture);
    }

    private class SessionInfo
    {
        public long id; public string sref, regno, reason, actor, status, openedAt;
    }

    private static SessionInfo ReadSession(MySqlConnection c, MySqlTransaction t, long id)
    {
        using (var cmd = Cmd(
            "SELECT id, session_ref, regno, reason, actor_user, status, " +
            "DATE_FORMAT(opened_at,'%d %b %Y %H:%i') FROM campus_dynamics.acad_rearrange_session " +
            "WHERE id=@i LIMIT 1", c, t))
        {
            cmd.Parameters.AddWithValue("@i", id);
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                var s = new SessionInfo();
                s.id = Convert.ToInt64(r[0]); s.sref = S(r, 1); s.regno = S(r, 2);
                s.reason = S(r, 3); s.actor = S(r, 4); s.status = S(r, 5); s.openedAt = S(r, 6);
                return s;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Recalculation
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Rewrites the cached semester GPA on every row of the student, in both
    /// acad_results and acad_transcript_results, using the same expression the credit-units
    /// backfill used. Returns before/after per (studyyear, semester) so the log can say what
    /// moved rather than merely that something was recomputed.</summary>
    private static List<object> Recalculate(MySqlConnection c, MySqlTransaction t, string regno)
    {
        var before = ReadGpaMap(c, t, regno);

        using (var cmd = Cmd(
            "UPDATE campus_dynamics.acad_results r JOIN (" +
            "  SELECT regno, studyyear, semester, " +
            "         ROUND(SUM(gradept*CreditUnits)/NULLIF(SUM(CreditUnits),0),2) g " +
            "    FROM campus_dynamics.acad_results WHERE regno=@r " +
            "   GROUP BY regno, studyyear, semester) x " +
            "  ON x.regno=r.regno AND x.studyyear=r.studyyear AND x.semester=r.semester " +
            " SET r.gpa = x.g WHERE r.regno=@r", c, t))
        { cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }

        using (var cmd = Cmd(
            "UPDATE campus_dynamics.acad_transcript_results tr JOIN (" +
            "  SELECT regno, studyyear, semester, " +
            "         ROUND(SUM(gradept*CreditUnits)/NULLIF(SUM(CreditUnits),0),2) g " +
            "    FROM campus_dynamics.acad_results WHERE regno=@r " +
            "   GROUP BY regno, studyyear, semester) x " +
            "  ON x.regno=tr.regno AND x.studyyear=tr.studyyear AND x.semester=tr.semester " +
            " SET tr.gpa = x.g WHERE tr.regno=@r", c, t))
        { cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }

        // acad_graduands caches CGPA and the degree class; it is only meaningful for a
        // student who has one, so this is a no-op for everybody else.
        try
        {
            using (var cmd = Cmd(
                "UPDATE campus_dynamics.acad_graduands SET cgpa = acad_CGPAFinder(regno) WHERE regno=@r", c, t))
            { cmd.Parameters.AddWithValue("@r", regno); cmd.ExecuteNonQuery(); }
        }
        catch { /* the student may not be a graduand, or the function may be absent */ }

        var after = ReadGpaMap(c, t, regno);

        var diff = new List<object>();
        var keys = new HashSet<string>(before.Keys);
        foreach (var k in after.Keys) keys.Add(k);
        foreach (var k in keys)
        {
            double b = before.ContainsKey(k) ? before[k] : 0;
            double a = after.ContainsKey(k) ? after[k] : 0;
            if (Math.Abs(a - b) > 0.0001)
            {
                var parts = k.Split('/');
                diff.Add(new { studyYear = parts[0], semester = parts[1], before = b, after = a });
            }
        }
        return diff;
    }

    private static Dictionary<string, double> ReadGpaMap(MySqlConnection c, MySqlTransaction t, string regno)
    {
        var m = new Dictionary<string, double>();
        using (var cmd = Cmd(
            "SELECT studyyear, semester, " +
            "       ROUND(SUM(gradept*CreditUnits)/NULLIF(SUM(CreditUnits),0),2) g " +
            "FROM campus_dynamics.acad_results WHERE regno=@r GROUP BY studyyear, semester", c, t))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    m[I(r, 0) + "/" + I(r, 1)] = r.IsDBNull(2) ? 0 : Convert.ToDouble(r[2]);
        }
        return m;
    }

    public static double Cgpa(MySqlConnection c, MySqlTransaction t, string regno)
    {
        using (var cmd = Cmd(
            "SELECT ROUND(SUM(gradept*CreditUnits)/NULLIF(SUM(CreditUnits),0),2) " +
            "FROM campus_dynamics.acad_results WHERE regno=@r", c, t))
        {
            cmd.Parameters.AddWithValue("@r", regno);
            object o = cmd.ExecuteScalar();
            return o == null || o == DBNull.Value ? 0 : Convert.ToDouble(o);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Row snapshots — what makes "nothing is ever hard deleted" true
    // ═════════════════════════════════════════════════════════════════════════

    public static Dictionary<string, object> ReadRow(MySqlConnection c, MySqlTransaction t,
                                                     string qualified, string pkCol, object pk)
    {
        using (var cmd = Cmd("SELECT * FROM " + qualified + " WHERE " + pkCol + "=@p LIMIT 1", c, t))
        {
            cmd.Parameters.AddWithValue("@p", pk);
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < r.FieldCount; i++)
                    d[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                return d;
            }
        }
    }

    /// <summary>Puts an archived row back under its ORIGINAL primary key. Safe because
    /// AUTO_INCREMENT never reissues a value, so the slot cannot have been taken.</summary>
    public static bool ReInsert(MySqlConnection c, MySqlTransaction t,
                                string qualified, Dictionary<string, object> row)
    {
        if (row == null || row.Count == 0) return false;
        var cols = new StringBuilder();
        var vals = new StringBuilder();
        var cmd = Cmd("", c, t);
        int i = 0;
        foreach (var kv in row)
        {
            if (i > 0) { cols.Append(','); vals.Append(','); }
            cols.Append('`').Append(kv.Key).Append('`');
            vals.Append("@v").Append(i);
            cmd.Parameters.AddWithValue("@v" + i, kv.Value ?? DBNull.Value);
            i++;
        }
        cmd.CommandText = "INSERT INTO " + qualified + " (" + cols + ") VALUES (" + vals + ")";
        using (cmd) return cmd.ExecuteNonQuery() > 0;
    }

    public static string ToJson(object o) { return Json.Serialize(o); }
    public static T FromJson<T>(string s) { return Json.Deserialize<T>(s); }
    public static JavaScriptSerializer Ser { get { return Json; } }
}
