# ODEL & Marks — Concerns, Findings and Fix Plan

**Date:** 2026-09-19
**Trigger:** review of the external consultant's ODEL benchmark proposal (July 2026), plus
"address the concerns, leave no stone unturned, focus on the new interface but take facts from
both the classic and the new interface."

**Status:** PLAN — written before any change is made. Execution log at the bottom.

---

## 0. Method

Everything below was verified against the live database and the live code, not inferred from
memory or from the proposal. Where a claim is quoted from the proposal or from project memory
and turned out to be wrong, that is stated explicitly.

Two interfaces touch the same marks:

| | Screen | Writes |
|---|---|---|
| **Classic** | `UserControls/Results/FacultyExamResults.ascx` | `acad_examresults_faculty` |
| **New** | `NewScreens/ExamResultsInfo.aspx` | `acad_examresults_faculty` |
| **New** | `NewScreens/MarkEntry.aspx` → `MarksSheetService.BulkSaveMarks` | `acad_examresults_faculty` |
| **New** | `NewScreens/AllMarksController.aspx` | `acad_course_registration` (provisional) |
| **New (portal)** | `LecturerProvisionalMarksController.aspx` | `acad_course_registration` (provisional) |
| **New (portal)** | ODEL `CourseworkPush` → `OdelPushService` | `acad_course_registration` (provisional) |

`acad_examresults_faculty` holds **207,053 rows and is current to 2026/2027** — it is live, not legacy.

---

## 1. Findings

### F1 — eportal silently rejects any upload over ~4 MB  *(new interface, ODEL)*

**Verified.** `CampusDynamics_Portal/web.config` has **no `<httpRuntime>` and no `requestLimits`**,
so the ASP.NET 4 default `maxRequestLength` of **4096 KB** applies. Meanwhile:

- `OdelUpload.ashx` enforces a policy limit `max_file_mb`, **defaulting to 20 MB**
- `OdelContentService` enforces `READING_MAX = 10 MB` for readings/images
- `CampusDynamics/web.config` (eadmin) sets `maxRequestLength="51200"` (50 MB)

So the platform kills the request before either app-level check runs. The student or lecturer
spends the data, then gets a connection reset rather than the app's own clean message.

**Corroboration:** `odel_file` holds 24 files, total 16.8 MB, **largest 2.06 MB, zero above 4 MB.**
Nobody has ever successfully uploaded a larger file.

**Blast radius:** every ODEL assignment submission and every library reading/image on eportal.

---

### F2 — `MarksSheetService` scales marks by ratios it never manages to read  *(new interface)*

**Verified.** `LoadRatios` and `GetRatios` run:

```sql
SELECT COALESCE(cw_ratio,0) ... FROM acad_examresults_faculty_settings
WHERE course_id=@course AND progid=@prog AND acad_year=@year AND semester=@sem
```

The table has **`coursework_ratio`** (not `cw_ratio`) and **`acadyear`** (not `acad_year`).
The query therefore always throws; the `catch` swallows it and sets **40 / 0 / 60**.

`BulkSaveMarks` then does:

```csharp
cwWeighted   = Round(CwEntered   * cwRatio   / 100);   // × 0.40
examWeighted = Round(ExamEntered * examRatio / 100);   // × 0.60
total        = cwWeighted + testWeighted + examWeighted;
```

**MRU does not enter marks out of 100.** Coursework is entered 0–40 and exam 0–60, and the total
is a plain sum. The data proves it:

| check | rows |
|---|---|
| `cw_mark = cw_mark_entered` (no scaling) | **36,660** |
| `cw_mark = ROUND(cw_mark_entered × 0.4)` (scaled) | **4** |

Live 2025/2026 rows read `cwE 37 → cwW 37`, `exE 28 → exW 28`, `total 65`.

So a save through this path would turn a legitimate 37 into 15 and a 28 into 17 — every mark
reduced to roughly 40%/60% of its true value.

**Why nothing has burned yet:** `MarkEntry` does not appear in `acad_marks_action_log` at all
(611 AllMarksController, 33 DeadlineManager, 7 CourseCorrectionCentre, 5 AuditCentre, 4
MarksAlertDashboard, 2 AssignmentManager, 2 TeacherDashboard, **0 MarkEntry**). The screen is
built but unused. **This is a loaded gun, not a fired one** — and the user's instruction is to
focus on the new interface, which is exactly where it sits.

---

### F3 — `ExamResultsInfo.aspx` cannot save an edit at all, and its defaults are wrong  *(new interface)*

**Verified.** Both ratio queries on this page filter on `acad_year`, `prog_id` and `study_year`.
The real columns are `acadyear`, `progid`, `cyear`. Running the query live returns:

```
ERROR 1054 (42S22): Unknown column 'acad_year' in 'where clause'
```

Unlike F2 there is **no `try/catch`** around the block in `gvResults_RowUpdating`, so editing a
mark on this screen throws out of the handler. The screen is linked from `SidebarMaster`, so it
is reachable.

Its fallback is also wrong on its own terms: `decimal cwRatio = 30, examRatio = 70;` — MRU is
40/60, not 30/70.

---

### F4 — the settings data is NOT garbage; it encodes "do not scale"

I initially called this table stale and its data nonsense. **That was wrong and the correction
matters**, because it changes what a safe fix looks like.

| | rows |
|---|---|
| total rows | 15,162 (2007/2008 → **2027/2028**) |
| rows where `coursework_ratio + exam_ratio <> 100` | 14,623 (96%) |
| **current years (≥2025/2026) with `100 / 100`** | **2,945 of ~3,015** |

Under the formula `entered × ratio / 100`, a ratio of **100 means "multiply by 1" — i.e. do not
scale** — which is precisely MRU's plain-sum model. The dominant value is therefore *correct*,
not corrupt.

But the tail is dangerous: **43 rows are `cw=0, ex=100`** and **18 rows are `0/0/0`**. If someone
"fixes" F2/F3 by merely correcting the column names, those courses would have their coursework —
or everything — zeroed on save.

**Therefore: correcting the column names alone is NOT a safe fix.**

---

### F4b — `ex_mark_entered` does not exist; eight SQL sites reference it

**Verified live:** `SELECT COALESCE(ex_mark_entered,0) FROM acad_examresults_faculty`
-> `ERROR 1054 Unknown column`. The real column is **`exam_mark_entered`**
(the table has `cw_mark_entered, cw_mark, test_mark_entered, test_mark, exam_mark_entered,
ex_mark, total_mark` — note `ex_mark` IS correct for the weighted column, only the *_entered*
one differs).

Genuine SQL references to the non-existent name:

| File | Line | Kind |
|---|---|---|
| `App_Code/Marks/MarksSheetService.cs` | 65, 333, 379 | read + read + **UPDATE** |
| `COOPERP/NewScreens/MarkEntry.aspx.cs` | 364, 482 (via `GetOriginalValue`, `String.Format` into SQL), 1010 | read |
| `COOPERP/NewScreens/DeanApproval.aspx.cs` | 239 | read (a COUNT) |
| `App_Code/Marks/MarksSheetSyncService.cs` | 79 | read |
| `App_Code/Marks/MarksReconciliationService.cs` | 175 | read |
| `API/v2/staff.aspx.cs` | 1668, **1856** | read + **UPDATE** |

Not SQL (safe): `UserControls/Results/FacultyExamResults.ascx.cs:160` is a DevExpress
`e.OldValues[...]` dictionary key, not a column reference.

**Consequence:** the entire faculty-sheet write path in the new interface *and* in the v2 staff
API throws before it writes. That is why `MarkEntry` has zero rows in `acad_marks_action_log`,
and it is the deeper reason the screen has never been used.

---

### F4c — there is no working weighting configuration anywhere in the system

Three separate code paths try to read a CW/exam weighting. **All three reference columns that do
not exist**, so all three always fall back — and the fallbacks disagree:

| Caller | Reads | Exists? | Fallback | Correct for MRU? |
|---|---|---|---|---|
| `MarksSheetService.LoadRatios/GetRatios` | `cw_ratio`, `acad_year` on `..._settings` | no | **40 / 0 / 60** then *multiplies* | **No** — halves the marks |
| `ExamResultsInfo.aspx.cs` (x2) | `acad_year`, `prog_id`, `study_year` on `..._settings` | no | **30 / 70** then *multiplies* | **No** — worse |
| `API/v2/staff.aspx.cs` | `pc.pcw`, `pc.ptst` on `acad_programmecourses` | **no** (`pcw` not a column) | ratio `0` -> uses `cwEntered` **unscaled** | **Yes**, by luck |

So the proposal's §3.2 weighting model has no foundation to build on, and the only fallback that
matches the 36,660-row reality is the API's accidental one.

---

### F7 — the newer marks services query `acad_examresults_faculty` with the wrong vocabulary

Found while sweeping for other bad columns. `acad_examresults_faculty` names its columns
`acadyear`, `cyear`, and has **no campus column at all**. Two neighbouring tables
(`acad_results_status`, `acad_teaching_assignments`) use `acadyear`, `study_year`, `campus_id`.
Four call sites wrote the faculty table as though it shared the neighbours' vocabulary:

| Site | Bad columns | Effect |
|---|---|---|
| `MarksSheetService.LoadSheet` (~line 72-80) | `ef.acad_year`, `ef.study_year`, `ef.campus`, `ef.midyear_session` | the mark sheet cannot load |
| `MarksSheetSyncService` (~58) | `acad_year`, `study_year`, `campus` | sync counts fail |
| `MarksReconciliationService` (~181) | `ef.acad_year`, `ef.study_year`, `ef.campusid` | reconciliation fails |
| `API/v2/staff.aspx.cs` (~1677) | `ef.acad_year`, `ef.study_year`, `ef.campus` | the sheet read fails |

Verified live: `SELECT ef.acad_year ... FROM acad_examresults_faculty ef` -> `ERROR 1054`.

Checked and **NOT** affected (they target tables that really do have those columns):
`ResultsStatusService`, `MarksAssignmentService`, `MarksWorkflowService`.

Together with F2/F4b this explains why `MarkEntry.aspx` has never been used: its sheet cannot
load and its save could not write. The screen is not merely unused, it is inoperable.

**NOT FIXED — deliberately.** Three of the four names map mechanically
(`acad_year`->`acadyear`, `study_year`->`cyear`, `midyear_session`-> drop), but **`campus` has
no counterpart on this table**. Dropping the predicate is not a rename: it widens every sheet
query to all campuses at once, and if a course runs at two campuses their students would merge
into one sheet. That is an academic decision about what a "mark sheet" is scoped to, not a typo,
and it should not be guessed at by whoever happens to be editing the file.

**What is needed before this can be fixed:** a ruling on whether the faculty mark sheet is
campus-scoped. If yes, the table needs a campus column and a backfill; if no, the predicate goes.
Until then the safest state is the current one — these paths fail loudly instead of returning a
wrong set of students.

---

### F5 — four rows carry fabricated component splits  *(classic interface)*

```
regno          course    year       cwE cwW  exE exW  total
MRU2024001696  MSC3116B  2025/2026   74  30   74  44     74
MRU2024001998  MSC3116B  2025/2026   76  30   76  46     76
MRU2024001696  MSC3115B  2025/2026   70  28   70  42     70
MRU2024001998  MSC3115B  2025/2026   78  31   78  47     78
```

Somebody entered the **total** into both the coursework and the exam box, and the scaling path
split it 40/60. The total is right; the components are invented. Two students, two courses.
Not a code fix — a data question for the faculty.

---

### F6 — the consultant proposal rests on three false premises

Documented so nobody builds from them:

1. **"The teaching allocation is the master key" (Principle 2, F1).** ODEL provisions and gates on
   `acad_programmecourses.lecturer_id` + `is_lecturere_assigned='YES'`. It never reads
   `acad_teaching_allocation`, which exists (15,779 rows) but carries
   `StartTime/EndTime/roomNo/lectureday` — it is the **timetable**. Re-pointing `StaffOnSpace` at
   it would re-break the gate that locked ~59 lecturers out of their own spaces in July 2026.
2. **The three-layer weighting model (§3.2) needs config that does not work.** The only weighting
   config is `acad_examresults_faculty_settings`, and per F2/F3 nothing can read it. ODEL already
   solves this correctly by scaling assignment points into 0–40 via `OdelCore.CwFromPoints`.
3. **§5.1's material model was already replaced.** The proposal specifies
   `odel_material(topic_id, type FILE/PAGE/LINK)`. That flat model was superseded in July by
   Chapter → Topic → reusable library material with kinds `YOUTUBE/READING/IMAGE/PAGE/LINK`.
   Building §5.1 as written is a regression.

**What the proposal gets right and is already built:** the push design (§3.4) is implemented
faithfully — transaction, `FOR UPDATE`, `UPDATE … WHERE mark_stage IN ('NOT_ENTERED','ENTERED')`,
immutable `odel_cw_push`/`odel_cw_push_detail` snapshot with `prev_cw` and `mark_stage_at_push`.
Receipts exist (`receipt_code`, `receipt_hash`) and files carry `sha1`. Quizzes, lectures and
attendance are built despite being scheduled P2.

---

## 2. Fix plan

Ordered by risk-to-students, safest change first. Each item states its verification.

### P1 — Neutralise the mark-scaling landmine  *(F2, F3, F4, F4b, F4c)*

**Decision: remove the scaling, repair only the column name, never the ratio lookup.**

Two distinct repairs, and the order matters. Fixing `ex_mark_entered` alone would *activate*
write paths that are currently dead-on-arrival, and they would come alive still multiplying by
the wrong fallback ratios. **The scaling must be removed in the same change that fixes the
column name, or not at all.**

Rationale: MRU's model is a plain sum of pre-scaled components (36,660 rows against 4). The
dominant config value (`100/100`) already means "do not scale", so removing the multiplication
produces *identical* results for 2,945 of ~3,015 current rows and *avoids* zeroing the 61 rows
whose ratios are `0/100` or `0/0`. Repairing the column names would do the opposite.

1. `App_Code/Marks/MarksSheetService.cs`
   - Fix `ex_mark_entered` -> `exam_mark_entered` at lines 65, 333, 379.
   - `BulkSaveMarks`: stop multiplying. `cw_mark = CwEntered`, `ex_mark = ExamEntered`,
     `test_mark = TestEntered`, `total = sum`. Keep the `*_entered` columns as they are.
   - `LoadRatios` / `GetRatios`: **delete** the broken SQL and return 40/0/60 as display metadata
     only (the UI labels "out of 40" / "out of 60"). A query that can never run is worse than no
     query. Leave a comment saying why it must not be "repaired".
2. `COOPERP/NewScreens/MarkEntry.aspx.cs` — fix `ex_mark_entered` at 364, 482, 1010.
3. `App_Code/Marks/MarksSheetSyncService.cs` (79), `App_Code/Marks/MarksReconciliationService.cs`
   (175), `COOPERP/NewScreens/DeanApproval.aspx.cs` (239) — read-only sites, fix the name so the
   counts stop silently failing.
4. `API/v2/staff.aspx.cs` — fix `ex_mark_entered` at 1668 and 1856. Its ratio fallback already
   yields unscaled marks, so no scaling change is needed there; delete the dead `pc.pcw` lookup
   so it cannot be "fixed" into life later.
5. `COOPERP/NewScreens/ExamResultsInfo.aspx.cs`
   - Delete both dead ratio queries.
   - `cwMark = cwEntered; exMark = examEntered; totalMark = cwMark + exMark;`
   - Keep the existing `totalMark > 100` guard; add component guards (cw ≤ 40, exam ≤ 60) so a
     mistyped 74-in-both is refused at entry instead of silently split (this is what produced F5).
   - Hide/'—' the ratio display panel rather than showing figures nobody can trust.
6. Validate the number a save would produce against the same row's current value for a sample of
   real rows **before and after**, and confirm they are identical for `100/100` courses.

**Risk if wrong:** marks. Therefore: no bulk data change, only the write path; verified by
computing expected vs actual on live rows without writing.

### P2 — Raise the eportal upload ceiling  *(F1)*

`CampusDynamics_Portal/web.config`: add
`<httpRuntime maxRequestLength="20480" executionTimeout="300" />` and a matching
`<requestLimits maxAllowedContentLength="20971520" />`, i.e. **20 MB** — above the app's own
`max_file_mb` default of 20 MB? No: set the platform to **25 MB (25600 / 26214400)** so the
*app's* limit is always the binding one and the user always sees the app's clean message.

Verification: confirm eadmin's existing values as precedent; after the change, confirm a >4 MB
upload is accepted and a >20 MB upload is refused *by the app* with its own message.

**Note:** this recycles the eportal app pool. Students mid-submission would lose an in-flight
request (autosave protects the text answer). Do it deliberately, not casually.

### P3 — Record the realignment  *(F6)*

Write the corrections into the repo next to the proposal so the next reader does not rebuild from
the false premises, and update project memory (which currently contains the wrong claim that the
settings table is unused legacy).

### P4 — Report, do not fix  *(F5)*

The four fabricated component splits are a faculty decision (which of 74 was coursework?). List
them for the registrar; do not guess.

---

## 3. Explicitly NOT doing

- **Not** widening `acad_results.Index_UNQ` — settled 2026-09-18; the single row per course is
  what the retake design depends on.
- **Not** building the proposal's §2 catalogue. Most of P2/P3 is already built; forums are the
  only clean gap and nobody has asked for them.
- **Not** repairing `acad_examresults_faculty_settings` column names anywhere — see F4.
- **Not** touching the 15,162 settings rows. Once nothing reads them they are inert.
- **Not** changing `OdelPushService` — verified correct.

---

## 4. Execution log

*(filled in as each step completes)*

| Step | Status | Evidence |
|---|---|---|
| P1 `MarksSheetService` — `exam_mark_entered` x3, scaling removed, both ratio lookups deleted | **done** | no `ex_mark_entered` left; no `* ratio / 100` left; braces 106/106, parens 193/193; MarkEntry + DeanApproval compile 200 |
| P1 `ProvisionalMarksReleaseController` — C# 7 named tuple replaced with a class | **done** | was 500 `CS1031: Type expected` (pre-existing, `git status` clean before the edit); now 302 |
| P1 column fixes — MarkEntry x3, DeanApproval, SyncService, ReconciliationService, staff API x2 | **done** | only the classic DevExpress dictionary key remained, then fixed too |
| P1 `ExamResultsInfo` — both write paths de-scaled, 3 dead ratio queries removed, component guards added | **done** | braces 147/147, parens 681/681; only a comment mentions the settings table; compiles 302 |
| P1 `API/v2/staff` — dead `pc.pcw` ratio join removed, no scaling | **done** | braces 831/831; compiles 200 |
| **P1 verification** — does the new formula reproduce reality? | **done** | `total = cw_mark_entered + exam_mark_entered` matches the stored total on **36,620 of 36,736 rows (99.7%)**. The 116 that differ are 79 legacy scaled rows + 37 whose stored total was already stale — both pre-existing, neither introduced here |
| P2 eportal upload ceiling 4 MB -> 25 MB | **done** | web.config parses; eportal serves (Default 200, OdelUpload 200); **6 MB POST now reaches the handler** (`uploaded=6291670`, handler answered `{"success":false,"message":"Not signed in."}`); **30 MB POST refused** with "Maximum request length exceeded" |
| P3 realignment recorded (F6) + memory corrected | **done** | this document; memory `lost-marks-root-cause` / ODEL notes updated |
| P4 data items for the registrar | **reported, not changed** | 4 fabricated splits (F5) + 37 rows with a stale stored total; both need a human ruling |
| F7 faculty-sheet column vocabulary | **NOT fixed, by decision** | `campus` has no counterpart on the table; scoping is an academic ruling, not a typo |

### Verification commands used

```sql
-- does the new formula reproduce what is stored?
SELECT SUM(total_mark = cw_mark_entered + exam_mark_entered) AS matches,
       SUM(total_mark <> cw_mark_entered + exam_mark_entered) AS differs
FROM acad_examresults_faculty WHERE cw_mark_entered > 0;

-- and are the differences pre-existing rather than introduced?
SELECT SUM(cw_mark <> cw_mark_entered OR ex_mark <> exam_mark_entered) AS legacy_scaled,
       SUM(cw_mark =  cw_mark_entered AND ex_mark =  exam_mark_entered) AS stale_total
FROM acad_examresults_faculty
WHERE cw_mark_entered > 0 AND total_mark <> cw_mark_entered + exam_mark_entered;
```

```bash
# upload ceiling, both ends
curl -F "file=@6mb.bin"  http://localhost:82/OdelUpload.ashx   # handler answers -> accepted
curl -F "file=@30mb.bin" http://localhost:82/OdelUpload.ashx   # "Maximum request length exceeded"
```
