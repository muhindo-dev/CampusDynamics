<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationFinance.aspx.cs" Inherits="COOPERP_NewScreens_GraduationFinance" Title="Fees Clearance - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="css/graduation.css" />
<style>
/* ── Fees Clearance, on top of the shared graduation styles ── */
.gf-money{font-variant-numeric:tabular-nums;white-space:nowrap;font-weight:700;}
.gf-owe{color:var(--bad);} .gf-cr{color:var(--ok);} .gf-zero{color:var(--mute);}
.gf-flags{display:flex;flex-wrap:wrap;gap:3px;}
.gf-flag{display:inline-block;font-size:9.5px;font-weight:700;padding:1px 6px;border-radius:9px;white-space:nowrap;}
.gf-flag--BLOCK{background:var(--bad-bg);color:var(--bad);}
.gf-flag--WARN{background:var(--warn-bg);color:var(--warn);}

/* summary strip */
.gf-sum{display:flex;align-items:center;gap:14px;flex-wrap:wrap;background:#fff;border:1px solid var(--line);
        border-radius:4px;padding:8px 12px;margin-bottom:8px;}
.gf-sum__prog{flex:1 1 260px;min-width:0;}
.gf-sum__prog b{color:var(--navy);font-size:13px;}
.gf-sum__prog small{color:var(--mute);margin-left:6px;}
.gf-bar{height:7px;background:var(--surface);border:1px solid var(--line);border-radius:4px;overflow:hidden;margin-top:5px;display:flex;}
.gf-bar i{display:block;height:100%;}
.gf-bar .b-ok{background:#2e8b57;} .gf-bar .b-held{background:#8b5cf6;} .gf-bar .b-ready{background:#9fd5b4;}
.gf-sum__fig{display:flex;flex-direction:column;min-width:110px;}
.gf-sum__fig span{font-size:9.5px;text-transform:uppercase;letter-spacing:.4px;color:var(--mute);font-weight:700;}
.gf-sum__fig b{font-size:14px;font-variant-numeric:tabular-nums;}
.gf-sum__age{margin-left:auto;font-size:10.5px;color:var(--mute);display:flex;align-items:center;gap:6px;white-space:nowrap;}

/* queue tabs */
.gf-qtabs{display:flex;gap:4px;flex-wrap:wrap;margin-bottom:8px;}
.gf-qt{display:inline-flex;align-items:center;gap:6px;height:28px;padding:0 10px;background:#fff;border:1px solid var(--line);
       border-radius:14px;font:inherit;font-size:11.5px;font-weight:600;color:var(--ink2);cursor:pointer;white-space:nowrap;}
.gf-qt:hover{border-color:var(--accent);color:var(--accent);}
.gf-qt em{font-style:normal;font-size:10.5px;font-weight:700;min-width:18px;text-align:center;padding:1px 6px;border-radius:9px;
          background:var(--surface);color:var(--ink2);}
.gf-qt.is-on{background:var(--navy);border-color:var(--navy);color:#fff;}
.gf-qt.is-on em{background:rgba(255,255,255,.18);color:#fff;}
.gf-qt--ok em{background:var(--ok-bg);color:var(--ok);} .gf-qt--bad em{background:var(--bad-bg);color:var(--bad);}
.gf-qt--warn em{background:var(--warn-bg);color:var(--warn);} .gf-qt--held em{background:var(--held-bg);color:var(--held);}
.gf-qt.is-zero{opacity:.55;}

/* card header tools */
.gf-tools{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-left:auto;}
.gf-tools label{font-size:10.5px;color:var(--ink2);display:inline-flex;align-items:center;gap:4px;cursor:pointer;font-weight:600;}
.gf-tools select{height:22px;font-size:11px;border:1px solid var(--line);background:#fff;padding:0 4px;}
.g-card__h{display:flex;align-items:center;gap:8px;flex-wrap:wrap;}

/* table */
.gf-list th.gf-sort{cursor:pointer;user-select:none;white-space:nowrap;}
.gf-list th.gf-sort:hover{color:var(--accent);}
.gf-list th.gf-sort i{font-style:normal;color:var(--accent);margin-left:3px;}
.gf-list tbody tr.is-focus td{background:#eef3ff;}
.gf-list tbody tr.is-focus td:first-child{box-shadow:inset 3px 0 0 var(--accent);}
.gf-list tbody tr.is-picked td{background:#f5f8ff;}
.gf-list tbody tr.is-fresh td{animation:gfFlash 1.6s ease-out;}
@keyframes gfFlash{0%{background:#fff6c7;}100%{background:transparent;}}
.gf-list tr.gf-grp td{background:var(--surface);font-weight:700;color:var(--navy);font-size:11px;padding:5px 8px;cursor:default;}
.gf-list tr.gf-grp td small{font-weight:600;color:var(--ink2);margin-left:8px;}
.gf-act-cell{white-space:nowrap;text-align:right;}
.gf-act-cell .g-btn{margin-left:3px;}
mark.gf-hl{background:#fff1a8;color:inherit;padding:0;border-radius:2px;}
.gf-skel td span{display:block;height:10px;border-radius:3px;background:linear-gradient(90deg,#eef1f6 25%,#f7f9fc 50%,#eef1f6 75%);
                 background-size:200% 100%;animation:gfSk 1.2s infinite linear;}
@keyframes gfSk{0%{background-position:200% 0;}100%{background-position:-200% 0;}}

/* pager */
.gf-pg{display:flex;align-items:center;gap:8px;flex-wrap:wrap;padding:7px 11px;border-top:1px solid var(--line);font-size:11px;color:var(--ink2);}
.gf-pg__n b{color:var(--navy);}
.gf-pg__b{display:flex;gap:3px;margin-left:auto;flex-wrap:wrap;}
.gf-pg__b .g-btn{min-width:26px;}
.gf-pg__b .g-btn.is-on{background:var(--navy);border-color:var(--navy);color:#fff;}
.gf-pg__gap{padding:0 3px;color:var(--mute);align-self:center;}

/* bulk bar, pinned to the bottom of the screen while anything is selected */
.gf-bulk{position:sticky;bottom:0;z-index:20;display:none;align-items:center;gap:8px;flex-wrap:wrap;background:var(--navy);color:#fff;
         padding:8px 12px;border-radius:4px 4px 0 0;box-shadow:0 -4px 14px rgba(5,39,92,.18);margin-top:8px;}
.gf-bulk.is-on{display:flex;}
.gf-bulk b{font-size:12px;}
.gf-bulk a{color:#cfe0ff;font-size:11px;cursor:pointer;text-decoration:underline;}
.gf-bulk .g-btn{background:#fff;color:var(--navy);border-color:#fff;}
.gf-bulk .g-btn--p{background:#2e8b57;border-color:#2e8b57;color:#fff;}
.gf-bulk input{width:90px;height:22px;border:0;padding:0 6px;font-size:11px;}
.gf-bulk__x{margin-left:auto;}

/* results of a bulk run */
.gf-res{border:1px solid var(--line);border-left:3px solid var(--accent);background:#fff;border-radius:4px;padding:8px 11px;margin-bottom:8px;font-size:11.5px;}
.gf-res b{color:var(--navy);}
.gf-res ul{margin:5px 0 0;padding-left:18px;max-height:150px;overflow:auto;}
.gf-res li{margin:1px 0;}
.gf-res__x{float:right;background:none;border:0;font-size:15px;line-height:1;cursor:pointer;color:var(--mute);}

.gf-keys{font-size:10.5px;color:var(--mute);margin-top:6px;}
.gf-keys kbd{display:inline-block;min-width:14px;text-align:center;font:10px/1.5 inherit;padding:0 4px;border:1px solid var(--line);
             border-bottom-width:2px;border-radius:3px;background:#fff;color:var(--ink2);margin:0 1px;}
.gf-search{position:relative;}
.gf-search kbd{position:absolute;right:6px;bottom:6px;font-size:9.5px;padding:0 4px;border:1px solid var(--line);border-radius:3px;color:var(--mute);background:#fff;}

/* the student workspace */
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
.gf-pos{font-size:10.5px;color:var(--mute);margin-right:4px;white-space:nowrap;}
/* Graduation Fee suggestions: click a category to fill the amount */
.gf-fees{display:flex;gap:5px;flex-wrap:wrap;align-items:center;margin-top:5px;}
.gf-fees > span{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:var(--mute);}
.gf-fee{display:inline-flex;align-items:baseline;gap:5px;border:1px solid var(--line);background:#fff;border-radius:14px;
        padding:3px 10px;font:inherit;font-size:11px;color:var(--ink2);cursor:pointer;white-space:nowrap;}
.gf-fee b{font-variant-numeric:tabular-nums;color:var(--navy);}
.gf-fee:hover{border-color:var(--accent);color:var(--accent);}
.gf-fee.is-sug{border-color:var(--ok-line);background:var(--ok-bg);}
.gf-fee.is-sug::after{content:"suggested";font-size:9px;font-weight:700;text-transform:uppercase;color:var(--ok);margin-left:2px;}
.gf-fee.is-on{background:var(--navy);border-color:var(--navy);color:#fff;}
.gf-fee.is-on b{color:#fff;} .gf-fee.is-on::after{color:#cfe0ff;}
.gf-bulk .gf-fee{border-color:rgba(255,255,255,.35);background:transparent;color:#fff;}
.gf-bulk .gf-fee b{color:#fff;} .gf-bulk .gf-fee.is-on{background:#fff;color:var(--navy);} .gf-bulk .gf-fee.is-on b{color:var(--navy);}
.gf-feesub{display:block;font-size:10px;color:var(--ink2);font-weight:600;}
@media(max-width:700px){.gf-big b{font-size:17px;} .gf-sum__age{margin-left:0;} .gf-keys{display:none;} .gf-search kbd{display:none;}}
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
    <div class="g-f gf-search" style="flex:1 1 190px;"><label for="fQ">Search</label>
      <input type="text" id="fQ" placeholder="Number, name or programme&hellip;" autocomplete="off" /><kbd>/</kbd></div>
    <div class="g-bar__sp">
      <button type="button" class="g-btn" id="btnRefresh" title="Read every balance again">Refresh</button>
      <button type="button" class="g-btn" id="btnXls">Export&hellip;</button>
      <button type="button" class="g-btn" id="btnReset">Reset</button>
    </div>
  </div>

  <div class="g-chips" id="gChips"></div>
  <div class="gf-sum" id="gfSum" style="display:none;"></div>
  <div class="gf-qtabs" id="gfTabs"></div>
  <div id="gGateNote"></div>
  <div id="gfResult"></div>

  <div class="g-card">
    <div class="g-card__h"><span>Graduands</span><small id="gMeta">&nbsp;</small>
      <div class="gf-tools">
        <label title="Show a heading for each programme"><input type="checkbox" id="optGroup" /> Group by programme</label>
        <label for="optSize">Rows <select id="optSize"><option>25</option><option selected>50</option><option>100</option><option value="0">All</option></select></label>
      </div>
    </div>
    <div class="g-wrap">
      <table class="g-tbl gf-list"><thead><tr>
        <th style="width:24px;"><input type="checkbox" id="ckAll" title="Select everyone on this page" /></th>
        <th class="gf-sort" data-sort="name">Student</th>
        <th class="gf-sort" data-sort="prog">Programme</th>
        <th class="gf-sort g-num" data-sort="balance">Balance (UGX)</th>
        <th class="gf-sort" data-sort="fee">Graduation fee</th>
        <th>Outstanding</th>
        <th class="gf-sort" data-sort="status">Finance</th>
        <th style="text-align:right;"></th>
      </tr></thead><tbody id="gBody"></tbody></table>
    </div>
    <div class="gf-pg" id="gPager"></div>
  </div>

  <div class="gf-bulk" id="gfBulk"></div>

  <div class="gf-keys" id="gfKeys">
    <kbd>/</kbd> search &nbsp; <kbd>&uarr;</kbd><kbd>&darr;</kbd> move &nbsp; <kbd>Enter</kbd> open &nbsp; <kbd>x</kbd> select &nbsp;
    <kbd>&larr;</kbd><kbd>&rarr;</kbd> page &nbsp;&middot;&nbsp; in a student: <kbd>&larr;</kbd><kbd>&rarr;</kbd> previous / next, <kbd>Esc</kbd> close
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
/* =====================================================================
   The queue is read ONCE per year / faculty / department / programme and
   kept in the browser (a graduation list is a few hundred names). Search,
   the status tabs, sorting, grouping and paging all work on that copy, so
   they are instant. After any action only the students touched are read
   again and patched in place: the table never jumps back to page one.
   ===================================================================== */
var PAGE = 'GraduationFinance.aspx', BOOT = null;
var ALL = [], VIEW = [], BYREG = {};
var picked = {}, page = 1, focusReg = null, scopeKey = '', loadedAt = '', loadMs = 0;
var sortKey = 'prog', sortDir = 1, size = 50, grouped = false;
var CUR = null, TAB = 'overview', DC = {}, DC_TTL = 60000;

function store(k, v) { try { if (v === undefined) return localStorage.getItem('gf_' + k); localStorage.setItem('gf_' + k, v); } catch (e) { return null; } }

function money(v) {
    v = Math.round(+v || 0);
    var s = Math.abs(v).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return (v < 0 ? '-' : '') + s;
}
/* Graduation Fee by category. sel = the category to mark "suggested" (the graduand's own). */
function feeCats() { return (BOOT && BOOT.settings && BOOT.settings.feeCategories) || []; }
function catLabel(k) { var c = feeCats().filter(function (x) { return x.key === k; })[0]; return c ? c.label : ''; }
function feeChips(target, sel, extra) {
    var h = '<div class="gf-fees" data-fees="' + target + '"><span>Graduation Fee</span>' + (extra || '');
    feeCats().forEach(function (c) {
        h += '<button type="button" class="gf-fee' + (c.key === sel ? ' is-sug' : '') + '" data-fee="' + (+c.amount) +
             '" data-feecat="' + c.key + '" title="Fill in the ' + G.esc(c.label) + ' fee">' + G.esc(c.label) + ' <b>' + money(c.amount) + '</b></button>';
    });
    return h + '</div>';
}
/* A click on a chip fills its input. Delegated once, so it works on chips drawn at any time. */
document.addEventListener('click', function (e) {
    var b = e.target && e.target.closest ? e.target.closest('.gf-fee[data-fee]') : null;
    if (!b) return;
    var box = b.closest('[data-fees]'), inp = box && G.qs(box.getAttribute('data-fees'));
    var all = box ? box.querySelectorAll('.gf-fee') : [];
    for (var i = 0; i < all.length; i++) all[i].classList.toggle('is-on', all[i] === b);
    if (inp) { inp.value = +b.getAttribute('data-fee') > 0 ? money(+b.getAttribute('data-fee')) : ''; inp.dispatchEvent(new Event('input')); }
    if (box && box.getAttribute('data-fees') === 'bFeeAmt') bulkMode = +b.getAttribute('data-fee') > 0 ? 'fixed' : 'category';
});
var bulkMode = 'category';

function short(v) {
    v = Math.abs(+v || 0);
    if (v >= 1e9) return (v / 1e9).toFixed(2).replace(/\.?0+$/, '') + 'B';
    if (v >= 1e6) return (v / 1e6).toFixed(1).replace(/\.0$/, '') + 'M';
    if (v >= 1e3) return Math.round(v / 1e3) + 'K';
    return String(Math.round(v));
}
function balCell(v) {
    v = +v || 0;
    var cls = v > 0 ? 'gf-owe' : (v < 0 ? 'gf-cr' : 'gf-zero');
    return '<span class="gf-money ' + cls + '">' + (v > 0 ? money(v) : v < 0 ? money(-v) + ' CR' : '0') + '</span>';
}
function chipFor(r) {
    if (r.status === 'CLEARED')
        return r.owesSinceClearance ? '<span class="g-chip g-chip--warn">Cleared · owes again</span>'
             : r.basis === 'OVERRIDE' ? '<span class="g-chip g-chip--warn">Cleared · override</span>'
             : '<span class="g-chip g-chip--ready">Cleared</span>';
    if (r.status === 'HELD') return '<span class="g-chip g-chip--held">Finance hold</span>';
    return r.eligible ? '<span class="g-chip g-chip--listed">Ready to clear</span>'
                      : '<span class="g-chip g-chip--blocked">Pending</span>';
}
function flagWord(c) {
    return c === 'OWES' ? 'Owes' : c === 'NO_GRAD_FEE' ? 'No grad fee' : c === 'PENDING_RECEIPTS' ? 'Receipt pending'
         : c === 'OWES_SINCE' ? 'Owes since clearance' : c;
}
function flags(r) {
    var h = '';
    for (var i = 0; i < r.findings.length; i++) {
        var f = r.findings[i];
        if (f.level === 'INFO') continue;
        h += '<span class="gf-flag gf-flag--' + f.level + '" title="' + G.esc(f.text) + '">' + G.esc(flagWord(f.code)) + '</span>';
    }
    return '<div class="gf-flags">' + (h || '<span class="g-sub">nothing</span>') + '</div>';
}

/* ── the statuses, in one place ── */
var TABS = [
    { k: '',         t: 'Everyone',              cls: '',     test: function () { return true; } },
    { k: 'ready',    t: 'Ready to clear',        cls: 'ok',   test: function (r) { return r.status === 'PENDING' && r.eligible; } },
    { k: 'blocked',  t: 'Something outstanding', cls: 'bad',  test: function (r) { return r.status !== 'CLEARED' && !r.eligible; } },
    { k: 'owing',    t: 'Owing',                 cls: 'bad',  test: function (r) { return r.balance > 0; } },
    { k: 'nofee',    t: 'No graduation fee',     cls: 'warn', test: function (r) { return r.gradFeeRequired && !r.gradFeeBilled; } },
    { k: 'receipts', t: 'Receipts not posted',   cls: 'warn', test: function (r) { return r.pendingReceipts > 0; }, hideZero: true },
    { k: 'held',     t: 'On finance hold',       cls: 'held', test: function (r) { return r.status === 'HELD'; } },
    { k: 'cleared',  t: 'Cleared',               cls: 'ok',   test: function (r) { return r.status === 'CLEARED'; } },
    { k: 'drift',    t: 'Owing again',           cls: 'bad',  test: function (r) { return r.owesSinceClearance; }, hideZero: true },
    { k: 'override', t: 'Overrides',             cls: 'warn', test: function (r) { return r.status === 'CLEARED' && r.basis === 'OVERRIDE'; }, hideZero: true },
    { k: 'pending',  t: 'Not yet decided',       cls: '',     test: function (r) { return r.status === 'PENDING'; }, hidden: true }
];
var STATUS = '';
function tabOf(k) { for (var i = 0; i < TABS.length; i++) if (TABS[i].k === k) return TABS[i]; return TABS[0]; }

/* ── search ── */
function terms() {
    var q = G.qs('fQ').value.trim().toLowerCase();
    return q ? q.split(/\s+/) : [];
}
function hay(r) {
    if (!r._h) r._h = (r.regno + ' ' + r.name + ' ' + r.progcode + ' ' + r.progname).toLowerCase();
    return r._h;
}
function hl(s, ts) {
    s = String(s == null ? '' : s);
    if (!ts.length) return G.esc(s);
    var low = s.toLowerCase(), marks = [];
    ts.forEach(function (t) {
        var i = low.indexOf(t);
        while (t && i >= 0) { marks.push([i, i + t.length]); i = low.indexOf(t, i + t.length); }
    });
    if (!marks.length) return G.esc(s);
    marks.sort(function (a, b) { return a[0] - b[0]; });
    var out = '', at = 0;
    marks.forEach(function (m) {
        if (m[0] < at) return;
        out += G.esc(s.substring(at, m[0])) + '<mark class="gf-hl">' + G.esc(s.substring(m[0], m[1])) + '</mark>';
        at = m[1];
    });
    return out + G.esc(s.substring(at));
}

/* ── sorting ── */
var STATUS_ORDER = { PENDING: 0, HELD: 1, CLEARED: 2 };
function cmp(a, b) {
    var x, y;
    switch (sortKey) {
        case 'name': x = (a.name || '').toLowerCase(); y = (b.name || '').toLowerCase(); break;
        case 'balance': x = +a.balance; y = +b.balance; break;
        case 'fee': x = !a.gradFeeRequired ? 2 : a.gradFeeBilled ? 1 : 0; y = !b.gradFeeRequired ? 2 : b.gradFeeBilled ? 1 : 0; break;
        case 'status':
            x = STATUS_ORDER[a.status] * 2 + (a.eligible ? 0 : 1); y = STATUS_ORDER[b.status] * 2 + (b.eligible ? 0 : 1);
            if (x === y) { x = a.decidedKey || ''; y = b.decidedKey || ''; }
            break;
        default: x = ''; y = '';
    }
    if (x < y) return -sortDir;
    if (x > y) return sortDir;
    return a._i - b._i;   // the server's order: programme, then name
}
function progKey(r) { return (r.progname || r.progcode || '').toLowerCase(); }

/* ── the view ── */
function rebuild() {
    var ts = terms(), tab = tabOf(STATUS);
    VIEW = ALL.filter(function (r) {
        if (!tab.test(r)) return false;
        for (var i = 0; i < ts.length; i++) if (hay(r).indexOf(ts[i]) < 0) return false;
        return true;
    });
    if (sortKey !== 'prog' || sortDir !== 1) VIEW.sort(cmp);
    else VIEW.sort(function (a, b) { return sortDir * (a._i - b._i); });
    if (grouped && sortKey !== 'prog')
        VIEW.sort(function (a, b) { var p = progKey(a), q = progKey(b); return p < q ? -1 : p > q ? 1 : cmp(a, b); });
}
function pages() { return size > 0 ? Math.max(1, Math.ceil(VIEW.length / size)) : 1; }
function pageRows() {
    if (page > pages()) page = pages();
    if (page < 1) page = 1;
    return size > 0 ? VIEW.slice((page - 1) * size, page * size) : VIEW;
}

function state() {
    return { year: G.qs('fYear').value, faculty: G.qs('fFac').value, dept: G.qs('fDep').value,
             prog: G.qs('fProg').value, status: STATUS, q: G.qs('fQ').value.trim(),
             sort: sortKey === 'prog' && sortDir === 1 ? '' : (sortDir < 0 ? '-' : '') + sortKey,
             page: page > 1 ? page : '' };
}
function cfg() {
    var s = state();
    return JSON.stringify({ acadYear: s.year, faculty: s.faculty, department: s.dept, programme: s.prog,
                            search: s.q, status: s.status, page: '1' });
}
function scopeOf() { return [G.qs('fYear').value, G.qs('fFac').value, G.qs('fDep').value, G.qs('fProg').value].join('|'); }

function txt(id) {
    var el = G.qs(id);
    return el.selectedIndex >= 0 ? el.options[el.selectedIndex].text.trim() : '';
}
function chips() {
    G.chips('gChips', [
        { k: 'fFac', label: 'Faculty', value: G.qs('fFac').value ? txt('fFac') : '' },
        { k: 'fDep', label: 'Department', value: G.qs('fDep').value ? txt('fDep') : '' },
        { k: 'fProg', label: 'Programme', value: G.qs('fProg').value ? txt('fProg') : '' },
        { k: 'status', label: 'Showing', value: STATUS ? tabOf(STATUS).t : '' },
        { k: 'fQ', label: 'Search', value: G.qs('fQ').value.trim() }
    ], function (k) {
        if (k === '*') { resetFilters(); G.cascade('fFac', 'fDep', 'fProg'); page = 1; scope(); return; }
        if (k === 'status') { STATUS = ''; page = 1; draw(); return; }
        if (k === 'fQ') { G.qs('fQ').value = ''; page = 1; draw(); return; }
        G.qs(k).value = ''; G.cascade('fFac', 'fDep', 'fProg'); page = 1; scope();
    });
}
function resetFilters() {
    ['fFac', 'fDep', 'fProg'].forEach(function (id) { G.qs(id).value = ''; });
    G.qs('fQ').value = ''; STATUS = '';
}

/* ── load the scope (the only server read for the list) ── */
function skeleton() {
    var h = '';
    for (var i = 0; i < 8; i++)
        h += '<tr class="gf-skel"><td></td><td><span style="width:' + (55 + (i * 7) % 30) + '%"></span></td><td><span style="width:70%"></span></td>' +
             '<td><span style="width:60%;margin-left:auto"></span></td><td><span style="width:50%"></span></td><td><span style="width:40%"></span></td>' +
             '<td><span style="width:55%"></span></td><td></td></tr>';
    G.qs('gBody').innerHTML = h;
    G.qs('gPager').innerHTML = '<span class="g-sub">Reading live balances&hellip;</span>';
}
function scope(force) {
    var key = scopeOf();
    if (!force && key === scopeKey && ALL.length) { draw(); return; }
    scopeKey = key;
    skeleton();
    var t0 = Date.now();
    G.ajax(PAGE, 'GetQueue', { configJson: cfg() }, function (d) {
        if (key !== scopeOf()) return;           // the filters moved on while this was loading
        if (!d || !d.success) {
            G.qs('gBody').innerHTML = '<tr><td colspan="8" class="g-empty">' + G.esc((d && d.message) || 'Could not load the list.') +
                ' <button type="button" class="g-btn g-btn--sm" id="gRetry">Try again</button></td></tr>';
            G.qs('gPager').innerHTML = '';
            if (G.qs('gRetry')) G.qs('gRetry').addEventListener('click', function () { scope(true); });
            return;
        }
        ALL = d.rows || []; BYREG = {};
        ALL.forEach(function (r, i) { r._i = i; BYREG[r.regno] = r; });
        for (var k in picked) if (picked.hasOwnProperty(k) && !BYREG[k]) delete picked[k];
        loadedAt = d.asOf || ''; loadMs = Date.now() - t0; DC = {};
        G.qs('gGateNote').innerHTML = (G.qs('fYear').value && !d.gated)
            ? '<div class="g-note g-note--info">Lists before ' + G.esc(String(BOOT.settings.firstGatedYear)) + '/' +
              (BOOT.settings.firstGatedYear + 1) + ' were compiled before fees clearance existed. Their names are shown for reference; the Graduation Fee rule and the document gate do not apply to them.</div>'
            : (d.capped ? '<div class="g-note g-note--warn">More than ' + d.cap + ' names in this scope; the first ' + d.cap + ' are shown. Choose a year or faculty to see the rest.</div>' : '');
        draw();
    });
}

/* ── draw everything from ALL ── */
function draw() {
    rebuild();
    G.writeUrl(state());
    chips(); summary(); tabs(); table(); bulkBar();
}

function summary() {
    var n = ALL.length, cl = 0, held = 0, ready = 0, owed = 0, owing = 0, credit = 0;
    ALL.forEach(function (r) {
        if (r.status === 'CLEARED') cl++; else if (r.status === 'HELD') held++; else if (r.eligible) ready++;
        if (r.balance > 0) { owing++; owed += +r.balance; } else if (r.balance < 0) credit += -r.balance;
    });
    var el = G.qs('gfSum');
    if (!n) { el.style.display = 'none'; return; }
    el.style.display = 'flex';
    function pct(x) { return n ? (x * 100 / n) : 0; }
    el.innerHTML =
        '<div class="gf-sum__prog"><b>' + cl + ' of ' + n + ' cleared</b><small>' + Math.round(pct(cl)) + '% · ' + ready + ' ready now · ' + held + ' on hold</small>' +
          '<div class="gf-bar" title="Cleared, ready to clear, on hold"><i class="b-ok" style="width:' + pct(cl) + '%"></i>' +
          '<i class="b-ready" style="width:' + pct(ready) + '%"></i><i class="b-held" style="width:' + pct(held) + '%"></i></div></div>' +
        '<div class="gf-sum__fig"><span>Owed</span><b class="gf-owe" title="UGX ' + money(owed) + '">UGX ' + short(owed) + '</b><small class="g-sub">' + owing + ' student' + (owing === 1 ? '' : 's') + '</small></div>' +
        '<div class="gf-sum__fig"><span>In credit</span><b class="gf-cr" title="UGX ' + money(credit) + '">UGX ' + short(credit) + '</b></div>' +
        '<div class="gf-sum__age">balances read ' + G.esc(loadedAt) + ' · ' + (loadMs / 1000).toFixed(1) + ' s</div>';
}

function tabs() {
    var h = '';
    TABS.forEach(function (t) {
        if (t.hidden && STATUS !== t.k) return;
        var c = 0;
        for (var i = 0; i < ALL.length; i++) if (t.test(ALL[i])) c++;
        if (t.hideZero && !c && STATUS !== t.k) return;
        h += '<button type="button" class="gf-qt' + (t.cls ? ' gf-qt--' + t.cls : '') + (STATUS === t.k ? ' is-on' : '') +
             (!c && t.k ? ' is-zero' : '') + '" data-st="' + t.k + '">' + G.esc(t.t) + '<em>' + c + '</em></button>';
    });
    G.qs('gfTabs').innerHTML = h;
}

function canPick(r) { return BOOT.canEdit && r.status !== 'CLEARED'; }

function rowHtml(r, ts) {
    var quick = '';
    if (BOOT.canEdit) {
        if (r.status !== 'CLEARED' && r.eligible)
            quick += '<button type="button" class="g-btn g-btn--sm g-btn--p" data-qclear="' + G.esc(r.regno) + '" title="Clear for graduation">Clear</button>';
        if (r.gradFeeRequired && !r.gradFeeBilled)
            quick += '<button type="button" class="g-btn g-btn--sm" data-qfee="' + G.esc(r.regno) + '" title="Bill the Graduation Fee">Bill fee</button>';
    }
    return '<tr class="is-click' + (r.regno === focusReg ? ' is-focus' : '') + (picked[r.regno] ? ' is-picked' : '') + '" data-reg="' + G.esc(r.regno) + '">' +
        '<td><input type="checkbox" class="ck" data-reg="' + G.esc(r.regno) + '"' + (canPick(r) ? '' : ' disabled') + (picked[r.regno] ? ' checked' : '') + ' /></td>' +
        '<td><div class="g-who"><img class="g-ph" loading="lazy" alt="" src="' + G.photo(r.regno) + '" />' +
            '<div class="g-who__t"><b>' + hl(r.regno, ts) + '</b><span>' + hl(r.name, ts) + '</span></div></div></td>' +
        '<td>' + hl(r.progname || r.progcode, ts) + '<div class="g-sub">' + hl(r.progcode, ts) + ' · ' + G.esc(r.acadyear) + '</div></td>' +
        '<td class="g-num">' + balCell(r.balance) + '</td>' +
        '<td>' + (!r.gradFeeRequired ? '<span class="g-sub">not required</span>'
                  : r.gradFeeBilled ? '<span class="gf-cr" style="font-weight:700;">Billed</span>'
                  : '<span class="gf-owe" style="font-weight:700;">Not billed</span>' +
                    (r.suggestedFee ? '<span class="gf-feesub">' + money(r.suggestedFee) + ' \u00b7 ' + G.esc(catLabel(r.feeCategory)) + '</span>'
                                    : '<span class="gf-feesub">no category: choose the fee</span>')) + '</td>' +
        '<td>' + flags(r) + '</td>' +
        '<td>' + chipFor(r) + (r.actor ? '<div class="g-sub">' + G.esc(r.actor) + ' · ' + G.esc(r.decidedAt) + '</div>' : '') + '</td>' +
        '<td class="gf-act-cell">' + quick + '<button type="button" class="g-btn g-btn--sm" data-open="' + G.esc(r.regno) + '">Open</button></td></tr>';
}

function table() {
    var rows = pageRows(), ts = terms(), h = '', lastG = null;
    var gstat = {};
    if (grouped) VIEW.forEach(function (r) {
        var k = progKey(r), g = gstat[k] || (gstat[k] = { n: 0, owed: 0, cl: 0 });
        g.n++; if (r.balance > 0) g.owed += +r.balance; if (r.status === 'CLEARED') g.cl++;
    });
    rows.forEach(function (r) {
        if (grouped) {
            var k = progKey(r);
            if (k !== lastG) {
                lastG = k; var g = gstat[k];
                h += '<tr class="gf-grp"><td colspan="8">' + G.esc(r.progname || r.progcode) + ' <span class="g-sub">(' + G.esc(r.progcode) + ')</span>' +
                     '<small>' + g.n + ' graduand' + (g.n === 1 ? '' : 's') + ' · ' + g.cl + ' cleared' + (g.owed ? ' · owes UGX ' + money(g.owed) : '') + '</small></td></tr>';
            }
        }
        h += rowHtml(r, ts);
    });
    if (!h) {
        var why = ALL.length ? 'Nobody matches.' : 'Nobody is on this graduation list yet.';
        h = '<tr><td colspan="8" class="g-empty">' + why +
            (ALL.length && (STATUS || ts.length) ? ' <button type="button" class="g-btn g-btn--sm" id="gShowAll">Show everyone</button>' : '') + '</td></tr>';
    }
    G.qs('gBody').innerHTML = h;
    if (G.qs('gShowAll')) G.qs('gShowAll').addEventListener('click', function () { STATUS = ''; G.qs('fQ').value = ''; page = 1; draw(); });
    G.qs('gMeta').textContent = VIEW.length === ALL.length ? ALL.length + ' graduand' + (ALL.length === 1 ? '' : 's')
                                                           : VIEW.length + ' of ' + ALL.length + ' graduands';
    var ths = document.querySelectorAll('.gf-list th.gf-sort');
    for (var i = 0; i < ths.length; i++) {
        var k = ths[i].getAttribute('data-sort'), base = ths[i].getAttribute('data-label') || ths[i].textContent;
        ths[i].setAttribute('data-label', base);
        ths[i].innerHTML = G.esc(base) + (sortKey === k ? '<i>' + (sortDir > 0 ? '&#9650;' : '&#9660;') + '</i>' : '');
    }
    var pickable = rows.filter(canPick);
    G.qs('ckAll').checked = pickable.length > 0 && pickable.every(function (r) { return picked[r.regno]; });
    G.qs('ckAll').disabled = !pickable.length;
    pager();
}

function pager() {
    var el = G.qs('gPager'), n = VIEW.length, ps = pages();
    if (!n) { el.innerHTML = ''; return; }
    var from = size > 0 ? (page - 1) * size + 1 : 1, to = size > 0 ? Math.min(n, page * size) : n;
    var h = '<span class="gf-pg__n"><b>' + from + '&ndash;' + to + '</b> of <b>' + n + '</b></span>';
    if (ps > 1) {
        h += '<span class="gf-pg__b">' + pb(page - 1, '&lsaquo; Previous', page <= 1);
        var list = [], i;
        for (i = 1; i <= ps; i++) if (i === 1 || i === ps || Math.abs(i - page) <= 1) list.push(i);
        var prev = 0;
        list.forEach(function (p) {
            if (p - prev > 1) h += '<span class="gf-pg__gap">&hellip;</span>';
            h += '<button type="button" class="g-btn g-btn--sm' + (p === page ? ' is-on' : '') + '" data-pg="' + p + '">' + p + '</button>';
            prev = p;
        });
        h += pb(page + 1, 'Next &rsaquo;', page >= ps) + '</span>';
    }
    el.innerHTML = h;
}
function pb(p, label, off) { return '<button type="button" class="g-btn g-btn--sm" data-pg="' + p + '"' + (off ? ' disabled' : '') + '>' + label + '</button>'; }
function go(p) { if (p < 1 || p > pages() || p === page) return; page = p; focusReg = null; draw(); scrollTop(); }
function scrollTop() { var c = G.qs('gBody'); if (c && c.getBoundingClientRect().top < 0) c.closest('.g-card').scrollIntoView({ block: 'start' }); }

/* ── patching: only the touched students are read again ── */
function refreshRows(ids, after) {
    ids = (ids || []).filter(function (x) { return !!x; });
    if (!ids.length) { if (after) after(); return; }
    ids.forEach(function (x) { delete DC[x]; });
    G.ajax(PAGE, 'GetRows', { regnos: ids }, function (d) {
        if (d && d.success) {
            var back = {};
            (d.rows || []).forEach(function (r) { back[r.regno] = r; });
            ids.forEach(function (id) {
                var old = BYREG[id], nw = back[id];
                if (old && nw) { nw._i = old._i; ALL[old._i] = nw; BYREG[id] = nw; nw._fresh = true; }
                else if (old && !nw) { ALL.splice(old._i, 1); ALL.forEach(function (r, i) { r._i = i; }); delete BYREG[id]; }
                if (BYREG[id] && !canPick(BYREG[id])) delete picked[id];
            });
            draw();
            ids.forEach(function (id) {
                var tr = document.querySelector('#gBody tr[data-reg="' + cssq(id) + '"]');
                if (tr) tr.classList.add('is-fresh');
            });
        }
        if (after) after();
    });
}
function cssq(s) { return window.CSS && CSS.escape ? CSS.escape(s) : String(s).replace(/"/g, '\\"'); }

/* ── quick actions from the row ── */
function quickClear(reg) {
    var r = BYREG[reg]; if (!r) return;
    if (!confirm('Clear ' + (r.name || reg) + ' (' + reg + ') for graduation?\n\nBalance: ' + (r.balance > 0 ? 'UGX ' + money(r.balance) + ' owed' : 'nothing owed'))) return;
    G.ajax(PAGE, 'Decide', { regno: reg, verdict: 'CLEARED', reason: '', asOverride: false }, function (d) {
        G.toast((d && d.message) || 'Nothing was saved.', !!(d && d.success));
        refreshRows([reg]);
    });
}
function quickFee(reg) {
    var r = BYREG[reg]; if (!r) return;
    if (!r.suggestedFee) { TAB = 'bill'; open(reg); return; }     // no category: pick the fee in the workspace
    if (!confirm('Bill the Graduation Fee of UGX ' + money(r.suggestedFee) + ' (' + catLabel(r.feeCategory) + ') to ' +
                 (r.name || reg) + ' for the ' + r.acadyear + ' graduation?')) return;
    G.ajax(PAGE, 'BillGradFee', { regno: reg, amount: r.suggestedFee }, function (d) {
        G.toast((d && d.message) || 'Nothing was posted.', !!(d && d.success));
        refreshRows([reg]);
    });
}

/* ── selection and bulk ── */
function pickedIds() { var a = []; for (var k in picked) if (picked.hasOwnProperty(k) && picked[k]) a.push(k); return a; }
function bulkBar() {
    var bar = G.qs('gfBulk'), ids = pickedIds(), n = ids.length;
    if (!n || !BOOT.canEdit) { bar.className = 'gf-bulk'; bar.innerHTML = ''; return; }
    var pickableInView = VIEW.filter(canPick).length;
    var settledSel = ids.filter(function (x) { return BYREG[x] && BYREG[x].eligible; }).length;
    var feeSel = ids.filter(function (x) { return BYREG[x] && BYREG[x].gradFeeRequired && !BYREG[x].gradFeeBilled; }).length;
    bar.className = 'gf-bulk is-on';
    bar.innerHTML = '<b>' + n + ' selected</b>' +
        (pickableInView > n ? '<a id="bAll">Select all ' + pickableInView + ' in this view</a>' : '') +
        '<button type="button" class="g-btn g-btn--sm g-btn--p" id="bClear"' + (settledSel ? '' : ' disabled') + '>Clear ' + settledSel + ' who owe nothing</button>' +
        (BOOT.settings.requireGradFee
            ? '<span style="display:inline-flex;align-items:center;gap:6px;flex-wrap:wrap;"><button type="button" class="g-btn g-btn--sm" id="bFee"' + (feeSel ? '' : ' disabled') + '>Bill Graduation Fee to ' + feeSel + '</button>' +
              '<input type="text" id="bFeeAmt" inputmode="numeric" value="" placeholder="by category" title="Leave empty to bill each student their category\u2019s fee, or type one amount for all" />' +
              feeChips('bFeeAmt', '', '<button type="button" class="gf-fee' + (bulkMode === 'category' ? ' is-on' : '') + '" data-fee="0" title="Each student is billed their own category\u2019s fee">By category</button>') +
              '</span>' : '') +
        '<button type="button" class="g-btn g-btn--sm gf-bulk__x" id="bNone">Deselect</button>';
    if (G.qs('bAll')) G.qs('bAll').addEventListener('click', function () { VIEW.forEach(function (r) { if (canPick(r)) picked[r.regno] = true; }); draw(); });
    G.qs('bNone').addEventListener('click', function () { picked = {}; draw(); });
    G.qs('bClear').addEventListener('click', function () {
        var go = ids.filter(function (x) { return BYREG[x] && BYREG[x].eligible; });
        if (!confirm('Clear ' + go.length + ' student(s) who owe nothing?\n\nThe ' + (n - go.length) + ' others selected are left as they are. Every one is re-checked live; anyone found owing is skipped, never overridden.')) return;
        bulk('BulkClear', { regnos: go }, 'cleared');
    });
    if (G.qs('bFee')) G.qs('bFee').addEventListener('click', function () {
        var a = parseInt(String(G.qs('bFeeAmt').value).replace(/[^0-9]/g, ''), 10) || 0;
        var go = ids.filter(function (x) { return BYREG[x] && BYREG[x].gradFeeRequired && !BYREG[x].gradFeeBilled; });
        var msg;
        if (a > 0) {
            msg = 'Post a Graduation Fee of UGX ' + money(a) + ' to each of ' + go.length + ' student(s)?\nThat is UGX ' + money(a * go.length) + ' in total.';
        } else {
            // by category: say exactly what each group is billed
            var by = {}, none = 0, total = 0;
            go.forEach(function (x) {
                var r = BYREG[x];
                if (!r.suggestedFee) { none++; return; }
                var k = r.feeCategory; by[k] = by[k] || { n: 0, amt: r.suggestedFee }; by[k].n++; total += +r.suggestedFee;
            });
            if (none) { G.toast(none + ' selected student(s) have no fee category (advanced certificates). Untick them or type one amount for all.', false); return; }
            msg = 'Bill the Graduation Fee by category to ' + go.length + ' student(s)?\n\n' +
                  Object.keys(by).map(function (k) { return '  ' + catLabel(k) + ': ' + by[k].n + ' x UGX ' + money(by[k].amt); }).join('\n') +
                  '\n\nTotal UGX ' + money(total) + '.';
        }
        if (!confirm(msg + '\nEach bill goes to the student ledger and the general ledger. Anyone already billed is skipped.')) return;
        bulk('BulkGradFee', { regnos: go, amount: a }, 'billed');
    });
}
function bulk(method, args, verb) {
    if (!args.regnos.length) return;
    G.toast('Working on ' + args.regnos.length + ' student(s)…', true);
    var btns = document.querySelectorAll('#gfBulk button'); for (var i = 0; i < btns.length; i++) btns[i].disabled = true;
    G.ajax(PAGE, method, args, function (d) {
        if (!d || !d.success) { G.toast((d && d.message) || 'That did not go through.', false); bulkBar(); return; }
        args.regnos.forEach(function (x) { delete picked[x]; });
        var sk = d.skipped || [];
        var h = '<div class="gf-res"><button type="button" class="gf-res__x" title="Dismiss">&times;</button><b>' + d.done + ' ' + verb + '</b>' +
                (sk.length ? ', ' + sk.length + ' skipped:' : '.') + ' <span class="g-sub">batch ' + G.esc(d.batch || '') + '</span>';
        if (sk.length) h += '<ul>' + sk.map(function (s) { return '<li><b>' + G.esc(s.regno) + '</b> ' + G.esc(s.why) + '</li>'; }).join('') + '</ul>';
        G.qs('gfResult').innerHTML = h + '</div>';
        G.toast(d.done + ' ' + verb + (sk.length ? ', ' + sk.length + ' skipped.' : '.'), d.done > 0);
        refreshRows(args.regnos);
    });
}

/* ── the student workspace ── */
function viewIndex(reg) { for (var i = 0; i < VIEW.length; i++) if (VIEW[i].regno === reg) return i; return -1; }
function neighbour(reg, step) {
    var i = viewIndex(reg);
    if (i < 0) return null;
    var j = i + step;
    return j >= 0 && j < VIEW.length ? VIEW[j].regno : null;
}
function fetchDetail(reg, cb) {
    var c = DC[reg];
    if (c && Date.now() - c.t < DC_TTL) { if (cb) cb(c.d); return; }
    if (c && c.waiting) { if (cb) c.waiting.push(cb); return; }
    DC[reg] = { t: 0, waiting: cb ? [cb] : [] };
    G.ajax(PAGE, 'GetStudent', { regno: reg }, function (d) {
        var w = DC[reg] ? DC[reg].waiting : [];
        if (d && d.success) DC[reg] = { d: d, t: Date.now() }; else delete DC[reg];
        (w || []).forEach(function (f) { f(d); });
    });
}
function prefetch(reg) {
    var n = neighbour(reg, 1);
    if (n) setTimeout(function () { if (CUR) fetchDetail(n, null); }, 350);
}
function open(reg) {
    CUR = null; focusReg = reg;
    var r = BYREG[reg];
    G.qs('gModalName').textContent = r ? (r.name || reg) : reg;
    G.qs('gModalSub').textContent = reg + '  ·  reading the account…';
    G.showPhoto(true);
    G.qs('gModalPhoto').src = G.photo(reg, 144);
    G.qs('gModalBody').innerHTML = '<div class="g-load">Loading the ledger…</div>';
    G.qs('gModalFoot').innerHTML = '';
    G.openModal();
    fetchDetail(reg, function (d) {
        if (focusReg !== reg) return;          // moved on to another student meanwhile
        if (!d || !d.success) {
            G.qs('gModalBody').innerHTML = '<div class="g-note g-note--bad">' + G.esc((d && d.message) || 'Could not load that student.') + '</div>';
            footerNav(reg);
            return;
        }
        CUR = d; render(); prefetch(reg);
    });
}

function render() {
    var d = CUR, s = d.student, t = d.totals, id = d.ident || {};
    G.qs('gModalName').textContent = s.name || s.regno;
    var vi = viewIndex(s.regno);
    G.qs('gModalSub').textContent = s.regno + (id.entryno ? '  ·  ' + id.entryno : '') + '  ·  ' + (s.progname || s.progcode);

    var nb = 0, nw = 0, i;
    for (i = 0; i < s.findings.length; i++) { if (s.findings[i].level === 'BLOCK') nb++; else if (s.findings[i].level === 'WARN') nw++; }
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
        var fix = '';
        if (d.rights.edit && f.code === 'NO_GRAD_FEE') fix = ' <button type="button" class="g-btn g-btn--sm" data-goto="bill">Bill it now</button>';
        else if (d.rights.edit && f.code === 'OWES') fix = ' <button type="button" class="g-btn g-btn--sm" data-goto="pay">Record a payment</button>';
        else if (f.code === 'UNBILLED' || f.code === 'REVERSED') fix = ' <button type="button" class="g-btn g-btn--sm" data-goto="sems">See semesters</button>';
        ov += '<div class="g-find g-find--' + lv + '"><div class="g-find__n">' +
              (f.level === 'BLOCK' ? 'Blocks clearance' : f.level === 'WARN' ? 'Check before clearing' : 'For information') +
              '</div><div class="g-find__d">' + G.esc(f.text) + fix + '</div></div>';
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
              '<input type="text" id="qFee" value="' + (o.feeCategory ? money(o.gradFeeAmount) : '') + '" placeholder="choose below" style="width:110px;border:1px solid var(--line);padding:5px 7px;" />' +
              '<button type="button" class="g-btn g-btn--p g-btn--sm" id="qFeeGo">Bill the Graduation Fee</button>' +
              '<div style="flex-basis:100%;">' + feeChips('qFee', o.feeCategory) + '</div>' +
              (o.feeCategory ? '' : '<div class="g-sub" style="flex-basis:100%;">This programme is not in the fee schedule. Choose the category that applies.</div>') + '</div>';
    if (canEdit) {
        var itemOpts = o.items.map(function (x) { return '<option value="' + G.esc(x.v) + '"' + (+x.v === +o.gradFeeItem ? ' selected' : '') + '>' + G.esc(x.t) + '</option>'; }).join('');
        bl += '<div class="gf-form">' +
              '<div><label for="bItem">Item</label><select id="bItem">' + itemOpts + '</select></div>' +
              '<div><label for="bAmt">Amount (UGX)</label><input id="bAmt" type="text" inputmode="numeric" placeholder="e.g. 555,000" /></div>' +
              '<div class="gf-wide" id="bFeeHelp"' + (+o.items[0] === +o.gradFeeItem ? '' : '') + '>' + feeChips('bAmt', o.feeCategory) + '</div>' +
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
        var owe = t.balance > 0 ? money(t.balance) : '';
        py = '<div class="g-note g-note--info">For money paid at the bank or in cash that is not already on the account. SchoolPay payments arrive by themselves; if one is missing, use the SchoolPay controller instead so it is not recorded twice.</div>' +
             '<div class="gf-form">' +
             '<div><label for="pBank">Paid into</label><select id="pBank">' + bankOpts + '</select></div>' +
             '<div><label for="pAmt">Amount (UGX)</label><input id="pAmt" type="text" inputmode="numeric" value="' + owe + '" placeholder="' + (owe ? 'balance ' + owe : '') + '" /></div>' +
             '<div><label for="pDate">Date paid</label><input id="pDate" type="date" max="' + today() + '" value="' + today() + '" /></div>' +
             '<div><label for="pRef">Receipt / bank slip number</label><input id="pRef" type="text" maxlength="60" /></div>' +
             '<div class="gf-wide"><label for="pPayer">Paid by (optional)</label><input id="pPayer" type="text" maxlength="80" placeholder="e.g. the sponsor or parent named on the slip" /></div>' +
             '</div><div class="gf-act"><button type="button" class="g-btn g-btn--p" id="pGo">Record the payment</button>' +
             '<span class="g-sub">The amount starts at the balance owed; change it to what the slip says. The receipt number must be unique.</span></div>';
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
    var gt = G.qs('gModalBody').querySelectorAll('[data-goto]');
    for (i = 0; i < gt.length; i++) gt[i].addEventListener('click', function () { TAB = this.getAttribute('data-goto'); render(); });
    wireMoney();
    footer(vi);
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
        busy(this);
        G.ajax(PAGE, 'BillGradFee', { regno: s.regno, amount: a }, after);
    });
    var bh = G.qs('bFeeHelp');
    if (bh) bh.addEventListener('click', function (e) {
        if (e.target.closest && e.target.closest('.gf-fee') && G.qs('bItem')) G.qs('bItem').value = String(CUR.options.gradFeeItem);
    });
    if (G.qs('bGo')) G.qs('bGo').addEventListener('click', function () {
        var a = num('bAmt'), it = G.qs('bItem');
        if (!a) { G.toast('Enter the amount.', false); return; }
        if (!confirm('Bill ' + it.options[it.selectedIndex].text + ' of UGX ' + money(a) + ' to ' + s.name + '?')) return;
        busy(this);
        G.ajax(PAGE, 'BillItem', { regno: s.regno, itemCode: +it.value, amount: a, acadYear: G.qs('bYear').value,
                                   semester: +G.qs('bSem').value, detail: G.qs('bDet').value.trim() }, after);
    });
    if (G.qs('pGo')) G.qs('pGo').addEventListener('click', function () { pay(false); });
}
function busy(btn) { if (btn) { btn.disabled = true; btn.textContent = 'Posting…'; } }
function pay(confirmSameDay) {
    var s = CUR.student, a = num('pAmt'), bank = G.qs('pBank').value, ref = G.qs('pRef').value.trim(), dt = G.qs('pDate').value;
    if (!bank) { G.toast('Choose the account the money was paid into.', false); return; }
    if (!a) { G.toast('Enter the amount.', false); return; }
    if (ref.length < 3) { G.toast('Enter the receipt or bank slip number.', false); return; }
    if (!confirmSameDay && !confirm('Record UGX ' + money(a) + ' paid on ' + dt + ' into ' + bank + ' (ref ' + ref + ') for ' + s.name + '?')) return;
    busy(G.qs('pGo'));
    G.ajax(PAGE, 'RecordPayment', { regno: s.regno, bankCode: bank, amount: a, payDate: dt, reference: ref,
                                    payer: G.qs('pPayer').value.trim(), confirmSameDay: confirmSameDay }, function (d) {
        if (d && d.sameDay) { if (confirm(d.message + '\n\nRecord it anyway?')) pay(true); else render(); return; }
        after(d);
    });
}
function after(d) {
    var reg = CUR && CUR.student.regno;
    if (d && d.success) {
        G.toast(d.message, true); TAB = 'overview';
        refreshRows([reg], function () { if (reg && G.qs('gOv').classList.contains('is-open')) open(reg); });
    } else { G.toast((d && d.message) || 'Nothing was posted.', false); if (CUR) render(); }
}

function footerNav(reg) {
    var foot = G.qs('gModalFoot'), vi = viewIndex(reg);
    var h = foot.innerHTML.indexOf('mPrev') >= 0 ? '' :
        '<span class="gf-sp"></span>' + (vi >= 0 ? '<span class="gf-pos">' + (vi + 1) + ' of ' + VIEW.length + '</span>' : '') +
        '<button type="button" class="g-btn g-btn--sm" id="mPrev" title="Previous (&larr;)">&lsaquo; Previous</button>' +
        '<button type="button" class="g-btn g-btn--sm" id="mNext" title="Next (&rarr;)">Next &rsaquo;</button>';
    foot.innerHTML += h;
    var pv = neighbour(reg, -1), nx = neighbour(reg, 1);
    G.qs('mPrev').disabled = !pv; G.qs('mNext').disabled = !nx;
    G.qs('mPrev').addEventListener('click', function () { if (pv) open(pv); });
    G.qs('mNext').addEventListener('click', function () { if (nx) open(nx); });
}
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
    G.qs('gModalFoot').innerHTML = h;
    footerNav(s.regno);
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
}
function decide(verdict, reason, asOverride) {
    var s = CUR.student, reg = s.regno;
    var nx = neighbour(reg, 1);                 // decided before the row can leave the view
    var b = document.querySelectorAll('#gModalFoot button'); for (var i = 0; i < b.length; i++) b[i].disabled = true;
    G.ajax(PAGE, 'Decide', { regno: reg, verdict: verdict, reason: reason || '', asOverride: !!asOverride }, function (d) {
        if (!(d && d.success)) { G.toast((d && d.message) || 'Nothing was saved.', false); render(); return; }
        G.toast(d.message, true);
        refreshRows([reg], function () {
            if (!G.qs('gOv').classList.contains('is-open')) return;
            if (verdict !== 'REVOKED' && nx && G.autoAdvance()) { TAB = 'overview'; open(nx); } else open(reg);
        });
    });
}

/* ── export ── */
function openExport() {
    G.exportDialog({
        page: PAGE, title: 'Export fees clearance', subtitle: 'Live balances and the finance decision for each graduand.',
        cfg: cfg(), columns: window.G_COLS || [], pageRows: pageRows().length, countMethod: 'CountExport',
        filters: { focus: false, years: (BOOT && BOOT.years) || [], faculties: (BOOT && BOOT.faculties) || [],
                   departments: (BOOT && BOOT.departments) || [], programmes: (BOOT && BOOT.programmes) || [],
                   current: { acadYear: G.qs('fYear').value, faculty: G.qs('fFac').value,
                              department: G.qs('fDep').value, programme: G.qs('fProg').value } },
        filterSummary: [ { label: 'Graduation year', value: G.qs('fYear').value || 'All years' },
                         { label: 'Showing', value: tabOf(STATUS).t } ].concat(G.qs('fQ').value.trim() ? [{ label: 'Search', value: G.qs('fQ').value.trim() }] : []),
        sorts: [ { k: 'name', t: 'Programme, then name' } ], sortDefault: 'name',
        groups: [ { k: 'prog', t: 'Programme' } ], groupDefault: 'prog',
        sheets: [ { k: 'summary', t: 'Summary by programme', d: 'Cleared, pending, held and owed per programme', on: true } ]
    });
}

/* ── keyboard ── */
function typing(e) {
    var t = e.target, n = t && t.tagName;
    return n === 'INPUT' || n === 'TEXTAREA' || n === 'SELECT' || (t && t.isContentEditable);
}
function moveFocus(step) {
    var rows = pageRows(); if (!rows.length) return;
    var i = -1; for (var k = 0; k < rows.length; k++) if (rows[k].regno === focusReg) { i = k; break; }
    var j = i < 0 ? (step > 0 ? 0 : rows.length - 1) : i + step;
    if (j >= rows.length) { if (page < pages()) { page++; focusReg = pageRows()[0].regno; draw(); } return; }
    if (j < 0) { if (page > 1) { page--; var pr = pageRows(); focusReg = pr[pr.length - 1].regno; draw(); } return; }
    focusReg = rows[j].regno;
    var all = document.querySelectorAll('#gBody tr[data-reg]');
    for (k = 0; k < all.length; k++) all[k].classList.toggle('is-focus', all[k].getAttribute('data-reg') === focusReg);
    var tr = document.querySelector('#gBody tr[data-reg="' + cssq(focusReg) + '"]');
    if (tr && tr.scrollIntoView) tr.scrollIntoView({ block: 'nearest' });
}
function onKey(e) {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    var modalOpen = G.qs('gOv') && G.qs('gOv').classList.contains('is-open');
    var anyOverlay = document.querySelector('.g-ov.is-open');
    if (modalOpen) {
        if (typing(e) || anyOverlay !== G.qs('gOv')) return;
        if (e.key === 'ArrowRight' && G.qs('mNext') && !G.qs('mNext').disabled) { e.preventDefault(); G.qs('mNext').click(); }
        else if (e.key === 'ArrowLeft' && G.qs('mPrev') && !G.qs('mPrev').disabled) { e.preventDefault(); G.qs('mPrev').click(); }
        return;
    }
    if (anyOverlay) return;
    if (e.key === '/' && !typing(e)) { e.preventDefault(); G.qs('fQ').focus(); G.qs('fQ').select(); return; }
    if (typing(e)) {
        if (e.target.id === 'fQ' && (e.key === 'ArrowDown' || e.key === 'Enter')) { e.preventDefault(); G.qs('fQ').blur(); focusReg = null; moveFocus(1); }
        if (e.target.id === 'fQ' && e.key === 'Escape') { G.qs('fQ').value = ''; page = 1; draw(); }
        return;
    }
    switch (e.key) {
        case 'ArrowDown': case 'j': e.preventDefault(); moveFocus(1); break;
        case 'ArrowUp': case 'k': e.preventDefault(); moveFocus(-1); break;
        case 'Enter': if (focusReg) { e.preventDefault(); open(focusReg); } break;
        case 'x': case ' ':
            if (focusReg && BYREG[focusReg] && canPick(BYREG[focusReg])) { e.preventDefault(); picked[focusReg] = !picked[focusReg]; if (!picked[focusReg]) delete picked[focusReg]; draw(); }
            break;
        case 'ArrowRight': go(page + 1); break;
        case 'ArrowLeft': go(page - 1); break;
    }
}

/* ── wiring ── */
document.addEventListener('DOMContentLoaded', function () {
    G.mount();
    G.wireModal(function () { CUR = null; draw(); });
    var pre = G.readUrl();
    size = parseInt(store('size') || '50', 10); if (!(size >= 0)) size = 50;
    grouped = store('group') === '1';
    G.qs('optSize').value = String(size); G.qs('optGroup').checked = grouped;

    document.addEventListener('click', function (e) {
        var t = e.target, c = t.closest ? t : null;
        if (!c) return;
        var ck = t.closest('#gBody .ck');
        if (ck) { e.stopPropagation(); var r = ck.getAttribute('data-reg'); if (ck.checked) picked[r] = true; else delete picked[r];
                  var tr0 = ck.closest('tr'); if (tr0) tr0.classList.toggle('is-picked', ck.checked); bulkBar(); return; }
        var q = t.closest('#gBody [data-qclear]'); if (q) { e.stopPropagation(); quickClear(q.getAttribute('data-qclear')); return; }
        q = t.closest('#gBody [data-qfee]'); if (q) { e.stopPropagation(); quickFee(q.getAttribute('data-qfee')); return; }
        var b = t.closest('#gBody [data-open]'); if (b) { e.stopPropagation(); open(b.getAttribute('data-open')); return; }
        var tr = t.closest('#gBody tr[data-reg]'); if (tr && t.tagName !== 'INPUT') { open(tr.getAttribute('data-reg')); return; }
        var st = t.closest('#gfTabs [data-st]'); if (st) { STATUS = st.getAttribute('data-st'); page = 1; focusReg = null; draw(); return; }
        var pg = t.closest('#gPager [data-pg]'); if (pg && !pg.disabled) { go(+pg.getAttribute('data-pg')); return; }
        var th = t.closest('.gf-list th.gf-sort');
        if (th) { var k = th.getAttribute('data-sort');
                  if (sortKey === k) sortDir = -sortDir; else { sortKey = k; sortDir = k === 'balance' ? -1 : 1; }
                  page = 1; draw(); return; }
        var x = t.closest('.gf-res__x'); if (x) { G.qs('gfResult').innerHTML = ''; return; }
    });
    document.addEventListener('keydown', onKey);
    G.qs('ckAll').addEventListener('change', function () {
        var on = G.qs('ckAll').checked;
        pageRows().forEach(function (r) { if (canPick(r)) { if (on) picked[r.regno] = true; else delete picked[r.regno]; } });
        draw();
    });
    G.qs('optSize').addEventListener('change', function () { size = parseInt(this.value, 10) || 0; store('size', String(size)); page = 1; draw(); });
    G.qs('optGroup').addEventListener('change', function () { grouped = this.checked; store('group', grouped ? '1' : '0'); draw(); });
    ['fYear', 'fProg'].forEach(function (id) { G.qs(id).addEventListener('change', function () { page = 1; picked = {}; scope(); }); });
    G.qs('fFac').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; picked = {}; scope(); });
    G.qs('fDep').addEventListener('change', function () { G.cascade('fFac', 'fDep', 'fProg'); page = 1; picked = {}; scope(); });
    var typed = G.debounce(function () { page = 1; focusReg = null; draw(); }, 120);
    G.qs('fQ').addEventListener('input', typed);
    G.qs('btnXls').addEventListener('click', openExport);
    G.qs('btnRefresh').addEventListener('click', function () { scope(true); });
    G.qs('btnReset').addEventListener('click', function () {
        resetFilters(); if (BOOT && BOOT.currentYear) G.qs('fYear').value = BOOT.currentYear;
        sortKey = 'prog'; sortDir = 1; G.cascade('fFac', 'fDep', 'fProg'); page = 1; picked = {}; G.qs('gfResult').innerHTML = ''; scope();
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
        if (!o.canEdit) G.qs('ckAll').style.visibility = 'hidden';
        G.fill('fYear', o.years.map(function (y) { return { v: y, t: y }; }), null);
        G.fill('fFac', o.faculties, 'All faculties');
        G.fill('fDep', o.departments, 'All departments');
        G.fill('fProg', o.programmes, 'All programmes');
        G.qs('fYear').value = pre.year || o.currentYear || '';
        if (pre.faculty) G.qs('fFac').value = pre.faculty;
        if (pre.dept) G.qs('fDep').value = pre.dept;
        if (pre.prog) G.qs('fProg').value = pre.prog;
        if (pre.q) G.qs('fQ').value = pre.q;
        if (pre.status) STATUS = tabOf(pre.status).k;
        if (pre.sort) { sortDir = pre.sort.charAt(0) === '-' ? -1 : 1; sortKey = pre.sort.replace(/^-/, ''); }
        page = parseInt(pre.page || '1', 10) || 1;
        G.cascade('fFac', 'fDep', 'fProg');
        G.combo('fProg', 'Type a code or part of the name…', { key: true });
        scope();
    }
    if (window.G_BOOT) applyBoot(window.G_BOOT); else G.ajax(PAGE, 'GetBootstrap', {}, applyBoot);
});
})();

</script>
</asp:Content>
