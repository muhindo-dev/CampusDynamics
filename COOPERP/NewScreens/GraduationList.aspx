<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationList.aspx.cs" Inherits="COOPERP_NewScreens_GraduationList" Title="Graduation List - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Graduation list</b><span id="gScope">&nbsp;</span>
      <span class="g-age" id="gAge">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 122px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 170px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:0 0 132px;"><label for="fAward">Class of award</label>
      <select id="fAward"><option value="">Every class</option></select></div>
    <div class="g-f" style="flex:0 0 118px;"><label for="fSort">Order</label>
      <select id="fSort"><option value="prog">Programme, then name</option><option value="cgpa">Highest CGPA</option>
        <option value="class">Class of award</option><option value="name">Name</option>
        <option value="regno">Student number</option></select></div>
    <div class="g-f" style="flex:1 1 140px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number or name&hellip;" autocomplete="off" /></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnPrint">Print for Senate</button>
      <button type="button" class="g-btn" id="btnXls">Export&hellip;</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

  <div class="g-chips" id="gChips"></div>

  <div class="g-card">
    <div class="g-card__h"><span id="gTitle">On the list</span><small id="gMeta">&nbsp;</small></div>
    <div class="g-wrap">
      <table class="g-tbl"><thead><tr>
        <th>Student</th><th>Programme</th><th class="g-num">CGPA</th><th>Class of award</th>
        <th>Approved by</th><th>Transcript</th><th></th>
      </tr></thead><tbody id="gBody"></tbody></table>
    </div>
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
var PAGE = 'GraduationList.aspx', BOOT = null, rows = [];

function state() {
    return { year: G.qs('fYear').value, faculty: G.qs('fFac').value, dept: G.qs('fDep').value,
             prog: G.qs('fProg').value, award: G.qs('fAward').value,
             sort: G.qs('fSort').value === 'prog' ? '' : G.qs('fSort').value,
             q: G.qs('fQ').value.trim() };
}
function cfg() {
    var s = state();
    return JSON.stringify({ acadYear: s.year, faculty: s.faculty, department: s.dept,
                            programme: s.prog, award: s.award, sort: G.qs('fSort').value,
                            search: s.q });
}
function sync() { G.writeUrl(state()); chips(); load(); }

function txt(id) {
    var el = G.qs(id);
    return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.replace(/\s*\(\d+\)\s*$/, '')
                                          .replace(/\s*\(none\)\s*$/, '').trim() : '';
}

function chips() {
    G.chips('gChips', [
        { k: 'fFac',   label: 'Faculty',    value: G.qs('fFac').value ? txt('fFac') : '' },
        { k: 'fDep',   label: 'Department', value: G.qs('fDep').value ? txt('fDep') : '' },
        { k: 'fProg',  label: 'Programme',  value: G.qs('fProg').value ? txt('fProg') : '' },
        { k: 'fAward', label: 'Class',      value: G.qs('fAward').value },
        { k: 'fQ',     label: 'Search',     value: G.qs('fQ').value.trim() }
    ], function (k) {
        if (k === '*') resetFilters(); else G.qs(k).value = '';
        G.cascade('fFac', 'fDep', 'fProg');
        sync();
    });
}

function resetFilters() {
    ['fFac', 'fDep', 'fProg', 'fAward'].forEach(function (id) { G.qs(id).value = ''; });
    G.qs('fQ').value = '';
}

/* The classes actually present, taken from the rows rather than assumed. A hard-coded list
   would quietly stop matching the day the grading scale is amended. */
function fillAwards() {
    var sel = G.qs('fAward'), cur = sel.value, seen = {}, list = [], i;
    for (i = 0; i < rows.length; i++) {
        var c = (rows[i].degclass || '').trim();
        if (c && !seen[c]) { seen[c] = 1; list.push(c); }
    }
    list.sort();
    var h = '<option value="">Every class</option>';
    for (i = 0; i < list.length; i++)
        h += '<option value="' + G.esc(list[i]) + '">' + G.esc(list[i]) + '</option>';
    sel.innerHTML = h;
    if (cur) sel.value = cur;
}

/* ── export ── */
function openExport() {
    var sum = [
        { label: 'Graduation year', value: G.qs('fYear').value || 'All years' },
        { label: 'Faculty', value: G.qs('fFac').value ? txt('fFac') : 'All faculties' },
        { label: 'Department', value: G.qs('fDep').value ? txt('fDep') : 'All departments' },
        { label: 'Programme', value: G.qs('fProg').value ? txt('fProg') : 'All programmes' },
        { label: 'Order', value: txt('fSort') }
    ];
    if (G.qs('fAward').value) sum.push({ label: 'Class of award', value: G.qs('fAward').value });
    if (G.qs('fQ').value.trim()) sum.push({ label: 'Search', value: G.qs('fQ').value.trim() });

    G.exportDialog({
        page: PAGE,
        title: 'Export the graduation list',
        subtitle: 'Numbered within programme, the way the list is read out and signed off.',
        cfg: cfg(),
        columns: window.G_COLS || [],
        pageRows: rows.length,
        countMethod: 'CountExport',
        filters: {
            focus: false,
            years: (BOOT && BOOT.years) || [],
            faculties: (BOOT && BOOT.faculties) || [],
            departments: (BOOT && BOOT.departments) || [],
            programmes: (BOOT && BOOT.programmes) || [],
            current: {
                acadYear: G.qs('fYear').value, faculty: G.qs('fFac').value,
                department: G.qs('fDep').value, programme: G.qs('fProg').value
            }
        },
        filterSummary: sum,
        sorts: [
            { k: 'name', t: 'Name' },
            { k: 'regno', t: 'Student number' },
            { k: 'cgpa', t: 'Performance, highest CGPA first' },
            { k: 'class', t: 'Class of award' },
            { k: 'prog', t: 'Programme, then name' }
        ],
        sortDefault: 'name',
        groups: [
            { k: 'prog', t: 'Programme' },
            { k: 'fac', t: 'Faculty' },
            { k: '', t: 'One continuous list' }
        ],
        groupDefault: 'prog',
        sheets: [
            { k: 'byprog', t: 'Summary by programme', d: 'How a Senate paper opens', on: true },
            { k: 'byclass', t: 'By class of award', d: 'The distribution table that always gets asked for', on: true },
            { k: 'bywho', t: 'Who approved whom', d: 'Names approved, by reviewer', on: false }
        ]
    });
}

function load() {
    G.qs('gBody').innerHTML = '<tr><td colspan="7" class="g-load">Loading&hellip;</td></tr>';
    G.ajax(PAGE, 'GetList', { configJson: cfg() }, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'Could not load the list.', false); return; }
        rows = d.rows || [];
        var h = '';
        for (var i = 0; i < rows.length; i++) {
            var r = rows[i];
            h += '<tr class="is-click" data-reg="' + G.esc(r.regno) + '">' +
                '<td>' + G.photoCell(r.regno, r.name) + '</td>' +
                '<td>' + G.esc(r.progname || r.progcode) + '<div class="g-sub">' + G.esc(r.progcode) + '</div></td>' +
                '<td class="g-num">' + G.n2(r.cgpa) + '</td>' +
                '<td>' + G.esc(r.degclass) + '</td>' +
                '<td>' + (r.clearedBy ? G.esc(r.clearedBy) + '<div class="g-sub">' + G.esc(r.clearedAt) + '</div>'
                                      : '<span class="g-sub">before this module</span>') + '</td>' +
                '<td class="g-sub">' + G.esc(r.transStatus || '&ndash;') + '</td>' +
                '<td><button type="button" class="g-btn g-btn--sm" data-open="' + G.esc(r.regno) + '">Open</button></td></tr>';
        }
        G.qs('gBody').innerHTML = h || '<tr><td colspan="7" class="g-empty">Nobody is on this list yet.</td></tr>';
        G.qs('gMeta').textContent = rows.length + ' on the ' + (G.qs('fYear').value || 'combined') + ' list';
        fillAwards();

        // No decisions to advance through here, but the walker still lets a reviewer step from
        // one name to the next without going back to the table.
        var ids = [];
        for (var q = 0; q < rows.length; q++) ids.push(rows[q].regno);
        G.queue({ ids: ids, total: rows.length, page: 1, pages: 1, size: rows.length });
        G.onWalk(open);
    });
}

/* Senate print: a faculty summary first, then the names grouped by faculty and numbered by
   programme, which is how a graduation list is read out and signed. Built from the rows on
   screen, so what prints is what was reviewed. */
function doPrint() {
    if (!rows.length) { G.toast('There is nothing on this list to print.', false); return; }
    var w = window.open('', '_blank');
    if (!w) { G.toast('Pop-up blocked: allow pop-ups to open the print view.', false); return; }

    var facName = {}, fl = (BOOT && BOOT.faculties) || [], i;
    for (i = 0; i < fl.length; i++) facName[(fl[i].v || '').replace(/^\s+|\s+$/g, '')] = fl[i].t;
    var NOFAC = 'Faculty not recorded';
    function fac(r) { var c = (r.faculty || '').replace(/^\s+|\s+$/g, ''); return c ? (facName[c] || ('Faculty ' + c)) : NOFAC; }
    function top(r) { return /first|distinction/i.test(r.degclass || ''); }

    // faculty -> programme -> rows
    var F = {}, fOrder = [];
    for (i = 0; i < rows.length; i++) {
        var r = rows[i], fk = fac(r), pk = r.progname || r.progcode;
        if (!F[fk]) { F[fk] = { progs: {}, order: [], n: 0, top: 0 }; fOrder.push(fk); }
        var f = F[fk];
        if (!f.progs[pk]) { f.progs[pk] = []; f.order.push(pk); }
        f.progs[pk].push(r); f.n++; if (top(r)) f.top++;
    }
    fOrder.sort(function (a, b) { return a === NOFAC ? 1 : b === NOFAC ? -1 : a.localeCompare(b); });

    var total = rows.length, totalTop = 0, totalProgs = 0;
    for (i = 0; i < fOrder.length; i++) { totalTop += F[fOrder[i]].top; totalProgs += F[fOrder[i]].order.length; }

    var year = G.qs('fYear').value || 'All years';
    var scope = [];
    if (G.qs('fFac').value) scope.push(txt('fFac'));
    if (G.qs('fDep').value) scope.push(txt('fDep'));
    if (G.qs('fProg').value) scope.push(txt('fProg'));
    var crest = new URL('../images/mru-crest.png', window.location.href).href;
    var today = new Date().toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric' });
    function pct(n) { return total ? (Math.round(n * 1000 / total) / 10) + '%' : '0%'; }
    function plural(n, one, many) { return n + ' ' + (n === 1 ? one : many); }

    var h = '<!doctype html><html><head><meta charset="utf-8"><title>Graduation List ' + G.esc(year) + ' - Muteesa I Royal University</title><style>' +
        '*{box-sizing:border-box;-webkit-print-color-adjust:exact;print-color-adjust:exact;}' +
        'body{font-family:"Segoe UI",Arial,sans-serif;color:#1a1a2e;margin:0;font-size:10pt;}' +
        '.doc{max-width:900px;margin:0 auto;padding:22px 26px;}' +
        '.lh{display:flex;align-items:center;gap:16px;padding-bottom:10px;border-bottom:3px solid #05275C;}' +
        '.lh img{width:76px;height:76px;object-fit:contain;flex:0 0 auto;}' +
        '.lh__t{flex:1;text-align:center;}' +
        '.lh__n{font-size:19pt;font-weight:700;color:#05275C;letter-spacing:1px;}' +
        '.lh__o{font-size:10.5pt;color:#174DA4;font-weight:600;margin-top:2px;letter-spacing:.3px;}' +
        '.lh__a{font-size:8.5pt;color:#666;margin-top:3px;}' +
        '.lh__sp{width:76px;flex:0 0 auto;}' +
        '.rule{height:1px;background:#174DA4;margin-top:2px;}' +
        '.band{margin:14px 0 4px;background:#05275C;color:#fff;text-align:center;padding:8px 12px;font-size:13pt;font-weight:700;letter-spacing:1.5px;}' +
        '.meta{text-align:center;font-size:9pt;color:#555;margin-bottom:16px;}' +
        '.cap{font-size:10pt;font-weight:700;color:#05275C;margin:4px 0 6px;text-transform:uppercase;letter-spacing:.5px;}' +
        '.sum{width:100%;border-collapse:collapse;margin-bottom:6px;font-size:9.5pt;}' +
        '.sum th{background:#174DA4;color:#fff;text-align:left;padding:6px 8px;font-size:8pt;text-transform:uppercase;letter-spacing:.4px;}' +
        '.sum td{padding:6px 8px;border-bottom:1px solid #e0e5ed;}' +
        '.sum tr:nth-child(even) td{background:#f5f7fa;}' +
        '.sum .tot td{background:#e8eef8 !important;font-weight:700;color:#05275C;border-top:2px solid #05275C;}' +
        '.n{text-align:right;white-space:nowrap;}' +
        'h2{font-size:11pt;margin:22px 0 0;background:#e8eef8;color:#05275C;padding:6px 10px;border-left:4px solid #05275C;}' +
        'h2 span,h3 span{font-weight:400;font-size:8.5pt;color:#555;}' +
        'h3{font-size:10pt;margin:12px 0 4px;color:#174DA4;border-bottom:1px solid #174DA4;padding-bottom:3px;}' +
        '.lst{width:100%;border-collapse:collapse;font-size:9.5pt;}' +
        '.lst th{text-align:left;border-bottom:1px solid #999;padding:4px 6px;font-size:7.5pt;text-transform:uppercase;letter-spacing:.3px;color:#444;}' +
        '.lst td{padding:5px 6px;border-bottom:1px solid #e0e5ed;}' +
        '.lst tbody tr:nth-child(even) td{background:#f0f4fa;}' +
        '.foot{margin-top:26px;font-size:8.5pt;color:#555;border-top:1px solid #999;padding-top:7px;line-height:1.5;}' +
        '.sig{margin-top:40px;display:flex;gap:44px;font-size:9pt;}' +
        '.sig div{flex:1;border-top:1px solid #333;padding-top:5px;}' +
        '.sig small{display:block;color:#777;margin-top:16px;}' +
        '.brand{margin-top:18px;text-align:center;font-size:7.5pt;color:#888;letter-spacing:.4px;border-top:3px solid #05275C;padding-top:6px;}' +
        '@media print{.doc{padding:0;}h2,h3{page-break-after:avoid;}tr{page-break-inside:avoid;}}' +
        '@page{margin:12mm 12mm 14mm;}' +
        '</style></head><body><div class="doc">';

    // Letterhead
    h += '<div class="lh"><img id="crest" src="' + crest + '" alt="Muteesa I Royal University crest" />' +
         '<div class="lh__t"><div class="lh__n">MUTEESA I ROYAL UNIVERSITY</div>' +
         '<div class="lh__o">Office of the Academic Registrar</div>' +
         '<div class="lh__a">www.mru.ac.ug</div></div><div class="lh__sp"></div></div><div class="rule"></div>' +
         '<div class="band">GRADUATION LIST &middot; ' + G.esc(year.toUpperCase()) + '</div>' +
         '<div class="meta">' + (scope.length ? G.esc(scope.join(' / ')) + ' &middot; ' : '') +
         plural(total, 'graduand', 'graduands') + ' &middot; ' + plural(fOrder.length, 'faculty', 'faculties') + ' &middot; ' +
         plural(totalProgs, 'programme', 'programmes') + ' &middot; prepared ' + today + '</div>';

    // Summary of graduands by faculty
    h += '<div class="cap">Summary of graduands by faculty</div><table class="sum"><thead><tr>' +
         '<th style="width:28px;">#</th><th>Faculty</th><th class="n">Programmes</th><th class="n">Graduands</th>' +
         '<th class="n">First Class / Distinction</th><th class="n">Share of list</th></tr></thead><tbody>';
    for (i = 0; i < fOrder.length; i++) {
        var fi = F[fOrder[i]];
        h += '<tr><td>' + (i + 1) + '</td><td>' + G.esc(fOrder[i]) + '</td><td class="n">' + fi.order.length + '</td><td class="n"><strong>' + fi.n +
             '</strong></td><td class="n">' + fi.top + '</td><td class="n">' + pct(fi.n) + '</td></tr>';
    }
    h += '<tr class="tot"><td></td><td>Total</td><td class="n">' + totalProgs + '</td><td class="n">' + total + '</td><td class="n">' + totalTop +
         '</td><td class="n">100%</td></tr></tbody></table>';

    // Names: faculty, then programme
    for (var a = 0; a < fOrder.length; a++) {
        var ff = F[fOrder[a]];
        ff.order.sort();
        h += '<h2>' + G.esc(fOrder[a]) + ' <span>(' + plural(ff.n, 'graduand', 'graduands') + ')</span></h2>';
        for (var b = 0; b < ff.order.length; b++) {
            var list = ff.progs[ff.order[b]];
            h += '<h3>' + G.esc(ff.order[b]) + ' <span>(' + list.length + ')</span></h3><table class="lst"><thead><tr>' +
                 '<th style="width:28px;">#</th><th style="width:150px;">Student number</th><th>Name</th>' +
                 '<th style="width:220px;">Class of award</th></tr></thead><tbody>';
            for (i = 0; i < list.length; i++)
                h += '<tr><td class="n">' + (i + 1) + '</td><td>' + G.esc(list[i].regno) + '</td><td>' + G.esc(list[i].name) +
                     '</td><td>' + G.esc(list[i].degclass) + '</td></tr>';
            h += '</tbody></table>';
        }
    }

    h += '<div class="foot">Prepared from the Graduation Centre on ' + today + '. Every name on this list was approved by a ' +
         'named reviewer against the results on record at the time of approval; the evidence behind each ' +
         'decision is retained and can be produced on request.</div>' +
         '<div class="sig"><div>Academic Registrar<small>Signature &amp; date</small></div><div>Chairperson, Senate<small>Signature &amp; date</small></div></div>' +
         '<div class="brand">MUTEESA I ROYAL UNIVERSITY &middot; GRADUATION LIST ' + G.esc(year.toUpperCase()) + ' &middot; CONFIDENTIAL</div>' +
         '</div></body></html>';
    w.document.open(); w.document.write(h); w.document.close();

    // Print once the crest has loaded (or after a short wait if it cannot)
    var printed = false;
    function go() { if (printed) return; printed = true; try { w.focus(); w.print(); } catch (e) {} }
    var img = w.document.getElementById('crest');
    if (img && !img.complete) { img.onload = go; img.onerror = go; setTimeout(go, 2500); } else setTimeout(go, 350);
}

function footer(g) {
    return '<button type="button" class="g-btn g-btn--d" id="mRemove">Take off the graduation list</button>' +
           '<span class="g-sub" style="margin-left:auto;">The decision history is kept either way.</span>';
}
function open(reg) {
    G.openStudent(PAGE, reg, footer, function () {
        var b = G.qs('mRemove');
        if (b) b.addEventListener('click', doRemove);
    });
}
function doRemove() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    // Taking a name OFF a graduation list is the most consequential thing this page can do, so
    // it goes through the same reason dialog as every other decision rather than a prompt box.
    G.reasonDialog({
        page: PAGE, regno: g.regno,
        title: 'Take ' + g.name + ' off the list',
        subtitle: g.regno + '  ·  ' + (g.progname || g.progcode),
        warn: 'This removes them from the ' + g.graduatedYear +
              ' graduation list. The decision history is kept either way.',
        verb: 'Take off the list',
        onSubmit: function (reason) {
            G.ajax(PAGE, 'RemoveStudent', { regno: g.regno, reason: reason }, function (d) {
                if (d && d.success) { G.toast(d.message, true); G.closeModal(); load(); }
                else G.toast((d && d.message) || 'Could not remove that name.', false);
            });
        }
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
    ['fYear', 'fProg', 'fAward', 'fSort'].forEach(function (id) {
        G.qs(id).addEventListener('change', sync);
    });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); sync(); });
    G.qs('fQ').addEventListener('input', G.debounce(sync, 350));
    G.qs('fQ').addEventListener('keydown', function (e) { if (e.key === 'Enter') sync(); });
    G.qs('btnPrint').addEventListener('click', doPrint);
    G.qs('btnXls').addEventListener('click', openExport);
    G.qs('btnReset').addEventListener('click', function () {
        resetFilters();
        G.qs('fSort').value = 'prog';
        if (BOOT && BOOT.currentYear) G.qs('fYear').value = BOOT.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); sync();
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
        G.fill('fYear', o.years.map(function (y) { return { v: y, t: y }; }), 'All years');
        G.fill('fFac', o.faculties, 'All faculties');
        G.fill('fDep', o.departments, 'All departments');
        G.fill('fProg', o.programmes, 'All programmes');
        G.qs('fYear').value = pre.year || o.currentYear || '';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        if (pre.q) G.qs('fQ').value = pre.q;
        if (pre.sort) G.qs('fSort').value = pre.sort;
        // The class list is built from the rows, so a pre-selected one has to be planted first.
        if (pre.award) {
            var o2 = document.createElement('option');
            o2.value = pre.award; o2.text = pre.award;
            G.qs('fAward').appendChild(o2);
            G.qs('fAward').value = pre.award;
        }
        G.cascade('fFac', 'fDep', 'fProg');
        G.combo('fProg', 'Type a code or part of the name\u2026', { key: true });
        chips();
        load();
    }

    if (window.G_BOOT) applyBoot(window.G_BOOT);
    else G.ajax(PAGE, 'GetBootstrap', {}, applyBoot);
});
})();
</script>
</asp:Content>
