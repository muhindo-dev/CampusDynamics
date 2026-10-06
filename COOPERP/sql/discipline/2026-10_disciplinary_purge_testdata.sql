-- ---------------------------------------------------------------------------
-- Student Disciplinary module: removes the acceptance-test data (run once before go-live, 6 Oct 2026).
-- Touches ONLY rows that belong to the two test students MRUZZTEST0001/0002 and the test users zz_dc_*.
-- The append-only guards refuse deletes, so they are dropped for this run and restored at the end by
-- re-running 2026-10_disciplinary_schema.sql (idempotent). Back up the dc_ tables first.
-- ---------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_dc_entry_bd;   DROP TRIGGER IF EXISTS trg_dc_case_bd;      DROP TRIGGER IF EXISTS trg_dc_sanction_bd;
DROP TRIGGER IF EXISTS trg_dc_audit_bd;   DROP TRIGGER IF EXISTS trg_dc_letter_bd;    DROP TRIGGER IF EXISTS trg_dc_appeal_bd;
DROP TRIGGER IF EXISTS trg_dc_incident_bd; DROP TRIGGER IF EXISTS trg_dc_attachment_bd; DROP TRIGGER IF EXISTS trg_dc_notification_bd;
DROP TRIGGER IF EXISTS trg_dc_access_bd;

DROP TABLE IF EXISTS zz_cases;
CREATE TABLE zz_cases AS SELECT id, incident_id FROM dc_case WHERE regno IN ('MRUZZTEST0001','MRUZZTEST0002');
DELETE FROM dc_access_log  WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_notification WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_letter      WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_appeal      WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_sanction    WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_hearing     WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_attachment  WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_entry       WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_audit       WHERE case_id IN (SELECT id FROM zz_cases)
                              OR (entity='INCIDENT' AND entity_id IN (SELECT incident_id FROM zz_cases));
DELETE FROM dc_case        WHERE id IN (SELECT id FROM zz_cases);
DELETE FROM dc_incident    WHERE id IN (SELECT incident_id FROM zz_cases) AND NOT EXISTS (SELECT 1 FROM dc_case c WHERE c.incident_id=dc_incident.id);
DELETE FROM acad_activity_log WHERE page_function='Student Discipline' AND user_id LIKE 'zz\_dc\_%';
-- Numbering starts again at 0001 for real cases, but only if no real case has been opened.
DELETE FROM dc_sequence WHERE NOT EXISTS (SELECT 1 FROM dc_case);
DROP TABLE zz_cases;

-- The test students and their portal logins.
DELETE FROM acad_student WHERE regno IN ('MRUZZTEST0001','MRUZZTEST0002');
DELETE m FROM campus_dynamics_portal.my_aspnet_membership m JOIN campus_dynamics_portal.my_aspnet_users u ON u.id=m.userId WHERE u.name IN ('MRUZZTEST0001','MRUZZTEST0002');
DELETE FROM campus_dynamics_portal.my_aspnet_users WHERE name IN ('MRUZZTEST0001','MRUZZTEST0002');
-- Then: re-run 2026-10_disciplinary_schema.sql to restore the guards, and delete Data_Private\Disciplinary\{case ids}.
