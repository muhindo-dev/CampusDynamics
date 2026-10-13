using System;
using System.Configuration;
using System.Data;
using MySql.Data.MySqlClient;

/// <summary>
/// Helper class to ensure certificate/transcript data is populated correctly.
/// Provides a fallback mechanism when the stored procedure returns incomplete data
/// (0 rows due to JOIN failures, or rows with NULL student names).
///
/// Also enriches data with thesis_title and supervisor_name from
/// acad_graduate_research / acad_superviors for Masters reports.
/// </summary>
public static class CertificateDataHelper
{
    /// <summary>
    /// Ensures certificate/transcript data is populated for a single student.
    /// If the stored procedure returned 0 rows or returned rows with missing studnm,
    /// this method fills in the data from acad_graduands and acad_student directly.
    /// Also injects thesis_title and supervisor_name for Masters reports.
    /// </summary>
    public static void EnsureSingleStudentData(ResultsData DS, string regno)
    {
        try
        {
            DataTable dt = DS.acad_GetBatchStudentTranscriptData;

            // Ensure thesis/supervisor columns exist (needed for Masters reports)
            if (!dt.Columns.Contains("thesis_title"))
                dt.Columns.Add("thesis_title", typeof(string));
            if (!dt.Columns.Contains("supervisor_name"))
                dt.Columns.Add("supervisor_name", typeof(string));

            // Case 1: SP returned 0 rows — need to build a row from scratch
            if (dt.Rows.Count == 0)
            {
                string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = @"SELECT 
                        IFNULL(g.degclass, '') AS deg,
                        IFNULL((SELECT f.faculty_name FROM acad_faculty f JOIN acad_programme p ON p.faculty_code = f.faculty_code WHERE p.progcode = g.progcode LIMIT 1), '') AS fax,
                        IFNULL(DATE_FORMAT(s.Birth_date, '%d %M, %Y'), '') AS dobs,
                        IFNULL(s.nationality, '') AS nat,
                        IFNULL(g.gender, '') AS gen,
                        IFNULL(g.cgpa, 0) AS cgpa,
                        IFNULL((SELECT p.progname FROM acad_programme p WHERE p.progcode = g.progcode LIMIT 1), '') AS prog,
                        IFNULL(NULLIF(TRIM(CONCAT(IFNULL(s.firstname, ''), ' ', IFNULL(s.othername, ''))), ''), IFNULL(g.stud_name, '')) AS studnm,
                        IFNULL(s.photo, '') AS photo,
                        TRIM(g.regno) AS regno,
                        IFNULL(s.entryno, '') AS entryno,
                        IFNULL(DATE_FORMAT(COALESCE(gd.graduation_date, g.grad_date), '%D %M, %Y'), '') AS formated_grad_date,
                        COALESCE(gd.graduation_date, g.grad_date) AS grad_date,
                        g.comp_date,
                        IFNULL(DATE_FORMAT(g.comp_date, '%D %M, %Y'), '') AS formated_comp_date,
                        IFNULL(g.convocation, '') AS convocation,
                        IFNULL(gr.res_topic, '') AS thesis_title,
                        IFNULL(sv.supervior_name, '') AS supervisor_name
                        FROM acad_graduands g
                        LEFT JOIN acad_grad_dates gd ON gd.acadyear = g.acadyear
                        LEFT JOIN acad_student s ON TRIM(s.regno) = TRIM(g.regno)
                        LEFT JOIN acad_graduate_research gr ON TRIM(gr.regno) = TRIM(g.regno)
                        LEFT JOIN acad_superviors sv ON sv.Id = gr.supervior_id
                        WHERE TRIM(g.regno) = @reg
                        LIMIT 1";

                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@reg", regno);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                DataRow row = dt.NewRow();
                                row["deg"] = reader["deg"];
                                row["fax"] = reader["fax"];
                                row["dobs"] = reader["dobs"];
                                row["nat"] = reader["nat"];
                                row["gen"] = reader["gen"];
                                row["cgpa"] = Convert.ToDouble(reader["cgpa"]);
                                row["prog"] = reader["prog"];
                                row["studnm"] = reader["studnm"];
                                row["photo"] = reader["photo"];
                                row["regno"] = reader["regno"];
                                row["entryno"] = reader["entryno"];
                                row["formated_grad_date"] = reader["formated_grad_date"];
                                row["formated_comp_date"] = reader["formated_comp_date"];
                                row["convocation"] = reader["convocation"];
                                row["thesis_title"] = reader["thesis_title"];
                                row["supervisor_name"] = reader["supervisor_name"];
                                if (reader["grad_date"] != DBNull.Value)
                                    row["grad_date"] = reader["grad_date"];
                                if (reader["comp_date"] != DBNull.Value)
                                    row["comp_date"] = reader["comp_date"];
                                dt.Rows.Add(row);
                            }
                        }
                    }
                }
            }
            // Case 2: SP returned rows but studnm is empty — patch it
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    object studnmVal = dt.Rows[i]["studnm"];
                    if (studnmVal == null || studnmVal == DBNull.Value || string.IsNullOrEmpty(studnmVal.ToString().Trim()))
                    {
                        string regVal = dt.Rows[i]["regno"] != null && dt.Rows[i]["regno"] != DBNull.Value
                            ? dt.Rows[i]["regno"].ToString().Trim() : regno;
                        string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
                        using (MySqlConnection conn = new MySqlConnection(connStr))
                        {
                            conn.Open();
                            string sql = @"SELECT IFNULL(NULLIF(TRIM(CONCAT(IFNULL(s.firstname, ''), ' ', IFNULL(s.othername, ''))), ''), 
                                          IFNULL(g.stud_name, '')) AS studnm 
                                          FROM acad_graduands g LEFT JOIN acad_student s ON TRIM(s.regno) = TRIM(g.regno) 
                                          WHERE TRIM(g.regno) = @reg LIMIT 1";
                            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@reg", regVal);
                                object result = cmd.ExecuteScalar();
                                if (result != null && result != DBNull.Value && !string.IsNullOrEmpty(result.ToString().Trim()))
                                    dt.Rows[i]["studnm"] = result.ToString().Trim();
                            }
                        }
                    }
                }

                // Enrich thesis/supervisor data for all rows
                GraduateHelper.EnrichTranscriptData(DS, regno);
            }
        }
        catch (Exception)
        {
            // Fallback silently fails — certificate will print with whatever data the SP returned
        }

        // Remap classification for masters students
        RemapMastersClassification(DS, regno);
    }

    /// <summary>
    /// Remaps degree classifications for all masters students in the dataset (batch operation).
    /// Masters students use different classification labels:
    /// - FIRST CLASS → Distinction
    /// - SECOND CLASS UPPER / SECOND CLASS LOWER → Credit
    /// - PASS / THIRD CLASS → Pass
    ///
    /// This public method checks each student's programme level (5, 6, 7, 8 = masters/graduate)
    /// and updates the 'deg' field accordingly for batch certificates and transcripts.
    /// </summary>
    public static void RemapMastersClassifications(ResultsData DS)
    {
        try
        {
            DataTable dt = DS.acad_GetBatchStudentTranscriptData;
            if (dt == null || dt.Rows.Count == 0)
                return;

            // Check if 'deg' column exists
            if (!dt.Columns.Contains("deg"))
                return;

            string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            
            // For each row in the transcript data
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string currentReg = dt.Rows[i]["regno"] != null ? dt.Rows[i]["regno"].ToString().Trim() : "";
                if (string.IsNullOrEmpty(currentReg))
                    continue;

                // Check if student is a masters/graduate student by programme level
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = @"SELECT p.levelCode 
                                   FROM acad_student s
                                   JOIN acad_programme p ON p.progcode = s.progid
                                   WHERE TRIM(s.regno) = @reg
                                   LIMIT 1";

                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@reg", currentReg);
                        object levelCode = cmd.ExecuteScalar();
                        
                        if (levelCode != null && levelCode != DBNull.Value)
                        {
                            int level = 0;
                            if (int.TryParse(levelCode.ToString(), out level))
                            {
                                // Masters/graduate levels: 5, 6, 7, 8
                                if (level >= 5 && level <= 8)
                                {
                                    string currentDeg = dt.Rows[i]["deg"] != null ? dt.Rows[i]["deg"].ToString().Trim().ToUpper() : "";
                                    string mappedDeg = currentDeg;

                                    // Map classifications
                                    if (currentDeg.Contains("FIRST"))
                                        mappedDeg = "Distinction";
                                    else if (currentDeg.Contains("SECOND"))
                                        mappedDeg = "Credit";
                                    else if (currentDeg.Contains("THIRD") || currentDeg.Contains("PASS"))
                                        mappedDeg = "Pass";

                                    // Update the deg field if mapping occurred
                                    if (mappedDeg != currentDeg)
                                        dt.Rows[i]["deg"] = mappedDeg;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail — use original classification if remapping fails
        }
    }

    /// <summary>
    /// Remaps degree classifications for masters students.
    /// Masters students use different classification labels:
    /// - FIRST CLASS → Distinction
    /// - SECOND CLASS UPPER / SECOND CLASS LOWER → Credit
    /// - PASS / THIRD CLASS → Pass
    ///
    /// This method checks the student's programme level (5, 6, 7, 8 = masters/graduate)
    /// and updates the 'deg' field accordingly.
    /// </summary>
    private static void RemapMastersClassification(ResultsData DS, string regno)
    {
        try
        {
            DataTable dt = DS.acad_GetBatchStudentTranscriptData;
            if (dt == null || dt.Rows.Count == 0)
                return;

            // Check if 'deg' column exists
            if (!dt.Columns.Contains("deg"))
                return;

            string connStr = System.Configuration.ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;
            
            // For each row in the transcript data
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string currentReg = dt.Rows[i]["regno"] != null ? dt.Rows[i]["regno"].ToString().Trim() : "";
                if (string.IsNullOrEmpty(currentReg))
                    currentReg = regno;

                // Check if student is a masters/graduate student by programme level
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = @"SELECT p.levelCode 
                                   FROM acad_student s
                                   JOIN acad_programme p ON p.progcode = s.progid
                                   WHERE TRIM(s.regno) = @reg
                                   LIMIT 1";

                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@reg", currentReg);
                        object levelCode = cmd.ExecuteScalar();
                        
                        if (levelCode != null && levelCode != DBNull.Value)
                        {
                            int level = 0;
                            if (int.TryParse(levelCode.ToString(), out level))
                            {
                                // Masters/graduate levels: 5, 6, 7, 8
                                if (level >= 5 && level <= 8)
                                {
                                    string currentDeg = dt.Rows[i]["deg"] != null ? dt.Rows[i]["deg"].ToString().Trim().ToUpper() : "";
                                    string mappedDeg = currentDeg;

                                    // Map classifications
                                    if (currentDeg.Contains("FIRST"))
                                        mappedDeg = "Distinction";
                                    else if (currentDeg.Contains("SECOND"))
                                        mappedDeg = "Credit";
                                    else if (currentDeg.Contains("THIRD") || currentDeg.Contains("PASS"))
                                        mappedDeg = "Pass";

                                    // Update the deg field if mapping occurred
                                    if(mappedDeg != currentDeg)
                                        dt.Rows[i]["deg"] = mappedDeg;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail — use original classification if remapping fails
        }
    }
}
