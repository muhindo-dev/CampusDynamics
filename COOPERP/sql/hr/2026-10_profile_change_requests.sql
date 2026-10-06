-- Staff profile change requests (Oct 2026).
-- Staff edit personal details directly in the portal (MyProfile.aspx). Details that drive payroll,
-- contracts or appraisal (name, date of birth, gender, NIN, TIN, NSSF no, nationality, bank and
-- account, department, position, staff category) are requested and applied only when HR approves
-- in eadmin (ProfileChangeRequests.aspx). One open request per employee.
CREATE TABLE IF NOT EXISTS hrm_profile_change_requests (
    request_id    INT AUTO_INCREMENT PRIMARY KEY,
    emp_id        INT          NOT NULL,
    status        VARCHAR(20)  NOT NULL DEFAULT 'PENDING',   -- PENDING | APPROVED | PARTLY_APPROVED | REJECTED | WITHDRAWN
    changes_json  TEXT         NOT NULL,                     -- [{"field":"dept_id","label":"Department","old":"..","old_text":"..","new":"..","new_text":".."}]
    reason        TEXT         NULL,
    submitted_at  DATETIME     NOT NULL,
    reviewed_by   VARCHAR(100) NULL,
    reviewed_at   DATETIME     NULL,
    review_note   TEXT         NULL,
    applied_json  TEXT         NULL,                         -- fields actually applied on approval
    KEY idx_pcr_emp (emp_id),
    KEY idx_pcr_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
