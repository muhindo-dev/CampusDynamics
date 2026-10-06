using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_AppraisalSessions : System.Web.UI.Page
{
    private string ConnStr
    {
        get { return System.Configuration.ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ─── Query-string helpers ──────────────────────────────────────────
    private int    QsPage   { get { int v; return int.TryParse(Request.QueryString["page"] ?? "1", out v) && v > 0 ? v : 1; } }
    private string QsSearch { get { return (Request.QueryString["q"]      ?? "").Trim(); } }
    private string QsStatus { get { return (Request.QueryString["status"] ?? "").Trim().ToUpper(); } }

    // ═══════════════════════════════════════════════════════════════════
    //  ONE-TIME SCHEMA MIGRATION
    // ═══════════════════════════════════════════════════════════════════
    private static bool _schemaChecked = false;

    private void EnsureDbSchema()
    {
        if (_schemaChecked) return;
        try
        {
            using (MySqlConnection conn = new MySqlConnection(ConnStr))
            {
                conn.Open();

                // ── 1. appraisal_sessions ──────────────────────────────────
                CreateTableIfMissing(conn, "appraisal_sessions", @"
                    CREATE TABLE `appraisal_sessions` (
                        `session_id`         INT AUTO_INCREMENT PRIMARY KEY,
                        `session_title`      VARCHAR(255) NOT NULL,
                        `session_description` TEXT,
                        `period_start`       DATE NOT NULL,
                        `period_end`         DATE NOT NULL,
                        `deadline`           DATE NOT NULL,
                        `target_categories`  VARCHAR(255) NOT NULL DEFAULT 'ACADEMIC,ADMINISTRATIVE,SUPPORT',
                        `status`             VARCHAR(20) NOT NULL DEFAULT 'DRAFT',
                        `created_by`         INT DEFAULT NULL,
                        `created_at`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                        `updated_at`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── 2. appraisal_records ───────────────────────────────────
                CreateTableIfMissing(conn, "appraisal_records", @"
                    CREATE TABLE `appraisal_records` (
                        `record_id`                INT AUTO_INCREMENT PRIMARY KEY,
                        `session_id`               INT NOT NULL,
                        `employee_id`              INT NOT NULL,
                        `reviewer_id`              INT DEFAULT NULL,
                        `staff_category`           VARCHAR(20) NOT NULL DEFAULT 'ADMINISTRATIVE',
                        `status`                   VARCHAR(30) NOT NULL DEFAULT 'PENDING',
                        `employee_submitted_at`    DATETIME DEFAULT NULL,
                        `supervisor_submitted_at`  DATETIME DEFAULT NULL,
                        `section_b_self_total`     DECIMAL(5,2) DEFAULT NULL,
                        `section_b_supervisor_total` DECIMAL(5,2) DEFAULT NULL,
                        `section_c_total`          DECIMAL(5,2) DEFAULT NULL,
                        `raw_score`                DECIMAL(5,2) DEFAULT NULL,
                        `max_possible`             DECIMAL(5,2) DEFAULT NULL,
                        `final_percentage`         DECIMAL(5,2) DEFAULT NULL,
                        `classification`           VARCHAR(50) DEFAULT NULL,
                        `created_at`               DATETIME DEFAULT CURRENT_TIMESTAMP,
                        `updated_at`               DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        UNIQUE KEY `uq_session_employee` (`session_id`,`employee_id`),
                        INDEX `idx_session` (`session_id`),
                        INDEX `idx_employee` (`employee_id`),
                        INDEX `idx_reviewer` (`reviewer_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── 3. appraisal_section_b ─────────────────────────────────
                CreateTableIfMissing(conn, "appraisal_section_b", @"
                    CREATE TABLE `appraisal_section_b` (
                        `entry_id`              INT AUTO_INCREMENT PRIMARY KEY,
                        `record_id`             INT NOT NULL,
                        `slot_number`           TINYINT NOT NULL,
                        `agreed_output`         TEXT,
                        `performance_indicators` TEXT,
                        `result_areas`          TEXT,
                        `self_rating`           TINYINT DEFAULT NULL,
                        `supervisor_rating`     TINYINT DEFAULT NULL,
                        `comments`              TEXT,
                        INDEX `idx_record` (`record_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── 4. appraisal_section_c ─────────────────────────────────
                CreateTableIfMissing(conn, "appraisal_section_c", @"
                    CREATE TABLE `appraisal_section_c` (
                        `entry_id`          INT AUTO_INCREMENT PRIMARY KEY,
                        `record_id`         INT NOT NULL,
                        `competency_code`   VARCHAR(10) NOT NULL,
                        `competency_name`   VARCHAR(255) NOT NULL,
                        `category_name`     VARCHAR(100) NOT NULL,
                        `rating`            TINYINT DEFAULT NULL,
                        `is_na`             TINYINT(1) NOT NULL DEFAULT 0,
                        `comment`           TEXT,
                        INDEX `idx_record` (`record_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── 5. appraisal_section_d ─────────────────────────────────
                CreateTableIfMissing(conn, "appraisal_section_d", @"
                    CREATE TABLE `appraisal_section_d` (
                        `entry_id`          INT AUTO_INCREMENT PRIMARY KEY,
                        `record_id`         INT NOT NULL,
                        `performance_gap`   TEXT,
                        `agreed_action`     TEXT,
                        `time_frame`        VARCHAR(100),
                        INDEX `idx_record` (`record_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── 6. appraisal_section_e ─────────────────────────────────
                CreateTableIfMissing(conn, "appraisal_section_e", @"
                    CREATE TABLE `appraisal_section_e` (
                        `entry_id`          INT AUTO_INCREMENT PRIMARY KEY,
                        `record_id`         INT NOT NULL,
                        `question_number`   TINYINT NOT NULL,
                        `question_text`     TEXT NOT NULL,
                        `response`          TEXT,
                        INDEX `idx_record` (`record_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── 7. appraisal_competency_templates ──────────────────────
                CreateTableIfMissing(conn, "appraisal_competency_templates", @"
                    CREATE TABLE `appraisal_competency_templates` (
                        `template_id`       INT AUTO_INCREMENT PRIMARY KEY,
                        `staff_category`    VARCHAR(20) NOT NULL,
                        `competency_code`   VARCHAR(10) NOT NULL,
                        `category_name`     VARCHAR(100) NOT NULL,
                        `competency_name`   VARCHAR(255) NOT NULL,
                        `description`       TEXT,
                        `sort_order`        INT NOT NULL DEFAULT 0,
                        INDEX `idx_category` (`staff_category`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── 8. appraisal_record_audit ─────────────────────────────
                CreateTableIfMissing(conn, "appraisal_record_audit", @"
                    CREATE TABLE `appraisal_record_audit` (
                        `audit_id`      INT AUTO_INCREMENT PRIMARY KEY,
                        `record_id`     INT NOT NULL,
                        `actor_empid`   INT DEFAULT NULL,
                        `action`        VARCHAR(50) NOT NULL,
                        `old_status`    VARCHAR(30),
                        `new_status`    VARCHAR(30),
                        `payload_json`  TEXT,
                        `created_at`    DATETIME DEFAULT CURRENT_TIMESTAMP,
                        INDEX `idx_record` (`record_id`),
                        INDEX `idx_actor`  (`actor_empid`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

                // ── Column migrations ──────────────────────────────────────
                AddColumnIfMissing(conn, "appraisal_section_c", "supervisor_comment", "TEXT DEFAULT NULL AFTER `comment`");
                AddColumnIfMissing(conn, "appraisal_records", "support_declaration", "VARCHAR(10) DEFAULT NULL COMMENT 'AGREE or DISAGREE: Support Staff declaration'");
                AddColumnIfMissing(conn, "appraisal_records", "supervisor_return_comment", "TEXT DEFAULT NULL COMMENT 'Reason given when supervisor returns form to employee'");
                AddColumnIfMissing(conn, "appraisal_records", "notify_email_status",  "VARCHAR(10) DEFAULT 'PENDING' COMMENT 'PENDING, SENT, FAILED, NO_EMAIL'");
                AddColumnIfMissing(conn, "appraisal_records", "notify_email_sent_at", "DATETIME DEFAULT NULL");
                AddColumnIfMissing(conn, "appraisal_records", "notify_email_error",   "VARCHAR(255) DEFAULT NULL");

                // ── Seed competency templates if empty ─────────────────────
                SeedCompetencyTemplates(conn);

                // ── Safety index to prevent duplicate employee records per session
                AddUniqueIndexIfMissing(conn, "appraisal_records", "uq_session_employee", "session_id,employee_id");
            }
        }
        catch { }
        _schemaChecked = true;
    }

    private void CreateTableIfMissing(MySqlConnection conn, string table, string createSql)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t", conn))
        {
            cmd.Parameters.AddWithValue("@t", table);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            {
                using (MySqlCommand create = new MySqlCommand(createSql, conn))
                {
                    create.ExecuteNonQuery();
                }
            }
        }
    }

    private void AddColumnIfMissing(MySqlConnection conn, string table, string column, string definition)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c", conn))
        {
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", column);
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            {
                using (MySqlCommand alter = new MySqlCommand(
                    string.Format("ALTER TABLE `{0}` ADD COLUMN `{1}` {2}", table, column, definition), conn))
                {
                    alter.ExecuteNonQuery();
                }
            }
        }
    }

    private void AddUniqueIndexIfMissing(MySqlConnection conn, string table, string indexName, string columnsCsv)
    {
        try
        {
            using (MySqlCommand cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND INDEX_NAME = @i", conn))
            {
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@i", indexName);
                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                {
                    using (MySqlCommand alter = new MySqlCommand(
                        string.Format("ALTER TABLE `{0}` ADD UNIQUE KEY `{1}` ({2})", table, indexName, columnsCsv), conn))
                    {
                        alter.ExecuteNonQuery();
                    }
                }
            }
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SEED COMPETENCY TEMPLATES
    // ═══════════════════════════════════════════════════════════════════
    private void SeedCompetencyTemplates(MySqlConnection conn)
    {
        // Seed ONLY an empty table. Templates are maintained by HR on the
        // CompetencyTemplates screen; this used to DELETE every template whenever the
        // Academic C1.1 wording differed from the built-in text, silently wiping HR's edits.
        using (MySqlCommand vchk = new MySqlCommand("SELECT COUNT(*) FROM appraisal_competency_templates", conn))
        {
            if (Convert.ToInt32(vchk.ExecuteScalar()) > 0) return;
        }

        // ── ACADEMIC STAFF: 45 explicit criteria (form states 50; 5 unconfirmed with HR)
        // Formula: x / 250 * 100
        int s = 0;

        // C1. Teaching Function (6)
        InsertTemplate(conn, "ACADEMIC", "C1.1", "Teaching Function", "This lecturer's class sessions were well organised", ++s);
        InsertTemplate(conn, "ACADEMIC", "C1.2", "Teaching Function", "The lecturer provided clear course outlines and outcomes", ++s);
        InsertTemplate(conn, "ACADEMIC", "C1.3", "Teaching Function", "The lecturer returns student work (assignments, tests, classwork) in a timely manner", ++s);
        InsertTemplate(conn, "ACADEMIC", "C1.4", "Teaching Function", "The lecturer provided well organized updated course notes, also uploaded online in PPT format", ++s);
        InsertTemplate(conn, "ACADEMIC", "C1.5", "Teaching Function", "The lecturer participates in curriculum development/developing course units", ++s);
        InsertTemplate(conn, "ACADEMIC", "C1.6", "Teaching Function", "Available to students for consultations", ++s);

        // C2. Administration (7)
        InsertTemplate(conn, "ACADEMIC", "C2.1", "Administration", "Communicates clearly with HoDs, Fellow lecturers and subordinates and Can be relied on to execute tasks", ++s);
        InsertTemplate(conn, "ACADEMIC", "C2.2", "Administration", "Generally meets deadlines, Shows interest in work and Performs extra duties", ++s);
        InsertTemplate(conn, "ACADEMIC", "C2.3", "Administration", "Loyal and takes advice", ++s);
        InsertTemplate(conn, "ACADEMIC", "C2.4", "Administration", "Attends departmental meetings regularly and contributes in meetings", ++s);
        InsertTemplate(conn, "ACADEMIC", "C2.5", "Administration", "Available for departmental work", ++s);
        InsertTemplate(conn, "ACADEMIC", "C2.6", "Administration", "Participates in the graduation ceremonies", ++s);
        InsertTemplate(conn, "ACADEMIC", "C2.7", "Administration", "Participates in students' orientation", ++s);

        // C3. Examinations (6)
        InsertTemplate(conn, "ACADEMIC", "C3.1", "Examinations", "Gives feedback to students on performance", ++s);
        InsertTemplate(conn, "ACADEMIC", "C3.2", "Examinations", "Is available for marking and submits examination results on time", ++s);
        InsertTemplate(conn, "ACADEMIC", "C3.3", "Examinations", "Adheres to examination regulations", ++s);
        InsertTemplate(conn, "ACADEMIC", "C3.4", "Examinations", "Participates in invigilation exercises", ++s);
        InsertTemplate(conn, "ACADEMIC", "C3.5", "Examinations", "Team player in team marking", ++s);
        InsertTemplate(conn, "ACADEMIC", "C3.6", "Examinations", "Available for examination moderation", ++s);

        // C4. Research (7)
        InsertTemplate(conn, "ACADEMIC", "C4.1", "Research", "Attends research presentations organized by the MRU Graduate Centre", ++s);
        InsertTemplate(conn, "ACADEMIC", "C4.2", "Research", "Has attended and presented papers at Conferences", ++s);
        InsertTemplate(conn, "ACADEMIC", "C4.3", "Research", "Has attended research training sessions/seminars organized by the MRU Graduate Centre", ++s);
        InsertTemplate(conn, "ACADEMIC", "C4.4", "Research", "Has published Articles", ++s);
        InsertTemplate(conn, "ACADEMIC", "C4.5", "Research", "Has published Monographs", ++s);
        InsertTemplate(conn, "ACADEMIC", "C4.6", "Research", "Has published Books", ++s);
        InsertTemplate(conn, "ACADEMIC", "C4.7", "Research", "Has contributed to Curriculum Development", ++s);

        // C5. Supervision (2)
        InsertTemplate(conn, "ACADEMIC", "C5.1", "Supervision", "Supervision of Undergraduate students", ++s);
        InsertTemplate(conn, "ACADEMIC", "C5.2", "Supervision", "Supervision of Graduate students", ++s);

        // C6. Training of Trainers (1)
        InsertTemplate(conn, "ACADEMIC", "C6.1", "Training of Trainers", "Attended TOTs, Short term courses", ++s);

        // C7. Relations (8)
        InsertTemplate(conn, "ACADEMIC", "C7.1", "Relations", "Relates to Students well", ++s);
        InsertTemplate(conn, "ACADEMIC", "C7.2", "Relations", "Relates to Administration well", ++s);
        InsertTemplate(conn, "ACADEMIC", "C7.3", "Relations", "Relates to fellow lecturers well", ++s);
        InsertTemplate(conn, "ACADEMIC", "C7.4", "Relations", "Relates to Heads of Departments well", ++s);
        InsertTemplate(conn, "ACADEMIC", "C7.5", "Relations", "Is aware of Students needs", ++s);
        InsertTemplate(conn, "ACADEMIC", "C7.6", "Relations", "Contributes to identifying students needs", ++s);
        InsertTemplate(conn, "ACADEMIC", "C7.7", "Relations", "Contributes to solving students problems", ++s);
        InsertTemplate(conn, "ACADEMIC", "C7.8", "Relations", "Deals well with Visitors", ++s);

        // C8. Skills (8)
        InsertTemplate(conn, "ACADEMIC", "C8.1", "Skills", "Knowledgeable in ICT", ++s);
        InsertTemplate(conn, "ACADEMIC", "C8.2", "Skills", "Uses Email effectively", ++s);
        InsertTemplate(conn, "ACADEMIC", "C8.3", "Skills", "Has attended ODeL training sessions", ++s);
        InsertTemplate(conn, "ACADEMIC", "C8.4", "Skills", "Has uploaded E-Learning materials", ++s);
        InsertTemplate(conn, "ACADEMIC", "C8.5", "Skills", "Has submitted E-Library books for his/her field", ++s);
        InsertTemplate(conn, "ACADEMIC", "C8.6", "Skills", "Practices blended learning", ++s);
        InsertTemplate(conn, "ACADEMIC", "C8.7", "Skills", "Inspires students", ++s);
        InsertTemplate(conn, "ACADEMIC", "C8.8", "Skills", "Inspires colleagues", ++s);

        // C9. Competences (7)
        InsertTemplate(conn, "ACADEMIC", "C9.1", "Competences", "Has high quality work", ++s);
        InsertTemplate(conn, "ACADEMIC", "C9.2", "Competences", "Applies rules to work", ++s);
        InsertTemplate(conn, "ACADEMIC", "C9.3", "Competences", "Is reliable", ++s);
        InsertTemplate(conn, "ACADEMIC", "C9.4", "Competences", "Is dependable", ++s);
        InsertTemplate(conn, "ACADEMIC", "C9.5", "Competences", "Has initiative and is hard working", ++s);
        InsertTemplate(conn, "ACADEMIC", "C9.6", "Competences", "Minds about his/her general appearance", ++s);
        InsertTemplate(conn, "ACADEMIC", "C9.7", "Competences", "Shares information", ++s);

        // ── ADMINISTRATIVE STAFF: 22 criteria (form states 30; 8 unconfirmed with HR)
        // Formula: x / 150 * 100
        s = 0;
        InsertTemplate(conn, "ADMINISTRATIVE", "A1",  "Core Competencies", "Professional Knowledge / Skills", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A2",  "Core Competencies", "Planning, Organising and Coordinating", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A3",  "Core Competencies", "Managing People", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A4",  "Core Competencies", "Decision Making", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A5",  "Core Competencies", "Team Work", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A6",  "Core Competencies", "Initiative", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A7",  "Core Competencies", "Writing and Communication Skills", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A8",  "Core Competencies", "Integrity", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A9",  "Core Competencies", "Time Management and Meeting Deadlines", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A10", "Core Competencies", "Meetings", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A11", "Core Competencies", "Dependability", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A12", "Core Competencies", "Loyalty", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A13", "Core Competencies", "Financial Management and Accountability", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A14", "Core Competencies", "Quality of Work and Results", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A15", "Core Competencies", "Record Keeping", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A16", "Core Competencies", "Interpersonal Relations", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A17", "Core Competencies", "Verbal and Listening Skills", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A18", "Core Competencies", "Discretion and Confidentiality", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A19", "Core Competencies", "Punctuality and Attendance", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A20", "Core Competencies", "Computer Knowledge", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A21", "Core Competencies", "Customer Care", ++s);
        InsertTemplate(conn, "ADMINISTRATIVE", "A22", "Core Competencies", "Adaptability and Flexibility", ++s);

        // ── SUPPORT STAFF: 25 explicit criteria (form states 30; 5 unconfirmed with HR)
        // Formula: x / 150 * 100
        s = 0;

        // Administrative Functions (5)
        InsertTemplate(conn, "SUPPORT", "S1",  "Administrative Functions", "Ensure that work/cleaning schedules are followed as closely as practical", ++s);
        InsertTemplate(conn, "SUPPORT", "S2",  "Administrative Functions", "Report all accidents/incidents to your supervisor on the shift they occur", ++s);
        InsertTemplate(conn, "SUPPORT", "S3",  "Administrative Functions", "Report to work as scheduled. Notify your supervisor per policy if you will be late/absent for your scheduled shift", ++s);
        InsertTemplate(conn, "SUPPORT", "S4",  "Administrative Functions", "Attend departmental and staff meetings as directed", ++s);
        InsertTemplate(conn, "SUPPORT", "S5",  "Administrative Functions", "Develop and maintain a good working relationship with staff, as well as with other departments to assure that cleanliness can be properly maintained", ++s);

        // Safety and Sanitation (5)
        InsertTemplate(conn, "SUPPORT", "S6",  "Safety and Sanitation", "Follow established safety procedures and precautions when performing tasks and when using equipment and supplies", ++s);
        InsertTemplate(conn, "SUPPORT", "S7",  "Safety and Sanitation", "Ensure that assigned work areas are maintained in a clean, safe, and sanitary manner", ++s);
        InsertTemplate(conn, "SUPPORT", "S8",  "Safety and Sanitation", "Report missing or improperly labeled containers of chemicals to your supervisor", ++s);
        InsertTemplate(conn, "SUPPORT", "S9",  "Safety and Sanitation", "Keep work areas free of hazardous objects such as protruding mop/broom handles, unnecessary equipment, supplies, etc.", ++s);
        InsertTemplate(conn, "SUPPORT", "S10", "Safety and Sanitation", "Follow proper techniques, including using appropriate personal protective equipment (PPE), when mixing chemicals, disinfectants and solutions used for cleaning", ++s);

        // Equipment and Supply Functions (4)
        InsertTemplate(conn, "SUPPORT", "S11", "Equipment and Supply Functions", "Keep supervisor informed of supply needs", ++s);
        InsertTemplate(conn, "SUPPORT", "S12", "Equipment and Supply Functions", "Assist others in lifting heavy equipment, supplies, etc., as directed or requested", ++s);
        InsertTemplate(conn, "SUPPORT", "S13", "Equipment and Supply Functions", "Ensure that equipment is cleaned and properly stored at the end of the shift", ++s);
        InsertTemplate(conn, "SUPPORT", "S14", "Equipment and Supply Functions", "Effective use of equipment without misuse or embezzlement", ++s);

        // Job Knowledge and Daily Work (2)
        InsertTemplate(conn, "SUPPORT", "S15", "Job Knowledge and Daily Work", "In depth knowledge of all requirements of the job. How well does the employee understand all phases of the job as defined by the performance standards set for the position?", ++s);
        InsertTemplate(conn, "SUPPORT", "S16", "Job Knowledge and Daily Work", "Perform day-to-day work as assigned and maintain all assigned areas clean", ++s);

        // Honesty and Conduct (5)
        InsertTemplate(conn, "SUPPORT", "S17", "Honesty and Conduct", "Maintain the confidentiality of University information", ++s);
        InsertTemplate(conn, "SUPPORT", "S18", "Honesty and Conduct", "Treat fellow workers with kindness, dignity and respect", ++s);
        InsertTemplate(conn, "SUPPORT", "S19", "Honesty and Conduct", "Knock before entering an office or room", ++s);
        InsertTemplate(conn, "SUPPORT", "S20", "Honesty and Conduct", "Inform officers when it is necessary to move his or her personal possessions during cleaning procedures", ++s);
        InsertTemplate(conn, "SUPPORT", "S21", "Honesty and Conduct", "Report allegations of abuse or neglect or misappropriation of University property to your supervisor", ++s);

        // Quality, Dependability, Attendance and Relations (4)
        InsertTemplate(conn, "SUPPORT", "S22", "Quality, Dependability, Attendance and Relations", "QUALITY OF WORK: Accuracy and neatness. Does the employee produce a high quality work product? Is quality work a priority for the employee?", ++s);
        InsertTemplate(conn, "SUPPORT", "S23", "Quality, Dependability, Attendance and Relations", "DEPENDABILITY: Employee needs little or no direction. To what extent can the employee be relied on to carry out instructions; and the degree to which the employee can work with limited supervision?", ++s);
        InsertTemplate(conn, "SUPPORT", "S24", "Quality, Dependability, Attendance and Relations", "ATTENDANCE AND PUNCTUALITY: Expected to report to work regularly and be ready to perform your assigned duties at the beginning of your assigned work shift. Is the employee absent frequently? Are the absences affecting his/her performance?", ++s);
        InsertTemplate(conn, "SUPPORT", "S25", "Quality, Dependability, Attendance and Relations", "RELATIONS WITH OTHERS: Consider employee's abilities to maintain a positive and harmonious attitude in the work environment. How well does the employee relate to the supervisors, co-workers and the broader University community?", ++s);

        // Migrate names in existing section_c rows that are still editable (not yet under supervisor review)
        using (MySqlCommand mig = new MySqlCommand(
            @"UPDATE appraisal_section_c sc
              INNER JOIN appraisal_competency_templates t
                ON t.competency_code = sc.competency_code
               AND t.staff_category  = (SELECT staff_category FROM appraisal_records WHERE record_id = sc.record_id)
              INNER JOIN appraisal_records ar ON ar.record_id = sc.record_id
              SET sc.competency_name = t.competency_name,
                  sc.category_name   = t.category_name
              WHERE ar.status IN ('PENDING','EMPLOYEE_IN_PROGRESS','RETURNED')", conn))
        {
            mig.ExecuteNonQuery();
        }
    }

    private void InsertTemplate(MySqlConnection conn, string category, string code, string catName, string name, int order)
    {
        using (MySqlCommand cmd = new MySqlCommand(
            @"INSERT INTO appraisal_competency_templates (staff_category, competency_code, category_name, competency_name, sort_order)
              VALUES (@cat, @code, @catName, @name, @ord)", conn))
        {
            cmd.Parameters.AddWithValue("@cat", category);
            cmd.Parameters.AddWithValue("@code", code);
            cmd.Parameters.AddWithValue("@catName", catName);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@ord", order);
            cmd.ExecuteNonQuery();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAGE LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════
    protected void Page_Load(object sender, EventArgs e)
    {
        string ajax = Request.QueryString["ajax"];
        if (!string.IsNullOrEmpty(ajax))
        {
            if (!HrAccess.RequireHr(true)) return;
            EnsureDbSchema();
            HandleAjax(ajax);
            return;
        }

        if (!HrAccess.RequireHr(false)) return;
        EnsureDbSchema();

        if (!IsPostBack)
        {
            BindGrid();
            LoadStats();
            ShowFlashMessage();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX ROUTER (caller already passed HrAccess.RequireHr)
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        Response.Clear();
        Response.ContentType = "application/json";
        try
        {
            if (action == "generate_appraisals" || action == "backfill_active" ||
                action == "delete_session"       || action == "delete_record"   ||
                action == "send_notifications"   || action == "send_single_notify")
            {
                if (!MarksAntiForgeryService.ValidateRequest())
                {
                    Response.Write("{\"error\":\"The page has expired. Refresh it and try again.\"}");
                    EndAjax();
                    return;
                }
            }
            switch (action)
            {
                case "get_session":         AjaxGetSession(); break;
                case "get_session_detail":  AjaxGetSessionDetail(); break;
                case "generate_appraisals": AjaxGenerateAppraisals(); break;
                case "backfill_active":     AjaxBackfillActiveSession(); break;
                case "delete_session":      AjaxDeleteSession(); break;
                case "delete_record":       AjaxDeleteRecord(); break;
                case "send_notifications":  AjaxSendNotifications(); break;
                case "get_notify_status":   AjaxGetNotifyStatus(); break;
                case "send_single_notify":  AjaxSendSingleNotify(); break;
                default:
                    Response.Write("{\"error\":\"Unknown action.\"}");
                    break;
            }
        }
        catch (System.Threading.ThreadAbortException) { throw; }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("AppraisalSessions " + action + ": " + ex);
            Response.Write("{\"error\":\"The request could not be completed. Try again, or contact MIS if it keeps failing.\"}");
        }
        EndAjax();
    }

    private void EndAjax()
    {
        try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
    }

    // ─── AJAX: session for the edit dialog ────────────────────────────
    private void AjaxGetSession()
    {
        int id;
        if (!int.TryParse(Request.QueryString["id"], out id))
        { Response.Write("{\"error\":\"Invalid session.\"}"); return; }

        DataTable dt = ExecuteQuery("SELECT * FROM appraisal_sessions WHERE session_id = @id", new MySqlParameter("@id", id));
        if (dt.Rows.Count == 0) { Response.Write("{\"error\":\"The session was not found.\"}"); return; }

        DataRow r = dt.Rows[0];
        StringBuilder sb = new StringBuilder("{");
        bool first = true;
        foreach (DataColumn col in dt.Columns)
        {
            if (!first) sb.Append(",");
            first = false;
            string val = "";
            if (r[col] != null && r[col] != DBNull.Value)
                val = col.DataType == typeof(DateTime) ? ((DateTime)r[col]).ToString("yyyy-MM-dd") : r[col].ToString();
            sb.AppendFormat("\"{0}\":\"{1}\"", col.ColumnName, EscapeJson(val));
        }
        sb.Append("}");
        Response.Write(sb.ToString());
    }

    // ─── AJAX: session detail (information, counts, notifications) ────
    // Population: every appraisal_records row of the session (no contract filter).
    private void AjaxGetSessionDetail()
    {
        int id;
        if (!int.TryParse(Request.QueryString["id"], out id))
        { Response.Write("{\"error\":\"Invalid session.\"}"); return; }

        DataTable dtS = ExecuteQuery(
            @"SELECT s.*, IFNULL(cr.emp_name,'') AS created_by_name
              FROM appraisal_sessions s
              LEFT JOIN hrm_employee cr ON cr.empID = s.created_by
              WHERE s.session_id = @id",
            new MySqlParameter("@id", id));
        if (dtS.Rows.Count == 0) { Response.Write("{\"error\":\"The session was not found.\"}"); return; }
        DataRow sess = dtS.Rows[0];

        DataTable dtSt = ExecuteQuery(
            "SELECT status, COUNT(*) AS n FROM appraisal_records WHERE session_id = @id GROUP BY status",
            new MySqlParameter("@id", id));
        Dictionary<string, int> counts = new Dictionary<string, int>();
        int total = 0, completed = 0;
        foreach (DataRow r in dtSt.Rows)
        {
            string st = SafeVal(r["status"]).ToUpper();
            int n = SafeInt(r["n"]);
            counts[st] = n;
            total += n;
            if (st == "COMPLETED" || st == "HR_REVIEWED") completed += n;
        }

        DataTable dtN = ExecuteQuery(
            @"SELECT IFNULL(notify_email_status,'PENDING') AS ns, COUNT(*) AS n
              FROM appraisal_records WHERE session_id = @id GROUP BY IFNULL(notify_email_status,'PENDING')",
            new MySqlParameter("@id", id));
        int nSent = 0, nFailed = 0, nNoEmail = 0, nPending = 0;
        foreach (DataRow r in dtN.Rows)
        {
            switch (SafeVal(r["ns"]).ToUpper())
            {
                case "SENT": nSent += SafeInt(r["n"]); break;
                case "FAILED": nFailed += SafeInt(r["n"]); break;
                case "NO_EMAIL": nNoEmail += SafeInt(r["n"]); break;
                default: nPending += SafeInt(r["n"]); break;
            }
        }

        DataTable dtU = ExecuteQuery(
            @"SELECT ar.record_id, IFNULL(e.emp_name, CONCAT('Staff record not found (ID ', ar.employee_id, ')')) AS emp_name,
                     ar.notify_email_status AS ns
              FROM appraisal_records ar
              LEFT JOIN hrm_employee e ON e.empID = ar.employee_id
              WHERE ar.session_id = @id AND ar.notify_email_status IN ('FAILED','NO_EMAIL')
              ORDER BY ar.notify_email_status, e.emp_name
              LIMIT 300",
            new MySqlParameter("@id", id));

        string status = SafeVal(sess["status"]).ToUpper();
        List<string> cats = new List<string>();
        foreach (string c in SafeVal(sess["target_categories"]).Split(','))
            if (c.Trim() != "") cats.Add(CategoryWord(c.Trim()));
        string createdBy = SafeVal(sess["created_by_name"]);

        StringBuilder sb = new StringBuilder("{");
        sb.AppendFormat("\"session_id\":{0},", SafeInt(sess["session_id"]));
        sb.AppendFormat("\"session_title\":\"{0}\",", EscapeJson(sess["session_title"]));
        sb.AppendFormat("\"status\":\"{0}\",", EscapeJson(status));
        sb.AppendFormat("\"status_html\":\"{0}\",", EscapeJson(SessionBadge(status)));
        sb.AppendFormat("\"period_text\":\"{0}\",", EscapeJson(FormatDateDisplay(sess["period_start"]) + " to " + FormatDateDisplay(sess["period_end"])));
        sb.AppendFormat("\"deadline_text\":\"{0}\",", EscapeJson(FormatDateDisplay(sess["deadline"])));
        sb.AppendFormat("\"categories_text\":\"{0}\",", EscapeJson(string.Join(", ", cats.ToArray())));
        sb.AppendFormat("\"created_text\":\"{0}\",", EscapeJson((createdBy != "" ? createdBy + ", " : "") + FormatDateDisplay(sess["created_at"])));
        sb.AppendFormat("\"total\":{0},\"completed\":{1},", total, completed);
        sb.AppendFormat("\"n_sent\":{0},\"n_failed\":{1},\"n_noemail\":{2},\"n_pending\":{3},", nSent, nFailed, nNoEmail, nPending);

        sb.Append("\"statuses\":[");
        string[] order = { "PENDING", "EMPLOYEE_IN_PROGRESS", "RETURNED", "EMPLOYEE_SUBMITTED", "SUPERVISOR_IN_PROGRESS", "COMPLETED", "HR_REVIEWED", "CANCELLED" };
        for (int i = 0; i < order.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendFormat("{{\"label\":\"{0}\",\"count\":{1}}}", StatusWord(order[i]), counts.ContainsKey(order[i]) ? counts[order[i]] : 0);
        }
        sb.Append("],\"undelivered\":[");
        for (int i = 0; i < dtU.Rows.Count; i++)
        {
            DataRow u = dtU.Rows[i];
            bool failed = SafeVal(u["ns"]).ToUpper() == "FAILED";
            if (i > 0) sb.Append(",");
            sb.AppendFormat("{{\"record_id\":{0},\"emp_name\":\"{1}\",\"state\":\"{2}\",\"can_send\":{3}}}",
                SafeInt(u["record_id"]), EscapeJson(u["emp_name"]), failed ? "Not delivered" : "No email address",
                failed && status == "ACTIVE" ? "true" : "false");
        }
        sb.Append("]}");
        Response.Write(sb.ToString());
    }

    // ─── AJAX: create missing appraisals for a session ────────────────
    private void AjaxGenerateAppraisals()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request.\"}"); return; }
        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId))
        { Response.Write("{\"error\":\"Invalid session.\"}"); return; }

        DataTable dtSess = ExecuteQuery(
            "SELECT target_categories, status FROM appraisal_sessions WHERE session_id = @id",
            new MySqlParameter("@id", sessionId));
        if (dtSess.Rows.Count == 0) { Response.Write("{\"error\":\"The session was not found.\"}"); return; }
        if (dtSess.Rows[0]["status"].ToString() != "ACTIVE")
        { Response.Write("{\"error\":\"Appraisals can be created only in an active session.\"}"); return; }

        int count = GenerateMissingAppraisals(sessionId, dtSess.Rows[0]["target_categories"].ToString());
        Response.Write(string.Format("{{\"success\":true,\"count\":{0}}}", count));
    }

    private void AjaxBackfillActiveSession()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request.\"}"); return; }

        DataTable dtActive = ExecuteQuery(
            @"SELECT session_id, session_title, target_categories
              FROM appraisal_sessions WHERE status = 'ACTIVE' ORDER BY created_at DESC LIMIT 1");
        if (dtActive.Rows.Count == 0) { Response.Write("{\"error\":\"There is no active session.\"}"); return; }

        DataRow active = dtActive.Rows[0];
        int sessionId = SafeInt(active["session_id"]);
        int count = GenerateMissingAppraisals(sessionId, SafeVal(active["target_categories"]));
        Response.Write(string.Format("{{\"success\":true,\"session_id\":{0},\"session_title\":\"{1}\",\"count\":{2}}}",
            sessionId, EscapeJson(SafeVal(active["session_title"])), count));
    }

    // ─── AJAX: delete session (cascade, audit kept) ───────────────────
    private void AjaxDeleteSession()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request.\"}"); return; }

        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId) || sessionId <= 0)
        { Response.Write("{\"error\":\"Invalid session.\"}"); return; }

        DataTable dtCheck = ExecuteQuery("SELECT COUNT(*) AS cnt FROM appraisal_sessions WHERE session_id = @id", new MySqlParameter("@id", sessionId));
        if (SafeInt(dtCheck.Rows[0]["cnt"]) == 0) { Response.Write("{\"error\":\"The session was not found.\"}"); return; }

        DataTable dtRecs = ExecuteQuery("SELECT record_id FROM appraisal_records WHERE session_id = @id", new MySqlParameter("@id", sessionId));
        List<int> recordIds = new List<int>();
        foreach (DataRow r in dtRecs.Rows) recordIds.Add(SafeInt(r["record_id"]));

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                if (recordIds.Count > 0)
                {
                    string idList = string.Join(",", recordIds);
                    foreach (string tbl in new[] { "appraisal_section_b", "appraisal_section_c", "appraisal_section_d", "appraisal_section_e" })
                    {
                        using (MySqlCommand cmd = new MySqlCommand(
                            string.Format("DELETE FROM `{0}` WHERE record_id IN ({1})", tbl, idList), conn, tx))
                            cmd.ExecuteNonQuery();
                    }
                    using (MySqlCommand cmd = new MySqlCommand(
                        @"INSERT INTO appraisal_record_audit (record_id, actor_empid, actor_username, action, old_status, new_status, payload_json, created_at)
                          SELECT record_id, @emp, @usr, 'RECORD_DELETED', status, NULL,
                                 CONCAT('{""reason"":""session deleted"",""session_id"":', session_id, ',""employee_id"":', employee_id, '}'), NOW()
                          FROM appraisal_records WHERE record_id IN (" + idList + ")", conn, tx))
                    {
                        int emp = ActorEmpId();
                        cmd.Parameters.AddWithValue("@emp", emp > 0 ? (object)emp : DBNull.Value);
                        cmd.Parameters.AddWithValue("@usr", "eadmin:" + CurrentUsername());
                        cmd.ExecuteNonQuery();
                    }
                    using (MySqlCommand cmd = new MySqlCommand("DELETE FROM appraisal_records WHERE session_id = @sid", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        cmd.ExecuteNonQuery();
                    }
                }
                using (MySqlCommand cmd = new MySqlCommand("DELETE FROM appraisal_sessions WHERE session_id = @sid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }
        Response.Write(string.Format("{{\"success\":true,\"deleted_records\":{0}}}", recordIds.Count));
    }

    // ─── AJAX: delete one appraisal (cascade, audit kept) ─────────────
    private void AjaxDeleteRecord()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request.\"}"); return; }

        int recordId;
        if (!int.TryParse(Request.QueryString["id"], out recordId) || recordId <= 0)
        { Response.Write("{\"error\":\"Invalid appraisal.\"}"); return; }

        DataTable dtCheck = ExecuteQuery("SELECT COUNT(*) AS cnt FROM appraisal_records WHERE record_id = @id", new MySqlParameter("@id", recordId));
        if (SafeInt(dtCheck.Rows[0]["cnt"]) == 0) { Response.Write("{\"error\":\"The appraisal was not found.\"}"); return; }

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                using (MySqlCommand cmd = new MySqlCommand(
                    @"INSERT INTO appraisal_record_audit (record_id, actor_empid, actor_username, action, old_status, new_status, payload_json, created_at)
                      SELECT record_id, @emp, @usr, 'RECORD_DELETED', status, NULL,
                             CONCAT('{""session_id"":', session_id, ',""employee_id"":', employee_id,
                                    ',""final_percentage"":', IFNULL(final_percentage,'null'), '}'), NOW()
                      FROM appraisal_records WHERE record_id = @rid", conn, tx))
                {
                    int emp = ActorEmpId();
                    cmd.Parameters.AddWithValue("@emp", emp > 0 ? (object)emp : DBNull.Value);
                    cmd.Parameters.AddWithValue("@usr", "eadmin:" + CurrentUsername());
                    cmd.Parameters.AddWithValue("@rid", recordId);
                    cmd.ExecuteNonQuery();
                }
                foreach (string tbl in new[] { "appraisal_section_b", "appraisal_section_c", "appraisal_section_d", "appraisal_section_e" })
                {
                    using (MySqlCommand cmd = new MySqlCommand(string.Format("DELETE FROM `{0}` WHERE record_id = @rid", tbl), conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@rid", recordId);
                        cmd.ExecuteNonQuery();
                    }
                }
                using (MySqlCommand cmd = new MySqlCommand("DELETE FROM appraisal_records WHERE record_id = @rid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@rid", recordId);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }
        Response.Write("{\"success\":true}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FORM HANDLERS (post, redirect, get)
    // ═══════════════════════════════════════════════════════════════════
    protected void btnCreateSession_Click(object sender, EventArgs e)
    {
        if (!HrAccess.IsHr()) { RedirectWithFlash("You do not have access to change appraisal sessions.", false); return; }
        string title    = txtTitle.Text.Trim();
        string desc     = txtDescription.Text.Trim();
        string pStart   = txtPeriodStart.Text.Trim();
        string pEnd     = txtPeriodEnd.Text.Trim();
        string deadline = txtDeadline.Text.Trim();

        List<string> cats = new List<string>();
        if (chkAcademic.Checked) cats.Add("ACADEMIC");
        if (chkAdministrative.Checked) cats.Add("ADMINISTRATIVE");
        if (chkSupport.Checked) cats.Add("SUPPORT");

        string err = ValidateSession(title, pStart, pEnd, deadline, cats.Count);
        if (err != "") { ShowInlineError(err, "createModal"); return; }

        ExecuteNonQuery(
            @"INSERT INTO appraisal_sessions (session_title, session_description, period_start, period_end, deadline, target_categories, status, created_by)
              VALUES (@title, @desc, @ps, @pe, @dl, @cats, 'DRAFT', @cb)",
            new MySqlParameter("@title", title),
            new MySqlParameter("@desc", desc),
            new MySqlParameter("@ps", pStart),
            new MySqlParameter("@pe", pEnd),
            new MySqlParameter("@dl", deadline),
            new MySqlParameter("@cats", string.Join(",", cats)),
            new MySqlParameter("@cb", DBNull.Value));

        RedirectWithFlash("Session created as a draft.", true);
    }

    protected void btnEditSession_Click(object sender, EventArgs e)
    {
        if (!HrAccess.IsHr()) { RedirectWithFlash("You do not have access to change appraisal sessions.", false); return; }
        int id;
        if (!int.TryParse(hfEditSessionId.Value, out id) || id <= 0) { ShowInlineError("The session could not be identified. Open it again.", null); return; }

        string title     = txtEditTitle.Text.Trim();
        string desc      = txtEditDescription.Text.Trim();
        string pStart    = txtEditPeriodStart.Text.Trim();
        string pEnd      = txtEditPeriodEnd.Text.Trim();
        string deadline  = txtEditDeadline.Text.Trim();
        string newStatus = ddlEditStatus.SelectedValue.ToUpper();
        if (newStatus != "DRAFT" && newStatus != "ACTIVE" && newStatus != "CLOSED" && newStatus != "ARCHIVED") newStatus = "DRAFT";

        List<string> cats = new List<string>();
        if (chkEditAcademic.Checked) cats.Add("ACADEMIC");
        if (chkEditAdministrative.Checked) cats.Add("ADMINISTRATIVE");
        if (chkEditSupport.Checked) cats.Add("SUPPORT");
        string targetCats = string.Join(",", cats);

        string err = ValidateSession(title, pStart, pEnd, deadline, cats.Count);
        if (err != "") { ShowInlineError(err, "editModal"); return; }

        if (newStatus == "ACTIVE")
        {
            DataTable dtOther = ExecuteQuery(
                "SELECT COUNT(*) AS cnt FROM appraisal_sessions WHERE status = 'ACTIVE' AND session_id <> @id",
                new MySqlParameter("@id", id));
            if (SafeInt(dtOther.Rows[0]["cnt"]) > 0)
            {
                ShowInlineError("Another session is active. Close it before activating this one.", "editModal");
                return;
            }
        }

        ExecuteNonQuery(
            @"UPDATE appraisal_sessions
              SET session_title = @title, session_description = @desc,
                  period_start = @ps, period_end = @pe, deadline = @dl,
                  target_categories = @cats, status = @st
              WHERE session_id = @id",
            new MySqlParameter("@title", title),
            new MySqlParameter("@desc",  desc),
            new MySqlParameter("@ps",    pStart),
            new MySqlParameter("@pe",    pEnd),
            new MySqlParameter("@dl",    deadline),
            new MySqlParameter("@cats",  targetCats),
            new MySqlParameter("@st",    newStatus),
            new MySqlParameter("@id",    id));

        if (newStatus == "ACTIVE") GenerateMissingAppraisals(id, targetCats);
        RedirectWithFlash("Session saved.", true);
    }

    private static string ValidateSession(string title, string pStart, string pEnd, string deadline, int catCount)
    {
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(pStart) || string.IsNullOrEmpty(pEnd) || string.IsNullOrEmpty(deadline))
            return "Fill in the title, the period and the deadline.";
        if (catCount == 0) return "Choose at least one staff category.";
        DateTime dStart, dEnd, dDeadline;
        if (!DateTime.TryParse(pStart, out dStart) || !DateTime.TryParse(pEnd, out dEnd) || !DateTime.TryParse(deadline, out dDeadline))
            return "Enter valid dates.";
        if (dStart > dEnd) return "The period must start before it ends.";
        if (dDeadline < dStart || dDeadline > dEnd) return "The deadline must fall within the appraisal period.";
        return "";
    }

    private int GenerateMissingAppraisals(int sessionId, string categoriesCsv)
    {
        string[] catArr = (categoriesCsv ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> normalizedCats = new List<string>();
        List<string> catConditions = new List<string>();
        for (int i = 0; i < catArr.Length; i++)
        {
            string cat = catArr[i].Trim().ToUpper();
            if (cat == "ACADEMIC" || cat == "ADMINISTRATIVE" || cat == "SUPPORT")
            {
                if (!normalizedCats.Contains(cat)) normalizedCats.Add(cat);
                catConditions.Add(BuildEmpTypeCondition(cat));
            }
        }
        if (normalizedCats.Count == 0) return 0;

        bool includesAllTargets = normalizedCats.Contains("ACADEMIC") && normalizedCats.Contains("ADMINISTRATIVE") && normalizedCats.Contains("SUPPORT");
        string catWhere = includesAllTargets ? "1=1" : "(" + string.Join(" OR ", catConditions) + ")";
        DataTable dtEmps = ExecuteQuery(string.Format(
            @"SELECT e.empID, e.EmpType
              FROM hrm_employee e
              WHERE IFNULL(e.to_be_appraised, 1) = 1
                AND IFNULL(e.employment_status, 'ACTIVE') IN ('ACTIVE','PROBATION')
                AND {0}
              ORDER BY e.emp_name", catWhere));
        int count = 0;

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                foreach (DataRow emp in dtEmps.Rows)
                {
                    string staffCat = ResolveStaffCategoryFromEmpType(SafeVal(emp["EmpType"]));
                    // Supervisor and expected-standards group come from the shared DB rules
                    // (appraisal_resolve_reviewer / appraisal_resolve_group; NULL = HR must assign).
                    using (MySqlCommand cmd = new MySqlCommand(
                        @"INSERT INTO appraisal_records (session_id, employee_id, reviewer_id, staff_category, status, standards_group_id)
                          SELECT @sid, @eid, appraisal_resolve_reviewer(@eid), @scat, 'PENDING', appraisal_resolve_group(@eid, @scat)
                          FROM DUAL
                          WHERE NOT EXISTS (SELECT 1 FROM appraisal_records WHERE session_id = @sid AND employee_id = @eid)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        cmd.Parameters.AddWithValue("@eid", SafeInt(emp["empID"]));
                        cmd.Parameters.AddWithValue("@scat", staffCat);
                        count += cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }
        return count;
    }

    private string BuildEmpTypeCondition(string category)
    {
        switch ((category ?? "").ToUpper())
        {
            case "ACADEMIC":
                return "(UPPER(IFNULL(e.EmpType,'')) LIKE '%ACADEMIC%' AND UPPER(IFNULL(e.EmpType,'')) NOT LIKE '%NON%')";
            case "ADMINISTRATIVE":
                return "(UPPER(IFNULL(e.EmpType,'')) LIKE '%NON-ACADEMIC%' OR UPPER(IFNULL(e.EmpType,'')) LIKE '%NON ACADEMIC%' OR UPPER(IFNULL(e.EmpType,'')) LIKE '%ADMIN%')";
            case "SUPPORT":
                return "UPPER(IFNULL(e.EmpType,'')) LIKE '%SUPPORT%'";
            default:
                return "1=0";
        }
    }

    private string ResolveStaffCategoryFromEmpType(string empType)
    {
        string normalized = (empType ?? string.Empty).ToUpper();
        if (normalized.Contains("SUPPORT")) return "SUPPORT";
        if (normalized.Contains("NON-ACADEMIC") || normalized.Contains("NON ACADEMIC") || normalized.Contains("ADMIN")) return "ADMINISTRATIVE";
        if (normalized.Contains("ACADEMIC")) return "ACADEMIC";
        return "ADMINISTRATIVE";
    }

    protected void btnActivateSession_Click(object sender, EventArgs e)
    {
        if (!HrAccess.IsHr()) { RedirectWithFlash("You do not have access to change appraisal sessions.", false); return; }
        int id;
        if (!int.TryParse(hfActionSessionId.Value, out id) || id <= 0) return;

        DataTable dtActive = ExecuteQuery(
            "SELECT COUNT(*) AS cnt FROM appraisal_sessions WHERE status = 'ACTIVE' AND session_id <> @id",
            new MySqlParameter("@id", id));
        if (SafeInt(dtActive.Rows[0]["cnt"]) > 0)
        {
            RedirectWithFlash("Another session is active. Close it before activating this one.", false);
            return;
        }

        int changed = ExecuteNonQuery("UPDATE appraisal_sessions SET status = 'ACTIVE' WHERE session_id = @id AND status = 'DRAFT'",
            new MySqlParameter("@id", id));
        if (changed == 0) { RedirectWithFlash("Only a draft session can be activated.", false); return; }

        DataTable dtCats = ExecuteQuery("SELECT target_categories FROM appraisal_sessions WHERE session_id = @id", new MySqlParameter("@id", id));
        string cats = dtCats.Rows.Count > 0 ? SafeVal(dtCats.Rows[0]["target_categories"]) : "";
        int generated = GenerateMissingAppraisals(id, cats);
        RedirectWithFlash(string.Format("Session activated. {0} appraisal(s) created.", generated), true);
    }

    protected void btnCloseSession_Click(object sender, EventArgs e)
    {
        if (!HrAccess.IsHr()) { RedirectWithFlash("You do not have access to change appraisal sessions.", false); return; }
        int id;
        if (!int.TryParse(hfActionSessionId.Value, out id) || id <= 0) return;
        ExecuteNonQuery("UPDATE appraisal_sessions SET status = 'CLOSED' WHERE session_id = @id AND status = 'ACTIVE'", new MySqlParameter("@id", id));
        RedirectWithFlash("Session closed.", true);
    }

    protected void btnArchiveSession_Click(object sender, EventArgs e)
    {
        if (!HrAccess.IsHr()) { RedirectWithFlash("You do not have access to change appraisal sessions.", false); return; }
        int id;
        if (!int.TryParse(hfActionSessionId.Value, out id) || id <= 0) return;
        ExecuteNonQuery("UPDATE appraisal_sessions SET status = 'ARCHIVED' WHERE session_id = @id AND status = 'CLOSED'", new MySqlParameter("@id", id));
        RedirectWithFlash("Session archived.", true);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LIST. Population: every appraisal_records row of the session.
    // ═══════════════════════════════════════════════════════════════════
    private void BindGrid()
    {
        StringBuilder where = new StringBuilder("WHERE 1=1");
        List<MySqlParameter> parms = new List<MySqlParameter>();
        if (!string.IsNullOrEmpty(QsSearch))
        {
            where.Append(" AND s.session_title LIKE @q");
            parms.Add(new MySqlParameter("@q", "%" + QsSearch + "%"));
        }
        if (!string.IsNullOrEmpty(QsStatus))
        {
            where.Append(" AND s.status = @st");
            parms.Add(new MySqlParameter("@st", QsStatus));
        }

        int totalRecords = SafeInt(ExecuteQuery("SELECT COUNT(*) FROM appraisal_sessions s " + where, parms.ToArray()).Rows[0][0]);
        int pageSize = 20;
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalRecords / pageSize));
        int currentPage = Math.Min(QsPage, totalPages);
        int offset = (currentPage - 1) * pageSize;

        DataTable dtData = ExecuteQuery(string.Format(
            @"SELECT s.*,
                     IFNULL(cr.emp_name, '') AS created_by_name,
                     (SELECT COUNT(*) FROM appraisal_records ar WHERE ar.session_id = s.session_id) AS total_records,
                     (SELECT COUNT(*) FROM appraisal_records ar WHERE ar.session_id = s.session_id
                         AND ar.status IN ('COMPLETED','HR_REVIEWED')) AS completed_records
              FROM appraisal_sessions s
              LEFT JOIN hrm_employee cr ON cr.empID = s.created_by
              {0}
              ORDER BY s.created_at DESC
              LIMIT {1} OFFSET {2}", where, pageSize, offset), CloneParams(parms));

        StringBuilder html = new StringBuilder();
        if (dtData.Rows.Count == 0)
        {
            html.Append("<tr><td colspan='7' class='hr-empty'>No sessions match the filters.</td></tr>");
        }
        else
        {
            foreach (DataRow r in dtData.Rows)
            {
                int sessId = SafeInt(r["session_id"]);
                List<string> cats = new List<string>();
                foreach (string c in SafeVal(r["target_categories"]).Split(','))
                    if (c.Trim() != "") cats.Add(CategoryWord(c.Trim()));

                html.AppendFormat("<tr class='is-click' onclick='viewSession({0})'>", sessId);
                html.AppendFormat("<td><strong>{0}</strong></td>", Enc(SafeVal(r["session_title"])));
                html.AppendFormat("<td style='white-space:nowrap;'>{0} to {1}</td>", FormatDateDisplay(r["period_start"]), FormatDateDisplay(r["period_end"]));
                html.AppendFormat("<td style='white-space:nowrap;'>{0}</td>", FormatDateDisplay(r["deadline"]));
                html.AppendFormat("<td>{0}</td>", Enc(string.Join(", ", cats.ToArray())));
                html.AppendFormat("<td>{0}</td>", SessionBadge(SafeVal(r["status"]).ToUpper()));
                html.AppendFormat("<td>{0}</td>", Progress(SafeInt(r["total_records"]), SafeInt(r["completed_records"])));
                html.AppendFormat("<td class='hr-right' onclick='event.stopPropagation()' style='white-space:nowrap;'>" +
                    "<button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick='openEditModal({0})'>Edit</button> " +
                    "<button type='button' class='hr-btn hr-btn--secondary hr-btn--sm' onclick='viewSession({0})'>Open</button></td>", sessId);
                html.Append("</tr>");
            }
        }
        litGridBody.Text = html.ToString();

        litPagerInfo.Text = totalRecords == 0 ? "No sessions"
            : string.Format("Showing {0} to {1} of {2}", offset + 1, Math.Min(offset + pageSize, totalRecords), totalRecords);
        BuildPager(currentPage, totalPages);
    }

    private void LoadStats()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT
                (SELECT COUNT(*) FROM appraisal_sessions) AS total_sessions,
                (SELECT COUNT(*) FROM appraisal_sessions WHERE status = 'ACTIVE') AS active_sessions,
                (SELECT COUNT(*) FROM appraisal_sessions WHERE status = 'DRAFT') AS draft_sessions,
                (SELECT COUNT(*) FROM appraisal_records) AS total_appraisals,
                (SELECT COUNT(*) FROM appraisal_records WHERE status IN ('COMPLETED','HR_REVIEWED')) AS completed_appraisals");
        if (dt.Rows.Count == 0) return;
        DataRow r = dt.Rows[0];
        litStatTotal.Text      = SafeInt(r["total_sessions"]).ToString("N0");
        litStatActive.Text     = SafeInt(r["active_sessions"]).ToString("N0");
        litStatDraft.Text      = SafeInt(r["draft_sessions"]).ToString("N0");
        litStatAppraisals.Text = SafeInt(r["total_appraisals"]).ToString("N0");
        litStatCompleted.Text  = SafeInt(r["completed_appraisals"]).ToString("N0");
    }

    private void BuildPager(int current, int totalPages)
    {
        if (totalPages <= 1) { litPager.Text = ""; return; }
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("<button type='button' onclick='goPage({0})'{1}>Previous</button>", current - 1, current > 1 ? "" : " disabled");
        for (int i = Math.Max(1, current - 2); i <= Math.Min(totalPages, current + 2); i++)
            sb.AppendFormat("<button type='button' onclick='goPage({0})'{1}>{0}</button>", i,
                i == current ? " disabled style='background:var(--hr-navy);color:#fff;border-color:var(--hr-navy);opacity:1;'" : "");
        sb.AppendFormat("<button type='button' onclick='goPage({0})'{1}>Next</button>", current + 1, current < totalPages ? "" : " disabled");
        litPager.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VOCABULARY AND HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private static string StatusWord(string s)
    {
        switch ((s ?? "").ToUpperInvariant())
        {
            case "PENDING":                return "Not started";
            case "EMPLOYEE_IN_PROGRESS":   return "In progress";
            case "EMPLOYEE_SUBMITTED":     return "Submitted";
            case "SUPERVISOR_IN_PROGRESS": return "With supervisor";
            case "RETURNED":               return "Returned";
            case "COMPLETED":              return "Awaiting HR";
            case "HR_REVIEWED":            return "HR reviewed";
            case "CANCELLED":              return "Cancelled";
            default:                       return s ?? "";
        }
    }

    private static string SessionBadge(string s)
    {
        string kind, word;
        switch (s)
        {
            case "ACTIVE":   kind = "ok"; word = "Active"; break;
            case "DRAFT":    kind = "neutral"; word = "Draft"; break;
            case "CLOSED":   kind = "info"; word = "Closed"; break;
            case "ARCHIVED": kind = "neutral"; word = "Archived"; break;
            default:         kind = "neutral"; word = HttpUtility.HtmlEncode(s); break;
        }
        return "<span class='hr-badge hr-badge--" + kind + "'>" + word + "</span>";
    }

    private static string CategoryWord(string c)
    {
        switch ((c ?? "").ToUpperInvariant())
        {
            case "ACADEMIC": return "Academic";
            case "ADMINISTRATIVE": return "Administrative";
            case "SUPPORT": return "Support";
            default: return c ?? "";
        }
    }

    private static string Progress(int total, int completed)
    {
        if (total == 0) return "<span class='hr-muted'>No appraisals</span>";
        double pct = Math.Round((double)completed / total * 100, 0);
        return string.Format(CultureInfo.InvariantCulture,
            "<div class='ps-progress'>{0} of {1} ({2}%)<span class='hr-bar'><span style='width:{2}%'></span></span></div>",
            completed.ToString("N0"), total.ToString("N0"), pct);
    }

    private static string Enc(string s) { return HttpUtility.HtmlEncode(s ?? ""); }

    private string BuildFilterUrl(string extra)
    {
        StringBuilder sb = new StringBuilder("AppraisalSessions.aspx?");
        if (!string.IsNullOrEmpty(QsSearch)) sb.AppendFormat("q={0}&", HttpUtility.UrlEncode(QsSearch));
        if (!string.IsNullOrEmpty(QsStatus)) sb.AppendFormat("status={0}&", HttpUtility.UrlEncode(QsStatus));
        if (!string.IsNullOrEmpty(extra)) sb.Append(extra);
        return sb.ToString().TrimEnd('&', '?');
    }

    private string SafeVal(object val) { return val == null || val == DBNull.Value ? "" : val.ToString(); }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private string FormatDateDisplay(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        return DateTime.TryParse(val.ToString(), out dt) ? dt.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : val.ToString();
    }

    /// <summary>Shows the message as a toast and reopens the dialog so the entered values are not lost.</summary>
    private void ShowInlineError(string message, string modalId)
    {
        string js = "toast('" + HttpUtility.JavaScriptStringEncode(message) + "', false);" +
                    (string.IsNullOrEmpty(modalId) ? "" : "openModal('" + modalId + "');");
        ScriptManager.RegisterStartupScript(this, GetType(), "err_" + DateTime.Now.Ticks, js, true);
    }

    private void RedirectWithFlash(string message, bool success)
    {
        Response.Redirect(BuildFilterUrl("msg=" + HttpUtility.UrlEncode(message) + "&ok=" + (success ? "1" : "0")), true);
    }

    private void ShowFlashMessage()
    {
        string msg = (Request.QueryString["msg"] ?? "").Trim();
        if (string.IsNullOrEmpty(msg)) return;
        bool ok = (Request.QueryString["ok"] ?? "") == "1";
        ScriptManager.RegisterStartupScript(this, GetType(), "flash_" + DateTime.Now.Ticks,
            "toast('" + HttpUtility.JavaScriptStringEncode(msg) + "', " + (ok ? "true" : "false") + ");", true);
    }

    private string EscapeJson(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        string s = val.ToString();
        StringBuilder sb = new StringBuilder(s.Length + 10);
        foreach (char c in s)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:X4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private string CurrentUsername() { return HrAccess.Username(); }

    private int? _actorEmpId;
    private int ActorEmpId()
    {
        if (_actorEmpId.HasValue) return _actorEmpId.Value;
        int v = 0;
        try
        {
            string u = CurrentUsername();
            if (!string.IsNullOrEmpty(u))
            {
                DataTable dt = ExecuteQuery("SELECT empID FROM hrm_employee WHERE usernames = @u LIMIT 1", new MySqlParameter("@u", u));
                if (dt.Rows.Count > 0) v = SafeInt(dt.Rows[0]["empID"]);
            }
        }
        catch { }
        _actorEmpId = v;
        return v;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DATA ACCESS
    // ═══════════════════════════════════════════════════════════════════
    private DataTable ExecuteQuery(string sql, params MySqlParameter[] parms)
    {
        DataTable dt = new DataTable();
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) { da.Fill(dt); }
            }
        }
        return dt;
    }

    private int ExecuteNonQuery(string sql, params MySqlParameter[] parms)
    {
        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlCommand cmd = new MySqlCommand(sql, conn))
            {
                if (parms != null) foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    private MySqlParameter[] CloneParams(List<MySqlParameter> parms)
    {
        MySqlParameter[] clone = new MySqlParameter[parms.Count];
        for (int i = 0; i < parms.Count; i++) clone[i] = new MySqlParameter(parms[i].ParameterName, parms[i].Value);
        return clone;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NOTIFICATIONS
    // ═══════════════════════════════════════════════════════════════════
    private static string PortalBaseUrl()
    {
        return System.Configuration.ConfigurationManager.AppSettings["PORTAL_BASE_URL"] ?? "https://eportal.mru.ac.ug";
    }

    private static string D(object v)
    {
        if (v == null || v == DBNull.Value) return "";
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "";
    }

    private const string EmailSubjectPrefix = "Performance appraisal: ";
    private const string EmailSender = "MRU Human Resource Office";

    private void AjaxSendNotifications()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request.\"}"); return; }

        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId) || sessionId <= 0)
        { Response.Write("{\"error\":\"Invalid session.\"}"); return; }
        bool resend = (Request.QueryString["resend"] ?? "0") == "1";

        DataTable dtSess = ExecuteQuery(
            "SELECT session_id, session_title, period_start, period_end, deadline, status FROM appraisal_sessions WHERE session_id = @id",
            new MySqlParameter("@id", sessionId));
        if (dtSess.Rows.Count == 0) { Response.Write("{\"error\":\"The session was not found.\"}"); return; }
        if (SafeVal(dtSess.Rows[0]["status"]) != "ACTIVE") { Response.Write("{\"error\":\"Notifications can be sent only for an active session.\"}"); return; }

        // Failed sends are always retried; resend=1 also sends again to SENT and NO_EMAIL records.
        ExecuteNonQuery(
            "UPDATE appraisal_records SET notify_email_status='PENDING', notify_email_sent_at=NULL, notify_email_error=NULL WHERE session_id=@sid AND notify_email_status='FAILED'",
            new MySqlParameter("@sid", sessionId));
        if (resend)
            ExecuteNonQuery(
                "UPDATE appraisal_records SET notify_email_status='PENDING', notify_email_sent_at=NULL, notify_email_error=NULL WHERE session_id=@sid AND notify_email_status IN ('SENT','NO_EMAIL')",
                new MySqlParameter("@sid", sessionId));

        int total = SafeInt(ExecuteQuery(
            "SELECT COUNT(*) AS cnt FROM appraisal_records WHERE session_id=@sid AND IFNULL(notify_email_status,'PENDING')='PENDING'",
            new MySqlParameter("@sid", sessionId)).Rows[0]["cnt"]);
        if (total == 0) { Response.Write("{\"started\":false,\"total\":0,\"message\":\"Every employee in this session has been notified.\"}"); return; }

        string connStr       = ConnStr;
        string portalBaseUrl = PortalBaseUrl();
        DataRow sessRow      = dtSess.Rows[0];
        string sessionTitle  = SafeVal(sessRow["session_title"]).Trim();
        string periodStart   = D(sessRow["period_start"]);
        string periodEnd     = D(sessRow["period_end"]);
        string deadline      = D(sessRow["deadline"]);

        System.Threading.Thread emailThread = new System.Threading.Thread(delegate()
        {
            try
            {
                DataTable dtRecs = new DataTable();
                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    using (MySqlCommand cmd = new MySqlCommand(
                        @"SELECT ar.record_id, e.emp_name, IFNULL(e.emp_email,'') AS emp_email, IFNULL(rev.emp_name,'') AS reviewer_name
                          FROM appraisal_records ar
                          INNER JOIN hrm_employee e   ON e.empID   = ar.employee_id
                          LEFT  JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
                          WHERE ar.session_id = @sid AND IFNULL(ar.notify_email_status,'PENDING') = 'PENDING'
                          ORDER BY e.emp_name", conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        using (MySqlDataAdapter da = new MySqlDataAdapter(cmd)) da.Fill(dtRecs);
                    }
                }

                foreach (DataRow r in dtRecs.Rows)
                {
                    int rid = r["record_id"] != DBNull.Value ? Convert.ToInt32(r["record_id"]) : 0;
                    string empName = r["emp_name"] != DBNull.Value ? r["emp_name"].ToString().Trim() : "";
                    string empEmail = r["emp_email"] != DBNull.Value ? r["emp_email"].ToString().Trim() : "";
                    string reviewerName = r["reviewer_name"] != DBNull.Value ? r["reviewer_name"].ToString().Trim() : "";

                    string newStatus, errMsg = "";
                    if (string.IsNullOrEmpty(empEmail) || !empEmail.Contains("@"))
                    {
                        newStatus = "NO_EMAIL";
                    }
                    else
                    {
                        string url = portalBaseUrl + "/SelfAppraisal.aspx?rid=" + rid;
                        string html = BuildEmployeeEmailHtml(empName, sessionTitle, periodStart, periodEnd, deadline, reviewerName, url);
                        string result = EmailSenderProtocol.SendHtmlEmail(html, empEmail, EmailSubjectPrefix + sessionTitle, EmailSender);
                        bool ok = result != null && result.StartsWith("Email sent");
                        newStatus = ok ? "SENT" : "FAILED";
                        errMsg = ok ? "" : (result != null && result.Length > 250 ? result.Substring(0, 250) : result ?? "");
                    }

                    using (MySqlConnection conn = new MySqlConnection(connStr))
                    {
                        conn.Open();
                        using (MySqlCommand cmd = new MySqlCommand(
                            "UPDATE appraisal_records SET notify_email_status=@st, notify_email_sent_at=" + (newStatus == "SENT" ? "NOW()" : "NULL") + ", notify_email_error=@err WHERE record_id=@rid", conn))
                        {
                            cmd.Parameters.AddWithValue("@st", newStatus);
                            cmd.Parameters.AddWithValue("@err", string.IsNullOrEmpty(errMsg) ? (object)DBNull.Value : (object)errMsg);
                            cmd.Parameters.AddWithValue("@rid", rid);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { /* background thread, isolated */ }
        });
        emailThread.IsBackground = true;
        emailThread.Start();

        Response.Write(string.Format("{{\"started\":true,\"total\":{0}}}", total));
    }

    private void AjaxGetNotifyStatus()
    {
        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId) || sessionId <= 0)
        { Response.Write("{\"error\":\"Invalid session.\"}"); return; }

        DataTable dt = ExecuteQuery(
            @"SELECT ar.record_id, IFNULL(e.emp_name,'') AS emp_name, IFNULL(e.emp_email,'') AS emp_email,
                     IFNULL(ar.notify_email_status,'PENDING') AS notify_status
              FROM appraisal_records ar
              LEFT JOIN hrm_employee e ON e.empID = ar.employee_id
              WHERE ar.session_id = @sid
              ORDER BY FIELD(IFNULL(ar.notify_email_status,'PENDING'),'FAILED','NO_EMAIL','SENT','PENDING'), e.emp_name",
            new MySqlParameter("@sid", sessionId));

        int total = dt.Rows.Count, sent = 0, failed = 0, noEmail = 0, pending = 0;
        StringBuilder records = new StringBuilder("[");
        bool first = true;
        foreach (DataRow r in dt.Rows)
        {
            string ns = SafeVal(r["notify_status"]);
            switch (ns) { case "SENT": sent++; break; case "FAILED": failed++; break; case "NO_EMAIL": noEmail++; break; default: pending++; break; }
            if (!first) records.Append(",");
            first = false;
            records.AppendFormat("{{\"record_id\":{0},\"emp_name\":\"{1}\",\"emp_email\":\"{2}\",\"notify_status\":\"{3}\"}}",
                SafeInt(r["record_id"]), EscapeJson(r["emp_name"]), EscapeJson(r["emp_email"]), EscapeJson(ns));
        }
        records.Append("]");
        Response.Write(string.Format("{{\"total\":{0},\"sent\":{1},\"failed\":{2},\"no_email\":{3},\"pending\":{4},\"done\":{5},\"records\":{6}}}",
            total, sent, failed, noEmail, pending, pending == 0 ? "true" : "false", records));
    }

    private void AjaxSendSingleNotify()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request.\"}"); return; }

        int recordId;
        if (!int.TryParse(Request.QueryString["id"], out recordId) || recordId <= 0)
        { Response.Write("{\"error\":\"Invalid appraisal.\"}"); return; }

        DataTable dt = ExecuteQuery(
            @"SELECT ar.record_id, e.emp_name, IFNULL(e.emp_email,'') AS emp_email, IFNULL(rev.emp_name,'') AS reviewer_name,
                     s.session_title, s.period_start, s.period_end, s.deadline
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              INNER JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE ar.record_id = @rid LIMIT 1",
            new MySqlParameter("@rid", recordId));
        if (dt.Rows.Count == 0) { Response.Write("{\"error\":\"The appraisal was not found.\"}"); return; }
        DataRow r = dt.Rows[0];

        string empName = SafeVal(r["emp_name"]).Trim();
        string empEmail = SafeVal(r["emp_email"]).Trim();
        string sessionTitle = SafeVal(r["session_title"]).Trim();

        if (string.IsNullOrEmpty(empEmail) || !empEmail.Contains("@"))
        {
            ExecuteNonQuery("UPDATE appraisal_records SET notify_email_status='NO_EMAIL', notify_email_sent_at=NULL, notify_email_error=NULL WHERE record_id=@rid",
                new MySqlParameter("@rid", recordId));
            Response.Write("{\"ok\":false,\"status\":\"NO_EMAIL\",\"message\":\"There is no email address on the staff record.\"}");
            return;
        }

        string html = BuildEmployeeEmailHtml(empName, sessionTitle, D(r["period_start"]), D(r["period_end"]), D(r["deadline"]),
            SafeVal(r["reviewer_name"]).Trim(), PortalBaseUrl() + "/SelfAppraisal.aspx?rid=" + recordId);
        string result = EmailSenderProtocol.SendHtmlEmail(html, empEmail, EmailSubjectPrefix + sessionTitle, EmailSender);
        bool ok = result != null && result.StartsWith("Email sent");
        string errMsg = ok ? "" : (result != null && result.Length > 250 ? result.Substring(0, 250) : result ?? "");

        ExecuteNonQuery(
            "UPDATE appraisal_records SET notify_email_status=@st, notify_email_sent_at=" + (ok ? "NOW()" : "NULL") + ", notify_email_error=@err WHERE record_id=@rid",
            new MySqlParameter("@st", ok ? "SENT" : "FAILED"),
            new MySqlParameter("@err", string.IsNullOrEmpty(errMsg) ? (object)DBNull.Value : (object)errMsg),
            new MySqlParameter("@rid", recordId));

        Response.Write(ok
            ? "{\"ok\":true,\"status\":\"SENT\",\"message\":\"Notification sent to " + EscapeJson(empEmail) + ".\"}"
            : "{\"ok\":false,\"status\":\"FAILED\",\"message\":\"The email could not be delivered. Check the address on the staff record.\"}");
    }

    /// <summary>Plain official notice: navy bar, greeting, short paragraphs, one navy button, sign-off.</summary>
    private static string BuildEmployeeEmailHtml(string empName, string sessionTitle, string periodStart, string periodEnd,
                                                 string deadline, string supervisorName, string appraisalUrl)
    {
        string enc = HttpUtility.HtmlEncode(empName);
        string session = HttpUtility.HtmlEncode(sessionTitle);
        string href = HttpUtility.HtmlAttributeEncode(appraisalUrl);
        const string p = "margin:0 0 14px;font-size:14px;line-height:1.6;color:#1a1a2e;";

        StringBuilder b = new StringBuilder();
        b.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>");
        b.Append("<body style=\"margin:0;padding:0;background:#f5f7fa;font-family:Arial,Helvetica,sans-serif;\">");
        b.Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#f5f7fa;padding:24px 12px;\"><tr><td align=\"center\">");
        b.Append("<table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:600px;width:100%;background:#ffffff;border:1px solid #e0e5ed;\">");
        b.Append("<tr><td style=\"background:#05275C;padding:16px 28px;color:#ffffff;font-size:16px;font-weight:bold;\">Muteesa I Royal University</td></tr>");
        b.Append("<tr><td style=\"padding:28px;\">");
        b.AppendFormat("<p style=\"{0}\">Dear {1},</p>", p, enc);
        b.AppendFormat("<p style=\"{0}\">The performance appraisal for <strong>{1}</strong> is now open. Please complete your self-appraisal on the staff portal{2}.</p>",
            p, session, string.IsNullOrEmpty(deadline) ? "" : " by " + HttpUtility.HtmlEncode(deadline));
        StringBuilder facts = new StringBuilder();
        if (!string.IsNullOrEmpty(periodStart))
            facts.Append("Appraisal period: " + HttpUtility.HtmlEncode(periodStart) + (string.IsNullOrEmpty(periodEnd) ? "" : " to " + HttpUtility.HtmlEncode(periodEnd)) + "<br/>");
        if (!string.IsNullOrEmpty(supervisorName))
            facts.Append("Supervisor: " + HttpUtility.HtmlEncode(supervisorName) + "<br/>");
        if (facts.Length > 0) b.AppendFormat("<p style=\"{0}\">{1}</p>", p, facts.ToString());
        b.AppendFormat("<p style=\"margin:22px 0;\"><a href=\"{0}\" target=\"_blank\" style=\"display:inline-block;background:#05275C;color:#ffffff;font-size:14px;font-weight:bold;text-decoration:none;padding:12px 28px;\">Open my appraisal</a></p>", href);
        b.AppendFormat("<p style=\"margin:0 0 20px;font-size:12px;line-height:1.5;color:#555555;\">If the button does not open, copy this address into your browser: {0}</p>", HttpUtility.HtmlEncode(appraisalUrl));
        b.AppendFormat("<p style=\"{0}margin-bottom:0;\">Human Resource Office<br/>Muteesa I Royal University</p>", p);
        b.Append("</td></tr></table></td></tr></table></body></html>");
        return b.ToString();
    }
}
