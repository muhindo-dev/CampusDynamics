<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AccountsReports.aspx.cs" Inherits="COOPERP_NewScreens_AccountsReports" Title="Accounts Reports - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/GlHeader.ascx" TagName="GlHeader" TagPrefix="gl" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/gl.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <gl:GlHeader ID="hdr" runat="server" Title="Accounts reports" Sub="Statements, ledgers and control reports from the general ledger, read only" Icon="file" Active="reports" />
    <div class="fa-grid-side">
        <div class="fa-card" style="align-self:start"><div class="fa-card__head"><div class="fa-card__title">Reports</div><button type="button" class="fa-btn fa-btn--link" id="rRefresh" title="Read the ledger again instead of the figures kept for five minutes">Refresh figures</button></div><div id="rList"></div></div>
        <div style="min-width:0">
            <div class="fa-card">
                <div class="fa-card__head">
                    <div><div class="fa-card__title" id="rTitle">Choose a report</div><div class="fa-hint" id="rDesc"></div></div>
                    <div class="fa-row"><select class="fa-select" id="rSaved" style="width:auto;min-width:170px"></select><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="rSave">Save these filters</button></div>
                </div>
                <div class="fa-card__body"><div class="fa-form fa-form--3" id="rParams"></div><div class="fa-error" id="rErr" style="margin-top:8px"></div></div>
                <div class="fa-card__foot" style="justify-content:flex-end;gap:8px">
                    <span class="fa-muted" id="rLast" style="margin-right:auto"></span>
                    <button type="button" class="fa-btn fa-btn--secondary" id="rExport" disabled>Export</button>
                    <button type="button" class="fa-btn fa-btn--primary" id="rRun">Run report</button>
                </div>
            </div>
            <div id="rChecks"></div>
            <div class="fa-card" id="rOutCard" style="display:none">
                <div class="fa-card__head"><div><div class="fa-card__title" id="rOutTitle"></div><div class="fa-hint" id="rOutSub"></div></div></div>
                <div id="rOut"></div>
            </div>
        </div>
    </div>
</div>
<script>window.GL_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/gl-reports.js") %>?v=1"></script>
</asp:Content>
