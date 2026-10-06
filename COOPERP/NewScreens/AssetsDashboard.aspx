<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AssetsDashboard.aspx.cs" Inherits="COOPERP_NewScreens_AssetsDashboard" Title="Assets Dashboard - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/FaHeader.ascx" TagName="FaHeader" TagPrefix="fa" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <fa:FaHeader ID="hdr" runat="server" Title="Fixed assets" Sub="What the University owns, where it is and what it is worth" Icon="chart" Active="dashboard" />

    <div class="fa-filters">
        <div class="fa-filter"><label for="fCampus">Campus</label><select id="fCampus" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fCat">Category</label><select id="fCat" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fSub">Sub-category</label><select id="fSub" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fFrom">From</label><input type="date" id="fFrom" class="fa-input" /></div>
        <div class="fa-filter"><label for="fTo">To (values as at)</label><input type="date" id="fTo" class="fa-input" /></div>
        <div class="fa-filters__actions"><button type="button" class="fa-btn fa-btn--primary" id="btnApply">Apply</button><button type="button" class="fa-btn fa-btn--secondary" id="btnReset">Reset</button></div>
    </div>
    <div id="dNote"></div>

    <div class="fa-kpis fa-kpis--4" id="dTiles"><div class="fa-kpi"><div class="fa-kpi__label">Loading</div><div class="fa-kpi__value">&nbsp;</div></div></div>

    <div class="fa-grid-2">
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Book value by category</div><span class="fa-card__meta">UGX, click a bar to open the assets</span></div>
            <div class="fa-card__body"><div class="fa-chart fa-chart--tall"><canvas id="cCat"></canvas></div></div></div>
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Assets by status</div></div>
            <div class="fa-card__body"><div class="fa-chart fa-chart--tall"><canvas id="cStatus"></canvas></div></div></div>
    </div>
    <div class="fa-grid-2">
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Acquisitions and book value by financial year</div><span class="fa-card__meta">UGX</span></div>
            <div class="fa-card__body"><div class="fa-chart"><canvas id="cYear"></canvas></div></div></div>
        <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Assets by campus</div></div>
            <div class="fa-card__body"><div class="fa-chart"><canvas id="cCampus"></canvas></div></div></div>
    </div>

    <div class="fa-card">
        <div class="fa-card__head"><div class="fa-card__title">Needs attention</div><span class="fa-card__meta">As of today</span></div>
        <div class="fa-card__body"><div class="fa-actions" id="dActions"></div></div>
    </div>

    <div class="fa-card">
        <div class="fa-card__head"><div class="fa-card__title">Top ten assets by book value</div></div>
        <div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>#</th><th>Asset no</th><th>Name</th><th>Category</th><th class="fa-num">Book value (UGX)</th></tr></thead><tbody id="dTop"></tbody></table></div>
    </div>
</div>

<script>window.FA_BOOT = <%= BootJson %>;</script>
<script src="https://cdn.jsdelivr.net/npm/chart.js@3.9.1/dist/chart.min.js"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa-dashboard.js") %>?v=2"></script>
</asp:Content>
