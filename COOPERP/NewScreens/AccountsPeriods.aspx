<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AccountsPeriods.aspx.cs" Inherits="COOPERP_NewScreens_AccountsPeriods" Title="Periods and Close - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/GlHeader.ascx" TagName="GlHeader" TagPrefix="gl" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/gl.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <gl:GlHeader ID="hdr" runat="server" Title="Periods and close" Sub="Review and sign off each month and year against an automatic checklist" Icon="calendar" Active="periods" />
    <div class="fa-error" id="pErr"></div>
    <div class="fa-notice">A sign-off records who reviewed a period, what the checklist showed and their note. It does not stop postings: anything posted into a signed-off period afterwards is flagged on Finance Warnings and needs a reason on an adjusting entry.</div>
    <div class="fa-filters"><div class="fa-filter fa-filter--grow"><label for="pYear">Financial year</label><select class="fa-select" id="pYear"></select></div></div>
    <div class="fa-grid-main">
        <div class="fa-card" style="min-width:0"><div class="fa-card__head"><div class="fa-card__title">Months</div><span class="fa-hint">Choose a month or the year to see its checklist</span></div><div id="pList"><div class="fa-loading">Loading</div></div></div>
        <div style="min-width:0" id="pPanel"><div class="fa-card"><div class="fa-empty">Choose a period.</div></div></div>
    </div>
</div>
<script>window.GL_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl.js") %>?v=3"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl-periods.js") %>?v=2"></script>
</asp:Content>
