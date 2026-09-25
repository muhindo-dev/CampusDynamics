# Graduation: how it works

A guide for staff who use the Graduation module. It goes tab by tab, in the order you
would normally use them.

The module lives in the sidebar under **Graduation**, and has five tabs:

| Tab | What it is for |
|---|---|
| Graduation Centre | Where the cycle stands. Numbers only, no decisions. |
| Candidates | The queue. Where clearing and holding happen. |
| Graduation List | Who has been approved. The finished list. |
| Held Candidates | Everyone stopped for a reason, and why. |
| Graduation Analysis | History and statistics, for reports. |

---

## The one rule

**Nobody appears on a graduation list until a person approves them.**

The system finds candidates, checks them and sorts them, but it never adds anyone by
itself. A name reaches the list only when a member of staff opens that student, reads
the checks and presses **Clear**. That is the whole design, and everything below follows
from it.

So an empty list at the start of a cycle is correct, not broken. It fills as you work
through the queue.

**There is one exception, and you should know about it.** Printing an academic document
adds that student to the list for their current registration year if they are not on it
already. This is not the Graduation Centre doing it. It is the older transcript printing,
which finds students by reading this same table, and which predates the approval rule. It
means a student can appear on the list without anyone approving them, including a student
who is not in their final year. Until that is separated, check the list before you publish
it, and remove anything you did not put there. A row added this way has no entry against
it on the Held tab and no decision recorded, which is how you can tell.

## What you can see

The module shows you your own area and no more.

* **Registry and system administrators** see the whole university.
* **A dean** sees their faculty.
* **A head of department** sees their department.

This applies everywhere: the queue, the list, the counts on the Centre tab, the search
box, and every export. Two people running the same export on the same day will get
different files, and both are right.

---

## Tab 1: Graduation Centre

**Open this first.** It answers "where are we?" before you start deciding anything.

Pick the graduation year at the top. You then see, for that year:

* **Ready**, candidates with nothing outstanding
* **Blocked**, candidates stopped by at least one check
* **Held**, candidates a reviewer has parked
* **On the list**, candidates already approved

Below that, **Where the blockers are** shows which check is stopping the most people, and
**Progress by programme** shows every programme with its candidates, how many are on the
list, held, blocked, and how many are still to be looked at.

Clicking a programme takes you into the Candidates tab already filtered to it.

There are no buttons on this tab that change anything. It is safe to leave open.

---

## Tab 2: Candidates

This is where the work happens.

### Choosing who to look at

The filter bar across the top narrows the queue:

* **Graduation year**, the cycle you are clearing for
* **Who to show**, either *This cycle, finishing now* or *Everyone not yet graduated*
* **Faculty**, **Department**, **Programme**, which cascade: choosing a faculty limits
  the departments, and choosing a department limits the programmes
* **Intake**, the year the student joined
* **Graduation list**, either *Not yet on a list*, *Already on the list*, or *Both*
* **Readiness**, either *Ready*, *Needs a look*, or *Blocked*
* **Order** and **Search**

The default is the pending queue for the current cycle, which is what you want on most days.

### The eight checks

Open any student and you see eight checks. Each one passes, warns, or blocks.

| | Check | What it means |
|---|---|---|
| C1 | Already on a graduation list | Blocks if the student has graduated before. |
| C2 | Outstanding papers | Blocks on a mark between 1 and 49. Warns on a mark of zero, which is usually a paper never marked rather than a paper failed. |
| C3 | Credits earned | Compares credits earned against the programme. Blocks only where a real curriculum is on file and the student is below 90% of it. Otherwise it warns. |
| C4 | Programme coverage | Courses in the curriculum with no result. **Never blocks.** A gap often means an equivalent course was taken under a different code. |
| C5 | Marks not yet published | Warns while marks are still with a lecturer, a head of department, or approved but not yet published. Their position may still change. |
| C6 | Programme duration | Blocks if the student has not reached the final year of their programme. |
| C7 | Class of award | Blocks if the CGPA is below 2.0, the floor below which no class is awarded at any level. |
| C8 | Held by a reviewer | Blocks while a hold is open, and shows who held them, when, and why. |

The student's overall state is the worst of the eight:

* **Ready**, every check passed
* **Needs a look**, at least one warning, nothing blocking
* **Blocked**, at least one check blocking

A warning is not a refusal. It is a request that someone look before approving.

### The four decisions

**Clear** puts the student on the graduation list for the selected year. If they are
blocked, the system stops and tells you which checks are blocking. You may still go
ahead, but you must confirm the override and type a justification of at least ten
characters. That justification is kept.

**Hold** stops a candidate until something is investigated. The reason is required and
must be at least ten characters. The reason box is not a blank field: it suggests the
reasons drawn from this student's own failing checks first, then the reasons your
colleagues have used most often, then the standing ones such as financial clearance or
unverified documents. Most holds are two clicks. A held candidate stays visible, with the
reason attached, rather than quietly disappearing.

**Release** lifts a hold and returns the student to the queue.

**Remove** takes a student off the graduation list. It needs a reason of at least ten
characters.

### Working through the queue

After a decision the system hands you the next candidate **that matches the filter you
set**, so you can work through a programme without going back to the table. It follows
your filter across page boundaries. The table behind the modal updates when you close it,
not between decisions, so the row numbering does not shift under you mid-run.

You can also select several rows and clear or hold them together. Any student the batch
could not act on is listed back to you with the reason, rather than failing silently.

### Adding someone the system missed

**Add a student** searches the whole of your area by name or student number, not just the
current queue. Each result says whether that person is already on a list, already a
candidate, or neither, so you can see before you click. Picking one opens them with the
full checks, and you decide from there as normal.

Use this when a student should be considered but the candidacy rule did not pick them up.

---

## Tab 3: Graduation List

Everyone who has been approved for the selected year.

You can filter and search it the same way as the queue, open any student to see their
record, and remove someone with a reason if a mistake was made.

This tab is also what students see in the portal, so treat it as published.

---

## Tab 4: Held Candidates

Everyone currently stopped, with the reason, who set it and when.

Work this tab down to empty before a cycle closes. Every row is either something to fix
or something to release. A hold nobody revisits is a student who quietly never graduates,
which is exactly what this tab exists to prevent.

From here you can release a student, change the reason, or clear them once the problem is
resolved.

---

## Tab 5: Graduation Analysis

History and statistics, for reports and for Senate papers. It opens on the current
academic year.

There are two ways of counting, and they answer different questions:

* **By academic year**, the year the students completed
* **By graduation ceremony**, the ceremony they were presented at

These do not match, and the difference is real rather than an error. One ceremony
presents students who completed across many different academic years, because a student
who finishes late is presented at the next ceremony after they complete. When you count
by ceremony, the tab shows you which years those graduands came from.

Narrow with the same faculty, department, programme and award level filters. You then get
the headline counts, awards and classes, a breakdown by faculty and by programme, a trend
across years, and the state of printed documents.

**Generate summary** writes the figures out as prose you can paste into a report, and
every number in it comes from the same query that drew the table above it.

Both the tables and the summary can be exported.

---

## Exporting

Every tab exports through the same dialog, and it always starts matching what is on your
screen.

**Choose the format:**

* **PDF**, the formal document. University crest, correct colours, a certification block,
  broken into sections by programme. This is the default, and it is produced by the
  server, not the browser, so it looks the same for everyone.
* **Excel workbook**, with a cover sheet recording the filters used, frozen headings, and
  optional extra summary sheets.
* **CSV**, one flat sheet for loading into something else.

**What is included** lets you change the year, faculty, department and programme from
inside the dialog, so you do not have to close it and change the page. These cascade the
same way the page filters do. Anything else narrowing the file, such as a search term, is
shown to you but not editable there, so the row count is never a mystery. The dialog
tells you how many rows you are about to export before you press the button.

**Columns** and **Advanced** are folded away, because most exports do not need them.
Advanced holds the row order and how the document is broken into sections. By default it
is grouped by programme and sorted by name; you can sort by performance instead.

Exports are never silently cut short. If a file is truncated it says so, on the cover.

---

## What students see

Students do not see any of the tabs above. In the student portal they see three things.

**A congratulation notice** appears on their home page when they are approved, and stays
for two months. It tells them the academic year they were approved for, and links to the
detail page.

**My Graduation** under the home page action links shows one student their own standing:
whether they are on a list, the year, their award, and what the Registry has done with
their documents. It answers the question whether the answer is yes or no, so a student who
is not graduating gets a real answer rather than a blank page.

**Graduation List** shows the published list for a year, 100 names to a page, searchable.
It shows the name, the award and the student number, and nothing else. Marks, grade point
averages and classes of award are personal and are not published there.

Under the count, the list says when it was last updated and that names appear as the
Registry approves them, so a student who does not see their name yet understands the list
is still being worked through rather than finished.

**This means clearing a student is visible to them almost at once.** Clear when you are
sure.

---

## What gets recorded

Every decision is written down. Nothing in this module is anonymous.

* `acad_graduands` is the graduation list itself, the official record of who graduated.
* `acad_grad_review` holds every decision ever made, cleared, held or released, with who
  made it, when, in what role, and the reason or justification they typed. Decisions are
  kept even after a student is removed from the list.
* `acad_activity_log` carries the same events into the system-wide audit trail.

A clearance made over a block is recorded as such, with the justification, and is
distinguishable from an ordinary clearance for ever.

---

## Running a cycle

1. **Graduation Centre.** Choose the year. See where you stand.
2. **Candidates**, filtered to *Ready*. Work through the queue, clearing as you go.
   These are the straightforward ones.
3. **Candidates**, filtered to *Needs a look*. Read the warnings and decide each one.
   Hold anything that needs someone else to act.
4. **Candidates**, filtered to *Blocked*. Most of these are genuine. Fix what can be
   fixed, hold the rest with a reason.
5. **Held Candidates.** Work it down to empty.
6. **Graduation List.** Check it, then export the PDF for the Registrar.
7. **Graduation Analysis.** Generate the summary for the Senate paper.

---

## Common questions

**The list is empty. Is it broken?**
No. A list starts empty and fills as people are approved. Nothing is added automatically.

**Why can I not see a student I know about?**
You see your own faculty or department only. If the student is outside it, ask the
Registry or the relevant dean.

**A student is blocked but should graduate.**
Fix the underlying record if you can, because that is better for everyone. If it cannot
be fixed in time, clear over the block and write the justification. It is kept, and it is
visible.

**Someone is on the list who should not be.**
Open them on the Graduation List tab and remove them with a reason. Do this before the
list is published, because students can see the list.

**A student asks why they are not on the list.**
Search for them on the Candidates tab. Their checks say exactly what is outstanding. If
they are held, the Held tab gives the reason and who set it.

**Will clearing someone change their marks or their CGPA?**
No. This module reads results and never writes them. Marks are corrected in the marks
screens, and the checks pick up the correction next time you open the student.
