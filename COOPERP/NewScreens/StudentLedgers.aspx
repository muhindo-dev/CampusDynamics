<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="StudentLedgers.aspx.cs" Inherits="COOPERP_NewScreens_StudentLedgers" Title="Student Ledgers - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===== STUDENT LEDGERS ================================================ */
.sl-wrap { display:flex; flex-direction:column; gap:12px; }
.sl-stats { display:grid; grid-template-columns:repeat(4,1fr); gap:10px; }
.sl-stat { background:#fff; border:1px solid #e0e5ed; padding:10px 12px; position:relative; overflow:hidden; }
.sl-stat::before { content:''; position:absolute; left:0; top:0; bottom:0; width:3px; background:var(--c,#174DA4); }
.sl-stat__v { font-size:18px; font-weight:700; color:#05275C; font-variant-numeric:tabular-nums; line-height:1.2; }
.sl-stat__l { font-size:10px; color:#7a8291; text-transform:uppercase; letter-spacing:.4px; margin-top:3px; }
.sl-stat--students { --c:#174DA4; } .sl-stat--billed { --c:#e65100; } .sl-stat--paid { --c:#2e7d32; } .sl-stat--balance { --c:#7b1fa2; }

.sl-card { background:#fff; border:1px solid #e0e5ed; }
.sl-card__header { padding:10px 14px; border-bottom:1px solid #e0e5ed; background:#f8f9fb; display:flex; align-items:center; justify-content:space-between; gap:8px; }
.sl-card__title { font-size:12px; font-weight:700; color:#05275C; }
.sl-btn { padding:6px 12px; border:none; font-size:11px; font-weight:600; cursor:pointer; }
.sl-btn--primary { background:#05275C; color:#fff; }
.sl-btn--ghost { background:#fff; border:1px solid #d8deea; color:#4f5b6d; }
.sl-btn--success { background:#2e7d32; color:#fff; }
.sl-btn:hover { opacity:.95; }

.sl-filters { padding:10px 14px; border-bottom:1px solid #e0e5ed; background:#f8f9fb; display:none; }
.sl-filters--open { display:block; }
.sl-frow { display:flex; gap:8px; flex-wrap:wrap; align-items:flex-end; }
.sl-fg { display:flex; flex-direction:column; gap:3px; min-width:120px; }
.sl-fg label { font-size:9px; text-transform:uppercase; letter-spacing:.4px; color:#7a8291; font-weight:700; }
.sl-input, .sl-select { border:1px solid #d5dbe7; background:#fff; padding:6px 8px; font-size:11px; color:#1a1a2e; }
.sl-input:focus, .sl-select:focus { outline:none; border-color:#174DA4; }
.sl-fg--search { min-width:240px; flex:1; }
.sl-balance-range { display:flex; gap:6px; align-items:center; }
.sl-balance-range .sl-input { width:110px; }
.sl-hint { display:block; margin-top:3px; font-size:9px; color:#8a93a3; line-height:1.3; max-width:300px; }
.sl-hint b { color:#536178; font-weight:700; }
.sl-fg--actions { min-width:auto; }
.sl-actions-row { display:flex; gap:6px; flex-wrap:wrap; }

.sl-table-wrap { overflow:auto; max-height:620px; }
.sl-table { width:100%; border-collapse:collapse; min-width:1220px; font-size:11px; }
.sl-table thead th { position:sticky; top:0; z-index:5; background:#f5f7fa; color:#536178; text-transform:uppercase; font-size:9px; letter-spacing:.35px; font-weight:700; border-bottom:2px solid #e0e5ed; padding:9px 8px; white-space:nowrap; }
.sl-table tbody td { border-bottom:1px solid #eef1f5; padding:8px; vertical-align:middle; }
.sl-table tbody tr:nth-child(even) { background:#fafbfc; }
.sl-table tbody tr:hover { background:#eef3ff; }
.sl-sort { color:#536178; text-decoration:none; }
.sl-sort:hover { color:#174DA4; }
.sl-money { text-align:right; font-variant-numeric:tabular-nums; font-weight:700; }
.sl-money--billed { color:#e65100; }
.sl-money--paid { color:#2e7d32; }
.sl-balance--debit { color:#c62828; }
.sl-balance--credit { color:#2e7d32; }
.sl-balance--zero { color:#546e7a; }
.sl-chip { display:inline-block; padding:2px 7px; font-size:9px; font-weight:700; text-transform:uppercase; letter-spacing:.3px; border:1px solid #d6ddeb; background:#eef2fb; color:#174DA4; }

.sl-actions { position:relative; display:inline-block; }
.sl-actions__btn { width:28px; height:28px; display:inline-flex; align-items:center; justify-content:center; background:#fff; border:1px solid #cfd6e3; cursor:pointer; color:#364152; padding:0; }
.sl-actions__btn svg { width:14px; height:14px; }
.sl-actions__btn:hover { border-color:#174DA4; color:#174DA4; background:#eef3ff; }
.sl-actions__menu { display:none; position:absolute; right:0; top:100%; min-width:185px; background:#fff; border:1px solid #dce3ef; box-shadow:0 8px 22px rgba(0,0,0,.12); z-index:20; }
.sl-actions__menu a { display:block; padding:8px 10px; font-size:11px; color:#233146; text-decoration:none; border-bottom:1px solid #f0f2f7; }
.sl-actions__menu a:last-child { border-bottom:none; }
.sl-actions__menu a:hover { background:#eef3ff; color:#174DA4; }
.sl-actions--open .sl-actions__menu { display:block; }

.sl-pager { padding:9px 14px; border-top:1px solid #e0e5ed; background:#f8f9fb; display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:8px; font-size:11px; color:#4f5b6d; }
.sl-pager__btns { display:flex; gap:4px; flex-wrap:wrap; }
.sl-pager__btn { border:1px solid #d4dbe8; background:#fff; color:#39485e; padding:4px 8px; font-size:11px; cursor:pointer; }
.sl-pager__btn--active { background:#05275C; color:#fff; border-color:#05275C; }
.sl-empty { padding:22px; text-align:center; color:#657389; font-size:12px; }

.sl-toast { display:none; padding:10px 14px; font-size:12px; font-weight:600; border:1px solid transparent; }
.sl-toast--show { display:block; }
.sl-toast--ok { background:#e6f4ea; color:#145c2a; border-color:#c8e6c9; }
.sl-toast--err { background:#fdecec; color:#b42318; border-color:#f5c2c7; }

.sl-modal-overlay { display:none; position:fixed; inset:0; background:rgba(0,0,0,.45); z-index:9998; }
.sl-modal-overlay--show { display:flex; align-items:center; justify-content:center; }
.sl-modal { width:900px; max-width:96vw; max-height:92vh; overflow:hidden; background:#fff; box-shadow:0 12px 36px rgba(0,0,0,.2); }
.sl-modal__head { background:#05275C; color:#fff; padding:12px 14px; display:flex; justify-content:space-between; align-items:center; }
.sl-modal__title { font-size:13px; font-weight:700; }
.sl-modal__close { border:none; background:rgba(255,255,255,.16); color:#fff; width:26px; height:26px; cursor:pointer; }
#slModalPrint:hover { background:rgba(255,255,255,.32); }
.sl-modal__body { padding:12px; max-height:75vh; overflow:auto; }
.sl-modal__stats { display:grid; grid-template-columns:repeat(3,1fr); gap:8px; margin-bottom:10px; }
.sl-modal__kpi { border:1px solid #e0e5ed; padding:8px 10px; background:#f8f9fb; }
.sl-modal__kpi b { display:block; font-size:14px; color:#05275C; }
.sl-modal__kpi span { font-size:9px; text-transform:uppercase; color:#7a8291; }
.sl-modal__kpi--billed { background:#fff7ed; border-color:#fed7aa; }
.sl-modal__kpi--billed b { color:#c2410c; }
.sl-modal__kpi--paid { background:#ecfdf3; border-color:#bbf7d0; }
.sl-modal__kpi--paid b { color:#166534; }
.sl-modal__kpi--bal-debit { background:#fef2f2; border-color:#fecaca; }
.sl-modal__kpi--bal-debit b { color:#b91c1c; }
.sl-modal__kpi--bal-credit { background:#ecfdf3; border-color:#bbf7d0; }
.sl-modal__kpi--bal-credit b { color:#166534; }
.sl-modal__kpi--bal-zero { background:#f8fafc; border-color:#e2e8f0; }
.sl-modal__kpi--bal-zero b { color:#475569; }
.sl-modal__tbl { width:100%; border-collapse:collapse; font-size:11px; }
.sl-modal__tbl th, .sl-modal__tbl td { border-bottom:1px solid #eef1f5; padding:7px 6px; text-align:left; }
.sl-modal__tbl th { background:#f5f7fa; text-transform:uppercase; font-size:9px; color:#6a7587; }
.sl-amt--dr { color:#b91c1c; font-weight:700; }
.sl-amt--cr { color:#166534; font-weight:700; }
.sl-amt--bal-dr { color:#b91c1c; font-weight:700; }
.sl-amt--bal-cr { color:#166534; font-weight:700; }
.sl-amt--bal-zero { color:#475569; font-weight:700; }

/* Fix Billing Wizard */
.sl-fx-overlay { display:none; position:fixed; inset:0; background:rgba(0,0,0,.45); z-index:10020; }
.sl-fx-overlay--show { display:flex; align-items:center; justify-content:center; }
.sl-fx { width:780px; max-width:96vw; max-height:92vh; overflow:hidden; background:#fff; box-shadow:0 12px 36px rgba(0,0,0,.22); }
.sl-fx__head { background:#05275C; color:#fff; padding:12px 14px; display:flex; justify-content:space-between; align-items:center; }
.sl-fx__title { font-size:13px; font-weight:700; }
.sl-fx__close { border:none; background:rgba(255,255,255,.16); color:#fff; width:26px; height:26px; cursor:pointer; }
.sl-fx__steps { display:grid; grid-template-columns:repeat(4,1fr); border-bottom:1px solid #e3e8f2; }
.sl-fx__step { padding:8px 10px; font-size:10px; text-transform:uppercase; letter-spacing:.3px; color:#6b7280; border-bottom:2px solid transparent; text-align:center; font-weight:700; }
.sl-fx__step--on { color:#1d4ed8; border-bottom-color:#1d4ed8; background:#eff6ff; }
.sl-fx__step--done { color:#166534; border-bottom-color:#16a34a; background:#ecfdf3; }
.sl-fx__body { padding:12px; max-height:62vh; overflow:auto; }
.sl-fx__panel { display:none; }
.sl-fx__panel--on { display:block; }
.sl-fx__note { font-size:11px; color:#475569; margin-bottom:10px; }
.sl-fx__grid { display:grid; grid-template-columns:repeat(3,1fr); gap:8px; margin-bottom:10px; }
.sl-fx__kpi { border:1px solid #e0e5ed; padding:8px 10px; background:#f8fafc; }
.sl-fx__kpi b { display:block; font-size:14px; color:#0f172a; }
.sl-fx__kpi span { font-size:9px; text-transform:uppercase; color:#64748b; }
.sl-fx__kpi--warn { background:#fff7ed; border-color:#fdba74; }
.sl-fx__kpi--warn b { color:#c2410c; }
.sl-fx__kpi--ok { background:#ecfdf3; border-color:#86efac; }
.sl-fx__kpi--ok b { color:#166534; }
.sl-fx__tbl { width:100%; border-collapse:collapse; font-size:11px; }
.sl-fx__tbl th,.sl-fx__tbl td { border-bottom:1px solid #eef1f5; padding:6px; }
.sl-fx__tbl th { background:#f5f7fa; font-size:9px; text-transform:uppercase; color:#64748b; }
.sl-fx__summary { border:1px solid #e2e8f0; background:#f8fafc; padding:10px; font-size:11px; line-height:1.5; }
.sl-fx__alert { border:1px solid #fecaca; background:#fef2f2; color:#991b1b; padding:9px 10px; font-size:11px; margin-top:8px; }
.sl-fx__ok { border:1px solid #bbf7d0; background:#ecfdf3; color:#166534; padding:9px 10px; font-size:11px; margin-top:8px; }
.sl-fx__foot { padding:10px 12px; border-top:1px solid #e3e8f2; background:#f8fafc; display:flex; justify-content:space-between; gap:8px; }
.sl-fx__foot-right { display:flex; gap:6px; }
.sl-fx__btn { padding:6px 12px; border:none; cursor:pointer; font-size:11px; font-weight:700; }
.sl-fx__btn--ghost { background:#fff; border:1px solid #d4dbe8; color:#334155; }
.sl-fx__btn--primary { background:#1d4ed8; color:#fff; }
.sl-fx__btn--danger { background:#b91c1c; color:#fff; }
.sl-fx__btn:disabled { opacity:.55; cursor:not-allowed; }

/* Batch selection */
.sl-chk { width:16px; height:16px; cursor:pointer; accent-color:#174DA4; }
.sl-batch-bar { display:none; padding:8px 14px; background:#eef3ff; border-bottom:1px solid #c7d2e8; align-items:center; gap:10px; font-size:11px; color:#1e3a5f; }
.sl-batch-bar--show { display:flex; }
.sl-batch-bar__count { font-weight:700; }
.sl-batch-bar__actions { margin-left:auto; display:flex; gap:6px; }

/* Batch wizard (reuses .sl-fx styles with --batch modifiers) */
.sl-bx-overlay { display:none; position:fixed; inset:0; background:rgba(0,0,0,.5); z-index:10030; }
.sl-bx-overlay--show { display:flex; align-items:center; justify-content:center; }
.sl-bx { width:880px; max-width:96vw; max-height:94vh; overflow:hidden; background:#fff; box-shadow:0 12px 36px rgba(0,0,0,.24); }
.sl-bx__head { background:#05275C; color:#fff; padding:12px 14px; display:flex; justify-content:space-between; align-items:center; }
.sl-bx__title { font-size:13px; font-weight:700; }
.sl-bx__close { border:none; background:rgba(255,255,255,.16); color:#fff; width:26px; height:26px; cursor:pointer; }
.sl-bx__steps { display:grid; grid-template-columns:repeat(4,1fr); border-bottom:1px solid #e3e8f2; }
.sl-bx__step { padding:8px 10px; font-size:10px; text-transform:uppercase; letter-spacing:.3px; color:#6b7280; border-bottom:2px solid transparent; text-align:center; font-weight:700; }
.sl-bx__step--on { color:#1d4ed8; border-bottom-color:#1d4ed8; background:#eff6ff; }
.sl-bx__step--done { color:#166534; border-bottom-color:#16a34a; background:#ecfdf3; }
.sl-bx__body { padding:12px; max-height:64vh; overflow:auto; }
.sl-bx__panel { display:none; }
.sl-bx__panel--on { display:block; }
.sl-bx__foot { padding:10px 12px; border-top:1px solid #e3e8f2; background:#f8fafc; display:flex; justify-content:space-between; gap:8px; }
.sl-bx__foot-right { display:flex; gap:6px; }
.sl-bx__progress { width:100%; height:6px; background:#e2e8f0; margin:8px 0; overflow:hidden; }
.sl-bx__progress-bar { height:100%; background:#1d4ed8; transition:width .3s; width:0; }
.sl-bx__log { max-height:280px; overflow:auto; font-size:10px; font-family:monospace; border:1px solid #e2e8f0; background:#f8fafc; padding:6px 8px; margin-top:6px; }
.sl-bx__log-ok { color:#166534; }
.sl-bx__log-err { color:#b91c1c; }
.sl-bx__log-skip { color:#92400e; }

@media (max-width:980px){ .sl-stats{grid-template-columns:repeat(2,1fr);} .sl-modal__stats{grid-template-columns:1fr;} }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="sl-wrap">

    <asp:Panel ID="pnlToast" runat="server" CssClass="sl-toast" Visible="false">
        <asp:Literal ID="litToast" runat="server"></asp:Literal>
    </asp:Panel>

    <div class="sl-stats">
        <div class="sl-stat sl-stat--students"><div class="sl-stat__v"><asp:Literal ID="litStatStudents" runat="server" Text="0"></asp:Literal></div><div class="sl-stat__l">Active Students (Filtered)</div></div>
        <div class="sl-stat sl-stat--billed"><div class="sl-stat__v"><asp:Literal ID="litStatBilled" runat="server" Text="0"></asp:Literal></div><div class="sl-stat__l">Total Billed</div></div>
        <div class="sl-stat sl-stat--paid"><div class="sl-stat__v"><asp:Literal ID="litStatPaid" runat="server" Text="0"></asp:Literal></div><div class="sl-stat__l">Total Paid / Credits</div></div>
        <div class="sl-stat sl-stat--balance"><div class="sl-stat__v"><asp:Literal ID="litStatBalance" runat="server" Text="0"></asp:Literal></div><div class="sl-stat__l">Net Balance (CR = overpaid / DR = owing)</div></div>
    </div>

    <div class="sl-card">
        <div class="sl-card__header">
            <button type="button" class="sl-btn sl-btn--ghost" onclick="toggleFilters()">Toggle Filters</button>
        </div>

        <div id="slFilters" class="sl-filters sl-filters--open">
            <div class="sl-frow">
                <div class="sl-fg sl-fg--search">
                    <label>Search</label>
                    <asp:TextBox ID="txtSearch" runat="server" CssClass="sl-input" placeholder="Reg No, Entry No, Student Name, Programme"></asp:TextBox>
                </div>
                <div class="sl-fg"><label>Programme</label><asp:DropDownList ID="ddlProgramme" runat="server" CssClass="sl-select"></asp:DropDownList></div>
                <div class="sl-fg"><label>Entry Year</label><asp:DropDownList ID="ddlEntryYear" runat="server" CssClass="sl-select"></asp:DropDownList></div>
                <div class="sl-fg"><label>Current Acad Year</label><asp:DropDownList ID="ddlAcadYear" runat="server" CssClass="sl-select"></asp:DropDownList></div>
                <div class="sl-fg"><label>Current Semester</label><asp:DropDownList ID="ddlSemester" runat="server" CssClass="sl-select"></asp:DropDownList></div>
                <div class="sl-fg"><label>Balance State</label><asp:DropDownList ID="ddlBalanceState" runat="server" CssClass="sl-select"></asp:DropDownList></div>
                <div class="sl-fg">
                    <label>Balance Amount</label>
                    <div class="sl-balance-range">
                        <asp:DropDownList ID="ddlBalanceOp" runat="server" CssClass="sl-select"></asp:DropDownList>
                        <asp:TextBox ID="txtBalanceFrom" runat="server" CssClass="sl-input" placeholder="Amount"></asp:TextBox>
                        <asp:TextBox ID="txtBalanceTo" runat="server" CssClass="sl-input" placeholder="To"></asp:TextBox>
                    </div>
                    <span class="sl-hint">Balance size only (sign ignored). Use <b>Balance State</b> for owing vs credit.</span>
                </div>
                <div class="sl-fg"><label>Rows</label><asp:DropDownList ID="ddlPageSize" runat="server" CssClass="sl-select" onchange="applyFilters()"></asp:DropDownList></div>
                <div class="sl-fg"><label>Options</label><asp:CheckBox ID="chkWithTransactions" runat="server" Text="Only with transactions" /></div>
                <div class="sl-fg sl-fg--actions">
                    <label>&nbsp;</label>
                    <div class="sl-actions-row">
                        <button type="button" id="btnSearch" class="sl-btn sl-btn--primary" onclick="applyFilters()">Apply</button>
                        <button type="button" id="btnReset" class="sl-btn sl-btn--ghost" onclick="resetFilters()">Reset</button>
                        <button type="button" class="sl-btn sl-btn--ghost" onclick="exportLedgers('csv')">Export CSV</button>
                        <button type="button" class="sl-btn sl-btn--success" onclick="exportLedgers('excel')">Export Excel</button>
                    </div>
                </div>
            </div>
        </div>

        <div id="slBatchBar" class="sl-batch-bar">
            <input type="checkbox" class="sl-chk" id="slSelectAll" onclick="batchToggleAll(this)" title="Select / deselect all" />
            <span>Selected: <span class="sl-batch-bar__count" id="slBatchCount">0</span> students</span>
            <div class="sl-batch-bar__actions">
                <button type="button" class="sl-btn sl-btn--primary" onclick="batchRunFixBilling()">Fix Billing (Batch)</button>
            </div>
        </div>

        <div class="sl-table-wrap">
            <table class="sl-table">
                <thead>
                    <tr>
                        <th style="width:30px;"><input type="checkbox" class="sl-chk" id="slSelectAllHead" onclick="batchToggleAll(this)" title="Select / deselect all" /></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('student_name')">Student Name</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('regno')">Reg No</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('programme')">Programme</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('entry_year')">Entry Year</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('current_year')">Current Year</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('current_sem')">Sem</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('billed')">Total Billed</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('paid')">Total Paid</a></th>
                        <th><a href="javascript:void(0)" class="sl-sort" onclick="setSort('balance')">Balance</a></th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    <asp:Repeater ID="rptLedgers" runat="server">
                        <ItemTemplate>
                            <tr>
                                <td><input type="checkbox" class="sl-chk sl-row-chk" data-regno='<%# Js(GetRegno(Container.DataItem)) %>' onclick="batchUpdateCount()" /></td>
                                <td><%# Server.HtmlEncode((Eval("student_name") ?? "").ToString()) %></td>
                                <td><span class="sl-chip"><%# Server.HtmlEncode((Eval("regno") ?? "").ToString()) %></span></td>
                                <td><span class="sl-chip"><%# Server.HtmlEncode((Eval("progid") ?? "").ToString()) %></span></td>
                                <td><%# Server.HtmlEncode((Eval("entry_year") ?? "").ToString()) %></td>
                                <td><%# FormatStudyYear(Eval("current_study_year")) %></td>
                                <td><%# FormatSemester(Eval("current_semester")) %></td>
                                <td class="sl-money sl-money--billed"><%# FormatMoney(Eval("total_billed")) %></td>
                                <td class="sl-money sl-money--paid"><%# FormatMoney(Eval("total_paid")) %></td>
                                <td class="sl-money <%# GetBalanceClass(Eval("total_balance")) %>"><%# FormatBalance(Eval("total_balance")) %></td>
                                <td>
                                    <div class="sl-actions">
                                        <button type="button" class="sl-actions__btn" onclick="toggleActionMenu(this)" title="Open actions" aria-label="Open actions menu">
                                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="5" r="1.5"></circle><circle cx="12" cy="12" r="1.5"></circle><circle cx="12" cy="19" r="1.5"></circle></svg>
                                        </button>
                                        <div class="sl-actions__menu">
                                            <a href="javascript:void(0)" onclick='viewLedgerDetails("<%# Js(GetRegno(Container.DataItem)) %>")'>View Ledger Details</a>
                                            <a href="javascript:void(0)" onclick='printStatement("<%# Js(GetRegno(Container.DataItem)) %>")'>&#128438; Print Statement</a>
                                            <a href="javascript:void(0)" onclick='runFixBilling("<%# Js(GetRegno(Container.DataItem)) %>")'>Fix Student Billing</a>
                                            <a href="javascript:void(0)" onclick='runRemoveDouble("<%# Js(GetRegno(Container.DataItem)) %>")'>Remove Double Billing</a>
                                            <a href="<%# ResolveUrl("~/COOPERP/NewScreens/FeesTransactions.aspx") %>" target="_blank">Open Transactions</a>
                                        </div>
                                    </div>
                                </td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
            <asp:Panel ID="pnlEmpty" runat="server" CssClass="sl-empty" Visible="false">No active students found for the selected filters.</asp:Panel>
        </div>

        <div class="sl-pager">
            <div><asp:Literal ID="litRecordInfo" runat="server" Text="0 records"></asp:Literal></div>
            <div class="sl-pager__btns"><asp:Literal ID="litPager" runat="server"></asp:Literal></div>
        </div>
    </div>

</div>

<asp:HiddenField ID="hfSortField" runat="server" />
<asp:HiddenField ID="hfSortDir" runat="server" />
<asp:HiddenField ID="hfPageIndex" runat="server" Value="0" />

<div id="slModalOverlay" class="sl-modal-overlay" onclick="if(event.target===this) closeLedgerModal();">
    <div class="sl-modal">
        <div class="sl-modal__head">
            <div class="sl-modal__title" id="slModalTitle">Student Ledger Details</div>
            <div style="display:flex;gap:6px;align-items:center;">
                <button type="button" id="slModalPrint" onclick="printStatement(_slCurrentRegno)" style="border:none;background:rgba(255,255,255,.18);color:#fff;padding:4px 12px;font-size:12px;cursor:pointer;border-radius:3px;" title="Print Statement">&#128438; Print</button>
                <button type="button" class="sl-modal__close" onclick="closeLedgerModal()">&times;</button>
            </div>
        </div>
        <div class="sl-modal__body">
            <div class="sl-modal__stats">
                <div id="slKpiCardBilled" class="sl-modal__kpi sl-modal__kpi--billed"><b id="slKpiBilled">0</b><span>Total Billed</span></div>
                <div id="slKpiCardPaid" class="sl-modal__kpi sl-modal__kpi--paid"><b id="slKpiPaid">0</b><span>Total Paid</span></div>
                <div id="slKpiCardBalance" class="sl-modal__kpi"><b id="slKpiBalance">0</b><span>Net Balance</span></div>
            </div>
            <table class="sl-modal__tbl">
                <thead>
                    <tr>
                        <th>#</th><th>Date</th><th>Ref</th><th>Particulars</th><th style="text-align:right;">Debit</th><th style="text-align:right;">Credit</th><th style="text-align:right;">Balance</th>
                    </tr>
                </thead>
                <tbody id="slLedgerRows"></tbody>
            </table>
        </div>
    </div>
</div>

<div id="slFixOverlay" class="sl-fx-overlay" onclick="if(event.target===this) closeFixWizard();">
    <div class="sl-fx">
        <div class="sl-fx__head">
            <div class="sl-fx__title" id="slFixTitle">Fix Student Billing</div>
            <button type="button" class="sl-fx__close" onclick="closeFixWizard()">&times;</button>
        </div>
        <div class="sl-fx__steps">
            <div id="slFxStep1" class="sl-fx__step">1 Detect</div>
            <div id="slFxStep2" class="sl-fx__step">2 Summary</div>
            <div id="slFxStep3" class="sl-fx__step">3 Confirm</div>
            <div id="slFxStep4" class="sl-fx__step">4 Results</div>
        </div>
        <div class="sl-fx__body">
            <div id="slFxPanel1" class="sl-fx__panel">
                <div class="sl-fx__note">Detecting anomalies before repair.</div>
                <div id="slFxDetectWrap"></div>
            </div>
            <div id="slFxPanel2" class="sl-fx__panel">
                <div class="sl-fx__note">Summary of operations to be executed.</div>
                <div id="slFxSummaryWrap"></div>
            </div>
            <div id="slFxPanel3" class="sl-fx__panel">
                <div class="sl-fx__note">Confirm and execute billing repair.</div>
                <div id="slFxConfirmWrap"></div>
            </div>
            <div id="slFxPanel4" class="sl-fx__panel">
                <div class="sl-fx__note">Processing results.</div>
                <div id="slFxResultWrap"></div>
            </div>
        </div>
        <div class="sl-fx__foot">
            <button id="slFxBack" type="button" class="sl-fx__btn sl-fx__btn--ghost" onclick="fixWizardBack()">Back</button>
            <div class="sl-fx__foot-right">
                <button id="slFxClose" type="button" class="sl-fx__btn sl-fx__btn--ghost" onclick="closeFixWizard()">Close</button>
                <button id="slFxNext" type="button" class="sl-fx__btn sl-fx__btn--primary" onclick="fixWizardNext()">Next</button>
                <button id="slFxRun" type="button" class="sl-fx__btn sl-fx__btn--danger" onclick="fixWizardRun()">Run Fix</button>
            </div>
        </div>
    </div>
</div>

<!-- Batch Fix Billing Wizard -->
<div id="slBxOverlay" class="sl-bx-overlay" onclick="if(event.target===this) closeBatchWizard();">
    <div class="sl-bx">
        <div class="sl-bx__head">
            <div class="sl-bx__title" id="slBxTitle">Batch Fix Billing</div>
            <button type="button" class="sl-bx__close" onclick="closeBatchWizard()">&times;</button>
        </div>
        <div class="sl-bx__steps">
            <div id="slBxStep1" class="sl-bx__step">1 Scan</div>
            <div id="slBxStep2" class="sl-bx__step">2 Summary</div>
            <div id="slBxStep3" class="sl-bx__step">3 Execute</div>
            <div id="slBxStep4" class="sl-bx__step">4 Results</div>
        </div>
        <div class="sl-bx__body">
            <div id="slBxPanel1" class="sl-bx__panel">
                <div class="sl-fx__note" id="slBxScanStatus">Scanning selected students for billing anomalies...</div>
                <div class="sl-bx__progress"><div class="sl-bx__progress-bar" id="slBxScanBar"></div></div>
                <div id="slBxScanWrap"></div>
            </div>
            <div id="slBxPanel2" class="sl-bx__panel">
                <div class="sl-fx__note">Aggregated summary of all detected issues across selected students.</div>
                <div id="slBxSummaryWrap"></div>
            </div>
            <div id="slBxPanel3" class="sl-bx__panel">
                <div class="sl-fx__note" id="slBxExecStatus">Processing billing fixes...</div>
                <div class="sl-bx__progress"><div class="sl-bx__progress-bar" id="slBxExecBar"></div></div>
                <div class="sl-bx__log" id="slBxExecLog"></div>
            </div>
            <div id="slBxPanel4" class="sl-bx__panel">
                <div class="sl-fx__note">Batch processing complete.</div>
                <div id="slBxResultWrap"></div>
            </div>
        </div>
        <div class="sl-bx__foot">
            <button id="slBxBack" type="button" class="sl-fx__btn sl-fx__btn--ghost" onclick="batchWizardBack()">Back</button>
            <div class="sl-bx__foot-right">
                <button id="slBxClose" type="button" class="sl-fx__btn sl-fx__btn--ghost" onclick="closeBatchWizard()">Close</button>
                <button id="slBxNext" type="button" class="sl-fx__btn sl-fx__btn--primary" onclick="batchWizardNext()">Next</button>
                <button id="slBxRun" type="button" class="sl-fx__btn sl-fx__btn--danger" onclick="batchWizardRun()">Run Batch Fix</button>
            </div>
        </div>
    </div>
</div>

<script type="text/javascript">
(function(){
    // Filters are visible by default (markup ships with sl-filters--open);
    // the Toggle Filters button lets the user collapse them to see more rows.

    window.toggleFilters = function(){
        var box = document.getElementById('slFilters');
        if (!box) return;
        if (box.className.indexOf('sl-filters--open') >= 0)
            box.className = 'sl-filters';
        else
            box.className = 'sl-filters sl-filters--open';
    };

    // ── GET-based navigation ────────────────────────────────────────────────
    // Every filter / sort / pagination / export action rebuilds the query string
    // and navigates (GET). No WebForms POST postback is used, so the listing is
    // fully bookmarkable and shareable.
    var SL_BASE = '<%= ResolveUrl("~/COOPERP/NewScreens/StudentLedgers.aspx") %>';

    function slEl(id){ return document.getElementById(id); }
    function slVal(id){ var e = slEl(id); return e ? String(e.value || '').replace(/^\s+|\s+$/g,'') : ''; }

    function slCurrentState(){
        var tx = slEl('<%= chkWithTransactions.ClientID %>');
        return {
            q:       slVal('<%= txtSearch.ClientID %>'),
            prog:    slVal('<%= ddlProgramme.ClientID %>'),
            entry:   slVal('<%= ddlEntryYear.ClientID %>'),
            acad:    slVal('<%= ddlAcadYear.ClientID %>'),
            sem:     slVal('<%= ddlSemester.ClientID %>'),
            bal:     slVal('<%= ddlBalanceState.ClientID %>'),
            balop:   slVal('<%= ddlBalanceOp.ClientID %>'),
            balfrom: slVal('<%= txtBalanceFrom.ClientID %>'),
            balto:   slVal('<%= txtBalanceTo.ClientID %>'),
            rows:    slVal('<%= ddlPageSize.ClientID %>'),
            tx:      (tx && tx.checked) ? '1' : '',
            sort:    slVal('<%= hfSortField.ClientID %>'),
            dir:     slVal('<%= hfSortDir.ClientID %>'),
            page:    slVal('<%= hfPageIndex.ClientID %>')
        };
    }

    function slBuildUrl(overrides){
        var p = slCurrentState();
        if (overrides) for (var k in overrides) if (overrides.hasOwnProperty(k)) p[k] = overrides[k];
        var parts = [];
        for (var key in p){
            if (!p.hasOwnProperty(key)) continue;
            var v = p[key];
            if (v === null || v === undefined || v === '') continue;
            parts.push(encodeURIComponent(key) + '=' + encodeURIComponent(v));
        }
        return parts.length ? (SL_BASE + '?' + parts.join('&')) : SL_BASE;
    }

    function slNavigate(overrides){ window.location.href = slBuildUrl(overrides); }

    window.applyFilters = function(){ slNavigate({ page: '0' }); };
    window.resetFilters = function(){ window.location.href = SL_BASE; };
    window.exportLedgers = function(fmt){ window.location.href = slBuildUrl({ export: fmt }); };

    window.setSort = function(field){
        var curField = slVal('<%= hfSortField.ClientID %>');
        var curDir = slVal('<%= hfSortDir.ClientID %>').toUpperCase();
        var dir = (curField === field) ? (curDir === 'ASC' ? 'DESC' : 'ASC') : 'ASC';
        slNavigate({ sort: field, dir: dir, page: '0' });
    };

    window.goPage = function(page){ slNavigate({ page: String(page) }); };

    // Pressing Enter anywhere in the filter panel applies the filters (it never
    // submits the page / exports). Standalone dropdown filters apply on change.
    (function(){
        var panel = document.getElementById('slFilters');
        if (panel){
            panel.addEventListener('keydown', function(ev){
                if (ev.key === 'Enter' || ev.keyCode === 13){ ev.preventDefault(); applyFilters(); }
            });
        }
        // Standalone dropdowns auto-apply (NOT the balance operator, which pairs
        // with the amount inputs and is applied via the Apply button / Enter).
        var ddlIds = ['<%= ddlProgramme.ClientID %>','<%= ddlEntryYear.ClientID %>','<%= ddlAcadYear.ClientID %>','<%= ddlSemester.ClientID %>','<%= ddlBalanceState.ClientID %>'];
        for (var j=0;j<ddlIds.length;j++){
            var d = slEl(ddlIds[j]);
            if (d) d.addEventListener('change', function(){ applyFilters(); });
        }
    })();

    window.toggleActionMenu = function(btn){
        var host = btn && btn.parentNode;
        if (!host) return;
        var open = host.className.indexOf('sl-actions--open') >= 0;
        var all = document.querySelectorAll('.sl-actions');
        for (var i=0;i<all.length;i++) all[i].className = 'sl-actions';
        host.className = open ? 'sl-actions' : 'sl-actions sl-actions--open';
    };

    document.addEventListener('click', function(e){
        var menus = document.querySelectorAll('.sl-actions');
        for (var i=0;i<menus.length;i++){
            if (!menus[i].contains(e.target)) menus[i].className = 'sl-actions';
        }
    });

    function esc(v){
        if (v === null || v === undefined) return '';
        return String(v).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
    }

    function money(v){
        var n = Number(v || 0);
        var isCredit = n < 0;
        if (isCredit) n = Math.abs(n);
        var txt;
        try { txt = n.toLocaleString(); }
        catch(ex){ txt = '' + n; }
        return isCredit ? (txt + ' CR') : txt;
    }

    // Signed balance: POSITIVE = overpaid (credit "CR"), NEGATIVE = owing (debit "DR").
    function balanceFmt(v){
        var n = Number(v || 0);
        var txt;
        try { txt = Math.abs(n).toLocaleString(); }
        catch(ex){ txt = '' + Math.abs(n); }
        if (n > 0) return txt + ' CR';
        if (n < 0) return txt + ' DR';
        return '0';
    }

    function balClass(v){
        var n = Number(v || 0);
        if (n > 0) return 'sl-amt--bal-dr';
        if (n < 0) return 'sl-amt--bal-cr';
        return 'sl-amt--bal-zero';
    }

    function balanceSign(balanceText){
        var t = String(balanceText || '').toUpperCase();
        if (t.indexOf('CR') >= 0) return -1;
        var digits = t.replace(/[^0-9.-]/g, '');
        var n = Number(digits || 0);
        if (n === 0) return 0;
        return 1;
    }

    var _slCurrentRegno = '';

    window.printStatement = function(regno){
        if (!regno) return;
        window.open('/API/StudentLedgerExport.aspx?reg=' + encodeURIComponent(regno), '_blank');
    };

    window.viewLedgerDetails = function(regno){
        if (!regno) return false;
        _slCurrentRegno = regno;
        var url = '<%= ResolveUrl("~/COOPERP/NewScreens/StudentLedgers.aspx") %>?ajax=ledger&regno=' + encodeURIComponent(regno) + '&_ts=' + new Date().getTime();
        fetch(url, {
            credentials:'same-origin',
            cache: 'no-store',
            headers: { 'Accept': 'application/json' }
        })
            .then(function(r){ return r.text(); })
            .then(function(txt){
                var d;
                try {
                    d = JSON.parse(txt);
                } catch(parseErr) {
                    alert('Unable to parse ledger response. ' + parseErr.message);
                    return;
                }
                if (!d || !d.ok){
                    alert((d && d.error) ? d.error : 'Failed to load ledger details.');
                    return;
                }

                try {
                    document.getElementById('slModalTitle').innerHTML = 'Ledger Details - ' + esc(d.regno) + ' (' + esc(d.student_name) + ')';
                    document.getElementById('slKpiBilled').innerHTML = money(d.total_billed);
                    document.getElementById('slKpiPaid').innerHTML = money(d.total_paid);
                    document.getElementById('slKpiBalance').innerHTML = balanceFmt(d.total_balance);

                    var kpiBalCard = document.getElementById('slKpiCardBalance');
                    if (kpiBalCard){
                        var balVal = Number(d.total_balance || 0);
                        // + overpaid (credit / green), - owing (debit / red)
                        if (balVal > 0) kpiBalCard.className = 'sl-modal__kpi sl-modal__kpi--bal-credit';
                        else if (balVal < 0) kpiBalCard.className = 'sl-modal__kpi sl-modal__kpi--bal-debit';
                        else kpiBalCard.className = 'sl-modal__kpi sl-modal__kpi--bal-zero';
                    }

                    var tbody = document.getElementById('slLedgerRows');
                    var rows = d.rows || [];
                    if (rows.length === 0){
                        tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;color:#667085;">No transactions found.</td></tr>';
                    } else {
                        var html = [];
                        for (var i=0;i<rows.length;i++){
                            var r = rows[i];
                            html.push('<tr>' +
                                '<td>' + esc(r.idx) + '</td>' +
                                '<td>' + esc(r.date) + '</td>' +
                                '<td>' + esc(r.ref) + '</td>' +
                                '<td>' + esc(r.particulars) + '</td>' +
                                '<td class="sl-money sl-amt--dr">' + (Number(r.debit||0) > 0 ? money(r.debit) : '-') + '</td>' +
                                '<td class="sl-money sl-amt--cr">' + (Number(r.credit||0) > 0 ? money(r.credit) : '-') + '</td>' +
                                '<td class="sl-money ' + balClass(balanceSign(r.balance)) + '">' + esc(r.balance) + '</td>' +
                            '</tr>');
                        }
                        tbody.innerHTML = html.join('');
                    }

                    var ov = document.getElementById('slModalOverlay');
                    ov.className = 'sl-modal-overlay sl-modal-overlay--show';
                } catch(renderErr) {
                    alert('Ledger render error: ' + renderErr.message);
                }
            })
            .catch(function(err){ alert('Unable to load ledger details. ' + (err && err.message ? err.message : '')); });
        return false;
    };

    window.closeLedgerModal = function(){
        var ov = document.getElementById('slModalOverlay');
        if (!ov) return;
        ov.className = 'sl-modal-overlay';
    };

    var _fx = {
        step: 1,
        regno: '',
        scan: null,
        result: null
    };

    function fxEsc(v){ return esc(v); }

    function fxMoney(v){ return money(v); }

    function fxSetStep(step){
        _fx.step = step;
        for (var i=1;i<=4;i++){
            var tab = document.getElementById('slFxStep' + i);
            var panel = document.getElementById('slFxPanel' + i);
            tab.className = 'sl-fx__step' + (i < step ? ' sl-fx__step--done' : (i === step ? ' sl-fx__step--on' : ''));
            panel.className = 'sl-fx__panel' + (i === step ? ' sl-fx__panel--on' : '');
        }

        document.getElementById('slFxBack').style.display = (step > 1 && step < 4) ? '' : 'none';
        document.getElementById('slFxNext').style.display = (step === 1 || step === 2) ? '' : 'none';
        document.getElementById('slFxRun').style.display = (step === 3) ? '' : 'none';
        document.getElementById('slFxClose').style.display = (step === 4) ? '' : 'none';
    }

    function fxRenderDetect(){
        var w = document.getElementById('slFxDetectWrap');
        if (!_fx.scan){ w.innerHTML = '<div class="sl-fx__note">Loading...</div>'; return; }
        var s = _fx.scan;
        var html = '';
        html += '<div class="sl-fx__grid">';

        var ub = Number(s.unbilled_count || 0);
        var dr = Number(s.missing_dr_count || 0);
        var cr = Number(s.missing_cr_count || 0);
        var al = Number(s.align_count || 0);
        var nm = Number(s.normalise_count || 0);

        if (ub > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxEsc(ub) + '</b><span>Unbilled Semesters</span></div>';
        if (ub > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxMoney(s.unbilled_amount) + '</b><span>Unbilled Amount</span></div>';
        if (dr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxEsc(dr) + '</b><span>Missing GL Mirrors</span></div>';
        if (dr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxMoney(s.missing_dr_amount) + '</b><span>Unmirrored Amount</span></div>';
        if (cr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxEsc(cr) + '</b><span>Missing Income CR</span></div>';
        if (cr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxMoney(s.missing_cr_amount) + '</b><span>Unrecognised Revenue</span></div>';
        if (al > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxEsc(al) + '</b><span>Metadata Alignment</span></div>';
        if (nm > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxEsc(nm) + '</b><span>Account Type Fix</span></div>';

        var hb = Number(s.held_back_count || 0);
        if (hb > 0){
            html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxEsc(hb) + '</b><span>Unbilled (Held Back)</span></div>';
            html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + fxMoney(s.held_back_amount) + '</b><span>Held-Back Amount</span></div>';
        }

        if (ub + dr + cr + al + nm + hb === 0)
            html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>0</b><span>Issues Found</span></div>';

        html += '</div>';

        if (hb > 0){
            html += '<div class="sl-fx__alert">This student is <strong>' + fxEsc(s.student_status || 'not active') + '</strong>. ' +
                    'The semesters below were registered but never billed. They are shown but <strong>not billed automatically</strong> — ' +
                    'tick the confirmation on the next step if this debt is genuinely owed.</div>';
        }

        var rows = s.samples || [];
        if (rows.length > 0){
            html += '<table class="sl-fx__tbl"><thead><tr><th>Category</th><th>Period</th><th>Fee Type</th><th>Amount</th><th>Detail</th></tr></thead><tbody>';
            var catLabels = { unbilled:'Unbilled', unbilled_held:'Unbilled (held back)', missing_dr:'Missing DR', missing_cr:'Missing CR' };
            for (var i=0;i<rows.length;i++){
                var r = rows[i];
                html += '<tr><td>' + fxEsc(catLabels[r.cat] || r.cat) + '</td><td>' + fxEsc(r.period) + '</td><td>' + fxEsc(r.feeType) + '</td><td>' + fxMoney(r.amount) + '</td><td>' + fxEsc(r.detail) + '</td></tr>';
            }
            html += '</tbody></table>';
        } else if (ub + dr + cr + hb === 0) {
            html += '<div class="sl-fx__ok">No missing billing entries detected.</div>';
        }
        w.innerHTML = html;
    }

    function fxRenderSummary(){
        var s = _fx.scan || {};
        var w = document.getElementById('slFxSummaryWrap');
        var ub = Number(s.unbilled_count || 0);
        var dr = Number(s.missing_dr_count || 0);
        var cr = Number(s.missing_cr_count || 0);
        var al = Number(s.align_count || 0);
        var nm = Number(s.normalise_count || 0);
        var hb = Number(s.held_back_count || 0);
        var total = ub + dr + cr + al + nm + hb;

        var html = '<div class="sl-fx__summary">';
        html += '<div><strong>Student:</strong> ' + fxEsc(_fx.regno) + '</div>';
        if (hb > 0) html += '<div><strong>Unbilled, held back (' + fxEsc(s.student_status || 'not active') + '):</strong> ' + fxEsc(hb) + ' items (' + fxMoney(s.held_back_amount) + ')</div>';
        if (ub > 0) html += '<div><strong>Create bills for unbilled semesters:</strong> ' + fxEsc(ub) + ' items (' + fxMoney(s.unbilled_amount) + ')</div>';
        if (dr > 0) html += '<div><strong>Insert missing GL debit mirrors:</strong> ' + fxEsc(dr) + ' rows (' + fxMoney(s.missing_dr_amount) + ')</div>';
        if (cr > 0) html += '<div><strong>Insert missing income credit entries:</strong> ' + fxEsc(cr) + ' rows (' + fxMoney(s.missing_cr_amount) + ')</div>';
        if (al > 0) html += '<div><strong>Align billing metadata:</strong> ' + fxEsc(al) + ' rows</div>';
        if (nm > 0) html += '<div><strong>Normalise account types:</strong> ' + fxEsc(nm) + ' rows</div>';
        html += '</div>';
        if (total === 0)
            html += '<div class="sl-fx__ok">No anomalies detected. You can close the wizard.</div>';
        else
            html += '<div class="sl-fx__alert">Proceed only if these actions are expected for this student account.</div>';
        w.innerHTML = html;
    }

    function fxRenderConfirm(){
        var s = _fx.scan || {};
        var ub = Number(s.unbilled_count || 0);
        var dr = Number(s.missing_dr_count || 0);
        var cr = Number(s.missing_cr_count || 0);
        var al = Number(s.align_count || 0);
        var nm = Number(s.normalise_count || 0);
        var hb = Number(s.held_back_count || 0);
        var canRun = (ub + dr + cr + al + nm + hb) > 0;
        document.getElementById('slFxRun').disabled = !canRun;

        var wrap = document.getElementById('slFxConfirmWrap');
        if (!canRun){
            wrap.innerHTML = '<div class="sl-fx__ok">No repair action required.</div>';
            return;
        }

        var ops = [];
        if (ub > 0) ops.push('create ' + ub + ' unbilled fee entries (tracking + ledger DR + income CR)');
        if (dr > 0) ops.push('insert ' + dr + ' missing GL debit mirrors');
        if (cr > 0) ops.push('insert ' + cr + ' missing income account credits');
        if (al > 0) ops.push('align metadata on ' + al + ' rows');
        if (nm > 0) ops.push('normalise account type on ' + nm + ' rows');

        var html = '';
        if (ops.length)
            html += '<div class="sl-fx__summary">Click <strong>Run Fix</strong> to execute the following in one transaction:<ul><li>' + ops.join('</li><li>') + '</li></ul></div>';

        // Billing someone who has already left is a deliberate act, so it needs its own
        // tick — the rest of the repair runs either way.
        if (hb > 0){
            html += '<div class="sl-fx__alert" style="margin-top:8px;">' +
                    '<label style="display:flex;gap:8px;align-items:flex-start;cursor:pointer;">' +
                    '<input type="checkbox" id="slFxAllowInactive" style="margin-top:3px;" />' +
                    '<span>Also bill this <strong>' + fxEsc(s.student_status || 'inactive') + '</strong> student for the ' + fxEsc(hb) +
                    ' held-back item(s), ' + fxMoney(s.held_back_amount) + '. Only tick this if the student really attended those semesters and the fee is owed.</span>' +
                    '</label></div>';
        }
        if (!ops.length && hb > 0)
            html += '<div class="sl-fx__note">Nothing else needs repair on this account.</div>';

        wrap.innerHTML = html;
    }

    function fxRenderResult(){
        var r = _fx.result;
        var w = document.getElementById('slFxResultWrap');
        if (!r){ w.innerHTML = '<div class="sl-fx__alert">No result returned.</div>'; return; }

        var html = '<div class="sl-fx__grid">';
        var ub = Number(r.inserted_unbilled || 0);
        var dr = Number(r.inserted_dr || 0);
        var cr = Number(r.inserted_cr || 0);
        var al = Number(r.aligned || 0);
        var nm = Number(r.normalised || 0);

        if (ub > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + fxEsc(ub) + '</b><span>Unbilled Created</span></div>';
        if (dr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + fxEsc(dr) + '</b><span>DR Mirrors Inserted</span></div>';
        if (cr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + fxEsc(cr) + '</b><span>Income CR Inserted</span></div>';
        if (al > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + fxEsc(al) + '</b><span>Metadata Aligned</span></div>';
        if (nm > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + fxEsc(nm) + '</b><span>Account Types Fixed</span></div>';

        if (ub + dr + cr + al + nm === 0)
            html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>0</b><span>Changes Needed</span></div>';

        html += '<div class="sl-fx__kpi"><b>' + fxEsc(r.before_balance) + ' &rarr; ' + fxEsc(r.after_balance) + '</b><span>Balance</span></div>';
        html += '</div>';
        w.innerHTML = html;
    }

    window.runFixBilling = function(regno){
        if (!regno) return false;
        _fx = { step:1, regno:regno, scan:null, result:null };
        document.getElementById('slFixTitle').innerHTML = 'Fix Student Billing - ' + fxEsc(regno);
        document.getElementById('slFixOverlay').className = 'sl-fx-overlay sl-fx-overlay--show';
        fxSetStep(1);
        document.getElementById('slFxDetectWrap').innerHTML = '<div class="sl-fx__note">Scanning anomalies...</div>';

        var url = '<%= ResolveUrl("~/COOPERP/NewScreens/StudentLedgers.aspx") %>?ajax=fixbillingscan&regno=' + encodeURIComponent(regno) + '&_ts=' + new Date().getTime();
        fetch(url, { credentials:'same-origin', cache:'no-store' })
            .then(function(r){ return r.json(); })
            .then(function(d){
                if (!d || !d.ok){ alert((d && d.error) ? d.error : 'Scan failed.'); closeFixWizard(); return; }
                _fx.scan = d;
                fxRenderDetect();
                fxRenderSummary();
                fxRenderConfirm();
            })
            .catch(function(){ alert('Unable to scan billing anomalies.'); closeFixWizard(); });
        return false;
    };

    window.fixWizardNext = function(){
        if (_fx.step === 1) { fxSetStep(2); return; }
        if (_fx.step === 2) { fxSetStep(3); return; }
    };

    window.fixWizardBack = function(){
        if (_fx.step === 3) { fxSetStep(2); return; }
        if (_fx.step === 2) { fxSetStep(1); return; }
    };

    window.fixWizardRun = function(){
        if (!_fx.regno) return;
        var btn = document.getElementById('slFxRun');
        btn.disabled = true;
        btn.innerHTML = 'Processing...';

        // Only sent when the operator ticked the box; the server bills an inactive
        // student on this flag alone, never by default.
        var allowBox = document.getElementById('slFxAllowInactive');
        var allow = (allowBox && allowBox.checked) ? '&allowinactive=1' : '';

        var url = '<%= ResolveUrl("~/COOPERP/NewScreens/StudentLedgers.aspx") %>?ajax=fixbillingapply&regno=' + encodeURIComponent(_fx.regno) + allow + '&_ts=' + new Date().getTime();
        fetch(url, { credentials:'same-origin', cache:'no-store' })
            .then(function(r){ return r.json(); })
            .then(function(d){
                btn.disabled = false;
                btn.innerHTML = 'Run Fix';
                if (!d || !d.ok){ alert((d && d.error) ? d.error : 'Fix operation failed.'); return; }
                _fx.result = d;
                fxRenderResult();
                fxSetStep(4);
            })
            .catch(function(){
                btn.disabled = false;
                btn.innerHTML = 'Run Fix';
                alert('Unable to run billing fix.');
            });
    };

    window.closeFixWizard = function(){
        var ov = document.getElementById('slFixOverlay');
        ov.className = 'sl-fx-overlay';
        if (_fx && _fx.result) {
            slNavigate({});
        }
    };

    window.runRemoveDouble = function(regno){
        if (!regno) return false;
        if (!confirm('Remove duplicate billing entries for ' + regno + '?')) return false;
        var url = '<%= ResolveUrl("~/COOPERP/NewScreens/StudentLedgers.aspx") %>?ajax=removedouble&regno=' + encodeURIComponent(regno);
        fetch(url, { credentials:'same-origin' })
            .then(function(r){ return r.json(); })
            .then(function(d){
                if (!d || !d.ok){ alert((d && d.error) ? d.error : 'Duplicate removal failed.'); return; }
                alert('Removed ' + d.deleted + ' duplicate ledger rows for ' + regno + '. Balance before: ' + d.balance_before + ', after: ' + d.balance_after + '.');
                slNavigate({});
            })
            .catch(function(){ alert('Unable to remove double billing.'); });
        return false;
    };

    // =========================================================================
    //  BATCH SELECTION
    // =========================================================================
    function getRowCheckboxes(){ return document.querySelectorAll('.sl-row-chk'); }

    function getSelectedRegnos(){
        var cbs = getRowCheckboxes(), arr = [];
        for (var i=0;i<cbs.length;i++) if (cbs[i].checked) arr.push(cbs[i].getAttribute('data-regno'));
        return arr;
    }

    window.batchToggleAll = function(src){
        var cbs = getRowCheckboxes();
        for (var i=0;i<cbs.length;i++) cbs[i].checked = src.checked;
        var ha = document.getElementById('slSelectAllHead');
        var ba = document.getElementById('slSelectAll');
        if (ha) ha.checked = src.checked;
        if (ba) ba.checked = src.checked;
        batchUpdateCount();
    };

    window.batchUpdateCount = function(){
        var n = getSelectedRegnos().length;
        document.getElementById('slBatchCount').innerHTML = n;
        var bar = document.getElementById('slBatchBar');
        bar.className = n > 0 ? 'sl-batch-bar sl-batch-bar--show' : 'sl-batch-bar';
        var ha = document.getElementById('slSelectAllHead');
        var ba = document.getElementById('slSelectAll');
        var total = getRowCheckboxes().length;
        if (ha) ha.checked = (n > 0 && n === total);
        if (ba) ba.checked = (n > 0 && n === total);
    };

    // =========================================================================
    //  BATCH WIZARD STATE
    // =========================================================================
    var _bx = {
        step: 1,
        regnos: [],
        scans: {},            // { regno: scanResult }
        scanDone: 0,
        scanErrors: [],
        results: {},          // { regno: applyResult }
        execDone: 0,
        execErrors: [],
        running: false
    };

    var _bxBaseUrl = '<%= ResolveUrl("~/COOPERP/NewScreens/StudentLedgers.aspx") %>';

    function bxSetStep(step){
        _bx.step = step;
        for (var i=1;i<=4;i++){
            var tab = document.getElementById('slBxStep' + i);
            var panel = document.getElementById('slBxPanel' + i);
            tab.className = 'sl-bx__step' + (i < step ? ' sl-bx__step--done' : (i === step ? ' sl-bx__step--on' : ''));
            panel.className = 'sl-bx__panel' + (i === step ? ' sl-bx__panel--on' : '');
        }
        document.getElementById('slBxBack').style.display = (step === 2) ? '' : 'none';
        document.getElementById('slBxNext').style.display = (step === 2) ? '' : 'none';
        document.getElementById('slBxRun').style.display = (step === 3 && !_bx.running) ? '' : 'none';
        document.getElementById('slBxClose').style.display = (step === 4) ? '' : 'none';
    }

    // ---- Step 1: Sequential scan ----
    function bxRunScan(){
        _bx.scanDone = 0;
        _bx.scanErrors = [];
        _bx.scans = {};
        document.getElementById('slBxScanWrap').innerHTML = '';
        bxUpdateScanProgress();
        bxScanNext();
    }

    function bxUpdateScanProgress(){
        var pct = _bx.regnos.length > 0 ? Math.round((_bx.scanDone / _bx.regnos.length) * 100) : 0;
        document.getElementById('slBxScanBar').style.width = pct + '%';
        document.getElementById('slBxScanStatus').innerHTML = 'Scanning ' + _bx.scanDone + ' / ' + _bx.regnos.length + ' students...';
    }

    function bxScanNext(){
        if (_bx.scanDone >= _bx.regnos.length){
            document.getElementById('slBxScanStatus').innerHTML = 'Scan complete. ' + _bx.regnos.length + ' students scanned' +
                (_bx.scanErrors.length > 0 ? ' (' + _bx.scanErrors.length + ' errors)' : '') + '.';
            bxRenderScanTable();
            bxSetStep(1);
            // auto-advance if no errors
            if (_bx.scanErrors.length === 0) setTimeout(function(){ bxSetStep(2); bxRenderSummary(); bxRenderConfirm(); }, 400);
            else { bxRenderSummary(); bxRenderConfirm(); }
            return;
        }
        var reg = _bx.regnos[_bx.scanDone];
        var url = _bxBaseUrl + '?ajax=fixbillingscan&regno=' + encodeURIComponent(reg) + '&_ts=' + new Date().getTime();
        fetch(url, { credentials:'same-origin', cache:'no-store' })
            .then(function(r){ return r.json(); })
            .then(function(d){
                if (d && d.ok) _bx.scans[reg] = d;
                else _bx.scanErrors.push({ regno:reg, error:(d && d.error) || 'Scan failed' });
            })
            .catch(function(err){ _bx.scanErrors.push({ regno:reg, error:err && err.message ? err.message : 'Network error' }); })
            .then(function(){
                _bx.scanDone++;
                bxUpdateScanProgress();
                bxScanNext();
            });
    }

    function bxRenderScanTable(){
        var w = document.getElementById('slBxScanWrap');
        var html = '<table class="sl-fx__tbl"><thead><tr><th>Reg No</th><th>Unbilled</th><th>Missing DR</th><th>Missing CR</th><th>Align</th><th>Normalise</th><th>Status</th></tr></thead><tbody>';
        for (var i=0;i<_bx.regnos.length;i++){
            var reg = _bx.regnos[i];
            var s = _bx.scans[reg];
            if (s){
                var issues = Number(s.unbilled_count||0) + Number(s.missing_dr_count||0) + Number(s.missing_cr_count||0) + Number(s.align_count||0) + Number(s.normalise_count||0);
                var cls = issues > 0 ? 'sl-fx__kpi--warn' : '';
                html += '<tr><td>' + esc(reg) + '</td>' +
                    '<td>' + esc(s.unbilled_count||0) + '</td>' +
                    '<td>' + esc(s.missing_dr_count||0) + '</td>' +
                    '<td>' + esc(s.missing_cr_count||0) + '</td>' +
                    '<td>' + esc(s.align_count||0) + '</td>' +
                    '<td>' + esc(s.normalise_count||0) + '</td>' +
                    '<td>' + (issues > 0 ? '<b style="color:#c2410c;">' + issues + ' issues</b>' : '<span style="color:#166534;">OK</span>') + '</td></tr>';
            } else {
                var errMsg = '';
                for (var e=0;e<_bx.scanErrors.length;e++) if (_bx.scanErrors[e].regno===reg) errMsg = _bx.scanErrors[e].error;
                html += '<tr><td>' + esc(reg) + '</td><td colspan="5"></td><td style="color:#b91c1c;">Error: ' + esc(errMsg) + '</td></tr>';
            }
        }
        html += '</tbody></table>';
        w.innerHTML = html;
    }

    // ---- Step 2: Aggregated summary ----
    function bxRenderSummary(){
        var w = document.getElementById('slBxSummaryWrap');
        var totals = { ub:0, ubAmt:0, dr:0, drAmt:0, cr:0, crAmt:0, al:0, nm:0, affected:0 };
        for (var reg in _bx.scans){
            var s = _bx.scans[reg];
            var u = Number(s.unbilled_count||0), d = Number(s.missing_dr_count||0), c = Number(s.missing_cr_count||0), a = Number(s.align_count||0), n = Number(s.normalise_count||0);
            totals.ub += u; totals.ubAmt += Number(s.unbilled_amount||0);
            totals.dr += d; totals.drAmt += Number(s.missing_dr_amount||0);
            totals.cr += c; totals.crAmt += Number(s.missing_cr_amount||0);
            totals.al += a; totals.nm += n;
            if (u+d+c+a+n > 0) totals.affected++;
        }
        var total = totals.ub + totals.dr + totals.cr + totals.al + totals.nm;

        var html = '<div class="sl-fx__grid">';
        html += '<div class="sl-fx__kpi"><b>' + esc(_bx.regnos.length) + '</b><span>Students Scanned</span></div>';
        html += '<div class="sl-fx__kpi' + (totals.affected>0?' sl-fx__kpi--warn':'') + '"><b>' + esc(totals.affected) + '</b><span>Students With Issues</span></div>';
        html += '<div class="sl-fx__kpi"><b>' + esc(_bx.regnos.length - totals.affected) + '</b><span>Already Correct</span></div>';
        if (totals.ub > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + esc(totals.ub) + ' (' + money(totals.ubAmt) + ')</b><span>Unbilled Items</span></div>';
        if (totals.dr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + esc(totals.dr) + ' (' + money(totals.drAmt) + ')</b><span>Missing DR Mirrors</span></div>';
        if (totals.cr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + esc(totals.cr) + ' (' + money(totals.crAmt) + ')</b><span>Missing Income CR</span></div>';
        if (totals.al > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + esc(totals.al) + '</b><span>Metadata Alignment</span></div>';
        if (totals.nm > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + esc(totals.nm) + '</b><span>Account Type Fix</span></div>';
        html += '</div>';

        if (total === 0)
            html += '<div class="sl-fx__ok">All selected students have correct billing. No action needed.</div>';
        else
            html += '<div class="sl-fx__alert"><strong>' + esc(totals.affected) + '</strong> student(s) require billing repair. Review carefully before proceeding.</div>';

        if (_bx.scanErrors.length > 0)
            html += '<div class="sl-fx__alert">' + esc(_bx.scanErrors.length) + ' student(s) failed to scan and will be skipped.</div>';

        w.innerHTML = html;
    }

    function bxRenderConfirm(){
        var hasIssues = false;
        for (var reg in _bx.scans){
            var s = _bx.scans[reg];
            if (Number(s.unbilled_count||0)+Number(s.missing_dr_count||0)+Number(s.missing_cr_count||0)+Number(s.align_count||0)+Number(s.normalise_count||0) > 0){ hasIssues = true; break; }
        }
        var btn = document.getElementById('slBxRun');
        btn.disabled = !hasIssues;
        btn.style.display = hasIssues ? '' : 'none';
    }

    // ---- Step 3: Sequential apply ----
    function bxRunApply(){
        _bx.running = true;
        _bx.execDone = 0;
        _bx.execErrors = [];
        _bx.results = {};
        document.getElementById('slBxExecLog').innerHTML = '';
        document.getElementById('slBxRun').style.display = 'none';
        bxUpdateExecProgress();
        bxApplyNext();
    }

    function bxUpdateExecProgress(){
        var applyList = bxGetApplyList();
        var pct = applyList.length > 0 ? Math.round((_bx.execDone / applyList.length) * 100) : 100;
        document.getElementById('slBxExecBar').style.width = pct + '%';
        document.getElementById('slBxExecStatus').innerHTML = 'Processing ' + _bx.execDone + ' / ' + applyList.length + ' students...';
    }

    function bxGetApplyList(){
        var list = [];
        for (var i=0;i<_bx.regnos.length;i++){
            var reg = _bx.regnos[i];
            var s = _bx.scans[reg];
            if (!s) continue;
            var issues = Number(s.unbilled_count||0)+Number(s.missing_dr_count||0)+Number(s.missing_cr_count||0)+Number(s.align_count||0)+Number(s.normalise_count||0);
            if (issues > 0) list.push(reg);
        }
        return list;
    }

    function bxApplyNext(){
        var applyList = bxGetApplyList();
        if (_bx.execDone >= applyList.length){
            _bx.running = false;
            document.getElementById('slBxExecStatus').innerHTML = 'Complete. ' + applyList.length + ' students processed' +
                (_bx.execErrors.length > 0 ? ' (' + _bx.execErrors.length + ' errors)' : '') + '.';
            bxRenderFinalResults();
            bxSetStep(4);
            return;
        }
        var reg = applyList[_bx.execDone];
        var log = document.getElementById('slBxExecLog');
        log.innerHTML += '<div>Processing ' + esc(reg) + '...</div>';
        log.scrollTop = log.scrollHeight;

        var url = _bxBaseUrl + '?ajax=fixbillingapply&regno=' + encodeURIComponent(reg) + '&_ts=' + new Date().getTime();
        fetch(url, { credentials:'same-origin', cache:'no-store' })
            .then(function(r){ return r.json(); })
            .then(function(d){
                if (d && d.ok){
                    _bx.results[reg] = d;
                    var total = Number(d.inserted_unbilled||0)+Number(d.inserted_dr||0)+Number(d.inserted_cr||0)+Number(d.aligned||0)+Number(d.normalised||0);
                    log.innerHTML += '<div class="sl-bx__log-ok">  ' + esc(reg) + ': OK — ' + total + ' changes, balance ' + esc(d.before_balance) + ' → ' + esc(d.after_balance) + '</div>';
                } else {
                    _bx.execErrors.push({ regno:reg, error:(d && d.error) || 'Apply failed' });
                    log.innerHTML += '<div class="sl-bx__log-err">  ' + esc(reg) + ': ERROR — ' + esc((d && d.error) || 'Apply failed') + '</div>';
                }
            })
            .catch(function(err){
                _bx.execErrors.push({ regno:reg, error:err && err.message ? err.message : 'Network error' });
                log.innerHTML += '<div class="sl-bx__log-err">  ' + esc(reg) + ': ERROR — ' + esc(err && err.message ? err.message : 'Network error') + '</div>';
            })
            .then(function(){
                _bx.execDone++;
                bxUpdateExecProgress();
                log.scrollTop = log.scrollHeight;
                bxApplyNext();
            });
    }

    // ---- Step 4: Final results ----
    function bxRenderFinalResults(){
        var w = document.getElementById('slBxResultWrap');
        var tUb=0, tDr=0, tCr=0, tAl=0, tNm=0, tOk=0, tFail=_bx.execErrors.length;
        for (var reg in _bx.results){
            var r = _bx.results[reg]; tOk++;
            tUb += Number(r.inserted_unbilled||0);
            tDr += Number(r.inserted_dr||0);
            tCr += Number(r.inserted_cr||0);
            tAl += Number(r.aligned||0);
            tNm += Number(r.normalised||0);
        }
        var html = '<div class="sl-fx__grid">';
        html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + esc(tOk) + '</b><span>Students Fixed</span></div>';
        if (tFail > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--warn"><b>' + esc(tFail) + '</b><span>Errors</span></div>';
        if (tUb > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + esc(tUb) + '</b><span>Unbilled Created</span></div>';
        if (tDr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + esc(tDr) + '</b><span>DR Mirrors Inserted</span></div>';
        if (tCr > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + esc(tCr) + '</b><span>Income CR Inserted</span></div>';
        if (tAl > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + esc(tAl) + '</b><span>Metadata Aligned</span></div>';
        if (tNm > 0) html += '<div class="sl-fx__kpi sl-fx__kpi--ok"><b>' + esc(tNm) + '</b><span>Account Types Fixed</span></div>';
        html += '</div>';

        // Per-student breakdown table
        html += '<table class="sl-fx__tbl" style="margin-top:10px;"><thead><tr><th>Reg No</th><th>Unbilled</th><th>DR</th><th>CR</th><th>Align</th><th>Norm</th><th>Balance Before</th><th>Balance After</th><th>Status</th></tr></thead><tbody>';
        var applyList = bxGetApplyList();
        for (var i=0;i<applyList.length;i++){
            var rg = applyList[i];
            var res = _bx.results[rg];
            if (res){
                html += '<tr><td>' + esc(rg) + '</td>' +
                    '<td>' + esc(res.inserted_unbilled||0) + '</td>' +
                    '<td>' + esc(res.inserted_dr||0) + '</td>' +
                    '<td>' + esc(res.inserted_cr||0) + '</td>' +
                    '<td>' + esc(res.aligned||0) + '</td>' +
                    '<td>' + esc(res.normalised||0) + '</td>' +
                    '<td>' + esc(res.before_balance) + '</td>' +
                    '<td>' + esc(res.after_balance) + '</td>' +
                    '<td class="sl-bx__log-ok">OK</td></tr>';
            } else {
                var errMsg = '';
                for (var e=0;e<_bx.execErrors.length;e++) if (_bx.execErrors[e].regno===rg) errMsg = _bx.execErrors[e].error;
                html += '<tr><td>' + esc(rg) + '</td><td colspan="7"></td><td class="sl-bx__log-err">' + esc(errMsg) + '</td></tr>';
            }
        }
        html += '</tbody></table>';
        w.innerHTML = html;
    }

    // ---- Wizard navigation ----
    window.batchRunFixBilling = function(){
        var regnos = getSelectedRegnos();
        if (regnos.length === 0){ alert('No students selected.'); return; }
        _bx = {
            step:1, regnos:regnos, scans:{}, scanDone:0, scanErrors:[],
            results:{}, execDone:0, execErrors:[], running:false
        };
        document.getElementById('slBxTitle').innerHTML = 'Batch Fix Billing — ' + esc(regnos.length) + ' Students';
        document.getElementById('slBxOverlay').className = 'sl-bx-overlay sl-bx-overlay--show';
        bxSetStep(1);
        bxRunScan();
    };

    window.batchWizardNext = function(){
        if (_bx.step === 2){ bxSetStep(3); bxRenderConfirm(); return; }
    };

    window.batchWizardBack = function(){
        if (_bx.step === 2){ bxSetStep(1); bxRenderScanTable(); return; }
    };

    window.batchWizardRun = function(){
        bxRunApply();
    };

    window.closeBatchWizard = function(){
        if (_bx.running){ if(!confirm('Processing is in progress. Close anyway?')) return; }
        document.getElementById('slBxOverlay').className = 'sl-bx-overlay';
        if (_bx.execDone > 0) slNavigate({});
    };
})();
</script>
</asp:Content>
