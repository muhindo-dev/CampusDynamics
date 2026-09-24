<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationList.aspx.cs" Inherits="COOPERP_NewScreens_GraduationList" Title="Graduation List - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Graduation list</b><span id="gScope">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 122px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 170px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:1 1 140px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number or name&hellip;" autocomplete="off" /></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnPrint">Print for Senate</button>
      <button type="button" class="g-btn" id="btnXls">Export</button>
      <button type="button" class="g-btn" id="btnCsv">CSV</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

  <div class="g-card">
    <div class="g-card__h"><span id="gTitle">On the list</span><small id="gMeta">&nbsp;</small></div>
    <div class="g-wrap">
      <table class="g-tbl"><thead><tr>
        <th>Student</th><th>Programme</th><th class="g-num">CGPA</th><th>Class of award</th>
        <th>Cleared by</th><th>Transcript</th><th></th>
      </tr></thead><tbody id="gBody"></tbody></table>
    </div>
  </div>

  <div id="gNoAccess" class="g-note g-note--warn" style="display:none;"></div>
</div>

<script src="js/graduation.js"></script>
<script type="text/javascript">
(function () {
'use strict';
var PAGE = 'GraduationList.aspx', boot = null, rows = [];

function state() {
    return { year: G.qs('fYear').value, faculty: G.qs('fFac').value, dept: G.qs('fDep').value,
             prog: G.qs('fProg').value, q: G.qs('fQ').value.trim() };
}
function cfg() {
    var s = state();
    return JSON.stringify({ acadYear: s.year, faculty: s.faculty, department: s.dept,
                            programme: s.prog, search: s.q });
}
function sync() { G.writeUrl(state()); load(); }

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
    });
}

/* Senate print: grouped and numbered by programme, which is how a graduation list is read out
   and signed. Built from the rows on screen, so what prints is what was reviewed. */
function doPrint() {
    if (!rows.length) { G.toast('There is nothing on this list to print.', false); return; }
    var w = window.open('', '_blank');
    if (!w) { G.toast('Pop-up blocked — allow pop-ups to open the print view.', false); return; }

    var groups = {}, order = [], i;
    for (i = 0; i < rows.length; i++) {
        var k = rows[i].progname || rows[i].progcode;
        if (!groups[k]) { groups[k] = []; order.push(k); }
        groups[k].push(rows[i]);
    }
    order.sort();

    var year = G.qs('fYear').value || 'all years';
    var h = '<!doctype html><html><head><meta charset="utf-8"><title>Graduation list ' + G.esc(year) + '</title><style>' +
        'body{font-family:"Segoe UI",Arial,sans-serif;color:#111;margin:24px;font-size:11pt;}' +
        '.hd{border-bottom:2.5px solid #05275C;padding-bottom:8px;margin-bottom:16px;}' +
        'h1{font-size:15pt;margin:0;color:#05275C;letter-spacing:.2px;}' +
        '.sub{font-size:9.5pt;color:#555;margin-top:3px;}' +
        'h2{font-size:11pt;margin:18px 0 5px;color:#05275C;border-bottom:1px solid #05275C;padding-bottom:3px;}' +
        'table{width:100%;border-collapse:collapse;font-size:9.5pt;}' +
        'th{text-align:left;border-bottom:1px solid #999;padding:4px 5px;font-size:8pt;text-transform:uppercase;' +
        'letter-spacing:.3px;color:#444;}' +
        'td{padding:4px 5px;border-bottom:1px solid #eee;}.n{text-align:right;}' +
        '.foot{margin-top:24px;font-size:8.5pt;color:#555;border-top:1px solid #999;padding-top:7px;}' +
        '.sig{margin-top:30px;display:flex;gap:44px;font-size:9pt;}' +
        '.sig div{flex:1;border-top:1px solid #333;padding-top:5px;}' +
        '@media print{h2{page-break-after:avoid;}tr{page-break-inside:avoid;}}' +
        '</style></head><body>';
    h += '<div class="hd"><h1>Muteesa I Royal University</h1>' +
         '<div class="sub">Graduation list for ' + G.esc(year) + ' &middot; ' + rows.length +
         ' candidate' + (rows.length === 1 ? '' : 's') + ' &middot; prepared ' +
         new Date().toLocaleDateString() + '</div></div>';

    for (var gi = 0; gi < order.length; gi++) {
        var list = groups[order[gi]];
        h += '<h2>' + G.esc(order[gi]) + ' <span style="font-weight:400;font-size:8.5pt;color:#666;">(' +
             list.length + ')</span></h2><table><thead><tr><th style="width:24px;">#</th>' +
             '<th>Student number</th><th>Name</th><th class="n">CGPA</th><th>Class of award</th>' +
             '</tr></thead><tbody>';
        for (i = 0; i < list.length; i++)
            h += '<tr><td class="n">' + (i + 1) + '</td><td>' + G.esc(list[i].regno) + '</td><td>' +
                 G.esc(list[i].name) + '</td><td class="n">' + G.n2(list[i].cgpa) + '</td><td>' +
                 G.esc(list[i].degclass) + '</td></tr>';
        h += '</tbody></table>';
    }

    h += '<div class="foot">Prepared from the Graduation Centre. Every name on this list was cleared by a ' +
         'named reviewer against the results on record at the time of clearing; the evidence behind each ' +
         'decision is retained and can be produced on request.</div>' +
         '<div class="sig"><div>Academic Registrar</div><div>Chairperson, Senate</div></div></body></html>';
    w.document.open(); w.document.write(h); w.document.close();
    setTimeout(function () { try { w.focus(); w.print(); } catch (e) {} }, 350);
}

function footer(g) {
    return '<button type="button" class="g-btn g-btn--d" id="mRemove">Take off the graduation list</button>' +
           '<span class="g-sub" style="margin-left:auto;">The decision history is kept either way.</span>';
}
function open(reg) {
    G.openStudent(PAGE, reg, footer);
    setTimeout(function () {
        var b = G.qs('mRemove');
        if (b) b.addEventListener('click', doRemove);
    }, 0);
}
function doRemove() {
    var cur = G.currentStudent(); if (!cur) return;
    var g = cur.student;
    var reason = prompt('Take ' + g.name + ' OFF the ' + g.graduatedYear + ' graduation list?\n\n' +
        'The decision history is kept. Say why (at least 10 characters):', '');
    if (reason === null) return;
    if (reason.trim().length < 10) { G.toast('A reason of at least 10 characters is needed.', false); return; }
    G.ajax(PAGE, 'RemoveStudent', { regno: g.regno, reason: reason }, function (d) {
        if (d && d.success) { G.toast(d.message, true); G.closeModal(); load(); }
        else G.toast((d && d.message) || 'Could not remove that name.', false);
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
    G.qs('btnPrint').addEventListener('click', doPrint);
    G.qs('btnXls').addEventListener('click', function () { G.serverExport(PAGE, 'xls', cfg()); });
    G.qs('btnCsv').addEventListener('click', function () { G.serverExport(PAGE, 'csv', cfg()); });
    G.qs('btnReset').addEventListener('click', function () {
        ['fFac', 'fDep', 'fProg'].forEach(function (id) { G.qs(id).value = ''; });
        G.qs('fQ').value = '';
        if (boot && boot.currentYear) G.qs('fYear').value = boot.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); sync();
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
        G.fill('fYear', o.years.map(function (y) { return { v: y, t: y }; }), 'All years');
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
    });
});
})();
</script>
</asp:Content>
