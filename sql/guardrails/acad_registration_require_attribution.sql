-- ============================================================================
--  GUARDRAIL v2: every semester registration must be attributable to a person.
--  2026-09-02. Replaces trg_acadreg_block_autoreg (which only blocked AUTO-/
--  RECON-/SYSTEM_ markers and was therefore bypassed by any routine that wrote
--  a blank or '-' registeredBy).
--
--  Policy: a row in acad_registration may ONLY be created by
--    (a) a student via the eportal registration wizard -> registeredBy = their regno
--    (b) a named staff member via an admin screen      -> registeredBy = username / email
--  It may NEVER be created by an automatic routine, a bulk promotion, or any
--  process that cannot say who authorised it.
--
--  What changed vs v1: unattributed inserts ('' or '-') are now rejected too.
--  StudentsPromotion.aspx.cs bulk-promoted students with registeredBy='-' and
--  slipped past v1 entirely; that is how 2026/2027 acquired registrations that
--  no student ever asked for.
--
--  Existing rows are untouched — this is BEFORE INSERT only. 15,445 historical
--  rows carry '-' and remain readable/updatable as before.
-- ============================================================================

DROP TRIGGER IF EXISTS trg_acadreg_block_autoreg;

DELIMITER $$
CREATE TRIGGER trg_acadreg_block_autoreg
BEFORE INSERT ON acad_registration
FOR EACH ROW
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
END$$
DELIMITER ;
