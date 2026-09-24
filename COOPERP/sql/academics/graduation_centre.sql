-- =====================================================================
--  Graduation Centre — schema.
--  Plan: COOPERP/NewScreens/GRADUATION_CENTRE_PLAN.md §6
--  Applied to production 2026-09-24.
--
--  acad_graduands is NOT touched. It stays exactly as it is, because
--  nineteen stored procedures and ten code files read it, including every
--  transcript procedure and CertificateDataHelper. A student being "on
--  the graduation list" still means precisely "has a row in
--  acad_graduands", before and after this work.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. acad_grad_review — the decision layer acad_graduands has no room for.
--
--    acad_graduands records WHO graduates. It cannot record a student who
--    was stopped, why, by whom, or on what evidence — and a hold with no
--    written reason is a student who quietly never graduates.
--
--    Append-only. A new verdict supersedes the previous one by stamping
--    superseded_at; nothing is updated in place and nothing is deleted,
--    because "who cleared this student, and on what basis" has to stay
--    answerable a year later, after the marks have been edited.
--
--    snapshot_json holds the engine's findings AS THEY STOOD when the
--    human decided. Without it a student cleared in September looks
--    unjustifiable in November because a mark changed in October.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS acad_grad_review (
  id            INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  regno         VARCHAR(85)   NOT NULL,
  acadyear      CHAR(25)      NOT NULL COMMENT 'the graduation year being reviewed',
  progcode      CHAR(25)      NOT NULL COMMENT 'as at decision time',
  verdict       ENUM('CLEARED','HELD','RELEASED') NOT NULL,
  reason        VARCHAR(1000)     NULL COMMENT 'required for HELD',
  snapshot_json TEXT              NULL COMMENT 'findings at decision time',
  cgpa          DOUBLE            NULL,
  degclass      VARCHAR(150)      NULL,
  cu_earned     DOUBLE            NULL,
  cu_required   DOUBLE            NULL,
  cu_source     VARCHAR(30)       NULL COMMENT 'STRUCTURE | STRUCTURE_TYPICAL | DECLARED | NONE',
  actor         VARCHAR(100)  NOT NULL,
  actor_role    VARCHAR(40)       NULL,
  created_at    DATETIME      NOT NULL,
  superseded_at DATETIME          NULL COMMENT 'NULL = the verdict in force',
  PRIMARY KEY (id),
  KEY idx_gr_current (regno, acadyear, superseded_at),
  KEY idx_gr_year    (acadyear, verdict, superseded_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ---------------------------------------------------------------------
-- 2. acad_graduands had no index on the column every list filters by.
--    It carries only PRIMARY(ID) and idx_grad_regno, so "the graduation
--    list for 2025/2026" was a full scan of the table.
-- ---------------------------------------------------------------------
ALTER TABLE acad_graduands ADD INDEX idx_grad_year (acadyear, progcode);

-- =====================================================================
--  To undo:
--    DROP TABLE acad_grad_review;
--    ALTER TABLE acad_graduands DROP INDEX idx_grad_year;
--  Neither removes any graduand. acad_graduands is untouched by this file
--  apart from gaining an index.
-- =====================================================================


-- ---------------------------------------------------------------------
-- 3. NOT APPLIED — waiting on a Registrar's decision.
--
--    acad_graduands holds one duplicated student, and the two rows are
--    byte-identical in every field except the primary key:
--
--      ID 935  MRU2021000451  SHARIFAH NAMIREMBE  DSM  2022/2023  3.39
--      ID 936  MRU2021000451  SHARIFAH NAMIREMBE  DSM  2022/2023  3.39
--
--    An accidental double-insert, not two competing records. Removing one
--    loses nothing — but taking a name off a graduation list is a
--    Registrar's action, so it is left alone here.
--
--    Once it is resolved, the unique key below is worth adding: it turns
--    idempotence from something GraduationService.Clear guards into
--    something the database guarantees, and closes the race where two
--    simultaneous clears both pass the guard.
-- ---------------------------------------------------------------------
-- DELETE FROM acad_graduands WHERE ID = 936;
-- ALTER TABLE acad_graduands ADD UNIQUE KEY uq_grad_regno (regno);
