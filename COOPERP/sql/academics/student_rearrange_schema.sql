-- ============================================================================
-- Student Course Rearrangement — schema
--
-- Four NEW tables. Nothing existing is altered or dropped.
--
-- Design notes that matter:
--
-- * The log is STRICTLY APPEND ONLY and that is enforced by the database, not
--   by convention: trg_rearrange_log_no_update / _no_delete raise SQLSTATE
--   45000 on any UPDATE or DELETE, so no code path — including a mistake in
--   this module — can rewrite history. Reversal state is therefore never
--   stamped onto the original row; it is derived from the existence of a later
--   entry whose reverses_log_id points at it.
--
-- * before_json on a DELETE entry is the complete row. That is what makes the
--   "no bare DELETE" promise real: the row is archived inside the same
--   transaction that removes it, and reversal re-inserts it under its original
--   primary key (safe, because AUTO_INCREMENT never reuses a value).
--
-- * acad_rearrange_batch.client_op_id is UNIQUE. That single constraint is the
--   idempotency guarantee: a replayed save collides and returns the stored
--   result_json instead of applying the work twice.
--
-- * Grants are deliberately ADMIN ONLY at install, per the brief. Run the
--   "OPEN UP" block at the bottom to add registrar, dean and hod once the
--   module has been reviewed.
--
-- Reversible: the DOWN section at the end drops exactly what this creates.
-- ============================================================================

USE campus_dynamics;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. Session — one sitting, opened against one student, by one officer.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS acad_rearrange_session (
  id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  session_ref       VARCHAR(30)  NOT NULL COMMENT 'SR-YYYYMMDD-NNNN',
  regno             VARCHAR(30)  NOT NULL,
  student_name      VARCHAR(200) NULL,
  prog_id           VARCHAR(25)  NULL,
  reason            VARCHAR(1000) NOT NULL COMMENT 'Why this sitting was opened; min length enforced server-side',
  acknowledged      TINYINT(1)   NOT NULL DEFAULT 0,
  acknowledged_text VARCHAR(500) NULL COMMENT 'The exact wording the officer agreed to',
  actor_user        VARCHAR(150) NOT NULL,
  actor_name        VARCHAR(200) NULL,
  actor_role        VARCHAR(60)  NULL,
  actor_ip          VARCHAR(45)  NULL,
  actor_agent       VARCHAR(400) NULL,
  opened_at         DATETIME     NOT NULL,
  last_saved_at     DATETIME     NULL,
  status            VARCHAR(20)  NOT NULL DEFAULT 'OPEN' COMMENT 'OPEN | SAVED | ABANDONED',
  ops_applied       INT          NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  UNIQUE KEY uq_rs_ref (session_ref),
  KEY ix_rs_regno (regno),
  KEY ix_rs_actor (actor_user, opened_at),
  KEY ix_rs_opened (opened_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8
  COMMENT='A rearrangement sitting: student, officer, reason, acknowledgement';

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. Batch — one save. UNIQUE client_op_id makes a retry harmless.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS acad_rearrange_batch (
  id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  session_id        BIGINT UNSIGNED NOT NULL,
  client_op_id      VARCHAR(64)  NOT NULL COMMENT 'Client-generated; UNIQUE = idempotency',
  regno             VARCHAR(30)  NOT NULL,
  actor_user        VARCHAR(150) NOT NULL,
  actor_role        VARCHAR(60)  NULL,
  actor_ip          VARCHAR(45)  NULL,
  applied_at        DATETIME     NOT NULL,
  ops_count         INT          NOT NULL DEFAULT 0,
  status            VARCHAR(20)  NOT NULL DEFAULT 'APPLIED' COMMENT 'APPLIED | REVERSAL',
  reverses_batch_id BIGINT UNSIGNED NULL,
  duration_ms       INT          NOT NULL DEFAULT 0,
  result_json       LONGTEXT     NULL COMMENT 'The response returned; replayed verbatim on a duplicate submit',
  PRIMARY KEY (id),
  UNIQUE KEY uq_rb_client_op (client_op_id),
  KEY ix_rb_session (session_id),
  KEY ix_rb_regno (regno),
  KEY ix_rb_applied (applied_at),
  KEY ix_rb_reverses (reverses_batch_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8
  COMMENT='One save. client_op_id is the idempotency key.';

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. Log — append only, enforced by trigger. The permanent record.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS acad_rearrange_log (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  session_id      BIGINT UNSIGNED NOT NULL,
  batch_id        BIGINT UNSIGNED NOT NULL,
  op_seq          INT          NOT NULL DEFAULT 0,
  regno           VARCHAR(30)  NOT NULL,
  op_type         VARCHAR(24)  NOT NULL
                  COMMENT 'MOVE | ADD | DELETE | REGISTER_SEMESTER | MARK_CHANGE | REVERSAL | RECALC',
  db_name         VARCHAR(64)  NOT NULL,
  table_name      VARCHAR(64)  NOT NULL,
  pk_column       VARCHAR(64)  NOT NULL,
  pk_value        VARCHAR(64)  NOT NULL,
  course_code     VARCHAR(25)  NULL,
  before_json     LONGTEXT     NULL COMMENT 'Complete row before. On DELETE this is the archive copy.',
  after_json      LONGTEXT     NULL COMMENT 'Complete row after. NULL on DELETE.',
  session_reason  VARCHAR(1000) NULL,
  op_reason       VARCHAR(1000) NULL COMMENT 'Per-operation reason, e.g. every mark change',
  is_override     TINYINT(1)   NOT NULL DEFAULT 0,
  override_kind   VARCHAR(40)  NULL COMMENT 'STATUS_LOCK | CLOSED_SEMESTER | CURRICULUM',
  override_reason VARCHAR(1000) NULL,
  lock_status     VARCHAR(40)  NULL COMMENT 'Results status at the moment of the change',
  actor_user      VARCHAR(150) NOT NULL,
  actor_name      VARCHAR(200) NULL,
  actor_role      VARCHAR(60)  NULL,
  actor_ip        VARCHAR(45)  NULL,
  actor_agent     VARCHAR(400) NULL,
  performed_at    DATETIME     NOT NULL,
  reverses_log_id BIGINT UNSIGNED NULL COMMENT 'Set on a reversal entry: the entry it undoes',
  PRIMARY KEY (id),
  KEY ix_rl_session (session_id),
  KEY ix_rl_batch (batch_id),
  KEY ix_rl_regno (regno, performed_at),
  KEY ix_rl_op (op_type, performed_at),
  KEY ix_rl_actor (actor_user, performed_at),
  KEY ix_rl_target (db_name, table_name, pk_value),
  KEY ix_rl_reverses (reverses_log_id),
  KEY ix_rl_when (performed_at),
  KEY ix_rl_override (is_override, performed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8
  COMMENT='Append-only rearrangement log. UPDATE and DELETE are blocked by trigger.';

-- The append-only guarantee. Not a convention — a constraint.
DROP TRIGGER IF EXISTS trg_rearrange_log_no_update;
DROP TRIGGER IF EXISTS trg_rearrange_log_no_delete;

DELIMITER $$
CREATE TRIGGER trg_rearrange_log_no_update BEFORE UPDATE ON acad_rearrange_log
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000'
  SET MESSAGE_TEXT = 'acad_rearrange_log is append only. Add a correcting entry; never rewrite one.';
END$$

CREATE TRIGGER trg_rearrange_log_no_delete BEFORE DELETE ON acad_rearrange_log
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000'
  SET MESSAGE_TEXT = 'acad_rearrange_log is append only. Log entries cannot be deleted.';
END$$
DELIMITER ;

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. Attempts — refusals and blocks. Often the interesting behaviour.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS acad_rearrange_attempt (
  id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  at_time     DATETIME     NOT NULL,
  actor_user  VARCHAR(150) NULL,
  actor_role  VARCHAR(60)  NULL,
  actor_ip    VARCHAR(45)  NULL,
  regno       VARCHAR(30)  NULL,
  session_id  BIGINT UNSIGNED NULL,
  action      VARCHAR(60)  NOT NULL COMMENT 'open_session | save | reverse | load ...',
  outcome     VARCHAR(30)  NOT NULL COMMENT 'DENIED | BLOCKED | CONFLICT | VALIDATION | DUPLICATE',
  detail      VARCHAR(600) NULL,
  PRIMARY KEY (id),
  KEY ix_ra_time (at_time),
  KEY ix_ra_outcome (outcome, at_time),
  KEY ix_ra_actor (actor_user, at_time),
  KEY ix_ra_regno (regno)
) ENGINE=InnoDB DEFAULT CHARSET=utf8
  COMMENT='Refused or blocked rearrangement attempts';

-- ─────────────────────────────────────────────────────────────────────────────
-- Menu — its own section, three items, per the brief.
-- ─────────────────────────────────────────────────────────────────────────────
INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active)
VALUES
 ('academics.rearrange',           'Student Course Rearrangement', 'academics', 'parent',  'academics', NULL, 160, 1),
 ('academics.rearrange.dashboard', 'Rearrangement Dashboard',      'academics', 'subitem', 'academics.rearrange',
  '~/COOPERP/NewScreens/StudentRearrangeDashboard.aspx', 161, 1),
 ('academics.rearrange.manage',    'Rearrange Student Record',     'academics', 'subitem', 'academics.rearrange',
  '~/COOPERP/NewScreens/StudentRearrangeManage.aspx', 162, 1),
 ('academics.rearrange.logs',      'Rearrangement Logs',           'academics', 'subitem', 'academics.rearrange',
  '~/COOPERP/NewScreens/StudentRearrangeLogs.aspx', 163, 1)
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), sort_order=VALUES(sort_order), is_active=1;

-- Grants: SUPER ADMIN ONLY at install, per section 10 of the brief.
INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by)
SELECT r.id, m.slug, 1, 1, 0, 'rearrange-install'
  FROM sys_roles r
  JOIN (SELECT 'academics.rearrange' slug
        UNION ALL SELECT 'academics.rearrange.dashboard'
        UNION ALL SELECT 'academics.rearrange.manage'
        UNION ALL SELECT 'academics.rearrange.logs') m
 WHERE r.role_code IN ('admin')
   AND NOT EXISTS (SELECT 1 FROM sys_role_permissions p WHERE p.role_id=r.id AND p.menu_slug=m.slug);

SELECT 'schema ready' AS status,
       (SELECT COUNT(*) FROM information_schema.tables
         WHERE table_schema='campus_dynamics'
           AND table_name IN ('acad_rearrange_session','acad_rearrange_batch',
                              'acad_rearrange_log','acad_rearrange_attempt')) AS tables_present,
       (SELECT COUNT(*) FROM information_schema.triggers
         WHERE trigger_schema='campus_dynamics'
           AND trigger_name IN ('trg_rearrange_log_no_update','trg_rearrange_log_no_delete')) AS guards,
       (SELECT COUNT(*) FROM sys_menu_items WHERE menu_slug LIKE 'academics.rearrange%') AS menu_rows,
       (SELECT COUNT(*) FROM sys_role_permissions WHERE menu_slug LIKE 'academics.rearrange%') AS grants;

-- ============================================================================
-- OPEN UP
-- ----------------------------------------------------------------------------
-- APPLIED 2026-09-21 for the hod role only, on request. registrar and dean are
-- still NOT granted; run the same statement with their role codes to add them.
--
-- INSERT IGNORE INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
-- SELECT 43, m.menu_slug, 1, 1, 0, 'open-to-hod', NOW()
--   FROM sys_menu_items m
--  WHERE m.menu_slug IN ('academics.rearrange','academics.rearrange.dashboard',
--                        'academics.rearrange.manage','academics.rearrange.logs');
--
-- Overriding a results status lock remains closed to HODs regardless of this
-- grant: StudentRearrangeService.CanOverrideLock() tests the role code directly
-- and admits only admin, registrar and dean.
--
-- TO REVERSE (removes the hod grant and nothing else):
-- DELETE FROM sys_role_permissions
--  WHERE role_id = 43 AND menu_slug LIKE 'academics.rearrange%';
-- ============================================================================

-- ============================================================================
-- DOWN — reverses exactly what the UP section created, nothing else.
-- Tested. Drops no existing table and no existing column.
-- ----------------------------------------------------------------------------
-- USE campus_dynamics;
-- DROP TRIGGER IF EXISTS trg_rearrange_log_no_update;
-- DROP TRIGGER IF EXISTS trg_rearrange_log_no_delete;
-- DELETE FROM sys_role_permissions WHERE menu_slug LIKE 'academics.rearrange%';
-- DELETE FROM sys_menu_items       WHERE menu_slug LIKE 'academics.rearrange%';
-- DROP TABLE IF EXISTS acad_rearrange_attempt;
-- DROP TABLE IF EXISTS acad_rearrange_log;
-- DROP TABLE IF EXISTS acad_rearrange_batch;
-- DROP TABLE IF EXISTS acad_rearrange_session;
-- ============================================================================
