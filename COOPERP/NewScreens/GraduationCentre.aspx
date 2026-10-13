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
    <div class="g-bar__id"><b>Graduation overview</b><span id="gScope">&nbsp;</span>
      <span class="g-age" id="gAge">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 122px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:0 0 196px;"><label for="fFocus">Who to show</label>
      <select id="fFocus"><option value="cycle">This cycle, finishing now</option><option value="all">Everyone not yet graduated</option></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 170px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnReset">Reset</button>
      <button type="button" class="g-btn" id="btnXls">Export&hellip;</button>
      <button type="button" class="g-btn" id="btnRefresh"
              title="Counts read a per-student summary. Decisions always read live marks.">Refresh</button>
    </div>
  </div>

  <div class="g-chips" id="gChips"></div>

  <div class="g-note g-note--info" id="gFocus" style="display:none;"></div>
  <div id="gIntegrity"></div>
  <div class="g-kpis" id="gKpis"></div>

  <div class="g-card gd-card" id="gdCard" style="display:none;">
    <div class="g-card__h"><span>Graduation dates</span>
      <small>printed on certificates (ceremony) and transcripts (date of completion)</small></div>
    <div class="g-card__b">
      <div class="gd-edit" id="gdEdit">
        <div class="g-f"><label for="gdYear">Graduation year</label><select id="gdYear"></select></div>
        <div class="g-f"><label for="gdGrad">Graduation date <em>certificate</em></label><input type="date" id="gdGrad" /></div>
        <div class="g-f"><label for="gdComp">Date of completion <em>transcript</em></label><input type="date" id="gdComp" /></div>
        <label class="gd-cur"><input type="checkbox" id="gdCurrent" checked="checked" /> This is the current graduation</label>
        <button type="button" class="g-btn g-btn--p" id="gdSave">Save and apply</button>
      </div>
      <div class="gd-preview" id="gdPreview"></div>
      <div class="g-wrap"><table class="g-tbl gd-tbl"><thead><tr>
        <th>Year</th><th class="g-num">Graduands</th><th>Certificate: graduation date</th>
        <th>Transcript: date of completion</th><th>Last set</th><th></th></tr></thead>
        <tbody id="gdRows"></tbody></table></div>
      <div class="g-note g-note--info" style="margin:8px 0 0;" id="gdNote"></div>
    </div>
  </div>

  <div class="g-card">
    <div class="g-card__h">Where the blockers are <small>what stands between these candidates and a list</small></div>
    <div class="g-card__b" id="gBlockers"><div class="g-load">Loading&hellip;</div></div>
  </div>

  <div class="g-card">
    <div class="g-card__h">Progress by programme <small>click a row to work through it</small></div>
    <div class="g-wrap" style="max-height:460px;">
      <table class="g-tbl"><thead><tr>
        <th>Programme</th><th class="g-num">Candidates</th><th class="g-num">On a list</th>
        <th class="g-num">On hold</th><th class="g-num">Failed papers</th><th class="g-num">To review</th>
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
/* BOOT, not boot: the old name was shadowed by "function boot(o)" inside DOMContentLoaded, so
   the store stayed null and Reset blanked the graduation year instead of restoring it. */
var PAGE = 'GraduationCentre.aspx', BOOT = null;

function state() {
    return { year: G.qs('fYear').value, focus: G.qs('fFocus').value, faculty: G.qs('fFac').value,
             dept: G.qs('fDep').value, prog: G.qs('fProg').value };
}
function cfg() {
    var s = state();
    return JSON.stringify({ acadYear: s.year, focus: s.focus, faculty: s.faculty,
                            department: s.dept, programme: s.prog });
}
function sync() { G.writeUrl(state()); chips(); load(); }

function txt(id) {
    var el = G.qs(id);
    return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.replace(/\s*\(\d+\)\s*$/, '')
                                          .replace(/\s*\(none\)\s*$/, '').trim() : '';
}

/* What is narrowing the view, each chip removable on its own. */
function chips() {
    G.chips('gChips', [
        { k: 'fFac',  label: 'Faculty',    value: G.qs('fFac').value ? txt('fFac') : '' },
        { k: 'fDep',  label: 'Department', value: G.qs('fDep').value ? txt('fDep') : '' },
        { k: 'fProg', label: 'Programme',  value: G.qs('fProg').value ? txt('fProg') : '' }
    ], function (k) {
        if (k === '*') resetFilters(); else G.qs(k).value = '';
        G.cascade('fFac', 'fDep', 'fProg');
        sync();
    });
}

function resetFilters() {
    ['fFac', 'fDep', 'fProg'].forEach(function (id) { G.qs(id).value = ''; });
}

/* ── export ──────────────────────────────────────────────────────────
   The overview has no per-student rows, so the dialog offers the tabs rather than the columns:
   the five figures, the faculty roll-up, the programme table, and any integrity findings. */
function openExport() {
    G.exportDialog({
        page: PAGE,
        title: 'Export the overview',
        subtitle: 'Summary tables, not a student list. For names, export from Candidates or the list.',
        cfg: cfg(),
        columns: [],
        pageRows: 0,
        countMethod: 'CountExport',
        filterSummary: [
            { label: 'Graduation year', value: G.qs('fYear').value || 'All years' },
            { label: 'Population', value: G.qs('fFocus').value === 'cycle'
                ? ('Finishing ' + (BOOT && BOOT.previousYear ? BOOT.previousYear + ' or ' : '') + G.qs('fYear').value)
                : 'Everyone not yet graduated' },
            { label: 'Faculty', value: G.qs('fFac').value ? txt('fFac') : 'All faculties' },
            { label: 'Department', value: G.qs('fDep').value ? txt('fDep') : 'All departments' },
            { label: 'Programme', value: G.qs('fProg').value ? txt('fProg') : 'All programmes' }
        ],
        sheets: [
            { k: 'summary', t: 'Summary', d: 'The five figures, and what is stopping the rest', on: true },
            { k: 'byfac', t: 'By faculty', d: 'The same table rolled up', on: true },
            { k: 'byprog', t: 'By programme', d: 'Candidates, approved, on hold, blocked, to review', on: true },
            { k: 'integrity', t: 'Data integrity', d: 'Anything the engine refused to assume', on: true }
        ]
    });
}

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
              ' Nothing has been changed, that is a decision for the Registrar.</div>';
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
        resetFilters();
        G.qs('fFocus').value = 'cycle';
        if (BOOT && BOOT.currentYear) G.qs('fYear').value = BOOT.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); sync();
    });
    G.qs('btnXls').addEventListener('click', openExport);
    G.qs('btnRefresh').addEventListener('click', function () {
        var b = G.qs('btnRefresh');
        b.disabled = true; b.textContent = 'Rebuilding…';
        G.ajax(PAGE, 'RefreshStats', {}, function (d) {
            b.disabled = false; b.textContent = 'Refresh';
            if (!d || !d.success) { G.toast((d && d.message) || 'The rebuild did not run.', false); return; }
            G.toast(d.message, true);
            window.G_AGE = d.freshness;
            G.freshness('gAge');
            load();
        });
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
        G.qs('fFocus').value = pre.focus || 'cycle';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        G.cascade('fFac', 'fDep', 'fProg');
        // This page was the only one left with a bare select here, so a programme whose name
        // runs past the field was unreadable and unsearchable.
        G.combo('fProg', 'Type a code or part of the name…', { key: true });
        chips();
        // The overview arrived with the page; render it without a round trip.
        if (window.G_DATA && window.G_DATA.success) render(window.G_DATA.overview);
        else load();
    }

    if (window.G_BOOT) applyBoot(window.G_BOOT);
    else G.ajax(PAGE, 'GetBootstrap', {}, applyBoot);
});
})();
</script>
<style>
.gd-edit{display:flex;align-items:flex-end;gap:10px;flex-wrap:wrap;margin-bottom:8px;}
.gd-edit .g-f{flex:0 0 190px;}
.gd-edit label em{font-style:normal;font-weight:600;color:var(--accent);text-transform:none;letter-spacing:0;margin-left:4px;}
.gd-edit input[type=date],.gd-edit select{width:100%;box-sizing:border-box;height:30px;border:1px solid var(--line);padding:0 7px;font:inherit;font-size:12.5px;background:#fff;}
.gd-cur{display:inline-flex;align-items:center;gap:6px;font-size:12px;font-weight:600;color:var(--ink2);height:30px;}
.gd-preview{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:10px;}
.gd-doc{flex:1 1 260px;border:1px solid var(--line);border-left:3px solid var(--accent);border-radius:4px;padding:8px 11px;background:#fff;}
.gd-doc span{display:block;font-size:9.5px;text-transform:uppercase;letter-spacing:.5px;color:var(--mute);font-weight:700;}
.gd-doc b{display:block;font-size:15px;color:var(--navy);margin-top:2px;font-family:Georgia,"Times New Roman",serif;}
.gd-doc small{display:block;font-size:10.5px;color:var(--ink2);margin-top:2px;}
.gd-doc--empty b{color:var(--mute);font-family:inherit;font-size:12.5px;font-weight:600;}
.gd-tbl td,.gd-tbl th{font-size:11.5px;}
.gd-tbl tr.is-cur td{background:#f5f8ff;}
.gd-tag{display:inline-block;font-size:9.5px;font-weight:700;padding:1px 7px;border-radius:9px;background:var(--navy);color:#fff;margin-left:6px;}
.gd-unset{color:var(--mute);font-style:italic;}
.gd-pend{color:var(--warn);font-weight:700;font-size:10.5px;}
</style>
<script type="text/javascript">
/* ── Graduation dates ───────────────────────────────────────────────────
   One ceremony date and one completion date per graduation list year. Saving writes them onto
   every graduand of that year, so whichever document is generated next carries them. */
(function () {
'use strict';
var PAGE = 'GraduationCentre.aspx', DATA = null;

function longDate(iso) {
    if (!iso) return '';
    var p = iso.split('-'), m = ['January','February','March','April','May','June','July','August','September','October','November','December'];
    var d = +p[2], suf = (d % 10 === 1 && d !== 11) ? 'st' : (d % 10 === 2 && d !== 12) ? 'nd' : (d % 10 === 3 && d !== 13) ? 'rd' : 'th';
    return d + suf + ' ' + m[+p[1] - 1] + ', ' + p[0];
}
function rowFor(y) { if (!DATA) return null; for (var i = 0; i < DATA.years.length; i++) if (DATA.years[i].year === y) return DATA.years[i]; return null; }

function preview() {
    var g = G.qs('gdGrad').value, c = G.qs('gdComp').value;
    function doc(label, iso, what) {
        return '<div class="gd-doc' + (iso ? '' : ' gd-doc--empty') + '"><span>' + label + '</span><b>' +
               (iso ? G.esc(longDate(iso)) : 'Not set') + '</b><small>' + what + '</small></div>';
    }
    G.qs('gdPreview').innerHTML =
        doc('Certificate will read', g, g ? 'graduation date, as printed on every certificate for this year'
                                          : 'certificates use whatever date each graduand already carries') +
        doc('Transcript will read', c, c ? 'date of completion, unless a student has their own completion date'
                                          : 'transcripts fall back to 30 June of the final academic year');
}

function fillEditor(y) {
    var r = rowFor(y);
    G.qs('gdGrad').value = r ? r.graduationDate : '';
    G.qs('gdComp').value = r ? r.completionDate : '';
    G.qs('gdCurrent').checked = r ? (r.isCurrent || !DATA.years.some(function (x) { return x.isCurrent; })) : true;
    preview();
}

function draw() {
    var d = DATA, h = '';
    d.years.forEach(function (r) {
        h += '<tr' + (r.isCurrent ? ' class="is-cur"' : '') + '><td><b>' + G.esc(r.year) + '</b>' + (r.isCurrent ? '<span class="gd-tag">current</span>' : '') + '</td>' +
             '<td class="g-num">' + r.graduands + '</td>' +
             '<td>' + (r.graduationDate ? G.esc(longDate(r.graduationDate)) : '<span class="gd-unset">not set</span>') + '</td>' +
             '<td>' + (r.completionDate ? G.esc(longDate(r.completionDate)) : '<span class="gd-unset">not set (30 June default)</span>') + '</td>' +
             '<td>' + (r.by ? G.esc(r.by) + ' &middot; ' + G.esc(r.at) : '<span class="g-sub">never</span>') +
               (r.pending ? '<div class="gd-pend">' + r.pending + ' graduand row(s) not yet carrying these dates</div>' : '') + '</td>' +
             '<td>' + (d.canSet ? '<button type="button" class="g-btn g-btn--sm" data-gdy="' + G.esc(r.year) + '">Edit</button>' : '') + '</td></tr>';
    });
    G.qs('gdRows').innerHTML = h || '<tr><td colspan="6" class="g-empty">No graduation lists yet.</td></tr>';
    G.qs('gdNote').innerHTML = 'Saving applies the dates to every graduand on that year’s list, so the next certificate or transcript generated carries them. ' +
        'A student given their own completion date (Student Information, quick edit) keeps it' +
        (d.studentOverrides ? ' — ' + d.studentOverrides + ' graduand' + (d.studentOverrides === 1 ? ' has' : 's have') + ' one' : '') + '. ' +
        (d.canSet ? 'Every change is logged with the old and new dates.' : 'Only an administrator, the Academic Registrar or the Graduation Officer can change these dates.');
    G.qs('gdEdit').style.display = d.canSet ? 'flex' : 'none';
    G.qs('gdPreview').style.display = d.canSet ? 'flex' : 'none';
    var sel = G.qs('gdYear'), keep = sel.value;
    sel.innerHTML = d.years.map(function (r) { return '<option>' + G.esc(r.year) + '</option>'; }).join('');
    var cur = d.years.filter(function (r) { return r.isCurrent; })[0];
    var top = (G.qs('fYear') && G.qs('fYear').value) || '';
    sel.value = keep || (cur ? cur.year : '') || (rowFor(top) ? top : (d.years[0] ? d.years[0].year : ''));
    fillEditor(sel.value);
    var b = document.querySelectorAll('#gdRows [data-gdy]');
    for (var i = 0; i < b.length; i++) b[i].addEventListener('click', function () {
        sel.value = this.getAttribute('data-gdy'); fillEditor(sel.value);
        G.qs('gdEdit').scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    });
}

function load() {
    G.ajax(PAGE, 'GetGradDates', {}, function (d) {
        if (!d || !d.success) return;
        DATA = d;
        G.qs('gdCard').style.display = '';
        draw();
    });
}

function save() {
    var y = G.qs('gdYear').value, g = G.qs('gdGrad').value, c = G.qs('gdComp').value;
    if (!y) { G.toast('Choose the graduation year.', false); return; }
    var r = rowFor(y);
    var msg = 'Set the ' + y + ' graduation dates?\n\nCertificate: ' + (g ? longDate(g) : 'not set') +
              '\nTranscript (date of completion): ' + (c ? longDate(c) : 'not set (30 June default)') +
              '\n\nThis is applied to ' + (r ? r.graduands : 0) + ' graduand(s) on the ' + y + ' list.';
    if (!confirm(msg)) return;
    var btn = G.qs('gdSave'); btn.disabled = true; btn.textContent = 'Saving…';
    G.ajax(PAGE, 'SaveGradDates', { acadYear: y, graduationDate: g, completionDate: c, makeCurrent: G.qs('gdCurrent').checked }, function (d) {
        btn.disabled = false; btn.textContent = 'Save and apply';
        G.toast((d && d.message) || 'Nothing was saved.', !!(d && d.success));
        if (d && d.success) load();
    });
}

document.addEventListener('DOMContentLoaded', function () {
    if (!G.qs('gdCard')) return;
    G.qs('gdYear').addEventListener('change', function () { fillEditor(this.value); });
    G.qs('gdGrad').addEventListener('input', preview);
    G.qs('gdComp').addEventListener('input', preview);
    G.qs('gdSave').addEventListener('click', save);
    load();
});
})();
</script>
</asp:Content>
