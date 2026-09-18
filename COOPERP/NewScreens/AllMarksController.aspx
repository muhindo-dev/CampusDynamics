<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AllMarksController.aspx.cs" Inherits="COOPERP_NewScreens_AllMarksController" Title="All Marks Controller - Admin" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
*{box-sizing:border-box;}
.pm-admin-wrap{max-width:1320px;margin:0 auto;padding:8px 10px 12px;}
.pm-stats{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px;margin-bottom:8px;}
.pm-stat{padding:8px 10px;border-radius:6px;border:1px solid #e3e9f2;background:#fff;min-height:60px;display:flex;flex-direction:column;justify-content:center;gap:3px;cursor:pointer;}
.pm-stat__lbl{font-size:9px;text-transform:uppercase;letter-spacing:.45px;color:#64748b;font-weight:800;line-height:1.25;}
.pm-stat__val{font-size:22px;line-height:1;font-weight:800;color:#05275C;letter-spacing:-.02em;}
.pm-stat--pending .pm-stat__val{color:#b45309;}
.pm-stat--rejected .pm-stat__val{color:#b42318;}
.pm-stat--approved .pm-stat__val{color:#2e7d32;}
.pm-stat--published .pm-stat__val{color:#174DA4;}
.pm-stat--notentered .pm-stat__val{color:#6b7280;}
.pm-card{background:#fff;border:1px solid #e3e9f2;border-radius:8px;overflow:visible;}
.pm-card__head{padding:8px 10px;border-bottom:1px solid #edf1f6;background:#fff;display:flex;justify-content:space-between;align-items:center;gap:8px;flex-wrap:wrap;}
.pm-card__title{font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.5px;color:#05275C;}
/* 9 cells: year, sem, status, prog, lecturer, STUDENT, COURSE, per-page, apply. */
.pm-filters{padding:8px 10px;border-bottom:1px solid #eef2f6;background:#fff;display:grid;grid-template-columns:minmax(104px,.7fr) minmax(84px,.55fr) minmax(94px,.62fr) minmax(112px,.78fr) minmax(112px,.78fr) minmax(150px,1.05fr) minmax(130px,.9fr) minmax(74px,.45fr) auto;gap:6px;align-items:flex-end;}
@media (max-width:1200px){.pm-filters{grid-template-columns:repeat(3,minmax(0,1fr));}}
@media (max-width:640px){.pm-filters{grid-template-columns:repeat(2,minmax(0,1fr));}}
.pm-fg{display:flex;flex-direction:column;gap:2px;min-width:0;}
.pm-fg label{font-size:9px;text-transform:uppercase;letter-spacing:.45px;color:#64748b;font-weight:800;}
.pm-input,.pm-select{height:30px;border:1px solid #cdd8e6;padding:4px 8px;font-size:11px;background:#fff;border-radius:6px;color:#1a1a2e;font-family:inherit;}
.pm-input:focus,.pm-select:focus{outline:none;border-color:#174DA4;box-shadow:0 0 0 3px rgba(23,77,164,.12);background:#fcfdff;}
.pm-bulk-bar{display:none;padding:6px 10px;border-bottom:1px solid #fdba74;background:#fff7ed;align-items:center;gap:10px;flex-wrap:wrap;}
.pm-bulk-bar.show{display:flex;}
.pm-bulk-label{font-size:11px;font-weight:700;color:#92400e;}
.pm-top-controls{padding:8px 10px;border-bottom:1px solid #eef2f6;background:#f8fafc;display:flex;gap:8px;align-items:center;flex-wrap:wrap;}
.pm-wiz-steps{display:flex;gap:6px;margin-bottom:12px;}
.pm-wiz-step{padding:4px 8px;border:1px solid #d6deea;border-radius:12px;font-size:10px;font-weight:800;color:#64748b;background:#fff;}
.pm-wiz-step.active{background:#05275C;color:#fff;border-color:#05275C;}
.pm-wiz-panel{display:none;}
.pm-wiz-panel.active{display:block;}
.pm-wiz-preview{max-height:280px;overflow:auto;border:1px solid #e5eaf1;border-radius:6px;}
.pm-wiz-table{width:100%;border-collapse:collapse;font-size:10px;}
.pm-wiz-table th,.pm-wiz-table td{padding:6px;border-bottom:1px solid #eef2f6;text-align:left;}
.pm-wiz-table th{background:#f8fafc;color:#64748b;font-size:9px;text-transform:uppercase;letter-spacing:.4px;}
.pm-btn{display:inline-flex;align-items:center;justify-content:center;gap:4px;padding:5px 9px;border:1px solid #d2dae6;background:#fff;color:#05275C;font-size:10px;font-weight:800;cursor:pointer;border-radius:6px;min-height:30px;text-decoration:none;}
.pm-btn:hover{color:#174DA4;border-color:#174DA4;background:#f4f8ff;}
.pm-btn--primary{background:#05275C;color:#fff;border-color:#05275C;}
.pm-btn--primary:hover{background:#174DA4;border-color:#174DA4;color:#fff;}
.pm-btn--success{background:#2e7d32;color:#fff;border-color:#2e7d32;}
.pm-btn--danger{background:#c62828;color:#fff;border-color:#c62828;}
.pm-btn--ghost{padding:0;border:none;background:transparent;min-height:0;color:#174DA4;font-size:10px;font-weight:700;}
.pm-btn--ghost:hover{background:transparent;border:none;color:#0f3f8c;text-decoration:underline;}
.pm-meta{padding:6px 10px;border-bottom:1px solid #eef2f6;font-size:10px;color:#64748b;display:flex;justify-content:space-between;gap:8px;flex-wrap:wrap;background:#fff;align-items:center;}
.pm-pager{display:flex;gap:3px;flex-wrap:wrap;}
.pm-pager a,.pm-pager span{border:1px solid #d4dbe8;background:#fff;color:#334155;font-size:9px;text-decoration:none;padding:4px 7px;border-radius:6px;}
.pm-pager .active{background:#05275C;border-color:#05275C;color:#fff;}
.pm-table-wrap{overflow:auto;scrollbar-color:#b6c5db #f5f8fc;scrollbar-width:thin;background:#fff;position:relative;padding:0;}
.pm-table{width:100%;min-width:920px;border-collapse:collapse;table-layout:fixed;}
.pm-table th{position:sticky;top:0;background:#f8fafc;border-bottom:1px solid #e0e5ed;font-size:8.5px;text-transform:uppercase;letter-spacing:.4px;color:#64748b;font-weight:800;padding:3px 3px;text-align:left;white-space:nowrap;z-index:1;}
.pm-table td{border-bottom:1px solid #eef2f6;font-size:10px;color:#1f2937;padding:2px 3px;vertical-align:middle;background:#fff;overflow:visible;line-height:1.25;}
/* Course title under its code — deliberately tiny, it is context not the key. */
.pm-subname{display:block;font-size:8px;line-height:1.2;color:#94a3b8;font-weight:500;
    white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:100%;margin-top:1px;}
.pm-table tbody tr:hover td{background:#fafcff;}
.pm-table tr.row--pending td:first-child{border-left:3px solid #f59e0b;}
.pm-table tr.row--approved td:first-child{border-left:3px solid #22c55e;}
.pm-table tr.row--rejected td:first-child{border-left:3px solid #ef4444;}
.pm-table tr.row--published td:first-child{border-left:3px solid #3b82f6;}
.pm-table .col-sel{width:26px;text-align:center;}
.pm-table .col-regno{width:84px;}
.pm-table .col-student{width:108px;}
/* Wider now that it carries the course title as well. */
.pm-table .col-course{width:132px;}
.pm-table .col-prog{width:52px;}
.pm-table .col-yr{width:62px;}
.pm-table .col-sem{width:76px;text-align:left;}
.pm-table .col-mark{width:34px;text-align:center;}
.pm-table .col-track{width:132px;}
.pm-auditwarn{display:flex;gap:10px;align-items:flex-start;background:#fef2f2;border:1px solid #fecaca;border-left:3px solid #b42318;padding:10px 12px;margin-bottom:8px;}
.pm-auditwarn b{display:block;font-size:12px;color:#7a271a;margin-bottom:2px;}
.pm-auditwarn p{margin:0;font-size:11px;color:#912018;line-height:1.5;}
.pm-auditwarn code{background:rgba(180,35,24,.08);padding:0 3px;}
.pm-track{line-height:1.25;}
.pm-track--none{color:#c7cdd6;}
.pm-track__tags{display:flex;flex-wrap:wrap;gap:3px;margin-bottom:2px;}
.pm-chg{display:inline-block;padding:0 4px;font-size:8.5px;font-weight:700;border-radius:2px;white-space:nowrap;}
.pm-chg--cw{background:#eef2ff;color:#3730a3;}
.pm-chg--ex{background:#fef3c7;color:#92400e;}
.pm-chg--other{background:#f1f5f9;color:#64748b;}
.pm-track__who{font-size:9.5px;font-weight:600;color:#334155;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.pm-track__when{font-size:8.5px;color:#94a3b8;white-space:nowrap;}
.pm-track__n{display:inline-block;padding:0 3px;font-size:8px;font-weight:700;color:#475569;background:#e2e8f0;border-radius:2px;}
.pm-track__hist{display:inline-block;margin-left:3px;padding:0 3px;font-size:8px;font-weight:700;color:#7c3aed;background:#f3e8ff;border-radius:2px;}
/* history inside the details modal */
.pm-hist{margin-top:10px;border-top:1px solid #e5e7eb;padding-top:8px;}
.pm-hist__h{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:#6b7280;margin-bottom:6px;}
.pm-hist__none{font-size:11px;color:#9ca3af;}
.pm-hist__i{border-left:2px solid #e5e7eb;padding:0 0 9px 9px;position:relative;}
.pm-hist__i:last-child{padding-bottom:0;}
.pm-hist__i::before{content:'';position:absolute;left:-4px;top:3px;width:6px;height:6px;border-radius:50%;background:#94a3b8;}
.pm-hist__i--cw::before{background:#4f46e5;}
.pm-hist__i--ex::before{background:#d97706;}
.pm-hist__top{display:flex;flex-wrap:wrap;gap:5px;align-items:baseline;}
.pm-hist__when{font-size:10px;color:#111827;font-weight:700;}
.pm-hist__by{font-size:10px;color:#374151;}
.pm-hist__meta{font-size:9.5px;color:#9ca3af;margin-top:2px;word-break:break-word;}
.pm-hist__move{font-size:11px;color:#111827;margin-top:2px;}
.pm-hist__move b{font-weight:700;}
.pm-hist__from{color:#b42318;}
.pm-hist__to{color:#067647;}
.pm-table .col-pub{width:42px;text-align:center;}
.pm-table .col-grade{width:42px;text-align:center;}
.pm-table .col-status{width:58px;text-align:center;}
.pm-table .col-act{width:30px;text-align:center;overflow:visible;}

/* ── Narrower screens ────────────────────────────────────────────────────────
   Fourteen columns of marks data cannot be reflowed into a phone, so the table
   keeps every column and scrolls sideways inside its own wrapper — the page
   itself never scrolls horizontally. Columns are NOT hidden at breakpoints:
   under table-layout:fixed the <colgroup> widths map to the columns that are
   actually rendered, so dropping one silently shifts every width after it.
   What steps down is padding, type size and the minimum width. */
@media (max-width:1100px){
    .pm-table th,.pm-table td{padding:2px;font-size:9.5px;}
    .pm-table{min-width:860px;}
}
@media (max-width:820px){
    .pm-table .col-student{width:96px;}
    .pm-table .col-course{width:118px;}
    .pm-table{min-width:800px;}
}
@media (max-width:560px){
    .pm-table th{font-size:8px;letter-spacing:.3px;padding:2px;}
    .pm-table td{font-size:9.5px;padding:2px;}
    .pm-subname{font-size:7.5px;}
    .pm-table .col-regno{width:78px;}
    .pm-table .col-student{width:88px;}
    .pm-table{min-width:760px;}
    .pm-table-wrap{-webkit-overflow-scrolling:touch;}
    .pm-meta{font-size:9.5px;}
}
.pm-head-act{display:flex;align-items:center;justify-content:center;gap:4px;}
.pm-row-sel{width:14px;height:14px;cursor:pointer;accent-color:#174DA4;}
.pm-ellipsis{display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.pm-code{font-family:Consolas,monospace;font-size:10px;color:#174DA4;font-weight:700;white-space:nowrap;}
.pm-muted{color:#6b7280;font-size:10px;}
.pm-center{text-align:center;}
.pm-mark{font-size:11px;font-weight:800;color:#05275C;}
.pm-mark--na{font-size:11px;color:#9ca3af;}
.pm-pill{display:inline-block;padding:2px 7px;font-size:9px;font-weight:800;text-transform:uppercase;letter-spacing:.3px;border-radius:3px;}
.pm-pill--pending{background:#fff3cd;color:#92400e;}.pm-pill--approved{background:#e6f4ea;color:#2e7d32;}.pm-pill--rejected{background:#fde8e8;color:#b42318;}.pm-pill--published{background:#e8f0fc;color:#174DA4;}.pm-pill--not_entered{background:#f3f4f6;color:#6b7280;}
.pm-row-wrap{position:relative;display:inline-flex;align-items:center;justify-content:center;z-index:20;}
.pm-row-trigger{display:inline-flex;align-items:center;justify-content:center;width:22px;height:22px;border:1px solid #d6deea;background:#fff;color:#475569;cursor:pointer;border-radius:5px;font-size:14px;line-height:1;padding:0;}
.pm-row-menu{display:none;position:absolute;right:0;top:calc(100% + 4px);min-width:160px;padding:6px;background:#fff;border:1px solid #dbe4ef;border-radius:8px;box-shadow:0 14px 34px rgba(15,23,42,.16);z-index:200;}
.pm-row-menu.open{display:block;}
.pm-row-menu__item{display:flex;align-items:center;gap:8px;width:100%;padding:8px 10px;border:0;background:transparent;color:#334155;font-size:11px;font-weight:700;text-align:left;border-radius:6px;cursor:pointer;white-space:nowrap;}
.pm-row-menu__item:hover{background:#f8fafc;color:#0f172a;}
.pm-row-menu__sep{height:1px;background:#edf2f7;margin:5px 2px;}
.pm-overlay{display:none;position:fixed;inset:0;background:rgba(5,15,35,.5);backdrop-filter:blur(2px);z-index:9000;}
.pm-overlay.show{display:block;}
.pm-modal{display:none;position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);background:#fff;border:1px solid #dde3ed;border-radius:10px;width:92%;max-width:580px;box-shadow:0 20px 60px rgba(5,15,35,.2);z-index:9001;max-height:90vh;overflow-y:auto;}
.pm-modal--wide{max-width:700px;}
.pm-modal.show{display:block;}
.pm-modal__head{padding:14px 16px;border-bottom:1px solid #e7ebf1;display:flex;justify-content:space-between;align-items:center;gap:8px;background:#f8fafc;}
.pm-modal__title{font-size:12px;font-weight:900;text-transform:uppercase;letter-spacing:.5px;color:#05275C;}
.pm-modal__close{background:0;border:0;font-size:20px;color:#6b7280;cursor:pointer;line-height:1;padding:0;width:26px;height:26px;}
.pm-modal__body{padding:16px;}
.pm-modal__foot{padding:10px 16px;border-top:1px solid #e7ebf1;background:#f8fafc;display:flex;justify-content:flex-end;gap:6px;}
.pm-dl{display:grid;grid-template-columns:1fr 1fr;gap:8px 16px;margin-bottom:14px;}
.pm-dl dt{font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#6b7280;font-weight:800;margin:0;}
.pm-dl dd{font-size:11px;font-weight:700;color:#1f2937;margin:0 0 2px;}
.pm-marks-disp{display:flex;gap:0;border:1px solid #e0e5ed;border-radius:6px;overflow:hidden;margin-bottom:14px;}
.pm-marks-disp__box{flex:1;padding:10px 8px;text-align:center;border-right:1px solid #e0e5ed;background:#f8fafc;}
.pm-marks-disp__box:last-child{border-right:none;}
.pm-marks-disp__val{font-size:22px;font-weight:800;color:#05275C;line-height:1;}
.pm-marks-disp__lbl{font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#6b7280;margin-top:3px;}
.pm-marks-edit{display:grid;grid-template-columns:1fr 1fr 1fr;gap:10px;margin-bottom:14px;background:#f8fafc;border:1px solid #e0e5ed;border-radius:6px;padding:12px;}
.pm-alert{padding:8px 12px;border-radius:4px;font-size:11px;margin-bottom:10px;display:none;}
.pm-alert.show{display:block;}
.pm-alert--ok{background:#f0fdf4;border:1px solid #bbf7d0;color:#15803d;}
.pm-alert--err{background:#fef2f2;border:1px solid #fecaca;color:#b91c1c;}
.pm-comment-area{width:100%;padding:8px 10px;border:1px solid #cfd8e3;font-size:11px;resize:vertical;min-height:64px;border-radius:4px;font-family:inherit;}
.pm-divider{border:none;border-top:1px solid #f1f5f9;margin:12px 0;}
.pm-toast{position:fixed;bottom:22px;right:22px;background:#1f2937;color:#fff;font-size:11px;font-weight:700;padding:10px 18px;border-radius:6px;z-index:9999;display:none;}
.pm-toast.show{display:block;}
.pm-toast--ok{background:#15803d;}
.pm-toast--err{background:#b91c1c;}
.pm-search-results{position:absolute;top:100%;left:0;right:0;background:#fff;border:1px solid #dbe4ef;border-top:none;border-radius:0 0 6px 6px;max-height:220px;overflow:auto;z-index:9002;display:none;box-shadow:0 12px 28px rgba(15,23,42,.14);}
.pm-search-results.show{display:block;}
.pm-search-item{padding:7px 10px;font-size:11px;cursor:pointer;border-bottom:1px solid #f1f5f9;color:#1f2937;line-height:1.35;}
.pm-search-item:last-child{border-bottom:none;}
.pm-search-item:hover{background:#f4f8ff;}
.pm-search-item small{color:#6b7280;}
.pm-picked{background:#f8fafc;border:1px solid #e0e5ed;border-radius:6px;padding:8px 10px;margin-bottom:10px;font-size:11px;color:#1f2937;}
.pm-existing-wrap{max-height:120px;overflow:auto;border:1px solid #eef2f6;border-radius:6px;}
.pm-existing-row{padding:5px 8px;border-bottom:1px solid #f1f5f9;font-size:10px;color:#374151;}
.pm-existing-row:last-child{border-bottom:none;}
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="pm-admin-wrap">
<div id="pmToast" class="pm-toast"></div>
<div id="pmOverlay" class="pm-overlay" onclick="closeAllModals()"></div>

<div id="modalDetails" class="pm-modal pm-modal--wide">
	<div class="pm-modal__head"><span class="pm-modal__title" id="detailsModalTitle">Record Details</span><button class="pm-modal__close" onclick="closeModal('modalDetails')">&times;</button></div>
	<div class="pm-modal__body">
		<div id="detailsGrid" class="pm-dl" style="display:grid;"></div>
		<div class="pm-marks-disp" id="detailsMarksDisp"></div>
		<hr class="pm-divider" /><div id="detailsComment" style="font-size:11px;color:#374151;"></div><div class="pm-alert" id="detailsAlert"></div>
	</div>
	<div class="pm-modal__foot"><button type="button" class="pm-btn pm-btn--ghost" onclick="closeModal('modalDetails')">Close</button></div>
</div>

<div id="modalReview" class="pm-modal">
	<div class="pm-modal__head"><span class="pm-modal__title" id="modalReviewTitle">Review Provisional Marks</span><button class="pm-modal__close" onclick="closeModal('modalReview')">&times;</button></div>
	<div class="pm-modal__body">
		<div id="reviewDetailGrid" class="pm-dl" style="display:grid;"></div><div class="pm-marks-disp" id="reviewMarksDisp"></div>
		<div id="reviewPrevComment" style="display:none;margin-bottom:12px;padding:8px 10px;background:#f8fafc;border:1px solid #e0e5ed;border-radius:4px;font-size:11px;color:#374151;"></div>
		<div class="pm-fg" style="margin-bottom:0;"><label style="font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#64748b;font-weight:800;">Comments <span id="reqNote" style="color:#b42318;">(required for rejection)</span></label><textarea class="pm-comment-area" id="reviewComment" placeholder="Enter review comment…"></textarea></div>
		<div class="pm-alert" id="reviewAlert"></div>
	</div>
	<div class="pm-modal__foot"><button type="button" class="pm-btn pm-btn--ghost" onclick="closeModal('modalReview')">Cancel</button><button type="button" class="pm-btn pm-btn--danger" id="btnReject" onclick="submitReview('rejected')">Reject</button><button type="button" class="pm-btn pm-btn--success" id="btnApprove" onclick="submitReview('approved')">Approve</button></div>
</div>

<div id="modalPublish" class="pm-modal" style="max-width:460px;">
	<div class="pm-modal__head"><span class="pm-modal__title">Publish to Final Results</span><button class="pm-modal__close" onclick="closeModal('modalPublish')">&times;</button></div>
	<div class="pm-modal__body"><p style="font-size:11px;color:#374151;margin:0 0 12px;">This will copy these provisional marks into <strong>acad_results</strong> as the official grade. This action cannot be undone.</p><div class="pm-marks-disp" id="publishMarksDisp"></div><div id="publishInfo" style="font-size:11px;color:#374151;margin-bottom:4px;"></div><div class="pm-alert" id="publishAlert"></div></div>
	<div class="pm-modal__foot"><button type="button" class="pm-btn pm-btn--ghost" onclick="closeModal('modalPublish')">Cancel</button><button type="button" class="pm-btn pm-btn--primary" id="btnPublishConfirm" onclick="submitPublish()">Confirm &amp; Publish</button></div>
</div>

<div id="modalEdit" class="pm-modal">
	<div class="pm-modal__head"><span class="pm-modal__title" id="editModalTitle">Edit Marks (Admin Override)</span><button class="pm-modal__close" onclick="closeModal('modalEdit')">&times;</button></div>
	<div class="pm-modal__body">
		<div id="editDetailGrid" class="pm-dl" style="display:grid;"></div>
		<div class="pm-marks-edit">
			<div class="pm-fg"><label>Course Work (0–100)</label><input type="number" class="pm-input" id="editCW" min="0" max="100" placeholder="CW" /></div>
			<div class="pm-fg"><label>Exam Marks (0–100)</label><input type="number" class="pm-input" id="editExam" min="0" max="100" placeholder="Exam" /></div>
			<div class="pm-fg"><label>Total <span id="editTotalMode" style="font-weight:600;color:#16a34a;">(auto)</span></label><input type="number" class="pm-input" id="editTotal" min="0" max="100" placeholder="Total" /></div>
		</div>
		<div class="pm-fg" style="margin-bottom:0;"><label style="font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#64748b;font-weight:800;">Admin Note (reason for override)</label><textarea class="pm-comment-area" id="editNote" placeholder="Reason for admin mark edit…"></textarea></div>
		<div id="editCWInfo" style="font-size:10px;color:#6b7280;margin-top:6px;">Total is computed from Course Work + Exam as you type. Type your own figure to override it; clear the box to go back to automatic.</div><div class="pm-alert" id="editAlert"></div>
	</div>
	<div class="pm-modal__foot"><button type="button" class="pm-btn pm-btn--ghost" onclick="closeModal('modalEdit')">Cancel</button><button type="button" class="pm-btn pm-btn--primary" id="btnEditSave" onclick="submitEdit()">Save Marks</button></div>
</div>

<div id="modalBulk" class="pm-modal" style="max-width:440px;">
	<div class="pm-modal__head"><span class="pm-modal__title" id="bulkModalTitle">Bulk Action</span><button class="pm-modal__close" onclick="closeModal('modalBulk')">&times;</button></div>
	<div class="pm-modal__body"><p id="bulkDesc" style="font-size:11px;color:#374151;margin:0 0 12px;"></p><div class="pm-fg" id="bulkCommentWrap"><label style="font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#64748b;font-weight:800;">Comment <span id="bulkReqNote" style="color:#b42318;">(required for rejection)</span></label><textarea class="pm-comment-area" id="bulkComment" placeholder="Comment for all selected records…"></textarea></div><div class="pm-alert" id="bulkAlert"></div></div>
	<div class="pm-modal__foot"><button type="button" class="pm-btn pm-btn--ghost" onclick="closeModal('modalBulk')">Cancel</button><button type="button" class="pm-btn pm-btn--primary" id="btnBulkConfirm" onclick="submitBulk()">Confirm</button></div>
</div>

<div id="modalBatchWizard" class="pm-modal pm-modal--wide">
	<div class="pm-modal__head"><span class="pm-modal__title">Batch Approval / Publish Wizard</span><button type="button" class="pm-modal__close" onclick="closeModal('modalBatchWizard')">&times;</button></div>
	<div class="pm-modal__body">
		<div class="pm-wiz-steps"><span class="pm-wiz-step" id="wizStep1">1. Action</span><span class="pm-wiz-step" id="wizStep2">2. Scope</span><span class="pm-wiz-step" id="wizStep3">3. Preview</span></div>
		<div id="wizPanel1" class="pm-wiz-panel">
			<div class="pm-fg"><label>Workflow Action</label><select id="wizAction" class="pm-select"><option value="approved">Approve pending marks</option><option value="published">Publish to final results</option></select></div>
		</div>
		<div id="wizPanel2" class="pm-wiz-panel">
			<div class="pm-fg" style="margin-bottom:8px;"><label>Scope</label><select id="wizScope" class="pm-select"><option value="selected">Selected rows in current table</option><option value="programme">By programme / year / semester</option></select></div>
			<div id="wizScopeFilters" style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:8px;">
				<div class="pm-fg"><label>Programme</label><select id="wizProg" class="pm-select"></select></div>
				<div class="pm-fg"><label>Academic Year</label><select id="wizYear" class="pm-select"></select></div>
				<div class="pm-fg"><label>Semester</label><select id="wizSem" class="pm-select"><option value="">All Sems</option><option value="1">Sem 1</option><option value="2">Sem 2</option><option value="3">Sem 3</option></select></div>
			</div>
		</div>
		<div id="wizPanel3" class="pm-wiz-panel">
			<div id="wizSummary" style="font-size:11px;color:#374151;margin-bottom:8px;"></div>
			<div class="pm-wiz-preview"><table class="pm-wiz-table"><thead><tr><th>Reg No</th><th>Course</th><th>Prog</th><th>Year</th><th>Sem</th><th>Status</th><th>Total</th></tr></thead><tbody id="wizPreviewRows"></tbody></table></div>
			<div class="pm-alert" id="wizAlert"></div>
		</div>
	</div>
	<div class="pm-modal__foot"><button type="button" class="pm-btn pm-btn--ghost" id="wizBack" onclick="wizardBack()">Back</button><button type="button" class="pm-btn pm-btn--primary" id="wizNext" onclick="wizardNext()">Next</button><button type="button" class="pm-btn pm-btn--success" id="wizRun" onclick="wizardRun()" style="display:none;">Run Workflow</button></div>
</div>

<!-- ── Set Status modal (single-record + batch) ── -->
<div id="modalSetStatus" class="pm-modal" style="max-width:460px;">
  <div class="pm-modal__head">
    <span class="pm-modal__title" id="setStatusTitle">Set Status</span>
    <button class="pm-modal__close" onclick="closeModal('modalSetStatus')">&times;</button>
  </div>
  <div class="pm-modal__body">
    <div id="setStatusInfo" style="font-size:11px;color:#374151;background:#f8fafc;border:1px solid #e0e5ed;border-radius:4px;padding:8px 10px;margin-bottom:12px;min-height:18px;"></div>
    <div class="pm-fg" style="margin-bottom:12px;">
      <label style="font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#64748b;font-weight:800;margin-bottom:7px;display:block;">New Status</label>
      <div style="display:flex;gap:6px;flex-wrap:wrap;">
        <label style="display:inline-flex;align-items:center;gap:5px;padding:6px 11px;border:1px solid #d6deea;border-radius:6px;cursor:pointer;font-size:11px;font-weight:700;color:#92400e;background:#fff;" id="ssLblPending">
          <input type="radio" name="ssStatus" value="pending" onchange="onSetStatusChange(this)" /> Pending
        </label>
        <label style="display:inline-flex;align-items:center;gap:5px;padding:6px 11px;border:1px solid #d6deea;border-radius:6px;cursor:pointer;font-size:11px;font-weight:700;color:#2e7d32;background:#fff;" id="ssLblApproved">
          <input type="radio" name="ssStatus" value="approved" onchange="onSetStatusChange(this)" /> Approved
        </label>
        <label style="display:inline-flex;align-items:center;gap:5px;padding:6px 11px;border:1px solid #d6deea;border-radius:6px;cursor:pointer;font-size:11px;font-weight:700;color:#b42318;background:#fff;" id="ssLblRejected">
          <input type="radio" name="ssStatus" value="rejected" onchange="onSetStatusChange(this)" /> Rejected
        </label>
        <label style="display:inline-flex;align-items:center;gap:5px;padding:6px 11px;border:1px solid #d6deea;border-radius:6px;cursor:pointer;font-size:11px;font-weight:700;color:#174DA4;background:#fff;" id="ssLblPublished">
          <input type="radio" name="ssStatus" value="published" onchange="onSetStatusChange(this)" /> Published
        </label>
      </div>
    </div>
    <div class="pm-fg" style="margin-bottom:6px;">
      <label style="font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#64748b;font-weight:800;">Comment <span id="ssCommentReq" style="color:#b42318;display:none;">(required for Rejected)</span></label>
      <textarea class="pm-comment-area" id="ssComment" placeholder="Optional reason or note…" style="min-height:52px;"></textarea>
    </div>
    <div id="ssPublishNote" style="display:none;padding:7px 10px;background:#e8f0fc;border:1px solid #c5d5f5;border-radius:4px;font-size:10px;color:#174DA4;margin-bottom:6px;">
      &#9432; <strong>Published</strong> runs the full pipeline: marks are written to <em>acad_results</em> and semester GPA is recomputed. At least one mark value must be present on the record.
    </div>
    <div class="pm-alert" id="ssAlert"></div>
  </div>
  <div class="pm-modal__foot">
    <button type="button" class="pm-btn pm-btn--ghost" onclick="closeModal('modalSetStatus')">Cancel</button>
    <button type="button" class="pm-btn pm-btn--primary" id="btnSetStatusConfirm" onclick="submitSetStatus()">Set Status</button>
  </div>
</div>

<!-- ── Register Student to Course (admin) ── -->
<div id="modalCreateReg" class="pm-modal pm-modal--wide">
  <div class="pm-modal__head"><span class="pm-modal__title">Register Student to a Course</span><span id="crSessionBadge" style="display:none;margin-left:auto;margin-right:8px;font-size:10px;font-weight:800;color:#15803d;background:#e6f4ea;border:1px solid #bbf7d0;border-radius:10px;padding:2px 9px;">0 added</span><button class="pm-modal__close" onclick="closeCreateReg()">&times;</button></div>
  <div class="pm-modal__body">
    <div class="pm-fg" style="margin-bottom:10px;position:relative;">
      <label>1. Student <span style="color:#94a3b8;font-weight:600;text-transform:none;letter-spacing:0;">(search reg no, name or entry no)</span></label>
      <input type="text" class="pm-input" id="crStudSearch" autocomplete="off" placeholder="Type at least 2 characters&hellip;" oninput="crSearchStudents()" />
      <div id="crStudResults" class="pm-search-results"></div>
    </div>
    <div id="crStudPicked" class="pm-picked" style="display:none;"></div>
    <div id="crExisting" style="display:none;margin-bottom:10px;"></div>
    <div class="pm-fg" style="margin-bottom:10px;position:relative;">
      <label>2. Course <span style="color:#94a3b8;font-weight:600;text-transform:none;letter-spacing:0;">(search code or name)</span></label>
      <input type="text" class="pm-input" id="crCourseSearch" autocomplete="off" placeholder="Type at least 2 characters&hellip;" oninput="crSearchCourses()" />
      <div id="crCourseResults" class="pm-search-results"></div>
    </div>
    <div id="crCoursePicked" class="pm-picked" style="display:none;"></div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;">
      <div class="pm-fg"><label>3. Academic Year</label><input type="text" class="pm-input" id="crYear" list="crYearList" autocomplete="off" placeholder="e.g. 2024/2025" /><datalist id="crYearList"></datalist></div>
      <div class="pm-fg"><label>4. Semester</label><select class="pm-select" id="crSem"><option value="1">Semester 1</option><option value="2">Semester 2</option><option value="3">Semester 3</option></select></div>
    </div>
    <div class="pm-alert" id="crAlert" style="margin-top:10px;"></div>
    <div id="crDoneWrap" style="display:none;margin-top:12px;">
      <div style="font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#15803d;font-weight:800;margin-bottom:4px;">Registered this session</div>
      <div id="crDoneList" class="pm-existing-wrap" style="max-height:130px;border-color:#bbf7d0;"></div>
    </div>
  </div>
  <div class="pm-modal__foot" style="justify-content:space-between;">
    <div style="font-size:10px;color:#6b7280;align-self:center;" id="crHint">Tip: after each save the student &amp; period are kept — just pick the next course.</div>
    <div style="display:flex;gap:6px;">
      <button type="button" class="pm-btn pm-btn--ghost" id="btnCreateRegClose" onclick="closeCreateReg()">Close</button>
      <button type="button" class="pm-btn pm-btn--primary" id="btnCreateReg" onclick="submitCreateReg()">Register to Course</button>
    </div>
  </div>
</div>

<!-- ── Delete Course Registration (admin) ── -->
<div id="modalDeleteReg" class="pm-modal" style="max-width:460px;">
  <div class="pm-modal__head"><span class="pm-modal__title" style="color:#b42318;">Delete Course Registration</span><button class="pm-modal__close" onclick="closeModal('modalDeleteReg')">&times;</button></div>
  <div class="pm-modal__body">
    <p style="font-size:11px;color:#374151;margin:0 0 10px;">This permanently removes the student's registration for this course in this period. It cannot be undone.</p>
    <div id="drInfo" style="font-size:11px;color:#374151;background:#f8fafc;border:1px solid #e0e5ed;border-radius:4px;padding:8px 10px;margin-bottom:10px;min-height:18px;">Loading&hellip;</div>
    <div class="pm-alert" id="drAlert"></div>
    <label id="drForceWrap" style="display:none;align-items:flex-start;gap:8px;font-size:11px;color:#7a271a;background:#fef3f2;border:1px solid #fda29b;border-radius:4px;padding:9px 10px;margin-top:8px;cursor:pointer;line-height:1.45;">
      <input type="checkbox" id="drForceChk" style="margin-top:1px;flex:0 0 auto;" />
      <span><strong>Force delete.</strong> This registration has a PUBLISHED / final result. Ticking this will also <strong>delete that result from acad_results and recompute the student&rsquo;s GPA</strong>. The removal is logged in the marks audit.</span>
    </label>
  </div>
  <div class="pm-modal__foot">
    <button type="button" class="pm-btn pm-btn--ghost" onclick="closeModal('modalDeleteReg')">Cancel</button>
    <button type="button" class="pm-btn pm-btn--danger" id="btnDeleteReg" onclick="submitDeleteReg()">Delete Registration</button>
  </div>
</div>

<div class="pm-card">
	<div class="pm-card__head">
		<div>
			<div class="pm-card__title">All Marks Controller</div>
			<div class="pm-muted">All marks for all students across all statuses</div>
		</div>
	</div>

	<div class="pm-top-controls">
		<button type="button" class="pm-btn pm-btn--primary" onclick="openCreateReg()">&#43; Register Student to Course</button>
		<span style="width:1px;height:20px;background:#e0e5ed;display:inline-block;"></span>
		<button type="button" class="pm-btn pm-btn--success" onclick="openBatchWizard('approved')">Wizard: Batch Approve</button>
		<button type="button" class="pm-btn pm-btn--primary" onclick="openBatchWizard('published')">Wizard: Batch Publish</button>
	</div>

<%-- A dropped trigger fails silently: marks simply stop being recorded and nothing
     complains. This says so, on the screen that depends on it, the moment it happens.
     It renders nothing at all while every guard is in place. --%>
<asp:Literal ID="litAuditHealth" runat="server" />

<div style="display:none;">
	<asp:Literal ID="litStatTotal" runat="server">0</asp:Literal>
	<asp:Literal ID="litPending" runat="server">0</asp:Literal>
	<asp:Literal ID="litApproved" runat="server">0</asp:Literal>
	<asp:Literal ID="litRejected" runat="server">0</asp:Literal>
	<asp:Literal ID="litPublished" runat="server">0</asp:Literal>
	<asp:Literal ID="litNotEntered" runat="server">0</asp:Literal>
</div>

	<div class="pm-filters">
		<div class="pm-fg"><label>Academic Year</label><asp:DropDownList ID="ddlYear" runat="server" CssClass="pm-select" /></div>
		<div class="pm-fg"><label>Semester</label><asp:DropDownList ID="ddlSemester" runat="server" CssClass="pm-select"><asp:ListItem Value="">All Semesters</asp:ListItem><asp:ListItem Value="1">Sem 1</asp:ListItem><asp:ListItem Value="2">Sem 2</asp:ListItem><asp:ListItem Value="3">Sem 3</asp:ListItem></asp:DropDownList></div>
		<div class="pm-fg"><label>Status</label><asp:DropDownList ID="ddlStatus" runat="server" CssClass="pm-select" Enabled="false"><asp:ListItem Value="pending" Selected="True">Pending Approval Only</asp:ListItem></asp:DropDownList></div>
		<div class="pm-fg"><label>Programme</label><asp:DropDownList ID="ddlProg" runat="server" CssClass="pm-select" /></div>
		<div class="pm-fg"><label>Lecturer</label><asp:DropDownList ID="ddlLecturer" runat="server" CssClass="pm-select" /></div>
		<%-- Beside the lecturer on purpose: the pair answers "which of this lecturer's marks
		     were changed after they were entered, and by whom". Bound from the query string in
		     Page_Load so it survives paging and the browser back button like every other filter. --%>
		<div class="pm-fg"><label>Mark changes</label>
			<asp:Literal ID="litChangedFilter" runat="server" Visible="false" />
			<select id="ddlChanged" class="pm-select" onchange="applyFilters()">
				<option value="">Any</option>
				<option value="cw">Coursework changed</option>
				<option value="exam">Exam changed</option>
				<option value="any">Either changed</option>
				<option value="none">Never changed</option>
			</select>
		</div>
		<%-- Student and course are separate boxes so they can be combined: one student AND
		     one paper. A single field could only ever OR them together. --%>
		<div class="pm-fg"><label>Student</label><asp:TextBox ID="txtSearch" runat="server" CssClass="pm-input" placeholder="Reg no, entry no or name" onkeydown="if(event.key==='Enter')applyFilters();" /></div>
		<div class="pm-fg"><label>Course</label><asp:TextBox ID="txtCourse" runat="server" CssClass="pm-input" placeholder="Course code or name" onkeydown="if(event.key==='Enter')applyFilters();" /></div>
		<div class="pm-fg"><label>Per Page</label><asp:DropDownList ID="ddlPageSize" runat="server" CssClass="pm-select"><asp:ListItem Value="50">50</asp:ListItem><asp:ListItem Value="100" Selected="True">100</asp:ListItem><asp:ListItem Value="200">200</asp:ListItem><asp:ListItem Value="500">500</asp:ListItem></asp:DropDownList></div>
		<div class="pm-fg" style="justify-content:flex-end;"><label>&nbsp;</label><button type="button" class="pm-btn pm-btn--primary" onclick="applyFilters()">Apply</button></div>
	</div>

	<div class="pm-bulk-bar" id="bulkBar"><span class="pm-bulk-label" id="bulkCountLabel">0 selected</span><button type="button" class="pm-btn pm-btn--success" onclick="openBulkModal('approved')">Approve Selected</button><button type="button" class="pm-btn pm-btn--danger" onclick="openBulkModal('rejected')">Reject Selected</button><button type="button" class="pm-btn pm-btn--primary" onclick="openBulkModal('published')">Publish Selected</button><button type="button" class="pm-btn" style="border-color:#7c3aed;color:#7c3aed;" onclick="openBulkSetStatus()">&#9654; Set Status&hellip;</button><button type="button" class="pm-btn pm-btn--ghost" onclick="clearSelection()">Clear Selection</button></div>

	<div class="pm-meta"><span>Showing <strong><asp:Literal ID="litFrom" runat="server">0</asp:Literal></strong>–<strong><asp:Literal ID="litTo" runat="server">0</asp:Literal></strong> of <strong><asp:Literal ID="litTotal" runat="server">0</asp:Literal></strong> records &nbsp;|&nbsp; Page <asp:Literal ID="litPage" runat="server">1</asp:Literal> of <asp:Literal ID="litPageCount" runat="server">1</asp:Literal></span><div class="pm-pager"><asp:Literal ID="litPager" runat="server" /></div></div>

	<div class="pm-table-wrap">
		<table class="pm-table" id="pmTable">
			<colgroup><col class="col-sel" /><col class="col-regno" /><col class="col-student" /><col class="col-course" /><col class="col-prog" /><col class="col-yr" /><col class="col-sem" /><col class="col-mark" /><col class="col-mark" /><col class="col-mark" /><col class="col-pub" /><col class="col-grade" /><col class="col-status" /><col class="col-track" /><col class="col-act" /></colgroup>
			<thead><tr><th class="col-sel"><input type="checkbox" id="chkAll" onclick="toggleAll(this)" title="Select all rows" /></th><th class="col-regno">Reg No</th><th class="col-student">Student</th><th class="col-course">Course</th><th class="col-prog">Prog</th><th class="col-yr">Acad Yr</th><th class="col-sem">Yr &amp; Sem</th><th class="col-mark">CW</th><th class="col-mark">Exam</th><th class="col-mark">Total</th><th class="col-pub">Pub Mk</th><th class="col-grade">Grade</th><th class="col-status">Status</th><th class="col-track" title="Who last changed the coursework or exam mark">Track</th><th class="col-act">Action</th></tr></thead>
			<tbody><asp:Literal ID="litRows" runat="server" /></tbody>
		</table>
	</div>

	<div class="pm-meta" style="border-top:1px solid #e0e5ed;border-bottom:none;"><span><asp:Literal ID="litTotal2" runat="server">0</asp:Literal> total records</span><div class="pm-pager"><asp:Literal ID="litPager2" runat="server" /></div></div>
</div>
</div>

<script type="text/javascript">
(function(){
'use strict';
var _id = null, _bulkAction = null, _selectedIds = [], _wizStep = 1;
function qs(id){ return document.getElementById(id); }
function showToast(msg,type){ var t=qs('pmToast'); if(!t)return; t.textContent=msg; t.className='pm-toast show'+(type?' pm-toast--'+type:''); setTimeout(function(){t.className='pm-toast';},3400); }
function showAlert(elId,msg,type){ var el=qs(elId); if(!el)return; el.textContent=msg; el.className='pm-alert show pm-alert--'+(type||'err'); }
function clearAlert(elId){ var el=qs(elId); if(el) el.className='pm-alert'; }
function callAJAX(method,params,cb){ var xhr=new XMLHttpRequest(); xhr.open('POST','AllMarksController.aspx/'+method,true); xhr.setRequestHeader('Content-Type','application/json; charset=utf-8'); xhr.onload=function(){ try{ var o=JSON.parse(xhr.responseText); cb(typeof o.d==='string'?JSON.parse(o.d):o.d); } catch(e){ cb({success:false,message:'Parse error.'}); } }; xhr.onerror=function(){ cb({success:false,message:'Network error.'}); }; xhr.send(JSON.stringify(params)); }
function statusPill(s){ var map={pending:'pm-pill--pending',approved:'pm-pill--approved',rejected:'pm-pill--rejected',published:'pm-pill--published',not_entered:'pm-pill--not_entered'}; var css=map[s]||'pm-pill--pending'; return '<span class="pm-pill '+css+'">'+(s||'pending').replace('_',' ')+'</span>'; }
function marksBanner(cw,exam,tot){ function box(lbl,v){ return '<div class="pm-marks-disp__box"><div class="pm-marks-disp__val">'+(v!=null&&v!==''?v:'—')+'</div><div class="pm-marks-disp__lbl">'+lbl+'</div></div>'; } return box('Course Work',cw)+box('Exam',exam)+box('Total',tot); }
function dlRow(lbl,val){ return '<dt>'+lbl+'</dt><dd>'+(val||'—')+'</dd>'; }
document.addEventListener('click',function(e){ if(!e.target.closest('.pm-row-wrap')) document.querySelectorAll('.pm-row-menu.open').forEach(function(m){m.classList.remove('open');}); });
window.closeMenuThen=function(btn,fn){ btn.closest('.pm-row-menu').classList.remove('open'); fn(); };
window.toggleRowMenu=function(btn){ var menu=btn.parentNode.querySelector('.pm-row-menu'); var wasOpen=menu.classList.contains('open'); document.querySelectorAll('.pm-row-menu.open').forEach(function(m){m.classList.remove('open');}); if(!wasOpen) menu.classList.add('open'); };
function openModal(id){ qs('pmOverlay').classList.add('show'); qs(id).classList.add('show'); }
window.closeModal=function(id){ qs('pmOverlay').classList.remove('show'); qs(id).classList.remove('show'); _id=null; };
window.closeAllModals=function(){ ['modalDetails','modalReview','modalPublish','modalEdit','modalBulk','modalBatchWizard','modalSetStatus','modalCreateReg','modalDeleteReg'].forEach(function(m){ var el=qs(m); if(el) el.classList.remove('show'); }); qs('pmOverlay').classList.remove('show'); _id=null; };
function goWithQuery(params){ var query = new URLSearchParams(); Object.keys(params).forEach(function(k){ if(params[k]!==undefined && params[k]!==null) query.set(k, params[k]); }); window.location.href='AllMarksController.aspx?'+query.toString(); }
// Restores the Mark-changes filter after a page turn. It is a plain <select>, so ASP.NET
// does not do it for us.
(function(){ var v='<%= litChangedFilter.Text %>'; var el=document.getElementById('ddlChanged'); if(el&&v) el.value=v; })();

window.applyFilters=function(){ var year=qs('<%= ddlYear.ClientID %>').value, sem=qs('<%= ddlSemester.ClientID %>').value, status=qs('<%= ddlStatus.ClientID %>').value, prog=qs('<%= ddlProg.ClientID %>').value, lect=qs('<%= ddlLecturer.ClientID %>').value, ps=qs('<%= ddlPageSize.ClientID %>').value, q=qs('<%= txtSearch.ClientID %>').value, qc=qs('<%= txtCourse.ClientID %>').value, chg=(qs('ddlChanged')?qs('ddlChanged').value:''); goWithQuery({pg:'1',year:year,sem:sem,status:status,prog:prog,lect:lect,ps:ps,q:q,qc:qc,chg:chg}); };
window.filterByStatus=function(){ var year=qs('<%= ddlYear.ClientID %>').value, sem=qs('<%= ddlSemester.ClientID %>').value, status=qs('<%= ddlStatus.ClientID %>').value, prog=qs('<%= ddlProg.ClientID %>').value, lect=qs('<%= ddlLecturer.ClientID %>').value, ps=qs('<%= ddlPageSize.ClientID %>').value, q=qs('<%= txtSearch.ClientID %>').value, qc=qs('<%= txtCourse.ClientID %>').value, chg=(qs('ddlChanged')?qs('ddlChanged').value:''); goWithQuery({pg:'1',year:year,sem:sem,status:status,prog:prog,lect:lect,ps:ps,q:q,qc:qc,chg:chg}); };
function updateBulkBar(){ _selectedIds=[]; document.querySelectorAll('.pm-row-chk:checked').forEach(function(c){ _selectedIds.push(parseInt(c.value,10)); }); var bar=qs('bulkBar'); if(_selectedIds.length>0){ bar.classList.add('show'); qs('bulkCountLabel').textContent=_selectedIds.length+' selected'; } else bar.classList.remove('show'); }
window.toggleAll=function(chk){ document.querySelectorAll('.pm-row-chk').forEach(function(c){ c.checked=chk.checked; }); updateBulkBar(); };
document.addEventListener('change',function(e){ if(e.target&&e.target.classList.contains('pm-row-chk')) updateBulkBar(); });
window.clearSelection=function(){ document.querySelectorAll('.pm-row-chk').forEach(function(c){ c.checked=false; }); if(qs('chkAll')) qs('chkAll').checked=false; updateBulkBar(); };
window.openDetails=function(id){ _id=id; clearAlert('detailsAlert'); qs('detailsGrid').innerHTML='<dt style="font-size:10px;color:#6b7280;">Loading…</dt>'; qs('detailsMarksDisp').innerHTML=''; qs('detailsComment').innerHTML=''; openModal('modalDetails'); callAJAX('GetRecordDetails',{id:id},function(d){ if(!d.success){ showAlert('detailsAlert',d.message||'Unable to load details.','err'); return; } var r=d.record; qs('detailsModalTitle').textContent='Details: '+r.regno+' — '+r.courseID; qs('detailsGrid').innerHTML=dlRow('Reg No',r.regno)+dlRow('Student',r.student_name||'—')+dlRow('Course',r.courseID+' — '+(r.course_name||'—'))+dlRow('Programme',r.prog_id||'—')+dlRow('Academic Year',r.acad_year)+dlRow('Semester','Yr '+(r.study_year||'—')+', Sem '+r.semester)+dlRow('Submitted By',r.submitted_by||'—')+dlRow('Lecturer',r.lecturer_name||'—')+dlRow('Current Status',statusPill(r.provisional_marks_status))+dlRow('Reviewed By',r.provisional_marks_reviewed_by||'—')+dlRow('Review Date',r.provisional_marks_review_date||'—')+dlRow('Published By',r.provisional_published_by||'—')+dlRow('Published Date',r.provisional_published_date||'—'); qs('detailsMarksDisp').innerHTML=marksBanner(r.provisional_course_work_marks,r.provisional_exam_marks,r.provisional_total_marks); qs('detailsComment').innerHTML='<strong>Review Comment:</strong> '+(r.provisional_marks_review_comments||'—')+markHistory(r.history); }); };

// Every recorded change to this row's coursework and exam marks, newest first.
//
// Written as a list of movements rather than a table of fields, because the question being
// asked is nearly always "what happened to this mark, and who did it" - which reads as a
// sequence. The old value and the new one are both shown on every line: a trail that only
// records the new figure cannot answer whether a mark went up or down.
window.markHistory=function(list){
    if(!list) return '';
    var h='<div class="pm-hist"><div class="pm-hist__h">Mark change history</div>';
    if(!list.length){
        return h+'<div class="pm-hist__none">No change has been recorded for this row. '
              +'Marks entered before recording began appear only if they were captured in the activity log.</div></div>';
    }
    for(var i=0;i<list.length;i++){
        var x=list[i];
        var cls='pm-hist__i'+(x.cw?' pm-hist__i--cw':(x.ex?' pm-hist__i--ex':''));
        h+='<div class="'+cls+'">';
        h+='<div class="pm-hist__top"><span class="pm-hist__when">'+esc(x.at)+'</span>'
          +'<span class="pm-hist__by">by <b>'+esc(x.by||'system')+'</b></span>';
        if(x.action==='MIGRATE') h+='<span class="pm-track__hist" title="Recovered from the activity log">log</span>';
        h+='</div>';

        var moves=[];
        if(x.cw) moves.push('<b>Coursework</b> <span class="pm-hist__from">'+mk(x.oldCw)+'</span> &rarr; <span class="pm-hist__to">'+mk(x.newCw)+'</span>');
        if(x.ex) moves.push('<b>Exam</b> <span class="pm-hist__from">'+mk(x.oldExam)+'</span> &rarr; <span class="pm-hist__to">'+mk(x.newExam)+'</span>');
        if(!moves.length) moves.push('<b>Marks entered</b>');
        h+='<div class="pm-hist__move">'+moves.join(' &nbsp;&middot;&nbsp; ')+'</div>';

        if(x.oldTotal!==null&&x.newTotal!==null&&x.oldTotal!==x.newTotal)
            h+='<div class="pm-hist__move">Total <span class="pm-hist__from">'+mk(x.oldTotal)+'</span> &rarr; <span class="pm-hist__to">'+mk(x.newTotal)+'</span></div>';
        if(x.oldStatus&&x.newStatus&&x.oldStatus!==x.newStatus)
            h+='<div class="pm-hist__move">Status '+esc(x.oldStatus)+' &rarr; '+esc(x.newStatus)+'</div>';

        var meta=[];
        if(x.source) meta.push(esc(x.source));
        if(x.ip) meta.push('IP '+esc(x.ip));
        if(x.reason) meta.push(esc(x.reason));
        if(meta.length) h+='<div class="pm-hist__meta">'+meta.join(' &middot; ')+'</div>';
        h+='</div>';
    }
    return h+'</div>';
};
// A mark that was absent is a dash, not a zero. "0" and "never entered" are different facts.
function mk(v){ return (v===null||v===undefined||v==='')?'&ndash;':esc(String(v)); }
// esc() is the page's own, declared further down; function declarations hoist, so it is
// available here. A second copy would only be a weaker one - the real one escapes quotes too.
window.openReview=function(id){ _id=id; clearAlert('reviewAlert'); qs('reviewDetailGrid').innerHTML='<dt style="font-size:10px;color:#6b7280;">Loading…</dt>'; qs('reviewMarksDisp').innerHTML=''; qs('reviewComment').value=''; qs('reviewPrevComment').style.display='none'; openModal('modalReview'); callAJAX('GetProvisionalRecord',{id:id},function(d){ if(!d.success){showToast(d.message||'Error','err');window.closeModal('modalReview');return;} var r=d.record; qs('modalReviewTitle').textContent='Review: '+r.regno+' — '+r.courseID; qs('reviewDetailGrid').innerHTML=dlRow('Reg No',r.regno)+dlRow('Course',r.courseID)+dlRow('Programme',r.prog_id)+dlRow('Academic Year',r.acad_year)+dlRow('Semester','Yr '+(r.study_year||'—')+', Sem '+r.semester)+dlRow('Submitted By',r.submitted_by||'—')+dlRow('Current Status',statusPill(r.provisional_marks_status))+dlRow('Reviewed By',r.provisional_marks_reviewed_by||'—'); qs('reviewMarksDisp').innerHTML=marksBanner(r.provisional_course_work_marks,r.provisional_exam_marks,r.provisional_total_marks); if(r.provisional_marks_review_comments){ qs('reviewPrevComment').style.display='block'; qs('reviewPrevComment').innerHTML='<strong>Previous comment:</strong> '+r.provisional_marks_review_comments; qs('reviewComment').value=r.provisional_marks_review_comments; } }); };
window.submitReview=function(action){ if(!_id)return; var comment=qs('reviewComment').value.trim(); if(action==='rejected'&&!comment){showAlert('reviewAlert','A comment is required when rejecting.','err');return;} clearAlert('reviewAlert'); qs('btnApprove').disabled=true; qs('btnReject').disabled=true; callAJAX('ReviewProvisionalMarks',{id:_id,action:action,comment:comment},function(d){ qs('btnApprove').disabled=false; qs('btnReject').disabled=false; if(d.success){showToast(action==='approved'?'Marks approved.':'Marks rejected.','ok');window.closeModal('modalReview');setTimeout(function(){location.reload();},800);} else showAlert('reviewAlert',d.message||'Operation failed.','err'); }); };
window.openPublish=function(id){ _id=id; clearAlert('publishAlert'); qs('publishMarksDisp').innerHTML='<div style="padding:10px;color:#6b7280;font-size:11px;">Loading…</div>'; qs('publishInfo').innerHTML=''; openModal('modalPublish'); callAJAX('GetProvisionalRecord',{id:id},function(d){ if(!d.success){showToast(d.message||'Error','err');window.closeModal('modalPublish');return;} var r=d.record; qs('publishMarksDisp').innerHTML=marksBanner(r.provisional_course_work_marks,r.provisional_exam_marks,r.provisional_total_marks); qs('publishInfo').innerHTML='<strong>'+r.regno+'</strong> — '+r.courseID+' &nbsp;|&nbsp; '+r.acad_year+' · Yr '+(r.study_year||'—')+', Sem '+r.semester; }); };
window.submitPublish=function(){ if(!_id)return; clearAlert('publishAlert'); qs('btnPublishConfirm').disabled=true; callAJAX('PublishMarks',{id:_id},function(d){ qs('btnPublishConfirm').disabled=false; if(d.success){showToast('Marks published to final results.','ok');window.closeModal('modalPublish');setTimeout(function(){location.reload();},800);} else showAlert('publishAlert',d.message||'Publish failed.','err'); }); };
window.openEdit=function(id){ _id=id; clearAlert('editAlert'); qs('editDetailGrid').innerHTML='<dt style="font-size:10px;color:#6b7280;">Loading…</dt>'; qs('editCW').value=''; qs('editExam').value=''; qs('editTotal').value=''; qs('editNote').value=''; openModal('modalEdit'); callAJAX('GetProvisionalRecord',{id:id},function(d){ if(!d.success){showToast(d.message||'Error','err');window.closeModal('modalEdit');return;} var r=d.record; qs('editModalTitle').textContent='Edit Marks: '+r.regno+' — '+r.courseID; qs('editDetailGrid').innerHTML=dlRow('Reg No',r.regno)+dlRow('Course',r.courseID)+dlRow('Programme',r.prog_id)+dlRow('Year / Sem','Yr '+(r.study_year||'—')+', Sem '+r.semester)+dlRow('Current Status',statusPill(r.provisional_marks_status))+dlRow('Submitted By',r.submitted_by||'—'); if(r.provisional_course_work_marks!=null) qs('editCW').value=r.provisional_course_work_marks; if(r.provisional_exam_marks!=null) qs('editExam').value=r.provisional_exam_marks; if(r.provisional_total_marks!=null) qs('editTotal').value=r.provisional_total_marks; _totalOverridden=false; editSyncTotal(); }); };

/* ---- Total: computed, not remembered -------------------------------------------------
   The Total box is pre-filled from the record so the operator can see the current figure.
   That display value must NOT be mistaken for an override: before this, editing CW or Exam
   left the OLD total sitting in the box, it was posted as an explicit total, and the wrong
   figure was saved. _totalOverridden is only set when the operator types in the box
   themselves, and clearing the box hands control back to the automatic sum. */
var _totalOverridden=false;
(function(){
    function bind(){
        var cw=qs('editCW'), ex=qs('editExam'), tt=qs('editTotal');
        if(!cw||!ex||!tt) return;
        cw.addEventListener('input', editSyncTotal);
        ex.addEventListener('input', editSyncTotal);
        /* Typing in the Total box is what makes it an override. Emptying it releases the
           override, so the sum takes over again — otherwise there would be no way back. */
        tt.addEventListener('input', function(){
            _totalOverridden = (tt.value.trim() !== '');
            if(!_totalOverridden) editSyncTotal(); else editSyncTotal();
        });
    }
    if(document.readyState!=='loading') bind(); else document.addEventListener('DOMContentLoaded', bind);
})();
function editSyncTotal(){
    var cwEl=qs('editCW'), exEl=qs('editExam'), tEl=qs('editTotal'), mode=qs('editTotalMode');
    var cw=cwEl.value.trim(), ex=exEl.value.trim();
    var both=(cw!=='' && ex!=='');
    if(!_totalOverridden){
        tEl.value = both ? String((parseInt(cw,10)||0)+(parseInt(ex,10)||0)) : '';
        tEl.readOnly = both;
        if(mode){ mode.textContent = both ? '(auto = CW + Exam)' : '(enter CW and Exam)'; mode.style.color='#16a34a'; }
    } else {
        tEl.readOnly = false;
        if(mode){ mode.textContent='(overridden — clear to auto)'; mode.style.color='#b45309'; }
    }
}
window.submitEdit=function(){ if(!_id)return; var cw=qs('editCW').value.trim(), ex=qs('editExam').value.trim(), note=qs('editNote').value.trim(); var tot=_totalOverridden ? qs('editTotal').value.trim() : ''; clearAlert('editAlert'); qs('btnEditSave').disabled=true; callAJAX('SaveAdminMarks',{id:_id,cw:cw===''?null:parseInt(cw,10),exam:ex===''?null:parseInt(ex,10),total:tot===''?null:parseInt(tot,10),note:note},function(d){ qs('btnEditSave').disabled=false; if(d.success){showToast('Marks saved.','ok');window.closeModal('modalEdit');setTimeout(function(){location.reload();},800);} else showAlert('editAlert',d.message||'Save failed.','err'); }); };
window.resetToPending=function(id){ if(!confirm('Reset this record back to Pending status? The lecturer will need to re-submit.')) return; callAJAX('ResetToPending',{id:id},function(d){ if(d.success){showToast('Record reset to pending.','ok');setTimeout(function(){location.reload();},800);} else showToast(d.message||'Reset failed.','err'); }); };
window.openBulkModal=function(action){ if(_selectedIds.length===0){showToast('No records selected.','err');return;} _bulkAction=action; var labels={approved:'Approve '+_selectedIds.length+' records',rejected:'Reject '+_selectedIds.length+' records',published:'Publish '+_selectedIds.length+' records to final results'}; qs('bulkModalTitle').textContent='Bulk '+action.charAt(0).toUpperCase()+action.slice(1); qs('bulkDesc').textContent=labels[action]+'.'; qs('bulkComment').value=''; clearAlert('bulkAlert'); qs('bulkReqNote').style.display=action==='rejected'?'':'none'; qs('bulkCommentWrap').style.display=action==='published'?'none':''; openModal('modalBulk'); };
window.submitBulk=function(){ if(!_bulkAction||_selectedIds.length===0)return; var comment=qs('bulkComment').value.trim(); if(_bulkAction==='rejected'&&!comment){showAlert('bulkAlert','Comment required for bulk rejection.','err');return;} clearAlert('bulkAlert'); qs('btnBulkConfirm').disabled=true; callAJAX('BulkAction',{ids:_selectedIds,action:_bulkAction,comment:comment},function(d){ qs('btnBulkConfirm').disabled=false; if(d.success){showToast(d.message||'Bulk action completed.','ok');window.closeModal('modalBulk');setTimeout(function(){location.reload();},800);} else showAlert('bulkAlert',d.message||'Bulk action failed.','err'); }); };

function setWizStep(step){ _wizStep=step; ['wizPanel1','wizPanel2','wizPanel3'].forEach(function(id,i){ var el=qs(id); if(el) el.className='pm-wiz-panel'+(i+1===step?' active':''); }); ['wizStep1','wizStep2','wizStep3'].forEach(function(id,i){ var el=qs(id); if(el) el.className='pm-wiz-step'+(i+1===step?' active':''); }); qs('wizBack').style.display=step===1?'none':'inline-flex'; qs('wizNext').style.display=step===3?'none':'inline-flex'; qs('wizRun').style.display=step===3?'inline-flex':'none'; }
function syncWizardLookups(){ var srcProg=qs('<%= ddlProg.ClientID %>'), srcYear=qs('<%= ddlYear.ClientID %>'); qs('wizProg').innerHTML=srcProg.innerHTML; qs('wizYear').innerHTML=srcYear.innerHTML; qs('wizProg').value=srcProg.value; qs('wizYear').value=srcYear.value; qs('wizSem').value=qs('<%= ddlSemester.ClientID %>').value; }
function wizardPayload(){ return { action:qs('wizAction').value, scope:qs('wizScope').value, ids:_selectedIds, year:qs('wizYear').value, sem:qs('wizSem').value, prog:qs('wizProg').value, comment:'' }; }
function wizardPreview(){ clearAlert('wizAlert'); qs('wizSummary').textContent='Preparing preview...'; qs('wizPreviewRows').innerHTML=''; callAJAX('PreviewBatchWorkflow',wizardPayload(),function(d){ if(!d.success){ showAlert('wizAlert',d.message||'Preview failed.','err'); return; } qs('wizSummary').innerHTML='<strong>'+d.count+'</strong> eligible record(s).'; var html=''; (d.rows||[]).forEach(function(r){ html+='<tr><td>'+ (r.regno||'') +'</td><td>'+ (r.courseID||'') +'</td><td>'+ (r.prog_id||'') +'</td><td>'+ (r.acad_year||'') +'</td><td>'+ (r.semester||'') +'</td><td>'+ (r.status||'') +'</td><td>'+ (r.total_marks||'') +'</td></tr>'; }); if(!html) html='<tr><td colspan="7" style="text-align:center;color:#6b7280;">No records to preview.</td></tr>'; qs('wizPreviewRows').innerHTML=html; }); }
window.openBatchWizard=function(action){ syncWizardLookups(); qs('wizAction').value=action||'approved'; qs('wizScope').value='selected'; qs('wizScopeFilters').style.display='none'; qs('wizScope').onchange=function(){ qs('wizScopeFilters').style.display=this.value==='programme'?'grid':'none'; }; setWizStep(1); openModal('modalBatchWizard'); };
window.wizardBack=function(){ if(_wizStep>1) setWizStep(_wizStep-1); };
window.wizardNext=function(){ if(_wizStep===1){ setWizStep(2); return; } if(_wizStep===2){ setWizStep(3); wizardPreview(); } };
window.wizardRun=function(){ clearAlert('wizAlert'); qs('wizRun').disabled=true; callAJAX('ExecuteBatchWorkflow',wizardPayload(),function(d){ qs('wizRun').disabled=false; if(d.success){ showToast(d.message||'Workflow completed.','ok'); window.closeModal('modalBatchWizard'); setTimeout(function(){ location.reload(); },800); } else showAlert('wizAlert',d.message||'Workflow failed.','err'); }); };

/* ── Set Status (single + batch) ─────────────────────────────── */
var _ssMode='single'; // 'single' | 'bulk'

function _resetSetStatusModal(){
  document.querySelectorAll('input[name="ssStatus"]').forEach(function(r){ r.checked=false; });
  qs('ssComment').value='';
  qs('ssCommentReq').style.display='none';
  qs('ssPublishNote').style.display='none';
  clearAlert('ssAlert');
  qs('btnSetStatusConfirm').disabled=false;
  // un-highlight all labels
  ['ssLblPending','ssLblApproved','ssLblRejected','ssLblPublished'].forEach(function(id){
    var el=qs(id); if(el){ el.style.background='#fff'; el.style.borderColor='#d6deea'; }
  });
}

window.onSetStatusChange=function(radio){
  var v=radio?radio.value:'';
  qs('ssCommentReq').style.display = v==='rejected' ? 'inline' : 'none';
  qs('ssPublishNote').style.display = v==='published' ? 'block' : 'none';
  clearAlert('ssAlert');
  // highlight selected label
  var map={pending:'ssLblPending',approved:'ssLblApproved',rejected:'ssLblRejected',published:'ssLblPublished'};
  var bgMap={pending:'#fff7ed',approved:'#f0fdf4',rejected:'#fef2f2',published:'#eef2ff'};
  var bdMap={pending:'#f59e0b',approved:'#22c55e',rejected:'#ef4444',published:'#6366f1'};
  ['pending','approved','rejected','published'].forEach(function(s){
    var el=qs(map[s]); if(!el) return;
    el.style.background = s===v ? bgMap[s] : '#fff';
    el.style.borderColor= s===v ? bdMap[s] : '#d6deea';
  });
};

window.openSetStatus=function(id){
  _ssMode='single'; _id=id;
  _resetSetStatusModal();
  // Load record info
  qs('setStatusTitle').textContent='Set Status';
  qs('setStatusInfo').textContent='Loading…';
  openModal('modalSetStatus');
  callAJAX('GetProvisionalRecord',{id:id},function(d){
    if(!d.success){ qs('setStatusInfo').textContent='Could not load record.'; return; }
    var r=d.record;
    qs('setStatusTitle').textContent='Set Status — '+r.regno+' / '+r.courseID;
    qs('setStatusInfo').innerHTML=
      '<strong>'+r.regno+'</strong> &mdash; '+r.courseID+' &nbsp;|&nbsp; '+r.acad_year+' · Yr '+(r.study_year||'—')+', Sem '+r.semester+
      '<br/>Current status: '+statusPill(r.provisional_marks_status);
    // Pre-select current status
    var cur=r.provisional_marks_status;
    document.querySelectorAll('input[name="ssStatus"]').forEach(function(radio){
      if(radio.value===cur){ radio.checked=true; onSetStatusChange(radio); }
    });
  });
};

window.openBulkSetStatus=function(){
  if(_selectedIds.length===0){ showToast('No records selected.','err'); return; }
  _ssMode='bulk';
  _resetSetStatusModal();
  qs('setStatusTitle').textContent='Set Status — '+_selectedIds.length+' selected';
  qs('setStatusInfo').innerHTML='Changing status for <strong>'+_selectedIds.length+'</strong> selected record(s). All will be set to the chosen status.';
  openModal('modalSetStatus');
};

window.submitSetStatus=function(){
  var selected=document.querySelector('input[name="ssStatus"]:checked');
  if(!selected){ showAlert('ssAlert','Please select a target status.','err'); return; }
  var newStatus=selected.value;
  var comment=qs('ssComment').value.trim();
  if(newStatus==='rejected'&&!comment){ showAlert('ssAlert','Comment is required when setting to Rejected.','err'); return; }
  clearAlert('ssAlert');
  qs('btnSetStatusConfirm').disabled=true;

  if(_ssMode==='single'){
    callAJAX('ForceSetStatus',{id:_id,status:newStatus,comment:comment},function(d){
      qs('btnSetStatusConfirm').disabled=false;
      if(d.success){ showToast(d.message||'Status updated.','ok'); window.closeModal('modalSetStatus'); setTimeout(function(){ location.reload(); },800); }
      else showAlert('ssAlert',d.message||'Update failed.','err');
    });
  } else {
    callAJAX('BulkForceSetStatus',{ids:_selectedIds,status:newStatus,comment:comment},function(d){
      qs('btnSetStatusConfirm').disabled=false;
      if(d.success){ showToast(d.message||'Bulk status updated.','ok'); window.closeModal('modalSetStatus'); setTimeout(function(){ location.reload(); },800); }
      else showAlert('ssAlert',d.message||'Bulk update failed.','err');
    });
  }
};

/* ── Admin: create / delete course registration ─────────────────── */
function esc(s){ return (s==null?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
var _crStud=null, _crCourse=null, _crStudList=[], _crCourseList=[], _crStudT=null, _crCourseT=null, _drId=null, _crCount=0;

// close search dropdowns when clicking outside them
document.addEventListener('click',function(e){
  if(!e.target.closest('#crStudSearch, #crStudResults')){ var a=qs('crStudResults'); if(a) a.classList.remove('show'); }
  if(!e.target.closest('#crCourseSearch, #crCourseResults')){ var b=qs('crCourseResults'); if(b) b.classList.remove('show'); }
});

window.openCreateReg=function(){
  _crStud=null; _crCourse=null; _crStudList=[]; _crCourseList=[]; _crCount=0;
  qs('crStudSearch').value=''; qs('crCourseSearch').value=''; qs('crYear').value=''; qs('crSem').value='1';
  qs('crStudPicked').style.display='none'; qs('crCoursePicked').style.display='none'; qs('crExisting').style.display='none';
  qs('crStudResults').classList.remove('show'); qs('crCourseResults').classList.remove('show');
  clearAlert('crAlert'); qs('btnCreateReg').disabled=false;
  // reset the "registered this session" tracker
  qs('crDoneWrap').style.display='none'; qs('crDoneList').innerHTML='';
  var badge=qs('crSessionBadge'); badge.style.display='none'; badge.textContent='0 added';
  var closeBtn=qs('btnCreateRegClose'); closeBtn.textContent='Close'; closeBtn.className='pm-btn pm-btn--ghost';
  // populate the academic-year datalist from the page's year filter
  var src=qs('<%= ddlYear.ClientID %>'), dl=qs('crYearList');
  if(src&&dl){ var h=''; for(var i=0;i<src.options.length;i++){ var v=src.options[i].value; if(v) h+='<option value="'+esc(v)+'"></option>'; } dl.innerHTML=h; }
  openModal('modalCreateReg');
};

// Close the register modal — reload the underlying list only if something was registered this session.
window.closeCreateReg=function(){
  if(_crCount>0){ window.closeModal('modalCreateReg'); setTimeout(function(){ location.reload(); },250); }
  else window.closeModal('modalCreateReg');
};

// Append a successfully-registered course to the session list + bump the badge.
function crAddDone(code,year,sem){
  qs('crDoneWrap').style.display='block';
  var row=document.createElement('div');
  row.className='pm-existing-row';
  row.innerHTML='<span style="color:#15803d;font-weight:800;">&#10003;</span> <strong>'+esc(code)+'</strong> &mdash; '+esc(year)+' &middot; Sem '+esc(sem)+(_crStud?(' &middot; '+esc(_crStud.regno)):'');
  var list=qs('crDoneList'); list.insertBefore(row, list.firstChild);
  var badge=qs('crSessionBadge'); badge.style.display='inline-block'; badge.textContent=_crCount+' added';
}

// Load + render the picked student's existing registrations (also refreshed after each new save).
function crLoadExisting(regno){
  callAJAX('GetStudentRegSummary',{regno:regno},function(d){
    if(!d||!d.success) return;
    if(d.in_scope===false){ showAlert('crAlert','This student is outside your faculty/department scope; registering them will be blocked.','err'); }
    var box=qs('crExisting');
    if(d.existing&&d.existing.length){
      var h='<div style="font-size:9px;text-transform:uppercase;letter-spacing:.4px;color:#64748b;font-weight:800;margin-bottom:4px;">Already registered ('+d.existing.length+' period(s))</div><div class="pm-existing-wrap">';
      d.existing.forEach(function(x){ h+='<div class="pm-existing-row"><strong>'+esc(x.acad_year)+' &middot; Sem '+esc(x.semester)+'</strong> &mdash; '+esc(x.courses||'')+'</div>'; });
      h+='</div>';
      box.innerHTML=h; box.style.display='block';
    } else { box.style.display='none'; }
  });
}

window.crSearchStudents=function(){
  var q=qs('crStudSearch').value.trim(); _crStud=null;
  qs('crStudPicked').style.display='none'; qs('crExisting').style.display='none'; clearAlert('crAlert');
  var box=qs('crStudResults');
  if(_crStudT) clearTimeout(_crStudT);
  if(q.length<2){ box.innerHTML=''; box.classList.remove('show'); return; }
  _crStudT=setTimeout(function(){
    callAJAX('RegSearchStudents',{q:q},function(d){
      if(!d||!d.success){ box.classList.remove('show'); return; }
      _crStudList=d.students||[];
      if(!_crStudList.length){ box.innerHTML='<div class="pm-search-item" style="color:#6b7280;">No students found.</div>'; box.classList.add('show'); return; }
      var h=''; _crStudList.forEach(function(s,i){ h+='<div class="pm-search-item" onclick="crPickStudent('+i+')"><strong>'+esc(s.regno)+'</strong> &mdash; '+esc(s.name||'')+'<br><small>'+esc(s.prog||'')+(s.status?(' &middot; '+esc(s.status)):'')+'</small></div>'; });
      box.innerHTML=h; box.classList.add('show');
    });
  },220);
};
window.crPickStudent=function(i){
  var s=_crStudList[i]; if(!s) return; _crStud=s;
  qs('crStudSearch').value=s.regno; qs('crStudResults').classList.remove('show');
  qs('crStudPicked').style.display='block';
  qs('crStudPicked').innerHTML='Selected student: <strong>'+esc(s.regno)+'</strong> &mdash; '+esc(s.name||'')+'<br><small style="color:#6b7280;">'+esc(s.prog||'')+'</small>';
  clearAlert('crAlert');
  crLoadExisting(s.regno);
};

window.crSearchCourses=function(){
  var q=qs('crCourseSearch').value.trim(); _crCourse=null; qs('crCoursePicked').style.display='none';
  var box=qs('crCourseResults');
  if(_crCourseT) clearTimeout(_crCourseT);
  if(q.length<2){ box.innerHTML=''; box.classList.remove('show'); return; }
  _crCourseT=setTimeout(function(){
    callAJAX('RegSearchCourses',{q:q},function(d){
      if(!d||!d.success){ box.classList.remove('show'); return; }
      _crCourseList=d.courses||[];
      if(!_crCourseList.length){ box.innerHTML='<div class="pm-search-item" style="color:#6b7280;">No courses found.</div>'; box.classList.add('show'); return; }
      var h=''; _crCourseList.forEach(function(c,i){ h+='<div class="pm-search-item" onclick="crPickCourse('+i+')"><strong>'+esc(c.code)+'</strong>'+(c.name?(' &mdash; '+esc(c.name)):'')+'</div>'; });
      box.innerHTML=h; box.classList.add('show');
    });
  },220);
};
window.crPickCourse=function(i){
  var c=_crCourseList[i]; if(!c) return; _crCourse=c;
  qs('crCourseSearch').value=c.code; qs('crCourseResults').classList.remove('show');
  qs('crCoursePicked').style.display='block';
  qs('crCoursePicked').innerHTML='Selected course: <strong>'+esc(c.code)+'</strong>'+(c.name?(' &mdash; '+esc(c.name)):'');
};

window.submitCreateReg=function(){
  if(!_crStud){ showAlert('crAlert','Select a student first.','err'); return; }
  if(!_crCourse){ showAlert('crAlert','Select a course first.','err'); return; }
  var year=qs('crYear').value.trim(), sem=qs('crSem').value, semN=parseInt(sem,10);
  if(!year){ showAlert('crAlert','Academic year is required.','err'); return; }
  clearAlert('crAlert'); qs('btnCreateReg').disabled=true;
  var code=_crCourse.code;
  callAJAX('CreateRegistration',{regno:_crStud.regno,course:code,acadYear:year,semester:semN},function(d){
    qs('btnCreateReg').disabled=false;
    if(d&&d.success){
      _crCount++;
      showToast(d.message||'Student registered.','ok');
      crAddDone(code, year, sem);
      showAlert('crAlert','✓ Registered '+code+' ('+year+', Sem '+sem+'). The student & period are kept - pick another course to add more.','ok');
      // keep the student + academic year + semester; clear only the course so the next one can be added
      _crCourse=null; _crCourseList=[];
      qs('crCourseSearch').value=''; qs('crCoursePicked').style.display='none'; qs('crCourseResults').classList.remove('show');
      // refresh the student's existing-registrations panel to include what we just added
      if(_crStud) crLoadExisting(_crStud.regno);
      // once something is saved, the Close button also refreshes the underlying list
      var closeBtn=qs('btnCreateRegClose'); closeBtn.textContent='Done & Refresh'; closeBtn.className='pm-btn pm-btn--success';
      setTimeout(function(){ qs('crCourseSearch').focus(); },60);
    }
    else showAlert('crAlert',(d&&d.message)||'Registration failed.','err');
  });
};

window.openDeleteReg=function(id){
  _drId=id; clearAlert('drAlert'); qs('btnDeleteReg').disabled=false; qs('drInfo').textContent='Loading…';
  // reset the force state each time the modal opens
  qs('drForceWrap').style.display='none'; qs('drForceChk').checked=false; qs('btnDeleteReg').textContent='Delete Registration';
  openModal('modalDeleteReg');
  callAJAX('GetProvisionalRecord',{id:id},function(d){
    if(!d||!d.success){ qs('drInfo').textContent='Could not load record.'; return; }
    var r=d.record;
    qs('drInfo').innerHTML='<strong>'+esc(r.regno)+'</strong> &mdash; '+esc(r.courseID)+'<br>'+esc(r.acad_year)+' &middot; Yr '+esc(r.study_year||'—')+', Sem '+esc(r.semester)+'<br>Status: '+statusPill(r.provisional_marks_status);
    if(r.provisional_marks_status==='published'){ showAlert('drAlert','This record is PUBLISHED. You can still delete it below — tick Force delete to also remove the final result and recompute GPA.','err'); }
  });
};
window.submitDeleteReg=function(){
  if(!_drId) return; clearAlert('drAlert');
  var force = qs('drForceChk') && qs('drForceChk').checked;
  qs('btnDeleteReg').disabled=true;
  callAJAX('DeleteRegistration',{id:_drId,force:force},function(d){
    qs('btnDeleteReg').disabled=false;
    if(d&&d.success){ showToast(d.message||'Registration deleted.','ok'); window.closeModal('modalDeleteReg'); setTimeout(function(){ location.reload(); },900); return; }
    if(d&&d.canForce){
      // published/final-result — reveal the force option and turn the button into a Force Delete
      qs('drForceWrap').style.display='flex';
      qs('btnDeleteReg').textContent='Force Delete';
      showAlert('drAlert',(d&&d.message)||'This registration has published results.','err');
      return;
    }
    showAlert('drAlert',(d&&d.message)||'Delete failed.','err');
  });
};
})();
</script>
</asp:Content>
