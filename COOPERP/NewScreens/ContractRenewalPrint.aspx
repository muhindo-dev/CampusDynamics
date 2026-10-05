<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ContractRenewalPrint.aspx.cs" Inherits="COOPERP_NewScreens_ContractRenewalPrint" %>
<%@ Reference Page="~/COOPERP/NewScreens/ContractRenewalView.aspx" %>
<%@ Reference Page="~/COOPERP/NewScreens/ContractRenewals.aspx" %>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Contract Renewal - <asp:Literal ID="litTitle" runat="server" /></title>
<style>
/* MRU contract renewal print pack. Navy #05275C, accent #174DA4. */
* { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
*, *::before, *::after { box-sizing: border-box; }
body { margin: 0; font-family: "Times New Roman", Times, serif; font-size: 10.5pt; color: #111; background: #e9edf2; }
.no-print { position: sticky; top: 0; z-index: 10; background: #05275C; padding: 10px 16px; display: flex; gap: 10px; justify-content: center; flex-wrap: wrap; }
.no-print button { background: #fff; color: #05275C; border: 0; padding: 7px 18px; font: 600 12px -apple-system, "Segoe UI", Roboto, sans-serif; cursor: pointer; border-radius: 0; }
.pp-sheet { width: 210mm; max-width: 100%; min-height: 270mm; margin: 18px auto; padding: 14mm 16mm; background: #fff; box-shadow: 0 4px 24px rgba(0,0,0,.18); }
.pp-landscape .pp-sheet--wide { width: 297mm; min-height: 0; }
.pp-lh { text-align: center; border-bottom: 2.5px solid #05275C; padding-bottom: 6px; margin-bottom: 8px; }
.pp-lh__uni { font-size: 17pt; font-weight: 700; color: #05275C; letter-spacing: 1px; }
.pp-lh__off { font-size: 10pt; color: #174DA4; letter-spacing: .5px; }
.pp-title { text-align: center; font-weight: 700; font-size: 11.5pt; margin: 8px 0 10px; text-decoration: underline; color: #05275C; }
.pp-refline { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 8px; font-size: 9.5pt; color: #333; margin-bottom: 10px; }
.pp-h { font-size: 11pt; color: #05275C; border-bottom: 1px solid #c5d3e8; padding-bottom: 2px; margin: 14px 0 6px; }
table { border-collapse: collapse; width: 100%; }
.pp-kv th, .pp-kv td { border: 1px solid #9aa6b8; padding: 4px 6px; text-align: left; vertical-align: top; font-size: 10pt; }
.pp-kv th { background: #eef3fb; width: 18%; font-weight: 700; font-size: 9pt; text-transform: uppercase; }
.pp-kv td { width: 32%; }
.pp-grid th, .pp-grid td { border: 1px solid #5f6b7d; padding: 5px 6px; vertical-align: top; text-align: left; font-size: 10pt; }
.pp-grid th { background: #05275C; color: #fff; font-size: 8.5pt; font-weight: 700; }
.pp-form th { background: #eef3fb; color: #05275C; }
.pp-no { width: 34px; text-align: center !important; }
.pp-revrow td { background: #fafbfd; font-size: 9.5pt; }
.pp-form-head td { border: 1px solid #5f6b7d; padding: 5px 7px; width: 33.33%; vertical-align: top; font-size: 10pt; }
.pp-form-head span { display: block; font-size: 8pt; font-weight: 700; color: #05275C; }
.pp-form-head { margin-bottom: 10px; }
.pp-file { font-size: 9pt; color: #174DA4; margin-top: 3px; }
.pp-para { margin-top: 8px; }
.pp-para__l { font-size: 9pt; font-weight: 700; text-transform: uppercase; color: #05275C; margin-bottom: 2px; }
.pp-para__t { border: 1px solid #9aa6b8; padding: 6px 8px; min-height: 30px; line-height: 1.45; }
.pp-box { border: 1px solid #5f6b7d; margin-top: 10px; }
.pp-box__l { background: #eef3fb; font-weight: 700; font-size: 9pt; color: #05275C; padding: 4px 7px; border-bottom: 1px solid #5f6b7d; }
.pp-box__t { padding: 7px; min-height: 60px; line-height: 1.45; }
.pp-sign { margin-top: 14px; }
.pp-sign td { width: 50%; padding: 6px 10px 6px 0; vertical-align: top; }
.pp-sign__l { font-size: 9pt; font-weight: 700; color: #05275C; }
.pp-sign__v { border-bottom: 1px solid #111; min-height: 30px; padding-top: 4px; }
.pp-sign__d { font-size: 9.5pt; margin-top: 4px; }
.pp-esign { font-family: "Brush Script MT", "Segoe Script", cursive; font-size: 15pt; color: #05275C; }
.pp-esign__n { font-size: 8pt; color: #666; margin-left: 8px; font-style: italic; }
.pp-mark { width: 120px; white-space: nowrap; }
.pp-small { font-size: 8.5pt; color: #555; }
.pp-nowrap { white-space: nowrap; }
.pp-empty { color: #777; font-style: italic; text-align: center; }
.pp-sched td { font-size: 9.5pt; }
@media print {
    body { background: #fff; }
    .no-print { display: none; }
    .pp-sheet { box-shadow: none; margin: 0; width: auto; min-height: 0; padding: 0; }
    .pp-break { page-break-before: always; }
    tr { page-break-inside: avoid; }
}
@page { size: A4; margin: 12mm; }
</style>
</head>
<body class="<asp:Literal ID="litBodyClass" runat="server" />">
<div class="no-print">
    <button type="button" onclick="window.print()">Print</button>
    <button type="button" onclick="window.close()">Close</button>
</div>
<asp:Literal ID="litBody" runat="server" />
<script type="text/javascript">
if (document.body.className.indexOf('pp-landscape') >= 0) {
    var s = document.createElement('style');
    s.textContent = '@page { size: A4 landscape; margin: 10mm; }';
    document.head.appendChild(s);
}
</script>
</body>
</html>
