-- ---------------------------------------------------------------------------
-- Extra Auditor access, and the menu items five of these pages never had.
--
-- GeneralDashboard, NewStudentInfo, AdmissionAnalysis, AlumniDataBank and
-- MarksActionLog are live screens with NO row in sys_menu_items. That had two
-- consequences, both bad: they could not be granted to anybody, because a grant
-- is a row against a menu_slug; and the sidebar filter skipped their links
-- entirely ("unmapped page -> keep visible"), so every user saw them whatever
-- their role. Registering them fixes both at once.
--
-- Knowledgebase.aspx is the READER. The slug that already existed,
-- system.more.knowledgebase, points at KnowledgebaseManagement.aspx, which is
-- the editor. They are different screens and only the reader belongs here.
-- ---------------------------------------------------------------------------

-- 1. Register the missing pages -------------------------------------------
--    is_active = 1 so the sidebar filter can see them; they appear in nobody's
--    menu until a role is granted them, which is the point.
INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at)
SELECT * FROM (
    SELECT 'system.more.general_dashboard'   AS s, 'General Dashboard'        AS l, 'system'    AS sec, 'subitem' AS t, NULL AS p, '~/COOPERP/NewScreens/GeneralDashboard.aspx'  AS u, 900 AS o, 1 AS a, NOW() AS c UNION ALL
    SELECT 'system.more.knowledgebase_read',       'Knowledgebase',               'system',        'subitem', NULL, '~/COOPERP/NewScreens/Knowledgebase.aspx',        901, 1, NOW() UNION ALL
    SELECT 'academics.students.info',              'Student Information',         'academics',     'subitem', NULL, '~/COOPERP/NewScreens/NewStudentInfo.aspx',       902, 1, NOW() UNION ALL
    SELECT 'academics.admissions.analysis',        'Admission Analysis',          'academics',     'subitem', NULL, '~/COOPERP/NewScreens/AdmissionAnalysis.aspx',    903, 1, NOW() UNION ALL
    -- academics.students.alumni already existed and points at AlumniStudents.aspx, which is
    -- a different screen; the data bank needs its own slug or it stays unmapped.
    SELECT 'academics.students.alumni_databank', 'Alumni Data Bank',            'academics',     'subitem', NULL, '~/COOPERP/NewScreens/AlumniDataBank.aspx',       904, 1, NOW() UNION ALL
    SELECT 'academics.exam.marks_action_log',      'Marks Action Log',            'academics',     'subitem', NULL, '~/COOPERP/NewScreens/MarksActionLog.aspx',       905, 1, NOW()
) x
WHERE NOT EXISTS (SELECT 1 FROM sys_menu_items m WHERE m.menu_slug = x.s);

-- 2. Grant them, plus the related read-only screens in the same families ----
SET @rid := (SELECT id FROM sys_roles WHERE role_code = 'auditor' LIMIT 1);

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT @rid, m.menu_slug, 1, 0, 0, 'system', NOW()
  FROM sys_menu_items m
 WHERE m.is_active = 1
   AND m.menu_slug IN (
        -- asked for by name
        'system.more.general_dashboard',
        'system.more.knowledgebase_read',
        'academics.students.info',
        'academics.admissions.analysis',
        'academics.students.alumni',
        'academics.students.alumni_databank',
        'academics.exam.marks_action_log',

        -- related, and read-only by nature: the rest of the admissions and
        -- student picture an auditor needs to make sense of the analysis above
        'academics.students.registered',
        'academics.admissions.applicants',
        'academics.admissions.admitted',
        'academics.exam.published',
        'academics.exam.provisional_marks',
        'fees.transactions.all',
        'fees.fee_admin.access_checker',
        'accounts.control.account_lifecycle',
        'hr.appraisal.sessions'
   )
   AND NOT EXISTS (SELECT 1 FROM sys_role_permissions p
                    WHERE p.role_id = @rid AND p.menu_slug = m.menu_slug);

SELECT CONCAT('auditor now holds ',
              (SELECT COUNT(*) FROM sys_role_permissions WHERE role_id = @rid),
              ' slugs') AS result;
