# Expenditure and Accounts: rebuild plan

Prepared 7 October 2026 by MIS, for the University Bursar and MIS. Read with `expenditure-accounts-audit.md`; defect IDs (D01...) refer to it.
Status: **implementation in progress**. Section 7 is the live checklist.

---

## 0. Principles and where this plan departs from the brief

1. **Present, do not change.**
   - Every report, check and warning is a `SELECT`, run under a read-only database account (`cd_gl_ro`).
   - No existing finance row is edited or deleted.
   - A **Fix** is always one of three things: a link to the right screen; an **adjusting entry**, which adds new lines with a reason, a preview, maker-checker approval and an audit row; or an **acknowledgement** with a reason.
2. **`AC6007` is not the receivables control account** (the brief assumed it was). In this ledger it is the Functional Fees **income** account (UGX 7.78B of credits). It is missing from the chart of accounts.
   - Student receivables have **no** control account. They live as subsidiary lines keyed by registration number (account_type Student, plus the faculty fee types).
   - The plan treats them that way. The trial balance shows the student subsidiary ledger as its own line, never as a hidden part of `AC6007`.
3. **The two fee stores are never added together.**
   - **Ledger reports use `fin_ledger` only.**
   - **Student fee positions use the canonical student balance** (`fin_student_balance_cache`, the dual-source de-duplicated figure already used by student statements).
   - Fee tracking (`fin_studentfeestracking`) is used on its own only for "billed twice" checks and for the receivables reconciliation, where it is shown as a separate column and never summed with the ledger.
4. **The trial balance basis is stated, not hidden.** The default basis is the **whole ledger**:
   - each general-ledger account (codes in the chart, plus provisionally mapped codes) is one line;
   - each subsidiary ledger family is one line;
   - the difference, UGX 4.03B today, is shown with its cause analysis (unbalanced vouchers by pattern), and the causes add up to the total.

   A second basis, **chart accounts only**, can be selected. That basis is out by 0.77B; its cause analysis is the same.
5. **Codes used but missing from the chart are mapped for presentation only**, in `gl_account_map`:
   - Each mapping is flagged *provisional* and suggested from history (e.g. AC6007 → Income / Functional Fees, AC6016 → Income / Retake Fees).
   - The Bursar confirms or changes each mapping.
   - Adding the codes to the real chart is a separate, approved step (open question Q2).
6. **Period close records a sign-off; it does not block posting.**
   - Posting goes through classic screens, stored procedures and the SchoolPay sweep. A real lock needs changes in those writers and the Bursar's agreement (Q5).
   - Until then, the period close wizard runs the checks, records who reviewed and signed off each month or year in `gl_period_signoff`, and the warnings flag any posting dated in a signed-off period.
7. **Year-end roll forward is proposed as an adjusting entry.**
   - None has ever been done.
   - The year-end step builds the closing entry (income and expense to retained earnings) as a **draft adjusting entry** for approval, rather than posting anything itself.
8. **Phase split, from the size of the problem.**
   - **Phase 1 (this work):** the read side, warnings, the adjusting-entry workflow and the period sign-off, plus emergency guards on three dangerous existing endpoints (D01-D03).
   - **Phase 2 (planned, not in this delivery):** replace the transactional screens. That means the voucher, journal, contra and requisition-to-payment posting workflow, a chart and supplier editor, budget entry and bank statement import. Those screens post money, and their replacement must be agreed with the Bursar screen by screen.

---

## 1. Target architecture

**Library:** `App_Code/Ledger/`. Pages are `NewScreens/Accounts*.aspx`.

| Class | Responsibility |
|---|---|
| `GlDb` | Two connections: read-only (`accountsReadOnlyConnectionString`, user `cd_gl_ro`) for every report, check and warning, and read-write (`accountsConnectionString`) used only by `GlWrite`. Parameterised helpers only. |
| `GlCore` | Slugs, access checks, the `GlApi` PageMethod wrapper (permission, anti-forgery token for writes, plain-English errors), JSON envelope, formatting (money with thousands separators, negatives in brackets everywhere, dates `d MMM yyyy`). |
| `GlCalc` | **The one calculation layer.** Classification of every ledger line into a *layer* and *key*. The account-by-month aggregate (cached 5 minutes, refreshable). Trial balance for any range or as-at date. Statement figures. Account and subsidiary balances. The voucher-level analysis used by the checks. |
| `GlChecks` | Checks per report: pass or fail, amount, cause, how to fix. The trial balance difference is decomposed so the causes add up to the total. |
| `GlWarnings` | The detection rules (section 5), run on demand and on a schedule. Keeps `gl_warning` state and history; health score. |
| `GlReports` | The report engine: definitions (parameters, columns, drill targets, checks) and a generic runner with paging, sorting and search. |
| `GlExport`, `GlPdf` | Excel (cover sheet with the parameters), CSV and PDF (crest, Bursar's office line, certification block). The same rows as the screen. |
| `GlWrite` | The only writer: adjusting entries (create, submit, approve, reject, post), acknowledgements, notes, assignments, saved filters, period sign-offs. Each write is one transaction with a `gl_audit` row. |

**JSON shape** (every PageMethod):

```
{ success: bool, message: "plain English" (on failure), denied: bool (permission),
  data: {...}, rows: [...], total: n, page: n, size: n, totals: {...},
  checks: [ { code, title, status: "pass|fail|info", amount, count, cause, fix: {kind, label, target} } ],
  basis: "sentence describing what the figures include", timings: { ms } }
```

**Line classification** (GlCalc, one SQL expression shared by every query):

| Layer | Rule | Trial balance key |
|---|---|---|
| GL | `accountcode` in `fin_subaccounts` | the code |
| GL (provisional) | code in `gl_account_map` | the code, flagged provisional |
| Students | account_type in (`Student`, `-`) | `SUB:STUDENTS` |
| Faculty fee lines | account_type in (`FOEFees`, `FSTEADFees`, `FBMFees`, `FSSAHFees`, `BursaryFees`) | `SUB:` + type |
| Suppliers | `Supplier` | `SUB:SUPPLIERS` |
| Salary advances, gratuity, sponsors | `Salary Advance`, `Gratuity`, `Sponsor` | `SUB:` + type |
| Unmapped | anything else (codes not in the chart and not mapped) | the code, flagged unmapped |

The amount is always `transaction_amount` (no line uses another currency).

**Caching:**
- The account-by-month aggregate (about 10,000 rows) is held in the application cache for 5 minutes.
- The cache key includes `COUNT(*)` and `MAX(TID)` of `fin_ledger`, read on each request (indexed, a few milliseconds), so new postings refresh it at once.
- In-place edits are caught by the 5-minute expiry and by the **Refresh figures** button.

---

## 2. New tables and migrations

All tables are new and in `campus_dynamics_accounts`, prefixed `gl_`; none is altered or dropped. Script: `COOPERP/sql/ledger/2026-10_gl_schema.sql`.
- **Back up first:** `mysqldump --no-data` of the schema, plus row counts of every `fin_*` table recorded in `gl_baseline`.
- The **read-only user** is created by `2026-10_gl_readonly_user.sql`: `cd_gl_ro` with `SELECT` on `campus_dynamics_accounts`, `campus_dynamics` and `campus_dynamics_portal`. The password is stored only in `web.config` as `accountsReadOnlyConnectionString`.

| Table | Purpose |
|---|---|
| `gl_settings` | key/value: warning schedule hours, large-posting threshold, health weights |
| `gl_account_map` | presentation mapping for codes missing from the chart: code, category, subcategory, name, provisional flag, confirmed by and when |
| `gl_warning` | one row per detected problem (rule + scope key): severity, status (OPEN, ACKNOWLEDGED, FIXED, REAPPEARED), count, amount, sample, cause, first detected, last seen, assigned to |
| `gl_warning_event` | append-only history per warning: detected, reappeared, fixed, acknowledged, assigned, note (who, when, text) |
| `gl_warning_run` | one row per detection run: when, by whom, duration, totals, health score |
| `gl_warning_trend` | per run, per rule: count and amount (the "is it falling" chart) |
| `gl_record_ack` | a single record accepted as known (e.g. one duplicate group): rule, record key, reason, who, when, active |
| `gl_saved_filter`, `gl_user_param` | named filters and last-used parameters per user and report |
| `gl_adjustment`, `gl_adjustment_line` | adjusting entries: number `ADJ/2026-27/0001`, status (DRAFT, PENDING, POSTED, REJECTED, CANCELLED), reason, purpose, link to a warning or voucher, entry date, maker, checker, posted voucher number; lines with code, layer, DR/CR, amount, narrative |
| `gl_period_signoff` | append-only: period (month `2026-09` or year `FY2025/26`), action (REVIEWED, SIGNED_OFF, REOPENED), checklist results as JSON, who, when, note |
| `gl_audit` | every write: entity, id, action, before and after JSON, reason, who, role, IP, when |
| `gl_baseline` | row counts and checksums of finance tables, taken before and after each deployment step |

**Guards:**
- triggers refuse DELETE on `gl_audit`, `gl_warning_event`, `gl_period_signoff`, `gl_adjustment_line` (once posted) and `gl_record_ack`;
- UPDATE is refused on `gl_audit` and `gl_warning_event`.

**Indexes proposed on existing tables (not applied without approval):**
- `fin_ledger (voucherNo, transactionType, transaction_amount)`, which turns the voucher analysis into an index scan.
- `fin_ledger (transactionDate, account_type)`.

Timings without them are recorded in section 8.

**Posting an adjusting entry** (the only write to finance tables):
- New `fin_ledger` rows with `source_system='GL_ADJUST'`, `RefNo` = the adjustment number, `folio` = the adjustment number, and one voucher number from `fin_NextVoucherNo(user)`.
- The existing BEFORE INSERT triggers (teller alias, DR/CR and amount guards) apply.
- Debit must equal credit, and the entry date must not fall in a signed-off period unless the approver records why.
- Row counts are asserted before and after: exactly the number of new lines.

---

## 3. Screens

All screens use `SidebarMaster`, share a header with tabs, and use the Fixed Assets and Graduation toolkit (`fa.css`, `fa.js`) plus `gl.css` and `gl.js`.

**Sidebar:** a new group **General Ledger** under Expenditure & Accounts, containing Accounts Dashboard, Reports, Finance Warnings (with count badge), Adjusting Entries and Periods. The existing items stay; the broken ones are labelled "classic" and proposed for retirement (audit, section 5).

| Screen | Purpose | Key interactions |
|---|---|---|
| **AccountsDashboard.aspx** | Figures at a glance, all from GlCalc | Tiles: cash and bank, receivables (canonical), payables, income and expenditure this year, surplus, trial balance difference, health score, open critical warnings. Charts: monthly income vs expenditure, expenditure by category, bank balances, warning trend. Every figure opens its report. |
| **AccountsReports.aspx** | The report engine | Report list by group; a parameter form built from the definition (cascading, validated, remembers the last values, saved filters); results table with server paging, sort and search; totals; **Checks** panel; drill-down; export dialog (PDF, Excel, CSV). |
| **AccountsAccount.aspx?code=** | Account card | Balance and movement by month; statement with running balance (paged); drill to vouchers; for a subsidiary family, the list of members (e.g. students) with balances. |
| **AccountsVoucher.aspx?v=** | Voucher | Every line with the same number, grouped by date and source; balance check; links to source (journal, fee bill or payment, requisition if any); any warnings on it; **Fix** opens the adjusting-entry wizard prefilled. |
| **AccountsWarnings.aspx** | Finance Warnings | Summary: health score, open critical, fixed this month, oldest open. Tabs: Open, Acknowledged, Fixed, History (trend). Each warning: what, where, count, amount, sample, cause, first and last seen, status. Actions: View records, Open screen, Start fix, Acknowledge with reason, Assign, Note. Run detection now. |
| **AccountsAdjustments.aspx** | Adjusting entries | List by status; **wizard**: (1) purpose, reason, linked warning or voucher, entry date; (2) lines with live DR = CR check and account search; (3) preview of the effect on each account and on the trial balance difference; (4) submit. A checker approves or rejects with a reason; approval posts. Maker cannot approve. |
| **AccountsPeriods.aspx** | Periods and close | Years and months with movement, trial balance and checks per period; **close wizard**: (1) choose the period; (2) automatic checklist (balanced vouchers, TB difference, unmapped codes, pending journals, control differences, postings after sign-off); (3) notes; (4) sign off. Year end: proposed closing entry, created as a draft adjusting entry. |

---

## 4. Report catalogue

**Common parameters:** financial year, date range or as-at date, account or range, layer.

**Every report has:** a Checks panel, drill-down, and PDF, Excel and CSV export.

| # | Report | Parameters | Columns | Checks |
|---|---|---|---|---|
| R01 | Trial Balance | as-at or range; basis (whole ledger, chart only); level (account, category); include zero | Code, Account, Category, Opening DR/CR, Movement DR/CR, Closing DR/CR | TB balances; difference decomposition (credit-only, debit-only, reused numbers, unequal two-sided, no voucher); unmapped codes; plug account balance |
| R02 | General Ledger and Account Statement | account (search), range | Date, Voucher, Particulars, Source, DR, CR, Running balance | lines on missing account; voucher of each line unbalanced |
| R03 | Journal and Voucher Listing | range, source, teller, balanced or not | Voucher, Date(s), Lines, DR, CR, Difference, Sources | unbalanced count and amount |
| R04 | Income and Expenditure | range; comparison range | Category, Account, Current, Comparison, Change | income and expense accounts not in the chart; balance on the unexpected side |
| R05 | Statement of Financial Position | as-at; comparison as-at | Section, Account, Amount, Comparison | Assets = Liabilities + Equity + current surplus, with the difference decomposed as R01 |
| R06 | Cash Flow (direct, from bank and cash accounts) | range | Inflows and outflows by counterpart category; opening and closing cash | movement = closing − opening; share of cash lines whose voucher has no counterpart ("unidentified") |
| R07 | Cash Book | bank or cash account, range | Date, Voucher, Particulars, Receipts, Payments, Balance | bank lines posted with type 'Bank' included; no statement data (reconciliation not possible) |
| R08 | Budget against Actual | financial year, category | Account, Budget, Actual, Variance, % used | "no budget recorded for the year" (fails today); accounts in use with no budget |
| R09 | Expenditure Analysis | range; by account, category, supplier, month | Group, Amount, Share | department and campus are not recorded on ledger lines (stated) |
| R10 | Payables Ageing and Supplier Statement | as-at; supplier | Supplier, Current, 31-60, 61-90, 90+, Total | supplier lines vs AC9021 trade payables |
| R11 | Receivables Ageing and Student Fees Position | as-at; programme; campus; status | Student, Programme, Billed, Paid, Balance, Age bucket (oldest unpaid bill) | canonical vs ledger vs tracking totals shown side by side (never summed) |
| R12 | Requisition to Payment Trace | range, status | Requisition, Amount, Each stage and date, Payment ref, Ledger voucher found? | approved not paid; marked "posted" with no ledger lines; payments with no requisition (count, amount) |
| R13 | Payment Voucher Register | range | Voucher, Date, Payee or account, Bank, Amount, Requisition | payments without a requisition |
| R14 | Period Close Summary | year or month | Period, Lines, DR, CR, Difference, Unbalanced vouchers, Sign-off | roll forward missing; postings after sign-off |
| R15 | Account Movement over Time | account or category, range | Account, then one column per month | none |
| R16 | Audit Trail | range, kind (edits, deletions, activity), user | When, Who, What, Account, Old, New, Amount | rows edited without actor; bulk deletions |
| R17 | Control Account Reconciliations | as-at | Control, Control balance, Subledger balance, Difference | difference per control |
| R18 | Unbalanced Vouchers | range, pattern | Voucher, Date, Pattern, DR, CR, Difference | totals equal the TB difference |
| R19 | Duplicate Postings | range | Account, Date, Amount, Particulars, Copies, Extra amount | acknowledged groups excluded |
| R20 | Chart of Accounts | none | Code, Name, Category, Subcategory, Lines, Balance, Status (in chart, provisional, unmapped, unused) | duplicates; unused; missing |

---

## 5. Warning rules

Severity: **C** Critical, **H** High, **M** Medium, **I** Information. All detection queries are `SELECT`s over the read-only connection. Detection runs:
- on demand (button);
- automatically when the warnings page or the dashboard opens and the last run is older than the scheduled interval (default 6 hours);
- from the existing in-process job pattern every 6 hours.

| Code | Rule | Sev | Detection | Likely cause shown | Fix |
|---|---|---|---|---|---|
| W01 | Unbalanced vouchers | C | vouchers with DR ≠ CR | posting wrote one side only or reused the number | View list (R18); open voucher; adjusting entry prefilled |
| W02 | Trial balance does not balance | C | whole-ledger DR − CR ≠ 0 | decomposition from W01 | R01 with checks |
| W03 | Lines on codes missing from the chart | C | Chart Account lines whose code is in neither `fin_subaccounts` nor `gl_account_map` | chart migration removed codes still in use | Map provisionally (Bursar); R20 |
| W04 | Lines with no voucher, zero or null amounts | M | voucherNo 0 or null; amount 0 | import without numbering | list; acknowledge |
| W05 | Voucher number reused | H | a number on more than one date or source | several number sequences | list; acknowledge |
| W06 | Year closed without roll forward | H | year marked Closed with no closing entry on retained earnings | close done as a flag only | Periods: year-end draft entry |
| W07 | Lines outside any financial year | H | date not inside any `fin_financial_years` range | year table incomplete | Q1; acknowledge |
| W08 | Posted into a closed or signed-off period | H | `timeLog` after the year's end + 30 days, or after a sign-off | late imports, migrations | list; acknowledge |
| W09 | Duplicate postings | M | identical account, type, amount, date, particulars | repeated import or double entry | R19; acknowledge per group; adjusting entry |
| W10 | Receivables bases disagree | C | canonical vs ledger student lines vs tracking | dual stores, missing mirrors | R17, R11 |
| W11 | Payables control differs from supplier lines | H | AC9021 vs Supplier lines | supplier postings skip the control | R17 |
| W12 | Requisitions approved but not paid, or flagged posted with no ledger lines | M / H | statuses vs `ledger_ref` lookup | workflow never posts | R12 |
| W13 | Payments without a requisition | I | bank or cash CR lines in expense vouchers with no requisition | requisitions not used | R13 |
| W14 | Balance on the unexpected side | M | asset or expense CR balance, liability, equity or income DR balance (contra accounts excluded by name) | misposting or missing entries | R02 |
| W15 | Students billed twice for the same item and period | H | tracking bills with the same regno, item, year and semester, net of reversals, more than once | double billing | student statement; billing screens |
| W16 | Backup and repair tables in the live schema | I | tables matching backup patterns | repairs left copies | acknowledge; MIS archive |
| W17 | Accounts in use with no budget | M | expense accounts with movement and no `fin_budget` row for the year | budgets not loaded | budget (phase 2) |
| W18 | Large or unusual postings | M | amount above account mean + 4 standard deviations, and over 10M | data entry error or genuine large item | list; acknowledge |
| W19 | Entries edited or deleted after posting | H | `edit_ledger` (last 90 days and all), `fin_deleted_ledger` | direct database maintenance | R16 |
| W20 | Plug or suspense accounts carrying a balance | H | `AC-RECONCILE-DIFF` and codes named suspense, difference | repair scripts | R02; adjusting entry |
| W21 | Year table inconsistent | M | year label not matching its dates; overlapping or missing ranges | manual edits | Q1 |
| W22 | Journals pending more than 30 days | M | `fin_journalnumbers` Pending | approval not done | classic journals |

**Health score** = 100 − (15 × open critical + 6 × open high + 2 × open medium), floor 0. Acknowledged warnings do not count.

---

## 6. Permissions

| Slug | Grants |
|---|---|
| `accounts.gl` | sidebar group |
| `accounts.gl.dashboard` | dashboard |
| `accounts.gl.reports` | reports, account and voucher pages, exports, saved filters |
| `accounts.gl.warnings` | warnings page (view) |
| `accounts.gl.warnings_manage` | acknowledge, assign, note, run detection, map codes provisionally |
| `accounts.gl.adjust` | create and submit adjusting entries |
| `accounts.gl.adjust_approve` | approve or reject (never one's own) |
| `accounts.gl.periods` | periods view |
| `accounts.gl.periods_manage` | review and sign off periods |

| Role | dashboard | reports | warnings | warnings_manage | adjust | adjust_approve | periods | periods_manage |
|---|---|---|---|---|---|---|---|---|
| bursar | yes | yes | yes | yes | yes | yes | yes | yes |
| accountant | yes | yes | yes | yes | yes | no | yes | no |
| finance_officer | yes | yes | yes | no | no | no | yes | no |
| auditor | yes | yes | yes | no | no | no | yes | no |
| vc | yes | yes | yes | no | no | no | yes | no |
| admin | wildcard |

---

## 7. Implementation checklist

Each item is ticked when done, with notes.

- [ ] 1. Back up: schema dump, row counts and checksums of all `fin_*` tables into `gl_baseline`.
- [ ] 2. Read-only user `cd_gl_ro` and connection string.
- [ ] 3. `gl_*` schema, guards, settings; provisional account map seeded from history.
- [ ] 4. Menu slugs and grants; sidebar group.
- [ ] 5. Emergency guards on D01 (GeneralLedger PageMethod), D02 (FixGLSync), D03 (JournalEntries delete).
- [ ] 6. `GlDb`, `GlCore`, `GlCalc` (classification, aggregate cache, TB, statement figures, voucher analysis).
- [ ] 7. `GlChecks` with the TB decomposition; `GlWarnings` with rules W01-W22, history and health score.
- [ ] 8. Report engine and R01 Trial Balance with the Checks panel; exports.
- [ ] 9. Reports R02-R20.
- [ ] 10. Account and voucher pages (drill targets).
- [ ] 11. Finance Warnings page.
- [ ] 12. Dashboard.
- [ ] 13. Adjusting entries: wizard, approval, posting.
- [ ] 14. Periods page and close wizard; year-end draft.
- [ ] 15. Test plan (section 8); fix; record results.
- [ ] 16. User guide; memory; commit.

---

## 8. Test plan

1. **No historical row changed.** Before and after every step, record row counts and `SUM(transaction_amount)` plus `CRC32` aggregate checksums over `fin_ledger`, `fin_studentfeestracking`, `fin_journalnumbers`, `fin_journal_details`, `fin_subaccounts` and `fin_mainaccounts`. They must match exactly, except for the test adjusting entry (exactly its lines, then removed by a documented reversal entry).
2. **One figure everywhere.** For a set of parameters, the trial balance totals, the dashboard tiles, the statement totals and the exported Excel totals must be equal. The cash on the dashboard must equal the sum of the cash-book closing balances. Receivables on the dashboard must equal R11's canonical total.
3. **Cause analysis adds up.** The sum of the R01 decomposition rows equals the R01 difference for the whole ledger, each year, and a random month.
4. **Read-only.** Every report method runs on `cd_gl_ro`; an attempted write under that user is refused by MySQL.
5. **Permissions.** Each PageMethod refuses without a session, without the slug, and (for writes) without the token. The maker cannot approve their own adjusting entry.
6. **Every write is audited.** Acknowledge, assign, note, map, saved filter, sign-off, adjusting-entry create, submit, approve, reject and post each produce exactly one `gl_audit` row (posting also produces the ledger lines).
7. **Performance.** Record timings for TB, voucher analysis and full detection (target: TB under 2 s cold, under 200 ms warm; detection under 60 s).
8. **Exports.** PDF, Excel and CSV row counts and totals equal the screen.
9. **Screens.** Every page at 1366 px and 768 px with no script errors; phone width viewable.

---

## 9. Open questions for MIS and the Bursar

| # | Question |
|---|---|
| Q1 | The year table: should id 4 be renamed 2025/2026 (dates 1 Aug 2025 to 31 Jul 2026), with 2026/2027 starting 1 Aug 2026? And what year should cover the 3,712 lines dated before 31 Jul 2024? |
| Q2 | May the 44 codes missing from the chart (AC6007 and others) be added back to the chart, using the provisional mapping once confirmed? |
| Q3 | Which receivables figure is authoritative for the balance sheet: the canonical student balance (4.21B) or the ledger student lines (8.39B)? A control account for student receivables is recommended. |
| Q4 | May the trial balance difference be corrected by adjusting entries, voucher by voucher, or should it be carried in a suspense account until the old vouchers are reviewed? |
| Q5 | Should a signed-off period block posting? This needs changes to the classic screens, the stored procedures and the SchoolPay sweep. |
| Q6 | Approval threshold for adjusting entries, and whether amounts above it need a second approver (the Bursar's memo response promised this). |
| Q7 | May the 96 backup and repair tables be moved out of the live schema into an archive schema? |
| Q8 | Retirement of the classic and FSR pages listed in the audit (section 5). |
| Q9 | Budgets for 2025/26 and 2026/27: who loads them, and by account or by vote and department? |
| Q10 | Phase 2 order: voucher and payment workflow first (it connects requisitions to the ledger), or bank reconciliation first? |
