# Expenditure and Accounts: audit

Prepared 7 October 2026 by MIS, for the University Bursar and MIS.
Scope: everything under **Expenditure & Accounts** in the eadmin sidebar, the code behind it, and the finance data as it stands today.
Method:
- read-only review of every page, class and stored procedure involved;
- read-only `SELECT` queries on `campus_dynamics_accounts` and `campus_dynamics`, run on 6 and 7 October 2026.

No finance row was changed. One scratch schema, `zz_fa_scratch`, holds copies of query results and will be dropped.

Companion document: `expenditure-accounts-plan.md`.

---

## 0. Summary

**The ledger cannot produce a trustworthy trial balance today**, and none of the screens tells the user so.

| What | Number |
|---|---|
| Total debits minus total credits, whole ledger | **UGX 4,030,430,035** (DR 118.11B, CR 114.08B) |
| Vouchers that do not balance | **41,660 of 102,640** (40.6%) |
| Vouchers with only debit or only credit lines | 32,150 |
| Voucher numbers reused across dates or sources | 14,559 numbers span more than one date |
| General-ledger lines on codes missing from the chart | 12,855 lines on 44 codes (includes `AC6007`, UGX 7.78B of income) |
| Posted bill lines edited in place | 5,142 rows on 14 Aug 2026 (+UGX 488.5M) |
| Ledger rows deleted directly on the live database in 2026 | **172,454 rows** (78,101 rows, UGX 220.1B on 12 Jun 2026 alone) |
| Possible duplicate postings | 802 groups, 1,438 extra lines, up to UGX 672.6M |
| Backup and repair tables in the live accounts schema | 96 tables, about 1.03 million rows |
| Receivables, four answers | 8.39B (student lines), 6.54B (fee tracking), 4.21B (canonical), 0.41B (receivable accounts) |
| Budget, periods, bank reconciliation data | none: all those tables are empty |
| Requisitions | 3 rows, none linked to any payment; every ledger payment has no requisition |

**Of the 22 sidebar items:**
- 8 do not compile or cannot run;
- 6 show wrong figures;
- 5 record controls that change nothing;
- the rest work with defects.

**None checks a permission on the server.**

**One endpoint posts to the ledger with no sign-in at all:** `GeneralLedger.aspx/SaveTransactionAjax`.

---

## 1. Inventory

**Legend.**
- **Kind:**
  - *New*: code written in the NewScreens style (`SidebarMaster`, code-behind, `FinanceDB`).
  - *FSR*: the April 2026 "Finance System Realignment" pages under `COOPERP/Finance/Admin` (new code on the old accounts master).
  - *Classic*: CALSI/legacy (`COOPERP/accounts`, user controls, TableAdapters).
- **Works?** *Yes*, *Partly* (runs, wrong figures or broken actions) or *No* (does not compile, wrong database, or every action fails).
- **Server-side permission:** none of the pages calls `RoleAccessService.RequireSlug`. The menu slugs only hide sidebar links; the FSR pages check five hard-coded role names.

| # | Sidebar item | Path | Slug (menu only) | Kind | Reads | Writes | Works? |
|---|---|---|---|---|---|---|---|
| 1 | Main Accounts Controller | NewScreens/MainAccountsController.aspx | accounts.ledgers.main_accounts | New | fin_mainaccounts, fin_subaccounts | SP MainAccountEditor, DeleteMainAccount | Partly |
| 2 | Sub Accounts Controller | NewScreens/SubAccountsController.aspx | accounts.ledgers.sub_accounts | New | fin_subaccounts, fin_ledger (per-row balance) | SP AccountEditor, fn fin_NextAccountCode | Partly |
| 3 | Ledger Categories | NewScreens/LedgerCategories.aspx | accounts.ledgers.categories | New | fin_ledgertypes | SP fin_LedgerCategoryEditor, fin_DeleteLedgerCategory | No (every write throws) |
| 4 | General Ledger | NewScreens/GeneralLedger.aspx | accounts.ledgers.general_ledger | New | fin_ledger, fin_subaccounts | fin_journalnumbers, **direct INSERT fin_ledger** via unauthenticated PageMethod | Partly, unsafe |
| 5 | Supplier Management | NewScreens/SupplierManagement.aspx | accounts.ledgers.suppliers | New | supplier | INSERT/UPDATE/**hard DELETE** supplier | Partly |
| 6 | Journal Entries | NewScreens/JournalEntries.aspx | accounts.transactions.journal_entries | New | fin_journalnumbers, fin_journal_details | SP fin_AddJournalDetails, fin_ApproveJournal_Safe, **fin_delete_journal_item (can delete a posted ledger row)** | No |
| 7 | Payment Vouchers | NewScreens/PaymentVouchers.aspx | accounts.transactions.payment_vouchers | New | fin_vouchernumbers, fin_voucher | SP fin_GetLatestVoucherNo, fin_VoucherCreator, fin_ApproveVoucher | No |
| 8 | Contra Vouchers | NewScreens/ContraVouchers.aspx | accounts.transactions.contra_vouchers | New | fin_vouchernumbers | SP fin_TransactionCreator (**direct to fin_ledger, no approval**) | Partly |
| 9 | Finance Dashboard | NewScreens/FinanceDashboard.aspx | accounts.control.finance_dashboard | New | fin_ledger, fin_journalnumbers | none | Partly |
| 10 | Financial Periods | NewScreens/FinancialPeriods.aspx | accounts.control.financial_periods | New | fin_financial_years | FinancePeriod.Add/Open/Close/Delete | **No (does not compile)** |
| 11 | Audit Trail | NewScreens/FinanceAuditTrail.aspx | accounts.control.audit_trail | New | acc_activity_log, fin_repair_log | none | **No (wrong columns, throws)** |
| 12 | Transaction Batch Monitor | Finance/Admin/TransactionBatchMonitor.aspx | accounts.control.batch_monitor | FSR | fin_transaction_batch (in campus_dynamics) | none | No (wrong database) |
| 13 | Double-Entry Validation | Finance/Admin/DoubleEntryValidation.aspx | accounts.control.double_entry | FSR | fin_posting_rules | toggles rules nothing reads | No |
| 14 | Accounting Period Management | Finance/Admin/PeriodManagement.aspx | accounts.control.period_management | FSR | fin_accounting_periods | flags nothing reads | No |
| 15 | Period Close Management | Finance/Admin/PeriodClose.aspx | accounts.control.period_close | FSR | as above | sp_MonthEndClose (parameter mismatch) | **No (does not compile)** |
| 16 | Reversal and Correction Approvals | Finance/Admin/ReversalApprovals.aspx | accounts.control.reversal_approvals | FSR | fin_transaction_reversal | status flag only, posts nothing | No |
| 17 | Transaction Audit Trail | Finance/Admin/TransactionAuditTrail.aspx | accounts.control.audit_trail_tx | FSR | fin_transaction_log (JSON columns, cannot exist on MySQL 5.6) | none | **No (does not compile)** |
| 18 | Bank Reconciliation Import | Finance/Admin/BankReconciliationImport.aspx | accounts.control.bank_reco_import | FSR | fin_reco_bank_statement_import | header row only; statement lines are discarded | No |
| 19 | Bank Reconciliation Matching | Finance/Admin/BankRecoMatching.aspx | accounts.control.bank_reco_match | FSR | non-existent columns | UPDATE fin_ledger flags | **No (does not compile)** |
| 20 | Chart of Accounts Lifecycle | Finance/Admin/AccountManagement.aspx | accounts.control.account_lifecycle | FSR | fin_subaccounts (wrong database) | is_active flag that does not exist | No |
| 21 | Initiate Reversal Request | Finance/Admin/ReversalRequest.aspx | accounts.control.reversal_request | FSR | fin_ledger with guessed columns | fin_transaction_reversal | No |
| 22 | Initiate Correction Request | Finance/Admin/CorrectionRequest.aspx | accounts.control.correction_request | FSR | as above (hard-coded `ledger_id`) | fin_transaction_reversal | No |
| 23 | Trial Balance | NewScreens/TrialBalance.aspx | accounts.reports.trial_balance | New | SP fin_TrialBalance | none | Partly (excludes the student ledger; period movements only) |
| 24 | Income Statement | NewScreens/IncomeStatement.aspx | accounts.reports.income_statement | New | SP fin_IncomeStatement | none | **Partly (totals counted twice)** |
| 25 | Balance Sheet | NewScreens/BalanceSheet.aspx | accounts.reports.balance_sheet | New | SP fin_BalanceSheet | none | **Partly (asset sections empty)** |

**Related screens that are not in the sidebar but belong to this section:**

| Path | Kind | Works? | Note |
|---|---|---|---|
| NewScreens/CashBook.aspx | New | **No** | Master page does not exist; does not compile. |
| NewScreens/BankReconciliation.aspx | New | **No** | Same as CashBook; duplicates FSR 18 and 19. |
| NewScreens/BudgetManager.aspx | New | Partly | Actuals are never calculated; `fin_budget` is empty. |
| NewScreens/ChartOfAccounts.aspx | New | n/a | Stub that redirects to Main Accounts. |
| NewScreens/FixGLSync.aspx | New | Runs | **Danger:** no master, no sign-in check; bulk INSERTs into and UPDATEs fin_ledger. |
| Requisitions: NewScreens/RequisitionsController, BursarRequisitions, FinanceRequisitions, RequisitionDetail; portal RequisitionForm, SupervisorRequisitions, MyRequisitions | New | Partly | Never post to the ledger; "Paid" and "Posted to GL" are flags. |
| COOPERP/accounts/ (41 pages: PaymentVoucher, StudentReceipt, CreateJournal, ViewJournal, NightAudit, ...) and UserControls/Accounts, UserControls/Inventory (SupplierLedger, PurchaseOrders, SchoolRequisitions) | Classic | Mixed | Still the main way money is posted. Out of the sidebar; reached from the old accounts master. |

**Shared code:**
- App_Code/Finance: `FinanceDB`, `FinancePeriod`, `FinanceLogger`, `AccountCache`, `MoneyHelper`, `PaginationHelper` (dead), `BillingReconciliationJob`.
- `App_Code/FinanceEngine.cs` (student balance).
- `App_Code/FinanceSystemRealignmentHelper.cs` (FSR pages; contains a hard-coded fallback connection string with a password).
- SQL: about 30 stored procedures in `campus_dynamics_accounts` (dump in `COOPERP/sql/_backup_routines_20260626.sql`).
- Events: `ev_schoolpay_autosweep` (every 10 min), `evt_daily_balance_check` (daily).

---

## 2. Defects register

**Severity:**
- **C** Critical: figures are wrong, or money can move without control.
- **H** High.
- **M** Medium.
- **L** Low.

Evidence is file:line or a query result.

| ID | Sev | Where | Defect | Evidence | Recommended fix |
|---|---|---|---|---|---|
| D01 | C | GeneralLedger.aspx.cs:120-299 | `[WebMethod] SaveTransactionAjax` inserts DR and CR rows into `fin_ledger` with no sign-in or permission check; the actor falls back to "SYSTEM". | Method body; PageMethods bypass the master's session check. | Require a session and the slug; better, retire quick posting and route through the adjusting-entry workflow. |
| D02 | C | FixGLSync.aspx(.cs) | Deployed admin tool, no master and no sign-in. Bulk-inserts ledger rows and mass-UPDATEs `fin_ledger.account_type`. Hard-codes a regno; echoes stack traces. | cs:95-149, :128-134 | Disable now (refuse unless the user is a signed-in administrator and an explicit switch is set); propose deletion. |
| D03 | C | JournalEntries.aspx.cs + SP fin_delete_journal_item | Delete passes a `fin_journal_details.TID`, but the SP deletes `fin_ledger WHERE TID=_id` when the journal is not Pending. That is a different ID space, so an unrelated posted ledger row can be deleted. | SP body; Delete button always shown (.aspx:289) | Hide and refuse Delete unless the journal is Pending; never call the SP for posted journals. |
| D04 | C | Ledger data | Trial balance out by **UGX 4,030,430,035**: 2024/25 +6.61B, 2025/26 −2.25B, before any year −0.33B. | Section 4.2 | Present it with its cause analysis; corrections by approved adjusting entries only. |
| D05 | C | Ledger data | 41,660 unbalanced vouchers. They are credit-only (12,869, −5.91B), debit-only (19,281, +9.69B), reused numbers (9,378, +0.29B net, 3.38B gross) or unequal two-sided (132, −0.05B). | zz_fa_scratch.vb | Same as D04. |
| D06 | C | fin_subaccounts | `AC6007` (Functional Fees income, UGX 7.78B CR) and 43 other codes used in 12,855 general-ledger lines are missing from the chart. Every report joining the chart drops them. | Section 4.5 | Add them back to the chart (an additive chart change, approved by the Bursar); meanwhile report them as "unmapped". |
| D07 | C | Ledger data | Receivables have four different values: 8.39B, 6.54B, 4.21B, 0.41B. There is no control account for student receivables, and none of the fee subledgers is mirrored into its control account. | Section 4.6 | Present the canonical figure and show the others as reconciling views with the differences explained. |
| D08 | C | PaymentVouchers.aspx.cs + SPs | `fin_ApproveVoucher` calls `fin_NextVoucherNo()` with no argument (the function needs one), so every approval fails. `fin_GetLatestVoucherNo(...,'Payment')` reuses a teller's first number, so later vouchers are silently dropped. The UI still says "created". | routines:8364, 16239 | Replace with the new voucher workflow; mark the screen classic. |
| D09 | C | Requisitions | "Paid" and "Posted to GL" set flags only and invent a reference `GL-yyyymmdd-id`; no ledger entry is created. Routing to the VC or Procurement is a dead end. | FinanceRequisitions.aspx.cs:258-312 | Trace report shows the break; real link requires the posting workflow (plan, phase 2). |
| D10 | C | Ledger data | 172,454 ledger rows deleted directly on the live database in 2026 (UGX 220.1B on 12 Jun alone), plus 5,142 posted bill lines edited in place on 14 Aug 2026. | fin_deleted_ledger, edit_ledger | Report and warn; stop direct edits by policy (plan, section 9). |
| D11 | H | IncomeStatement.aspx.cs:81-109 | Section header rows (TOTAL REVENUE, NET INCOME, ...) are added to the totals again; data rows are classified by a DR>CR guess. | Code | Rebuilt on the shared calculation. |
| D12 | H | SP fin_BalanceSheet | Filters subcategories ('Current Assets', ...) that do not exist in the chart, so asset sections are empty. Typos in the totals; header rows are counted twice by the C#. | SP; chart values | Rebuilt on the shared calculation. |
| D13 | H | SP fin_TrialBalance | Period movement only (no opening balances), excludes the 80,731-line Student subledger and restored rows, and returns `FORMAT()` strings. | SP | Rebuilt; basis stated on screen. |
| D14 | H | Seven different balance formulas | TB SP, `fin_GetPeriodBalance` (counts the opening twice for type Opening), `fin_GetSurplusDeficit`, the dashboard and GL raw sums, SubAccounts direct-only, CashBook Chart-only, and FinanceEngine dual-source. They use different amount columns, scopes and exclusions. | Audit agent report, section 2 | One calculation layer (plan, section 1). |
| D15 | H | FinancialPeriods.aspx.cs:65,104,111,134 | Does not compile (signature mismatch with FinancePeriod). | Code | Replaced by the new period view. |
| D16 | H | fin_financial_years | Year id 4 is labelled "2026/2027" but runs 1 Aug 2025 to 30 Oct 2026. No year covers 3,712 lines (1.23B) dated before 31 Jul 2024. | Query | Bursar to confirm the year table (open question); reports label years by date range. |
| D17 | H | All finance pages | No server-side permission checks. Initial page queries run before the master's session redirect. | Section 0 of the code audit | Every new method checks a slug. |
| D18 | H | FinanceAuditTrail.aspx.cs:74-79 | Selects columns that do not exist in `acc_activity_log` and `fin_repair_log`; falls back to an unfiltered `SELECT *`; then throws on bind. | Code | New audit-trail report. |
| D19 | H | FSR pages (12-22) | Wrong database (`campus_dynamics`); four pages do not compile; audit table cannot exist on MySQL 5.6; controls change nothing. | FSR audit | Retire; replaced by the new section. |
| D20 | H | JournalEntries.aspx.cs:220-222, :309, :364 | Statuses New/Approved never occur (the data uses Pending, Posted, Void). The amount is passed in the refNo parameter, so lines are saved as zero. The approval SP is called with wrong parameter names. | Code; 3,030 pending/void journals | Classic; the adjusting-entry wizard replaces manual journals in this section. |
| D21 | H | Edits | `edit_ledger` has 4,858 rows with no actor and no date (before the August 2026 trigger repair). | Query | Audit report shows them as "actor unknown". |
| D22 | H | Bank | CashBook and BankReconciliation do not compile. Statement lines are never stored. Supplier bank payments are posted with account_type 'Bank' and are excluded from the cash book. | Code | New cash book report on the shared calculation (all line types). |
| D23 | H | Payables | Supplier lines total 261.8M owed, but `AC9021 Trade Payables` holds a **debit** balance of 87.7M. Two unrelated supplier tables exist (`supplier` 3 rows, `inv_supplierdetails` 98 rows). | Query | Control reconciliation report and warning. |
| D24 | H | Budget | `fin_budget` is empty; actuals are never computed; categories do not match ("Expenditure" vs "Expense"); "All categories" initialises nothing. | Code; counts | Budget against actual report states "no budget recorded" until budgets are loaded. |
| D25 | M | FinanceLogger usage | `LogAction(page, action, details)` puts the details into `user_id` char(20); in strict mode the insert fails silently, so most screen writes have no audit row. | Zero rows for those page names | New code writes its own audit table. |
| D26 | M | FinanceDashboard.aspx.cs:50,103 | "Unposted" counts status 'New' (always 0; 181 are Pending). The unbalanced-voucher count is capped at 1,000 (actual 41,660). | Code; query | New dashboard. |
| D27 | M | MainAccountsController.aspx:110-114 | Offers "Asset"/"Liability", while the data uses "Assets"/"Liabilities". Saving writes the singular form and breaks the balance-sheet logic. Delete orphans sub-accounts. | Code; data | Classic; propose replacement in phase 2 with a validated chart editor. |
| D28 | M | LedgerCategories.aspx.cs:101-150 | Stored-procedure parameter names do not match, so every write throws. Message not HTML-encoded. | Code | Classic. |
| D29 | M | Chart | 40 sub-accounts never used. Duplicate names (E-LIBRARY COST, GRADUATION EXPENSES, LIBRARY BOOKS). No active flag. No control-account flag. | Query | Chart quality report and warnings. |
| D30 | M | Balances | Retained earnings AC7008 holds a debit balance of 3.49B. Deferred tax AC9014 (an asset) holds a credit of 0.58B. AC1321 (loan account under cash) holds a credit of 10.1M. | Query | Warning "balance on the unexpected side" (contra accounts such as accumulated depreciation excluded). |
| D31 | M | Ledger data | 802 groups of identical lines (same account, type, amount, date, particulars), 1,438 extra lines, up to UGX 672.6M. | Query | Duplicate-postings warning with review list; some are genuine. |
| D32 | M | Ledger data | Plug account `AC-RECONCILE-DIFF` (not in the chart) holds 16 credits of 1.61B and 33 debits of 20.3M from repairs on 17 Mar 2026 ("E2 repair", "E6-fix"). | Query | Show as its own line in every report and as a warning. |
| D33 | M | Schema | 96 backup and repair tables (about 1.03M rows) in the live accounts schema. | information_schema | Propose moving them to an archive schema (Bursar and MIS decision). |
| D34 | M | Voucher numbering | At least four number sequences write into `fin_ledger.voucherNo`; 17,584 numbers span more than one source. | Query | New entries use one sequence; reports group by voucher **and** date. |
| D35 | M | Ledger data | 33,450 lines dated in the closed 2024/25 year were recorded after 31 Aug 2025 (125.9B, mostly migrations and restores). | Query | Warning "posted into a closed year". |
| D36 | L | SupplierManagement.aspx.cs:127 | Hard delete with no check for ledger use. | Code | Classic. |
| D37 | L | Hard-coded credentials | Fallback connection strings with passwords in FinanceSystemRealignmentHelper.cs:119 and SidebarMaster.master.cs:10-12; root password in FINANCE_UI_PLAN.md. | Code | Remove fallbacks (MIS). |
| D38 | L | FSR helper | Stale duplicate copy at COOPERP/App_Code/FinanceSystemRealignmentHelper.cs. | Files | Retire with FSR. |

---

## 3. Missing features

1. A trial balance that covers the whole ledger, with opening, movement and closing figures, and that says when and why it does not balance.
2. Income and expenditure statement, statement of financial position and cash flow statement built on one calculation.
3. Account statement and general ledger with drill-down from a total to its lines to the voucher.
4. Cash book per bank or cash account covering every line type; bank reconciliation that stores statement lines.
5. Budget data and budget against actual.
6. Payables and receivables ageing; supplier statement; student fees position from one source.
7. Requisition to payment trace, and a payment voucher register.
8. Period status, period close checklist and close summary; year-end roll forward (none has ever been done).
9. Control account reconciliations (students, faculty fee subledgers, suppliers, salary advances).
10. A warnings page that finds problems and tracks them until fixed or acknowledged.
11. Adjusting entries with a reason, a preview, maker-checker approval and an audit row.
12. Server-side permissions and a full audit of every write.
13. Exports (PDF, Excel, CSV) that match the screen.

---

## 4. Data health report (7 October 2026)

### 4.1 Tables and ranges

| Table | Rows | Range or note |
|---|---|---|
| fin_ledger | 187,371 | 27 Feb 2024 to 6 Oct 2026; 2024: 37,682, 2025: 75,138, 2026: 74,551 |
| fin_studentfeestracking | 75,808 | student bills and payments (never added to fin_ledger in any figure) |
| fin_journalnumbers / fin_journal_details | 10,710 / 19,058 | journals: 8,633 Posted, 2,849 Void, 181 Pending |
| fin_deleted_ledger | 179,727 | rows deleted from fin_ledger, 30 Mar to 6 Oct 2026 |
| edit_ledger | 10,196 | edits to posted rows, 10,156 rows; dated 14 Aug to 5 Oct 2026, 4,858 undated |
| fin_mainaccounts / fin_subaccounts | 18 / 199 | two-level chart |
| fin_financial_years | 2 | see D16 |
| fin_budget, fin_financial_periods, accountingperiod, fin_voucher, fin_vouchernumbers, fin_reconciliationstatement, fin_reco_bank_entries | 0 each | never used |
| supplier / inv_supplierdetails | 3 / 98 | two supplier lists |
| campus_dynamics.sys_requisitions | 3 | test data |

### 4.2 Trial balance

| Scope | Debit | Credit | Difference |
|---|---|---|---|
| Before 31 Jul 2024 (no year) | 454,409,170 | 780,531,661 | −326,122,491 |
| 2024/25 (31 Jul 2024 to 31 Jul 2025) | 73,355,239,184 | 66,749,968,350 | 6,605,270,834 |
| 2025/26 (1 Aug 2025 to 30 Oct 2026, labelled 2026/2027) | 44,298,499,206 | 46,547,217,514 | −2,248,718,308 |
| **Whole ledger** | **118,108,147,560** | **114,077,717,525** | **4,030,430,035** |

The whole-ledger difference splits into vouchered lines, 4,026,480,035, and 7 lines with no voucher, 3,950,000.

**By layer:**

| Layer | Net (DR − CR) |
|---|---|
| General-ledger accounts in the chart (Chart Account) | 773,153,123 (on its own the general-ledger layer does not balance) |
| Student lines (types Student, '-', BursaryFees, FBMFees, FOEFees, FSSAHFees, FSTEADFees) | 8,394,319,831 (Student type) and −4,814,382,622 (fee-type lines), shown separately |
| Supplier / Salary Advance / Gratuity / Sponsor / Bank / Expense types | −261.8M / +25.2M / −2.0M / −0.68M / −315.7M / +232.3M |

### 4.3 Cause analysis of the difference

The difference equals the sum of the unbalanced vouchers. Each cause below adds up to the total.

| Cause | Vouchers | Net effect |
|---|---|---|
| Credit-only vouchers | 12,869 | −5,905,730,080 |
| Debit-only vouchers | 19,281 | +9,692,277,816 |
| Number reused across dates or sources | 9,378 | +289,677,299 |
| Two-sided, amounts differ | 132 | −49,745,000 |
| **Total** | **41,660** | **+4,026,480,035** |

**By source system (net effect of unbalanced vouchers):**
- Manual −5.62B
- Billing +4.98B
- SB_COLLECTIONS +4.71B
- (none) −0.04B
- RSL_GL_SIDE −0.01B

The pattern is clear. Student billing posts the student debit without the income credit in the same voucher, or the other way round. Collections post the bank and student sides under different voucher numbers.

### 4.4 Periods and roll forward

- There is one opening balance, dated 31 Jul 2024: 55 lines, balanced, 35.89B each side.
- 2024/25 is marked Closed, but **no closing entries were posted**. Income and expense were never moved to retained earnings, and no opening entries exist for 2025/26.
- 33,450 lines (125.9B) dated in 2024/25 were recorded after 31 Aug 2025.
- No accounting periods (months) exist, and nothing blocks posting into a closed year.

### 4.5 Chart of accounts

- 18 main accounts (categories Assets, Expense, Income, Equity, Liabilities) and 199 sub-accounts (193 Basic, 7 Collection).
- 40 sub-accounts are never used. Three names are duplicated.
- **44 codes are used but missing from the chart (12,855 lines).** The largest:
  - AC6007 Functional Fees income: 10,554 lines, CR 7.78B. It was a salary payable account in the old chart.
  - AC6016: 567 lines (retake fees).
  - AC6034: 560 lines (graduation fees).
  - AC6004: 549 lines (late registration).
  - AC6037: 294 lines.
  - AC6032: 133 lines.
  - AC-RECONCILE-DIFF: 49 lines.
  - 110/011: 10 lines.
  - AC1007: 10 lines, 333M DR.
- **Balances on the unexpected side** (contra accounts such as accumulated depreciation excluded):
  - Retained earnings AC7008: DR 3.49B.
  - Deferred tax AC9014: CR 0.58B.
  - Trade payables AC9021: DR 87.7M.
  - Loan account AC1321: CR 10.1M.

### 4.6 Control accounts against subledgers

| Control | Control balance | Subledger | Difference |
|---|---|---|---|
| Student receivables | no control account (AC1101 group holds 407.4M) | 8,394.3M (student lines) / 6,542.1M (fee tracking, never to be added to the ledger) / 4,212.7M (canonical cache, refreshed 7 Oct 00:18) | not reconcilable until a control basis is chosen |
| Faculty fee collection accounts AC1323-AC1326 | general-ledger lines on those codes | fee-type lines FOEFees/FSTEADFees/FBMFees/FSSAHFees | shown per account in the reconciliation report |
| Trade payables AC9021 | −87.7M (debit) | 261.8M owed on supplier lines | 349.5M |
| Salary advances AC1102 | ledger balance | 25.2M on Salary Advance lines | shown in the report |

Bank and cash balances: AC1301 Centenary operations 2.60B, AC1302 Stanbic collection 1.10B, AC1303 Centenary collection 0.38B, petty cash 0.07B. None can be reconciled, because no bank statement data has ever been stored.

### 4.7 Expenditure chain

- Requisitions: 3 rows, all test data, none linked to a voucher.
- Payment vouchers (`fin_voucher`): 0 rows.
- Expense postings in the ledger come from classic journals (`fin_ApproveJournal_Safe`), quick posting, supplier ledger postings and imports. **None references a requisition.**
- So the chain breaks at the first link: approved spending and paid spending are never connected.

### 4.8 Other findings

- Duplicate postings: 802 groups, 1,438 extra lines, up to UGX 672.6M (some will be genuine repeats).
- Large postings of 50M or more: 320 lines, 52.3B. Mostly bank transfers; the unusual ones need a statistical rule (plan, section 5).
- Foreign currency: no lines in a currency other than UGX.
- Lines dated in the future: none. Zero amounts: 29. No voucher: 7.
- Deleted rows:

  | Date | By | Rows | Amount |
  |---|---|---|---|
  | 12 Jun 2026 | dbmanager | 78,101 | 220.1B |
  | 7 Aug 2026 | root | 31,360 | 13.6B |
  | 1 Apr 2026 | dbmanager | 30,220 | 16.9B |
  | 21 Jul 2026 | dbmanager | 21,179 | 7.6B |
  | 25 Jun 2026 | dbmanager | 7,418 | 4.7B |
  | undated | unknown | 7,273 | 24.7B |

- Edits: the functional-fee increase of 14 Aug 2026 edited 5,142 posted bill lines in place (687,000 to 782,000 each, +488.5M). 148 more edits on 3 Sep 2026 (−64.0M).

---

## 5. Classic versus new, and what can retire

| Classic or broken item | Replaced by (new) | Retire after go-live? |
|---|---|---|
| Finance Dashboard (9) | Accounts Dashboard | Yes |
| Trial Balance (23), Income Statement (24), Balance Sheet (25) and their SPs fin_TrialBalance, fin_IncomeStatement, fin_BalanceSheet | Report engine: Trial Balance, Income and Expenditure, Financial Position | Yes (keep SPs until other callers, e.g. xtraReportCentre, are moved) |
| Financial Periods (10) | Periods report and close summary (read-only); period status table | Yes |
| Audit Trail (11), Transaction Audit Trail (17) | Audit trail report (edit_ledger, fin_deleted_ledger, activity log) | Yes |
| FSR pages 12-22 and FinanceSystemRealignmentHelper | Warnings page, control reconciliations, adjusting entries with approval | Yes (all eleven) |
| CashBook, BankReconciliation (not in sidebar) | Cash book report; bank reconciliation (phase 2) | Yes |
| FixGLSync | nothing (one-off repair tool) | Yes, now |
| General Ledger (4) quick posting | Adjusting entry wizard | Posting: yes. Listing: replaced by the General Ledger report. |
| Journal Entries (6), Payment Vouchers (7), Contra Vouchers (8) | Phase 2: voucher workflow on the new service layer. Until then, classic COOPERP/accounts journals remain the posting route. | Not yet |
| Main/Sub Accounts, Ledger Categories (1-3), Supplier Management (5), BudgetManager | Phase 2: chart, supplier and budget editors on the new layer | Not yet |

No classic file is deleted by this work. Retirement needs the Bursar's and MIS's approval.
