<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="IDCardController.aspx.cs" Inherits="COOPERP_NewScreens_IDCardController" Title="ID Card Controller - Campus Dynamics" %>
<asp:Content ID="Head" ContentPlaceHolderID="HeadContent" runat="server">
<style>
.idc{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;color:#1a1a2e;}
.idc-hd{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:10px;margin-bottom:14px;}
.idc-hd h1{font-size:18px;font-weight:700;color:#05275C;margin:0;}
.idc-hd p{font-size:12px;color:#5a6472;margin:2px 0 0;}
.idc-hd__r{display:flex;align-items:center;gap:10px;}
.idc-updated{font-size:11px;color:#8a94a6;}
.idc-tabs{display:flex;gap:2px;margin-bottom:12px;}
.idc-tab{border:1px solid #e0e5ed;background:#fff;color:#05275C;font-size:12px;font-weight:600;padding:8px 16px;cursor:pointer;border-radius:0;}
.idc-tab.on{background:#05275C;color:#fff;border-color:#05275C;}
.idc-stats{display:grid;grid-template-columns:repeat(9,1fr);gap:8px;margin-bottom:14px;}
.idc-stat{background:#fff;border:1px solid #e0e5ed;border-radius:4px;padding:10px 6px;text-align:center;cursor:pointer;transition:border-color .12s,background .12s;}
.idc-stat:hover{border-color:#174DA4;}
.idc-stat__v{font-size:19px;font-weight:700;color:#05275C;line-height:1;}
.idc-stat__l{font-size:9.5px;color:#5a6472;margin-top:4px;text-transform:uppercase;letter-spacing:.3px;}
.idc-stat.on{background:#05275C;border-color:#05275C;}
.idc-stat.on .idc-stat__v,.idc-stat.on .idc-stat__l{color:#fff;}
.idc-card{background:#fff;border:1px solid #e0e5ed;border-radius:4px;padding:14px;margin-bottom:12px;}
.idc-filters{display:flex;flex-wrap:wrap;gap:8px;align-items:flex-end;}
.idc-fg{display:flex;flex-direction:column;gap:3px;}
.idc-fg label{font-size:10px;color:#5a6472;text-transform:uppercase;letter-spacing:.3px;}
.idc-sel,.idc-in{border:1px solid #d0d5dd;padding:7px 9px;font-size:12px;border-radius:0;background:#fff;min-width:120px;height:34px;box-sizing:border-box;}
.idc-in--search{min-width:210px;}
.idc-btn{border:1px solid #05275C;background:#05275C;color:#fff;font-size:12px;font-weight:600;padding:8px 14px;border-radius:0;cursor:pointer;height:34px;box-sizing:border-box;}
.idc-btn:disabled{opacity:.5;cursor:not-allowed;}
.idc-btn--ghost{background:#fff;color:#05275C;}
.idc-btn--sm{padding:5px 10px;font-size:11px;height:auto;}
.idc-btn--danger{border-color:#c0392b;background:#c0392b;color:#fff;}
.idc-btn--green{border-color:#128a4a;background:#128a4a;color:#fff;}
.idc-chips{display:flex;flex-wrap:wrap;gap:6px;margin-top:10px;min-height:0;}
.idc-chipf{display:inline-flex;align-items:center;gap:6px;font-size:11px;font-weight:600;background:#eef3ff;color:#1e3a5f;border:1px solid #cdddff;padding:3px 6px 3px 10px;border-radius:12px;}
.idc-chipf button{border:none;background:#1e3a5f;color:#fff;width:15px;height:15px;border-radius:50%;font-size:11px;line-height:1;cursor:pointer;padding:0;}
.idc-chipf--clear{background:#fff;color:#c0392b;border-color:#f3c4c0;cursor:pointer;padding:3px 10px;}
.idc-toolbar{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:8px;margin:12px 0 8px;}
.idc-bulk{display:flex;align-items:center;gap:8px;flex-wrap:wrap;background:#05275C;color:#fff;padding:7px 12px;border-radius:4px;}
.idc-bulk__n{font-size:12px;font-weight:700;}
.idc-bulk__hint{font-size:11px;color:#c5d3ec;}
.idc-toolbar__r{display:flex;align-items:center;gap:8px;font-size:11px;color:#5a6472;}
.idc-tbl{width:100%;border-collapse:collapse;font-size:12px;}
.idc-tbl th{background:#f5f7fa;color:#05275C;text-align:left;padding:8px;border-bottom:1px solid #e0e5ed;font-size:10.5px;text-transform:uppercase;letter-spacing:.3px;}
.idc-tbl td{padding:8px;border-bottom:1px solid #eef1f6;}
.idc-tbl tbody tr:hover td{background:#fafbfc;}
.idc-tbl tbody tr.sel td{background:#eef3ff;}
.idc-tbl .idc-ck{width:34px;text-align:center;cursor:default;}
.idc-tbl .idc-clickrow{cursor:pointer;}
.idc-chip{display:inline-block;font-size:10px;font-weight:700;padding:2px 8px;border-radius:10px;}
.st-REQUESTED,.st-FINANCE_CHECK{background:#eef3ff;color:#1e3a5f;}
.st-BLOCKED,.st-HALTED,.st-CANCELLED{background:#fef2f2;color:#991b1b;}
.st-SUBMITTED{background:#fff7ed;color:#b5720a;}
.st-APPROVED,.st-PRINTED{background:#f0f9ff;color:#0369a1;}
.st-READY{background:#ecfdf5;color:#047857;}
.st-COLLECTED{background:#e5e7eb;color:#374151;}
.idc-pager{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:8px;margin-top:10px;font-size:12px;color:#5a6472;}
.idc-pages{display:flex;gap:4px;flex-wrap:wrap;}
.idc-pg{border:1px solid #d0d5dd;background:#fff;color:#05275C;font-size:12px;padding:5px 10px;cursor:pointer;border-radius:0;min-width:32px;}
.idc-pg.on{background:#05275C;color:#fff;border-color:#05275C;}
.idc-pg:disabled{opacity:.4;cursor:not-allowed;}
.idc-modal{display:none;position:fixed;inset:0;background:rgba(5,39,92,.45);z-index:1000;align-items:flex-start;justify-content:center;padding:30px 12px;overflow:auto;}
.idc-modal.on{display:flex;}
.idc-box{background:#fff;width:100%;max-width:720px;border-radius:2px;}
.idc-box__hd{display:flex;align-items:center;justify-content:space-between;padding:14px 18px;background:#05275C;color:#fff;font-weight:700;font-size:14px;}
.idc-box__x{background:none;border:none;color:#fff;font-size:20px;cursor:pointer;}
.idc-box__bd{padding:18px;}
.idc-box__ft{padding:12px 18px;border-top:1px solid #eef1f6;display:flex;flex-wrap:wrap;gap:8px;justify-content:flex-end;}
.idc-idn{display:flex;gap:14px;align-items:center;margin-bottom:14px;}
.idc-idn__ph{width:64px;height:64px;border:1px solid #e0e5ed;border-radius:4px;object-fit:cover;background:#f5f7fa;}
.idc-grid{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:14px;}
.idc-fld{background:#f5f7fa;border:1px solid #eef1f6;border-radius:2px;padding:7px 10px;}
.idc-fld__k{font-size:9.5px;color:#5a6472;text-transform:uppercase;}
.idc-fld__v{font-size:12.5px;font-weight:600;margin-top:2px;}
.idc-tl{border-left:2px solid #e0e5ed;margin-left:6px;padding-left:14px;}
.idc-tl__i{position:relative;padding:6px 0;font-size:12px;}
.idc-tl__i:before{content:'';position:absolute;left:-19px;top:10px;width:8px;height:8px;border-radius:50%;background:#174DA4;}
.idc-tl__t{font-size:10px;color:#8a94a6;}
.idc-note{font-size:11px;color:#5a6472;margin-top:4px;}
.idc-empty{text-align:center;color:#8a94a6;padding:26px;font-size:12px;}
/* ── Force ID collection ────────────────────────────────────────────────────
   The modal is a data-entry surface an operator uses over and over, so the year
   picker fills the number prefix, the date chips replace arithmetic, and the
   form resets to the next student instead of closing. */
.idc-yr{display:flex;gap:6px;flex-wrap:wrap;}
.idc-yr button{border:1px solid #cdddff;background:#fff;color:#1e3a5f;font-size:12px;font-weight:700;
  padding:6px 12px;cursor:pointer;border-radius:2px;font-family:inherit;}
.idc-yr button:hover{border-color:#05275C;}
.idc-yr button.on{background:#05275C;border-color:#05275C;color:#fff;}
.idc-when{display:flex;gap:6px;flex-wrap:wrap;margin-top:6px;}
.idc-when button{border:1px solid #e2e8f0;background:#f8fafc;color:#475569;font-size:11px;
  padding:4px 9px;cursor:pointer;border-radius:2px;font-family:inherit;}
.idc-when button:hover{border-color:#05275C;color:#05275C;}
.idc-when button.on{border-color:#05275C;border-width:2px;background:#eef3ff;color:#05275C;font-weight:700;padding:3px 8px;}
.idc-who{background:#f8fafc;border:1px solid #e2e8f0;border-left:3px solid #05275C;padding:10px 12px;
  font-size:12px;line-height:1.6;color:#334155;min-height:44px;}
.idc-who b{color:#05275C;}
.idc-who .k{color:#8a94a6;display:inline-block;min-width:78px;}
.idc-who--warn{border-left-color:#d97706;background:#fffbeb;}
.idc-who--err{border-left-color:#b42318;background:#fef2f2;color:#7a1f1a;}
/* The student number is tinted by campus so a list can be scanned and grouped by eye:
   blue = Kakeeka, green = Kirumba. Matched on the campus NAME rather than an id, so it
   still works for the "Campus N" fallback and survives an id being renumbered. */
.idc-cid{display:inline-block;font-family:Consolas,Menlo,monospace;font-weight:700;font-size:10.5px;
  padding:2px 7px;border-radius:10px;letter-spacing:.3px;}
.idc-cid--kak{background:#eef3ff;color:#174DA4;border:1px solid #cdddff;}
.idc-cid--kir{background:#ecfdf5;color:#0f7040;border:1px solid #bbf7d0;}
.idc-cid--na {background:#f1f5f9;color:#64748b;border:1px solid #e2e8f0;}
/* -- Card scanner -----------------------------------------------------------
   Only the student number is ever read. The rest of the card (name, programme,
   dates) is deliberately ignored: the number is a rigid MRU+10-digit pattern, so
   it can be whitelisted, regex-matched and error-corrected, and the SERVER then
   returns the authoritative name. Reading 13 characters reliably beats reading a
   whole card badly.

   The frame is read in passes — the whole frame, the narrow band the operator aims
   the number into, a wider strip taking in the name line above it, and those three
   mirrored. The ORDER is not fixed: every attempt is scored, and every ten reads
   the passes are sorted by what has actually been working here. The best of them
   gets three goes at a live frame, and the runner-up two, before the next is
   reached for. */
.idc-scan{border:1px dashed #cdddff;background:#f8fafc;padding:14px;margin-top:8px;text-align:center;}
.idc-scan.drag{border-color:#05275C;background:#eef4ff;}
.idc-scan__hint{font-size:12px;color:#5a6472;line-height:1.6;}
.idc-scan__row{display:flex;gap:8px;justify-content:center;flex-wrap:wrap;margin-top:10px;}
.idc-scan__prev{max-width:100%;max-height:170px;margin-top:10px;border:1px solid #e2e8f0;display:none;}
/* A small live window. The operator is looking at the form, not at a video feed;
   the camera only has to be big enough to aim a card into. */
.idc-scan__cam{width:230px;height:145px;object-fit:cover;background:#000;
  display:none;border:2px solid #05275C;border-radius:3px;}
/* The aim band. The whole frame is read, so this is a guide and not a mask: it
   marks where to put the student number so the card is square and roughly the
   right distance away. The dimming outside is light for that reason — it suggests
   where to aim without claiming the rest is ignored. */
.idc-scan__lens{position:relative;display:none;line-height:0;margin-top:10px;overflow:hidden;
  border-radius:3px;}
.idc-scan__band{position:absolute;left:0;right:0;pointer-events:none;
  border-top:1px solid rgba(255,209,0,.95);border-bottom:1px solid rgba(255,209,0,.95);
  box-shadow:0 0 0 9999px rgba(0,0,0,.14);}
.idc-scan__band > span{position:absolute;left:0;top:0;font-size:9px;line-height:13px;
  color:#111;background:rgba(255,209,0,.95);padding:0 5px;letter-spacing:.3px;}
/* One quiet line of what the reader has learnt. It is small on purpose — useful to
   glance at, never in the way of the queue. */
.idc-scan__learn{font-size:10px;color:#7a8494;line-height:1.6;margin-top:6px;cursor:pointer;}
.idc-scan__learn b{color:#05275C;font-weight:600;}
.idc-scan--live{display:flex;align-items:center;gap:14px;text-align:left;}
/* Two columns so the whole form — including Apply — fits on one screen. The
   modal grew tall enough that the operator had to scroll past the scanner to
   reach the button they press for every single student. */
.fc-cols{display:grid;grid-template-columns:1.15fr 1fr;gap:20px;align-items:start;}
.fc-cols > div{min-width:0;}
@media(max-width:860px){ .fc-cols{grid-template-columns:1fr;gap:12px;} }
/* Keep the footer in view on a short screen rather than off the bottom. */
.idc-box--wide .idc-box__bd{max-height:calc(100vh - 210px);overflow:auto;}
.idc-box--wide .idc-box__ft{position:sticky;bottom:0;background:#fff;}
.idc-scan--live .idc-scan__hint{display:none;}
.idc-scan--live .idc-scan__side{flex:1;min-width:0;}
.idc-scan__aim{position:relative;display:inline-block;}
.idc-scan__bar{height:5px;background:#e9edf3;margin-top:10px;display:none;overflow:hidden;}
.idc-scan__bar i{display:block;height:100%;width:0;background:#05275C;transition:width .2s;}
.idc-scan__msg{font-size:12px;margin-top:8px;color:#334155;min-height:16px;}
.idc-legend{display:flex;gap:14px;align-items:center;font-size:11px;color:#5a6472;margin:10px 0 0;}
.idc-pt{display:flex;gap:8px;flex-wrap:wrap;}
.idc-pt label{flex:1 1 160px;display:flex;gap:8px;align-items:flex-start;border:1px solid #e2e8f0;
  padding:9px 11px;font-size:12px;cursor:pointer;background:#fff;}
.idc-pt label:hover{border-color:#05275C;}
.idc-pt input{margin-top:2px;}
.idc-fbadge{display:inline-block;font-size:10px;font-weight:700;padding:2px 8px;border-radius:10px;}
.idc-fbadge--on{background:#fee2e2;color:#991b1b;}
.idc-fbadge--wait{background:#fef3c7;color:#92400e;}
.idc-fbadge--done{background:#dcfce7;color:#166534;}
.idc-toast{position:fixed;top:18px;right:18px;z-index:2000;padding:10px 16px;font-size:13px;font-weight:600;color:#fff;border-radius:2px;box-shadow:0 4px 16px rgba(0,0,0,.2);}
@media(max-width:1100px){.idc-stats{grid-template-columns:repeat(5,1fr);}}
@media(max-width:700px){.idc-stats{grid-template-columns:repeat(3,1fr);}.idc-grid{grid-template-columns:1fr;}.idc-in--search{min-width:150px;}}
</style>
</asp:Content>
<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="idc">
  <div class="idc-hd">
    <div><h1>ID Card Controller</h1><p>Requests, approvals, printing and collection &middot; XAXU operations</p></div>
    <div class="idc-hd__r"><span class="idc-updated" id="idc-updated"></span><button type="button" class="idc-btn" onclick="IDC.openForce()">Force ID Collection</button><button type="button" class="idc-btn idc-btn--ghost" onclick="IDC.refresh()">Refresh</button></div>
  </div>

  <div class="idc-tabs">
    <button type="button" id="tab-q" class="idc-tab on" onclick="IDC.tab('q')">Requests</button>
    <button type="button" id="tab-w" class="idc-tab" onclick="IDC.tab('w')">Request Windows</button>
    <button type="button" id="tab-f" class="idc-tab" onclick="IDC.tab('f')">Force Collection</button>
  </div>

  <div id="view-q">
    <div class="idc-stats" id="idc-stats"></div>
    <div class="idc-card">
      <div class="idc-filters">
        <div class="idc-fg"><label>Status</label><select id="f-status" class="idc-sel" onchange="IDC.apply()"><option value="">All statuses</option><%= IDCardOptions() %></select></div>
        <div class="idc-fg"><label>Type</label><select id="f-type" class="idc-sel" onchange="IDC.apply()"><option value="">All types</option><option value="STUDENT">Student</option><option value="STAFF">Staff</option></select></div>
        <div class="idc-fg"><label>Card</label><select id="f-card" class="idc-sel" onchange="IDC.apply()"><option value="">All cards</option><option value="NEW">New</option><option value="REPLACEMENT">Replacement</option></select></div>
        <div class="idc-fg"><label>Search</label><input id="f-q" class="idc-in idc-in--search" placeholder="request no / student no / name" onkeydown="if(event.keyCode===13)IDC.apply()" /></div>
        <div class="idc-fg"><label>&nbsp;</label><button type="button" class="idc-btn" onclick="IDC.apply()">Apply</button></div>
      </div>
      <div class="idc-chips" id="idc-chips"></div>

      <div class="idc-toolbar">
        <div id="idc-bulk" style="display:none"></div>
        <div class="idc-toolbar__r">
          <span id="idc-count"></span>
          <label>Rows
            <select id="f-size" class="idc-sel" style="min-width:70px;height:28px;padding:3px 6px" onchange="IDC.apply()">
              <option value="25">25</option><option value="50" selected>50</option><option value="100">100</option><option value="200">200</option>
            </select>
          </label>
        </div>
      </div>

      <div style="overflow:auto"><table class="idc-tbl">
        <thead><tr>
          <th class="idc-ck"><input type="checkbox" id="ck-all" onclick="IDC.selAll(this.checked)" title="Select all on this page" /></th>
          <th>Request</th><th>Requester</th><th>Type</th><th>Card</th><th>Status</th><th>Created</th>
        </tr></thead>
        <tbody id="idc-rows"><tr><td colspan="7" class="idc-empty">Loading&hellip;</td></tr></tbody>
      </table></div>
      <div class="idc-pager"><div id="idc-pgtext"></div><div class="idc-pages" id="idc-pages"></div></div>
    </div>
  </div>

  <div id="view-w" style="display:none">
    <div class="idc-card">
      <strong style="font-size:12px;color:#05275C;">Create a request window</strong>
      <div class="idc-filters" style="margin-top:10px;">
        <div class="idc-fg"><label>Title</label><input id="w-title" class="idc-in" placeholder="e.g. 2026/2027 ID drive" /></div>
        <div class="idc-fg"><label>Scope</label><select id="w-scope" class="idc-sel"><option value="BOTH">Both</option><option value="STUDENT">Students</option><option value="STAFF">Staff</option></select></div>
        <div class="idc-fg"><label>Opens</label><input id="w-open" class="idc-in" type="datetime-local" /></div>
        <div class="idc-fg"><label>Closes</label><input id="w-close" class="idc-in" type="datetime-local" /></div>
        <div class="idc-fg"><label>&nbsp;</label><button type="button" class="idc-btn" onclick="IDC.createWindow()">Create</button></div>
      </div>
      <div class="idc-note">While no window exists, requests are open. Once you create one, requests are only accepted inside an active window.</div>
    </div>
    <div class="idc-card"><div style="overflow:auto"><table class="idc-tbl">
      <thead><tr><th>Title</th><th>Scope</th><th>Opens</th><th>Closes</th><th>State</th><th></th></tr></thead>
      <tbody id="idc-wins"><tr><td colspan="6" class="idc-empty">Loading&hellip;</td></tr></tbody>
    </table></div></div>
  </div>
</div>

  <div id="view-f" style="display:none">
    <div class="idc-stats" id="idc-fstats"></div>
    <div class="idc-card">
      <div class="idc-filters">
        <div class="idc-fg"><label>State</label>
          <select id="ff-state" class="idc-sel" onchange="IDC.forceLoad()">
            <option value="">All</option><option value="active" selected>In force now</option>
            <option value="scheduled">Scheduled</option><option value="collected">Collected</option>
          </select></div>
        <div class="idc-fg"><label>Search</label><input id="ff-q" class="idc-in idc-in--search" placeholder="student no / name" onkeydown="if(event.keyCode===13)IDC.forceLoad()" /></div>
        <div class="idc-fg"><label>&nbsp;</label><button type="button" class="idc-btn" onclick="IDC.forceLoad()">Apply</button></div>
        <div class="idc-fg"><label>&nbsp;</label><button type="button" class="idc-btn idc-btn--ghost" onclick="IDC.openForce()">+ Force a student</button></div>
        <%-- For when printing stops. Holding the blockade is the one action that has to be
             possible in bulk and in a hurry: a student told to collect a card nobody can
             print is locked out of their portal for something they cannot do. --%>
        <div class="idc-fg"><label>&nbsp;</label><button type="button" class="idc-btn idc-btn--ghost"
             title="Hold every block still in force, for when card printing has stopped"
             onclick="IDC.postpone()">&#9202; Hold all&hellip;</button></div>
      </div>
      <div class="idc-legend">
        <span><span class="idc-cid idc-cid--kak">KAKEEKA</span> &nbsp;Kakeeka campus</span>
        <span><span class="idc-cid idc-cid--kir">KIRUMBA</span> &nbsp;Kirumba campus</span>
      </div>
      <div style="overflow:auto"><table class="idc-tbl">
        <thead><tr><th>Student</th><th>Batch</th><th>Collect from</th><th>Campus</th><th>In force from</th><th>State</th><th>Set by</th><th></th></tr></thead>
        <tbody id="idc-frows"><tr><td colspan="7" class="idc-empty">Loading&hellip;</td></tr></tbody>
      </table></div>
    </div>
  </div>

  <%-- Force ID collection. Deliberately does NOT close on save: the operator is
       usually working through a stack of students, so it resets to the next one. --%>
  <div class="idc-modal" id="idc-force"><div class="idc-box idc-box--wide" style="max-width:960px">
    <div class="idc-box__hd"><span>Force ID card collection</span><button type="button" class="idc-box__x" onclick="IDC.closeForce()">&times;</button></div>
    <div class="idc-box__bd">

      <div class="fc-cols">
      <div>

      <div class="idc-fld__k" style="margin-bottom:5px">Intake year</div>
      <div class="idc-yr" id="fc-years"></div>

      <div class="idc-fld__k" style="margin:12px 0 5px">Student number</div>
      <div style="display:flex;gap:8px;align-items:center">
        <input id="fc-reg" class="idc-in" style="flex:1;font-family:Consolas,monospace;font-weight:700;letter-spacing:.5px"
               placeholder="Pick a year above, then type the rest" autocomplete="off" spellcheck="false"
               oninput="IDC.regTyped()" onkeydown="if(event.keyCode===13){event.preventDefault();IDC.searchNow();}" />
        <button type="button" class="idc-btn idc-btn--sm idc-btn--ghost" onclick="IDC.searchNow()"
                title="Look this number up now, whatever its length">Search</button>
        <button type="button" class="idc-btn idc-btn--sm" onclick="IDC.scanToggle()"
                title="Read the number off a photo of the student's card">Scan card</button>
      </div>
      <div class="idc-scan" id="fc-scan" style="display:none">
        <div class="idc-scan__hint">
          Drop a photo of the card here, paste it, or choose one.<br/>
          A mirrored photo (front camera) is handled automatically.
        </div>
        <div class="idc-scan__row">
          <input type="file" id="fc-file" accept="image/*" capture="environment" style="display:none" onchange="IDC.scanFile(this.files[0])" />
          <button type="button" class="idc-btn idc-btn--sm" id="fc-camstart" onclick="IDC.camOn()">Open camera</button>
          <button type="button" class="idc-btn idc-btn--sm idc-btn--ghost" onclick="document.getElementById('fc-file').click()">Choose a photo</button>
          <button type="button" class="idc-btn idc-btn--sm idc-btn--ghost" onclick="IDC.scanToggle()">Close</button>
        </div>

        <div class="idc-scan__lens" id="fc-lens">
          <video id="fc-cam" class="idc-scan__cam" playsinline muted autoplay></video>
          <div class="idc-scan__band" id="fc-band"><span>Student No.</span></div>
        </div>

        <div class="idc-scan__row" id="fc-camrow" style="display:none">
          <button type="button" class="idc-btn idc-btn--sm" onclick="IDC.camShot()">Read now</button>
          <button type="button" class="idc-btn idc-btn--sm" id="fc-voice" onclick="IDC.voice()"
                  title="Read the student's name and campus aloud">Voice on</button>
          <button type="button" class="idc-btn idc-btn--sm idc-btn--ghost" onclick="IDC.camOff()">Stop camera</button>
        </div>
        <div class="idc-scan__learn" id="fc-learn" title="What the reader has worked out for itself. Click to forget it."
             onclick="IDC.learnReset()"></div>
        <img id="fc-prev" class="idc-scan__prev" alt="" />
        <div class="idc-scan__bar" id="fc-bar"><i id="fc-barf"></i></div>
        <div class="idc-scan__msg" id="fc-scanmsg"></div>
      </div>

      <div class="idc-who" id="fc-who" style="margin-top:8px">Choose an intake year, then type the student number.</div>

      </div>
      <div>

      <div class="idc-fld__k" style="margin-top:0;margin-bottom:5px">Batch / group
        <span style="font-weight:400;color:#8a94a6">(cards are sorted into these &mdash; stays put between students)</span></div>
      <div class="idc-yr" id="fc-groups"></div>

      <div class="idc-fld__k" style="margin:14px 0 5px">Collect from</div>
      <div class="idc-pt">
        <label><input type="radio" name="fc-pt" value="AR_OFFICE" checked /><span><b>Academic Registrar&rsquo;s Office</b><br/><span style="color:#8a94a6">Main administration block</span></span></label>
        <label><input type="radio" name="fc-pt" value="IT_OFFICE" /><span><b>ICT Office</b><br/><span style="color:#8a94a6">ICT / systems office</span></span></label>
      </div>

      <div class="idc-fld__k" style="margin:14px 0 5px">Block the portal from</div>
      <input id="fc-date" class="idc-in" type="date" style="width:100%" onchange="IDC.dateChanged()" />
      <div class="idc-when" id="fc-when">
        <button type="button" data-d="0"  onclick="IDC.when(0)">Today</button>
        <button type="button" data-d="2"  onclick="IDC.when(2)">In 2 days</button>
        <button type="button" data-d="3"  onclick="IDC.when(3)">In 3 days</button>
        <button type="button" data-d="7"  onclick="IDC.when(7)">In 1 week</button>
        <button type="button" data-d="14" onclick="IDC.when(14)">In 2 weeks</button>
      </div>
      <div class="idc-note" id="fc-whynote"></div>

      <div class="idc-fld__k" style="margin:14px 0 5px">Note to the student <span style="font-weight:400;color:#8a94a6">(optional)</span></div>
      <input id="fc-note" class="idc-in" style="width:100%" maxlength="200" placeholder="e.g. Bring your admission letter" />

      <div class="idc-note" style="margin-top:12px">Until the card is marked collected, this student cannot use any portal page from the date above.</div>

      </div>
      </div>
    </div>
    <div class="idc-box__ft">
      <button type="button" class="idc-btn idc-btn--ghost" onclick="IDC.closeForce()">Close</button>
      <button type="button" class="idc-btn" id="fc-go" onclick="IDC.saveForce()" disabled>Apply block</button>
    </div>
  </div></div>

  <div class="idc-modal" id="idc-detail"><div class="idc-box">
  <div class="idc-box__hd"><span id="d-title">Request</span><button type="button" class="idc-box__x" onclick="IDC.close()">&times;</button></div>
  <div class="idc-box__bd" id="d-body">Loading&hellip;</div>
  <div class="idc-box__ft" id="d-actions"></div>
</div></div>

<%-- Administrative status change. Kept separate from the action buttons because it is
     a different kind of act: the buttons walk the lifecycle, this one sets it. --%>
<div class="idc-modal" id="idc-setst"><div class="idc-box" style="max-width:520px">
  <div class="idc-box__hd"><span>Change status</span><button type="button" class="idc-box__x" onclick="IDC.closeStatus()">&times;</button></div>
  <div class="idc-box__bd">
    <div class="idc-fld" style="margin-bottom:12px">
      <div class="idc-fld__k">Request</div>
      <div class="idc-fld__v" id="ss-who">&mdash;</div>
    </div>
    <div class="idc-fld__k" style="margin-bottom:5px">Move to</div>
    <select id="ss-to" class="idc-in" style="width:100%" onchange="IDC.statusPicked()"></select>
    <div id="ss-hint" style="font-size:11.5px;line-height:1.5;margin-top:8px"></div>
    <div id="ss-ovwrap" style="display:none;margin-top:10px">
      <label style="display:flex;gap:8px;align-items:flex-start;background:#fef2f2;border:1px solid #fecaca;padding:9px 11px;font-size:12px;color:#7a1f1a;cursor:pointer">
        <input type="checkbox" id="ss-override" style="margin-top:2px;flex:0 0 auto" onchange="IDC.statusPicked()"/>
        <span>I understand this is not a normal step and I am setting it deliberately. This will be recorded as an override, with my name and reason, in the request&rsquo;s history.</span>
      </label>
    </div>
    <div class="idc-fld__k" style="margin:12px 0 5px">Reason <span id="ss-reqd" style="font-weight:400;text-transform:none;color:#8a94a6"></span></div>
    <textarea id="ss-reason" class="idc-in" style="width:100%;min-height:64px;resize:vertical" placeholder="Why is this being changed? Shown in the request history."></textarea>
    <div id="ss-msg" style="display:none;font-size:12px;padding:8px 10px;margin-top:10px"></div>
  </div>
  <div class="idc-box__ft">
    <button type="button" class="idc-btn idc-btn--ghost" onclick="IDC.closeStatus()">Cancel</button>
    <button type="button" class="idc-btn idc-btn--green" id="ss-go" onclick="IDC.applyStatus()">Apply</button>
  </div>
</div></div>

<script>
/* The status labels come from IDCardService, the same table the student's portal
   uses, so a state cannot be called one thing here and another thing there. Never
   hardcode a status name in this file -- call SL(). */
var IDC_LABELS = <%= IDCardService.StatusLabelsJson() %>;
function SL(s){ return IDC_LABELS[s] || s; }
window.IDC=(function(){
var CUR=null, CURST='', SEL={}, LASTROWS=[];
// Status list + legal-transition map, fetched once. The status picker uses it to
// label each choice as a normal step or an override, so an operator sees which is
// which before acting instead of after being refused.
var META={statuses:[],transitions:{}};
var state={status:'',type:'',card:'',q:'',page:1,size:50};
function qs(i){return document.getElementById(i);}
function esc(s){return s?String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'):'';}
function toast(m,err){var t=document.createElement('div');t.className='idc-toast';t.style.background=err?'#c0392b':'#128a4a';t.textContent=m;document.body.appendChild(t);setTimeout(function(){t.style.transition='opacity .4s';t.style.opacity='0';setTimeout(function(){t.remove();},400);},2800);}
function call(m,p,cb){var x=new XMLHttpRequest();x.open('POST',location.pathname+'/'+m,true);x.setRequestHeader('Content-Type','application/json; charset=utf-8');x.onload=function(){try{var o=JSON.parse(x.responseText);cb(typeof o.d==='string'?JSON.parse(o.d):o.d);}catch(e){cb({success:false,message:'Parse error'});}};x.onerror=function(){cb({success:false,message:'Network error'});};x.send(JSON.stringify(p||{}));}
function n(v){return (v==null||v==='')?'0':Number(v).toLocaleString();}
function chip(s){return '<span class="idc-chip st-'+esc(s)+'" title="'+esc(s)+'">'+esc(SL(s))+'</span>';}

/* ---------- GET / URL state ---------- */
function readURL(){
  var u=new URLSearchParams(location.search);
  state.status=u.get('status')||'';state.type=u.get('type')||'';state.card=u.get('card')||'';
  state.q=u.get('q')||'';state.page=parseInt(u.get('page')||'1',10)||1;state.size=parseInt(u.get('size')||'50',10)||50;
  qs('f-status').value=state.status;qs('f-type').value=state.type;qs('f-card').value=state.card;qs('f-q').value=state.q;qs('f-size').value=String(state.size);
}
function writeURL(){
  var u=new URLSearchParams();
  if(state.status)u.set('status',state.status);if(state.type)u.set('type',state.type);if(state.card)u.set('card',state.card);
  if(state.q)u.set('q',state.q);if(state.page>1)u.set('page',state.page);if(state.size!==50)u.set('size',state.size);
  var qsr=u.toString();history.replaceState(null,'',location.pathname+(qsr?('?'+qsr):''));
}
function chips(){
  var c=[],lbl={status:'Status',type:'Type',card:'Card',q:'Search'};
  ['status','type','card','q'].forEach(function(k){if(state[k])c.push('<span class="idc-chipf">'+lbl[k]+': '+esc(k==='status'?SL(state[k]):state[k])+' <button onclick="IDC.clear(\''+k+'\')" title="Remove">&times;</button></span>');});
  qs('idc-chips').innerHTML=c.length?(c.join('')+'<span class="idc-chipf idc-chipf--clear" onclick="IDC.clearAll()">Clear all</span>'):'';
}

/* ---------- stats ---------- */
function stats(){call('Stats',{},function(d){if(!d||!d.success)return;var m=[['total','Total',''],['requested',SL('REQUESTED'),'REQUESTED'],['submitted',SL('SUBMITTED'),'SUBMITTED'],['blocked',SL('BLOCKED'),'BLOCKED'],['approved',SL('APPROVED'),'APPROVED'],['halted',SL('HALTED'),'HALTED'],['printed',SL('PRINTED'),'PRINTED'],['ready',SL('READY'),'READY'],['collected',SL('COLLECTED'),'COLLECTED']];
 qs('idc-stats').innerHTML=m.map(function(k){return '<div class="idc-stat'+(state.status===k[2]&&k[2]!==''?' on':(k[2]===''&&!state.status?' on':''))+'" onclick="IDC.filterStatus(\''+k[2]+'\')"><div class="idc-stat__v">'+n(d[k[0]])+'</div><div class="idc-stat__l">'+k[1]+'</div></div>';}).join('');});}

/* ---------- list ---------- */
function load(){
  writeURL();chips();
  var b=qs('idc-rows');b.innerHTML='<tr><td colspan="7" class="idc-empty">Loading&hellip;</td></tr>';
  SEL={};syncBulk();qs('ck-all').checked=false;
  call('List',{status:state.status,type:state.type,cardType:state.card,q:state.q,page:state.page,size:state.size},function(d){
    if(!d||!d.success){b.innerHTML='<tr><td colspan="7" class="idc-empty" style="color:#b42318">'+esc((d&&d.message)||'Error')+'</td></tr>';return;}
    LASTROWS=d.rows||[];
    if(!LASTROWS.length){b.innerHTML='<tr><td colspan="7" class="idc-empty">No requests match these filters.</td></tr>';qs('idc-count').textContent='0 requests';qs('idc-pgtext').textContent='';qs('idc-pages').innerHTML='';return;}
    b.innerHTML=LASTROWS.map(function(r){var rn=esc(r.requestNo);return '<tr id="row-'+rn+'">'+
      '<td class="idc-ck"><input type="checkbox" class="idc-rowck" value="'+rn+'" data-status="'+esc(r.status)+'" onclick="event.stopPropagation();IDC.selOne(\''+rn+'\',this.checked)"'+(SEL[r.requestNo]?' checked':'')+'/></td>'+
      '<td class="idc-clickrow" onclick="IDC.open(\''+rn+'\')"><b>'+rn+'</b></td>'+
      '<td class="idc-clickrow" onclick="IDC.open(\''+rn+'\')">'+esc(r.name||r.number)+'<div style="font-size:10px;color:#8a94a6">'+esc(r.number)+'</div></td>'+
      '<td>'+esc(r.type)+'</td><td>'+esc(r.cardType)+'</td><td>'+chip(r.status)+'</td>'+
      '<td style="font-size:11px;color:#5a6472">'+esc((r.createdAt||'').substring(0,16))+'</td></tr>';}).join('');
    var from=(d.page-1)*state.size+1, to=(d.page-1)*state.size+LASTROWS.length;
    qs('idc-count').textContent=n(d.total)+' request(s) - showing '+from+' to '+to;
    qs('idc-pgtext').textContent='Page '+d.page+' of '+d.pages;
    qs('idc-pages').innerHTML=pager(d.page,d.pages);
  });
}
function pager(pg,pages){
  if(pages<=1)return '';
  var h='<button type="button" class="idc-pg" '+(pg<=1?'disabled':'')+' onclick="IDC.go('+(pg-1)+')">&lsaquo;</button>';
  var start=Math.max(1,pg-2),end=Math.min(pages,pg+2);
  if(start>1){h+=btnpg(1,pg);if(start>2)h+='<span style="padding:5px 2px">…</span>';}
  for(var i=start;i<=end;i++)h+=btnpg(i,pg);
  if(end<pages){if(end<pages-1)h+='<span style="padding:5px 2px">…</span>';h+=btnpg(pages,pg);}
  h+='<button type="button" class="idc-pg" '+(pg>=pages?'disabled':'')+' onclick="IDC.go('+(pg+1)+')">&rsaquo;</button>';
  return h;
}
function btnpg(i,pg){return '<button type="button" class="idc-pg'+(i===pg?' on':'')+'" onclick="IDC.go('+i+')">'+i+'</button>';}

/* ---------- selection + batch ---------- */
function syncBulk(){
  var keys=Object.keys(SEL);var box=qs('idc-bulk');
  if(!keys.length){box.style.display='none';box.innerHTML='';return;}
  var sts={};keys.forEach(function(k){sts[SEL[k]]=1;});var uniq=Object.keys(sts);
  var h='<div class="idc-bulk"><span class="idc-bulk__n">'+keys.length+' selected</span>';
  if(uniq.length===1){
    var s=uniq[0],acts=bulkActionsFor(s);
    if(acts.length){acts.forEach(function(a){h+=' <button type="button" class="idc-btn idc-btn--sm '+(a.cls||'')+'" onclick="IDC.bulk(\''+a.act+'\')">'+a.lbl+'</button>';});}
    else h+='<span class="idc-bulk__hint">No bulk action for '+esc(s)+' requests.</span>';
  }else h+='<span class="idc-bulk__hint">Select requests of the same status to act in bulk.</span>';
  h+=' <button type="button" class="idc-btn idc-btn--sm" onclick="IDC.bulkForce()" title="Require the selected students to collect their card before the portal will open">Force collection</button>';
  h+=' <button type="button" class="idc-btn idc-btn--sm idc-btn--ghost" onclick="IDC.selClear()">Clear</button></div>';
  box.style.display='';box.innerHTML=h;
}
function bulkActionsFor(s){
  if(s==='SUBMITTED')return [{act:'APPROVE',lbl:'Approve selected',cls:'idc-btn--green'}];
  if(s==='APPROVED')return [{act:'PRINTED',lbl:'Mark printed'}];
  if(s==='PRINTED')return [{act:'READY',lbl:'Mark ready',cls:'idc-btn--green'}];
  if(s==='READY')return [{act:'COLLECTED',lbl:'Mark collected',cls:'idc-btn--green'}];
  return [];
}
function rowStatus(rn){for(var i=0;i<LASTROWS.length;i++)if(LASTROWS[i].requestNo===rn)return LASTROWS[i].status;return '';}

/* ══ FORCE ID COLLECTION ═══════════════════════════════════════════════════
   The modal is used down a stack of students, so it is built for repetition:
   the year buttons write the number prefix, the number is looked up in the
   background as it is typed (debounced — one request per pause, not per key),
   the date chips do the arithmetic, and saving resets to the next student
   without closing. Nothing is written until the lookup has confirmed a real
   student, so a block can never land on a mistyped number. */
var FCT=null, FCFOUND=null, FCDEB=null;

function fcYears(){
  var now=new Date().getFullYear(), h='';
  for(var y=now-4;y<=now;y++) h+='<button type="button" data-y="'+y+'" onclick="IDC.pickYear('+y+')">'+y+'</button>';
  qs('fc-years').innerHTML=h;
}
function fcMarkYear(y){
  var b=qs('fc-years').getElementsByTagName('button');
  for(var i=0;i<b.length;i++) b[i].classList.toggle('on', b[i].getAttribute('data-y')===String(y));
}
/* The fixed part of a number for the chosen intake: MRU + year + three leading zeros.
   Three rather than four on purpose — it is the safer default, because a number that
   needs FEWER zeros can always be resolved by the server (it strips and re-pads), and
   starting short means the operator adds rather than deletes. Typing starts after this,
   and a save clears only what was typed after it. */
function fcBase(){ return FCT ? ('MRU'+FCT+'000') : ''; }
/* The batch letters cards are sorted into. Chosen once and then LEFT ALONE — not by a
   save, and not by looking a student up — because an operator works through one batch
   at a time and having the selection jump under them is the whole thing to avoid. */
var FCG='';
var FC_GROUPS=['A','B','C','E','F','G','H','I','J','K'];
function fcGroups(){
  qs('fc-groups').innerHTML=FC_GROUPS.map(function(g){
    return '<button type="button" data-g="'+g+'" onclick="IDC.pickGroup(\'' + g + '\')">'+g+'</button>';
  }).join('');
  fcMarkGroup();
}
function fcMarkGroup(){
  var b=qs('fc-groups').getElementsByTagName('button');
  for(var i=0;i<b.length;i++) b[i].classList.toggle('on', b[i].getAttribute('data-g')===FCG);
}
/* Kirumba students need travel time, so a block on them starts a week out; Kakeeka
   is on the main site and starts today. Returns null for an unknown campus, which
   means "leave whatever the operator has chosen alone". */
function fcCampusDays(campus){
  var v=String(campus||'').toUpperCase();
  if(v.indexOf('KIRUMBA')>=0) return 7;
  if(v.indexOf('KAKEEKA')>=0) return 0;
  return null;
}
function fcDateIn(days){ var d=new Date(); d.setDate(d.getDate()+days); return fcISO(d); }
/* Outlines whichever quick-date matches the date actually in the box — including one
   the campus rule set, so the operator can see at a glance what was chosen for them. */
function fcMarkWhen(){
  var cur=qs('fc-date').value, b=qs('fc-when').getElementsByTagName('button');
  for(var i=0;i<b.length;i++){
    var off=parseInt(b[i].getAttribute('data-d'),10);
    b[i].classList.toggle('on', !!cur && fcDateIn(off)===cur);
  }
}
/* ══ CARD SCANNER (OCR) ════════════════════════════════════════════════════
   Tesseract.js v5, loaded lazily from a CDN the first time Scan is used — the
   engine plus the English model is about 2 MB, and a console that mostly types
   numbers should not pay for that on every page load.

   The whole design rests on one idea: DO NOT try to read the card. Read the
   student number only. It is MRU + a four-digit year + six digits, so:
     · Tesseract is whitelisted to M, R, U and the digits, which removes almost
       every way it can go wrong;
     · the usual OCR confusions are corrected (O->0, I/L->1, S->5, B->8, G->6...)
       because after "MRU" every character MUST be a digit;
     · a candidate is only accepted if it starts with a plausible intake year,
       which rejects the issue/expiry dates printed on the card;
     · whatever survives is handed to the server, whose RegnoCandidates() already
       repairs leading-zero mistakes and returns the REAL number and name.
   So 13 characters have to be read approximately right, not a whole card.

   Mirrored cards (a front-facing camera, like the sample) are expected: each
   image is tried as-is and then flipped, and the first variant that resolves to
   a real student wins. */
var TESS_SRC='https://cdn.jsdelivr.net/npm/tesseract.js@5/dist/tesseract.min.js';
var TESS_LOADING=false, TESS_WORKER=null;
var CAM_STREAM=null, CAM_TIMER=null, CAM_BUSY=false;
/* CAM_HOLD  — a student is loaded and not yet applied, so scanning idles. Without
                it a second card drifting into frame would silently swap the person
                about to be blocked, which is the one mistake that must not happen.
   CAM_SKIP  — the number just applied. The card is usually still in front of the
                lens, so it is ignored until a different one appears. */
var CAM_HOLD=false, CAM_SKIP='';
/* Two strips of the live frame, as fractions of what the operator can actually
   see. SCAN_BAND is the target and the only one drawn on screen: one line tall,
   the student number and nothing else — the tighter it is, the less there is to
   misread. SCAN_BAND2 is the safety net, tall enough to hold the name line and
   the number together, for a card held a little high or a little low. */
var SCAN_BAND ={y:0.36,h:0.28};
var SCAN_BAND2={y:0.16,h:0.64};
/* -- A reader that learns which way of looking actually works -----------------
   Every attempt is scored. A pass is credited when it produces a student and
   debited when it does not, and every ten reads the passes are re-ordered by what
   they have actually achieved. Which order is best is not a thing that can be
   decided in advance: it depends on the camera, the light over that desk, how the
   operator holds a card and even which batch of cards is being collected. So it
   is not decided in advance — it is measured. */
var PASS_DEFS={
  full:   {k:'full',   mirror:false, band:null},
  band:   {k:'band',   mirror:false, band:SCAN_BAND},
  band2:  {k:'band2',  mirror:false, band:SCAN_BAND2},
  fullM:  {k:'fullM',  mirror:true,  band:null},
  bandM:  {k:'bandM',  mirror:true,  band:SCAN_BAND},
  band2M: {k:'band2M', mirror:true,  band:SCAN_BAND2}
};
var PASS_NAME={
  full:'whole frame',        band:'number band',        band2:'name and number',
  fullM:'whole frame, mirrored', bandM:'number band, mirrored', band2M:'name and number, mirrored'
};
/* Roughly what each pass costs to run, against the whole frame at 1.0, measured
   from the crop geometry. It only ever breaks a near-tie: given two ways of
   looking that are equally accurate, take the quicker one. */
var PASS_COST={ full:1, fullM:1, band:0.35, bandM:0.35, band2:0.62, band2M:0.62 };
var PASS_ORDER0=['full','band','band2','fullM','bandM','band2M'];
/* How many goes each rank gets before the next one is tried, best first. The best
   way of looking is worth three attempts, because on a live camera each attempt
   sees a DIFFERENT frame — a third of a second later, refocused, held a little
   steadier — and the pass that has been landing nine times in ten deserves those
   before a weaker one is reached for. A still photo gets one go at each: the same
   pixels cannot give a different answer, so repeating them is only delay. */
var PASS_TRIES=[3,2,1,1,1,1];

var LEARN_KEY='idcScanLearn';
var LEARN_EVERY=10;                 /* reads between one re-ordering and the next */
var LEARN={ rank:PASS_ORDER0.slice(), reads:0, s:{} };
var LEARN_FLASH=null, LEARN_FLASH_T=null;

function learnCell(k){ var c=LEARN.s[k]; if(!c){ c={w:0,a:0}; LEARN.s[k]=c; } return c; }
function learnSeen(k){ return learnCell(k).a; }

/* Wins over attempts, with one imaginary win and one imaginary loss added. That
   small fiction matters: it puts a pass that has never been tried at 50%, high
   enough to stay in the running and get its chance, but not so high that it
   displaces one already proven. Nothing is ever ranked out for good. */
function learnRate(k){ var c=learnCell(k); return (c.w+1)/(c.a+2); }
function learnScore(k){ return learnRate(k) + 0.04*(1-(PASS_COST[k]||1)); }

function learnTry(k){ learnCell(k).a++; }
function learnWin(k){
  learnCell(k).w++;
  LEARN.reads++;
  if(LEARN.reads>=LEARN_EVERY) learnRetune();
  learnSave(); learnPaint();
}

/* Re-order by what worked, then fade every count by a fifth. The fade is the whole
   point of doing this repeatedly rather than once: without it the first hundred
   cards would settle the order for ever, and a new camera, a darker desk or a
   different batch of cards could never change its mind. */
function learnRetune(){
  var was=LEARN.rank[0], r=LEARN.rank.slice(), k;
  r.sort(function(a,b){ return learnScore(b)-learnScore(a); });
  LEARN.rank=r; LEARN.reads=0;
  for(k in LEARN.s) if(LEARN.s.hasOwnProperty(k)){ LEARN.s[k].w*=0.8; LEARN.s[k].a*=0.8; }
  learnFlash(was===LEARN.rank[0]
    ? 'retuned &mdash; <b>'+PASS_NAME[LEARN.rank[0]]+'</b> still leads'
    : 'retuned &mdash; now trying <b>'+PASS_NAME[LEARN.rank[0]]+'</b> first');
}

function learnFlash(html){
  LEARN_FLASH=html;
  if(LEARN_FLASH_T) clearTimeout(LEARN_FLASH_T);
  LEARN_FLASH_T=setTimeout(function(){ LEARN_FLASH=null; learnPaint(); },7000);
  learnPaint();
}

function learnPaint(){
  var el=qs('fc-learn'); if(!el) return;
  if(LEARN_FLASH){ el.innerHTML=LEARN_FLASH; return; }
  var top=LEARN.rank[0];
  el.innerHTML = learnSeen(top)<1
    ? 'reader: learning&hellip;'
    : 'reader: <b>'+PASS_NAME[top]+'</b> '+Math.round(learnRate(top)*100)+'%'
      +' &middot; retunes in '+(LEARN_EVERY-LEARN.reads);
}

/* Only keys this build knows, in the stored order, with anything new appended —
   so a saved order from an older version can never leave a pass unreachable. */
function learnFix(r){
  var out=[], seen={}, i;
  for(i=0;i<r.length;i++) if(PASS_DEFS[r[i]] && !seen[r[i]]){ seen[r[i]]=1; out.push(r[i]); }
  for(i=0;i<PASS_ORDER0.length;i++) if(!seen[PASS_ORDER0[i]]) out.push(PASS_ORDER0[i]);
  return out;
}
function learnSave(){ try{ localStorage.setItem(LEARN_KEY, JSON.stringify(LEARN)); }catch(e){} }
function learnLoad(){
  try{
    var raw=localStorage.getItem(LEARN_KEY); if(!raw) return;
    var o=JSON.parse(raw); if(!o||typeof o!=='object') return;
    if(o.s && typeof o.s==='object') LEARN.s=o.s;
    if(typeof o.reads==='number' && o.reads>=0 && o.reads<LEARN_EVERY) LEARN.reads=o.reads;
    if(o.rank && o.rank.length) LEARN.rank=learnFix(o.rank);
  }catch(e){}
}
function learnReset(){
  if(!window.confirm('Forget what the reader has learnt and start again?')) return;
  LEARN={ rank:PASS_ORDER0.slice(), reads:0, s:{} };
  learnSave(); learnFlash('forgotten &mdash; learning again from the whole frame');
}
learnLoad();   /* it carries on from where the last shift left off */
/* A 16x16 luma signature of the last frame that was read, and how many frames have
   been skipped since. Re-reading pixels that have not changed cannot produce a
   different answer, so it is pure waste — but the skip is capped, because a frame
   that merely came into focus can look identical at this resolution. */
var CAM_SIG=null, CAM_IDLE=0;

/* -- Speaking a detected student ---------------------------------------------
   No library. The browser's own Web Speech API (window.speechSynthesis) is built
   in, works offline, costs nothing and needs no download — every alternative
   (ResponsiveVoice, meSpeak.js, a cloud voice) would add weight, latency or a
   licence for a worse result. It is read aloud so the operator can keep their
   eyes on the card and the queue rather than on the screen.

   Only the NAME and the CAMPUS are spoken. The student number is deliberately
   not read out: thirteen characters of "M-R-U-two-zero-two-six..." is slower than
   reading it and tells the operator nothing they cannot already see. */
var TTS_ON = true;
try { TTS_ON = (localStorage.getItem('idcVoice') !== 'off'); } catch(e) {}

function ttsReady(){ return typeof window!=='undefined' && 'speechSynthesis' in window; }

/* Title-case so a voice reads "Kirumba Campus" rather than spelling out capitals. */
function ttsCase(t){
  return String(t||'').toLowerCase().replace(/\b[a-z]/g, function(c){ return c.toUpperCase(); });
}

function speak(name, campus){
  if(!TTS_ON || !ttsReady()) return;
  var line = ttsCase(name);
  if(campus) line += '. ' + ttsCase(campus);
  if(!line) return;
  try{
    /* Cancel first. In a continuous scan the utterances would otherwise queue and
       the voice would fall further and further behind the card in front of it. */
    window.speechSynthesis.cancel();
    var u = new SpeechSynthesisUtterance(line);
    u.lang='en-GB'; u.rate=0.95; u.pitch=1; u.volume=1;
    window.speechSynthesis.speak(u);
  }catch(e){}
}

function ttsToggle(){
  TTS_ON = !TTS_ON;
  try{ localStorage.setItem('idcVoice', TTS_ON?'on':'off'); }catch(e){}
  if(!TTS_ON && ttsReady()){ try{ window.speechSynthesis.cancel(); }catch(e){} }
  ttsPaint();
}
function ttsPaint(){
  var b=qs('fc-voice'); if(!b) return;
  b.textContent = TTS_ON ? 'Voice on' : 'Voice off';
  b.className = 'idc-btn idc-btn--sm' + (TTS_ON ? '' : ' idc-btn--ghost');
}

function scanMsg(html,err){ var m=qs('fc-scanmsg'); m.innerHTML=html; m.style.color=err?'#b42318':'#334155'; }
function scanBar(p){ var b=qs('fc-bar'); b.style.display=p==null?'none':'block'; if(p!=null) qs('fc-barf').style.width=Math.round(p*100)+'%'; }

function tessLoad(cb){
  if(window.Tesseract){ cb(true); return; }
  if(TESS_LOADING){ setTimeout(function(){ tessLoad(cb); },300); return; }
  TESS_LOADING=true;
  scanMsg('Loading the reader (about 2 MB, first time only)&hellip;');
  var el=document.createElement('script');
  el.src=TESS_SRC;
  el.onload=function(){ TESS_LOADING=false; cb(!!window.Tesseract); };
  el.onerror=function(){ TESS_LOADING=false; cb(false); };
  document.getElementsByTagName('head')[0].appendChild(el);
}

/* ONE worker, reused for every frame. Live scanning reads a frame a second, and
   spinning up and tearing down a Tesseract worker each time would cost more than
   the recognition itself. It is terminated when the scanner closes. */
function tessWorker(){
  if(TESS_WORKER) return TESS_WORKER;
  TESS_WORKER = window.Tesseract.createWorker('eng',1,{
    logger:function(m){ if(m&&m.status==='recognizing text') scanBar(m.progress||0); }
  }).then(function(w){
    return w.setParameters({
      tessedit_char_whitelist:'MRU0123456789',
      tessedit_pageseg_mode:'11'
    }).then(function(){ return w; });
  });
  return TESS_WORKER;
}
function tessFree(){
  if(!TESS_WORKER) return;
  var w=TESS_WORKER; TESS_WORKER=null;
  w.then(function(x){ try{ x.terminate(); }catch(e){} }).catch(function(){});
}

/* Grayscale + contrast stretch at a sane size. Tesseract does far better on a
   flattened, high-contrast image than on a phone photo of a watermarked card, and
   downscaling also keeps a live scan quick enough to repeat. */
function scanPrep(src,mirror,box){
  var sw=src.videoWidth||src.naturalWidth||src.width||0, sh=src.videoHeight||src.naturalHeight||src.height||0;
  if(!sw||!sh) return null;
  var bx=0, by=0, bw=sw, bh=sh;
  if(box){
    bx=Math.max(0,Math.round(box.x)); by=Math.max(0,Math.round(box.y));
    bw=Math.min(sw-bx,Math.round(box.w)); bh=Math.min(sh-by,Math.round(box.h));
    if(bw<40||bh<20) return null;
  }
  /* A whole frame is only ever scaled down. A crop may be scaled UP, because a
     150-pixel-tall strip has to carry digits tall enough to recognise — but never
     past 2x, beyond which it is inventing detail rather than revealing it. */
  var sc = box ? Math.min(2, 1200/bw) : Math.min(1, 1400/bw);
  if(!(sc>0)) sc=1;
  var w=Math.max(1,Math.round(bw*sc)), h=Math.max(1,Math.round(bh*sc));
  var c=document.createElement('canvas'); c.width=w; c.height=h;
  var x=c.getContext('2d');
  if(mirror){ x.translate(w,0); x.scale(-1,1); }
  x.drawImage(src,bx,by,bw,bh,0,0,w,h);
  try{
    var d=x.getImageData(0,0,w,h), p=d.data, i, g, lo=255, hi=0;
    for(i=0;i<p.length;i+=4){ g=(p[i]*0.299+p[i+1]*0.587+p[i+2]*0.114)|0; p[i]=p[i+1]=p[i+2]=g; if(g<lo)lo=g; if(g>hi)hi=g; }
    var rng=Math.max(1,hi-lo);
    for(i=0;i<p.length;i+=4){ var v=((p[i]-lo)*255/rng)|0; p[i]=p[i+1]=p[i+2]=v; }
    x.putImageData(d,0,0);
  }catch(e){ }
  return c;
}

/* Where the guide band lands in the SOURCE frame. The video is drawn with
   object-fit:cover, so part of the frame is off-screen; cropping the raw frame by
   the same fractions would read a strip the operator never saw. This undoes the
   cover crop first, so what is read is exactly what is inside the bright band. */
function scanBand(src,band){
  var sw=src.videoWidth||0, sh=src.videoHeight||0;
  if(!sw||!sh) return null;
  var cw=src.clientWidth||sw, ch=src.clientHeight||sh;
  var sc=Math.max(cw/sw, ch/sh) || 1;
  var vw=Math.min(sw, cw/sc), vh=Math.min(sh, ch/sc);
  return { x:(sw-vw)/2, y:(sh-vh)/2 + vh*band.y, w:vw, h:vh*band.h };
}

/* The attempts to make, in the order the scoreboard currently favours. The strips
   are offered only for a live camera, where the operator is aiming at a guide; a
   chosen photo has no guide, so it goes straight to the whole image rather than
   gambling on a strip. */
function scanPasses(src){
  var live=!!(src&&src.videoWidth), rank=[], out=[], i, j, n, d;
  for(i=0;i<LEARN.rank.length;i++){
    d=PASS_DEFS[LEARN.rank[i]];
    if(!d) continue;
    if(d.band && !live) continue;
    rank.push(d);
  }
  if(!rank.length) rank.push(PASS_DEFS.full);   /* never leave the reader with nothing to try */
  for(i=0;i<rank.length;i++){
    d=rank[i];
    n = live ? (PASS_TRIES[i]||1) : 1;
    for(j=0;j<n;j++) out.push({ k:d.k, mirror:d.mirror, band:d.band, no:j+1, of:n });
  }
  return out;
}

/* Every plausible student number in a blob of OCR text, best first. */
function scanNumbers(text){
  var toks=String(text||'').toUpperCase().split(/[^A-Z0-9]+/), out=[], seen={}, i;
  for(i=0;i<toks.length;i++){
    var t=toks[i];
    if(t.length<9) continue;
    var body=t.replace(/^M?R?U?/,'')
              .replace(/[OQD]/g,'0').replace(/[ILT|]/g,'1').replace(/Z/g,'2')
              .replace(/S/g,'5').replace(/B/g,'8').replace(/G/g,'6').replace(/A/g,'4')
              .replace(/[^0-9]/g,'');
    if(body.length<9||body.length>12) continue;
    if(!/^20[0-9][0-9]/.test(body)) continue;
    var cand='MRU'+body;
    if(!seen[cand]){ seen[cand]=1; out.push(cand); }
  }
  return out;
}

/* Ask the server about each candidate in turn; the first real student wins. */
function scanResolve(cands,done){
  if(!cands.length){ done(null); return; }
  var reg=cands.shift();
  call('ForceLookup',{regno:reg},function(d){
    if(d&&d.success){ done(d); return; }
    scanResolve(cands,done);
  });
}

function scanHit(hit){
  scanBar(null);
  qs('fc-reg').value=hit.regno;
  speak(hit.name, hit.campus);          /* name, then campus */
  fcLookup(true);                       /* one code path fills the student panel */
  if(CAM_STREAM){
    /* Keep the camera running — the operator works through a stack of cards. Just
       idle the reader until this one has been applied or skipped past. */
    CAM_HOLD=true;
    scanMsg('Read <b>'+esc(hit.regno)+'</b> &mdash; '+esc(hit.name||'')+'.<br/>Apply it, then hold up the next card.');
  } else {
    scanMsg('Read <b>'+esc(hit.regno)+'</b> &mdash; '+esc(hit.name||'')+'.');
    qs('fc-scan').style.display='none';
  }
}

/* Read one source (an <img> or a live <video> frame): as-is, then mirrored.
   done(true) when a real student was resolved. */
function scanOnce(src,quiet,done){
  tessLoad(function(ok){
    if(!ok){ scanBar(null); scanMsg('The reader could not be loaded (no route to the CDN). Type the number instead.',true); done(false); return; }
    var passes=scanPasses(src), vi=0, counted={};
    function next(){
      if(vi>=passes.length){
        scanBar(null);
        if(!quiet) scanMsg('Could not read a student number. Hold the card so the number sits in the band &mdash; or type it.',true);
        done(false); return;
      }
      var pass=passes[vi++];
      var canvas=scanPrep(src, pass.mirror, pass.band?scanBand(src,pass.band):null);
      if(!canvas){ next(); return; }
      if(!quiet){
        scanMsg('Reading the '+PASS_NAME[pass.k]+(pass.of>1?' ('+pass.no+' of '+pass.of+')':'')+'&hellip;');
        scanBar(0);
      }
      /* Scored once per card, not once per go. Counting each repeat separately
         would have the best pass punished for the very retries it earned: three
         attempts and one win reads as 33%, against 100% for a weaker pass given a
         single go. The question being scored is "when the reader gets this far,
         does this way of looking deliver?" — and that is asked once. */
      if(!counted[pass.k]){ counted[pass.k]=1; learnTry(pass.k); }
      tessWorker().then(function(w){ return w.recognize(canvas); }).then(function(res){
        var cands=scanNumbers(res&&res.data?res.data.text:'');
        if(!cands.length){ next(); return; }
        scanResolve(cands.slice(),function(hit){
          if(!hit){ next(); return; }
          learnWin(pass.k);          /* it read a real student — that is what counts */
          if(quiet && hit.regno===CAM_SKIP){ done(true); return; }   /* same card, already done */
          scanHit(hit); done(true);
        });
      }).catch(function(){ next(); });
    }
    next();
  });
}

function scanImageFromBlob(blob){
  if(!blob) return;
  camStop();
  var url=URL.createObjectURL(blob), img=new Image();
  img.onload=function(){
    var p=qs('fc-prev'); p.src=url; p.style.display='inline-block';
    scanOnce(img,false,function(){});
  };
  img.onerror=function(){ scanMsg('That file could not be read as an image.',true); };
  img.src=url;
}

/* -- Live camera -------------------------------------------------------------
   getUserMedia needs a secure context; eadmin is HTTPS, so this works. The rear
   camera is preferred but not demanded, so a laptop webcam still functions. The
   stream is ALWAYS stopped on close: leaving tracks running keeps the camera
   light on and holds the device against every other application. */
function camStop(){
  if(CAM_TIMER){ clearTimeout(CAM_TIMER); CAM_TIMER=null; }
  CAM_BUSY=false;
  if(CAM_STREAM){
    try{ CAM_STREAM.getTracks().forEach(function(t){ t.stop(); }); }catch(e){}
    CAM_STREAM=null;
  }
  var v=qs('fc-cam');
  if(v){ try{ v.pause(); v.srcObject=null; }catch(e){} v.style.display='none'; }
  var lens=qs('fc-lens'); if(lens) lens.style.display='none';
  var row=qs('fc-camrow'); if(row) row.style.display='none';
  var st=qs('fc-camstart'); if(st) st.style.display='';
  var pan=qs('fc-scan'); if(pan) pan.classList.remove('idc-scan--live');
  if(ttsReady()){ try{ window.speechSynthesis.cancel(); }catch(e){} }
  CAM_HOLD=false; CAM_SKIP=''; CAM_SIG=null; CAM_IDLE=0;
}

function camStart(){
  if(!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia){
    scanMsg('This browser cannot open a camera. Choose a photo instead.',true); return;
  }
  scanMsg('Asking for the camera&hellip;');
  navigator.mediaDevices.getUserMedia({
    video:{ facingMode:{ ideal:'environment' }, width:{ ideal:1920 }, height:{ ideal:1080 } },
    audio:false
  }).then(function(stream){
    CAM_STREAM=stream;
    var v=qs('fc-cam');
    v.srcObject=stream; v.style.display='block';
    qs('fc-lens').style.display='inline-block';
    var bd=qs('fc-band');
    if(bd){ bd.style.top=(SCAN_BAND.y*100)+'%'; bd.style.height=(SCAN_BAND.h*100)+'%'; }
    qs('fc-prev').style.display='none';
    qs('fc-camrow').style.display='flex';
    qs('fc-camstart').style.display='none';
    qs('fc-scan').classList.add('idc-scan--live');
    try{ qs('fc-reg').blur(); }catch(e){}   /* the camera is the input now */
    ttsPaint(); learnPaint();
    CAM_HOLD=false; CAM_SKIP=''; CAM_SIG=null; CAM_IDLE=0;
    v.play();
    scanMsg('Hold the card in the frame, <b>student number</b> across the band &mdash; it is read automatically.');
    camLoop();
  }).catch(function(err){
    var n=(err&&err.name)||'';
    scanMsg(n==='NotAllowedError' ? 'Camera permission was refused. Allow it in the browser, or choose a photo.'
          : n==='NotFoundError'  ? 'No camera was found on this device. Choose a photo instead.'
          : 'The camera could not be opened. Choose a photo instead.', true);
  });
}

/* Reads a frame about once a second. CAM_BUSY stops attempts overlapping — a
   recognition takes longer than the interval, and stacking them would freeze the
   tab. It stops itself the moment a student is resolved. */
/* A coarse luma fingerprint of the current frame — sixteen by sixteen is enough
   to tell "nothing has moved" from "a card just arrived", and costs a fraction of
   a millisecond against roughly a third of a second for a recognition. */
function camSig(v){
  try{
    var c=document.createElement('canvas'); c.width=16; c.height=16;
    var x=c.getContext('2d'); x.drawImage(v,0,0,16,16);
    var p=x.getImageData(0,0,16,16).data, out=[], i;
    for(i=0;i<p.length;i+=4) out.push((p[i]*0.299+p[i+1]*0.587+p[i+2]*0.114)|0);
    return out;
  }catch(e){ return null; }
}
function camSame(a,b){
  if(!a||!b||a.length!==b.length) return false;
  var d=0,i; for(i=0;i<a.length;i++) d+=Math.abs(a[i]-b[i]);
  return (d/a.length) < 6;            /* under sensor noise: the scene is unchanged */
}

function camLoop(){
  if(!CAM_STREAM) return;
  /* Idle rather than stop: the timer keeps ticking so scanning resumes by itself
     the moment the hold is lifted, with nothing for the operator to press. */
  if(CAM_HOLD || CAM_BUSY){ CAM_TIMER=setTimeout(camLoop,500); return; }
  /* Skip a frame identical to the last one read — the same pixels cannot give a
     different answer. Capped at six skips so a frame that only came into focus,
     which barely shifts the fingerprint, is still retried within a couple of
     seconds. Skipped ticks are short, so a card arriving is picked up at once. */
  var sig=camSig(qs('fc-cam'));
  if(sig && CAM_IDLE<6 && camSame(sig,CAM_SIG)){ CAM_IDLE++; CAM_TIMER=setTimeout(camLoop,300); return; }
  CAM_SIG=sig; CAM_IDLE=0;
  CAM_BUSY=true;
  scanOnce(qs('fc-cam'),true,function(){
    CAM_BUSY=false;
    if(!CAM_STREAM) return;
    CAM_TIMER=setTimeout(camLoop,900);
  });
}

/* Restart the read loop from a known state. The loop keeps its own timer alive
   while held, but an explicit press or an apply may have cleared it, so this never
   assumes one is already pending. */
/* Move every obligation still in force back by N days. Used when card printing stops:
   a student told to collect a card nobody can print is locked out for something they
   cannot do. The server refuses to pull any date forward, so this can only ever relax. */
function fcPostpone(){
  var d = window.prompt('Postpone every ID-card block still in force by how many days?', '8');
  if(d===null) return;
  d = parseInt(d,10);
  if(!(d>=1 && d<=90)){ alert('Enter a number of days between 1 and 90.'); return; }
  var why = window.prompt('Why? (recorded against every record)','card supply paused');
  if(why===null) return;
  if(!window.confirm('Hold every ID-card block for '+d+' day'+(d===1?'':'s')+'?\n\n'
      + 'Nobody already collected is affected, and nobody\u2019s block is ever brought forward.')) return;

  call('ForcePostpone',{days:d,why:why},function(r){
    if(!r||!r.success){ alert((r&&r.message)||'Could not postpone.'); return; }
    alert(r.moved+' block'+(r.moved===1?'':'s')+' held until '+r.until+'.');
    IDC.forceLoad();
  });
}

function camResume(){
  if(!CAM_STREAM) return;
  CAM_HOLD=false;
  CAM_SIG=null; CAM_IDLE=0;           /* the next frame is read, not compared away */
  if(CAM_TIMER){ clearTimeout(CAM_TIMER); CAM_TIMER=null; }
  CAM_TIMER=setTimeout(camLoop,400);
}

function camShot(){
  if(!CAM_STREAM){ scanMsg('The camera is not running.',true); return; }
  if(CAM_TIMER){ clearTimeout(CAM_TIMER); CAM_TIMER=null; }
  CAM_HOLD=false; CAM_SKIP=''; CAM_SIG=null; CAM_IDLE=0;   /* an explicit press overrides every guard */
  CAM_BUSY=true;
  scanOnce(qs('fc-cam'),false,function(){
    CAM_BUSY=false;
    if(CAM_STREAM) CAM_TIMER=setTimeout(camLoop,600);
  });
}function campusCls(c){
  var v=String(c||'').toUpperCase();
  if(v.indexOf('KAKEEKA')>=0) return 'idc-cid idc-cid--kak';
  if(v.indexOf('KIRUMBA')>=0) return 'idc-cid idc-cid--kir';
  return 'idc-cid idc-cid--na';
}
function cid(regno,campus){ return '<span class="'+campusCls(campus)+'" title="'+esc(campus||'Campus not known')+'">'+esc(regno)+'</span>'; }
/* Puts the caret after the prefix — but NOT while the camera is running. In scan
   mode the operator is holding a card, not typing: taking focus there pops the
   on-screen keyboard on a tablet and parks a blinking cursor where nobody is
   looking. While scanning, the camera is the input. */
function fcCaretEnd(){
  var i=qs('fc-reg');
  if(CAM_STREAM){ try{ i.blur(); }catch(e){} return; }
  i.focus();
  try{ i.setSelectionRange(i.value.length,i.value.length); }catch(e){}
}
function fcWho(html,cls){ var w=qs('fc-who'); w.className='idc-who'+(cls?(' idc-who--'+cls):''); w.innerHTML=html; }
function fcReady(on){ qs('fc-go').disabled=!on; }
function fcISO(d){ return d.getFullYear()+'-'+('0'+(d.getMonth()+1)).slice(-2)+'-'+('0'+d.getDate()).slice(-2); }

/* force=true comes from the Search button and skips the length gate, so a shorter
   form (MRU20264301) can be resolved on demand. Otherwise the search starts once the
   number is longer than 12 characters — the issued length is 13 — which keeps it off
   the bare MRU<year>000 prefix and off every half-typed number in between. */
function fcLookup(force){
  var reg=qs('fc-reg').value.trim().toUpperCase();
  FCFOUND=null; fcReady(false);
  if(!/^MRU[0-9]{4}[0-9]+$/.test(reg)){ fcWho('Type the rest of the student number to load their details.'); return; }
  if(!force && reg.length<13){ fcWho('Keep typing &mdash; or press <b>Search</b> to look up <b>'+esc(reg)+'</b> now.'); return; }
  fcWho('Looking up <b>'+esc(reg)+'</b>&hellip;');
  call('ForceLookup',{regno:reg},function(d){
    if(qs('fc-reg').value.trim().toUpperCase()!==reg) return;   /* a later keystroke won */
    if(!d||!d.success){ fcWho(esc((d&&d.message)||'Lookup failed.'),'err'); return; }
    FCFOUND=d;
    /* The server resolves leading-zero variants, so write the REAL number back into
       the box. The operator never has to add or delete a zero by hand. */
    if(d.regno && d.regno!==reg) qs('fc-reg').value=d.regno;
    /* Last seen tells the operator whether this student is even likely to notice the
       block — someone last seen a year ago will not be turned away by it tomorrow. */
    var seen = d.lastSeen
      ? (esc(d.lastSeenAgo||'') + (d.lastSeen?(' <span style="color:#8a94a6">('+esc(d.lastSeen)+')</span>'):''))
      : '<span style="color:#8a94a6">never signed in</span>';
    var h='<b>'+esc(d.name||'(no name on file)')+'</b> &nbsp;'+cid(d.regno,d.campus)+'<br/>'+
          '<span class="k">Programme</span> '+esc(d.programme||'-')+'<br/>'+
          '<span class="k">Campus</span> '+esc(d.campus||'-')+'<br/>'+
          '<span class="k">Last seen</span> '+seen+'<br/>'+
          '<span class="k">Card</span> '+esc(d.cardStatusLabel||d.cardStatus||'no request on file');
    var cls='';
    if(d.hasExisting){
      cls='warn';
      h+='<br/><span class="k">Existing</span> '+
         (d.existingCollected?'already marked collected':(d.existingActive?'block already in force':'block scheduled'))+
         (d.enforceFrom?(' from '+esc(d.enforceFrom)):'')+' &mdash; saving will reset it.';
      /* pre-load what was set before, so an edit does not silently change it */
      var pt=document.getElementsByName('fc-pt');
      for(var i=0;i<pt.length;i++) pt[i].checked=(pt[i].value===d.point);
      if(d.enforceFrom) qs('fc-date').value=d.enforceFrom;   /* an existing block keeps its own date */
      /* the group is shown, never applied — the operator's batch choice stands */
      if(d.group) h+='<br/><span class="k">On record</span> batch '+esc(d.group)+'.';
      if(d.note) qs('fc-note').value=d.note;
    }
    /* Campus sets the start date — but never over an existing block, whose date is a
       real obligation already in force and must not move just because it was opened. */
    var note='';
    if(!d.hasExisting){
      var days=fcCampusDays(d.campus);
      if(days!==null){
        qs('fc-date').value=fcDateIn(days);
        note = days===0 ? 'Kakeeka &mdash; starts <b>today</b> automatically.'
                        : 'Kirumba &mdash; starts <b>in 1 week</b> automatically (travel time).';
      }
    } else {
      note = 'Existing block &mdash; its own start date has been kept.';
    }
    qs('fc-whynote').innerHTML=note;
    fcMarkWhen();
    if(String(d.studentStatus||'').toUpperCase()==='ALUMNI'){ cls='warn'; h+='<br/><b>Note:</b> this student is an alumnus.'; }
    fcWho(h,cls); fcReady(true);
  });
}

/* A save does NOT empty the number box — it rewinds it to MRU<year>0000 and drops only
   the figures typed after the zeros, so the operator carries straight on with the next
   student instead of re-entering the part that never changes. */
function fcReset(keepContext){
  qs('fc-reg').value = keepContext && FCT ? fcBase() : '';
  qs('fc-note').value='';
  FCFOUND=null; fcReady(false);
  fcWho(FCT?('Ready for the next '+FCT+' student &mdash; type the figures after '+fcBase()+'.')
           :'Choose an intake year, then type the student number.');
  fcCaretEnd();
}

function fcStats(d){
  var m=[['active','In force now'],['scheduled','Scheduled'],['collected','Collected']];
  qs('idc-fstats').innerHTML=m.map(function(k){
    return '<div class="idc-stat"><div class="idc-stat__v">'+n(d[k[0]]||0)+'</div><div class="idc-stat__l">'+k[1]+'</div></div>';
  }).join('');
}
return {
 refresh:function(){readURL();stats();load();windows();stamp();
   call('Meta',{},function(d){if(d&&d.success)META={statuses:d.statuses||[],transitions:d.transitions||{}};});},
 apply:function(){state.status=qs('f-status').value;state.type=qs('f-type').value;state.card=qs('f-card').value;state.q=qs('f-q').value.trim();state.size=parseInt(qs('f-size').value,10)||50;state.page=1;stats();load();},
 go:function(p){state.page=p;load();window.scrollTo(0,0);},
 filterStatus:function(s){state.status=s;qs('f-status').value=s;state.page=1;stats();load();},
 clear:function(k){state[k]='';qs('f-'+(k==='q'?'q':k)).value='';state.page=1;stats();load();},
 clearAll:function(){state.status='';state.type='';state.card='';state.q='';state.page=1;qs('f-status').value='';qs('f-type').value='';qs('f-card').value='';qs('f-q').value='';stats();load();},
 tab:function(t){
   ['q','w','f'].forEach(function(k){
     var tb=qs('tab-'+k), vw=qs('view-'+k);
     if(tb) tb.classList.toggle('on',t===k);
     if(vw) vw.style.display=(t===k?'':'none');
   });
   if(t==='w')windows(); if(t==='f')IDC.forceLoad();
 },

 /* ── force ID collection ── */
 openForce:function(){
   fcYears(); fcGroups(); IDC.scanWire();
   qs('fc-whynote').innerHTML='';
   if(!qs('fc-date').value) qs('fc-date').value=fcISO(new Date());
   qs('idc-force').classList.add('on');
   fcMarkWhen();
   fcReset(true);
 },
 closeForce:function(){
   camStop();                    /* release the camera device */
   tessFree();                   /* and the OCR worker's memory */
   qs('idc-force').classList.remove('on');
 },
 pickYear:function(y){
   FCT=y; fcMarkYear(y);
   qs('fc-reg').value=fcBase();      /* MRU + year + 0000, caret after the zeros */
   FCFOUND=null; fcReady(false);
   fcWho('Type the figures after <b>'+fcBase()+'</b>.');
   fcCaretEnd();
 },
 regTyped:function(){
   var v=qs('fc-reg');
   v.value=v.value.toUpperCase().replace(/[^A-Z0-9]/g,'');   /* the format carries no dashes */
   clearTimeout(FCDEB); FCDEB=setTimeout(function(){fcLookup(false);},350);
 },
 searchNow:function(){ clearTimeout(FCDEB); fcLookup(true); },
 scanToggle:function(){
   var p=qs('fc-scan'), on=p.style.display==='none';
   p.style.display=on?'block':'none';
   if(on){ scanMsg(''); scanBar(null); learnPaint(); }
   else  { camStop(); }          /* never leave the camera running behind a closed panel */
 },
 scanFile:function(f){ if(f) scanImageFromBlob(f); },
 camOn:function(){ camStart(); },
 camOff:function(){ camStop(); scanMsg('Camera stopped.'); },
 camShot:function(){ camShot(); },
 voice:function(){ ttsToggle(); },
 learnReset:function(){ learnReset(); },
 postpone:function(){ fcPostpone(); },
 scanWire:function(){
   var p=qs('fc-scan');
   if(!p || p.getAttribute('data-wired')) return;
   p.setAttribute('data-wired','1');
   ['dragenter','dragover'].forEach(function(ev){
     p.addEventListener(ev,function(e){ e.preventDefault(); e.stopPropagation(); p.classList.add('drag'); });
   });
   ['dragleave','drop'].forEach(function(ev){
     p.addEventListener(ev,function(e){ e.preventDefault(); e.stopPropagation(); p.classList.remove('drag'); });
   });
   p.addEventListener('drop',function(e){
     var f=e.dataTransfer&&e.dataTransfer.files&&e.dataTransfer.files[0];
     if(f) scanImageFromBlob(f);
   });
   /* Paste works while the modal is open, so a screenshot goes straight in. */
   document.addEventListener('paste',function(e){
     if(qs('idc-force').className.indexOf('on')<0) return;
     var it=e.clipboardData&&e.clipboardData.items; if(!it) return;
     for(var i=0;i<it.length;i++){
       if(it[i].type&&it[i].type.indexOf('image')===0){
         qs('fc-scan').style.display='block';
         scanImageFromBlob(it[i].getAsFile());
         e.preventDefault(); return;
       }
     }
   });
 },
 pickGroup:function(g){ FCG=(FCG===g?'':g); fcMarkGroup(); },   /* click again to clear */
 when:function(days){
   qs('fc-date').value=fcDateIn(days);
   qs('fc-whynote').innerHTML='';        /* an explicit choice replaces the campus default */
   fcMarkWhen();
 },
 dateChanged:function(){ qs('fc-whynote').innerHTML=''; fcMarkWhen(); },
 saveForce:function(){
   if(!FCFOUND){ toast('Load a student first.',true); return; }
   var pt='AR_OFFICE', r=document.getElementsByName('fc-pt');
   for(var i=0;i<r.length;i++) if(r[i].checked) pt=r[i].value;
   var when=qs('fc-date').value||fcISO(new Date());
   var go=qs('fc-go'); go.disabled=true; go.textContent='Applying\u2026';
   call('ForceSave',{regnos:FCFOUND.regno,point:pt,enforceFrom:when,note:qs('fc-note').value||'',groupName:FCG},function(d){
     go.textContent='Apply block';
     if(!d||!d.success){ go.disabled=false; toast((d&&d.message)||'Could not apply.',true); return; }
     toast(FCFOUND.name+' must now collect from the '+d.point+(d.group?(' — batch '+d.group):'')+' (from '+d.enforceFrom+').');
     var doneReg=FCFOUND?FCFOUND.regno:'';
     fcReset(true);                 /* stay open, ready for the next student */
     IDC.forceLoad();               /* refresh the list behind the modal */
     if(CAM_STREAM){
       /* Seamless run: the camera never closed, so just note the card still in
          front of the lens and start looking for the next one. */
       CAM_SKIP=doneReg;
       camResume();                 /* start looking for the next card straight away */
       scanMsg('Applied. Hold up the next card &mdash; scanning&hellip;');
     }
   });
 },
 forceLoad:function(){
   var b=qs('idc-frows'); if(!b) return;
   b.innerHTML='<tr><td colspan="8" class="idc-empty">Loading&hellip;</td></tr>';
   call('ForceList',{state:qs('ff-state').value,q:qs('ff-q').value.trim()},function(d){
     if(!d||!d.success){ b.innerHTML='<tr><td colspan="8" class="idc-empty" style="color:#b42318">'+esc((d&&d.message)||'Error')+'</td></tr>'; return; }
     fcStats(d);
     var rows=d.rows||[];
     if(!rows.length){ b.innerHTML='<tr><td colspan="8" class="idc-empty">Nobody matches this filter.</td></tr>'; return; }
     b.innerHTML=rows.map(function(r){
       var badge = r.collected ? '<span class="idc-fbadge idc-fbadge--done">Collected</span>'
                 : (r.inForce ? '<span class="idc-fbadge idc-fbadge--on">Blocking now</span>'
                              : '<span class="idc-fbadge idc-fbadge--wait">Scheduled</span>');
       var act = r.collected ? ''
         : '<button type="button" class="idc-btn idc-btn--sm idc-btn--green" onclick="IDC.forceClear(\''+esc(r.regno)+'\',\'collected\')">Mark collected</button> '+
           '<button type="button" class="idc-btn idc-btn--sm idc-btn--ghost" onclick="IDC.forceClear(\''+esc(r.regno)+'\',\'cancel\')">Cancel</button>';
       return '<tr><td><b>'+esc(r.name||r.regno)+'</b><div style="margin-top:3px">'+cid(r.regno,r.campus)+'</div></td>'+
              '<td>'+(r.group?('<b>'+esc(r.group)+'</b>'):'<span style="color:#8a94a6">-</span>')+'</td>'+
              '<td>'+esc(r.point)+'</td><td>'+esc(r.campus||'-')+'</td>'+
              '<td style="font-size:11px">'+esc(r.from)+'</td><td>'+badge+'</td>'+
              '<td style="font-size:11px;color:#5a6472">'+esc(r.by||'-')+'</td><td style="text-align:right">'+act+'</td></tr>';
     }).join('');
   });
 },
 forceClear:function(regno,mode){
   if(mode==='cancel' && !confirm('Lift the collection block on '+regno+'?')) return;
   call('ForceClear',{regnos:regno,mode:mode},function(d){
     if(!d||!d.success){ toast((d&&d.message)||'Failed.',true); return; }
     toast(mode==='collected'?('Card marked collected for '+regno+'.'):('Block lifted for '+regno+'.'));
     IDC.forceLoad();
   });
 },
 bulkForce:function(){
   var regs=[],skipped=0;
   Object.keys(SEL).forEach(function(rn){
     for(var i=0;i<LASTROWS.length;i++) if(LASTROWS[i].requestNo===rn){
       if(String(LASTROWS[i].type).toUpperCase()==='STUDENT' && LASTROWS[i].number) regs.push(LASTROWS[i].number);
       else skipped++;
     }
   });
   if(!regs.length){ toast('Select student requests to force collection.',true); return; }
   var pt=confirm('Collect from the Academic Registrar\'s Office?\n\nOK = AR\'s Office     Cancel = ICT Office')?'AR_OFFICE':'IT_OFFICE';
   var when=prompt('Block the portal from (YYYY-MM-DD):', (function(){var d=new Date();return d.getFullYear()+'-'+('0'+(d.getMonth()+1)).slice(-2)+'-'+('0'+d.getDate()).slice(-2);})());
   if(when===null) return;
   if(!confirm('Force ID collection for '+regs.length+' student(s)?'+(skipped?('\n'+skipped+' non-student row(s) will be skipped.'):''))) return;
   call('ForceSave',{regnos:regs.join(','),point:pt,enforceFrom:when,note:'',groupName:FCG},function(d){
     if(!d||!d.success){ toast((d&&d.message)||'Failed.',true); return; }
     toast(d.applied+' student(s) must now collect from the '+d.point+(d.unknown?(' \u2014 '+d.unknown+' not found'):''), d.applied===0);
     IDC.selClear();
   });
 },
 selOne:function(rn,on){if(on)SEL[rn]=rowStatus(rn);else delete SEL[rn];var tr=qs('row-'+rn);if(tr)tr.classList.toggle('sel',on);syncBulk();syncAllCk();},
 selAll:function(on){var cks=document.getElementsByClassName('idc-rowck');for(var i=0;i<cks.length;i++){cks[i].checked=on;var rn=cks[i].value;if(on)SEL[rn]=cks[i].getAttribute('data-status');else delete SEL[rn];var tr=qs('row-'+rn);if(tr)tr.classList.toggle('sel',on);}syncBulk();},
 selClear:function(){SEL={};var cks=document.getElementsByClassName('idc-rowck');for(var i=0;i<cks.length;i++){cks[i].checked=false;var tr=qs('row-'+cks[i].value);if(tr)tr.classList.remove('sel');}qs('ck-all').checked=false;syncBulk();},
 bulk:function(a){
   var ids=Object.keys(SEL);if(!ids.length)return;var cp='';
   if(a==='READY'){cp=prompt('Collection point for '+ids.length+' card(s):','ID Card Office, Admin Block');if(cp===null)return;}
   if(!confirm('Apply "'+a+'" to '+ids.length+' selected request(s)?'))return;
   call('BatchAction',{requestNos:ids.join(','),action:a,reason:'',collectionPoint:cp},function(d){
     if(d&&d.success){toast(d.ok+' updated'+(d.fail?(', '+d.fail+' skipped'):''),d.fail>0&&d.ok===0);stats();load();}
     else toast((d&&d.message)||'Batch failed',true);});
 },
 open:function(rn){CUR=rn;qs('idc-detail').classList.add('on');qs('d-title').textContent=rn;qs('d-body').innerHTML='Loading&hellip;';qs('d-actions').innerHTML='';
  call('Detail',{requestNo:rn},function(d){
   if(!d||!d.success){qs('d-body').innerHTML='<div class="idc-empty" style="color:#b42318">'+esc((d&&d.message)||'Error')+'</div>';return;}
   var q=d.request,idn=d.identity||{},f=d.finance;
   var ph=idn.photo&&idn.photo!=='-'?('../StudentInfo/Photos/'+encodeURIComponent(idn.photo)):'';
   var h='<div class="idc-idn">'+(ph?'<img class="idc-idn__ph" src="'+ph+'" onerror="this.style.display=\'none\'"/>':'<div class="idc-idn__ph"></div>')+'<div><div style="font-size:15px;font-weight:700">'+esc(idn.name)+'</div><div style="font-size:12px;color:#5a6472">'+esc(idn.number)+' &middot; '+esc(idn.subtitle)+'</div><div style="margin-top:4px">'+chip(q.status)+'</div></div></div>';
   h+='<div class="idc-grid">'+fld('Card type',q.card_type)+fld('Requester',q.requester_type)+fld('Created',(q.created_at||'').substring(0,16))+fld('Submitted',(q.submitted_at||'').substring(0,16))+'</div>';
   if(q.card_type==='REPLACEMENT'){h+='<div class="idc-grid">'+fld('Repl. fee ref',q.replacement_fee_ref)+fld('Paid via',q.replacement_fee_method)+fld('Paid on',q.replacement_fee_date)+fld('Notes',q.replacement_fee_notes)+'</div>';}
   if(f){h+='<div class="idc-grid">'+fld('Semester fee',Number(f.fee).toLocaleString())+fld('Paid this sem',Number(f.paid).toLocaleString())+fld('Needed (10%)',Number(f.required).toLocaleString())+fld('Finance',f.eligible?('OK'+(f.flagged?' (flagged)':'')):'BELOW 10%')+'</div>';}
   if(q.halt_reason){h+='<div class="idc-fld" style="background:#fef2f2;border-color:#fecaca"><div class="idc-fld__k">Halt reason</div><div class="idc-fld__v">'+esc(q.halt_reason)+'</div></div>';}
   if(q.collection_point){h+='<div class="idc-fld" style="background:#ecfdf5;border-color:#a7f3d0;margin-top:8px"><div class="idc-fld__k">Collection</div><div class="idc-fld__v">'+esc(q.collection_point)+'</div></div>';}
   h+='<div style="font-size:11px;font-weight:700;color:#05275C;margin:16px 0 6px;text-transform:uppercase">Timeline</div><div class="idc-tl">'+(d.timeline||[]).map(function(e){return '<div class="idc-tl__i"><b>'+esc(e.to)+'</b> '+(e.actor?('&middot; '+esc(e.actor)):'')+(e.channel?(' <span style="font-size:10px;color:#8a94a6">['+esc(e.channel)+']</span>'):'')+(e.note?('<div class="idc-note">'+esc(e.note)+'</div>'):'')+'<div class="idc-tl__t">'+esc((e.at||'').substring(0,16))+'</div></div>';}).join('')+'</div>';
   CURST=q.status;
   qs('d-body').innerHTML=h;
   qs('d-actions').innerHTML=actionsFor(q.status);
  });},
 close:function(){qs('idc-detail').classList.remove('on');},

 /* ---------- administrative status change ----------
    The ordinary buttons walk the lifecycle one legal step at a time. This sets it.
    Both kinds of move are offered in one list and each is LABELLED, so the operator
    can see at a glance which are normal and which are not, rather than discovering it
    from a rejection. Anything abnormal needs the tick and a reason, and lands in the
    request history as an override. Terminal states are included on purpose: a request
    marked COLLECTED by mistake cannot be undone any other way. */
 openStatus:function(){
   if(!CUR){toast('Open a request first',true);return;}
   qs('ss-who').textContent=CUR+(CURST?('  ·  currently '+CURST):'');
   var sel=qs('ss-to'),legal=(META.transitions&&META.transitions[CURST])||[];
   var opts='<option value="">Select a status&hellip;</option>';
   (META.statuses||[]).forEach(function(s){
     if(s===CURST)return;                       // no-op; nothing to choose
     var ok=legal.indexOf(s)>=0;
     opts+='<option value="'+esc(s)+'">'+esc(s)+(ok?'   (normal next step)':'   (override)')+'</option>';
   });
   sel.innerHTML=opts;
   qs('ss-reason').value='';
   qs('ss-override').checked=false;
   qs('ss-msg').style.display='none';
   IDC.statusPicked();
   qs('idc-setst').classList.add('on');
 },
 closeStatus:function(){qs('idc-setst').classList.remove('on');},
 statusPicked:function(){
   var to=qs('ss-to').value,legal=(META.transitions&&META.transitions[CURST])||[];
   var hint=qs('ss-hint'),ov=qs('ss-ovwrap'),go=qs('ss-go'),reqd=qs('ss-reqd');
   if(!to){hint.innerHTML='';ov.style.display='none';reqd.textContent='';go.disabled=true;return;}
   go.disabled=false;
   if(legal.indexOf(to)>=0){
     hint.innerHTML='<span style="color:#128a4a">This is a normal step in the lifecycle. It behaves exactly like the action buttons, and the requester is notified as usual.</span>';
     ov.style.display='none';
     // HALTED always needs a reason — the requester is shown it and has to act on it.
     reqd.textContent=(to==='HALTED')?'(required)':'(optional)';
   }else{
     hint.innerHTML='<span style="color:#b42318"><b>'+esc(CURST)+' &rarr; '+esc(to)+' is not a normal step.</b> '+
       'It can still be done, but it will be recorded as an override against your name.</span>';
     ov.style.display='block';
     reqd.textContent='(required for an override)';
   }
 },
 applyStatus:function(){
   var to=qs('ss-to').value,reason=qs('ss-reason').value.replace(/^\s+|\s+$/g,'');
   var legal=(META.transitions&&META.transitions[CURST])||[];
   var isOv=legal.indexOf(to)<0, ov=qs('ss-override').checked;
   var msg=qs('ss-msg');
   function bad(t){msg.style.display='block';msg.style.background='#fef2f2';msg.style.color='#b42318';msg.style.border='1px solid #fecaca';msg.textContent=t;}
   if(!to){bad('Choose a status.');return;}
   if(isOv&&!ov){bad('This is not a normal step. Tick the box to confirm you mean it.');return;}
   if((isOv||to==='HALTED')&&!reason){bad('Give a reason — it is written into the request history.');return;}
   if(isOv&&!confirm('Force '+CUR+' from '+CURST+' to '+to+'?\n\nThis is not a normal step. It will be recorded as an override with your name and reason.'))return;
   var go=qs('ss-go');go.disabled=true;
   call('SetStatus',{requestNo:CUR,toStatus:to,reason:reason,allowOverride:ov},function(d){
     go.disabled=false;
     if(d&&d.success){IDC.closeStatus();toast(d.message||('Set to '+to),false);IDC.open(CUR);stats();load();}
     else bad((d&&d.message)||'Could not change the status.');
   });
 },
 act:function(a){var reason='',cp='';
  if(a==='HALT'){reason=prompt('Reason for halting this request:');if(!reason)return;}
  if(a==='READY'){cp=prompt('Collection point (location + times):','ID Card Office, Admin Block');if(cp===null)return;}
  if(a==='CANCEL'&&!confirm('Cancel this request?'))return;
  call('Action',{requestNo:CUR,action:a,reason:reason,collectionPoint:cp},function(d){
   if(d&&d.success){toast('Updated: '+d.status);IDC.open(CUR);stats();load();}else{toast((d&&d.message)||'Failed',true);}});},
 createWindow:function(){var t=qs('w-title').value,o=qs('w-open').value,c=qs('w-close').value;if(!t||!o||!c){toast('Title, open and close are required',true);return;}
  call('CreateWindow',{title:t,scope:qs('w-scope').value,opensAt:o.replace('T',' '),closesAt:c.replace('T',' '),notes:''},function(d){if(d&&d.success){toast('Window created');qs('w-title').value='';windows();}else toast((d&&d.message)||'Failed',true);});},
 setWin:function(id,active){call('SetWindow',{id:id,active:active},function(d){if(d&&d.success){toast(d.message);windows();}else toast((d&&d.message)||'Failed',true);});}
};

function syncAllCk(){var cks=document.getElementsByClassName('idc-rowck');var all=cks.length>0;for(var i=0;i<cks.length;i++)if(!cks[i].checked){all=false;break;}qs('ck-all').checked=all;}
function fld(k,v){return '<div class="idc-fld"><div class="idc-fld__k">'+k+'</div><div class="idc-fld__v">'+esc(v||'—')+'</div></div>';}
function actionsFor(st){var b='';
 if(st==='SUBMITTED'){b+=btn('Approve','APPROVE','green')+btn('Halt','HALT','danger');}
 else if(st==='APPROVED'){b+=btn('Mark Printed','PRINTED','')+btn('Halt','HALT','danger');}
 else if(st==='PRINTED'){b+=btn('Mark Ready','READY','green');}
 else if(st==='READY'){b+=btn('Mark Collected','COLLECTED','green');}
 else if(st==='HALTED'){b+='<span style="font-size:11px;color:#8a94a6;align-self:center">Awaiting requester resubmission.</span>';}
 if(st!=='COLLECTED'&&st!=='CANCELLED'&&st!=='PRINTED'&&st!=='READY'&&st!=='APPROVED'){b+=btn('Cancel','CANCEL','ghost');}
 // Every state gets this, including the terminal ones — a request marked COLLECTED by
 // mistake is exactly the case the ordinary buttons cannot reach.
 b+='<button type="button" class="idc-btn idc-btn--ghost" onclick="IDC.openStatus()">Change status&hellip;</button>';
 return b+'<button type="button" class="idc-btn idc-btn--ghost" onclick="IDC.close()">Close</button>';}
function btn(lbl,act,cls){return '<button type="button" class="idc-btn '+(cls?('idc-btn--'+cls):'')+'" onclick="IDC.act(\''+act+'\')">'+lbl+'</button>';}
function windows(){var b=qs('idc-wins');call('Windows',{},function(d){
  if(!d||!d.success||!d.windows.length){b.innerHTML='<tr><td colspan="6" class="idc-empty">No windows yet — requests are open.</td></tr>';return;}
  b.innerHTML=d.windows.map(function(w){return '<tr><td><b>'+esc(w.title)+'</b></td><td>'+esc(w.scope)+'</td><td style="font-size:11px">'+esc((w.opensAt||'').substring(0,16))+'</td><td style="font-size:11px">'+esc((w.closesAt||'').substring(0,16))+'</td><td>'+(w.open?'<span class="idc-chip st-READY">OPEN</span>':(w.active?'<span class="idc-chip st-SUBMITTED">SCHEDULED</span>':'<span class="idc-chip st-COLLECTED">CLOSED</span>'))+'</td><td><button type="button" class="idc-btn idc-btn--ghost idc-btn--sm" onclick="IDC.setWin('+w.id+','+(w.active?'false':'true')+')">'+(w.active?'Close':'Activate')+'</button></td></tr>';}).join('');});}
function stamp(){var d=new Date();qs('idc-updated').textContent='Updated '+d.toLocaleTimeString();}
})();
IDC.refresh();
</script>
</asp:Content>
