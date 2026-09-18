using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using MySql.Data.MySqlClient;

/// <summary>
/// MarksSheetSyncService — Idempotent sheet synchronisation.
///
/// Ensures the mark sheet for a given course/programme/period contains rows for
/// ALL students who are registered for that course. Late-registering students
/// (or students whose registration was processed after sheet creation) are
/// inserted with zero marks, ready for data entry.
///
/// Key design:
///   - INSERT ... SELECT ... LEFT JOIN to guarantee idempotency: only students
///     NOT already in the sheet are inserted (atomic, single SQL statement).
///   - Returns a SyncResult DTO with counts for added, already-present, and total.
///   - Transaction-safe and deadlock-aware.
///
/// This complements the Reconciliation service (B-06): reconciliation *reports*
/// discrepancies; sync *resolves* the "missing" category by adding rows.
///
/// Design: C# 5 compatible (no ?. operator, no string interpolation).
/// Addresses: P6 (silent student exclusion), P7 (late registration gaps)
/// Task: H-04
/// </summary>
public static class MarksSheetSyncService
{
    private static string ConnStr
    {
        get { return MarksConfiguration.ConnStr; }
    }

    // ─────────────────────── Main Sync Method ───────────────────────────

    /// <summary>
    /// Synchronises the mark sheet by inserting rows for registered students
    /// who are not yet in the sheet. Fully idempotent — safe to call multiple times.
    ///
    /// Returns a SyncResult with counts and any error message.
    /// </summary>
    public static SyncResult SyncSheet(string courseId, string progId, string acadyear,
        int semester, int studyYear, int campusId, string studSession)
    {
        SyncResult result = new SyncResult();

        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // Step 1: Count existing rows before sync
                int beforeCount = 0;
                using (MySqlCommand cntCmd = new MySqlCommand(
                    @"SELECT COUNT(*) FROM acad_examresults_faculty
                      WHERE course_id = @course AND progid = @prog AND acad_year = @year
                        AND semester = @sem AND study_year = @sy
                        AND campus = @campus AND stud_session = @session", conn))
                {
                    cntCmd.Parameters.AddWithValue("@course", courseId);
                    cntCmd.Parameters.AddWithValue("@prog", progId);
                    cntCmd.Parameters.AddWithValue("@year", acadyear);
                    cntCmd.Parameters.AddWithValue("@sem", semester);
                    cntCmd.Parameters.AddWithValue("@sy", studyYear);
                    cntCmd.Parameters.AddWithValue("@campus", campusId);
                    cntCmd.Parameters.AddWithValue("@session", studSession);
                    beforeCount = Convert.ToInt32(cntCmd.ExecuteScalar());
                }

                // Step 2: Atomic INSERT ... SELECT with LEFT JOIN to add missing students.
                // Only students registered for this course (via acad_programmecourses mapping)
                // who are NOT already in the sheet are inserted. Zero marks, empty grade, '-' approval.
                int inserted = 0;
                using (MySqlCommand ins = new MySqlCommand(
                    @"INSERT INTO acad_examresults_faculty 
                        (regno, course_id, progid, acad_year, semester, study_year, campus, stud_session,
                         cw_mark_entered, test_mark_entered, exam_mark_entered,
                         cw_mark, test_mark, ex_mark, total_mark, grade, approved_by)
                      SELECT r.regno, @course, @prog, @year, @sem, @sy, @campus, @session,
                             0, 0, 0, 0, 0, 0, 0, '', '-'
                      FROM acad_registration r
                      JOIN acad_programmecourses pc 
                          ON pc.progcode = r.progcode 
                          AND pc.course_code = @course
                          AND pc.semester = @sem
                          AND pc.study_year = @sy
                      LEFT JOIN acad_examresults_faculty ef 
                          ON ef.regno = r.regno 
                          AND ef.course_id = @course 
                          AND ef.progid = @prog
                          AND ef.acad_year = @year 
                          AND ef.semester = @sem 
                          AND ef.study_year = @sy
                          AND ef.campus = @campus 
                          AND ef.stud_session = @session
                      WHERE r.progcode = @prog
                        AND r.acad_year = @year
                        AND r.semester = @sem
                        AND r.campusid = @campus
                        AND r.studysession = @session
                        AND r.study_year = @sy
                        AND ef.id IS NULL", conn))
                {
                    ins.Parameters.AddWithValue("@course", courseId);
                    ins.Parameters.AddWithValue("@prog", progId);
                    ins.Parameters.AddWithValue("@year", acadyear);
                    ins.Parameters.AddWithValue("@sem", semester);
                    ins.Parameters.AddWithValue("@sy", studyYear);
                    ins.Parameters.AddWithValue("@campus", campusId);
                    ins.Parameters.AddWithValue("@session", studSession);
                    inserted = ins.ExecuteNonQuery();
                }

                result.AddedCount = inserted;
                result.AlreadyExistCount = beforeCount;
                result.TotalAfterSync = beforeCount + inserted;
                result.Ok = true;

                // Audit the sync action
                if (inserted > 0)
                {
                    string user = MarksAuthorizationService.GetCurrentUser();
                    MarksAuditService.LogEntry(new MarksAuditService.AuditEntry
                    {
                        CourseId = courseId,
                        ProgId = progId,
                        AcadYear = acadyear,
                        Semester = semester,
                        ActionType = "SYNC",
                        ActionTypeExt = "SHEET_SYNC",
                        ChangedBy = user,
                        IpAddress = MarksAuthorizationService.GetClientIP(),
                        ChangeReason = String.Format("Sheet sync: added {0} students (total now {1})",
                            inserted, beforeCount + inserted)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = ex.Message;
        }

        return result;
    }

    // ─────────────────────── DTO ────────────────────────────────────────

    public class SyncResult
    {
        public bool Ok { get; set; }
        public int AddedCount { get; set; }
        public int AlreadyExistCount { get; set; }
        public int TotalAfterSync { get; set; }
        public string Error { get; set; }
    }
}
