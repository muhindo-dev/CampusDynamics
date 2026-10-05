<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="CompetencyTemplates.aspx.cs" Inherits="COOPERP_NewScreens_CompetencyTemplates" Title="Competency Templates - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<style>
/* ===== COMPETENCY TEMPLATES ===== */
*,*::before,*::after{box-sizing:border-box;}
:root{--brand:#174DA4;--brand-light:#e8eef8;--brand-dark:#0f3670;--success:#28a745;--danger:#dc3545;--warning:#ffc107;--info:#17a2b8;--grey:#6c757d;--grey-light:#f4f5f7;--border:#dee2e6;--radius:6px;--shadow:0 1px 3px rgba(0,0,0,.08);}

/* ── Page header ── */
.pa-page-header{display:flex;align-items:center;gap:14px;margin-bottom:18px;flex-wrap:wrap;}
.pa-page-header__icon{width:44px;height:44px;border-radius:10px;background:var(--brand);display:flex;align-items:center;justify-content:center;color:#fff;flex-shrink:0;}
.pa-page-header__title{font-size:22px;font-weight:700;color:#1a1a1a;line-height:1.2;}
.pa-page-header__sub{font-size:12px;color:#888;margin-top:1px;}
.pa-page-header__actions{margin-left:auto;display:flex;gap:8px;align-items:center;}

/* ── Filter bar ── */
.pa-filter-bar{display:flex;align-items:center;gap:10px;flex-wrap:wrap;background:#fff;border:1px solid var(--border);border-radius:var(--radius);padding:10px 14px;margin-bottom:16px;}
.pa-filter-bar label{font-size:11px;font-weight:600;color:#555;white-space:nowrap;}
.pa-filter-bar select,.pa-filter-bar input[type="text"]{font-size:12px;padding:5px 8px;border:1px solid var(--border);border-radius:var(--radius);background:#fff;color:#333;}
.pa-filter-bar select{min-width:150px;}
.pa-filter-bar input[type="text"]{min-width:180px;}
.pa-filter-bar select:focus,.pa-filter-bar input:focus{outline:none;border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.15);}
.pa-filter-sep{width:1px;height:24px;background:var(--border);flex-shrink:0;}
@media(max-width:800px){.pa-filter-sep{display:none;}}

/* ── KPI cards ── */
.pa-kpi-row{display:flex;gap:10px;margin-bottom:16px;flex-wrap:wrap;}
.pa-kpi{background:#fff;border:1px solid #e0e5ed;border-top:3px solid transparent;padding:12px 16px;border-radius:0 0 var(--radius) var(--radius);min-width:120px;}
.pa-kpi--blue{border-top-color:var(--brand);}
.pa-kpi--green{border-top-color:var(--success);}
.pa-kpi--purple{border-top-color:#7c3aed;}
.pa-kpi--teal{border-top-color:#0d9488;}
.pa-kpi__body{min-width:0;}
.pa-kpi__val{font-size:22px;font-weight:700;color:#1a1a2e;line-height:1.15;}
.pa-kpi__label{font-size:9px;color:#888;text-transform:uppercase;letter-spacing:.3px;margin-top:1px;}

/* ── Section ── */
.pa-section{background:#fff;border:1px solid #e0e5ed;margin-bottom:16px;border-radius:var(--radius);}
.pa-section__hdr{padding:11px 16px;border-bottom:1px solid #e0e5ed;font-size:12px;font-weight:700;color:#333;display:flex;align-items:center;gap:8px;text-transform:uppercase;letter-spacing:.3px;}
.pa-section__hdr svg{flex-shrink:0;color:#666;}
.pa-section__hdr-right{margin-left:auto;font-size:11px;font-weight:400;color:#888;text-transform:none;letter-spacing:0;}
.pa-section__body{padding:0;}

/* ── Table ── */
.pa-table{width:100%;border-collapse:collapse;font-size:12px;}
.pa-table th{text-align:left;padding:7px 10px;border-bottom:2px solid #e0e5ed;color:#666;font-weight:600;text-transform:uppercase;font-size:10px;letter-spacing:.3px;white-space:nowrap;}
.pa-table td{padding:7px 10px;border-bottom:1px solid #f5f7fa;color:#333;vertical-align:middle;}
.pa-table tr:last-child td{border-bottom:none;}
.pa-table tr:hover td{background:#f9fbff;}
.pa-num{text-align:right;font-variant-numeric:tabular-nums;}

/* ── Group header row ── */
.ct-group-row td{background:#f4f6f9!important;padding:8px 10px!important;border-bottom:1px solid #e0e5ed!important;font-size:12px;}

/* ── Category badge ── */
.pa-cat-badge{display:inline-block;padding:2px 7px;border-radius:3px;font-size:10px;font-weight:600;letter-spacing:.2px;}
.pa-cat-badge--academic{background:#e8f0fe;color:#174DA4;}
.pa-cat-badge--administrative{background:#ede9fe;color:#5b21b6;}
.pa-cat-badge--support{background:#ccfbf1;color:#0d9488;}

/* ── Inline action buttons ── */
.ct-btn{display:inline-flex;align-items:center;justify-content:center;width:28px;height:28px;border:1px solid var(--border);border-radius:var(--radius);background:#fff;cursor:pointer;transition:all .15s;color:#666;}
.ct-btn:hover{border-color:var(--brand);color:var(--brand);background:var(--brand-light);}
.ct-btn--delete:hover{border-color:var(--danger);color:var(--danger);background:#fff5f5;}

/* ── Alert ── */
.pa-alert--error{padding:12px 16px;background:#f8d7da;color:#721c24;border-radius:var(--radius);margin-bottom:14px;font-size:13px;}

/* ── Buttons ── */
.hr-btn{display:inline-flex;align-items:center;gap:6px;padding:7px 14px;font-size:12px;font-weight:600;border:none;border-radius:var(--radius);cursor:pointer;transition:all .15s ease;text-decoration:none;}
.hr-btn--primary{background:var(--brand);color:#fff;}
.hr-btn--primary:hover{background:var(--brand-dark);}
.hr-btn--success{background:var(--success);color:#fff;}
.hr-btn--success:hover{background:#1e7e34;}
.hr-btn--outline{background:transparent;border:1px solid var(--border);color:#555;}
.hr-btn--outline:hover{border-color:var(--brand);color:var(--brand);}
.hr-btn--danger{background:var(--danger);color:#fff;}
.hr-btn--danger:hover{background:#bd2130;}

/* ── Modal ── */
.ct-modal-overlay{display:none;position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,.45);z-index:9999;align-items:center;justify-content:center;}
.ct-modal-overlay.active{display:flex;}
.ct-modal{background:#fff;border-radius:8px;width:520px;max-width:95vw;max-height:90vh;overflow-y:auto;box-shadow:0 12px 40px rgba(0,0,0,.2);}
.ct-modal__hdr{padding:16px 20px;border-bottom:1px solid var(--border);display:flex;align-items:center;gap:10px;}
.ct-modal__hdr h3{margin:0;font-size:16px;font-weight:700;color:#1a1a2e;flex:1;}
.ct-modal__close{background:none;border:none;cursor:pointer;color:#999;font-size:20px;padding:2px 6px;border-radius:4px;}
.ct-modal__close:hover{background:#f0f0f0;color:#333;}
.ct-modal__body{padding:20px;}
.ct-modal__footer{padding:14px 20px;border-top:1px solid var(--border);display:flex;justify-content:flex-end;gap:8px;}

/* ── Form fields ── */
.ct-field{margin-bottom:14px;}
.ct-field label{display:block;font-size:11px;font-weight:600;color:#555;margin-bottom:4px;text-transform:uppercase;letter-spacing:.3px;}
.ct-field input,.ct-field select,.ct-field textarea{width:100%;font-size:13px;padding:7px 10px;border:1px solid var(--border);border-radius:var(--radius);background:#fff;color:#333;font-family:inherit;}
.ct-field input:focus,.ct-field select:focus,.ct-field textarea:focus{outline:none;border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.15);}
.ct-field textarea{resize:vertical;min-height:60px;}
.ct-field-row{display:grid;grid-template-columns:1fr 1fr;gap:12px;}
@media(max-width:500px){.ct-field-row{grid-template-columns:1fr;}}

/* ── Toast ── */
.ct-toast{position:fixed;top:20px;right:20px;padding:12px 20px;border-radius:var(--radius);font-size:13px;font-weight:600;color:#fff;z-index:10000;opacity:0;transition:opacity .3s;pointer-events:none;box-shadow:0 4px 12px rgba(0,0,0,.15);}
.ct-toast.show{opacity:1;}
.ct-toast--success{background:var(--success);}
.ct-toast--error{background:var(--danger);}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- ── Error placeholder ── -->
<asp:Literal ID="litError" runat="server" />

<!-- ── Page Header ── -->
<div class="pa-page-header">
    <div class="pa-page-header__icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="3" y1="15" x2="21" y2="15"/><line x1="9" y1="3" x2="9" y2="21"/></svg>
    </div>
    <div>
        <div class="pa-page-header__title">Competency Templates</div>
        <div class="pa-page-header__sub">Manage appraisal competency criteria by staff category</div>
    </div>
    <div class="pa-page-header__actions">
        <button type="button" class="hr-btn hr-btn--primary" onclick="openCreateModal()">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            Add Competency
        </button>
    </div>
</div>

<!-- ── Summary Stats ── -->
<div class="pa-kpi-row">
    <div class="pa-kpi pa-kpi--blue">
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiTotal" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Total Competencies</div>
        </div>
    </div>
    <div class="pa-kpi pa-kpi--green">
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiGroups" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Category Groups</div>
        </div>
    </div>
    <asp:Literal ID="litCatStats" runat="server" />
</div>

<!-- ── Filter Bar ── -->
<div class="pa-filter-bar">
    <label>Staff Category:</label>
    <select id="selCategory" onchange="applyFilters()">
        <asp:Literal ID="litCatOptions" runat="server" />
    </select>
    <div class="pa-filter-sep"></div>
    <label>Search:</label>
    <input type="text" id="txtSearch" placeholder="Code, name, or group..." value="<%= HttpUtility.HtmlAttributeEncode(Request.QueryString["q"] ?? "") %>" onkeydown="if(event.key==='Enter')applyFilters()" />
    <button type="button" class="hr-btn hr-btn--outline" onclick="applyFilters()" style="font-size:11px;padding:5px 10px;">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
        Search
    </button>
    <button type="button" class="hr-btn hr-btn--outline" onclick="clearFilters()" style="font-size:11px;padding:5px 10px;">
        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
        Clear
    </button>
</div>

<!-- ── Templates Table ── -->
<div class="pa-section">
    <div class="pa-section__hdr">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="9" y1="3" x2="9" y2="21"/></svg>
        Competency Templates
        <span class="pa-section__hdr-right"><asp:Literal ID="litRecordCount" runat="server" Text="0" /> entries</span>
    </div>
    <div class="pa-section__body">
        <div style="overflow-x:auto;">
        <table class="pa-table" id="tblTemplates">
            <thead>
                <tr>
                    <th style="width:36px;">#</th>
                    <th>Code</th>
                    <th>Competency Name</th>
                    <th>Group</th>
                    <th style="text-align:right;">Order</th>
                    <th>Description</th>
                    <th style="width:70px;">Actions</th>
                </tr>
            </thead>
            <tbody>
                <asp:Literal ID="litRows" runat="server" />
            </tbody>
        </table>
        </div>
    </div>
</div>

<!-- ── Modal: Create/Edit ── -->
<div class="ct-modal-overlay" id="modalOverlay">
    <div class="ct-modal">
        <div class="ct-modal__hdr">
            <h3 id="modalTitle">Add Competency</h3>
            <button type="button" class="ct-modal__close" onclick="closeModal()">&times;</button>
        </div>
        <div class="ct-modal__body">
            <input type="hidden" id="fldTemplateId" value="0" />
            <div class="ct-field-row">
                <div class="ct-field">
                    <label>Staff Category *</label>
                    <select id="fldStaffCategory">
                        <option value="">— Select —</option>
                        <option value="ACADEMIC">Academic</option>
                        <option value="ADMINISTRATIVE">Administrative</option>
                        <option value="SUPPORT">Support</option>
                    </select>
                </div>
                <div class="ct-field">
                    <label>Competency Code *</label>
                    <input type="text" id="fldCode" placeholder="e.g. C1.1" maxlength="10" />
                </div>
            </div>
            <div class="ct-field">
                <label>Category / Group Name *</label>
                <input type="text" id="fldCategoryName" placeholder="e.g. Teaching Function" />
            </div>
            <div class="ct-field">
                <label>Competency Name *</label>
                <input type="text" id="fldCompetencyName" placeholder="e.g. Preparation of Schemes of Work" />
            </div>
            <div class="ct-field-row">
                <div class="ct-field">
                    <label>Sort Order</label>
                    <input type="number" id="fldSortOrder" value="0" min="0" />
                </div>
                <div></div>
            </div>
            <div class="ct-field">
                <label>Description (optional)</label>
                <textarea id="fldDescription" rows="3" placeholder="Additional details about this competency..."></textarea>
            </div>
        </div>
        <div class="ct-modal__footer">
            <button type="button" class="hr-btn hr-btn--outline" onclick="closeModal()">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnSave" onclick="saveForm()">Save</button>
        </div>
    </div>
</div>

<!-- ── Toast ── -->
<div class="ct-toast" id="ctToast"></div>

<script type="text/javascript">
/* ── Filter navigation ── */
function applyFilters() {
    var cat = document.getElementById('selCategory').value;
    var q   = document.getElementById('txtSearch').value.trim();
    var params = [];
    if (cat) params.push('cat=' + encodeURIComponent(cat));
    if (q)   params.push('q=' + encodeURIComponent(q));
    var url = 'CompetencyTemplates.aspx';
    if (params.length > 0) url += '?' + params.join('&');
    window.location.href = url;
}

function clearFilters() {
    window.location.href = 'CompetencyTemplates.aspx';
}

/* ── Modal control ── */
function openCreateModal() {
    document.getElementById('modalTitle').innerText = 'Add Competency';
    document.getElementById('fldTemplateId').value = '0';
    document.getElementById('fldStaffCategory').value = '';
    document.getElementById('fldCode').value = '';
    document.getElementById('fldCategoryName').value = '';
    document.getElementById('fldCompetencyName').value = '';
    document.getElementById('fldSortOrder').value = '0';
    document.getElementById('fldDescription').value = '';
    document.getElementById('modalOverlay').classList.add('active');
}

function closeModal() {
    document.getElementById('modalOverlay').classList.remove('active');
}

/* ── Edit row ── */
function editRow(id) {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', 'CompetencyTemplates.aspx?ajax=get&id=' + id, true);
    xhr.onreadystatechange = function() {
        if (xhr.readyState !== 4) return;
        try {
            var resp = JSON.parse(xhr.responseText);
            if (!resp.ok) { showToast(resp.msg || 'Load failed', 'error'); return; }
            var d = resp.data;
            document.getElementById('modalTitle').innerText = 'Edit Competency';
            document.getElementById('fldTemplateId').value = d.template_id;
            document.getElementById('fldStaffCategory').value = d.staff_category;
            document.getElementById('fldCode').value = d.competency_code;
            document.getElementById('fldCategoryName').value = d.category_name;
            document.getElementById('fldCompetencyName').value = d.competency_name;
            document.getElementById('fldSortOrder').value = d.sort_order;
            document.getElementById('fldDescription').value = d.description || '';
            document.getElementById('modalOverlay').classList.add('active');
        } catch(e) {
            showToast('Failed to load record', 'error');
        }
    };
    xhr.send();
}

/* ── Delete row ── */
function deleteRow(id) {
    if (!confirm('Delete this competency template? This cannot be undone.')) return;
    var fd = new FormData();
    fd.append('template_id', id);
    var xhr = new XMLHttpRequest();
    xhr.open('POST', 'CompetencyTemplates.aspx?ajax=delete', true);
    setCsrf(xhr);
    xhr.onreadystatechange = function() {
        if (xhr.readyState !== 4) return;
        try {
            var resp = JSON.parse(xhr.responseText);
            showToast(resp.msg, resp.ok ? 'success' : 'error');
            if (resp.ok) setTimeout(function(){ location.reload(); }, 800);
        } catch(e) {
            showToast('Delete failed', 'error');
        }
    };
    xhr.send(fd);
}

/* ── Save form ── */
function saveForm() {
    var cat  = document.getElementById('fldStaffCategory').value;
    var code = document.getElementById('fldCode').value.trim();
    var catName = document.getElementById('fldCategoryName').value.trim();
    var compName = document.getElementById('fldCompetencyName').value.trim();

    if (!cat) { showToast('Please select a staff category', 'error'); return; }
    if (!code) { showToast('Competency code is required', 'error'); return; }
    if (!catName) { showToast('Category/group name is required', 'error'); return; }
    if (!compName) { showToast('Competency name is required', 'error'); return; }

    var btn = document.getElementById('btnSave');
    btn.disabled = true;
    btn.innerText = 'Saving...';

    var fd = new FormData();
    fd.append('template_id', document.getElementById('fldTemplateId').value);
    fd.append('staff_category', cat);
    fd.append('competency_code', code);
    fd.append('category_name', catName);
    fd.append('competency_name', compName);
    fd.append('sort_order', document.getElementById('fldSortOrder').value || '0');
    fd.append('description', document.getElementById('fldDescription').value);

    var xhr = new XMLHttpRequest();
    xhr.open('POST', 'CompetencyTemplates.aspx?ajax=save', true);
    setCsrf(xhr);
    xhr.onreadystatechange = function() {
        if (xhr.readyState !== 4) return;
        btn.disabled = false;
        btn.innerText = 'Save';
        try {
            var resp = JSON.parse(xhr.responseText);
            showToast(resp.msg, resp.ok ? 'success' : 'error');
            if (resp.ok) {
                closeModal();
                setTimeout(function(){ location.reload(); }, 800);
            }
        } catch(e) {
            showToast('Save failed', 'error');
        }
    };
    xhr.send(fd);
}

/* ── Anti-forgery token (same pattern as AppraisalSessions / AppraisalView) ── */
function setCsrf(xhr) {
    var m = document.querySelector('meta[name="csrf-token"]');
    if (m) xhr.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
}

/* ── Toast ── */
function showToast(msg, type) {
    var el = document.getElementById('ctToast');
    el.innerText = msg;
    el.className = 'ct-toast ct-toast--' + (type || 'success') + ' show';
    setTimeout(function(){ el.classList.remove('show'); }, 3000);
}
</script>

</asp:Content>
