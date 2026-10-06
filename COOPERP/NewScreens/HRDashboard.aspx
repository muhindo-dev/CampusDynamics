<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master"
    AutoEventWireup="true" CodeFile="HRDashboard.aspx.cs"
    Inherits="COOPERP_NewScreens_HRDashboard"
    Title="Human resources - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Human resources</div>
            <div class="hr-header__sub">Staff, contracts and leave at a glance</div>
        </div>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab hr-tab--active" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a><a class="hr-tab" href="ProfileChangeRequests.aspx">Profile requests</a></div>

<asp:Literal ID="litError" runat="server" />

<div class="hr-kpis">
    <a class="hr-kpi" href="HREmployees.aspx?status=ACTIVE">
        <div class="hr-kpi__label">Active staff</div>
        <div class="hr-kpi__value"><asp:Literal ID="litActiveStaff" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub">With a valid contract</div>
    </a>
    <a class="hr-kpi" href="HRContracts.aspx?status=ENDING">
        <div class="hr-kpi__label">Contracts ending in 90 days</div>
        <div class="hr-kpi__value"><asp:Literal ID="litEnding" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub">Current contracts</div>
    </a>
    <a class="hr-kpi" href="ContractRenewals.aspx">
        <div class="hr-kpi__label">Renewals with HR</div>
        <div class="hr-kpi__value"><asp:Literal ID="litRenewalsHr" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub">Awaiting HR verification</div>
    </a>
    <a class="hr-kpi" href="LeaveApplications.aspx?status=PENDING">
        <div class="hr-kpi__label">Leave awaiting action</div>
        <div class="hr-kpi__value"><asp:Literal ID="litLeavePending" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub"><asp:Literal ID="litLeaveWithHr" runat="server" Text="0" /> with HR</div>
    </a>
</div>

<div class="hr-grid-2">
    <div class="hr-card">
        <div class="hr-card__head">
            <div class="hr-card__title">Needs action</div>
        </div>
        <asp:Literal ID="litNeedsAction" runat="server" />
    </div>

    <div class="hr-card">
        <div class="hr-card__head">
            <div class="hr-card__title">Contracts ending soon</div>
            <a class="hr-btn hr-btn--link" href="HRContracts.aspx?status=ENDING">View all</a>
        </div>
        <div class="hr-table-wrap">
            <asp:Literal ID="litEndingSoon" runat="server" />
        </div>
    </div>
</div>

</div>
</asp:Content>
