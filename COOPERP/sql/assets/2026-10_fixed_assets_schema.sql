-- ---------------------------------------------------------------------------
-- Fixed Assets module: schema (database campus_dynamics)
-- File: COOPERP/sql/assets/2026-10_fixed_assets_schema.sql
-- Creates new tables only. Touches no existing table. Safe to re-run.
-- Run with the mysql client (uses DELIMITER for the guard triggers).
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS fa_settings (
  setting_key    VARCHAR(60)  NOT NULL,
  setting_value  VARCHAR(500) NOT NULL,
  description    VARCHAR(300) NULL,
  updated_by     VARCHAR(100) NULL,
  updated_at     DATETIME     NULL,
  PRIMARY KEY (setting_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

INSERT IGNORE INTO fa_settings (setting_key, setting_value, description, updated_by, updated_at) VALUES
 ('fy_start_month',        '8',    'First month of the financial year (8 = August; year ends 31 July)', 'install', NOW()),
 ('asset_no_prefix',       'MRU',  'Prefix of generated asset numbers', 'install', NOW()),
 ('cap_threshold_default', '0',    'Default capitalisation threshold in UGX (0 = no warning)', 'install', NOW()),
 ('revalue_months_default','12',   'Default revaluation interval in months for categories on the revaluation model', 'install', NOW()),
 ('attachment_max_mb',     '10',   'Largest attachment accepted, in MB', 'install', NOW());

CREATE TABLE IF NOT EXISTS fa_category (
  id                   INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  parent_id            INT UNSIGNED      NOT NULL DEFAULT 0,      -- 0 = category, otherwise the parent category id
  code                 VARCHAR(10)       NOT NULL,
  name                 VARCHAR(120)      NOT NULL,
  description          VARCHAR(500)      NULL,
  asset_type           ENUM('TANGIBLE','INTANGIBLE') NULL,       -- NULL on a sub-category = inherit
  dep_method           ENUM('SL','RB','NONE') NULL,
  useful_life_years    DECIMAL(6,2)      NULL,
  dep_rate_pct         DECIMAL(7,4)      NULL,
  residual_pct         DECIMAL(5,2)      NULL,
  revalue_every_months SMALLINT UNSIGNED NULL,                    -- NULL = cost model, not revalued
  cap_threshold        DECIMAL(18,2)     NULL,
  gl_cost_account      VARCHAR(20)       NULL,
  gl_accum_account     VARCHAR(20)       NULL,
  gl_expense_account   VARCHAR(20)       NULL,
  next_seq             INT UNSIGNED      NOT NULL DEFAULT 1,      -- asset number sequence (sub-categories only)
  sort_order           SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active            TINYINT(1)        NOT NULL DEFAULT 1,
  created_by           VARCHAR(100)      NOT NULL,
  created_at           DATETIME          NOT NULL,
  updated_by           VARCHAR(100)      NULL,
  updated_at           DATETIME          NULL,
  row_version          INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_fa_category_code (parent_id, code),
  KEY ix_fa_category_parent (parent_id, sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_asset (
  id                    INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  asset_no              VARCHAR(40)       NOT NULL,
  asset_no_edits        TINYINT UNSIGNED  NOT NULL DEFAULT 0,
  tag_no                VARCHAR(60)       NULL,
  name                  VARCHAR(200)      NOT NULL,
  description           VARCHAR(1000)     NULL,
  serial_no             VARCHAR(100)      NULL,
  model                 VARCHAR(100)      NULL,
  make                  VARCHAR(100)      NULL,
  category_id           INT UNSIGNED      NOT NULL,              -- the sub-category
  asset_type            ENUM('TANGIBLE','INTANGIBLE') NOT NULL DEFAULT 'TANGIBLE',
  quantity              INT UNSIGNED      NOT NULL DEFAULT 1,
  campus_id             INT               NOT NULL,              -- acad_campuses.ID
  building              VARCHAR(120)      NULL,
  room_id               INT               NULL,                  -- acad_lecturerooms.RoomID when it is a teaching room
  room                  VARCHAR(120)      NULL,
  department_id         INT UNSIGNED      NULL,                  -- hrm_departments.ID
  custodian_emp_id      INT UNSIGNED      NULL,                  -- hrm_employee.empID
  custodian_since       DATE              NULL,
  supplier_id           INT UNSIGNED      NULL,                  -- campus_dynamics_accounts.supplier.supplierID
  supplier_name         VARCHAR(150)      NULL,
  purchase_date         DATE              NOT NULL,
  invoice_ref           VARCHAR(100)      NULL,
  order_ref             VARCHAR(100)      NULL,
  requisition_ref       VARCHAR(40)       NULL,                  -- sys_requisitions.req_number
  funding_source        VARCHAR(120)      NULL,
  warranty_expiry       DATE              NULL,
  original_cost         DECIMAL(18,2)     NOT NULL,
  dep_method            ENUM('SL','RB','NONE') NOT NULL,
  useful_life_years     DECIMAL(6,2)      NULL,
  dep_rate_pct          DECIMAL(7,4)      NULL,
  residual_value        DECIMAL(18,2)     NOT NULL DEFAULT 0,
  dep_start_date        DATE              NOT NULL,
  -- Posted state. Written only by the posting routine, from fa_record.
  status                ENUM('IN_USE','IN_STORE','UNDER_REPAIR','LOST','DISPOSED','WRITTEN_OFF','VOID') NOT NULL DEFAULT 'IN_USE',
  current_value         DECIMAL(18,2)     NOT NULL DEFAULT 0,
  cost_basis            DECIMAL(18,2)     NOT NULL DEFAULT 0,
  accum_depreciation    DECIMAL(18,2)     NOT NULL DEFAULT 0,
  reval_surplus         DECIMAL(18,2)     NOT NULL DEFAULT 0,
  base_value            DECIMAL(18,2)     NOT NULL DEFAULT 0,
  base_date             DATE              NULL,
  base_remaining_months SMALLINT UNSIGNED NULL,
  depreciated_to        DATE              NULL,
  last_valuation_date   DATE              NULL,
  last_verified_date    DATE              NULL,
  closed_on             DATE              NULL,
  last_record_id        INT UNSIGNED      NULL,
  notes                 TEXT              NULL,
  created_by            VARCHAR(100)      NOT NULL,
  created_at            DATETIME          NOT NULL,
  updated_by            VARCHAR(100)      NULL,
  updated_at            DATETIME          NULL,
  row_version           INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_fa_asset_no (asset_no),
  KEY ix_fa_asset_tag (tag_no),
  KEY ix_fa_asset_cat (category_id, status),
  KEY ix_fa_asset_campus (campus_id, status),
  KEY ix_fa_asset_dept (department_id),
  KEY ix_fa_asset_custodian (custodian_emp_id),
  KEY ix_fa_asset_purchase (purchase_date),
  KEY ix_fa_asset_status (status),
  KEY ix_fa_asset_serial (serial_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_record (
  id                     INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  asset_id               INT UNSIGNED      NOT NULL,
  record_type            ENUM('ACQUISITION','OPENING','DEPRECIATION','REVALUATION','APPRECIATION','TRANSFER',
                              'STATUS','MAINTENANCE','DISPOSAL','VERIFICATION','ESTIMATE','VOID','REVERSAL') NOT NULL,
  value_class            ENUM('COST','DEPRECIATION','REVALUATION','DISPOSAL','NONE') NOT NULL DEFAULT 'NONE',
  record_date            DATE              NOT NULL,
  fin_year               CHAR(9)           NOT NULL,             -- e.g. 2025/2026
  period_from            DATE              NULL,                 -- depreciation: first month covered
  period_to              DATE              NULL,                 -- depreciation: last month-end covered
  months                 TINYINT UNSIGNED  NULL,
  dep_active             TINYINT(1)        NULL,                 -- 1 on a live depreciation row, NULL otherwise
  value_before           DECIMAL(18,2)     NOT NULL,
  value_after            DECIMAL(18,2)     NOT NULL,
  change_amount          DECIMAL(18,2)     NOT NULL,
  quantity               INT UNSIGNED      NULL,
  cost_amount            DECIMAL(18,2)     NULL,                 -- maintenance cost
  is_capital             TINYINT(1)        NULL,
  status_before          VARCHAR(15)       NULL,
  status_after           VARCHAR(15)       NULL,
  from_campus_id         INT               NULL,
  to_campus_id           INT               NULL,
  from_department_id     INT UNSIGNED      NULL,
  to_department_id       INT UNSIGNED      NULL,
  from_location          VARCHAR(250)      NULL,
  to_location            VARCHAR(250)      NULL,
  from_custodian_emp_id  INT UNSIGNED      NULL,
  to_custodian_emp_id    INT UNSIGNED      NULL,
  disposal_method        ENUM('SALE','DONATION','SCRAP','WRITE_OFF','TRADE_IN','TRANSFER_OUT') NULL,
  proceeds               DECIMAL(18,2)     NULL,
  gain_loss              DECIMAL(18,2)     NULL,
  valuer                 VARCHAR(150)      NULL,
  new_remaining_months   SMALLINT UNSIGNED NULL,
  condition_grade        ENUM('GOOD','FAIR','POOR','UNSERVICEABLE') NULL,
  verified_found         TINYINT(1)        NULL,
  reason                 VARCHAR(1000)     NULL,
  reference              VARCHAR(150)      NULL,
  approval_ref           VARCHAR(150)      NULL,
  approved_by            VARCHAR(100)      NULL,
  approved_at            DATETIME          NULL,
  run_id                 INT UNSIGNED      NULL,
  import_batch_id        INT UNSIGNED      NULL,
  reverses_record_id     INT UNSIGNED      NULL,
  reversed_by_record_id  INT UNSIGNED      NULL,
  recorded_by            VARCHAR(100)      NOT NULL,
  recorded_role          VARCHAR(40)       NULL,
  recorded_at            DATETIME          NOT NULL,
  client_op_id           VARCHAR(40)       NULL,
  PRIMARY KEY (id),
  KEY ix_fa_record_asset (asset_id, record_date, id),
  KEY ix_fa_record_type (record_type, record_date),
  KEY ix_fa_record_year (fin_year, record_type),
  KEY ix_fa_record_run (run_id),
  KEY ix_fa_record_date (record_date),
  UNIQUE KEY uq_fa_record_dep_guard (asset_id, period_to, dep_active),
  UNIQUE KEY uq_fa_record_client_op (client_op_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_depreciation_run (
  id              INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  fin_year        CHAR(9)       NOT NULL,
  period_end      DATE          NOT NULL,
  scope_json      VARCHAR(1000) NULL,
  status          ENUM('POSTED','REVERSED') NOT NULL DEFAULT 'POSTED',
  asset_count     INT UNSIGNED  NOT NULL DEFAULT 0,
  total_amount    DECIMAL(18,2) NOT NULL DEFAULT 0,
  preview_hash    CHAR(40)      NULL,
  notes           VARCHAR(500)  NULL,
  posted_by       VARCHAR(100)  NOT NULL,
  posted_at       DATETIME      NOT NULL,
  reversed_by     VARCHAR(100)  NULL,
  reversed_at     DATETIME      NULL,
  reverse_reason  VARCHAR(1000) NULL,
  client_op_id    VARCHAR(40)   NULL,
  PRIMARY KEY (id),
  KEY ix_fa_run_year (fin_year, period_end),
  UNIQUE KEY uq_fa_run_client_op (client_op_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_year_lock (
  fin_year     CHAR(9)       NOT NULL,
  is_locked    TINYINT(1)    NOT NULL DEFAULT 1,
  reason       VARCHAR(1000) NULL,
  locked_by    VARCHAR(100)  NOT NULL,
  locked_at    DATETIME      NOT NULL,
  unlocked_by  VARCHAR(100)  NULL,
  unlocked_at  DATETIME      NULL,
  PRIMARY KEY (fin_year)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_attachment (
  id              INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  asset_id        INT UNSIGNED  NOT NULL,
  record_id       INT UNSIGNED  NULL,
  kind            ENUM('PHOTO','INVOICE','WARRANTY','VALUATION','DISPOSAL','OTHER') NOT NULL DEFAULT 'OTHER',
  original_name   VARCHAR(200)  NOT NULL,
  stored_name     VARCHAR(120)  NOT NULL,
  mime            VARCHAR(100)  NOT NULL,
  size_bytes      INT UNSIGNED  NOT NULL,
  sha1            CHAR(40)      NOT NULL,
  uploaded_by     VARCHAR(100)  NOT NULL,
  uploaded_at     DATETIME      NOT NULL,
  is_active       TINYINT(1)    NOT NULL DEFAULT 1,
  removed_by      VARCHAR(100)  NULL,
  removed_at      DATETIME      NULL,
  removed_reason  VARCHAR(500)  NULL,
  PRIMARY KEY (id),
  KEY ix_fa_att_asset (asset_id, is_active),
  KEY ix_fa_att_record (record_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_import_batch (
  id            INT UNSIGNED NOT NULL AUTO_INCREMENT,
  file_name     VARCHAR(200) NOT NULL,
  sha1          CHAR(40)     NOT NULL,
  rows_total    INT UNSIGNED NOT NULL DEFAULT 0,
  rows_ok       INT UNSIGNED NOT NULL DEFAULT 0,
  rows_error    INT UNSIGNED NOT NULL DEFAULT 0,
  status        ENUM('VALIDATED','COMMITTED','ABANDONED') NOT NULL DEFAULT 'VALIDATED',
  payload       MEDIUMTEXT   NOT NULL,
  report        MEDIUMTEXT   NULL,
  created_by    VARCHAR(100) NOT NULL,
  created_at    DATETIME     NOT NULL,
  committed_by  VARCHAR(100) NULL,
  committed_at  DATETIME     NULL,
  PRIMARY KEY (id),
  KEY ix_fa_import_status (status, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_audit (
  id           INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  entity       ENUM('CATEGORY','ASSET','RECORD','RUN','LOCK','ATTACHMENT','IMPORT','SETTING') NOT NULL,
  entity_id    INT UNSIGNED  NOT NULL,
  asset_id     INT UNSIGNED  NULL,
  action       VARCHAR(40)   NOT NULL,
  before_json  TEXT          NULL,
  after_json   TEXT          NULL,
  reason       VARCHAR(1000) NULL,
  actor        VARCHAR(100)  NOT NULL,
  actor_role   VARCHAR(40)   NULL,
  ip_address   VARCHAR(45)   NULL,
  created_at   DATETIME      NOT NULL,
  PRIMARY KEY (id),
  KEY ix_fa_audit_entity (entity, entity_id, created_at),
  KEY ix_fa_audit_asset (asset_id, created_at),
  KEY ix_fa_audit_actor (actor, created_at),
  KEY ix_fa_audit_date (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Guard triggers: the database itself refuses deletes and value edits.
DROP TRIGGER IF EXISTS trg_fa_record_bd;
DROP TRIGGER IF EXISTS trg_fa_record_bu;
DROP TRIGGER IF EXISTS trg_fa_asset_bd;
DROP TRIGGER IF EXISTS trg_fa_category_bd;
DROP TRIGGER IF EXISTS trg_fa_audit_bd;
DROP TRIGGER IF EXISTS trg_fa_audit_bu;

DELIMITER $$
CREATE TRIGGER trg_fa_record_bd BEFORE DELETE ON fa_record FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_record is append-only: post a reversal instead';
END$$

CREATE TRIGGER trg_fa_record_bu BEFORE UPDATE ON fa_record FOR EACH ROW
BEGIN
  -- Only the reversal links (dep_active, reversed_by_record_id) may change.
  IF NEW.asset_id <> OLD.asset_id OR NEW.record_type <> OLD.record_type
     OR NEW.record_date <> OLD.record_date OR NEW.value_before <> OLD.value_before
     OR NEW.value_after <> OLD.value_after OR NEW.change_amount <> OLD.change_amount
     OR NOT (NEW.period_to <=> OLD.period_to) OR NOT (NEW.period_from <=> OLD.period_from)
     OR NEW.value_class <> OLD.value_class OR NEW.recorded_by <> OLD.recorded_by
     OR NEW.recorded_at <> OLD.recorded_at THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_record values cannot be changed: post a reversal instead';
  END IF;
END$$

CREATE TRIGGER trg_fa_asset_bd BEFORE DELETE ON fa_asset FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_asset rows are never deleted: dispose or void the asset';
END$$

CREATE TRIGGER trg_fa_category_bd BEFORE DELETE ON fa_category FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_category rows are never deleted: deactivate the category';
END$$

CREATE TRIGGER trg_fa_audit_bd BEFORE DELETE ON fa_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_audit is append-only';
END$$

CREATE TRIGGER trg_fa_audit_bu BEFORE UPDATE ON fa_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_audit is append-only';
END$$
DELIMITER ;
