<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="SemesterDeletionRequestsController.aspx.cs" Inherits="COOPERP_NewScreens_SemesterDeletionRequestsController" Title="Semester Deletion Requests" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<asp:Literal ID="litInitVars" runat="server" />
<style>
*{box-sizing:border-box}
html,body{background:#f5f7fa}
.sdr-wrap{width:100%;max-width:100%;margin:0;padding:12px;padding-bottom:64px}
.sdr-card{background:#fff;border:1px solid #d0dce9;border-radius:3px;overflow:hidden;margin-bottom:12px;box-shadow:0 1px 2px rgba(0,0,0,.06)}
.sdr-head{padding:10px 12px;border-bottom:1px solid #e8ecf3;display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap;background:linear-gradient(180deg,#fafbfc 0%,#f5f7fa 100%)}
.sdr-head--primary{background:linear-gradient(180deg,#05275C 0%,#0a3a7a 100%);border-bottom-color:rgba(0,0,0,.1)}
.sdr-head--primary .sdr-title,.sdr-head--primary .sdr-sub{color:#fff}
.sdr-title{font-size:13px;font-weight:900;letter-spacing:.5px;text-transform:uppercase;color:#03234e;margin:0}
.sdr-sub{font-size:10px;color:#7a8795;margin:0}
.sdr-filters{padding:10px 12px;display:grid;grid-template-columns:1.4fr 1fr 1.4fr 0.7fr 0.7fr;gap:8px;align-items:end;background:#f8f9fb;border-bottom:1px solid #eef1f7}
.sdr-fg{display:flex;flex-direction:column;gap:4px;min-width:0}
.sdr-fg label{font-size:9px;text-transform:uppercase;letter-spacing:.45px;color:#5a6370;font-weight:900}
.sdr-input,.sdr-select{height:32px;border:1px solid #d0dce9;border-radius:2px;background:#fff;padding:5px 8px;font-size:11px;color:#1f2937;font-family:inherit;width:100%;transition:all .15s}
.sdr-input:hover,.sdr-select:hover{border-color:#bccae3}
.sdr-input:focus,.sdr-select:focus{outline:none;border-color:#05275C;box-shadow:0 0 0 3px rgba(5,39,92,.12);background:#f9fbff}
.sdr-actions{display:flex;gap:6px;flex-wrap:wrap}
.sdr-btn{display:inline-flex;align-items:center;justify-content:center;gap:5px;height:32px;padding:0 12px;border-radius:2px;border:1px solid #d0dce9;background:#fff;color:#05275C;font-size:10px;font-weight:900;cursor:pointer;transition:all .15s;text-transform:uppercase;letter-spacing:.35px;white-space:nowrap}
.sdr-btn:hover{color:#174DA4;border-color:#bccae3;background:#f9fbff}
.sdr-btn:active{transform:scale(.99)}
.sdr-btn:disabled{opacity:.6;cursor:not-allowed;pointer-events:none}
.sdr-btn--primary{background:#05275C;color:#fff;border-color:#05275C}
.sdr-btn--primary:hover{background:#174DA4;border-color:#174DA4;box-shadow:0 2px 8px rgba(23,77,164,.2);color:#fff}
.sdr-btn--secondary{background:#f0f4f9;color:#05275C;border-color:#d0dce9}
.sdr-btn--secondary:hover{background:#e3e8f3;border-color:#bccae3}
.sdr-btn--success{background:#16a34a;color:#fff;border-color:#16a34a}
.sdr-btn--success:hover{background:#15803d;border-color:#15803d;box-shadow:0 2px 8px rgba(22,163,74,.2);color:#fff}
.sdr-btn--danger{background:#b42318;color:#fff;border-color:#b42318}
.sdr-btn--danger:hover{background:#8f1d16;border-color:#8f1d16;box-shadow:0 2px 8px rgba(180,35,24,.2);color:#fff}
.sdr-btn--ghost{background:transparent;color:rgba(255,255,255,.85);border-color:rgba(255,255,255,.3)}
.sdr-btn--ghost:hover{background:rgba(255,255,255,.12);color:#fff;border-color:rgba(255,255,255,.5)}
.sdr-btn--loading::after{content:'';display:inline-block;width:12px;height:12px;border:2px solid rgba(255,255,255,.3);border-top-color:#fff;border-radius:50%;animation:sdr-spin 0.8s linear infinite;margin-left:4px}
.sdr-btn--sm{height:26px;padding:0 8px;font-size:9px}
.sdr-stats{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;margin-bottom:12px}
.sdr-stat{background:#fff;border:1px solid #d0dce9;border-radius:3px;padding:12px 14px;display:flex;flex-direction:column;gap:5px;box-shadow:0 1px 2px rgba(0,0,0,.04);cursor:pointer;transition:all .15s}
.sdr-stat:hover{border-color:#bccae3;box-shadow:0 2px 6px rgba(0,0,0,.08)}
.sdr-stat--pending{border-left:3px solid #f59e0b}
.sdr-stat--approved{border-left:3px solid #16a34a}
.sdr-stat--rejected{border-left:3px solid #b42318}
.sdr-stat--total{border-left:3px solid #174DA4}
.sdr-stat__lbl{font-size:8px;text-transform:uppercase;letter-spacing:.45px;color:#6b7280;font-weight:900}
.sdr-stat__val{font-size:26px;color:#03234e;line-height:1;font-weight:900}
.sdr-stat__sub{font-size:9px;color:#7a8795}
.sdr-table-wrap{overflow:auto;background:#fff}
.sdr-table{width:100%;border-collapse:collapse;font-size:10px}
.sdr-table th{background:linear-gradient(180deg,#f8f9fb 0%,#f0f3f9 100%);border-bottom:2px solid #d0dce9;font-size:8px;text-transform:uppercase;letter-spacing:.45px;color:#5a6370;font-weight:900;padding:8px;text-align:left;white-space:nowrap}
.sdr-table td{border-bottom:1px solid #f0f3f9;color:#1f2937;padding:8px;vertical-align:middle;word-break:break-word}
.sdr-table tbody tr:hover td{background:#f9fbff}
.sdr-table tbody tr.sdr-row--selected td{background:#eff6ff}
.sdr-table tbody tr:last-child td{border-bottom-color:#d0dce9}
.sdr-chk-th{width:32px;text-align:center!important}
.sdr-chk-td{text-align:center;width:32px}
.sdr-chk{cursor:pointer;accent-color:#05275C;width:14px;height:14px}
.sdr-pill{display:inline-block;padding:2px 7px;border-radius:2px;font-size:8px;font-weight:900;letter-spacing:.35px;text-transform:uppercase;border:1px solid transparent}
.sdr-pill--PENDING{background:#fef3c7;color:#92400e;border-color:#fde68a}
.sdr-pill--APPROVED{background:#dcfce7;color:#166534;border-color:#a3cfbb}
.sdr-pill--REJECTED{background:#fee2e2;color:#b42318;border-color:#f5c2c7}
.sdr-note{font-size:9px;color:#7a8795;line-height:1.5}
.sdr-right{text-align:right}
.sdr-action-wrap{display:flex;gap:4px;justify-content:flex-end}
.sdr-row-student__code{font-weight:900;color:#05275C;font-size:10px;display:block}
.sdr-row-student__name{font-size:9px;color:#7a8795;display:block}
.sdr-row-student__prog{font-size:8px;color:#9ca3af;display:block}
.sdr-sep{color:#d0dce9}
.sdr-range{font-size:9px;color:#7a8795}
/* Pagination */
.sdr-pag-wrap{padding:10px 12px;border-top:1px solid #eef1f7;display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;background:#f8f9fb}
.sdr-pag{display:flex;align-items:center;gap:4px;flex-wrap:wrap}
.sdr-pag__btn{height:28px;min-width:28px;padding:0 8px;border:1px solid #d0dce9;background:#fff;color:#05275C;font-size:10px;font-weight:700;cursor:pointer;border-radius:2px;transition:all .12s;font-family:inherit}
.sdr-pag__btn:hover:not(:disabled){background:#f0f4f9;border-color:#bccae3}
.sdr-pag__btn:disabled{opacity:.45;cursor:not-allowed}
.sdr-pag__btn--active{background:#05275C;color:#fff;border-color:#05275C;cursor:default}
.sdr-pag__btn--active:hover{background:#05275C}
.sdr-pag__ellipsis{padding:0 4px;color:#9ca3af;font-size:11px;line-height:28px}
.sdr-pag__info{font-size:9px;color:#7a8795;white-space:nowrap;font-weight:600}
/* Alert */
.sdr-alert{display:none;margin-bottom:12px;padding:9px 12px;border:1px solid #fed7aa;background:#fffbeb;color:#92400e;border-radius:2px;font-size:11px;font-weight:600}
.sdr-alert.show{display:block;animation:sdr-slideDown .25s ease-out}
.sdr-alert--success{background:#dcfce7;border-color:#a3cfbb;color:#166534}
.sdr-alert--danger{background:#fee2e2;border-color:#f5c2c7;color:#b42318}
/* Batch Bar */
.sdr-batch{position:fixed;bottom:0;left:0;right:0;background:#05275C;color:#fff;padding:10px 16px;display:none;align-items:center;gap:12px;flex-wrap:wrap;z-index:8000;box-shadow:0 -3px 12px rgba(0,0,0,.25)}
.sdr-batch.show{display:flex;animation:sdr-slideUp .2s ease-out}
.sdr-batch__lbl{font-size:11px;font-weight:900;flex:1;min-width:80px}
.sdr-batch__lbl span{background:rgba(255,255,255,.15);border-radius:2px;padding:2px 6px;margin-right:4px}
/* Modals */
.sdr-modal{position:fixed;inset:0;background:rgba(5,39,92,.45);display:none;align-items:center;justify-content:center;padding:12px;z-index:9999}
.sdr-modal.show{display:flex;animation:sdr-fadeIn .18s ease-out}
.sdr-modal__panel{width:100%;max-width:560px;background:#fff;border:1px solid #d0dce9;border-radius:3px;box-shadow:0 8px 32px rgba(0,0,0,.18);overflow:hidden;animation:sdr-slideUp .25s ease-out}
.sdr-modal__panel--sm{max-width:440px}
.sdr-modal__head{padding:12px 14px;border-bottom:1px solid #e8ecf3;display:flex;justify-content:space-between;align-items:center;gap:12px;background:linear-gradient(180deg,#fafbfc 0%,#f5f7fa 100%)}
.sdr-modal__title{font-size:12px;font-weight:900;letter-spacing:.45px;text-transform:uppercase;color:#03234e}
.sdr-modal__body{padding:14px;display:flex;flex-direction:column;gap:12px;max-height:calc(90vh - 140px);overflow-y:auto}
.sdr-modal__foot{padding:12px 14px;border-top:1px solid #e8ecf3;display:flex;justify-content:flex-end;gap:6px;flex-wrap:wrap;background:#f9fbff}
/* What approving will actually destroy. This panel is the point of the screen now:
   the reason a student gives is one line, but the consequence is a registration, a
   course list and a fee that somebody has to answer for later. */
.sdr-imp{border:1px solid #d0dce9;border-left:3px solid #03234e;background:#f7f9fc;padding:9px 11px}
.sdr-imp__h{font-size:9px;font-weight:900;letter-spacing:.45px;text-transform:uppercase;color:#03234e;margin-bottom:7px}
.sdr-imp__g{display:grid;grid-template-columns:repeat(4,1fr);gap:7px}
.sdr-imp__c{background:#fff;border:1px solid #e2e9f2;padding:6px 8px;min-width:0}
.sdr-imp__n{font-size:15px;font-weight:900;color:#03234e;line-height:1.15;word-break:break-word}
.sdr-imp__l{font-size:8px;font-weight:700;letter-spacing:.35px;text-transform:uppercase;color:#7a8795;margin-top:2px}
.sdr-imp__c--money .sdr-imp__n{color:#8a5a00}
.sdr-imp__c--warn{border-color:#f0c7c3;background:#fdf3f2}
.sdr-imp__c--warn .sdr-imp__n{color:#b42318}
.sdr-imp__note{margin-top:8px;font-size:10px;line-height:1.5;padding:7px 9px}
.sdr-imp__note--stop{background:#fdecec;border:1px solid #f4c2c2;color:#8f1c16}
.sdr-imp__note--hold{background:#fff6e5;border:1px solid #f0d9a8;color:#7a4f00}
.sdr-imp__note--ok{background:#eef7f0;border:1px solid #cbe5d2;color:#1e6b33}
@media(max-width:700px){.sdr-imp__g{grid-template-columns:repeat(2,1fr)}}
/* Step two: the plan. Approving used to be one click on a screen that showed a
   sentence of the student's prose; it now costs a second click on a screen that
   says, line by line, what the first one will do. */
.sdr-steps{display:flex;align-items:center;gap:6px;margin-bottom:11px}
.sdr-steps__i{display:flex;align-items:center;gap:5px;font-size:8px;font-weight:800;letter-spacing:.4px;text-transform:uppercase;color:#9aa6b4}
.sdr-steps__i.is-on{color:#03234e}
.sdr-steps__n{width:16px;height:16px;border-radius:50%;background:#e2e9f2;color:#7a8795;display:flex;align-items:center;justify-content:center;font-size:8px;font-weight:900}
.sdr-steps__i.is-on .sdr-steps__n{background:#03234e;color:#fff}
.sdr-steps__b{flex:1;height:1px;background:#e2e9f2}
.sdr-plan{display:flex;flex-direction:column;gap:0;border:1px solid #d0dce9;background:#fff}
.sdr-plan__r{display:flex;gap:9px;padding:9px 11px;align-items:flex-start}
.sdr-plan__r+.sdr-plan__r{border-top:1px solid #edf1f6}
.sdr-plan__i{flex:0 0 auto;width:17px;height:17px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:9px;font-weight:900;color:#fff;margin-top:1px}
.sdr-plan__r--go .sdr-plan__i{background:#b42318}
.sdr-plan__r--keep .sdr-plan__i{background:#7a8795}
.sdr-plan__r--info .sdr-plan__i{background:#03234e}
.sdr-plan__t{flex:1;min-width:0;font-size:10.5px;line-height:1.5;color:#23324a}
.sdr-plan__t b{color:#03234e}
.sdr-plan__r--go .sdr-plan__t b{color:#b42318}
.sdr-bal{display:flex;align-items:center;gap:10px;margin-top:9px;padding:8px 11px;background:#f7f9fc;border:1px solid #d0dce9}
.sdr-bal__x{font-size:8px;font-weight:800;letter-spacing:.4px;text-transform:uppercase;color:#7a8795}
.sdr-bal__v{font-size:13px;font-weight:900;color:#03234e}
.sdr-bal__v--cr{color:#1e6b33}
.sdr-bal__a{color:#7a8795;font-size:12px}
.sdr-dl{display:grid;grid-template-columns:1fr 1fr;gap:10px;padding:10px 12px;background:#f8f9fb;border-radius:2px;border:1px solid #eef1f7}
.sdr-dl__k{font-size:8px;text-transform:uppercase;letter-spacing:.4px;color:#6b7280;font-weight:900}
.sdr-dl__v{font-size:11px;color:#1f2937;font-weight:600;margin-top:2px}
.sdr-textarea{width:100%;min-height:90px;resize:vertical;border:1px solid #d0dce9;border-radius:2px;padding:8px;font-size:11px;color:#1f2937;font-family:inherit;transition:all .15s}
.sdr-textarea:focus{outline:none;border-color:#05275C;box-shadow:0 0 0 3px rgba(5,39,92,.12);background:#f9fbff}
.sdr-textarea::placeholder{color:#a3a9b5}
.sdr-fg-inline{display:flex;flex-direction:column;gap:4px}
.sdr-fg-inline label{font-size:9px;text-transform:uppercase;letter-spacing:.45px;color:#5a6370;font-weight:900}
.sdr-decision-badge{display:inline-block;padding:4px 10px;border-radius:2px;font-size:10px;font-weight:900;text-transform:uppercase;letter-spacing:.4px}
.sdr-decision-badge--approve{background:#dcfce7;color:#166534;border:1px solid #a3cfbb}
.sdr-decision-badge--reject{background:#fee2e2;color:#b42318;border:1px solid #f5c2c7}
.sdr-empty{padding:32px 16px;text-align:center;color:#7a8795;font-size:11px}
.sdr-empty svg{width:36px;height:36px;margin:0 auto 10px;display:block;opacity:.35}
.sdr-empty strong{display:block;color:#5a6370;margin-bottom:4px}
@media(max-width:1200px){.sdr-stats{grid-template-columns:repeat(2,1fr)}.sdr-filters{grid-template-columns:repeat(3,1fr)}}
@media(max-width:700px){.sdr-stats,.sdr-dl{grid-template-columns:1fr}.sdr-filters{grid-template-columns:1fr}.sdr-modal__panel{max-width:95vw}.sdr-table{font-size:9px}.sdr-table td,.sdr-table th{padding:6px 4px}}
@keyframes sdr-fadeIn{from{opacity:0}to{opacity:1}}
@keyframes sdr-slideUp{from{transform:translateY(16px);opacity:0}to{transform:translateY(0);opacity:1}}
@keyframes sdr-slideDown{from{transform:translateY(-8px);opacity:0}to{transform:translateY(0);opacity:1}}
@keyframes sdr-spin{to{transform:rotate(360deg)}}
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="sdr-wrap" id="sdrWrap">

    <div id="sdrAlert" class="sdr-alert"></div>

    <!-- Header + Filters -->
    <div class="sdr-card">
        <div class="sdr-head sdr-head--primary">
            <div>
                <div class="sdr-title">Semester Deletion Requests</div>
                <div class="sdr-sub">Review and decide student semester registration deletion requests</div>
            </div>
            <div class="sdr-actions">
                <button type="button" class="sdr-btn sdr-btn--ghost" id="btnRefresh">&#8635; Refresh</button>
                <button type="button" class="sdr-btn sdr-btn--primary" id="btnApply" style="background:#174DA4;border-color:#174DA4">Apply Filters</button>
            </div>
        </div>
        <div class="sdr-filters">
            <div class="sdr-fg">
                <label>Status</label>
                <select id="fltStatus" class="sdr-select">
                    <option value="PENDING">Pending Only</option>
                    <option value="ALL">All Requests</option>
                    <option value="APPROVED">Approved</option>
                    <option value="REJECTED">Rejected</option>
                </select>
            </div>
            <div class="sdr-fg">
                <label>Academic Year</label>
                <input type="text" id="fltYear" class="sdr-input" placeholder="e.g. 2025/2026" />
            </div>
            <div class="sdr-fg">
                <label>Search</label>
                <input type="text" id="fltSearch" class="sdr-input" placeholder="Reg No, Name, Programme..." />
            </div>
            <div class="sdr-fg">
                <label>Per Page</label>
                <select id="fltPer" class="sdr-select">
                    <option value="10">10</option>
                    <option value="25" selected="selected">25</option>
                    <option value="50">50</option>
                    <option value="100">100</option>
                </select>
            </div>
            <div class="sdr-fg" style="justify-content:flex-end">
                <label>&nbsp;</label>
                <button type="button" class="sdr-btn sdr-btn--secondary" id="btnClearFilters" style="width:100%">Clear</button>
            </div>
        </div>
    </div>

    <!-- Stats -->
    <div class="sdr-stats">
        <div class="sdr-stat sdr-stat--pending" onclick="window.__sdrFilterStatus('PENDING')" title="Show pending">
            <div class="sdr-stat__lbl">Pending</div>
            <div class="sdr-stat__val" id="kPending">—</div>
            <div class="sdr-stat__sub">Awaiting decision</div>
        </div>
        <div class="sdr-stat sdr-stat--approved" onclick="window.__sdrFilterStatus('APPROVED')" title="Show approved">
            <div class="sdr-stat__lbl">Approved</div>
            <div class="sdr-stat__val" id="kApproved">—</div>
            <div class="sdr-stat__sub">Deletion executed</div>
        </div>
        <div class="sdr-stat sdr-stat--rejected" onclick="window.__sdrFilterStatus('REJECTED')" title="Show rejected">
            <div class="sdr-stat__lbl">Rejected</div>
            <div class="sdr-stat__val" id="kRejected">—</div>
            <div class="sdr-stat__sub">Request denied</div>
        </div>
        <div class="sdr-stat sdr-stat--total" onclick="window.__sdrFilterStatus('ALL')" title="Show all">
            <div class="sdr-stat__lbl">Total</div>
            <div class="sdr-stat__val" id="kTotal">—</div>
            <div class="sdr-stat__sub">Current scope</div>
        </div>
    </div>

    <!-- Table Card -->
    <div class="sdr-card">
        <div class="sdr-head">
            <div>
                <div class="sdr-title" id="tblTitle">Requests</div>
                <div class="sdr-sub sdr-range" id="tblRange">Loading...</div>
            </div>
        </div>
        <div class="sdr-table-wrap">
            <table class="sdr-table">
                <thead>
                    <tr>
                        <th class="sdr-chk-th"><input type="checkbox" id="sdrChkAll" class="sdr-chk" title="Select all on this page" /></th>
                        <th style="width:16%">Student</th>
                        <th style="width:20%">Programme</th>
                        <th style="width:11%">Period</th>
                        <th style="width:8%">Status</th>
                        <th style="width:27%">Reason</th>
                        <th style="width:11%">Decided By</th>
                        <th style="width:7%;text-align:right">Action</th>
                    </tr>
                </thead>
                <tbody id="sdrRows">
                    <tr><td colspan="8" class="sdr-empty">
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>
                        Loading requests...
                    </td></tr>
                </tbody>
            </table>
        </div>
        <div class="sdr-pag-wrap" id="sdrPagWrap" style="display:none">
            <div id="sdrPagination"></div>
            <div class="sdr-pag__info" id="sdrPagInfo"></div>
        </div>
    </div>
</div>

<!-- Batch Bar -->
<div class="sdr-batch" id="sdrBatchBar">
    <div class="sdr-batch__lbl"><span id="sdrBatchCount">0</span> selected</div>
    <button type="button" class="sdr-btn sdr-btn--ghost sdr-btn--sm" id="btnBatchClear">Clear</button>
    <button type="button" class="sdr-btn sdr-btn--danger sdr-btn--sm" id="btnBatchReject">Reject Selected</button>
    <button type="button" class="sdr-btn sdr-btn--success sdr-btn--sm" id="btnBatchApprove">Approve Selected</button>
</div>

<!-- Single Decision Modal -->
<div class="sdr-modal" id="decisionModal">
    <div class="sdr-modal__panel" role="dialog" aria-modal="true" aria-labelledby="decisionTitle">
        <div class="sdr-modal__head">
            <div class="sdr-modal__title" id="decisionTitle">Review Request</div>
            <button type="button" class="sdr-btn sdr-btn--secondary sdr-btn--sm" id="btnCloseModal">&#10005;</button>
        </div>
        <div class="sdr-modal__body">
            <div class="sdr-steps" id="decisionSteps" style="display:none">
                <div class="sdr-steps__i is-on" id="stepPip1"><span class="sdr-steps__n">1</span><span>Review</span></div>
                <div class="sdr-steps__b"></div>
                <div class="sdr-steps__i" id="stepPip2"><span class="sdr-steps__n">2</span><span>Confirm</span></div>
            </div>

            <div id="decisionStep1">
                <div class="sdr-dl" id="decisionDetails"></div>
                <div id="decisionImpact"></div>
                <div class="sdr-fg-inline" id="decisionCommentWrap">
                    <label>Decision Comment <span style="color:#b42318">*</span> <span id="decisionCommentHint" style="font-weight:400;font-size:8px;color:#7a8795;text-transform:none">(required for rejection)</span></label>
                    <textarea id="decisionComment" class="sdr-textarea" placeholder="Explain your decision to the student..."></textarea>
                </div>
            </div>

            <div id="decisionStep2" style="display:none"></div>
        </div>
        <div class="sdr-modal__foot">
            <button type="button" class="sdr-btn sdr-btn--secondary" id="btnCancelDecision">Cancel</button>
            <button type="button" class="sdr-btn sdr-btn--danger" id="btnReject">Reject Request</button>
            <button type="button" class="sdr-btn sdr-btn--success" id="btnApprove">Approve Deletion</button>
            <button type="button" class="sdr-btn sdr-btn--secondary" id="btnPlanBack" style="display:none">&#8592; Back</button>
            <button type="button" class="sdr-btn sdr-btn--danger" id="btnPlanGo" style="display:none">Yes, carry this out</button>
        </div>
    </div>
</div>

<!-- Batch Decision Modal -->
<div class="sdr-modal" id="batchModal">
    <div class="sdr-modal__panel sdr-modal__panel--sm" role="dialog" aria-modal="true" aria-labelledby="batchTitle">
        <div class="sdr-modal__head">
            <div class="sdr-modal__title" id="batchTitle">Batch Decision</div>
            <button type="button" class="sdr-btn sdr-btn--secondary sdr-btn--sm" id="btnCloseBatch">&#10005;</button>
        </div>
        <div class="sdr-modal__body">
            <div style="display:flex;align-items:center;gap:8px">
                <span style="font-size:11px;color:#5a6370">Applying decision:</span>
                <span id="batchDecisionBadge" class="sdr-decision-badge"></span>
                <span style="font-size:11px;color:#5a6370">to <strong id="batchCountLabel">0</strong> request(s)</span>
            </div>
            <div class="sdr-fg-inline">
                <label>Comment <span id="batchCommentRequired" style="color:#b42318">*</span> <span id="batchCommentHint" style="font-weight:400;font-size:8px;color:#7a8795;text-transform:none"></span></label>
                <textarea id="batchComment" class="sdr-textarea" placeholder="Explain your decision to the students..."></textarea>
            </div>
        </div>
        <div class="sdr-modal__foot">
            <button type="button" class="sdr-btn sdr-btn--secondary" id="btnCancelBatch">Cancel</button>
            <button type="button" class="sdr-btn" id="btnConfirmBatch">Confirm</button>
        </div>
    </div>
</div>

<script type="text/javascript">
(function(){
'use strict';
function byId(id){return document.getElementById(id);}
function esc(s){return String(s||'').replace(/[&<>"']/g,function(c){return({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c];});}
function n(v){var x=parseInt(v,10);return isNaN(x)?0:x;}

var state={page:1,per:25,status:'PENDING',year:'',q:'',records:[],total:0,total_pages:1,pending:0,approved:0,rejected:0,selectedIds:[],currentId:0};

/* ── Init from server ── */
if(typeof _sdrInit!=='undefined'){
  state.status=_sdrInit.status||'PENDING';
  state.year=_sdrInit.year||'';
  state.q=_sdrInit.q||'';
  state.page=_sdrInit.page||1;
  state.per=_sdrInit.per||25;
}

function syncUiFromState(){
  byId('fltStatus').value=state.status;
  byId('fltYear').value=state.year;
  byId('fltSearch').value=state.q;
  byId('fltPer').value=String(state.per);
}
syncUiFromState();

/* ── Alerts ── */
function showAlert(msg,type){
  var el=byId('sdrAlert');
  el.textContent=msg||'Unexpected error.';
  el.className='sdr-alert show'+(type?' sdr-alert--'+type:'');
  clearTimeout(el._t);
  el._t=setTimeout(function(){el.classList.remove('show');},6000);
}
function hideAlert(){byId('sdrAlert').className='sdr-alert';}

/* ── AJAX ── */
function ajax(method,payload,done){
  var xhr=new XMLHttpRequest();
  xhr.open('POST','SemesterDeletionRequestsController.aspx/'+method,true);
  xhr.setRequestHeader('Content-Type','application/json; charset=utf-8');
  xhr.onload=function(){
    try{
      var raw=JSON.parse(xhr.responseText);
      var data=(raw&&raw.d!==undefined)?raw.d:raw;
      if(typeof data==='string')data=JSON.parse(data);
      done(data||{success:false,message:'Empty response'});
    }catch(ex){done({success:false,message:'Parse error: '+ex.message});}
  };
  xhr.onerror=function(){done({success:false,message:'Network error.'});};
  xhr.send(JSON.stringify(payload||{}));
}

/* ── URL Management ── */
function buildUrl(){
  var p=[];
  if(state.status&&state.status!=='PENDING')p.push('status='+encodeURIComponent(state.status));
  if(state.year)p.push('year='+encodeURIComponent(state.year));
  if(state.q)p.push('q='+encodeURIComponent(state.q));
  if(state.page>1)p.push('page='+state.page);
  if(state.per!==25)p.push('per='+state.per);
  return location.pathname+(p.length?'?'+p.join('&'):'');
}
function parseQS(){
  var q=location.search.replace(/^\?/,'');
  if(!q)return{};
  var pairs=q.split('&'),m={};
  for(var i=0;i<pairs.length;i++){var kv=pairs[i].split('=');if(kv[0])m[decodeURIComponent(kv[0])]=decodeURIComponent(kv[1]||'');}
  return m;
}

/* ── Load ── */
function load(pushUrl){
  state.status=byId('fltStatus').value;
  state.year=(byId('fltYear').value||'').trim();
  state.q=(byId('fltSearch').value||'').trim();
  state.per=n(byId('fltPer').value)||25;
  if(pushUrl!==false)history.pushState(null,'',buildUrl());
  hideAlert();
  byId('tblRange').textContent='Loading...';

  ajax('GetRequests',{statusFilter:state.status,acadYear:state.year,search:state.q,page:state.page,pageSize:state.per},function(res){
    if(!res||!res.success){showAlert((res&&res.message)||'Failed to load requests.','danger');byId('tblRange').textContent='Error loading data.';return;}
    state.records=res.requests||[];
    state.total=res.total||0;
    state.total_pages=res.total_pages||1;
    state.pending=res.pending||0;
    state.approved=res.approved||0;
    state.rejected=res.rejected||0;
    state.selectedIds=[];
    render();
    renderPagination();
    updateStats();
    updateBatchBar();
  });
}

function applyFilters(){state.page=1;load(true);}

/* ── Stats ── */
function updateStats(){
  byId('kPending').textContent=String(state.pending);
  byId('kApproved').textContent=String(state.approved);
  byId('kRejected').textContent=String(state.rejected);
  byId('kTotal').textContent=String(state.total);
}

/* ── Render rows ── */
function render(){
  var rows=state.records,html='';
  var start=(state.page-1)*state.per+1;
  var end=Math.min(start+rows.length-1,state.total);
  if(state.total===0){
    byId('tblRange').textContent='No results';
    byId('tblTitle').textContent='Requests';
  }else{
    byId('tblRange').textContent='Showing '+start+'–'+end+' of '+state.total+' record'+(state.total!==1?'s':'');
    byId('tblTitle').textContent='Requests';
  }

  if(!rows.length){
    html='<tr><td colspan="8" class="sdr-empty"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M9 12h6m-6 4h6M9 8h6M17 3H7a4 4 0 0 0-4 4v10a4 4 0 0 0 4 4h10a4 4 0 0 0 4-4V7a4 4 0 0 0-4-4z"/></svg><strong>No requests found</strong>No requests match your current filters.</td></tr>';
  }else{
    for(var j=0;j<rows.length;j++){
      var x=rows[j];
      var st=String(x.status||'').toUpperCase();
      var isPending=st==='PENDING';
      var chkCell='<td class="sdr-chk-td"><input type="checkbox" class="sdr-chk sdr-chk-row" value="'+n(x.id)+'" onchange="window.__sdrCheck()" /></td>';
      var decBy=isPending
        ?'<span class="sdr-note">—</span>'
        :'<span class="sdr-note">'+esc(x.admin_username||'—')+'<br>'+esc(x.decided_at||'—')+'</span>';
      var actionBtn='<button type="button" class="sdr-btn sdr-btn--primary sdr-btn--sm" onclick="window.__sdrOpen('+n(x.id)+')">Review</button>';
      var reason=esc(x.request_reason||'');
      if((x.request_reason||'').length>80)reason=esc((x.request_reason||'').substring(0,80))+'<span style="color:#9ca3af">…</span>';
      html+='<tr data-id="'+n(x.id)+'">'+
        chkCell+
        '<td><span class="sdr-row-student__code">'+esc(x.regno||'')+'</span><span class="sdr-row-student__name">'+esc(x.student_name||'—')+'</span></td>'+
        '<td><span class="sdr-note">'+esc((x.programme_name||'—').substring(0,40))+'</span></td>'+
        '<td class="sdr-note">'+esc(x.acad_year||'—')+'<br>Y'+n(x.study_year)+' &nbsp;S'+n(x.semester)+'</td>'+
        '<td><span class="sdr-pill sdr-pill--'+esc(st)+'">'+esc(st)+'</span></td>'+
        '<td class="sdr-note">'+reason+'</td>'+
        '<td>'+decBy+'</td>'+
        '<td class="sdr-right"><div class="sdr-action-wrap">'+actionBtn+'</div></td>'+
        '</tr>';
    }
  }
  byId('sdrRows').innerHTML=html;
  byId('sdrChkAll').checked=false;
  byId('sdrChkAll').indeterminate=false;
}

/* ── Pagination ── */
function renderPagination(){
  var wrap=byId('sdrPagWrap');
  var p=state.page,t=state.total_pages;
  if(t<=1){wrap.style.display='none';return;}
  wrap.style.display='flex';

  var pages=[];
  if(t<=7){for(var i=1;i<=t;i++)pages.push(i);}
  else{
    pages.push(1);
    if(p>3)pages.push('...');
    for(var i=Math.max(2,p-1);i<=Math.min(t-1,p+1);i++)pages.push(i);
    if(p<t-2)pages.push('...');
    pages.push(t);
  }

  var html='<div class="sdr-pag"><button class="sdr-pag__btn" '+(p<=1?'disabled':'')+' onclick="window.__sdrPage('+(p-1)+')">&#8592; Prev</button>';
  for(var i=0;i<pages.length;i++){
    var pg=pages[i];
    if(pg==='...'){html+='<span class="sdr-pag__ellipsis">&#8230;</span>';}
    else{html+='<button class="sdr-pag__btn'+(pg===p?' sdr-pag__btn--active':'')+'" '+(pg===p?'disabled':'')+' onclick="window.__sdrPage('+pg+')">'+pg+'</button>';}
  }
  html+='<button class="sdr-pag__btn" '+(p>=t?'disabled':'')+' onclick="window.__sdrPage('+(p+1)+')">Next &#8594;</button></div>';
  byId('sdrPagination').innerHTML=html;
  byId('sdrPagInfo').textContent='Page '+p+' of '+t;
}

window.__sdrPage=function(p){if(p<1||p>state.total_pages)return;state.page=p;load(true);};

/* ── Batch selection ── */
window.__sdrCheck=function(){
  var chks=document.querySelectorAll('.sdr-chk-row:checked');
  var all=document.querySelectorAll('.sdr-chk-row');
  state.selectedIds=[];
  for(var i=0;i<chks.length;i++)state.selectedIds.push(n(chks[i].value));
  var allChk=byId('sdrChkAll');
  allChk.checked=chks.length>0&&chks.length===all.length;
  allChk.indeterminate=chks.length>0&&chks.length<all.length;
  updateBatchBar();
};
window.__sdrChkAll=function(checked){
  var chks=document.querySelectorAll('.sdr-chk-row');
  for(var i=0;i<chks.length;i++)chks[i].checked=checked;
  window.__sdrCheck();
};
function updateBatchBar(){
  var bar=byId('sdrBatchBar');
  byId('sdrBatchCount').textContent=state.selectedIds.length;
  bar.className='sdr-batch'+(state.selectedIds.length>0?' show':'');
}

/* ── Single decision modal ── */
function showDecisionModal(on){byId('decisionModal').className='sdr-modal'+(on?' show':'');}
function fillDetails(item){
  byId('decisionDetails').innerHTML=
    '<div><div class="sdr-dl__k">Reg No.</div><div class="sdr-dl__v">'+esc(item.regno||'')+'</div></div>'+
    '<div><div class="sdr-dl__k">Student Name</div><div class="sdr-dl__v">'+esc(item.student_name||'—')+'</div></div>'+
    '<div><div class="sdr-dl__k">Programme</div><div class="sdr-dl__v">'+esc(item.programme_name||'—')+'</div></div>'+
    '<div><div class="sdr-dl__k">Academic Year</div><div class="sdr-dl__v">'+esc(item.acad_year||'—')+'</div></div>'+
    '<div><div class="sdr-dl__k">Study Period</div><div class="sdr-dl__v">Year '+n(item.study_year)+', Semester '+n(item.semester)+'</div></div>'+
    '<div><div class="sdr-dl__k">Status</div><div class="sdr-dl__v"><span class="sdr-pill sdr-pill--'+esc(String(item.status||'').toUpperCase())+'">'+esc(item.status||'')+'</span></div></div>'+
    '<div style="grid-column:1/-1"><div class="sdr-dl__k">Reason for Deletion</div><div class="sdr-dl__v">'+esc(item.request_reason||'—')+'</div></div>';
}
function money(v){
  var x=Number(v||0);
  return isNaN(x)?'0':x.toFixed(0).replace(/\B(?=(\d{3})+(?!\d))/g,',');
}

/* The consequence of the click, spelled out before it is made. Deleting a semester
   registration is not one row: the wizard that created it also raised a bill, so
   approving reverses a course list and a charge as well. */
function fillImpact(imp,readOnly){
  var el=byId('decisionImpact');
  if(!imp){el.innerHTML='';return;}
  var sharesPeriod=!!imp.shares_period, blocked=!!imp.blocked;

  var cells=
    '<div class="sdr-imp__c"><div class="sdr-imp__n">'+n(imp.courses)+'</div><div class="sdr-imp__l">Courses</div></div>'+
    '<div class="sdr-imp__c'+(n(imp.marked_courses)>0?' sdr-imp__c--warn':'')+'"><div class="sdr-imp__n">'+n(imp.marked_courses)+'</div><div class="sdr-imp__l">With marks</div></div>'+
    '<div class="sdr-imp__c'+(n(imp.results)>0?' sdr-imp__c--warn':'')+'"><div class="sdr-imp__n">'+n(imp.results)+'</div><div class="sdr-imp__l">Results</div></div>'+
    '<div class="sdr-imp__c sdr-imp__c--money"><div class="sdr-imp__n">'+money(imp.bill_amount)+'</div><div class="sdr-imp__l">Fees, UGX</div></div>';

  var note='';
  if(blocked){
    note='<div class="sdr-imp__note sdr-imp__note--stop"><b>Cannot be deleted.</b> This semester carries '
       +n(imp.results)+' published result(s). Withdraw the results through the marks pipeline first.</div>';
  } else if(sharesPeriod){
    note='<div class="sdr-imp__note sdr-imp__note--hold"><b>Only the registration will go.</b> The student holds '
       +n(imp.siblings)+' other registration(s) for this same semester at a different year of study, and the '
       +'course list and the fees belong to the semester rather than to one registration. They will be left alone.</div>';
  } else {
    note='<div class="sdr-imp__note sdr-imp__note--ok">Approving removes the registration, '+n(imp.courses)
       +' course row(s), and reverses '+n(imp.bills)+' fee bill(s) worth UGX '+money(imp.bill_amount)
       +' ('+n(imp.ledger_rows)+' ledger entr'+(n(imp.ledger_rows)===1?'y':'ies')+'). '
       +'Payments already made are <b>not</b> touched — they stay on the account as credit.</div>';
  }

  el.innerHTML='<div class="sdr-imp"><div class="sdr-imp__h">'
    +(readOnly?'What this removed':'What approving will remove')
    +'</div><div class="sdr-imp__g">'+cells+'</div>'+note+'</div>';
}

/* ── The approve wizard ──────────────────────────────────────────────────────
   Step one is the request and its consequence. Step two is the plan: every row a
   line, in the order it will happen, with the real figures. It exists because an
   approval on this screen now cancels money as well as a registration, and the one
   thing a person should never be able to say afterwards is "I didn't know it would
   do that". */
function planStep(on){
  byId('decisionStep1').style.display = on ? 'none' : '';
  byId('decisionStep2').style.display = on ? '' : 'none';
  byId('decisionSteps').style.display = '';
  byId('stepPip2').className = 'sdr-steps__i' + (on ? ' is-on' : '');
  byId('stepPip1').className = 'sdr-steps__i' + (on ? '' : ' is-on');
  byId('btnApprove').style.display = on ? 'none' : '';
  byId('btnReject').style.display  = on ? 'none' : '';
  byId('btnCancelDecision').style.display = on ? 'none' : '';
  byId('btnPlanBack').style.display = on ? '' : 'none';
  byId('btnPlanGo').style.display   = on ? '' : 'none';
  byId('decisionTitle').textContent = on ? 'Confirm — what will happen' : 'Review Request';
}

function planRow(kind,icon,html){
  return '<div class="sdr-plan__r sdr-plan__r--'+kind+'"><span class="sdr-plan__i">'+icon+'</span>'
       + '<span class="sdr-plan__t">'+html+'</span></div>';
}

function buildPlan(item,imp){
  if(!imp){return '<div class="sdr-imp__note sdr-imp__note--hold">The details of this request could not be read again. Close and reopen it.</div>';}
  var period='<b>'+esc(item.acad_year||'')+' &mdash; Year '+n(item.study_year)+', Semester '+n(item.semester)+'</b>';
  var rows='';

  rows+=planRow('go','1','Delete the semester registration for '+period+'.');

  if(imp.shares_period){
    rows+=planRow('keep','2','<b>Keep</b> the '+n(imp.courses)+' course registration(s). This student holds '
      +n(imp.siblings)+' other registration(s) for this same semester at a different year of study, and the '
      +'course list belongs to the semester rather than to one registration.');
    rows+=planRow('keep','3','<b>Keep</b> the fees of UGX '+money(imp.bill_amount)+' &mdash; for the same reason. '
      +'The semester is still registered, so it is still chargeable.');
  } else {
    rows+= n(imp.courses)>0
      ? planRow('go','2','Delete <b>'+n(imp.courses)+'</b> course registration(s) for that semester.')
      : planRow('info','2','No courses were ever selected, so there are none to delete.');
    rows+= n(imp.bills)>0
      ? planRow('go','3','Cancel <b>'+n(imp.bills)+'</b> fee bill(s) worth <b>UGX '+money(imp.bill_amount)+'</b>, '
          +'and the '+n(imp.ledger_rows)+' matching ledger entr'+(n(imp.ledger_rows)===1?'y':'ies')+'.')
      : planRow('info','3','No fees were raised for this semester, so there is nothing to cancel.');
  }

  rows+=planRow('info','4','<b>Payments are not touched.</b> Only charges are reversed &mdash; any money the '
    +'student has paid stays on their account.');
  rows+=planRow('info','5','Everything deleted is copied to the recovery tables first, so it can be put back.');
  rows+=planRow('info','6','The student is told on their portal, and this decision is recorded against your name.');

  var bal='';
  if(!imp.shares_period && n(imp.bills)>0){
    var after=Number(imp.balance_after||0);
    bal='<div class="sdr-bal"><span class="sdr-bal__x">Balance</span>'
      + '<span class="sdr-bal__v">UGX '+money(imp.balance_before)+'</span>'
      + '<span class="sdr-bal__a">&#8594;</span>'
      + '<span class="sdr-bal__v'+(after<0?' sdr-bal__v--cr':'')+'">UGX '+money(Math.abs(after))
      + (after<0?' in credit':'')+'</span></div>';
  }

  return '<div class="sdr-plan">'+rows+'</div>'+bal;
}

function openReview(id,readOnly){
  state.currentId=n(id);
  ajax('GetRequest',{id:state.currentId},function(res){
    if(!res||!res.success){showAlert((res&&res.message)||'Failed to load request.','danger');return;}
    var item=res.request||{};
    var imp=res.impact||null;
    state.currentItem=item;
    state.currentImpact=imp;
    planStep(false);
    byId('decisionSteps').style.display=readOnly?'none':'';
    fillDetails(item);
    fillImpact(imp,readOnly);
    byId('decisionComment').value=item.admin_comment||'';
    byId('decisionTitle').textContent=readOnly?'Request Details':'Review Request';
    var approveBtn=byId('btnApprove'),rejectBtn=byId('btnReject');
    var commentWrap=byId('decisionCommentWrap');
    if(readOnly){approveBtn.style.display='none';rejectBtn.style.display='none';commentWrap.style.display='none';}
    else{
      approveBtn.style.display='';rejectBtn.style.display='';commentWrap.style.display='';
      /* The server refuses this case anyway; taking the button away as well means
         nobody spends a click finding that out. */
      var stop=!!(imp&&imp.blocked);
      approveBtn.disabled=stop;
      byId('btnPlanGo').disabled=stop;
      approveBtn.title=stop?'This semester has published results and cannot be deleted here.':'';
      approveBtn.style.opacity=stop?'.45':'';
    }
    showDecisionModal(true);
  });
}
function decideSingle(decision){
  var id=state.currentId;
  if(!id){showAlert('No request selected.','danger');return;}
  var comment=(byId('decisionComment').value||'').trim();
  if(decision==='REJECT'&&!comment){showAlert('A comment is required when rejecting a request.','danger');return;}
  /* On approve the visible button is the plan's, not the one on step one. */
  var btn=decision==='APPROVE'?byId('btnPlanGo'):byId('btnReject');
  btn.disabled=true;btn.classList.add('sdr-btn--loading');
  ajax('DecideRequest',{id:id,decision:decision,comment:comment},function(res){
    btn.disabled=false;btn.classList.remove('sdr-btn--loading');
    if(!res||!res.success){showAlert((res&&res.message)||'Action failed.','danger');return;}
    showDecisionModal(false);
    showAlert('Request '+(decision==='APPROVE'?'approved':'rejected')+' successfully.','success');
    load(false);
  });
}

/* ── Batch decision modal ── */
var _batchDecision='';
function showBatchModal(on){byId('batchModal').className='sdr-modal'+(on?' show':'');}
function openBatch(decision){
  if(!state.selectedIds||!state.selectedIds.length){showAlert('No requests selected.','danger');return;}
  _batchDecision=decision;
  var badge=byId('batchDecisionBadge');
  if(decision==='APPROVE'){badge.className='sdr-decision-badge sdr-decision-badge--approve';badge.textContent='Approve';}
  else{badge.className='sdr-decision-badge sdr-decision-badge--reject';badge.textContent='Reject';}
  byId('batchCountLabel').textContent=state.selectedIds.length;
  byId('batchComment').value='';
  var hint=decision==='REJECT'?'Required for rejection':'Optional — shown to students';
  byId('batchCommentHint').textContent=hint;
  byId('batchCommentRequired').style.display=decision==='REJECT'?'':'none';
  var confirmBtn=byId('btnConfirmBatch');
  confirmBtn.className='sdr-btn '+(decision==='APPROVE'?'sdr-btn--success':'sdr-btn--danger');
  confirmBtn.textContent=decision==='APPROVE'?'Approve '+state.selectedIds.length+' Request(s)':'Reject '+state.selectedIds.length+' Request(s)';
  showBatchModal(true);
}
function confirmBatch(){
  var comment=(byId('batchComment').value||'').trim();
  if(_batchDecision==='REJECT'&&!comment){showAlert('A comment is required when rejecting requests.','danger');return;}
  var btn=byId('btnConfirmBatch');
  btn.disabled=true;btn.classList.add('sdr-btn--loading');
  ajax('BatchDecide',{idsJson:JSON.stringify(state.selectedIds),decision:_batchDecision,comment:comment},function(res){
    btn.disabled=false;btn.classList.remove('sdr-btn--loading');
    if(!res||!res.success){showAlert((res&&res.message)||'Batch action failed.','danger');return;}
    showBatchModal(false);
    var type=res.skipped>0?'':'success';
    showAlert(res.message||(res.processed+' processed.'),type);
    load(false);
  });
}

/* ── Public handles ── */
window.__sdrOpen=function(id){openReview(id,false);};
window.__sdrView=function(id){openReview(id,false);};
window.__sdrFilterStatus=function(s){state.status=s;state.page=1;syncUiFromState();load(true);};

/* ── Popstate (back/forward) ── */
window.addEventListener('popstate',function(){
  var qs=parseQS();
  var st=qs['status']||'PENDING';
  state.status=st;
  state.year=qs['year']||'';
  state.q=qs['q']||'';
  state.page=n(qs['page'])||1;
  state.per=n(qs['per'])||25;
  syncUiFromState();
  load(false);
});

/* ── Wire up controls ── */
byId('btnApply').onclick=applyFilters;
byId('btnRefresh').onclick=function(){load(false);};
byId('btnClearFilters').onclick=function(){
  byId('fltStatus').value='PENDING';
  byId('fltYear').value='';
  byId('fltSearch').value='';
  byId('fltPer').value='25';
  state.page=1;
  applyFilters();
};
byId('fltSearch').addEventListener('keydown',function(e){if(e.key==='Enter')applyFilters();});
byId('fltYear').addEventListener('keydown',function(e){if(e.key==='Enter')applyFilters();});
byId('sdrChkAll').onchange=function(){window.__sdrChkAll(this.checked);};
byId('btnBatchClear').onclick=function(){
  var chks=document.querySelectorAll('.sdr-chk-row');
  for(var i=0;i<chks.length;i++)chks[i].checked=false;
  byId('sdrChkAll').checked=false;
  byId('sdrChkAll').indeterminate=false;
  state.selectedIds=[];
  updateBatchBar();
};
byId('btnBatchApprove').onclick=function(){openBatch('APPROVE');};
byId('btnBatchReject').onclick=function(){openBatch('REJECT');};
byId('btnCloseModal').onclick=function(){showDecisionModal(false);};
byId('btnCancelDecision').onclick=function(){showDecisionModal(false);};
/* Approve no longer fires. It opens the plan, and the plan fires. Rejecting still
   goes straight through — turning a request down destroys nothing. */
byId('btnApprove').onclick=function(){
  byId('decisionStep2').innerHTML=buildPlan(state.currentItem||{},state.currentImpact);
  planStep(true);
};
byId('btnReject').onclick=function(){decideSingle('REJECT');};
byId('btnPlanBack').onclick=function(){planStep(false);};
byId('btnPlanGo').onclick=function(){decideSingle('APPROVE');};
byId('btnCloseBatch').onclick=function(){showBatchModal(false);};
byId('btnCancelBatch').onclick=function(){showBatchModal(false);};
byId('btnConfirmBatch').onclick=confirmBatch;

/* Backdrop clicks close modals */
byId('decisionModal').addEventListener('click',function(e){if(e.target===this)showDecisionModal(false);});
byId('batchModal').addEventListener('click',function(e){if(e.target===this)showBatchModal(false);});

document.addEventListener('keydown',function(e){
  if(e.key!=='Escape')return;
  if(byId('batchModal').className.indexOf('show')>-1){showBatchModal(false);return;}
  if(byId('decisionModal').className.indexOf('show')>-1){showDecisionModal(false);}
});

/* ── Initial load ── */
load(false);
})();
</script>
</asp:Content>
