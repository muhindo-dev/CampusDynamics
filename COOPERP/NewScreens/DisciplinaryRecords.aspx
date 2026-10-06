<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="DisciplinaryRecords.aspx.cs" Inherits="COOPERP_NewScreens_DisciplinaryRecords" Title="Disciplinary Records - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/DcHeader.ascx" TagName="DcHeader" TagPrefix="dc" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/dc.css") %>?v=1" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <dc:DcHeader ID="hdr" runat="server" Title="Disciplinary records" Sub="Every case, its status, hearings and sanctions" Icon="folder" Active="records" />

    <div class="fa-filters">
        <div class="fa-filter fa-filter--grow"><label for="fQ">Search</label><input id="fQ" class="fa-input" placeholder="Name, registration number or case number" /></div>
        <div class="fa-filter"><label for="fStatus">Status</label><select id="fStatus" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fType">Case type</label><select id="fType" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fSev">Severity</label><select id="fSev" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fYear">Academic year</label><select id="fYear" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fCampus">Campus</label><select id="fCampus" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fFac">Faculty</label><select id="fFac" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fDept">Department</label><select id="fDept" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fProg">Programme</label><select id="fProg" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fFlag">Needs attention</label><select id="fFlag" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fEffect">Under sanction</label><select id="fEffect" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fSort">Sort</label><select id="fSort" class="fa-select">
            <option value="">Newest first</option><option value="oldest">Oldest first</option><option value="updated">Recently updated</option><option value="hearing">Next hearing</option><option value="student">Student name</option></select></div>
        <div class="fa-filters__actions"><button type="button" class="fa-btn fa-btn--secondary" id="btnReset">Reset</button></div>
    </div>
    <div class="fa-chips" id="rChips"></div>

    <div class="fa-card">
        <div class="fa-card__head">
            <div class="fa-card__title">Cases <span class="fa-card__meta" id="rCount"></span></div>
            <div class="fa-row"><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="btnExport"></button></div>
        </div>
        <div class="fa-batch" id="rBatch"><span id="rBatchText"></span><span class="fa-spacer"></span>
            <button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" id="btnBatchHearing">Schedule one hearing</button>
            <button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" id="btnBatchClear">Clear selection</button></div>
        <div class="fa-table-wrap"><table class="fa-table" id="rTable">
            <thead><tr><th class="fa-check"><input type="checkbox" id="rAll" aria-label="Select all on this page" /></th><th>Case</th><th>Student</th><th>Type</th><th>Status</th><th>Next hearing</th><th>In force</th><th>Updated</th></tr></thead>
            <tbody id="rBody"><tr><td colspan="8" class="fa-loading">Loading</td></tr></tbody></table></div>
        <div class="fa-card__foot"><div id="rPager"></div></div>
    </div>
</div>
<script>window.DC_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc.js") %>?v=1"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/dc-records.js") %>?v=1"></script>
</asp:Content>
