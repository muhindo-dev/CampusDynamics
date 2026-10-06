<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="DisciplinaryCase.aspx.cs" Inherits="COOPERP_NewScreens_DisciplinaryCase" Title="Disciplinary Case - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/DcHeader.ascx" TagName="DcHeader" TagPrefix="dc" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/dc.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <dc:DcHeader ID="hdr" runat="server" Title="Case file" Sub="The full record of one disciplinary case" Icon="folder" Active="records" />
    <div id="cMain"><div class="fa-card"><div class="fa-loading">Loading the case</div></div></div>
</div>
<script>window.DC_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc-case.js") %>?v=1"></script>
</asp:Content>
