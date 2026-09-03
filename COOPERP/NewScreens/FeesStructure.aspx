<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="FeesStructure.aspx.cs" Inherits="COOPERP_NewScreens_FeesStructure" Title="Fee Structure & Settings - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===== FEE STRUCTURE & SETTINGS — aligned to Campus Dynamics design system ===== */

/* ---- Page header ---- */
.fm-page-header {
    display: flex; align-items: center; justify-content: space-between;
    padding: 14px 0 12px; margin-bottom: 16px;
    border-bottom: 2px solid #174DA4; flex-wrap: wrap; gap: 10px;
}
.fm-page-header__left { display: flex; align-items: center; gap: 12px; min-width: 0; }
.fm-page-header__icon {
    width: 40px; height: 40px; background: #05275C;
    display: flex; align-items: center; justify-content: center; flex-shrink: 0;
}
.fm-page-header__title { font-size: 17px; font-weight: 700; color: #05275C; line-height: 1.2; margin: 0; }
.fm-page-header__sub   { font-size: 12px; color: #666; margin: 2px 0 0 0; }

/* ---- Tab nav ---- */
.fm-tabs { display: flex; gap: 0; border-bottom: 2px solid #e0e5ed; margin-bottom: 14px; overflow-x: auto; }
.fm-tab {
    padding: 9px 18px; font-size: 12px; font-weight: 600; color: #666;
    cursor: pointer; border: none; background: none; border-bottom: 2px solid transparent;
    margin-bottom: -2px; white-space: nowrap; display: flex; align-items: center;
    gap: 6px; text-decoration: none; transition: color .15s;
}
.fm-tab:hover { color: #174DA4; }
.fm-tab--active { color: #174DA4; border-bottom-color: #174DA4; font-weight: 700; }

/* ---- Section tabs ---- */
.fs-section-tabs { display: flex; gap: 4px; margin-bottom: 14px; flex-wrap: wrap; }
.fs-section-tab {
    padding: 6px 14px; font-size: 11px; font-weight: 600;
    border: 1px solid #cdd3de; background: #fff; color: #555;
    cursor: pointer; display: flex; align-items: center; gap: 5px; transition: all .15s;
}
.fs-section-tab:hover { border-color: #174DA4; color: #174DA4; }
.fs-section-tab--active { background: #05275C; color: #fff; border-color: #05275C; }

/* ---- Panels ---- */
.fs-panel { display: none; }
.fs-panel--active { display: block; }

/* ---- Card ---- */
.fs-card { background: #fff; border: 1px solid #e0e5ed; overflow: hidden; margin-bottom: 14px; }
.fs-card__header {
    padding: 10px 14px; border-bottom: 1px solid #e0e5ed; background: #f8f9fb;
    display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 8px;
}
.fs-card__title { font-size: 12px; font-weight: 700; color: #05275C; display: flex; align-items: center; gap: 6px; }
.fs-card__meta {
    font-size: 10px; color: #174DA4; font-weight: 600;
    background: rgba(23,77,164,.07); padding: 2px 8px; border: 1px solid rgba(23,77,164,.15);
}

/* ---- Table ---- */
.fs-table { width: 100%; border-collapse: collapse; }
.fs-table th {
    font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #666;
    font-weight: 700; padding: 8px 12px; text-align: left;
    border-bottom: 2px solid #e0e5ed; background: #f5f7fa;
}
.fs-table td { font-size: 12px; padding: 7px 12px; color: #333; border-bottom: 1px solid #f0f2f5; vertical-align: middle; }
.fs-table tbody tr:hover td { background: #f5f8ff; }
.fs-code {
    font-family: 'Consolas','Courier New',monospace; font-size: 10px; color: #174DA4;
    font-weight: 700; background: rgba(23,77,164,.07); padding: 1px 5px;
    border: 1px solid rgba(23,77,164,.15);
}
.fs-badge { display: inline-block; padding: 2px 7px; font-size: 10px; font-weight: 700; text-transform: uppercase; }
.fs-badge--primary { background: #e8f0fc; color: #174DA4; }
.fs-badge--green  { background: #e6f4ea; color: #155724; }
.fs-badge--amber  { background: #fff8e1; color: #b45309; }
.fs-badge--red    { background: #fde8e8; color: #c62828; }
.fs-amount { font-variant-numeric: tabular-nums; font-weight: 600; }

/* ---- Filter bar ---- */
.fs-filter-bar { display: flex; gap: 10px; align-items: flex-end; flex-wrap: wrap; margin-bottom: 12px; }
.fs-filter-grp { display: flex; flex-direction: column; gap: 3px; }
.fs-filter-grp__label { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #888; font-weight: 700; }
.fs-filter-select {
    border: 1px solid #cdd3de; padding: 5px 9px; font-size: 12px;
    background: #fff; color: #1a1a2e; min-width: 130px; height: 30px;
}
.fs-filter-select:focus { border-color: #174DA4; outline: none; box-shadow: 0 0 0 2px rgba(23,77,164,.12); }

/* ---- Buttons ---- */
.fs-btn {
    padding: 5px 13px; font-size: 11px; font-weight: 600; border: none;
    cursor: pointer; display: inline-flex; align-items: center; gap: 5px;
    white-space: nowrap; transition: background .15s; line-height: 1.5;
}
.fs-btn--primary { background: #05275C; color: #fff; } .fs-btn--primary:hover { background: #041d45; }
.fs-btn--success { background: #16a34a; color: #fff; } .fs-btn--success:hover { background: #15803d; }
.fs-btn--danger  { background: #dc3545; color: #fff; } .fs-btn--danger:hover  { background: #b91c2c; }
.fs-btn--ghost   { background: #fff; color: #444; border: 1px solid #cdd3de; }
.fs-btn--ghost:hover { border-color: #05275C; color: #05275C; }
.fs-btn--sm { padding: 4px 10px; font-size: 10px; }

/* ---- Row action links ---- */
.fs-row-action { font-size: 11px; font-weight: 600; color: #174DA4; cursor: pointer; text-decoration: none; padding: 2px 5px; }
.fs-row-action:hover { background: rgba(23,77,164,.08); }
.fs-row-action--danger { color: #dc3545; }
.fs-row-action--danger:hover { background: rgba(220,53,69,.08); }

/* ---- Modal ---- */
.fs-modal-overlay { display: none; position: fixed; inset: 0; background: rgba(0,0,0,.45); z-index: 9998; }
.fs-modal-overlay--visible { display: flex; align-items: center; justify-content: center; }
.fs-modal { background: #fff; width: 520px; max-width: 96vw; max-height: 92vh; overflow-y: auto; box-shadow: 0 12px 40px rgba(0,0,0,.18); }
.fs-modal--wide   { width: 720px; }
.fs-modal--xlarge { width: 96vw; max-width: 1200px; }
.fs-modal__header {
    padding: 13px 18px; background: #05275C; color: #fff;
    display: flex; align-items: center; justify-content: space-between;
}
.fs-modal__title  { font-size: 13px; font-weight: 700; color: #fff; }
.fs-modal__close  { width: 24px; height: 24px; border: none; background: rgba(255,255,255,.15); cursor: pointer; color: #fff; font-size: 16px; line-height: 1; display: flex; align-items: center; justify-content: center; }
.fs-modal__close:hover { background: rgba(255,255,255,.3); }
.fs-modal__body   { padding: 16px 18px; }
.fs-modal__footer { padding: 11px 18px; border-top: 1px solid #e0e5ed; display: flex; gap: 8px; justify-content: flex-end; background: #f8f9fb; }

/* ---- Form ---- */
.fs-form-row { display: flex; gap: 10px; margin-bottom: 10px; flex-wrap: wrap; }
.fs-form-group { display: flex; flex-direction: column; gap: 3px; flex: 1; min-width: 130px; }
.fs-form-label { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #666; font-weight: 700; }
.fs-form-input { border: 1px solid #cdd3de; padding: 6px 9px; font-size: 12px; color: #1a1a2e; background: #fff; width: 100%; box-sizing: border-box; }
.fs-form-input:focus { border-color: #174DA4; outline: none; box-shadow: 0 0 0 2px rgba(23,77,164,.12); }

/* ---- Fee entry modal — compact grid ---- */
.pf-top-row { display: grid; grid-template-columns: 1fr 120px 190px; gap: 10px; margin-bottom: 12px; }

/* Which of a programme's fee structures is loaded. The two share one set of inputs, so this
   must be impossible to miss — an admin who edits the standard rates believing they are
   editing in-service silently reprices the wrong cohort. */
.pf-session-banner { font-size: 11px; font-weight: 700; padding: 7px 10px; margin: 0 0 10px;
                     border-left: 3px solid #05275C; background: #eef2f8; color: #05275C; }
.pf-session-banner--inservice { border-left-color: #d97706; background: #fff7ed; color: #92400e; }
.pf-session-banner span.pf-sb-note { display: block; font-weight: 400; color: #5b6472; margin-top: 2px; }
.pf-help { cursor: help; color: #8b93a3; font-weight: 400; }

/* Which fee console this URL is. Deliberately prominent: the two URLs are otherwise
   identical, and entering in-service rates into the standard structure would reprice every
   day student on the programme. */
.fs-console { display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
              padding: 9px 12px; margin: 0 0 14px; background: #eef2f8;
              border-left: 4px solid #05275C; }
.fs-console--inservice { background: #fff7ed; border-left-color: #d97706; }
.fs-console__tag { font-size: 10px; font-weight: 800; letter-spacing: .6px; padding: 3px 8px;
                   background: #d97706; color: #fff; white-space: nowrap; }
.fs-console__tag--main { background: #05275C; }
.fs-console__txt { font-size: 11.5px; color: #44506a; flex: 1 1 320px; line-height: 1.45; }
.fs-console__txt strong { color: #05275C; }
.fs-console--inservice .fs-console__txt strong { color: #92400e; }
.fs-console__switch { font-size: 11px; font-weight: 700; text-decoration: none; white-space: nowrap;
                      padding: 6px 11px; border: 1px solid #cbd5e4; background: #fff; color: #05275C; }
.fs-console__switch:hover { border-color: #174DA4; color: #174DA4; }
@media (max-width: 640px) { .fs-console__txt { flex-basis: 100%; } }

/* Marks a listing row as the in-service structure rather than the standard one. */
.fs-sess-badge { display: inline-block; font-size: 9px; font-weight: 700; letter-spacing: .3px;
                 padding: 1px 5px; margin-left: 6px; vertical-align: middle; }
.fs-sess-badge--inservice { background: #fff7ed; color: #92400e; border: 1px solid #fed7aa; }
.pf-year-section { border: 1px solid #e0e5ed; margin-bottom: 8px; }
.pf-year-header {
    padding: 8px 12px; background: #f5f7fa; cursor: pointer;
    display: flex; align-items: center; justify-content: space-between;
    font-size: 12px; font-weight: 700; color: #05275C; user-select: none;
    border-bottom: 1px solid #e0e5ed;
}
.pf-year-header:hover { background: #eef2fa; }
.pf-year-chk-label { display: flex; align-items: center; gap: 8px; }
.pf-year-meta { display: flex; align-items: center; gap: 10px; }
.pf-year-subtotal-badge {
    font-size: 10px; font-weight: 700; color: #174DA4;
    background: rgba(23,77,164,.08); padding: 2px 8px; border: 1px solid rgba(23,77,164,.15);
}
.pf-toggle-arrow { font-size: 10px; color: #999; }
.pf-year-body  { display: none; }
.pf-year-body--open { display: block; }

/* Compact fee grid inside modal */
.pf-fee-grid { width: 100%; border-collapse: collapse; }
.pf-fee-grid th {
    font-size: 10px; text-transform: uppercase; letter-spacing: .3px;
    color: #666; font-weight: 700; padding: 6px 10px; text-align: left;
    background: #f8f9fb; border-bottom: 1px solid #e0e5ed;
}
.pf-fee-grid th:not(:first-child) { text-align: right; }
.pf-fee-grid td { padding: 5px 8px; border-bottom: 1px solid #f0f2f5; vertical-align: middle; }
.pf-fee-grid tfoot td { padding: 6px 10px; background: #f5f7fa; border-top: 2px solid #e0e5ed; font-size: 11px; font-weight: 700; }
.pf-sem-tag { font-size: 11px; font-weight: 700; color: #174DA4; min-width: 55px; white-space: nowrap; }
.pf-amount-input {
    border: 1px solid #cdd3de; padding: 5px 8px;
    font-size: 12px; font-weight: 600; color: #1a1a2e; background: #fff;
    text-align: right; width: 100%; font-variant-numeric: tabular-nums; box-sizing: border-box;
}
.pf-amount-input:focus { border-color: #174DA4; outline: none; box-shadow: 0 0 0 2px rgba(23,77,164,.12); }
.pf-sem-total { text-align: right; font-size: 11px; font-weight: 700; color: #333; font-variant-numeric: tabular-nums; white-space: nowrap; min-width: 80px; }
.pf-subtotal { text-align: right; font-size: 12px; font-weight: 700; color: #05275C; font-variant-numeric: tabular-nums; }
.pf-grand-total { padding: 8px 10px; text-align: right; font-size: 12px; font-weight: 700; color: #05275C; border-top: 2px solid #05275C; background: rgba(5,39,92,.04); font-variant-numeric: tabular-nums; }

/* ---- Student overview cards ---- */
.st-overview { display: grid; grid-template-columns: repeat(4,1fr); gap: 10px; margin-bottom: 10px; }
.st-card { background: #fff; border: 1px solid #e0e5ed; padding: 12px 14px; display: flex; align-items: flex-start; gap: 12px; }
.st-card__icon { width: 36px; height: 36px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
.st-card__icon--active { background: #e6f4ea; color: #155724; }
.st-card__icon--enrolled { background: #e3f2fd; color: #0d47a1; }
.st-card__icon--billed { background: #fff8e1; color: #b45309; }
.st-card__icon--unbilled { background: #fde8e8; color: #c62828; }
.st-card__body { flex: 1; min-width: 0; }
.st-card__value { font-size: 22px; font-weight: 800; line-height: 1.1; }
.st-card__value--green { color: #155724; }
.st-card__value--blue { color: #0d47a1; }
.st-card__value--amber { color: #b45309; }
.st-card__value--red { color: #c62828; }
.st-card__label { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #888; font-weight: 700; margin-top: 1px; }
.st-card__sub { font-size: 10px; color: #999; margin-top: 2px; }
.st-overview__yr { font-size: 10px; color: #174DA4; font-weight: 700; background: rgba(23,77,164,.07); padding: 2px 8px; border: 1px solid rgba(23,77,164,.15); display: inline-block; margin-bottom: 8px; }

/* ---- Financial summary cards ---- */
.fn-overview { display: grid; grid-template-columns: repeat(4,1fr); gap: 10px; margin-bottom: 14px; }
.fn-card { background: #fff; border: 1px solid #e0e5ed; padding: 14px 16px; position: relative; overflow: hidden; }
.fn-card::before { content: ''; position: absolute; top: 0; left: 0; width: 3px; height: 100%; }
.fn-card--tuition::before { background: #05275C; }
.fn-card--functional::before { background: #174DA4; }
.fn-card--income::before { background: #16a34a; }
.fn-card--balance::before { background: #c62828; }
.fn-card__top { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
.fn-card__label { font-size: 10px; text-transform: uppercase; letter-spacing: .5px; color: #888; font-weight: 700; }
.fn-card__icon { width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; }
.fn-card__icon--tuition { background: rgba(5,39,92,.07); color: #05275C; }
.fn-card__icon--functional { background: rgba(23,77,164,.07); color: #174DA4; }
.fn-card__icon--income { background: rgba(22,163,74,.07); color: #16a34a; }
.fn-card__icon--balance { background: rgba(198,40,40,.07); color: #c62828; }
.fn-card__value { font-size: 20px; font-weight: 800; line-height: 1.2; font-variant-numeric: tabular-nums; }
.fn-card__value--navy { color: #05275C; }
.fn-card__value--blue { color: #174DA4; }
.fn-card__value--green { color: #16a34a; }
.fn-card__value--red { color: #c62828; }
.fn-card__currency { font-size: 11px; font-weight: 600; color: #999; margin-right: 2px; }
.fn-card__sub { font-size: 10px; color: #aaa; margin-top: 4px; display: flex; align-items: center; gap: 4px; }
.fn-card__sub svg { flex-shrink: 0; }
.fn-card__bar { height: 3px; background: #f0f0f0; margin-top: 10px; overflow: hidden; }
.fn-card__bar-fill { height: 100%; transition: width .6s ease; }
.fn-card__bar-fill--navy { background: #05275C; }
.fn-card__bar-fill--blue { background: #174DA4; }
.fn-card__bar-fill--green { background: #16a34a; }
.fn-card__bar-fill--red { background: #c62828; }

/* ---- Stats cards ---- */
.pf-stats { display: grid; grid-template-columns: repeat(4,1fr); gap: 10px; margin-bottom: 14px; }
.pf-stat { background: #fff; border: 1px solid #e0e5ed; padding: 12px 14px; }
.pf-stat__label { font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #888; font-weight: 700; }
.pf-stat__value { font-size: 22px; font-weight: 800; color: #1a1a2e; margin-top: 2px; line-height: 1.1; }
.pf-stat__sub { font-size: 11px; color: #666; margin-top: 2px; }

/* ---- Toast ---- */
.fs-toast { display: none; padding: 9px 14px; font-size: 12px; font-weight: 600; margin-bottom: 12px; border: 1px solid transparent; }
.fs-toast--success { display: block; background: #e6f4ea; color: #155724; border-color: #c3e6cb; }
.fs-toast--error   { display: block; background: #fde8e8; color: #c62828; border-color: #f5c6cb; }

/* ---- Programme cell ---- */
.fs-prog-cell { display: flex; flex-direction: column; gap: 1px; }
.fs-prog-name { font-weight: 600; font-size: 12px; color: #1a1a2e; line-height: 1.3; }
.fs-prog-code { font-family: 'Consolas','Courier New',monospace; font-size: 10px; color: #174DA4; font-weight: 700; }
.fs-prog-fac  { font-size: 10px; color: #888; margin-top: 1px; }

/* ---- Year dots ---- */
.fs-yr-dots { display: flex; gap: 3px; }
.fs-yr-dot { width: 20px; height: 20px; display: flex; align-items: center; justify-content: center; font-size: 8px; font-weight: 800; border: 1px solid transparent; }
.fs-yr-dot--on  { background: #e6f4ea; color: #155724; border-color: #c3e6cb; }
.fs-yr-dot--off { background: #f5f5f5; color: #ccc; border-color: #e8e8e8; }

/* ---- Action menu ---- */
.fs-action-wrap    { position: relative; display: inline-block; }
.fs-action-trigger {
    padding: 3px 10px; border: 1px solid #cdd3de; background: #f5f7fa;
    cursor: pointer; font-size: 11px; font-weight: 700; color: #444;
    transition: all .15s; white-space: nowrap;
}
.fs-action-trigger:hover { border-color: #05275C; color: #05275C; background: #eef2fa; }
.fs-action-trigger--open { border-color: #05275C; color: #05275C; background: #eef2fa; }
.fs-action-menu {
    display: none; position: absolute; right: 0; top: calc(100% + 2px);
    background: #fff; border: 1px solid #cdd3de;
    box-shadow: 0 4px 16px rgba(0,0,0,.12); z-index: 200;
    min-width: 165px; padding: 3px 0;
}
.fs-action-menu--visible { display: block; animation: fsMenuIn .1s ease-out; }
@keyframes fsMenuIn { from { opacity:0; transform:translateY(-3px); } to { opacity:1; transform:translateY(0); } }
.fs-action-menu__item {
    display: flex; align-items: center; gap: 7px; padding: 7px 12px;
    font-size: 11px; font-weight: 600; color: #333; cursor: pointer;
    border: none; background: none; width: 100%; text-align: left; white-space: nowrap;
}
.fs-action-menu__item:hover { background: #f5f8ff; color: #174DA4; }
.fs-action-menu__item--danger { color: #dc3545; }
.fs-action-menu__item--danger:hover { background: #fef5f5; color: #b91c2c; }
.fs-action-menu__divider { height: 1px; background: #f0f0f0; margin: 3px 0; }
.fs-action-menu__icon { width: 13px; height: 13px; flex-shrink: 0; }

/* ---- Bill Unbilled Button ---- */
.fs-bill-unbilled-btn {
    padding: 5px 14px; font-size: 11px; font-weight: 700; border: none; cursor: pointer;
    display: inline-flex; align-items: center; gap: 5px; white-space: nowrap;
    background: #dc3545; color: #fff; margin-left: auto;
}
.fs-bill-unbilled-btn:hover { background: #b91c2c; }
.fs-bill-unbilled-btn svg { flex-shrink: 0; }

/* ---- Batch bar ---- */
.fs-batch-bar {
    display: none; position: sticky; bottom: 0; left: 0; right: 0; z-index: 90;
    background: #05275C; padding: 10px 18px;
    box-shadow: 0 -3px 16px rgba(0,0,0,.18);
    align-items: center; justify-content: space-between; gap: 10px; flex-wrap: wrap;
}
.fs-batch-bar--visible { display: flex; }
.fs-batch-bar__info { display: flex; align-items: center; gap: 10px; }
.fs-batch-bar__count { background: rgba(255,255,255,.2); color: #fff; padding: 3px 10px; font-size: 11px; font-weight: 700; }
.fs-batch-bar__label { font-size: 11px; color: rgba(255,255,255,.8); font-weight: 600; }
.fs-batch-bar__actions { display: flex; gap: 6px; flex-wrap: wrap; }
.fs-batch-btn {
    padding: 5px 12px; font-size: 11px; font-weight: 700; border: none; cursor: pointer;
    display: inline-flex; align-items: center; gap: 5px; white-space: nowrap;
}
.fs-batch-btn--activate   { background: #16a34a; color: #fff; }
.fs-batch-btn--activate:hover { background: #15803d; }
.fs-batch-btn--deactivate { background: #d97706; color: #fff; }
.fs-batch-btn--deactivate:hover { background: #b45309; }
.fs-batch-btn--adjust     { background: #174DA4; color: #fff; }
.fs-batch-btn--adjust:hover { background: #0f3a7d; }
.fs-batch-btn--delete     { background: #dc3545; color: #fff; }
.fs-batch-btn--delete:hover { background: #b91c2c; }
.fs-batch-btn--clear      { background: rgba(255,255,255,.12); color: #fff; border: 1px solid rgba(255,255,255,.25); }
.fs-batch-btn--clear:hover{ background: rgba(255,255,255,.22); }

/* ---- Checkboxes ---- */
.fs-check { width: 15px; height: 15px; cursor: pointer; accent-color: #174DA4; }
.fs-table tbody tr.fs-row--selected td { background: #eef3fc; }

/* ---- Fee adjustment wizard ---- */
.fs-modal--wizard { max-width: 780px; width: 96%; }
.fw-rail { display:flex; border-bottom:1px solid #e0e5ed; background:#f8f9fb; }
.fw-step { flex:1 1 0; display:flex; align-items:center; gap:7px; padding:10px 12px; font-size:11px; color:#94a3b8; border-bottom:2px solid transparent; min-width:0; }
.fw-step__n { width:19px; height:19px; flex:0 0 19px; display:inline-flex; align-items:center; justify-content:center; background:#e2e8f0; color:#64748b; font-weight:800; font-size:10px; }
.fw-step__t { font-weight:700; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
.fw-step.is-on { color:#05275C; border-bottom-color:#174DA4; background:#fff; }
.fw-step.is-on .fw-step__n { background:#174DA4; color:#fff; }
.fw-step.is-done .fw-step__n { background:#16a34a; color:#fff; }
.fw-body { min-height:320px; }
.fw-pane { display:none; }
.fw-pane.is-on { display:block; }
.fw-lead { font-size:12px; color:#475569; margin:0 0 14px; line-height:1.5; }
.fw-field { margin-bottom:16px; }
.fw-label { display:block; font-size:10px; text-transform:uppercase; letter-spacing:.5px; font-weight:800; color:#64748b; margin-bottom:6px; }
.fw-hint { display:block; font-size:10px; color:#94a3b8; margin-top:5px; line-height:1.45; }
.fw-radios { display:flex; gap:8px; flex-wrap:wrap; }
.fw-radio { flex:1 1 210px; display:flex; align-items:center; gap:9px; padding:10px 12px; border:1px solid #cdd5e1; background:#fff; cursor:pointer; }
.fw-radio:hover { border-color:#174DA4; }
.fw-radio input { margin:0; }
.fw-radio.is-on { border-color:#174DA4; background:#f0f7ff; }
.fw-radio__sign { font-size:19px; font-weight:800; width:22px; text-align:center; color:#94a3b8; }
.fw-radio--up.is-on   .fw-radio__sign { color:#16a34a; }
.fw-radio--down.is-on .fw-radio__sign { color:#dc3545; }
.fw-radio--up.is-on   { border-color:#16a34a; background:#f0fdf4; }
.fw-radio--down.is-on { border-color:#dc3545; background:#fef2f2; }
.fw-radio__body { display:flex; flex-direction:column; min-width:0; }
.fw-radio__body b { font-size:12px; color:#1a1a2e; }
.fw-radio__body em { font-style:normal; font-size:10px; color:#94a3b8; }
.fw-amount { display:flex; align-items:stretch; border:1px solid #cdd5e1; background:#fff; max-width:300px; }
.fw-amount__sign { width:38px; flex:0 0 38px; display:flex; align-items:center; justify-content:center; font-size:20px; font-weight:800; background:#f0fdf4; color:#16a34a; border-right:1px solid #cdd5e1; }
.fw-amount__sign.is-down { background:#fef2f2; color:#dc3545; }
.fw-amount__in { flex:1 1 auto; border:none; outline:none; padding:10px 12px; font-size:15px; font-weight:700; color:#05275C; font-family:inherit; text-align:right; min-width:0; }
.fw-checks { display:flex; gap:7px; flex-wrap:wrap; }
.fw-check { display:flex; align-items:center; gap:7px; padding:8px 13px; border:1px solid #cdd5e1; background:#fff; cursor:pointer; font-size:11.5px; font-weight:600; color:#475569; }
.fw-check:hover { border-color:#174DA4; }
.fw-check.is-on { border-color:#174DA4; background:#f0f7ff; color:#05275C; }
.fw-check input { margin:0; }
.fw-grid-note { font-size:11px; color:#475569; background:#f8f9fb; border:1px solid #e0e5ed; padding:9px 11px; }
.fw-scroll { max-height:210px; overflow:auto; border:1px solid #e0e5ed; }
.fw-scroll--tall { max-height:250px; }
.fw-scroll table { width:100%; border-collapse:collapse; font-size:11px; }
.fw-scroll th { position:sticky; top:0; background:#f8f9fb; border-bottom:1px solid #e0e5ed; text-align:left; padding:6px 9px; font-size:9.5px; text-transform:uppercase; letter-spacing:.4px; color:#64748b; font-weight:800; }
.fw-scroll td { border-bottom:1px solid #f2f4f7; padding:5px 9px; }
.fw-scroll tr.fw-skip td { background:#fffaf5; color:#9a3412; }
.fw-was { color:#94a3b8; text-decoration:line-through; }
.fw-now { font-weight:800; color:#05275C; }
.fw-now--up { color:#15803d; } .fw-now--down { color:#b91c1c; }
.fw-sentence { font-size:13px; line-height:1.6; color:#1a1a2e; background:#f0f7ff; border:1px solid #cfe0f5; padding:12px 14px; margin-bottom:12px; }
.fw-sentence b { color:#05275C; }
.fw-tally { display:flex; gap:7px; flex-wrap:wrap; margin-bottom:11px; }
.fw-pill { font-size:10.5px; font-weight:800; padding:5px 10px; border:1px solid; }
.fw-pill--go { background:#f0fdf4; border-color:#a7dfba; color:#15803d; }
.fw-pill--skip { background:#fff7ed; border-color:#fdba74; color:#9a3412; }
.fw-pill--neutral { background:#f5f7fa; border-color:#e0e5ed; color:#64748b; }
.fw-msg { display:none; font-size:11.5px; padding:9px 11px; margin-bottom:12px; border:1px solid; }
.fw-msg.show { display:block; }
.fw-msg--bad { background:#fef2f2; border-color:#fca5a5; color:#991b1b; }
.fw-msg--ok  { background:#f0fdf4; border-color:#a7dfba; color:#15803d; }
.fw-foot__sp { flex:1 1 auto; }
@media (max-width:640px){ .fw-step__t{display:none;} .fw-rail{gap:0;} }

/* ---- Batch adjust preview ---- */
.fs-adjust-preview { background: #f8f9fb; border: 1px solid #e0e5ed; padding: 10px; margin-top: 10px; max-height: 200px; overflow-y: auto; font-size: 11px; }
.fs-adjust-preview table { width: 100%; border-collapse: collapse; }
.fs-adjust-preview th { font-size: 10px; text-transform: uppercase; color: #888; font-weight: 700; padding: 4px 8px; border-bottom: 1px solid #e0e5ed; }
.fs-adjust-preview td { padding: 4px 8px; font-size: 11px; border-bottom: 1px solid #f2f3f5; }

/* ---- Process Billing modal ---- */
.pb-header { display: flex; align-items: center; gap: 14px; padding: 10px 0 14px; border-bottom: 1px solid #e0e5ed; margin-bottom: 14px; }

/* ---- Batch Billing Panel ---- */
.bb-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 10px; margin-bottom: 14px; }
.bb-header__left { display: flex; align-items: center; gap: 14px; }
.bb-header__icon { width: 42px; height: 42px; background: #174DA4; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
.bb-header__title { font-size: 15px; font-weight: 700; color: #05275C; }
.bb-header__sub { font-size: 11px; color: #666; margin-top: 2px; }
.bb-header__badges { display: flex; gap: 6px; flex-wrap: wrap; }
.bb-badge { padding: 3px 10px; font-size: 10px; font-weight: 700; border: 1px solid; display: inline-flex; align-items: center; gap: 4px; }
.bb-badge--year { background: #e6f4ea; color: #155724; border-color: #c3e6cb; }
.bb-badge--sem  { background: #e8f0fc; color: #174DA4; border-color: rgba(23,77,164,.2); }
.bb-badge--warn { background: #fff8e1; color: #b45309; border-color: #ffd54f; }

.bb-controls { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; flex-wrap: wrap; }
.bb-gen-btn {
    padding: 8px 22px; font-size: 12px; font-weight: 700; border: none; cursor: pointer;
    background: #174DA4; color: #fff; display: inline-flex; align-items: center; gap: 6px;
}
.bb-gen-btn:hover { background: #0f3a7d; }
.bb-gen-btn:disabled { background: #b0bec5; cursor: not-allowed; }

.bb-stats { display: grid; grid-template-columns: repeat(5,1fr); gap: 10px; margin-bottom: 14px; }
.bb-stat { background: #fff; border: 1px solid #e0e5ed; padding: 12px 14px; text-align: center; }
.bb-stat__val { font-size: 20px; font-weight: 800; line-height: 1.1; }
.bb-stat__val--blue   { color: #174DA4; }
.bb-stat__val--green  { color: #155724; }
.bb-stat__val--amber  { color: #b45309; }
.bb-stat__val--red    { color: #c62828; }
.bb-stat__val--purple { color: #6a1b9a; }
.bb-stat__lbl { font-size: 10px; color: #888; font-weight: 700; text-transform: uppercase; margin-top: 3px; }

.bb-table { width: 100%; border-collapse: collapse; }
.bb-table th {
    font-size: 10px; text-transform: uppercase; letter-spacing: .4px; color: #666; font-weight: 700;
    padding: 8px 12px; text-align: left; border-bottom: 2px solid #e0e5ed; background: #f5f7fa;
}
.bb-table td { font-size: 12px; padding: 7px 12px; color: #333; border-bottom: 1px solid #f0f2f5; vertical-align: middle; }
.bb-table tbody tr:hover td { background: #f5f8ff; }
.bb-table td.bb-amt { text-align: right; font-weight: 700; font-variant-numeric: tabular-nums; }
.bb-row-prog { font-weight: 600; color: #1a1a2e; }
.bb-row-code { font-size: 10px; color: #174DA4; font-family: 'Consolas',monospace; font-weight: 700; margin-left: 6px; }
.bb-row-fac  { font-size: 10px; color: #888; display: block; margin-top: 1px; }

.bb-proceed-bar {
    display: flex; align-items: center; justify-content: space-between; gap: 12px;
    padding: 12px 16px; border-top: 2px solid #e0e5ed; background: #f8f9fb; margin-top: 14px; flex-wrap: wrap;
}
.bb-proceed-bar__info { font-size: 11px; color: #888; display: flex; align-items: center; gap: 6px; }
.bb-exec-btn {
    padding: 9px 28px; font-size: 12px; font-weight: 700; border: none; cursor: pointer;
    background: #05275C; color: #fff; display: inline-flex; align-items: center; gap: 7px;
}
.bb-exec-btn:hover { background: #041d45; }
.bb-exec-btn:disabled { background: #b0bec5; cursor: not-allowed; }

.bb-progress { display: none; text-align: center; padding: 32px 20px; }
.bb-progress--visible { display: block; }
.bb-progress__bar-wrap { width: 100%; max-width: 400px; height: 6px; background: #e0e5ed; margin: 12px auto 8px; }
.bb-progress__bar { height: 100%; background: #174DA4; width: 0%; transition: width .3s ease; }
.bb-progress__text { font-size: 12px; color: #555; font-weight: 600; }
.bb-progress__detail { font-size: 11px; color: #888; margin-top: 4px; }

.bb-result { display: none; padding: 20px; text-align: center; }
.bb-result--visible { display: block; }
.bb-result__icon { width: 48px; height: 48px; margin: 0 auto 10px; }
.bb-result__title { font-size: 15px; font-weight: 700; margin-bottom: 4px; }
.bb-result__title--success { color: #155724; }
.bb-result__title--error   { color: #c62828; }
.bb-result__detail { font-size: 12px; color: #666; max-width: 600px; margin: 0 auto; }
.bb-result__stats { display: flex; gap: 12px; justify-content: center; margin-top: 12px; flex-wrap: wrap; }
.bb-result__stat { background: #f8f9fb; border: 1px solid #e0e5ed; padding: 8px 16px; text-align: center; min-width: 100px; }
.bb-result__stat-val { font-size: 18px; font-weight: 800; color: #05275C; }
.bb-result__stat-lbl { font-size: 10px; color: #888; font-weight: 700; text-transform: uppercase; margin-top: 2px; }

.bb-placeholder { display: flex; align-items: center; justify-content: center; min-height: 260px; color: #bbb; font-size: 13px; text-align: center; }
.bb-placeholder svg { display: block; margin: 0 auto 10px; opacity: .4; }

@media (max-width: 900px) {
    .bb-stats { grid-template-columns: repeat(3,1fr); }
}
@media (max-width: 640px) {
    .bb-stats { grid-template-columns: 1fr 1fr; }
}
.pb-header__prog  { font-size: 15px; font-weight: 700; color: #05275C; }
.pb-header__code  { font-size: 12px; color: #888; font-family: 'Consolas',monospace; margin-left: 6px; }
.pb-header__badge { padding: 2px 8px; font-size: 10px; font-weight: 700; background: #e6f4ea; color: #155724; border: 1px solid #c3e6cb; }
.pb-controls { display: flex; align-items: center; gap: 14px; margin-bottom: 14px; flex-wrap: wrap; }
.pb-controls label { font-size: 12px; font-weight: 600; color: #555; }
.pb-controls select { padding: 6px 10px; border: 1px solid #cdd3de; font-size: 12px; background: #fff; min-width: 150px; }
.pb-preview-btn { padding: 7px 18px; font-size: 12px; font-weight: 700; border: none; cursor: pointer; background: #174DA4; color: #fff; display: inline-flex; align-items: center; gap: 5px; }
.pb-preview-btn:hover { background: #0f3a7d; }
.pb-preview-btn:disabled { background: #b0bec5; cursor: not-allowed; }
.pb-panels { display: flex; gap: 16px; margin-top: 4px; min-height: 300px; }
.pb-panel { flex: 1; min-width: 0; background: #f8f9fb; border: 1px solid #e0e5ed; display: flex; flex-direction: column; overflow: hidden; }
.pb-panel--left  { border-top: 3px solid #174DA4; }
.pb-panel--right { border-top: 3px solid #16a34a; }
.pb-panel__head  { display: flex; align-items: center; justify-content: space-between; padding: 10px 14px 8px; background: #fff; border-bottom: 1px solid #e0e5ed; }
.pb-panel__title { font-size: 12px; font-weight: 700; color: #05275C; display: flex; align-items: center; gap: 6px; }
.pb-panel__count { padding: 2px 8px; font-size: 11px; font-weight: 700; }
.pb-panel--left  .pb-panel__count { background: #e8f0fc; color: #174DA4; }
.pb-panel--right .pb-panel__count { background: #e6f4ea; color: #155724; }
.pb-panel__body  { flex: 1; overflow-y: auto; max-height: 340px; }
.pb-panel__foot  { padding: 8px 14px; background: #fff; border-top: 1px solid #e0e5ed; display: flex; justify-content: space-between; align-items: center; }
.pb-panel__total-label  { font-size: 10px; color: #888; font-weight: 700; text-transform: uppercase; }
.pb-panel__total-amount { font-size: 15px; font-weight: 800; }
.pb-panel--left  .pb-panel__total-amount { color: #174DA4; }
.pb-panel--right .pb-panel__total-amount { color: #155724; }
.pb-table { width: 100%; border-collapse: collapse; font-size: 11px; }
.pb-table thead th { font-size: 9px; text-transform: uppercase; letter-spacing: .04em; color: #888; font-weight: 700; padding: 7px 10px; text-align: left; border-bottom: 1px solid #e0e5ed; position: sticky; top: 0; background: #f8f9fb; z-index: 1; }
.pb-table tbody td { padding: 6px 10px; border-bottom: 1px solid #f0f2f5; color: #333; vertical-align: top; }
.pb-table tbody tr:hover td { background: #eef3fc; }
.pb-stud-name  { font-weight: 600; display: block; }
.pb-stud-regno { font-size: 10px; color: #888; font-family: 'Consolas',monospace; }
.pb-amt { text-align: right; font-weight: 700; font-variant-numeric: tabular-nums; }
.pb-amt--tuition { color: #174DA4; }
.pb-amt--func    { color: #6a1b9a; }
.pb-amt--total   { color: #1a2233; font-size: 12px; }
.pb-empty { text-align: center; padding: 36px 20px; color: #aaa; font-size: 13px; }
.pb-empty svg { display: block; margin: 0 auto 10px; opacity: .4; }
.pb-summary { display: flex; gap: 12px; padding: 10px 0; margin-top: 10px; border-top: 1px solid #e0e5ed; flex-wrap: wrap; }
.pb-summary-card { flex: 1; min-width: 130px; background: #fff; border: 1px solid #e0e5ed; padding: 10px 14px; text-align: center; }
.pb-summary-card__val { font-size: 20px; font-weight: 800; }
.pb-summary-card__val--blue   { color: #174DA4; }
.pb-summary-card__val--green  { color: #155724; }
.pb-summary-card__val--amber  { color: #b45309; }
.pb-summary-card__val--purple { color: #6a1b9a; }
.pb-summary-card__lbl { font-size: 10px; color: #888; font-weight: 700; text-transform: uppercase; margin-top: 2px; }
.pb-footer { display: flex; align-items: center; justify-content: space-between; margin-top: 14px; padding-top: 12px; border-top: 1px solid #e0e5ed; }
.pb-footer__info { font-size: 11px; color: #888; }
.pb-proceed-btn { padding: 9px 24px; font-size: 12px; font-weight: 700; border: none; cursor: pointer; background: #05275C; color: #fff; display: inline-flex; align-items: center; gap: 7px; }
.pb-proceed-btn:hover { background: #041d45; }
.pb-proceed-btn:disabled { background: #b0bec5; cursor: not-allowed; }
.pb-progress { display: none; text-align: center; padding: 28px; }
.pb-progress--visible { display: block; }
.pb-progress__spinner { width: 34px; height: 34px; border: 3px solid #e0e5ed; border-top-color: #174DA4; border-radius: 50%; animation: pbSpin 0.8s linear infinite; margin: 0 auto 10px; }
@keyframes pbSpin { to { transform: rotate(360deg); } }
.pb-progress__text { font-size: 12px; color: #555; font-weight: 600; }
.pb-result { display: none; padding: 20px; text-align: center; }
.pb-result--visible { display: block; }
.pb-result__title { font-size: 15px; font-weight: 700; margin-bottom: 4px; }
.pb-result__title--success { color: #155724; }
.pb-result__title--error   { color: #c62828; }
.pb-result__detail { font-size: 12px; color: #666; }
.pb-placeholder { display: flex; align-items: center; justify-content: center; min-height: 280px; color: #bbb; font-size: 13px; text-align: center; }

/* Responsive */
@media (max-width: 900px) {
    .st-overview { grid-template-columns: repeat(2,1fr); }
    .pf-stats { grid-template-columns: repeat(2,1fr); }
    .pb-panels { flex-direction: column; }
}
@media (max-width: 640px) {
    .fm-tabs .fm-tab { padding: 8px 12px; font-size: 11px; }
    .fs-modal { width: 98vw; }
    .pf-top-row { grid-template-columns: 1fr; }
    .pf-session-banner { font-size: 10.5px; }
    .fs-batch-bar { padding: 10px 12px; }
    .fs-batch-btn { padding: 5px 9px; font-size: 10px; }
    .st-overview { grid-template-columns: 1fr 1fr; }
    .pf-stats { grid-template-columns: 1fr 1fr; }
}
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- Toast Message -->
<asp:Panel ID="pnlToast" runat="server" Visible="false">
    <div class="fs-toast" id="divToast" runat="server"></div>
</asp:Panel>

<!-- Hidden fields for CRUD -->
<asp:HiddenField ID="hfEditId" runat="server" />
<asp:HiddenField ID="hfEditType" runat="server" />
<asp:HiddenField ID="hfActivePanel" runat="server" Value="prog-fees" />
<%-- Which fee console is open (MAIN / INSERVICE). Set from the route on first load and
     carried across postbacks, since a postback need not re-supply route values. --%>
<asp:HiddenField ID="hfConsoleSession" runat="server" />

<!-- Page Header -->
<div class="fm-page-header">
    <div class="fm-page-header__left">
        <div class="fm-page-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2"><circle cx="12" cy="12" r="3"></circle><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"></path></svg>
        </div>
        <div>
            <div class="fm-page-header__title">Fee Structure &amp; Settings</div>
            <div class="fm-page-header__sub">Programme fee structures, billing items &amp; billing systems</div>
        </div>
    </div>
</div>

<%-- Which of the two fee consoles this URL is, and a switch to the other. --%>
<asp:Literal ID="litConsoleBanner" runat="server" />

<!-- Tab Navigation -->
<div class="fm-tabs">
    <a class="fm-tab" href="FeesManagement.aspx">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="9"></rect><rect x="14" y="3" width="7" height="5"></rect><rect x="14" y="12" width="7" height="9"></rect><rect x="3" y="16" width="7" height="5"></rect></svg>
        Dashboard
    </a>
    <a class="fm-tab" href="FeesTransactions.aspx">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="17 1 21 5 17 9"></polyline><path d="M3 11V9a4 4 0 0 1 4-4h14"></path><polyline points="7 23 3 19 7 15"></polyline><path d="M21 13v2a4 4 0 0 1-4 4H3"></path></svg>
        Transactions
    </a>
    <a class="fm-tab fm-tab--active" href="FeesStructure.aspx">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"></circle><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"></path></svg>
        Fee Structure &amp; Settings
    </a>
    <a class="fm-tab" href="FeesRegistration.aspx">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><path d="M23 21v-2a4 4 0 0 0-3-3.87"></path><path d="M16 3.13a4 4 0 0 1 0 7.75"></path></svg>
        Registration
    </a>
</div>

<!-- Section Tabs -->
<div class="fs-section-tabs">
    <button type="button" class="fs-section-tab fs-section-tab--active" id="tabProgFees" onclick="showPanel('prog-fees',this)">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"></path></svg>
        Programme Fee Structures
    </button>
    <button type="button" class="fs-section-tab" id="tabBillingItems" onclick="showPanel('billing-items',this)">Billing Items</button>
    <button type="button" class="fs-section-tab" id="tabBillingSystems" onclick="showPanel('billing-systems',this)">Billing Systems</button>
    <button type="button" class="fs-section-tab" id="tabBatchBilling" onclick="showPanel('batch-billing',this)">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
        Batch Billing
    </button>
    <button type="button" class="fs-bill-unbilled-btn" onclick="confirmBillUnbilled();" title="Bill all unbilled registered students for this academic year">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
        Bill Unbilled Students
    </button>
</div>

<!-- ======= PANEL: Programme Fee Structures (PRIMARY) ============== -->
<div id="panel-prog-fees" class="fs-panel fs-panel--active">

    <!-- Student Overview Cards -->
    <span class="st-overview__yr"><asp:Literal ID="litStAcadYear" runat="server" /></span>
    <div class="st-overview">
        <div class="st-card">
            <div class="st-card__icon st-card__icon--active">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
            </div>
            <div class="st-card__body">
                <div class="st-card__value st-card__value--green"><asp:Literal ID="litStActive" runat="server" Text="0" /></div>
                <div class="st-card__label">Active Students</div>
                <div class="st-card__sub">new_status = ACTIVE</div>
            </div>
        </div>
        <div class="st-card">
            <div class="st-card__icon st-card__icon--enrolled">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c0 2 3 4 6 4s6-2 6-4v-5"/></svg>
            </div>
            <div class="st-card__body">
                <div class="st-card__value st-card__value--blue"><asp:Literal ID="litStEnrolled" runat="server" Text="0" /></div>
                <div class="st-card__label">Enrolled This Year</div>
                <div class="st-card__sub">active &amp; registered in active sem</div>
            </div>
        </div>
        <div class="st-card">
            <div class="st-card__icon st-card__icon--billed">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/></svg>
            </div>
            <div class="st-card__body">
                <div class="st-card__value st-card__value--amber"><asp:Literal ID="litStBilled" runat="server" Text="0" /></div>
                <div class="st-card__label">Billed Students</div>
                <div class="st-card__sub">enrolled in current year &amp; semester and billed</div>
            </div>
        </div>
        <div class="st-card">
            <div class="st-card__icon st-card__icon--unbilled">
                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
            </div>
            <div class="st-card__body">
                <div class="st-card__value st-card__value--red"><asp:Literal ID="litStUnbilled" runat="server" Text="0" /></div>
                <div class="st-card__label">Not Billed</div>
                <div class="st-card__sub">active, registered this year, no bill</div>
            </div>
        </div>
    </div>

    <!-- Financial Summary Cards (enrolled students only) -->
    <div class="fn-overview">
        <div class="fn-card fn-card--tuition">
            <div class="fn-card__top">
                <div class="fn-card__label">Tuition Billed</div>
                <div class="fn-card__icon fn-card__icon--tuition">
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/></svg>
                </div>
            </div>
            <div class="fn-card__value fn-card__value--navy"><span class="fn-card__currency">UGX</span><asp:Literal ID="litFnTuition" runat="server" Text="0" /></div>
            <div class="fn-card__sub">
                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg>
                item code 1 &mdash; enrolled students only
            </div>
            <div class="fn-card__bar"><div class="fn-card__bar-fill fn-card__bar-fill--navy" id="barTuition" style="width:0%"></div></div>
        </div>
        <div class="fn-card fn-card--functional">
            <div class="fn-card__top">
                <div class="fn-card__label">Functional Fees</div>
                <div class="fn-card__icon fn-card__icon--functional">
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>
                </div>
            </div>
            <div class="fn-card__value fn-card__value--blue"><span class="fn-card__currency">UGX</span><asp:Literal ID="litFnFunctional" runat="server" Text="0" /></div>
            <div class="fn-card__sub">
                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg>
                item code 52 &mdash; enrolled students only
            </div>
            <div class="fn-card__bar"><div class="fn-card__bar-fill fn-card__bar-fill--blue" id="barFunctional" style="width:0%"></div></div>
        </div>
        <div class="fn-card fn-card--income">
            <div class="fn-card__top">
                <div class="fn-card__label">Total Paid (Income)</div>
                <div class="fn-card__icon fn-card__icon--income">
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
                </div>
            </div>
            <div class="fn-card__value fn-card__value--green"><span class="fn-card__currency">UGX</span><asp:Literal ID="litFnPaid" runat="server" Text="0" /></div>
            <div class="fn-card__sub">
                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg>
                payments received &mdash; enrolled students
            </div>
            <div class="fn-card__bar"><div class="fn-card__bar-fill fn-card__bar-fill--green" id="barPaid" style="width:0%"></div></div>
        </div>
        <div class="fn-card fn-card--balance">
            <div class="fn-card__top">
                <div class="fn-card__label">Outstanding Balance</div>
                <div class="fn-card__icon fn-card__icon--balance">
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
                </div>
            </div>
            <div class="fn-card__value fn-card__value--red"><span class="fn-card__currency">UGX</span><asp:Literal ID="litFnBalance" runat="server" Text="0" /></div>
            <div class="fn-card__sub">
                <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg>
                total billed &minus; total paid
            </div>
            <div class="fn-card__bar"><div class="fn-card__bar-fill fn-card__bar-fill--red" id="barBalance" style="width:0%"></div></div>
        </div>
    </div>
    <asp:HiddenField ID="hfFnTotalBill" runat="server" Value="0" />

    <!-- Fee Structure Stats Row -->
    <div class="pf-stats">
        <div class="pf-stat">
            <div class="pf-stat__label">Total Programmes</div>
            <div class="pf-stat__value"><asp:Literal ID="litStatTotal" runat="server" Text="0" /></div>
            <div class="pf-stat__sub">with fee structures</div>
        </div>
        <div class="pf-stat">
            <div class="pf-stat__label">Active Structures</div>
            <div class="pf-stat__value" style="color:#155724;"><asp:Literal ID="litStatActive" runat="server" Text="0" /></div>
            <div class="pf-stat__sub">ready for billing</div>
        </div>
        <div class="pf-stat">
            <div class="pf-stat__label">Inactive / Pending</div>
            <div class="pf-stat__value" style="color:#e67e00;"><asp:Literal ID="litStatInactive" runat="server" Text="0" /></div>
            <div class="pf-stat__sub">need configuration</div>
        </div>
        <div class="pf-stat">
            <div class="pf-stat__label">Missing Structures</div>
            <div class="pf-stat__value" style="color:#c62828;"><asp:Literal ID="litStatMissing" runat="server" Text="0" /></div>
            <div class="pf-stat__sub">programmes with no structure</div>
        </div>
    </div>

    <!-- Filters -->
    <div class="fs-filter-bar">
        <div class="fs-filter-grp">
            <label class="fs-filter-grp__label">Filter by Status</label>
            <asp:DropDownList ID="ddlPFStatus" runat="server" CssClass="fs-filter-select" AutoPostBack="true" OnSelectedIndexChanged="ddlPFStatus_Changed">
                <asp:ListItem Text="All Structures" Value="" />
                <asp:ListItem Text="Active Only" Value="Yes" />
                <asp:ListItem Text="Inactive Only" Value="No" />
            </asp:DropDownList>
        </div>
        <div class="fs-filter-grp">
            <label class="fs-filter-grp__label">Search Programme</label>
            <asp:TextBox ID="txtPFSearch" runat="server" CssClass="fs-filter-select" placeholder="Type programme code or name..." style="min-width:240px;" AutoPostBack="true" OnTextChanged="txtPFSearch_Changed" />
        </div>
        <div style="display:flex;gap:6px;align-items:flex-end;">
            <asp:Button ID="btnAddStructure" runat="server" Text="+ Add Fee Structure" CssClass="fs-btn fs-btn--primary fs-btn--sm" OnClick="btnAddStructure_Click" />
            <button type="button" class="fs-btn fs-btn--ghost fs-btn--sm" onclick="window.open('FeesStructurePrint.aspx','_blank');" title="Generate printable PDF of entire fee structure">
                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 9V2h12v7"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
                Print All Fee Structures
            </button>
            <button type="button" class="fs-btn fs-btn--sm" onclick="window.location='FeesStructureExport.aspx';" title="Download complete fee structure as Excel spreadsheet" style="background:#1d6f42;color:#fff;border:none;">
                <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
                Export to Excel
            </button>
        </div>
    </div>

    <!-- Fee Structure Table -->
    <div class="fs-card">
        <div class="fs-card__header">
            <div class="fs-card__title">Programme Fee Structures
                <span class="fs-card__meta"><asp:Literal ID="litPFCount" runat="server" Text="0 structures" /></span>
            </div>
        </div>
        <div style="overflow-x:auto;">
            <table class="fs-table" id="tblPF">
                <thead>
                    <tr>
                        <th style="width:28px;text-align:center;padding:8px 6px;"><input type="checkbox" class="fs-check" id="chkSelectAll" onclick="toggleAllRows(this);" title="Select All" /></th>
                        <th style="width:32px">#</th>
                        <th>Programme</th>
                        <th style="text-align:center;width:72px">Yrs</th>
                        <th style="text-align:right;width:110px">Y1 Tuition</th>
                        <th style="text-align:right;width:120px">Grand Total</th>
                        <th style="text-align:center;width:70px">Status</th>
                        <th style="width:80px;text-align:center">Actions</th>
                    </tr>
                </thead>
                <tbody><asp:Literal ID="litPFRows" runat="server" /></tbody>
            </table>
        </div>
    </div>

    <!-- Batch Action Bar (sticky bottom, shows when rows selected) -->
    <div class="fs-batch-bar" id="batchBar">
        <div class="fs-batch-bar__info">
            <span class="fs-batch-bar__count" id="batchCount">0</span>
            <span class="fs-batch-bar__label">structures selected</span>
        </div>
        <div class="fs-batch-bar__actions">
            <button type="button" class="fs-batch-btn fs-batch-btn--activate" onclick="doBatchAction('ACTIVATE');" title="Activate selected structures">
                <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Activate
            </button>
            <button type="button" class="fs-batch-btn fs-batch-btn--deactivate" onclick="doBatchAction('DEACTIVATE');" title="Deactivate selected structures">
                <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M18.36 6.64a9 9 0 1 1-12.73 0"></path><line x1="12" y1="2" x2="12" y2="12"></line></svg> Deactivate
            </button>
            <button type="button" class="fs-batch-btn fs-batch-btn--adjust" onclick="openBatchAdjust();" title="Add or subtract a fixed amount from the selected structures">
                <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="1" x2="12" y2="23"></line><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path></svg> Adjust Fees
            </button>
            <button type="button" class="fs-batch-btn fs-batch-btn--delete" onclick="doBatchAction('DELETE');" title="Delete selected structures">
                <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg> Delete
            </button>
            <button type="button" class="fs-batch-btn fs-batch-btn--clear" onclick="clearAllSelections();">Clear</button>
        </div>
    </div>
</div>

<!-- ======= PANEL: Billing Items =================================== -->
<div id="panel-billing-items" class="fs-panel">
    <div class="fs-card">
        <div class="fs-card__header">
            <div class="fs-card__title">Academic Billing Items
                <span class="fs-card__meta"><asp:Literal ID="litBillingItemCount" runat="server" Text="0 items" /></span>
            </div>
            <button type="button" class="fs-btn fs-btn--primary fs-btn--sm" onclick="openModal('modal-billing-item');clearBillingItemForm();">+ Add Billing Item</button>
        </div>
        <div style="overflow-x:auto;">
            <table class="fs-table">
                <thead><tr><th>Code</th><th>Item Name</th><th>GL Account</th><th>Priority</th><th style="width:100px">Actions</th></tr></thead>
                <tbody><asp:Literal ID="litBillingItems" runat="server" /></tbody>
            </table>
        </div>
    </div>
</div>

<!-- ======= PANEL: Billing Systems ================================= -->
<div id="panel-billing-systems" class="fs-panel">
    <div class="fs-card">
        <div class="fs-card__header">
            <div class="fs-card__title">Billing Systems
                <span class="fs-card__meta"><asp:Literal ID="litSystemCount" runat="server" Text="0 systems" /></span>
            </div>
            <button type="button" class="fs-btn fs-btn--primary fs-btn--sm" onclick="openModal('modal-billing-system');clearBillingSystemForm();">+ Add Billing System</button>
        </div>
        <div style="overflow-x:auto;">
            <table class="fs-table">
                <thead><tr><th>ID</th><th>System Name</th><th>Description</th><th>Currency</th><th style="width:100px">Actions</th></tr></thead>
                <tbody><asp:Literal ID="litSystemRows" runat="server" /></tbody>
            </table>
        </div>
    </div>
</div>

<!-- ======= PANEL: Batch Billing =================================== -->
<div id="panel-batch-billing" class="fs-panel">

    <!-- Header -->
    <div class="bb-header">
        <div class="bb-header__left">
            <div class="bb-header__icon">
                <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
            </div>
            <div>
                <div class="bb-header__title">Batch Billing Processor</div>
                <div class="bb-header__sub">Process billing across all programmes with active fee structures at once</div>
            </div>
        </div>
        <div class="bb-header__badges">
            <span class="bb-badge bb-badge--year"><asp:Literal ID="litBBAcadYear" runat="server" Text="—" /></span>
            <span class="bb-badge bb-badge--sem"><asp:Literal ID="litBBActiveSems" runat="server" Text="—" /></span>
        </div>
    </div>

    <!-- Controls -->
    <div class="bb-controls">
        <button type="button" class="bb-gen-btn" id="btnBBGenPreview" onclick="bbGeneratePreview();">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
            Generate Batch Preview
        </button>
        <span style="font-size:11px;color:#888;">Scans all active fee structures to identify unbilled students</span>
    </div>

    <!-- Placeholder -->
    <div class="bb-placeholder" id="bbPlaceholder">
        <div>
            <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="1.5"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
            <div style="margin-top:8px;">Click <strong>Generate Batch Preview</strong> to scan all<br/>active fee structures and identify unbilled students.</div>
        </div>
    </div>

    <!-- Loading -->
    <div class="bb-progress" id="bbLoading">
        <div class="pb-progress__spinner"></div>
        <div class="bb-progress__text">Scanning all programmes&hellip;</div>
        <div class="bb-progress__detail">This may take a moment for large student populations.</div>
    </div>

    <!-- Preview content (populated by server) -->
    <div id="bbPreviewContent" style="display:none;">
        <!-- Summary stats -->
        <div class="bb-stats">
            <div class="bb-stat"><div class="bb-stat__val bb-stat__val--blue" id="bbStatProgs">0</div><div class="bb-stat__lbl">Programmes</div></div>
            <div class="bb-stat"><div class="bb-stat__val bb-stat__val--red" id="bbStatUnbilled">0</div><div class="bb-stat__lbl">To Be Billed</div></div>
            <div class="bb-stat"><div class="bb-stat__val bb-stat__val--green" id="bbStatBilled">0</div><div class="bb-stat__lbl">Already Billed</div></div>
            <div class="bb-stat"><div class="bb-stat__val bb-stat__val--amber" id="bbStatNoFee">0</div><div class="bb-stat__lbl">No Fee Defined</div></div>
            <div class="bb-stat"><div class="bb-stat__val bb-stat__val--purple" id="bbStatAmount">0</div><div class="bb-stat__lbl">Total to Bill</div></div>
        </div>

        <!-- Programme breakdown table -->
        <div class="fs-card">
            <div class="fs-card__header">
                <div class="fs-card__title">Programme Breakdown
                    <span class="fs-card__meta" id="bbTableMeta">0 programmes</span>
                </div>
            </div>
            <div style="overflow-x:auto;max-height:400px;overflow-y:auto;">
                <asp:Literal ID="litBBPreviewTable" runat="server" />
            </div>
        </div>

        <!-- Proceed bar -->
        <div class="bb-proceed-bar" id="bbProceedBar">
            <div class="bb-proceed-bar__info">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#f57c00" stroke-width="2" style="flex-shrink:0;"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
                This will process billing for ALL unbilled students across all listed programmes. This cannot be undone.
            </div>
            <button type="button" class="bb-exec-btn" id="btnBBExecute" onclick="bbExecuteBilling();" disabled="disabled">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
                Execute Batch Billing
            </button>
        </div>
    </div>

    <!-- Processing -->
    <div class="bb-progress" id="bbProcessing">
        <div class="pb-progress__spinner"></div>
        <div class="bb-progress__text">Processing batch billing&hellip;</div>
        <div class="bb-progress__detail">Billing students programme by programme. Please do not close this page.</div>
    </div>

    <!-- Result -->
    <div class="bb-result" id="bbResult">
        <asp:Literal ID="litBBResult" runat="server" />
    </div>
</div>


<!-- ======= MODALS ================================================= -->

<!-- Modal: Add/Edit Programme Fee Structure -->
<div id="modal-prog-fee" class="fs-modal-overlay">
<div class="fs-modal fs-modal--wide">
    <div class="fs-modal__header">
        <div class="fs-modal__title" id="modalPFTitle">Add Programme Fee Structure</div>
        <button type="button" class="fs-modal__close" onclick="closeModal('modal-prog-fee');">&times;</button>
    </div>
    <div class="fs-modal__body">
        <!-- Programme + Status -->
        <div class="pf-top-row">
            <div class="fs-form-group">
                <label class="fs-form-label">Programme <span style="color:#dc3545">*</span></label>
                <asp:DropDownList ID="ddlPFProg" runat="server" CssClass="fs-form-input" />
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">Status</label>
                <asp:DropDownList ID="ddlPFActive" runat="server" CssClass="fs-form-input">
                    <asp:ListItem Text="Active"   Value="Yes" />
                    <asp:ListItem Text="Inactive" Value="No" />
                </asp:DropDownList>
            </div>
            <div class="fs-form-group">
                <label class="fs-form-label">
                    Fee structure for
                    <span class="pf-help" title="In-service is a delivery mode, not a programme: every programme that runs in-service also runs day or weekend students. So in-service fees are held as a SEPARATE structure on the same programme, and each student is billed from the structure matching their own session.">&#9432;</span>
                </label>
                <asp:DropDownList ID="ddlPFSession" runat="server" CssClass="fs-form-input"
                                  AutoPostBack="true" OnSelectedIndexChanged="ddlPFSession_Changed">
                    <asp:ListItem Text="Standard (day / weekend)" Value="MAIN" />
                    <asp:ListItem Text="In-service"                Value="INSERVICE" />
                </asp:DropDownList>
            </div>
        </div>

        <!-- Which structure is on screen. Stated plainly, because the two structures share
             one set of inputs and editing the wrong one would silently reprice a cohort. -->
        <div id="pfSessionBannerDiv" runat="server" class="pf-session-banner">
            <asp:Literal ID="litPFSessionBanner" runat="server" />
        </div>

        <p style="font-size:10px;color:#888;margin:0 0 10px;font-style:italic;">
            Check each year to include it. Enter fees in UGX. Semester 3 can be left at 0 if not applicable.
        </p>

        <!-- YEAR 1 -->
        <div class="pf-year-section" id="pfYear1Section">
            <div class="pf-year-header" onclick="toggleYear(1)">
                <span class="pf-year-chk-label">
                    <asp:CheckBox ID="chkYear1" runat="server" onclick="event.stopPropagation();" />
                    <label style="cursor:pointer;" onclick="event.stopPropagation();">Year 1</label>
                </span>
                <span class="pf-year-meta">
                    <span class="pf-year-subtotal-badge" id="pfYear1SubBadge">UGX 0</span>
                    <span class="pf-toggle-arrow" id="pfYear1Arrow">&#9660;</span>
                </span>
            </div>
            <div class="pf-year-body pf-year-body--open" id="pfYear1Body">
                <table class="pf-fee-grid">
                    <thead><tr><th>Semester</th><th style="width:38%">Tuition (UGX)</th><th style="width:38%">Functional (UGX)</th><th style="width:16%">Sem Total</th></tr></thead>
                    <tbody>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 1</span></td>
                            <td><asp:TextBox ID="txtY1S1T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY1S1F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y1s1">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 2</span></td>
                            <td><asp:TextBox ID="txtY1S2T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY1S2F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y1s2">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 3</span></td>
                            <td><asp:TextBox ID="txtY1S3T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY1S3F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y1s3">0</td>
                        </tr>
                    </tbody>
                    <tfoot>
                        <tr><td colspan="3" class="pf-subtotal">Year 1 Total</td><td class="pf-subtotal" id="pfYear1Subtotal">0</td></tr>
                    </tfoot>
                </table>
            </div>
        </div>

        <!-- YEAR 2 -->
        <div class="pf-year-section" id="pfYear2Section">
            <div class="pf-year-header" onclick="toggleYear(2)">
                <span class="pf-year-chk-label">
                    <asp:CheckBox ID="chkYear2" runat="server" onclick="event.stopPropagation();" />
                    <label style="cursor:pointer;" onclick="event.stopPropagation();">Year 2</label>
                </span>
                <span class="pf-year-meta">
                    <span class="pf-year-subtotal-badge" id="pfYear2SubBadge">UGX 0</span>
                    <span class="pf-toggle-arrow" id="pfYear2Arrow">&#9660;</span>
                </span>
            </div>
            <div class="pf-year-body" id="pfYear2Body">
                <table class="pf-fee-grid">
                    <thead><tr><th>Semester</th><th style="width:38%">Tuition (UGX)</th><th style="width:38%">Functional (UGX)</th><th style="width:16%">Sem Total</th></tr></thead>
                    <tbody>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 1</span></td>
                            <td><asp:TextBox ID="txtY2S1T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY2S1F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y2s1">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 2</span></td>
                            <td><asp:TextBox ID="txtY2S2T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY2S2F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y2s2">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 3</span></td>
                            <td><asp:TextBox ID="txtY2S3T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY2S3F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y2s3">0</td>
                        </tr>
                    </tbody>
                    <tfoot>
                        <tr><td colspan="3" class="pf-subtotal">Year 2 Total</td><td class="pf-subtotal" id="pfYear2Subtotal">0</td></tr>
                    </tfoot>
                </table>
            </div>
        </div>

        <!-- YEAR 3 -->
        <div class="pf-year-section" id="pfYear3Section">
            <div class="pf-year-header" onclick="toggleYear(3)">
                <span class="pf-year-chk-label">
                    <asp:CheckBox ID="chkYear3" runat="server" onclick="event.stopPropagation();" />
                    <label style="cursor:pointer;" onclick="event.stopPropagation();">Year 3</label>
                </span>
                <span class="pf-year-meta">
                    <span class="pf-year-subtotal-badge" id="pfYear3SubBadge">UGX 0</span>
                    <span class="pf-toggle-arrow" id="pfYear3Arrow">&#9660;</span>
                </span>
            </div>
            <div class="pf-year-body" id="pfYear3Body">
                <table class="pf-fee-grid">
                    <thead><tr><th>Semester</th><th style="width:38%">Tuition (UGX)</th><th style="width:38%">Functional (UGX)</th><th style="width:16%">Sem Total</th></tr></thead>
                    <tbody>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 1</span></td>
                            <td><asp:TextBox ID="txtY3S1T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY3S1F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y3s1">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 2</span></td>
                            <td><asp:TextBox ID="txtY3S2T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY3S2F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y3s2">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 3</span></td>
                            <td><asp:TextBox ID="txtY3S3T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY3S3F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y3s3">0</td>
                        </tr>
                    </tbody>
                    <tfoot>
                        <tr><td colspan="3" class="pf-subtotal">Year 3 Total</td><td class="pf-subtotal" id="pfYear3Subtotal">0</td></tr>
                    </tfoot>
                </table>
            </div>
        </div>

        <!-- YEAR 4 -->
        <div class="pf-year-section" id="pfYear4Section">
            <div class="pf-year-header" onclick="toggleYear(4)">
                <span class="pf-year-chk-label">
                    <asp:CheckBox ID="chkYear4" runat="server" onclick="event.stopPropagation();" />
                    <label style="cursor:pointer;" onclick="event.stopPropagation();">Year 4</label>
                </span>
                <span class="pf-year-meta">
                    <span class="pf-year-subtotal-badge" id="pfYear4SubBadge">UGX 0</span>
                    <span class="pf-toggle-arrow" id="pfYear4Arrow">&#9660;</span>
                </span>
            </div>
            <div class="pf-year-body" id="pfYear4Body">
                <table class="pf-fee-grid">
                    <thead><tr><th>Semester</th><th style="width:38%">Tuition (UGX)</th><th style="width:38%">Functional (UGX)</th><th style="width:16%">Sem Total</th></tr></thead>
                    <tbody>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 1</span></td>
                            <td><asp:TextBox ID="txtY4S1T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY4S1F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y4s1">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 2</span></td>
                            <td><asp:TextBox ID="txtY4S2T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY4S2F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y4s2">0</td>
                        </tr>
                        <tr>
                            <td><span class="pf-sem-tag">Sem 3</span></td>
                            <td><asp:TextBox ID="txtY4S3T" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td><asp:TextBox ID="txtY4S3F" runat="server" CssClass="pf-amount-input" Text="0" oninput="recalcPF()" /></td>
                            <td class="pf-sem-total" id="pfST_y4s3">0</td>
                        </tr>
                    </tbody>
                    <tfoot>
                        <tr><td colspan="3" class="pf-subtotal">Year 4 Total</td><td class="pf-subtotal" id="pfYear4Subtotal">0</td></tr>
                    </tfoot>
                </table>
            </div>
        </div>

        <!-- Grand total bar -->
        <div class="pf-grand-total" id="pfGrandTotal">Grand Total: UGX 0</div>

    </div>
    <div class="fs-modal__footer">
        <button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-prog-fee');">Cancel</button>
        <asp:Button ID="btnSavePF" runat="server" Text="Save Fee Structure" CssClass="fs-btn fs-btn--primary" OnClick="btnSavePF_Click" />
    </div>
</div>
</div>

<!-- Modal: View Programme Fee Detail -->
<div id="modal-prog-fee-detail" class="fs-modal-overlay">
<div class="fs-modal fs-modal--wide">
    <div class="fs-modal__header">
        <div class="fs-modal__title" id="modalPFDetailTitle">Fee Structure Detail</div>
        <button type="button" class="fs-modal__close" onclick="closeModal('modal-prog-fee-detail');">&times;</button>
    </div>
    <div class="fs-modal__body">
        <div id="pfDetailContent"></div>
    </div>
    <div class="fs-modal__footer">
        <button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-prog-fee-detail');">Close</button>
    </div>
</div>
</div>

<!-- Modal: Add/Edit Billing Item -->
<div id="modal-billing-item" class="fs-modal-overlay">
<div class="fs-modal">
    <div class="fs-modal__header"><div class="fs-modal__title" id="modalBillingItemTitle">Add Billing Item</div><button type="button" class="fs-modal__close" onclick="closeModal('modal-billing-item');">&times;</button></div>
    <div class="fs-modal__body">
        <div class="fs-form-row"><div class="fs-form-group"><label class="fs-form-label">Item Name</label><asp:TextBox ID="txtBIName" runat="server" CssClass="fs-form-input" MaxLength="45" /></div></div>
        <div class="fs-form-row">
            <div class="fs-form-group"><label class="fs-form-label">GL Account Code</label><asp:TextBox ID="txtBIAccount" runat="server" CssClass="fs-form-input" MaxLength="45" /></div>
            <div class="fs-form-group"><label class="fs-form-label">Priority</label>
                <asp:DropDownList ID="ddlBIPriority" runat="server" CssClass="fs-form-input"><asp:ListItem Text="Core (billed automatically)" Value="1" /><asp:ListItem Text="Optional (manual only)" Value="0" /></asp:DropDownList></div>
        </div>
    </div>
    <div class="fs-modal__footer"><button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-billing-item');">Cancel</button><asp:Button ID="btnSaveBillingItem" runat="server" Text="Save Item" CssClass="fs-btn fs-btn--primary" OnClick="btnSaveBillingItem_Click" /></div>
</div></div>

<!-- Modal: Add/Edit Billing System -->
<div id="modal-billing-system" class="fs-modal-overlay">
<div class="fs-modal">
    <div class="fs-modal__header"><div class="fs-modal__title" id="modalBillingSystemTitle">Add Billing System</div><button type="button" class="fs-modal__close" onclick="closeModal('modal-billing-system');">&times;</button></div>
    <div class="fs-modal__body">
        <div class="fs-form-row"><div class="fs-form-group"><label class="fs-form-label">System Name</label><asp:TextBox ID="txtBSName" runat="server" CssClass="fs-form-input" MaxLength="45" /></div></div>
        <div class="fs-form-row">
            <div class="fs-form-group"><label class="fs-form-label">Description</label><asp:TextBox ID="txtBSDesc" runat="server" CssClass="fs-form-input" MaxLength="45" /></div>
            <div class="fs-form-group"><label class="fs-form-label">Currency</label><asp:TextBox ID="txtBSCurrency" runat="server" CssClass="fs-form-input" MaxLength="25" Text="UGX" /></div>
        </div>
    </div>
    <div class="fs-modal__footer"><button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-billing-system');">Cancel</button><asp:Button ID="btnSaveBillingSystem" runat="server" Text="Save System" CssClass="fs-btn fs-btn--primary" OnClick="btnSaveBillingSystem_Click" /></div>
</div></div>

<!-- Hidden postback buttons -->
<div style="display:none;">
    <asp:Button ID="btnDeleteRow" runat="server" OnClick="btnDeleteRow_Click" />
    <asp:Button ID="btnToggleActive" runat="server" OnClick="btnToggleActive_Click" />
    <asp:Button ID="btnBatchAction" runat="server" OnClick="btnBatchAction_Click" />
    <asp:HiddenField ID="hfBatchIds" runat="server" />
    <asp:HiddenField ID="hfBatchAction" runat="server" />
    <asp:HiddenField ID="hfBatchPercent" runat="server" />
    <asp:HiddenField ID="hfBatchFeeType" runat="server" />
    <asp:Button ID="btnPreviewBilling" runat="server" OnClick="btnPreviewBilling_Click" />
    <asp:Button ID="btnExecuteBilling" runat="server" OnClick="btnExecuteBilling_Click" />
    <asp:HiddenField ID="hfBillingPfId" runat="server" />
    <asp:Button ID="btnBBPreview" runat="server" OnClick="btnBBPreview_Click" />
    <asp:Button ID="btnBBExecute" runat="server" OnClick="btnBBExecute_Click" />
    <asp:Button ID="btnBillUnbilled" runat="server" OnClick="btnBillUnbilled_Click" />
</div>

<!-- Modal: Process Billing -->
<div id="modal-process-billing" class="fs-modal-overlay">
<div class="fs-modal fs-modal--xlarge">
    <div class="fs-modal__header">
        <div class="fs-modal__title" id="modalProcessBillingTitle">Process Billing</div>
        <button type="button" class="fs-modal__close" onclick="closeProcessBilling();">&times;</button>
    </div>
    <div class="fs-modal__body" style="padding:16px 24px;">
        <!-- Programme Info Header -->
        <div class="pb-header">
            <div>
                <span class="pb-header__prog" id="pbProgName"></span>
                <span class="pb-header__code" id="pbProgCode"></span>
            </div>
            <div class="pb-header__badge" id="pbAcadYear"></div>
        </div>

        <!-- Controls: Preview -->
        <div class="pb-controls">
            <button type="button" class="pb-preview-btn" id="btnPBPreview" onclick="previewBilling();">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                Generate Preview
            </button>
        </div>

        <!-- Placeholder before preview -->
        <div class="pb-placeholder" id="pbPlaceholder">
            <div>
                <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#ccc" stroke-width="1.5"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path></svg>
                <div style="margin-top:8px;">Click <strong>Generate Preview</strong> to see billing details<br/>for all semesters of this programme.</div>
            </div>
        </div>

        <!-- Loading spinner -->
        <div class="pb-progress" id="pbLoading">
            <div class="pb-progress__spinner"></div>
            <div class="pb-progress__text">Loading billing preview&hellip;</div>
        </div>

        <!-- Preview content (populated by server) -->
        <div id="pbPreviewContent" style="display:none;">
            <!-- Summary cards -->
            <div class="pb-summary" id="pbSummaryCards">
                <asp:Literal ID="litBillingSummary" runat="server" />
            </div>

            <!-- Two side-by-side panels -->
            <div class="pb-panels">
                <!-- LEFT: Students to be billed -->
                <div class="pb-panel pb-panel--left">
                    <div class="pb-panel__head">
                        <div class="pb-panel__title">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#1565c0" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="16"></line><line x1="8" y1="12" x2="16" y2="12"></line></svg>
                            To Be Billed
                            <span class="pb-panel__count" id="pbUnbilledCount">0</span>
                        </div>
                    </div>
                    <div class="pb-panel__body"><asp:Literal ID="litUnbilledStudents" runat="server" /></div>
                    <div class="pb-panel__foot">
                        <span class="pb-panel__total-label">Total Amount</span>
                        <span class="pb-panel__total-amount" id="pbUnbilledTotal">0</span>
                    </div>
                </div>

                <!-- RIGHT: Already billed students -->
                <div class="pb-panel pb-panel--right">
                    <div class="pb-panel__head">
                        <div class="pb-panel__title">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#2e7d32" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>
                            Already Billed
                            <span class="pb-panel__count" id="pbBilledCount">0</span>
                        </div>
                    </div>
                    <div class="pb-panel__body"><asp:Literal ID="litBilledStudents" runat="server" /></div>
                    <div class="pb-panel__foot">
                        <span class="pb-panel__total-label">Total Billed</span>
                        <span class="pb-panel__total-amount" id="pbBilledTotal">0</span>
                    </div>
                </div>
            </div>

            <!-- Footer: warning + proceed button -->
            <div class="pb-footer" id="pbFooter">
                <div class="pb-footer__info">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#f57c00" stroke-width="2" style="vertical-align:middle;"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>
                    This will create billing records and ledger entries. This action cannot be undone.
                </div>
                <button type="button" class="pb-proceed-btn" id="btnPBProceed" onclick="executeBilling();" disabled="disabled">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path></svg>
                    Proceed to Billing
                </button>
            </div>
        </div>

        <!-- Processing progress -->
        <div class="pb-progress" id="pbProcessing">
            <div class="pb-progress__spinner"></div>
            <div class="pb-progress__text">Processing billing&hellip; Please wait.</div>
        </div>

        <!-- Result -->
        <div class="pb-result" id="pbResult">
            <asp:Literal ID="litBillingResult" runat="server" />
        </div>
    </div>
</div>
</div>

<!-- Modal: Batch Fee Adjustment — four-step wizard -->
<div id="modal-batch-adjust" class="fs-modal-overlay">
<div class="fs-modal fs-modal--wizard">
    <div class="fs-modal__header">
        <div class="fs-modal__title">Batch Fee Adjustment</div>
        <button type="button" class="fs-modal__close" onclick="closeModal('modal-batch-adjust');">&times;</button>
    </div>

    <!-- Step rail -->
    <div class="fw-rail">
        <div class="fw-step is-on" data-step="1"><span class="fw-step__n">1</span><span class="fw-step__t">Structures</span></div>
        <div class="fw-step" data-step="2"><span class="fw-step__n">2</span><span class="fw-step__t">Adjustment</span></div>
        <div class="fw-step" data-step="3"><span class="fw-step__n">3</span><span class="fw-step__t">Where it applies</span></div>
        <div class="fw-step" data-step="4"><span class="fw-step__n">4</span><span class="fw-step__t">Review &amp; commit</span></div>
    </div>

    <div class="fs-modal__body fw-body">
        <div class="fw-msg" id="fwMsg"></div>

        <!-- ── STEP 1 ─────────────────────────────────────────────── -->
        <section class="fw-pane is-on" data-pane="1">
            <p class="fw-lead">These are the fee structures the adjustment will run against. Close this
               window and change your ticks if the list is not right.</p>
            <div class="fw-scroll" id="fwScope"></div>
        </section>

        <!-- ── STEP 2 ─────────────────────────────────────────────── -->
        <section class="fw-pane" data-pane="2">
            <p class="fw-lead">A fixed amount is added to or taken off each fee &mdash; not a percentage,
               so every programme moves by exactly the same figure.</p>

            <div class="fw-field">
                <label class="fw-label">Direction</label>
                <div class="fw-radios">
                    <label class="fw-radio fw-radio--up is-on" id="fwDirUpL">
                        <input type="radio" name="fwDir" id="fwDirUp" value="+" checked="checked" />
                        <span class="fw-radio__sign">+</span>
                        <span class="fw-radio__body"><b>Increase</b><em>Add to the current fee</em></span>
                    </label>
                    <label class="fw-radio fw-radio--down" id="fwDirDownL">
                        <input type="radio" name="fwDir" id="fwDirDown" value="-" />
                        <span class="fw-radio__sign">&minus;</span>
                        <span class="fw-radio__body"><b>Decrease</b><em>Take off the current fee</em></span>
                    </label>
                </div>
            </div>

            <div class="fw-field">
                <label class="fw-label">Amount per fee (UGX)</label>
                <div class="fw-amount">
                    <span class="fw-amount__sign" id="fwAmtSign">+</span>
                    <input type="text" id="fwAmount" class="fw-amount__in" inputmode="numeric" autocomplete="off" placeholder="e.g. 50,000" />
                </div>
                <span class="fw-hint" id="fwAmountWords">Enter a block figure. It is applied to each selected fee, not shared between them.</span>
            </div>

            <div class="fw-field">
                <label class="fw-label">Which fee</label>
                <div class="fw-radios">
                    <label class="fw-radio is-on" id="fwFtFuncL">
                        <input type="radio" name="fwFt" id="fwFtFunc" value="FUNCTIONAL" checked="checked" />
                        <span class="fw-radio__body"><b>Functional fee</b><em>Leaves tuition untouched</em></span>
                    </label>
                    <label class="fw-radio" id="fwFtTuitL">
                        <input type="radio" name="fwFt" id="fwFtTuit" value="TUITION" />
                        <span class="fw-radio__body"><b>Tuition</b><em>Leaves functional fees untouched</em></span>
                    </label>
                </div>
            </div>
        </section>

        <!-- ── STEP 3 ─────────────────────────────────────────────── -->
        <section class="fw-pane" data-pane="3">
            <p class="fw-lead">Tick the years of study and the semesters to change. Anything left unticked
               keeps its current figure.</p>

            <div class="fw-field">
                <label class="fw-label">Year of study</label>
                <div class="fw-checks" id="fwYears">
                    <label class="fw-check is-on"><input type="checkbox" class="fw-yr" value="1" checked="checked" /><span>Year 1</span></label>
                    <label class="fw-check is-on"><input type="checkbox" class="fw-yr" value="2" checked="checked" /><span>Year 2</span></label>
                    <label class="fw-check is-on"><input type="checkbox" class="fw-yr" value="3" checked="checked" /><span>Year 3</span></label>
                    <label class="fw-check is-on"><input type="checkbox" class="fw-yr" value="4" checked="checked" /><span>Year 4</span></label>
                </div>
                <span class="fw-hint">A year a programme does not offer is skipped automatically.</span>
            </div>

            <div class="fw-field">
                <label class="fw-label">Due semester</label>
                <div class="fw-checks" id="fwSems">
                    <label class="fw-check is-on"><input type="checkbox" class="fw-sem" value="1" checked="checked" /><span>Semester 1</span></label>
                    <label class="fw-check is-on"><input type="checkbox" class="fw-sem" value="2" checked="checked" /><span>Semester 2</span></label>
                    <label class="fw-check"><input type="checkbox" class="fw-sem" value="3" /><span>Semester 3</span></label>
                </div>
                <span class="fw-hint">Semester 3 is only charged on some programmes; where it is 0 it stays 0.</span>
            </div>

            <div class="fw-grid-note" id="fwCellCount"></div>
        </section>

        <!-- ── STEP 4 ─────────────────────────────────────────────── -->
        <section class="fw-pane" data-pane="4">
            <div class="fw-sentence" id="fwSentence"></div>
            <div class="fw-tally" id="fwTally"></div>
            <div class="fw-scroll fw-scroll--tall" id="fwReview"></div>
            <div class="fw-field" style="margin-top:12px;">
                <label class="fw-label">Reason / minute reference <span style="font-weight:400;color:#94a3b8;">(optional, stored with the batch)</span></label>
                <input type="text" id="fwNote" class="fw-amount__in" style="width:100%;" autocomplete="off" placeholder="e.g. Senate minute 14/2026 — functional fee revision" />
            </div>
        </section>
    </div>

    <div class="fs-modal__footer fw-foot">
        <button type="button" class="fs-btn fs-btn--ghost" onclick="closeModal('modal-batch-adjust');">Cancel</button>
        <span class="fw-foot__sp"></span>
        <button type="button" class="fs-btn fs-btn--ghost" id="fwBack" onclick="fwGo(-1);">Back</button>
        <button type="button" class="fs-btn fs-btn--primary" id="fwNext" onclick="fwGo(1);">Continue</button>
        <button type="button" class="fs-btn fs-btn--primary" id="fwCommit" style="display:none;background:#16a34a;border-color:#16a34a;" onclick="fwDoCommit();">Commit adjustment</button>
    </div>
</div>
</div>

<script type="text/javascript">
/* ===== FEE STRUCTURE JS ===== */
function showPanel(panelId, btn) {
    var panels = document.querySelectorAll('.fs-panel');
    for (var i = 0; i < panels.length; i++) panels[i].className = panels[i].className.replace(' fs-panel--active', '');
    var tabs = document.querySelectorAll('.fs-section-tab');
    for (var i = 0; i < tabs.length; i++) tabs[i].className = tabs[i].className.replace(' fs-section-tab--active', '');
    var el = document.getElementById('panel-' + panelId);
    if (el) el.className += ' fs-panel--active';
    if (btn) btn.className += ' fs-section-tab--active';
    var hf = document.getElementById('<%= hfActivePanel.ClientID %>');
    if (hf) hf.value = panelId;
}

function openModal(id) {
    var el = document.getElementById(id);
    if (el) el.className = 'fs-modal-overlay fs-modal-overlay--visible';
    if (id === 'modal-prog-fee') setTimeout(recalcPF, 50);
}
function closeModal(id) {
    var el = document.getElementById(id);
    if (el) el.className = 'fs-modal-overlay';
}

function toggleYear(yr) {
    var body  = document.getElementById('pfYear' + yr + 'Body');
    var arrow = document.getElementById('pfYear' + yr + 'Arrow');
    if (!body) return;
    if (body.className.indexOf('pf-year-body--open') >= 0) {
        body.className = 'pf-year-body';
        if (arrow) arrow.style.transform = 'rotate(-90deg)';
    } else {
        body.className = 'pf-year-body pf-year-body--open';
        if (arrow) arrow.style.transform = '';
    }
}

/* Live fee totals */
function recalcPF() {
    var fmt = function(n) { return Number(n).toLocaleString(); };
    var v   = function(id) { var el = document.getElementById(id); return el ? (parseFloat(el.value) || 0) : 0; };
    var set = function(id, n) { var el = document.getElementById(id); if (el) el.textContent = fmt(n); };
    var setBadge = function(id, n) { var el = document.getElementById(id); if (el) el.textContent = 'UGX ' + fmt(n); };

    // TextBox IDs rendered by ASP.NET include the form prefix
    var ids = [
        ['<%= txtY1S1T.ClientID %>','<%= txtY1S1F.ClientID %>','pfST_y1s1'],
        ['<%= txtY1S2T.ClientID %>','<%= txtY1S2F.ClientID %>','pfST_y1s2'],
        ['<%= txtY1S3T.ClientID %>','<%= txtY1S3F.ClientID %>','pfST_y1s3'],
        ['<%= txtY2S1T.ClientID %>','<%= txtY2S1F.ClientID %>','pfST_y2s1'],
        ['<%= txtY2S2T.ClientID %>','<%= txtY2S2F.ClientID %>','pfST_y2s2'],
        ['<%= txtY2S3T.ClientID %>','<%= txtY2S3F.ClientID %>','pfST_y2s3'],
        ['<%= txtY3S1T.ClientID %>','<%= txtY3S1F.ClientID %>','pfST_y3s1'],
        ['<%= txtY3S2T.ClientID %>','<%= txtY3S2F.ClientID %>','pfST_y3s2'],
        ['<%= txtY3S3T.ClientID %>','<%= txtY3S3F.ClientID %>','pfST_y3s3'],
        ['<%= txtY4S1T.ClientID %>','<%= txtY4S1F.ClientID %>','pfST_y4s1'],
        ['<%= txtY4S2T.ClientID %>','<%= txtY4S2F.ClientID %>','pfST_y4s2'],
        ['<%= txtY4S3T.ClientID %>','<%= txtY4S3F.ClientID %>','pfST_y4s3']
    ];
    var totals = [0,0,0,0,0,0,0,0,0,0,0,0];
    for (var i = 0; i < ids.length; i++) {
        var t = (parseFloat(document.getElementById(ids[i][0]) ? document.getElementById(ids[i][0]).value : 0) || 0)
              + (parseFloat(document.getElementById(ids[i][1]) ? document.getElementById(ids[i][1]).value : 0) || 0);
        totals[i] = t;
        set(ids[i][2], t);
    }
    var y1 = totals[0]+totals[1]+totals[2];
    var y2 = totals[3]+totals[4]+totals[5];
    var y3 = totals[6]+totals[7]+totals[8];
    var y4 = totals[9]+totals[10]+totals[11];
    set('pfYear1Subtotal', y1); setBadge('pfYear1SubBadge', y1);
    set('pfYear2Subtotal', y2); setBadge('pfYear2SubBadge', y2);
    set('pfYear3Subtotal', y3); setBadge('pfYear3SubBadge', y3);
    set('pfYear4Subtotal', y4); setBadge('pfYear4SubBadge', y4);
    var grand = y1+y2+y3+y4;
    var gEl = document.getElementById('pfGrandTotal');
    if (gEl) gEl.textContent = 'Grand Total: UGX ' + fmt(grand);
}
/* Run on modal open */
function initPFCalc() { recalcPF(); }

/* Action menu */
function toggleActionMenu(e, btn) {
    e.stopPropagation();
    var wrap = btn.parentNode;
    var menu = wrap.querySelector('.fs-action-menu');
    if (!menu) return;
    var isOpen = menu.className.indexOf('fs-action-menu--visible') >= 0;
    closeAllMenus();
    if (!isOpen) {
        menu.className = 'fs-action-menu fs-action-menu--visible';
        btn.className = 'fs-action-trigger fs-action-trigger--open';
    }
}
function closeAllMenus() {
    var menus = document.querySelectorAll('.fs-action-menu--visible');
    for (var i = 0; i < menus.length; i++) menus[i].className = 'fs-action-menu';
    var btns = document.querySelectorAll('.fs-action-trigger--open');
    for (var i = 0; i < btns.length; i++) btns[i].className = 'fs-action-trigger';
}
document.addEventListener('click', function () { closeAllMenus(); });

/* Edit / Toggle / Delete postback helpers */
function editPF(id) {
    var hfId = document.getElementById('<%= hfEditId.ClientID %>');
    var hfType = document.getElementById('<%= hfEditType.ClientID %>');
    if (hfId) hfId.value = id;
    if (hfType) hfType.value = 'PF_EDIT';
    __doPostBack('<%= btnToggleActive.UniqueID %>', '');
}
function toggleActive(id) {
    var hfId = document.getElementById('<%= hfEditId.ClientID %>');
    var hfType = document.getElementById('<%= hfEditType.ClientID %>');
    if (hfId) hfId.value = id;
    if (hfType) hfType.value = 'PF_TOGGLE';
    __doPostBack('<%= btnToggleActive.UniqueID %>', '');
}
function deleteRow(id, type, name) {
    if (!confirm('Delete fee structure for ' + name + '?')) return;
    var hfId = document.getElementById('<%= hfEditId.ClientID %>');
    var hfType = document.getElementById('<%= hfEditType.ClientID %>');
    if (hfId) hfId.value = id;
    if (hfType) hfType.value = type;
    __doPostBack('<%= btnDeleteRow.UniqueID %>', '');
}

/* View detail modal */
function viewPFDetail(id, code, name, hy1, hy2, hy3, hy4, active,
    y1s1t, y1s1f, y1s2t, y1s2f, y1s3t, y1s3f,
    y2s1t, y2s1f, y2s2t, y2s2f, y2s3t, y2s3f,
    y3s1t, y3s1f, y3s2t, y3s2f, y3s3t, y3s3f,
    y4s1t, y4s1f, y4s2t, y4s2f, y4s3t, y4s3f) {
    var fmt = function(n) { return Number(n).toLocaleString(); };
    var grand = y1s1t+y1s1f+y1s2t+y1s2f+y1s3t+y1s3f
              + y2s1t+y2s1f+y2s2t+y2s2f+y2s3t+y2s3f
              + y3s1t+y3s1f+y3s2t+y3s2f+y3s3t+y3s3f
              + y4s1t+y4s1f+y4s2t+y4s2f+y4s3t+y4s3f;
    var statusBadge = active === 'Yes'
        ? "<span class='fs-badge fs-badge--green'>Active</span>"
        : "<span class='fs-badge fs-badge--red'>Inactive</span>";
    var h = "<div style='margin-bottom:12px'><strong>" + name + "</strong> <span class='fs-code'>" + code + "</span> " + statusBadge + "</div>";
    h += "<table class='fs-table'><thead><tr><th>Year</th><th>Semester</th><th style=\"text-align:right\">Tuition</th><th style=\"text-align:right\">Functional</th><th style=\"text-align:right\">Total</th></tr></thead><tbody>";
    var rows = [
        [1,1,y1s1t,y1s1f],[1,2,y1s2t,y1s2f],[1,3,y1s3t,y1s3f],
        [2,1,y2s1t,y2s1f],[2,2,y2s2t,y2s2f],[2,3,y2s3t,y2s3f],
        [3,1,y3s1t,y3s1f],[3,2,y3s2t,y3s2f],[3,3,y3s3t,y3s3f],
        [4,1,y4s1t,y4s1f],[4,2,y4s2t,y4s2f],[4,3,y4s3t,y4s3f]
    ];
    var yFlags = {'1':hy1,'2':hy2,'3':hy3,'4':hy4};
    for (var i = 0; i < rows.length; i++) {
        var r = rows[i];
        if (yFlags[r[0]] !== 'Yes') continue;
        if (r[2] === 0 && r[3] === 0) continue;
        h += "<tr><td>Year " + r[0] + "</td><td>Sem " + r[1] + "</td><td style='text-align:right' class='fs-amount'>" + fmt(r[2]) + "</td><td style='text-align:right' class='fs-amount'>" + fmt(r[3]) + "</td><td style='text-align:right' class='fs-amount'><strong>" + fmt(r[2]+r[3]) + "</strong></td></tr>";
    }
    h += "</tbody></table>";
    h += "<div style='text-align:right;margin-top:10px;font-size:13px;font-weight:700;color:#174DA4'>Grand Total: " + fmt(grand) + "</div>";
    document.getElementById('pfDetailContent').innerHTML = h;
    document.getElementById('modalPFDetailTitle').innerText = 'Fee Structure #' + id + ' - ' + code;
    openModal('modal-prog-fee-detail');
}

/* Billing item/system form clearers */
function clearBillingItemForm() {
    document.getElementById('modalBillingItemTitle').innerText = 'Add Billing Item';
    document.getElementById('<%= hfEditId.ClientID %>').value = '';
    var el;
    el = document.getElementById('<%= txtBIName.ClientID %>'); if(el) el.value = '';
    el = document.getElementById('<%= txtBIAccount.ClientID %>'); if(el) el.value = '';
    el = document.getElementById('<%= ddlBIPriority.ClientID %>'); if(el) el.selectedIndex = 0;
}
function clearBillingSystemForm() {
    document.getElementById('modalBillingSystemTitle').innerText = 'Add Billing System';
    document.getElementById('<%= hfEditId.ClientID %>').value = '';
    var el;
    el = document.getElementById('<%= txtBSName.ClientID %>'); if(el) el.value = '';
    el = document.getElementById('<%= txtBSDesc.ClientID %>'); if(el) el.value = '';
    el = document.getElementById('<%= txtBSCurrency.ClientID %>'); if(el) el.value = 'UGX';
}

/* Edit billing item — populate modal and open */
function editBillingItem(code, name, acct, prio) {
    document.getElementById('modalBillingItemTitle').innerText = 'Edit Billing Item #' + code;
    document.getElementById('<%= hfEditId.ClientID %>').value = code;
    var el;
    el = document.getElementById('<%= txtBIName.ClientID %>'); if(el) el.value = name;
    el = document.getElementById('<%= txtBIAccount.ClientID %>'); if(el) el.value = acct;
    el = document.getElementById('<%= ddlBIPriority.ClientID %>'); if(el) el.value = prio;
    openModal('modal-billing-item');
}

/* Edit billing system — populate modal and open */
function editBillingSystem(id, name, desc, curr) {
    document.getElementById('modalBillingSystemTitle').innerText = 'Edit Billing System #' + id;
    document.getElementById('<%= hfEditId.ClientID %>').value = id;
    var el;
    el = document.getElementById('<%= txtBSName.ClientID %>'); if(el) el.value = name;
    el = document.getElementById('<%= txtBSDesc.ClientID %>'); if(el) el.value = desc;
    el = document.getElementById('<%= txtBSCurrency.ClientID %>'); if(el) el.value = curr;
    openModal('modal-billing-system');
}

/* Toast notification — called from server-side startup scripts */
function showToast(message, type) {
    // type: 'success', 'error', 'warning'
    var existing = document.getElementById('_jsToast');
    if (existing) existing.parentNode.removeChild(existing);

    var div = document.createElement('div');
    div.id = '_jsToast';
    div.style.cssText = 'position:fixed;top:16px;right:16px;z-index:99999;padding:12px 20px;' +
        'font-size:13px;font-weight:600;max-width:480px;box-shadow:0 4px 16px rgba(0,0,0,.15);' +
        'animation:fsToastIn .3s ease-out;border:1px solid transparent;';
    if (type === 'success') {
        div.style.background = '#e6f4ea'; div.style.color = '#155724'; div.style.borderColor = '#c3e6cb';
    } else if (type === 'error') {
        div.style.background = '#fde8e8'; div.style.color = '#c62828'; div.style.borderColor = '#f5c6cb';
    } else {
        div.style.background = '#fff8e1'; div.style.color = '#b45309'; div.style.borderColor = '#ffd54f';
    }
    div.textContent = message;
    document.body.appendChild(div);

    setTimeout(function() {
        if (div.parentNode) { div.style.opacity = '0'; div.style.transition = 'opacity .4s'; }
    }, 4000);
    setTimeout(function() { if (div.parentNode) div.parentNode.removeChild(div); }, 4500);
}

/* ===== BATCH OPERATIONS ===== */
function getSelectedIds() {
    var cbs = document.querySelectorAll('.fs-row-check:checked');
    var ids = [];
    for (var i = 0; i < cbs.length; i++) ids.push(cbs[i].getAttribute('data-id'));
    return ids;
}

function toggleRowCheck(cb) {
    var tr = cb.parentNode.parentNode;
    if (cb.checked) {
        tr.className = (tr.className || '') + ' fs-row--selected';
    } else {
        tr.className = (tr.className || '').replace(' fs-row--selected', '');
        document.getElementById('chkSelectAll').checked = false;
    }
    updateBatchBar();
}

function toggleAllRows(masterCb) {
    var cbs = document.querySelectorAll('.fs-row-check');
    for (var i = 0; i < cbs.length; i++) {
        cbs[i].checked = masterCb.checked;
        var tr = cbs[i].parentNode.parentNode;
        if (masterCb.checked) {
            if (tr.className.indexOf('fs-row--selected') < 0)
                tr.className = (tr.className || '') + ' fs-row--selected';
        } else {
            tr.className = (tr.className || '').replace(' fs-row--selected', '');
        }
    }
    updateBatchBar();
}

function updateBatchBar() {
    var ids = getSelectedIds();
    var bar = document.getElementById('batchBar');
    var cnt = document.getElementById('batchCount');
    if (ids.length > 0) {
        bar.className = 'fs-batch-bar fs-batch-bar--visible';
        cnt.innerText = ids.length;
    } else {
        bar.className = 'fs-batch-bar';
        cnt.innerText = '0';
    }
}

function clearAllSelections() {
    var masterCb = document.getElementById('chkSelectAll');
    if (masterCb) masterCb.checked = false;
    toggleAllRows(masterCb);
}

function doBatchAction(action) {
    var ids = getSelectedIds();
    if (ids.length === 0) { alert('No rows selected.'); return; }

    var msg = '';
    if (action === 'ACTIVATE') msg = 'Activate ' + ids.length + ' selected fee structure(s)?';
    else if (action === 'DEACTIVATE') msg = 'Deactivate ' + ids.length + ' selected fee structure(s)?';
    else if (action === 'DELETE') msg = 'DELETE ' + ids.length + ' selected fee structure(s)? This cannot be undone!';

    if (!confirm(msg)) return;

    var hfIds = document.getElementById('<%= hfBatchIds.ClientID %>');
    var hfAction = document.getElementById('<%= hfBatchAction.ClientID %>');
    if (hfIds) hfIds.value = ids.join(',');
    if (hfAction) hfAction.value = action;
    __doPostBack('<%= btnBatchAction.UniqueID %>', '');
}

/* ===== BATCH FEE ADJUSTMENT WIZARD ==========================================
   Four steps, and nothing is written until the last one. Step 4 asks the server
   what WOULD change and shows it cell by cell — old figure struck through, new
   figure beside it — so the adjustment is read before it is committed, not
   after. Changing anything on steps 2 or 3 throws the review away, so a commit
   can never act on a preview of different settings.
   ========================================================================== */
var _fwStep = 1, _fwIds = [], _fwPreview = null, _fwBusy = false;

function fwEl(id){ return document.getElementById(id); }
function fwEsc(s){ return (s==null?'':String(s)).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
function fwNum(n){ return (Number(n)||0).toLocaleString('en-US'); }
function fwSay(text, kind){
    var m = fwEl('fwMsg');
    if (!text) { m.className = 'fw-msg'; m.innerHTML = ''; return; }
    m.className = 'fw-msg show fw-msg--' + (kind || 'bad');
    m.innerHTML = text;
}
function fwAmtVal(){ return parseFloat((fwEl('fwAmount').value || '').replace(/[^0-9.]/g,'')) || 0; }
function fwDir(){ return fwEl('fwDirUp').checked ? '+' : '-'; }
function fwFeeType(){ return fwEl('fwFtFunc').checked ? 'FUNCTIONAL' : 'TUITION'; }
function fwFeeLabel(){ return fwFeeType() === 'FUNCTIONAL' ? 'functional fee' : 'tuition'; }
function fwPicked(cls){
    var out = [], b = document.querySelectorAll('.' + cls);
    for (var i=0;i<b.length;i++) if (b[i].checked) out.push(b[i].value);
    return out;
}

/* Any change to the settings invalidates a review that was already fetched. */
function fwDirty(){ _fwPreview = null; }

function openBatchAdjust() {
    _fwIds = getSelectedIds();
    if (_fwIds.length === 0) { alert('Tick the fee structures you want to adjust first.'); return; }

    fwEl('fwAmount').value = '';
    fwEl('fwNote').value = '';
    fwEl('fwDirUp').checked = true;
    fwEl('fwFtFunc').checked = true;
    fwPaintChoices();
    fwDirty();
    fwSay('');

    var h = '<table><thead><tr><th>#</th><th>Programme</th><th style="text-align:right;">Current total</th></tr></thead><tbody>';
    var cbs = document.querySelectorAll('.fs-row-check:checked');
    for (var i = 0; i < cbs.length; i++) {
        h += '<tr><td>' + (i+1) + '</td><td style="font-weight:600;">' + fwEsc(cbs[i].getAttribute('data-name') || '') +
             '</td><td style="text-align:right;font-weight:700;">' + fwNum(cbs[i].getAttribute('data-total') || 0) + '</td></tr>';
    }
    fwEl('fwScope').innerHTML = h + '</tbody></table>';

    _fwStep = 1; fwPaintStep();
    openModal('modal-batch-adjust');
}

function fwPaintChoices(){
    fwEl('fwDirUpL').className   = 'fw-radio fw-radio--up'   + (fwEl('fwDirUp').checked ? ' is-on' : '');
    fwEl('fwDirDownL').className = 'fw-radio fw-radio--down' + (fwEl('fwDirDown').checked ? ' is-on' : '');
    fwEl('fwFtFuncL').className  = 'fw-radio' + (fwEl('fwFtFunc').checked ? ' is-on' : '');
    fwEl('fwFtTuitL').className  = 'fw-radio' + (fwEl('fwFtTuit').checked ? ' is-on' : '');
    var up = fwEl('fwDirUp').checked;
    fwEl('fwAmtSign').textContent = up ? '+' : '−';
    fwEl('fwAmtSign').className = 'fw-amount__sign' + (up ? '' : ' is-down');

    var amt = fwAmtVal();
    fwEl('fwAmountWords').innerHTML = amt > 0
        ? 'Every selected ' + fwFeeLabel() + ' will ' + (up ? 'go up' : 'come down') + ' by <b>UGX ' + fwNum(amt) + '</b>.'
        : 'Enter a block figure. It is applied to each selected fee, not shared between them.';

    var boxes = document.querySelectorAll('.fw-yr, .fw-sem');
    for (var i=0;i<boxes.length;i++) boxes[i].parentNode.className = 'fw-check' + (boxes[i].checked ? ' is-on' : '');

    var yrs = fwPicked('fw-yr'), sems = fwPicked('fw-sem');
    var note = fwEl('fwCellCount');
    if (note) note.innerHTML = (yrs.length && sems.length)
        ? 'This targets <b>' + (yrs.length * sems.length) + '</b> fee cell(s) per programme &mdash; ' +
          yrs.length + ' year(s) &times; ' + sems.length + ' semester(s) &mdash; across <b>' + _fwIds.length + '</b> structure(s).'
        : 'Choose at least one year and one semester.';
}

function fwPaintStep(){
    var steps = document.querySelectorAll('.fw-step');
    for (var i=0;i<steps.length;i++){
        var n = parseInt(steps[i].getAttribute('data-step'),10);
        steps[i].className = 'fw-step' + (n === _fwStep ? ' is-on' : (n < _fwStep ? ' is-done' : ''));
    }
    var panes = document.querySelectorAll('.fw-pane');
    for (var j=0;j<panes.length;j++){
        var p = parseInt(panes[j].getAttribute('data-pane'),10);
        panes[j].className = 'fw-pane' + (p === _fwStep ? ' is-on' : '');
    }
    fwEl('fwBack').style.display   = _fwStep > 1 ? '' : 'none';
    fwEl('fwNext').style.display   = _fwStep < 4 ? '' : 'none';
    fwEl('fwCommit').style.display = _fwStep === 4 ? '' : 'none';
    fwEl('fwCommit').disabled = !(_fwPreview && _fwPreview.cellsChanged > 0);
}

function fwValidate(step){
    if (step === 2){
        if (fwAmtVal() <= 0) return 'Enter the amount to add or take off. It must be more than zero.';
        if (fwAmtVal() > 100000000) return 'That amount looks wrong (over 100,000,000). Check it before continuing.';
    }
    if (step === 3){
        if (fwPicked('fw-yr').length === 0)  return 'Choose at least one year of study.';
        if (fwPicked('fw-sem').length === 0) return 'Choose at least one semester.';
    }
    return '';
}

function fwGo(delta){
    fwSay('');
    if (delta > 0){
        var err = fwValidate(_fwStep);
        if (err) { fwSay(err); return; }
    }
    var next = _fwStep + delta;
    if (next < 1 || next > 4) return;
    _fwStep = next;
    if (_fwStep === 4) { fwPaintStep(); fwBuildReview(); }
    else fwPaintStep();
}

function fwQuery(){
    return 'ids=' + encodeURIComponent(_fwIds.join(',')) +
           '&feeType=' + encodeURIComponent(fwFeeType()) +
           '&direction=' + encodeURIComponent(fwDir()) +
           '&amount=' + encodeURIComponent(fwAmtVal()) +
           '&years=' + encodeURIComponent(fwPicked('fw-yr').join(',')) +
           '&sems=' + encodeURIComponent(fwPicked('fw-sem').join(',')) +
           '&note=' + encodeURIComponent(fwEl('fwNote').value || '');
}

function fwBuildReview(){
    fwEl('fwReview').innerHTML = '<div style="padding:14px;color:#94a3b8;font-size:11px;">Working out what would change&hellip;</div>';
    fwEl('fwTally').innerHTML = '';
    fwEl('fwSentence').innerHTML = '';
    _fwPreview = null; fwPaintStep();

    var x = new XMLHttpRequest();
    x.open('POST', 'FeesStructure.aspx?ajax=adjpreview', true);
    x.setRequestHeader('Content-Type','application/x-www-form-urlencoded');
    x.onreadystatechange = function(){
        if (x.readyState !== 4) return;
        var r = null; try { r = JSON.parse(x.responseText); } catch(e){}
        if (!r || !r.success){ fwSay((r && r.message) || 'Could not work out the change.'); fwEl('fwReview').innerHTML=''; return; }
        _fwPreview = r;

        var up = fwDir() === '+';
        fwEl('fwSentence').innerHTML =
            '<b>' + (up ? 'Increase' : 'Decrease') + '</b> the <b>' + fwFeeLabel() + '</b> by <b>UGX ' + fwNum(fwAmtVal()) + '</b> ' +
            'for <b>Year ' + fwPicked('fw-yr').join(', ') + '</b>, <b>Semester ' + fwPicked('fw-sem').join(', ') + '</b>, ' +
            'across <b>' + r.structures + '</b> fee structure(s).<br />' +
            'Total charged across the affected cells moves from <b>UGX ' + fwNum(r.totalBefore) + '</b> to ' +
            '<b>UGX ' + fwNum(r.totalAfter) + '</b> &mdash; a change of <b>' + (r.totalDelta >= 0 ? '+' : '−') +
            'UGX ' + fwNum(Math.abs(r.totalDelta)) + '</b>.';

        var t = '<span class="fw-pill ' + (r.cellsChanged ? 'fw-pill--go' : 'fw-pill--neutral') + '">' + r.cellsChanged + ' fee(s) will change</span>';
        if (r.skippedZero)     t += '<span class="fw-pill fw-pill--skip">' + r.skippedZero + ' not charged (left at 0)</span>';
        if (r.skippedNoYear)   t += '<span class="fw-pill fw-pill--skip">' + r.skippedNoYear + ' year not offered</span>';
        if (r.skippedNegative) t += '<span class="fw-pill fw-pill--skip">' + r.skippedNegative + ' would go below zero</span>';
        fwEl('fwTally').innerHTML = t;

        var h = '<table><thead><tr><th>Programme</th><th>Fee</th><th style="text-align:right;">Was</th><th style="text-align:right;">Becomes</th><th>Note</th></tr></thead><tbody>';
        for (var i=0;i<r.rows.length;i++){
            var row = r.rows[i];
            for (var c=0;c<row.cells.length;c++){
                var cell = row.cells[c], go = cell.verdict === 'CHANGE';
                h += '<tr' + (go ? '' : ' class="fw-skip"') + '>' +
                     '<td style="font-weight:600;">' + fwEsc(row.progcode) + '</td>' +
                     '<td>' + fwEsc(cell.label) + '</td>' +
                     '<td style="text-align:right;" class="' + (go ? 'fw-was' : '') + '">' + fwNum(cell.before) + '</td>' +
                     '<td style="text-align:right;" class="fw-now ' + (go ? (up ? 'fw-now--up' : 'fw-now--down') : '') + '">' +
                        (go ? fwNum(cell.after) : '&mdash;') + '</td>' +
                     '<td>' + fwEsc(cell.why) + '</td></tr>';
            }
        }
        fwEl('fwReview').innerHTML = h + '</tbody></table>';

        if (!r.cellsChanged) fwSay('Nothing would change with these settings. Adjust the amount, years or semesters.', 'bad');
        fwPaintStep();
    };
    x.onerror = function(){ fwSay('Network error while preparing the review.'); };
    x.send(fwQuery());
}

function fwDoCommit(){
    if (_fwBusy || !_fwPreview || !_fwPreview.cellsChanged) return;
    var up = fwDir() === '+';
    if (!confirm((up ? 'Increase' : 'Decrease') + ' ' + fwFeeLabel() + ' by UGX ' + fwNum(fwAmtVal()) +
                 ' on ' + _fwPreview.cellsChanged + ' fee(s) across ' + _fwPreview.structures + ' structure(s)?\n\n' +
                 'This changes what students are billed. The batch is recorded and can be reversed.')) return;

    _fwBusy = true;
    var btn = fwEl('fwCommit'); var orig = btn.innerHTML;
    btn.disabled = true; btn.innerHTML = 'Committing&hellip;';

    var x = new XMLHttpRequest();
    x.open('POST', 'FeesStructure.aspx?ajax=adjapply', true);
    x.setRequestHeader('Content-Type','application/x-www-form-urlencoded');
    x.onreadystatechange = function(){
        if (x.readyState !== 4) return;
        _fwBusy = false; btn.disabled = false; btn.innerHTML = orig;
        var r = null; try { r = JSON.parse(x.responseText); } catch(e){}
        if (!r || !r.success){ fwSay((r && r.message) || 'The adjustment did not go through. Nothing was changed.'); return; }
        alert(r.message);
        window.location.reload();
    };
    x.onerror = function(){ _fwBusy=false; btn.disabled=false; btn.innerHTML=orig; fwSay('Network error. Nothing was changed.'); };
    x.send(fwQuery());
}

/* Wire the live repaint + review invalidation once the DOM is up. */
(function fwWire(){
    function bind(){
        var watched = ['fwDirUp','fwDirDown','fwFtFunc','fwFtTuit'];
        for (var i=0;i<watched.length;i++){
            var el = fwEl(watched[i]);
            if (el) el.addEventListener('change', function(){ fwDirty(); fwPaintChoices(); });
        }
        var amt = fwEl('fwAmount');
        if (amt) amt.addEventListener('input', function(){ fwDirty(); fwPaintChoices(); });
        var boxes = document.querySelectorAll('.fw-yr, .fw-sem');
        for (var j=0;j<boxes.length;j++) boxes[j].addEventListener('change', function(){ fwDirty(); fwPaintChoices(); });
    }
    if (document.readyState !== 'loading') bind(); else document.addEventListener('DOMContentLoaded', bind);
})();

/* ===== PROCESS BILLING ===== */
var _pbPfId = 0;
var _pbProgCode = '';
var _pbProgName = '';
var _pbPreviewDone = false;

/* ===== BATCH BILLING ===== */
var _bbPreviewDone = false;

function bbGeneratePreview() {
    document.getElementById('bbPlaceholder').style.display = 'none';
    document.getElementById('bbPreviewContent').style.display = 'none';
    document.getElementById('bbResult').className = 'bb-result';
    document.getElementById('bbLoading').className = 'bb-progress bb-progress--visible';
    document.getElementById('btnBBGenPreview').disabled = true;
    __doPostBack('<%= btnBBPreview.UniqueID %>', '');
}

function bbExecuteBilling() {
    if (!_bbPreviewDone) { alert('Please generate a preview first.'); return; }
    var cnt = parseInt(document.getElementById('bbStatUnbilled').innerText) || 0;
    if (cnt === 0) { alert('There are no unbilled students to process.'); return; }
    if (!confirm('You are about to batch-bill ' + cnt + ' student-semester record(s) across all listed programmes.\n\nThis will create billing records and GL ledger entries.\nThis action CANNOT be undone.\n\nProceed?')) return;
    document.getElementById('bbPreviewContent').style.display = 'none';
    document.getElementById('bbProcessing').className = 'bb-progress bb-progress--visible';
    document.getElementById('btnBBExecute').disabled = true;
    __doPostBack('<%= btnBBExecute.UniqueID %>', '');
}

function confirmBillUnbilled() {
    var cnt = document.querySelector('.st-card__value--red');
    var n = cnt ? (parseInt(cnt.innerText.replace(/,/g,'')) || 0) : 0;
    if (n === 0) { alert('There are no unbilled students.'); return; }
    if (!confirm('This will bill ' + n + ' unbilled registered student(s) for the current academic year.\n\nBilling records and ledger entries will be created.\nThis action CANNOT be undone.\n\nProceed?')) return;
    __doPostBack('<%= btnBillUnbilled.UniqueID %>', '');
}

function openProcessBilling(pfId, progCode, progName) {
    _pbPfId = pfId;
    _pbProgCode = progCode;
    _pbProgName = progName;
    _pbPreviewDone = false;

    document.getElementById('modalProcessBillingTitle').innerText = 'Process Billing - ' + progCode;
    document.getElementById('pbProgName').innerText = progName;
    document.getElementById('pbProgCode').innerText = progCode;

    // Set current academic year from server
    var ayEl = document.getElementById('pbAcadYear');
    if (ayEl) ayEl.innerText = '<%= GetCurrentAcadYear() %>';

    // Reset state
    document.getElementById('pbPlaceholder').style.display = '';
    document.getElementById('pbLoading').className = 'pb-progress';
    document.getElementById('pbPreviewContent').style.display = 'none';
    document.getElementById('pbProcessing').className = 'pb-progress';
    document.getElementById('pbResult').className = 'pb-result';
    document.getElementById('btnPBProceed').disabled = true;
    document.getElementById('btnPBPreview').disabled = false;

    openModal('modal-process-billing');
}

function closeProcessBilling() {
    closeModal('modal-process-billing');
    // If billing was processed, reload page to refresh data
    if (document.getElementById('pbResult').className.indexOf('pb-result--visible') >= 0) {
        window.location.reload();
    }
}

function previewBilling() {
    if (!_pbPfId) return;

    // Set hidden fields
    var hfId = document.getElementById('<%= hfBillingPfId.ClientID %>');
    if (hfId) hfId.value = _pbPfId.toString();

    // Show loading
    document.getElementById('pbPlaceholder').style.display = 'none';
    document.getElementById('pbPreviewContent').style.display = 'none';
    document.getElementById('pbResult').className = 'pb-result';
    document.getElementById('pbLoading').className = 'pb-progress pb-progress--visible';
    document.getElementById('btnPBPreview').disabled = true;

    // Trigger postback for preview
    __doPostBack('<%= btnPreviewBilling.UniqueID %>', '');
}

function executeBilling() {
    if (!_pbPreviewDone) { alert('Please generate a preview first.'); return; }

    var unbilledCount = parseInt(document.getElementById('pbUnbilledCount').innerText) || 0;
    if (unbilledCount === 0) { alert('There are no unbilled students to process.'); return; }

    if (!confirm('You are about to bill ' + unbilledCount + ' student-semester record(s) for ' + _pbProgName + '.\n\nThis will create billing records and ledger entries.\nThis action CANNOT be undone.\n\nProceed?')) return;

    // Set hidden fields
    var hfId = document.getElementById('<%= hfBillingPfId.ClientID %>');
    if (hfId) hfId.value = _pbPfId.toString();

    // Show processing spinner
    document.getElementById('pbPreviewContent').style.display = 'none';
    document.getElementById('pbProcessing').className = 'pb-progress pb-progress--visible';
    document.getElementById('btnPBProceed').disabled = true;

    // Trigger postback
    __doPostBack('<%= btnExecuteBilling.UniqueID %>', '');
}

/* Restore active panel on postback */
(function() {
    var hf = document.getElementById('<%= hfActivePanel.ClientID %>');
    if (hf && hf.value && hf.value !== 'prog-fees') {
        // Map panel IDs to their actual tab element IDs
        var tabMap = {
            'prog-fees': 'tabProgFees',
            'billing-items': 'tabBillingItems',
            'billing-systems': 'tabBillingSystems',
            'batch-billing': 'tabBatchBilling'
        };
        var tabId = tabMap[hf.value];
        var btn = tabId ? document.getElementById(tabId) : null;
        showPanel(hf.value, btn);
    }
})();
</script>

</asp:Content>

