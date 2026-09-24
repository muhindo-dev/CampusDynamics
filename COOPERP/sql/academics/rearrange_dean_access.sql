-- =====================================================================
--  Student Course Rearrangement — open it to Deans.
--  Applied to production 2026-09-24.
--
--  The module was granted to admin and hod only. Deans were left out, which
--  was never deliberate - it was simply not done when the slugs were created.
--
--  WHY THIS IS SAFE TO OPEN.
--
--  1. It is already scoped by identity, not by trust. StudentRearrangeService
--     resolves MarksScopeResolver and refuses to OPEN a student outside the
--     caller's programmes - "a HOD may not open a student outside their
--     department even to look". A Dean therefore gets their own faculty and
--     nothing else, automatically, with no further restriction needed.
--
--  2. It is one of the best-instrumented parts of the system. Every change
--     writes acad_rearrange_log with before_json and after_json, the actor's
--     username, name, role, IP and user-agent, the written reason, any
--     override and its kind, and a link to the entry it reverses. Failed
--     attempts are recorded too, in acad_rearrange_attempt. At the time of
--     this change: 198 log rows, 43 batches, 60 attempts, 58 sessions.
--
--  Access is slug-driven end to end - the pages call
--  RoleAccessService.RequireSlug - so these three rows are the whole change.
--  No code is touched.
-- ---------------------------------------------------------------------
--  REGISTRAR IS DELIBERATELY NOT INCLUDED, and not out of caution.
--
--  MarksScopeResolver derives scope from WHO you are: a super admin gets
--  everything, a Dean is matched against acad_faculty.faculty_dean, a HOD
--  against their department, and anyone else falls through to Mode = "none".
--  Registrar is not one of those cases, so a registrar granted this slug
--  would open the page and immediately be told "You do not have a
--  marks-management scope" - a dead end that looks like a bug.
--
--  Giving registrars real access means teaching MarksScopeResolver what a
--  registrar's scope is (all programmes, presumably). That is a change to
--  how scope works across the whole marks estate, not a permission row, and
--  it should be decided on purpose rather than smuggled in here.
-- =====================================================================

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT r.id, n.slug, 1, 0, 0, 'rearrange_dean_access', NOW()
FROM sys_roles r
CROSS JOIN (
    SELECT 'academics.rearrange.dashboard' AS slug
    UNION ALL SELECT 'academics.rearrange.manage'
    UNION ALL SELECT 'academics.rearrange.logs'
) n
WHERE r.role_code = 'dean'
  AND r.is_active = 1
  AND NOT EXISTS (
      SELECT 1 FROM sys_role_permissions e
      WHERE e.role_id = r.id AND e.menu_slug = n.slug);

-- =====================================================================
--  To undo:
--    DELETE FROM sys_role_permissions
--     WHERE granted_by = 'rearrange_dean_access';
--
--  NOTE: the sidebar caches the url->slug map in HttpRuntime.Cache
--  ("rbac_menu_map_js"), and a user's own slug set is cached in their
--  session and refreshed every few minutes by RoleAccessService.MaybeRefresh.
--  A Dean already signed in picks this up on their next refresh without
--  having to sign in again.
-- =====================================================================
