-- ---------------------------------------------------------------------------
-- Student Disciplinary module: schema (database campus_dynamics)
-- File: COOPERP/sql/discipline/2026-10_disciplinary_schema.sql
-- New tables, guard triggers, the restriction functions and the nightly expiry
-- event. Touches no existing table. Safe to re-run. Run with the mysql client.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dc_settings (
  setting_key    VARCHAR(60)  NOT NULL,
  setting_value  VARCHAR(1000) NOT NULL,
  description    VARCHAR(300) NULL,
  updated_by     VARCHAR(100) NULL,
  updated_at     DATETIME     NULL,
  PRIMARY KEY (setting_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

INSERT IGNORE INTO dc_settings (setting_key, setting_value, description, updated_by, updated_at) VALUES
 ('appeal_window_days',      '14', 'Days a student has to appeal, counted from the day the decision is notified to them', 'install', NOW()),
 ('overdue_no_update_days',  '14', 'An open case with no entry for this many days is overdue', 'install', NOW()),
 ('appeal_closing_days',     '3',  'Warn when an appeal window closes within this many days', 'install', NOW()),
 ('contact_office',          'Office of the Dean of Students', 'Shown to a student whose portal access is restricted', 'install', NOW()),
 ('contact_details',         'deanofstudents@mru.ac.ug', 'Contact line shown with the office', 'install', NOW()),
 ('appellate_authority',     'University Appeals Committee', 'Name of the body that decides appeals, printed on letters', 'install', NOW()),
 ('letter_office',           'Office of the Academic Registrar', 'Office line on disciplinary letters', 'install', NOW()),
 ('summon_notice_days',      '3',  'Minimum days between a summons and the hearing, unless a reason for short notice is recorded', 'install', NOW()),
 ('attachment_max_mb',       '15', 'Largest file that can be attached to a case, in MB', 'install', NOW());

CREATE TABLE IF NOT EXISTS dc_case_type (
  id              INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  code            VARCHAR(20)       NOT NULL,
  name            VARCHAR(150)      NOT NULL,
  description     VARCHAR(600)      NULL,
  severity        ENUM('MINOR','SERIOUS','GROSS') NOT NULL DEFAULT 'SERIOUS',
  is_exam_related TINYINT(1)        NOT NULL DEFAULT 0,
  default_restricted TINYINT(1)     NOT NULL DEFAULT 0,
  sort_order      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active       TINYINT(1)        NOT NULL DEFAULT 1,
  created_by      VARCHAR(100)      NOT NULL,
  created_at      DATETIME          NOT NULL,
  updated_by      VARCHAR(100)      NULL,
  updated_at      DATETIME          NULL,
  row_version     INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_case_type_code (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_sanction_type (
  id              INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  code            VARCHAR(20)       NOT NULL,
  name            VARCHAR(150)      NOT NULL,
  description     VARCHAR(600)      NULL,
  outcome         ENUM('SANCTION','DISMISSAL','ACQUITTAL') NOT NULL DEFAULT 'SANCTION',
  effects         SET('PORTAL_BLOCK','RESULTS_WITHHELD','SUSPENSION','EXPULSION','GRADUATION_BAR',
                      'CANCEL_PAPER','CANCEL_SEMESTER','FINE','RESTITUTION') NOT NULL DEFAULT '',
  needs_amount    TINYINT(1)        NOT NULL DEFAULT 0,
  needs_dates     ENUM('NONE','FROM','FROM_TO') NOT NULL DEFAULT 'NONE',
  needs_course    TINYINT(1)        NOT NULL DEFAULT 0,
  needs_semester  TINYINT(1)        NOT NULL DEFAULT 0,
  allowed_interim TINYINT(1)        NOT NULL DEFAULT 0,
  sort_order      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active       TINYINT(1)        NOT NULL DEFAULT 1,
  created_by      VARCHAR(100)      NOT NULL,
  created_at      DATETIME          NOT NULL,
  updated_by      VARCHAR(100)      NULL,
  updated_at      DATETIME          NULL,
  row_version     INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_sanction_type_code (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_case_type_sanction (
  case_type_id     INT UNSIGNED      NOT NULL,
  sanction_type_id INT UNSIGNED      NOT NULL,
  sort_order       SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active        TINYINT(1)        NOT NULL DEFAULT 1,      -- pairings are switched off, never deleted
  PRIMARY KEY (case_type_id, sanction_type_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_committee_member (
  id           INT UNSIGNED NOT NULL AUTO_INCREMENT,
  acad_year    VARCHAR(9)   NOT NULL,
  emp_id       INT UNSIGNED NULL,
  username     VARCHAR(100) NULL,
  member_name  VARCHAR(150) NOT NULL,
  panel_role   ENUM('CHAIR','MEMBER','SECRETARY','STUDENT_REP','APPELLATE') NOT NULL DEFAULT 'MEMBER',
  sort_order   SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active    TINYINT(1)   NOT NULL DEFAULT 1,
  created_by   VARCHAR(100) NOT NULL,
  created_at   DATETIME     NOT NULL,
  updated_by   VARCHAR(100) NULL,
  updated_at   DATETIME     NULL,
  PRIMARY KEY (id),
  KEY ix_dc_member_year (acad_year, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_letter_template (
  id           INT UNSIGNED NOT NULL AUTO_INCREMENT,
  code         VARCHAR(20)  NOT NULL,
  name         VARCHAR(150) NOT NULL,
  subject      VARCHAR(250) NOT NULL,
  body         TEXT         NOT NULL,          -- plain paragraphs with {{merge_fields}}
  student_copy TINYINT(1)   NOT NULL DEFAULT 1,
  sort_order   SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active    TINYINT(1)   NOT NULL DEFAULT 1,
  created_by   VARCHAR(100) NOT NULL,
  created_at   DATETIME     NOT NULL,
  updated_by   VARCHAR(100) NULL,
  updated_at   DATETIME     NULL,
  row_version  INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_letter_template_code (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_sequence (
  seq_key   VARCHAR(40)  NOT NULL,           -- CASE:2026-27, INCIDENT:2026-27
  next_no   INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (seq_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_incident (
  id                  INT UNSIGNED NOT NULL AUTO_INCREMENT,
  incident_no         VARCHAR(30)  NOT NULL,                  -- DI/2026-27/0007
  case_type_id        INT UNSIGNED NOT NULL,
  occurred_at         DATETIME     NOT NULL,
  place               VARCHAR(200) NULL,
  campus_id           INT          NULL,
  description         TEXT         NOT NULL,
  reported_by         VARCHAR(100) NOT NULL,                  -- username of the reporting officer
  reported_by_emp_id  INT UNSIGNED NULL,
  reporter_name       VARCHAR(150) NULL,
  witnesses           TEXT         NULL,
  complaint_ticket_id INT          NULL,                      -- campus_dynamics_portal.support_tickets
  exam_timetable_id   INT UNSIGNED NULL,                      -- acad_exam_timetable.ID
  course_code         VARCHAR(25)  NULL,
  exam_acad_year      VARCHAR(9)   NULL,
  exam_semester       TINYINT UNSIGNED NULL,
  invigilator_emp_id  INT UNSIGNED NULL,
  created_by          VARCHAR(100) NOT NULL,
  created_at          DATETIME     NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_incident_no (incident_no),
  KEY ix_dc_incident_ticket (complaint_ticket_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_case (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_no          VARCHAR(30)  NOT NULL,                     -- DC/2026-27/0042
  incident_id      INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  student_name     VARCHAR(200) NOT NULL,                     -- as at opening
  progcode         VARCHAR(25)  NULL,
  faculty_code     VARCHAR(10)  NULL,
  department_id    INT          NULL,
  campus_id        INT          NULL,
  study_year       TINYINT UNSIGNED NULL,
  acad_year        VARCHAR(9)   NOT NULL,                     -- 2026/2027
  case_type_id     INT UNSIGNED NOT NULL,
  severity         ENUM('MINOR','SERIOUS','GROSS') NOT NULL,
  status           ENUM('REPORTED','UNDER_INVESTIGATION','HEARING_SCHEDULED','SUMMONED','HEARD','DECIDED',
                        'UNDER_APPEAL','APPEAL_DECIDED','CLOSED','WITHDRAWN') NOT NULL DEFAULT 'REPORTED',
  is_restricted    TINYINT(1)   NOT NULL DEFAULT 0,
  reported_by      VARCHAR(100) NOT NULL,
  case_officer     VARCHAR(100) NULL,
  hearing_at       DATETIME     NULL,                         -- next or last hearing
  hearing_venue    VARCHAR(200) NULL,
  decided_at       DATE         NULL,
  decision_summary VARCHAR(1000) NULL,
  notified_at      DATETIME     NULL,                         -- decision notified to the student
  appeal_deadline  DATE         NULL,
  closed_at        DATETIME     NULL,
  last_entry_at    DATETIME     NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  updated_by       VARCHAR(100) NULL,
  updated_at       DATETIME     NULL,
  row_version      INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_case_no (case_no),
  KEY ix_dc_case_regno (regno, status),
  KEY ix_dc_case_status (status, last_entry_at),
  KEY ix_dc_case_year (acad_year, status),
  KEY ix_dc_case_incident (incident_id),
  KEY ix_dc_case_type (case_type_id),
  KEY ix_dc_case_faculty (faculty_code, department_id),
  KEY ix_dc_case_hearing (hearing_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- The case file: append-only.
CREATE TABLE IF NOT EXISTS dc_entry (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  entry_type       ENUM('CASE_OPENED','NOTE','FINDING','STATEMENT','EVIDENCE','INTERIM_MEASURE','HEARING_SCHEDULED',
                        'SUMMON','ADJOURNED','HEARING_HELD','DECISION','APPEAL_LODGED','APPEAL_DECISION',
                        'SANCTION_VARIED','SANCTION_LIFTED','BLOCK_APPLIED','BLOCK_LIFTED','STATUS_CHANGE',
                        'STUDENT_NOTIFIED','LETTER_ISSUED','MARKS_ACTION','FEES_ACTION','CORRECTION',
                        'CASE_CLOSED','CASE_WITHDRAWN','SYSTEM') NOT NULL,
  entry_at         DATETIME     NOT NULL,                     -- when it happened
  title            VARCHAR(250) NOT NULL,
  body             TEXT         NULL,
  details_json     TEXT         NULL,
  student_visible  TINYINT(1)   NOT NULL DEFAULT 0,
  status_before    VARCHAR(30)  NULL,
  status_after     VARCHAR(30)  NULL,
  sanction_id      INT UNSIGNED NULL,
  corrects_entry_id INT UNSIGNED NULL,
  recorded_by      VARCHAR(100) NOT NULL,
  recorded_role    VARCHAR(40)  NULL,
  recorded_at      DATETIME     NOT NULL,
  interface        ENUM('EADMIN','EPORTAL','SYSTEM') NOT NULL,
  ip_address       VARCHAR(45)  NULL,
  client_op_id     VARCHAR(40)  NULL,
  PRIMARY KEY (id),
  KEY ix_dc_entry_case (case_id, entry_at, id),
  KEY ix_dc_entry_type (entry_type, recorded_at),
  KEY ix_dc_entry_recorded (recorded_at),
  KEY ix_dc_entry_sanction (sanction_id),
  UNIQUE KEY uq_dc_entry_op (client_op_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_attachment (
  id              INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id         INT UNSIGNED NOT NULL,
  entry_id        INT UNSIGNED NULL,
  kind            ENUM('STATEMENT','PHOTO','SCRIPT','SCREENSHOT','MINUTES','LETTER','APPEAL','EVIDENCE','OTHER') NOT NULL DEFAULT 'OTHER',
  description     VARCHAR(300) NULL,
  original_name   VARCHAR(200) NOT NULL,
  stored_name     VARCHAR(120) NOT NULL,
  mime            VARCHAR(100) NOT NULL,
  size_bytes      INT UNSIGNED NOT NULL,
  sha1            CHAR(40)     NOT NULL,
  student_visible TINYINT(1)   NOT NULL DEFAULT 0,
  uploaded_by     VARCHAR(100) NOT NULL,
  uploaded_via    ENUM('EADMIN','EPORTAL','SYSTEM') NOT NULL,
  uploaded_at     DATETIME     NOT NULL,
  is_active       TINYINT(1)   NOT NULL DEFAULT 1,
  removed_by      VARCHAR(100) NULL,
  removed_at      DATETIME     NULL,
  removed_reason  VARCHAR(500) NULL,
  PRIMARY KEY (id),
  KEY ix_dc_att_case (case_id, is_active),
  KEY ix_dc_att_entry (entry_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_hearing (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  scheduled_at     DATETIME     NOT NULL,
  venue            VARCHAR(200) NOT NULL,
  panel            VARCHAR(1000) NULL,
  status           ENUM('SCHEDULED','ADJOURNED','HELD','CANCELLED') NOT NULL DEFAULT 'SCHEDULED',
  student_attended TINYINT(1)   NULL,
  scheduled_entry_id INT UNSIGNED NULL,
  outcome_entry_id INT UNSIGNED NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  updated_by       VARCHAR(100) NULL,
  updated_at       DATETIME     NULL,
  PRIMARY KEY (id),
  KEY ix_dc_hearing_case (case_id),
  KEY ix_dc_hearing_when (scheduled_at, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Sanctions and interim measures. Terms are never edited: a variation ends the old row and adds a new one.
CREATE TABLE IF NOT EXISTS dc_sanction (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  sanction_type_id INT UNSIGNED NOT NULL,
  effects          SET('PORTAL_BLOCK','RESULTS_WITHHELD','SUSPENSION','EXPULSION','GRADUATION_BAR',
                       'CANCEL_PAPER','CANCEL_SEMESTER','FINE','RESTITUTION') NOT NULL DEFAULT '',
  source           ENUM('INTERIM','DECISION','APPEAL') NOT NULL,
  amount           DECIMAL(14,2) NULL,
  starts_on        DATE         NOT NULL,
  ends_on          DATE         NULL,
  course_code      VARCHAR(25)  NULL,
  acad_year        VARCHAR(9)   NULL,
  semester         TINYINT UNSIGNED NULL,
  terms            VARCHAR(1000) NULL,
  status           ENUM('ACTIVE','LIFTED','EXPIRED','VARIED','SET_ASIDE') NOT NULL DEFAULT 'ACTIVE',
  follow_up        ENUM('NONE','PENDING','DONE') NOT NULL DEFAULT 'NONE',   -- marks or fees action owed
  follow_up_ref    VARCHAR(150) NULL,
  applied_entry_id INT UNSIGNED NULL,
  ended_entry_id   INT UNSIGNED NULL,
  ended_at         DATETIME     NULL,
  ended_by         VARCHAR(100) NULL,
  ended_reason     VARCHAR(1000) NULL,
  replaces_id      INT UNSIGNED NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_sanction_regno (regno, status, starts_on, ends_on),
  KEY ix_dc_sanction_case (case_id),
  KEY ix_dc_sanction_due (status, ends_on),
  KEY ix_dc_sanction_follow (follow_up)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_appeal (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  lodged_at        DATETIME     NOT NULL,
  lodged_via       ENUM('EADMIN','EPORTAL') NOT NULL,
  lodged_by        VARCHAR(100) NOT NULL,
  grounds          TEXT         NOT NULL,
  within_window    TINYINT(1)   NOT NULL,
  late_reason      VARCHAR(1000) NULL,
  status           ENUM('LODGED','UPHELD','DISMISSED','VARIED','WITHDRAWN') NOT NULL DEFAULT 'LODGED',
  decided_at       DATE         NULL,
  decided_by       VARCHAR(100) NULL,
  decision_text    TEXT         NULL,
  lodged_entry_id  INT UNSIGNED NULL,
  decision_entry_id INT UNSIGNED NULL,
  PRIMARY KEY (id),
  KEY ix_dc_appeal_case (case_id),
  KEY ix_dc_appeal_status (status, lodged_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_letter (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  letter_no        VARCHAR(40)  NOT NULL,                    -- DC/2026-27/0042/L2
  template_code    VARCHAR(20)  NOT NULL,
  subject          VARCHAR(250) NOT NULL,
  body             TEXT         NOT NULL,                    -- the merged text exactly as issued
  stored_name      VARCHAR(120) NOT NULL,
  sha1             CHAR(40)     NOT NULL,
  student_visible  TINYINT(1)   NOT NULL DEFAULT 1,
  issued_by        VARCHAR(100) NOT NULL,
  issued_at        DATETIME     NOT NULL,
  entry_id         INT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_letter_no (letter_no),
  KEY ix_dc_letter_case (case_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_notification (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  kind             ENUM('CASE_OPENED','SUMMON','DECISION','APPEAL_RECEIVED','APPEAL_OUTCOME','SANCTION_LIFTED',
                        'BLOCK_APPLIED','BLOCK_LIFTED','LETTER','OTHER') NOT NULL,
  title            VARCHAR(200) NOT NULL,
  message          VARCHAR(2000) NOT NULL,
  letter_id        INT UNSIGNED NULL,
  portal_read_at   DATETIME     NULL,
  email_to         VARCHAR(200) NULL,
  email_status     ENUM('PENDING','SENT','FAILED','NO_ADDRESS','NOT_SENT') NOT NULL DEFAULT 'PENDING',
  email_attempts   TINYINT UNSIGNED NOT NULL DEFAULT 0,
  email_error      VARCHAR(500) NULL,
  email_sent_at    DATETIME     NULL,
  entry_id         INT UNSIGNED NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_note_regno (regno, portal_read_at),
  KEY ix_dc_note_email (email_status, email_attempts),
  KEY ix_dc_note_case (case_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_audit (
  id           INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  entity       ENUM('CASE','ENTRY','SANCTION','APPEAL','HEARING','LETTER','ATTACHMENT','NOTIFICATION','SETTING',
                    'CASE_TYPE','SANCTION_TYPE','TEMPLATE','MEMBER','INCIDENT') NOT NULL,
  entity_id    INT UNSIGNED  NOT NULL,
  case_id      INT UNSIGNED  NULL,
  action       VARCHAR(40)   NOT NULL,
  before_json  TEXT          NULL,
  after_json   TEXT          NULL,
  reason       VARCHAR(1000) NULL,
  actor        VARCHAR(100)  NOT NULL,
  actor_role   VARCHAR(40)   NULL,
  interface    ENUM('EADMIN','EPORTAL','SYSTEM') NOT NULL,
  ip_address   VARCHAR(45)   NULL,
  created_at   DATETIME      NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_audit_case (case_id, created_at),
  KEY ix_dc_audit_entity (entity, entity_id),
  KEY ix_dc_audit_actor (actor, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Who opened a restricted case file, and when (read access log for confidential cases).
CREATE TABLE IF NOT EXISTS dc_access_log (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id     INT UNSIGNED NOT NULL,
  viewer      VARCHAR(100) NOT NULL,
  viewer_role VARCHAR(40)  NULL,
  what        VARCHAR(40)  NOT NULL,                     -- VIEW, EXPORT, LETTER, ATTACHMENT
  ip_address  VARCHAR(45)  NULL,
  viewed_at   DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_access_case (case_id, viewed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- ── Guard triggers ─────────────────────────────────────────────────────────
DROP TRIGGER IF EXISTS trg_dc_entry_bd;
DROP TRIGGER IF EXISTS trg_dc_entry_bu;
DROP TRIGGER IF EXISTS trg_dc_case_bd;
DROP TRIGGER IF EXISTS trg_dc_sanction_bd;
DROP TRIGGER IF EXISTS trg_dc_sanction_bu;
DROP TRIGGER IF EXISTS trg_dc_audit_bd;
DROP TRIGGER IF EXISTS trg_dc_audit_bu;
DROP TRIGGER IF EXISTS trg_dc_letter_bd;
DROP TRIGGER IF EXISTS trg_dc_letter_bu;
DROP TRIGGER IF EXISTS trg_dc_appeal_bd;
DROP TRIGGER IF EXISTS trg_dc_incident_bd;
DROP TRIGGER IF EXISTS trg_dc_attachment_bd;
DROP TRIGGER IF EXISTS trg_dc_notification_bd;
DROP TRIGGER IF EXISTS trg_dc_access_bd;

DELIMITER $$
CREATE TRIGGER trg_dc_entry_bd BEFORE DELETE ON dc_entry FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_entry is append-only: add a correction entry instead'; END$$
CREATE TRIGGER trg_dc_entry_bu BEFORE UPDATE ON dc_entry FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_entry is append-only: add a correction entry instead'; END$$
CREATE TRIGGER trg_dc_case_bd BEFORE DELETE ON dc_case FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cases are never deleted: withdraw or close them'; END$$
CREATE TRIGGER trg_dc_sanction_bd BEFORE DELETE ON dc_sanction FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Sanctions are never deleted: lift or vary them'; END$$
CREATE TRIGGER trg_dc_sanction_bu BEFORE UPDATE ON dc_sanction FOR EACH ROW
BEGIN
  -- The terms of a sanction never change; only its ending and follow-up may be recorded.
  IF NEW.case_id <> OLD.case_id OR NEW.regno <> OLD.regno OR NEW.sanction_type_id <> OLD.sanction_type_id
     OR NEW.effects <> OLD.effects OR NEW.source <> OLD.source OR NOT (NEW.amount <=> OLD.amount)
     OR NEW.starts_on <> OLD.starts_on OR NOT (NEW.ends_on <=> OLD.ends_on) OR NOT (NEW.course_code <=> OLD.course_code)
     OR NOT (NEW.acad_year <=> OLD.acad_year) OR NOT (NEW.semester <=> OLD.semester) OR NOT (NEW.terms <=> OLD.terms)
     OR NEW.created_by <> OLD.created_by OR NEW.created_at <> OLD.created_at THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Sanction terms cannot be edited: vary the sanction instead';
  END IF;
  IF OLD.status <> 'ACTIVE' AND NEW.status <> OLD.status THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'An ended sanction cannot be reactivated';
  END IF;
END$$
CREATE TRIGGER trg_dc_audit_bd BEFORE DELETE ON dc_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_audit is append-only'; END$$
CREATE TRIGGER trg_dc_audit_bu BEFORE UPDATE ON dc_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_audit is append-only'; END$$
CREATE TRIGGER trg_dc_letter_bd BEFORE DELETE ON dc_letter FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Issued letters are never deleted'; END$$
CREATE TRIGGER trg_dc_letter_bu BEFORE UPDATE ON dc_letter FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Issued letters are never edited: issue a new letter'; END$$
CREATE TRIGGER trg_dc_appeal_bd BEFORE DELETE ON dc_appeal FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Appeals are never deleted'; END$$
CREATE TRIGGER trg_dc_incident_bd BEFORE DELETE ON dc_incident FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Incidents are never deleted'; END$$
CREATE TRIGGER trg_dc_attachment_bd BEFORE DELETE ON dc_attachment FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Attachments are never deleted: remove hides them'; END$$
CREATE TRIGGER trg_dc_notification_bd BEFORE DELETE ON dc_notification FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Notifications are never deleted'; END$$
CREATE TRIGGER trg_dc_access_bd BEFORE DELETE ON dc_access_log FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_access_log is append-only'; END$$
DELIMITER ;

-- ── The single source of truth for restrictions ────────────────────────────
-- Both eadmin and eportal, the registration triggers and the clearance gates call these.
-- A restriction is in force from starts_on to ends_on inclusive; no job is needed for it to end.
DROP FUNCTION IF EXISTS dc_effect_active;
DROP FUNCTION IF EXISTS dc_open_case_count;
DROP FUNCTION IF EXISTS dc_restriction_summary;
DELIMITER $$
CREATE FUNCTION dc_effect_active(p_regno VARCHAR(30), p_effect VARCHAR(20)) RETURNS TINYINT
READS SQL DATA
BEGIN
  DECLARE n INT DEFAULT 0;
  SELECT COUNT(*) INTO n FROM dc_sanction
   WHERE regno = p_regno AND status = 'ACTIVE' AND FIND_IN_SET(p_effect, effects) > 0
     AND starts_on <= CURDATE() AND (ends_on IS NULL OR ends_on >= CURDATE());
  RETURN IF(n > 0, 1, 0);
END$$
CREATE FUNCTION dc_open_case_count(p_regno VARCHAR(30)) RETURNS INT
READS SQL DATA
BEGIN
  DECLARE n INT DEFAULT 0;
  SELECT COUNT(*) INTO n FROM dc_case WHERE regno = p_regno AND status NOT IN ('CLOSED','WITHDRAWN');
  RETURN n;
END$$
-- Comma list of every effect in force, for example 'PORTAL_BLOCK,RESULTS_WITHHELD'; '' when none.
CREATE FUNCTION dc_restriction_summary(p_regno VARCHAR(30)) RETURNS VARCHAR(200)
READS SQL DATA
BEGIN
  DECLARE s VARCHAR(200) DEFAULT '';
  SELECT IFNULL(GROUP_CONCAT(DISTINCT effects ORDER BY effects SEPARATOR ','), '') INTO s FROM dc_sanction
   WHERE regno = p_regno AND status = 'ACTIVE' AND effects <> ''
     AND starts_on <= CURDATE() AND (ends_on IS NULL OR ends_on >= CURDATE());
  RETURN s;
END$$
DELIMITER ;

-- ── Nightly: sanctions past their end date are recorded as ended ───────────
DROP PROCEDURE IF EXISTS dc_expire_sanctions;
DELIMITER $$
CREATE PROCEDURE dc_expire_sanctions()
BEGIN
  DECLARE done INT DEFAULT 0;
  DECLARE v_id, v_case INT UNSIGNED;
  DECLARE v_regno VARCHAR(30);
  DECLARE v_effects VARCHAR(200);
  DECLARE v_name VARCHAR(150);
  DECLARE v_end DATE;
  DECLARE v_entry INT UNSIGNED;
  DECLARE cur CURSOR FOR
    SELECT s.id, s.case_id, s.regno, s.effects, t.name, s.ends_on
      FROM dc_sanction s JOIN dc_sanction_type t ON t.id = s.sanction_type_id
     WHERE s.status = 'ACTIVE' AND s.ends_on IS NOT NULL AND s.ends_on < CURDATE();
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;
  OPEN cur;
  lp: LOOP
    FETCH cur INTO v_id, v_case, v_regno, v_effects, v_name, v_end;
    IF done = 1 THEN LEAVE lp; END IF;
    START TRANSACTION;
    INSERT INTO dc_entry (case_id, entry_type, entry_at, title, body, student_visible, sanction_id, recorded_by, recorded_role, recorded_at, interface)
    VALUES (v_case, IF(FIND_IN_SET('PORTAL_BLOCK', v_effects) > 0, 'BLOCK_LIFTED', 'SANCTION_LIFTED'),
            NOW(), CONCAT(v_name, ' ended'), CONCAT('The sanction ended on its end date, ', DATE_FORMAT(v_end, '%e %b %Y'), '. Recorded automatically.'),
            1, v_id, 'system', 'system', NOW(), 'SYSTEM');
    SET v_entry = LAST_INSERT_ID();
    UPDATE dc_sanction SET status = 'EXPIRED', ended_at = NOW(), ended_by = 'system',
           ended_reason = 'End date reached', ended_entry_id = v_entry WHERE id = v_id AND status = 'ACTIVE';
    UPDATE dc_case SET last_entry_at = NOW() WHERE id = v_case;
    INSERT INTO dc_notification (case_id, regno, kind, title, message, email_status, entry_id, created_by, created_at)
    VALUES (v_case, v_regno, IF(FIND_IN_SET('PORTAL_BLOCK', v_effects) > 0, 'BLOCK_LIFTED', 'SANCTION_LIFTED'),
            CONCAT(v_name, ' has ended'), CONCAT('The ', LOWER(v_name), ' recorded against you ended on ', DATE_FORMAT(v_end, '%e %b %Y'), '.'),
            'PENDING', v_entry, 'system', NOW());
    INSERT INTO dc_audit (entity, entity_id, case_id, action, before_json, after_json, reason, actor, actor_role, interface, created_at)
    VALUES ('SANCTION', v_id, v_case, 'EXPIRE', '{"status":"ACTIVE"}', '{"status":"EXPIRED"}', 'End date reached', 'system', 'system', 'SYSTEM', NOW());
    COMMIT;
  END LOOP;
  CLOSE cur;
END$$
DELIMITER ;

DROP EVENT IF EXISTS ev_dc_expire_sanctions;
CREATE EVENT ev_dc_expire_sanctions ON SCHEDULE EVERY 1 DAY STARTS (CURRENT_DATE + INTERVAL 1 DAY + INTERVAL 10 MINUTE)
  ON COMPLETION PRESERVE ENABLE DO CALL dc_expire_sanctions();
