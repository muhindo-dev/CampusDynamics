<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AppraisalSessions.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalSessions" Title="Appraisal sessions" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<meta name="csrf-token" content="<%= MarksAntiForgeryService.GetToken() %>" />
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.ps-checks { display: flex; flex-wrap: wrap; gap: 6px 16px; padding: 4px 0; }
.ps-checks label { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; cursor: pointer; }
.ps-checks input { accent-color: var(--hr-navy); margin: 0; }
.ps-progress { min-width: 140px; }
.ps-progress .hr-bar { margin-top: 4px; }
.ps-sec { font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: .4px; color: var(--hr-muted); margin: 16px 0 8px; }
.ps-log { max-height: 220px; overflow: auto; border: 1px solid var(--hr-border); font-size: 12px; }
.ps-log div { padding: 5px 10px; border-bottom: 1px solid var(--hr-border); }
.ps-log div:last-child { border-bottom: none; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon"><svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg></div>
        <div>
            <div class="hr-header__title">Appraisal sessions</div>
            <div class="hr-header__sub">Create, open and close appraisal periods</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openCreateModal()">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            New session
        </button>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="AppraisalDashboard.aspx">Overview</a><a class="hr-tab hr-tab--active" href="AppraisalSessions.aspx">Sessions</a><a class="hr-tab" href="AppraisalView.aspx">Appraisals</a><a class="hr-tab" href="AppraisalReports.aspx">Reports</a><a class="hr-tab" href="CompetencyTemplates.aspx">Competencies</a><a class="hr-tab" href="ExpectedStandards.aspx">Expected standards</a></div>

<div class="hr-kpis">
    <div class="hr-kpi"><div class="hr-kpi__label">Sessions</div><div class="hr-kpi__value"><asp:Literal ID="litStatTotal" runat="server" Text="0" /></div></div>
    <a class="hr-kpi" href="AppraisalSessions.aspx?status=ACTIVE"><div class="hr-kpi__label">Active</div><div class="hr-kpi__value"><asp:Literal ID="litStatActive" runat="server" Text="0" /></div></a>
    <a class="hr-kpi" href="AppraisalSessions.aspx?status=DRAFT"><div class="hr-kpi__label">Draft</div><div class="hr-kpi__value"><asp:Literal ID="litStatDraft" runat="server" Text="0" /></div></a>
    <div class="hr-kpi"><div class="hr-kpi__label">Appraisals</div><div class="hr-kpi__value"><asp:Literal ID="litStatAppraisals" runat="server" Text="0" /></div><div class="hr-kpi__sub">All sessions</div></div>
    <div class="hr-kpi"><div class="hr-kpi__label">Completed by supervisor</div><div class="hr-kpi__value"><asp:Literal ID="litStatCompleted" runat="server" Text="0" /></div><div class="hr-kpi__sub">Awaiting HR or HR reviewed</div></div>
</div>

<div class="hr-filters">
    <div class="hr-filter hr-filter--grow">
        <label for="txtSearch">Search</label>
        <input type="text" id="txtSearch" class="hr-input" placeholder="Session title" value="<%= HttpUtility.HtmlAttributeEncode(Request.QueryString["q"] ?? "") %>" />
    </div>
    <div class="hr-filter">
        <label for="ddlStatusFilter">Status</label>
        <select id="ddlStatusFilter" class="hr-select" onchange="applyFilters()">
            <option value="">All statuses</option>
            <option value="DRAFT">Draft</option>
            <option value="ACTIVE">Active</option>
            <option value="CLOSED">Closed</option>
            <option value="ARCHIVED">Archived</option>
        </select>
    </div>
    <div class="hr-filters__actions">
        <a class="hr-btn hr-btn--secondary" href="AppraisalSessions.aspx">Clear</a>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head"><div class="hr-card__title">Sessions</div></div>
    <div class="hr-table-wrap">
        <table class="hr-table">
            <thead>
                <tr>
                    <th>Session</th>
                    <th>Appraisal period</th>
                    <th>Deadline</th>
                    <th>Staff categories</th>
                    <th>Status</th>
                    <th>Completed by supervisor</th>
                    <th></th>
                </tr>
            </thead>
            <tbody><asp:Literal ID="litGridBody" runat="server" /></tbody>
        </table>
    </div>
    <div class="hr-card__foot">
        <span><asp:Literal ID="litPagerInfo" runat="server" /></span>
        <div class="hr-pager"><asp:Literal ID="litPager" runat="server" /></div>
    </div>
</div>

<!-- Create session -->
<div id="createModal" class="hr-modal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>New session</span><button type="button" class="hr-modal__close" onclick="closeModal('createModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field hr-full">
                    <label class="hr-label">Title <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtTitle" runat="server" CssClass="hr-input" placeholder="Performance Appraisal 2026/2027, Second Quarter" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label">Description</label>
                    <asp:TextBox ID="txtDescription" runat="server" CssClass="hr-textarea" TextMode="MultiLine" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Period start <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtPeriodStart" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Period end <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtPeriodEnd" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Deadline <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtDeadline" runat="server" CssClass="hr-input" TextMode="Date" />
                    <span class="hr-hint">Within the appraisal period.</span>
                </div>
                <div class="hr-field">
                    <span class="hr-label">Staff categories <span class="hr-req">*</span></span>
                    <div class="ps-checks">
                        <label><asp:CheckBox ID="chkAcademic" runat="server" Checked="true" /> Academic</label>
                        <label><asp:CheckBox ID="chkAdministrative" runat="server" Checked="true" /> Administrative</label>
                        <label><asp:CheckBox ID="chkSupport" runat="server" Checked="true" /> Support</label>
                    </div>
                </div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('createModal')">Cancel</button>
            <asp:Button ID="btnCreateSession" runat="server" CssClass="hr-btn hr-btn--primary" Text="Create session" OnClick="btnCreateSession_Click" />
        </div>
    </div>
</div>

<!-- Edit session -->
<div id="editModal" class="hr-modal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Edit session</span><button type="button" class="hr-modal__close" onclick="closeModal('editModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <asp:HiddenField ID="hfEditSessionId" runat="server" />
            <div class="hr-form">
                <div class="hr-field hr-full">
                    <label class="hr-label">Title <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtEditTitle" runat="server" CssClass="hr-input" />
                </div>
                <div class="hr-field hr-full">
                    <label class="hr-label">Description</label>
                    <asp:TextBox ID="txtEditDescription" runat="server" CssClass="hr-textarea" TextMode="MultiLine" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Period start <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtEditPeriodStart" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Period end <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtEditPeriodEnd" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Deadline <span class="hr-req">*</span></label>
                    <asp:TextBox ID="txtEditDeadline" runat="server" CssClass="hr-input" TextMode="Date" />
                </div>
                <div class="hr-field">
                    <label class="hr-label">Status <span class="hr-req">*</span></label>
                    <asp:DropDownList ID="ddlEditStatus" runat="server" CssClass="hr-select">
                        <asp:ListItem Value="DRAFT" Text="Draft" />
                        <asp:ListItem Value="ACTIVE" Text="Active" />
                        <asp:ListItem Value="CLOSED" Text="Closed" />
                        <asp:ListItem Value="ARCHIVED" Text="Archived" />
                    </asp:DropDownList>
                    <span class="hr-hint">Setting a session to Active creates the missing appraisals.</span>
                </div>
                <div class="hr-field hr-full">
                    <span class="hr-label">Staff categories <span class="hr-req">*</span></span>
                    <div class="ps-checks">
                        <label><asp:CheckBox ID="chkEditAcademic" runat="server" /> Academic</label>
                        <label><asp:CheckBox ID="chkEditAdministrative" runat="server" /> Administrative</label>
                        <label><asp:CheckBox ID="chkEditSupport" runat="server" /> Support</label>
                    </div>
                </div>
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('editModal')">Cancel</button>
            <asp:Button ID="btnEditSession" runat="server" CssClass="hr-btn hr-btn--primary" Text="Save changes" OnClick="btnEditSession_Click" />
        </div>
    </div>
</div>

<!-- Session detail -->
<div id="detailModal" class="hr-modal">
    <div class="hr-modal__box hr-modal__box--wide">
        <div class="hr-modal__head"><span id="detailTitle">Session</span><button type="button" class="hr-modal__close" onclick="closeDetailModal()"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div id="detailBody"><div class="hr-loading">Loading</div></div>
            <div class="ps-sec">Actions</div>
            <div class="hr-row" id="detailActions">
                <a class="hr-btn hr-btn--primary" id="lnkAppraisals" href="AppraisalView.aspx">Open appraisals</a>
                <button type="button" class="hr-btn hr-btn--secondary" onclick="openEditModal(_currentDetailId)">Edit</button>
                <button type="button" class="hr-btn hr-btn--secondary" id="btnActivate" style="display:none" onclick="confirmPostback('activate')">Activate</button>
                <button type="button" class="hr-btn hr-btn--secondary" id="btnGenerate" style="display:none" onclick="generateAppraisals()">Create missing appraisals</button>
                <button type="button" class="hr-btn hr-btn--secondary" id="btnSendNotifications" style="display:none" onclick="sendNotifications(false)">Send notifications</button>
                <button type="button" class="hr-btn hr-btn--secondary" id="btnClose" style="display:none" onclick="confirmPostback('close')">Close session</button>
                <button type="button" class="hr-btn hr-btn--secondary" id="btnArchive" style="display:none" onclick="confirmPostback('archive')">Archive</button>
                <a class="hr-btn hr-btn--secondary" id="lnkReport" href="#" target="_blank" rel="noopener" style="display:none">Session report</a>
                <span class="hr-spacer"></span>
                <button type="button" class="hr-btn hr-btn--danger" onclick="openDeleteSession()">Delete session</button>
            </div>
            <asp:HiddenField ID="hfActionSessionId" runat="server" />
            <div style="display:none;">
                <asp:Button ID="btnActivateSession" runat="server" Text="Activate" OnClick="btnActivateSession_Click" />
                <asp:Button ID="btnCloseSession" runat="server" Text="Close" OnClick="btnCloseSession_Click" />
                <asp:Button ID="btnArchiveSession" runat="server" Text="Archive" OnClick="btnArchiveSession_Click" />
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeDetailModal()">Close</button>
        </div>
    </div>
</div>

<!-- Confirm -->
<div id="confirmModal" class="hr-modal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="confirmTitle">Confirm</span><button type="button" class="hr-modal__close" onclick="closeModal('confirmModal')"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <p id="confirmText" style="margin:0;"></p>
            <div class="hr-field" id="confirmTypeWrap" style="display:none;margin-top:12px;">
                <label class="hr-label" for="confirmType" id="confirmTypeLabel"></label>
                <input type="text" id="confirmType" class="hr-input" autocomplete="off" oninput="checkTyped()" />
            </div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('confirmModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="confirmOk" onclick="confirmRun()">Confirm</button>
        </div>
    </div>
</div>

<!-- Notification progress -->
<div id="progModal" class="hr-modal">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span>Appraisal notifications</span><button type="button" class="hr-modal__close" onclick="closeProgModal()"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <p id="progSummaryText" style="margin:0 0 10px;">Starting</p>
            <span class="hr-bar"><span id="progBarFill" style="width:0%"></span></span>
            <dl class="hr-dl" style="grid-template-columns:repeat(4,minmax(0,1fr));margin:12px 0;">
                <div><dt>Sent</dt><dd id="chipSent">0</dd></div>
                <div><dt>Failed</dt><dd id="chipFailed">0</dd></div>
                <div><dt>No email address</dt><dd id="chipNoEmail">0</dd></div>
                <div><dt>Remaining</dt><dd id="chipPending">0</dd></div>
            </dl>
            <div class="ps-log" id="progLog"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" id="progResendBtn" class="hr-btn hr-btn--secondary" onclick="sendNotifications(true)" style="display:none">Send to everyone again</button>
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeProgModal()">Close</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="psToast"></div>

</div>

<script type="text/javascript">
// ── Common ───────────────────────────────────────────────────────────
function openModal(id) { document.getElementById(id).classList.add('is-open'); }
function closeModal(id) { document.getElementById(id).classList.remove('is-open'); }
function toast(msg, ok) {
    var t = document.getElementById('psToast');
    t.textContent = msg || '';
    t.className = 'hr-toast is-on' + (ok === false ? ' hr-toast--err' : '');
    clearTimeout(t._h);
    t._h = setTimeout(function () { t.classList.remove('is-on'); }, 4500);
}
function esc(v) {
    if (v === null || v === undefined) return '';
    var d = document.createElement('div'); d.appendChild(document.createTextNode(String(v))); return d.innerHTML;
}
function csrf(xhr) {
    var m = document.querySelector('meta[name="csrf-token"]');
    if (m) xhr.setRequestHeader('X-CSRF-Token', m.getAttribute('content'));
}
function call(method, url, cb) {
    var xhr = new XMLHttpRequest();
    xhr.open(method, url, true);
    if (method === 'POST') csrf(xhr);
    xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) return;
        var d;
        try { d = JSON.parse(xhr.responseText); }
        catch (e) { d = { error: 'The request could not be completed. Refresh the page and sign in again if asked.' }; }
        if (d && d.success === false && d.message && !d.error) d.error = d.message;
        cb(d);
    };
    xhr.send(method === 'POST' ? '' : null);
}

// ── Filters ──────────────────────────────────────────────────────────
(function () {
    var p = new URLSearchParams(window.location.search);
    if (p.get('status')) document.getElementById('ddlStatusFilter').value = p.get('status');
    var box = document.getElementById('txtSearch'), timer = null;
    box.addEventListener('keyup', function () { clearTimeout(timer); timer = setTimeout(applyFilters, 400); });
    box.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); applyFilters(); } });
})();
function applyFilters() {
    var q = document.getElementById('txtSearch').value.trim();
    var st = document.getElementById('ddlStatusFilter').value;
    var parts = [];
    if (q) parts.push('q=' + encodeURIComponent(q));
    if (st) parts.push('status=' + encodeURIComponent(st));
    window.location.href = 'AppraisalSessions.aspx' + (parts.length ? '?' + parts.join('&') : '');
}
function goPage(p) {
    var params = new URLSearchParams(window.location.search);
    params.set('page', p); params.delete('msg'); params.delete('ok');
    window.location.href = 'AppraisalSessions.aspx?' + params.toString();
}

// ── Create and edit ──────────────────────────────────────────────────
function openCreateModal() { openModal('createModal'); }
function openEditModal(sessionId) {
    call('GET', 'AppraisalSessions.aspx?ajax=get_session&id=' + sessionId, function (d) {
        if (d.error) { toast(d.error, false); return; }
        document.getElementById('<%= hfEditSessionId.ClientID %>').value = d.session_id;
        document.getElementById('<%= txtEditTitle.ClientID %>').value = d.session_title;
        document.getElementById('<%= txtEditDescription.ClientID %>').value = d.session_description || '';
        document.getElementById('<%= txtEditPeriodStart.ClientID %>').value = d.period_start;
        document.getElementById('<%= txtEditPeriodEnd.ClientID %>').value = d.period_end;
        document.getElementById('<%= txtEditDeadline.ClientID %>').value = d.deadline;
        var cats = (d.target_categories || '').toUpperCase();
        document.getElementById('<%= chkEditAcademic.ClientID %>').checked = cats.indexOf('ACADEMIC') >= 0;
        document.getElementById('<%= chkEditAdministrative.ClientID %>').checked = cats.indexOf('ADMINISTRATIVE') >= 0;
        document.getElementById('<%= chkEditSupport.ClientID %>').checked = cats.indexOf('SUPPORT') >= 0;
        document.getElementById('<%= ddlEditStatus.ClientID %>').value = (d.status || 'DRAFT').toUpperCase();
        closeModal('detailModal');
        openModal('editModal');
    });
}

// ── Detail ───────────────────────────────────────────────────────────
var _currentDetailId = 0, _detail = null;
function viewSession(sessionId) {
    _currentDetailId = sessionId;
    document.getElementById('<%= hfActionSessionId.ClientID %>').value = sessionId;
    document.getElementById('detailBody').innerHTML = '<div class="hr-loading">Loading</div>';
    ['btnActivate', 'btnGenerate', 'btnSendNotifications', 'btnClose', 'btnArchive', 'lnkReport'].forEach(function (id) {
        document.getElementById(id).style.display = 'none';
    });
    openModal('detailModal');
    call('GET', 'AppraisalSessions.aspx?ajax=get_session_detail&id=' + sessionId, function (d) {
        if (d.error) { document.getElementById('detailBody').innerHTML = '<div class="hr-notice hr-notice--bad">' + esc(d.error) + '</div>'; return; }
        renderDetail(d);
    });
}
function dl(label, value) { return '<div><dt>' + label + '</dt><dd>' + value + '</dd></div>'; }
function renderDetail(d) {
    _detail = d;
    document.getElementById('detailTitle').textContent = d.session_title;
    var h = '<dl class="hr-dl">';
    h += dl('Status', d.status_html);
    h += dl('Appraisal period', esc(d.period_text));
    h += dl('Deadline', esc(d.deadline_text));
    h += dl('Staff categories', esc(d.categories_text));
    h += dl('Created', esc(d.created_text));
    h += dl('Completed by supervisor', esc(d.completed) + ' of ' + esc(d.total));
    h += '</dl>';
    h += '<div class="ps-sec">Appraisals by status</div><dl class="hr-dl" style="grid-template-columns:repeat(4,minmax(0,1fr));">';
    for (var i = 0; i < d.statuses.length; i++) h += dl(esc(d.statuses[i].label), esc(d.statuses[i].count));
    h += '</dl>';
    h += '<div class="ps-sec">Notifications</div><dl class="hr-dl" style="grid-template-columns:repeat(4,minmax(0,1fr));">';
    h += dl('Sent', d.n_sent) + dl('Failed', d.n_failed) + dl('No email address', d.n_noemail) + dl('Not sent', d.n_pending);
    h += '</dl>';
    if (d.undelivered && d.undelivered.length) {
        h += '<div class="hr-table-wrap" style="margin-top:8px;max-height:200px;overflow:auto;border:1px solid var(--hr-border);"><table class="hr-table"><thead><tr><th>Employee</th><th>Notification</th><th></th></tr></thead><tbody>';
        for (var j = 0; j < d.undelivered.length; j++) {
            var u = d.undelivered[j];
            h += '<tr><td>' + esc(u.emp_name) + '</td><td>' + esc(u.state) + '</td><td class="hr-right">' +
                 (u.can_send ? '<button type="button" class="hr-btn hr-btn--secondary hr-btn--sm" onclick="resendSingle(' + u.record_id + ')">Send again</button>' : '') +
                 '</td></tr>';
        }
        h += '</tbody></table></div>';
    }
    document.getElementById('detailBody').innerHTML = h;

    document.getElementById('lnkAppraisals').href = 'AppraisalView.aspx?sid=' + d.session_id;
    document.getElementById('lnkAppraisals').textContent = 'Open appraisals at HR (' + d.completed + ')';
    var st = (d.status || '').toUpperCase();
    if (st === 'DRAFT') document.getElementById('btnActivate').style.display = '';
    if (st === 'ACTIVE') {
        document.getElementById('btnGenerate').style.display = '';
        document.getElementById('btnSendNotifications').style.display = '';
        document.getElementById('btnClose').style.display = '';
    }
    if (st === 'CLOSED') document.getElementById('btnArchive').style.display = '';
    if (d.total > 0) {
        var r = document.getElementById('lnkReport');
        r.href = 'AppraisalSessionReport.aspx?sid=' + d.session_id;
        r.style.display = '';
    }
}
function closeDetailModal() {
    closeModal('detailModal');
    if (window._needsRefresh) { window._needsRefresh = false; window.location.reload(); }
}

// ── Confirm dialog ───────────────────────────────────────────────────
var _confirm = null;
function askConfirm(cfg) {
    _confirm = cfg;
    document.getElementById('confirmTitle').textContent = cfg.title;
    document.getElementById('confirmText').textContent = cfg.text;
    var wrap = document.getElementById('confirmTypeWrap'), inp = document.getElementById('confirmType');
    wrap.style.display = cfg.typeLabel ? '' : 'none';
    document.getElementById('confirmTypeLabel').textContent = cfg.typeLabel || '';
    inp.value = '';
    var ok = document.getElementById('confirmOk');
    ok.textContent = cfg.okLabel;
    ok.className = 'hr-btn ' + (cfg.danger ? 'hr-btn--danger' : 'hr-btn--primary');
    ok.disabled = !!cfg.mustType;
    openModal('confirmModal');
    if (cfg.typeLabel) inp.focus();
}
function checkTyped() {
    if (!_confirm || !_confirm.mustType) return;
    var v = document.getElementById('confirmType').value.trim();
    var ok = false;
    for (var i = 0; i < _confirm.mustType.length; i++) if (v.toLowerCase() === _confirm.mustType[i].toLowerCase()) ok = true;
    document.getElementById('confirmOk').disabled = !ok;
}
function confirmRun() {
    if (!_confirm || document.getElementById('confirmOk').disabled) return;
    var cb = _confirm.onOk;
    closeModal('confirmModal');
    cb();
}
function confirmPostback(kind) {
    var cfg = {
        activate: { title: 'Activate session', text: 'The session opens to staff and the missing appraisals are created.', ok: 'Activate', btn: '<%= btnActivateSession.ClientID %>' },
        close:    { title: 'Close session', text: 'Staff can no longer start or submit appraisals in this session.', ok: 'Close session', btn: '<%= btnCloseSession.ClientID %>' },
        archive:  { title: 'Archive session', text: 'The session moves to the archive. Its appraisals stay available.', ok: 'Archive', btn: '<%= btnArchiveSession.ClientID %>' }
    }[kind];
    askConfirm({ title: cfg.title, text: cfg.text, okLabel: cfg.ok, onOk: function () { document.getElementById(cfg.btn).click(); } });
}

// ── Generate, delete ─────────────────────────────────────────────────
function generateAppraisals() {
    askConfirm({ title: 'Create missing appraisals', text: 'An appraisal is created for every eligible employee who does not have one in this session.', okLabel: 'Create appraisals',
        onOk: function () {
            var btn = document.getElementById('btnGenerate'); btn.disabled = true;
            call('POST', 'AppraisalSessions.aspx?ajax=generate_appraisals&id=' + _currentDetailId, function (d) {
                btn.disabled = false;
                if (d.error) { toast(d.error, false); return; }
                toast(d.count + ' appraisal(s) created.', true);
                window._needsRefresh = true;
                viewSession(_currentDetailId);
            });
        } });
}
function openDeleteSession() {
    if (!_detail) return;
    var n = _detail.total || 0;
    askConfirm({ title: 'Delete session',
        text: 'This permanently deletes "' + _detail.session_title + '"' + (n > 0 ? ' and its ' + n + ' appraisal(s) with everything entered on them' : '') + '. It cannot be undone.',
        typeLabel: 'Type the session title or DELETE to confirm', mustType: [_detail.session_title, 'DELETE'],
        okLabel: 'Delete session', danger: true,
        onOk: function () {
            call('POST', 'AppraisalSessions.aspx?ajax=delete_session&id=' + _currentDetailId, function (d) {
                if (d.error) { toast(d.error, false); return; }
                var msg = 'Session deleted' + (d.deleted_records > 0 ? ' with ' + d.deleted_records + ' appraisal(s).' : '.');
                window.location.href = 'AppraisalSessions.aspx?msg=' + encodeURIComponent(msg) + '&ok=1';
            });
        } });
}

// ── Notifications ────────────────────────────────────────────────────
var _notifyPollTimer = null;
function sendNotifications(resend) {
    if (_notifyPollTimer) { clearInterval(_notifyPollTimer); _notifyPollTimer = null; }
    openModal('progModal');
    document.getElementById('progBarFill').style.width = '0%';
    ['chipSent', 'chipFailed', 'chipNoEmail', 'chipPending'].forEach(function (id) { document.getElementById(id).textContent = '0'; });
    document.getElementById('progSummaryText').textContent = 'Starting';
    document.getElementById('progResendBtn').style.display = 'none';
    document.getElementById('progLog').innerHTML = '';
    call('POST', 'AppraisalSessions.aspx?ajax=send_notifications&id=' + _currentDetailId + (resend ? '&resend=1' : ''), function (d) {
        if (d.error) { document.getElementById('progSummaryText').textContent = d.error; return; }
        if (!d.started) {
            document.getElementById('progSummaryText').textContent = d.message || 'There is nothing to send.';
            document.getElementById('progResendBtn').style.display = '';
            return;
        }
        document.getElementById('chipPending').textContent = d.total;
        document.getElementById('progSummaryText').textContent = 'Sending to ' + d.total + ' employee(s).';
        _notifyPollTimer = setInterval(pollNotifyStatus, 2000);
        pollNotifyStatus();
    });
}
function pollNotifyStatus() {
    call('GET', 'AppraisalSessions.aspx?ajax=get_notify_status&id=' + _currentDetailId, function (d) {
        if (d.error) return;
        var processed = d.sent + d.failed + d.no_email, total = d.total || 1;
        document.getElementById('progBarFill').style.width = Math.round(processed / total * 100) + '%';
        document.getElementById('chipSent').textContent = d.sent;
        document.getElementById('chipFailed').textContent = d.failed;
        document.getElementById('chipNoEmail').textContent = d.no_email;
        document.getElementById('chipPending').textContent = d.pending;
        var log = '';
        (d.records || []).forEach(function (r) {
            var ns = (r.notify_status || 'PENDING').toUpperCase();
            if (ns === 'PENDING') return;
            var info = ns === 'SENT' ? 'Sent to ' + (r.emp_email || '') : ns === 'FAILED' ? 'Not delivered' : 'No email address';
            log += '<div>' + esc(r.emp_name) + '<span class="hr-sub">' + esc(info) + '</span></div>';
        });
        document.getElementById('progLog').innerHTML = log;
        if (d.done) {
            clearInterval(_notifyPollTimer); _notifyPollTimer = null;
            document.getElementById('progSummaryText').textContent = 'Finished: ' + d.sent + ' sent, ' + d.failed + ' failed, ' + d.no_email + ' without an email address.';
            document.getElementById('progResendBtn').style.display = '';
            window._needsRefresh = true;
            viewSession(_currentDetailId);
        }
    });
}
function closeProgModal() {
    if (_notifyPollTimer) { clearInterval(_notifyPollTimer); _notifyPollTimer = null; }
    closeModal('progModal');
}
function resendSingle(recordId) {
    call('POST', 'AppraisalSessions.aspx?ajax=send_single_notify&id=' + recordId, function (d) {
        toast(d.message || d.error || 'Done.', !!d.ok);
        viewSession(_currentDetailId);
    });
}
</script>
</asp:Content>
