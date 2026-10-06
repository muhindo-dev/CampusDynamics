# Student Discipline: user guide

For the Office of the Dean of Students, the Academic Registrar, the Students Disciplinary Committee, deans, heads of department, the examination office, the Bursar, lecturers and MIS.
Module built October 2026. Design and decisions: `COOPERP/docs/disciplinary-plan.md`.

---

## 1. Where to find it

In eadmin, the sidebar heading **Student Discipline** has five screens:

| Screen | What it is for |
|---|---|
| Disciplinary Dashboard | Open cases, hearings, appeals, students under sanction, and what needs attention |
| Disciplinary Records | Every case you may see; report a new case; open a case file |
| Record Updates | Everything recorded in the case files, newest first |
| Case Types and Sanctions | The lists, letter templates, committee and settings |
| Disciplinary Reports | Registers, schedules and the Senate summary in PDF, Excel and CSV |

You see the screens your role allows. Each action also checks your permission when you save.

## 2. Who can do what

| Role | Can |
|---|---|
| Dean of Students, Academic Registrar | Everything except deciding cases and appeals; see restricted cases |
| Disciplinary Committee | See all cases; record hearings and decisions |
| Vice-Chancellor (appellate authority) | See all cases; decide appeals; accept late appeals |
| Dean | Report cases; manage the cases of their faculty |
| Head of Department | Report cases; manage the cases of their department |
| Examination Officer | Report cases; manage examination cases in every faculty |
| Lecturers and other staff with the reporting role | Report a case; add notes and evidence to cases they reported |
| Auditor | Read everything they may see; change nothing |

Grant the **Dean of Students** and **Disciplinary Committee** roles in User Roles. The Committee chair is recorded under Case Types and Sanctions, Committee.

**Restricted cases** (for example sexual harassment cases) are seen only by officers allowed to see restricted cases, administrators and the Committee chair. Everyone else does not see them in lists, searches, counts, charts, feeds, reports or exports. A direct link gives "not found". Each opening of a restricted case is logged.

## 3. A case from report to close

1. **Report a case.** Disciplinary Records, *Report a case*.
   - Add every student involved: one incident opens a separate case for each student.
   - Give the type, what happened, the date and the place.
   - For an examination case, choose the paper from the timetable.
   - You can link a complaint ticket (the ticket screen has *Open disciplinary case*) and attach evidence.
   - **Interim measures** (block the portal, withhold results, suspend pending the hearing) take effect at once if you hold the restriction permission. If you do not, they are recorded as a recommendation for an officer to confirm.
2. **Place under investigation** and assign a case officer.
3. **Schedule the hearing.**
   - Give the date, time, venue and panel.
   - Tick *Summon the student now* to issue the summons letter and notify the student.
   - A hearing less than 3 days away needs a reason for the short notice.
   - Several students from one incident can be heard together: select them in Disciplinary Records, then *Schedule one hearing*.
4. **Adjourn**, if needed. An adjourned hearing after a summons re-summons the student automatically, with a new letter.
5. **Record the hearing**: who attended, the panel present and a summary. The signed minutes can be attached.
6. **Record the decision** (Committee).
   - Write the findings and the decision.
   - Choose the sanctions (the usual ones for the case type are listed first), or dismissal or acquittal.
   - On saving:
     - the sanctions take effect;
     - interim measures end;
     - the decision letter (and a suspension letter, if any) is issued;
     - the student is notified;
     - the 14-day appeal window starts.
   - A minor case may be decided without a hearing. A serious or gross case cannot.
7. **Appeal.** The student appeals from the portal within the window, or the office lodges it for them. The appellate authority decides it:
   - *dismissed*: the decision stands;
   - *upheld*: the decision is set aside;
   - *varied*: new sanctions replace the old;
   - *withdrawn*.
8. **Close** once the window has passed or the appeal is decided. Closing early, while the window is open, needs the reason (for example a written waiver). Sanctions with end dates continue until those dates.
9. **Withdraw** an undecided case if it should not continue. Interim measures are lifted, hearings cancelled and the student told.

Nothing in a case file is ever edited or deleted. To fix a mistake, use *Add a correction* on the entry. The correction is added, linked to the original, and the original stays.

## 4. Sanctions and what they do

| Effect | What the system does |
|---|---|
| Portal access blocked | The student can open only the restricted page: their case, decisions, letters and the appeal form. |
| Results withheld | Results, CGPA, provisional scores, printed statements, the exam card, transcripts and certificates are withheld. |
| Suspension or expulsion | No semester or course registration by any route (the database refuses it). No exam clearance or exam card. Graduation is blocked. |
| Barred from graduation | Graduation check C9 blocks the student and cannot be overridden in the Graduation Centre. |
| Cancellation of a result, fine, restitution | Recorded as an action **owed**. See below. |

- A sanction ends on its end date by itself. Overnight the case file records the ending and the student is notified.
- *Lift* ends a sanction early, with a reason; a lifting letter is optional.
- *Vary* changes its terms: the old sanction is kept as "varied" and a new one recorded.

**Actions owed (follow-ups).** The module never changes marks or fees itself.
- **Cancellations:** the marks office cancels the result in the marks screens, giving the case number as the reason.
- **Fines and restitution:** the Bursar raises the bill in the fees screens, quoting the case number.
- Each then opens the case, Sanctions tab, *Record action done*, and enters the reference.

Until a cancellation is recorded as done, the student's graduation stays blocked. The dashboard shows how many actions are owed.

## 5. Letters

*Issue a letter* shows a preview, then builds the PDF on University letterhead with the Academic Registrar's name and title. Letters are kept exactly as issued and cannot be changed; to correct one, issue a new one. Summons, decision, suspension and appeal-decision letters are issued automatically at the step they belong to.

A **clearance letter** can be issued only when the student has no open case, no sanction in force and no action owed.

To show the Registrar's signature on letters, place the image at `COOPERP/NewScreens/images/ar_signature.png` (the path set in the admission letter settings). Until then, letters print a signature line.

## 6. Notices to the student

Every summons, decision, appeal outcome, lifting, block and withdrawal is shown to the student on the portal straight away, at the top of their pages until they open it. It is also emailed to their University address (or the address on their record).

Emails are not attached letters, because letters are confidential; the student downloads them from the portal. Failed emails are retried. The dashboard shows notices not read after 7 days and emails not delivered, so the office can reach those students another way.

## 7. Reports

| Report | Use |
|---|---|
| Disciplinary register | Every case in a period or year |
| Cases by type and severity | Counts, outcomes, days to decision |
| Cases by faculty, department and programme | Cases by stage |
| Students currently under sanction | Who is restricted today |
| Hearing schedule | Hearings by day |
| Summon list | Students summoned for one day, by campus |
| Appeals register | Every appeal and its outcome |
| Case statement | One case in full: committee copy or student copy (from the case file, *Statement*) |
| Senate summary | The year in figures with a written summary |
| Follow-ups owed | Cancellations, fines and restitution not yet carried out |
| Notices not delivered | Unread notices and failed emails |

## 8. What the student sees (for the office to explain)

- **My Disciplinary Cases** on the portal (a link appears in the footer once they have a case) lists every case. For each case it shows:
  - the decision and the sanctions with their dates;
  - any hearing they are summoned to;
  - their letters to download;
  - what has happened.
- It never shows internal notes, findings, witness details or panel deliberations.
- While the appeal window is open, the student can lodge an appeal there, with a file. The appeal is recorded at once and the case moves to "under appeal".
- A student whose portal access is blocked sees only that page, with the reason and the office to contact.

## 9. Settings

Case Types and Sanctions, Settings: the appeal window (14 days), overdue threshold, appeal-closing warning, minimum summons notice (3 days), largest file (15 MB), and the contact office, appellate authority and letter office printed on letters and shown to students. Case types, sanctions and letter templates are never deleted. Make them inactive instead.

## 10. For MIS

- **Tables and rules:**
  - The tables are `dc_*` in `campus_dynamics`.
  - The single rule for "is this restriction in force" is the database function `dc_effect_active(regno, effect)`, used by eadmin, the portal and the registration triggers.
  - The nightly event `ev_dc_expire_sanctions` (00:10) records endings. The event scheduler must stay on; opening the dashboard also runs it.
- **Guards:** triggers refuse deletes and edits of the case file, audit, letters and sanction terms.
- **Scripts:** in `COOPERP/sql/discipline/`: schema, registration guard, seed, menu and roles, and the test-data purge used before go-live.
- **Files:** stored outside the web roots in `Data_Private\Disciplinary\{case id}`.
- **Error logs:** `App_Data\Discipline\errors.log` in each application.
