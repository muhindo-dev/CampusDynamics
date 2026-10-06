-- ---------------------------------------------------------------------------
-- Student Disciplinary module: menu, permission slugs, roles and grants (database campus_dynamics)
-- File: COOPERP/sql/discipline/2026-10_disciplinary_menu.sql
-- Backs up the three RBAC tables first. Idempotent.
-- Undo: DELETE FROM sys_role_permissions WHERE granted_by='disciplinary-2026-10';
--       UPDATE sys_menu_items SET is_active=0 WHERE menu_slug LIKE 'discipline%';
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS sys_menu_items_bak_dc2026       AS SELECT * FROM sys_menu_items;
CREATE TABLE IF NOT EXISTS sys_role_permissions_bak_dc2026 AS SELECT * FROM sys_role_permissions;
CREATE TABLE IF NOT EXISTS sys_roles_bak_dc2026            AS SELECT * FROM sys_roles;

INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at) VALUES
 ('discipline',                 'Student Discipline',          'academics','parent', 'academics',  NULL,                                                 930,1,NOW()),
 ('discipline.dashboard',       'Disciplinary Dashboard',      'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryDashboard.aspx',    931,1,NOW()),
 ('discipline.records',         'Disciplinary Records',        'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryRecords.aspx',      932,1,NOW()),
 ('discipline.updates',         'Record Updates',              'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryUpdates.aspx',      933,1,NOW()),
 ('discipline.settings',        'Case Types and Sanctions',    'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinarySettings.aspx',     934,1,NOW()),
 ('discipline.reports',         'Disciplinary Reports',        'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryReports.aspx',      935,1,NOW()),
 ('discipline.report',          'Discipline: report a case (own cases)',               'academics','subitem','discipline',NULL,936,1,NOW()),
 ('discipline.manage',          'Discipline: manage cases in own faculty or department','academics','subitem','discipline',NULL,937,1,NOW()),
 ('discipline.manage_exam',     'Discipline: manage examination cases (all faculties)', 'academics','subitem','discipline',NULL,938,1,NOW()),
 ('discipline.manage_all',      'Discipline: manage all cases',                         'academics','subitem','discipline',NULL,939,1,NOW()),
 ('discipline.view_all',        'Discipline: see all cases, read only',                 'academics','subitem','discipline',NULL,946,1,NOW()),
 ('discipline.hearing',         'Discipline: schedule hearings and issue letters',      'academics','subitem','discipline',NULL,940,1,NOW()),
 ('discipline.decide',          'Discipline: record hearings and decisions',            'academics','subitem','discipline',NULL,941,1,NOW()),
 ('discipline.appeal',          'Discipline: decide appeals',                           'academics','subitem','discipline',NULL,942,1,NOW()),
 ('discipline.restrict',        'Discipline: portal blocks, interim measures, vary or lift sanctions, withdraw cases','academics','subitem','discipline',NULL,943,1,NOW()),
 ('discipline.restricted',      'Discipline: see restricted cases',                     'academics','subitem','discipline',NULL,944,1,NOW()),
 ('discipline.settings_manage', 'Discipline: change settings',                          'academics','subitem','discipline',NULL,945,1,NOW())
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), parent_slug=VALUES(parent_slug), sort_order=VALUES(sort_order), is_active=1;

INSERT INTO sys_roles (role_code, role_name, description, color_hex, is_system_role, is_active, created_by, created_at)
SELECT 'dean_students','Dean of Students','Manages student discipline: all cases, hearings, letters and restrictions','#05275C',0,1,'disciplinary-2026-10',NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_roles WHERE role_code='dean_students');
INSERT INTO sys_roles (role_code, role_name, description, color_hex, is_system_role, is_active, created_by, created_at)
SELECT 'dc_committee','Disciplinary Committee','Members of the Students Disciplinary Committee: hearings and decisions','#174DA4',0,1,'disciplinary-2026-10',NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_roles WHERE role_code='dc_committee');

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT r.id, g.slug, 1, 0, 0, 'disciplinary-2026-10', NOW()
FROM sys_roles r
JOIN (
            SELECT 'dean_students' rc, 'discipline' slug
  UNION ALL SELECT 'dean_students','discipline.dashboard'      UNION ALL SELECT 'dean_students','discipline.records'
  UNION ALL SELECT 'dean_students','discipline.updates'        UNION ALL SELECT 'dean_students','discipline.settings'
  UNION ALL SELECT 'dean_students','discipline.reports'        UNION ALL SELECT 'dean_students','discipline.report'
  UNION ALL SELECT 'dean_students','discipline.manage_all'     UNION ALL SELECT 'dean_students','discipline.hearing'
  UNION ALL SELECT 'dean_students','discipline.restrict'       UNION ALL SELECT 'dean_students','discipline.restricted'
  UNION ALL SELECT 'dean_students','discipline.settings_manage'
  UNION ALL SELECT 'registrar','discipline'                    UNION ALL SELECT 'registrar','discipline.dashboard'
  UNION ALL SELECT 'registrar','discipline.records'            UNION ALL SELECT 'registrar','discipline.updates'
  UNION ALL SELECT 'registrar','discipline.settings'           UNION ALL SELECT 'registrar','discipline.reports'
  UNION ALL SELECT 'registrar','discipline.report'             UNION ALL SELECT 'registrar','discipline.manage_all'
  UNION ALL SELECT 'registrar','discipline.hearing'            UNION ALL SELECT 'registrar','discipline.restrict'
  UNION ALL SELECT 'registrar','discipline.restricted'         UNION ALL SELECT 'registrar','discipline.settings_manage'
  UNION ALL SELECT 'dc_committee','discipline'                 UNION ALL SELECT 'dc_committee','discipline.dashboard'
  UNION ALL SELECT 'dc_committee','discipline.records'         UNION ALL SELECT 'dc_committee','discipline.updates'
  UNION ALL SELECT 'dc_committee','discipline.reports'         UNION ALL SELECT 'dc_committee','discipline.manage_all'
  UNION ALL SELECT 'dc_committee','discipline.decide'
  UNION ALL SELECT 'dean','discipline'                         UNION ALL SELECT 'dean','discipline.dashboard'
  UNION ALL SELECT 'dean','discipline.records'                 UNION ALL SELECT 'dean','discipline.updates'
  UNION ALL SELECT 'dean','discipline.reports'                 UNION ALL SELECT 'dean','discipline.report'
  UNION ALL SELECT 'dean','discipline.manage'
  UNION ALL SELECT 'hod','discipline'                          UNION ALL SELECT 'hod','discipline.records'
  UNION ALL SELECT 'hod','discipline.updates'                  UNION ALL SELECT 'hod','discipline.report'
  UNION ALL SELECT 'hod','discipline.manage'
  UNION ALL SELECT 'exam_officer','discipline'                 UNION ALL SELECT 'exam_officer','discipline.dashboard'
  UNION ALL SELECT 'exam_officer','discipline.records'         UNION ALL SELECT 'exam_officer','discipline.updates'
  UNION ALL SELECT 'exam_officer','discipline.reports'         UNION ALL SELECT 'exam_officer','discipline.report'
  UNION ALL SELECT 'exam_officer','discipline.manage_exam'
  UNION ALL SELECT 'faculty_staff','discipline'                UNION ALL SELECT 'faculty_staff','discipline.records'
  UNION ALL SELECT 'faculty_staff','discipline.report'
  UNION ALL SELECT 'student_services','discipline'             UNION ALL SELECT 'student_services','discipline.records'
  UNION ALL SELECT 'student_services','discipline.report'
  UNION ALL SELECT 'vc','discipline'                           UNION ALL SELECT 'vc','discipline.dashboard'
  UNION ALL SELECT 'vc','discipline.records'                   UNION ALL SELECT 'vc','discipline.reports'
  UNION ALL SELECT 'vc','discipline.appeal'          UNION ALL SELECT 'vc','discipline.view_all'
  UNION ALL SELECT 'auditor','discipline'                      UNION ALL SELECT 'auditor','discipline.dashboard'
  UNION ALL SELECT 'auditor','discipline.records'              UNION ALL SELECT 'auditor','discipline.updates'
  UNION ALL SELECT 'auditor','discipline.reports'              UNION ALL SELECT 'auditor','discipline.view_all'
) g ON g.rc = r.role_code
WHERE NOT EXISTS (SELECT 1 FROM sys_role_permissions x WHERE x.role_id = r.id AND x.menu_slug = g.slug);

-- The case file and its file handler are reached from the records list, not the sidebar. They get slugs
-- of their own so that page-gated roles (the read-only Auditor) can open them; every role that holds
-- discipline.records receives both.
INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at) VALUES
 ('discipline.case',  'Disciplinary case file',           'academics','subitem','discipline','~/COOPERP/NewScreens/DisciplinaryCase.aspx',947,1,NOW()),
 ('discipline.files', 'Disciplinary letters and evidence','academics','subitem','discipline','~/COOPERP/NewScreens/DcFile.ashx',948,1,NOW())
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), parent_slug=VALUES(parent_slug), sort_order=VALUES(sort_order), is_active=1;

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT p.role_id, s.slug, 1, 0, 0, 'disciplinary-2026-10', NOW()
FROM sys_role_permissions p JOIN (SELECT 'discipline.case' slug UNION ALL SELECT 'discipline.files') s
WHERE p.menu_slug='discipline.records'
  AND NOT EXISTS (SELECT 1 FROM sys_role_permissions x WHERE x.role_id=p.role_id AND x.menu_slug=s.slug);
