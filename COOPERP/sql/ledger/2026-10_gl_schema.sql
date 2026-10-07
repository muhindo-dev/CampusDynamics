-- ---------------------------------------------------------------------------
-- General Ledger rebuild: new tables (database campus_dynamics_accounts).
-- Plan: COOPERP/docs/expenditure-accounts-plan.md, section 2.
-- Additive only: creates gl_* tables, guards and seed rows. Alters, drops or updates no existing table.
-- Safe to re-run. Back up first (schema dump + gl_baseline_check.sql).
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS gl_settings (
  setting_key   VARCHAR(60)   NOT NULL,
  setting_value VARCHAR(500)  NOT NULL,
  description   VARCHAR(300)  NULL,
  updated_by    VARCHAR(100)  NULL,
  updated_at    DATETIME      NULL,
  PRIMARY KEY (setting_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT IGNORE INTO gl_settings (setting_key, setting_value, description, updated_by, updated_at) VALUES
 ('detection_interval_hours', '6',        'Run the warning detection automatically when the last run is older than this', 'install', NOW()),
 ('large_posting_floor',      '10000000', 'A posting is only flagged as unusually large when it is at least this amount (UGX)', 'install', NOW()),
 ('large_posting_sigma',      '4',        'Flag postings above the account average plus this many standard deviations', 'install', NOW()),
 ('pending_journal_days',     '30',       'Journals pending longer than this are flagged', 'install', NOW()),
 ('late_posting_grace_days',  '30',       'Postings recorded more than this many days after their year ended are flagged', 'install', NOW()),
 ('adjust_threshold',         '0',        'Adjusting entries at or above this amount need a second approver (0 = every entry needs one approver)', 'install', NOW());

-- Presentation-only mapping of ledger codes that are missing from the chart of accounts.
-- It never changes fin_subaccounts; reports read it so these lines are shown in the right section.
CREATE TABLE IF NOT EXISTS gl_account_map (
  accountcode   VARCHAR(30)  NOT NULL,
  account_name  VARCHAR(150) NOT NULL,
  category      ENUM('Assets','Liabilities','Equity','Income','Expense','Suspense','Unclassified') NOT NULL,
  subcategory   VARCHAR(100) NULL,
  is_provisional TINYINT(1)  NOT NULL DEFAULT 1,
  basis         VARCHAR(300) NULL,
  confirmed_by  VARCHAR(100) NULL,
  confirmed_at  DATETIME     NULL,
  created_by    VARCHAR(100) NOT NULL,
  created_at    DATETIME     NOT NULL,
  updated_by    VARCHAR(100) NULL,
  updated_at    DATETIME     NULL,
  PRIMARY KEY (accountcode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT IGNORE INTO gl_account_map (accountcode, account_name, category, subcategory, basis, created_by, created_at) VALUES
 ('AC6007', 'FUNCTIONAL FEES', 'Income', 'INCOME', 'Billing posts item 52 (Functional Fees) here: 10,549 lines', 'install', NOW()),
 ('AC6004', 'LATE REGISTRATION FEES', 'Income', 'INCOME', 'All 549 lines are late registration fees', 'install', NOW()),
 ('AC6016', 'LATE REGISTRATION AND RETAKE FEES', 'Income', 'INCOME', 'Late registration (273) and retake fees (63)', 'install', NOW()),
 ('AC6034', 'GRADUATION FEES', 'Income', 'INCOME', 'Graduation fee lines', 'install', NOW()),
 ('AC6037', 'OTHER STUDENT CHARGES', 'Income', 'INCOME', 'Late bursary renewal (156), transfer of campus (36)', 'install', NOW()),
 ('AC6032', 'EXEMPTION FEES', 'Income', 'INCOME', 'Exemption fee lines', 'install', NOW()),
 ('AC6009', 'ABSCONDMENT FEES', 'Income', 'INCOME', 'Abscondment and dead-semester fees', 'install', NOW()),
 ('AC6024', 'RESEARCH FEES', 'Income', 'INCOME', 'Research and treatise fees', 'install', NOW()),
 ('AC6019', 'LOST IDENTITY CARD FEES', 'Income', 'INCOME', 'Lost ID charges', 'install', NOW()),
 ('AC6038', 'SCHOOL PRACTICE FEES', 'Income', 'INCOME', 'School practice charge', 'install', NOW()),
 ('AC-RECONCILE-DIFF', 'RECONCILIATION DIFFERENCES (REPAIR PLUG)', 'Suspense', 'SUSPENSE', 'Offsetting lines written by repair scripts on 17 Mar 2026', 'install', NOW());

-- Every other code used on general-ledger lines but missing from the chart: unclassified until the Bursar maps it.
INSERT IGNORE INTO gl_account_map (accountcode, account_name, category, subcategory, basis, created_by, created_at)
SELECT l.accountcode, CONCAT('UNMAPPED CODE ', l.accountcode), 'Unclassified', 'UNCLASSIFIED',
       CONCAT(COUNT(*), ' lines, e.g. "', LEFT(MAX(l.particulars), 120), '"'), 'install', NOW()
  FROM fin_ledger l
  LEFT JOIN fin_subaccounts s ON s.AccountCode = l.accountcode
 WHERE l.account_type IN ('Chart Account', 'Basic Account', 'Expense', 'Bank') AND s.AccountCode IS NULL
 GROUP BY l.accountcode;

CREATE TABLE IF NOT EXISTS gl_warning (
  id             INT UNSIGNED NOT NULL AUTO_INCREMENT,
  rule_code      VARCHAR(10)  NOT NULL,
  scope_key      VARCHAR(120) NOT NULL DEFAULT '',
  title          VARCHAR(250) NOT NULL,
  severity       ENUM('CRITICAL','HIGH','MEDIUM','INFO') NOT NULL,
  status         ENUM('OPEN','ACKNOWLEDGED','FIXED','REAPPEARED') NOT NULL DEFAULT 'OPEN',
  record_count   INT          NOT NULL DEFAULT 0,
  amount         DECIMAL(20,2) NOT NULL DEFAULT 0,
  sample_json    TEXT         NULL,
  cause          VARCHAR(1000) NULL,
  where_text     VARCHAR(300) NULL,
  first_detected DATETIME     NOT NULL,
  last_seen      DATETIME     NOT NULL,
  fixed_at       DATETIME     NULL,
  assigned_to    VARCHAR(100) NULL,
  assigned_at    DATETIME     NULL,
  ack_by         VARCHAR(100) NULL,
  ack_at         DATETIME     NULL,
  ack_reason     VARCHAR(1000) NULL,
  ack_count      INT          NULL,
  ack_amount     DECIMAL(20,2) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_gl_warning (rule_code, scope_key),
  KEY ix_gl_warning_status (status, severity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_warning_event (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  warning_id  INT UNSIGNED NOT NULL,
  event_type  ENUM('DETECTED','REAPPEARED','FIXED','ACKNOWLEDGED','REOPENED','ASSIGNED','NOTE','CHANGED') NOT NULL,
  detail      VARCHAR(2000) NULL,
  record_count INT NULL,
  amount      DECIMAL(20,2) NULL,
  actor       VARCHAR(100) NOT NULL,
  created_at  DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_gl_wevent (warning_id, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_warning_run (
  id           INT UNSIGNED NOT NULL AUTO_INCREMENT,
  started_at   DATETIME     NOT NULL,
  finished_at  DATETIME     NULL,
  started_by   VARCHAR(100) NOT NULL,
  trigger_kind ENUM('MANUAL','AUTO','SCHEDULE') NOT NULL,
  duration_ms  INT          NULL,
  rules_run    INT          NULL,
  open_total   INT          NULL,
  critical_open INT         NULL,
  high_open    INT          NULL,
  medium_open  INT          NULL,
  health_score INT          NULL,
  errors       TEXT         NULL,
  PRIMARY KEY (id),
  KEY ix_gl_wrun (started_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_warning_trend (
  run_id      INT UNSIGNED NOT NULL,
  rule_code   VARCHAR(10)  NOT NULL,
  scope_key   VARCHAR(120) NOT NULL DEFAULT '',
  record_count INT         NOT NULL,
  amount      DECIMAL(20,2) NOT NULL,
  PRIMARY KEY (run_id, rule_code, scope_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- One record accepted as known (for example a duplicate group that is genuine); detection then leaves it out.
CREATE TABLE IF NOT EXISTS gl_record_ack (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  rule_code   VARCHAR(10)  NOT NULL,
  record_key  VARCHAR(200) NOT NULL,
  reason      VARCHAR(1000) NOT NULL,
  amount      DECIMAL(20,2) NULL,
  is_active   TINYINT(1)   NOT NULL DEFAULT 1,
  created_by  VARCHAR(100) NOT NULL,
  created_at  DATETIME     NOT NULL,
  revoked_by  VARCHAR(100) NULL,
  revoked_at  DATETIME     NULL,
  revoked_reason VARCHAR(1000) NULL,
  PRIMARY KEY (id),
  KEY ix_gl_rack (rule_code, record_key, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_saved_filter (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  username    VARCHAR(100) NOT NULL,
  report_key  VARCHAR(40)  NOT NULL,
  name        VARCHAR(100) NOT NULL,
  params_json TEXT         NOT NULL,
  is_active   TINYINT(1)   NOT NULL DEFAULT 1,
  created_at  DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_gl_sf (username, report_key, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_user_param (
  username    VARCHAR(100) NOT NULL,
  report_key  VARCHAR(40)  NOT NULL,
  params_json TEXT         NOT NULL,
  updated_at  DATETIME     NOT NULL,
  PRIMARY KEY (username, report_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_adjustment (
  id            INT UNSIGNED NOT NULL AUTO_INCREMENT,
  adj_no        VARCHAR(30)  NOT NULL,
  status        ENUM('DRAFT','PENDING','POSTED','REJECTED','CANCELLED') NOT NULL DEFAULT 'DRAFT',
  purpose       VARCHAR(250) NOT NULL,
  reason        VARCHAR(2000) NOT NULL,
  entry_date    DATE         NOT NULL,
  warning_id    INT UNSIGNED NULL,
  related_voucher INT UNSIGNED NULL,
  total_amount  DECIMAL(20,2) NOT NULL DEFAULT 0,
  created_by    VARCHAR(100) NOT NULL,
  created_at    DATETIME     NOT NULL,
  submitted_at  DATETIME     NULL,
  decided_by    VARCHAR(100) NULL,
  decided_at    DATETIME     NULL,
  decision_note VARCHAR(1000) NULL,
  posted_voucher INT UNSIGNED NULL,
  posted_at     DATETIME     NULL,
  row_version   INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_gl_adj_no (adj_no),
  KEY ix_gl_adj_status (status, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_adjustment_line (
  id            INT UNSIGNED NOT NULL AUTO_INCREMENT,
  adj_id        INT UNSIGNED NOT NULL,
  line_no       SMALLINT UNSIGNED NOT NULL,
  accountcode   VARCHAR(30)  NOT NULL,
  account_type  VARCHAR(45)  NOT NULL,
  dr_cr         ENUM('DR','CR') NOT NULL,
  amount        DECIMAL(20,0) NOT NULL,
  particulars   VARCHAR(350) NOT NULL,
  ledger_tid    INT UNSIGNED NULL,
  PRIMARY KEY (id),
  KEY ix_gl_adjl (adj_id, line_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_sequence (
  seq_key  VARCHAR(40)  NOT NULL,
  next_no  INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (seq_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_period_signoff (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  period_key  VARCHAR(20)  NOT NULL,          -- 2026-09 (month) or FY:2025-08-01 (year starting date)
  period_kind ENUM('MONTH','YEAR') NOT NULL,
  action      ENUM('REVIEWED','SIGNED_OFF','REOPENED') NOT NULL,
  checklist_json TEXT      NULL,
  note        VARCHAR(2000) NULL,
  actor       VARCHAR(100) NOT NULL,
  actor_role  VARCHAR(40)  NULL,
  created_at  DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_gl_signoff (period_key, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_audit (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  entity      VARCHAR(30)  NOT NULL,
  entity_id   VARCHAR(60)  NOT NULL,
  action      VARCHAR(40)  NOT NULL,
  before_json TEXT         NULL,
  after_json  TEXT         NULL,
  reason      VARCHAR(2000) NULL,
  actor       VARCHAR(100) NOT NULL,
  actor_role  VARCHAR(40)  NULL,
  ip_address  VARCHAR(45)  NULL,
  created_at  DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_gl_audit_entity (entity, entity_id),
  KEY ix_gl_audit_when (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS gl_baseline (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  label       VARCHAR(60)  NOT NULL,
  table_name  VARCHAR(60)  NOT NULL,
  max_id      BIGINT       NULL,
  row_count   BIGINT       NOT NULL,
  amount      DECIMAL(24,2) NULL,
  checksum    BIGINT UNSIGNED NULL,
  taken_by    VARCHAR(100) NOT NULL,
  taken_at    DATETIME     NOT NULL,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- ── Guards: history tables are append-only ─────────────────────────────────
DROP TRIGGER IF EXISTS trg_gl_audit_bu;
DROP TRIGGER IF EXISTS trg_gl_audit_bd;
DROP TRIGGER IF EXISTS trg_gl_wevent_bu;
DROP TRIGGER IF EXISTS trg_gl_wevent_bd;
DROP TRIGGER IF EXISTS trg_gl_signoff_bu;
DROP TRIGGER IF EXISTS trg_gl_signoff_bd;
DROP TRIGGER IF EXISTS trg_gl_adj_bd;
DROP TRIGGER IF EXISTS trg_gl_adjl_bd;
DROP TRIGGER IF EXISTS trg_gl_rack_bd;
DELIMITER $$
CREATE TRIGGER trg_gl_audit_bu BEFORE UPDATE ON gl_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'gl_audit is append-only'; END$$
CREATE TRIGGER trg_gl_audit_bd BEFORE DELETE ON gl_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'gl_audit is append-only'; END$$
CREATE TRIGGER trg_gl_wevent_bu BEFORE UPDATE ON gl_warning_event FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'gl_warning_event is append-only'; END$$
CREATE TRIGGER trg_gl_wevent_bd BEFORE DELETE ON gl_warning_event FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'gl_warning_event is append-only'; END$$
CREATE TRIGGER trg_gl_signoff_bu BEFORE UPDATE ON gl_period_signoff FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'gl_period_signoff is append-only'; END$$
CREATE TRIGGER trg_gl_signoff_bd BEFORE DELETE ON gl_period_signoff FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'gl_period_signoff is append-only'; END$$
CREATE TRIGGER trg_gl_adj_bd BEFORE DELETE ON gl_adjustment FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Adjusting entries are never deleted: cancel or reject them'; END$$
CREATE TRIGGER trg_gl_adjl_bd BEFORE DELETE ON gl_adjustment_line FOR EACH ROW
BEGIN
  IF (SELECT status FROM gl_adjustment WHERE id = OLD.adj_id) <> 'DRAFT' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Lines of a submitted adjusting entry cannot be removed';
  END IF;
END$$
CREATE TRIGGER trg_gl_rack_bd BEFORE DELETE ON gl_record_ack FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Acknowledgements are never deleted: revoke them'; END$$
DELIMITER ;
