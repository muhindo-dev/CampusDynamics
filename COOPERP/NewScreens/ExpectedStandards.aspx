<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ExpectedStandards.aspx.cs" Inherits="COOPERP_NewScreens_ExpectedStandards" Title="Expected standards" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.es-layout { display: grid; grid-template-columns: 280px minmax(0, 1fr); gap: 14px; align-items: start; }
@media (max-width: 900px) { .es-layout { grid-template-columns: minmax(0, 1fr); } }
.es-group { display: block; padding: 9px 14px; border-bottom: 1px solid var(--hr-border); border-left: 3px solid transparent; color: var(--hr-text); text-decoration: none; }
.es-group:last-child { border-bottom: none; }
.es-group:hover { background: var(--hr-band); }
.es-group.is-selected { border-left-color: var(--hr-navy); background: var(--hr-surface); }
.es-group.is-inactive { color: var(--hr-muted); }
.es-group__name { display: block; font-weight: 600; font-size: 12px; }
.es-group__meta { display: block; font-size: 11px; color: var(--hr-muted); margin-top: 2px; }
.es-off td { color: var(--hr-muted); }
.es-move svg { width: 12px; height: 12px; }
.es-table-text td { vertical-align: top; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">
<asp:HiddenField ID="hfGroupId" runat="server" Value="0" />

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg></div>
        <div>
            <div class="hr-header__title">Expected standards</div>
            <div class="hr-header__sub">Section B standards by office and staff type</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <a class="hr-btn hr-btn--inverse" href="ExpectedStandards.aspx?action=print" target="_blank" rel="noopener">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
            Print catalogue
        </a>
        <asp:Literal ID="litAddGroup" runat="server" />
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="AppraisalDashboard.aspx">Overview</a><a class="hr-tab" href="AppraisalSessions.aspx">Sessions</a><a class="hr-tab" href="AppraisalView.aspx">Appraisals</a><a class="hr-tab" href="AppraisalReports.aspx">Reports</a><a class="hr-tab" href="CompetencyTemplates.aspx">Competencies</a><a class="hr-tab hr-tab--active" href="ExpectedStandards.aspx">Expected standards</a></div>

<asp:Literal ID="litError" runat="server" />
<asp:Literal ID="litReadOnly" runat="server" />

<div class="hr-kpis">
    <div class="hr-kpi"><div class="hr-kpi__label">Active groups</div><div class="hr-kpi__value"><asp:Literal ID="litKpiGroups" runat="server" Text="0" /></div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">Active standards</div><div class="hr-kpi__value"><asp:Literal ID="litKpiStandards" runat="server" Text="0" /></div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">Departments mapped</div><div class="hr-kpi__value"><asp:Literal ID="litKpiMapped" runat="server" Text="0" /></div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">Appraisal rows using a standard</div><div class="hr-kpi__value"><asp:Literal ID="litKpiUsed" runat="server" Text="0" /></div></div>
</div>

<div class="es-layout">
    <div class="hr-card">
        <div class="hr-card__head"><div class="hr-card__title">Groups</div></div>
        <asp:Literal ID="litGroups" runat="server" />
    </div>

    <div style="min-width:0;">
        <div class="hr-card">
            <asp:Literal ID="litGroupHead" runat="server" />
            <div class="hr-table-wrap">
                <table class="hr-table es-table-text" id="tblStandards">
                    <thead><tr>
                        <th class="hr-num">No.</th>
                        <th>Responsibility or key performance area</th>
                        <th>Expected standard</th>
                        <th class="hr-num">Used</th>
                        <th></th>
                    </tr></thead>
                    <tbody><asp:Literal ID="litStandards" runat="server" /></tbody>
                </table>
            </div>
        </div>

        <div class="hr-card">
            <div class="hr-card__head">
                <div class="hr-card__title">Department groups</div>
                <div class="hr-card__meta">Default: Lecturers for academic staff, Administrative staff for others</div>
            </div>
            <div class="hr-table-wrap">
                <table class="hr-table">
                    <thead><tr><th>Department</th><th>Faculty</th><th class="hr-num">Staff</th><th style="min-width:220px;">Standards group</th></tr></thead>
                    <tbody><asp:Literal ID="litDepts" runat="server" /></tbody>
                </table>
            </div>
        </div>
    </div>
</div>

<!-- Group -->
<div class="hr-modal" id="groupModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="groupModalTitle">Add group</span><button type="button" class="hr-modal__close" onclick="closeModal('groupModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <input type="hidden" id="gId" value="0" />
            <div class="hr-form">
                <div class="hr-field">
                    <label class="hr-label" for="gCode">Code <span class="hr-req">*</span></label>
                    <input type="text" id="gCode" class="hr-input" maxlength="30" placeholder="FINANCE" />
                    <span class="hr-hint">Letters, digits and underscore.</span>
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="gApplies">Applies to <span class="hr-req">*</span></label>
                    <select id="gApplies" class="hr-select">
                        <option value="ADMINISTRATIVE">Administrative staff</option>
                        <option value="ACADEMIC">Academic staff</option>
                        <option value="ANY">Any staff</option>
                    </select>
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label" for="gName">Name <span class="hr-req">*</span></label>
                    <input type="text" id="gName" class="hr-input" maxlength="150" placeholder="Finance Office" />
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="gSort">Order</label>
                    <input type="number" id="gSort" class="hr-input" value="0" min="0" />
                    <span class="hr-hint">0 places it at the end.</span>
                </div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('groupModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnSaveGroup" onclick="saveGroup()">Save group</button>
        </div>
    </div>
</div>

<!-- Standard -->
<div class="hr-modal" id="stdModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="stdModalTitle">Add standard</span><button type="button" class="hr-modal__close" onclick="closeModal('stdModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <input type="hidden" id="sId" value="0" />
            <div class="hr-field" style="margin-bottom:12px;">
                <label class="hr-label" for="sKpa">Responsibility or key performance area <span class="hr-req">*</span></label>
                <input type="text" id="sKpa" class="hr-input" maxlength="255" placeholder="Account reconciliation" />
            </div>
            <div class="hr-field">
                <label class="hr-label" for="sStd">Expected standard <span class="hr-req">*</span></label>
                <textarea id="sStd" class="hr-textarea" rows="4"></textarea>
                <span class="hr-hint">Appraisals already started keep their wording.</span>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('stdModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnSaveStd" onclick="saveStandard()">Save standard</button>
        </div>
    </div>
</div>

<!-- Confirm -->
<div class="hr-modal" id="confirmModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="confirmTitle">Confirm</span><button type="button" class="hr-modal__close" onclick="closeModal('confirmModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body"><p id="confirmText" style="margin:0;"></p></div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('confirmModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--danger" id="confirmOk" onclick="confirmRun()">Confirm</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="esToast"></div>
</div>

<script type="text/javascript">
var ES_GROUP = parseInt(document.getElementById('<%= hfGroupId.ClientID %>').value || '0', 10);

function toast(msg, ok) {
    var t = document.getElementById('esToast');
    t.textContent = msg || '';
    t.className = 'hr-toast is-on' + (ok ? '' : ' hr-toast--err');
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, 3500);
}
function openModal(id) { document.getElementById(id).classList.add('is-open'); }
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }
function parse(xhr) {
    var res;
    try { res = JSON.parse(xhr.responseText); } catch (e) { res = { ok: false, msg: 'The request could not be completed. Refresh the page and sign in again if asked.' }; }
    if (!res.msg && res.message) res.msg = res.message;
    return res;
}
function post(action, fields, cb) {
    var fd = new FormData();
    for (var k in fields) if (fields.hasOwnProperty(k)) fd.append(k, fields[k]);
    var xhr = new XMLHttpRequest();
    xhr.open('POST', 'ExpectedStandards.aspx?ajax=' + action, true);
    var m = document.querySelector('meta[name="csrf-token"]');
    if (m) xhr.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
    xhr.onreadystatechange = function () { if (xhr.readyState === 4) cb(parse(xhr)); };
    xhr.send(fd);
}
function get(action, id, cb) {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'ExpectedStandards.aspx?ajax=' + action + '&id=' + encodeURIComponent(id), true);
    xhr.onreadystatechange = function () { if (xhr.readyState === 4) cb(parse(xhr)); };
    xhr.send();
}
function reloadTo(gid) {
    setTimeout(function () { window.location.href = 'ExpectedStandards.aspx' + (gid ? '?gid=' + gid : ''); }, 600);
}
var _confirmCb = null;
function askConfirm(title, text, okLabel, cb) {
    document.getElementById('confirmTitle').textContent = title;
    document.getElementById('confirmText').textContent = text;
    document.getElementById('confirmOk').textContent = okLabel;
    _confirmCb = cb;
    openModal('confirmModal');
}
function confirmRun() { closeModal('confirmModal'); if (_confirmCb) _confirmCb(); }

// ── Groups ───────────────────────────────────────────────────────────
function addGroup() {
    document.getElementById('groupModalTitle').textContent = 'Add group';
    document.getElementById('gId').value = '0';
    document.getElementById('gCode').value = '';
    document.getElementById('gName').value = '';
    document.getElementById('gApplies').value = 'ADMINISTRATIVE';
    document.getElementById('gSort').value = '0';
    openModal('groupModal');
}
function editGroup(id) {
    get('get_group', id, function (res) {
        if (!res.ok) { toast(res.msg, false); return; }
        var d = res.data;
        document.getElementById('groupModalTitle').textContent = 'Edit group';
        document.getElementById('gId').value = d.group_id;
        document.getElementById('gCode').value = d.group_code;
        document.getElementById('gName').value = d.group_name;
        document.getElementById('gApplies').value = d.applies_to;
        document.getElementById('gSort').value = d.sort_order;
        openModal('groupModal');
    });
}
function saveGroup() {
    var code = document.getElementById('gCode').value.trim();
    var name = document.getElementById('gName').value.trim();
    if (!code) { toast('Enter a code.', false); return; }
    if (!name) { toast('Enter a name.', false); return; }
    var btn = document.getElementById('btnSaveGroup'); btn.disabled = true;
    post('save_group', {
        group_id: document.getElementById('gId').value, group_code: code, group_name: name,
        applies_to: document.getElementById('gApplies').value, sort_order: document.getElementById('gSort').value || '0'
    }, function (res) {
        btn.disabled = false;
        toast(res.msg, res.ok);
        if (res.ok) { closeModal('groupModal'); reloadTo(res.id || ES_GROUP); }
    });
}
function toggleGroup(id, active) {
    var run = function () { post('toggle_group', { id: id, active: active }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(id); }); };
    if (active) { run(); return; }
    askConfirm('Deactivate group', 'New appraisals in departments mapped to this group will use the default group. Existing appraisals do not change.', 'Deactivate', run);
}
function deleteGroup(id) {
    askConfirm('Delete group', 'Delete this empty group permanently?', 'Delete', function () {
        post('delete_group', { id: id }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(0); });
    });
}

// ── Standards ────────────────────────────────────────────────────────
function addStandard() {
    if (!ES_GROUP) { toast('Choose a group first.', false); return; }
    document.getElementById('stdModalTitle').textContent = 'Add standard';
    document.getElementById('sId').value = '0';
    document.getElementById('sKpa').value = '';
    document.getElementById('sStd').value = '';
    openModal('stdModal');
}
function editStandard(id) {
    get('get_standard', id, function (res) {
        if (!res.ok) { toast(res.msg, false); return; }
        var d = res.data;
        document.getElementById('stdModalTitle').textContent = 'Edit standard';
        document.getElementById('sId').value = d.standard_id;
        document.getElementById('sKpa').value = d.kpa_title;
        document.getElementById('sStd').value = d.expected_standard;
        openModal('stdModal');
    });
}
function saveStandard() {
    var kpa = document.getElementById('sKpa').value.trim();
    var std = document.getElementById('sStd').value.trim();
    if (!kpa) { toast('Enter the responsibility or key performance area.', false); return; }
    if (!std) { toast('Enter the expected standard.', false); return; }
    var btn = document.getElementById('btnSaveStd'); btn.disabled = true;
    post('save_standard', { standard_id: document.getElementById('sId').value, group_id: ES_GROUP, kpa_title: kpa, expected_standard: std }, function (res) {
        btn.disabled = false;
        toast(res.msg, res.ok);
        if (res.ok) { closeModal('stdModal'); reloadTo(ES_GROUP); }
    });
}
function toggleStandard(id, active) {
    post('toggle_standard', { id: id, active: active }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(ES_GROUP); });
}
function deleteStandard(id) {
    askConfirm('Delete standard', 'Delete this standard permanently? No appraisal uses it.', 'Delete', function () {
        post('delete_standard', { id: id }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(ES_GROUP); });
    });
}
function moveRow(btn, dir) {
    var tr = btn.parentNode;
    while (tr && tr.tagName !== 'TR') tr = tr.parentNode;
    if (!tr) return;
    var sib = dir < 0 ? tr.previousElementSibling : tr.nextElementSibling;
    if (!sib || sib.getAttribute('data-active') !== '1') return;
    if (dir < 0) tr.parentNode.insertBefore(tr, sib); else tr.parentNode.insertBefore(sib, tr);
    var rows = document.querySelectorAll('#tblStandards tbody tr[data-active="1"]'), ids = [];
    for (var i = 0; i < rows.length; i++) {
        ids.push(rows[i].getAttribute('data-id'));
        var num = rows[i].querySelector('td.hr-num');
        if (num) num.textContent = (i + 1);
    }
    post('reorder', { group_id: ES_GROUP, ids: ids.join(',') }, function (res) { if (!res.ok) toast(res.msg, false); });
}
function mapDept(sel) {
    var prev = sel.getAttribute('data-cur') || '0';
    sel.disabled = true;
    post('map_department', { dept_id: sel.getAttribute('data-dept'), group_id: sel.value }, function (res) {
        sel.disabled = false;
        toast(res.msg, res.ok);
        if (res.ok) sel.setAttribute('data-cur', sel.value); else sel.value = prev;
    });
}
</script>
</asp:Content>
