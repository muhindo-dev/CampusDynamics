<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="StudentRearrangeDashboard.aspx.cs" Inherits="COOPERP_NewScreens_StudentRearrangeDashboard" Title="Rearrangement Dashboard - Campus Dynamics" %>
<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <link href="css/rearrange.css?v=20260921b" rel="stylesheet" type="text/css" />
</asp:Content>
<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="rx-wrap">

  <div class="rx-actorbar">
    <span class="rx-actorbar__who" id="rx-actor">…</span>
    <span class="rx-actorbar__role" id="rx-role">…</span>
    <span class="rx-actorbar__note">Refused and blocked attempts are listed below — often more telling than the successful ones.</span>
  </div>

  <div class="rx-head" style="display:flex;align-items:flex-end;gap:14px;flex-wrap:wrap">
    <div style="flex:1 1 320px">
      <h1 class="rx-title">Rearrangement Dashboard</h1>
      <div class="rx-sub">Who changed which student's record, when, and why.</div>
    </div>
    <div style="flex:0 0 auto">
      <label class="rx-lbl" for="rx-days">Period</label>
      <select id="rx-days" class="rx-sel" style="width:170px">
        <option value="7">Last 7 days</option>
        <option value="30" selected="selected">Last 30 days</option>
        <option value="90">Last 90 days</option>
        <option value="365">Last 12 months</option>
      </select>
    </div>
  </div>

  <div class="rx-stats" id="rx-stats"></div>

  <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:14px">
    <div class="rx-card">
      <div class="rx-card__hd">By operation</div>
      <div class="rx-tw"><table class="rx-tbl">
        <thead><tr><th>Operation</th><th style="text-align:right">Count</th></tr></thead>
        <tbody id="rx-byop"><tr><td colspan="2" class="rx-state">Loading…</td></tr></tbody>
      </table></div>
    </div>
    <div class="rx-card">
      <div class="rx-card__hd">By actor</div>
      <div class="rx-tw"><table class="rx-tbl">
        <thead><tr><th>Actor</th><th>Role</th><th style="text-align:right">Count</th></tr></thead>
        <tbody id="rx-byactor"><tr><td colspan="3" class="rx-state">Loading…</td></tr></tbody>
      </table></div>
    </div>
  </div>

  <div class="rx-card">
    <div class="rx-card__hd">Recent activity</div>
    <div class="rx-tw"><table class="rx-tbl">
      <thead><tr><th>When</th><th>Student</th><th>Operation</th><th>Course</th><th>Actor</th><th>Reason</th><th></th></tr></thead>
      <tbody id="rx-recent"><tr><td colspan="7" class="rx-state">Loading…</td></tr></tbody>
    </table></div>
  </div>

  <div class="rx-card">
    <div class="rx-card__hd">Recent sessions</div>
    <div class="rx-tw"><table class="rx-tbl">
      <thead><tr><th>Session</th><th>Student</th><th>Officer</th><th>Opened</th><th>Reason</th><th style="text-align:right">Changes</th><th>Status</th></tr></thead>
      <tbody id="rx-sessions"><tr><td colspan="7" class="rx-state">Loading…</td></tr></tbody>
    </table></div>
  </div>

  <div class="rx-card">
    <div class="rx-card__hd">Refused and blocked attempts</div>
    <div class="rx-tw"><table class="rx-tbl">
      <thead><tr><th>When</th><th>Who</th><th>Student</th><th>Action</th><th>Outcome</th><th>Detail</th></tr></thead>
      <tbody id="rx-blocked"><tr><td colspan="6" class="rx-state">Loading…</td></tr></tbody>
    </table></div>
  </div>
</div>

<script src="js/rearrange-dashboard.js?v=20260921b" type="text/javascript"></script>
</asp:Content>
