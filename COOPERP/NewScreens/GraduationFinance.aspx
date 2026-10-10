<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationFinance.aspx.cs" Inherits="COOPERP_NewScreens_GraduationFinance" Title="Fees Clearance - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
<style>
/* ── Fees Clearance additions, on top of the shared graduation styles ── */
.gf-money{font-variant-numeric:tabular-nums;white-space:nowrap;font-weight:700;}
.gf-owe{color:var(--bad);} .gf-cr{color:var(--ok);} .gf-zero{color:var(--mute);}
.gf-flags{display:flex;flex-wrap:wrap;gap:3px;}
.gf-flag{display:inline-block;font-size:9.5px;font-weight:700;padding:1px 6px;border-radius:9px;white-space:nowrap;}
.gf-flag--BLOCK{background:var(--bad-bg);color:var(--bad);}
.gf-flag--WARN{background:var(--warn-bg);color:var(--warn);}
.gf-tabs{display:flex;gap:2px;border-bottom:1px solid var(--line);margin:0 0 9px;flex-wrap:wrap;}
.gf-tab{background:none;border:0;border-bottom:2px solid transparent;padding:7px 12px;font-size:12px;font-weight:700;
        color:var(--ink2);cursor:pointer;font-family:inherit;}
.gf-tab:hover{color:var(--accent);}
.gf-tab.is-on{color:var(--navy);border-bottom-color:var(--accent);}
.gf-tab em{font-style:normal;font-weight:600;color:var(--mute);margin-left:4px;}
.gf-pane{display:none;} .gf-pane.is-on{display:block;}
.gf-hero{display:flex;align-items:stretch;gap:8px;margin-bottom:9px;flex-wrap:wrap;}
.gf-big{flex:1 1 170px;border:1px solid var(--line);border-radius:4px;padding:9px 12px;background:#fff;}
.gf-big span{display:block;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;color:var(--mute);}
.gf-big b{display:block;font-size:21px;margin-top:2px;line-height:1.1;}
.gf-big small{display:block;font-size:10.5px;color:var(--ink2);margin-top:3px;}
.gf-big--status{flex:1 1 230px;}
.gf-form{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:9px 12px;max-width:820px;}
.gf-form label{display:block;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:var(--mute);margin-bottom:3px;}
.gf-form input,.gf-form select,.gf-form textarea{width:100%;box-sizing:border-box;border:1px solid var(--line);border-radius:3px;
        padding:6px 8px;font-size:12.5px;font-family:inherit;background:#fff;}
.gf-form .gf-wide{grid-column:1/-1;}
.gf-act{display:flex;gap:7px;align-items:center;margin-top:11px;flex-wrap:wrap;}
.gf-quick{border:1px dashed var(--accent);background:#f5f8ff;border-radius:4px;padding:9px 12px;margin-bottom:12px;
        display:flex;align-items:center;gap:10px;flex-wrap:wrap;}
.gf-quick b{color:var(--navy);}
.gf-tbl td,.gf-tbl th{font-size:11.5px;}
.gf-tbl td.gf-money{text-align:right;}
.gf-tbl tr.is-miss td{background:#fffafa;}
.gf-hist{border-left:3px solid var(--line);padding:5px 9px;margin-bottom:6px;font-size:11.5px;}
.gf-hist--CLEARED{border-left-color:var(--ok);} .gf-hist--HELD{border-left-color:var(--held);}
.gf-hist--REVOKED{border-left-color:var(--bad);} .gf-hist.is-old{opacity:.65;}
.gf-hist b{color:var(--navy);}
.gf-links{display:flex;gap:7px;flex-wrap:wrap;margin-top:8px;}
.gf-links a{font-size:11.5px;font-weight:700;color:var(--accent);text-decoration:none;border:1px solid var(--line);padding:4px 9px;border-radius:3px;background:#fff;}
.gf-links a:hover{border-color:var(--accent);}
.g-modal__f .gf-sp{margin-left:auto;}
@media(max-width:700px){.gf-big b{font-size:17px;}}
</style>
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="g">

  <div class="g-bar" id="gToolbar">
    <div class="g-bar__id"><b>Fees clearance</b><span id="gScope">&nbsp;</span></div>
    <div class="g-f" style="flex:0 0 122px;"><label for="fYear">Graduation year</label><select id="fYear"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fFac">Faculty</label><select id="fFac"></select></div>
    <div class="g-f" style="flex:1 1 150px;"><label for="fDep">Department</label><select id="fDep"></select></div>
    <div class="g-f" style="flex:1 1 170px;"><label for="fProg">Programme</label><select id="fProg"></select></div>
    <div class="g-f" style="flex:1 1 140px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number or name&hellip;" autocomplete="off" /></div>
    <div class="g-f" style="flex:0 0 170px;"><label for="fStatus">Show</label>
      <select id="fStatus">
        <option value="">Everyone on the list</option>
        <option value="pending">Not yet decided</option>
        <option value="ready">Ready to clear</option>
        <option value="blocked">Something outstanding</option>
        <option value="owing">Owing money</option>
        <option value="nofee">Graduation Fee not billed</option>
        <option value="cleared">Cleared</option>
        <option value="drift">Cleared but owing again</option>
        <option value="held">On finance hold</option>
      </select></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnXls">Export&hellip;</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

  <div class="g-chips" id="gChips"></div>
  <div class="g-kpis" id="gKpis"></div>
  <div id="gGateNote"></div>
  <div class="g-batch" id="gBatch" style="display:none;"></div>

  <div class="g-card">
    <div class="g-card__h"><span>Graduands</span><small id="gMeta">&nbsp;</small></div>
    <div class="g-wrap">
      <table class="g-tbl"><thead><tr>
        <th style="width:24px;"><input type="checkbox" id="ckAll" title="Select every student on this page" /></th>
        <th>Student</th><th>Programme</th><th class="g-num">Balance (UGX)</th><th>Graduation fee</th>
        <th>Outstanding</th><th>Finance</th><th></th>
      </tr></thead><tbody id="gBody"></tbody></table>
    </div>
    <div class="g-pager" id="gPager"></div>
  </div>

  <div class="g-note g-note--info" style="margin-top:8px;">
    Only students the Academic Registry has put on a graduation list appear here. Balances are live
    (billed minus paid, the figure every finance screen uses). A student who owes nothing can be cleared
    at once; clearing anyone with money outstanding is an override, recorded against your name with a reason.
  </div>
  <div id="gNoAccess" class="g-note g-note--warn" style="display:none;"></div>
</div>

<script type="text/javascript">
window.G_BOOT = <%= BootJson %>;
window.G_COLS = <%= ColsJson %>;
window.G_AGE  = '';
</script>
<script src="js/graduation.js"></script>
<script type="text/javascript">
(function () {
'use strict';
var PAGE = 'GraduationFinance.aspx', BOOT = null, rows = [], page = 1, total = 0, picked = {};
var CUR = null, TAB = 'overview', dirty = false;

function money(v) {
    v = Math.round(+v || 0);
    var s = Math.abs(v).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return (v < 0 ? '-' : '') + s;
}
function balCell(v) {
    v = +v || 0;
    var cls = v > 0 ? 'gf-owe' : (v < 0 ? 'gf-cr' : 'gf-zero');
    return '<span class="gf-money ' + cls + '">' + (v > 0 ? money(v) : v < 0 ? money(-v) + ' CR' : '0') + '</span>';
}
function chipFor(r) {
    if (r.status === 'CLEARED')
        return r.owesSinceClearance ? '<span class="g-chip g-chip--warn">Cleared · owes again</span>'
                                    : '<span class="g-chip g-chip--ready">Cleared</span>';
    if (r.status === 'HELD') return '<span class="g-chip g-chip--held">Finance hold</span>';
    return r.eligible ? '<span class="g-chip g-chip--listed">Ready to clear</span>'
                      : '<span class="g-chip g-chip--blocked">Pending</span>';
}
function flags(r) {
    var h = '';
    for (var i = 0; i < r.findings.length; i++) {
        var f = r.findings[i];
        if (f.level === 'INFO') continue;
        var t = f.code === 'OWES' ? 'Owes' : f.code === 'NO_GRAD_FEE' ? 'No grad fee'
              : f.code === 'PENDING_RECEIPTS' ? 'Receipt pending' : f.code === 'OWES_SINCE' ? 'Owes since clearance' : f.code;
        h += '<span class="gf-flag gf-flag--' + f.level + '" title="' + G.esc(f.text) + '">' + G.esc(t) + '</span>';
    }
    return '<div class="gf-flags">' + (h || '<span class="g-sub">nothing</span>') + '</div>';
}

function state() {
    return { year: G.qs('fYear').value, faculty: G.qs('fFac').value, dept: G.qs('fDep').value,
             prog: G.qs('fProg').value, status: G.qs('fStatus').value, q: G.qs('fQ').value.trim(),
             page: page > 1 ? page : '' };
}
function cfg() {
    var s = state();
    return JSON.stringify({ acadYear: s.year, faculty: s.faculty, department: s.dept, programme: s.prog,
                            search: s.q, status: s.status, page: String(page) });
}
function sync() { G.writeUrl(state()); chips(); load(); }

function txt(id) {
    var el = G.qs(id);
    return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.trim() : '';
}
function chips() {
    G.chips('gChips', [
        { k: 'fFac', label: 'Faculty', value: G.qs('fFac').value ? txt('fFac') : '' },
        { k: 'fDep', label: 'Department', value: G.qs('fDep').value ? txt('fDep') : '' },
        { k: 'fProg', label: 'Programme', value: G.qs('fProg').value ? txt('fProg') : '' },
        { k: 'fStatus', label: 'Show', value: G.qs('fStatus').value ? txt('fStatus') : '' },
        { k: 'fQ', label: 'Search', value: G.qs('fQ').value.trim() }
    ], function (k) {
        if (k === '*') resetFilters(); else G.qs(k).value = '';
        G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync();
    });
}
function resetFilters() {
    ['fFac', 'fDep', 'fProg', 'fStatus'].forEach(function (id) { G.qs(id).value = ''; });
    G.qs('fQ').value = '';
}

function kpis(k) {
    function tile(key, n, label, sub, cls) {
        return '<button type="button" class="g-kpi' + (cls ? ' g-kpi--' + cls : '') + '" data-st="' + key + '"' +
               (G.qs('fStatus').value === key ? ' style="border-left-color:var(--accent);background:#f5f8ff;"' : '') + '>' +
               '<b>' + G.esc(String(n)) + '</b><span>' + G.esc(label) + '</span>' + (sub ? '<small>' + sub + '</small>' : '') + '</button>';
    }
    G.qs('gKpis').innerHTML =
        tile('', k.total, 'On the list', 'approved by the Registry', '') +
        tile('cleared', k.cleared, 'Cleared', k.total ? Math.round(k.cleared * 100 / k.total) + '% of the list' : '', 'ok') +
        tile('ready', k.ready, 'Ready to clear', 'nothing outstanding', k.ready ? 'ok' : '') +
        tile('owing', k.owing, 'Owing', 'UGX ' + money(k.owed) + ' in total', k.owing ? 'bad' : '') +
        tile('nofee', k.nofee, 'No graduation fee', 'not yet billed', k.nofee ? 'warn' : '') +
        tile('held', k.held, 'On finance hold', '', k.held ? 'warn' : '') +
        tile('drift', k.drift, 'Owing again', 'cleared, then billed', k.drift ? 'bad' : '');
    var b = G.qs('gKpis').querySelectorAll('[data-st]');
    for (var i = 0; i < b.length; i++)
        b[i].addEventListener('click', function () { G.qs('fStatus').value = this.getAttribute('data-st'); page = 1; sync(); });
}

function load() {
    G.qs('gBody').innerHTML = '<tr><td colspan="8" class="g-load">Reading live balances&hellip;</td></tr>';
    G.qs('gPager').innerHTML = '';
    G.ajax(PAGE, 'GetQueue', { configJson: cfg() }, function (d) {
        if (!d || !d.success) { G.qs('gBody').innerHTML = ''; G.toast((d && d.message) || 'Could not load the list.', false); return; }
        rows = d.rows || []; total = d.total || 0; page = d.page || 1;
        kpis(d.kpis || {});
        G.qs('gGateNote').innerHTML = (G.qs('fYear').value && !d.gated)
            ? '<div class="g-note g-note--info">Lists before ' + G.esc(String(BOOT.settings.firstGatedYear)) + '/' +
              (BOOT.settings.firstGatedYear + 1) + ' were compiled before fees clearance existed. Their names are shown for reference; the Graduation Fee rule and the document gate do not apply to them.</div>'
            : '';
        var h = '';
        for (var i = 0; i < rows.length; i++) {
            var r = rows[i];
            var canPick = BOOT.canEdit && r.status !== 'CLEARED';
            h += '<tr class="is-click" data-reg="' + G.esc(r.regno) + '">' +
                 '<td><input type="checkbox" class="ck" data-reg="' + G.esc(r.regno) + '"' +
                    (canPick ? '' : ' disabled') + (picked[r.regno] ? ' checked' : '') + ' /></td>' +
                 '<td>' + G.photoCell(r.regno, r.name) + '</td>' +
                 '<td>' + G.esc(r.progname || r.progcode) + '<div class="g-sub">' + G.esc(r.progcode) + ' · ' + G.esc(r.acadyear) + '</div></td>' +
                 '<td class="g-num">' + balCell(r.balance) + '</td>' +
                 '<td>' + (!r.gradFeeRequired ? '<span class="g-sub">not required</span>'
                            : r.gradFeeBilled ? '<span class="gf-cr" style="font-weight:700;">Billed</span>'
                            : '<span class="gf-owe" style="font-weight:700;">Not billed</span>') + '</td>' +
                 '<td>' + flags(r) + '</td>' +
                 '<td>' + chipFor(r) + (r.actor ? '<div class="g-sub">' + G.esc(r.actor) + ' · ' + G.esc(r.decidedAt) + '</div>' : '') + '</td>' +
                 '<td><button type="button" class="g-btn g-btn--sm" data-open="' + G.esc(r.regno) + '">Open</button></td></tr>';
        }
        G.qs('gBody').innerHTML = h || '<tr><td colspan="8" class="g-empty">Nobody matches these filters.</td></tr>';
        G.qs('gMeta').textContent = total + ' graduand' + (total === 1 ? '' : 's');
        var pages = Math.max(1, Math.ceil(total / (d.size || 100)));
        G.pager('gPager', { page: page, size: d.size || 100, total: total, shown: rows.length }, function (n) { page = n; sync(); });
        var ids = []; for (var q = 0; q < rows.length; q++) ids.push(rows[q].regno);
        G.queue({ ids: ids, total: total, page: page, pages: pages, size: d.size || 100, nextPage: function (n) { page = n; sync(); } });
        G.qs('ckAll').checked = false;
        batchBar();
    });
}

/* ── bulk ── */
function pickedIds() { var a = []; for (var k in picked) if (picked.hasOwnProperty(k) && picked[k]) a.push(k); return a; }
function batchBar() {
    var bar = G.qs('gBatch'), ids = pickedIds(), n = ids.length;
    if (!n || !BOOT.canEdit) { bar.style.display = 'none'; bar.innerHTML = ''; return; }
    bar.style.display = 'flex';
    bar.innerHTML = '<span class="g-batch__n">' + n + ' selected</span>' +
        '<button type="button" class="g-btn g-btn--sm g-btn--p" id="bClear">Clear those who owe nothing</button>' +
        (BOOT.settings.requireGradFee ? '<button type="button" class="g-btn g-btn--sm" id="bFee">Bill the Graduation Fee (UGX ' + money(BOOT.settings.gradFeeAmount) + ')</button>' : '') +
        '<button type="button" class="g-btn g-btn--sm g-batch__x" id="bNone">Deselect</button>';
    G.qs('bNone').addEventListener('click', function () { picked = {}; load(); });
    G.qs('bClear').addEventListener('click', function () {
        if (!confirm('Clear the ' + n + ' selected student(s)?\n\nAnyone who still owes money, has no Graduation Fee, or is otherwise blocked is skipped and listed. No one is overridden.')) return;
        bulk('BulkClear', { regnos: ids }, 'cleared');
    });
    if (G.qs('bFee')) G.qs('bFee').addEventListener('click', function () {
        var amt = prompt('Graduation Fee to bill to each of the ' + n + ' selected student(s), in UGX.\nAnyone already billed for this graduation is skipped.', String(BOOT.settings.gradFeeAmount));
        if (amt === null) return;
        var a = parseInt(String(amt).replace(/[^0-9]/g, ''), 10);
        if (!(a > 0)) { G.toast('Enter a whole amount.', false); return; }
        if (!confirm('Post a Graduation Fee of UGX ' + money(a) + ' to up to ' + n + ' student(s)?\nThat is up to UGX ' + money(a * n) + ' in total. Each bill goes to the student ledger and the general ledger.')) return;
        bulk('BulkGradFee', { regnos: ids, amount: a }, 'billed');
    });
}
function bulk(method, args, verb) {
    G.toast('Working on ' + args.regnos.length + ' student(s)…', true);
    G.ajax(PAGE, method, args, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'That did not go through.', false); return; }
        picked = {};
        var msg = d.done + ' ' + verb + '.';
        if (d.skipped && d.skipped.length) {
            msg += ' ' + d.skipped.length + ' skipped.';
            var lines = d.skipped.slice(0, 25).map(function (s) { return s.regno + ': ' + s.why; }).join('\n');
            setTimeout(function () { alert('Skipped (' + d.skipped.length + '):\n\n' + lines + (d.skipped.length > 25 ? '\n…' : '')); }, 50);
        }
        G.toast(msg, d.done > 0);
        load();
    });
}

/* ── the student workspace ── */
function open(reg) {
    CUR = null;
    var ids = G.queueIds();
    for (var i = 0; i < ids.length; i++) if (ids[i] === reg) { G.queueSetIndex(i); break; }
    G.qs('gModalName').textContent = reg;
    G.qs('gModalSub').textContent = 'Reading the account…';
    G.showPhoto(true);
    G.qs('gModalPhoto').src = G.photo(reg, 144);
    G.qs('gModalBody').innerHTML = '<div class="g-load">Loading the ledger…</div>';
    G.qs('gModalFoot').innerHTML = '';
    G.openModal();
    G.ajax(PAGE, 'GetStudent', { regno: reg }, function (d) {
        if (!d || !d.success) {
            G.qs('gModalBody').innerHTML = '<div class="g-note g-note--bad">' + G.esc((d && d.message) || 'Could not load that student.') + '</div>';
            return;
        }
        CUR = d; render();
    });
}

function render() {
    var d = CUR, s = d.student, t = d.totals, id = d.ident || {};
    G.qs('gModalName').textContent = s.name || s.regno;
    var pos = G.queueIndex() >= 0 ? '  ·  ' + (G.queueIndex() + 1) + ' of ' + G.queueIds().length + ' on this page' : '';
    G.qs('gModalSub').textContent = s.regno + (id.entryno ? '  ·  ' + id.entryno : '') + '  ·  ' + (s.progname || s.progcode) + pos;

    var nb = 0, nw = 0;
    for (var i = 0; i < s.findings.length; i++) { if (s.findings[i].level === 'BLOCK') nb++; else if (s.findings[i].level === 'WARN') nw++; }
    var missing = 0; for (i = 0; i < d.semesters.length; i++) if (d.semesters[i].missing) missing++;

    var tabs = [['overview', 'Overview', nb + nw ? (nb + nw) : ''], ['ledger', 'Ledger', d.ledger.length],
                ['sems', 'Semesters', missing ? missing + ' missing' : d.semesters.length],
                ['bill', 'Bill', ''], ['pay', 'Record payment', ''], ['history', 'History', d.history.length + d.postings.length]];
    var h = '<div class="gf-tabs">';
    tabs.forEach(function (x) {
        h += '<button type="button" class="gf-tab' + (TAB === x[0] ? ' is-on' : '') + '" data-tab="' + x[0] + '">' + x[1] +
             (x[2] !== '' && x[2] !== 0 ? '<em>' + G.esc(String(x[2])) + '</em>' : '') + '</button>';
    });
    h += '</div>';

    // Overview
    var stWord = s.status === 'CLEARED' ? (s.owesSinceClearance ? 'Cleared, owes again' : 'Cleared')
               : s.status === 'HELD' ? 'On finance hold' : (s.eligible ? 'Ready to clear' : 'Not cleared');
    var stCls = s.status === 'CLEARED' && !s.owesSinceClearance ? 'gf-cr' : (s.eligible ? 'gf-cr' : 'gf-owe');
    var ov = '<div class="gf-hero">' +
        '<div class="gf-big"><span>Balance</span><b>' + balCell(t.balance) + '</b><small>' +
          (t.balance > 0 ? 'owed to the University' : t.balance < 0 ? 'in credit' : 'nothing owed') + '</small></div>' +
        '<div class="gf-big"><span>Billed</span><b class="gf-money">' + money(t.billed) + '</b><small>all charges, all years</small></div>' +
        '<div class="gf-big"><span>Paid and credited</span><b class="gf-money">' + money(t.paid) + '</b><small>' +
          t.payments + ' payment' + (t.payments === 1 ? '' : 's') + (t.lastPayment ? ', last on ' + G.esc(t.lastPayment) : '') + '</small></div>' +
        '<div class="gf-big gf-big--status"><span>Finance clearance</span><b class="' + stCls + '" style="font-size:16px;">' + G.esc(stWord) + '</b><small>' +
          (s.actor ? G.esc(s.actor) + ' · ' + G.esc(s.decidedAt) + (s.reason ? '<br>' + G.esc(s.reason) : '') : 'no finance decision yet') + '</small></div>' +
        '</div>';
    ov += '<div class="g-facts">' +
        fact('Graduation list', s.acadyear, s.degclass) +
        fact('Approved by Registry', id.approvedBy || '(before the module)', id.approvedOn || '') +
        fact('Graduation fee', !s.gradFeeRequired ? 'Not required' : (s.gradFeeBilled ? 'Billed' : 'NOT billed'), s.acadyear) +
        fact('SchoolPay received', 'UGX ' + money(t.schoolPay), '') +
        fact('Bursaries', 'UGX ' + money(t.bursary), '') +
        fact('Waivers', 'UGX ' + money(t.waivers), '') +
        fact('Session', id.session || '-', 'entry ' + (id.entryyear || '-')) +
        fact('Contact', id.phone || '-', id.email || '') +
        '</div>';
    ov += '<div class="g-sec" style="margin-top:6px;"><div class="g-sum__h" style="font-weight:700;color:var(--navy);margin:4px 0 6px;">What stands between this student and clearance</div>';
    if (!s.findings.length) ov += '<div class="g-find g-find--PASS"><div class="g-find__d">Nothing. The account is settled and every check passes.</div></div>';
    for (i = 0; i < s.findings.length; i++) {
        var f = s.findings[i];
        var lv = f.level === 'INFO' ? 'NA' : f.level;
        ov += '<div class="g-find g-find--' + lv + '"><div class="g-find__n">' +
              (f.level === 'BLOCK' ? 'Blocks clearance' : f.level === 'WARN' ? 'Check before clearing' : 'For information') +
              '</div><div class="g-find__d">' + G.esc(f.text) + '</div></div>';
    }
    ov += '</div><div class="gf-links">' +
        '<a href="StudentLedgers.aspx?search=' + encodeURIComponent(s.regno) + '" target="_blank" rel="noopener">Open in Student Ledgers</a>' +
        '<a href="../../API/StudentLedgerExport.aspx?reg=' + encodeURIComponent(s.regno) + '" target="_blank" rel="noopener">Printable statement</a>' +
        '<a href="BillWaivers.aspx?regno=' + encodeURIComponent(s.regno) + '" target="_blank" rel="noopener">Waivers and adjustments</a>' +
        '</div>';

    // Ledger
    var lg = '<div class="g-wrap"><table class="g-tbl gf-tbl"><thead><tr><th>Date</th><th>Particulars</th>' +
             '<th class="g-num">Debit</th><th class="g-num">Credit</th><th class="g-num">Balance</th></tr></thead><tbody>';
    for (i = 0; i < d.ledger.length; i++) {
        var L = d.ledger[i];
        lg += '<tr><td style="white-space:nowrap;">' + G.esc(L.date) + '</td><td>' + G.esc(L.detail) + '</td>' +
              '<td class="gf-money">' + (L.debit ? money(L.debit) : '') + '</td>' +
              '<td class="gf-money">' + (L.credit ? money(L.credit) : '') + '</td>' +
              '<td class="gf-money">' + balCell(L.balance) + '</td></tr>';
    }
    lg += '</tbody><tfoot><tr><th></th><th>Totals</th><th class="g-num gf-money">' + money(t.billed) + '</th><th class="g-num gf-money">' +
          money(t.paid) + '</th><th class="g-num">' + balCell(t.billed - t.paid) + '</th></tr></tfoot></table></div>';
    if (!d.ledger.length) lg = '<div class="g-empty">No transactions on this account.</div>';

    // Semesters
    var sm = '<p class="g-sub" style="margin:0 0 7px;">Every semester the student registered for, against the tuition and functional fees billed for it (net of reversals). A registered semester with nothing billed is a missing charge: clearing the student would forgive it.</p>' +
             '<div class="g-wrap"><table class="g-tbl gf-tbl"><thead><tr><th>Year</th><th>Study year</th><th>Semester</th><th>Registration</th>' +
             '<th class="g-num">Billed</th><th class="g-num">Reversed</th><th class="g-num">Net</th><th></th></tr></thead><tbody>';
    for (i = 0; i < d.semesters.length; i++) {
        var S = d.semesters[i];
        sm += '<tr' + (S.missing ? ' class="is-miss"' : '') + '><td>' + G.esc(S.acadYear) + '</td><td>' + G.esc(S.studyYear) + '</td><td>' +
              G.esc(S.semester) + '</td><td>' + G.esc(S.regStatus) + '</td><td class="gf-money">' + money(S.billed) + '</td><td class="gf-money">' +
              (S.reversed ? money(S.reversed) : '') + '</td><td class="gf-money">' + money(S.net) + '</td><td>' +
              (S.missing ? '<span class="gf-flag gf-flag--WARN">' + (S.fullyReversed ? 'reversed, not re-billed' : 'not billed') + '</span>' : '') + '</td></tr>';
    }
    sm += '</tbody></table></div>';
    if (!d.semesters.length) sm = '<div class="g-empty">No semester registrations on record.</div>';

    // Bill
    var o = d.options, canEdit = d.rights.edit;
    var bl = !canEdit ? '<div class="g-note g-note--warn">You may view this account but not post bills.</div>' : '';
    if (canEdit && s.gradFeeRequired && !s.gradFeeBilled)
        bl += '<div class="gf-quick"><span><b>No Graduation Fee</b> has been billed for the ' + G.esc(s.acadyear) + ' graduation.</span>' +
              '<input type="text" id="qFee" value="' + money(o.gradFeeAmount) + '" style="width:110px;border:1px solid var(--line);padding:5px 7px;" />' +
              '<button type="button" class="g-btn g-btn--p g-btn--sm" id="qFeeGo">Bill the Graduation Fee</button></div>';
    if (canEdit) {
        var itemOpts = o.items.map(function (x) { return '<option value="' + G.esc(x.v) + '"' + (+x.v === +o.gradFeeItem ? ' selected' : '') + '>' + G.esc(x.t) + '</option>'; }).join('');
        bl += '<div class="gf-form">' +
              '<div><label for="bItem">Item</label><select id="bItem">' + itemOpts + '</select></div>' +
              '<div><label for="bAmt">Amount (UGX)</label><input id="bAmt" type="text" inputmode="numeric" placeholder="e.g. 505,000" /></div>' +
              '<div><label for="bYear">Academic year</label><select id="bYear">' + yearOptions(o.year) + '</select></div>' +
              '<div><label for="bSem">Semester</label><select id="bSem"><option>1</option><option' + (o.gradFeeSemester === 2 ? ' selected' : '') + '>2</option><option>3</option></select></div>' +
              '<div class="gf-wide"><label for="bDet">Description (optional)</label><input id="bDet" type="text" maxlength="250" placeholder="Defaults to the item name, semester and year" /></div>' +
              '</div><div class="gf-act"><button type="button" class="g-btn g-btn--p" id="bGo">Post the bill</button>' +
              '<span class="g-sub">Posted to the student ledger and the general ledger together. The same item cannot be billed twice for one semester.</span></div>';
    }

    // Payment
    var py = !canEdit ? '<div class="g-note g-note--warn">You may view this account but not record payments.</div>' : '';
    if (canEdit) {
        var bankOpts = '<option value="">Choose…</option>' + o.banks.map(function (x) { return '<option value="' + G.esc(x.v) + '">' + G.esc(x.v + ' · ' + x.t) + '</option>'; }).join('');
        py = '<div class="g-note g-note--info">For money paid at the bank or in cash that is not already on the account. SchoolPay payments arrive by themselves; if one is missing, use the SchoolPay controller instead so it is not recorded twice.</div>' +
             '<div class="gf-form">' +
             '<div><label for="pBank">Paid into</label><select id="pBank">' + bankOpts + '</select></div>' +
             '<div><label for="pAmt">Amount (UGX)</label><input id="pAmt" type="text" inputmode="numeric" /></div>' +
             '<div><label for="pDate">Date paid</label><input id="pDate" type="date" max="' + today() + '" value="' + today() + '" /></div>' +
             '<div><label for="pRef">Receipt / bank slip number</label><input id="pRef" type="text" maxlength="60" /></div>' +
             '<div class="gf-wide"><label for="pPayer">Paid by (optional)</label><input id="pPayer" type="text" maxlength="80" placeholder="e.g. the sponsor or parent named on the slip" /></div>' +
             '</div><div class="gf-act"><button type="button" class="g-btn g-btn--p" id="pGo">Record the payment</button>' +
             '<span class="g-sub">The receipt number must be unique. The bank account is debited and the student credited in one transaction.</span></div>';
    }

    // History
    var hs = '';
    if (d.history.length) {
        hs += '<div class="g-sum__h" style="font-weight:700;color:var(--navy);margin:2px 0 6px;">Finance decisions</div>';
        d.history.forEach(function (x) {
            hs += '<div class="gf-hist gf-hist--' + x.verdict + (x.current ? '' : ' is-old') + '"><b>' + G.esc(x.verdict === 'CLEARED' ? 'Cleared' : x.verdict === 'HELD' ? 'Put on hold' : 'Clearance revoked') +
                  '</b>' + (x.basis ? ' (' + G.esc(x.basis === 'OVERRIDE' ? 'override' : x.basis === 'IN_CREDIT' ? 'in credit' : 'nothing owed') + ')' : '') +
                  ' by ' + G.esc(x.actor) + (x.role ? ' · ' + G.esc(x.role) : '') + ' · ' + G.esc(x.at) + (x.bulk ? ' · bulk' : '') +
                  (x.current ? '' : ' · superseded') + '<br>balance then: ' + balCell(x.balance) +
                  (x.reason ? '<br>' + G.esc(x.reason) : '') + '</div>';
        });
    }
    if (d.postings.length) {
        hs += '<div class="g-sum__h" style="font-weight:700;color:var(--navy);margin:10px 0 6px;">Posted from this screen</div><div class="g-wrap"><table class="g-tbl gf-tbl"><thead><tr><th>When</th><th>What</th><th>Details</th><th class="g-num">Amount</th><th>By</th></tr></thead><tbody>';
        d.postings.forEach(function (x) {
            hs += '<tr><td style="white-space:nowrap;">' + G.esc(x.at) + '</td><td>' + G.esc(x.kind === 'BILL' ? 'Bill' : 'Payment') + '</td><td>' + G.esc(x.detail) +
                  (x.reference ? '<div class="g-sub">ref ' + G.esc(x.reference) + ' · voucher ' + G.esc(String(x.voucher)) + '</div>' : '<div class="g-sub">bill ' + G.esc(String(x.tid)) + '</div>') +
                  '</td><td class="gf-money">' + money(x.amount) + '</td><td>' + G.esc(x.actor) + '</td></tr>';
        });
        hs += '</tbody></table></div>';
    }
    if (!hs) hs = '<div class="g-empty">No finance decisions or postings yet.</div>';

    h += pane('overview', ov) + pane('ledger', lg) + pane('sems', sm) + pane('bill', bl) + pane('pay', py) + pane('history', hs);
    G.qs('gModalBody').innerHTML = h;

    var tb = G.qs('gModalBody').querySelectorAll('.gf-tab');
    for (i = 0; i < tb.length; i++) tb[i].addEventListener('click', function () { TAB = this.getAttribute('data-tab'); render(); });
    wireMoney();
    footer();
}
function pane(k, body) { return '<div class="gf-pane' + (TAB === k ? ' is-on' : '') + '">' + body + '</div>'; }
function fact(label, value, sub) {
    return '<div class="g-fact"><span>' + G.esc(label) + '</span><b>' + G.esc(value) + '</b>' + (sub ? '<small>' + G.esc(sub) + '</small>' : '') + '</div>';
}
function today() { var d = new Date(); return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2); }
function yearOptions(sel) {
    var ys = (BOOT && BOOT.years) || [];
    if (sel && ys.indexOf(sel) < 0) ys = [sel].concat(ys);
    return ys.map(function (y) { return '<option' + (y === sel ? ' selected' : '') + '>' + G.esc(y) + '</option>'; }).join('');
}
function num(id) { var v = (G.qs(id) && G.qs(id).value) || ''; var n = parseInt(String(v).replace(/[^0-9]/g, ''), 10); return n > 0 ? n : 0; }

function wireMoney() {
    var s = CUR.student;
    if (G.qs('qFeeGo')) G.qs('qFeeGo').addEventListener('click', function () {
        var a = num('qFee'); if (!a) { G.toast('Enter the amount.', false); return; }
        if (!confirm('Bill a Graduation Fee of UGX ' + money(a) + ' to ' + s.name + ' for the ' + s.acadyear + ' graduation?')) return;
        G.ajax(PAGE, 'BillGradFee', { regno: s.regno, amount: a }, after);
    });
    if (G.qs('bGo')) G.qs('bGo').addEventListener('click', function () {
        var a = num('bAmt'), it = G.qs('bItem');
        if (!a) { G.toast('Enter the amount.', false); return; }
        if (!confirm('Bill ' + it.options[it.selectedIndex].text + ' of UGX ' + money(a) + ' to ' + s.name + '?')) return;
        G.ajax(PAGE, 'BillItem', { regno: s.regno, itemCode: +it.value, amount: a, acadYear: G.qs('bYear').value,
                                   semester: +G.qs('bSem').value, detail: G.qs('bDet').value.trim() }, after);
    });
    if (G.qs('pGo')) G.qs('pGo').addEventListener('click', function () { pay(false); });
}
function pay(confirmSameDay) {
    var s = CUR.student, a = num('pAmt'), bank = G.qs('pBank').value, ref = G.qs('pRef').value.trim(), dt = G.qs('pDate').value;
    if (!bank) { G.toast('Choose the account the money was paid into.', false); return; }
    if (!a) { G.toast('Enter the amount.', false); return; }
    if (ref.length < 3) { G.toast('Enter the receipt or bank slip number.', false); return; }
    if (!confirmSameDay && !confirm('Record UGX ' + money(a) + ' paid on ' + dt + ' into ' + bank + ' (ref ' + ref + ') for ' + s.name + '?')) return;
    G.ajax(PAGE, 'RecordPayment', { regno: s.regno, bankCode: bank, amount: a, payDate: dt, reference: ref,
                                    payer: G.qs('pPayer').value.trim(), confirmSameDay: confirmSameDay }, function (d) {
        if (d && d.sameDay) { if (confirm(d.message + '\n\nRecord it anyway?')) pay(true); return; }
        after(d);
    });
}
function after(d) {
    if (d && d.success) { G.toast(d.message, true); dirty = true; TAB = 'overview'; reopen(); }
    else G.toast((d && d.message) || 'Nothing was posted.', false);
}
function reopen() { if (CUR) open(CUR.student.regno); }

function footer() {
    var s = CUR.student, r = CUR.rights, h = '';
    if (r.edit) {
        if (s.status !== 'CLEARED') {
            h += s.eligible ? '<button type="button" class="g-btn g-btn--p" id="mClear">Clear for graduation</button>'
                            : (r.override ? '<button type="button" class="g-btn g-btn--d" id="mOverride">Clear anyway (override)</button>' : '');
            h += '<button type="button" class="g-btn" id="mHold">' + (s.status === 'HELD' ? 'Change the hold reason' : 'Put on finance hold') + '</button>';
        } else if (r.override) {
            h += '<button type="button" class="g-btn g-btn--d" id="mRevoke">Revoke the clearance</button>';
        }
    }
    h += '<span class="gf-sp"></span>' +
         '<button type="button" class="g-btn g-btn--sm" id="mPrev">&lsaquo; Previous</button>' +
         '<button type="button" class="g-btn g-btn--sm" id="mNext">Next &rsaquo;</button>';
    G.qs('gModalFoot').innerHTML = h;
    if (G.qs('mClear')) G.qs('mClear').addEventListener('click', function () {
        if (!confirm('Clear ' + s.name + ' for graduation?\n\nBalance: ' + (s.balance > 0 ? 'UGX ' + money(s.balance) + ' owed' : 'nothing owed'))) return;
        decide('CLEARED', '', false);
    });
    if (G.qs('mOverride')) G.qs('mOverride').addEventListener('click', function () {
        var b = s.findings.filter(function (f) { return f.level === 'BLOCK'; }).map(function (f) { return f.text; }).join(' ');
        G.reasonDialog({ page: PAGE, regno: s.regno, min: 15, title: 'Clear ' + s.name + ' anyway',
            subtitle: s.regno + '  ·  ' + (s.progname || s.progcode),
            warn: 'Still outstanding: ' + b + ' An override is recorded against your name and shown on the graduation list.',
            verb: 'Clear anyway', onSubmit: function (why) { decide('CLEARED', why, true); } });
    });
    if (G.qs('mHold')) G.qs('mHold').addEventListener('click', function () {
        G.reasonDialog({ page: PAGE, regno: s.regno, title: 'Put ' + s.name + ' on finance hold',
            subtitle: 'The student sees this reason on the portal.', verb: 'Put on hold', initial: s.status === 'HELD' ? s.reason : '',
            onSubmit: function (why) { decide('HELD', why, false); } });
    });
    if (G.qs('mRevoke')) G.qs('mRevoke').addEventListener('click', function () {
        G.reasonDialog({ page: PAGE, regno: s.regno, title: 'Revoke the finance clearance for ' + s.name,
            subtitle: 'They return to the pending queue. The original clearance stays in the history.', verb: 'Revoke',
            onSubmit: function (why) { decide('REVOKED', why, false); } });
    });
    var pv = G.queuePrev(), nx = G.queueNext();
    G.qs('mPrev').disabled = pv < 0; G.qs('mNext').disabled = nx < 0;
    G.qs('mPrev').addEventListener('click', function () { if (pv >= 0) open(G.queueAt(pv)); });
    G.qs('mNext').addEventListener('click', function () { if (nx >= 0) open(G.queueAt(nx)); });
}
function decide(verdict, reason, asOverride) {
    var s = CUR.student;
    G.ajax(PAGE, 'Decide', { regno: s.regno, verdict: verdict, reason: reason || '', asOverride: !!asOverride }, function (d) {
        if (!(d && d.success)) { G.toast((d && d.message) || 'Nothing was saved.', false); return; }
        G.toast(d.message, true); dirty = true;
        G.markDecided(s.regno, verdict === 'CLEARED' ? 'Cleared' : verdict === 'HELD' ? 'Finance hold' : 'Revoked');
        var nx = G.queueNext();
        if (verdict !== 'REVOKED' && nx >= 0) open(G.queueAt(nx)); else reopen();
    });
}

/* ── export ── */
function openExport() {
    G.exportDialog({
        page: PAGE, title: 'Export fees clearance', subtitle: 'Live balances and the finance decision for each graduand.',
        cfg: cfg(), columns: window.G_COLS || [], pageRows: rows.length, countMethod: 'CountExport',
        filters: { focus: false, years: (BOOT && BOOT.years) || [], faculties: (BOOT && BOOT.faculties) || [],
                   departments: (BOOT && BOOT.departments) || [], programmes: (BOOT && BOOT.programmes) || [],
                   current: { acadYear: G.qs('fYear').value, faculty: G.qs('fFac').value,
                              department: G.qs('fDep').value, programme: G.qs('fProg').value } },
        filterSummary: [ { label: 'Graduation year', value: G.qs('fYear').value || 'All years' },
                         { label: 'Showing', value: txt('fStatus') } ],
        sorts: [ { k: 'name', t: 'Programme, then name' } ], sortDefault: 'name',
        groups: [ { k: 'prog', t: 'Programme' } ], groupDefault: 'prog',
        sheets: [ { k: 'summary', t: 'Summary by programme', d: 'Cleared, pending, held and owed per programme', on: true } ]
    });
}

document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    G.wireModal(function () { CUR = null; if (dirty) { dirty = false; load(); } });
    var pre = G.readUrl();

    document.addEventListener('click', function (e) {
        var ck = e.target.closest ? e.target.closest('#gBody .ck') : null;
        if (ck) { e.stopPropagation(); picked[ck.getAttribute('data-reg')] = ck.checked; batchBar(); return; }
        var b = e.target.closest ? e.target.closest('#gBody [data-open]') : null;
        if (b) { e.stopPropagation(); open(b.getAttribute('data-open')); return; }
        var tr = e.target.closest ? e.target.closest('#gBody tr[data-reg]') : null;
        if (tr && !(e.target.tagName === 'INPUT')) open(tr.getAttribute('data-reg'));
    });
    G.qs('ckAll').addEventListener('change', function () {
        var b = document.querySelectorAll('#gBody .ck');
        for (var i = 0; i < b.length; i++) if (!b[i].disabled) { b[i].checked = G.qs('ckAll').checked; picked[b[i].getAttribute('data-reg')] = b[i].checked; }
        batchBar();
    });
    ['fYear', 'fProg', 'fStatus'].forEach(function (id) { G.qs(id).addEventListener('change', function () { page = 1; picked = {}; sync(); }); });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync(); });
    var typed = G.debounce(function () { page = 1; sync(); }, 350);
    G.qs('fQ').addEventListener('input', typed);
    G.qs('btnXls').addEventListener('click', openExport);
    G.qs('btnReset').addEventListener('click', function () {
        resetFilters(); if (BOOT && BOOT.currentYear) G.qs('fYear').value = BOOT.currentYear;
        G.cascade('fFac', 'fDep', 'fProg'); page = 1; sync();
    });

    function applyBoot(o) {
        if (!o || !o.success || !o.hasAccess) {
            G.qs('gToolbar').style.display = 'none';
            var na = G.qs('gNoAccess'); na.style.display = 'block';
            na.textContent = (o && o.message) || 'You do not have access to Fees Clearance.';
            return;
        }
        BOOT = o;
        G.qs('gScope').textContent = o.roleNote || '';
        G.fill('fYear', o.years.map(function (y) { return { v: y, t: y }; }), null);
        G.fill('fFac', o.faculties, 'All faculties');
        G.fill('fDep', o.departments, 'All departments');
        G.fill('fProg', o.programmes, 'All programmes');
        G.qs('fYear').value = pre.year || o.currentYear || '';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        if (pre.q) G.qs('fQ').value = pre.q;
        if (pre.status) G.qs('fStatus').value = pre.status;
        page = parseInt(pre.page || '1', 10) || 1;
        G.cascade('fFac', 'fDep', 'fProg');
        G.combo('fProg', 'Type a code or part of the name…', { key: true });
        chips(); load();
    }
    if (window.G_BOOT) applyBoot(window.G_BOOT); else G.ajax(PAGE, 'GetBootstrap', {}, applyBoot);
});
})();
</script>
</asp:Content>
