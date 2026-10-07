# General Ledger: user guide

For the Office of the University Bursar, Muteesa I Royal University. October 2026.

The General Ledger screens are under **Expenditure & Accounts > General Ledger** in the sidebar.

They show the university's ledger as it is: statements, ledgers, checks and warnings. They never change an existing ledger line.

The only way they add to the ledger is an **adjusting entry**. It needs a reason, a preview and a second person's approval.

---

## 1. Who can do what

| Role | Dashboard, reports, account card, voucher | Finance Warnings | Act on warnings | Make adjusting entries | Approve adjusting entries | Periods | Sign off periods |
|---|---|---|---|---|---|---|---|
| University Bursar | Yes | Yes | Yes | Yes | Yes (never your own) | Yes | Yes |
| Accountant | Yes | Yes | Yes | Yes | No | Yes | No |
| Finance Officer | Yes | Yes | No | No | No | Yes | No |
| Auditor | Yes | Yes | No | No | No | Yes | No |
| Vice Chancellor | Yes | Yes | No | No | No | Yes | No |

"Act on warnings" means:
- acknowledge, reopen, assign and add notes;
- accept single records;
- confirm account mappings;
- run the checks on demand.

---

## 2. Accounts dashboard

The first screen shows the figures at a glance:
- cash and bank;
- what students owe and what is owed to suppliers;
- income, expenditure and the surplus for the year;
- the trial balance difference;
- open critical warnings;
- monthly income against expenditure, spending by sub-category, bank balances and the health score.

**Every figure opens the report it comes from**, and shows the same amount there.

**What students owe** is the canonical student balance, the figure on student statements. The ledger's own student lines are shown beside it but never added to it.

---

## 3. Reports

Open **Accounts Reports**, choose a report on the left, set its parameters and press **Run report**.

| Group | Reports |
|---|---|
| Statements | R01 Trial Balance, R04 Income and Expenditure, R05 Statement of Financial Position |
| Ledgers and vouchers | R02 General Ledger and Account Statement, R03 Journal and Voucher Listing, R15 Account Movement over Time |
| Cash and bank | R06 Cash Flow, R07 Cash Book |
| Receivables and payables | R10 Payables Ageing, R11 Receivables Ageing and Student Fees Position, R17 Control Account Reconciliations |
| Spending | R08 Budget against Actual, R09 Expenditure Analysis, R12 Requisition to Payment Trace, R13 Payment Voucher Register |
| Control and audit | R14 Period Close Summary, R16 Audit Trail, R18 Unbalanced Vouchers, R19 Duplicate Postings, R20 Chart of Accounts |

Things that work the same in every report:
- **Financial year** fills the dates for you. Change a date and it switches to custom dates.
- **The values you last used** come back the next time you open the report.
- **Save these filters** keeps a named set of parameters. Choose it from the saved filters list to run it again.
- **Click a column heading** to sort. Type in the search box to filter the rows.
- **Click a row** to open what is behind it. The trial balance opens the account card; a statement line opens the voucher.
- **Checks.** The panel above the results says whether the figures pass their checks. If they do not, it says why, by how much and where to fix it.
- **Export** downloads exactly what the screen shows, with the same rows, totals and checks, as PDF, Excel or CSV. The parameters are printed on the cover.
- **Refresh figures** re-reads the ledger. Figures are otherwise kept for five minutes, and refresh at once when anything new is posted.

### Reading the trial balance

**The trial balance does not balance today.** Total debit exceeds total credit by UGX 4,030,430,035.

The Checks panel shows that the whole difference comes from vouchers that do not balance, and the parts add up exactly:

| Kind of voucher | Vouchers | Effect on the difference |
|---|---|---|
| Debit only | 19,281 | +9,692,277,816 |
| Credit only | 12,869 | (5,905,730,080) |
| Voucher number reused | 9,378 | +289,677,299 |
| Two-sided but unequal | 132 | (49,745,000) |
| No voucher number | 7 lines | +3,950,000 |

Choose **List them** to see the vouchers of each kind.

**Basis.** The trial balance has two bases:
- **Whole ledger** (the default) shows every chart account plus each subsidiary ledger as one line. The subsidiary ledgers are students, faculty fee lines, suppliers, staff advances, gratuity and sponsors.
- **Chart accounts only** leaves the subsidiary ledgers out.

**Provisional mappings.** Codes that the chart no longer contains but postings still use are shown under a provisional mapping, marked "(provisional)". For example, AC6007 Functional Fees is income. The Bursar confirms each mapping on the account card.

---

## 4. Account card and voucher

The **account card** shows:
- an account's opening and closing balance, its debits and credits;
- its movement by month;
- every line, with a running balance;
- any warnings on it.

For a subsidiary ledger such as students, the **Members** tab lists each student or supplier with their balance; click one to see their own statement.

For a code missing from the chart, the card shows its mapping. With permission, you can **confirm or change** it with a reason.

The **voucher** page shows every line carrying one voucher number:
- whether the voucher balances, and if not, which kind of problem it is;
- the journal and fee records behind it;
- any lines that were later edited or deleted under that number.

From an unbalanced voucher you can **Start an adjusting entry** to complete it, or **Accept as known** with a reason.

---

## 5. Finance Warnings

The checks run every six hours. They also run when the page is opened and the last run is older than that, and on demand with **Run the checks now**.

They look for 22 kinds of problem. Each warning shows:
- what was found, where, and how many records and how much money it involves;
- the likely cause;
- links to the report or screen where it is dealt with.

**Severity:** Critical, High, Medium and Information.

**Status:**

| Status | Meaning |
|---|---|
| Open | Found and not yet dealt with. |
| Acknowledged | Accepted for now, with a reason. If it grows, it becomes Reappeared. |
| Fixed | No longer found by the checks. |
| Reappeared | Found again after it was fixed, or grew after it was acknowledged. |

**Health score** = 100 less 15 for each critical rule, 6 for each high rule and 2 for each medium rule that has an open warning. Acknowledged warnings do not count. **Where the points are lost** shows which rules cost the most.

What you can do:
- **View all records** finds the records again now, not from memory. For unbalanced vouchers, reused numbers, duplicates, double bills and large postings, you can **Accept** a single record with a reason; it is left out from the next run.
- **Acknowledge** a whole warning with a reason, or **Reopen** it.
- **Assign** it to a member of the finance team, and **Add a note**.
- **History** shows each run and whether each problem is falling.

Nothing on this page changes the ledger. To correct the ledger, use an adjusting entry.

---

## 6. Adjusting entries

An adjusting entry is the only way these screens add to the ledger. Existing lines are never edited or deleted; a mistake is corrected by another entry.

There are two kinds:
- **Balanced entry.** Debit equals credit, under a new voucher number from 900,000,001 upwards. Use it to move an amount between accounts, reverse a duplicate, clear the suspense account or close a year.
- **Complete an unbalanced voucher.** It adds the missing side under the voucher's own number. It is accepted only if the voucher then balances exactly. **This is the kind that reduces the trial balance difference**; a balanced entry cannot.

**Making one (Accountant or Bursar):**
1. **New adjusting entry**, or **Start an adjusting entry** from a voucher or a warning. A voucher pre-fills the missing amount.
2. **Step 1: purpose and reason.**
   - Choose the kind and the entry date: the date the correction belongs to.
   - For "complete a voucher", give the voucher number. **Show it** lists its lines and how much is missing.
   - Write the reason: what is wrong, how you know, and what the entry does.
3. **Step 2: lines.** Choose each account by code or name. For a student or supplier line, give the registration number or supplier code. The bar below the lines says whether the entry balances, or whether the voucher will balance.
4. **Step 3: check and save.** The preview shows each account's balance before and after, and the trial balance difference before and after. **Save as draft**.
5. Open the draft and **Submit for approval**. While it waits you can **Take back to draft** or **Cancel** it.

**Approving one (Bursar):**
- Open it from **Waiting for approval**, read the reason and lines, and choose **Approve and post** or **Reject** with a reason.
- You cannot approve an entry you made yourself.
- If the entry date falls in a signed-off month or year, you must record why it has to be posted there.
- On posting, the system checks the ledger grew by exactly the entry's lines and the voucher balances. If any check fails, nothing is posted.

**Correcting a posted entry:** choose **Prepare a reversal**. It drafts the opposite entry for you to check and submit.

---

## 7. Periods and close

**Periods and close** lists each month of a financial year, and the year itself. Each row shows its lines, the difference, the unbalanced vouchers and its review status.

Choose a period to run its checklist:
- debit equals credit, and every voucher balances;
- every code is in the chart or mapped;
- no journal is still pending;
- control accounts agree with their subsidiary ledgers;
- nothing is parked in a suspense account;
- lines were recorded on time, and nothing was posted after a sign-off;
- for a year, that income and expenditure were closed to retained earnings.

**Monthly routine (Bursar):**
1. When a month ends, open it and read the checklist.
2. Deal with what you can: adjusting entries, pending journals.
3. **Record a review**, or **Sign off**.

You can sign off with failing checks, but you must explain why the figures can be accepted. The checklist as it stood is kept with your sign-off. **Reopen** if the month needs more work.

A sign-off does not stop postings. Anything posted into a signed-off period afterwards is flagged on Finance Warnings.

**Year end:**
1. On the year, **Prepare the closing entry**. It drafts one line per income and expense account, moving the year's net result to retained earnings (AC7008 by default).
2. Check the draft, submit it, and have it approved.

No year has been closed before, so the 2024/25 draft includes every earlier balance. On 31 July 2025 that is a net deficit of UGX 5,450,785,052.

---

## 8. Good to know

- **Two fee stores are never added together.** Student fees appear in the ledger and in fee tracking. The reports show them side by side and never sum them.
- **Classic screens.** Items marked "(classic)" in the sidebar are the older screens these replace. They still work, but their figures may disagree with these; these are checked.
- **Nothing is hidden.** Unclassified codes, the suspense account and every unbalanced voucher appear in the statements and checks rather than being netted away.
- **Questions or a figure that looks wrong:** use the Checks panel first; it usually explains the figure. Then tell MIS, with the report, its parameters and the export.
