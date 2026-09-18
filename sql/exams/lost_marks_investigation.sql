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
--  So when a student takes the same course code again — a repeat, a carry-over,
--  a retake, or simply a second registration — the publish UPSERT lands on the
--  EARLIER term's row:
--     * the earlier mark is overwritten by the later one, and
--     * the term being published still shows the student nothing.
--  The publish reports success. Nothing looks wrong to the person who did it.
--
--  This is 113 of the 142 invisible marks since 2023/2024 — about 80%.
--
--  Guarded in code on 2026-09-18: ProcessProvisionalAction now refuses to
--  publish one term's mark on top of another's and says exactly why. That stops
--  new losses; it does not repair the old ones, and it does not let a repeated
--  course be published at all until the key below is widened.

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
--  Run this regularly. Anything it returns is a student who will complain.

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
-- │ CAUSE 2 — marks deleted and never put back                               │
-- └──────────────────────────────────────────────────────────────────────────┘
--  Every one of these is recoverable: the audit kept the score, the grade and
--  the term. The largest source is the course-deletion approval, which removes
--  the published result along with the registration; the rest are unpublishes
--  that were never followed by a publish.

SELECT a.regno, a.course_id, a.acad_year, a.semester,
       a.old_total, a.old_grade, a.performed_by, a.source_page, a.created_at
FROM acad_marks_audit a
WHERE a.action_type = 'DELETE'
  AND a.old_total > 0
  AND NOT EXISTS (SELECT 1 FROM acad_results r
                   WHERE r.regno = a.regno AND r.courseid = a.course_id)
ORDER BY a.created_at DESC;
-- 2026-09-18: 236 marks, 109 students, all with enough detail to restore.


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
--  THE PERMANENT FIX — needs a decision before it is applied
-- ============================================================================
--
--  The key has to carry the term:
--
--      ALTER TABLE acad_results DROP INDEX Index_UNQ,
--                               ADD UNIQUE INDEX Index_UNQ (regno, courseid, acad, semester);
--
--  That is the only thing that lets a student hold a mark for the same course
--  in two terms, and without it the new publish guard simply refuses those
--  publishes rather than corrupting them.
--
--  It cannot just be run, because GPA and CGPA are computed as
--      SUM(gradept * CreditUnits) / SUM(CreditUnits)
--  over acad_results (MarksControllerShared, ~line 2275). A second row for a
--  repeated course would be counted a second time, changing the CGPA of every
--  affected student. So the academic policy has to be settled first:
--
--      Does a repeat REPLACE the earlier grade, or does it stand alongside it?
--
--  If it replaces  -> widen the key, then make the GPA queries count only the
--                     latest attempt per course (acad_results.is_retake already
--                     exists for this and is set on 193 rows).
--  If it stands    -> widen the key and leave the GPA as it is; CGPAs will move
--                     for the 86 students concerned, and that movement is the
--                     correct figure rather than the current one.
--
--  Either way the transcript templates need checking: today they can only ever
--  print one line per course code.
-- ============================================================================
