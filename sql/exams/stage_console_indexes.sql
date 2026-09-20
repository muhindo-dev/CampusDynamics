-- ============================================================================
-- Stage consoles (Capture / Approve / Publish) - funnel index
--
-- APPLIED TO PRODUCTION 2026-09-20. Kept here so the index is reproducible on
-- any other environment; re-running it is safe (it errors as a duplicate key
-- name rather than doing damage).
--
-- WHY
-- The stage funnel counts registrations per mark_stage restricted to ACTIVE
-- students, and it runs on every load of all three consoles. The active-student
-- rule was written as a correlated EXISTS, which made MySQL walk all 691,256
-- registrations and probe the portal user table once per row:
--
--     SELECT cr.mark_stage, COUNT(*) FROM acad_course_registration cr
--     WHERE EXISTS (SELECT 1 FROM my_aspnet_users u
--                   WHERE u.name = cr.regno
--                     AND u.user_verification_status = 'ACTIVE STUDENT')
--     GROUP BY cr.mark_stage;                         -- 19.65 s
--
-- Rewritten as a join (see ActiveStudentFilter.Join) the optimiser drives from
-- the ~4,700 active students instead and reads only their registrations, which
-- brings it to 1.84 s. The rest of that time was 108k primary-key lookups purely
-- to fetch mark_stage, because no index carried both regno and mark_stage.
-- This index makes the whole aggregate index-only:
--
--     ... Extra: Using index                          -- 0.178 s
--
-- Same five numbers in every case - verified by running both forms side by side.
--
-- COST
-- ~97 MB on a 633 MB table, and one extra secondary-index maintenance per row
-- whose regno or mark_stage changes. The bulk publish path updates mark_stage,
-- so it pays that; it is one B-tree entry against the result-table writes the
-- same operation already performs.
--
-- The ALTER is online (INPLACE / LOCK=NONE) and took 3.2 s. Check
-- information_schema.INNODB_TRX is clear before running it: a long-running
-- transaction will make the ALTER queue on the metadata lock, and everything
-- touching the table then queues behind the ALTER.
-- ============================================================================

ALTER TABLE campus_dynamics_portal.acad_course_registration
    ADD INDEX idx_acr_regno_stage (regno, mark_stage),
    ALGORITHM = INPLACE, LOCK = NONE;

-- Verify: the funnel must report "Using index" on cr.
-- EXPLAIN SELECT cr.mark_stage, COUNT(*) c
--   FROM campus_dynamics_portal.acad_course_registration cr
--   JOIN campus_dynamics_portal.my_aspnet_users u_asf
--     ON u_asf.name = cr.regno
--    AND u_asf.user_verification_status = 'ACTIVE STUDENT'
--  GROUP BY cr.mark_stage;

-- Rollback:
-- ALTER TABLE campus_dynamics_portal.acad_course_registration
--     DROP INDEX idx_acr_regno_stage, ALGORITHM = INPLACE, LOCK = NONE;
