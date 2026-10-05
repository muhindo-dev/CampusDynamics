-- ============================================================================
-- Performance Appraisal: register the two catalogue screens in the RBAC menu
-- (sidebar: Performance Appraisal > Competency Templates / Expected Standards).
-- Same pattern as hr.appraisal.* : menu item + view grant for hr_manager (12).
-- Admin (1) sees everything through the code wildcard. Idempotent.
-- ============================================================================
INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at)
VALUES
 ('hr.appraisal.competencies', 'Competency Templates', 'hr', 'subitem', 'hr.appraisal', '~/COOPERP/NewScreens/CompetencyTemplates.aspx', 445, 1, NOW()),
 ('hr.appraisal.standards',    'Expected Standards',   'hr', 'subitem', 'hr.appraisal', '~/COOPERP/NewScreens/ExpectedStandards.aspx',   446, 1, NOW())
ON DUPLICATE KEY UPDATE label = VALUES(label), url = VALUES(url), parent_slug = VALUES(parent_slug),
                        sort_order = VALUES(sort_order), is_active = 1;

INSERT IGNORE INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
VALUES
 (12, 'hr.appraisal.competencies', 1, 1, 0, 'appraisal-2026-10', NOW()),
 (12, 'hr.appraisal.standards',    1, 1, 0, 'appraisal-2026-10', NOW());
