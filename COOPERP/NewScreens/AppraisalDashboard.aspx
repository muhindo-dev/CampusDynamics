<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AppraisalDashboard.aspx.cs" Inherits="COOPERP_NewScreens_AppraisalDashboard" Title="Appraisal Dashboard" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ══════════════════════════════════════════════════════════════
   APPRAISAL DASHBOARD  — squared, compact, primary palette
   Palette: #05275C navy · #174DA4 blue · #f5f7fa surface · #e0e5ed border
   ══════════════════════════════════════════════════════════════ */
*,*::before,*::after{box-sizing:border-box;}

/* ── Tokens ── */
:root{
  --navy:#05275C; --blue:#174DA4; --blue-dim:#1e3a7b;
  --surface:#f5f7fa; --border:#e0e5ed; --text:#1a1a2e; --sub:#555; --muted:#888;
  --green:#1a7a4a; --green-bg:#e6f4ee;
  --red:#c62828;   --red-bg:#fdecea;
  --amber:#92400e; --amber-bg:#fef3c7;
  --grey:#6b7280;  --grey-bg:#f1f3f5;
}

/* ── Shared resets ── */
.pad{ padding:14px 16px; }
.pad-sm{ padding:10px 14px; }

/* ══ PAGE HEADER ══════════════════════════════════════════════ */
.pa-hdr{
  display:flex; align-items:center; gap:14px;
  margin-bottom:16px; flex-wrap:wrap;
  padding-bottom:12px; border-bottom:2px solid var(--navy);
}
.pa-hdr__badge{
  background:var(--navy); color:#fff;
  padding:8px 12px; font-size:11px; font-weight:700;
  letter-spacing:.5px; text-transform:uppercase; white-space:nowrap;
}
.pa-hdr__title{ font-size:18px; font-weight:700; color:var(--navy); line-height:1.2; }
.pa-hdr__sub{ font-size:11px; color:var(--muted); margin-top:1px; }
.pa-hdr__right{ margin-left:auto; display:flex; align-items:center; gap:8px; flex-wrap:wrap; }

/* ── Session filter ── */
.pa-sf{ display:flex; align-items:center; gap:6px; }
.pa-sf label{ font-size:11px; font-weight:600; color:var(--sub); white-space:nowrap; }
.pa-sf select{
  font-size:11px; padding:5px 8px;
  border:1px solid var(--border); border-radius:0;
  background:#fff; color:var(--text); min-width:200px;
}
.pa-sf select:focus{ outline:none; border-color:var(--blue); }

/* ── Button ── */
.pa-btn{
  display:inline-flex; align-items:center; gap:5px;
  padding:6px 12px; font-size:11px; font-weight:600;
  border:1px solid var(--border); background:#fff; color:var(--sub);
  cursor:pointer; text-decoration:none; white-space:nowrap;
}
.pa-btn:hover{ border-color:var(--blue); color:var(--blue); }
.pa-btn--primary{ background:var(--navy); color:#fff; border-color:var(--navy); }
.pa-btn--primary:hover{ background:var(--blue); border-color:var(--blue); }

/* ══ KPI GRID ═════════════════════════════════════════════════ */
.pa-kpi-grid{
  display:grid; grid-template-columns:repeat(4,1fr);
  gap:1px; background:var(--border);
  border:1px solid var(--border);
  margin-bottom:14px;
}
@media(max-width:900px){.pa-kpi-grid{grid-template-columns:repeat(2,1fr);}}
@media(max-width:500px){.pa-kpi-grid{grid-template-columns:1fr 1fr;}}

.pa-kpi{
  background:#fff; padding:12px 14px;
  display:flex; align-items:center; gap:10px;
  border-left:3px solid transparent;
  position:relative;
}
.pa-kpi--navy  { border-left-color:var(--navy); }
.pa-kpi--blue  { border-left-color:var(--blue); }
.pa-kpi--green { border-left-color:var(--green); }
.pa-kpi--red   { border-left-color:var(--red);   background:#fffafa; }
.pa-kpi--amber { border-left-color:#d97706; }
.pa-kpi--grey  { border-left-color:var(--grey); }

.pa-kpi__icon{
  width:32px; height:32px; flex-shrink:0;
  display:flex; align-items:center; justify-content:center;
  background:var(--surface);
}
.pa-kpi--navy  .pa-kpi__icon{ background:#e8edf8; color:var(--navy); }
.pa-kpi--blue  .pa-kpi__icon{ background:#e8edf8; color:var(--blue); }
.pa-kpi--green .pa-kpi__icon{ background:var(--green-bg); color:var(--green); }
.pa-kpi--red   .pa-kpi__icon{ background:var(--red-bg); color:var(--red); }
.pa-kpi--amber .pa-kpi__icon{ background:var(--amber-bg); color:#92400e; }
.pa-kpi--grey  .pa-kpi__icon{ background:var(--grey-bg); color:var(--grey); }

.pa-kpi__body{ flex:1; min-width:0; }
.pa-kpi__val{ font-size:22px; font-weight:700; color:var(--text); line-height:1.1; }
.pa-kpi--red .pa-kpi__val{ color:var(--red); }
.pa-kpi__label{ font-size:10px; color:var(--muted); text-transform:uppercase; letter-spacing:.3px; margin-top:1px; }
.pa-kpi__sub{ font-size:10px; color:var(--blue); text-decoration:none; display:block; margin-top:2px; }
.pa-kpi__sub:hover{ text-decoration:underline; }

/* Completion progress strip at bottom of KPI grid */
.pa-completion-strip{
  background:#fff; border:1px solid var(--border); border-top:none;
  padding:8px 14px; display:flex; align-items:center; gap:12px;
  margin-bottom:14px; font-size:11px;
}
.pa-completion-strip__label{ color:var(--sub); font-weight:600; white-space:nowrap; }
.pa-completion-strip__bar{
  flex:1; height:6px; background:#e0e5ed; position:relative;
}
.pa-completion-strip__fill{
  position:absolute; left:0; top:0; height:100%;
  background:var(--green); transition:width .4s ease;
}
.pa-completion-strip__pct{
  font-weight:700; color:var(--green); white-space:nowrap; min-width:40px; text-align:right;
}

/* ══ PIPELINE ═════════════════════════════════════════════════ */
.pa-pipe{
  display:flex; align-items:stretch;
  border:1px solid var(--border); background:var(--border);
  gap:1px; margin-bottom:14px;
}
.pa-pipe-stage{
  flex:1; background:#fff; padding:10px 8px; text-align:center;
}
.pa-pipe-stage__n{
  font-size:20px; font-weight:700; color:var(--text); line-height:1.1;
}
.pa-pipe-stage__lbl{
  font-size:9px; text-transform:uppercase; letter-spacing:.3px;
  color:var(--muted); margin-top:2px; line-height:1.3;
}
.pa-pipe-stage__dot{
  width:6px; height:6px; margin:5px auto 0; display:block;
}
/* stage colours — dots only */
.ps--pending .pa-pipe-stage__dot  { background:var(--grey); }
.ps--emp     .pa-pipe-stage__dot  { background:var(--blue); }
.ps--ret     .pa-pipe-stage__dot  { background:#d97706; }
.ps--esub    .pa-pipe-stage__dot  { background:#1d4ed8; }
.ps--sup     .pa-pipe-stage__dot  { background:#d97706; }
.ps--await   { background:#fffbf0; }
.ps--await   .pa-pipe-stage__n    { color:#92400e; }
.ps--await   .pa-pipe-stage__dot  { background:#d97706; }
.ps--done    { background:#f0f9f4; }
.ps--done    .pa-pipe-stage__n    { color:var(--green); }
.ps--done    .pa-pipe-stage__dot  { background:var(--green); }

/* ══ ACTION BANNER ════════════════════════════════════════════ */
.pa-banner{
  display:flex; align-items:center; gap:12px;
  background:var(--red-bg); border:1px solid #f5c6c6;
  border-left:3px solid var(--red);
  padding:10px 14px; margin-bottom:14px;
}
.pa-banner__icon{ color:var(--red); flex-shrink:0; }
.pa-banner__body{ flex:1; }
.pa-banner__title{ font-size:12px; font-weight:700; color:#7f1d1d; }
.pa-banner__sub{ font-size:11px; color:#9b1c1c; margin-top:1px; }
.pa-banner__btn{
  display:inline-flex; align-items:center; gap:5px;
  padding:5px 12px; background:var(--red); color:#fff;
  font-size:11px; font-weight:600; text-decoration:none; white-space:nowrap;
}
.pa-banner__btn:hover{ background:#b91c1c; }

/* ══ SECTIONS ═════════════════════════════════════════════════ */
.pa-sec{
  background:#fff; border:1px solid var(--border);
  margin-bottom:14px;
}
.pa-sec__hdr{
  background:var(--navy); color:#fff;
  padding:7px 14px; font-size:11px; font-weight:700;
  text-transform:uppercase; letter-spacing:.4px;
  display:flex; align-items:center; gap:8px;
}
.pa-sec__hdr svg{ flex-shrink:0; opacity:.8; }
.pa-sec__hdr-right{
  margin-left:auto; font-size:10px; font-weight:400;
  text-transform:none; letter-spacing:0; opacity:.75;
}
.pa-sec__hdr-right a{ color:#fff; text-decoration:none; }
.pa-sec__hdr-right a:hover{ text-decoration:underline; }
.pa-sec__body{ padding:12px 14px; }

/* Urgent variant */
.pa-sec--urgent .pa-sec__hdr{
  background:var(--red);
}

/* ══ LAYOUT ═══════════════════════════════════════════════════ */
.pa-cols{ display:grid; grid-template-columns:1fr 1fr; gap:14px; }
@media(max-width:900px){ .pa-cols{ grid-template-columns:1fr; } }

/* ══ TABLE ════════════════════════════════════════════════════ */
.pa-tbl{ width:100%; border-collapse:collapse; font-size:12px; }
.pa-tbl th{
  text-align:left; padding:6px 10px;
  border-bottom:1px solid var(--border);
  color:var(--sub); font-weight:600;
  font-size:10px; text-transform:uppercase; letter-spacing:.3px;
  background:var(--surface);
}
.pa-tbl td{ padding:6px 10px; border-bottom:1px solid #f5f7fa; color:var(--text); vertical-align:middle; }
.pa-tbl tr:last-child td{ border-bottom:none; }
.pa-tbl tr:hover td{ background:var(--surface); }
.pa-num{ text-align:right; font-variant-numeric:tabular-nums; }

/* ── Mini progress ── */
.pa-prog{ display:inline-flex; align-items:center; gap:6px; width:100%; }
.pa-prog__track{ flex:1; height:5px; background:#e0e5ed; }
.pa-prog__fill{ height:100%; background:var(--blue); }
.pa-prog__txt{ font-size:10px; font-weight:700; color:var(--sub); white-space:nowrap; min-width:32px; text-align:right; }

/* ── Status bars ── */
.pa-sbar{ display:flex; align-items:center; gap:8px; padding:4px 0; }
.pa-sbar__lbl{ font-size:11px; min-width:160px; color:var(--text); }
.pa-sbar__n{ font-size:11px; font-weight:700; min-width:28px; text-align:right; color:var(--text); }
.pa-sbar__track{ flex:1; height:7px; background:#e8edf8; }
.pa-sbar__fill{ height:100%; transition:width .4s ease; }
.pa-sbar__pct{ font-size:10px; color:var(--muted); min-width:38px; text-align:right; }

/* ── Badges ── */
.pa-badge{
  display:inline-block; padding:2px 7px;
  font-size:9px; font-weight:700;
  text-transform:uppercase; letter-spacing:.3px;
  border:1px solid transparent;
}
.pa-badge--pending    { background:var(--grey-bg); color:var(--grey); border-color:#d1d5db; }
.pa-badge--emp-prog   { background:#dbeafe; color:#1e40af; border-color:#bfdbfe; }
.pa-badge--emp-done   { background:#dbeafe; color:#1e40af; border-color:#93c5fd; }
.pa-badge--sup-prog   { background:var(--amber-bg); color:var(--amber); border-color:#fde68a; }
.pa-badge--completed  { background:#fef3c7; color:#92400e; border-color:#fde68a; }
.pa-badge--hr-reviewed{ background:var(--green-bg); color:var(--green); border-color:#a7f3d0; }
.pa-badge--returned   { background:#ffedd5; color:#c2410c; border-color:#fed7aa; }
.pa-badge--cancelled  { background:var(--red-bg); color:var(--red); border-color:#fca5a5; }

/* ── Action Required table ── */
.pa-ar-emp{ font-weight:600; font-size:12px; }
.pa-ar-code{ font-size:10px; color:var(--muted); }
.pa-ar-btn{
  display:inline-flex; padding:3px 9px;
  background:var(--navy); color:#fff;
  font-size:10px; font-weight:600; text-decoration:none;
}
.pa-ar-btn:hover{ background:var(--blue); }
.pa-ar-days--warn{ color:var(--red); font-weight:700; font-size:11px; }
.pa-ar-days--ok{ color:var(--muted); font-size:11px; }

/* ── Alert items ── */
.pa-alert-item{
  padding:8px 12px; margin-bottom:6px;
  border-left:3px solid var(--border); background:var(--surface);
}
.pa-alert-item:last-child{ margin-bottom:0; }
.pa-alert-item--critical{ border-left-color:var(--red); background:var(--red-bg); }
.pa-alert-item--high    { border-left-color:#d97706; background:var(--amber-bg); }
.pa-alert-item--medium  { border-left-color:var(--blue); background:#eff6ff; }
.pa-alert-item__title{ font-size:12px; font-weight:600; color:var(--text); margin-bottom:2px; }
.pa-alert-item__detail{ font-size:11px; color:var(--sub); }
.pa-no-alerts{ display:flex; align-items:center; gap:8px; padding:14px; color:var(--green); font-size:12px; font-weight:600; }
.pa-alert--error{ padding:10px 14px; background:var(--red-bg); color:var(--red); margin-bottom:12px; font-size:12px; }

/* ── Quick links ── */
.pa-links{ display:grid; grid-template-columns:1fr 1fr; gap:8px; }
.pa-link{
  display:flex; align-items:center; gap:8px;
  padding:9px 11px; background:var(--surface);
  border:1px solid var(--border);
  text-decoration:none; color:var(--text);
  font-size:11px; font-weight:600;
}
.pa-link:hover{ background:#e8edf8; border-color:var(--blue); color:var(--blue); }
.pa-link__dot{ width:4px; height:4px; background:var(--blue); flex-shrink:0; }
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<!-- ══ PAGE HEADER ═══════════════════════════════════════════ -->
<div class="pa-hdr">
    <div class="pa-hdr__badge">
        <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="vertical-align:middle;margin-right:4px;"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        Appraisal Dashboard
    </div>
    <div>
        <div class="pa-hdr__title">Performance Appraisal</div>
        <div class="pa-hdr__sub">HR overview — valid-contract staff only &middot; track progress, review completions</div>
    </div>
    <div class="pa-hdr__right">
        <div class="pa-sf">
            <label>Session:</label>
            <select onchange="filterBySession(this.value)">
                <asp:Literal ID="litSessionOptions" runat="server" />
            </select>
        </div>
        <a href="AppraisalSessions.aspx" class="pa-btn">
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
            Sessions
        </a>
    </div>
</div>

<!-- Records with no reviewer (nobody can ever review them until HR assigns one) -->
<asp:Literal ID="litUnassignedBanner" runat="server" />

<!-- ══ KPI GRID (4 × 2) ══════════════════════════════════════ -->
<div class="pa-kpi-grid">

    <div class="pa-kpi pa-kpi--navy">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiTotal" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Total Records</div>
        </div>
    </div>

    <div class="pa-kpi pa-kpi--red">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiNeedsHr" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Awaiting HR Review</div>
            <a class="pa-kpi__sub" href="AppraisalView.aspx?status=COMPLETED">Open review queue &rarr;</a>
        </div>
    </div>

    <div class="pa-kpi pa-kpi--green">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiHrReviewed" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">HR Reviewed &amp; Done</div>
        </div>
    </div>

    <div class="pa-kpi pa-kpi--red">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiOverdue" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Past Deadline</div>
        </div>
    </div>

    <div class="pa-kpi pa-kpi--amber">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiSupStage" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Supervisor Stage</div>
            <span style="font-size:9px;color:var(--muted);display:block;margin-top:1px;">Submitted + Reviewing</span>
        </div>
    </div>

    <div class="pa-kpi pa-kpi--blue">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiEmpStage" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Employee Stage</div>
            <span style="font-size:9px;color:var(--muted);display:block;margin-top:1px;">In Progress + Returned</span>
        </div>
    </div>

    <div class="pa-kpi pa-kpi--grey">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="8" y1="12" x2="16" y2="12"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiNotStarted" runat="server" Text="0" /></div>
            <div class="pa-kpi__label">Not Yet Started</div>
            <a class="pa-kpi__sub" href="AppraisalView.aspx?status=PENDING">View pending &rarr;</a>
        </div>
    </div>

    <div class="pa-kpi pa-kpi--navy">
        <div class="pa-kpi__icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
        </div>
        <div class="pa-kpi__body">
            <div class="pa-kpi__val"><asp:Literal ID="litKpiAvgScore" runat="server" Text="—" /></div>
            <div class="pa-kpi__label">Average Score</div>
            <span style="font-size:9px;color:var(--muted);display:block;margin-top:1px;">Completed appraisals</span>
        </div>
    </div>

</div>

<!-- Completion progress strip -->
<div class="pa-completion-strip">
    <span class="pa-completion-strip__label">Overall Completion (Supervisor Done / Total):</span>
    <div class="pa-completion-strip__bar" id="compBar">
        <div class="pa-completion-strip__fill" id="compFill" style="width:0%;"></div>
    </div>
    <span class="pa-completion-strip__pct"><asp:Literal ID="litKpiCompletionPct" runat="server" Text="0%" /></span>
</div>

<!-- ══ PIPELINE ═══════════════════════════════════════════════ -->
<div class="pa-pipe">
    <div class="pa-pipe-stage ps--pending">
        <div class="pa-pipe-stage__n"><asp:Literal ID="litPipeNotStarted" runat="server" Text="0" /></div>
        <div class="pa-pipe-stage__lbl">Not Started</div>
        <span class="pa-pipe-stage__dot"></span>
    </div>
    <div class="pa-pipe-stage ps--emp">
        <div class="pa-pipe-stage__n"><asp:Literal ID="litPipeEmpProg" runat="server" Text="0" /></div>
        <div class="pa-pipe-stage__lbl">Emp. In Progress</div>
        <span class="pa-pipe-stage__dot"></span>
    </div>
    <div class="pa-pipe-stage ps--ret">
        <div class="pa-pipe-stage__n"><asp:Literal ID="litPipeReturned" runat="server" Text="0" /></div>
        <div class="pa-pipe-stage__lbl">Returned</div>
        <span class="pa-pipe-stage__dot"></span>
    </div>
    <div class="pa-pipe-stage ps--esub">
        <div class="pa-pipe-stage__n"><asp:Literal ID="litPipeEmpSubmitted" runat="server" Text="0" /></div>
        <div class="pa-pipe-stage__lbl">Emp. Submitted</div>
        <span class="pa-pipe-stage__dot"></span>
    </div>
    <div class="pa-pipe-stage ps--sup">
        <div class="pa-pipe-stage__n"><asp:Literal ID="litPipeSupProg" runat="server" Text="0" /></div>
        <div class="pa-pipe-stage__lbl">Sup. Reviewing</div>
        <span class="pa-pipe-stage__dot"></span>
    </div>
    <div class="pa-pipe-stage ps--await">
        <div class="pa-pipe-stage__n"><asp:Literal ID="litPipeCompleted" runat="server" Text="0" /></div>
        <div class="pa-pipe-stage__lbl">Awaiting HR</div>
        <span class="pa-pipe-stage__dot"></span>
    </div>
    <div class="pa-pipe-stage ps--done">
        <div class="pa-pipe-stage__n"><asp:Literal ID="litPipeHrReviewed" runat="server" Text="0" /></div>
        <div class="pa-pipe-stage__lbl">HR Reviewed</div>
        <span class="pa-pipe-stage__dot"></span>
    </div>
</div>

<!-- ══ ACTION REQUIRED BANNER ════════════════════════════════ -->
<asp:Literal ID="litActionBanner" runat="server" />

<!-- Action Required table -->
<div class="pa-sec pa-sec--urgent">
    <div class="pa-sec__hdr">
        <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
        Action Required — Awaiting HR Review
        <span class="pa-sec__hdr-right"><a href="AppraisalView.aspx?status=COMPLETED">View all &rarr;</a></span>
    </div>
    <div style="padding:0;">
        <table class="pa-tbl">
            <thead>
                <tr>
                    <th>Employee</th>
                    <th>Department</th>
                    <th>Session</th>
                    <th>Supervisor</th>
                    <th class="pa-num">Score</th>
                    <th>Grade</th>
                    <th>Waiting</th>
                    <th style="text-align:center;">Action</th>
                </tr>
            </thead>
            <tbody><asp:Literal ID="litActionRequired" runat="server" /></tbody>
        </table>
    </div>
</div>

<!-- ══ TWO-COLUMN LAYOUT ═════════════════════════════════════ -->
<div class="pa-cols">

    <!-- LEFT -->
    <div>
        <!-- By Category -->
        <div class="pa-sec">
            <div class="pa-sec__hdr">
                <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
                By Staff Category
                <span class="pa-sec__hdr-right">Valid-contract staff only</span>
            </div>
            <div style="padding:0;">
                <table class="pa-tbl">
                    <thead>
                        <tr>
                            <th>Category</th>
                            <th class="pa-num">Total</th>
                            <th class="pa-num">Done</th>
                            <th class="pa-num">Await HR</th>
                            <th class="pa-num">Active</th>
                            <th class="pa-num">Pending</th>
                            <th>Progress</th>
                            <th class="pa-num">Avg</th>
                        </tr>
                    </thead>
                    <tbody><asp:Literal ID="litCategoryRows" runat="server" /></tbody>
                </table>
            </div>
        </div>

        <!-- By Department -->
        <div class="pa-sec">
            <div class="pa-sec__hdr">
                <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>
                By Department
                <span class="pa-sec__hdr-right">Top 20</span>
            </div>
            <div style="padding:0;">
                <table class="pa-tbl">
                    <thead>
                        <tr>
                            <th>Department</th>
                            <th class="pa-num">Total</th>
                            <th class="pa-num">Complete</th>
                            <th class="pa-num">Outstanding</th>
                            <th>Progress</th>
                            <th class="pa-num">Avg</th>
                        </tr>
                    </thead>
                    <tbody><asp:Literal ID="litDeptRows" runat="server" /></tbody>
                </table>
            </div>
        </div>
    </div>

    <!-- RIGHT -->
    <div>
        <!-- Status Distribution -->
        <div class="pa-sec">
            <div class="pa-sec__hdr">
                <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>
                Status Distribution
            </div>
            <div class="pa-sec__body">
                <asp:Literal ID="litStatusBars" runat="server" />
            </div>
        </div>

        <!-- Overdue -->
        <div class="pa-sec">
            <div class="pa-sec__hdr">
                <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
                Overdue Sessions
            </div>
            <div class="pa-sec__body">
                <asp:Literal ID="litAlerts" runat="server" />
            </div>
        </div>

        <!-- Quick Links -->
        <div class="pa-sec">
            <div class="pa-sec__hdr">
                <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg>
                Quick Links
            </div>
            <div class="pa-sec__body">
                <div class="pa-links">
                    <a href="AppraisalSessions.aspx" class="pa-link">
                        <span class="pa-link__dot"></span> Manage Sessions
                    </a>
                    <a href="AppraisalView.aspx?status=COMPLETED" class="pa-link">
                        <span class="pa-link__dot"></span> HR Review Queue
                    </a>
                    <a href="AppraisalView.aspx" class="pa-link">
                        <span class="pa-link__dot"></span> All Records
                    </a>
                    <a href="AppraisalReports.aspx" class="pa-link">
                        <span class="pa-link__dot"></span> Reports
                    </a>
                    <a href="HREmployees.aspx" class="pa-link">
                        <span class="pa-link__dot"></span> Employee Profiles
                    </a>
                    <a href="AppraisalView.aspx?status=PENDING" class="pa-link">
                        <span class="pa-link__dot"></span> Not Started
                    </a>
                </div>
            </div>
        </div>
    </div>

</div>

<!-- ══ RECENT ACTIVITY ═══════════════════════════════════════ -->
<div class="pa-sec">
    <div class="pa-sec__hdr">
        <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
        Recent Activity
        <span class="pa-sec__hdr-right">Last 15 status changes</span>
    </div>
    <div style="padding:0;">
        <table class="pa-tbl">
            <thead>
                <tr>
                    <th>Employee</th>
                    <th>Department</th>
                    <th>Session</th>
                    <th>Status</th>
                    <th class="pa-num">Score</th>
                    <th>Updated</th>
                    <th style="text-align:center;">View</th>
                </tr>
            </thead>
            <tbody><asp:Literal ID="litRecentRows" runat="server" /></tbody>
        </table>
    </div>
</div>

<script>
function filterBySession(v){
    window.location.href = 'AppraisalDashboard.aspx' + (v && v!=='0' ? '?sid='+v : '');
}
// Animate completion progress bar
(function(){
    var fill = document.getElementById('compFill');
    var pctEl = document.querySelector('.pa-completion-strip__pct');
    if (!fill || !pctEl) return;
    var pct = parseFloat(pctEl.textContent) || 0;
    setTimeout(function(){ fill.style.width = Math.min(pct,100) + '%'; }, 120);
})();
</script>

</asp:Content>
