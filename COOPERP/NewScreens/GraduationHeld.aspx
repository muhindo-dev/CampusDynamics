<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationHeld.aspx.cs" Inherits="COOPERP_NewScreens_GraduationHeld" Title="Held Candidates - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Held candidates</b><span id="gScope">&nbsp;</span>
      <span class="g-age" id="gAge">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 122px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 170px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:1 1 140px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number or name&hellip;" autocomplete="off" /></div>
    <div class="g-f" style="flex:0 0 116px;"><label for="fStale">Show</label>
      <select id="fStale"><option value="">Every hold</option>
        <option value="stale">Only those nothing is blocking any more</option></select></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnXls">Export&hellip;</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

  <div class="g-chips" id="gChips"></div>

  <div class="g-note g-note--info">
    Oldest hold first. A hold nobody revisits is a student who quietly never graduates.
  </div>

  <div class="g-card">
    <div class="g-card__h"><span>Held</span><small id="gMeta">&nbsp;</small></div>
    <div class="g-wrap">
      <table class="g-tbl"><thead><tr>
        <th>Student</th><th>Programme</th><th>Why</th><th>Held by</th><th class="g-num">When</th><th></th>
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
/* BOOT, not boot: the old name was shadowed by "function boot(o)" inside DOMContentLoaded, so
   the store stayed null and Reset blanked the graduation year instead of restoring it. */
var PAGE = 'GraduationHeld.aspx', BOOT = null, rows = [], page = 1;

function state() {
    return { year: G.qs('fYear').value, faculty: G.qs('fFac').value, dept: G.qs('fDep').value,
             prog: G.qs('fProg').value, stale: G.qs('fStale').value,
             q: G.qs('fQ').value.trim(), page: page > 1 ? page : '' };
}
function cfg() {
    var s = state();
    // 200 is the ceiling Page() honours; asking for 300 silently got 50 and the screen had no
    // pager to reach the rest.
    return JSON.stringify({ acadYear: s.year, faculty: s.faculty, department: s.dept,
                            programme: s.prog, search: s.q, state: 'held',
                            page: String(page), size: '100' });
}
function sync() { G.writeUrl(state()); chips(); load(); }

function chips() {
    function txt(id) {
        var el = G.qs(id);
        return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.replace(/\s*\(\d+\)\s*$/, '')
                                              .replace(/\s*\(none\)\s*$/, '').trim() : '';
    }
    G.chips('gChips', [
        { k: 'fFac',   label: 'Faculty',    value: G.qs('fFac').value ? txt('fFac') : '' },
        { k: 'fDep',   label: 'Department', value: G.qs('fDep').value ? txt('fDep') : '' },
        { k: 'fProg',  label: 'Programme',  value: G.qs('fProg').value ? txt('fProg') : '' },
        { k: 'fStale', label: 'Show',       value: G.qs('fStale').value ? 'Liftable only' : '' },
        { k: 'fQ',     label: 'Search',     value: G.qs('fQ').value.trim() }
    ], function (k) {
        if (k === '*') resetFilters(); else G.qs(k).value = '';
        G.cascade('fFac', 'fDep', 'fProg');
        page = 1; sync();
    });
}

function resetFilters() {
    ['fFac', 'fDep', 'fProg', 'fStale'].forEach(function (id) { G.qs(id).value = ''; });
    G.qs('fQ').value = '';
}

function load() {
    G.qs('gBody').innerHTML = '<tr><td colspan="6" class="g-load">Loading&hellip;</td></tr>';
    G.qs('gPager').innerHTML = '';
    G.ajax(PAGE, 'GetHeld', { configJson: cfg() }, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'Could not load held candidates.', false); return; }
        rows = d.rows || [];
        // "Nothing is blocking them any more" is decided per student in C#, not in SQL, so this
        // one narrowing has to happen here.
        if (G.qs('fStale').value === 'stale')
            rows = rows.filter(function (g) { return g.readiness !== 'BLOCKED'; });
        var h = '';
        for (var i = 0; i < rows.length; i++) {
            var g = rows[i];
            // A hold whose blocking finding has since cleared is the one worth acting on.
            var stale = (g.readiness !== 'BLOCKED');
            h += '<tr class="is-click" data-reg="' + G.esc(g.regno) + '">' +
                '<td>' + G.photoCell(g.regno, g.name) + '</td>' +
                '<td>' + G.esc(g.progname || g.progcode) + '<div class="g-sub">' + G.esc(g.progcode) + '</div></td>' +
                '<td style="max-width:320px;">' + G.esc(g.holdReason) +
                  (stale ? '<div class="g-sub" style="color:#0b5c3a;font-weight:700;">Nothing is blocking this candidate any more</div>' : '') + '</td>' +
                '<td class="g-sub">' + G.esc(g.holdActor) + '</td>' +
                '<td class="g-num g-sub">' + G.esc(g.holdAt) + '</td>' +
                '<td><button type="button" class="g-btn g-btn--sm" data-open="' + G.esc(g.regno) + '">Review</button></td></tr>';
        }
        G.qs('gBody').innerHTML = h || '<tr><td colspan="6" class="g-empty">Nobody is held in this scope.</td></tr>';

        G.qs('gMeta').textContent = G.qs('fStale').value === 'stale'
            ? 'holds nothing is blocking any more' : 'oldest first';
        var pages = Math.max(1, Math.ceil((d.total || 0) / 100));
        G.pager('gPager', { page: page, size: 100, total: d.total || 0, shown: rows.length },
                function (n) { page = n; sync(); });

        var ids = [];
        for (var q = 0; q < rows.length; q++) ids.push(rows[q].regno);
        G.queue({ ids: ids, total: d.total || 0, page: page, pages: pages, size: 100,
                  nextPage: function (n) { page = n; sync(); } });
        G.onWalk(open);
    });
}

/* ── export ── */
function openExport() {
    function txt(id) {
        var el = G.qs(id);
        return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.replace(/\s*\(\d+\)\s*$/, '')
                                              .replace(/\s*\(none\)\s*$/, '').trim() : '';
    }
    G.exportDialog({
        page: PAGE,
        title: 'Export held candidates',
        subtitle: 'Oldest hold first, with the reason and who wrote it.',
        cfg: cfg(),
        columns: window.G_COLS || [],
        pageRows: rows.length,
        countMethod: 'CountExport',
        filterSummary: [
            { label: 'Graduation year', value: G.qs('fYear').value || 'All years' },
            { label: 'Faculty', value: G.qs('fFac').value ? txt('fFac') : 'All faculties' },
            { label: 'Department', value: G.qs('fDep').value ? txt('fDep') : 'All departments' },
            { label: 'Programme', value: G.qs('fProg').value ? txt('fProg') : 'All programmes' }
        ],
        sorts: [
            { k: 'name', t: 'Name' },
            { k: 'regno', t: 'Student number' },
            { k: 'cgpa', t: 'Performance — highest CGPA first' }
        ],
        sortDefault: 'name',
        groups: [
            { k: 'prog', t: 'Programme' },
            { k: '', t: 'One continuous list' }
        ],
        groupDefault: 'prog',
        sheets: [
            { k: 'stale', t: 'Holds that can be lifted', d: 'Nothing is blocking these students any more', on: true }
        ]
    });
}

function footer(g) {
    return '<button type="button" class="g-btn g-btn--p" id="mRelease">Lift the hold</button>' +
           '<button type="button" class="g-btn" id="mEdit">Edit the reason</button>' +
           '<button type="button" class="g-btn" id="mClear">Clear for graduation</button>' +
           '<label class="g-auto" title="After a decision, open the next hold in this filter">' +
             '<input type="checkbox" id="mAuto"' + (G.autoAdvance() ? ' checked' : '') + ' />' +
             'Move to the next' +
           '</label>';
}
function open(reg) {
    G.openStudent(PAGE, reg, footer, function () {
        if (G.qs('mRelease')) G.qs('mRelease').addEventListener('click', doRelease);
        if (G.qs('mEdit')) G.qs('mEdit').addEventListener('click', doEdit);
        if (G.qs('mClear')) G.qs('mClear').addEventListener('click', doClear);
        if (G.qs('mAuto')) G.qs('mAuto').addEventListener('change', function () {
            G.setAutoAdvance(this.checked);
        });
    });
}

function year() { return G.qs('fYear').value; }

/* Marked in place and on to the next hold, rather than reloading the list under the reviewer.
   The queue refreshes when the modal closes. */
var dirty = false;

function afterDecision(regno, label) {
    dirty = true;
    G.markDecided(regno, label);
    if (!G.autoAdvance()) { G.closeModal(); return; }
    var i = G.queueNext();
    if (i >= 0) { open(G.queueAt(i)); return; }
    G.closeModal();
    G.toast('That was the last hold matching these filters.', true);
}

function doRelease() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    G.ajax(PAGE, 'ReleaseStudent', { regno: g.regno, acadYear: year(), note: '' }, function (d) {
        if (d && d.success) { G.toast(d.message, true); afterDecision(g.regno, 'Released'); }
        else G.toast((d && d.message) || 'Could not lift that hold.', false);
    });
}

/* Re-holding with a new reason supersedes rather than overwrites, so the original stays in the
   decision history where the next reviewer can still read it. */
function doEdit() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    G.reasonDialog({
        page: PAGE, regno: g.regno,
        title: 'Update why ' + g.name + ' is held',
        subtitle: 'The original is kept in the decision history.',
        verb: 'Update the reason',
        initial: g.holdReason || '',
        onSubmit: function (reason) {
            if (reason === (g.holdReason || '').trim()) {
                G.toast('That is the reason already recorded.', false);
                return;
            }
            G.ajax(PAGE, 'HoldStudent', { regno: g.regno, acadYear: year(), reason: reason }, function (d) {
                if (d && d.success) { G.toast('The reason has been updated.', true); dirty = true; open(g.regno); }
                else G.toast((d && d.message) || 'Could not update the reason.', false);
            });
        }
    });
}

function doClear() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    if (!year()) { G.toast('Choose the graduation year first.', false); return; }

    function send(note) {
        G.ajax(PAGE, 'ClearStudent',
            { regno: g.regno, acadYear: year(), note: note || '', overrideBlock: g.readiness === 'BLOCKED' },
            function (d) {
                if (d && d.success) { G.toast(d.message, true); afterDecision(g.regno, 'On ' + year()); }
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
            subtitle: g.regno + '  ·  ' + (g.progname || g.progcode),
            warn: 'Still blocked: ' + b.join('; ') +
                  ' Clearing is recorded against your name and shown on the graduation list.',
            verb: 'Clear anyway',
            onSubmit: send
        });
    } else if (confirm('Put ' + g.name + ' on the ' + year() + ' graduation list?')) send('');
}


document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    G.wireModal(function () { if (dirty) { dirty = false; load(); } });
    var pre = G.readUrl();

    document.addEventListener('click', function (e) {
        var b = e.target.closest ? e.target.closest('[data-open]') : null;
        if (b) { e.stopPropagation(); open(b.getAttribute('data-open')); return; }
        var tr = e.target.closest ? e.target.closest('#gBody tr[data-reg]') : null;
        if (tr) open(tr.getAttribute('data-reg'));
    });
    ['fYear', 'fProg', 'fStale'].forEach(function (id) {
        G.qs(id).addEventListener('change', function () { page = 1; sync(); });
    });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync(); });
    var typed = G.debounce(function () { page = 1; sync(); }, 350);
    G.qs('fQ').addEventListener('input', typed);
    G.qs('fQ').addEventListener('keydown', function (e) { if (e.key === 'Enter') { page = 1; sync(); } });
    G.qs('btnXls').addEventListener('click', openExport);
    G.qs('btnReset').addEventListener('click', function () {
        resetFilters();
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
        G.qs('fYear').value = pre.year || o.currentYear || '';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        if (pre.q) G.qs('fQ').value = pre.q;
        if (pre.stale) G.qs('fStale').value = pre.stale;
        page = parseInt(pre.page || '1', 10) || 1;
        G.cascade('fFac', 'fDep', 'fProg');
        chips();
        load();
    }

    if (window.G_BOOT) applyBoot(window.G_BOOT);
    else G.ajax(PAGE, 'GetBootstrap', {}, applyBoot);
});
})();
</script>
</asp:Content>
