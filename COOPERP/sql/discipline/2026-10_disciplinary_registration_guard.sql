-- ---------------------------------------------------------------------------
-- Student Disciplinary module: registration guard
-- File: COOPERP/sql/discipline/2026-10_disciplinary_registration_guard.sql
-- A suspended or expelled student cannot be registered for a semester or a course by ANY path
-- (portal wizard, eadmin screens, API v2, stored procedures, direct inserts).
--
-- MySQL 5.6 allows one BEFORE INSERT trigger per table. acad_registration already has
-- trg_acadreg_block_autoreg (sql/guardrails/acad_registration_block_autoreg_trigger.sql), so this
-- script REPLACES it with the same two rules unchanged plus the disciplinary rule.
-- Backs up nothing (no rows change); the previous trigger body is kept below for rollback.
-- ---------------------------------------------------------------------------

DROP TRIGGER IF EXISTS campus_dynamics.trg_acadreg_block_autoreg;
DELIMITER $$
CREATE TRIGGER campus_dynamics.trg_acadreg_block_autoreg BEFORE INSERT ON campus_dynamics.acad_registration FOR EACH ROW
BEGIN
    DECLARE who VARCHAR(64);
    SET who = UPPER(TRIM(COALESCE(NEW.registeredBy,'')));

    IF who REGEXP '^(AUTO-|AUTO_|RECON-|RECON_|AUTORECON|SYSTEM_)' THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Automatic semester registration is disabled by policy. The student must register via the eportal.';
    END IF;

    IF who = '' OR who = '-' THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Semester registration requires attribution (student regno or staff username). Bulk/unattributed inserts are blocked.';
    END IF;

    -- Disciplinary module (2026-10): suspension or expulsion in force.
    IF campus_dynamics.dc_effect_active(NEW.regno, 'SUSPENSION') = 1 OR campus_dynamics.dc_effect_active(NEW.regno, 'EXPULSION') = 1 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'This student is suspended or expelled under a disciplinary decision and cannot be registered. See Disciplinary Records.';
    END IF;
END$$
DELIMITER ;

DROP TRIGGER IF EXISTS campus_dynamics_portal.trg_dc_coursereg_bi;
DELIMITER $$
CREATE TRIGGER campus_dynamics_portal.trg_dc_coursereg_bi BEFORE INSERT ON campus_dynamics_portal.acad_course_registration FOR EACH ROW
BEGIN
    IF campus_dynamics.dc_effect_active(NEW.regno, 'SUSPENSION') = 1 OR campus_dynamics.dc_effect_active(NEW.regno, 'EXPULSION') = 1 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Suspended or expelled under a disciplinary decision: cannot be registered for courses. See Disciplinary Records.';
    END IF;
END$$
DELIMITER ;

-- Rollback: re-run sql/guardrails/acad_registration_block_autoreg_trigger.sql and
--           DROP TRIGGER campus_dynamics_portal.trg_dc_coursereg_bi;
