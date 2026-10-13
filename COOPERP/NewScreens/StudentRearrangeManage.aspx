<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="StudentRearrangeManage.aspx.cs" Inherits="COOPERP_NewScreens_StudentRearrangeManage" Title="Rearrange Student Record - Campus Dynamics" %>
<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <link href="css/rearrange.css?v=20261003b" rel="stylesheet" type="text/css" />
    <style>
    /* ── Auto-arrange ── */
    .rx-btn--auto{display:inline-flex;align-items:center;gap:6px;background:#0b5c3a;border-color:#0b5c3a;color:#fff;}
    .rx-btn--auto:hover{background:#09492e;}
    .rx-btn--auto svg{flex:0 0 auto;}
    .rx-btn--auto em{font-style:normal;background:rgba(255,255,255,.22);border-radius:9px;padding:0 7px;font-size:11px;font-weight:700;}
    .rx-btn--auto[disabled]{background:#9aa5b1;border-color:#9aa5b1;}
    .rx-aa .rx-modal__box{max-width:980px;width:96%;}
    .rx-aa__sum{display:flex;gap:8px;flex-wrap:wrap;margin-bottom:10px;}
    .rx-aa__tile{flex:1 1 150px;border:1px solid #e0e5ed;border-left:3px solid #174DA4;border-radius:4px;padding:7px 10px;background:#fff;}
    .rx-aa__tile b{display:block;font-size:19px;color:#05275C;line-height:1.1;}
    .rx-aa__tile span{font-size:10.5px;text-transform:uppercase;letter-spacing:.4px;color:#8a94a6;font-weight:700;}
    .rx-aa__tile--ok{border-left-color:#0b5c3a;} .rx-aa__tile--warn{border-left-color:#d97706;} .rx-aa__tile--del{border-left-color:#8c2019;}
    .rx-aa__sec{margin:12px 0 4px;font-weight:700;color:#05275C;font-size:12.5px;display:flex;align-items:center;gap:8px;}
    .rx-aa__sec small{font-weight:600;color:#4b5563;}
    .rx-aa__sec a{margin-left:auto;font-size:11px;font-weight:600;color:#174DA4;cursor:pointer;}
    .rx-aa__row{display:grid;grid-template-columns:22px minmax(0,1.6fr) minmax(0,1.3fr) minmax(0,1fr);gap:8px;align-items:center;
                padding:6px 8px;border:1px solid #eef2f6;border-radius:4px;margin-bottom:4px;background:#fff;font-size:12px;}
    .rx-aa__row.is-off{opacity:.5;}
    .rx-aa__row b{color:#05275C;}
    .rx-aa__row .rx-aa__t{color:#4b5563;font-size:11px;display:block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
    .rx-aa__path{font-variant-numeric:tabular-nums;white-space:nowrap;}
    .rx-aa__path i{font-style:normal;color:#174DA4;font-weight:700;margin:0 4px;}
    .rx-aa__why{font-size:11px;color:#4b5563;}
    .rx-aa__tag{display:inline-block;font-size:9.5px;font-weight:700;padding:1px 6px;border-radius:9px;margin-left:4px;background:#eef3ff;color:#174DA4;}
    .rx-aa__tag--lock{background:#fff8e1;color:#92400e;} .rx-aa__tag--del{background:#fdecea;color:#8c2019;}
    .rx-aa__note{font-size:11.5px;color:#4b5563;border:1px dashed #d6dde8;border-radius:4px;padding:6px 9px;margin-bottom:4px;background:#fafbfd;}
    .rx-aa__empty{padding:22px;text-align:center;color:#0b5c3a;font-weight:600;}
    /* ── Duplicates: one colour and letter per group, on the right edge (the left edge is the pending-change colour) ── */
    .rx-course.rx-dup{box-shadow:inset -4px 0 0 var(--dup);}
    .rx-course.rx-dup .rx-course__hd{background-image:linear-gradient(90deg,transparent 70%,color-mix(in srgb,var(--dup) 9%,transparent));}
    .rx-dupchip{display:inline-flex;align-items:center;gap:3px;border:0;border-radius:9px;padding:1px 7px 1px 3px;cursor:pointer;
                font:700 10px/1.5 inherit;color:#fff;background:var(--dup);flex:0 0 auto;font-variant-numeric:tabular-nums;}
    .rx-dupchip b{display:inline-block;min-width:14px;text-align:center;background:rgba(255,255,255,.28);border-radius:7px;padding:0 3px;}
    .rx-dupchip.is-contested{background-image:repeating-linear-gradient(135deg,rgba(255,255,255,.18) 0 4px,transparent 4px 8px);}
    .rx-dupchip:hover,.rx-dupchip.is-on{box-shadow:0 0 0 2px #fff,0 0 0 4px var(--dup);}
    .rx-groups--focus .rx-course{opacity:.28;transition:opacity .2s;}
    .rx-groups--focus .rx-course.is-dupfocus{opacity:1;outline:2px solid var(--dup);outline-offset:1px;animation:rx-dup-pulse 1.4s ease-out 2;}
    @keyframes rx-dup-pulse{0%{box-shadow:inset -4px 0 0 var(--dup),0 0 0 0 var(--dup);}70%{box-shadow:inset -4px 0 0 var(--dup),0 0 0 8px transparent;}100%{box-shadow:inset -4px 0 0 var(--dup);}}
    .rx-dupdots{display:inline-flex;gap:3px;align-items:center;margin-left:4px;}
    .rx-dupdots i{display:block;width:8px;height:8px;border-radius:50%;box-shadow:0 0 0 1px #fff;}
    .rx-dups{margin:0 0 10px;}
    .rx-dups:empty{display:none;}
    .rx-dups__ok{display:flex;align-items:center;gap:6px;color:#0b5c3a;font-size:12px;font-weight:600;padding:7px 11px;background:#e6f4ec;border:1px solid #c3e6cb;border-radius:4px;}
    .rx-dups__hd{display:flex;align-items:center;gap:10px;flex-wrap:wrap;padding:8px 11px;background:#fff;border:1px solid #e0e5ed;border-bottom:0;border-radius:4px 4px 0 0;}
    .rx-dups__hd b{color:#05275C;font-size:13px;}
    .rx-dups__hd span{color:#4b5563;font-size:11.5px;}
    .rx-dups__hd em{font-style:normal;color:#92400e;font-weight:700;}
    .rx-dups__fix{margin-left:auto;padding:4px 10px;font-size:11.5px;}
    .rx-dups__clear{background:none;border:1px solid #cbd5e1;border-radius:3px;font:600 11px inherit;padding:3px 8px;cursor:pointer;color:#174DA4;}
    .rx-dups__list{display:flex;flex-wrap:wrap;gap:6px;padding:8px 11px;background:#fafbfd;border:1px solid #e0e5ed;border-radius:0 0 4px 4px;}
    .rx-dups__g{display:inline-flex;align-items:center;gap:6px;border:1px solid color-mix(in srgb,var(--dup) 45%,#fff);background:#fff;
                border-radius:14px;padding:2px 9px 2px 2px;cursor:pointer;font:inherit;font-size:11.5px;color:#1a1a2e;transition:all .15s;}
    .rx-dups__g i{font-style:normal;display:inline-flex;align-items:center;justify-content:center;width:20px;height:20px;border-radius:50%;
                  background:var(--dup);color:#fff;font-weight:700;font-size:10.5px;}
    .rx-dups__g span{color:#8a94a6;font-weight:700;}
    .rx-dups__g svg{color:#b45309;}
    .rx-dups__g.is-contested{border-style:dashed;}
    .rx-dups__g:hover,.rx-dups__g.is-on{background:var(--dup);color:#fff;border-color:var(--dup);}
    .rx-dups__g:hover span,.rx-dups__g.is-on span,.rx-dups__g:hover svg,.rx-dups__g.is-on svg{color:#fff;}
    .rx-dups__g:hover i,.rx-dups__g.is-on i{background:#fff;color:var(--dup);}
    @media(max-width:720px){.rx-aa__row{grid-template-columns:22px minmax(0,1fr);} .rx-aa__row > :nth-child(n+3){grid-column:2;}}
    </style>
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

    <%-- Shown only when the sitting was opened straight from a link, so a reason recorded on
         the user's behalf is never invisible to them. --%>
    <div class="rx-auto" id="rx-auto" style="display:none"></div>

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
        <button type="button" class="rx-btn rx-btn--auto" id="rx-autoarr" disabled="disabled" title="Find courses in the wrong semester and empty duplicates, and propose the fix">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3v3M12 18v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M3 12h3M18 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1"/><circle cx="12" cy="12" r="3.5"/></svg>
          Auto-arrange<em id="rx-auto-n" style="display:none"></em></button>
        <button type="button" class="rx-btn" id="rx-save" disabled="disabled">Review &amp; save…</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-discard" disabled="disabled">Discard pending changes</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-regsem">Register a new semester…</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-expand">Expand all</button>
        <button type="button" class="rx-btn rx-btn--ghost" id="rx-reload">Reload from database</button>
      </div>
    </div>

    <div id="rx-dups" class="rx-dups"></div>
    <div id="rx-groups"></div>
  </div>

  <div id="rx-boot" class="rx-state" style="display:none">
    <div class="rx-spin"></div>Loading the student record…
  </div>
</div>

<!-- ══ Auto-arrange: the proposal ══ -->
<div class="rx-modal rx-aa" id="rx-aa-modal">
  <div class="rx-modal__box">
    <div class="rx-modal__hd"><span>Auto-arrange this record</span>
      <button type="button" class="rx-modal__x" data-close="rx-aa-modal">&times;</button></div>
    <div class="rx-modal__bd" id="rx-aa-body"></div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-aa-modal">Cancel</button>
      <button type="button" class="rx-btn" id="rx-aa-apply">Add to pending changes</button>
    </div>
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

<!-- ══ Change the academic year a semester sits in ══ -->
<div class="rx-modal" id="rx-reterm-modal">
  <div class="rx-modal__box" style="max-width:600px">
    <div class="rx-modal__hd"><span id="rx-rt-title">Change academic year</span>
      <button type="button" class="rx-modal__x" data-close="rx-reterm-modal">&times;</button></div>
    <div class="rx-modal__bd">
      <div class="rx-hint" id="rx-rt-context"></div>

      <div style="display:flex; gap:10px; flex-wrap:wrap; margin-top:10px">
        <div style="flex:0 0 150px"><label class="rx-lbl">Currently in</label>
          <input type="text" id="rx-rt-from" class="rx-in" readonly="readonly" /></div>
        <div style="flex:1 1 170px"><label class="rx-lbl" for="rx-rt-to">Move it to</label>
          <select id="rx-rt-to" class="rx-sel"></select></div>
      </div>

      <%-- A year of study normally sits inside one academic year. Re-terming a single
           semester out of a year that has more than one leaves the year straddling two,
           so the choice is put in front of the officer rather than inferred. --%>
      <div style="margin-top:12px">
        <label class="rx-lbl">Apply to</label>
        <label class="rx-radio"><input type="radio" name="rx-rt-scope" value="semester" checked="checked" />
          <span id="rx-rt-scope-sem">this semester only</span></label>
        <label class="rx-radio"><input type="radio" name="rx-rt-scope" value="year" />
          <span id="rx-rt-scope-year">the whole year of study</span></label>
      </div>

      <div class="rx-check" id="rx-rt-check"></div>

      <div style="margin-top:12px">
        <label class="rx-lbl" for="rx-rt-reason">Reason</label>
        <input type="text" id="rx-rt-reason" class="rx-in" placeholder="Why does this semester belong to a different academic year?" autocomplete="off" />
        <div class="rx-chips" id="rx-rt-chips"></div>
      </div>
    </div>
    <div class="rx-modal__ft">
      <button type="button" class="rx-btn rx-btn--ghost" data-close="rx-reterm-modal">Cancel</button>
      <button type="button" class="rx-btn" id="rx-rt-add">Add to pending changes</button>
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

<script src="js/rearrange-manage.js?v=20261013d" type="text/javascript"></script>
</asp:Content>
