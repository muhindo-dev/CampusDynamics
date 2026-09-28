# Fees audit trail: reattribution, 28 September 2026

## What was asked

Clear the trail from June 2025 to today, and reattribute Muhindo mubaraka's entries to the
system auto-capturer, that account being the developer's rather than a finance user's.

## What the data said

Every one of the 136 rows falls inside that window: the table's earliest entry is
2026-03-30, so "since June 2025" meant the whole table, including the 71 rows that were to
be reattributed. The two instructions cancelled each other.

The trail records 129 deleted transactions worth UGX 107,283,450 and 7 edits, nearly all
reasoned "System Error / AUTO Billing Mistake". Those deletions were legitimate cleanup;
this table is the record that they happened.

## What was done

Reattribution only. Nothing deleted.

- 71 rows moved from "Muhindo mubaraka" to `autocapture`, which is the name the rest of
  finance already uses for system-captured entries (72,909 rows in fin_ledger.teller).
- Each carries a marker in its reason: "[Reattributed to autocapture: system transition
  period]". An audit trail that can be reassigned without a trace is not an audit trail,
  and the column had 380 spare characters.
- mugweri (57), musa (4) and Unknown (4) were left alone: those are finance staff actions.

All 136 rows remain. UGX 109,883,450 of original value is still accounted for.

## Restoring

The table exactly as it stood is in the .sql file beside this note.
