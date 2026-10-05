-- ============================================================================
-- Contract Renewal Requests (Oct 2026)
-- HR Manual: an employee who wants their contract renewed applies at least
-- three (3) months before it expires. The application carries an application
-- letter, a letter of motivation, the Council's Staff Achievement Evaluation
-- Form (achievements + evidence over the current contract) and the online
-- performance appraisal; the supervisor recommends, HR verifies and forwards
-- to the Governance Council, whose decision HR records and implements.
--
-- Status flow (hr_contract_renewals.status):
--   DRAFT -> AWAITING_SUPERVISOR -> AWAITING_HR -> FORWARDED -> APPROVED -> CONTRACT_ISSUED
--                     |                  |             |-> NOT_APPROVED
--                     +-> RETURNED <-----+             +-> DEFERRED
--   (no supervisor resolvable: DRAFT -> AWAITING_HR)    WITHDRAWN (by employee, before FORWARDED)
-- ============================================================================

CREATE TABLE IF NOT EXISTS hr_renewal_rounds (
    round_id            INT AUTO_INCREMENT PRIMARY KEY,
    title               VARCHAR(200) NOT NULL,
    council_sitting     VARCHAR(100) NULL,           -- e.g. 'November 2026'
    council_date        DATE         NULL,
    submission_deadline DATE         NOT NULL,
    eligible_expiry_to  DATE         NOT NULL,       -- contracts ending on/before this date belong to the round
    status              VARCHAR(20)  NOT NULL DEFAULT 'OPEN',   -- OPEN | CLOSED
    notes               TEXT         NULL,
    created_by          VARCHAR(100) NULL,
    created_at          DATETIME     NULL,
    updated_at          DATETIME     NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS hr_contract_renewals (
    renewal_id              INT AUTO_INCREMENT PRIMARY KEY,
    ref_no                  VARCHAR(30)  NULL,
    round_id                INT          NULL,
    employee_id             INT          NOT NULL,
    contract_id             INT          NOT NULL,
    status                  VARCHAR(30)  NOT NULL DEFAULT 'DRAFT',

    -- snapshot of the contract being renewed and of the applicant
    emp_name                VARCHAR(200) NULL,
    emp_code                VARCHAR(50)  NULL,
    staff_category          VARCHAR(20)  NULL,
    cur_start               DATE         NULL,
    cur_end                 DATE         NULL,
    cur_type                VARCHAR(20)  NULL,
    cur_job_id              INT          NULL,
    cur_job                 VARCHAR(100) NULL,
    cur_department_id       INT          NULL,
    cur_department          VARCHAR(200) NULL,

    -- the request
    requested_term_months   INT          NULL,
    requested_start         DATE         NULL,
    requested_end           DATE         NULL,
    requested_type          VARCHAR(20)  NULL,       -- FULL TIME | PART TIME
    requested_position      VARCHAR(200) NULL,
    justification           TEXT         NULL,       -- why the contract should be renewed
    future_plans            TEXT         NULL,       -- what the employee will deliver in the next contract
    contact_phone           VARCHAR(50)  NULL,
    contact_email           VARCHAR(150) NULL,

    -- submission
    reviewer_id             INT          NULL,
    submitted_at            DATETIME     NULL,
    employee_signed_at      DATETIME     NULL,
    employee_sign_name      VARCHAR(200) NULL,
    days_to_expiry_at_submit INT         NULL,
    is_late                 TINYINT(1)   NOT NULL DEFAULT 0,   -- fewer than 3 months before expiry

    -- supervisor recommendation (reviewer on the Council form)
    sup_recommendation      VARCHAR(30)  NULL,       -- RECOMMEND | RECOMMEND_WITH_CONDITIONS | NOT_RECOMMENDED
    sup_term_months         INT          NULL,
    sup_comments            TEXT         NULL,
    sup_name                VARCHAR(200) NULL,
    sup_title               VARCHAR(200) NULL,
    sup_signed_at           DATETIME     NULL,

    -- returns (by supervisor or HR)
    return_reason           TEXT         NULL,
    returned_by             VARCHAR(20)  NULL,       -- SUPERVISOR | HR
    returned_at             DATETIME     NULL,

    -- HR verification and Council
    hr_checklist_json       TEXT         NULL,
    hr_comments             TEXT         NULL,
    hr_actor                VARCHAR(100) NULL,
    hr_verified_at          DATETIME     NULL,
    council_sitting         VARCHAR(100) NULL,
    forwarded_at            DATETIME     NULL,
    decision                VARCHAR(20)  NULL,       -- APPROVED | NOT_APPROVED | DEFERRED
    decision_term_months    INT          NULL,
    decision_start          DATE         NULL,
    decision_end            DATE         NULL,
    decision_notes          TEXT         NULL,
    decision_recorded_by    VARCHAR(100) NULL,
    decision_at             DATETIME     NULL,
    new_contract_id         INT          NULL,
    contract_issued_at      DATETIME     NULL,
    contract_issued_by      VARCHAR(100) NULL,

    withdrawn_at            DATETIME     NULL,
    withdraw_reason         TEXT         NULL,
    created_at              DATETIME     NOT NULL,
    updated_at              DATETIME     NULL,

    UNIQUE KEY uq_renewal_ref (ref_no),
    KEY idx_renewal_emp (employee_id),
    KEY idx_renewal_contract (contract_id),
    KEY idx_renewal_status (status),
    KEY idx_renewal_reviewer (reviewer_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Staff Achievement Evaluation Form rows (Council form)
CREATE TABLE IF NOT EXISTS hr_renewal_achievements (
    item_id            INT AUTO_INCREMENT PRIMARY KEY,
    renewal_id         INT          NOT NULL,
    sort_order         INT          NOT NULL DEFAULT 0,
    source             VARCHAR(20)  NOT NULL DEFAULT 'OWN',   -- CATALOGUE | APPRAISAL | OWN
    standard_id        INT          NULL,
    kpa                VARCHAR(255) NULL,      -- Employee Responsibility / Key Performance Area
    expected_standard  TEXT         NULL,
    achievement        TEXT         NULL,
    evidence           TEXT         NULL,
    reviewer_comment   TEXT         NULL,
    KEY idx_ach_renewal (renewal_id, sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Uploaded files: letters, CV, supporting documents, per-achievement evidence
CREATE TABLE IF NOT EXISTS hr_renewal_documents (
    doc_id         INT AUTO_INCREMENT PRIMARY KEY,
    renewal_id     INT          NOT NULL,
    doc_type       VARCHAR(30)  NOT NULL,   -- APPLICATION_LETTER | MOTIVATION_LETTER | CV | SUPPORTING | EVIDENCE
    item_id        INT          NULL,       -- hr_renewal_achievements.item_id for EVIDENCE
    original_name  VARCHAR(255) NOT NULL,
    stored_name    VARCHAR(100) NOT NULL,
    content_type   VARCHAR(100) NULL,
    size_bytes     INT          NOT NULL DEFAULT 0,
    uploaded_by    INT          NULL,
    uploaded_at    DATETIME     NOT NULL,
    KEY idx_doc_renewal (renewal_id, doc_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS hr_renewal_audit (
    audit_id        INT AUTO_INCREMENT PRIMARY KEY,
    renewal_id      INT          NOT NULL,
    actor_empid     INT          NULL,
    actor_username  VARCHAR(100) NULL,
    action          VARCHAR(50)  NOT NULL,
    old_status      VARCHAR(30)  NULL,
    new_status      VARCHAR(30)  NULL,
    payload_json    TEXT         NULL,
    created_at      DATETIME     NOT NULL,
    KEY idx_raudit (renewal_id, audit_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- The round HR announced on 2026-10-04
INSERT INTO hr_renewal_rounds (title, council_sitting, submission_deadline, eligible_expiry_to, status, notes, created_by, created_at)
VALUES ('Contract Renewal - November 2026 Governance Council', 'November 2026', '2026-10-30', '2027-06-30', 'OPEN',
        'Staff whose contracts expire in December 2026 or early 2027. Applications with all documents by Friday 30 October 2026.',
        'system', NOW());

-- ---------------------------------------------------------------------------
-- Shared routine: the contract an employee would renew = latest VALID contract
-- (latest end date), else their most recent contract of any status.
-- ---------------------------------------------------------------------------
DROP FUNCTION IF EXISTS hr_current_contract_id;
DELIMITER $$
CREATE FUNCTION hr_current_contract_id(p_emp INT) RETURNS INT
    READS SQL DATA
BEGIN
    DECLARE v INT DEFAULT NULL;
    DECLARE done INT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;
    SELECT ID INTO v FROM hrm_emp_contracts
     WHERE empID = p_emp
     ORDER BY (contractStatus = 'VALID') DESC, contractEnd DESC, ID DESC
     LIMIT 1;
    RETURN v;
END$$
DELIMITER ;
