<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="GraduationCentre.aspx.cs" Inherits="COOPERP_NewScreens_GraduationCentre" Title="Graduation Centre - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style type="text/css">
/* =====================================================================
   Graduation Centre. Plan: GRADUATION_CENTRE_PLAN.md

   The vocabulary is fixed and deliberate: candidate, cleared, held,
   graduation list. Never "passed", never "approve" — Senate approves;
   this screen produces the list Senate approves.
   ===================================================================== */
.gc{ --navy:#05275C; --accent:#174DA4; --surface:#f5f7fa; --line:#e0e5ed; --ink:#1a1a2e;
     --ink2:#4b5563; --mute:#8a94a6;
     --ok:#0b5c3a; --ok-bg:#e6f4ec; --ok-line:#c3e6cb;
     --warn:#92400e; --warn-bg:#fff8e1; --warn-line:#fde68a;
     --bad:#8c2019; --bad-bg:#fdecea; --bad-line:#f5c6c2;
     padding:12px; color:var(--ink); font-size:12.5px; min-width:0; max-width:100%;
     box-sizing:border-box; }
.gc *{box-sizing:border-box;}

/* The master's content column will not shrink below its content without this. */
.cd-content{min-width:0;}

.gc-hd{background:var(--navy);padding:13px 16px;margin:-12px -12px 12px;display:flex;
  align-items:center;gap:12px;flex-wrap:wrap;border-bottom:3px solid #041d45;}
.gc-hd__ic{width:34px;height:34px;background:rgba(255,255,255,.12);display:flex;align-items:center;
  justify-content:center;border-radius:4px;flex:0 0 34px;color:#fff;}
.gc-hd__t{font-size:15.5px;font-weight:700;color:#fff;margin:0;line-height:1.2;}
.gc-hd__s{font-size:11.5px;color:rgba(255,255,255,.78);margin-top:2px;}
.gc-hd__scope{margin-left:auto;font-size:11px;color:rgba(255,255,255,.85);text-align:right;
  background:rgba(255,255,255,.10);padding:5px 10px;border-radius:3px;}

/* ── tabs ── */
.gc-tabs{display:flex;gap:2px;border-bottom:2px solid var(--line);margin-bottom:12px;flex-wrap:wrap;}
.gc-tab{appearance:none;border:0;background:none;padding:8px 14px;font-size:12.5px;font-weight:600;
  color:var(--ink2);cursor:pointer;border-bottom:2px solid transparent;margin-bottom:-2px;
  font-family:inherit;display:flex;align-items:center;gap:7px;}
.gc-tab:hover{color:var(--accent);}
.gc-tab.on{color:var(--navy);border-bottom-color:var(--navy);}
.gc-tab__n{background:var(--surface);border:1px solid var(--line);border-radius:9px;padding:0 6px;
  font-size:10.5px;font-weight:700;color:var(--ink2);min-width:18px;text-align:center;}
.gc-tab.on .gc-tab__n{background:#e8eefb;border-color:#c7d7f5;color:var(--accent);}

/* ── filters ── */
.gc-filters{display:flex;gap:8px;flex-wrap:wrap;align-items:flex-end;background:#fff;
  border:1px solid var(--line);border-radius:4px;padding:10px 12px;margin-bottom:12px;}
.gc-f{display:flex;flex-direction:column;gap:3px;min-width:0;}
.gc-f label{font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:var(--mute);}
.gc-f select,.gc-f input{border:1px solid var(--line);border-radius:0;padding:5px 7px;font-size:12px;
  font-family:inherit;color:var(--ink);background:#fff;min-width:0;}
.gc-f select:focus,.gc-f input:focus{outline:none;border-color:var(--accent);}
.gc-btn{border:1px solid var(--line);background:#fff;color:var(--ink2);padding:6px 12px;font-size:12px;
  font-weight:600;cursor:pointer;font-family:inherit;border-radius:0;}
.gc-btn:hover{border-color:var(--accent);color:var(--accent);}
.gc-btn--p{background:var(--navy);border-color:var(--navy);color:#fff;}
.gc-btn--p:hover{background:#0a3574;color:#fff;}
.gc-btn--d{border-color:var(--bad-line);color:var(--bad);}
.gc-btn--d:hover{background:var(--bad-bg);}
.gc-btn[disabled]{opacity:.45;cursor:not-allowed;}

/* ── KPI strip ── */
.gc-kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px;margin-bottom:12px;}
.gc-kpi{background:#fff;border:1px solid var(--line);border-radius:4px;padding:11px 13px;cursor:pointer;
  border-left:3px solid var(--line);}
.gc-kpi:hover{border-left-color:var(--accent);}
.gc-kpi b{display:block;font-size:22px;font-weight:700;color:var(--navy);line-height:1.1;
  font-variant-numeric:tabular-nums;}
.gc-kpi span{display:block;font-size:10.5px;text-transform:uppercase;letter-spacing:.4px;
  color:var(--mute);margin-top:3px;font-weight:700;}
.gc-kpi small{display:block;font-size:10.5px;color:var(--ink2);margin-top:4px;line-height:1.4;}
.gc-kpi--ok{border-left-color:var(--ok);} .gc-kpi--ok b{color:var(--ok);}
.gc-kpi--warn{border-left-color:#d97706;} .gc-kpi--warn b{color:var(--warn);}
.gc-kpi--bad{border-left-color:#b3261e;} .gc-kpi--bad b{color:var(--bad);}

/* ── cards & tables ── */
.gc-card{background:#fff;border:1px solid var(--line);border-radius:4px;margin-bottom:12px;overflow:hidden;}
.gc-card__h{padding:9px 13px;background:var(--surface);border-bottom:1px solid var(--line);
  font-size:12px;font-weight:700;color:var(--navy);display:flex;align-items:center;gap:8px;flex-wrap:wrap;}
.gc-card__h small{font-weight:400;color:var(--mute);font-size:10.5px;}
.gc-card__b{padding:12px 13px;}
.gc-tblwrap{overflow-x:auto;max-width:100%;}
.gc-tbl{width:100%;border-collapse:collapse;font-size:12px;}
.gc-tbl th{background:var(--surface);text-align:left;padding:7px 9px;font-size:10px;font-weight:700;
  text-transform:uppercase;letter-spacing:.4px;color:var(--mute);border-bottom:1px solid var(--line);
  white-space:nowrap;position:sticky;top:0;z-index:1;}
.gc-tbl td{padding:7px 9px;border-bottom:1px solid #eef2f7;vertical-align:middle;}
.gc-tbl tbody tr:hover{background:#f8fbff;}
.gc-tbl tbody tr.is-click{cursor:pointer;}
.gc-num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap;}
.gc-reg{font-weight:700;color:var(--navy);white-space:nowrap;}
.gc-sub{color:var(--mute);font-size:10.5px;}

/* ── chips ── */
.gc-chip{display:inline-block;font-size:9.5px;font-weight:700;text-transform:uppercase;
  letter-spacing:.4px;padding:2px 7px;border-radius:2px;white-space:nowrap;}
.gc-chip--ready{background:var(--ok-bg);color:var(--ok);}
.gc-chip--warn{background:var(--warn-bg);color:var(--warn);}
.gc-chip--blocked{background:var(--bad-bg);color:var(--bad);}
.gc-chip--listed{background:#e8eefb;color:var(--accent);}
.gc-chip--held{background:#f3e8ff;color:#6b21a8;}

/* credits bar — the comparison the whole module turns on */
.gc-bar{display:flex;align-items:center;gap:7px;min-width:130px;}
.gc-bar__t{flex:1 1 auto;height:6px;background:#eef2f7;border-radius:3px;overflow:hidden;min-width:44px;}
.gc-bar__f{height:100%;background:var(--ok);}
.gc-bar__f.is-short{background:#d97706;}
.gc-bar__f.is-none{background:#c3ccd9;}
.gc-bar__v{font-size:10.5px;color:var(--ink2);white-space:nowrap;font-variant-numeric:tabular-nums;}

/* ── evidence panel ── */
.gc-ov{position:fixed;inset:0;background:rgba(5,39,92,.55);z-index:1200;display:none;}
.gc-panel{position:fixed;top:0;right:0;bottom:0;width:min(760px,96vw);background:#fff;z-index:1201;
  display:none;flex-direction:column;box-shadow:-8px 0 32px rgba(5,39,92,.28);}
.gc-panel__h{padding:12px 16px;background:var(--navy);color:#fff;display:flex;align-items:flex-start;
  gap:10px;flex-shrink:0;}
.gc-panel__h b{font-size:14px;display:block;}
.gc-panel__h span{font-size:11.5px;color:rgba(255,255,255,.78);display:block;margin-top:2px;}
.gc-panel__x{margin-left:auto;background:none;border:0;color:#fff;font-size:22px;line-height:1;
  cursor:pointer;padding:0 4px;opacity:.8;}
.gc-panel__x:hover{opacity:1;}
.gc-panel__b{flex:1 1 auto;overflow-y:auto;padding:14px 16px;}
.gc-panel__f{flex-shrink:0;border-top:1px solid var(--line);background:var(--surface);padding:11px 16px;
  display:flex;gap:8px;flex-wrap:wrap;align-items:center;}

.gc-find{border:1px solid var(--line);border-radius:3px;margin-bottom:7px;padding:8px 11px;
  display:flex;gap:10px;align-items:flex-start;}
.gc-find__n{flex:0 0 150px;font-size:11px;font-weight:700;color:var(--navy);}
.gc-find__d{flex:1 1 auto;font-size:12px;line-height:1.5;color:var(--ink2);min-width:0;}
.gc-find--PASS{border-left:3px solid var(--ok);}
.gc-find--WARN{border-left:3px solid #d97706;background:#fffdf7;}
.gc-find--BLOCK{border-left:3px solid #b3261e;background:#fffafa;}
.gc-find--NA{border-left:3px solid #c3ccd9;background:#fafbfc;}

.gc-grid2{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:7px 14px;
  margin-bottom:12px;}
.gc-grid2 div{font-size:12px;padding:5px 0;border-bottom:1px dashed #eef2f7;min-width:0;}
.gc-grid2 span{display:block;font-size:9.5px;text-transform:uppercase;letter-spacing:.3px;
  color:var(--mute);font-weight:700;}
.gc-grid2 b{color:var(--navy);word-break:break-word;}

.gc-sec{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.5px;color:var(--mute);
  margin:16px 0 7px;padding-bottom:4px;border-bottom:1px solid var(--line);}

.gc-note{font-size:11.5px;line-height:1.55;padding:9px 11px;border-radius:3px;margin-bottom:10px;}
.gc-note--warn{background:var(--warn-bg);border:1px solid var(--warn-line);border-left:3px solid #d97706;color:var(--warn);}
.gc-note--bad{background:var(--bad-bg);border:1px solid var(--bad-line);border-left:3px solid #b3261e;color:var(--bad);}
.gc-note--info{background:#f0f6ff;border:1px solid #cddffa;border-left:3px solid var(--accent);color:#1e3a6b;}

.gc-empty{text-align:center;padding:30px 16px;color:var(--mute);font-size:12.5px;}
.gc-load{text-align:center;padding:26px;color:var(--mute);font-size:12px;}
.gc-pager{display:flex;gap:7px;align-items:center;justify-content:flex-end;padding:9px 13px;
  border-top:1px solid var(--line);font-size:11.5px;color:var(--ink2);flex-wrap:wrap;}

.gc-msg{position:fixed;left:50%;transform:translateX(-50%);bottom:22px;z-index:1400;padding:10px 16px;
  border-radius:3px;font-size:12.5px;font-weight:600;display:none;max-width:min(560px,92vw);
  box-shadow:0 6px 20px rgba(0,0,0,.18);}
.gc-msg--ok{background:var(--ok-bg);border:1px solid var(--ok-line);color:var(--ok);}
.gc-msg--bad{background:var(--bad-bg);border:1px solid var(--bad-line);color:var(--bad);}

.gc-ta{width:100%;border:1px solid var(--line);padding:7px 9px;font-size:12px;font-family:inherit;
  color:var(--ink);resize:vertical;}
.gc-ta:focus{outline:none;border-color:var(--accent);}
.gc-struct td.miss{background:#fff8e1;}
.gc-struct td.fail{background:#fdecea;}
@media(max-width:760px){ .gc-find__n{flex-basis:100%;} .gc-find{flex-wrap:wrap;} }
</style>
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="gc">

  <div class="gc-hd">
    <div class="gc-hd__ic">
      <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 10v6M2 10l10-5 10 5-10 5z"></path><path d="M6 12v5c3 3 9 3 12 0v-5"></path></svg>
    </div>
    <div>
      <h1 class="gc-hd__t">Graduation Centre</h1>
      <div class="gc-hd__s">Who is ready to graduate, on what evidence, and who said so</div>
    </div>
    <div class="gc-hd__scope" id="gcScope">&nbsp;</div>
  </div>

  <div id="gcNoAccess" class="gc-note gc-note--warn" style="display:none;"></div>

  <div id="gcMain">
    <div class="gc-tabs" id="gcTabs">
      <button type="button" class="gc-tab on" data-tab="overview">Overview</button>
      <button type="button" class="gc-tab" data-tab="candidates">Candidates <span class="gc-tab__n" id="nCand">–</span></button>
      <button type="button" class="gc-tab" data-tab="list">Graduation list <span class="gc-tab__n" id="nList">–</span></button>
      <button type="button" class="gc-tab" data-tab="held">Held <span class="gc-tab__n" id="nHeld">–</span></button>
    </div>

    <div class="gc-filters">
      <div class="gc-f" style="flex:0 0 150px;">
        <label for="fYear">Graduation year</label>
        <select id="fYear"></select>
      </div>
      <div class="gc-f" style="flex:0 0 235px;">
        <label for="fFocus">Who to show</label>
        <select id="fFocus">
          <option value="cycle">This cycle &mdash; finishing now</option>
          <option value="all">Everyone not yet graduated</option>
        </select>
      </div>
      <div class="gc-f" style="flex:1 1 180px;">
        <label for="fFac">Faculty</label>
        <select id="fFac"><option value="">All faculties</option></select>
      </div>
      <div class="gc-f" style="flex:1 1 180px;">
        <label for="fDep">Department</label>
        <select id="fDep"><option value="">All departments</option></select>
      </div>
      <div class="gc-f" style="flex:1 1 200px;">
        <label for="fProg">Programme</label>
        <select id="fProg"><option value="">All programmes</option></select>
      </div>
      <div class="gc-f" style="flex:0 0 110px;">
        <label for="fIntake">Intake</label>
        <select id="fIntake"><option value="">All intakes</option></select>
      </div>
      <div class="gc-f" style="flex:0 0 130px;">
        <label for="fFin" title="The academic year the student last sat a paper">Finished in</label>
        <select id="fFin"><option value="">Any year</option></select>
      </div>
      <div class="gc-f" style="flex:1 1 150px;">
        <label for="fQ">Student number or name</label>
        <input type="text" id="fQ" placeholder="Search&hellip;" autocomplete="off" />
      </div>
      <button type="button" class="gc-btn gc-btn--p" id="btnApply">Apply</button>
      <button type="button" class="gc-btn" id="btnReset">Reset</button>
    </div>

    <!-- ══ OVERVIEW ══ -->
    <div id="tabOverview">
      <div class="gc-note gc-note--info" id="gcFocusNote" style="display:none;"></div>
      <div id="gcIntegrity"></div>
      <div class="gc-kpis" id="gcKpis"></div>
      <div class="gc-card">
        <div class="gc-card__h">Where the blockers are <small>what is standing between the candidates and a list</small></div>
        <div class="gc-card__b" id="gcBlockers"><div class="gc-load">Loading&hellip;</div></div>
      </div>
      <div class="gc-card">
        <div class="gc-card__h">Progress by programme <small>candidates, cleared, held, and still to review</small></div>
        <div class="gc-tblwrap" style="max-height:440px;">
          <table class="gc-tbl"><thead><tr>
            <th>Programme</th><th class="gc-num">Candidates</th><th class="gc-num">On a list</th>
            <th class="gc-num">Held</th><th class="gc-num">Failed papers</th><th class="gc-num">To review</th>
          </tr></thead><tbody id="gcProgBody"></tbody></table>
        </div>
      </div>
    </div>

    <!-- ══ CANDIDATES ══ -->
    <div id="tabCandidates" style="display:none;">
      <div class="gc-card">
        <div class="gc-card__h">
          <span id="candTitle">Candidates</span>
          <small id="candMeta">&nbsp;</small>
          <span style="margin-left:auto;display:flex;gap:7px;align-items:center;">
            <button type="button" class="gc-btn gc-btn--p" id="btnBulk" disabled="disabled">Clear selected</button>
            <select id="fReady" class="gc-btn" style="padding:5px 8px;">
              <option value="">Every readiness</option>
              <option value="ready">Ready only</option>
              <option value="warn">Needs a look</option>
              <option value="blocked">Blocked</option>
            </select>
          </span>
        </div>
        <div class="gc-note gc-note--info" style="margin:0;border-radius:0;border-left:0;border-right:0;border-top:0;font-size:11px;">
          Only candidates with nothing outstanding can be selected. Anything carrying a warning or a
          block has to be opened and decided on its own — that is what the warning is for.
        </div>
        <div class="gc-tblwrap">
          <table class="gc-tbl"><thead><tr>
            <th style="width:26px;"><input type="checkbox" id="ckAll" title="Select every ready candidate on this page" /></th>
            <th>Student</th><th>Programme</th><th>Intake</th><th style="min-width:150px;">Credits</th>
            <th class="gc-num">CGPA</th><th>Class</th><th>Readiness</th><th></th>
          </tr></thead><tbody id="candBody"></tbody></table>
        </div>
        <div class="gc-pager" id="candPager"></div>
      </div>
    </div>

    <!-- ══ GRADUATION LIST ══ -->
    <div id="tabList" style="display:none;">
      <div class="gc-card">
        <div class="gc-card__h">
          <span id="listTitle">Graduation list</span>
          <small id="listMeta">&nbsp;</small>
          <span style="margin-left:auto;display:flex;gap:7px;">
            <button type="button" class="gc-btn" id="btnPrint">Print for Senate</button>
            <button type="button" class="gc-btn" id="btnExport">Export CSV</button>
          </span>
        </div>
        <div class="gc-tblwrap">
          <table class="gc-tbl"><thead><tr>
            <th>Student</th><th>Programme</th><th class="gc-num">CGPA</th><th>Class</th>
            <th>Cleared by</th><th>Transcript</th><th></th>
          </tr></thead><tbody id="listBody"></tbody></table>
        </div>
      </div>
    </div>

    <!-- ══ HELD ══ -->
    <div id="tabHeld" style="display:none;">
      <div class="gc-note gc-note--info">
        A hold nobody revisits is a student who quietly never graduates. Oldest first.
      </div>
      <div class="gc-card">
        <div class="gc-card__h"><span>Held candidates</span><small id="heldMeta">&nbsp;</small></div>
        <div class="gc-tblwrap">
          <table class="gc-tbl"><thead><tr>
            <th>Student</th><th>Programme</th><th>Reason</th><th>Held by</th><th>When</th><th></th>
          </tr></thead><tbody id="heldBody"></tbody></table>
        </div>
      </div>
    </div>
  </div>
</div>

<!-- ══ EVIDENCE PANEL ══ -->
<div class="gc-ov" id="gcOv"></div>
<div class="gc-panel" id="gcPanel" role="dialog" aria-modal="true">
  <div class="gc-panel__h">
    <div style="min-width:0;">
      <b id="pName">Student</b>
      <span id="pSub">&nbsp;</span>
    </div>
    <button type="button" class="gc-panel__x" id="pClose" aria-label="Close">&times;</button>
  </div>
  <div class="gc-panel__b" id="pBody"><div class="gc-load">Loading&hellip;</div></div>
  <div class="gc-panel__f" id="pFoot"></div>
</div>

<div class="gc-msg" id="gcMsg"></div>

<script type="text/javascript">
(function(){
'use strict';

// ── plumbing ────────────────────────────────────────────────────────
function qs(id){ return document.getElementById(id); }
function esc(s){ return String(s==null?'':s).replace(/[&<>"']/g,function(m){
    return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]; }); }
function n1(v){ return (Math.round((+v||0)*10)/10).toFixed(1); }
function n2(v){ return (Math.round((+v||0)*100)/100).toFixed(2); }
function n0(v){ return String(Math.round(+v||0)); }

function ajax(method,params,cb){
    var x=new XMLHttpRequest();
    x.open('POST','GraduationCentre.aspx/'+method,true);
    x.setRequestHeader('Content-Type','application/json; charset=utf-8');
    x.timeout=180000;
    x.onload=function(){ try{ var o=JSON.parse(x.responseText); cb(typeof o.d==='string'?JSON.parse(o.d):o.d); }
                         catch(e){ cb({success:false,message:'The server did not return usable data.'}); } };
    x.onerror=function(){ cb({success:false,message:'Network error.'}); };
    x.ontimeout=function(){ cb({success:false,message:'That took too long — narrow the filters and try again.'}); };
    x.send(JSON.stringify(params||{}));
}

var msgT=null;
function msg(text,good){
    var m=qs('gcMsg');
    m.className='gc-msg gc-msg--'+(good?'ok':'bad');
    m.textContent=text; m.style.display='block';
    clearTimeout(msgT); msgT=setTimeout(function(){ m.style.display='none'; }, good?4200:7000);
}

// ── state, in the URL so a view is linkable ─────────────────────────
var boot=null, tab='overview', page=1, curReg=null, curStudent=null;

function readUrl(){
    var p=new URLSearchParams(location.search);
    tab=p.get('tab')||'overview';
    page=parseInt(p.get('page')||'1',10)||1;
    return { year:p.get('year')||'', fac:p.get('faculty')||'', dep:p.get('dept')||'',
             prog:p.get('prog')||'', q:p.get('q')||'', ready:p.get('ready')||'',
             intake:p.get('intake')||'', fin:p.get('fin')||'', focus:p.get('focus')||'' };
}
function pushUrl(){
    var p=new URLSearchParams();
    p.set('tab',tab);
    if(qs('fYear').value) p.set('year',qs('fYear').value);
    if(qs('fFac').value)  p.set('faculty',qs('fFac').value);
    if(qs('fDep').value)  p.set('dept',qs('fDep').value);
    if(qs('fProg').value) p.set('prog',qs('fProg').value);
    if(qs('fFocus').value && qs('fFocus').value!=='cycle') p.set('focus',qs('fFocus').value);
    if(qs('fIntake').value) p.set('intake',qs('fIntake').value);
    if(qs('fFin').value)   p.set('fin',qs('fFin').value);
    if(qs('fQ').value)    p.set('q',qs('fQ').value);
    if(qs('fReady').value)p.set('ready',qs('fReady').value);
    if(page>1) p.set('page',String(page));
    history.replaceState(null,'', location.pathname+'?'+p.toString());
}

function cfg(extra){
    var c={ acadYear:qs('fYear').value, faculty:qs('fFac').value, department:qs('fDep').value,
            programme:qs('fProg').value, entryYear:qs('fIntake').value, finishedIn:qs('fFin').value,
            focus:qs('fFocus').value,
            search:qs('fQ').value.trim(),
            readiness:qs('fReady').value, page:String(page), size:'50' };
    if(extra) for(var k in extra) if(extra.hasOwnProperty(k)) c[k]=extra[k];
    return JSON.stringify(c);
}

// ── filter cascade, the Export Summary Report's behaviour ───────────
function fillSel(id,items,allLabel,keep){
    var el=qs(id), cur=keep?el.value:'';
    var h=allLabel?'<option value="">'+esc(allLabel)+'</option>':'';
    for(var i=0;i<items.length;i++)
        h+='<option value="'+esc(items[i].v)+'"'+
           (items[i].fac!==undefined?' data-fac="'+esc(items[i].fac)+'"':'')+
           (items[i].dep!==undefined?' data-dep="'+esc(items[i].dep)+'"':'')+
           '>'+esc(items[i].t)+'</option>';
    el.innerHTML=h;
    if(cur) el.value=cur;
}
function cascade(){
    var fac=qs('fFac').value, dep=qs('fDep').value;
    var ds=qs('fDep').options, i, o;
    for(i=0;i<ds.length;i++){ o=ds[i];
        o.style.display=(!o.value||!fac||o.getAttribute('data-fac')===fac)?'':'none'; }
    if(dep && qs('fDep').selectedOptions[0] && qs('fDep').selectedOptions[0].style.display==='none') qs('fDep').value='';
    var ps=qs('fProg').options;
    for(i=0;i<ps.length;i++){ o=ps[i];
        var okF=(!fac||o.getAttribute('data-fac')===fac);
        var okD=(!qs('fDep').value||o.getAttribute('data-dep')===qs('fDep').value);
        o.style.display=(!o.value||(okF&&okD))?'':'none'; }
    if(qs('fProg').selectedOptions[0] && qs('fProg').selectedOptions[0].style.display==='none') qs('fProg').value='';
}

// ── tabs ────────────────────────────────────────────────────────────
function showTab(t){
    tab=t; page=1;
    var b=qs('gcTabs').querySelectorAll('.gc-tab'), i;
    for(i=0;i<b.length;i++) b[i].className='gc-tab'+(b[i].getAttribute('data-tab')===t?' on':'');
    qs('tabOverview').style.display  = t==='overview'  ?'block':'none';
    qs('tabCandidates').style.display= t==='candidates'?'block':'none';
    qs('tabList').style.display      = t==='list'      ?'block':'none';
    qs('tabHeld').style.display      = t==='held'      ?'block':'none';
    pushUrl(); load();
}

function load(){
    if(tab==='overview') loadOverview();
    else if(tab==='candidates') loadCandidates('pending');
    else if(tab==='list') loadList();
    else loadHeld();
}

// ── overview ────────────────────────────────────────────────────────
function loadOverview(){
    qs('gcBlockers').innerHTML='<div class="gc-load">Loading&hellip;</div>';
    qs('gcProgBody').innerHTML='';
    ajax('GetOverview',{configJson:cfg()},function(d){
        if(!d||!d.success){ msg((d&&d.message)||'Could not load the overview.',false); return; }
        var o=d.overview;
        qs('nCand').textContent=o.candidates;
        qs('nList').textContent=o.listed;
        qs('nHeld').textContent=o.held;

        var fn=qs('gcFocusNote');
        if(o.focusLabel){
            fn.style.display='block';
            fn.innerHTML = (o.focusFrom
                ? '<b>Showing the '+esc(o.focusTo)+' graduating cycle.</b> Everything on this page &mdash; '+
                  'the counts below, the candidate queue and the programme table &mdash; covers students who '+
                  'last sat a paper in <b>'+esc(o.focusFrom)+'</b> or <b>'+esc(o.focusTo)+'</b>. '+
                  'Switch <i>Who to show</i> to see everyone who has ever reached a final year and never graduated.'
                : '<b>Showing everyone not yet graduated.</b> This includes students who finished years ago '+
                  'and were never put on a list. Switch <i>Who to show</i> back to <i>This cycle</i> to '+
                  'narrow it to the people being graduated now.');
        } else fn.style.display='none';

        qs('gcKpis').innerHTML=
          kpi('gcKpiCand','', o.candidates,'Candidates',
              o.focusFrom ? ('Finished in '+o.focusFrom+' or '+o.focusTo+', and on no list yet.')
                          : 'Reached the final year of their programme and are on no list.')+
          kpi('gcKpiReady','ok', o.ready,'Ready','Nothing outstanding. These are the ones to work through.')+
          kpi('gcKpiBlock','bad', o.blocked,'Blocked','At least one check stops them. Open one to see which.')+
          kpi('gcKpiHeld','warn', o.held,'Held','Stopped by a reviewer, with a reason, awaiting investigation.')+
          kpi('gcKpiList','', o.listed,'On the graduation list','For the selected year, within your scope.');

        var h='';
        if(o.blockers && o.blockers.length){
            h='<div class="gc-tblwrap"><table class="gc-tbl"><thead><tr><th>What is stopping them</th>'+
              '<th class="gc-num">Candidates</th><th></th></tr></thead><tbody>';
            for(var i=0;i<o.blockers.length;i++){
                var b=o.blockers[i];
                h+='<tr><td>'+esc(b.name)+'</td><td class="gc-num"><b>'+b.count+'</b></td>'+
                   '<td class="gc-sub">'+(b.count?barPct(b.count,o.candidates):'')+'</td></tr>';
            }
            h+='</tbody></table></div>';
        } else h='<div class="gc-empty">Nothing is blocking anyone in this scope.</div>';
        qs('gcBlockers').innerHTML=h;

        var pb='';
        for(var j=0;j<(o.programmes||[]).length;j++){
            var p=o.programmes[j];
            var toReview=p.candidates-p.listed-p.held; if(toReview<0) toReview=0;
            pb+='<tr class="is-click" data-prog="'+esc(p.progcode)+'">'+
                '<td><b>'+esc(p.progname||p.progcode)+'</b><div class="gc-sub">'+esc(p.progcode)+'</div></td>'+
                '<td class="gc-num">'+p.candidates+'</td>'+
                '<td class="gc-num">'+p.listed+'</td>'+
                '<td class="gc-num">'+(p.held||'')+'</td>'+
                '<td class="gc-num">'+(p.blocked||'')+'</td>'+
                '<td class="gc-num"><b>'+toReview+'</b></td></tr>';
        }
        qs('gcProgBody').innerHTML=pb||'<tr><td colspan="6" class="gc-empty">No programmes in scope.</td></tr>';

        wire('gcKpiCand', function(){ qs('fReady').value='';        showTab('candidates'); });
        wire('gcKpiReady',function(){ qs('fReady').value='ready';   showTab('candidates'); });
        wire('gcKpiBlock',function(){ qs('fReady').value='blocked'; showTab('candidates'); });
        wire('gcKpiHeld', function(){ showTab('held'); });
        wire('gcKpiList', function(){ showTab('list'); });

        var ig='';
        for(var k=0;k<(o.integrity||[]).length;k++)
            ig+='<div class="gc-note gc-note--bad"><b>Data integrity.</b> '+esc(o.integrity[k])+
                ' Nothing has been changed — taking a name off a graduation list is a decision for the Registrar.</div>';
        qs('gcIntegrity').innerHTML=ig;
    });
}
function wire(id,fn){ var el=qs(id); if(el) el.addEventListener('click',fn); }
function kpi(id,kind,val,label,sub){
    return '<div class="gc-kpi'+(kind?' gc-kpi--'+kind:'')+'" id="'+id+'"><b>'+val+'</b>'+
           '<span>'+esc(label)+'</span><small>'+esc(sub)+'</small></div>';
}
function barPct(v,total){
    if(!total) return '';
    var pct=Math.round(v*100/total);
    return '<div class="gc-bar"><div class="gc-bar__t"><div class="gc-bar__f is-short" style="width:'+pct+'%"></div></div>'+
           '<div class="gc-bar__v">'+pct+'%</div></div>';
}

// ── candidates ──────────────────────────────────────────────────────
function loadCandidates(state){
    qs('candBody').innerHTML='<tr><td colspan="9" class="gc-load">Loading&hellip;</td></tr>';
    qs('candPager').innerHTML='';
    ajax('GetCandidates',{configJson:cfg({state:state||'pending'})},function(d){
        if(!d||!d.success){ msg((d&&d.message)||'Could not load candidates.',false);
            qs('candBody').innerHTML='<tr><td colspan="9" class="gc-empty">Nothing to show.</td></tr>'; return; }
        renderCandidates(d);
    });
}
function renderCandidates(d){
    var rows=d.rows||[], h='', i;
    var want=qs('fReady').value;
    for(i=0;i<rows.length;i++){
        var g=rows[i];
        if(want==='ready'   && g.readiness!=='READY')   continue;
        if(want==='warn'    && g.readiness!=='WARN')    continue;
        if(want==='blocked' && g.readiness!=='BLOCKED') continue;
        var selectable=(g.readiness==='READY' && !g.graduatedYear && !g.holdReason);
        h+='<tr class="is-click" data-reg="'+esc(g.regno)+'">'+
           '<td>'+(selectable
                    ? '<input type="checkbox" class="ck" data-reg="'+esc(g.regno)+'" />'
                    : '<span class="gc-sub" title="Only candidates with nothing outstanding can be bulk-cleared">&ndash;</span>')+'</td>'+
           '<td><span class="gc-reg">'+esc(g.regno)+'</span><div class="gc-sub">'+esc(g.name)+'</div></td>'+
           '<td>'+esc(g.progname||g.progcode)+'<div class="gc-sub">'+esc(g.progcode)+'</div></td>'+
           '<td class="gc-sub">'+esc(g.entryyear)+'</td>'+
           '<td>'+credits(g)+'</td>'+
           '<td class="gc-num">'+(g.cgpa?n2(g.cgpa):'&ndash;')+'</td>'+
           '<td class="gc-sub">'+esc(g.degClass||'&ndash;')+'</td>'+
           '<td>'+chip(g)+'</td>'+
           '<td><button type="button" class="gc-btn" data-open="'+esc(g.regno)+'">Review</button></td></tr>';
    }
    qs('candBody').innerHTML=h||'<tr><td colspan="9" class="gc-empty">No candidates match these filters.</td></tr>';
    qs('ckAll').checked=false; syncBulk();
    var scopeTxt = qs('fFocus').value==='cycle'
        ? (' · finishing ' + (boot && boot.previousYear ? boot.previousYear+' or ' : '') + qs('fYear').value)
        : ' · everyone not yet graduated';
    qs('candMeta').textContent='showing '+rows.length+' of '+d.total+
        (d.pages>1?(' · page '+d.page+' of '+d.pages):'') + scopeTxt;
    qs('nCand').textContent=d.total;

    var pg='';
    if(d.pages>1){
        pg='<button type="button" class="gc-btn" id="pgPrev"'+(d.page<=1?' disabled':'')+'>Previous</button>'+
           '<span>page '+d.page+' of '+d.pages+'</span>'+
           '<button type="button" class="gc-btn" id="pgNext"'+(d.page>=d.pages?' disabled':'')+'>Next</button>';
    }
    qs('candPager').innerHTML=pg;
    if(qs('pgPrev')) qs('pgPrev').addEventListener('click',function(){ if(page>1){page--;pushUrl();load();} });
    if(qs('pgNext')) qs('pgNext').addEventListener('click',function(){ page++;pushUrl();load(); });
}
function chip(g){
    if(g.graduatedYear) return '<span class="gc-chip gc-chip--listed">On '+esc(g.graduatedYear)+'</span>';
    if(g.holdReason)    return '<span class="gc-chip gc-chip--held">Held</span>';
    if(g.readiness==='BLOCKED') return '<span class="gc-chip gc-chip--blocked">Blocked</span>';
    if(g.readiness==='WARN')    return '<span class="gc-chip gc-chip--warn">Needs a look</span>';
    return '<span class="gc-chip gc-chip--ready">Ready</span>';
}
function credits(g){
    if(g.cuSource==='NONE')
        return '<div class="gc-bar"><div class="gc-bar__t"><div class="gc-bar__f is-none" style="width:100%"></div></div>'+
               '<div class="gc-bar__v">'+n0(g.cuEarned)+' CU · no bar set</div></div>';
    var pct=g.cuRequired>0?Math.min(100,Math.round(g.cuEarned*100/g.cuRequired)):0;
    var shortfall=g.cuEarned<g.cuRequired;
    return '<div class="gc-bar"><div class="gc-bar__t"><div class="gc-bar__f'+(shortfall?' is-short':'')+
           '" style="width:'+pct+'%"></div></div>'+
           '<div class="gc-bar__v">'+n0(g.cuEarned)+'/'+n0(g.cuRequired)+'</div></div>';
}

// ── graduation list ─────────────────────────────────────────────────
var listRows=[];
function loadList(){
    qs('listBody').innerHTML='<tr><td colspan="7" class="gc-load">Loading&hellip;</td></tr>';
    ajax('GetGraduationList',{configJson:cfg()},function(d){
        if(!d||!d.success){ msg((d&&d.message)||'Could not load the list.',false); return; }
        listRows=d.rows||[];
        var h='';
        for(var i=0;i<listRows.length;i++){
            var r=listRows[i];
            h+='<tr><td><span class="gc-reg">'+esc(r.regno)+'</span><div class="gc-sub">'+esc(r.name)+'</div></td>'+
               '<td>'+esc(r.progname||r.progcode)+'<div class="gc-sub">'+esc(r.progcode)+'</div></td>'+
               '<td class="gc-num">'+n2(r.cgpa)+'</td>'+
               '<td>'+esc(r.degclass)+'</td>'+
               '<td>'+(r.clearedBy?esc(r.clearedBy)+'<div class="gc-sub">'+esc(r.clearedAt)+'</div>'
                                  :'<span class="gc-sub">before this module</span>')+'</td>'+
               '<td class="gc-sub">'+esc(r.transStatus||'&ndash;')+'</td>'+
               '<td><button type="button" class="gc-btn" data-open="'+esc(r.regno)+'">Open</button></td></tr>';
        }
        qs('listBody').innerHTML=h||'<tr><td colspan="7" class="gc-empty">Nobody is on this list yet.</td></tr>';
        qs('listMeta').textContent=listRows.length+' on the list';
        qs('nList').textContent=listRows.length;
    });
}

// ── held ────────────────────────────────────────────────────────────
function loadHeld(){
    qs('heldBody').innerHTML='<tr><td colspan="6" class="gc-load">Loading&hellip;</td></tr>';
    ajax('GetCandidates',{configJson:cfg({state:'held',size:'200'})},function(d){
        if(!d||!d.success){ msg((d&&d.message)||'Could not load held candidates.',false); return; }
        var rows=d.rows||[], h='';
        for(var i=0;i<rows.length;i++){
            var g=rows[i];
            var stale=(g.readiness!=='BLOCKED');
            h+='<tr class="is-click" data-reg="'+esc(g.regno)+'">'+
               '<td><span class="gc-reg">'+esc(g.regno)+'</span><div class="gc-sub">'+esc(g.name)+'</div></td>'+
               '<td>'+esc(g.progname||g.progcode)+'</td>'+
               '<td>'+esc(g.holdReason)+
                 (stale?'<div class="gc-sub" style="color:#0b5c3a;font-weight:700;">Nothing is blocking this candidate any more</div>':'')+'</td>'+
               '<td class="gc-sub">'+esc(g.holdActor)+'</td>'+
               '<td class="gc-sub">'+esc(g.holdAt)+'</td>'+
               '<td><button type="button" class="gc-btn" data-open="'+esc(g.regno)+'">Review</button></td></tr>';
        }
        qs('heldBody').innerHTML=h||'<tr><td colspan="6" class="gc-empty">Nobody is held in this scope.</td></tr>';
        qs('heldMeta').textContent=rows.length+' held';
        qs('nHeld').textContent=rows.length;
    });
}

// ── evidence panel ──────────────────────────────────────────────────
function openPanel(reg){
    curReg=reg; curStudent=null;
    qs('gcOv').style.display='block';
    qs('gcPanel').style.display='flex';
    qs('pName').textContent=reg;
    qs('pSub').textContent='Loading the record…';
    qs('pBody').innerHTML='<div class="gc-load">Loading…</div>';
    qs('pFoot').innerHTML='';
    ajax('GetStudent',{regno:reg},function(d){
        if(!d||!d.success){ qs('pBody').innerHTML='<div class="gc-note gc-note--bad">'+
            esc((d&&d.message)||'Could not load that student.')+'</div>'; return; }
        curStudent=d; renderPanel(d);
    });
}
function closePanel(){
    qs('gcOv').style.display='none';
    qs('gcPanel').style.display='none';
    curReg=null; curStudent=null;
}

function renderPanel(d){
    var g=d.student;
    qs('pName').textContent=g.name+' · '+g.regno;
    qs('pSub').textContent=(g.progname||g.progcode)+'  ·  intake '+(g.entryyear||'–')+
        '  ·  '+(g.firstYear?g.firstYear+' to '+g.lastYear:'no results on record');

    var h='';

    if(g.graduatedYear)
        h+='<div class="gc-note gc-note--info"><b>Already on the '+esc(g.graduatedYear)+
           ' graduation list.</b></div>';
    if(g.holdReason)
        h+='<div class="gc-note gc-note--warn"><b>Held by '+esc(g.holdActor)+' on '+esc(g.holdAt)+
           '.</b><br>'+esc(g.holdReason)+'</div>';

    h+='<div class="gc-grid2">'+
       cell('Credits earned', n0(g.cuEarned)+' CU')+
       cell('Credits required', g.cuSource==='NONE'?'Not assessable':(n0(g.cuRequired)+' CU'))+
       cell('CGPA', g.cgpa?n2(g.cgpa):'–')+
       cell('Class of award', g.degClass||'–')+
       cell('Courses on record', String(g.coursesTaken))+
       cell('Reached year', g.maxStudyYear+' of '+g.progLength)+
       '</div>';

    h+='<div class="gc-note gc-note--info" style="font-size:11px;">'+
       '<b>Where the credit bar comes from:</b> '+esc(g.cuSourceLabel||'not established')+'.'+
       (g.specIsPlaceholder
         ? ' This student carries no real specialisation — the value on their record is a placeholder, ' +
           'so the programme structure cannot be matched to them course by course.'
         : '')+
       '</div>';

    h+='<div class="gc-sec">What the checks say</div>';
    for(var i=0;i<(g.findings||[]).length;i++){
        var f=g.findings[i];
        h+='<div class="gc-find gc-find--'+esc(f.level)+'">'+
           '<div class="gc-find__n">'+esc(f.name)+'</div>'+
           '<div class="gc-find__d">'+esc(f.detail)+'</div></div>';
    }

    if(d.structure && d.structure.length){
        h+='<div class="gc-sec">Against the programme structure</div>'+
           '<div class="gc-tblwrap" style="max-height:260px;"><table class="gc-tbl gc-struct"><thead><tr>'+
           '<th>Yr</th><th>Sem</th><th>Course</th><th class="gc-num">CU</th><th>Result</th></tr></thead><tbody>';
        for(var s=0;s<d.structure.length;s++){
            var c=d.structure[s];
            var cls=c.score===null?'miss':(c.score<50?'fail':'');
            var txt=c.score===null?'no result':(c.score+'%');
            h+='<tr><td>'+esc(c.sy)+'</td><td>'+esc(c.sem)+'</td>'+
               '<td>'+esc(c.code)+'<div class="gc-sub">'+esc(c.name)+'</div></td>'+
               '<td class="gc-num">'+n0(c.cu)+'</td>'+
               '<td class="'+cls+'">'+esc(txt)+'</td></tr>';
        }
        h+='</tbody></table></div>';
    }

    h+='<div class="gc-sec">Results on record ('+(d.results||[]).length+')</div>'+
       '<div class="gc-tblwrap" style="max-height:280px;"><table class="gc-tbl"><thead><tr>'+
       '<th>Year</th><th>Yr/Sem</th><th>Course</th><th class="gc-num">CU</th>'+
       '<th class="gc-num">Score</th><th>Grade</th></tr></thead><tbody>';
    for(var r=0;r<(d.results||[]).length;r++){
        var x=d.results[r];
        var bad=(x.score!==null && x.score>0 && x.score<50), zero=(x.score===0);
        h+='<tr><td class="gc-sub">'+esc(x.acad)+'</td><td class="gc-sub">'+esc(x.sy)+'/'+esc(x.sem)+'</td>'+
           '<td>'+esc(x.code)+'<div class="gc-sub">'+esc(x.name)+'</div></td>'+
           '<td class="gc-num">'+n0(x.cu)+'</td>'+
           '<td class="gc-num"'+(bad?' style="color:#8c2019;font-weight:700"':(zero?' style="color:#92400e;font-weight:700"':''))+'>'+
             (x.score===null?'–':x.score)+'</td>'+
           '<td>'+esc(x.grade)+'</td></tr>';
    }
    h+='</tbody></table></div>';

    if(d.history && d.history.length){
        h+='<div class="gc-sec">Decision history</div>';
        for(var v=0;v<d.history.length;v++){
            var e=d.history[v];
            h+='<div class="gc-find gc-find--'+(e.verdict==='HELD'?'WARN':(e.verdict==='CLEARED'?'PASS':'NA'))+'">'+
               '<div class="gc-find__n">'+esc(e.verdict)+(e.inForce?' · in force':'')+'</div>'+
               '<div class="gc-find__d">'+esc(e.reason||'(no note)')+
               '<div class="gc-sub">'+esc(e.actor)+(e.role?' ('+esc(e.role)+')':'')+' · '+esc(e.at)+
               (e.year&&e.year!=='-'?' · '+esc(e.year):'')+'</div></div></div>';
        }
    }

    qs('pBody').innerHTML=h;

    var ft='';
    if(g.graduatedYear){
        ft='<button type="button" class="gc-btn gc-btn--d" id="pRemove">Take off the graduation list</button>'+
           '<span class="gc-sub">On the '+esc(g.graduatedYear)+' list'+
           (g.clearedActor?', cleared by '+esc(g.clearedActor):'')+'.</span>';
    } else if(g.holdReason){
        ft='<button type="button" class="gc-btn gc-btn--p" id="pRelease">Lift the hold</button>'+
           '<button type="button" class="gc-btn" id="pEdit">Edit the reason</button>'+
           '<button type="button" class="gc-btn" id="pClear">Clear for graduation</button>';
    } else {
        ft='<button type="button" class="gc-btn gc-btn--p" id="pClear">Clear for graduation</button>'+
           '<button type="button" class="gc-btn gc-btn--d" id="pHold">Hold&hellip;</button>'+
           '<span class="gc-sub" style="margin-left:auto;">'+
             (g.readiness==='BLOCKED'?'Blocked — clearing will ask you to justify it.':
              g.readiness==='WARN'?'Read the warnings before clearing.':'Nothing outstanding.')+'</span>';
    }
    qs('pFoot').innerHTML=ft;
    if(qs('pClear'))   qs('pClear').addEventListener('click',doClear);
    if(qs('pHold'))    qs('pHold').addEventListener('click',doHold);
    if(qs('pRelease')) qs('pRelease').addEventListener('click',doRelease);
    if(qs('pRemove'))  qs('pRemove').addEventListener('click',doRemove);
    if(qs('pEdit'))    qs('pEdit').addEventListener('click',doEditHold);
}
function cell(label,val){
    return '<div><span>'+esc(label)+'</span><b>'+esc(val)+'</b></div>';
}

// ── decisions ───────────────────────────────────────────────────────
function year(){ return qs('fYear').value; }

function doClear(){
    if(!curStudent) return;
    var g=curStudent.student;
    if(!year()){ msg('Choose the graduation year at the top first.',false); return; }
    var note='';
    if(g.readiness==='BLOCKED'){
        var blockers=[];
        for(var i=0;i<g.findings.length;i++) if(g.findings[i].level==='BLOCK') blockers.push('• '+g.findings[i].detail);
        note=prompt('This candidate is BLOCKED:\n\n'+blockers.join('\n')+
            '\n\nClearing them anyway is recorded against your name and shown on the graduation list.\n'+
            'Say why (at least 10 characters):','');
        if(note===null) return;
        if(note.trim().length<10){ msg('A justification of at least 10 characters is needed.',false); return; }
    } else {
        if(!confirm('Put '+g.name+' on the '+year()+' graduation list?')) return;
    }
    ajax('ClearStudent',{regno:g.regno,acadYear:year(),note:note,overrideBlock:g.readiness==='BLOCKED'},
        function(d){
            if(d&&d.success){ msg(d.message,true); closePanel(); load(); }
            else msg((d&&d.message)||'Could not clear that candidate.',false);
        });
}
function doHold(){
    if(!curStudent) return;
    var g=curStudent.student;
    if(!year()){ msg('Choose the graduation year at the top first.',false); return; }
    var reason=prompt('Why is '+g.name+' being held?\n\n'+
        'Whoever picks this up next has only this sentence to go on, so be specific '+
        '(at least 10 characters).','');
    if(reason===null) return;
    if(reason.trim().length<10){ msg('Say a little more — at least 10 characters.',false); return; }
    ajax('HoldStudent',{regno:g.regno,acadYear:year(),reason:reason},function(d){
        if(d&&d.success){ msg(d.message,true); closePanel(); load(); }
        else msg((d&&d.message)||'Could not hold that candidate.',false);
    });
}
// Re-holding with a new reason: the old hold is superseded, not overwritten, so the panel's
// history still shows what the first reviewer originally wrote.
function doEditHold(){
    if(!curStudent) return;
    var g=curStudent.student;
    var reason=prompt('Update the reason '+g.name+' is being held.\n\n'+
        'The original is kept in the decision history.', g.holdReason||'');
    if(reason===null) return;
    if(reason.trim().length<10){ msg('Say a little more \u2014 at least 10 characters.',false); return; }
    if(reason.trim()===(g.holdReason||'').trim()){ msg('That is the reason already recorded.',false); return; }
    ajax('HoldStudent',{regno:g.regno,acadYear:year()||g.holdAt,reason:reason},function(d){
        if(d&&d.success){ msg('The reason has been updated.',true); openPanel(g.regno); load(); }
        else msg((d&&d.message)||'Could not update the reason.',false);
    });
}

function doRelease(){
    if(!curStudent) return;
    var g=curStudent.student;
    var note=prompt('Lift the hold on '+g.name+'?\n\nAnything worth noting? (optional)','');
    if(note===null) return;
    ajax('ReleaseStudent',{regno:g.regno,acadYear:year(),note:note},function(d){
        if(d&&d.success){ msg(d.message,true); closePanel(); load(); }
        else msg((d&&d.message)||'Could not lift that hold.',false);
    });
}
function doRemove(){
    if(!curStudent) return;
    var g=curStudent.student;
    var reason=prompt('Take '+g.name+' OFF the '+g.graduatedYear+' graduation list?\n\n'+
        'The decision history is kept. Say why (at least 10 characters):','');
    if(reason===null) return;
    if(reason.trim().length<10){ msg('A reason of at least 10 characters is needed.',false); return; }
    ajax('RemoveStudent',{regno:g.regno,reason:reason},function(d){
        if(d&&d.success){ msg(d.message,true); closePanel(); load(); }
        else msg((d&&d.message)||'Could not remove that name.',false);
    });
}

// ── bulk clear ────────────────────────────────────────────
function selected(){
    var out=[], b=document.querySelectorAll('#candBody .ck:checked');
    for(var i=0;i<b.length;i++) out.push(b[i].getAttribute('data-reg'));
    return out;
}
function syncBulk(){
    var n=selected().length;
    var b=qs('btnBulk');
    b.disabled=(n===0);
    b.textContent=n?('Clear '+n+' selected'):'Clear selected';
}
function doBulk(){
    var regs=selected();
    if(!regs.length) return;
    if(!year()){ msg('Choose the graduation year at the top first.',false); return; }
    if(!confirm('Put '+regs.length+' candidate'+(regs.length===1?'':'s')+' on the '+year()+
                ' graduation list?\n\nEach one is re-checked on the server before it is added.')) return;
    qs('btnBulk').disabled=true;
    ajax('ClearMany',{regnos:regs.join(','),acadYear:year()},function(d){
        if(!d||!d.success){ msg((d&&d.message)||'The bulk clear did not run.',false); syncBulk(); return; }
        msg(d.message, d.cleared>0);
        if(d.skipped && d.skipped.length)
            alert('Skipped '+d.skipped.length+':\n\n'+d.skipped.join('\n'));
        load();
    });
}

// ── Senate print ───────────────────────────────────────────
//  Grouped by programme, numbered within each, because that is how a graduation list is read
//  out and signed off. Built from the rows already on screen, so what prints is exactly what
//  was reviewed.
function doPrint(){
    if(!listRows.length){ msg('There is nothing on this list to print.',false); return; }
    var w=window.open('','_blank');
    if(!w){ msg('Pop-up blocked \u2014 allow pop-ups to open the print view.',false); return; }

    var groups={}, order=[];
    for(var i=0;i<listRows.length;i++){
        var r=listRows[i], k=r.progname||r.progcode;
        if(!groups[k]){ groups[k]=[]; order.push(k); }
        groups[k].push(r);
    }
    order.sort();

    var h='<!doctype html><html><head><meta charset="utf-8"><title>Graduation list '+esc(year())+'</title><style>'+
      'body{font-family:"Segoe UI",Arial,sans-serif;color:#111;margin:26px;font-size:11pt;}'+
      'h1{font-size:15pt;margin:0 0 2px;color:#05275C;}'+
      '.sub{font-size:10pt;color:#555;margin-bottom:18px;}'+
      'h2{font-size:11.5pt;margin:20px 0 6px;color:#05275C;border-bottom:1.5px solid #05275C;padding-bottom:3px;}'+
      'table{width:100%;border-collapse:collapse;font-size:9.5pt;}'+
      'th{text-align:left;border-bottom:1px solid #999;padding:4px 5px;font-size:8.5pt;'+
        'text-transform:uppercase;letter-spacing:.3px;color:#444;}'+
      'td{padding:4px 5px;border-bottom:1px solid #eee;}'+
      '.n{text-align:right;}'+
      '.foot{margin-top:28px;font-size:9pt;color:#555;border-top:1px solid #999;padding-top:8px;}'+
      '.sig{margin-top:34px;display:flex;gap:46px;font-size:9.5pt;}'+
      '.sig div{flex:1;border-top:1px solid #333;padding-top:5px;}'+
      '@media print{ h2{page-break-after:avoid;} tr{page-break-inside:avoid;} }'+
      '</style></head><body>';
    h+='<h1>Muteesa I Royal University</h1>'+
       '<div class="sub">Graduation list for '+esc(year()||'all years')+
       ' &middot; '+listRows.length+' candidate'+(listRows.length===1?'':'s')+
       ' &middot; prepared '+new Date().toLocaleDateString()+'</div>';

    for(var g=0;g<order.length;g++){
        var rows=groups[order[g]];
        h+='<h2>'+esc(order[g])+' <span style="font-weight:400;font-size:9pt;color:#666;">('+
           rows.length+')</span></h2>'+
           '<table><thead><tr><th style="width:26px;">#</th><th>Student number</th><th>Name</th>'+
           '<th class="n">CGPA</th><th>Class of award</th></tr></thead><tbody>';
        for(var r2=0;r2<rows.length;r2++)
            h+='<tr><td class="n">'+(r2+1)+'</td><td>'+esc(rows[r2].regno)+'</td><td>'+esc(rows[r2].name)+
               '</td><td class="n">'+n2(rows[r2].cgpa)+'</td><td>'+esc(rows[r2].degclass)+'</td></tr>';
        h+='</tbody></table>';
    }

    h+='<div class="foot">Prepared from the Graduation Centre. Every name on this list was cleared by a '+
       'named reviewer against the results on record at the time of clearing; the evidence for each '+
       'decision is retained and can be produced on request.</div>'+
       '<div class="sig"><div>Academic Registrar</div><div>Chairperson, Senate</div></div>'+
       '</body></html>';
    w.document.open(); w.document.write(h); w.document.close();
    setTimeout(function(){ try{ w.focus(); w.print(); }catch(e){} }, 350);
}

// ── export ──────────────────────────────────────────────────────────
function exportCsv(){
    if(!listRows.length){ msg('There is nothing on this list to export.',false); return; }
    var head=['Student Number','Name','Programme Code','Programme','CGPA','Class','Graduation Year',
              'Gender','Nationality','Cleared By','Cleared On'];
    var lines=[head.join(',')];
    for(var i=0;i<listRows.length;i++){
        var r=listRows[i];
        lines.push([r.regno,r.name,r.progcode,r.progname,n2(r.cgpa),r.degclass,r.year,
                    r.gender,r.nationality,r.clearedBy,r.clearedAt]
            .map(function(v){ v=String(v==null?'':v); return /[",\n]/.test(v)?'"'+v.replace(/"/g,'""')+'"':v; })
            .join(','));
    }
    var blob=new Blob(['﻿'+lines.join('\r\n')],{type:'text/csv;charset=utf-8;'});
    var a=document.createElement('a');
    a.href=URL.createObjectURL(blob);
    a.download='graduation-list-'+(year()||'all').replace('/','-')+'.csv';
    document.body.appendChild(a); a.click(); document.body.removeChild(a);
}

// ── wiring ──────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded',function(){
    var pre=readUrl();

    qs('gcTabs').addEventListener('click',function(e){
        var b=e.target.closest ? e.target.closest('.gc-tab') : null;
        if(b) showTab(b.getAttribute('data-tab'));
    });
    qs('btnApply').addEventListener('click',function(){ page=1; pushUrl(); load(); });
    qs('btnReset').addEventListener('click',function(){
        qs('fFac').value=''; qs('fDep').value=''; qs('fProg').value=''; qs('fQ').value='';
        qs('fReady').value=''; qs('fIntake').value=''; qs('fFin').value='';
        qs('fFocus').value='cycle';
        if(boot && boot.currentYear) qs('fYear').value=boot.currentYear;
        cascade(); page=1; pushUrl(); load();
    });
    qs('fFac').addEventListener('change',function(){ cascade(); page=1; pushUrl(); load(); });
    qs('fDep').addEventListener('change',function(){ cascade(); page=1; pushUrl(); load(); });
    qs('fProg').addEventListener('change',function(){ page=1; pushUrl(); load(); });
    qs('fYear').addEventListener('change',function(){ page=1; pushUrl(); load(); });
    qs('fReady').addEventListener('change',function(){ pushUrl(); load(); });
    qs('fQ').addEventListener('keydown',function(e){ if(e.key==='Enter'){ page=1; pushUrl(); load(); } });
    qs('btnExport').addEventListener('click',exportCsv);
    qs('btnPrint').addEventListener('click',doPrint);
    qs('btnBulk').addEventListener('click',doBulk);
    qs('fFocus').addEventListener('change',function(){ page=1; pushUrl(); load(); });
    qs('fIntake').addEventListener('change',function(){ page=1; pushUrl(); load(); });
    qs('fFin').addEventListener('change',function(){ page=1; pushUrl(); load(); });
    qs('ckAll').addEventListener('change',function(){
        var b=document.querySelectorAll('#candBody .ck');
        for(var i=0;i<b.length;i++) b[i].checked=qs('ckAll').checked;
        syncBulk();
    });
    qs('candBody').addEventListener('change',function(e){
        if(e.target && e.target.className==='ck') syncBulk();
    });
    // A checkbox click must not also open the evidence panel behind it.
    qs('candBody').addEventListener('click',function(e){
        if(e.target && e.target.className==='ck') e.stopPropagation();
    });
    qs('pClose').addEventListener('click',closePanel);
    qs('gcOv').addEventListener('click',closePanel);
    document.addEventListener('keydown',function(e){ if(e.key==='Escape' && curReg) closePanel(); });

    // One delegated handler for every "open this student" affordance on the page.
    document.addEventListener('click',function(e){
        var b=e.target.closest ? e.target.closest('[data-open]') : null;
        if(b){ e.stopPropagation(); openPanel(b.getAttribute('data-open')); return; }
        var tr=e.target.closest ? e.target.closest('tr.is-click') : null;
        if(tr && tr.getAttribute('data-reg')) openPanel(tr.getAttribute('data-reg'));
        else if(tr && tr.getAttribute('data-prog')){
            qs('fProg').value=tr.getAttribute('data-prog'); showTab('candidates');
        }
    });

    ajax('GetBootstrap',{},function(o){
        if(!o||!o.success||!o.hasAccess){
            qs('gcMain').style.display='none';
            var na=qs('gcNoAccess'); na.style.display='block';
            na.textContent=(o&&o.message)||'You do not have access to graduation data.';
            return;
        }
        boot=o;
        qs('gcScope').textContent=(o.roleNote||'')+(o.scopeLabel?' · '+o.scopeLabel:'');
        var ys=[]; for(var i=0;i<o.years.length;i++) ys.push({v:o.years[i],t:o.years[i]});
        fillSel('fYear',ys,'');
        fillSel('fFac',o.faculties,'All faculties');
        fillSel('fDep',o.departments,'All departments');
        fillSel('fProg',o.programmes,'All programmes');
        var ints=[]; for(var k=0;k<(o.intakes||[]).length;k++) ints.push({v:o.intakes[k],t:o.intakes[k]});
        fillSel('fIntake',ints,'All intakes');
        fillSel('fFin',ys,'Any year');
        qs('fYear').value = pre.year || o.currentYear || '';
        if(pre.fac)  qs('fFac').value=pre.fac;
        if(pre.dep)  qs('fDep').value=pre.dep;
        if(pre.prog) qs('fProg').value=pre.prog;
        if(pre.q)    qs('fQ').value=pre.q;
        if(pre.ready) qs('fReady').value=pre.ready;
        if(pre.intake)qs('fIntake').value=pre.intake;
        if(pre.fin)   qs('fFin').value=pre.fin;
        // The cycle is the default on purpose: without it the queue opens on 14,548 students,
        // 13,460 of whom finished before last year.
        qs('fFocus').value = pre.focus || 'cycle';
        cascade();
        showTab(tab);
    });
});
})();
</script>
</asp:Content>
