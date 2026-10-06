-- ---------------------------------------------------------------------------
-- Fixed Assets module: menu, permission slugs, role and grants (database campus_dynamics)
-- File: COOPERP/sql/assets/2026-10_fixed_assets_menu.sql
-- Backs up the three RBAC tables first. Idempotent.
-- Undo: DELETE FROM sys_role_permissions WHERE granted_by='fixed-assets-2026-10';
--       UPDATE sys_menu_items SET is_active=0 WHERE menu_slug LIKE 'accounts.assets%';
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS sys_menu_items_bak_20261006       AS SELECT * FROM sys_menu_items;
CREATE TABLE IF NOT EXISTS sys_role_permissions_bak_20261006 AS SELECT * FROM sys_role_permissions;
CREATE TABLE IF NOT EXISTS sys_roles_bak_20261006            AS SELECT * FROM sys_roles;

-- Menu: a parent under the Expenditure & Accounts heading, five pages, then action slugs (no url).
INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at) VALUES
 ('accounts.assets',                   'Fixed Assets',                    'accounts','parent', 'accounts',        NULL,                                          360,1,NOW()),
 ('accounts.assets.dashboard',         'Assets Dashboard',                'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetsDashboard.aspx',   361,1,NOW()),
 ('accounts.assets.categories',        'Asset Categories',                'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetCategories.aspx',   362,1,NOW()),
 ('accounts.assets.register',          'Assets',                          'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/Assets.aspx',            363,1,NOW()),
 ('accounts.assets.records',           'Asset Records',                   'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetRecords.aspx',      364,1,NOW()),
 ('accounts.assets.reports',           'Asset Reports',                   'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetReports.aspx',      365,1,NOW()),
 ('accounts.assets.edit',              'Fixed Assets: create and edit assets',        'accounts','subitem','accounts.assets',NULL,366,1,NOW()),
 ('accounts.assets.value',             'Fixed Assets: depreciation and revaluation',  'accounts','subitem','accounts.assets',NULL,367,1,NOW()),
 ('accounts.assets.transfer',          'Fixed Assets: transfers and status changes',  'accounts','subitem','accounts.assets',NULL,368,1,NOW()),
 ('accounts.assets.dispose',           'Fixed Assets: disposal and write-off',        'accounts','subitem','accounts.assets',NULL,369,1,NOW()),
 ('accounts.assets.categories_manage', 'Fixed Assets: manage categories',             'accounts','subitem','accounts.assets',NULL,370,1,NOW()),
 ('accounts.assets.import',            'Fixed Assets: import from Excel',             'accounts','subitem','accounts.assets',NULL,371,1,NOW()),
 ('accounts.assets.yearlock',          'Fixed Assets: lock and unlock financial years','accounts','subitem','accounts.assets',NULL,372,1,NOW())
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), parent_slug=VALUES(parent_slug),
                        sort_order=VALUES(sort_order), is_active=1;

-- New role for Estates and Stores staff who keep the register (no users assigned here).
INSERT INTO sys_roles (role_code, role_name, description, color_hex, is_system_role, is_active, created_by, created_at)
SELECT 'assets_officer','Assets Officer','Keeps the fixed asset register: records, tags, transfers and verification','#174DA4',0,1,'fixed-assets-2026-10',NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_roles WHERE role_code='assets_officer');

-- Grants (admin has every slug through the wildcard).
INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT r.id, g.slug, 1, 0, 0, 'fixed-assets-2026-10', NOW()
FROM sys_roles r
JOIN (
            SELECT 'bursar' rc,'accounts.assets' slug
  UNION ALL SELECT 'bursar','accounts.assets.dashboard'   UNION ALL SELECT 'bursar','accounts.assets.categories'
  UNION ALL SELECT 'bursar','accounts.assets.register'    UNION ALL SELECT 'bursar','accounts.assets.records'
  UNION ALL SELECT 'bursar','accounts.assets.reports'     UNION ALL SELECT 'bursar','accounts.assets.edit'
  UNION ALL SELECT 'bursar','accounts.assets.value'       UNION ALL SELECT 'bursar','accounts.assets.transfer'
  UNION ALL SELECT 'bursar','accounts.assets.dispose'     UNION ALL SELECT 'bursar','accounts.assets.categories_manage'
  UNION ALL SELECT 'bursar','accounts.assets.import'      UNION ALL SELECT 'bursar','accounts.assets.yearlock'
  UNION ALL SELECT 'accountant','accounts.assets'         UNION ALL SELECT 'accountant','accounts.assets.dashboard'
  UNION ALL SELECT 'accountant','accounts.assets.categories' UNION ALL SELECT 'accountant','accounts.assets.register'
  UNION ALL SELECT 'accountant','accounts.assets.records' UNION ALL SELECT 'accountant','accounts.assets.reports'
  UNION ALL SELECT 'accountant','accounts.assets.edit'    UNION ALL SELECT 'accountant','accounts.assets.value'
  UNION ALL SELECT 'accountant','accounts.assets.categories_manage'
  UNION ALL SELECT 'finance_officer','accounts.assets'    UNION ALL SELECT 'finance_officer','accounts.assets.dashboard'
  UNION ALL SELECT 'finance_officer','accounts.assets.categories' UNION ALL SELECT 'finance_officer','accounts.assets.register'
  UNION ALL SELECT 'finance_officer','accounts.assets.records' UNION ALL SELECT 'finance_officer','accounts.assets.reports'
  UNION ALL SELECT 'assets_officer','accounts.assets'     UNION ALL SELECT 'assets_officer','accounts.assets.dashboard'
  UNION ALL SELECT 'assets_officer','accounts.assets.categories' UNION ALL SELECT 'assets_officer','accounts.assets.register'
  UNION ALL SELECT 'assets_officer','accounts.assets.records' UNION ALL SELECT 'assets_officer','accounts.assets.reports'
  UNION ALL SELECT 'assets_officer','accounts.assets.edit' UNION ALL SELECT 'assets_officer','accounts.assets.transfer'
  UNION ALL SELECT 'assets_officer','accounts.assets.import'
  UNION ALL SELECT 'procurement','accounts.assets'        UNION ALL SELECT 'procurement','accounts.assets.register'
  UNION ALL SELECT 'procurement','accounts.assets.edit'
  UNION ALL SELECT 'auditor','accounts.assets'            UNION ALL SELECT 'auditor','accounts.assets.dashboard'
  UNION ALL SELECT 'auditor','accounts.assets.categories' UNION ALL SELECT 'auditor','accounts.assets.register'
  UNION ALL SELECT 'auditor','accounts.assets.records'    UNION ALL SELECT 'auditor','accounts.assets.reports'
  UNION ALL SELECT 'vc','accounts.assets'                 UNION ALL SELECT 'vc','accounts.assets.dashboard'
  UNION ALL SELECT 'vc','accounts.assets.register'        UNION ALL SELECT 'vc','accounts.assets.reports'
) g ON g.rc = r.role_code
WHERE NOT EXISTS (SELECT 1 FROM sys_role_permissions x WHERE x.role_id = r.id AND x.menu_slug = g.slug);
