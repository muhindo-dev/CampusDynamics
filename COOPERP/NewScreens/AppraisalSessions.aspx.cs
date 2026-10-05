using System;
using System.Collections.Generic;
using System.Data;
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
                AddColumnIfMissing(conn, "appraisal_records", "support_declaration", "VARCHAR(10) DEFAULT NULL COMMENT 'AGREE or DISAGREE — Support Staff declaration'");
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

        // ── ACADEMIC STAFF — 45 explicit criteria (form states 50; 5 unconfirmed with HR)
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

        // ── ADMINISTRATIVE STAFF — 22 criteria (form states 30; 8 unconfirmed with HR)
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

        // ── SUPPORT STAFF — 25 explicit criteria (form states 30; 5 unconfirmed with HR)
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
        EnsureDbSchema();

        string ajax = Request.QueryString["ajax"];
        if (!string.IsNullOrEmpty(ajax))
        {
            // ?ajax= runs BEFORE SidebarMaster's login check - gate it here.
            if (!IsCallerAuthenticated())
            {
                Response.Clear();
                Response.ContentType = "application/json";
                Response.Write("{\"error\":\"Your session has expired. Please sign in again, then retry.\"}");
                try { Response.End(); } catch (System.Threading.ThreadAbortException) { }
                return;
            }
            HandleAjax(ajax);
            return;
        }

        if (!IsPostBack)
        {
            BindGrid();
            LoadStats();
            ShowFlashMessage();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX ROUTER
    // ═══════════════════════════════════════════════════════════════════
    private void HandleAjax(string action)
    {
        Response.ContentType = "application/json";
        try
        {
            if (action == "generate_appraisals" || action == "backfill_active" ||
                action == "delete_session"       || action == "delete_record"   ||
                action == "send_notifications"   || action == "send_single_notify")
            {
                if (!MarksAntiForgeryService.ValidateRequest())
                {
                    Response.Write("{\"error\":\"Security validation failed. Please refresh and try again.\"}");
                    return;
                }
                if (!HasHrAppraisalAccess())
                {
                    Response.Write("{\"error\":\"Access denied. HR or administrator access is required.\"}");
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
                    Response.Write("{\"error\":\"Unknown action\"}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Response.Write("{\"error\":\"" + EscapeJson(ex.Message) + "\"}");
        }
        Response.End();
    }

    // ─── AJAX: Get Session for Edit ────────────────────────────────────
    private void AjaxGetSession()
    {
        int id;
        if (!int.TryParse(Request.QueryString["id"], out id))
        { Response.Write("{\"error\":\"Invalid ID\"}"); return; }

        DataTable dt = ExecuteQuery(
            @"SELECT s.*, IFNULL(cr.emp_name,'System') AS created_by_name
              FROM appraisal_sessions s
              LEFT JOIN hrm_employee cr ON cr.empID = s.created_by
              WHERE s.session_id = @id",
            new MySqlParameter("@id", id));

        if (dt.Rows.Count == 0) { Response.Write("{\"error\":\"Not found\"}"); return; }

        DataRow r = dt.Rows[0];
        StringBuilder sb = new StringBuilder("{");
        bool first = true;
        foreach (DataColumn col in dt.Columns)
        {
            if (!first) sb.Append(",");
            first = false;
            string val = "";
            if (r[col] != null && r[col] != DBNull.Value)
            {
                if (col.DataType == typeof(DateTime))
                    val = ((DateTime)r[col]).ToString("yyyy-MM-dd");
                else
                    val = r[col].ToString();
            }
            sb.AppendFormat("\"{0}\":\"{1}\"", col.ColumnName, EscapeJson(val));
        }
        sb.Append("}");
        Response.Write(sb.ToString());
    }

    // ─── AJAX: Get Session Detail (with appraisal stats) ──────────────
    private void AjaxGetSessionDetail()
    {
        int id;
        if (!int.TryParse(Request.QueryString["id"], out id))
        { Response.Write("{\"error\":\"Invalid ID\"}"); return; }

        // Session info
        DataTable dtS = ExecuteQuery(
            @"SELECT s.*, IFNULL(cr.emp_name,'System') AS created_by_name
              FROM appraisal_sessions s
              LEFT JOIN hrm_employee cr ON cr.empID = s.created_by
              WHERE s.session_id = @id",
            new MySqlParameter("@id", id));

        if (dtS.Rows.Count == 0) { Response.Write("{\"error\":\"Not found\"}"); return; }
        DataRow sess = dtS.Rows[0];

        // Appraisal stats — valid-contract employees only
        DataTable dtStats = ExecuteQuery(
            @"SELECT
                COUNT(*) AS total,
                COALESCE(SUM(CASE WHEN ar.status = 'PENDING' THEN 1 ELSE 0 END),0) AS pending,
                COALESCE(SUM(CASE WHEN ar.status IN ('EMPLOYEE_IN_PROGRESS','RETURNED','EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS') THEN 1 ELSE 0 END),0) AS in_progress,
                COALESCE(SUM(CASE WHEN ar.status IN ('COMPLETED','HR_REVIEWED') THEN 1 ELSE 0 END),0) AS completed,
                COALESCE(SUM(CASE WHEN ar.status = 'CANCELLED' THEN 1 ELSE 0 END),0) AS cancelled
              FROM appraisal_records ar
              WHERE ar.session_id = @id
                AND EXISTS (SELECT 1 FROM hrm_emp_contracts vc
                            WHERE vc.empID = ar.employee_id AND vc.contractStatus = 'VALID')",
            new MySqlParameter("@id", id));

        DataRow st = dtStats.Rows[0];
        int total = SafeInt(st["total"]);
        int completed = SafeInt(st["completed"]);

        // Individual records
        DataTable dtRecs = ExecuteQuery(
            @"SELECT ar.record_id, ar.status, ar.staff_category,
                     ar.final_percentage, ar.classification,
                     IFNULL(ar.notify_email_status,'PENDING') AS notify_email_status,
                     IFNULL(ar.notify_email_error,'')         AS notify_email_error,
                     e.emp_name, e.EMP_CODE,
                     IFNULL(rev.emp_name,'Unassigned') AS reviewer_name
              FROM appraisal_records ar
              INNER JOIN hrm_employee e ON e.empID = ar.employee_id
              LEFT JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              WHERE ar.session_id = @id
              ORDER BY e.emp_name",
            new MySqlParameter("@id", id));

        // Build JSON
        StringBuilder sb = new StringBuilder("{");
        sb.AppendFormat("\"session_id\":\"{0}\",", EscapeJson(sess["session_id"]));
        sb.AppendFormat("\"session_title\":\"{0}\",", EscapeJson(sess["session_title"]));
        sb.AppendFormat("\"status\":\"{0}\",", EscapeJson(sess["status"]));
        sb.AppendFormat("\"period_start\":\"{0}\",", FormatDate(sess["period_start"]));
        sb.AppendFormat("\"period_end\":\"{0}\",", FormatDate(sess["period_end"]));
        sb.AppendFormat("\"deadline\":\"{0}\",", FormatDate(sess["deadline"]));
        sb.AppendFormat("\"target_categories\":\"{0}\",", EscapeJson(sess["target_categories"]));
        sb.AppendFormat("\"created_by_name\":\"{0}\",", EscapeJson(sess["created_by_name"]));
        sb.AppendFormat("\"total\":{0},", total);
        sb.AppendFormat("\"pending\":{0},", SafeInt(st["pending"]));
        sb.AppendFormat("\"in_progress\":{0},", SafeInt(st["in_progress"]));
        sb.AppendFormat("\"completed\":{0},", completed);
        sb.AppendFormat("\"cancelled\":{0},", SafeInt(st["cancelled"]));
        sb.AppendFormat("\"progress_pct\":{0},", total > 0 ? Math.Round((double)completed / total * 100, 1) : 0);

        // Records array
        sb.Append("\"records\":[");
        bool first = true;
        foreach (DataRow r in dtRecs.Rows)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.Append("{");
            sb.AppendFormat("\"record_id\":\"{0}\",", EscapeJson(r["record_id"]));
            sb.AppendFormat("\"emp_name\":\"{0}\",", EscapeJson(r["emp_name"]));
            sb.AppendFormat("\"EMP_CODE\":\"{0}\",", EscapeJson(r["EMP_CODE"]));
            sb.AppendFormat("\"staff_category\":\"{0}\",", EscapeJson(r["staff_category"]));
            sb.AppendFormat("\"reviewer_name\":\"{0}\",", EscapeJson(r["reviewer_name"]));
            sb.AppendFormat("\"status\":\"{0}\",", EscapeJson(r["status"]));
            string pct = (r["final_percentage"] != null && r["final_percentage"] != DBNull.Value)
                ? Convert.ToDecimal(r["final_percentage"]).ToString("F1") : "";
            sb.AppendFormat("\"final_percentage\":\"{0}\",", pct);
            string cls = (r["classification"] != null && r["classification"] != DBNull.Value)
                ? r["classification"].ToString() : "";
            sb.AppendFormat("\"classification\":\"{0}\",", EscapeJson(cls));
            string notifyStatus = (r["notify_email_status"] != null && r["notify_email_status"] != DBNull.Value) ? r["notify_email_status"].ToString() : "PENDING";
            string notifyError  = (r["notify_email_error"]  != null && r["notify_email_error"]  != DBNull.Value) ? r["notify_email_error"].ToString()  : "";
            sb.AppendFormat("\"notify_status\":\"{0}\",", EscapeJson(notifyStatus));
            sb.AppendFormat("\"notify_error\":\"{0}\"",   EscapeJson(notifyError));
            sb.Append("}");
        }
        sb.Append("]}");
        Response.Write(sb.ToString());
    }

    // ─── AJAX: Generate Appraisals for a Session ──────────────────────
    private void AjaxGenerateAppraisals()
    {
        Response.ContentType = "application/json";
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            Response.Write("{\"error\":\"Invalid request method\"}");
            return;
        }
        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId))
        { Response.Write("{\"error\":\"Invalid session ID\"}"); return; }

        // Get session info
        DataTable dtSess = ExecuteQuery(
            "SELECT target_categories, status FROM appraisal_sessions WHERE session_id = @id",
            new MySqlParameter("@id", sessionId));

        if (dtSess.Rows.Count == 0) { Response.Write("{\"error\":\"Session not found\"}"); return; }

        string sessStatus = dtSess.Rows[0]["status"].ToString();
        if (sessStatus != "ACTIVE")
        { Response.Write("{\"error\":\"Session must be ACTIVE to generate appraisals\"}"); return; }

        string cats = dtSess.Rows[0]["target_categories"].ToString();
        int count = GenerateMissingAppraisals(sessionId, cats);
        Response.Write(string.Format("{{\"success\":true,\"count\":{0}}}", count));
    }

    private void AjaxBackfillActiveSession()
    {
        Response.ContentType = "application/json";
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            Response.Write("{\"error\":\"Invalid request method\"}");
            return;
        }

        DataTable dtActive = ExecuteQuery(
            @"SELECT session_id, session_title, target_categories
              FROM appraisal_sessions
              WHERE status = 'ACTIVE'
              ORDER BY created_at DESC
              LIMIT 1");

        if (dtActive.Rows.Count == 0)
        {
            Response.Write("{\"error\":\"No ACTIVE session found.\"}");
            return;
        }

        DataRow active = dtActive.Rows[0];
        int sessionId = SafeInt(active["session_id"]);
        string sessionTitle = SafeVal(active["session_title"]);
        string categories = SafeVal(active["target_categories"]);

        int count = GenerateMissingAppraisals(sessionId, categories);
        Response.Write(string.Format("{{\"success\":true,\"session_id\":{0},\"session_title\":\"{1}\",\"count\":{2}}}",
            sessionId,
            EscapeJson(sessionTitle),
            count));
    }

    // ─── AJAX: Delete Session (cascade) ───────────────────────────────
    private void AjaxDeleteSession()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request method\"}"); return; }

        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId) || sessionId <= 0)
        { Response.Write("{\"error\":\"Invalid session ID\"}"); return; }

        DataTable dtCheck = ExecuteQuery(
            "SELECT COUNT(*) AS cnt FROM appraisal_sessions WHERE session_id = @id",
            new MySqlParameter("@id", sessionId));
        if (SafeInt(dtCheck.Rows[0]["cnt"]) == 0)
        { Response.Write("{\"error\":\"Session not found\"}"); return; }

        // Gather record IDs before opening transaction
        DataTable dtRecs = ExecuteQuery(
            "SELECT record_id FROM appraisal_records WHERE session_id = @id",
            new MySqlParameter("@id", sessionId));

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
                    // The audit trail is kept (it is the record of what happened); one
                    // RECORD_DELETED row per record is added below.
                    foreach (string tbl in new[] { "appraisal_section_b", "appraisal_section_c",
                                                   "appraisal_section_d", "appraisal_section_e" })
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
                    using (MySqlCommand cmd = new MySqlCommand(
                        "DELETE FROM appraisal_records WHERE session_id = @sid", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        cmd.ExecuteNonQuery();
                    }
                }
                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM appraisal_sessions WHERE session_id = @sid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@sid", sessionId);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }
        Response.Write(string.Format("{{\"success\":true,\"deleted_records\":{0}}}", recordIds.Count));
    }

    // ─── AJAX: Delete Individual Appraisal Record (cascade) ───────────
    private void AjaxDeleteRecord()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request method\"}"); return; }

        int recordId;
        if (!int.TryParse(Request.QueryString["id"], out recordId) || recordId <= 0)
        { Response.Write("{\"error\":\"Invalid record ID\"}"); return; }

        DataTable dtCheck = ExecuteQuery(
            "SELECT COUNT(*) AS cnt FROM appraisal_records WHERE record_id = @id",
            new MySqlParameter("@id", recordId));
        if (SafeInt(dtCheck.Rows[0]["cnt"]) == 0)
        { Response.Write("{\"error\":\"Record not found\"}"); return; }

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                // Audit first (kept - the trail outlives the record).
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
                foreach (string tbl in new[] { "appraisal_section_b", "appraisal_section_c",
                                               "appraisal_section_d", "appraisal_section_e" })
                {
                    using (MySqlCommand cmd = new MySqlCommand(
                        string.Format("DELETE FROM `{0}` WHERE record_id = @rid", tbl), conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@rid", recordId);
                        cmd.ExecuteNonQuery();
                    }
                }
                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM appraisal_records WHERE record_id = @rid", conn, tx))
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
    //  FORM HANDLERS (POST-REDIRECT-GET)
    // ═══════════════════════════════════════════════════════════════════
    protected void btnCreateSession_Click(object sender, EventArgs e)
    {
        if (!HasHrAppraisalAccess()) { RedirectWithFlash("Access denied. HR or administrator access is required.", false); return; }
        string title   = txtTitle.Text.Trim();
        string desc    = txtDescription.Text.Trim();
        string pStart  = txtPeriodStart.Text.Trim();
        string pEnd    = txtPeriodEnd.Text.Trim();
        string deadline = txtDeadline.Text.Trim();

        // Collect target categories
        List<string> cats = new List<string>();
        if (chkAcademic.Checked) cats.Add("ACADEMIC");
        if (chkAdministrative.Checked) cats.Add("ADMINISTRATIVE");
        if (chkSupport.Checked) cats.Add("SUPPORT");
        string targetCats = string.Join(",", cats);

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(pStart) || string.IsNullOrEmpty(pEnd) || string.IsNullOrEmpty(deadline))
        {
            ShowInlineError("Please fill in all required fields.");
            return;
        }
        if (cats.Count == 0)
        {
            ShowInlineError("Please select at least one target category.");
            return;
        }
        DateTime dStart, dEnd, dDeadline;
        if (!DateTime.TryParse(pStart, out dStart) || !DateTime.TryParse(pEnd, out dEnd) || !DateTime.TryParse(deadline, out dDeadline))
        {
            ShowInlineError("Please provide valid dates.");
            return;
        }
        if (dStart > dEnd)
        {
            ShowInlineError("Period start date cannot be after period end date.");
            return;
        }
        if (dDeadline < dStart || dDeadline > dEnd)
        {
            ShowInlineError("Deadline must fall within the appraisal period.");
            return;
        }

        ExecuteNonQuery(
            @"INSERT INTO appraisal_sessions (session_title, session_description, period_start, period_end, deadline, target_categories, status, created_by)
              VALUES (@title, @desc, @ps, @pe, @dl, @cats, 'DRAFT', @cb)",
            new MySqlParameter("@title", title),
            new MySqlParameter("@desc", desc),
            new MySqlParameter("@ps", pStart),
            new MySqlParameter("@pe", pEnd),
            new MySqlParameter("@dl", deadline),
            new MySqlParameter("@cats", targetCats),
            new MySqlParameter("@cb", DBNull.Value));

        RedirectWithFlash("Appraisal session created successfully.", true);
    }

    protected void btnEditSession_Click(object sender, EventArgs e)
    {
        if (!HasHrAppraisalAccess()) { RedirectWithFlash("Access denied. HR or administrator access is required.", false); return; }
        int id;
        if (!int.TryParse(hfEditSessionId.Value, out id) || id <= 0)
        {
            ShowInlineError("Invalid session ID.");
            return;
        }

        string title    = txtEditTitle.Text.Trim();
        string desc     = txtEditDescription.Text.Trim();
        string pStart   = txtEditPeriodStart.Text.Trim();
        string pEnd     = txtEditPeriodEnd.Text.Trim();
        string deadline = txtEditDeadline.Text.Trim();
        string newStatus = ddlEditStatus.SelectedValue.ToUpper();

        // Validate status value
        if (newStatus != "DRAFT" && newStatus != "ACTIVE" && newStatus != "CLOSED" && newStatus != "ARCHIVED")
            newStatus = "DRAFT";

        List<string> cats = new List<string>();
        if (chkEditAcademic.Checked) cats.Add("ACADEMIC");
        if (chkEditAdministrative.Checked) cats.Add("ADMINISTRATIVE");
        if (chkEditSupport.Checked) cats.Add("SUPPORT");
        string targetCats = string.Join(",", cats);

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(pStart) || string.IsNullOrEmpty(pEnd) || string.IsNullOrEmpty(deadline))
        {
            ShowInlineError("Please fill in all required fields.");
            return;
        }
        if (cats.Count == 0)
        {
            ShowInlineError("Please select at least one target category.");
            return;
        }
        DateTime dStart, dEnd, dDeadline;
        if (!DateTime.TryParse(pStart, out dStart) || !DateTime.TryParse(pEnd, out dEnd) || !DateTime.TryParse(deadline, out dDeadline))
        {
            ShowInlineError("Please provide valid dates.");
            return;
        }
        if (dStart > dEnd)
        {
            ShowInlineError("Period start date cannot be after period end date.");
            return;
        }
        if (dDeadline < dStart || dDeadline > dEnd)
        {
            ShowInlineError("Deadline must fall within the appraisal period.");
            return;
        }

        // If activating, ensure no other ACTIVE session exists
        if (newStatus == "ACTIVE")
        {
            DataTable dtOther = ExecuteQuery(
                "SELECT COUNT(*) AS cnt FROM appraisal_sessions WHERE status = 'ACTIVE' AND session_id <> @id",
                new MySqlParameter("@id", id));
            if (SafeInt(dtOther.Rows[0]["cnt"]) > 0)
            {
                ShowInlineError("Another ACTIVE session already exists. Close it before activating this one.");
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

        // Auto-generate missing appraisals when activating
        if (newStatus == "ACTIVE")
            GenerateMissingAppraisals(id, targetCats);

        RedirectWithFlash("Session updated successfully.", true);
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

        string catWhere = includesAllTargets
            ? "1=1"
            : "(" + string.Join(" OR ", catConditions) + ")";
        string sql = string.Format(
            @"SELECT e.empID, e.EmpType
              FROM hrm_employee e
              WHERE IFNULL(e.to_be_appraised, 1) = 1
                AND IFNULL(e.employment_status, 'ACTIVE') IN ('ACTIVE','PROBATION')
                AND {0}
              ORDER BY e.emp_name", catWhere);

        DataTable dtEmps = ExecuteQuery(sql);
        int count = 0;

        using (MySqlConnection conn = new MySqlConnection(ConnStr))
        {
            conn.Open();
            using (MySqlTransaction tx = conn.BeginTransaction())
            {
                foreach (DataRow emp in dtEmps.Rows)
                {
                    string empType = SafeVal(emp["EmpType"]);
                    string staffCat = ResolveStaffCategoryFromEmpType(empType);

                    // Reviewer and expected-standards group come from the shared DB rules
                    // (reviewer_id -> supervisorID -> dept head -> contract dept head, never
                    // self; NULL = HR must assign) - the old IFNULL(reviewer_id, supervisorID)
                    // stopped at a stored 0 and never reached the department head.
                    using (MySqlCommand cmd = new MySqlCommand(
                        @"INSERT INTO appraisal_records (session_id, employee_id, reviewer_id, staff_category, status, standards_group_id)
                          SELECT @sid, @eid, appraisal_resolve_reviewer(@eid), @scat, 'PENDING', appraisal_resolve_group(@eid, @scat)
                          FROM DUAL
                          WHERE NOT EXISTS (
                              SELECT 1 FROM appraisal_records
                              WHERE session_id = @sid AND employee_id = @eid
                          )", conn, tx))
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
        if (!HasHrAppraisalAccess()) { RedirectWithFlash("Access denied. HR or administrator access is required.", false); return; }
        int id;
        if (!int.TryParse(hfActionSessionId.Value, out id) || id <= 0) return;

        DataTable dtActive = ExecuteQuery(
            "SELECT COUNT(*) AS cnt FROM appraisal_sessions WHERE status = 'ACTIVE' AND session_id <> @id",
            new MySqlParameter("@id", id));
        if (SafeInt(dtActive.Rows[0]["cnt"]) > 0)
        {
            RedirectWithFlash("Another ACTIVE appraisal session already exists. Close it first.", false);
            return;
        }

        int changed = ExecuteNonQuery("UPDATE appraisal_sessions SET status = 'ACTIVE' WHERE session_id = @id AND status = 'DRAFT'",
            new MySqlParameter("@id", id));
        if (changed == 0)
        {
            RedirectWithFlash("Only a DRAFT session can be activated.", false);
            return;
        }

        // Same as Edit -> ACTIVE: create the missing appraisal records straight away.
        DataTable dtCats = ExecuteQuery("SELECT target_categories FROM appraisal_sessions WHERE session_id = @id",
            new MySqlParameter("@id", id));
        string cats = dtCats.Rows.Count > 0 ? SafeVal(dtCats.Rows[0]["target_categories"]) : "";
        int generated = GenerateMissingAppraisals(id, cats);
        RedirectWithFlash(string.Format("Session activated. {0} appraisal record(s) generated.", generated), true);
    }

    protected void btnCloseSession_Click(object sender, EventArgs e)
    {
        if (!HasHrAppraisalAccess()) { RedirectWithFlash("Access denied. HR or administrator access is required.", false); return; }
        int id;
        if (!int.TryParse(hfActionSessionId.Value, out id) || id <= 0) return;

        ExecuteNonQuery("UPDATE appraisal_sessions SET status = 'CLOSED' WHERE session_id = @id AND status = 'ACTIVE'",
            new MySqlParameter("@id", id));
        RedirectWithFlash("Session closed.", true);
    }

    protected void btnArchiveSession_Click(object sender, EventArgs e)
    {
        if (!HasHrAppraisalAccess()) { RedirectWithFlash("Access denied. HR or administrator access is required.", false); return; }
        int id;
        if (!int.TryParse(hfActionSessionId.Value, out id) || id <= 0) return;

        ExecuteNonQuery("UPDATE appraisal_sessions SET status = 'ARCHIVED' WHERE session_id = @id AND status = 'CLOSED'",
            new MySqlParameter("@id", id));
        RedirectWithFlash("Session archived.", true);
    }

    protected void btnDeleteSession_Click(object sender, EventArgs e)
    {
        if (!HasHrAppraisalAccess()) { RedirectWithFlash("Access denied. HR or administrator access is required.", false); return; }
        int id;
        if (!int.TryParse(hfActionSessionId.Value, out id) || id <= 0) return;

        DataTable dtCheck = ExecuteQuery(
            "SELECT COUNT(*) AS cnt FROM appraisal_sessions WHERE session_id = @id",
            new MySqlParameter("@id", id));
        if (SafeInt(dtCheck.Rows[0]["cnt"]) == 0)
        {
            RedirectWithFlash("Session not found.", false);
            return;
        }

        // Cascade delete — collect record IDs first
        DataTable dtRecs = ExecuteQuery(
            "SELECT record_id FROM appraisal_records WHERE session_id = @id",
            new MySqlParameter("@id", id));

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
                    // The audit trail is kept (it is the record of what happened); one
                    // RECORD_DELETED row per record is added below.
                    foreach (string tbl in new[] { "appraisal_section_b", "appraisal_section_c",
                                                   "appraisal_section_d", "appraisal_section_e" })
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
                    using (MySqlCommand cmd = new MySqlCommand(
                        "DELETE FROM appraisal_records WHERE session_id = @sid", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@sid", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                using (MySqlCommand cmd = new MySqlCommand(
                    "DELETE FROM appraisal_sessions WHERE session_id = @sid", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@sid", id);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }

        string msg = recordIds.Count > 0
            ? string.Format("Session deleted along with {0} appraisal record(s).", recordIds.Count)
            : "Session deleted.";
        RedirectWithFlash(msg, true);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GRID BINDING
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

        string countSql = "SELECT COUNT(*) FROM appraisal_sessions s " + where.ToString();
        DataTable dtCount = ExecuteQuery(countSql, parms.ToArray());
        int totalRecords = SafeInt(dtCount.Rows[0][0]);

        int pageSize = 20;
        int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
        if (totalPages < 1) totalPages = 1;
        int currentPage = QsPage;
        if (currentPage > totalPages) currentPage = totalPages;
        int offset = (currentPage - 1) * pageSize;

        string dataSql = string.Format(
            @"SELECT s.*,
                     IFNULL(cr.emp_name, 'System') AS created_by_name,
                     (SELECT COUNT(*) FROM appraisal_records ar WHERE ar.session_id = s.session_id
                      AND EXISTS (SELECT 1 FROM hrm_emp_contracts vc WHERE vc.empID = ar.employee_id AND vc.contractStatus = 'VALID')) AS total_records,
                     (SELECT COUNT(*) FROM appraisal_records ar WHERE ar.session_id = s.session_id
                      AND ar.status IN ('COMPLETED','HR_REVIEWED')
                      AND EXISTS (SELECT 1 FROM hrm_emp_contracts vc WHERE vc.empID = ar.employee_id AND vc.contractStatus = 'VALID')) AS completed_records
              FROM appraisal_sessions s
              LEFT JOIN hrm_employee cr ON cr.empID = s.created_by
              {0}
              ORDER BY s.created_at DESC
              LIMIT {1} OFFSET {2}",
            where.ToString(), pageSize, offset);

        DataTable dtData = ExecuteQuery(dataSql, CloneParams(parms));

        // Render rows server-side
        StringBuilder html = new StringBuilder();
        if (dtData.Rows.Count == 0)
        {
            html.Append("<tr><td colspan='8' class='pa-empty-state'>");
            html.Append("<svg xmlns='http://www.w3.org/2000/svg' width='40' height='40' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.5'><rect x='3' y='4' width='18' height='18' rx='2' ry='2'/><line x1='16' y1='2' x2='16' y2='6'/><line x1='8' y1='2' x2='8' y2='6'/><line x1='3' y1='10' x2='21' y2='10'/></svg>");
            html.Append("<p>No appraisal sessions found.</p></td></tr>");
        }
        else
        {
            int rowNum = 0;
            foreach (DataRow r in dtData.Rows)
            {
                rowNum++;
                int sessId = SafeInt(r["session_id"]);
                string title = SafeVal(r["session_title"]);
                string createdBy = SafeVal(r["created_by_name"]);
                string status = SafeVal(r["status"]);
                int totalRecs = SafeInt(r["total_records"]);
                int completedRecs = SafeInt(r["completed_records"]);

                html.AppendFormat("<tr onclick='viewSession({0})' title='Click to view details'>", sessId);
                html.AppendFormat("<td class='pa-col-num'>{0}</td>", offset + rowNum);
                html.AppendFormat("<td><strong>{0}</strong><br/><span style='font-size:11px;color:#999;'>by {1}</span></td>",
                    HttpUtility.HtmlEncode(title), HttpUtility.HtmlEncode(createdBy));
                html.AppendFormat("<td style='white-space:nowrap;font-size:12px;'>{0} &mdash; {1}</td>",
                    FormatDateDisplay(r["period_start"]), FormatDateDisplay(r["period_end"]));
                html.AppendFormat("<td style='white-space:nowrap;font-size:12px;'>{0}</td>", FormatDateDisplay(r["deadline"]));
                html.AppendFormat("<td style='font-size:11px;'>{0}</td>", HttpUtility.HtmlEncode(SafeVal(r["target_categories"])));
                html.AppendFormat("<td><span class='pa-badge {0}'>{1}</span></td>",
                    GetStatusBadgeClass(r["status"]), HttpUtility.HtmlEncode(status));
                html.AppendFormat("<td>{0}</td>", GetProgressBar(r["total_records"], r["completed_records"]));
                html.AppendFormat("<td class='pa-col-actions' onclick='event.stopPropagation()'>" +
                    "<button type='button' class='hr-btn hr-btn--ghost hr-btn--sm' onclick='openEditModal({0})' title='Edit'>" +
                    "<svg xmlns='http://www.w3.org/2000/svg' width='13' height='13' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7'/><path d='M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z'/></svg>" +
                    "</button></td>", sessId);
                html.Append("</tr>");
            }
        }
        litGridBody.Text = html.ToString();

        // Store for pager
        ViewState["TotalRecords"] = totalRecords;
        ViewState["CurrentPage"] = currentPage;
        ViewState["TotalPages"] = totalPages;

        // Pager
        litPagerInfo.Text = string.Format("Showing {0}–{1} of {2}",
            totalRecords == 0 ? 0 : offset + 1,
            Math.Min(offset + pageSize, totalRecords),
            totalRecords);

        BuildPager(currentPage, totalPages);
    }

    // Only count appraisals where the employee holds a VALID contract (mirrors AppraisalDashboard).
    private const string ValidContractFilter =
        " AND EXISTS (SELECT 1 FROM hrm_emp_contracts vc" +
        "             WHERE vc.empID = ar.employee_id AND vc.contractStatus = 'VALID')";

    private void LoadStats()
    {
        DataTable dt = ExecuteQuery(
            @"SELECT
                (SELECT COUNT(*) FROM appraisal_sessions) AS total_sessions,
                (SELECT COUNT(*) FROM appraisal_sessions WHERE status = 'ACTIVE') AS active_sessions,
                (SELECT COUNT(*) FROM appraisal_sessions WHERE status = 'DRAFT') AS draft_sessions,
                (SELECT COUNT(*) FROM appraisal_records ar
                 WHERE EXISTS (SELECT 1 FROM hrm_emp_contracts vc
                               WHERE vc.empID = ar.employee_id AND vc.contractStatus = 'VALID')) AS total_appraisals,
                (SELECT COUNT(*) FROM appraisal_records ar
                 WHERE ar.status IN ('COMPLETED','HR_REVIEWED')
                   AND EXISTS (SELECT 1 FROM hrm_emp_contracts vc
                               WHERE vc.empID = ar.employee_id AND vc.contractStatus = 'VALID')) AS completed_appraisals");

        if (dt.Rows.Count > 0)
        {
            DataRow r = dt.Rows[0];
            litStatTotal.Text     = SafeInt(r["total_sessions"]).ToString("N0");
            litStatActive.Text    = SafeInt(r["active_sessions"]).ToString("N0");
            litStatDraft.Text     = SafeInt(r["draft_sessions"]).ToString("N0");
            litStatAppraisals.Text = SafeInt(r["total_appraisals"]).ToString("N0");
            litStatCompleted.Text = SafeInt(r["completed_appraisals"]).ToString("N0");
        }
    }

    private void BuildPager(int current, int totalPages)
    {
        if (totalPages <= 1) { litPager.Text = ""; return; }

        StringBuilder sb = new StringBuilder();
        // Previous
        if (current > 1)
            sb.AppendFormat("<a href='{0}' class='pa-pager__btn'>&laquo;</a>", BuildFilterUrl("page=" + (current - 1)));
        else
            sb.Append("<span class='pa-pager__btn pa-pager__btn--disabled'>&laquo;</span>");

        int startPage = Math.Max(1, current - 2);
        int endPage = Math.Min(totalPages, current + 2);

        for (int i = startPage; i <= endPage; i++)
        {
            if (i == current)
                sb.AppendFormat("<span class='pa-pager__btn pa-pager__btn--active'>{0}</span>", i);
            else
                sb.AppendFormat("<a href='{0}' class='pa-pager__btn'>{1}</a>", BuildFilterUrl("page=" + i), i);
        }

        // Next
        if (current < totalPages)
            sb.AppendFormat("<a href='{0}' class='pa-pager__btn'>&raquo;</a>", BuildFilterUrl("page=" + (current + 1)));
        else
            sb.Append("<span class='pa-pager__btn pa-pager__btn--disabled'>&raquo;</span>");

        litPager.Text = sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════
    private string BuildFilterUrl(string extra)
    {
        StringBuilder sb = new StringBuilder("AppraisalSessions.aspx?");
        if (!string.IsNullOrEmpty(QsSearch)) sb.AppendFormat("q={0}&", HttpUtility.UrlEncode(QsSearch));
        if (!string.IsNullOrEmpty(QsStatus)) sb.AppendFormat("status={0}&", HttpUtility.UrlEncode(QsStatus));
        if (!string.IsNullOrEmpty(extra)) sb.Append(extra);
        return sb.ToString().TrimEnd('&');
    }

    private string SafeVal(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        return val.ToString();
    }

    private int SafeInt(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        int result;
        return int.TryParse(val.ToString(), out result) ? result : 0;
    }

    private string FormatDate(object val)
    {
        if (val == null || val == DBNull.Value) return "";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt))
            return dt.ToString("yyyy-MM-dd");
        return val.ToString();
    }

    private string FormatDateDisplay(object val)
    {
        if (val == null || val == DBNull.Value) return "—";
        DateTime dt;
        if (DateTime.TryParse(val.ToString(), out dt))
            return dt.ToString("dd MMM yyyy");
        return val.ToString();
    }

    protected string GetStatusBadgeClass(object statusObj)
    {
        string s = SafeVal(statusObj).ToUpper();
        switch (s)
        {
            case "DRAFT": return "pa-badge--draft";
            case "ACTIVE": return "pa-badge--active";
            case "CLOSED": return "pa-badge--closed";
            case "ARCHIVED": return "pa-badge--archived";
            default: return "pa-badge--draft";
        }
    }

    protected string GetProgressBar(object totalObj, object completedObj)
    {
        int total = Convert.ToInt32(totalObj);
        int completed = Convert.ToInt32(completedObj);
        if (total == 0) return "<span class='pa-progress-empty'>No appraisals</span>";

        double pct = Math.Round((double)completed / total * 100, 1);
        string color = pct >= 75 ? "var(--success)" : pct >= 40 ? "var(--warning)" : pct > 0 ? "var(--brand)" : "var(--border)";
        return string.Format(
            "<div class='pa-progress'>" +
            "<div class='pa-progress__bar' style='width:{0}%;background:{1}'></div>" +
            "</div>" +
            "<span class='pa-progress__text'>{2}/{3} ({0}%)</span>",
            pct, color, completed, total);
    }

    private void ShowInlineError(string message)
    {
        string js = string.Format(
            "alert('{0}');", HttpUtility.JavaScriptStringEncode(message));
        ScriptManager.RegisterStartupScript(this, GetType(), "err_" + DateTime.Now.Ticks, js, true);
    }

    private void RedirectWithFlash(string message, bool success)
    {
        string url = BuildFilterUrl("msg=" + HttpUtility.UrlEncode(message) + "&ok=" + (success ? "1" : "0"));
        url = url.TrimEnd('&');
        Response.Redirect(url, true);
    }

    private void ShowFlashMessage()
    {
        string msg = (Request.QueryString["msg"] ?? "").Trim();
        if (string.IsNullOrEmpty(msg)) return;
        bool ok = (Request.QueryString["ok"] ?? "") == "1";
        string js = string.Format(
            "var b=document.createElement('div');" +
            "b.className='hr-flash-msg';" +
            "b.style.cssText='padding:12px 20px;margin-bottom:14px;border-radius:6px;font-size:13px;font-weight:500;" +
            "background:{0};color:#fff;display:flex;align-items:center;gap:8px;animation:modalSlide .3s ease-out;';" +
            "b.innerHTML='{1}<button onclick=\"this.parentNode.remove()\" style=\"margin-left:auto;background:none;border:none;color:#fff;font-size:18px;cursor:pointer;\">&times;</button>';" +
            "var card=document.querySelector('.cd-card');" +
            "if(card)card.parentNode.insertBefore(b,card);" +
            "setTimeout(function(){{if(b.parentNode)b.remove();}},6000);",
            ok ? "#28a745" : "#d32f2f",
            HttpUtility.JavaScriptStringEncode(msg));
        ScriptManager.RegisterStartupScript(this, GetType(), "flash_" + DateTime.Now.Ticks, js, true);
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

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH / ACCESS / AUDIT
    // ═══════════════════════════════════════════════════════════════════
    private bool IsCallerAuthenticated()
    {
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated
                && !string.IsNullOrEmpty(User.Identity.Name)) return true;
        }
        catch { }
        try
        {
            if (Session != null)
            {
                object u = Session["username"];
                if (u != null && !string.IsNullOrEmpty(u.ToString().Trim())) return true;
            }
        }
        catch { }
        return false;
    }

    private string CurrentUsername()
    {
        try
        {
            if (Session != null && Session["username"] != null && !string.IsNullOrEmpty(Session["username"].ToString().Trim()))
                return Session["username"].ToString().Trim();
        }
        catch { }
        try
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated) return User.Identity.Name ?? "";
        }
        catch { }
        return "";
    }

    private bool? _hrAccess;

    /// <summary>
    /// HR appraisal administration: RBAC admin wildcard, an active sys role 'admin' or
    /// 'hr_manager', or legacy my_aspnet role Administrator / System Admin / Human Resource /
    /// Human Resource Manager. (Same rule as AppraisalView.)
    /// </summary>
    private bool HasHrAppraisalAccess()
    {
        if (_hrAccess.HasValue) return _hrAccess.Value;
        bool ok = false;
        try
        {
            if (IsCallerAuthenticated())
            {
                if (RoleAccessService.IsAdmin()) ok = true;
                string u = CurrentUsername();
                if (!ok && !string.IsNullOrEmpty(u))
                {
                    DataTable dt = ExecuteQuery(
                        @"SELECT
                            (SELECT COUNT(*) FROM sys_user_roles ur
                               JOIN sys_roles r ON r.id = ur.role_id
                              WHERE ur.username = @u AND ur.is_active = 1 AND r.is_active = 1
                                AND (ur.expires_at IS NULL OR ur.expires_at > NOW())
                                AND r.role_code IN ('admin','hr_manager'))
                          + (SELECT COUNT(*) FROM my_aspnet_users mu
                               JOIN my_aspnet_usersinroles mur ON mur.userId = mu.id
                               JOIN my_aspnet_roles mr ON mr.id = mur.roleId
                              WHERE mu.name = @u
                                AND mr.name IN ('Administrator','System Admin','Human Resource','Human Resource Manager')) AS n",
                        new MySqlParameter("@u", u));
                    ok = dt.Rows.Count > 0 && SafeInt(dt.Rows[0]["n"]) > 0;
                }
            }
        }
        catch { ok = false; }
        _hrAccess = ok;
        return ok;
    }

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
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
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
                if (parms != null)
                    foreach (MySqlParameter p in parms) cmd.Parameters.Add(p);
                return cmd.ExecuteNonQuery();
            }
        }
    }

    private MySqlParameter[] CloneParams(List<MySqlParameter> parms)
    {
        MySqlParameter[] clone = new MySqlParameter[parms.Count];
        for (int i = 0; i < parms.Count; i++)
            clone[i] = new MySqlParameter(parms[i].ParameterName, parms[i].Value);
        return clone;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AJAX: SEND NOTIFICATIONS
    // ═══════════════════════════════════════════════════════════════════
    private void AjaxSendNotifications()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request method\"}"); return; }

        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId) || sessionId <= 0)
        { Response.Write("{\"error\":\"Invalid session ID\"}"); return; }

        bool resend = (Request.QueryString["resend"] ?? "0") == "1";

        DataTable dtSess = ExecuteQuery(
            "SELECT session_id, session_title, period_start, period_end, deadline, status FROM appraisal_sessions WHERE session_id = @id",
            new MySqlParameter("@id", sessionId));
        if (dtSess.Rows.Count == 0) { Response.Write("{\"error\":\"Session not found\"}"); return; }

        string sessStatus = SafeVal(dtSess.Rows[0]["status"]);
        if (sessStatus != "ACTIVE") { Response.Write("{\"error\":\"Session must be ACTIVE to send notifications\"}"); return; }

        // Always retry FAILED records (failed = should send, just errored last time).
        // resend=1 additionally re-sends already-SENT and NO_EMAIL records.
        ExecuteNonQuery(
            "UPDATE appraisal_records SET notify_email_status='PENDING', notify_email_sent_at=NULL, notify_email_error=NULL WHERE session_id=@sid AND notify_email_status='FAILED'",
            new MySqlParameter("@sid", sessionId));

        if (resend)
        {
            ExecuteNonQuery(
                "UPDATE appraisal_records SET notify_email_status='PENDING', notify_email_sent_at=NULL, notify_email_error=NULL WHERE session_id=@sid AND notify_email_status IN ('SENT','NO_EMAIL')",
                new MySqlParameter("@sid", sessionId));
        }

        DataTable dtCount = ExecuteQuery(
            "SELECT COUNT(*) AS cnt FROM appraisal_records WHERE session_id=@sid AND IFNULL(notify_email_status,'PENDING')='PENDING'",
            new MySqlParameter("@sid", sessionId));
        int total = SafeInt(dtCount.Rows[0]["cnt"]);

        if (total == 0) { Response.Write("{\"started\":false,\"total\":0,\"message\":\"All notifications have already been sent. Use Re-send to send again.\"}"); return; }

        string connStr       = ConnStr;
        string portalBaseUrl = System.Configuration.ConfigurationManager.AppSettings["PORTAL_BASE_URL"] ?? "https://eportal.mru.ac.ug";
        DataRow sessRow      = dtSess.Rows[0];

        string sessionTitle = sessRow["session_title"] != DBNull.Value ? sessRow["session_title"].ToString().Trim() : "";
        string periodStart  = (sessRow["period_start"] != null && sessRow["period_start"] != DBNull.Value) ? ((DateTime)sessRow["period_start"]).ToString("dd MMM yyyy") : "";
        string periodEnd    = (sessRow["period_end"]   != null && sessRow["period_end"]   != DBNull.Value) ? ((DateTime)sessRow["period_end"]).ToString("dd MMM yyyy")   : "";
        string deadline     = (sessRow["deadline"]     != null && sessRow["deadline"]     != DBNull.Value) ? ((DateTime)sessRow["deadline"]).ToString("dd MMM yyyy")     : "";

        var emailThread = new System.Threading.Thread(delegate()
        {
            try
            {
                DataTable dtRecs = new DataTable();
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand(
                        @"SELECT ar.record_id, e.emp_name, IFNULL(e.emp_email,'') AS emp_email,
                                 IFNULL(rev.emp_name,'') AS reviewer_name
                          FROM   appraisal_records ar
                          INNER JOIN hrm_employee e   ON e.empID   = ar.employee_id
                          LEFT  JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
                          WHERE  ar.session_id = @sid
                            AND  IFNULL(ar.notify_email_status,'PENDING') = 'PENDING'
                          ORDER BY e.emp_name", conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        using (var da = new MySqlDataAdapter(cmd)) da.Fill(dtRecs);
                    }
                }

                foreach (DataRow r in dtRecs.Rows)
                {
                    int    rid          = (r["record_id"] != DBNull.Value) ? Convert.ToInt32(r["record_id"]) : 0;
                    string empName      = r["emp_name"]      != DBNull.Value ? r["emp_name"].ToString().Trim()      : "";
                    string empEmail     = r["emp_email"]     != DBNull.Value ? r["emp_email"].ToString().Trim()     : "";
                    string reviewerName = r["reviewer_name"] != DBNull.Value ? r["reviewer_name"].ToString().Trim() : "";

                    string newStatus, sentAt, errMsg;
                    if (string.IsNullOrEmpty(empEmail) || !empEmail.Contains("@"))
                    {
                        newStatus = "NO_EMAIL"; sentAt = "NULL"; errMsg = "DBNull.Value";
                    }
                    else
                    {
                        string appraisalUrl = portalBaseUrl + "/SelfAppraisal.aspx?rid=" + rid;
                        string subject = "[MRU Appraisal] Your self-appraisal for " + sessionTitle + " is now open";
                        string html    = BuildEmployeeEmailHtml(empName, sessionTitle, periodStart, periodEnd, deadline, reviewerName, appraisalUrl, portalBaseUrl);
                        string result  = EmailSenderProtocol.SendHtmlEmail(html, empEmail, subject, "MRU Appraisal");
                        bool   ok      = result != null && result.StartsWith("Email sent");
                        newStatus = ok ? "SENT" : "FAILED";
                        sentAt    = ok ? "NOW()" : "NULL";
                        errMsg    = ok ? "" : (result != null && result.Length > 250 ? result.Substring(0, 250) : result ?? "");
                    }

                    using (var conn = new MySqlConnection(connStr))
                    {
                        conn.Open();
                        using (var cmd = new MySqlCommand(
                            "UPDATE appraisal_records SET notify_email_status=@st, notify_email_sent_at=" + (newStatus == "SENT" ? "NOW()" : "NULL") + ", notify_email_error=@err WHERE record_id=@rid", conn))
                        {
                            cmd.Parameters.AddWithValue("@st",  newStatus);
                            cmd.Parameters.AddWithValue("@err", string.IsNullOrEmpty(errMsg) ? (object)DBNull.Value : (object)errMsg);
                            cmd.Parameters.AddWithValue("@rid", rid);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { /* background thread — isolated */ }
        });
        emailThread.IsBackground = true;
        emailThread.Start();

        Response.Write(string.Format("{{\"started\":true,\"total\":{0}}}", total));
    }

    // ─── AJAX: Get Per-Record Notification Status ──────────────────────
    private void AjaxGetNotifyStatus()
    {
        int sessionId;
        if (!int.TryParse(Request.QueryString["id"], out sessionId) || sessionId <= 0)
        { Response.Write("{\"error\":\"Invalid session ID\"}"); return; }

        DataTable dt = ExecuteQuery(
            @"SELECT ar.record_id,
                     e.emp_name,
                     IFNULL(e.emp_email,'') AS emp_email,
                     IFNULL(ar.notify_email_status,'PENDING') AS notify_status,
                     IFNULL(ar.notify_email_error,'')         AS notify_error
              FROM   appraisal_records ar
              INNER  JOIN hrm_employee e ON e.empID = ar.employee_id
              WHERE  ar.session_id = @sid
              ORDER  BY FIELD(IFNULL(ar.notify_email_status,'PENDING'),'SENT','FAILED','NO_EMAIL','PENDING'), e.emp_name",
            new MySqlParameter("@sid", sessionId));

        int total = dt.Rows.Count, sent = 0, failed = 0, noEmail = 0, pending = 0;
        var records = new StringBuilder("[");
        bool first = true;
        foreach (DataRow r in dt.Rows)
        {
            string ns = r["notify_status"] != DBNull.Value ? r["notify_status"].ToString() : "PENDING";
            switch (ns) { case "SENT": sent++; break; case "FAILED": failed++; break; case "NO_EMAIL": noEmail++; break; default: pending++; break; }
            if (!first) records.Append(",");
            first = false;
            records.Append("{");
            records.AppendFormat("\"record_id\":{0},",       r["record_id"] != DBNull.Value ? r["record_id"].ToString() : "0");
            records.AppendFormat("\"emp_name\":\"{0}\",",    EscapeJson(r["emp_name"]));
            records.AppendFormat("\"emp_email\":\"{0}\",",   EscapeJson(r["emp_email"]));
            records.AppendFormat("\"notify_status\":\"{0}\",", EscapeJson(ns));
            records.AppendFormat("\"notify_error\":\"{0}\"",   EscapeJson(r["notify_error"]));
            records.Append("}");
        }
        records.Append("]");

        Response.Write(string.Format("{{\"total\":{0},\"sent\":{1},\"failed\":{2},\"no_email\":{3},\"pending\":{4},\"done\":{5},\"records\":{6}}}",
            total, sent, failed, noEmail, pending, pending == 0 ? "true" : "false", records));
    }

    // ─── AJAX: Send to Single Record ──────────────────────────────────
    private void AjaxSendSingleNotify()
    {
        if (!string.Equals(Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        { Response.Write("{\"error\":\"Invalid request method\"}"); return; }

        int recordId;
        if (!int.TryParse(Request.QueryString["id"], out recordId) || recordId <= 0)
        { Response.Write("{\"error\":\"Invalid record ID\"}"); return; }

        string portalBaseUrl = System.Configuration.ConfigurationManager.AppSettings["PORTAL_BASE_URL"] ?? "https://eportal.mru.ac.ug";

        DataTable dt = ExecuteQuery(
            @"SELECT ar.record_id, e.emp_name, IFNULL(e.emp_email,'') AS emp_email,
                     IFNULL(rev.emp_name,'') AS reviewer_name,
                     s.session_title, s.period_start, s.period_end, s.deadline
              FROM   appraisal_records ar
              INNER  JOIN hrm_employee e   ON e.empID   = ar.employee_id
              LEFT   JOIN hrm_employee rev ON rev.empID = ar.reviewer_id
              INNER  JOIN appraisal_sessions s ON s.session_id = ar.session_id
              WHERE  ar.record_id = @rid LIMIT 1",
            new MySqlParameter("@rid", recordId));

        if (dt.Rows.Count == 0) { Response.Write("{\"error\":\"Record not found\"}"); return; }
        DataRow r = dt.Rows[0];

        string empName      = r["emp_name"]      != DBNull.Value ? r["emp_name"].ToString().Trim()      : "";
        string empEmail     = r["emp_email"]      != DBNull.Value ? r["emp_email"].ToString().Trim()     : "";
        string reviewerName = r["reviewer_name"]  != DBNull.Value ? r["reviewer_name"].ToString().Trim() : "";
        string sessionTitle = r["session_title"]  != DBNull.Value ? r["session_title"].ToString().Trim() : "";
        string periodStart  = (r["period_start"] != null && r["period_start"] != DBNull.Value) ? ((DateTime)r["period_start"]).ToString("dd MMM yyyy") : "";
        string periodEnd    = (r["period_end"]   != null && r["period_end"]   != DBNull.Value) ? ((DateTime)r["period_end"]).ToString("dd MMM yyyy")   : "";
        string deadline     = (r["deadline"]     != null && r["deadline"]     != DBNull.Value) ? ((DateTime)r["deadline"]).ToString("dd MMM yyyy")     : "";

        if (string.IsNullOrEmpty(empEmail) || !empEmail.Contains("@"))
        {
            ExecuteNonQuery("UPDATE appraisal_records SET notify_email_status='NO_EMAIL', notify_email_sent_at=NULL, notify_email_error=NULL WHERE record_id=@rid",
                new MySqlParameter("@rid", recordId));
            Response.Write("{\"ok\":false,\"status\":\"NO_EMAIL\",\"message\":\"No valid email address on record.\"}");
            return;
        }

        string appraisalUrl = portalBaseUrl + "/SelfAppraisal.aspx?rid=" + recordId;
        string subject      = "[MRU Appraisal] Your self-appraisal for " + sessionTitle + " is now open";
        string html         = BuildEmployeeEmailHtml(empName, sessionTitle, periodStart, periodEnd, deadline, reviewerName, appraisalUrl, portalBaseUrl);
        string result       = EmailSenderProtocol.SendHtmlEmail(html, empEmail, subject, "MRU Appraisal");
        bool   ok           = result != null && result.StartsWith("Email sent");
        string errMsg       = ok ? "" : (result != null && result.Length > 250 ? result.Substring(0, 250) : result ?? "");

        ExecuteNonQuery(
            "UPDATE appraisal_records SET notify_email_status=@st, notify_email_sent_at=" + (ok ? "NOW()" : "NULL") + ", notify_email_error=@err WHERE record_id=@rid",
            new MySqlParameter("@st",  ok ? "SENT" : "FAILED"),
            new MySqlParameter("@err", string.IsNullOrEmpty(errMsg) ? (object)DBNull.Value : (object)errMsg),
            new MySqlParameter("@rid", recordId));

        if (ok)
            Response.Write("{\"ok\":true,\"status\":\"SENT\",\"message\":\"Email sent to " + EscapeJson(empEmail) + "\"}");
        else
            Response.Write("{\"ok\":false,\"status\":\"FAILED\",\"message\":\"" + EscapeJson(errMsg) + "\"}");
    }

    // ─── Employee Notification Email ──────────────────────────────────
    private static string BuildEmployeeEmailHtml(
        string empName, string sessionTitle, string periodStart, string periodEnd,
        string deadline, string reviewerName, string appraisalUrl, string portalUrl)
    {
        string enc_emp     = HttpUtility.HtmlEncode(empName);
        string enc_session = HttpUtility.HtmlEncode(sessionTitle);
        string enc_review  = HttpUtility.HtmlAttributeEncode(appraisalUrl);
        string enc_portal  = HttpUtility.HtmlAttributeEncode(portalUrl);

        string periodRow = (string.IsNullOrEmpty(periodStart) && string.IsNullOrEmpty(periodEnd)) ? "" :
            "<tr><td style=\"font-size:12px;color:#888;width:120px;vertical-align:top;padding-bottom:10px;padding-right:12px;\">Period</td>" +
            "<td style=\"font-size:13px;color:#1a1a2e;font-weight:600;padding-bottom:10px;\">" +
            HttpUtility.HtmlEncode(periodStart) + (string.IsNullOrEmpty(periodEnd) ? "" : " &mdash; " + HttpUtility.HtmlEncode(periodEnd)) + "</td></tr>";

        string deadlineRow = string.IsNullOrEmpty(deadline) ? "" :
            "<tr><td style=\"font-size:12px;color:#888;vertical-align:top;padding-bottom:10px;padding-right:12px;\">Complete By</td>" +
            "<td style=\"font-size:13px;color:#c0392b;font-weight:700;padding-bottom:10px;\">" + HttpUtility.HtmlEncode(deadline) + " &mdash; <em>Do not miss this deadline</em></td></tr>";

        string supervisorRow = string.IsNullOrEmpty(reviewerName) ? "" :
            "<tr><td style=\"font-size:12px;color:#888;vertical-align:top;padding-right:12px;\">Your Supervisor</td>" +
            "<td style=\"font-size:13px;color:#1a1a2e;font-weight:600;\">" + HttpUtility.HtmlEncode(reviewerName) + "</td></tr>";

        return "<!DOCTYPE html>\n" +
"<html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>\n" +
"<body style=\"margin:0;padding:0;background:#f0f2f5;font-family:Arial,Helvetica,sans-serif;\">\n" +
"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background:#f0f2f5;padding:36px 16px;\">\n" +
"  <tr><td align=\"center\">\n" +
"    <table width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"\n" +
"           style=\"max-width:600px;width:100%;background:#ffffff;border-radius:4px;overflow:hidden;box-shadow:0 2px 10px rgba(0,0,0,0.10);\">\n" +

// HEADER
"      <tr><td style=\"background:#05275C;padding:28px 40px;text-align:center;\">\n" +
"        <div style=\"color:#ffffff;font-size:24px;font-weight:700;letter-spacing:2px;line-height:1;\">MRU</div>\n" +
"        <div style=\"color:#8ab4d8;font-size:11px;letter-spacing:1px;margin-top:6px;text-transform:uppercase;\">Performance Appraisal System</div>\n" +
"      </td></tr>\n" +

// ACTION BANNER
"      <tr><td style=\"background:#1a7a3a;padding:10px 40px;text-align:center;\">\n" +
"        <span style=\"color:#ffffff;font-size:12px;font-weight:600;letter-spacing:0.5px;\">&#9679;&nbsp; Action Required &mdash; Your Self-Appraisal Is Now Open</span>\n" +
"      </td></tr>\n" +

// BODY
"      <tr><td style=\"padding:36px 40px;\">\n" +
"        <p style=\"margin:0 0 4px;font-size:13px;color:#888;\">Dear</p>\n" +
"        <p style=\"margin:0 0 24px;font-size:19px;font-weight:700;color:#05275C;\">" + enc_emp + ",</p>\n" +
"        <p style=\"margin:0 0 10px;font-size:14px;color:#333;line-height:1.8;\">\n" +
"          The annual performance appraisal cycle has commenced at <strong style=\"color:#05275C;\">Muteesa I Royal University</strong>.\n" +
"          You are required to complete your <strong>self-appraisal</strong> for the session below.\n" +
"        </p>\n" +
"        <p style=\"margin:0 0 26px;font-size:13px;color:#666;line-height:1.7;\">\n" +
"          This process allows you to reflect on your performance, document your achievements,\n" +
"          and rate yourself on agreed outputs and core competencies. Your supervisor will\n" +
"          review and rate your submission after you submit.\n" +
"        </p>\n" +

// DETAILS BOX
"        <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"\n" +
"               style=\"background:#f5f7fa;border:1px solid #e0e5ed;border-radius:4px;margin-bottom:28px;\">\n" +
"          <tr><td style=\"padding:20px 24px;\">\n" +
"            <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">\n" +
"              <tr>\n" +
"                <td style=\"font-size:12px;color:#888;width:120px;vertical-align:top;padding-bottom:10px;padding-right:12px;\">Session</td>\n" +
"                <td style=\"font-size:13px;color:#1a1a2e;font-weight:600;padding-bottom:10px;\">" + enc_session + "</td>\n" +
"              </tr>\n" +
periodRow +
deadlineRow +
supervisorRow +
"            </table>\n" +
"          </td></tr>\n" +
"        </table>\n" +

// STEPS CHECKLIST
"        <p style=\"margin:0 0 12px;font-size:13px;font-weight:700;color:#05275C;\">What you need to complete:</p>\n" +
"        <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin-bottom:28px;\">\n" +
"          <tr><td style=\"padding:4px 0;font-size:13px;color:#333;\">&#10003;&nbsp;&nbsp;<strong>Section A</strong> &mdash; Personal Information (auto-filled)</td></tr>\n" +
"          <tr><td style=\"padding:4px 0;font-size:13px;color:#333;\">&#10003;&nbsp;&nbsp;<strong>Section B</strong> &mdash; Performance Outputs &amp; Self-Rating</td></tr>\n" +
"          <tr><td style=\"padding:4px 0;font-size:13px;color:#333;\">&#10003;&nbsp;&nbsp;<strong>Section C</strong> &mdash; Core Competencies Self-Assessment</td></tr>\n" +
"          <tr><td style=\"padding:4px 0;font-size:13px;color:#333;\">&#10003;&nbsp;&nbsp;<strong>Section D</strong> &mdash; Action Plan for Improvement</td></tr>\n" +
"          <tr><td style=\"padding:4px 0;font-size:13px;color:#333;\">&#10003;&nbsp;&nbsp;<strong>Section E</strong> &mdash; Personal Reflections</td></tr>\n" +
"        </table>\n" +

// CTA BUTTON
"        <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin-bottom:28px;\">\n" +
"          <tr><td align=\"center\">\n" +
"            <a href=\"" + enc_review + "\" target=\"_blank\"\n" +
"               style=\"display:inline-block;background:#1a7a3a;color:#ffffff;font-size:14px;\n" +
"                      font-weight:700;text-decoration:none;padding:16px 52px;\n" +
"                      letter-spacing:0.5px;line-height:1;\">\n" +
"              Begin My Self-Appraisal &nbsp;&rarr;\n" +
"            </a>\n" +
"          </td></tr>\n" +
"        </table>\n" +
"        <p style=\"margin:0 0 5px;font-size:11px;color:#aaa;line-height:1.5;\">If the button does not work, paste this link into your browser:</p>\n" +
"        <p style=\"margin:0;font-size:11px;color:#174DA4;word-break:break-all;line-height:1.6;\">" + appraisalUrl + "</p>\n" +
"      </td></tr>\n" +

// FOOTER
"      <tr><td style=\"background:#f5f7fa;border-top:1px solid #e0e5ed;padding:18px 40px;text-align:center;\">\n" +
"        <p style=\"margin:0 0 4px;font-size:11px;color:#aaa;\">This is an automated notification from the <a href=\"" + enc_portal + "\" style=\"color:#174DA4;text-decoration:none;\">MRU Staff Portal</a>. Please do not reply.</p>\n" +
"        <p style=\"margin:0;font-size:10px;color:#ccc;\">Muteesa I Royal University</p>\n" +
"      </td></tr>\n" +

"    </table>\n  </td></tr>\n</table>\n</body></html>";
    }
}
