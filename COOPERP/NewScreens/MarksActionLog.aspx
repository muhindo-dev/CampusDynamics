<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="MarksActionLog.aspx.cs" Inherits="COOPERP_NewScreens_MarksActionLog" Title="Admin Action Log - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===================================================================
   Admin Action Log  --  house design system (navy, flat, compact).
   Prefix: mal-      Spec: NewScreens/DESIGN_SYSTEM.md
   =================================================================== */

/* --- page header ------------------------------------------------- */
.mal-head{display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;
          background:#05275C;color:#fff;padding:14px 20px;border-bottom:3px solid #041d45}
.mal-head__l{display:flex;align-items:center;gap:12px;min-width:0}
.mal-head__ic{width:40px;height:40px;background:rgba(255,255,255,.12);border-radius:4px;
              display:flex;align-items:center;justify-content:center;flex-shrink:0}
.mal-head__t{font-size:16px;font-weight:700;line-height:1.2}
.mal-head__s{font-size:12px;opacity:.75;margin-top:2px;max-width:640px;line-height:1.45}
.mal-head__r{display:flex;align-items:center;gap:8px;flex-wrap:wrap}

.mal-body{padding:14px 20px 24px}

/* --- notices ----------------------------------------------------- */
.mal-note{padding:10px 14px;font-size:11.5px;line-height:1.55;margin-bottom:12px;border-radius:2px}
.mal-note--info{background:#eef3fb;border:1px solid #cfdcf2;color:#173f79}
.mal-note--warn{background:#fff8e1;border:1px solid #f4dba6;color:#8a5a08}
.mal-note--error{background:#fef5f5;border:1px solid #f5c6cb;color:#912018}
.mal-note b{font-weight:600}

/* --- KPI cards --------------------------------------------------- */
.mal-stats{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin-bottom:14px}
.mal-stat{background:#fff;border:1px solid #e0e5ed;border-radius:4px;padding:13px 15px;min-width:0}
.mal-stat__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;margin-bottom:5px}
.mal-stat__value{font-size:22px;font-weight:700;color:#05275C;line-height:1;font-variant-numeric:tabular-nums}
.mal-stat__value--warn{color:#b45309}
.mal-stat__sub{font-size:11px;color:#888;margin-top:5px;line-height:1.4}

/* --- quick ranges ------------------------------------------------ */
.mal-quick{display:flex;gap:6px;flex-wrap:wrap;align-items:center;margin-bottom:12px}
.mal-quick__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;margin-right:2px}
.mal-pill{padding:5px 11px;font-size:11px;font-weight:500;background:#fff;border:1px solid #e0e5ed;
          color:#555;border-radius:0;cursor:pointer;text-decoration:none;font-family:inherit}
.mal-pill:hover{background:#f5f7fa;color:#05275C;text-decoration:none}
.mal-pill--active{background:#05275C;color:#fff;border-color:#05275C}
.mal-pill--active:hover{background:#041d45;color:#fff}

/* --- filter bar -------------------------------------------------- */
.mal-filters{display:flex;flex-wrap:wrap;align-items:flex-end;gap:10px;background:#fff;
             padding:12px 14px;border:1px solid #e0e5ed;border-radius:4px;margin-bottom:12px}
.mal-fg{display:flex;flex-direction:column;gap:4px;min-width:0}
.mal-fg__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#555}
.mal-fg input,.mal-fg select{padding:6px 8px;border:1px solid #cdd3de;border-radius:0;font-size:12px;
                             font-family:inherit;color:#1a1a2e;background:#fff;min-width:130px;max-width:100%;box-sizing:border-box}
.mal-fg input:focus,.mal-fg select:focus{outline:none;border-color:#174DA4}
.mal-fg--grow{flex:1 1 190px}
.mal-fg--grow input{width:100%;min-width:0}
.mal-fg--wide input{min-width:172px}
.mal-fg__hint{font-weight:400;text-transform:none;letter-spacing:0;color:#a0a8b4;margin-left:4px}
.mal-filters__end{display:flex;gap:8px;align-items:flex-end;margin-left:auto}

/* --- buttons ----------------------------------------------------- */
.mal-btn{padding:7px 14px;font-size:12px;font-weight:500;border:1px solid transparent;border-radius:0;
         cursor:pointer;display:inline-flex;align-items:center;gap:5px;font-family:inherit;
         text-decoration:none;white-space:nowrap;line-height:1.4}
.mal-btn:hover{text-decoration:none}
.mal-btn--primary{background:#05275C;color:#fff;border-color:#05275C}
.mal-btn--primary:hover{background:#041d45;color:#fff}
.mal-btn--ghost{background:#fff;color:#333;border-color:#cdd3de}
.mal-btn--ghost:hover{background:#f5f7fa;color:#333}
.mal-btn--inv{background:transparent;color:#fff;border-color:rgba(255,255,255,.5)}
.mal-btn--inv:hover{background:rgba(255,255,255,.12);color:#fff}
.mal-btn--sm{padding:4px 10px;font-size:11px}

/* --- card + table ------------------------------------------------ */
.mal-card{background:#fff;border:1px solid #e0e5ed;border-radius:4px;overflow:hidden;margin-bottom:14px}
.mal-card__hdr{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;
               padding:11px 14px;border-bottom:1px solid #e0e5ed;background:#f5f7fa}
.mal-card__title{font-size:13px;font-weight:600;color:#1a1a2e}
.mal-card__meta{font-size:11px;color:#888}
.mal-tablewrap{width:100%;overflow-x:auto;-webkit-overflow-scrolling:touch}
.mal-table{width:100%;border-collapse:collapse;font-size:11px}
.mal-table thead tr{background:#f5f7fa}
.mal-table th{padding:8px 12px;text-align:left;font-size:10px;font-weight:600;text-transform:uppercase;
              letter-spacing:.4px;color:#555;border-bottom:2px solid #e0e5ed;white-space:nowrap}
.mal-table td{padding:8px 12px;border-bottom:1px solid #e0e5ed;color:#1a1a2e;vertical-align:top}
.mal-table tbody tr:hover{background:#f9fafc}
.mal-table tbody tr:last-child td{border-bottom:none}
.mal-empty{text-align:center;padding:44px 20px;color:#888;font-size:12px}
.mal-empty b{display:block;font-size:13px;color:#555;margin-bottom:4px;font-weight:600}

/* --- cell pieces ------------------------------------------------- */
.mal-when{white-space:nowrap}
.mal-when__d{font-weight:600;color:#1a1a2e}
.mal-when__t{color:#888;font-size:10px;display:block;margin-top:1px}
.mal-who{font-weight:600;color:#05275C;word-break:break-word}
.mal-who__sub{display:block;color:#888;font-weight:400;font-size:10px;margin-top:1px}
.mal-what{font-weight:600;color:#1a1a2e;line-height:1.35}
.mal-what__sub{display:block;color:#888;font-weight:400;font-size:10px;margin-top:2px;line-height:1.4}
.mal-code{font-family:Consolas,"Courier New",monospace;font-size:10.5px;font-weight:600;
          background:rgba(23,77,164,.07);border:1px solid rgba(23,77,164,.15);color:#174DA4;padding:1px 5px;
          border-radius:0;white-space:nowrap;display:inline-block}
.mal-sub{display:block;color:#888;font-size:10px;margin-top:2px;line-height:1.4}
.mal-nil{color:#c7cdd6}
a.mal-link{text-decoration:none;cursor:pointer}
a.mal-link:hover{background:rgba(23,77,164,.14);border-color:rgba(23,77,164,.35);text-decoration:none}
.mal-many{font-weight:600;color:#05275C}

/* --- badges ------------------------------------------------------ */
.mal-badge{display:inline-block;font-size:9.5px;font-weight:600;padding:2px 6px;border-radius:0;
           text-transform:uppercase;letter-spacing:.3px;white-space:nowrap}
.mal-badge--navy{background:rgba(5,39,92,.08);color:#05275C;border:1px solid rgba(5,39,92,.18)}
.mal-badge--blue{background:#e8f0fc;color:#174DA4;border:1px solid #c7d8f3}
.mal-badge--green{background:#e6f4ea;color:#155724;border:1px solid #c3e6cb}
.mal-badge--amber{background:#fff8e1;color:#b45309;border:1px solid #f4dba6}
.mal-badge--red{background:#fef5f5;color:#912018;border:1px solid #f5c6cb}
.mal-badge--grey{background:#f1f3f7;color:#667085;border:1px solid #e0e5ed}
.mal-dur{font-size:10px;color:#a0a8b4;font-variant-numeric:tabular-nums}
.mal-dur--slow{color:#b45309;font-weight:600}

/* --- legend ------------------------------------------------------ */
.mal-legend{display:flex;gap:18px;flex-wrap:wrap;align-items:center;padding:9px 14px;
            background:#f9fafc;border-top:1px solid #e0e5ed;font-size:10.5px;color:#888;line-height:1.5}
.mal-legend__g{display:inline-flex;align-items:center;gap:5px;flex-wrap:wrap}
.mal-legend__g>b{color:#555;font-weight:600}

/* --- pager ------------------------------------------------------- */
.mal-pager{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;
           padding:10px 14px;border-top:1px solid #e0e5ed;background:#f9fafc}
.mal-pager__info{font-size:11px;color:#888}
.mal-pager__btns{display:flex;gap:4px;flex-wrap:wrap}
.mal-pg{padding:5px 10px;font-size:11px;border:1px solid #cdd3de;background:#fff;color:#333;
        text-decoration:none;border-radius:0;line-height:1.4}
.mal-pg:hover{background:#f5f7fa;color:#05275C;text-decoration:none}
.mal-pg--active{background:#05275C;color:#fff;border-color:#05275C;font-weight:600}
.mal-pg--active:hover{background:#05275C;color:#fff}
.mal-pg--off{color:#c7cdd6;pointer-events:none;background:#f9fafc}

/* --- detail modal ------------------------------------------------ */
.mal-ov{display:none;position:fixed;top:0;right:0;bottom:0;left:0;background:rgba(0,0,0,.45);
        z-index:1000;align-items:center;justify-content:center;padding:12px}
.mal-ov.is-open{display:flex}
.mal-modal{background:#fff;border-radius:2px;width:700px;max-width:100%;max-height:92vh;
           overflow-y:auto;box-shadow:0 12px 40px rgba(0,0,0,.18)}
.mal-modal__hdr{background:#05275C;color:#fff;padding:13px 18px;display:flex;align-items:center;
                justify-content:space-between;gap:10px;position:sticky;top:0;z-index:1}
.mal-modal__title{font-size:13px;font-weight:600}
.mal-modal__close{background:none;border:none;color:#fff;font-size:22px;cursor:pointer;line-height:1;padding:0;opacity:.8}
.mal-modal__close:hover{opacity:1}
.mal-modal__body{padding:16px 18px;min-height:90px}
.mal-modal__foot{padding:12px 18px;border-top:1px solid #e0e5ed;display:flex;justify-content:flex-end;gap:8px;background:#f9fafc}
.mal-sect{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;
          margin:0 0 7px;padding-bottom:5px;border-bottom:1px solid #e0e5ed}
.mal-sect--top{margin-top:0}
.mal-dl{display:grid;grid-template-columns:140px minmax(0,1fr);gap:6px 12px;font-size:11.5px;margin-bottom:18px}
.mal-dl dt{color:#888;font-weight:500}
.mal-dl dd{margin:0;color:#1a1a2e;word-break:break-word}
.mal-lead{font-size:13px;font-weight:600;color:#05275C;line-height:1.35;margin-bottom:3px}
.mal-leadsub{font-size:11px;color:#888;margin-bottom:16px}
.mal-rtbl{width:100%;border-collapse:collapse;font-size:11px;margin-bottom:18px}
.mal-rtbl th{text-align:left;font-size:9.5px;text-transform:uppercase;letter-spacing:.4px;color:#888;
             font-weight:600;padding:5px 8px;border-bottom:1px solid #e0e5ed;white-space:nowrap}
.mal-rtbl td{padding:6px 8px;border-bottom:1px solid #f0f2f5;vertical-align:top}
.mal-rtbl tr:last-child td{border-bottom:none}
.mal-raw{font-family:Consolas,"Courier New",monospace;font-size:10.5px;background:#f5f7fa;
         border:1px solid #e0e5ed;padding:10px;white-space:pre-wrap;word-break:break-word;color:#475569;margin-bottom:18px}
.mal-toggle{font-size:10px;color:#174DA4;cursor:pointer;font-weight:600;float:right;text-transform:none;letter-spacing:0}
.mal-toggle:hover{text-decoration:underline}

/* --- responsive -------------------------------------------------- */
@media(max-width:1100px){.mal-stats{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:760px){
    .mal-head{padding:12px 14px}
    .mal-head__ic{display:none}
    .mal-body{padding:12px 14px 20px}
    .mal-stats{grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}
    .mal-stat__value{font-size:19px}
    .mal-fg{flex:1 1 140px}
    .mal-fg input,.mal-fg select{min-width:0;width:100%}
    .mal-filters__end{margin-left:0;width:100%}
    .mal-filters__end .mal-btn{flex:1 1 auto;justify-content:center}
    .mal-dl{grid-template-columns:1fr;gap:2px 0}
    .mal-dl dd{margin-bottom:8px}
}
@media print{
    .mal-filters,.mal-quick,.mal-pager,.mal-head__r,.mal-ov{display:none!important}
    .mal-head{background:#05275C!important;-webkit-print-color-adjust:exact;print-color-adjust:exact}
    .mal-card{border:none}
}
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<div class="mal-head">
    <div class="mal-head__l">
        <div class="mal-head__ic">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                <path d="M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2"/>
                <rect x="9" y="3" width="6" height="4" rx="1"/><path d="M9 12h6M9 16h4"/>
            </svg>
        </div>
        <div>
            <div class="mal-head__t">Admin Action Log</div>
            <div class="mal-head__s"><asp:Literal ID="litHeadSub" runat="server" /></div>
        </div>
    </div>
    <div class="mal-head__r">
        <asp:Literal ID="litExportBtn" runat="server" />
    </div>
</div>

<div class="mal-body">

    <asp:Literal ID="litNotice" runat="server" />

    <asp:Panel ID="pnlMain" runat="server">

        <div class="mal-stats">
            <div class="mal-stat">
                <div class="mal-stat__label">Actions</div>
                <div class="mal-stat__value"><asp:Literal ID="litKpiActions" runat="server">0</asp:Literal></div>
                <div class="mal-stat__sub"><asp:Literal ID="litKpiActionsSub" runat="server" /></div>
            </div>
            <div class="mal-stat">
                <div class="mal-stat__label">Changed something</div>
                <div class="mal-stat__value"><asp:Literal ID="litKpiChanges" runat="server">0</asp:Literal></div>
                <div class="mal-stat__sub"><asp:Literal ID="litKpiChangesSub" runat="server" /></div>
            </div>
            <div class="mal-stat">
                <div class="mal-stat__label">Students affected</div>
                <div class="mal-stat__value"><asp:Literal ID="litKpiStudents" runat="server">0</asp:Literal></div>
                <div class="mal-stat__sub"><asp:Literal ID="litKpiStudentsSub" runat="server" /></div>
            </div>
            <div class="mal-stat">
                <div class="mal-stat__label">Did not go through</div>
                <div class="mal-stat__value mal-stat__value--warn"><asp:Literal ID="litKpiProblems" runat="server">0</asp:Literal></div>
                <div class="mal-stat__sub"><asp:Literal ID="litKpiProblemsSub" runat="server" /></div>
            </div>
        </div>

        <div class="mal-quick">
            <span class="mal-quick__label">Period</span>
            <asp:Literal ID="litQuick" runat="server" />
        </div>

        <div class="mal-filters">
            <div class="mal-fg">
                <label class="mal-fg__label" for="fFrom">From</label>
                <input type="date" id="fFrom" value="<asp:Literal ID='litFrom' runat='server' />" />
            </div>
            <div class="mal-fg">
                <label class="mal-fg__label" for="fTo">To</label>
                <input type="date" id="fTo" value="<asp:Literal ID='litTo' runat='server' />" />
            </div>
            <div class="mal-fg">
                <label class="mal-fg__label" for="fWho">Who</label>
                <select id="fWho"><asp:Literal ID="litWhoOpts" runat="server" /></select>
            </div>
            <div class="mal-fg">
                <label class="mal-fg__label" for="fImp">Effect</label>
                <select id="fImp"><asp:Literal ID="litImpOpts" runat="server" /></select>
            </div>
            <div class="mal-fg">
                <label class="mal-fg__label" for="fAct">Action</label>
                <select id="fAct"><asp:Literal ID="litActOpts" runat="server" /></select>
            </div>
            <div class="mal-fg">
                <label class="mal-fg__label" for="fScreen">Screen</label>
                <select id="fScreen"><asp:Literal ID="litScreenOpts" runat="server" /></select>
            </div>
            <div class="mal-fg">
                <label class="mal-fg__label" for="fOut">Outcome</label>
                <select id="fOut"><asp:Literal ID="litOutOpts" runat="server" /></select>
            </div>
            <div class="mal-fg mal-fg--wide">
                <label class="mal-fg__label" for="fStu">Student
                    <span class="mal-fg__hint"><asp:Literal ID="litStuCount" runat="server" /></span>
                </label>
                <input type="text" id="fStu" list="malStuList" autocomplete="off" placeholder="reg. number or name"
                       value="<asp:Literal ID='litStu' runat='server' />" onkeydown="if(event.keyCode==13){malApply();return false;}" />
                <datalist id="malStuList"><asp:Literal ID="litStuList" runat="server" /></datalist>
            </div>
            <div class="mal-fg mal-fg--wide">
                <label class="mal-fg__label" for="fCrs">Course
                    <span class="mal-fg__hint"><asp:Literal ID="litCrsCount" runat="server" /></span>
                </label>
                <input type="text" id="fCrs" list="malCrsList" autocomplete="off" placeholder="course code or title"
                       value="<asp:Literal ID='litCrs' runat='server' />" onkeydown="if(event.keyCode==13){malApply();return false;}" />
                <datalist id="malCrsList"><asp:Literal ID="litCrsList" runat="server" /></datalist>
            </div>
            <div class="mal-fg mal-fg--grow">
                <label class="mal-fg__label" for="fQ">Anything else</label>
                <input type="text" id="fQ" placeholder="IP address, note, correction reference..."
                       value="<asp:Literal ID='litQ' runat='server' />" onkeydown="if(event.keyCode==13){malApply();return false;}" />
            </div>
            <div class="mal-filters__end">
                <button type="button" class="mal-btn mal-btn--primary" onclick="malApply()">Search</button>
                <a href="MarksActionLog.aspx" class="mal-btn mal-btn--ghost">Clear</a>
            </div>
        </div>

        <div class="mal-card">
            <div class="mal-card__hdr">
                <span class="mal-card__title">Actions</span>
                <span class="mal-card__meta">
                    <asp:Literal ID="litMeta" runat="server" />
                    &nbsp;&middot;&nbsp;Show
                    <select id="malPageSize" onchange="malApply()" style="border:1px solid #cdd3de;padding:2px 4px;font-size:11px;font-family:inherit">
                        <asp:Literal ID="litPsOpts" runat="server" />
                    </select>
                </span>
            </div>
            <div class="mal-tablewrap">
                <table class="mal-table">
                    <thead>
                        <tr>
                            <th style="width:96px">When</th>
                            <th style="width:150px">Who</th>
                            <th style="width:240px">What happened</th>
                            <th style="width:160px">Student</th>
                            <th style="width:135px">Course</th>
                            <th style="width:96px">Outcome</th>
                            <th style="width:64px"></th>
                        </tr>
                    </thead>
                    <tbody><asp:Literal ID="litRows" runat="server" /></tbody>
                </table>
            </div>
            <div class="mal-legend">
                <span class="mal-legend__g"><b>Effect</b>
                    <span class="mal-badge mal-badge--navy">Changed</span> the action altered a record
                    &nbsp;<span class="mal-badge mal-badge--grey">Viewed</span> it only read one
                </span>
                <span class="mal-legend__g"><b>Student</b> and <b>course</b> are resolved from the registration the action referenced &mdash; neither is stored on the log row. Click either code to filter by it.</span>
            </div>
            <div class="mal-pager">
                <span class="mal-pager__info"><asp:Literal ID="litPagerInfo" runat="server" /></span>
                <span class="mal-pager__btns"><asp:Literal ID="litPager" runat="server" /></span>
            </div>
        </div>

    </asp:Panel>

</div>

<!-- ============================ DETAIL MODAL ============================= -->
<div class="mal-ov" id="malDetail" onclick="if(event.target===this)malClose()">
    <div class="mal-modal" role="dialog" aria-modal="true" aria-labelledby="malDetailTitle">
        <div class="mal-modal__hdr">
            <span class="mal-modal__title" id="malDetailTitle">Action detail</span>
            <button type="button" class="mal-modal__close" onclick="malClose()" aria-label="Close">&#215;</button>
        </div>
        <div class="mal-modal__body" id="malDetailBody"></div>
        <div class="mal-modal__foot">
            <button type="button" class="mal-btn mal-btn--ghost" onclick="malClose()">Close</button>
        </div>
    </div>
</div>

<script type="text/javascript">
// --- filters ------------------------------------------------------------
// The page is a GET report: every filtered view is a link that can be
// bookmarked, pasted into an email, or reached with the Back button.
function malApply(){
    var p = [];
    function add(key, id){
        var el = document.getElementById(id);
        if (el && el.value) p.push(key + '=' + encodeURIComponent(el.value));
    }
    add('from','fFrom'); add('to','fTo');   add('who','fWho');
    add('imp','fImp');   add('act','fAct'); add('screen','fScreen');
    add('out','fOut');   add('stu','fStu'); add('crs','fCrs');
    add('q','fQ');
    // 50 is the default, so leave it out and keep the link clean
    var ps = document.getElementById('malPageSize');
    if (ps && ps.value && ps.value !== '50') p.push('ps=' + encodeURIComponent(ps.value));
    window.location.href = 'MarksActionLog.aspx' + (p.length ? '?' + p.join('&') : '');
}

// --- detail -------------------------------------------------------------
function malEsc(s){
    return String(s === null || s === undefined ? '' : s)
        .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
function malRow(label, value, raw){
    if (value === null || value === undefined || value === '') return '';
    return '<dt>' + malEsc(label) + '</dt><dd>' + (raw ? value : malEsc(value)) + '</dd>';
}
function malOpen(btn){
    var id = btn.getAttribute('data-id');
    var box = document.getElementById('malDetailBody');
    document.getElementById('malDetailTitle').textContent = 'Action #' + id;
    box.innerHTML = '<div style="color:#888;font-size:12px;padding:20px 0;text-align:center">Loading the full record...</div>';
    document.getElementById('malDetail').classList.add('is-open');

    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'MarksActionLog.aspx?ajax=detail&id=' + encodeURIComponent(id), true);
    xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
    xhr.onreadystatechange = function(){
        if (xhr.readyState !== 4) return;
        var d = null;
        try { d = JSON.parse(xhr.responseText); } catch (e) { d = null; }
        if (!d || !d.ok){
            box.innerHTML = '<div class="mal-note mal-note--error">' +
                malEsc((d && d.message) || 'This record could not be loaded.') + '</div>';
            return;
        }
        box.innerHTML = malRender(d);
        var t = document.getElementById('malRawToggle');
        if (t) t.onclick = function(){
            var el = document.getElementById('malRawBox');
            var showing = el.style.display !== 'none';
            el.style.display = showing ? 'none' : 'block';
            this.textContent = showing ? 'show' : 'hide';
        };
    };
    xhr.send();
}
function malRender(d){
    var r = d.row, h = '';

    h += '<div class="mal-lead">' + malEsc(r.what) + '</div>';
    h += '<div class="mal-leadsub">' + malEsc(r.screen) + ' &middot; ' + malEsc(r.outcomeLabel) + '</div>';

    h += '<div class="mal-sect mal-sect--top">Who and when</div><dl class="mal-dl">';
    h += malRow('Performed by', r.who);
    h += malRow('Staff record',  r.staff);
    h += malRow('Date and time', r.at);
    h += malRow('Screen',        r.screen);
    h += malRow('Action code',   r.action);
    h += malRow('Outcome',       r.outcomeLabel);
    h += malRow('IP address',    r.ip);
    h += malRow('Took',          r.duration);
    h += malRow('Correlation',   r.corr);
    h += '</dl>';

    if (d.batchNote) h += '<div class="mal-note mal-note--info">' + malEsc(d.batchNote) + '</div>';

    if (d.records && d.records.length){
        h += '<div class="mal-sect">Records touched (' + d.records.length +
             (d.moreRecords ? ' of ' + d.moreRecords : '') + ')</div>';
        h += '<table class="mal-rtbl"><thead><tr><th>Student</th><th>Course</th><th>Term</th>' +
             '<th>Marks now</th><th>Status</th><th></th></tr></thead><tbody>';
        for (var i = 0; i < d.records.length; i++){
            var x = d.records[i];
            h += '<tr><td>' + (x.regno ? '<span class="mal-code">' + malEsc(x.regno) + '</span>' : '<span class="mal-nil">gone</span>') +
                 (x.name ? '<span class="mal-sub">' + malEsc(x.name) + '</span>' : '') + '</td>' +
                 '<td>' + (x.course ? '<span class="mal-code">' + malEsc(x.course) + '</span>' : '') +
                 (x.courseName ? '<span class="mal-sub">' + malEsc(x.courseName) + '</span>' : '') + '</td>' +
                 '<td>' + malEsc(x.term || '') + '</td>' +
                 '<td>' + malEsc(x.marks || '') + '</td>' +
                 '<td>' + (x.status ? '<span class="mal-badge mal-badge--grey">' + malEsc(x.status) + '</span>' : '') + '</td>' +
                 '<td>' + (x.regno ? '<a class="mal-btn mal-btn--ghost mal-btn--sm" target="_blank" href="MarksAuditTrail.aspx?r=all&amp;q=' +
                    encodeURIComponent(x.regno) + '">Mark history</a>' : '') + '</td></tr>';
        }
        h += '</tbody></table>';
        if (d.missing) h += '<div class="mal-note mal-note--warn">' + malEsc(d.missing) + '</div>';
    } else if (d.noRecordsNote){
        h += '<div class="mal-sect">Records touched</div>' +
             '<div class="mal-note mal-note--info">' + malEsc(d.noRecordsNote) + '</div>';
    }

    if (d.context && d.context.length){
        h += '<div class="mal-sect">What the action recorded</div><dl class="mal-dl">';
        for (var j = 0; j < d.context.length; j++) h += malRow(d.context[j].k, d.context[j].v);
        h += '</dl>';
    }

    if (r.raw){
        h += '<div class="mal-sect">Raw context <span class="mal-toggle" id="malRawToggle">show</span></div>' +
             '<div class="mal-raw" id="malRawBox" style="display:none">' + malEsc(r.raw) + '</div>';
    }
    return h;
}
function malClose(){ document.getElementById('malDetail').classList.remove('is-open'); }
document.addEventListener('keydown', function(e){ if (e.keyCode === 27) malClose(); });
</script>

</asp:Content>
