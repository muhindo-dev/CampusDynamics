-- ============================================================================
--  Mark entry: open for in-service and summer school, 2025/2026 and 2026/2027.
--  Everything else stays closed. 2026-09-03.
--
--  Why a SESSION scope was needed
--  ------------------------------
--  In-service is a delivery MODE, not a programme. Every programme that runs
--  in-service also runs day or weekend (13 programmes, none exclusively
--  in-service), so a PROGRAMME-scoped rule cannot open mark entry for the
--  in-service cohort without also opening it for the day cohort of the same
--  programme. acad_exam_config.scope_type therefore gained SESSION, matched
--  against the study session on the teaching allocation / course registration.
--
--  Summer school = semester 3, the recess semester. In 2026/2027 semester 3 is
--  entirely in-service; in 2025/2026 it also carries 92 day allocations across
--  3 programmes -- that is the summer school. Semester was already a scope
--  dimension, so this needs no new machinery.
--
--  Precedence (ExamConfig.Resolve):
--      PROGRAMME > FACULTY > CAMPUS > SESSION > GLOBAL,
--      then a row naming a year beats one that does not,
--      then a row naming a semester beats one that does not.
--
--  Both the on/off switch AND the date window are set at each scope. Setting
--  only the switch would leave the university-wide window (which closed on
--  2026-08-31) still blocking entry. An empty date means "no limit" -- see
--  ExamConfig.GetDateTime, where an empty value is deliberately treated as no
--  restriction.
-- ============================================================================

INSERT INTO acad_exam_config
    (config_key, scope_type, scope_value, acad_year, semester, value_type, config_value, is_active, notes, updated_by, updated_at)
VALUES
 ('coursework.entry.enabled','SESSION','INSERVICE','2025/2026',0,'BOOL','1',1,'In-service mark entry open','system-config-20260903',NOW()),
 ('exam.entry.enabled',      'SESSION','INSERVICE','2025/2026',0,'BOOL','1',1,'In-service mark entry open','system-config-20260903',NOW()),
 ('coursework.entry.opens',  'SESSION','INSERVICE','2025/2026',0,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('coursework.entry.closes', 'SESSION','INSERVICE','2025/2026',0,'DATETIME','',1,'No deadline','system-config-20260903',NOW()),
 ('exam.entry.opens',        'SESSION','INSERVICE','2025/2026',0,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('exam.entry.closes',       'SESSION','INSERVICE','2025/2026',0,'DATETIME','',1,'No deadline','system-config-20260903',NOW()),
 ('coursework.entry.enabled','SESSION','INSERVICE','2026/2027',0,'BOOL','1',1,'In-service mark entry open','system-config-20260903',NOW()),
 ('exam.entry.enabled',      'SESSION','INSERVICE','2026/2027',0,'BOOL','1',1,'In-service mark entry open','system-config-20260903',NOW()),
 ('coursework.entry.opens',  'SESSION','INSERVICE','2026/2027',0,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('coursework.entry.closes', 'SESSION','INSERVICE','2026/2027',0,'DATETIME','',1,'No deadline','system-config-20260903',NOW()),
 ('exam.entry.opens',        'SESSION','INSERVICE','2026/2027',0,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('exam.entry.closes',       'SESSION','INSERVICE','2026/2027',0,'DATETIME','',1,'No deadline','system-config-20260903',NOW()),
 ('coursework.entry.enabled','GLOBAL','','2025/2026',3,'BOOL','1',1,'Summer school mark entry open','system-config-20260903',NOW()),
 ('exam.entry.enabled',      'GLOBAL','','2025/2026',3,'BOOL','1',1,'Summer school mark entry open','system-config-20260903',NOW()),
 ('coursework.entry.opens',  'GLOBAL','','2025/2026',3,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('coursework.entry.closes', 'GLOBAL','','2025/2026',3,'DATETIME','',1,'No deadline','system-config-20260903',NOW()),
 ('exam.entry.opens',        'GLOBAL','','2025/2026',3,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('exam.entry.closes',       'GLOBAL','','2025/2026',3,'DATETIME','',1,'No deadline','system-config-20260903',NOW()),
 ('coursework.entry.enabled','GLOBAL','','2026/2027',3,'BOOL','1',1,'Summer school mark entry open','system-config-20260903',NOW()),
 ('exam.entry.enabled',      'GLOBAL','','2026/2027',3,'BOOL','1',1,'Summer school mark entry open','system-config-20260903',NOW()),
 ('coursework.entry.opens',  'GLOBAL','','2026/2027',3,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('coursework.entry.closes', 'GLOBAL','','2026/2027',3,'DATETIME','',1,'No deadline','system-config-20260903',NOW()),
 ('exam.entry.opens',        'GLOBAL','','2026/2027',3,'DATETIME','',1,'No start limit','system-config-20260903',NOW()),
 ('exam.entry.closes',       'GLOBAL','','2026/2027',3,'DATETIME','',1,'No deadline','system-config-20260903',NOW())
ON DUPLICATE KEY UPDATE
    config_value = VALUES(config_value),
    value_type   = VALUES(value_type),
    is_active    = 1,
    notes        = VALUES(notes),
    updated_by   = VALUES(updated_by),
    updated_at   = NOW();
