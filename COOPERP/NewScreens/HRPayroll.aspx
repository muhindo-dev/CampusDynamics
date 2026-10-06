<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master"
    AutoEventWireup="true" CodeFile="HRPayroll.aspx.cs"
    Inherits="COOPERP_NewScreens_HRPayroll"
    Title="Payroll runs - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.run-menu { position: relative; display: inline-block; }
.run-menu .hr-btn svg { width: 14px; height: 14px; }
.run-menu__list { display: none; position: fixed; z-index: 1500; min-width: 180px; background: #fff; border: 1px solid #cdd3de; padding: 4px 0; text-align: left; }
.run-menu__list.is-open { display: block; }
.run-menu__list a, .run-menu__list button { display: block; width: 100%; padding: 7px 14px; font: inherit; font-size: 12px; color: #1a1a2e; background: none; border: 0; text-align: left; text-decoration: none; cursor: pointer; white-space: nowrap; }
.run-menu__list a:hover, .run-menu__list button:hover { background: #f5f7fa; color: #05275C; }
.run-menu__list .is-danger { color: #b42318; }
.pick-list { max-height: 200px; overflow-y: auto; border: 1px solid #cdd3de; padding: 4px 8px; background: #fff; }
.pick { display: flex; align-items: center; gap: 6px; padding: 3px 0; font-size: 12px; font-weight: 400; text-transform: none; letter-spacing: 0; color: #1a1a2e; cursor: pointer; }
.pick input { margin: 0; accent-color: #05275C; }
.hr-btn svg { width: 14px; height: 14px; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="5" width="20" height="14" rx="2"/><line x1="2" y1="10" x2="22" y2="10"/><line x1="6" y1="15" x2="10" y2="15"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Payroll runs</div>
            <div class="hr-header__sub">Monthly payroll runs and registers</div>
        </div>
    </div>
    <div class="hr-header__actions">
        <button type="button" class="hr-btn hr-btn--inverse" onclick="openCreate()">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            New payroll run
        </button>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab hr-tab--active" href="HRPayroll.aspx">Payroll runs</a><a class="hr-tab" href="HRPayslips.aspx">Payslips</a><a class="hr-tab" href="HRAllowances.aspx">Allowances</a><a class="hr-tab" href="HRDeductions.aspx">Deductions</a></div>

<asp:Literal ID="litBody" runat="server" />

</div>

<!-- New payroll run -->
<div class="hr-modal" id="createModal" role="dialog" aria-modal="true" aria-labelledby="createTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="createTitle">New payroll run</span><button type="button" class="hr-modal__close" onclick="closeModal('createModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div class="hr-form">
                <div class="hr-field"><label class="hr-label" for="cMonth">Month <span class="hr-req">*</span></label>
                    <select id="cMonth" class="hr-select" onchange="autoTitle()"><asp:Literal ID="litMonthOptions" runat="server" /></select></div>
                <div class="hr-field"><label class="hr-label" for="cYear">Year <span class="hr-req">*</span></label>
                    <select id="cYear" class="hr-select" onchange="autoTitle()"><asp:Literal ID="litYearOptions" runat="server" /></select></div>
                <div class="hr-field hr-full"><label class="hr-label" for="cTitle">Title <span class="hr-req">*</span></label>
                    <input type="text" id="cTitle" class="hr-input" maxlength="45" /></div>
                <div class="hr-field hr-full"><span class="hr-label">Coverage</span>
                    <div class="hr-radios">
                        <label><input type="radio" name="cTarget" value="ALL" checked onchange="showTarget()" /> All staff with a valid contract</label>
                        <label><input type="radio" name="cTarget" value="DEPARTMENT" onchange="showTarget()" /> Departments</label>
                        <label><input type="radio" name="cTarget" value="EMPLOYEE" onchange="showTarget()" /> Selected employees</label>
                    </div></div>
                <div class="hr-field hr-full" id="cDeptBox" style="display:none">
                    <div class="pick-list"><asp:Literal ID="litDeptChecks" runat="server" /></div></div>
                <div class="hr-field hr-full" id="cEmpBox" style="display:none">
                    <input type="text" id="cEmpFilter" class="hr-input" placeholder="Filter by name or staff no" oninput="filterEmps()" />
                    <div class="pick-list" id="cEmpList"><asp:Literal ID="litEmpChecks" runat="server" /></div></div>
                <div class="hr-field"><span class="hr-label">One-off allowances</span>
                    <div class="hr-radios"><label><input type="radio" name="cAllow" value="YES" checked /> Include</label><label><input type="radio" name="cAllow" value="NO" /> Exclude</label></div></div>
                <div class="hr-field"><span class="hr-label">One-off deductions</span>
                    <div class="hr-radios"><label><input type="radio" name="cDed" value="YES" checked /> Include</label><label><input type="radio" name="cDed" value="NO" /> Exclude</label></div></div>
                <div class="hr-field hr-full"><label class="hr-label" for="cNotes">Notes</label>
                    <input type="text" id="cNotes" class="hr-input" maxlength="45" /></div>
            </div>
            <div class="hr-error" id="createError" style="margin-top:10px"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('createModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="createBtn" onclick="submitCreate()">Create run</button>
        </div>
    </div>
</div>

<!-- Generate payslips -->
<div class="hr-modal" id="genModal" role="dialog" aria-modal="true" aria-labelledby="genTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="genTitle">Generate payslips</span><button type="button" class="hr-modal__close" onclick="closeModal('genModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <div id="genBody" class="hr-loading">Loading</div>
            <div class="hr-error" id="genError" style="margin-top:10px"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('genModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="genBtn" onclick="submitGenerate()">Generate</button>
        </div>
    </div>
</div>

<!-- Confirm -->
<div class="hr-modal" id="confirmModal" role="dialog" aria-modal="true" aria-labelledby="confirmTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="confirmTitle"></span><button type="button" class="hr-modal__close" onclick="closeModal('confirmModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body"><p id="confirmText" style="margin:0"></p><div class="hr-error" id="confirmError" style="margin-top:10px"></div></div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('confirmModal')">Back</button>
            <button type="button" class="hr-btn hr-btn--primary" id="confirmBtn">Confirm</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="toast" role="status"></div>

<script type="text/javascript">
(function () {
    var PAGE = 'HRPayroll.aspx';
    var MONTHS = ['', 'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
    var genId = 0;

    function $(id) { return document.getElementById(id); }

    function toast(msg, isErr) {
        var t = $('toast');
        t.textContent = msg;
        t.className = 'hr-toast is-on' + (isErr ? ' hr-toast--err' : '');
        clearTimeout(t._h);
        t._h = setTimeout(function () { t.className = 'hr-toast'; }, 4000);
    }

    function post(action, data, done) {
        var body = new URLSearchParams();
        for (var k in data) if (data.hasOwnProperty(k)) body.append(k, data[k]);
        fetch(PAGE + '?action=' + action, { method: 'POST', body: body, credentials: 'same-origin' })
            .then(function (r) { return r.json(); })
            .then(done)
            .catch(function () { done({ ok: false, message: 'The server did not respond. Please try again.' }); });
    }

    function openModal(id) { $(id).classList.add('is-open'); }
    window.closeModal = function (id) { $(id).classList.remove('is-open'); };

    // filters
    window.applyFilters = function () {
        var y = $('fYear') ? $('fYear').value : '';
        var s = $('fStatus') ? $('fStatus').value : '';
        location.href = PAGE + '?year=' + encodeURIComponent(y) + (s ? '&status=' + encodeURIComponent(s) : '');
    };

    // row menu
    window.toggleMenu = function (btn, ev) {
        ev.stopPropagation();
        var list = btn.nextElementSibling, open = list.classList.contains('is-open');
        closeMenus();
        if (open) return;
        list.classList.add('is-open');
        var r = btn.getBoundingClientRect();
        var h = list.offsetHeight, w = list.offsetWidth;
        list.style.top = (window.innerHeight - r.bottom < h + 8 ? r.top - h - 4 : r.bottom + 4) + 'px';
        list.style.left = Math.max(8, r.right - w) + 'px';
    };
    function closeMenus() {
        var open = document.querySelectorAll('.run-menu__list.is-open');
        for (var i = 0; i < open.length; i++) open[i].classList.remove('is-open');
    }
    document.addEventListener('click', closeMenus);
    window.addEventListener('scroll', closeMenus, true);

    // selection
    function checked() {
        var out = [], c = document.querySelectorAll('.row-chk:checked');
        for (var i = 0; i < c.length; i++) out.push(c[i].value);
        return out;
    }
    window.updateBulk = function () {
        var n = checked().length, bar = $('bulkBar');
        if (!bar) return;
        bar.classList.toggle('is-on', n > 0);
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

    // confirm dialog
    function confirmBox(title, text, label, danger, onOk) {
        $('confirmTitle').textContent = title;
        $('confirmText').textContent = text;
        $('confirmError').textContent = '';
        var b = $('confirmBtn');
        b.textContent = label;
        b.className = 'hr-btn ' + (danger ? 'hr-btn--danger' : 'hr-btn--primary');
        b.disabled = false;
        b.onclick = function () { b.disabled = true; onOk(function (msg) { $('confirmError').textContent = msg; b.disabled = false; }); };
        openModal('confirmModal');
    }

    function afterOk(res) {
        if (res.ok) { try { sessionStorage.setItem('hrPayrollToast', res.message); } catch (e) { } location.reload(); return true; }
        return false;
    }

    var ACTIONS = {
        approve: ['Approve and lock', 'Approve this payroll run and lock it. One-off allowances and deductions in the run are marked settled.', 'Approve and lock', false],
        cancel: ['Cancel payroll run', 'Cancel this payroll run. Its one-off allowances and deductions return to pending.', 'Cancel run', true],
        'delete': ['Delete payroll run', 'Delete this payroll run and its payslips that are not approved.', 'Delete run', true]
    };
    window.runAction = function (action, id) {
        closeMenus();
        var a = ACTIONS[action];
        confirmBox(a[0], a[1], a[2], a[3], function (fail) {
            post(action, { id: id }, function (res) {
                if (!res.ok) { fail(res.message); return; }
                try { sessionStorage.setItem('hrPayrollToast', res.message); } catch (e) { }
                if (action === 'delete' && /run=/.test(location.search)) location.href = PAGE;
                else location.reload();
            });
        });
    };

    window.bulkOp = function (op) {
        var ids = checked();
        if (!ids.length) return;
        var words = { GENERATE: ['Generate payslips', 'Generate payslips for ' + ids.length + ' payroll run(s).', 'Generate', false],
                      CANCEL: ['Cancel payroll runs', 'Cancel ' + ids.length + ' payroll run(s). Their one-off items return to pending.', 'Cancel runs', true],
                      DELETE: ['Delete payroll runs', 'Delete ' + ids.length + ' payroll run(s) and their payslips that are not approved.', 'Delete runs', true] }[op];
        confirmBox(words[0], words[1], words[2], words[3], function (fail) {
            post('bulk', { op: op, ids: ids.join(',') }, function (res) {
                try { sessionStorage.setItem('hrPayrollToast', res.message); } catch (e) { }
                location.reload();
            });
        });
    };

    // generate
    window.openGenerate = function (id) {
        closeMenus();
        genId = id;
        $('genBody').className = 'hr-loading';
        $('genBody').textContent = 'Loading';
        $('genError').textContent = '';
        $('genBtn').disabled = true;
        openModal('genModal');
        post('preview', { id: id }, function (res) {
            if (!res.ok) { $('genBody').className = ''; $('genBody').textContent = ''; $('genError').textContent = res.message; return; }
            var rows = [['Payroll run', res.title], ['Period', res.period], ['Coverage', res.coverage], ['Staff to pay', String(res.staff)],
                        ['One-off allowances', res.allowances], ['One-off deductions', res.deductions]];
            var html = '<dl class="hr-dl hr-dl--2">';
            for (var i = 0; i < rows.length; i++) html += '<div><dt>' + esc(rows[i][0]) + '</dt><dd>' + esc(rows[i][1]) + '</dd></div>';
            html += '</dl>';
            if (res.replace > 0) html += '<div class="hr-notice hr-notice--warn" style="margin:12px 0 0">' + res.replace + ' existing payslip(s) will be replaced.' + (res.keep > 0 ? ' ' + res.keep + ' approved payslip(s) stay as they are.' : '') + '</div>';
            $('genBody').className = '';
            $('genBody').innerHTML = html;
            $('genBtn').disabled = false;
        });
    };
    window.submitGenerate = function () {
        $('genBtn').disabled = true;
        $('genBtn').textContent = 'Generating';
        post('generate', { id: genId }, function (res) {
            if (!afterOk(res)) { $('genError').textContent = res.message; $('genBtn').disabled = false; $('genBtn').textContent = 'Generate'; }
        });
    };

    // create
    window.openCreate = function () {
        $('createError').textContent = '';
        $('cTitle').value = '';
        autoTitle();
        showTarget();
        openModal('createModal');
    };
    window.autoTitle = function () {
        var t = $('cTitle'), m = parseInt($('cMonth').value, 10) || 0, y = $('cYear').value;
        if (t.value && !/ payroll$/i.test(t.value)) return;
        if (m && y) t.value = MONTHS[m] + ' ' + y + ' payroll';
    };
    function target() {
        var r = document.querySelector('input[name="cTarget"]:checked');
        return r ? r.value : 'ALL';
    }
    window.showTarget = function () {
        var t = target();
        $('cDeptBox').style.display = t === 'DEPARTMENT' ? '' : 'none';
        $('cEmpBox').style.display = t === 'EMPLOYEE' ? '' : 'none';
    };
    window.filterEmps = function () {
        var q = $('cEmpFilter').value.toLowerCase().trim(), l = document.querySelectorAll('#cEmpList .pick');
        for (var i = 0; i < l.length; i++) l[i].style.display = !q || l[i].getAttribute('data-q').indexOf(q) >= 0 ? '' : 'none';
    };
    function radio(name) { var r = document.querySelector('input[name="' + name + '"]:checked'); return r ? r.value : ''; }
    window.submitCreate = function () {
        var t = target(), ids = [];
        if (t !== 'ALL') {
            var c = document.querySelectorAll('input[name="' + (t === 'DEPARTMENT' ? 'pickDept' : 'pickEmp') + '"]:checked');
            for (var i = 0; i < c.length; i++) ids.push(c[i].value);
        }
        var b = $('createBtn');
        b.disabled = true;
        post('create', { title: $('cTitle').value, month: $('cMonth').value, year: $('cYear').value, target: t, ids: ids.join(','),
                         allowances: radio('cAllow'), deductions: radio('cDed'), comments: $('cNotes').value }, function (res) {
            if (!afterOk(res)) { $('createError').textContent = res.message; b.disabled = false; }
        });
    };

    function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }

    // keep Enter in dialog fields from posting the page
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { closeModal('createModal'); closeModal('genModal'); closeModal('confirmModal'); closeMenus(); }
        if (e.key === 'Enter' && e.target && e.target.tagName === 'INPUT') e.preventDefault();
    });

    try {
        var msg = sessionStorage.getItem('hrPayrollToast');
        if (msg) { sessionStorage.removeItem('hrPayrollToast'); toast(msg, false); }
    } catch (e) { }
})();
</script>
</asp:Content>
