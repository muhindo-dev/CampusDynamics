# Repeated payment postings: what happened, what was fixed, what now prevents it

Investigated 30 September 2026. Scoped to 2026 onward.

---

## The case that was reported

MRU2024000688 showed payments of UGX 24,064,000 against bills of UGX 14,222,000. One
Centenary deposit of **UGX 1,455,000** had been posted to the ledger **nine times**:

| Voucher | Posted | By |
|---|---|---|
| 116522 | 19 May 12:48:53 | sharifah |
| 116549 | 19 May 13:30:23 | autocapture |
| 116575 | 19 May 14:15:31 | sharifah |
| 116626 | 19 May 16:04:07 | autocapture |
| 116627 | 19 May **16:04:43** | autocapture |
| 116628 | 19 May **16:05:14** | autocapture |
| 116629 | 19 May **16:05:28** | autocapture |
| 116634 | 19 May 16:27:35 | autocapture |
| 116956 | 20 May 13:36:12 | sharifah |

Four of them inside eighty-one seconds, by two operators, across two days. That is somebody
clicking again because nothing appeared to happen, and somebody else later trying the same
deposit afresh.

## Why nothing caught it

Each posting allocated its **own voucher number** and wrote a balanced CR/DR pair, so the
books never looked wrong. The student simply appeared to have paid nine times.

`fin_ApproveStudentReceipt` does guard itself: it posts only while the source voucher reads
`New`, then flips it. So this was **not** one receipt approved nine times. These rows carry no
source voucher at all, which means the deposit was entered from scratch on each attempt.

**Nothing anywhere refused a payment identical to one already on the account.** Every attempt
therefore succeeded, and succeeding silently is what invited the next one.

## What else was checked, and found clean

The reported case is the only one this year.

| Checked | Result |
|---|---|
| `fin_ledger`, student fee credits, 2026 onward | **1 group** (this one), 8 extra postings, UGX 11,640,000 |
| `fin_ledger`, student fee credits, **all history** | **1 group**. The same one. |
| `fin_studentfeestracking`, 2026 onward | **0** |
| `fin_studentfeestracking`, all history | **0** |
| SchoolPay (`fin_schoolpaydata`), 2026 onward | **0 duplicate receipts**, 15,717 rows all Captured |

One near miss worth recording. A first pass found "25 duplicate bursary payments" in the
tracking table. They were not duplicates: the rows carried the **same amount, narrative and
date but different acadyear and semester**, which is one bursary award split across terms.
Grouping without the term would have deleted legitimate allocations. Including it returns
zero.

Likewise the repeated `RefNo` values (5215, 5156, 5281) that looked alarming are chart
account, bank and supplier lines sharing one voucher reference across many different students
and amounts. Not duplicates either.

## What was deleted

Sixteen rows: the eight duplicate postings, **both sides of each**, so double entry stays
balanced. Voucher 116522, the first posting at 12:48:53, was kept.

- Before: UGX 13,095,000 credited from this one deposit
- After: **UGX 1,455,000**, which is what was actually banked
- Withdrawn: **UGX 11,640,000** of credit the student never paid

All eighteen rows as they stood are in `fin_ledger-kasumba-duplicate-postings-20260930-145426.sql`
beside this note. Double entry re-verified: the remaining pair nets to zero.

## What now prevents it

### A refusal at the database

The `BEFORE INSERT` trigger on `fin_ledger` now refuses a student fee credit identical to one
already on that account within 180 days:

```
Duplicate payment refused for MRU2024000688: same amount and narrative
already posted 2026-05-19. Check the ledger.
```

**Why it is safe.** Across the entire history of the ledger, exactly one group of student fee
credits shares a student, an amount and a narrative: this incident. Zero false positives,
ever. The narratives carry a bank reference or a mobile-money transaction number, so two
genuine payments never read the same.

**Why it is narrow.** Chart account, bursary and supplier lines repeat identically as a matter
of course. Applying the same rule to everything would have rejected 571 legitimate postings in
2026 alone, so the trigger touches student fee credits only.

Two things about the implementation are worth knowing if it is ever edited:

- MySQL 5.6 allows **one** `BEFORE INSERT` trigger per table and `fin_ledger` already had one.
  The duplicate check was **appended to the existing trigger**, whose original body is
  reproduced unchanged: the three validations, and the teller alias mapping that rewrites a
  shared login to the account it posts as. Losing that would be a worse bug than the one fixed.
- `MESSAGE_TEXT` is capped at **128 characters**. The first version overflowed it, and MySQL
  replaced the whole explanation with "Data too long for condition item", which tells an
  operator nothing. The message is now wrapped in `LEFT(..., 128)`.

### A report for what a rule cannot decide

`fin_duplicate_payment_watch` lists anything that looks the same across **both** tables that
carry a student's money, with how many postings, how much is overstated and how far apart they
are. It is a report, not a control: nothing is blocked on the strength of it. It runs in under
three seconds and currently returns **nothing**.

## Tested

| Test | Result |
|---|---|
| Re-post the exact duplicate | **refused**, with the readable message |
| A genuine different payment, same student, same amount | allowed |
| A chart-account line repeating identically | allowed (out of scope) |
| `transaction_amount = 0` | still refused by the original validation |
| Teller alias mapping | still present in the trigger |
| Outstanding duplicate groups after the fix | **0** |

Every test ran inside a transaction that was rolled back.

## One thing that went wrong during the investigation

An exploratory query joining SchoolPay receipts to the ledger with `LIKE CONCAT('%',...,'%')`
was a full scan over a large table. It timed out at two minutes and **was left running**,
holding a metadata lock on `fin_ledger` for fourteen minutes. Trigger DDL queued behind it,
and then real traffic queued behind that: the portal's balance queries and
`mobile_stud_summary` were stalled for up to three minutes.

It was found by checking the process list, and killed. The lesson is not about the query but
about the timeout: a statement that times out on a shared database has not stopped, and
leaving it is what turns a slow query into an outage.
