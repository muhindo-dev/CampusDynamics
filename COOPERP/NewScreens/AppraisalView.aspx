<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AppraisalView.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalView" Title="Appraisals" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.pa-chk { width: 32px; text-align: center; }
.pa-table-text td { vertical-align: top; }
.pa-group td { background: var(--hr-surface) !important; font-weight: 600; color: var(--hr-navy); }
.pa-na td { color: var(--hr-muted); }
.pa-rating { white-space: nowrap; }
.pa-files a { display: block; color: var(--hr-accent); text-decoration: none; margin-top: 2px; }
.pa-files a:hover { text-decoration: underline; }
.pa-actions { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 14px; }
.pa-q { font-weight: 600; margin-bottom: 4px; }
.pa-a { margin-bottom: 14px; white-space: pre-wrap; }
.pa-list { max-height: 160px; overflow: auto; border: 1px solid var(--hr-border); padding: 6px 10px; margin-bottom: 12px; font-size: 12px; }
.pa-sup-list { width: 100%; height: 200px; border: 1px solid var(--hr-input); font-size: 12px; font-family: inherit; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg></div>
        <div>
            <asp:Literal ID="litCrumb" runat="server" />
            <div class="hr-header__title"><asp:Literal ID="litHeaderTitle" runat="server" Text="Appraisals" /></div>
            <div class="hr-header__sub"><asp:Literal ID="litHeaderSub" runat="server" Text="Staff appraisal records and HR review" /></div>
        </div>
    </div>
    <div class="hr-header__actions">
        <asp:Literal ID="litHeaderActions" runat="server" />
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="AppraisalDashboard.aspx">Overview</a><a class="hr-tab" href="AppraisalSessions.aspx">Sessions</a><a class="hr-tab hr-tab--active" href="AppraisalView.aspx">Appraisals</a><a class="hr-tab" href="AppraisalReports.aspx">Reports</a><a class="hr-tab" href="CompetencyTemplates.aspx">Competencies</a><a class="hr-tab" href="ExpectedStandards.aspx">Expected standards</a></div>

<!-- List -->
<asp:Panel ID="pnlList" runat="server">

<asp:Literal ID="litListStats" runat="server" />

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="txtSearch">Search</label>
        <input type="text" id="txtSearch" class="hr-input" placeholder="Name or staff number" onkeyup="debounceFilter()" autocomplete="off" />
    </div>
    <div class="hr-filter">
        <label for="selSession">Session</label>
        <select id="selSession" class="hr-select" onchange="applyFilter()"><asp:Literal ID="litSessionOptions" runat="server" /></select>
    </div>
    <div class="hr-filter" id="fltStatus">
        <label for="selStatus">Status</label>
        <select id="selStatus" class="hr-select" onchange="applyFilter()">
            <option value="">Awaiting HR and HR reviewed</option>
            <option value="COMPLETED">Awaiting HR</option>
            <option value="HR_REVIEWED">HR reviewed</option>
            <option value="CANCELLED">Cancelled</option>
        </select>
    </div>
    <div class="hr-filter">
        <label for="selCategory">Category</label>
        <select id="selCategory" class="hr-select" onchange="applyFilter()">
            <option value="">All categories</option>
            <option value="ACADEMIC">Academic</option>
            <option value="ADMINISTRATIVE">Administrative</option>
            <option value="SUPPORT">Support</option>
        </select>
    </div>
    <div class="hr-filter">
        <label for="selReviewer">Supervisor</label>
        <select id="selReviewer" class="hr-select" onchange="applyFilter()">
            <option value="">All</option>
            <option value="none">Not assigned</option>
        </select>
    </div>
    <div class="hr-filters__actions">
        <button type="button" class="hr-btn hr-btn--secondary" onclick="clearFilters()">Clear</button>
    </div>
</div>

<asp:Literal ID="litListHint" runat="server" />
<div class="hr-card">
    <div class="hr-bulk" id="batchBar">
        <strong id="batchCount">0 selected</strong>
        <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" id="btnBatchHr" onclick="batchHrReview()" disabled>HR review</button>

        <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" id="btnBatchReopen" onclick="batchReopen()" disabled>Reopen for supervisor</button>
        <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" id="btnBatchAssign" onclick="batchAssignReviewer()" disabled>Assign supervisor</button>
        <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" id="btnBatchCancel" onclick="batchCancel()" disabled>Cancel appraisals</button>
        <span class="hr-spacer"></span>
        <button type="button" class="hr-btn hr-btn--inverse hr-btn--sm" onclick="clearSelection()">Clear selection</button>
    </div>
    <div class="hr-card__head">
        <div class="hr-card__title"><asp:Literal ID="litCardTitle" runat="server" Text="Appraisal records" /></div>
        <div class="hr-row">
            <span class="hr-card__meta"><asp:Literal ID="litTotalCount" runat="server" Text="0" /> records</span>
            <select id="selPageSize" class="hr-select" style="width:auto;height:26px;padding:0 6px;font-size:11px;" onchange="applyFilter()">
                <option value="25">25 per page</option>
                <option value="50">50 per page</option>
                <option value="100">100 per page</option>
                <option value="200">200 per page</option>
            </select>
        </div>
    </div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead><asp:Literal ID="litGridHead" runat="server" /></thead>
            <tbody id="gridBody"><asp:Literal ID="litGridBody" runat="server" /></tbody>
        </table>
    </div>
    <div class="hr-card__foot">
        <span><asp:Literal ID="litPagerInfo" runat="server" /></span>
        <div class="hr-pager"><asp:Literal ID="litPager" runat="server" /></div>
    </div>
</div>

</asp:Panel>

<!-- Detail -->
<asp:Panel ID="pnlDetail" runat="server" Visible="false">
<asp:Literal ID="litDetailContent" runat="server" />
</asp:Panel>

<!-- Action dialog (return, cancel, reopen; single and batch) -->
<div class="hr-modal" id="actModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="actTitle">Confirm</span><button type="button" class="hr-modal__close" onclick="closeModal('actModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <p id="actText" style="margin:0 0 12px;"></p>
            <div class="pa-list" id="actList" style="display:none;"></div>
            <div class="hr-field" id="actCommentWrap" style="display:none;">
                <label class="hr-label" for="actComment" id="actCommentLabel">Reason</label>
                <textarea class="hr-textarea" id="actComment" maxlength="1000"></textarea>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('actModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="actOk" onclick="runAction()">Confirm</button>
        </div>
    </div>
</div>

<!-- HR review dialog (single and batch) -->
<div class="hr-modal" id="hrModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="hrTitle">HR review</span><button type="button" class="hr-modal__close" onclick="closeModal('hrModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <dl class="hr-dl hr-dl--2" id="hrSingle" style="margin-bottom:14px;">
                <div><dt>Employee</dt><dd id="hrEmp"></dd></div>
                <div><dt>Session</dt><dd id="hrMeta"></dd></div>
                <div><dt>Score %</dt><dd id="hrPct"></dd></div>
                <div><dt>Classification</dt><dd id="hrCls"></dd></div>
                <div style="grid-column:1/-1;"><dt>Employee acknowledgement</dt><dd id="hrAck"></dd></div>
            </dl>
            <div id="hrBatch" style="display:none;">
                <p style="margin:0 0 6px;" id="hrBatchCount"></p>
                <div class="pa-list" id="hrBatchList"></div>
            </div>
            <div class="hr-field" style="margin-bottom:12px;">
                <span class="hr-label">HR rating <span class="hr-req">*</span></span>
                <div class="hr-radios">
                    <label><input type="radio" name="hr_rating" value="5" /> 5</label>
                    <label><input type="radio" name="hr_rating" value="4" /> 4</label>
                    <label><input type="radio" name="hr_rating" value="3" /> 3</label>
                    <label><input type="radio" name="hr_rating" value="2" /> 2</label>
                    <label><input type="radio" name="hr_rating" value="1" /> 1</label>
                </div>
                <span class="hr-hint">Academic: 5 Exceptional to 1 Unsatisfactory. Administrative and support: 5 Excellent to 1 Poor.</span>
            </div>
            <div class="hr-field" style="margin-bottom:12px;">
                <span class="hr-label">HR recommendation <span class="hr-req">*</span></span>
                <div class="hr-radios">
                    <label><input type="radio" name="hr_recommendation" value="CONFIRM" /> Confirm appointment</label>
                    <label><input type="radio" name="hr_recommendation" value="EXTEND_PROBATION" /> Extend probation</label>
                    <label><input type="radio" name="hr_recommendation" value="PIP" /> Performance improvement plan</label>
                    <label><input type="radio" name="hr_recommendation" value="PROMOTE" /> Promote</label>
                    <label><input type="radio" name="hr_recommendation" value="OTHER" /> Other</label>
                </div>
            </div>
            <div class="hr-field">
                <label class="hr-label" for="hrComments">HR comments</label>
                <textarea class="hr-textarea" id="hrComments" maxlength="2000"></textarea>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('hrModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="hrOk" onclick="hrSubmit()">Save HR review</button>
        </div>
    </div>
</div>

<!-- Supervisor dialog (single change and batch assign) -->
<div class="hr-modal" id="supModal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="supTitle">Change supervisor</span><button type="button" class="hr-modal__close" onclick="closeModal('supModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <dl class="hr-dl hr-dl--2" style="margin-bottom:12px;">
                <div><dt id="supCurrentPrefix">Current supervisor</dt><dd id="supCurrentLabel"></dd></div>
            </dl>
            <div class="hr-field">
                <label class="hr-label" for="supSearchInput">New supervisor</label>
                <input type="text" id="supSearchInput" class="hr-input" placeholder="Type a name or staff number" oninput="filterSupOptions()" autocomplete="off" />
                <select id="supSelect" class="pa-sup-list" size="9"></select>
                <span class="hr-hint" id="supMatchCount"></span>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('supModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="btnSupConfirm" onclick="confirmChangeSupervisor()">Save supervisor</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="paToast"></div>

</div>

<script type="text/javascript">
// ── Filters ──────────────────────────────────────────────────────────
var _filterTimer = null;
function debounceFilter() {
    if (_filterTimer) clearTimeout(_filterTimer);
    _filterTimer = setTimeout(applyFilter, 400);
}
function val(id) { var el = document.getElementById(id); return el ? (el.value || '') : ''; }
function applyFilter() {
    var q = [];
    if (val('txtSearch')) q.push('q=' + encodeURIComponent(val('txtSearch')));
    if (val('selStatus')) q.push('status=' + encodeURIComponent(val('selStatus')));
    if (val('selCategory')) q.push('cat=' + encodeURIComponent(val('selCategory')));
    if (val('selSession') && val('selSession') !== '0') q.push('sid=' + encodeURIComponent(val('selSession')));
    if (val('selReviewer') === 'none') q.push('rev=none');
    if (val('selPageSize') && val('selPageSize') !== '25') q.push('ps=' + encodeURIComponent(val('selPageSize')));
    window.location.href = 'AppraisalView.aspx' + (q.length ? '?' + q.join('&') : '');
}
function clearFilters() { window.location.href = 'AppraisalView.aspx'; }
function goPage(p) {
    var params = new URLSearchParams(window.location.search);
    params.set('page', p);
    window.location.href = 'AppraisalView.aspx?' + params.toString();
}
(function () {
    var p = new URLSearchParams(window.location.search), el;
    el = document.getElementById('txtSearch');   if (el) el.value = p.get('q') || '';
    el = document.getElementById('selStatus');   if (el) el.value = p.get('status') || '';
    el = document.getElementById('selCategory'); if (el) el.value = p.get('cat') || '';
    el = document.getElementById('selReviewer'); if (el) el.value = p.get('rev') === 'none' ? 'none' : '';
    el = document.getElementById('selPageSize'); if (el && p.get('ps')) el.value = p.get('ps');
    if (window.PA_READ_ONLY) {
        var hide = document.querySelectorAll('.pa-chk');
        for (var i = 0; i < hide.length; i++) hide[i].style.display = 'none';
    }
    if (window.PA_ASSIGN_MODE) {
        var ids = ['fltStatus', 'btnBatchHr', 'btnBatchReopen', 'btnBatchCancel'];
        for (var j = 0; j < ids.length; j++) { el = document.getElementById(ids[j]); if (el) el.style.display = 'none'; }
    }
})();

// ── Common ───────────────────────────────────────────────────────────
function openModal(id) { document.getElementById(id).classList.add('is-open'); }
function closeModal(id) { if (!_busy) document.getElementById(id).classList.remove('is-open'); }
function toast(msg, ok) {
    var t = document.getElementById('paToast');
    t.textContent = msg || '';
    t.className = 'hr-toast is-on' + (ok === false ? ' hr-toast--err' : '');
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, 4000);
}
function escHtml(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
function adminAjax(action, data, callback) {
    var xhr = new XMLHttpRequest();
    xhr.open('POST', 'AppraisalView.aspx?ajax=' + action, true);
    xhr.setRequestHeader('Content-Type', 'application/json');
    var m = document.querySelector('meta[name="csrf-token"]');
    if (m) xhr.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
    xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) return;
        var res;
        try { res = JSON.parse(xhr.responseText); }
        catch (e) { res = { ok: false, error: 'The request could not be completed. Refresh the page and sign in again if asked.' }; }
        callback(res);
    };
    xhr.send(JSON.stringify(data || {}));
}
var _busy = false;
function done(res, fallback) {
    _busy = false;
    if (res.ok) {
        document.querySelectorAll('.hr-modal.is-open').forEach(function (m) { m.classList.remove('is-open'); });
        toast(res.message || fallback, true);
        setTimeout(function () { location.reload(); }, 1200);
    } else {
        toast(res.error || res.message || 'The change could not be saved.', false);
    }
}

// ── Selection and bulk bar ───────────────────────────────────────────
var _sel = {};
function rowOf(el) { while (el && el.tagName !== 'TR') el = el.parentNode; return el; }
function rowData(tr) {
    if (!tr) return {};
    return {
        status: (tr.getAttribute('data-status') || '').toUpperCase(),
        empName: tr.getAttribute('data-empname') || '',
        dept: tr.getAttribute('data-dept') || '',
        session: tr.getAttribute('data-session') || '',
        pct: tr.getAttribute('data-pct') || '',
        cls: tr.getAttribute('data-cls') || '',
        ack: tr.getAttribute('data-ack') || '',
        reviewer: tr.getAttribute('data-reviewer') || ''
    };
}
function onSelectAll(cb) {
    var checks = document.querySelectorAll('.pa-row-chk');
    for (var i = 0; i < checks.length; i++) {
        checks[i].checked = cb.checked;
        var rid = parseInt(checks[i].value, 10);
        if (cb.checked) _sel[rid] = rowData(rowOf(checks[i])); else delete _sel[rid];
    }
    updateBatchBar();
}
function onRowCheck(cb) {
    var rid = parseInt(cb.value, 10);
    if (cb.checked) _sel[rid] = rowData(rowOf(cb)); else delete _sel[rid];
    var all = document.querySelectorAll('.pa-row-chk').length;
    var on = document.querySelectorAll('.pa-row-chk:checked').length;
    var ca = document.getElementById('chkAll');
    if (ca) { ca.indeterminate = on > 0 && on < all; ca.checked = all > 0 && on === all; }
    updateBatchBar();
}
function clearSelection() {
    _sel = {};
    var checks = document.querySelectorAll('.pa-row-chk');
    for (var i = 0; i < checks.length; i++) checks[i].checked = false;
    var ca = document.getElementById('chkAll'); if (ca) { ca.checked = false; ca.indeterminate = false; }
    updateBatchBar();
}
function updateBatchBar() {
    var n = Object.keys(_sel).length;
    var bar = document.getElementById('batchBar');
    if (!bar) return;
    bar.classList.toggle('is-on', n > 0);
    document.getElementById('batchCount').textContent = n + ' selected';
    var canHr = false;
    for (var r in _sel) {
        var s = _sel[r].status;
        if (s === 'COMPLETED' || s === 'HR_REVIEWED') canHr = true;
    }
    document.getElementById('btnBatchHr').disabled = !canHr;
    document.getElementById('btnBatchReopen').disabled = !canHr;
    document.getElementById('btnBatchCancel').disabled = !canHr;
    document.getElementById('btnBatchAssign').disabled = n === 0;
}
function eligible(statuses) {
    var out = [];
    for (var r in _sel) if (statuses.indexOf(_sel[r].status) >= 0) out.push(parseInt(r, 10));
    return out;
}
function nameList(rids) {
    var h = '';
    for (var i = 0; i < Math.min(rids.length, 12); i++) {
        var d = _sel[rids[i]];
        if (d) h += '<div>' + escHtml(d.empName) + (d.dept ? ', ' + escHtml(d.dept) : '') + '</div>';
    }
    if (rids.length > 12) h += '<div class="hr-muted">and ' + (rids.length - 12) + ' more</div>';
    return h;
}

// ── Return, cancel, reopen (one dialog) ──────────────────────────────
var _act = null;
function openAction(cfg) {
    _act = cfg;
    document.getElementById('actTitle').textContent = cfg.title;
    document.getElementById('actText').textContent = cfg.text;
    var list = document.getElementById('actList');
    list.style.display = cfg.list ? '' : 'none';
    list.innerHTML = cfg.list || '';
    document.getElementById('actCommentWrap').style.display = cfg.comment ? '' : 'none';
    document.getElementById('actCommentLabel').textContent = cfg.commentLabel || 'Reason';
    document.getElementById('actComment').value = '';
    var ok = document.getElementById('actOk');
    ok.textContent = cfg.okLabel;
    ok.className = 'hr-btn ' + (cfg.danger ? 'hr-btn--danger' : 'hr-btn--primary');
    ok.disabled = false;
    openModal('actModal');
}
function runAction() {
    if (_busy || !_act) return;
    var comment = document.getElementById('actComment').value.trim();
    if (_act.comment === 'required' && !comment) { toast('Enter a reason.', false); return; }
    _busy = true;
    document.getElementById('actOk').disabled = true;
    var payload = _act.payload; payload.comment = comment;
    adminAjax(_act.action, payload, function (res) {
        document.getElementById('actOk').disabled = false;
        done(res, 'Saved.');
    });
}
function adminReturnToEmployee(rid) {
    openAction({ title: 'Return to employee', text: 'The appraisal goes back to the employee for changes.', comment: 'required',
        commentLabel: 'Reason', okLabel: 'Return to employee', action: 'admin_return', payload: { rid: rid } });
}
function adminCancel(rid) {
    openAction({ title: 'Cancel appraisal', text: 'The appraisal is closed and can no longer be edited.', comment: 'optional',
        commentLabel: 'Reason (optional)', okLabel: 'Cancel appraisal', danger: true, action: 'admin_cancel', payload: { rid: rid } });
}
function adminReopen(rid) {
    openAction({ title: 'Reopen for supervisor', text: 'The appraisal returns to the supervisor. The HR review, the supervisor sign-off and the employee acknowledgement are cleared.',
        comment: 'optional', commentLabel: 'Reason (optional)', okLabel: 'Reopen', action: 'admin_reopen', payload: { rid: rid } });
}

function batchReopen() {
    var rids = eligible(['COMPLETED', 'HR_REVIEWED']);
    if (!rids.length) { toast('None of the selected appraisals can be reopened.', false); return; }
    openAction({ title: 'Reopen for supervisor', text: rids.length + ' appraisal(s) return to the supervisor. HR review, supervisor sign-off and acknowledgement are cleared.',
        list: nameList(rids), comment: 'optional', commentLabel: 'Reason (optional)', okLabel: 'Reopen', action: 'batch_reopen', payload: { rids: rids } });
}
function batchCancel() {
    var rids = eligible(['COMPLETED', 'HR_REVIEWED']);
    if (!rids.length) { toast('None of the selected appraisals can be cancelled.', false); return; }
    openAction({ title: 'Cancel appraisals', text: rids.length + ' appraisal(s) will be closed and can no longer be edited.', list: nameList(rids),
        comment: 'optional', commentLabel: 'Reason (optional)', okLabel: 'Cancel appraisals', danger: true, action: 'batch_cancel', payload: { rids: rids } });
}

// ── HR review ────────────────────────────────────────────────────────
var _hrRids = [], _hrSingle = 0;
function setRadio(name, v) {
    var r = document.querySelectorAll('#hrModal input[name="' + name + '"]');
    for (var i = 0; i < r.length; i++) r[i].checked = (v !== undefined && v !== null && String(r[i].value) === String(v));
}
function openHr(single, d) {
    _hrSingle = single;
    document.getElementById('hrSingle').style.display = single ? '' : 'none';
    document.getElementById('hrBatch').style.display = single ? 'none' : '';
    document.getElementById('hrTitle').textContent = single ? 'HR review' : 'HR review for selected appraisals';
    setRadio('hr_rating', d && d.rating); setRadio('hr_recommendation', d && d.rec);
    document.getElementById('hrComments').value = (d && d.comments) || '';
    document.getElementById('hrOk').disabled = false;
    openModal('hrModal');
}
function fillHrSingle(d) {
    document.getElementById('hrEmp').textContent = d.empName || '';
    document.getElementById('hrMeta').textContent = d.session || '';
    document.getElementById('hrPct').textContent = d.pct || 'Not scored';
    document.getElementById('hrCls').textContent = d.cls || 'Not scored';
    document.getElementById('hrAck').textContent = d.ack || 'Not recorded';
}
function openHrWizardFromRowBtn(btn) {
    var tr = rowOf(btn);
    _hrRids = [parseInt(tr.getAttribute('data-rid'), 10)];
    fillHrSingle(rowData(tr));
    openHr(true, null);
}
function openHrWizardFromData() {
    var w = window.PA_DETAIL || {};
    if (!w.rid) return;
    _hrRids = [w.rid];
    fillHrSingle(w);
    openHr(true, { rating: w.rating, rec: w.rec, comments: w.comments });
}
function batchHrReview() {
    var rids = eligible(['COMPLETED', 'HR_REVIEWED']);
    if (!rids.length) { toast('None of the selected appraisals is awaiting HR.', false); return; }
    _hrRids = rids;
    document.getElementById('hrBatchCount').textContent = rids.length + ' appraisal(s). The same rating, recommendation and comments are saved on each.';
    document.getElementById('hrBatchList').innerHTML = nameList(rids);
    openHr(false, null);
}
function hrSubmit() {
    if (_busy) return;
    var rat = document.querySelector('#hrModal input[name="hr_rating"]:checked');
    var rec = document.querySelector('#hrModal input[name="hr_recommendation"]:checked');
    if (!rat) { toast('Choose an HR rating.', false); return; }
    if (!rec) { toast('Choose an HR recommendation.', false); return; }
    _busy = true;
    document.getElementById('hrOk').disabled = true;
    var comments = document.getElementById('hrComments').value.trim();
    var payload = _hrSingle
        ? { rid: _hrRids[0], rating: parseInt(rat.value, 10), recommendation: rec.value, comments: comments }
        : { rids: _hrRids, rating: parseInt(rat.value, 10), recommendation: rec.value, comments: comments };
    adminAjax(_hrSingle ? 'hr_input' : 'batch_hr_input', payload, function (res) {
        document.getElementById('hrOk').disabled = false;
        done(res, 'HR review saved.');
    });
}

// ── Supervisor ───────────────────────────────────────────────────────
var _supRid = 0, _supRids = [], _supAll = [];
function changeSupervisorFromDetailBtn(btn) {
    _supRid = parseInt(btn.getAttribute('data-rid') || '0', 10);
    _supRids = [];
    openSup(btn.getAttribute('data-reviewer') || 'Not assigned');
}
function batchAssignReviewer() {
    var rids = [];
    for (var r in _sel) rids.push(parseInt(r, 10));
    if (!rids.length) return;
    _supRid = 0; _supRids = rids;
    openSup(rids.length + ' selected appraisal(s)');
}
function openSup(current) {
    var bulk = _supRids.length > 0;
    document.getElementById('supTitle').textContent = bulk ? 'Assign supervisor' : 'Change supervisor';
    document.getElementById('supCurrentPrefix').textContent = bulk ? 'Applies to' : 'Current supervisor';
    document.getElementById('supCurrentLabel').textContent = current;
    document.getElementById('supSearchInput').value = '';
    document.getElementById('supSelect').innerHTML = '<option disabled>Loading</option>';
    document.getElementById('supMatchCount').textContent = '';
    document.getElementById('btnSupConfirm').disabled = false;
    openModal('supModal');
    adminAjax('get_employees', {}, function (res) {
        if (res.ok === false) { toast(res.error || 'The staff list could not be loaded.', false); return; }
        _supAll = res.employees || [];
        renderSup(_supAll);
        document.getElementById('supSearchInput').focus();
    });
}
function renderSup(list) {
    var sel = document.getElementById('supSelect'), h = '';
    for (var i = 0; i < list.length; i++)
        h += '<option value="' + list[i].id + '">' + escHtml(list[i].name) + (list[i].code ? ' (' + escHtml(list[i].code) + ')' : '') + '</option>';
    sel.innerHTML = h || '<option disabled>No staff found</option>';
    document.getElementById('supMatchCount').textContent = list.length + ' shown';
}
function filterSupOptions() {
    var q = (document.getElementById('supSearchInput').value || '').toLowerCase().trim();
    if (!q) { renderSup(_supAll); return; }
    var out = [];
    for (var i = 0; i < _supAll.length; i++) {
        var e = _supAll[i];
        if ((e.name || '').toLowerCase().indexOf(q) >= 0 || (e.code || '').toLowerCase().indexOf(q) >= 0) out.push(e);
    }
    renderSup(out);
}
function confirmChangeSupervisor() {
    if (_busy) return;
    var id = parseInt(document.getElementById('supSelect').value || '0', 10);
    if (!id) { toast('Choose a supervisor from the list.', false); return; }
    _busy = true;
    document.getElementById('btnSupConfirm').disabled = true;
    var bulk = _supRids.length > 0;
    adminAjax(bulk ? 'batch_change_supervisor' : 'admin_change_supervisor',
        bulk ? { rids: _supRids, new_reviewer_id: id } : { rid: _supRid, new_reviewer_id: id },
        function (res) { document.getElementById('btnSupConfirm').disabled = false; done(res, 'Supervisor saved.'); });
}
</script>
</asp:Content>
