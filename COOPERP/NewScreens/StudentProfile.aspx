<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="StudentProfile.aspx.cs" Inherits="COOPERP_NewScreens_StudentProfile" Title="Student Profile - Campus Dynamics" %>
<%@ Register Src="~/COOPERP/NewScreens/DcStudentBanner.ascx" TagName="DcStudentBanner" TagPrefix="dc" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
.sp-header { background:#05275C; padding:14px 20px 12px; margin-bottom:20px; border-bottom:3px solid #041d45; display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:10px; }
.sp-header__left { display:flex; align-items:center; gap:12px; }
.sp-header__icon { width:38px; height:38px; background:rgba(255,255,255,.12); display:flex; align-items:center; justify-content:center; border-radius:4px; }
.sp-header__title { font-size:16px; font-weight:700; color:#fff; margin:0; }
.sp-header__sub { font-size:12px; color:rgba(255,255,255,.7); margin-top:2px; }
.sp-header__actions { display:flex; gap:8px; }
.sp-btn { display:inline-flex; align-items:center; gap:6px; padding:7px 14px; font-size:12px; font-weight:600; border-radius:4px; text-decoration:none; cursor:pointer; border:none; }
.sp-btn--outline { background:transparent; color:#fff; border:1px solid rgba(255,255,255,.4); }
.sp-btn--outline:hover { background:rgba(255,255,255,.1); color:#fff; }
.sp-btn--primary { background:#174DA4; color:#fff; }
.sp-btn--sm { padding:5px 10px; font-size:11px; }

.sp-error { background:#f8d7da; color:#721c24; border:1px solid #f5c6cb; border-radius:6px; padding:16px 20px; margin-bottom:16px; font-size:13px; }

/* Hero banner */
.sp-hero { display:flex; align-items:center; gap:18px; background:#fff; border:1px solid #e0e0e0; border-radius:8px; padding:18px 22px; margin-bottom:16px; box-shadow:0 1px 4px rgba(0,0,0,.05); flex-wrap:wrap; }
.sp-photo-placeholder { width:70px; height:70px; border-radius:50%; border:3px solid #174DA4; display:flex; align-items:center; justify-content:center; background:#e8f0fe; color:#174DA4; font-size:26px; font-weight:700; flex-shrink:0; }
.sp-hero__info { flex:1; min-width:0; }
.sp-hero__name { font-size:18px; font-weight:700; color:#111; margin-bottom:3px; }
.sp-hero__regno { font-size:12px; font-weight:600; color:#174DA4; background:#e8f0fe; display:inline-block; padding:2px 12px; border-radius:4px; margin-bottom:6px; }
.sp-badge { display:inline-block; padding:3px 10px; border-radius:4px; font-size:10px; font-weight:700; text-transform:uppercase; letter-spacing:.3px; }
.sp-badge--active { background:#d4edda; color:#155724; }
.sp-badge--inactive { background:#f8d7da; color:#721c24; }
.sp-badge--alumni { background:#cce5ff; color:#004085; }
.sp-badge--pending { background:#fff3cd; color:#856404; }
.sp-hero__meta { display:flex; flex-wrap:wrap; gap:14px; margin-top:8px; font-size:11px; color:#555; }
.sp-hero__meta span strong { color:#222; }
.sp-hero__links { display:flex; gap:8px; flex-wrap:wrap; align-self:center; }

/* Section cards */
.sp-section { background:#fff; border:1px solid #e0e0e0; border-radius:8px; margin-bottom:14px; box-shadow:0 1px 3px rgba(0,0,0,.04); overflow:hidden; }
.sp-section__hdr { display:flex; align-items:center; gap:8px; padding:10px 16px; background:#f4f6fb; border-bottom:1px solid #e0e0e0; }
.sp-section__icon { color:#174DA4; }
.sp-section__title { font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:.5px; color:#174DA4; }

/* Two-column grid for fields */
.sp-fields { display:grid; grid-template-columns:1fr 1fr; gap:0; }
@media(max-width:600px){ .sp-fields { grid-template-columns:1fr; } }
.sp-field { display:flex; flex-direction:column; padding:9px 16px; border-bottom:1px solid #f0f0f0; border-right:1px solid #f0f0f0; }
.sp-field:nth-child(2n) { border-right:none; }
.sp-field:last-child, .sp-field:nth-last-child(2) { border-bottom:none; }
.sp-field__label { font-size:10px; font-weight:700; text-transform:uppercase; letter-spacing:.3px; color:#888; margin-bottom:2px; }
.sp-field__value { font-size:13px; color:#222; font-weight:500; word-break:break-word; }
.sp-field__value:empty::after { content:'—'; color:#ccc; }
.sp-field--full { grid-column:1/-1; border-right:none; }

/* Reg table */
.sp-reg-table { width:100%; border-collapse:collapse; font-size:12px; }
.sp-reg-table th { background:#f4f6fb; padding:9px 12px; text-align:left; font-size:10px; text-transform:uppercase; color:#555; border-bottom:2px solid #174DA4; font-weight:700; letter-spacing:.3px; }
.sp-reg-table td { padding:9px 12px; border-bottom:1px solid #eee; }
.sp-reg-table tr:last-child td { border-bottom:none; }
.sp-reg-table tr:hover td { background:#f8f9fa; }
.sp-status-pill { display:inline-block; padding:2px 8px; border-radius:3px; font-size:10px; font-weight:700; text-transform:uppercase; }
.sp-status-registered { background:#d4edda; color:#155724; }
.sp-status-unregistered { background:#f8d7da; color:#721c24; }
.sp-status-cleared { background:#cce5ff; color:#004085; }
.sp-status-other { background:#e2e3e5; color:#383d41; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- Page Header -->
<div class="sp-header">
    <div class="sp-header__left">
        <div class="sp-header__icon">
            <svg width="20" height="20" fill="none" stroke="#fff" stroke-width="2" viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg>
        </div>
        <div>
            <div class="sp-header__title">Student Profile</div>
            <div class="sp-header__sub"><asp:Literal ID="litHeaderSub" runat="server">Loading...</asp:Literal></div>
        </div>
    </div>
    <div class="sp-header__actions">
        <a href="NewStudentRegistration.aspx" class="sp-btn sp-btn--outline">
            <svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>
            New Student
        </a>
        <a href="javascript:history.back()" class="sp-btn sp-btn--outline">← Back</a>
    </div>
</div>

<!-- Error panel -->
<asp:Panel ID="pnlError" runat="server" Visible="false">
    <div class="sp-error"><asp:Literal ID="litError" runat="server" /></div>
</asp:Panel>

<!-- Main content -->
<asp:Panel ID="pnlMain" runat="server" Visible="false">

<!-- Hero banner -->
<div class="sp-hero">
    <div class="sp-photo-placeholder" id="photoPlaceholder" runat="server"></div>
    <div class="sp-hero__info">
        <div class="sp-hero__name"><asp:Literal ID="litFullName" runat="server" /></div>
        <div><span class="sp-hero__regno"><asp:Literal ID="litRegNo" runat="server" /></span></div>
        <div><asp:Literal ID="litStatusBadge" runat="server" /></div>
        <div class="sp-hero__meta">
            <span><strong>Programme:</strong> <asp:Literal ID="litProg" runat="server" /></span>
            <span><strong>Year:</strong> <asp:Literal ID="litEntryYear" runat="server" /></span>
            <span><strong>Session:</strong> <asp:Literal ID="litSession" runat="server" /></span>
            <span><strong>Intake:</strong> <asp:Literal ID="litIntake" runat="server" /></span>
            <span><strong>Campus:</strong> <asp:Literal ID="litCampus" runat="server" /></span>
        </div>
    </div>
    <div class="sp-hero__links">
        <a id="aLedger" runat="server" href="#" class="sp-btn sp-btn--primary sp-btn--sm">View Ledger</a>
        <a id="aReceipts" runat="server" href="#" class="sp-btn sp-btn--outline" style="color:#174DA4;border-color:#174DA4;">Receipts</a>
        <a id="aDocs" runat="server" href="#" class="sp-btn sp-btn--outline" style="color:#174DA4;border-color:#174DA4;">Documents</a>
    </div>
</div>
<dc:DcStudentBanner ID="dcBanner" runat="server" />

<!-- Section: Biographical Information -->
<div class="sp-section">
    <div class="sp-section__hdr">
        <svg class="sp-section__icon" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg>
        <span class="sp-section__title">Biographical Information</span>
    </div>
    <div class="sp-fields">
        <div class="sp-field"><div class="sp-field__label">First Name</div><div class="sp-field__value"><asp:Literal ID="litFirstName" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Other Name(s)</div><div class="sp-field__value"><asp:Literal ID="litOtherName" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Gender</div><div class="sp-field__value"><asp:Literal ID="litGender" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Date of Birth</div><div class="sp-field__value"><asp:Literal ID="litDOB" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Nationality</div><div class="sp-field__value"><asp:Literal ID="litNationality" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Religion</div><div class="sp-field__value"><asp:Literal ID="litReligion" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">National ID</div><div class="sp-field__value"><asp:Literal ID="litNationalID" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Home District</div><div class="sp-field__value"><asp:Literal ID="litDistrict" runat="server" /></div></div>
    </div>
</div>

<!-- Section: Contact Information -->
<div class="sp-section">
    <div class="sp-section__hdr">
        <svg class="sp-section__icon" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 12 19.79 19.79 0 0 1 1.63 3.38 2 2 0 0 1 3.6 1h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L7.91 8.6a16 16 0 0 0 6 6l.96-.96a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 21.73 16z"/></svg>
        <span class="sp-section__title">Contact Information</span>
    </div>
    <div class="sp-fields">
        <div class="sp-field"><div class="sp-field__label">Phone</div><div class="sp-field__value"><asp:Literal ID="litPhone" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Email</div><div class="sp-field__value"><asp:Literal ID="litEmail" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Next of Kin</div><div class="sp-field__value"><asp:Literal ID="litNOK" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">NOK Relationship</div><div class="sp-field__value"><asp:Literal ID="litNOKRel" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">NOK Phone</div><div class="sp-field__value"><asp:Literal ID="litNOKPhone" runat="server" /></div></div>
    </div>
</div>

<!-- Section: Academic Information -->
<div class="sp-section">
    <div class="sp-section__hdr">
        <svg class="sp-section__icon" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>
        <span class="sp-section__title">Academic Information</span>
    </div>
    <div class="sp-fields">
        <div class="sp-field sp-field--full"><div class="sp-field__label">Programme</div><div class="sp-field__value"><asp:Literal ID="litProgFull" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Programme Code</div><div class="sp-field__value"><asp:Literal ID="litProgCode" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Specialisation</div><div class="sp-field__value"><asp:Literal ID="litSpec" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Entry Method</div><div class="sp-field__value"><asp:Literal ID="litEntryMethod" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Entry Year</div><div class="sp-field__value"><asp:Literal ID="litEntryYearFull" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Intake</div><div class="sp-field__value"><asp:Literal ID="litIntakeFull" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Session</div><div class="sp-field__value"><asp:Literal ID="litSessionFull" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Duration</div><div class="sp-field__value"><asp:Literal ID="litDuration" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Graduation System</div><div class="sp-field__value"><asp:Literal ID="litGradSys" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Sponsor</div><div class="sp-field__value"><asp:Literal ID="litSponsor" runat="server" /></div></div>
        <div class="sp-field"><div class="sp-field__label">Sponsor Contact</div><div class="sp-field__value"><asp:Literal ID="litSponsorContact" runat="server" /></div></div>
    </div>
</div>

<!-- Section: Registration History -->
<div class="sp-section">
    <div class="sp-section__hdr">
        <svg class="sp-section__icon" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
        <span class="sp-section__title">Registration History</span>
    </div>
    <asp:Panel ID="pnlNoReg" runat="server" Visible="false">
        <div style="padding:16px; color:#888; font-size:12px;">No registration records found.</div>
    </asp:Panel>
    <asp:Panel ID="pnlRegTable" runat="server" Visible="false">
        <div style="overflow-x:auto;">
        <table class="sp-reg-table">
            <thead><tr>
                <th>Academic Year</th>
                <th>Semester</th>
                <th>Study Year</th>
                <th>Status</th>
                <th>Registered By</th>
            </tr></thead>
            <tbody><asp:Literal ID="litRegRows" runat="server" /></tbody>
        </table>
        </div>
    </asp:Panel>
</div>

</asp:Panel>

</asp:Content>
