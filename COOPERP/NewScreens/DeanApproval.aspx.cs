using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using System.Web.Configuration;
using MySql.Data.MySqlClient;

/// <summary>
/// DeanApproval — Dean approval dashboard for reviewing and approving/rejecting mark sheets.
///
/// The Dean (or Admin) can:
///   - View queue of all mark sheets filtered by programme/year/semester/status
///   - Review a specific sheet (read-only mark table view)
///   - Approve a submitted sheet (SUBMITTED → DEAN_APPROVED)
///   - Reject a submitted sheet with a reason (SUBMITTED → DRAFT)
///
/// AJAX Endpoints:
///   ?ajax=dropdowns  — Load filter dropdowns (programmes, academic years)
///   ?ajax=queue      — Load sheet queue with filters + counts
///   ?ajax=review     — Load full sheet data for review (read-only)
///   ?ajax=approve    — Approve a submitted sheet
///   ?ajax=reject     — Reject a submitted sheet with reason
///   ?ajax=stats      — Get approval statistics
///
/// Security:
///   - Access restricted to MarksAuthorizationService.CanApproveMarks()
///   - Approve/reject delegate to MarksWorkflowService
///   - All actions are audited via MarksAuditService
///
/// Design: C# 5 compatible (no ?. operator, no string interpolation).
/// Task: E-07
/// </summary>
public partial class COOPERP_NewScreens_DeanApproval : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return WebConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        EnsureTables();

        // Session integrity check (C-05)
        string sessionErr = MarksSessionSecurity.ValidateSessionIntegrity();
        if (sessionErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(sessionErr) + "\"}");
            return;
        }

        // Authorization: Dean/Admin only
        if (!MarksAuthorizationService.CanApproveMarks())
        {
            WriteJson("{\"error\":\"Access denied. Dean or administrator role required.\"}");
            return;
        }

        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (string.IsNullOrEmpty(ajax)) return;

        try
        {
            // CSRF validation for write operations
            if (ajax == "approve" || ajax == "reject" || ajax == "bulk_approve")
            {
                if (!MarksAntiForgeryService.ValidateRequest())
                {
                    MarksAntiForgeryService.RejectRequest(Response);
                    return;
                }
            }

            if (ajax == "dropdowns") { HandleDropdowns(); return; }
            if (ajax == "queue")     { HandleQueue(); return; }
            if (ajax == "review")    { HandleReview(); return; }
            if (ajax == "approve")   { HandleApprove(); return; }
            if (ajax == "reject")    { HandleReject(); return; }
            if (ajax == "stats")     { HandleStats(); return; }
            if (ajax == "bulk_approve") { HandleBulkApprove(); return; }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            WriteJson(MarksErrorHandler.HandleException(ex, "DeanApproval", ajax));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Dropdowns (?ajax=dropdowns)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleDropdowns()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // Programmes
                sb.Append("\"programmes\":[");
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT DISTINCT progcode, progname FROM acad_programme
                      WHERE progcode IN (SELECT DISTINCT progid FROM acad_results_status)
                      ORDER BY progname", conn))
                {
                    int pi = 0;
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            if (pi > 0) sb.Append(",");
                            sb.Append("{\"code\":\"");
                            sb.Append(JsEsc(rdr["progcode"].ToString()));
                            sb.Append("\",\"name\":\"");
                            sb.Append(JsEsc(rdr["progname"].ToString()));
                            sb.Append("\"}");
                            pi++;
                        }
                    }
                }
                sb.Append("]");

                // Academic years
                sb.Append(",\"years\":[");
                using (MySqlCommand cmd2 = new MySqlCommand(
                    @"SELECT DISTINCT acadyear FROM acad_results_status
                      ORDER BY acadyear DESC", conn))
                {
                    int yi = 0;
                    using (MySqlDataReader rdr = cmd2.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            if (yi > 0) sb.Append(",");
                            sb.Append("\"");
                            sb.Append(JsEsc(rdr["acadyear"].ToString()));
                            sb.Append("\"");
                            yi++;
                        }
                    }
                }
                sb.Append("]");
            }
        }
        catch (Exception ex)
        {
            sb.Clear();
            sb.Append("{\"error\":\"");
            sb.Append(JsEsc(ex.Message));
            sb.Append("\"");
        }

        sb.Append("}");
        WriteJson(sb.ToString());
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Queue (?ajax=queue)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleQueue()
    {
        string prog     = (Request.Form["prog"] ?? "").Trim();
        string year     = (Request.Form["year"] ?? "").Trim();
        string semStr   = (Request.Form["sem"] ?? "").Trim();
        string status   = (Request.Form["status"] ?? "").Trim();
        string search   = (Request.Form["search"] ?? "").Trim().ToLower();

        int sem = 0;
        int.TryParse(semStr, out sem);

        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");

            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // ── Counts ────────────────────────────────────────────────
                int pendingCount = 0, approvedCount = 0, rejectedCount = 0, totalCount = 0;

                using (MySqlCommand cntCmd = new MySqlCommand(
                    @"SELECT rs.status, COUNT(*) AS cnt
                      FROM acad_results_status rs
                      WHERE (@prog = '' OR rs.progid = @prog)
                        AND (@year = '' OR rs.acadyear = @year)
                        AND (@sem = 0 OR rs.semester = @sem)
                      GROUP BY rs.status", conn))
                {
                    cntCmd.Parameters.AddWithValue("@prog", prog);
                    cntCmd.Parameters.AddWithValue("@year", year);
                    cntCmd.Parameters.AddWithValue("@sem", sem);

                    using (MySqlDataReader rdr = cntCmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string st = rdr["status"].ToString();
                            int cnt = Convert.ToInt32(rdr["cnt"]);
                            totalCount += cnt;
                            if (st == "SUBMITTED") pendingCount = cnt;
                            else if (st == "DEAN_APPROVED" || st == "PROVISIONAL_PUBLISHED" || st == "FINAL_PUBLISHED") approvedCount += cnt;
                            else if (st == "DRAFT") rejectedCount += cnt; // Draft after rejection
                        }
                    }
                }

                sb.Append("\"pendingCount\":");  sb.Append(pendingCount);
                sb.Append(",\"approvedCount\":"); sb.Append(approvedCount);
                sb.Append(",\"rejectedCount\":"); sb.Append(rejectedCount);
                sb.Append(",\"totalCount\":");    sb.Append(totalCount);

                // ── Queue items ───────────────────────────────────────────
                string sql = @"SELECT rs.course_id, rs.progid, rs.acadyear, rs.semester,
                                      rs.study_year, rs.campus_id, rs.stud_session,
                                      rs.status, rs.submitted_by, rs.submitted_at,
                                      rs.approved_by, rs.approved_at, rs.reject_reason,
                                      COALESCE(c.CourseName, rs.course_id) AS course_name,
                                      COALESCE(p.progname, rs.progid) AS prog_name,
                                      COALESCE(emp.emp_name, rs.submitted_by) AS teacher_name,
                                      COALESCE(cnt.total_students, 0) AS total_students,
                                      COALESCE(cnt.marks_entered, 0) AS marks_entered
                               FROM acad_results_status rs
                               LEFT JOIN acad_courses c ON c.CourseCode = rs.course_id
                               LEFT JOIN acad_programme p ON p.progcode = rs.progid
                               LEFT JOIN hrm_employee emp ON emp.usernames = rs.submitted_by
                               LEFT JOIN (
                                   SELECT course_id, progid, acad_year, semester, study_year, campus, stud_session,
                                          COUNT(*) AS total_students,
                                          SUM(CASE WHEN COALESCE(cw_mark_entered,0) > 0 OR COALESCE(test_mark_entered,0) > 0
                                                        OR COALESCE(ex_mark_entered,0) > 0 THEN 1 ELSE 0 END) AS marks_entered
                                   FROM acad_examresults_faculty
                                   GROUP BY course_id, progid, acad_year, semester, study_year, campus, stud_session
                               ) cnt ON cnt.course_id = rs.course_id AND cnt.progid = rs.progid
                                     AND cnt.acad_year = rs.acadyear AND cnt.semester = rs.semester
                                     AND cnt.study_year = rs.study_year AND cnt.campus = rs.campus_id
                                     AND cnt.stud_session = rs.stud_session
                               WHERE (@prog = '' OR rs.progid = @prog)
                                 AND (@year = '' OR rs.acadyear = @year)
                                 AND (@sem = 0 OR rs.semester = @sem)
                                 AND (@status = '' OR rs.status = @status)
                               ORDER BY CASE rs.status
                                   WHEN 'SUBMITTED' THEN 1
                                   WHEN 'DRAFT' THEN 2
                                   WHEN 'DEAN_APPROVED' THEN 3
                                   WHEN 'PROVISIONAL_PUBLISHED' THEN 4
                                   WHEN 'FINAL_PUBLISHED' THEN 5
                                   ELSE 6 END,
                                   rs.submitted_at DESC
                               LIMIT 200";

                sb.Append(",\"items\":[");
                int idx = 0;

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@prog", prog);
                    cmd.Parameters.AddWithValue("@year", year);
                    cmd.Parameters.AddWithValue("@sem", sem);
                    cmd.Parameters.AddWithValue("@status", status);

                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string itemCourseId = rdr["course_id"].ToString();
                            string itemCourseName = rdr["course_name"].ToString();
                            string itemProgName = rdr["prog_name"].ToString();
                            string itemTeacher = rdr["teacher_name"].ToString();
                            string itemSubmittedBy = rdr["submitted_by"] != DBNull.Value ? rdr["submitted_by"].ToString() : "";

                            // Apply text search filter
                            if (!string.IsNullOrEmpty(search))
                            {
                                string searchable = String.Format("{0} {1} {2} {3}",
                                    itemCourseId, itemCourseName, itemProgName, itemTeacher).ToLower();
                                if (!searchable.Contains(search)) continue;
                            }

                            if (idx > 0) sb.Append(",");
                            sb.Append("{");
                            sb.Append("\"courseId\":\""); sb.Append(JsEsc(itemCourseId)); sb.Append("\"");
                            sb.Append(",\"courseName\":\""); sb.Append(JsEsc(itemCourseName)); sb.Append("\"");
                            sb.Append(",\"progId\":\""); sb.Append(JsEsc(rdr["progid"].ToString())); sb.Append("\"");
                            sb.Append(",\"progName\":\""); sb.Append(JsEsc(itemProgName)); sb.Append("\"");
                            sb.Append(",\"acadyear\":\""); sb.Append(JsEsc(rdr["acadyear"].ToString())); sb.Append("\"");
                            sb.Append(",\"semester\":"); sb.Append(Convert.ToInt32(rdr["semester"]));
                            sb.Append(",\"studyYear\":"); sb.Append(Convert.ToInt32(rdr["study_year"]));
                            sb.Append(",\"campusId\":"); sb.Append(Convert.ToInt32(rdr["campus_id"]));
                            sb.Append(",\"studSession\":\""); sb.Append(JsEsc(rdr["stud_session"].ToString())); sb.Append("\"");
                            sb.Append(",\"status\":\""); sb.Append(JsEsc(rdr["status"].ToString())); sb.Append("\"");
                            sb.Append(",\"submittedBy\":\""); sb.Append(JsEsc(itemSubmittedBy)); sb.Append("\"");
                            sb.Append(",\"teacherName\":\""); sb.Append(JsEsc(itemTeacher)); sb.Append("\"");
                            sb.Append(",\"totalStudents\":"); sb.Append(Convert.ToInt32(rdr["total_students"]));
                            sb.Append(",\"marksEntered\":"); sb.Append(Convert.ToInt32(rdr["marks_entered"]));

                            if (rdr["submitted_at"] != DBNull.Value)
                            {
                                DateTime submAt = Convert.ToDateTime(rdr["submitted_at"]);
                                sb.Append(",\"submittedAt\":\"");
                                sb.Append(submAt.ToString("MMM d, yyyy HH:mm"));
                                sb.Append("\"");
                            }
                            else
                            {
                                sb.Append(",\"submittedAt\":null");
                            }

                            if (rdr["reject_reason"] != DBNull.Value)
                            {
                                sb.Append(",\"rejectReason\":\"");
                                sb.Append(JsEsc(rdr["reject_reason"].ToString()));
                                sb.Append("\"");
                            }

                            sb.Append("}");
                            idx++;
                        }
                    }
                }
                sb.Append("]");
            }

            sb.Append("}");
            WriteJson(sb.ToString());
        }
        catch (Exception ex)
        {
            WriteJson("{\"error\":\"" + JsEsc("Queue load failed: " + ex.Message) + "\"}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Review (?ajax=review)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleReview()
    {
        string courseId    = (Request.Form["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();

        if (string.IsNullOrEmpty(courseId) || string.IsNullOrEmpty(progId) || string.IsNullOrEmpty(acadyear))
        {
            WriteJson("{\"error\":\"Missing required parameters.\"}");
            return;
        }

        try
        {
            // Load sheet data via MarksSheetService
            MarksSheetService.SheetData sheet = MarksSheetService.LoadSheet(
                courseId, progId, acadyear, semester, studyYear, campusId, studSession, "");

            if (sheet.Error != null)
            {
                WriteJson("{\"error\":\"" + JsEsc(sheet.Error) + "\"}");
                return;
            }

            // Get status info
            ResultsStatusService.StatusInfo statusInfo = ResultsStatusService.GetStatusInfo(
                courseId, progId, acadyear, semester, studyYear, campusId, studSession);

            // Build JSON
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"cwRatio\":"); sb.Append(sheet.CwRatio);
            sb.Append(",\"testRatio\":"); sb.Append(sheet.TestRatio);
            sb.Append(",\"examRatio\":"); sb.Append(sheet.ExamRatio);
            sb.Append(",\"totalStudents\":"); sb.Append(sheet.TotalStudents);
            sb.Append(",\"marksEntered\":"); sb.Append(sheet.MarksEnteredCount);
            sb.Append(",\"expectedStudents\":"); sb.Append(sheet.ExpectedStudentCount);
            sb.Append(",\"status\":\""); sb.Append(JsEsc(statusInfo.Status)); sb.Append("\"");

            if (statusInfo.SubmittedBy != null)
            {
                sb.Append(",\"submittedBy\":\""); sb.Append(JsEsc(statusInfo.SubmittedBy)); sb.Append("\"");
            }

            // Student rows
            sb.Append(",\"rows\":[");
            List<MarksSheetService.GradeBoundary> scale = MarksSheetService.GetGradingScale(progId);

            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                MarksSheetService.MarkRow row = sheet.Rows[i];
                if (i > 0) sb.Append(",");
                sb.Append("{\"regno\":\""); sb.Append(JsEsc(row.Regno)); sb.Append("\"");
                sb.Append(",\"studentName\":\""); sb.Append(JsEsc(row.StudentName)); sb.Append("\"");
                sb.Append(",\"cwEntered\":"); sb.Append(row.CwEntered);
                sb.Append(",\"testEntered\":"); sb.Append(row.TestEntered);
                sb.Append(",\"examEntered\":"); sb.Append(row.ExamEntered);
                sb.Append(",\"cwMark\":"); sb.Append(row.CwMark);
                sb.Append(",\"testMark\":"); sb.Append(row.TestMark);
                sb.Append(",\"examMark\":"); sb.Append(row.ExamMark);
                sb.Append(",\"totalMark\":"); sb.Append(row.TotalMark);

                // Compute grade if not stored
                string grade = row.Grade;
                if (string.IsNullOrEmpty(grade) && row.TotalMark > 0)
                {
                    grade = MarksSheetService.ComputeGrade(row.TotalMark, scale);
                }
                sb.Append(",\"grade\":\""); sb.Append(JsEsc(grade)); sb.Append("\"");
                sb.Append("}");
            }
            sb.Append("]");

            sb.Append("}");
            WriteJson(sb.ToString());
        }
        catch (Exception ex)
        {
            WriteJson("{\"error\":\"" + JsEsc("Review load failed: " + ex.Message) + "\"}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Approve (?ajax=approve)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleApprove()
    {
        // C-02: Per-handler role guard
        if (!MarksAuthorizationService.CanApproveMarks())
        {
            WriteJson("{\"error\":\"Access denied. Approval requires Dean or administrator role.\"}");
            return;
        }

        string courseId    = (Request.Form["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();

        // C-04: Input validation
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        MarksWorkflowService.WorkflowResult wr = MarksWorkflowService.ApproveSheet(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession);

        // F-04: Notify the submitting teacher of approval
        if (wr.Success)
        {
            string approver = MarksAuthorizationService.GetCurrentUser();
            MarksNotificationService.NotifyApproval(courseId, progId, acadyear, semester, approver);
        }

        WriteJson(MarksWorkflowService.ToJson(wr));
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Reject (?ajax=reject)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleReject()
    {
        // C-02: Per-handler role guard
        if (!MarksAuthorizationService.CanApproveMarks())
        {
            WriteJson("{\"error\":\"Access denied. Rejection requires Dean or administrator role.\"}");
            return;
        }

        string courseId    = (Request.Form["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();
        string reason      = MarksInputValidator.Sanitize(Request.Form["reason"] ?? "", 500);

        // C-04: Input validation
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        if (string.IsNullOrEmpty(reason))
        {
            WriteJson("{\"error\":\"A reason is required when rejecting marks.\"}");
            return;
        }

        MarksWorkflowService.WorkflowResult wr = MarksWorkflowService.RejectSheet(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession, reason);

        // F-04: Notify the submitting teacher of rejection with reason
        if (wr.Success)
        {
            string rejector = MarksAuthorizationService.GetCurrentUser();
            MarksNotificationService.NotifyRejection(courseId, progId, acadyear, semester, rejector, reason);
        }

        WriteJson(MarksWorkflowService.ToJson(wr));
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Stats (?ajax=stats)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleStats()
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                StringBuilder sb = new StringBuilder();
                sb.Append("{");

                // ── Status counts ─────────────────────────────────────
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT status, COUNT(*) AS cnt FROM acad_results_status GROUP BY status", conn))
                {
                    int pending = 0, approved = 0, rejected = 0, total = 0;
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string st = rdr["status"].ToString();
                            int cnt = Convert.ToInt32(rdr["cnt"]);
                            total += cnt;
                            if (st == "SUBMITTED") pending += cnt;
                            if (st == "DEAN_APPROVED" || st == "PROVISIONAL_PUBLISHED" || st == "FINAL_PUBLISHED") approved += cnt;
                        }
                    }
                    sb.Append("\"pending\":"); sb.Append(pending);
                    sb.Append(",\"approved\":"); sb.Append(approved);
                    sb.Append(",\"total\":"); sb.Append(total);
                }

                // ── Reviewed today (Batch 12) ────────────────────────
                int reviewedToday = 0;
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT COUNT(*) FROM acad_marks_audit
                      WHERE action_type IN ('APPROVE','REJECT')
                        AND DATE(change_date) = CURDATE()", conn))
                {
                    reviewedToday = Convert.ToInt32(cmd.ExecuteScalar());
                }
                sb.AppendFormat(",\"reviewed_today\":{0}", reviewedToday);

                // ── Average turnaround hours (Batch 12) ──────────────
                int avgHours = 0;
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT ROUND(AVG(
                        TIMESTAMPDIFF(HOUR, rs.submitted_at, rs.approved_at)
                      )) AS avg_h
                      FROM acad_results_status rs
                      WHERE rs.submitted_at IS NOT NULL
                        AND rs.approved_at IS NOT NULL
                        AND rs.approved_at > rs.submitted_at", conn))
                {
                    object val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value)
                    {
                        avgHours = Convert.ToInt32(val);
                    }
                }
                sb.AppendFormat(",\"avg_turnaround_hours\":{0}", avgHours);

                sb.Append("}");
                WriteJson(sb.ToString());
            }
        }
        catch (Exception ex)
        {
            WriteJson("{\"error\":\"" + JsEsc(ex.Message) + "\"}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Bulk Approve (?ajax=bulk_approve) — Batch 12
    // ═════════════════════════════════════════════════════════════════════

    private void HandleBulkApprove()
    {
        if (!MarksAuthorizationService.CanApproveMarks())
        {
            WriteJson("{\"error\":\"Access denied. Bulk approval requires Dean or administrator role.\"}");
            return;
        }

        string bulkJson = (Request.Form["bulkItems"] ?? "").Trim();
        if (string.IsNullOrEmpty(bulkJson))
        {
            WriteJson("{\"error\":\"No items selected.\"}");
            return;
        }

        try
        {
            var jss = new System.Web.Script.Serialization.JavaScriptSerializer();
            var items = jss.Deserialize<List<Dictionary<string, object>>>(bulkJson);

            if (items == null || items.Count == 0)
            {
                WriteJson("{\"error\":\"No items to approve.\"}");
                return;
            }

            if (items.Count > 50)
            {
                WriteJson("{\"error\":\"Maximum 50 sheets per bulk approval.\"}");
                return;
            }

            string approver = MarksAuthorizationService.GetCurrentUser();
            int approvedCount = 0;
            int failedCount = 0;
            List<string> errors = new List<string>();

            foreach (var item in items)
            {
                string courseId    = item.ContainsKey("courseId") ? Convert.ToString(item["courseId"]) : "";
                string progId      = item.ContainsKey("progId") ? Convert.ToString(item["progId"]) : "";
                string acadyear    = item.ContainsKey("acadyear") ? Convert.ToString(item["acadyear"]) : "";
                int semester       = item.ContainsKey("semester") ? Convert.ToInt32(item["semester"]) : 0;
                int studyYear      = item.ContainsKey("studyYear") ? Convert.ToInt32(item["studyYear"]) : 1;
                int campusId       = item.ContainsKey("campusId") ? Convert.ToInt32(item["campusId"]) : 1;
                string studSession = item.ContainsKey("studSession") ? Convert.ToString(item["studSession"]) : "Day";

                if (string.IsNullOrEmpty(courseId) || string.IsNullOrEmpty(progId))
                {
                    failedCount++;
                    errors.Add("Skipped item with missing course/programme");
                    continue;
                }

                MarksWorkflowService.WorkflowResult wr = MarksWorkflowService.ApproveSheet(
                    courseId, progId, acadyear, semester, studyYear, campusId, studSession);

                if (wr.Success)
                {
                    approvedCount++;
                    MarksNotificationService.NotifyApproval(courseId, progId, acadyear, semester, approver);
                }
                else
                {
                    failedCount++;
                    errors.Add(String.Format("{0}: {1}", courseId, wr.Error ?? "Unknown error"));
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("{\"ok\":true");
            sb.AppendFormat(",\"approved\":{0}", approvedCount);
            sb.AppendFormat(",\"failed\":{0}", failedCount);
            sb.Append(",\"errors\":[");
            for (int i = 0; i < errors.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.AppendFormat("\"{0}\"", JsEsc(errors[i]));
            }
            sb.Append("]}");
            WriteJson(sb.ToString());
        }
        catch (Exception ex)
        {
            WriteJson("{\"error\":\"Bulk approve failed: " + JsEsc(ex.Message) + "\"}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════════════════════════════

    private void EnsureTables()
    {
        ResultsStatusService.EnsureStatusTable();
        MarksActionLogger.EnsureActionLogTable();
    }

    private void WriteJson(string json)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        Response.Write(json);
        Response.End();
    }

    private static int ToInt(string s, int def)
    {
        int val;
        return int.TryParse(s, out val) ? val : def;
    }

    private static string JsEsc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
