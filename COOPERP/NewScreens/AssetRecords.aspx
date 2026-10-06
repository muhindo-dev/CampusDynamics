<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AssetRecords.aspx.cs" Inherits="COOPERP_NewScreens_AssetRecords" Title="Asset Records - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/FaHeader.ascx" TagName="FaHeader" TagPrefix="fa" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/fa.css") %>?v=2" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="fa-page">
    <fa:FaHeader ID="hdr" runat="server" Title="Asset records" Sub="The ledger of every asset, depreciation runs and year locks" Icon="list" Active="records" />

    <div class="fa-subtabs" id="faViews">
        <button type="button" class="fa-subtab is-active" data-v="ledger">Ledger</button>
        <button type="button" class="fa-subtab" data-v="dep">Depreciation runs</button>
        <button type="button" class="fa-subtab" data-v="locks">Year locks</button>
    </div>

    <!-- Ledger -->
    <div id="vLedger">
        <div class="fa-filters">
            <div class="fa-filter"><label for="lFrom">From</label><input type="date" id="lFrom" class="fa-input" /></div>
            <div class="fa-filter"><label for="lTo">To</label><input type="date" id="lTo" class="fa-input" /></div>
            <div class="fa-filter"><label for="lType">Record type</label>
                <select id="lType" class="fa-select"><option value="">All types</option><option value="ACQUISITION">Acquisition</option><option value="OPENING">Opening balance</option>
                    <option value="DEPRECIATION">Depreciation</option><option value="REVALUATION">Revaluation</option><option value="APPRECIATION">Appreciation</option>
                    <option value="TRANSFER">Transfer</option><option value="STATUS">Status change</option><option value="MAINTENANCE">Maintenance</option>
                    <option value="VERIFICATION">Verification</option><option value="ESTIMATE">Change of estimate</option><option value="DISPOSAL">Disposal</option>
                    <option value="VOID">Void</option><option value="REVERSAL">Reversal</option></select></div>
            <div class="fa-filter"><label for="lCampus">Campus</label><select id="lCampus" class="fa-select"></select></div>
            <div class="fa-filter"><label for="lCat">Category</label><select id="lCat" class="fa-select"></select></div>
            <div class="fa-filter fa-filter--grow"><label for="lQ">Search</label><input type="text" id="lQ" class="fa-input" placeholder="Asset, reason, reference or person" /></div>
            <div class="fa-filter" style="flex:0 0 auto;min-width:0"><label>&nbsp;</label><label class="fa-check-line" style="height:32px"><input type="checkbox" id="lRev" /> Show reversed</label></div>
        </div>
        <div class="fa-kpis" id="lKpis"></div>
        <div class="fa-card">
            <div class="fa-card__head"><div class="fa-card__title">Records <span class="fa-card__meta" id="lCount"></span></div>
                <button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="lExport"></button></div>
            <div class="fa-table-wrap"><table class="fa-table">
                <thead><tr><th>Date</th><th>Asset</th><th>Type</th><th>Details</th><th class="fa-num">Before</th><th class="fa-num">Change</th><th class="fa-num">After</th><th>Reason and reference</th><th>Recorded by</th></tr></thead>
                <tbody id="lBody"><tr><td colspan="9" class="fa-loading">Loading</td></tr></tbody></table></div>
            <div class="fa-card__foot"><div id="lPager" style="width:100%"></div></div>
        </div>
    </div>

    <!-- Depreciation -->
    <div id="vDep" style="display:none">
        <div class="fa-card">
            <div class="fa-card__head"><div class="fa-card__title">Run depreciation</div></div>
            <div class="fa-card__body">
                <p class="fa-hint" style="margin:0 0 12px;font-size:12px">Each asset is charged from the month after its last charge up to the period end, by month, from the month it was bought. A run never charges the same month twice. Preview first; posting repeats exactly what the preview showed.</p>
                <div class="fa-filters" style="border:0;padding:0;margin:0">
                    <div class="fa-filter"><label for="dEnd">Period end</label><input type="date" id="dEnd" class="fa-input" /></div>
                    <div class="fa-filter"><label for="dCampus">Campus</label><select id="dCampus" class="fa-select"></select></div>
                    <div class="fa-filter"><label for="dCat">Category</label><select id="dCat" class="fa-select"></select></div>
                    <div class="fa-filters__actions"><button type="button" class="fa-btn fa-btn--primary" id="dPreview">Preview</button></div>
                </div>
            </div>
        </div>
        <div id="dResult"></div>
        <div class="fa-card">
            <div class="fa-card__head"><div class="fa-card__title">Runs</div>
                <div class="fa-row"><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="dJournalYear"></button><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="dCheck">Check register</button></div></div>
            <div class="fa-table-wrap"><table class="fa-table">
                <thead><tr><th>Run</th><th>Period end</th><th>Scope</th><th class="fa-num">Assets</th><th class="fa-num">Total (UGX)</th><th>Posted</th><th>Status</th><th></th></tr></thead>
                <tbody id="dRuns"><tr><td colspan="8" class="fa-loading">Loading</td></tr></tbody></table></div>
        </div>
    </div>

    <!-- Locks -->
    <div id="vLocks" style="display:none">
        <div class="fa-notice">Lock a financial year once its accounts are final. No record can then be dated in it, posted into it or reversed from it.</div>
        <div class="fa-card">
            <div class="fa-table-wrap"><table class="fa-table">
                <thead><tr><th>Financial year</th><th class="fa-num">Records</th><th>State</th><th>Changed by</th><th>Reason</th><th></th></tr></thead>
                <tbody id="kBody"><tr><td colspan="6" class="fa-loading">Loading</td></tr></tbody></table></div>
        </div>
    </div>
</div>

<script>window.FA_BOOT = <%= BootJson %>;</script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa.js") %>?v=2"></script>
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/js/fa-records.js") %>?v=2"></script>
</asp:Content>
