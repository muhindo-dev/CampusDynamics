<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ResultsExporter.aspx.cs" Inherits="COOPERP_NewScreens_ResultsExporter" Title="Results Export Centre - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<script src="https://cdn.jsdelivr.net/npm/chart.js@3.9.1/dist/chart.min.js"></script>
<style type="text/css">
    .re-wrap { padding:12px; color:#1a1a2e; min-width:0; max-width:100%; box-sizing:border-box; }

    /* ── Horizontal-overflow fix ──────────────────────────────────────────────
       The master's content column is `.cd-content{flex:1}` with the default
       min-width:auto, so it refuses to shrink below its content's intrinsic
       width — Chart.js canvases (which carry a pixel width) and grid children
       then push the whole column past the viewport (content cut off on the right
       until zoomed out). Allowing every flex/grid child to shrink to zero fixes
       it. Scoped to this page (inline <style> loads only here). */
    .cd-main, .cd-content { min-width:0 !important; }   /* also fixed globally in sidebar.css */
    .re-wrap *, .re-wrap { box-sizing:border-box; }
    .re-stats, .re-charts, .re-perf { min-width:0; }
    .re-stats > *, .re-charts > *, .re-perf > *, .re-card { min-width:0; }
    .re-wrap canvas { max-width:100% !important; }
    .re-chartbox { width:100%; }
    .re-hd__l { min-width:0; }
    .re-chip { max-width:100%; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .re-hd { display:flex; align-items:center; justify-content:space-between; background:#05275C; color:#fff; padding:12px 18px; }
    .re-hd__l { display:flex; align-items:center; gap:12px; }
    .re-hd__ic { width:38px; height:38px; background:rgba(255,255,255,.12); display:flex; align-items:center; justify-content:center; }
    .re-hd__t { font-size:16px; font-weight:700; }
    .re-hd__s { font-size:11px; opacity:.8; margin-top:2px; }
    .re-chip { background:rgba(255,255,255,.15); font-size:10px; padding:3px 9px; border-radius:10px; }

    .re-filter { background:#fff; border:1px solid #e0e5ed; padding:10px 12px; margin-top:12px; }
    .re-row { display:flex; flex-wrap:wrap; gap:8px; align-items:flex-end; }
    .re-fld { display:flex; flex-direction:column; gap:2px; flex:1 1 128px; min-width:0; }
    .re-fld label { font-size:9px; font-weight:600; text-transform:uppercase; letter-spacing:.3px; color:#555; }
    .re-fld select, .re-fld input { padding:5px 7px; border:1px solid #cdd3de; font-size:11px; border-radius:0; background:#fff; width:100%; min-width:0; font-family:inherit; box-sizing:border-box; }
    .re-srcnote { margin-top:8px; font-size:10px; color:#8a5a00; background:#fff8e1; border:1px solid #fde3a7; padding:5px 9px; }

    .re-cfg { display:flex; flex-wrap:wrap; gap:14px; align-items:center; margin-top:10px; padding-top:10px; border-top:1px solid #eef0f4; }
    .re-seg { display:inline-flex; border:1px solid #cdd3de; }
    .re-seg button { background:#fff; border:0; border-right:1px solid #cdd3de; padding:6px 12px; font-size:11px; cursor:pointer; color:#333; font-family:inherit; }
    .re-seg button:last-child { border-right:0; }
    .re-seg button.on { background:#05275C; color:#fff; }
    .re-seglbl { font-size:9px; font-weight:600; text-transform:uppercase; letter-spacing:.3px; color:#888; margin-right:6px; }

    .re-stats { display:grid; grid-template-columns:repeat(6,minmax(0,1fr)); gap:8px; margin-top:12px; }
    .re-kpi { background:#fff; border:1px solid #e0e5ed; border-left:3px solid #174DA4; padding:10px 12px; }
    .re-kpi b { display:block; font-size:19px; font-weight:700; color:#05275C; line-height:1; }
    .re-kpi span { font-size:9px; text-transform:uppercase; letter-spacing:.3px; color:#888; margin-top:4px; display:block; }

    .re-charts { display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1fr) minmax(0,1.1fr); gap:12px; margin-top:12px; }

    /* Staged marks-submission pipeline */
    .re-pipe { display:flex; flex-direction:column; gap:7px; }
    .re-pipe__hd { display:flex; justify-content:space-between; align-items:baseline; margin-bottom:2px; }
    .re-pipe__pct { font-size:20px; font-weight:700; color:#16a34a; }
    .re-pipe__pctl { font-size:9px; color:#888; text-transform:uppercase; }
    .re-stage { display:grid; grid-template-columns:78px 1fr 44px; align-items:center; gap:7px; }
    .re-stage__l { font-size:9px; font-weight:600; text-transform:uppercase; letter-spacing:.2px; color:#555; }
    .re-stage__bar { height:9px; background:#eef2f7; overflow:hidden; }
    .re-stage__bar>span { display:block; height:100%; }
    .re-stage__n { font-size:10px; font-weight:700; color:#1a1a2e; text-align:right; }

    /* Top / lowest performers */
    .re-perf { display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1fr); gap:12px; margin-top:12px; }
    .re-plist { list-style:none; margin:0; padding:0; }
    .re-plist li { display:grid; grid-template-columns:20px 1fr auto; gap:8px; align-items:center; padding:4px 2px; border-bottom:1px solid #f0f2f6; font-size:10px; }
    .re-plist li:last-child { border-bottom:0; }
    .re-plist .rk { color:#9098a5; font-weight:700; font-size:9px; text-align:center; }
    .re-plist .nm { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .re-plist .nm small { color:#9098a5; }
    .re-plist .gp { font-weight:700; }
    .re-plist .gp--hi { color:#16a34a; } .re-plist .gp--lo { color:#dc3545; }

    /* Columns popover */
    .re-colwrap { position:relative; }
    .re-colbtn { font-size:10px; padding:3px 9px; border:1px solid #cdd3de; background:#fff; cursor:pointer; color:#333; }
    .re-colpop { display:none; position:absolute; right:0; top:26px; z-index:50; background:#fff; border:1px solid #cdd3de; box-shadow:0 8px 24px rgba(0,0,0,.15); padding:8px; min-width:190px; max-height:300px; overflow:auto; }
    .re-colpop.show { display:block; }
    .re-colpop label { display:flex; align-items:center; gap:6px; font-size:10px; padding:3px 4px; cursor:pointer; color:#333; }
    .re-colpop label:hover { background:#f5f7fa; }
    .re-card { background:#fff; border:1px solid #e0e5ed; }
    .re-card__h { font-size:11px; font-weight:600; color:#1a1a2e; padding:9px 12px; border-bottom:1px solid #e0e5ed; background:#f5f7fa; display:flex; justify-content:space-between; align-items:center; }
    .re-card__h small { color:#888; font-weight:400; }
    .re-card__b { padding:10px; }
    .re-chartbox { position:relative; height:230px; }

    .re-tblwrap { overflow:auto; max-height:460px; }
    .re-tbl { width:100%; border-collapse:collapse; font-size:10px; }
    .re-tbl th { position:sticky; top:0; background:#05275C; color:#fff; padding:6px 9px; text-align:left; font-size:9px; text-transform:uppercase; letter-spacing:.3px; white-space:nowrap; }
    .re-tbl td { padding:5px 9px; border-bottom:1px solid #eef0f4; white-space:nowrap; }
    .re-tbl tbody tr:nth-child(even) td { background:#f9fafc; }

    .re-actions { display:flex; gap:8px; align-items:center; margin-top:12px; }
    .re-btn { padding:8px 16px; font-size:12px; font-weight:600; border:1px solid transparent; cursor:pointer; border-radius:0; font-family:inherit; }
    .re-btn--primary { background:#05275C; color:#fff; }
    .re-btn--primary:hover { background:#041d45; }
    .re-btn--accent { background:#174DA4; color:#fff; }
    .re-btn--ghost { background:#fff; color:#333; border-color:#cdd3de; }
    .re-btn:disabled { opacity:.5; cursor:not-allowed; }
    .re-btn--sm { padding:6px 12px; font-size:11px; }
    .re-total { font-size:11px; color:#555; margin-left:auto; }
    .re-exp { display:inline-flex; align-items:center; gap:6px; margin-left:auto; flex-wrap:wrap; }
    .re-exp .re-seglbl { margin-right:2px; }

    .re-note { font-size:9px; color:#9098a5; font-style:italic; margin-top:6px; }
    .re-err { display:none; background:#fef5f5; border:1px solid #f5c6cb; color:#dc3545; font-size:11px; padding:8px 12px; margin-top:10px; }
    .re-err.show { display:block; }
    .re-noaccess { display:none; background:#fff8e1; border:1px solid #fcd34d; color:#b45309; font-size:12px; padding:14px 18px; margin-top:12px; }

    .md-loader { position:fixed; inset:0; background:rgba(245,247,250,.8); z-index:9998; display:none; align-items:center; justify-content:center; }
    .md-loader.show { display:flex; }
    .md-spinner { width:42px; height:42px; border:4px solid #e6ebf2; border-top-color:#05275C; border-radius:50%; animation:respin .75s linear infinite; margin:0 auto; }
    .md-loader__t { font-size:12px; color:#05275C; font-weight:600; margin-top:12px; text-align:center; }
    @keyframes respin { to { transform:rotate(360deg); } }
    @media (max-width:1100px){ .re-stats{grid-template-columns:repeat(3,minmax(0,1fr));} .re-charts{grid-template-columns:minmax(0,1fr);} .re-perf{grid-template-columns:minmax(0,1fr);} }
    @media (max-width:640px){
        .re-wrap{padding:8px;}
        .re-stats{grid-template-columns:repeat(2,minmax(0,1fr));gap:6px;}
        .re-kpi{padding:8px 9px;} .re-kpi b{font-size:16px;}
        .re-hd{padding:10px 12px;} .re-hd__s{display:none;}
        .re-fld{flex:1 1 46%;}
        .re-cfg{gap:8px;} .re-seg{flex-wrap:wrap;}
        .re-actions{flex-wrap:wrap;} .re-total{margin-left:0;width:100%;}
    }
    @media (max-width:400px){ .re-stats{grid-template-columns:minmax(0,1fr) minmax(0,1fr);} .re-fld{flex:1 1 100%;} }

    /* ── Summary Report: standout launch button (gold on navy header) ── */
    .re-hd__r { display:flex; align-items:center; gap:12px; flex-wrap:wrap; justify-content:flex-end; }
    .re-srlaunch { position:relative; display:inline-flex; align-items:center; gap:9px; padding:9px 16px; border:none; cursor:pointer; border-radius:24px; font-family:inherit; font-size:12.5px; font-weight:800; color:#05275C; white-space:nowrap; overflow:hidden;
        background:linear-gradient(135deg,#ffe694 0%,#f6b301 55%,#e59400 100%); box-shadow:0 4px 14px rgba(245,179,1,.45), inset 0 1px 0 rgba(255,255,255,.55); transition:transform .15s, box-shadow .15s; }
    .re-srlaunch:hover { transform:translateY(-1px); box-shadow:0 8px 22px rgba(245,179,1,.62), inset 0 1px 0 rgba(255,255,255,.65); }
    .re-srlaunch:active { transform:translateY(0); }
    .re-srlaunch svg { width:17px; height:17px; stroke:#05275C; flex-shrink:0; }
    .re-srlaunch__tag { font-size:8px; font-weight:900; letter-spacing:.5px; background:#05275C; color:#ffd45e; padding:2px 6px; border-radius:8px; }
    .re-srlaunch__sheen { position:absolute; top:0; left:-60%; width:38%; height:100%; background:linear-gradient(100deg,transparent,rgba(255,255,255,.6),transparent); transform:skewX(-18deg); animation:reSheen 3.6s ease-in-out infinite; pointer-events:none; }
    @keyframes reSheen { 0%,58%{left:-60%} 100%{left:135%} }

    /* ── Summary Report modal ── */
    .sr-ov { display:none; position:fixed; inset:0; background:rgba(6,20,45,.55); z-index:10000; align-items:flex-start; justify-content:center; padding:26px 14px; overflow-y:auto; }
    .sr-ov.show { display:flex; }
    .sr-modal { background:#fff; width:560px; max-width:100%; border-radius:4px; box-shadow:0 24px 60px rgba(0,0,0,.32); overflow:hidden; animation:srPop .18s ease; }
    @keyframes srPop { from{opacity:0;transform:translateY(-8px) scale(.985)} to{opacity:1;transform:none} }
    .sr-hd { display:flex; align-items:center; justify-content:space-between; gap:10px; padding:14px 18px; background:linear-gradient(135deg,#05275C,#174DA4); color:#fff; }
    .sr-hd__t { font-size:14px; font-weight:800; display:flex; align-items:center; gap:8px; }
    .sr-hd__x { background:none; border:none; color:#fff; font-size:24px; line-height:1; cursor:pointer; opacity:.85; }
    .sr-hd__x:hover { opacity:1; }
    .sr-bd { padding:16px 18px; max-height:calc(100vh - 200px); overflow-y:auto; }
    .sr-intro { font-size:11px; color:#64748b; margin:0 0 14px; line-height:1.5; }
    .sr-req { color:#dc3545; }
    .sr-fg { margin-bottom:12px; }
    .sr-fg label { display:block; font-size:10px; text-transform:uppercase; letter-spacing:.4px; color:#475569; font-weight:700; margin-bottom:4px; }
    .sr-in,.sr-sel,.sr-ta { width:100%; box-sizing:border-box; border:1px solid #cdd5e1; border-radius:4px; padding:8px 10px; font-size:12px; font-family:inherit; color:#1a1a2e; background:#fff; }
    .sr-in:focus,.sr-sel:focus,.sr-ta:focus { outline:none; border-color:#174DA4; box-shadow:0 0 0 3px rgba(23,77,164,.12); }
    .sr-ta { resize:vertical; min-height:52px; }
    .sr-hint { font-size:10px; color:#94a3b8; margin-top:4px; display:block; }
    .sr-hint code { background:#f1f5f9; padding:1px 4px; border-radius:3px; }
    .sr-combo { position:relative; }
    .sr-list { display:none; position:absolute; top:calc(100% + 2px); left:0; right:0; z-index:20; background:#fff; border:1px solid #cdd5e1; border-radius:4px; max-height:210px; overflow-y:auto; box-shadow:0 8px 24px rgba(5,39,92,.16); }
    .sr-combo.open .sr-list { display:block; }
    .sr-opt { padding:7px 10px; font-size:11px; cursor:pointer; border-bottom:1px solid #f0f3f8; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .sr-opt:last-child { border-bottom:none; }
    .sr-opt:hover,.sr-opt.active { background:#eef4ff; color:#05275C; }
    .sr-opt small { color:#94a3b8; }
    .sr-none { padding:8px 10px; font-size:11px; color:#94a3b8; font-style:italic; }
    .sr-div { border:none; border-top:1px solid #eef2f7; margin:14px 0; }
    .sr-chk { display:flex; align-items:center; gap:8px; font-size:12px; font-weight:600; color:#334155; cursor:pointer; }
    .sr-chk input { width:15px; height:15px; margin:0; cursor:pointer; }
    /* Yes/No question pair (specialisation) */
    .sr-ask { display:flex; gap:8px; flex-wrap:wrap; }
    .sr-ask label { flex:1 1 0; min-width:0; display:flex; align-items:center; gap:7px; padding:7px 10px; border:1px solid #cdd5e1; border-radius:4px; background:#fff; font-size:11.5px; font-weight:600; color:#475569; cursor:pointer; }
    .sr-ask label:hover { border-color:#174DA4; }
    .sr-ask input { width:14px; height:14px; margin:0; cursor:pointer; flex-shrink:0; }
    .sr-ask input:checked + span { color:#05275C; }
    .sr-ask label.on { border-color:#174DA4; background:#f0f7ff; }
    .sr-ask label.off { opacity:.5; cursor:not-allowed; }
    .sr-ask label.off:hover { border-color:#cdd5e1; }
    .sr-spec { display:none; margin-top:8px; }
    .sr-spec.show { display:block; }
    .sr-prev { display:none; align-items:center; gap:14px; margin-top:14px; padding:12px 14px; background:#f0f7ff; border:1px solid #cfe0f5; border-radius:4px; }
    .sr-prev.show { display:flex; }
    .sr-prev__n { font-size:26px; font-weight:800; color:#05275C; line-height:1; font-variant-numeric:tabular-nums; }
    .sr-prev__l { font-size:11px; color:#475569; }
    .sr-ft { display:flex; align-items:center; gap:8px; flex-wrap:wrap; padding:12px 18px; border-top:1px solid #eef2f7; background:#fafbfc; }
    .sr-ft__sp { flex:1 1 auto; }
    .sr-btn { padding:8px 14px; font-size:12px; font-weight:600; border:1px solid transparent; border-radius:4px; cursor:pointer; font-family:inherit; display:inline-flex; align-items:center; gap:6px; white-space:nowrap; }
    .sr-btn svg { width:14px; height:14px; }
    .sr-btn--nav { background:#174DA4; color:#fff; } .sr-btn--nav:hover { background:#123d84; }
    .sr-btn--primary { background:#05275C; color:#fff; } .sr-btn--primary:hover { background:#041d45; }
    .sr-btn--ok { background:#16a34a; color:#fff; } .sr-btn--ok:hover { background:#12833c; }
    .sr-btn--ghost { background:#fff; color:#334155; border-color:#cdd5e1; } .sr-btn--ghost:hover { background:#f1f5f9; }
    .sr-btn:disabled { opacity:.5; cursor:not-allowed; }
    .sr-sel:disabled { background:#f5f7fa; color:#9aa6b6; cursor:not-allowed; }
    .sr-status { font-size:10.5px; color:#64748b; margin:0 0 12px; min-height:14px; display:flex; align-items:center; gap:6px; line-height:1.4; }
    .sr-status--load { color:#174DA4; } .sr-status--ok { color:#16a34a; } .sr-status--warn { color:#b45309; }
    .sr-spin { width:11px; height:11px; border:2px solid #cfe0f5; border-top-color:#174DA4; border-radius:50%; animation:respin .7s linear infinite; display:inline-block; flex-shrink:0; }
    @media (max-width:560px){ .sr-ft{flex-direction:column;align-items:stretch;} .sr-ft__sp{display:none;} .sr-btn{justify-content:center;} }

/* The insights toggle. Deliberately quiet: it is a switch on a cost, not a headline. */
.re-insbar{display:flex;align-items:center;gap:9px;background:#fff;border:1px solid #e0e5ed;
  border-radius:4px;padding:8px 12px;margin-bottom:10px;cursor:pointer;user-select:none;}
.re-insbar:hover{border-color:#174DA4;}
.re-insbar svg{width:14px;height:14px;color:#174DA4;flex:0 0 14px;transition:transform .15s;}
.re-insbar.is-open svg{transform:rotate(90deg);}
.re-insbar b{font-size:12px;color:#05275C;}
.re-insbar span{font-size:11px;color:#9098a5;flex:1 1 auto;min-width:0;overflow:hidden;
  text-overflow:ellipsis;white-space:nowrap;}
.re-insbar em{font-style:normal;font-size:10px;font-weight:700;text-transform:uppercase;
  letter-spacing:.4px;color:#9098a5;background:#f5f7fa;border:1px solid #e0e5ed;padding:2px 7px;}
.re-insbar.is-open em{color:#0b5c3a;background:#e6f4ec;border-color:#c3e6cb;}
</style>
</asp:Content>

<asp:Content ID="Content1" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="re-wrap">

    <div class="re-hd">
        <div class="re-hd__l">
            <div class="re-hd__ic">
                <svg width="22" height="22" fill="none" stroke="#fff" stroke-width="1.8" viewBox="0 0 24 24"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            </div>
            <div>
                <div class="re-hd__t">Results Export Centre</div>
                <div class="re-hd__s">Export marks &amp; results statistics for external representation</div>
            </div>
        </div>
        <div class="re-hd__r">
            <button type="button" class="re-srlaunch" id="btnSRLaunch" onclick="openSRModal()" title="Generate per-student marksheet or performance-classification PDF (Published / Approved / Captured / Entered)">
                <span class="re-srlaunch__sheen"></span>
                <svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line></svg>
                Export Summary Report
                <span class="re-srlaunch__tag">PDF</span>
            </button>
            <div class="re-chip" id="reScope">&nbsp;</div>
        </div>
    </div>

    <div class="re-noaccess" id="reNoAccess"></div>
    <div class="re-err" id="reErr"></div>

    <div id="reMain">
    <!-- FILTERS -->
    <div class="re-filter">
        <div class="re-row">
            <div class="re-fld"><label>Source</label><select id="fSource">
                <option value="published">Published results</option>
                <option value="approved">Approved (Dean)</option>
                <option value="captured">Captured (HOD)</option>
                <option value="entered">Entered (Lecturer)</option>
            </select></div>
            <div class="re-fld"><label>Academic Year</label><select id="fYear"></select></div>
            <div class="re-fld"><label>Semester</label><select id="fSem"><option value="">All</option><option value="1">Sem 1</option><option value="2">Sem 2</option><option value="3">Sem 3</option></select></div>
            <div class="re-fld"><label>Study Year</label><select id="fStudyYear"><option value="">All</option><option>1</option><option>2</option><option>3</option><option>4</option><option>5</option></select></div>
            <div class="re-fld"><label>Faculty</label><select id="fFaculty"></select></div>
            <div class="re-fld"><label>Department</label><select id="fDept"></select></div>
            <div class="re-fld"><label>Programme</label><select id="fProg"></select></div>
            <div class="re-fld"><label>Course code</label><input type="text" id="fCourse" placeholder="optional" style="min-width:100px" /></div>
            <div class="re-fld"><label>Retakes</label><select id="fRetakes"><option value="all">Include</option><option value="exclude">Exclude</option><option value="only">Only retakes</option></select></div>
            <div class="re-fld"><label>Grade</label><select id="fGrade"><option value="">All</option><option>A</option><option>B+</option><option>B</option><option>C+</option><option>C</option><option>D+</option><option>D</option><option>F</option></select></div>
            <div class="re-fld"><label>Pass/Fail</label><select id="fPassFail"><option value="all">All</option><option value="pass">Pass only</option><option value="fail">Fail only</option></select></div>
        </div>
        <div class="re-cfg">
            <div><span class="re-seglbl">Report</span>
                <span class="re-seg" id="segMode">
                    <button type="button" data-mode="marks" class="on">Marks Sheet</button>
                    <button type="button" data-mode="summary">Student Summary</button>
                    <button type="button" data-mode="course_stats">Course Stats</button>
                    <button type="button" data-mode="prog_stats">Programme/Faculty Stats</button>
                </span>
            </div>
            <button type="button" class="re-btn re-btn--accent" id="btnApply">Generate Preview</button>
            <button type="button" class="re-btn re-btn--ghost" id="btnReset">Reset</button>
            <span class="re-exp">
                <span class="re-seglbl">Export</span>
                <button type="button" class="re-btn re-btn--primary re-btn--sm" id="btnXlsxTop" disabled="disabled">Excel</button>
                <button type="button" class="re-btn re-btn--primary re-btn--sm" id="btnCsvTop" disabled="disabled">CSV</button>
                <button type="button" class="re-btn re-btn--ghost re-btn--sm" id="btnPrintTop" disabled="disabled">Print</button>
            </span>
        </div>
        <div class="re-srcnote" id="reSrcNote" style="display:none;"></div>
    </div>

    <%-- The analytics below are a dashboard bolted onto an export screen. They cost five
         passes over the marks table, and most visits here are to filter and download, not to
         read charts. So they are opt-in, remembered per user, and fetched once per filter
         change rather than on every one. --%>
    <div class="re-insbar" id="reInsBar" title="Analytics are loaded only while this is open">
        <svg id="reInsChev" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"></polyline></svg>
        <b>Insights</b>
        <span>KPIs, grade distribution, class of degree, performers, submission pipeline</span>
        <em id="reInsState">off</em>
    </div>

    <div id="reInsights" style="display:none;">
    <!-- STATS -->
    <div class="re-stats">
        <div class="re-kpi"><b id="kRec">&ndash;</b><span>Records</span></div>
        <div class="re-kpi"><b id="kStu">&ndash;</b><span>Students</span></div>
        <div class="re-kpi"><b id="kCrs">&ndash;</b><span>Courses</span></div>
        <div class="re-kpi"><b id="kMean">&ndash;</b><span>Mean Score</span></div>
        <div class="re-kpi"><b id="kPass">&ndash;</b><span>Pass Rate</span></div>
        <div class="re-kpi"><b id="kGp">&ndash;</b><span>Mean Grade Pt</span></div>
    </div>

    <div class="re-charts">
        <div class="re-card"><div class="re-card__h">Grade Distribution <small>NCHE bands (score-derived)</small></div><div class="re-card__b"><div class="re-chartbox"><canvas id="chGrade"></canvas></div></div></div>
        <div class="re-card"><div class="re-card__h">Class of Degree <small>indicative · selected scope</small></div><div class="re-card__b"><div class="re-chartbox"><canvas id="chClass"></canvas></div></div></div>
        <div class="re-card"><div class="re-card__h">Marks Submission <small>staged pipeline</small></div><div class="re-card__b"><div class="re-pipe" id="rePipe"><div style="font-size:10px;color:#9098a5;">Generate a preview to see submission progress.</div></div></div></div>
    </div>

    <div class="re-perf">
        <div class="re-card"><div class="re-card__h">Top Performers <small>by GPA · selected scope</small></div><div class="re-card__b"><ul class="re-plist" id="rePerfTop"></ul></div></div>
        <div class="re-card"><div class="re-card__h">Needs Attention <small>lowest GPA · selected scope</small></div><div class="re-card__b"><ul class="re-plist" id="rePerfBot"></ul></div></div>
    </div>

    <div class="re-card" id="reBreakCard" style="margin-top:12px;display:none;">
        <div class="re-card__h">
            <span id="reBreakTitle">Performance breakdown</span>
            <small id="reBreakMeta">&nbsp;</small>
        </div>
        <div class="re-card__b" style="padding:0;">
            <div class="re-tblwrap" style="max-height:320px;"><table class="re-tbl" id="reBreakTable"><thead></thead><tbody></tbody></table></div>
        </div>
    </div>
    </div><%-- /reInsights --%>

    <div class="re-card" style="margin-top:12px;">
        <div class="re-card__h">
            <span id="reTblTitle">Preview</span>
            <span style="display:flex;align-items:center;gap:10px;">
                <small id="reTblMeta">&nbsp;</small>
                <span class="re-colwrap">
                    <button type="button" class="re-colbtn" id="btnCols">Columns &#9662;</button>
                    <div class="re-colpop" id="colPop"></div>
                </span>
            </span>
        </div>
        <div class="re-card__b" style="padding:0;">
            <div class="re-tblwrap"><table class="re-tbl" id="reTable"><thead></thead><tbody></tbody></table></div>
        </div>
    </div>

    <div class="re-actions">
        <span class="re-seglbl">Format</span>
        <button type="button" class="re-btn re-btn--primary" id="btnXlsx">Export Excel</button>
        <button type="button" class="re-btn re-btn--primary" id="btnCsv">Export CSV</button>
        <button type="button" class="re-btn re-btn--ghost" id="btnPrint">Open Print View</button>
        <span class="re-total" id="reTotal">&nbsp;</span>
    </div>
    <div class="re-note">Statistics use derived NCHE grade bands (score-based). Student Summary CGPA &amp; class use the university's authoritative computation. Exports respect your access scope. Excel &amp; CSV return all rows in scope; the on-screen preview shows the first 100.</div>
    </div><!-- /reMain -->

    <input type="hidden" id="hdnConfig" name="hdnConfig" />
    <input type="hidden" id="hdnExportAction" name="hdnExportAction" />
</div>

<div class="md-loader" id="reLoader"><div><div class="md-spinner"></div><div class="md-loader__t">Working&hellip;</div></div></div>

<script type="text/javascript">
(function(){
'use strict';
var opts=null, gradeChart=null, classChart=null, mode='marks';
var hiddenSet={}, lastData=null, lastMode='';
function qs(id){ return document.getElementById(id); }
function setExportDisabled(dis){ ['btnXlsx','btnCsv','btnPrint','btnXlsxTop','btnCsvTop','btnPrintTop'].forEach(function(id){ var b=qs(id); if(b) b.disabled=dis; }); }
function esc(s){ return s==null?'':String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }
function nfmt(v){ return (Number(v)||0).toLocaleString('en-US'); }
function setL(on){ var l=qs('reLoader'); if(l) l.className='md-loader'+(on?' show':''); }
function showErr(m){ var e=qs('reErr'); if(e){ e.textContent=m; e.className='re-err show'; } }
function hideErr(){ var e=qs('reErr'); if(e) e.className='re-err'; }

function ajax(method,params,cb){
    var xhr=new XMLHttpRequest();
    xhr.open('POST','ResultsExporter.aspx/'+method,true);
    xhr.setRequestHeader('Content-Type','application/json; charset=utf-8');
    xhr.timeout=120000;
    xhr.onload=function(){ try{ var o=JSON.parse(xhr.responseText); cb(typeof o.d==='string'?JSON.parse(o.d):o.d); }catch(e){ cb({success:false,message:'Server did not return valid data (it may be warming up — retry).'}); } };
    xhr.onerror=function(){ cb({success:false,message:'Network error.'}); };
    xhr.ontimeout=function(){ cb({success:false,message:'The request took too long — narrow the filters and retry.'}); };
    xhr.send(JSON.stringify(params||{}));
}

function fill(id,items,allLabel){
    var el=qs(id); if(!el)return;
    var h = allLabel!=null ? '<option value="">'+esc(allLabel)+'</option>' : '';
    (items||[]).forEach(function(it){ h+='<option value="'+esc(it.value)+'" data-fac="'+esc(it.faculty||'')+'" data-dep="'+esc(it.department||'')+'">'+esc(it.text)+'</option>'; });
    el.innerHTML=h;
}
function hiddenArr(){ var a=[]; for(var k in hiddenSet){ if(hiddenSet[k]) a.push(Number(k)); } return a; }
function config(){
    var c={ mode:mode, source:qs('fSource').value, acadYear:qs('fYear').value, semester:qs('fSem').value, studyYear:qs('fStudyYear').value,
        faculty:qs('fFaculty').value, department:qs('fDept').value, programme:qs('fProg').value, course:qs('fCourse').value.trim(),
        retakes:qs('fRetakes').value, grade:qs('fGrade').value, passfail:qs('fPassFail').value, hidden:hiddenArr() };
    qs('hdnConfig').value=JSON.stringify(c);
    return c;
}
function cascadeDept(){
    var fac=qs('fFaculty').value, sel=qs('fDept');
    for(var i=0;i<sel.options.length;i++){
        var o=sel.options[i]; if(!o.value){ o.style.display=''; continue; }
        o.style.display=(!fac || o.getAttribute('data-fac')===fac)?'':'none';
    }
    if(sel.selectedOptions && sel.selectedOptions[0] && sel.selectedOptions[0].style.display==='none') sel.value='';
}
function cascadeProg(){
    var fac=qs('fFaculty').value, dep=qs('fDept').value, sel=qs('fProg');
    for(var i=0;i<sel.options.length;i++){
        var o=sel.options[i]; if(!o.value){ o.style.display=''; continue; }
        var okFac=(!fac || o.getAttribute('data-fac')===fac);
        var okDep=(!dep || o.getAttribute('data-dep')===dep);
        o.style.display=(okFac && okDep)?'':'none';
    }
    if(sel.selectedOptions && sel.selectedOptions[0] && sel.selectedOptions[0].style.display==='none') sel.value='';
}

function renderStats(s){
    if(!s) return;
    qs('kRec').textContent=nfmt(s.records);
    qs('kStu').textContent=nfmt(s.students);
    qs('kCrs').textContent=nfmt(s.courses);
    qs('kMean').textContent=(Number(s.meanScore)||0).toFixed(1);
    qs('kPass').textContent=(Number(s.passRate)||0).toFixed(1)+'%';
    qs('kGp').textContent=(Number(s.meanGp)||0).toFixed(2);
    var labels=(s.grades||[]).map(function(x){return x.name;});
    var data=(s.grades||[]).map(function(x){return Number(x.count)||0;});
    var colors=['#16a34a','#22a06b','#174DA4','#2f6fbf','#5b8def','#d97706','#e08e0b','#dc3545'];
    if(gradeChart) gradeChart.destroy();
    gradeChart=new Chart(qs('chGrade').getContext('2d'),{ type:'bar',
        data:{ labels:labels, datasets:[{ data:data, backgroundColor:colors }] },
        options:{ responsive:true, maintainAspectRatio:false, plugins:{legend:{display:false}},
            scales:{ y:{beginAtZero:true,ticks:{font:{size:9}},grid:{color:'#f0f2f6'}}, x:{ticks:{font:{size:10}},grid:{display:false}} } } });

    var clabels=(s.classes||[]).map(function(x){return x.name;});
    var cdata=(s.classes||[]).map(function(x){return Number(x.count)||0;});
    var ctotal=cdata.reduce(function(a,b){return a+b;},0)||1;
    var ccolors=['#16a34a','#174DA4','#2f6fbf','#d97706','#dc3545'];
    if(classChart) classChart.destroy();
    classChart=new Chart(qs('chClass').getContext('2d'),{ type:'doughnut',
        data:{ labels:clabels, datasets:[{ data:cdata, backgroundColor:ccolors }] },
        options:{ responsive:true, maintainAspectRatio:false, plugins:{
            legend:{ position:'right', labels:{ font:{size:10}, boxWidth:12, padding:8,
                // doughnut has no axis, so surface each slice's COUNT right in the legend
                generateLabels:function(chart){
                    var d=chart.data; if(!d.labels.length||!d.datasets.length) return [];
                    var ds=d.datasets[0];
                    return d.labels.map(function(label,i){
                        var v=Number(ds.data[i])||0;
                        return { text:label+'  ('+nfmt(v)+')', fillStyle:ds.backgroundColor[i],
                            strokeStyle:'#fff', lineWidth:1, hidden:!chart.getDataVisibility(i), index:i };
                    });
                }
            }},
            tooltip:{ callbacks:{ label:function(ctx){
                var v=Number(ctx.parsed)||0; return ' '+ctx.label+': '+nfmt(v)+' ('+(v*100/ctotal).toFixed(1)+'%)';
            }}}
        } } });
}
function visible(i){ return !hiddenSet[i]; }
function renderTable(){
    if(!lastData) return;
    var cols=lastData.columns||[], rows=lastData.rows||[];
    var th=qs('reTable').getElementsByTagName('thead')[0];
    var tb=qs('reTable').getElementsByTagName('tbody')[0];
    th.innerHTML='<tr>'+cols.map(function(c,i){return visible(i)?('<th>'+esc(c)+'</th>'):'';}).join('')+'</tr>';
    var vis=cols.filter(function(c,i){return visible(i);}).length;
    if(!rows.length){ tb.innerHTML='<tr><td colspan="'+Math.max(1,vis)+'" style="text-align:center;color:#9098a5;padding:24px;">No results match the selected filters.</td></tr>'; }
    else tb.innerHTML=rows.map(function(r){ return '<tr>'+r.map(function(v,i){return visible(i)?('<td>'+esc(v)+'</td>'):'';}).join('')+'</tr>'; }).join('');
    qs('reTblMeta').textContent = nfmt(lastData.total)+' total · showing '+nfmt(lastData.previewCount);
    qs('reTotal').textContent = nfmt(lastData.total)+' rows in scope';
    var none = !lastData.total || lastData.total===0;
    setExportDisabled(none);
}
function buildCols(cols){
    var pop=qs('colPop'); pop.innerHTML='';
    (cols||[]).forEach(function(c,i){
        var lbl=document.createElement('label');
        var cb=document.createElement('input'); cb.type='checkbox'; cb.checked=visible(i); cb.setAttribute('data-i',i);
        cb.addEventListener('change',function(){ hiddenSet[i]=!cb.checked; renderTable(); });
        lbl.appendChild(cb); lbl.appendChild(document.createTextNode(' '+c));
        pop.appendChild(lbl);
    });
}
function renderSubmission(sub){
    var box=qs('rePipe');
    if(!sub || !sub.total){ box.innerHTML='<div style="font-size:10px;color:#9098a5;">No registration/marks pipeline rows for this scope.</div>'; return; }
    var stages=[['Not Entered',sub.notEntered,'#c3c9d4'],['Entered',sub.entered,'#f0a52b'],['Captured',sub.captured,'#17a2b8'],['Approved',sub.approved,'#2f6fbf'],['Published',sub.published,'#16a34a']];
    var t=Number(sub.total)||1;
    var h='<div class="re-pipe__hd"><span><span class="re-pipe__pct">'+(Number(sub.pctPublished)||0).toFixed(1)+'%</span> <span class="re-pipe__pctl">published</span></span><span style="font-size:9px;color:#888;">'+nfmt(sub.total)+' course regs</span></div>';
    stages.forEach(function(s){
        var w=Math.max(0,Math.round((Number(s[1])||0)*100/t));
        h+='<div class="re-stage"><span class="re-stage__l">'+esc(s[0])+'</span><span class="re-stage__bar"><span style="width:'+w+'%;background:'+s[2]+';"></span></span><span class="re-stage__n">'+nfmt(s[1])+'</span></div>';
    });
    box.innerHTML=h;
}
function perfList(el,arr,hi){
    var ul=qs(el);
    if(!arr||!arr.length){ ul.innerHTML='<li style="color:#9098a5;">No data.</li>'; return; }
    ul.innerHTML=arr.map(function(p,i){
        return '<li><span class="rk">'+(i+1)+'</span><span class="nm">'+esc(p.name||p.regno)+' <small>'+esc(p.regno)+'</small></span><span class="gp '+(hi?'gp--hi':'gp--lo')+'">'+(Number(p.gpa)||0).toFixed(2)+'</span></li>';
    }).join('');
}
var MODE_TITLE={marks:'Marks Sheet (detailed)',summary:'Student Results Summary',course_stats:'Course Performance Statistics',prog_stats:'Programme / Faculty Statistics'};

function renderBreakdown(bd){
    var card=qs('reBreakCard');
    var th=qs('reBreakTable').getElementsByTagName('thead')[0];
    var tb=qs('reBreakTable').getElementsByTagName('tbody')[0];
    if(!bd||!bd.columns||!bd.columns.length){ card.style.display='none'; return; }
    card.style.display='';
    qs('reBreakTitle').textContent=bd.title||'Performance breakdown';
    qs('reBreakMeta').textContent=nfmt((bd.rows||[]).length)+' rows';
    th.innerHTML='<tr>'+bd.columns.map(function(c){return '<th>'+esc(c)+'</th>';}).join('')+'</tr>';
    if(!bd.rows||!bd.rows.length){ tb.innerHTML='<tr><td colspan="'+bd.columns.length+'" style="text-align:center;color:#9098a5;padding:16px;">No data for this selection.</td></tr>'; }
    else tb.innerHTML=bd.rows.map(function(r){ return '<tr>'+r.map(function(v){return '<td>'+esc(v)+'</td>';}).join('')+'</tr>'; }).join('');
}
function srcNote(source,label){
    var el=qs('reSrcNote'); if(!el) return;
    if(source && source!=='published'){
        el.style.display='';
        el.innerHTML='<strong>'+esc(label||source)+'.</strong> These marks are not yet published, so grades, grade points and GPA are computed on the fly from raw scores (NCHE 2015 bands) and are indicative until published.';
    } else { el.style.display='none'; }
}

// ── Preview: the table, and nothing that is not the table ──────────────────
var insCfg=null, insData=null;          // the config the cached analytics belong to
function cfgKey(c){ return JSON.stringify(c); }

function preview(){
    hideErr(); setL(true);
    if(mode!==lastMode){ hiddenSet={}; lastMode=mode; }   // columns differ per mode → reset
    var c=config(), key=cfgKey(c);
    ajax('GetPreview',{ configJson: key }, function(d){
        setL(false);
        if(!d||!d.success){ showErr((d&&d.message)||'Unable to build preview.'); return; }
        qs('reTblTitle').textContent=MODE_TITLE[d.mode]||'Preview';
        srcNote(d.source, d.sourceLabel);
        lastData={ columns:d.columns, rows:d.rows, total:d.total, previewCount:d.previewCount };
        buildCols(d.columns);
        renderTable();
        // The analytics belong to the OLD filter now. Drop them, and refetch only if the
        // panel showing them is actually open.
        if(key!==insCfg){ insCfg=null; insData=null; }
        if(insOpen()) loadInsights();
    });
}

// ── Insights: opt-in, remembered, and fetched once per filter change ──────────
function insOpen(){
    try{ return localStorage.getItem('re_insights')==='1'; }catch(e){ return false; }
}
function insPaint(){
    var open=insOpen();
    qs('reInsights').style.display = open?'block':'none';
    qs('reInsBar').className = 're-insbar'+(open?' is-open':'');
    qs('reInsState').textContent = open?'on':'off';
}
function toggleInsights(){
    try{ localStorage.setItem('re_insights', insOpen()?'0':'1'); }catch(e){}
    insPaint();
    if(insOpen()) loadInsights();
}
function loadInsights(){
    var c=config(), key=cfgKey(c);
    if(key===insCfg && insData){ renderInsights(insData); return; }   // already have these
    qs('reInsState').textContent='loading\u2026';
    ajax('GetInsights',{ configJson: key }, function(d){
        if(!d||!d.success){ qs('reInsState').textContent='unavailable'; return; }
        insCfg=key; insData=d; qs('reInsState').textContent='on';
        renderInsights(d);
    });
}
function renderInsights(d){
    renderStats(d.stats);
    renderSubmission(d.submission);
    renderBreakdown(d.breakdown);
    perfList('rePerfTop', d.stats?d.stats.topPerformers:[], true);
    perfList('rePerfBot', d.stats?d.stats.bottomPerformers:[], false);
}
function doExport(action){
    config();
    qs('hdnExportAction').value=action;
    var f=document.forms[0]; f.submit();
    setTimeout(function(){ qs('hdnExportAction').value=''; },1500);
}
function doPrint(){
    hideErr(); setL(true); var c=config();
    ajax('GetPrintHtml',{ configJson: JSON.stringify(c) }, function(d){
        setL(false);
        if(!d||!d.success){ showErr((d&&d.message)||'Unable to build print view.'); return; }
        var w=window.open('','_blank'); if(!w){ showErr('Pop-up blocked — allow pop-ups to open the print view.'); return; }
        w.document.open(); w.document.write(d.html); w.document.close();
    });
}

function init(){
    setL(true);
    ajax('GetFilterOptions',{},function(o){
        setL(false);
        if(!o||!o.success){ showErr((o&&o.message)||'Unable to load filters.'); return; }
        qs('reScope').textContent=(o.roleNote||'')+(o.scopeLabel?(' · '+o.scopeLabel):'');
        if(!o.hasAccess){ qs('reMain').style.display='none'; var na=qs('reNoAccess'); na.style.display='block'; na.textContent='Your account is not linked to any faculty or department, so no results data is available. Contact the administrator if this is unexpected.'; return; }
        opts=o;
        fill('fYear',o.years,'All years');
        fill('fFaculty',o.faculties,'All faculties');
        fill('fDept',o.departments,'All departments');
        fill('fProg',o.programmes,'All programmes');
        if(o.currentYear) qs('fYear').value=o.currentYear;
        insPaint();
        preview();
    });
}

document.addEventListener('DOMContentLoaded',function(){
    qs('btnApply').addEventListener('click',preview);
    qs('btnCols').addEventListener('click',function(e){ e.stopPropagation(); qs('colPop').classList.toggle('show'); });
    document.addEventListener('click',function(e){ var p=qs('colPop'); if(p&&!p.contains(e.target)&&e.target!==qs('btnCols')) p.classList.remove('show'); });
    qs('btnXlsx').addEventListener('click',function(){ doExport('xlsx'); });
    qs('btnCsv').addEventListener('click',function(){ doExport('csv'); });
    qs('btnPrint').addEventListener('click',doPrint);
    qs('btnXlsxTop').addEventListener('click',function(){ doExport('xlsx'); });
    qs('btnCsvTop').addEventListener('click',function(){ doExport('csv'); });
    qs('btnPrintTop').addEventListener('click',doPrint);
    qs('reInsBar').addEventListener('click',toggleInsights);
    qs('fFaculty').addEventListener('change',function(){ cascadeDept(); cascadeProg(); });
    qs('fDept').addEventListener('change',cascadeProg);
    qs('fSource').addEventListener('change',preview);
    qs('btnReset').addEventListener('click',function(){
        ['fSem','fStudyYear','fFaculty','fDept','fProg','fGrade'].forEach(function(id){ qs(id).value=''; });
        qs('fCourse').value=''; qs('fRetakes').value='all'; qs('fPassFail').value='all'; qs('fSource').value='published';
        if(opts&&opts.currentYear) qs('fYear').value=opts.currentYear;
        cascadeDept(); cascadeProg(); preview();
    });
    var mb=qs('segMode').querySelectorAll('button');
    for(var i=0;i<mb.length;i++){ (function(btn){ btn.addEventListener('click',function(){
        for(var j=0;j<mb.length;j++) mb[j].classList.remove('on'); btn.classList.add('on');
        mode=btn.getAttribute('data-mode'); preview();
    }); })(mb[i]); }
    init();
});
})();
</script>

<!-- ========== EXPORT SUMMARY REPORT MODAL ========== -->
<div class="sr-ov" id="srModal">
    <div class="sr-modal" role="dialog" aria-modal="true" aria-label="Export Summary Report">
        <div class="sr-hd">
            <div class="sr-hd__t">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line></svg>
                Export Summary Report
            </div>
            <button type="button" class="sr-hd__x" onclick="closeSRModal()" aria-label="Close">&times;</button>
        </div>
        <div class="sr-bd">
            <div class="sr-fg">
                <label>Results Source</label>
                <select id="srSource" class="sr-sel">
                    <option value="all" selected="selected">All results — published + fully-marked (widest coverage)</option>
                    <option value="published">Published (final results only)</option>
                    <option value="approved">Approved (Dean) — pending Senate approval</option>
                    <option value="captured">Captured (HOD) — pending approval</option>
                    <option value="entered">Entered (Lecturer) — pending capture</option>
                </select>
            </div>

            <div class="sr-fg" style="margin-bottom:6px;">
                <label>Programme <span class="sr-req">*</span></label>
                <div class="sr-combo" id="srProgCombo">
                    <input type="text" id="srProgInput" class="sr-in" placeholder="Type to search programme&hellip;" autocomplete="off" spellcheck="false" />
                    <input type="hidden" id="srProg" value="" />
                    <div class="sr-list" id="srProgList"></div>
                </div>
            </div>
            <div class="sr-status" id="srStatus">Pick a programme &mdash; the fields below fill in automatically (you can still change any of them). Counts show where marks exist.</div>

            <div class="sr-fg">
                <label>Academic Year <span class="sr-req">*</span></label>
                <select id="srAcadYear" class="sr-sel" disabled><option value="">Select a programme first&hellip;</option></select>
                <span class="sr-hint">The sitting. Marks are scoped to this academic year, so students who sat this semester in a different year are not mixed in.</span>
            </div>

            <div class="sr-fg">
                <label>Year of Study <span class="sr-req">*</span></label>
                <select id="srStudyYear" class="sr-sel" disabled><option value="">Select academic year first&hellip;</option></select>
            </div>

            <div class="sr-fg">
                <label>Semester <span class="sr-req">*</span></label>
                <select id="srSem" class="sr-sel" disabled><option value="">Select year of study first&hellip;</option></select>
            </div>

            <div class="sr-fg">
                <label>Entry Year <span class="sr-hint" style="font-weight:400;">(optional — narrows to one intake)</span></label>
                <select id="srYear" class="sr-sel" disabled><option value="">All intakes</option></select>
            </div>

            <div class="sr-fg">
                <label>Specialisation</label>
                <div class="sr-ask">
                    <label id="srSpecNoL" class="on"><input type="radio" name="srSpecMode" id="srSpecNo" value="no" checked="checked" /><span>No &mdash; all specialisations</span></label>
                    <label id="srSpecYesL"><input type="radio" name="srSpecMode" id="srSpecYes" value="yes" /><span>Yes &mdash; limit to one</span></label>
                </div>
                <div class="sr-spec" id="srSpecWrap">
                    <select id="srSpec" class="sr-sel"><option value="">-- Select Specialisation --</option></select>
                </div>
                <span class="sr-hint" id="srSpecHint">Only this programme&rsquo;s specialisations are offered, each with the number of students holding it.</span>
            </div>

            <div class="sr-fg" style="margin-bottom:8px;">
                <label class="sr-chk"><input type="checkbox" id="srExcludePromoted" /> Only students still at this year/semester</label>
                <span class="sr-hint">Off (default): include everyone who sat this sitting, even if they have since moved on — their completed marks are never dropped. On: keep only students whose latest registration is not beyond this semester.</span>
            </div>

            <div class="sr-fg">
                <label>Minimum courses to pass</label>
                <input type="number" id="srMinCourses" class="sr-in" min="1" max="30" step="1" value="5" />
                <span class="sr-hint">A student who sits fewer than this many courses is marked FAIL. Leave blank to use 4.</span>
            </div>

            <hr class="sr-div" />

            <div class="sr-fg" style="margin-bottom:0;">
                <label>Or specific students by registration number</label>
                <textarea id="srReg" class="sr-ta" placeholder="Comma-separated reg numbers, e.g. MRU2024001230, MRU2024001231"></textarea>
                <span class="sr-hint">If provided, Programme &amp; Entry Year are still required.</span>
            </div>

            <div class="sr-fg" style="margin:14px 0 0;">
                <button type="button" class="sr-btn sr-btn--nav" onclick="srPreview()">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                    Preview Students
                </button>
            </div>

            <div class="sr-prev" id="srPrev">
                <div class="sr-prev__n" id="srPrevN">0</div>
                <div class="sr-prev__l" id="srPrevL">students will be included in the report</div>
            </div>
        </div>
        <div class="sr-ft">
            <button type="button" class="sr-btn sr-btn--ghost" onclick="closeSRModal()">Cancel</button>
            <span class="sr-ft__sp"></span>
            <button type="button" class="sr-btn sr-btn--primary" id="srBtnMarks" onclick="srExport('ExportSummaryReport',this)" disabled>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
                Export Results
            </button>
            <button type="button" class="sr-btn sr-btn--ok" id="srBtnPerf" onclick="srExport('ExportPerformanceReport',this)" disabled>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line></svg>
                Summary Report
            </button>
        </div>
    </div>
</div>

<script type="text/javascript">
/* Export Summary Report — reuses NewStudentInfo.aspx's proven, source-aware handlers
   (Preview / ExportSummaryReport / ExportPerformanceReport) so the output is identical
   to the NewStudentInfo feature. Self-contained; exposes only the 4 window.* entry points. */
(function(){
    var BACKEND='NewStudentInfo.aspx';
    var srProgs=[], srCombos=[], srAllYears=[], srSpecs=[];   // srCombos: [{y,sy,s,n}] terms WITH data; srAllYears: all entry years for the programme; srSpecs: [{id,name,abbrev,n}] for the chosen programme
    function d(id){ return document.getElementById(id); }
    function e2(s){ return s==null?'':String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
    function stu(n){ return n+' student'+(n==1?'':'s'); }
    function status(html,cls){ var el=d('srStatus'); if(el){ el.className='sr-status'+(cls?' '+cls:''); el.innerHTML=html||''; } }

    function loadProgs(){
        srProgs=[]; var sel=d('fProg');
        if(sel){ for(var i=0;i<sel.options.length;i++){ var o=sel.options[i];
            var v=(o.value||'').trim();
            if(!v || v==='-' || v.toUpperCase()==='ALL') continue;   // skip the blank/placeholder programme rows
            srProgs.push({value:v,text:o.text}); } }
    }
    function renderProg(){
        var q=(d('srProgInput').value||'').toLowerCase().trim(), list=d('srProgList'), h='';
        var items=srProgs.filter(function(p){ return !q||p.text.toLowerCase().indexOf(q)>-1||String(p.value).toLowerCase().indexOf(q)>-1; });
        if(!items.length) h='<div class="sr-none">No programmes found</div>';
        else items.slice(0,400).forEach(function(p){ h+='<div class="sr-opt" data-v="'+e2(p.value)+'" data-t="'+e2(p.text)+'">'+e2(p.text)+' <small>'+e2(p.value)+'</small></div>'; });
        list.innerHTML=h;
        var o=list.querySelectorAll('.sr-opt');
        for(var i=0;i<o.length;i++) o[i].onmousedown=function(ev){ ev.preventDefault();
            d('srProg').value=this.getAttribute('data-v')||''; d('srProgInput').value=this.getAttribute('data-t')||'';
            d('srProgCombo').classList.remove('open'); fetchCascade(); };
    }

    // ── dependent-select helpers ──
    function setSel(id,opts,placeholder,enabled){
        var el=d(id); if(!el) return;
        var h='<option value="">'+e2(placeholder)+'</option>';
        opts.forEach(function(o){ h+='<option value="'+e2(o.v)+'">'+e2(o.t)+'</option>'; });
        el.innerHTML=h; el.value=''; el.disabled=!enabled;
    }
    // counts from the data-bearing combos (used to annotate + auto-fill; NOT to limit options).
    // The sitting is now anchored on ACADEMIC YEAR (ay); entry year (y) is an optional narrowing.
    function eyMatch(c){ var y=d('srYear').value; return !y || c.y===y; }   // honour the optional entry-year filter
    function cntAY(ay){ var t=0; srCombos.forEach(function(c){ if(c.ay===ay&&eyMatch(c)) t+=c.n; }); return t; }
    function cntStudy(ay,sy){ var t=0; srCombos.forEach(function(c){ if(c.ay===ay&&c.sy===sy&&eyMatch(c)) t+=c.n; }); return t; }
    function cntSem(ay,sy,s){ var t=0; srCombos.forEach(function(c){ if(c.ay===ay&&c.sy===sy&&c.s===s&&eyMatch(c)) t+=c.n; }); return t; }
    function bestFor(pred){ var b=null; srCombos.forEach(function(c){ if(pred(c) && (!b||c.n>b.n)) b=c; }); return b; }
    var STUDY_YEARS=['1','2','3','4','5'], SEMS=['1','2','3'];
    function acadYearOpts(){
        var list=[]; srCombos.forEach(function(c){ if(c.ay && list.indexOf(c.ay)<0) list.push(c.ay); });
        list.sort(function(a,b){ return b.localeCompare(a); });   // newest first
        return list.map(function(ay){ var n=cntAY(ay); return {v:ay, t:ay+(n>0?('  ·  '+stu(n)):'')}; });
    }
    function entryYearOpts(){
        var list=srAllYears.slice();
        srCombos.forEach(function(c){ if(c.y && list.indexOf(c.y)<0) list.push(c.y); });
        list.sort(function(a,b){ return b.localeCompare(a); });
        return list.map(function(y){ return {v:y, t:'Entry '+y}; });
    }

    function clearPreview(){ d('srPrev').classList.remove('show'); d('srPrevN').textContent='0'; d('srBtnMarks').disabled=true; d('srBtnPerf').disabled=true; }

    /* ── Specialisation (optional narrowing) ──────────────────────────────────────────────
       The list is rebuilt from the cascade every time the programme changes, and the answer
       is forced back to "No" at the same moment. That reset is the important part: a spec_id
       belongs to exactly one programme, so a stale selection carried across a programme
       change would silently export nobody. */
    function specAsking(){ var y=d('srSpecYes'); return !!(y && y.checked); }
    function specValue(){ return specAsking() ? ((d('srSpec')||{}).value||'') : ''; }
    function paintSpecMode(){
        var yes=specAsking(), a=d('srSpecNoL'), b=d('srSpecYesL'), w=d('srSpecWrap');
        if(a) a.classList.toggle('on', !yes);
        if(b) b.classList.toggle('on', yes);
        if(w) w.classList.toggle('show', yes);
    }
    function specReset(){
        var no=d('srSpecNo'); if(no) no.checked=true;
        var sel=d('srSpec'); if(sel) sel.value='';
        paintSpecMode();
    }
    function populateSpecs(){
        var sel=d('srSpec'), yesL=d('srSpecYesL'), yes=d('srSpecYes'), hint=d('srSpecHint');
        specReset();
        if(!sel) return;
        var h='<option value="">-- Select Specialisation --</option>';
        for(var i=0;i<srSpecs.length;i++){
            var sp=srSpecs[i], lbl=sp.name+(sp.abbrev?(' ('+sp.abbrev+')'):'')+'  ·  '+stu(sp.n);
            h+='<option value="'+e2(sp.id)+'">'+e2(lbl)+'</option>';
        }
        sel.innerHTML=h;
        // A programme with no specialisations on record cannot be narrowed — say so and lock
        // the Yes option rather than offering an empty dropdown.
        var none=(srSpecs.length===0);
        if(yes) yes.disabled=none;
        if(yesL) yesL.classList.toggle('off', none);
        if(hint) hint.innerHTML = none
            ? 'This programme has no specialisations on record &mdash; every student is reported together.'
            : 'Only this programme&rsquo;s specialisations are offered, each with the number of students holding it.';
    }
    var SRC_LBL={all:'all-results',published:'published',approved:'approved',captured:'captured',entered:'entered'};

    function fetchCascade(){
        var prog=d('srProg').value;
        srCombos=[]; srAllYears=[]; srSpecs=[];
        setSel('srAcadYear',[], 'Select a programme first…', false);
        setSel('srStudyYear',[], 'Select academic year first…', false);
        setSel('srSem',[], 'Select year of study first…', false);
        setSel('srYear',[], 'All intakes', false);
        populateSpecs();
        clearPreview();
        if(!prog){ status('Pick a programme &mdash; the fields below fill in automatically.'); return; }
        status('<span class="sr-spin"></span> Loading&hellip;','sr-status--load');
        var src=d('srSource').value||'all';
        var xhr=new XMLHttpRequest();
        xhr.open('GET', BACKEND+'?action=SummaryReportCascade&source='+encodeURIComponent(src)+'&programme='+encodeURIComponent(prog), true);
        xhr.onreadystatechange=function(){
            if(xhr.readyState!==4) return;
            var r=null; try{ r=JSON.parse(xhr.responseText); }catch(ex){}
            if(!r){ status('Could not load. Please retry.','sr-status--warn'); return; }
            srCombos=r.combos||[]; srAllYears=r.years||[]; srSpecs=r.specs||[];
            populateSpecs();
            populateAcadYears();
        };
        xhr.onerror=function(){ status('Network error. Please retry.','sr-status--warn'); };
        xhr.send();
    }
    // Anchor on academic year: populate Academic Year (the sittings that have data), Year of Study 1–5,
    // Semester 1–3, and the optional Entry Year (all intakes); annotate with counts and auto-select the
    // best-populated sitting. The user can freely change any field afterwards.
    function populateAcadYears(){
        setSel('srAcadYear', acadYearOpts(), '-- Select Academic Year --', true);
        setSel('srStudyYear', STUDY_YEARS.map(function(sy){ return {v:sy,t:'Year '+sy}; }), '-- Select Year of Study --', true);
        setSel('srSem', SEMS.map(function(s){ return {v:s,t:'Semester '+s}; }), '-- Select Semester --', true);
        setSel('srYear', entryYearOpts(), 'All intakes', true);
        clearPreview();
        var ays=acadYearOpts();   // newest-first
        if(ays.length){ d('srAcadYear').value=ays[0].v; onAcadYear();   // default to the most recent sitting
               var src=d('srSource').value||'all';
               status('Auto-filled to the latest sitting ('+e2(ays[0].v)+') with '+e2(SRC_LBL[src]||src)+' marks — change any field freely.','sr-status--ok'); }
        else { var src2=d('srSource').value||'all';
               status('No <b>'+e2(SRC_LBL[src2]||src2)+'</b> marks for this programme yet — pick any sitting (may return 0) or change the Results Source.','sr-status--warn'); }
    }
    function onAcadYear(){
        var ay=d('srAcadYear').value;
        setSel('srStudyYear', STUDY_YEARS.map(function(sy){ var n=cntStudy(ay,sy); return {v:sy,t:'Year '+sy+(n>0?('  ·  '+stu(n)):'')}; }), '-- Select Year of Study --', true);
        setSel('srSem', SEMS.map(function(s){ return {v:s,t:'Semester '+s}; }), '-- Select Semester --', true);
        clearPreview();
        if(!ay) return;
        var b=bestFor(function(c){ return c.ay===ay&&eyMatch(c); });
        if(b){ d('srStudyYear').value=b.sy; onStudy(); }
    }
    function onStudy(){
        var ay=d('srAcadYear').value, sy=d('srStudyYear').value;
        setSel('srSem', SEMS.map(function(s){ var n=cntSem(ay,sy,s); return {v:s,t:'Semester '+s+(n>0?('  ·  '+stu(n)):'')}; }), '-- Select Semester --', true);
        clearPreview();
        if(!sy) return;
        var b=bestFor(function(c){ return c.ay===ay&&c.sy===sy&&eyMatch(c); });
        if(b){ d('srSem').value=b.s; maybeAutoPreview(); }
    }
    function onEntryYear(){
        // Entry year is an optional narrowing — re-annotate the sitting fields with its counts.
        onAcadYear();
    }
    function maybeAutoPreview(){
        if(d('srProg').value && d('srAcadYear').value && d('srStudyYear').value && d('srSem').value) window.srPreview(true);
        else clearPreview();
    }

    function reset(){
        d('srSource').value='all';
        d('srProg').value=''; d('srProgInput').value='';
        srCombos=[]; srAllYears=[]; srSpecs=[];
        setSel('srAcadYear',[], 'Select a programme first…', false);
        setSel('srStudyYear',[], 'Select academic year first…', false);
        setSel('srSem',[], 'Select year of study first…', false);
        setSel('srYear',[], 'All intakes', false);
        populateSpecs();
        var xp=d('srExcludePromoted'); if(xp) xp.checked=false;
        d('srReg').value=''; var mc=d('srMinCourses'); if(mc) mc.value='5';
        clearPreview();
        status('Pick a programme &mdash; the fields below fill in automatically (you can still change any of them).');
    }
    function collect(silent){
        var p=d('srProg').value, ay=d('srAcadYear').value, sy=d('srStudyYear').value, sm=d('srSem').value;
        if(!p){ if(!silent){ alert('Please select a Programme.'); d('srProgInput').focus(); } return null; }
        if(!ay){ if(!silent) alert('Please select an Academic Year.'); return null; }
        if(!sy){ if(!silent) alert('Please select a Year of Study.'); return null; }
        if(!sm){ if(!silent) alert('Please select a Semester.'); return null; }
        // Answering "Yes" without choosing one is the only way this field can go wrong, and it
        // must not fall through as "all" — that would hand back a report the user did not ask for.
        if(specAsking() && !specValue()){
            if(!silent){ alert('Please choose a Specialisation, or answer "No — all specialisations".'); d('srSpec').focus(); }
            return null;
        }
        return { programme:p, acadYear:ay, entryYear:(d('srYear').value||''), studyYear:sy, semester:sm,
                 source:d('srSource').value||'all', entryNumbers:(d('srReg').value||'').trim(),
                 minCourses:((d('srMinCourses')||{}).value||''),
                 specialisation:specValue(),
                 excludePromoted:(d('srExcludePromoted')&&d('srExcludePromoted').checked)?'1':'' };
    }
    function query(action,c){
        var q='?action='+action+'&programme='+encodeURIComponent(c.programme)+'&acadYear='+encodeURIComponent(c.acadYear||'')
            +'&entryYear='+encodeURIComponent(c.entryYear||'')
            +'&studyYear='+encodeURIComponent(c.studyYear)+'&semester='+encodeURIComponent(c.semester)+'&source='+encodeURIComponent(c.source)
            +'&minCourses='+encodeURIComponent(c.minCourses||'');
        if(c.excludePromoted) q+='&excludePromoted=1';
        if(c.specialisation) q+='&specialisation='+encodeURIComponent(c.specialisation);
        if(c.entryNumbers) q+='&entryNumbers='+encodeURIComponent(c.entryNumbers);
        return q;
    }

    window.openSRModal=function(){ loadProgs(); reset(); d('srModal').classList.add('show'); setTimeout(function(){ var pi=d('srProgInput'); if(pi) pi.focus(); },60); };
    window.closeSRModal=function(){ d('srModal').classList.remove('show'); };

    window.srPreview=function(auto){
        var c=collect(auto===true); if(!c) return;
        d('srPrevN').textContent='…'; d('srPrev').classList.add('show');
        d('srBtnMarks').disabled=true; d('srBtnPerf').disabled=true;
        var xhr=new XMLHttpRequest();
        xhr.open('GET', BACKEND+query('PreviewSummaryReport',c), true);
        xhr.onreadystatechange=function(){
            if(xhr.readyState!==4) return;
            if(xhr.status===200){
                var r=null; try{ r=JSON.parse(xhr.responseText); }catch(ex){ r=null; }
                if(!r){ d('srPrevN').textContent='0'; if(auto!==true) alert('Unexpected server response during preview.'); return; }
                var n=Number(r.count)||0;
                d('srPrevN').textContent=n.toLocaleString('en-US');
                d('srPrevL').textContent=(n===1?'student will be included in the report':'students will be included in the report');
                d('srBtnMarks').disabled=(n===0); d('srBtnPerf').disabled=(n===0);
                if(r.error && auto!==true) alert('Error: '+r.error);
            } else { d('srPrevN').textContent='0'; if(auto!==true) alert('Preview failed (HTTP '+xhr.status+'). Please try again.'); }
        };
        xhr.onerror=function(){ d('srPrevN').textContent='0'; if(auto!==true) alert('Network error during preview.'); };
        xhr.send();
    };
    window.srExport=function(action,btn){
        var c=collect(false); if(!c) return;
        var n=parseInt((d('srPrevN').textContent||'0').replace(/[^0-9]/g,''),10)||0;
        if(n<=0){ alert('No students match — adjust the filters (the preview count must be greater than 0).'); return; }
        var orig=btn.innerHTML; btn.disabled=true; btn.innerHTML='Generating&hellip;';
        window.open(BACKEND+query(action,c), '_blank');
        setTimeout(function(){ btn.innerHTML=orig; btn.disabled=false; }, 2200);
    };

    function wire(){
        var pi=d('srProgInput');
        if(pi){ pi.addEventListener('focus',function(){ renderProg(); d('srProgCombo').classList.add('open'); });
                pi.addEventListener('input',function(){ d('srProg').value=''; renderProg(); d('srProgCombo').classList.add('open'); }); }
        var src=d('srSource'); if(src) src.addEventListener('change',function(){ if(d('srProg').value) fetchCascade(); });
        var ay=d('srAcadYear'); if(ay) ay.addEventListener('change',onAcadYear);
        var y=d('srYear'); if(y) y.addEventListener('change',onEntryYear);
        var sy=d('srStudyYear'); if(sy) sy.addEventListener('change',onStudy);
        var sm=d('srSem'); if(sm) sm.addEventListener('change',maybeAutoPreview);
        var xp=d('srExcludePromoted'); if(xp) xp.addEventListener('change',function(){ if(d('srProg').value&&d('srAcadYear').value&&d('srStudyYear').value&&d('srSem').value) window.srPreview(true); });
        // Answering the specialisation question re-counts, so the preview figure on screen always
        // describes the export that the buttons would actually produce.
        var sNo=d('srSpecNo'), sYes=d('srSpecYes'), sSel=d('srSpec');
        function onSpecChange(){ paintSpecMode(); if(specAsking() && !specValue()) { clearPreview(); return; } maybeAutoPreview(); }
        if(sNo)  sNo.addEventListener('change', onSpecChange);
        if(sYes) sYes.addEventListener('change', onSpecChange);
        if(sSel) sSel.addEventListener('change', onSpecChange);
        document.addEventListener('click',function(ev){ var c=d('srProgCombo'); if(c && !c.contains(ev.target)) c.classList.remove('open'); });
        document.addEventListener('keydown',function(ev){ if(ev.key==='Escape' && d('srModal').classList.contains('show')) window.closeSRModal(); });
        var ov=d('srModal'); if(ov) ov.addEventListener('click',function(ev){ if(ev.target===ov) window.closeSRModal(); });
    }
    if(document.readyState!=='loading') wire(); else document.addEventListener('DOMContentLoaded',wire);
})();
</script>
</asp:Content>
