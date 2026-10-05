<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AppraisalReports.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalReports" Title="Appraisal Reports - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===== APPRAISAL REPORTS ===== */
*,*::before,*::after{box-sizing:border-box;}
:root{--brand:#174DA4;--brand-light:#e8eef8;--brand-dark:#0f3670;--success:#28a745;--danger:#dc3545;--warning:#ffc107;--info:#17a2b8;--grey:#6c757d;--grey-light:#f4f5f7;--border:#dee2e6;--radius:6px;--shadow:0 1px 3px rgba(0,0,0,.08);}

/* ── Page header ── */
.pa-page-header{display:flex;align-items:center;gap:14px;margin-bottom:18px;flex-wrap:wrap;}
.pa-page-header__icon{width:44px;height:44px;border-radius:10px;background:var(--brand);display:flex;align-items:center;justify-content:center;color:#fff;flex-shrink:0;}
.pa-page-header__title{font-size:22px;font-weight:700;color:#1a1a1a;line-height:1.2;}
.pa-page-header__sub{font-size:12px;color:#888;margin-top:1px;}
.pa-page-header__actions{margin-left:auto;display:flex;gap:8px;align-items:center;}

/* ── Filter bar ── */
.pa-filter-bar{display:flex;align-items:center;gap:10px;flex-wrap:wrap;background:#fff;border:1px solid var(--border);border-radius:var(--radius);padding:10px 14px;margin-bottom:16px;}
.pa-filter-bar label{font-size:11px;font-weight:600;color:#555;white-space:nowrap;}
.pa-filter-bar select{font-size:12px;padding:5px 8px;border:1px solid var(--border);border-radius:var(--radius);background:#fff;color:#333;min-width:140px;}
.pa-filter-bar select:focus{outline:none;border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.15);}
.pa-filter-sep{width:1px;height:24px;background:var(--border);flex-shrink:0;}
@media(max-width:800px){.pa-filter-sep{display:none;}}

/* ── KPI Grid ── */
.pa-kpi-grid{display:grid;grid-template-columns:repeat(7,1fr);gap:10px;margin-bottom:16px;}
@media(max-width:1200px){.pa-kpi-grid{grid-template-columns:repeat(4,1fr);}}
@media(max-width:800px){.pa-kpi-grid{grid-template-columns:repeat(2,1fr);}}
@media(max-width:500px){.pa-kpi-grid{grid-template-columns:1fr;}}

.pa-kpi{background:#fff;border:1px solid #e0e5ed;border-top:3px solid transparent;padding:12px 14px;display:flex;align-items:center;gap:10px;border-radius:0 0 var(--radius) var(--radius);}
.pa-kpi--blue{border-top-color:var(--brand);}
.pa-kpi--green{border-top-color:var(--success);}
.pa-kpi--amber{border-top-color:#f59e0b;}
.pa-kpi--red{border-top-color:var(--danger);}
.pa-kpi--purple{border-top-color:#7c3aed;}
.pa-kpi--teal{border-top-color:#0d9488;}
.pa-kpi--cyan{border-top-color:var(--info);}

.pa-kpi__icon{width:34px;height:34px;border-radius:4px;display:flex;align-items:center;justify-content:center;flex-shrink:0;}
.pa-kpi--blue .pa-kpi__icon{background:#e8f0fe;color:var(--brand);}
.pa-kpi--green .pa-kpi__icon{background:#d4edda;color:#155724;}
.pa-kpi--amber .pa-kpi__icon{background:#fff3cd;color:#856404;}
.pa-kpi--red .pa-kpi__icon{background:#f8d7da;color:#721c24;}
.pa-kpi--purple .pa-kpi__icon{background:#ede9fe;color:#5b21b6;}
.pa-kpi--teal .pa-kpi__icon{background:#ccfbf1;color:#0d9488;}
.pa-kpi--cyan .pa-kpi__icon{background:#cff4fc;color:#055160;}

.pa-kpi__body{flex:1;min-width:0;}
.pa-kpi__val{font-size:20px;font-weight:700;color:#1a1a2e;line-height:1.15;}
.pa-kpi__label{font-size:9px;color:#888;text-transform:uppercase;letter-spacing:.3px;margin-top:1px;}

/* ── Sections ── */
.pa-section{background:#fff;border:1px solid #e0e5ed;margin-bottom:16px;border-radius:var(--radius);}
.pa-section__hdr{padding:11px 16px;border-bottom:1px solid #e0e5ed;font-size:12px;font-weight:700;color:#333;display:flex;align-items:center;gap:8px;text-transform:uppercase;letter-spacing:.3px;}
.pa-section__hdr svg{flex-shrink:0;color:#666;}
.pa-section__hdr-right{margin-left:auto;font-size:11px;font-weight:400;color:#888;text-transform:none;letter-spacing:0;}
.pa-section__body{padding:14px 16px;}

/* ── Two-column layout ── */
.pa-two-col{display:grid;grid-template-columns:1fr 1fr;gap:16px;}
@media(max-width:900px){.pa-two-col{grid-template-columns:1fr;}}

/* ── Tables ── */
.pa-table{width:100%;border-collapse:collapse;font-size:12px;}
.pa-table th{text-align:left;padding:7px 10px;border-bottom:2px solid #e0e5ed;color:#666;font-weight:600;text-transform:uppercase;font-size:10px;letter-spacing:.3px;white-space:nowrap;}
.pa-table td{padding:7px 10px;border-bottom:1px solid #f5f7fa;color:#333;vertical-align:middle;}
.pa-table tr:last-child td{border-bottom:none;}
.pa-table tr:hover td{background:#f9fbff;}
.pa-num{text-align:right;font-variant-numeric:tabular-nums;}

/* ── Mini progress bar ── */
.pa-mini-prog{width:55px;height:5px;background:#eee;border-radius:3px;display:inline-block;vertical-align:middle;margin-right:5px;}
.pa-mini-prog__bar{height:100%;border-radius:3px;transition:width .3s ease;}
.pa-mini-prog__text{font-size:10px;color:#555;font-weight:600;}

/* ── Distribution bars ── */
.pa-dist-row{display:flex;align-items:center;gap:10px;padding:5px 0;}
.pa-dist-row__label{font-size:12px;min-width:155px;color:#333;}
.pa-dist-row__count{font-size:12px;font-weight:700;min-width:28px;text-align:right;color:#1a1a2e;}
.pa-dist-row__bar{flex:1;height:10px;background:#eee;border-radius:5px;overflow:hidden;}
.pa-dist-row__fill{height:100%;border-radius:5px;transition:width .4s ease;}
.pa-dist-row__pct{font-size:11px;color:#888;min-width:40px;text-align:right;}

/* ── Record badge ── */
.pa-rec-badge{display:inline-block;padding:2px 7px;border-radius:3px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;white-space:nowrap;}
.pa-rec-badge--pending{background:#e2e3e5;color:#383d41;}
.pa-rec-badge--emp-prog{background:#cff4fc;color:#055160;}
.pa-rec-badge--emp-done{background:#cce5ff;color:#004085;}
.pa-rec-badge--sup-prog{background:#fff3cd;color:#856404;}
.pa-rec-badge--returned{background:#fef3cd;color:#856404;border:1px solid #f59e0b;}
.pa-rec-badge--completed{background:#fff0e8;color:#92400e;}
.pa-rec-badge--hr-reviewed{background:#d4edda;color:#155724;}
.pa-rec-badge--cancelled{background:#f8d7da;color:#721c24;}

/* ── Category badge ── */
.pa-cat-badge{display:inline-block;padding:2px 7px;border-radius:3px;font-size:10px;font-weight:600;letter-spacing:.2px;}
.pa-cat-badge--academic{background:#e8f0fe;color:#174DA4;}
.pa-cat-badge--administrative{background:#ede9fe;color:#5b21b6;}
.pa-cat-badge--support{background:#ccfbf1;color:#0d9488;}

/* ── Alert ── */
.pa-alert--error{padding:12px 16px;background:#f8d7da;color:#721c24;border-radius:var(--radius);margin-bottom:14px;font-size:13px;}

/* ── Buttons ── */
.hr-btn{display:inline-flex;align-items:center;gap:6px;padding:7px 14px;font-size:12px;font-weight:600;border:none;border-radius:var(--radius);cursor:pointer;transition:all .15s ease;text-decoration:none;}
.hr-btn--primary{background:var(--brand);color:#fff;}
.hr-btn--primary:hover{background:var(--brand-dark);}
.hr-btn--success{background:var(--success);color:#fff;}
.hr-btn--success:hover{background:#1e7e34;}
.hr-btn--outline{background:transparent;border:1px solid var(--border);color:#555;}
.hr-btn--outline:hover{border-color:var(--brand);color:var(--brand);}

/* ── Print styles ── */
@media print{
    .pa-filter-bar,.pa-page-header__actions,.hr-btn,.pa-section__hdr-right{display:none!important;}
    .pa-section{page-break-inside:avoid;}
    .pa-kpi-grid{grid-template-columns:repeat(7,1fr)!important;}
}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- ── Error placeholder ── -->
<asp:Literal ID="litError" runat="server" />

<!-- ── Page Header ── -->
<div class="pa-page-header">
    <div class="pa-page-header__icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><line x1="10" y1="9" x2="8" y2="9"/></svg>
    </div>
    <div>
        <div class="pa-page-header__title">Appraisal Reports</div>
        <div class="pa-page-header__sub">Aggregate reporting &amp; data export for performance appraisals</div>
    </div>
    <div class="pa-page-header__actions">
        <button type="button" class="hr-btn hr-btn--success" onclick="exportCsv()">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
            Export CSV
        </button>
        <button type="button" class="hr-btn hr-btn--outline" onclick="window.print()">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
            Print
        </button>
    </div>
</div>

<!-- ── Filter Bar ── -->
<div class="pa-filter-bar">
    <label>Session:</label>
    <select id="selSession" onchange="applyFilters()">
        <asp:Literal ID="litSessionOptions" runat="server" />
    </select>
    <div class="pa-filter-sep"></div>
    <label>Department:</label>
    <select id="selDept" onchange="applyFilters()">
        <asp:Literal ID="litDeptOptions" runat="server" />
    </select>
    <div class="pa-filter-sep"></div>
    <label>Category:</label>
    <select id="selCat" onchange="applyFilters()">
        <asp:Literal ID="litCatOptions" runat="server" />
    </select>
    <div class="pa-filter-sep"></div>
    <label>Status:</label>
    <select id="selStatus" onchange="applyFilters()">
        <asp:Literal ID="litStatusOptions" runat="server" />
    </select>
    <div class="pa-filter-sep"></div>
    <button type="button" class="hr-btn hr-btn--outline" onclick="clearFilters()" style="font-size:11px;padding:5px 10px;">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
        Clear
    </button>
</div>

<!-- ── KPI Summary Cards ── -->
<div class="pa-kpi-grid">
    <div class="pa-kpi pa-kpi--blue">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiTotal" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Total</div>
        </div>
    </div>
    <div class="pa-kpi pa-kpi--green">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiCompleted" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Finalized</div>
        </div>
    </div>
    <div class="pa-kpi pa-kpi--amber">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiOutstanding" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Not Yet Completed</div>
        </div>
    </div>
    <div class="pa-kpi pa-kpi--purple">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiRate" runat="server" Text="0%" /></div>
            <div class="pa-kpi__label">Completion</div>
        </div>
    </div>
    <div class="pa-kpi pa-kpi--teal">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiAvg" runat="server" Text="&#x2014;" /></div>
            <div class="pa-kpi__label">Avg Score</div>
        </div>
    </div>
    <div class="pa-kpi pa-kpi--cyan">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 6 13.5 15.5 8.5 10.5 1 18"/><polyline points="17 6 23 6 23 12"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiMax" runat="server" Text="&#x2014;" /></div>
            <div class="pa-kpi__label">Highest</div>
        </div>
    </div>
    <div class="pa-kpi pa-kpi--red">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 18 13.5 8.5 8.5 13.5 1 6"/><polyline points="17 18 23 18 23 12"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiMin" runat="server" Text="&#x2014;" /></div>
            <div class="pa-kpi__label">Lowest</div>
        </div>
    </div>
</div>

<!-- ── Score Distribution & Department Summary (Two-Column) ── -->
<div class="pa-two-col">

    <!-- Score Distribution -->
    <div class="pa-section">
        <div class="pa-section__hdr">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
            Score Distribution
            <span class="pa-section__hdr-right">Completed appraisals only</span>
        </div>
        <div class="pa-section__body">
            <asp:Literal ID="litDistBars" runat="server" />
        </div>
    </div>

    <!-- Department Summary -->
    <div class="pa-section">
        <div class="pa-section__hdr">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>
            Department Summary
        </div>
        <div class="pa-section__body" style="padding:0;">
            <div style="overflow-x:auto;">
            <table class="pa-table">
                <thead>
                    <tr>
                        <th>Department</th>
                        <th style="text-align:right;">Total</th>
                        <th style="text-align:right;">Done</th>
                        <th style="text-align:right;">Open</th>
                        <th>Progress</th>
                        <th style="text-align:right;">Avg</th>
                        <th style="text-align:right;">Min / Max</th>
                    </tr>
                </thead>
                <tbody>
                    <asp:Literal ID="litDeptRows" runat="server" />
                </tbody>
            </table>
            </div>
        </div>
    </div>
</div>

<!-- ── Detailed Records (Full Width) ── -->
<div class="pa-section">
    <div class="pa-section__hdr">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
        Individual Records
        <span class="pa-section__hdr-right"><asp:Literal ID="litRecordCount" runat="server" Text="0" /> records</span>
    </div>
    <div class="pa-section__body" style="padding:0;">
        <div style="overflow-x:auto;">
        <table class="pa-table" id="tblRecords">
            <thead>
                <tr>
                    <th style="width:36px;">#</th>
                    <th>Employee</th>
                    <th>Department</th>
                    <th>Category</th>
                    <th>Status</th>
                    <th style="text-align:right;">Score</th>
                    <th>Classification</th>
                    <th>Supervisor</th>
                    <th>Session</th>
                </tr>
            </thead>
            <tbody>
                <asp:Literal ID="litRecordRows" runat="server" />
            </tbody>
        </table>
        </div>
    </div>
</div>

<script type="text/javascript">
/* ── Filter navigation ── */
function applyFilters() {
    var sid  = document.getElementById('selSession').value;
    var dept = document.getElementById('selDept').value;
    var cat  = document.getElementById('selCat').value;
    var st   = document.getElementById('selStatus').value;

    var params = [];
    if (sid && sid !== '0') params.push('sid=' + encodeURIComponent(sid));
    if (dept) params.push('dept=' + encodeURIComponent(dept));
    if (cat)  params.push('cat=' + encodeURIComponent(cat));
    if (st)   params.push('st=' + encodeURIComponent(st));

    var url = 'AppraisalReports.aspx';
    if (params.length > 0) url += '?' + params.join('&');
    window.location.href = url;
}

function clearFilters() {
    window.location.href = 'AppraisalReports.aspx';
}

/* ── CSV export ── */
function exportCsv() {
    var sid  = document.getElementById('selSession').value;
    var dept = document.getElementById('selDept').value;
    var cat  = document.getElementById('selCat').value;
    var st   = document.getElementById('selStatus').value;

    var params = ['ajax=exportcsv'];
    if (sid && sid !== '0') params.push('sid=' + encodeURIComponent(sid));
    if (dept) params.push('dept=' + encodeURIComponent(dept));
    if (cat)  params.push('cat=' + encodeURIComponent(cat));
    if (st)   params.push('st=' + encodeURIComponent(st));

    window.location.href = 'AppraisalReports.aspx?' + params.join('&');
}
</script>

</asp:Content>
