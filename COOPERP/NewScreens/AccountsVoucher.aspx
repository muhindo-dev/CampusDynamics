<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AccountsVoucher.aspx.cs" Inherits="COOPERP_NewScreens_AccountsVoucher" Title="Voucher - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/GlHeader.ascx" TagName="GlHeader" TagPrefix="gl" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/gl.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <gl:GlHeader ID="hdr" runat="server" Title="Voucher" Sub="Every line under one voucher number, and whether it balances" Icon="hash" Active="reports" />
    <div class="fa-filters">
        <div class="fa-filter"><label for="vNo">Voucher number</label><input type="number" class="fa-input" id="vNo" min="1"/></div>
        <div class="fa-filters__actions"><button type="button" class="fa-btn fa-btn--primary" id="vGo">Open</button></div>
    </div>
    <div class="fa-error" id="vErr"></div>
    <div id="vMain"></div>
</div>
<script>window.GL_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl.js") %>?v=3"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl-voucher.js") %>?v=1"></script>
</asp:Content>
