<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AssetCategories.aspx.cs" Inherits="COOPERP_NewScreens_AssetCategories" Title="Asset Categories - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/FaHeader.ascx" TagName="FaHeader" TagPrefix="fa" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <fa:FaHeader ID="hdr" runat="server" Title="Asset categories" Sub="Categories, sub-categories and their depreciation defaults" Icon="layers" Active="categories" />

    <div class="fa-grid-2">
        <div>
            <div class="fa-card">
                <div class="fa-card__head">
                    <div class="fa-card__title">Categories <span class="fa-card__meta" id="cCount"></span></div>
                    <div class="fa-row">
                        <label class="fa-check-line"><input type="checkbox" id="cInactive" /> Show inactive</label>
                        <button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="cExpand">Expand all</button>
                    </div>
                </div>
                <div class="fa-card__body" id="cTreeWrap"><div class="fa-loading">Loading</div></div>
            </div>
            <div class="fa-pending" id="cPending"></div>
            <p class="fa-hint" id="cHelp" style="display:none">Drag a category by its handle to reorder it. Drag a sub-category onto another category, or use its Move to list, to move it. Changes are saved together after you review them.</p>
        </div>
        <div>
            <div class="fa-card" id="cEditor"><div class="fa-empty">Choose a category or sub-category to see its defaults.</div></div>
        </div>
    </div>
</div>

<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa-categories.js") %>?v=2"></script>
</asp:Content>
