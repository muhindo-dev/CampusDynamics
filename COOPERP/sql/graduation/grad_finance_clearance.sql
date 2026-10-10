-- =====================================================================
--  Graduation Centre: Fees Clearance (campus_dynamics)
--
--  A student on the graduation list (acad_graduands) is academically
--  approved. Finance clearance is a second, independent decision taken
--  by the Bursar's office. These tables hold it.
--
--    acad_grad_finance           every decision, append-only. A new decision
--                                supersedes the previous one by stamping
--                                superseded_at; nothing is ever edited or
--                                deleted (enforced by triggers below).
--    acad_grad_finance_state     the current decision per student, kept in
--                                step with the log inside the same
--                                transaction, so lists and exports read one
--                                row instead of walking the history.
--    acad_grad_finance_posting   every bill or payment posted from the
--                                clearance screen, with the ledger ids it
--                                created, so the module's own money trail is
--                                one query away.
--    acad_grad_finance_setting   the few rules the Bursar may tune.
--
--  Idempotent: safe to run more than once.
-- =====================================================================

CREATE TABLE IF NOT EXISTS acad_grad_finance (
  id             INT UNSIGNED NOT NULL AUTO_INCREMENT,
  regno          VARCHAR(30)  NOT NULL,
  acadyear       VARCHAR(15)  NOT NULL,
  verdict        VARCHAR(10)  NOT NULL,              -- CLEARED | HELD | REVOKED
  basis          VARCHAR(20)  NOT NULL DEFAULT '',   -- NO_BALANCE | IN_CREDIT | OVERRIDE | '' (holds/revokes)
  reason         VARCHAR(600) NOT NULL DEFAULT '',
  billed         DECIMAL(15,2) NOT NULL DEFAULT 0,   -- canonical figures at the moment of decision
  paid           DECIMAL(15,2) NOT NULL DEFAULT 0,
  balance        DECIMAL(15,2) NOT NULL DEFAULT 0,   -- billed - paid; > 0 means the student owes
  checks_json    TEXT NULL,                          -- the findings the decision was taken against
  batch_id       VARCHAR(40)  NULL,                  -- set when decided in a bulk action
  actor          VARCHAR(100) NOT NULL,
  actor_role     VARCHAR(60)  NOT NULL DEFAULT '',
  ip             VARCHAR(45)  NULL,
  created_at     DATETIME     NOT NULL,
  superseded_at  DATETIME     NULL,
  superseded_by  INT UNSIGNED NULL,
  PRIMARY KEY (id),
  KEY ix_gf_reg (regno, superseded_at),
  KEY ix_gf_year (acadyear),
  KEY ix_gf_actor (actor)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS acad_grad_finance_state (
  regno        VARCHAR(30)  NOT NULL,
  acadyear     VARCHAR(15)  NOT NULL,
  status       VARCHAR(10)  NOT NULL,                -- CLEARED | HELD   (no row = pending)
  decision_id  INT UNSIGNED NOT NULL,
  basis        VARCHAR(20)  NOT NULL DEFAULT '',
  reason       VARCHAR(600) NOT NULL DEFAULT '',
  balance      DECIMAL(15,2) NOT NULL DEFAULT 0,
  actor        VARCHAR(100) NOT NULL,
  decided_at   DATETIME     NOT NULL,
  PRIMARY KEY (regno),
  KEY ix_gfs_year (acadyear, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS acad_grad_finance_posting (
  id           INT UNSIGNED NOT NULL AUTO_INCREMENT,
  regno        VARCHAR(30)  NOT NULL,
  acadyear     VARCHAR(15)  NOT NULL DEFAULT '',     -- graduation year the posting was made for
  kind         VARCHAR(10)  NOT NULL,                -- BILL | PAYMENT
  item_code    INT          NULL,
  amount       DECIMAL(15,2) NOT NULL,
  detail       VARCHAR(350) NOT NULL DEFAULT '',
  reference    VARCHAR(80)  NOT NULL DEFAULT '',     -- receipt / bank slip number for a payment
  bank_code    VARCHAR(20)  NOT NULL DEFAULT '',
  tracking_tid BIGINT       NULL,                    -- fin_studentfeestracking.TID created
  voucher_no   BIGINT       NULL,                    -- fin_ledger voucher created
  batch_id     VARCHAR(40)  NULL,
  actor        VARCHAR(100) NOT NULL,
  ip           VARCHAR(45)  NULL,
  created_at   DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_gfp_reg (regno),
  KEY ix_gfp_ref (reference)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS acad_grad_finance_setting (
  k           VARCHAR(40)  NOT NULL,
  v           VARCHAR(200) NOT NULL,
  note        VARCHAR(300) NOT NULL DEFAULT '',
  updated_by  VARCHAR(100) NOT NULL DEFAULT 'install',
  updated_at  DATETIME     NOT NULL,
  PRIMARY KEY (k)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

INSERT IGNORE INTO acad_grad_finance_setting (k, v, note, updated_at) VALUES
 ('require_grad_fee',  '1',      'A Graduation Fee bill for the list year must exist before a student can be cleared', NOW()),
 ('grad_fee_item',     '60',     'academicbillingitems.ItemCode of the Graduation Fee', NOW()),
 ('grad_fee_amount',   '505000', 'Default Graduation Fee (UGX) offered when billing from this screen', NOW()),
 ('grad_fee_semester', '2',      'Semester the Graduation Fee is billed against', NOW()),
 ('document_gate',     'certificate', 'Which documents need finance clearance: off | certificate | both', NOW()),
 ('first_gated_year',  '2026',   'Graduation lists from this year onward need finance clearance', NOW());

-- ── The decision log may only be appended to. ─────────────────────────
DROP TRIGGER IF EXISTS trg_grad_finance_no_delete;
DROP TRIGGER IF EXISTS trg_grad_finance_only_supersede;
DELIMITER $$
CREATE TRIGGER trg_grad_finance_no_delete BEFORE DELETE ON acad_grad_finance
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'acad_grad_finance is append-only: a decision is superseded, never deleted';
END$$
CREATE TRIGGER trg_grad_finance_only_supersede BEFORE UPDATE ON acad_grad_finance
FOR EACH ROW
BEGIN
  IF NOT (NEW.regno <=> OLD.regno AND NEW.acadyear <=> OLD.acadyear AND NEW.verdict <=> OLD.verdict
          AND NEW.basis <=> OLD.basis AND NEW.reason <=> OLD.reason AND NEW.billed <=> OLD.billed
          AND NEW.paid <=> OLD.paid AND NEW.balance <=> OLD.balance AND NEW.actor <=> OLD.actor
          AND NEW.created_at <=> OLD.created_at AND NEW.checks_json <=> OLD.checks_json) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'acad_grad_finance: only superseded_at / superseded_by may change';
  END IF;
END$$
DELIMITER ;

-- ── Menu entry and permissions ─────────────────────────────────────────
--  can_view   = see the queue and a student's finances
--  can_edit   = bill, record a payment, clear (balance settled), hold
--  can_delete = override (clear with money outstanding) and revoke a clearance
INSERT IGNORE INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at)
VALUES ('academics.graduation.fees_clearance', 'Fees Clearance', 'academics', 'subitem', 'academics.graduation',
        '~/COOPERP/NewScreens/GraduationFinance.aspx', 758, 1, NOW());

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT r.id, 'academics.graduation.fees_clearance',
       1,
       CASE WHEN r.role_code IN ('bursar','fees_officer','finance_officer','accountant') THEN 1 ELSE 0 END,
       CASE WHEN r.role_code IN ('bursar') THEN 1 ELSE 0 END,
       'install', NOW()
FROM sys_roles r
WHERE r.role_code IN ('bursar','fees_officer','finance_officer','accountant','auditor','registrar','vc')
  AND NOT EXISTS (SELECT 1 FROM sys_role_permissions p WHERE p.role_id = r.id
                  AND p.menu_slug = 'academics.graduation.fees_clearance');
