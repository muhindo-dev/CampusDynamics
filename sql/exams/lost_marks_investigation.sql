-- ============================================================================
--  "My marks are missing" — what is actually happening, and how to find it
--
--  Investigated 2026-09-18 after student complaints about lost, untraceable
--  marks. Everything below is READ-ONLY. The remediation at the bottom is
--  written out but commented, because each part needs a decision first.
--
--  Student-visible marks live in campus_dynamics.acad_results. The portal's
--  StudentResults screen reads that table and nothing else, so a mark that is
--  not there does not exist as far as the student is concerned — however
--  complete it looks in campus_dynamics_portal.acad_course_registration.
-- ============================================================================

USE campus_dynamics;


-- ┌──────────────────────────────────────────────────────────────────────────┐
-- │ CAUSE 1 — acad_results cannot hold the same course twice                 │
-- └──────────────────────────────────────────────────────────────────────────┘
--
--  SHOW INDEX FROM acad_results  ->  Index_UNQ is UNIQUE (regno, courseid).
--  No academic year. No semester. One result per course code per student for
--  their entire time at the university.
--
--  That is DELIBERATE, and widening it would be wrong. It is what lets a retake
--  show the course once carrying its new grade: RetakeService snapshots the
--  original marks into acad_retake_registrations, blanks the row, and the later
--  publish refills the SAME result row. Add acad + semester to the key and every
--  repeated course starts printing twice on the transcript and counting twice in
--  the CGPA, which is summed over acad_results.
--
--  So when a student takes the same course code again — a repeat, a carry-over,
--  a retake, or simply a second registration — the publish UPSERT lands on the
--  EARLIER term's row:
--     * the earlier mark is overwritten by the later one, and
--     * the term being published still shows the student nothing.
--  The publish reports success. Nothing looks wrong to the person who did it.
--
--  This is 113 of the 142 invisible marks since 2023/2024 — about 80%.
--
--  The hole is that a SECOND ORDINARY REGISTRATION of the same course gets the
--  retake treatment with none of the safeguards: no snapshot, no notice. Only 9
--  of 246 repeat registrations are flagged RETAKE — the other 237 are ordinary
--  repeats, carry-overs and mis-registrations that the results table has no way
--  to represent.
--
--  Guarded in code on 2026-09-18: ProcessProvisionalAction refuses to publish one
--  term's mark on top of another's and names the route out — Retake Registration
--  if the student really is sitting it again, the Course Correction Centre if the
--  registration is in the wrong term or is a duplicate. 125 registrations are
--  blocked by it; 643,241 publish exactly as before.

-- Every student whose marks are colliding on this key:
SELECT cr.regno, cr.courseID,
       GROUP_CONCAT(DISTINCT CONCAT(cr.acad_year, ' S', cr.semester, ' = ',
                    IFNULL(cr.provisional_total_marks, '-')) ORDER BY cr.acad_year SEPARATOR '  |  ') AS terms_registered,
       (SELECT CONCAT(r.acad, ' S', r.semester, ' = ', r.score)
          FROM acad_results r
         WHERE r.regno = CONVERT(cr.regno USING utf8)
           AND r.courseid = CONVERT(cr.courseID USING utf8) LIMIT 1) AS the_one_result_row
FROM campus_dynamics_portal.acad_course_registration cr
WHERE cr.provisional_total_marks IS NOT NULL
GROUP BY cr.regno, cr.courseID
HAVING COUNT(DISTINCT CONCAT(cr.acad_year, '|', cr.semester)) > 1
ORDER BY cr.regno;
-- 2026-09-18: 120 student/course pairs, 86 students, 72 of them holding
-- different totals in the two terms — so one real mark has been destroyed.


-- ┌──────────────────────────────────────────────────────────────────────────┐
-- │ THE HEADLINE CHECK — marks the student cannot see                        │
-- └──────────────────────────────────────────────────────────────────────────┘
--  This is now a screen: COOPERP/NewScreens/MissingMarks.aspx, under
--  Academics -> Exam in the sidebar. It classifies every gap and says what closes
--  it, so nobody has to run SQL to find out which students will complain.
--  The query below is the same reconciliation, kept here for ad-hoc use.
--
--  Supporting index added 2026-09-18:
--    ALTER TABLE campus_dynamics_portal.acad_course_registration
--      ADD INDEX idx_acr_pubstatus_term (provisional_marks_status, acad_year, semester);

SELECT cr.regno, cr.courseID, cr.acad_year, cr.semester, cr.prog_id,
       cr.provisional_course_work_marks AS cw,
       cr.provisional_exam_marks        AS exam,
       cr.provisional_total_marks       AS total,
       cr.provisional_published_by      AS published_by,
       cr.provisional_published_date    AS published_at,
       CASE WHEN EXISTS (SELECT 1 FROM acad_results r2
                          WHERE r2.regno = CONVERT(cr.regno USING utf8)
                            AND r2.courseid = CONVERT(cr.courseID USING utf8))
            THEN 'collided with another term (CAUSE 1)'
            ELSE 'no result row at all' END AS why
FROM campus_dynamics_portal.acad_course_registration cr
WHERE cr.provisional_marks_status = 'published'
  AND NOT EXISTS (SELECT 1 FROM acad_results r
                   WHERE r.regno    = CONVERT(cr.regno USING utf8)
                     AND r.courseid = CONVERT(cr.courseID USING utf8)
                     AND r.acad     = CONVERT(cr.acad_year USING utf8)
                     AND r.semester = cr.semester)
ORDER BY cr.provisional_published_date DESC;
-- 2026-09-18: 359 rows overall, 142 of them from 2023/2024 onwards.


-- ┌──────────────────────────────────────────────────────────────────────────┐
-- │ CAUSE 2 — a mark was removed on purpose and nobody closed the loop        │
-- └──────────────────────────────────────────────────────────────────────────┘
--  CORRECTION to the first draft of this file, which claimed 236 marks had been
--  deleted and were recoverable. They had not been lost. Of those 236, 221 had
--  their course registration deleted as well and 15 did not — so the deletions
--  were consistent with the record, which is what a course deletion is supposed
--  to do. Restoring them would have been wrong.
--
--  What IS open is the handful where the registration is still there and the
--  mark never came back:
--    * a MARKS_RESET ("wrong marks") that nobody re-entered — SWA3208B, eight
--      students holding 62 to 78, cleared 2026-08-17 and still blank a month on;
--    * an unpublish where the mark was then revised and never re-published —
--      three students, one of whom went from 33 to 66.
--  Nothing here can be repaired by a script: the marks have to be entered again.
--  What matters is that somebody sees them, which is what MissingMarks.aspx is
--  for (class D).

SELECT a.regno, a.course_id, a.acad_year, a.semester,
       a.old_total, a.old_grade, a.performed_by, a.source_page, a.created_at
FROM acad_marks_audit a
WHERE a.action_type = 'DELETE'
  AND a.old_total > 0
  AND NOT EXISTS (SELECT 1 FROM acad_results r
                   WHERE r.regno = a.regno AND r.courseid = a.course_id)
ORDER BY a.created_at DESC;
-- 2026-09-18: 236 rows, of which 221 also lost their registration (correct) and
-- 15 did not (open). Join to acad_course_registration to tell them apart, which
-- is what the Missing Marks screen does.


-- ┌──────────────────────────────────────────────────────────────────────────┐
-- │ CAUSE 3 — marks entered, approved, and never published                   │
-- └──────────────────────────────────────────────────────────────────────────┘
--  Nothing is lost here; the mark simply never reached the student. The Dean
--  approved it and the publish step was never run.

SELECT cr.provisional_marks_status, cr.mark_stage, COUNT(*) AS n,
       COUNT(DISTINCT cr.regno) AS students
FROM campus_dynamics_portal.acad_course_registration cr
WHERE cr.provisional_total_marks IS NOT NULL
  AND IFNULL(cr.provisional_marks_status,'') <> 'published'
GROUP BY cr.provisional_marks_status, cr.mark_stage
ORDER BY n DESC;
-- 2026-09-18: 912 in total, of which 296 are APPROVED and stuck.


-- ┌──────────────────────────────────────────────────────────────────────────┐
-- │ CAUSE 4 — the same registration recorded twice                           │
-- └──────────────────────────────────────────────────────────────────────────┘
--  Two rows for one student, one course, one term. Marks entered on one copy
--  are invisible on the other, and whichever copy a screen happens to show is
--  a coin toss.

SELECT regno, courseID, acad_year, semester, COUNT(*) AS copies,
       GROUP_CONCAT(ID ORDER BY ID) AS registration_ids,
       GROUP_CONCAT(IFNULL(provisional_total_marks,'-') ORDER BY ID) AS totals
FROM campus_dynamics_portal.acad_course_registration
GROUP BY regno, courseID, acad_year, semester
HAVING COUNT(*) > 1
ORDER BY COUNT(DISTINCT IFNULL(provisional_total_marks,-1)) DESC, regno;
-- 2026-09-18: 700 surplus rows over 195 students; 10 groups disagree on the mark.


-- ┌──────────────────────────────────────────────────────────────────────────┐
-- │ WHY IT "CANNOT BE TRACKED"                                               │
-- └──────────────────────────────────────────────────────────────────────────┘
--  The audit itself is sound — every change to acad_results has been recorded
--  since 2026-04. What is missing is the name of the person: a write that does
--  not set mark_audit_context first is recorded as "system".

SELECT action_type, COUNT(*) AS unattributed
FROM acad_marks_audit WHERE performed_by = 'system' GROUP BY action_type;
-- 2026-09-18: 847 deletes, 1,340 updates, 389 inserts — about 10% of the trail.
--
-- AcademicResults.aspx could delete a published mark with no attribution at
-- all; fixed 2026-09-18. The remaining unattributed writes need the same
-- one-line MarkAuditContext.Set before the statement. Find them with:
SELECT source_page, action_type, COUNT(*) n, MIN(created_at) first_at, MAX(created_at) last_at
FROM acad_marks_audit WHERE performed_by = 'system'
GROUP BY source_page, action_type ORDER BY n DESC;


-- ============================================================================
--  WHAT ACTUALLY PREVENTS THIS
-- ============================================================================
--
--  1. The publish guard, in place since 2026-09-18. A mark can no longer be
--     written silently on top of another term's.
--
--  2. MissingMarks.aspx. Every class above became visible only when a student
--     complained; now it is a standing list with a named remedy per row.
--
--  3. Attribution. A write that does not set mark_audit_context first is
--     recorded as "system", and about a tenth of the trail is. Each remaining
--     offender needs one line before the statement:
--         MarkAuditContext.Set(conn, tx, "Screen:what-it-did", "why");
--     AcademicResults.aspx was the worst of them and is done.
--
--  DO NOT widen Index_UNQ to (regno, courseid, acad, semester). It looks like the
--  fix and it is not: the single row per course is what the retake design depends
--  on, and CGPA is summed over this table, so a second row for a repeated course
--  would both print twice on the transcript and count twice in the award.
--  If the university ever rules that a repeat should stand alongside the original
--  rather than replace it, that is a change to the transcript and GPA rules first,
--  and only then to this key.
-- ============================================================================
