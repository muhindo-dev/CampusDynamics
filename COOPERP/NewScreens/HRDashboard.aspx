<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master"
    AutoEventWireup="true" CodeFile="HRDashboard.aspx.cs"
    Inherits="COOPERP_NewScreens_HRDashboard"
    Title="HR &amp; Payroll Dashboard - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ---- HR Dashboard ---- */
.hr-stats-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px;
    margin-bottom: 16px;
}
@media(max-width:1100px){ .hr-stats-grid { grid-template-columns: repeat(2,1fr); } }
@media(max-width:580px) { .hr-stats-grid { grid-template-columns: 1fr; } }

.hr-kpi {
    background: #fff;
    border: 1px solid #e0e5ed;
    border-top: 3px solid transparent;
    padding: 14px 16px;
    display: flex;
    align-items: center;
    gap: 12px;
}
.hr-kpi--blue   { border-top-color: #174DA4; }
.hr-kpi--green  { border-top-color: #28a745; }
.hr-kpi--amber  { border-top-color: #f59e0b; }
.hr-kpi--red    { border-top-color: #dc3545; }
.hr-kpi--purple { border-top-color: #7c3aed; }
.hr-kpi--teal   { border-top-color: #0d9488; }
.hr-kpi__icon {
    width: 40px; height: 40px;
    border-radius: 4px;
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0;
}
.hr-kpi--blue   .hr-kpi__icon { background: #e8f0fe; color: #174DA4; }
.hr-kpi--green  .hr-kpi__icon { background: #d4edda; color: #155724; }
.hr-kpi--amber  .hr-kpi__icon { background: #fff3cd; color: #856404; }
.hr-kpi--red    .hr-kpi__icon { background: #f8d7da; color: #721c24; }
.hr-kpi--purple .hr-kpi__icon { background: #ede9fe; color: #5b21b6; }
.hr-kpi--teal   .hr-kpi__icon { background: #ccfbf1; color: #0d9488; }
.hr-kpi__body { flex: 1; min-width: 0; }
.hr-kpi__value { font-size: 22px; font-weight: 700; color: #1a1a2e; line-height: 1.15; }
.hr-kpi__label { font-size: 10px; color: #888; text-transform: uppercase; letter-spacing: 0.3px; margin-top: 2px; }

.hr-section {
    background: #fff;
    border: 1px solid #e0e5ed;
    margin-bottom: 16px;
}
.hr-section__hdr {
    padding: 11px 16px;
    border-bottom: 1px solid #e0e5ed;
    font-size: 12px;
    font-weight: 700;
    color: #333;
    display: flex;
    align-items: center;
    gap: 8px;
    text-transform: uppercase;
    letter-spacing: 0.3px;
}
.hr-section__hdr svg { flex-shrink: 0; color: #666; }
.hr-section__body { padding: 14px 16px; }

.hr-two-col {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 16px;
}
@media(max-width:900px){ .hr-two-col { grid-template-columns: 1fr; } }

.hr-table { width: 100%; border-collapse: collapse; font-size: 12px; }
.hr-table th { text-align: left; padding: 7px 10px; border-bottom: 2px solid #e0e5ed; color: #666; font-weight: 600; text-transform: uppercase; font-size: 10px; letter-spacing: 0.3px; }
.hr-table td { padding: 7px 10px; border-bottom: 1px solid #f5f7fa; color: #333; vertical-align: middle; }
.hr-table tr:last-child td { border-bottom: none; }
.hr-table tr:hover td { background: #f9fbff; }

.hr-badge { display: inline-block; padding: 2px 8px; border-radius: 3px; font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.3px; }
.hr-badge--pending   { background: #fff3cd; color: #856404; }
.hr-badge--processed { background: #d4edda; color: #155724; }
.hr-badge--cancelled { background: #f8d7da; color: #721c24; }
.hr-badge--approved  { background: #d4edda; color: #155724; }
.hr-badge--rejected  { background: #f8d7da; color: #721c24; }
.hr-badge--valid     { background: #cff4fc; color: #055160; }
.hr-badge--expiring  { background: #fff3cd; color: #664d03; }
.hr-badge--expired   { background: #f8d7da; color: #721c24; }
.hr-badge--settled   { background: #e2e3e5; color: #383d41; }

.hr-quicklinks {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 10px;
}
@media(max-width:1000px){ .hr-quicklinks { grid-template-columns: repeat(2,1fr); } }
@media(max-width:560px)  { .hr-quicklinks { grid-template-columns: 1fr; } }

.hr-ql {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 12px 14px;
    border: 1px solid #e0e5ed;
    background: #f9fbff;
    text-decoration: none;
    color: #1a1a2e;
    transition: background 0.15s, border-color 0.15s;
}
.hr-ql:hover { background: #eef2fb; border-color: #174DA4; }
.hr-ql__icon {
    width: 34px; height: 34px;
    border-radius: 4px;
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0;
    background: #e8f0fe;
    color: #174DA4;
}
.hr-ql__icon svg { width: 16px; height: 16px; }
.hr-ql__title { font-size: 12px; font-weight: 600; }
.hr-ql__sub   { font-size: 10px; color: #888; margin-top: 1px; }

.hr-alert { padding: 9px 13px; border-left: 3px solid; margin-bottom: 8px; font-size: 12px; }
.hr-alert:last-child { margin-bottom: 0; }
.hr-alert--warn  { border-color: #f59e0b; background: #fffbeb; color: #92400e; }
.hr-alert--info  { border-color: #174DA4; background: #eff6ff; color: #1e3a8a; }
.hr-alert--ok    { border-color: #28a745; background: #f0fdf4; color: #14532d; }
.hr-alert--error { border-color: #dc3545; background: #fef5f5; color: #7f1d1d; }

.hr-empty { text-align: center; padding: 24px; color: #aaa; font-size: 12px; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- Page header -->
<div class="pr-page-header" style="display:flex;align-items:center;justify-content:space-between;padding:12px 18px;background:#fff;border-bottom:2px solid #174DA4;margin-bottom:14px;">
    <div style="display:flex;align-items:center;gap:10px;">
        <div style="width:36px;height:36px;background:#eef2fb;border-radius:6px;display:flex;align-items:center;justify-content:center;">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#174DA4" stroke-width="2">
                <rect x="2" y="7" width="20" height="14" rx="2"/><path d="M16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2"/>
                <line x1="12" y1="12" x2="12" y2="16"/><line x1="10" y1="14" x2="14" y2="14"/>
            </svg>
        </div>
        <div>
            <div style="font-size:15px;font-weight:700;color:#1a1a2e;">HR &amp; Payroll Dashboard</div>
            <div style="font-size:11px;color:#666;">Overview of all human resource and payroll activity</div>
        </div>
    </div>
    <div style="font-size:11px;color:#888;">
        <asp:Label ID="lblCurrentPeriod" runat="server" Text="" />
    </div>
</div>

<!-- KPI Cards -->
<div class="hr-stats-grid">
    <div class="hr-kpi hr-kpi--blue">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblTotalEmployees" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Active Employees</div>
        </div>
    </div>
    <div class="hr-kpi hr-kpi--green">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblActiveContracts" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Valid Contracts</div>
        </div>
    </div>
    <div class="hr-kpi hr-kpi--amber">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblPendingPayslips" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Payslips Awaiting Approval</div>
        </div>
    </div>
    <div class="hr-kpi hr-kpi--purple">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblPayrollsThisYear" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Payroll Runs This Year</div>
        </div>
    </div>
    <div class="hr-kpi hr-kpi--teal">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblNetPayThisMonth" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Net Pay — Current Month</div>
        </div>
    </div>
    <div class="hr-kpi hr-kpi--amber">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblPendingAllowances" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Pending Allowances</div>
        </div>
    </div>
    <div class="hr-kpi hr-kpi--red">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblPendingDeductions" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Pending Deductions</div>
        </div>
    </div>
    <div class="hr-kpi hr-kpi--red">
        <div class="hr-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
        </div>
        <div class="hr-kpi__body">
            <div class="hr-kpi__value"><asp:Label ID="lblExpiringContracts" runat="server" Text="0" /></div>
            <div class="hr-kpi__label">Contracts Expiring (30 days)</div>
        </div>
    </div>
</div>

<!-- Two-column: Recent Payrolls + Alerts -->
<div class="hr-two-col">

    <div class="hr-section">
        <div class="hr-section__hdr">
            <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
            Recent Payroll Runs
        </div>
        <div class="hr-section__body" style="padding:0;">
            <asp:Literal ID="litRecentPayrolls" runat="server" />
        </div>
    </div>

    <div class="hr-section">
        <div class="hr-section__hdr">
            <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
            Alerts &amp; Notices
        </div>
        <div class="hr-section__body">
            <asp:Literal ID="litAlerts" runat="server" />
        </div>
    </div>

</div>

<!-- Two-column: Expiring Contracts + Pending Payslips -->
<div class="hr-two-col">

    <div class="hr-section">
        <div class="hr-section__hdr">
            <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
            Contracts Expiring Within 30 Days
        </div>
        <div class="hr-section__body" style="padding:0;">
            <asp:Literal ID="litExpiringContracts" runat="server" />
        </div>
    </div>

    <div class="hr-section">
        <div class="hr-section__hdr">
            <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
            Payslips Pending Approval
        </div>
        <div class="hr-section__body" style="padding:0;">
            <asp:Literal ID="litPendingPayslips" runat="server" />
        </div>
    </div>

</div>

<!-- Quick Access Links -->
<div class="hr-section">
    <div class="hr-section__hdr">
        <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"/></svg>
        Quick Access
    </div>
    <div class="hr-section__body">
        <div class="hr-quicklinks">
            <a href="HREmployees.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#e8f0fe;color:#174DA4;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
                </div>
                <div><div class="hr-ql__title">Employee Directory</div><div class="hr-ql__sub">View &amp; manage staff records</div></div>
            </a>
            <a href="HRContracts.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#d4edda;color:#155724;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
                </div>
                <div><div class="hr-ql__title">Contracts</div><div class="hr-ql__sub">Employment contracts &amp; pay scales</div></div>
            </a>
            <a href="ContractRenewals.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#e8f0fe;color:#05275C;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>
                </div>
                <div><div class="hr-ql__title">Contract Renewals</div><div class="hr-ql__sub">Applications, Council &amp; new contracts</div></div>
            </a>
            <a href="HRPayroll.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#ede9fe;color:#5b21b6;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>
                </div>
                <div><div class="hr-ql__title">Payroll Management</div><div class="hr-ql__sub">Create &amp; process payroll runs</div></div>
            </a>
            <a href="HRPayslips.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#ccfbf1;color:#0d9488;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="7" width="20" height="14" rx="2"/><path d="M16 7V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v2"/></svg>
                </div>
                <div><div class="hr-ql__title">Payslips</div><div class="hr-ql__sub">View &amp; approve employee payslips</div></div>
            </a>
            <a href="HRAllowances.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#fff3cd;color:#856404;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/></svg>
                </div>
                <div><div class="hr-ql__title">Allowance Records</div><div class="hr-ql__sub">Ad-hoc allowances &amp; bonuses</div></div>
            </a>
            <a href="HRDeductions.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#f8d7da;color:#721c24;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="8" y1="12" x2="16" y2="12"/></svg>
                </div>
                <div><div class="hr-ql__title">Deduction Records</div><div class="hr-ql__sub">Loans, advances &amp; deductions</div></div>
            </a>
            <a href="HRLeaveManagement.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#e0f2f1;color:#00695c;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
                </div>
                <div><div class="hr-ql__title">Leave Management</div><div class="hr-ql__sub">Leave requests &amp; balances</div></div>
            </a>
            <a href="HRConfig.aspx" class="hr-ql">
                <div class="hr-ql__icon" style="background:#e2e3e5;color:#383d41;">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>
                </div>
                <div><div class="hr-ql__title">Payroll &amp; Tax Config</div><div class="hr-ql__sub">PAYE, NSSF, Kabaka rates</div></div>
            </a>
        </div>
    </div>
</div>

</asp:Content>
