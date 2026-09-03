-- ============================================================================
--  In-service fee structure - Stage 1: schema + session-aware lookup
--  2026-09-03
--
--  The problem
--  -----------
--  fin_programme_fees held exactly ONE row per programme (UNIQUE uq_progcode) and
--  carried no session. fin_BillProgrammeFees already receives the student's session
--  (p_session) but passed it no further: it called fin_GetProgrammeFee with only
--  programme / study year / semester. So an in-service student was billed the SAME
--  tuition (item 1) and functional fee (item 52) as a day student on the same
--  programme.
--
--  Everything OTHER than tuition/functional was already session-aware - the legacy
--  fin_fees_pay_schedule and fin_fees_structure tables both key on stud_session.
--  Only the two largest items were session-blind.
--
--  This stage is deliberately BEHAVIOUR-PRESERVING. Every existing row becomes
--  session 'MAIN', and the lookup falls back to MAIN whenever no session-specific
--  row exists - which, until an administrator enters in-service rates, is always.
--  Nobody's bill changes when this script is applied.
-- ============================================================================

USE campus_dynamics_accounts;

-- -- 1. Session dimension -----------------------------------------------------
ALTER TABLE fin_programme_fees
    ADD COLUMN stud_session VARCHAR(25) NOT NULL DEFAULT 'MAIN'
        COMMENT 'MAIN = default structure for the programme; INSERVICE = in-service rates'
        AFTER progcode;

UPDATE fin_programme_fees SET stud_session = 'MAIN' WHERE COALESCE(stud_session,'') = '';

-- One structure per (programme, session) rather than per programme.
ALTER TABLE fin_programme_fees DROP INDEX uq_progcode;
ALTER TABLE fin_programme_fees ADD UNIQUE KEY uq_prog_session (progcode, stud_session);
ALTER TABLE fin_programme_fees ADD INDEX ix_prog_active_session (progcode, is_active, stud_session);


-- -- 2. Session-aware fee lookup ----------------------------------------------
DROP PROCEDURE IF EXISTS fin_GetProgrammeFee;
DELIMITER $$
CREATE PROCEDURE fin_GetProgrammeFee(
    IN  p_progcode   CHAR(25),
    IN  p_study_year INT,
    IN  p_semester   INT,
    IN  p_session    CHAR(25),
    OUT p_tuition    DECIMAL(15,2),
    OUT p_functional DECIMAL(15,2))
BEGIN
    DECLARE v_id   INT DEFAULT NULL;
    DECLARE v_sess VARCHAR(25);

    SET p_tuition = 0;
    SET p_functional = 0;

    -- Session names vary by screen ("InService", "IN SERVICE", "INSRV"); fold them to
    -- one canonical value so a rate entered once is found however the caller spells it.
    SET v_sess = UPPER(TRIM(COALESCE(p_session, '')));
    SET v_sess = REPLACE(REPLACE(REPLACE(v_sess, ' ', ''), '-', ''), '_', '');
    SET v_sess = CASE
                    WHEN v_sess IN ('INSERVICE','INSRV','INSERV') THEN 'INSERVICE'
                    WHEN v_sess IN ('DAY','FULLTIME')             THEN 'DAY'
                    WHEN v_sess IN ('WEEKEND','WKD')              THEN 'WEEKEND'
                    WHEN v_sess IN ('EVENING','EVE')              THEN 'EVENING'
                    ELSE v_sess
                 END;

    -- Most specific first: a structure entered for this exact session.
    IF v_sess <> '' AND v_sess <> 'MAIN' THEN
        SET v_id = NULL;
        SELECT ID INTO v_id FROM fin_programme_fees
        WHERE progcode = p_progcode AND is_active = 'Yes'
          AND UPPER(TRIM(stud_session)) = v_sess
        LIMIT 1;
    END IF;

    -- Otherwise the programme's default structure.
    IF v_id IS NULL THEN
        SET v_id = NULL;
        SELECT ID INTO v_id FROM fin_programme_fees
        WHERE progcode = p_progcode AND is_active = 'Yes'
          AND UPPER(TRIM(stud_session)) = 'MAIN'
        LIMIT 1;
    END IF;

    -- Safety net for any row that predates the session column and was never stamped.
    IF v_id IS NULL THEN
        SET v_id = NULL;
        SELECT ID INTO v_id FROM fin_programme_fees
        WHERE progcode = p_progcode AND is_active = 'Yes'
        LIMIT 1;
    END IF;

    IF v_id IS NOT NULL THEN
        IF     p_study_year = 1 AND p_semester = 1 THEN
            SELECT y1_s1_tuition, y1_s1_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 1 AND p_semester = 2 THEN
            SELECT y1_s2_tuition, y1_s2_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 1 AND p_semester = 3 THEN
            SELECT y1_s3_tuition, y1_s3_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 2 AND p_semester = 1 THEN
            SELECT y2_s1_tuition, y2_s1_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 2 AND p_semester = 2 THEN
            SELECT y2_s2_tuition, y2_s2_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 2 AND p_semester = 3 THEN
            SELECT y2_s3_tuition, y2_s3_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 3 AND p_semester = 1 THEN
            SELECT y3_s1_tuition, y3_s1_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 3 AND p_semester = 2 THEN
            SELECT y3_s2_tuition, y3_s2_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 3 AND p_semester = 3 THEN
            SELECT y3_s3_tuition, y3_s3_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 4 AND p_semester = 1 THEN
            SELECT y4_s1_tuition, y4_s1_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 4 AND p_semester = 2 THEN
            SELECT y4_s2_tuition, y4_s2_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        ELSEIF p_study_year = 4 AND p_semester = 3 THEN
            SELECT y4_s3_tuition, y4_s3_functional INTO p_tuition, p_functional FROM fin_programme_fees WHERE ID = v_id;
        END IF;
    END IF;

    SET p_tuition    = COALESCE(p_tuition, 0);
    SET p_functional = COALESCE(p_functional, 0);
END$$
DELIMITER ;


-- -- 3. Pass through the session the biller already has ------------------------
DROP PROCEDURE IF EXISTS fin_BillProgrammeFees;
DELIMITER $$
CREATE PROCEDURE fin_BillProgrammeFees(
    IN p_regno      CHAR(35),
    IN p_progcode   CHAR(25),
    IN p_session    CHAR(25),
    IN p_study_year INT,
    IN p_semester   INT,
    IN p_acad_year  CHAR(15),
    IN p_user       CHAR(45),
    IN p_csid       CHAR(25))
BEGIN
    DECLARE v_tuition    DECIMAL(15,2) DEFAULT 0;
    DECLARE v_functional DECIMAL(15,2) DEFAULT 0;
    DECLARE v_dummy      CHAR(25);

    -- p_session was always supplied by every caller and then thrown away here. It is
    -- now carried into the lookup, which is what makes an in-service rate reachable.
    CALL fin_GetProgrammeFee(p_progcode, p_study_year, p_semester, p_session, v_tuition, v_functional);

    IF v_tuition > 0 THEN
        SELECT fin_TermlyItemBillingFN('Bill', p_regno, 1, p_semester, p_progcode,
            p_session, DATE(SYSDATE()), p_user, p_study_year, p_acad_year, p_csid, v_tuition)
        INTO v_dummy;
    END IF;

    IF v_functional > 0 THEN
        SELECT fin_TermlyItemBillingFN('Bill', p_regno, 52, p_semester, p_progcode,
            p_session, DATE(SYSDATE()), p_user, p_study_year, p_acad_year, p_csid, v_functional)
        INTO v_dummy;
    END IF;
END$$
DELIMITER ;
