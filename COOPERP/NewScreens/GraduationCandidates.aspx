<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationCandidates.aspx.cs" Inherits="COOPERP_NewScreens_GraduationCandidates" Title="Graduation Candidates - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Candidates</b><span id="gScope">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 118px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:0 0 184px;"><label for="fFocus">Who to show</label>
      <select id="fFocus"><option value="cycle">This cycle &mdash; finishing now</option><option value="all">Everyone not yet graduated</option></select></div>
    <div class="g-f" style="flex:1 1 130px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 130px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:0 0 88px;"><label for="fIntake">Intake</label><select id="fIntake"></select></div>
    <div class="g-f" style="flex:0 0 116px;"><label for="fReady">Readiness</label>
      <select id="fReady"><option value="">Any</option><option value="ready">Ready</option><option value="warn">Needs a look</option><option value="blocked">Blocked</option></select></div>
    <div class="g-f" style="flex:1 1 130px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number or name&hellip;" autocomplete="off" /></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn g-btn--p" id="btnBulk" disabled="disabled">Clear selected</button>
      <button type="button" class="g-btn" id="btnXls">Export</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

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

<script src="js/graduation.js"></script>
<script type="text/javascript">
(function () {
'use strict';
var PAGE = 'GraduationCandidates.aspx', boot = null, page = 1, rows = [];

function state() {
    return { year: G.qs('fYear').value, focus: G.qs('fFocus').value, faculty: G.qs('fFac').value,
             dept: G.qs('fDep').value, prog: G.qs('fProg').value, intake: G.qs('fIntake').value,
             ready: G.qs('fReady').value, q: G.qs('fQ').value.trim(), page: page > 1 ? page : '' };
}
function cfg(extra) {
    var s = state();
    var c = { acadYear: s.year, focus: s.focus, faculty: s.faculty, department: s.dept,
              programme: s.prog, entryYear: s.intake, readiness: s.ready, search: s.q,
              state: 'pending', page: String(page), size: '50' };
    if (extra) for (var k in extra) if (extra.hasOwnProperty(k)) c[k] = extra[k];
    return JSON.stringify(c);
}
function sync() { G.writeUrl(state()); load(); }

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
        ? (' · finishing ' + (boot && boot.previousYear ? boot.previousYear + ' or ' : '') + G.qs('fYear').value)
        : ' · everyone not yet graduated';
    G.qs('gMeta').textContent = 'showing ' + rows.length + ' of ' + d.total +
        (d.pages > 1 ? (' · page ' + d.page + ' of ' + d.pages) : '') + scope;

    var pg = '';
    if (d.pages > 1) {
        pg = '<button type="button" class="g-btn g-btn--sm" id="pgPrev"' + (d.page <= 1 ? ' disabled' : '') + '>Previous</button>' +
             '<span>page ' + d.page + ' of ' + d.pages + '</span>' +
             '<button type="button" class="g-btn g-btn--sm" id="pgNext"' + (d.page >= d.pages ? ' disabled' : '') + '>Next</button>';
    }
    G.qs('gPager').innerHTML = pg;
    if (G.qs('pgPrev')) G.qs('pgPrev').addEventListener('click', function () { if (page > 1) { page--; sync(); } });
    if (G.qs('pgNext')) G.qs('pgNext').addEventListener('click', function () { page++; sync(); });

    G.qs('ckAll').checked = false;
    syncBulk();
}

/* ── bulk ── */
function selected() {
    var out = [], b = document.querySelectorAll('#gBody .ck:checked');
    for (var i = 0; i < b.length; i++) out.push(b[i].getAttribute('data-reg'));
    return out;
}
function syncBulk() {
    var n = selected().length, b = G.qs('btnBulk');
    b.disabled = (n === 0);
    b.textContent = n ? ('Clear ' + n + ' selected') : 'Clear selected';
}
function doBulk() {
    var regs = selected();
    if (!regs.length) return;
    if (!G.qs('fYear').value) { G.toast('Choose the graduation year first.', false); return; }
    if (!confirm('Put ' + regs.length + ' candidate' + (regs.length === 1 ? '' : 's') + ' on the ' +
                 G.qs('fYear').value + ' graduation list?\n\nEach one is re-checked on the server before it is added.')) return;
    G.qs('btnBulk').disabled = true;
    G.ajax(PAGE, 'ClearMany', { regnos: regs.join(','), acadYear: G.qs('fYear').value }, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'The bulk clear did not run.', false); syncBulk(); return; }
        G.toast(d.message, d.cleared > 0);
        if (d.skipped && d.skipped.length) alert('Skipped ' + d.skipped.length + ':\n\n' + d.skipped.join('\n'));
        load();
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
           '<span class="g-sub" style="margin-left:auto;">' +
           (g.readiness === 'BLOCKED' ? 'Blocked — clearing will ask you to justify it.'
            : g.readiness === 'WARN' ? 'Read the warnings before clearing.' : 'Nothing outstanding.') + '</span>';
}

function open(reg) {
    G.openStudent(PAGE, reg, footer);
    setTimeout(wireFooter, 0);
}
function wireFooter() {
    var c = G.qs('mClear'), h = G.qs('mHold'), r = G.qs('mRelease');
    if (c) c.addEventListener('click', doClear);
    if (h) h.addEventListener('click', doHold);
    if (r) r.addEventListener('click', doRelease);
}

function doClear() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student, year = G.qs('fYear').value, note = '';
    if (!year) { G.toast('Choose the graduation year first.', false); return; }
    if (g.readiness === 'BLOCKED') {
        var b = [];
        for (var i = 0; i < g.findings.length; i++) if (g.findings[i].level === 'BLOCK') b.push('• ' + g.findings[i].detail);
        note = prompt('This candidate is BLOCKED:\n\n' + b.join('\n') +
            '\n\nClearing them anyway is recorded against your name and shown on the graduation list.\n' +
            'Say why (at least 10 characters):', '');
        if (note === null) return;
        if (note.trim().length < 10) { G.toast('A justification of at least 10 characters is needed.', false); return; }
    } else if (!confirm('Put ' + g.name + ' on the ' + year + ' graduation list?')) return;

    G.ajax(PAGE, 'ClearStudent',
        { regno: g.regno, acadYear: year, note: note, overrideBlock: g.readiness === 'BLOCKED' },
        function (d) {
            if (d && d.success) { G.toast(d.message, true); G.closeModal(); load(); }
            else G.toast((d && d.message) || 'Could not clear that candidate.', false);
        });
}

function doHold() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student, year = G.qs('fYear').value;
    if (!year) { G.toast('Choose the graduation year first.', false); return; }
    var reason = prompt('Why is ' + g.name + ' being held?\n\n' +
        'Whoever picks this up next has only this sentence to go on, so be specific (at least 10 characters).', '');
    if (reason === null) return;
    if (reason.trim().length < 10) { G.toast('Say a little more — at least 10 characters.', false); return; }
    G.ajax(PAGE, 'HoldStudent', { regno: g.regno, acadYear: year, reason: reason }, function (d) {
        if (d && d.success) { G.toast(d.message, true); G.closeModal(); load(); }
        else G.toast((d && d.message) || 'Could not hold that candidate.', false);
    });
}

function doRelease() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    var note = prompt('Lift the hold on ' + g.name + '?\n\nAnything worth noting? (optional)', '');
    if (note === null) return;
    G.ajax(PAGE, 'ReleaseStudent', { regno: g.regno, acadYear: G.qs('fYear').value, note: note }, function (d) {
        if (d && d.success) { G.toast(d.message, true); G.closeModal(); load(); }
        else G.toast((d && d.message) || 'Could not lift that hold.', false);
    });
}

document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    G.wireModal();
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
    G.qs('fQ').addEventListener('keydown', function (e) { if (e.key === 'Enter') { page = 1; sync(); } });
    G.qs('btnBulk').addEventListener('click', doBulk);
    G.qs('btnXls').addEventListener('click', function () { G.serverExport(PAGE, 'xls', cfg()); });
    G.qs('btnReset').addEventListener('click', function () {
        ['fFac', 'fDep', 'fProg', 'fIntake', 'fReady'].forEach(function (id) { G.qs(id).value = ''; });
        G.qs('fQ').value = ''; G.qs('fFocus').value = 'cycle';
        if (boot && boot.currentYear) G.qs('fYear').value = boot.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync();
    });

    G.ajax(PAGE, 'GetBootstrap', {}, function (o) {
        if (!o || !o.success || !o.hasAccess) {
            G.qs('gToolbar').style.display = 'none';
            var na = G.qs('gNoAccess'); na.style.display = 'block';
            na.textContent = (o && o.message) || 'You do not have access to graduation data.';
            return;
        }
        boot = o;
        G.qs('gScope').textContent = (o.roleNote || '') + (o.scopeLabel ? ' · ' + o.scopeLabel : '');
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
        if (pre.q) G.qs('fQ').value = pre.q;
        page = parseInt(pre.page || '1', 10) || 1;
        G.cascade('fFac', 'fDep', 'fProg');
        load();
    });
});
})();
</script>
</asp:Content>
