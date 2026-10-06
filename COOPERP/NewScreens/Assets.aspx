<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="Assets.aspx.cs" Inherits="COOPERP_NewScreens_Assets" Title="Assets - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/FaHeader.ascx" TagName="FaHeader" TagPrefix="fa" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <fa:FaHeader ID="hdr" runat="server" Title="Assets" Sub="The fixed asset register" Icon="box" Active="assets" />

    <div class="fa-filters" id="faFilters">
        <div class="fa-filter fa-filter--grow"><label for="fQ">Search</label><input type="text" id="fQ" class="fa-input" placeholder="Asset number, tag, name or serial number" /></div>
        <div class="fa-filter"><label for="fCampus">Campus</label><select id="fCampus" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fCat">Category</label><select id="fCat" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fSub">Sub-category</label><select id="fSub" class="fa-select"></select></div>
        <div class="fa-filter"><label for="fStatus">Status</label>
            <select id="fStatus" class="fa-select">
                <option value="">All except void</option><option value="OPEN">Still held</option><option value="IN_USE">In use</option>
                <option value="IN_STORE">In store</option><option value="UNDER_REPAIR">Under repair</option><option value="LOST">Lost</option>
                <option value="CLOSED">Disposed or written off</option><option value="DISPOSED">Disposed</option><option value="WRITTEN_OFF">Written off</option>
                <option value="VOID">Void</option>
            </select></div>
        <div class="fa-filter"><label for="fDept">Department</label><select id="fDept" class="fa-select"></select></div>
        <div class="fa-filter"><label>Responsible person</label><div id="fCus"></div></div>
        <div class="fa-filter"><label for="fFy">Year acquired</label><select id="fFy" class="fa-select"></select></div>
        <div class="fa-filters__actions">
            <button type="button" class="fa-btn fa-btn--primary" id="btnApply">Apply</button>
            <button type="button" class="fa-btn fa-btn--secondary" id="btnReset">Reset</button>
        </div>
    </div>
    <div class="fa-chips" id="faChips"></div>

    <div class="fa-card">
        <div class="fa-card__head">
            <div class="fa-card__title">Register <span class="fa-card__meta" id="faCount"></span></div>
            <div class="fa-row">
                <label class="fa-label" for="fSort" style="margin:0">Sort</label>
                <select id="fSort" class="fa-select" style="width:auto;height:28px">
                    <option value="asset_no">Asset number</option><option value="name">Name</option><option value="category">Category</option>
                    <option value="purchase">Purchase date</option><option value="cost">Cost</option><option value="value">Book value</option>
                    <option value="custodian">Responsible person</option><option value="campus">Location</option><option value="status">Status</option>
                </select>
                <select id="fDir" class="fa-select" style="width:auto;height:28px"><option value="asc">Ascending</option><option value="desc">Descending</option></select>
                <button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="btnExport"></button>
            </div>
        </div>
        <div class="fa-batch" id="faBatch"></div>
        <div class="fa-table-wrap">
            <table class="fa-table">
                <thead><tr>
                    <th class="fa-check"><input type="checkbox" id="ckAll" aria-label="Select all on this page" /></th>
                    <th>Asset no</th><th>Name</th><th>Sub-category</th><th>Location</th><th>Department</th><th>Responsible person</th>
                    <th>Purchased</th><th class="fa-num">Cost (UGX)</th><th class="fa-num">Book value (UGX)</th><th>Change</th><th>Status</th>
                </tr></thead>
                <tbody id="faBody"><tr><td colspan="12" class="fa-loading">Loading</td></tr></tbody>
                <tfoot id="faFoot"></tfoot>
            </table>
        </div>
        <div class="fa-card__foot"><div id="faPager" style="width:100%"></div></div>
    </div>
</div>

<script>window.FA_BOOT = <%= BootJson %>;</script>
<script src="https://cdn.jsdelivr.net/npm/chart.js@3.9.1/dist/chart.min.js"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa-assets.js") %>?v=2"></script>
</asp:Content>
