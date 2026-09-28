# The Auditor role: plan

A read-only role that can open the dashboards, the audit screens, the reports and the main
record lists across every core module, and cannot change anything anywhere.

---

## 1. What the system already has, and what it does not

Worth stating plainly, because it decides the whole design.

**There is an RBAC system.** `sys_roles`, `sys_user_roles`, `sys_role_permissions`,
`sys_menu_items` (185 items), and `RoleAccessService`. A user's accessible menu slugs are
loaded into the session at login and drive which links the sidebar renders.

**`sys_role_permissions` already has `can_view`, `can_edit` and `can_delete`.** The columns
are written when a role is cloned. **Nothing in the application reads `can_edit` or
`can_delete`.** A search of the entire codebase returns the clone statement and nothing else.
So today a role is a menu filter, not a permission.

**Page-level access is barely enforced.** `RoleAccessService.RequireSlug` exists and is
correct, but only **11 files** call it, against 195 screens. Every other page is reachable by
typing its URL, whatever the sidebar shows.

**The write surface is mostly AJAX.** 36 screens expose 253 PageMethod declarations under 162
distinct names, plus 8 screens with `?action=` handlers. PageMethods do not run the page
lifecycle, so nothing a page does in `Page_Load` can guard them.

The consequence: **a read-only role cannot be built out of the existing pieces.** Hiding menu
items would produce a role that looks read-only and is not.

## 2. What "read-only" has to mean here

Three things have to be true, and each needs its own mechanism.

| | Requirement | Mechanism |
|---|---|---|
| L1 | The auditor sees only their own menu | `sys_role_permissions.can_view` (exists) |
| L2 | The auditor cannot open a page outside their set, even by URL | new global page gate |
| L3 | The auditor cannot perform a write on a page they CAN open | new global write gate |
| L4 | Every refusal is recorded | new log table |

L2 and L3 both live in `Global.asax`'s `Application_PreRequestHandlerExecute`, which already
exists, runs before the handler, and has the session available. One gate, every request,
nothing to remember to add to a new page.

### The rule for L3, and why it is an allowlist

The survey of 162 PageMethod names splits cleanly enough to be useful but not cleanly enough
to denylist:

```
reads     Get 47   Preview 7   Search 5   Stats 5   Count 4   Browse 3   Init 3
          Detail 3   Records 3   Progress 3
writes    Save 9   Create 9   Publish 8   Batch 8   Reverse 7   Set 7   Force 6
          Hold 6   Bulk 6   Delete 5   Execute 5   Reset 5   Commit 3   Cancel 3
ambiguous Admin 11   Review 5   Return 3
```

`CreateAndPreview` writes a draft record despite containing "Preview". `Admin*` covers both
`AdminGetX` and `AdminSaveX`. A denylist of write verbs would therefore let several real
writes through, and would let through every method added after today.

**So the gate is an allowlist: a method is refused unless it is known to be a read.** A new
PageMethod written next year is denied to the auditor by default, which is the correct
direction for this role to fail in.

The same applies to `?action=` values.

### What is deliberately NOT attempted

A read-only **database user** would be the real guarantee, immune to any endpoint anyone
forgets. It is not feasible here: the whole application resolves one connection string
(`vacConnectionString`) statically in hundreds of places, and swapping it per request would
mean touching all of them. The residual risk is stated in section 7 rather than hidden.

## 3. The role

| Field | Value |
|---|---|
| `role_code` | `auditor` |
| `role_name` | Auditor |
| `description` | Read-only access to dashboards, audit trails, reports and record lists across all modules. Cannot create, change or delete anything. |
| `color_hex` | `#0f766e` |
| `is_system_role` | 0 |
| `is_read_only` | **1** (new column) |

`is_read_only` is a new column on `sys_roles`. Making it a property of the role rather than a
hardcoded check on the string "auditor" means a second read-only role later (an external
examiner, a board observer) needs no code change at all.

## 4. What the Auditor can see

Across all five sections. Chosen on one test: would an auditor need it to verify what the
institution reports about itself?

**Academics** dashboards and analysis (allocation, workload, marks, enrolment, programme
courses, rearrangement), the correction register, rearrangement logs, timetable view, the
graduation list and graduation analysis, all marks, published results, the student list.

**Fees** dashboard, audit trail, student ledgers, transactions, fee structure, bursary
dashboard and beneficiaries, active students.

**Accounts** finance dashboard, both audit trails, batch monitor, double-entry validation,
financial periods, chart of accounts, and all four statutory reports (trial balance, income
statement, balance sheet, financial reports).

**HR** dashboard, appraisal dashboard, appraisal reports, view appraisals, employee
directory, payslips, contracts.

**System** audit centre, marks audit trail, results audit log, results analytics, student
results view, access overview, the access audit log, academic years, system configuration
(view only), and the home dashboard.

Excluded on purpose: everything whose only reason to exist is to change something. Capture,
approve, publish, reversal requests, period close, billing, payroll processing, user and role
administration. An auditor reads the outcome of those, not their controls.

## 5. Order of work

1. `is_read_only` column on `sys_roles`; `sys_access_denied_log` table.
2. The `auditor` role row.
3. The slug grants, `can_view=1, can_edit=0, can_delete=0`.
4. `ReadOnlyGate` class: is the current user read-only, is this path allowed, is this
   operation a read, and the logging of refusals.
5. Hook it into `Application_PreRequestHandlerExecute`.
6. The refusal page, which has to say why and by whose rule, not just "denied".
7. Assign to Mugagga Joseph and retire his other roles (see below).
8. Test every layer against the running site.
9. Document as built.

### Mugagga Joseph

`mugajose@gmail.com`, in `hrm_employee` as "Mugagga Joseph". He currently holds **three**
roles: `admin` (expiring 2026-10-04), `bursar` and `accountant`. `LoadUserAccess` selects one
role by lowest id, so `admin` wins and he is effectively an administrator today.

Adding `auditor` alongside them would therefore change nothing at all. His other three are
deactivated in the same statement that grants the new one, and the plan says so explicitly
because quietly removing an administrator's access is not something to discover later.

## 6. How it gets tested

Not by reading the diff. Against the running site, signed in as the auditor:

- a granted page opens
- a page outside the set is refused by URL, not merely missing from the menu
- a read PageMethod returns data
- a write PageMethod is refused
- a write `?action=` is refused
- the refusals appear in the log with the path and the reason
- an administrator is unaffected by all of the above

## 7. Residual risk, stated rather than hidden

- **Anything not routed through ASP.NET** bypasses the gate. `.ashx` handlers are covered
  (same pipeline); anything served by IIS directly is not.
- **A read PageMethod that writes.** The allowlist trusts the name. `GetX` that quietly
  updates a last-viewed timestamp would pass. The allowlist is built by reading each method,
  not by pattern alone, which limits this to methods that lie about themselves.
- **No database-level enforcement**, for the reason in section 2. If the institution ever
  needs a guarantee rather than a strong control, that is the work to do.
- **The session caches one role.** A user with `auditor` plus a writing role is not read-only
  unless `auditor` has the lowest id, so the grant must retire the others. That is why step 7
  is written the way it is.
