<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ExpectedStandards.aspx.cs" Inherits="COOPERP_NewScreens_ExpectedStandards" Title="Expected Standards - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<style>
/* ===== EXPECTED STANDARDS CATALOGUE ===== */
.es-page{--navy:#05275C;--navy-h:#041d45;--accent:#174DA4;--surface:#f5f7fa;--border:#e0e5ed;--border-in:#cdd3de;--text:#1a1a2e;--text2:#555;--muted:#888;--danger:#dc3545;--warn:#d97706;
    font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;font-size:12px;color:var(--text);}
.es-page *,.es-page *::before,.es-page *::after{box-sizing:border-box;}

/* header */
.es-head{display:flex;align-items:center;gap:12px;margin-bottom:14px;flex-wrap:wrap;}
.es-head__icon{width:40px;height:40px;background:var(--navy);color:#fff;display:flex;align-items:center;justify-content:center;flex-shrink:0;border-radius:4px;}
.es-head__title{font-size:20px;font-weight:700;color:var(--text);line-height:1.2;}
.es-head__sub{font-size:12px;color:var(--muted);margin-top:2px;}
.es-head__actions{margin-left:auto;display:flex;gap:8px;flex-wrap:wrap;}

/* KPIs */
.es-kpis{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:1px;background:var(--border);border:1px solid var(--border);margin-bottom:14px;}
@media(max-width:800px){.es-kpis{grid-template-columns:repeat(2,minmax(0,1fr));}}
.es-kpi{background:#fff;padding:10px 14px;border-left:3px solid var(--navy);}
.es-kpi--accent{border-left-color:var(--accent);}
.es-kpi--green{border-left-color:#16a34a;}
.es-kpi--amber{border-left-color:var(--warn);}
.es-kpi__val{font-size:20px;font-weight:700;color:var(--text);line-height:1.15;}
.es-kpi__lbl{font-size:10px;color:var(--muted);text-transform:uppercase;letter-spacing:.4px;margin-top:2px;}

/* layout */
.es-layout{display:grid;grid-template-columns:300px minmax(0,1fr);gap:14px;align-items:start;}
@media(max-width:900px){.es-layout{grid-template-columns:minmax(0,1fr);}}
.es-card{background:#fff;border:1px solid var(--border);border-radius:4px;margin-bottom:14px;min-width:0;}
.es-card__hdr{padding:10px 14px;border-bottom:1px solid var(--border);font-size:11px;font-weight:700;color:var(--text2);text-transform:uppercase;letter-spacing:.4px;display:flex;align-items:center;gap:8px;background:var(--surface);}
.es-card__hdr-right{margin-left:auto;font-weight:400;text-transform:none;letter-spacing:0;color:var(--muted);}

/* group list */
.es-group{display:block;padding:9px 14px;border-bottom:1px solid var(--border);text-decoration:none;color:var(--text);border-left:3px solid transparent;}
.es-group:hover{background:#f7f9fc;}
.es-group.is-selected{background:#eef3fb;border-left-color:var(--accent);}
.es-group.is-inactive .es-group__name{color:var(--muted);text-decoration:line-through;}
.es-group__name{display:block;font-weight:600;font-size:12px;}
.es-group__meta{display:flex;flex-wrap:wrap;gap:4px;align-items:center;margin-top:3px;}
.es-group__count{font-size:10px;color:var(--muted);margin-left:auto;}

/* badges / code */
.es-code{display:inline-block;font-family:Consolas,monospace;font-size:10px;padding:1px 5px;background:rgba(23,77,164,.07);border:1px solid rgba(23,77,164,.15);color:var(--accent);border-radius:0;}
.es-badge{display:inline-block;font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;padding:1px 5px;border-radius:0;}
.es-badge--academic{background:#e8eef8;color:#174DA4;}
.es-badge--administrative{background:#fff8e1;color:#b45309;}
.es-badge--any{background:#e6f4ea;color:#155724;}
.es-badge--off{background:#f1f3f6;color:#6b7280;border:1px solid #d1d5db;}

/* panel head */
.es-panel-head{display:flex;gap:12px;align-items:flex-start;flex-wrap:wrap;padding:12px 14px;border-bottom:1px solid var(--border);}
.es-panel-head__text{min-width:0;flex:1 1 260px;}
.es-panel-head__title{font-size:15px;font-weight:700;color:var(--navy);}
.es-panel-head__sub{font-size:11px;color:var(--muted);margin-top:3px;line-height:1.6;}
.es-panel-head__actions{display:flex;gap:6px;flex-wrap:wrap;}

/* table */
.es-table-wrap{overflow-x:auto;}
.es-table{width:100%;border-collapse:collapse;font-size:12px;}
.es-table th{text-align:left;padding:7px 10px;background:var(--surface);border-bottom:1px solid var(--border);color:var(--text2);font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;white-space:nowrap;}
.es-table td{padding:7px 10px;border-bottom:1px solid #f0f2f5;vertical-align:top;}
.es-table tr.is-inactive td{color:#9ca3af;background:#fafafa;}
.es-num{text-align:right;width:44px;color:var(--muted);font-variant-numeric:tabular-nums;}
.es-kpa{font-weight:600;min-width:150px;}
.es-std{color:#444;line-height:1.45;}
.es-muted{color:var(--muted);font-size:11px;}
.es-actions{white-space:nowrap;text-align:right;width:150px;}
.es-empty{padding:18px;text-align:center;color:var(--muted);font-style:italic;}

/* buttons / inputs - radius 0 */
.es-btn{display:inline-flex;align-items:center;gap:5px;padding:6px 12px;font-size:12px;font-weight:600;border:1px solid var(--border-in);background:#fff;color:var(--text2);cursor:pointer;border-radius:0;white-space:nowrap;font-family:inherit;}
.es-btn:hover{border-color:var(--accent);color:var(--accent);}
.es-btn--primary{background:var(--navy);border-color:var(--navy);color:#fff;}
.es-btn--primary:hover{background:var(--navy-h);border-color:var(--navy-h);color:#fff;}
.es-btn--outline{background:#fff;}
.es-btn--danger-outline{color:var(--danger);border-color:#f1b0b7;}
.es-btn--danger-outline:hover{background:#fff5f5;color:var(--danger);border-color:var(--danger);}
.es-btn:disabled{opacity:.5;cursor:not-allowed;}
.es-icon-btn{display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border:1px solid var(--border-in);background:#fff;color:#666;cursor:pointer;border-radius:0;margin-left:2px;}
.es-icon-btn:hover{border-color:var(--accent);color:var(--accent);background:#eef3fb;}
.es-icon-btn--danger:hover{border-color:var(--danger);color:var(--danger);background:#fff5f5;}
.es-select,.es-field input,.es-field select,.es-field textarea{font-size:12px;padding:6px 8px;border:1px solid var(--border-in);border-radius:0;background:#fff;color:var(--text);font-family:inherit;width:100%;}
.es-select:focus,.es-field input:focus,.es-field select:focus,.es-field textarea:focus{outline:none;border-color:var(--accent);box-shadow:0 0 0 2px rgba(23,77,164,.15);}
.es-select.is-saved{border-color:#16a34a;}

/* alerts */
.es-alert{padding:10px 14px;margin-bottom:12px;font-size:12px;border:1px solid;}
.es-alert--error{background:#f8d7da;color:#721c24;border-color:#f5c6cb;}
.es-alert--info{background:#eef3fb;color:#05275C;border-color:#c5d3e8;}

/* modal */
.es-modal-ov{display:none;position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:9999;align-items:center;justify-content:center;padding:16px;}
.es-modal-ov.is-open{display:flex;}
.es-modal{background:#fff;border-radius:2px;width:560px;max-width:100%;max-height:92vh;overflow-y:auto;box-shadow:0 10px 32px rgba(0,0,0,.2);}
.es-modal__hdr{padding:12px 16px;border-bottom:1px solid var(--border);display:flex;align-items:center;background:var(--navy);color:#fff;}
.es-modal__hdr h3{margin:0;font-size:14px;font-weight:700;flex:1;}
.es-modal__close{background:none;border:none;color:#fff;font-size:20px;cursor:pointer;line-height:1;padding:0 4px;}
.es-modal__body{padding:16px;}
.es-modal__foot{padding:12px 16px;border-top:1px solid var(--border);display:flex;justify-content:flex-end;gap:8px;}
.es-field{margin-bottom:12px;}
.es-field label{display:block;font-size:10px;font-weight:700;color:var(--text2);text-transform:uppercase;letter-spacing:.4px;margin-bottom:4px;}
.es-field textarea{min-height:90px;resize:vertical;}
.es-field-row{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:12px;}
@media(max-width:520px){.es-field-row{grid-template-columns:minmax(0,1fr);}}
.es-hint{font-size:11px;color:var(--muted);margin-top:3px;}

/* toast */
.es-toast{position:fixed;bottom:20px;right:20px;padding:10px 16px;font-size:12px;font-weight:600;color:#fff;z-index:10000;opacity:0;transition:opacity .25s;pointer-events:none;max-width:360px;}
.es-toast.is-on{opacity:1;}
.es-toast--ok{background:#16a34a;}
.es-toast--err{background:var(--danger);}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="es-page">

<asp:Literal ID="litError" runat="server" />
<asp:Literal ID="litReadOnly" runat="server" />
<asp:HiddenField ID="hfGroupId" runat="server" Value="0" />

<div class="es-head">
    <div class="es-head__icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>
    </div>
    <div>
        <div class="es-head__title">Expected Standards</div>
        <div class="es-head__sub">Responsibilities / Key Performance Areas and their expected standards, used to pre-fill Section B of the appraisal</div>
    </div>
    <div class="es-head__actions">
        <a href="CompetencyTemplates.aspx" class="es-btn">Competency Templates</a>
        <button type="button" class="es-btn es-btn--primary es-edit-only" onclick="addGroup()">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            Add Group
        </button>
    </div>
</div>

<div class="es-kpis">
    <div class="es-kpi"><div class="es-kpi__val"><asp:Literal ID="litKpiGroups" runat="server" Text="0" /></div><div class="es-kpi__lbl">Active Groups</div></div>
    <div class="es-kpi es-kpi--accent"><div class="es-kpi__val"><asp:Literal ID="litKpiStandards" runat="server" Text="0" /></div><div class="es-kpi__lbl">Active Standards</div></div>
    <div class="es-kpi es-kpi--green"><div class="es-kpi__val"><asp:Literal ID="litKpiMapped" runat="server" Text="0" /></div><div class="es-kpi__lbl">Departments Mapped</div></div>
    <div class="es-kpi es-kpi--amber"><div class="es-kpi__val"><asp:Literal ID="litKpiUsed" runat="server" Text="0" /></div><div class="es-kpi__lbl">Appraisal Rows Using a Standard</div></div>
</div>

<div class="es-layout">
    <div class="es-card">
        <div class="es-card__hdr">Groups</div>
        <asp:Literal ID="litGroups" runat="server" />
    </div>

    <div style="min-width:0;">
        <div class="es-card">
            <asp:Literal ID="litGroupHead" runat="server" />
            <div class="es-table-wrap">
                <table class="es-table" id="tblStandards">
                    <thead><tr>
                        <th class="es-num">#</th>
                        <th>Responsibility / KPA</th>
                        <th>Expected Standard</th>
                        <th class="es-num" title="Appraisal Section B rows that use this standard">Used</th>
                        <th class="es-actions">Actions</th>
                    </tr></thead>
                    <tbody><asp:Literal ID="litStandards" runat="server" /></tbody>
                </table>
            </div>
        </div>

        <div class="es-card">
            <div class="es-card__hdr">Department &rarr; Group Mapping
                <span class="es-card__hdr-right">Unmapped departments fall back to Lecturers (academic staff) or Administrative Staff (administrative staff). Support staff have no group.</span>
            </div>
            <div class="es-table-wrap">
                <table class="es-table">
                    <thead><tr><th>Department</th><th>Faculty</th><th class="es-num">Staff</th><th style="min-width:220px;">Standards Group</th></tr></thead>
                    <tbody><asp:Literal ID="litDepts" runat="server" /></tbody>
                </table>
            </div>
        </div>
    </div>
</div>

<!-- Group modal -->
<div class="es-modal-ov" id="groupModal">
    <div class="es-modal">
        <div class="es-modal__hdr"><h3 id="groupModalTitle">Add Group</h3><button type="button" class="es-modal__close" onclick="closeModal('groupModal')">&times;</button></div>
        <div class="es-modal__body">
            <input type="hidden" id="gId" value="0" />
            <div class="es-field-row">
                <div class="es-field">
                    <label for="gCode">Group Code *</label>
                    <input type="text" id="gCode" maxlength="30" placeholder="e.g. FINANCE" />
                    <div class="es-hint">Letters, digits and _ only.</div>
                </div>
                <div class="es-field">
                    <label for="gApplies">Applies To *</label>
                    <select id="gApplies">
                        <option value="ADMINISTRATIVE">Administrative staff</option>
                        <option value="ACADEMIC">Academic staff</option>
                        <option value="ANY">Any staff</option>
                    </select>
                </div>
            </div>
            <div class="es-field">
                <label for="gName">Group Name *</label>
                <input type="text" id="gName" maxlength="150" placeholder="e.g. Finance Office" />
            </div>
            <div class="es-field-row">
                <div class="es-field">
                    <label for="gSort">Sort Order</label>
                    <input type="number" id="gSort" value="0" min="0" />
                    <div class="es-hint">0 = place at the end.</div>
                </div>
                <div></div>
            </div>
        </div>
        <div class="es-modal__foot">
            <button type="button" class="es-btn" onclick="closeModal('groupModal')">Cancel</button>
            <button type="button" class="es-btn es-btn--primary" id="btnSaveGroup" onclick="saveGroup()">Save Group</button>
        </div>
    </div>
</div>

<!-- Standard modal -->
<div class="es-modal-ov" id="stdModal">
    <div class="es-modal">
        <div class="es-modal__hdr"><h3 id="stdModalTitle">Add Standard</h3><button type="button" class="es-modal__close" onclick="closeModal('stdModal')">&times;</button></div>
        <div class="es-modal__body">
            <input type="hidden" id="sId" value="0" />
            <div class="es-field">
                <label for="sKpa">Responsibility / Key Performance Area *</label>
                <input type="text" id="sKpa" maxlength="255" placeholder="e.g. Account Reconciliation" />
            </div>
            <div class="es-field">
                <label for="sStd">Expected Standard *</label>
                <textarea id="sStd" placeholder="e.g. Timely reconciliation of accounts."></textarea>
                <div class="es-hint">Editing wording does not change appraisals already started - they keep the text they were given.</div>
            </div>
        </div>
        <div class="es-modal__foot">
            <button type="button" class="es-btn" onclick="closeModal('stdModal')">Cancel</button>
            <button type="button" class="es-btn es-btn--primary" id="btnSaveStd" onclick="saveStandard()">Save Standard</button>
        </div>
    </div>
</div>

<div class="es-toast" id="esToast"></div>
</div>

<script type="text/javascript">
var ES_GROUP = parseInt(document.getElementById('<%= hfGroupId.ClientID %>').value || '0', 10);

(function () {
    if (window.ES_READ_ONLY) {
        var els = document.querySelectorAll('.es-edit-only');
        for (var i = 0; i < els.length; i++) els[i].style.display = 'none';
    }
})();

function toast(msg, ok) {
    var t = document.getElementById('esToast');
    t.textContent = msg || '';
    t.className = 'es-toast es-toast--' + (ok ? 'ok' : 'err') + ' is-on';
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, 3500);
}
function openModal(id) { document.getElementById(id).classList.add('is-open'); }
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }

function post(action, fields, cb) {
    var fd = new FormData();
    for (var k in fields) if (fields.hasOwnProperty(k)) fd.append(k, fields[k]);
    var xhr = new XMLHttpRequest();
    xhr.open('POST', 'ExpectedStandards.aspx?ajax=' + action, true);
    var m = document.querySelector('meta[name="csrf-token"]');
    if (m) xhr.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
    xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) return;
        var res;
        try { res = JSON.parse(xhr.responseText); } catch (e) { res = { ok: false, msg: 'Server error (HTTP ' + xhr.status + ')' }; }
        cb(res);
    };
    xhr.send(fd);
}
function get(action, id, cb) {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'ExpectedStandards.aspx?ajax=' + action + '&id=' + encodeURIComponent(id), true);
    xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) return;
        var res;
        try { res = JSON.parse(xhr.responseText); } catch (e) { res = { ok: false, msg: 'Server error (HTTP ' + xhr.status + ')' }; }
        cb(res);
    };
    xhr.send();
}
function reloadTo(gid) {
    setTimeout(function () { window.location.href = 'ExpectedStandards.aspx' + (gid ? '?gid=' + gid : ''); }, 600);
}

/* ── groups ── */
function addGroup() {
    document.getElementById('groupModalTitle').textContent = 'Add Group';
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
        document.getElementById('groupModalTitle').textContent = 'Edit Group';
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
    if (!code) { toast('Group code is required', false); return; }
    if (!name) { toast('Group name is required', false); return; }
    var btn = document.getElementById('btnSaveGroup');
    btn.disabled = true;
    post('save_group', {
        group_id: document.getElementById('gId').value,
        group_code: code, group_name: name,
        applies_to: document.getElementById('gApplies').value,
        sort_order: document.getElementById('gSort').value || '0'
    }, function (res) {
        btn.disabled = false;
        toast(res.msg, res.ok);
        if (res.ok) { closeModal('groupModal'); reloadTo(res.id || ES_GROUP); }
    });
}
function toggleGroup(id, active) {
    if (!active && !confirm('Deactivate this group? Departments mapped to it fall back to the default group for new appraisal records. Existing records are not changed.')) return;
    post('toggle_group', { id: id, active: active }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(id); });
}
function deleteGroup(id) {
    if (!confirm('Delete this empty group permanently?')) return;
    post('delete_group', { id: id }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(0); });
}

/* ── standards ── */
function addStandard() {
    if (!ES_GROUP) { toast('Select a group first', false); return; }
    document.getElementById('stdModalTitle').textContent = 'Add Standard';
    document.getElementById('sId').value = '0';
    document.getElementById('sKpa').value = '';
    document.getElementById('sStd').value = '';
    openModal('stdModal');
}
function editStandard(id) {
    get('get_standard', id, function (res) {
        if (!res.ok) { toast(res.msg, false); return; }
        var d = res.data;
        document.getElementById('stdModalTitle').textContent = 'Edit Standard';
        document.getElementById('sId').value = d.standard_id;
        document.getElementById('sKpa').value = d.kpa_title;
        document.getElementById('sStd').value = d.expected_standard;
        openModal('stdModal');
    });
}
function saveStandard() {
    var kpa = document.getElementById('sKpa').value.trim();
    var std = document.getElementById('sStd').value.trim();
    if (!kpa) { toast('Responsibility / KPA is required', false); return; }
    if (!std) { toast('Expected standard is required', false); return; }
    var btn = document.getElementById('btnSaveStd');
    btn.disabled = true;
    post('save_standard', {
        standard_id: document.getElementById('sId').value,
        group_id: ES_GROUP, kpa_title: kpa, expected_standard: std
    }, function (res) {
        btn.disabled = false;
        toast(res.msg, res.ok);
        if (res.ok) { closeModal('stdModal'); reloadTo(ES_GROUP); }
    });
}
function toggleStandard(id, active) {
    post('toggle_standard', { id: id, active: active }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(ES_GROUP); });
}
function deleteStandard(id) {
    if (!confirm('Delete this standard permanently? (Only possible because no appraisal uses it.)')) return;
    post('delete_standard', { id: id }, function (res) { toast(res.msg, res.ok); if (res.ok) reloadTo(ES_GROUP); });
}

/* ── reorder (active rows only; saved immediately) ── */
function moveRow(btn, dir) {
    var tr = btn.parentNode;
    while (tr && tr.tagName !== 'TR') tr = tr.parentNode;
    if (!tr) return;
    var sib = dir < 0 ? tr.previousElementSibling : tr.nextElementSibling;
    if (!sib || sib.getAttribute('data-active') !== '1') return;
    if (dir < 0) tr.parentNode.insertBefore(tr, sib); else tr.parentNode.insertBefore(sib, tr);
    var rows = document.querySelectorAll('#tblStandards tbody tr[data-active="1"]');
    var ids = [];
    for (var i = 0; i < rows.length; i++) {
        ids.push(rows[i].getAttribute('data-id'));
        var num = rows[i].querySelector('td.es-num');
        if (num) num.textContent = (i + 1);
    }
    post('reorder', { group_id: ES_GROUP, ids: ids.join(',') }, function (res) { if (!res.ok) toast(res.msg, false); });
}

/* ── department mapping ── */
function mapDept(sel) {
    var dept = sel.getAttribute('data-dept');
    var prev = sel.getAttribute('data-cur') || '0';
    sel.disabled = true;
    post('map_department', { dept_id: dept, group_id: sel.value }, function (res) {
        sel.disabled = false;
        toast(res.msg, res.ok);
        if (res.ok) { sel.setAttribute('data-cur', sel.value); sel.classList.add('is-saved'); }
        else sel.value = prev;
    });
}
</script>
</asp:Content>
