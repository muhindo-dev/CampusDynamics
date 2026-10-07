<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AccountsWarnings.aspx.cs" Inherits="COOPERP_NewScreens_AccountsWarnings" Title="Finance Warnings - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/GlHeader.ascx" TagName="GlHeader" TagPrefix="gl" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/gl.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <gl:GlHeader ID="hdr" runat="server" Title="Finance Warnings" Sub="Problems found in the ledger by read-only checks, with their cause and what to do" Icon="alert" Active="warnings" />
    <div class="fa-error" id="wErr"></div>
    <div class="fa-kpis" id="wKpis"></div>
    <div class="fa-grid-main">
        <div style="min-width:0">
            <div class="fa-subtabs" id="wTabs"></div>
            <div class="fa-filters" id="wFilters" style="padding:8px 12px">
                <div class="fa-filter fa-filter--grow"><label for="wQ">Search</label><input type="search" class="fa-input" id="wQ" placeholder="Title, rule or account"/></div>
                <div class="fa-filter"><label for="wSev">Severity</label><select class="fa-select" id="wSev"><option value="">All</option><option value="CRITICAL">Critical</option><option value="HIGH">High</option><option value="MEDIUM">Medium</option><option value="INFO">Information</option></select></div>
                <div class="fa-filter"><label for="wMine">Assigned</label><select class="fa-select" id="wMine"><option value="">Anyone</option><option value="me">To me</option><option value="none">Nobody</option></select></div>
            </div>
            <div id="wList"><div class="fa-loading">Loading</div></div>
        </div>
        <div style="min-width:0">
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Health score</div></div><div class="fa-card__body" id="wHealth"></div></div>
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Where the points are lost</div></div><div id="wLost"></div></div>
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">The rules</div></div><div class="fa-card__body" id="wRules" style="max-height:420px;overflow:auto;padding:8px 14px"></div></div>
        </div>
    </div>
</div>
<script>window.GL_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl.js") %>?v=3"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl-warnings.js") %>?v=2"></script>
</asp:Content>
