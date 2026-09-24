-- =====================================================================
--  Graduation Centre — registering the three new pages for menu access.
--  Applied to production 2026-09-24.
--
--  The module was split from one tabbed page into four independent ones.
--  Only the original (GraduationCentre.aspx) was in sys_menu_items, and
--  the sidebar's filter treats an UNMAPPED page as always visible — so
--  without this the three new pages would have shown to every signed-in
--  user regardless of role, which is more permissive than the page they
--  were split out of.
--
--  menu_slug carries a UNIQUE index, so each page needs its own slug.
--  The grants below mirror EXACTLY the roles that already hold
--  system.more.graduation_centre: admissions, dean, exam_officer,
--  faculty_staff, hod, registrar, student_services. Nothing new is
--  granted to anybody; the same access simply follows the pages it was
--  always meant to cover. A super admin (role_code 'admin') carries the
--  '*' wildcard and never consults this table at all.
-- =====================================================================

INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at)
SELECT * FROM (
    SELECT 'academics.graduation.candidates' AS a, 'Candidates' AS b, 'academics' AS c, 'subitem' AS d,
           'academics.graduation' AS e, '~/COOPERP/NewScreens/GraduationCandidates.aspx' AS f,
           755 AS g, 1 AS h, NOW() AS i
    UNION ALL SELECT 'academics.graduation.list', 'Graduation List', 'academics', 'subitem',
           'academics.graduation', '~/COOPERP/NewScreens/GraduationList.aspx', 756, 1, NOW()
    UNION ALL SELECT 'academics.graduation.held', 'Held Candidates', 'academics', 'subitem',
           'academics.graduation', '~/COOPERP/NewScreens/GraduationHeld.aspx', 757, 1, NOW()
) x
WHERE NOT EXISTS (SELECT 1 FROM sys_menu_items m WHERE m.menu_slug = x.a);

-- Exactly the roles that can already open the Graduation Centre.
INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT rp.role_id, n.slug, 1, 0, 0, 'graduation_split', NOW()
FROM sys_role_permissions rp
CROSS JOIN (
    SELECT 'academics.graduation.candidates' AS slug
    UNION ALL SELECT 'academics.graduation.list'
    UNION ALL SELECT 'academics.graduation.held'
) n
WHERE rp.menu_slug = 'system.more.graduation_centre'
  AND rp.can_view = 1
  AND NOT EXISTS (
      SELECT 1 FROM sys_role_permissions e
      WHERE e.role_id = rp.role_id AND e.menu_slug = n.slug);

-- =====================================================================
--  To undo:
--    DELETE FROM sys_role_permissions WHERE menu_slug LIKE 'academics.graduation.%';
--    DELETE FROM sys_menu_items       WHERE menu_slug LIKE 'academics.graduation.%';
--  The sidebar then treats the three pages as unmapped, i.e. visible to
--  everyone the Graduation group itself admits.
--
--  NOTE: the sidebar caches the url->slug map in HttpRuntime.Cache under
--  "rbac_menu_map_js". An application restart (or touching web.config)
--  is needed for these rows to take effect.
-- =====================================================================
