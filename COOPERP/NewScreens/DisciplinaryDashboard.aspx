<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="DisciplinaryDashboard.aspx.cs" Inherits="COOPERP_NewScreens_DisciplinaryDashboard" Title="Disciplinary Dashboard - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/DcHeader.ascx" TagName="DcHeader" TagPrefix="dc" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/dc.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <dc:DcHeader ID="hdr" runat="server" Title="Student discipline" Sub="Cases, hearings and sanctions across the University" Icon="shield" Active="dashboard" />

    <div class="fa-filters">
        <div class="fa-filter"><label for="fCampus">Campus</label><select id="fCampus" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fFac">Faculty</label><select id="fFac" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fYear">Academic year</label><select id="fYear" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fType">Case type</label><select id="fType" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fFrom">Reported from</label><input type="date" id="fFrom" class="fa-input" /></div>
        <div class="fa-filter"><label for="fTo">To</label><input type="date" id="fTo" class="fa-input" /></div>
        <div class="fa-filters__actions"><button type="button" class="fa-btn fa-btn--primary" id="btnApply">Apply</button><button type="button" class="fa-btn fa-btn--secondary" id="btnReset">Reset</button></div>
    </div>

    <div class="fa-kpis" id="dTiles"><div class="fa-kpi"><div class="fa-kpi__label">Loading</div><div class="fa-kpi__value">&nbsp;</div></div></div>

    <div class="fa-card">
        <div class="fa-card__head"><div class="fa-card__title">Needs attention</div><span class="fa-card__meta">As of today</span></div>
        <div class="fa-card__body"><div class="fa-kpis" id="dAttention" style="margin:0"></div></div>
    </div>

    <div class="fa-grid-2">
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Cases by type</div><span class="fa-card__meta">Reported in the period; click a bar to open them</span></div>
            <div class="fa-card__body"><div class="fa-chart fa-chart--tall"><canvas id="cType"></canvas></div></div></div>
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Cases by status</div><span class="fa-card__meta">All cases</span></div>
            <div class="fa-card__body"><div class="fa-chart fa-chart--tall"><canvas id="cStatus"></canvas></div></div></div>
    </div>
    <div class="fa-grid-2">
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">New cases per month</div></div>
            <div class="fa-card__body"><div class="fa-chart"><canvas id="cMonth"></canvas></div></div></div>
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Cases per academic year</div></div>
            <div class="fa-card__body"><div class="fa-chart"><canvas id="cYear"></canvas></div></div></div>
    </div>
    <div class="fa-grid-3">
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">By severity</div></div><div class="fa-card__body"><div class="fa-chart"><canvas id="cSev"></canvas></div></div></div>
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">By campus</div></div><div class="fa-card__body"><div class="fa-chart"><canvas id="cCampus"></canvas></div></div></div>
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">By faculty</div></div><div class="fa-card__body"><div class="fa-chart"><canvas id="cFac"></canvas></div></div></div>
    </div>
    <div class="fa-grid-2">
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Students under sanction now</div><span class="fa-card__meta" id="dEffects"></span></div>
            <div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Student</th><th>Sanction</th><th>From</th><th>To</th></tr></thead><tbody id="dSanctions"></tbody></table></div></div>
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Hearings coming up</div></div>
            <div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>When</th><th>Student</th><th>Venue</th><th>Status</th></tr></thead><tbody id="dHearings"></tbody></table></div></div>
    </div>
</div>
<script>window.DC_BOOT = <%= BootJson %>;</script>
<script src="https://cdn.jsdelivr.net/npm/chart.js@3.9.1/dist/chart.min.js"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc-dashboard.js") %>?v=1"></script>
</asp:Content>
