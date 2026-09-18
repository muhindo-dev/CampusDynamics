using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using MySql.Data.MySqlClient;

/// <summary>
/// MarksReconciliationService — Compares expected students (from registration) with
/// students actually present in the mark sheet, identifying discrepancies.
///
/// Detects:
///   - Missing: Students registered for the course but not in the marksheet
///   - Extra:   Students in the marksheet but no longer registered
///   - Matched: Students present in both
///
/// This addresses the "silent student exclusion" problem (P6) where students
/// silently disappear from marksheets due to registration parameter mismatches.
///
/// Design: C# 5 compatible (no ?. operator, no string interpolation).
/// Addresses: P6 (silent exclusion), P7 (late registration), P10 (course-reg verify)
/// Task: B-06
/// </summary>
public static class MarksReconciliationService
{
    private static string ConnStr
    {
        get { return MarksConfiguration.ConnStr; }
    }

    // ─────────────────────── Main Reconciliation ────────────────────────

    /// <summary>
    /// Reconciles registered students against the marksheet for a given context.
    /// Returns a ReconciliationResult with matched, missing, and extra student lists.
    /// </summary>
    public static ReconciliationResult Reconcile(string courseId, string progId,
        string acadyear, int semester, int studyYear, int campusId, string studSession)
    {
        ReconciliationResult result = new ReconciliationResult();
        result.Missing = new List<StudentInfo>();
        result.Extra = new List<StudentInfo>();
        result.Matched = new List<StudentInfo>();

        try
        {
            // Collect sets from both sources
            Dictionary<string, StudentInfo> registered = GetRegisteredStudents(
                courseId, progId, acadyear, semester, studyYear, campusId, studSession);
            Dictionary<string, StudentInfo> inSheet = GetSheetStudents(
                courseId, progId, acadyear, semester, studyYear, campusId, studSession);

            // Compare: registered but not in sheet => MISSING
            foreach (KeyValuePair<string, StudentInfo> kvp in registered)
            {
                if (inSheet.ContainsKey(kvp.Key))
                {
                    result.Matched.Add(kvp.Value);
                }
                else
                {
                    result.Missing.Add(kvp.Value);
                }
            }

            // Compare: in sheet but not registered => EXTRA
            foreach (KeyValuePair<string, StudentInfo> kvp in inSheet)
            {
                if (!registered.ContainsKey(kvp.Key))
                {
                    result.Extra.Add(kvp.Value);
                }
            }

            result.RegisteredCount = registered.Count;
            result.InSheetCount = inSheet.Count;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    // ─────────────────────── Data Sources ────────────────────────────────

    /// <summary>
    /// Gets students who should be in the marksheet based on registration data.
    /// Queries acad_registration joined with student info, filtered by programme
    /// courses for the given course/semester combination.
    /// </summary>
    private static Dictionary<string, StudentInfo> GetRegisteredStudents(
        string courseId, string progId, string acadyear, int semester,
        int studyYear, int campusId, string studSession)
    {
        Dictionary<string, StudentInfo> students = new Dictionary<string, StudentInfo>();

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();

            // Join registration with programme courses to find students
            // who should be taking this course in this semester.
            // acad_registration holds student semester registration.
            // acad_programmecourses maps programme->course->semester->study_year.
            string sql = @"
                SELECT DISTINCT r.regno, 
                       COALESCE(TRIM(CONCAT(COALESCE(s.surname,''), ' ', COALESCE(s.othernames,''))), r.regno) AS student_name,
                       r.status AS reg_status
                FROM acad_registration r
                JOIN acad_programmecourses pc 
                    ON pc.progcode = r.progcode 
                    AND pc.course_code = @course
                    AND pc.semester = @sem
                    AND pc.study_year = @sy
                LEFT JOIN acad_students_biodata s ON s.regno = r.regno
                WHERE r.progcode = @prog
                  AND r.acad_year = @year
                  AND r.semester = @sem
                  AND r.campusid = @campus
                  AND r.studysession = @session
                  AND r.study_year = @sy
                ORDER BY student_name";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@course", courseId);
                cmd.Parameters.AddWithValue("@prog", progId);
                cmd.Parameters.AddWithValue("@year", acadyear);
                cmd.Parameters.AddWithValue("@sem", semester);
                cmd.Parameters.AddWithValue("@sy", studyYear);
                cmd.Parameters.AddWithValue("@campus", campusId);
                cmd.Parameters.AddWithValue("@session", studSession);

                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string regno = rdr["regno"].ToString().Trim();
                        if (!string.IsNullOrEmpty(regno) && !students.ContainsKey(regno))
                        {
                            StudentInfo si = new StudentInfo();
                            si.Regno = regno;
                            si.StudentName = rdr["student_name"].ToString().Trim();
                            si.RegStatus = rdr["reg_status"] != DBNull.Value
                                ? rdr["reg_status"].ToString() : "";
                            students[regno] = si;
                        }
                    }
                }
            }
        }

        return students;
    }

    /// <summary>
    /// Gets students currently in the marksheet (acad_examresults_faculty).
    /// </summary>
    private static Dictionary<string, StudentInfo> GetSheetStudents(
        string courseId, string progId, string acadyear, int semester,
        int studyYear, int campusId, string studSession)
    {
        Dictionary<string, StudentInfo> students = new Dictionary<string, StudentInfo>();

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();

            string sql = @"
                SELECT ef.regno,
                       COALESCE(TRIM(CONCAT(COALESCE(s.surname,''), ' ', COALESCE(s.othernames,''))), ef.regno) AS student_name,
                       CASE WHEN COALESCE(ef.cw_mark_entered, 0) > 0 
                            OR COALESCE(ef.test_mark_entered, 0) > 0 
                            OR COALESCE(ef.exam_mark_entered, 0) > 0 
                       THEN 1 ELSE 0 END AS has_marks
                FROM acad_examresults_faculty ef
                LEFT JOIN acad_students_biodata s ON s.regno = ef.regno
                WHERE ef.course_id = @course
                  AND ef.progid = @prog
                  AND ef.acad_year = @year
                  AND ef.semester = @sem
                  AND ef.study_year = @sy
                  AND ef.campusid = @campus
                  AND ef.studsession = @session
                ORDER BY student_name";

            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@course", courseId);
                cmd.Parameters.AddWithValue("@prog", progId);
                cmd.Parameters.AddWithValue("@year", acadyear);
                cmd.Parameters.AddWithValue("@sem", semester);
                cmd.Parameters.AddWithValue("@sy", studyYear);
                cmd.Parameters.AddWithValue("@campus", campusId);
                cmd.Parameters.AddWithValue("@session", studSession);

                using (MySqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string regno = rdr["regno"].ToString().Trim();
                        if (!string.IsNullOrEmpty(regno) && !students.ContainsKey(regno))
                        {
                            StudentInfo si = new StudentInfo();
                            si.Regno = regno;
                            si.StudentName = rdr["student_name"].ToString().Trim();
                            si.HasMarks = Convert.ToInt32(rdr["has_marks"]) == 1;
                            students[regno] = si;
                        }
                    }
                }
            }
        }

        return students;
    }

    // ─────────────────────── DTOs ───────────────────────────────────────

    public class ReconciliationResult
    {
        public int RegisteredCount { get; set; }
        public int InSheetCount { get; set; }
        public List<StudentInfo> Missing { get; set; }
        public List<StudentInfo> Extra { get; set; }
        public List<StudentInfo> Matched { get; set; }
        public string Error { get; set; }
    }

    public class StudentInfo
    {
        public string Regno { get; set; }
        public string StudentName { get; set; }
        public string RegStatus { get; set; }
        public bool HasMarks { get; set; }
    }
}
