-- ============================================================================
--  FIX: acad_RegNoCreator issued duplicate registration numbers
--  2026-08-05
--
--  Symptom
--  -------
--  502 applicants shared only 21 distinct registration numbers. Worst case:
--  26/U/BAED/0055/K/DAY was stamped on 294 applications, spanning 7 different
--  programmes. Because acad_student's PRIMARY KEY is `regno`, every applicant
--  registered under a shared number collapsed onto ONE student row, which ended
--  up holding a mixture of different people's details.
--
--  Column semantics in the live system (verified against production data):
--      acad_student.regno    = the MRU number  (acad_applications.stud_entry_no)
--                              -> the operative key: portal login, acad_registration,
--                                 acad_results all use this
--      acad_student.entryno  = the academic/slash number (stud_reg_no)
--
--  Defects fixed here
--  ------------------
--  1. The opening `SELECT ... INTO` had no LIMIT 1. An applicant with more than
--     one matching choice row raises ER_TOO_MANY_ROWS (1172) and aborts the
--     function mid-way, so callers could proceed on a stale/blank number.
--
--  2. The next sequence number was extracted by character offset:
--         SUBSTRING(stud_reg_no, cslength + 6, 4)
--     which silently reads the wrong characters whenever the programme code or
--     level code length differs from what the arithmetic assumes. Replaced with
--     slash-segment extraction, which is correct for every programme code.
--
--  3. The candidate number was never checked for availability, and it was only
--     compared against acad_applications.stud_reg_no. If the caller failed to
--     persist it (its UPDATE is guarded by `stud_reg_no IS NULL/'-'/''`) or its
--     transaction rolled back, the very same number was handed out again on the
--     next call. Now the function checks BOTH acad_applications.stud_reg_no and
--     acad_student.entryno and advances until it finds a genuinely free number.
--
--  Unchanged on purpose: the portal account provisioning at the end still keys
--  the account on `eno` (the MRU number). That is what students actually log in
--  with — 16,033 student logins use the MRU number versus 64 on the slash form.
-- ============================================================================

DROP FUNCTION IF EXISTS acad_RegNoCreator;

DELIMITER $$

CREATE DEFINER=`root`@`localhost` FUNCTION `acad_RegNoCreator`(eno CHAR(25))
RETURNS char(35) CHARSET utf8
BEGIN
    DECLARE stud_no, eyr, lev, guard, taken INT;
    DECLARE intk, prog, sess, reg, init_reg, campus, nationality, levc, sess_abbr CHAR(45);

    -- LIMIT 1: never abort on a duplicate choice row.
    SELECT SUBSTRING(ap.stud_intake,1,1), UPPER(ac.prog_id), ap.stud_entry_year,
           ap.stud_nationality, UPPER(ac.adm_session), c.campus_name
      INTO intk, prog, eyr, nationality, sess, campus
    FROM acad_applications ap
    JOIN acad_applicant_choices ac ON ap.stud_entry_no = ac.stud_entry_no
    JOIN acad_campuses c ON ap.stud_campus = c.campus_code
    WHERE ap.stud_entry_no = eno AND ac.Choice = 1
    LIMIT 1;

    -- No resolvable applicant/programme/campus: refuse rather than invent a number.
    IF prog IS NULL OR eyr IS NULL OR campus IS NULL THEN
        RETURN '-';
    END IF;

    SELECT levelcode   INTO lev       FROM acad_programme     WHERE progcode = prog LIMIT 1;
    SELECT Abbreviation INTO sess_abbr FROM acad_studysessions s WHERE s.Session = sess LIMIT 1;

    IF lev IS NULL OR sess_abbr IS NULL THEN
        RETURN '-';
    END IF;

    SET levc   = IF(lev < 4, 'U', 'GC');
    SET campus = IF(campus LIKE '%Kirumba%', 'M', 'K');
    SET eyr    = SUBSTRING(eyr, 3, 2);

    SET init_reg = CONCAT(eyr, '/', levc, '/', prog, '/', '____', '/', campus, '/', sess_abbr);

    -- Highest sequence already used for this pattern, taken from BOTH tables that
    -- store the slash-form number. Segment 4 of `YY/LEVC/PROG/NNNN/CAMPUS/SESS`
    -- is extracted by delimiter, so programme-code length no longer matters.
    SELECT GREATEST(
             IFNULL((SELECT MAX(CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(stud_reg_no,'/',4),'/',-1) AS UNSIGNED))
                       FROM acad_applications WHERE stud_reg_no LIKE init_reg), 0),
             IFNULL((SELECT MAX(CAST(SUBSTRING_INDEX(SUBSTRING_INDEX(entryno,'/',4),'/',-1) AS UNSIGNED))
                       FROM acad_student      WHERE entryno     LIKE init_reg), 0)
           ) INTO stud_no;

    SET stud_no = IFNULL(stud_no, 0) + 1;
    SET guard   = 0;
    SET reg     = CONCAT(eyr,'/',levc,'/',prog,'/',LPAD(stud_no,4,0),'/',campus,'/',sess_abbr);

    -- Advance past anything already taken. MAX() alone is not enough: gaps and
    -- previously-issued-but-unpersisted numbers must not be handed out twice.
    SET taken = 1;
    WHILE taken > 0 AND guard < 5000 DO
        SELECT (SELECT COUNT(*) FROM acad_applications WHERE stud_reg_no = reg)
             + (SELECT COUNT(*) FROM acad_student      WHERE entryno     = reg)
          INTO taken;

        IF taken > 0 THEN
            SET stud_no = stud_no + 1;
            SET reg     = CONCAT(eyr,'/',levc,'/',prog,'/',LPAD(stud_no,4,0),'/',campus,'/',sess_abbr);
            SET guard   = guard + 1;
        END IF;
    END WHILE;

    IF taken > 0 THEN
        RETURN '-';   -- exhausted the guard; refuse rather than duplicate
    END IF;

    -- Portal login account, keyed on the MRU number (eno) — this is what the
    -- eportal authenticates against. Without these three rows the student gets
    -- "Account not found" on https://eportal.mru.ac.ug/.
    IF reg IS NOT NULL THEN
        INSERT IGNORE INTO campus_dynamics_portal.my_aspnet_users
            (id, applicationId, name, isAnonymous, lastActivityDate)
        SELECT NULL, 1, eno, 0, NOW() FROM dual;

        INSERT IGNORE INTO campus_dynamics_portal.my_aspnet_usersinroles(userId, roleId)
        SELECT id, 16 FROM campus_dynamics_portal.my_aspnet_users WHERE name = eno;

        INSERT IGNORE INTO campus_dynamics_portal.my_aspnet_membership
            (userId, Email, Comment, Password, PasswordKey, PasswordFormat,
             PasswordQuestion, PasswordAnswer, IsApproved, LastActivityDate, LastLoginDate,
             LastPasswordChangedDate, CreationDate, IsLockedOut, LastLockedOutDate,
             FailedPasswordAttemptCount, FailedPasswordAttemptWindowStart,
             FailedPasswordAnswerAttemptCount, FailedPasswordAnswerAttemptWindowStart)
        SELECT id, '-', '-', 'XdiDC5xPHsIAtl8URiEgMUMLiCMIWEK8Q0WEWh9txzo=',
               'FGyxDhogiUILE9K+xekuPA==', 1, '-',
               'hqU9IFrPDlHRzuufBgR52YUEH6Ke2sxxzedJxB5eC60=', 1, NOW(),
               NOW(), NOW(), NOW(), 0, NOW(), 0, 0, 0, 0
        FROM campus_dynamics_portal.my_aspnet_users WHERE name = eno;
    END IF;

    RETURN IF(reg IS NULL, '-', reg);
END$$

DELIMITER ;
