-- Graduation Officer: the person who runs the graduation process end to end
-- (2026-10-13, for Miriam Nakitende / MiriamN). Idempotent.
--   * every Graduation Centre screen: view, edit, remove
--   * Fees Clearance: view only (clearing a student's fees is the Bursar's decision)
--   * Student Course Rearrangement: full
-- Institution-wide scope and the right to approve/hold graduands come from the role code
-- in MarksScopeResolver and GraduationRights.
USE campus_dynamics;

INSERT INTO sys_roles (role_code, role_name, description, color_hex, is_system_role, is_read_only, is_active, created_by)
SELECT 'graduation_officer', 'Graduation Officer',
       'Runs the graduation process: candidates, holds, the graduation list, analysis, transcripts and certificates; corrects course registrations. Institution-wide.',
       '#7c3aed', 0, 0, 1, 'muhindo'
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_roles WHERE role_code='graduation_officer');

SET @rid = (SELECT id FROM sys_roles WHERE role_code='graduation_officer');

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT @rid, s.slug, 1, s.e, s.d, 'muhindo', NOW() FROM (
          SELECT 'academics' slug, 0 e, 0 d
    UNION SELECT 'academics.graduation.candidates', 1, 1
    UNION SELECT 'academics.graduation.held', 1, 1
    UNION SELECT 'academics.graduation.list', 1, 1
    UNION SELECT 'academics.graduation.fees_clearance', 0, 0
    UNION SELECT 'academics.rearrange', 1, 1
    UNION SELECT 'academics.rearrange.manage', 1, 1
    UNION SELECT 'academics.rearrange.logs', 1, 1
    UNION SELECT 'academics.rearrange.dashboard', 1, 1
    UNION SELECT 'system', 0, 0
    UNION SELECT 'system.more', 0, 0
    UNION SELECT 'system.more.graduation_centre', 1, 1
    UNION SELECT 'system.more.graduating_students', 1, 1
    UNION SELECT 'system.more.graduation_analysis', 1, 1
) s
ON DUPLICATE KEY UPDATE can_view=1, can_edit=VALUES(can_edit), can_delete=VALUES(can_delete), granted_by='muhindo', granted_at=NOW();

INSERT INTO sys_user_roles (username, emp_id, role_id, assigned_by, is_active, notes)
VALUES ('MiriamN', 25, @rid, 'muhindo', 1, 'Responsible for graduation; full Graduation Centre and course rearrangement')
ON DUPLICATE KEY UPDATE is_active=1, expires_at=NULL;

SELECT r.role_code, rp.menu_slug, rp.can_view, rp.can_edit, rp.can_delete
FROM sys_role_permissions rp JOIN sys_roles r ON r.id=rp.role_id WHERE rp.role_id=@rid ORDER BY rp.menu_slug;
SELECT ur.username, r.role_code, ur.is_active FROM sys_user_roles ur JOIN sys_roles r ON r.id=ur.role_id WHERE ur.username='MiriamN';
