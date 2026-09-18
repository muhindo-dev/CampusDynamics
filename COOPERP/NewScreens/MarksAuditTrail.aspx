<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="MarksAuditTrail.aspx.cs" Inherits="COOPERP_NewScreens_MarksAuditTrail" Title="Marks Audit Trail - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===================================================================
   Marks Audit Trail  --  house design system (navy, flat, compact).
   Prefix: mat-      Spec: NewScreens/DESIGN_SYSTEM.md
   =================================================================== */

/* --- page header ------------------------------------------------- */
.mat-head{display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;
          background:#05275C;color:#fff;padding:14px 20px;border-bottom:3px solid #041d45}
.mat-head__l{display:flex;align-items:center;gap:12px;min-width:0}
.mat-head__ic{width:40px;height:40px;background:rgba(255,255,255,.12);border-radius:4px;
              display:flex;align-items:center;justify-content:center;flex-shrink:0}
.mat-head__t{font-size:16px;font-weight:700;line-height:1.2}
.mat-head__s{font-size:12px;opacity:.75;margin-top:2px;max-width:620px;line-height:1.45}
.mat-head__r{display:flex;align-items:center;gap:8px;flex-wrap:wrap}

/* --- view tabs --------------------------------------------------- */
.mat-tabs{display:flex;gap:2px;background:#f0f2f5;border-bottom:2px solid #e0e5ed;padding:0 20px;overflow-x:auto}
.mat-tab{padding:9px 16px;font-size:12px;font-weight:500;color:#555;text-decoration:none;
         border-bottom:2px solid transparent;margin-bottom:-2px;white-space:nowrap}
.mat-tab:hover{color:#05275C;text-decoration:none}
.mat-tab--active{color:#05275C;border-bottom-color:#05275C;font-weight:600}
.mat-tab__n{font-size:10px;color:#888;margin-left:5px;font-weight:600}

/* --- page body --------------------------------------------------- */
.mat-body{padding:14px 20px 24px}

/* --- notices ----------------------------------------------------- */
.mat-note{padding:10px 14px;font-size:11.5px;line-height:1.55;margin-bottom:12px;border-radius:2px}
.mat-note--info{background:#eef3fb;border:1px solid #cfdcf2;color:#173f79}
.mat-note--error{background:#fef5f5;border:1px solid #f5c6cb;color:#912018}
.mat-note b{font-weight:600}

/* the guard-health banner rendered by MarksControllerShared */
.pm-auditwarn{display:flex;gap:10px;align-items:flex-start;background:#fef2f2;border:1px solid #fecaca;
              border-left:3px solid #b42318;padding:10px 12px;margin-bottom:12px}
.pm-auditwarn b{display:block;font-size:12px;color:#7a271a;margin-bottom:2px}
.pm-auditwarn p{margin:0;font-size:11px;color:#912018;line-height:1.5}
.pm-auditwarn code{background:rgba(180,35,24,.08);padding:0 3px}

/* --- KPI cards --------------------------------------------------- */
.mat-stats{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin-bottom:14px}
.mat-stat{background:#fff;border:1px solid #e0e5ed;border-radius:4px;padding:13px 15px;min-width:0}
.mat-stat__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;margin-bottom:5px}
.mat-stat__value{font-size:22px;font-weight:700;color:#05275C;line-height:1;font-variant-numeric:tabular-nums}
.mat-stat__sub{font-size:11px;color:#888;margin-top:5px;line-height:1.4}

/* --- filter bar -------------------------------------------------- */
.mat-filters{display:flex;flex-wrap:wrap;align-items:flex-end;gap:10px;background:#fff;
             padding:12px 14px;border:1px solid #e0e5ed;border-radius:4px;margin-bottom:12px}
.mat-fg{display:flex;flex-direction:column;gap:4px;min-width:0}
.mat-fg__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#555}
.mat-fg input,.mat-fg select{padding:6px 8px;border:1px solid #cdd3de;border-radius:0;font-size:12px;
                             font-family:inherit;color:#1a1a2e;background:#fff;min-width:132px;max-width:100%;box-sizing:border-box}
.mat-fg input:focus,.mat-fg select:focus{outline:none;border-color:#174DA4}
.mat-fg--grow{flex:1 1 190px}
.mat-fg--grow input{width:100%;min-width:0}
.mat-fg--wide input{min-width:172px}
.mat-fg__hint{font-weight:400;text-transform:none;letter-spacing:0;color:#a0a8b4;margin-left:4px}
.mat-filters__end{display:flex;gap:8px;align-items:flex-end;margin-left:auto}

/* --- buttons ----------------------------------------------------- */
.mat-btn{padding:7px 14px;font-size:12px;font-weight:500;border:1px solid transparent;border-radius:0;
         cursor:pointer;display:inline-flex;align-items:center;gap:5px;font-family:inherit;
         text-decoration:none;white-space:nowrap;line-height:1.4}
.mat-btn:hover{text-decoration:none}
.mat-btn--primary{background:#05275C;color:#fff;border-color:#05275C}
.mat-btn--primary:hover{background:#041d45;color:#fff}
.mat-btn--ghost{background:#fff;color:#333;border-color:#cdd3de}
.mat-btn--ghost:hover{background:#f5f7fa;color:#333}
.mat-btn--inv{background:transparent;color:#fff;border-color:rgba(255,255,255,.5)}
.mat-btn--inv:hover{background:rgba(255,255,255,.12);color:#fff}
.mat-btn--sm{padding:4px 10px;font-size:11px}

/* --- quick ranges ------------------------------------------------ */
.mat-quick{display:flex;gap:6px;flex-wrap:wrap;align-items:center;margin-bottom:12px}
.mat-quick__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;margin-right:2px}
.mat-pill{padding:5px 11px;font-size:11px;font-weight:500;background:#fff;border:1px solid #e0e5ed;
          color:#555;border-radius:0;cursor:pointer;text-decoration:none;font-family:inherit}
.mat-pill:hover{background:#f5f7fa;color:#05275C;text-decoration:none}
.mat-pill--active{background:#05275C;color:#fff;border-color:#05275C}
.mat-pill--active:hover{background:#041d45;color:#fff}

/* --- card + table ------------------------------------------------ */
.mat-card{background:#fff;border:1px solid #e0e5ed;border-radius:4px;overflow:hidden;margin-bottom:14px}
.mat-card__hdr{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;
               padding:11px 14px;border-bottom:1px solid #e0e5ed;background:#f5f7fa}
.mat-card__title{font-size:13px;font-weight:600;color:#1a1a2e}
.mat-card__meta{font-size:11px;color:#888}
.mat-tablewrap{width:100%;overflow-x:auto;-webkit-overflow-scrolling:touch}
.mat-table{width:100%;border-collapse:collapse;font-size:11px}
.mat-table thead tr{background:#f5f7fa}
.mat-table th{padding:8px 12px;text-align:left;font-size:10px;font-weight:600;text-transform:uppercase;
              letter-spacing:.4px;color:#555;border-bottom:2px solid #e0e5ed;white-space:nowrap}
.mat-table td{padding:8px 12px;border-bottom:1px solid #e0e5ed;color:#1a1a2e;vertical-align:top}
.mat-table tbody tr:hover{background:#f9fafc}
.mat-table tbody tr:last-child td{border-bottom:none}
.mat-empty{text-align:center;padding:44px 20px;color:#888;font-size:12px}
.mat-empty b{display:block;font-size:13px;color:#555;margin-bottom:4px;font-weight:600}

/* --- cell pieces ------------------------------------------------- */
.mat-when{white-space:nowrap}
.mat-when__d{font-weight:600;color:#1a1a2e}
.mat-when__t{color:#888;font-size:10px;display:block;margin-top:1px}
.mat-who{font-weight:600;color:#05275C;word-break:break-word}
.mat-who__sub{display:block;color:#888;font-weight:400;font-size:10px;margin-top:1px}
.mat-code{font-family:Consolas,"Courier New",monospace;font-size:10.5px;font-weight:600;
          background:rgba(23,77,164,.07);border:1px solid rgba(23,77,164,.15);color:#174DA4;padding:1px 5px;
          border-radius:0;white-space:nowrap;display:inline-block}
.mat-sub{display:block;color:#888;font-size:10px;margin-top:2px;line-height:1.4}
.mat-nil{color:#c7cdd6}
a.mat-link{text-decoration:none;cursor:pointer;display:inline-block}
a.mat-code.mat-link:hover{background:rgba(23,77,164,.14);border-color:rgba(23,77,164,.35);text-decoration:none}
a.mat-who.mat-link:hover{text-decoration:underline}

/* old -> new mark movement */
.mat-mv{white-space:nowrap;font-variant-numeric:tabular-nums}
.mat-mv__o{color:#888;text-decoration:line-through}
.mat-mv__a{color:#b0b8c4;margin:0 3px}
.mat-mv__n{font-weight:700}
.mat-mv--up .mat-mv__n{color:#15803d}
.mat-mv--down .mat-mv__n{color:#b42318}
.mat-mv--set .mat-mv__n{color:#05275C}
.mat-mv__unset{color:#a9b2bf;font-style:italic;text-decoration:none}

/* --- badges ------------------------------------------------------ */
.mat-badge{display:inline-block;font-size:9.5px;font-weight:600;padding:2px 6px;border-radius:0;
           text-transform:uppercase;letter-spacing:.3px;white-space:nowrap}
.mat-badge--navy{background:rgba(5,39,92,.08);color:#05275C;border:1px solid rgba(5,39,92,.18)}
.mat-badge--blue{background:#e8f0fc;color:#174DA4;border:1px solid #c7d8f3}
.mat-badge--green{background:#e6f4ea;color:#155724;border:1px solid #c3e6cb}
.mat-badge--amber{background:#fff8e1;color:#b45309;border:1px solid #f4dba6}
.mat-badge--red{background:#fef5f5;color:#912018;border:1px solid #f5c6cb}
.mat-badge--grey{background:#f1f3f7;color:#667085;border:1px solid #e0e5ed}

/* --- legend ------------------------------------------------------ */
.mat-legend{display:flex;gap:18px;flex-wrap:wrap;align-items:center;padding:9px 14px;
            background:#f9fafc;border-top:1px solid #e0e5ed;font-size:10.5px;color:#888;line-height:1.5}
.mat-legend__g{display:inline-flex;align-items:center;gap:5px;flex-wrap:wrap}
.mat-legend__g>b{color:#555;font-weight:600}

/* --- pager ------------------------------------------------------- */
.mat-pager{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;
           padding:10px 14px;border-top:1px solid #e0e5ed;background:#f9fafc}
.mat-pager__info{font-size:11px;color:#888}
.mat-pager__btns{display:flex;gap:4px;flex-wrap:wrap}
.mat-pg{padding:5px 10px;font-size:11px;border:1px solid #cdd3de;background:#fff;color:#333;
        text-decoration:none;border-radius:0;line-height:1.4}
.mat-pg:hover{background:#f5f7fa;color:#05275C;text-decoration:none}
.mat-pg--active{background:#05275C;color:#fff;border-color:#05275C;font-weight:600}
.mat-pg--active:hover{background:#05275C;color:#fff}
.mat-pg--off{color:#c7cdd6;pointer-events:none;background:#f9fafc}

/* --- detail modal ------------------------------------------------ */
.mat-ov{display:none;position:fixed;top:0;right:0;bottom:0;left:0;background:rgba(0,0,0,.45);
        z-index:1000;align-items:center;justify-content:center;padding:12px}
.mat-ov.is-open{display:flex}
.mat-modal{background:#fff;border-radius:2px;width:620px;max-width:100%;max-height:92vh;
           overflow-y:auto;box-shadow:0 12px 40px rgba(0,0,0,.18)}
.mat-modal__hdr{background:#05275C;color:#fff;padding:13px 18px;display:flex;align-items:center;
                justify-content:space-between;gap:10px;position:sticky;top:0}
.mat-modal__title{font-size:13px;font-weight:600}
.mat-modal__close{background:none;border:none;color:#fff;font-size:22px;cursor:pointer;line-height:1;padding:0;opacity:.8}
.mat-modal__close:hover{opacity:1}
.mat-modal__body{padding:16px 18px}
.mat-modal__foot{padding:12px 18px;border-top:1px solid #e0e5ed;display:flex;justify-content:flex-end;background:#f9fafc}
.mat-sect{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;
          margin:0 0 7px;padding-bottom:5px;border-bottom:1px solid #e0e5ed}
.mat-sect+.mat-sect{margin-top:18px}
.mat-dl{display:grid;grid-template-columns:132px minmax(0,1fr);gap:6px 12px;font-size:11.5px;margin-bottom:18px}
.mat-dl dt{color:#888;font-weight:500}
.mat-dl dd{margin:0;color:#1a1a2e;word-break:break-word}
.mat-dl:last-child{margin-bottom:0}
.mat-mvtable{width:100%;border-collapse:collapse;font-size:11.5px;margin-bottom:18px}
.mat-mvtable th{text-align:left;font-size:10px;text-transform:uppercase;letter-spacing:.4px;color:#888;
                font-weight:600;padding:5px 8px;border-bottom:1px solid #e0e5ed}
.mat-mvtable td{padding:6px 8px;border-bottom:1px solid #f0f2f5;font-variant-numeric:tabular-nums}
.mat-mvtable tr:last-child td{border-bottom:none}
.mat-mvtable td:first-child{color:#555;font-weight:500}

/* --- responsive -------------------------------------------------- */
@media(max-width:1100px){.mat-stats{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:760px){
    .mat-head{padding:12px 14px}
    .mat-head__ic{display:none}
    .mat-head__s{font-size:11.5px}
    .mat-tabs{padding:0 14px}
    .mat-body{padding:12px 14px 20px}
    .mat-stats{grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}
    .mat-stat__value{font-size:19px}
    .mat-fg{flex:1 1 140px}
    .mat-fg input,.mat-fg select{min-width:0;width:100%}
    .mat-filters__end{margin-left:0;width:100%}
    .mat-filters__end .mat-btn{flex:1 1 auto;justify-content:center}
    .mat-dl{grid-template-columns:1fr;gap:2px 0}
    .mat-dl dd{margin-bottom:8px}
}
@media print{
    .mat-tabs,.mat-filters,.mat-quick,.mat-pager,.mat-head__r,.mat-ov{display:none!important}
    .mat-head{background:#05275C!important;-webkit-print-color-adjust:exact;print-color-adjust:exact}
    .mat-card{border:none}
}
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<div class="mat-head">
    <div class="mat-head__l">
        <div class="mat-head__ic">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                <path d="M12 3l8 3.5v5c0 4.6-3.4 8.6-8 9.5-4.6-.9-8-4.9-8-9.5v-5L12 3z"/><path d="M9 12l2 2 4-4"/>
            </svg>
        </div>
        <div>
            <div class="mat-head__t">Marks Audit Trail</div>
            <div class="mat-head__s"><asp:Literal ID="litHeadSub" runat="server" /></div>
        </div>
    </div>
    <div class="mat-head__r">
        <asp:Literal ID="litExportBtn" runat="server" />
    </div>
</div>

<div class="mat-tabs"><asp:Literal ID="litTabs" runat="server" /></div>

<div class="mat-body">

    <asp:Literal ID="litHealth" runat="server" />
    <asp:Literal ID="litNotice" runat="server" />

    <!-- ============================ MARK CHANGES ============================ -->
    <asp:Panel ID="pnlChanges" runat="server">

        <div class="mat-stats">
            <div class="mat-stat">
                <div class="mat-stat__label">Recorded changes</div>
                <div class="mat-stat__value"><asp:Literal ID="litKpiTotal" runat="server">0</asp:Literal></div>
                <div class="mat-stat__sub"><asp:Literal ID="litKpiTotalSub" runat="server" /></div>
            </div>
            <div class="mat-stat">
                <div class="mat-stat__label">Coursework marks moved</div>
                <div class="mat-stat__value"><asp:Literal ID="litKpiCw" runat="server">0</asp:Literal></div>
                <div class="mat-stat__sub">changes that touched coursework</div>
            </div>
            <div class="mat-stat">
                <div class="mat-stat__label">Exam marks moved</div>
                <div class="mat-stat__value"><asp:Literal ID="litKpiExam" runat="server">0</asp:Literal></div>
                <div class="mat-stat__sub">changes that touched the exam mark</div>
            </div>
            <div class="mat-stat">
                <div class="mat-stat__label">Students affected</div>
                <div class="mat-stat__value"><asp:Literal ID="litKpiStudents" runat="server">0</asp:Literal></div>
                <div class="mat-stat__sub"><asp:Literal ID="litKpiStaff" runat="server" /></div>
            </div>
        </div>

        <div class="mat-quick">
            <span class="mat-quick__label">Period</span>
            <asp:Literal ID="litQuickC" runat="server" />
        </div>

        <div class="mat-filters">
            <div class="mat-fg">
                <label class="mat-fg__label" for="cFrom">From</label>
                <input type="date" id="cFrom" value="<asp:Literal ID='litCFrom' runat='server' />" />
            </div>
            <div class="mat-fg">
                <label class="mat-fg__label" for="cTo">To</label>
                <input type="date" id="cTo" value="<asp:Literal ID='litCTo' runat='server' />" />
            </div>
            <div class="mat-fg">
                <label class="mat-fg__label" for="cWho">Lecturer / staff</label>
                <select id="cWho" title="Whoever moved the mark"><asp:Literal ID="litCWhoOpts" runat="server" /></select>
            </div>
            <div class="mat-fg">
                <label class="mat-fg__label" for="cChg">What changed</label>
                <select id="cChg"><asp:Literal ID="litCChgOpts" runat="server" /></select>
            </div>
            <div class="mat-fg">
                <label class="mat-fg__label" for="cSrc">Where from</label>
                <select id="cSrc"><asp:Literal ID="litCSrcOpts" runat="server" /></select>
            </div>
            <div class="mat-fg mat-fg--wide">
                <label class="mat-fg__label" for="cStu">Student
                    <span class="mat-fg__hint"><asp:Literal ID="litCStuCount" runat="server" /></span>
                </label>
                <input type="text" id="cStu" list="matStuList" autocomplete="off" placeholder="reg. number or name"
                       value="<asp:Literal ID='litCStu' runat='server' />" onkeydown="if(event.keyCode==13){matApply('changes');return false;}" />
                <datalist id="matStuList"><asp:Literal ID="litCStuList" runat="server" /></datalist>
            </div>
            <div class="mat-fg mat-fg--wide">
                <label class="mat-fg__label" for="cCrs">Course
                    <span class="mat-fg__hint"><asp:Literal ID="litCCrsCount" runat="server" /></span>
                </label>
                <input type="text" id="cCrs" list="matCrsList" autocomplete="off" placeholder="course code or title"
                       value="<asp:Literal ID='litCCrs' runat='server' />" onkeydown="if(event.keyCode==13){matApply('changes');return false;}" />
                <datalist id="matCrsList"><asp:Literal ID="litCCrsList" runat="server" /></datalist>
            </div>
            <div class="mat-fg mat-fg--grow">
                <label class="mat-fg__label" for="cQ">Anything else</label>
                <input type="text" id="cQ" placeholder="IP address, reason, programme, year..."
                       value="<asp:Literal ID='litCQ' runat='server' />" onkeydown="if(event.keyCode==13){matApply('changes');return false;}" />
            </div>
            <div class="mat-filters__end">
                <button type="button" class="mat-btn mat-btn--primary" onclick="matApply('changes')">Search</button>
                <a href="MarksAuditTrail.aspx" class="mat-btn mat-btn--ghost">Clear</a>
            </div>
        </div>

        <div class="mat-card">
            <div class="mat-card__hdr">
                <span class="mat-card__title">Mark changes</span>
                <span class="mat-card__meta">
                    <asp:Literal ID="litCMeta" runat="server" />
                    &nbsp;&middot;&nbsp;Show
                    <select id="matPageSize" onchange="matApply('changes')" style="border:1px solid #cdd3de;padding:2px 4px;font-size:11px;font-family:inherit">
                        <asp:Literal ID="litCPsOpts" runat="server" />
                    </select>
                </span>
            </div>
            <div class="mat-tablewrap">
                <table class="mat-table">
                    <thead>
                        <tr>
                            <th style="width:96px">When</th>
                            <th style="width:150px">Changed by</th>
                            <th style="width:160px">Student</th>
                            <th style="width:120px">Course</th>
                            <th style="width:110px">Coursework</th>
                            <th style="width:110px">Exam</th>
                            <th style="width:110px">Total</th>
                            <th style="width:120px">Where from</th>
                            <th style="width:64px"></th>
                        </tr>
                    </thead>
                    <tbody><asp:Literal ID="litCRows" runat="server" /></tbody>
                </table>
            </div>
            <div class="mat-legend">
                <span class="mat-legend__g"><b>Where from</b>
                    <span class="mat-badge mat-badge--navy">eAdmin</span>
                    <span class="mat-badge mat-badge--green">Student portal</span>
                    <span class="mat-badge mat-badge--blue">Mark request</span>
                    <span class="mat-badge mat-badge--amber">ODEL</span>
                    <span class="mat-badge mat-badge--red">Staff API</span>
                </span>
                <span class="mat-legend__g"><b>Reconstructed</b> rebuilt from the older activity log, not recorded by the database as it happened.</span>
                <span class="mat-legend__g"><b>Not set / cleared</b> the mark had no value before the change, or has none after it.</span>
                <span class="mat-legend__g"><b>Tip</b> click a lecturer, a student or a course to filter by it.</span>
            </div>
            <div class="mat-pager">
                <span class="mat-pager__info"><asp:Literal ID="litCPagerInfo" runat="server" /></span>
                <span class="mat-pager__btns"><asp:Literal ID="litCPager" runat="server" /></span>
            </div>
        </div>

    </asp:Panel>

    <!-- =========================== ACTIVITY LOG ============================ -->
    <asp:Panel ID="pnlActivity" runat="server" Visible="false">

        <div class="mat-quick">
            <span class="mat-quick__label">Period</span>
            <asp:Literal ID="litQuickA" runat="server" />
        </div>

        <div class="mat-filters">
            <div class="mat-fg">
                <label class="mat-fg__label" for="aFrom">From</label>
                <input type="date" id="aFrom" value="<asp:Literal ID='litAFrom' runat='server' />" />
            </div>
            <div class="mat-fg">
                <label class="mat-fg__label" for="aTo">To</label>
                <input type="date" id="aTo" value="<asp:Literal ID='litATo' runat='server' />" />
            </div>
            <div class="mat-fg">
                <label class="mat-fg__label" for="aWho">User</label>
                <select id="aWho"><asp:Literal ID="litAWhoOpts" runat="server" /></select>
            </div>
            <div class="mat-fg">
                <label class="mat-fg__label" for="aAct">Action</label>
                <select id="aAct"><asp:Literal ID="litAActOpts" runat="server" /></select>
            </div>
            <div class="mat-fg mat-fg--grow">
                <label class="mat-fg__label" for="aQ">Search</label>
                <input type="text" id="aQ" placeholder="student, staff name or staff code"
                       value="<asp:Literal ID='litAQ' runat='server' />" onkeydown="if(event.keyCode==13){matApply('activity');return false;}" />
            </div>
            <div class="mat-filters__end">
                <button type="button" class="mat-btn mat-btn--primary" onclick="matApply('activity')">Search</button>
                <a href="MarksAuditTrail.aspx?view=activity" class="mat-btn mat-btn--ghost">Clear</a>
            </div>
        </div>

        <div class="mat-card">
            <div class="mat-card__hdr">
                <span class="mat-card__title">Activity log</span>
                <span class="mat-card__meta">
                    <asp:Literal ID="litAMeta" runat="server" />
                    &nbsp;&middot;&nbsp;Show
                    <select id="matPageSize" onchange="matApply('activity')" style="border:1px solid #cdd3de;padding:2px 4px;font-size:11px;font-family:inherit">
                        <asp:Literal ID="litAPsOpts" runat="server" />
                    </select>
                </span>
            </div>
            <div class="mat-tablewrap">
                <table class="mat-table">
                    <thead>
                        <tr>
                            <th style="width:96px">When</th>
                            <th style="width:180px">User</th>
                            <th style="width:140px">Action</th>
                            <th style="width:160px">Student</th>
                            <th>What was recorded</th>
                            <th style="width:110px">IP address</th>
                        </tr>
                    </thead>
                    <tbody><asp:Literal ID="litARows" runat="server" /></tbody>
                </table>
            </div>
            <div class="mat-pager">
                <span class="mat-pager__info"><asp:Literal ID="litAPagerInfo" runat="server" /></span>
                <span class="mat-pager__btns"><asp:Literal ID="litAPager" runat="server" /></span>
            </div>
        </div>

    </asp:Panel>

</div>

<!-- ============================ DETAIL MODAL ============================= -->
<div class="mat-ov" id="matDetail" onclick="if(event.target===this)matCloseDetail()">
    <div class="mat-modal" role="dialog" aria-modal="true" aria-labelledby="matDetailTitle">
        <div class="mat-modal__hdr">
            <span class="mat-modal__title" id="matDetailTitle">Change details</span>
            <button type="button" class="mat-modal__close" onclick="matCloseDetail()" aria-label="Close">&#215;</button>
        </div>
        <div class="mat-modal__body" id="matDetailBody"></div>
        <div class="mat-modal__foot">
            <button type="button" class="mat-btn mat-btn--ghost" onclick="matCloseDetail()">Close</button>
        </div>
    </div>
</div>

<script type="text/javascript">
// --- filter submit ------------------------------------------------------
// Built as a URL rather than a form post: the page is a GET-driven report, so
// every view is a link somebody can bookmark, share or hit Back out of.
function matApply(view){
    var p = ['view=' + view];
    function add(key, id){
        var el = document.getElementById(id);
        if (el && el.value) p.push(key + '=' + encodeURIComponent(el.value));
    }
    if (view === 'changes'){
        add('from','cFrom'); add('to','cTo');  add('who','cWho');
        add('chg','cChg');   add('src','cSrc');
        add('stu','cStu');   add('crs','cCrs'); add('q','cQ');
    } else {
        add('from','aFrom'); add('to','aTo'); add('who','aWho');
        add('act','aAct');   add('q','aQ');
    }
    // 50 is the default, so leave it out and keep the link clean
    var ps = document.getElementById('matPageSize');
    if (ps && ps.value && ps.value !== '50') p.push('ps=' + encodeURIComponent(ps.value));
    window.location.href = 'MarksAuditTrail.aspx?' + p.join('&');
}

// --- detail modal -------------------------------------------------------
function matEsc(s){
    return String(s === null || s === undefined ? '' : s)
        .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
function matRow(label, value, raw){
    if (value === null || value === undefined || value === '') return '';
    return '<dt>' + matEsc(label) + '</dt><dd>' + (raw ? value : matEsc(value)) + '</dd>';
}
function matMove(oldV, newV){
    var o = (oldV === '' || oldV === null || oldV === undefined) ? null : oldV;
    var n = (newV === '' || newV === null || newV === undefined) ? null : newV;
    if (o === null && n === null) return '<span class="mat-nil">not recorded</span>';
    var cls = 'mat-mv--set';
    if (o !== null && n !== null){
        var a = parseFloat(o), b = parseFloat(n);
        if (!isNaN(a) && !isNaN(b)) cls = (b > a) ? 'mat-mv--up' : (b < a ? 'mat-mv--down' : 'mat-mv--set');
    }
    return '<span class="mat-mv ' + cls + '">'
         + (o === null ? '<span class="mat-mv__unset">not set</span>' : '<span class="mat-mv__o">' + matEsc(o) + '</span>')
         + '<span class="mat-mv__a">&rarr;</span>'
         + (n === null ? '<span class="mat-mv__unset">cleared</span>' : '<span class="mat-mv__n">' + matEsc(n) + '</span>')
         + '</span>';
}
function matShowDetail(btn){
    var d = btn.getAttribute.bind(btn);
    var html = '';

    html += '<div class="mat-sect">What moved</div>';
    html += '<table class="mat-mvtable"><thead><tr><th>Mark</th><th>Before</th><th>After</th><th>Change</th></tr></thead><tbody>';
    var parts = [['Coursework','ocw','ncw'],['Exam','oex','nex'],['Total','otot','ntot']];
    for (var i = 0; i < parts.length; i++){
        var o = d('data-' + parts[i][1]), n = d('data-' + parts[i][2]);
        html += '<tr><td>' + parts[i][0] + '</td>'
              + '<td>' + (o === '' || o === null ? '<span class="mat-mv__unset">not set</span>' : matEsc(o)) + '</td>'
              + '<td>' + (n === '' || n === null ? '<span class="mat-mv__unset">cleared</span>' : matEsc(n)) + '</td>'
              + '<td>' + matMove(o, n) + '</td></tr>';
    }
    html += '</tbody></table>';

    html += '<div class="mat-sect">Who and when</div><dl class="mat-dl">';
    html += matRow('Changed by',  d('data-by'));
    html += matRow('Staff record', d('data-staff'));
    html += matRow('Date and time', d('data-at'));
    html += matRow('Came from',   d('data-src'));
    html += matRow('IP address',  d('data-ip'));
    html += matRow('Reason given', d('data-reason'));
    html += '</dl>';

    html += '<div class="mat-sect">Which mark</div><dl class="mat-dl">';
    html += matRow('Student',       d('data-regno'));
    html += matRow('Name',          d('data-sname'));
    html += matRow('Course',        d('data-course'));
    html += matRow('Course title',  d('data-ctitle'));
    html += matRow('Programme',     d('data-prog'));
    html += matRow('Academic year', d('data-year'));
    html += matRow('Semester',      d('data-sem'));
    html += matRow('Status',        matMove(d('data-ostat'), d('data-nstat')), true);
    html += '</dl>';

    html += '<div class="mat-sect">Record</div><dl class="mat-dl">';
    html += matRow('Entry number',  '#' + d('data-id'));
    html += matRow('Operation',     d('data-act'));
    html += matRow('Table changed', d('data-table'));
    html += matRow('Registration',  d('data-reg'));
    html += '</dl>';

    document.getElementById('matDetailBody').innerHTML = html;
    document.getElementById('matDetailTitle').textContent = 'Change #' + d('data-id') + ' - ' + d('data-regno');
    document.getElementById('matDetail').classList.add('is-open');
}
function matCloseDetail(){ document.getElementById('matDetail').classList.remove('is-open'); }
document.addEventListener('keydown', function(e){ if (e.keyCode === 27) matCloseDetail(); });
</script>

</asp:Content>
