<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="StudentRearrangeLogs.aspx.cs" Inherits="COOPERP_NewScreens_StudentRearrangeLogs" Title="Rearrangement Logs - Campus Dynamics" %>
<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <link href="css/rearrange.css?v=20260921b" rel="stylesheet" type="text/css" />
</asp:Content>
<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="rx-wrap">

  <div class="rx-actorbar">
    <span class="rx-actorbar__who" id="rx-actor">…</span>
    <span class="rx-actorbar__role" id="rx-role">…</span>
    <span class="rx-actorbar__note">Log entries are append-only. A correction is added as a new entry; nothing is ever rewritten.</span>
  </div>

  <div class="rx-head">
    <h1 class="rx-title">Rearrangement Logs</h1>
    <div class="rx-sub">Every change this module has made, with a full before and after, and the option to reverse it.</div>
  </div>

  <div class="rx-card">
    <div class="rx-card__hd">Filters</div>
    <div class="rx-card__bd">
      <div class="rx-filters">
        <div class="rx-fg rx-fg--sm"><label class="rx-lbl" for="f-from">From</label><input type="date" id="f-from" class="rx-in" /></div>
        <div class="rx-fg rx-fg--sm"><label class="rx-lbl" for="f-to">To</label><input type="date" id="f-to" class="rx-in" /></div>
        <div class="rx-fg"><label class="rx-lbl" for="f-actor">Actor</label><select id="f-actor" class="rx-sel"><option value="">Anyone</option></select></div>
        <div class="rx-fg"><label class="rx-lbl" for="f-role">Role</label><select id="f-role" class="rx-sel"><option value="">Any role</option></select></div>
        <div class="rx-fg"><label class="rx-lbl" for="f-regno">Student number</label><input type="text" id="f-regno" class="rx-in" placeholder="MRU…" /></div>
        <div class="rx-fg"><label class="rx-lbl" for="f-op">Operation</label><select id="f-op" class="rx-sel"><option value="">All</option></select></div>
        <div class="rx-fg rx-fg--sm"><label class="rx-lbl" for="f-rev">Reversed</label>
          <select id="f-rev" class="rx-sel"><option value="">Either</option><option value="1">Reversed</option><option value="0">Not reversed</option></select></div>
        <div class="rx-fg"><label class="rx-lbl" for="f-q">Search reasons</label><input type="text" id="f-q" class="rx-in" placeholder="text inside the reason" /></div>
        <div class="rx-fg rx-fg--sm"><label class="rx-lbl">&nbsp;</label><button type="button" class="rx-btn" id="f-apply">Apply</button></div>
        <div class="rx-fg rx-fg--sm"><label class="rx-lbl">&nbsp;</label><button type="button" class="rx-btn rx-btn--ghost" id="f-reset">Reset</button></div>
        <div class="rx-fg rx-fg--sm"><label class="rx-lbl">&nbsp;</label><a class="rx-btn rx-btn--ghost" id="f-csv" href="#">Export CSV</a></div>
      </div>
    </div>
  </div>

  <div class="rx-card">
    <div class="rx-card__hd"><span id="rx-count">…</span></div>
    <div class="rx-tw">
      <table class="rx-tbl">
        <thead><tr>
          <th>#</th><th>When</th><th>Student</th><th>Operation</th><th>Course</th>
          <th>Actor</th><th>Reason</th><th>State</th><th></th>
        </tr></thead>
        <tbody id="rx-body"><tr><td colspan="9" class="rx-state">Loading…</td></tr></tbody>
      </table>
    </div>
    <div class="rx-card__bd" id="rx-pager" style="display:flex;gap:8px;align-items:center;border-top:1px solid #eef2f6"></div>
  </div>
</div>

<div class="rx-modal" id="rx-detail-modal">
  <div class="rx-modal__box">
    <div class="rx-modal__hd"><span>Log entry</span>
      <button type="button" class="rx-modal__x" data-close="rx-detail-modal">&times;</button></div>
    <div class="rx-modal__bd" id="rx-detail-body"></div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-detail-modal">Close</button>
      <button type="button" class="rx-btn rx-btn--ghost" id="rx-rev-batch" style="display:none">Reverse the whole session…</button>
      <button type="button" class="rx-btn rx-btn--danger" id="rx-rev-one" style="display:none">Reverse this change…</button>
    </div>
  </div>
</div>

<div class="rx-modal" id="rx-reason-modal">
  <div class="rx-modal__box" style="max-width:560px">
    <div class="rx-modal__hd"><span id="rx-reason-title">Reason for reversal</span>
      <button type="button" class="rx-modal__x" data-close="rx-reason-modal">&times;</button></div>
    <div class="rx-modal__bd">
      <div id="rx-reason-context"></div>
      <label class="rx-lbl" for="rx-reason-text" style="margin-top:10px">Typed reason</label>
      <textarea id="rx-reason-text" class="rx-ta" style="min-height:70px"></textarea>
      <div class="rx-hint" id="rx-reason-hint2"></div>
    </div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-reason-modal">Cancel</button>
      <button type="button" class="rx-btn rx-btn--danger" id="rx-reason-ok">Reverse</button>
    </div>
  </div>
</div>

<script src="js/rearrange-logs.js?v=20260921b" type="text/javascript"></script>
</asp:Content>
