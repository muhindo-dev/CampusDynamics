<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationCandidates.aspx.cs" Inherits="COOPERP_NewScreens_GraduationCandidates" Title="Graduation Candidates - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Candidates</b><span id="gScope">&nbsp;</span>
      <span class="g-age" id="gAge">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 118px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:0 0 184px;"><label for="fFocus">Who to show</label>
      <select id="fFocus"><option value="cycle">This cycle &mdash; finishing now</option><option value="all">Everyone not yet graduated</option></select></div>
    <div class="g-f" style="flex:1 1 130px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 130px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:0 0 88px;"><label for="fIntake">Intake</label><select id="fIntake"></select></div>
    <div class="g-f" style="flex:0 0 116px;"><label for="fReady">Readiness</label>
      <select id="fReady"><option value="">Any</option><option value="ready">Ready</option><option value="warn">Needs a look</option><option value="blocked">Blocked</option></select></div>
    <div class="g-f" style="flex:0 0 108px;"><label for="fSort">Order</label>
      <select id="fSort"><option value="regno">Student number</option><option value="name">Name</option>
        <option value="prog">Programme</option><option value="entry">Newest intake</option></select></div>
    <div class="g-f" style="flex:1 1 130px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number or name&hellip;" autocomplete="off" /></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnAdd" title="Bring a student into the queue who the engine did not">Add a student&hellip;</button>
      <button type="button" class="g-btn" id="btnXls">Export&hellip;</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

  <%-- What is narrowing the view, each chip removable on its own. Without this, a filter left
       set three controls away makes "no candidates match" read like a fault in the data. --%>
  <div class="g-chips" id="gChips"></div>
  <div class="g-batch" id="gBatch" style="display:none;"></div>

  <div class="g-card">
    <div class="g-card__h">
      <span id="gTitle">Candidates</span><small id="gMeta">&nbsp;</small>
      <small style="margin-left:auto;">Only candidates with nothing outstanding can be selected for bulk clearing.</small>
    </div>
    <div class="g-wrap">
      <table class="g-tbl"><thead><tr>
        <th style="width:24px;"><input type="checkbox" id="ckAll" title="Select every ready candidate on this page" /></th>
        <th>Student</th><th>Programme</th><th class="g-num">Intake</th>
        <th style="min-width:118px;">Credits</th><th class="g-num">CGPA</th><th>Class</th><th>Readiness</th><th></th>
      </tr></thead><tbody id="gBody"></tbody></table>
    </div>
    <div class="g-pager" id="gPager"></div>
  </div>

  <div id="gNoAccess" class="g-note g-note--warn" style="display:none;"></div>
</div>

<script type="text/javascript">
// Rendered into the page by Page_Load, so the controls are populated without a round trip.
window.G_BOOT = <%= BootJson %>;
window.G_COLS = <%= ColsJson %>;
window.G_AGE  = '<%= StatsAge %>';
</script>
<script src="js/graduation.js"></script>
<script type="text/javascript">
(function () {
'use strict';
/* BOOT, not boot. Each page declared "var boot = null" here and then "function boot(o)"
   inside DOMContentLoaded; the inner declaration shadowed the outer variable, so "boot = o"
   only ever assigned to the local function binding. Reset then read boot.currentYear off a
   FUNCTION - undefined - and blanked the graduation year, silently widening every view to all
   years. Renaming the store leaves nothing for the callback to shadow. */
var PAGE = 'GraduationCandidates.aspx', BOOT = null, page = 1, rows = [];

function state() {
    return { year: G.qs('fYear').value, focus: G.qs('fFocus').value, faculty: G.qs('fFac').value,
             dept: G.qs('fDep').value, prog: G.qs('fProg').value, intake: G.qs('fIntake').value,
             ready: G.qs('fReady').value, sort: G.qs('fSort').value === 'regno' ? '' : G.qs('fSort').value,
             q: G.qs('fQ').value.trim(), page: page > 1 ? page : '' };
}
function cfg(extra) {
    var s = state();
    var c = { acadYear: s.year, focus: s.focus, faculty: s.faculty, department: s.dept,
              programme: s.prog, entryYear: s.intake, readiness: s.ready, search: s.q,
              sort: G.qs('fSort').value, state: 'pending', page: String(page), size: '50' };
    if (extra) for (var k in extra) if (extra.hasOwnProperty(k)) c[k] = extra[k];
    return JSON.stringify(c);
}
function sync() { G.writeUrl(state()); chips(); load(); }

/* The filter strip. Every chip names one filter and removes exactly that one. */
function chips() {
    function txt(id) {
        var el = G.qs(id);
        return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.replace(/\s*\(\d+\)\s*$/, '')
                                              .replace(/\s*\(none\)\s*$/, '').trim() : '';
    }
    G.chips('gChips', [
        { k: 'fFac',    label: 'Faculty',    value: G.qs('fFac').value ? txt('fFac') : '' },
        { k: 'fDep',    label: 'Department', value: G.qs('fDep').value ? txt('fDep') : '' },
        { k: 'fProg',   label: 'Programme',  value: G.qs('fProg').value ? txt('fProg') : '' },
        { k: 'fIntake', label: 'Intake',     value: G.qs('fIntake').value },
        { k: 'fReady',  label: 'Readiness',  value: G.qs('fReady').value ? txt('fReady') : '' },
        { k: 'fQ',      label: 'Search',     value: G.qs('fQ').value.trim() }
    ], function (k) {
        if (k === '*') { resetFilters(); } else { G.qs(k).value = ''; }
        G.cascade('fFac', 'fDep', 'fProg');
        page = 1; sync();
    });
}

function resetFilters() {
    ['fFac', 'fDep', 'fProg', 'fIntake', 'fReady'].forEach(function (id) { G.qs(id).value = ''; });
    G.qs('fQ').value = '';
}

function load() {
    G.qs('gBody').innerHTML = '<tr><td colspan="9" class="g-load">Loading&hellip;</td></tr>';
    G.qs('gPager').innerHTML = '';
    G.ajax(PAGE, 'GetCandidates', { configJson: cfg() }, function (d) {
        if (!d || !d.success) {
            G.toast((d && d.message) || 'Could not load candidates.', false);
            G.qs('gBody').innerHTML = '<tr><td colspan="9" class="g-empty">Nothing to show.</td></tr>';
            return;
        }
        rows = d.rows || [];
        render(d);
    });
}

function render(d) {
    var h = '', i;
    for (i = 0; i < rows.length; i++) {
        var g = rows[i];
        var pick = (g.readiness === 'READY' && !g.graduatedYear && !g.holdReason);
        h += '<tr class="is-click" data-reg="' + G.esc(g.regno) + '">' +
            '<td>' + (pick ? '<input type="checkbox" class="ck" data-reg="' + G.esc(g.regno) + '" />'
                           : '<span class="g-sub" title="Only candidates with nothing outstanding can be bulk-cleared">&ndash;</span>') + '</td>' +
            '<td>' + G.photoCell(g.regno, g.name) + '</td>' +
            '<td>' + G.esc(g.progname || g.progcode) + '<div class="g-sub">' + G.esc(g.progcode) + '</div></td>' +
            '<td class="g-num g-sub">' + G.esc(g.entryyear) + '</td>' +
            '<td>' + G.credits(g) + '</td>' +
            '<td class="g-num">' + (g.cgpa ? G.n2(g.cgpa) : '&ndash;') + '</td>' +
            '<td class="g-sub">' + G.esc(g.degClass || '&ndash;') + '</td>' +
            '<td>' + G.chip(g) + '</td>' +
            '<td><button type="button" class="g-btn g-btn--sm" data-open="' + G.esc(g.regno) + '">Review</button></td></tr>';
    }
    G.qs('gBody').innerHTML = h || '<tr><td colspan="9" class="g-empty">No candidates match these filters.</td></tr>';

    var scope = G.qs('fFocus').value === 'cycle'
        ? (' · finishing ' + (BOOT && BOOT.previousYear ? BOOT.previousYear + ' or ' : '') + G.qs('fYear').value)
        : ' · everyone not yet graduated';
    G.qs('gMeta').textContent = scope.replace(/^ · /, '');

    // The pager carries the numbers now — which records these are, and how many there are
    // altogether — so the card heading no longer repeats them.
    G.pager('gPager', { page: d.page, size: 50, total: d.total, shown: rows.length },
            function (n) { page = n; sync(); });

    // The queue the modal walks: this page's rows, in the order the filter produced them.
    var ids = [];
    for (i = 0; i < rows.length; i++) ids.push(rows[i].regno);
    G.queue({ ids: ids, total: d.total, page: d.page, pages: d.pages, size: 50,
              nextPage: function (n) { page = n; sync(); } });
    G.onWalk(open);

    G.qs('ckAll').checked = false;
    syncBulk();
}

/* ── bulk ── */
function selected() {
    var out = [], b = document.querySelectorAll('#gBody .ck:checked');
    for (var i = 0; i < b.length; i++) out.push(b[i].getAttribute('data-reg'));
    return out;
}
/* The batch bar appears only when something is selected, and says exactly what it will do to
   how many. A bar that is always there, mostly disabled, is furniture. */
function syncBulk() {
    var n = selected().length, bar = G.qs('gBatch');
    if (!n) { bar.style.display = 'none'; bar.innerHTML = ''; return; }
    bar.style.display = 'flex';
    bar.innerHTML =
        '<span class="g-batch__n">' + n + ' selected</span>' +
        '<button type="button" class="g-btn g-btn--p g-btn--sm" id="bClear">Clear ' + n + ' for graduation</button>' +
        '<button type="button" class="g-btn g-btn--d g-btn--sm" id="bHold">Hold ' + n + '&hellip;</button>' +
        '<button type="button" class="g-btn g-btn--sm g-batch__x" id="bNone">Deselect</button>';
    G.qs('bClear').addEventListener('click', doBulkClear);
    G.qs('bHold').addEventListener('click', doBulkHold);
    G.qs('bNone').addEventListener('click', function () {
        var b = document.querySelectorAll('#gBody .ck');
        for (var i = 0; i < b.length; i++) b[i].checked = false;
        G.qs('ckAll').checked = false;
        syncBulk();
    });
}
function doBulkClear() {
    var regs = selected();
    if (!regs.length) return;
    if (!G.qs('fYear').value) { G.toast('Choose the graduation year first.', false); return; }
    if (!confirm('Put ' + regs.length + ' candidate' + (regs.length === 1 ? '' : 's') + ' on the ' +
                 G.qs('fYear').value + ' graduation list?\n\nEach one is re-checked on the server before it is added.')) return;
    G.qs('bClear').disabled = true;
    G.ajax(PAGE, 'ClearMany', { regnos: regs.join(','), acadYear: G.qs('fYear').value }, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'The bulk clear did not run.', false); syncBulk(); return; }
        G.toast(d.message, d.cleared > 0);
        report(d.skipped, 'cleared');
        load();
    });
}

/* A batch hold goes through the same reason dialog as a single one. Holding forty students
   under a reason nobody had to write down would be the one place this module stopped being
   accountable. */
function doBulkHold() {
    var regs = selected();
    if (!regs.length) return;
    if (!G.qs('fYear').value) { G.toast('Choose the graduation year first.', false); return; }
    G.reasonDialog({
        page: PAGE,
        regno: '',
        count: regs.length,
        title: 'Hold ' + regs.length + ' candidate' + (regs.length === 1 ? '' : 's'),
        subtitle: 'One reason, recorded separately against each of them.',
        verb: 'Hold ' + regs.length,
        onSubmit: function (reason) {
            G.ajax(PAGE, 'HoldMany',
                { regnos: regs.join(','), acadYear: G.qs('fYear').value, reason: reason },
                function (d) {
                    if (!d || !d.success) { G.toast((d && d.message) || 'The bulk hold did not run.', false); return; }
                    G.toast(d.message, d.held > 0);
                    report(d.skipped, 'held');
                    load();
                });
        }
    });
}

/* What a batch did NOT do matters as much as what it did. */
function report(skipped, verb) {
    if (!skipped || !skipped.length) return;
    alert(skipped.length + ' of the selection could not be ' + verb + ':\n\n' + skipped.join('\n'));
}

/* ── bringing somebody in by hand ─────────────────────────────
   The chosen student goes to the SAME review panel as everyone else: re-assessed from live
   marks, and a written justification demanded before anyone blocked can be cleared. The queue
   is not rewritten - they are appended to it, so the walker still works and the reviewer can
   carry on where they were afterwards. */
function addStudent() {
    G.findStudent({
        page: PAGE,
        acadYear: G.qs('fYear').value,
        onPick: function (reg) {
            var ids = G.queueIds();
            if (ids.indexOf(reg) < 0) ids.push(reg);
            open(reg);
        }
    });
}

/* ── export ──────────────────────────────────────────────────────────
   The button no longer guesses. It opens a dialog that states what is included, counts the
   rows on the server before anything is committed to, and lets the columns and extra sheets be
   chosen. The count matters: the previous build asked for the whole queue and silently produced
   fifty rows of it. */
function openExport() {
    function txt(id) {
        var el = G.qs(id);
        return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.replace(/\s*\(\d+\)\s*$/, '')
                                              .replace(/\s*\(none\)\s*$/, '').trim() : '';
    }
    // Only what the dialog does NOT control: a search term or a readiness filter can be doing
    // most of the narrowing, and a row count with no visible cause is a mystery.
    var sum = [];
    if (G.qs('fIntake').value) sum.push({ label: 'Intake', value: G.qs('fIntake').value });
    if (G.qs('fReady').value) sum.push({ label: 'Readiness', value: txt('fReady') });
    if (G.qs('fQ').value.trim()) sum.push({ label: 'Search', value: G.qs('fQ').value.trim() });

    G.exportDialog({
        page: PAGE,
        title: 'Export candidates',
        subtitle: 'Everything here is branded and carries a cover sheet stating this exact scope.',
        cfg: cfg(),
        columns: window.G_COLS || [],
        pageRows: rows.length,
        countMethod: 'CountExport',
        filterSummary: sum,
        filters: {
            focus: true,
            years: (BOOT && BOOT.years) || [],
            faculties: (BOOT && BOOT.faculties) || [],
            departments: (BOOT && BOOT.departments) || [],
            programmes: (BOOT && BOOT.programmes) || [],
            current: {
                acadYear: G.qs('fYear').value, focus: G.qs('fFocus').value,
                faculty: G.qs('fFac').value, department: G.qs('fDep').value,
                programme: G.qs('fProg').value
            }
        },
        sorts: [
            { k: 'name', t: 'Name' },
            { k: 'regno', t: 'Student number' },
            { k: 'cgpa', t: 'Performance — highest CGPA first' },
            { k: 'class', t: 'Class of award' }
        ],
        sortDefault: 'name',
        groups: [
            { k: 'prog', t: 'Programme' },
            { k: 'fac', t: 'Faculty' },
            { k: '', t: 'One continuous list' }
        ],
        groupDefault: 'prog',
        sheets: [
            { k: 'byprog', t: 'Summary by programme', d: 'Candidates, ready, needs a look, blocked', on: true },
            { k: 'byready', t: 'Summary by readiness', d: 'The queue in five figures', on: false },
            { k: 'blockers', t: 'Why they are blocked', d: 'Each check, and how many it stops', on: true }
        ]
    });
}

/* ── the evidence modal's actions ── */
function footer(g) {
    if (g.graduatedYear) return '<span class="g-sub">Already on the ' + G.esc(g.graduatedYear) + ' list.</span>';
    if (g.holdReason)
        return '<button type="button" class="g-btn g-btn--p" id="mRelease">Lift the hold</button>' +
               '<button type="button" class="g-btn" id="mClear">Clear for graduation</button>';
    return '<button type="button" class="g-btn g-btn--p" id="mClear">Clear for graduation</button>' +
           '<button type="button" class="g-btn g-btn--d" id="mHold">Hold&hellip;</button>' +
           // The switch belongs beside the decision it changes, not in a settings menu.
           '<label class="g-auto" title="After a decision, open the next candidate in this filter">' +
             '<input type="checkbox" id="mAuto"' + (G.autoAdvance() ? ' checked' : '') + ' />' +
             'Move to the next candidate' +
           '</label>' +
           '<span class="g-hint">' +
           (g.readiness === 'BLOCKED' ? 'Blocked — clearing will ask you to justify it in writing.'
            : g.readiness === 'WARN' ? 'Read the points above before clearing.'
            : 'Nothing outstanding — safe to clear.') + '</span>';
}

// The footer only exists once the record has come back, so it is wired from the callback
// that writes it. The old setTimeout(fn, 0) fired long before the response landed, which is
// why the decision buttons did nothing even when they were on screen.
function open(reg) {
    G.openStudent(PAGE, reg, footer, wireFooter);
}
function wireFooter() {
    var c = G.qs('mClear'), h = G.qs('mHold'), r = G.qs('mRelease'), a = G.qs('mAuto');
    if (c) c.addEventListener('click', doClear);
    if (h) h.addEventListener('click', doHold);
    if (r) r.addEventListener('click', doRelease);
    if (a) a.addEventListener('change', function () { G.setAutoAdvance(this.checked); });
}

/* ── after a decision ─────────────────────────────────────
   The candidate is marked decided IN PLACE and the next one opens. The list is not reloaded
   until the modal closes: a cleared candidate leaves the pending queue, so reloading now would
   renumber the rows underneath and make "next" mean something different every time. */
var dirty = false;

function afterDecision(regno, label) {
    dirty = true;
    G.markDecided(regno, label);
    if (!G.autoAdvance()) { G.closeModal(); return; }

    var i = G.queueNext();
    if (i >= 0) { open(G.queueAt(i)); return; }
    if (G.queueHasMorePages()) {
        G.toast('End of this page \\u2014 fetching the next.', true);
        G.closeModal();
        page++; sync();
        return;
    }
    G.closeModal();
    G.toast('That was the last candidate matching these filters.', true);
}

function doClear() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student, year = G.qs('fYear').value;
    if (!year) { G.toast('Choose the graduation year first.', false); return; }

    function send(note) {
        G.ajax(PAGE, 'ClearStudent',
            { regno: g.regno, acadYear: year, note: note || '', overrideBlock: g.readiness === 'BLOCKED' },
            function (d) {
                if (d && d.success) { G.toast(d.message, true); afterDecision(g.regno, 'On ' + year); }
                else G.toast((d && d.message) || 'Could not clear that candidate.', false);
            });
    }

    if (g.readiness === 'BLOCKED') {
        var b = [];
        for (var i = 0; i < g.findings.length; i++)
            if (g.findings[i].level === 'BLOCK') b.push(g.findings[i].detail);
        G.reasonDialog({
            page: PAGE, regno: g.regno,
            title: 'Clear ' + g.name + ' anyway',
            subtitle: g.regno + '  \\u00b7  ' + (g.progname || g.progcode),
            warn: 'This candidate is blocked: ' + b.join('; ') +
                  ' Clearing them is recorded against your name and shown on the graduation list.',
            verb: 'Clear anyway',
            onSubmit: send
        });
    } else if (confirm('Put ' + g.name + ' on the ' + year + ' graduation list?')) send('');
}

function doHold() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student, year = G.qs('fYear').value;
    if (!year) { G.toast('Choose the graduation year first.', false); return; }
    G.reasonDialog({
        page: PAGE, regno: g.regno,
        title: 'Hold ' + g.name,
        subtitle: g.regno + '  \\u00b7  ' + (g.progname || g.progcode),
        verb: 'Hold',
        onSubmit: function (reason) {
            G.ajax(PAGE, 'HoldStudent', { regno: g.regno, acadYear: year, reason: reason }, function (d) {
                if (d && d.success) { G.toast(d.message, true); afterDecision(g.regno, 'Held'); }
                else G.toast((d && d.message) || 'Could not hold that candidate.', false);
            });
        }
    });
}

function doRelease() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    G.ajax(PAGE, 'ReleaseStudent', { regno: g.regno, acadYear: G.qs('fYear').value, note: '' }, function (d) {
        if (d && d.success) { G.toast(d.message, true); afterDecision(g.regno, 'Released'); }
        else G.toast((d && d.message) || 'Could not lift that hold.', false);
    });
}



document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    // Refreshing on close rather than after each decision is what keeps the queue stable while
    // it is being worked.
    G.wireModal(function () { if (dirty) { dirty = false; load(); } });
    var pre = G.readUrl();

    document.addEventListener('click', function (e) {
        if (e.target && e.target.className === 'ck') { e.stopPropagation(); syncBulk(); return; }
        var b = e.target.closest ? e.target.closest('[data-open]') : null;
        if (b) { e.stopPropagation(); open(b.getAttribute('data-open')); return; }
        var tr = e.target.closest ? e.target.closest('#gBody tr[data-reg]') : null;
        if (tr) open(tr.getAttribute('data-reg'));
    });
    G.qs('gBody').addEventListener('change', function (e) {
        if (e.target && e.target.className === 'ck') syncBulk();
    });
    G.qs('ckAll').addEventListener('change', function () {
        var b = document.querySelectorAll('#gBody .ck');
        for (var i = 0; i < b.length; i++) b[i].checked = G.qs('ckAll').checked;
        syncBulk();
    });

    ['fYear', 'fFocus', 'fProg', 'fIntake', 'fReady'].forEach(function (id) {
        G.qs(id).addEventListener('change', function () { page = 1; sync(); });
    });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync(); });
    G.qs('fSort').addEventListener('change', function () { page = 1; sync(); });
    // Typing is enough. Requiring Enter meant a filter that looked applied and was not.
    var typed = G.debounce(function () { page = 1; sync(); }, 350);
    G.qs('fQ').addEventListener('input', typed);
    G.qs('fQ').addEventListener('keydown', function (e) { if (e.key === 'Enter') { page = 1; sync(); } });
    G.qs('btnXls').addEventListener('click', openExport);
    G.qs('btnAdd').addEventListener('click', addStudent);
    G.qs('btnReset').addEventListener('click', function () {
        resetFilters();
        G.qs('fFocus').value = 'cycle';
        G.qs('fSort').value = 'regno';
        if (BOOT && BOOT.currentYear) G.qs('fYear').value = BOOT.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync();
    });

    function applyBoot(o) {
        if (!o || !o.success || !o.hasAccess) {
            G.qs('gToolbar').style.display = 'none';
            var na = G.qs('gNoAccess'); na.style.display = 'block';
            na.textContent = (o && o.message) || 'You do not have access to graduation data.';
            return;
        }
        BOOT = o;
        G.qs('gScope').textContent = (o.roleNote || '') + (o.scopeLabel ? ' · ' + o.scopeLabel : '');
        G.freshness('gAge');
        G.fill('fYear', o.years.map(function (y) { return { v: y, t: y }; }), null);
        G.fill('fFac', o.faculties, 'All faculties');
        G.fill('fDep', o.departments, 'All departments');
        G.fill('fProg', o.programmes, 'All programmes');
        G.fill('fIntake', (o.intakes || []).map(function (y) { return { v: y, t: y }; }), 'All');
        G.qs('fYear').value = pre.year || o.currentYear || '';
        G.qs('fFocus').value = pre.focus || 'cycle';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        if (pre.intake) G.qs('fIntake').value = pre.intake;
        if (pre.ready) G.qs('fReady').value = pre.ready;
        if (pre.sort) G.qs('fSort').value = pre.sort;
        if (pre.q) G.qs('fQ').value = pre.q;
        page = parseInt(pre.page || '1', 10) || 1;
        G.cascade('fFac', 'fDep', 'fProg');
        // 130 programmes is not a list anyone should scroll through.
        G.combo('fProg', 'Type a code or part of the name\u2026');
        chips();
        load();
    }

    if (window.G_BOOT) applyBoot(window.G_BOOT);
    else G.ajax(PAGE, 'GetBootstrap', {}, applyBoot);
});
})();
</script>
</asp:Content>
