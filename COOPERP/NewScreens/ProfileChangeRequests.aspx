<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ProfileChangeRequests.aspx.cs" Inherits="COOPERP_NewScreens_ProfileChangeRequests" Title="Profile change requests - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<script src="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.js") %>?v=1"></script>
<style>
a.hr-kpi.pcr-kpi--on { border-color: var(--hr-navy); }
.pcr-apply { width: 60px; text-align: center; }
.pcr-apply input { accent-color: var(--hr-navy); }
.pcr-was { display: block; font-size: 11px; color: var(--hr-muted); margin-top: 2px; }
.pcr-block { margin: 14px 0 6px; font-size: 11px; font-weight: 700; color: var(--hr-navy); text-transform: uppercase; letter-spacing: .4px; }
.pcr-reason { font-size: 12px; color: var(--hr-text); white-space: pre-wrap; margin: 0; }
.pcr-modal .hr-table td { vertical-align: top; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8.5" cy="7" r="4"/><polyline points="17 11 19 13 23 9"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Profile change requests</div>
            <div class="hr-header__sub">Staff requests to correct their records</div>
        </div>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRDashboard.aspx">Overview</a><a class="hr-tab" href="HREmployees.aspx">Employees</a><a class="hr-tab" href="HRContracts.aspx">Contracts</a><a class="hr-tab" href="ContractRenewals.aspx">Renewals</a><a class="hr-tab" href="LeaveApplications.aspx">Leave applications</a><a class="hr-tab" href="HRLeaveManagement.aspx">Leave balances</a><a class="hr-tab hr-tab--active" href="ProfileChangeRequests.aspx">Profile requests</a></div>

<asp:Literal ID="litKpis" runat="server" />

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="fltSearch">Search</label>
        <input type="text" id="fltSearch" class="hr-input" placeholder="Name or staff number" value="<%= HttpUtility.HtmlAttributeEncode(SearchValue) %>" />
    </div>
    <div class="hr-filter">
        <label for="fltStatus">Status</label>
        <select id="fltStatus" class="hr-select"><asp:Literal ID="litStatusOptions" runat="server" /></select>
    </div>
    <div class="hr-filters__actions">
        <button type="button" class="hr-btn hr-btn--primary" onclick="applyFilters()">Apply</button>
        <a class="hr-btn hr-btn--secondary" href="ProfileChangeRequests.aspx">Reset</a>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head">
        <div class="hr-card__title">Requests <span class="hr-card__meta"><asp:Literal ID="litTotalCount" runat="server" /></span></div>
        <div class="hr-row"><asp:Literal ID="litExport" runat="server" /></div>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th>Staff no</th>
                    <th>Name</th>
                    <th>Department</th>
                    <th>Fields requested</th>
                    <th>Submitted</th>
                    <th>Status</th>
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

<!-- Request detail -->
<div class="hr-modal pcr-modal" id="reqModal">
    <div class="hr-modal__box hr-modal__box--wide">
        <div class="hr-modal__head"><span id="reqTitle">Profile change request</span><button type="button" class="hr-modal__close" onclick="closeModal('reqModal')" aria-label="Close">&times;</button></div>
        <div class="hr-modal__body">
            <div id="reqLoading" class="hr-muted">Loading</div>
            <div id="reqBody" style="display:none">
                <dl class="hr-dl">
                    <div><dt>Name</dt><dd id="dName"></dd></div>
                    <div><dt>Staff no</dt><dd id="dCode"></dd></div>
                    <div><dt>Status</dt><dd id="dStatus"></dd></div>
                    <div><dt>Department</dt><dd id="dDept"></dd></div>
                    <div><dt>Position</dt><dd id="dJob"></dd></div>
                    <div><dt>Staff category</dt><dd id="dCat"></dd></div>
                    <div><dt>Submitted</dt><dd id="dSubmitted"></dd></div>
                    <div id="dRevByWrap"><dt>Reviewed by</dt><dd id="dRevBy"></dd></div>
                    <div id="dRevAtWrap"><dt>Reviewed on</dt><dd id="dRevAt"></dd></div>
                </dl>

                <div class="pcr-block">Reason</div>
                <p class="pcr-reason" id="dReason"></p>

                <div class="pcr-block">Changes</div>
                <div class="hr-table-wrap">
                    <table class="hr-table">
                        <thead><tr><th>Field</th><th>Current record</th><th>Requested</th><th class="pcr-apply" id="thApply">Apply</th></tr></thead>
                        <tbody id="dRows"></tbody>
                    </table>
                </div>

                <div id="noteEdit">
                    <div class="pcr-block"><label for="reviewNote">Review note</label></div>
                    <textarea id="reviewNote" class="hr-textarea" rows="2" maxlength="2000"></textarea>
                    <div class="hr-hint">Required when rejecting. The staff member sees it.</div>
                </div>
                <div id="noteView" style="display:none">
                    <div class="pcr-block">Review note</div>
                    <p class="pcr-reason" id="dNote"></p>
                </div>
            </div>
            <div class="hr-error" id="reqResult" role="alert"></div>
        </div>
        <div class="hr-modal__foot" id="reqFootPending">
            <button type="button" class="hr-btn hr-btn--danger" onclick="doReject()">Reject</button>
            <span class="hr-spacer"></span>
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('reqModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" onclick="doApprove()">Approve selected</button>
        </div>
        <div class="hr-modal__foot" id="reqFootDone" style="display:none">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('reqModal')">Close</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="hrToast" role="status" aria-live="polite"></div>

</div>

<script type="text/javascript">
function el(id){ return document.getElementById(id); }
function openModal(id){ el(id).classList.add('is-open'); }
function closeModal(id){ el(id).classList.remove('is-open'); }
function esc(s){ var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }
function showToast(msg, isErr){
    var t = el('hrToast');
    t.textContent = msg;
    t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
    clearTimeout(t._tmr);
    t._tmr = setTimeout(function(){ t.className = 'hr-toast'; }, 4000);
}

/* Filters */
function applyFilters(){
    var p = [], q = el('fltSearch').value.trim(), s = el('fltStatus').value;
    if(s) p.push('status=' + encodeURIComponent(s));
    if(q) p.push('q=' + encodeURIComponent(q));
    location.href = 'ProfileChangeRequests.aspx' + (p.length ? '?' + p.join('&') : '');
}
el('fltSearch').addEventListener('keydown', function(e){ if(e.key === 'Enter'){ e.preventDefault(); applyFilters(); } });
el('fltStatus').addEventListener('change', applyFilters);

/* Requests */
function getJson(url, cb){
    var x = new XMLHttpRequest();
    x.open('GET', url, true);
    x.onreadystatechange = function(){
        if(x.readyState !== 4) return;
        var r; try { r = JSON.parse(x.responseText); } catch(e){ r = { ok:false, msg:'The server did not respond. Reload the page and try again.' }; }
        if(r && r.msg == null && r.message) r.msg = r.message;
        cb(r);
    };
    x.send();
}
function postJson(url, data, cb){
    var x = new XMLHttpRequest();
    x.open('POST', url, true);
    x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
    var m = document.querySelector('meta[name="csrf-token"]');
    if(m) x.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
    x.onreadystatechange = function(){
        if(x.readyState !== 4) return;
        var r; try { r = JSON.parse(x.responseText); } catch(e){ r = { ok:false, msg:'The server did not respond. Reload the page and try again.' }; }
        if(r && r.msg == null && r.message) r.msg = r.message;
        cb(r);
    };
    x.send(JSON.stringify(data));
}

var CUR = null, BUSY = false;
function openRequest(id){
    CUR = null;
    el('reqTitle').textContent = 'Profile change request';
    el('reqLoading').style.display = '';
    el('reqBody').style.display = 'none';
    el('reqResult').textContent = '';
    el('reqFootPending').style.display = 'none';
    el('reqFootDone').style.display = 'none';
    openModal('reqModal');
    getJson('ProfileChangeRequests.aspx?ajax=detail&id=' + encodeURIComponent(id), function(r){
        el('reqLoading').style.display = 'none';
        if(!r || !r.ok){ el('reqResult').textContent = (r && r.msg) || 'This request could not be loaded.'; el('reqFootDone').style.display = ''; return; }
        CUR = r;
        renderRequest(r);
    });
}
function renderRequest(r){
    el('reqTitle').textContent = 'Profile change request: ' + r.emp_name;
    el('dName').textContent = r.emp_name;
    el('dCode').textContent = r.emp_code;
    el('dStatus').innerHTML = '<span class="hr-badge hr-badge--' + esc(r.status_kind) + '">' + esc(r.status_word) + '</span>';
    el('dDept').textContent = r.dept || 'Not recorded';
    el('dJob').textContent = r.job || 'Not recorded';
    el('dCat').textContent = r.category;
    el('dSubmitted').textContent = r.submitted;
    el('dRevBy').textContent = r.reviewed_by;
    el('dRevAt').textContent = r.reviewed_at;
    el('dRevByWrap').style.display = r.pending ? 'none' : '';
    el('dRevAtWrap').style.display = r.pending ? 'none' : '';
    el('dReason').textContent = r.reason || 'Not given';
    el('thApply').textContent = r.pending ? 'Apply' : 'Applied';

    var h = '';
    for(var i = 0; i < r.rows.length; i++){
        var x = r.rows[i], cur = esc(x.current);
        if(r.pending && x.changed) cur += ' <span class="hr-badge hr-badge--warn">Changed since the request</span><span class="pcr-was">When requested: ' + (esc(x.old) || 'blank') + '</span>';
        var apply;
        if(r.pending){
            if(x.blocked) apply = '<input type="checkbox" disabled="disabled" aria-label="Apply" /><span class="pcr-was">' + esc(x.blocked) + '</span>';
            else apply = '<input type="checkbox" class="pcr-tick" data-field="' + esc(x.field) + '" data-changed="' + (x.changed ? '1' : '0') + '"' + (x.changed ? '' : ' checked="checked"') + ' aria-label="Apply ' + esc(x.label) + '" />';
        } else if(x.applied === true){
            apply = '<span class="hr-badge hr-badge--ok">Yes</span>' + (x.applied_note && x.applied_note !== 'Applied' ? '<span class="pcr-was">' + esc(x.applied_note) + '</span>' : '');
        } else if(x.applied === false){
            apply = '<span class="hr-badge hr-badge--neutral">No</span><span class="pcr-was">' + esc(x.applied_note) + '</span>';
        } else apply = '';
        h += '<tr><td>' + esc(x.label) + '</td><td>' + cur + '</td><td>' + esc(x.requested) + '</td><td class="pcr-apply">' + apply + '</td></tr>';
    }
    el('dRows').innerHTML = h || '<tr><td colspan="4" class="hr-empty">No changes in this request.</td></tr>';

    el('noteEdit').style.display = r.pending ? '' : 'none';
    el('noteView').style.display = r.pending || !r.review_note ? 'none' : '';
    el('dNote').textContent = r.review_note || '';
    el('reviewNote').value = '';
    el('reqFootPending').style.display = r.pending ? '' : 'none';
    el('reqFootDone').style.display = r.pending ? 'none' : '';
    el('reqBody').style.display = '';
}

function afterAction(r){
    BUSY = false;
    if(!r || !r.ok){ el('reqResult').textContent = (r && r.msg) || 'The action could not be completed.'; return; }
    var u = location.pathname + location.search.replace(/([?&])(msg|ok|id)=[^&]*/g, '$1').replace(/[?&]+$/, '').replace(/\?&+/, '?');
    location.href = u + (u.indexOf('?') >= 0 ? '&' : '?') + 'msg=' + encodeURIComponent(r.msg) + '&ok=1';
}
function doApprove(){
    if(!CUR || BUSY) return;
    var ticks = document.querySelectorAll('.pcr-tick'), fields = [], ack = [], labels = [];
    for(var i = 0; i < ticks.length; i++){
        if(!ticks[i].checked) continue;
        fields.push(ticks[i].getAttribute('data-field'));
        if(ticks[i].getAttribute('data-changed') === '1') ack.push(ticks[i].getAttribute('data-field'));
    }
    if(!fields.length){ el('reqResult').textContent = 'Tick at least one field to approve, or reject the request.'; return; }
    var n = fields.length;
    hrConfirm({ title: 'Approve request', message: 'Apply ' + n + (n === 1 ? ' field' : ' fields') + ' to the record of ' + CUR.emp_name + (n < CUR.rows.length ? '? The other fields are not applied.' : '?'), ok: 'Approve' }, function(){
        BUSY = true;
        el('reqResult').textContent = '';
        postJson('ProfileChangeRequests.aspx?ajax=approve', { id: CUR.id, fields: fields, ack: ack, note: el('reviewNote').value.trim() }, afterAction);
    });
}
function doReject(){
    if(!CUR || BUSY) return;
    var note = el('reviewNote').value.trim();
    if(!note){ el('reqResult').textContent = 'Enter a note for the staff member saying why the request is rejected.'; el('reviewNote').focus(); return; }
    hrConfirm({ title: 'Reject request', message: 'Reject this request from ' + CUR.emp_name + '? The record is not changed.', ok: 'Reject', danger: true }, function(){
        BUSY = true;
        el('reqResult').textContent = '';
        postJson('ProfileChangeRequests.aspx?ajax=reject', { id: CUR.id, note: note }, afterAction);
    });
}

document.addEventListener('keydown', function(e){ if(e.key === 'Escape') closeModal('reqModal'); });
</script>
</asp:Content>
