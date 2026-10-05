-- ============================================================================
-- Contract Renewal Requests - HR console (eadmin)
--   * hr_renewal_reminders: one row per "please apply" reminder email HR sends
--     from the Expiring Contracts tab (an employee may have no application yet,
--     so these cannot live in hr_renewal_audit, which is keyed by renewal_id).
--   * RBAC: register ContractRenewals.aspx under People & Contracts, same pattern
--     as hr.appraisal.* (menu item + view/edit grant for hr_manager = role 12).
--     Admin (1) sees everything through the code wildcard.
-- Requires 2026-10_contract_renewal.sql. Idempotent.
-- ============================================================================

CREATE TABLE IF NOT EXISTS hr_renewal_reminders (
    reminder_id   INT AUTO_INCREMENT PRIMARY KEY,
    employee_id   INT          NOT NULL,
    contract_id   INT          NULL,
    round_id      INT          NULL,
    renewal_id    INT          NULL,          -- application that existed at send time, if any
    email         VARCHAR(150) NULL,
    send_status   VARCHAR(20)  NOT NULL,      -- SENT | FAILED | NO_EMAIL
    error_text    VARCHAR(255) NULL,
    sent_by       VARCHAR(100) NULL,          -- 'eadmin:<username>'
    sent_at       DATETIME     NOT NULL,
    KEY idx_rrem_emp (employee_id, reminder_id),
    KEY idx_rrem_contract (contract_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at)
VALUES
 ('hr.contract.renewals', 'Contract Renewals', 'hr', 'subitem', 'hr.people', '~/COOPERP/NewScreens/ContractRenewals.aspx', 415, 1, NOW())
ON DUPLICATE KEY UPDATE label = VALUES(label), url = VALUES(url), parent_slug = VALUES(parent_slug),
                        sort_order = VALUES(sort_order), is_active = 1;

INSERT IGNORE INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
VALUES
 (12, 'hr.contract.renewals', 1, 1, 0, 'contract-renewal-2026-10', NOW());
