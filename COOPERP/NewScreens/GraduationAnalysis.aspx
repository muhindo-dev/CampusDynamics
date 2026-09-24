<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationAnalysis.aspx.cs" Inherits="COOPERP_NewScreens_GraduationAnalysis" Title="Graduation Analysis - Campus Dynamics" %>
<%@ Register Assembly="DevExpress.Web.v16.1, Version=16.1.4.0, Culture=neutral, PublicKeyToken=b88d1754d700e49a" Namespace="DevExpress.Web" TagPrefix="dx" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="Server">
<style type="text/css">
/* ---- Page Header ---- */
.cd-page-header { background:#05275C; padding:14px 0 12px; margin-bottom:16px; border-bottom:3px solid #041d45; display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:10px; }
.cd-page-header__left { display:flex; align-items:center; gap:12px; }
.cd-page-header__icon { width:38px; height:38px; background:rgba(255,255,255,.12); display:flex; align-items:center; justify-content:center; border-radius:4px; flex-shrink:0; }
.cd-page-header__title { font-size:16px; font-weight:700; color:#fff; line-height:1.2; margin:0; }
.cd-page-header__sub { font-size:12px; color:rgba(255,255,255,.75); margin-top:2px; }
/* Graduation Analysis Module Styles - ga- prefix */
.ga-container { padding: 8px; font-size: 11px; }

/* The compact identity line the rest of the Graduation module uses, so this page stops
   announcing itself in a navy banner nothing else in the menu has. */
.ga-id { display:flex; align-items:baseline; gap:8px; flex-wrap:wrap;
    padding:0 0 7px; margin-bottom:9px; border-bottom:1px solid #e0e5ed; }
.ga-id b { font-size:13px; color:#05275C; line-height:1.2; }
.ga-id span { font-size:10.5px; color:#8a94a6; }
.ga-id__scope { font-size:10px; color:#8a94a6; margin-left:auto; white-space:nowrap; }
.ga-noaccess { background:#fff8e1; border:1px solid #fde68a; border-left:3px solid #d97706;
    color:#92400e; font-size:11px; line-height:1.5; padding:8px 10px; border-radius:3px; }
.ga-header { margin-bottom: 6px; }
.ga-title { font-size: 14px; font-weight: 600; color: #1a1a2e; margin: 0 0 4px 0; }

/* Compact Inline Stats Bar */
.ga-stats-bar { display: flex; align-items: center; gap: 4px; padding: 4px 8px; background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%); border-radius: 4px; font-size: 10px; margin-bottom: 6px; flex-wrap: wrap; }
.ga-stat { display: flex; align-items: center; gap: 3px; padding: 2px 6px; background: #fff; border-radius: 3px; border: 1px solid #dee2e6; }
.ga-stat-label { color: #6c757d; }
.ga-stat-value { font-weight: 600; color: #495057; }
.ga-stat--primary .ga-stat-value { color: #0d6efd; }
.ga-stat--success .ga-stat-value { color: #198754; }
.ga-stat--warning .ga-stat-value { color: #fd7e14; }
.ga-stat--info .ga-stat-value { color: #0dcaf0; }

/* Filter Toggle Button */
.ga-filter-toggle { margin-left: auto; padding: 3px 8px; font-size: 10px; background: #e7f1ff; border: 1px solid #b6d4fe; border-radius: 3px; color: #0d6efd; cursor: pointer; display: flex; align-items: center; gap: 4px; }
.ga-filter-toggle:hover { background: #cfe2ff; }
.ga-filter-toggle svg { width: 12px; height: 12px; }

/* Collapsible Filter Row */
.ga-filter-row { display: none; padding: 6px 8px; background: #f8f9fa; border: 1px solid #e9ecef; border-radius: 4px; margin-bottom: 6px; }
.ga-filter-row.show { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.ga-filter-group { display: flex; align-items: center; gap: 3px; }
.ga-filter-group label { font-size: 10px; color: #6c757d; white-space: nowrap; }
.ga-filter-group select { font-size: 10px; padding: 2px 4px; border: 1px solid #ced4da; border-radius: 3px; min-width: 100px; }

/* Card */
.ga-card { background: #fff; border: 1px solid #e9ecef; border-radius: 4px; margin-bottom: 6px; }
.ga-card__header { padding: 6px 10px; background: #f8f9fa; border-bottom: 1px solid #e9ecef; font-weight: 600; font-size: 11px; display: flex; justify-content: space-between; align-items: center; }
.ga-card__body { padding: 6px; }

/* Action Buttons */
.ga-btn { padding: 3px 10px; font-size: 10px; border: 1px solid #dee2e6; border-radius: 3px; cursor: pointer; display: inline-flex; align-items: center; gap: 4px; background: #fff; color: #495057; }
.ga-btn:hover { background: #f8f9fa; }
.ga-btn--primary { background: #0d6efd; border-color: #0d6efd; color: #fff; }
.ga-btn--primary:hover { background: #0b5ed7; }
.ga-btn--warning { background: #fd7e14; border-color: #fd7e14; color: #fff; }
.ga-btn--warning:hover { background: #e96e0a; }
.ga-btn--danger { background: #dc3545; border-color: #dc3545; color: #fff; }
.ga-btn--danger:hover { background: #bb2d3b; }
.ga-btn-group { display: flex; gap: 4px; }

/* Summary Table */
.ga-summary-table { width: 100%; border-collapse: collapse; font-size: 10px; }
.ga-summary-table th { background: linear-gradient(180deg, #f8f9fa 0%, #e9ecef 100%); padding: 6px 8px; text-align: left; font-weight: 600; border: 1px solid #dee2e6; }
.ga-summary-table td { padding: 5px 8px; border: 1px solid #dee2e6; }
.ga-summary-table tr:hover { background-color: #e8f4fc; }
.ga-summary-table .text-center { text-align: center; }
.ga-summary-table .text-right { text-align: right; }
.ga-summary-table .font-bold { font-weight: 600; }
.ga-summary-table .total-row { background: #e9ecef; font-weight: 600; }

/* Analysis Grid */
.ga-grid-container { display: grid; grid-template-columns: repeat(auto-fit, minmax(400px, 1fr)); gap: 8px; }

/* Chart Placeholder */
.ga-chart { min-height: 200px; display: flex; align-items: center; justify-content: center; background: #f8f9fa; border: 1px dashed #dee2e6; border-radius: 4px; color: #6c757d; }

/* Stats Badges */
.ga-badge { display: inline-block; padding: 2px 6px; border-radius: 3px; font-size: 9px; font-weight: 600; }
.ga-badge--male { background: #cce5ff; color: #004085; }
.ga-badge--female { background: #f8d7da; color: #721c24; }
.ga-badge--first { background: #d4edda; color: #155724; }
.ga-badge--upper { background: #cce5ff; color: #004085; }
.ga-badge--lower { background: #fff3cd; color: #856404; }
.ga-badge--pass { background: #e2e3e5; color: #383d41; }
</style>
<script type="text/javascript">
function toggleFilters() {
    var filterRow = document.querySelector('.ga-filter-row');
    if (filterRow) {
        filterRow.classList.toggle('show');
    }
}
</script>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="Server">
<div class="ga-container">

<%-- The navy banner is gone. The sidebar already says where you are, and the other four pages
     in this menu spend that ninety pixels on data instead. What replaces it is the same compact
     identity line they use: what this page is, and whose data you are looking at. --%>
<div class="ga-id">
    <b>Graduation analysis</b>
    <span>Outcomes on the graduation lists on record</span>
    <asp:Label ID="lblScope" runat="server" CssClass="ga-id__scope" />
</div>

<asp:Panel ID="pnlNoAccess" runat="server" Visible="false" CssClass="ga-noaccess">
    Your account is not linked to a faculty or department, so no graduation data is available.
</asp:Panel>

<asp:Panel ID="pnlAnalysis" runat="server">
    <!-- Stats Bar with Filter Toggle -->
    <div class="ga-stats-bar">
        <div class="ga-stat ga-stat--primary">
            <span class="ga-stat-label">Convocation:</span>
            <span class="ga-stat-value"><asp:Literal ID="litConvocationDisplay" runat="server" /></span>
        </div>
        <div class="ga-stat ga-stat--primary">
            <span class="ga-stat-label">Total Graduands:</span>
            <span class="ga-stat-value"><asp:Literal ID="litTotalGraduands" runat="server">0</asp:Literal></span>
        </div>
        <div class="ga-stat ga-stat--success">
            <span class="ga-stat-label">Male:</span>
            <span class="ga-stat-value"><asp:Literal ID="litMaleCount" runat="server">0</asp:Literal></span>
        </div>
        <div class="ga-stat ga-stat--warning">
            <span class="ga-stat-label">Female:</span>
            <span class="ga-stat-value"><asp:Literal ID="litFemaleCount" runat="server">0</asp:Literal></span>
        </div>
        <button type="button" class="ga-filter-toggle" onclick="toggleFilters()">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" /></svg>
            Filters
        </button>
    </div>
    
    <!-- Collapsible Filter Row -->
    <div class="ga-filter-row show">
        <div class="ga-filter-group">
            <label>Faculty:</label>
            <asp:DropDownList ID="ddlFaculty" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlFaculty_SelectedIndexChanged">
                <asp:ListItem Text="-- All Faculties --" Value="" />
            </asp:DropDownList>
        </div>
        <div class="ga-filter-group">
            <label>Programme:</label>
            <asp:DropDownList ID="ddlProgramme" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlProgramme_SelectedIndexChanged">
                <asp:ListItem Text="-- All Programmes --" Value="" />
            </asp:DropDownList>
        </div>
        <div class="ga-filter-group">
            <label>Convocation:</label>
            <asp:DropDownList ID="ddlConvocation" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlConvocation_SelectedIndexChanged">
                <asp:ListItem Text="-- All --" Value="" />
            </asp:DropDownList>
        </div>
        <asp:Button ID="btnRefresh" runat="server" Text="Refresh" CssClass="ga-btn" OnClick="btnRefresh_Click" />
    </div>
    
    <div class="ga-grid-container">
        <!-- Summary by Faculty -->
        <div class="ga-card">
            <div class="ga-card__header">
                <span>Summary by Faculty</span>
                <div class="ga-btn-group">
                    <asp:Button ID="btnExportFacultyExcel" runat="server" Text="Export Excel" CssClass="ga-btn ga-btn--warning" OnClick="btnExportFacultyExcel_Click" />
                </div>
            </div>
            <div class="ga-card__body">
                <asp:GridView ID="gvFacultySummary" runat="server" AutoGenerateColumns="False" CssClass="ga-summary-table" ShowFooter="true">
                    <Columns>
                        <asp:BoundField DataField="faculty" HeaderText="Faculty" />
                        <asp:BoundField DataField="male_count" HeaderText="Male" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                        <asp:BoundField DataField="female_count" HeaderText="Female" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                        <asp:BoundField DataField="total" HeaderText="Total" ItemStyle-CssClass="text-center font-bold" HeaderStyle-CssClass="text-center" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>
        
        <!-- Summary by Programme -->
        <div class="ga-card">
            <div class="ga-card__header">
                <span>Summary by Programme</span>
                <div class="ga-btn-group">
                    <asp:Button ID="btnExportProgExcel" runat="server" Text="Export Excel" CssClass="ga-btn ga-btn--warning" OnClick="btnExportProgExcel_Click" />
                </div>
            </div>
            <div class="ga-card__body">
                <asp:GridView ID="gvProgrammeSummary" runat="server" AutoGenerateColumns="False" CssClass="ga-summary-table" ShowFooter="true">
                    <Columns>
                        <asp:BoundField DataField="prog_name" HeaderText="Programme" />
                        <asp:BoundField DataField="male_count" HeaderText="Male" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                        <asp:BoundField DataField="female_count" HeaderText="Female" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                        <asp:BoundField DataField="total" HeaderText="Total" ItemStyle-CssClass="text-center font-bold" HeaderStyle-CssClass="text-center" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </div>
    
    <!-- Summary by Degree Class -->
    <div class="ga-card">
        <div class="ga-card__header">
            <span>Summary by Degree Class</span>
            <div class="ga-btn-group">
                <asp:Button ID="btnExportClassExcel" runat="server" Text="Export Excel" CssClass="ga-btn ga-btn--warning" OnClick="btnExportClassExcel_Click" />
                <asp:Button ID="btnExportFullPDF" runat="server" Text="Full Report (PDF)" CssClass="ga-btn ga-btn--danger" OnClick="btnExportFullPDF_Click" />
            </div>
        </div>
        <div class="ga-card__body">
            <asp:GridView ID="gvClassSummary" runat="server" AutoGenerateColumns="False" CssClass="ga-summary-table" ShowFooter="true">
                <Columns>
                    <asp:BoundField DataField="degclass" HeaderText="Degree Class" />
                    <asp:BoundField DataField="male_count" HeaderText="Male" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                    <asp:BoundField DataField="female_count" HeaderText="Female" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center" />
                    <asp:BoundField DataField="total" HeaderText="Total" ItemStyle-CssClass="text-center font-bold" HeaderStyle-CssClass="text-center" />
                    <asp:TemplateField HeaderText="Percentage" ItemStyle-CssClass="text-center" HeaderStyle-CssClass="text-center">
                        <ItemTemplate>
                            <%# String.Format("{0:N1}%", Eval("percentage")) %>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </div>
    
    <!-- Detailed Graduands List -->
    <div class="ga-card">
        <div class="ga-card__header">
            <span>Detailed Graduands List</span>
            <div class="ga-btn-group">
                <asp:Button ID="btnExportDetailExcel" runat="server" Text="Export All" CssClass="ga-btn ga-btn--warning" OnClick="btnExportDetailExcel_Click" />
            </div>
        </div>
        <div class="ga-card__body">
            <dx:ASPxGridView ID="gvGraduandsDetail" runat="server" Width="100%" AutoGenerateColumns="False" KeyFieldName="regno">
                <SettingsPager PageSize="25" AlwaysShowPager="true" />
                <Settings ShowFilterRow="true" />
                <SettingsSearchPanel Visible="true" />
                <Columns>
                    <dx:GridViewDataTextColumn FieldName="regno" Caption="Reg No" VisibleIndex="0" Width="100px">
                        <CellStyle Font-Bold="true" ForeColor="#174DA4" />
                    </dx:GridViewDataTextColumn>
                    <dx:GridViewDataTextColumn FieldName="stud_name" Caption="Student Name" VisibleIndex="1" Width="180px">
                    </dx:GridViewDataTextColumn>
                    <dx:GridViewDataTextColumn FieldName="prog_name" Caption="Programme" VisibleIndex="2" Width="150px">
                    </dx:GridViewDataTextColumn>
                    <dx:GridViewDataTextColumn FieldName="cgpa" Caption="CGPA" VisibleIndex="3" Width="60px">
                        <CellStyle HorizontalAlign="Center" Font-Bold="true" />
                    </dx:GridViewDataTextColumn>
                    <dx:GridViewDataTextColumn FieldName="degclass" Caption="Class" VisibleIndex="4" Width="120px">
                    </dx:GridViewDataTextColumn>
                    <dx:GridViewDataTextColumn FieldName="gen" Caption="Gender" VisibleIndex="5" Width="60px">
                        <CellStyle HorizontalAlign="Center" />
                    </dx:GridViewDataTextColumn>
                    <dx:GridViewDataDateColumn FieldName="grad_date" Caption="Grad Date" VisibleIndex="6" Width="90px">
                        <PropertiesDateEdit DisplayFormatString="dd/MM/yyyy" />
                    </dx:GridViewDataDateColumn>
                    <dx:GridViewDataTextColumn FieldName="convocation" Caption="Convocation" VisibleIndex="7" Width="90px">
                    </dx:GridViewDataTextColumn>
                </Columns>
                <Styles>
                    <Header BackColor="#f8f9fa" Font-Bold="true" Font-Size="10px" />
                    <Row Font-Size="10px" />
                    <AlternatingRow BackColor="#fafbfc" />
                </Styles>
            </dx:ASPxGridView>
        </div>
    </div>
    
    <!-- Excel Exporter -->
    <dx:ASPxGridViewExporter ID="gvExporter" runat="server" GridViewID="gvGraduandsDetail" />
</asp:Panel>
</div>
</asp:Content>
