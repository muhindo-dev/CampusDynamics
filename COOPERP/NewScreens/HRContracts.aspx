<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="HRContracts.aspx.cs" Inherits="COOPERP_NewScreens_HRContracts" Title="Contracts - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=1" />
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.js") %>?v=1"></script>
<style>
a.hr-kpi.ct-kpi--on { border-color: var(--hr-navy); }
.ct-chk { width: 34px; }
.ct-chk input { accent-color: var(--hr-navy); }
.ct-pick .hr-input { margin-bottom: 4px; }
.ct-section { grid-column: 1 / -1; font-size: 11px; font-weight: 700; color: var(--hr-navy); text-transform: uppercase; letter-spacing: .4px; padding-bottom: 4px; border-bottom: 1px solid var(--hr-border); margin-top: 4px; }
.ct-bulk-select { height: 26px; width: auto; padding: 0 6px; font-size: 11px; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<asp:HiddenField ID="hdnBatchIDs"         runat="server" />
<asp:HiddenField ID="hdnBatchStatus"      runat="server" />
<asp:HiddenField ID="hdnRenewContractID"  runat="server" />
<asp:HiddenField ID="hdnRenewEmpID"       runat="server" />
<asp:HiddenField ID="hdnEditContractID"   runat="server" />
<asp:HiddenField ID="hdnDeleteContractID" runat="server" />
<asp:HiddenField ID="hfSelectedEmpID"     runat="server" />
<asp:Button ID="btnBatchDelete"    runat="server" style="display:none;" OnClick="btnBatchDelete_Click" />
<asp:Button ID="btnBatchStatus"    runat="server" style="display:none;" OnClick="btnBatchStatus_Click" />
<asp:Button ID="btnUpdateContract" runat="server" style="display:none;" OnClick="btnUpdateContract_Click" />
<asp:Button ID="btnDeleteContract" runat="server" style="display:none;" OnClick="btnDeleteContract_Click" />

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Contracts</div>
            <div class="hr-header__sub">Employment contracts, terms and pay scales</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openAddModal()">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            New contract
        </button>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab hr-tab--active" href="HRContracts.aspx">Contracts</a><a class="hr-tab" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a></div>

<asp:Literal ID="litKpis" runat="server" />

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="fltSearch">Search</label>
        <input type="text" id="fltSearch" class="hr-input" placeholder="Name, staff number or comment" value="<%= HttpUtility.HtmlAttributeEncode(SearchValue) %>" />
    </div>
    <div class="hr-filter">
        <label for="fltView">Show</label>
        <select id="fltView" class="hr-select"><asp:Literal ID="litViewOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="fltStatus">Status</label>
        <select id="fltStatus" class="hr-select"><asp:Literal ID="litStatusOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="fltType">Type</label>
        <select id="fltType" class="hr-select"><asp:Literal ID="litTypeOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="fltDept">Department</label>
        <select id="fltDept" class="hr-select"><asp:Literal ID="litDeptOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="fltJob">Position</label>
        <select id="fltJob" class="hr-select"><asp:Literal ID="litJobOptions" runat="server" /></select>
    </div>
    <div class="hr-filter" style="flex-basis:90px;min-width:80px">
        <label for="fltSize">Per page</label>
        <select id="fltSize" class="hr-select"><asp:Literal ID="litSizeOptions" runat="server" /></select>
    </div>
    <div class="hr-filters__actions">
        <button type="button" class="hr-btn hr-btn--primary" onclick="applyFilters()">Apply</button>
        <a class="hr-btn hr-btn--secondary" href="HRContracts.aspx">Reset</a>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Contract register <span class="hr-card__meta"><asp:Literal ID="litTotalCount" runat="server" />, <asp:Literal ID="litStaffTotal" runat="server" /></span></div>
        <div class="hr-row"><asp:Literal ID="litExport" runat="server" /></div>
    </div>

    <div class="hr-bulk" id="batchToolbar">
        <strong id="batchCount">0</strong>&nbsp;selected
        <span class="hr-spacer"></span>
        <select id="ddlBatchStatus" class="hr-select ct-bulk-select" aria-label="New status">
            <option value="">Set status to</option>
            <option value="VALID">Valid</option>
            <option value="EXPIRED">Expired</option>
            <option value="TERMINATED">Terminated</option>
            <option value="RESIGNED">Resigned</option>
        </select>
        <button type="button" class="hr-btn hr-btn--secondary hr-btn--sm" onclick="doBatchStatus()">Set status</button>
        <button type="button" class="hr-btn hr-btn--danger hr-btn--sm" onclick="doBatchDelete()">Delete</button>
        <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="clearBatchSelection()">Clear</button>
    </div>

    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th class="ct-chk"><input type="checkbox" id="chkSelectAll" onclick="toggleSelectAll(this)" aria-label="Select all" /></th>
                    <th>Employee</th>
                    <th>Department</th>
                    <th>Position</th>
                    <th>Type</th>
                    <th>Status</th>
                    <th>Start</th>
                    <th>End</th>
                    <th class="hr-num">Basic pay (UGX)</th>
                    <th></th>
                </tr>
            </thead>
            <tbody>
                <asp:Literal ID="litGridBody" runat="server" />
            </tbody>
        </table>
    </div>
    <div class="hr-card__foot">
        <span><asp:Literal ID="litPagerInfo" runat="server" /></span>
        <asp:Literal ID="litPager" runat="server" />
    </div>
</div>

<!-- New contract -->
<div class="hr-modal" id="addModal">
    <div class="hr-modal__box hr-modal__box--wide">
        <div class="hr-modal__head"><span>New contract</span><button type="button" class="hr-modal__close" onclick="closeModal('addModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field hr-full ct-pick">
                    <label class="hr-label" for="addEmpFilter">Employee <span class="hr-req">*</span></label>
                    <input type="text" id="addEmpFilter" class="hr-input" placeholder="Type to filter the list" autocomplete="off" />
                    <select id="addEmp" class="hr-select"><asp:Literal ID="litEmpOptions" runat="server" /></select>
                </div>
                <div class="hr-field">
                    <label class="hr-label">Contract type <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlContractType" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field"></div>
                <div class="hr-field">
                    <label class="hr-label">Start date <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtContractStart" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">End date <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtContractEnd" runat="server" CssClass="hr-input" TextMode="Date" />
                    <div class="hr-hint" id="contractDurationHint"></div>
                </div>
                <div class="hr-field">
                    <label class="hr-label">Position <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlContractJob" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Department <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlContractDept" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Pay scale <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlContractScale" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Fixed amount (UGX)</label>
                    <asp:TextBox ID="txtContractFixed" runat="server" CssClass="hr-input" TextMode="Number" Text="0" />
                    <div class="hr-hint">Used only when the pay scale has no basic pay.</div>
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label">Comments</label>
                    <asp:TextBox ID="txtContractComment" runat="server" CssClass="hr-textarea" TextMode="MultiLine" Rows="2" />
                </div>
            </div>
            <div class="hr-error" id="addResult" role="alert"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('addModal')">Cancel</button>
            <asp:Button ID="btnAddContract" runat="server" Text="Create contract" CssClass="hr-btn hr-btn--primary" OnClick="btnAddContract_Click"
                OnClientClick="return validateAddModal();" />
        </div>
    </div>
</div>

<!-- Renew -->
<div class="hr-modal" id="renewModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Renew contract</span><button type="button" class="hr-modal__close" onclick="closeModal('renewModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <dl class="hr-dl hr-dl--2" style="margin-bottom:14px">
                <div><dt>Employee</dt><dd id="renewEmpName"></dd></div>
                <div><dt>Current contract ends</dt><dd id="renewEmpEnd"></dd></div>
            </dl>
            <div class="hr-form">
                <div class="hr-field hr-full">
                    <label class="hr-label">Contract type <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlRenewType" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">New start date <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtRenewStart" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">New end date <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtRenewEnd" runat="server" CssClass="hr-input" TextMode="Date" />
                    <div class="hr-hint" id="renewDurationHint"></div>
                </div>
            </div>
            <p class="hr-hint" style="margin:12px 0 0">Renewals that need an application and Council approval go through <a href="ContractRenewals.aspx">Contract renewals</a>.</p>
            <div class="hr-error" id="renewResult" role="alert"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('renewModal')">Cancel</button>
            <asp:Button ID="btnRenewContract" runat="server" Text="Renew contract" CssClass="hr-btn hr-btn--primary" OnClick="btnRenewContract_Click"
                OnClientClick="return validateRenewModal();" />
        </div>
    </div>
</div>

<!-- Edit -->
<div class="hr-modal" id="editModal">
    <div class="hr-modal__box hr-modal__box--wide">
        <div class="hr-modal__head"><span id="editTitle">Edit contract</span><button type="button" class="hr-modal__close" onclick="closeModal('editModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field">
                    <label class="hr-label">Contract type <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlEditType" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Status <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlEditStatus" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Start date <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtEditStart" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">End date <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtEditEnd" runat="server" CssClass="hr-input" TextMode="Date" />
                    <div class="hr-hint" id="editDurationHint"></div>
                </div>
                <div class="hr-field">
                    <label class="hr-label">Position <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlEditJob" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Department <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlEditDept" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Pay scale <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlEditScale" runat="server" CssClass="hr-select" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Fixed amount (UGX)</label>
                    <asp:TextBox ID="txtEditFixed" runat="server" CssClass="hr-input" TextMode="Number" Text="0" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label">Comments</label>
                    <asp:TextBox ID="txtEditComment" runat="server" CssClass="hr-textarea" TextMode="MultiLine" Rows="2" />
                </div>
            </div>
            <div class="hr-error" id="editResult" role="alert"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--danger" onclick="confirmDelete()">Delete contract</button>
            <span class="hr-spacer"></span>
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('editModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" onclick="submitEditModal()">Save changes</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="hrToast" role="status" aria-live="polite"></div>

</div>

<script type="text/javascript">
function el(id){ return document.getElementById(id); }
function openModal(id){ el(id).classList.add('is-open'); }
function closeModal(id){ el(id).classList.remove('is-open'); }
function showToast(msg, isErr){
    var t = el('hrToast');
    t.textContent = msg;
    t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function(){ t.className = 'hr-toast'; }, 4000);
}

/* Filters */
function applyFilters(){
    var p = [];
    function add(k, id){ var v = el(id).value; if(v) p.push(k + '=' + encodeURIComponent(v)); }
    var q = el('fltSearch').value.trim();
    if(q) p.push('q=' + encodeURIComponent(q));
    if(el('fltView').value === 'all') p.push('view=all');
    add('status', 'fltStatus'); add('type', 'fltType'); add('dept', 'fltDept'); add('job', 'fltJob');
    if(el('fltSize').value !== '50') p.push('sz=' + el('fltSize').value);
    location.href = 'HRContracts.aspx' + (p.length ? '?' + p.join('&') : '');
}
el('fltSearch').addEventListener('keydown', function(e){ if(e.key === 'Enter'){ e.preventDefault(); applyFilters(); } });
['fltView','fltStatus','fltType','fltDept','fltJob','fltSize'].forEach(function(id){ el(id).addEventListener('change', applyFilters); });

/* Employee picker: a filter box above a plain select */
var EMP_OPTS = [];
(function(){
    var s = el('addEmp');
    for(var i = 0; i < s.options.length; i++) EMP_OPTS.push([s.options[i].value, s.options[i].text]);
})();
function esc(t){ return String(t).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/"/g,'&quot;'); }
function renderEmpOptions(q){
    var s = el('addEmp'), keep = s.value, html = '', first = '';
    q = (q || '').toLowerCase();
    for(var i = 0; i < EMP_OPTS.length; i++){
        var o = EMP_OPTS[i];
        if(o[0] && q && o[1].toLowerCase().indexOf(q) < 0) continue;
        if(o[0] && !first) first = o[0];
        html += '<option value="' + esc(o[0]) + '">' + esc(o[1]) + '</option>';
    }
    s.innerHTML = html;
    s.value = keep;
    if(!s.value && q && first) s.value = first;
    onEmpPicked();
}
function onEmpPicked(){
    var id = el('addEmp').value;
    el('<%= hfSelectedEmpID.ClientID %>').value = id;
    if(!id || typeof employeeDefaults === 'undefined') return;
    var d = employeeDefaults[id];
    if(!d) return;
    if(d.j) el('<%= ddlContractJob.ClientID %>').value = d.j;
    if(d.d) el('<%= ddlContractDept.ClientID %>').value = d.d;
    if(d.s) el('<%= ddlContractScale.ClientID %>').value = d.s;
}
el('addEmpFilter').addEventListener('input', function(){ renderEmpOptions(this.value.trim()); });
el('addEmp').addEventListener('change', onEmpPicked);

/* New contract */
function openAddModal(empID){
    el('addResult').textContent = '';
    el('addEmpFilter').value = '';
    renderEmpOptions('');
    el('addEmp').value = empID ? String(empID) : '';
    ['<%= ddlContractJob.ClientID %>','<%= ddlContractDept.ClientID %>','<%= ddlContractScale.ClientID %>'].forEach(function(id){ el(id).value = ''; });
    el('<%= ddlContractType.ClientID %>').selectedIndex = 0;
    el('<%= txtContractStart.ClientID %>').value = '';
    el('<%= txtContractEnd.ClientID %>').value = '';
    el('<%= txtContractFixed.ClientID %>').value = '0';
    el('<%= txtContractComment.ClientID %>').value = '';
    el('contractDurationHint').textContent = '';
    onEmpPicked();
    openModal('addModal');
    if(!empID) el('addEmpFilter').focus();
}
function validateAddModal(){
    var r = el('addResult');
    function fail(m){ r.textContent = m; return false; }
    if(!el('<%= hfSelectedEmpID.ClientID %>').value) return fail('Choose an employee.');
    if(!el('<%= ddlContractJob.ClientID %>').value) return fail('Choose a position.');
    if(!el('<%= ddlContractDept.ClientID %>').value) return fail('Choose a department.');
    if(!el('<%= ddlContractScale.ClientID %>').value) return fail('Choose a pay scale.');
    var s = el('<%= txtContractStart.ClientID %>').value, e = el('<%= txtContractEnd.ClientID %>').value;
    if(!s || !e) return fail('Enter the start and end dates.');
    if(e <= s) return fail('The end date must be after the start date.');
    return true;
}

/* Edit */
var editContractId = '', editEmpName = '';
function openEditModal(cid, jobID, deptID, scaleID, type, status, startDt, endDt, fixedAmt, comment, empName){
    editContractId = cid; editEmpName = empName || '';
    el('<%= hdnEditContractID.ClientID %>').value = cid;
    el('editTitle').textContent = 'Edit contract: ' + (empName || '');
    el('<%= ddlEditType.ClientID %>').value = type || 'FULL TIME';
    el('<%= ddlEditStatus.ClientID %>').value = status || 'VALID';
    el('<%= ddlEditJob.ClientID %>').value = jobID || '';
    el('<%= ddlEditDept.ClientID %>').value = deptID || '';
    el('<%= ddlEditScale.ClientID %>').value = scaleID || '';
    el('<%= txtEditStart.ClientID %>').value = startDt || '';
    el('<%= txtEditEnd.ClientID %>').value = endDt || '';
    el('<%= txtEditFixed.ClientID %>').value = fixedAmt || '0';
    el('<%= txtEditComment.ClientID %>').value = comment || '';
    el('editResult').textContent = '';
    showDuration('<%= txtEditStart.ClientID %>', '<%= txtEditEnd.ClientID %>', 'editDurationHint');
    openModal('editModal');
}
function submitEditModal(){
    var r = el('editResult');
    if(!el('<%= ddlEditJob.ClientID %>').value){ r.textContent = 'Choose a position.'; return; }
    if(!el('<%= ddlEditDept.ClientID %>').value){ r.textContent = 'Choose a department.'; return; }
    if(!el('<%= ddlEditScale.ClientID %>').value){ r.textContent = 'Choose a pay scale.'; return; }
    var s = el('<%= txtEditStart.ClientID %>').value, e = el('<%= txtEditEnd.ClientID %>').value;
    if(!s || !e){ r.textContent = 'Enter the start and end dates.'; return; }
    if(e <= s){ r.textContent = 'The end date must be after the start date.'; return; }
    el('<%= btnUpdateContract.ClientID %>').click();
}
function confirmDelete(){
    if(!editContractId) return;
    hrConfirm({ title: 'Delete contract', message: 'Delete this contract for ' + editEmpName + '? This cannot be undone.', ok: 'Delete', danger: true }, function(){
        el('<%= hdnDeleteContractID.ClientID %>').value = editContractId;
        el('<%= btnDeleteContract.ClientID %>').click();
    });
}

/* Renew */
function openRenewModal(contractID, empID, empName, endDate, contractType){
    el('<%= hdnRenewContractID.ClientID %>').value = contractID;
    el('<%= hdnRenewEmpID.ClientID %>').value = empID;
    el('renewEmpName').textContent = empName;
    el('renewEmpEnd').textContent = endDate ? fmtDate(endDate) : 'Not recorded';
    el('<%= txtRenewStart.ClientID %>').value = '';
    el('<%= txtRenewEnd.ClientID %>').value = '';
    el('renewDurationHint').textContent = '';
    if(contractType) el('<%= ddlRenewType.ClientID %>').value = contractType;
    el('renewResult').textContent = '';
    openModal('renewModal');
}
function validateRenewModal(){
    var s = el('<%= txtRenewStart.ClientID %>').value, e = el('<%= txtRenewEnd.ClientID %>').value;
    if(!s || !e){ el('renewResult').textContent = 'Enter the start and end dates.'; return false; }
    if(e <= s){ el('renewResult').textContent = 'The end date must be after the start date.'; return false; }
    return true;
}

/* Bulk */
function checkedBoxes(){ return document.querySelectorAll('.ct-row-check:checked'); }
function updateBatchToolbar(){
    var n = checkedBoxes().length, all = document.querySelectorAll('.ct-row-check').length;
    el('batchToolbar').classList.toggle('is-on', n > 0);
    el('batchCount').textContent = n;
    var chk = el('chkSelectAll');
    chk.indeterminate = n > 0 && n < all;
    chk.checked = all > 0 && n === all;
}
function toggleSelectAll(cb){
    var boxes = document.querySelectorAll('.ct-row-check');
    for(var i = 0; i < boxes.length; i++) boxes[i].checked = cb.checked;
    updateBatchToolbar();
}
function clearBatchSelection(){ el('chkSelectAll').checked = false; toggleSelectAll(el('chkSelectAll')); }
function batchIds(){
    var ids = [], b = checkedBoxes();
    for(var i = 0; i < b.length; i++) ids.push(b[i].value);
    return ids.join(',');
}
function doBatchStatus(){
    var status = el('ddlBatchStatus').value, n = checkedBoxes().length;
    if(!status){ showToast('Choose the status to set.', true); return; }
    if(!n) return;
    var label = el('ddlBatchStatus').options[el('ddlBatchStatus').selectedIndex].text.toLowerCase();
    hrConfirm({ title: 'Set contract status', message: 'Set ' + n + (n === 1 ? ' contract' : ' contracts') + ' to ' + label + '?', ok: 'Set status' }, function(){
        el('<%= hdnBatchIDs.ClientID %>').value = batchIds();
        el('<%= hdnBatchStatus.ClientID %>').value = status;
        el('<%= btnBatchStatus.ClientID %>').click();
    });
}
function doBatchDelete(){
    var n = checkedBoxes().length;
    if(!n) return;
    hrConfirm({ title: 'Delete contracts', message: 'Delete ' + n + (n === 1 ? ' contract' : ' contracts') + '? This cannot be undone.', ok: 'Delete', danger: true }, function(){
        el('<%= hdnBatchIDs.ClientID %>').value = batchIds();
        el('<%= btnBatchDelete.ClientID %>').click();
    });
}

/* Duration hints */
var MONTHS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
function fmtDate(iso){ var p = (iso || '').split('-'); return p.length === 3 ? (+p[2]) + ' ' + MONTHS[+p[1] - 1] + ' ' + p[0] : iso; }
function showDuration(startId, endId, hintId){
    var s = el(startId).value, e = el(endId).value, h = el(hintId);
    if(!s || !e){ h.textContent = ''; return; }
    var days = Math.round((new Date(e) - new Date(s)) / 864e5);
    if(days <= 0){ h.textContent = 'The end date must be after the start date.'; return; }
    var months = Math.round(days / 30.44);
    h.textContent = months > 0 ? months + (months === 1 ? ' month' : ' months') : days + (days === 1 ? ' day' : ' days');
}
[['<%= txtContractStart.ClientID %>','<%= txtContractEnd.ClientID %>','contractDurationHint'],
 ['<%= txtRenewStart.ClientID %>','<%= txtRenewEnd.ClientID %>','renewDurationHint'],
 ['<%= txtEditStart.ClientID %>','<%= txtEditEnd.ClientID %>','editDurationHint']].forEach(function(t){
    var f = function(){ showDuration(t[0], t[1], t[2]); };
    el(t[0]).addEventListener('change', f);
    el(t[1]).addEventListener('change', f);
});

document.addEventListener('keydown', function(e){
    if(e.key === 'Escape'){ closeModal('addModal'); closeModal('renewModal'); closeModal('editModal'); }
    if(e.key === 'Enter' && e.target && e.target.tagName === 'INPUT' && e.target.closest && e.target.closest('.hr-modal')) e.preventDefault();
});
</script>
</asp:Content>
