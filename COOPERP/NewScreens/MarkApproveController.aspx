<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="MarkApproveController.aspx.cs" Inherits="COOPERP_NewScreens_MarkApproveController" Title="Approve Marks (Dean) - Campus Dynamics" %>
<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <link href="css/stageconsole.css?v=20260920b" rel="stylesheet" type="text/css" />
</asp:Content>
<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="sc-wrap">
  <div class="sc-head">
    <div class="sc-head__top">
      <div>
        <div class="sc-title">Approve Marks &middot; Dean / Faculty Board</div>
        <div class="sc-flow">Advance marks from <b id="sc-from-label">…</b> &rarr; <b id="sc-to-label">…</b> &middot; once approved, the HOD can no longer change them.</div>
      </div>
      <span class="sc-chip" id="sc-role">…</span>
    </div>
    <div class="sc-scopebar" id="sc-scope">Resolving scope…</div>
  </div>

  <div class="sc-funnel">
    <div class="sc-st"><div class="sc-st__v" id="sc-not">0</div><div class="sc-st__l">Not entered</div></div>
    <div class="sc-st"><div class="sc-st__v" id="sc-entered">0</div><div class="sc-st__l">Entered</div></div>
    <div class="sc-st"><div class="sc-st__v" id="sc-captured">0</div><div class="sc-st__l">Captured</div></div>
    <div class="sc-st"><div class="sc-st__v" id="sc-approved">0</div><div class="sc-st__l">Approved</div></div>
    <div class="sc-st sc-st--pub"><div class="sc-st__v" id="sc-published">0</div><div class="sc-st__l">Published</div></div>
    <div class="sc-st sc-st--act"><div class="sc-st__v" id="sc-actionable">0</div><div class="sc-st__l">Awaiting <span class="sc-to-label">action</span></div></div>
  </div>

  <div class="sc-actionbar">
    <button type="button" class="sc-btn sc-btn--lg" id="sc-launch" onclick="SC.openWizard()">
      <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
      Approve marks&hellip;</button>
    <span class="sc-actionbar__hint">Pick captured marks by programme / year / semester, preview, then approve.</span>
    <span class="sc-cantact" id="sc-cantact" style="display:none;">Read-only — your role cannot perform this stage.</span>
  </div>

  <div><span class="sc-tabs">
    <button type="button" id="sc-tab-queue" class="sc-tab active" onclick="SC.tab('queue')">Queue</button>
    <button type="button" id="sc-tab-hist" class="sc-tab" onclick="SC.tab('hist')">Session history</button>
  </span></div>

  <div id="sc-view-queue">
    <div class="sc-filters">
      <div class="sc-fg"><label>Year</label><select id="sc-year" class="sc-sel"></select></div>
      <div class="sc-fg"><label>Semester</label><select id="sc-sem" class="sc-sel"><option value="">All</option><option>1</option><option>2</option><option>3</option></select></div>
      <div class="sc-fg"><label>Programme</label><select id="sc-prog" class="sc-sel"></select></div>
      <div class="sc-fg"><label>Search</label><input id="sc-q" class="sc-in" placeholder="student no / reg no / course" /></div>
      <div class="sc-fg"><label>&nbsp;</label><button type="button" class="sc-btn sc-btn--navy" onclick="SC.apply()">Apply</button></div>
      <div class="sc-fg"><label>&nbsp;</label><button type="button" class="sc-btn sc-btn--ghost" onclick="SC.reset()">Reset</button></div>
    </div>

    <div class="sc-card">
      <div class="sc-toolbar">
        <strong style="font-size:11px;color:#05275C;">Marks awaiting approval</strong>
        <span style="margin-left:auto"></span>
        <button type="button" class="sc-btn sc-btn--ghost" id="sc-failed-toggle" onclick="SC.toggleFailed()">&#9873; Show failed (F) only</button>
        <button type="button" class="sc-btn sc-btn--navy" id="sc-advance-btn" onclick="SC.advanceSelected()">&#10003; Advance selected</button>
        <button type="button" class="sc-btn sc-btn--ghost" onclick="SC.returnSelected()">&#8634; Send selected back</button>
      </div>
      <div class="sc-tw">
        <table class="sc-tbl">
          <thead><tr>
            <th style="width:28px;"><input type="checkbox" onclick="SC.selectAll(this)"></th>
            <th>Student No / Name</th><th>Course</th><th>Lecturer</th><th>Programme</th><th class="sc-c">Year/Sem</th>
            <th class="sc-c">CW</th><th class="sc-c">Exam</th><th class="sc-c">Total</th><th class="sc-c">Grade</th><th>Tracker</th>
          </tr></thead>
          <tbody id="sc-body"><tr><td colspan="11" class="sc-empty">Loading&hellip;</td></tr></tbody>
        </table>
      </div>
      <div class="sc-pager" id="sc-pager"></div>
    </div>
  </div>

  <div id="sc-view-hist" style="display:none">
    <div class="sc-card"><div class="sc-tw"><table class="sc-tbl">
      <thead><tr><th>Session</th><th>Status</th><th>By</th><th>Scope</th><th class="sc-c">Affected</th><th>When</th><th></th></tr></thead>
      <tbody id="sc-hist-body"><tr><td colspan="7" class="sc-empty">Loading&hellip;</td></tr></tbody>
    </table></div></div>
    <div style="font-size:10px;color:#94a3b8;padding:6px 2px;">Click any session row to view full details &amp; performance snapshot.</div>
  </div>
</div>

<!-- Wizard modal: params → preview → commit -->
<div class="sc-modal" id="sc-modal">
  <div class="sc-modal__box">
    <div class="sc-modal__hd"><span>New approval session</span><button type="button" class="sc-modal__x" onclick="SC.closeWizard()">&times;</button></div>
    <div class="sc-modal__bd">
      <div id="sc-wiz-params">
        <p class="sc-wiz-intro">Select what to approve. Leave a field as <em>All</em> to include everything in your scope, or tick &ldquo;Everything in my scope&rdquo;.</p>
        <div class="sc-wiz-grid">
          <div class="sc-fg"><label>Programme</label><select id="sc-p-prog" class="sc-sel" style="width:100%"></select></div>
          <div class="sc-fg"><label>Academic Year</label><select id="sc-p-year" class="sc-sel" style="width:100%"></select></div>
          <div class="sc-fg"><label>Semester</label><select id="sc-p-sem" class="sc-sel" style="width:100%"><option value="">All</option><option>1</option><option>2</option><option>3</option></select></div>
          <div class="sc-fg"><label>Year of Study</label><select id="sc-p-yos" class="sc-sel" style="width:100%"><option value="">All</option><option>1</option><option>2</option><option>3</option><option>4</option><option>5</option></select></div>
          <div class="sc-fg"><label>Failures (below 50)</label><select id="sc-p-fail" class="sc-sel" style="width:100%"><option value="INCLUDE">Include failures</option><option value="EXCLUDE">Exclude failures &mdash; do not capture F</option></select></div>
        </div>
        <label class="sc-allchk"><input type="checkbox" id="sc-p-all" /> Everything in my scope (ignore the filters above)</label>
        <div class="sc-fg" style="margin-top:10px;"><label>Notes (optional)</label><input id="sc-p-notes" class="sc-in" style="width:100%" /></div>
      </div>
      <div id="sc-wiz-preview" style="display:none"></div>
    </div>
    <div class="sc-modal__ft">
      <button type="button" class="sc-btn sc-btn--ghost" onclick="SC.closeWizard()">Cancel</button>
      <button type="button" class="sc-btn sc-btn--ghost" id="sc-btn-back" style="display:none" onclick="SC.wizBack()">&larr; Back</button>
      <button type="button" class="sc-btn" id="sc-btn-preview" onclick="SC.doPreview()">Preview &rarr;</button>
      <button type="button" class="sc-btn" id="sc-commit-btn" style="display:none" onclick="SC.commit()">Commit</button>
    </div>
  </div>
</div>

<!-- Session detail modal -->
<div class="sc-modal" id="sc-detail">
  <div class="sc-modal__box">
    <div class="sc-modal__hd"><span>Session details</span><button type="button" class="sc-modal__x" onclick="SC.closeDetail()">&times;</button></div>
    <div class="sc-modal__bd" id="sc-detail-body"></div>
    <div class="sc-modal__ft">
      <a class="sc-btn" id="sc-detail-csv" style="display:none" href="#">Export CSV</a>
      <button type="button" class="sc-btn sc-btn--ghost" onclick="SC.closeDetail()">Close</button>
    </div>
  </div>
</div>

<script src="js/stageconsole.js?v=20260920b" type="text/javascript"></script>
</asp:Content>
