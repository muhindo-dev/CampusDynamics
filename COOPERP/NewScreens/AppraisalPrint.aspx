<%@ Page Language="C#" AutoEventWireup="true" CodeFile="AppraisalPrint.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalPrint" %>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Appraisal Form — <asp:Literal ID="litPageTitle" runat="server" /></title>
<style>
/* ═══════════════════════════════════════════════════════════════
   MRU APPRAISAL PRINT DOCUMENT
   Brand colours: Navy #05275C · Blue #174DA4 · Light #e8f0fb
   ═══════════════════════════════════════════════════════════════ */

/* ── Ensure print colours render correctly in Chrome/Edge ── */
* { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

body {
    font-family: "Times New Roman", Times, serif;
    font-size: 10.5pt;
    color: #111;
    background: #e9edf2;
}

/* ── Page wrapper ── */
.print-wrapper {
    width: 210mm;
    min-height: 297mm;
    margin: 0 auto;
    padding: 14mm 18mm 20mm 18mm;
    background: #fff;
}
@media screen { .print-wrapper { box-shadow: 0 4px 28px rgba(0,0,0,.22); margin: 24px auto; } }

/* ── Screen-only toolbar ── */
.no-print {
    text-align: center;
    padding: 12px 16px;
    background: linear-gradient(135deg, #05275C 0%, #174DA4 100%);
    position: sticky;
    top: 0;
    z-index: 999;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 10px;
    box-shadow: 0 2px 8px rgba(0,0,0,.3);
}
.no-print button {
    background: #fff;
    color: #05275C;
    border: none;
    padding: 7px 22px;
    font-size: 12.5px;
    font-weight: 700;
    cursor: pointer;
    border-radius: 3px;
    letter-spacing: 0.3px;
    transition: background .15s;
}
.no-print button:hover { background: #dde6f5; }
.no-print .back-btn {
    background: transparent;
    color: #fff;
    border: 1.5px solid rgba(255,255,255,.55);
    font-weight: 600;
}
.no-print .back-btn:hover { background: rgba(255,255,255,.12); }
.no-print .toolbar-title { color: rgba(255,255,255,.85); font-size: 12px; margin: 0 8px; }
@media print { .no-print { display: none !important; } }

/* ── University Header ── */
.uni-header {
    display: flex;
    align-items: center;
    gap: 14pt;
    padding: 6pt 0 8pt;
    border-bottom: 2pt solid #05275C;
}
.uni-header__logo { height: 58pt; width: auto; flex-shrink: 0; }
.uni-header__text { flex: 1; text-align: center; }
.uni-header__name {
    font-size: 14pt;
    font-weight: bold;
    text-transform: uppercase;
    letter-spacing: 0.5pt;
    color: #05275C;
}
.uni-header__motto { font-size: 8pt; font-style: italic; color: #555; margin-top: 1pt; }
.uni-header__sub   { font-size: 9.5pt; color: #174DA4; margin-top: 3pt; font-weight: 600; letter-spacing: 0.2pt; }
.uni-header__ref   { flex-shrink: 0; text-align: right; font-size: 8pt; color: #555; font-weight: 600; line-height: 1.5; min-width: 48pt; }

/* ── Address bar (below header) ── */
.uni-address-bar {
    text-align: center;
    font-size: 8pt;
    color: #666;
    padding: 3pt 0 4pt;
    border-bottom: 0.5pt solid #c0cfe0;
    margin-bottom: 6pt;
    letter-spacing: 0.2pt;
}

/* ── Form title block ── */
.form-title-block { text-align: center; padding: 8pt 0 4pt; border-bottom: 0.5pt solid #c0cfe0; margin-bottom: 6pt; }
.form-title {
    font-size: 12pt;
    font-weight: bold;
    text-transform: uppercase;
    color: #05275C;
    letter-spacing: 0.5pt;
}
.form-subtitle { font-size: 9pt; color: #555; margin-top: 3pt; }

/* ── Preamble ── */
.preamble-box {
    border-left: 3pt solid #174DA4;
    background: #f4f7fc;
    padding: 6pt 9pt;
    margin: 6pt 0 8pt 0;
    font-size: 8.5pt;
}
.preamble-title {
    font-weight: bold;
    font-size: 9pt;
    text-transform: uppercase;
    color: #05275C;
    margin-bottom: 4pt;
    letter-spacing: 0.4pt;
}
.preamble-box p { margin: 0; line-height: 1.55; color: #333; }

/* ── Section headers ── */
.sec-header {
    background: #05275C;
    color: #fff;
    font-size: 9.5pt;
    font-weight: bold;
    padding: 3.5pt 7pt;
    margin: 10pt 0 0 0;
    text-transform: uppercase;
    letter-spacing: 0.5pt;
}
.sec-header-note { font-weight: normal; font-size: 8pt; text-transform: none; letter-spacing: 0; }
.sec-subheader {
    background: #dde6f5;
    color: #05275C;
    font-size: 8.5pt;
    font-weight: bold;
    padding: 2pt 7pt;
    border: 0.5pt solid #b0c4e0;
    text-transform: uppercase;
    letter-spacing: 0.3pt;
}
.sec-instruction {
    font-size: 8.5pt;
    font-style: italic;
    color: #444;
    margin: 3pt 0 4pt 0;
}

/* ── Bio data grid ── */
.bio-grid {
    width: 100%;
    border-collapse: collapse;
    font-size: 9pt;
    margin-bottom: 3pt;
}
.bio-grid td {
    border: 0.5pt solid #b0c4e0;
    padding: 3pt 5pt;
    vertical-align: middle;
}
.bio-grid thead th {
    border: 0.5pt solid #b0c4e0;
    padding: 3pt 5pt;
    vertical-align: middle;
}
.bio-grid .label {
    font-weight: bold;
    width: 23%;
    background: #e8f0fb;
    color: #05275C;
    white-space: nowrap;
    font-size: 8.5pt;
}
.qual-block { margin-top: 5pt; }
.qual-title { font-weight: bold; font-size: 9pt; color: #05275C; margin-bottom: 2pt; }
.qual-th { background: #174DA4 !important; color: #fff; font-weight: bold; font-size: 8.5pt; text-align: left; }
.reports-to-block {
    margin-top: 4pt;
    border: 0.5pt solid #b0c4e0;
    background: #f8fafd;
    padding: 4pt 7pt;
    font-size: 9pt;
}
.reports-to-label { font-weight: bold; color: #05275C; }
.sig-underline {
    border-bottom: 0.5pt solid #555;
    display: inline-block;
    min-width: 100pt;
    padding-bottom: 1pt;
}

/* ── Status chip ── */
.status-chip {
    display: inline-block;
    font-size: 7.5pt;
    font-weight: 700;
    padding: 1pt 5pt;
    border-radius: 2pt;
    letter-spacing: 0.3pt;
}
.status-chip--valid      { background: #d1fae5; color: #065f46; border: 0.5pt solid #6ee7b7; }
.status-chip--expired    { background: #fee2e2; color: #991b1b; border: 0.5pt solid #fca5a5; }
.status-chip--terminated { background: #fef3c7; color: #92400e; border: 0.5pt solid #fcd34d; }
.status-chip--resigned   { background: #ede9fe; color: #4c1d95; border: 0.5pt solid #c4b5fd; }
.field-blank { color: #bbb; font-size: 8pt; font-style: italic; }

/* ── Section B table ── */
.sec-b-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 8.5pt;
    margin-bottom: 3pt;
}
.sec-b-table th {
    background: #174DA4;
    color: #fff;
    padding: 3pt 4pt;
    font-size: 8pt;
    text-align: center;
    border: 0.5pt solid #0d3a82;
    font-weight: bold;
    letter-spacing: 0.2pt;
}
.sec-b-table td {
    border: 0.5pt solid #c0cfe0;
    padding: 3pt 4pt;
    vertical-align: top;
}
.sec-b-table td.num     { text-align: center; }
.sec-b-table td.b-rating { text-align: center; font-weight: bold; color: #05275C; }
.sec-b-table td.cmnt    { font-size: 8pt; }
.sec-b-total-row td {
    font-weight: bold;
    background: #dde6f5;
    color: #05275C;
    border: 0.5pt solid #b0c4e0;
    padding: 3pt 4pt;
}
.empty-row { text-align: center; color: #bbb; font-style: italic; padding: 10pt; }

/* ── Section C table ── */
.sec-c-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 8.5pt;
    margin-bottom: 3pt;
}
.sec-c-table th {
    background: #174DA4;
    color: #fff;
    padding: 3pt 4pt;
    font-size: 8pt;
    border: 0.5pt solid #0d3a82;
    font-weight: bold;
}
.sec-c-table td {
    border: 0.5pt solid #c0cfe0;
    padding: 2.5pt 4pt;
    vertical-align: middle;
}
.sec-c-table .cat-row td {
    background: #174DA4;
    color: #fff;
    font-weight: bold;
    padding: 2.5pt 5pt;
    font-size: 8.5pt;
    letter-spacing: 0.3pt;
}
.cat-subtotal td {
    background: #edf2fb;
    color: #174DA4;
    font-style: italic;
    font-size: 8pt;
    padding: 2pt 4pt;
    border: 0.5pt solid #c0cfe0;
}
.sec-c-table td.code { text-align: center; font-weight: bold; white-space: nowrap; color: #05275C; }
.sec-c-table td.num  { text-align: center; }
.sec-c-table td.na   { text-align: center; color: #05275C; font-weight: bold; }
.sec-c-table td.cmnt { font-size: 8pt; }
.na-text  { color: #aaa; font-style: italic; }
.empty-note { font-style: italic; color: #999; margin: 6pt 0; }

/* ── Section D table ── */
.sec-d-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 8.5pt;
    margin-bottom: 3pt;
}
.sec-d-table th {
    background: #174DA4;
    color: #fff;
    padding: 3pt 4pt;
    font-size: 8pt;
    border: 0.5pt solid #0d3a82;
    font-weight: bold;
}
.sec-d-table td {
    border: 0.5pt solid #c0cfe0;
    padding: 3pt 4pt;
    vertical-align: top;
}

/* ── Section E / Questions ── */
.sec-e-question {
    margin: 5pt 0;
    border: 0.5pt solid #c0cfe0;
    border-left: 2.5pt solid #174DA4;
    padding: 4pt 6pt;
    background: #fcfeff;
}
.sec-e-qnum  { font-weight: bold; font-size: 9pt; color: #05275C; margin-bottom: 2pt; }
.sec-e-qtext { font-style: italic; font-size: 8.5pt; color: #444; margin-bottom: 3pt; }
.sec-e-response {
    font-size: 9pt;
    min-height: 24pt;
    border-top: 0.5pt dashed #c0cfe0;
    padding-top: 3pt;
    margin-top: 3pt;
    white-space: pre-wrap;
}
.sec-e-empty { color: #bbb; font-style: italic; }

/* ── Declaration block ── */
.decl-box {
    border: 1pt solid #174DA4;
    background: #f8fafd;
    padding: 8pt 10pt;
    margin: 6pt 0;
    font-size: 9.5pt;
}
.emp-underline {
    border-bottom: 0.5pt solid #05275C;
    display: inline-block;
    min-width: 160pt;
    font-weight: 600;
    padding-bottom: 1pt;
}
.decl-option { display: inline-block; margin-right: 22pt; font-size: 10pt; }
.decl-checked { font-weight: bold; text-decoration: underline; color: #05275C; }
.decl-recorded { margin-top: 5pt; font-size: 9pt; color: #05275C; }
.lined-box { border: 0.5pt solid #c0cfe0; min-height: 36pt; padding: 5pt; margin-bottom: 5pt; }

/* ── Score Summary ── */
.score-box {
    border: 1.5pt solid #05275C;
    padding: 6pt 9pt;
    margin: 8pt 0;
    page-break-inside: avoid;
    background: #fafbff;
}
.score-title {
    font-weight: bold;
    font-size: 10pt;
    text-transform: uppercase;
    color: #05275C;
    border-bottom: 0.5pt solid #05275C;
    margin-bottom: 5pt;
    padding-bottom: 3pt;
    letter-spacing: 0.4pt;
}
.score-row {
    display: flex;
    justify-content: space-between;
    font-size: 9pt;
    padding: 2.5pt 0;
    border-bottom: 0.5pt dotted #c0cfe0;
}
.score-row:last-child { border-bottom: none; }
.score-row--rule { border-top: 0.5pt solid #c0cfe0; margin-top: 3pt; padding-top: 3pt; }
.score-label { color: #333; }
.score-val   { font-weight: bold; color: #05275C; }
.score-note  { font-weight: normal; font-size: 7.5pt; color: #888; }
.score-final {
    font-size: 11.5pt;
    font-weight: bold;
    text-align: center;
    margin-top: 7pt;
    border: 1.5pt solid #05275C;
    padding: 5pt;
    background: #05275C;
    color: #fff;
    letter-spacing: 0.5pt;
}
.score-band {
    text-align: center;
    font-size: 8.5pt;
    color: #174DA4;
    margin-top: 4pt;
    font-style: italic;
}

/* ── HR comment box ── */
.hr-comment-box {
    border: 0.5pt solid #c0cfe0;
    background: #f8fafd;
    padding: 5pt 7pt;
    margin-top: 4pt;
    font-size: 9pt;
}

/* ── Signatures ── */
.sig-block { margin-top: 14pt; page-break-inside: avoid; }
.sig-title {
    font-weight: bold;
    font-size: 10pt;
    text-transform: uppercase;
    color: #05275C;
    border-bottom: 1.5pt solid #05275C;
    margin-bottom: 7pt;
    padding-bottom: 3pt;
    letter-spacing: 0.4pt;
}
.comment-box {
    border: 0.5pt solid #b0c4e0;
    padding: 5pt 7pt;
    margin-bottom: 6pt;
    background: #fafbff;
}
.comment-box__label { font-weight: bold; font-size: 9pt; color: #05275C; margin-bottom: 3pt; }
.comment-box__body  { min-height: 28pt; border-bottom: 0.5pt solid #dde6f5; margin-bottom: 4pt; }
.comment-box__footer { font-size: 8pt; color: #444; }
.sig-grid {
    display: grid;
    grid-template-columns: 1fr 1fr 1fr;
    gap: 12pt;
    margin-top: 8pt;
}
.sig-item { border-top: 1.5pt solid #05275C; padding-top: 5pt; }
.sig-item .role   { font-weight: bold; font-size: 9pt; color: #05275C; text-transform: uppercase; margin-bottom: 10pt; }
.sig-item .sig-name { font-size: 9pt; font-weight: 600; margin-bottom: 2pt; }
.sig-field-label  { font-size: 7.5pt; color: #666; margin-top: 2pt; }
.sig-line {
    border-top: 0.5pt solid #666;
    margin-top: 14pt;
    padding-top: 0;
    height: 1pt;
}
.sig-date { font-size: 7.5pt; color: #888; margin-top: 3pt; }

/* ── Rating legend ── */
.rating-legend {
    font-size: 8pt;
    color: #333;
    margin: 3pt 0;
    border: 0.5pt solid #c0cfe0;
    padding: 3pt 7pt;
    background: #f4f7fc;
    border-left: 2.5pt solid #174DA4;
}

/* ── Council-format header, KPA table, e-signatures ── */
.council-grid .label { width: 17%; }
.na-row td { background: #f1f3f6 !important; color: #8a94a3; }
.ev-files { margin-top: 2pt; font-size: 7.5pt; color: #174DA4; }
.esign { margin-top: 6pt; font-size: 8pt; color: #05275C; border: 0.5pt dashed #9fb3d6; padding: 3pt 5pt; background: #f4f7fc; }
.ack-comment { font-size: 8.5pt; margin-top: 4pt; color: #333; }
.decl-checked { font-weight: bold; color: #05275C; }

/* ── Page break utilities ── */
.page-break  { page-break-before: always; }
.avoid-break { page-break-inside: avoid; }

/* ── Print specifics ── */
@media print {
    body { background: #fff; }
    html, body { margin: 0; padding: 0; }
    .print-wrapper { margin: 0; padding: 10mm 15mm 15mm 15mm; box-shadow: none; width: 100%; }
    a { color: inherit; text-decoration: none; }
    table { page-break-inside: auto; }
    tr { page-break-inside: avoid; page-break-after: auto; }
    thead { display: table-header-group; }
    @page { size: A4 portrait; margin: 0; }
}
</style>
</head>
<body>

<!-- Screen-only toolbar -->
<div class="no-print">
    <button class="back-btn" onclick="history.back()"><svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px"><line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/></svg> Back</button>
    <span class="toolbar-title">Appraisal Print Preview</span>
    <button onclick="window.print()"><svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>&nbsp; Print / Save as PDF</button>
</div>

<div class="print-wrapper">

    <!-- University header -->
    <div class="uni-header">
        <img class="uni-header__logo"
             src="<%= ResolveUrl("~/COOPERP/images/welcomelogo.png") %>"
             alt="MRU Logo"
             onerror="this.style.display='none'" />
        <div class="uni-header__text">
            <div class="uni-header__name">Muteesa I Royal University</div>
            <div class="uni-header__motto">Knowledge for Service</div>
            <div class="uni-header__sub">Staff Performance Appraisal System</div>
        </div>
        <div class="uni-header__ref">
            <asp:Literal ID="litEmpRef" runat="server" />
        </div>
    </div>
    <!-- Address bar -->
    <div class="uni-address-bar">
        P.O. Box 1 Masaka / Kampala &nbsp;&bull;&nbsp; www.mru.ac.ug &nbsp;&bull;&nbsp; Muteesa I Royal University
    </div>

    <!-- Form title -->
    <div class="form-title-block">
        <div class="form-title"><asp:Literal ID="litFormTitle" runat="server" /></div>
        <div class="form-subtitle">
            Appraisal Period: <strong><asp:Literal ID="litPeriod" runat="server" /></strong>
            &nbsp;&nbsp;|&nbsp;&nbsp;
            Session: <strong><asp:Literal ID="litSession" runat="server" /></strong>
        </div>
    </div>

    <!-- Dynamic content rendered server-side -->
    <asp:Literal ID="litContent" runat="server" />

</div>

<script>
if (window.location.search.indexOf('autoprint=1') >= 0) {
    window.addEventListener('load', function () {
        setTimeout(function () { window.print(); }, 700);
    });
}
</script>

</body>
</html>
