# Graduation training reset, 25 September 2026

The Graduation Centre was used for training on 24 and 25 September 2026. Everything it
recorded in that window was test data: the decision reasons read "this is for just simple
testing", and every row was written by one account within about twenty hours.

## What was removed

| Table | Rows | What they were |
|---|---|---|
| `acad_graduands` where `acadyear='2026/2027'` | 8 | The current graduation list, created during training |
| `acad_grad_review` | 23 | Every decision ever recorded: 15 cleared, 2 held, 6 released |
| `acad_graduands_archive` | 18 | The archive kept by the earlier reset, itself training data |

`acad_grad_stats.on_list` was also cleared for anyone no longer on the list, because the
candidate queue reads that flag and would otherwise still show them as already listed.

## What was kept

All 1,607 historical graduation records, 2025/2026 and earlier. Those are real graduands,
they predate the Graduation Centre, none of them has a decision recorded against it, and
the Analysis tab, the transcript printing and the public verification page all read them.
They were never training data and were not touched.

## Restoring

The full contents of all three tables as they stood before the reset are in the .sql file
beside this note. It is a data-only dump with complete column lists, so it can be replayed
into the same schema.
