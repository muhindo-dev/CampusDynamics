<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="LeaveApplications.aspx.cs" Inherits="COOPERP_NewScreens_LeaveApplications" Title="Leave applications - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.lv-emp { display: flex; align-items: center; gap: 9px; }
.lv-ini { width: 28px; height: 28px; flex: 0 0 auto; display: flex; align-items: center; justify-content: center; background: var(--hr-surface); border: 1px solid var(--hr-border); color: var(--hr-text-2); font-size: 10px; font-weight: 700; }
.lv-name { font-weight: 600; }
a.hr-kpi.lv-kpi--on { border-color: var(--hr-navy); }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Leave applications</div>
            <div class="hr-header__sub"><asp:Literal ID="litSubtitle" runat="server" /></div>
        </div>
    </div>
    <div class="hr-header__actions"><asp:Literal ID="litNewBtn" runat="server" /></div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab hr-tab--active" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a><a class="hr-tab" href="ProfileChangeRequests.aspx">Profile requests</a></div>

<asp:Literal ID="litStats" runat="server" />

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="fltSearch">Search</label>
        <input type="text" id="fltSearch" class="hr-input" placeholder="Name, staff number or department" value="<%= HttpUtility.HtmlAttributeEncode(txtSearchValue) %>" />
    </div>
    <div class="hr-filter">
        <label for="fltStatus">Status</label>
        <select id="fltStatus" class="hr-select"><asp:Literal ID="litStatusOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="fltType">Leave type</label>
        <select id="fltType" class="hr-select"><asp:Literal ID="litTypeOptions" runat="server" /></select>
    </div>
    <div class="hr-filters__actions">
        <button type="button" class="hr-btn hr-btn--primary" onclick="applyFilters()">Apply</button>
        <a class="hr-btn hr-btn--secondary" href="LeaveApplications.aspx">Reset</a>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Applications <span class="hr-card__meta"><asp:Literal ID="litCount" runat="server" /></span></div>
        <div class="hr-row"><asp:Literal ID="litExportLinks" runat="server" /></div>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th>Employee</th>
                    <th>Leave type</th>
                    <th>First day</th>
                    <th>Last day</th>
                    <th class="hr-num">Days</th>
                    <th>Status</th>
                    <th>Submitted</th>
                    <th></th>
                </tr>
            </thead>
            <tbody>
                <asp:Literal ID="litRows" runat="server" />
            </tbody>
        </table>
    </div>
    <div class="hr-card__foot">
        <span></span>
        <asp:Literal ID="litPager" runat="server" />
    </div>
</div>

<div class="hr-toast" id="hrToast" role="status" aria-live="polite"></div>

</div>

<script type="text/javascript">
function applyFilters(){
    var p = [];
    var q = document.getElementById('fltSearch').value.trim();
    var s = document.getElementById('fltStatus').value;
    var t = document.getElementById('fltType').value;
    if(q) p.push('q=' + encodeURIComponent(q));
    if(s) p.push('status=' + encodeURIComponent(s));
    if(t) p.push('type=' + encodeURIComponent(t));
    location.href = 'LeaveApplications.aspx' + (p.length ? '?' + p.join('&') : '');
}
document.getElementById('fltSearch').addEventListener('keydown', function(e){
    if(e.key === 'Enter'){ e.preventDefault(); applyFilters(); }
});
document.getElementById('fltStatus').addEventListener('change', applyFilters);
document.getElementById('fltType').addEventListener('change', applyFilters);

function showToast(msg, isErr){
    var t = document.getElementById('hrToast');
    t.textContent = msg;
    t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function(){ t.className = 'hr-toast'; }, 3500);
}

function cancelApp(id){
    if(!confirm('Cancel this leave application? This cannot be undone.')) return;
    fetch('LeaveApplicationForm.aspx?ajax=cancel', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: 'id=' + encodeURIComponent(id)
    })
    .then(function(r){ return r.json(); })
    .then(function(d){
        if(d.ok){ showToast('Application cancelled.'); setTimeout(function(){ location.reload(); }, 900); }
        else showToast(d.error || 'The application could not be cancelled.', true);
    })
    .catch(function(){ showToast('The application could not be cancelled. Check your connection and try again.', true); });
}
</script>
</asp:Content>
