<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="HRLeaveManagement.aspx.cs" Inherits="COOPERP_NewScreens_HRLeaveManagement" Title="Leave balances - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=1" />
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.js") %>?v=1"></script>
<style>
.lm-pick .hr-input { margin-bottom: 4px; }
.lm-info { min-height: 18px; margin-top: 4px; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/><polyline points="9 16 11 18 15 14"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Leave balances</div>
            <div class="hr-header__sub">Annual leave allocated and taken</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openAllocate()">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            Allocate leave
        </button>
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openRecord()">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 11 12 14 22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>
            Record leave taken
        </button>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab hr-tab--active" href="HRLeaveManagement.aspx">Leave balances</a></div>

<asp:Literal ID="litError" runat="server" />

<div class="hr-kpis">
    <div class="hr-kpi">
        <div class="hr-kpi__label">Staff with an allocation</div>
        <div class="hr-kpi__value"><asp:Literal ID="litTotalStaff" runat="server" Text="0" /></div>
    </div>
    <div class="hr-kpi">
        <div class="hr-kpi__label">On leave today</div>
        <div class="hr-kpi__value"><asp:Literal ID="litOnLeave" runat="server" Text="0" /></div>
    </div>
    <div class="hr-kpi">
        <div class="hr-kpi__label">Days taken</div>
        <div class="hr-kpi__value"><asp:Literal ID="litTotalDaysTaken" runat="server" Text="0" /></div>
    </div>
    <div class="hr-kpi">
        <div class="hr-kpi__label">Leave used up</div>
        <div class="hr-kpi__value"><asp:Literal ID="litExhausted" runat="server" Text="0" /></div>
    </div>
</div>

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="fltSearch">Search</label>
        <input type="text" id="fltSearch" class="hr-input" placeholder="Name or staff number" value="<%= HttpUtility.HtmlAttributeEncode(SearchValue) %>" />
    </div>
    <div class="hr-filter" style="flex-basis:110px;min-width:100px">
        <label for="fltYear">Year</label>
        <select id="fltYear" class="hr-select"><asp:Literal ID="litYearOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="fltDept">Department</label>
        <select id="fltDept" class="hr-select"><asp:Literal ID="litDeptOptions" runat="server" /></select>
    </div>
    <div class="hr-filters__actions">
        <button type="button" class="hr-btn hr-btn--primary" onclick="applyFilters()">Apply</button>
        <a class="hr-btn hr-btn--secondary" href="HRLeaveManagement.aspx">Reset</a>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Balances for <asp:Literal ID="litYear" runat="server" /> <span class="hr-card__meta"><asp:Literal ID="litBalanceCount" runat="server" /></span></div>
        <asp:Literal ID="litExport" runat="server" />
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead><tr><th>Employee</th><th>Department</th><th class="hr-num">Allocated</th><th class="hr-num">Taken</th><th class="hr-num">Remaining</th><th>Status</th><th></th></tr></thead>
            <tbody><asp:Literal ID="litBalances" runat="server" /></tbody>
        </table>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Leave taken in <asp:Literal ID="litYear2" runat="server" /></div>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead><tr><th>Employee</th><th>Department</th><th>First day</th><th>Last day</th><th class="hr-num">Days</th><th></th></tr></thead>
            <tbody><asp:Literal ID="litTaken" runat="server" /></tbody>
        </table>
    </div>
</div>

<!-- Allocate leave -->
<div class="hr-modal" id="mdlAllocate">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Allocate leave</span><button type="button" class="hr-modal__close" onclick="closeModal('mdlAllocate')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field hr-full lm-pick">
                    <label class="hr-label" for="allocEmpFilter">Employee <span class="hr-req">*</span></label>
                    <input type="text" id="allocEmpFilter" class="hr-input" placeholder="Type to filter the list" autocomplete="off" />
                    <select id="allocEmp" class="hr-select"><asp:Literal ID="litEmpOptions" runat="server" /></select>
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="allocYear">Leave year <span class="hr-req">*</span></label>
                    <input type="number" id="allocYear" class="hr-input" min="2000" max="2100" value="<%= CurrentYear %>" />
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="allocDays">Days <span class="hr-req">*</span></label>
                    <input type="number" id="allocDays" class="hr-input" min="0" max="365" value="30" />
                </div>
            </div>
            <div class="hr-error lm-info" id="allocMsg"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('mdlAllocate')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnAllocate" onclick="submitAllocate()">Allocate</button>
        </div>
    </div>
</div>

<!-- Record leave taken -->
<div class="hr-modal" id="mdlRecord">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Record leave taken</span><button type="button" class="hr-modal__close" onclick="closeModal('mdlRecord')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field hr-full lm-pick">
                    <label class="hr-label" for="recEmpFilter">Employee <span class="hr-req">*</span></label>
                    <input type="text" id="recEmpFilter" class="hr-input" placeholder="Type to filter the list" autocomplete="off" />
                    <select id="recEmp" class="hr-select"></select>
                    <div class="hr-hint lm-info" id="recBalance"></div>
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="recStart">First day <span class="hr-req">*</span></label>
                    <input type="date" id="recStart" class="hr-input" />
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="recEnd">Last day <span class="hr-req">*</span></label>
                    <input type="date" id="recEnd" class="hr-input" />
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="recDays">Number of days <span class="hr-req">*</span></label>
                    <input type="number" id="recDays" class="hr-input" min="1" />
                </div>
            </div>
            <div class="hr-error lm-info" id="recMsg"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('mdlRecord')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnRecord" onclick="submitRecord()">Record leave</button>
        </div>
    </div>
</div>

<!-- Edit allocated days -->
<div class="hr-modal" id="mdlEdit">
    <div class="hr-modal__box" style="width:400px">
        <div class="hr-modal__head"><span>Edit allocated days</span><button type="button" class="hr-modal__close" onclick="closeModal('mdlEdit')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-field">
                <label class="hr-label" for="editDays" id="editLabel">Allocated days</label>
                <input type="number" id="editDays" class="hr-input" min="0" max="365" />
            </div>
            <div class="hr-error lm-info" id="editMsg"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('mdlEdit')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnEdit" onclick="submitEdit()">Save</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="hrToast" role="status" aria-live="polite"></div>
<asp:Literal ID="litBalanceJson" runat="server" />

</div>

<script type="text/javascript">
var LV_YEAR = <%= CurrentYear %>;
var editId = 0;

function el(id){ return document.getElementById(id); }
function openModal(id){ el(id).classList.add('is-open'); }
function closeModal(id){ el(id).classList.remove('is-open'); }
function showToast(msg, isErr){
    var t = el('hrToast');
    t.textContent = msg;
    t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function(){ t.className = 'hr-toast'; }, 3500);
}

/* Filters */
function applyFilters(){
    var p = ['year=' + encodeURIComponent(el('fltYear').value)];
    if(el('fltDept').value) p.push('dept=' + encodeURIComponent(el('fltDept').value));
    var q = el('fltSearch').value.trim();
    if(q) p.push('q=' + encodeURIComponent(q));
    location.href = 'HRLeaveManagement.aspx?' + p.join('&');
}
el('fltSearch').addEventListener('keydown', function(e){ if(e.key === 'Enter'){ e.preventDefault(); applyFilters(); } });
el('fltYear').addEventListener('change', applyFilters);
el('fltDept').addEventListener('change', applyFilters);

/* Employee picker: a filter box above a plain select, no postback */
var EMP_OPTS = [];
(function(){
    var s = el('allocEmp');
    for(var i = 0; i < s.options.length; i++) EMP_OPTS.push([s.options[i].value, s.options[i].text]);
    el('recEmp').innerHTML = s.innerHTML;
})();
function bindPicker(filterId, selectId, onChange){
    var f = el(filterId), s = el(selectId);
    f.addEventListener('input', function(){
        var q = f.value.trim().toLowerCase(), keep = s.value, html = '', first = '';
        for(var i = 0; i < EMP_OPTS.length; i++){
            var o = EMP_OPTS[i];
            if(o[0] && q && o[1].toLowerCase().indexOf(q) < 0) continue;
            if(o[0] && !first) first = o[0];
            html += '<option value="' + o[0] + '">' + o[1].replace(/&/g,'&amp;').replace(/</g,'&lt;') + '</option>';
        }
        s.innerHTML = html;
        s.value = keep;
        if(!s.value && q && first) s.value = first;
        if(onChange) onChange();
    });
    s.addEventListener('change', function(){ if(onChange) onChange(); });
}
function resetPicker(filterId, selectId){
    el(filterId).value = '';
    var html = '';
    for(var i = 0; i < EMP_OPTS.length; i++) html += '<option value="' + EMP_OPTS[i][0] + '">' + EMP_OPTS[i][1].replace(/&/g,'&amp;').replace(/</g,'&lt;') + '</option>';
    el(selectId).innerHTML = html;
    el(selectId).value = '';
}
bindPicker('allocEmpFilter', 'allocEmp', null);
bindPicker('recEmpFilter', 'recEmp', showBalance);

function showBalance(){
    var id = el('recEmp').value, box = el('recBalance');
    if(!id){ box.textContent = ''; return; }
    var b = (window.LV_BAL || {})[id];
    if(!b){ box.textContent = 'No leave allocated for ' + LV_YEAR + '. Allocate leave first.'; return; }
    box.textContent = LV_YEAR + ': ' + b[0] + ' days allocated, ' + b[1] + ' taken, ' + (b[0] - b[1]) + ' remaining.';
}

/* Server calls */
function post(action, data, btn, msgId, done){
    var body = [];
    for(var k in data) if(data.hasOwnProperty(k)) body.push(encodeURIComponent(k) + '=' + encodeURIComponent(data[k]));
    if(btn) btn.disabled = true;
    if(msgId) el(msgId).textContent = '';
    fetch('HRLeaveManagement.aspx?action=' + action, {
        method: 'POST', credentials: 'same-origin',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: body.join('&')
    })
    .then(function(r){ return r.json(); })
    .then(function(d){
        if(btn) btn.disabled = false;
        if(d.ok){ done(d); }
        else if(msgId){ el(msgId).textContent = d.message || 'The change could not be saved.'; }
        else showToast(d.message || 'The change could not be saved.', true);
    })
    .catch(function(){
        if(btn) btn.disabled = false;
        var m = 'The change could not be saved. Check your connection and try again.';
        if(msgId) el(msgId).textContent = m; else showToast(m, true);
    });
}
function reloadWith(msg){ showToast(msg); setTimeout(function(){ location.reload(); }, 800); }

function openAllocate(){
    resetPicker('allocEmpFilter', 'allocEmp');
    el('allocYear').value = el('fltYear').value || LV_YEAR;
    el('allocDays').value = 30;
    el('allocMsg').textContent = '';
    openModal('mdlAllocate');
    el('allocEmpFilter').focus();
}
function submitAllocate(){
    if(!el('allocEmp').value){ el('allocMsg').textContent = 'Choose an employee.'; return; }
    post('allocate', { emp_id: el('allocEmp').value, year: el('allocYear').value, days: el('allocDays').value },
        el('btnAllocate'), 'allocMsg', function(d){ closeModal('mdlAllocate'); reloadWith(d.message); });
}

function openRecord(){
    resetPicker('recEmpFilter', 'recEmp');
    el('recStart').value = ''; el('recEnd').value = ''; el('recDays').value = '';
    el('recBalance').textContent = ''; el('recMsg').textContent = '';
    openModal('mdlRecord');
    el('recEmpFilter').focus();
}
function calcDays(){
    var s = el('recStart').value, e = el('recEnd').value;
    if(s && e && !el('recDays').dataset.touched){
        var n = Math.round((new Date(e) - new Date(s)) / 864e5) + 1;
        if(n > 0) el('recDays').value = n;
    }
}
el('recStart').addEventListener('change', calcDays);
el('recEnd').addEventListener('change', calcDays);
el('recDays').addEventListener('input', function(){ this.dataset.touched = '1'; });
function submitRecord(){
    if(!el('recEmp').value){ el('recMsg').textContent = 'Choose an employee.'; return; }
    post('record', { emp_id: el('recEmp').value, start: el('recStart').value, end: el('recEnd').value, days: el('recDays').value },
        el('btnRecord'), 'recMsg', function(d){ closeModal('mdlRecord'); reloadWith(d.message); });
}

function openEditDays(id, name, days){
    editId = id;
    el('editLabel').textContent = 'Allocated days for ' + name;
    el('editDays').value = days;
    el('editMsg').textContent = '';
    openModal('mdlEdit');
}
function submitEdit(){
    post('update_days', { id: editId, days: el('editDays').value }, el('btnEdit'), 'editMsg',
        function(d){ closeModal('mdlEdit'); reloadWith(d.message); });
}
function deleteAlloc(id, name){
    hrConfirm({ title: 'Delete leave allocation', message: 'Delete the leave allocation for ' + name + '? Leave recorded against it is deleted too.', ok: 'Delete', danger: true }, function(){
        post('delete_alloc', { id: id }, null, null, function(d){ reloadWith(d.message); });
    });
}
function deleteRecord(id, name){
    hrConfirm({ title: 'Delete leave record', message: 'Delete this leave record for ' + name + '?', ok: 'Delete', danger: true }, function(){
        post('delete_record', { id: id }, null, null, function(d){ reloadWith(d.message); });
    });
}

document.addEventListener('keydown', function(e){
    if(e.key === 'Escape'){ closeModal('mdlAllocate'); closeModal('mdlRecord'); closeModal('mdlEdit'); }
    if(e.key === 'Enter' && e.target && e.target.tagName === 'INPUT' && e.target.closest && e.target.closest('.hr-modal')) e.preventDefault();
});
</script>
</asp:Content>
