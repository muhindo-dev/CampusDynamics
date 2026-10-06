<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="HREmployees.aspx.cs" Inherits="COOPERP_NewScreens_HREmployees" Title="Employees - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.js") %>?v=1"></script>
<style>
a.hr-kpi.em-kpi--on { border-color: var(--hr-navy); }
.hr-kpi__value.em-kpi__value--warn { color: var(--hr-warn); }
.em-sort { color: inherit; text-decoration: none; display: inline-flex; align-items: center; gap: 3px; cursor: pointer; }
.em-sort:hover { color: var(--hr-navy); }
.em-sort svg { width: 10px; height: 10px; }
.em-group { grid-column: 1 / -1; font-size: 11px; font-weight: 700; color: var(--hr-navy); text-transform: uppercase; letter-spacing: .4px; padding-bottom: 4px; border-bottom: 1px solid var(--hr-border); margin: 16px 0 10px; }
.em-group:first-child { margin-top: 0; }
.em-pick .hr-input { margin-bottom: 4px; }
.em-prof-head { display: flex; gap: 14px; align-items: center; margin-bottom: 14px; }
.em-photo { width: 64px; height: 64px; object-fit: cover; border: 1px solid var(--hr-border); background: var(--hr-surface); flex: 0 0 auto; }
.em-prof-name { font-size: 15px; font-weight: 700; color: var(--hr-text); }
.em-prof-pane { display: none; }
.em-prof-pane.is-on { display: block; }
.em-code-row { display: flex; gap: 8px; }
.em-code-row .hr-input { flex: 1; }
.em-creds { font-family: Consolas, "Courier New", monospace; }
.em-log { margin: 6px 0 0; padding-left: 18px; font-size: 11px; color: var(--hr-text-2); line-height: 1.7; }
.em-preview { width: 110px; height: 110px; object-fit: cover; border: 1px solid var(--hr-border); display: block; margin: 0 auto 12px; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<asp:HiddenField ID="hdnEditEmpID"      runat="server" />
<asp:HiddenField ID="hdnDeleteEmpID"    runat="server" />
<asp:HiddenField ID="hfSupervisorID"    runat="server" />
<asp:HiddenField ID="hfReviewerID"      runat="server" />
<asp:HiddenField ID="hfOriginalEmpCode" runat="server" />
<asp:Button ID="btnAddEmployee"    runat="server" OnClick="btnAddEmployee_Click"    style="display:none" />
<asp:Button ID="btnEditEmployee"   runat="server" OnClick="btnEditEmployee_Click"   style="display:none" />
<asp:Button ID="btnDeleteEmployee" runat="server" OnClick="btnDeleteEmployee_Click" style="display:none" />

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Employees</div>
            <div class="hr-header__sub">Staff records, profiles and sign-in</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openAddModal()">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            New employee
        </button>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab hr-tab--active" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a><a class="hr-tab" href="ProfileChangeRequests.aspx">Profile requests</a></div>

<asp:Literal ID="litKpis" runat="server" />

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label>Search</label>
        <asp:TextBox ID="txtSearch" runat="server" CssClass="hr-input" placeholder="Name, staff number, email or phone" />
    </div>
    <div class="hr-filter">
        <label>Status</label>
        <asp:DropDownList ID="ddlFilterStatus" runat="server" CssClass="hr-select" onchange="applyFilters()">
            <asp:ListItem Value="" Text="All staff" />
            <asp:ListItem Value="ACTIVE" Text="Active" />
            <asp:ListItem Value="NOVALID" Text="No valid contract" />
            <asp:ListItem Value="DUP" Text="Duplicate staff numbers" />
        </asp:DropDownList>
    </div>
    <div class="hr-filter">
        <label>Category</label>
        <asp:DropDownList ID="ddlFilterType" runat="server" CssClass="hr-select" onchange="applyFilters()" />
    </div>
    <div class="hr-filter">
        <label>Department</label>
        <asp:DropDownList ID="ddlFilterDept" runat="server" CssClass="hr-select" onchange="applyFilters()" />
    </div>
    <div class="hr-filter">
        <label>Station</label>
        <asp:DropDownList ID="ddlFilterStation" runat="server" CssClass="hr-select" onchange="applyFilters()" />
    </div>
    <div class="hr-filter" style="flex-basis:90px;min-width:80px">
        <label>Per page</label>
        <asp:DropDownList ID="ddlPageSize" runat="server" CssClass="hr-select" onchange="applyFilters()">
            <asp:ListItem Value="25" Text="25" />
            <asp:ListItem Value="50" Text="50" Selected="True" />
            <asp:ListItem Value="100" Text="100" />
            <asp:ListItem Value="200" Text="200" />
        </asp:DropDownList>
    </div>
    <div class="hr-filters__actions">
        <button type="button" class="hr-btn hr-btn--primary" onclick="applyFilters()">Apply</button>
        <a class="hr-btn hr-btn--secondary" href="HREmployees.aspx">Reset</a>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Staff directory</div>
        <div class="hr-row">
            <button type="button" class="hr-btn hr-btn--secondary hr-btn--sm" onclick="exportEmployees('xlsx')">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
                Staff register
            </button>
            <button type="button" class="hr-btn hr-btn--secondary hr-btn--sm" onclick="exportEmployees('csv')">CSV</button>
        </div>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th><%= SortHead("name", "Employee") %></th>
                    <th><%= SortHead("code", "Staff No") %></th>
                    <th><%= SortHead("type", "Category") %></th>
                    <th><%= SortHead("dept", "Department") %></th>
                    <th><%= SortHead("job", "Position") %></th>
                    <th><%= SortHead("contractend", "Contract") %></th>
                    <th class="hr-num"><%= SortHead("pay", "Basic pay (UGX)") %></th>
                    <th></th>
                </tr>
            </thead>
            <tbody>
                <asp:Literal ID="litGridBody" runat="server" />
            </tbody>
        </table>
    </div>
    <div class="hr-card__foot">
        <span><asp:Literal ID="litPagerInfo" runat="server" /></span>
        <asp:Literal ID="litPager" runat="server" />
    </div>
</div>

<!-- Profile -->
<div class="hr-modal" id="profileModal">
    <div class="hr-modal__box hr-modal__box--wide">
        <div class="hr-modal__head"><span>Employee profile</span><button type="button" class="hr-modal__close" onclick="closeModal('profileModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="em-prof-head">
                <img id="epPhoto" class="em-photo" src="../staffimages/default.jpg" onerror="this.onerror=null;this.src='../staffimages/default.jpg'" alt="" />
                <div style="min-width:0">
                    <div class="em-prof-name" id="epName">Loading</div>
                    <div class="hr-muted" id="epMeta"></div>
                    <div style="margin-top:4px" id="epBadges"></div>
                </div>
            </div>
            <div class="hr-subtabs" id="epTabs">
                <button type="button" class="hr-subtab hr-subtab--active" data-tab="details">Details</button>
                <button type="button" class="hr-subtab" data-tab="contracts">Contracts</button>
                <button type="button" class="hr-subtab" data-tab="leave">Leave</button>
                <button type="button" class="hr-subtab" data-tab="payroll">Payroll</button>
            </div>
            <div class="em-prof-pane is-on" id="epPane_details"><div class="hr-loading">Loading</div></div>
            <div class="em-prof-pane" id="epPane_contracts"></div>
            <div class="em-prof-pane" id="epPane_leave"></div>
            <div class="em-prof-pane" id="epPane_payroll"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--danger" onclick="confirmDelete()">Delete employee</button>
            <span class="hr-spacer"></span>
            <button type="button" class="hr-btn hr-btn--secondary" onclick="openResetLogin()">Fix account</button>
            <button type="button" class="hr-btn hr-btn--secondary" onclick="openSetPhotoModal()">Change photo</button>
            <button type="button" class="hr-btn hr-btn--primary" onclick="openEditModal(PROFILE.id)">Edit</button>
        </div>
    </div>
</div>

<!-- Add / edit employee -->
<div class="hr-modal" id="empFormModal">
    <div class="hr-modal__box hr-modal__box--wide">
        <div class="hr-modal__head"><span id="empFormTitle">New employee</span><button type="button" class="hr-modal__close" onclick="closeModal('empFormModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-error" id="empFormResult" role="alert" style="margin-bottom:10px"></div>
            <div class="hr-form hr-form--3">
                <div class="hr-field hr-full" id="fgEmpCode" style="display:none">
                    <label class="hr-label">Staff number</label>
                    <div class="em-code-row">
                        <asp:TextBox ID="txtEmpCodeDisplay" runat="server" CssClass="hr-input" MaxLength="25" />
                        <button type="button" class="hr-btn hr-btn--secondary" id="btnEcLock" onclick="toggleEmpCodeLock()">Change</button>
                    </div>
                    <div class="hr-hint" id="ecWarning" style="display:none">The staff number links payroll and reports. Change it only to correct an error.</div>
                </div>

                <div class="em-group">Personal</div>
                <div class="hr-field"><label class="hr-label">Full name <span class="hr-req">*</span></label><asp:TextBox ID="txtEmpName" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Email <span class="hr-req">*</span></label><asp:TextBox ID="txtEmpEmail" runat="server" CssClass="hr-input" TextMode="Email" /></div>
                <div class="hr-field"><label class="hr-label">Phone <span class="hr-req">*</span></label><asp:TextBox ID="txtEmpPhone" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Date of birth</label><asp:TextBox ID="txtEmpDOB" runat="server" CssClass="hr-input" TextMode="Date" /></div>
                <div class="hr-field"><label class="hr-label">Gender</label>
                    <asp:DropDownList ID="ddlGender" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="Male" Text="Male" />
                        <asp:ListItem Value="Female" Text="Female" />
                    </asp:DropDownList></div>
                <div class="hr-field"><label class="hr-label">Marital status</label>
                    <asp:DropDownList ID="ddlMarital" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="SINGLE" Text="Single" />
                        <asp:ListItem Value="MARRIED" Text="Married" />
                        <asp:ListItem Value="DIVORCED" Text="Divorced" />
                        <asp:ListItem Value="WIDOWED" Text="Widowed" />
                    </asp:DropDownList></div>
                <div class="hr-field"><label class="hr-label">Nationality</label>
                    <asp:DropDownList ID="ddlNationality" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="UGANDAN" Text="Ugandan" />
                        <asp:ListItem Value="KENYAN" Text="Kenyan" />
                        <asp:ListItem Value="TANZANIAN" Text="Tanzanian" />
                        <asp:ListItem Value="RWANDESE" Text="Rwandese" />
                        <asp:ListItem Value="CONGOLESE" Text="Congolese" />
                        <asp:ListItem Value="OTHER" Text="Other" />
                    </asp:DropDownList></div>
                <div class="hr-field"><label class="hr-label">Religion</label><asp:TextBox ID="txtReligion" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Tribe</label><asp:TextBox ID="txtTribe" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">National ID number</label><asp:TextBox ID="txtNIN" runat="server" CssClass="hr-input" MaxLength="50" /></div>
                <div class="hr-field"><label class="hr-label">Residence</label><asp:TextBox ID="txtResidence" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Address</label><asp:TextBox ID="txtAddress" runat="server" CssClass="hr-input" /></div>

                <div class="em-group">Employment</div>
                <div class="hr-field"><label class="hr-label">Category <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlEmpType" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="Academic" Text="Academic" />
                        <asp:ListItem Value="Administrative" Text="Administrative" />
                        <asp:ListItem Value="Support" Text="Support" />
                        <asp:ListItem Value="Consultant" Text="Consultant" />
                    </asp:DropDownList></div>
                <div class="hr-field"><label class="hr-label">Station</label><asp:DropDownList ID="ddlStation" runat="server" CssClass="hr-select" /></div>
                <div class="hr-field"><label class="hr-label">Entry year</label><asp:TextBox ID="txtEntryYear" runat="server" CssClass="hr-input" TextMode="Number" /></div>
                <div class="hr-field"><label class="hr-label">Highest education</label>
                    <asp:DropDownList ID="ddlEducation" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="NA" Text="Not recorded" />
                        <asp:ListItem Value="Certificate" Text="Certificate" />
                        <asp:ListItem Value="Diploma" Text="Diploma" />
                        <asp:ListItem Value="Bachelors" Text="Bachelor's degree" />
                        <asp:ListItem Value="Postgraduate" Text="Postgraduate diploma" />
                        <asp:ListItem Value="Masters" Text="Master's degree" />
                        <asp:ListItem Value="PhD" Text="Doctorate" />
                        <asp:ListItem Value="Professor" Text="Professor" />
                    </asp:DropDownList></div>
                <div class="hr-field" style="grid-column:span 2"><label class="hr-label">Qualifications</label><asp:TextBox ID="txtQualifications" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Employment status</label>
                    <asp:DropDownList ID="ddlEmpStatus" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="ACTIVE" Text="Active" Selected="True" />
                        <asp:ListItem Value="PROBATION" Text="Probation" />
                        <asp:ListItem Value="SUSPENDED" Text="Suspended" />
                        <asp:ListItem Value="ON_LEAVE" Text="On leave" />
                        <asp:ListItem Value="TERMINATED" Text="Terminated" />
                        <asp:ListItem Value="RESIGNED" Text="Resigned" />
                        <asp:ListItem Value="RETIRED" Text="Retired" />
                    </asp:DropDownList></div>
                <div class="hr-field"><label class="hr-label">Date joined</label><asp:TextBox ID="txtDateJoined" runat="server" CssClass="hr-input" TextMode="Date" /></div>
                <div class="hr-field"><label class="hr-label">Probation ends</label><asp:TextBox ID="txtProbationEnd" runat="server" CssClass="hr-input" TextMode="Date" /></div>
                <div class="hr-field em-pick" id="supAcGroup" style="grid-column:span 2">
                    <label class="hr-label" for="supFilter">Supervisor <span class="hr-req">*</span></label>
                    <input type="text" id="supFilter" class="hr-input" placeholder="Type to filter the list" autocomplete="off" />
                    <select id="supSelect" class="hr-select"><asp:Literal ID="litPeopleOptions" runat="server" /></select>
                </div>
                <div class="hr-field"><label class="hr-label">Appraised</label>
                    <asp:DropDownList ID="ddlAppraised" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="1" Text="Yes" Selected="True" />
                        <asp:ListItem Value="0" Text="No" />
                    </asp:DropDownList></div>
                <div class="hr-field em-pick" style="grid-column:span 2">
                    <label class="hr-label" for="revFilter">Reviewer</label>
                    <input type="text" id="revFilter" class="hr-input" placeholder="Type to filter the list" autocomplete="off" />
                    <select id="revSelect" class="hr-select"></select>
                    <div class="hr-hint">Countersigns appraisals.</div>
                </div>
                <div class="hr-field"><label class="hr-label">Appraisal cycle</label>
                    <asp:DropDownList ID="ddlAppraisalCycle" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="ANNUAL" Text="Annual" Selected="True" />
                        <asp:ListItem Value="SEMI_ANNUAL" Text="Twice a year" />
                        <asp:ListItem Value="QUARTERLY" Text="Quarterly" />
                        <asp:ListItem Value="PROBATION" Text="Probation" />
                    </asp:DropDownList></div>

                <div class="em-group">Pay and statutory</div>
                <div class="hr-field"><label class="hr-label">TIN</label><asp:TextBox ID="txtTIN" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">NSSF number</label><asp:TextBox ID="txtNSSF" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Bank</label><asp:DropDownList ID="ddlBank" runat="server" CssClass="hr-select" /></div>
                <div class="hr-field"><label class="hr-label">Bank account</label><asp:TextBox ID="txtBankAccount" runat="server" CssClass="hr-input" /></div>

                <div class="em-group">Family and emergency contact</div>
                <div class="hr-field"><label class="hr-label">Spouse</label><asp:TextBox ID="txtSpouse" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Children</label><asp:TextBox ID="txtChildren" runat="server" CssClass="hr-input" TextMode="Number" Text="0" /></div>
                <div class="hr-field"><label class="hr-label">Father</label><asp:TextBox ID="txtFather" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Mother</label><asp:TextBox ID="txtMother" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Emergency contact</label><asp:TextBox ID="txtContactPerson" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Relationship</label><asp:TextBox ID="txtRelation" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Contact phone</label><asp:TextBox ID="txtContactPhone" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Referee 1</label><asp:TextBox ID="txtReferee1" runat="server" CssClass="hr-input" /></div>
                <div class="hr-field"><label class="hr-label">Referee 2</label><asp:TextBox ID="txtReferee2" runat="server" CssClass="hr-input" /></div>

                <div class="em-group">Background</div>
                <div class="hr-field hr-full"><label class="hr-label">Training</label><asp:TextBox ID="txtSchooling" runat="server" CssClass="hr-textarea" TextMode="MultiLine" Rows="2" /></div>
                <div class="hr-field hr-full"><label class="hr-label">Employment history</label><asp:TextBox ID="txtEmploymentHist" runat="server" CssClass="hr-textarea" TextMode="MultiLine" Rows="2" /></div>
                <div class="hr-field hr-full"><label class="hr-label">Medical</label><asp:TextBox ID="txtMedical" runat="server" CssClass="hr-textarea" TextMode="MultiLine" Rows="2" /></div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('empFormModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnEmpFormSubmit" onclick="submitEmpForm()">Save employee</button>
        </div>
    </div>
</div>

<!-- Fix account -->
<div class="hr-modal" id="fixLoginModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Fix account</span><button type="button" class="hr-modal__close" onclick="closeModal('fixLoginModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <p style="margin:0 0 12px" id="fixLoginUserInfo"></p>
            <div class="hr-field">
                <label class="hr-label" for="fixLoginPassword">New password</label>
                <input type="text" id="fixLoginPassword" class="hr-input em-creds" autocomplete="off" />
                <div class="hr-hint">Leave blank to generate one. At least 6 characters.</div>
            </div>
            <div class="hr-error" id="fixLoginResult" role="alert" style="margin-top:8px"></div>
            <div id="fixLoginPwdWrap" style="display:none;margin-top:12px">
                <dl class="hr-dl hr-dl--2">
                    <div><dt>Username</dt><dd class="em-creds" id="fixLoginUsername"></dd></div>
                    <div><dt>Password</dt><dd class="em-creds" id="fixLoginPwdValue"></dd></div>
                </dl>
                <div class="hr-row" style="margin-top:8px">
                    <button type="button" class="hr-btn hr-btn--secondary hr-btn--sm" onclick="copyCredentials()">Copy</button>
                    <span class="hr-hint">Give these to the employee and ask them to change the password after signing in.</span>
                </div>
            </div>
            <details id="fixLoginLogWrap" style="display:none;margin-top:12px">
                <summary class="hr-hint" style="cursor:pointer">Steps taken</summary>
                <ul class="em-log" id="fixLoginLog"></ul>
            </details>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('fixLoginModal')">Close</button>
            <button type="button" id="fixLoginSubmitBtn" class="hr-btn hr-btn--primary" onclick="submitFixLogin()">Fix account</button>
        </div>
    </div>
</div>

<!-- Photo -->
<div class="hr-modal" id="setPhotoModal">
    <div class="hr-modal__box" style="width:420px">
        <div class="hr-modal__head"><span>Change photo</span><button type="button" class="hr-modal__close" onclick="closeModal('setPhotoModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <img id="photoPreviewImg" class="em-preview" src="../staffimages/default.jpg" alt="" />
            <div class="hr-field">
                <label class="hr-label" for="photoFileInput">Photo</label>
                <input type="file" id="photoFileInput" accept="image/*" onchange="previewEmployeePhotoFile(this)" />
                <div class="hr-hint">JPG, PNG, BMP or GIF, up to 5 MB.</div>
            </div>
            <div class="hr-error" id="photoResult" role="alert" style="margin-top:8px"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('setPhotoModal')">Cancel</button>
            <button type="button" id="photoSubmitBtn" class="hr-btn hr-btn--primary" onclick="submitSetPhotoForm()">Upload</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="hrToast" role="status" aria-live="polite"></div>

</div>

<script type="text/javascript">
function el(id){ return document.getElementById(id); }
function openModal(id){ el(id).classList.add('is-open'); }
function closeModal(id){ el(id).classList.remove('is-open'); }
function escHtml(s){ var d = document.createElement('div'); d.appendChild(document.createTextNode(s || '')); return d.innerHTML; }
function showToast(msg, isErr){
    var t = el('hrToast');
    t.textContent = msg;
    t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function(){ t.className = 'hr-toast'; }, isErr ? 7000 : 4000);
}
function getJson(url, done, fail){
    var xhr = new XMLHttpRequest();
    xhr.open('GET', url, true);
    xhr.onreadystatechange = function(){
        if(xhr.readyState !== 4) return;
        var d = null;
        try { d = JSON.parse(xhr.responseText); } catch(e) {}
        if(xhr.status === 200 && d && !d.error) done(d); else fail(d && d.error ? d.error : 'The request could not be completed.');
    };
    xhr.send();
}

/* Filters, sorting, paging, export (GET) */
function filterParams(){
    var p = [];
    function add(k, id){ var v = el(id).value; if(v) p.push(k + '=' + encodeURIComponent(v)); }
    var q = el('<%= txtSearch.ClientID %>').value.trim();
    if(q) p.push('q=' + encodeURIComponent(q));
    add('status', '<%= ddlFilterStatus.ClientID %>');
    add('type', '<%= ddlFilterType.ClientID %>');
    add('dept', '<%= ddlFilterDept.ClientID %>');
    add('station', '<%= ddlFilterStation.ClientID %>');
    return p;
}
function withSort(p){
    var sp = new URLSearchParams(window.location.search);
    if(sp.get('sort')) p.push('sort=' + encodeURIComponent(sp.get('sort')));
    if(sp.get('dir')) p.push('dir=' + encodeURIComponent(sp.get('dir')));
    return p;
}
function applyFilters(){
    var p = withSort(filterParams());
    var sz = el('<%= ddlPageSize.ClientID %>').value;
    if(sz !== '50') p.push('sz=' + sz);
    location.href = 'HREmployees.aspx' + (p.length ? '?' + p.join('&') : '');
}
function goPage(n){
    var sp = new URLSearchParams(window.location.search);
    sp.set('page', n);
    location.href = 'HREmployees.aspx?' + sp.toString();
}
function doSort(col){
    var sp = new URLSearchParams(window.location.search);
    var cur = (sp.get('sort') || 'name').toLowerCase(), dir = (sp.get('dir') || 'ASC').toUpperCase();
    sp.set('sort', col);
    sp.set('dir', (cur === col && dir === 'ASC') ? 'DESC' : 'ASC');
    sp.delete('page');
    location.href = 'HREmployees.aspx?' + sp.toString();
}
function exportEmployees(fmt){
    var p = withSort(filterParams());
    p.unshift('action=export_employees');
    if(fmt === 'csv') p.push('fmt=csv');
    location.href = 'HREmployees.aspx?' + p.join('&');
}
el('<%= txtSearch.ClientID %>').addEventListener('keydown', function(e){ if(e.key === 'Enter'){ e.preventDefault(); applyFilters(); } });

/* People pickers (supervisor, reviewer): a filter box above a plain select */
var PEOPLE = [];
(function(){
    var s = el('supSelect');
    for(var i = 0; i < s.options.length; i++) PEOPLE.push([s.options[i].value, s.options[i].text]);
    el('revSelect').innerHTML = s.innerHTML;
})();
function fillPicker(selectId, q, keep){
    var html = '', first = '';
    q = (q || '').toLowerCase();
    for(var i = 0; i < PEOPLE.length; i++){
        var o = PEOPLE[i];
        if(o[0] && q && o[1].toLowerCase().indexOf(q) < 0) continue;
        if(o[0] && !first) first = o[0];
        html += '<option value="' + escHtml(o[0]) + '">' + escHtml(o[1]) + '</option>';
    }
    var s = el(selectId);
    s.innerHTML = html;
    s.value = keep || '';
    if(!s.value && q && first) s.value = first;
}
function bindPicker(filterId, selectId, hiddenId){
    el(filterId).addEventListener('input', function(){
        fillPicker(selectId, this.value.trim(), el(selectId).value);
        el(hiddenId).value = el(selectId).value;
    });
    el(selectId).addEventListener('change', function(){ el(hiddenId).value = this.value; });
}
function setPicker(filterId, selectId, hiddenId, value){
    el(filterId).value = '';
    fillPicker(selectId, '', value && value !== '0' ? String(value) : '');
    el(hiddenId).value = el(selectId).value;
}
bindPicker('supFilter', 'supSelect', '<%= hfSupervisorID.ClientID %>');
bindPicker('revFilter', 'revSelect', '<%= hfReviewerID.ClientID %>');

/* Add / edit */
var editMode = false;
function resetEmpForm(){
    var fields = document.querySelectorAll('#empFormModal input.hr-input, #empFormModal textarea');
    for(var i = 0; i < fields.length; i++){
        if(fields[i].id === 'supFilter' || fields[i].id === 'revFilter') continue;
        fields[i].value = fields[i].type === 'number' ? '0' : '';
    }
    var selects = document.querySelectorAll('#empFormModal select.hr-select');
    for(var j = 0; j < selects.length; j++) if(selects[j].id !== 'supSelect' && selects[j].id !== 'revSelect') selects[j].selectedIndex = 0;
    setPicker('supFilter', 'supSelect', '<%= hfSupervisorID.ClientID %>', '');
    setPicker('revFilter', 'revSelect', '<%= hfReviewerID.ClientID %>', '');
    el('<%= hdnEditEmpID.ClientID %>').value = '';
    el('<%= hfOriginalEmpCode.ClientID %>').value = '';
    el('empFormResult').textContent = '';
    el('fgEmpCode').style.display = 'none';
    var code = el('<%= txtEmpCodeDisplay.ClientID %>');
    code.value = ''; code.readOnly = true;
    el('btnEcLock').textContent = 'Change';
    el('ecWarning').style.display = 'none';
}
function openAddModal(){
    editMode = false;
    resetEmpForm();
    el('empFormTitle').textContent = 'New employee';
    el('<%= txtEntryYear.ClientID %>').value = new Date().getFullYear();
    el('<%= txtReferee1.ClientID %>').value = '';
    el('<%= txtReferee2.ClientID %>').value = '';
    openModal('empFormModal');
}
function setVal(id, v){ var e = el(id); if(e) e.value = (v && v !== '-') ? v : ''; }
function setDdl(id, v){
    var e = el(id); if(!e || !v) return;
    for(var i = 0; i < e.options.length; i++)
        if(e.options[i].value.toUpperCase() === String(v).toUpperCase()){ e.selectedIndex = i; return; }
}
function openEditModal(empID){
    if(!empID) return;
    closeModal('profileModal');
    editMode = true;
    resetEmpForm();
    el('empFormTitle').textContent = 'Edit employee';
    el('<%= hdnEditEmpID.ClientID %>').value = empID;
    el('empFormResult').textContent = 'Loading';
    openModal('empFormModal');
    getJson('HREmployees.aspx?ajax=get_emp&id=' + empID, function(d){
        el('empFormResult').textContent = '';
        setVal('<%= txtEmpName.ClientID %>', d.emp_name);
        setVal('<%= txtEmpEmail.ClientID %>', d.emp_email);
        setVal('<%= txtEmpPhone.ClientID %>', d.emp_phone);
        setVal('<%= txtEmpDOB.ClientID %>', d.emp_birthdate);
        setDdl('<%= ddlGender.ClientID %>', d.gender);
        setDdl('<%= ddlMarital.ClientID %>', d.marital_status);
        setDdl('<%= ddlNationality.ClientID %>', d.emp_nationality);
        setVal('<%= txtReligion.ClientID %>', d.religion);
        setVal('<%= txtTribe.ClientID %>', d.tribe);
        setVal('<%= txtNIN.ClientID %>', d.nin);
        setVal('<%= txtResidence.ClientID %>', d.current_residence);
        setVal('<%= txtAddress.ClientID %>', d.address);
        setDdl('<%= ddlEmpType.ClientID %>', d.EmpType);
        setDdl('<%= ddlStation.ClientID %>', d.Entry_Satation);
        setVal('<%= txtEntryYear.ClientID %>', d.Entry_Year);
        setDdl('<%= ddlEducation.ClientID %>', d.max_education);
        setVal('<%= txtQualifications.ClientID %>', d.emp_qualifications);
        setVal('<%= txtTIN.ClientID %>', d.tin);
        setVal('<%= txtNSSF.ClientID %>', d.nssf_no);
        setDdl('<%= ddlBank.ClientID %>', d.bankID);
        setVal('<%= txtBankAccount.ClientID %>', d.bankAccount);
        setVal('<%= txtSpouse.ClientID %>', d.spouse_name);
        setVal('<%= txtChildren.ClientID %>', d.no_children || '0');
        setVal('<%= txtFather.ClientID %>', d.father_name);
        setVal('<%= txtMother.ClientID %>', d.mother_name);
        setVal('<%= txtContactPerson.ClientID %>', d.contact_person);
        setVal('<%= txtRelation.ClientID %>', d.relation);
        setVal('<%= txtContactPhone.ClientID %>', d.phone_contacts);
        setVal('<%= txtReferee1.ClientID %>', d.referee_1);
        setVal('<%= txtReferee2.ClientID %>', d.referee_2);
        setVal('<%= txtMedical.ClientID %>', d.medical_background);
        setVal('<%= txtSchooling.ClientID %>', d.schooling_info);
        setVal('<%= txtEmploymentHist.ClientID %>', d.employment_info);
        setDdl('<%= ddlEmpStatus.ClientID %>', d.employment_status);
        setVal('<%= txtDateJoined.ClientID %>', d.date_joined);
        setVal('<%= txtProbationEnd.ClientID %>', d.probation_end_date);
        setDdl('<%= ddlAppraised.ClientID %>', d.to_be_appraised);
        setDdl('<%= ddlAppraisalCycle.ClientID %>', d.appraisal_cycle);
        setPicker('supFilter', 'supSelect', '<%= hfSupervisorID.ClientID %>', d.supervisorID);
        setPicker('revFilter', 'revSelect', '<%= hfReviewerID.ClientID %>', d.reviewer_id);
        el('fgEmpCode').style.display = '';
        el('<%= txtEmpCodeDisplay.ClientID %>').value = d.EMP_CODE || '';
        el('<%= hfOriginalEmpCode.ClientID %>').value = d.EMP_CODE || '';
    }, function(msg){ el('empFormResult').textContent = msg; });
}
function toggleEmpCodeLock(){
    var inp = el('<%= txtEmpCodeDisplay.ClientID %>');
    if(inp.readOnly){
        inp.readOnly = false;
        el('btnEcLock').textContent = 'Keep';
        el('ecWarning').style.display = '';
        inp.focus(); inp.select();
    } else {
        inp.readOnly = true;
        inp.value = el('<%= hfOriginalEmpCode.ClientID %>').value;
        el('btnEcLock').textContent = 'Change';
        el('ecWarning').style.display = 'none';
    }
}
function submitEmpForm(){
    var r = el('empFormResult');
    var name = el('<%= txtEmpName.ClientID %>').value.trim(), email = el('<%= txtEmpEmail.ClientID %>').value.trim(), phone = el('<%= txtEmpPhone.ClientID %>').value.trim();
    if(!name){ r.textContent = 'Enter the full name.'; return; }
    if(!email || email.indexOf('@') < 0){ r.textContent = 'Enter a valid email address.'; return; }
    if(!phone){ r.textContent = 'Enter the phone number.'; return; }
    var sup = el('<%= hfSupervisorID.ClientID %>').value;
    if(!editMode && (!sup || sup === '0')){ r.textContent = 'Choose the supervisor.'; el('supAcGroup').scrollIntoView({ block: 'center' }); return; }
    el(editMode ? '<%= btnEditEmployee.ClientID %>' : '<%= btnAddEmployee.ClientID %>').click();
}
function reopenEmpForm(msg){
    editMode = !!el('<%= hdnEditEmpID.ClientID %>').value;
    el('empFormTitle').textContent = editMode ? 'Edit employee' : 'New employee';
    if(editMode) el('fgEmpCode').style.display = '';
    var sup = el('<%= hfSupervisorID.ClientID %>').value, rev = el('<%= hfReviewerID.ClientID %>').value;
    fillPicker('supSelect', '', sup); fillPicker('revSelect', '', rev);
    el('empFormResult').textContent = msg;
    openModal('empFormModal');
}

/* Profile */
var PROFILE = {};
function openEmployeeProfile(empID){
    PROFILE = { id: empID };
    el('epName').textContent = 'Loading';
    el('epMeta').textContent = '';
    el('epBadges').innerHTML = '';
    el('epPhoto').src = '../staffimages/default.jpg';
    showTab('details');
    el('epPane_details').innerHTML = '<div class="hr-loading">Loading</div>';
    ['contracts','leave','payroll'].forEach(function(t){ el('epPane_' + t).innerHTML = ''; });
    openModal('profileModal');
    getJson('HREmployees.aspx?ajax=get_profile&id=' + empID, function(d){
        PROFILE = d;
        el('epName').textContent = d.name;
        var meta = [d.code, d.job, d.dept].filter(function(x){ return x; });
        el('epMeta').textContent = meta.join(', ');
        el('epPhoto').src = d.photo || '../staffimages/default.jpg';
        el('epBadges').innerHTML = d.statusBadge + (d.type ? ' <span class="hr-badge hr-badge--neutral">' + escHtml(d.type) + '</span>' : '');
        el('epPane_details').innerHTML = d.detailsHtml || '<div class="hr-empty">No details recorded.</div>';
        el('epPane_contracts').innerHTML = d.contractsHtml || '';
        el('epPane_leave').innerHTML = d.leaveHtml || '';
        el('epPane_payroll').innerHTML = d.payrollHtml || '';
    }, function(msg){ el('epPane_details').innerHTML = '<div class="hr-empty">' + escHtml(msg) + '</div>'; });
}
function showTab(name){
    var tabs = document.querySelectorAll('#epTabs .hr-subtab');
    for(var i = 0; i < tabs.length; i++) tabs[i].classList.toggle('hr-subtab--active', tabs[i].getAttribute('data-tab') === name);
    ['details','contracts','leave','payroll'].forEach(function(t){ el('epPane_' + t).classList.toggle('is-on', t === name); });
}
(function(){
    var tabs = document.querySelectorAll('#epTabs .hr-subtab');
    for(var i = 0; i < tabs.length; i++) tabs[i].addEventListener('click', function(){ showTab(this.getAttribute('data-tab')); });
})();

/* Delete */
function confirmDelete(){
    if(!PROFILE.id) return;
    hrConfirm({ title: 'Delete employee', message: 'Delete ' + (PROFILE.name || 'this employee') + '? This removes the employee record and their login. It cannot be undone.', ok: 'Delete', danger: true }, function(){
        el('<%= hdnDeleteEmpID.ClientID %>').value = PROFILE.id;
        el('<%= btnDeleteEmployee.ClientID %>').click();
    });
}

/* Fix account (login): opened from a list row or from the profile */
var LOGIN_TARGET = { id: 0, name: '' };
function fixAccountFromRow(btn, id){
    openFixAccount(id, btn.getAttribute('data-name') || '', btn.getAttribute('data-email') || '');
}
function openResetLogin(){
    if(!PROFILE.id) return;
    openFixAccount(PROFILE.id, PROFILE.name || '', PROFILE.email || '');
}
function openFixAccount(id, name, email){
    LOGIN_TARGET = { id: id, name: name };
    el('fixLoginUserInfo').innerHTML = 'Fix the login account of <strong>' + escHtml(name) + '</strong>' +
        (email ? '. The username will be ' + escHtml(email) + '.' : '.') +
        ' This creates the account if it is missing, unlocks it and sets a new password.';
    el('fixLoginResult').textContent = '';
    el('fixLoginPassword').value = '';
    el('fixLoginLogWrap').style.display = 'none';
    el('fixLoginLog').innerHTML = '';
    el('fixLoginPwdWrap').style.display = 'none';
    el('fixLoginSubmitBtn').disabled = false;
    openModal('fixLoginModal');
}
function submitFixLogin(){
    if(!LOGIN_TARGET.id) return;
    var pwd = el('fixLoginPassword').value || '';
    if(pwd && pwd.length < 6){ el('fixLoginResult').textContent = 'Use at least 6 characters, or leave the password blank.'; return; }
    var btn = el('fixLoginSubmitBtn');
    btn.disabled = true; btn.textContent = 'Working';
    el('fixLoginResult').textContent = '';
    fetch('HREmployees.aspx?ajax=fix_login&id=' + encodeURIComponent(LOGIN_TARGET.id), {
        method: 'POST', credentials: 'same-origin',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
        body: 'new_password=' + encodeURIComponent(pwd)
    })
    .then(function(r){ return r.json(); })
    .then(function(d){
        btn.disabled = false; btn.textContent = 'Fix account';
        if(d.log && d.log.length){
            var ul = el('fixLoginLog'); ul.innerHTML = '';
            for(var i = 0; i < d.log.length; i++){ var li = document.createElement('li'); li.textContent = d.log[i]; ul.appendChild(li); }
            el('fixLoginLogWrap').style.display = '';
        }
        if(d.success){
            el('fixLoginUsername').textContent = d.username || '';
            el('fixLoginPwdValue').textContent = d.password || '';
            el('fixLoginPwdWrap').style.display = '';
            showToast('Account fixed for ' + (LOGIN_TARGET.name || d.username) + '.');
        } else {
            el('fixLoginResult').textContent = d.error || 'The account could not be fixed.';
        }
    })
    .catch(function(){
        btn.disabled = false; btn.textContent = 'Fix account';
        el('fixLoginResult').textContent = 'The account could not be fixed. Check your connection and try again.';
    });
}
function copyCredentials(){
    var text = 'Username: ' + el('fixLoginUsername').textContent + '\nPassword: ' + el('fixLoginPwdValue').textContent;
    var ta = document.createElement('textarea');
    ta.value = text; document.body.appendChild(ta); ta.select();
    try { document.execCommand('copy'); showToast('Copied.'); } catch(e) {}
    document.body.removeChild(ta);
}

/* Photo */
var _photoFile = null;
function openSetPhotoModal(){
    if(!PROFILE.id) return;
    _photoFile = null;
    el('photoFileInput').value = '';
    el('photoPreviewImg').src = PROFILE.photo || '../staffimages/default.jpg';
    el('photoResult').textContent = '';
    el('photoSubmitBtn').disabled = false;
    openModal('setPhotoModal');
}
function previewEmployeePhotoFile(input){
    _photoFile = null;
    if(!input.files || !input.files.length) return;
    var file = input.files[0];
    if(file.size > 5 * 1024 * 1024){ el('photoResult').textContent = 'The photo is larger than 5 MB.'; input.value = ''; return; }
    _photoFile = file;
    var reader = new FileReader();
    reader.onload = function(e){ el('photoPreviewImg').src = e.target.result; };
    reader.readAsDataURL(file);
}
function submitSetPhotoForm(){
    if(!PROFILE.id) return;
    if(!_photoFile){ el('photoResult').textContent = 'Choose a photo first.'; return; }
    var btn = el('photoSubmitBtn');
    btn.disabled = true; btn.textContent = 'Uploading';
    var fd = new FormData();
    fd.append('photoFile', _photoFile);
    fetch('HREmployees.aspx?ajax=set_photo&id=' + encodeURIComponent(PROFILE.id), { method: 'POST', credentials: 'same-origin', body: fd })
    .then(function(r){ return r.json(); })
    .then(function(d){
        btn.disabled = false; btn.textContent = 'Upload';
        if(d.success){
            PROFILE.photo = d.photoUrl;
            el('epPhoto').src = d.photoUrl;
            closeModal('setPhotoModal');
            showToast('Photo updated.');
        } else el('photoResult').textContent = d.error || 'The photo could not be saved.';
    })
    .catch(function(){ btn.disabled = false; btn.textContent = 'Upload'; el('photoResult').textContent = 'The photo could not be saved. Check your connection and try again.'; });
}

document.addEventListener('keydown', function(e){
    if(e.key === 'Escape'){
        var open = document.querySelectorAll('.hr-modal.is-open');
        if(open.length) open[open.length - 1].classList.remove('is-open');
    }
    if(e.key === 'Enter' && e.target && e.target.tagName === 'INPUT' && e.target.closest && e.target.closest('.hr-modal')) e.preventDefault();
});
</script>
</asp:Content>
