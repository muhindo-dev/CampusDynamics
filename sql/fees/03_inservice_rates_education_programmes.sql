-- ============================================================================
--  In-service fee structures for the education programmes
--  2026-09-03 - transcribed from the Registrar's printed fee sheets.
--
--  Convention (as instructed): the Tuition row is stored as tuition; EVERY other row
--  on the sheet is summed into the functional fee. So functional = printed Total minus
--  printed Tuition, per session. Each figure below was reconciled against the printed
--  Total for that session and matches exactly.
--
--  Recurring non-tuition items, identical on both sheets and every session:
--      Development 48,000 + Examination 24,000 + Library 24,000
--    + Computer 36,000 + Registration 12,000 + Symposium 10,000  =  154,000
--  One-off / period items folded into the session they fall in:
--      Identity Card   30,000  - Y1 S1 only ("Paid once on Entry")
--      School Practice 110,000 - DPE/DECE Y1 S3 and Y2 S3
--      Research         80,000 - final session only
--
--  SHEET 1 - BACHELOR OF EDUCATION (PRIMARY & SECONDARY), 3 years, tuition 280,000
--    progcodes BED(P), BED(S)
--      Y1S1 280,000 + (154,000 + 30,000 ID) = 464,000   [sheet: 464,000]
--      Y1S2..Y3S2 280,000 + 154,000         = 434,000   [sheet: 434,000]
--      Y3S3 280,000 + (154,000 + 80,000 res)= 514,000   [sheet: 514,000]
--
--  SHEET 2 - DIPLOMA (DPE & DECE), 2 years, tuition 230,000
--    progcodes DPE, decd  (the sheet's "DECE" is DIPLOMA IN EARLY CHILD DEVELOPMENT,
--    stored as progcode 'decd' - the only diploma-level ECD programme, 94 in-service
--    students. There is no 'DECE' code in acad_programme.)
--      Y1S1 230,000 + (154,000 + 30,000 ID)            = 414,000   [sheet: 414,000]
--      Y1S2 230,000 + 154,000                          = 384,000   [sheet: 384,000]
--      Y1S3 230,000 + (154,000 + 110,000 practice)     = 494,000   [sheet: 494,000]
--      Y2S1 230,000 + 154,000                          = 384,000   [sheet: 384,000]
--      Y2S2 230,000 + 154,000                          = 384,000   [sheet: 384,000]
--      Y2S3 230,000 + (154,000 + 110,000 + 80,000 res) = 574,000   [sheet: 574,000]
--
--  DELIBERATELY NOT INCLUDED
--  Both sheets carry "Other Charges Applicable .... Shs 20,000 for NCHE per year".
--  That is charged PER YEAR, outside the session table. fin_programme_fees is a
--  per-session structure, so folding 20,000 into a session's functional fee would bill
--  it three times a year. It belongs as its own billing item on the annual schedule.
--
--  Years the programme does not run are left at zero with has_year_N = 'No', so the
--  biller produces nothing for them.
-- ============================================================================

USE campus_dynamics_accounts;

-- -- Sheet 1: 3-year bachelors, tuition 280,000 ------------------------------
INSERT INTO fin_programme_fees
    (progcode, stud_session, has_year_1, has_year_2, has_year_3, has_year_4,
     y1_s1_tuition, y1_s1_functional, y1_s2_tuition, y1_s2_functional, y1_s3_tuition, y1_s3_functional,
     y2_s1_tuition, y2_s1_functional, y2_s2_tuition, y2_s2_functional, y2_s3_tuition, y2_s3_functional,
     y3_s1_tuition, y3_s1_functional, y3_s2_tuition, y3_s2_functional, y3_s3_tuition, y3_s3_functional,
     y4_s1_tuition, y4_s1_functional, y4_s2_tuition, y4_s2_functional, y4_s3_tuition, y4_s3_functional,
     is_active, created_by)
VALUES
 ('BED(P)','INSERVICE','Yes','Yes','Yes','No',
  280000,184000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,234000,
  0,0, 0,0, 0,0,
  'Yes','inservice-sheet-20260903'),
 ('BED(S)','INSERVICE','Yes','Yes','Yes','No',
  280000,184000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,234000,
  0,0, 0,0, 0,0,
  'Yes','inservice-sheet-20260903'),

-- -- Sheet 2: 2-year diplomas, tuition 230,000 -------------------------------
 ('DPE','INSERVICE','Yes','Yes','No','No',
  230000,184000, 230000,154000, 230000,264000,
  230000,154000, 230000,154000, 230000,344000,
  0,0, 0,0, 0,0,
  0,0, 0,0, 0,0,
  'Yes','inservice-sheet-20260903'),
 ('decd','INSERVICE','Yes','Yes','No','No',
  230000,184000, 230000,154000, 230000,264000,
  230000,154000, 230000,154000, 230000,344000,
  0,0, 0,0, 0,0,
  0,0, 0,0, 0,0,
  'Yes','inservice-sheet-20260903')
ON DUPLICATE KEY UPDATE
    has_year_1=VALUES(has_year_1), has_year_2=VALUES(has_year_2),
    has_year_3=VALUES(has_year_3), has_year_4=VALUES(has_year_4),
    y1_s1_tuition=VALUES(y1_s1_tuition), y1_s1_functional=VALUES(y1_s1_functional),
    y1_s2_tuition=VALUES(y1_s2_tuition), y1_s2_functional=VALUES(y1_s2_functional),
    y1_s3_tuition=VALUES(y1_s3_tuition), y1_s3_functional=VALUES(y1_s3_functional),
    y2_s1_tuition=VALUES(y2_s1_tuition), y2_s1_functional=VALUES(y2_s1_functional),
    y2_s2_tuition=VALUES(y2_s2_tuition), y2_s2_functional=VALUES(y2_s2_functional),
    y2_s3_tuition=VALUES(y2_s3_tuition), y2_s3_functional=VALUES(y2_s3_functional),
    y3_s1_tuition=VALUES(y3_s1_tuition), y3_s1_functional=VALUES(y3_s1_functional),
    y3_s2_tuition=VALUES(y3_s2_tuition), y3_s2_functional=VALUES(y3_s2_functional),
    y3_s3_tuition=VALUES(y3_s3_tuition), y3_s3_functional=VALUES(y3_s3_functional),
    y4_s1_tuition=VALUES(y4_s1_tuition), y4_s1_functional=VALUES(y4_s1_functional),
    y4_s2_tuition=VALUES(y4_s2_tuition), y4_s2_functional=VALUES(y4_s2_functional),
    y4_s3_tuition=VALUES(y4_s3_tuition), y4_s3_functional=VALUES(y4_s3_functional),
    is_active='Yes', created_by=VALUES(created_by);
