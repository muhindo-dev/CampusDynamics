<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AppraisalDashboard.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalDashboard" Title="Appraisal overview" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg></div>
        <div>
            <div class="hr-header__title">Performance appraisal</div>
            <div class="hr-header__sub">Progress and the HR review queue</div>
        </div>
    </div>
    <div class="hr-header__actions">
        
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab hr-tab--active" href="AppraisalDashboard.aspx">Overview</a><a class="hr-tab" href="AppraisalSessions.aspx">Sessions</a><a class="hr-tab" href="AppraisalView.aspx">Appraisals</a><a class="hr-tab" href="AppraisalReports.aspx">Reports</a><a class="hr-tab" href="CompetencyTemplates.aspx">Competencies</a><a class="hr-tab" href="ExpectedStandards.aspx">Expected standards</a></div>

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="selSession">Session</label>
        <select id="selSession" class="hr-select" onchange="window.location.href='AppraisalDashboard.aspx'+(this.value&&this.value!=='0'?'?sid='+this.value:'');">
            <asp:Literal ID="litSessionOptions" runat="server" />
        </select>
    </div>
</div>

<asp:Literal ID="litUnassignedBanner" runat="server" />

<div class="hr-kpis">
    <div class="hr-kpi">
        <div class="hr-kpi__label">Total</div>
        <div class="hr-kpi__value"><asp:Literal ID="litKpiTotal" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub">Appraisal records</div>
    </div>
    <div class="hr-kpi">
        <div class="hr-kpi__label">Not started</div>
        <div class="hr-kpi__value"><asp:Literal ID="litKpiNotStarted" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub"><asp:Literal ID="litKpiEmpStage" runat="server" /></div>
    </div>
    <div class="hr-kpi">
        <div class="hr-kpi__label">With supervisor</div>
        <div class="hr-kpi__value"><asp:Literal ID="litKpiSupStage" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub">Submitted or being rated</div>
    </div>
    <a class="hr-kpi" href="<%= ViewLink("COMPLETED") %>">
        <div class="hr-kpi__label">Awaiting HR</div>
        <div class="hr-kpi__value"><asp:Literal ID="litKpiNeedsHr" runat="server" Text="0" /></div>
        <div class="hr-kpi__sub"><asp:Literal ID="litKpiHrReviewed" runat="server" /></div>
    </a>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Awaiting HR review</div>
        <a class="hr-btn hr-btn--secondary hr-btn--sm" href="<%= ViewLink("COMPLETED") %>">Open queue</a>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th>Employee</th>
                    <th>Department</th>
                    <th>Session</th>
                    <th>Supervisor</th>
                    <th class="hr-num">Score %</th>
                    <th>Classification</th>
                    <th class="hr-num">Days waiting</th>
                    <th></th>
                </tr>
            </thead>
            <tbody><asp:Literal ID="litActionRequired" runat="server" /></tbody>
        </table>
    </div>
    <asp:Literal ID="litActionFoot" runat="server" />
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Past deadline</div>
        <div class="hr-card__meta">Sessions with appraisals not yet completed by the supervisor</div>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th>Session</th>
                    <th>Deadline</th>
                    <th class="hr-num">Days overdue</th>
                    <th class="hr-num">Outstanding</th>
                    <th></th>
                </tr>
            </thead>
            <tbody><asp:Literal ID="litAlerts" runat="server" /></tbody>
        </table>
    </div>
</div>

</div>
</asp:Content>
