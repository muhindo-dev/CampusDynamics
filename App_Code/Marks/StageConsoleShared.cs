using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// Shared brain for the three staged-marks consoles (Capture / Approve / Publish).
/// Each page passes its stage name; all data + actions flow through here so the three
/// screens are identical in mechanics (browse → preview → commit / cancel / return /
/// history), differing only by stage + role gate. Scope is enforced via MarksScope.
/// </summary>
public static class StageConsoleShared
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    private static StageDef Def(string stage) { return StageAdvanceService.ByName(stage); }

    // Who may ACT at a stage: hierarchy — HOD≤Dean≤Admin.
    private static bool CanAct(string stage, MarksScope s)
    {
        StageDef def = Def(stage);
        if (s.IsAdmin) return true;
        if (def.Name == "CAPTURE") return s.Mode == "department" || s.Mode == "faculty"; // HOD or Dean
        if (def.Name == "APPROVE") return s.Mode == "faculty";                            // Dean
        return false;                                                                     // PUBLISH = admin only
    }

    private static string RoleFor(MarksScope s)
    {
        if (s.IsAdmin) return "admin";
        if (s.Mode == "faculty") return "dean";
        if (s.Mode == "department") return "hod";
        return s.RoleNote;
    }

    private static string Scope(MarksScope s, string alias) { return s.ProgFilter(alias); }

    // Exam stats/summaries must count ONLY active students — those who completed
    // onboarding and are flagged 'ACTIVE STUDENT'. That flag lives in the portal login
    // store campus_dynamics_portal.my_aspnet_users (name = regno). Alumni, not-yet-
    // onboarded (NULL) and accountless regnos are excluded so counts (esp. "not entered")
    // reflect real, active students instead of the whole historical registration table.
    // Join form, not the EXISTS form: this is the driver of an aggregate over the whole
    // 691k-row registration table. See ActiveStudentFilter.Join - 19.65s -> 0.178s for the
    // same five numbers.
    private static string ActiveOnlyJoin(string alias)
    {
        return ActiveStudentFilter.Join(alias, "regno");
    }

    private static string DeniedJson(string stage)
    {
        return Json.Serialize(new { success = false, denied = true,
            message = "Your role cannot perform the " + Def(stage).Name + " stage." });
    }

    // ── INIT: scope banner + filter options (years/programmes at this stage) ──
    public static string Init(string stage)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            StageDef def = Def(stage);
            var years = new List<string>(); var progs = new List<object>();
            using (var conn = new MySqlConnection(MarkStage.ConnStr))
            {
                conn.Open();
                MarkStage.EnsureSchema(conn);
                using (var cmd = new MySqlCommand("SELECT DISTINCT cr.acad_year FROM " + MarkStage.REG + " cr WHERE cr.mark_stage=@s" +
                    Scope(scope, "cr") + " AND cr.acad_year<>'' ORDER BY cr.acad_year DESC LIMIT 12", conn))
                {
                    cmd.Parameters.AddWithValue("@s", def.FromStage);
                    using (var r = cmd.ExecuteReader()) while (r.Read()) years.Add(r.GetString(0));
                }
                using (var cmd = new MySqlCommand("SELECT DISTINCT cr.prog_id, COALESCE(NULLIF(p.progname,''),cr.prog_id) pn FROM " +
                    MarkStage.REG + " cr LEFT JOIN acad_programme p ON p.progcode=cr.prog_id WHERE cr.mark_stage=@s" +
                    Scope(scope, "cr") + " ORDER BY pn LIMIT 300", conn))
                {
                    cmd.Parameters.AddWithValue("@s", def.FromStage);
                    using (var r = cmd.ExecuteReader()) while (r.Read())
                        progs.Add(new { value = S(r[0]), text = S(r[1]) });
                }
            }
            return Json.Serialize(new {
                success = true, stage = def.Name, fromStage = def.FromStage, toStage = def.ToStage,
                fromLabel = MarkStage.Label(def.FromStage), toLabel = MarkStage.Label(def.ToStage),
                canAct = CanAct(stage, scope), years = years, programmes = progs,
                scope = new { label = scope.Label, role = scope.RoleNote, isAdmin = scope.IsAdmin, hasAccess = scope.HasAccess }
            });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── STATS: stage funnel within scope + actionable (FromStage) count ──
    public static string Stats(string stage)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            var funnel = new Dictionary<string, int>();
            using (var conn = new MySqlConnection(MarkStage.ConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT cr.mark_stage, COUNT(*) c FROM " + MarkStage.REG +
                    " cr" + ActiveOnlyJoin("cr") + " WHERE 1=1" + Scope(scope, "cr") + " GROUP BY cr.mark_stage", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) funnel[S(r[0])] = ToI(r[1]);
            }
            StageDef def = Def(stage);
            return Json.Serialize(new { success = true,
                notEntered = G(funnel, MarkStage.NOT_ENTERED), entered = G(funnel, MarkStage.ENTERED),
                captured = G(funnel, MarkStage.CAPTURED), approved = G(funnel, MarkStage.APPROVED),
                published = G(funnel, MarkStage.PUBLISHED), actionable = G(funnel, def.FromStage) });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── BROWSE: paginated list of marks at FromStage within scope ──
    public static string Browse(string stage, string year, string sem, string prog, string q, int page, int pageSize, bool failedOnly)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            StageDef def = Def(stage);
            if (page < 1) page = 1; if (pageSize < 1 || pageSize > 200) pageSize = 50;
            var rows = new List<object>(); int total = 0;
            using (var conn = new MySqlConnection(MarkStage.ConnStr))
            {
                conn.Open();
                var where = new StringBuilder(" WHERE cr.mark_stage=@from");
                where.Append(Scope(scope, "cr"));
                // Browse shows ALL marks at this stage for everyone (alumni + active) — no filter.
                // Only the funnel Stats() count above is narrowed to active students.
                using (var cnt = new MySqlCommand("", conn))
                {
                    cnt.Parameters.AddWithValue("@from", def.FromStage);
                    if (!string.IsNullOrEmpty(year)) { where.Append(" AND cr.acad_year=@y"); cnt.Parameters.AddWithValue("@y", year); }
                    if (!string.IsNullOrEmpty(sem))  { where.Append(" AND cr.semester=@sm"); cnt.Parameters.AddWithValue("@sm", sem); }
                    if (!string.IsNullOrEmpty(prog)) { where.Append(" AND cr.prog_id=@p"); cnt.Parameters.AddWithValue("@p", prog); }
                    // search by student number (entryno) as well as reg no / course — matches the
                    // eportal marks-entry search. EXISTS (not a JOIN) so the COUNT never inflates.
                    if (!string.IsNullOrEmpty(q))    { where.Append(" AND (cr.regno LIKE @q OR cr.courseID LIKE @q OR EXISTS(SELECT 1 FROM acad_student sq WHERE sq.regno=cr.regno AND TRIM(IFNULL(sq.entryno,'')) LIKE @q))"); cnt.Parameters.AddWithValue("@q", "%" + q + "%"); }
                    if (failedOnly) where.Append(" AND cr.provisional_total_marks < 50");
                    cnt.CommandText = "SELECT COUNT(*) FROM " + MarkStage.REG + " cr" + where;
                    total = Convert.ToInt32(cnt.ExecuteScalar());
                }
                using (var cmd = new MySqlCommand(
                    "SELECT cr.id, cr.regno," +
                    // Student number = acad_student.entryno (e.g. 23/U/DPE/1184/KA/INS), same as the
                    // eportal marks-entry display; fall back to the reg no if entryno is ever blank.
                    " COALESCE(NULLIF(TRIM(IFNULL(s.entryno,'')),''), cr.regno) AS studno," +
                    " TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) nm," +
                    " cr.prog_id, COALESCE(NULLIF(p.progname,''),cr.prog_id) pn, cr.courseID," +
                    " COALESCE(c.courseName,cr.courseID) cn, cr.acad_year, cr.semester," +
                    " cr.provisional_course_work_marks cw, cr.provisional_exam_marks ex, cr.provisional_total_marks tot," +
                    // Assigned lecturer for this course: per-term teaching allocation first (best coverage),
                    // then the programme-course assignment as a fallback.
                    " COALESCE(" +
                    "  (SELECT TRIM(he.emp_name) FROM acad_teaching_allocation ta JOIN hrm_employee he ON he.empID=ta.staffCode" +
                    "   WHERE ta.courseID=cr.courseID AND ta.acad_year=cr.acad_year AND ta.semester=cr.semester" +
                    "   AND IFNULL(ta.staffCode,'')<>'' AND NULLIF(TRIM(he.emp_name),'') IS NOT NULL LIMIT 1)," +
                    "  (SELECT TRIM(he2.emp_name) FROM acad_programmecourses pc JOIN hrm_employee he2 ON he2.empID=pc.lecturer_id" +
                    "   WHERE pc.progcode=cr.prog_id AND pc.course_code=cr.courseID AND UPPER(IFNULL(pc.is_lecturere_assigned,''))='YES'" +
                    "   AND IFNULL(pc.lecturer_id,0)>0 LIMIT 1)" +
                    " ) lecturer," +
                    // ── Tracker: who did what to this mark. Two independent facts, both already
                    // recorded, neither previously shown:
                    //   pa.*  - the last change to the MARKS themselves (trigger-written audit),
                    //           i.e. the lecturer who entered or edited them, and from where.
                    //   cr.mark_stage_changed_* - who moved the mark into the stage it now sits
                    //           at, i.e. who captured / approved / published it.
                    // Dates are formatted in SQL so the output does not depend on the worker's
                    // culture. The audit join is one correlated MAX(id) on idx_pma_reg(reg_id,id)
                    // plus a primary-key read, over the 50 rows of one page: unmeasurable.
                    " pa.performed_by mark_by, pa.action_type mark_act, pa.source_page mark_via," +
                    " DATE_FORMAT(pa.created_at,'%d %b %Y %H:%i') mark_at," +
                    " cr.mark_stage_changed_by stage_by," +
                    " DATE_FORMAT(cr.mark_stage_changed_at,'%d %b %Y %H:%i') stage_at" +
                    " FROM " + MarkStage.REG + " cr" +
                    " LEFT JOIN acad_student s ON s.regno=cr.regno" +
                    " LEFT JOIN acad_programme p ON p.progcode=cr.prog_id" +
                    " LEFT JOIN acad_course c ON c.courseID=cr.courseID" +
                    " LEFT JOIN campus_dynamics_portal.acad_provisional_marks_audit pa" +
                    "   ON pa.id=(SELECT MAX(a.id) FROM campus_dynamics_portal.acad_provisional_marks_audit a" +
                    "             WHERE a.reg_id=cr.ID)" +
                    where + " ORDER BY cr.prog_id, cr.regno LIMIT @off,@ps", conn))
                {
                    cmd.Parameters.AddWithValue("@from", def.FromStage);
                    if (!string.IsNullOrEmpty(year)) cmd.Parameters.AddWithValue("@y", year);
                    if (!string.IsNullOrEmpty(sem))  cmd.Parameters.AddWithValue("@sm", sem);
                    if (!string.IsNullOrEmpty(prog)) cmd.Parameters.AddWithValue("@p", prog);
                    if (!string.IsNullOrEmpty(q))    cmd.Parameters.AddWithValue("@q", "%" + q + "%");
                    cmd.Parameters.AddWithValue("@off", (page - 1) * pageSize);
                    cmd.Parameters.AddWithValue("@ps", pageSize);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            rows.Add(new {
                                id = ToI(r["id"]), regno = S(r["regno"]), studentNo = S(r["studno"]), name = S(r["nm"]),
                                prog = S(r["prog_id"]), progName = S(r["pn"]), course = S(r["courseID"]),
                                courseName = S(r["cn"]), acadYear = S(r["acad_year"]), semester = S(r["semester"]),
                                cw = NI(r["cw"]), exam = NI(r["ex"]), total = NI(r["tot"]),
                                lecturer = S(r["lecturer"]), grade = GradeFromTotal(r["tot"]), failed = IsFail(r["tot"]),
                                markBy = S(r["mark_by"]), markAt = S(r["mark_at"]), markVia = S(r["mark_via"]),
                                markAct = S(r["mark_act"]), stageBy = S(r["stage_by"]), stageAt = S(r["stage_at"]) });
                }
            }
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            return Json.Serialize(new { success = true, rows = rows, total = total, page = page, pages = pages, pageSize = pageSize });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── PREVIEW: create a DRAFT session for the chosen params + return preview ──
    public static string CreateAndPreview(string stage, string prog, int yos, string acadYear, int sem, bool all, string notes, string failMode)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!CanAct(stage, scope)) return DeniedJson(stage);
            StageDef def = Def(stage);
            string actor = User(); string nm = UserName();
            int recId = StageAdvanceService.CreateDraft(def, scope, actor, nm, RoleFor(scope), prog, yos, acadYear, sem, all, notes, failMode);
            object preview = StageAdvanceService.Preview(def, scope, recId);
            return Json.Serialize(new { success = true, preview = preview });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    public static string Commit(string stage, int recordId)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!CanAct(stage, scope)) return DeniedJson(stage);
            object res = StageAdvanceService.Commit(Def(stage), scope, recordId, User());
            return Json.Serialize(res);
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    public static string Cancel(string stage, int recordId)
    {
        try { return Json.Serialize(StageAdvanceService.Cancel(Def(stage), recordId)); }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // Live progress for a running commit. Deliberately cheap (one indexed row read) —
    // the console polls this every couple of seconds while a batch is publishing.
    public static string Progress(string stage, int recordId)
    {
        try { return Json.Serialize(StageAdvanceService.Progress(Def(stage), recordId)); }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    public static string ReturnMarks(string stage, int[] ids, string reason)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!CanAct(stage, scope)) return DeniedJson(stage);
            return Json.Serialize(StageAdvanceService.ReturnMarks(Def(stage), scope, ids, reason, User()));
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // Advance a hand-picked set of marks (checkbox selection) forward one stage.
    public static string AdvanceSelected(string stage, int[] ids, string notes)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            if (!CanAct(stage, scope)) return DeniedJson(stage);
            return Json.Serialize(StageAdvanceService.AdvanceMarks(Def(stage), scope, ids, notes, User(), UserName(), RoleFor(scope)));
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── HISTORY: committed sessions for this stage within scope ──
    public static string Records(string stage, int page)
    {
        try
        {
            MarksScope scope = MarksScopeResolver.Resolve();
            StageDef def = Def(stage);
            if (page < 1) page = 1; int ps = 25;
            var list = new List<object>();
            using (var conn = new MySqlConnection(MarkStage.ConnStr))
            {
                conn.Open();
                // Admin sees all; others see their own sessions.
                string mine = scope.IsAdmin ? "" : " AND performed_by=@me";
                using (var cmd = new MySqlCommand("SELECT id, status, performed_by, performed_by_name, performed_by_role," +
                    " param_prog_id, param_acad_year, param_semester, param_year_of_study, param_all," +
                    " preview_count, affected_count, created_at, committed_at, notes, is_migration" +
                    " FROM campus_dynamics_portal." + def.RecordTable + " WHERE 1=1" + mine +
                    " ORDER BY id DESC LIMIT @off,@ps", conn))
                {
                    if (!scope.IsAdmin) cmd.Parameters.AddWithValue("@me", User());
                    cmd.Parameters.AddWithValue("@off", (page - 1) * ps);
                    cmd.Parameters.AddWithValue("@ps", ps);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(new {
                                id = ToI(r["id"]), status = S(r["status"]), by = S(r["performed_by"]),
                                byName = S(r["performed_by_name"]), role = S(r["performed_by_role"]),
                                prog = S(r["param_prog_id"]), acadYear = S(r["param_acad_year"]),
                                semester = S(r["param_semester"]), yearOfStudy = S(r["param_year_of_study"]),
                                all = ToI(r["param_all"]) == 1, preview = NI(r["preview_count"]),
                                affected = NI(r["affected_count"]), createdAt = S(r["created_at"]),
                                committedAt = S(r["committed_at"]), notes = S(r["notes"]),
                                isMigration = ToI(r["is_migration"]) == 1 });
                }
            }
            return Json.Serialize(new { success = true, records = list });
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    // ── RECORD DETAIL: full info for one session (params, who/when, summary, snapshot) ──
    public static string RecordDetail(string stage, int recordId)
    {
        try
        {
            StageDef def = Def(stage);
            using (var conn = new MySqlConnection(MarkStage.ConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT id, status, performed_by, performed_by_name, performed_by_role," +
                    " scope_faculty_code, scope_department_id, param_prog_id, param_year_of_study, param_acad_year," +
                    " param_semester, param_all, preview_count, affected_count, summary_json, stats_snapshot_json," +
                    " notes, is_migration, created_at, committed_at FROM campus_dynamics_portal." + def.RecordTable +
                    " WHERE id=@id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", recordId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return Json.Serialize(new { success = false, message = "Session not found." });
                        object summary = Parse(S(r["summary_json"]));
                        object stats = Parse(S(r["stats_snapshot_json"]));
                        var rec = new {
                            id = ToI(r["id"]), stage = def.Name, status = S(r["status"]),
                            by = S(r["performed_by"]), byName = S(r["performed_by_name"]), role = S(r["performed_by_role"]),
                            facultyCode = S(r["scope_faculty_code"]), departmentId = S(r["scope_department_id"]),
                            prog = S(r["param_prog_id"]), yearOfStudy = S(r["param_year_of_study"]),
                            acadYear = S(r["param_acad_year"]), semester = S(r["param_semester"]),
                            all = ToI(r["param_all"]) == 1, preview = NI(r["preview_count"]), affected = NI(r["affected_count"]),
                            notes = S(r["notes"]), isMigration = ToI(r["is_migration"]) == 1,
                            createdAt = S(r["created_at"]), committedAt = S(r["committed_at"]),
                            fromLabel = MarkStage.Label(def.FromStage), toLabel = MarkStage.Label(def.ToStage)
                        };
                        return Json.Serialize(new { success = true, record = rec, summary = summary, stats = stats });
                    }
                }
            }
        }
        catch (Exception ex) { return Json.Serialize(new { success = false, message = ex.Message }); }
    }

    private static object Parse(string s) { try { return string.IsNullOrEmpty(s) ? null : Json.DeserializeObject(s); } catch { return null; } }

    // ── CSV export of a committed session's affected marks ──
    public static void ExportRecordCsv(HttpResponse resp, string stage, int recordId)
    {
        MarksScope scope = MarksScopeResolver.Resolve();
        StageDef def = Def(stage);
        var sb = new StringBuilder("RegNo,Student,Programme,Course,CourseName,AcadYear,Sem,CW,Exam,Total\r\n");
        using (var conn = new MySqlConnection(MarkStage.ConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(
                "SELECT cr.regno, TRIM(CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,''))) nm," +
                " cr.prog_id, cr.courseID, COALESCE(c.courseName,cr.courseID) cn, cr.acad_year, cr.semester," +
                " cr.provisional_course_work_marks, cr.provisional_exam_marks, cr.provisional_total_marks" +
                " FROM " + MarkStage.REG + " cr LEFT JOIN acad_student s ON s.regno=cr.regno" +
                " LEFT JOIN acad_course c ON c.courseID=cr.courseID" +
                " WHERE cr." + def.RecordCol + "=@rid ORDER BY cr.prog_id, cr.regno", conn))
            {
                cmd.Parameters.AddWithValue("@rid", recordId);
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        sb.Append(Csv(S(r[0]))).Append(',').Append(Csv(S(r[1]))).Append(',').Append(Csv(S(r[2]))).Append(',')
                          .Append(Csv(S(r[3]))).Append(',').Append(Csv(S(r[4]))).Append(',').Append(Csv(S(r[5]))).Append(',')
                          .Append(Csv(S(r[6]))).Append(',').Append(Csv(S(r[7]))).Append(',').Append(Csv(S(r[8]))).Append(',')
                          .Append(Csv(S(r[9]))).Append("\r\n");
            }
        }
        resp.Clear();
        resp.ContentType = "text/csv";
        resp.AddHeader("Content-Disposition", "attachment; filename=" + def.Name.ToLower() + "_session_" + recordId + ".csv");
        resp.Write(sb.ToString());
        resp.End();
    }

    // ── helpers ──
    private static string User()
    {
        var ctx = HttpContext.Current; var s = ctx != null ? ctx.Session : null;
        if (s == null) return "system";
        return (s["username"] as string) ?? (s["ScreenName"] as string) ?? "system";
    }
    private static string UserName()
    {
        var ctx = HttpContext.Current; var s = ctx != null ? ctx.Session : null;
        if (s == null) return "";
        return (s["ScreenName"] as string) ?? (s["username"] as string) ?? "";
    }
    // NCHE 2015 letter grade from a total mark (pass = 50). "" when no mark.
    private static string GradeFromTotal(object tot)
    {
        if (tot == null || tot == DBNull.Value) return "";
        int t; if (!int.TryParse(tot.ToString(), out t)) return "";
        if (t >= 80) return "A";  if (t >= 75) return "B+"; if (t >= 70) return "B";
        if (t >= 65) return "C+"; if (t >= 60) return "C";  if (t >= 55) return "D+";
        if (t >= 50) return "D";  return "F";
    }
    private static bool IsFail(object tot)
    {
        if (tot == null || tot == DBNull.Value) return false;
        int t; return int.TryParse(tot.ToString(), out t) && t < 50;
    }

    private static int G(Dictionary<string, int> d, string k) { return d.ContainsKey(k) ? d[k] : 0; }
    private static int ToI(object v) { return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v); }
    private static object NI(object v) { return v == null || v == DBNull.Value ? null : (object)Convert.ToInt32(v); }
    private static string S(object v) { return v == null || v == DBNull.Value ? "" : v.ToString(); }
    private static string Csv(string s) { if (s == null) return ""; if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0) return "\"" + s.Replace("\"", "\"\"") + "\""; return s; }
}
