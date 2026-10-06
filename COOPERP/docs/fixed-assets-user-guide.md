# Fixed Assets: user guide

For the Office of the University Bursar, Accounts, and Estates and Stores staff.
Open it from the sidebar: **Assets > Fixed Assets**. What you see depends on your role (see the end of this guide).

---

## How the register works, in one paragraph

Every asset has a **ledger of records**: its acquisition, every depreciation charge, revaluation, transfer, repair, verification and, at the end, its disposal. The asset's value, status, location and responsible person always come from that ledger. They are never typed in directly. A record cannot be edited or deleted. A mistake is corrected by **reversing** the record, which keeps both on file. Every change records who made it, when and why.

---

## 1. Dashboard

The first screen. Figures and charts only.

- **Filters:** campus, category, sub-category and a date range. Values are shown **as at the "To" date**, so to see the position at 31 July 2026, set To to 31 Jul 2026.
- **Click anything** (a figure, a bar, a slice, a list) to open the Assets screen already filtered to it.
- **Needs attention** lists, as of today:

| List | Meaning |
|---|---|
| No responsible person | Assets held with nobody assigned |
| Not yet tagged | No physical tag or barcode recorded |
| Revaluation due | Categories that are revalued (Land and Buildings) and are past their interval |
| Useful life ended | Fully depreciated but still in use |
| Not verified in 12 months | No physical count recorded in the last year |

## 2. Assets

The register.

- **Find assets:** search by asset number, tag, name or serial number, or filter by campus, category, status, department, responsible person or year acquired. Filters stay in the page address, so you can bookmark or share a view.
- **Open an asset:** click its row. Four tabs:

| Tab | Contents |
|---|---|
| Overview | Details, values and a value-over-time chart |
| Records | The ledger |
| Attachments | Photos, invoices, warranties, valuation and disposal papers |
| Change history | Who changed what |

- **New asset** (top right):
  - Required: name, sub-category, campus, status (In use or In store), purchase date and purchase value.
  - The asset number is generated when you choose the sub-category, for example `MRU-CE-LAP-00012`. You may change it **once**, for example to match a number already engraved on the asset.
  - Depreciation settings come from the sub-category. Change them only if this asset is different.
  - **Bought before the register started?** Tick *Opening balance*. Enter the opening date (normally 31 July) and either type the depreciation already charged or choose *Calculate it*.
- **Add a record** (in the asset window): see section 3.
- **Select several assets** with the tick boxes to transfer them, change their status, record a verification, print their tags or export them together.
- **Print tags:** A4 sheets of 24 labels (3 across, 8 down, 70 by 37 mm), each with a barcode of the asset number.
- **Export:** the Fixed Asset Register as PDF, Excel or CSV. Choose the columns and how to group the rows.

### Editing an asset

Name, description, serial number, make, model, tag, supplier, invoice and notes can be edited at any time; you must give a reason. Some things are deliberately **not** edited here:

| To change | Use |
|---|---|
| Location, department or responsible person | A **Transfer** record |
| Status (In use, In store, Under repair, Lost) | A **Status change** record |
| Depreciation method, life or residual value after depreciation has been posted | A **Change of estimate** record |
| Cost or purchase date after depreciation has been posted | Reverse the later records first, or record a revaluation |

## 3. Records you can add to an asset

| Record | When to use it | What you must give |
|---|---|---|
| Transfer | Moved to another campus, building, room or department, or handed to another person | What changes, and a reason |
| Status change | In use, In store, Under repair or Lost | New status, reason |
| Verification | Seen (or not seen) during a physical count | Found or not, condition |
| Maintenance or repair | Work done on the asset | Cost and description. Tick *capital* only if the work adds to its value (an upgrade, not a repair) |
| Manual depreciation | A charge outside a depreciation run (rare) | Amount, reason |
| Revaluation | New value from a valuer or committee | New value, valuer, report reference, reason |
| Appreciation | An increase in value (usually land and buildings) | New value, reference, reason |
| Change of estimate | Method, useful life or residual value is revised | New values, reason |
| Disposal or write-off | Sold, donated, scrapped, traded in or written off | Method, proceeds, **approval reference** (Board of Survey or Council minute), reason |
| Void | Entered in error, for example a duplicate. Only before any depreciation | Reason |

**Catch-up:** if depreciation is behind when you record a revaluation, improvement, change of estimate or disposal, the system shows the amount due up to the end of last month and posts it first, together with your record. The value is then right on the date of the event.

**Disposed assets** are closed. To reopen one (for example a cancelled sale), reverse the disposal.

**Reversing:** in the Records tab, *Reverse* appears only where it is allowed. That is the newest value record of the asset, or the newest record of its kind. Reverse later records first.

## 4. Asset records

- **Ledger:** every record across the register for a period, with totals for additions, depreciation, revaluation and disposals. Click a row to open the asset.
- **Depreciation runs:**
  1. Choose the **period end** (normally 31 July) and optionally a campus or category. Press **Preview**.
  2. Check the totals by category and the list of assets not charged, with the reason for each.
  3. Press **Post**. The run charges exactly what the preview showed. If anything changed meanwhile, it asks you to preview again.
  4. **Journal** shows the debits and credits per general ledger account. Post these through Journal Entries: the register does not post to the general ledger itself.
  5. A run can be **reversed** only while it is the newest.

  Each asset is charged by month: the month it was bought counts in full, and the month it is disposed of is not charged. A month is never charged twice, so running monthly and then annually is safe.
- **Year locks:** once a financial year's accounts are final, lock it. Nothing can then be dated in it, posted into it or reversed from it. Only the Bursar can lock or unlock.
- **Check register:** confirms every asset's values agree with its records.

## 5. Categories

- Categories (for example Computer Equipment) hold sub-categories (for example Laptops and tablets). Each carries default depreciation settings and general ledger accounts.
- A sub-category inherits anything left blank from its category.
- **Changing defaults affects new assets only.** Existing assets keep the settings they were created with.
- **Reorder** by dragging a category by its handle, or with Up and Down. **Move a sub-category** by dragging it onto another category, or with its *Move to* list. Changes are saved together after you review them. Moving one that holds assets needs a reason.
- **Deactivate** a category or sub-category to stop it being offered for new assets. It is never deleted.

## 6. Import from Excel

1. **Download the template.** It has three sheets:
   - Assets: one row per asset; keep the header row.
   - Lists: the valid codes, also offered as drop-downs.
   - Notes: what goes in each column.
2. **Fill it in and upload it** (.xlsx or CSV, up to 3,000 rows).
3. **Read the report.** Every row is checked exactly as the import would save it. Errors are listed by row and column. Warnings, such as a possible duplicate, do not stop the import.
4. **Import.** Either every row is saved or none is.

For the opening register, fill in `opening_date` (31 July) and `opening_accum_dep` (an amount, or CALC).

## 7. Reports

Choose a report, set its filters, press **Preview** to see the first 100 rows on screen, then **Export** to PDF, Excel or CSV.

| Report | Use |
|---|---|
| Fixed Asset Register | Full list with values and custodians, as at a date |
| Assets by Category and Sub-category | Counts and values with category totals |
| Assets by Campus and Department | Counts and values per department |
| Custody List | One signable page per responsible person |
| Depreciation Schedule | Opening, brought in, additions, depreciation, revaluation, disposals and closing value for a year |
| Revaluation and Appreciation Report | Value changes in a period |
| Disposals Report | Disposals with proceeds and gain or loss |
| Asset Movement Report | Transfers in a period |
| Asset History Statement | The full ledger of one asset |
| Verification Sheet | Printable count sheet with columns to tick |
| Register to General Ledger Reconciliation | Register values against ledger balances per account |
| Depreciation Journal Summary | Debits and credits to post for a run or a year |
| Asset Records Ledger | Every record in a period |

PDFs carry the University crest, the filters used, the date and who produced them, totals, and signature lines.

## Who can do what

| Role | Access |
|---|---|
| University Bursar | Everything, including disposals, year locks and categories |
| Accountant | View everything, create and edit assets, depreciation, revaluation, categories |
| Finance Officer | View everything and run reports |
| Assets Officer (Estates or Stores) | View, create and edit assets, transfers, status changes, verification, import, reports |
| Procurement | View the register and add newly bought assets |
| Auditor | View everything, read only |
| Vice-Chancellor | Dashboard, register and reports |

Ask MIS to give a member of staff the Assets Officer role.

## Year-end checklist (31 July)

1. Record any transfers, disposals and verifications for the year.
2. Asset Records > Depreciation runs: period end 31 July, Preview, then Post.
3. Open the Journal for the run and post it through Journal Entries.
4. Reports: Depreciation Schedule and Register to General Ledger Reconciliation for the year.
5. After the audit, lock the year.
