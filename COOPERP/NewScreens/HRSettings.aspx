<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="HRSettings.aspx.cs" Inherits="COOPERP_NewScreens_HRSettings" Title="Organisation and pay scales - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.hr-subtabs a.hr-subtab { text-decoration: none; display: inline-block; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>
        </div>
        <div>
            <div class="hr-header__title">HR settings</div>
            <div class="hr-header__sub">Departments, positions, pay scales and payroll items</div>
        </div>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab hr-tab--active" href="HRSettings.aspx">Organisation and pay scales</a><a class="hr-tab" href="HRConfig.aspx">Payroll and tax settings</a></div>

<asp:Literal ID="litBody" runat="server" />

</div>

<!-- Add / edit -->
<div class="hr-modal" id="editModal" role="dialog" aria-modal="true" aria-labelledby="editTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="editTitle"></span><button type="button" class="hr-modal__close" onclick="closeModal('editModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field hr-full"><label class="hr-label" for="fName" id="fNameLabel">Name</label>
                    <input type="text" id="fName" class="hr-input" /></div>
                <div class="hr-field hr-full" data-for="departments"><label class="hr-label" for="fHead">Head of department</label>
                    <select id="fHead" class="hr-select"><asp:Literal ID="litHeadOptions" runat="server" /></select></div>
                <div class="hr-field hr-full" data-for="positions"><label class="hr-label" for="fQual">Minimum qualifications</label>
                    <input type="text" id="fQual" class="hr-input" maxlength="45" /></div>
                <div class="hr-field" data-for="items"><label class="hr-label" for="fKind">Kind</label>
                    <select id="fKind" class="hr-select"><option value="ALLOWANCE">Allowance</option><option value="DEDUCTION">Deduction</option></select></div>
                <div class="hr-field" data-for="items"><label class="hr-label" for="fCategory">Category</label>
                    <select id="fCategory" class="hr-select"><option value="OPTIONAL">Optional</option><option value="MANDATORY">Mandatory</option><option value="STATUTORY">Statutory</option></select></div>
                <div class="hr-field" data-for="items"><label class="hr-label" for="fComp">Computation</label>
                    <select id="fComp" class="hr-select"><option value="FIXED">Fixed amount</option><option value="PERCENTAGE">Percentage of basic pay</option></select></div>
                <div class="hr-field" data-for="scales items"><label class="hr-label" for="fAmount" id="fAmountLabel">Amount</label>
                    <input type="text" id="fAmount" class="hr-input" inputmode="decimal" /></div>
            </div>
            <div class="hr-error" id="editError" style="margin-top:10px"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('editModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="editBtn" onclick="save()">Save</button>
        </div>
    </div>
</div>

<!-- Delete -->
<div class="hr-modal" id="delModal" role="dialog" aria-modal="true" aria-labelledby="delTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="delTitle">Delete</span><button type="button" class="hr-modal__close" onclick="closeModal('delModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body"><p id="delText" style="margin:0"></p><div class="hr-error" id="delError" style="margin-top:8px"></div></div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('delModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--danger" id="delBtn">Delete</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="toast" role="status"></div>

<asp:Literal ID="litSection" runat="server" />
<script type="text/javascript">
(function () {
    var PAGE = 'HRSettings.aspx', editId = 0;
    var NAME_LABEL = { departments: 'Department name', positions: 'Position', stations: 'Station name', scales: 'Scale name', banks: 'Bank name', items: 'Name' };
    var NAME_MAX = { departments: 200, positions: 45, stations: 150, scales: 45, banks: 150, items: 45 };
    function $(id) { return document.getElementById(id); }
    function cap(s) { return s.charAt(0).toUpperCase() + s.slice(1); }

    function toast(msg, isErr) {
        var t = $('toast');
        t.textContent = msg;
        t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
        clearTimeout(t._h);
        t._h = setTimeout(function () { t.className = 'hr-toast'; }, 4000);
    }
    function post(action, data, done) {
        var body = new URLSearchParams();
        body.append('section', SECTION);
        for (var k in data) if (data.hasOwnProperty(k)) body.append(k, data[k]);
        fetch(PAGE + '?action=' + action, { method: 'POST', body: body, credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(done)
            .catch(function () { done({ ok: false, message: 'The server did not respond. Please try again.' }); });
    }
    function reloadWith(msg) { try { sessionStorage.setItem('hrSetToast', msg); } catch (e) { } location.reload(); }
    function openModal(id) { $(id).classList.add('is-open'); }
    window.closeModal = function (id) { $(id).classList.remove('is-open'); };

    // fields for this list
    var f = document.querySelectorAll('[data-for]');
    for (var i = 0; i < f.length; i++) f[i].style.display = (' ' + f[i].getAttribute('data-for') + ' ').indexOf(' ' + SECTION + ' ') >= 0 ? '' : 'none';
    $('fNameLabel').innerHTML = NAME_LABEL[SECTION] + ' <span class="hr-req">*</span>';
    $('fName').maxLength = NAME_MAX[SECTION];
    $('fAmountLabel').innerHTML = (SECTION === 'scales' ? 'Basic pay (UGX)' : 'Amount or rate') + ' <span class="hr-req">*</span>';

    function setSelect(id, v) {
        var s = $(id), found = false;
        for (var i = 0; i < s.options.length; i++) if (s.options[i].value === v) { found = true; break; }
        if (!found && v) { var o = document.createElement('option'); o.value = v; o.textContent = cap(v.toLowerCase()); s.appendChild(o); }
        s.value = v;
    }

    window.openEdit = function (btn) {
        var tr = btn ? btn.closest('tr') : null;
        editId = tr ? tr.getAttribute('data-id') : 0;
        $('editTitle').textContent = (tr ? 'Edit ' : 'Add ') + SINGLE;
        function d(k, def) { return tr ? (tr.getAttribute('data-' + k) || '') : def; }
        $('fName').value = d('name', '');
        if (SECTION === 'departments') setSelect('fHead', d('head', ''));
        if (SECTION === 'positions') $('fQual').value = d('qual', '');
        if (SECTION === 'scales' || SECTION === 'items') $('fAmount').value = d('amount', '');
        if (SECTION === 'items') {
            setSelect('fKind', d('kind', 'ALLOWANCE'));
            setSelect('fCategory', d('category', 'OPTIONAL'));
            setSelect('fComp', d('computation', 'FIXED'));
        }
        $('editError').textContent = '';
        $('editBtn').disabled = false;
        openModal('editModal');
        $('fName').focus();
    };
    window.save = function () {
        var data = { id: editId || 0, name: $('fName').value, head: $('fHead').value, qual: $('fQual').value, amount: $('fAmount').value,
                     kind: $('fKind').value, category: $('fCategory').value, computation: $('fComp').value };
        $('editBtn').disabled = true;
        post('save', data, function (res) {
            if (res.ok) reloadWith(res.message);
            else { $('editError').textContent = res.message; $('editBtn').disabled = false; }
        });
    };
    window.del = function (btn) {
        var tr = btn.closest('tr'), id = tr.getAttribute('data-id');
        $('delTitle').textContent = 'Delete ' + SINGLE;
        $('delText').textContent = 'Delete "' + tr.getAttribute('data-name') + '". This cannot be undone.';
        $('delError').textContent = '';
        var b = $('delBtn');
        b.disabled = false;
        b.onclick = function () {
            b.disabled = true;
            post('delete', { id: id }, function (res) {
                if (res.ok) reloadWith(res.message);
                else { $('delError').textContent = res.message; }
            });
        };
        openModal('delModal');
    };
    window.filterRows = function (q) {
        q = q.toLowerCase().trim();
        var rows = document.querySelectorAll('#setTable tbody tr');
        for (var i = 0; i < rows.length; i++) rows[i].style.display = !q || rows[i].textContent.toLowerCase().indexOf(q) >= 0 ? '' : 'none';
    };

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { closeModal('editModal'); closeModal('delModal'); }
        if (e.key === 'Enter' && e.target && e.target.tagName === 'INPUT') e.preventDefault();
    });
    try {
        var msg = sessionStorage.getItem('hrSetToast');
        if (msg) { sessionStorage.removeItem('hrSetToast'); toast(msg, false); }
    } catch (e) { }
})();
</script>
</asp:Content>
