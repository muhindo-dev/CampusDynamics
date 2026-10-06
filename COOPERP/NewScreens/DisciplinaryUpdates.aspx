<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="DisciplinaryUpdates.aspx.cs" Inherits="COOPERP_NewScreens_DisciplinaryUpdates" Title="Record Updates - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/DcHeader.ascx" TagName="DcHeader" TagPrefix="dc" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/dc.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <dc:DcHeader ID="hdr" runat="server" Title="Record updates" Sub="What changed in the case files, newest first" Icon="activity" Active="updates" />
    <div class="fa-filters">
        <div class="fa-filter fa-filter--grow"><label for="fQ">Search</label><input id="fQ" class="fa-input" placeholder="Case number, student or entry title" /></div>
        <div class="fa-filter"><label for="fFrom">From</label><input type="date" id="fFrom" class="fa-input" /></div>
        <div class="fa-filter"><label for="fTo">To</label><input type="date" id="fTo" class="fa-input" /></div>
        <div class="fa-filter"><label for="fEntry">Kind of entry</label><select id="fEntry" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fType">Case type</label><select id="fType" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fFac">Faculty</label><select id="fFac" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fBy">Recorded by</label><input id="fBy" class="fa-input" placeholder="Username" /></div>
        <div class="fa-filters__actions"><button type="button" class="fa-btn fa-btn--secondary" id="btnReset">Reset</button></div>
    </div>
    <div class="fa-card">
        <div class="fa-card__head"><div class="fa-card__title">Entries <span class="fa-card__meta" id="uCount"></span></div>
            <div class="fa-row"><label class="fa-check-line"><input type="checkbox" id="fSys" /> Include notification entries</label>
            <button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="btnExport"></button></div></div>
        <div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>When</th><th>Case</th><th>Entry</th><th>Recorded by</th><th>Student sees</th></tr></thead><tbody id="uBody"></tbody></table></div>
        <div class="fa-card__foot"><div id="uPager"></div></div>
    </div>
</div>
<script>window.DC_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc-updates.js") %>?v=1"></script>
</asp:Content>
