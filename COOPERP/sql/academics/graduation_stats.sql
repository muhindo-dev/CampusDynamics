-- =====================================================================
--  Graduation Centre — the per-student summary the module reads from.
--  Applied to production 2026-09-24.
--
--  WHY.  Every screen in the module asked the same question of the same
--  639,185-row results table on every load: for each of ~20,800 students,
--  how many papers did they fail, how many are zero, what is their
--  credit-weighted CGPA, how far through the programme did they get.
--
--  Written as correlated subqueries that was five lookups per student,
--  roughly 73,000 of them, and the overview took 4.8 seconds. Rewritten
--  as a single aggregate pass it took 2.3 seconds. Both are far too slow
--  for a page somebody opens twenty times a day, and the second one is
--  as good as that shape gets: it has to read the whole results table.
--
--  So the answer is not a faster query, it is asking the question once.
--  This table holds the answer per student. Every list and every count
--  in the module reads it, and reads become index lookups on ~20,800
--  rows instead of an aggregate over 639,185.
--
--  STALENESS.  Marks change, so this can lag. That is deliberate and
--  bounded:
--    * the EVIDENCE PANEL and every clear/hold/release recompute the one
--      student live, straight from acad_results, so NO DECISION is ever
--      taken on a cached number;
--    * the lists and dashboards read this table and show when it was
--      last rebuilt, with a Refresh control beside it.
--  A count that is an hour old is fine. A graduation decision on an
--  hour-old mark is not, and never happens.
-- =====================================================================

CREATE TABLE IF NOT EXISTS acad_grad_stats (
  regno        VARCHAR(85) NOT NULL,
  progcode     CHAR(25)        NULL,
  plen         INT         NOT NULL DEFAULT 3   COMMENT 'programme length at refresh time',
  maxsy        INT         NOT NULL DEFAULT 0,
  courses      INT         NOT NULL DEFAULT 0,
  cu_earned    DOUBLE      NOT NULL DEFAULT 0   COMMENT 'credits from passed papers only',
  fails        INT         NOT NULL DEFAULT 0   COMMENT 'marks of 1-49',
  zero_marks   INT         NOT NULL DEFAULT 0   COMMENT 'marks of exactly 0 - usually unmarked',
  no_score     INT         NOT NULL DEFAULT 0,
  gp_num       DOUBLE      NOT NULL DEFAULT 0,
  gp_den       DOUBLE      NOT NULL DEFAULT 0,
  cgpa         DOUBLE      NOT NULL DEFAULT 0,
  first_year   CHAR(25)        NULL,
  last_year    CHAR(25)        NULL            COMMENT 'believable years only',
  is_candidate TINYINT     NOT NULL DEFAULT 0  COMMENT 'reached the final year of their programme',
  on_list      TINYINT     NOT NULL DEFAULT 0  COMMENT 'has an acad_graduands row',
  refreshed_at DATETIME    NOT NULL,
  PRIMARY KEY (regno),
  KEY idx_gs_cand (is_candidate, on_list, last_year),
  KEY idx_gs_prog (progcode, is_candidate, on_list)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- ---------------------------------------------------------------------
--  Rebuild. One pass over acad_results, grouped by student.
--
--  The year tests use the believability rule, not the bare 4-digit
--  pattern: acad_results holds 0/1, 2022/2024, 20222/2023, 2023/204 and
--  2202/2203 across eleven students, and because these are character
--  columns the worst of them sorts above every real year.
-- ---------------------------------------------------------------------
TRUNCATE TABLE acad_grad_stats;

INSERT INTO acad_grad_stats
 (regno, progcode, plen, maxsy, courses, cu_earned, fails, zero_marks, no_score,
  gp_num, gp_den, cgpa, first_year, last_year, is_candidate, on_list, refreshed_at)
SELECT
  s.regno,
  TRIM(s.progid),
  IFNULL(NULLIF(p.couselength,0),3)                                        AS plen,
  IFNULL(MAX(r.studyyear),0)                                               AS maxsy,
  COUNT(r.courseid)                                                        AS courses,
  IFNULL(SUM(CASE WHEN r.score>=50 THEN IFNULL(r.CreditUnits,0) ELSE 0 END),0) AS cu_earned,
  IFNULL(SUM(r.score>0 AND r.score<50),0)                                  AS fails,
  IFNULL(SUM(r.score=0),0)                                                 AS zero_marks,
  IFNULL(SUM(r.score IS NULL),0)                                           AS no_score,
  IFNULL(SUM(IFNULL(r.CreditUnits,0)*IFNULL(r.gradept,0)),0)               AS gp_num,
  IFNULL(SUM(IFNULL(r.CreditUnits,0)),0)                                   AS gp_den,
  IFNULL(ROUND(SUM(IFNULL(r.CreditUnits,0)*IFNULL(r.gradept,0))
               / NULLIF(SUM(IFNULL(r.CreditUnits,0)),0), 2),0)             AS cgpa,
  MIN(CASE WHEN r.acad REGEXP '^[0-9]{4}/[0-9]{4}$'
            AND CAST(LEFT(r.acad,4) AS UNSIGNED) BETWEEN 2000 AND 2035
            AND CAST(RIGHT(r.acad,4) AS UNSIGNED)=CAST(LEFT(r.acad,4) AS UNSIGNED)+1
           THEN r.acad END)                                                AS first_year,
  MAX(CASE WHEN r.acad REGEXP '^[0-9]{4}/[0-9]{4}$'
            AND CAST(LEFT(r.acad,4) AS UNSIGNED) BETWEEN 2000 AND 2035
            AND CAST(RIGHT(r.acad,4) AS UNSIGNED)=CAST(LEFT(r.acad,4) AS UNSIGNED)+1
           THEN r.acad END)                                                AS last_year,
  IF(IFNULL(MAX(r.studyyear),0) >= IFNULL(NULLIF(p.couselength,0),3), 1, 0) AS is_candidate,
  IF(EXISTS(SELECT 1 FROM acad_graduands g WHERE g.regno=s.regno), 1, 0)   AS on_list,
  NOW()
FROM acad_student s
LEFT JOIN acad_programme p ON p.progcode = s.progid
JOIN acad_results r        ON r.regno    = s.regno
GROUP BY s.regno;

-- =====================================================================
--  To undo:  DROP TABLE acad_grad_stats;
--  Nothing else depends on it; the engine falls back to reading
--  acad_results directly if the table is empty.
-- =====================================================================
