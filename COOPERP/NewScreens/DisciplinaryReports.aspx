<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="DisciplinaryReports.aspx.cs" Inherits="COOPERP_NewScreens_DisciplinaryReports" Title="Disciplinary Reports - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/DcHeader.ascx" TagName="DcHeader" TagPrefix="dc" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/dc.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <dc:DcHeader ID="hdr" runat="server" Title="Disciplinary reports" Sub="Registers, schedules and summaries in PDF, Excel and CSV" Icon="file" Active="reports" />
    <div class="fa-grid-side">
        <div class="fa-card" style="align-self:start"><div class="fa-card__head"><div class="fa-card__title">Reports</div></div><ul class="fa-reports" id="rList"></ul></div>
        <div>
            <div class="fa-card">
                <div class="fa-card__head"><div><div class="fa-card__title" id="rTitle"></div><div class="fa-hint" id="rDesc"></div></div></div>
                <div class="fa-card__body"><div class="fa-form fa-form--3" id="rFilters"></div><div class="fa-error" id="rErr" style="margin-top:8px"></div></div>
                <div class="fa-card__foot" style="justify-content:flex-end;gap:8px">
                    <button type="button" class="fa-btn fa-btn--secondary" id="rPreview">Preview</button>
                    <button type="button" class="fa-btn fa-btn--primary" id="rExport"></button>
                </div>
            </div>
            <div id="rOut"></div>
        </div>
    </div>
</div>
<script>window.DC_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc-reports.js") %>?v=1"></script>
</asp:Content>
