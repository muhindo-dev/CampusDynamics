-- ---------------------------------------------------------------------------
-- The Auditor role: schema, the role itself, and its grants.
--
-- Read-only is a property of the ROLE, not a hardcoded check on the word
-- "auditor". A second read-only role later (an external examiner, a board
-- observer, a regulator during an inspection) then needs no code change.
-- ---------------------------------------------------------------------------

-- 1. The flag ---------------------------------------------------------------
--    Idempotent: re-running this file must never fail on an existing column.
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_roles'
              AND COLUMN_NAME = 'is_read_only');
SET @s := IF(@c = 0,
    'ALTER TABLE sys_roles ADD COLUMN is_read_only TINYINT(1) NOT NULL DEFAULT 0 AFTER is_system_role',
    'SELECT ''sys_roles.is_read_only already present''');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- 2. Where refusals are recorded --------------------------------------------
--    An auditor being stopped is worth knowing about twice over: it says the
--    control works, and a run of refusals on one screen says somebody needs
--    access they have not got.
CREATE TABLE IF NOT EXISTS sys_access_denied_log (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    username     VARCHAR(100) NOT NULL DEFAULT '',
    role_code    VARCHAR(60)  NOT NULL DEFAULT '',
    path         VARCHAR(400) NOT NULL DEFAULT '',
    operation    VARCHAR(160)          DEFAULT NULL,   -- PageMethod or ?action= if there was one
    http_method  VARCHAR(10)  NOT NULL DEFAULT '',
    denied_for   VARCHAR(24)  NOT NULL DEFAULT '',     -- PAGE | WRITE
    reason       VARCHAR(255)          DEFAULT NULL,
    ip_address   VARCHAR(45)           DEFAULT NULL,
    denied_at    DATETIME     NOT NULL,
    PRIMARY KEY (id),
    KEY idx_adl_user (username, denied_at),
    KEY idx_adl_when (denied_at),
    KEY idx_adl_kind (denied_for, denied_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- 3. The role ---------------------------------------------------------------
INSERT INTO sys_roles (role_code, role_name, description, color_hex,
                       is_system_role, is_read_only, is_active, created_by, created_at)
SELECT 'auditor', 'Auditor',
       'Read-only access to dashboards, audit trails, reports and record lists across all modules. Cannot create, change or delete anything.',
       '#0f766e', 0, 1, 1, 'system', NOW()
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM sys_roles r WHERE r.role_code = 'auditor');

UPDATE sys_roles SET is_read_only = 1, is_active = 1 WHERE role_code = 'auditor';

-- 4. What the Auditor may see ----------------------------------------------
--    can_view only. can_edit and can_delete are 0 and stay 0; the gate in the
--    application refuses writes regardless, but a role that claimed edit rights
--    it does not have would be a lie waiting to be believed.
SET @rid := (SELECT id FROM sys_roles WHERE role_code = 'auditor' LIMIT 1);

DELETE FROM sys_role_permissions WHERE role_id = @rid;

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT @rid, m.menu_slug, 1, 0, 0, 'system', NOW()
  FROM sys_menu_items m
 WHERE m.is_active = 1
   AND m.menu_slug IN (
    -- home
    'home.dashboard',

    -- academics: what was taught, to whom, and how it came out
    'academics.allocation.dashboard', 'academics.allocation.workload_analysis',
    'academics.exam.marks_dashboard', 'academics.exam.all_marks',
    'academics.exam.correction_register',
    'academics.programmes.courses_dashboard', 'academics.programmes.committee_report',
    'academics.rearrange.dashboard', 'academics.rearrange.logs',
    'academics.students.enrollment_analysis', 'academics.timetable.view',
    'academics.graduation.list',

    -- fees: what was billed and what was paid
    'fees.fee_admin.dashboard', 'fees.fee_admin.audit_trail',
    'fees.fee_admin.student_ledgers', 'fees.fee_admin.structure',
    'fees.fee_admin.active_students_fees', 'fees.fee_admin.double_billing',
    'fees.bursaries.dashboard', 'fees.bursaries.beneficiaries',

    -- accounts: the statutory reports and the controls around them
    'accounts.control.finance_dashboard', 'accounts.control.audit_trail',
    'accounts.control.audit_trail_tx', 'accounts.control.batch_monitor',
    'accounts.control.double_entry', 'accounts.control.financial_periods',
    'accounts.reports', 'accounts.reports.trial_balance',
    'accounts.reports.income_statement', 'accounts.reports.balance_sheet',

    -- hr: establishment and cost, not payroll processing
    'hr.dashboard', 'hr.appraisal.dashboard', 'hr.appraisal.reports',
    'hr.appraisal.view', 'hr.people.employees', 'hr.people.contracts',
    'hr.payroll.payslips',

    -- system: the audit apparatus itself
    'system.more.audit_centre', 'system.more.marks_audit_trail',
    'system.more.results_audit_log', 'system.more.results_analytics',
    'system.more.student_results_view', 'system.more.academic_results',
    'system.more.chart_of_accounts', 'system.more.graduation_analysis',
    'system.user_roles.overview', 'system.user_roles.audit',
    'system.config.academic_years'
   );

-- Reports what landed rather than what was asked for: a slug renamed or retired in
-- sys_menu_items silently drops out of the IN list above, and the count is how that
-- would be noticed.
SELECT CONCAT('auditor role id ', @rid, ' granted ',
              (SELECT COUNT(*) FROM sys_role_permissions WHERE role_id = @rid),
              ' slugs (49 expected)') AS result;
