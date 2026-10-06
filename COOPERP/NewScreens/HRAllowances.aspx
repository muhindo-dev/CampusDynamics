<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master"
    AutoEventWireup="true" CodeFile="HRAllowances.aspx.cs"
    Inherits="COOPERP_NewScreens_HRAllowances"
    Title="Allowances - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.emp-pick { position: relative; }
.emp-pick__list { display: none; position: absolute; left: 0; right: 0; top: 100%; z-index: 20; max-height: 220px; overflow-y: auto; background: #fff; border: 1px solid #cdd3de; border-top: 0; }
.emp-pick__list.is-open { display: block; }
.emp-pick__item { padding: 7px 10px; cursor: pointer; font-size: 12px; border-bottom: 1px solid #e0e5ed; }
.emp-pick__item:last-child { border-bottom: 0; }
.emp-pick__item:hover, .emp-pick__item.is-active { background: #f5f7fa; }
.emp-pick__none { padding: 8px 10px; font-size: 12px; color: #888; }
.emp-chosen { display: none; margin-top: 6px; padding: 6px 10px; border: 1px solid #e0e5ed; background: #f5f7fa; font-size: 12px; }
.emp-chosen.is-on { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.hr-btn svg { width: 14px; height: 14px; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Allowances</div>
            <div class="hr-header__sub">One-off allowances paid through payroll</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openAdd()">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            Add allowance
        </button>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRPayroll.aspx">Payroll runs</a><a class="hr-tab" href="HRPayslips.aspx">Payslips</a><a class="hr-tab hr-tab--active" href="HRAllowances.aspx">Allowances</a><a class="hr-tab" href="HRDeductions.aspx">Deductions</a></div>

<asp:Literal ID="litBody" runat="server" />

</div>

<!-- Add / edit -->
<div class="hr-modal" id="itemModal" role="dialog" aria-modal="true" aria-labelledby="itemTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="itemTitle">Add allowance</span><button type="button" class="hr-modal__close" onclick="closeModal('itemModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field hr-full" id="empField"><label class="hr-label" for="empSearch">Employee <span class="hr-req">*</span></label>
                    <div class="emp-pick">
                        <input type="text" id="empSearch" class="hr-input" placeholder="Search by name or staff no" autocomplete="off" />
                        <div class="emp-pick__list" id="empList"></div>
                    </div>
                    <div class="emp-chosen" id="empChosen"><span id="empChosenText"></span><button type="button" class="hr-btn hr-btn--link" onclick="clearEmp()">Change</button></div>
                </div>
                <div class="hr-field hr-full" id="empFixed" style="display:none"><span class="hr-label">Employee</span><div id="empFixedName"></div></div>
                <div class="hr-field"><label class="hr-label" for="fType">Type <span class="hr-req">*</span></label>
                    <select id="fType" class="hr-select"><asp:Literal ID="litTypeOptions" runat="server" /></select></div>
                <div class="hr-field"><label class="hr-label" for="fAmount">Amount (UGX) <span class="hr-req">*</span></label>
                    <input type="text" id="fAmount" class="hr-input" inputmode="decimal" /></div>
                <div class="hr-field"><label class="hr-label" for="fMonthSel">Month <span class="hr-req">*</span></label>
                    <select id="fMonthSel" class="hr-select"><asp:Literal ID="litMonthOptions" runat="server" /></select></div>
                <div class="hr-field"><label class="hr-label" for="fYearSel">Year <span class="hr-req">*</span></label>
                    <select id="fYearSel" class="hr-select"><asp:Literal ID="litYearOptions" runat="server" /></select></div>
                <div class="hr-field" id="monthsField"><label class="hr-label" for="fMonths">Number of months</label>
                    <input type="number" id="fMonths" class="hr-input" min="1" max="60" value="1" />
                    <span class="hr-hint">One item per month from the month above</span></div>
                <div class="hr-field hr-full"><label class="hr-label" for="fDesc">Description</label>
                    <textarea id="fDesc" class="hr-textarea" maxlength="500"></textarea></div>
            </div>
            <div class="hr-error" id="itemError" style="margin-top:10px"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('itemModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="itemBtn" onclick="saveItem()">Save</button>
        </div>
    </div>
</div>

<!-- Confirm -->
<div class="hr-modal" id="confirmModal" role="dialog" aria-modal="true" aria-labelledby="confirmTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="confirmTitle"></span><button type="button" class="hr-modal__close" onclick="closeModal('confirmModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body"><p id="confirmText" style="margin:0"></p><div class="hr-error" id="confirmError" style="margin-top:8px"></div></div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('confirmModal')">Back</button>
            <button type="button" class="hr-btn hr-btn--danger" id="confirmBtn">Confirm</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="toast" role="status"></div>

<script type="text/javascript">
(function () {
    var PAGE = 'HRAllowances.aspx';
    var editId = 0, empId = '', searchTimer = null, searchSeq = 0, results = [], active = -1;
    function $(id) { return document.getElementById(id); }
    function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }

    function toast(msg, isErr) {
        var t = $('toast');
        t.textContent = msg;
        t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
        clearTimeout(t._h);
        t._h = setTimeout(function () { t.className = 'hr-toast'; }, 4500);
    }
    function post(action, data, done) {
        var body = new URLSearchParams();
        for (var k in data) if (data.hasOwnProperty(k)) body.append(k, data[k]);
        fetch(PAGE + '?action=' + action, { method: 'POST', body: body, credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(done)
            .catch(function () { done({ ok: false, message: 'The server did not respond. Please try again.' }); });
    }
    function reloadWith(msg) { try { sessionStorage.setItem('hrItemToast', msg); } catch (e) { } location.reload(); }
    function openModal(id) { $(id).classList.add('is-open'); }
    window.closeModal = function (id) { $(id).classList.remove('is-open'); };

    window.applyFilters = function () {
        var p = [], q = $('fQ').value.trim();
        if (q) p.push('q=' + encodeURIComponent(q));
        if ($('fMonth').value) p.push('month=' + $('fMonth').value);
        if ($('fYear').value) p.push('year=' + $('fYear').value);
        if ($('fStatus').value) p.push('status=' + $('fStatus').value);
        location.href = PAGE + (p.length ? '?' + p.join('&') : '');
    };

    // selection
    function checked() {
        var out = [], c = document.querySelectorAll('.row-chk:checked');
        for (var i = 0; i < c.length; i++) out.push(c[i].value);
        return out;
    }
    window.updateBulk = function () {
        var n = checked().length;
        $('bulkBar').classList.toggle('is-on', n > 0);
        $('bulkCount').textContent = n + ' selected';
    };
    window.selectAll = function (box) {
        var c = document.querySelectorAll('.row-chk');
        for (var i = 0; i < c.length; i++) c[i].checked = box.checked;
        updateBulk();
    };
    window.clearSelection = function () {
        var c = document.querySelectorAll('.row-chk');
        for (var i = 0; i < c.length; i++) c[i].checked = false;
        if ($('chkAll')) $('chkAll').checked = false;
        updateBulk();
    };

    // cancel / delete
    function confirmBox(action, ids) {
        var n = ids.length, del = action === 'delete';
        $('confirmTitle').textContent = del ? 'Delete allowances' : 'Cancel allowances';
        $('confirmText').textContent = (del ? 'Delete ' : 'Cancel ') + (n === 1 ? 'this allowance.' : n + ' allowances.') + (del ? ' This cannot be undone.' : '');
        $('confirmError').textContent = '';
        var b = $('confirmBtn');
        b.textContent = del ? 'Delete' : 'Cancel allowance' + (n === 1 ? '' : 's');
        b.disabled = false;
        b.onclick = function () {
            b.disabled = true;
            post(action, { ids: ids.join(',') }, function (res) {
                if (res.ok) reloadWith(res.message);
                else { $('confirmError').textContent = res.message; b.disabled = false; }
            });
        };
        openModal('confirmModal');
    }
    window.one = function (action, id) { confirmBox(action, [String(id)]); };
    window.bulk = function (action) { var ids = checked(); if (ids.length) confirmBox(action, ids); };

    // add / edit
    function setSelect(sel, value) {
        var s = $(sel), found = false;
        for (var i = 0; i < s.options.length; i++) if (s.options[i].value === value) { found = true; break; }
        if (!found && value) { var o = document.createElement('option'); o.value = value; o.textContent = value; s.appendChild(o); }
        s.value = value;
    }
    window.openAdd = function () {
        editId = 0;
        $('itemTitle').textContent = 'Add allowance';
        $('empField').style.display = '';
        $('empFixed').style.display = 'none';
        $('monthsField').style.display = '';
        clearEmp();
        $('fType').value = '';
        $('fAmount').value = '';
        $('fMonths').value = '1';
        $('fDesc').value = '';
        $('itemError').textContent = '';
        $('itemBtn').disabled = false;
        openModal('itemModal');
        $('empSearch').focus();
    };
    window.openEdit = function (btn) {
        var tr = btn.closest('tr');
        editId = tr.getAttribute('data-id');
        $('itemTitle').textContent = 'Edit allowance';
        $('empField').style.display = 'none';
        $('empFixed').style.display = '';
        $('empFixedName').textContent = tr.getAttribute('data-name');
        $('monthsField').style.display = 'none';
        setSelect('fType', tr.getAttribute('data-type'));
        $('fAmount').value = tr.getAttribute('data-amount');
        $('fMonthSel').value = tr.getAttribute('data-month');
        setSelect('fYearSel', tr.getAttribute('data-year'));
        $('fDesc').value = tr.getAttribute('data-desc');
        $('itemError').textContent = '';
        $('itemBtn').disabled = false;
        openModal('itemModal');
    };
    window.saveItem = function () {
        var data = { type: $('fType').value, amount: $('fAmount').value, month: $('fMonthSel').value, year: $('fYearSel').value, description: $('fDesc').value };
        if (editId) data.id = editId;
        else {
            if (!empId) { $('itemError').textContent = 'Select the employee.'; return; }
            data.emp = empId;
            data.months = $('fMonths').value;
        }
        $('itemBtn').disabled = true;
        post(editId ? 'edit' : 'add', data, function (res) {
            if (res.ok) reloadWith(res.message);
            else { $('itemError').textContent = res.message; $('itemBtn').disabled = false; }
        });
    };

    // employee picker: listeners attached once
    window.clearEmp = function () {
        empId = '';
        $('empSearch').value = '';
        $('empSearch').style.display = '';
        $('empChosen').classList.remove('is-on');
        hideList();
    };
    function hideList() { $('empList').classList.remove('is-open'); active = -1; }
    function choose(i) {
        var r = results[i];
        if (!r) return;
        empId = r.empID;
        $('empChosenText').textContent = r.emp_name + (r.EMP_CODE ? ' (' + r.EMP_CODE + ')' : '') + (r.emp_position ? ', ' + r.emp_position : '');
        $('empChosen').classList.add('is-on');
        $('empSearch').style.display = 'none';
        hideList();
    }
    function render() {
        var l = $('empList');
        if (!results.length) { l.innerHTML = '<div class="emp-pick__none">No employee found</div>'; l.classList.add('is-open'); return; }
        var html = '';
        for (var i = 0; i < results.length; i++) {
            var r = results[i];
            html += '<div class="emp-pick__item' + (i === active ? ' is-active' : '') + '" data-i="' + i + '">' + esc(r.emp_name) +
                (r.EMP_CODE ? ' <span class="hr-muted">' + esc(r.EMP_CODE) + '</span>' : '') +
                (r.emp_position ? '<span class="hr-sub">' + esc(r.emp_position) + '</span>' : '') + '</div>';
        }
        l.innerHTML = html;
        l.classList.add('is-open');
    }
    function search(q) {
        var seq = ++searchSeq;
        fetch(PAGE + '?ajax=search_emp&q=' + encodeURIComponent(q), { credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(function (d) { if (seq !== searchSeq) return; results = d.results || []; active = -1; render(); })
            .catch(function () { if (seq === searchSeq) { results = []; hideList(); } });
    }
    $('empSearch').addEventListener('input', function () {
        var q = this.value.trim();
        clearTimeout(searchTimer);
        if (q.length < 2) { hideList(); return; }
        searchTimer = setTimeout(function () { search(q); }, 250);
    });
    $('empSearch').addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); if (active >= 0) choose(active); else if (results.length === 1) choose(0); return; }
        if (!$('empList').classList.contains('is-open') || !results.length) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); active = Math.min(results.length - 1, active + 1); render(); }
        if (e.key === 'ArrowUp') { e.preventDefault(); active = Math.max(0, active - 1); render(); }
    });
    $('empList').addEventListener('mousedown', function (e) {
        var item = e.target.closest('.emp-pick__item');
        if (item) { e.preventDefault(); choose(parseInt(item.getAttribute('data-i'), 10)); }
    });
    $('empSearch').addEventListener('blur', function () { setTimeout(hideList, 150); });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { closeModal('itemModal'); closeModal('confirmModal'); }
        if (e.key === 'Enter' && e.target && e.target.tagName === 'INPUT' && e.target.id !== 'empSearch' && e.target.id !== 'fQ') e.preventDefault();
    });

    try {
        var msg = sessionStorage.getItem('hrItemToast');
        if (msg) { sessionStorage.removeItem('hrItemToast'); toast(msg, false); }
    } catch (e) { }
})();
</script>
</asp:Content>
