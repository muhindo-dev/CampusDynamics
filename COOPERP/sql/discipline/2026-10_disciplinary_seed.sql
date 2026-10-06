-- ---------------------------------------------------------------------------
-- Student Disciplinary module: starting case types, sanctions, defaults and letter templates
-- File: COOPERP/sql/discipline/2026-10_disciplinary_seed.sql
-- RUN ONLY AFTER THE LISTS IN THE PLAN (section 3) ARE APPROVED. Insert-only, idempotent.
-- ---------------------------------------------------------------------------

INSERT IGNORE INTO dc_sanction_type
 (code, name, description, outcome, effects, needs_amount, needs_dates, needs_course, needs_semester, allowed_interim, sort_order, created_by, created_at) VALUES
 ('WARN_VERBAL',  'Verbal warning',                     'A recorded verbal warning.',                                   'SANCTION', '',                 0,'NONE',   0,0,0, 10,'seed',NOW()),
 ('WARN_WRITTEN', 'Written warning',                    'A written warning placed on the student file.',                'SANCTION', '',                 0,'NONE',   0,0,0, 20,'seed',NOW()),
 ('COMMUNITY',    'Community service',                  'Supervised community service; give the hours and period in the terms.', 'SANCTION', '',        0,'FROM_TO',0,0,0, 30,'seed',NOW()),
 ('FINE',         'Fine',                               'A fine payable to the University. The Bursar raises the bill.', 'SANCTION', 'FINE',            1,'NONE',   0,0,0, 40,'seed',NOW()),
 ('RESTITUTION',  'Restitution',                        'Payment to make good loss or damage. The Bursar raises the bill.', 'SANCTION', 'RESTITUTION',  1,'NONE',   0,0,0, 50,'seed',NOW()),
 ('CANCEL_PAPER', 'Cancellation of a paper result',     'The result of one paper is cancelled through the marks screens.', 'SANCTION', 'CANCEL_PAPER',  0,'NONE',   1,1,0, 60,'seed',NOW()),
 ('CANCEL_SEM',   'Cancellation of a semester''s results', 'All results of one semester are cancelled through the marks screens.', 'SANCTION', 'CANCEL_SEMESTER', 0,'NONE', 0,1,0, 70,'seed',NOW()),
 ('SUSPENSION',   'Suspension',                         'Suspended from studies for the period; cannot register or sit examinations.', 'SANCTION', 'SUSPENSION', 0,'FROM_TO',0,0,1, 80,'seed',NOW()),
 ('EXPULSION',    'Expulsion',                          'Expelled from the University.',                                'SANCTION', 'EXPULSION,PORTAL_BLOCK,GRADUATION_BAR,RESULTS_WITHHELD', 0,'FROM',0,0,0, 90,'seed',NOW()),
 ('WITHHOLD',     'Withholding of results or transcript','Results, the examination card and documents are withheld for the period.', 'SANCTION', 'RESULTS_WITHHELD', 0,'FROM_TO',0,0,1,100,'seed',NOW()),
 ('BAR_GRAD',     'Barring from graduation',            'Cannot be cleared for graduation for the period.',             'SANCTION', 'GRADUATION_BAR',   0,'FROM_TO',0,0,0,110,'seed',NOW()),
 ('PORTAL_BLOCK', 'Portal access blocked',              'The student portal shows only the restriction notice and the student''s own case.', 'SANCTION', 'PORTAL_BLOCK', 0,'FROM_TO',0,0,1,120,'seed',NOW()),
 ('DISMISSAL',    'Dismissal of case',                  'The case is dismissed.',                                       'DISMISSAL','',                 0,'NONE',   0,0,0,130,'seed',NOW()),
 ('ACQUITTAL',    'Acquittal',                          'The student is found not responsible.',                        'ACQUITTAL','',                 0,'NONE',   0,0,0,140,'seed',NOW());

INSERT IGNORE INTO dc_case_type (code, name, description, severity, is_exam_related, default_restricted, sort_order, created_by, created_at) VALUES
 ('EXAM_MALPRACTICE', 'Examination malpractice',                         'Unauthorised material, copying, collusion or misconduct in an examination.', 'SERIOUS', 1, 0,  10, 'seed', NOW()),
 ('IMPERSONATION',    'Impersonation',                                    'Sitting an examination or assessment for another person, or being sat for.', 'GROSS',  1, 0,  20, 'seed', NOW()),
 ('FORGERY',          'Forgery of documents or results',                  'Forged or altered academic documents, results or University records.',     'GROSS',   0, 0,  30, 'seed', NOW()),
 ('FEES_FRAUD',       'Fees fraud or forged payment evidence',            'Forged receipts, bank slips or payment evidence.',                          'GROSS',   0, 0,  40, 'seed', NOW()),
 ('THEFT_DAMAGE',     'Theft or damage to University property',           NULL,                                                                       'SERIOUS', 0, 0,  50, 'seed', NOW()),
 ('VIOLENCE',         'Violence, assault or threats',                     NULL,                                                                       'GROSS',   0, 0,  60, 'seed', NOW()),
 ('SEXUAL_MISCONDUCT','Sexual harassment or misconduct',                  'Handled as a restricted case by default.',                                 'GROSS',   0, 1,  70, 'seed', NOW()),
 ('DRUGS_ALCOHOL',    'Drug or alcohol abuse on campus',                  NULL,                                                                       'SERIOUS', 0, 0,  80, 'seed', NOW()),
 ('STAFF_INDISCIPLINE','Indiscipline towards staff',                      NULL,                                                                       'SERIOUS', 0, 0,  90, 'seed', NOW()),
 ('ICT_MISUSE',       'Breach of ICT or portal use policy',               'Account sharing, unauthorised access, misuse of University systems.',       'MINOR',   0, 0, 100, 'seed', NOW()),
 ('PLAGIARISM',       'Plagiarism or academic dishonesty',                'Outside examinations: coursework, reports, dissertations.',                 'SERIOUS', 1, 0, 110, 'seed', NOW()),
 ('UNAUTH_PROTEST',   'Unauthorised protest or disruption',               NULL,                                                                       'SERIOUS', 0, 0, 120, 'seed', NOW()),
 ('OTHER',            'Other',                                            'Any other breach of the student regulations; describe it in full.',         'MINOR',   0, 0, 130, 'seed', NOW());

-- Default sanctions per case type. Dismissal and acquittal are always available.
INSERT IGNORE INTO dc_case_type_sanction (case_type_id, sanction_type_id, sort_order)
SELECT ct.id, st.id, st.sort_order FROM dc_case_type ct JOIN dc_sanction_type st ON
  (st.code IN ('DISMISSAL','ACQUITTAL'))
  OR (ct.code = 'EXAM_MALPRACTICE'   AND st.code IN ('WARN_WRITTEN','CANCEL_PAPER','CANCEL_SEM','SUSPENSION','EXPULSION','WITHHOLD'))
  OR (ct.code = 'IMPERSONATION'      AND st.code IN ('CANCEL_PAPER','CANCEL_SEM','SUSPENSION','EXPULSION','BAR_GRAD','WITHHOLD'))
  OR (ct.code = 'FORGERY'            AND st.code IN ('CANCEL_SEM','SUSPENSION','EXPULSION','WITHHOLD','BAR_GRAD','PORTAL_BLOCK'))
  OR (ct.code = 'FEES_FRAUD'         AND st.code IN ('RESTITUTION','FINE','WITHHOLD','SUSPENSION','EXPULSION','PORTAL_BLOCK'))
  OR (ct.code = 'THEFT_DAMAGE'       AND st.code IN ('WARN_WRITTEN','RESTITUTION','FINE','COMMUNITY','SUSPENSION','EXPULSION'))
  OR (ct.code = 'VIOLENCE'           AND st.code IN ('WARN_WRITTEN','COMMUNITY','SUSPENSION','EXPULSION'))
  OR (ct.code = 'SEXUAL_MISCONDUCT'  AND st.code IN ('WARN_WRITTEN','SUSPENSION','EXPULSION','BAR_GRAD'))
  OR (ct.code = 'DRUGS_ALCOHOL'      AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','COMMUNITY','FINE','SUSPENSION','EXPULSION'))
  OR (ct.code = 'STAFF_INDISCIPLINE' AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','COMMUNITY','SUSPENSION'))
  OR (ct.code = 'ICT_MISUSE'         AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','PORTAL_BLOCK','FINE','SUSPENSION'))
  OR (ct.code = 'PLAGIARISM'         AND st.code IN ('WARN_WRITTEN','CANCEL_PAPER','CANCEL_SEM','SUSPENSION'))
  OR (ct.code = 'UNAUTH_PROTEST'     AND st.code IN ('WARN_WRITTEN','COMMUNITY','FINE','SUSPENSION','EXPULSION'))
  OR (ct.code = 'OTHER'              AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','COMMUNITY','FINE','SUSPENSION'));

-- Letter templates. Merge fields: {{student_name}} {{regno}} {{programme}} {{faculty}} {{campus}} {{case_no}}
-- {{case_type}} {{incident_date}} {{incident_place}} {{hearing_date}} {{hearing_time}} {{hearing_venue}}
-- {{decision_text}} {{sanctions}} {{appeal_deadline}} {{appeal_window_days}} {{appellate_authority}}
-- {{sanction_lifted}} {{lift_reason}} {{suspension_from}} {{suspension_to}} {{contact_office}} {{today}}
INSERT IGNORE INTO dc_letter_template (code, name, subject, body, student_copy, sort_order, created_by, created_at) VALUES
('SUMMON', 'Summon to appear', 'SUMMONS TO APPEAR BEFORE THE STUDENTS DISCIPLINARY COMMITTEE',
'You are required to appear before the Students Disciplinary Committee on {{hearing_date}} at {{hearing_time}} in {{hearing_venue}}.

The Committee will hear the matter of {{case_type}} alleged to have taken place on {{incident_date}} at {{incident_place}} (case {{case_no}}).

You may bring any evidence and witnesses you wish the Committee to consider, and you may make a written statement. If you do not appear, the Committee may hear the matter in your absence.

Please confirm receipt of this summons with the {{contact_office}}.', 1, 10, 'seed', NOW()),
('DECISION', 'Notice of decision', 'NOTICE OF DECISION OF THE STUDENTS DISCIPLINARY COMMITTEE',
'The Students Disciplinary Committee considered case {{case_no}}, {{case_type}}, and reached the following decision.

{{decision_text}}

Sanctions: {{sanctions}}

You may appeal against this decision to the {{appellate_authority}} within {{appeal_window_days}} days, that is by {{appeal_deadline}}. You can lodge an appeal through My Disciplinary Cases on the student portal, or in writing to the {{contact_office}}.', 1, 20, 'seed', NOW()),
('SUSPENSION', 'Suspension letter', 'SUSPENSION FROM THE UNIVERSITY',
'Following the decision in case {{case_no}}, you are suspended from the University from {{suspension_from}} to {{suspension_to}}.

During this period you may not register for courses, attend classes or sit examinations, and you should not be on University premises without the written permission of the {{contact_office}}.

You may appeal against this decision to the {{appellate_authority}} by {{appeal_deadline}}.', 1, 30, 'seed', NOW()),
('LIFTING', 'Lifting of sanction', 'LIFTING OF DISCIPLINARY SANCTION',
'This is to inform you that the following sanction in case {{case_no}} has been lifted with effect from {{today}}: {{sanction_lifted}}.

Reason: {{lift_reason}}', 1, 40, 'seed', NOW()),
('CLEARANCE', 'Clearance letter', 'DISCIPLINARY CLEARANCE',
'This is to certify that, as at {{today}}, {{student_name}} ({{regno}}), a student of {{programme}}, has no pending disciplinary case and no disciplinary sanction in force at Muteesa I Royal University.', 1, 50, 'seed', NOW()),
('APPEAL_DECISION', 'Appeal decision', 'DECISION ON YOUR APPEAL',
'The {{appellate_authority}} considered your appeal in case {{case_no}} and decided as follows.

{{decision_text}}

Sanctions now in force: {{sanctions}}

This decision is final within the University.', 1, 60, 'seed', NOW());
