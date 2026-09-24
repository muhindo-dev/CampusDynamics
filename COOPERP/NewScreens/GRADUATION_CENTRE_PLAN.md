# Graduation Centre — design and build plan

**Status:** plan agreed, not yet built
**Author:** Claude (Opus 5) with the MIS Manager
**Date:** 2026-09-24
**Benchmark:** `ResultsExporter.aspx` — its Export Summary Report modal for the filter
cascade and its scope model; `MarksScopeResolver` for who may see what.

---

## 0. Read this first — the three things that decide the design

### 0.1 A Graduation Centre already exists

`COOPERP/NewScreens/GraduationCentre.aspx` (+ `.cs`) was written in **January 2026**. It is
DevExpress/postback-era: a faculty and programme filter, a flat candidate list, and a button
that writes into `acad_graduands`. It has no dashboard, no holds, no reason capture, no credit
accounting, no comparison against the programme structure, and no record of who approved what.

`GraduationAnalysis.aspx` (April 2026) and `AlumniDataBank.aspx` (July 2026) also read
`acad_graduands`.

**Decision:** rebuild `GraduationCentre.aspx` in place, in the NewScreens idiom
(PageMethods + design system + GET-driven tabs). Do not create a second page with a similar
name — two graduation screens that disagree is the worst outcome available. `GraduationAnalysis`
and `AlumniDataBank` are left alone; they read the same table and keep working.

### 0.2 `acad_graduands` is the system of record and must not be bypassed

It is not a reporting table. **19 stored procedures and 10 code files read it**, including every
transcript procedure, `CertificateDataHelper`, `AlumniDataBank`, `NewStudentInfo`,
`CourseCorrectionService` and `StudentRearrangeService`. `trans_status` / `cert_status` /
`trans_printer` / `cert_printer` on that table drive document printing.

Current contents: **1,621 rows, 1,620 distinct students, 19 academic years** — so there is
already one duplicate to resolve (§6.4).

**Decision:** the Graduation Centre writes *through* `acad_graduands`, never around it. A student
"on the graduation list" means exactly "has a row in `acad_graduands`", now and after this work.

### 0.3 The classic degree-class call has always been broken

`acad_GetGraduandInfo` computes its degree class as:

```sql
acad_GetDegClass(acad_CGPAFinder(regno), gradSystemID, SUBSTRING(progid,3,1))
```

`acad_GetDegClass` looks that third argument up against `acad_gs_award.acad_level`, whose values
are `Bachelors`, `Diploma`, `Certificate`, `Masters`, `Postgraduate`. `SUBSTRING(progid,3,1)` is
**one character**. It can never match. Verified:

```sql
SELECT acad_GetDegClass(4.5, 1, SUBSTRING('BSCS',3,1));  -- 'N|A'
SELECT acad_GetDegClass(4.5, 1, 'Bachelors');            -- 'First Class (Honours)'
```

**Decision:** the new module derives the level from `acad_programme.levelCode`
(1 Certificate, 2 Diploma, 3 Bachelors, 4 Masters, 5 Postgraduate) exactly as
`ResultsExporter.AwardClass()` already does, and never calls `acad_GetDegClass` with a substring.
The classic procedure is left in place but is not used by this module.

---

## 1. What the module is for

One place where an Academic Registrar, a Dean or a Head of Department can answer, for a given
academic year and scope:

1. **Who looks ready to graduate?** — derived from results, credits and programme structure.
2. **Is that student actually ready?** — one click to the evidence.
3. **Yes / not yet** — clear them onto the list, or hold them with a written reason.
4. **What is the final list?** — and who put each name on it.

The centre does **not** award anything. It produces the list Senate approves. Wording throughout
reflects that: *candidate*, *cleared*, *held*, *graduation list* — never "passed" or "graduated"
as an action performed by this screen.

---

## 2. Vocabulary (fixed — used in UI, code and database alike)

| Term | Meaning |
|---|---|
| **Candidate** | A student the engine believes is at or near the end of their programme in the selected year. Not a decision. |
| **Cleared** | A human has reviewed the candidate and put them on the graduation list. Creates the `acad_graduands` row. |
| **Held** | A human has stopped the candidate, with a reason, until something is investigated. No `acad_graduands` row. |
| **Graduation list** | The set of `acad_graduands` rows for the academic year. |
| **Verdict** | A row in `acad_grad_review` — cleared or held, by whom, when, why. |
| **Finding** | One automated check's result on one student: `PASS`, `WARN` or `BLOCK`, with evidence. |
| **Readiness** | The worst finding across all checks. `BLOCK` anywhere ⇒ not ready. |

Deliberately avoided: "approve" (Senate approves, not this screen), "fail" (a student does not
fail graduation — a check blocks), "reject".

---

## 3. The eligibility engine

### 3.1 Shape

`App_Code/Graduation/GraduationEngine.cs` — one class, no UI knowledge. Given a scope, an
academic year and filters, it returns a `Candidate` per student carrying every `Finding`.

Each check is independent, named, and returns `PASS` / `WARN` / `BLOCK` **plus the numbers it
used**. The UI never re-derives a number; it renders what the engine reported. This is the single
most important rule in the module: *one source of arithmetic*.

### 3.2 The checks

| # | Check | BLOCK when | WARN when | Evidence carried |
|---|---|---|---|---|
| C1 | **Already graduated** | a row exists in `acad_graduands` for this student | — | the year and list it is on |
| C2 | **Outstanding papers** | any published result with `score < 50` | any result with `score IS NULL` | count + the course codes |
| C3 | **Credits earned** | earned < required × 0.90 | earned < required | earned, required, shortfall, source of "required" |
| C4 | **Programme coverage** | — | a required course has no result at all | how many required courses unmatched, and which |
| C5 | **Unpublished marks** | — | registrations at a stage below PUBLISHED in the portal | count by stage |
| C6 | **Programme duration** | — | years spanned < `couselength` | first and last academic year seen |
| C7 | **CGPA floor** | CGPA < 2.0 (below Third Class) | CGPA is 0 / not computable | CGPA and the resulting class |
| C8 | **Existing hold** | an open `HELD` verdict exists | — | reason, who, when |

C1 and C8 are membership facts, not academic judgements, and are evaluated first so the list
never offers an action that contradicts itself.

### 3.3 Where "required credits" comes from — and why it is layered

This is the weakest data in the system and the plan says so up front. Required CU is resolved in
this order, and the **source is always shown to the user**:

1. **Programme structure** — `SUM(acad_course.CreditUnit)` over `acad_programmecourses` for the
   student's programme, filtered to `status='Active'`, scoped to their specialisation where that
   is real (see §3.4). Best answer. Available for **63 of 130 programmes**.
2. **`acad_programme.mincredit`** — the classic field. **62 of 131 programmes have it empty**,
   and the populated values range from 3 to 506, which is not credible for a degree. Used only
   when it is non-zero *and* no structure exists, and always labelled "declared minimum".
3. **Nothing** — the check reports `NOT ASSESSABLE` rather than inventing a number. It never
   silently becomes a pass.

> **Rule:** the module must never print a credit requirement without saying where it came from.
> A Dean signing a graduation list is entitled to know whether the bar was the curriculum, a
> legacy field, or a guess. There is no guess.

### 3.4 The specialisation trap

`acad_programmecourses` varies by `CurriculumID` and `specialisation_id`. For BAED the whole
table sums to **6,484 CU** across 2,175 rows — obviously not a degree — while any one
specialisation is **185–211 CU over three years**, which is right.

But `acad_student.specialisation` is a placeholder for almost everyone:

| value | students |
|---|---|
| `13` | **30,009** |
| `0` | 1,035 |
| real values (136 others) | ~2,200 |

**Decision:** when the student's specialisation resolves to a real `acad_specialisation` row for
their programme, use that variant's total. Otherwise use the **median** total across that
programme's active specialisations and label it *"programme typical"*. Never the sum, never the
max. The UI shows which of the two was used, and a specialisation that is a placeholder is
reported as such on the student's evidence panel.

### 3.5 Credits earned

`SUM(acad_results.CreditUnits)` over **passed** results only (`score >= 50`), across all years —
graduation is cumulative, not per-year. Retakes are counted **once**: a course code contributes
its credit a single time regardless of how many attempts exist. This matters because
`acad_results` has `UNIQUE(regno, courseid)` with no term, so a repeat overwrites rather than
duplicating — but the staged tables do not, and the engine must not double-count when a source
other than published is selected.

### 3.6 Class of award

`CGPA` from `acad_CGPAFinder(regno)` — the authoritative cumulative figure, same function the
transcripts use. Class from `levelCode` + CGPA, using the bands in `acad_gs_award` (gsid = 1;
every student in the database is on grading system 1). The bands are read from the table, not
hardcoded, so a Senate change to the award scale takes effect without a code change.

---

## 4. Scope — who sees whom

Reuse `MarksScopeResolver.Resolve()` unchanged:

| Role | Sees |
|---|---|
| Administrator | every programme |
| Dean | programmes in their faculty |
| Head of Department | programmes in their department |
| anyone else | nothing, with the same "not linked to a faculty or department" message ResultsExporter shows |

`scope.ProgFilter("s", "progid")` is applied to every query without exception, including the
counts on the dashboard. A Dean's dashboard must never include a number they could not drill into
— a KPI that does not reconcile with the list beneath it destroys trust in the whole screen.

**Clearing and holding are scope-gated the same way.** A HOD cannot clear a student outside their
department, and the server re-checks on every write; the UI hiding a button is not access control.

---

## 5. The screen

`GraduationCentre.aspx`, four tabs, GET-driven (`?tab=…&year=…&faculty=…&prog=…`) so a view is
linkable and the back button works — same convention as the other NewScreens consoles.

### 5.1 Tab 1 — **Overview**

Not a wall of KPIs. Four numbers that lead somewhere, each a link into a filtered list:

- **Candidates in scope** — how many the engine surfaced for the year
- **Ready** — no BLOCK finding; the queue to work through
- **Held** — with the count of distinct reasons
- **On the graduation list** — `acad_graduands` for the year

Then two things that are genuinely decision-support rather than decoration:

- **Where the blockers are** — a small table of check → how many candidates it blocks, so a
  Registrar can see "41 students blocked on outstanding papers" and act on the *cause*.
- **Progress by programme** — per programme: candidates, cleared, held, still to review. This is
  the view a Dean actually wants in the week before Senate.

### 5.2 Tab 2 — **Candidates**

The working queue. Filters cascade exactly like the Export Summary Report modal: academic year →
faculty → department → programme → entry year, each narrowing the next, with counts shown so an
empty combination is visible before it is selected.

Columns: student number, name, programme, entry year, credits (earned / required, with a bar),
CGPA, class, readiness chip, and the action.

A row is not a form. Clicking it opens the **evidence panel** (§5.5).

Bulk clearing is offered **only** for candidates whose worst finding is `PASS`, and it always
lands on a confirmation that names the count and the year. Bulk-clearing a `WARN` is not
offered — a warning exists to be read.

### 5.3 Tab 3 — **Graduation list**

The output. Per academic year: every `acad_graduands` row in scope, with class, CGPA, programme,
and — the part that does not exist today — **who cleared them and when**, from `acad_grad_review`.

Actions: export (XLSX/CSV, same writer as ResultsExporter), print a Senate-ready list, and
remove a student from the list (which reverses the verdict and writes an audit row; it does not
delete history).

### 5.4 Tab 4 — **Held**

Every student stopped, grouped by reason, oldest first — because a hold nobody revisits is a
student who quietly never graduates. Each row shows the reason, who held them, how long ago, and
whether the underlying finding still applies. A hold whose blocking finding has since cleared is
highlighted: *"the reason for this hold no longer applies"*.

Actions: release (back to the candidate queue), edit the reason, or clear straight to the list if
the reviewer is satisfied.

### 5.5 The evidence panel

Opens over any candidate, anywhere in the module. It answers "why does the system think this?"
without the reviewer leaving the queue.

- **Identity** — number, name, programme, specialisation (marked if placeholder), entry year,
  intake
- **The verdict strip** — each of the eight checks as a row: name, PASS/WARN/BLOCK, and the
  numbers behind it
- **Credits** — earned vs required, with the *source* of the requirement stated in words
- **Against the programme structure** — required courses listed year by year, each marked
  *passed* / *failed* / *no result*, so a missing course is visible as a gap rather than a total
- **Results** — the student's marks, newest first, with retakes marked
- **History** — every verdict ever recorded for this student, with actor and timestamp

Footer: **Clear for graduation** and **Hold**. Hold requires a reason of at least 10 characters —
a blank reason is the thing that makes a hold useless three months later.

---

## 6. Data

### 6.1 New table — `acad_grad_review`

`acad_graduands` records *who graduates*. It has nowhere to record a hold, a reason, or a
reviewer. That is the gap this table fills.

```sql
CREATE TABLE acad_grad_review (
  id            INT UNSIGNED NOT NULL AUTO_INCREMENT,
  regno         VARCHAR(85)  NOT NULL,
  acadyear      CHAR(25)     NOT NULL,   -- the graduation year being reviewed
  progcode      CHAR(25)     NOT NULL,   -- as at decision time
  verdict       ENUM('CLEARED','HELD','RELEASED') NOT NULL,
  reason        VARCHAR(1000) NULL,      -- required for HELD
  -- the engine's answer at decision time, so a later data change cannot rewrite history
  snapshot_json TEXT         NULL,
  cgpa          DOUBLE       NULL,
  degclass      VARCHAR(150) NULL,
  cu_earned     DOUBLE       NULL,
  cu_required   DOUBLE       NULL,
  cu_source     VARCHAR(30)  NULL,       -- STRUCTURE | STRUCTURE_TYPICAL | DECLARED | NONE
  actor         VARCHAR(100) NOT NULL,
  actor_role    VARCHAR(40)  NULL,
  created_at    DATETIME     NOT NULL,
  superseded_at DATETIME     NULL,       -- NULL = the verdict in force
  PRIMARY KEY (id),
  KEY idx_gr_current (regno, acadyear, superseded_at),
  KEY idx_gr_year    (acadyear, verdict, superseded_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

**Append-only.** A new verdict supersedes the previous one by stamping `superseded_at`; nothing is
updated in place and nothing is deleted. "Who approved this student and on what evidence" must
still be answerable a year later, after the marks have been edited.

`snapshot_json` stores the findings as they stood when the human decided. Without it, a student
cleared in September looks unjustifiable in November if a mark changed in October.

### 6.2 Index needed

```sql
ALTER TABLE acad_graduands ADD INDEX idx_grad_year (acadyear, progcode);
```

Every list view filters by year; the table has only `PRIMARY(ID)` and `idx_grad_regno`.

### 6.3 What is NOT changed

- `acad_graduands` structure — untouched. New rows are written with the same columns
  `acad_AddGraduand` writes, so transcripts and certificates are unaffected.
- `acad_AddGraduand` / `acad_RemoveGraduand` — left in place for the old screen and any other
  caller. The new module writes the same shape **in a transaction together with the review row**,
  because a graduand row without a verdict, or a verdict without a graduand row, is exactly the
  inconsistency this module exists to prevent.
- `acad_CompletionCheck`, `acad_GetGraduandInfo`, `acad_GetDegClass` — not used, not modified.

### 6.4 Data problems to surface, not silently fix

| Finding | Size | Plan |
|---|---|---|
| Duplicate in `acad_graduands` | 1,621 rows vs 1,620 students | list it on the Overview as a data-integrity notice; do **not** auto-delete |
| Programmes with no structure | 67 of 130 | credit check reports `NOT ASSESSABLE` |
| Programmes with no `mincredit` | 62 of 131 | same |
| `specialisation = '13'` | 30,009 of 33,253 students | fall back to programme-typical, label it |
| `completion_date` populated | 6 students only | not used as a signal; derived from results instead |
| `new_status = 'ALUMNI'` | 28,361 students | advisory only — it is not evidence of graduating |

None of these are fixed by this module. It reports them where they affect a decision, which is
the honest behaviour: a screen that quietly guesses is worse than one that says "I cannot tell".

---

## 7. Deliberately out of scope

- **Finance clearance.** Not requested, and a graduation list that silently enforces a fees rule
  would be a policy decision made in code. The engine has a slot for an advisory check; it is not
  wired to anything. Flagged for the MIS Manager to decide.
- **Senate approval workflow.** This produces the list Senate approves; it does not model the
  approval itself.
- **Certificate/transcript printing.** Already exists and already reads `acad_graduands`.
- **Convocation / ceremony management.** `acad_graduands.convocation` exists and is left alone.

---

## 8. Build order

Each step is independently verifiable, and nothing user-visible ships before the arithmetic under
it has been checked against the database by hand.

1. **Schema** — `acad_grad_review`, the `acad_graduands` index, in
   `COOPERP/sql/academics/graduation_centre.sql` with an undo.
2. **`GraduationEngine.cs`** — checks C1–C8, required-CU resolution, class of award. No UI.
3. **Verify the engine against reality** — run it over a year already graduated
   (2024/2025, 522 students) and reconcile: who it would have cleared vs who is actually on the
   list, and explain every difference. *This is the gate — the UI is not built until this
   reconciles.*
4. **`GraduationService.cs`** — the transactional clear / hold / release writes.
5. **Page shell** — tabs, scope, filter cascade (ported from the Export Summary Report).
6. **Candidates tab + evidence panel.**
7. **Overview tab** — built from the same engine output, so the KPIs cannot disagree with the list.
8. **Graduation list + Held tabs**, export and print.
9. **Sidebar entry** under Academics, scope-gated.
10. **End-to-end verification** on a real signed-in account per role.

---

## 9. How this will be verified

- **Engine vs history** — step 3 above, reconciled student by student for one full year.
- **Arithmetic** — credits earned, credits required and CGPA recomputed in SQL for a sample of
  30 students across five programmes and compared to what the engine reports. Any difference is a
  bug in the engine, not a tolerance.
- **Scope** — signed in as an administrator, a Dean and a HOD; confirm each sees exactly their
  programmes, and that a cross-scope clear is refused **server-side** with the button removed.
- **Transaction integrity** — force a failure between the `acad_graduands` insert and the
  `acad_grad_review` insert; confirm neither survives.
- **Idempotence** — clearing an already-cleared student twice must not create a second
  `acad_graduands` row.
- **No regression** — a transcript and a certificate for a newly cleared student render exactly
  as they do for an existing graduand.

---

## 10. Open questions for the MIS Manager

1. **Credit shortfall tolerance.** C3 blocks below 90% of required. Is 90% the right line, or
   should any shortfall block?
2. **Failed papers.** Should a single failed paper block, or only block above a threshold — and
   does a passed retake of the same course clear the original failure? (The engine counts a course
   once and takes the best attempt; confirm that is the rule.)
3. **CGPA floor.** Below 2.0 there is no award class. Block, or hold for Senate discretion?
4. **Finance.** In or out? (Currently out — §7.)
5. **The one duplicate** in `acad_graduands` — which row is correct?
