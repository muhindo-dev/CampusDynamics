<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="StudentRearrangeManage.aspx.cs" Inherits="COOPERP_NewScreens_StudentRearrangeManage" Title="Rearrange Student Record - Campus Dynamics" %>
<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <link href="css/rearrange.css?v=20260921d" rel="stylesheet" type="text/css" />
</asp:Content>
<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="rx-wrap">

  <!-- Who you are acting as. Permanent, not dismissible. -->
  <div class="rx-actorbar">
    <span class="rx-actorbar__who" id="rx-actor"><%= Server.HtmlEncode(StudentRearrangeService.ActorName()) %></span>
    <span class="rx-actorbar__role" id="rx-role"><%= Server.HtmlEncode(StudentRearrangeService.ActorRole()) %></span>
    <span class="rx-actorbar__note">Every change on this page is recorded under your account and can be reversed.</span>
  </div>

  <div class="rx-head">
    <h1 class="rx-title">Rearrange Student Record</h1>
    <div class="rx-sub">Move courses between semesters, correct marks, add or remove registrations. Nothing is written until you review and save.</div>
  </div>

  <!-- ══ Step 1: the gate. No record is loaded until all three are supplied. ══ -->
  <div id="rx-gate" class="rx-card rx-gate">
    <div class="rx-card__hd">Open a rearrangement session</div>
    <div class="rx-card__bd">
      <div style="margin-bottom:12px; position:relative; max-width:460px">
        <label class="rx-lbl" for="rx-regno">Student</label>
        <input type="text" id="rx-regno" class="rx-in" autocomplete="off" role="combobox"
               aria-autocomplete="list" aria-expanded="false" aria-controls="rx-stu-list"
               placeholder="Registration number, entry number, or name" />
        <div class="rx-sugg" id="rx-stu-list" role="listbox"></div>
        <div class="rx-picked" id="rx-picked" style="display:none"></div>
      </div>

      <div>
        <label class="rx-lbl" for="rx-reason">Reason for this rearrangement</label>
        <textarea id="rx-reason" class="rx-ta" placeholder="Explain why this student's record is being rearranged. This is recorded permanently against your name and shown on every change you make in this sitting."></textarea>
        <div class="rx-hint" id="rx-reason-hint">At least 30 characters.</div>
        <div class="rx-chips" id="rx-reason-chips" aria-label="Common reasons"></div>
      </div>

      <div class="rx-gate__ack">
        <input type="checkbox" id="rx-ack" />
        <label for="rx-ack" id="rx-ack-label">I understand that this action will be recorded under my name, with my user account, date, time and IP address, and that it can be audited and reversed.</label>
      </div>

      <div style="margin-top:14px; display:flex; gap:8px; align-items:center; flex-wrap:wrap">
        <button type="button" class="rx-btn" id="rx-open" disabled="disabled">Load student record</button>
        <span class="rx-hint" id="rx-gate-msg"></span>
      </div>
    </div>
  </div>

  <!-- ══ Step 2: the workspace ══ -->
  <div id="rx-workspace" style="display:none">

    <div class="rx-card">
      <div class="rx-card__bd">
        <div class="rx-student" id="rx-student"></div>
      </div>
    </div>

    <div class="rx-card">
      <div class="rx-card__bd" style="display:flex; gap:14px; align-items:center; flex-wrap:wrap">
        <div class="rx-legend">
          <span><i class="rx-dot-moved"></i>Moved</span>
          <span><i class="rx-dot-added"></i>Added</span>
          <span><i class="rx-dot-deleted"></i>Removed</span>
          <span><i class="rx-dot-mark"></i>Mark changed</span>
        </div>
        <span class="rx-pending rx-pending--zero" id="rx-pending">No pending changes</span>
      </div>
      <div class="rx-card__bd" style="border-top:1px solid #eef2f6; display:flex; gap:8px; flex-wrap:wrap">
        <button type="button" class="rx-btn" id="rx-save" disabled="disabled">Review &amp; save…</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-discard" disabled="disabled">Discard pending changes</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-regsem">Register a new semester…</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-expand">Expand all</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-reload">Reload from database</button>
      </div>
    </div>

    <div id="rx-groups"></div>
  </div>

  <div id="rx-boot" class="rx-state" style="display:none">
    <div class="rx-spin"></div>Loading the student record…
  </div>
</div>

<!-- ══ Review & confirm ══ -->
<div class="rx-modal" id="rx-review-modal">
  <div class="rx-modal__box">
    <div class="rx-modal__hd"><span>Review changes before saving</span>
      <button type="button" class="rx-modal__x" data-close="rx-review-modal">&times;</button></div>
    <div class="rx-modal__bd" id="rx-review-body"></div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-review-modal">Back</button>
      <button type="button" class="rx-btn" id="rx-confirm">Confirm and save</button>
    </div>
  </div>
</div>

<!-- ══ Add a course ══ -->
<div class="rx-modal" id="rx-add-modal">
  <div class="rx-modal__box" style="max-width:600px">
    <div class="rx-modal__hd"><span id="rx-add-title">Add a course</span>
      <button type="button" class="rx-modal__x" data-close="rx-add-modal">&times;</button></div>
    <div class="rx-modal__bd">
      <label class="rx-lbl" for="rx-add-q">Search the course catalogue</label>
      <input type="text" id="rx-add-q" class="rx-in" placeholder="Course code or title" autocomplete="off" />
      <div class="rx-hint">Courses on this student's programme curriculum are listed first.</div>
      <div id="rx-add-results" style="margin-top:10px; max-height:280px; overflow-y:auto"></div>
      <div style="margin-top:12px">
        <label class="rx-lbl" for="rx-add-reason">Reason</label>
        <input type="text" id="rx-add-reason" class="rx-in" placeholder="Why is this course being added?" autocomplete="off" />
        <div class="rx-chips" id="rx-add-chips"></div>
      </div>
    </div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-add-modal">Cancel</button>
    </div>
  </div>
</div>

<!-- ══ Register a semester ══ -->
<div class="rx-modal" id="rx-regsem-modal">
  <div class="rx-modal__box" style="max-width:560px">
    <div class="rx-modal__hd"><span>Register the student into a semester</span>
      <button type="button" class="rx-modal__x" data-close="rx-regsem-modal">&times;</button></div>
    <div class="rx-modal__bd">
      <div style="display:flex; gap:10px; flex-wrap:wrap">
        <div style="flex:1 1 150px"><label class="rx-lbl" for="rx-rs-acad">Academic year</label>
          <select id="rx-rs-acad" class="rx-sel"></select></div>
        <div style="flex:0 0 110px"><label class="rx-lbl" for="rx-rs-year">Year of study</label>
          <select id="rx-rs-year" class="rx-sel"></select></div>
        <div style="flex:0 0 110px"><label class="rx-lbl" for="rx-rs-sem">Semester</label>
          <select id="rx-rs-sem" class="rx-sel"></select></div>
      </div>
      <div class="rx-check" id="rx-rs-check"></div>
      <div style="margin-top:12px">
        <label class="rx-lbl" for="rx-rs-reason">Reason</label>
        <input type="text" id="rx-rs-reason" class="rx-in" placeholder="Why is this semester being registered?" autocomplete="off" />
        <div class="rx-chips" id="rx-rs-chips"></div>
      </div>
      <div class="rx-gate__ack" style="margin-top:12px; background:#fff8e1; border-color:#ffe08a; border-left-color:#e65100">
        <input type="checkbox" id="rx-rs-bill" />
        <label for="rx-rs-bill"><b>Create fee billing for this semester.</b><br />
          Leave this off unless the student should be billed. If ticked, the standard billing
          routine runs inside the same transaction, and the choice is recorded in the log either way.</label>
      </div>
    </div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-regsem-modal">Cancel</button>
      <button type="button" class="rx-btn" id="rx-rs-add">Add to pending changes</button>
    </div>
  </div>
</div>

<!-- ══ Reason / override prompt ══ -->
<div class="rx-modal" id="rx-reason-modal">
  <div class="rx-modal__box" style="max-width:560px">
    <div class="rx-modal__hd"><span id="rx-reason-title">Reason required</span>
      <button type="button" class="rx-modal__x" data-close="rx-reason-modal">&times;</button></div>
    <div class="rx-modal__bd">
      <div id="rx-reason-context"></div>
      <label class="rx-lbl" for="rx-reason-text" style="margin-top:10px">Typed reason</label>
      <textarea id="rx-reason-text" class="rx-ta" style="min-height:70px"></textarea>
      <div class="rx-hint" id="rx-reason-hint2"></div>
      <div class="rx-chips" id="rx-prompt-chips"></div>
    </div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-reason-modal">Cancel</button>
      <button type="button" class="rx-btn" id="rx-reason-ok">Confirm</button>
    </div>
  </div>
</div>

<script src="js/rearrange-manage.js?v=20260921d" type="text/javascript"></script>
</asp:Content>
