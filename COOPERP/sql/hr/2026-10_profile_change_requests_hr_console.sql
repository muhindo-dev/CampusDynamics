-- ============================================================================
-- Profile change requests - HR console (eadmin ProfileChangeRequests.aspx)
--   RBAC: register the page under People & Contracts, after Leave Applications (414)
--   and Contract Renewals (415), same pattern as hr.contract.renewals
--   (menu item + view/edit grant for hr_manager = role 12). Admin (1) sees
--   everything through the code wildcard.
-- Requires 2026-10_profile_change_requests.sql. Idempotent.
-- ============================================================================

INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at)
VALUES
 ('hr.profile.requests', 'Profile Requests', 'hr', 'subitem', 'hr.people', '~/COOPERP/NewScreens/ProfileChangeRequests.aspx', 416, 1, NOW())
ON DUPLICATE KEY UPDATE label = VALUES(label), url = VALUES(url), parent_slug = VALUES(parent_slug),
                        sort_order = VALUES(sort_order), is_active = 1;

INSERT IGNORE INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
VALUES
 (12, 'hr.profile.requests', 1, 1, 0, 'profile-change-requests-2026-10', NOW());
