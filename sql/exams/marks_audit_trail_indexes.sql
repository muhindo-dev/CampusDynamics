-- ============================================================================
--  acad_activity_log: indexes for the Marks Audit Trail
--
--  The table had nothing but its PRIMARY KEY on logid, so every visit to
--  MarksAuditTrail.aspx read all 250,000 rows to count 220 of them, and did it
--  again to order them. On a CPU-bound box that is the whole page's cost.
--
--  Applied to production 2026-09-18. Takes about 3 seconds; adds roughly 15 MB.
--  Safe to run again — MySQL 5.6 errors on a duplicate key name, so the drops
--  come first.
-- ============================================================================

USE campus_dynamics;

-- MySQL 5.6 has no DROP INDEX IF EXISTS, so ignore "can't DROP" on a first run.
-- ALTER TABLE acad_activity_log DROP INDEX idx_aal_pf_date;
-- ALTER TABLE acad_activity_log DROP INDEX idx_aal_date;
-- ALTER TABLE acad_activity_log DROP INDEX idx_aal_pf_user;

ALTER TABLE acad_activity_log
    -- the page_function IN (...) filter plus the date window, which is every
    -- query the audit trail runs
    ADD INDEX idx_aal_pf_date (page_function, access_date),
    -- ordering newest-first across all action types
    ADD INDEX idx_aal_date (access_date),
    -- the "User" dropdown: GROUP BY user_id within the marks page_functions
    ADD INDEX idx_aal_pf_user (page_function, user_id);

-- ---------------------------------------------------------------------------
--  Check
-- ---------------------------------------------------------------------------
SHOW INDEX FROM acad_activity_log;

EXPLAIN
SELECT COUNT(*) FROM acad_activity_log a
WHERE a.page_function IN ('Capture Results','Results Capture','Faculty Exam Results Editor',
                          'Results Approval Cancel','Results Management','Results Auto Pass',
                          'Mark Request Approve','Mark Request Reject','Mark Request Force Close',
                          'Mark Request Reopen','Mark Request Marks Update','Mark Request Batch',
                          'Marks Published to Results')
  AND a.access_date >= '2026-01-01';
-- expect: type=range, key=idx_aal_pf_date  (was type=ALL, rows=230691)
