# Fixed Assets module: plan

Prepared 6 October 2026 by MIS for the University Bursar and MIS review.
Status: **Phase 1, awaiting approval.** No application code has been written.

Scope: a new Fixed Assets module in eadmin with five screens (Assets Dashboard, Asset Categories, Assets, Asset Records, Reports), its database, depreciation engine, permissions, reports, import and tests.

---

## 0. Summary of decisions

These are the decisions that refine or correct the brief. Each is explained in the section named.

| # | Decision | Why | Section |
|---|---|---|---|
| D1 | The financial year runs **1 August to 31 July**. | The finance journals post depreciation "for the year ended 31 July 2025", and `fin_financial_years` uses August to July. | 5.1 |
| D2 | Depreciation is **monthly pro-rata**. The month of acquisition counts as a full month and the month of disposal is not charged. | This reproduces the University's own 2024/25 journals to the shilling (Toyota Coaster, engineering equipment, both leasehold lands). | 5.3 |
| D3 | Depreciation is **posted annually at 31 July by default**. Monthly or to-date runs are allowed for management accounts. | This matches current practice, and the guard in 5.6 makes mixed run frequencies safe. | 5.4 |
| D4 | Version 1 keeps the register and **does not post to `fin_ledger` automatically**. It produces a depreciation journal summary per GL account and a register-to-GL reconciliation report. | The accountants already post year-end journals by hand. Automatic posting would double-count unless that practice changes. This is open question Q3. | 5.9, 7 |
| D5 | "Assets with no asset number" becomes **"Assets not yet tagged"**. | The brief makes the asset number required and auto-generated, so it can never be empty. The real gap is a missing physical tag. | 4.1 |
| D6 | "Not revalued in more than twelve months" applies **only to categories on the revaluation model** (Land and Buildings by default), each with its own interval. | Computers and furniture are carried at cost and never revalued. Listing them would bury the real items. | 4.1 |
| D7 | Five record types are added to the brief's eight: **Opening, Verification, Change of estimate, Void and Reversal**. | Opening loads assets bought before go-live. Verification records the physical count. Change of estimate makes life or method changes visible in the ledger. Void handles assets entered in error. Reversal is the only way to correct an append-only ledger. | 2.4 |
| D8 | A third depreciation method, **Not depreciated**, is added. | Freehold land is not depreciated. | 5.2 |
| D9 | A category, **Plant and Machinery**, is added to the brief's list. | The University owns generators, water tanks, solar systems and an engraving machine. The legacy `assetcategory` table already has MACHINERY. | 3 |
| D10 | The module gets **its own front-end toolkit and export classes** (`fa.js`, `fa.css`, `FaExport`, `FaPdf`), adapted from the Graduation ones rather than linked to them. | `graduation.js`, `GraduationExport` and `GraduationPdf` hard-code Graduation wording ("Office of the Academic Registrar", "Chairperson, Senate", "candidates"). Editing them risks the Graduation module, which is in live use. | 1, 4 |
| D11 | New tables live in **`campus_dynamics`** with the prefix `fa_`. The legacy tables in `campus_dynamics_accounts` are left untouched. | Most joins are to staff, departments, campuses, RBAC and the activity log, which are all in `campus_dynamics`. The legacy tables are empty or hold names only, and no code uses them. | 1, 2 |
| D12 | The database refuses deletes and value edits through **guard triggers**, not only the application. | Safety rules 1 and 4 then hold even for someone with a SQL client. | 2.9 |
| D13 | **Attachments are stored under `App_Data`** and served by a handler that checks permission. | Files under `Data_Uploads` are served to anyone with the link, and invoices and valuation reports should not be. | 2.7, 4.6 |
| D14 | The module is a **parent item "Fixed Assets" under the Expenditure & Accounts heading**. | This is how every other module with sub-screens appears (Payroll, Accounts & Ledgers). | 4, 6 |
| D15 | A new role, **`assets_officer`**, is added for Estates and Stores staff. | No existing role fits the people who keep the register day to day. | 6 |

---

## 1. What exists today

### 1.1 Asset data already in the system

| Item | Where | What it holds | Use in this module |
|---|---|---|---|
| Legacy asset tables | `campus_dynamics_accounts`: `fixedassetregister` (0 rows), `assetcategory` (9 rows), `depreciationrecords` (0), `assetlocations` (0), `fin_assetlocations` (0) | `assetcategory` holds 9 names: BUILDINGS, COMPUTERS, PRINTERS, MACHINERY, LIBRARY STOCK, MOTOR VEHICLE, FURNITURE & FITTINGS, EQUIPMENT, LAND. The rest are empty. | Names are mapped into the new categories (section 3.3). The tables are left as they are. |
| Legacy routines | `fin_Depreciator`, `fin_GetDepreciation`, `fin_fixedAssetEditor`, `fin_DeleteAsset` and 6 others | Monthly per-asset depreciation posted straight to the GL. No C# or ASPX code calls them. | Not used. `fin_DeleteAsset` deletes rows, which breaks rule 1. |
| GL fixed-asset accounts | `fin_subaccounts` under `AC8004 NON CURRENT ASSET A/C` | Cost and accumulated depreciation per class (AC8016 to AC8042), expense accounts AC2161, AC2162 and AC2163, and the revaluation reserve AC7009. | Each category maps to a cost, an accumulated-depreciation and an expense account. Used for the journal summary and reconciliation. |
| GL balances | `fin_ledger` | Opening balances at 31 July 2024, purchases since, and year-end depreciation journals at 31 July 2025. Examples: buildings at cost UGX 11.16bn, land at cost UGX 9.0bn, leasehold land at Masaka UGX 9.57bn, revaluation reserve UGX 8.8bn. | These are class-level totals only. There is **no item-level register anywhere**, so the opening register must come from a physical verification (open question Q6). |
| Second chart of accounts | `fin_subaccounts` under `AC9001`, `AC9100`, `AC9200` | A newer "final" set: LAND, BUILDINGS, MOTOR VEHICLES and so on. Only AC9012 has postings. | Which set to map to is open question Q4. The seed uses the AC80xx set, which holds the balances. |
| Item groups and stores | `inv_itemgroup` (10), `inv_storelocation` (2) | Consumables and equipment groups, Main Store and Stationery Store. | Informed the sub-category list. "In store" assets can name the store in the room field. |
| Suppliers | `campus_dynamics_accounts.supplier` (3 rows, used by `SupplierManagement.aspx`); `inv_supplierdetails` (97 rows, legacy) | Names, addresses and phones. | Supplier lookup reads both (union, de-duplicated by name), with free text as a fallback. `supplier_id` stores `supplier.supplierID` when matched. |
| Requisitions | `sys_requisitions`, `sys_requisition_items` (`campus_dynamics`) | One test requisition and 6 test item lines. | Optional `requisition_ref` on the asset links it to `req_number`. There is nothing to seed from. |
| Campuses | `acad_campuses` | 1 KAKEEKA CAMPUS, 2 KIRUMBA CAMPUS (0 = ALL, excluded). | Required campus on every asset. |
| Rooms and buildings | `acad_lecturerooms` (48 rooms, `campusId`), `acad_building` (0 rows) | Teaching rooms only. | The room picker offers teaching rooms, with free text for offices and stores. Building is free text until `acad_building` is filled. |
| Departments | `hrm_departments` (`ID`, `dept_name`) | 31 departments, including ESTATES, STORES, LIBRARY and ICT. | Department lookup. |
| Staff | `hrm_employee` (`empID`, `emp_name`, `EMP_CODE`); the department comes from the current contract through `hr_current_contract_id(empID)` | 313 staff. | Responsible-person lookup shows name, staff number and department. |
| Financial years | `fin_financial_years`, `FinancePeriod.cs` | August to July. The live rows are inconsistent: 2024/2025 starts 31 July 2024, and the "2026/2027" row covers 1 August 2025 to 30 October 2026. | The module derives the year from the date by rule (D1) and does not depend on these rows (Q1). |

### 1.2 Code patterns reused

| Pattern | Source | How it is reused |
|---|---|---|
| Review-centre list, filter bar, chips, pager, open-a-row modal, reason dialog, batch bar, export dialog | `NewScreens/GraduationCandidates.aspx`, `js/graduation.js` (`window.G`), `css/graduation.css` | Adapted into `js/fa.js` (`window.FA`) and `css/fa.css` with the same structure and behaviour. Graduation wording is removed and the design rules are applied: radius 0, no shadow, no gradient (D10). |
| PageMethods returning JSON strings | Graduation: `[WebMethod(EnableSession = true)] public static string X(...)`, `{success, ...}` | The same, with a permission check as the first line of every method. |
| Server-side PDF, Excel with cover sheet, and CSV | `App_Code/Graduation/GraduationExport.cs` (SpreadsheetML, CSV with BOM and formula guard, `FileName`), `GraduationPdf.cs` (DevExpress XtraReports v16.1, crest, certification block, groups) | Adapted copies `App_Code/FixedAssets/FaExport.cs` and `FaPdf.cs`. The header office line and signatories become parameters: "Office of the University Bursar", "Prepared by / Checked by / Approved by". |
| Drag-and-drop tree | `StudentRearrangeManage.aspx`, `js/rearrange-manage.js`: native HTML5 drag and drop, a "Move to" select as the keyboard alternative, pending changes, a review modal, one transaction on save, `clientOpId` | The same model, simplified to two levels. |
| Append-only decisions with superseding | `acad_grad_review` | `fa_record` is append-only. Corrections are new rows that point at the row they reverse. |
| Activity log | `acad_activity_log (user_id, page_function, par VARCHAR(300), comments VARCHAR(200), access_date)` | Every write also logs a one-line summary with `page_function = 'Fixed Assets'`. Text is truncated to the column widths, because strict mode would otherwise fail the write. |
| Ledger-with-audit discipline | `fin_ledger` plus the `edit_ledger` trigger; `FinanceDB.ExecuteInTransaction` | Each write is one transaction covering the record, the asset row and the audit rows. Triggers guard the tables (D12). |
| RBAC | `RoleAccessService.RequireSlug(page, slug)`, `CanAccess(slug)`; `sys_menu_items`, `sys_role_permissions`; the sidebar is hand-written markup in `SidebarMaster.master` filtered by slug | New slugs and grants (section 6) plus the sidebar markup. Page titles go in `SetPageTitle()`. |
| Uploads | `Communications.aspx.cs` (sanitised name plus timestamp suffix), `GetFile.ashx` (session-checked download) | `FaFile.ashx` serves files from `App_Data/FixedAssets` after a permission check (D13). |
| Charts | Chart.js 3.9.1 from jsDelivr (used by 5 NewScreens pages) | The dashboard and asset trend charts. |
| Browser tests | Headless Edge over CDP (`browser.py`), the same harness used for the HR module | End-to-end tests (section 9). |

### 1.3 Constraints that shape the build

- **Unauthenticated PageMethods.** eadmin PageMethods and `?action=` handlers run even without login (memory: eadmin-anonymous-action-endpoints). Every `[WebMethod]` checks the session and slug itself.
- **Session lock.** PageMethods with session serialise per user. The dashboard therefore loads everything in one call rather than eight parallel calls.
- **Recompile cost.** `App_Code` changes recompile the whole site (1 to 2 minutes, sessions drop). App_Code work is batched and deployed out of hours.
- **Character set.** The database defaults are latin1 while the tables are utf8. Every new table declares `utf8 / utf8_general_ci` explicitly, matching `hrm_employee`, so joins keep their indexes.
- **Join hygiene.** No `TRIM()` or function on indexed join columns. Joins are on integer IDs only (memory: StudentResults TRIM meltdown, cross-db collation).
- **Tooling.** MySQL 5.6.43, `STRICT_TRANS_TABLES`; C# 5 only (no `$""`, `?.`, `=>` members).

---

## 2. Database design

All tables are in `campus_dynamics`, InnoDB, utf8. The full SQL is in Appendix A. **It has been run twice against MySQL 5.6.43 in a scratch schema**, together with the seed and menu scripts. The second run changed nothing, and the guard triggers and keys were tested (section 9.1).

### 2.1 `fa_category`: categories and sub-categories

| Column | Type | Notes |
|---|---|---|
| id | INT UNSIGNED PK AI | |
| parent_id | INT UNSIGNED NOT NULL DEFAULT 0 | 0 marks a category; otherwise the parent category. Two levels are enforced in the service: a parent must itself have `parent_id = 0`. |
| code | VARCHAR(10) | `UNIQUE(parent_id, code)`. Category codes are 2 letters and sub-category codes 3, used in asset numbers. |
| name, description | VARCHAR(120), VARCHAR(500) | |
| asset_type | ENUM TANGIBLE, INTANGIBLE, NULL | NULL on a sub-category means "inherit". |
| dep_method | ENUM SL, RB, NONE, NULL | Straight line, reducing balance, not depreciated. |
| useful_life_years | DECIMAL(6,2) NULL | Drives SL. |
| dep_rate_pct | DECIMAL(7,4) NULL | Drives RB. For SL it is shown as 100/life. |
| residual_pct | DECIMAL(5,2) NULL | Default residual as % of cost. |
| revalue_every_months | SMALLINT NULL | NULL = cost model. A value = revaluation model with this interval (D6). |
| cap_threshold | DECIMAL(18,2) NULL | Below this, the form warns that the item may be an expense, not an asset (Q5). |
| gl_cost_account, gl_accum_account, gl_expense_account | VARCHAR(20) NULL | `fin_subaccounts.AccountCode`. |
| next_seq | INT UNSIGNED DEFAULT 1 | Asset number sequence (sub-categories only). |
| sort_order, is_active | | Never deleted; deactivated. |
| created_by/at, updated_by/at, row_version | | `row_version` gives optimistic concurrency, so two people cannot overwrite each other. |

**Inheritance.** The effective value is `COALESCE(sub.x, parent.x, setting default)`. It is resolved in C#. A new asset copies the effective values at creation, and editing a category never changes existing assets.

### 2.2 `fa_asset`: the register

These columns are entered on the form (required ones in bold):

| Group | Columns |
|---|---|
| Identity | **asset_no** VARCHAR(40) UNIQUE, asset_no_edits, tag_no, **name**, description, serial_no, model, make |
| Classification | **category_id** (the sub-category), asset_type, quantity (for pooled items such as a lot of books or 120 chairs) |
| Location | **campus_id**, building, room_id (teaching room) or room (free text), department_id |
| Custody | custodian_emp_id, custodian_since |
| Acquisition | supplier_id, supplier_name, **purchase_date**, invoice_ref, order_ref, requisition_ref, funding_source, warranty_expiry, **original_cost** |
| Valuation settings | dep_method, useful_life_years, dep_rate_pct, residual_value, dep_start_date (defaults to the purchase date) |
| Other | notes, created/updated, row_version |

The following columns are **posted state**. They are written only by the posting routine (section 2.10), never by the form:

| Column | Meaning |
|---|---|
| status | IN_USE, IN_STORE, UNDER_REPAIR, LOST, DISPOSED, WRITTEN_OFF, VOID |
| current_value | Carrying amount after the latest value record |
| cost_basis | Original cost plus capital additions |
| accum_depreciation | Depreciation since the last base reset (acquisition or revaluation) |
| reval_surplus | Net revaluation and appreciation |
| base_value, base_date, base_remaining_months | Inputs to the SL engine (section 5.5) |
| depreciated_to | Last month-end depreciation covers. The heart of the double-posting guard. |
| last_valuation_date, last_verified_date, closed_on, last_record_id | |

**Indexes:** unique asset_no; tag_no; (category_id, status); (campus_id, status); department_id; custodian_emp_id; purchase_date; status; serial_no.

**Asset number.** The format is `MRU-{CAT}-{SUB}-{00001}`, for example `MRU-MV-BUS-00001`. The sequence is per sub-category, claimed inside the create transaction with `UPDATE fa_category SET next_seq = LAST_INSERT_ID(next_seq + 1) WHERE id = @sub`, which locks the row so two clerks cannot get the same number.
- It may be **edited once** (`asset_no_edits` goes from 0 to 1), for example to match an existing engraved tag. That edit needs a reason, must stay unique and is audited.
- The number never changes when the asset moves to another sub-category, because it is the physical identity.

### 2.3 `fa_record`: the asset ledger (append-only)

| Column | Notes |
|---|---|
| id, asset_id, record_type, record_date, fin_year | `fin_year` such as `2025/2026`, derived from the date (D1). |
| value_class | COST, DEPRECIATION, REVALUATION, DISPOSAL, NONE. Totals are a plain `SUM(change_amount)` per class. A reversal carries the class of the record it reverses, with the amount negated. |
| value_before, value_after, change_amount | Always filled. For records that do not affect value, before = after and change = 0. |
| period_from, period_to, months, dep_active | Depreciation only. `dep_active = 1` on a live depreciation row and NULL once reversed. |
| quantity, cost_amount, is_capital | Maintenance and pooled items. |
| status_before, status_after | |
| from_/to_ campus_id, department_id, location, custodian_emp_id | Transfers, and the initial placement on acquisition. |
| disposal_method, proceeds, gain_loss | Disposal. |
| valuer, new_remaining_months | Revaluation. |
| condition_grade, verified_found | Verification. |
| reason, reference, approval_ref, approved_by, approved_at | |
| run_id, import_batch_id | Source of system-generated rows. |
| reverses_record_id, reversed_by_record_id | Corrections. |
| recorded_by, recorded_role, recorded_at, client_op_id | `client_op_id` is unique, so a double click or retry cannot post twice. |

**Keys:**
- `(asset_id, record_date, id)` gives the history and the as-at queries.
- `(record_type, record_date)` and `(fin_year, record_type)` serve the reports.
- `run_id` and `record_date` are indexed.
- **`UNIQUE(asset_id, period_to, dep_active)`** is the database half of the double-posting guard. NULLs never collide, so reversed rows (`dep_active = NULL`) and non-depreciation rows (`period_to = NULL`) are not constrained.

Attachments for a record are rows in `fa_attachment` with `record_id` set, so a record can carry more than one document.

### 2.4 Record types

| Type | Value effect | Required fields | Permission |
|---|---|---|---|
| ACQUISITION | Opening value = cost (class COST) | Created with the asset; sets the initial status (In use or In store), location and custodian | edit |
| OPENING | Value = cost less accumulated depreciation to the opening date | Used for assets bought before go-live (import or form): opening date, accumulated depreciation (typed, or computed by the engine) | edit / import |
| DEPRECIATION | Reduces value (class DEPRECIATION) | From a run: period. Manual: period, amount, reason (at least 10 characters) | value |
| REVALUATION | New value up or down; resets accumulated depreciation (net method) and the SL base | New value, valuation date, valuer, reason, reference; optional new remaining life | value |
| APPRECIATION | Increase (land and buildings) | New value, reason, reference | value |
| TRANSFER | None | At least one of campus, location, department or custodian changes; reason | transfer |
| STATUS | None | New status (In use, In store, Under repair, Lost); reason | transfer |
| MAINTENANCE | Capital: adds the cost to value and cost basis. Revenue: none. | Date, description (reason), cost, capital yes/no; supplier and invoice optional | transfer (revenue), value (capital) |
| VERIFICATION | None; sets the last verified date | Found yes/no, condition, verified by, date | transfer |
| ESTIMATE | None; prospectively changes method, life, rate or residual and resets the base | New values, reason | value |
| DISPOSAL | Value to 0, closes the asset; status DISPOSED (sale, donation, scrap, trade-in, transfer out) or WRITTEN_OFF | Method, date, proceeds (0 allowed), approval reference, reason (at least 10 characters); gain or loss = proceeds less carrying amount | **dispose** |
| VOID | Value to 0, status VOID, excluded from every total | Reason (entered in error, duplicate); only before any depreciation or other value record | **dispose** |
| REVERSAL | Negates one earlier record | Reason; see the rules below | the permission of the reversed type |

**Reversal rules:**
- Value records are reversed **newest first only**. A middle record cannot be removed, because every later value_before depends on it.
- Transfers and status changes reverse only the latest of their kind.
- A depreciation run is reversed as a whole, newest run first.
- A record in a locked year cannot be reversed.

### 2.5 `fa_depreciation_run`

| Columns | Notes |
|---|---|
| id, fin_year, period_end, scope_json, status | `scope_json`: campus and category filters. Status POSTED or REVERSED. |
| asset_count, total_amount, preview_hash, notes | `preview_hash`: SHA-1 of the previewed lines; the post must recompute the same hash. |
| posted_by/at, reversed_by/at, reverse_reason | |
| client_op_id | Unique. |

### 2.6 `fa_year_lock`

Columns: `fin_year` (PK), `is_locked`, `reason`, `locked_by/at`, `unlocked_by/at`.
- No record of any type may be dated in a locked year, and none in it may be reversed.
- Locking and unlocking need `accounts.assets.yearlock` and a reason, and are audited.

### 2.7 `fa_attachment`

Columns: asset_id, record_id, kind (PHOTO, INVOICE, WARRANTY, VALUATION, DISPOSAL, OTHER), original_name, stored_name, mime, size_bytes, sha1, uploaded_by/at, is_active, removed_by/at/reason.
- Files are stored at `~/App_Data/FixedAssets/{assetId}/{stored_name}`, which IIS never serves directly.
- Removing an attachment only hides it, with a reason.
- Accepted types: pdf, jpg, png, docx, xlsx; at most 10 MB (setting). The file's leading bytes are checked against its extension.

### 2.8 `fa_import_batch`

Columns: file_name, sha1, rows_total/ok/error, status (VALIDATED, COMMITTED, ABANDONED), payload (parsed rows, JSON), report (errors, JSON), created and committed by/at.
- A batch can be committed once.
- Uploading the same file again (same sha1) gives a warning.

### 2.9 `fa_audit` and the guard triggers

`fa_audit` columns: entity (CATEGORY, ASSET, RECORD, RUN, LOCK, ATTACHMENT, IMPORT, SETTING), entity_id, asset_id, action, before_json, after_json, reason, actor, actor_role, ip_address, created_at.
- Every write inserts at least one row in the same transaction. **If the audit insert fails, the write fails.** This is stricter than Graduation, which swallows log errors.
- The one-line `acad_activity_log` entry is written in the same transaction.

**Triggers** (tested, section 9.1):

| Trigger | Effect |
|---|---|
| `trg_fa_record_bd`, `trg_fa_asset_bd`, `trg_fa_category_bd`, `trg_fa_audit_bd` | Refuse DELETE. |
| `trg_fa_record_bu` | Refuses any UPDATE that changes a value, date, type, period or author. Only the reversal links `dep_active` and `reversed_by_record_id` can change. |
| `trg_fa_audit_bu` | Refuses any UPDATE. |

### 2.10 `fa_settings`

A key-value table. `system_config` is a single wide row and is not suitable. Keys:

| Key | Default |
|---|---|
| fy_start_month | 8 |
| asset_no_prefix | MRU |
| cap_threshold_default | 0 |
| revalue_months_default | 12 |
| attachment_max_mb | 10 |

### 2.11 Posting the asset row

`FaPosting.Recompute(assetId, conn, tx)` runs at the end of every write transaction.
1. It reads the asset with `SELECT ... FOR UPDATE`.
2. It replays the live records in `(record_date, id)` order. Records that are reversed, and reversal rows themselves, are skipped. A reversed pair therefore cancels out exactly.
3. It derives status, current value, cost basis, accumulated depreciation, revaluation surplus, the SL base, depreciated_to, location, department, custodian, last valuation, last verification and closed_on.
4. It writes them to `fa_asset` and bumps `row_version`.
5. It inserts an `fa_audit` row (ASSET / POST) holding before and after values for the fields that changed.

Replaying the whole ledger takes milliseconds for one asset, and it cannot drift. An admin action, **Check register integrity**, recomputes every asset without writing and lists any difference.

### 2.12 Queries that must work on MySQL 5.6

- **Value as at a date** (no window functions): join each asset to its latest value record on or before the date, using a correlated `ORDER BY record_date DESC, id DESC LIMIT 1` on `ix_fa_record_asset`. Tested.
- **Fast path:** when the as-at date is today, the dashboard and list read the posted columns on `fa_asset` directly.
- **Per-year totals:** `SUM(change_amount)` grouped by `value_class` and `fin_year`. No CTEs needed.

---

## 3. Proposed starting categories (for approval)

### 3.1 Evidence used

The rates below come from the University's 2024/25 year-end journals in `fin_ledger`, not from guesswork:

| Asset | Journal amount | Matches |
|---|---|---|
| Toyota Coaster, UGX 240,000,000, bought October 2024 | 40,000,000 | 20% straight line × 10 months (October to July) |
| Electrical lab equipment, UGX 399,500, bought September 2024 | 122,069 | 33.33% straight line × 11 months |
| Leasehold land, Masaka, UGX 9,571,560,000 | 96,682,424 a year | 99-year lease |
| Leasehold land, Mubende, UGX 216,075,000 | 4,409,694 a year | 49-year lease |
| Buildings, UGX 11.16bn | 222,948,895 | About 2% straight line (50 years) |

Furniture, computers, library books and software charges do not match a single clean rate, because their mix of older assets is unknown. Their rates are proposals and are marked "confirm" (Q2).

### 3.2 Categories and sub-categories

Asset numbers take the form `MRU-{category}-{sub-category}-00001`. "Inherit" means the sub-category uses the category's defaults.

| Category | Method and life | Revalue | GL cost / accum / expense | Sub-categories (code: name, overrides) |
|---|---|---|---|---|
| **LB** Land and Buildings | SL 50 yrs (2%) | every 12 months (Q10) | AC8022 / AC8023 / AC2161 | FHL Freehold land: **not depreciated**, GL cost AC8039. LHL Leasehold land: SL over the lease term, default 49 yrs (Masaka asset set to 99), GL AC8031 / AC8032 / AC2162. BLD Buildings: inherit. EXT External works and site improvements: SL 20 yrs. |
| **FF** Furniture and Fittings | SL 8 yrs (12.5%), confirm | cost model | AC8021 / AC8030 / AC2161 | OFF Office furniture. TCH Teaching furniture (lecture desks, whiteboards, exam furniture). RES Residential furniture. FIT Fixtures and fittings (curtains, signboards, partitions). |
| **CE** Computer Equipment | SL 3 yrs (33.33%), confirm | cost model | AC8024 / AC8026 / AC2161 | DSK Desktop computers (including lab machines). LAP Laptops and tablets. SRV Servers and storage. PRN Printers and scanners. PER Peripherals and UPS units. |
| **OE** Office Equipment | SL 5 yrs (20%) | cost model | AC8016 / AC8041 / AC2161 | PRJ Projectors and screens. CPY Photocopiers. APL Office appliances (air conditioners, refrigerators, dispensers). SEC Safes and security equipment. OTH Other office equipment. |
| **LW** Laboratory and Workshop Equipment | SL 3 yrs (33.33%, matches the journal) | cost model | AC8025 / AC8027 / AC2161 | SCI Science laboratory. ENG Engineering workshop. MED Media and studio. HTL Hospitality training. ART Art and design. |
| **PM** Plant and Machinery (added, D9) | SL 10 yrs (10%) | cost model | AC8016 / AC8041 / AC2161 (no own account; Q4) | GEN Generators. SOL Solar and power systems. WTR Water tanks and pumps. MCH Machines and tools. |
| **MV** Motor Vehicles | SL 5 yrs (20%, matches) | cost model | AC8035 / AC8036 / AC2161 | BUS Buses and coasters. CAR Cars and pickups. MCY Motorcycles. |
| **LM** Library Books and Materials | SL 5 yrs (20%), confirm | cost model | AC8020 / AC8029 / AC2161 | BKS Printed books (one asset per acquisition lot, with a quantity). JNL Journals and serials. ELB E-library and digital resources: intangible, GL AC8028 / AC8040. |
| **NC** Network and Communication Equipment | SL 5 yrs (20%) | cost model | AC8024 / AC8026 / AC2161 (no own account; Q4) | NET Switches, routers and access points. CAB Structured cabling. TEL Telephony and radio. CCT CCTV and access control. |
| **SE** Sports Equipment | SL 5 yrs (20%) | cost model | AC8016 / AC8041 / AC2161 | FLD Field and outdoor. GYM Gym and fitness. IND Indoor games. |
| **SW** Software and Licences (intangible) | SL 4 yrs (25%), confirm | cost model | AC8037 / AC8038 / AC2163 | ERP Institutional systems (Campus Dynamics: GL AC9012 sits in the other chart, Q4). LIC Software licences. |

That is 11 categories and 42 sub-categories. All residual values default to 0. The seed script is Appendix B. It is tested, but **it will not be run until this table is approved.**

### 3.3 Legacy category mapping

| `assetcategory` | New sub-category |
|---|---|
| BUILDINGS | LB-BLD |
| LAND | LB-FHL or LB-LHL |
| COMPUTERS | CE-DSK or CE-LAP |
| PRINTERS | CE-PRN |
| MACHINERY | PM-MCH |
| LIBRARY STOCK | LM-BKS |
| MOTOR VEHICLE | MV-* |
| FURNITURE & FITTINGS | FF-* |
| EQUIPMENT | OE-OTH |

The table holds no assets, so this only guides the people preparing the opening import.

---

## 4. Screens

Common to all five pages:
- **Page setup:** `SidebarMaster`; `RoleAccessService.RequireSlug` in `Page_Load`; `css/fa.css` and `js/fa.js`.
- **Design rules:** flat; radius 0; no shadow or gradient. Colours: navy `#05275C`, accent `#174DA4`, gold `#D4A017` (used only for warnings), surface `#F5F7FA`, border `#E0E5ED`.
- **Wording:** sentence case, statuses as words, no em dashes, no emojis or glyph icons (inline SVG only).
- **Money:** whole shillings with thousands separators, labelled "UGX" in column headers.
- **Dates:** `6 Oct 2026`.
- **Server responses:** every PageMethod returns `{"success":true,...}` or `{"success":false,"message":"..."}`. A permission failure adds `"denied":true`.

### 4.1 Assets Dashboard (`AssetsDashboard.aspx`, slug `accounts.assets.dashboard`)

**Layout, top to bottom:**
1. **Filter bar:** campus, category, sub-category, date from, date to (default: start of this financial year to today), Apply, Reset. Values are shown **as at the "to" date**. Acquisitions and disposals count within the range.
2. **Six figure tiles:** Assets (count, excluding Void); Original cost; Book value; Accumulated depreciation; Revaluation surplus; Disposed in period (count and proceeds).
3. **Charts** (Chart.js 3.9.1, flat, navy and accent palette):
   - Assets by status (doughnut, six statuses).
   - Book value by category (horizontal bar).
   - Assets by campus (grouped bar: count and value for Kakeeka and Kirumba).
   - Acquisitions by financial year (line of cost), with a second line showing University book value at each 31 July (the trend).
4. **Top ten assets by book value** (table).
5. **Action lists:** five panels, each showing a count, its first five rows and "View all":
   - No responsible person (in use, no custodian).
   - Not yet tagged (no tag number) (D5).
   - Revaluation due (revaluation-model categories past their interval) (D6).
   - Useful life ended (fully depreciated and still in use).
   - Not verified in the last 12 months.

**Drill-through.** Every tile, chart segment, table row and panel links to `Assets.aspx` with the matching query string, for example `?status=LOST&campus=2&cat=7&flag=no_custodian&fy=2025/2026`. Assets.aspx reads it on load.

**PageMethod** (one call, because of the session lock): `GetDashboard(string filterJson)`.
- Input: `{campusId, categoryId, subCategoryId, from, to}`
- Output:
  ```
  {success, asAt, tiles:{count,cost,value,accumDep,revalSurplus,disposedCount,disposedProceeds},
   byStatus:[{status,label,count,value}], byCategory:[{id,code,name,count,cost,value}],
   byCampus:[{id,name,count,value}], byYear:[{finYear,count,cost,bookValueAtYearEnd}],
   top:[{id,assetNo,name,category,value}],
   actions:{noCustodian:{count,rows:[{id,assetNo,name}]}, notTagged:{...}, revaluationDue:{...},
            lifeEnded:{...}, notVerified:{...}}}
  ```

### 4.2 Asset Categories (`AssetCategories.aspx`, view `accounts.assets.categories`, change `accounts.assets.categories_manage`)

**Layout:** two columns.
- **Left: the tree.** Categories in sort order, each expandable to its sub-categories. Every node shows its code, name, asset count, effective method and life, and an "Inactive" label when deactivated. A drag handle on each node; a "Move to" select on each sub-category as the keyboard alternative.
- **Right: the editor** for the selected node. Fields: code, name, description, asset type, method, life, rate, residual %, revaluation interval, capitalisation threshold, three GL accounts (search over `fin_subaccounts`), active, and read-only sort order. On a sub-category each field shows the inherited value as grey placeholder text, with an "Override" tick. Below the form: "New assets in this sub-category will use: SL, 5 years, residual 0".

**Drag and drop** (simplified from the rearrangement module):

| Rule | Detail |
|---|---|
| Allowed moves | Categories reorder among categories. Sub-categories reorder within a parent or move to another parent. |
| Not allowed | Nesting deeper than two levels. Turning a category into a sub-category or the reverse. |
| Saving | Moves collect in a pending bar: "3 changes not saved. Review and save / Discard". The review lists each change. One transaction applies them all, with a `clientOpId`. |
| Moving a sub-category that has assets | Needs a reason. Asset numbers and existing asset settings do not change; only defaults for new assets follow the new parent. |
| Deactivating | Needs a reason. It hides the node from pickers for new assets. Existing assets are untouched. Deactivating a category also hides its sub-categories. |
| Codes | Can change only while no asset uses the node, because codes are inside asset numbers. |

**PageMethods:**

| Method | Input | Output / effect |
|---|---|---|
| `GetTree()` | | `{success, canManage, settings:{...}, glAccounts:[{code,name}], nodes:[{id,parentId,code,name,description,assetType,method,lifeYears,ratePct,residualPct,revalueMonths,capThreshold,glCost,glAccum,glExpense,active,sort,assetCount,rowVersion,effective:{assetType,method,lifeYears,ratePct,residualPct,revalueMonths,capThreshold,glCost,glAccum,glExpense},children:[...]}]}` |
| `SaveNode(string json)` | `{id?, parentId, code, name, ..., rowVersion, reason}` | `{success, id, rowVersion}` |
| `SetActive(int id, bool active, string reason)` | | `{success}` |
| `SaveLayout(string opsJson, string clientOpId)` | `[{id, parentId, sort, reason?}]` | `{success, changed}` |

### 4.3 Assets (`Assets.aspx`, view `accounts.assets.register`)

**Toolbar** (Graduation Candidates pattern):
- Filters: campus, category, sub-category (cascading), status, responsible person (typeable staff search), department, year acquired (financial year), search (name, asset number, tag, serial), sort, and a hidden `flag` filter set by dashboard links.
- Active filters appear as removable chips.
- Buttons: New asset (edit), Import (import), Export.
- Filters are mirrored in the URL, so links and the back button work.

**Table** (50 rows a page, server-side paging):

| Column | Content |
|---|---|
| (tick) | Row selection for batch actions |
| Asset no | Asset number, tag below |
| Name | Name, serial and make below |
| Sub-category | |
| Campus and location | |
| Department | |
| Responsible person | |
| Purchased | Purchase date |
| Cost (UGX) | |
| Book value (UGX) | |
| Change | "Down 16.7%" or "Up 12.0%" since acquisition |
| Status | Label |

The footer shows totals for cost and book value across all filtered rows, not just the page.

**Batch bar**, shown when rows are ticked:
- Transfer: campus, department, location, custodian, reason.
- Change status: needs a reason.
- Print tags: PDF of labels with a Code 128 barcode of the asset number, using DevExpress XRBarCode.
- Export selected.

Each batch action is capped at 300 assets. It returns `{done, skipped:[{assetNo, why}]}`, for example "disposed assets cannot be transferred".

**Asset detail** (modal opened from a row) has four tabs:

| Tab | Contents |
|---|---|
| Overview | Every field by group. Valuation box: cost basis, accumulated depreciation, book value, revaluation surplus, depreciated to, last valuation and last verification. A small Chart.js line of value over time with the trend summary: "Depreciating. Down UGX 40,000,000 (16.7%) since acquisition". |
| Records | The ledger: date, type, financial year, before, change, after, details, reason and reference, recorded by. Reversed rows are struck through with a link to their reversal. |
| Attachments | Upload with a kind, list, download, remove (reason). |
| History | `fa_audit` entries: who, when, action, field-by-field before and after, reason. |

**Footer actions,** shown according to permissions and status:
- Edit.
- Add record, a menu of the types in 2.4. Each opens a short form with only its required fields, and every reason field uses the reason dialog (minimum 10 characters).
- Reverse latest.
- Print history statement.

**Form** (create and edit modal):
- Grouped as in the brief. Required: asset number (pre-filled with the next number), name, sub-category, campus, initial status (In use or In store), original cost, purchase date. Everything else is optional.
- Choosing a sub-category fills method, life, rate and residual from its effective defaults. Each can be overridden.
- A tick, "Bought before the register started", reveals the opening date and accumulated depreciation (typed, or "Calculate", which runs the engine) and creates an OPENING record instead of ACQUISITION.
- **Valuation on edit:** method, life, rate and residual can be changed only on an asset with no depreciation yet. After that the form sends the user to the Change of estimate record, so the ledger shows it. Current value and status are never fields on this form (rule 4).

**Validation and warnings:**

| Check | Result |
|---|---|
| Purchase date in the future | Refused |
| Cost not above 0 | Refused (donated assets are entered at fair value, and the funding source says "Donation") |
| Purchase date in a locked year | Refused |
| Asset number not unique | Refused |
| Cost below the capitalisation threshold | Warning only |
| Another active asset has the same serial | Warning only |

**PageMethods:**

| Method | Input | Output / effect |
|---|---|---|
| `GetBootstrap()` | | `{success, rights:{edit,value,transfer,dispose,import,reports}, campuses, categories (tree, active only), statuses, departments, finYears, settings}` |
| `GetAssets(configJson)` | `{campusId, categoryId, subCategoryId, status, custodianEmpId, departmentId, finYear, q, flag, sort, dir, page, size}` | `{success, rows:[{id,assetNo,tagNo,name,serialNo,make,subCategory,category,campus,location,department,custodian,purchaseDate,cost,value,changePct,status,statusLabel}], total, page, size, pages, totals:{cost,value}}` |
| `GetAsset(int id)` | | `{success, asset:{...all fields, effectiveDefaults, rowVersion}, records:[{id,type,typeLabel,date,finYear,before,change,after,details,reason,reference,approvalRef,by,at,reversedBy,reverses,canReverse}], series:[{date,value}], trend:{direction,change,pct}, attachments:[{id,kind,name,size,by,at}], audit:[{at,actor,action,reason,changes:[{field,before,after}]}]}` |
| `NextAssetNo(int subCategoryId)` | | `{success, assetNo}` (preview only; the number is claimed on save) |
| `SaveAsset(json)` | `{id?, rowVersion, ...fields, opening:{date, accumDep or "calc"}?, reason (required when editing), clientOpId}` | `{success, id, assetNo, warnings:[]}` |
| `AddRecord(json)` | `{assetId, type, date, ...type fields, clientOpId, confirmCatchUp}` | `{success, recordId, catchUp:{months,amount}?, asset:{status,value,depreciatedTo}}`. If depreciation is behind, the first call returns `{needsCatchUp:{months,amount}}` and the user confirms. |
| `ReverseRecord(int recordId, string reason, string clientOpId)` | | `{success}` |
| `BatchTransfer(json)` | `{ids, to:{campusId, departmentId, building, room, custodianEmpId}, date, reason, clientOpId}` | `{success, done, skipped}` |
| `BatchStatus(json)` | | as above |
| `SearchStaff(q)` | | `[{empId,name,code,department}]` |
| `SearchSuppliers(q)` | | `[{id,name,source}]` |
| `CountExport(configJson)` | | `{success, count}` |

**Export:** a form POST to the page itself with fields `faExport` (pdf, xls, csv), `faConfig`, `faCols`, `faRows`. It is handled in `Page_Load` after the slug check.

**Uploads** go to `FaUpload.ashx`:
- Multipart; session plus `accounts.assets.edit`.
- Returns `{success, id}`.
- Downloads go through `FaFile.ashx?id=` with session plus `accounts.assets.register`.

### 4.4 Import (`AssetImport.aspx`, slug `accounts.assets.import`, reached from Assets; not in the sidebar)

**Step 1: Download the template** (`?template=1`). An `.xlsx` with three sheets:
- Assets: one header row, with required columns marked.
- Lists: sub-category codes with names, campus codes, statuses, department names, disposal methods.
- Notes: explains each column.

**Template columns:** sub_category_code\*, name\*, campus\*, purchase_date\*, original_cost\*, status\* (In use / In store), asset_no (blank = generate), tag_no, description, serial_no, model, make, building, room, department, custodian_staff_no, supplier, invoice_ref, order_ref, funding_source, warranty_expiry, method, useful_life_years, rate_pct, residual_value, opening_date, opening_accum_dep (number or CALC), notes.

**Step 2: Upload** an `.xlsx` (read with `System.IO.Packaging`, available on .NET 4.0) or a `.csv`. At most 3,000 rows.

**Step 3: Validation report.** Each row is checked the same way as the form. Every error is listed by row, column, value and message. The screen shows "2,140 rows ready, 12 rows with errors" and offers the report as a download.
- Checks also cover lookups (staff number, department, campus, sub-category), duplicate asset numbers within the file and against the register, and duplicate serials (warning).

**Step 4: Commit.** This is **all or nothing**, and only when there are no errors. Allowing a partial import would leave the University unsure which rows went in.
- One transaction creates the assets and their ACQUISITION or OPENING records, tags them with `import_batch_id`, and audits each asset plus the batch.
- A committed batch cannot be committed again.

**PageMethods:**

| Method | Output |
|---|---|
| (upload) | `?ajax=validate` (multipart) returns `{success, batchId, total, ok, errors:[{row,column,value,message}], warnings:[...]}` |
| `Commit(int batchId, string clientOpId)` | `{success, created}` |
| `Abandon(int batchId)` | `{success}` |

### 4.5 Asset Records (`AssetRecords.aspx`, view `accounts.assets.records`)

**Tab 1: Ledger.** Every record across the register.
- Filters: date range, record type, campus, category, financial year, search (asset number or name, reason, reference).
- Columns: date, asset no, asset name, type, before, change, after, reason and reference, recorded by.
- Opening a row opens the asset modal at its Records tab. Export is available.
- `GetRecords(configJson)` returns `{success, rows, total, page, size, pages, totals:{byClass:{DEPRECIATION,REVALUATION,COST,DISPOSAL}}}`.

**Tab 2: Depreciation** (post needs `accounts.assets.value`).
1. Choose the period end: a month-end, defaulting to the next 31 July. Optional scope: campus and category.
2. **Preview.** A table of assets that would be charged: asset, method, months covered (from and to), opening value, charge, closing value, note. Totals by category and a grand total. A list of skipped assets with the reason ("already depreciated to 31 Jul 2026", "fully depreciated", "not depreciated", "disposed").
3. **Post**, after a confirmation that repeats the total.
   - The server recomputes inside one transaction and compares the preview hash. If anything changed since the preview, it refuses: "Values changed since the preview. Preview again."
4. **Run history:** period, scope, assets, total, posted by and at, status.
   - Reverse is available on the newest run only, needs a reason, and is blocked if any of its assets has a later value record.
   - **Journal summary** for each run: per GL account, the debit to the expense account and the credit to the accumulated-depreciation account, ready for Finance to post through Journal Entries (D4).

**Tab 3: Year locks.** Financial years with lock state, locked by and at. Lock and Unlock need `accounts.assets.yearlock` and a reason.

**PageMethods:**

| Method | Input | Output |
|---|---|---|
| `PreviewDepreciation(json)` | `{periodEnd, campusId?, categoryId?}` | `{success, finYear, periodEnd, rows:[{assetId,assetNo,name,method,from,to,months,before,charge,after,note}], byCategory:[{code,name,count,charge}], total, count, skipped:[{assetNo,why}], previewHash}` |
| `PostDepreciation(json)` | `{periodEnd, campusId?, categoryId?, previewHash, notes, clientOpId}` | `{success, runId, count, total}` |
| `GetRuns()` | | |
| `ReverseRun(int runId, string reason, string clientOpId)` | | |
| `GetJournal(int runId)` | | `{success, lines:[{account,accountName,debit,credit}]}` |
| `GetLocks()` | | |
| `SetLock(string finYear, bool locked, string reason)` | | |

### 4.6 Reports (`AssetReports.aspx`, slug `accounts.assets.reports`)

**Layout:**
- **Left:** the report list (section 7), with one line describing each.
- **Right:** the standard filters (campus, category, sub-category, department, responsible person, status, period or as-at date as the report needs) and report-specific options (asset for the history statement, financial year for the depreciation schedule).
- **Buttons:** PDF, Excel, CSV. Each opens the export dialog: columns to include, grouping, sort, this page or all rows, and a live row count.

**Output formats:**

| Format | Details |
|---|---|
| PDF | `FaPdf`. Crest, "Muteesa I Royal University", "Office of the University Bursar", report title, certification block (prepared for, filters, extracted date and count), grouped sections with subtotals, "Page n of m", and signature lines: Prepared by (Assets Officer), Checked by (Accountant), Approved by (University Bursar). |
| Excel | `FaExport`. A cover sheet with the same facts, then the data sheet(s): navy header, striped rows, frozen header, numbers stored as numbers. |
| CSV | BOM, `#` provenance lines, formula-injection guard. |

File names: `MRU-assets-{report}-{yyyyMMdd-HHmm}`.

**PageMethods:** `GetReportMeta()` returns the catalogue with each report's columns, groups and needed filters; `CountReport(key, configJson)`. Downloads are a form POST with `faReport={key}` handled in `Page_Load`.

---

## 5. Depreciation engine

### 5.1 Financial year

- The year runs 1 August to 31 July. `fy_start_month = 8` in `fa_settings`.
- The label is `2025/2026`, computed from any date: a month of August or later starts a year.
- The module does not rely on the `fin_financial_years` rows, which are inconsistent (Q1).

### 5.2 Methods

| Method | Rule |
|---|---|
| SL (straight line) | (Depreciable base − residual) spread evenly over the remaining months since the base date (5.5). |
| RB (reducing balance) | Annual rate × carrying amount at the start of the financial year, or at the base date if that falls inside the year. Pro-rated by months: charge = opening carrying × rate × months / 12. |
| NONE | No charge (freehold land). |

The carrying amount never goes below the residual value. The final charge is cut to land exactly on it.

### 5.3 Mid-year rule

- **Month-based.** The month in which depreciation starts counts as a full month, and the month of disposal is not charged. The start date is the purchase date unless `dep_start_date` is later, for an asset not yet in use.
- This is the rule the University's own 2024/25 journals follow:

| Asset | Cost | Rule | Expected | Journal |
|---|---|---|---|---|
| Toyota Coaster | 240,000,000 | 20% SL from October 2024: 10 months to 31 July 2025 | 240,000,000 × 20% × 10/12 = **40,000,000** | 40,000,000 |
| Electrical lab equipment | 399,500 | 33.33% SL from September 2024: 11 months | 399,500 / 36 × 11 = **122,069** | 122,069 |

- A day-based rule would not reproduce these figures, and a half-year rule would not either.

### 5.4 Periods and runs

- A run covers every eligible asset **up to a month-end period end**. For each asset the uncovered months run from the later of the start month and the month after `depreciated_to`, through the period end.
- **Annual is the default:** period end 31 July, one record per asset per year.
- **Monthly or quarterly runs** are allowed for management accounts. The next run simply starts where the last stopped, so mixing frequencies cannot double count.
- **A run never writes one record across two financial years.** If an asset is behind by more than a year (for example, an imported asset), the run writes one record per year, so the depreciation schedule for each year is right.
- If any year a run would write into is locked, the asset is skipped with the reason shown.
- **Eligible assets:** status In use, In store, Under repair or Lost, and value above residual. Assets are depreciated while idle (IPSAS 17). Disposed, written-off and void assets never are.

### 5.5 Straight line, exactly

Each asset keeps a **base**: `base_value`, `base_date` (a month) and `base_remaining_months`.

| Event | Base value | Remaining months |
|---|---|---|
| Acquisition | Cost | Life × 12 |
| Opening | Opening value | Life × 12 − months since start |
| Revaluation | New value | The valuer's figure, or the original life less elapsed months |
| Capital maintenance | Value after the addition | Unchanged |
| Change of estimate | Current value | New life less elapsed months |

- Monthly charge = (base_value − residual) / base_remaining_months.
- To avoid rounding drift, each record's charge is the **difference of rounded cumulative amounts**:
  `round(monthly × (k + m)) − round(monthly × k)`, where k is the months already charged since the base and m the months in this record.
- Amounts are rounded to whole shillings. Over the full life the charges add up exactly to base − residual.

### 5.6 Double-posting guard (four layers)

1. **Coverage, not periods.** The engine charges only months after `depreciated_to`. A second run for the same period finds nothing to charge ("already depreciated to 31 Jul 2026").
2. **Row lock.** Posting re-reads each asset with `SELECT ... FOR UPDATE`, in id order to avoid deadlocks, and re-checks `depreciated_to` inside the transaction. Two users posting at once are serialised, and the second finds nothing left.
3. **Database key.** `UNIQUE(asset_id, period_to, dep_active)` refuses a second live depreciation row ending on the same month for the same asset. Tested: error 1062. If it ever fires, the whole run rolls back.
4. **Idempotent request.** `client_op_id` is unique on runs and records, so a double click or a network retry cannot post twice.

Reversal is the only way to re-post a period. It sets `dep_active` to NULL on the reversed rows (allowed by the trigger) and moves `depreciated_to` back.

### 5.7 Events between runs

Value-changing records (revaluation, appreciation, capital maintenance, change of estimate, disposal) need depreciation to be up to date to the **end of the previous month**. When it is not:
- The screen shows "Depreciation of UGX 2,000,000 for Aug 2026 to Feb 2027 will be posted first".
- On confirmation, the catch-up depreciation and the event go in **one** transaction.

So, for an asset disposed on 15 March:
- It is charged to 28 February.
- March is not charged (5.3).
- The carrying amount at disposal is right, and gain or loss = proceeds − carrying amount.

Records that do not affect value (transfer, status, verification) need no catch-up.

### 5.8 Back-dating and locked years

- A value record cannot be dated before the asset's latest value record or its `depreciated_to`. To insert one earlier, the later records are reversed first. The ledger order then always equals the value order.
- Transfers and status changes may be back-dated within open years. The latest by date wins.
- Nothing may be dated in, or reversed from, a locked year (`fa_year_lock`). Locking 2025/2026 after the audit freezes it.

### 5.9 Link to the general ledger (version 1)

- The module **does not post to `fin_ledger`** (D4). For each run, and for each financial year, it produces the journal summary: debit the expense account and credit the accumulated-depreciation account, per category GL mapping.
- The **Register to GL reconciliation** report compares, per GL account and as at a date, the register's cost, accumulated depreciation and book value with the `fin_ledger` balances, and shows the difference.
- Automatic posting can be added later as a separate, approved step (Q3).

### 5.10 Code layout

| File | Purpose |
|---|---|
| `App_Code/FixedAssets/FaMath.cs` | Pure functions with no database. Unit tested. |
| `FaFinYear.cs` | Financial year from date, and month arithmetic. |
| `FaDepreciation.cs` | Preview, post, reverse, catch-up. Uses FaMath. |
| `FaPosting.cs` | Replays the ledger onto the asset row (2.11). |

---

## 6. Permissions

**Slugs** (Appendix C, tested). Page slugs map to URLs; action slugs have no URL and show in role management under "Fixed Assets".

| Slug | Grants |
|---|---|
| `accounts.assets` | The sidebar parent |
| `accounts.assets.dashboard` | View the dashboard |
| `accounts.assets.categories` | View categories |
| `accounts.assets.register` | View assets, details and attachments |
| `accounts.assets.records` | View the ledger, depreciation runs and locks |
| `accounts.assets.reports` | Run reports and exports |
| `accounts.assets.edit` | Create and edit assets, attachments, and the opening value on create |
| `accounts.assets.value` | Depreciation (runs and manual), revaluation, appreciation, capital maintenance, change of estimate, and reversing those |
| `accounts.assets.transfer` | Transfers, status changes, revenue maintenance, verification |
| `accounts.assets.dispose` | Disposal, write-off, void, and reversing those (higher) |
| `accounts.assets.categories_manage` | Create, edit, move and deactivate categories |
| `accounts.assets.import` | Bulk import |
| `accounts.assets.yearlock` | Lock and unlock financial years |

**Proposed grants** (admin has every slug through the wildcard):

| Slug | Bursar | Accountant | Finance officer | Assets officer (new) | Procurement | Auditor (read-only) | VC |
|---|---|---|---|---|---|---|---|
| dashboard | yes | yes | yes | yes | | yes | yes |
| categories (view) | yes | yes | yes | yes | | yes | |
| register (view) | yes | yes | yes | yes | yes | yes | yes |
| records (view) | yes | yes | yes | yes | | yes | |
| reports | yes | yes | yes | yes | | yes | yes |
| edit | yes | yes | | yes | yes | | |
| value | yes | yes | | | | | |
| transfer | yes | | | yes | | | |
| dispose | **yes** | | | | | | |
| categories_manage | yes | yes | | | | | |
| import | yes | | | yes | | | |
| yearlock | **yes** | | | | | | |

**Enforcement:**
- The page uses `RequireSlug`.
- Every PageMethod checks the session and slug first (`FaAccess.Require(slug)`). Write methods check the action slug for the specific record type.
- The UI hides actions the user lacks, but the server is the guard.
- The auditor role is also blocked from writes by `ReadOnlyGate`.

---

## 7. Reports

Every report takes the standard filters and is available as PDF, Excel and CSV through the export dialog. Columns marked (opt) are off by default.

| # | Report | Grouping | Columns |
|---|---|---|---|
| R1 | Fixed Asset Register | Category, sub-category | Asset no, tag, name, serial (opt), make and model (opt), campus, location, department, responsible person, purchase date, supplier (opt), cost, accumulated depreciation, book value, status. Subtotals and grand total. |
| R2 | Assets by Category and Sub-category | Category | Sub-category, count, cost, accumulated depreciation, book value, % of total value. |
| R3 | Assets by Campus and Department | Campus, department | Count, cost, book value; detail rows optional. |
| R4 | Custody List (assets by responsible person) | Person | Asset no, tag, name, serial, location, date assigned, book value. A signature block per person: "I confirm the assets above are in my custody", with name, signature and date. One page per person in the PDF. |
| R5 | Depreciation Schedule (financial year) | Category | Asset no, name, method, rate or life, opening value (1 August), additions, depreciation charge, revaluation, disposals, closing value (31 July), accumulated depreciation. Category totals tie to the journal summary. |
| R6 | Revaluation and Appreciation Report (period) | Category | Date, asset no, name, value before, new value, change, valuer, reference, reason, recorded by. |
| R7 | Disposals Report (period) | Disposal method | Date, asset no, name, category, carrying amount, proceeds, gain or loss, approval reference, reason, approved by. |
| R8 | Asset Movement Report (period) | Date | Date, asset no, name, from (campus, location, department, person), to (same), reason, recorded by. |
| R9 | Asset History Statement (one asset) | None | Header block with the asset's details and current position. Then the full ledger: date, type, before, change, after, reason, reference, recorded by. Then the value chart (PDF) and attachments list. |
| R10 | Verification Sheet | Campus, department, location | Asset no, tag, name, serial, recorded location, custodian, and blank columns: Found (tick), Condition, Remarks. Sign-off: verifying officer, custodian, witness, date. Recorded results come back as Verification records. |
| R11 | Register to GL Reconciliation (added) | GL account | Account, register cost, GL cost, difference, register accumulated depreciation, GL accumulated depreciation, difference. |
| R12 | Depreciation Journal Summary (added) | Run or year | GL account, account name, debit, credit. |

R11 and R12 are needed by Finance because of D4.

---

## 8. Implementation checklist

Phase 2 follows this order. Each item is ticked here when done, with any change from the plan noted beneath it.

**Database**
- [ ] 1. Copy the tested scripts to `COOPERP/sql/assets/`: `2026-10_fixed_assets_schema.sql`, `2026-10_fixed_assets_seed_categories.sql`, `2026-10_fixed_assets_menu.sql`.
- [ ] 2. Run the schema script on production (new tables only).
- [ ] 3. Run the menu script (it backs up `sys_menu_items`, `sys_role_permissions` and `sys_roles` first).
- [ ] 4. Run the seed script, **only after section 3 is approved**, with any changes made.

**Server core** (`App_Code/FixedAssets`, deployed as one batch out of hours)
- [ ] 5. `FaDb`, `FaAccess`, `FaAudit` (transaction helper, slug checks, audit plus activity log), `FaFinYear`, `FaSettings`.
- [ ] 6. `FaMath` (SL, RB, NONE, cumulative rounding, residual cap) and its console test harness (9.2), passing.
- [ ] 7. `FaCategoryService` (tree, inheritance, save, move, deactivate, codes).
- [ ] 8. `FaAssetService` (numbering, create with Acquisition or Opening, edit with row_version, list query builder, flags).
- [ ] 9. `FaPosting` (ledger replay) and `FaRecordService` (every record type, validation, catch-up, reversal rules, year locks).
- [ ] 10. `FaDepreciation` (preview, hash, post, reverse run, journal summary).
- [ ] 11. `FaExport` and `FaPdf` (adapted from Graduation), `FaReports` (R1 to R12 queries), tag label PDF.
- [ ] 12. `FaImport` (template writer, xlsx and csv reader, validator, commit).
- [ ] 13. `FaUpload.ashx`, `FaFile.ashx`.

**Front end**
- [ ] 14. `css/fa.css`, `js/fa.js` (toolkit adapted from Graduation, design rules applied).
- [ ] 15. Sidebar: the "Fixed Assets" parent and five links in `SidebarMaster.master`, plus page titles in `SetPageTitle()`.
- [ ] 16. `AssetCategories.aspx` (tree, editor, drag and drop, review and save).
- [ ] 17. `Assets.aspx` (list, filters, batch, detail modal, form, record forms, attachments, export).
- [ ] 18. `AssetImport.aspx`.
- [ ] 19. `AssetRecords.aspx` (ledger, depreciation, year locks).
- [ ] 20. `AssetsDashboard.aspx`.
- [ ] 21. `AssetReports.aspx` and the 12 reports in three formats.

**Finish**
- [ ] 22. Run the full test plan (section 9) and fix failures.
- [ ] 23. User guide, `COOPERP/docs/fixed-assets-user-guide.md`: one page per screen, plus year-end steps.
- [ ] 24. Memory note for future work, then commit and push.

---

## 9. Test plan

### 9.1 SQL (done in Phase 1)

Run against MySQL 5.6.43 in a scratch schema that held copies of `sys_menu_items`, `sys_role_permissions` and `sys_roles`. The schema was dropped afterwards.

| Check | Result |
|---|---|
| Schema, seed and menu scripts each run twice | No errors; the second run changes nothing (11 categories, 42 sub-categories, 13 menu rows, grants per section 6) |
| Second live depreciation row for the same asset and month | Refused (1062 on `uq_fa_record_dep_guard`) |
| DELETE on `fa_record`, `fa_asset`, `fa_category`, `fa_audit` | Refused by trigger (1644) |
| UPDATE of a value on `fa_record` | Refused |
| UPDATE of the reversal links only | Allowed; the same period can then be posted again |
| Value as at a date (correlated LIMIT 1 query) | Returns the right figure |
| Sequence claim with `LAST_INSERT_ID` | Works |

### 9.2 Engine unit tests

`FaMath` is compiled offline with `csc` and run from a console harness, with no database involved. The expected figures come from the University's own journals:

| # | Case | Expected |
|---|---|---|
| E1 | Coaster 240,000,000, SL 5 yrs, October 2024, run 31 Jul 2025 | 40,000,000 (journal) |
| E2 | Same asset, run 31 Jul 2026 | 48,000,000, value 152,000,000 |
| E3 | Lab equipment 399,500, SL 3 yrs, September 2024, to 31 Jul 2025 | 122,069 (journal) |
| E4 | Leasehold Mubende 216,075,000 over 49 yrs, full year | 4,409,694 (journal) |
| E5 | Leasehold Masaka 9,571,560,000 over 99 yrs, full year | 96,682,424 (journal) |
| E6 | RB 30%, 10,000,000, January 2026 | FY 2025/26: 1,750,000. FY 2026/27: 2,475,000 |
| E7 | Residual cap: 1,000,000, SL 3 yrs, residual 100,000 | Charges total exactly 900,000; the last month is trimmed |
| E8 | Cumulative rounding: 1,000,001 over 36 months, charged monthly versus annually | Same total to the shilling |
| E9 | Catch-up across two years: start August 2024, never depreciated, run 31 Jul 2026 | Two records, one per year |
| E10 | Revaluation mid-life, then SL | New charge = (new value − residual) / remaining months |
| E11 | Disposal 15 March | Charged to 28 February; March not charged; gain or loss correct |
| E12 | NONE method | No charge, ever |

### 9.3 Service and database tests (on the server, through the PageMethods)

| # | Test |
|---|---|
| S1 | Create an asset: number claimed, Acquisition record, asset row posted, audit and activity log rows present. |
| S2 | Two creates at once in the same sub-category get different numbers. |
| S3 | Every record type with its required fields missing is refused, with a clear message. |
| S4 | Each record type posts the asset row correctly (value, status, location, custodian, dates). |
| S5 | Run the same depreciation period twice: the second charges 0 assets. |
| S6 | Two concurrent posts of one period: one succeeds, the other charges nothing or rolls back whole. |
| S7 | Preview, then change an asset, then post: refused with "Values changed since the preview". |
| S8 | Reverse the newest run, then post again: same totals. Reversing an older run is refused. |
| S9 | Locked year: records dated in it, reversals in it, and runs into it are all refused. |
| S10 | Disposal without an approval reference, without a reason, or without the dispose slug: refused. With them: status, value 0, gain or loss, asset closed to further value records. |
| S11 | Void is refused after the first depreciation. |
| S12 | Edit with a stale row_version is refused ("changed by someone else, reload"). |
| S13 | Check register integrity reports 0 differences after all of the above. |
| S14 | Import: template round-trip. A file with planted errors (bad codes, dates, duplicates, unknown staff) gets every one reported and commits nothing. A clean file commits all rows. Committing twice is refused. |
| S15 | Attachments: wrong type refused, too large refused, download without the slug refused, removal hides with a reason. |

### 9.4 Security

| # | Test |
|---|---|
| P1 | Every PageMethod called anonymously returns denied JSON (no data). |
| P2 | Every PageMethod called by a user without the slug returns denied. |
| P3 | Each action is tried as each role in section 6, using a test login per role, and must succeed or fail as the matrix says. |
| P4 | CSV export guards formulas. Typed text is HTML-encoded everywhere (an asset named `<script>` shows as text). |

### 9.5 Browser (headless Edge, `browser.py`)

| # | Test |
|---|---|
| B1 | Each page loads with no JavaScript errors, at 1366 px and 390 px wide. |
| B2 | Dashboard: every tile, chart segment and action list drills into Assets with the right filter applied. |
| B3 | Categories: drag a sub-category to another parent, review, save, reload, order persisted. The keyboard "Move to" path does the same. |
| B4 | Assets: filter, page, sort, open a row, every tab, every record form, batch transfer, export dialog. |
| B5 | Every report in PDF, Excel and CSV, opened and checked: crest, cover, totals that match the screen, grouping, no em dashes. |
| B6 | Screenshots of each screen reviewed against the design rules (flat, radius 0, colours). |

### 9.6 Performance

With 5,000 synthetic assets, each with three years of records, created in a scratch schema:

| Operation | Target |
|---|---|
| Dashboard | Under 1 second |
| List page | Under 0.5 seconds |
| Depreciation preview | Under 10 seconds |
| Post | Under 30 seconds |
| Any report | Under 15 seconds |

### 9.7 Test data on production

The tables are new and empty, so tests on production would leave rows that can never be deleted (rule 1, enforced by trigger). **Proposal (Q11):**
- Run all tests before go-live using a sub-category coded `ZZ` named "Test, do not use".
- Then remove the test rows in **one approved, documented exception**: drop the triggers, remove rows where `created_by` is the test account, recreate the triggers, with a backup taken first.
- Alternatively, test only in a scratch copy of the schema.

---

## 10. Open questions for MIS and the Bursar

1. **Financial year.** Confirm 1 August to 31 July. The `fin_financial_years` rows are inconsistent: the row labelled 2026/2027 covers 1 Aug 2025 to 30 Oct 2026. Should Finance correct them? The module does not depend on them.
2. **Depreciation rates.** Confirm the rates in section 3. Buildings (2%), motor vehicles (20%), lab and engineering equipment (33.33%) and leasehold land (lease terms) match the journals. Furniture, computers, library books, software and the new categories are proposals. Is there an approved accounting policy or auditors' note to follow?
3. **GL posting.** Is version 1 register-only, with Finance posting the journal summary by hand (recommended, D4), or should the module post year-end depreciation to `fin_ledger` itself?
4. **Which chart of accounts.** Map to the AC80xx set, which holds the balances, or the newer AC90xx set? Plant and Machinery, Network and Sports have no own accounts. Should Finance create them?
5. **Capitalisation threshold.** What value (for example UGX 500,000)? The GL shows small items such as cables, a mouse and door labels capitalised as Equipment.
6. **Opening register.** No item-level register exists, only GL class totals. Who runs the physical verification and fills the import template, and by when? The proposed go-live is 1 August 2026 (start of 2026/27), with opening values as at 31 July 2026 reconciled to the GL closing balances.
7. **Locations.** Land at Mubende is not a campus in the system. Add Mubende as a site, or record it under the campus responsible with the location in the building field? Should `acad_building` be filled so building becomes a list?
8. **Roles.** Approve the new `assets_officer` role and name its holders (Estates or Stores). Should procurement create assets?
9. **Disposal approval.** Is the external approval reference (Board of Survey or Council minute) enough, or should disposals also need a second person in the system (maker and checker)?
10. **Revaluation model.** Which categories are revalued, and how often? The seed sets Land and Buildings to every 12 months, per the brief. Five years is more usual. A UGX 8.8bn revaluation reserve exists.
11. **Test data.** Approve the one-time pre-go-live clean-up in 9.7, or require testing only in a scratch schema.
12. **Pooled assets.** Library lots and furniture sets are one asset with a quantity. Partial disposal (for example 20 of 120 chairs) would need a "split" action. Build it now or later?
13. **Staff self-service.** Later, should staff see "My assets" in eportal and acknowledge custody (feeding R4)?
14. **Tags.** Is there a label printer and size, or should tags print on A4 label sheets?
15. **Department scope.** Should Heads of Department see the assets of their own department?

---

## Appendix A: schema SQL (tested)

```sql
-- ---------------------------------------------------------------------------
-- Fixed Assets module: schema (database campus_dynamics)
-- File: COOPERP/sql/assets/2026-10_fixed_assets_schema.sql
-- Creates new tables only. Touches no existing table. Safe to re-run.
-- Run with the mysql client (uses DELIMITER for the guard triggers).
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS fa_settings (
  setting_key    VARCHAR(60)  NOT NULL,
  setting_value  VARCHAR(500) NOT NULL,
  description    VARCHAR(300) NULL,
  updated_by     VARCHAR(100) NULL,
  updated_at     DATETIME     NULL,
  PRIMARY KEY (setting_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

INSERT IGNORE INTO fa_settings (setting_key, setting_value, description, updated_by, updated_at) VALUES
 ('fy_start_month',        '8',    'First month of the financial year (8 = August; year ends 31 July)', 'install', NOW()),
 ('asset_no_prefix',       'MRU',  'Prefix of generated asset numbers', 'install', NOW()),
 ('cap_threshold_default', '0',    'Default capitalisation threshold in UGX (0 = no warning)', 'install', NOW()),
 ('revalue_months_default','12',   'Default revaluation interval in months for categories on the revaluation model', 'install', NOW()),
 ('attachment_max_mb',     '10',   'Largest attachment accepted, in MB', 'install', NOW());

CREATE TABLE IF NOT EXISTS fa_category (
  id                   INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  parent_id            INT UNSIGNED      NOT NULL DEFAULT 0,      -- 0 = category, otherwise the parent category id
  code                 VARCHAR(10)       NOT NULL,
  name                 VARCHAR(120)      NOT NULL,
  description          VARCHAR(500)      NULL,
  asset_type           ENUM('TANGIBLE','INTANGIBLE') NULL,       -- NULL on a sub-category = inherit
  dep_method           ENUM('SL','RB','NONE') NULL,
  useful_life_years    DECIMAL(6,2)      NULL,
  dep_rate_pct         DECIMAL(7,4)      NULL,
  residual_pct         DECIMAL(5,2)      NULL,
  revalue_every_months SMALLINT UNSIGNED NULL,                    -- NULL = cost model, not revalued
  cap_threshold        DECIMAL(18,2)     NULL,
  gl_cost_account      VARCHAR(20)       NULL,
  gl_accum_account     VARCHAR(20)       NULL,
  gl_expense_account   VARCHAR(20)       NULL,
  next_seq             INT UNSIGNED      NOT NULL DEFAULT 1,      -- asset number sequence (sub-categories only)
  sort_order           SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  is_active            TINYINT(1)        NOT NULL DEFAULT 1,
  created_by           VARCHAR(100)      NOT NULL,
  created_at           DATETIME          NOT NULL,
  updated_by           VARCHAR(100)      NULL,
  updated_at           DATETIME          NULL,
  row_version          INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_fa_category_code (parent_id, code),
  KEY ix_fa_category_parent (parent_id, sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_asset (
  id                    INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  asset_no              VARCHAR(40)       NOT NULL,
  asset_no_edits        TINYINT UNSIGNED  NOT NULL DEFAULT 0,
  tag_no                VARCHAR(60)       NULL,
  name                  VARCHAR(200)      NOT NULL,
  description           VARCHAR(1000)     NULL,
  serial_no             VARCHAR(100)      NULL,
  model                 VARCHAR(100)      NULL,
  make                  VARCHAR(100)      NULL,
  category_id           INT UNSIGNED      NOT NULL,              -- the sub-category
  asset_type            ENUM('TANGIBLE','INTANGIBLE') NOT NULL DEFAULT 'TANGIBLE',
  quantity              INT UNSIGNED      NOT NULL DEFAULT 1,
  campus_id             INT               NOT NULL,              -- acad_campuses.ID
  building              VARCHAR(120)      NULL,
  room_id               INT               NULL,                  -- acad_lecturerooms.RoomID when it is a teaching room
  room                  VARCHAR(120)      NULL,
  department_id         INT UNSIGNED      NULL,                  -- hrm_departments.ID
  custodian_emp_id      INT UNSIGNED      NULL,                  -- hrm_employee.empID
  custodian_since       DATE              NULL,
  supplier_id           INT UNSIGNED      NULL,                  -- campus_dynamics_accounts.supplier.supplierID
  supplier_name         VARCHAR(150)      NULL,
  purchase_date         DATE              NOT NULL,
  invoice_ref           VARCHAR(100)      NULL,
  order_ref             VARCHAR(100)      NULL,
  requisition_ref       VARCHAR(40)       NULL,                  -- sys_requisitions.req_number
  funding_source        VARCHAR(120)      NULL,
  warranty_expiry       DATE              NULL,
  original_cost         DECIMAL(18,2)     NOT NULL,
  dep_method            ENUM('SL','RB','NONE') NOT NULL,
  useful_life_years     DECIMAL(6,2)      NULL,
  dep_rate_pct          DECIMAL(7,4)      NULL,
  residual_value        DECIMAL(18,2)     NOT NULL DEFAULT 0,
  dep_start_date        DATE              NOT NULL,
  -- Posted state. Written only by the posting routine, from fa_record.
  status                ENUM('IN_USE','IN_STORE','UNDER_REPAIR','LOST','DISPOSED','WRITTEN_OFF','VOID') NOT NULL DEFAULT 'IN_USE',
  current_value         DECIMAL(18,2)     NOT NULL DEFAULT 0,
  cost_basis            DECIMAL(18,2)     NOT NULL DEFAULT 0,
  accum_depreciation    DECIMAL(18,2)     NOT NULL DEFAULT 0,
  reval_surplus         DECIMAL(18,2)     NOT NULL DEFAULT 0,
  base_value            DECIMAL(18,2)     NOT NULL DEFAULT 0,
  base_date             DATE              NULL,
  base_remaining_months SMALLINT UNSIGNED NULL,
  depreciated_to        DATE              NULL,
  last_valuation_date   DATE              NULL,
  last_verified_date    DATE              NULL,
  closed_on             DATE              NULL,
  last_record_id        INT UNSIGNED      NULL,
  notes                 TEXT              NULL,
  created_by            VARCHAR(100)      NOT NULL,
  created_at            DATETIME          NOT NULL,
  updated_by            VARCHAR(100)      NULL,
  updated_at            DATETIME          NULL,
  row_version           INT UNSIGNED      NOT NULL DEFAULT 1,
  PRIMARY KEY (id),
  UNIQUE KEY uq_fa_asset_no (asset_no),
  KEY ix_fa_asset_tag (tag_no),
  KEY ix_fa_asset_cat (category_id, status),
  KEY ix_fa_asset_campus (campus_id, status),
  KEY ix_fa_asset_dept (department_id),
  KEY ix_fa_asset_custodian (custodian_emp_id),
  KEY ix_fa_asset_purchase (purchase_date),
  KEY ix_fa_asset_status (status),
  KEY ix_fa_asset_serial (serial_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_record (
  id                     INT UNSIGNED      NOT NULL AUTO_INCREMENT,
  asset_id               INT UNSIGNED      NOT NULL,
  record_type            ENUM('ACQUISITION','OPENING','DEPRECIATION','REVALUATION','APPRECIATION','TRANSFER',
                              'STATUS','MAINTENANCE','DISPOSAL','VERIFICATION','ESTIMATE','VOID','REVERSAL') NOT NULL,
  value_class            ENUM('COST','DEPRECIATION','REVALUATION','DISPOSAL','NONE') NOT NULL DEFAULT 'NONE',
  record_date            DATE              NOT NULL,
  fin_year               CHAR(9)           NOT NULL,             -- e.g. 2025/2026
  period_from            DATE              NULL,                 -- depreciation: first month covered
  period_to              DATE              NULL,                 -- depreciation: last month-end covered
  months                 TINYINT UNSIGNED  NULL,
  dep_active             TINYINT(1)        NULL,                 -- 1 on a live depreciation row, NULL otherwise
  value_before           DECIMAL(18,2)     NOT NULL,
  value_after            DECIMAL(18,2)     NOT NULL,
  change_amount          DECIMAL(18,2)     NOT NULL,
  quantity               INT UNSIGNED      NULL,
  cost_amount            DECIMAL(18,2)     NULL,                 -- maintenance cost
  is_capital             TINYINT(1)        NULL,
  status_before          VARCHAR(15)       NULL,
  status_after           VARCHAR(15)       NULL,
  from_campus_id         INT               NULL,
  to_campus_id           INT               NULL,
  from_department_id     INT UNSIGNED      NULL,
  to_department_id       INT UNSIGNED      NULL,
  from_location          VARCHAR(250)      NULL,
  to_location            VARCHAR(250)      NULL,
  from_custodian_emp_id  INT UNSIGNED      NULL,
  to_custodian_emp_id    INT UNSIGNED      NULL,
  disposal_method        ENUM('SALE','DONATION','SCRAP','WRITE_OFF','TRADE_IN','TRANSFER_OUT') NULL,
  proceeds               DECIMAL(18,2)     NULL,
  gain_loss              DECIMAL(18,2)     NULL,
  valuer                 VARCHAR(150)      NULL,
  new_remaining_months   SMALLINT UNSIGNED NULL,
  condition_grade        ENUM('GOOD','FAIR','POOR','UNSERVICEABLE') NULL,
  verified_found         TINYINT(1)        NULL,
  reason                 VARCHAR(1000)     NULL,
  reference              VARCHAR(150)      NULL,
  approval_ref           VARCHAR(150)      NULL,
  approved_by            VARCHAR(100)      NULL,
  approved_at            DATETIME          NULL,
  run_id                 INT UNSIGNED      NULL,
  import_batch_id        INT UNSIGNED      NULL,
  reverses_record_id     INT UNSIGNED      NULL,
  reversed_by_record_id  INT UNSIGNED      NULL,
  recorded_by            VARCHAR(100)      NOT NULL,
  recorded_role          VARCHAR(40)       NULL,
  recorded_at            DATETIME          NOT NULL,
  client_op_id           CHAR(36)          NULL,
  PRIMARY KEY (id),
  KEY ix_fa_record_asset (asset_id, record_date, id),
  KEY ix_fa_record_type (record_type, record_date),
  KEY ix_fa_record_year (fin_year, record_type),
  KEY ix_fa_record_run (run_id),
  KEY ix_fa_record_date (record_date),
  UNIQUE KEY uq_fa_record_dep_guard (asset_id, period_to, dep_active),
  UNIQUE KEY uq_fa_record_client_op (client_op_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_depreciation_run (
  id              INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  fin_year        CHAR(9)       NOT NULL,
  period_end      DATE          NOT NULL,
  scope_json      VARCHAR(1000) NULL,
  status          ENUM('POSTED','REVERSED') NOT NULL DEFAULT 'POSTED',
  asset_count     INT UNSIGNED  NOT NULL DEFAULT 0,
  total_amount    DECIMAL(18,2) NOT NULL DEFAULT 0,
  preview_hash    CHAR(40)      NULL,
  notes           VARCHAR(500)  NULL,
  posted_by       VARCHAR(100)  NOT NULL,
  posted_at       DATETIME      NOT NULL,
  reversed_by     VARCHAR(100)  NULL,
  reversed_at     DATETIME      NULL,
  reverse_reason  VARCHAR(1000) NULL,
  client_op_id    CHAR(36)      NULL,
  PRIMARY KEY (id),
  KEY ix_fa_run_year (fin_year, period_end),
  UNIQUE KEY uq_fa_run_client_op (client_op_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_year_lock (
  fin_year     CHAR(9)       NOT NULL,
  is_locked    TINYINT(1)    NOT NULL DEFAULT 1,
  reason       VARCHAR(1000) NULL,
  locked_by    VARCHAR(100)  NOT NULL,
  locked_at    DATETIME      NOT NULL,
  unlocked_by  VARCHAR(100)  NULL,
  unlocked_at  DATETIME      NULL,
  PRIMARY KEY (fin_year)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_attachment (
  id              INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  asset_id        INT UNSIGNED  NOT NULL,
  record_id       INT UNSIGNED  NULL,
  kind            ENUM('PHOTO','INVOICE','WARRANTY','VALUATION','DISPOSAL','OTHER') NOT NULL DEFAULT 'OTHER',
  original_name   VARCHAR(200)  NOT NULL,
  stored_name     VARCHAR(120)  NOT NULL,
  mime            VARCHAR(100)  NOT NULL,
  size_bytes      INT UNSIGNED  NOT NULL,
  sha1            CHAR(40)      NOT NULL,
  uploaded_by     VARCHAR(100)  NOT NULL,
  uploaded_at     DATETIME      NOT NULL,
  is_active       TINYINT(1)    NOT NULL DEFAULT 1,
  removed_by      VARCHAR(100)  NULL,
  removed_at      DATETIME      NULL,
  removed_reason  VARCHAR(500)  NULL,
  PRIMARY KEY (id),
  KEY ix_fa_att_asset (asset_id, is_active),
  KEY ix_fa_att_record (record_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_import_batch (
  id            INT UNSIGNED NOT NULL AUTO_INCREMENT,
  file_name     VARCHAR(200) NOT NULL,
  sha1          CHAR(40)     NOT NULL,
  rows_total    INT UNSIGNED NOT NULL DEFAULT 0,
  rows_ok       INT UNSIGNED NOT NULL DEFAULT 0,
  rows_error    INT UNSIGNED NOT NULL DEFAULT 0,
  status        ENUM('VALIDATED','COMMITTED','ABANDONED') NOT NULL DEFAULT 'VALIDATED',
  payload       MEDIUMTEXT   NOT NULL,
  report        MEDIUMTEXT   NULL,
  created_by    VARCHAR(100) NOT NULL,
  created_at    DATETIME     NOT NULL,
  committed_by  VARCHAR(100) NULL,
  committed_at  DATETIME     NULL,
  PRIMARY KEY (id),
  KEY ix_fa_import_status (status, created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

CREATE TABLE IF NOT EXISTS fa_audit (
  id           INT UNSIGNED  NOT NULL AUTO_INCREMENT,
  entity       ENUM('CATEGORY','ASSET','RECORD','RUN','LOCK','ATTACHMENT','IMPORT','SETTING') NOT NULL,
  entity_id    INT UNSIGNED  NOT NULL,
  asset_id     INT UNSIGNED  NULL,
  action       VARCHAR(40)   NOT NULL,
  before_json  TEXT          NULL,
  after_json   TEXT          NULL,
  reason       VARCHAR(1000) NULL,
  actor        VARCHAR(100)  NOT NULL,
  actor_role   VARCHAR(40)   NULL,
  ip_address   VARCHAR(45)   NULL,
  created_at   DATETIME      NOT NULL,
  PRIMARY KEY (id),
  KEY ix_fa_audit_entity (entity, entity_id, created_at),
  KEY ix_fa_audit_asset (asset_id, created_at),
  KEY ix_fa_audit_actor (actor, created_at),
  KEY ix_fa_audit_date (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

-- Guard triggers: the database itself refuses deletes and value edits.
DROP TRIGGER IF EXISTS trg_fa_record_bd;
DROP TRIGGER IF EXISTS trg_fa_record_bu;
DROP TRIGGER IF EXISTS trg_fa_asset_bd;
DROP TRIGGER IF EXISTS trg_fa_category_bd;
DROP TRIGGER IF EXISTS trg_fa_audit_bd;
DROP TRIGGER IF EXISTS trg_fa_audit_bu;

DELIMITER $$
CREATE TRIGGER trg_fa_record_bd BEFORE DELETE ON fa_record FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_record is append-only: post a reversal instead';
END$$

CREATE TRIGGER trg_fa_record_bu BEFORE UPDATE ON fa_record FOR EACH ROW
BEGIN
  -- Only the reversal links (dep_active, reversed_by_record_id) may change.
  IF NEW.asset_id <> OLD.asset_id OR NEW.record_type <> OLD.record_type
     OR NEW.record_date <> OLD.record_date OR NEW.value_before <> OLD.value_before
     OR NEW.value_after <> OLD.value_after OR NEW.change_amount <> OLD.change_amount
     OR NOT (NEW.period_to <=> OLD.period_to) OR NOT (NEW.period_from <=> OLD.period_from)
     OR NEW.value_class <> OLD.value_class OR NEW.recorded_by <> OLD.recorded_by
     OR NEW.recorded_at <> OLD.recorded_at THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_record values cannot be changed: post a reversal instead';
  END IF;
END$$

CREATE TRIGGER trg_fa_asset_bd BEFORE DELETE ON fa_asset FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_asset rows are never deleted: dispose or void the asset';
END$$

CREATE TRIGGER trg_fa_category_bd BEFORE DELETE ON fa_category FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_category rows are never deleted: deactivate the category';
END$$

CREATE TRIGGER trg_fa_audit_bd BEFORE DELETE ON fa_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_audit is append-only';
END$$

CREATE TRIGGER trg_fa_audit_bu BEFORE UPDATE ON fa_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'fa_audit is append-only';
END$$
DELIMITER ;
```

## Appendix B: seed categories SQL (tested; run only after approval)

```sql
-- ---------------------------------------------------------------------------
-- Fixed Assets module: starting categories (database campus_dynamics)
-- File: COOPERP/sql/assets/2026-10_fixed_assets_seed_categories.sql
-- RUN ONLY AFTER MIS AND THE BURSAR APPROVE THE LIST IN THE PLAN (section 3).
-- Idempotent: keyed on (parent_id, code). Inserts only; never updates.
-- ---------------------------------------------------------------------------

-- Categories (parent_id = 0)
INSERT IGNORE INTO fa_category
 (parent_id, code, name, description, asset_type, dep_method, useful_life_years, dep_rate_pct, residual_pct,
  revalue_every_months, cap_threshold, gl_cost_account, gl_accum_account, gl_expense_account, sort_order, created_by, created_at)
VALUES
 (0,'LB','Land and Buildings','Land, buildings and site works','TANGIBLE','SL',50,2.0000,0,12,NULL,'AC8022','AC8023','AC2161',10,'seed',NOW()),
 (0,'FF','Furniture and Fittings','Office, teaching and residential furniture, fixtures and fittings','TANGIBLE','SL',8,12.5000,0,NULL,NULL,'AC8021','AC8030','AC2161',20,'seed',NOW()),
 (0,'CE','Computer Equipment','Computers, servers, printers and peripherals','TANGIBLE','SL',3,33.3333,0,NULL,NULL,'AC8024','AC8026','AC2161',30,'seed',NOW()),
 (0,'OE','Office Equipment','Projectors, copiers, appliances and other office equipment','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8016','AC8041','AC2161',40,'seed',NOW()),
 (0,'LW','Laboratory and Workshop Equipment','Teaching laboratories, workshops and studios','TANGIBLE','SL',3,33.3333,0,NULL,NULL,'AC8025','AC8027','AC2161',50,'seed',NOW()),
 (0,'PM','Plant and Machinery','Generators, power and water systems, machines','TANGIBLE','SL',10,10.0000,0,NULL,NULL,'AC8016','AC8041','AC2161',60,'seed',NOW()),
 (0,'MV','Motor Vehicles','Buses, cars, pickups and motorcycles','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8035','AC8036','AC2161',70,'seed',NOW()),
 (0,'LM','Library Books and Materials','Printed and electronic library collections','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8020','AC8029','AC2161',80,'seed',NOW()),
 (0,'NC','Network and Communication Equipment','Data network, telephony, radio and CCTV','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8024','AC8026','AC2161',90,'seed',NOW()),
 (0,'SE','Sports Equipment','Field, gym and indoor sports equipment','TANGIBLE','SL',5,20.0000,0,NULL,NULL,'AC8016','AC8041','AC2161',100,'seed',NOW()),
 (0,'SW','Software and Licences','Institutional systems and software licences','INTANGIBLE','SL',4,25.0000,0,NULL,NULL,'AC8037','AC8038','AC2163',110,'seed',NOW());

-- Sub-categories. NULL defaults inherit from the parent.
INSERT IGNORE INTO fa_category
 (parent_id, code, name, description, asset_type, dep_method, useful_life_years, dep_rate_pct, residual_pct,
  revalue_every_months, cap_threshold, gl_cost_account, gl_accum_account, gl_expense_account, sort_order, created_by, created_at)
SELECT p.id, s.code, s.name, s.description, s.asset_type, s.dep_method, s.life, s.rate, NULL, NULL, NULL,
       s.gl_cost, s.gl_accum, s.gl_exp, s.sort_order, 'seed', NOW()
FROM fa_category p
JOIN (
  SELECT 'LB' pc,'FHL' code,'Freehold land' name,'Land held on freehold or mailo title; not depreciated' description,NULL asset_type,'NONE' dep_method,NULL life,NULL rate,'AC8039' gl_cost,NULL gl_accum,NULL gl_exp,10 sort_order
  UNION ALL SELECT 'LB','LHL','Leasehold land','Amortised over the lease term (Masaka 99 years, Mubende 49 years)',NULL,'SL',49,NULL,'AC8031','AC8032','AC2162',20
  UNION ALL SELECT 'LB','BLD','Buildings','Teaching, administrative and residential buildings',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'LB','EXT','External works and site improvements','Fences, roads, drainage, external lighting',NULL,'SL',20,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'FF','OFF','Office furniture','Desks, chairs, cabinets and cupboards in offices',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'FF','TCH','Teaching furniture','Lecture desks, chairs, whiteboards, examination furniture',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'FF','RES','Residential furniture','Hostel and staff housing furniture',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'FF','FIT','Fixtures and fittings','Curtains, signboards, partitions, door labels',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'CE','DSK','Desktop computers','Desktop computers including laboratory machines',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'CE','LAP','Laptops and tablets','Portable computers',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'CE','SRV','Servers and storage','Servers, storage and data centre equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'CE','PRN','Printers and scanners','Printers, scanners and multifunction printers',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'CE','PER','Peripherals and UPS units','Monitors, UPS units and other peripherals',NULL,NULL,NULL,NULL,NULL,NULL,NULL,50
  UNION ALL SELECT 'OE','PRJ','Projectors and screens','Multimedia projectors and display screens',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'OE','CPY','Photocopiers','Photocopying machines',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'OE','APL','Office appliances','Air conditioners, refrigerators, water dispensers',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'OE','SEC','Safes and security equipment','Safes, strong boxes, metal detectors',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'OE','OTH','Other office equipment','Office equipment not listed elsewhere',NULL,NULL,NULL,NULL,NULL,NULL,NULL,50
  UNION ALL SELECT 'LW','SCI','Science laboratory equipment','Science laboratory apparatus and instruments',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'LW','ENG','Engineering workshop equipment','Civil, electrical and mechanical engineering equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'LW','MED','Media and studio equipment','Cameras, sound and studio equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'LW','HTL','Hospitality training equipment','Kitchen and hotel training equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'LW','ART','Art and design equipment','Studio and design equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,50
  UNION ALL SELECT 'PM','GEN','Generators','Standby generators',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'PM','SOL','Solar and power systems','Solar installations, inverters, power systems',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'PM','WTR','Water tanks and pumps','Water storage tanks and pumps',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'PM','MCH','Machines and tools','Engraving and other machines and power tools',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'MV','BUS','Buses and coasters','Passenger buses and coasters',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'MV','CAR','Cars and pickups','Saloon cars, station wagons and pickups',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'MV','MCY','Motorcycles','Motorcycles',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'LM','BKS','Printed books','Book collections recorded by acquisition lot',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'LM','JNL','Journals and serials','Bound journals and serials',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'LM','ELB','E-library and digital resources','Perpetual electronic collections','INTANGIBLE',NULL,NULL,NULL,'AC8028','AC8040',NULL,30
  UNION ALL SELECT 'NC','NET','Switches, routers and access points','Active data network equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'NC','CAB','Structured cabling','Network cabling and cabinets',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'NC','TEL','Telephony and radio','Telephone systems, handsets and radios',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'NC','CCT','CCTV and access control','CCTV cameras, recorders and access control',NULL,NULL,NULL,NULL,NULL,NULL,NULL,40
  UNION ALL SELECT 'SE','FLD','Field and outdoor equipment','Goal posts, nets and outdoor equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'SE','GYM','Gym and fitness equipment','Gym machines and fitness equipment',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
  UNION ALL SELECT 'SE','IND','Indoor games equipment','Tables and equipment for indoor games',NULL,NULL,NULL,NULL,NULL,NULL,NULL,30
  UNION ALL SELECT 'SW','ERP','Institutional systems','ERP, finance and library systems',NULL,NULL,NULL,NULL,NULL,NULL,NULL,10
  UNION ALL SELECT 'SW','LIC','Software licences','Perpetual or multi-year software licences',NULL,NULL,NULL,NULL,NULL,NULL,NULL,20
) s ON s.pc = p.code AND p.parent_id = 0;
```

## Appendix C: menu, slugs, role and grants SQL (tested)

```sql
-- ---------------------------------------------------------------------------
-- Fixed Assets module: menu, permission slugs, role and grants (database campus_dynamics)
-- File: COOPERP/sql/assets/2026-10_fixed_assets_menu.sql
-- Backs up the three RBAC tables first. Idempotent.
-- Undo: DELETE FROM sys_role_permissions WHERE granted_by='fixed-assets-2026-10';
--       UPDATE sys_menu_items SET is_active=0 WHERE menu_slug LIKE 'accounts.assets%';
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS sys_menu_items_bak_20261006       AS SELECT * FROM sys_menu_items;
CREATE TABLE IF NOT EXISTS sys_role_permissions_bak_20261006 AS SELECT * FROM sys_role_permissions;
CREATE TABLE IF NOT EXISTS sys_roles_bak_20261006            AS SELECT * FROM sys_roles;

-- Menu: a parent under the Expenditure & Accounts heading, five pages, then action slugs (no url).
INSERT INTO sys_menu_items (menu_slug, label, section, item_type, parent_slug, url, sort_order, is_active, created_at) VALUES
 ('accounts.assets',                   'Fixed Assets',                    'accounts','parent', 'accounts',        NULL,                                          360,1,NOW()),
 ('accounts.assets.dashboard',         'Assets Dashboard',                'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetsDashboard.aspx',   361,1,NOW()),
 ('accounts.assets.categories',        'Asset Categories',                'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetCategories.aspx',   362,1,NOW()),
 ('accounts.assets.register',          'Assets',                          'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/Assets.aspx',            363,1,NOW()),
 ('accounts.assets.records',           'Asset Records',                   'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetRecords.aspx',      364,1,NOW()),
 ('accounts.assets.reports',           'Asset Reports',                   'accounts','subitem','accounts.assets', '~/COOPERP/NewScreens/AssetReports.aspx',      365,1,NOW()),
 ('accounts.assets.edit',              'Fixed Assets: create and edit assets',        'accounts','subitem','accounts.assets',NULL,366,1,NOW()),
 ('accounts.assets.value',             'Fixed Assets: depreciation and revaluation',  'accounts','subitem','accounts.assets',NULL,367,1,NOW()),
 ('accounts.assets.transfer',          'Fixed Assets: transfers and status changes',  'accounts','subitem','accounts.assets',NULL,368,1,NOW()),
 ('accounts.assets.dispose',           'Fixed Assets: disposal and write-off',        'accounts','subitem','accounts.assets',NULL,369,1,NOW()),
 ('accounts.assets.categories_manage', 'Fixed Assets: manage categories',             'accounts','subitem','accounts.assets',NULL,370,1,NOW()),
 ('accounts.assets.import',            'Fixed Assets: import from Excel',             'accounts','subitem','accounts.assets',NULL,371,1,NOW()),
 ('accounts.assets.yearlock',          'Fixed Assets: lock and unlock financial years','accounts','subitem','accounts.assets',NULL,372,1,NOW())
ON DUPLICATE KEY UPDATE label=VALUES(label), url=VALUES(url), parent_slug=VALUES(parent_slug),
                        sort_order=VALUES(sort_order), is_active=1;

-- New role for Estates and Stores staff who keep the register (no users assigned here).
INSERT INTO sys_roles (role_code, role_name, description, color_hex, is_system_role, is_active, created_by, created_at)
SELECT 'assets_officer','Assets Officer','Keeps the fixed asset register: records, tags, transfers and verification','#174DA4',0,1,'fixed-assets-2026-10',NOW()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_roles WHERE role_code='assets_officer');

-- Grants (admin has every slug through the wildcard).
INSERT INTO sys_role_permissions (role_id, menu_slug, can_view, can_edit, can_delete, granted_by, granted_at)
SELECT r.id, g.slug, 1, 0, 0, 'fixed-assets-2026-10', NOW()
FROM sys_roles r
JOIN (
            SELECT 'bursar' rc,'accounts.assets' slug
  UNION ALL SELECT 'bursar','accounts.assets.dashboard'   UNION ALL SELECT 'bursar','accounts.assets.categories'
  UNION ALL SELECT 'bursar','accounts.assets.register'    UNION ALL SELECT 'bursar','accounts.assets.records'
  UNION ALL SELECT 'bursar','accounts.assets.reports'     UNION ALL SELECT 'bursar','accounts.assets.edit'
  UNION ALL SELECT 'bursar','accounts.assets.value'       UNION ALL SELECT 'bursar','accounts.assets.transfer'
  UNION ALL SELECT 'bursar','accounts.assets.dispose'     UNION ALL SELECT 'bursar','accounts.assets.categories_manage'
  UNION ALL SELECT 'bursar','accounts.assets.import'      UNION ALL SELECT 'bursar','accounts.assets.yearlock'
  UNION ALL SELECT 'accountant','accounts.assets'         UNION ALL SELECT 'accountant','accounts.assets.dashboard'
  UNION ALL SELECT 'accountant','accounts.assets.categories' UNION ALL SELECT 'accountant','accounts.assets.register'
  UNION ALL SELECT 'accountant','accounts.assets.records' UNION ALL SELECT 'accountant','accounts.assets.reports'
  UNION ALL SELECT 'accountant','accounts.assets.edit'    UNION ALL SELECT 'accountant','accounts.assets.value'
  UNION ALL SELECT 'accountant','accounts.assets.categories_manage'
  UNION ALL SELECT 'finance_officer','accounts.assets'    UNION ALL SELECT 'finance_officer','accounts.assets.dashboard'
  UNION ALL SELECT 'finance_officer','accounts.assets.categories' UNION ALL SELECT 'finance_officer','accounts.assets.register'
  UNION ALL SELECT 'finance_officer','accounts.assets.records' UNION ALL SELECT 'finance_officer','accounts.assets.reports'
  UNION ALL SELECT 'assets_officer','accounts.assets'     UNION ALL SELECT 'assets_officer','accounts.assets.dashboard'
  UNION ALL SELECT 'assets_officer','accounts.assets.categories' UNION ALL SELECT 'assets_officer','accounts.assets.register'
  UNION ALL SELECT 'assets_officer','accounts.assets.records' UNION ALL SELECT 'assets_officer','accounts.assets.reports'
  UNION ALL SELECT 'assets_officer','accounts.assets.edit' UNION ALL SELECT 'assets_officer','accounts.assets.transfer'
  UNION ALL SELECT 'assets_officer','accounts.assets.import'
  UNION ALL SELECT 'procurement','accounts.assets'        UNION ALL SELECT 'procurement','accounts.assets.register'
  UNION ALL SELECT 'procurement','accounts.assets.edit'
  UNION ALL SELECT 'auditor','accounts.assets'            UNION ALL SELECT 'auditor','accounts.assets.dashboard'
  UNION ALL SELECT 'auditor','accounts.assets.categories' UNION ALL SELECT 'auditor','accounts.assets.register'
  UNION ALL SELECT 'auditor','accounts.assets.records'    UNION ALL SELECT 'auditor','accounts.assets.reports'
  UNION ALL SELECT 'vc','accounts.assets'                 UNION ALL SELECT 'vc','accounts.assets.dashboard'
  UNION ALL SELECT 'vc','accounts.assets.register'        UNION ALL SELECT 'vc','accounts.assets.reports'
) g ON g.rc = r.role_code
WHERE NOT EXISTS (SELECT 1 FROM sys_role_permissions x WHERE x.role_id = r.id AND x.menu_slug = g.slug);
```
