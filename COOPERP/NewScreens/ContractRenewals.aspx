<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ContractRenewals.aspx.cs" Inherits="COOPERP_NewScreens_ContractRenewals" Title="Contract renewals - Campus Dynamics" %>
<%@ Reference Page="~/COOPERP/NewScreens/ContractRenewalView.aspx" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
/* Contract renewals console: page-specific layout only (components come from hr.css) */
a.hr-subtab { text-decoration: none; display: inline-block; }
.cr-cb { width: 30px; }
.cr-nowrap { white-space: nowrap; }
.cr-checkline { display: flex; align-items: flex-start; gap: 8px; margin: 10px 0 0; font-size: 12px; cursor: pointer; line-height: 1.45; }
.cr-checkline input { margin: 2px 0 0; accent-color: var(--hr-navy); }
.cr-result { display: none; max-height: 200px; overflow-y: auto; margin-top: 12px; padding: 8px 10px; border: 1px solid var(--hr-border); background: var(--hr-surface); font-size: 11px; }
.cr-result.is-on { display: block; }
.cr-result div { padding: 2px 0; }
.cr-s-ok { color: var(--hr-ok); } .cr-s-warn { color: var(--hr-warn); } .cr-s-err { color: var(--hr-bad); }
.hr-modal .hr-field { margin-bottom: 12px; }
.hr-modal .hr-form .hr-field { margin-bottom: 0; }
.hr-modal .hr-form { margin-bottom: 12px; }
.cr-inline { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; height: 32px; cursor: pointer; }
.cr-inline input { accent-color: var(--hr-navy); margin: 0; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page cr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg></div>
        <div>
            <div class="hr-header__title">Contract renewals</div>
            <div class="hr-header__sub">Applications, expiring contracts and Council rounds</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openSchedule('print')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>
            Council schedule
        </button>
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openSchedule('xlsx')">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
            Excel
        </button>
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openSchedule('csv')">CSV</button>
    </div>
</div>
<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab hr-tab--active" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a><a class="hr-tab" href="ProfileChangeRequests.aspx">Profile requests</a></div>

<asp:Literal ID="litError" runat="server" />

<asp:Panel ID="pnlMain" runat="server">
<asp:HiddenField ID="hfDefaultSitting" runat="server" Value="" />

<asp:Literal ID="litRoundBanner" runat="server" />
<div class="hr-kpis"><asp:Literal ID="litKpis" runat="server" /></div>
<div class="hr-subtabs"><asp:Literal ID="litTabs" runat="server" /></div>

<!-- Applications -->
<asp:Panel ID="pnlApps" runat="server">
    <div class="hr-filters"><asp:Literal ID="litFilters" runat="server" /></div>
    <div class="hr-card">
        <div class="hr-card__head">
            <span class="hr-card__title">Applications</span>
            <span class="hr-card__meta"><asp:Literal ID="litAppsCount" runat="server" /></span>
        </div>
        <div class="hr-bulk" id="batchBar">
            <strong id="batchCount">0</strong><span>selected</span>
            <span class="hr-spacer"></span>
            <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="openBulkForward()">Forward to Council</button>
            <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="openBulkDecision()">Record Council decision</button>
            <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="openSchedule('print', true)">Schedule for selected</button>
            <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="clearSel()">Clear</button>
        </div>
        <div class="hr-table-wrap">
            <table class="hr-table">
                <thead><tr>
                    <th class="cr-cb"><input type="checkbox" id="cbAll" onchange="toggleAll(this)" title="Select all" /></th>
                    <th>Ref</th><th>Employee</th><th>Position</th><th>Contract ends</th><th>Status</th>
                    <th>Supervisor</th><th>Submitted</th><th>Late</th><th></th>
                </tr></thead>
                <tbody><asp:Literal ID="litApps" runat="server" /></tbody>
            </table>
        </div>
    </div>
</asp:Panel>

<!-- Expiring contracts -->
<asp:Panel ID="pnlExpiring" runat="server">
    <div class="hr-filters"><asp:Literal ID="litExpFilters" runat="server" /></div>
    <div class="hr-card">
        <div class="hr-card__head">
            <span class="hr-card__title">Expiring contracts</span>
            <span class="hr-card__meta"><asp:Literal ID="litExpCount" runat="server" /></span>
        </div>
        <div class="hr-bulk" id="expBar">
            <strong id="expCount">0</strong><span>selected</span>
            <span class="hr-spacer"></span>
            <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="openReminders()">Send reminder email</button>
            <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="clearExpSel()">Clear</button>
        </div>
        <div class="hr-table-wrap">
            <table class="hr-table">
                <thead><tr>
                    <th class="cr-cb"><input type="checkbox" id="cbExpAll" onchange="toggleExpAll(this)" title="Select all with an email address" /></th>
                    <th>Employee</th><th>Position</th><th>Current contract</th><th>Ends</th><th>Application</th><th>Email</th><th>Last reminder</th>
                </tr></thead>
                <tbody><asp:Literal ID="litExpiring" runat="server" /></tbody>
            </table>
        </div>
    </div>
</asp:Panel>

<!-- Rounds -->
<asp:Panel ID="pnlRounds" runat="server">
    <div class="hr-card">
        <div class="hr-card__head">
            <span class="hr-card__title">Renewal rounds</span>
            <button type="button" class="hr-btn hr-btn--primary hr-btn--sm" onclick="addRound()">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                New round
            </button>
        </div>
        <div class="hr-table-wrap">
            <table class="hr-table">
                <thead><tr><th>Round</th><th>Council sitting</th><th>Deadline</th><th>Contracts ending by</th><th>Status</th><th class="hr-num">Applications</th><th class="hr-num">At Council</th><th></th></tr></thead>
                <tbody><asp:Literal ID="litRounds" runat="server" /></tbody>
            </table>
        </div>
    </div>
</asp:Panel>

<!-- Bulk forward -->
<div class="hr-modal" id="fwdModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Forward to the Governance Council</span><button type="button" class="hr-modal__close" title="Close" onclick="closeModal('fwdModal')"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-hint" style="margin-bottom:12px;">Only applications with HR are forwarded. Others are skipped.</div>
            <div class="hr-field"><label class="hr-label" for="fwdSitting">Council sitting</label><input type="text" id="fwdSitting" class="hr-input" maxlength="100" /></div>
            <div class="hr-field"><label class="hr-label" for="fwdComments">HR remarks</label><textarea id="fwdComments" class="hr-textarea"></textarea><span class="hr-hint">Added to every forwarded application.</span></div>
            <label class="cr-checkline"><input type="checkbox" id="fwdOnlyComplete" checked /> <span>Forward only applications that pass all automatic checks</span></label>
            <label class="cr-checkline"><input type="checkbox" id="fwdContractOk" /> <span>I have verified the current contract details of the selected applications</span></label>
            <div class="cr-result" id="fwdResult"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('fwdModal')">Close</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnFwd" onclick="bulkForward()">Forward</button>
        </div>
    </div>
</div>

<!-- Bulk decision -->
<div class="hr-modal" id="decModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Record Council decision</span><button type="button" class="hr-modal__close" title="Close" onclick="closeModal('decModal')"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-hint" style="margin-bottom:12px;">Applies to the selected applications at Council. Each employee is emailed the outcome.</div>
            <div class="hr-field">
                <label class="hr-label" for="bdDecision">Decision <span class="hr-req">*</span></label>
                <select id="bdDecision" class="hr-select" onchange="bdChanged()">
                    <option value="APPROVED">Approved</option>
                    <option value="NOT_APPROVED">Not approved</option>
                    <option value="DEFERRED">Deferred</option>
                </select>
            </div>
            <div id="bdApproved">
                <div class="hr-form">
                    <div class="hr-field">
                        <label class="hr-label" for="bdTermMode">Term</label>
                        <select id="bdTermMode" class="hr-select" onchange="bdChanged()">
                            <option value="requested">Each applicant's requested term</option>
                            <option value="fixed">The same term for all</option>
                        </select>
                    </div>
                    <div class="hr-field" id="bdTermBox"><label class="hr-label" for="bdTerm">Months</label><input type="number" id="bdTerm" class="hr-input" min="1" max="120" value="24" /></div>
                    <div class="hr-full hr-hint">New contracts start the day after the current contract ends.</div>
                </div>
            </div>
            <div class="hr-field"><label class="hr-label" for="bdNotes">Council notes</label><textarea id="bdNotes" class="hr-textarea"></textarea><span class="hr-hint" id="bdNotesReq">Required unless approved.</span></div>
            <div class="cr-result" id="decResult"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('decModal')">Close</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnBd" onclick="bulkDecision()">Record decision</button>
        </div>
    </div>
</div>

<!-- Reminders -->
<div class="hr-modal" id="remModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Send reminder email</span><button type="button" class="hr-modal__close" title="Close" onclick="closeReminders()"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-hint" style="margin-bottom:10px;">Each employee is asked to apply in the staff portal before the round deadline.</div>
            <div id="remSummary" style="margin-bottom:10px;"></div>
            <span class="hr-bar"><span id="remBar" style="width:0;"></span></span>
            <div class="cr-result" id="remResult"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" id="btnRemClose" onclick="closeReminders()">Close</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnRem" onclick="sendReminders()">Send</button>
        </div>
    </div>
</div>

<!-- Round -->
<div class="hr-modal" id="roundModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="roundTitle">New round</span><button type="button" class="hr-modal__close" title="Close" onclick="closeModal('roundModal')"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <input type="hidden" id="rdId" value="0" />
            <div class="hr-form">
                <div class="hr-field hr-full"><label class="hr-label" for="rdTitle">Title <span class="hr-req">*</span></label><input type="text" id="rdTitle" class="hr-input" maxlength="200" placeholder="e.g. Contract renewal, November 2026 Council" /></div>
                <div class="hr-field"><label class="hr-label" for="rdSitting">Council sitting</label><input type="text" id="rdSitting" class="hr-input" maxlength="100" placeholder="e.g. November 2026" /></div>
                <div class="hr-field"><label class="hr-label" for="rdCDate">Council date</label><input type="date" id="rdCDate" class="hr-input" /></div>
                <div class="hr-field"><label class="hr-label" for="rdDeadline">Submission deadline <span class="hr-req">*</span></label><input type="date" id="rdDeadline" class="hr-input" /></div>
                <div class="hr-field"><label class="hr-label" for="rdEligible">Contracts ending on or before <span class="hr-req">*</span></label><input type="date" id="rdEligible" class="hr-input" /></div>
                <div class="hr-field"><label class="hr-label" for="rdStatus">Status</label><select id="rdStatus" class="hr-select"><option value="OPEN">Open</option><option value="CLOSED">Closed</option></select></div>
                <div class="hr-field hr-full"><label class="hr-label" for="rdNotes">Notes</label><textarea id="rdNotes" class="hr-textarea"></textarea></div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('roundModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnRound" onclick="saveRound()">Save round</button>
        </div>
    </div>
</div>
</asp:Panel>

<div class="hr-toast" id="crToast"></div>
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

var DECISION_LABELS = { APPROVED: 'Approved', NOT_APPROVED: 'Not approved', DEFERRED: 'Deferred' };

function toast(msg, ok) {
    var t = document.getElementById('crToast');
    t.textContent = msg || '';
    t.className = 'hr-toast' + (ok ? '' : ' hr-toast--err') + ' is-on';
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
        try { res = JSON.parse(xhr.responseText); } catch (e) { res = { ok: false, msg: 'The server did not respond. Reload the page and try again.' }; }
        if (res && res.msg == null && res.message) res.msg = res.message;
        cb(res);
    };
    xhr.send(fd);
}
function go(params) {
    var parts = [];
    for (var k in params) if (params.hasOwnProperty(k) && params[k] !== '' && params[k] !== '0' && params[k] != null) parts.push(k + '=' + encodeURIComponent(params[k]));
    window.location.href = 'ContractRenewals.aspx' + (parts.length ? '?' + parts.join('&') : '');
}

/* applications filters */
function applyFilters() {
    go({ round: val('fRound'), status: val('fStatus'), cat: val('fCat'), dept: val('fDept'), late: val('fLate'), q: val('fQ') });
}
function applyExpFilters() {
    var na = document.getElementById('xNoApp');
    go({ tab: 'expiring', win: val('xWin'), round: val('xRound'), dept: val('xDept'), noapp: na && na.checked ? '1' : '', q: val('xQ') });
}

/* selection (applications) */
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

/* bulk forward */
function openBulkForward() {
    if (countStatus('AWAITING_HR') === 0) { toast('None of the selected applications is with HR.', false); return; }
    var def = val('<%= hfDefaultSitting.ClientID %>');
    document.getElementById('fwdSitting').value = '';
    document.getElementById('fwdSitting').placeholder = def ? 'Blank uses each round, e.g. ' + def : 'Blank uses each round';
    document.getElementById('fwdResult').classList.remove('is-on');
    document.getElementById('btnFwd').disabled = false;
    openModal('fwdModal');
}
function bulkForward() {
    if (!document.getElementById('fwdContractOk').checked) { toast('Confirm that you have verified the contract details.', false); return; }
    var ids = selected();
    if (!confirm('Forward ' + countStatus('AWAITING_HR') + ' application(s) to the Governance Council?')) return;
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

/* bulk decision */
function bdChanged() {
    var ap = val('bdDecision') === 'APPROVED';
    document.getElementById('bdApproved').style.display = ap ? '' : 'none';
    document.getElementById('bdNotesReq').style.display = ap ? 'none' : '';
    document.getElementById('bdTermBox').style.visibility = val('bdTermMode') === 'fixed' ? 'visible' : 'hidden';
}
function openBulkDecision() {
    if (countStatus('FORWARDED') === 0) { toast('None of the selected applications is at Council.', false); return; }
    document.getElementById('decResult').classList.remove('is-on');
    document.getElementById('btnBd').disabled = false;
    bdChanged();
    openModal('decModal');
}
function bulkDecision() {
    var d = val('bdDecision'), notes = val('bdNotes');
    if (d !== 'APPROVED' && !notes) { toast('Add the Council notes for this decision.', false); return; }
    if (d === 'APPROVED' && val('bdTermMode') === 'fixed' && !(parseInt(val('bdTerm'), 10) > 0)) { toast('Enter the term in months.', false); return; }
    if (!confirm('Record "' + DECISION_LABELS[d] + '" for ' + countStatus('FORWARDED') + ' application(s) at Council and email the employees?')) return;
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

/* Council schedule: print, Excel or CSV (same columns) */
function openSchedule(kind, onlySelected) {
    var ids = onlySelected ? selected() : [];
    var round = qs('round');
    var q = ids.length ? 'ids=' + ids.join(',') : ('status=FORWARDED' + (round ? '&round=' + round : ''));
    if (kind === 'csv' || kind === 'xlsx') window.location.href = 'ContractRenewals.aspx?ajax=schedule_' + kind + '&' + q;
    else window.open('ContractRenewalPrint.aspx?schedule=1&' + q, '_blank');
}

/* expiring: reminders */
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
            box.insertAdjacentHTML('afterbegin', '<div><strong>' + sent + ' of ' + sel.length + ' sent.</strong></div>');
            return;
        }
        var s = sel[i];
        post('ContractRenewals.aspx?ajax=send_reminder', { emp_id: s.id, round_id: round }, function (res) {
            i++;
            if (res.ok) {
                sent++;
                var cell = document.getElementById('rem_' + s.id);
                if (cell) cell.textContent = 'Just now';
            }
            box.insertAdjacentHTML('beforeend', '<div class="' + (res.ok ? 'cr-s-ok' : (res.status === 'NO_EMAIL' ? 'cr-s-warn' : 'cr-s-err')) + '">' + esc(s.name) + ': ' + esc(res.msg) + '</div>');
            bar.style.width = Math.round(i * 100 / sel.length) + '%';
            next();
        });
    }
    next();
}

/* rounds */
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
    if (!val('rdTitle')) { toast('Enter a title for the round.', false); return; }
    if (!val('rdDeadline') || !val('rdEligible')) { toast('Enter the submission deadline and the contract end date limit.', false); return; }
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
    if (!confirm('Close this round? Staff will no longer be able to apply under it.')) return;
    post('ContractRenewals.aspx?ajax=close_round', { round_id: id }, function (res) {
        toast(res.msg, res.ok);
        if (res.ok) setTimeout(function () { window.location.href = 'ContractRenewals.aspx?tab=rounds'; }, 700);
    });
}
</script>
</asp:Content>
