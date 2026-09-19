<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="Communications.aspx.cs" Inherits="COOPERP_NewScreens_Communications" Title="Communications - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===== COMMUNICATIONS PAGE (prefix: cm-) ================================ */

/* Stats Row */
.cm-stats { display: grid; grid-template-columns: repeat(5, 1fr); gap: 10px; margin-bottom: 14px; }
.cm-stat { background: #fff; border: 1px solid #e0e5ed; padding: 12px 14px; display: flex; align-items: center; gap: 10px; position: relative; overflow: hidden; }
.cm-stat::after { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 3px; background: var(--stat-c, #ccc); }
.cm-stat__icon { width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
.cm-stat__val { font-size: 15px; font-weight: 700; line-height: 1.2; font-variant-numeric: tabular-nums; }
.cm-stat__label { font-size: 9px; text-transform: uppercase; letter-spacing: .5px; color: #888; margin-top: 2px; }
.cm-stat--total    { --stat-c:#174DA4; } .cm-stat--total .cm-stat__icon    { background:#e8f0fc; } .cm-stat--total .cm-stat__val    { color:#174DA4; }
.cm-stat--published{ --stat-c:#2e7d32; } .cm-stat--published .cm-stat__icon{ background:#e6f4ea; } .cm-stat--published .cm-stat__val{ color:#2e7d32; }
.cm-stat--draft    { --stat-c:#e65100; } .cm-stat--draft .cm-stat__icon   { background:#fce8de; } .cm-stat--draft .cm-stat__val   { color:#e65100; }
.cm-stat--force    { --stat-c:#c62828; } .cm-stat--force .cm-stat__icon   { background:#fde0e0; } .cm-stat--force .cm-stat__val   { color:#c62828; }
.cm-stat--archived { --stat-c:#6a1b9a; } .cm-stat--archived .cm-stat__icon{ background:#f3e5f5; } .cm-stat--archived .cm-stat__val{ color:#6a1b9a; }

/* Header */
.cm-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 14px; }
.cm-header__left { display: flex; align-items: center; gap: 10px; }
.cm-header__icon { width: 36px; height: 36px; background: #05275C; display: flex; align-items: center; justify-content: center; }
.cm-header__icon svg { color: #fff; }
.cm-header__title { font-size: 15px; font-weight: 700; color: #05275C; }
.cm-header__sub   { font-size: 10px; color: #888; margin-top: 1px; }
.cm-header__right { display: flex; gap: 8px; }

/* Buttons */
.cm-btn { padding: 7px 14px; font-size: 11px; font-weight: 600; cursor: pointer; border: none; display: inline-flex; align-items: center; gap: 5px; transition: background .15s; }
.cm-btn--primary { background: #05275C; color: #fff; }
.cm-btn--primary:hover { background: #0a3a7a; }
.cm-btn--success { background: #2e7d32; color: #fff; }
.cm-btn--success:hover { background: #1b5e20; }
.cm-btn--danger  { background: #c62828; color: #fff; }
.cm-btn--danger:hover  { background: #b71c1c; }
.cm-btn--ghost   { background: transparent; color: #555; border: 1px solid #cdd3de; }
.cm-btn--ghost:hover   { background: #f5f7fa; }
.cm-btn--sm { padding: 4px 10px; font-size: 10px; }

/* Filter bar */
.cm-filters { display: flex; gap: 8px; margin-bottom: 12px; flex-wrap: wrap; align-items: flex-end; }
.cm-filters__group { display: flex; flex-direction: column; gap: 2px; }
.cm-filters__label { font-size: 9px; font-weight: 600; text-transform: uppercase; letter-spacing: .3px; color: #666; }
.cm-filters select, .cm-filters input[type=text] { padding: 6px 10px; font-size: 11px; border: 1px solid #cdd3de; background: #fff; min-width: 130px; }
.cm-filters select:focus, .cm-filters input[type=text]:focus { border-color: #174DA4; outline: none; }

/* Card */
.cm-card { background: #fff; border: 1px solid #e0e5ed; margin-bottom: 14px; }
.cm-card__header { padding: 10px 14px; border-bottom: 1px solid #e0e5ed; background: #f8f9fb; display: flex; align-items: center; justify-content: space-between; }
.cm-card__title  { font-size: 12px; font-weight: 700; color: #05275C; display: flex; align-items: center; gap: 6px; }

/* Table */
.cm-table { width: 100%; border-collapse: collapse; font-size: 11px; }
.cm-table th { background:#f5f7fa; padding:9px 12px; text-align:left; font-size:10px; text-transform:uppercase; letter-spacing:.3px; color:#555; font-weight:600; border-bottom:2px solid #e0e5ed; white-space:nowrap; }
.cm-table td { padding:8px 12px; border-bottom:1px solid #f0f2f5; color:#1a1a2e; vertical-align:middle; }
.cm-table tbody tr:hover { background:#f0f4ff; }
.cm-table td.cm-title-cell { max-width: 250px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

/* Badges */
.cm-badge { display:inline-block; padding:2px 8px; font-size:10px; font-weight:600; }
.cm-badge--published { background:#e6f4ea; color:#2e7d32; }
.cm-badge--draft     { background:#fff3e0; color:#e65100; }
.cm-badge--archived  { background:#f3e5f5; color:#6a1b9a; }
.cm-badge--student   { background:#e3f2fd; color:#1565c0; }
.cm-badge--staff     { background:#fce4ec; color:#c62828; }
.cm-badge--both      { background:#e8eaf6; color:#283593; }
.cm-badge--normal    { background:#f5f5f5; color:#616161; }
.cm-badge--high      { background:#fff3e0; color:#e65100; }
.cm-badge--urgent    { background:#fde0e0; color:#c62828; }
.cm-badge--force     { background:#fde0e0; color:#c62828; font-weight:700; }

/* Attachments chip */
.cm-att-chip { display:inline-flex; align-items:center; gap:4px; background:#f0f4ff; border:1px solid #c7d8f7; padding:2px 8px; font-size:10px; color:#174DA4; margin:2px; }
.cm-att-chip__remove { cursor:pointer; color:#c62828; font-weight:700; margin-left:4px; }

/* Read progress bar */
.cm-read-bar { width: 100px; height: 6px; background: #e0e5ed; display: inline-block; vertical-align: middle; margin-right: 6px; }
.cm-read-bar__fill { height: 100%; background: #2e7d32; transition: width .3s; }

/* ── Modal ─────────────────────────────────────────────────── */
.cm-overlay { display:none; position:fixed; inset:0; background:rgba(0,0,0,.45); z-index:9000; }
.cm-overlay.is-open { display:block; }
.cm-modal { display:none; position:fixed; top:50%; left:50%; transform:translate(-50%,-50%); background:#fff; z-index:9001; width:740px; max-height:90vh; overflow:auto; box-shadow: 0 8px 40px rgba(0,0,0,.25); }
.cm-modal.is-open { display:block; }
.cm-modal__header { padding:14px 18px; background:#05275C; color:#fff; display:flex; align-items:center; justify-content:space-between; }
.cm-modal__header h3 { font-size:14px; margin:0; }
.cm-modal__close { background:none; border:none; color:#fff; font-size:20px; cursor:pointer; line-height:1; }
.cm-modal__body { padding:18px; }
.cm-modal__footer { padding:12px 18px; border-top:1px solid #e0e5ed; display:flex; justify-content:flex-end; gap:8px; }

/* Form fields */
.cm-field { margin-bottom: 14px; }
.cm-field label { display:block; font-size:10px; font-weight:600; text-transform:uppercase; letter-spacing:.3px; color:#555; margin-bottom:4px; }
.cm-field input[type=text], .cm-field select, .cm-field textarea { width:100%; padding:8px 10px; font-size:12px; border:1px solid #cdd3de; box-sizing:border-box; font-family:inherit; }
.cm-field textarea { resize:vertical; min-height:140px; }
.cm-field input:focus, .cm-field select:focus, .cm-field textarea:focus { border-color:#174DA4; outline:none; }
.cm-field--row { display: flex; gap: 12px; }
.cm-field--row > .cm-field { flex: 1; margin-bottom: 0; }
/* quick-pick dates, hints and the force-read warning */
.cm-quick { display:flex; gap:6px; flex-wrap:wrap; margin-top:6px; }
.cm-quick__b { padding:4px 10px; font-size:11px; font-weight:500; background:#fff; border:1px solid #cdd3de;
               color:#174DA4; cursor:pointer; border-radius:0; font-family:inherit; }
.cm-quick__b:hover { background:#eef3fb; border-color:#174DA4; }
.cm-hint { font-size:11px; color:#888; margin-top:5px; line-height:1.45; }
.cm-hint--warn { color:#b45309; font-weight:600; }
.cm-warn { background:#fff8e1; border:1px solid #f4dba6; border-left:3px solid #d97706; color:#8a5a08;
           padding:10px 14px; font-size:11.5px; line-height:1.55; margin-bottom:14px; border-radius:2px; }
.cm-warn strong { display:block; color:#7a4e07; margin-bottom:2px; }
.cm-field--check { display:flex; align-items:center; gap:8px; }
.cm-field--check input[type=checkbox] { width:16px; height:16px; }
.cm-field--check label { display:inline; margin-bottom:0; }

/* Rich text toolbar */
.cm-editor-toolbar { display:flex; gap:2px; padding:6px 8px; background:#f8f9fb; border:1px solid #cdd3de; border-bottom:none; flex-wrap:wrap; }
.cm-editor-toolbar button { background:#fff; border:1px solid #ddd; padding:4px 8px; cursor:pointer; font-size:12px; color:#333; }
.cm-editor-toolbar button:hover { background:#e8f0fc; }
.cm-editor-toolbar button.active { background:#05275C; color:#fff; }
.cm-editor-frame { width:100%; height:240px; border:1px solid #cdd3de; box-sizing:border-box; }

/* Empty state */
.cm-empty { text-align:center; padding:40px 20px; color:#888; }
.cm-empty svg { margin-bottom:12px; opacity:.4; }
.cm-empty__title { font-size:14px; font-weight:600; margin-bottom:4px; }
.cm-empty__sub { font-size:11px; }

/* Spinner */
.cm-spinner { text-align:center; padding:30px; color:#888; font-size:11px; }

/* Action dots */
.cm-actions { position:relative; }
.cm-actions__trigger { background:none; border:1px solid #ddd; padding:4px 8px; cursor:pointer; font-size:14px; line-height:1; }
.cm-actions__trigger:hover { background:#f5f7fa; }
.cm-actions__menu { display:none; position:absolute; right:0; top:100%; background:#fff; border:1px solid #e0e5ed; box-shadow:0 4px 16px rgba(0,0,0,.12); z-index:100; min-width:150px; }
.cm-actions__menu.is-open { display:block; }
.cm-actions__item { display:block; width:100%; padding:8px 14px; font-size:11px; text-align:left; border:none; background:none; cursor:pointer; color:#333; }
.cm-actions__item:hover { background:#f0f4ff; }
.cm-actions__item--danger { color:#c62828; }

/* Preview panel */
.cm-preview { border:1px solid #e0e5ed; padding:16px; background:#fafbfc; max-height:300px; overflow:auto; }
.cm-preview h1,.cm-preview h2,.cm-preview h3 { margin-top:0; }

/* Read stats detail table */
.cm-read-detail { margin-top:10px; }

/* Responsive */
@media (max-width:900px) {
    .cm-stats { grid-template-columns: repeat(3, 1fr); }
    .cm-modal { width: 95vw; }
}
@media (max-width:600px) {
    .cm-stats { grid-template-columns: repeat(2, 1fr); }
    .cm-filters { flex-direction: column; }
}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- Stats Row -->
<div class="cm-stats" id="cmStats">
    <div class="cm-stat cm-stat--total"><div class="cm-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg></div><div><div class="cm-stat__val" id="statTotal">0</div><div class="cm-stat__label">Total</div></div></div>
    <div class="cm-stat cm-stat--published"><div class="cm-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg></div><div><div class="cm-stat__val" id="statPublished">0</div><div class="cm-stat__label">Published</div></div></div>
    <div class="cm-stat cm-stat--draft"><div class="cm-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg></div><div><div class="cm-stat__val" id="statDraft">0</div><div class="cm-stat__label">Drafts</div></div></div>
    <div class="cm-stat cm-stat--force"><div class="cm-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg></div><div><div class="cm-stat__val" id="statForce">0</div><div class="cm-stat__label">Force-Read</div></div></div>
    <div class="cm-stat cm-stat--archived"><div class="cm-stat__icon"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/></svg></div><div><div class="cm-stat__val" id="statArchived">0</div><div class="cm-stat__label">Archived</div></div></div>
</div>

<!-- Header -->
<div class="cm-header">
    <div class="cm-header__left">
        <div class="cm-header__icon"><svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg></div>
        <div>
            <div class="cm-header__title">Communications Management</div>
            <div class="cm-header__sub">Post notices &amp; announcements to students and staff</div>
        </div>
    </div>
    <div class="cm-header__right">
        <button type="button" class="cm-btn cm-btn--primary" onclick="cmOpenCreate()">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            New Communication
        </button>
    </div>
</div>

<!-- Filters -->
<div class="cm-filters">
    <div class="cm-filters__group">
        <div class="cm-filters__label">Status</div>
        <select id="fltStatus" onchange="cmLoad()">
            <option value="" selected="selected">All Statuses</option>
            <option value="PUBLISHED">Published</option>
            <option value="DRAFT">Draft</option>
            <option value="ARCHIVED">Archived</option>
        </select>
    </div>
    <div class="cm-filters__group">
        <div class="cm-filters__label">Audience</div>
        <select id="fltAudience" onchange="cmLoad()">
            <option value="">All Audiences</option>
            <option value="STUDENT">Students</option>
            <option value="STAFF">Staff</option>
            <option value="BOTH">Both</option>
        </select>
    </div>
    <div class="cm-filters__group">
        <div class="cm-filters__label">Priority</div>
        <select id="fltPriority" onchange="cmLoad()">
            <option value="">All Priorities</option>
            <option value="NORMAL">Normal</option>
            <option value="HIGH">High</option>
            <option value="URGENT">Urgent</option>
        </select>
    </div>
    <div class="cm-filters__group">
        <div class="cm-filters__label">Search</div>
        <input type="text" id="fltSearch" placeholder="Search title..." onkeyup="cmDebounceSearch()" />
    </div>
</div>

<!-- Communications Table -->
<div class="cm-card">
    <div class="cm-card__header">
        <div class="cm-card__title">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>
            Communications
            <span id="cmCount" style="font-weight:400; color:#888;"></span>
        </div>
    </div>
    <div id="cmTableWrap">
        <div class="cm-spinner">Loading communications...</div>
    </div>
</div>

<!-- ═══════ Create / Edit Modal ═══════ -->
<div class="cm-overlay" id="cmOverlay" onclick="cmCloseModal()"></div>
<div class="cm-modal" id="cmModal">
    <div class="cm-modal__header">
        <h3 id="cmModalTitle">New Communication</h3>
        <button type="button" class="cm-modal__close" onclick="cmCloseModal()">&times;</button>
    </div>
    <div class="cm-modal__body">
        <input type="hidden" id="cmEditId" value="0" />

        <div class="cm-field">
            <label>Title *</label>
            <input type="text" id="cmTitle" maxlength="500" placeholder="Enter communication title" />
        </div>

        <div class="cm-field--row">
            <div class="cm-field">
                <label>Target Audience *</label>
                <select id="cmAudience">
                    <option value="BOTH">Both (Students &amp; Staff)</option>
                    <option value="STUDENT">Students Only</option>
                    <option value="STAFF">Staff Only</option>
                </select>
            </div>
            <div class="cm-field">
                <label>Priority</label>
                <select id="cmPriority">
                    <option value="NORMAL">Normal</option>
                    <option value="HIGH">High</option>
                    <option value="URGENT">Urgent</option>
                </select>
            </div>
            <div class="cm-field">
                <label>Status</label>
                <select id="cmStatus">
                    <option value="DRAFT">Draft</option>
                    <option value="PUBLISHED">Published</option>
                </select>
            </div>
        </div>

        <div class="cm-field--row" style="margin-bottom:14px;">
            <div class="cm-field cm-field--check">
                <input type="checkbox" id="cmForceRead" onchange="cmToggleForceExpiry()" />
                <label for="cmForceRead">Force-Read (users must confirm they read this)</label>
            </div>
            <div class="cm-field" id="cmExpiryWrap" style="display:none;">
                <label>Stop forcing it after</label>
                <input type="date" id="cmExpiry" onchange="cmExpiryChanged()" />
                <div class="cm-quick">
                    <button type="button" class="cm-quick__b" onclick="cmSetExpiry('cmExpiry',7)">1 week</button>
                    <button type="button" class="cm-quick__b" onclick="cmSetExpiry('cmExpiry',14)">2 weeks</button>
                    <button type="button" class="cm-quick__b" onclick="cmSetExpiry('cmExpiry',30)">1 month</button>
                    <button type="button" class="cm-quick__b" onclick="cmSetExpiry('cmExpiry',90)">1 term</button>
                </div>
                <div class="cm-hint" id="cmExpiryHint">Covers the whole of the day you pick.</div>
            </div>
        </div>

        <div id="cmForceWarn" class="cm-warn" style="display:none;">
            <strong>This notice will block the portal until you remove it.</strong>
            Every student in the audience has to open it and confirm before they can reach any
            other page. With no end date that never stops on its own. Pick a date above unless
            you really mean it to run indefinitely.
        </div>

        <div class="cm-field--row" style="margin-bottom:14px;">
            <div class="cm-field cm-field--check">
                <input type="checkbox" id="cmShowMarquee" onchange="cmToggleMarqueeExpiry()" />
                <label for="cmShowMarquee">Show in Marquee (scrolling banner)</label>
            </div>
            <div class="cm-field" id="cmMarqueeExpiryWrap" style="display:none;">
                <label>Show in the banner until</label>
                <input type="date" id="cmMarqueeExpiry" onchange="cmExpiryChanged()" />
                <div class="cm-quick">
                    <button type="button" class="cm-quick__b" onclick="cmSetExpiry('cmMarqueeExpiry',7)">1 week</button>
                    <button type="button" class="cm-quick__b" onclick="cmSetExpiry('cmMarqueeExpiry',14)">2 weeks</button>
                    <button type="button" class="cm-quick__b" onclick="cmSetExpiry('cmMarqueeExpiry',30)">1 month</button>
                </div>
                <div class="cm-hint">Leave empty to keep it in the banner until you remove it.</div>
            </div>
        </div>

        <div class="cm-field cm-field--check" style="margin-bottom:14px;">
            <input type="checkbox" id="cmAllowComments" checked="checked" />
            <label for="cmAllowComments">Allow Comments</label>
        </div>

        <div class="cm-field">
            <label>Content *</label>
            <div class="cm-editor-toolbar" id="cmToolbar">
                <button type="button" onclick="cmExecCmd('bold')" title="Bold"><b>B</b></button>
                <button type="button" onclick="cmExecCmd('italic')" title="Italic"><i>I</i></button>
                <button type="button" onclick="cmExecCmd('underline')" title="Underline"><u>U</u></button>
                <button type="button" onclick="cmExecCmd('insertUnorderedList')" title="Bullet List">&#8226; List</button>
                <button type="button" onclick="cmExecCmd('insertOrderedList')" title="Numbered List">1. List</button>
                <button type="button" onclick="cmExecCmd('formatBlock','<h3>')" title="Heading">H3</button>
                <button type="button" onclick="cmExecCmd('formatBlock','<p>')" title="Paragraph">P</button>
                <button type="button" onclick="cmInsertLink()" title="Insert Link">&#128279; Link</button>
                <button type="button" onclick="cmInsertTable()" title="Insert Table">&#9638; Table</button>
            </div>
            <iframe id="cmEditor" class="cm-editor-frame"></iframe>
        </div>

        <div class="cm-field">
            <label>Attachments</label>
            <input type="file" id="cmFileInput" multiple="multiple" onchange="cmQueueFiles()" accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.jpg,.jpeg,.png,.gif,.mp4,.mov,.avi" />
            <div id="cmAttachments" style="margin-top:6px;"></div>
        </div>
    </div>
    <div class="cm-modal__footer">
        <button type="button" class="cm-btn cm-btn--ghost" onclick="cmCloseModal()">Cancel</button>
        <button type="button" class="cm-btn cm-btn--primary" id="cmSaveBtn" onclick="cmSave()">Save Communication</button>
    </div>
</div>

<!-- ═══════ Read Stats Modal ═══════ -->
<div class="cm-overlay" id="cmReadOverlay" onclick="cmCloseReadModal()"></div>
<div class="cm-modal" id="cmReadModal" style="width:600px;">
    <div class="cm-modal__header">
        <h3 id="cmReadTitle">Read Statistics</h3>
        <button type="button" class="cm-modal__close" onclick="cmCloseReadModal()">&times;</button>
    </div>
    <div class="cm-modal__body">
        <div id="cmReadContent"><div class="cm-spinner">Loading...</div></div>
    </div>
    <div class="cm-modal__footer">
        <button type="button" class="cm-btn cm-btn--ghost" onclick="cmCloseReadModal()">Close</button>
    </div>
</div>

<!-- ═══════ Preview Modal ═══════ -->
<div class="cm-overlay" id="cmPrevOverlay" onclick="cmClosePrevModal()"></div>
<div class="cm-modal" id="cmPrevModal" style="width:660px;">
    <div class="cm-modal__header">
        <h3 id="cmPrevTitle">Preview</h3>
        <button type="button" class="cm-modal__close" onclick="cmClosePrevModal()">&times;</button>
    </div>
    <div class="cm-modal__body">
        <div id="cmPrevContent" class="cm-preview"></div>
        <div id="cmPrevAttachments" style="margin-top:10px;"></div>
    </div>
    <div class="cm-modal__footer">
        <button type="button" class="cm-btn cm-btn--ghost" onclick="cmClosePrevModal()">Close</button>
    </div>
</div>

<script type="text/javascript">
/* ===== Communications Manager — Client-side JS ========================== */
(function () {
    var PAGE = window.location.pathname;
    var dataCache = [];
    var editingAttachments = [];    // [{id,name,isNew,file}]
    var searchTimer = null;

    // ─── Helpers ─────────────────────────────────────────────────
    function qs(sel) { return document.querySelector(sel); }
    function qsa(sel) { return document.querySelectorAll(sel); }
    function val(sel) { var el = qs(sel); return el ? el.value.trim() : ''; }
    function setVal(sel, v) { var el = qs(sel); if (el) el.value = v; }

    function ajax(action, body, cb) {
        var url = PAGE + '?ajax=' + action;
        var xhr = new XMLHttpRequest();
        if (body) {
            xhr.open('POST', url, true);
            xhr.setRequestHeader('Content-Type', 'application/json');
        } else {
            xhr.open('GET', url, true);
        }
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;
            if (xhr.status === 200) {
                try { cb(JSON.parse(xhr.responseText)); }
                catch (ex) { cb({ ok: false, error: 'Invalid response' }); }
            } else {
                cb({ ok: false, error: 'HTTP ' + xhr.status });
            }
        };
        xhr.send(body ? JSON.stringify(body) : null);
    }

    function uploadFile(commId, file, cb) {
        var fd = new FormData();
        fd.append('file', file);
        var xhr = new XMLHttpRequest();
        xhr.open('POST', PAGE + '?ajax=upload&commId=' + commId, true);
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) return;
            try { cb(JSON.parse(xhr.responseText)); }
            catch (ex) { cb({ ok: false, error: 'Upload failed' }); }
        };
        xhr.send(fd);
    }

    function esc(s) { if (!s) return ''; var d = document.createElement('div'); d.appendChild(document.createTextNode(s)); return d.innerHTML; }

    function fmtDate(d) {
        if (!d) return '-';
        var dt = new Date(d);
        if (isNaN(dt.getTime())) return d;
        var dd = ('0' + dt.getDate()).slice(-2);
        var mm = ('0' + (dt.getMonth() + 1)).slice(-2);
        return dd + '/' + mm + '/' + dt.getFullYear() + ' ' + ('0' + dt.getHours()).slice(-2) + ':' + ('0' + dt.getMinutes()).slice(-2);
    }

    function fmtSize(bytes) {
        if (!bytes || bytes === 0) return '0 B';
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / 1048576).toFixed(1) + ' MB';
    }

    // ─── Rich Text Editor ────────────────────────────────────────
    function initEditor(html) {
        var frame = qs('#cmEditor');
        var doc = frame.contentDocument || frame.contentWindow.document;
        doc.open();
        doc.write('<!DOCTYPE html><html><head><style>body{font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1a1a2e;padding:10px;margin:0;} table{border-collapse:collapse;} td,th{border:1px solid #ccc;padding:6px 10px;}</style></head><body contenteditable="true">' + (html || '') + '</body></html>');
        doc.close();
    }

    function getEditorContent() {
        var frame = qs('#cmEditor');
        var doc = frame.contentDocument || frame.contentWindow.document;
        return doc.body.innerHTML;
    }

    window.cmExecCmd = function (cmd, val) {
        var frame = qs('#cmEditor');
        var doc = frame.contentDocument || frame.contentWindow.document;
        frame.contentWindow.focus();
        doc.execCommand(cmd, false, val || null);
    };

    window.cmInsertLink = function () {
        var url = prompt('Enter URL:', 'https://');
        if (url) {
            var frame = qs('#cmEditor');
            frame.contentWindow.focus();
            frame.contentDocument.execCommand('createLink', false, url);
        }
    };

    window.cmInsertTable = function () {
        var rows = prompt('Number of rows:', '3');
        var cols = prompt('Number of columns:', '3');
        if (!rows || !cols) return;
        var r = parseInt(rows), c = parseInt(cols);
        if (isNaN(r) || isNaN(c) || r < 1 || c < 1) return;
        var html = '<table>';
        for (var i = 0; i < r; i++) {
            html += '<tr>';
            for (var j = 0; j < c; j++) html += (i === 0 ? '<th>Header</th>' : '<td>&nbsp;</td>');
            html += '</tr>';
        }
        html += '</table><p>&nbsp;</p>';
        var frame = qs('#cmEditor');
        frame.contentWindow.focus();
        frame.contentDocument.execCommand('insertHTML', false, html);
    };

    // ─── Load Communications ─────────────────────────────────────
    window.cmLoad = function () {
        var fStatus = val('#fltStatus');
        var fAud = val('#fltAudience');
        var fPri = val('#fltPriority');
        var fSearch = val('#fltSearch');
        var params = [];
        if (fStatus) params.push('status=' + encodeURIComponent(fStatus));
        if (fAud) params.push('audience=' + encodeURIComponent(fAud));
        if (fPri) params.push('priority=' + encodeURIComponent(fPri));
        if (fSearch) params.push('search=' + encodeURIComponent(fSearch));

        qs('#cmTableWrap').innerHTML = '<div class="cm-spinner">Loading communications...</div>';

        ajax('list' + (params.length ? '&' + params.join('&') : ''), null, function (r) {
            if (!r.ok) { qs('#cmTableWrap').innerHTML = '<div class="cm-empty"><div class="cm-empty__title">Error</div><div class="cm-empty__sub">' + esc(r.error) + '</div></div>'; return; }
            dataCache = r.rows || [];
            renderStats(r.stats);
            renderTable(dataCache);
        });
    };

    function renderStats(s) {
        if (!s) return;
        qs('#statTotal').textContent     = s.total || 0;
        qs('#statPublished').textContent = s.published || 0;
        qs('#statDraft').textContent     = s.draft || 0;
        qs('#statForce').textContent     = s.forceRead || 0;
        qs('#statArchived').textContent  = s.archived || 0;
    }

    function renderTable(rows) {
        if (!rows || rows.length === 0) {
            qs('#cmTableWrap').innerHTML = '<div class="cm-empty"><svg xmlns="http://www.w3.org/2000/svg" width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg><div class="cm-empty__title">No communications found</div><div class="cm-empty__sub">Create a new communication to get started</div></div>';
            qs('#cmCount').textContent = '';
            return;
        }
        qs('#cmCount').textContent = '(' + rows.length + ')';
        var h = '<table class="cm-table"><thead><tr>' +
            '<th>Title</th><th>Audience</th><th>Priority</th><th>Status</th><th>Force</th><th>Read</th><th>Published</th><th style="width:50px;">Actions</th>' +
            '</tr></thead><tbody>';
        for (var i = 0; i < rows.length; i++) {
            var r = rows[i];
            var statusCls = r.status === 'PUBLISHED' ? 'published' : (r.status === 'DRAFT' ? 'draft' : 'archived');
            var audCls = r.target_audience === 'STUDENT' ? 'student' : (r.target_audience === 'STAFF' ? 'staff' : 'both');
            var priCls = r.priority === 'HIGH' ? 'high' : (r.priority === 'URGENT' ? 'urgent' : 'normal');
            var readPct = r.readCount > 0 ? Math.round((r.confirmedCount / Math.max(r.readCount, 1)) * 100) : 0;

            h += '<tr>' +
                '<td class="cm-title-cell" title="' + esc(r.title) + '">' + esc(r.title) + (r.attachCount > 0 ? ' <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="#888" stroke-width="2" style="vertical-align:middle;"><path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>' + r.attachCount : '') + '</td>' +
                '<td><span class="cm-badge cm-badge--' + audCls + '">' + esc(r.target_audience) + '</span></td>' +
                '<td><span class="cm-badge cm-badge--' + priCls + '">' + esc(r.priority) + '</span></td>' +
                '<td><span class="cm-badge cm-badge--' + statusCls + '">' + esc(r.status) + '</span></td>' +
                '<td>' + (r.is_force_read ? '<span class="cm-badge cm-badge--force">YES</span>' : '-') + '</td>' +
                '<td><div class="cm-read-bar"><div class="cm-read-bar__fill" style="width:' + readPct + '%"></div></div>' + r.readCount + ' read / ' + r.confirmedCount + ' confirmed</td>' +
                '<td>' + fmtDate(r.published_at) + '</td>' +
                '<td>' +
                    '<div class="cm-actions">' +
                        '<button type="button" class="cm-actions__trigger" onclick="cmToggleActions(this)">&#8942;</button>' +
                        '<div class="cm-actions__menu">' +
                            '<button type="button" class="cm-actions__item" onclick="cmPreview(' + r.ID + ')">&#128065; Preview</button>' +
                            '<button type="button" class="cm-actions__item" onclick="cmEdit(' + r.ID + ')">&#9998; Edit</button>' +
                            '<button type="button" class="cm-actions__item" onclick="cmViewReads(' + r.ID + ')">&#128202; Read Stats</button>' +
                            (r.status === 'DRAFT' ? '<button type="button" class="cm-actions__item" onclick="cmPublish(' + r.ID + ')">&#10003; Publish</button>' : '') +
                            (r.status === 'PUBLISHED' ? '<button type="button" class="cm-actions__item" onclick="cmArchive(' + r.ID + ')">&#128230; Archive</button>' : '') +
                            '<button type="button" class="cm-actions__item cm-actions__item--danger" onclick="cmDelete(' + r.ID + ')">&#128465; Delete</button>' +
                        '</div>' +
                    '</div>' +
                '</td>' +
                '</tr>';
        }
        h += '</tbody></table>';
        qs('#cmTableWrap').innerHTML = h;
    }

    window.cmToggleActions = function (btn) {
        var menu = btn.nextElementSibling;
        var wasOpen = menu.classList.contains('is-open');
        qsa('.cm-actions__menu.is-open').forEach(function (m) { m.classList.remove('is-open'); });
        if (!wasOpen) menu.classList.add('is-open');
    };

    document.addEventListener('click', function (e) {
        if (!e.target.closest('.cm-actions')) {
            qsa('.cm-actions__menu.is-open').forEach(function (m) { m.classList.remove('is-open'); });
        }
    });

    // ─── Debounced search ────────────────────────────────────────
    window.cmDebounceSearch = function () {
        if (searchTimer) clearTimeout(searchTimer);
        searchTimer = setTimeout(function () { cmLoad(); }, 300);
    };

    // ─── Modal ───────────────────────────────────────────────────
    window.cmOpenCreate = function () {
        qs('#cmEditId').value = '0';
        qs('#cmModalTitle').textContent = 'New Communication';
        qs('#cmTitle').value = '';
        qs('#cmAudience').value = 'BOTH';
        qs('#cmPriority').value = 'NORMAL';
        qs('#cmStatus').value = 'DRAFT';
        qs('#cmForceRead').checked = false;
        qs('#cmExpiry').value = '';
        qs('#cmExpiryWrap').style.display = 'none';
        qs('#cmAllowComments').checked = true;
        qs('#cmShowMarquee').checked = false;
        qs('#cmMarqueeExpiry').value = '';
        qs('#cmMarqueeExpiryWrap').style.display = 'none';
        cmExpiryChanged();
        editingAttachments = [];
        renderAttachmentChips();
        qs('#cmFileInput').value = '';
        initEditor('');
        qs('#cmOverlay').classList.add('is-open');
        qs('#cmModal').classList.add('is-open');
    };

    window.cmCloseModal = function () {
        qs('#cmOverlay').classList.remove('is-open');
        qs('#cmModal').classList.remove('is-open');
    };

    window.cmToggleForceExpiry = function () {
        var on = qs('#cmForceRead').checked;
        qs('#cmExpiryWrap').style.display = on ? '' : 'none';
        // Ticking force-read with no date is the setting that blocks the portal indefinitely,
        // so offer a sensible one rather than leaving it empty and hoping.
        if (on && !qs('#cmExpiry').value) cmSetExpiry('cmExpiry', 14);
        cmExpiryChanged();
    };

    window.cmToggleMarqueeExpiry = function () {
        qs('#cmMarqueeExpiryWrap').style.display = qs('#cmShowMarquee').checked ? '' : 'none';
        cmExpiryChanged();
    };

    // Today + n days, as YYYY-MM-DD for a native date input.
    window.cmSetExpiry = function (fieldId, days) {
        var d = new Date();
        d.setDate(d.getDate() + days);
        var p = function (n) { return n < 10 ? '0' + n : '' + n; };
        var el = qs('#' + fieldId);
        if (el) { el.value = d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()); }
        cmExpiryChanged();
    };

    // Say plainly what the chosen date means, and shout when there is none.
    window.cmExpiryChanged = function () {
        var forced = qs('#cmForceRead') && qs('#cmForceRead').checked;
        var val = qs('#cmExpiry') ? qs('#cmExpiry').value : '';
        var warn = qs('#cmForceWarn');
        var hint = qs('#cmExpiryHint');

        if (warn) warn.style.display = (forced && !val) ? '' : 'none';

        if (hint) {
            if (!val) {
                hint.textContent = 'No end date - it keeps blocking until you remove it.';
                hint.className = 'cm-hint cm-hint--warn';
            } else {
                var parts = val.split('-');
                var d = new Date(+parts[0], +parts[1] - 1, +parts[2]);
                var days = Math.ceil((d - new Date()) / 86400000);
                var when = d.toDateString();
                hint.textContent = days < 0
                    ? 'That date has passed, so this will not be forced on anyone.'
                    : 'Forced until the end of ' + when + (days <= 1 ? ' (today).' : ' - about ' + days + ' days.');
                hint.className = days < 0 ? 'cm-hint cm-hint--warn' : 'cm-hint';
            }
        }
    };

    // ─── Edit ────────────────────────────────────────────────────
    window.cmEdit = function (id) {
        ajax('get&id=' + id, null, function (r) {
            if (!r.ok) { alert(r.error || 'Failed to load'); return; }
            var c = r.comm;
            qs('#cmEditId').value = c.ID;
            qs('#cmModalTitle').textContent = 'Edit Communication';
            qs('#cmTitle').value = c.title || '';
            qs('#cmAudience').value = c.target_audience || 'BOTH';
            qs('#cmPriority').value = c.priority || 'NORMAL';
            qs('#cmStatus').value = c.status || 'DRAFT';
            qs('#cmForceRead').checked = !!c.is_force_read;
            qs('#cmExpiry').value = c.force_read_expiry ? c.force_read_expiry.substring(0, 10) : '';
            qs('#cmExpiryWrap').style.display = c.is_force_read ? '' : 'none';
            qs('#cmAllowComments').checked = c.allow_comments !== false && c.allow_comments !== 0;
            qs('#cmShowMarquee').checked = !!c.show_in_marquee;
            qs('#cmMarqueeExpiry').value = c.marquee_expiry ? c.marquee_expiry.substring(0, 10) : '';
            qs('#cmMarqueeExpiryWrap').style.display = c.show_in_marquee ? '' : 'none';
            // Reflect what was loaded: the hint and the no-end-date warning must describe the
            // saved notice, not the state the form happened to be left in.
            cmExpiryChanged();
            editingAttachments = (c.attachments || []).map(function (a) { return { id: a.ID, name: a.file_name, size: a.file_size, isNew: false }; });
            renderAttachmentChips();
            qs('#cmFileInput').value = '';
            initEditor(c.content || '');
            qs('#cmOverlay').classList.add('is-open');
            qs('#cmModal').classList.add('is-open');
        });
    };

    // ─── Attachments UI ──────────────────────────────────────────
    window.cmQueueFiles = function () {
        var files = qs('#cmFileInput').files;
        for (var i = 0; i < files.length; i++) {
            editingAttachments.push({ id: 0, name: files[i].name, size: files[i].size, isNew: true, file: files[i] });
        }
        renderAttachmentChips();
    };

    function renderAttachmentChips() {
        var wrap = qs('#cmAttachments');
        if (!editingAttachments.length) { wrap.innerHTML = '<span style="font-size:11px;color:#888;">No attachments</span>'; return; }
        var h = '';
        for (var i = 0; i < editingAttachments.length; i++) {
            var a = editingAttachments[i];
            h += '<span class="cm-att-chip">' + esc(a.name) + ' <small>(' + fmtSize(a.size) + ')</small>' +
                '<span class="cm-att-chip__remove" data-idx="' + i + '" onclick="cmRemoveAttachment(' + i + ')">&times;</span></span>';
        }
        wrap.innerHTML = h;
    }

    window.cmRemoveAttachment = function (idx) {
        var att = editingAttachments[idx];
        if (att && !att.isNew && att.id > 0) {
            ajax('removeattachment', { id: att.id }, function () { });
        }
        editingAttachments.splice(idx, 1);
        renderAttachmentChips();
    };

    // ─── Save ────────────────────────────────────────────────────
    window.cmSave = function () {
        var title = val('#cmTitle');
        var content = getEditorContent();
        if (!title) { alert('Title is required'); return; }
        if (!content || content === '<br>' || content === '<p><br></p>') { alert('Content is required'); return; }

        var payload = {
            id: parseInt(val('#cmEditId')) || 0,
            title: title,
            content: content,
            target_audience: val('#cmAudience'),
            priority: val('#cmPriority'),
            status: val('#cmStatus'),
            is_force_read: qs('#cmForceRead').checked ? 1 : 0,
            force_read_expiry: qs('#cmForceRead').checked ? val('#cmExpiry') : '',
            allow_comments: qs('#cmAllowComments').checked ? 1 : 0,
            show_in_marquee: qs('#cmShowMarquee').checked ? 1 : 0,
            marquee_expiry: qs('#cmShowMarquee').checked ? val('#cmMarqueeExpiry') : ''
        };

        qs('#cmSaveBtn').disabled = true;
        qs('#cmSaveBtn').textContent = 'Saving...';

        ajax('save', payload, function (r) {
            if (!r.ok) { alert(r.error || 'Save failed'); qs('#cmSaveBtn').disabled = false; qs('#cmSaveBtn').textContent = 'Save Communication'; return; }
            var commId = r.id;
            // Upload new files sequentially
            var newFiles = editingAttachments.filter(function (a) { return a.isNew && a.file; });
            var uploaded = 0;
            function uploadNext() {
                if (uploaded >= newFiles.length) {
                    qs('#cmSaveBtn').disabled = false;
                    qs('#cmSaveBtn').textContent = 'Save Communication';
                    cmCloseModal();
                    cmLoad();
                    return;
                }
                qs('#cmSaveBtn').textContent = 'Uploading ' + (uploaded + 1) + '/' + newFiles.length + '...';
                uploadFile(commId, newFiles[uploaded].file, function (ur) {
                    if (ur && !ur.ok) {
                        var errMsg = (ur.error || 'Upload failed') + '\n\nFile: ' + newFiles[uploaded].file.name;
                        alert('Upload error: ' + errMsg + '\n\nThe communication was saved but this attachment was not attached.');
                    }
                    uploaded++;
                    uploadNext();
                });
            }
            uploadNext();
        });
    };

    // ─── Publish / Archive / Delete ──────────────────────────────
    window.cmPublish = function (id) {
        if (!confirm('Publish this communication? It will become visible to the target audience.')) return;
        ajax('changestatus', { id: id, status: 'PUBLISHED' }, function (r) {
            if (!r.ok) alert(r.error || 'Failed');
            cmLoad();
        });
    };

    window.cmArchive = function (id) {
        if (!confirm('Archive this communication?')) return;
        ajax('changestatus', { id: id, status: 'ARCHIVED' }, function (r) {
            if (!r.ok) alert(r.error || 'Failed');
            cmLoad();
        });
    };

    window.cmDelete = function (id) {
        if (!confirm('Delete this communication permanently? This cannot be undone.')) return;
        ajax('delete', { id: id }, function (r) {
            if (!r.ok) alert(r.error || 'Failed');
            cmLoad();
        });
    };

    // ─── Preview ─────────────────────────────────────────────────
    window.cmPreview = function (id) {
        ajax('get&id=' + id, null, function (r) {
            if (!r.ok) { alert(r.error); return; }
            var c = r.comm;
            qs('#cmPrevTitle').textContent = c.title || 'Preview';
            qs('#cmPrevContent').innerHTML = c.content || '';
            var ah = '';
            if (c.attachments && c.attachments.length > 0) {
                ah = '<div style="font-size:11px;font-weight:600;margin-bottom:4px;">Attachments:</div>';
                for (var i = 0; i < c.attachments.length; i++) {
                    ah += '<span class="cm-att-chip">' + esc(c.attachments[i].file_name) + ' (' + fmtSize(c.attachments[i].file_size) + ')</span>';
                }
            }
            qs('#cmPrevAttachments').innerHTML = ah;
            qs('#cmPrevOverlay').classList.add('is-open');
            qs('#cmPrevModal').classList.add('is-open');
        });
    };

    window.cmClosePrevModal = function () {
        qs('#cmPrevOverlay').classList.remove('is-open');
        qs('#cmPrevModal').classList.remove('is-open');
    };

    // ─── Read Stats ──────────────────────────────────────────────
    window.cmViewReads = function (id) {
        qs('#cmReadContent').innerHTML = '<div class="cm-spinner">Loading read statistics...</div>';
        qs('#cmReadOverlay').classList.add('is-open');
        qs('#cmReadModal').classList.add('is-open');

        ajax('readstats&id=' + id, null, function (r) {
            if (!r.ok) { qs('#cmReadContent').innerHTML = '<div class="cm-empty"><div class="cm-empty__title">' + esc(r.error) + '</div></div>'; return; }
            qs('#cmReadTitle').textContent = 'Read Stats: ' + (r.title || '');
            var html = '<div style="margin-bottom:12px;">' +
                '<span style="font-size:12px;"><strong>' + (r.readCount || 0) + '</strong> read &nbsp;|&nbsp; <strong>' + (r.confirmedCount || 0) + '</strong> confirmed</span>' +
                '</div>';

            if (r.readers && r.readers.length > 0) {
                html += '<table class="cm-table cm-read-detail"><thead><tr><th>User</th><th>Type</th><th>Read At</th><th>Confirmed</th></tr></thead><tbody>';
                for (var i = 0; i < r.readers.length; i++) {
                    var rd = r.readers[i];
                    html += '<tr>' +
                        '<td>' + esc(rd.user_name || rd.user_id) + ' <small style="color:#888;">(' + esc(rd.user_id) + ')</small></td>' +
                        '<td><span class="cm-badge cm-badge--' + (rd.user_type === 'STAFF' ? 'staff' : 'student') + '">' + esc(rd.user_type) + '</span></td>' +
                        '<td>' + fmtDate(rd.read_at) + '</td>' +
                        '<td>' + (rd.confirmed_at ? '<span style="color:#2e7d32;">&#10003; ' + fmtDate(rd.confirmed_at) + '</span>' : '<span style="color:#888;">Not confirmed</span>') + '</td>' +
                        '</tr>';
                }
                html += '</tbody></table>';
            } else {
                html += '<div class="cm-empty"><div class="cm-empty__title">No readers yet</div></div>';
            }
            qs('#cmReadContent').innerHTML = html;
        });
    };

    window.cmCloseReadModal = function () {
        qs('#cmReadOverlay').classList.remove('is-open');
        qs('#cmReadModal').classList.remove('is-open');
    };

    // ─── Init ────────────────────────────────────────────────────
    cmLoad();
})();
</script>

</asp:Content>
