<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationAnalysis.aspx.cs" Inherits="COOPERP_NewScreens_GraduationAnalysis" Title="Graduation Analysis - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<%-- Rebuilt on the module's own idiom: no banner, no second design system, the same compact
     identity line and toolbar as the other four Graduation screens. --%>
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Graduation analysis</b><span id="gScope">&nbsp;</span>
      <span class="g-age" id="gLens">&nbsp;</span></div>

    <div class="g-f" style="flex:0 0 136px;"><label for="fLens">Look at it by</label>
      <select id="fLens"><option value="year">Academic year</option>
        <option value="ceremony">Graduation ceremony</option></select></div>

    <div class="g-f" id="wrapYear" style="flex:0 0 122px;"><label for="fYear">Academic year</label>
      <select id="fYear"></select></div>
    <div class="g-f" id="wrapCer" style="flex:0 0 210px; display:none;"><label for="fCer">Ceremony</label>
      <select id="fCer"></select></div>

    <div class="g-f" style="flex:1 1 140px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 140px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 160px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:0 0 126px;"><label for="fLevel">Award level</label><select id="fLevel"></select></div>

    <div class="g-bar__sp">
      <button type="button" class="g-btn g-btn--p" id="btnSummary">Generate summary</button>
      <button type="button" class="g-btn" id="btnXls">Export&hellip;</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

  <div class="g-chips" id="gChips"></div>
  <div id="gNotes"></div>
  <div class="g-kpis" id="gKpis"></div>

  <%-- The class of award means a different thing at each award level, so the levels come first
       and each carries its own vocabulary. --%>
  <div class="g-card">
    <div class="g-card__h">Awards and classes
      <small>certificates and diplomas are classed I&ndash;III; degrees First to Third &mdash; the two never mix</small></div>
    <div class="g-card__b" id="gLevels"><div class="g-load">Loading&hellip;</div></div>
  </div>

  <div class="g-2col">
    <div class="g-card">
      <div class="g-card__h">By faculty</div>
      <div class="g-wrap"><table class="g-tbl"><thead><tr>
        <th>Faculty</th><th class="g-num">Graduands</th><th class="g-num">Women</th>
        <th class="g-num">Mean CGPA</th><th class="g-num">Top class</th>
      </tr></thead><tbody id="gFac"></tbody></table></div>
    </div>

    <div class="g-card">
      <div class="g-card__h"><span id="gTrendH">Graduands by academic year</span>
        <small>the whole series, whatever is selected above</small></div>
      <div class="g-card__b" id="gTrend"></div>
    </div>
  </div>

  <div class="g-card" id="gSpreadCard" style="display:none;">
    <div class="g-card__h">When this ceremony&rsquo;s graduands completed
      <small>a convocation is not an academic year</small></div>
    <div class="g-card__b" id="gSpread"></div>
  </div>

  <div class="g-card">
    <div class="g-card__h">By programme <small id="gProgMeta"></small></div>
    <div class="g-wrap" style="max-height:420px;"><table class="g-tbl"><thead><tr>
      <th>Programme</th><th>Faculty</th><th>Award level</th><th class="g-num">Graduands</th>
      <th class="g-num">Women</th><th class="g-num">Men</th>
      <th class="g-num">Mean CGPA</th><th class="g-num">Top class</th>
    </tr></thead><tbody id="gProg"></tbody></table></div>
  </div>

  <div class="g-card">
    <div class="g-card__h">Documents <small>what has actually been produced for these graduands</small></div>
    <div class="g-card__b" id="gDocs"></div>
  </div>

  <div id="gNoAccess" class="g-note g-note--warn" style="display:none;"></div>
</div>

<script type="text/javascript">
// Rendered by Page_Load, so the page paints with figures rather than a spinner.
window.G_BOOT = <%= BootJson %>;
window.G_DATA = <%= DataJson %>;
</script>
<script src="js/graduation.js"></script>
<script type="text/javascript">
(function () {
'use strict';
var PAGE = 'GraduationAnalysis.aspx', BOOT = null, DATA = null;

function state() {
    var s = { lens: G.qs('fLens').value, faculty: G.qs('fFac').value, dept: G.qs('fDep').value,
              prog: G.qs('fProg').value, level: G.qs('fLevel').value };
    if (s.lens === 'ceremony') s.ceremony = G.qs('fCer').value;
    else s.year = G.qs('fYear').value;
    return s;
}
function cfg() {
    var s = state();
    return JSON.stringify({ lens: s.lens, acadYear: s.year || '', ceremony: s.ceremony || '',
                            faculty: s.faculty, department: s.dept, programme: s.prog,
                            level: s.level });
}
function sync() { G.writeUrl(state()); chips(); load(); }

function txt(id) {
    var el = G.qs(id);
    return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.replace(/\s*\(\d+\)\s*$/, '').trim() : '';
}

function chips() {
    G.chips('gChips', [
        { k: 'fFac',   label: 'Faculty',    value: G.qs('fFac').value ? txt('fFac') : '' },
        { k: 'fDep',   label: 'Department', value: G.qs('fDep').value ? txt('fDep') : '' },
        { k: 'fProg',  label: 'Programme',  value: G.qs('fProg').value ? txt('fProg') : '' },
        { k: 'fLevel', label: 'Level',      value: G.qs('fLevel').value ? txt('fLevel') : '' }
    ], function (k) {
        if (k === '*') ['fFac', 'fDep', 'fProg', 'fLevel'].forEach(function (i) { G.qs(i).value = ''; });
        else G.qs(k).value = '';
        G.cascade('fFac', 'fDep', 'fProg');
        sync();
    });
}

function lensSwap() {
    var byYear = G.qs('fLens').value === 'year';
    G.qs('wrapYear').style.display = byYear ? '' : 'none';
    G.qs('wrapCer').style.display = byYear ? 'none' : '';
}

function load() {
    G.qs('gLevels').innerHTML = '<div class="g-load">Loading&hellip;</div>';
    G.ajax(PAGE, 'Analyse', { configJson: cfg() }, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'Could not load the analysis.', false); return; }
        render(d);
    });
}

function pct(a, b) { return b > 0 ? Math.round(a * 100 / b) : 0; }

function render(d) {
    DATA = d;
    var h = d.headline, i;
    G.qs('gLens').textContent = d.lensLabel;

    G.qs('gKpis').innerHTML =
        kpi('', h.graduands, 'Graduands', d.lensLabel) +
        kpi('', h.programmes, 'Programmes', h.faculties + (h.faculties === 1 ? ' faculty' : ' faculties')) +
        kpi('ok', h.avgCgpa ? h.avgCgpa.toFixed(2) : '&ndash;', 'Mean CGPA',
            h.avgCgpa ? (h.minCgpa.toFixed(2) + ' to ' + h.maxCgpa.toFixed(2)) : 'no CGPA on record') +
        kpi('', h.womenPct + '%', 'Women', h.women + ' women, ' + h.men + ' men') +
        kpi('warn', h.top, 'Top class', h.topPct + '% took the best class their award offers');

    // ── data notes, before the figures they qualify ──
    var n = '';
    for (i = 0; i < (d.notes || []).length; i++)
        n += '<div class="g-note g-note--warn">' + G.esc(d.notes[i]) + '</div>';
    G.qs('gNotes').innerHTML = n;

    // ── award levels ──
    var lv = '';
    for (i = 0; i < (d.levels || []).length; i++) {
        var L = d.levels[i], cls = '';
        for (var j = 0; j < L.classes.length; j++) {
            var c = L.classes[j], p = pct(c.n, L.n);
            cls += '<div class="g-dist"><div class="g-dist__n' + (c.top ? ' is-top' : '') + '">' +
                   G.esc(c.name) + '</div>' +
                   '<div class="g-dist__b"><i style="width:' + p + '%"' +
                   (c.top ? ' class="is-top"' : '') + '></i></div>' +
                   '<div class="g-dist__v"><b>' + c.n + '</b> ' + p + '%</div></div>';
        }
        lv += '<div class="g-lvl"><div class="g-lvl__h"><b>' + G.esc(L.name) + '</b>' +
              '<span>' + L.n + (L.n === 1 ? ' graduand' : ' graduands') +
              (L.avgCgpa ? '  &middot;  mean CGPA ' + L.avgCgpa.toFixed(2) : '') +
              '  &middot;  ' + pct(L.women, L.n) + '% women</span></div>' + cls + '</div>';
    }
    G.qs('gLevels').innerHTML = lv || '<div class="g-empty">No graduands match this selection.</div>';

    // ── faculties ──
    var fh = '';
    for (i = 0; i < (d.faculties || []).length; i++) {
        var F = d.faculties[i];
        fh += '<tr><td>' + G.esc(F.name) + '<div class="g-sub">' + F.progs +
              (F.progs === 1 ? ' programme' : ' programmes') + '</div></td>' +
              '<td class="g-num"><b>' + F.n + '</b><div class="g-sub">' + pct(F.n, h.graduands) + '%</div></td>' +
              '<td class="g-num">' + F.women + '<div class="g-sub">' + pct(F.women, F.n) + '%</div></td>' +
              '<td class="g-num">' + (F.avgCgpa ? F.avgCgpa.toFixed(2) : '&ndash;') + '</td>' +
              '<td class="g-num">' + F.top + '<div class="g-sub">' + pct(F.top, F.n) + '%</div></td></tr>';
    }
    G.qs('gFac').innerHTML = fh || '<tr><td colspan="5" class="g-empty">Nothing to show.</td></tr>';

    // ── trend ──
    var max = 0;
    for (i = 0; i < (d.trend || []).length; i++) if (d.trend[i].n > max) max = d.trend[i].n;
    var th = '';
    for (i = 0; i < (d.trend || []).length; i++) {
        var T = d.trend[i];
        var here = (G.qs('fLens').value === 'year' && T.year === G.qs('fYear').value);
        th += '<div class="g-trend' + (here ? ' is-here' : '') + '">' +
              '<span class="g-trend__y">' + G.esc(T.year) + '</span>' +
              '<span class="g-trend__b"><i style="width:' + (max ? Math.round(T.n * 100 / max) : 0) + '%"></i></span>' +
              '<span class="g-trend__v"><b>' + T.n + '</b>' +
              (T.avgCgpa ? '<em>' + T.avgCgpa.toFixed(2) + '</em>' : '') + '</span></div>';
    }
    G.qs('gTrend').innerHTML = th || '<div class="g-empty">No year series available.</div>';

    // ── where a ceremony's graduands completed ──
    if (d.spread && d.spread.length) {
        G.qs('gSpreadCard').style.display = '';
        var smax = 0;
        for (i = 0; i < d.spread.length; i++) if (d.spread[i].n > smax) smax = d.spread[i].n;
        var sh = '<div class="g-note g-note--info">These ' + h.graduands + ' graduands completed in <b>' +
                 d.spread.length + '</b> different academic year' + (d.spread.length === 1 ? '' : 's') +
                 '. People are capped at the next ceremony after they finish, whenever that falls.</div>';
        for (i = 0; i < d.spread.length; i++)
            sh += '<div class="g-trend"><span class="g-trend__y">' + G.esc(d.spread[i].year) + '</span>' +
                  '<span class="g-trend__b"><i style="width:' +
                  (smax ? Math.round(d.spread[i].n * 100 / smax) : 0) + '%"></i></span>' +
                  '<span class="g-trend__v"><b>' + d.spread[i].n + '</b></span></div>';
        G.qs('gSpread').innerHTML = sh;
    } else G.qs('gSpreadCard').style.display = 'none';

    // ── programmes ──
    var ph = '';
    for (i = 0; i < (d.programmes || []).length; i++) {
        var P = d.programmes[i];
        ph += '<tr><td>' + G.esc(P.name) + '<div class="g-sub">' + G.esc(P.code) + '</div></td>' +
              '<td class="g-sub">' + G.esc(P.faculty) + '</td>' +
              '<td class="g-sub">' + G.esc(P.level) + '</td>' +
              '<td class="g-num"><b>' + P.n + '</b></td>' +
              '<td class="g-num">' + P.women + '</td><td class="g-num">' + P.men + '</td>' +
              '<td class="g-num">' + (P.avgCgpa ? P.avgCgpa.toFixed(2) : '&ndash;') + '</td>' +
              '<td class="g-num">' + P.top + '</td></tr>';
    }
    G.qs('gProg').innerHTML = ph || '<tr><td colspan="8" class="g-empty">Nothing to show.</td></tr>';
    G.qs('gProgMeta').textContent = (d.programmes || []).length + ' programmes, largest first';

    // ── documents ──
    var D = d.documents, tot = h.graduands;
    G.qs('gDocs').innerHTML =
        docRow('Transcripts', D.transPrinted, D.transPicked, tot) +
        docRow('Certificates', D.certPrinted, D.certPicked, tot);
}

function docRow(what, printed, picked, tot) {
    var notYet = tot - printed - picked; if (notYet < 0) notYet = 0;
    return '<div class="g-doc"><div class="g-doc__n">' + what + '</div>' +
           '<div class="g-doc__b">' +
             '<i class="is-picked" style="width:' + pct(picked, tot) + '%" title="collected"></i>' +
             '<i class="is-printed" style="width:' + pct(printed, tot) + '%" title="printed"></i>' +
           '</div>' +
           '<div class="g-doc__v"><b>' + printed + '</b> printed, <b>' + picked + '</b> collected, ' +
           '<b>' + notYet + '</b> not yet printed <span class="g-sub">of ' + tot + '</span></div></div>';
}

function kpi(kind, val, label, sub) {
    return '<div class="g-kpi' + (kind ? ' g-kpi--' + kind : '') + '">' +
           '<b>' + val + '</b><span>' + G.esc(label) + '</span><small>' + G.esc(sub) + '</small></div>';
}

/* ── the written brief ──────────────────────────────────────────────
   Composed on the server from the same queries the page drew, so a sentence and the table above
   it cannot disagree. It opens in the shared modal, can be copied whole, and goes out as the
   module's branded PDF. */
function openSummary() {
    G.qs('gModalName').textContent = 'Graduation summary';
    G.qs('gModalSub').textContent = 'Composing from the figures on screen…';
    G.showPhoto(false);
    G.qs('gModalBody').innerHTML = '<div class="g-load">Working…</div>';
    G.qs('gModalFoot').innerHTML = '';
    G.openModal();

    G.ajax(PAGE, 'Summarise', { configJson: cfg() }, function (d) {
        if (!d || !d.success) {
            G.qs('gModalBody').innerHTML = '<div class="g-note g-note--bad">' +
                G.esc((d && d.message) || 'Could not compose the summary.') + '</div>';
            return;
        }
        G.qs('gModalName').textContent = d.title;
        G.qs('gModalSub').textContent = d.subtitle + '  ·  ' + d.scopeLabel + '  ·  ' + d.generated;

        var h = '';
        for (var i = 0; i < d.paragraphs.length; i++) {
            var p = d.paragraphs[i];
            h += '<div class="g-sum"><div class="g-sum__h">' + G.esc(p.head) + '</div>';
            for (var j = 0; j < p.lines.length; j++)
                h += '<p>' + G.esc(p.lines[j]) + '</p>';
            h += '</div>';
        }
        G.qs('gModalBody').innerHTML = h;

        G.qs('gModalFoot').innerHTML =
            '<button type="button" class="g-btn g-btn--p" id="sumPdf">Download as PDF</button>' +
            '<button type="button" class="g-btn" id="sumCopy">Copy the text</button>' +
            '<span class="g-hint">Every figure here came from a query run just now against ' +
            'acad_graduands. Nothing is estimated.</span>';

        G.qs('sumPdf').addEventListener('click', function () {
            G.serverExport(PAGE, 'summary', cfg());
        });
        G.qs('sumCopy').addEventListener('click', function () {
            var t = d.title + ' — ' + d.subtitle + '\n' + d.scopeLabel + ', ' + d.generated + '\n\n';
            for (var i = 0; i < d.paragraphs.length; i++) {
                t += d.paragraphs[i].head.toUpperCase() + '\n';
                for (var j = 0; j < d.paragraphs[i].lines.length; j++)
                    t += '  ' + d.paragraphs[i].lines[j] + '\n';
                t += '\n';
            }
            copy(t);
        });
    });
}

function copy(text) {
    try {
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.select();
        document.execCommand('copy');
        document.body.removeChild(ta);
        G.toast('The summary has been copied.', true);
    } catch (e) { G.toast('Could not copy — select the text and copy it by hand.', false); }
}

function openExport() {
    G.exportDialog({
        page: PAGE,
        title: 'Export the analysis',
        subtitle: 'The tables on this page, branded and carrying the selection behind them.',
        cfg: cfg(),
        columns: [],
        pageRows: DATA && DATA.programmes ? DATA.programmes.length : 0,
        filterSummary: [
            { label: 'Population', value: DATA ? DATA.lensLabel : '' },
            { label: 'Faculty', value: G.qs('fFac').value ? txt('fFac') : 'All faculties' },
            { label: 'Programme', value: G.qs('fProg').value ? txt('fProg') : 'All programmes' },
            { label: 'Award level', value: G.qs('fLevel').value ? txt('fLevel') : 'All levels' }
        ]
    });
}

document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    G.wireModal();
    var pre = G.readUrl();

    G.qs('fLens').addEventListener('change', function () { lensSwap(); sync(); });
    ['fYear', 'fCer', 'fProg', 'fLevel'].forEach(function (id) {
        G.qs(id).addEventListener('change', sync);
    });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('btnSummary').addEventListener('click', openSummary);
    G.qs('btnXls').addEventListener('click', openExport);
    G.qs('btnReset').addEventListener('click', function () {
        ['fFac', 'fDep', 'fProg', 'fLevel'].forEach(function (id) { G.qs(id).value = ''; });
        G.qs('fLens').value = 'year';
        if (BOOT && BOOT.currentYear) G.qs('fYear').value = BOOT.currentYear;
        lensSwap(); G.cascade('fFac', 'fDep', 'fProg'); sync();
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
        var L = o.lists;
        G.fill('fYear', L.years.map(function (y) { return { v: y, t: y }; }), 'Every year');
        G.fill('fCer', L.ceremonies, 'Every ceremony');
        G.fill('fFac', L.faculties, 'All faculties');
        G.fill('fDep', L.departments, 'All departments');
        G.fill('fProg', L.programmes, 'All programmes');
        G.fill('fLevel', L.levels, 'All levels');

        G.qs('fLens').value = pre.lens || 'year';
        G.qs('fYear').value = pre.year || o.currentYear || '';
        if (pre.ceremony) G.qs('fCer').value = pre.ceremony;
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        if (pre.level) G.qs('fLevel').value = pre.level;

        lensSwap();
        G.cascade('fFac', 'fDep', 'fProg');
        G.combo('fProg', 'Type a code or part of the name…');
        G.combo('fCer', 'Type a ceremony…');
        chips();

        // The opening figures came with the page; render them without a round trip.
        var fresh = (!pre.lens || pre.lens === 'year') &&
                    (!pre.year || pre.year === o.currentYear) &&
                    !pre.faculty && !pre.dept && !pre.prog && !pre.level;
        if (fresh && window.G_DATA && window.G_DATA.success) render(window.G_DATA);
        else load();
    }

    if (window.G_BOOT) applyBoot(window.G_BOOT);
    else G.ajax(PAGE, 'Analyse', { configJson: cfg() }, function () { });
});
})();
</script>
</asp:Content>
