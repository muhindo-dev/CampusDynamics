-- ============================================================================
--  In-service fee structures: Business Education and Early Childhood Education
--  2026-09-03
--
--  The Bursar confirmed these programmes also run in-service on "almost similar"
--  fees. Rather than assume what "similar" means, the figures below are taken from
--  two independent lines of evidence that agree exactly:
--
--  1. The in-service TWIN already carries the rate on its MAIN structure:
--       BBEI "BACHELOR OF BUSINESS EDUCATION(INSERVICE)"  -> 280,000 / 184,000
--       BED(ECD) "BACHELOR OF EDUCATION IN EARLY CHILD HOOD DEVELOPMENT"
--                                                         -> 280,000 / 184,000
--     Both identical to the BED(P)/BED(S) in-service sheet.
--
--  2. The in-service students on BBE and BECD have actually BEEN BILLED that rate
--     for almost every session of their studies, from the legacy schedule:
--       BBE  MRU2024001266: 280,000/184,000 then 280,000/154,000 ... 280,000/234,000
--       BECD MRU2024001438: 280,000/184,000 then 280,000/154,000 ...
--     Each has exactly ONE anomalous session priced from the MAIN day rate
--     (BBE 2026/2027 Y3S2 at 630,000/933,000; BECD 2025/2026 Y2S1 at 630,000/687,000)
--     - the same failure this whole change addresses.
--
--  BBE's own billing history reproduces the BED(P) functional pattern exactly:
--  184,000 in the first session, 154,000 thereafter, 234,000 in the final session.
--  So these four programmes take the 3-year Bachelor of Education in-service sheet.
--
--  NOT DONE HERE - the masters programmes
--  MEMA (108 in-service students) and MPCHD carry two competing rates in their
--  billing history: 528,000/289,000 (817,000 a session) for 14-21 students and
--  1,585,000/914,500 (about 2,499,500) for 8-11. A three-fold difference, and the
--  payment record points the other way - 23 MEMA students have each paid over
--  2.5m, and the cohort has paid 125,691,700 against 107,259,500 billed. Inventing
--  a low in-service masters rate here would UNDER-bill 108 students by millions.
--  These need the Bursar's actual fee sheet before anything is entered.
--
--  BBEI and BED(ECD) are given explicit in-service rows even though their MAIN
--  structure already holds these figures. That changes no bill today, but it stops
--  them breaking the moment somebody edits MAIN to a day rate - which is precisely
--  what happened to BBE, BECD and decd.
-- ============================================================================

USE campus_dynamics_accounts;

INSERT INTO fin_programme_fees
    (progcode, stud_session, has_year_1, has_year_2, has_year_3, has_year_4,
     y1_s1_tuition, y1_s1_functional, y1_s2_tuition, y1_s2_functional, y1_s3_tuition, y1_s3_functional,
     y2_s1_tuition, y2_s1_functional, y2_s2_tuition, y2_s2_functional, y2_s3_tuition, y2_s3_functional,
     y3_s1_tuition, y3_s1_functional, y3_s2_tuition, y3_s2_functional, y3_s3_tuition, y3_s3_functional,
     y4_s1_tuition, y4_s1_functional, y4_s2_tuition, y4_s2_functional, y4_s3_tuition, y4_s3_functional,
     is_active, created_by)
VALUES
 ('BBE','INSERVICE','Yes','Yes','Yes','No',
  280000,184000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,234000,
  0,0, 0,0, 0,0, 'Yes','inservice-bursar-20260903'),
 ('BBEI','INSERVICE','Yes','Yes','Yes','No',
  280000,184000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,234000,
  0,0, 0,0, 0,0, 'Yes','inservice-bursar-20260903'),
 ('BECD','INSERVICE','Yes','Yes','Yes','No',
  280000,184000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,234000,
  0,0, 0,0, 0,0, 'Yes','inservice-bursar-20260903'),
 ('BED(ECD)','INSERVICE','Yes','Yes','Yes','No',
  280000,184000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,154000,
  280000,154000, 280000,154000, 280000,234000,
  0,0, 0,0, 0,0, 'Yes','inservice-bursar-20260903')
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
