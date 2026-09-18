using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

/// <summary>
/// MarkEntry — Dedicated mark entry page with full-table editing.
///
/// Provides a keyboard-friendly bulk editing experience where teachers
/// enter CW, Test, and Exam marks for all students at once with
/// real-time weighted mark calculation and grade preview (E-03).
///
/// AJAX Endpoints:
///   ?ajax=load            — Load sheet data with rows, ratios, grading scale, lock state
///   ?ajax=save            — POST: Bulk save marks (dirty rows only)
///   ?ajax=submit_preview  — Get pre-submission summary (student count, missing, avg, pass rate)
///   ?ajax=submit          — POST: Submit marks for Dean review via MarksWorkflowService
///
/// URL Parameters:
///   course, prog, year, sem, sy, campus, session
///
/// Security:
///   - Authorization via MarksAuthorizationService.CanEnterMarks()
///   - Assignment check via MarksAuthorizationService.IsAssignedToCourse()
///   - Lock enforcement via MarksLockService.GetLockState()
///   - Status checks via ResultsStatusService
///
/// Design: C# 5 compatible (no ?. operator, no string interpolation).
/// Tasks: E-02, E-03
/// </summary>
public partial class COOPERP_NewScreens_MarkEntry : System.Web.UI.Page
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

        // Authorization gate
        if (!MarksAuthorizationService.CanEnterMarks() && !MarksAuthorizationService.CanApproveMarks())
        {
            WriteJson("{\"error\":\"Access denied. You do not have marks module access.\"}");
            return;
        }

        string ajax = (Request.QueryString["ajax"] ?? "").Trim().ToLower();
        if (string.IsNullOrEmpty(ajax)) return;

        System.Diagnostics.Stopwatch _actionTimer = MarksActionLogger.StartTimer();
        string _actionOutcome = MarksActionLogger.OUTCOME_SUCCESS;

        try
        {
            // CSRF validation for write operations
            if (ajax == "save" || ajax == "import" || ajax == "submit" || ajax == "request_unlock" || ajax == "sync")
            {
                if (!MarksAntiForgeryService.ValidateRequest())
                {
                    _actionOutcome = MarksActionLogger.OUTCOME_AUTH_FAIL;
                    MarksAntiForgeryService.RejectRequest(Response);
                    return;
                }
            }

            if (ajax == "load")            { HandleLoad(); return; }
            if (ajax == "save")            { HandleSave(); return; }
            if (ajax == "import")          { HandleImport(); return; }
            if (ajax == "submit_preview")  { HandleSubmitPreview(); return; }
            if (ajax == "submit")          { HandleSubmit(); return; }
            if (ajax == "request_unlock")  { HandleRequestUnlock(); return; }
            if (ajax == "reconcile")       { HandleReconcile(); return; }
            if (ajax == "sync")            { HandleSync(); return; }
            if (ajax == "export_csv")      { HandleExportCsv(); return; }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            _actionOutcome = MarksActionLogger.OUTCOME_ERROR;
            WriteJson(MarksErrorHandler.HandleException(ex, "MarkEntry", ajax));
        }
        finally
        {
            MarksActionLogger.StopAndLog(_actionTimer, "MarkEntry", ajax, _actionOutcome, null, null);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Load Sheet (?ajax=load)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleLoad()
    {
        string courseId    = (Request.Form["course"] ?? Request.QueryString["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? Request.QueryString["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? Request.QueryString["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"] ?? Request.QueryString["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"] ?? Request.QueryString["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"] ?? Request.QueryString["campus"], 1);
        string studSession = (Request.Form["session"] ?? Request.QueryString["session"] ?? "Day").Trim();

        // Input validation (C-04)
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        // Authorization: check assignment
        string user = MarksAuthorizationService.GetCurrentUser();
        if (!MarksAuthorizationService.IsAssignedToCourse(user, courseId, progId, acadyear, semester))
        {
            WriteJson("{\"error\":\"You are not assigned to this course. Contact your administrator.\"}");
            return;
        }

        try
        {
            // ── Load sheet data ───────────────────────────────────────────
            MarksSheetService.SheetData sheet = MarksSheetService.LoadSheet(
                courseId, progId, acadyear, semester, studyYear, campusId, studSession, "");

            if (sheet.Error != null)
            {
                WriteJson("{\"error\":\"" + JsEsc(sheet.Error) + "\"}");
                return;
            }

            // ── Load lock state ───────────────────────────────────────────
            MarksLockService.LockState lockState = MarksLockService.GetLockState(
                courseId, progId, acadyear, semester, studyYear, campusId, studSession);

            // ── Load status info ──────────────────────────────────────────
            ResultsStatusService.StatusInfo statusInfo = ResultsStatusService.GetStatusInfo(
                courseId, progId, acadyear, semester, studyYear, campusId, studSession);

            // ── Load grading scale ────────────────────────────────────────
            List<MarksSheetService.GradeBoundary> scale = MarksSheetService.GetGradingScale(progId);

            // ── Load course and programme names ───────────────────────────
            string courseCode = courseId;
            string courseName = courseId;
            string progName = progId;

            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT COALESCE(pc.CourseName, c.CourseName, @cid) AS cname
                      FROM acad_courses c
                      LEFT JOIN acad_programmecourses pc ON pc.course_code = c.CourseCode AND pc.progcode = @prog
                      WHERE c.CourseCode = @cid LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@cid", courseId);
                    cmd.Parameters.AddWithValue("@prog", progId);
                    object r = cmd.ExecuteScalar();
                    if (r != null && r != DBNull.Value) courseName = r.ToString();
                }

                using (MySqlCommand cmd2 = new MySqlCommand(
                    "SELECT progname FROM acad_programme WHERE progcode = @p LIMIT 1", conn))
                {
                    cmd2.Parameters.AddWithValue("@p", progId);
                    object r2 = cmd2.ExecuteScalar();
                    if (r2 != null && r2 != DBNull.Value) progName = r2.ToString();
                }
            }

            // ── Build JSON response ───────────────────────────────────────
            StringBuilder sb = new StringBuilder();
            sb.Append("{");

            // Context
            sb.Append("\"courseCode\":\""); sb.Append(JsEsc(courseCode)); sb.Append("\"");
            sb.Append(",\"courseName\":\""); sb.Append(JsEsc(courseName)); sb.Append("\"");
            sb.Append(",\"progName\":\""); sb.Append(JsEsc(progName)); sb.Append("\"");
            sb.Append(",\"cwRatio\":"); sb.Append(sheet.CwRatio);
            sb.Append(",\"testRatio\":"); sb.Append(sheet.TestRatio);
            sb.Append(",\"examRatio\":"); sb.Append(sheet.ExamRatio);
            sb.Append(",\"expectedStudents\":"); sb.Append(sheet.ExpectedStudentCount);
            sb.Append(",\"marksEntered\":"); sb.Append(sheet.MarksEnteredCount);
            sb.Append(",\"missingMarks\":"); sb.Append(sheet.TotalStudents - sheet.MarksEnteredCount);

            // Status
            string statusLabel = MarksWorkflowService.GetStatusLabel(statusInfo.Status);
            string statusClass = MarksWorkflowService.GetStatusBadgeClass(statusInfo.Status);
            sb.Append(",\"status\":\""); sb.Append(JsEsc(statusInfo.Status)); sb.Append("\"");
            sb.Append(",\"statusLabel\":\""); sb.Append(JsEsc(statusLabel)); sb.Append("\"");
            sb.Append(",\"statusClass\":\""); sb.Append(JsEsc(statusClass)); sb.Append("\"");

            // Rejection reason
            if (!string.IsNullOrEmpty(statusInfo.RejectReason))
            {
                sb.Append(",\"rejectReason\":\""); sb.Append(JsEsc(statusInfo.RejectReason)); sb.Append("\"");
            }

            // Lock state
            sb.Append(",\"lockState\":"); sb.Append(MarksLockService.ToJson(lockState));

            // Grading scale
            sb.Append(",\"gradingScale\":[");
            for (int i = 0; i < scale.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\"grade\":\""); sb.Append(JsEsc(scale[i].Grade)); sb.Append("\"");
                sb.Append(",\"minMark\":"); sb.Append(scale[i].MinMark);
                sb.Append(",\"maxMark\":"); sb.Append(scale[i].MaxMark);
                sb.Append(",\"gradePoint\":"); sb.Append(scale[i].GradePoint);
                sb.Append("}");
            }
            sb.Append("]");

            // Student rows
            sb.Append(",\"rows\":[");
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                MarksSheetService.MarkRow row = sheet.Rows[i];
                if (i > 0) sb.Append(",");
                sb.Append("{\"id\":"); sb.Append(row.Id);
                sb.Append(",\"regno\":\""); sb.Append(JsEsc(row.Regno)); sb.Append("\"");
                sb.Append(",\"studentName\":\""); sb.Append(JsEsc(row.StudentName)); sb.Append("\"");
                sb.Append(",\"cwEntered\":"); sb.Append(row.CwEntered);
                sb.Append(",\"testEntered\":"); sb.Append(row.TestEntered);
                sb.Append(",\"examEntered\":"); sb.Append(row.ExamEntered);
                sb.Append(",\"cwMark\":"); sb.Append(row.CwMark);
                sb.Append(",\"testMark\":"); sb.Append(row.TestMark);
                sb.Append(",\"examMark\":"); sb.Append(row.ExamMark);
                sb.Append(",\"totalMark\":"); sb.Append(row.TotalMark);
                sb.Append(",\"grade\":\""); sb.Append(JsEsc(row.Grade)); sb.Append("\"");
                sb.Append(",\"approvedBy\":\""); sb.Append(JsEsc(row.ApprovedBy)); sb.Append("\"");
                sb.Append(",\"isApproved\":"); sb.Append(row.IsApproved ? "true" : "false");
                sb.Append("}");
            }
            sb.Append("]");

            // Historical year detection (E-11)
            bool isHistorical = IsHistoricalYear(acadyear);
            sb.Append(",\"isHistorical\":"); sb.Append(isHistorical ? "true" : "false");

            // Provisional marks summary — so lecturer UI can show review status
            ProvisionalSummary provSummary = GetProvisionalSummary(courseId, progId, acadyear, semester);
            sb.Append(",\"provPending\":");   sb.Append(provSummary.Pending);
            sb.Append(",\"provApproved\":");  sb.Append(provSummary.Approved);
            sb.Append(",\"provRejected\":");  sb.Append(provSummary.Rejected);
            sb.Append(",\"provPublished\":"); sb.Append(provSummary.Published);
            sb.Append(",\"provTotal\":");     sb.Append(provSummary.Total);

            sb.Append("}");
            WriteJson(sb.ToString());
        }
        catch (Exception ex)
        {
            WriteJson("{\"error\":\"" + JsEsc("Failed to load sheet: " + ex.Message) + "\"}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Save Marks (?ajax=save)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleSave()
    {
        string courseId    = (Request.Form["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();
        string inputsJson  = (Request.Form["inputs"] ?? "").Trim();

        // Input validation (C-04)
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        // Authorization
        string authError = MarksAuthorizationService.ValidateMarkEntryAuthorization(courseId, progId, acadyear, semester);
        if (authError != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(authError) + "\"}");
            return;
        }

        // Historical year guard (E-11)
        if (IsHistoricalYear(acadyear))
        {
            WriteJson("{\"error\":\"This academic year is archived and read-only. Marks cannot be modified.\"}");
            return;
        }

        // Lock check
        MarksLockService.LockState lockState = MarksLockService.GetLockState(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession);
        if (lockState.IsFullyLocked)
        {
            WriteJson("{\"error\":\"Marks are locked and cannot be edited.\"}");
            return;
        }

        // Parse inputs
        JavaScriptSerializer jss = new JavaScriptSerializer();
        List<Dictionary<string, object>> inputDicts;
        try
        {
            inputDicts = jss.Deserialize<List<Dictionary<string, object>>>(inputsJson);
        }
        catch
        {
            WriteJson("{\"error\":\"Invalid input data format.\"}");
            return;
        }

        if (inputDicts == null || inputDicts.Count == 0)
        {
            WriteJson("{\"error\":\"No mark data provided.\"}");
            return;
        }

        // Load ratios for weighted calculation (H-01: lightweight query, no student rows)
        MarksSheetService.RatioData ratios = MarksSheetService.GetRatios(courseId, progId, acadyear, semester);
        int cwRatio = ratios.CwRatio;
        int testRatio = ratios.TestRatio;
        int examRatio = ratios.ExamRatio;

        // Build MarkInput list
        List<MarksSheetService.MarkInput> inputs = new List<MarksSheetService.MarkInput>();
        foreach (Dictionary<string, object> d in inputDicts)
        {
            MarksSheetService.MarkInput mi = new MarksSheetService.MarkInput();
            mi.RowId = d.ContainsKey("rowId") ? Convert.ToInt32(d["rowId"]) : 0;
            mi.Regno = d.ContainsKey("regno") ? d["regno"].ToString() : "";
            mi.CwEntered = d.ContainsKey("cwEntered") ? Convert.ToInt32(d["cwEntered"]) : 0;
            mi.TestEntered = d.ContainsKey("testEntered") ? Convert.ToInt32(d["testEntered"]) : 0;
            mi.ExamEntered = d.ContainsKey("examEntered") ? Convert.ToInt32(d["examEntered"]) : 0;

            // Component-level lock enforcement
            if (lockState.IsCwLocked)
            {
                // Restore original CW value — don't allow saving locked component
                mi.CwEntered = GetOriginalValue(mi.RowId, "cw_mark_entered");
            }
            if (lockState.IsExamLocked)
            {
                mi.ExamEntered = GetOriginalValue(mi.RowId, "ex_mark_entered");
                mi.TestEntered = GetOriginalValue(mi.RowId, "test_mark_entered");
            }

            inputs.Add(mi);
        }

        // Perform bulk save (audit logging now happens inside BulkSaveMarks transaction — H-02)
        MarksSheetService.SaveResult result = MarksSheetService.BulkSaveMarks(
            courseId, progId, acadyear, semester, cwRatio, testRatio, examRatio, inputs);

        // Build response
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"ok\":"); sb.Append(result.Ok ? "true" : "false");
        sb.Append(",\"savedCount\":"); sb.Append(result.SavedCount);
        if (result.Errors != null && result.Errors.Count > 0)
        {
            sb.Append(",\"errors\":[");
            for (int i = 0; i < result.Errors.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\""); sb.Append(JsEsc(result.Errors[i])); sb.Append("\"");
            }
            sb.Append("]");
        }
        sb.Append("}");
        WriteJson(sb.ToString());
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: CSV Import (?ajax=import) — E-05
    // ═════════════════════════════════════════════════════════════════════

    private void HandleImport()
    {
        string courseId    = (Request.Form["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();
        string inputsJson  = (Request.Form["inputs"] ?? "").Trim();

        // Input validation (C-04)
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        // Authorization
        string authError = MarksAuthorizationService.ValidateMarkEntryAuthorization(courseId, progId, acadyear, semester);
        if (authError != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(authError) + "\"}");
            return;
        }

        // Historical year guard (E-11)
        if (IsHistoricalYear(acadyear))
        {
            WriteJson("{\"error\":\"This academic year is archived and read-only. Marks cannot be imported.\"}");
            return;
        }

        // Lock check
        MarksLockService.LockState lockState = MarksLockService.GetLockState(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession);
        if (lockState.IsFullyLocked)
        {
            WriteJson("{\"error\":\"Marks are locked and cannot be imported.\"}");
            return;
        }

        // Parse inputs
        JavaScriptSerializer jss = new JavaScriptSerializer();
        List<Dictionary<string, object>> inputDicts;
        try
        {
            inputDicts = jss.Deserialize<List<Dictionary<string, object>>>(inputsJson);
        }
        catch
        {
            WriteJson("{\"error\":\"Invalid import data format.\"}");
            return;
        }

        if (inputDicts == null || inputDicts.Count == 0)
        {
            WriteJson("{\"error\":\"No import data provided.\"}");
            return;
        }

        // Load ratios (H-01: lightweight query, no student rows)
        MarksSheetService.RatioData ratios = MarksSheetService.GetRatios(courseId, progId, acadyear, semester);
        int cwRatio = ratios.CwRatio;
        int testRatio = ratios.TestRatio;
        int examRatio = ratios.ExamRatio;

        // Build MarkInput list
        List<MarksSheetService.MarkInput> inputs = new List<MarksSheetService.MarkInput>();
        foreach (Dictionary<string, object> d in inputDicts)
        {
            MarksSheetService.MarkInput mi = new MarksSheetService.MarkInput();
            mi.RowId = d.ContainsKey("rowId") ? Convert.ToInt32(d["rowId"]) : 0;
            mi.Regno = d.ContainsKey("regno") ? d["regno"].ToString() : "";
            mi.CwEntered = d.ContainsKey("cwEntered") ? Convert.ToInt32(d["cwEntered"]) : 0;
            mi.TestEntered = d.ContainsKey("testEntered") ? Convert.ToInt32(d["testEntered"]) : 0;
            mi.ExamEntered = d.ContainsKey("examEntered") ? Convert.ToInt32(d["examEntered"]) : 0;

            // Component-level lock enforcement
            if (lockState.IsCwLocked)
                mi.CwEntered = GetOriginalValue(mi.RowId, "cw_mark_entered");
            if (lockState.IsExamLocked)
            {
                mi.ExamEntered = GetOriginalValue(mi.RowId, "ex_mark_entered");
                mi.TestEntered = GetOriginalValue(mi.RowId, "test_mark_entered");
            }
            inputs.Add(mi);
        }

        // Perform bulk save (same as regular save)
        MarksSheetService.SaveResult result = MarksSheetService.BulkSaveMarks(
            courseId, progId, acadyear, semester, cwRatio, testRatio, examRatio, inputs);

        // Audit each imported row with ACTION_IMPORT
        if (result.Ok)
        {
            foreach (MarksSheetService.MarkInput mi in inputs)
            {
                MarksAuditService.LogImport(
                    mi.Regno, courseId, progId, acadyear, semester,
                    mi.CwEntered, mi.TestEntered, mi.ExamEntered,
                    mi.CwEntered, mi.TestEntered, mi.ExamEntered);
            }
        }

        // Build response
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"ok\":"); sb.Append(result.Ok ? "true" : "false");
        sb.Append(",\"savedCount\":"); sb.Append(result.SavedCount);
        if (result.Errors != null && result.Errors.Count > 0)
        {
            sb.Append(",\"errors\":[");
            for (int i = 0; i < result.Errors.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\""); sb.Append(JsEsc(result.Errors[i])); sb.Append("\"");
            }
            sb.Append("]");
        }
        sb.Append("}");
        WriteJson(sb.ToString());
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Submit Preview (?ajax=submit_preview)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleSubmitPreview()
    {
        string courseId    = (Request.Form["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();

        // Input validation (C-04)
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        // Use workflow service for pre-submission check (without forcing)
        MarksWorkflowService.WorkflowResult wr = MarksWorkflowService.SubmitForReview(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession, false);

        if (wr.Error != null && !wr.RequiresConfirmation)
        {
            WriteJson("{\"error\":\"" + JsEsc(wr.Error) + "\"}");
            return;
        }

        // Return summary data
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"totalStudents\":"); sb.Append(wr.TotalStudents);
        sb.Append(",\"marksEntered\":"); sb.Append(wr.MarksEntered);
        sb.Append(",\"missingMarks\":"); sb.Append(wr.MissingMarks);
        sb.Append(",\"expectedStudents\":"); sb.Append(wr.ExpectedStudents);
        sb.Append(",\"averageMark\":"); sb.Append(wr.AverageMark);
        sb.Append(",\"passRate\":"); sb.Append(wr.PassRate);

        if (wr.Warnings != null && wr.Warnings.Count > 0)
        {
            sb.Append(",\"warnings\":[");
            for (int i = 0; i < wr.Warnings.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\""); sb.Append(JsEsc(wr.Warnings[i])); sb.Append("\"");
            }
            sb.Append("]");
        }

        if (wr.RequiresConfirmation)
        {
            sb.Append(",\"requiresConfirmation\":true");
        }

        sb.Append("}");
        WriteJson(sb.ToString());
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Submit (?ajax=submit)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleSubmit()
    {
        string courseId    = (Request.Form["course"] ?? "").Trim();
        string progId      = (Request.Form["prog"] ?? "").Trim();
        string acadyear    = (Request.Form["year"] ?? "").Trim();
        int semester       = ToInt(Request.Form["sem"], 0);
        int studyYear      = ToInt(Request.Form["sy"], 1);
        int campusId       = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();
        bool force         = (Request.Form["force"] ?? "").Trim().ToLower() == "true";

        // Input validation (C-04)
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        // Historical year guard (E-11)
        if (IsHistoricalYear(acadyear))
        {
            WriteJson("{\"error\":\"This academic year is archived and read-only. Marks cannot be submitted.\"}");
            return;
        }

        MarksWorkflowService.WorkflowResult wr = MarksWorkflowService.SubmitForReview(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession, force);

        // F-04: Notify approvers (Dean/Admin) of the new submission
        if (wr.Success)
        {
            string user = MarksAuthorizationService.GetCurrentUser();
            MarksNotificationService.NotifySubmission(courseId, progId, acadyear, semester, user);

            // Provisional marks bridge: write CW/Exam/Total to acad_course_registration
            // so ExamOfficer/Dean can review in ProvisionalMarksController.aspx
            try
            {
                WriteProvisionalMarks(courseId, progId, acadyear, semester,
                    studyYear, campusId, studSession, user);
            }
            catch { /* non-critical — marks already submitted to workflow */ }
        }

        WriteJson(MarksWorkflowService.ToJson(wr));
    }

    // ═════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════════════════════════════

    // ── F-03: Unlock Request ──────────────────────────────────────────

    /// <summary>
    /// Handles teacher-initiated unlock request for deadline-locked marksheets.
    /// Inserts a PENDING request into acad_mark_unlock_requests.
    /// The Dean/Registrar reviews via DeadlineManager's HandleReview endpoint.
    /// </summary>
    private void HandleRequestUnlock()
    {
        string courseId     = (Request.Form["course"] ?? "").Trim();
        string progId       = (Request.Form["prog"] ?? "").Trim();
        string acadyear     = (Request.Form["year"] ?? "").Trim();
        int semester        = ToInt(Request.Form["sem"], 0);
        string deadlineType = (Request.Form["deadline_type"] ?? "").Trim().ToUpper();
        string reason       = (Request.Form["reason"] ?? "").Trim();

        // Input validation (C-04)
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        // Validate deadline type
        if (deadlineType != "COURSEWORK" && deadlineType != "EXAM" && deadlineType != "SUBMISSION")
        {
            WriteJson("{\"error\":\"Invalid deadline type. Must be COURSEWORK, EXAM, or SUBMISSION.\"}");
            return;
        }

        if (string.IsNullOrEmpty(reason) || reason.Length < 10)
        {
            WriteJson("{\"error\":\"Please provide a detailed reason (at least 10 characters).\"}");
            return;
        }

        string user = MarksAuthorizationService.GetCurrentUser();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // Check for existing pending request of same type
                using (MySqlCommand chk = new MySqlCommand(
                    @"SELECT COUNT(*) FROM acad_mark_unlock_requests
                      WHERE requested_by = @user AND course_id = @cid AND acadyear = @year
                        AND semester = @sem AND deadline_type = @dtype AND status = 'PENDING'", conn))
                {
                    chk.Parameters.AddWithValue("@user", user);
                    chk.Parameters.AddWithValue("@cid", courseId);
                    chk.Parameters.AddWithValue("@year", acadyear);
                    chk.Parameters.AddWithValue("@sem", semester);
                    chk.Parameters.AddWithValue("@dtype", deadlineType);
                    int existing = Convert.ToInt32(chk.ExecuteScalar());
                    if (existing > 0)
                    {
                        WriteJson("{\"error\":\"You already have a pending unlock request for this deadline type.\"}");
                        return;
                    }
                }

                // Insert the unlock request
                using (MySqlCommand cmd = new MySqlCommand(
                    @"INSERT INTO acad_mark_unlock_requests
                        (requested_by, course_id, progid, acadyear, semester, deadline_type, reason)
                      VALUES (@user, @cid, @prog, @year, @sem, @dtype, @reason)", conn))
                {
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@cid", courseId);
                    cmd.Parameters.AddWithValue("@prog", progId);
                    cmd.Parameters.AddWithValue("@year", acadyear);
                    cmd.Parameters.AddWithValue("@sem", semester);
                    cmd.Parameters.AddWithValue("@dtype", deadlineType);
                    cmd.Parameters.AddWithValue("@reason", reason);
                    cmd.ExecuteNonQuery();
                }
            }

            // Audit the unlock request
            MarksAuditService.LogUnlock(courseId, progId, acadyear, semester,
                user, "Unlock requested (" + deadlineType + "): " + reason);

            WriteJson("{\"ok\":true}");
        }
        catch (Exception ex)
        {
            WriteJson("{\"error\":\"" + JsEsc("Failed to submit unlock request: " + ex.Message) + "\"}");
        }
    }

    // ── E-11: Historical Year Detection ────────────────────────────────

    /// <summary>
    /// Determines if the given academic year is historical (completed).
    /// A year is historical if it precedes the current academic year.
    /// Uses AcademicYearHelper.GetCurrentAcademicYear() for the comparison.
    /// </summary>
    private bool IsHistoricalYear(string acadyear)
    {
        try
        {
            string currentYear = AcademicYearHelper.GetCurrentAcademicYear();
            if (string.IsNullOrEmpty(currentYear) || string.IsNullOrEmpty(acadyear)) return false;
            // "YYYY/YYYY" format — ordinal comparison works correctly
            return String.Compare(acadyear.Trim(), currentYear.Trim(), StringComparison.Ordinal) < 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the original DB value for a specific column of a row.
    /// Used when a component is locked — we preserve the original value.
    /// </summary>
    private int GetOriginalValue(int rowId, string column)
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(
                    String.Format("SELECT COALESCE({0}, 0) FROM acad_examresults_faculty WHERE id = @id", column), conn))
                {
                    cmd.Parameters.AddWithValue("@id", rowId);
                    object result = cmd.ExecuteScalar();
                    return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
                }
            }
        }
        catch
        {
            return 0;
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Sheet Sync (?ajax=sync)  (H-04)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleSync()
    {
        string courseId = (Request.Form["course"] ?? "").Trim();
        string progId = (Request.Form["prog"] ?? "").Trim();
        string acadyear = (Request.Form["year"] ?? "").Trim();
        int semester = ToInt(Request.Form["sem"], 0);
        int studyYear = ToInt(Request.Form["sy"], 1);
        int campusId = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();

        // Validation
        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        // Authorization — only users who can enter marks may sync sheets
        string authError = MarksAuthorizationService.ValidateMarkEntryAuthorization(courseId, progId, acadyear, semester);
        if (authError != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(authError) + "\"}");
            return;
        }

        MarksSheetSyncService.SyncResult result = MarksSheetSyncService.SyncSheet(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession);

        if (!result.Ok)
        {
            WriteJson("{\"error\":\"" + JsEsc(result.Error) + "\"}");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("{\"ok\":true");
        sb.Append(",\"added\":").Append(result.AddedCount);
        sb.Append(",\"existing\":").Append(result.AlreadyExistCount);
        sb.Append(",\"total\":").Append(result.TotalAfterSync);
        sb.Append("}");
        WriteJson(sb.ToString());
    }

    // ═════════════════════════════════════════════════════════════════════
    // AJAX: Reconcile (?ajax=reconcile)  (B-06)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleReconcile()
    {
        string courseId = (Request.Form["course"] ?? "").Trim();
        string progId = (Request.Form["prog"] ?? "").Trim();
        string acadyear = (Request.Form["year"] ?? "").Trim();
        int semester = ToInt(Request.Form["sem"], 0);
        int studyYear = ToInt(Request.Form["sy"], 1);
        int campusId = ToInt(Request.Form["campus"], 1);
        string studSession = (Request.Form["session"] ?? "Day").Trim();

        if (string.IsNullOrEmpty(courseId) || string.IsNullOrEmpty(progId) || string.IsNullOrEmpty(acadyear))
        {
            WriteJson("{\"error\":\"Missing context parameters for reconciliation.\"}");
            return;
        }

        MarksReconciliationService.ReconciliationResult result =
            MarksReconciliationService.Reconcile(courseId, progId, acadyear, semester, studyYear, campusId, studSession);

        if (!string.IsNullOrEmpty(result.Error))
        {
            WriteJson("{\"error\":\"" + JsEsc(result.Error) + "\"}");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("{\"registered\":").Append(result.RegisteredCount);
        sb.Append(",\"in_sheet\":").Append(result.InSheetCount);

        // Missing list
        sb.Append(",\"missing\":[");
        for (int i = 0; i < result.Missing.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"regno\":\"").Append(JsEsc(result.Missing[i].Regno));
            sb.Append("\",\"name\":\"").Append(JsEsc(result.Missing[i].StudentName));
            sb.Append("\",\"status\":\"").Append(JsEsc(result.Missing[i].RegStatus)).Append("\"}");
        }
        sb.Append("]");

        // Extra list
        sb.Append(",\"extra\":[");
        for (int i = 0; i < result.Extra.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"regno\":\"").Append(JsEsc(result.Extra[i].Regno));
            sb.Append("\",\"name\":\"").Append(JsEsc(result.Extra[i].StudentName));
            sb.Append("\",\"has_marks\":").Append(result.Extra[i].HasMarks ? "true" : "false").Append("}");
        }
        sb.Append("]");

        // Matched list
        sb.Append(",\"matched\":[");
        for (int i = 0; i < result.Matched.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("{\"regno\":\"").Append(JsEsc(result.Matched[i].Regno));
            sb.Append("\",\"name\":\"").Append(JsEsc(result.Matched[i].StudentName)).Append("\"}");
        }
        sb.Append("]}");

        WriteJson(sb.ToString());
    }

    // ═════════════════════════════════════════════════════════════════════
    // Export CSV (?ajax=export_csv)  — GET, read-only, no CSRF (Batch 11)
    // ═════════════════════════════════════════════════════════════════════

    private void HandleExportCsv()
    {
        string courseId    = (Request.QueryString["course"]  ?? "").Trim();
        string progId      = (Request.QueryString["prog"]    ?? "").Trim();
        string acadyear    = (Request.QueryString["year"]    ?? "").Trim();
        int semester       = ToInt(Request.QueryString["sem"],    0);
        int studyYear      = ToInt(Request.QueryString["sy"],     1);
        int campusId       = ToInt(Request.QueryString["campus"], 1);
        string studSession = (Request.QueryString["session"] ?? "Day").Trim();

        string valErr = MarksInputValidator.ValidateMarkContext(courseId, progId, acadyear, semester);
        if (valErr != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(valErr) + "\"}");
            return;
        }

        string user = MarksAuthorizationService.GetCurrentUser();
        if (!MarksAuthorizationService.IsAssignedToCourse(user, courseId, progId, acadyear, semester))
        {
            WriteJson("{\"error\":\"You are not assigned to this course.\"}"  );
            return;
        }

        MarksSheetService.SheetData sheet = MarksSheetService.LoadSheet(
            courseId, progId, acadyear, semester, studyYear, campusId, studSession, "");

        if (sheet.Error != null)
        {
            WriteJson("{\"error\":\"" + JsEsc(sheet.Error) + "\"}");
            return;
        }

        string filename = String.Format("marks_{0}_{1}_{2}_sem{3}.csv",
            courseId.Replace("/", "-"),
            progId.Replace("/", "-"),
            acadyear.Replace("/", "-"),
            semester);

        Response.Clear();
        Response.ContentType = "text/csv";
        Response.AddHeader("Content-Disposition", "attachment; filename=\"" + filename + "\"");

        System.Text.StringBuilder csv = new System.Text.StringBuilder();
        csv.AppendLine(
            "\"Reg No\",\"Student Name\",\"CW Entered\",\"Test Entered\",\"Exam Entered\"," +
            "\"CW Mark\",\"Test Mark\",\"Exam Mark\",\"Total Mark\",\"Grade\",\"Approved By\"");

        if (sheet.Rows != null)
        {
            foreach (MarksSheetService.MarkRow row in sheet.Rows)
            {
                csv.Append('"').Append(CsvEsc(row.Regno      ?? "")).Append("\",");
                csv.Append('"').Append(CsvEsc(row.StudentName ?? "")).Append("\",");
                csv.Append(row.CwEntered).Append(',');
                csv.Append(row.TestEntered).Append(',');
                csv.Append(row.ExamEntered).Append(',');
                csv.Append(row.CwMark).Append(',');
                csv.Append(row.TestMark).Append(',');
                csv.Append(row.ExamMark).Append(',');
                csv.Append(row.TotalMark).Append(',');
                csv.Append('"').Append(CsvEsc(row.Grade      ?? "")).Append("\",");
                csv.Append('"').Append(CsvEsc(row.ApprovedBy ?? "")).Append('"');
                csv.AppendLine();
            }
        }

        Response.Write(csv.ToString());
        Response.End();
    }

    private void EnsureTables()
    {
        ResultsStatusService.EnsureStatusTable();
        MarksActionLogger.EnsureActionLogTable();
        // Ensure provisional marks columns exist in acad_course_registration
        try
        {
            using (MySqlConnection c = new MySqlConnection(ConnStr))
            {
                c.Open();
                EnsureProvisionalMarksColumns(c);
            }
        }
        catch { /* non-critical */ }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Provisional Marks Bridge
    // Copies lecturer-entered marks from acad_examresults_faculty into the
    // provisional_* columns of acad_course_registration so the admin
    // ProvisionalMarksController can pick them up for review and publish.
    // ─────────────────────────────────────────────────────────────────────

    private void WriteProvisionalMarks(string courseId, string progId, string acadyear,
        int semester, int studyYear, int campusId, string studSession, string submittedBy)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();

            // Read all rows for this sheet
            var rows = new List<int[]>();           // [cw_entered, ex_entered, total, regno_index]
            var regnos = new List<string>();

            string readSql = @"
                SELECT regno,
                       COALESCE(cw_mark_entered, 0) AS cw_entered,
                       COALESCE(ex_mark_entered, 0) AS ex_entered,
                       COALESCE(total_mark,      0) AS total_mark
                FROM acad_examresults_faculty
                WHERE course_id = @course AND progid = @prog AND acad_year = @year
                  AND semester   = @sem   AND study_year = @sy
                  AND campus     = @campus AND stud_session = @sess";

            using (MySqlCommand cmd = new MySqlCommand(readSql, conn))
            {
                cmd.Parameters.AddWithValue("@course", courseId);
                cmd.Parameters.AddWithValue("@prog",   progId);
                cmd.Parameters.AddWithValue("@year",   acadyear);
                cmd.Parameters.AddWithValue("@sem",    semester);
                cmd.Parameters.AddWithValue("@sy",     studyYear);
                cmd.Parameters.AddWithValue("@campus", campusId);
                cmd.Parameters.AddWithValue("@sess",   studSession);
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        regnos.Add(rdr["regno"].ToString());
                        rows.Add(new int[] {
                            Convert.ToInt32(rdr["cw_entered"]),
                            Convert.ToInt32(rdr["ex_entered"]),
                            Convert.ToInt32(rdr["total_mark"])
                        });
                    }
                }
            }

            if (regnos.Count == 0) return;

            // Update provisional columns in acad_course_registration for each student
            string coursePredicate = GetCourseRegistrationCoursePredicate(conn);
            if (string.IsNullOrEmpty(coursePredicate))
            {
                return; // No usable course column on target table
            }

            MarkAuditContext.Set(conn, null, "MarkEntry:sheet", "Marks entered on the mark-entry sheet");

            string updateSql = @"
                UPDATE campus_dynamics_portal.acad_course_registration
                SET provisional_course_work_marks     = @cw,
                    provisional_exam_marks            = @ex,
                    provisional_total_marks           = @total,
                    provisional_marks_status          = 'pending',
                    provisional_submitted_by          = @sub,
                    provisional_marks_review_comments = NULL,
                    provisional_marks_reviewed_by     = NULL,
                    provisional_marks_review_date     = NULL
                WHERE regno = @regno
                  AND " + coursePredicate + @"
                  AND acad_year = @year
                  AND semester  = @sem";

            for (int i = 0; i < regnos.Count; i++)
            {
                using (MySqlCommand upd = new MySqlCommand(updateSql, conn))
                {
                    upd.Parameters.AddWithValue("@cw",    rows[i][0]);
                    upd.Parameters.AddWithValue("@ex",    rows[i][1]);
                    upd.Parameters.AddWithValue("@total", rows[i][2]);
                    upd.Parameters.AddWithValue("@sub",   submittedBy);
                    upd.Parameters.AddWithValue("@regno", regnos[i]);
                    upd.Parameters.AddWithValue("@course",courseId);
                    upd.Parameters.AddWithValue("@year",  acadyear);
                    upd.Parameters.AddWithValue("@sem",   semester);
                    upd.ExecuteNonQuery();
                }
            }
        }
    }

    // ─── Provisional summary for the load response ───────────────────────

    private struct ProvisionalSummary
    {
        public int Pending;
        public int Approved;
        public int Rejected;
        public int Published;
        public int Total;
    }

    private ProvisionalSummary GetProvisionalSummary(string courseId, string progId,
        string acadyear, int semester)
    {
        var s = new ProvisionalSummary();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                string coursePredicate = GetCourseRegistrationCoursePredicate(conn);
                if (string.IsNullOrEmpty(coursePredicate))
                    return s;

                string sql = @"
                    SELECT
                        SUM(CASE WHEN COALESCE(provisional_marks_status,'pending') = 'pending'   THEN 1 ELSE 0 END),
                        SUM(CASE WHEN provisional_marks_status = 'approved'  THEN 1 ELSE 0 END),
                        SUM(CASE WHEN provisional_marks_status = 'rejected'  THEN 1 ELSE 0 END),
                        SUM(CASE WHEN provisional_marks_status = 'published' THEN 1 ELSE 0 END),
                        COUNT(*)
                    FROM campus_dynamics_portal.acad_course_registration
                    WHERE " + coursePredicate + @"
                      AND acad_year = @year AND semester = @sem
                      AND provisional_total_marks IS NOT NULL";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@course", courseId);
                    cmd.Parameters.AddWithValue("@year",   acadyear);
                    cmd.Parameters.AddWithValue("@sem",    semester);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read() && !rdr.IsDBNull(4))
                        {
                            s.Pending   = rdr.IsDBNull(0) ? 0 : Convert.ToInt32(rdr[0]);
                            s.Approved  = rdr.IsDBNull(1) ? 0 : Convert.ToInt32(rdr[1]);
                            s.Rejected  = rdr.IsDBNull(2) ? 0 : Convert.ToInt32(rdr[2]);
                            s.Published = rdr.IsDBNull(3) ? 0 : Convert.ToInt32(rdr[3]);
                            s.Total     = rdr.IsDBNull(4) ? 0 : Convert.ToInt32(rdr[4]);
                        }
                    }
                }
            }
        }
        catch { /* return zeroes on error */ }
        return s;
    }

    // ─── Column-safety helpers for provisional columns ───────────────────

    private static void EnsureProvisionalMarksColumns(MySqlConnection conn)
    {
        const string schema = "campus_dynamics_portal";
        const string table  = "acad_course_registration";
        try { EnsureProvCol(conn, schema, table, "provisional_course_work_marks",    "INT NULL"); }          catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_exam_marks",            "INT NULL"); }          catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_total_marks",           "INT NULL"); }          catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_marks_status",          "VARCHAR(20) NULL DEFAULT 'pending'"); } catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_marks_review_comments", "TEXT NULL"); }         catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_marks_reviewed_by",     "VARCHAR(150) NULL"); }  catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_marks_review_date",     "DATETIME NULL"); }     catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_submitted_by",          "VARCHAR(150) NULL"); }  catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_published_by",          "VARCHAR(150) NULL"); }  catch { }
        try { EnsureProvCol(conn, schema, table, "provisional_published_date",        "DATETIME NULL"); }     catch { }
    }

    private static bool ProvColExists(MySqlConnection conn, string schema, string table, string column)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@s AND TABLE_NAME=@t AND COLUMN_NAME=@c", conn))
        {
            cmd.Parameters.AddWithValue("@s", schema);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", column);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }

    private static void EnsureProvCol(MySqlConnection conn, string schema, string table,
        string column, string sqlType)
    {
        if (ProvColExists(conn, schema, table, column)) return;
        using (MySqlCommand cmd = new MySqlCommand(
            "ALTER TABLE " + schema + "." + table + " ADD COLUMN " + column + " " + sqlType, conn))
        {
            cmd.ExecuteNonQuery();
        }
    }

    private static string GetCourseRegistrationCoursePredicate(MySqlConnection conn)
    {
        bool hasCourseId = ProvColExists(conn, "campus_dynamics_portal", "acad_course_registration", "courseID");
        bool hasCourseCode = ProvColExists(conn, "campus_dynamics_portal", "acad_course_registration", "course_code");

        if (hasCourseId && hasCourseCode) return "(courseID = @course OR course_code = @course)";
        if (hasCourseId) return "courseID = @course";
        if (hasCourseCode) return "course_code = @course";
        return null;
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

    private static string CsvEsc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\"", "\"\"");
    }
}
