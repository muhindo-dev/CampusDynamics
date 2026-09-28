# Auditor role: final report

Completed and tested. The role is held by **Mugagga Joseph** (`mugajose@gmail.com`) and now
covers **67 screens** across every core module.

The design and the reasoning behind it are in [AUDITOR_ROLE.md](AUDITOR_ROLE.md); the plan it
was built from is in [AUDITOR_ROLE_PLAN.md](AUDITOR_ROLE_PLAN.md). This report covers what
changed in the second round and what the whole thing now does.

---

## 1. The nine pages that were asked for

All nine open for the Auditor. Six of them could not have been granted before, because
**five live screens had no row in `sys_menu_items` at all** and a grant is a row against a
menu slug.

| Page | Slug | Was |
|---|---|---|
| GeneralDashboard.aspx | `system.more.general_dashboard` | **had no menu item** |
| Knowledgebase.aspx | `system.more.knowledgebase_read` | **had no menu item** |
| NewStudentInfo.aspx | `academics.students.info` | **had no menu item** |
| AdmissionAnalysis.aspx | `academics.admissions.analysis` | **had no menu item** |
| AlumniDataBank.aspx | `academics.students.alumni_databank` | **had no menu item** |
| MarksActionLog.aspx | `academics.exam.marks_action_log` | **had no menu item** |
| GraduationList.aspx | `academics.graduation.list` | already granted |
| GraduationAnalysis.aspx | `system.more.graduation_analysis` | already granted |
| FeesStructure.aspx | `fees.fee_admin.structure` | already granted |

One trap worth recording: `academics.students.alumni` already existed and points at
**AlumniStudents.aspx**, which is a different screen from **AlumniDataBank.aspx**. Reusing it
would have granted the wrong page and left the requested one unreachable, so the data bank
got its own slug.

**Related screens added alongside**, on the same read-only test: Provisional Marks, Fee Access
Checker, Chart of Accounts Lifecycle, Appraisal Sessions, Alumni.

### Transactions

Added afterwards, and as a family rather than as the one screen labelled "Transactions".
Auditing money means following it: **FeesTransactions** is where a payment is captured,
**GeneralLedger** is where it lands, and **PaymentVouchers**, **JournalEntries** and
**ContraVouchers** are the instruments that moved it, with **LedgerCategories** for how they
are classified. Granting one and withholding the rest would leave an auditor able to see an
entry and unable to see what produced it.

The voucher and journal screens are where those documents are raised, which sounds like the
wrong thing to hand a read-only role. It is not: the gate refuses every write on them, so what
the Auditor gets is the register, which is what an audit reads.

`FeesTransactions` also carries four actions, and reading them settled how to treat them:
`batchdup_scan` (223 lines) and `glsync_scan` (106 lines) contain **no INSERT, UPDATE or
DELETE at all**, while `batchdup_fix_one` and `glsync_fix` hold the three write statements
between them. So "scan" became a read word and the two scans work; the two fixes are refused
on the ordinary rule, having neither a read word nor a name that starts like one.

## 2. Menu items now match access, for everyone

This was the second request, and the cause turned out to be general rather than specific to
the Auditor.

The sidebar filter maps each link to a slug and skipped what it could not map:

```js
var slug = map[fileFromHref(href)];
if (!slug) continue;        // unmapped page → keep visible
```

**Sixteen sidebar links had no menu item**, so they were shown to every signed-in user
whatever their role, and for a gated user they were doors that answer 403. They are now
registered:

Billing Health, Billing Reconciliation, SchoolPay Controller, Course Deletion Requests, Exam
Configuration, Missing Marks, Results Exporter, Retake Controller, Timetable Calendar,
Timetable Manager, Rooms & Buildings, ID Card Controller, Photo Change Controller, Student
Email Controller, ODEL Dashboard, ODEL Policy.

**170 sidebar links, 170 registered, 0 escaping the filter.**

Registering a page makes it access-controlled for the first time, which would have removed it
from every role that can see it today. So the same migration granted each new slug to the
roles that already hold ground in the same section: **11 roles keep exactly what they had**.
The Auditor is excluded by name, these being operational screens.

### And the fail-open is closed for gated users

For a user whose page access is actually enforced, the menu is now filtered against **the very
set the gate uses**, read from the same method, so the menu and the gate cannot drift into two
opinions. For them an unmapped link is hidden rather than shown, because it is a page the gate
will refuse.

For every other role the previous fail-open behaviour is untouched: an unmapped link stays
visible, so menu filtering still cannot lock anyone out.

## 3. Three defects the testing found

None of these would have been visible by reading the code.

**The read test was prefix-only.** `SpecList` feeds a dropdown on the student list, and a
prefix rule refused it because the name starts with "Spec". The page would have half worked
with no clue why. Read words are now matched as words anywhere in the name, so `SpecList`,
`RegSearchStudents` and `StageDriftCount` pass on their own merits. Order still protects it: a
name carrying both a write word and a read word was already refused before this test runs, so
`DeleteList` never reaches it.

**`Logs` was refused.** The word-matching rule required four characters for a prefix match and
"log" is three. Lowered to three.

**`ChangeProgInit` was allowed**, because it carries "init". It opens the change-programme
dialog, and a reader has no business there. The obvious fix, making "change" a write word, was
wrong: `MarkChanges` is a read the Auditor genuinely needs. It went on an explicit deny list
instead, which is the honest mechanism for a case the rules cannot express.

Two write words were also added after reading the student list's 27 actions: **`edit`** (for
`QuickEditLoad`) and **`set`** (for `SetPhoto`, `SetPassword`, `CheckStudentForSetPassword`).
Short words are matched exactly, so "set" cannot swallow "Settings".

## 4. Test results

Against the running site, signed in with a genuine forms ticket.

| Test | Result |
|---|---|
| 33 read names allowed, including the student list's own actions | **pass** |
| 34 write names refused, including all 12 write actions on the student list | **pass** |
| 3 invented names default to refused | **pass** |
| Auditor resolves as gated, 67 pages | **pass** |
| The nine requested pages open | **9 × 200** |
| SchoolPay, ID Card, Timetable Manager, Retake, Missing Marks by URL | **5 × 403** |
| `?action=ListStudents`, `?action=SpecList` | 200 |
| `?action=ChangeProgramme`, `SetPhoto`, `ResetPasswordToDefault`, `GenerateAcademicDocument` | **4 × 403** |
| Sidebar rendered for the Auditor | **73 visible, 121 hidden** |
| Visible links pointing at a page the gate would refuse | **0** |
| 6 transaction screens open | **6 × 200** |
| `batchdup_scan`, `glsync_scan` | **2 × 200** |
| `batchdup_fix_one`, `glsync_fix` | **2 × 403** |
| Administrator: `cdPageGated=false`, slugs `'*'`, both tests above | **200, unaffected** |

The temporary handler that issued test tickets has been deleted and returns 404; test rows
were removed from `sys_access_denied_log`.

## 5. What an administrator needs to know

- **Widening the role:** grant the slug against `auditor` with `can_view = 1`. Access and the
  menu both refresh within a minute. No code change.
- **A new screen:** give it a row in `sys_menu_items`. Without one it is invisible to the
  access filter, which is how the sixteen above came to be shown to everybody.
- **Refusals are recorded** in `sys_access_denied_log` with user, path, operation and time. A
  run of them against one screen usually means somebody needs access they have not got.
- **A read-only role only holds if it is the user's only role.** Someone who is both an Auditor
  and a Bursar is a Bursar. This is why Mugagga Joseph's `admin`, `bursar` and `accountant`
  assignments were deactivated when the Auditor role was granted; 22 other administrators
  remain.

## 6. Limits, stated plainly

- **No database-level enforcement.** A read-only MySQL user would be immune to any endpoint
  anyone forgets, but the application resolves one connection string statically in hundreds of
  places. If a guarantee is ever needed rather than a strong control, that is the work.
- **The allowlist trusts method names.** A `GetX` that quietly writes would pass. Every name on
  the explicit allow and deny lists was read in the source first; the word rule is a convention
  the codebase follows, not a proof.
- **The menu filter runs in the browser.** It decides what is shown, never what is permitted:
  the gate refuses on the server whatever the menu displays.

## 7. Files

| File | What it is |
|---|---|
| `App_Code/ReadOnlyGate.cs` | The gate: the rule, the refusal, the logging, the page set |
| `Global.asax` | One call in `Application_PostAuthenticateRequest` |
| `COOPERP/NewScreens/SidebarMaster.master(.cs)` | Emits the gated page set; filters against it |
| `COOPERP/sql/security/auditor_role.sql` | Column, log table, the role, the first 49 grants |
| `COOPERP/sql/security/auditor_role_extra.sql` | The nine requested pages and their relatives |
| `COOPERP/sql/security/register_unmapped_menu_items.sql` | The sixteen unregistered screens |
| `COOPERP/sql/security/auditor_role_transactions.sql` | The transaction family |
| `AUDITOR_ROLE.md` | How it works, for developers and administrators |
| `AUDITOR_ROLE_PLAN.md` | The plan it was built from |
