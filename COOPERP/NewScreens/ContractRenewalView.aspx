<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ContractRenewalView.aspx.cs" Inherits="COOPERP_NewScreens_ContractRenewalView" Title="Contract renewal - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=1" />
<style>
/* Contract renewal application: page-specific layout only (components come from hr.css) */
.cr-title { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.cr-para { margin-top: 12px; }
.cr-para__txt { margin-top: 4px; line-height: 1.55; background: var(--hr-surface); border: 1px solid var(--hr-border); padding: 8px 10px; word-break: break-word; }
.cr-top td { vertical-align: top; line-height: 1.45; }
.cr-files { margin-top: 4px; display: flex; flex-direction: column; gap: 3px; }
.cr-file { color: var(--hr-accent); text-decoration: none; display: inline-flex; align-items: center; gap: 4px; word-break: break-all; }
.cr-file:hover { text-decoration: underline; }
.cr-file svg { width: 12px; height: 12px; flex: 0 0 auto; }
.cr-check { display: flex; flex-direction: column; gap: 6px; margin-bottom: 12px; }
.cr-check__row { display: flex; align-items: center; gap: 8px; padding: 6px 8px; border: 1px solid var(--hr-border); background: #fff; cursor: pointer; }
.cr-check__row span:first-of-type { flex: 1; }
.cr-check__row input { accent-color: var(--hr-navy); margin: 0; }
.cr-confirm { display: flex; align-items: flex-start; gap: 8px; margin-top: 12px; font-size: 12px; cursor: pointer; line-height: 1.45; }
.cr-confirm input { margin: 2px 0 0; accent-color: var(--hr-navy); }
.cr-sec { margin-top: 14px; padding-top: 12px; border-top: 1px solid var(--hr-border); }
.cr-sec__t { margin-bottom: 8px; }
.cr-mini { margin-top: 4px; }
.cr-mini td, .cr-mini th { padding: 6px 8px; }
.cr-tl { list-style: none; margin: 0; padding: 0; }
.cr-tl li { display: grid; grid-template-columns: 140px minmax(0, 1fr); gap: 10px; padding: 8px 14px; border-bottom: 1px solid var(--hr-border); }
.cr-tl li:last-child { border-bottom: none; }
.cr-tl__when { color: var(--hr-muted); font-size: 11px; font-variant-numeric: tabular-nums; }
.cr-tl__d { font-size: 11px; color: var(--hr-text-2); margin-top: 2px; word-break: break-word; }
@media (max-width: 520px) { .cr-tl li { grid-template-columns: minmax(0, 1fr); gap: 2px; } }
.hr-modal .hr-field { margin-bottom: 12px; }
.hr-modal .hr-form { margin-bottom: 12px; }
.hr-modal .hr-form .hr-field { margin-bottom: 0; }
.cr-nowrap { white-space: nowrap; }
.cr-actions { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page cr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg></div>
        <div>
            <div class="hr-header__title cr-title"><asp:Literal ID="litName" runat="server" Text="Contract renewal" /> <asp:Literal ID="litStatus" runat="server" /></div>
            <div class="hr-header__sub"><asp:Literal ID="litHeaderMeta" runat="server" /></div>
        </div>
    </div>
    <div class="hr-header__actions">
        <a href="ContractRenewals.aspx" class="hr-btn hr-btn--inverse">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg>
            All renewals
        </a>
        <asp:Literal ID="litPrintLink" runat="server" />
    </div>
</div>
<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab hr-tab--active" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a></div>

<asp:Literal ID="litError" runat="server" />

<asp:Panel ID="pnlMain" runat="server">
<asp:HiddenField ID="hfId" runat="server" Value="0" />
<asp:HiddenField ID="hfStatus" runat="server" Value="" />
<asp:HiddenField ID="hfDefTerm" runat="server" Value="24" />
<asp:HiddenField ID="hfDefStart" runat="server" Value="" />
<asp:HiddenField ID="hfCurEnd" runat="server" Value="" />

<asp:Literal ID="litAlerts" runat="server" />

<div class="hr-card">
    <div class="hr-card__head">
        <span class="hr-card__title">HR action</span>
        <div class="cr-actions"><asp:Literal ID="litActions" runat="server" /></div>
    </div>
    <div class="hr-card__body"><asp:Literal ID="litStages" runat="server" /></div>
</div>

<div class="hr-grid-2">
    <div class="hr-card">
        <div class="hr-card__head"><span class="hr-card__title">Applicant and current contract</span></div>
        <div class="hr-card__body"><asp:Literal ID="litApplicant" runat="server" /></div>
    </div>
    <div class="hr-card">
        <div class="hr-card__head"><span class="hr-card__title">Renewal request</span></div>
        <div class="hr-card__body"><asp:Literal ID="litRequest" runat="server" /></div>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head"><span class="hr-card__title">Evaluation form: achievements and key performance areas</span></div>
    <div class="hr-table-wrap">
        <table class="hr-table cr-top">
            <thead><tr>
                <th class="hr-num">No</th>
                <th>Responsibility or key performance area</th>
                <th>Expected standard</th>
                <th>Achievements</th>
                <th>Evidence</th>
                <th>Supervisor comment</th>
            </tr></thead>
            <tbody><asp:Literal ID="litAchievements" runat="server" /></tbody>
        </table>
    </div>
</div>

<div class="hr-grid-2">
    <div class="hr-card">
        <div class="hr-card__head"><span class="hr-card__title">Supervisor recommendation</span></div>
        <div class="hr-card__body"><asp:Literal ID="litSupervisor" runat="server" /></div>
    </div>
    <div class="hr-card">
        <div class="hr-card__head"><span class="hr-card__title">Documents</span></div>
        <div class="hr-table-wrap">
            <table class="hr-table cr-top">
                <thead><tr><th>Type</th><th>File</th><th class="hr-num">Size</th><th>Uploaded</th></tr></thead>
                <tbody><asp:Literal ID="litDocs" runat="server" /></tbody>
            </table>
        </div>
    </div>
</div>

<div class="hr-grid-2">
    <div class="hr-card">
        <div class="hr-card__head"><span class="hr-card__title">HR verification</span></div>
        <div class="hr-card__body"><asp:Literal ID="litHrVerify" runat="server" /></div>
    </div>
    <div class="hr-card">
        <div class="hr-card__head"><span class="hr-card__title">Governance Council decision</span></div>
        <div class="hr-card__body"><asp:Literal ID="litDecision" runat="server" /></div>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head"><span class="hr-card__title">History</span></div>
    <ul class="cr-tl"><asp:Literal ID="litAudit" runat="server" /></ul>
</div>

<!-- Return modal -->
<div class="hr-modal" id="returnModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Return to employee</span><button type="button" class="hr-modal__close" title="Close" onclick="closeModal('returnModal')"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-field">
                <label class="hr-label" for="retReason">What must the employee correct? <span class="hr-req">*</span></label>
                <textarea id="retReason" class="hr-textarea" placeholder="e.g. Attach the signed application letter and complete the evidence for rows 2 and 3."></textarea>
                <span class="hr-hint">The employee is emailed this reason.</span>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('returnModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnReturn" onclick="returnApp()">Return application</button>
        </div>
    </div>
</div>

<!-- Skip supervisor modal -->
<div class="hr-modal" id="skipModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Skip supervisor stage</span><button type="button" class="hr-modal__close" title="Close" onclick="closeModal('skipModal')"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-field">
                <label class="hr-label" for="skipReason">Reason <span class="hr-req">*</span></label>
                <textarea id="skipReason" class="hr-textarea" placeholder="e.g. Supervisor on study leave until January 2027."></textarea>
                <span class="hr-hint">Use only when the supervisor is unavailable.</span>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('skipModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnSkip" onclick="skipSupervisor()">Move to HR</button>
        </div>
    </div>
</div>

<!-- Decision modal -->
<div class="hr-modal" id="decisionModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Record Council decision</span><button type="button" class="hr-modal__close" title="Close" onclick="closeModal('decisionModal')"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-field">
                <label class="hr-label" for="decDecision">Decision <span class="hr-req">*</span></label>
                <select id="decDecision" class="hr-select" onchange="decChanged()">
                    <option value="APPROVED">Approved</option>
                    <option value="NOT_APPROVED">Not approved</option>
                    <option value="DEFERRED">Deferred</option>
                </select>
            </div>
            <div id="decApprovedFields">
                <div class="hr-form">
                    <div class="hr-field"><label class="hr-label" for="decTerm">Approved term (months) <span class="hr-req">*</span></label><input type="number" id="decTerm" class="hr-input" min="1" max="120" oninput="decRecalc()" /></div>
                    <div class="hr-field"><label class="hr-label" for="decStart">New contract start <span class="hr-req">*</span></label><input type="date" id="decStart" class="hr-input" onchange="decRecalc()" /></div>
                    <div class="hr-field"><label class="hr-label" for="decEnd">New contract end <span class="hr-req">*</span></label><input type="date" id="decEnd" class="hr-input" /><span class="hr-hint">Calculated from the start date and term.</span></div>
                </div>
            </div>
            <div class="hr-field">
                <label class="hr-label" for="decNotes">Council notes</label>
                <textarea id="decNotes" class="hr-textarea"></textarea>
                <span class="hr-hint" id="decNotesReq">Required unless approved.</span>
            </div>
            <div class="hr-hint">The employee is emailed the outcome.</div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('decisionModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnDecision" onclick="saveDecision()">Record decision</button>
        </div>
    </div>
</div>

<!-- Issue contract modal -->
<div class="hr-modal" id="issueModal">
    <div class="hr-modal__box hr-modal__box--wide">
        <div class="hr-modal__head"><span>Issue new contract</span><button type="button" class="hr-modal__close" title="Close" onclick="closeModal('issueModal')"><svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body"><asp:Literal ID="litIssueForm" runat="server" /></div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('issueModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnIssue" onclick="issueContract()">Issue contract</button>
        </div>
    </div>
</div>
</asp:Panel>

<div class="hr-toast" id="crToast"></div>
</div>

<script type="text/javascript">
var CR_ID = parseInt((document.getElementById('<%= hfId.ClientID %>') || {}).value || '0', 10);
var CR_DEF_TERM = (document.getElementById('<%= hfDefTerm.ClientID %>') || {}).value || '24';
var CR_DEF_START = (document.getElementById('<%= hfDefStart.ClientID %>') || {}).value || '';
var DECISION_LABELS = { APPROVED: 'Approved', NOT_APPROVED: 'Not approved', DEFERRED: 'Deferred' };
var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

// The page sits inside the master's <form>: Enter in a text box must not post it back.
document.addEventListener('keydown', function (e) {
    var t = e.target || {};
    if (e.keyCode === 13 && t.tagName === 'INPUT' && t.closest && t.closest('.cr-page')) e.preventDefault();
});

function toast(msg, ok) {
    var t = document.getElementById('crToast');
    t.textContent = msg || '';
    t.className = 'hr-toast' + (ok ? '' : ' hr-toast--err') + ' is-on';
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, ok ? 4000 : 7000);
}
function openModal(id) {
    if (id === 'decisionModal') decInit();
    document.getElementById(id).classList.add('is-open');
}
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }
function val(id) { var e = document.getElementById(id); return e ? (e.value || '').trim() : ''; }
function fmtDate(iso) { var p = (iso || '').split('-'); return p.length === 3 ? parseInt(p[2], 10) + ' ' + MONTHS[parseInt(p[1], 10) - 1] + ' ' + p[0] : iso; }

function post(action, fields, cb) {
    var fd = new FormData();
    for (var k in fields) if (fields.hasOwnProperty(k)) fd.append(k, fields[k]);
    var xhr = new XMLHttpRequest();
    xhr.open('POST', 'ContractRenewalView.aspx?ajax=' + action, true);
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
function reload() { setTimeout(function () { window.location.href = 'ContractRenewalView.aspx?id=' + CR_ID; }, 900); }
function busy(id, on) { var b = document.getElementById(id); if (b) b.disabled = !!on; }

function checklistFields() {
    var keys = ['app_letter', 'motivation', 'form_complete', 'appraisal', 'sup_rec', 'contract_ok'];
    var f = { id: CR_ID, hr_comments: val('hrComments') };
    for (var i = 0; i < keys.length; i++) {
        var c = document.getElementById('chk_' + keys[i]);
        f['chk_' + keys[i]] = c && c.checked ? '1' : '0';
    }
    return f;
}
function saveChecklist() {
    post('save_checklist', checklistFields(), function (res) { toast(res.msg, res.ok); if (res.ok) reload(); });
}
function forwardApp() {
    var f = checklistFields();
    f.council_sitting = val('councilSitting');
    if (f.chk_contract_ok !== '1') { toast('Tick "Current contract details verified" first.', false); return; }
    if (!f.council_sitting) { toast('Enter the Council sitting.', false); return; }
    if (!confirm('Forward this application to the Governance Council (' + f.council_sitting + ')?')) return;
    post('forward', f, function (res) { toast(res.msg, res.ok); if (res.ok) reload(); });
}
function returnApp() {
    var reason = val('retReason');
    if (reason.length < 5) { toast('Enter what the employee must correct.', false); return; }
    busy('btnReturn', true);
    post('return', { id: CR_ID, reason: reason }, function (res) {
        busy('btnReturn', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('returnModal'); reload(); }
    });
}
function skipSupervisor() {
    var reason = val('skipReason');
    if (reason.length < 5) { toast('Enter the reason.', false); return; }
    busy('btnSkip', true);
    post('skip_supervisor', { id: CR_ID, reason: reason }, function (res) {
        busy('btnSkip', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('skipModal'); reload(); }
    });
}

/* decision */
function pad(n) { return (n < 10 ? '0' : '') + n; }
function isoAddMonths(iso, months) {
    var p = iso.split('-'); if (p.length !== 3) return '';
    var y = parseInt(p[0], 10), m = parseInt(p[1], 10) - 1, d = parseInt(p[2], 10);
    var tm = m + months, ty = y + Math.floor(tm / 12); tm = ((tm % 12) + 12) % 12;
    var last = new Date(ty, tm + 1, 0).getDate();
    var dt = new Date(ty, tm, Math.min(d, last));
    dt.setDate(dt.getDate() - 1);
    return dt.getFullYear() + '-' + pad(dt.getMonth() + 1) + '-' + pad(dt.getDate());
}
function decInit() {
    if (!val('decTerm')) document.getElementById('decTerm').value = CR_DEF_TERM;
    if (!val('decStart')) document.getElementById('decStart').value = CR_DEF_START;
    decRecalc(); decChanged();
}
function decRecalc() {
    var t = parseInt(val('decTerm') || '0', 10), s = val('decStart');
    if (t > 0 && s) document.getElementById('decEnd').value = isoAddMonths(s, t);
}
function decChanged() {
    var ap = val('decDecision') === 'APPROVED';
    document.getElementById('decApprovedFields').style.display = ap ? '' : 'none';
    document.getElementById('decNotesReq').style.display = ap ? 'none' : '';
}
function saveDecision() {
    var d = val('decDecision'), notes = val('decNotes');
    if (d !== 'APPROVED' && !notes) { toast('Add the Council notes for this decision.', false); return; }
    var f = { id: CR_ID, decision: d, notes: notes };
    if (d === 'APPROVED') {
        f.term_months = val('decTerm'); f.start = val('decStart'); f.end = val('decEnd');
        if (!(parseInt(f.term_months, 10) > 0) || !f.start || !f.end) { toast('Enter the approved term and the new contract dates.', false); return; }
    }
    if (!confirm('Record the Council decision "' + DECISION_LABELS[d] + '" and email the employee?')) return;
    busy('btnDecision', true);
    post('decision', f, function (res) {
        busy('btnDecision', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('decisionModal'); reload(); }
    });
}

/* issue contract */
function issueContract() {
    var c = document.getElementById('icConfirm');
    if (!c || !c.checked) { toast('Tick the confirmation first.', false); return; }
    var f = {
        id: CR_ID, confirm: '1', start: val('icStart'), end: val('icEnd'),
        job_id: val('icJob'), dept_id: val('icDept'), contract_type: val('icType'),
        payscale: val('icScale'), fixedamount: val('icFixed') || '0'
    };
    if (!f.start || !f.end) { toast('Enter the contract start and end dates.', false); return; }
    if (f.end <= f.start) { toast('The contract must end after it starts.', false); return; }
    var curEnd = (document.getElementById('<%= hfCurEnd.ClientID %>') || {}).value || '';
    var overlap = curEnd && f.start <= curEnd ? ' It starts before the current contract ends on ' + fmtDate(curEnd) + '.' : '';
    if (!confirm('Issue the new contract from ' + fmtDate(f.start) + ' to ' + fmtDate(f.end) + '? The current contract will be closed.' + overlap)) return;
    busy('btnIssue', true);
    post('issue_contract', f, function (res) {
        busy('btnIssue', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('issueModal'); reload(); }
    });
}
</script>
</asp:Content>
