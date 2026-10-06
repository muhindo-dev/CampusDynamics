<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="LeaveApplicationForm.aspx.cs" Inherits="COOPERP_NewScreens_LeaveApplicationForm" Title="Leave application - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=1" />
<style>
.lf-doc { max-width: 900px; margin: 0 auto; }
.lf-stages { background: #fff; border: 1px solid var(--hr-border); border-radius: 4px; padding: 14px 10px 10px; margin-bottom: 14px; }
.lf-policy { margin: 0 0 12px; }
.lf-sec--now { border-color: var(--hr-accent); }
.lf-sec--locked .hr-card__head { background: #fff; }
.lf-sec--locked .hr-card__title { color: var(--hr-muted); }
.lf-group { font-size: 11px; font-weight: 700; color: var(--hr-navy); text-transform: uppercase; letter-spacing: .4px; margin: 16px 0 10px; padding-bottom: 4px; border-bottom: 1px solid var(--hr-border); }
.lf-group:first-child { margin-top: 0; }
.lf-wide { grid-column: 1 / -1; }
.lf-err { display: none; }
.is-invalid .lf-err { display: block; }
.is-invalid .hr-input, .is-invalid .hr-select, .is-invalid .hr-textarea { border-color: var(--hr-bad); }
.lf-days { margin-top: -4px; }
.lf-days strong { color: var(--hr-text); }
.lf-actions { position: sticky; bottom: 0; z-index: 10; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; background: #fff; border: 1px solid var(--hr-border); border-top: 2px solid var(--hr-navy); padding: 10px 14px; margin-top: 14px; }
.lf-actions:empty { display: none; }
.lf-note { font-size: 12px; color: var(--hr-text-2); }
.lf-summary { background: var(--hr-surface); border: 1px solid var(--hr-border); padding: 8px 10px; margin-bottom: 12px; font-size: 12px; line-height: 1.5; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
        </div>
        <div style="min-width:0">
            <div class="hr-header__title"><asp:Literal ID="litPageTitle" runat="server">Leave application</asp:Literal> <asp:Literal ID="litStatusBadge" runat="server" /></div>
            <div class="hr-header__sub"><asp:Literal ID="litHeaderSub" runat="server" /></div>
        </div>
    </div>
    <div class="hr-header__actions">
        <a class="hr-btn hr-btn--inverse" href="LeaveApplications.aspx">All applications</a>
        <asp:Literal ID="litPrintBtn" runat="server" />
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab hr-tab--active" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a></div>

<div class="lf-doc">
    <div class="lf-stages"><asp:Literal ID="litTimeline" runat="server" /></div>

    <p class="hr-hint lf-policy">Annual leave is one month a year on full pay for permanent staff. Approval goes to the Head of Department, HR and the Vice Chancellor. Report to HR on return.</p>

    <asp:Literal ID="litSection1" runat="server" />
    <asp:Literal ID="litSection2" runat="server" />
    <asp:Literal ID="litSection3" runat="server" />
    <asp:Literal ID="litSection4" runat="server" />

    <asp:Literal ID="litAuditTrail" runat="server" />

    <div class="lf-actions" id="actionBar"><asp:Literal ID="litActionBar" runat="server" /></div>
</div>

<!-- Head of Department: approve -->
<div class="hr-modal" id="modalHodApprove">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Approve and forward to HR</span><button type="button" class="hr-modal__close" onclick="closeModal('modalHodApprove')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field hr-full">
                    <label class="hr-label" for="hodHandoverTo">Duties handed over to</label>
                    <input type="text" id="hodHandoverTo" class="hr-input" placeholder="Title and full name" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label" for="hodNotes">Remarks</label>
                    <textarea id="hodNotes" class="hr-textarea"></textarea>
                </div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('modalHodApprove')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnHodApproveConfirm" onclick="submitHodApprove()">Approve</button>
        </div>
    </div>
</div>

<!-- Head of Department: decline -->
<div class="hr-modal" id="modalHodDecline">
    <div class="hr-modal__box" style="width:440px">
        <div class="hr-modal__head"><span>Decline application</span><button type="button" class="hr-modal__close" onclick="closeModal('modalHodDecline')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-field">
                <label class="hr-label" for="hodDeclineReason">Reason <span class="hr-req">*</span></label>
                <textarea id="hodDeclineReason" class="hr-textarea"></textarea>
                <div class="hr-hint">The employee sees this reason.</div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('modalHodDecline')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--danger" id="btnHodDeclineConfirm" onclick="submitHodDecline()">Decline</button>
        </div>
    </div>
</div>

<!-- HR: approve -->
<div class="hr-modal" id="modalHrApprove">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Approve and forward to the Vice Chancellor</span><button type="button" class="hr-modal__close" onclick="closeModal('modalHrApprove')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field">
                    <label class="hr-label" for="hrEffFrom">Leave from <span class="hr-req">*</span></label>
                    <input type="date" id="hrEffFrom" class="hr-input" />
                </div>
                <div class="hr-field">
                    <label class="hr-label" for="hrEffTo">Leave to <span class="hr-req">*</span></label>
                    <input type="date" id="hrEffTo" class="hr-input" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label" for="hrNotes">HR notes</label>
                    <textarea id="hrNotes" class="hr-textarea" placeholder="Leave balance, benefits, conditions"></textarea>
                </div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('modalHrApprove')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnHrApproveConfirm" onclick="submitHrApprove()">Approve</button>
        </div>
    </div>
</div>

<!-- HR: decline -->
<div class="hr-modal" id="modalHrDecline">
    <div class="hr-modal__box" style="width:440px">
        <div class="hr-modal__head"><span>Decline application</span><button type="button" class="hr-modal__close" onclick="closeModal('modalHrDecline')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="hr-field">
                <label class="hr-label" for="hrDeclineReason">Reason <span class="hr-req">*</span></label>
                <textarea id="hrDeclineReason" class="hr-textarea"></textarea>
                <div class="hr-hint">The employee sees this reason.</div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('modalHrDecline')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--danger" id="btnHrDeclineConfirm" onclick="submitHrDecline()">Decline</button>
        </div>
    </div>
</div>

<!-- Vice Chancellor: decision -->
<div class="hr-modal" id="modalVc">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Vice Chancellor's decision</span><button type="button" class="hr-modal__close" onclick="closeModal('modalVc')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div class="lf-summary" id="vcSummary"></div>
            <div class="hr-form">
                <div class="hr-field hr-full">
                    <span class="hr-label" id="vcDecisionLbl">Decision <span class="hr-req">*</span></span>
                    <div class="hr-radios" role="radiogroup" aria-labelledby="vcDecisionLbl">
                        <label><input type="radio" name="vcDecision" value="GRANTED" />Grant</label>
                        <label><input type="radio" name="vcDecision" value="NOT_GRANTED" />Do not grant</label>
                        <label><input type="radio" name="vcDecision" value="POSTPONED" />Postpone</label>
                    </div>
                </div>
                <div class="hr-field vc-grant">
                    <label class="hr-label" for="vcAccumDays">Accumulated leave days</label>
                    <input type="number" id="vcAccumDays" class="hr-input" min="0" />
                </div>
                <div class="hr-field vc-grant">
                    <label class="hr-label" for="vcDaysTaken">Days granted</label>
                    <input type="number" id="vcDaysTaken" class="hr-input" min="0" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label" for="vcReason" id="vcReasonLbl">Remarks</label>
                    <textarea id="vcReason" class="hr-textarea"></textarea>
                </div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('modalVc')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnVcConfirm" onclick="submitVcDecision()">Record decision</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="paToast" role="status" aria-live="polite"></div>
<input type="hidden" id="hdnAppId" value="<asp:Literal ID="litAppId" runat="server"></asp:Literal>" />

</div>

<script type="text/javascript">
var APP_ID = parseInt(document.getElementById('hdnAppId').value, 10) || 0;
var LF_APP = window.LF_APP || {};

function el(id){ return document.getElementById(id); }
function val(id){ var e = el(id); return e ? (e.value || '').trim() : ''; }
function openModal(id){ el(id).classList.add('is-open'); }
function closeModal(id){ el(id).classList.remove('is-open'); }

function showToast(msg, type){
    var t = el('paToast');
    t.textContent = msg;
    t.className = 'hr-toast is-on' + (type === 'err' ? ' hr-toast--err' : '');
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function(){ t.className = 'hr-toast'; }, 3500);
}

// ── Server calls ──────────────────────────────────────────────────────────────
function doAction(action, data, btn, onSuccess){
    var oldHtml = btn ? btn.innerHTML : '';
    if(btn){ btn.disabled = true; btn.textContent = 'Please wait'; }
    var body = 'id=' + encodeURIComponent(APP_ID);
    for(var k in data){ if(data.hasOwnProperty(k)) body += '&' + encodeURIComponent(k) + '=' + encodeURIComponent(data[k]); }
    fetch('LeaveApplicationForm.aspx?ajax=' + action, {
        method: 'POST', credentials: 'same-origin',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: body
    })
    .then(function(r){ return r.json(); })
    .then(function(d){
        if(btn){ btn.disabled = false; btn.innerHTML = oldHtml; }
        if(d.ok) onSuccess(d);
        else showToast(d.error || 'The change could not be saved.', 'err');
    })
    .catch(function(){
        if(btn){ btn.disabled = false; btn.innerHTML = oldHtml; }
        showToast('The change could not be saved. Check your connection and try again.', 'err');
    });
}
function reloadSuccess(msg, newId){
    showToast(msg, 'ok');
    setTimeout(function(){
        if(newId && !APP_ID) location.href = 'LeaveApplicationForm.aspx?id=' + newId;
        else location.reload();
    }, 1000);
}

// ── Dates ─────────────────────────────────────────────────────────────────────
var LF_DAYS = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
var LF_MONTHS = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
function parseYmd(s){
    var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s || '');
    if(!m) return null;
    var d = new Date(Date.UTC(+m[1], +m[2]-1, +m[3]));
    return (isNaN(d.getTime()) || d.getUTCDate() !== +m[3]) ? null : d;
}
function fmtDay(d){ return LF_DAYS[d.getUTCDay()] + ' ' + d.getUTCDate() + ' ' + LF_MONTHS[d.getUTCMonth()] + ' ' + d.getUTCFullYear(); }
function daysBetween(a, b){ return Math.round((b - a) / 864e5) + 1; }
function todayUtc(){ var n = new Date(); return new Date(Date.UTC(n.getFullYear(), n.getMonth(), n.getDate())); }

// ── Section 1 ─────────────────────────────────────────────────────────────────
function checkedVal(name){
    var r = document.querySelector('input[name="' + name + '"]:checked');
    return r ? r.value : '';
}
function setFieldError(fieldId, msg){
    var f = el('fld_' + fieldId);
    if(!f) return;
    var e = f.querySelector('.lf-err');
    if(msg){ f.classList.add('is-invalid'); if(e) e.textContent = msg; }
    else   { f.classList.remove('is-invalid'); if(e) e.textContent = ''; }
}
function clearErrors(){
    var all = document.querySelectorAll('#sec1Form .is-invalid');
    for(var i = 0; i < all.length; i++) all[i].classList.remove('is-invalid');
}
function updateLeaveSummary(){
    var box = el('leaveSummary'); if(!box) return;
    var f = parseYmd(val('leaveFrom')), t = parseYmd(val('leaveTo'));
    if(el('leaveTo') && val('leaveFrom')) el('leaveTo').min = val('leaveFrom');
    if(!f || !t){ box.textContent = 'Calendar days, weekends included.'; return; }
    if(t < f){ box.textContent = 'The last day is before the first day.'; return; }
    var n = daysBetween(f, t);
    box.innerHTML = '<strong>' + n + (n === 1 ? ' day' : ' days') + '</strong>, ' + fmtDay(f) + ' to ' + fmtDay(t) +
        (f < todayUtc() ? '. The first day is in the past.' : '');
}

// submit=false (draft): only what the server needs to store a draft.
// submit=true: every field marked * on the form.
function collectSection1(submit){
    clearErrors();
    var errs = [];
    function need(id, msg){ if(!val(id)){ errs.push(id); setFieldError(id, msg); } }

    var lType = checkedVal('leaveTypeRadio');
    if(!lType){ errs.push('leaveType'); setFieldError('leaveType', 'Choose the type of leave.'); }

    var lfrom = val('leaveFrom'), lto = val('leaveTo');
    var f = parseYmd(lfrom), t = parseYmd(lto);
    if(!f){ errs.push('leaveFrom'); setFieldError('leaveFrom', 'Enter the first day of leave.'); }
    if(!t){ errs.push('leaveTo');   setFieldError('leaveTo',   'Enter the last day of leave.'); }
    if(f && t && t < f){ errs.push('leaveTo'); setFieldError('leaveTo', 'The last day must be on or after the first day.'); }

    if(submit) need('substituteArrangement', 'Say who will cover your duties.');
    if(submit) need('supUsername', 'Choose the person who approves your leave.');
    need('empName', 'Enter your full name.');
    if(submit) need('empDept', 'Enter your faculty, department or section.');
    if(submit) need('mobileContact', 'Enter your mobile number.');

    var phones = ['mobileContact', 'nokMobile'];
    for(var i = 0; i < phones.length; i++){
        var p = val(phones[i]);
        if(p && (p.replace(/\D/g, '').length < 9 || /[^0-9+\-\s()\/]/.test(p)) && errs.indexOf(phones[i]) < 0){
            errs.push(phones[i]); setFieldError(phones[i], 'Enter a phone number with at least 9 digits.');
        }
    }

    if(errs.length){
        var first = el('fld_' + errs[0]);
        if(first){
            first.scrollIntoView({ behavior: 'smooth', block: 'center' });
            var inp = first.querySelector('input:not([type=hidden]),select,textarea');
            if(inp) setTimeout(function(){ try{ inp.focus({ preventScroll: true }); }catch(e){ inp.focus(); } }, 350);
        }
        showToast(errs.length === 1 ? 'Correct the highlighted field.' : 'Correct the ' + errs.length + ' highlighted fields.', 'err');
        return null;
    }

    return {
        emp_name: val('empName'), emp_code: val('empCode'), faculty_dept: val('empDept'),
        office_location: val('officeLocation'), position_held: val('positionHeld'),
        residence_address: val('residenceAddress'), mobile_contact: val('mobileContact'),
        nok_name: val('nokName'), nok_address: val('nokAddress'), nok_mobile: val('nokMobile'),
        leave_type: lType, leave_from: lfrom, leave_to: lto, num_days: daysBetween(f, t),
        substitute_arrangement: val('substituteArrangement')
    };
}

function saveDraft(submit){
    var data = collectSection1(submit);
    if(!data) return;
    data.submit = submit ? '1' : '0';
    var sup = el('supUsername');
    data.supervisor_username = sup ? sup.value : '';
    data.supervisor_name = (sup && sup.value && sup.options[sup.selectedIndex]) ? (sup.options[sup.selectedIndex].getAttribute('data-name') || sup.options[sup.selectedIndex].text) : '';
    if(submit && !confirm('Submit this application to ' + (data.supervisor_name || 'your Head of Department') + '? It cannot be edited after submission.')) return;
    doAction(submit ? 'submit' : 'save_draft', data, submit ? el('btnSubmitApp') : el('btnSaveDraft'), function(d){
        reloadSuccess(submit ? 'Application submitted.' : 'Draft saved.', d.id);
    });
}

function initSection1(){
    if(!el('sec1Form')) return;
    var radios = document.querySelectorAll('input[name="leaveTypeRadio"]');
    for(var i = 0; i < radios.length; i++) radios[i].addEventListener('change', function(){ setFieldError('leaveType', ''); });
    ['leaveFrom','leaveTo'].forEach(function(id){
        var e = el(id); if(!e) return;
        e.addEventListener('change', function(){ setFieldError('leaveFrom',''); setFieldError('leaveTo',''); updateLeaveSummary(); });
        e.addEventListener('input', updateLeaveSummary);
    });
    var inputs = document.querySelectorAll('#sec1Form input, #sec1Form select, #sec1Form textarea');
    for(var j = 0; j < inputs.length; j++){
        if(!inputs[j].id) continue;
        inputs[j].addEventListener('input',  (function(id){ return function(){ setFieldError(id, ''); }; })(inputs[j].id));
        inputs[j].addEventListener('change', (function(id){ return function(){ setFieldError(id, ''); }; })(inputs[j].id));
    }
    updateLeaveSummary();
}

// ── Head of Department ────────────────────────────────────────────────────────
function openHodApprove(){
    var h = el('hodHandoverTo');
    if(h && !h.value && LF_APP.cover) h.value = LF_APP.cover;
    openModal('modalHodApprove');
}
function submitHodApprove(){
    doAction('hod_approve', { hod_handover_to: val('hodHandoverTo'), hod_notes: val('hodNotes') },
        el('btnHodApproveConfirm'), function(){ closeModal('modalHodApprove'); reloadSuccess('Approved and forwarded to HR.'); });
}
function submitHodDecline(){
    var reason = val('hodDeclineReason');
    if(!reason){ showToast('Give a reason for declining.', 'err'); el('hodDeclineReason').focus(); return; }
    doAction('hod_decline', { reason: reason }, el('btnHodDeclineConfirm'),
        function(){ closeModal('modalHodDecline'); reloadSuccess('Application declined.'); });
}

// ── HR ────────────────────────────────────────────────────────────────────────
function openHrApprove(){
    if(!val('hrEffFrom') && LF_APP.from) el('hrEffFrom').value = LF_APP.from;
    if(!val('hrEffTo')   && LF_APP.to)   el('hrEffTo').value   = LF_APP.to;
    openModal('modalHrApprove');
}
function submitHrApprove(){
    var ef = val('hrEffFrom'), et = val('hrEffTo');
    if(!ef || !et){ showToast('Enter both leave dates.', 'err'); return; }
    if(et < ef){ showToast('The end date must be on or after the start date.', 'err'); return; }
    doAction('hr_approve', { hr_effective_from: ef, hr_effective_to: et, hr_notes: val('hrNotes') },
        el('btnHrApproveConfirm'), function(){ closeModal('modalHrApprove'); reloadSuccess('Approved and forwarded to the Vice Chancellor.'); });
}
function submitHrDecline(){
    var reason = val('hrDeclineReason');
    if(!reason){ showToast('Give a reason for declining.', 'err'); el('hrDeclineReason').focus(); return; }
    doAction('hr_decline', { reason: reason }, el('btnHrDeclineConfirm'),
        function(){ closeModal('modalHrDecline'); reloadSuccess('Application declined.'); });
}

// ── Vice Chancellor ───────────────────────────────────────────────────────────
function onVcDecisionChange(){
    var d = checkedVal('vcDecision');
    var g = document.querySelectorAll('.vc-grant');
    for(var i = 0; i < g.length; i++) g[i].style.display = (d === 'GRANTED') ? '' : 'none';
    var needReason = (d === 'NOT_GRANTED' || d === 'POSTPONED');
    el('vcReasonLbl').innerHTML = needReason ? 'Reason <span class="hr-req">*</span>' : 'Remarks';
    var btn = el('btnVcConfirm');
    btn.className = 'hr-btn ' + (d === 'NOT_GRANTED' ? 'hr-btn--danger' : 'hr-btn--primary');
    btn.textContent = d === 'GRANTED' ? 'Grant leave' : d === 'NOT_GRANTED' ? 'Do not grant' : d === 'POSTPONED' ? 'Postpone leave' : 'Record decision';
}
function openVcDecision(preset){
    var radios = document.querySelectorAll('input[name="vcDecision"]');
    for(var i = 0; i < radios.length; i++) radios[i].checked = (radios[i].value === preset);
    if(!val('vcDaysTaken') && LF_APP.days) el('vcDaysTaken').value = LF_APP.days;
    var s = el('vcSummary');
    s.innerHTML = LF_APP.summary || '';
    s.style.display = LF_APP.summary ? '' : 'none';
    onVcDecisionChange();
    openModal('modalVc');
}
function submitVcDecision(){
    var d = checkedVal('vcDecision');
    if(!d){ showToast('Choose Grant, Do not grant or Postpone.', 'err'); return; }
    var reason = val('vcReason');
    if(d !== 'GRANTED' && !reason){ showToast('Give a reason for this decision.', 'err'); el('vcReason').focus(); return; }
    doAction('vc_decision', {
        vc_decision: d, vc_reason: reason,
        vc_accumulated_days: d === 'GRANTED' ? val('vcAccumDays') : '',
        vc_days_taken: d === 'GRANTED' ? val('vcDaysTaken') : ''
    }, el('btnVcConfirm'), function(){
        closeModal('modalVc');
        reloadSuccess(d === 'GRANTED' ? 'Leave granted.' : d === 'NOT_GRANTED' ? 'Leave not granted.' : 'Leave postponed.');
    });
}
(function(){
    var radios = document.querySelectorAll('input[name="vcDecision"]');
    for(var i = 0; i < radios.length; i++) radios[i].addEventListener('change', onVcDecisionChange);
})();

// ── Cancel (HR) ───────────────────────────────────────────────────────────────
function cancelAppForm(){
    if(!confirm('Cancel this leave application? This cannot be undone.')) return;
    doAction('cancel', {}, null, function(){
        showToast('Application cancelled.', 'ok');
        setTimeout(function(){ location.href = 'LeaveApplications.aspx'; }, 1000);
    });
}

document.addEventListener('keydown', function(e){
    if(e.key === 'Escape'){
        var open = document.querySelectorAll('.hr-modal.is-open');
        for(var i = 0; i < open.length; i++) open[i].classList.remove('is-open');
    }
    if(e.key === 'Enter' && e.target && e.target.tagName === 'INPUT') e.preventDefault();
});

initSection1();
</script>
</asp:Content>
