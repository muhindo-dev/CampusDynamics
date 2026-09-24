<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationCentre.aspx.cs" Inherits="COOPERP_NewScreens_GraduationCentre" Title="Graduation Centre - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<%-- Overview. No banner and no tab strip: the sidebar already says where you are, and
     repeating it costs ninety pixels of the only screen a Registrar has. Everything above
     the figures is one wrapping row. --%>
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Graduation overview</b><span id="gScope">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 122px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:0 0 196px;"><label for="fFocus">Who to show</label>
      <select id="fFocus"><option value="cycle">This cycle &mdash; finishing now</option><option value="all">Everyone not yet graduated</option></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 170px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnReset">Reset</button>
      <button type="button" class="g-btn" id="btnXls">Export</button>
      <button type="button" class="g-btn" id="btnRefresh"
              title="Counts read a per-student summary. Decisions always read live marks.">Refresh</button>
    </div>
  </div>

  <div class="g-note g-note--info" id="gFocus" style="display:none;"></div>
  <div id="gIntegrity"></div>
  <div class="g-kpis" id="gKpis"></div>

  <div class="g-card">
    <div class="g-card__h">Where the blockers are <small>what stands between these candidates and a list</small></div>
    <div class="g-card__b" id="gBlockers"><div class="g-load">Loading&hellip;</div></div>
  </div>

  <div class="g-card">
    <div class="g-card__h">Progress by programme <small>click a row to work through it</small></div>
    <div class="g-wrap" style="max-height:460px;">
      <table class="g-tbl"><thead><tr>
        <th>Programme</th><th class="g-num">Candidates</th><th class="g-num">On a list</th>
        <th class="g-num">Held</th><th class="g-num">Failed papers</th><th class="g-num">To review</th>
      </tr></thead><tbody id="gProg"></tbody></table>
    </div>
  </div>

  <div id="gNoAccess" class="g-note g-note--warn" style="display:none;"></div>
</div>

<script type="text/javascript">
// The first paint, rendered into the page by Page_Load. Two sequential round trips - and two
// turns of the per-session PageMethod lock - removed from the opening of every visit.
window.G_BOOT = <%= BootJson %>;
window.G_DATA = <%= DataJson %>;
window.G_AGE  = '<%= StatsAge %>';
</script>
<script src="js/graduation.js"></script>
<script type="text/javascript">
(function () {
'use strict';
var PAGE = 'GraduationCentre.aspx', boot = null;

function state() {
    return { year: G.qs('fYear').value, focus: G.qs('fFocus').value, faculty: G.qs('fFac').value,
             dept: G.qs('fDep').value, prog: G.qs('fProg').value };
}
function cfg() {
    var s = state();
    return JSON.stringify({ acadYear: s.year, focus: s.focus, faculty: s.faculty,
                            department: s.dept, programme: s.prog });
}
function sync() { G.writeUrl(state()); load(); }

function load() {
    G.qs('gBlockers').innerHTML = '<div class="g-load">Loading&hellip;</div>';
    G.qs('gProg').innerHTML = '';
    G.ajax(PAGE, 'GetOverview', { configJson: cfg() }, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'Could not load the overview.', false); return; }
        render(d.overview);
    });
}

// Each figure links into the page that lists exactly those people. A number you cannot drill
// into is decoration.
function kpi(href, kind, val, label, sub) {
    return '<a class="g-kpi' + (kind ? ' g-kpi--' + kind : '') + '" href="' + href + '">' +
           '<b>' + val + '</b><span>' + G.esc(label) + '</span><small>' + G.esc(sub) + '</small></a>';
}

function render(o) {
    var s = state(), i;
    var q = '?year=' + encodeURIComponent(s.year) + '&focus=' + encodeURIComponent(s.focus) +
            (s.faculty ? '&faculty=' + encodeURIComponent(s.faculty) : '') +
            (s.dept ? '&dept=' + encodeURIComponent(s.dept) : '') +
            (s.prog ? '&prog=' + encodeURIComponent(s.prog) : '');

    var f = G.qs('gFocus');
    if (o.focusLabel) {
        f.style.display = 'block';
        f.innerHTML = o.focusFrom
            ? '<b>Showing the ' + G.esc(o.focusTo) + ' graduating cycle.</b> Every figure on this page covers ' +
              'students who last sat a paper in <b>' + G.esc(o.focusFrom) + '</b> or <b>' + G.esc(o.focusTo) +
              '</b>. Switch <i>Who to show</i> for everyone who has ever reached a final year and never graduated.'
            : '<b>Showing everyone not yet graduated,</b> including students who finished years ago and were ' +
              'never put on a list.';
    } else f.style.display = 'none';

    G.qs('gKpis').innerHTML =
        kpi('GraduationCandidates.aspx' + q, '', o.candidates, 'Candidates',
            o.focusFrom ? ('Finished in ' + o.focusFrom + ' or ' + o.focusTo + ', on no list yet.')
                        : 'Reached a final year and are on no list.') +
        kpi('GraduationCandidates.aspx' + q + '&ready=ready', 'ok', o.ready, 'Ready',
            'Nothing outstanding. Work through these first.') +
        kpi('GraduationCandidates.aspx' + q + '&ready=blocked', 'bad', o.blocked, 'Blocked',
            'At least one check stops them.') +
        kpi('GraduationHeld.aspx' + q, 'warn', o.held, 'Held',
            'Stopped by a reviewer, with a reason.') +
        kpi('GraduationList.aspx' + q, '', o.listed, 'On the list',
            'Already on the graduation list for this year.');

    var h = '';
    if (o.blockers && o.blockers.length) {
        h = '<div class="g-wrap"><table class="g-tbl"><thead><tr><th>What is stopping them</th>' +
            '<th class="g-num">Candidates</th><th style="width:38%"></th></tr></thead><tbody>';
        for (i = 0; i < o.blockers.length; i++) {
            var b = o.blockers[i];
            var pct = o.candidates ? Math.round(b.count * 100 / o.candidates) : 0;
            h += '<tr><td>' + G.esc(b.name) + '</td><td class="g-num"><b>' + b.count + '</b></td>' +
                 '<td>' + (b.count ? '<div class="g-bar2"><div class="g-bar2__t"><div class="g-bar2__f is-short" ' +
                 'style="width:' + pct + '%"></div></div><div class="g-bar2__v">' + pct + '%</div></div>' : '') +
                 '</td></tr>';
        }
        h += '</tbody></table></div>';
    } else h = '<div class="g-empty">Nothing is blocking anyone in this scope.</div>';
    G.qs('gBlockers').innerHTML = h;

    var p = '';
    for (i = 0; i < (o.programmes || []).length; i++) {
        var r = o.programmes[i];
        var left = r.candidates - r.listed - r.held; if (left < 0) left = 0;
        p += '<tr class="is-click" data-prog="' + G.esc(r.progcode) + '">' +
             '<td><b>' + G.esc(r.progname || r.progcode) + '</b><div class="g-sub">' + G.esc(r.progcode) + '</div></td>' +
             '<td class="g-num">' + r.candidates + '</td><td class="g-num">' + r.listed + '</td>' +
             '<td class="g-num">' + (r.held || '') + '</td><td class="g-num">' + (r.blocked || '') + '</td>' +
             '<td class="g-num"><b>' + left + '</b></td></tr>';
    }
    G.qs('gProg').innerHTML = p || '<tr><td colspan="6" class="g-empty">No programmes in scope.</td></tr>';

    var ig = '';
    for (i = 0; i < (o.integrity || []).length; i++)
        ig += '<div class="g-note g-note--bad"><b>Data integrity.</b> ' + G.esc(o.integrity[i]) +
              ' Nothing has been changed &mdash; that is a decision for the Registrar.</div>';
    G.qs('gIntegrity').innerHTML = ig;
}

document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    var pre = G.readUrl();

    G.qs('gProg').addEventListener('click', function (e) {
        var tr = e.target.closest ? e.target.closest('tr[data-prog]') : null;
        if (!tr) return;
        var s = state();
        location.href = 'GraduationCandidates.aspx?year=' + encodeURIComponent(s.year) +
            '&focus=' + encodeURIComponent(s.focus) + '&prog=' + encodeURIComponent(tr.getAttribute('data-prog'));
    });
    ['fYear', 'fFocus', 'fProg'].forEach(function (id) { G.qs(id).addEventListener('change', sync); });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('btnReset').addEventListener('click', function () {
        G.qs('fFac').value = ''; G.qs('fDep').value = ''; G.qs('fProg').value = '';
        G.qs('fFocus').value = 'cycle';
        if (boot && boot.currentYear) G.qs('fYear').value = boot.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); sync();
    });
    G.qs('btnXls').addEventListener('click', function () { G.serverExport(PAGE, 'xls', cfg()); });
    G.qs('btnRefresh').addEventListener('click', function () {
        var b = G.qs('btnRefresh');
        b.disabled = true; b.textContent = 'Rebuilding…';
        G.ajax(PAGE, 'RefreshStats', {}, function (d) {
            b.disabled = false; b.textContent = 'Refresh';
            if (!d || !d.success) { G.toast((d && d.message) || 'The rebuild did not run.', false); return; }
            G.toast(d.message, true);
            window.G_AGE = d.freshness;
            load();
        });
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
        G.qs('fFocus').value = pre.focus || 'cycle';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        G.cascade('fFac', 'fDep', 'fProg');
        // The overview arrived with the page; render it without a round trip.
        if (window.G_DATA && window.G_DATA.success) render(window.G_DATA.overview);
        else load();
    }

    if (window.G_BOOT) boot(window.G_BOOT);
    else G.ajax(PAGE, 'GetBootstrap', {}, boot);
});
})();
</script>
</asp:Content>
