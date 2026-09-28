-- ---------------------------------------------------------------------------
-- Sixteen sidebar links had no row in sys_menu_items.
--
-- The menu filter maps a link's file name to a slug and skips anything it cannot
-- map: "unmapped page -> keep visible". So these sixteen were shown to every
-- signed-in user whatever their role, and clicking one gave whatever the page
-- itself decided, which for a read-only user is now a refusal. The menu was
-- advertising doors that do not open.
--
-- Registering them closes the gap for every role at once, not only the Auditor.
--
-- BUT registering a page also makes it access-controlled for the first time, and
-- every role that can see it today would lose it. So the second statement grants
-- each new slug to the roles that already hold ground in the same section, which
-- leaves every existing role seeing exactly what it sees now. The Auditor is
-- excluded by name: these are operational screens, and not seeing them is the
-- entire point of the role.
-- ---------------------------------------------------------------------------

INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at)
SELECT * FROM (
    SELECT 'fees.fee_admin.billing_health'      AS s, 'Billing Health'            AS l, 'fees'      AS sec, 'subitem' AS t, NULL AS p, '~/COOPERP/NewScreens/BillingHealth.aspx'           AS u, 910 AS o, 1 AS a, NOW() AS c UNION ALL
    SELECT 'fees.fee_admin.billing_reconcile',       'Billing Reconciliation',        'fees',        'subitem', NULL, '~/COOPERP/NewScreens/BillingReconciliation.aspx',   911, 1, NOW() UNION ALL
    SELECT 'fees.fee_admin.schoolpay',               'SchoolPay Controller',          'fees',        'subitem', NULL, '~/COOPERP/NewScreens/SchoolPayController.aspx',     912, 1, NOW() UNION ALL
    SELECT 'academics.exam.course_deletions',        'Course Deletion Requests',      'academics',   'subitem', NULL, '~/COOPERP/NewScreens/CourseDeletionRequests.aspx',  913, 1, NOW() UNION ALL
    SELECT 'academics.exam.configuration',           'Exam Configuration',            'academics',   'subitem', NULL, '~/COOPERP/NewScreens/ExamConfiguration.aspx',       914, 1, NOW() UNION ALL
    SELECT 'academics.exam.missing_marks',           'Missing Marks',                 'academics',   'subitem', NULL, '~/COOPERP/NewScreens/MissingMarks.aspx',            915, 1, NOW() UNION ALL
    SELECT 'academics.exam.results_exporter',        'Results Exporter',              'academics',   'subitem', NULL, '~/COOPERP/NewScreens/ResultsExporter.aspx',         916, 1, NOW() UNION ALL
    SELECT 'academics.exam.retakes',                 'Retake Controller',             'academics',   'subitem', NULL, '~/COOPERP/NewScreens/RetakeController.aspx',        917, 1, NOW() UNION ALL
    SELECT 'academics.timetable.calendar',           'Timetable Calendar',            'academics',   'subitem', NULL, '~/COOPERP/NewScreens/TimetableCalendar.aspx',       918, 1, NOW() UNION ALL
    SELECT 'academics.timetable.manager',            'Timetable Manager',             'academics',   'subitem', NULL, '~/COOPERP/NewScreens/TimetableManager.aspx',        919, 1, NOW() UNION ALL
    SELECT 'academics.timetable.rooms',              'Rooms & Buildings',             'academics',   'subitem', NULL, '~/COOPERP/NewScreens/RoomsBuildings.aspx',          920, 1, NOW() UNION ALL
    SELECT 'system.more.idcard_controller',          'ID Card Controller',            'system',      'subitem', NULL, '~/COOPERP/NewScreens/IDCardController.aspx',        921, 1, NOW() UNION ALL
    SELECT 'system.more.photo_changes',              'Photo Change Controller',       'system',      'subitem', NULL, '~/COOPERP/NewScreens/PhotoChangeController.aspx',   922, 1, NOW() UNION ALL
    SELECT 'system.more.student_email',              'Student Email Controller',      'system',      'subitem', NULL, '~/COOPERP/NewScreens/StudentEmailController.aspx',  923, 1, NOW() UNION ALL
    SELECT 'system.more.odel_dashboard',             'ODEL Dashboard',                'system',      'subitem', NULL, '~/COOPERP/NewScreens/OdelDashboard.aspx',           924, 1, NOW() UNION ALL
    SELECT 'system.more.odel_policy',                'ODEL Policy',                   'system',      'subitem', NULL, '~/COOPERP/NewScreens/OdelPolicy.aspx',              925, 1, NOW()
) x
WHERE NOT EXISTS (SELECT 1 FROM sys_menu_items m WHERE m.menu_slug = x.s);

-- Preserve what every existing role can see today. A role that already holds any
-- slug in a section keeps seeing that section's newly registered pages.
INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT DISTINCT r.id, n.menu_slug, 1, 1, 0, 'system:preserve-existing-visibility', NOW()
  FROM sys_roles r
  JOIN sys_menu_items n
    ON n.sort_order BETWEEN 910 AND 925 AND n.is_active = 1
  JOIN sys_role_permissions p ON p.role_id = r.id
  JOIN sys_menu_items m      ON m.menu_slug = p.menu_slug AND m.section = n.section
 WHERE r.is_active = 1
   AND r.role_code <> 'auditor'
   AND IFNULL(r.is_read_only,0) = 0
   AND NOT EXISTS (SELECT 1 FROM sys_role_permissions q
                    WHERE q.role_id = r.id AND q.menu_slug = n.menu_slug);

SELECT CONCAT('registered ', (SELECT COUNT(*) FROM sys_menu_items WHERE sort_order BETWEEN 910 AND 925),
              ' pages; preserved for ',
              (SELECT COUNT(DISTINCT role_id) FROM sys_role_permissions
                WHERE granted_by = 'system:preserve-existing-visibility'),
              ' roles') AS result;
