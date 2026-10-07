-- ---------------------------------------------------------------------------
-- General Ledger rebuild: menu items, permission slugs and grants (database campus_dynamics).
-- Backs up the RBAC tables first. Idempotent.
-- Undo: DELETE FROM sys_role_permissions WHERE granted_by='gl-2026-10';
--       UPDATE sys_menu_items SET is_active=0 WHERE menu_slug LIKE 'accounts.gl%';
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sys_menu_items_bak_gl2026       AS SELECT * FROM sys_menu_items;
CREATE TABLE IF NOT EXISTS sys_role_permissions_bak_gl2026 AS SELECT * FROM sys_role_permissions;

INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at) VALUES
 ('accounts.gl',                 'General Ledger',                       'accounts','parent', 'accounts',    NULL,                                            760,1,NOW()),
 ('accounts.gl.dashboard',       'Accounts Dashboard',                   'accounts','subitem','accounts.gl', '~/COOPERP/NewScreens/AccountsDashboard.aspx',   761,1,NOW()),
 ('accounts.gl.reports',         'Accounts Reports',                     'accounts','subitem','accounts.gl', '~/COOPERP/NewScreens/AccountsReports.aspx',     762,1,NOW()),
 ('accounts.gl.warnings',        'Finance Warnings',                     'accounts','subitem','accounts.gl', '~/COOPERP/NewScreens/AccountsWarnings.aspx',    763,1,NOW()),
 ('accounts.gl.adjust',          'Adjusting Entries',                    'accounts','subitem','accounts.gl', '~/COOPERP/NewScreens/AccountsAdjustments.aspx', 764,1,NOW()),
 ('accounts.gl.periods',         'Periods and Close',                    'accounts','subitem','accounts.gl', '~/COOPERP/NewScreens/AccountsPeriods.aspx',     765,1,NOW()),
 ('accounts.gl.account',         'Account card',                         'accounts','subitem','accounts.gl', '~/COOPERP/NewScreens/AccountsAccount.aspx',     766,1,NOW()),
 ('accounts.gl.voucher',         'Voucher',                              'accounts','subitem','accounts.gl', '~/COOPERP/NewScreens/AccountsVoucher.aspx',     767,1,NOW()),
 ('accounts.gl.warnings_manage', 'General Ledger: acknowledge, assign and map warnings', 'accounts','subitem','accounts.gl', NULL, 768,1,NOW()),
 ('accounts.gl.adjust_approve',  'General Ledger: approve adjusting entries',            'accounts','subitem','accounts.gl', NULL, 769,1,NOW()),
 ('accounts.gl.periods_manage',  'General Ledger: review and sign off periods',          'accounts','subitem','accounts.gl', NULL, 770,1,NOW())
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), parent_slug=VALUES(parent_slug), sort_order=VALUES(sort_order), is_active=1;

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT r.id, g.slug, 1, 0, 0, 'gl-2026-10', NOW()
FROM sys_roles r JOIN (
            SELECT 'bursar' rc, 'accounts' slug
  UNION ALL SELECT 'bursar','accounts.gl'                UNION ALL SELECT 'bursar','accounts.gl.dashboard'
  UNION ALL SELECT 'bursar','accounts.gl.reports'        UNION ALL SELECT 'bursar','accounts.gl.warnings'
  UNION ALL SELECT 'bursar','accounts.gl.warnings_manage' UNION ALL SELECT 'bursar','accounts.gl.adjust'
  UNION ALL SELECT 'bursar','accounts.gl.adjust_approve' UNION ALL SELECT 'bursar','accounts.gl.periods'
  UNION ALL SELECT 'bursar','accounts.gl.periods_manage' UNION ALL SELECT 'bursar','accounts.gl.account'
  UNION ALL SELECT 'bursar','accounts.gl.voucher'
  UNION ALL SELECT 'accountant','accounts'               UNION ALL SELECT 'accountant','accounts.gl'
  UNION ALL SELECT 'accountant','accounts.gl.dashboard'  UNION ALL SELECT 'accountant','accounts.gl.reports'
  UNION ALL SELECT 'accountant','accounts.gl.warnings'   UNION ALL SELECT 'accountant','accounts.gl.warnings_manage'
  UNION ALL SELECT 'accountant','accounts.gl.adjust'     UNION ALL SELECT 'accountant','accounts.gl.periods'
  UNION ALL SELECT 'accountant','accounts.gl.account'    UNION ALL SELECT 'accountant','accounts.gl.voucher'
  UNION ALL SELECT 'finance_officer','accounts'          UNION ALL SELECT 'finance_officer','accounts.gl'
  UNION ALL SELECT 'finance_officer','accounts.gl.dashboard' UNION ALL SELECT 'finance_officer','accounts.gl.reports'
  UNION ALL SELECT 'finance_officer','accounts.gl.warnings'  UNION ALL SELECT 'finance_officer','accounts.gl.periods'
  UNION ALL SELECT 'finance_officer','accounts.gl.account'   UNION ALL SELECT 'finance_officer','accounts.gl.voucher'
  UNION ALL SELECT 'auditor','accounts'                  UNION ALL SELECT 'auditor','accounts.gl'
  UNION ALL SELECT 'auditor','accounts.gl.dashboard'     UNION ALL SELECT 'auditor','accounts.gl.reports'
  UNION ALL SELECT 'auditor','accounts.gl.warnings'      UNION ALL SELECT 'auditor','accounts.gl.periods'
  UNION ALL SELECT 'auditor','accounts.gl.account'       UNION ALL SELECT 'auditor','accounts.gl.voucher'
  UNION ALL SELECT 'vc','accounts'                       UNION ALL SELECT 'vc','accounts.gl'
  UNION ALL SELECT 'vc','accounts.gl.dashboard'          UNION ALL SELECT 'vc','accounts.gl.reports'
  UNION ALL SELECT 'vc','accounts.gl.warnings'           UNION ALL SELECT 'vc','accounts.gl.periods'
  UNION ALL SELECT 'vc','accounts.gl.account'            UNION ALL SELECT 'vc','accounts.gl.voucher'
) g ON g.rc = r.role_code
WHERE NOT EXISTS (SELECT 1 FROM sys_role_permissions x WHERE x.role_id = r.id AND x.menu_slug = g.slug);
