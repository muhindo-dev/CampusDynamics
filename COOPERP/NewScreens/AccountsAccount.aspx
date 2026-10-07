<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AccountsAccount.aspx.cs" Inherits="COOPERP_NewScreens_AccountsAccount" Title="Account Card - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/GlHeader.ascx" TagName="GlHeader" TagPrefix="gl" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/gl.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <gl:GlHeader ID="hdr" runat="server" Title="Account card" Sub="Balance, movement and every line of one account, read only" Icon="hash" Active="reports" />
    <div class="fa-error" id="aErr"></div>
    <div id="aMain" style="display:none">
        <div class="fa-card">
            <div class="fa-card__head">
                <div style="min-width:0"><div class="fa-card__title" id="aTitle"></div><div class="fa-hint" id="aSub"></div></div>
                <div class="fa-row">
                    <select class="fa-select" id="aYear" style="width:auto"></select>
                    <input type="date" class="fa-input" id="aFrom" style="width:150px"/>
                    <input type="date" class="fa-input" id="aTo" style="width:150px"/>
                    <button type="button" class="fa-btn fa-btn--primary fa-btn--sm" id="aGo">Show</button>
                </div>
            </div>
            <div class="fa-card__body" style="padding-bottom:4px"><dl class="fa-dl fa-dl--4" id="aFacts"></dl></div>
        </div>
        <div class="gl-summary" id="aSummary"></div>
        <div id="aMapping"></div>
        <div id="aWarnings"></div>
        <div class="fa-subtabs" id="aTabs"></div>
        <div class="fa-card" id="aPanel"><div id="aPanelBody"></div></div>
    </div>
</div>
<script>window.GL_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl-account.js") %>?v=1"></script>
</asp:Content>
