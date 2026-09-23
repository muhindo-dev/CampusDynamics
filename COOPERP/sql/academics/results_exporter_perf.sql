-- =====================================================================
--  Results Exporter — the two schema changes its speed work depended on.
--  Applied to production 2026-09-23. Kept here so a rebuilt database is
--  not quietly slower, and so the reasoning survives the commit message.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. acad_results had no index on `acad`.
--
--    Every query on the exporter filters by academic year, and every one
--    of them was a full scan of 639,185 rows. The detailed-marks grid was
--    the worst case: it sorted the whole year to return the first 100
--    rows. With this index the range is ~24k rows and the ORDER BY is
--    satisfied by the index itself, so the LIMIT stops after 100.
--
--    Measured on 2013/2014 (59,071 rows), server-side:
--        grid query        0.627 s  ->  0.002 s
--        per-student pass  0.745 s  ->  0.301 s
--        breakdown         1.008 s  ->  0.792 s
--
--    Column order matters: (acad) makes the range, and (regno, semester,
--    courseid) is exactly the grid's ORDER BY, which is what turns the
--    sort into an ordered read. A wider covering version carrying score,
--    gradept, progid and CreditUnits was tried and measured NO faster
--    while costing an extra 159 MB, so it was dropped again.
-- ---------------------------------------------------------------------
ALTER TABLE acad_results
  ADD INDEX idx_ar_export (acad, regno, semester, courseid);

-- To undo:
-- ALTER TABLE acad_results DROP INDEX idx_ar_export;


-- ---------------------------------------------------------------------
-- 2. acad_CGPAFinder took CHAR(25); student numbers reach 26 characters.
--
--    Under STRICT mode the call threw 1406 "Data too long for column
--    'reg'", so the exporter's Summary mode failed outright for any scope
--    containing one of these four students:
--
--        MRU11/U/BED (P) /253/K/INS
--        MRU11/U/BED (P) 2254/K/INS
--        MRU11/U/BED/P/2272/B/INS/K
--        MRU14/U/BEICT/0927/K/B/DAY
--
--    The function is also called by ten stored procedures (transcripts,
--    marksheets, graduand info) and six code files, so the same four
--    students would have failed there too.
--
--    Widening a parameter is strictly permissive — every call that worked
--    before still works. The body is byte-for-byte the original; only the
--    parameter type changed. Verified by comparing the returned CGPA for
--    300 students before and after: identical.
--
--    NOTE the DELIMITER. Running the CREATE without it drops the function
--    and then fails on the first internal semicolon.
-- ---------------------------------------------------------------------
SET SESSION sql_mode='';
DROP FUNCTION IF EXISTS acad_CGPAFinder;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` FUNCTION `acad_CGPAFinder`(reg CHAR(85)) RETURNS double(5,2)
BEGIN
DECLARE gpa DOUBLE;
SELECT SUM(CreditUnits*gradept)/SUM(CreditUnits) INTO gpa FROM acad_results
WHERE regno=reg;
RETURN IF(gpa IS NULL,0,ROUND(gpa,2));

END$$
DELIMITER ;
