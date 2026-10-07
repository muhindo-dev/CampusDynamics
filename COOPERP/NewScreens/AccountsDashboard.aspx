<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AccountsDashboard.aspx.cs" Inherits="COOPERP_NewScreens_AccountsDashboard" Title="Accounts Dashboard - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/GlHeader.ascx" TagName="GlHeader" TagPrefix="gl" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/gl.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <gl:GlHeader ID="hdr" runat="server" Title="Accounts dashboard" Sub="The general ledger at a glance. Every figure opens the report it comes from." Icon="chart" Active="dashboard" />
    <div class="fa-error" id="dErr"></div>
    <div class="fa-notice" id="dNote"></div>
    <div class="fa-kpis fa-kpis--4" id="dTiles"></div>
    <div class="fa-grid-main">
        <div style="min-width:0">
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Income and expenditure by month</div><span class="fa-hint" id="dMonthsSub"></span></div><div class="fa-card__body" id="dMonths"></div></div>
            <div class="fa-grid-2">
                <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Spending by sub-category</div></div><div class="fa-card__body" id="dSpend"></div></div>
                <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Largest expense accounts</div></div><div id="dTop"></div></div>
            </div>
        </div>
        <div style="min-width:0">
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Finance Warnings</div><a class="gl-link" href="AccountsWarnings.aspx">Open</a></div><div class="fa-card__body" id="dWarn"></div></div>
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Bank and cash balances</div></div><div id="dBanks"></div></div>
        </div>
    </div>
</div>
<script>window.GL_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl.js") %>?v=3"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl-dashboard.js") %>?v=1"></script>
</asp:Content>
