# Performance Appraisal — Stabilisation & Alignment Plan (Oct 2026)

Trigger: HR email of 2026-10-04 ("Urgent Submission of Applications for Contract Renewal — 30 Oct 2026")
plus the Council's *Evaluation Form for Achievements of Staff Responsibilities and Key Performance Areas*.
Scope of THIS plan: the performance appraisal only. Contract Renewal Request is a separate, later module.

---

## 1. Three different things the email mixes together

| | Performance Appraisal (exists) | Staff Achievement Evaluation (new, Council) | Contract Renewal Request (later module) |
|---|---|---|---|
| Purpose | Periodic performance review, development | Prove achievements over the **contract period** | Ask the University to renew an expiring contract |
| Trigger | HR opens a session (biannual / quarterly) | Contract renewal | Contract expiry within ≥3 months (HR Manual) |
| Who | All staff in the session | Staff applying for renewal | Staff whose contract ends Dec 2026 / early 2027 |
| Content | Outputs, competencies, training plan, narrative, scores | Per responsibility/KPA: Expected Standard → Achievement → Evidence; reviewer name/title; signatures | Application + latest appraisal(s) + Achievement Evaluation + other docs |
| Ends at | HR_REVIEWED + HR recommendation | Reviewer approval | Governance Council decision (Nov 2026 sitting) |

Relationship: the **renewal application is the container**. It *consumes* the completed appraisal(s) and the
Achievement Evaluation. The Achievement form is NOT a replacement for the appraisal, but it is built on the same
idea as appraisal Section B — so both should share one **Expected Standards catalogue** and one
"Standard → Achievement → Evidence" row model. Do that now in the appraisal, and the renewal module later can
prefill the Achievement form from the appraisals inside the contract period.

Live data (2026-10-05): valid contracts ending 2026-11/12 = 91 (71 academic); 2027-01..05 = 65. 116 of 313
active employees have no VALID contract row at all.

---

## 2. Current state (verified)

Session 4 (Biannual 2025/26) CLOSED: 70 HR_REVIEWED, 4 COMPLETED (never HR'd), 9 SUBMITTED, 11 IN_PROGRESS, 220 PENDING.
Session 5 (Q1 2026/27) ACTIVE, deadline 2026-12-14: 310 PENDING, 1 IN_PROGRESS, 1 COMPLETED.
**211 of 312 session-5 records have no reviewer** → nobody can ever review them. None of the 211 has
supervisorID / reviewer_id / dept_id on hrm_employee; 93 are resolvable through their VALID contract's
department head, 118 need HR to assign.

Form today: A Bio (read-only) · B Outputs (free text: agreed_output / performance_indicators / result_areas +
self & supervisor 1–5) · C Competencies (template per staff category) · D Training plan · E six narrative
questions (Support staff: AGREE/DISAGREE declaration instead) · HR block (admin side).

---

## 3. Form changes to align with the Council form

### 3.1 Section B becomes "Responsibilities / Key Performance Areas"
| Column | Source | Existing column |
|---|---|---|
| Responsibility / KPA | catalogue (or own row) | `agreed_output` |
| Expected Standard | catalogue, editable | `performance_indicators` |
| Achievement | employee | `result_areas` |
| Evidence of achievement (text) | employee | **new** `evidence_text` |
| Evidence attachment(s) | employee upload (PDF/image) | **new** table `appraisal_evidence` |
| Self rating / Supervisor rating / Supervisor comment | as today | unchanged |
| Catalogue link | — | **new** `standard_id` (NULL = own row) |

Existing data stays valid (columns reused). Session 5 has Section B rows on only 2 records → safe to change now.

### 3.2 Expected Standards catalogue (new)
- `appraisal_standard_groups` (Lecturers/Teaching, Administrative-general, Academic Registrar, Finance,
  Marketing, International, Student Affairs, Procurement, ICT, HR, Audit, Library, Research) and
  `appraisal_standards` (group, KPA, expected standard text, sort).
- Seeded verbatim from the Council annex. Admin screen to maintain (like CompetencyTemplates).
- `hrm_departments.standards_group` maps each department; academic departments → Lecturers; fallback by EmpType.
- On first open, Section B is pre-filled with the group's standards; employee may add own rows.
- Support staff: the annex has no group → keep their current competency model (decision needed).

### 3.3 Header & sign-off (matches the Council header)
- Snapshot on the record at submit/complete: position held, department, reports-to, reviewer name,
  reviewer title, date of review, date submitted (today only computed at print time).
- Electronic sign-off: employee confirms at submit; reviewer confirms at completion; **employee acknowledges
  the supervisor's rating afterwards** (Agree / Disagree + comment) — for ALL categories, replacing the
  support-only declaration that is currently asked before the supervisor has rated anything.
- Print page re-laid out to the Council format (header grid, KPA/Standard/Achievement/Evidence table,
  comments & approval, signature/date blocks).

---

## 4. Stabilisation — defects to fix (priority order)

### P0 — process integrity
1. Employee can reopen a supervisor-COMPLETED record just by clicking Next (auto-save) — wipes score
   (`Portal/SelfAppraisal.aspx.cs:86`, `:992`). COMPLETED must be read-only to the employee.
2. Reviewer resolution: single resolver `reviewer_id → supervisorID → contract dept head → HR queue`, used by
   generation, submit, queue, email and access check; supervisor page crashes on NULL reviewer
   (`SupervisorAppraisal.aspx.cs:187`); employee shows one supervisor while access uses another; changing
   supervisor in portal settings never updates open records; `IFNULL` doesn't fall through on 0.
   Block submit without a reviewer; HR "Unassigned" queue + bulk assign.
3. Security: admin `?ajax=` handlers run before the master's login check (CompetencyTemplates save/delete
   anonymous; View/Sessions/Reports leak data); AppraisalPrint & SessionReport have no auth;
   `HasHrAppraisalAccess()` returns true for any user (`AppraisalView.aspx.cs:60`); API v2 appraisal is
   written against non-existent columns AND lets any staff create sessions / read any record → disable.
4. Scoring gaming: unrated Section B rows drop out of the max; employee alone controls Section C N/A.
   Supervisor must rate every row; supervisor can override N/A.

### P1 — consistency
5. One classification vocabulary (5 exist across Portal/API/Reports/SessionReport/View; cut-offs 40 vs 50).
   Single source of truth, per-category labels.
6. Server-side validation = client-side (min rows, Achievement + Evidence required, C self-ratings, E answers).
7. `change_category`, `confirm_preflight`, `add_b_row` skip the editability check (category change after
   submit deletes B/C/E).
8. Activate button doesn't generate records (only Edit→ACTIVE does).
9. Template seeder wipes all custom templates if C1.1 text was edited (`AppraisalSessions.aspx.cs:238`).
10. HR declaration overwrites the employee's `support_declaration`.
11. Bulk HR input doesn't validate recommendation; Reopen keeps stale HR fields/score.
12. Audit: no admin action (return/cancel/reopen/HR/change-supervisor/delete) writes `appraisal_record_audit`.

### P2 — reporting/print
13. Reports score-band uses COMPLETED only (>100%, "no completed" once all HR'd); Dashboard supervisor column
    reads non-existent `supervisor_id`; counts differ (contract filter vs employment_status).
14. Print: supervisor comments never printed, Section C ordered by code (splits groups), C self-ratings missing.
15. Deadline shown but never enforced; HR_REVIEWED label missing on portal pages; MyAppraisals dept "Unassigned".

### Data clean-up
- Session 5: assign reviewers to the 211 (93 auto via contract dept head, 118 via HR).
- Session 4 leftovers (4 COMPLETED without HR, 9 SUBMITTED, 11 IN_PROGRESS) — HR decides.

---

## 5. Sequence
1. P0 fixes + reviewer clean-up for session 5 (unblocks staff now).
2. Standards catalogue + Section B redesign + header/sign-off + print layout.
3. P1/P2 fixes, user guide + video notes update.
4. (Later) Contract Renewal Request module consuming appraisals + Achievement Evaluation.
