<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="AdmissionAnalysis.aspx.cs" Inherits="COOPERP_NewScreens_AdmissionAnalysis" Title="Admission Analysis - Campus Dynamics" %>
<asp:Content ID="H" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ===== ADMISSION ANALYSIS (prefix aa-) ================================= */
.aa-wrap{padding:2px 2px 8px;}
.aa-top{display:flex;align-items:flex-end;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:9px;}
.aa-h__t{font-size:17px;font-weight:800;color:#05275C;line-height:1.1;}
.aa-h__s{font-size:11px;color:#6b7280;margin-top:2px;}
.aa-top__act{display:flex;gap:6px;}
.aa-btn{display:inline-flex;align-items:center;gap:5px;padding:6px 11px;border:1px solid #d2dae6;background:#fff;color:#05275C;text-decoration:none;font-size:11.5px;font-weight:700;cursor:pointer;border-radius:0;font-family:inherit;height:31px;box-sizing:border-box;}
.aa-btn:hover{border-color:#174DA4;color:#174DA4;}
.aa-btn--p{background:#05275C;border-color:#05275C;color:#fff;}.aa-btn--p:hover{background:#174DA4;border-color:#174DA4;color:#fff;}
.aa-card{background:#fff;border:1px solid #e0e5ed;}
.aa-filters{display:flex;gap:7px;align-items:flex-end;flex-wrap:wrap;padding:9px 10px;border-bottom:1px solid #eef1f5;}
.aa-fl{display:flex;flex-direction:column;gap:2px;}
.aa-fl>span{font-size:8px;font-weight:800;text-transform:uppercase;letter-spacing:.4px;color:#9aa6b8;padding-left:1px;}
.aa-sel,.aa-in{border:1px solid #cfd8e3;background:#fff;padding:6px 8px;font-size:12px;color:#1f2937;border-radius:0;font-family:inherit;height:31px;box-sizing:border-box;}
.aa-sel:focus,.aa-in:focus{outline:none;border-color:#174DA4;}
.aa-fl__sp{flex:1;}
.aa-combo{position:relative;} .aa-combo>input{width:100%;box-sizing:border-box;}
.aa-combo__list{position:absolute;top:100%;left:0;right:0;z-index:60;background:#fff;border:1px solid #cfd8e3;max-height:260px;overflow:auto;display:none;box-shadow:0 6px 18px rgba(0,0,0,.13);min-width:230px;}
.aa-combo__list.on{display:block;}
.aa-combo__i{padding:7px 10px;font-size:12px;cursor:pointer;border-bottom:1px solid #f0f2f5;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.aa-combo__i:hover{background:#eef4ff;} .aa-combo__i--none{color:#9ca3af;cursor:default;}
/* KPI strip */
.aa-kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:8px;margin:12px 0;}
.aa-kpi{background:#fff;border:1px solid #e0e5ed;padding:9px 13px;position:relative;overflow:hidden;}
.aa-kpi::before{content:'';position:absolute;left:0;top:0;bottom:0;width:3px;background:var(--c,#94a3b8);}
.aa-kpi__n{font-size:20px;font-weight:800;color:var(--c,#05275C);line-height:1;font-variant-numeric:tabular-nums;}
.aa-kpi__l{font-size:8.5px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:#94a3b8;margin-top:4px;}
.aa-kpi--total{--c:#174DA4;} .aa-kpi--pending{--c:#e65100;} .aa-kpi--admitted{--c:#16a34a;} .aa-kpi--registered{--c:#05275C;} .aa-kpi--rejected{--c:#c62828;} .aa-kpi--rate{--c:#7c3aed;}
/* dimension segmented control */
.aa-dims{display:flex;gap:0;border:1px solid #cfd8e3;margin:0;overflow:hidden;}
.aa-dims button{font-size:11px;font-weight:700;padding:7px 14px;border:0;border-right:1px solid #cfd8e3;background:#fff;color:#556;cursor:pointer;font-family:inherit;}
.aa-dims button:last-child{border-right:0;}
.aa-dims button.on{background:#05275C;color:#fff;}
.aa-tbl-head{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;padding:10px 12px;border-bottom:1px solid #eef1f5;}
.aa-tbl-title{font-size:12.5px;font-weight:800;color:#05275C;}
/* analysis table */
.aa-tbl-wrap{overflow-x:auto;max-height:66vh;}
.aa-table{width:100%;border-collapse:collapse;font-size:11.5px;}
.aa-table th{position:sticky;top:0;background:#f5f7fa;padding:8px 12px;text-align:left;font-size:10px;text-transform:uppercase;letter-spacing:.3px;color:#555;font-weight:700;border-bottom:2px solid #e0e5ed;white-space:nowrap;cursor:pointer;user-select:none;z-index:1;}
.aa-table th.num{text-align:right;}
.aa-table th.on{color:#05275C;}
.aa-table th .ar{font-size:8px;color:#94a3b8;margin-left:2px;}
.aa-table td{padding:6px 12px;border-bottom:1px solid #f0f2f5;color:#1a1a2e;vertical-align:middle;}
.aa-table td.num{text-align:right;font-variant-numeric:tabular-nums;}
.aa-table td.name{font-weight:600;color:#05275C;}
.aa-table td.sub{font-size:10px;color:#94a3b8;font-weight:400;}
.aa-table tbody tr:hover td{background:#f5f9ff;}
.aa-rank{color:#b0b8c6;font-size:10px;font-weight:700;text-align:center;width:26px;}
.aa-pill{display:inline-block;min-width:34px;text-align:right;}
.aa-mut{color:#c3ccda;}
.aa-table tfoot td{position:sticky;bottom:0;background:#eef2f8;border-top:2px solid #05275C;font-weight:800;color:#05275C;padding:8px 12px;}
.aa-bar{display:inline-block;height:6px;background:#16a34a;vertical-align:middle;margin-right:5px;min-width:1px;}
.aa-empty{text-align:center;color:#94a3b8;padding:34px;font-size:13px;}
</style>
</asp:Content>

<asp:Content ID="C" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="aa-wrap">
  <div class="aa-top">
    <div>
      <div class="aa-h__t">Admission Analysis</div>
      <div class="aa-h__s" id="aaSub">Loading&hellip;</div>
    </div>
    <div class="aa-top__act">
      <a class="aa-btn" href="AdmissionsController.aspx">Applicants</a>
      <button type="button" class="aa-btn" onclick="AA.reconcile()" title="Ensure every active student has an admission record">&#9878; Reconcile</button>
      <button type="button" class="aa-btn" onclick="AA.csv()">&#8681; CSV</button>
      <button type="button" class="aa-btn aa-btn--p" onclick="AA.print()">&#128424; Print / PDF</button>
    </div>
  </div>

  <div class="aa-card">
    <div class="aa-filters">
      <div class="aa-fl"><span>Entry year</span><select id="fYear" class="aa-sel"><option value="">All years</option></select></div>
      <div class="aa-fl"><span>Session</span><select id="fSession" class="aa-sel"><option value="">All sessions</option></select></div>
      <div class="aa-fl"><span>Campus</span><select id="fCampus" class="aa-sel"><option value="">All campuses</option></select></div>
      <div class="aa-fl"><span>Source</span><select id="fSource" class="aa-sel"><option value="">All sources</option><option value="ONLINE">Online Portal</option><option value="WALKIN">Walk-in / Manual</option></select></div>
      <div class="aa-fl" style="min-width:190px;"><span>Faculty</span><div id="cFac"></div></div>
      <div class="aa-fl" style="min-width:210px;"><span>Programme</span><div id="cProg"></div></div>
      <div class="aa-fl"><span>&nbsp;</span><button type="button" class="aa-btn aa-btn--p" onclick="AA.apply()">Apply</button></div>
      <div class="aa-fl"><span>&nbsp;</span><button type="button" class="aa-btn" onclick="AA.clear()">Clear</button></div>
    </div>
    <div id="aaKpis" class="aa-kpis" style="padding:0 10px;"></div>
    <div id="aaNote" style="padding:6px 12px 2px;font-size:11.5px;color:#64748b;line-height:1.55;"></div>
  </div>

  <div class="aa-card" style="margin-top:12px;">
    <div class="aa-tbl-head">
      <div class="aa-dims" id="aaDims">
        <button type="button" data-dim="byProg" class="on" onclick="AA.dim('byProg')">By Programme</button>
        <button type="button" data-dim="byFaculty" onclick="AA.dim('byFaculty')">By Faculty</button>
        <button type="button" data-dim="byCampus" onclick="AA.dim('byCampus')">By Campus</button>
        <button type="button" data-dim="byYear" onclick="AA.dim('byYear')">By Year</button>
        <button type="button" data-dim="byCohort" onclick="AA.dim('byCohort')">By Intake (Year &times; Session)</button>
        <button type="button" data-dim="bySession" onclick="AA.dim('bySession')">By Session</button>
        <button type="button" data-dim="bySource" onclick="AA.dim('bySource')">By Source</button>
      </div>
      <div class="aa-tbl-title" id="aaDimTitle">By Programme</div>
    </div>
    <div id="aaHost"><div class="aa-empty">Loading&hellip;</div></div>
  </div>
</div>

<!-- Reconcile modal -->
<div class="rc-ov" id="rcOv">
  <div class="rc-modal">
    <div class="rc-hd"><b>Reconcile missing admission records</b><button type="button" class="rc-x" onclick="AA.rcClose()">&times;</button></div>
    <div class="rc-body" id="rcBody">
      <p class="rc-note">Every <b>active</b> student should have an admission record (linked by <code>regno → stud_entry_no</code>). This creates the missing <code>acad_applications</code> + primary choice for active students in the range below. It is additive and safe to re-run.</p>
      <div class="rc-range">
        <div class="aa-fl"><span>From year</span><input type="number" id="rcMin" class="aa-sel" value="2024" style="width:90px;"></div>
        <div class="aa-fl"><span>To year</span><input type="number" id="rcMax" class="aa-sel" value="2027" style="width:90px;"></div>
        <div class="aa-fl"><span>&nbsp;</span><button type="button" class="aa-btn aa-btn--p" onclick="AA.rcPreview()">Preview</button></div>
      </div>
      <div id="rcResult"></div>
    </div>
    <div class="rc-foot">
      <button type="button" class="aa-btn" onclick="AA.rcClose()">Close</button>
      <button type="button" class="aa-btn aa-btn--p" id="rcRunBtn" style="display:none;" onclick="AA.rcRun()">Create records</button>
    </div>
    <div class="rc-busy" id="rcBusy"><div class="rc-spin"></div><span id="rcBusyTxt">Working…</span></div>
  </div>
</div>
<style>
.rc-ov{position:fixed;inset:0;background:rgba(9,20,40,.5);z-index:1000;display:none;align-items:flex-start;justify-content:center;padding-top:60px;}
.rc-ov.on{display:flex;}
.rc-modal{background:#fff;width:640px;max-width:95vw;max-height:82vh;display:flex;flex-direction:column;box-shadow:0 20px 60px rgba(0,0,0,.3);position:relative;}
.rc-hd{display:flex;align-items:center;justify-content:space-between;padding:12px 16px;background:#05275C;color:#fff;font-size:14px;}
.rc-x{background:none;border:0;color:rgba(255,255,255,.85);font-size:22px;line-height:1;cursor:pointer;}
.rc-body{padding:16px;overflow-y:auto;}
.rc-note{font-size:11.5px;color:#4b5563;line-height:1.5;margin:0 0 12px;}
.rc-note code{background:#eef2f8;padding:1px 5px;font-size:11px;color:#05275C;}
.rc-range{display:flex;gap:10px;align-items:flex-end;margin-bottom:12px;}
.rc-foot{display:flex;justify-content:flex-end;gap:8px;padding:12px 16px;border-top:1px solid #e0e5ed;background:#fafbfc;}
.rc-kpis{display:flex;gap:8px;margin-bottom:12px;}
.rc-kpi{flex:1;border:1px solid #e0e5ed;padding:10px 12px;text-align:center;}
.rc-kpi__n{font-size:22px;font-weight:800;color:#05275C;line-height:1;}
.rc-kpi__n.warn{color:#e65100;} .rc-kpi__n.ok{color:#16a34a;}
.rc-kpi__l{font-size:8.5px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:#94a3b8;margin-top:4px;}
.rc-sub{font-size:11px;color:#6b7280;margin:8px 0 4px;font-weight:700;}
.rc-tbl{width:100%;border-collapse:collapse;font-size:11px;}
.rc-tbl th{background:#f5f7fa;text-align:left;padding:5px 8px;font-size:9px;text-transform:uppercase;letter-spacing:.3px;color:#666;border-bottom:1px solid #e0e5ed;}
.rc-tbl td{padding:4px 8px;border-bottom:1px solid #f0f2f5;}
.rc-flag{background:#fff8e1;border:1px solid #ffe082;padding:8px 10px;font-size:11px;color:#8a5a00;margin-top:8px;}
.rc-ok{background:#e8f5e9;border:1px solid #c8e6c9;padding:10px 12px;font-size:12px;color:#166534;font-weight:600;}
.rc-err{background:#fde8e8;border:1px solid #f5c6cb;padding:10px 12px;font-size:12px;color:#c62828;}
.rc-busy{display:none;position:absolute;inset:0;background:rgba(255,255,255,.85);flex-direction:column;align-items:center;justify-content:center;gap:12px;font-size:12px;font-weight:700;color:#05275C;}
.rc-busy.on{display:flex;}
.rc-spin{width:32px;height:32px;border:3px solid #dbe3ee;border-top-color:#174DA4;border-radius:50%;animation:rcspin .7s linear infinite;}
@keyframes rcspin{to{transform:rotate(360deg);}}
</style>

<script>window.__AA_INIT = <%= InitJson %>;</script>
<script>
var AA = (function(){
  var D = window.__AA_INIT || {};
  var DIM='byProg', SORT='total', DIR=-1;
  var facCombo=null, progCombo=null;
  var DIMLABEL={byProg:'By Programme',byFaculty:'By Faculty',byCampus:'By Campus',byYear:'By Year',byCohort:'By Intake (Year \u00d7 Session)',bySession:'By Session',bySource:'By Source'};
  var DIMCOL={byProg:'Programme',byFaculty:'Faculty',byCampus:'Campus',byYear:'Entry Year',byCohort:'Intake',bySession:'Session',bySource:'Source'};
  function qs(id){return document.getElementById(id);}
  function esc(s){s=(s==null?'':''+s);return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');}
  function nf(n){ return (n||0).toLocaleString(); }
  function pctv(part,whole){ return whole>0?(part*100/whole):0; }
  function pct(part,whole){ return whole>0?(pctv(part,whole).toFixed(1)+'%'):'—'; }

  function makeCombo(host, items, placeholder, cur){
    host.className='aa-combo';
    host.innerHTML='<input type="text" class="aa-in" placeholder="'+esc(placeholder)+'" autocomplete="off"><div class="aa-combo__list"></div>';
    var inp=host.querySelector('input'), list=host.querySelector('.aa-combo__list'), val='';
    var c=items.filter(function(o){return String(o.value)===String(cur);})[0]; if(c&&c.value){ val=c.value; inp.value=c.label; }
    function draw(f){ f=(f||'').toLowerCase();
      var m=items.filter(function(o){ return !f || (o.label||'').toLowerCase().indexOf(f)>=0; });
      if(!m.length){ list.innerHTML='<div class="aa-combo__i aa-combo__i--none">No match</div>'; return; }
      list.innerHTML=m.slice(0,120).map(function(o){ return '<div class="aa-combo__i" data-v="'+esc(o.value)+'" title="'+esc(o.label)+'">'+esc(o.label)+'</div>'; }).join('');
      [].forEach.call(list.querySelectorAll('.aa-combo__i[data-v]'),function(el){ el.onmousedown=function(e){ e.preventDefault(); val=el.getAttribute('data-v'); inp.value=el.textContent; list.classList.remove('on'); }; });
    }
    inp.addEventListener('focus',function(){ draw(''); list.classList.add('on'); });
    inp.addEventListener('input',function(){ val=''; draw(inp.value); list.classList.add('on'); });
    inp.addEventListener('keydown',function(e){ if(e.key==='Enter'){ e.preventDefault(); apply(); } });
    inp.addEventListener('blur',function(){ setTimeout(function(){ list.classList.remove('on'); },160); });
    return { value:function(){return val;} };
  }

  function init(){
    if(!D||!D.ok){ qs('aaHost').innerHTML='<div class="aa-empty">'+esc((D&&D.error)||'Unable to load analysis.')+'</div>'; return; }
    var L=D.lists||{}, F=D.filters||{};
    // native selects
    qs('fYear').innerHTML='<option value="">All years</option>'+(L.years||[]).map(function(y){return '<option value="'+esc(y)+'"'+(F.year==y?' selected':'')+'>'+esc(y)+'</option>';}).join('');
    qs('fSession').innerHTML='<option value="">All sessions</option>'+(L.sessions||[]).map(function(s){return '<option value="'+esc(s)+'"'+(F.session==s?' selected':'')+'>'+esc(s)+'</option>';}).join('');
    qs('fCampus').innerHTML='<option value="">All campuses</option>'+(L.campuses||[]).map(function(c){return '<option value="'+esc(c.code)+'"'+(F.campus==c.code?' selected':'')+'>'+esc(c.name)+'</option>';}).join('');
    qs('fSource').value=F.source||'';
    // searchable combos
    facCombo=makeCombo(qs('cFac'), [{value:'',label:'All faculties'}].concat((L.faculties||[]).map(function(f){return {value:f.code,label:f.name};})), 'All faculties', F.fac||'');
    progCombo=makeCombo(qs('cProg'), [{value:'',label:'All programmes'}].concat((L.programmes||[]).map(function(p){return {value:p.code,label:p.code+' — '+p.name};})), 'All programmes', F.prog||'');
    if(!F.hasSource) qs('fSource').parentNode.style.display='none';
    renderKpis(); renderTable();
    var k=D.kpis||{}; var t=k.total||0;
    qs('aaSub').textContent=nf(t)+' applicant'+(t===1?'':'s')+' in scope · offer rate '+pct((k.admitted||0)+(k.registered||0),t)+' · reg rate '+pct(k.registered||0,t);
  }

  function apply(){
    var sp=[];
    function add(k,v){ if(v) sp.push(k+'='+encodeURIComponent(v)); }
    add('year',qs('fYear').value); add('session',qs('fSession').value); add('source',qs('fSource').value); add('campus',qs('fCampus').value);
    add('fac',facCombo?facCombo.value():''); add('prog',progCombo?progCombo.value():'');
    location.href='AdmissionAnalysis.aspx'+(sp.length?('?'+sp.join('&')):'');
  }
  function clr(){ location.href='AdmissionAnalysis.aspx'; }

  function renderKpis(){
    var k=D.kpis||{}, t=k.total||0;
    var offer=(k.admitted||0)+(k.registered||0);
    function cell(mod,n,l){ return '<div class="aa-kpi aa-kpi--'+mod+'"><div class="aa-kpi__n">'+n+'</div><div class="aa-kpi__l">'+l+'</div></div>'; }
    var h=cell('total',nf(t),'Total applicants')
      +cell('pending',nf(k.pending||0),'Pending')
      +cell('admitted',nf(k.admitted||0),'Admitted, not taken up')
      +cell('registered',nf(k.registered||0),'Took up the place')
      +cell('rejected',nf((k.rejected||0)+(k.withdrawn||0)),'Rejected / Withdrawn')
      +((k.other||0)?cell('pending',nf(k.other),'Other status'):'')
      +cell('rate',pct(offer,t),'Offer rate')
      +cell('rate',pct(k.registered||0,offer),'Take-up rate');
    qs('aaKpis').innerHTML=h;
    /* Two figures changed meaning and the reader is told so rather than left to assume. */
    var na=(D.notAnalysed||0);
    qs('aaNote').innerHTML='<b>Took up the place</b> counts applicants who actually registered for a semester. '
      +'It previously counted anyone holding an entry number, which is issued at admission, so nearly every '
      +'admitted applicant was reported as registered. <b>Admitted, not taken up</b> is the remainder — an offer made and never acted on.'
      +(na?(' &middot; '+nf(na)+' application'+(na==1?'':'s')+' carry no primary choice and are not included in any figure on this page.'):'');
  }

  function rows(){ return (D[DIM]||[]).slice(); }
  function sortRows(list){
    return list.sort(function(a,b){
      var av,bv;
      if(SORT==='name'){ av=(a.name||'').toLowerCase(); bv=(b.name||'').toLowerCase(); return av<bv?-DIR:(av>bv?DIR:0); }
      if(SORT==='sessions'){ av=a.sessions||0; bv=b.sessions||0; return (av-bv)*DIR; }
      av=a.stat[SORT]||0; bv=b.stat[SORT]||0; return (av-bv)*DIR;
    });
  }
  function th(key,label,cls){ var on=(SORT===key); return '<th class="'+(cls||'')+(on?' on':'')+'" onclick="AA.sort(\''+key+'\')">'+label+(on?'<span class="ar">'+(DIR<0?'▼':'▲')+'</span>':'')+'</th>'; }

  function renderTable(){
    var list=sortRows(rows());
    qs('aaDimTitle').textContent=DIMLABEL[DIM]+' · '+list.length+' row'+(list.length===1?'':'s');
    if(!list.length){ qs('aaHost').innerHTML='<div class="aa-empty">No applicants match these filters.</div>'; return; }
    var maxTotal=0; list.forEach(function(r){ if(r.stat.total>maxTotal) maxTotal=r.stat.total; });
    var showSessions=(DIM==='byProg'||DIM==='byFaculty'||DIM==='byCampus'||DIM==='byYear');
    var T={total:0,pending:0,admitted:0,registered:0,rejected:0,withdrawn:0,other:0};
    var h='<div class="aa-tbl-wrap"><table class="aa-table"><thead><tr><th class="aa-rank">#</th>'
      +th('name',esc(DIMCOL[DIM]))
      +(showSessions?th('sessions','Sessions','num'):'')
      +th('total','Applicants','num')+th('pending','Pending','num')+th('admitted','Admitted','num')
      +th('registered','Registered','num')+th('rejected','Rejected','num')+th('withdrawn','Withdrawn','num')+th('other','Other','num')
      +'<th class="num">Offer %</th><th class="num">Reg %</th>'
      +'<th class="num" title="Enrolled as a share of those offered a place \u2014 the Admissions Office retention rate">Retention %</th>'
      +'</tr></thead><tbody>';
    list.forEach(function(r,i){
      var s=r.stat; T.total+=s.total;T.pending+=s.pending;T.admitted+=s.admitted;T.registered+=s.registered;T.rejected+=s.rejected;T.withdrawn+=s.withdrawn;T.other+=(s.other||0);
      var offer=s.admitted+s.registered, bw=maxTotal>0?Math.round(s.total/maxTotal*60):0;
      var sub=(DIM==='byProg'&&r.facCode)?('<div class="sub">'+esc(r.facName||r.facCode)+'</div>'):'';
      h+='<tr><td class="aa-rank">'+(i+1)+'</td>'
        +'<td class="name">'+esc(r.name)+(DIM==='byProg'&&r.code&&r.code!==r.name?'':'')+sub+'</td>'
        +(showSessions?('<td class="num">'+(r.sessions||0)+'</td>'):'')
        +'<td class="num"><span class="aa-bar" style="width:'+bw+'px"></span>'+nf(s.total)+'</td>'
        +'<td class="num">'+(s.pending?nf(s.pending):'<span class="aa-mut">0</span>')+'</td>'
        +'<td class="num">'+(s.admitted?nf(s.admitted):'<span class="aa-mut">0</span>')+'</td>'
        +'<td class="num">'+(s.registered?nf(s.registered):'<span class="aa-mut">0</span>')+'</td>'
        +'<td class="num">'+(s.rejected?nf(s.rejected):'<span class="aa-mut">0</span>')+'</td>'
        +'<td class="num">'+(s.withdrawn?nf(s.withdrawn):'<span class="aa-mut">0</span>')+'</td>'
        +'<td class="num">'+(s.other?nf(s.other):'<span class="aa-mut">0</span>')+'</td>'
        +'<td class="num">'+pct(offer,s.total)+'</td>'
        +'<td class="num">'+pct(s.registered,s.total)+'</td>'
        +'<td class="num"><b>'+pct(s.registered,offer)+'</b></td></tr>';
    });
    var toffer=T.admitted+T.registered;
    h+='</tbody><tfoot><tr><td></td><td>TOTAL — '+list.length+'</td>'
      +(showSessions?'<td></td>':'')
      +'<td class="num">'+nf(T.total)+'</td><td class="num">'+nf(T.pending)+'</td><td class="num">'+nf(T.admitted)+'</td>'
      +'<td class="num">'+nf(T.registered)+'</td><td class="num">'+nf(T.rejected)+'</td><td class="num">'+nf(T.withdrawn)+'</td><td class="num">'+nf(T.other)+'</td>'
      +'<td class="num">'+pct(toffer,T.total)+'</td><td class="num">'+pct(T.registered,T.total)+'</td>'
      +'<td class="num"><b>'+pct(T.registered,toffer)+'</b></td></tr></tfoot></table></div>';
    qs('aaHost').innerHTML=h;
  }

  function dim(d){ DIM=d; SORT='total'; DIR=-1; [].forEach.call(qs('aaDims').children,function(b){ b.className=(b.getAttribute('data-dim')===d?'on':''); }); renderTable(); }
  function sort(key){ if(SORT===key) DIR=-DIR; else { SORT=key; DIR=(key==='name'?1:-1); } renderTable(); }

  /* ── CSV (current dimension) ── */
  function csvCell(v){ v=(v==null?'':''+v); if(/[",\n]/.test(v)) v='"'+v.replace(/"/g,'""')+'"'; return v; }
  function csv(){
    var list=sortRows(rows()); if(!list.length){ alert('Nothing to export.'); return; }
    var ss=(DIM==='byProg'||DIM==='byFaculty'||DIM==='byCampus'||DIM==='byYear');
    var head=[DIMCOL[DIM]].concat(ss?['Sessions']:[]).concat(['Applicants','Pending','Admitted','Registered','Rejected','Withdrawn','Other','Offer%','Reg%','Retention%']);
    var lines=[head.join(',')];
    var T={total:0,pending:0,admitted:0,registered:0,rejected:0,withdrawn:0,other:0};
    list.forEach(function(r){ var s=r.stat; T.total+=s.total;T.pending+=s.pending;T.admitted+=s.admitted;T.registered+=s.registered;T.rejected+=s.rejected;T.withdrawn+=s.withdrawn;T.other+=(s.other||0);
      lines.push([r.name].concat(ss?[r.sessions||0]:[]).concat([s.total,s.pending,s.admitted,s.registered,s.rejected,s.withdrawn,(s.other||0),pctv(s.admitted+s.registered,s.total).toFixed(1),pctv(s.registered,s.total).toFixed(1),pctv(s.registered,s.admitted+s.registered).toFixed(1)]).map(csvCell).join(',')); });
    lines.push(['TOTAL'].concat(ss?['']:[]).concat([T.total,T.pending,T.admitted,T.registered,T.rejected,T.withdrawn,T.other,pctv(T.admitted+T.registered,T.total).toFixed(1),pctv(T.registered,T.total).toFixed(1),pctv(T.registered,T.admitted+T.registered).toFixed(1)]).map(csvCell).join(','));
    var blob=new Blob([lines.join('\r\n')],{type:'text/csv;charset=utf-8;'});
    var a=document.createElement('a'); a.href=URL.createObjectURL(blob); a.download='admission_analysis_'+DIM+'.csv'; document.body.appendChild(a); a.click(); document.body.removeChild(a);
  }

  /* ── branded print (KPIs + every dimension) ── */
  function ptable(title, list, keepOrder){
    if(!list||!list.length) return '';
    var T={total:0,pending:0,admitted:0,registered:0,rejected:0,withdrawn:0,other:0};
    var body='';
    var seq=list.slice(); if(!keepOrder) seq.sort(function(a,b){return b.stat.total-a.stat.total;});
    seq.forEach(function(r,i){ var s=r.stat;
      T.total+=s.total;T.pending+=s.pending;T.admitted+=s.admitted;T.registered+=s.registered;T.rejected+=s.rejected;T.withdrawn+=s.withdrawn;T.other+=(s.other||0);
      body+='<tr><td class="n">'+(i+1)+'</td><td>'+esc(r.name)+'</td><td class="c">'+nf(s.total)+'</td><td class="c">'+nf(s.pending)+'</td><td class="c">'+nf(s.admitted)+'</td><td class="c">'+nf(s.registered)+'</td><td class="c">'+nf(s.rejected)+'</td><td class="c">'+nf(s.withdrawn)+'</td><td class="c">'+pct(s.admitted+s.registered,s.total)+'</td><td class="c">'+pct(s.registered,s.total)+'</td><td class="c"><b>'+pct(s.registered,s.admitted+s.registered)+'</b></td></tr>';
    });
    body+='<tr class="tot"><td></td><td><b>TOTAL</b></td><td class="c">'+nf(T.total)+'</td><td class="c">'+nf(T.pending)+'</td><td class="c">'+nf(T.admitted)+'</td><td class="c">'+nf(T.registered)+'</td><td class="c">'+nf(T.rejected)+'</td><td class="c">'+nf(T.withdrawn)+'</td><td class="c">'+pct(T.admitted+T.registered,T.total)+'</td><td class="c">'+pct(T.registered,T.total)+'</td><td class="c"><b>'+pct(T.registered,T.admitted+T.registered)+'</b></td></tr>';
    return '<h3>'+esc(title)+'</h3><table><thead><tr><th>#</th><th>'+esc(title.replace(/^By /,''))+'</th><th class="c">Applicants</th><th class="c">Pending</th><th class="c">Admitted</th><th class="c">Registered</th><th class="c">Rejected</th><th class="c">Withdrawn</th><th class="c">Offer %</th><th class="c">Reg %</th><th class="c">Retention %</th></tr></thead><tbody>'+body+'</tbody></table>';
  }
  function print(){
    var k=D.kpis||{}, t=k.total||0, F=D.filters||{};
    var meta=[];
    if(F.year) meta.push('Year '+F.year); if(F.session) meta.push(F.session);
    if(F.source) meta.push(F.source==='ONLINE'?'Online':'Walk-in');
    if(F.fac){ var fo=(D.lists.faculties||[]).filter(function(x){return x.code===F.fac;})[0]; if(fo) meta.push(fo.name); }
    if(F.prog){ var po=(D.lists.programmes||[]).filter(function(x){return x.code===F.prog;})[0]; if(po) meta.push(po.name); }
    meta.push(nf(t)+' applicants'); meta.push('offer '+pct(k.admitted+k.registered,t)); meta.push('reg '+pct(k.registered,t));
    var now=new Date(), MON=['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    var stamp=now.getDate()+' '+MON[now.getMonth()]+' '+now.getFullYear();
    var logo=(location.origin||'')+'/COOPERP/images/welcomelogo.png';
    var css='@page{size:A4 landscape;margin:11mm 11mm 13mm;}*{box-sizing:border-box;}body{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;color:#1a1a2e;margin:0;font-size:10px;}'
      +'.hd{display:flex;align-items:center;gap:13px;border-bottom:3px solid #05275C;padding-bottom:8px;margin-bottom:10px;}.hd img{height:44px;}'
      +'.hd .u{font-size:15px;font-weight:800;color:#05275C;line-height:1.1;}.hd .t{font-size:12px;font-weight:700;color:#174DA4;margin-top:2px;}.hd .m{font-size:9px;color:#555;margin-top:3px;}.hd .m span{color:#c3ccda;margin:0 5px;}'
      +'h3{font-size:11px;color:#05275C;margin:12px 0 4px;text-transform:uppercase;letter-spacing:.4px;border-left:3px solid #174DA4;padding-left:7px;}'
      +'table{width:100%;border-collapse:collapse;margin-bottom:6px;page-break-inside:auto;}tr{page-break-inside:avoid;}'
      +'thead th{background:#05275C;color:#fff;text-align:left;font-size:8px;font-weight:800;letter-spacing:.3px;text-transform:uppercase;padding:4px 6px;}th.c,td.c{text-align:right;}'
      +'td{padding:3px 6px;border-bottom:1px solid #edf1f6;font-size:9.5px;}td.n{color:#94a3b8;}tbody tr:nth-child(even) td{background:#fafbfd;}tr.tot td{background:#eef2f8;border-top:2px solid #05275C;font-weight:800;color:#05275C;}'
      +'.ft{margin-top:6px;border-top:1px solid #e0e5ed;padding-top:6px;font-size:9px;color:#94a3b8;display:flex;justify-content:space-between;}';
    var html='<!doctype html><html><head><meta charset="utf-8"><title>Admission Analysis</title><style>'+css+'</style></head><body>'
      +'<div class="hd"><img src="'+logo+'" onerror="this.style.display=\'none\'" alt=""/><div>'
      +'<div class="u">Muteesa I Royal University</div><div class="t">Admission Analysis</div>'
      +'<div class="m">'+meta.map(esc).join('<span>&bull;</span>')+'</div></div></div>'
      +ptable('By Intake (Year \u00d7 Session)', D.byCohort, true)+ptable('By Programme', D.byProg)+ptable('By Faculty', D.byFaculty)+ptable('By Campus', D.byCampus)+ptable('By Year', D.byYear)+ptable('By Session', D.bySession)+ptable('By Source', D.bySource)
      +'<div class="ft"><span>Generated '+stamp+'</span><span>eadmin.mru.ac.ug</span></div></body></html>';
    var w=window.open('','_blank'); if(!w){ alert('Please allow pop-ups to open the printable analysis.'); return; }
    w.document.open(); w.document.write(html); w.document.close(); w.focus();
    setTimeout(function(){ try{ w.print(); }catch(e){} },350);
  }

  /* ── reconciliation ── */
  function api(m,b){ return fetch('AdmissionAnalysis.aspx/'+m,{method:'POST',headers:{'Content-Type':'application/json; charset=utf-8'},body:JSON.stringify(b||{})}).then(function(r){return r.json();}).then(function(j){return j.d;}); }
  function rcBusy(on,txt){ if(txt) qs('rcBusyTxt').textContent=txt; qs('rcBusy').classList.toggle('on',!!on); }
  function reconcile(){ qs('rcResult').innerHTML=''; qs('rcRunBtn').style.display='none'; qs('rcOv').classList.add('on'); }
  function rcClose(){ qs('rcOv').classList.remove('on'); }
  function rcYears(){ return { minYear:parseInt(qs('rcMin').value,10)||2024, maxYear:parseInt(qs('rcMax').value,10)||2027 }; }
  function rcPreview(){
    rcBusy(true,'Scanning…'); qs('rcRunBtn').style.display='none';
    api('ReconcilePreview',rcYears()).then(function(d){
      rcBusy(false);
      if(!d||!d.ok){ qs('rcResult').innerHTML='<div class="rc-err">'+esc((d&&d.error)||'Failed.')+'</div>'; return; }
      var h='<div class="rc-kpis"><div class="rc-kpi"><div class="rc-kpi__n">'+d.orphans+'</div><div class="rc-kpi__l">Active w/o record</div></div>'
        +'<div class="rc-kpi"><div class="rc-kpi__n ok">'+d.backfillable+'</div><div class="rc-kpi__l">Will be created</div></div>'
        +'<div class="rc-kpi"><div class="rc-kpi__n'+(d.flagged?' warn':'')+'">'+d.flagged+'</div><div class="rc-kpi__l">Flagged (manual)</div></div></div>';
      if(d.byYear&&d.byYear.length){ h+='<div class="rc-sub">By year</div><table class="rc-tbl"><thead><tr><th>Year</th><th>To create</th></tr></thead><tbody>'
        +d.byYear.sort(function(a,b){return b.year-a.year;}).map(function(y){return '<tr><td>'+y.year+'</td><td>'+y.count+'</td></tr>';}).join('')+'</tbody></table>'; }
      if(d.flagged>0){ h+='<div class="rc-flag"><b>'+d.flagged+' cannot be auto-created</b> — their reg-no is longer than 15 chars so it will not fit the admission key. Handle these manually.<table class="rc-tbl" style="margin-top:6px;"><thead><tr><th>Reg No</th><th>Name</th><th>Prog</th><th>Yr</th></tr></thead><tbody>'
        +d.flaggedList.map(function(f){return '<tr><td>'+esc(f.regno)+'</td><td>'+esc(f.name)+'</td><td>'+esc(f.progid)+'</td><td>'+esc(f.year)+'</td></tr>';}).join('')+'</tbody></table></div>'; }
      if(d.backfillable===0){ h+='<div class="rc-ok" style="margin-top:8px;">Nothing to create — every active student in this range already has an admission record. ✓</div>'; }
      else { qs('rcRunBtn').style.display=''; qs('rcRunBtn').textContent='Create '+d.backfillable+' record'+(d.backfillable===1?'':'s'); }
      qs('rcResult').innerHTML=h;
    }).catch(function(){ rcBusy(false); qs('rcResult').innerHTML='<div class="rc-err">Network error.</div>'; });
  }
  function rcRun(){
    if(!confirm('Create admission records for the previewed active students? This is additive and safe to re-run.')) return;
    rcBusy(true,'Creating records…'); qs('rcRunBtn').style.display='none';
    api('ReconcileRun',rcYears()).then(function(d){
      rcBusy(false);
      if(!d||!d.ok){ qs('rcResult').innerHTML='<div class="rc-err">'+esc((d&&d.error)||'Failed.')+'</div>'; return; }
      var h='<div class="rc-ok">Created '+d.inserted+' admission record'+(d.inserted===1?'':'s')+'.'+(d.flaggedSkipped?(' '+d.flaggedSkipped+' flagged case(s) skipped (long reg-no).'):'')+' The analysis will refresh.</div>';
      qs('rcResult').innerHTML=h;
      setTimeout(function(){ location.reload(); },1400);
    }).catch(function(){ rcBusy(false); qs('rcResult').innerHTML='<div class="rc-err">Network error.</div>'; });
  }
  qs('rcOv').addEventListener('click',function(e){ if(e.target===qs('rcOv')) rcClose(); });

  init();
  return { apply:apply, clear:clr, dim:dim, sort:sort, csv:csv, print:print, reconcile:reconcile, rcClose:rcClose, rcPreview:rcPreview, rcRun:rcRun };
})();
</script>
</asp:Content>
