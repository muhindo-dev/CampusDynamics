# The Auditor role

A read-only role. It opens the dashboards, the audit screens, the reports and the main record
lists across every core module, and it cannot change anything, anywhere, by any route.

Held by **Mugagga Joseph** (`mugajose@gmail.com`).

---

## For administrators: what it does

An Auditor can open 49 screens and read them. If they try to open anything else, or to perform
any operation that changes data even on a screen they are allowed to open, the request is
refused before it runs and the refusal is written down.

They are not trusted to behave. They are prevented.

### What they can see

| Module | Screens |
|---|---|
| **Home** | Dashboard |
| **Academics** | Allocation dashboard, workload analysis, marks dashboard, all marks, correction register, programme courses dashboard, academic committee report, rearrangement dashboard and logs, enrolment analysis, timetable view, graduation list |
| **Fees** | Fees dashboard, audit trail, student ledgers, fee structure, active students, double billing, bursary dashboard, bursary beneficiaries |
| **Accounts** | Finance dashboard, audit trail, transaction audit trail, batch monitor, double-entry validation, financial periods, financial reports, trial balance, income statement, balance sheet |
| **HR** | HR dashboard, appraisal dashboard, appraisal reports, view appraisals, employee directory, contracts, payslips |
| **System** | Audit centre, marks audit trail, results audit log, results analytics, student results view, academic results, chart of accounts, graduation analysis, access overview, access audit log, academic years |

### What they cannot do

Everything else. Capture, approve or publish marks. Bill, reverse or waive anything. Close a
period. Register a student. Touch payroll. Administer users or roles. Open any screen not in
the table above, including by typing its address.

### When an Auditor is refused

They see a plain page saying so, with a link back to the dashboard. Nothing is changed and
nothing is half-done. The refusal is recorded in `sys_access_denied_log` with the user, the
screen, the operation, and the time.

A run of refusals against one screen is worth looking at: it usually means somebody needs
access they have not been given, rather than somebody trying it on.

### Widening the role

Grant the slug in `sys_role_permissions` against the `auditor` role with `can_view = 1`.
Access refreshes within a minute. No code change is needed to add a screen.

To create a **second** read-only role later (an external examiner, a board observer), create
the role with `is_read_only = 1` and grant its slugs. Everything below applies to it
automatically.

---

## For developers: how it is enforced

### Why the existing RBAC was not enough

Three facts, each measured rather than assumed:

1. `sys_role_permissions` has carried `can_view`, `can_edit` and `can_delete` from the start.
   **Nothing has ever read `can_edit` or `can_delete`.** The only occurrence in the codebase is
   the statement that copies them when a role is cloned. A role was a menu filter.
2. `RoleAccessService.RequireSlug` is correct and is called by **11 files out of 195 screens**.
   The sidebar hid links from users who could still reach every page by URL.
3. The write surface is mostly AJAX: **36 screens, 253 PageMethod declarations, 162 distinct
   names**, plus 8 screens with `?action=` handlers. PageMethods do not run the page lifecycle,
   so nothing a page does in `Page_Load` can guard them.

So the role is enforced in one place that every request passes through.

### Where the gate runs, and why that stage

`ReadOnlyGate.Intercept`, called from `Application_PostAuthenticateRequest` in `Global.asax`.

The stage was chosen by measurement, not by reading documentation. A probe on three pipeline
stages, with a PageMethod POST, logged this:

| Stage | Plain page GET | PageMethod POST |
|---|---|---|
| `PostAuthenticateRequest` | fires | **fires** |
| `PostAcquireRequestState` | fires | does not fire |
| `PreRequestHandlerExecute` | fires | does not fire |

The gate was first written on `PreRequestHandlerExecute`, which guarded the pages perfectly and
left **every AJAX write wide open**, silently. The tests caught it; reading the code would not
have.

A consequence of that stage: **Session does not exist yet.** The gate identifies the user from
the forms authentication ticket (`HttpContext.User.Identity.Name`), which is established at
`AuthenticateRequest`, and caches the two lookups it needs in `HttpRuntime.Cache` for 60
seconds rather than in the session it does not have.

### The two questions it asks

**1. May this user open this page?** Their granted slugs resolve to a set of page file names.
Anything outside it is refused, menu or no menu.

**2. Is this operation a read?** A PageMethod arrives as `Page.aspx/MethodName`; older screens
pass `?action=Name`. A plain page view names no operation and is a read by definition.

### The read test is an allowlist, deliberately

The 162 method names nearly separate by verb, but not well enough to denylist:

```
reads     Get 47   Preview 7   Search 5   Stats 5   Count 4   Browse 3   Init 3 ...
writes    Save 9   Create 9   Publish 8   Batch 8   Reverse 7   Set 7   Delete 5 ...
ambiguous Admin 11   Review 5   Return 3
```

`CreateAndPreview` writes a draft record despite saying Preview. `Admin*` covers both
`AdminGetX` and `AdminSaveX`. A denylist would let several real writes through, and would let
through **every method written after today**.

So: a name is refused unless it is known to be a read. A new PageMethod added next year is
denied to the Auditor by default, which is the correct direction for this role to fail in.

The test runs in three steps, in order:

1. An explicit allowlist of reads whose names say nothing useful (`HoldReasons`,
   `RegSearchStudents`, `StageDriftCount`). **Each was read in the source before being listed.**
2. Write words, matched **as whole words**, not substrings. This is not a nicety: a substring
   search finds `review` inside `PreviewBatchWorkflow` and refuses a method that only counts
   rows. The test caught exactly that. Names are camel case, so they tokenise cleanly.
3. Read prefixes. Anything still unmatched is refused.

### Files

| File | What it is |
|---|---|
| `App_Code/ReadOnlyGate.cs` | The gate: the rule, the refusal, the logging |
| `Global.asax` | One call in `Application_PostAuthenticateRequest` |
| `COOPERP/sql/security/auditor_role.sql` | `is_read_only` column, log table, the role, its 49 grants |
| `COOPERP/NewScreens/AUDITOR_ROLE_PLAN.md` | The plan this was built from |

### A read-only role only holds if it is the user's only role

Somebody who is both an Auditor and a Bursar is a Bursar: the gate checks for any non
read-only role and stands down if it finds one. Otherwise granting the role would lock a
working account out of its real job.

This is why Mugagga Joseph's `admin`, `bursar` and `accountant` assignments were deactivated
when the Auditor role was granted. He held all three, and `LoadUserAccess` selects one role by
lowest id, so `admin` would have kept winning and the grant would have changed nothing.

---

## How it was tested

Against the running site, signed in with a genuine forms ticket, not by reading the diff.

| Test | Result |
|---|---|
| 34 read method names must be allowed | pass |
| 33 write method names must be refused | pass |
| 4 invented names must default to refused | pass |
| Auditor resolves as read-only, with 49 pages | pass |
| 5 granted pages open | 200 |
| 2 non-granted pages by direct URL | 403, refused |
| `SaveAdminMarks`, `PublishMarks` on a page they CAN open | 403, refused |
| `RegSearchStudents`, `PreviewBatchWorkflow` on the same page | 200 |
| Refusals recorded with user, path and operation | 4 rows |
| An administrator doing both of the above | 200, unaffected |

Two real defects were found this way and fixed: the pipeline stage, and the substring match.

## What this does not do

- **No database-level enforcement.** A read-only MySQL user would be immune to any endpoint
  anyone forgets, but the application resolves one connection string statically in hundreds of
  places. If a guarantee is ever needed rather than a strong control, that is the work.
- **The allowlist trusts method names.** A `GetX` that quietly writes would pass. Every name on
  the explicit allowlist was read first; the prefix rule is a convention the codebase follows,
  not a proof.
- **Anything served outside the ASP.NET pipeline** does not pass the gate.
