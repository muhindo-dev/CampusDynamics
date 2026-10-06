<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="DisciplinarySettings.aspx.cs" Inherits="COOPERP_NewScreens_DisciplinarySettings" Title="Case Types and Sanctions - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/DcHeader.ascx" TagName="DcHeader" TagPrefix="dc" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/dc.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <dc:DcHeader ID="hdr" runat="server" Title="Case types and sanctions" Sub="What can be reported, what can be decided, the letters and the committee" Icon="sliders" Active="settings" />
    <div class="fa-subtabs" id="sTabs"></div>
    <div id="sBody"><div class="fa-card"><div class="fa-loading">Loading</div></div></div>
</div>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc-settings.js") %>?v=1"></script>
</asp:Content>
