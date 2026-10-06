<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="CompetencyTemplates.aspx.cs" Inherits="COOPERP_NewScreens_CompetencyTemplates" Title="Competencies" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=1" />
<style>
.ct-group td { background: var(--hr-surface) !important; font-weight: 600; color: var(--hr-navy); }
.ct-desc { color: var(--hr-text-2); font-size: 11px; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="3" y1="15" x2="21" y2="15"/><line x1="9" y1="3" x2="9" y2="21"/></svg></div>
        <div>
            <div class="hr-header__title">Competencies</div>
            <div class="hr-header__sub">Section C criteria by staff category</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <a class="hr-btn hr-btn--inverse" href="<%= PrintLink %>" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
            Print catalogue
        </a>
        <asp:Literal ID="litAddButton" runat="server" />
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="AppraisalDashboard.aspx">Overview</a><a class="hr-tab" href="AppraisalSessions.aspx">Sessions</a><a class="hr-tab" href="AppraisalView.aspx">Appraisals</a><a class="hr-tab" href="AppraisalReports.aspx">Reports</a><a class="hr-tab hr-tab--active" href="CompetencyTemplates.aspx">Competencies</a><a class="hr-tab" href="ExpectedStandards.aspx">Expected standards</a></div>

<asp:Literal ID="litError" runat="server" />

<div class="hr-kpis">
    <a class="hr-kpi" href="CompetencyTemplates.aspx"><div class="hr-kpi__label">Competencies</div><div class="hr-kpi__value"><asp:Literal ID="litKpiTotal" runat="server" Text="0" /></div><div class="hr-kpi__sub"><asp:Literal ID="litKpiGroups" runat="server" /></div></a>
    <asp:Literal ID="litCatStats" runat="server" />
</div>

<div class="hr-filters">
    <div class="hr-filter">
        <label for="selCategory">Staff category</label>
        <select id="selCategory" class="hr-select" onchange="applyFilters()"><asp:Literal ID="litCatOptions" runat="server" /></select>
    </div>
    <div class="hr-filter hr-filter--grow">
        <label for="txtSearch">Search</label>
        <input type="text" id="txtSearch" class="hr-input" placeholder="Code, competency or group" value="<%= HttpUtility.HtmlAttributeEncode(Request.QueryString["q"] ?? "") %>" onkeydown="if(event.key==='Enter'){event.preventDefault();applyFilters();}" />
    </div>
    <div class="hr-filters__actions">
        <button type="button" class="hr-btn hr-btn--primary" onclick="applyFilters()">Search</button>
        <a class="hr-btn hr-btn--secondary" href="CompetencyTemplates.aspx">Clear</a>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Competency list</div>
        <div class="hr-card__meta"><asp:Literal ID="litRecordCount" runat="server" Text="0" /> competencies</div>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th>Code</th>
                    <th>Competency</th>
                    <th class="hr-num">Order</th>
                    <th>Description</th>
                    <th></th>
                </tr>
            </thead>
            <tbody><asp:Literal ID="litRows" runat="server" /></tbody>
        </table>
    </div>
</div>

<!-- Add or edit -->
<div class="hr-modal" id="modalOverlay">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="modalTitle">Add competency</span><button type="button" class="hr-modal__close" onclick="closeModal('modalOverlay')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <input type="hidden" id="fldTemplateId" value="0" />
            <div class="hr-form">
                <div class="hr-field">
                    <label class="hr-label" for="fldStaffCategory">Staff category <span class="hr-req">*</span></label>
                    <select id="fldStaffCategory" class="hr-select">
                        <option value="">Choose</option>
                        <option value="ACADEMIC">Academic</option>
                        <option value="ADMINISTRATIVE">Administrative</option>
                        <option value="SUPPORT">Support</option>
                    </select>
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="fldCode">Code <span class="hr-req">*</span></label>
                    <input type="text" id="fldCode" class="hr-input" placeholder="C1.1" maxlength="10" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label" for="fldCategoryName">Group <span class="hr-req">*</span></label>
                    <input type="text" id="fldCategoryName" class="hr-input" placeholder="Teaching function" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label" for="fldCompetencyName">Competency <span class="hr-req">*</span></label>
                    <input type="text" id="fldCompetencyName" class="hr-input" />
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="fldSortOrder">Order</label>
                    <input type="number" id="fldSortOrder" class="hr-input" value="0" min="0" />
                    <span class="hr-hint">0 places it at the end.</span>
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label" for="fldDescription">Description</label>
                    <textarea id="fldDescription" class="hr-textarea" rows="3"></textarea>
                </div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('modalOverlay')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnSave" onclick="saveForm()">Save</button>
        </div>
    </div>
</div>

<!-- Delete -->
<div class="hr-modal" id="deleteModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Delete competency</span><button type="button" class="hr-modal__close" onclick="closeModal('deleteModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body"><p style="margin:0;" id="deleteText"></p></div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('deleteModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--danger" id="btnDelete" onclick="confirmDelete()">Delete</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="ctToast"></div>
</div>

<script type="text/javascript">
function applyFilters() {
    var cat = document.getElementById('selCategory').value;
    var q = document.getElementById('txtSearch').value.trim();
    var p = [];
    if (cat) p.push('cat=' + encodeURIComponent(cat));
    if (q) p.push('q=' + encodeURIComponent(q));
    window.location.href = 'CompetencyTemplates.aspx' + (p.length ? '?' + p.join('&') : '');
}
function openModal(id) { document.getElementById(id).classList.add('is-open'); }
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }
function showToast(msg, ok) {
    var t = document.getElementById('ctToast');
    t.textContent = msg || '';
    t.className = 'hr-toast is-on' + (ok === false ? ' hr-toast--err' : '');
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, 3500);
}
function request(method, url, fd, cb) {
    var xhr = new XMLHttpRequest();
    xhr.open(method, url, true);
    if (method === 'POST') {
        var m = document.querySelector('meta[name="csrf-token"]');
        if (m) xhr.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
    }
    xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) return;
        var r;
        try { r = JSON.parse(xhr.responseText); } catch (e) { r = { ok: false, msg: 'The request could not be completed. Refresh the page and sign in again if asked.' }; }
        if (!r.msg && r.message) r.msg = r.message;
        cb(r);
    };
    xhr.send(fd);
}
function openCreateModal() {
    document.getElementById('modalTitle').textContent = 'Add competency';
    document.getElementById('fldTemplateId').value = '0';
    ['fldStaffCategory', 'fldCode', 'fldCategoryName', 'fldCompetencyName', 'fldDescription'].forEach(function (id) { document.getElementById(id).value = ''; });
    document.getElementById('fldSortOrder').value = '0';
    openModal('modalOverlay');
}
function editRow(id) {
    request('GET', 'CompetencyTemplates.aspx?ajax=get&id=' + id, null, function (r) {
        if (!r.ok) { showToast(r.msg, false); return; }
        var d = r.data;
        document.getElementById('modalTitle').textContent = 'Edit competency';
        document.getElementById('fldTemplateId').value = d.template_id;
        document.getElementById('fldStaffCategory').value = d.staff_category;
        document.getElementById('fldCode').value = d.competency_code;
        document.getElementById('fldCategoryName').value = d.category_name;
        document.getElementById('fldCompetencyName').value = d.competency_name;
        document.getElementById('fldSortOrder').value = d.sort_order;
        document.getElementById('fldDescription').value = d.description || '';
        openModal('modalOverlay');
    });
}
var _deleteId = 0;
function deleteRow(id, label) {
    _deleteId = id;
    document.getElementById('deleteText').textContent = 'Delete ' + label + '? This cannot be undone.';
    openModal('deleteModal');
}
function confirmDelete() {
    var fd = new FormData(); fd.append('template_id', _deleteId);
    document.getElementById('btnDelete').disabled = true;
    request('POST', 'CompetencyTemplates.aspx?ajax=delete', fd, function (r) {
        document.getElementById('btnDelete').disabled = false;
        showToast(r.msg, !!r.ok);
        if (r.ok) { closeModal('deleteModal'); setTimeout(function () { location.reload(); }, 800); }
    });
}
function saveForm() {
    var v = function (id) { return document.getElementById(id).value.trim(); };
    if (!v('fldStaffCategory')) { showToast('Choose a staff category.', false); return; }
    if (!v('fldCode')) { showToast('Enter a code.', false); return; }
    if (!v('fldCategoryName')) { showToast('Enter a group.', false); return; }
    if (!v('fldCompetencyName')) { showToast('Enter the competency.', false); return; }
    var btn = document.getElementById('btnSave'); btn.disabled = true;
    var fd = new FormData();
    fd.append('template_id', v('fldTemplateId'));
    fd.append('staff_category', v('fldStaffCategory'));
    fd.append('competency_code', v('fldCode'));
    fd.append('category_name', v('fldCategoryName'));
    fd.append('competency_name', v('fldCompetencyName'));
    fd.append('sort_order', v('fldSortOrder') || '0');
    fd.append('description', document.getElementById('fldDescription').value);
    request('POST', 'CompetencyTemplates.aspx?ajax=save', fd, function (r) {
        btn.disabled = false;
        showToast(r.msg, !!r.ok);
        if (r.ok) { closeModal('modalOverlay'); setTimeout(function () { location.reload(); }, 800); }
    });
}
</script>
</asp:Content>
