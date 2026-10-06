<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master"
    AutoEventWireup="true" CodeFile="HRPayslips.aspx.cs"
    Inherits="COOPERP_NewScreens_HRPayslips"
    Title="Payslips - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.hr-btn svg { width: 14px; height: 14px; }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>
        </div>
        <div>
            <div class="hr-header__title">Payslips</div>
            <div class="hr-header__sub">Review and approve payslips by payroll run</div>
        </div>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRPayroll.aspx">Payroll runs</a><a class="hr-tab hr-tab--active" href="HRPayslips.aspx">Payslips</a><a class="hr-tab" href="HRAllowances.aspx">Allowances</a><a class="hr-tab" href="HRDeductions.aspx">Deductions</a></div>

<asp:Literal ID="litBody" runat="server" />

</div>

<!-- Reject -->
<div class="hr-modal" id="rejectModal" role="dialog" aria-modal="true" aria-labelledby="rejectTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="rejectTitle">Reject payslips</span><button type="button" class="hr-modal__close" onclick="closeModal('rejectModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <p id="rejectCount" style="margin:0 0 10px"></p>
            <div class="hr-field"><label class="hr-label" for="rejectReason">Reason <span class="hr-req">*</span></label>
                <textarea id="rejectReason" class="hr-textarea" maxlength="500"></textarea></div>
            <div class="hr-error" id="rejectError" style="margin-top:8px"></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('rejectModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--danger" id="rejectBtn" onclick="submitReject()">Reject</button>
        </div>
    </div>
</div>

<!-- Approve -->
<div class="hr-modal" id="approveModal" role="dialog" aria-modal="true" aria-labelledby="approveTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="approveTitle">Approve payslips</span><button type="button" class="hr-modal__close" onclick="closeModal('approveModal')" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body"><p id="approveText" style="margin:0"></p><div class="hr-error" id="approveError" style="margin-top:8px"></div></div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeModal('approveModal')">Cancel</button>
            <button type="button" class="hr-btn hr-btn--primary" id="approveBtn" onclick="submitApprove()">Approve</button>
        </div>
    </div>
</div>

<div class="hr-toast" id="toast" role="status"></div>

<script type="text/javascript">
(function () {
    var PAGE = 'HRPayslips.aspx';
    var pending = [];
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

    window.applyFilters = function () {
        var p = [];
        p.push('payroll_id=' + encodeURIComponent($('fRun').value));
        if ($('fStatus').value) p.push('status=' + encodeURIComponent($('fStatus').value));
        if ($('fQ').value.trim()) p.push('q=' + encodeURIComponent($('fQ').value.trim()));
        location.href = PAGE + '?' + p.join('&');
    };

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

    function done(res, errBox, btn) {
        if (res.ok) { try { sessionStorage.setItem('hrSlipToast', res.message); } catch (e) { } location.reload(); return; }
        $(errBox).textContent = res.message;
        $(btn).disabled = false;
    }

    function openApprove(ids) {
        pending = ids;
        $('approveText').textContent = ids.length === 1 ? 'Approve this payslip.' : 'Approve ' + ids.length + ' payslips.';
        $('approveError').textContent = '';
        $('approveBtn').disabled = false;
        openModal('approveModal');
    }
    function openReject(ids) {
        pending = ids;
        $('rejectCount').textContent = ids.length === 1 ? 'Reject this payslip. The reason is shown on the payslip list.' : 'Reject ' + ids.length + ' payslips with the same reason.';
        $('rejectReason').value = '';
        $('rejectError').textContent = '';
        $('rejectBtn').disabled = false;
        openModal('rejectModal');
        $('rejectReason').focus();
    }
    window.approveOne = function (id) { openApprove([String(id)]); };
    window.rejectOne = function (id) { openReject([String(id)]); };
    window.approveSelected = function () { var ids = checked(); if (ids.length) openApprove(ids); };
    window.rejectSelected = function () { var ids = checked(); if (ids.length) openReject(ids); };

    window.submitApprove = function () {
        $('approveBtn').disabled = true;
        post('approve', { ids: pending.join(',') }, function (res) { done(res, 'approveError', 'approveBtn'); });
    };
    window.submitReject = function () {
        var reason = $('rejectReason').value.trim();
        if (!reason) { $('rejectError').textContent = 'Enter the reason for rejecting.'; return; }
        $('rejectBtn').disabled = true;
        post('reject', { ids: pending.join(','), reason: reason }, function (res) { done(res, 'rejectError', 'rejectBtn'); });
    };

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { closeModal('rejectModal'); closeModal('approveModal'); }
    });

    try {
        var msg = sessionStorage.getItem('hrSlipToast');
        if (msg) { sessionStorage.removeItem('hrSlipToast'); toast(msg, false); }
    } catch (e) { }
})();
</script>
</asp:Content>
