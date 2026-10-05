<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ContractRenewals.aspx.cs" Inherits="COOPERP_NewScreens_ContractRenewals" Title="Contract Renewals - Campus Dynamics" %>
<%@ Reference Page="~/COOPERP/NewScreens/ContractRenewalView.aspx" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<style>
/* ===== CONTRACT RENEWALS - HR CONSOLE ===== */
.cr-page{--navy:#05275C;--navy-h:#041d45;--accent:#174DA4;--surface:#f5f7fa;--border:#e0e5ed;--border-in:#cdd3de;--text:#1a1a2e;--text2:#555;--muted:#888;--danger:#dc3545;--warn:#d97706;--ok:#16a34a;
    font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;font-size:12px;color:var(--text);padding-bottom:60px;}
.cr-page *,.cr-page *::before,.cr-page *::after{box-sizing:border-box;}
.cr-head{display:flex;align-items:center;gap:12px;margin-bottom:12px;flex-wrap:wrap;}
.cr-head__icon{width:40px;height:40px;background:var(--navy);color:#fff;display:flex;align-items:center;justify-content:center;flex-shrink:0;border-radius:4px;}
.cr-head__title{font-size:20px;font-weight:700;line-height:1.2;}
.cr-head__sub{font-size:12px;color:var(--muted);margin-top:2px;}
.cr-head__actions{margin-left:auto;display:flex;gap:8px;flex-wrap:wrap;}
.cr-round{background:#fff;border:1px solid var(--border);border-left:3px solid var(--accent);padding:9px 14px;margin-bottom:12px;border-radius:4px;}
.cr-round__t{font-weight:700;color:var(--navy);}
.cr-round__m{font-size:11px;color:var(--text2);margin-top:2px;line-height:1.7;}
.cr-kpis{display:grid;grid-template-columns:repeat(7,minmax(0,1fr));gap:1px;background:var(--border);border:1px solid var(--border);margin-bottom:14px;}
@media(max-width:1200px){.cr-kpis{grid-template-columns:repeat(4,minmax(0,1fr));}}
@media(max-width:700px){.cr-kpis{grid-template-columns:repeat(2,minmax(0,1fr));}}
.cr-kpi{background:#fff;padding:10px 12px;border-left:3px solid var(--navy);text-decoration:none;color:inherit;display:block;min-width:0;}
a.cr-kpi:hover{background:#f7f9fc;}
.cr-kpi--red{border-left-color:var(--danger);}.cr-kpi--red .cr-kpi__val{color:var(--danger);}
.cr-kpi--grey{border-left-color:#9ca3af;}
.cr-kpi--blue{border-left-color:#5b8bd6;}
.cr-kpi--accent{border-left-color:var(--accent);}
.cr-kpi--amber{border-left-color:var(--warn);}
.cr-kpi--green{border-left-color:var(--ok);}
.cr-kpi__val{font-size:20px;font-weight:700;line-height:1.15;}
.cr-kpi__lbl{font-size:10px;color:var(--muted);text-transform:uppercase;letter-spacing:.4px;margin-top:2px;}
.cr-kpi__sub{font-size:10px;color:var(--text2);margin-top:2px;}
.cr-tabs{display:flex;gap:2px;background:#f0f2f5;border-bottom:2px solid var(--border);padding:0 10px;margin-bottom:12px;flex-wrap:wrap;}
.cr-tab{padding:9px 16px;font-size:12px;font-weight:500;color:#555;text-decoration:none;border-bottom:2px solid transparent;margin-bottom:-2px;}
.cr-tab:hover{color:var(--navy);}
.cr-tab--active{color:var(--navy);border-bottom-color:var(--navy);font-weight:600;}
.cr-filters{display:flex;gap:6px;flex-wrap:wrap;align-items:center;margin-bottom:12px;}
.cr-input{font-size:12px;padding:6px 8px;border:1px solid var(--border-in);border-radius:0;background:#fff;color:var(--text);font-family:inherit;max-width:100%;}
.cr-input:focus{outline:none;border-color:var(--accent);box-shadow:0 0 0 2px rgba(23,77,164,.15);}
.cr-input--search{min-width:200px;flex:1 1 200px;}
.cr-inline{display:inline-flex;align-items:center;gap:5px;font-size:12px;color:var(--text2);}
.cr-card{background:#fff;border:1px solid var(--border);border-radius:4px;margin-bottom:14px;min-width:0;}
.cr-card__hdr{padding:10px 14px;border-bottom:1px solid var(--border);font-size:11px;font-weight:700;color:var(--text2);text-transform:uppercase;letter-spacing:.4px;background:var(--surface);display:flex;gap:8px;align-items:center;flex-wrap:wrap;}
.cr-card__hdr-right{margin-left:auto;font-weight:400;text-transform:none;letter-spacing:0;color:var(--muted);display:flex;gap:6px;align-items:center;flex-wrap:wrap;}
.cr-table-wrap{overflow-x:auto;}
.cr-table{width:100%;border-collapse:collapse;font-size:12px;}
.cr-table th{text-align:left;padding:7px 10px;background:var(--surface);border-bottom:1px solid var(--border);color:var(--text2);font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;white-space:nowrap;}
.cr-table td{padding:7px 10px;border-bottom:1px solid #f0f2f5;vertical-align:top;}
.cr-table tr:hover td{background:#fafbfd;}
.cr-cb{width:28px;}
.cr-num{text-align:right;font-variant-numeric:tabular-nums;}
.cr-nowrap{white-space:nowrap;}
.cr-strong{font-weight:600;}
.cr-muted{color:var(--muted);font-size:11px;}
.cr-empty{padding:18px;text-align:center;color:var(--muted);font-style:italic;}
.cr-link{color:var(--accent);text-decoration:none;}
.cr-link:hover{text-decoration:underline;}
.cr-code{display:inline-block;font-family:Consolas,monospace;font-size:10px;padding:1px 5px;background:rgba(23,77,164,.07);border:1px solid rgba(23,77,164,.15);color:var(--accent);border-radius:0;font-weight:600;white-space:nowrap;}
.cr-badge{display:inline-block;font-size:10px;font-weight:600;padding:2px 7px;border-radius:0;text-transform:uppercase;letter-spacing:.3px;white-space:nowrap;}
.cr-badge--green{background:#e6f4ea;color:#155724;border:1px solid #c3e6cb;}
.cr-badge--amber{background:#fff8e1;color:#b45309;border:1px solid #fcd34d;}
.cr-badge--red{background:#fef5f5;color:#dc3545;border:1px solid #f5c6cb;}
.cr-badge--primary{background:rgba(5,39,92,.08);color:#05275C;border:1px solid rgba(5,39,92,.2);}
.cr-badge--blue{background:rgba(23,77,164,.08);color:#174DA4;border:1px solid rgba(23,77,164,.2);}
.cr-badge--accent{background:#174DA4;color:#fff;border:1px solid #174DA4;}
.cr-badge--grey{background:#f1f3f6;color:#6b7280;border:1px solid #d1d5db;}
.cr-days{display:inline-block;font-size:10px;font-weight:700;padding:1px 6px;border-radius:0;white-space:nowrap;}
.cr-days--red{background:#fef5f5;color:#dc3545;border:1px solid #f5c6cb;}
.cr-days--amber{background:#fff8e1;color:#b45309;border:1px solid #fcd34d;}
.cr-days--ok{background:#f1f3f6;color:#555;border:1px solid #e0e5ed;}
.cr-btn{display:inline-flex;align-items:center;gap:5px;padding:6px 12px;font-size:12px;font-weight:600;border:1px solid var(--border-in);background:#fff;color:var(--text2);cursor:pointer;border-radius:0;white-space:nowrap;font-family:inherit;text-decoration:none;}
.cr-btn:hover{border-color:var(--accent);color:var(--accent);}
.cr-btn--primary{background:var(--navy);border-color:var(--navy);color:#fff;}
.cr-btn--primary:hover{background:var(--navy-h);border-color:var(--navy-h);color:#fff;}
.cr-btn--danger-outline{color:var(--danger);border-color:#f1b0b7;}
.cr-btn--danger-outline:hover{background:#fff5f5;color:var(--danger);border-color:var(--danger);}
.cr-btn--sm{padding:3px 8px;font-size:11px;margin-right:3px;}
.cr-btn:disabled{opacity:.5;cursor:not-allowed;}
.cr-icon-btn{display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border:1px solid var(--border-in);background:#fff;color:#666;border-radius:0;margin-left:2px;}
.cr-icon-btn:hover{border-color:var(--accent);color:var(--accent);background:#eef3fb;}
.cr-alert{padding:10px 14px;margin-bottom:12px;font-size:12px;border:1px solid;line-height:1.5;}
.cr-alert--error{background:#f8d7da;color:#721c24;border-color:#f5c6cb;}
.cr-alert--warn{background:#fff8e1;color:#7a4a05;border-color:#fcd34d;}
.cr-alert--info{background:#eef3fb;color:#05275C;border-color:#c5d3e8;}
.cr-batch{position:fixed;bottom:0;left:0;right:0;background:var(--navy);color:#fff;padding:10px 20px;display:none;align-items:center;gap:12px;z-index:500;border-top:3px solid var(--navy-h);flex-wrap:wrap;}
.cr-batch.is-on{display:flex;}
.cr-batch__count{font-size:15px;font-weight:700;}
.cr-batch__actions{margin-left:auto;display:flex;gap:8px;flex-wrap:wrap;}
.cr-batch-btn{padding:7px 14px;font-size:11px;font-weight:600;border:none;cursor:pointer;border-radius:0;background:#fff;color:var(--navy);font-family:inherit;}
.cr-batch-btn--ghost{background:transparent;color:#fff;border:1px solid rgba(255,255,255,.45);}
.cr-modal-ov{display:none;position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:9999;align-items:center;justify-content:center;padding:16px;}
.cr-modal-ov.is-open{display:flex;}
.cr-modal{background:#fff;border-radius:2px;width:560px;max-width:100%;max-height:92vh;overflow-y:auto;box-shadow:0 10px 32px rgba(0,0,0,.2);}
.cr-modal__hdr{padding:12px 16px;display:flex;align-items:center;background:var(--navy);color:#fff;}
.cr-modal__hdr h3{margin:0;font-size:14px;font-weight:700;flex:1;}
.cr-modal__close{background:none;border:none;color:#fff;font-size:20px;cursor:pointer;line-height:1;padding:0 4px;}
.cr-modal__body{padding:16px;}
.cr-modal__foot{padding:12px 16px;border-top:1px solid var(--border);display:flex;justify-content:flex-end;gap:8px;flex-wrap:wrap;}
.cr-field{margin-bottom:12px;}
.cr-field label{display:block;font-size:10px;font-weight:700;color:var(--text2);text-transform:uppercase;letter-spacing:.4px;margin-bottom:4px;}
.cr-field input,.cr-field select,.cr-field textarea{font-size:12px;padding:6px 8px;border:1px solid var(--border-in);border-radius:0;background:#fff;color:var(--text);font-family:inherit;width:100%;}
.cr-field input:focus,.cr-field select:focus,.cr-field textarea:focus{outline:none;border-color:var(--accent);box-shadow:0 0 0 2px rgba(23,77,164,.15);}
.cr-field textarea{min-height:70px;resize:vertical;}
.cr-field-row{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:12px;}
@media(max-width:520px){.cr-field-row{grid-template-columns:minmax(0,1fr);}}
.cr-hint{font-size:11px;color:var(--muted);margin-top:3px;line-height:1.5;}
.cr-check{display:flex;align-items:flex-start;gap:8px;padding:7px 9px;border:1px solid var(--border);margin-bottom:8px;cursor:pointer;line-height:1.45;}
.cr-check--confirm{background:#fff8e1;border-color:#fcd34d;}
.cr-result{max-height:200px;overflow-y:auto;font-size:11px;border:1px solid var(--border);padding:8px 10px;background:var(--surface);margin-top:8px;display:none;}
.cr-result.is-on{display:block;}
.cr-result div{padding:2px 0;}
.cr-progress{height:6px;background:#eef1f6;margin:10px 0;}
.cr-progress__bar{height:6px;background:var(--accent);width:0;transition:width .2s;}
.cr-toast{position:fixed;bottom:70px;right:20px;padding:10px 16px;font-size:12px;font-weight:600;color:#fff;z-index:10000;opacity:0;transition:opacity .25s;pointer-events:none;max-width:420px;}
.cr-toast.is-on{opacity:1;}
.cr-toast--ok{background:#16a34a;}
.cr-toast--err{background:var(--danger);}
.cr-s-ok{color:#155724;}.cr-s-err{color:#dc3545;}.cr-s-warn{color:#b45309;}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="cr-page">
<asp:Literal ID="litError" runat="server" />

<asp:Panel ID="pnlMain" runat="server">
<asp:HiddenField ID="hfDefaultSitting" runat="server" Value="" />

<div class="cr-head">
    <div class="cr-head__icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>
    </div>
    <div>
        <div class="cr-head__title">Contract Renewals</div>
        <div class="cr-head__sub">Applications for contract renewal: supervisor recommendation, HR verification, Governance Council decision and the new contract</div>
    </div>
    <div class="cr-head__actions">
        <a href="HRContracts.aspx" class="cr-btn">Contracts</a>
        <button type="button" class="cr-btn" onclick="openSchedule('print')">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
            Council schedule
        </button>
        <button type="button" class="cr-btn" onclick="openSchedule('csv')">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
            CSV
        </button>
    </div>
</div>

<asp:Literal ID="litRoundBanner" runat="server" />
<div class="cr-kpis"><asp:Literal ID="litKpis" runat="server" /></div>
<div class="cr-tabs"><asp:Literal ID="litTabs" runat="server" /></div>

<!-- ═══ APPLICATIONS ═══ -->
<asp:Panel ID="pnlApps" runat="server">
    <div class="cr-filters"><asp:Literal ID="litFilters" runat="server" /></div>
    <div class="cr-card">
        <div class="cr-card__hdr">Applications
            <span class="cr-card__hdr-right"><asp:Literal ID="litAppsCount" runat="server" /> &middot; tick rows with HR or at Council for bulk actions</span>
        </div>
        <div class="cr-table-wrap">
            <table class="cr-table">
                <thead><tr>
                    <th class="cr-cb"><input type="checkbox" id="cbAll" onchange="toggleAll(this)" title="Select all selectable rows" /></th>
                    <th>Ref</th><th>Employee</th><th>Position / Department</th><th>Contract end</th><th>Status</th>
                    <th>Supervisor</th><th>Submitted</th><th>Late</th><th></th>
                </tr></thead>
                <tbody><asp:Literal ID="litApps" runat="server" /></tbody>
            </table>
        </div>
    </div>
</asp:Panel>

<!-- ═══ EXPIRING CONTRACTS ═══ -->
<asp:Panel ID="pnlExpiring" runat="server">
    <div class="cr-filters"><asp:Literal ID="litExpFilters" runat="server" /></div>
    <div class="cr-card">
        <div class="cr-card__hdr">Expiring contracts
            <span class="cr-card__hdr-right"><asp:Literal ID="litExpCount" runat="server" /></span>
        </div>
        <div class="cr-table-wrap">
            <table class="cr-table">
                <thead><tr>
                    <th class="cr-cb"><input type="checkbox" id="cbExpAll" onchange="toggleExpAll(this)" title="Select all with an email address" /></th>
                    <th>Employee</th><th>Position / Department</th><th>Current contract</th><th>Ends</th><th>Application</th><th>Email</th><th>Last reminder</th>
                </tr></thead>
                <tbody><asp:Literal ID="litExpiring" runat="server" /></tbody>
            </table>
        </div>
    </div>
</asp:Panel>

<!-- ═══ ROUNDS ═══ -->
<asp:Panel ID="pnlRounds" runat="server">
    <div class="cr-filters">
        <button type="button" class="cr-btn cr-btn--primary" onclick="addRound()">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            New round
        </button>
        <span class="cr-hint">A round ties applications to a Governance Council sitting and a submission deadline. Staff apply under the open round.</span>
    </div>
    <div class="cr-card">
        <div class="cr-card__hdr">Renewal rounds</div>
        <div class="cr-table-wrap">
            <table class="cr-table">
                <thead><tr><th>Round</th><th>Council sitting</th><th>Deadline</th><th>Contracts ending by</th><th>Status</th><th class="cr-num">Applications</th><th class="cr-num">Forwarded</th><th></th></tr></thead>
                <tbody><asp:Literal ID="litRounds" runat="server" /></tbody>
            </table>
        </div>
    </div>
</asp:Panel>

<!-- Batch bar: applications -->
<div class="cr-batch" id="batchBar">
    <span class="cr-batch__count" id="batchCount">0</span><span>selected</span>
    <div class="cr-batch__actions">
        <button type="button" class="cr-batch-btn" onclick="openBulkForward()">Forward to Council</button>
        <button type="button" class="cr-batch-btn" onclick="openBulkDecision()">Record Council decision</button>
        <button type="button" class="cr-batch-btn" onclick="openSchedule('print', true)">Schedule (selected)</button>
        <button type="button" class="cr-batch-btn cr-batch-btn--ghost" onclick="clearSel()">Clear</button>
    </div>
</div>
<!-- Batch bar: expiring -->
<div class="cr-batch" id="expBar">
    <span class="cr-batch__count" id="expCount">0</span><span>employee(s) selected</span>
    <div class="cr-batch__actions">
        <button type="button" class="cr-batch-btn" onclick="openReminders()">Send reminder email</button>
        <button type="button" class="cr-batch-btn cr-batch-btn--ghost" onclick="clearExpSel()">Clear</button>
    </div>
</div>

<!-- Bulk forward -->
<div class="cr-modal-ov" id="fwdModal">
    <div class="cr-modal">
        <div class="cr-modal__hdr"><h3>Forward to Governance Council</h3><button type="button" class="cr-modal__close" onclick="closeModal('fwdModal')">&times;</button></div>
        <div class="cr-modal__body">
            <div class="cr-alert cr-alert--info">Only applications with HR (<strong>With HR</strong>) are forwarded; others are skipped and listed. The checklist is filled from what the system finds (letters, form, appraisal, supervisor recommendation).</div>
            <div class="cr-field"><label for="fwdSitting">Council sitting</label><input type="text" id="fwdSitting" maxlength="100" placeholder="Leave blank to use each application's round" /></div>
            <div class="cr-field"><label for="fwdComments">HR remarks (added to every forwarded application)</label><textarea id="fwdComments"></textarea></div>
            <label class="cr-check"><input type="checkbox" id="fwdOnlyComplete" checked /> <span>Forward only applications whose automatic checks all pass (others are skipped so they can be checked one by one)</span></label>
            <label class="cr-check cr-check--confirm"><input type="checkbox" id="fwdContractOk" /> <span>I have verified the current contract details of the selected applications.</span></label>
            <div class="cr-result" id="fwdResult"></div>
        </div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" onclick="closeModal('fwdModal')">Close</button>
            <button type="button" class="cr-btn cr-btn--primary" id="btnFwd" onclick="bulkForward()">Forward</button>
        </div>
    </div>
</div>

<!-- Bulk decision -->
<div class="cr-modal-ov" id="decModal">
    <div class="cr-modal">
        <div class="cr-modal__hdr"><h3>Record Council decision</h3><button type="button" class="cr-modal__close" onclick="closeModal('decModal')">&times;</button></div>
        <div class="cr-modal__body">
            <div class="cr-alert cr-alert--info">Applies to the selected applications that are <strong>Forwarded to Council</strong>. Each employee is emailed the outcome.</div>
            <div class="cr-field">
                <label for="bdDecision">Decision *</label>
                <select id="bdDecision" onchange="bdChanged()">
                    <option value="APPROVED">Approved - renew</option>
                    <option value="NOT_APPROVED">Not approved</option>
                    <option value="DEFERRED">Deferred</option>
                </select>
            </div>
            <div id="bdApproved">
                <div class="cr-field-row">
                    <div class="cr-field">
                        <label for="bdTermMode">Term</label>
                        <select id="bdTermMode" onchange="bdChanged()">
                            <option value="requested">Each applicant's requested term</option>
                            <option value="fixed">The same term for all</option>
                        </select>
                    </div>
                    <div class="cr-field" id="bdTermBox"><label for="bdTerm">Months</label><input type="number" id="bdTerm" min="1" max="120" value="24" /></div>
                </div>
                <div class="cr-hint" style="margin-top:-6px;margin-bottom:10px;">Each new contract starts the day after the current one ends. Edit individual dates on the application if needed.</div>
            </div>
            <div class="cr-field"><label for="bdNotes">Council notes <span id="bdNotesReq">(required unless approved)</span></label><textarea id="bdNotes"></textarea></div>
            <div class="cr-result" id="decResult"></div>
        </div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" onclick="closeModal('decModal')">Close</button>
            <button type="button" class="cr-btn cr-btn--primary" id="btnBd" onclick="bulkDecision()">Record decision</button>
        </div>
    </div>
</div>

<!-- Reminders -->
<div class="cr-modal-ov" id="remModal">
    <div class="cr-modal">
        <div class="cr-modal__hdr"><h3>Send reminder email</h3><button type="button" class="cr-modal__close" onclick="closeModal('remModal')">&times;</button></div>
        <div class="cr-modal__body">
            <div class="cr-alert cr-alert--info">Each selected employee is emailed that their contract is ending and asked to apply in the staff portal (My Contracts) before the round deadline, with the list of required documents. Every send is logged.</div>
            <div id="remSummary" class="cr-hint"></div>
            <div class="cr-progress"><div class="cr-progress__bar" id="remBar"></div></div>
            <div class="cr-result" id="remResult"></div>
        </div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" id="btnRemClose" onclick="closeReminders()">Close</button>
            <button type="button" class="cr-btn cr-btn--primary" id="btnRem" onclick="sendReminders()">Send</button>
        </div>
    </div>
</div>

<!-- Round -->
<div class="cr-modal-ov" id="roundModal">
    <div class="cr-modal">
        <div class="cr-modal__hdr"><h3 id="roundTitle">New round</h3><button type="button" class="cr-modal__close" onclick="closeModal('roundModal')">&times;</button></div>
        <div class="cr-modal__body">
            <input type="hidden" id="rdId" value="0" />
            <div class="cr-field"><label for="rdTitle">Title *</label><input type="text" id="rdTitle" maxlength="200" placeholder="e.g. Contract Renewal - November 2026 Governance Council" /></div>
            <div class="cr-field-row">
                <div class="cr-field"><label for="rdSitting">Council sitting</label><input type="text" id="rdSitting" maxlength="100" placeholder="e.g. November 2026" /></div>
                <div class="cr-field"><label for="rdCDate">Council date</label><input type="date" id="rdCDate" /></div>
            </div>
            <div class="cr-field-row">
                <div class="cr-field"><label for="rdDeadline">Submission deadline *</label><input type="date" id="rdDeadline" /></div>
                <div class="cr-field"><label for="rdEligible">Contracts ending on or before *</label><input type="date" id="rdEligible" /></div>
            </div>
            <div class="cr-field"><label for="rdStatus">Status</label><select id="rdStatus"><option value="OPEN">Open</option><option value="CLOSED">Closed</option></select></div>
            <div class="cr-field"><label for="rdNotes">Notes</label><textarea id="rdNotes"></textarea></div>
        </div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" onclick="closeModal('roundModal')">Cancel</button>
            <button type="button" class="cr-btn cr-btn--primary" id="btnRound" onclick="saveRound()">Save round</button>
        </div>
    </div>
</div>
</asp:Panel>

<div class="cr-toast" id="crToast"></div>
</div>

<script type="text/javascript">
// The page sits inside the master's <form>: Enter in a text box must not post it back.
document.addEventListener('keydown', function (e) {
    var t = e.target || {};
    if (e.keyCode === 13 && t.tagName === 'INPUT' && t.closest && t.closest('.cr-page')) {
        e.preventDefault();
        if (t.id === 'fQ') applyFilters();
        if (t.id === 'xQ') applyExpFilters();
    }
});

function toast(msg, ok) {
    var t = document.getElementById('crToast');
    t.textContent = msg || '';
    t.className = 'cr-toast cr-toast--' + (ok ? 'ok' : 'err') + ' is-on';
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, ok ? 4000 : 7000);
}
function openModal(id) { document.getElementById(id).classList.add('is-open'); }
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }
function val(id) { var e = document.getElementById(id); return e ? (e.value || '').trim() : ''; }
function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }
function qs(name) { var m = new RegExp('[?&]' + name + '=([^&]*)').exec(window.location.search); return m ? decodeURIComponent(m[1].replace(/\+/g, ' ')) : ''; }

function post(url, fields, cb) {
    var fd = new FormData();
    for (var k in fields) if (fields.hasOwnProperty(k)) fd.append(k, fields[k]);
    var xhr = new XMLHttpRequest();
    xhr.open('POST', url, true);
    var m = document.querySelector('meta[name="csrf-token"]');
    if (m) xhr.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
    xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) return;
        var res;
        try { res = JSON.parse(xhr.responseText); } catch (e) { res = { ok: false, msg: 'Server error (HTTP ' + xhr.status + ')' }; }
        cb(res);
    };
    xhr.send(fd);
}
function go(params) {
    var parts = [];
    for (var k in params) if (params.hasOwnProperty(k) && params[k] !== '' && params[k] !== '0' && params[k] != null) parts.push(k + '=' + encodeURIComponent(params[k]));
    window.location.href = 'ContractRenewals.aspx' + (parts.length ? '?' + parts.join('&') : '');
}

/* ── applications filters ── */
function applyFilters() {
    go({ round: val('fRound'), status: val('fStatus'), cat: val('fCat'), dept: val('fDept'), late: val('fLate'), q: val('fQ') });
}
function applyExpFilters() {
    var na = document.getElementById('xNoApp');
    go({ tab: 'expiring', win: val('xWin'), round: val('xRound'), dept: val('xDept'), noapp: na && na.checked ? '1' : '', q: val('xQ') });
}

/* ── selection (applications) ── */
function selected() {
    var cbs = document.querySelectorAll('.cr-row-cb:checked'), ids = [];
    for (var i = 0; i < cbs.length; i++) ids.push(cbs[i].value);
    return ids;
}
function selChanged() {
    var n = selected().length;
    document.getElementById('batchCount').textContent = n;
    document.getElementById('batchBar').classList.toggle('is-on', n > 0);
}
function toggleAll(cb) {
    var cbs = document.querySelectorAll('.cr-row-cb');
    for (var i = 0; i < cbs.length; i++) cbs[i].checked = cb.checked;
    selChanged();
}
function clearSel() { var a = document.getElementById('cbAll'); if (a) a.checked = false; toggleAll({ checked: false }); }
function countStatus(st) {
    var cbs = document.querySelectorAll('.cr-row-cb:checked'), n = 0;
    for (var i = 0; i < cbs.length; i++) if (cbs[i].getAttribute('data-status') === st) n++;
    return n;
}
function showResult(boxId, res) {
    var box = document.getElementById(boxId), h = '<div class="' + (res.ok ? 'cr-s-ok' : 'cr-s-err') + '"><strong>' + esc(res.msg) + '</strong></div>';
    if (res.skipped) for (var i = 0; i < res.skipped.length; i++) h += '<div class="cr-s-warn">Skipped ' + esc(res.skipped[i]) + '</div>';
    box.innerHTML = h;
    box.classList.add('is-on');
}

/* ── bulk forward ── */
function openBulkForward() {
    if (countStatus('AWAITING_HR') === 0) { toast('None of the selected applications is with HR', false); return; }
    document.getElementById('fwdSitting').value = '';
    document.getElementById('fwdSitting').placeholder = 'Leave blank to use each round' + (val('<%= hfDefaultSitting.ClientID %>') ? ' (e.g. ' + val('<%= hfDefaultSitting.ClientID %>') + ')' : '');
    document.getElementById('fwdResult').classList.remove('is-on');
    document.getElementById('btnFwd').disabled = false;
    openModal('fwdModal');
}
function bulkForward() {
    if (!document.getElementById('fwdContractOk').checked) { toast('Confirm that you have verified the contract details', false); return; }
    var ids = selected();
    if (!confirm('Forward ' + countStatus('AWAITING_HR') + ' application(s) with HR to the Governance Council?')) return;
    var btn = document.getElementById('btnFwd'); btn.disabled = true;
    post('ContractRenewalView.aspx?ajax=bulk_forward', {
        ids: ids.join(','), council_sitting: val('fwdSitting'), hr_comments: val('fwdComments'),
        only_complete: document.getElementById('fwdOnlyComplete').checked ? '1' : '0', contract_ok: '1'
    }, function (res) {
        showResult('fwdResult', res);
        toast(res.msg, res.ok);
        if (res.done > 0) setTimeout(function () { window.location.reload(); }, (res.skipped && res.skipped.length) ? 5000 : 1200);
        else btn.disabled = false;
    });
}

/* ── bulk decision ── */
function bdChanged() {
    var ap = val('bdDecision') === 'APPROVED';
    document.getElementById('bdApproved').style.display = ap ? '' : 'none';
    document.getElementById('bdNotesReq').style.display = ap ? 'none' : '';
    document.getElementById('bdTermBox').style.visibility = val('bdTermMode') === 'fixed' ? 'visible' : 'hidden';
}
function openBulkDecision() {
    if (countStatus('FORWARDED') === 0) { toast('None of the selected applications is forwarded to Council', false); return; }
    document.getElementById('decResult').classList.remove('is-on');
    document.getElementById('btnBd').disabled = false;
    bdChanged();
    openModal('decModal');
}
function bulkDecision() {
    var d = val('bdDecision'), notes = val('bdNotes');
    if (d !== 'APPROVED' && !notes) { toast('Add the Council notes for this decision', false); return; }
    if (d === 'APPROVED' && val('bdTermMode') === 'fixed' && !(parseInt(val('bdTerm'), 10) > 0)) { toast('Enter the term in months', false); return; }
    if (!confirm('Record "' + d.replace('_', ' ') + '" for ' + countStatus('FORWARDED') + ' forwarded application(s) and email the employees?')) return;
    var btn = document.getElementById('btnBd'); btn.disabled = true;
    post('ContractRenewalView.aspx?ajax=bulk_decision', {
        ids: selected().join(','), decision: d, notes: notes, term_mode: val('bdTermMode'), term_months: val('bdTerm')
    }, function (res) {
        showResult('decResult', res);
        toast(res.msg, res.ok);
        if (res.done > 0) setTimeout(function () { window.location.reload(); }, (res.skipped && res.skipped.length) ? 5000 : 1500);
        else btn.disabled = false;
    });
}

/* ── Council schedule ── */
function openSchedule(kind, onlySelected) {
    var ids = onlySelected ? selected() : [];
    var round = qs('round');
    var q = ids.length ? 'ids=' + ids.join(',') : ('status=FORWARDED' + (round ? '&round=' + round : ''));
    if (kind === 'csv') window.location.href = 'ContractRenewals.aspx?ajax=schedule_csv&' + q;
    else window.open('ContractRenewalPrint.aspx?schedule=1&' + q, '_blank');
}

/* ── expiring: reminders ── */
function expSelected() {
    var cbs = document.querySelectorAll('.cr-exp-cb:checked'), out = [];
    for (var i = 0; i < cbs.length; i++) out.push({ id: cbs[i].value, name: cbs[i].getAttribute('data-name') });
    return out;
}
function expSelChanged() {
    var n = expSelected().length;
    document.getElementById('expCount').textContent = n;
    document.getElementById('expBar').classList.toggle('is-on', n > 0);
}
function toggleExpAll(cb) {
    var cbs = document.querySelectorAll('.cr-exp-cb');
    for (var i = 0; i < cbs.length; i++) cbs[i].checked = cb.checked;
    expSelChanged();
}
function clearExpSel() { var a = document.getElementById('cbExpAll'); if (a) a.checked = false; toggleExpAll({ checked: false }); }
var REM_RUNNING = false;
function openReminders() {
    var sel = expSelected();
    document.getElementById('remSummary').textContent = sel.length + ' employee(s) will be emailed.';
    document.getElementById('remResult').innerHTML = '';
    document.getElementById('remResult').classList.remove('is-on');
    document.getElementById('remBar').style.width = '0';
    document.getElementById('btnRem').disabled = false;
    openModal('remModal');
}
function closeReminders() {
    if (REM_RUNNING && !confirm('Sending is in progress. Stop after the current email?')) return;
    REM_RUNNING = false;
    closeModal('remModal');
}
function sendReminders() {
    var sel = expSelected();
    if (!sel.length) return;
    var round = val('xRound');
    var box = document.getElementById('remResult'), bar = document.getElementById('remBar');
    box.classList.add('is-on');
    document.getElementById('btnRem').disabled = true;
    REM_RUNNING = true;
    var i = 0, sent = 0;
    function next() {
        if (!REM_RUNNING || i >= sel.length) {
            REM_RUNNING = false;
            box.insertAdjacentHTML('afterbegin', '<div><strong>Done: ' + sent + ' of ' + sel.length + ' sent.</strong></div>');
            return;
        }
        var s = sel[i];
        post('ContractRenewals.aspx?ajax=send_reminder', { emp_id: s.id, round_id: round }, function (res) {
            i++;
            if (res.ok) {
                sent++;
                var cell = document.getElementById('rem_' + s.id);
                if (cell) cell.textContent = 'just now';
            }
            box.insertAdjacentHTML('beforeend', '<div class="' + (res.ok ? 'cr-s-ok' : (res.status === 'NO_EMAIL' ? 'cr-s-warn' : 'cr-s-err')) + '">' + esc(s.name) + ': ' + esc(res.msg) + '</div>');
            bar.style.width = Math.round(i * 100 / sel.length) + '%';
            next();
        });
    }
    next();
}

/* ── rounds ── */
function addRound() {
    document.getElementById('roundTitle').textContent = 'New round';
    document.getElementById('rdId').value = '0';
    ['rdTitle', 'rdSitting', 'rdCDate', 'rdDeadline', 'rdEligible', 'rdNotes'].forEach(function (k) { document.getElementById(k).value = ''; });
    document.getElementById('rdStatus').value = 'OPEN';
    openModal('roundModal');
}
function editRound(btn) {
    var tr = btn.closest('tr');
    document.getElementById('roundTitle').textContent = 'Edit round';
    document.getElementById('rdId').value = tr.getAttribute('data-round');
    document.getElementById('rdTitle').value = tr.getAttribute('data-title');
    document.getElementById('rdSitting').value = tr.getAttribute('data-sitting');
    document.getElementById('rdCDate').value = tr.getAttribute('data-cdate');
    document.getElementById('rdDeadline').value = tr.getAttribute('data-deadline');
    document.getElementById('rdEligible').value = tr.getAttribute('data-eligible');
    document.getElementById('rdStatus').value = tr.getAttribute('data-status');
    document.getElementById('rdNotes').value = tr.getAttribute('data-notes');
    openModal('roundModal');
}
function saveRound() {
    if (!val('rdTitle')) { toast('Title is required', false); return; }
    if (!val('rdDeadline') || !val('rdEligible')) { toast('Deadline and "contracts ending on or before" are required', false); return; }
    var btn = document.getElementById('btnRound'); btn.disabled = true;
    post('ContractRenewals.aspx?ajax=save_round', {
        round_id: val('rdId'), title: val('rdTitle'), council_sitting: val('rdSitting'), council_date: val('rdCDate'),
        submission_deadline: val('rdDeadline'), eligible_expiry_to: val('rdEligible'), status: val('rdStatus'), notes: val('rdNotes')
    }, function (res) {
        btn.disabled = false; toast(res.msg, res.ok);
        if (res.ok) setTimeout(function () { window.location.href = 'ContractRenewals.aspx?tab=rounds'; }, 700);
    });
}
function closeRound(id) {
    if (!confirm('Close this round? Staff will no longer be able to apply under it. Existing applications are not affected.')) return;
    post('ContractRenewals.aspx?ajax=close_round', { round_id: id }, function (res) {
        toast(res.msg, res.ok);
        if (res.ok) setTimeout(function () { window.location.href = 'ContractRenewals.aspx?tab=rounds'; }, 700);
    });
}
</script>
</asp:Content>
