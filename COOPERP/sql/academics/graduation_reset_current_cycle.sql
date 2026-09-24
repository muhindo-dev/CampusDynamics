-- =====================================================================
--  Clear the 2026/2027 graduation list, so that from now on the only
--  way onto it is approval through the Graduation Centre.
--
--  Applied to production 2026-09-25.
--
--  WHY THIS IS SCOPED TO ONE CYCLE, AND NOT THE WHOLE TABLE.
--
--  acad_graduands is the system of record for who has graduated, not a
--  working list. Nineteen stored procedures and eighteen code files read
--  it: every transcript procedure, CertificateDataHelper, AlumniDataBank,
--  and Verify.aspx - which is the PUBLIC, unauthenticated page an
--  employer uses to verify an MRU award. A row removed from here stops
--  all of that working for that person.
--
--  Of the 1,625 rows on the list, 1,419 belong to people who have
--  physically attended one of the thirteen graduation ceremonies, 553
--  have a printed transcript and 477 a printed certificate. Emptying the
--  table would have told every future enquirer that those people never
--  graduated.
--
--  The 2026/2027 cycle is different: it is the list still being compiled.
--  None of its rows has a printed transcript, a printed certificate, or a
--  convocation. Removing them costs nobody a document and breaks no
--  verification - it simply returns the cycle to the state the approval
--  workflow expects to start from.
--
--  NOTHING IS DESTROYED. Every row is copied into acad_graduands_archive
--  with who removed it, when, and why, inside the same transaction that
--  deletes it. Restoring is one INSERT..SELECT, spelled out at the foot
--  of this file.
-- =====================================================================

-- ── 1. The archive. Same shape as the source, plus the provenance. ────
CREATE TABLE IF NOT EXISTS acad_graduands_archive LIKE acad_graduands;

-- LIKE copies the AUTO_INCREMENT and the keys; an archive wants neither,
-- because the whole point is to keep the original ID intact.
ALTER TABLE acad_graduands_archive
    MODIFY COLUMN ID INT(10) NOT NULL,
    DROP PRIMARY KEY;

ALTER TABLE acad_graduands_archive
    ADD COLUMN archive_id   INT(10) NOT NULL AUTO_INCREMENT FIRST,
    ADD COLUMN removed_at   DATETIME NULL,
    ADD COLUMN removed_by   VARCHAR(100) NULL,
    ADD COLUMN removed_why  VARCHAR(500) NULL,
    ADD PRIMARY KEY (archive_id),
    ADD KEY idx_ga_regno (regno),
    ADD KEY idx_ga_year (acadyear);

-- ── 2. Move the cycle. One transaction: archived and removed together,
--       or neither. ───────────────────────────────────────────────────
START TRANSACTION;

INSERT INTO acad_graduands_archive
    (ID, regno, acadyear, cgpa, degclass, nationality, stud_name, progcode,
     trans_status, cert_status, trans_printer, cert_printer, trans_date, cert_date,
     gender, grad_date, comp_date, convocation,
     removed_at, removed_by, removed_why)
SELECT
     g.ID, g.regno, g.acadyear, g.cgpa, g.degclass, g.nationality, g.stud_name, g.progcode,
     g.trans_status, g.cert_status, g.trans_printer, g.cert_printer, g.trans_date, g.cert_date,
     g.gender, g.grad_date, g.comp_date, g.convocation,
     NOW(), 'graduation_reset_current_cycle',
     'Cleared so that only approval through the Graduation Centre can place a name on the 2026/2027 list.'
FROM acad_graduands g
WHERE g.acadyear = '2026/2027';

DELETE FROM acad_graduands WHERE acadyear = '2026/2027';

-- ── 3. The per-student summary caches list membership, so it has to be
--       told. GraduationStats.Touch() does exactly this one student at a
--       time; this is the same statement for the whole cycle. ─────────
UPDATE acad_grad_stats a
   SET a.on_list = IF(EXISTS(SELECT 1 FROM acad_graduands g WHERE g.regno = a.regno), 1, 0)
 WHERE a.regno IN (SELECT regno FROM acad_graduands_archive
                    WHERE removed_by = 'graduation_reset_current_cycle');

COMMIT;

-- =====================================================================
--  WHAT THIS DOES NOT DO.
--
--  It does not touch acad_grad_review. The decision history stays: who
--  cleared whom, when, and on what evidence is a record in its own right
--  and survives the name leaving the list. Re-clearing a student simply
--  supersedes the earlier verdict, exactly as it would have anyway.
--
--  It does not touch any other academic year. 1,607 graduands keep their
--  record, their documents and their public verification.
-- ---------------------------------------------------------------------
--  TO UNDO, in full:
--
--    INSERT INTO acad_graduands
--      (ID, regno, acadyear, cgpa, degclass, nationality, stud_name, progcode,
--       trans_status, cert_status, trans_printer, cert_printer, trans_date,
--       cert_date, gender, grad_date, comp_date, convocation)
--    SELECT ID, regno, acadyear, cgpa, degclass, nationality, stud_name, progcode,
--           trans_status, cert_status, trans_printer, cert_printer, trans_date,
--           cert_date, gender, grad_date, comp_date, convocation
--      FROM acad_graduands_archive
--     WHERE removed_by = 'graduation_reset_current_cycle';
--
--    UPDATE acad_grad_stats a
--       SET a.on_list = IF(EXISTS(SELECT 1 FROM acad_graduands g
--                                  WHERE g.regno = a.regno), 1, 0);
--
--  A full table backup was also taken immediately beforehand:
--    COOPERP/sql/academics/backups/acad_graduands-20260925-020541.sql
-- =====================================================================
