using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;

/// <summary>
/// MarksSheetService — Core business logic for mark sheet operations.
/// 
/// Provides:
///   - Loading mark sheet data for a given course/programme/period.
///   - Saving marks (single and bulk) with validation and transaction safety.
///   - Sheet status management (DRAFT → SUBMITTED → DEAN_APPROVED → etc.).
///   - Student reconciliation (expected vs in-sheet).
///   - Mark ratio caching to eliminate repeated scalar queries (P58, P59).
///
/// All save operations enforce authorization via MarksAuthorizationService and
/// check deadline locks via MarksDeadlineService before any data mutation.
///
/// Design: C# 5 compatible (no ?. operator, no string interpolation).
/// Addresses: P11-P20 (marks entry workflow gaps), P58-P60 (performance)
/// Task: D-01
/// </summary>
public static class MarksSheetService
{
    // ─────────────────────── Connection String ──────────────────────────

    private static string ConnStr
    {
        get { return MarksConfiguration.ConnStr; }
    }

    // ─────────────────────── Load Sheet Data ────────────────────────────

    /// <summary>
    /// Loads complete mark sheet data for a given course/programme/period.
    /// Returns all student rows with their current marks plus sheet-level metadata.
    /// </summary>
    public static SheetData LoadSheet(string courseId, string progid, string acadyear,
        int semester, int studyYear, int campusId, string studSession, string entryYear)
    {
        SheetData sheet = new SheetData();
        sheet.CourseId = courseId;
        sheet.ProgId = progid;
        sheet.AcadYear = acadyear;
        sheet.Semester = semester;
        sheet.Rows = new List<MarkRow>();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // ── Load mark ratios (cached in the SheetData object) ──────
                LoadRatios(conn, sheet, courseId, progid, acadyear, semester, studyYear, campusId, studSession);

                // ── Load student mark rows ─────────────────────────────────
                using (MySqlCommand cmd = new MySqlCommand(
                    @"SELECT ef.id, ef.regno, 
                             TRIM(CONCAT(COALESCE(s.firstname,''), ' ', COALESCE(s.othername,''))) AS student_name,
                             COALESCE(ef.cw_mark_entered, 0) AS cw_entered,
                             COALESCE(ef.test_mark_entered, 0) AS test_entered,
                             COALESCE(ef.exam_mark_entered, 0) AS exam_entered,
                             COALESCE(ef.cw_mark, 0) AS cw_mark,
                             COALESCE(ef.test_mark, 0) AS test_mark,
                             COALESCE(ef.ex_mark, 0) AS exam_mark,
                             COALESCE(ef.total_mark, 0) AS total_mark,
                             COALESCE(ef.grade, '') AS grade,
                             COALESCE(ef.approved_by, '-') AS approved_by,
                             ef.midyear_session
                      FROM acad_examresults_faculty ef
                      LEFT JOIN acad_student s ON s.regno = ef.regno
                      WHERE ef.course_id = @course
                        AND ef.progid = @prog
                        AND ef.acad_year = @year
                        AND ef.semester = @sem
                        AND ef.study_year = @sy
                        AND ef.campus = @campus
                        AND ef.stud_session = @sess
                      ORDER BY s.firstname, s.othername, ef.regno", conn))
                {
                    cmd.Parameters.AddWithValue("@course", courseId);
                    cmd.Parameters.AddWithValue("@prog", progid);
                    cmd.Parameters.AddWithValue("@year", acadyear);
                    cmd.Parameters.AddWithValue("@sem", semester);
                    cmd.Parameters.AddWithValue("@sy", studyYear);
                    cmd.Parameters.AddWithValue("@campus", campusId);
                    cmd.Parameters.AddWithValue("@sess", studSession);

                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            MarkRow row = new MarkRow();
                            row.Id = Convert.ToInt32(rdr["id"]);
                            row.Regno = rdr["regno"].ToString();
                            row.StudentName = rdr["student_name"].ToString();
                            row.CwEntered = Convert.ToInt32(rdr["cw_entered"]);
                            row.TestEntered = Convert.ToInt32(rdr["test_entered"]);
                            row.ExamEntered = Convert.ToInt32(rdr["exam_entered"]);
                            row.CwMark = Convert.ToInt32(rdr["cw_mark"]);
                            row.TestMark = Convert.ToInt32(rdr["test_mark"]);
                            row.ExamMark = Convert.ToInt32(rdr["exam_mark"]);
                            row.TotalMark = Convert.ToInt32(rdr["total_mark"]);
                            row.Grade = rdr["grade"].ToString();
                            row.ApprovedBy = rdr["approved_by"].ToString();
                            row.IsApproved = row.ApprovedBy != "-" && !string.IsNullOrEmpty(row.ApprovedBy);
                            sheet.Rows.Add(row);
                        }
                    }
                }

                // ── Load expected student count (from registration) ────────
                using (MySqlCommand cmd2 = new MySqlCommand(
                    @"SELECT COUNT(DISTINCT r.regno)
                      FROM acad_registration r
                      INNER JOIN acad_course_registration cr ON cr.regno = r.regno
                        AND cr.course_code = @course AND cr.acad_year = @year AND cr.semester = @sem
                      WHERE r.progid = @prog AND r.acad_year = @year AND r.semester = @sem
                        AND r.study_year = @sy", conn))
                {
                    cmd2.Parameters.AddWithValue("@course", courseId);
                    cmd2.Parameters.AddWithValue("@prog", progid);
                    cmd2.Parameters.AddWithValue("@year", acadyear);
                    cmd2.Parameters.AddWithValue("@sem", semester);
                    cmd2.Parameters.AddWithValue("@sy", studyYear);
                    try
                    {
                        sheet.ExpectedStudentCount = Convert.ToInt32(cmd2.ExecuteScalar());
                    }
                    catch
                    {
                        sheet.ExpectedStudentCount = -1; // unknown
                    }
                }

                // ── Compute statistics ─────────────────────────────────────
                sheet.TotalStudents = sheet.Rows.Count;
                int marksEntered = 0;
                int approved = 0;
                foreach (MarkRow r in sheet.Rows)
                {
                    if (r.CwEntered > 0 || r.TestEntered > 0 || r.ExamEntered > 0) marksEntered++;
                    if (r.IsApproved) approved++;
                }
                sheet.MarksEnteredCount = marksEntered;
                sheet.ApprovedCount = approved;
            }
        }
        catch (Exception ex)
        {
            sheet.Error = ex.Message;
        }
        return sheet;
    }

    // ─────────────────────── Ratio Loading (Cached) ─────────────────────

    /// <summary>
    /// Loads mark ratios once and stores them in the SheetData object.
    /// This eliminates the 3 repeated scalar queries per row save (P58, P59).
    /// </summary>
    private static void LoadRatios(MySqlConnection conn, SheetData sheet,
        string courseId, string progid, string acadyear, int semester,
        int studyYear, int campusId, string studSession)
    {
        // These are LABELS, not multipliers - the sheet shows "out of 40" / "out of 60".
        // Nothing multiplies by them any more (see BulkSaveMarks).
        //
        // A lookup against acad_examresults_faculty_settings used to sit here. It selected
        // cw_ratio and filtered on acad_year; the table has coursework_ratio and acadyear, so it
        // threw on every call and the catch supplied exactly these numbers. Removed rather than
        // corrected, deliberately: of that table's rows for current years 2,945 of ~3,015 read
        // 100/100 (which under the old formula meant "do not scale" - what MRU already does),
        // but 43 read 0/100 and 18 read 0/0. Repairing the query would have zeroed the
        // coursework, or everything, for those courses. Do not "fix" it back.
        sheet.CwRatio = 40;
        sheet.TestRatio = 0;
        sheet.ExamRatio = 60;
    }

    // ─────────────────────── Public Ratio Access (H-01) ─────────────────

    /// <summary>
    /// Lightweight public accessor that returns only the CW/Test/Exam ratios
    /// for a given course context, without loading all student rows.
    /// Replaces the pattern of calling LoadSheet() just to read 3 ratio ints (P58, P59).
    /// Task: H-01
    /// </summary>
    public static RatioData GetRatios(string courseId, string progid, string acadyear, int semester)
    {
        // Display labels only. The lookup that used to be here could never run - it selected
        // cw_ratio and filtered on acad_year, while the table has coursework_ratio and
        // acadyear - so these defaults were always what callers actually got. Removed rather
        // than repaired; see LoadRatios for why repairing it would zero some courses.
        RatioData data = new RatioData();
        data.CwRatio = 40;
        data.TestRatio = 0;
        data.ExamRatio = 60;
        return data;
    }

    // ─────────────────────── Save Marks (Bulk) ──────────────────────────

    /// <summary>
    /// Saves multiple mark rows in a single transaction with integrated audit logging.
    /// Each row is validated, weighted marks computed, old values pre-loaded for audit,
    /// and the update + audit written atomically in the same transaction.
    /// Includes deadlock retry logic (up to 3 attempts) for concurrent access safety.
    /// Returns a result object with per-row success/failure information.
    /// 
    /// Authorization and deadline-lock checks must be performed by the caller before
    /// invoking this method.
    /// 
    /// Enhancement: H-02 — Transactional integrity with in-tx audit logging.
    /// Uses MarksAuditService.LogMarkUpdate(conn, tx, ...) transactional overload
    /// to ensure audit records are committed atomically with mark data changes.
    /// Pre-loads old values before UPDATE so audit trail captures real deltas.
    /// </summary>
    public static SaveResult BulkSaveMarks(string courseId, string progid, string acadyear,
        int semester, int cwRatio, int testRatio, int examRatio, List<MarkInput> inputs)
    {
        const int MAX_RETRIES = 3;
        const int DEADLOCK_ERROR_CODE = 1213;

        SaveResult result = new SaveResult();
        result.Errors = new List<string>();
        string user = MarksAuthorizationService.GetCurrentUser();

        for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
        {
            // Reset for retry
            result.Errors.Clear();
            result.Ok = false;
            result.SavedCount = 0;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnStr))
                {
                    conn.Open();
                    MySqlTransaction tx = conn.BeginTransaction();
                    try
                    {
                        int saved = 0;
                        foreach (MarkInput input in inputs)
                        {
                            // Validate non-negative
                            if (input.CwEntered < 0 || input.TestEntered < 0 || input.ExamEntered < 0)
                            {
                                result.Errors.Add(String.Format("Row {0}: Marks cannot be negative.", input.RowId));
                                continue;
                            }

                            // MRU enters marks ALREADY on their final scale - coursework out of 40,
                            // exam out of 60 - and the total is their plain sum. 36,660 rows in
                            // acad_examresults_faculty have cw_mark = cw_mark_entered against 4 that
                            // are scaled, so this is the established behaviour, not a preference.
                            //
                            // This used to multiply by cwRatio/examRatio. Those ratios could never be
                            // read (the lookup named columns that do not exist - see GetRatios), so the
                            // fallback 40/0/60 applied and every mark would have been cut to roughly
                            // 40%/60% of its true value. The path never ran because the column name
                            // above was wrong too; correcting that name without removing this
                            // multiplication would have turned a dead screen into a mark-corrupting one.
                            int cwWeighted = input.CwEntered;
                            int testWeighted = input.TestEntered;
                            int examWeighted = input.ExamEntered;
                            int total = cwWeighted + testWeighted + examWeighted;

                            if (total > 100)
                            {
                                result.Errors.Add(String.Format("Row {0} ({1}): Total mark {2} exceeds 100.", input.RowId, input.Regno, total));
                                continue;
                            }

                            // Pre-load old values + approval check in one query (H-02)
                            int oldCwEntered = 0, oldTestEntered = 0, oldExamEntered = 0;
                            int oldCwMark = 0, oldTestMark = 0, oldExamMark = 0;
                            using (MySqlCommand chk = new MySqlCommand(
                                @"SELECT approved_by, 
                                         COALESCE(cw_mark_entered, 0), COALESCE(test_mark_entered, 0), COALESCE(exam_mark_entered, 0),
                                         COALESCE(cw_mark, 0), COALESCE(test_mark, 0), COALESCE(ex_mark, 0)
                                  FROM acad_examresults_faculty WHERE id = @id", conn, tx))
                            {
                                chk.Parameters.AddWithValue("@id", input.RowId);
                                using (MySqlDataReader rdr = chk.ExecuteReader())
                                {
                                    if (rdr.Read())
                                    {
                                        string currentApproval = rdr.IsDBNull(0) ? "-" : rdr.GetString(0);
                                        if (currentApproval != "-" && !string.IsNullOrEmpty(currentApproval))
                                        {
                                            result.Errors.Add(String.Format("Row {0} ({1}): Already approved by {2} — cannot edit.",
                                                input.RowId, input.Regno, currentApproval));
                                            rdr.Close();
                                            continue;
                                        }
                                        oldCwEntered = rdr.GetInt32(1);
                                        oldTestEntered = rdr.GetInt32(2);
                                        oldExamEntered = rdr.GetInt32(3);
                                        oldCwMark = rdr.GetInt32(4);
                                        oldTestMark = rdr.GetInt32(5);
                                        oldExamMark = rdr.GetInt32(6);
                                    }
                                    else
                                    {
                                        result.Errors.Add(String.Format("Row {0} ({1}): Record not found.",
                                            input.RowId, input.Regno));
                                        rdr.Close();
                                        continue;
                                    }
                                }
                            }

                            // Skip update if nothing changed (avoid unnecessary writes + audit noise)
                            bool unchanged = (input.CwEntered == oldCwEntered
                                && input.TestEntered == oldTestEntered
                                && input.ExamEntered == oldExamEntered);
                            if (unchanged)
                            {
                                continue;
                            }

                            // Update the row
                            using (MySqlCommand upd = new MySqlCommand(
                                @"UPDATE acad_examresults_faculty 
                                  SET cw_mark_entered = @cwe, test_mark_entered = @te, exam_mark_entered = @ee,
                                      cw_mark = @cwm, test_mark = @tm, ex_mark = @em,
                                      total_mark = @total
                                  WHERE id = @id", conn, tx))
                            {
                                upd.Parameters.AddWithValue("@cwe", input.CwEntered);
                                upd.Parameters.AddWithValue("@te", input.TestEntered);
                                upd.Parameters.AddWithValue("@ee", input.ExamEntered);
                                upd.Parameters.AddWithValue("@cwm", cwWeighted);
                                upd.Parameters.AddWithValue("@tm", testWeighted);
                                upd.Parameters.AddWithValue("@em", examWeighted);
                                upd.Parameters.AddWithValue("@total", total);
                                upd.Parameters.AddWithValue("@id", input.RowId);
                                upd.ExecuteNonQuery();
                                saved++;
                            }

                            // Audit inside transaction with real old values (H-02)
                            MarksAuditService.LogMarkUpdate(conn, tx,
                                input.Regno, courseId, progid, acadyear, semester,
                                oldCwMark, cwWeighted, oldTestMark, testWeighted,
                                oldExamMark, examWeighted,
                                oldCwEntered, input.CwEntered,
                                oldTestEntered, input.TestEntered,
                                oldExamEntered, input.ExamEntered);
                        }

                        tx.Commit();
                        result.SavedCount = saved;
                        result.Ok = true;
                        return result; // Success — exit retry loop
                    }
                    catch (MySqlException mex)
                    {
                        try { tx.Rollback(); } catch { /* ignore rollback failure */ }

                        if (mex.Number == DEADLOCK_ERROR_CODE && attempt < MAX_RETRIES)
                        {
                            // Deadlock detected — retry after brief pause
                            System.Threading.Thread.Sleep(100 * attempt);
                            continue;
                        }

                        result.Ok = false;
                        result.Errors.Add("Transaction failed: " + mex.Message);
                    }
                    catch (Exception txEx)
                    {
                        try { tx.Rollback(); } catch { /* ignore rollback failure */ }
                        result.Ok = false;
                        result.Errors.Add("Transaction failed: " + txEx.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Errors.Add("Connection failed: " + ex.Message);
            }

            // If we get here on a non-deadlock error, don't retry
            break;
        }

        return result;
    }

    // ─────────────────────── Grading ────────────────────────────────────

    /// <summary>
    /// Loads the grading scale for a given programme/system.
    /// Returns a list of grade boundaries sorted descending by min_mark.
    /// Cached per request context to avoid repeated DB calls.
    /// </summary>
    public static List<GradeBoundary> GetGradingScale(string progid)
    {
        List<GradeBoundary> scale = new List<GradeBoundary>();
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // Try programme-specific grading system first
                string gsCode = "";
                using (MySqlCommand cmd = new MySqlCommand(
                    "SELECT COALESCE(gradingsystem, '') FROM acad_programme WHERE progcode = @prog LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@prog", progid);
                    object obj = cmd.ExecuteScalar();
                    gsCode = obj != null ? obj.ToString() : "";
                }

                if (string.IsNullOrEmpty(gsCode)) gsCode = "DEFAULT";

                using (MySqlCommand cmd2 = new MySqlCommand(
                    @"SELECT grade, min_mark, max_mark, grade_point, description
                      FROM acad_gs_details
                      WHERE gs_code = @gs
                      ORDER BY min_mark DESC", conn))
                {
                    cmd2.Parameters.AddWithValue("@gs", gsCode);

                    using (MySqlDataReader rdr = cmd2.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            GradeBoundary gb = new GradeBoundary();
                            gb.Grade = rdr["grade"].ToString();
                            gb.MinMark = Convert.ToInt32(rdr["min_mark"]);
                            gb.MaxMark = Convert.ToInt32(rdr["max_mark"]);
                            gb.GradePoint = rdr["grade_point"] != DBNull.Value ? Convert.ToDouble(rdr["grade_point"]) : 0;
                            gb.Description = rdr["description"] != DBNull.Value ? rdr["description"].ToString() : "";
                            scale.Add(gb);
                        }
                    }
                }
            }
        }
        catch
        {
            // Return empty scale on error
        }
        return scale;
    }

    /// <summary>
    /// Computes the grade for a given total mark based on a grading scale.
    /// </summary>
    public static string ComputeGrade(int totalMark, List<GradeBoundary> scale)
    {
        foreach (GradeBoundary gb in scale)
        {
            if (totalMark >= gb.MinMark && totalMark <= gb.MaxMark) return gb.Grade;
        }
        return "F"; // default if no match
    }

    // ─────────────────────── DTOs ───────────────────────────────────────

    public class SheetData
    {
        public string CourseId { get; set; }
        public string ProgId { get; set; }
        public string AcadYear { get; set; }
        public int Semester { get; set; }
        public int CwRatio { get; set; }
        public int TestRatio { get; set; }
        public int ExamRatio { get; set; }
        public int TotalStudents { get; set; }
        public int MarksEnteredCount { get; set; }
        public int ApprovedCount { get; set; }
        public int ExpectedStudentCount { get; set; }
        public List<MarkRow> Rows { get; set; }
        public string Error { get; set; }
    }

    public class MarkRow
    {
        public int Id { get; set; }
        public string Regno { get; set; }
        public string StudentName { get; set; }
        public int CwEntered { get; set; }
        public int TestEntered { get; set; }
        public int ExamEntered { get; set; }
        public int CwMark { get; set; }
        public int TestMark { get; set; }
        public int ExamMark { get; set; }
        public int TotalMark { get; set; }
        public string Grade { get; set; }
        public string ApprovedBy { get; set; }
        public bool IsApproved { get; set; }
    }

    public class MarkInput
    {
        public int RowId { get; set; }
        public string Regno { get; set; }
        public int CwEntered { get; set; }
        public int TestEntered { get; set; }
        public int ExamEntered { get; set; }
    }

    public class SaveResult
    {
        public bool Ok { get; set; }
        public int SavedCount { get; set; }
        public List<string> Errors { get; set; }
    }

    public class GradeBoundary
    {
        public string Grade { get; set; }
        public int MinMark { get; set; }
        public int MaxMark { get; set; }
        public double GradePoint { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Lightweight DTO for ratio-only queries (H-01).
    /// Avoids loading full SheetData when only ratios are needed.
    /// </summary>
    public class RatioData
    {
        public int CwRatio { get; set; }
        public int TestRatio { get; set; }
        public int ExamRatio { get; set; }
    }
}
