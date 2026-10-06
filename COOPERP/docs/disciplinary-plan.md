# Student Disciplinary module: plan

Prepared 6 October 2026 by MIS for the Academic Registrar, the Dean of Students and MIS.
Status: **Built and live, 6 October 2026 (Phase 2).** Section 13 records what was built, the defaults taken for the open questions and the test results. The user guide is `COOPERP/docs/disciplinary-user-guide.md`.

Scope: a Disciplinary module in eadmin (dashboard, records, case file, settings, reports) and in eportal (My Disciplinary Cases, the restricted-access page and the enforcement of sanctions).

---

## 0. Summary of decisions

Where this plan departs from or adds to the brief, it says so here. Each point is explained in the section named.

| # | Decision | Why | Section |
|---|---|---|---|
| D1 | A blocked student's single page is **their own case page**: the restriction notice, the case number, the office to contact, their decisions, sanctions and letters, and the appeal form. Nothing else of the portal. | The brief says the blocked student sees "nothing else", but its own fairness rule 4 says the student must always be able to see every decision against them and how to appeal. A block that hides the appeal route would be unfair and open to challenge. | 6.1 |
| D2 | Interim measures (block now, withhold results now, suspend pending hearing) are stored as **sanction rows with source INTERIM**, enforced exactly like decided sanctions. They need the restriction permission. A reporting officer can *recommend* one; an authorised officer confirms it. | One enforcement path. Reporting officers include any lecturer or invigilator; letting them block a student's portal on their own would breach rule 3. | 4, 8 |
| D3 | **One source of truth for restrictions:** the database function `dc_effect_active(regno, effect)`. Every check in eadmin, eportal, the registration triggers and the clearance gates calls it (directly or through a thin C# wrapper). | A restriction is then exactly the same everywhere, and adding an enforcement point never means re-implementing the rule. | 6 |
| D4 | Restrictions are **date-bounded in the function itself**, so a sanction stops applying at the end of its end date even if nothing else runs. A nightly MySQL event then records the ending in the case file and notifies the student. | Lifting must not depend on a job running. The event scheduler is on now but was found off once before. | 2, 6.6 |
| D5 | **Suspension blocks registration in the database**, by BEFORE INSERT triggers on `acad_registration` and `acad_course_registration`. | There is no central registration function: about ten code paths insert registrations (portal wizard, five eadmin screens, API v2, stored procedures). A trigger is the only way to cover them all. MySQL 5.6 allows one such trigger per table, so the existing `trg_acadreg_block_autoreg` is replaced by one that keeps its two rules unchanged and adds this one. | 6.3 |
| D6 | Status order: **Hearing scheduled** comes before **Summoned**. A summons can also set the hearing in one step. | "Summoned requires a hearing date and generates the letter" (brief) implies the hearing is fixed first. | 4 |
| D7 | The appeal window runs from the day the decision is **notified to the student**, not the decision date. | A student cannot appeal a decision they have not been told about. | 4, 7 |
| D8 | Results withheld is the module's own restriction. It does **not** reuse `fin_studentlocks` (owned by Finance, shared with fee locks) or `approved_by='HELD'` (per course, no reason column, ignored by the portal). | Reusing either would mix disciplinary and finance holds, and the portal does not honour HELD at all. | 1.3, 6.2 |
| D9 | Fines, restitution and result cancellations create **follow-up tasks**, never writes. The Bursar bills through fees screens, and the marks office cancels through marks screens with the case number as the reason; each then records "done" in the case file. | Brief rule 8. | 6.5 |
| D10 | Expulsion bundles four effects: expulsion, portal block, graduation bar and results withheld. The module does **not** change `acad_student.new_status`; the Registrar does that through NewStudentInfo, quoting the case number. | Rule 8 spirit; the student record has its own owners and audit. | 3, Q6 |
| D11 | **Security holes found during the study are closed as part of this work**, because enforcement depends on them: an unauthenticated results endpoint, unchecked result prints, handlers and AJAX requests that skip the gates, and a session-restore gap. | A results-withheld sanction is meaningless while `COOPERP/Mobile/Default.aspx` returns any student's results to anyone. | 6.1, 6.2 |
| D12 | Student notices use the module's own `dc_notification` table, shown in the portal alert zone, on My Disciplinary Cases and on the restricted page. | The Communications module (`sys_communications`) cannot target one student. | 7 |
| D13 | Letters are real PDF files (DevExpress, as transcripts and the graduation list), stored privately under `Data_Private\Disciplinary`, with the Registrar's name, title and signature taken from the existing `admission_letter_config` keys. | One source for the Registrar's details; issued letters are immutable records. | 5.8 |
| D14 | Two new roles: **Dean of Students** (`dean_students`) and **Disciplinary Committee** (`dc_committee`). | `student_services` exists but has no users and no defined duties; there is no Dean of Students role. | 8 |
| D15 | Every opening of a restricted case (view, export, letter, attachment) is written to an access log. | Confidential cases need a record of who looked, not only who changed. | 2, 8 |
| D16 | Graduation check **C9 cannot be overridden** when a graduation bar, expulsion or suspension is in force; an open case without a bar is a warning. | Today any BLOCK can be overridden with a note. A barred student must not be approvable by an HOD. | 6.4 |
| D17 | The module gets its own sidebar heading, **Student Discipline**, shown by permission. | The Academics heading's legacy role filter hides it from the exam officer, VC, auditor and the two new roles (the same problem met with Fixed Assets). | 5 |
| D18 | Sexual harassment and misconduct cases are **restricted by default** (a property of the case type). | Protects complainants and the accused. | 3 |

---

## 1. What exists today

### 1.1 Portal access control (eportal)

| Item | Where | What it does | Use |
|---|---|---|---|
| Global gate | `Global.asax` `Application_PreRequestHandlerExecute` (lines 136-200) | The only check that runs for every page. Skips anything not `.aspx` (line 147), skips `?ajax=` (line 164), skips staff, uses `Session["regno"]`. Gates: unread email reply, then must-collect-email. Fails open. | **The block check goes here once**, placed before the `.aspx` filter so it also covers `.ashx` handlers, and not skipping `?ajax=`. |
| Master gates | `PortalMaster.master.cs` `Page_Load` (from line 84) | ForcePasswordChange, ForceRead, ID collection, MailResponse, MyEmail, enrolment gate, then alerts. Each lists its exempt pages inline. | The restricted page must be added to each gate's exemptions so the gates do not loop (the "three places"). |
| Enrolment gate | `App_Code\Portal\PortalHelper.cs` `EnforceEnrollmentGate`, `ExemptPaths` (lines 37-59) | Third gate list. | Add the restricted page and the disciplinary file handler. |
| Login | `COOPERP\fonts\lg_modern.ascx.cs` `Login1_Authenticate` (182), `Login1_LoggedIn` (641) | Sets `Session["regno"]` via `ResolveCanonicalRegno`; master key 1111 logs in as any student; 730-day auth cookie. | Login stays allowed (the student must be able to read their case); the gate then sends them to the restricted page. The block applies to master-key sessions too. |
| Session restore gap | `Global.asax` line 161 | After a restart, a valid auth cookie with no session skips the gate; the session is rebuilt later by the master. | The new check falls back to `User.Identity.Name`. |
| Results pages | `StudentResults.aspx`, `UserControls\Academics\Results.ascx`, `COOPERP\Academics\PrintResults.aspx`, dashboard `SystemApplications_Modern.ascx` (`LoadCGPA`, `LoadLatestResults`), `MyCourses.aspx` provisional chips, `COOPERP\Academics\CourseworkResults.aspx`, `COOPERP\Mobile\Default.aspx` | Only two of them check the finance lock today; PrintResults and the dashboard check nothing; the mobile endpoint has **no authentication**. | Each gets the results-withheld check; the mobile endpoint is fixed (session required, own regno only). |
| Exam clearance card | Portal link in `SystemApplications_Modern.ascx` (around line 1032); PDF built in **eadmin** `API\doc_verification.aspx.cs` (`doc=="Student Exam Card"`, line 148), gated only by the finance access policy and cached by policy fingerprint | | Refused there for suspension and results withheld; the restriction joins the cache fingerprint. The portal hides the link. |
| Registration | Portal: `SemesterRegistrationWizardService.RegisterNewSemester` (181) and `SelectExistingSemester` (359), `CourseRegistration.aspx`, `StudentCourseRegistrationController.RegisterCourses` (230), `RetakeRegistration.aspx` | | Friendly refusal messages there; the database trigger is the guarantee (D5). |
| Graduation (portal) | `MyGraduation.aspx`, `GraduationList.aspx` via `App_Code\Portal\GraduationPortal.cs` | | Hidden for a student with results withheld. |
| Complaints | `support_tickets` (+ `_messages`, `_attachments`) in `campus_dynamics_portal`; portal `NewTicket`/`MyTickets`/`TicketView`; eadmin `TicketsController.aspx` | The "Complaint or Suggestion" issue type is how students raise complaints. | A case can reference a ticket; the ticket screen gets "Open disciplinary case". |
| Alerts | `App_Code\Portal\PortalAlerts.cs`, `AppraisalReminder` pattern, alert zone in `PortalMaster.master` (line 303) | Staff only today. | A student loader for unread disciplinary notices. |
| Email | `App_Code\communications\EmailSenderProtocol.SendHtmlEmail` | Unreliable (known). | Secondary channel only. |

### 1.2 eadmin hooks

| Item | Where | Use |
|---|---|---|
| Graduation checks | `App_Code\Graduation\GraduationEngine.cs`: `GradFinding`, `Assess` (850-974, C1..C8), `Enrich` (550-626, set-based per page), `Overview` (727-751); `GraduationService.Load` (58-174) and `Clear` (265-341, override rule at 291-304) | Add **C9** in `Assess` after C8, its data in `Enrich` and `Load`, the count in `Overview`, and a non-overridable guard in `Clear` (D16). The hold reason "A disciplinary matter is unresolved" already exists in `GraduationReasons.cs:147`. |
| Document print gate | `App_Code\AcademicDocuments\GraduationPrintGate.cs` (`Check` 98, `Decide` 186), called by `TranscriptPrint.aspx` and `NewStudentInfo` academic documents | Add the disciplinary rule in `Decide`: transcript and certificates refused while results are withheld. |
| Exam clearance | `acad_registration.examClearance` set in `FeesRegistration.ClearStudent` (1654), `StudentsRegistration.ClearStudent` (1725) and the legacy ClearanceCentre | Refuse clearance while suspended; warn while a case is open. |
| Marks audit | `MarkAuditContext.Set(conn, tx, source, reason)`; `MarksControllerShared.SaveAdminMarks(..., note)`; `MarksAuditTrail.aspx` source labels | Cancellations are done by the marks office through these screens with reason "DC/2026-27/0042: ..."; add a "Disciplinary" source label. |
| Exams | `acad_exam_timetable` (ID, exam_date, course_code, venue, acad_year, semester, invigilator_id) | An exam-related incident references the timetable row, the course, the session and the invigilator. |
| Student record | `StudentProfile.aspx` (badge area, line 101), `NewStudentInfo.aspx` profile | A sanction banner on the staff side. |
| Summary prose | `GraduationAnalytics.Summarise` (rule-based C#, no AI) | Same approach for the Senate Summary. |
| Letterhead and PDF | `GraduationPdf`, `FaPdf` (DevExpress), crest `~/COOPERP/images/mru-crest.png`; Registrar details in `admission_letter_config` (`registrar_name`, `registrar_title`, `registrar_sig_path`) | Letter PDFs and reports. |
| Area scope | `MarksScopeResolver.Resolve()` (dean by faculty, HOD by department, `ProgFilter`) | Case visibility for deans and HODs. |
| Student lookup, photo | `acad_student` (`regno`, `firstname`, `othername`, `progid`, `studCampus`, `photofile`, `email`); `StudentThumb.ashx` | Case form. |
| Exports, toolkit | Fixed Assets `FaExport`, `FaPdf`, `fa.js` export dialog (built on the Graduation pattern) | Reused for reports and lists. |

### 1.3 What does not exist

No disciplinary, misconduct or malpractice table, screen or record anywhere in either codebase. `ResultsHoldList.aspx` shows a "Disciplinary" label but stores nothing. There is no per-student notice, no Dean of Students role and no letter-template table.

---

## 2. Database design

All tables are in `campus_dynamics`, InnoDB, utf8 (matching `acad_student`). The full SQL is in Appendix A (schema), B (registration guard), C (seed) and D (menu and roles).

| Table | Purpose | Key points |
|---|---|---|
| `dc_settings` | Key-value settings | Appeal window 14 days, overdue 14 days, appeal-closing warning 3 days, contact office, appellate authority, letter office |
| `dc_case_type` | Case types | Code, name, severity (MINOR, SERIOUS, GROSS), exam-related flag, restricted-by-default flag, sort, active |
| `dc_sanction_type` | Sanctions | Outcome (sanction, dismissal, acquittal); **effects** as a SET (PORTAL_BLOCK, RESULTS_WITHHELD, SUSPENSION, EXPULSION, GRADUATION_BAR, CANCEL_PAPER, CANCEL_SEMESTER, FINE, RESTITUTION); which fields it needs (amount, dates, course, semester); whether it may be an interim measure |
| `dc_case_type_sanction` | Default sanctions for each case type | |
| `dc_committee_member` | Committee per academic year | Staff, panel role (chair, member, secretary, student representative, appellate) |
| `dc_letter_template` | Letter templates | Subject and body with `{{merge_fields}}` |
| `dc_sequence` | Number sequences | `CASE:2026-27`, `INCIDENT:2026-27`; claimed with a row lock |
| `dc_incident` | One incident, one or more students | Incident number, type, date and time, place, campus, description, reporting officer, witnesses, complaint ticket, exam timetable row, course, exam session, invigilator |
| `dc_case` | One case per student per incident | Case number `DC/2026-27/0042`, student snapshot (name, programme, faculty, department, campus, year), type, severity, **status**, restricted flag, case officer, next hearing, decision summary, notified date, appeal deadline, closed date, last entry date, row version. Indexed by regno, status, year, faculty, hearing date |
| `dc_entry` | **The case file, append-only** | Type (26 kinds), when it happened, title, body, details, **student_visible**, status before and after, sanction link, corrects-entry link, recorded by, role, recorded at, **interface** (EADMIN, EPORTAL, SYSTEM) and **IP address**, idempotency key |
| `dc_attachment` | Evidence, minutes, letters, appeal documents | Stored outside the web roots; student-visible flag; removal hides with a reason |
| `dc_hearing` | Hearings | Date and time, venue, panel, status (scheduled, adjourned, held, cancelled), attendance |
| `dc_sanction` | Sanctions and interim measures in force | Effects copied from the type at the time, source (INTERIM, DECISION, APPEAL), amount, from and to dates, course and semester, terms, **status** (ACTIVE, LIFTED, EXPIRED, VARIED, SET_ASIDE), follow-up (marks or fees action owed and done), who ended it and why. A variation ends the row and adds a new one |
| `dc_appeal` | Appeals register | Grounds, lodged via, within the window or late (with reason), outcome (upheld, dismissed, varied, withdrawn) |
| `dc_letter` | Letters issued | Letter number, template, the merged text exactly as issued, PDF, issued by and when |
| `dc_notification` | Notices to the student | Portal read time; email address, status, attempts, error |
| `dc_audit` | Before and after of every write | Entity, action, reason, actor, role, interface, IP |
| `dc_access_log` | Who opened a restricted case | View, export, letter or attachment |

**Guard triggers** (tested, 11.1): no delete on any of these tables; no update at all on the case file, the audit, letters and the access log; sanction terms cannot be edited and an ended sanction cannot be reactivated.

**Functions** (the single source of truth, D3):
- `dc_effect_active(regno, effect)` returns 1 when an ACTIVE sanction with that effect covers today.
- `dc_open_case_count(regno)` counts cases not closed or withdrawn.
- `dc_restriction_summary(regno)` lists the effects in force, for banners and gates.

**Procedure and event:** `dc_expire_sanctions()` ends every ACTIVE sanction whose end date has passed, writes a student-visible SYSTEM entry ("Portal access blocked ended"), a notice and an audit row, one transaction per sanction. `ev_dc_expire_sanctions` runs it daily at 00:10. Re-running it does nothing (tested).

**Files:** `E:\OneDrive\Campus Dynamics MRU\Data_Private\Disciplinary\{caseId}\`, beside the existing `AppraisalEvidence` folder, outside both web roots, read by both applications.

---

## 3. Seed lists (for approval)

### 3.1 Sanctions and their system effects

| Code | Sanction | Effects in the system | Needs | May be interim |
|---|---|---|---|---|
| WARN_VERBAL | Verbal warning | none (recorded only) | | |
| WARN_WRITTEN | Written warning | none | | |
| COMMUNITY | Community service | none | from and to dates; hours in the terms | |
| FINE | Fine | FINE: follow-up for the Bursar to bill | amount | |
| RESTITUTION | Restitution | RESTITUTION: follow-up for the Bursar | amount | |
| CANCEL_PAPER | Cancellation of a paper result | CANCEL_PAPER: follow-up for the marks office; C9 warning until done | course, year and semester | |
| CANCEL_SEM | Cancellation of a semester's results | CANCEL_SEMESTER: follow-up for the marks office | year and semester | |
| SUSPENSION | Suspension | SUSPENSION: no registration, no exam clearance or card, C9 block | from and to | yes |
| EXPULSION | Expulsion | EXPULSION, PORTAL_BLOCK, GRADUATION_BAR, RESULTS_WITHHELD | from | |
| WITHHOLD | Withholding of results or transcript | RESULTS_WITHHELD | from, to optional | yes |
| BAR_GRAD | Barring from graduation | GRADUATION_BAR: C9 block | from, to optional | |
| PORTAL_BLOCK | Portal access blocked | PORTAL_BLOCK | from, to optional | yes |
| DISMISSAL | Dismissal of case | outcome: none | | |
| ACQUITTAL | Acquittal | outcome: none | | |

### 3.2 Case types

| Code | Type | Severity | Exam related | Restricted by default | Default sanctions (plus dismissal and acquittal) |
|---|---|---|---|---|---|
| EXAM_MALPRACTICE | Examination malpractice | Serious | yes | | written warning, cancel paper, cancel semester, suspension, expulsion, withhold |
| IMPERSONATION | Impersonation | Gross | yes | | cancel paper, cancel semester, suspension, expulsion, bar graduation, withhold |
| FORGERY | Forgery of documents or results | Gross | | | cancel semester, suspension, expulsion, withhold, bar graduation, portal block |
| FEES_FRAUD | Fees fraud or forged payment evidence | Gross | | | restitution, fine, withhold, suspension, expulsion, portal block |
| THEFT_DAMAGE | Theft or damage to University property | Serious | | | written warning, restitution, fine, community, suspension, expulsion |
| VIOLENCE | Violence, assault or threats | Gross | | | written warning, community, suspension, expulsion |
| SEXUAL_MISCONDUCT | Sexual harassment or misconduct | Gross | | **yes** | written warning, suspension, expulsion, bar graduation |
| DRUGS_ALCOHOL | Drug or alcohol abuse on campus | Serious | | | verbal and written warning, community, fine, suspension, expulsion |
| STAFF_INDISCIPLINE | Indiscipline towards staff | Serious | | | verbal and written warning, community, suspension |
| ICT_MISUSE | Breach of ICT or portal use policy | Minor | | | verbal and written warning, portal block, fine, suspension |
| PLAGIARISM | Plagiarism or academic dishonesty | Serious | yes | | written warning, cancel paper, cancel semester, suspension |
| UNAUTH_PROTEST | Unauthorised protest or disruption | Serious | | | written warning, community, fine, suspension, expulsion |
| OTHER | Other | Minor | | | verbal and written warning, community, fine, suspension |

### 3.3 Letter templates

Summon to appear; Notice of decision; Suspension letter; Lifting of sanction; Clearance letter (no pending case); Appeal decision. The full text is in Appendix C. Merge fields: student name, regno, programme, faculty, campus, case number and type, incident date and place, hearing date, time and venue, decision text, sanctions, appeal deadline and window, appellate authority, lifted sanction and reason, suspension dates, contact office, today. The office line, the Registrar's name, title and signature come from settings (D13). Templates are edited in Settings; each issued letter keeps its own text.

---

## 4. Status flow

```
REPORTED ──> UNDER_INVESTIGATION ──> HEARING_SCHEDULED ──> SUMMONED ──> HEARD ──> DECIDED ──> CLOSED
    │               │                       │   ^              │  (adjourn: stays,          │       ^
    │               │                       └───┘ re-summon    │   new date, new letter)    v       │
    │               └──────────────────────────────────────> SUMMONED (summon sets hearing) UNDER_APPEAL
    │                                                                                       │
    └── any status before DECIDED ──> WITHDRAWN                                             v
                                                                                     APPEAL_DECIDED ──> CLOSED
```

| From | To | Who (slug) | Requires | Effect |
|---|---|---|---|---|
| (new) | REPORTED | report | student(s), type, severity, date, description; reporter | case number, CASE_OPENED entry, optional interim measures (need `restrict`) |
| REPORTED | UNDER_INVESTIGATION | manage (in scope) | note | STATUS_CHANGE entry; case officer set |
| REPORTED, UNDER_INVESTIGATION | HEARING_SCHEDULED | hearing | date, time, venue, panel from the committee list | hearing row; HEARING_SCHEDULED entry (internal) |
| REPORTED, UNDER_INVESTIGATION, HEARING_SCHEDULED | SUMMONED | hearing | hearing details (set here if not yet), at least 3 days' notice unless a reason is given | **Summon letter** issued; SUMMON entry (visible); student notified |
| SUMMONED, HEARING_SCHEDULED | (same, adjourned) | hearing | new date and reason | ADJOURNED entry; new summon letter |
| SUMMONED | HEARD | decide | attendance (present, absent, represented), panel present, minutes (attachment optional) | HEARING_HELD entry (internal) |
| HEARD; also UNDER_INVESTIGATION for MINOR cases only | DECIDED | decide | findings, decision text, at least one sanction **or** dismissal or acquittal, decision date | sanctions created and effects applied in one transaction; interim measures ended or confirmed; DECISION entry (visible); **Notice of decision** issued; student notified; appeal deadline = notified date + window |
| DECIDED | UNDER_APPEAL | the student (portal) or manage on their behalf | grounds; within the window, or late with a reason and `appeal` permission | appeal row; APPEAL_LODGED entry (visible); sanctions stay in force unless `restrict` suspends them |
| UNDER_APPEAL | APPEAL_DECIDED | appeal | outcome (upheld, dismissed, varied), decision text, new sanctions if varied | old sanctions SET_ASIDE or VARIED, new ones created; APPEAL_DECISION entry (visible); letter; notified |
| DECIDED (window passed or waived), APPEAL_DECIDED | CLOSED | manage | none (closing note optional) | CASE_CLOSED entry. **Sanctions continue** until their end dates |
| any before DECIDED | WITHDRAWN | restrict | reason (at least 10 characters) | interim measures lifted; CASE_WITHDRAWN entry (visible); student notified |

**Not allowed:** going back to an earlier status, reopening a closed or withdrawn case (a new case is opened that refers to it), or deciding a SERIOUS or GROSS case without a hearing.

---

## 5. Screens

All eadmin screens use `SidebarMaster`, `RequireSlug`, the shared toolkit and stylesheet (flat, radius 0, the four tokens), PageMethods through one permission wrapper, and a shared header with tabs (Dashboard, Records, Record updates, Settings, Reports), each shown only with its permission. Every list, count, search and export applies the **visibility rule** (8.2). Sidebar: own heading **Student Discipline** (D17).

### 5.1 Disciplinary Dashboard (`DisciplinaryDashboard.aspx`, `discipline.dashboard`)

- **Filters:** campus, faculty, academic year, case type, date range.
- **Tiles:** open, awaiting hearing, awaiting decision, under appeal, closed in period, average days from report to decision.
- **Charts:** by type, severity, campus and faculty; new cases per month and per academic year.
- **Lists:** students under sanction now (suspended, blocked, results withheld, barred) with counts; overdue (no entry in 14 days; hearing passed with no outcome; appeal window closing in 3 days; follow-ups owed by marks or fees).
- **Drill-through:** every figure opens Records already filtered.
- **One PageMethod:** `GetDashboard(filterJson)` returns tiles, series, sanctions-now and overdue lists in one call.

### 5.2 Disciplinary Records (`DisciplinaryRecords.aspx`, `discipline.records`)

- **Filter bar:** campus, faculty, department, programme, type, severity, status, academic year, reporting officer, flag (overdue, under sanction, restricted), search by name, regno or case number. Kept in the URL.
- **Table:** case number, student (photo, name, regno), programme, type and severity, status, next hearing, last update, sanctions in force, officer. Paged on the server.
- **Row:** opens the case file (5.3).
- **Batch actions:** export; print the summon list for selected cases; schedule one joint hearing for linked cases (one incident).
- **New case:** a modal in three steps.
  1. **Students:** lookup by regno or name, with photo, programme, year, campus and current status; any open case or sanction is shown. Several students may be added.
  2. **Incident:** type (severity defaulted, editable); date and time; place; campus; description; reporting officer (defaults to the user); witnesses; related complaint ticket; exam paper (timetable row, course, session, invigilator) when the type is exam related.
  3. **Evidence and measures:**
     - attachments, each with a description;
     - interim measures: block portal, withhold results, suspend pending hearing. Each has a reason; it applies at once with `restrict`, otherwise it is recorded as a recommendation;
     - confidentiality, defaulted from the type.
- **On save:** one incident, one case per student, case numbers claimed, CASE_OPENED entries, measures applied, all in one transaction.
- **PageMethods:** `GetBootstrap`, `GetCases(configJson)`, `SearchStudents(q)`, `SearchStaff(q)`, `SearchTickets(q)`, `SearchExamPapers(q)`, `CreateIncident(json)`, `CountExport`. Export is a form POST, as in Fixed Assets.

### 5.3 The case file (`DisciplinaryCase.aspx?id=`)

- **Header:** photo, student, case number, linked cases, status stepper, restricted badge, sanctions in force (with end dates), next hearing, appeal deadline.
- **Timeline:** every entry in order with type, title, text, attachments, who, role, when, interface and IP. Student-visible entries carry a "visible to student" mark. Corrections link to what they correct.
- **Action panel**, showing only what the status and the user's permissions allow:
  - Note, finding, statement, evidence;
  - Schedule hearing, summon, adjourn, record hearing;
  - Record decision: pick sanctions from the type's list and fill each one's fields; the dates and effects are shown before saving;
  - Lodge appeal on the student's behalf, decide appeal;
  - Vary or lift a sanction, apply or lift a portal block;
  - Record a follow-up done (marks or fees);
  - Issue a letter, previewed before issue;
  - Notify the student again;
  - Close, withdraw.
- **Tabs:** Timeline; Sanctions (history of every sanction row); Hearings; Letters; Attachments; Audit (before and after).
- **PageMethods:** `GetCase(id)`, `AddEntry(json)` (all entry types; one transaction each; idempotent by client id), `PreviewLetter(caseId, template)`, `IssueLetter(json)`, `ChangeStatus(json)`, `RecordDecision(json)`, `DecideAppeal(json)`, `EndSanction(json)`, `VarySanction(json)`.
- **Files:** `DcFile.ashx` (permission check plus the access log for restricted cases) and `DcUpload.ashx`.

### 5.4 Record Updates (`DisciplinaryUpdates.aspx`)

A feed of entries across all visible cases, newest first, filtered by date, type, case type, faculty and officer. It shows what changed this week; each row opens its case file. Export is available.

### 5.5 Case Types and Sanctions (`DisciplinarySettings.aspx`)

Tabs:
- Case types, with their default sanctions;
- Sanctions, with effects and needed fields;
- Letter templates, with a preview using a sample case;
- Committee for the year;
- Settings (appeal window, thresholds, contact office, appellate authority).

Sort order and an active flag on everything; nothing is deleted; each change is audited.

### 5.6 Reports (`DisciplinaryReports.aspx`): section 9.

### 5.7 eportal

- **My Disciplinary Cases** (`MyDisciplinaryCases.aspx`, PortalMaster): the student's cases. For each case:
  - number, type, status, hearing date and venue when summoned;
  - decision and sanctions (with dates), appeal deadline and how to appeal;
  - letters (download), student-visible entries and attachments.
  - **Lodge appeal** within the window, with grounds and attachments. It creates the appeal, the APPEAL_LODGED entry (interface EPORTAL with the student's IP) and a confirmation notice.
  - Never shown: internal entries, witness names, panel deliberations, restricted internal entries.
  - A dashboard link appears only when the student has a case.
- **Restricted page** (`AccessRestricted.aspx`, no navigation): the notice ("Your portal access is restricted under case DC/...") and the contact office, followed by the same content as My Disciplinary Cases for that student, and sign-out (D1).
- **Alerts:** an unread disciplinary notice appears in the alert zone with a link to the case.
- **Files:** `DcFile.ashx` (portal) serves only the student's own letters and student-visible attachments.

### 5.8 Letters

Issuing a letter:
1. merges the template with the case;
2. shows a preview;
3. on Issue, builds the PDF (letterhead, crest, office line, reference = letter number, date, addressee block, subject, body, the Registrar's name, title and signature);
4. stores it with its SHA-1, records LETTER_ISSUED, attaches it to the case file and, when the template is a student copy, makes it visible and notifies the student.

A letter is never regenerated or edited; a correction is a new letter.

---

## 6. Enforcement

### 6.1 Portal block

- **Where:** one check in `Global.asax` `Application_PreRequestHandlerExecute`, before the `.aspx` filter.
- **Who it applies to:** student sessions only, including master-key sessions. The regno is resolved from the session, else from the auth cookie (closing the restore gap).
- **The check:** `DisciplinaryGate.IsBlocked(regno)` calls `campus_dynamics.dc_effect_active(regno,'PORTAL_BLOCK')`. The answer is cached in the session for 60 seconds, so a new block takes effect within a minute.
- **When blocked:**
  - normal page requests are redirected to `AccessRestricted.aspx`;
  - PageMethod calls, `?ajax=`, `?action=` and `.ashx` requests get HTTP 403 with `{"success":false,"blocked":true}`;
  - nothing else is served.
- **Allowed through:** `Default.aspx`, login and logout pages and the logout postback, `ErrorPage.aspx`, `AccessRestricted.aspx`, `MyDisciplinaryCases.aspx` (its appeal action), the portal `DcFile.ashx`, and static and framework files (css, js, images, fonts, `WebResource.axd`, `ScriptResource.axd`, DevExpress resources).
- **Registration in the other gates:** the restricted page is added to the exemptions in `Global.asax` (both lists), `PortalMaster` gates and `PortalHelper.ExemptPaths`, so no gate redirects away from it.
- **Lifting:** automatic at the end date (D4), or by a BLOCK_LIFTED entry (`restrict`).

### 6.2 Results withheld

`DisciplinaryGate.ResultsWithheld(regno)` applies at:

| Point | Behaviour |
|---|---|
| `StudentResults.aspx`, legacy `Results.ascx` | Results replaced by "Your results are withheld under case DC/... Contact ..." |
| `PrintResults.aspx` | Refused |
| Dashboard Latest Results and CGPA | Hidden |
| `MyCourses.aspx` | Marks hidden; stage and status still shown |
| `CourseworkResults.aspx` | Hidden |
| Exam clearance card | Link hidden in the portal; refused in eadmin `doc_verification` (the restriction is part of the cache fingerprint) |
| `MyGraduation.aspx`, `GraduationList.aspx` | The student's own entry hidden |
| `COOPERP/Mobile/Default.aspx` | Requires a session and serves only the session's own regno (security fix, D11) |
| eadmin transcript and certificate printing | `GraduationPrintGate.Decide` refuses with the case number |

Mark Status (`MarkStatusCheck.aspx`) stays available, as the brief asks.

### 6.3 Suspension (and expulsion)

| Point | Behaviour |
|---|---|
| Database | Registration triggers (D5): no semester or course registration by any path |
| Portal wizard, course registration, retakes | A friendly message before the attempt |
| Exam clearance | Refused in `FeesRegistration` and `StudentsRegistration` `ClearStudent`, and in `doc_verification` |
| Graduation | C9 block |
| Staff side | Banner on `StudentProfile` and the NewStudentInfo profile: "Suspended until 30 Jun 2027 under DC/2026-27/0042" |

Paying fees is not blocked, as the brief says.

### 6.4 Graduation bar and C9

- **`GraduationEngine.Assess`:** adds C9 "Disciplinary sanction":
  - BLOCK when graduation bar, expulsion or suspension is in force, or a cancellation follow-up is still pending;
  - WARN when a case is open without a bar;
  - PASS otherwise.
- **Data:** loaded set-based in `Enrich` (one query per page) and in `GraduationService.Load`.
- **Overview:** counted in `Overview`.
- **`GraduationService.Clear`:** refuses a C9 BLOCK even with an override note (D16). Bulk approval already skips blocked candidates.

### 6.5 Cancelled results, fines and restitution

The sanction is created with follow-up = PENDING. It appears:
- in the case file;
- on the dashboard's "follow-ups owed" list;
- in the marks office's view of Records (filter "marks action owed") and the Bursar's ("fees action owed").

The officer makes the change in the marks or fees screens, quoting the case number as the reason. Then they record MARKS_ACTION or FEES_ACTION "done" with the reference, which sets follow-up = DONE. The module never writes marks or fees (rule 8).

### 6.6 Automatic lifting

There are two layers:
- **Enforcement** reads the dates directly, so a restriction stops at midnight after its end date.
- **The nightly event** (00:10) writes the SYSTEM entry, notice and audit row (tested).

If the event scheduler is ever off, enforcement is still right and a "sanctions past their end date but not recorded" count appears on the dashboard. Opening the dashboard runs the same procedure, so the case file catches up.

### 6.7 Clearance question

**eadmin** `DcClearance.Check(conn, regno)` returns:

```
{ openCases, effects[], blocksGraduation, blocksExamCard, blocksDocuments, blocksRegistration, cases:[{caseNo, status, sanctions[]}] }
```

It is used by C9, the print gate, exam clearance, `doc_verification` and the staff banner.

**eportal** `DisciplinaryGate` exposes the same answers from the same database functions, and the registration triggers use them too. That is one rule in one place (D3).

---

## 7. Notifications

| Event | Portal notice | Email | Case file |
|---|---|---|---|
| Case opened | yes (neutral wording, no allegation detail when restricted) | yes | STUDENT_NOTIFIED |
| Summoned, adjourned | yes, with the letter | yes, with the PDF attached | STUDENT_NOTIFIED |
| Decision | yes, with the letter and appeal deadline | yes | STUDENT_NOTIFIED; this date starts the appeal window (D7) |
| Appeal received | yes | yes | STUDENT_NOTIFIED |
| Appeal outcome | yes, with the letter | yes | STUDENT_NOTIFIED |
| Sanction lifted (by officer or end date) | yes | yes | STUDENT_NOTIFIED |
| Portal block applied or lifted | yes (shown on the restricted page) | yes | STUDENT_NOTIFIED |
| Case withdrawn | yes | yes | STUDENT_NOTIFIED |

**Channels:**
- The portal notice is written in the same transaction as the event.
- Email is sent after commit to the student's University address (`acad_student.email` or the SEMS address), falling back to their personal address. The result goes into `dc_notification` (SENT, FAILED with the error, NO_ADDRESS). Failed emails are retried by a sweep (on dashboard load and hourly) up to 3 times.
- The dashboard shows notices never read and emails failed, so an officer can deliver by hand and record it.

**Staff notifications:** the case officer and the committee get a portal notice on summons and appeal.

---

## 8. Permissions

### 8.1 Slugs (Appendix D)

| Slug | Grants |
|---|---|
| `discipline` | Sidebar parent |
| `discipline.dashboard` | Dashboard |
| `discipline.records` | Records list and case files, within the user's scope (8.2) |
| `discipline.updates` | Record Updates feed |
| `discipline.settings` | View settings |
| `discipline.reports` | Reports |
| `discipline.report` | Open cases; add notes, statements and evidence to cases one reported; recommend interim measures |
| `discipline.manage` | Manage cases in own faculty (dean) or department (HOD) |
| `discipline.manage_exam` | Manage exam-related cases in all faculties |
| `discipline.manage_all` | Manage all cases |
| `discipline.view_all` | See all cases, read only |
| `discipline.hearing` | Schedule hearings, summon, adjourn, issue letters |
| `discipline.decide` | Record hearings and decisions |
| `discipline.appeal` | Decide appeals; accept late appeals |
| `discipline.restrict` | Apply and lift portal blocks and interim measures, vary or lift sanctions, withdraw cases |
| `discipline.restricted` | See restricted cases |
| `discipline.settings_manage` | Change settings |

### 8.2 Visibility rule (applied in every query, count, search and export)

1. A restricted case is visible **only** with `discipline.restricted`, to admins, or to the current committee chair (from `dc_committee_member`). Without these it is absent from counts, search and exports. A direct link gives "not found", not "forbidden", so its existence is not revealed.
2. Otherwise:
   - `manage_all` or `view_all`: all cases;
   - `manage_exam`: exam-related types;
   - `manage`: the user's faculty or department via `MarksScopeResolver`;
   - `report` only: cases the user reported.

Every view of a restricted case is logged (D15).

### 8.3 Proposed grants

| Role | Grants |
|---|---|
| Dean of Students (new) | everything except decide and appeal |
| Academic Registrar | everything except decide and appeal |
| Disciplinary Committee (new) | dashboard, records, updates, reports, manage_all, **decide** |
| Dean | dashboard, records, updates, reports, report, manage (faculty) |
| Head of Department | records, updates, report, manage (department) |
| Examination Officer | dashboard, records, updates, reports, report, manage_exam |
| Faculty Staff (lecturers, invigilators) | records (own reported cases), report |
| Student Services | records, report |
| Vice-Chancellor | dashboard, records, reports, view_all, **appeal** (appellate authority, Q3) |
| Auditor | dashboard, records, updates, reports, view_all (read only) |
| System administrator | everything (wildcard) |

Rule 3 is enforced in code: blocks, interim measures, suspensions, expulsions and results sanctions each need a reason, a DECISION or INTERIM_MEASURE entry in the same transaction, and the specific permission (`restrict` for interim and lifting, `decide` for decisions). The server refuses otherwise.

---

## 9. Reports

Each report is available as PDF (crest, certification block, groups and totals), Excel (cover sheet) and CSV, through the export dialog. Standard filters apply (campus, faculty, department, programme, type, severity, status, year, period). Restricted cases are included only for users who may see them, and the cover says so.

| # | Report | Columns |
|---|---|---|
| R1 | Disciplinary Register (period or year) | case no, date reported, student, regno, programme, campus, type, severity, status, decision date, sanctions, officer |
| R2 | Cases by Type and Severity | type, severity, opened, decided, dismissed or acquitted, open, average days to decision |
| R3 | Cases by Faculty, Department and Programme | grouped counts by status |
| R4 | Students Currently Under Sanction | student, regno, programme, case no, sanction, from, to, effects |
| R5 | Hearing Schedule (date range) | date, time, venue, case no, student, regno, type, panel |
| R6 | Summon List (one hearing date) | in the Registrar's memo layout: grouped by campus, student name and regno, programme, case no, time and venue |
| R7 | Appeals Register | case no, student, lodged, within window, grounds (summary), outcome, decided by and when |
| R8 | Case Statement (one case) | header block, then every entry in order. Two versions: *Committee* (all entries) and *Student copy* (student-visible entries only) |
| R9 | Senate Summary (academic year) | counts and outcomes by type, faculty and severity, appeals and their results, plus generated prose (rule-based, as Graduation Analysis) |
| R10 | Follow-ups owed | marks and fees actions pending, by sanction |
| R11 | Notifications not delivered | portal unread past 7 days, emails failed |

---

## 10. Implementation checklist

**Database**
- [ ] 1. Back up `acad_registration` triggers (definition), `sys_menu_items`, `sys_role_permissions`, `sys_roles`; apply the schema, registration guard, menu and (after approval) seed scripts.

**eadmin core** (`App_Code/Discipline`, staged and compiled offline, deployed in batches)
- [ ] 2. DcCore: database helpers, access and visibility rule, audit, numbering, settings, PageMethod wrapper.
- [ ] 3. DcCases: incident and case creation, list query with visibility and filters.
- [ ] 4. DcEntries and status machine: every entry type, transitions, interim measures, decisions with sanctions, appeals, variation and lifting, follow-ups; one transaction each.
- [ ] 5. DcClearance (eadmin) and the expiry sweep caller.
- [ ] 6. DcLetters: merge, preview, PDF on letterhead, storage.
- [ ] 7. DcNotify: notices, email after commit, retry sweep.
- [ ] 8. DcReports: R1 to R11 with exports (reusing the FA export and PDF classes).
- [ ] 9. Handlers: DcFile.ashx, DcUpload.ashx (eadmin).

**eadmin screens**
- [ ] 10. Shared header, styles, sidebar heading and page titles.
- [ ] 11. Settings screen.
- [ ] 12. Records (list, new-case modal, batch).
- [ ] 13. Case file.
- [ ] 14. Record Updates.
- [ ] 15. Dashboard.
- [ ] 16. Reports.

**Hooks in existing eadmin code**
- [ ] 17. Graduation C9 (`Enrich`, `Load`, `Assess`, `Overview`, `Clear` guard).
- [ ] 18. `GraduationPrintGate`, `doc_verification` exam card, the two `ClearStudent` methods.
- [ ] 19. Student banners (`StudentProfile`, NewStudentInfo profile); marks audit "Disciplinary" source label; ticket screen "Open disciplinary case".

**eportal**
- [ ] 20. DisciplinaryGate (portal), the Global.asax check, exemptions in the three gate lists, `AccessRestricted.aspx`.
- [ ] 21. `MyDisciplinaryCases.aspx` with appeal, portal `DcFile.ashx`, student alert loader, dashboard link.
- [ ] 22. Results-withheld points (6.2) and the mobile endpoint fix.
- [ ] 23. Suspension messages in registration (6.3).

**Finish**
- [ ] 24. Run the test plan (section 11), fix, re-test.
- [ ] 25. User guide for staff and a one-page student explainer; memory note; commit and push.

---

## 11. Test plan

### 11.1 SQL (done in Phase 1, scratch schemas, then dropped)

| Test | Result |
|---|---|
| All four scripts run twice | No errors; second run changes nothing (13 types, 14 sanctions, 93 defaults, 6 templates, 17 menu rows, grants for 10 roles, event enabled) |
| `dc_effect_active` with today, yesterday's end, a future start, another student | 1, 0, 0, 0 |
| Registration of a suspended student | Refused with the disciplinary message; another student registers; the existing automatic-registration rule still fires |
| Course registration of a suspended student | Refused (message shortened after the first test hit MySQL's 128-character limit) |
| Edit sanction terms; reactivate an ended sanction; delete a case; edit an entry | All refused |
| `dc_expire_sanctions` | Ends only the expired block, writes the visible SYSTEM entry, notice and audit; a second run writes nothing |

### 11.2 Service tests (through the PageMethods, as in Fixed Assets)

1. Create an incident with three students: three cases, three numbers, one incident; interim block applied only with `restrict`, otherwise recorded as a recommendation.
2. Every transition in section 4, allowed and refused (wrong status, missing fields, wrong permission, SERIOUS case decided without a hearing).
3. Decision with suspension, results withheld and a fine:
   - sanctions created with the right effects and dates;
   - the fine shows as a follow-up owed;
   - all in one transaction (a forced failure leaves nothing).
4. Appeal within the window from the portal; late appeal refused for the student, accepted by `appeal` with a reason; appeal varied: old sanctions VARIED, new ones ACTIVE.
5. Lift and vary: needs `restrict` and a reason; the case file shows the entry; the student is notified.
6. The case file is append-only from the application as well: a correction entry links the original.
7. Letters: preview equals the issued PDF text; letter numbers are unique; an issued letter cannot be changed.
8. Notifications: portal notice in the same transaction; email outcome recorded; retry stops at 3.

### 11.3 A blocked student cannot reach any portal data

With a test student blocked:
- **Crawl:** request **every** `.aspx` page in the portal (the file list is generated from disk), every PageMethod name found in the code-behind, every `?action=` and `?ajax=` endpoint, and every `.ashx`. Expect a redirect to the restricted page or 403 JSON, never data.
- **Allowed set:** the allowed pages work, the appeal can be lodged, and the student's own letters download. Another student's letter refuses.
- **Master key:** sign in with the master key as the blocked student: the same results.
- **Session restore:** restart the portal application with a live cookie: the first request is still blocked.
- **Timing:** apply the block from eadmin: blocked within 60 seconds. End date reached: unblocked the next day with the SYSTEM entry recorded.

### 11.4 Results withheld and suspension

- Every point in 6.2 shows the withheld message.
- The mobile endpoint refuses another student's regno and anonymous calls.
- The exam card is refused.
- Registration is refused on every path: the portal wizard, eadmin semester and course screens, API v2.
- C9 blocks and cannot be overridden; the transcript print is refused.

### 11.5 A restricted case is invisible to unauthorised staff

As a dean of the student's faculty (no `restricted`), and as the reporting officer:
- The case is absent from the list, search (by name, regno, case number), dashboard counts, charts and lists, Record Updates, every report and export, and the Senate Summary.
- A direct link to the case gives "not found".
- Its files refuse.

As the Dean of Students, the case is visible and the access log records each view and export.

### 11.6 Screens and exports

- Every screen at 1366 px and 390 px with no JavaScript errors.
- Every report in PDF, Excel and CSV, checked visually: letterhead, totals, no em dashes, wrapping text not dropped.

### 11.7 Test data

A dedicated test student (and portal login) is created for the tests and removed afterwards, together with all test cases, in one documented clean-up before go-live, as with Fixed Assets (Q11).

---

## 12. Open questions for MIS, the Academic Registrar and the Dean of Students

| # | Question |
|---|---|
| Q1 | Are the case types, sanctions, default pairings and severities in section 3 right? Are there sanctions in the Student Regulations not listed (for example exclusion from halls of residence)? |
| Q2 | Who holds the new roles? Name the Dean of Students and the Committee members for 2026/2027. |
| Q3 | Who is the appellate authority? The plan assumes the Vice-Chancellor or a University Appeals Committee, with the `appeal` permission. |
| Q4 | May the Registrar's office record decisions on the Committee's behalf (as its secretary), or only Committee members? |
| Q5 | May a MINOR case be decided without a hearing (for example a verbal warning by the Dean of Students), as the plan allows? |
| Q6 | On expulsion, should the Registrar set `acad_student.new_status = EXPELLED`? The plan leaves that to the Registrar (D10). |
| Q7 | Fines and restitution: billed by the Bursar by hand (as planned), or should a later phase raise the bill automatically through fees? |
| Q8 | Wardens and security staff have no eadmin login today. Should they get accounts with the reporting role, or report through the Dean of Students? |
| Q9 | Minimum notice for a summons: 3 days (as planned)? |
| Q10 | Should suspended students keep any portal access (notices, fees)? The brief and this plan block only registration and examinations; only a portal block hides the portal. |
| Q11 | Testing: approve creating a test student and removing all test data once before go-live. |
| Q12 | The Summon List layout: please share a copy of the existing memo so R6 matches it exactly. |
| Q13 | Retention: how long are closed case files kept, and are any sealed after a period (for example minor cases after graduation)? |
| Q14 | The portal security fixes in D11 (the unauthenticated mobile results endpoint in particular) change existing behaviour. Is the mobile app still in use and does it call that endpoint? |

---

## 13. Phase 2: what was built (6 October 2026)

### 13.1 Defaults taken for the open questions

| # | Default applied | Change it by |
|---|---|---|
| Q1 | Section 3 lists seeded as proposed | Case Types and Sanctions screen |
| Q2 | Roles `dean_students` and `dc_committee` exist with no members | User Roles; Committee tab |
| Q3 | Appellate authority: the Vice-Chancellor role holds `discipline.appeal`; letters name the "University Appeals Committee" | setting `appellate_authority`; role grants |
| Q4 | Only holders of `discipline.decide` record decisions (Committee; administrators) | role grants |
| Q5 | Minor cases may be decided without a hearing | n/a (rule in code) |
| Q6 | The module does not change `acad_student.new_status` on expulsion | Registrar, in NewStudentInfo |
| Q7 | Fines and restitution are billed by hand and recorded as follow-ups | n/a |
| Q8 | Wardens and security report through the Dean of Students | grant `discipline.report` to a role |
| Q9 | Minimum summons notice 3 days | setting `summon_notice_days` |
| Q10 | Suspension blocks registration and examinations only; the portal stays open unless a portal block is applied | n/a |
| Q11 | Two test students were used and removed with all test data (`2026-10_disciplinary_purge_testdata.sql`) | n/a |
| Q12 | Summon list in a standard grouped layout | report definition |
| Q13 | Closed cases kept indefinitely | not built |
| Q14 | `COOPERP/Mobile/Default.aspx` (portal) now needs a signed-in session: no request reached it in 99 days of web logs | n/a |

### 13.2 Changes against the plan

- **Pairings are switched off, not deleted:** `dc_case_type_sanction` gained `is_active`.
- **Two more settings:** `summon_notice_days` and `attachment_max_mb`.
- **Two more slugs:** `discipline.case` and `discipline.files`, so page-gated roles (Auditor) can open the case file and letters.
- **Email does not attach letters**, because they are confidential. The email points to My Disciplinary Cases.
- **The portal gate runs twice:** at `AcquireRequestState` (PageMethods are answered before `PreRequestHandlerExecute`) and at `PreRequestHandlerExecute`.
- **One plan item dropped:** the marks audit "Disciplinary" label was not needed, since the module never writes marks.
- **Document requests also checked:** `API/doc_verification.aspx` checks results withheld for result statements, transcripts and certificates as well as the exam card. **That endpoint still answers without sign-in for any registration number; this is outside this module and should be reviewed.**

### 13.3 Files

- **eadmin library:** `App_Code/Discipline/Dc*.cs` (Core, Cases, Workflow, Letters, Notify, Files, Clearance, Admin, Dashboard, Reports, Export, Pdf).
- **eadmin screens:** `NewScreens/Disciplinary{Dashboard,Records,Case,Updates,Settings,Reports}.aspx`, `DcHeader.ascx`, `DcStudentBanner.ascx`, `DcFile.ashx`, `DcUpload.ashx`, `css/dc.css`, `js/dc*.js`.
- **eadmin hooks:**
  - `GraduationEngine` (C9) and `GraduationService.Clear` (C9 cannot be overridden);
  - `GraduationPrintGate`;
  - `API/doc_verification.aspx.cs`;
  - `FeesRegistration` and `StudentsRegistration` exam clearance;
  - `StudentProfile` and `NewStudentInfo` banner;
  - `TicketsController` link;
  - the sidebar and page titles.
- **Portal library:** `App_Code/Portal/DisciplinaryPortal.cs` (gate, results withheld, the student's cases, appeal).
- **Portal pages:** `AccessRestricted.aspx`, `MyDisciplinaryCases.aspx`, `DcPortal.ashx`.
- **Portal hooks:**
  - `Global.asax`;
  - `PortalHelper` exemptions;
  - `PortalMaster` (notice and footer link);
  - `PortalAlerts` icon;
  - results: `StudentResults`, `PrintResults`, `Results.ascx`, `Results_complaint.ascx`, `CourseWorkResults.ascx`, the dashboard control (results, CGPA, exam card), `MyCourses`, `MyGraduation`, `COOPERP/Mobile/Default.aspx`;
  - registration: the wizard service, course registration and retakes.

### 13.4 Test results (production, test students, then purged)

| Area | Result |
|---|---|
| Lifecycle | Report with two students, investigate, schedule and summon, adjourn (re-summons), record the hearing, decide with suspension, cancellation and fine (interim measure replaced), follow-up, vary, appeal, appeal varied, close, withdraw: all pass. The six letters render on letterhead. |
| Refusals | Deciding a serious case without a hearing, a stale version, short notice without a reason, recording a hearing before it happened, suspension without an end date, dismissal combined with a fine, a clearance letter while something is pending, a reporter deciding: all refused with a plain message. |
| Database guards | Entry edits and letter deletion refused. Suspended student refused by both registration triggers; another student allowed; the automatic-registration rule still fires. |
| Restricted case | As a manage-all officer without `discipline.restricted`: absent from list, search, filters, dashboard, feed, register and Senate summary; a direct link says "not found"; actions refused. Admin views are logged. |
| Portal block | 179 pages, 72 PageMethods, `?ajax=`/`?action=` calls and handlers all refused for the blocked student (the only exceptions were 4 pages that already fail to compile). The same holds with the session lost and the cookie kept, and with the master key. The restricted page shows the case. The student's own letter downloads; another student's gives 404. The appeal from the portal is recorded with the EPORTAL interface and IP; a second appeal is refused, as is an appeal on another student's case. |
| Results withheld | Notice shown on StudentResults and the dashboard (results and CGPA hidden). PrintResults answers 403. The exam card picker explains the withholding; the exam card PDF answers 403. My Graduation redirects. The mobile endpoint refuses. The notice clears after reading. |
| Graduation and documents | C9 blocks the suspended student and the pending cancellation; the print gate refuses withheld documents. |
| Automatic ending | A past-dated block was not in force before any job ran. The procedure (run from the dashboard) recorded the SYSTEM entry and notice and set the sanction to EXPIRED. |
| Exports | Register (PDF, Excel, CSV), Senate summary, sanctions, appeals, follow-ups, student statement and list export all produced; group order is kept. |
| Errors | No errors were logged by either application. |

## Appendix A: schema SQL (tested, applied)

```sql
-- ---------------------------------------------------------------------------
-- Student Disciplinary module: schema (database campus_dynamics)
-- File: COOPERP/sql/discipline/2026-10_disciplinary_schema.sql
-- New tables, guard triggers, the restriction functions and the nightly expiry
-- event. Touches no existing table. Safe to re-run. Run with the mysql client.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS dc_settings (
  setting_key    VARCHAR(60)  NOT NULL,
  setting_value  VARCHAR(1000) NOT NULL,
  description    VARCHAR(300) NULL,
  updated_by     VARCHAR(100) NULL,
  updated_at     DATETIME     NULL,
  PRIMARY KEY (setting_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

INSERT IGNORE INTO dc_settings (setting_key, setting_value, description, updated_by, updated_at) VALUES
 ('appeal_window_days',      '14', 'Days a student has to appeal, counted from the day the decision is notified to them', 'install', NOW()),
 ('overdue_no_update_days',  '14', 'An open case with no entry for this many days is overdue', 'install', NOW()),
 ('appeal_closing_days',     '3',  'Warn when an appeal window closes within this many days', 'install', NOW()),
 ('contact_office',          'Office of the Dean of Students', 'Shown to a student whose portal access is restricted', 'install', NOW()),
 ('contact_details',         'deanofstudents@mru.ac.ug', 'Contact line shown with the office', 'install', NOW()),
 ('appellate_authority',     'University Appeals Committee', 'Name of the body that decides appeals, printed on letters', 'install', NOW()),
 ('letter_office',           'Office of the Academic Registrar', 'Office line on disciplinary letters', 'install', NOW()),
 ('summon_notice_days',      '3',  'Minimum days between a summons and the hearing, unless a reason for short notice is recorded', 'install', NOW()),
 ('attachment_max_mb',       '15', 'Largest file that can be attached to a case, in MB', 'install', NOW());

CREATE TABLE IF NOT EXISTS dc_case_type (
  id              INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  code            VARCHAR(20)       NOT NULL,
  name            VARCHAR(150)      NOT NULL,
  description     VARCHAR(600)      NULL,
  severity        ENUM('MINOR','SERIOUS','GROSS') NOT NULL DEFAULT 'SERIOUS',
  is_exam_related TINYINT(1)        NOT NULL DEFAULT 0,
  default_restricted TINYINT(1)     NOT NULL DEFAULT 0,
  sort_order      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active       TINYINT(1)        NOT NULL DEFAULT 1,
  created_by      VARCHAR(100)      NOT NULL,
  created_at      DATETIME          NOT NULL,
  updated_by      VARCHAR(100)      NULL,
  updated_at      DATETIME          NULL,
  row_version     INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_case_type_code (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_sanction_type (
  id              INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  code            VARCHAR(20)       NOT NULL,
  name            VARCHAR(150)      NOT NULL,
  description     VARCHAR(600)      NULL,
  outcome         ENUM('SANCTION','DISMISSAL','ACQUITTAL') NOT NULL DEFAULT 'SANCTION',
  effects         SET('PORTAL_BLOCK','RESULTS_WITHHELD','SUSPENSION','EXPULSION','GRADUATION_BAR',
                      'CANCEL_PAPER','CANCEL_SEMESTER','FINE','RESTITUTION') NOT NULL DEFAULT '',
  needs_amount    TINYINT(1)        NOT NULL DEFAULT 0,
  needs_dates     ENUM('NONE','FROM','FROM_TO') NOT NULL DEFAULT 'NONE',
  needs_course    TINYINT(1)        NOT NULL DEFAULT 0,
  needs_semester  TINYINT(1)        NOT NULL DEFAULT 0,
  allowed_interim TINYINT(1)        NOT NULL DEFAULT 0,
  sort_order      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active       TINYINT(1)        NOT NULL DEFAULT 1,
  created_by      VARCHAR(100)      NOT NULL,
  created_at      DATETIME          NOT NULL,
  updated_by      VARCHAR(100)      NULL,
  updated_at      DATETIME          NULL,
  row_version     INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_sanction_type_code (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_case_type_sanction (
  case_type_id     INT UNSIGNED      NOT NULL,
  sanction_type_id INT UNSIGNED      NOT NULL,
  sort_order       SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active        TINYINT(1)        NOT NULL DEFAULT 1,      -- pairings are switched off, never deleted
  PRIMARY KEY (case_type_id, sanction_type_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_committee_member (
  id           INT UNSIGNED NOT NULL AUTO_INCREMENT,
  acad_year    VARCHAR(9)   NOT NULL,
  emp_id       INT UNSIGNED NULL,
  username     VARCHAR(100) NULL,
  member_name  VARCHAR(150) NOT NULL,
  panel_role   ENUM('CHAIR','MEMBER','SECRETARY','STUDENT_REP','APPELLATE') NOT NULL DEFAULT 'MEMBER',
  sort_order   SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active    TINYINT(1)   NOT NULL DEFAULT 1,
  created_by   VARCHAR(100) NOT NULL,
  created_at   DATETIME     NOT NULL,
  updated_by   VARCHAR(100) NULL,
  updated_at   DATETIME     NULL,
  PRIMARY KEY (id),
  KEY ix_dc_member_year (acad_year, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_letter_template (
  id           INT UNSIGNED NOT NULL AUTO_INCREMENT,
  code         VARCHAR(20)  NOT NULL,
  name         VARCHAR(150) NOT NULL,
  subject      VARCHAR(250) NOT NULL,
  body         TEXT         NOT NULL,          -- plain paragraphs with {{merge_fields}}
  student_copy TINYINT(1)   NOT NULL DEFAULT 1,
  sort_order   SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active    TINYINT(1)   NOT NULL DEFAULT 1,
  created_by   VARCHAR(100) NOT NULL,
  created_at   DATETIME     NOT NULL,
  updated_by   VARCHAR(100) NULL,
  updated_at   DATETIME     NULL,
  row_version  INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_letter_template_code (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_sequence (
  seq_key   VARCHAR(40)  NOT NULL,           -- CASE:2026-27, INCIDENT:2026-27
  next_no   INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (seq_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_incident (
  id                  INT UNSIGNED NOT NULL AUTO_INCREMENT,
  incident_no         VARCHAR(30)  NOT NULL,                  -- DI/2026-27/0007
  case_type_id        INT UNSIGNED NOT NULL,
  occurred_at         DATETIME     NOT NULL,
  place               VARCHAR(200) NULL,
  campus_id           INT          NULL,
  description         TEXT         NOT NULL,
  reported_by         VARCHAR(100) NOT NULL,                  -- username of the reporting officer
  reported_by_emp_id  INT UNSIGNED NULL,
  reporter_name       VARCHAR(150) NULL,
  witnesses           TEXT         NULL,
  complaint_ticket_id INT          NULL,                      -- campus_dynamics_portal.support_tickets
  exam_timetable_id   INT UNSIGNED NULL,                      -- acad_exam_timetable.ID
  course_code         VARCHAR(25)  NULL,
  exam_acad_year      VARCHAR(9)   NULL,
  exam_semester       TINYINT UNSIGNED NULL,
  invigilator_emp_id  INT UNSIGNED NULL,
  created_by          VARCHAR(100) NOT NULL,
  created_at          DATETIME     NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_incident_no (incident_no),
  KEY ix_dc_incident_ticket (complaint_ticket_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_case (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_no          VARCHAR(30)  NOT NULL,                     -- DC/2026-27/0042
  incident_id      INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  student_name     VARCHAR(200) NOT NULL,                     -- as at opening
  progcode         VARCHAR(25)  NULL,
  faculty_code     VARCHAR(10)  NULL,
  department_id    INT          NULL,
  campus_id        INT          NULL,
  study_year       TINYINT UNSIGNED NULL,
  acad_year        VARCHAR(9)   NOT NULL,                     -- 2026/2027
  case_type_id     INT UNSIGNED NOT NULL,
  severity         ENUM('MINOR','SERIOUS','GROSS') NOT NULL,
  status           ENUM('REPORTED','UNDER_INVESTIGATION','HEARING_SCHEDULED','SUMMONED','HEARD','DECIDED',
                        'UNDER_APPEAL','APPEAL_DECIDED','CLOSED','WITHDRAWN') NOT NULL DEFAULT 'REPORTED',
  is_restricted    TINYINT(1)   NOT NULL DEFAULT 0,
  reported_by      VARCHAR(100) NOT NULL,
  case_officer     VARCHAR(100) NULL,
  hearing_at       DATETIME     NULL,                         -- next or last hearing
  hearing_venue    VARCHAR(200) NULL,
  decided_at       DATE         NULL,
  decision_summary VARCHAR(1000) NULL,
  notified_at      DATETIME     NULL,                         -- decision notified to the student
  appeal_deadline  DATE         NULL,
  closed_at        DATETIME     NULL,
  last_entry_at    DATETIME     NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  updated_by       VARCHAR(100) NULL,
  updated_at       DATETIME     NULL,
  row_version      INT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_case_no (case_no),
  KEY ix_dc_case_regno (regno, status),
  KEY ix_dc_case_status (status, last_entry_at),
  KEY ix_dc_case_year (acad_year, status),
  KEY ix_dc_case_incident (incident_id),
  KEY ix_dc_case_type (case_type_id),
  KEY ix_dc_case_faculty (faculty_code, department_id),
  KEY ix_dc_case_hearing (hearing_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- The case file: append-only.
CREATE TABLE IF NOT EXISTS dc_entry (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  entry_type       ENUM('CASE_OPENED','NOTE','FINDING','STATEMENT','EVIDENCE','INTERIM_MEASURE','HEARING_SCHEDULED',
                        'SUMMON','ADJOURNED','HEARING_HELD','DECISION','APPEAL_LODGED','APPEAL_DECISION',
                        'SANCTION_VARIED','SANCTION_LIFTED','BLOCK_APPLIED','BLOCK_LIFTED','STATUS_CHANGE',
                        'STUDENT_NOTIFIED','LETTER_ISSUED','MARKS_ACTION','FEES_ACTION','CORRECTION',
                        'CASE_CLOSED','CASE_WITHDRAWN','SYSTEM') NOT NULL,
  entry_at         DATETIME     NOT NULL,                     -- when it happened
  title            VARCHAR(250) NOT NULL,
  body             TEXT         NULL,
  details_json     TEXT         NULL,
  student_visible  TINYINT(1)   NOT NULL DEFAULT 0,
  status_before    VARCHAR(30)  NULL,
  status_after     VARCHAR(30)  NULL,
  sanction_id      INT UNSIGNED NULL,
  corrects_entry_id INT UNSIGNED NULL,
  recorded_by      VARCHAR(100) NOT NULL,
  recorded_role    VARCHAR(40)  NULL,
  recorded_at      DATETIME     NOT NULL,
  interface        ENUM('EADMIN','EPORTAL','SYSTEM') NOT NULL,
  ip_address       VARCHAR(45)  NULL,
  client_op_id     VARCHAR(40)  NULL,
  PRIMARY KEY (id),
  KEY ix_dc_entry_case (case_id, entry_at, id),
  KEY ix_dc_entry_type (entry_type, recorded_at),
  KEY ix_dc_entry_recorded (recorded_at),
  KEY ix_dc_entry_sanction (sanction_id),
  UNIQUE KEY uq_dc_entry_op (client_op_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_attachment (
  id              INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id         INT UNSIGNED NOT NULL,
  entry_id        INT UNSIGNED NULL,
  kind            ENUM('STATEMENT','PHOTO','SCRIPT','SCREENSHOT','MINUTES','LETTER','APPEAL','EVIDENCE','OTHER') NOT NULL DEFAULT 'OTHER',
  description     VARCHAR(300) NULL,
  original_name   VARCHAR(200) NOT NULL,
  stored_name     VARCHAR(120) NOT NULL,
  mime            VARCHAR(100) NOT NULL,
  size_bytes      INT UNSIGNED NOT NULL,
  sha1            CHAR(40)     NOT NULL,
  student_visible TINYINT(1)   NOT NULL DEFAULT 0,
  uploaded_by     VARCHAR(100) NOT NULL,
  uploaded_via    ENUM('EADMIN','EPORTAL','SYSTEM') NOT NULL,
  uploaded_at     DATETIME     NOT NULL,
  is_active       TINYINT(1)   NOT NULL DEFAULT 1,
  removed_by      VARCHAR(100) NULL,
  removed_at      DATETIME     NULL,
  removed_reason  VARCHAR(500) NULL,
  PRIMARY KEY (id),
  KEY ix_dc_att_case (case_id, is_active),
  KEY ix_dc_att_entry (entry_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_hearing (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  scheduled_at     DATETIME     NOT NULL,
  venue            VARCHAR(200) NOT NULL,
  panel            VARCHAR(1000) NULL,
  status           ENUM('SCHEDULED','ADJOURNED','HELD','CANCELLED') NOT NULL DEFAULT 'SCHEDULED',
  student_attended TINYINT(1)   NULL,
  scheduled_entry_id INT UNSIGNED NULL,
  outcome_entry_id INT UNSIGNED NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  updated_by       VARCHAR(100) NULL,
  updated_at       DATETIME     NULL,
  PRIMARY KEY (id),
  KEY ix_dc_hearing_case (case_id),
  KEY ix_dc_hearing_when (scheduled_at, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Sanctions and interim measures. Terms are never edited: a variation ends the old row and adds a new one.
CREATE TABLE IF NOT EXISTS dc_sanction (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  sanction_type_id INT UNSIGNED NOT NULL,
  effects          SET('PORTAL_BLOCK','RESULTS_WITHHELD','SUSPENSION','EXPULSION','GRADUATION_BAR',
                       'CANCEL_PAPER','CANCEL_SEMESTER','FINE','RESTITUTION') NOT NULL DEFAULT '',
  source           ENUM('INTERIM','DECISION','APPEAL') NOT NULL,
  amount           DECIMAL(14,2) NULL,
  starts_on        DATE         NOT NULL,
  ends_on          DATE         NULL,
  course_code      VARCHAR(25)  NULL,
  acad_year        VARCHAR(9)   NULL,
  semester         TINYINT UNSIGNED NULL,
  terms            VARCHAR(1000) NULL,
  status           ENUM('ACTIVE','LIFTED','EXPIRED','VARIED','SET_ASIDE') NOT NULL DEFAULT 'ACTIVE',
  follow_up        ENUM('NONE','PENDING','DONE') NOT NULL DEFAULT 'NONE',   -- marks or fees action owed
  follow_up_ref    VARCHAR(150) NULL,
  applied_entry_id INT UNSIGNED NULL,
  ended_entry_id   INT UNSIGNED NULL,
  ended_at         DATETIME     NULL,
  ended_by         VARCHAR(100) NULL,
  ended_reason     VARCHAR(1000) NULL,
  replaces_id      INT UNSIGNED NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_sanction_regno (regno, status, starts_on, ends_on),
  KEY ix_dc_sanction_case (case_id),
  KEY ix_dc_sanction_due (status, ends_on),
  KEY ix_dc_sanction_follow (follow_up)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_appeal (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  lodged_at        DATETIME     NOT NULL,
  lodged_via       ENUM('EADMIN','EPORTAL') NOT NULL,
  lodged_by        VARCHAR(100) NOT NULL,
  grounds          TEXT         NOT NULL,
  within_window    TINYINT(1)   NOT NULL,
  late_reason      VARCHAR(1000) NULL,
  status           ENUM('LODGED','UPHELD','DISMISSED','VARIED','WITHDRAWN') NOT NULL DEFAULT 'LODGED',
  decided_at       DATE         NULL,
  decided_by       VARCHAR(100) NULL,
  decision_text    TEXT         NULL,
  lodged_entry_id  INT UNSIGNED NULL,
  decision_entry_id INT UNSIGNED NULL,
  PRIMARY KEY (id),
  KEY ix_dc_appeal_case (case_id),
  KEY ix_dc_appeal_status (status, lodged_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_letter (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  letter_no        VARCHAR(40)  NOT NULL,                    -- DC/2026-27/0042/L2
  template_code    VARCHAR(20)  NOT NULL,
  subject          VARCHAR(250) NOT NULL,
  body             TEXT         NOT NULL,                    -- the merged text exactly as issued
  stored_name      VARCHAR(120) NOT NULL,
  sha1             CHAR(40)     NOT NULL,
  student_visible  TINYINT(1)   NOT NULL DEFAULT 1,
  issued_by        VARCHAR(100) NOT NULL,
  issued_at        DATETIME     NOT NULL,
  entry_id         INT UNSIGNED NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_dc_letter_no (letter_no),
  KEY ix_dc_letter_case (case_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_notification (
  id               INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id          INT UNSIGNED NOT NULL,
  regno            VARCHAR(30)  NOT NULL,
  kind             ENUM('CASE_OPENED','SUMMON','DECISION','APPEAL_RECEIVED','APPEAL_OUTCOME','SANCTION_LIFTED',
                        'BLOCK_APPLIED','BLOCK_LIFTED','LETTER','OTHER') NOT NULL,
  title            VARCHAR(200) NOT NULL,
  message          VARCHAR(2000) NOT NULL,
  letter_id        INT UNSIGNED NULL,
  portal_read_at   DATETIME     NULL,
  email_to         VARCHAR(200) NULL,
  email_status     ENUM('PENDING','SENT','FAILED','NO_ADDRESS','NOT_SENT') NOT NULL DEFAULT 'PENDING',
  email_attempts   TINYINT UNSIGNED NOT NULL DEFAULT 0,
  email_error      VARCHAR(500) NULL,
  email_sent_at    DATETIME     NULL,
  entry_id         INT UNSIGNED NULL,
  created_by       VARCHAR(100) NOT NULL,
  created_at       DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_note_regno (regno, portal_read_at),
  KEY ix_dc_note_email (email_status, email_attempts),
  KEY ix_dc_note_case (case_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS dc_audit (
  id           INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  entity       ENUM('CASE','ENTRY','SANCTION','APPEAL','HEARING','LETTER','ATTACHMENT','NOTIFICATION','SETTING',
                    'CASE_TYPE','SANCTION_TYPE','TEMPLATE','MEMBER','INCIDENT') NOT NULL,
  entity_id    INT UNSIGNED  NOT NULL,
  case_id      INT UNSIGNED  NULL,
  action       VARCHAR(40)   NOT NULL,
  before_json  TEXT          NULL,
  after_json   TEXT          NULL,
  reason       VARCHAR(1000) NULL,
  actor        VARCHAR(100)  NOT NULL,
  actor_role   VARCHAR(40)   NULL,
  interface    ENUM('EADMIN','EPORTAL','SYSTEM') NOT NULL,
  ip_address   VARCHAR(45)   NULL,
  created_at   DATETIME      NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_audit_case (case_id, created_at),
  KEY ix_dc_audit_entity (entity, entity_id),
  KEY ix_dc_audit_actor (actor, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Who opened a restricted case file, and when (read access log for confidential cases).
CREATE TABLE IF NOT EXISTS dc_access_log (
  id          INT UNSIGNED NOT NULL AUTO_INCREMENT,
  case_id     INT UNSIGNED NOT NULL,
  viewer      VARCHAR(100) NOT NULL,
  viewer_role VARCHAR(40)  NULL,
  what        VARCHAR(40)  NOT NULL,                     -- VIEW, EXPORT, LETTER, ATTACHMENT
  ip_address  VARCHAR(45)  NULL,
  viewed_at   DATETIME     NOT NULL,
  PRIMARY KEY (id),
  KEY ix_dc_access_case (case_id, viewed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- ── Guard triggers ─────────────────────────────────────────────────────────
DROP TRIGGER IF EXISTS trg_dc_entry_bd;
DROP TRIGGER IF EXISTS trg_dc_entry_bu;
DROP TRIGGER IF EXISTS trg_dc_case_bd;
DROP TRIGGER IF EXISTS trg_dc_sanction_bd;
DROP TRIGGER IF EXISTS trg_dc_sanction_bu;
DROP TRIGGER IF EXISTS trg_dc_audit_bd;
DROP TRIGGER IF EXISTS trg_dc_audit_bu;
DROP TRIGGER IF EXISTS trg_dc_letter_bd;
DROP TRIGGER IF EXISTS trg_dc_letter_bu;
DROP TRIGGER IF EXISTS trg_dc_appeal_bd;
DROP TRIGGER IF EXISTS trg_dc_incident_bd;
DROP TRIGGER IF EXISTS trg_dc_attachment_bd;
DROP TRIGGER IF EXISTS trg_dc_notification_bd;
DROP TRIGGER IF EXISTS trg_dc_access_bd;

DELIMITER $$
CREATE TRIGGER trg_dc_entry_bd BEFORE DELETE ON dc_entry FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_entry is append-only: add a correction entry instead'; END$$
CREATE TRIGGER trg_dc_entry_bu BEFORE UPDATE ON dc_entry FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_entry is append-only: add a correction entry instead'; END$$
CREATE TRIGGER trg_dc_case_bd BEFORE DELETE ON dc_case FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cases are never deleted: withdraw or close them'; END$$
CREATE TRIGGER trg_dc_sanction_bd BEFORE DELETE ON dc_sanction FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Sanctions are never deleted: lift or vary them'; END$$
CREATE TRIGGER trg_dc_sanction_bu BEFORE UPDATE ON dc_sanction FOR EACH ROW
BEGIN
  -- The terms of a sanction never change; only its ending and follow-up may be recorded.
  IF NEW.case_id <> OLD.case_id OR NEW.regno <> OLD.regno OR NEW.sanction_type_id <> OLD.sanction_type_id
     OR NEW.effects <> OLD.effects OR NEW.source <> OLD.source OR NOT (NEW.amount <=> OLD.amount)
     OR NEW.starts_on <> OLD.starts_on OR NOT (NEW.ends_on <=> OLD.ends_on) OR NOT (NEW.course_code <=> OLD.course_code)
     OR NOT (NEW.acad_year <=> OLD.acad_year) OR NOT (NEW.semester <=> OLD.semester) OR NOT (NEW.terms <=> OLD.terms)
     OR NEW.created_by <> OLD.created_by OR NEW.created_at <> OLD.created_at THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Sanction terms cannot be edited: vary the sanction instead';
  END IF;
  IF OLD.status <> 'ACTIVE' AND NEW.status <> OLD.status THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'An ended sanction cannot be reactivated';
  END IF;
END$$
CREATE TRIGGER trg_dc_audit_bd BEFORE DELETE ON dc_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_audit is append-only'; END$$
CREATE TRIGGER trg_dc_audit_bu BEFORE UPDATE ON dc_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_audit is append-only'; END$$
CREATE TRIGGER trg_dc_letter_bd BEFORE DELETE ON dc_letter FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Issued letters are never deleted'; END$$
CREATE TRIGGER trg_dc_letter_bu BEFORE UPDATE ON dc_letter FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Issued letters are never edited: issue a new letter'; END$$
CREATE TRIGGER trg_dc_appeal_bd BEFORE DELETE ON dc_appeal FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Appeals are never deleted'; END$$
CREATE TRIGGER trg_dc_incident_bd BEFORE DELETE ON dc_incident FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Incidents are never deleted'; END$$
CREATE TRIGGER trg_dc_attachment_bd BEFORE DELETE ON dc_attachment FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Attachments are never deleted: remove hides them'; END$$
CREATE TRIGGER trg_dc_notification_bd BEFORE DELETE ON dc_notification FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Notifications are never deleted'; END$$
CREATE TRIGGER trg_dc_access_bd BEFORE DELETE ON dc_access_log FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'dc_access_log is append-only'; END$$
DELIMITER ;

-- ── The single source of truth for restrictions ────────────────────────────
-- Both eadmin and eportal, the registration triggers and the clearance gates call these.
-- A restriction is in force from starts_on to ends_on inclusive; no job is needed for it to end.
DROP FUNCTION IF EXISTS dc_effect_active;
DROP FUNCTION IF EXISTS dc_open_case_count;
DROP FUNCTION IF EXISTS dc_restriction_summary;
DELIMITER $$
CREATE FUNCTION dc_effect_active(p_regno VARCHAR(30), p_effect VARCHAR(20)) RETURNS TINYINT
READS SQL DATA
BEGIN
  DECLARE n INT DEFAULT 0;
  SELECT COUNT(*) INTO n FROM dc_sanction
   WHERE regno = p_regno AND status = 'ACTIVE' AND FIND_IN_SET(p_effect, effects) > 0
     AND starts_on <= CURDATE() AND (ends_on IS NULL OR ends_on >= CURDATE());
  RETURN IF(n > 0, 1, 0);
END$$
CREATE FUNCTION dc_open_case_count(p_regno VARCHAR(30)) RETURNS INT
READS SQL DATA
BEGIN
  DECLARE n INT DEFAULT 0;
  SELECT COUNT(*) INTO n FROM dc_case WHERE regno = p_regno AND status NOT IN ('CLOSED','WITHDRAWN');
  RETURN n;
END$$
-- Comma list of every effect in force, for example 'PORTAL_BLOCK,RESULTS_WITHHELD'; '' when none.
CREATE FUNCTION dc_restriction_summary(p_regno VARCHAR(30)) RETURNS VARCHAR(200)
READS SQL DATA
BEGIN
  DECLARE s VARCHAR(200) DEFAULT '';
  SELECT IFNULL(GROUP_CONCAT(DISTINCT effects ORDER BY effects SEPARATOR ','), '') INTO s FROM dc_sanction
   WHERE regno = p_regno AND status = 'ACTIVE' AND effects <> ''
     AND starts_on <= CURDATE() AND (ends_on IS NULL OR ends_on >= CURDATE());
  RETURN s;
END$$
DELIMITER ;

-- ── Nightly: sanctions past their end date are recorded as ended ───────────
DROP PROCEDURE IF EXISTS dc_expire_sanctions;
DELIMITER $$
CREATE PROCEDURE dc_expire_sanctions()
BEGIN
  DECLARE done INT DEFAULT 0;
  DECLARE v_id, v_case INT UNSIGNED;
  DECLARE v_regno VARCHAR(30);
  DECLARE v_effects VARCHAR(200);
  DECLARE v_name VARCHAR(150);
  DECLARE v_end DATE;
  DECLARE v_entry INT UNSIGNED;
  DECLARE cur CURSOR FOR
    SELECT s.id, s.case_id, s.regno, s.effects, t.name, s.ends_on
      FROM dc_sanction s JOIN dc_sanction_type t ON t.id = s.sanction_type_id
     WHERE s.status = 'ACTIVE' AND s.ends_on IS NOT NULL AND s.ends_on < CURDATE();
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = 1;
  OPEN cur;
  lp: LOOP
    FETCH cur INTO v_id, v_case, v_regno, v_effects, v_name, v_end;
    IF done = 1 THEN LEAVE lp; END IF;
    START TRANSACTION;
    INSERT INTO dc_entry (case_id, entry_type, entry_at, title, body, student_visible, sanction_id, recorded_by, recorded_role, recorded_at, interface)
    VALUES (v_case, IF(FIND_IN_SET('PORTAL_BLOCK', v_effects) > 0, 'BLOCK_LIFTED', 'SANCTION_LIFTED'),
            NOW(), CONCAT(v_name, ' ended'), CONCAT('The sanction ended on its end date, ', DATE_FORMAT(v_end, '%e %b %Y'), '. Recorded automatically.'),
            1, v_id, 'system', 'system', NOW(), 'SYSTEM');
    SET v_entry = LAST_INSERT_ID();
    UPDATE dc_sanction SET status = 'EXPIRED', ended_at = NOW(), ended_by = 'system',
           ended_reason = 'End date reached', ended_entry_id = v_entry WHERE id = v_id AND status = 'ACTIVE';
    UPDATE dc_case SET last_entry_at = NOW() WHERE id = v_case;
    INSERT INTO dc_notification (case_id, regno, kind, title, message, email_status, entry_id, created_by, created_at)
    VALUES (v_case, v_regno, IF(FIND_IN_SET('PORTAL_BLOCK', v_effects) > 0, 'BLOCK_LIFTED', 'SANCTION_LIFTED'),
            CONCAT(v_name, ' has ended'), CONCAT('The ', LOWER(v_name), ' recorded against you ended on ', DATE_FORMAT(v_end, '%e %b %Y'), '.'),
            'PENDING', v_entry, 'system', NOW());
    INSERT INTO dc_audit (entity, entity_id, case_id, action, before_json, after_json, reason, actor, actor_role, interface, created_at)
    VALUES ('SANCTION', v_id, v_case, 'EXPIRE', '{"status":"ACTIVE"}', '{"status":"EXPIRED"}', 'End date reached', 'system', 'system', 'SYSTEM', NOW());
    COMMIT;
  END LOOP;
  CLOSE cur;
END$$
DELIMITER ;

DROP EVENT IF EXISTS ev_dc_expire_sanctions;
CREATE EVENT ev_dc_expire_sanctions ON SCHEDULE EVERY 1 DAY STARTS (CURRENT_DATE + INTERVAL 1 DAY + INTERVAL 10 MINUTE)
  ON COMPLETION PRESERVE ENABLE DO CALL dc_expire_sanctions();
```

## Appendix B: registration guard SQL (tested, applied)

```sql
-- ---------------------------------------------------------------------------
-- Student Disciplinary module: registration guard
-- File: COOPERP/sql/discipline/2026-10_disciplinary_registration_guard.sql
-- A suspended or expelled student cannot be registered for a semester or a course by ANY path
-- (portal wizard, eadmin screens, API v2, stored procedures, direct inserts).
--
-- MySQL 5.6 allows one BEFORE INSERT trigger per table. acad_registration already has
-- trg_acadreg_block_autoreg (sql/guardrails/acad_registration_block_autoreg_trigger.sql), so this
-- script REPLACES it with the same two rules unchanged plus the disciplinary rule.
-- Backs up nothing (no rows change); the previous trigger body is kept below for rollback.
-- ---------------------------------------------------------------------------

DROP TRIGGER IF EXISTS campus_dynamics.trg_acadreg_block_autoreg;
DELIMITER $$
CREATE TRIGGER campus_dynamics.trg_acadreg_block_autoreg BEFORE INSERT ON campus_dynamics.acad_registration FOR EACH ROW
BEGIN
    DECLARE who VARCHAR(64);
    SET who = UPPER(TRIM(COALESCE(NEW.registeredBy,'')));

    IF who REGEXP '^(AUTO-|AUTO_|RECON-|RECON_|AUTORECON|SYSTEM_)' THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Automatic semester registration is disabled by policy. The student must register via the eportal.';
    END IF;

    IF who = '' OR who = '-' THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Semester registration requires attribution (student regno or staff username). Bulk/unattributed inserts are blocked.';
    END IF;

    -- Disciplinary module (2026-10): suspension or expulsion in force.
    IF campus_dynamics.dc_effect_active(NEW.regno, 'SUSPENSION') = 1 OR campus_dynamics.dc_effect_active(NEW.regno, 'EXPULSION') = 1 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'This student is suspended or expelled under a disciplinary decision and cannot be registered. See Disciplinary Records.';
    END IF;
END$$
DELIMITER ;

DROP TRIGGER IF EXISTS campus_dynamics_portal.trg_dc_coursereg_bi;
DELIMITER $$
CREATE TRIGGER campus_dynamics_portal.trg_dc_coursereg_bi BEFORE INSERT ON campus_dynamics_portal.acad_course_registration FOR EACH ROW
BEGIN
    IF campus_dynamics.dc_effect_active(NEW.regno, 'SUSPENSION') = 1 OR campus_dynamics.dc_effect_active(NEW.regno, 'EXPULSION') = 1 THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Suspended or expelled under a disciplinary decision: cannot be registered for courses. See Disciplinary Records.';
    END IF;
END$$
DELIMITER ;

-- Rollback: re-run sql/guardrails/acad_registration_block_autoreg_trigger.sql and
--           DROP TRIGGER campus_dynamics_portal.trg_dc_coursereg_bi;
```

## Appendix C: seed SQL (applied)

```sql
-- ---------------------------------------------------------------------------
-- Student Disciplinary module: starting case types, sanctions, defaults and letter templates
-- File: COOPERP/sql/discipline/2026-10_disciplinary_seed.sql
-- RUN ONLY AFTER THE LISTS IN THE PLAN (section 3) ARE APPROVED. Insert-only, idempotent.
-- ---------------------------------------------------------------------------

INSERT IGNORE INTO dc_sanction_type
 (code, name, description, outcome, effects, needs_amount, needs_dates, needs_course, needs_semester, allowed_interim, sort_order, created_by, created_at) VALUES
 ('WARN_VERBAL',  'Verbal warning',                     'A recorded verbal warning.',                                   'SANCTION', '',                 0,'NONE',   0,0,0, 10,'seed',NOW()),
 ('WARN_WRITTEN', 'Written warning',                    'A written warning placed on the student file.',                'SANCTION', '',                 0,'NONE',   0,0,0, 20,'seed',NOW()),
 ('COMMUNITY',    'Community service',                  'Supervised community service; give the hours and period in the terms.', 'SANCTION', '',        0,'FROM_TO',0,0,0, 30,'seed',NOW()),
 ('FINE',         'Fine',                               'A fine payable to the University. The Bursar raises the bill.', 'SANCTION', 'FINE',            1,'NONE',   0,0,0, 40,'seed',NOW()),
 ('RESTITUTION',  'Restitution',                        'Payment to make good loss or damage. The Bursar raises the bill.', 'SANCTION', 'RESTITUTION',  1,'NONE',   0,0,0, 50,'seed',NOW()),
 ('CANCEL_PAPER', 'Cancellation of a paper result',     'The result of one paper is cancelled through the marks screens.', 'SANCTION', 'CANCEL_PAPER',  0,'NONE',   1,1,0, 60,'seed',NOW()),
 ('CANCEL_SEM',   'Cancellation of a semester''s results', 'All results of one semester are cancelled through the marks screens.', 'SANCTION', 'CANCEL_SEMESTER', 0,'NONE', 0,1,0, 70,'seed',NOW()),
 ('SUSPENSION',   'Suspension',                         'Suspended from studies for the period; cannot register or sit examinations.', 'SANCTION', 'SUSPENSION', 0,'FROM_TO',0,0,1, 80,'seed',NOW()),
 ('EXPULSION',    'Expulsion',                          'Expelled from the University.',                                'SANCTION', 'EXPULSION,PORTAL_BLOCK,GRADUATION_BAR,RESULTS_WITHHELD', 0,'FROM',0,0,0, 90,'seed',NOW()),
 ('WITHHOLD',     'Withholding of results or transcript','Results, the examination card and documents are withheld for the period.', 'SANCTION', 'RESULTS_WITHHELD', 0,'FROM_TO',0,0,1,100,'seed',NOW()),
 ('BAR_GRAD',     'Barring from graduation',            'Cannot be cleared for graduation for the period.',             'SANCTION', 'GRADUATION_BAR',   0,'FROM_TO',0,0,0,110,'seed',NOW()),
 ('PORTAL_BLOCK', 'Portal access blocked',              'The student portal shows only the restriction notice and the student''s own case.', 'SANCTION', 'PORTAL_BLOCK', 0,'FROM_TO',0,0,1,120,'seed',NOW()),
 ('DISMISSAL',    'Dismissal of case',                  'The case is dismissed.',                                       'DISMISSAL','',                 0,'NONE',   0,0,0,130,'seed',NOW()),
 ('ACQUITTAL',    'Acquittal',                          'The student is found not responsible.',                        'ACQUITTAL','',                 0,'NONE',   0,0,0,140,'seed',NOW());

INSERT IGNORE INTO dc_case_type (code, name, description, severity, is_exam_related, default_restricted, sort_order, created_by, created_at) VALUES
 ('EXAM_MALPRACTICE', 'Examination malpractice',                         'Unauthorised material, copying, collusion or misconduct in an examination.', 'SERIOUS', 1, 0,  10, 'seed', NOW()),
 ('IMPERSONATION',    'Impersonation',                                    'Sitting an examination or assessment for another person, or being sat for.', 'GROSS',  1, 0,  20, 'seed', NOW()),
 ('FORGERY',          'Forgery of documents or results',                  'Forged or altered academic documents, results or University records.',     'GROSS',   0, 0,  30, 'seed', NOW()),
 ('FEES_FRAUD',       'Fees fraud or forged payment evidence',            'Forged receipts, bank slips or payment evidence.',                          'GROSS',   0, 0,  40, 'seed', NOW()),
 ('THEFT_DAMAGE',     'Theft or damage to University property',           NULL,                                                                       'SERIOUS', 0, 0,  50, 'seed', NOW()),
 ('VIOLENCE',         'Violence, assault or threats',                     NULL,                                                                       'GROSS',   0, 0,  60, 'seed', NOW()),
 ('SEXUAL_MISCONDUCT','Sexual harassment or misconduct',                  'Handled as a restricted case by default.',                                 'GROSS',   0, 1,  70, 'seed', NOW()),
 ('DRUGS_ALCOHOL',    'Drug or alcohol abuse on campus',                  NULL,                                                                       'SERIOUS', 0, 0,  80, 'seed', NOW()),
 ('STAFF_INDISCIPLINE','Indiscipline towards staff',                      NULL,                                                                       'SERIOUS', 0, 0,  90, 'seed', NOW()),
 ('ICT_MISUSE',       'Breach of ICT or portal use policy',               'Account sharing, unauthorised access, misuse of University systems.',       'MINOR',   0, 0, 100, 'seed', NOW()),
 ('PLAGIARISM',       'Plagiarism or academic dishonesty',                'Outside examinations: coursework, reports, dissertations.',                 'SERIOUS', 1, 0, 110, 'seed', NOW()),
 ('UNAUTH_PROTEST',   'Unauthorised protest or disruption',               NULL,                                                                       'SERIOUS', 0, 0, 120, 'seed', NOW()),
 ('OTHER',            'Other',                                            'Any other breach of the student regulations; describe it in full.',         'MINOR',   0, 0, 130, 'seed', NOW());

-- Default sanctions per case type. Dismissal and acquittal are always available.
INSERT IGNORE INTO dc_case_type_sanction (case_type_id, sanction_type_id, sort_order)
SELECT ct.id, st.id, st.sort_order FROM dc_case_type ct JOIN dc_sanction_type st ON
  (st.code IN ('DISMISSAL','ACQUITTAL'))
  OR (ct.code = 'EXAM_MALPRACTICE'   AND st.code IN ('WARN_WRITTEN','CANCEL_PAPER','CANCEL_SEM','SUSPENSION','EXPULSION','WITHHOLD'))
  OR (ct.code = 'IMPERSONATION'      AND st.code IN ('CANCEL_PAPER','CANCEL_SEM','SUSPENSION','EXPULSION','BAR_GRAD','WITHHOLD'))
  OR (ct.code = 'FORGERY'            AND st.code IN ('CANCEL_SEM','SUSPENSION','EXPULSION','WITHHOLD','BAR_GRAD','PORTAL_BLOCK'))
  OR (ct.code = 'FEES_FRAUD'         AND st.code IN ('RESTITUTION','FINE','WITHHOLD','SUSPENSION','EXPULSION','PORTAL_BLOCK'))
  OR (ct.code = 'THEFT_DAMAGE'       AND st.code IN ('WARN_WRITTEN','RESTITUTION','FINE','COMMUNITY','SUSPENSION','EXPULSION'))
  OR (ct.code = 'VIOLENCE'           AND st.code IN ('WARN_WRITTEN','COMMUNITY','SUSPENSION','EXPULSION'))
  OR (ct.code = 'SEXUAL_MISCONDUCT'  AND st.code IN ('WARN_WRITTEN','SUSPENSION','EXPULSION','BAR_GRAD'))
  OR (ct.code = 'DRUGS_ALCOHOL'      AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','COMMUNITY','FINE','SUSPENSION','EXPULSION'))
  OR (ct.code = 'STAFF_INDISCIPLINE' AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','COMMUNITY','SUSPENSION'))
  OR (ct.code = 'ICT_MISUSE'         AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','PORTAL_BLOCK','FINE','SUSPENSION'))
  OR (ct.code = 'PLAGIARISM'         AND st.code IN ('WARN_WRITTEN','CANCEL_PAPER','CANCEL_SEM','SUSPENSION'))
  OR (ct.code = 'UNAUTH_PROTEST'     AND st.code IN ('WARN_WRITTEN','COMMUNITY','FINE','SUSPENSION','EXPULSION'))
  OR (ct.code = 'OTHER'              AND st.code IN ('WARN_VERBAL','WARN_WRITTEN','COMMUNITY','FINE','SUSPENSION'));

-- Letter templates. Merge fields: {{student_name}} {{regno}} {{programme}} {{faculty}} {{campus}} {{case_no}}
-- {{case_type}} {{incident_date}} {{incident_place}} {{hearing_date}} {{hearing_time}} {{hearing_venue}}
-- {{decision_text}} {{sanctions}} {{appeal_deadline}} {{appeal_window_days}} {{appellate_authority}}
-- {{sanction_lifted}} {{lift_reason}} {{suspension_from}} {{suspension_to}} {{contact_office}} {{today}}
INSERT IGNORE INTO dc_letter_template (code, name, subject, body, student_copy, sort_order, created_by, created_at) VALUES
('SUMMON', 'Summon to appear', 'SUMMONS TO APPEAR BEFORE THE STUDENTS DISCIPLINARY COMMITTEE',
'You are required to appear before the Students Disciplinary Committee on {{hearing_date}} at {{hearing_time}} in {{hearing_venue}}.

The Committee will hear the matter of {{case_type}} alleged to have taken place on {{incident_date}} at {{incident_place}} (case {{case_no}}).

You may bring any evidence and witnesses you wish the Committee to consider, and you may make a written statement. If you do not appear, the Committee may hear the matter in your absence.

Please confirm receipt of this summons with the {{contact_office}}.', 1, 10, 'seed', NOW()),
('DECISION', 'Notice of decision', 'NOTICE OF DECISION OF THE STUDENTS DISCIPLINARY COMMITTEE',
'The Students Disciplinary Committee considered case {{case_no}}, {{case_type}}, and reached the following decision.

{{decision_text}}

Sanctions: {{sanctions}}

You may appeal against this decision to the {{appellate_authority}} within {{appeal_window_days}} days, that is by {{appeal_deadline}}. You can lodge an appeal through My Disciplinary Cases on the student portal, or in writing to the {{contact_office}}.', 1, 20, 'seed', NOW()),
('SUSPENSION', 'Suspension letter', 'SUSPENSION FROM THE UNIVERSITY',
'Following the decision in case {{case_no}}, you are suspended from the University from {{suspension_from}} to {{suspension_to}}.

During this period you may not register for courses, attend classes or sit examinations, and you should not be on University premises without the written permission of the {{contact_office}}.

You may appeal against this decision to the {{appellate_authority}} by {{appeal_deadline}}.', 1, 30, 'seed', NOW()),
('LIFTING', 'Lifting of sanction', 'LIFTING OF DISCIPLINARY SANCTION',
'This is to inform you that the following sanction in case {{case_no}} has been lifted with effect from {{today}}: {{sanction_lifted}}.

Reason: {{lift_reason}}', 1, 40, 'seed', NOW()),
('CLEARANCE', 'Clearance letter', 'DISCIPLINARY CLEARANCE',
'This is to certify that, as at {{today}}, {{student_name}} ({{regno}}), a student of {{programme}}, has no pending disciplinary case and no disciplinary sanction in force at Muteesa I Royal University.', 1, 50, 'seed', NOW()),
('APPEAL_DECISION', 'Appeal decision', 'DECISION ON YOUR APPEAL',
'The {{appellate_authority}} considered your appeal in case {{case_no}} and decided as follows.

{{decision_text}}

Sanctions now in force: {{sanctions}}

This decision is final within the University.', 1, 60, 'seed', NOW());
```

## Appendix D: menu, roles and grants SQL (applied)

```sql
-- ---------------------------------------------------------------------------
-- Student Disciplinary module: menu, permission slugs, roles and grants (database campus_dynamics)
-- File: COOPERP/sql/discipline/2026-10_disciplinary_menu.sql
-- Backs up the three RBAC tables first. Idempotent.
-- Undo: DELETE FROM sys_role_permissions WHERE granted_by='disciplinary-2026-10';
--       UPDATE sys_menu_items SET is_active=0 WHERE menu_slug LIKE 'discipline%';
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS sys_menu_items_bak_dc2026       AS SELECT * FROM sys_menu_items;
CREATE TABLE IF NOT EXISTS sys_role_permissions_bak_dc2026 AS SELECT * FROM sys_role_permissions;
CREATE TABLE IF NOT EXISTS sys_roles_bak_dc2026            AS SELECT * FROM sys_roles;

INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at) VALUES
 ('discipline',                 'Student Discipline',          'academics','parent', 'academics',  NULL,                                                 930,1,NOW()),
 ('discipline.dashboard',       'Disciplinary Dashboard',      'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryDashboard.aspx',    931,1,NOW()),
 ('discipline.records',         'Disciplinary Records',        'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryRecords.aspx',      932,1,NOW()),
 ('discipline.updates',         'Record Updates',              'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryUpdates.aspx',      933,1,NOW()),
 ('discipline.settings',        'Case Types and Sanctions',    'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinarySettings.aspx',     934,1,NOW()),
 ('discipline.reports',         'Disciplinary Reports',        'academics','subitem','discipline', '~/COOPERP/NewScreens/DisciplinaryReports.aspx',      935,1,NOW()),
 ('discipline.report',          'Discipline: report a case (own cases)',               'academics','subitem','discipline',NULL,936,1,NOW()),
 ('discipline.manage',          'Discipline: manage cases in own faculty or department','academics','subitem','discipline',NULL,937,1,NOW()),
 ('discipline.manage_exam',     'Discipline: manage examination cases (all faculties)', 'academics','subitem','discipline',NULL,938,1,NOW()),
 ('discipline.manage_all',      'Discipline: manage all cases',                         'academics','subitem','discipline',NULL,939,1,NOW()),
 ('discipline.view_all',        'Discipline: see all cases, read only',                 'academics','subitem','discipline',NULL,946,1,NOW()),
 ('discipline.hearing',         'Discipline: schedule hearings and issue letters',      'academics','subitem','discipline',NULL,940,1,NOW()),
 ('discipline.decide',          'Discipline: record hearings and decisions',            'academics','subitem','discipline',NULL,941,1,NOW()),
 ('discipline.appeal',          'Discipline: decide appeals',                           'academics','subitem','discipline',NULL,942,1,NOW()),
 ('discipline.restrict',        'Discipline: portal blocks, interim measures, vary or lift sanctions, withdraw cases','academics','subitem','discipline',NULL,943,1,NOW()),
 ('discipline.restricted',      'Discipline: see restricted cases',                     'academics','subitem','discipline',NULL,944,1,NOW()),
 ('discipline.settings_manage', 'Discipline: change settings',                          'academics','subitem','discipline',NULL,945,1,NOW())
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), parent_slug=VALUES(parent_slug), sort_order=VALUES(sort_order), is_active=1;

INSERT INTO sys_roles (role_code, role_name, description, color_hex, is_system_role, is_active, created_by, created_at)
SELECT 'dean_students','Dean of Students','Manages student discipline: all cases, hearings, letters and restrictions','#05275C',0,1,'disciplinary-2026-10',NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_roles WHERE role_code='dean_students');
INSERT INTO sys_roles (role_code, role_name, description, color_hex, is_system_role, is_active, created_by, created_at)
SELECT 'dc_committee','Disciplinary Committee','Members of the Students Disciplinary Committee: hearings and decisions','#174DA4',0,1,'disciplinary-2026-10',NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_roles WHERE role_code='dc_committee');

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT r.id, g.slug, 1, 0, 0, 'disciplinary-2026-10', NOW()
FROM sys_roles r
JOIN (
            SELECT 'dean_students' rc, 'discipline' slug
  UNION ALL SELECT 'dean_students','discipline.dashboard'      UNION ALL SELECT 'dean_students','discipline.records'
  UNION ALL SELECT 'dean_students','discipline.updates'        UNION ALL SELECT 'dean_students','discipline.settings'
  UNION ALL SELECT 'dean_students','discipline.reports'        UNION ALL SELECT 'dean_students','discipline.report'
  UNION ALL SELECT 'dean_students','discipline.manage_all'     UNION ALL SELECT 'dean_students','discipline.hearing'
  UNION ALL SELECT 'dean_students','discipline.restrict'       UNION ALL SELECT 'dean_students','discipline.restricted'
  UNION ALL SELECT 'dean_students','discipline.settings_manage'
  UNION ALL SELECT 'registrar','discipline'                    UNION ALL SELECT 'registrar','discipline.dashboard'
  UNION ALL SELECT 'registrar','discipline.records'            UNION ALL SELECT 'registrar','discipline.updates'
  UNION ALL SELECT 'registrar','discipline.settings'           UNION ALL SELECT 'registrar','discipline.reports'
  UNION ALL SELECT 'registrar','discipline.report'             UNION ALL SELECT 'registrar','discipline.manage_all'
  UNION ALL SELECT 'registrar','discipline.hearing'            UNION ALL SELECT 'registrar','discipline.restrict'
  UNION ALL SELECT 'registrar','discipline.restricted'         UNION ALL SELECT 'registrar','discipline.settings_manage'
  UNION ALL SELECT 'dc_committee','discipline'                 UNION ALL SELECT 'dc_committee','discipline.dashboard'
  UNION ALL SELECT 'dc_committee','discipline.records'         UNION ALL SELECT 'dc_committee','discipline.updates'
  UNION ALL SELECT 'dc_committee','discipline.reports'         UNION ALL SELECT 'dc_committee','discipline.manage_all'
  UNION ALL SELECT 'dc_committee','discipline.decide'
  UNION ALL SELECT 'dean','discipline'                         UNION ALL SELECT 'dean','discipline.dashboard'
  UNION ALL SELECT 'dean','discipline.records'                 UNION ALL SELECT 'dean','discipline.updates'
  UNION ALL SELECT 'dean','discipline.reports'                 UNION ALL SELECT 'dean','discipline.report'
  UNION ALL SELECT 'dean','discipline.manage'
  UNION ALL SELECT 'hod','discipline'                          UNION ALL SELECT 'hod','discipline.records'
  UNION ALL SELECT 'hod','discipline.updates'                  UNION ALL SELECT 'hod','discipline.report'
  UNION ALL SELECT 'hod','discipline.manage'
  UNION ALL SELECT 'exam_officer','discipline'                 UNION ALL SELECT 'exam_officer','discipline.dashboard'
  UNION ALL SELECT 'exam_officer','discipline.records'         UNION ALL SELECT 'exam_officer','discipline.updates'
  UNION ALL SELECT 'exam_officer','discipline.reports'         UNION ALL SELECT 'exam_officer','discipline.report'
  UNION ALL SELECT 'exam_officer','discipline.manage_exam'
  UNION ALL SELECT 'faculty_staff','discipline'                UNION ALL SELECT 'faculty_staff','discipline.records'
  UNION ALL SELECT 'faculty_staff','discipline.report'
  UNION ALL SELECT 'student_services','discipline'             UNION ALL SELECT 'student_services','discipline.records'
  UNION ALL SELECT 'student_services','discipline.report'
  UNION ALL SELECT 'vc','discipline'                           UNION ALL SELECT 'vc','discipline.dashboard'
  UNION ALL SELECT 'vc','discipline.records'                   UNION ALL SELECT 'vc','discipline.reports'
  UNION ALL SELECT 'vc','discipline.appeal'          UNION ALL SELECT 'vc','discipline.view_all'
  UNION ALL SELECT 'auditor','discipline'                      UNION ALL SELECT 'auditor','discipline.dashboard'
  UNION ALL SELECT 'auditor','discipline.records'              UNION ALL SELECT 'auditor','discipline.updates'
  UNION ALL SELECT 'auditor','discipline.reports'              UNION ALL SELECT 'auditor','discipline.view_all'
) g ON g.rc = r.role_code
WHERE NOT EXISTS (SELECT 1 FROM sys_role_permissions x WHERE x.role_id = r.id AND x.menu_slug = g.slug);

-- The case file and its file handler are reached from the records list, not the sidebar. They get slugs
-- of their own so that page-gated roles (the read-only Auditor) can open them; every role that holds
-- discipline.records receives both.
INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at) VALUES
 ('discipline.case',  'Disciplinary case file',           'academics','subitem','discipline','~/COOPERP/NewScreens/DisciplinaryCase.aspx',947,1,NOW()),
 ('discipline.files', 'Disciplinary letters and evidence','academics','subitem','discipline','~/COOPERP/NewScreens/DcFile.ashx',948,1,NOW())
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), parent_slug=VALUES(parent_slug), sort_order=VALUES(sort_order), is_active=1;

INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT p.role_id, s.slug, 1, 0, 0, 'disciplinary-2026-10', NOW()
FROM sys_role_permissions p JOIN (SELECT 'discipline.case' slug UNION ALL SELECT 'discipline.files') s
WHERE p.menu_slug='discipline.records'
  AND NOT EXISTS (SELECT 1 FROM sys_role_permissions x WHERE x.role_id=p.role_id AND x.menu_slug=s.slug);
```

## Appendix E: test-data purge (run once before go-live)

```sql
-- ---------------------------------------------------------------------------
-- Student Disciplinary module: removes the acceptance-test data (run once before go-live, 6 Oct 2026).
-- Touches ONLY rows that belong to the two test students MRUZZTEST0001/0002 and the test users zz_dc_*.
-- The append-only guards refuse deletes, so they are dropped for this run and restored at the end by
-- re-running 2026-10_disciplinary_schema.sql (idempotent). Back up the dc_ tables first.
-- ---------------------------------------------------------------------------
DROP TRIGGER IF EXISTS trg_dc_entry_bd;   DROP TRIGGER IF EXISTS trg_dc_case_bd;      DROP TRIGGER IF EXISTS trg_dc_sanction_bd;
DROP TRIGGER IF EXISTS trg_dc_audit_bd;   DROP TRIGGER IF EXISTS trg_dc_letter_bd;    DROP TRIGGER IF EXISTS trg_dc_appeal_bd;
DROP TRIGGER IF EXISTS trg_dc_incident_bd; DROP TRIGGER IF EXISTS trg_dc_attachment_bd; DROP TRIGGER IF EXISTS trg_dc_notification_bd;
DROP TRIGGER IF EXISTS trg_dc_access_bd;

DROP TABLE IF EXISTS zz_cases;
CREATE TABLE zz_cases AS SELECT id, incident_id FROM dc_case WHERE regno IN ('MRUZZTEST0001','MRUZZTEST0002');
DELETE FROM dc_access_log  WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_notification WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_letter      WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_appeal      WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_sanction    WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_hearing     WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_attachment  WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_entry       WHERE case_id IN (SELECT id FROM zz_cases);
DELETE FROM dc_audit       WHERE case_id IN (SELECT id FROM zz_cases)
                              OR (entity='INCIDENT' AND entity_id IN (SELECT incident_id FROM zz_cases));
DELETE FROM dc_case        WHERE id IN (SELECT id FROM zz_cases);
DELETE FROM dc_incident    WHERE id IN (SELECT incident_id FROM zz_cases) AND NOT EXISTS (SELECT 1 FROM dc_case c WHERE c.incident_id=dc_incident.id);
DELETE FROM acad_activity_log WHERE page_function='Student Discipline' AND user_id LIKE 'zz\_dc\_%';
-- Numbering starts again at 0001 for real cases, but only if no real case has been opened.
DELETE FROM dc_sequence WHERE NOT EXISTS (SELECT 1 FROM dc_case);
DROP TABLE zz_cases;

-- The test students and their portal logins.
DELETE FROM acad_student WHERE regno IN ('MRUZZTEST0001','MRUZZTEST0002');
DELETE m FROM campus_dynamics_portal.my_aspnet_membership m JOIN campus_dynamics_portal.my_aspnet_users u ON u.id=m.userId WHERE u.name IN ('MRUZZTEST0001','MRUZZTEST0002');
DELETE FROM campus_dynamics_portal.my_aspnet_users WHERE name IN ('MRUZZTEST0001','MRUZZTEST0002');
-- Then: re-run 2026-10_disciplinary_schema.sql to restore the guards, and delete Data_Private\Disciplinary\{case ids}.
```
