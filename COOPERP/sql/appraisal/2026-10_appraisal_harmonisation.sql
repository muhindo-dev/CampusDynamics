-- ============================================================================
-- Performance Appraisal harmonisation (Oct 2026)
-- Aligns Section B with the Council "Evaluation Form for Achievements of Staff
-- Responsibilities and Key Performance Areas" and gives both apps (eadmin +
-- eportal) ONE reviewer rule, ONE score formula and ONE classification scale.
-- Plan: COOPERP/APPRAISAL_STABILISATION_PLAN_2026-10.md
-- Idempotent-ish: run once; ALTERs fail loudly if re-run.
-- ============================================================================

-- ---------------------------------------------------------------------------
-- 1. Section B: Responsibility/KPA -> Expected Standard -> Achievement -> Evidence
--    agreed_output = KPA, performance_indicators = Expected Standard,
--    result_areas = Achievement (existing columns reused, old rows stay valid)
-- ---------------------------------------------------------------------------
ALTER TABLE appraisal_section_b
    ADD COLUMN standard_id   INT          NULL AFTER slot_number,
    ADD COLUMN is_catalogue  TINYINT(1)   NOT NULL DEFAULT 0 AFTER standard_id,
    ADD COLUMN evidence_text TEXT         NULL AFTER result_areas,
    ADD COLUMN is_na         TINYINT(1)   NOT NULL DEFAULT 0 AFTER evidence_text,
    ADD COLUMN na_reason     VARCHAR(500) NULL AFTER is_na,
    ADD UNIQUE KEY uq_b_record_slot (record_id, slot_number);

ALTER TABLE appraisal_section_c
    ADD UNIQUE KEY uq_c_record_code (record_id, competency_code);

-- ---------------------------------------------------------------------------
-- 2. Record header snapshot + electronic sign-off + employee acknowledgement
-- ---------------------------------------------------------------------------
ALTER TABLE appraisal_records
    ADD COLUMN standards_group_id   INT          NULL,
    ADD COLUMN snap_position        VARCHAR(200) NULL,
    ADD COLUMN snap_department      VARCHAR(200) NULL,
    ADD COLUMN snap_reports_to      VARCHAR(200) NULL,
    ADD COLUMN reviewer_name        VARCHAR(200) NULL,
    ADD COLUMN reviewer_title       VARCHAR(200) NULL,
    ADD COLUMN employee_signed_at   DATETIME     NULL,
    ADD COLUMN employee_sign_name   VARCHAR(200) NULL,
    ADD COLUMN reviewer_signed_at   DATETIME     NULL,
    ADD COLUMN reviewer_sign_name   VARCHAR(200) NULL,
    ADD COLUMN employee_ack         VARCHAR(10)  NULL,
    ADD COLUMN employee_ack_comment TEXT         NULL,
    ADD COLUMN employee_ack_at      DATETIME     NULL;

-- admin users act by login name (they may have no empID)
ALTER TABLE appraisal_record_audit
    ADD COLUMN actor_username VARCHAR(100) NULL AFTER actor_empid;

-- ---------------------------------------------------------------------------
-- 3. Expected Standards catalogue (Council annex) + department mapping
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS appraisal_standard_groups (
    group_id    INT AUTO_INCREMENT PRIMARY KEY,
    group_code  VARCHAR(30)  NOT NULL,
    group_name  VARCHAR(150) NOT NULL,
    applies_to  VARCHAR(20)  NOT NULL DEFAULT 'ADMINISTRATIVE',  -- ACADEMIC | ADMINISTRATIVE | ANY
    sort_order  INT          NOT NULL DEFAULT 0,
    is_active   TINYINT(1)   NOT NULL DEFAULT 1,
    created_at  DATETIME     NULL,
    updated_at  DATETIME     NULL,
    UNIQUE KEY uq_group_code (group_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS appraisal_standards (
    standard_id       INT AUTO_INCREMENT PRIMARY KEY,
    group_id          INT          NOT NULL,
    kpa_title         VARCHAR(255) NOT NULL,
    expected_standard TEXT         NOT NULL,
    sort_order        INT          NOT NULL DEFAULT 0,
    is_active         TINYINT(1)   NOT NULL DEFAULT 1,
    created_at        DATETIME     NULL,
    updated_at        DATETIME     NULL,
    KEY idx_std_group (group_id, sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS appraisal_evidence (
    evidence_id    INT AUTO_INCREMENT PRIMARY KEY,
    record_id      INT          NOT NULL,
    slot_number    INT          NOT NULL,
    original_name  VARCHAR(255) NOT NULL,
    stored_name    VARCHAR(100) NOT NULL,
    content_type   VARCHAR(100) NULL,
    size_bytes     INT          NOT NULL DEFAULT 0,
    uploaded_by    INT          NULL,
    uploaded_at    DATETIME     NOT NULL,
    KEY idx_ev_record (record_id, slot_number)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

ALTER TABLE hrm_departments ADD COLUMN standards_group_id INT NULL;

INSERT INTO appraisal_standard_groups (group_code, group_name, applies_to, sort_order, created_at) VALUES
('LECTURERS',       'Lecturers / Teaching Staff',      'ACADEMIC',       10,  NOW()),
('RESEARCH',        'Research Staff',                  'ACADEMIC',       20,  NOW()),
('ADMIN_GENERAL',   'Administrative Staff',            'ADMINISTRATIVE', 30,  NOW()),
('REGISTRAR',       'Academic Registrar''s Office',    'ADMINISTRATIVE', 40,  NOW()),
('FINANCE',         'Finance Office',                  'ADMINISTRATIVE', 50,  NOW()),
('MARKETING',       'Marketing Office',                'ADMINISTRATIVE', 60,  NOW()),
('INTERNATIONAL',   'International Office',            'ADMINISTRATIVE', 70,  NOW()),
('STUDENT_AFFAIRS', 'Student Affairs Staff',           'ADMINISTRATIVE', 80,  NOW()),
('PROCUREMENT',     'Procurement Office',              'ADMINISTRATIVE', 90,  NOW()),
('ICT',             'ICT Office',                      'ADMINISTRATIVE', 100, NOW()),
('HR',              'Human Resource (HR) Office',      'ADMINISTRATIVE', 110, NOW()),
('AUDIT',           'Audit Office',                    'ADMINISTRATIVE', 120, NOW()),
('LIBRARY',         'Library Staff',                   'ADMINISTRATIVE', 130, NOW());

-- helper: insert standards by group code
DROP PROCEDURE IF EXISTS _seed_std;
DELIMITER $$
CREATE PROCEDURE _seed_std(IN p_code VARCHAR(30), IN p_sort INT, IN p_kpa VARCHAR(255), IN p_std TEXT)
BEGIN
    INSERT INTO appraisal_standards (group_id, kpa_title, expected_standard, sort_order, created_at)
    SELECT group_id, p_kpa, p_std, p_sort, NOW() FROM appraisal_standard_groups WHERE group_code = p_code;
END$$
DELIMITER ;

-- Lecturers / Teaching Staff
CALL _seed_std('LECTURERS', 1, 'Effective Teaching Delivery', 'Conducting lectures, tutorials, and practical sessions consistently and professionally, without absenteeism or dodging classes.');
CALL _seed_std('LECTURERS', 2, 'Curriculum Preparation', 'Developing lesson plans, course outlines, and teaching materials aligned with departmental and university standards.');
CALL _seed_std('LECTURERS', 3, 'Student Engagement', 'Encouraging active participation, mentorship, and guidance to foster academic growth and personal development.');
CALL _seed_std('LECTURERS', 4, 'Assessment & Feedback', 'Timely preparation, administration, and marking of examinations and assignments, with constructive feedback to students.');
CALL _seed_std('LECTURERS', 5, 'Research & Publication', 'Conducting scholarly research, publishing in recognized journals, and contributing to the advancement of knowledge.');
CALL _seed_std('LECTURERS', 6, 'Professional Development', 'Continuous improvement through workshops, conferences, and training to enhance teaching and research skills.');
CALL _seed_std('LECTURERS', 7, 'Collaboration', 'Working with colleagues on curriculum development, departmental projects, and interdisciplinary initiatives.');
CALL _seed_std('LECTURERS', 8, 'Adherence to Policies', 'Compliance with academic regulations, ethical standards, and institutional policies.');
CALL _seed_std('LECTURERS', 9, 'Integrity & Accountability', 'Upholding honesty, fairness, and transparency in teaching, assessment, and research.');
CALL _seed_std('LECTURERS', 10, 'Community Service', 'Participation in outreach, consultancy, and service activities that support the university''s mission.');

-- Administrative Staff (general)
CALL _seed_std('ADMIN_GENERAL', 1, 'Policy Reviews & Development', 'Regular review, drafting, and updating of institutional policies to ensure compliance and relevance.');
CALL _seed_std('ADMIN_GENERAL', 2, 'Adherence to Policies', 'Consistent application of university rules, regulations, and governance frameworks.');
CALL _seed_std('ADMIN_GENERAL', 3, 'Document Development', 'Preparation of accurate reports, committee minutes, governance documents, and HR records.');
CALL _seed_std('ADMIN_GENERAL', 4, 'Strategic Plan Implementation', 'Active contribution to the execution and monitoring of the university''s strategic objectives.');
CALL _seed_std('ADMIN_GENERAL', 5, 'Teamwork & Good Relations', 'Collaboration across departments, fostering positive working relationships, and supporting colleagues.');
CALL _seed_std('ADMIN_GENERAL', 6, 'Customer Care & Client Handling', 'Professional responsiveness to students, staff, and external stakeholders, ensuring satisfaction and trust.');
CALL _seed_std('ADMIN_GENERAL', 7, 'Effective Management', 'Efficient coordination of resources, schedules, and administrative processes to meet institutional goals.');
CALL _seed_std('ADMIN_GENERAL', 8, 'Integrity & Accountability', 'Upholding ethical standards, transparency, and confidentiality in all administrative duties.');
CALL _seed_std('ADMIN_GENERAL', 9, 'Communication', 'Clear, timely, and professional correspondence with committees, governing bodies, and staff.');
CALL _seed_std('ADMIN_GENERAL', 10, 'Innovation & Initiative', 'Proactive identification of improvements in administrative systems and processes.');

-- Academic Registrar's Office
CALL _seed_std('REGISTRAR', 1, 'Admissions', 'Accurate and timely admission of students.');
CALL _seed_std('REGISTRAR', 2, 'Document Verification', 'Verification of academic documents.');
CALL _seed_std('REGISTRAR', 3, 'Student Records', 'Maintenance of student records and confidentiality.');
CALL _seed_std('REGISTRAR', 4, 'Registration, Examinations & Graduation', 'Coordination of registration, examinations, and graduation.');
CALL _seed_std('REGISTRAR', 5, 'Policy Reviews & Development', 'Policy reviews and development in admissions and governance.');
CALL _seed_std('REGISTRAR', 6, 'Strategic Enrolment', 'Implementation of strategic enrolment plans.');
CALL _seed_std('REGISTRAR', 7, 'Integrity & Transparency', 'Integrity and transparency in admissions.');

-- Finance Office
CALL _seed_std('FINANCE', 1, 'Bookkeeping & Reporting', 'Accuracy in bookkeeping and financial reporting.');
CALL _seed_std('FINANCE', 2, 'Audit & Regulatory Compliance', 'Compliance with audit standards and financial regulations.');
CALL _seed_std('FINANCE', 3, 'Budgets & Expenditure Reports', 'Timely preparation of budgets and expenditure reports.');
CALL _seed_std('FINANCE', 4, 'Payroll Management', 'Effective payroll management.');
CALL _seed_std('FINANCE', 5, 'Resource Allocation', 'Transparency and accountability in resource allocation.');
CALL _seed_std('FINANCE', 6, 'Risk & Internal Control', 'Risk management and internal control compliance.');
CALL _seed_std('FINANCE', 7, 'Financial Record Keeping', 'Good record keeping of financial documents.');
CALL _seed_std('FINANCE', 8, 'Receipts, Invoices & Vouchers', 'Proper management of receipts, invoices, and vouchers.');
CALL _seed_std('FINANCE', 9, 'Account Reconciliation', 'Timely reconciliation of accounts.');
CALL _seed_std('FINANCE', 10, 'Financial Policy Compliance', 'Compliance with financial policies and procedures.');
CALL _seed_std('FINANCE', 11, 'Integrity in Handling Funds', 'Integrity and accountability in handling funds.');
CALL _seed_std('FINANCE', 12, 'Audit Support', 'Support for audits and financial reviews.');

-- Marketing Office
CALL _seed_std('MARKETING', 1, 'Brand Management', 'Upholding and promoting the university''s image, values, and reputation across all platforms.');
CALL _seed_std('MARKETING', 2, 'Strategic Planning', 'Development and implementation of marketing strategies aligned with the institutional strategic plan.');
CALL _seed_std('MARKETING', 3, 'Advertising & Promotion', 'Designing and executing campaigns to attract students, partners, and stakeholders.');
CALL _seed_std('MARKETING', 4, 'Digital Marketing', 'Effective use of social media, websites, and online platforms to enhance visibility.');
CALL _seed_std('MARKETING', 5, 'Market Research', 'Conducting surveys and analysis to understand trends, student needs, and competitor positioning.');
CALL _seed_std('MARKETING', 6, 'Public Relations', 'Building strong relationships with media, alumni, and external stakeholders.');
CALL _seed_std('MARKETING', 7, 'Event Management', 'Organizing and promoting university events, open days, and outreach programs.');
CALL _seed_std('MARKETING', 8, 'Customer Care', 'Professional handling of inquiries from prospective students, parents, and partners.');
CALL _seed_std('MARKETING', 9, 'Collaboration', 'Working with academic and administrative units to ensure consistent messaging.');
CALL _seed_std('MARKETING', 10, 'Integrity & Accountability', 'Transparent reporting of marketing activities, budgets, and outcomes.');

-- International Office
CALL _seed_std('INTERNATIONAL', 1, 'International Admissions', 'Accurate and timely processing of applications of International Students ensuring compliance with admission policies.');
CALL _seed_std('INTERNATIONAL', 2, 'Scholarships & Grants', 'Transparent administration of international scholarships, grants, and funding opportunities.');
CALL _seed_std('INTERNATIONAL', 3, 'Immigration & Regulatory Compliance', 'Adherence to immigration laws, visa requirements, and regulatory frameworks.');
CALL _seed_std('INTERNATIONAL', 4, 'Student Orientation & Welfare', 'Support for international student orientation, cultural integration, and welfare services.');
CALL _seed_std('INTERNATIONAL', 5, 'Partnerships', 'Development and maintenance of collaborations with foreign universities, agencies, and organizations.');
CALL _seed_std('INTERNATIONAL', 6, 'Strategic Plan Contribution', 'Contribution to the university''s strategic plan through international programs and partnerships.');
CALL _seed_std('INTERNATIONAL', 7, 'Stakeholder Correspondence', 'Professional correspondence with international stakeholders, embassies, and partner institutions.');
CALL _seed_std('INTERNATIONAL', 8, 'Ethical Handling of Funds & Records', 'Ethical handling of grants, funds, and student records.');
CALL _seed_std('INTERNATIONAL', 9, 'Innovation & Initiative', 'Introduction of new programs to enhance global visibility and student experience.');
CALL _seed_std('INTERNATIONAL', 10, 'Teamwork', 'Teamwork with academic and administrative units to support internationalization goals.');

-- Student Affairs Staff
CALL _seed_std('STUDENT_AFFAIRS', 1, 'Counseling & Support', 'Effective student counseling and support.');
CALL _seed_std('STUDENT_AFFAIRS', 2, 'Activities & Welfare', 'Organization of student activities and welfare programs.');
CALL _seed_std('STUDENT_AFFAIRS', 3, 'Responsiveness', 'Responsiveness to student concerns.');
CALL _seed_std('STUDENT_AFFAIRS', 4, 'Discipline & Conduct', 'Promotion of discipline and positive conduct.');
CALL _seed_std('STUDENT_AFFAIRS', 5, 'Collaboration', 'Collaboration with academic and administrative units.');

-- Procurement Office
CALL _seed_std('PROCUREMENT', 1, 'Procurement Compliance', 'Compliance with procurement laws, policies, and regulations.');
CALL _seed_std('PROCUREMENT', 2, 'Transparent Processes', 'Transparent and accountable procurement processes.');
CALL _seed_std('PROCUREMENT', 3, 'Timely Acquisition', 'Timely acquisition of goods and services.');
CALL _seed_std('PROCUREMENT', 4, 'Vendor Management', 'Effective vendor and supplier management.');
CALL _seed_std('PROCUREMENT', 5, 'Procurement Records', 'Preparation and maintenance of procurement records and reports.');
CALL _seed_std('PROCUREMENT', 6, 'Value for Money', 'Contribution to cost efficiency and value-for-money purchasing.');
CALL _seed_std('PROCUREMENT', 7, 'Integrity', 'Integrity and avoidance of conflict of interest.');
CALL _seed_std('PROCUREMENT', 8, 'Collaboration', 'Collaboration with finance and administrative units.');
CALL _seed_std('PROCUREMENT', 9, 'Procurement Planning', 'Support for strategic plan implementation through procurement planning.');
CALL _seed_std('PROCUREMENT', 10, 'Customer Care', 'Customer care in handling supplier and departmental requests.');

-- ICT Office (the annex's last three lines repeat items 2, 5 and 6 and are folded in)
CALL _seed_std('ICT', 1, 'System Reliability', 'System reliability and uptime management.');
CALL _seed_std('ICT', 2, 'Technical Support', 'Timely troubleshooting and technical support for staff and students.');
CALL _seed_std('ICT', 3, 'Data Security & Compliance', 'Data security, privacy, and compliance with ICT policies.');
CALL _seed_std('ICT', 4, 'Infrastructure Maintenance', 'Maintenance and upgrading of ICT infrastructure.');
CALL _seed_std('ICT', 5, 'Digital Innovation', 'Innovation in digital learning and e-governance tools.');
CALL _seed_std('ICT', 6, 'E-learning Platforms', 'Effective management of e-learning platforms and online resources.');
CALL _seed_std('ICT', 7, 'Training & Capacity Building', 'Training and capacity building for staff and students in ICT use (user support and training).');
CALL _seed_std('ICT', 8, 'ICT Documentation', 'Documentation and record keeping of ICT systems and licenses.');
CALL _seed_std('ICT', 9, 'Digital Transformation', 'Collaboration with academic and administrative units to support digital transformation.');
CALL _seed_std('ICT', 10, 'Integrity & Accountability', 'Integrity and accountability in ICT resource management.');

-- Human Resource Office
CALL _seed_std('HR', 1, 'Recruitment & Selection', 'Recruitment and selection processes conducted fairly and transparently.');
CALL _seed_std('HR', 2, 'HR Policies & Manuals', 'Development and implementation of HR policies and manuals.');
CALL _seed_std('HR', 3, 'Labour Law Compliance', 'Adherence to labor laws and institutional regulations.');
CALL _seed_std('HR', 4, 'Workforce Planning', 'Support for strategic plan implementation through workforce planning.');
CALL _seed_std('HR', 5, 'Workplace Culture', 'Promotion of teamwork, collaboration, and positive workplace culture.');
CALL _seed_std('HR', 6, 'Staff Welfare & Queries', 'Customer care in handling staff queries and welfare issues.');
CALL _seed_std('HR', 7, 'Staff Meetings & Training', 'Staff Meetings and training programs.');
CALL _seed_std('HR', 8, 'Performance Appraisal', 'Performance appraisal coordination and reporting.');
CALL _seed_std('HR', 9, 'Employee Relations', 'Employee relations management and conflict resolution.');
CALL _seed_std('HR', 10, 'Confidentiality of Records', 'Confidentiality and integrity in handling staff records.');

-- Audit Office
CALL _seed_std('AUDIT', 1, 'Audit Standards Compliance', 'Compliance with internal and external audit standards.');
CALL _seed_std('AUDIT', 2, 'Financial Auditing', 'Regular auditing of financial records and transactions.');
CALL _seed_std('AUDIT', 3, 'Procurement & Expenditure Verification', 'Verification of procurement and expenditure processes.');
CALL _seed_std('AUDIT', 4, 'Audit Reports', 'Preparation of accurate audit reports and recommendations.');
CALL _seed_std('AUDIT', 5, 'Risk & Internal Controls', 'Monitoring of risk management and internal controls.');
CALL _seed_std('AUDIT', 6, 'Independence & Integrity', 'Integrity, independence, and accountability in audit practices.');
CALL _seed_std('AUDIT', 7, 'Collaboration', 'Collaboration with finance, bursar, and procurement offices.');
CALL _seed_std('AUDIT', 8, 'Governance Support', 'Support for governance committees with audit findings.');
CALL _seed_std('AUDIT', 9, 'Reporting Irregularities', 'Transparency in reporting irregularities and corrective measures.');
CALL _seed_std('AUDIT', 10, 'Institutional Efficiency', 'Contribution to institutional efficiency and accountability.');

-- Library Staff
CALL _seed_std('LIBRARY', 1, 'Cataloguing & Resources', 'Efficient cataloging and resource management.');
CALL _seed_std('LIBRARY', 2, 'Research Support', 'Support for student and faculty research needs.');
CALL _seed_std('LIBRARY', 3, 'Information Literacy', 'Promotion of information literacy.');
CALL _seed_std('LIBRARY', 4, 'Collections', 'Updating digital and physical collections.');
CALL _seed_std('LIBRARY', 5, 'Study Environment', 'Maintenance of conducive study environment.');

-- Research Staff
CALL _seed_std('RESEARCH', 1, 'Ethical & Impactful Research', 'Conducting ethical and impact research.');
CALL _seed_std('RESEARCH', 2, 'Research Reports', 'Timely submission of research reports.');
CALL _seed_std('RESEARCH', 3, 'Collaboration', 'Collaboration with faculty and students.');
CALL _seed_std('RESEARCH', 4, 'Grants & Funding', 'Securing grants and external funding.');
CALL _seed_std('RESEARCH', 5, 'Publication', 'Publication in recognized journals.');

DROP PROCEDURE IF EXISTS _seed_std;

-- department -> standards group (administrative offices only; academic
-- departments fall back to LECTURERS for academic staff)
UPDATE hrm_departments d JOIN appraisal_standard_groups g ON g.group_code = CASE d.ID
        WHEN 36 THEN 'REGISTRAR'        -- ACADEMIC REGISTRAR
        WHEN 24 THEN 'FINANCE'          -- UNIVERSITY BURSAR
        WHEN 20 THEN 'MARKETING'        -- MARKETING DEPARTMENT
        WHEN 35 THEN 'INTERNATIONAL'    -- INTERNATIONAL RELATIONS
        WHEN 31 THEN 'STUDENT_AFFAIRS'  -- DEAN OF STUDENTS
        WHEN 27 THEN 'STUDENT_AFFAIRS'  -- DEPUTY DEAN OF STUDENTS
        WHEN 16 THEN 'PROCUREMENT'      -- STORES
        WHEN 34 THEN 'ICT'              -- INFORMATION COMMUNICATIONS TECHNOLOGY
        WHEN 12 THEN 'HR'               -- HUMAN RESOURCE
        WHEN 37 THEN 'LIBRARY'          -- LIBRARY
     END
SET d.standards_group_id = g.group_id;

-- ---------------------------------------------------------------------------
-- 4. Shared routines (single source of truth for both apps)
-- ---------------------------------------------------------------------------
DROP FUNCTION IF EXISTS appraisal_resolve_reviewer;
DROP FUNCTION IF EXISTS appraisal_classify;
DROP FUNCTION IF EXISTS appraisal_resolve_group;
DROP PROCEDURE IF EXISTS appraisal_recalc;

DELIMITER $$

-- Reviewer rule: hrm_employee.reviewer_id -> supervisorID -> head of
-- hrm_employee.dept_id -> head of the department on the latest VALID contract.
-- Never the employee themselves. NULL = HR must assign.
CREATE FUNCTION appraisal_resolve_reviewer(p_emp INT) RETURNS INT
    READS SQL DATA
BEGIN
    DECLARE v INT DEFAULT NULL;
    DECLARE done INT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;

    SELECT CASE WHEN IFNULL(reviewer_id,0)  > 0 AND reviewer_id  <> p_emp THEN reviewer_id
                WHEN IFNULL(supervisorID,0) > 0 AND supervisorID <> p_emp THEN supervisorID
           END
      INTO v FROM hrm_employee WHERE empID = p_emp LIMIT 1;

    IF v IS NULL THEN
        SELECT d.dept_headID INTO v
          FROM hrm_employee e JOIN hrm_departments d ON d.ID = e.dept_id
         WHERE e.empID = p_emp AND IFNULL(d.dept_headID,0) > 0 AND d.dept_headID <> p_emp
         LIMIT 1;
    END IF;

    IF v IS NULL THEN
        SELECT d.dept_headID INTO v
          FROM hrm_emp_contracts c JOIN hrm_departments d ON d.ID = c.departmentID
         WHERE c.empID = p_emp AND c.contractStatus = 'VALID'
           AND IFNULL(d.dept_headID,0) > 0 AND d.dept_headID <> p_emp
         ORDER BY c.contractEnd DESC, c.ID DESC
         LIMIT 1;
    END IF;

    IF v IS NOT NULL AND (SELECT COUNT(*) FROM hrm_employee WHERE empID = v) = 0 THEN
        SET v = NULL;
    END IF;
    RETURN v;
END$$

-- Expected-standards group for an employee + staff category.
-- Department mapping wins when it suits the category; else LECTURERS for
-- academic staff, ADMIN_GENERAL for administrative staff, NULL for support.
CREATE FUNCTION appraisal_resolve_group(p_emp INT, p_cat VARCHAR(20)) RETURNS INT
    READS SQL DATA
BEGIN
    DECLARE v INT DEFAULT NULL;
    DECLARE done INT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;

    IF p_cat = 'SUPPORT' THEN RETURN NULL; END IF;

    SELECT g.group_id INTO v
      FROM hrm_employee e
      JOIN hrm_departments d ON d.ID = e.dept_id
      JOIN appraisal_standard_groups g ON g.group_id = d.standards_group_id AND g.is_active = 1
     WHERE e.empID = p_emp AND (g.applies_to = p_cat OR g.applies_to = 'ANY')
     LIMIT 1;

    IF v IS NULL THEN
        SELECT g.group_id INTO v
          FROM hrm_emp_contracts c
          JOIN hrm_departments d ON d.ID = c.departmentID
          JOIN appraisal_standard_groups g ON g.group_id = d.standards_group_id AND g.is_active = 1
         WHERE c.empID = p_emp AND c.contractStatus = 'VALID'
           AND (g.applies_to = p_cat OR g.applies_to = 'ANY')
         ORDER BY c.contractEnd DESC, c.ID DESC
         LIMIT 1;
    END IF;

    IF v IS NULL THEN
        SELECT group_id INTO v FROM appraisal_standard_groups
         WHERE is_active = 1
           AND group_code = CASE WHEN p_cat = 'ACADEMIC' THEN 'LECTURERS' ELSE 'ADMIN_GENERAL' END
         LIMIT 1;
    END IF;
    RETURN v;
END$$

-- One classification scale, used everywhere
CREATE FUNCTION appraisal_classify(p_pct DECIMAL(6,2)) RETURNS VARCHAR(50)
    DETERMINISTIC
BEGIN
    RETURN CASE
        WHEN p_pct IS NULL THEN NULL
        WHEN p_pct >= 90 THEN 'Exceptional'
        WHEN p_pct >= 75 THEN 'Above Expectations'
        WHEN p_pct >= 60 THEN 'Satisfactory'
        WHEN p_pct >= 50 THEN 'Development Needed'
        ELSE 'Unsatisfactory'
    END;
END$$

-- One score formula. Every applicable Section B row (has a KPA and is not
-- N/A) and every applicable Section C competency counts towards the maximum,
-- so unrated rows lower the score instead of disappearing from it.
CREATE PROCEDURE appraisal_recalc(IN p_rid INT)
BEGIN
    DECLARE v_bself DECIMAL(7,2);
    DECLARE v_bsup  DECIMAL(7,2);
    DECLARE v_c     DECIMAL(7,2);
    DECLARE v_max   DECIMAL(7,2);
    DECLARE v_pct   DECIMAL(6,2);

    SELECT IFNULL(SUM(self_rating),0),
           IFNULL(SUM(supervisor_rating),0),
           COUNT(*) * 5
      INTO v_bself, v_bsup, v_max
      FROM appraisal_section_b
     WHERE record_id = p_rid AND is_na = 0 AND TRIM(IFNULL(agreed_output,'')) <> '';

    SELECT IFNULL(SUM(rating),0), v_max + COUNT(*) * 5
      INTO v_c, v_max
      FROM appraisal_section_c
     WHERE record_id = p_rid AND IFNULL(is_na,0) = 0;

    SET v_pct = CASE WHEN v_max > 0 THEN ROUND((v_bsup + v_c) / v_max * 100, 2) ELSE NULL END;

    UPDATE appraisal_records
       SET section_b_self_total       = v_bself,
           section_b_supervisor_total = v_bsup,
           section_c_total            = v_c,
           raw_score                  = v_bsup + v_c,
           max_possible               = v_max,
           final_percentage           = v_pct,
           classification             = appraisal_classify(v_pct)
     WHERE record_id = p_rid;
END$$

DELIMITER ;
