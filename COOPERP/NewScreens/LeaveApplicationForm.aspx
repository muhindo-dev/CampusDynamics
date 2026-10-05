<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="LeaveApplicationForm.aspx.cs" Inherits="COOPERP_NewScreens_LeaveApplicationForm" Title="Leave Application - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
:root{--brand:#174DA4;--brand-dk:#05275C;--danger:#dc2626;--success:#16a34a;--warn:#b45309;--surf:#f5f7fa;--bdr:#e0e5ed;--bdr-in:#cdd3de;--txt:#1a1a2e;--muted:#64748b;}

/* ── Page header ─────────────────────────────────────────────────────────── */
.lf-header{background:#fff;border-bottom:1px solid var(--bdr);padding:14px 28px;display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;}
.lf-header__left{display:flex;align-items:center;gap:12px;min-width:0;}
.lf-back{display:inline-flex;align-items:center;gap:4px;font-size:11px;color:var(--muted);text-decoration:none;border:1px solid var(--bdr);padding:4px 10px;background:#fff;white-space:nowrap;}
.lf-back:hover{background:var(--surf);color:var(--brand);}
.lf-header__title{font-size:16px;font-weight:700;color:var(--txt);margin:0;}
.lf-header__sub{font-size:11px;color:var(--muted);margin-top:2px;}
.lf-header__actions{display:flex;align-items:center;gap:8px;flex-wrap:wrap;}

/* ── Status badge ─────────────────────────────────────────────────────────── */
.lf-status{display:inline-block;padding:3px 10px;border-radius:0;font-size:11px;font-weight:700;vertical-align:middle;}
.lf-status--draft        {background:#f1f5f9;color:#64748b;}
.lf-status--submitted    {background:#fef9c3;color:#b45309;}
.lf-status--hod_approved {background:#e0f2fe;color:#0369a1;}
.lf-status--hod_declined {background:#fef2f2;color:#dc2626;}
.lf-status--hr_approved  {background:#ede9fe;color:#5b21b6;}
.lf-status--hr_declined  {background:#fef2f2;color:#dc2626;}
.lf-status--vc_granted   {background:#dcfce7;color:#15803d;}
.lf-status--vc_not_granted{background:#fef2f2;color:#dc2626;}
.lf-status--vc_postponed {background:#fff7ed;color:#b45309;}
.lf-status--cancelled    {background:#f1f5f9;color:#94a3b8;}

/* ── Workflow timeline ───────────────────────────────────────────────────── */
.lf-timeline{background:#fff;border-bottom:1px solid var(--bdr);padding:12px 28px;display:flex;align-items:center;gap:0;overflow-x:auto;}
.lf-step{display:flex;align-items:center;gap:0;flex-shrink:0;}
.lf-step__dot{width:26px;height:26px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;border:2px solid var(--bdr);background:#fff;color:var(--muted);flex-shrink:0;}
.lf-step__dot--done{background:var(--success);border-color:var(--success);color:#fff;}
.lf-step__dot--active{background:var(--brand);border-color:var(--brand);color:#fff;box-shadow:0 0 0 3px rgba(23,77,164,.2);}
.lf-step__dot--declined{background:var(--danger);border-color:var(--danger);color:#fff;}
.lf-step__info{margin-left:8px;}
.lf-step__label{font-size:10px;font-weight:600;color:var(--txt);}
.lf-step__sub{font-size:9px;color:var(--muted);}
.lf-step__arrow{width:40px;height:2px;background:var(--bdr);margin:0 6px;flex-shrink:0;}
.lf-step__arrow--done{background:var(--success);}

/* ── Document area ───────────────────────────────────────────────────────── */
.lf-doc{max-width:880px;margin:20px auto;padding:0 24px 40px;box-sizing:border-box;}

/* ── Letterhead ──────────────────────────────────────────────────────────── */
.lf-letterhead{background:#fff;border:1px solid var(--bdr);border-top:3px solid var(--brand-dk);border-radius:4px;padding:14px 20px;}
.lf-letterhead__row{display:flex;align-items:center;gap:14px;}
.lf-letterhead__crest{width:46px;height:46px;object-fit:contain;flex-shrink:0;}
.lf-letterhead__org{font-size:14px;font-weight:700;color:var(--brand-dk);letter-spacing:.6px;text-transform:uppercase;}
.lf-letterhead__title{font-size:12px;font-weight:600;color:var(--txt);margin-top:2px;}
.lf-letterhead__ref{font-size:10px;color:var(--muted);margin-top:2px;}
.lf-policy{display:flex;gap:10px;align-items:flex-start;margin-top:12px;background:var(--surf);border:1px solid var(--bdr);border-left:3px solid var(--brand);padding:9px 12px;font-size:11px;color:#374151;line-height:1.6;}
.lf-policy svg{flex-shrink:0;margin-top:2px;color:var(--brand);}
.lf-policy ul{margin:0;padding:0 0 0 14px;}
.lf-policy strong{color:var(--txt);}

/* ── Section card ─────────────────────────────────────────────────────────── */
.lf-section{background:#fff;border:1px solid var(--bdr);border-radius:4px;margin-top:14px;overflow:hidden;}
.lf-section.lf-section--active{border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.10);}
.lf-section.lf-section--completed{border-color:#86efac;}
.lf-section.lf-section--declined{border-color:#fca5a5;}
.lf-section.lf-section--locked{opacity:.65;}
.lf-section__head{padding:11px 18px;display:flex;align-items:center;gap:10px;border-bottom:1px solid var(--bdr);}
.lf-section--active   .lf-section__head{background:#eff6ff;}
.lf-section--completed .lf-section__head{background:#f0fdf4;}
.lf-section--declined  .lf-section__head{background:#fef2f2;}
.lf-section--locked    .lf-section__head{background:var(--surf);}
.lf-section__num{width:24px;height:24px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;color:#fff;flex-shrink:0;}
.lf-section--active    .lf-section__num{background:var(--brand);}
.lf-section--completed .lf-section__num{background:var(--success);}
.lf-section--declined  .lf-section__num{background:var(--danger);}
.lf-section--locked    .lf-section__num{background:var(--muted);}
.lf-section__title{font-size:13px;font-weight:600;color:var(--txt);}
.lf-section__badge{margin-left:auto;font-size:10px;font-weight:700;padding:2px 8px;border-radius:0;white-space:nowrap;}
.badge--waiting{background:#fef9c3;color:#b45309;}
.badge--approved{background:#dcfce7;color:#15803d;}
.badge--declined{background:#fef2f2;color:#dc2626;}
.badge--locked{background:#f1f5f9;color:#94a3b8;}
.badge--granted{background:#dcfce7;color:#15803d;}
.badge--notgranted{background:#fef2f2;color:#dc2626;}
.badge--postponed{background:#fff7ed;color:#b45309;}
.lf-section__body{padding:16px 18px;}

/* ── Generic grid + read-only field (sections 2-4) ───────────────────────── */
.lf-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px 18px;}
.lf-grid--3{grid-template-columns:repeat(3,minmax(0,1fr));}
.lf-span{grid-column:1/-1;}
.lf-field{min-width:0;}
.lf-field > label,.lf-label{display:block;font-size:11px;font-weight:600;color:#374151;margin-bottom:4px;}
.lf-read-val{font-size:12px;color:var(--txt);padding:6px 0;border-bottom:1px solid var(--bdr);min-height:30px;box-sizing:border-box;word-wrap:break-word;}
.lf-read-val--empty{color:var(--muted);}

/* ── Section 1 editable form ─────────────────────────────────────────────── */
.lf-intro{font-size:11px;color:var(--muted);margin:0 0 4px;}
.lf-req{color:var(--danger);font-weight:700;margin-left:2px;}
.lf-opt{font-weight:400;color:#94a3b8;}
.lf-group{padding:14px 0 16px;border-bottom:1px solid #eef1f5;}
.lf-group:last-child{border-bottom:none;padding-bottom:2px;}
.lf-group__head{display:flex;align-items:center;gap:8px;margin-bottom:12px;}
.lf-group__icon{width:24px;height:24px;background:var(--brand-dk);color:#fff;display:flex;align-items:center;justify-content:center;flex-shrink:0;border-radius:0;}
.lf-group__title{font-size:12px;font-weight:700;color:var(--brand-dk);text-transform:uppercase;letter-spacing:.4px;}
.lf-group__note{font-size:11px;color:var(--muted);margin-left:auto;text-align:right;}
.lf-form input[type=text],.lf-form input[type=date],.lf-form input[type=tel],.lf-form select,.lf-form textarea{width:100%;height:34px;border:1px solid var(--bdr-in);border-radius:0;font-size:12px;font-family:inherit;padding:0 10px;outline:none;box-sizing:border-box;background:#fff;color:var(--txt);}
.lf-form textarea{height:60px;padding:7px 10px;resize:vertical;line-height:1.5;}
.lf-form input:focus,.lf-form select:focus,.lf-form textarea:focus{border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.12);}
.lf-form .is-invalid input,.lf-form .is-invalid select,.lf-form .is-invalid textarea{border-color:var(--danger);}
.lf-form .is-invalid .lf-radios{border-color:var(--danger);}
.lf-hint{font-size:10px;color:var(--muted);margin-top:4px;line-height:1.45;}
.lf-err{display:none;font-size:11px;color:var(--danger);margin-top:4px;font-weight:500;}
.is-invalid .lf-err{display:block;}
.lf-leave-grid{display:grid;grid-template-columns:minmax(0,1.15fr) minmax(0,1fr);gap:14px 20px;}

/* Radio list (leave type, VC decision) */
.lf-radios{border:1px solid var(--bdr-in);background:#fff;}
.lf-radio{display:flex;align-items:flex-start;gap:9px;padding:7px 10px;cursor:pointer;border-bottom:1px solid #eef1f5;border-left:3px solid transparent;}
.lf-radio:last-child{border-bottom:none;}
.lf-radio:hover{background:var(--surf);}
.lf-radio input[type=radio]{margin:2px 0 0;width:14px;height:14px;flex-shrink:0;accent-color:var(--brand);cursor:pointer;}
.lf-radio__text{display:block;min-width:0;}
.lf-radio__title{display:block;font-size:12px;font-weight:600;color:var(--txt);line-height:1.3;}
.lf-radio__desc{display:block;font-size:10.5px;color:var(--muted);line-height:1.35;margin-top:1px;}
.lf-radio.is-checked{background:#eff6ff;border-left-color:var(--brand);}
.lf-radio.is-checked .lf-radio__title{color:var(--brand-dk);}
.lf-radios--inline{display:flex;flex-wrap:wrap;}
.lf-radios--inline .lf-radio{flex:1 1 0;min-width:120px;border-bottom:none;border-right:1px solid #eef1f5;}
.lf-radios--inline .lf-radio:last-child{border-right:none;}

/* Days summary */
.lf-days{display:flex;align-items:flex-start;gap:8px;margin-top:10px;padding:9px 11px;background:var(--surf);border:1px solid var(--bdr);font-size:12px;color:var(--muted);line-height:1.45;}
.lf-days svg{flex-shrink:0;margin-top:1px;}
.lf-days strong{color:var(--txt);}
.lf-days__n{display:inline-block;background:var(--brand-dk);color:#fff;font-weight:700;padding:1px 7px;margin-left:2px;white-space:nowrap;}
.lf-days--ok{background:#eff6ff;border-color:#bfdbfe;color:#374151;}
.lf-days--ok svg{color:var(--brand);}
.lf-days--bad{background:#fef2f2;border-color:#fca5a5;color:var(--danger);}
.lf-days__warn{display:block;color:var(--warn);font-size:11px;margin-top:2px;}

/* ── Read-only definition list (Section 1 after submission) ──────────────── */
.lf-ro-group{padding:4px 0 12px;}
.lf-ro-group + .lf-ro-group{border-top:1px solid #eef1f5;padding-top:12px;}
.lf-ro-title{font-size:11px;font-weight:700;color:var(--brand-dk);text-transform:uppercase;letter-spacing:.4px;margin:0 0 8px;}
.lf-dl{display:grid;grid-template-columns:132px minmax(0,1fr) 132px minmax(0,1fr);gap:0;margin:0;font-size:12px;}
.lf-dl dt,.lf-dl dd{margin:0;padding:6px 8px 6px 0;border-bottom:1px solid #f1f4f8;}
.lf-dl dt{color:var(--muted);font-weight:500;}
.lf-dl dd{color:var(--txt);word-wrap:break-word;white-space:pre-line;}
.lf-dl dd.lf-dl__wide{grid-column:span 3;}
.lf-dl dd.lf-dl__empty{color:#94a3b8;}
.lf-meta{font-size:10px;color:var(--muted);margin:8px 0 0;}
.lf-sign{display:none;}

/* ── Action bar ───────────────────────────────────────────────────────────── */
.lf-actions{background:#fff;border-top:1px solid var(--bdr);padding:12px 28px;display:flex;align-items:center;gap:10px;flex-wrap:wrap;position:sticky;bottom:0;z-index:10;box-shadow:0 -2px 8px rgba(0,0,0,.06);}
.lf-btn{display:inline-flex;align-items:center;gap:5px;padding:7px 16px;font-size:12px;font-weight:600;border:1px solid transparent;cursor:pointer;border-radius:0;white-space:nowrap;font-family:inherit;}
.lf-btn--primary{background:var(--brand-dk);color:#fff;}
.lf-btn--primary:hover{background:#041d45;}
.lf-btn--success{background:var(--success);color:#fff;}
.lf-btn--success:hover{background:#15803d;}
.lf-btn--outline{background:#fff;color:#374151;border-color:var(--bdr-in);}
.lf-btn--outline:hover{background:var(--surf);}
.lf-btn--danger{background:#fef2f2;color:var(--danger);border-color:#fca5a5;}
.lf-btn--danger:hover{background:var(--danger);color:#fff;}
.lf-btn--warn{background:#fff7ed;color:var(--warn);border-color:#fcd34d;}
.lf-btn--warn:hover{background:var(--warn);color:#fff;}
.lf-btn:disabled{opacity:.5;cursor:not-allowed;}
.lf-actions__note{font-size:11px;color:var(--muted);margin-left:auto;}

/* ── Modals ───────────────────────────────────────────────────────────────── */
.pa-modal-overlay{display:none;position:fixed;inset:0;background:rgba(0,0,0,.46);z-index:1000;align-items:center;justify-content:center;padding:12px;box-sizing:border-box;}
.pa-modal-overlay.is-open{display:flex;}
.pa-modal{background:#fff;border-radius:2px;width:500px;max-width:100%;max-height:100%;overflow:auto;box-shadow:0 20px 60px rgba(0,0,0,.2);}
.pa-modal--sm{width:420px;}
.pa-modal__head{padding:14px 20px;display:flex;align-items:center;justify-content:space-between;background:var(--brand-dk);color:#fff;}
.pa-modal__head--danger{background:var(--danger);}
.pa-modal__head--success{background:var(--success);}
.pa-modal__title{font-size:13px;font-weight:600;margin:0;}
.pa-modal__close{background:none;border:none;color:rgba(255,255,255,.75);cursor:pointer;font-size:20px;line-height:1;}
.pa-modal__close:hover{color:#fff;}
.pa-modal__body{padding:18px 20px;}
.pa-modal__foot{padding:12px 20px;border-top:1px solid var(--bdr);display:flex;justify-content:flex-end;gap:8px;flex-wrap:wrap;}
.urm-field{margin-bottom:14px;}
.urm-field:last-child{margin-bottom:0;}
.urm-field > label,.urm-label{display:block;font-size:11px;font-weight:600;color:#374151;margin-bottom:4px;}
.urm-field input[type=text],.urm-field input[type=date],.urm-field input[type=number],.urm-field textarea,.urm-field select{width:100%;height:34px;border:1px solid var(--bdr-in);border-radius:0;font-size:12px;font-family:inherit;padding:0 10px;outline:none;box-sizing:border-box;}
.urm-field input:focus,.urm-field textarea:focus,.urm-field select:focus{border-color:var(--brand);}
.urm-field textarea{height:72px;padding:8px 10px;resize:vertical;}
.urm-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:0 14px;}
.urm-hint{font-size:10px;color:var(--muted);margin-top:3px;}
.urm-summary{font-size:11px;color:#374151;background:var(--surf);border:1px solid var(--bdr);padding:8px 10px;margin-bottom:14px;line-height:1.5;}

/* ── Audit trail ─────────────────────────────────────────────────────────── */
.lf-audit{max-width:880px;margin:0 auto;padding:0 24px 24px;box-sizing:border-box;}
.lf-audit__title{font-size:11px;font-weight:700;color:var(--muted);text-transform:uppercase;letter-spacing:.4px;padding:12px 0 8px;}
.audit-entry{display:flex;gap:12px;padding:8px 0;border-bottom:1px solid #f1f5f9;}
.audit-entry:last-child{border-bottom:none;}
.audit-dot{width:10px;height:10px;border-radius:50%;background:var(--brand);flex-shrink:0;margin-top:4px;}
.audit-dot--ok{background:var(--success);}
.audit-dot--err{background:var(--danger);}
.audit-dot--warn{background:var(--warn);}
.audit-body{flex:1;min-width:0;}
.audit-action{font-size:11px;font-weight:600;color:var(--txt);}
.audit-meta{font-size:10px;color:var(--muted);}
.audit-remark{font-size:11px;color:#374151;margin-top:3px;font-style:italic;}

/* ── Toast ───────────────────────────────────────────────────────────────── */
.pa-toast{display:none;position:fixed;bottom:80px;right:24px;z-index:3000;background:#fff;border:1px solid var(--bdr);border-left:4px solid var(--success);padding:12px 18px;font-size:12px;box-shadow:0 4px 18px rgba(0,0,0,.12);max-width:320px;border-radius:2px;}
.pa-toast.visible{display:block;}
.pa-toast--err{border-left-color:var(--danger);}

/* ── Small screens ───────────────────────────────────────────────────────── */
@media (max-width:700px){
    .lf-header,.lf-timeline,.lf-actions{padding-left:16px;padding-right:16px;}
    .lf-doc,.lf-audit{padding-left:16px;padding-right:16px;}
    .lf-grid,.lf-grid--3,.lf-leave-grid,.urm-grid{grid-template-columns:minmax(0,1fr);}
    .lf-dl{grid-template-columns:110px minmax(0,1fr);}
    .lf-dl dd.lf-dl__wide{grid-column:auto;}
    .lf-group__head{flex-wrap:wrap;}
    .lf-group__note{margin-left:0;text-align:left;width:100%;}
    .lf-section__body{padding:14px;}
    .lf-actions__note{margin-left:0;width:100%;}
    .lf-radios--inline{display:block;}
    .lf-radios--inline .lf-radio{border-right:none;border-bottom:1px solid #eef1f5;}
}

/* ── Print: clean official form ──────────────────────────────────────────── */
@media print {
    @page{size:A4;margin:14mm 14mm 16mm;}
    .lf-header,.lf-timeline,.lf-actions,.lf-audit,.pa-modal-overlay,.pa-toast,
    .lf-btn,.lf-section--locked,.lf-intro,nav,aside,.sidebar { display:none!important; }
    body,.cd-main,.cd-content{background:#fff!important;}
    .lf-doc{max-width:100%;margin:0;padding:0;}
    .lf-letterhead{border:none;border-bottom:2px solid #000;border-radius:0;padding:0 0 8px;text-align:center;}
    .lf-letterhead__row{flex-direction:column;gap:4px;}
    .lf-letterhead__crest{width:58px;height:58px;}
    .lf-letterhead__org{color:#000;font-size:15pt;}
    .lf-letterhead__title{font-size:11pt;text-transform:uppercase;letter-spacing:.5px;}
    .lf-letterhead__ref{color:#333;}
    .lf-policy{background:#fff!important;border:1px solid #999;border-left:1px solid #999;font-size:8.5pt;padding:5px 8px;margin-top:8px;}
    .lf-policy svg{display:none;}
    .lf-section{break-inside:avoid;page-break-inside:avoid;border:1px solid #888!important;border-radius:0;box-shadow:none!important;margin-top:10px;opacity:1;}
    .lf-section__head{background:#eee!important;padding:6px 10px;-webkit-print-color-adjust:exact;print-color-adjust:exact;}
    .lf-section__num{background:#fff!important;color:#000!important;border:1px solid #000;width:18px;height:18px;font-size:9pt;}
    .lf-section__title{font-size:10.5pt;}
    .lf-section__badge{background:none!important;color:#000!important;border:1px solid #000;}
    .lf-section__body{padding:8px 10px;}
    .lf-ro-title{color:#000;}
    .lf-dl{font-size:9.5pt;}
    .lf-dl dt,.lf-dl dd{padding:3px 6px 3px 0;border-bottom:1px solid #ddd;}
    .lf-dl dt{color:#333;}
    .lf-read-val{border-bottom:1px solid #bbb;padding:3px 0;min-height:0;}
    .lf-field > label{color:#333;}
    .lf-sign{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px;margin-top:22px;font-size:9pt;}
    .lf-sign div{border-top:1px solid #000;padding-top:3px;}
    body{font-size:10pt;}
}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- Page Header -->
<div class="lf-header">
    <div class="lf-header__left">
        <a href="LeaveApplications.aspx" class="lf-back">
            <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"/></svg>
            Leave Applications
        </a>
        <div style="min-width:0;">
            <div class="lf-header__title">
                <asp:Literal ID="litPageTitle" runat="server">Leave Application</asp:Literal>
                &nbsp;<asp:Literal ID="litStatusBadge" runat="server"></asp:Literal>
            </div>
            <div class="lf-header__sub"><asp:Literal ID="litHeaderSub" runat="server"></asp:Literal></div>
        </div>
    </div>
    <div class="lf-header__actions">
        <asp:Literal ID="litPrintBtn" runat="server"></asp:Literal>
    </div>
</div>

<!-- Workflow Timeline -->
<asp:Literal ID="litTimeline" runat="server"></asp:Literal>

<div class="lf-doc">

    <!-- MRU Letterhead -->
    <div class="lf-letterhead">
        <div class="lf-letterhead__row">
            <img class="lf-letterhead__crest" src="<%= ResolveUrl("~/COOPERP/images/mru-crest.png") %>" alt="MRU crest" />
            <div>
                <div class="lf-letterhead__org">Muteesa I Royal University</div>
                <div class="lf-letterhead__title">Employee Leave Application Form</div>
                <div class="lf-letterhead__ref"><asp:Literal ID="litLetterRef" runat="server"></asp:Literal></div>
            </div>
        </div>
        <div class="lf-policy">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>
            <ul>
                <li><strong>Annual leave:</strong> one month a year, on full pay, taken in two or four phases. Open to permanent staff.</li>
                <li><strong>Approval:</strong> your Supervisor / HOD, then Human Resources, then the Vice Chancellor.</li>
                <li><strong>On return:</strong> report to the Human Resources office before resuming duties.</li>
            </ul>
        </div>
    </div>

    <!-- SECTION 1 — EMPLOYEE REQUEST -->
    <asp:Literal ID="litSection1" runat="server"></asp:Literal>

    <!-- SECTION 2 — HEAD OF DEPARTMENT -->
    <asp:Literal ID="litSection2" runat="server"></asp:Literal>

    <!-- SECTION 3 — HUMAN RESOURCES -->
    <asp:Literal ID="litSection3" runat="server"></asp:Literal>

    <!-- SECTION 4 — VICE CHANCELLOR -->
    <asp:Literal ID="litSection4" runat="server"></asp:Literal>

</div>

<!-- Audit Trail -->
<div class="lf-audit">
    <asp:Literal ID="litAuditTrail" runat="server"></asp:Literal>
</div>

<!-- Sticky action bar -->
<div class="lf-actions" id="actionBar">
    <asp:Literal ID="litActionBar" runat="server"></asp:Literal>
</div>

<!-- ══════════════════════════  MODALS  ══════════════════════════ -->

<!-- HOD Approve -->
<div class="pa-modal-overlay" id="modalHodApprove">
<div class="pa-modal">
    <div class="pa-modal__head pa-modal__head--success">
        <span class="pa-modal__title">Approve and forward to HR</span>
        <button type="button" class="pa-modal__close" onclick="closeModal('modalHodApprove')" aria-label="Close">&times;</button>
    </div>
    <div class="pa-modal__body">
        <div class="urm-field">
            <label for="hodHandoverTo">Duties handed over to</label>
            <input type="text" id="hodHandoverTo" placeholder="Title and full name, e.g. Mr. John Doe, Senior Lecturer" />
            <div class="urm-hint">Pre-filled from the employee's cover arrangement. Edit if it changed.</div>
        </div>
        <div class="urm-field">
            <label for="hodNotes">Remarks <span class="lf-opt">(optional)</span></label>
            <textarea id="hodNotes" placeholder="Any conditions or instructions"></textarea>
        </div>
    </div>
    <div class="pa-modal__foot">
        <button type="button" class="lf-btn lf-btn--outline" onclick="closeModal('modalHodApprove')">Cancel</button>
        <button type="button" class="lf-btn lf-btn--success" id="btnHodApproveConfirm" onclick="submitHodApprove()">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
            Approve &amp; Forward to HR
        </button>
    </div>
</div>
</div>

<!-- HOD Decline -->
<div class="pa-modal-overlay" id="modalHodDecline">
<div class="pa-modal pa-modal--sm">
    <div class="pa-modal__head pa-modal__head--danger">
        <span class="pa-modal__title">Decline application</span>
        <button type="button" class="pa-modal__close" onclick="closeModal('modalHodDecline')" aria-label="Close">&times;</button>
    </div>
    <div class="pa-modal__body">
        <div class="urm-field">
            <label for="hodDeclineReason">Reason for declining <span class="lf-req">*</span></label>
            <textarea id="hodDeclineReason" placeholder="The employee will see this reason"></textarea>
        </div>
    </div>
    <div class="pa-modal__foot">
        <button type="button" class="lf-btn lf-btn--outline" onclick="closeModal('modalHodDecline')">Cancel</button>
        <button type="button" class="lf-btn lf-btn--danger" id="btnHodDeclineConfirm" onclick="submitHodDecline()">Decline Application</button>
    </div>
</div>
</div>

<!-- HR Approve -->
<div class="pa-modal-overlay" id="modalHrApprove">
<div class="pa-modal">
    <div class="pa-modal__head pa-modal__head--success">
        <span class="pa-modal__title">HR: approve and forward to the Vice Chancellor</span>
        <button type="button" class="pa-modal__close" onclick="closeModal('modalHrApprove')" aria-label="Close">&times;</button>
    </div>
    <div class="pa-modal__body">
        <div class="urm-grid">
            <div class="urm-field">
                <label for="hrEffFrom">Effective from <span class="lf-req">*</span></label>
                <input type="date" id="hrEffFrom" />
            </div>
            <div class="urm-field">
                <label for="hrEffTo">Effective to <span class="lf-req">*</span></label>
                <input type="date" id="hrEffTo" />
            </div>
        </div>
        <div class="urm-hint" style="margin:-6px 0 12px;">Pre-filled with the dates requested.</div>
        <div class="urm-field">
            <label for="hrNotes">HR notes <span class="lf-opt">(optional)</span></label>
            <textarea id="hrNotes" placeholder="Leave balance, benefits, conditions"></textarea>
        </div>
    </div>
    <div class="pa-modal__foot">
        <button type="button" class="lf-btn lf-btn--outline" onclick="closeModal('modalHrApprove')">Cancel</button>
        <button type="button" class="lf-btn lf-btn--success" id="btnHrApproveConfirm" onclick="submitHrApprove()">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
            Approve &amp; Forward to VC
        </button>
    </div>
</div>
</div>

<!-- HR Decline -->
<div class="pa-modal-overlay" id="modalHrDecline">
<div class="pa-modal pa-modal--sm">
    <div class="pa-modal__head pa-modal__head--danger">
        <span class="pa-modal__title">HR: decline application</span>
        <button type="button" class="pa-modal__close" onclick="closeModal('modalHrDecline')" aria-label="Close">&times;</button>
    </div>
    <div class="pa-modal__body">
        <div class="urm-field">
            <label for="hrDeclineReason">Reason for declining <span class="lf-req">*</span></label>
            <textarea id="hrDeclineReason" placeholder="The employee will see this reason"></textarea>
        </div>
    </div>
    <div class="pa-modal__foot">
        <button type="button" class="lf-btn lf-btn--outline" onclick="closeModal('modalHrDecline')">Cancel</button>
        <button type="button" class="lf-btn lf-btn--danger" id="btnHrDeclineConfirm" onclick="submitHrDecline()">Decline</button>
    </div>
</div>
</div>

<!-- VC Decision (one modal, three choices) -->
<div class="pa-modal-overlay" id="modalVc">
<div class="pa-modal">
    <div class="pa-modal__head">
        <span class="pa-modal__title">Vice Chancellor's decision</span>
        <button type="button" class="pa-modal__close" onclick="closeModal('modalVc')" aria-label="Close">&times;</button>
    </div>
    <div class="pa-modal__body">
        <div class="urm-summary" id="vcSummary"></div>
        <div class="urm-field">
            <span class="urm-label" id="vcDecisionLbl">Decision <span class="lf-req">*</span></span>
            <div class="lf-radios lf-radios--inline" role="radiogroup" aria-labelledby="vcDecisionLbl">
                <label class="lf-radio"><input type="radio" name="vcDecision" value="GRANTED" /><span class="lf-radio__text"><span class="lf-radio__title">Grant</span><span class="lf-radio__desc">Approve the leave</span></span></label>
                <label class="lf-radio"><input type="radio" name="vcDecision" value="NOT_GRANTED" /><span class="lf-radio__text"><span class="lf-radio__title">Not grant</span><span class="lf-radio__desc">Refuse the leave</span></span></label>
                <label class="lf-radio"><input type="radio" name="vcDecision" value="POSTPONED" /><span class="lf-radio__text"><span class="lf-radio__title">Postpone</span><span class="lf-radio__desc">Defer to a later date</span></span></label>
            </div>
        </div>
        <div class="urm-grid" id="vcGrantFields" style="display:none;">
            <div class="urm-field">
                <label for="vcAccumDays">Accumulated leave days</label>
                <input type="number" id="vcAccumDays" min="0" placeholder="e.g. 30" />
            </div>
            <div class="urm-field">
                <label for="vcDaysTaken">Days granted</label>
                <input type="number" id="vcDaysTaken" min="0" placeholder="e.g. 15" />
            </div>
        </div>
        <div class="urm-field">
            <label for="vcReason" id="vcReasonLbl">Remarks <span class="lf-opt">(optional)</span></label>
            <textarea id="vcReason" placeholder="Any conditions attached to the decision"></textarea>
        </div>
    </div>
    <div class="pa-modal__foot">
        <button type="button" class="lf-btn lf-btn--outline" onclick="closeModal('modalVc')">Cancel</button>
        <button type="button" class="lf-btn lf-btn--primary" id="btnVcConfirm" onclick="submitVcDecision()">Record Decision</button>
    </div>
</div>
</div>

<!-- Toast -->
<div class="pa-toast" id="paToast" role="status" aria-live="polite"></div>

<input type="hidden" id="hdnAppId" value="<asp:Literal ID="litAppId" runat="server"></asp:Literal>" />

<script type="text/javascript">
var APP_ID = parseInt(document.getElementById('hdnAppId').value, 10) || 0;
var LF_APP = window.LF_APP || {};

// ── Modal helpers ─────────────────────────────────────────────────────────────
function openModal(id)  { document.getElementById(id).classList.add('is-open'); }
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }

// ── AJAX helper ───────────────────────────────────────────────────────────────
function doAction(action, data, btn, btnLabel, onSuccess){
    var oldHtml = btn ? btn.innerHTML : '';
    if(btn){ btn.disabled = true; btn.textContent = 'Please wait...'; }
    var body = 'id=' + encodeURIComponent(APP_ID);
    for(var k in data){ if(data.hasOwnProperty(k)) body += '&' + encodeURIComponent(k) + '=' + encodeURIComponent(data[k]); }

    fetch('LeaveApplicationForm.aspx?ajax=' + action, {
        method:'POST',
        credentials:'same-origin',
        headers:{'Content-Type':'application/x-www-form-urlencoded'},
        body: body
    })
    .then(function(r){ return r.json(); })
    .then(function(d){
        if(btn){ btn.disabled = false; btn.innerHTML = oldHtml; }
        if(d.ok) onSuccess(d);
        else showToast(d.error || 'Action failed.', 'err');
    })
    .catch(function(){
        if(btn){ btn.disabled = false; btn.innerHTML = oldHtml; }
        showToast('Network error. Please try again.', 'err');
    });
}

function reloadSuccess(msg, newId){
    showToast(msg, 'ok');
    setTimeout(function(){
        if(newId && !APP_ID) location.href = 'LeaveApplicationForm.aspx?id=' + newId;
        else location.reload();
    }, 1100);
}

// ── Dates ─────────────────────────────────────────────────────────────────────
var LF_DAYS = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
var LF_MONTHS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
function parseYmd(s){
    var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s || '');
    if(!m) return null;
    var d = new Date(Date.UTC(+m[1], +m[2]-1, +m[3]));
    return (isNaN(d.getTime()) || d.getUTCDate() !== +m[3]) ? null : d;
}
function fmtDay(d){
    return LF_DAYS[d.getUTCDay()] + ' ' + d.getUTCDate() + ' ' + LF_MONTHS[d.getUTCMonth()] + ' ' + d.getUTCFullYear();
}
function daysBetween(a, b){ return Math.round((b - a) / 864e5) + 1; }
function todayUtc(){ var n = new Date(); return new Date(Date.UTC(n.getFullYear(), n.getMonth(), n.getDate())); }

// ── Section 1: helpers ────────────────────────────────────────────────────────
function el(id){ return document.getElementById(id); }
function val(id){ var e = el(id); return e ? (e.value || '').trim() : ''; }
function checkedVal(name){
    var r = document.querySelector('input[name="' + name + '"]:checked');
    return r ? r.value : '';
}
function syncRadios(name){
    var all = document.querySelectorAll('input[name="' + name + '"]');
    for(var i = 0; i < all.length; i++){
        var lbl = all[i].closest ? all[i].closest('.lf-radio') : all[i].parentNode;
        if(lbl) lbl.classList.toggle('is-checked', all[i].checked);
    }
}
function setFieldError(fieldId, msg){
    var f = el('fld_' + fieldId);
    if(!f) return;
    var e = f.querySelector('.lf-err');
    if(msg){ f.classList.add('is-invalid'); if(e) e.textContent = msg; }
    else   { f.classList.remove('is-invalid'); if(e) e.textContent = ''; }
}
function clearErrors(){
    var all = document.querySelectorAll('.lf-form .is-invalid');
    for(var i = 0; i < all.length; i++) all[i].classList.remove('is-invalid');
}

function updateLeaveSummary(){
    var box = el('leaveSummary'); if(!box) return;
    var f = parseYmd(val('leaveFrom')), t = parseYmd(val('leaveTo'));
    if(el('leaveTo') && val('leaveFrom')) el('leaveTo').min = val('leaveFrom');
    var icon = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="0"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>';
    if(!f || !t){
        box.className = 'lf-days';
        box.innerHTML = icon + '<span>Choose your first and last day of leave. Days are counted as calendar days, including weekends.</span>';
        return;
    }
    if(t < f){
        box.className = 'lf-days lf-days--bad';
        box.innerHTML = icon + '<span>The last day is before the first day. Please check the dates.</span>';
        return;
    }
    var n = daysBetween(f, t);
    var warn = (f < todayUtc()) ? '<span class="lf-days__warn">Note: the first day is in the past.</span>' : '';
    box.className = 'lf-days lf-days--ok';
    box.innerHTML = icon + '<span><strong>' + fmtDay(f) + '</strong> to <strong>' + fmtDay(t) + '</strong> &middot; <span class="lf-days__n">' +
        n + (n === 1 ? ' day' : ' days') + '</span>' + warn + '</span>';
}

// ── Section 1: collect + validate ─────────────────────────────────────────────
// submit=false (draft): only what the server needs to store a draft.
// submit=true: every field marked * on the form.
function collectSection1(submit){
    clearErrors();
    var errs = [];
    function need(id, msg){ if(!val(id)){ errs.push(id); setFieldError(id, msg); } }

    var lType = checkedVal('leaveTypeRadio');
    if(!lType){ errs.push('leaveType'); setFieldError('leaveType', 'Choose the type of leave.'); }

    var lfrom = val('leaveFrom'), lto = val('leaveTo');
    var f = parseYmd(lfrom), t = parseYmd(lto);
    if(!f){ errs.push('leaveFrom'); setFieldError('leaveFrom', 'Enter the first day of leave.'); }
    if(!t){ errs.push('leaveTo');   setFieldError('leaveTo',   'Enter the last day of leave.'); }
    if(f && t && t < f){ errs.push('leaveTo'); setFieldError('leaveTo', 'The last day must be on or after the first day.'); }

    if(submit) need('substituteArrangement', 'Say who will cover your duties while you are away.');
    if(submit) need('supUsername', 'Choose the Supervisor / HOD who will approve this request.');
    need('empName', 'Enter your full name.');
    if(submit) need('empDept', 'Enter your faculty, department or section.');
    if(submit) need('mobileContact', 'Enter a mobile number we can reach you on.');

    var phones = [['mobileContact', 'Enter a valid phone number (at least 9 digits).'], ['nokMobile', 'Enter a valid phone number (at least 9 digits).']];
    for(var i = 0; i < phones.length; i++){
        var p = val(phones[i][0]);
        if(p && (p.replace(/\D/g, '').length < 9 || /[^0-9+\-\s()\/]/.test(p)) && errs.indexOf(phones[i][0]) < 0){
            errs.push(phones[i][0]); setFieldError(phones[i][0], phones[i][1]);
        }
    }

    if(errs.length){
        var first = el('fld_' + errs[0]);
        if(first){
            first.scrollIntoView({ behavior:'smooth', block:'center' });
            var inp = first.querySelector('input:not([type=hidden]),select,textarea');
            if(inp) setTimeout(function(){ try{ inp.focus({ preventScroll:true }); }catch(e){ inp.focus(); } }, 350);
        }
        showToast(errs.length === 1 ? 'Please fix the highlighted field.' : 'Please fix the ' + errs.length + ' highlighted fields.', 'err');
        return null;
    }

    var days = daysBetween(f, t);
    return {
        emp_name: val('empName'), emp_code: val('empCode'), faculty_dept: val('empDept'),
        office_location: val('officeLocation'), position_held: val('positionHeld'),
        residence_address: val('residenceAddress'), mobile_contact: val('mobileContact'),
        nok_name: val('nokName'), nok_address: val('nokAddress'), nok_mobile: val('nokMobile'),
        leave_type: lType, leave_from: lfrom, leave_to: lto, num_days: days,
        substitute_arrangement: val('substituteArrangement')
    };
}

function saveDraft(submit){
    var data = collectSection1(submit);
    if(!data) return;
    data.submit = submit ? '1' : '0';
    var sup = el('supUsername');
    data.supervisor_username = sup ? sup.value : '';
    data.supervisor_name = (sup && sup.value && sup.options[sup.selectedIndex]) ? (sup.options[sup.selectedIndex].getAttribute('data-name') || sup.options[sup.selectedIndex].text) : '';
    if(submit){
        var msg = 'Submit this leave request to ' + (data.supervisor_name || 'your Supervisor / HOD') + ' for approval?\n\n' +
                  'You will not be able to edit it after submitting.';
        if(!confirm(msg)) return;
    }
    var btn = submit ? el('btnSubmitApp') : el('btnSaveDraft');
    doAction(submit ? 'submit' : 'save_draft', data, btn, '', function(d){
        reloadSuccess(submit ? 'Application submitted to your Supervisor / HOD.' : 'Draft saved.', d.id);
    });
}

function initSection1(){
    if(!el('sec1Form')) return;
    var radios = document.querySelectorAll('input[name="leaveTypeRadio"]');
    for(var i = 0; i < radios.length; i++){
        radios[i].addEventListener('change', function(){ syncRadios('leaveTypeRadio'); setFieldError('leaveType', ''); });
    }
    syncRadios('leaveTypeRadio');
    ['leaveFrom','leaveTo'].forEach(function(id){
        var e = el(id); if(!e) return;
        e.addEventListener('change', function(){ setFieldError('leaveFrom',''); setFieldError('leaveTo',''); updateLeaveSummary(); });
        e.addEventListener('input', updateLeaveSummary);
    });
    var inputs = document.querySelectorAll('#sec1Form input, #sec1Form select, #sec1Form textarea');
    for(var j = 0; j < inputs.length; j++){
        if(!inputs[j].id) continue;
        inputs[j].addEventListener('input',  (function(id){ return function(){ setFieldError(id, ''); }; })(inputs[j].id));
        inputs[j].addEventListener('change', (function(id){ return function(){ setFieldError(id, ''); }; })(inputs[j].id));
    }
    updateLeaveSummary();
}

// ── HOD ───────────────────────────────────────────────────────────────────────
function openHodApprove(){
    var h = el('hodHandoverTo');
    if(h && !h.value && LF_APP.cover) h.value = LF_APP.cover;
    openModal('modalHodApprove');
}
function submitHodApprove(){
    doAction('hod_approve', {
        hod_handover_to: val('hodHandoverTo'),
        hod_notes:       val('hodNotes')
    }, el('btnHodApproveConfirm'), '', function(){
        closeModal('modalHodApprove');
        reloadSuccess('Application approved and forwarded to HR.');
    });
}
function submitHodDecline(){
    var reason = val('hodDeclineReason');
    if(!reason){ showToast('A reason is required.', 'err'); el('hodDeclineReason').focus(); return; }
    doAction('hod_decline', { reason: reason }, el('btnHodDeclineConfirm'), '', function(){
        closeModal('modalHodDecline');
        reloadSuccess('Application declined.');
    });
}

// ── HR ────────────────────────────────────────────────────────────────────────
function openHrApprove(){
    if(!val('hrEffFrom') && LF_APP.from) el('hrEffFrom').value = LF_APP.from;
    if(!val('hrEffTo')   && LF_APP.to)   el('hrEffTo').value   = LF_APP.to;
    openModal('modalHrApprove');
}
function submitHrApprove(){
    var ef = val('hrEffFrom'), et = val('hrEffTo');
    if(!ef || !et){ showToast('Please set both effective dates.', 'err'); return; }
    if(et < ef){ showToast('"Effective to" must be on or after "Effective from".', 'err'); return; }
    doAction('hr_approve', {
        hr_effective_from: ef, hr_effective_to: et,
        hr_notes: val('hrNotes')
    }, el('btnHrApproveConfirm'), '', function(){
        closeModal('modalHrApprove');
        reloadSuccess('Application approved and forwarded to the Vice Chancellor.');
    });
}
function submitHrDecline(){
    var reason = val('hrDeclineReason');
    if(!reason){ showToast('A reason is required.', 'err'); el('hrDeclineReason').focus(); return; }
    doAction('hr_decline', { reason: reason }, el('btnHrDeclineConfirm'), '', function(){
        closeModal('modalHrDecline');
        reloadSuccess('Application declined by HR.');
    });
}

// ── VC (single modal, radio decision) ─────────────────────────────────────────
function onVcDecisionChange(){
    var d = checkedVal('vcDecision');
    syncRadios('vcDecision');
    el('vcGrantFields').style.display = (d === 'GRANTED') ? '' : 'none';
    var needReason = (d === 'NOT_GRANTED' || d === 'POSTPONED');
    el('vcReasonLbl').innerHTML = needReason ? 'Reason <span class="lf-req">*</span>' : 'Remarks <span class="lf-opt">(optional)</span>';
    el('vcReason').placeholder = needReason ? 'The employee will see this reason' : 'Any conditions attached to the decision';
    var btn = el('btnVcConfirm');
    btn.className = 'lf-btn ' + (d === 'GRANTED' ? 'lf-btn--success' : d === 'NOT_GRANTED' ? 'lf-btn--danger' : d === 'POSTPONED' ? 'lf-btn--warn' : 'lf-btn--primary');
    btn.textContent = d === 'GRANTED' ? 'Grant Leave' : d === 'NOT_GRANTED' ? 'Do Not Grant' : d === 'POSTPONED' ? 'Postpone Leave' : 'Record Decision';
}
function openVcDecision(preset){
    var radios = document.querySelectorAll('input[name="vcDecision"]');
    for(var i = 0; i < radios.length; i++) radios[i].checked = (radios[i].value === preset);
    if(!val('vcDaysTaken') && LF_APP.days) el('vcDaysTaken').value = LF_APP.days;
    var s = el('vcSummary');
    if(s) s.innerHTML = LF_APP.summary || '';
    if(s) s.style.display = LF_APP.summary ? '' : 'none';
    onVcDecisionChange();
    openModal('modalVc');
}
function submitVcDecision(){
    var d = checkedVal('vcDecision');
    if(!d){ showToast('Choose Grant, Not grant or Postpone.', 'err'); return; }
    var reason = val('vcReason');
    if(d !== 'GRANTED' && !reason){ showToast('A reason is required for this decision.', 'err'); el('vcReason').focus(); return; }
    var btn = el('btnVcConfirm');
    doAction('vc_decision', {
        vc_decision:         d,
        vc_reason:           reason,
        vc_accumulated_days: d === 'GRANTED' ? val('vcAccumDays') : '',
        vc_days_taken:       d === 'GRANTED' ? val('vcDaysTaken') : ''
    }, btn, '', function(){
        closeModal('modalVc');
        reloadSuccess(d === 'GRANTED' ? 'Leave granted.' : d === 'NOT_GRANTED' ? 'Leave not granted.' : 'Leave postponed.');
    });
}
(function(){
    var radios = document.querySelectorAll('input[name="vcDecision"]');
    for(var i = 0; i < radios.length; i++) radios[i].addEventListener('change', onVcDecisionChange);
})();

// ── Print ─────────────────────────────────────────────────────────────────────
function printForm(){ window.print(); }

// ── Toast ─────────────────────────────────────────────────────────────────────
function showToast(msg, type){
    var t = el('paToast');
    t.textContent = msg;
    t.className = 'pa-toast visible' + (type === 'err' ? ' pa-toast--err' : '');
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function(){ t.classList.remove('visible'); }, 3500);
}

// Close modals with Escape
document.addEventListener('keydown', function(e){
    if(e.key === 'Escape'){
        var open = document.querySelectorAll('.pa-modal-overlay.is-open');
        for(var i = 0; i < open.length; i++) open[i].classList.remove('is-open');
    }
});

initSection1();
</script>
</asp:Content>
