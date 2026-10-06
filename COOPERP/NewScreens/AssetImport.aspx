<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AssetImport.aspx.cs" Inherits="COOPERP_NewScreens_AssetImport" Title="Import Assets - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/FaHeader.ascx" TagName="FaHeader" TagPrefix="fa" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <fa:FaHeader ID="hdr" runat="server" Title="Import assets" Sub="Add many assets at once from the Excel template" Icon="upload" Active="assets" />
    <ol class="fa-steps" id="iSteps">
        <li data-s="1" class="is-now"><b>Step 1</b>Download the template</li>
        <li data-s="2"><b>Step 2</b>Upload the filled file</li>
        <li data-s="3"><b>Step 3</b>Check the report</li>
        <li data-s="4"><b>Step 4</b>Import</li>
    </ol>
    <div class="fa-grid-main">
        <div>
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">1. Template</div></div>
                <div class="fa-card__body">
                    <p style="margin-top:0;line-height:1.6">The template has three sheets: <strong>Assets</strong> (one row per asset; the header must stay in row 1), <strong>Lists</strong> (the valid sub-category codes, campuses, statuses and departments, also offered as drop-downs) and <strong>Notes</strong> (what to enter in each column).</p>
                    <p style="line-height:1.6">For an asset bought before the register started, fill in <strong>opening_date</strong> (normally 31 July) and <strong>opening_accum_dep</strong> (the depreciation charged up to then, or CALC to work it out).</p>
                    <a class="fa-btn fa-btn--secondary" href="AssetImport.aspx?template=1" id="iTpl"></a>
                </div></div>
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">2. Upload</div></div>
                <div class="fa-card__body">
                    <div class="fa-drop" id="iDrop">Drop the file here, or click to choose it.<br/><span class="fa-hint">Excel workbook (.xlsx) or CSV, at most <span id="iMax"></span> rows.</span></div>
                    <input type="file" id="iFile" accept=".xlsx,.csv,.xml" style="display:none" />
                </div></div>
            <div id="iReport"></div>
        </div>
        <div>
            <div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Recent imports</div></div>
                <div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>File</th><th class="fa-num">Rows</th><th>State</th></tr></thead><tbody id="iRecent"></tbody></table></div></div>
            <div class="fa-notice">Nothing is saved until you press Import, and then either every row is saved or none is.</div>
        </div>
    </div>
</div>
<script>window.FA_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa-import.js") %>?v=2"></script>
</asp:Content>
