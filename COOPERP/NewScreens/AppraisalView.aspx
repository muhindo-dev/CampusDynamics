<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AppraisalView.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalView" Title="View Appraisals - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<style>
/* ===== APPRAISAL VIEW / RECORDS ===== */
*,*::before,*::after{box-sizing:border-box;}
:root{--brand:#174DA4;--brand-light:#e8eef8;--brand-dark:#0f3670;--success:#28a745;--danger:#dc3545;--warning:#ffc107;--info:#17a2b8;--grey:#6c757d;--grey-light:#f4f5f7;--border:#dee2e6;--radius:6px;--shadow:0 1px 3px rgba(0,0,0,.08);}

/* ── Page header ── */
.pa-page-header{display:flex;align-items:center;gap:14px;margin-bottom:14px;flex-wrap:wrap;}
.pa-page-header__icon{width:44px;height:44px;border-radius:10px;background:var(--brand);display:flex;align-items:center;justify-content:center;color:#fff;flex-shrink:0;}
.pa-page-header__title{font-size:22px;font-weight:700;color:#1a1a1a;line-height:1.2;}
.pa-page-header__sub{font-size:12px;color:#888;margin-top:1px;}
.pa-page-header__actions{margin-left:auto;display:flex;gap:8px;align-items:center;flex-wrap:wrap;}

/* ── Quick stats bar ── */
.pa-list-stats{display:flex;gap:6px;flex-wrap:wrap;padding:8px 16px;background:#f8f9fa;border:1px solid #e0e5ed;border-radius:var(--radius);margin-bottom:10px;align-items:center;}
.pa-list-stat{display:inline-flex;align-items:center;gap:5px;padding:3px 10px;border-radius:10px;font-size:11px;font-weight:600;cursor:pointer;transition:opacity .1s;}
.pa-list-stat:hover{opacity:.8;}
.pa-list-stat--pending{background:#e2e3e5;color:#383d41;}
.pa-list-stat--emp{background:#cff4fc;color:#055160;}
.pa-list-stat--returned{background:#fff3cd;color:#856404;border:1px solid #f59e0b;}
.pa-list-stat--awaiting{background:#fff0e8;color:#92400e;border:1px solid #fcd9b6;}
.pa-list-stat--done{background:#d4edda;color:#155724;}
.pa-list-stat--cancelled{background:#f8d7da;color:#721c24;}
.pa-list-stat__lbl{font-weight:400;opacity:.8;}

/* ── Batch action bar ── */
.pa-batch-bar{display:none;align-items:center;gap:10px;padding:10px 16px;background:linear-gradient(90deg,#eef2ff,#e8eef8);border:1px solid #c5d3e8;border-radius:var(--radius);margin-bottom:10px;flex-wrap:wrap;}
.pa-batch-bar.is-active{display:flex;}
.pa-batch-count{font-size:12px;font-weight:700;color:var(--brand);min-width:90px;white-space:nowrap;}
.pa-batch-divider{width:1px;height:22px;background:rgba(23,77,164,.25);flex-shrink:0;}
.pa-batch-label{font-size:10px;text-transform:uppercase;letter-spacing:.4px;color:#888;font-weight:600;}
.pa-batch-actions{display:flex;gap:6px;flex-wrap:wrap;align-items:center;}
.pa-batch-btn{display:inline-flex;align-items:center;gap:5px;padding:5px 13px;font-size:11px;font-weight:600;border:none;border-radius:var(--radius);cursor:pointer;transition:all .15s;white-space:nowrap;}
.pa-batch-btn--hr{background:#d1ecf1;color:#0c5460;border:1px solid #bee5eb;}
.pa-batch-btn--hr:not(:disabled):hover{background:#bee5eb;}
.pa-batch-btn--warning{background:#fff3cd;color:#856404;border:1px solid #ffc107;}
.pa-batch-btn--warning:not(:disabled):hover{background:#ffe69c;}
.pa-batch-btn--success{background:#d4edda;color:#155724;border:1px solid #b2dfbc;}
.pa-batch-btn--success:not(:disabled):hover{background:#b2dfbc;}
.pa-batch-btn--danger{background:#f8d7da;color:#721c24;border:1px solid #f5c6cb;}
.pa-batch-btn--danger:not(:disabled):hover{background:#f1b0b7;}
.pa-batch-btn--outline{background:#fff;color:#555;border:1px solid var(--border);}
.pa-batch-btn--outline:hover{border-color:var(--brand);color:var(--brand);}
.pa-batch-btn:disabled{opacity:.4;cursor:not-allowed;}

/* ── Filter bar ── */
.pa-filter-bar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:10px 14px;background:#f8f9fa;border-bottom:1px solid #e0e5ed;}
.pa-filter-bar input,.pa-filter-bar select{font-size:12px;padding:6px 10px;border:1px solid var(--border);border-radius:var(--radius);background:#fff;color:#333;}
.pa-filter-bar input:focus,.pa-filter-bar select:focus{outline:none;border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.12);}
.pa-filter-bar input[type=text]{min-width:210px;}
.pa-filter-bar__count{margin-left:auto;font-size:11px;color:#888;white-space:nowrap;}

/* ── Card ── */
.cd-card{background:#fff;border:1px solid #e0e5ed;border-radius:var(--radius);margin-bottom:16px;overflow:visible;}
.cd-card--clip{overflow:hidden;}

/* ── Table ── */
.pa-table{width:100%;border-collapse:collapse;font-size:12px;}
.pa-table th{text-align:left;padding:8px 10px;border-bottom:2px solid #e0e5ed;color:#555;font-weight:600;text-transform:uppercase;font-size:10px;letter-spacing:.3px;background:#fafbfc;white-space:nowrap;}
.pa-table td{padding:7px 10px;border-bottom:1px solid #f5f7fa;color:#333;vertical-align:middle;}
.pa-table tr:last-child td{border-bottom:none;}
.pa-table tbody tr.pa-row:hover td{background:#f4f8ff;}
.pa-table tbody tr.pa-row.is-selected td{background:#ebf0ff;}
.pa-row{transition:background .1s;}
.pa-num{text-align:right;font-variant-numeric:tabular-nums;}
.pa-col-num{width:36px;text-align:center;color:#bbb;font-size:11px;}
.pa-empty-state{text-align:center;padding:48px 20px;color:#999;}
.pa-empty-state svg{color:#ddd;display:block;margin:0 auto 10px;}
.pa-empty-state p{margin:0;font-size:13px;}

/* ── Checkbox column ── */
.pa-col-chk{width:38px;text-align:center;}
.pa-col-chk input[type=checkbox]{cursor:pointer;width:14px;height:14px;accent-color:var(--brand);}

/* ── Action column ── */
.pa-col-act{width:108px;text-align:right;}
.pa-col-act-hdr{width:108px;text-align:right;}
.pa-act-menu{position:relative;display:inline-block;}
.pa-act-btn{display:inline-flex;align-items:center;gap:4px;padding:5px 10px;font-size:11px;font-weight:600;border:1px solid var(--border);border-radius:var(--radius);background:#fff;cursor:pointer;color:#555;white-space:nowrap;transition:all .15s;}
.pa-act-btn:hover,.pa-act-btn.is-open{border-color:var(--brand);color:var(--brand);background:var(--brand-light);}
.pa-act-btn svg{transition:transform .15s;}
.pa-act-btn.is-open svg{transform:rotate(180deg);}
.pa-act-drop{position:absolute;right:0;top:calc(100% + 3px);min-width:185px;background:#fff;border:1px solid #d0d7e3;border-radius:var(--radius);box-shadow:0 6px 20px rgba(0,0,0,.14);z-index:9999;display:none;overflow:hidden;}
.pa-act-drop.is-open{display:block;}
.pa-act-item{display:flex;align-items:center;gap:9px;padding:8px 14px;font-size:12px;color:#333;cursor:pointer;border:none;background:none;width:100%;text-align:left;text-decoration:none;transition:background .1s;line-height:1.3;}
.pa-act-item:hover{background:#f4f8ff;color:var(--brand);}
.pa-act-item svg{color:#aaa;flex-shrink:0;}
.pa-act-item:hover svg{color:var(--brand);}
.pa-act-item--hr{color:#0c5460;}
.pa-act-item--hr:hover{background:#e3f4f7;color:#0c5460;}
.pa-act-item--hr svg{color:#17a2b8;}
.pa-act-item--hr:hover svg{color:#0c5460;}
.pa-act-item--danger{color:var(--danger);}
.pa-act-item--danger:hover{background:#fff5f5;color:var(--danger);}
.pa-act-item--danger svg{color:var(--danger);}
.pa-act-sep{height:1px;background:var(--border);margin:3px 0;}

/* ── Employee link ── */
.pa-emp-link{color:#1a1a2e;text-decoration:none;font-weight:600;}
.pa-emp-link:hover{color:var(--brand);}
.pa-emp-code{font-size:10px;color:#aaa;letter-spacing:.2px;}

/* ── Record badge ── */
.pa-rec-badge{display:inline-block;padding:2px 8px;border-radius:3px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;white-space:nowrap;}
.pa-rec-badge--pending{background:#e2e3e5;color:#383d41;}
.pa-rec-badge--emp-prog{background:#cff4fc;color:#055160;}
.pa-rec-badge--returned{background:#fff3cd;color:#856404;border:1px solid #f59e0b;}
.pa-rec-badge--emp-done{background:#cce5ff;color:#004085;}
.pa-rec-badge--sup-prog{background:#fff3cd;color:#856404;}
.pa-rec-badge--completed{background:#fff0e8;color:#92400e;border:1px solid #fcd9b6;}
.pa-rec-badge--hr-reviewed{background:#d4edda;color:#155724;}
.pa-rec-badge--cancelled{background:#f8d7da;color:#721c24;}

/* ── Pager ── */
.pa-pager{display:flex;align-items:center;justify-content:space-between;padding:10px 16px;border-top:1px solid #e0e5ed;font-size:12px;color:#888;flex-wrap:wrap;gap:8px;}
.pa-pager__nav{display:flex;gap:4px;}
.pa-pager__btn{display:inline-flex;align-items:center;justify-content:center;min-width:30px;height:30px;padding:0 8px;border:1px solid var(--border);border-radius:var(--radius);font-size:12px;color:#555;text-decoration:none;transition:all .15s ease;}
.pa-pager__btn:hover{border-color:var(--brand);color:var(--brand);background:var(--brand-light);}
.pa-pager__btn--active{background:var(--brand);color:#fff;border-color:var(--brand);}
.pa-pager__btn--disabled{color:#ccc;cursor:default;pointer-events:none;}

/* ── Buttons ── */
.hr-btn{display:inline-flex;align-items:center;gap:6px;padding:7px 14px;font-size:12px;font-weight:600;border:none;border-radius:var(--radius);cursor:pointer;transition:all .15s ease;text-decoration:none;white-space:nowrap;}
.hr-btn--primary{background:var(--brand);color:#fff;}
.hr-btn--primary:hover{background:var(--brand-dark);}
.hr-btn--outline{background:transparent;border:1px solid var(--border);color:#555;}
.hr-btn--outline:hover{border-color:var(--brand);color:var(--brand);}
.hr-btn--danger{background:var(--danger);color:#fff;}
.hr-btn--danger:hover{background:#b02a37;}
.hr-btn--warning{background:var(--warning);color:#333;}
.hr-btn--warning:hover{background:#e0a800;}
.hr-btn--success{background:var(--success);color:#fff;}
.hr-btn--success:hover{background:#218838;}
.hr-btn--sm{padding:4px 10px;font-size:11px;}

/* ══════════════════════════════════════════════════════
   DETAIL VIEW
   ══════════════════════════════════════════════════════ */
.pa-detail-back{margin-bottom:14px;}
.pa-detail-back a{display:inline-flex;align-items:center;gap:6px;font-size:12px;font-weight:600;color:var(--brand);text-decoration:none;}
.pa-detail-back a:hover{text-decoration:underline;}

.pa-detail-header{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;background:#f8f9fa;border-bottom:1px solid #e0e5ed;flex-wrap:wrap;gap:10px;}
.pa-detail-header__name{font-size:18px;font-weight:700;color:#1a1a2e;margin:0;}
.pa-detail-header__meta{font-size:12px;color:#888;margin-top:2px;}

.pa-detail-section{background:#fff;border:1px solid #e0e5ed;border-radius:var(--radius);margin-bottom:14px;overflow:hidden;}
.pa-detail-section__hdr{padding:10px 16px;background:#fafbfc;border-bottom:1px solid #e0e5ed;font-size:12px;font-weight:700;color:#333;text-transform:uppercase;letter-spacing:.3px;}
.pa-detail-section__body{padding:14px 16px;}
.pa-detail-section--score{border-left:3px solid var(--brand);}
.pa-detail-empty{color:#999;font-size:13px;font-style:italic;margin:0;}

.pa-detail-table{font-size:12px;}
.pa-detail-table th{font-size:10px;}
.pa-cat-row td{background:#f4f5f7 !important;font-size:11px;border-bottom:1px solid #e0e5ed !important;}

/* ── Info grid ── */
.pa-info-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px 20px;}
@media(max-width:700px){.pa-info-grid{grid-template-columns:1fr;}}
.pa-info-item{display:flex;flex-direction:column;gap:2px;}
.pa-info-label{font-size:10px;text-transform:uppercase;letter-spacing:.3px;color:#888;font-weight:600;}
.pa-info-val{font-size:13px;color:#1a1a2e;}

/* ── Score grid ── */
.pa-score-grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:14px;}
@media(max-width:900px){.pa-score-grid{grid-template-columns:repeat(2,1fr);}}
@media(max-width:500px){.pa-score-grid{grid-template-columns:1fr;}}
.pa-score-item{background:#f8f9fa;border:1px solid #e0e5ed;border-radius:var(--radius);padding:10px 14px;text-align:center;}
.pa-score-item--final{background:var(--brand-light);border-color:var(--brand);}
.pa-score-item--class{background:#d4edda;border-color:#28a745;}
.pa-score-label{display:block;font-size:10px;text-transform:uppercase;letter-spacing:.3px;color:#888;margin-bottom:4px;}
.pa-score-val{display:block;font-size:20px;font-weight:700;color:#1a1a2e;}
.pa-score-item--final .pa-score-val{color:var(--brand);}
.pa-score-item--class .pa-score-val{color:#155724;}

/* ── Comment blocks ── */
.pa-comment-block{margin-bottom:12px;padding-bottom:12px;border-bottom:1px solid #f0f1f3;}
.pa-comment-block:last-child{border-bottom:none;margin-bottom:0;padding-bottom:0;}
.pa-comment-block__q{font-size:12px;font-weight:600;color:#333;margin-bottom:4px;}
.pa-comment-block__a{font-size:13px;color:#555;line-height:1.5;}

/* ── Timestamps ── */
.pa-timestamps{display:flex;gap:24px;font-size:11px;color:#888;padding-top:10px;border-top:1px solid #e0e5ed;}

/* ── Alert ── */
.pa-alert--error{padding:12px 16px;background:#f8d7da;color:#721c24;border-radius:var(--radius);margin-bottom:14px;font-size:13px;}
.pa-alert--info{padding:12px 16px;background:#cce5ff;color:#004085;border-radius:var(--radius);margin-bottom:14px;font-size:13px;}

/* ── Admin action bar (detail view) ── */
.pa-actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:14px;padding-top:14px;border-top:1px solid #e0e5ed;}

/* ── Modal ── */
.pa-modal-overlay{display:none;position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:1000;align-items:center;justify-content:center;}
.pa-modal-overlay.is-open{display:flex;}
.pa-modal{background:#fff;width:480px;max-width:92vw;border-radius:var(--radius);box-shadow:0 8px 32px rgba(0,0,0,.18);overflow:hidden;}
.pa-modal__head{padding:14px 18px;border-bottom:1px solid var(--border);display:flex;align-items:flex-start;justify-content:space-between;}
.pa-modal__title{font-size:14px;font-weight:700;color:#333;}
.pa-modal__close{background:none;border:none;font-size:20px;cursor:pointer;color:#999;padding:0 4px;line-height:1;flex-shrink:0;}
.pa-modal__close:hover{color:#333;}
.pa-modal__body{padding:18px;font-size:13px;color:#333;line-height:1.5;max-height:70vh;overflow-y:auto;}
.pa-modal__body textarea{width:100%;border:1px solid var(--border);border-radius:var(--radius);padding:8px 10px;font-size:12px;font-family:inherit;resize:vertical;box-sizing:border-box;min-height:80px;margin-top:8px;}
.pa-modal__body textarea:focus{outline:none;border-color:var(--brand);}
.pa-modal__foot{padding:12px 18px;border-top:1px solid var(--border);display:flex;justify-content:flex-end;gap:8px;}

/* ── Toast ── */
.pa-toast{position:fixed;bottom:24px;right:24px;padding:12px 20px;font-size:13px;font-weight:600;color:#fff;z-index:9999;border-radius:var(--radius);opacity:0;transform:translateY(12px);transition:all .3s ease;pointer-events:none;max-width:360px;box-shadow:0 4px 16px rgba(0,0,0,.15);}
.pa-toast.is-visible{opacity:1;transform:translateY(0);}
.pa-toast--ok{background:var(--success);}
.pa-toast--err{background:var(--danger);}

/* ── HR Wizard ── */
.pa-modal--hr{width:640px;max-width:96vw;}
.hw-steps-bar{display:flex;align-items:center;gap:0;margin-top:6px;width:110px;}
.hw-dot{width:24px;height:24px;border-radius:50%;border:2px solid var(--border);display:inline-flex;align-items:center;justify-content:center;font-size:10px;font-weight:700;color:#888;transition:all .2s;flex-shrink:0;}
.hw-dot.is-active{border-color:var(--brand);background:var(--brand);color:#fff;}
.hw-dot.is-done{border-color:var(--success);background:var(--success);color:#fff;}
.hw-line{flex:1;height:2px;background:var(--border);}
.hw-step-label{font-size:11px;text-transform:uppercase;letter-spacing:.5px;color:#888;font-weight:600;margin-bottom:14px;}
.hw-score-box{background:#f4f8ff;border:1px solid #c5d3e8;border-radius:var(--radius);padding:12px 16px;margin-bottom:16px;}
.hw-score-box__name{font-size:15px;font-weight:700;color:#05275C;margin-bottom:2px;}
.hw-score-box__meta{font-size:11px;color:#888;margin-bottom:10px;}
.hw-score-chips{display:flex;gap:8px;flex-wrap:wrap;}
.hw-chip{padding:3px 12px;border-radius:3px;font-size:12px;font-weight:700;background:#e8eef8;color:#174DA4;}
.hw-chip--pct{background:#174DA4;color:#fff;}
.hw-chip--cls{background:#d4edda;color:#155724;}
.hw-field-group{margin-bottom:16px;}
.hw-field-label{font-size:12px;font-weight:600;color:#333;margin-bottom:6px;}
.hw-field-sub{font-size:11px;color:#888;margin-bottom:8px;}
.sc-r--text{width:auto !important;min-width:60px;padding:0 14px;font-size:12px;}
.hw-radios{display:flex;gap:6px;flex-wrap:wrap;}
.hw-textarea{width:100%;border:1px solid var(--border);border-radius:var(--radius);padding:10px 12px;font-size:12px;font-family:inherit;resize:vertical;min-height:100px;color:#333;box-sizing:border-box;}
.hw-textarea:focus{outline:none;border-color:var(--brand);}
.hw-officer-badge{display:inline-flex;align-items:center;gap:8px;padding:7px 14px;background:#f0f4f8;border:1px solid #c5d3e8;border-radius:var(--radius);font-size:13px;font-weight:600;color:#05275C;}
.hw-sign-off-box{background:linear-gradient(135deg,#05275C 0%,#174DA4 100%);color:#fff;border-radius:var(--radius);padding:16px 20px;margin-top:16px;}
.hw-sign-off-box__title{font-size:13px;font-weight:700;margin-bottom:6px;}
.hw-sign-off-box__text{font-size:12px;opacity:.85;line-height:1.5;}
.pa-detail-section--hr{border-left:3px solid #17a2b8;}
.hw-review-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px 20px;}
@media(max-width:600px){.hw-review-grid{grid-template-columns:1fr;}}

/* ── Batch modal info box ── */
.pa-batch-info{padding:10px 14px;background:#f0f4ff;border:1px solid #c5d3e8;border-radius:4px;margin-bottom:12px;font-size:13px;color:#1a1a2e;}
.pa-batch-info strong{color:var(--brand);}
.pa-batch-emp-list{background:#fafbfc;border:1px solid #e0e5ed;border-radius:4px;padding:8px 12px;max-height:110px;overflow-y:auto;font-size:12px;color:#555;margin-bottom:14px;line-height:1.6;}

/* ── Unassigned reviewer ── */
.pa-list-stat--norev{background:#fff7ed;color:#9a3412;border:1px solid #fdba74;}
.pa-norev-badge{display:inline-block;padding:2px 7px;font-size:10px;font-weight:700;background:#fff7ed;color:#9a3412;border:1px solid #fdba74;border-radius:0;white-space:nowrap;}

/* ── Council-format header + KPA table + sign-off ── */
.pa-info-grid--3{grid-template-columns:repeat(3,minmax(0,1fr));}
@media(max-width:900px){.pa-info-grid--3{grid-template-columns:repeat(2,minmax(0,1fr));}}
@media(max-width:600px){.pa-info-grid--3{grid-template-columns:1fr;}}
.pa-b-table td{vertical-align:top;}
tr.pa-b-na td{background:#f3f4f6 !important;color:#9ca3af !important;}
.pa-tag{display:inline-block;margin-left:4px;padding:1px 5px;font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;background:#e8eef8;color:#174DA4;border-radius:0;vertical-align:1px;}
.pa-ev-list{display:flex;flex-direction:column;gap:3px;margin-top:4px;}
.pa-ev-file{display:inline-flex;align-items:center;gap:4px;font-size:11px;color:#174DA4;text-decoration:none;word-break:break-all;}
.pa-ev-file:hover{text-decoration:underline;}
.pa-ev-file svg{flex-shrink:0;color:#05275C;}
.pa-sign-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;}
@media(max-width:800px){.pa-sign-grid{grid-template-columns:1fr;}}
.pa-sign-item{border:1px solid #e0e5ed;border-radius:4px;padding:10px 12px;background:#fafbfc;}
.pa-sign-name{font-size:14px;font-weight:700;color:#05275C;margin-top:4px;}
.pa-sign-date{font-size:11px;color:#888;margin-top:2px;}
.pa-sign-none{font-size:12px;color:#999;font-style:italic;margin-top:4px;}
.pa-ack{display:inline-block;margin-top:4px;padding:2px 10px;font-size:12px;font-weight:700;border-radius:0;}
.pa-ack--agree{background:#d4edda;color:#155724;}
.pa-ack--disagree{background:#f8d7da;color:#721c24;}
.pa-ack-comment{font-size:12px;color:#444;margin-top:6px;line-height:1.45;}
.hw-ack-box{padding:10px 14px;background:#fafbfc;border:1px solid #e0e5ed;font-size:12px;color:#333;}

/* ── Print ── */
@media print{
    .pa-page-header__actions,.pa-detail-back,.cd-sidebar,.pa-filter-bar,.pa-pager,.pa-actions,.pa-modal-overlay,.pa-batch-bar,.pa-list-stats{display:none !important;}
    .pa-detail-section{break-inside:avoid;}
}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- ══════════════════════════════════════════════════════
     LIST VIEW
     ══════════════════════════════════════════════════════ -->
<asp:Panel ID="pnlList" runat="server">

<div class="pa-page-header">
    <div class="pa-page-header__icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
    </div>
    <div>
        <div class="pa-page-header__title">Appraisal Records</div>
        <div class="pa-page-header__sub">View and manage individual staff performance appraisals</div>
    </div>
    <div class="pa-page-header__actions">
        <a href="AppraisalSessions.aspx" class="hr-btn hr-btn--outline">
            <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
            Sessions
        </a>
        <a href="AppraisalReports.aspx" class="hr-btn hr-btn--outline">
            <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
            Reports
        </a>
        <a href="AppraisalDashboard.aspx" class="hr-btn hr-btn--primary">
            <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="9" y1="3" x2="9" y2="21"/></svg>
            Dashboard
        </a>
    </div>
</div>

<!-- Quick stats -->
<asp:Literal ID="litListStats" runat="server" />

<!-- Batch action toolbar (hidden until rows selected) -->
<div class="pa-batch-bar" id="batchBar">
    <span class="pa-batch-count" id="batchCount">0 selected</span>
    <div class="pa-batch-divider"></div>
    <span class="pa-batch-label">Actions for selected:</span>
    <div class="pa-batch-actions">
        <button type="button" class="pa-batch-btn pa-batch-btn--hr" id="btnBatchHr" onclick="batchHrReview()" disabled title="HR Final Review — eligible: COMPLETED or HR_REVIEWED">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><polyline points="17 11 19 13 23 9"/></svg>
            HR Final Review
        </button>
        <button type="button" class="pa-batch-btn pa-batch-btn--warning" id="btnBatchReturn" onclick="batchReturn()" disabled title="Return to Employee — eligible: Employee Submitted or Supervisor Reviewing">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="1 4 1 10 7 10"/><path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"/></svg>
            Return to Employee
        </button>
        <button type="button" class="pa-batch-btn pa-batch-btn--success" id="btnBatchReopen" onclick="batchReopen()" disabled title="Re-open for Supervisor — eligible: Awaiting HR or HR Reviewed">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 2v6h-6"/><path d="M21 13a9 9 0 1 1-3-7.7L21 8"/></svg>
            Re-open
        </button>
        <button type="button" class="pa-batch-btn pa-batch-btn--outline" id="btnBatchAssign" onclick="batchAssignReviewer()" disabled title="Assign the same reviewer to every selected record">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8.5" cy="7" r="4"/><line x1="20" y1="8" x2="20" y2="14"/><line x1="23" y1="11" x2="17" y2="11"/></svg>
            Assign Reviewer
        </button>
        <div class="pa-batch-divider"></div>
        <button type="button" class="pa-batch-btn pa-batch-btn--danger" id="btnBatchCancel" onclick="batchCancel()" disabled>
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
            Cancel Records
        </button>
    </div>
    <button type="button" class="pa-batch-btn pa-batch-btn--outline" style="margin-left:auto;" onclick="clearSelection()">
        Clear Selection
    </button>
</div>

<!-- Filter + Grid Card -->
<div class="cd-card">
    <div class="pa-filter-bar">
        <input type="text" id="txtSearch" placeholder="Search name or staff code..." onkeyup="debounceFilter()" autocomplete="off" />
        <select id="selStatus" onchange="applyFilter()">
            <option value="">All Statuses</option>
            <option value="PENDING">Not Started</option>
            <option value="EMPLOYEE_IN_PROGRESS">Employee In Progress</option>
            <option value="RETURNED">Returned to Employee</option>
            <option value="EMPLOYEE_SUBMITTED">Employee Submitted</option>
            <option value="SUPERVISOR_IN_PROGRESS">Supervisor Reviewing</option>
            <option value="COMPLETED">Awaiting HR Review</option>
            <option value="HR_REVIEWED">HR Reviewed</option>
            <option value="CANCELLED">Cancelled</option>
        </select>
        <select id="selCategory" onchange="applyFilter()">
            <option value="">All Categories</option>
            <option value="ACADEMIC">Academic</option>
            <option value="ADMINISTRATIVE">Administrative</option>
            <option value="SUPPORT">Support</option>
        </select>
        <select id="selSession" onchange="applyFilter()">
            <asp:Literal ID="litSessionOptions" runat="server" />
        </select>
        <select id="selReviewer" onchange="applyFilter()" title="Reviewer assignment">
            <option value="">All Reviewers</option>
            <option value="none">No reviewer assigned</option>
        </select>
        <select id="selPageSize" onchange="applyFilter()" title="Rows per page">
            <option value="25">25 / page</option>
            <option value="50">50 / page</option>
            <option value="100">100 / page</option>
            <option value="200">200 / page</option>
        </select>
        <button type="button" class="hr-btn hr-btn--outline hr-btn--sm" onclick="clearFilters()" title="Clear all filters">
            <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
            Clear
        </button>
        <span class="pa-filter-bar__count"><asp:Literal ID="litTotalCount" runat="server" Text="0" /> records</span>
    </div>

    <table class="pa-table">
        <thead>
            <tr>
                <th class="pa-col-chk"><input type="checkbox" id="chkAll" onclick="onSelectAll(this)" title="Select / deselect all on this page" /></th>
                <th class="pa-col-num">#</th>
                <th>Employee</th>
                <th style="width:140px;">Department</th>
                <th style="width:90px;">Category</th>
                <th style="width:130px;">Session</th>
                <th style="width:120px;">Reviewer</th>
                <th style="width:130px;">Status</th>
                <th style="text-align:right;width:68px;">Score</th>
                <th style="width:100px;">Classification</th>
                <th class="pa-col-act-hdr">Actions</th>
            </tr>
        </thead>
        <tbody id="gridBody">
            <asp:Literal ID="litGridBody" runat="server" />
        </tbody>
    </table>

    <div class="pa-pager">
        <span><asp:Literal ID="litPagerInfo" runat="server" /></span>
        <div class="pa-pager__nav">
            <asp:Literal ID="litPager" runat="server" />
        </div>
    </div>
</div>

</asp:Panel>

<!-- ══════════════════════════════════════════════════════
     DETAIL VIEW  (when ?rid= is present)
     ══════════════════════════════════════════════════════ -->
<asp:Panel ID="pnlDetail" runat="server" Visible="false">

<div class="pa-detail-back">
    <a href="AppraisalView.aspx">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg>
        Back to Records List
    </a>
</div>

<div class="pa-page-header">
    <div class="pa-page-header__icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><path d="M9 15l2 2 4-4"/></svg>
    </div>
    <div>
        <div class="pa-page-header__title">Appraisal Detail</div>
        <div class="pa-page-header__sub">Complete view of individual performance appraisal</div>
    </div>
    <div class="pa-page-header__actions">
        <button type="button" class="hr-btn hr-btn--outline" onclick="openPrintView()">
            <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
            Print / PDF
        </button>
    </div>
</div>

<asp:Literal ID="litDetailContent" runat="server" />

</asp:Panel>

<!-- ══════════════════════════════════════════════════════
     MODALS
     ══════════════════════════════════════════════════════ -->

<!-- Admin Action Modal (detail view) -->
<div class="pa-modal-overlay" id="adminModal">
    <div class="pa-modal">
        <div class="pa-modal__head">
            <span class="pa-modal__title" id="adminModalTitle">Action</span>
            <button type="button" class="pa-modal__close" onclick="closeAdminModal()">&times;</button>
        </div>
        <div class="pa-modal__body" id="adminModalBody"></div>
        <div class="pa-modal__foot" id="adminModalFoot"></div>
    </div>
</div>

<!-- HR Input Wizard Modal (individual) -->
<div class="pa-modal-overlay" id="hrModal">
    <div class="pa-modal pa-modal--hr">
        <div class="pa-modal__head">
            <div>
                <span class="pa-modal__title">HR Final Input</span>
                <div class="hw-steps-bar">
                    <span class="hw-dot is-active" id="hwDot1">1</span>
                    <div class="hw-line"></div>
                    <span class="hw-dot" id="hwDot2">2</span>
                    <div class="hw-line"></div>
                    <span class="hw-dot" id="hwDot3">3</span>
                </div>
            </div>
            <button type="button" class="pa-modal__close" onclick="closeHrModal()">&times;</button>
        </div>
        <div id="hwPanel1">
            <div class="pa-modal__body">
                <div class="hw-step-label">Step 1 of 3 &mdash; Review</div>
                <div class="hw-score-box">
                    <div class="hw-score-box__name" id="hwEmpName">—</div>
                    <div class="hw-score-box__meta" id="hwEmpMeta">—</div>
                    <div class="hw-score-chips" id="hwScoreChips"></div>
                </div>
                <div class="hw-field-group">
                    <div class="hw-field-label">Employee acknowledgement of the rating</div>
                    <div class="hw-field-sub">Recorded by the employee after the supervisor's rating (read-only).</div>
                    <div class="hw-ack-box" id="hwAck">&mdash;</div>
                </div>
            </div>
            <div class="pa-modal__foot">
                <button type="button" class="hr-btn hr-btn--outline" onclick="closeHrModal()">Cancel</button>
                <button type="button" class="hr-btn hr-btn--primary" onclick="hwStep1Next()">Next &rarr;</button>
            </div>
        </div>
        <div id="hwPanel2" style="display:none;">
            <div class="pa-modal__body">
                <div class="hw-step-label">Step 2 of 3 &mdash; HR Assessment</div>
                <div class="hw-field-group">
                    <div class="hw-field-label">HR Overall Performance Rating</div>
                    <div class="hw-field-sub">Academic: 5&nbsp;Exceptional &middot; 4&nbsp;Above Expectations &middot; 3&nbsp;Satisfactory &middot; 2&nbsp;Development Needed &middot; 1&nbsp;Unsatisfactory<br/>Administrative &amp; Support: 5&nbsp;Excellent &middot; 4&nbsp;Very Good &middot; 3&nbsp;Good &middot; 2&nbsp;Fair &middot; 1&nbsp;Poor</div>
                    <div class="sc-radios">
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="hr_rating" value="1" /> 1</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="hr_rating" value="2" /> 2</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="hr_rating" value="3" /> 3</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="hr_rating" value="4" /> 4</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="hr_rating" value="5" /> 5</label>
                    </div>
                </div>
                <div class="hw-field-group">
                    <div class="hw-field-label">HR Recommendation</div>
                    <div class="hw-radios">
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="hr_recommendation" value="CONFIRM" /> Confirm</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="hr_recommendation" value="EXTEND_PROBATION" /> Extend Probation</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="hr_recommendation" value="PIP" /> Perf. Improvement</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="hr_recommendation" value="PROMOTE" /> Promote</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="hr_recommendation" value="OTHER" /> Other</label>
                    </div>
                </div>
            </div>
            <div class="pa-modal__foot">
                <button type="button" class="hr-btn hr-btn--outline" onclick="hwGoPanel(1)">&larr; Back</button>
                <button type="button" class="hr-btn hr-btn--primary" onclick="hwStep2Next()">Next &rarr;</button>
            </div>
        </div>
        <div id="hwPanel3" style="display:none;">
            <div class="pa-modal__body">
                <div class="hw-step-label">Step 3 of 3 &mdash; Comments &amp; Sign-off</div>
                <div class="hw-field-group">
                    <div class="hw-field-label">HR Comments &amp; Observations</div>
                    <textarea class="hw-textarea" id="hrComments" placeholder="Provide your HR narrative, observations, and additional notes..." maxlength="2000"></textarea>
                </div>
                <div class="hw-field-group">
                    <div class="hw-field-label">HR Officer</div>
                    <div class="hw-officer-badge">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
                        <span id="hwOfficerName">HR Officer</span>
                    </div>
                </div>
                <div class="hw-sign-off-box">
                    <div class="hw-sign-off-box__title">Ready to sign off?</div>
                    <div class="hw-sign-off-box__text">By submitting, you confirm this appraisal has been reviewed by HR. The record will be marked as <strong>HR Reviewed</strong>.</div>
                </div>
            </div>
            <div class="pa-modal__foot">
                <button type="button" class="hr-btn hr-btn--outline" onclick="hwGoPanel(2)">&larr; Back</button>
                <button type="button" class="hr-btn hr-btn--success" id="btnHrSubmit" onclick="hrSubmit()">
                    <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>
                    Submit HR Review
                </button>
            </div>
        </div>
    </div>
</div>

<!-- Batch HR Review Modal -->
<div class="pa-modal-overlay" id="batchHrModal">
    <div class="pa-modal pa-modal--hr">
        <div class="pa-modal__head">
            <div>
                <span class="pa-modal__title">Batch HR Review</span>
                <div class="hw-steps-bar">
                    <span class="hw-dot is-active" id="bhwDot1">1</span>
                    <div class="hw-line"></div>
                    <span class="hw-dot" id="bhwDot2">2</span>
                </div>
            </div>
            <button type="button" class="pa-modal__close" onclick="closeBatchHrModal()">&times;</button>
        </div>
        <div id="bhwPanel1">
            <div class="pa-modal__body">
                <div class="hw-step-label">Step 1 of 2 &mdash; Assessment</div>
                <div class="hw-score-box">
                    <div class="hw-score-box__name" id="bhwCount">— records selected</div>
                    <div class="hw-score-box__meta">Same rating &amp; recommendation will apply to all eligible records</div>
                </div>
                <div class="hw-field-group">
                    <div class="hw-field-label">Overall Performance Rating</div>
                    <div class="hw-field-sub">Academic: 5&nbsp;Exceptional &middot; 4&nbsp;Above Expectations &middot; 3&nbsp;Satisfactory &middot; 2&nbsp;Development Needed &middot; 1&nbsp;Unsatisfactory<br/>Administrative &amp; Support: 5&nbsp;Excellent &middot; 4&nbsp;Very Good &middot; 3&nbsp;Good &middot; 2&nbsp;Fair &middot; 1&nbsp;Poor</div>
                    <div class="sc-radios">
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="bhr_rating" value="1" /> 1</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="bhr_rating" value="2" /> 2</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="bhr_rating" value="3" /> 3</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="bhr_rating" value="4" /> 4</label>
                        <label class="sc-r"><input type="radio" class="sc-r__inp" name="bhr_rating" value="5" /> 5</label>
                    </div>
                </div>
                <div class="hw-field-group">
                    <div class="hw-field-label">Recommendation</div>
                    <div class="hw-radios">
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="bhr_recommendation" value="CONFIRM" /> Confirm</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="bhr_recommendation" value="EXTEND_PROBATION" /> Extend Probation</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="bhr_recommendation" value="PIP" /> Perf. Improvement</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="bhr_recommendation" value="PROMOTE" /> Promote</label>
                        <label class="sc-r sc-r--text"><input type="radio" class="sc-r__inp" name="bhr_recommendation" value="OTHER" /> Other</label>
                    </div>
                </div>
            </div>
            <div class="pa-modal__foot">
                <button type="button" class="hr-btn hr-btn--outline" onclick="closeBatchHrModal()">Cancel</button>
                <button type="button" class="hr-btn hr-btn--primary" onclick="bhwStep1Next()">Next &rarr;</button>
            </div>
        </div>
        <div id="bhwPanel2" style="display:none;">
            <div class="pa-modal__body">
                <div class="hw-step-label">Step 2 of 2 &mdash; Employees &amp; Sign-off</div>
                <div id="bhwEmployeeList" class="pa-batch-emp-list"></div>
                <div class="hw-field-group">
                    <div class="hw-field-label">HR Comments (applies to all records)</div>
                    <textarea class="hw-textarea" id="bhrComments" placeholder="Optional batch HR comments..." maxlength="2000"></textarea>
                </div>
                <div class="hw-sign-off-box">
                    <div class="hw-sign-off-box__title">Ready to submit batch review?</div>
                    <div class="hw-sign-off-box__text">The same rating and recommendation will be applied to all eligible selected records (COMPLETED or HR_REVIEWED status). Records in other statuses will be skipped.</div>
                </div>
            </div>
            <div class="pa-modal__foot">
                <button type="button" class="hr-btn hr-btn--outline" onclick="bhwGoPanel(1)">&larr; Back</button>
                <button type="button" class="hr-btn hr-btn--success" id="btnBhrSubmit" onclick="bhrSubmit()">
                    <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>
                    Submit Batch HR Review
                </button>
            </div>
        </div>
    </div>
</div>

<!-- Change Supervisor Modal -->
<div class="pa-modal-overlay" id="supModal">
    <div class="pa-modal" style="width:500px;max-width:94vw;">
        <div class="pa-modal__head">
            <span class="pa-modal__title" id="supModalTitle">Change Supervisor</span>
            <button type="button" class="pa-modal__close" onclick="closeSupModal()">&times;</button>
        </div>
        <div class="pa-modal__body">
            <div style="margin-bottom:14px;padding:10px 14px;background:#f0f4f8;border:1px solid #c5d3e8;border-radius:4px;font-size:13px;color:#333;">
                <span id="supCurrentPrefix">Current supervisor:</span> <strong id="supCurrentLabel">—</strong>
            </div>
            <div style="font-size:11px;color:#888;margin:-6px 0 12px;">An employee can never be assigned as their own reviewer. Every change is recorded in the appraisal audit log.</div>
            <div class="hw-field-group">
                <div class="hw-field-label">Search &amp; Select New Supervisor</div>
                <input type="text" id="supSearchInput"
                    placeholder="Type name or staff code to filter..."
                    oninput="filterSupOptions()"
                    autocomplete="off"
                    style="width:100%;padding:7px 10px;border:1px solid var(--border);border-radius:var(--radius);font-size:12px;margin-bottom:8px;box-sizing:border-box;" />
                <select id="supSelect" size="9"
                    style="width:100%;border:1px solid var(--border);border-radius:var(--radius);font-size:12px;padding:4px;box-sizing:border-box;">
                    <option disabled value="">Loading employees...</option>
                </select>
                <div id="supMatchCount" style="font-size:11px;color:#888;margin-top:5px;"></div>
            </div>
        </div>
        <div class="pa-modal__foot">
            <button type="button" class="hr-btn hr-btn--outline" onclick="closeSupModal()">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnSupConfirm" onclick="confirmChangeSupervisor()">
                <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d='M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M23 21v-2a4 4 0 0 0-3-3.87'/><path d='M16 3.13a4 4 0 0 1 0 7.75'/></svg>
                Confirm Change
            </button>
        </div>
    </div>
</div>

<!-- Batch Action Modal (return / reopen / cancel) -->
<div class="pa-modal-overlay" id="batchModal">
    <div class="pa-modal" style="width:520px;max-width:94vw;">
        <div class="pa-modal__head">
            <span class="pa-modal__title" id="batchModalTitle">Batch Action</span>
            <button type="button" class="pa-modal__close" onclick="closeBatchModal()">&times;</button>
        </div>
        <div class="pa-modal__body">
            <div class="pa-batch-info" id="batchModalInfo"></div>
            <div class="pa-batch-emp-list" id="batchModalEmpList"></div>
            <div id="batchModalCommentWrap" style="display:none;">
                <label id="batchModalCommentLabel" style="display:block;font-size:12px;font-weight:600;color:#333;margin-bottom:6px;"></label>
                <textarea id="batchModalComment" class="hw-textarea" style="min-height:70px;" maxlength="1000"></textarea>
            </div>
        </div>
        <div class="pa-modal__foot">
            <button type="button" class="hr-btn hr-btn--outline" onclick="closeBatchModal()">Cancel</button>
            <button type="button" class="hr-btn" id="batchModalConfirmBtn" onclick="executeBatchAction()">Confirm</button>
        </div>
    </div>
</div>

<!-- Toast -->
<div class="pa-toast" id="paToast"></div>

<script type="text/javascript">
// ═══════════════════════════════════════════════════════
//  FILTER
// ═══════════════════════════════════════════════════════
var _filterTimer = null;
function debounceFilter() {
    if (_filterTimer) clearTimeout(_filterTimer);
    _filterTimer = setTimeout(function () { applyFilter(); }, 380);
}
function applyFilter() {
    var q   = (document.getElementById('txtSearch')   || {}).value || '';
    var st  = (document.getElementById('selStatus')   || {}).value || '';
    var cat = (document.getElementById('selCategory') || {}).value || '';
    var sid = (document.getElementById('selSession')  || {}).value || '';
    var rev = (document.getElementById('selReviewer') || {}).value || '';
    var ps  = (document.getElementById('selPageSize') || {}).value || '';
    var url = 'AppraisalView.aspx?';
    if (q)   url += 'q='      + encodeURIComponent(q)   + '&';
    if (st)  url += 'status=' + encodeURIComponent(st)  + '&';
    if (cat) url += 'cat='    + encodeURIComponent(cat) + '&';
    if (sid && sid !== '0') url += 'sid=' + sid + '&';
    if (rev === 'none') url += 'rev=none&';
    if (ps && ps !== '25') url += 'ps=' + encodeURIComponent(ps) + '&';
    window.location.href = url.replace(/&$/, '');
}
function clearFilters() {
    var el;
    el = document.getElementById('txtSearch');   if (el) el.value = '';
    el = document.getElementById('selStatus');   if (el) el.value = '';
    el = document.getElementById('selCategory'); if (el) el.value = '';
    el = document.getElementById('selSession');  if (el) el.value = '0';
    el = document.getElementById('selReviewer'); if (el) el.value = '';
    applyFilter();
}
// Restore filter values from URL
(function () {
    var params = new URLSearchParams(window.location.search);
    var el;
    el = document.getElementById('txtSearch');   if (el) el.value = params.get('q')      || '';
    el = document.getElementById('selStatus');   if (el) el.value = params.get('status') || '';
    el = document.getElementById('selCategory'); if (el) el.value = params.get('cat')    || '';
    el = document.getElementById('selSession');  if (el && params.get('sid')) el.value = params.get('sid');
    el = document.getElementById('selReviewer'); if (el) el.value = params.get('rev') === 'none' ? 'none' : '';
    el = document.getElementById('selPageSize'); if (el && params.get('ps')) el.value = params.get('ps');
    // Users without HR/admin access can browse but not act: no selection / batch bar.
    if (window.PA_READ_ONLY) {
        var hide = document.querySelectorAll('.pa-col-chk');
        for (var i = 0; i < hide.length; i++) hide[i].style.display = 'none';
    }
})();

// ═══════════════════════════════════════════════════════
//  SELECTION & BATCH BAR
// ═══════════════════════════════════════════════════════
var _sel = {};   // rid -> {status, empName, dept, session, score, pct, cls}
var _batchRids = [];
var _batchAction = null;
var _batchBusy = false;
var _bhrEligible = [];
var _bhrBusy = false;

function findParentTr(el) {
    while (el && el.tagName !== 'TR') el = el.parentNode;
    return el || null;
}

function rowData(tr) {
    if (!tr) return {};
    return {
        status:  (tr.getAttribute('data-status')  || '').toUpperCase(),
        empName:  tr.getAttribute('data-empname') || '',
        dept:     tr.getAttribute('data-dept')    || '',
        session:  tr.getAttribute('data-session') || '',
        score:    tr.getAttribute('data-score')   || '',
        pct:      tr.getAttribute('data-pct')     || '',
        cls:      tr.getAttribute('data-cls')     || '',
        ack:      tr.getAttribute('data-ack')     || '',
        reviewer: tr.getAttribute('data-reviewer') || ''
    };
}

function onSelectAll(cb) {
    var checks = document.querySelectorAll('.pa-row-chk');
    for (var i = 0; i < checks.length; i++) {
        var chk = checks[i];
        chk.checked = cb.checked;
        var rid = parseInt(chk.value, 10);
        var tr = findParentTr(chk);
        if (cb.checked) {
            _sel[rid] = rowData(tr);
            if (tr) tr.classList.add('is-selected');
        } else {
            delete _sel[rid];
            if (tr) tr.classList.remove('is-selected');
        }
    }
    updateBatchBar();
}

function onRowCheck(cb, evt) {
    if (evt) evt.stopPropagation();
    var rid = parseInt(cb.value, 10);
    var tr = findParentTr(cb);
    if (cb.checked) {
        _sel[rid] = rowData(tr);
        if (tr) tr.classList.add('is-selected');
    } else {
        delete _sel[rid];
        if (tr) tr.classList.remove('is-selected');
    }
    var all = document.querySelectorAll('.pa-row-chk');
    var chked = document.querySelectorAll('.pa-row-chk:checked');
    var ca = document.getElementById('chkAll');
    if (ca) {
        ca.indeterminate = chked.length > 0 && chked.length < all.length;
        ca.checked = all.length > 0 && chked.length === all.length;
    }
    updateBatchBar();
}

function clearSelection() {
    _sel = {};
    var checks = document.querySelectorAll('.pa-row-chk');
    for (var i = 0; i < checks.length; i++) checks[i].checked = false;
    var ca = document.getElementById('chkAll');
    if (ca) { ca.checked = false; ca.indeterminate = false; }
    var rows = document.querySelectorAll('.pa-row.is-selected');
    for (var j = 0; j < rows.length; j++) rows[j].classList.remove('is-selected');
    updateBatchBar();
}

function updateBatchBar() {
    var keys = Object.keys(_sel);
    var n = keys.length;
    var bar = document.getElementById('batchBar');
    var cnt = document.getElementById('batchCount');
    if (bar) bar.classList.toggle('is-active', n > 0);
    if (cnt) cnt.textContent = n + ' record' + (n === 1 ? '' : 's') + ' selected';

    var canHr = false, canReturn = false, canReopen = false, canCancel = false;
    for (var r in _sel) {
        var s = _sel[r].status;
        if (s === 'COMPLETED' || s === 'HR_REVIEWED') { canHr = true; canReopen = true; }
        if (s === 'EMPLOYEE_SUBMITTED' || s === 'SUPERVISOR_IN_PROGRESS') canReturn = true;
        if (s !== 'CANCELLED') canCancel = true;
    }
    var el;
    el = document.getElementById('btnBatchHr');     if (el) el.disabled = !canHr;
    el = document.getElementById('btnBatchReturn'); if (el) el.disabled = !canReturn;
    el = document.getElementById('btnBatchReopen'); if (el) el.disabled = !canReopen;
    el = document.getElementById('btnBatchCancel'); if (el) el.disabled = !canCancel;
    el = document.getElementById('btnBatchAssign'); if (el) el.disabled = n === 0;
}

function getEligibleRids(statuses) {
    var out = [];
    for (var r in _sel) {
        if (statuses.indexOf(_sel[r].status) >= 0) out.push(parseInt(r, 10));
    }
    return out;
}

// ═══════════════════════════════════════════════════════
//  ACTION DROPDOWN
// ═══════════════════════════════════════════════════════
function toggleActMenu(btn) {
    var drop = btn.nextElementSibling;
    var wasOpen = drop && drop.classList.contains('is-open');
    closeAllMenus();
    if (!wasOpen && drop) {
        drop.classList.add('is-open');
        btn.classList.add('is-open');
    }
}
function closeAllMenus() {
    var drops = document.querySelectorAll('.pa-act-drop.is-open');
    for (var i = 0; i < drops.length; i++) drops[i].classList.remove('is-open');
    var btns = document.querySelectorAll('.pa-act-btn.is-open');
    for (var j = 0; j < btns.length; j++) btns[j].classList.remove('is-open');
}
function openHrWizardFromRowBtn(btn) {
    var tr = findParentTr(btn);
    if (!tr) return;
    closeAllMenus();
    window.HR_WIZARD_RID   = parseInt(tr.getAttribute('data-rid') || '0', 10);
    window.HR_WIZARD_EMP   = tr.getAttribute('data-empname') || '—';
    window.HR_WIZARD_META  = (tr.getAttribute('data-dept') || '') + (tr.getAttribute('data-session') ? ' · ' + tr.getAttribute('data-session') : '');
    window.HR_WIZARD_SCORE = tr.getAttribute('data-score') || '';
    window.HR_WIZARD_PCT   = tr.getAttribute('data-pct')   || '';
    window.HR_WIZARD_CLS   = tr.getAttribute('data-cls')   || '';
    window.HR_WIZARD_ACK   = tr.getAttribute('data-ack')   || '';
    openHrWizardFromData();
}

// ═══════════════════════════════════════════════════════
//  BATCH OPERATIONS
// ═══════════════════════════════════════════════════════
function batchHrReview() {
    var eligible = getEligibleRids(['COMPLETED', 'HR_REVIEWED']);
    if (!eligible.length) { showPaToast('No selected records eligible for HR review', 'err'); return; }
    _bhrEligible = eligible;
    _bhrBusy = false;

    var inps = document.querySelectorAll('#batchHrModal input.sc-r__inp');
    for (var i = 0; i < inps.length; i++) inps[i].checked = false;
    var lbls = document.querySelectorAll('#batchHrModal label.sc-r');
    for (var j = 0; j < lbls.length; j++) lbls[j].classList.remove('is-checked');
    var ce = document.getElementById('bhrComments'); if (ce) ce.value = '';

    var ce2 = document.getElementById('bhwCount');
    if (ce2) ce2.textContent = eligible.length + ' record' + (eligible.length === 1 ? '' : 's') + ' eligible for HR review';

    var empHtml = '';
    for (var k = 0; k < Math.min(eligible.length, 12); k++) {
        var d = _sel[eligible[k]];
        if (d) empHtml += '<div>' + escHtml(d.empName) + (d.pct ? '&nbsp;<span style="color:#888;">(' + escHtml(d.pct) + '%)</span>' : '') + '</div>';
    }
    if (eligible.length > 12) empHtml += '<div style="color:#888;font-style:italic;">...and ' + (eligible.length - 12) + ' more</div>';
    var listEl = document.getElementById('bhwEmployeeList'); if (listEl) listEl.innerHTML = empHtml;

    bhwGoPanel(1);
    document.getElementById('batchHrModal').classList.add('is-open');
}

function closeBatchHrModal() {
    if (_bhrBusy) return;
    document.getElementById('batchHrModal').classList.remove('is-open');
}

function bhwGoPanel(n) {
    for (var i = 1; i <= 2; i++) {
        var p = document.getElementById('bhwPanel' + i);
        var d = document.getElementById('bhwDot'   + i);
        if (p) p.style.display = (i === n) ? '' : 'none';
        if (d) d.className = 'hw-dot' + (i < n ? ' is-done' : i === n ? ' is-active' : '');
    }
}

function bhwStep1Next() {
    if (!document.querySelector('#batchHrModal input[name="bhr_rating"]:checked'))
        { showPaToast('Please select a rating', 'err'); return; }
    if (!document.querySelector('#batchHrModal input[name="bhr_recommendation"]:checked'))
        { showPaToast('Please select a recommendation', 'err'); return; }
    bhwGoPanel(2);
}

function bhrSubmit() {
    if (_bhrBusy) return;
    var rat  = document.querySelector('#batchHrModal input[name="bhr_rating"]:checked');
    var rec  = document.querySelector('#batchHrModal input[name="bhr_recommendation"]:checked');
    if (!rat || !rec) { showPaToast('Please complete all fields', 'err'); return; }
    _bhrBusy = true;
    var btn = document.getElementById('btnBhrSubmit');
    if (btn) { btn.disabled = true; btn.textContent = 'Submitting...'; }
    adminAjax('batch_hr_input', {
        rids: _bhrEligible,
        rating: parseInt(rat.value, 10), recommendation: rec.value,
        comments: (document.getElementById('bhrComments') || {}).value || ''
    }, function(res) {
        _bhrBusy = false;
        if (btn) { btn.disabled = false; btn.textContent = 'Submit Batch HR Review'; }
        if (res.ok) {
            closeBatchHrModal();
            showPaToast((res.count || _bhrEligible.length) + ' record(s) HR-reviewed', 'ok');
            setTimeout(function() { location.reload(); }, 1500);
        } else { showPaToast(res.error || 'Failed', 'err'); }
    });
}

function batchReturn() {
    var rids = getEligibleRids(['EMPLOYEE_SUBMITTED', 'SUPERVISOR_IN_PROGRESS']);
    openBatchModal('Return to Employee', rids, 'return', true, 'Reason for returning (required)');
}
function batchReopen() {
    var rids = getEligibleRids(['COMPLETED', 'HR_REVIEWED']);
    openBatchModal('Re-open for Supervisor', rids, 'reopen', false, '');
}
function batchCancel() {
    var rids = getEligibleRids(['PENDING','EMPLOYEE_IN_PROGRESS','RETURNED','EMPLOYEE_SUBMITTED','SUPERVISOR_IN_PROGRESS','COMPLETED','HR_REVIEWED']);
    openBatchModal('Cancel Records', rids, 'cancel', true, 'Reason for cancellation (optional)');
}

function openBatchModal(title, rids, action, showComment, commentLabel) {
    if (!rids.length) { showPaToast('No eligible records for this action', 'err'); return; }
    _batchRids = rids; _batchAction = action; _batchBusy = false;

    document.getElementById('batchModalTitle').textContent = title;

    var infoEl = document.getElementById('batchModalInfo');
    if (infoEl) infoEl.innerHTML = '<strong>' + rids.length + ' record' + (rids.length === 1 ? '' : 's') + '</strong> will be affected by this action.';

    var empHtml = '';
    for (var j = 0; j < Math.min(rids.length, 10); j++) {
        var d = _sel[rids[j]];
        if (d) empHtml += '<div>' + escHtml(d.empName || '(unknown)') + (d.dept ? ' &mdash; <span style="color:#888;">' + escHtml(d.dept) + '</span>' : '') + '</div>';
    }
    if (rids.length > 10) empHtml += '<div style="color:#888;font-style:italic;">...and ' + (rids.length - 10) + ' more</div>';
    var listEl = document.getElementById('batchModalEmpList'); if (listEl) listEl.innerHTML = empHtml;

    var cw = document.getElementById('batchModalCommentWrap'); if (cw) cw.style.display = showComment ? '' : 'none';
    var cl = document.getElementById('batchModalCommentLabel'); if (cl) cl.textContent = commentLabel;
    var ca = document.getElementById('batchModalComment'); if (ca) ca.value = '';

    var btn = document.getElementById('batchModalConfirmBtn');
    if (btn) {
        btn.textContent = 'Confirm ' + title;
        btn.className = 'hr-btn ' + (action === 'cancel' ? 'hr-btn--danger' : action === 'return' ? 'hr-btn--warning' : 'hr-btn--primary');
    }
    document.getElementById('batchModal').classList.add('is-open');
}

function closeBatchModal() {
    if (_batchBusy) return;
    document.getElementById('batchModal').classList.remove('is-open');
}

function executeBatchAction() {
    if (_batchBusy || !_batchAction || !_batchRids.length) return;
    var comment = (document.getElementById('batchModalComment') || {}).value || '';
    if (_batchAction === 'return' && !comment.trim())
        { showPaToast('Please provide a reason for returning', 'err'); return; }
    _batchBusy = true;
    var btn = document.getElementById('batchModalConfirmBtn');
    if (btn) { btn.disabled = true; btn.textContent = 'Processing...'; }
    var ajaxMap = { 'return': 'batch_return', 'reopen': 'batch_reopen', 'cancel': 'batch_cancel' };
    adminAjax(ajaxMap[_batchAction], { rids: _batchRids, comment: comment.trim() }, function(res) {
        _batchBusy = false;
        if (btn) { btn.disabled = false; }
        if (res.ok) {
            closeBatchModal();
            showPaToast((res.count || _batchRids.length) + ' record(s) updated', 'ok');
            setTimeout(function() { location.reload(); }, 1500);
        } else { showPaToast(res.error || 'Failed', 'err'); }
    });
}

// ═══════════════════════════════════════════════════════
//  ADMIN ACTIONS (detail view)
// ═══════════════════════════════════════════════════════
var _adminBusy = false;

function openAdminModal(title, bodyHtml, footHtml) {
    document.getElementById('adminModalTitle').textContent = title;
    document.getElementById('adminModalBody').innerHTML = bodyHtml;
    document.getElementById('adminModalFoot').innerHTML = footHtml;
    document.getElementById('adminModal').classList.add('is-open');
}
function closeAdminModal() { document.getElementById('adminModal').classList.remove('is-open'); }

function adminReturnToEmployee(rid) {
    openAdminModal('Return to Employee',
        '<p style="margin:0 0 10px;">This will return the appraisal to the employee for revision. Please provide a reason:</p>' +
        '<textarea id="adminReturnComment" placeholder="Reason for returning..." maxlength="1000"></textarea>',
        '<button type="button" class="hr-btn hr-btn--outline" onclick="closeAdminModal()">Cancel</button>' +
        '<button type="button" class="hr-btn hr-btn--warning" id="btnAdminReturn" onclick="confirmAdminReturn(' + rid + ')">Return to Employee</button>'
    );
}
function confirmAdminReturn(rid) {
    if (_adminBusy) return;
    var comment = (document.getElementById('adminReturnComment') || {}).value || '';
    if (!comment.trim()) { showPaToast('Please provide a reason', 'err'); return; }
    _adminBusy = true;
    var btn = document.getElementById('btnAdminReturn');
    if (btn) { btn.disabled = true; btn.textContent = 'Returning...'; }
    adminAjax('admin_return', { rid: rid, comment: comment.trim() }, function(res) {
        _adminBusy = false; closeAdminModal();
        if (res.ok) { showPaToast(res.message || 'Returned', 'ok'); setTimeout(function() { location.reload(); }, 1500); }
        else showPaToast(res.error || 'Failed', 'err');
    });
}

function adminCancel(rid) {
    openAdminModal('Cancel Appraisal',
        '<p style="margin:0 0 10px;">Are you sure you want to cancel this appraisal? This action prevents further submissions.</p>' +
        '<textarea id="adminCancelComment" placeholder="Reason (optional)..." maxlength="1000"></textarea>',
        '<button type="button" class="hr-btn hr-btn--outline" onclick="closeAdminModal()">Cancel</button>' +
        '<button type="button" class="hr-btn hr-btn--danger" id="btnAdminCancel" onclick="confirmAdminCancel(' + rid + ')">Confirm Cancel</button>'
    );
}
function confirmAdminCancel(rid) {
    if (_adminBusy) return;
    _adminBusy = true;
    var comment = (document.getElementById('adminCancelComment') || {}).value || '';
    var btn = document.getElementById('btnAdminCancel');
    if (btn) { btn.disabled = true; btn.textContent = 'Cancelling...'; }
    adminAjax('admin_cancel', { rid: rid, comment: comment.trim() }, function(res) {
        _adminBusy = false; closeAdminModal();
        if (res.ok) { showPaToast(res.message || 'Cancelled', 'ok'); setTimeout(function() { location.reload(); }, 1500); }
        else showPaToast(res.error || 'Failed', 'err');
    });
}

function adminReopen(rid) {
    if (_adminBusy) return;
    if (!confirm('Re-open this appraisal? It will return to Supervisor In Progress status.')) return;
    _adminBusy = true;
    adminAjax('admin_reopen', { rid: rid }, function(res) {
        _adminBusy = false;
        if (res.ok) { showPaToast(res.message || 'Re-opened', 'ok'); setTimeout(function() { location.reload(); }, 1500); }
        else showPaToast(res.error || 'Failed', 'err');
    });
}

function openPrintView() {
    var params = new URLSearchParams(window.location.search);
    var rid = params.get('rid') || '';
    if (!rid) { alert('Please open a specific appraisal record first.'); return; }
    window.open('AppraisalPrint.aspx?rid=' + rid + '&autoprint=1', '_blank');
}

// ═══════════════════════════════════════════════════════
//  AJAX CORE
// ═══════════════════════════════════════════════════════
function adminAjax(action, data, callback) {
    var xhr = new XMLHttpRequest();
    xhr.open('POST', 'AppraisalView.aspx?ajax=' + action, true);
    xhr.setRequestHeader('Content-Type', 'application/json');
    var csrfMeta = document.querySelector('meta[name="csrf-token"]');
    if (csrfMeta) xhr.setRequestHeader('X-CSRF-Token', csrfMeta.getAttribute('content'));
    xhr.onreadystatechange = function() {
        if (xhr.readyState !== 4) return;
        if (xhr.status === 200) {
            try { callback(JSON.parse(xhr.responseText)); }
            catch(e) { callback({ ok: false, error: 'Invalid response' }); }
        } else { callback({ ok: false, error: 'Server error (HTTP ' + xhr.status + ')' }); }
    };
    xhr.send(JSON.stringify(data));
}

function showPaToast(msg, type) {
    var t = document.getElementById('paToast');
    if (!t) return;
    t.textContent = msg;
    t.className = 'pa-toast pa-toast--' + (type || 'ok') + ' is-visible';
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function() { t.classList.remove('is-visible'); }, 3800);
}

function escHtml(s) {
    return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

// ═══════════════════════════════════════════════════════
//  HR INPUT WIZARD
// ═══════════════════════════════════════════════════════
var _hrRid = 0;
var _hrBusy = false;

function openHrWizardFromData() {
    var rid = window.HR_WIZARD_RID || 0;
    if (!rid) { showPaToast('No appraisal selected', 'err'); return; }
    _hrRid = rid; _hrBusy = false;

    document.getElementById('hwEmpName').textContent = window.HR_WIZARD_EMP || '—';
    document.getElementById('hwEmpMeta').textContent = window.HR_WIZARD_META || '';

    var chips = '';
    if (window.HR_WIZARD_SCORE) chips += '<span class="hw-chip">Raw: ' + window.HR_WIZARD_SCORE + '</span>';
    if (window.HR_WIZARD_PCT)   chips += '<span class="hw-chip hw-chip--pct">' + window.HR_WIZARD_PCT + '%</span>';
    if (window.HR_WIZARD_CLS)   chips += '<span class="hw-chip hw-chip--cls">' + escHtml(window.HR_WIZARD_CLS) + '</span>';
    document.getElementById('hwScoreChips').innerHTML = chips;
    document.getElementById('hwOfficerName').textContent = window.HR_WIZARD_OFFICER || 'HR Officer';
    document.getElementById('hwAck').textContent = window.HR_WIZARD_ACK || 'Not yet acknowledged';

    var inps = document.querySelectorAll('#hrModal input.sc-r__inp');
    for (var i = 0; i < inps.length; i++) inps[i].checked = false;
    var lbls = document.querySelectorAll('#hrModal label.sc-r');
    for (var j = 0; j < lbls.length; j++) lbls[j].classList.remove('is-checked');
    document.getElementById('hrComments').value = '';

    hwGoPanel(1);
    document.getElementById('hrModal').classList.add('is-open');
}
function closeHrModal() { if (_hrBusy) return; document.getElementById('hrModal').classList.remove('is-open'); }

function hwGoPanel(n) {
    for (var i = 1; i <= 3; i++) {
        var p = document.getElementById('hwPanel' + i);
        var d = document.getElementById('hwDot'   + i);
        if (p) p.style.display = (i === n) ? '' : 'none';
        if (d) d.className = 'hw-dot' + (i < n ? ' is-done' : i === n ? ' is-active' : '');
    }
}
function hwStep1Next() {
    hwGoPanel(2);
}
function hwStep2Next() {
    if (!document.querySelector('#hrModal input[name="hr_rating"]:checked'))
        { showPaToast('Please select a rating', 'err'); return; }
    if (!document.querySelector('#hrModal input[name="hr_recommendation"]:checked'))
        { showPaToast('Please select a recommendation', 'err'); return; }
    hwGoPanel(3);
}
function hrSubmit() {
    if (_hrBusy) return;
    var rat = document.querySelector('#hrModal input[name="hr_rating"]:checked');
    var rec = document.querySelector('#hrModal input[name="hr_recommendation"]:checked');
    if (!rat || !rec) { showPaToast('Please complete all steps', 'err'); return; }
    _hrBusy = true;
    var btn = document.getElementById('btnHrSubmit');
    if (btn) { btn.disabled = true; btn.textContent = 'Submitting...'; }
    adminAjax('hr_input', {
        rid: _hrRid,
        rating: parseInt(rat.value, 10), recommendation: rec.value,
        comments: document.getElementById('hrComments').value.trim()
    }, function(res) {
        _hrBusy = false;
        if (btn) { btn.disabled = false; btn.textContent = 'Submit HR Review'; }
        if (res.ok) {
            closeHrModal();
            showPaToast(res.message || 'HR review submitted', 'ok');
            setTimeout(function() { location.reload(); }, 1500);
        } else showPaToast(res.error || 'Failed', 'err');
    });
}

// ═══════════════════════════════════════════════════════
//  CHANGE SUPERVISOR
// ═══════════════════════════════════════════════════════
var _supRid    = 0;
var _supRids   = [];   // non-empty = bulk "Assign Reviewer"
var _supBusy   = false;
var _supAll    = [];   // [{id, name, code}]

function changeSupervisorFromRow(btn) {
    var tr = findParentTr(btn);
    if (!tr) return;
    closeAllMenus();
    _supRid = parseInt(tr.getAttribute('data-rid') || '0', 10);
    _supRids = [];
    var reviewer = tr.getAttribute('data-reviewer') || '—';
    _openSupModal(reviewer);
}

function changeSupervisorFromDetailBtn(btn) {
    _supRid  = parseInt(btn.getAttribute('data-rid')      || '0', 10);
    _supRids = [];
    var reviewer = btn.getAttribute('data-reviewer') || '—';
    _openSupModal(reviewer);
}

function batchAssignReviewer() {
    var rids = [];
    for (var r in _sel) rids.push(parseInt(r, 10));
    if (!rids.length) { showPaToast('Select at least one record', 'err'); return; }
    _supRid = 0;
    _supRids = rids;
    _openSupModal(rids.length + ' selected record' + (rids.length === 1 ? '' : 's'));
}

function _openSupModal(currentSup) {
    _supBusy = false;
    var bulk = _supRids.length > 0;
    document.getElementById('supModalTitle').textContent = bulk ? 'Assign Reviewer' : 'Change Supervisor';
    document.getElementById('supCurrentPrefix').textContent = bulk ? 'Applies to:' : 'Current supervisor:';
    document.getElementById('supCurrentLabel').textContent = currentSup || '—';
    document.getElementById('supSearchInput').value = '';
    document.getElementById('supSelect').innerHTML = '<option disabled value="">Loading…</option>';
    document.getElementById('supMatchCount').textContent = '';
    document.getElementById('supModal').classList.add('is-open');

    adminAjax('get_employees', {}, function(res) {
        _supAll = res.employees || [];
        _renderSupOptions(_supAll);
        document.getElementById('supSearchInput').focus();
    });
}

function closeSupModal() {
    if (_supBusy) return;
    document.getElementById('supModal').classList.remove('is-open');
}

function _renderSupOptions(list) {
    var sel = document.getElementById('supSelect');
    var cnt = document.getElementById('supMatchCount');
    if (!list || !list.length) {
        sel.innerHTML = '<option disabled value="">No employees found</option>';
        if (cnt) cnt.textContent = '';
        return;
    }
    var html = '';
    for (var i = 0; i < list.length; i++) {
        var e = list[i];
        var label = escHtml(e.name) + (e.code ? ' (' + escHtml(e.code) + ')' : '');
        html += '<option value="' + e.id + '">' + label + '</option>';
    }
    sel.innerHTML = html;
    if (cnt) cnt.textContent = list.length + ' employee' + (list.length === 1 ? '' : 's') + ' shown';
}

function filterSupOptions() {
    var q = (document.getElementById('supSearchInput').value || '').toLowerCase().trim();
    if (!q) { _renderSupOptions(_supAll); return; }
    var filtered = [];
    for (var i = 0; i < _supAll.length; i++) {
        var e = _supAll[i];
        if ((e.name || '').toLowerCase().indexOf(q) >= 0 ||
            (e.code || '').toLowerCase().indexOf(q) >= 0)
            filtered.push(e);
    }
    _renderSupOptions(filtered);
}

function confirmChangeSupervisor() {
    if (_supBusy) return;
    var sel = document.getElementById('supSelect');
    var newId = sel && sel.value ? parseInt(sel.value, 10) : 0;
    if (!newId) { showPaToast('Please select a supervisor from the list', 'err'); return; }
    _supBusy = true;
    var btn = document.getElementById('btnSupConfirm');
    if (btn) { btn.disabled = true; btn.textContent = 'Saving…'; }
    var bulk = _supRids.length > 0;
    var action = bulk ? 'batch_change_supervisor' : 'admin_change_supervisor';
    var payload = bulk ? { rids: _supRids, new_reviewer_id: newId } : { rid: _supRid, new_reviewer_id: newId };
    adminAjax(action, payload, function(res) {
        _supBusy = false;
        if (btn) { btn.disabled = false; btn.textContent = 'Confirm Change'; }
        if (res.ok) {
            closeSupModal();
            showPaToast(res.message || 'Supervisor updated', 'ok');
            setTimeout(function() { location.reload(); }, 1400);
        } else { showPaToast(res.error || 'Failed', 'err'); }
    });
}

// ═══════════════════════════════════════════════════════
//  GLOBAL CLICK DELEGATION
// ═══════════════════════════════════════════════════════
document.addEventListener('click', function(e) {
    // Close act menus when clicking outside any pa-act-menu
    var inMenu = false, t = e.target;
    while (t && t !== document) {
        if (t.className && typeof t.className === 'string' && t.className.indexOf('pa-act-menu') >= 0) { inMenu = true; break; }
        t = t.parentNode;
    }
    if (!inMenu) closeAllMenus();

    // sc-r label delegation for both wizard modals
    var label = null, t2 = e.target;
    while (t2 && t2 !== document) {
        if (t2.tagName === 'LABEL' && t2.className && t2.className.indexOf('sc-r') >= 0) { label = t2; break; }
        t2 = t2.parentNode;
    }
    if (!label) return;
    var modalIds = ['hrModal', 'batchHrModal'];
    var theModal = null;
    for (var m = 0; m < modalIds.length; m++) {
        var mo = document.getElementById(modalIds[m]);
        if (mo && mo.contains(label)) { theModal = mo; break; }
    }
    if (!theModal) return;
    var inp = label.querySelector('input.sc-r__inp');
    if (!inp || !inp.name) return;
    var group = theModal.querySelectorAll('input.sc-r__inp[name="' + inp.name + '"]');
    for (var i = 0; i < group.length; i++) {
        group[i].checked = false;
        var gl = group[i].parentNode;
        if (gl && gl.classList) gl.classList.remove('is-checked');
    }
    inp.checked = true;
    label.classList.add('is-checked');
});
</script>

</asp:Content>
