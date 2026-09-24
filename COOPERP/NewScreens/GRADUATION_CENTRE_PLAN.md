# Graduation Centre — design and build plan

**Status:** built — schema, engine, service and interface are in. Not yet exercised by a signed-in user (see §11).
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
| C2 | **Outstanding papers** | a mark of **1–49** | a mark of **exactly 0**, or no mark at all | counts of each, and the course codes |
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

**Decision (revised 2026-09-24 after measuring the variants — the original "median across
specialisations" rule did not survive contact with the data):**

The spread *within* one programme is too wide for any average to mean anything. BAED has 47
variants ranging 15–211 CU; BED(S) has 10 ranging 2–856. Most of the low ones are stubs — a
specialisation with two courses attached — and some of the high ones are duplicated structures.
A median over that is a number with no defensible meaning, and a graduation decision is not the
place for one.

Required CU is therefore resolved as:

1. **Structure**, *only if credible* — the student's specialisation resolves to a real
   `acad_specialisation` row for their programme AND that variant carries at least 20 courses.
   Source `STRUCTURE`. This is the only source strong enough to **block**.
2. **Declared minimum**, *only if credible for the level* — `acad_programme.mincredit` within the
   band observed for that `levelCode`/`couselength` (Certificate 1yr 20–40, Diploma 2yr 40–130,
   Bachelors 100–220, Masters 30–60, Postgraduate 40–200). Source `DECLARED`. **Warns only.**
3. **Nothing** — source `NONE`, reported as *Not assessable*. Never a silent pass.

The credibility band matters because `acad_programme` contains junk rows whose `progcode` is
actually a **course** code — `BEE1101`, `DCS1101`, `HRP 1101`, `SDB1101` — each carrying
`mincredit = 3`. Without the band, a student on one of those would be "short of 3 credits" and
cleared. With it, they are Not assessable and a human looks.

> **The governing principle:** credits *inform* the decision; they do not gate it, unless the
> requirement came from a real curriculum. The user asked for no room for error, and blocking a
> graduation on a number this data cannot support would be exactly that error.

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

## 9a. Verification results — the step-3 gate, run 2026-09-24

The plan said the interface would not be built until the engine's candidate rule reconciled
against a year that has already graduated. It has been run, and it reconciles.

### The rule that survived

A student is a **candidate** when `MAX(acad_results.studyyear) >= acad_programme.couselength`
— they have reached the final year of their own programme. That is the whole rule. Two earlier
candidate rules were tried against the data and discarded:

| Rule tried | Why it was wrong |
|---|---|
| "has results in the graduation year" | missed 3 of 522. All three finished in an *earlier* year and graduated at the later ceremony — MRU2021000091 (BBA, last results 2023/2024), MRU2022000659 (HEC, 2022/2023), MRU2022000179 (SWSA, 2023/2024), each with a complete result set. |
| "last results year ≤ graduation year" | missed 18 of 522. Those students have results *after* the year they graduated in — post-graduation retakes and late entries. A year boundary is simply not what candidacy means. |

Candidacy is about **completion, not about a year**. The academic year on screen is therefore the
list being compiled, not a filter on who may appear; "when did they finish" is a column and an
optional filter, not a gate.

### Coverage against every graduation year on record

| Graduation year | On `acad_graduands` | Engine identifies | |
|---|---|---|---|
| 2024/2025 | 522 | **522** | ✔ |
| 2023/2024 | 599 | **599** | ✔ |
| 2022/2023 | 59 | **59** | ✔ |
| 2021/2022 | 27 | **27** | ✔ |
| 2020/2021 | 13 | **13** | ✔ |
| 2019/2020 | 34 | **34** | ✔ |
| 2025/2026 | 190 | 187 | 3 unexplained by the rule — see below |
| 2026/2027 | 14 | 2 | 12 unexplained by the rule — see below |

**1,254 of 1,254** graduands across the six completed years are identified. Not one is missed.

### The 15 the engine refuses — and why that is the point

Every one of the 15 outstanding differences is a student **on a graduation list who has not
reached the final year of their programme**:

| Student | Programme | Length | Study year reached | Results |
|---|---|---|---|---|
| MRU2027000002 | `TEST` | 3 | 2 | 17 |
| MRU2025004248 | BIT | 3 | **1** | 6 |
| MRU2025002951 | BEE | 4 | **1** | 12 |
| MRU2025002345 | BEICT | 3 | **1** | 18 |
| MRU2025003775 | BED(P) | 3 | **1** | 26 |
| MRU2025002148 | BCE | 4 | **1** | 12 |
| MRU2025003390 | BAED | 3 | **1** | 17 |
| MRU2025002638 | DAF | 2 | **1** | 10 |
| MRU2023000125 | BCE | 4 | 3 | 38 |
| MRU2023000070 | BCE | 4 | 3 | 38 |
| MRU2024000643 | BEICT | 3 | 2 | 40 |
| MRU2024001453 | BCE | 4 | 2 | 13 |
| MRU2025002173 | DAF | 2 | **1** | 10 |
| MRU2025002536 | BEICT | 3 | **1** | 17 |
| MRU2024000792 | BCE | 4 | 2 | 13 |

A student in year 1 of a three-year degree with six results has not graduated. Twelve of the
fourteen names currently on the **2026/2027** list are of this kind, and one of them is on a
programme literally called `TEST`.

These are not engine misses. They are erroneous rows on live graduation lists, and finding them
is the clearest possible argument for the module: the check that catches them is C-final-year,
and it would have blocked every one at the point of clearing.

**Action:** the Overview tab carries a data-integrity notice listing them. The module does not
delete them — removing a name from a graduation list is a Registrar's decision, not a script's.

### The backlog nobody is looking at

Students who have reached their final year and are on **no** graduation list at all:

| | students |
|---|---|
| finished 2025/2026 | 854 |
| finished 2024/2025 | 87 |
| finished before 2024/2025 | **13,460** |
| **total** | **14,548** |

This is the single largest thing the module will surface. Most of the 13,460 will have
outstanding papers or credit shortfalls — that is what the checks are for — but they have never
been looked at as a queue, because until now there was nowhere to look at them.

## 9b. Engine verification — run 2026-09-24

### CGPA is identical to the authoritative function

The engine computes CGPA as `SUM(CU × gradept) / SUM(CU)`. `acad_CGPAFinder`, which every
transcript in the system uses, computes the same thing. Compared across **1,999 students**:

| compared | agree | differ | worst gap |
|---|---|---|---|
| 1,999 | **1,999** | 0 | **0.00** |

If these ever diverge, the engine is wrong and the transcripts are right.

### The checks, calibrated against students who really graduated

Run over the 522 students on the 2024/2025 graduation list, with C1 (already listed) suppressed
so the other checks can be judged on their own:

| | students |
|---|---|
| clean on every check | **505** (96.7%) |
| flagged for a human | 17 |
| blocked on CGPA | 0 |
| blocked on final year | 0 |

Zero false blocks on CGPA and on duration. The 17 were all C2 — and looking at them changed the
check.

### A mark of zero is a gap, not a failure

Of those 17, only **2** had a mark between 1 and 49. The other 15 had marks of exactly **0** —
`ICT2206=0`, `ICT2219B=0`, and so on — which in this database means a paper that was never
marked far more often than a paper that was failed:

| across all 648,785 results | rows | students |
|---|---|---|
| no score at all (NULL) | 216 | 151 |
| **exactly 0** | **5,380** | 1,692 |
| 1–49 — a real fail | 6,793 | 3,089 |

So C2 blocks on 1–49 and warns on 0, with a message that says which fix is needed: a zero needs a
mark entered, a 41 needs a retake. The effect on the calibration cohort:

| | before | after |
|---|---|---|
| would block | 17 | **2** |
| would warn | 0 | 15 |
| clean | 505 | 505 |

Both remaining blocks are correct. `MRU2023000649` (DME) is on the 2024/2025 graduation list with
**five** failed papers — 28, 31, 33, 34 and 44. That is precisely the name this module exists to
stop, and it is already on a list.

## 9c. Build verification — 2026-09-24

| Step | Result |
|---|---|
| Schema applied | `acad_grad_review` created, `idx_grad_year` added, `acad_graduands` **untouched** (1,621 rows before and after) |
| Engine compiles | yes |
| Service compiles | yes |
| Page compiles, script parses | yes |
| Endpoints deny an unauthenticated caller | all six, including the write endpoints |
| Nothing written by those probes | 0 review rows, 1,621 graduands |
| Sidebar entry | **this was wrong — see §9d** |

### Speed

| query | cost |
|---|---|
| Overview — bucketed pass over 14,548 candidates | **0.015 s** |
| Overview — progress by programme | 0.003 s |
| Candidates — identify and page | 0.002 s |
| Candidates — aggregate one page of 100 | 0.082 s |
| Evidence panel — one student, live | 0.0008 s |

### A bug found in the Overview arithmetic, before anyone saw it

The first cut computed `ready = total − (failed + lowCgpa) − warned − held`, which subtracts a
student who both fails a paper and sits below the CGPA floor **twice** — 286 students were in
both buckets, so Ready was understated by that much and the four numbers did not add up to the
pool. The buckets now partition properly:

| | candidates |
|---|---|
| blocked | 1,587 |
| needs a look | 352 |
| ready | 12,609 |
| **total** | **14,548** |

The blocker *table* still overlaps on purpose — it answers "how many would this one problem
release", not "how do they partition" — and that is now said in the code.

## 11. What has NOT been verified

The interface has never been opened by a signed-in user. eadmin has no master-key login (the
portal's `1111` is a student-portal feature only), so every check above is a compile, a parse, an
unauthenticated denial, or SQL measured directly against the database.

What needs a human with an eadmin account:

1. Open the module and confirm each of the four tabs renders.
2. Confirm the scope line reads correctly for an administrator, a Dean and a HOD, and that a
   Dean sees only their faculty.
3. Clear one candidate and confirm the `acad_graduands` row and the `acad_grad_review` row both
   appear, and that a transcript still prints for that student.
4. Hold one candidate, confirm the reason is required, then release them.
5. Confirm a blocked candidate refuses to clear without a justification.

## 9d. The menu — a verification failure worth recording

§9c originally said *"Sidebar entry — already present from the previous version, no change
needed."* That was checked by grepping the markup for the link and finding it. The link existed.
It could not be seen by anybody it was built for.

`GraduationCentre.aspx` sat inside the **More Features** group:

```html
<li class="cd-sidebar__item cd-sidebar__item--has-submenu" data-roles="admin" data-superadmin="1">
```

The sidebar's filter runs `data-superadmin` **before** it consults any slug grant, and hides
those items for every non-admin unconditionally:

```js
var sa = document.querySelectorAll('[data-superadmin]');
for (var z = 0; z < sa.length; z++) sa[z].style.display = 'none';
```

So although `sys_role_permissions` grants `system.more.graduation_centre` to **dean, hod,
registrar, exam_officer, faculty_staff, admissions and student_services**, not one of them could
reach the page — **30 Heads of Department and 16 Deans**, the module's primary users, and exactly
the people §1 says it is for. An administrator could, but only by opening a cog-labelled
catch-all at the bottom of the menu that nobody would think to look in.

**Fix:** Graduation is now its **own group under Academics**, sitting between *Exam* and
*Student Course Rearrangement*:

```
Academics
  ├─ Students
  ├─ Programmes & Courses
  ├─ Exam
  ├─ Graduation                ← new
  │    ├─ Graduation Centre        GraduationCentre.aspx
  │    ├─ Candidates               ?tab=candidates
  │    ├─ Graduation List          ?tab=list
  │    ├─ Held Candidates          ?tab=held
  │    ├─ Graduating Students      GraduateStudents.aspx
  │    └─ Graduation Analysis      GraduationAnalysis.aspx
  ├─ Student Course Rearrangement
  └─ …
```

It is a group rather than one entry because the module is GET-driven: each tab is a real
destination, so the submenu lands a Registrar straight in the queue they came for instead of on
a dashboard they then have to navigate out of. `fileFromHref` strips the query string before the
slug lookup, so all four tab links resolve to the same grant.

*Graduating Students* and *Graduation Analysis* moved in from More Features too. They are
graduation pages, their grants already name dean/hod/registrar, and they were invisible to every
one of those roles for exactly the same reason this module was.

The item carries `data-roles="all"`, not a hand-written role list. The role filter skips `"all"`
and leaves the decision entirely to the slug grant, which is what the sidebar's own comment calls
the single source of truth: *show a menu item ⇔ the user can open its page*. A second hand-written
gate is a thing that has to be kept in step with `sys_role_permissions` by hand, and the last time
those two disagreed every Dean and HOD lost the page. One gate.

**Super admin** is `role_code = "admin"` — `RoleAccessService` resolves the user's **lowest-id**
active role and, when that is `admin`, adds the `"*"` wildcard, which makes
`applyMenuAccessFilter` return before it hides anything. So a super admin sees it unconditionally.

Who reaches it now, simulating both filters exactly as the JS runs them:

| role | result |
|---|---|
| **admin** (super admin) | sees it — `"*"` wildcard |
| **dean**, **hod** | sees it — the two roles §1 names |
| registrar, exam_officer, faculty_staff | sees it — group + slug grant |
| admissions, student_services | hidden by the Exam group, though they hold the legacy slug |
| vc, bursar, hr_manager, … | hidden |

The slug and its grants are deliberately **unchanged**. They already permitted the right roles;
rewriting them would have been gratuitous risk for a cosmetic tidy.

### A second instance of the same bug, found while checking this

The **Exam** group admits `exam_officer registrar faculty_staff dean hod admin`. Inside it,
*Publish Results (Senate)* carries `data-roles="admin vc dvc"`. `vc` and `dvc` are **not in the
parent group**, so a Vice-Chancellor is hidden at the group level and can never see the page that
is named for them. Not changed here — it belongs to the marks workflow, not this module, and
touching it means re-verifying that menu — but it is the identical fault and worth a one-word fix.

**The lesson for the rest of this plan:** "the markup contains it" is not the same as "a user can
see it". Nothing in §9c's verification table proves a human can reach a thing — which is exactly
what §11 has been saying all along, and why it matters.

## 9e. Focusing on the graduating cycle — and what the second look found

The module now opens on the cycle being graduated rather than on every student who has ever
reached a final year.

### The default year was wrong

`currentYear` was `years[0]` — the highest-sorting academic year in the data. That is
**`2202/2203`**, a transposed digit on four rows belonging to one student. Because these are
CHARACTER columns it sorts above every real year, so the whole module defaulted to a graduation
year that does not exist and showed nothing.

It now comes from `AcademicYearHelper.GetCurrentAcademicYear()`, which reads
`acad_acadyears.is_current_year` — the year the institution says it is in. That is **2026/2027**.
The dropdown also rejects any year outside 2000–2035, so a typo cannot be selected at all.

### "Who to show" — the cycle, by default

| focus | 2026/2027 |
|---|---|
| **This cycle** (default) — last sat in 2025/2026 or 2026/2027 | **997** |
| Everyone not yet graduated | 14,548 |

Without the default, the queue opens on 14,548 people, **13,460 of whom finished before last
year**, and the ~1,000 a Registrar is compiling a list for this week are lost in it. The control
sits beside the graduation year, the Overview prints in words what the figures cover, and the
Candidates tab repeats it so a deep link is self-describing.

The narrowing is written as *"has a result in Y or Y−1, and none after Y"* rather than a
correlated `MAX()`, because two `EXISTS` clauses seek into `Index_UNQ(regno, …)` and stop at the
first row: **0.0029s**, against 0.0049s for the `MAX()` form, and both return 997.

Overview and Candidates reconcile exactly — **997 = 997** — and the partition adds up:
119 blocked + 44 needing a look + 834 ready.

### A typo'd year was exiling real candidates

Ranking students by "last year sat" over a CHARACTER column means `2202/2203` beats
`2026/2027`. MRU2021001253 — BED(P), reached year 3, **53 results** — had four rows carrying it,
so their last year read as the twenty-third century: they could never match a "finished in"
filter, and any "did they sit after this year" test exiled them. Their real last year is
2023/2024.

Every comparison and every MIN/MAX over a year now runs through a **believability test**, not
just the 4-digit pattern. That turned out to matter more than expected — the pattern alone had
been hiding four further kinds of bad year:

| | rows | students |
|---|---|---|
| `0/1`, `2022/2024`, `20222/2023`, `2023/204`, `2202/2203` | **31** | **11** |

Only `2202/2203` matches `^[0-9]{4}/[0-9]{4}$`. The other four never did, so every year-scoped
query in the module had been silently dropping them. They are now ignored for ranking — nobody is
hidden by one — **and reported** on the Overview, because the mark is still filed in the wrong
year and only a human can say where it belongs.

### On the live 2026/2027 list

**12 of the 14 names** on it have not reached the final year of their programme. The integrity
notice now says so on the Overview, scoped to the year on screen.

## 9f. Split into four pages — 2026-09-24

One tabbed page became four independent ones, because a tab strip duplicates what the sidebar
already says and because a single page had to carry the code for all four queues whether you
wanted them or not.

| page | endpoints it carries |
|---|---|
| `GraduationCentre.aspx` — overview | 2 |
| `GraduationCandidates.aspx` — the queue | 7 |
| `GraduationList.aspx` — the output | 4 |
| `GraduationHeld.aspx` — the held queue | 6 |

Shared code lives in exactly one place: `css/graduation.css`, `js/graduation.js` (both cached
once by the browser), and in App_Code `GraduationBootstrap` (the filter lists),
`GraduationStudent` (the evidence payload) and `GraduationExport` (the workbook writer). The
evidence panel is rendered by the shared script and each page supplies only its own buttons, so
a reviewer never meets two layouts for the same question.

### Space

The navy banner and the tab strip are gone — together about 90px of the only screen a Registrar
has, spent restating what the sidebar already says. Everything above the table is now one
wrapping toolbar. Type sizes came down a step throughout (12.5px base, 11.5px in tables).

### The modal moved to the top

A drawer down the right edge fights the sidebar and makes the eye travel the full width of the
screen. It is now centred and anchored near the top, so the evidence appears under the row that
was clicked. It closes on Escape, on the X, or on a click on the backdrop — but only a click
that *starts* on the backdrop, so dragging to select text inside the card does not dismiss it.

### Photographs, and why they needed a handler first

Faces make identification instant, but the photos on disk have a **median size of 469 KB and 89%
exceed 300 KB** — most are uncompressed bitmaps saved with a `.jpg` extension. Fifty rows would
have pulled about **23 MB**.

`StudentThumb.ashx` serves a 72px square at JPEG quality 82 — roughly 4 KB — and writes it beside
the originals so the resize happens **once per student, ever**. The box is CPU-bound, so paying
once beats an output cache that re-resizes.

It takes a student number, never a filename; the name comes from `acad_student.photofile` and is
stripped to its base name before it touches the filesystem. Verified: an unauthenticated request,
an unknown student, an empty parameter and `?r=../../../web.config` all return the same 1,594-byte
placeholder — never a photograph, never an error.

| candidates in the 2026/2027 cycle | with a photo | without |
|---|---|---|
| 1,001 | 875 | 126 (placeholder) |

### Exports

One writer, `GraduationExport`, so every file that leaves the module is branded and laid out the
same way. Each workbook opens with a **Cover** sheet carrying the university, the report, the
scope it was produced for, every filter behind it and the timestamp — a spreadsheet with no
provenance is one nobody can defend in a meeting. Data sheets have a navy header row, frozen
panes, right-aligned numerics typed as numbers, and a footer stating that every name was cleared
by a named reviewer.

- **Overview** → Summary, By programme, Data integrity
- **Candidates** → the whole queue, not the page on screen, with credits, source of the
  requirement, CGPA, class and every count behind the readiness
- **Graduation list** → numbered **within each programme**, the way a list is read out and signed,
  plus a By-programme tally sheet
- **Held** → with a *Still blocked* column, so a hold whose reason no longer applies is visible at
  a glance

CSV output carries the same provenance in comment lines, and any cell starting `=`, `+`, `-` or
`@` is prefixed so a student name can never execute as a formula in someone else's spreadsheet.

### Menu

The sidebar entries now point at the four real pages. The three new ones were registered in
`sys_menu_items` with grants mirroring **exactly** the roles that already hold
`system.more.graduation_centre` — admissions, dean, exam_officer, faculty_staff, hod, registrar,
student_services — because an unmapped page is treated as *always visible*, which would have been
more permissive than the page they were split out of. `COOPERP/sql/academics/graduation_menu.sql`
records it with the undo.

### Verified

All four pages compile; all four scripts parse; every id each script references exists in its own
markup or is injected by `G.mount()`; **all 19 endpoints across the four pages deny an anonymous
caller**, as do all four export posts (302, 130 bytes, no data); and `acad_graduands` is still
1,621 rows with 0 review rows afterwards.

## 10. The five open questions — answered

They were policy, not arithmetic, so each is now a **named constant at the top of
`GraduationEngine`**. Changing Senate's mind is a one-line edit, not an archaeology exercise.
Every one of these decisions is also overridable per student by a reviewer with a written
justification, which is the real answer to "what if the rule is wrong in this case".

| # | Question | Decision | Where |
|---|---|---|---|
| 1 | Credit shortfall tolerance | Blocks below **90%** of required, and **only** when the requirement came from a real curriculum. A shortfall against a declared minimum warns. | `CREDIT_BLOCK_RATIO = 0.90` |
| 2 | Failed papers | **Any** mark of 1–49 blocks. A course is counted once: `acad_results` carries `UNIQUE(regno, courseid)`, so a retake *overwrites* the earlier attempt and only the current mark exists. There is no "best attempt" to choose — the row is the answer. | `PASS_MARK = 50` |
| 3 | CGPA floor | **Blocks** below 2.0. Not a judgement about the student: `acad_gs_award` maps no class at any level below 2.0, so clearing one would produce a graduand whose degree class is literally undefined. | `CGPA_FLOOR = 2.0` |
| 4 | Finance clearance | **Out.** Not requested, and a graduation list that silently enforced a fees rule would be a policy decision taken in code. The engine has a slot for it; nothing is wired to it. Say the word and it becomes check C9. | §7 |
| 5 | The duplicate graduand | **Identified, not deleted.** See below. | — |

### The duplicate

```
ID 935  MRU2021000451  SHARIFAH NAMIREMBE  DSM  2022/2023  3.39  Class II (Credit)  Printed/Printed  2025-01-06
ID 936  MRU2021000451  SHARIFAH NAMIREMBE  DSM  2022/2023  3.39  Class II (Credit)  Printed/Printed  2025-01-06
```

Byte-identical in every field except the primary key — an accidental double-insert, not two
competing records, so there is no information to lose by removing one. It is still not removed
here: taking a row out of a graduation list is a Registrar's action, and this module's whole
argument is that such things should be decided by a person and recorded.

Once it is resolved, this becomes possible and is worth doing, because it turns idempotence from
something the service *guards* into something the database *guarantees*:

```sql
DELETE FROM acad_graduands WHERE ID = 936;      -- the later of two identical rows
ALTER TABLE acad_graduands ADD UNIQUE KEY uq_grad_regno (regno);
```

Until then, `GraduationService.Clear` refuses a student who already has a row, which covers every
path through the interface.

---

## 12. Filters, seamlessness and a configurable export — 2026-09-24

Brief: *"improve the pages and filters, and ensure things are very seamless (under whole module of
graduation) … improve on logic of export, user should be given modal and configure what they need."*

Before designing anything, the four pages were read end to end against the live data. Four defects
came out of that reading, and the first two are the reason the module does not feel finished.

### 12a. What was broken

**1. Every export was silently truncated to 50 rows.**

`GraduationEngine.Page` opens with a guard against an absurd page size:

```csharp
if (f.size < 1 || f.size > 200) f.size = 50;
```

That is right for a screen. But the export handlers ask for the whole queue by setting a large size
and calling the same method:

| Caller | Asked for | Actually got |
|---|---|---|
| `GraduationCandidates` export | `f.size = 5000` | **50** |
| `GraduationHeld` export | `f.size = 2000` | **50** |
| `GraduationHeld` screen | `size: '300'` | **50**, and the page has no pager |

Measured on the live database at the module's own default view — 2026/2027, cycle focus, all
faculties — there are **996 pending candidates**. The Excel file contained **50 of them**, its cover
sheet stated "Rows in this workbook: 50", and nothing anywhere said rows had been dropped. A
workbook that quietly omits 95% of a Senate list is worse than no workbook.

The held queue is 1 student today, so nobody has hit that one yet. They would have.

**2. `boot` is shadowed on all four pages, so Reset loses the graduation year.**

Each page declares `var PAGE = '…', boot = null;` at module scope, then declares
`function boot(o) { … boot = o; … }` *inside* the `DOMContentLoaded` handler. The inner function
declaration shadows the outer variable, so:

* `boot = o` assigns to the local function binding. The module-scope `boot` stays `null` forever.
* The Reset handler closes over the *function*, which is truthy, so `boot.currentYear` is
  `undefined` and `fYear.value = undefined` blanks the year. **Reset silently widened every view to
  all years** on all four pages.
* On Candidates, `render()` sits at module scope where `boot` really is `null`, so the meta line
  never showed the "finishing 2025/2026 or 2026/2027" range it was written to show.

**3. The summary-table freshness is computed, shipped to the browser, and never displayed.**

All four pages emit `window.G_AGE = '<%= StatsAge %>'`. Nothing reads it. The Centre's Refresh
button therefore rebuilds something the user cannot see the age of, which makes it a button with no
visible purpose. Counts come from `acad_grad_stats`; how stale they are is exactly what a reviewer
needs to know before trusting a number.

**4. Cover sheets identify the filter by code, not by name.**

`CoverOf` writes `Faculty: 01`, `Department: 7`, `Programme: BIT`. A cover sheet exists so the file
can be defended in a meeting; `Faculty: 01` defends nothing.

### 12b. The export modal

Export stops being a button that guesses and becomes a short conversation. One shared dialog,
`G.exportDialog`, used by all four pages, laid out as a top-popup in the module's existing modal
language:

1. **What's included** — the live filter restated in plain English, with the true row count fetched
   from the server before the dialog can be used. The user sees "996 rows" *before* choosing, not a
   surprise afterwards.
2. **Rows** — everything that matches, or only the page on screen.
3. **Columns** — the page's full catalogue as grouped checkboxes (Identity / Academic / Progress /
   Decision), with select-all and select-none. Choices persist per page for the session.
4. **Extra sheets** — the summaries that page can produce, each off or on.
5. **Format** — Excel workbook or CSV.

The footer echoes what is about to happen: "996 rows · 11 columns · Excel workbook".

### 12c. Server side

* `GraduationEngine.All(scope, f, cap, out total, out truncated)` walks `Page` in 200-row chunks, so
  every query keeps the shape it was tuned for and no `IN` list grows unbounded. It returns an
  explicit `truncated` flag; the cover sheet states the cap in words when it is hit, rather than
  quietly stopping.
* Each page declares a **column catalogue** — key, header, numeric, and how to read the value off a
  candidate — and the selected keys drive the sheet. One list to maintain, and the dialog and the
  workbook can never disagree about what a column is.
* Readiness is computed in C#, not SQL, so it is applied *before* paging for export purposes by
  requesting the full set and filtering, and the count shown in the dialog is the count after that
  filter.
* `CoverOf` resolves faculty, department and programme to their names.

### 12d. Filters

* Reset restores the default graduation year instead of blanking it (the `boot` fix).
* Search debounces at 350 ms instead of requiring Enter.
* An active-filter strip under the toolbar shows what is narrowing the view, each removable with one
  click. On a screen with eight controls, this is the difference between "no candidates match" being
  informative and being mystifying.
* Held gains a pager and honours the size it asks for.
* The freshness of the counts is shown next to the scope on every page.

### 12e. Verification — run 2026-09-24

A temporary harness (`ZZGradVerify.aspx`, since removed) exercised the real engine against the
live database with a synthetic admin scope. Read-only throughout.

**The truncation, measured rather than assumed.**

| Filter | Matched | Old export | New export | Time |
|---|---|---|---|---|
| 2026/2027, cycle, all faculties | 996 | **50** | **996** | 1.30 s |
| 2026/2027, cycle, readiness = blocked | 996 → 297 after assessment | **50** | **297** | 0.75 s |
| 2026/2027, everyone not yet graduated | 14,547 | **50** | **14,547** | 13.0 s |

996 distinct students, **0 duplicates, 0 out-of-order chunk boundaries** — the chunked walk does
not skip or repeat rows across page joins, which is the failure a paged export would otherwise be
prone to.

**The count in the dialog.** The SQL count is exact for every filter except readiness, which is
decided per student in C#. Blocked on the 2026/2027 cycle matches 996 in SQL and writes 297 — so
below 3,000 candidates the exact figure is computed (0.75 s, affordable while a dialog is open) and
above it the discrepancy is stated instead of hidden. Over 5,000 rows the dialog also says roughly
how long the file will take, because 14,547 rows is a thirteen-second wait someone should agree to
before it starts, not during.

**The workbook.** Loaded through a real XML parser: parses clean, 3 worksheets, sheet names
sanitised to Excel's 31 characters with its illegal characters replaced, no raw angle bracket
reaches a data cell, the incomplete-export banner appears on the cover. The CSV guards
formula injection (`=1+1` → `'=1+1`), quotes embedded quotes and ampersands, and carries the row
count and any truncation in its comment header.

**The catalogue.** Selecting `n,off,a` returns columns in *catalogue* order, not request order, so
two people exporting the same columns get identical files. An unrecognised selection falls back to
the catalogue defaults rather than producing a sheet with no columns.

**The dialog.** Driven headless against the real shared script: the footer reads
`996 rows · 16 columns · 2 extra sheets · Excel workbook`; the Export button is on screen with a
live handler at 1000, 700 and 560 pixels of viewport height; choosing CSV disables the extra-sheet
checkboxes and shows why; unticking a column removes it from the posted `gradCols` and nothing else.

---

## 13. Graduation Analysis — what the seamlessness pass found there

`GraduationAnalysis.aspx` sits in the same sidebar group as the four pages above and predates them.
Reading it for consistency turned up two defects worth more than the styling.

**1. It had no scope at all.** Every query read `acad_graduands` unrestricted, and the faculty
dropdown was `SELECT DISTINCT faculty_code, faculty_name FROM acad_faculty`. A Dean or HOD opening
Graduation Analysis saw **the whole university's graduands**, while the four pages beside it in the
same menu correctly showed them only their own. That is not a difference in presentation — it is
one menu answering the same question two different ways depending on which item you click.

Fixed at the single choke point: all five queries build their WHERE through one
`GetWhereClause(alias)`, so `Scope.ProgFilter(alias, "progcode")` goes there and a sixth query
cannot quietly forget it. The faculty and programme dropdowns are scoped too, and a user with no
scope now gets the same plain refusal the other four pages give. Verified against MySQL: a scope of
three programmes returns 196 graduands across 2 faculties and offers exactly those 2 in the
dropdown, where an unscoped read returns 1,621.

**2. Its Excel exports produced empty files.** `ExportToExcel(gv)` set `AllowPaging = false` and
called `gv.DataBind()`. `LoadAnalysisData()` only runs when `!IsPostBack`, so on an export postback
the grid had no DataSource and `DataBind()` discarded the ViewState rows: the file that came out had
headings and no data. On top of that it wrote an HTML table with a `.xls` extension, which makes
Excel open a "the file format does not match" warning every time, and recorded nothing about which
filters produced it.

The handlers now rebuild the data for the filters on screen and write through `GraduationExport`,
so a file from this page is branded, carries the same cover sheet as one from the graduation list,
and is a real workbook. The detail export ships all four tables in one file, because a reader
asking for the detail almost always wants the summaries that explain it.

The navy `cd-page-header` banner is gone, replaced by the compact identity line the rest of the
module uses — the same ninety pixels the other four pages were already spending on data.

### 13a. Still open, and deliberately not decided here

* **`GraduationAnalysis` largely duplicates the new pages.** Its faculty, programme and class
  tables are what `GraduationList`'s three summary sheets now produce, properly scoped. Retiring
  it would remove a whole design system from the module. That is a call for the Registrar, not a
  refactor to slip in.
* **`GraduateStudents.aspx`** ("Masters Certificate Management") is in the same menu group, has its
  own third design system (`ft-` prefix), and **also has no scope resolution**. It is a different
  function — thesis and supervisor tracking — rather than a duplicate, so it was left alone. Its
  scope gap is real and should be closed the same way.
* Its "Chart Placeholder" was never built, and the PDF button calls `window.print()`.

---

## 14. The review panel, re-ordered around the evidence — 2026-09-24

Brief: *"when previewing a student, show results first … in grid format (2 per row) in years
starting with year 1, y2, y3 … make the width of modal more wide … make the warnings collapsible
and by default collapsed … ensure easiness and going direct to the point."*

**Results first, by study year, two years to a row.** What a student actually did is the evidence;
everything else on the panel is a conclusion drawn from it. It was the third fold down, collapsed,
behind eight checks and six statistics. It is now the first thing on the panel and it opens
expanded.

They are grouped by *study year*, not listed by academic year, because that is how a degree is
read. Each panel carries the academic year it maps to, the course count, the credits earned and the
number failed; a year containing a fail takes a red border and header, so the problem year is
visible before a single row is read. Within a panel, semesters are separated and courses sort by
code. Results the system cannot place in a study year get their own panel, always last — never
first, which a naive numeric sort would have done since their year is 0.

A three-year degree is therefore two rows and fits without scrolling. The grid falls to one column
below 900px, and `align-items:start` keeps a short year from stretching to match the tall one
beside it.

**The modal is 1180px wide**, up from 880. Two panels side by side in 880px put each year's courses
in a scrolling sliver. The export dialog keeps its own narrower 660px through `.g-modal--x`: a form
of six short questions wants the opposite treatment.

**Warnings collapse, blockers do not.** On this data most candidates carry several warnings — 12,829
of the 14,547-strong backlog are WARN — and an open list of them buried the one or two lines that
decide the case. What is *blocking* a candidate stays on the face of the verdict; what merely wants
a look is one click away, with its count on the button.

**A fold bug fixed on the way.** `foldState` read the stored value and then forced any default-open
section back open whenever it was stored closed:

```js
if (folds[key] === false && def) folds[key] = true;   // wrong
```

It could not tell "no stored value" from "stored closed", so a section you deliberately collapsed
reopened on the next student, every time. It now distinguishes the two, and a remembered choice
beats the default in both directions.

**Verified** headless against the real shared script with a three-year degree carrying a fail, a
zero, an unmarked paper and an unplaced result: first block is the year grid; panels read
`Year 1, Year 2, Year 3, Not placed in a year`; the grid computes to 2 columns at 1280px and 1 at
820px; the warnings fold starts collapsed, opens on click, persists the choice, and holds all 3
warnings; the structure fold stays closed and decision history stays open; the Clear button remains
on screen with a live handler.
