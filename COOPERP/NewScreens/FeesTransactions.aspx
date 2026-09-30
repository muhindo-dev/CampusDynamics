<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="FeesTransactions.aspx.cs" Inherits="COOPERP_NewScreens_FeesTransactions" Title="Fee Transactions - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===== FEE TRANSACTIONS ================================================ */

/* Stats Row */
.ft-stats { display: grid; grid-template-columns: repeat(5, 1fr); gap: 10px; margin-bottom: 14px; }
.ft-stat { background: #fff; border: 1px solid #e0e5ed; padding: 12px 14px; display: flex; align-items: center; gap: 10px; position: relative; overflow: hidden; }
.ft-stat::after { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 3px; background: var(--stat-c, #ccc); }
.ft-stat__icon { width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
.ft-stat__val { font-size: 15px; font-weight: 700; line-height: 1.2; font-variant-numeric: tabular-nums; word-break: break-word; overflow-wrap: break-word; }
.ft-stat__label { font-size: 9px; text-transform: uppercase; letter-spacing: .5px; color: #888; margin-top: 2px; }
.ft-stat--bills   { --stat-c: #00897b; } .ft-stat--bills   .ft-stat__icon { background: #e0f2f1; } .ft-stat--bills .ft-stat__val { color: #00695c; }
.ft-stat--pays    { --stat-c: #2e7d32; } .ft-stat--pays    .ft-stat__icon { background: #e6f4ea; } .ft-stat--pays .ft-stat__val { color: #2e7d32; }
.ft-stat--total   { --stat-c: #174DA4; } .ft-stat--total   .ft-stat__icon { background: #e8f0fc; } .ft-stat--total .ft-stat__val { color: #174DA4; }
.ft-stat--bamt    { --stat-c: #e65100; } .ft-stat--bamt    .ft-stat__icon { background: #fff3e0; } .ft-stat--bamt .ft-stat__val { color: #e65100; }
.ft-stat--pamt    { --stat-c: #2e7d32; } .ft-stat--pamt    .ft-stat__icon { background: #e6f4ea; } .ft-stat--pamt .ft-stat__val { color: #2e7d32; }

/* Filters Card */
.ft-card { background: #fff; border: 1px solid #e0e5ed; overflow: hidden; margin-bottom: 14px; }
.ft-card__header { padding: 10px 14px; border-bottom: 1px solid #e0e5ed; background: #f8f9fb; display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 6px; }
.ft-card__title { font-size: 12px; font-weight: 700; color: #05275C; display: flex; align-items: center; gap: 6px; }
.ft-card__meta { font-size: 10px; color: #174DA4; font-weight: 600; background: rgba(23,77,164,.07); padding: 2px 8px; border: 1px solid rgba(23,77,164,.15); }

.ft-filters { background: #f8f9fb; border-bottom: 1px solid #e0e5ed; padding: 10px 14px; }
.ft-filters__top { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; flex-wrap: wrap; }
.ft-search-wrap { position: relative; flex: 1; min-width: 200px; max-width: 380px; }
.ft-search-wrap svg { position: absolute; left: 10px; top: 50%; transform: translateY(-50%); color: #999; pointer-events: none; }
.ft-search-box { width: 100%; padding: 7px 12px 7px 32px; border: 1px solid #e0e5ed; font-size: 12px; background: #fff; box-sizing: border-box; }
.ft-search-box:focus { border-color: #174DA4; outline: none; }
.ft-filters__row { display: flex; gap: 8px; flex-wrap: wrap; align-items: flex-end; }
.ft-filter-grp { display: flex; flex-direction: column; gap: 3px; }
.ft-filter-grp__label { font-size: 9px; text-transform: uppercase; letter-spacing: .5px; color: #999; font-weight: 600; }
.ft-filter-select { border: 1px solid #e0e5ed; padding: 6px 10px; font-size: 11px; background: #fff; color: #333; cursor: pointer; min-width: 110px; }
.ft-filter-select:focus { border-color: #174DA4; outline: none; }

/* Buttons */
.ft-btn { padding: 6px 14px; font-size: 11px; font-weight: 600; border: none; cursor: pointer; display: inline-flex; align-items: center; gap: 5px; white-space: nowrap; transition: all .15s; }
.ft-btn--primary { background: #05275C; color: #fff; } .ft-btn--primary:hover { background: #174DA4; }
.ft-btn--ghost { background: transparent; border: 1px solid #e0e5ed; color: #555; } .ft-btn--ghost:hover { border-color: #174DA4; color: #174DA4; }
.ft-btn--sm { padding: 5px 11px; font-size: 10px; }

/* Grid Footer */
.ft-grid-footer { display: flex; justify-content: space-between; align-items: center; padding: 8px 14px; background: #f8f9fb; border-top: 1px solid #e0e5ed; font-size: 11px; color: #666; flex-wrap: wrap; gap: 6px; }
.ft-grid-footer strong { color: #05275C; }

/* Badges */
.ft-badge { display: inline-block; padding: 3px 9px; font-size: 9px; font-weight: 700; text-transform: uppercase; letter-spacing: .3px; }
.ft-badge--bill { background: #fff3cd; color: #856404; }
.ft-badge--pay  { background: #d4edda; color: #155724; }
.ft-badge--posted { background: #e8f0fc; color: #174DA4; }
.ft-badge--pending { background: #f8d7da; color: #721c24; }

/* === Custom Data Table ================================================= */
/* Single natural page scroll (no inner fixed-height box, no sticky header). Wide table
   scrolls horizontally inside its wrap; the page scrolls vertically as one. */
.ft-table-wrap { overflow-x: auto; border-bottom: 1px solid #e0e5ed; position: relative; }
.ft-table { width: 100%; border-collapse: collapse; min-width: 960px; font-size: 12px; }
.ft-table thead th { background: #f5f7fa; color: #555; font-size: 10px; text-transform: uppercase; letter-spacing: .3px; font-weight: 600; padding: 5px 8px; border-bottom: 2px solid #e0e5ed; white-space: nowrap; box-shadow: 0 2px 0 #e0e5ed; }
.ft-table tbody tr { border-bottom: 1px solid #f0f2f5; transition: background .08s; cursor: pointer; }
.ft-table tbody tr:nth-child(even) { background: #f9fafb; }
.ft-table tbody tr:hover, .ft-table tbody tr:nth-child(even):hover { background: #eef2fc; }
.ft-table tbody td { padding: 4px 8px; vertical-align: middle; color: #1a1a2e; font-size: 11px; }
/* Column widths */
.ft-col-id    { width: 62px;  }
.ft-col-regno { width: 135px; white-space: nowrap; }
.ft-col-name  { width: 185px; }
.ft-col-type  { width: 82px;  white-space: nowrap; }
.ft-col-item  { width: 148px; }
.ft-col-amt   { width: 120px; white-space: nowrap; text-align: right; }
.ft-col-detail{ min-width: 260px; }
.ft-col-status{ width: 82px;  white-space: nowrap; }
.ft-col-date  { width: 92px;  white-space: nowrap; }
.ft-col-year  { width: 92px;  white-space: nowrap; }
.ft-col-sem   { width: 46px;  text-align: center; white-space: nowrap; }
.ft-col-by    { width: 120px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 120px; }
.ft-col-action{ width: 44px;  text-align: center; white-space: nowrap; }
td.ft-col-regno { color: #05275C; font-weight: 700; }
td.ft-col-amt   { font-weight: 700; font-variant-numeric: tabular-nums; }
td.ft-col-detail { white-space: normal; word-break: break-word; line-height: 1.45; }
/* Pager */
.ft-pager { display: flex; align-items: center; justify-content: space-between; padding: 8px 14px; background: #f8f9fb; border-top: 1px solid #e0e5ed; font-size: 11px; color: #666; flex-wrap: wrap; gap: 8px; }
.ft-pager__info strong { color: #05275C; }
.ft-pager__btns { display: flex; gap: 3px; align-items: center; flex-wrap: wrap; }
.ft-pager__btn { min-width: 30px; padding: 4px 8px; font-size: 11px; font-weight: 600; border: 1px solid #e0e5ed; background: #fff; color: #444; cursor: pointer; font-family: inherit; line-height: 1.4; text-align: center; }
.ft-pager__btn:hover:not([disabled]) { border-color: #174DA4; color: #174DA4; background: #eef2fc; }
.ft-pager__btn[disabled] { opacity: .4; cursor: not-allowed; }
.ft-pager__btn--active { background: #05275C !important; color: #fff !important; border-color: #05275C !important; }
.ft-pager__ellipsis { padding: 4px 2px; color: #aaa; font-size: 12px; }

/* ===== TOTALS BAR ======================================================= */
.ft-totals { display: flex; align-items: center; gap: 16px; padding: 7px 14px; background: linear-gradient(90deg, #f0f4fc 0%, #f8f9fb 100%); border-top: 1px solid #e0e5ed; font-size: 11px; }
.ft-totals__label { font-weight: 700; color: #555; text-transform: uppercase; letter-spacing: .6px; font-size: 9px; margin-right: 2px; }
.ft-totals__pills { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
.ft-totals__pill { display: inline-flex; align-items: center; gap: 5px; padding: 3px 10px; border-radius: 20px; font-weight: 600; font-variant-numeric: tabular-nums; line-height: 1.5; white-space: nowrap; }
.ft-totals__pill--bill { background: #e0f2f1; color: #00695c; border: 1px solid #b2dfdb; }
.ft-totals__pill--pay  { background: #e6f4ea; color: #2e7d32; border: 1px solid #c8e6c9; }
.ft-totals__pill--net  { background: #e8f0fc; color: #174DA4; border: 1px solid #c5d5f0; }
.ft-totals__pill--neg  { background: #fce8e8; color: #c62828; border: 1px solid #f0c5c5; }
.ft-totals__pill svg   { flex-shrink: 0; }

/* ===== MODAL (from FeesStructure design system) ========================= */
.fs-modal-overlay { display: none; position: fixed; inset: 0; background: rgba(0,0,0,.45); z-index: 9998; }
.fs-modal-overlay--visible { display: flex; align-items: center; justify-content: center; }
.fs-modal { background: #fff; width: 560px; max-width: 96vw; max-height: 92vh; overflow-y: auto; box-shadow: 0 12px 40px rgba(0,0,0,.18); }
.fs-modal__header { background: #05275C; padding: 12px 18px; display: flex; align-items: center; justify-content: space-between; }
.fs-modal__title  { font-size: 13px; font-weight: 700; color: #fff; }
.fs-modal__close  { width: 24px; height: 24px; border: none; background: rgba(255,255,255,.15); cursor: pointer; color: #fff; font-size: 16px; line-height: 1; display: flex; align-items: center; justify-content: center; }
.fs-modal__close:hover { background: rgba(255,255,255,.3); }
.fs-modal__body   { padding: 16px 18px; }
.fs-modal__footer { padding: 11px 18px; border-top: 1px solid #e0e5ed; display: flex; gap: 8px; justify-content: flex-end; background: #f8f9fb; }

/* Form controls */
.fs-form-row { display: flex; gap: 10px; margin-bottom: 10px; flex-wrap: wrap; }
.fs-form-group { display: flex; flex-direction: column; gap: 3px; flex: 1; min-width: 130px; }
.fs-form-label { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #666; font-weight: 700; }
.fs-form-input { border: 1px solid #cdd3de; padding: 6px 9px; font-size: 12px; color: #1a1a2e; background: #fff; width: 100%; box-sizing: border-box; }
.fs-form-input:focus { border-color: #174DA4; outline: none; box-shadow: 0 0 0 2px rgba(23,77,164,.12); }
.fs-form-input:disabled, .fs-form-input[readonly] { background: #f5f7fa; color: #888; cursor: not-allowed; }

/* Buttons (modal) */
.fs-btn { padding: 5px 13px; font-size: 11px; font-weight: 600; border: none; cursor: pointer; display: inline-flex; align-items: center; gap: 5px; white-space: nowrap; transition: background .15s; line-height: 1.5; }
.fs-btn--primary { background: #05275C; color: #fff; } .fs-btn--primary:hover { background: #041d45; }
.fs-btn--ghost   { background: #fff; color: #444; border: 1px solid #cdd3de; } .fs-btn--ghost:hover { border-color: #05275C; color: #05275C; }

/* Toast */
.fs-toast { display: none; padding: 9px 14px; font-size: 12px; font-weight: 600; margin-bottom: 12px; border: 1px solid transparent; }
.fs-toast--success { display: block; background: #e6f4ea; color: #155724; border-color: #c3e6cb; }
.fs-toast--error   { display: block; background: #fde8e8; color: #c62828; border-color: #f5c6cb; }

/* ===== GL SYNC MODAL ==================================================== */
.gl-modal { width: 720px; }
.gl-kpi-row { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 14px; }
.gl-kpi { padding: 12px 14px; border: 1px solid #e0e5ed; background: #f8f9fb; position: relative; overflow: hidden; }
.gl-kpi::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 3px; }
.gl-kpi--orphan::before { background: #e65100; } .gl-kpi--wrong::before { background: #7b1fa2; }
.gl-kpi__val { font-size: 22px; font-weight: 700; font-variant-numeric: tabular-nums; }
.gl-kpi--orphan .gl-kpi__val { color: #e65100; } .gl-kpi--wrong .gl-kpi__val { color: #7b1fa2; }
.gl-kpi__label { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #888; margin-top: 2px; }
.gl-progress { margin: 14px 0; }
.gl-progress__bar-wrap { height: 6px; background: #e0e5ed; width: 100%; overflow: hidden; }
.gl-progress__bar { height: 100%; width: 0; background: #16a34a; transition: width .4s ease; }
.gl-progress__text { font-size: 11px; color: #555; margin-top: 5px; text-align: center; }
.gl-log { max-height: 260px; overflow-y: auto; font-family: 'Consolas', 'Courier New', monospace; font-size: 11px; background: #1a1a2e; color: #a8e6cf; padding: 10px 12px; line-height: 1.6; margin-top: 10px; }
.gl-log__line { margin: 0; white-space: pre-wrap; word-break: break-word; }
.gl-log__line--info { color: #a8e6cf; } .gl-log__line--warn { color: #ffd54f; } .gl-log__line--err { color: #ef5350; }
.gl-log__line--ok   { color: #69f0ae; }
.gl-sample { margin-top: 10px; }
.gl-sample__title { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: .4px; color: #888; margin-bottom: 4px; }
.gl-sample__table { width: 100%; border-collapse: collapse; font-size: 11px; }
.gl-sample__table th { background: #f0f2f5; padding: 5px 8px; text-align: left; font-weight: 700; font-size: 10px; text-transform: uppercase; color: #555; border-bottom: 1px solid #e0e5ed; }
.gl-sample__table td { padding: 4px 8px; border-bottom: 1px solid #f0f2f5; }
.gl-sample__table tr:hover td { background: #fafbff; }
.gl-badge-dr { background: #fff3e0; color: #e65100; padding: 1px 6px; font-size: 10px; font-weight: 600; } 
.gl-badge-cr { background: #e6f4ea; color: #2e7d32; padding: 1px 6px; font-size: 10px; font-weight: 600; }
.gl-result { margin-top: 14px; padding: 12px 14px; font-size: 12px; font-weight: 600; }
.gl-result--ok { background: #e6f4ea; border: 1px solid #c3e6cb; color: #155724; }
.gl-result--err { background: #fde8e8; border: 1px solid #f5c6cb; color: #c62828; }
.gl-result--none { background: #f0f4ff; border: 1px solid #d0daf0; color: #174DA4; }

/* ===== BATCH DOUBLE-BILLING FIX WIZARD ================================== */
.bd-modal { width: 820px; }

/* Wizard steps indicator */
.bd-steps { display: flex; align-items: center; gap: 0; margin-bottom: 18px; padding: 0 4px; }
.bd-step { display: flex; align-items: center; gap: 8px; flex: 1; }
.bd-step__num { width: 28px; height: 28px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700; border: 2px solid #d0d5dd; color: #999; background: #fff; flex-shrink: 0; transition: all .3s; }
.bd-step__label { font-size: 11px; font-weight: 600; color: #999; transition: color .3s; white-space: nowrap; }
.bd-step__line { flex: 1; height: 2px; background: #e0e5ed; margin: 0 8px; transition: background .3s; }
.bd-step--active .bd-step__num { border-color: #174DA4; color: #fff; background: #174DA4; }
.bd-step--active .bd-step__label { color: #174DA4; }
.bd-step--done .bd-step__num { border-color: #16a34a; color: #fff; background: #16a34a; }
.bd-step--done .bd-step__label { color: #16a34a; }
.bd-step--done .bd-step__line { background: #16a34a; }

/* Wizard panels */
.bd-panel { display: none; }
.bd-panel--active { display: block; }

/* KPI row */
.bd-kpi-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; margin-bottom: 14px; }
.bd-kpi { padding: 12px 14px; border: 1px solid #e0e5ed; background: #f8f9fb; position: relative; overflow: hidden; }
.bd-kpi::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 3px; }
.bd-kpi--students::before { background: #174DA4; }
.bd-kpi--dups::before { background: #dc3545; }
.bd-kpi--amount::before { background: #e65100; }
.bd-kpi--index::before { background: #16a34a; }
.bd-kpi__val { font-size: 20px; font-weight: 700; font-variant-numeric: tabular-nums; }
.bd-kpi--students .bd-kpi__val { color: #174DA4; }
.bd-kpi--dups .bd-kpi__val { color: #dc3545; }
.bd-kpi--amount .bd-kpi__val { color: #e65100; }
.bd-kpi--index .bd-kpi__val { color: #16a34a; }
.bd-kpi__label { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #888; margin-top: 2px; }

/* Affected accounts table */
.bd-acct-wrap { max-height: 260px; overflow-y: auto; margin-bottom: 12px; border: 1px solid #e0e5ed; }
.bd-acct-tbl { width: 100%; border-collapse: collapse; font-size: 11px; }
.bd-acct-tbl th { padding: 6px 10px; text-align: left; font-size: 9px; text-transform: uppercase; letter-spacing: .3px; color: #555; background: #f9fafc; border-bottom: 2px solid #e0e5ed; position: sticky; top: 0; z-index: 1; }
.bd-acct-tbl td { padding: 5px 10px; border-bottom: 1px solid #f0f2f5; }
.bd-acct-tbl tr:hover td { background: #fafbff; }
.bd-acct-tbl td.r { text-align: right; font-variant-numeric: tabular-nums; }
.bd-status-cell { min-width: 90px; }
.bd-tag { display: inline-block; padding: 1px 6px; font-size: 9px; font-weight: 600; }
.bd-tag--pending { background: #fff3e0; color: #e65100; }
.bd-tag--fixing { background: #e3f2fd; color: #1565c0; }
.bd-tag--done { background: #e6f4ea; color: #16a34a; }
.bd-tag--error { background: #fde8e8; color: #dc3545; }

/* Progress panel */
.bd-progress-card { background: #f8f9fb; border: 1px solid #e0e5ed; padding: 16px; margin-bottom: 14px; }
.bd-progress-hdr { display: flex; align-items: center; justify-content: space-between; margin-bottom: 10px; }
.bd-progress-hdr__title { font-size: 12px; font-weight: 700; color: #05275C; }
.bd-progress-hdr__count { font-size: 11px; color: #555; font-weight: 600; font-variant-numeric: tabular-nums; }
.bd-progress__bar-wrap { height: 10px; background: #e0e5ed; width: 100%; overflow: hidden; border-radius: 5px; }
.bd-progress__bar { height: 100%; width: 0; background: linear-gradient(90deg, #174DA4, #16a34a); transition: width .4s ease; border-radius: 5px; }
.bd-progress__text { font-size: 11px; color: #555; margin-top: 6px; text-align: center; }
.bd-progress__current { font-size: 11px; color: #174DA4; font-weight: 600; margin-top: 6px; }

/* Log console (reuses gl-log scheme) */
.bd-log { max-height: 200px; overflow-y: auto; font-family: 'Consolas', 'Courier New', monospace; font-size: 11px; background: #1a1a2e; color: #a8e6cf; padding: 10px 12px; line-height: 1.6; margin-top: 10px; }
.bd-log p { margin: 0; white-space: pre-wrap; word-break: break-word; }
.bd-log .l-info { color: #a8e6cf; } .bd-log .l-warn { color: #ffd54f; } .bd-log .l-err { color: #ef5350; } .bd-log .l-ok { color: #69f0ae; }

/* Results panel */
.bd-result-banner { padding: 14px 18px; margin-bottom: 14px; font-size: 13px; font-weight: 600; }
.bd-result-banner--ok { background: #e6f4ea; border: 1px solid #c3e6cb; color: #155724; }
.bd-result-banner--none { background: #f0f4ff; border: 1px solid #d0daf0; color: #174DA4; }
.bd-result-banner--err { background: #fde8e8; border: 1px solid #f5c6cb; color: #c62828; }
.bd-stat-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; margin-bottom: 14px; }
.bd-stat { padding: 10px 14px; border: 1px solid #e0e5ed; background: #fff; text-align: center; }
.bd-stat__val { font-size: 22px; font-weight: 800; font-variant-numeric: tabular-nums; color: #05275C; }
.bd-stat__label { font-size: 10px; text-transform: uppercase; letter-spacing: .3px; color: #888; margin-top: 2px; }
.bd-stat--green .bd-stat__val { color: #16a34a; }
.bd-stat--red .bd-stat__val { color: #dc3545; }

@media (max-width: 700px) { .bd-kpi-row, .bd-stat-row { grid-template-columns: repeat(2, 1fr); } .bd-modal { width: 100%; } }

/* Student lookup info card */
.ft-student-info { display: none; background: #f0f4ff; border: 1px solid #d0daf0; padding: 10px 14px; margin-bottom: 10px; font-size: 12px; }
.ft-student-info--visible { display: block; }
.ft-student-info--error { background: #fde8e8; border-color: #f5c6cb; color: #c62828; }
.ft-student-info__name { font-weight: 700; color: #05275C; font-size: 13px; }
.ft-student-info__detail { color: #555; margin-top: 2px; }

/* Required asterisk */
.fs-form-label .req { color: #dc3545; }

/* ===== STUDENT AUTOCOMPLETE ============================================ */
.ft-ac { position: relative; flex: 1; }
.ft-ac__input { width: 100%; border: 1px solid #cdd3de; padding: 7px 32px 7px 9px; font-size: 12px; color: #1a1a2e; background: #fff; box-sizing: border-box; }
.ft-ac__input:focus { border-color: #174DA4; outline: none; box-shadow: 0 0 0 2px rgba(23,77,164,.12); }
.ft-ac__input--selected { border-color: #16a34a; background: #f0fdf4; }
.ft-ac__spinner { display: none; position: absolute; right: 9px; top: 50%; transform: translateY(-50%); width: 14px; height: 14px; border: 2px solid #e0e5ed; border-top-color: #174DA4; animation: ftSpin .6s linear infinite; }
.ft-ac__spinner--visible { display: block; }
@keyframes ftSpin { to { transform: translateY(-50%) rotate(360deg); } }
.ft-ac__clear { display: none; position: absolute; right: 9px; top: 50%; transform: translateY(-50%); width: 18px; height: 18px; border: none; background: #e0e5ed; color: #666; font-size: 12px; line-height: 1; cursor: pointer; align-items: center; justify-content: center; }
.ft-ac__clear--visible { display: flex; }
.ft-ac__clear:hover { background: #dc3545; color: #fff; }
.ft-ac__list { display: none; position: absolute; left: 0; right: 0; top: 100%; z-index: 9999; background: #fff; border: 1px solid #174DA4; border-top: none; max-height: 260px; overflow-y: auto; box-shadow: 0 8px 24px rgba(0,0,0,.15); }
.ft-ac__list--visible { display: block; }
.ft-ac__item { padding: 8px 12px; cursor: pointer; border-bottom: 1px solid #f0f2f5; display: flex; align-items: center; gap: 10px; transition: background .1s; }
.ft-ac__item:last-child { border-bottom: none; }
.ft-ac__item:hover, .ft-ac__item--active { background: #e8f0fc; }
.ft-ac__item--active { background: #dbeafe; }
.ft-ac__avatar { width: 32px; height: 32px; background: #05275C; color: #fff; font-size: 11px; font-weight: 700; display: flex; align-items: center; justify-content: center; flex-shrink: 0; letter-spacing: .5px; }
.ft-ac__info { flex: 1; min-width: 0; }
.ft-ac__name { font-size: 12px; font-weight: 700; color: #1a1a2e; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.ft-ac__name mark { background: #fff3cd; color: #1a1a2e; padding: 0 1px; font-weight: 700; }
.ft-ac__meta { font-size: 10px; color: #888; margin-top: 1px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.ft-ac__meta mark { background: #fff3cd; color: #888; padding: 0 1px; }
.ft-ac__regno { font-size: 10px; font-weight: 600; color: #174DA4; white-space: nowrap; text-align: right; }
.ft-ac__regno mark { background: #fff3cd; color: #174DA4; padding: 0 1px; font-weight: 700; }
.ft-ac__empty { padding: 14px 12px; text-align: center; font-size: 11px; color: #888; }
.ft-ac__hint { padding: 6px 12px; background: #f8f9fb; border-top: 1px solid #e0e5ed; font-size: 9px; color: #999; text-align: center; }
.ft-ac__status { font-size: 9px; font-weight: 600; padding: 1px 6px; text-transform: uppercase; letter-spacing: .3px; display: inline-block; margin-top: 2px; }
.ft-ac__status--active { background: #dcfce7; color: #16a34a; }
.ft-ac__status--admitted { background: #dbeafe; color: #174DA4; }
.ft-ac__status--other { background: #f3f4f6; color: #888; }

/* Selected student card */
.ft-selected-student { display: none; background: #f0fdf4; border: 1px solid #bbf7d0; padding: 10px 14px; margin-bottom: 10px; }
.ft-selected-student--visible { display: flex; align-items: center; gap: 12px; }
.ft-selected-student__avatar { width: 38px; height: 38px; background: #05275C; color: #fff; font-size: 13px; font-weight: 700; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
.ft-selected-student__info { flex: 1; min-width: 0; }
.ft-selected-student__name { font-size: 13px; font-weight: 700; color: #05275C; }
.ft-selected-student__detail { font-size: 11px; color: #555; margin-top: 2px; }
.ft-selected-student__detail span { display: inline-block; margin-right: 12px; }
.ft-selected-student__regno { font-size: 11px; font-weight: 700; color: #16a34a; background: #dcfce7; padding: 2px 8px; white-space: nowrap; }
.ft-selected-student__remove { width: 22px; height: 22px; border: 1px solid #e0e5ed; background: #fff; color: #888; font-size: 14px; cursor: pointer; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
.ft-selected-student__remove:hover { border-color: #dc3545; color: #dc3545; background: #fde8e8; }

/* Row Action Button */
.ft-row-action { width:28px; height:28px; border:1px solid transparent; background:none; color:#666; font-size:18px; line-height:1; cursor:pointer; border-radius:4px; display:inline-flex; align-items:center; justify-content:center; transition:all .15s; }
.ft-row-action:hover { background:#eef1f6; border-color:#d0d5dd; color:#05275C; }

/* Action Popover (position:fixed to avoid clipping) */
.ft-action-pop { position:fixed; z-index:99999; background:#fff; border-radius:8px; box-shadow:0 8px 24px rgba(0,0,0,.16),0 2px 8px rgba(0,0,0,.08); border:1px solid #e0e5ed; min-width:170px; padding:4px 0; display:none; }
.ft-action-pop--visible { display:block; }
.ft-action-pop__item { display:flex; align-items:center; gap:8px; padding:9px 16px; font-size:13px; color:#333; cursor:pointer; border:none; background:none; width:100%; text-align:left; transition:background .12s; font-family:inherit; }
.ft-action-pop__item:hover { background:#f0f4ff; color:#05275C; }
.ft-action-pop__item--danger { color:#dc3545; }
.ft-action-pop__item--danger:hover { background:#fde8e8; color:#b91c1c; }
.ft-action-pop__sep { height:1px; background:#e5e7eb; margin:4px 0; }

/* Delete Confirmation Modal */
.ft-confirm-overlay { position:fixed; z-index:100000; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,.45); display:none; align-items:center; justify-content:center; }
.ft-confirm-overlay--visible { display:flex; }
.ft-confirm { background:#fff; border-radius:10px; box-shadow:0 12px 40px rgba(0,0,0,.2); width:520px; max-width:95vw; overflow:hidden; animation:ftConfirmIn .2s ease; }
@keyframes ftConfirmIn { from { transform:scale(.95); opacity:0; } to { transform:scale(1); opacity:1; } }
.ft-confirm__header { padding:16px 20px; background:#fde8e8; border-bottom:1px solid #fecaca; display:flex; align-items:center; gap:10px; }
.ft-confirm__icon { width:36px; height:36px; background:#dc3545; color:#fff; border-radius:8px; display:flex; align-items:center; justify-content:center; flex-shrink:0; }
.ft-confirm__title { font-size:15px; font-weight:700; color:#991b1b; }
.ft-confirm__body { padding:20px; font-size:13px; color:#333; line-height:1.6; }
.ft-confirm__detail { background:#f9fafb; border:1px solid #e5e7eb; border-radius:6px; padding:10px 14px; margin-top:10px; }
.ft-confirm__detail dt { font-size:11px; color:#888; text-transform:uppercase; letter-spacing:.5px; margin-top:6px; }
.ft-confirm__detail dt:first-child { margin-top:0; }
.ft-confirm__detail dd { margin:2px 0 0 0; font-weight:600; color:#05275C; }
.ft-confirm__footer { padding:12px 20px; border-top:1px solid #e5e7eb; display:flex; justify-content:flex-end; gap:8px; }
.ft-confirm__btn { padding:8px 18px; border-radius:6px; font-size:13px; font-weight:600; cursor:pointer; border:1px solid transparent; transition:all .15s; font-family:inherit; }
.ft-confirm__btn--cancel { background:#fff; border-color:#d0d5dd; color:#555; }
.ft-confirm__btn--cancel:hover { background:#f5f5f5; }
.ft-confirm__btn--delete { background:#dc3545; color:#fff; }
.ft-confirm__btn--delete:hover { background:#b91c1c; }
.ft-confirm__reason-group { margin-top:14px; }
.ft-confirm__reason-group > .ft-reason-label { display:block; font-size:11px; font-weight:700; color:#374151; margin-bottom:8px; text-transform:uppercase; letter-spacing:.4px; }
.ft-reason-options { display:grid; grid-template-columns:1fr 1fr; gap:5px 10px; }
.ft-reason-option { display:flex; align-items:center; gap:7px; padding:6px 9px; border:1.5px solid #e5e7eb; border-radius:6px; cursor:pointer; transition:border-color .15s, background .15s; user-select:none; }
.ft-reason-option:hover { border-color:#94a3b8; background:#f8fafc; }
.ft-reason-option input[type=radio] { accent-color:#dc3545; width:14px; height:14px; flex-shrink:0; cursor:pointer; margin:0; }
.ft-reason-option span { font-size:12px; color:#374151; line-height:1.3; }
.ft-reason-option--selected { border-color:#dc3545; background:#fff5f5; }
.ft-reason-option--selected span { color:#991b1b; font-weight:600; }
.ft-reason-options--invalid .ft-reason-option { border-color:#fca5a5; }
.ft-confirm__reason-group textarea { width:100%; padding:7px 10px; border:1px solid #d0d5dd; border-radius:6px; font-size:13px; color:#374151; resize:vertical; font-family:inherit; outline:none; transition:border-color .15s; box-sizing:border-box; margin-top:8px; }
.ft-confirm__reason-group textarea:focus { border-color:#4f46e5; }
.ft-confirm__reason-error { font-size:11px; color:#dc3545; margin-top:5px; display:none; }

/* Responsive */
@media (max-width: 1200px) { .ft-stats { grid-template-columns: repeat(3, 1fr); } }
@media (max-width: 800px) { .ft-stats { grid-template-columns: 1fr 1fr; } .ft-stat__val { font-size: 13px; } }
@media (max-width: 500px) { .ft-stats { grid-template-columns: 1fr; } .fs-modal { width: 98vw; } }

/* ===== BATCH SELECTION ================================================== */
.ft-col-chk { width: 36px; text-align: center; padding: 0 6px !important; }
.ft-chk { width: 15px; height: 15px; cursor: pointer; accent-color: #05275C; }
.ft-table tbody tr.ft-row--selected { background: #e8f0fc !important; }
.ft-table tbody tr.ft-row--selected:nth-child(even) { background: #dce8fb !important; }

/* Batch Toolbar (slides up from bottom) */
.ft-batch-bar { position: fixed; bottom: 0; left: 0; right: 0; z-index: 9990;
    background: #05275C; color: #fff; padding: 10px 22px;
    display: flex; align-items: center; gap: 12px; flex-wrap: wrap;
    box-shadow: 0 -4px 18px rgba(0,0,0,.22);
    transform: translateY(100%); transition: transform .22s ease; }
.ft-batch-bar--visible { transform: translateY(0); }
.ft-batch-bar__count { font-size: 13px; font-weight: 700; white-space: nowrap; flex: 1; }
.ft-batch-bar__count span { background: #174DA4; padding: 2px 10px; margin-right: 6px; font-variant-numeric: tabular-nums; }
.ft-batch-btn { padding: 7px 16px; font-size: 12px; font-weight: 600; border: none; cursor: pointer;
    display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; transition: all .15s; font-family: inherit; }
.ft-batch-btn--danger  { background: #dc3545; color: #fff; }
.ft-batch-btn--danger:hover  { background: #b91c1c; }
.ft-batch-btn--warning { background: #e65100; color: #fff; }
.ft-batch-btn--warning:hover { background: #bf360c; }
.ft-batch-btn--success { background: #16a34a; color: #fff; }
.ft-batch-btn--success:hover { background: #15803d; }
.ft-batch-btn--ghost   { background: transparent; border: 1px solid rgba(255,255,255,.35); color: #fff; }
.ft-batch-btn--ghost:hover   { background: rgba(255,255,255,.12); }
.ft-batch-sep { width: 1px; height: 24px; background: rgba(255,255,255,.2); flex-shrink: 0; }

/* Batch Confirm Modal */
.ft-batch-modal { background:#fff; width:560px; max-width:96vw; max-height:90vh; overflow-y:auto; box-shadow:0 12px 40px rgba(0,0,0,.22); }
.ft-batch-modal__header { background:#991b1b; padding:14px 18px; display:flex; align-items:center; justify-content:space-between; }
.ft-batch-modal__title { font-size:14px; font-weight:700; color:#fff; display:flex; align-items:center; gap:8px; }
.ft-batch-modal__close { width:26px; height:26px; border:none; background:rgba(255,255,255,.15); cursor:pointer; color:#fff; font-size:16px; display:flex; align-items:center; justify-content:center; }
.ft-batch-modal__close:hover { background:rgba(255,255,255,.3); }
.ft-batch-modal__body { padding:18px; }
.ft-batch-modal__footer { padding:12px 18px; border-top:1px solid #e5e7eb; display:flex; justify-content:flex-end; gap:8px; background:#f8f9fb; }
.ft-batch-summary { background:#fff5f5; border:1px solid #fecaca; padding:12px 14px; margin-bottom:14px; font-size:13px; color:#991b1b; font-weight:600; display:flex; align-items:center; gap:10px; }
.ft-batch-progress { margin:14px 0; }
.ft-batch-progress__bar-wrap { height:8px; background:#e0e5ed; width:100%; overflow:hidden; }
.ft-batch-progress__bar { height:100%; width:0%; background:linear-gradient(90deg,#dc3545,#e65100); transition:width .3s ease; }
.ft-batch-progress__text { font-size:11px; color:#555; margin-top:5px; text-align:center; }
.ft-batch-result { padding:12px 14px; font-size:12px; font-weight:600; margin-top:10px; }
.ft-batch-result--ok  { background:#e6f4ea; border:1px solid #c3e6cb; color:#155724; }
.ft-batch-result--err { background:#fde8e8; border:1px solid #f5c6cb; color:#c62828; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<asp:Button ID="btnExportCsv" runat="server" style="display:none;" OnClick="btnExportCsv_Click" />
<asp:Button ID="btnSearch" runat="server" style="display:none;" OnClick="btnSearch_Click" />
<asp:Button ID="btnReset" runat="server" style="display:none;" OnClick="btnReset_Click" />
<asp:Button ID="btnSaveTransaction" runat="server" style="display:none;" OnClick="btnSaveTransaction_Click" />
<asp:Button ID="btnEditTransaction" runat="server" style="display:none;" OnClick="btnEditTransaction_Click" />
<asp:Button ID="btnDeleteTransaction" runat="server" style="display:none;" OnClick="btnDeleteTransaction_Click" />
<asp:Button ID="btnRemoveFromGL" runat="server" style="display:none;" OnClick="btnRemoveFromGL_Click" />
<asp:HiddenField ID="hfEditTID" runat="server" />
<%-- The TID alone does not identify a row: manual rows are keyed in fin_studentfeestracking
     and ledger-only rows in fin_ledger, and the two numberings overlap. Source says which
     table, regno says which account, and the server checks both before it writes. --%>
<asp:HiddenField ID="hfEditSource" runat="server" />
<asp:HiddenField ID="hfEditRegno" runat="server" />
<asp:HiddenField ID="hfDeleteTID" runat="server" />
<asp:HiddenField ID="hfRemoveGLTID" runat="server" />
<asp:HiddenField ID="hfDeleteCategory" runat="server" />
<asp:HiddenField ID="hfDeleteExplanation" runat="server" />
<asp:HiddenField ID="hfPageIndex" runat="server" Value="0" />
<asp:Button ID="btnGoToPage" runat="server" style="display:none;" OnClick="btnGoToPage_Click" />

<!-- Toast -->
<asp:Panel ID="pnlToast" runat="server" Visible="false">
    <div class="fs-toast" id="divToast" runat="server"></div>
</asp:Panel>

<!-- Stats -->
<div class="ft-stats">
    <div class="ft-stat ft-stat--total">
        <div class="ft-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#174DA4" stroke-width="2"><polyline points="17 1 21 5 17 9"></polyline><path d="M3 11V9a4 4 0 0 1 4-4h14"></path><polyline points="7 23 3 19 7 15"></polyline><path d="M21 13v2a4 4 0 0 1-4 4H3"></path></svg></div>
        <div><div class="ft-stat__val"><asp:Literal ID="litTotalTx" runat="server" Text="0" /></div><div class="ft-stat__label">Total Transactions</div></div>
    </div>
    <div class="ft-stat ft-stat--bills">
        <div class="ft-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#00695c" stroke-width="2"><path d="M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1-2-1z"></path></svg></div>
        <div><div class="ft-stat__val"><asp:Literal ID="litBillTx" runat="server" Text="0" /></div><div class="ft-stat__label">Bills</div></div>
    </div>
    <div class="ft-stat ft-stat--pays">
        <div class="ft-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#2e7d32" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg></div>
        <div><div class="ft-stat__val"><asp:Literal ID="litPayTx" runat="server" Text="0" /></div><div class="ft-stat__label">Payments</div></div>
    </div>
    <div class="ft-stat ft-stat--bamt">
        <div class="ft-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#e65100" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"></line><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path></svg></div>
        <div><div class="ft-stat__val"><asp:Literal ID="litBillAmt" runat="server" Text="0" /></div><div class="ft-stat__label">Total Billed</div></div>
    </div>
    <div class="ft-stat ft-stat--pamt">
        <div class="ft-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#2e7d32" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"></line><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path></svg></div>
        <div><div class="ft-stat__val"><asp:Literal ID="litPayAmt" runat="server" Text="0" /></div><div class="ft-stat__label">Total Paid</div></div>
    </div>
</div>

<!-- Main Grid Card -->
<div class="ft-card">
    <!-- Filters -->
    <div class="ft-filters">
        <div class="ft-filters__top">
            <div class="ft-search-wrap">
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                <asp:TextBox ID="txtSearch" runat="server" CssClass="ft-search-box" placeholder="Search by reg no, student name, description..." AutoPostBack="false" />
            </div>
            <button type="button" class="ft-btn ft-btn--primary ft-btn--sm" onclick="ftApply()">Search</button>
            <asp:Label ID="lblRecordCount" runat="server" CssClass="ft-card__meta" Text="0 records" />
            <asp:Literal ID="litAcadContext" runat="server" />
        </div>
        <div class="ft-filters__row">
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Academic Year</label>
                <asp:DropDownList ID="ddlAcadYear" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()" />
            </div>
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Semester</label>
                <asp:DropDownList ID="ddlSemester" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()">
                    <asp:ListItem Value="" Text="All Semesters" />
                    <asp:ListItem Value="1" Text="Semester 1" />
                    <asp:ListItem Value="2" Text="Semester 2" />
                    <asp:ListItem Value="3" Text="Semester 3" />
                </asp:DropDownList>
            </div>
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Type</label>
                <asp:DropDownList ID="ddlTransType" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()">
                    <asp:ListItem Value="" Text="All Types" />
                    <asp:ListItem Value="Bill" Text="Bills" />
                    <asp:ListItem Value="Payment" Text="Payments" />
                </asp:DropDownList>
            </div>
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Billing Item</label>
                <asp:DropDownList ID="ddlBillItem" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()" style="min-width:160px;" />
            </div>
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Post Status</label>
                <asp:DropDownList ID="ddlPostStatus" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()">
                    <asp:ListItem Value="" Text="All" />
                    <asp:ListItem Value="Posted" Text="Posted" />
                    <asp:ListItem Value="Pending" Text="Pending" />
                </asp:DropDownList>
            </div>
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Student Status</label>
                <asp:DropDownList ID="ddlStudStatus" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()">
                    <asp:ListItem Value="" Text="All Students" Selected="True" />
                    <asp:ListItem Value="Active" Text="Active" />
                    <asp:ListItem Value="ADMITTED" Text="Admitted" />
                </asp:DropDownList>
            </div>
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Source</label>
                <asp:DropDownList ID="ddlSource" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()">
                    <asp:ListItem Value="" Text="All Sources" Selected="True" />
                    <asp:ListItem Value="manual" Text="Manual Only" />
                    <asp:ListItem Value="gl_only" Text="GL Only (Orphaned)" />
                </asp:DropDownList>
            </div>
            <div class="ft-filter-grp">
                <label class="ft-filter-grp__label">Per Page</label>
                <asp:DropDownList ID="ddlPageSize" runat="server" CssClass="ft-filter-select" AutoPostBack="false" onchange="ftApply()" style="min-width:80px;">
                    <asp:ListItem Value="50" Text="50" Selected="True" />
                    <asp:ListItem Value="100" Text="100" />
                    <asp:ListItem Value="200" Text="200" />
                    <asp:ListItem Value="500" Text="500" />
                </asp:DropDownList>
            </div>
            <button type="button" class="ft-btn ft-btn--ghost ft-btn--sm" style="align-self:flex-end;" onclick="ftReset()">
                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="1 4 1 10 7 10"></polyline><path d="M3.51 15a9 9 0 1 0 .49-3.5"></path></svg>
                Reset
            </button>
            <span style="flex:1;"></span>
            <button type="button" class="ft-btn ft-btn--primary ft-btn--sm" style="align-self:flex-end;" onclick="openAddTxModal();">
                <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg>
                New Transaction
            </button>
            <button type="button" class="ft-btn ft-btn--ghost ft-btn--sm" style="align-self:flex-end;" onclick="document.getElementById('<%= btnExportCsv.ClientID %>').click()">
                <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
                Export CSV
            </button>
            <button type="button" class="ft-btn ft-btn--ghost ft-btn--sm" style="align-self:flex-end; border-color:#e65100; color:#e65100;" onclick="openGLSyncModal();">
                <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
                Fix GL
            </button>
            <button type="button" class="ft-btn ft-btn--ghost ft-btn--sm" style="align-self:flex-end; border-color:#dc3545; color:#dc3545;" onclick="openBatchDupModal();">
                <svg xmlns="http://www.w3.org/2000/svg" width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="8.5" cy="7" r="4"></circle><polyline points="17 11 19 13 23 9"></polyline></svg>
                Fix Double Billing
            </button>
        </div>
    </div>

    <!-- Data Table (single scroll container - sticky header, consistent H+V scroll) -->
    <div class="ft-table-wrap">
        <table class="ft-table">
            <colgroup>
                <col class="ft-col-chk"><col class="ft-col-id"><col class="ft-col-regno"><col class="ft-col-name">
                <col class="ft-col-type"><col class="ft-col-amt">
                <col class="ft-col-detail"><col class="ft-col-date">
                <col class="ft-col-year"><col class="ft-col-sem"><col class="ft-col-by"><col style="width:78px;"><col class="ft-col-action">
            </colgroup>
            <thead>
                <tr>
                    <th class="ft-col-chk"><input type="checkbox" class="ft-chk" id="chkSelectAll" title="Select all on this page" onclick="batchSelectAll(this)" /></th>
                    <th class="ft-col-id">ID</th>
                    <th class="ft-col-regno">Reg No</th>
                    <th class="ft-col-name">Student</th>
                    <th class="ft-col-type">Type</th>
                    <th class="ft-col-amt">Amount</th>
                    <th class="ft-col-detail">Description</th>
                    <th class="ft-col-date">Date</th>
                    <th class="ft-col-year">Year</th>
                    <th class="ft-col-sem">Sem</th>
                    <th class="ft-col-by">Created By</th>
                    <th style="width:78px;">Source</th>
                    <th class="ft-col-action"></th>
                </tr>
            </thead>
            <tbody>
                <asp:Repeater ID="rptTransactions" runat="server">
                    <ItemTemplate>
                        <tr data-tid='<%# Eval("TID") %>'
                            data-regno='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("regno"))) %>'
                            data-name='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("student_name"))) %>'
                            data-type='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("trans_type"))) %>'
                            data-itemcode='<%# Eval("item_code") %>'
                            data-itemname='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("item_name"))) %>'
                            data-amount='<%# Eval("amount") %>'
                            data-amountfmt='<%# HttpUtility.HtmlAttributeEncode(FormatAmt(Eval("amount"))) %>'
                            data-detail='<%# HttpUtility.HtmlAttributeEncode(DisplayDetail(Eval("detail"), Eval("item_name"), Eval("trans_type"), Eval("TID"))) %>'
                            data-status='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("post_status"))) %>'
                            data-date='<%# FormatDateISO(Eval("trans_date")) %>'
                            data-year='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("acadyear"))) %>'
                            data-sem='<%# Eval("semester") %>'
                            data-source='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("row_source"))) %>'
                            data-createdby='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("created_by"))) %>'
                            onclick="ftShowDetails(this)">
                            <td class="ft-col-chk"><input type="checkbox" class="ft-chk ft-row-chk" value='<%# Eval("TID") %>' data-source='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("row_source"))) %>' onclick="event.stopPropagation();batchRowCheck(this)" /></td>
                            <td class="ft-col-id"><%# Eval("TID") %></td>
                            <td class="ft-col-regno"><%# HttpUtility.HtmlEncode(SafeStr(Eval("regno"))) %></td>
                            <td class="ft-col-name"><%# HttpUtility.HtmlEncode(SafeStr(Eval("student_name"))) %></td>
                            <td class="ft-col-type"><span class='ft-badge <%# GetTypeClass(Eval("trans_type")) %>'><%# HttpUtility.HtmlEncode(SafeStr(Eval("trans_type"))) %></span></td>
                            <td class="ft-col-amt"><%# FormatAmt(Eval("amount")) %></td>
                            <td class="ft-col-detail"><%# HttpUtility.HtmlEncode(DisplayDetail(Eval("detail"), Eval("item_name"), Eval("trans_type"), Eval("TID"))) %></td>
                            <td class="ft-col-date"><%# FormatDateShort(Eval("trans_date")) %></td>
                            <td class="ft-col-year"><%# HttpUtility.HtmlEncode(SafeStr(Eval("acadyear"))) %></td>
                            <td class="ft-col-sem"><%# Eval("semester") %></td>
                            <td class="ft-col-by" title='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("created_by"))) %>'><%# HttpUtility.HtmlEncode(SafeStr(Eval("created_by"))) %></td>
                            <td><%# GetSourceBadge(Eval("row_source")) %></td>
                            <td class="ft-col-action">
                                <button type="button" class="ft-row-action"
                                    data-tid='<%# Eval("TID") %>'
                                    data-regno='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("regno"))) %>'
                                    data-name='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("student_name"))) %>'
                                    data-type='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("trans_type"))) %>'
                                    data-itemcode='<%# Eval("item_code") %>'
                                    data-amount='<%# Eval("amount") %>'
                                    data-detail='<%# HttpUtility.HtmlAttributeEncode(DisplayDetail(Eval("detail"), Eval("item_name"), Eval("trans_type"), Eval("TID"))) %>'
                                    data-status='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("post_status"))) %>'
                                    data-date='<%# FormatDateISO(Eval("trans_date")) %>'
                                    data-year='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("acadyear"))) %>'
                                    data-sem='<%# Eval("semester") %>'
                                    data-source='<%# HttpUtility.HtmlAttributeEncode(SafeStr(Eval("row_source"))) %>'
                                    onclick="event.stopPropagation();showRowAction(event,this)">&#8942;</button>
                            </td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
                <asp:PlaceHolder ID="phNoData" runat="server" Visible="false">
                    <tr><td colspan="13" style="padding:44px 20px;text-align:center;color:#999;font-size:13px;">
                        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="2" style="display:block;margin:0 auto 8px;"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                        No transactions match your current filters.
                    </td></tr>
                </asp:PlaceHolder>
            </tbody>
        </table>
    </div>

    <!-- Totals Bar -->
    <div class="ft-totals">
        <span class="ft-totals__label">Totals</span>
        <div class="ft-totals__pills">
            <span class="ft-totals__pill ft-totals__pill--bill">
                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
                Bills: <asp:Literal ID="litTotalBarBill" runat="server" />
            </span>
            <span class="ft-totals__pill ft-totals__pill--pay">
                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
                Payments: <asp:Literal ID="litTotalBarPay" runat="server" />
            </span>
            <asp:Literal ID="litTotalBarNet" runat="server" />
        </div>
    </div>

    <!-- Pager -->
    <div class="ft-pager">
        <span class="ft-pager__info"><asp:Label ID="lblGridFooter" runat="server" Text="" /></span>
        <asp:Literal ID="litPager" runat="server" />
    </div>
</div>

<!-- Action Popover (position:fixed) -->
<div class="ft-action-pop" id="actionPop">
    <button type="button" class="ft-action-pop__item" id="popBtnEdit" onclick="openEditTx()">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z"></path></svg>
        Edit Transaction
    </button>
    <div class="ft-action-pop__sep" id="popSepManual"></div>
    <button type="button" class="ft-action-pop__item ft-action-pop__item--danger" id="popBtnDelete" onclick="confirmDeleteTx()">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
        Delete Transaction
    </button>
    <button type="button" class="ft-action-pop__item ft-action-pop__item--danger" id="popBtnRemoveGL" style="display:none;" onclick="confirmRemoveGL()">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>
        Remove from GL
    </button>
</div>

<!-- Transaction Details Modal (click any row) -->
<div id="ftDetailsOverlay" style="display:none;position:fixed;inset:0;background:rgba(10,20,40,.45);z-index:1200;align-items:flex-start;justify-content:center;padding:40px 14px;overflow:auto;" onclick="if(event.target===this)ftCloseDetails()">
    <div style="background:#fff;max-width:560px;width:100%;border-radius:4px;box-shadow:0 12px 40px rgba(5,39,92,.28);">
        <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;padding:13px 18px;background:#05275C;color:#fff;border-radius:4px 4px 0 0;">
            <div style="font-size:14px;font-weight:700;">Transaction Details <span id="ftdTid" style="opacity:.7;font-weight:500;"></span></div>
            <button type="button" onclick="ftCloseDetails()" style="background:none;border:none;color:#fff;font-size:22px;line-height:1;cursor:pointer;">&times;</button>
        </div>
        <div style="padding:16px 18px;">
            <div id="ftdStudent" style="font-size:15px;font-weight:700;color:#05275C;margin-bottom:2px;"></div>
            <div id="ftdRegno" style="font-size:12px;color:#174DA4;font-weight:600;margin-bottom:14px;"></div>
            <dl id="ftdGrid" style="display:grid;grid-template-columns:130px 1fr;gap:0;margin:0;"></dl>
            <div id="ftdLinks" style="margin-top:16px;border-top:1px dashed #e0e5ed;padding-top:12px;"></div>
        </div>
        <div id="ftdActions" style="display:flex;flex-wrap:wrap;gap:8px;justify-content:flex-end;padding:12px 18px;background:#f8f9fb;border-top:1px solid #e0e5ed;border-radius:0 0 4px 4px;"></div>
    </div>
</div>

<script type="text/javascript">
document.addEventListener('DOMContentLoaded', function() {
    var tb = document.getElementById('<%= txtSearch.ClientID %>');
    if (tb) {
        tb.addEventListener('keydown', function(e) {
            if (e.keyCode === 13) {
                e.preventDefault();
                ftApply();
            }
        });
    }
    /* Initialize student autocomplete */
    StudentAC.init();
});

/* ==== Modal helpers ==== */
function openModal(id) {
    var el = document.getElementById(id);
    if (el) el.className = 'fs-modal-overlay fs-modal-overlay--visible';
}
function closeModal(id) {
    var el = document.getElementById(id);
    if (el) el.className = 'fs-modal-overlay';
}

/* ==== Add Transaction Modal ==== */
/* ================================================================
   STUDENT AUTOCOMPLETE ENGINE
   - Searches by name, reg number, or student number
   - Debounced XHR with abort on new keystroke
   - Keyboard navigation (Up/Down/Enter/Escape)
   - Highlights matching text
   - Shows rich student cards
   ================================================================ */
var StudentAC = (function() {
    var input, list, spinner, clearBtn, hfRegNo, card;
    var timer = null, xhr = null, activeIdx = -1, results = [], selectedStudent = null;
    var DEBOUNCE = 250, MIN_CHARS = 2;

    function init() {
        input   = document.getElementById('acInput');
        list    = document.getElementById('acList');
        spinner = document.getElementById('acSpinner');
        clearBtn= document.getElementById('acClear');
        hfRegNo = document.getElementById('<%= hfSelectedRegNo.ClientID %>');
        card    = document.getElementById('selectedStudentCard');
        if (!input) return;

        input.addEventListener('input', onInput);
        input.addEventListener('keydown', onKeyDown);
        input.addEventListener('focus', function() { if (results.length > 0 && !selectedStudent) showList(); });
        document.addEventListener('click', function(e) {
            if (!input.contains(e.target) && !list.contains(e.target)) hideList();
        });
        clearBtn.addEventListener('click', clearSelection);
    }

    function onInput() {
        var q = input.value.trim();
        if (selectedStudent) {
            /* User is editing after selection - deselect */
            deselectStudent();
        }
        if (timer) clearTimeout(timer);
        if (xhr) { xhr.abort(); xhr = null; }
        if (q.length < MIN_CHARS) { hideList(); results = []; return; }
        timer = setTimeout(function() { doSearch(q); }, DEBOUNCE);
    }

    function onKeyDown(e) {
        if (!list.classList.contains('ft-ac__list--visible')) {
            if (e.keyCode === 40 && results.length > 0) { showList(); e.preventDefault(); }
            return;
        }
        if (e.keyCode === 40) { /* Down */
            e.preventDefault(); activeIdx = Math.min(activeIdx + 1, results.length - 1); renderActive();
        } else if (e.keyCode === 38) { /* Up */
            e.preventDefault(); activeIdx = Math.max(activeIdx - 1, 0); renderActive();
        } else if (e.keyCode === 13) { /* Enter */
            e.preventDefault();
            if (activeIdx >= 0 && activeIdx < results.length) selectStudent(results[activeIdx]);
        } else if (e.keyCode === 27) { /* Escape */
            hideList();
        }
    }

    function doSearch(q) {
        showSpinner();
        if (xhr) xhr.abort();
        xhr = new XMLHttpRequest();
        xhr.open('GET', 'FeesTransactions.aspx?ajax=search&q=' + encodeURIComponent(q), true);
        xhr.onreadystatechange = function() {
            if (xhr.readyState !== 4) return;
            hideSpinner();
            if (xhr.status === 200) {
                try {
                    var data = JSON.parse(xhr.responseText);
                    results = data.results || [];
                    activeIdx = -1;
                    renderList(q);
                } catch(ex) { results = []; hideList(); }
            }
        };
        xhr.send();
    }

    function statusClass(s) { var u = (s||'').toUpperCase(); if (u === 'ACTIVE') return 'ft-ac__status--active'; if (u === 'ADMITTED') return 'ft-ac__status--admitted'; return 'ft-ac__status--other'; }
    function renderList(query) {
        if (results.length === 0) {
            list.innerHTML = '<div class="ft-ac__empty">'
                + '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="2" style="vertical-align:middle;margin-right:4px;"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>'
                + 'No students found for &ldquo;' + esc(query) + '&rdquo;</div>';
            showList(); return;
        }
        var html = '';
        for (var i = 0; i < results.length; i++) {
            var r = results[i];
            var initials = getInitials(r.name);
            html += '<div class="ft-ac__item' + (i === activeIdx ? ' ft-ac__item--active' : '') + '" data-idx="' + i + '">'
                + '<div class="ft-ac__avatar">' + esc(initials) + '</div>'
                + '<div class="ft-ac__info">'
                + '<div class="ft-ac__name">' + highlight(r.name, query) + '</div>'
                + '<div class="ft-ac__meta">' + highlight(r.programme, query) + ' &middot; ' + esc(r.session) + '</div>'
                + '</div>'
                + '<div style="text-align:right;"><div class="ft-ac__regno">' + highlight(r.regno, query) + '</div>'
                + '<span class="ft-ac__status ' + statusClass(r.status) + '">' + esc(r.status) + '</span></div>'
                + '</div>';
        }
        html += '<div class="ft-ac__hint">' + results.length + (results.length >= 15 ? '+' : '') + ' result' + (results.length !== 1 ? 's' : '') + ' &mdash; type more to refine</div>';
        list.innerHTML = html;

        /* Attach click handlers */
        var items = list.querySelectorAll('.ft-ac__item');
        for (var j = 0; j < items.length; j++) {
            (function(idx) {
                items[idx].addEventListener('click', function() { selectStudent(results[idx]); });
                items[idx].addEventListener('mouseenter', function() { activeIdx = idx; renderActive(); });
            })(j);
        }
        showList();
    }

    function renderActive() {
        var items = list.querySelectorAll('.ft-ac__item');
        for (var i = 0; i < items.length; i++) {
            items[i].className = 'ft-ac__item' + (i === activeIdx ? ' ft-ac__item--active' : '');
        }
        if (activeIdx >= 0 && items[activeIdx]) {
            items[activeIdx].scrollIntoView({ block: 'nearest' });
        }
    }

    function selectStudent(student) {
        selectedStudent = student;
        hideList();
        results = [];
        input.value = student.name + ' (' + student.regno + ')';
        input.classList.add('ft-ac__input--selected');
        hfRegNo.value = student.regno;
        clearBtn.classList.add('ft-ac__clear--visible');

        /* Show selected card */
        var initials = getInitials(student.name);
        card.innerHTML = '<div class="ft-selected-student__avatar">' + esc(initials) + '</div>'
            + '<div class="ft-selected-student__info">'
            + '<div class="ft-selected-student__name">' + esc(student.name) + '</div>'
            + '<div class="ft-selected-student__detail">'
            + '<span>' + esc(student.programme) + '</span>'
            + '<span>' + esc(student.session) + '</span>'
            + (student.studno ? '<span>No: ' + esc(student.studno) + '</span>' : '')
            + '<span class="ft-ac__status ' + statusClass(student.status) + '">' + esc(student.status) + '</span>'
            + '</div></div>'
            + '<div class="ft-selected-student__regno">' + esc(student.regno) + '</div>'
            + '<button type="button" class="ft-selected-student__remove" title="Remove" onclick="StudentAC.clear();">&times;</button>';
        card.className = 'ft-selected-student ft-selected-student--visible';
    }

    function deselectStudent() {
        selectedStudent = null;
        hfRegNo.value = '';
        input.classList.remove('ft-ac__input--selected');
        clearBtn.classList.remove('ft-ac__clear--visible');
        card.className = 'ft-selected-student';
        card.innerHTML = '';
    }

    function clearSelection() {
        deselectStudent();
        input.value = '';
        input.focus();
        hideList();
        results = [];
    }

    function showList()  { list.classList.add('ft-ac__list--visible'); }
    function hideList()  { list.classList.remove('ft-ac__list--visible'); activeIdx = -1; }
    function showSpinner(){ spinner.classList.add('ft-ac__spinner--visible'); clearBtn.classList.remove('ft-ac__clear--visible'); }
    function hideSpinner(){ spinner.classList.remove('ft-ac__spinner--visible'); if (selectedStudent) clearBtn.classList.add('ft-ac__clear--visible'); }

    function highlight(text, query) {
        if (!text || !query) return esc(text || '');
        /* Split query into words and highlight each */
        var words = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').split(/\s+/).filter(function(w) { return w.length > 0; });
        if (words.length === 0) return esc(text);
        var re = new RegExp('(' + words.join('|') + ')', 'gi');
        /* Escape first, then wrap matches */
        var parts = text.split(re);
        var out = '';
        for (var i = 0; i < parts.length; i++) {
            if (re.test(parts[i])) { re.lastIndex = 0; out += '<mark>' + esc(parts[i]) + '</mark>'; }
            else { out += esc(parts[i]); }
        }
        return out;
    }

    function esc(str) {
        var d = document.createElement('div'); d.appendChild(document.createTextNode(str || '')); return d.innerHTML;
    }

    function getInitials(name) {
        if (!name) return '?';
        var p = name.trim().split(/\s+/);
        if (p.length >= 2) return (p[0][0] + p[p.length-1][0]).toUpperCase();
        return p[0][0].toUpperCase();
    }

    function getSelectedRegNo() { return selectedStudent ? selectedStudent.regno : ''; }
    function isStudentSelected() { return !!selectedStudent; }

    return { init: init, clear: clearSelection, getRegNo: getSelectedRegNo, isSelected: isStudentSelected, select: selectStudent };
})();

function openAddTxModal() {
    /* Reset form fields */
    var f = document.getElementById('addTxForm');
    if (f) {
        var inputs = f.querySelectorAll('input[type="text"], input[type="number"]');
        for (var i = 0; i < inputs.length; i++) inputs[i].value = '';
    }
    /* Set date to today */
    var dtField = document.getElementById('<%= txtTxDate.ClientID %>');
    if (dtField) {
        var now = new Date();
        var m = ('0' + (now.getMonth() + 1)).slice(-2);
        var d = ('0' + now.getDate()).slice(-2);
        dtField.value = now.getFullYear() + '-' + m + '-' + d;
    }
    /* Reset student autocomplete */
    StudentAC.clear();
    /* Reset selects to defaults */
    var ddlType = document.getElementById('<%= ddlTxTransType.ClientID %>');
    if (ddlType) ddlType.selectedIndex = 0;
    var ddlStatus = document.getElementById('<%= ddlTxPostStatus.ClientID %>');
    if (ddlStatus) ddlStatus.selectedIndex = 0;
    /* Default academic year & semester from filters */
    var filterYear = document.getElementById('<%= ddlAcadYear.ClientID %>');
    var txYear = document.getElementById('<%= ddlTxAcadYear.ClientID %>');
    if (filterYear && txYear && filterYear.value) {
        for (var j = 0; j < txYear.options.length; j++) {
            if (txYear.options[j].value === filterYear.value) { txYear.selectedIndex = j; break; }
        }
    }
    var filterSem = document.getElementById('<%= ddlSemester.ClientID %>');
    var txSem = document.getElementById('<%= ddlTxSemester.ClientID %>');
    if (filterSem && txSem && filterSem.value) {
        for (var k = 0; k < txSem.options.length; k++) {
            if (txSem.options[k].value === filterSem.value) { txSem.selectedIndex = k; break; }
        }
    }
    /* Clear amount */
    var amt = document.getElementById('<%= txtTxAmount.ClientID %>');
    if (amt) amt.value = '';
    /* Clear detail */
    var det = document.getElementById('<%= txtTxDetail.ClientID %>');
    if (det) det.value = '';
    /* Open */
    openModal('modal-add-tx');
    /* Focus search after modal opens */
    setTimeout(function() { var inp = document.getElementById('acInput'); if (inp) inp.focus(); }, 150);
}

function autoFillDetail() {
    var ddlType = document.getElementById('<%= ddlTxTransType.ClientID %>');
    var ddlItem = document.getElementById('<%= ddlTxBillItem.ClientID %>');
    var det = document.getElementById('<%= txtTxDetail.ClientID %>');
    if (!ddlType || !ddlItem || !det) return;
    var typeTxt = ddlType.options[ddlType.selectedIndex] ? ddlType.options[ddlType.selectedIndex].text : '';
    var itemTxt = ddlItem.options[ddlItem.selectedIndex] ? ddlItem.options[ddlItem.selectedIndex].text : '';
    if (typeTxt && itemTxt && itemTxt !== '-- Select Item --') {
        det.value = typeTxt + ' - ' + itemTxt;
    }
}

function validateAndSaveTx() {
    var errors = [];
    var regno = StudentAC.getRegNo();
    var amount = document.getElementById('<%= txtTxAmount.ClientID %>').value.trim();
    var transType = document.getElementById('<%= ddlTxTransType.ClientID %>').value;
    var billItem = document.getElementById('<%= ddlTxBillItem.ClientID %>').value;
    var acadYear = document.getElementById('<%= ddlTxAcadYear.ClientID %>').value;
    var semester = document.getElementById('<%= ddlTxSemester.ClientID %>').value;
    var txDate = document.getElementById('<%= txtTxDate.ClientID %>').value.trim();
    var detail = document.getElementById('<%= txtTxDetail.ClientID %>').value.trim();

    if (!regno) errors.push('Please select a student from the search results.');
    if (!transType) errors.push('Transaction Type is required.');
    if (!billItem) errors.push('Billing Item is required.');
    if (!amount || isNaN(parseFloat(amount)) || parseFloat(amount) <= 0) errors.push('Amount must be a positive number.');
    if (!acadYear) errors.push('Academic Year is required.');
    if (!semester) errors.push('Semester is required.');
    if (!txDate) errors.push('Transaction Date is required.');
    if (!detail) errors.push('Description is required.');
    if (detail.length > 250) errors.push('Description must be 250 characters or less.');

    /* Write regno to hidden field before submit */
    var hf = document.getElementById('<%= hfSelectedRegNo.ClientID %>');
    if (hf) hf.value = regno;

    if (errors.length > 0) {
        alert(errors.join('\n'));
        return;
    }

    /* Prevent double-click */
    var saveBtn = document.getElementById('btnModalSave');
    if (saveBtn) { saveBtn.disabled = true; saveBtn.innerText = 'Saving...'; }

    document.getElementById('<%= btnSaveTransaction.ClientID %>').click();
}

/* ==== Row Action Popover ==== */
var _activeRowData = null;
var _deleteMode    = 'manual'; // 'manual' | 'gl'

// ---- Transaction details (click a row) ----
function ftEsc(s){ return (s==null?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
function ftDRow(label, val){
    return '<dt style="font-size:10.5px;color:#888;text-transform:uppercase;letter-spacing:.4px;padding:7px 0;border-bottom:1px solid #f0f2f5;">'+ftEsc(label)+'</dt>'
         + '<dd style="margin:0;font-size:12.5px;color:#1a1a2e;font-weight:600;padding:7px 0;border-bottom:1px solid #f0f2f5;word-break:break-word;">'+(val&&val.length?val:'&mdash;')+'</dd>';
}
function ftShowDetails(row){
    var d = {
        tid: row.getAttribute('data-tid'), regno: row.getAttribute('data-regno'), name: row.getAttribute('data-name'),
        type: row.getAttribute('data-type'), itemcode: row.getAttribute('data-itemcode'), itemname: row.getAttribute('data-itemname'),
        amount: row.getAttribute('data-amount'), amountfmt: row.getAttribute('data-amountfmt'), detail: row.getAttribute('data-detail'),
        status: row.getAttribute('data-status'), date: row.getAttribute('data-date'), year: row.getAttribute('data-year'),
        sem: row.getAttribute('data-sem'), source: row.getAttribute('data-source') || 'manual',
        createdby: row.getAttribute('data-createdby')
    };
    // Make the row's actions (edit / delete / remove-GL) available from the modal.
    _activeRowData = { tid:d.tid, regno:d.regno, name:d.name, type:d.type, itemcode:d.itemcode, amount:d.amount, detail:d.detail, status:d.status, date:d.date, year:d.year, sem:d.sem, source:d.source };

    document.getElementById('ftdTid').textContent = '#' + (d.tid||'');
    document.getElementById('ftdStudent').textContent = d.name || '(no name)';
    document.getElementById('ftdRegno').textContent = d.regno || '';

    var srcMap = {manual:'Manual entry', auto:'Auto (from GL)', ghost:'GL orphan (no manual row)', gl:'General Ledger'};
    var g = '';
    g += ftDRow('Type', ftEsc(d.type));
    g += ftDRow('Billing Item', ftEsc(d.itemname || d.itemcode));
    g += ftDRow('Amount', 'UGX ' + ftEsc(d.amountfmt || d.amount));
    g += ftDRow('Status', ftEsc(d.status));
    g += ftDRow('Description', ftEsc(d.detail));
    g += ftDRow('Date', ftEsc(d.date));
    g += ftDRow('Academic Year', ftEsc(d.year));
    g += ftDRow('Semester', ftEsc(d.sem));
    g += ftDRow('Created By', ftEsc(d.createdby));
    g += ftDRow('Source', ftEsc(srcMap[d.source] || d.source));
    g += ftDRow('Transaction ID', ftEsc(d.tid));
    document.getElementById('ftdGrid').innerHTML = g;

    var rq = encodeURIComponent(d.regno||''), tq = encodeURIComponent(d.type||'');
    var links = '<div style="font-size:10px;color:#999;text-transform:uppercase;letter-spacing:.4px;margin-bottom:4px;">Linked views</div>'
        + '<a href="FeesTransactions.aspx?q='+rq+'" style="display:block;font-size:12px;color:#174DA4;font-weight:600;text-decoration:none;padding:4px 0;">&rsaquo; All transactions for this student</a>'
        + '<a href="FeesTransactions.aspx?q='+rq+'&type='+tq+'" style="display:block;font-size:12px;color:#174DA4;font-weight:600;text-decoration:none;padding:4px 0;">&rsaquo; This student’s '+ftEsc(d.type||'')+' entries only</a>'
        + '<a href="FeesManagement.aspx" style="display:block;font-size:12px;color:#174DA4;font-weight:600;text-decoration:none;padding:4px 0;">&rsaquo; Open Fees Management</a>';
    document.getElementById('ftdLinks').innerHTML = links;

    var isManual = (d.source === 'manual'), a = '';
    if (isManual) {
        a += '<button type="button" onclick="ftCloseDetails();openEditTx();" style="padding:7px 14px;font-size:12px;font-weight:600;border:1px solid #174DA4;background:#fff;color:#174DA4;cursor:pointer;">Edit</button>';
        a += '<button type="button" onclick="ftCloseDetails();confirmDeleteTx();" style="padding:7px 14px;font-size:12px;font-weight:600;border:1px solid #c0392b;background:#fff;color:#c0392b;cursor:pointer;">Delete</button>';
    } else {
        if (FT_SUPER)
            a += '<button type="button" onclick="ftCloseDetails();openEditTx();" style="padding:7px 14px;font-size:12px;font-weight:600;border:1px solid #174DA4;background:#fff;color:#174DA4;cursor:pointer;">Edit GL entry</button>';
        a += '<button type="button" onclick="ftCloseDetails();confirmRemoveGL();" style="padding:7px 14px;font-size:12px;font-weight:600;border:1px solid #c0392b;background:#fff;color:#c0392b;cursor:pointer;">Remove from GL</button>';
    }
    a += '<button type="button" onclick="ftCloseDetails()" style="padding:7px 14px;font-size:12px;font-weight:600;border:1px solid #e0e5ed;background:#fff;color:#444;cursor:pointer;">Close</button>';
    document.getElementById('ftdActions').innerHTML = a;

    document.getElementById('ftDetailsOverlay').style.display = 'flex';
}
function ftCloseDetails(){ document.getElementById('ftDetailsOverlay').style.display = 'none'; }

function showRowAction(evt, btn) {
    evt.stopPropagation();
    evt.preventDefault();
    var pop = document.getElementById('actionPop');

    // Collect data from button attributes
    _activeRowData = {
        tid:      btn.getAttribute('data-tid'),
        regno:    btn.getAttribute('data-regno'),
        name:     btn.getAttribute('data-name'),
        type:     btn.getAttribute('data-type'),
        itemcode: btn.getAttribute('data-itemcode'),
        amount:   btn.getAttribute('data-amount'),
        detail:   btn.getAttribute('data-detail'),
        status:   btn.getAttribute('data-status'),
        date:     btn.getAttribute('data-date'),
        year:     btn.getAttribute('data-year'),
        sem:      btn.getAttribute('data-sem'),
        source:   btn.getAttribute('data-source') || 'manual'
    };

    // Show/hide actions based on source
    // Delete stays manual-only: removing a ledger row is what "Remove from GL" is for, and
    // it keeps its own confirmation. Only EDIT opens up, and only for a super administrator.
    var isManual = (_activeRowData.source === 'manual');
    var canEdit  = isManual || FT_SUPER;
    document.getElementById('popBtnEdit').style.display     = canEdit  ? '' : 'none';
    document.getElementById('popSepManual').style.display   = isManual ? '' : 'none';
    document.getElementById('popBtnDelete').style.display   = isManual ? '' : 'none';
    document.getElementById('popBtnRemoveGL').style.display = isManual ? 'none' : '';

    // Position using fixed coords (avoids clipping by overflow containers)
    var rect = btn.getBoundingClientRect();
    var popH = isManual ? 94 : (canEdit ? 78 : 46);
    var spaceBelow = window.innerHeight - rect.bottom;

    pop.style.left = Math.max(4, rect.left - 140) + 'px';
    if (spaceBelow < popH + 10) {
        pop.style.top = (rect.top - popH - 4) + 'px';
    } else {
        pop.style.top = (rect.bottom + 4) + 'px';
    }
    pop.classList.add('ft-action-pop--visible');
}

function hideRowAction() {
    var pop = document.getElementById('actionPop');
    if (pop) pop.classList.remove('ft-action-pop--visible');
}

// Close popover on outside click
document.addEventListener('click', function(e) {
    var pop = document.getElementById('actionPop');
    if (pop && pop.classList.contains('ft-action-pop--visible')) {
        if (!pop.contains(e.target) && !e.target.classList.contains('ft-row-action')) {
            hideRowAction();
        }
    }
});
// Close popover on scroll
window.addEventListener('scroll', hideRowAction, true);

/* ==== Edit Transaction ==== */
/* Whether this operator is a super administrator, as the server sees it. It decides what
   is OFFERED here; what is PERMITTED is decided again on the server at the point of the
   write, because a hidden button is not a control. */
var FT_SUPER = <%= IsSuperAdmin ? "true" : "false" %>;

/* A ledger-only row has no billing item, academic year, semester or post status — those
   columns do not exist on it. The modal puts them out of the way rather than asking for
   values that have nowhere to be stored. */
function ftApplyEditMode(source) {
    var gl = (source === 'auto' || source === 'ghost');
    var ids = ['<%= ddlEditBillItem.ClientID %>','<%= ddlEditAcadYear.ClientID %>',
               '<%= ddlEditSemester.ClientID %>','<%= ddlEditPostStatus.ClientID %>'];
    for (var i = 0; i < ids.length; i++) {
        var el = document.getElementById(ids[i]);
        if (!el) continue;
        var grp = el.closest ? el.closest('.fs-form-group') : null;
        if (grp) grp.style.display = gl ? 'none' : '';
        el.disabled = gl;
    }
    var banner = document.getElementById('editGlBanner');
    if (banner) banner.style.display = gl ? 'block' : 'none';
    var hdr = document.getElementById('editModalTitle');
    if (hdr) hdr.firstChild.nodeValue = gl ? 'Edit General Ledger Entry ' : 'Edit Transaction ';
}

function openEditTx() {
    hideRowAction();
    if (!_activeRowData) return;
    var d = _activeRowData;

    // TID badge
    document.getElementById('editTidBadge').textContent = '#' + d.tid;

    // Student info (read-only)
    var initials = _getInitials(d.name);
    document.getElementById('editStudentAvatar').textContent = initials;
    document.getElementById('editStudentName').textContent = d.name;
    document.getElementById('editStudentRegno').textContent = d.regno;

    // Hidden fields — the TID on its own is ambiguous, see the note beside them
    document.getElementById('<%= hfEditTID.ClientID %>').value = d.tid;
    document.getElementById('<%= hfEditSource.ClientID %>').value = d.source || 'manual';
    document.getElementById('<%= hfEditRegno.ClientID %>').value = d.regno || '';
    ftApplyEditMode(d.source || 'manual');

    // Set dropdowns
    _setSelect('<%= ddlEditTransType.ClientID %>', d.type);
    _setSelect('<%= ddlEditBillItem.ClientID %>', d.itemcode);
    _setSelect('<%= ddlEditAcadYear.ClientID %>', d.year);
    _setSelect('<%= ddlEditSemester.ClientID %>', d.sem);
    _setSelect('<%= ddlEditPostStatus.ClientID %>', d.status);

    // Text fields
    document.getElementById('<%= txtEditAmount.ClientID %>').value = d.amount;
    document.getElementById('<%= txtEditDate.ClientID %>').value = d.date;
    document.getElementById('<%= txtEditDetail.ClientID %>').value = d.detail;

    // Reset save button
    var btn = document.getElementById('btnModalEdit');
    if (btn) {
        btn.disabled = false;
        btn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg> Update Transaction';
    }

    openModal('modal-edit-tx');
}

function _setSelect(id, val) {
    var sel = document.getElementById(id);
    if (!sel) return;
    for (var i = 0; i < sel.options.length; i++) {
        if (sel.options[i].value == val) { sel.selectedIndex = i; return; }
    }
}

function _getInitials(name) {
    if (!name) return '?';
    var p = name.trim().split(/\s+/);
    if (p.length >= 2) return (p[0][0] + p[p.length-1][0]).toUpperCase();
    return p[0][0].toUpperCase();
}

function validateAndEditTx() {
    var errors = [];
    var _src = document.getElementById('<%= hfEditSource.ClientID %>').value || 'manual';
    var _gl  = (_src === 'auto' || _src === 'ghost');
    var amount = document.getElementById('<%= txtEditAmount.ClientID %>').value.trim();
    var transType = document.getElementById('<%= ddlEditTransType.ClientID %>').value;
    var billItem = document.getElementById('<%= ddlEditBillItem.ClientID %>').value;
    var acadYear = document.getElementById('<%= ddlEditAcadYear.ClientID %>').value;
    var semester = document.getElementById('<%= ddlEditSemester.ClientID %>').value;
    var txDate = document.getElementById('<%= txtEditDate.ClientID %>').value.trim();
    var detail = document.getElementById('<%= txtEditDetail.ClientID %>').value.trim();

    if (!transType) errors.push('Transaction Type is required.');
    if (!_gl && !billItem) errors.push('Billing Item is required.');
    if (!amount || isNaN(parseFloat(amount)) || parseFloat(amount) <= 0) errors.push('Amount must be a positive number.');
    if (!_gl && !acadYear) errors.push('Academic Year is required.');
    if (!_gl && !semester) errors.push('Semester is required.');
    if (!txDate) errors.push('Transaction Date is required.');
    if (!detail) errors.push('Description is required.');
    if (detail.length > 250) errors.push('Description must be 250 characters or less.');

    if (errors.length > 0) { alert(errors.join('\n')); return; }

    var btn = document.getElementById('btnModalEdit');
    if (btn) { btn.disabled = true; btn.innerText = 'Saving...'; }

    document.getElementById('<%= btnEditTransaction.ClientID %>').click();
}

/* ==== Delete Transaction ==== */
function _getSelectedReason() {
    var radios = document.querySelectorAll('input[name="delReasonCat"]');
    for (var i = 0; i < radios.length; i++) { if (radios[i].checked) return radios[i].value; }
    return '';
}

function _resetReasonFields() {
    var radios = document.querySelectorAll('input[name="delReasonCat"]');
    radios.forEach(function(r) {
        r.checked = false;
        r.closest('.ft-reason-option').classList.remove('ft-reason-option--selected');
    });
    var opts = document.getElementById('delReasonOptions');
    if (opts) opts.classList.remove('ft-reason-options--invalid');
    var txt = document.getElementById('delReasonExplanation');
    if (txt) txt.value = '';
    var err = document.getElementById('delReasonError');
    if (err) err.style.display = 'none';
}

// Highlight selected radio card
document.addEventListener('change', function(e) {
    if (e.target && e.target.name === 'delReasonCat') {
        document.querySelectorAll('input[name="delReasonCat"]').forEach(function(r) {
            r.closest('.ft-reason-option').classList.toggle('ft-reason-option--selected', r.checked);
        });
        var opts = document.getElementById('delReasonOptions');
        if (opts) opts.classList.remove('ft-reason-options--invalid');
        var err = document.getElementById('delReasonError');
        if (err) err.style.display = 'none';
    }
});

function confirmDeleteTx() {
    hideRowAction();
    if (!_activeRowData) return;
    var d = _activeRowData;
    _deleteMode = 'manual';

    document.getElementById('<%= hfDeleteTID.ClientID %>').value = d.tid;
    _resetReasonFields();

    var dl = document.getElementById('deleteDetail');
    dl.innerHTML = '<dt>Transaction ID</dt><dd>#' + _esc(d.tid) + '</dd>'
        + '<dt>Student</dt><dd>' + _esc(d.name) + ' (' + _esc(d.regno) + ')</dd>'
        + '<dt>Type</dt><dd>' + _esc(d.type) + '</dd>'
        + '<dt>Amount</dt><dd>UGX ' + _fmtNum(d.amount) + '</dd>'
        + '<dt>Date</dt><dd>' + _esc(d.date) + '</dd>';

    var btn = document.getElementById('btnConfirmDelete');
    if (btn) {
        btn.disabled = false;
        btn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:middle;margin-right:4px;"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg> Delete';
    }

    document.getElementById('deleteConfirm').classList.add('ft-confirm-overlay--visible');
}

function confirmRemoveGL() {
    hideRowAction();
    if (!_activeRowData) return;
    var d = _activeRowData;
    _deleteMode = 'gl';

    document.getElementById('<%= hfRemoveGLTID.ClientID %>').value = d.tid;
    _resetReasonFields();

    var sourceLabel = d.source === 'ghost'
        ? 'Ghost (deleted from tracking but lingering in GL)'
        : 'AUTO (system-generated billing entry)';

    var dl = document.getElementById('deleteDetail');
    dl.innerHTML = '<dt>GL Row ID</dt><dd>#' + _esc(d.tid) + '</dd>'
        + '<dt>Student</dt><dd>' + _esc(d.name) + ' (' + _esc(d.regno) + ')</dd>'
        + '<dt>Type</dt><dd>' + _esc(d.type) + ' &mdash; ' + sourceLabel + '</dd>'
        + '<dt>Amount</dt><dd>UGX ' + _fmtNum(d.amount) + '</dd>'
        + '<dt>Description</dt><dd>' + _esc(d.detail) + '</dd>';

    var btn = document.getElementById('btnConfirmDelete');
    if (btn) {
        btn.disabled = false;
        btn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:middle;margin-right:4px;"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg> Remove from GL';
    }

    document.getElementById('deleteConfirm').classList.add('ft-confirm-overlay--visible');
}

function closeDeleteConfirm() {
    document.getElementById('deleteConfirm').classList.remove('ft-confirm-overlay--visible');
    _resetReasonFields();
}

function doDeleteTx() {
    var btn = document.getElementById('btnConfirmDelete');
    if (btn) { btn.disabled = true; btn.innerText = 'Deleting...'; }
    document.getElementById('<%= btnDeleteTransaction.ClientID %>').click();
}

function doRemoveGL() {
    var btn = document.getElementById('btnConfirmDelete');
    if (btn) { btn.disabled = true; btn.innerText = 'Removing...'; }
    document.getElementById('<%= btnRemoveFromGL.ClientID %>').click();
}

function doConfirmAction() {
    // Validate a reason radio is selected
    var chosen = _getSelectedReason();
    var err = document.getElementById('delReasonError');
    var opts = document.getElementById('delReasonOptions');
    if (!chosen) {
        if (opts) opts.classList.add('ft-reason-options--invalid');
        if (err) err.style.display = 'block';
        return;
    }
    if (err) err.style.display = 'none';
    if (opts) opts.classList.remove('ft-reason-options--invalid');

    // Copy values to hidden fields for server-side capture
    var txt = document.getElementById('delReasonExplanation');
    document.getElementById('<%= hfDeleteCategory.ClientID %>').value = chosen;
    document.getElementById('<%= hfDeleteExplanation.ClientID %>').value = txt ? txt.value : '';

    if (_deleteMode === 'gl') doRemoveGL();
    else doDeleteTx();
}

function _fmtNum(x) { if (!x) return '0'; return parseFloat(x).toLocaleString(); }
function _esc(str) { var d = document.createElement('div'); d.appendChild(document.createTextNode(str || '')); return d.innerHTML; }

/* ==== GET-driven filters + pagination (no postback) ==== */
function _ftv(id){ var el=document.getElementById(id); return el ? (el.value||'') : ''; }
// Build the canonical query string from the current filter controls and navigate (GET).
function ftApply() {
    var p = new URLSearchParams();
    function s(k,v){ v=(v==null?'':(''+v)).trim(); if(v!=='') p.set(k,v); }
    s('acad',       _ftv('<%= ddlAcadYear.ClientID %>'));
    s('sem',        _ftv('<%= ddlSemester.ClientID %>'));
    s('type',       _ftv('<%= ddlTransType.ClientID %>'));
    s('item',       _ftv('<%= ddlBillItem.ClientID %>'));
    s('post',       _ftv('<%= ddlPostStatus.ClientID %>'));
    s('studstatus', _ftv('<%= ddlStudStatus.ClientID %>'));
    s('source',     _ftv('<%= ddlSource.ClientID %>'));
    var sz = _ftv('<%= ddlPageSize.ClientID %>'); if (sz && sz !== '50') p.set('size', sz);
    var q  = _ftv('<%= txtSearch.ClientID %>').trim(); if (q) p.set('q', q);
    p.set('page', '1');   // any filter/search change returns to page 1
    window.location.href = window.location.pathname + '?' + p.toString();
}
function ftReset() { window.location.href = window.location.pathname; }
// Pager buttons pass a 0-based index; the URL carries a 1-based page.
function goToPage(idx) {
    idx = parseInt(idx, 10); if (isNaN(idx) || idx < 0) return;
    var p = new URLSearchParams(window.location.search);
    p.set('page', (idx + 1).toString());
    window.location.href = window.location.pathname + '?' + p.toString();
}

/* ================================================================
   GL SYNC - Scan & Fix orphan tracking rows / wrong account_type
   Powered by AJAX endpoints:
     ?ajax=glsync_scan  (read-only detection)
     ?ajax=glsync_fix   (write: insert missing + normalise)
   ================================================================ */

var _glState = { scanning: false, fixing: false, orphanCount: 0, wrongCount: 0 };

function openGLSyncModal() {
    _glResetUI();
    openModal('modal-glsync');
    // Auto-scan on open
    setTimeout(glSyncScan, 200);
}

function _glResetUI() {
    _glState = { scanning: false, fixing: false, orphanCount: 0, wrongCount: 0 };
    _glId('glOrphanCount').textContent = '\u2014';
    _glId('glWrongTypeCount').textContent = '\u2014';
    _glId('glProgress').style.display = 'none';
    _glId('glProgressBar').style.width = '0%';
    _glId('glResult').style.display = 'none';
    _glId('glOrphanSample').style.display = 'none';
    _glId('glWrongSample').style.display = 'none';
    _glId('glLog').style.display = 'none';
    _glId('glLog').innerHTML = '';
    _glId('glOrphanTableBody').innerHTML = '';
    _glId('glWrongTableBody').innerHTML = '';
    _glId('btnGLFix').disabled = true;
    _glId('btnGLScan').disabled = false;
}

function _glId(id) { return document.getElementById(id); }

function _glLog(msg, cls) {
    var log = _glId('glLog');
    log.style.display = 'block';
    var p = document.createElement('p');
    p.className = 'gl-log__line gl-log__line--' + (cls || 'info');
    p.textContent = '[' + new Date().toLocaleTimeString() + '] ' + msg;
    log.appendChild(p);
    log.scrollTop = log.scrollHeight;
}

function glSyncScan() {
    if (_glState.scanning || _glState.fixing) return;
    _glResetUI();
    _glState.scanning = true;
    _glId('btnGLScan').disabled = true;

    _glId('glProgress').style.display = 'block';
    _glId('glProgressBar').style.width = '30%';
    _glId('glProgressText').textContent = 'Scanning for anomalies...';
    _glLog('Starting GL sync scan...', 'info');

    var url = window.location.pathname + '?ajax=glsync_scan&_t=' + Date.now();
    var xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.timeout = 180000; // 3 min
    xhr.onload = function () {
        _glState.scanning = false;
        _glId('glProgressBar').style.width = '100%';
        try {
            var d = JSON.parse(xhr.responseText);
            if (!d.ok) { _glLog('ERROR: ' + (d.error || 'Unknown'), 'err'); return; }

            _glState.orphanCount = d.orphanCount;
            _glState.wrongCount = d.wrongTypeCount;

            _glId('glOrphanCount').textContent = _fmtNum(d.orphanCount);
            _glId('glWrongTypeCount').textContent = _fmtNum(d.wrongTypeCount);

            _glLog('Scan complete.', 'ok');
            _glLog('  Orphan tracking rows: ' + d.orphanCount, d.orphanCount > 0 ? 'warn' : 'ok');
            _glLog('  Wrong account_type:   ' + d.wrongTypeCount, d.wrongTypeCount > 0 ? 'warn' : 'ok');

            // Show sample tables
            if (d.orphanSample && d.orphanSample.length > 0) {
                var tbody = _glId('glOrphanTableBody');
                tbody.innerHTML = '';
                d.orphanSample.forEach(function (r) {
                    var badge = r.type === 'Payment' ? 'gl-badge-cr' : 'gl-badge-dr';
                    tbody.innerHTML += '<tr><td>' + r.tid + '</td><td>' + _esc(r.regno) + '</td>' +
                        '<td><span class="' + badge + '">' + _esc(r.type) + '</span></td>' +
                        '<td style="text-align:right;">' + _fmtNum(r.amount) + '</td>' +
                        '<td>' + _esc(r.date) + '</td><td>' + _esc(r.year) + '</td><td>' + r.sem + '</td></tr>';
                });
                _glId('glOrphanSample').style.display = 'block';
            }

            if (d.wrongTypeSample && d.wrongTypeSample.length > 0) {
                var tbody2 = _glId('glWrongTableBody');
                tbody2.innerHTML = '';
                d.wrongTypeSample.forEach(function (r) {
                    tbody2.innerHTML += '<tr><td>' + r.tid + '</td><td>' + _esc(r.regno) + '</td>' +
                        '<td><span style="color:#7b1fa2;font-weight:600;">' + _esc(r.wrongType) + '</span></td>' +
                        '<td>' + r.txType + '</td>' +
                        '<td style="text-align:right;">' + _fmtNum(r.amount) + '</td>' +
                        '<td>' + _esc(r.date) + '</td></tr>';
                });
                _glId('glWrongSample').style.display = 'block';
            }

            var total = d.orphanCount + d.wrongTypeCount;
            if (total > 0) {
                _glId('btnGLFix').disabled = false;
                _glId('glProgressText').textContent = total + ' issue(s) detected - ready to fix.';
                _glLog(total + ' issue(s) found. Click "Apply Fix" to repair.', 'warn');
            } else {
                _glId('glProgressText').textContent = 'All clear - no issues found.';
                _glId('glResult').className = 'gl-result gl-result--none';
                _glId('glResult').innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px;margin-right:4px;"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg> All GL entries are in sync. No action needed.';
                _glId('glResult').style.display = 'block';
            }
            _glId('btnGLScan').disabled = false;
        } catch (e) {
            _glLog('Parse error: ' + e.message, 'err');
            _glId('btnGLScan').disabled = false;
        }
    };
    xhr.onerror = function () {
        _glState.scanning = false;
        _glLog('Network error during scan.', 'err');
        _glId('glProgressText').textContent = 'Scan failed.';
        _glId('btnGLScan').disabled = false;
    };
    xhr.ontimeout = function () {
        _glState.scanning = false;
        _glLog('Scan timed out (>3 min). Try again.', 'err');
        _glId('btnGLScan').disabled = false;
    };
    xhr.send();
}


function glSyncFix() {
    if (_glState.fixing || _glState.scanning) return;
    var total = _glState.orphanCount + _glState.wrongCount;
    if (total === 0) { _glLog('Nothing to fix.', 'info'); return; }

    if (!confirm('This will INSERT ' + _fmtNum(_glState.orphanCount) + ' missing GL row(s) and normalise ' +
        _fmtNum(_glState.wrongCount) + ' account_type value(s).\n\nThis is safe (idempotent, transactional). Proceed?')) return;

    _glState.fixing = true;
    _glId('btnGLFix').disabled = true;
    _glId('btnGLScan').disabled = true;
    _glId('glProgress').style.display = 'block';
    _glId('glProgressBar').style.width = '0%';
    _glId('glResult').style.display = 'none';

    _glLog('Applying GL sync fix...', 'info');
    _glLog('  Step 1/2: Inserting ' + _glState.orphanCount + ' missing GL entries...', 'info');
    _glId('glProgressBar').style.width = '20%';
    _glId('glProgressText').textContent = 'Inserting missing GL entries...';

    var url = window.location.pathname + '?ajax=glsync_fix&_t=' + Date.now();
    var xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.timeout = 600000; // 10 min for large fixes

    // Simulate progress while waiting
    var pct = 20;
    var progInterval = setInterval(function () {
        if (pct < 85) {
            pct += Math.random() * 3;
            _glId('glProgressBar').style.width = pct + '%';
            if (pct > 50) _glId('glProgressText').textContent = 'Normalising account types...';
        }
    }, 800);

    xhr.onload = function () {
        clearInterval(progInterval);
        _glState.fixing = false;
        _glId('glProgressBar').style.width = '100%';

        try {
            var d = JSON.parse(xhr.responseText);
            if (!d.ok) {
                _glLog('FIX FAILED: ' + (d.error || 'Unknown error'), 'err');
                _glId('glProgressText').textContent = 'Fix failed - see log.';
                _glId('glResult').className = 'gl-result gl-result--err';
                _glId('glResult').textContent = 'Fix failed: ' + (d.error || 'Unknown');
                _glId('glResult').style.display = 'block';
                _glId('btnGLScan').disabled = false;
                return;
            }

            _glLog('  Step 1/2 complete: ' + d.inserted + ' GL row(s) inserted.', 'ok');
            _glLog('  Step 2/2 complete: ' + d.normalised + ' account_type(s) normalised.', 'ok');
            _glLog('Fix applied successfully!', 'ok');

            _glId('glProgressText').textContent = 'Fix complete!';
            _glId('glResult').className = 'gl-result gl-result--ok';
            _glId('glResult').innerHTML =
                '<strong>Fix applied successfully</strong><br>' +
                '<span style="font-variant-numeric:tabular-nums;">' +
                d.inserted + '</span> missing GL row(s) inserted &nbsp;&bull;&nbsp; ' +
                '<span style="font-variant-numeric:tabular-nums;">' +
                d.normalised + '</span> account_type(s) normalised to \'Student\'';
            _glId('glResult').style.display = 'block';

            // Update KPI to zero after fix
            _glId('glOrphanCount').textContent = '0';
            _glId('glWrongTypeCount').textContent = '0';
            _glId('btnGLFix').disabled = true;
            _glId('btnGLScan').disabled = false;
        } catch (e) {
            _glLog('Response parse error: ' + e.message, 'err');
            _glId('btnGLScan').disabled = false;
        }
    };
    xhr.onerror = function () {
        clearInterval(progInterval);
        _glState.fixing = false;
        _glLog('Network error during fix.', 'err');
        _glId('glProgressText').textContent = 'Fix failed - network error.';
        _glId('btnGLScan').disabled = false;
    };
    xhr.ontimeout = function () {
        clearInterval(progInterval);
        _glState.fixing = false;
        _glLog('Fix timed out (>10 min). Re-scan to check status.', 'err');
        _glId('btnGLScan').disabled = false;
    };
    xhr.send();
}
</script>

<script type="text/javascript">
/* ================================================================
   BATCH DOUBLE-BILLING FIX — 3-Step Admin Wizard
   Step 1: Detect all affected student accounts (system-wide scan)
   Step 2: Fix each account one-by-one with live AJAX progress
   Step 3: Summary results
   AJAX endpoints:
     ?ajax=batchdup_scan              (detect all affected accounts)
     ?ajax=batchdup_fix_one&regno=X   (fix one student)
   ================================================================ */

var _bd = {
    scanning: false,
    fixing: false,
    step: 1,
    accounts: [],       // from scan
    results: [],        // per-account fix results
    totalFixed: 0,
    totalDeleted: 0,
    totalSkipped: 0,
    totalErrors: 0,
    fixIndex: 0,
    aborted: false
};

function openBatchDupModal() {
    _bdReset();
    openModal('modal-batchdup');
    setTimeout(bdScan, 300);
}

function closeBatchDupModal() {
    if (_bd.fixing && !_bd.aborted) {
        if (!confirm('A batch fix is in progress. Closing will stop processing remaining accounts.\n\nAccounts already fixed will keep their changes.\n\nClose anyway?')) return;
        _bd.aborted = true;
    }
    closeModal('modal-batchdup');
}

function _bdReset() {
    _bd = { scanning:false, fixing:false, step:1, accounts:[], results:[], totalFixed:0, totalDeleted:0, totalSkipped:0, totalErrors:0, fixIndex:0, aborted:false };
    _bdSetStep(1);
    _bdId('bdAffected').textContent = '\u2014';
    _bdId('bdTotalDups').textContent = '\u2014';
    _bdId('bdTotalAmt').textContent = '\u2014';
    _bdId('bdIndexStatus').textContent = '\u2014';
    _bdId('bdAcctWrap').style.display = 'none';
    _bdId('bdAcctBody').innerHTML = '';
    _bdId('bdScanResult').style.display = 'none';
    _bdId('bdScanResult').innerHTML = '';
    _bdId('bdFixBar').style.width = '0%';
    _bdId('bdFixCounter').textContent = '0 / 0';
    _bdId('bdFixText').textContent = 'Waiting to start...';
    _bdId('bdFixCurrent').textContent = '';
    _bdId('bdFixBody').innerHTML = '';
    _bdId('bdLog').style.display = 'none';
    _bdId('bdLog').innerHTML = '';
    _bdId('btnBdScan').disabled = false;
    _bdId('btnBdFix').disabled = true;
    _bdId('bdResultBanner').innerHTML = '';
    _bdId('bdResFixed').textContent = '0';
    _bdId('bdResDeleted').textContent = '0';
    _bdId('bdResSkipped').textContent = '0';
    _bdId('bdResErrors').textContent = '0';
}

function _bdId(id) { return document.getElementById(id); }
function _bdFmt(x) { if (!x && x !== 0) return '0'; return parseFloat(x).toLocaleString(); }
function _bdEsc(s) { var d = document.createElement('div'); d.appendChild(document.createTextNode(s || '')); return d.innerHTML; }

function _bdLog(msg, cls) {
    var log = _bdId('bdLog');
    log.style.display = 'block';
    var p = document.createElement('p');
    p.className = 'l-' + (cls || 'info');
    p.textContent = '[' + new Date().toLocaleTimeString() + '] ' + msg;
    log.appendChild(p);
    log.scrollTop = log.scrollHeight;
}

function _bdSetStep(n) {
    _bd.step = n;
    for (var i = 1; i <= 3; i++) {
        var ind = _bdId('bdStep' + i + 'Ind');
        var num = _bdId('bdStep' + i + 'Num');
        var panel = _bdId('bdPanel' + i);
        ind.className = 'bd-step';
        panel.className = 'bd-panel';
        if (i < n) {
            ind.className = 'bd-step bd-step--done';
            num.innerHTML = '\u2713';
        } else if (i === n) {
            ind.className = 'bd-step bd-step--active';
            num.textContent = i;
            panel.className = 'bd-panel bd-panel--active';
        } else {
            num.textContent = i;
        }
    }
    // Lines
    _bdId('bdLine1').style.background = n > 1 ? '#16a34a' : '#e0e5ed';
    _bdId('bdLine2').style.background = n > 2 ? '#16a34a' : '#e0e5ed';
}

/* ── STEP 1: SCAN ── */
function bdScan() {
    if (_bd.scanning || _bd.fixing) return;
    _bdReset();
    _bd.scanning = true;
    _bdId('btnBdScan').disabled = true;
    _bdLog('Starting system-wide double-billing scan...', 'info');
    _bdLog('  4-method detection: tracking_ref, folio, GLSync, BATCH', 'info');

    var url = window.location.pathname + '?ajax=batchdup_scan&_t=' + Date.now();
    var xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.timeout = 300000;
    xhr.onload = function () {
        _bd.scanning = false;
        try {
            var d = JSON.parse(xhr.responseText);
            if (!d.ok) { _bdLog('ERROR: ' + (d.error || 'Unknown'), 'err'); _bdId('btnBdScan').disabled = false; return; }

            _bd.accounts = d.accounts || [];

            // Update KPIs
            _bdId('bdAffected').textContent = _bdFmt(d.affectedCount);
            _bdId('bdTotalDups').textContent = _bdFmt(d.grandTotalDups);
            _bdId('bdTotalAmt').textContent = d.grandTotalAmount > 0 ? 'UGX ' + _bdFmt(d.grandTotalAmount) : '0';
            _bdId('bdIndexStatus').textContent = d.uniqueIndexActive ? '\u2713 Active' : '\u2717 Missing';
            _bdId('bdIndexStatus').style.color = d.uniqueIndexActive ? '#16a34a' : '#dc3545';

            _bdLog('Scan complete.', 'ok');
            _bdLog('  Total student accounts:  ' + _bdFmt(d.totalStudents), 'info');
            _bdLog('  Total ledger entries:    ' + _bdFmt(d.totalLedger), 'info');
            _bdLog('  Affected accounts:       ' + d.affectedCount, d.affectedCount > 0 ? 'warn' : 'ok');
            _bdLog('  Duplicate entries:       ' + _bdFmt(d.grandTotalDups), d.grandTotalDups > 0 ? 'warn' : 'ok');
            _bdLog('  Over-billed amount:      UGX ' + _bdFmt(d.grandTotalAmount), d.grandTotalAmount > 0 ? 'warn' : 'ok');
            _bdLog('  UNIQUE index:            ' + (d.uniqueIndexActive ? 'Active' : 'MISSING!'), d.uniqueIndexActive ? 'ok' : 'err');

            // Build accounts table
            if (_bd.accounts.length > 0) {
                var tbody = _bdId('bdAcctBody');
                tbody.innerHTML = '';
                for (var i = 0; i < _bd.accounts.length; i++) {
                    var a = _bd.accounts[i];
                    tbody.innerHTML += '<tr id="bdRow_' + i + '">' +
                        '<td>' + (i + 1) + '</td>' +
                        '<td style="font-weight:600;">' + _bdEsc(a.regno) + '</td>' +
                        '<td>' + _bdEsc(a.name) + '</td>' +
                        '<td class="r">' + _bdFmt(a.dupCount) + '</td>' +
                        '<td class="r">' + _bdFmt(a.dupAmount) + '</td>' +
                        '<td class="bd-status-cell"><span class="bd-tag bd-tag--pending">Pending</span></td></tr>';
                }
                _bdId('bdAcctWrap').style.display = 'block';
                _bdId('btnBdFix').disabled = false;
                _bdLog(_bd.accounts.length + ' account(s) ready to fix. Click "Fix All" to proceed.', 'warn');
            } else {
                _bdId('bdScanResult').className = 'gl-result gl-result--none';
                _bdId('bdScanResult').innerHTML =
                    '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px;margin-right:4px;"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>' +
                    '<strong>All Clear</strong> \u2014 No duplicate billing detected across ' + _bdFmt(d.totalStudents) + ' student accounts.';
                _bdId('bdScanResult').style.display = 'block';
                _bdLog('No duplicates found system-wide. All accounts are clean.', 'ok');
            }
            _bdId('btnBdScan').disabled = false;
        } catch (e) {
            _bdLog('Parse error: ' + e.message, 'err');
            _bdId('btnBdScan').disabled = false;
        }
    };
    xhr.onerror = function () { _bd.scanning = false; _bdLog('Network error.', 'err'); _bdId('btnBdScan').disabled = false; };
    xhr.ontimeout = function () { _bd.scanning = false; _bdLog('Scan timed out.', 'err'); _bdId('btnBdScan').disabled = false; };
    xhr.send();
}

/* ── STEP 2: FIX ALL (sequential one-by-one) ── */
function bdStartFix() {
    if (_bd.fixing || _bd.scanning) return;
    if (_bd.accounts.length === 0) { _bdLog('Nothing to fix.', 'info'); return; }

    var msg = 'This will fix double billing for ' + _bd.accounts.length + ' student account(s).\n\n' +
        'Each account will be processed one at a time.\n' +
        'Deleted entries are archived to fin_deleted_ledger for safety.\n\n' +
        'Proceed?';
    if (!confirm(msg)) return;

    _bd.fixing = true;
    _bd.aborted = false;
    _bd.fixIndex = 0;
    _bd.totalFixed = 0;
    _bd.totalDeleted = 0;
    _bd.totalSkipped = 0;
    _bd.totalErrors = 0;
    _bd.results = [];

    _bdId('btnBdFix').disabled = true;
    _bdId('btnBdScan').disabled = true;

    _bdSetStep(2);

    // Prepare fix table body
    var tbody = _bdId('bdFixBody');
    tbody.innerHTML = '';
    for (var i = 0; i < _bd.accounts.length; i++) {
        var a = _bd.accounts[i];
        tbody.innerHTML += '<tr id="bdFixRow_' + i + '">' +
            '<td>' + (i + 1) + '</td>' +
            '<td style="font-weight:600;">' + _bdEsc(a.regno) + '</td>' +
            '<td>' + _bdEsc(a.name) + '</td>' +
            '<td class="r" id="bdFixDel_' + i + '">-</td>' +
            '<td id="bdFixBefore_' + i + '">-</td>' +
            '<td id="bdFixAfter_' + i + '">-</td>' +
            '<td class="bd-status-cell" id="bdFixSt_' + i + '"><span class="bd-tag bd-tag--pending">Pending</span></td></tr>';
    }

    _bdLog('Starting batch fix for ' + _bd.accounts.length + ' account(s)...', 'info');
    _bdFixNext();
}

function _bdFixNext() {
    if (_bd.aborted) {
        _bdLog('Batch fix aborted by user.', 'warn');
        _bdFinalize();
        return;
    }

    if (_bd.fixIndex >= _bd.accounts.length) {
        _bdFinalize();
        return;
    }

    var idx = _bd.fixIndex;
    var acct = _bd.accounts[idx];
    var total = _bd.accounts.length;
    var pct = ((idx) / total * 100).toFixed(1);

    _bdId('bdFixBar').style.width = pct + '%';
    _bdId('bdFixCounter').textContent = (idx + 1) + ' / ' + total;
    _bdId('bdFixText').textContent = 'Fixing ' + acct.regno + '...';
    _bdId('bdFixCurrent').textContent = '\u25B6 ' + acct.regno + (acct.name ? ' \u2014 ' + acct.name : '');

    // Update status in both tables
    var scanRow = _bdId('bdRow_' + idx);
    if (scanRow) { var st = scanRow.querySelector('.bd-status-cell'); if (st) st.innerHTML = '<span class="bd-tag bd-tag--fixing">Fixing...</span>'; }
    _bdId('bdFixSt_' + idx).innerHTML = '<span class="bd-tag bd-tag--fixing">Fixing...</span>';

    // Scroll fix table row into view
    var fixRow = _bdId('bdFixRow_' + idx);
    if (fixRow) fixRow.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

    _bdLog('  [' + (idx + 1) + '/' + total + '] Fixing ' + acct.regno + '...', 'info');

    var url = window.location.pathname + '?ajax=batchdup_fix_one&regno=' + encodeURIComponent(acct.regno) + '&_t=' + Date.now();
    var xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.timeout = 120000;
    xhr.onload = function () {
        try {
            var d = JSON.parse(xhr.responseText);
            _bd.results.push(d);

            if (!d.ok) {
                _bd.totalErrors++;
                _bdId('bdFixSt_' + idx).innerHTML = '<span class="bd-tag bd-tag--error">Error</span>';
                if (scanRow) { var st = scanRow.querySelector('.bd-status-cell'); if (st) st.innerHTML = '<span class="bd-tag bd-tag--error">Error</span>'; }
                _bdLog('    \u2717 Error: ' + (d.error || 'Unknown'), 'err');
            } else if (d.deleted === 0) {
                _bd.totalSkipped++;
                _bdId('bdFixSt_' + idx).innerHTML = '<span class="bd-tag bd-tag--done">Clean</span>';
                _bdId('bdFixDel_' + idx).textContent = '0';
                _bdId('bdFixBefore_' + idx).textContent = d.balBefore || '-';
                _bdId('bdFixAfter_' + idx).textContent = d.balAfter || '-';
                if (scanRow) { var st = scanRow.querySelector('.bd-status-cell'); if (st) st.innerHTML = '<span class="bd-tag bd-tag--done">Clean</span>'; }
                _bdLog('    \u2713 Already clean (0 duplicates). Balance: ' + (d.balAfter || '-'), 'ok');
            } else {
                _bd.totalFixed++;
                _bd.totalDeleted += d.deleted;
                _bdId('bdFixSt_' + idx).innerHTML = '<span class="bd-tag bd-tag--done">\u2713 Fixed</span>';
                _bdId('bdFixDel_' + idx).textContent = d.deleted;
                _bdId('bdFixDel_' + idx).style.color = '#dc3545';
                _bdId('bdFixDel_' + idx).style.fontWeight = '700';
                _bdId('bdFixBefore_' + idx).textContent = d.balBefore || '-';
                _bdId('bdFixAfter_' + idx).textContent = d.balAfter || '-';
                if (scanRow) { var st = scanRow.querySelector('.bd-status-cell'); if (st) st.innerHTML = '<span class="bd-tag bd-tag--done">\u2713 Fixed</span>'; }
                _bdLog('    \u2713 Deleted ' + d.deleted + ' duplicate(s). M1=' + d.m1 + ' M2=' + d.m2 + ' M3=' + d.m3 + ' M4=' + d.m4 + '. Balance: ' + d.balBefore + ' \u2192 ' + d.balAfter, 'ok');
            }
        } catch (e) {
            _bd.totalErrors++;
            _bdId('bdFixSt_' + idx).innerHTML = '<span class="bd-tag bd-tag--error">Parse Err</span>';
            _bdLog('    \u2717 Parse error: ' + e.message, 'err');
        }

        _bd.fixIndex++;
        // Small delay between requests to avoid hammering the server
        setTimeout(_bdFixNext, 150);
    };
    xhr.onerror = function () {
        _bd.totalErrors++;
        _bdId('bdFixSt_' + idx).innerHTML = '<span class="bd-tag bd-tag--error">Network</span>';
        _bdLog('    \u2717 Network error for ' + acct.regno, 'err');
        _bd.fixIndex++;
        setTimeout(_bdFixNext, 500);
    };
    xhr.ontimeout = function () {
        _bd.totalErrors++;
        _bdId('bdFixSt_' + idx).innerHTML = '<span class="bd-tag bd-tag--error">Timeout</span>';
        _bdLog('    \u2717 Timeout for ' + acct.regno, 'err');
        _bd.fixIndex++;
        setTimeout(_bdFixNext, 500);
    };
    xhr.send();
}

/* ── STEP 3: FINALIZE ── */
function _bdFinalize() {
    _bd.fixing = false;

    _bdId('bdFixBar').style.width = '100%';
    _bdId('bdFixText').textContent = _bd.aborted ? 'Batch fix aborted.' : 'Batch fix complete!';
    _bdId('bdFixCurrent').textContent = '';
    _bdId('bdFixCounter').textContent = _bd.fixIndex + ' / ' + _bd.accounts.length;

    _bdLog('', 'info');
    _bdLog('========================================', 'ok');
    _bdLog('  BATCH FIX COMPLETE', 'ok');
    _bdLog('  Accounts processed: ' + _bd.fixIndex + ' / ' + _bd.accounts.length, 'info');
    _bdLog('  Fixed:     ' + _bd.totalFixed, _bd.totalFixed > 0 ? 'ok' : 'info');
    _bdLog('  Deleted:   ' + _bd.totalDeleted + ' duplicate entries', _bd.totalDeleted > 0 ? 'ok' : 'info');
    _bdLog('  Clean:     ' + _bd.totalSkipped, 'info');
    _bdLog('  Errors:    ' + _bd.totalErrors, _bd.totalErrors > 0 ? 'err' : 'info');
    _bdLog('========================================', 'ok');
    if (_bd.aborted) _bdLog('  Note: Batch was aborted. ' + (_bd.accounts.length - _bd.fixIndex) + ' account(s) were not processed.', 'warn');
    _bdLog('  All deleted entries archived to fin_deleted_ledger.', 'info');

    // Move to step 3
    _bdSetStep(3);

    // Fill results
    _bdId('bdResFixed').textContent = _bdFmt(_bd.totalFixed);
    _bdId('bdResDeleted').textContent = _bdFmt(_bd.totalDeleted);
    _bdId('bdResSkipped').textContent = _bdFmt(_bd.totalSkipped);
    _bdId('bdResErrors').textContent = _bdFmt(_bd.totalErrors);

    var banner = _bdId('bdResultBanner');
    if (_bd.totalErrors > 0) {
        banner.className = 'bd-result-banner bd-result-banner--err';
        banner.innerHTML = '<strong>Batch fix completed with errors.</strong> ' +
            _bd.totalFixed + ' account(s) fixed, ' + _bd.totalErrors + ' error(s). ' +
            'Review the log for details.';
    } else if (_bd.totalFixed === 0 && _bd.totalSkipped > 0) {
        banner.className = 'bd-result-banner bd-result-banner--none';
        banner.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px;margin-right:4px;"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>' +
            '<strong>All accounts already clean.</strong> No duplicates were found during the fix pass.';
    } else if (_bd.aborted) {
        banner.className = 'bd-result-banner bd-result-banner--err';
        banner.innerHTML = '<strong>Batch fix was aborted.</strong> ' +
            _bd.fixIndex + ' of ' + _bd.accounts.length + ' account(s) were processed. ' +
            _bd.totalFixed + ' fixed, ' + _bd.totalDeleted + ' entries removed.';
    } else {
        banner.className = 'bd-result-banner bd-result-banner--ok';
        banner.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px;margin-right:4px;"><polyline points="20 6 9 17 4 12"></polyline></svg>' +
            '<strong>Batch fix completed successfully!</strong> ' +
            _bd.totalFixed + ' account(s) fixed, ' + _bdFmt(_bd.totalDeleted) + ' duplicate entries removed. ' +
            'All deleted entries archived to fin_deleted_ledger.';
    }

    _bdId('btnBdScan').disabled = false;
    _bdId('btnBdFix').disabled = true;
}
</script>

<!-- ============= ADD TRANSACTION MODAL ============= -->
<div id="modal-add-tx" class="fs-modal-overlay">
<div class="fs-modal">
    <div class="fs-modal__header">
        <div class="fs-modal__title">New Fee Transaction</div>
        <button type="button" class="fs-modal__close" onclick="closeModal('modal-add-tx');">&times;</button>
    </div>
    <div class="fs-modal__body" id="addTxForm">

        <!-- Student Search (autocomplete) -->
        <div class="fs-form-row">
            <div class="fs-form-group" style="flex:2;">
                <label class="fs-form-label">Student <span class="req">*</span></label>
                <div class="ft-ac" id="acWrap">
                    <input type="text" id="acInput" class="ft-ac__input" autocomplete="off" spellcheck="false"
                           placeholder="Type name, reg number or student number..." maxlength="80" />
                    <div class="ft-ac__spinner" id="acSpinner"></div>
                    <button type="button" class="ft-ac__clear" id="acClear" title="Clear selection">&times;</button>
                    <div class="ft-ac__list" id="acList"></div>
                </div>
                <asp:HiddenField ID="hfSelectedRegNo" runat="server" />
            </div>
        </div>

        <!-- Selected Student Card -->
        <div class="ft-selected-student" id="selectedStudentCard"></div>

        <!-- Transaction Type + Billing Item -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Transaction Type <span class="req">*</span></label>
                <asp:DropDownList ID="ddlTxTransType" runat="server" CssClass="fs-form-input" onchange="autoFillDetail();">
                    <asp:ListItem Value="" Text="-- Select Type --" />
                    <asp:ListItem Value="Bill" Text="Bill" />
                    <asp:ListItem Value="Payment" Text="Payment" />
                </asp:DropDownList>
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">Billing Item <span class="req">*</span></label>
                <asp:DropDownList ID="ddlTxBillItem" runat="server" CssClass="fs-form-input" onchange="autoFillDetail();" />
            </div>
        </div>

        <!-- Amount + Date -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Amount (UGX) <span class="req">*</span></label>
                <asp:TextBox ID="txtTxAmount" runat="server" CssClass="fs-form-input" placeholder="0" />
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">Transaction Date <span class="req">*</span></label>
                <asp:TextBox ID="txtTxDate" runat="server" CssClass="fs-form-input" />
            </div>
        </div>

        <!-- Academic Year + Semester -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Academic Year <span class="req">*</span></label>
                <asp:DropDownList ID="ddlTxAcadYear" runat="server" CssClass="fs-form-input" />
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">Semester <span class="req">*</span></label>
                <asp:DropDownList ID="ddlTxSemester" runat="server" CssClass="fs-form-input">
                    <asp:ListItem Value="" Text="-- Select --" />
                    <asp:ListItem Value="1" Text="Semester 1" />
                    <asp:ListItem Value="2" Text="Semester 2" />
                    <asp:ListItem Value="3" Text="Semester 3" />
                </asp:DropDownList>
            </div>
        </div>

        <!-- Description -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Description <span class="req">*</span></label>
                <asp:TextBox ID="txtTxDetail" runat="server" CssClass="fs-form-input" MaxLength="250" placeholder="Auto-filled or type a description" />
            </div>
        </div>

        <!-- Post Status -->
        <div class="fs-form-row">
            <div class="fs-form-group" style="max-width:200px;">
                <label class="fs-form-label">Post Status</label>
                <asp:DropDownList ID="ddlTxPostStatus" runat="server" CssClass="fs-form-input">
                    <asp:ListItem Value="Pending" Text="Pending" Selected="True" />
                    <asp:ListItem Value="Posted" Text="Posted" />
                </asp:DropDownList>
            </div>
        </div>

    </div>
    <div class="fs-modal__footer">
        <button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-add-tx');">Cancel</button>
        <button type="button" id="btnModalSave" class="fs-btn fs-btn--primary" onclick="validateAndSaveTx();">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>
            Save Transaction
        </button>
    </div>
</div>
</div>
<!-- ============= /ADD TRANSACTION MODAL ============= -->

<!-- ============= EDIT TRANSACTION MODAL ============= -->
<div id="modal-edit-tx" class="fs-modal-overlay">
<div class="fs-modal">
    <div class="fs-modal__header" style="background:#174DA4;">
        <div class="fs-modal__title" id="editModalTitle" style="color:#fff;">Edit Transaction <span id="editTidBadge" style="font-size:11px;background:rgba(255,255,255,.2);padding:2px 8px;border-radius:10px;margin-left:8px;"></span></div>
        <button type="button" class="fs-modal__close" onclick="closeModal('modal-edit-tx');" style="color:#fff;">&times;</button>
    </div>
    <div class="fs-modal__body" id="editTxForm">

        <div id="editGlBanner" style="display:none;background:#fff7e8;border:1px solid #efd9a9;border-left:3px solid #8a5a00;padding:10px 13px;margin-bottom:14px;font-size:12px;line-height:1.55;color:#7a4f00;">
            <b>This is a General Ledger entry.</b> It has no billing item, academic year or
            semester, so those are hidden. The amount, direction, description and date are
            written straight to the ledger and the change is recorded against your name.
        </div>

        <!-- Student (Read-only) -->
        <div class="fs-form-row">
            <div class="fs-form-group" style="flex:2;">
                <label class="fs-form-label">Student</label>
                <div id="editStudentInfo" style="display:flex;align-items:center;gap:10px;padding:8px 12px;background:#f0f4ff;border:1px solid #d0d5dd;border-radius:6px;">
                    <div id="editStudentAvatar" style="width:32px;height:32px;background:#05275C;color:#fff;font-size:12px;font-weight:700;display:flex;align-items:center;justify-content:center;border-radius:4px;flex-shrink:0;"></div>
                    <div style="flex:1;min-width:0;">
                        <div id="editStudentName" style="font-size:13px;font-weight:700;color:#05275C;"></div>
                        <div id="editStudentRegno" style="font-size:11px;color:#555;"></div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Transaction Type + Billing Item -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Transaction Type <span class="req">*</span></label>
                <asp:DropDownList ID="ddlEditTransType" runat="server" CssClass="fs-form-input">
                    <asp:ListItem Value="" Text="-- Select Type --" />
                    <asp:ListItem Value="Bill" Text="Bill" />
                    <asp:ListItem Value="Payment" Text="Payment" />
                </asp:DropDownList>
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">Billing Item <span class="req">*</span></label>
                <asp:DropDownList ID="ddlEditBillItem" runat="server" CssClass="fs-form-input" />
            </div>
        </div>

        <!-- Amount + Date -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Amount (UGX) <span class="req">*</span></label>
                <asp:TextBox ID="txtEditAmount" runat="server" CssClass="fs-form-input" placeholder="0" />
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">Transaction Date <span class="req">*</span></label>
                <asp:TextBox ID="txtEditDate" runat="server" CssClass="fs-form-input" />
            </div>
        </div>

        <!-- Academic Year + Semester -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Academic Year <span class="req">*</span></label>
                <asp:DropDownList ID="ddlEditAcadYear" runat="server" CssClass="fs-form-input" />
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">Semester <span class="req">*</span></label>
                <asp:DropDownList ID="ddlEditSemester" runat="server" CssClass="fs-form-input">
                    <asp:ListItem Value="" Text="-- Select --" />
                    <asp:ListItem Value="1" Text="Semester 1" />
                    <asp:ListItem Value="2" Text="Semester 2" />
                    <asp:ListItem Value="3" Text="Semester 3" />
                </asp:DropDownList>
            </div>
        </div>

        <!-- Description -->
        <div class="fs-form-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Description <span class="req">*</span></label>
                <asp:TextBox ID="txtEditDetail" runat="server" CssClass="fs-form-input" MaxLength="250" />
            </div>
        </div>

        <!-- Post Status -->
        <div class="fs-form-row">
            <div class="fs-form-group" style="max-width:200px;">
                <label class="fs-form-label">Post Status</label>
                <asp:DropDownList ID="ddlEditPostStatus" runat="server" CssClass="fs-form-input">
                    <asp:ListItem Value="Pending" Text="Pending" />
                    <asp:ListItem Value="Posted" Text="Posted" />
                </asp:DropDownList>
            </div>
        </div>

    </div>
    <div class="fs-modal__footer">
        <button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-edit-tx');">Cancel</button>
        <button type="button" id="btnModalEdit" class="fs-btn fs-btn--primary" onclick="validateAndEditTx();">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>
            Update Transaction
        </button>
    </div>
</div>
</div>
<!-- ============= /EDIT TRANSACTION MODAL ============= -->

<!-- ============= GL SYNC MODAL ============= -->
<div class="fs-modal-overlay" id="modal-glsync">
<div class="fs-modal gl-modal">
    <div class="fs-modal__header">
        <span class="fs-modal__title">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px;margin-right:5px;"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
            GL Sync &mdash; Detect &amp; Repair
        </span>
        <button type="button" class="fs-modal__close" onclick="closeModal('modal-glsync');">&times;</button>
    </div>
    <div class="fs-modal__body" id="glSyncBody">

        <!-- KPI cards (populated by scan) -->
        <div class="gl-kpi-row">
            <div class="gl-kpi gl-kpi--orphan">
                <div class="gl-kpi__val" id="glOrphanCount">&mdash;</div>
                <div class="gl-kpi__label">Orphan tracking rows (missing from GL)</div>
            </div>
            <div class="gl-kpi gl-kpi--wrong">
                <div class="gl-kpi__val" id="glWrongTypeCount">&mdash;</div>
                <div class="gl-kpi__label">Wrong account_type (invisible to portal)</div>
            </div>
        </div>

        <!-- Progress bar (hidden until fix starts) -->
        <div class="gl-progress" id="glProgress" style="display:none;">
            <div class="gl-progress__bar-wrap">
                <div class="gl-progress__bar" id="glProgressBar"></div>
            </div>
            <div class="gl-progress__text" id="glProgressText">Preparing...</div>
        </div>

        <!-- Result banner (shown after fix completes) -->
        <div id="glResult" style="display:none;"></div>

        <!-- Sample orphan rows table -->
        <div class="gl-sample" id="glOrphanSample" style="display:none;">
            <div class="gl-sample__title">Sample orphan rows (newest first, max 50)</div>
            <div style="max-height:180px;overflow-y:auto;">
                <table class="gl-sample__table">
                    <thead><tr><th>TID</th><th>Reg No</th><th>Type</th><th>Amount</th><th>Date</th><th>Year</th><th>Sem</th></tr></thead>
                    <tbody id="glOrphanTableBody"></tbody>
                </table>
            </div>
        </div>

        <!-- Sample wrong-type rows -->
        <div class="gl-sample" id="glWrongSample" style="display:none;">
            <div class="gl-sample__title">Sample wrong account_type rows (newest first, max 20)</div>
            <div style="max-height:140px;overflow-y:auto;">
                <table class="gl-sample__table">
                    <thead><tr><th>TID</th><th>Reg No</th><th>Wrong Type</th><th>DR/CR</th><th>Amount</th><th>Date</th></tr></thead>
                    <tbody id="glWrongTableBody"></tbody>
                </table>
            </div>
        </div>

        <!-- Live log -->
        <div class="gl-log" id="glLog" style="display:none;"></div>

    </div>
    <div class="fs-modal__footer">
        <button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-glsync');">Close</button>
        <button type="button" class="fs-btn fs-btn--ghost" id="btnGLScan" onclick="glSyncScan();">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
            Scan
        </button>
        <button type="button" class="fs-btn fs-btn--primary" id="btnGLFix" onclick="glSyncFix();" disabled>
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
            Apply Fix
        </button>
    </div>
</div>
</div>
<!-- ============= /GL SYNC MODAL ============= -->

<!-- ============= BATCH DOUBLE-BILLING FIX WIZARD ============= -->
<div class="fs-modal-overlay" id="modal-batchdup">
<div class="fs-modal bd-modal">
    <div class="fs-modal__header">
        <span class="fs-modal__title">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="8.5" cy="7" r="4"></circle><polyline points="17 11 19 13 23 9"></polyline></svg>
            Batch Fix Double Billing
        </span>
        <button type="button" class="fs-modal__close" onclick="closeBatchDupModal();">&times;</button>
    </div>
    <div class="fs-modal__body">

        <!-- Wizard Step Indicator -->
        <div class="bd-steps">
            <div class="bd-step bd-step--active" id="bdStep1Ind">
                <span class="bd-step__num" id="bdStep1Num">1</span>
                <span class="bd-step__label">Detect</span>
            </div>
            <div class="bd-step__line" id="bdLine1"></div>
            <div class="bd-step" id="bdStep2Ind">
                <span class="bd-step__num" id="bdStep2Num">2</span>
                <span class="bd-step__label">Fix</span>
            </div>
            <div class="bd-step__line" id="bdLine2"></div>
            <div class="bd-step" id="bdStep3Ind">
                <span class="bd-step__num" id="bdStep3Num">3</span>
                <span class="bd-step__label">Results</span>
            </div>
        </div>

        <!-- ─── STEP 1: DETECT ─── -->
        <div class="bd-panel bd-panel--active" id="bdPanel1">
            <div class="bd-kpi-row">
                <div class="bd-kpi bd-kpi--students">
                    <div class="bd-kpi__val" id="bdAffected">&mdash;</div>
                    <div class="bd-kpi__label">Affected Accounts</div>
                </div>
                <div class="bd-kpi bd-kpi--dups">
                    <div class="bd-kpi__val" id="bdTotalDups">&mdash;</div>
                    <div class="bd-kpi__label">Duplicate Entries</div>
                </div>
                <div class="bd-kpi bd-kpi--amount">
                    <div class="bd-kpi__val" id="bdTotalAmt">&mdash;</div>
                    <div class="bd-kpi__label">Over-billed (UGX)</div>
                </div>
                <div class="bd-kpi bd-kpi--index">
                    <div class="bd-kpi__val" id="bdIndexStatus">&mdash;</div>
                    <div class="bd-kpi__label">UNIQUE Index</div>
                </div>
            </div>

            <div class="bd-acct-wrap" id="bdAcctWrap" style="display:none;">
                <table class="bd-acct-tbl">
                    <thead><tr><th>#</th><th>Reg No</th><th>Student Name</th><th style="text-align:right;">Duplicates</th><th style="text-align:right;">Amount (UGX)</th><th>Status</th></tr></thead>
                    <tbody id="bdAcctBody"></tbody>
                </table>
            </div>

            <div id="bdScanResult" style="display:none;"></div>
        </div>

        <!-- ─── STEP 2: FIX ─── -->
        <div class="bd-panel" id="bdPanel2">
            <div class="bd-progress-card">
                <div class="bd-progress-hdr">
                    <span class="bd-progress-hdr__title">Fixing Accounts</span>
                    <span class="bd-progress-hdr__count" id="bdFixCounter">0 / 0</span>
                </div>
                <div class="bd-progress__bar-wrap">
                    <div class="bd-progress__bar" id="bdFixBar"></div>
                </div>
                <div class="bd-progress__text" id="bdFixText">Waiting to start...</div>
                <div class="bd-progress__current" id="bdFixCurrent"></div>
            </div>

            <div class="bd-acct-wrap" style="max-height:200px;">
                <table class="bd-acct-tbl">
                    <thead><tr><th>#</th><th>Reg No</th><th>Student Name</th><th style="text-align:right;">Deleted</th><th>Balance Before</th><th>Balance After</th><th>Status</th></tr></thead>
                    <tbody id="bdFixBody"></tbody>
                </table>
            </div>
        </div>

        <!-- ─── STEP 3: RESULTS ─── -->
        <div class="bd-panel" id="bdPanel3">
            <div id="bdResultBanner"></div>
            <div class="bd-stat-row">
                <div class="bd-stat bd-stat--green">
                    <div class="bd-stat__val" id="bdResFixed">0</div>
                    <div class="bd-stat__label">Accounts Fixed</div>
                </div>
                <div class="bd-stat bd-stat--red">
                    <div class="bd-stat__val" id="bdResDeleted">0</div>
                    <div class="bd-stat__label">Entries Removed</div>
                </div>
                <div class="bd-stat">
                    <div class="bd-stat__val" id="bdResSkipped">0</div>
                    <div class="bd-stat__label">Already Clean</div>
                </div>
                <div class="bd-stat">
                    <div class="bd-stat__val" id="bdResErrors">0</div>
                    <div class="bd-stat__label">Errors</div>
                </div>
            </div>
        </div>

        <!-- Log console (shared across steps) -->
        <div class="bd-log" id="bdLog" style="display:none;"></div>

    </div>
    <div class="fs-modal__footer">
        <button type="button" class="fs-btn fs-btn--ghost" onclick="closeBatchDupModal();">Close</button>
        <button type="button" class="fs-btn fs-btn--ghost" id="btnBdScan" onclick="bdScan();">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
            Scan All
        </button>
        <button type="button" class="fs-btn fs-btn--primary" id="btnBdFix" onclick="bdStartFix();" disabled>
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
            Fix All
        </button>
    </div>
</div>
</div>
<!-- ============= /BATCH DOUBLE-BILLING FIX WIZARD ============= -->

<!-- ============= DELETE CONFIRMATION ============= -->
<div class="ft-confirm-overlay" id="deleteConfirm">
<div class="ft-confirm">
    <div class="ft-confirm__header">
        <div class="ft-confirm__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
        </div>
        <div class="ft-confirm__title">Delete Transaction</div>
    </div>
    <div class="ft-confirm__body">
        Are you sure you want to delete this transaction? This action <strong>cannot be undone</strong>.
        <dl class="ft-confirm__detail" id="deleteDetail"></dl>
        <div class="ft-confirm__reason-group">
            <span class="ft-reason-label">Reason Category <span style="color:#dc3545">*</span></span>
            <div class="ft-reason-options" id="delReasonOptions">
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="Data Entry Error" /><span>Data Entry Error</span></label>
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="Duplicate Transaction" /><span>Duplicate Transaction</span></label>
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="Reversal / Adjustment" /><span>Reversal / Adjustment</span></label>
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="Student Request" /><span>Student Request</span></label>
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="Transfer / Campus Change" /><span>Transfer / Campus Change</span></label>
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="System Error / AUTO Billing Mistake" /><span>System Error / AUTO Billing</span></label>
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="Waiver Approved" /><span>Waiver Approved</span></label>
                <label class="ft-reason-option"><input type="radio" name="delReasonCat" value="Other" /><span>Other</span></label>
            </div>
            <div class="ft-confirm__reason-error" id="delReasonError">Please select a reason before proceeding.</div>
            <textarea id="delReasonExplanation" rows="2" placeholder="Additional explanation (optional)..."></textarea>
        </div>
    </div>
    <div class="ft-confirm__footer">
        <button type="button" class="ft-confirm__btn ft-confirm__btn--cancel" onclick="closeDeleteConfirm()">Cancel</button>
        <button type="button" class="ft-confirm__btn ft-confirm__btn--delete" id="btnConfirmDelete" onclick="doConfirmAction()">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:middle;margin-right:4px;"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
            Delete
        </button>
    </div>
</div>
</div>
<!-- ============= /DELETE CONFIRMATION ============= -->

<!-- ============= BATCH ACTION BAR ============= -->
<div class="ft-batch-bar" id="batchBar">
    <div class="ft-batch-bar__count"><span id="batchCount">0</span> transaction(s) selected</div>
    <div class="ft-batch-sep"></div>
    <button type="button" class="ft-batch-btn ft-batch-btn--success" onclick="batchPostStatus('Posted')">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>
        Mark Posted
    </button>
    <button type="button" class="ft-batch-btn ft-batch-btn--warning" onclick="batchPostStatus('Pending')">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
        Mark Pending
    </button>
    <div class="ft-batch-sep"></div>
    <button type="button" class="ft-batch-btn ft-batch-btn--danger" onclick="openBatchDeleteModal()">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
        Delete Selected
    </button>
    <div class="ft-batch-sep"></div>
    <button type="button" class="ft-batch-btn ft-batch-btn--ghost" onclick="batchClearAll()">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
        Clear
    </button>
</div>
<!-- ============= /BATCH ACTION BAR ============= -->

<!-- ============= BATCH DELETE MODAL ============= -->
<div class="ft-confirm-overlay" id="batchDeleteOverlay">
<div class="ft-batch-modal">
    <div class="ft-batch-modal__header">
        <div class="ft-batch-modal__title">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
            Batch Delete Transactions
        </div>
        <button type="button" class="ft-batch-modal__close" onclick="closeBatchDeleteModal()">&#x2715;</button>
    </div>
    <div class="ft-batch-modal__body">
        <div class="ft-batch-summary" id="batchDelSummary">
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
            <span id="batchDelSummaryText">0 transactions will be permanently deleted.</span>
        </div>
        <p style="font-size:12px;color:#555;margin:0 0 12px;">This action is <strong>irreversible</strong>. All selected transactions and their GL entries will be deleted. Each deletion is archived and audit-logged.</p>

        <div class="ft-confirm__reason-group">
            <span class="ft-reason-label">Reason for deletion <span style="color:#dc3545;">*</span></span>
            <div class="ft-reason-options" id="batchReasonOptions">
                <label class="ft-reason-option"><input type="radio" name="batchDelCat" value="Duplicate Entry" /><span>Duplicate Entry</span></label>
                <label class="ft-reason-option"><input type="radio" name="batchDelCat" value="Reversal / Adjustment" /><span>Reversal / Adjustment</span></label>
                <label class="ft-reason-option"><input type="radio" name="batchDelCat" value="Student Request" /><span>Student Request</span></label>
                <label class="ft-reason-option"><input type="radio" name="batchDelCat" value="Transfer / Campus Change" /><span>Transfer / Campus Change</span></label>
                <label class="ft-reason-option"><input type="radio" name="batchDelCat" value="System Error / AUTO Billing Mistake" /><span>System Error / AUTO Billing</span></label>
                <label class="ft-reason-option"><input type="radio" name="batchDelCat" value="Other" /><span>Other</span></label>
            </div>
            <div class="ft-confirm__reason-error" id="batchReasonError">Please select a reason before proceeding.</div>
            <textarea id="batchDelExplanation" rows="2" style="width:100%;padding:7px 10px;border:1px solid #d0d5dd;font-size:12px;color:#374151;resize:vertical;font-family:inherit;outline:none;box-sizing:border-box;margin-top:8px;" placeholder="Additional explanation (optional)..."></textarea>
        </div>

        <div class="ft-batch-progress" id="batchDelProgress" style="display:none;">
            <div class="ft-batch-progress__bar-wrap"><div class="ft-batch-progress__bar" id="batchDelProgressBar"></div></div>
            <div class="ft-batch-progress__text" id="batchDelProgressText">Processing...</div>
        </div>
        <div id="batchDelResult" style="display:none;" class="ft-batch-result"></div>
    </div>
    <div class="ft-batch-modal__footer" id="batchDelFooter">
        <button type="button" class="ft-confirm__btn ft-confirm__btn--cancel" onclick="closeBatchDeleteModal()">Cancel</button>
        <button type="button" class="ft-confirm__btn ft-confirm__btn--delete" id="btnBatchDelConfirm" onclick="doBatchDelete()">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:middle;margin-right:4px;"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
            Delete All Selected
        </button>
    </div>
</div>
</div>
<!-- ============= /BATCH DELETE MODAL ============= -->

<script type="text/javascript">
/* ================================================================
   BATCH SELECTION ENGINE
   ================================================================ */
var _batch = { selected: {}, count: 0 };

function batchSelectAll(chk) {
    var rows = document.querySelectorAll('.ft-row-chk');
    for (var i = 0; i < rows.length; i++) {
        var cb = rows[i];
        if (chk.checked) {
            if (!_batch.selected[cb.value]) { _batch.selected[cb.value] = true; _batch.count++; }
            cb.checked = true;
            cb.closest('tr').classList.add('ft-row--selected');
        } else {
            if (_batch.selected[cb.value]) { delete _batch.selected[cb.value]; _batch.count--; }
            cb.checked = false;
            cb.closest('tr').classList.remove('ft-row--selected');
        }
    }
    batchUpdateBar();
}

function batchRowCheck(cb) {
    var row = cb.closest('tr');
    if (cb.checked) {
        _batch.selected[cb.value] = true; _batch.count++;
        row.classList.add('ft-row--selected');
    } else {
        delete _batch.selected[cb.value]; _batch.count--;
        row.classList.remove('ft-row--selected');
        var all = document.getElementById('chkSelectAll');
        if (all) all.checked = false;
    }
    batchUpdateBar();
}

function batchClearAll() {
    _batch.selected = {}; _batch.count = 0;
    var rows = document.querySelectorAll('.ft-row-chk');
    for (var i = 0; i < rows.length; i++) { rows[i].checked = false; rows[i].closest('tr').classList.remove('ft-row--selected'); }
    var all = document.getElementById('chkSelectAll');
    if (all) all.checked = false;
    batchUpdateBar();
}

function batchUpdateBar() {
    var bar = document.getElementById('batchBar');
    var cnt = document.getElementById('batchCount');
    cnt.textContent = _batch.count;
    if (_batch.count > 0) bar.classList.add('ft-batch-bar--visible');
    else bar.classList.remove('ft-batch-bar--visible');
}

function batchGetIds() {
    var ids = [];
    for (var k in _batch.selected) { if (_batch.selected.hasOwnProperty(k)) ids.push(k); }
    return ids;
}

/* ================================================================
   BATCH MARK AS POSTED / PENDING
   ================================================================ */
function batchPostStatus(status) {
    var ids = batchGetIds();
    if (ids.length === 0) return;
    if (!confirm('Mark ' + ids.length + ' transaction(s) as ' + status + '?')) return;

    var fd = new FormData();
    fd.append('ids', ids.join(','));
    fd.append('status', status);

    fetch(window.location.pathname + '?ajax=batch_post_status', { method: 'POST', body: fd })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            if (d.ok) {
                alert('Done! ' + d.updated + ' transaction(s) marked as ' + status + '.');
                batchClearAll();
                window.location.reload();
            } else {
                alert('Error: ' + (d.error || 'Unknown error'));
            }
        })
        .catch(function(err) { alert('Network error: ' + err); });
}

/* ================================================================
   BATCH DELETE
   ================================================================ */
function openBatchDeleteModal() {
    var ids = batchGetIds();
    if (ids.length === 0) return;
    if (ids.length > 200) { alert('Maximum 200 transactions can be deleted at once. Please narrow your selection.'); return; }

    document.getElementById('batchDelSummaryText').textContent =
        ids.length + ' transaction(s) will be permanently deleted. This cannot be undone.';
    document.getElementById('batchDelProgress').style.display = 'none';
    document.getElementById('batchDelResult').style.display = 'none';
    document.getElementById('batchDelFooter').style.display = 'flex';
    document.getElementById('batchDelProgressBar').style.width = '0%';
    document.getElementById('batchDelProgressText').textContent = 'Processing...';
    document.querySelectorAll('input[name="batchDelCat"]').forEach(function(r) { r.checked = false; r.closest('label').classList.remove('ft-reason-option--selected'); });
    document.getElementById('batchDelExplanation').value = '';
    document.getElementById('batchReasonError').style.display = 'none';

    document.getElementById('batchDeleteOverlay').classList.add('ft-confirm-overlay--visible');

    // Style radio buttons on change
    document.querySelectorAll('input[name="batchDelCat"]').forEach(function(r) {
        r.onchange = function() {
            document.querySelectorAll('input[name="batchDelCat"]').forEach(function(x) {
                x.closest('label').classList.toggle('ft-reason-option--selected', x.checked);
            });
            document.getElementById('batchReasonError').style.display = 'none';
        };
    });
}

function closeBatchDeleteModal() {
    document.getElementById('batchDeleteOverlay').classList.remove('ft-confirm-overlay--visible');
}

function doBatchDelete() {
    var ids = batchGetIds();
    if (ids.length === 0) { closeBatchDeleteModal(); return; }

    var selCat = document.querySelector('input[name="batchDelCat"]:checked');
    if (!selCat) {
        document.getElementById('batchReasonError').style.display = 'block';
        document.getElementById('batchReasonOptions').classList.add('ft-reason-options--invalid');
        return;
    }
    document.getElementById('batchReasonOptions').classList.remove('ft-reason-options--invalid');
    document.getElementById('batchReasonError').style.display = 'none';

    var category    = selCat.value;
    var explanation = document.getElementById('batchDelExplanation').value.trim();

    // Lock UI
    document.getElementById('btnBatchDelConfirm').disabled = true;
    document.getElementById('batchDelProgress').style.display = 'block';
    document.getElementById('batchDelResult').style.display = 'none';
    document.getElementById('batchDelFooter').style.display = 'flex';

    var bar  = document.getElementById('batchDelProgressBar');
    var text = document.getElementById('batchDelProgressText');
    bar.style.width = '5%';
    text.textContent = 'Sending request for ' + ids.length + ' transaction(s)...';

    var fd = new FormData();
    fd.append('ids',         ids.join(','));
    fd.append('category',    category);
    fd.append('explanation', explanation);

    fetch(window.location.pathname + '?ajax=batch_delete', { method: 'POST', body: fd })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            bar.style.width = '100%';
            var resDiv = document.getElementById('batchDelResult');
            resDiv.style.display = 'block';

            if (d.ok) {
                resDiv.className = 'ft-batch-result ft-batch-result--ok';
                resDiv.innerHTML = '&#10003; Deleted ' + d.deleted + ' of ' + d.total + ' transaction(s).' +
                    (d.errors > 0 ? ' <strong>' + d.errors + ' failed.</strong>' : '');
                text.textContent = 'Complete.';
                batchClearAll();
                document.getElementById('batchDelFooter').innerHTML =
                    '<button type="button" class="ft-confirm__btn ft-confirm__btn--cancel" onclick="closeBatchDeleteModal();window.location.reload()">Close &amp; Refresh</button>';
            } else {
                resDiv.className = 'ft-batch-result ft-batch-result--err';
                resDiv.textContent = 'Error: ' + (d.error || 'Unknown error');
                text.textContent = 'Failed.';
                document.getElementById('btnBatchDelConfirm').disabled = false;
            }
        })
        .catch(function(err) {
            document.getElementById('batchDelResult').style.display = 'block';
            document.getElementById('batchDelResult').className = 'ft-batch-result ft-batch-result--err';
            document.getElementById('batchDelResult').textContent = 'Network error: ' + err;
            document.getElementById('btnBatchDelConfirm').disabled = false;
        });
}

// Deselect all when page navigates (postback)
window.addEventListener('beforeunload', function() { batchClearAll(); });
</script>

</asp:Content>
