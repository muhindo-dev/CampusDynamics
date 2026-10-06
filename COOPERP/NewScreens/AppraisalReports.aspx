<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AppraisalReports.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalReports" Title="Appraisal reports" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=1" />
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg></div>
        <div>
            <div class="hr-header__title">Appraisal reports</div>
            <div class="hr-header__sub">Completion and results by category and department</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <a class="hr-btn hr-btn--inverse" href="<%= ExportLink("xlsx") %>">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
            Export .xlsx
        </a>
        <a class="hr-btn hr-btn--inverse" href="<%= ExportLink("csv") %>">Export .csv</a>
        <asp:Literal ID="litSessionReport" runat="server" />
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="AppraisalDashboard.aspx">Overview</a><a class="hr-tab" href="AppraisalSessions.aspx">Sessions</a><a class="hr-tab" href="AppraisalView.aspx">Appraisals</a><a class="hr-tab hr-tab--active" href="AppraisalReports.aspx">Reports</a><a class="hr-tab" href="CompetencyTemplates.aspx">Competencies</a><a class="hr-tab" href="ExpectedStandards.aspx">Expected standards</a></div>

<asp:Literal ID="litError" runat="server" />

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="selSession">Session</label>
        <select id="selSession" class="hr-select" onchange="applyFilters()"><asp:Literal ID="litSessionOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="selDept">Department</label>
        <select id="selDept" class="hr-select" onchange="applyFilters()"><asp:Literal ID="litDeptOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="selCat">Category</label>
        <select id="selCat" class="hr-select" onchange="applyFilters()"><asp:Literal ID="litCatOptions" runat="server" /></select>
    </div>
    <div class="hr-filter">
        <label for="selStatus">Status</label>
        <select id="selStatus" class="hr-select" onchange="applyFilters()"><asp:Literal ID="litStatusOptions" runat="server" /></select>
    </div>
    <div class="hr-filters__actions">
        <a class="hr-btn hr-btn--secondary" href="AppraisalReports.aspx">Clear</a>
    </div>
</div>

<div class="hr-kpis">
    <div class="hr-kpi"><div class="hr-kpi__label">Appraisals</div><div class="hr-kpi__value"><asp:Literal ID="litKpiTotal" runat="server" Text="0" /></div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">Submitted</div><div class="hr-kpi__value"><asp:Literal ID="litKpiSubmitted" runat="server" Text="0" /></div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">Completed by supervisor</div><div class="hr-kpi__value"><asp:Literal ID="litKpiCompleted" runat="server" Text="0" /></div><div class="hr-kpi__sub"><asp:Literal ID="litKpiRate" runat="server" /></div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">HR reviewed</div><div class="hr-kpi__value"><asp:Literal ID="litKpiHr" runat="server" Text="0" /></div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">Average score %</div><div class="hr-kpi__value"><asp:Literal ID="litKpiAvg" runat="server" /></div><div class="hr-kpi__sub">Completed appraisals</div></div>
</div>

<div class="hr-grid-2">
    <div class="hr-card">
        <div class="hr-card__head"><div class="hr-card__title">By staff category</div></div>
        <div class="hr-table-wrap">
            <table class="hr-table">
                <thead><tr><th>Category</th><th class="hr-num">Appraisals</th><th class="hr-num">Submitted</th><th class="hr-num">Completed</th><th class="hr-num">HR reviewed</th><th class="hr-num">Completion %</th><th class="hr-num">Average score %</th></tr></thead>
                <asp:Literal ID="litCatRows" runat="server" />
            </table>
        </div>
    </div>
    <div class="hr-card">
        <div class="hr-card__head"><div class="hr-card__title">Classification</div><div class="hr-card__meta">Scored appraisals completed by the supervisor</div></div>
        <div class="hr-table-wrap">
            <table class="hr-table">
                <thead><tr><th>Classification</th><th>Score range</th><th class="hr-num">Appraisals</th><th class="hr-num">% of scored</th></tr></thead>
                <asp:Literal ID="litClassRows" runat="server" />
            </table>
        </div>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head"><div class="hr-card__title">By department</div></div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead><tr><th>Department</th><th class="hr-num">Appraisals</th><th class="hr-num">Submitted</th><th class="hr-num">Completed</th><th class="hr-num">HR reviewed</th><th class="hr-num">Completion %</th><th class="hr-num">Average score %</th></tr></thead>
            <asp:Literal ID="litDeptRows" runat="server" />
        </table>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">By session</div>
        <asp:Literal ID="litRecordsLink" runat="server" />
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead><tr><th>Session</th><th class="hr-num">Appraisals</th><th class="hr-num">Submitted</th><th class="hr-num">Completed</th><th class="hr-num">HR reviewed</th><th class="hr-num">Completion %</th><th class="hr-num">Average score %</th></tr></thead>
            <asp:Literal ID="litSessionRows" runat="server" />
        </table>
    </div>
</div>

</div>

<script type="text/javascript">
function applyFilters() {
    var p = [];
    var sid = document.getElementById('selSession').value;
    var dept = document.getElementById('selDept').value;
    var cat = document.getElementById('selCat').value;
    var st = document.getElementById('selStatus').value;
    if (sid && sid !== '0') p.push('sid=' + encodeURIComponent(sid));
    if (dept) p.push('dept=' + encodeURIComponent(dept));
    if (cat) p.push('cat=' + encodeURIComponent(cat));
    if (st) p.push('st=' + encodeURIComponent(st));
    window.location.href = 'AppraisalReports.aspx' + (p.length ? '?' + p.join('&') : '');
}
</script>
</asp:Content>
