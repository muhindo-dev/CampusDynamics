<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ContractRenewalView.aspx.cs" Inherits="COOPERP_NewScreens_ContractRenewalView" Title="Contract Renewal - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<style>
/* ===== CONTRACT RENEWAL - APPLICATION ===== */
.cr-page{--navy:#05275C;--navy-h:#041d45;--accent:#174DA4;--surface:#f5f7fa;--border:#e0e5ed;--border-in:#cdd3de;--text:#1a1a2e;--text2:#555;--muted:#888;--danger:#dc3545;--warn:#d97706;--ok:#16a34a;
    font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;font-size:12px;color:var(--text);}
.cr-page *,.cr-page *::before,.cr-page *::after{box-sizing:border-box;}
.cr-head{display:flex;align-items:flex-start;gap:12px;margin-bottom:14px;flex-wrap:wrap;}
.cr-head__icon{width:40px;height:40px;background:var(--navy);color:#fff;display:flex;align-items:center;justify-content:center;flex-shrink:0;border-radius:4px;}
.cr-head__text{min-width:0;flex:1 1 260px;}
.cr-head__title{font-size:19px;font-weight:700;line-height:1.25;display:flex;flex-wrap:wrap;gap:8px;align-items:center;}
.cr-head__sub{font-size:12px;color:var(--muted);margin-top:3px;}
.cr-head__actions{display:flex;gap:8px;flex-wrap:wrap;}
.cr-actionbar{display:flex;gap:8px;flex-wrap:wrap;align-items:center;background:#fff;border:1px solid var(--border);border-left:3px solid var(--accent);padding:10px 14px;margin-bottom:14px;border-radius:4px;}
.cr-actionbar__lbl{font-size:10px;font-weight:700;color:var(--text2);text-transform:uppercase;letter-spacing:.4px;margin-right:6px;}
.cr-grid{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:14px;}
@media(max-width:900px){.cr-grid{grid-template-columns:minmax(0,1fr);}}
.cr-card{background:#fff;border:1px solid var(--border);border-radius:4px;margin-bottom:14px;min-width:0;}
.cr-card__hdr{padding:10px 14px;border-bottom:1px solid var(--border);font-size:11px;font-weight:700;color:var(--text2);text-transform:uppercase;letter-spacing:.4px;background:var(--surface);display:flex;gap:8px;align-items:center;}
.cr-card__hdr-right{margin-left:auto;font-weight:400;text-transform:none;letter-spacing:0;color:var(--muted);}
.cr-card__body{padding:12px 14px;}
.cr-dl{display:grid;grid-template-columns:140px minmax(0,1fr);gap:6px 12px;margin:0;}
.cr-dl dt{font-size:10px;font-weight:700;color:var(--muted);text-transform:uppercase;letter-spacing:.3px;padding-top:2px;}
.cr-dl dd{margin:0;word-break:break-word;}
@media(max-width:520px){.cr-dl{grid-template-columns:minmax(0,1fr);}.cr-dl dd{margin-bottom:6px;}}
.cr-para{margin-top:12px;}
.cr-para__lbl{font-size:10px;font-weight:700;color:var(--muted);text-transform:uppercase;letter-spacing:.3px;margin-bottom:3px;}
.cr-para__txt{line-height:1.55;background:var(--surface);border:1px solid var(--border);padding:8px 10px;}
.cr-table-wrap{overflow-x:auto;}
.cr-table{width:100%;border-collapse:collapse;font-size:12px;}
.cr-table th{text-align:left;padding:7px 10px;background:var(--surface);border-bottom:1px solid var(--border);color:var(--text2);font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;white-space:nowrap;}
.cr-table td{padding:7px 10px;border-bottom:1px solid #f0f2f5;vertical-align:top;line-height:1.45;}
.cr-num{text-align:right;width:40px;color:var(--muted);font-variant-numeric:tabular-nums;}
.cr-strong{font-weight:600;min-width:140px;}
.cr-muted{color:var(--muted);font-size:11px;}
.cr-actions{white-space:nowrap;}
.cr-empty{padding:14px;text-align:center;color:var(--muted);font-style:italic;}
.cr-code{display:inline-block;font-family:Consolas,monospace;font-size:10px;padding:1px 5px;background:rgba(23,77,164,.07);border:1px solid rgba(23,77,164,.15);color:var(--accent);border-radius:0;font-weight:600;}
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
.cr-files{margin-top:4px;display:flex;flex-direction:column;gap:3px;}
.cr-file,.cr-link{color:var(--accent);text-decoration:none;display:inline-flex;align-items:center;gap:4px;word-break:break-all;}
.cr-file:hover,.cr-link:hover{text-decoration:underline;}
.cr-check{display:flex;flex-direction:column;gap:6px;margin-bottom:12px;}
.cr-check__row{display:flex;align-items:center;gap:8px;padding:6px 8px;border:1px solid var(--border);background:#fff;cursor:pointer;}
.cr-check__row span:first-of-type{flex:1;}
.cr-check__row--confirm{background:#fff8e1;border-color:#fcd34d;margin-top:10px;}
.cr-auto{font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;padding:1px 5px;border-radius:0;}
.cr-auto--ok{background:#e6f4ea;color:#155724;}
.cr-auto--miss{background:#fef5f5;color:#dc3545;}
.cr-auto--na{background:#f1f3f6;color:#6b7280;}
.cr-hint{font-size:11px;color:var(--muted);margin-top:4px;line-height:1.5;}
.cr-hint--warn{color:#b45309;}
.cr-issued{margin-top:12px;padding:10px 12px;border:1px solid #c3e6cb;background:#f3fbf5;}
.cr-issued__t{font-weight:700;color:#155724;margin-bottom:3px;}
.cr-ul{margin:6px 0 0 18px;padding:0;}
.cr-tl{list-style:none;margin:0;padding:0;}
.cr-tl li{display:grid;grid-template-columns:130px minmax(0,1fr);gap:10px;padding:8px 14px;border-bottom:1px solid #f0f2f5;}
.cr-tl__when{color:var(--muted);font-size:11px;font-variant-numeric:tabular-nums;}
.cr-tl__what strong{text-transform:capitalize;}
.cr-tl__d{font-size:11px;color:var(--text2);margin-top:2px;word-break:break-word;}
@media(max-width:520px){.cr-tl li{grid-template-columns:minmax(0,1fr);gap:2px;}}
.cr-btn{display:inline-flex;align-items:center;gap:5px;padding:6px 12px;font-size:12px;font-weight:600;border:1px solid var(--border-in);background:#fff;color:var(--text2);cursor:pointer;border-radius:0;white-space:nowrap;font-family:inherit;text-decoration:none;}
.cr-btn:hover{border-color:var(--accent);color:var(--accent);}
.cr-btn--primary{background:var(--navy);border-color:var(--navy);color:#fff;}
.cr-btn--primary:hover{background:var(--navy-h);border-color:var(--navy-h);color:#fff;}
.cr-btn--success{background:var(--ok);border-color:var(--ok);color:#fff;}
.cr-btn--success:hover{background:#12813b;color:#fff;border-color:#12813b;}
.cr-btn--danger-outline{color:var(--danger);border-color:#f1b0b7;}
.cr-btn--danger-outline:hover{background:#fff5f5;color:var(--danger);border-color:var(--danger);}
.cr-btn:disabled{opacity:.5;cursor:not-allowed;}
.cr-field{margin-bottom:12px;}
.cr-field label{display:block;font-size:10px;font-weight:700;color:var(--text2);text-transform:uppercase;letter-spacing:.4px;margin-bottom:4px;}
.cr-field input,.cr-field select,.cr-field textarea{font-size:12px;padding:6px 8px;border:1px solid var(--border-in);border-radius:0;background:#fff;color:var(--text);font-family:inherit;width:100%;}
.cr-field input:focus,.cr-field select:focus,.cr-field textarea:focus{outline:none;border-color:var(--accent);box-shadow:0 0 0 2px rgba(23,77,164,.15);}
.cr-field textarea{min-height:80px;resize:vertical;}
.cr-field textarea:disabled{background:var(--surface);}
.cr-field-row{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:12px;}
@media(max-width:520px){.cr-field-row{grid-template-columns:minmax(0,1fr);}}
.cr-alert{padding:10px 14px;margin-bottom:12px;font-size:12px;border:1px solid;line-height:1.5;}
.cr-alert--error{background:#f8d7da;color:#721c24;border-color:#f5c6cb;}
.cr-alert--warn{background:#fff8e1;color:#7a4a05;border-color:#fcd34d;}
.cr-alert--info{background:#eef3fb;color:#05275C;border-color:#c5d3e8;}
.cr-modal-ov{display:none;position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:9999;align-items:center;justify-content:center;padding:16px;}
.cr-modal-ov.is-open{display:flex;}
.cr-modal{background:#fff;border-radius:2px;width:560px;max-width:100%;max-height:92vh;overflow-y:auto;box-shadow:0 10px 32px rgba(0,0,0,.2);}
.cr-modal--wide{width:680px;}
.cr-modal__hdr{padding:12px 16px;display:flex;align-items:center;background:var(--navy);color:#fff;}
.cr-modal__hdr h3{margin:0;font-size:14px;font-weight:700;flex:1;}
.cr-modal__close{background:none;border:none;color:#fff;font-size:20px;cursor:pointer;line-height:1;padding:0 4px;}
.cr-modal__body{padding:16px;}
.cr-modal__foot{padding:12px 16px;border-top:1px solid var(--border);display:flex;justify-content:flex-end;gap:8px;flex-wrap:wrap;}
.cr-toast{position:fixed;bottom:20px;right:20px;padding:10px 16px;font-size:12px;font-weight:600;color:#fff;z-index:10000;opacity:0;transition:opacity .25s;pointer-events:none;max-width:420px;}
.cr-toast.is-on{opacity:1;}
.cr-toast--ok{background:#16a34a;}
.cr-toast--err{background:var(--danger);}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="cr-page">
<asp:Literal ID="litError" runat="server" />

<asp:Panel ID="pnlMain" runat="server">
<asp:HiddenField ID="hfId" runat="server" Value="0" />
<asp:HiddenField ID="hfStatus" runat="server" Value="" />
<asp:HiddenField ID="hfDefTerm" runat="server" Value="24" />
<asp:HiddenField ID="hfDefStart" runat="server" Value="" />
<asp:HiddenField ID="hfCurEnd" runat="server" Value="" />

<div class="cr-head">
    <div class="cr-head__icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10"/></svg>
    </div>
    <div class="cr-head__text">
        <div class="cr-head__title"><span class="cr-code" style="font-size:12px;"><asp:Literal ID="litRef" runat="server" /></span> <asp:Literal ID="litName" runat="server" /> <asp:Literal ID="litStatus" runat="server" /></div>
        <div class="cr-head__sub"><asp:Literal ID="litHeaderMeta" runat="server" /></div>
    </div>
    <div class="cr-head__actions">
        <a href="ContractRenewals.aspx" class="cr-btn">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg>
            All applications
        </a>
        <asp:Literal ID="litPrintLink" runat="server" />
    </div>
</div>

<asp:Literal ID="litAlerts" runat="server" />

<div class="cr-actionbar">
    <span class="cr-actionbar__lbl">HR action</span>
    <asp:Literal ID="litActions" runat="server" />
</div>

<div class="cr-grid">
    <div class="cr-card">
        <div class="cr-card__hdr">Applicant &amp; current contract</div>
        <div class="cr-card__body"><asp:Literal ID="litApplicant" runat="server" /></div>
    </div>
    <div class="cr-card">
        <div class="cr-card__hdr">Renewal request</div>
        <div class="cr-card__body"><asp:Literal ID="litRequest" runat="server" /></div>
    </div>
</div>

<div class="cr-card">
    <div class="cr-card__hdr">Evaluation form - achievements of staff responsibilities and key performance areas</div>
    <div class="cr-table-wrap">
        <table class="cr-table">
            <thead><tr>
                <th class="cr-num">No</th>
                <th>Responsibility / KPA</th>
                <th>Expected standard</th>
                <th>Achievements</th>
                <th>Evidence</th>
                <th>Supervisor comment</th>
            </tr></thead>
            <tbody><asp:Literal ID="litAchievements" runat="server" /></tbody>
        </table>
    </div>
</div>

<div class="cr-grid">
    <div class="cr-card">
        <div class="cr-card__hdr">Supervisor recommendation</div>
        <div class="cr-card__body"><asp:Literal ID="litSupervisor" runat="server" /></div>
    </div>
    <div class="cr-card">
        <div class="cr-card__hdr">Documents</div>
        <div class="cr-table-wrap">
            <table class="cr-table">
                <thead><tr><th>Type</th><th>File</th><th>Size</th><th>Uploaded</th></tr></thead>
                <tbody><asp:Literal ID="litDocs" runat="server" /></tbody>
            </table>
        </div>
    </div>
</div>

<div class="cr-grid">
    <div class="cr-card">
        <div class="cr-card__hdr">HR verification
            <span class="cr-card__hdr-right">"Found" / "Not found" is what the system detected</span>
        </div>
        <div class="cr-card__body"><asp:Literal ID="litHrVerify" runat="server" /></div>
    </div>
    <div class="cr-card">
        <div class="cr-card__hdr">Governance Council decision</div>
        <div class="cr-card__body"><asp:Literal ID="litDecision" runat="server" /></div>
    </div>
</div>

<div class="cr-card">
    <div class="cr-card__hdr">Performance appraisals (latest first)</div>
    <div class="cr-table-wrap">
        <table class="cr-table">
            <thead><tr><th>Session</th><th>Status</th><th class="cr-num">Score</th><th>Classification</th><th>Submitted</th><th></th></tr></thead>
            <tbody><asp:Literal ID="litAppraisals" runat="server" /></tbody>
        </table>
    </div>
</div>

<div class="cr-card">
    <div class="cr-card__hdr">History</div>
    <ul class="cr-tl"><asp:Literal ID="litAudit" runat="server" /></ul>
</div>

<!-- Return modal -->
<div class="cr-modal-ov" id="returnModal">
    <div class="cr-modal">
        <div class="cr-modal__hdr"><h3>Return to employee</h3><button type="button" class="cr-modal__close" onclick="closeModal('returnModal')">&times;</button></div>
        <div class="cr-modal__body">
            <div class="cr-field">
                <label for="retReason">What must the employee correct? *</label>
                <textarea id="retReason" placeholder="e.g. Attach the signed application letter and complete the evidence column for rows 2 and 3."></textarea>
                <div class="cr-hint">The employee is emailed this reason and can edit and resubmit in the staff portal.</div>
            </div>
        </div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" onclick="closeModal('returnModal')">Cancel</button>
            <button type="button" class="cr-btn cr-btn--primary" id="btnReturn" onclick="returnApp()">Return application</button>
        </div>
    </div>
</div>

<!-- Skip supervisor modal -->
<div class="cr-modal-ov" id="skipModal">
    <div class="cr-modal">
        <div class="cr-modal__hdr"><h3>Skip supervisor stage</h3><button type="button" class="cr-modal__close" onclick="closeModal('skipModal')">&times;</button></div>
        <div class="cr-modal__body">
            <div class="cr-alert cr-alert--info">The application moves to HR verification without a supervisor recommendation. Use this only when the supervisor is unavailable.</div>
            <div class="cr-field">
                <label for="skipReason">Reason *</label>
                <textarea id="skipReason" placeholder="e.g. Supervisor on study leave until January 2027."></textarea>
            </div>
        </div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" onclick="closeModal('skipModal')">Cancel</button>
            <button type="button" class="cr-btn cr-btn--primary" id="btnSkip" onclick="skipSupervisor()">Move to HR</button>
        </div>
    </div>
</div>

<!-- Decision modal -->
<div class="cr-modal-ov" id="decisionModal">
    <div class="cr-modal">
        <div class="cr-modal__hdr"><h3>Record Council decision</h3><button type="button" class="cr-modal__close" onclick="closeModal('decisionModal')">&times;</button></div>
        <div class="cr-modal__body">
            <div class="cr-field">
                <label for="decDecision">Decision *</label>
                <select id="decDecision" onchange="decChanged()">
                    <option value="APPROVED">Approved - renew the contract</option>
                    <option value="NOT_APPROVED">Not approved</option>
                    <option value="DEFERRED">Deferred</option>
                </select>
            </div>
            <div id="decApprovedFields">
                <div class="cr-field-row">
                    <div class="cr-field"><label for="decTerm">Approved term (months) *</label><input type="number" id="decTerm" min="1" max="120" oninput="decRecalc()" /></div>
                    <div class="cr-field"><label for="decStart">New contract start *</label><input type="date" id="decStart" onchange="decRecalc()" /></div>
                </div>
                <div class="cr-field-row">
                    <div class="cr-field"><label for="decEnd">New contract end *</label><input type="date" id="decEnd" /><div class="cr-hint">Computed from start + term; you may edit it.</div></div>
                    <div></div>
                </div>
            </div>
            <div class="cr-field">
                <label for="decNotes">Council notes <span id="decNotesReq">(required unless approved)</span></label>
                <textarea id="decNotes"></textarea>
            </div>
            <div class="cr-hint">The employee is emailed the outcome.</div>
        </div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" onclick="closeModal('decisionModal')">Cancel</button>
            <button type="button" class="cr-btn cr-btn--primary" id="btnDecision" onclick="saveDecision()">Record decision</button>
        </div>
    </div>
</div>

<!-- Issue contract modal -->
<div class="cr-modal-ov" id="issueModal">
    <div class="cr-modal cr-modal--wide">
        <div class="cr-modal__hdr"><h3>Issue new contract</h3><button type="button" class="cr-modal__close" onclick="closeModal('issueModal')">&times;</button></div>
        <div class="cr-modal__body"><asp:Literal ID="litIssueForm" runat="server" /></div>
        <div class="cr-modal__foot">
            <button type="button" class="cr-btn" onclick="closeModal('issueModal')">Cancel</button>
            <button type="button" class="cr-btn cr-btn--success" id="btnIssue" onclick="issueContract()">Issue contract</button>
        </div>
    </div>
</div>
</asp:Panel>

<div class="cr-toast" id="crToast"></div>
</div>

<script type="text/javascript">
var CR_ID = parseInt((document.getElementById('<%= hfId.ClientID %>') || {}).value || '0', 10);
var CR_DEF_TERM = (document.getElementById('<%= hfDefTerm.ClientID %>') || {}).value || '24';
var CR_DEF_START = (document.getElementById('<%= hfDefStart.ClientID %>') || {}).value || '';

// The page sits inside the master's <form>: Enter in a text box must not post it back.
document.addEventListener('keydown', function (e) {
    var t = e.target || {};
    if (e.keyCode === 13 && t.tagName === 'INPUT' && t.closest && t.closest('.cr-page')) e.preventDefault();
});

function toast(msg, ok) {
    var t = document.getElementById('crToast');
    t.textContent = msg || '';
    t.className = 'cr-toast cr-toast--' + (ok ? 'ok' : 'err') + ' is-on';
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, ok ? 4000 : 7000);
}
function openModal(id) {
    if (id === 'decisionModal') decInit();
    document.getElementById(id).classList.add('is-open');
}
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }
function val(id) { var e = document.getElementById(id); return e ? (e.value || '').trim() : ''; }

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
        try { res = JSON.parse(xhr.responseText); } catch (e) { res = { ok: false, msg: 'Server error (HTTP ' + xhr.status + ')' }; }
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
    if (f.chk_contract_ok !== '1') { toast('Tick "Current contract details verified correct" first', false); return; }
    if (!f.council_sitting) { toast('Enter the Council sitting', false); return; }
    if (!confirm('Forward this application to the Governance Council (' + f.council_sitting + ')?')) return;
    post('forward', f, function (res) { toast(res.msg, res.ok); if (res.ok) reload(); });
}
function returnApp() {
    var reason = val('retReason');
    if (reason.length < 5) { toast('Enter what the employee must correct', false); return; }
    busy('btnReturn', true);
    post('return', { id: CR_ID, reason: reason }, function (res) {
        busy('btnReturn', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('returnModal'); reload(); }
    });
}
function skipSupervisor() {
    var reason = val('skipReason');
    if (reason.length < 5) { toast('Enter the reason', false); return; }
    busy('btnSkip', true);
    post('skip_supervisor', { id: CR_ID, reason: reason }, function (res) {
        busy('btnSkip', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('skipModal'); reload(); }
    });
}

/* ── decision ── */
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
    if (d !== 'APPROVED' && !notes) { toast('Add the Council notes for this decision', false); return; }
    var f = { id: CR_ID, decision: d, notes: notes };
    if (d === 'APPROVED') {
        f.term_months = val('decTerm'); f.start = val('decStart'); f.end = val('decEnd');
        if (!(parseInt(f.term_months, 10) > 0) || !f.start || !f.end) { toast('Enter the approved term and the new contract dates', false); return; }
    }
    if (!confirm('Record the Council decision "' + d.replace('_', ' ') + '" and email the employee?')) return;
    busy('btnDecision', true);
    post('decision', f, function (res) {
        busy('btnDecision', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('decisionModal'); reload(); }
    });
}

/* ── issue contract ── */
function issueContract() {
    var c = document.getElementById('icConfirm');
    if (!c || !c.checked) { toast('Tick the confirmation first', false); return; }
    var f = {
        id: CR_ID, confirm: '1', start: val('icStart'), end: val('icEnd'),
        job_id: val('icJob'), dept_id: val('icDept'), contract_type: val('icType'),
        payscale: val('icScale'), fixedamount: val('icFixed') || '0'
    };
    if (!f.start || !f.end) { toast('Enter the contract start and end dates', false); return; }
    if (f.end <= f.start) { toast('The contract must end after it starts', false); return; }
    var curEnd = (document.getElementById('<%= hfCurEnd.ClientID %>') || {}).value || '';
    var warn = curEnd && f.start <= curEnd ? '\n\nNote: the new contract starts on or before the current contract end (' + curEnd + ').' : '';
    if (!confirm('Issue the new contract ' + f.start + ' to ' + f.end + '?\n\nThis creates a VALID contract and marks the current one superseded. It cannot be undone from this screen.' + warn)) return;
    busy('btnIssue', true);
    post('issue_contract', f, function (res) {
        busy('btnIssue', false); toast(res.msg, res.ok);
        if (res.ok) { closeModal('issueModal'); reload(); }
    });
}
</script>
</asp:Content>
