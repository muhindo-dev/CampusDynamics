<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AcademicYears.aspx.cs" Inherits="COOPERP_NewScreens_AcademicYears" Title="Academic Years - Campus Dynamics" %>
<%@ Register Assembly="DevExpress.Web.v16.1, Version=16.1.4.0, Culture=neutral, PublicKeyToken=b88d1754d700e49a" Namespace="DevExpress.Web" TagPrefix="dx" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===== ACADEMIC YEARS MANAGEMENT ======================================= */

/* -- Page Header ----------------------------------- */
.cd-page-header { background:#05275C; padding:14px 0 12px; margin-bottom:16px; border-bottom:3px solid #041d45; display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:10px; }
.cd-page-header__left { display:flex; align-items:center; gap:12px; }
.cd-page-header__icon { width:38px; height:38px; background:rgba(255,255,255,.12); display:flex; align-items:center; justify-content:center; border-radius:4px; flex-shrink:0; }
.cd-page-header__title { font-size:16px; font-weight:700; color:#fff; line-height:1.2; margin:0; }
.cd-page-header__sub { font-size:12px; color:rgba(255,255,255,.75); margin-top:2px; }
.cd-page-header__right { display:flex; gap:8px; align-items:center; }

/* -- Stats Row ------------------------------------- */
.ay-stats-row {
    display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 12px; margin-bottom: 18px;
}
.ay-stat {
    background: #fff; border: 1px solid #e4e8f0;
    padding: 16px 18px; border-radius: 4px;
    position: relative; overflow: hidden;
    transition: background .2s;
}
.ay-stat::after {
    content: ''; position: absolute;
    left: 0; top: 0; bottom: 0; width: 4px;
    background: var(--stat-accent, #ccc);
    border-radius: 4px 0 0 4px;
}
.ay-stat:hover { background: #f9fafc; }
.ay-stat__label { font-size: 10px; text-transform: uppercase; letter-spacing: .6px; color: #999; font-weight: 600; margin-bottom: 4px; }
.ay-stat__val { font-size: 24px; font-weight: 800; line-height: 1.1; }
.ay-stat--total   { --stat-accent: #174DA4; } .ay-stat--total   .ay-stat__val { color: #174DA4; }
.ay-stat--active  { --stat-accent: #2e7d32; } .ay-stat--active  .ay-stat__val { color: #2e7d32; }
.ay-stat--current { --stat-accent: #e65100; } .ay-stat--current .ay-stat__val { color: #e65100; }
.ay-stat--finance { --stat-accent: #00695c; } .ay-stat--finance .ay-stat__val { color: #00695c; }
.ay-stat--sem     { --stat-accent: #6a1b9a; } .ay-stat--sem     .ay-stat__val { color: #6a1b9a; font-size:14px; font-weight:700; }

/* -- Toolbar --------------------------------------- */
.ay-toolbar {
    display: flex; align-items: center; justify-content: space-between;
    margin-bottom: 14px; gap: 10px; flex-wrap: wrap;
}
.ay-toolbar__left { display: flex; align-items: center; gap: 8px; }
.ay-toolbar__right { display: flex; align-items: center; gap: 8px; }

.ay-btn {
    display: inline-flex; align-items: center; gap: 6px;
    padding: 8px 16px; border-radius: 0; border: 1px solid #dde1e8;
    font-size: 12px; font-weight: 600; cursor: pointer;
    background: #fff; color: #333; transition: all .15s;
}
.ay-btn:hover { border-color: #bbb; }
.ay-btn--primary { background: #174DA4; color: #fff; border-color: #174DA4; }
.ay-btn--primary:hover { background: #1257b8; }
.ay-btn--success { background: #2e7d32; color: #fff; border-color: #2e7d32; }
.ay-btn--success:hover { background: #388e3c; }
.ay-btn--danger  { background: #c62828; color: #fff; border-color: #c62828; }
.ay-btn--danger:hover  { background: #d32f2f; }
.ay-btn svg { width: 14px; height: 14px; }

/* -- Alert / Toast --------------------------------- */
.ay-alert {
    padding: 12px 16px; border-radius: 0; margin-bottom: 14px;
    font-size: 12px; font-weight: 500; display: none;
    border: 1px solid transparent;
}
.ay-alert--success { display: block; background: #e8f5e9; border-color: #a5d6a7; color: #2e7d32; }
.ay-alert--error   { display: block; background: #ffebee; border-color: #ef9a9a; color: #c62828; }

/* -- Grid Wrapper ---------------------------------- */
.ay-grid-wrap {
    background: #fff; border: 1px solid #e4e8f0;
    border-radius: 4px; overflow: hidden;
}
.ay-grid-wrap .dxgvControl_Office2010Blue { border: none !important; }
.ay-grid-wrap .dxgvHeader_Office2010Blue {
    background: #f7f8fb !important; color: #555 !important;
    font-size: 11px !important; font-weight: 700 !important;
    text-transform: uppercase; letter-spacing: .5px;
    padding: 10px 12px !important; border-bottom: 2px solid #e4e8f0 !important;
}
.ay-grid-wrap .dxgvDataRow_Office2010Blue td,
.ay-grid-wrap .dxgvDataRowAlt_Office2010Blue td {
    padding: 10px 12px !important; font-size: 12px !important;
    vertical-align: middle !important; border-color: #f0f2f5 !important;
    color: #333 !important;
}
.ay-grid-wrap .dxgvDataRowAlt_Office2010Blue td { background: #fafbfc !important; }
.ay-grid-wrap .dxgvDataRow_Office2010Blue:hover td,
.ay-grid-wrap .dxgvDataRowAlt_Office2010Blue:hover td {
    background: #eef3fb !important;
}
.ay-grid-wrap .dxgvFooter_Office2010Blue { background: #f7f8fb !important; border-top: 2px solid #e4e8f0 !important; padding: 8px 12px !important; }
.ay-grid-wrap .dxgvPagerBottomPanel_Office2010Blue { background: #f7f8fb !important; border-top: 1px solid #e4e8f0 !important; padding: 8px !important; }

/* -- Status Badge ---------------------------------- */
.ay-badge {
    display: inline-flex; align-items: center; gap: 4px;
    padding: 3px 10px; border-radius: 0; font-size: 11px; font-weight: 600;
}
.ay-badge--active   { background: #e8f5e9; color: #2e7d32; }
.ay-badge--inactive { background: #f5f5f5; color: #999; }
.ay-badge--current  { background: #e3f2fd; color: #174DA4; }
.ay-badge--finance  { background: #e0f2f1; color: #00695c; }

/* -- Modal ----------------------------------------- */
.ay-modal-overlay {
    display: none; position: fixed; top: 0; left: 0; right: 0; bottom: 0;
    background: rgba(0,0,0,.4); z-index: 9999;
    align-items: center; justify-content: center;
}
.ay-modal-overlay.open { display: flex; }
.ay-modal {
    background: #fff; border-radius: 2px; width: 580px; max-width: 94vw;
    max-height: 90vh; overflow-y: auto;
    box-shadow: 0 12px 40px rgba(0,0,0,.18);
}
.ay-modal__header {
    display: flex; align-items: center; justify-content: space-between;
    padding: 18px 22px; border-bottom: 1px solid #e4e8f0;
}
.ay-modal__title { font-size: 15px; font-weight: 700; color: #1a1a2e; }
.ay-modal__close {
    width: 30px; height: 30px; display: flex; align-items: center; justify-content: center;
    border: none; background: #f5f5f5; border-radius: 0; cursor: pointer;
    color: #666; transition: all .15s;
}
.ay-modal__close:hover { background: #e0e0e0; }
.ay-modal__body { padding: 22px; }
.ay-modal__footer {
    display: flex; align-items: center; justify-content: flex-end; gap: 8px;
    padding: 14px 22px; border-top: 1px solid #e4e8f0; background: #fafbfc;
}

/* -- Form Fields ----------------------------------- */
.ay-form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 14px; }
.ay-form-group { margin-bottom: 0; }
.ay-form-group--full { grid-column: 1 / -1; }
.ay-form-label { display: block; font-size: 11px; font-weight: 600; color: #555; margin-bottom: 4px; text-transform: uppercase; letter-spacing: .4px; }
.ay-form-input {
    width: 100%; padding: 8px 12px; border: 1px solid #dde1e8;
    border-radius: 0; font-size: 13px; color: #333;
    background: #fff; transition: border-color .15s;
    box-sizing: border-box;
}
.ay-form-input:focus { border-color: #174DA4; outline: none; }
select.ay-form-input { cursor: pointer; }

/* auto-fill year 2 */
.ay-year-display { display: inline-flex; align-items: center; gap: 6px; font-size: 15px; font-weight: 700; color: #174DA4; padding: 6px 0; }

/* -- Responsive ------------------------------------ */
@media (max-width: 900px) {
    .ay-stats-row { grid-template-columns: repeat(2, 1fr); }
    .ay-form-row { grid-template-columns: 1fr; }
}
@media (max-width: 600px) {
    .ay-stats-row { grid-template-columns: 1fr; }
    .ay-toolbar { flex-direction: column; align-items: stretch; }
}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- ======= PAGE HEADER ======= -->
<div class="cd-page-header">
    <div class="cd-page-header__left">
        <div class="cd-page-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect><line x1="16" y1="2" x2="16" y2="6"></line><line x1="8" y1="2" x2="8" y2="6"></line><line x1="3" y1="10" x2="21" y2="10"></line></svg>
        </div>
        <div>
            <div class="cd-page-header__title">Academic Years</div>
            <div class="cd-page-header__sub">Manage academic &amp; financial year settings</div>
        </div>
    </div>
    <div class="cd-page-header__right">
        <button type="button" class="ay-btn ay-btn--primary" onclick="openAddModal();">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg>
            Add Academic Year
        </button>
    </div>
</div>

<!-- ======= STATS ROW ======= -->
<div class="ay-stats-row">
    <div class="ay-stat ay-stat--total">
        <div class="ay-stat__label">Total Years</div>
        <div class="ay-stat__val"><asp:Literal ID="litTotal" runat="server" Text="0" /></div>
    </div>
    <div class="ay-stat ay-stat--active">
        <div class="ay-stat__label">Active Years</div>
        <div class="ay-stat__val"><asp:Literal ID="litActive" runat="server" Text="0" /></div>
    </div>
    <div class="ay-stat ay-stat--current">
        <div class="ay-stat__label">Current Academic Year</div>
        <div class="ay-stat__val"><asp:Literal ID="litCurrentAcad" runat="server" Text="-" /></div>
    </div>
    <div class="ay-stat ay-stat--finance">
        <div class="ay-stat__label">Current Financial Year</div>
        <div class="ay-stat__val"><asp:Literal ID="litCurrentFin" runat="server" Text="-" /></div>
    </div>
    <div class="ay-stat ay-stat--sem">
        <div class="ay-stat__label">Active Semester(s)</div>
        <div class="ay-stat__val"><asp:Literal ID="litActiveSems" runat="server" Text="None" /></div>
    </div>
</div>

<!-- ======= ALERT AREA ======= -->
<asp:Panel ID="pnlAlert" runat="server" CssClass="ay-alert" Visible="false"></asp:Panel>

<!-- ======= TOOLBAR ======= -->
<div class="ay-toolbar">
    <div class="ay-toolbar__left">
        <span style="font-size:12px;color:#888;">
            Showing <asp:Literal ID="litShowing" runat="server" Text="0" /> academic years
        </span>
    </div>
    <div class="ay-toolbar__right">
        <asp:DropDownList ID="ddlStatusFilter" runat="server" CssClass="ay-form-input" AutoPostBack="true"
            OnSelectedIndexChanged="ddlStatusFilter_SelectedIndexChanged" style="width:140px;font-size:12px;padding:6px 10px;">
            <asp:ListItem Text="All Statuses" Value="" />
            <asp:ListItem Text="Active" Value="Active" Selected="True" />
            <asp:ListItem Text="Inactive" Value="Inactive" />
        </asp:DropDownList>
    </div>
</div>

<!-- ======= GRID ======= -->
<div class="ay-grid-wrap">
    <dx:ASPxGridView ID="gridYears" runat="server" Width="100%" KeyFieldName="ID"
        AutoGenerateColumns="False" Theme="Office2010Blue"
        OnCustomButtonCallback="gridYears_CustomButtonCallback">
        <Settings ShowFilterRow="false" ShowGroupPanel="false" />
        <SettingsBehavior AllowFocusedRow="false" />
        <SettingsPager PageSize="20" />
        <Columns>
            <dx:GridViewDataTextColumn FieldName="acadyear" Caption="Academic Year" Width="130px">
                <CellStyle Font-Bold="True" Font-Size="13px" />
            </dx:GridViewDataTextColumn>
            <dx:GridViewDataDateColumn FieldName="start_date" Caption="Start Date" Width="110px">
                <PropertiesDateEdit DisplayFormatString="dd MMM yyyy" />
            </dx:GridViewDataDateColumn>
            <dx:GridViewDataDateColumn FieldName="end_date" Caption="End Date" Width="110px">
                <PropertiesDateEdit DisplayFormatString="dd MMM yyyy" />
            </dx:GridViewDataDateColumn>
            <dx:GridViewDataTextColumn FieldName="semester_count" Caption="Semesters" Width="85px">
                <CellStyle HorizontalAlign="Center" />
                <HeaderStyle HorizontalAlign="Center" />
            </dx:GridViewDataTextColumn>
            <dx:GridViewDataTextColumn Caption="Semester Status" Width="160px">
                <DataItemTemplate>
                    <%# GetSemesterStatusHtml(Eval("semester_1_is_active"), Eval("semester_2_is_active"), Eval("semester_3_is_active"), Eval("semester_count")) %>
                </DataItemTemplate>
                <HeaderStyle HorizontalAlign="Center" />
                <CellStyle HorizontalAlign="Center" />
            </dx:GridViewDataTextColumn>
            <dx:GridViewDataTextColumn Caption="Admission" Width="130px" ToolTip="Whether applicants may apply for this year">
                <DataItemTemplate>
                    <%# GetAdmissionHtml(Eval("acadyear")) %>
                </DataItemTemplate>
                <HeaderStyle HorizontalAlign="Center" />
                <CellStyle HorizontalAlign="Center" />
            </dx:GridViewDataTextColumn>
            <dx:GridViewDataTextColumn FieldName="is_current_year" Caption="Current Year" Width="100px">
                <DataItemTemplate>
                    <%# Eval("is_current_year").ToString() == "Yes"
                            ? "<span class='ay-badge ay-badge--current'>&#10003; Current</span>"
                            : "<span style='color:#bbb;font-size:11px;'>-</span>" %>
                </DataItemTemplate>
                <HeaderStyle HorizontalAlign="Center" />
                <CellStyle HorizontalAlign="Center" />
            </dx:GridViewDataTextColumn>
            <dx:GridViewDataTextColumn FieldName="is_current_financial_year" Caption="Financial Year" Width="110px">
                <DataItemTemplate>
                    <%# Eval("is_current_financial_year").ToString() == "Yes"
                            ? "<span class='ay-badge ay-badge--finance'>&#10003; Financial</span>"
                            : "<span style='color:#bbb;font-size:11px;'>-</span>" %>
                </DataItemTemplate>
                <HeaderStyle HorizontalAlign="Center" />
                <CellStyle HorizontalAlign="Center" />
            </dx:GridViewDataTextColumn>
            <dx:GridViewDataTextColumn FieldName="status" Caption="Status" Width="90px">
                <DataItemTemplate>
                    <%# Eval("status").ToString() == "Active"
                            ? "<span class='ay-badge ay-badge--active'>Active</span>"
                            : "<span class='ay-badge ay-badge--inactive'>Inactive</span>" %>
                </DataItemTemplate>
                <HeaderStyle HorizontalAlign="Center" />
                <CellStyle HorizontalAlign="Center" />
            </dx:GridViewDataTextColumn>
            <dx:GridViewDataColumn Caption="Actions" Width="180px" UnboundType="String">
                <DataItemTemplate>
                    <div style="display:flex;gap:4px;align-items:center;">
                        <button type="button" class="ay-btn ay-action-edit" style="padding:4px 10px;font-size:11px;"
                                data-id='<%# Eval("ID") %>' onclick="editYear(this.getAttribute('data-id'));return false;">
                            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path></svg>
                            Edit
                        </button>
                        <button type="button" class="ay-btn ay-btn--success ay-action-setcurrent" style="padding:4px 8px;font-size:11px;"
                                data-year='<%# Eval("acadyear") %>' onclick="setCurrentYear(this.getAttribute('data-year'));return false;"
                                title="Set as current academic year">
                            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
                            Set Current
                        </button>
                    </div>
                </DataItemTemplate>
                <HeaderStyle HorizontalAlign="Center" />
                <CellStyle HorizontalAlign="Center" />
            </dx:GridViewDataColumn>
        </Columns>
    </dx:ASPxGridView>
</div>

<!-- ======= ADD / EDIT MODAL ======= -->
<div class="ay-modal-overlay" id="modalOverlay">
    <div class="ay-modal">
        <div class="ay-modal__header">
            <span class="ay-modal__title" id="modalTitle">Add Academic Year</span>
            <button type="button" class="ay-modal__close" onclick="closeModal();">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
            </button>
        </div>
        <div class="ay-modal__body">
            <asp:HiddenField ID="hfEditId" runat="server" Value="" />

            <!-- Academic Year Preview -->
            <div style="text-align:center;margin-bottom:18px;">
                <div style="font-size:10px;text-transform:uppercase;letter-spacing:.6px;color:#999;font-weight:600;margin-bottom:4px;">ACADEMIC YEAR</div>
                <div class="ay-year-display" id="yearPreview">-</div>
            </div>

            <div class="ay-form-row">
                <div class="ay-form-group">
                    <label class="ay-form-label">Start Year *</label>
                    <asp:TextBox ID="txtStartYear" runat="server" CssClass="ay-form-input" placeholder="e.g. 2025"
                        MaxLength="4" onkeyup="updateYearPreview();" onchange="updateYearPreview();" />
                </div>
                <div class="ay-form-group">
                    <label class="ay-form-label">End Year (auto)</label>
                    <asp:TextBox ID="txtEndYear" runat="server" CssClass="ay-form-input" ReadOnly="true"
                        style="background:#f5f5f5;color:#888;" />
                </div>
            </div>
            <div class="ay-form-row">
                <div class="ay-form-group">
                    <label class="ay-form-label">Start Date *</label>
                    <asp:TextBox ID="txtStartDate" runat="server" CssClass="ay-form-input" TextMode="Date" />
                </div>
                <div class="ay-form-group">
                    <label class="ay-form-label">End Date *</label>
                    <asp:TextBox ID="txtEndDate" runat="server" CssClass="ay-form-input" TextMode="Date" />
                </div>
            </div>
            <div class="ay-form-row">
                <div class="ay-form-group">
                    <label class="ay-form-label">Semester Count</label>
                    <asp:DropDownList ID="ddlSemesterCount" runat="server" CssClass="ay-form-input">
                        <asp:ListItem Text="2 Semesters" Value="2" Selected="True" />
                        <asp:ListItem Text="3 Semesters (Trimester)" Value="3" />
                        <asp:ListItem Text="1 Semester" Value="1" />
                    </asp:DropDownList>
                </div>
                <div class="ay-form-group">
                    <label class="ay-form-label">Status</label>
                    <asp:DropDownList ID="ddlStatus" runat="server" CssClass="ay-form-input">
                        <asp:ListItem Text="Active" Value="Active" Selected="True" />
                        <asp:ListItem Text="Inactive" Value="Inactive" />
                    </asp:DropDownList>
                </div>
            </div>

            <%-- ── Semester Active Status ──────────────────────── --%>
            <div style="border:1px solid #e4e8f0;border-radius:4px;padding:14px 16px;margin-bottom:14px;">
                <div style="font-size:11px;font-weight:700;color:#555;text-transform:uppercase;letter-spacing:.5px;margin-bottom:10px;">Semester Active Status</div>
                <div style="font-size:11px;color:#888;margin-bottom:12px;">Set a semester to <strong>Yes (Active)</strong> to open course registration for that semester. Set to <strong>No</strong> to block all course registrations in that semester.</div>
                <div class="ay-form-row">
                    <div class="ay-form-group">
                        <label class="ay-form-label">Semester 1 Active</label>
                        <asp:DropDownList ID="ddlSem1Active" runat="server" CssClass="ay-form-input">
                            <asp:ListItem Text="No — Closed" Value="No" Selected="True" />
                            <asp:ListItem Text="Yes — Open" Value="Yes" />
                        </asp:DropDownList>
                    </div>
                    <div class="ay-form-group">
                        <label class="ay-form-label">Semester 2 Active</label>
                        <asp:DropDownList ID="ddlSem2Active" runat="server" CssClass="ay-form-input">
                            <asp:ListItem Text="No — Closed" Value="No" Selected="True" />
                            <asp:ListItem Text="Yes — Open" Value="Yes" />
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="ay-form-row" style="margin-bottom:0;">
                    <div class="ay-form-group">
                        <label class="ay-form-label">Semester 3 Active</label>
                        <asp:DropDownList ID="ddlSem3Active" runat="server" CssClass="ay-form-input">
                            <asp:ListItem Text="No — Closed" Value="No" Selected="True" />
                            <asp:ListItem Text="Yes — Open" Value="Yes" />
                        </asp:DropDownList>
                    </div>
                    <div class="ay-form-group"></div>
                </div>
            </div>
            <div class="ay-form-row">
                <div class="ay-form-group ay-form-group--full">
                    <label class="ay-form-label">Description (optional)</label>
                    <asp:TextBox ID="txtDescription" runat="server" CssClass="ay-form-input" placeholder="e.g. Extended semester due to COVID" />
                </div>
            </div>

            <%-- ── Admission ─────────────────────────────────────
                 Writes apply_intakes, which the student portal's application wizard and
                 the v2 API already read when they build the Intake / Entry Year list. --%>
            <div style="border:1px solid #e4e8f0;border-radius:4px;padding:14px 16px;margin-bottom:14px;">
                <div style="font-size:11px;font-weight:700;color:#555;text-transform:uppercase;letter-spacing:.5px;margin-bottom:10px;">Admission</div>
                <div style="font-size:11px;color:#888;margin-bottom:12px;">
                    Whether applicants may apply <strong>for this academic year</strong> in the student
                    portal. Closing it removes the year from the applicant's Intake / Entry Year list;
                    applications already submitted are not affected.
                </div>
                <label style="display:flex;align-items:center;gap:8px;font-size:12px;color:#555;cursor:pointer;margin-bottom:12px;">
                    <asp:CheckBox ID="chkAdmissionOpen" runat="server" /> Accept applications for this year
                </label>
                <div class="ay-form-row">
                    <div class="ay-form-group">
                        <label class="ay-form-label">Applications open from (optional)</label>
                        <asp:TextBox ID="txtAdmFrom" runat="server" CssClass="ay-form-input" TextMode="Date" />
                    </div>
                    <div class="ay-form-group">
                        <label class="ay-form-label">Applications close on (optional)</label>
                        <asp:TextBox ID="txtAdmTo" runat="server" CssClass="ay-form-input" TextMode="Date" />
                    </div>
                </div>
                <div style="font-size:11px;color:#888;margin-top:8px;">
                    Leave both blank to stay open until you close it here. Outside the window the year
                    is hidden from applicants even while it is ticked.
                </div>
                <div id="admNote"><asp:Literal ID="litAdmCurrent" runat="server" /></div>
            </div>

            <!-- Set as current checkboxes -->
            <div style="display:flex;gap:20px;margin-top:6px;">
                <label style="display:flex;align-items:center;gap:6px;font-size:12px;color:#555;cursor:pointer;">
                    <asp:CheckBox ID="chkSetCurrentAcad" runat="server" /> Set as Current Academic Year
                </label>
                <label style="display:flex;align-items:center;gap:6px;font-size:12px;color:#555;cursor:pointer;">
                    <asp:CheckBox ID="chkSetCurrentFin" runat="server" /> Set as Current Financial Year
                </label>
            </div>
        </div>
        <div class="ay-modal__footer">
            <button type="button" class="ay-btn" onclick="closeModal();">Cancel</button>
            <asp:Button ID="btnSave" runat="server" Text="Save Academic Year" CssClass="ay-btn ay-btn--primary"
                OnClick="btnSave_Click" />
        </div>
    </div>
</div>

<!-- ======= SET-CURRENT MODAL ======= -->
<div class="ay-modal-overlay" id="setCurrentOverlay">
    <div class="ay-modal" style="width:420px;">
        <div class="ay-modal__header">
            <span class="ay-modal__title">Set Current Year</span>
            <button type="button" class="ay-modal__close" onclick="closeSetCurrentModal();">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
            </button>
        </div>
        <div class="ay-modal__body">
            <asp:HiddenField ID="hfSetCurrentYear" runat="server" Value="" />
            <p style="font-size:13px;color:#333;margin:0 0 14px;">
                You are about to set <strong id="lblSetYear"></strong> as:
            </p>
            <div style="display:flex;flex-direction:column;gap:10px;">
                <label style="display:flex;align-items:center;gap:8px;font-size:13px;color:#333;cursor:pointer;">
                    <asp:CheckBox ID="chkSetAcad" runat="server" /> Current Academic Year
                </label>
                <label style="display:flex;align-items:center;gap:8px;font-size:13px;color:#333;cursor:pointer;">
                    <asp:CheckBox ID="chkSetFin" runat="server" /> Current Financial Year
                </label>
            </div>
        </div>
        <div class="ay-modal__footer">
            <button type="button" class="ay-btn" onclick="closeSetCurrentModal();">Cancel</button>
            <asp:Button ID="btnSetCurrent" runat="server" Text="Apply" CssClass="ay-btn ay-btn--success"
                OnClick="btnSetCurrent_Click" />
        </div>
    </div>
</div>

<script type="text/javascript">
    function updateYearPreview() {
        var sy = document.getElementById('<%= txtStartYear.ClientID %>');
        var ey = document.getElementById('<%= txtEndYear.ClientID %>');
        var preview = document.getElementById('yearPreview');
        var v = (sy.value || '').replace(/\D/g, '');
        if (v.length === 4) {
            var n = parseInt(v, 10);
            ey.value = (n + 1).toString();
            preview.textContent = v + '/' + (n + 1);
            // Auto-fill dates
            var sd = document.getElementById('<%= txtStartDate.ClientID %>');
            var ed = document.getElementById('<%= txtEndDate.ClientID %>');
            if (!sd.value) sd.value = v + '-08-01';
            if (!ed.value) ed.value = (n + 1) + '-07-31';
        } else {
            ey.value = '';
            preview.textContent = '\u2014';
        }
    }

    function openAddModal() {
        document.getElementById('<%= hfEditId.ClientID %>').value = '';
        document.getElementById('<%= txtStartYear.ClientID %>').value = '';
        document.getElementById('<%= txtEndYear.ClientID %>').value = '';
        document.getElementById('<%= txtStartDate.ClientID %>').value = '';
        document.getElementById('<%= txtEndDate.ClientID %>').value = '';
        document.getElementById('<%= txtDescription.ClientID %>').value = '';
        document.getElementById('<%= chkAdmissionOpen.ClientID %>').checked = false;
        document.getElementById('<%= txtAdmFrom.ClientID %>').value = '';
        document.getElementById('<%= txtAdmTo.ClientID %>').value = '';
        document.getElementById('admNote').innerHTML = '';
        document.getElementById('yearPreview').textContent = '\u2014';
        document.getElementById('modalTitle').textContent = 'Add Academic Year';
        document.getElementById('modalOverlay').classList.add('open');
    }

    function editYear(id) {
        // Trigger a postback to load the year data into the modal
        __doPostBack('EditYear', id);
    }

    function setCurrentYear(acadyear) {
        document.getElementById('<%= hfSetCurrentYear.ClientID %>').value = acadyear;
        document.getElementById('lblSetYear').textContent = acadyear;
        document.getElementById('setCurrentOverlay').classList.add('open');
    }

    function closeModal() {
        document.getElementById('modalOverlay').classList.remove('open');
    }

    function closeSetCurrentModal() {
        document.getElementById('setCurrentOverlay').classList.remove('open');
    }


    // Close modals on overlay click
    document.addEventListener('click', function (e) {
        if (e.target.id === 'modalOverlay') closeModal();
        if (e.target.id === 'setCurrentOverlay') closeSetCurrentModal();
    });

    // Close modals on Escape
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { closeModal(); closeSetCurrentModal(); }
    });
</script>

</asp:Content>