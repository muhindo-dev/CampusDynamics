<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AccountsAdjustments.aspx.cs" Inherits="COOPERP_NewScreens_AccountsAdjustments" Title="Adjusting Entries - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/GlHeader.ascx" TagName="GlHeader" TagPrefix="gl" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/gl.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <gl:GlHeader ID="hdr" runat="server" Title="Adjusting entries" Sub="Corrections are new entries with a reason, approved by a second person. Nothing in the ledger is edited or deleted." Icon="edit" Active="adjust" />
    <div class="fa-notice" id="jNote"></div>
    <div class="fa-row" style="margin-bottom:12px"><div class="fa-subtabs" id="jTabs" style="margin:0"></div><span class="fa-spacer"></span><button type="button" class="fa-btn fa-btn--primary" id="jNew">New adjusting entry</button></div>
    <div class="fa-card"><div id="jList"><div class="fa-loading">Loading</div></div></div>
</div>
<script>window.GL_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl.js") %>?v=3"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl-adjust.js") %>?v=1"></script>
</asp:Content>
