<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationHeld.aspx.cs" Inherits="COOPERP_NewScreens_GraduationHeld" Title="Held Candidates - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Held candidates</b><span id="gScope">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 122px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 170px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:1 1 140px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number or name&hellip;" autocomplete="off" /></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnXls">Export</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

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
  </div>

  <div id="gNoAccess" class="g-note g-note--warn" style="display:none;"></div>
</div>

<script type="text/javascript">
// Rendered into the page by Page_Load, so the controls are populated without a round trip.
window.G_BOOT = <%= BootJson %>;
window.G_AGE  = '<%= StatsAge %>';
</script>
<script src="js/graduation.js"></script>
<script type="text/javascript">
(function () {
'use strict';
var PAGE = 'GraduationHeld.aspx', boot = null, rows = [];

function state() {
    return { year: G.qs('fYear').value, faculty: G.qs('fFac').value, dept: G.qs('fDep').value,
             prog: G.qs('fProg').value, q: G.qs('fQ').value.trim() };
}
function cfg() {
    var s = state();
    return JSON.stringify({ acadYear: s.year, faculty: s.faculty, department: s.dept,
                            programme: s.prog, search: s.q, state: 'held', size: '300' });
}
function sync() { G.writeUrl(state()); load(); }

function load() {
    G.qs('gBody').innerHTML = '<tr><td colspan="6" class="g-load">Loading&hellip;</td></tr>';
    G.ajax(PAGE, 'GetHeld', { configJson: cfg() }, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'Could not load held candidates.', false); return; }
        rows = d.rows || [];
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
        G.qs('gMeta').textContent = rows.length + ' held';
    });
}

function footer(g) {
    return '<button type="button" class="g-btn g-btn--p" id="mRelease">Lift the hold</button>' +
           '<button type="button" class="g-btn" id="mEdit">Edit the reason</button>' +
           '<button type="button" class="g-btn" id="mClear">Clear for graduation</button>';
}
function open(reg) {
    G.openStudent(PAGE, reg, footer, function () {
        if (G.qs('mRelease')) G.qs('mRelease').addEventListener('click', doRelease);
        if (G.qs('mEdit')) G.qs('mEdit').addEventListener('click', doEdit);
        if (G.qs('mClear')) G.qs('mClear').addEventListener('click', doClear);
    });
}

function year() { return G.qs('fYear').value; }

function doRelease() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    var note = prompt('Lift the hold on ' + g.name + '?\n\nAnything worth noting? (optional)', '');
    if (note === null) return;
    G.ajax(PAGE, 'ReleaseStudent', { regno: g.regno, acadYear: year(), note: note }, function (d) {
        if (d && d.success) { G.toast(d.message, true); G.closeModal(); load(); }
        else G.toast((d && d.message) || 'Could not lift that hold.', false);
    });
}

// Re-holding with a new reason supersedes rather than overwrites, so the original stays in the
// decision history where the next reviewer can still read it.
function doEdit() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    var reason = prompt('Update the reason ' + g.name + ' is being held.\n\n' +
        'The original is kept in the decision history.', g.holdReason || '');
    if (reason === null) return;
    if (reason.trim().length < 10) { G.toast('Say a little more — at least 10 characters.', false); return; }
    if (reason.trim() === (g.holdReason || '').trim()) { G.toast('That is the reason already recorded.', false); return; }
    G.ajax(PAGE, 'HoldStudent', { regno: g.regno, acadYear: year(), reason: reason }, function (d) {
        if (d && d.success) { G.toast('The reason has been updated.', true); open(g.regno); load(); }
        else G.toast((d && d.message) || 'Could not update the reason.', false);
    });
}

function doClear() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student, note = '';
    if (!year()) { G.toast('Choose the graduation year first.', false); return; }
    if (g.readiness === 'BLOCKED') {
        var b = [];
        for (var i = 0; i < g.findings.length; i++) if (g.findings[i].level === 'BLOCK') b.push('• ' + g.findings[i].detail);
        note = prompt('This candidate is BLOCKED:\n\n' + b.join('\n') +
            '\n\nClearing them anyway is recorded against your name.\nSay why (at least 10 characters):', '');
        if (note === null) return;
        if (note.trim().length < 10) { G.toast('A justification of at least 10 characters is needed.', false); return; }
    } else if (!confirm('Put ' + g.name + ' on the ' + year() + ' graduation list?')) return;

    G.ajax(PAGE, 'ClearStudent',
        { regno: g.regno, acadYear: year(), note: note, overrideBlock: g.readiness === 'BLOCKED' },
        function (d) {
            if (d && d.success) { G.toast(d.message, true); G.closeModal(); load(); }
            else G.toast((d && d.message) || 'Could not clear that candidate.', false);
        });
}

document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    G.wireModal();
    var pre = G.readUrl();

    document.addEventListener('click', function (e) {
        var b = e.target.closest ? e.target.closest('[data-open]') : null;
        if (b) { e.stopPropagation(); open(b.getAttribute('data-open')); return; }
        var tr = e.target.closest ? e.target.closest('#gBody tr[data-reg]') : null;
        if (tr) open(tr.getAttribute('data-reg'));
    });
    ['fYear', 'fProg'].forEach(function (id) { G.qs(id).addEventListener('change', sync); });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('fQ').addEventListener('keydown', function (e) { if (e.key === 'Enter') sync(); });
    G.qs('btnXls').addEventListener('click', function () { G.serverExport(PAGE, 'xls', cfg()); });
    G.qs('btnReset').addEventListener('click', function () {
        ['fFac', 'fDep', 'fProg'].forEach(function (id) { G.qs(id).value = ''; });
        G.qs('fQ').value = '';
        if (boot && boot.currentYear) G.qs('fYear').value = boot.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); sync();
    });

    function boot(o) {
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
        G.qs('fYear').value = pre.year || o.currentYear || '';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        if (pre.q) G.qs('fQ').value = pre.q;
        G.cascade('fFac', 'fDep', 'fProg');
        load();
    }

    if (window.G_BOOT) boot(window.G_BOOT);
    else G.ajax(PAGE, 'GetBootstrap', {}, boot);
});
})();
</script>
</asp:Content>
