<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="MissingMarks.aspx.cs" Inherits="COOPERP_NewScreens_MissingMarks" Title="Missing Marks - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>

/* ===================================================================
   Missing Marks  --  house design system (navy, flat, compact).
   Prefix: mm-      Spec: NewScreens/DESIGN_SYSTEM.md
   =================================================================== */

/* --- page header ------------------------------------------------- */
.mm-head{display:flex;align-items:center;justify-content:space-between;gap:16px;flex-wrap:wrap;
          background:#05275C;color:#fff;padding:14px 20px;border-bottom:3px solid #041d45}
.mm-head__l{display:flex;align-items:center;gap:12px;min-width:0}
.mm-head__ic{width:40px;height:40px;background:rgba(255,255,255,.12);border-radius:4px;
              display:flex;align-items:center;justify-content:center;flex-shrink:0}
.mm-head__t{font-size:16px;font-weight:700;line-height:1.2}
.mm-head__s{font-size:12px;opacity:.75;margin-top:2px;max-width:640px;line-height:1.45}
.mm-head__r{display:flex;align-items:center;gap:8px;flex-wrap:wrap}

.mm-body{padding:14px 20px 24px}

/* --- notices ----------------------------------------------------- */
.mm-note{padding:10px 14px;font-size:11.5px;line-height:1.55;margin-bottom:12px;border-radius:2px}
.mm-note--info{background:#eef3fb;border:1px solid #cfdcf2;color:#173f79}
.mm-note--warn{background:#fff8e1;border:1px solid #f4dba6;color:#8a5a08}
.mm-note--error{background:#fef5f5;border:1px solid #f5c6cb;color:#912018}
.mm-note b{font-weight:600}

/* --- KPI cards --------------------------------------------------- */
.mm-stats{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin-bottom:14px}
.mm-stat{background:#fff;border:1px solid #e0e5ed;border-radius:4px;padding:13px 15px;min-width:0}
.mm-stat__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;margin-bottom:5px}
.mm-stat__value{font-size:22px;font-weight:700;color:#05275C;line-height:1;font-variant-numeric:tabular-nums}
.mm-stat__value--warn{color:#b45309}
.mm-stat__sub{font-size:11px;color:#888;margin-top:5px;line-height:1.4}

/* --- quick ranges ------------------------------------------------ */
.mm-quick{display:flex;gap:6px;flex-wrap:wrap;align-items:center;margin-bottom:12px}
.mm-quick__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;margin-right:2px}
.mm-pill{padding:5px 11px;font-size:11px;font-weight:500;background:#fff;border:1px solid #e0e5ed;
          color:#555;border-radius:0;cursor:pointer;text-decoration:none;font-family:inherit}
.mm-pill:hover{background:#f5f7fa;color:#05275C;text-decoration:none}
.mm-pill--active{background:#05275C;color:#fff;border-color:#05275C}
.mm-pill--active:hover{background:#041d45;color:#fff}

/* --- filter bar -------------------------------------------------- */
.mm-filters{display:flex;flex-wrap:wrap;align-items:flex-end;gap:10px;background:#fff;
             padding:12px 14px;border:1px solid #e0e5ed;border-radius:4px;margin-bottom:12px}
.mm-fg{display:flex;flex-direction:column;gap:4px;min-width:0}
.mm-fg__label{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#555}
.mm-fg input,.mm-fg select{padding:6px 8px;border:1px solid #cdd3de;border-radius:0;font-size:12px;
                             font-family:inherit;color:#1a1a2e;background:#fff;min-width:130px;max-width:100%;box-sizing:border-box}
.mm-fg input:focus,.mm-fg select:focus{outline:none;border-color:#174DA4}
.mm-fg--grow{flex:1 1 190px}
.mm-fg--grow input{width:100%;min-width:0}
.mm-fg--wide input{min-width:172px}
.mm-fg__hint{font-weight:400;text-transform:none;letter-spacing:0;color:#a0a8b4;margin-left:4px}
.mm-filters__end{display:flex;gap:8px;align-items:flex-end;margin-left:auto}

/* --- buttons ----------------------------------------------------- */
.mm-btn{padding:7px 14px;font-size:12px;font-weight:500;border:1px solid transparent;border-radius:0;
         cursor:pointer;display:inline-flex;align-items:center;gap:5px;font-family:inherit;
         text-decoration:none;white-space:nowrap;line-height:1.4}
.mm-btn:hover{text-decoration:none}
.mm-btn--primary{background:#05275C;color:#fff;border-color:#05275C}
.mm-btn--primary:hover{background:#041d45;color:#fff}
.mm-btn--ghost{background:#fff;color:#333;border-color:#cdd3de}
.mm-btn--ghost:hover{background:#f5f7fa;color:#333}
.mm-btn--inv{background:transparent;color:#fff;border-color:rgba(255,255,255,.5)}
.mm-btn--inv:hover{background:rgba(255,255,255,.12);color:#fff}
.mm-btn--sm{padding:4px 10px;font-size:11px}

/* --- card + table ------------------------------------------------ */
.mm-card{background:#fff;border:1px solid #e0e5ed;border-radius:4px;overflow:hidden;margin-bottom:14px}
.mm-card__hdr{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;
               padding:11px 14px;border-bottom:1px solid #e0e5ed;background:#f5f7fa}
.mm-card__title{font-size:13px;font-weight:600;color:#1a1a2e}
.mm-card__meta{font-size:11px;color:#888}
.mm-tablewrap{width:100%;overflow-x:auto;-webkit-overflow-scrolling:touch}
.mm-table{width:100%;border-collapse:collapse;font-size:11px}
.mm-table thead tr{background:#f5f7fa}
.mm-table th{padding:8px 12px;text-align:left;font-size:10px;font-weight:600;text-transform:uppercase;
              letter-spacing:.4px;color:#555;border-bottom:2px solid #e0e5ed;white-space:nowrap}
.mm-table td{padding:8px 12px;border-bottom:1px solid #e0e5ed;color:#1a1a2e;vertical-align:top}
.mm-table tbody tr:hover{background:#f9fafc}
.mm-table tbody tr:last-child td{border-bottom:none}
.mm-empty{text-align:center;padding:44px 20px;color:#888;font-size:12px}
.mm-empty b{display:block;font-size:13px;color:#555;margin-bottom:4px;font-weight:600}

/* --- cell pieces ------------------------------------------------- */
.mm-when{white-space:nowrap}
.mm-when__d{font-weight:600;color:#1a1a2e}
.mm-when__t{color:#888;font-size:10px;display:block;margin-top:1px}
.mm-who{font-weight:600;color:#05275C;word-break:break-word}
.mm-who__sub{display:block;color:#888;font-weight:400;font-size:10px;margin-top:1px}
.mm-what{font-weight:600;color:#1a1a2e;line-height:1.35}
.mm-what__sub{display:block;color:#888;font-weight:400;font-size:10px;margin-top:2px;line-height:1.4}
.mm-code{font-family:Consolas,"Courier New",monospace;font-size:10.5px;font-weight:600;
          background:rgba(23,77,164,.07);border:1px solid rgba(23,77,164,.15);color:#174DA4;padding:1px 5px;
          border-radius:0;white-space:nowrap;display:inline-block}
.mm-sub{display:block;color:#888;font-size:10px;margin-top:2px;line-height:1.4}
.mm-nil{color:#c7cdd6}
a.mm-link{text-decoration:none;cursor:pointer}
a.mm-link:hover{background:rgba(23,77,164,.14);border-color:rgba(23,77,164,.35);text-decoration:none}
.mm-many{font-weight:600;color:#05275C}

/* --- badges ------------------------------------------------------ */
.mm-badge{display:inline-block;font-size:9.5px;font-weight:600;padding:2px 6px;border-radius:0;
           text-transform:uppercase;letter-spacing:.3px;white-space:nowrap}
.mm-badge--navy{background:rgba(5,39,92,.08);color:#05275C;border:1px solid rgba(5,39,92,.18)}
.mm-badge--blue{background:#e8f0fc;color:#174DA4;border:1px solid #c7d8f3}
.mm-badge--green{background:#e6f4ea;color:#155724;border:1px solid #c3e6cb}
.mm-badge--amber{background:#fff8e1;color:#b45309;border:1px solid #f4dba6}
.mm-badge--red{background:#fef5f5;color:#912018;border:1px solid #f5c6cb}
.mm-badge--grey{background:#f1f3f7;color:#667085;border:1px solid #e0e5ed}
.mm-dur{font-size:10px;color:#a0a8b4;font-variant-numeric:tabular-nums}
.mm-dur--slow{color:#b45309;font-weight:600}

/* --- legend ------------------------------------------------------ */
.mm-legend{display:flex;gap:18px;flex-wrap:wrap;align-items:center;padding:9px 14px;
            background:#f9fafc;border-top:1px solid #e0e5ed;font-size:10.5px;color:#888;line-height:1.5}
.mm-legend__g{display:inline-flex;align-items:center;gap:5px;flex-wrap:wrap}
.mm-legend__g>b{color:#555;font-weight:600}

/* --- pager ------------------------------------------------------- */
.mm-pager{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;
           padding:10px 14px;border-top:1px solid #e0e5ed;background:#f9fafc}
.mm-pager__info{font-size:11px;color:#888}
.mm-pager__btns{display:flex;gap:4px;flex-wrap:wrap}
.mm-pg{padding:5px 10px;font-size:11px;border:1px solid #cdd3de;background:#fff;color:#333;
        text-decoration:none;border-radius:0;line-height:1.4}
.mm-pg:hover{background:#f5f7fa;color:#05275C;text-decoration:none}
.mm-pg--active{background:#05275C;color:#fff;border-color:#05275C;font-weight:600}
.mm-pg--active:hover{background:#05275C;color:#fff}
.mm-pg--off{color:#c7cdd6;pointer-events:none;background:#f9fafc}

/* --- detail modal ------------------------------------------------ */
.mm-ov{display:none;position:fixed;top:0;right:0;bottom:0;left:0;background:rgba(0,0,0,.45);
        z-index:1000;align-items:center;justify-content:center;padding:12px}
.mm-ov.is-open{display:flex}
.mm-modal{background:#fff;border-radius:2px;width:700px;max-width:100%;max-height:92vh;
           overflow-y:auto;box-shadow:0 12px 40px rgba(0,0,0,.18)}
.mm-modal__hdr{background:#05275C;color:#fff;padding:13px 18px;display:flex;align-items:center;
                justify-content:space-between;gap:10px;position:sticky;top:0;z-index:1}
.mm-modal__title{font-size:13px;font-weight:600}
.mm-modal__close{background:none;border:none;color:#fff;font-size:22px;cursor:pointer;line-height:1;padding:0;opacity:.8}
.mm-modal__close:hover{opacity:1}
.mm-modal__body{padding:16px 18px;min-height:90px}
.mm-modal__foot{padding:12px 18px;border-top:1px solid #e0e5ed;display:flex;justify-content:flex-end;gap:8px;background:#f9fafc}
.mm-sect{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.4px;color:#888;
          margin:0 0 7px;padding-bottom:5px;border-bottom:1px solid #e0e5ed}
.mm-sect--top{margin-top:0}
.mm-dl{display:grid;grid-template-columns:140px minmax(0,1fr);gap:6px 12px;font-size:11.5px;margin-bottom:18px}
.mm-dl dt{color:#888;font-weight:500}
.mm-dl dd{margin:0;color:#1a1a2e;word-break:break-word}
.mm-lead{font-size:13px;font-weight:600;color:#05275C;line-height:1.35;margin-bottom:3px}
.mm-leadsub{font-size:11px;color:#888;margin-bottom:16px}
.mm-rtbl{width:100%;border-collapse:collapse;font-size:11px;margin-bottom:18px}
.mm-rtbl th{text-align:left;font-size:9.5px;text-transform:uppercase;letter-spacing:.4px;color:#888;
             font-weight:600;padding:5px 8px;border-bottom:1px solid #e0e5ed;white-space:nowrap}
.mm-rtbl td{padding:6px 8px;border-bottom:1px solid #f0f2f5;vertical-align:top}
.mm-rtbl tr:last-child td{border-bottom:none}
.mm-raw{font-family:Consolas,"Courier New",monospace;font-size:10.5px;background:#f5f7fa;
         border:1px solid #e0e5ed;padding:10px;white-space:pre-wrap;word-break:break-word;color:#475569;margin-bottom:18px}
.mm-toggle{font-size:10px;color:#174DA4;cursor:pointer;font-weight:600;float:right;text-transform:none;letter-spacing:0}
.mm-toggle:hover{text-decoration:underline}

/* --- responsive -------------------------------------------------- */
@media(max-width:1100px){.mm-stats{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:760px){
    .mm-head{padding:12px 14px}
    .mm-head__ic{display:none}
    .mm-body{padding:12px 14px 20px}
    .mm-stats{grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}
    .mm-stat__value{font-size:19px}
    .mm-fg{flex:1 1 140px}
    .mm-fg input,.mm-fg select{min-width:0;width:100%}
    .mm-filters__end{margin-left:0;width:100%}
    .mm-filters__end .mm-btn{flex:1 1 auto;justify-content:center}
    .mm-dl{grid-template-columns:1fr;gap:2px 0}
    .mm-dl dd{margin-bottom:8px}
}
@media print{
    .mm-filters,.mm-quick,.mm-pager,.mm-head__r,.mm-ov{display:none!important}
    .mm-head{background:#05275C!important;-webkit-print-color-adjust:exact;print-color-adjust:exact}
    .mm-card{border:none}
}

/* --- class chips ------------------------------------------------- */
.mm-classes{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:14px}
.mm-class{flex:1 1 210px;min-width:0;background:#fff;border:1px solid #e0e5ed;border-radius:4px;
          padding:11px 13px;text-decoration:none;display:block;border-left:3px solid #cdd3de}
.mm-class:hover{border-color:#c7d8f3;text-decoration:none}
.mm-class--on{border-color:#05275C;border-left-color:#05275C;background:#f5f7fa}
.mm-class__n{font-size:20px;font-weight:700;color:#05275C;line-height:1;font-variant-numeric:tabular-nums}
.mm-class__t{font-size:11.5px;font-weight:600;color:#1a1a2e;margin-top:5px;line-height:1.3}
.mm-class__s{font-size:10.5px;color:#888;margin-top:3px;line-height:1.4}
.mm-class--loss{border-left-color:#b42318}
.mm-class--loss .mm-class__n{color:#b42318}
.mm-fix{font-size:10.5px;color:#555;line-height:1.45}
.mm-fix b{color:#1a1a2e;font-weight:600;display:block;margin-bottom:1px}

</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<div class="mm-head">
    <div class="mm-head__l">
        <div class="mm-head__ic">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                <path d="M12 9v4M12 17h.01"/><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/>
            </svg>
        </div>
        <div>
            <div class="mm-head__t">Missing Marks</div>
            <div class="mm-head__s"><asp:Literal ID="litHeadSub" runat="server" /></div>
        </div>
    </div>
    <div class="mm-head__r">
        <asp:Literal ID="litExportBtn" runat="server" />
    </div>
</div>

<div class="mm-body">

    <asp:Literal ID="litNotice" runat="server" />

    <asp:Panel ID="pnlMain" runat="server">

        <div class="mm-classes"><asp:Literal ID="litClasses" runat="server" /></div>

        <div class="mm-filters">
            <div class="mm-fg">
                <label class="mm-fg__label" for="fYear">Academic year</label>
                <select id="fYear"><asp:Literal ID="litYearOpts" runat="server" /></select>
            </div>
            <div class="mm-fg">
                <label class="mm-fg__label" for="fSem">Semester</label>
                <select id="fSem"><asp:Literal ID="litSemOpts" runat="server" /></select>
            </div>
            <div class="mm-fg">
                <label class="mm-fg__label" for="fProg">Programme</label>
                <select id="fProg"><asp:Literal ID="litProgOpts" runat="server" /></select>
            </div>
            <div class="mm-fg mm-fg--grow">
                <label class="mm-fg__label" for="fQ">Student or course</label>
                <input type="text" id="fQ" placeholder="reg. number or course code"
                       value="<asp:Literal ID='litQ' runat='server' />" onkeydown="if(event.keyCode==13){mmApply();return false;}" />
            </div>
            <div class="mm-filters__end">
                <button type="button" class="mm-btn mm-btn--primary" onclick="mmApply()">Search</button>
                <a href="MissingMarks.aspx" class="mm-btn mm-btn--ghost">Clear</a>
            </div>
        </div>

        <div class="mm-card">
            <div class="mm-card__hdr">
                <span class="mm-card__title"><asp:Literal ID="litCardTitle" runat="server" /></span>
                <span class="mm-card__meta">
                    <asp:Literal ID="litMeta" runat="server" />
                    &nbsp;&middot;&nbsp;Show
                    <select id="mmPageSize" onchange="mmApply()" style="border:1px solid #cdd3de;padding:2px 4px;font-size:11px;font-family:inherit">
                        <asp:Literal ID="litPsOpts" runat="server" />
                    </select>
                </span>
            </div>
            <div class="mm-tablewrap">
                <table class="mm-table">
                    <thead>
                        <tr>
                            <th style="width:150px">Student</th>
                            <th style="width:140px">Course</th>
                            <th style="width:120px">Term</th>
                            <th style="width:120px">Marks held</th>
                            <th style="width:210px">Why the student sees nothing</th>
                            <th>What closes it</th>
                        </tr>
                    </thead>
                    <tbody><asp:Literal ID="litRows" runat="server" /></tbody>
                </table>
            </div>
            <div class="mm-pager">
                <span class="mm-pager__info"><asp:Literal ID="litPagerInfo" runat="server" /></span>
                <span class="mm-pager__btns"><asp:Literal ID="litPager" runat="server" /></span>
            </div>
        </div>

    </asp:Panel>

</div>

<script type="text/javascript">
// The page is a GET report: every view is a link that can be bookmarked or shared.
function mmApply(){
    var p = [];
    function add(key, id){
        var el = document.getElementById(id);
        if (el && el.value) p.push(key + '=' + encodeURIComponent(el.value));
    }
    var cls = document.getElementById('mmClass');
    if (cls && cls.value) p.push('cls=' + encodeURIComponent(cls.value));
    add('year','fYear'); add('sem','fSem'); add('prog','fProg'); add('q','fQ');
    var ps = document.getElementById('mmPageSize');
    if (ps && ps.value && ps.value !== '50') p.push('ps=' + encodeURIComponent(ps.value));
    window.location.href = 'MissingMarks.aspx' + (p.length ? '?' + p.join('&') : '');
}
</script>

<input type="hidden" id="mmClass" value="" />
</asp:Content>
