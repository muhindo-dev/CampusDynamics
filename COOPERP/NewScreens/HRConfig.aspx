<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master"
    AutoEventWireup="true" CodeFile="HRConfig.aspx.cs"
    Inherits="COOPERP_NewScreens_HRConfig"
    Title="Payroll and tax settings - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<link rel="stylesheet" href="<%= ResolveUrl("~/COOPERP/NewScreens/css/hr.css") %>?v=2" />
<style>
.cfg-unit { display: flex; align-items: center; gap: 6px; }
.cfg-unit .hr-input, .cfg-unit .hr-select { flex: 0 1 140px; }
.cfg-unit span { font-size: 11px; color: #888; white-space: nowrap; }
.cfg-bands .hr-input { max-width: 170px; }
.cfg-bands td { padding: 6px 12px; }
.cfg-form { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px 18px; }
@media (max-width: 900px) { .cfg-form { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 600px) { .cfg-form { grid-template-columns: minmax(0, 1fr); } }
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="hr-page">

<div class="hr-header">
    <div class="hr-header__left">
        <div class="hr-header__icon">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg>
        </div>
        <div>
            <div class="hr-header__title">HR settings</div>
            <div class="hr-header__sub">Tax bands, statutory rates and leave defaults</div>
        </div>
    </div>
</div>

<div class="hr-tabs"><a class="hr-tab" href="HRSettings.aspx">Organisation and pay scales</a><a class="hr-tab hr-tab--active" href="HRConfig.aspx">Payroll and tax settings</a></div>

<asp:Literal ID="litResult" runat="server" />

<div class="hr-card">
    <div class="hr-card__head"><div class="hr-card__title">PAYE bands</div><div class="hr-card__meta">Monthly chargeable income, UGX</div></div>
    <div class="hr-table-wrap">
        <table class="hr-table cfg-bands">
            <thead><tr><th style="width:70px">Band</th><th>From (UGX)</th><th>To (UGX)</th><th>Rate (%)</th></tr></thead>
            <tbody>
                <tr><td>1</td>
                    <td><asp:TextBox ID="txtPayeB1Min" runat="server" Text="0" CssClass="hr-input" ReadOnly="true" /></td>
                    <td><asp:TextBox ID="txtPayeB1Max" runat="server" Text="235000" CssClass="hr-input" /></td>
                    <td><asp:TextBox ID="txtPayeB1Rate" runat="server" Text="0" CssClass="hr-input" ReadOnly="true" /></td></tr>
                <tr><td>2</td>
                    <td><asp:TextBox ID="txtPayeB2Min" runat="server" Text="235001" CssClass="hr-input" ReadOnly="true" /></td>
                    <td><asp:TextBox ID="txtPayeB2Max" runat="server" Text="335000" CssClass="hr-input" /></td>
                    <td><asp:TextBox ID="txtPayeB2Rate" runat="server" Text="10" CssClass="hr-input" /></td></tr>
                <tr><td>3</td>
                    <td><asp:TextBox ID="txtPayeB3Min" runat="server" Text="335001" CssClass="hr-input" ReadOnly="true" /></td>
                    <td><asp:TextBox ID="txtPayeB3Max" runat="server" Text="410000" CssClass="hr-input" /></td>
                    <td><asp:TextBox ID="txtPayeB3Rate" runat="server" Text="20" CssClass="hr-input" /></td></tr>
                <tr><td>4</td>
                    <td><asp:TextBox ID="txtPayeB4Min" runat="server" Text="410001" CssClass="hr-input" ReadOnly="true" /></td>
                    <td><asp:TextBox ID="txtPayeB4Max" runat="server" Text="10000000" CssClass="hr-input" /></td>
                    <td><asp:TextBox ID="txtPayeB4Rate" runat="server" Text="30" CssClass="hr-input" /></td></tr>
                <tr><td>5</td>
                    <td><asp:TextBox ID="txtPayeB5Min" runat="server" Text="10000001" CssClass="hr-input" ReadOnly="true" /></td>
                    <td><asp:TextBox ID="txtPayeB5Max" runat="server" Text="" CssClass="hr-input" ReadOnly="true" placeholder="No limit" /></td>
                    <td><asp:TextBox ID="txtPayeB5Rate" runat="server" Text="40" CssClass="hr-input" /></td></tr>
            </tbody>
        </table>
    </div>
    <div class="hr-card__foot"><span>Each band starts one shilling above the previous band.</span></div>
</div>

<div class="hr-card">
    <div class="hr-card__head"><div class="hr-card__title">Statutory deductions</div></div>
    <div class="hr-card__body">
        <div class="cfg-form">
            <div class="hr-field"><label class="hr-label" for="<%= txtNssfEmployee.ClientID %>">NSSF, employee</label>
                <div class="cfg-unit"><asp:TextBox ID="txtNssfEmployee" runat="server" Text="5" CssClass="hr-input" /><span>% of basic pay</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtNssfEmployer.ClientID %>">NSSF, employer</label>
                <div class="cfg-unit"><asp:TextBox ID="txtNssfEmployer" runat="server" Text="10" CssClass="hr-input" /><span>% of basic pay</span></div></div>
            <div class="hr-field"></div>
            <div class="hr-field"><label class="hr-label" for="<%= ddlChargeKabaka.ClientID %>">Kabaka contribution</label>
                <asp:DropDownList ID="ddlChargeKabaka" runat="server" CssClass="hr-select">
                    <asp:ListItem Value="Yes">Charge</asp:ListItem>
                    <asp:ListItem Value="No">Do not charge</asp:ListItem>
                </asp:DropDownList></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtKabakaRate.ClientID %>">Kabaka contribution rate</label>
                <div class="cfg-unit"><asp:TextBox ID="txtKabakaRate" runat="server" Text="1" CssClass="hr-input" /><span>% of basic pay</span></div></div>
            <div class="hr-field"></div>
            <div class="hr-field"><label class="hr-label" for="<%= ddlChargeLocalTax.ClientID %>">Local service tax</label>
                <asp:DropDownList ID="ddlChargeLocalTax" runat="server" CssClass="hr-select">
                    <asp:ListItem Value="No">Do not charge</asp:ListItem>
                    <asp:ListItem Value="Yes">Charge</asp:ListItem>
                </asp:DropDownList></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtLocalTaxRate.ClientID %>">Local service tax rate</label>
                <div class="cfg-unit"><asp:TextBox ID="txtLocalTaxRate" runat="server" Text="1" CssClass="hr-input" /><span>% of basic pay</span></div></div>
        </div>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head"><div class="hr-card__title">Leave entitlements</div><div class="hr-card__meta">Days per year</div></div>
    <div class="hr-card__body">
        <div class="cfg-form">
            <div class="hr-field"><label class="hr-label" for="<%= txtAnnualLeave.ClientID %>">Annual leave</label>
                <div class="cfg-unit"><asp:TextBox ID="txtAnnualLeave" runat="server" Text="30" CssClass="hr-input" /><span>days</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtMaternityLeave.ClientID %>">Maternity leave</label>
                <div class="cfg-unit"><asp:TextBox ID="txtMaternityLeave" runat="server" Text="60" CssClass="hr-input" /><span>days</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtPaternityLeave.ClientID %>">Paternity leave</label>
                <div class="cfg-unit"><asp:TextBox ID="txtPaternityLeave" runat="server" Text="4" CssClass="hr-input" /><span>days</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtSickLeave.ClientID %>">Sick leave</label>
                <div class="cfg-unit"><asp:TextBox ID="txtSickLeave" runat="server" Text="30" CssClass="hr-input" /><span>days</span></div></div>
        </div>
    </div>
</div>

<div class="hr-card">
    <div class="hr-card__head"><div class="hr-card__title">Employment terms</div></div>
    <div class="hr-card__body">
        <div class="cfg-form">
            <div class="hr-field"><label class="hr-label" for="<%= ddlFYStartMonth.ClientID %>">Financial year starts</label>
                <asp:DropDownList ID="ddlFYStartMonth" runat="server" CssClass="hr-select">
                    <asp:ListItem Value="1">January</asp:ListItem>
                    <asp:ListItem Value="2">February</asp:ListItem>
                    <asp:ListItem Value="3">March</asp:ListItem>
                    <asp:ListItem Value="4">April</asp:ListItem>
                    <asp:ListItem Value="5">May</asp:ListItem>
                    <asp:ListItem Value="6">June</asp:ListItem>
                    <asp:ListItem Value="7" Selected="True">July</asp:ListItem>
                    <asp:ListItem Value="8">August</asp:ListItem>
                    <asp:ListItem Value="9">September</asp:ListItem>
                    <asp:ListItem Value="10">October</asp:ListItem>
                    <asp:ListItem Value="11">November</asp:ListItem>
                    <asp:ListItem Value="12">December</asp:ListItem>
                </asp:DropDownList></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtProbation.ClientID %>">Probation</label>
                <div class="cfg-unit"><asp:TextBox ID="txtProbation" runat="server" Text="3" CssClass="hr-input" /><span>months</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtNotice.ClientID %>">Notice period</label>
                <div class="cfg-unit"><asp:TextBox ID="txtNotice" runat="server" Text="30" CssClass="hr-input" /><span>days</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtOvertime.ClientID %>">Overtime rate</label>
                <div class="cfg-unit"><asp:TextBox ID="txtOvertime" runat="server" Text="1.5" CssClass="hr-input" /><span>times the hourly rate</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtGratuity.ClientID %>">Gratuity</label>
                <div class="cfg-unit"><asp:TextBox ID="txtGratuity" runat="server" Text="5" CssClass="hr-input" /><span>% of annual gross</span></div></div>
            <div class="hr-field"></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtWorkingDays.ClientID %>">Working days</label>
                <div class="cfg-unit"><asp:TextBox ID="txtWorkingDays" runat="server" Text="22" CssClass="hr-input" /><span>days per month</span></div></div>
            <div class="hr-field"><label class="hr-label" for="<%= txtWorkingHours.ClientID %>">Working hours</label>
                <div class="cfg-unit"><asp:TextBox ID="txtWorkingHours" runat="server" Text="8" CssClass="hr-input" /><span>hours per day</span></div></div>
        </div>
    </div>
    <div class="hr-card__foot">
        <span><asp:Literal ID="litLastUpdated" runat="server" /></span>
        <div class="hr-row">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="openReset()">Reset to defaults</button>
            <asp:Button ID="btnSave" runat="server" Text="Save settings" CssClass="hr-btn hr-btn--primary" OnClick="btnSave_Click" />
        </div>
    </div>
</div>

</div>

<asp:HiddenField ID="hdnResetConfirm" runat="server" />
<asp:Button ID="btnReset" runat="server" style="display:none" OnClick="btnReset_Click" />

<div class="hr-modal" id="resetModal" role="dialog" aria-modal="true" aria-labelledby="resetTitle">
    <div class="hr-modal__box">
        <div class="hr-modal__head"><span id="resetTitle">Reset to defaults</span><button type="button" class="hr-modal__close" onclick="closeReset()" aria-label="Close"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button></div>
        <div class="hr-modal__body">
            <p style="margin:0 0 10px">All payroll, tax and leave settings on this page return to their default values. The next payroll run uses them.</p>
            <div class="hr-field"><label class="hr-label" for="resetWord">Type RESET to confirm</label>
                <input type="text" id="resetWord" class="hr-input" autocomplete="off" oninput="document.getElementById('resetBtn').disabled = this.value !== 'RESET';" /></div>
        </div>
        <div class="hr-modal__foot">
            <button type="button" class="hr-btn hr-btn--secondary" onclick="closeReset()">Cancel</button>
            <button type="button" class="hr-btn hr-btn--danger" id="resetBtn" disabled onclick="doReset()">Reset</button>
        </div>
    </div>
</div>

<script type="text/javascript">
(function () {
    window.openReset = function () {
        document.getElementById('resetWord').value = '';
        document.getElementById('resetBtn').disabled = true;
        document.getElementById('resetModal').classList.add('is-open');
        document.getElementById('resetWord').focus();
    };
    window.closeReset = function () { document.getElementById('resetModal').classList.remove('is-open'); };
    window.doReset = function () {
        var w = document.getElementById('resetWord').value;
        if (w !== 'RESET') return;
        document.getElementById('<%= hdnResetConfirm.ClientID %>').value = w;
        document.getElementById('<%= btnReset.ClientID %>').click();
    };
    document.getElementById('resetWord').addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); doReset(); }
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeReset(); });

    // Each band starts one shilling above the previous band's upper limit.
    var maxIds = ['<%= txtPayeB1Max.ClientID %>', '<%= txtPayeB2Max.ClientID %>', '<%= txtPayeB3Max.ClientID %>', '<%= txtPayeB4Max.ClientID %>'];
    var minIds = ['<%= txtPayeB2Min.ClientID %>', '<%= txtPayeB3Min.ClientID %>', '<%= txtPayeB4Min.ClientID %>', '<%= txtPayeB5Min.ClientID %>'];
    maxIds.forEach(function (id, i) {
        var mx = document.getElementById(id), mn = document.getElementById(minIds[i]);
        if (!mx || !mn) return;
        mx.addEventListener('input', function () {
            var v = parseInt(mx.value.replace(/,/g, ''), 10);
            if (!isNaN(v)) mn.value = String(v + 1);
        });
    });
})();
</script>
</asp:Content>
