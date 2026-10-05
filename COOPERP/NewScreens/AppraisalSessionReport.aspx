<%@ Page Language="C#" AutoEventWireup="true" CodeFile="AppraisalSessionReport.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalSessionReport" %>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Session Report — <asp:Literal ID="litTitle" runat="server" /></title>
<style>
/* ═══════════════════════════════════════════════════════════════
   MRU APPRAISAL SESSION REPORT
   ═══════════════════════════════════════════════════════════════ */
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0;
    -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }

body { font-family: "Segoe UI", Arial, sans-serif; font-size: 10pt; color: #111; background: #e9edf2; }

/* ── A4 Wrapper ── */
.print-wrapper { width: 210mm; min-height: 297mm; margin: 0 auto; padding: 12mm 16mm 20mm 16mm; background: #fff; }
@media screen { .print-wrapper { box-shadow: 0 4px 28px rgba(0,0,0,.22); margin: 24px auto; } }
@media print  { body { background:#fff; } .print-wrapper { margin:0; padding:10mm 14mm 15mm 14mm; box-shadow:none; width:100%; } a{color:inherit;text-decoration:none;} @page{size:A4 portrait;margin:0;} }

/* ── Screen toolbar ── */
.no-print { display:flex; align-items:center; gap:10px; padding:10px 18px; background:linear-gradient(135deg,#05275C 0%,#174DA4 100%); position:sticky; top:0; z-index:999; box-shadow:0 2px 8px rgba(0,0,0,.3); }
.no-print span { color:rgba(255,255,255,.8); font-size:12px; }
.no-print button { padding:6px 18px; font-size:12px; font-weight:700; cursor:pointer; border:none; border-radius:3px; }
.no-print .btn-back { background:transparent; color:#fff; border:1.5px solid rgba(255,255,255,.5); }
.no-print .btn-back:hover { background:rgba(255,255,255,.12); }
.no-print .btn-print { background:#fff; color:#05275C; margin-left:auto; }
.no-print .btn-print:hover { background:#dde6f5; }
@media print { .no-print { display:none !important; } }

/* ── University Header ── */
.uni-header { display:flex; align-items:center; gap:14pt; padding:6pt 0 8pt; border-bottom:2pt solid #05275C; }
.uni-header__logo { height:56pt; width:auto; flex-shrink:0; }
.uni-header__text { flex:1; text-align:center; }
.uni-header__name { font-size:13pt; font-weight:bold; text-transform:uppercase; letter-spacing:.5pt; color:#05275C; }
.uni-header__motto { font-size:8pt; font-style:italic; color:#666; margin-top:1pt; }
.uni-header__sub   { font-size:9pt; color:#174DA4; font-weight:600; margin-top:3pt; }
.uni-header__ref   { flex-shrink:0; font-size:7.5pt; color:#888; text-align:right; line-height:1.6; min-width:50pt; }
.uni-address-bar   { text-align:center; font-size:8pt; color:#666; padding:3pt 0 4pt; border-bottom:.5pt solid #c0cfe0; margin-bottom:6pt; }

/* ── Report Title Block ── */
.rep-title-block { text-align:center; padding:8pt 0 6pt; border-bottom:.5pt solid #c0cfe0; margin-bottom:8pt; }
.rep-title  { font-size:12pt; font-weight:bold; text-transform:uppercase; color:#05275C; letter-spacing:.5pt; }
.rep-subtitle { font-size:10.5pt; color:#333; font-weight:600; margin-top:2pt; }
.rep-meta   { font-size:8pt; color:#888; margin-top:4pt; }

/* ── Section Headers ── */
.rep-sec-header { background:#05275C; color:#fff; font-size:8.5pt; font-weight:bold; padding:3.5pt 8pt;
    margin:10pt 0 6pt 0; text-transform:uppercase; letter-spacing:.5pt;
    display:flex; align-items:center; justify-content:space-between; }
.rep-sec-note { font-weight:normal; font-size:7.5pt; text-transform:none; letter-spacing:0; opacity:.85; }

/* ── Stat Cards ── */
.stat-grid { display:grid; grid-template-columns:repeat(4,1fr); gap:7pt; margin-bottom:4pt; }
.stat-card { border:.5pt solid #ddd; border-top:3pt solid #174DA4; padding:8pt 10pt; text-align:center; }
.stat-val  { font-size:15pt; font-weight:bold; line-height:1; }
.stat-label { font-size:7.5pt; color:#888; text-transform:uppercase; letter-spacing:.3pt; margin-top:3pt; }

/* ── Bio / Info Grid ── */
.bio-grid { width:100%; border-collapse:collapse; font-size:9pt; margin-bottom:3pt; }
.bio-grid td { border:.5pt solid #c0cfe0; padding:3pt 6pt; vertical-align:middle; }
.bio-grid .label { font-weight:bold; width:22%; background:#e8f0fb; color:#05275C; font-size:8.5pt; white-space:nowrap; }

/* ── Stacked Bar ── */
.stacked-bar-wrap { margin-bottom:4pt; }
.stacked-bar { height:13pt; border-radius:2pt; overflow:hidden; display:flex; border:.5pt solid #ddd; margin-bottom:6pt; }
.seg { height:100%; }

/* ── Bar Chart ── */
.bar-chart { }
.bar-row { display:flex; align-items:center; gap:5pt; margin-bottom:3.5pt; }
.bar-label { width:68pt; font-size:8pt; color:#444; text-align:right; }
.bar-track { flex:1; height:9pt; background:#f0f0f0; border-radius:1pt; overflow:hidden; }
.bar-fill  { height:100%; border-radius:1pt; }
.bar-count { width:62pt; font-size:8.5pt; color:#222; font-weight:600; }
.bar-pct   { color:#999; font-weight:400; font-size:7.5pt; }

/* ── Classification Chart ── */
.class-chart { margin-bottom:4pt; }
.class-row { display:flex; align-items:center; gap:5pt; margin-bottom:3.5pt; }
.class-dot  { display:inline-block; width:7pt; height:7pt; border-radius:50%; margin-right:2pt; flex-shrink:0; }
.class-label { width:75pt; font-size:8pt; color:#333; display:flex; align-items:center; }
.class-range { width:44pt; font-size:7.5pt; color:#888; text-align:center; }
.class-track { flex:1; height:9pt; background:#f0f0f0; border-radius:1pt; overflow:hidden; }
.class-fill  { height:100%; border-radius:1pt; }
.class-count { width:62pt; font-size:8.5pt; color:#222; font-weight:600; }

/* ── Score Stats Strip ── */
.score-stats-row { display:flex; border:.5pt solid #ddd; margin-top:6pt; margin-bottom:2pt; }
.score-stat { flex:1; padding:6pt; text-align:center; border-right:.5pt solid #ddd; }
.score-stat:last-child { border-right:none; }
.ss-label { display:block; font-size:7.5pt; color:#888; text-transform:uppercase; letter-spacing:.3pt; }
.ss-val   { display:block; font-size:14pt; font-weight:bold; margin-top:2pt; }

/* ── Category Summary Table ── */
.print-table { width:100%; border-collapse:collapse; font-size:8.5pt; }
.print-table th { background:#174DA4; color:#fff; padding:3.5pt 7pt; text-align:left; font-weight:600; border:.5pt solid #aaa; font-size:8pt; }
.print-table td { padding:3.5pt 7pt; border:.5pt solid #ddd; vertical-align:middle; }
.print-table tr:nth-child(even) td { background:#f8faff; }

/* ── Roster Table ── */
.roster-table { font-size:8pt; }
.roster-cat-header td { background:#05275C !important; color:#fff; font-weight:700; font-size:8.5pt; padding:4pt 8pt; letter-spacing:.2pt; }

/* ── Status Badges ── */
.st-badge { display:inline-block; padding:1.5pt 5pt; font-size:7pt; font-weight:700; text-transform:uppercase; letter-spacing:.2pt; }
.st-done { background:#d1fae5; color:#065f46; }
.st-sub  { background:#dbeafe; color:#1e40af; }
.st-prog { background:#fef3c7; color:#92400e; }
.st-pend { background:#f3f4f6; color:#6b7280; }
.st-can  { background:#fee2e2; color:#991b1b; }

/* ── Classification Badge ── */
.cls-badge { display:inline-block; padding:1.5pt 5pt; font-size:7pt; font-weight:700; }

/* ── Signature Grid ── */
.sig-grid  { display:grid; grid-template-columns:repeat(3,1fr); gap:18pt; margin-top:8pt; }
.sig-item  { }
.sig-name  { font-weight:700; font-size:9pt; color:#05275C; }
.sig-title { font-size:8pt; color:#888; margin-bottom:12pt; }
.sig-line  { border-top:1pt solid #666; margin-bottom:2pt; }
.sig-date  { font-size:7.5pt; color:#aaa; }

/* ── Footer ── */
.rep-footer { text-align:center; font-size:7.5pt; color:#aaa; margin-top:10pt; border-top:.5pt solid #ddd; padding-top:4pt; }

/* ── Utility ── */
.page-break { page-break-before:always; }
.avoid-break { page-break-inside:avoid; }
</style>
</head>
<body>

<!-- Screen toolbar -->
<div class="no-print">
    <button class="btn-back" onclick="history.back()">&#8592; Back to Sessions</button>
    <span>|&nbsp; Appraisal Session Report</span>
    <button class="btn-print" onclick="window.print()"><svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:-2px"><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/></svg>&nbsp; Print / Save as PDF</button>
</div>

<div class="print-wrapper">
    <asp:Literal ID="litContent" runat="server" />
</div>

<script>
if (window.location.search.indexOf('autoprint=1') >= 0) {
    window.addEventListener('load', function () { setTimeout(function () { window.print(); }, 700); });
}
</script>
</body>
</html>
