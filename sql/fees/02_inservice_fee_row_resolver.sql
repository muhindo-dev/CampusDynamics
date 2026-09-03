-- ============================================================================
--  In-service fee structure - Stage 2: one resolver for "which structure applies"
--  2026-09-03
--
--  fin_programme_fees now holds one row per (programme, session). Roughly a dozen
--  readers across eadmin and eportal ask "the fee structure for programme X" with
--  WHERE progcode = X ... LIMIT 1. Those queries do not break on a second row - they
--  silently pick an ARBITRARY one, which is worse: a day student's fees could be read
--  from the in-service structure with nothing to show anything was wrong.
--
--  Rather than repeat the same precedence rule in every one of those queries, this
--  function states it once:
--
--        a structure for the student's own session, else the programme's MAIN
--        structure, else any active row (a safety net for rows that predate the
--        session column).
--
--  Callers become:  ... WHERE ID = fin_ProgFeeRow(@progcode, @session)
--  Passing NULL or '' for the session yields the MAIN row, which is exactly the
--  behaviour every one of those readers had before this change.
-- ============================================================================

USE campus_dynamics_accounts;

DROP FUNCTION IF EXISTS fin_ProgFeeRow;

DELIMITER $$
CREATE FUNCTION fin_ProgFeeRow(p_progcode CHAR(25), p_session CHAR(25))
RETURNS INT
READS SQL DATA
DETERMINISTIC
BEGIN
    DECLARE v_id   INT DEFAULT NULL;
    DECLARE v_sess VARCHAR(25);

    -- Session names differ by screen ("InService", "IN SERVICE", "INSRV"); fold them so a
    -- rate entered once is found however the caller spells it.
    SET v_sess = UPPER(TRIM(COALESCE(p_session, '')));
    SET v_sess = REPLACE(REPLACE(REPLACE(v_sess, ' ', ''), '-', ''), '_', '');
    SET v_sess = CASE
                    WHEN v_sess IN ('INSERVICE','INSRV','INSERV') THEN 'INSERVICE'
                    WHEN v_sess IN ('DAY','FULLTIME')             THEN 'DAY'
                    WHEN v_sess IN ('WEEKEND','WKD')              THEN 'WEEKEND'
                    WHEN v_sess IN ('EVENING','EVE')              THEN 'EVENING'
                    ELSE v_sess
                 END;

    IF v_sess <> '' AND v_sess <> 'MAIN' THEN
        SELECT ID INTO v_id FROM fin_programme_fees
        WHERE progcode = p_progcode AND is_active = 'Yes'
          AND UPPER(TRIM(stud_session)) = v_sess
        LIMIT 1;
    END IF;

    IF v_id IS NULL THEN
        SELECT ID INTO v_id FROM fin_programme_fees
        WHERE progcode = p_progcode AND is_active = 'Yes'
          AND UPPER(TRIM(stud_session)) = 'MAIN'
        LIMIT 1;
    END IF;

    IF v_id IS NULL THEN
        SELECT ID INTO v_id FROM fin_programme_fees
        WHERE progcode = p_progcode AND is_active = 'Yes'
        LIMIT 1;
    END IF;

    RETURN v_id;
END$$
DELIMITER ;
