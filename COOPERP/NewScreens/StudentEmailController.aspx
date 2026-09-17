<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true"
    CodeFile="StudentEmailController.aspx.cs" Inherits="COOPERP_NewScreens_StudentEmailController"
    Title="Student Email Controller" %>

<asp:Content ID="ch" ContentPlaceHolderID="HeadContent" runat="server">
<style>
.se-wrap{max-width:1280px;margin:0 auto;padding:18px 20px 40px;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;color:#1a1a2e;}
.se-head{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:12px;margin-bottom:14px;}
.se-title{font-size:21px;font-weight:800;color:#05275C;margin:0;}
.se-sub{font-size:12px;color:#64748b;margin-top:2px;}
.se-gen{display:inline-flex;align-items:center;gap:7px;background:#05275C;color:#fff;border:0;padding:11px 16px;font-size:13px;font-weight:700;cursor:pointer;}
.se-gen:hover{background:#0a3a82;}
.se-gen:disabled{opacity:.6;cursor:default;}
.se-kpis{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px;margin-bottom:16px;}
.se-kpi{background:#fff;border:1px solid #e0e5ed;padding:12px 14px;border-left:3px solid #174DA4;}
.se-kpi__v{font-size:22px;font-weight:800;color:#05275C;line-height:1;}
.se-kpi__l{font-size:10.5px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:#94a3b8;margin-top:5px;}
.se-kpi--ok{border-left-color:#16a34a;}.se-kpi--warn{border-left-color:#ea580c;}.se-kpi--info{border-left-color:#0891b2;}
.se-tabs{display:flex;gap:4px;border-bottom:1px solid #e0e5ed;margin-bottom:12px;}
.se-tab{padding:9px 15px;font-size:13px;font-weight:700;color:#64748b;cursor:pointer;border-bottom:2px solid transparent;}
.se-tab.active{color:#05275C;border-bottom-color:#05275C;}
.se-toolbar{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:10px;}
.se-in,.se-sel{padding:9px 11px;border:1px solid #cbd5e1;font-size:13px;background:#fff;color:#1a1a2e;}
.se-in{flex:1 1 220px;min-width:0;}
.se-btn{padding:9px 13px;border:1px solid #cbd5e1;background:#fff;color:#334155;font-size:13px;font-weight:600;cursor:pointer;}
.se-btn--p{background:#05275C;border-color:#05275C;color:#fff;}
.se-meta{font-size:12px;color:#64748b;margin:4px 0 8px;}
.se-tblwrap{overflow-x:auto;border:1px solid #e0e5ed;background:#fff;}
.se-tbl{width:100%;min-width:900px;border-collapse:collapse;font-size:12px;}
.se-tbl th{background:#f9fafc;text-align:left;padding:9px 11px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;color:#64748b;border-bottom:2px solid #e0e5ed;white-space:nowrap;}
.se-tbl td{padding:9px 11px;border-bottom:1px solid #f0f3f7;vertical-align:middle;}
.se-tbl tbody tr:hover td{background:#f9fbff;}
.se-badge{display:inline-block;font-size:10px;font-weight:700;padding:2px 8px;white-space:nowrap;border:1px solid transparent;}
.se-b--pending{background:#fff7ed;color:#9a3412;border-color:#fed7aa;}
.se-b--ready{background:#eef2ff;color:#3730a3;border-color:#c7d2fe;}
.se-b--learn{background:#f5f3ff;color:#6d28d9;border-color:#ddd6fe;}
.se-b--done{background:#e6f4ec;color:#0b5c3a;border-color:#b5dcc5;}
.se-b--verified{background:#ecfeff;color:#155e75;border-color:#a5f3fc;}
.se-b--muted{background:#f1f5f9;color:#475569;border-color:#cbd5e1;}
.se-act{color:#174DA4;font-weight:700;cursor:pointer;font-size:11.5px;}
/* Pager entries are real <a href> links (see renderPager) so they can be opened in a new
   tab or copied; only the disabled ends are inert <button>s. */
.se-pager{display:flex;flex-wrap:wrap;gap:4px;justify-content:center;align-items:center;margin-top:14px;}
.se-pager a,.se-pager button{min-width:32px;padding:6px 9px;border:1px solid #cbd5e1;background:#fff;font-size:12px;cursor:pointer;text-align:center;color:#334155;text-decoration:none;line-height:1.3;font-family:inherit;}
.se-pager a:hover{border-color:#174DA4;color:#174DA4;}
.se-pager a.active{background:#05275C;border-color:#05275C;color:#fff;font-weight:700;}
.se-pager button:disabled{opacity:.45;cursor:default;}
.se-ov{display:none;position:fixed;inset:0;background:rgba(5,39,92,.55);z-index:1000;}
.se-modal{display:none;position:fixed;z-index:1001;top:50%;left:50%;transform:translate(-50%,-50%);width:92%;max-width:460px;background:#fff;box-shadow:0 24px 70px rgba(0,0,0,.35);}
.se-modal__h{display:flex;justify-content:space-between;align-items:center;padding:15px 18px;border-bottom:1px solid #eef2f7;}
.se-modal__t{font-size:16px;font-weight:800;color:#05275C;}
.se-modal__x{border:0;background:none;font-size:24px;color:#94a3b8;cursor:pointer;}
.se-modal__b{padding:16px 18px;}
.se-fl{display:block;font-size:12px;font-weight:700;color:#374151;margin:10px 0 4px;}
.se-fi{width:100%;box-sizing:border-box;padding:10px;border:1px solid #cbd5e1;font-size:14px;}
.se-modal__f{padding:12px 18px;border-top:1px solid #eef2f7;display:flex;justify-content:flex-end;gap:8px;}
.se-msg{font-size:12.5px;padding:9px 11px;margin-bottom:10px;display:none;}
.se-msg--ok{background:#e6f4ec;color:#0b5c3a;border:1px solid #b5dcc5;}
.se-msg--err{background:#fef2f2;color:#b91c1c;border:1px solid #fecaca;}
.se-empty{text-align:center;padding:40px;color:#94a3b8;font-size:13px;}
/* Per-campus cards. Clickable: they set the campus filter and re-run the search. */
.se-camps{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:10px;margin-bottom:16px;}
.se-camp{background:#fff;border:1px solid #e0e5ed;border-top:3px solid #05275C;padding:12px 14px;cursor:pointer;transition:border-color .15s,box-shadow .15s;}
.se-camp:hover{border-color:#174DA4;box-shadow:0 2px 10px rgba(5,39,92,.09);}
.se-camp.active{border-color:#174DA4;background:#f7faff;box-shadow:inset 0 0 0 1px #174DA4;}
.se-camp__h{display:flex;align-items:baseline;justify-content:space-between;gap:8px;margin-bottom:9px;}
.se-camp__n{font-size:13.5px;font-weight:800;color:#05275C;}
.se-camp__t{font-size:19px;font-weight:800;color:#05275C;line-height:1;font-variant-numeric:tabular-nums;}
.se-camp__g{display:grid;grid-template-columns:repeat(4,1fr);gap:6px;}
.se-camp__s{text-align:center;padding:5px 2px;background:#f9fafc;border:1px solid #eef2f7;}
.se-camp__sv{font-size:14px;font-weight:800;color:#334155;font-variant-numeric:tabular-nums;}
.se-camp__sl{font-size:8.5px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;color:#94a3b8;margin-top:2px;}
.se-camp__bar{height:4px;background:#eef2f7;margin-top:9px;overflow:hidden;}
.se-camp__fill{height:100%;background:#16a34a;width:0;transition:width .4s ease;}
.se-camp__pct{font-size:10px;color:#64748b;margin-top:4px;font-weight:600;}
/* Email suggestions in the create modal */
.se-sug{background:#f7faff;border:1px solid #dbe6f5;padding:10px 12px;margin-bottom:12px;}
.se-sug__h{font-size:10px;font-weight:800;text-transform:uppercase;letter-spacing:.4px;color:#64748b;margin-bottom:7px;}
.se-sug__r{display:flex;align-items:center;gap:7px;margin-bottom:6px;}
.se-sug__r:last-child{margin-bottom:0;}
.se-sug__v{flex:1 1 auto;min-width:0;font-size:13px;font-weight:700;color:#05275C;font-family:Consolas,"Courier New",monospace;word-break:break-all;cursor:pointer;}
.se-sug__v:hover{text-decoration:underline;}
.se-cp{flex:0 0 auto;display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border:1px solid #cbd5e1;background:#fff;color:#475569;cursor:pointer;padding:0;}
.se-cp:hover{border-color:#174DA4;color:#174DA4;}
.se-cp.ok{border-color:#16a34a;color:#16a34a;}
.se-use{flex:0 0 auto;border:1px solid #cbd5e1;background:#fff;color:#174DA4;font-size:10.5px;font-weight:700;padding:5px 8px;cursor:pointer;}
.se-use:hover{border-color:#174DA4;background:#174DA4;color:#fff;}
.se-fi--row{display:flex;gap:7px;align-items:center;}
.se-fi--row .se-fi{flex:1 1 auto;min-width:0;}
@media(max-width:640px){.se-wrap{padding:12px;}.se-camps{grid-template-columns:1fr;}}
</style>
</asp:Content>

<asp:Content ID="cm" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="se-wrap">
    <div class="se-head">
        <div>
            <h1 class="se-title">Student Email Controller</h1>
            <div class="se-sub">Automated university-email lifecycle for the 2026 intake and beyond — no ICT-office visit required.</div>
        </div>
        <button type="button" class="se-gen" id="btnGen" onclick="genEligible()">
            <svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14"/></svg>
            Generate Eligible Students
        </button>
    </div>

    <div class="se-kpis" id="kpis"></div>

    <!-- Per-campus breakdown. Clicking a card filters the pipeline to that campus, so the
         numbers are a way in rather than just a readout. -->
    <div class="se-camps" id="camps"></div>

    <div class="se-msg" id="topMsg"></div>

    <div class="se-tabs">
        <div class="se-tab active" id="tabPipe" onclick="setTab('pipe')">Pipeline</div>
        <div class="se-tab" id="tabCand" onclick="setTab('cand')">Candidates</div>
        <div class="se-tab" id="tabComp" onclick="setTab('comp')">Complaints</div>
        <div class="se-tab" id="tabBatch" onclick="setTab('batch')">Batch &amp; Google</div>
    </div>

    <!-- Pipeline -->
    <div id="paneP">
        <div class="se-toolbar">
            <input type="text" id="fq" class="se-in" placeholder="Search name, student number, reg no or email &mdash; any order&hellip;" />
            <select id="fStage" class="se-sel">
                <option value="">All stages</option>
                <option value="PENDING_CREATION">Pending creation</option>
                <option value="READY_FOR_COLLECTION">Ready for collection</option>
                <option value="EMAIL_CREATED">Email created</option>
                <option value="COMPLETED">Completed</option>
            </select>
            <select id="fVerif" class="se-sel">
                <option value="">Any verification</option>
                <option value="VERIFIED">Verified</option>
                <option value="UNVERIFIED">Unverified</option>
            </select>
            <select id="fCampus" class="se-sel"><option value="">All campuses</option></select>
            <select id="fProg" class="se-sel"><option value="">All programmes</option></select>
            <select id="fPs" class="se-sel" title="Rows per page">
                <option value="25">25 / page</option>
                <option value="50">50 / page</option>
                <option value="100">100 / page</option>
                <option value="200">200 / page</option>
            </select>
            <button type="button" class="se-btn se-btn--p" onclick="doSearch(1)">Search</button>
            <button type="button" class="se-btn" onclick="resetF()">Reset</button>
        </div>
        <div class="se-meta" id="pMeta">Loading&hellip;</div>
        <div class="se-tblwrap">
            <table class="se-tbl"><thead><tr>
                <th>Student</th><th>Student No.</th><th>Programme</th><th>Campus</th><th>Year</th><th>Paid</th><th>Email</th><th>Stage</th><th>Verify</th><th>Updated</th><th></th>
            </tr></thead><tbody id="pBody"></tbody></table>
        </div>
        <div class="se-empty" id="pEmpty" style="display:none;">No students match these filters.</div>
        <div class="se-pager" id="pPager"></div>
    </div>

    <!-- Candidates: 2026+ students NOT yet in the pipeline, with their payments -->
    <div id="paneD" style="display:none;">
        <div class="se-toolbar">
            <input type="text" id="dq" class="se-in" placeholder="Search name or student number&hellip;" />
            <select id="dPay" class="se-sel" onchange="loadCand(1)">
                <option value="paid">Paid something (any amount)</option>
                <option value="partial">Partial payers &mdash; under UGX 100,000</option>
                <option value="eligible">Fully eligible &mdash; UGX 100,000+</option>
                <option value="none">No payment yet</option>
                <option value="">All 2026+ not in pipeline</option>
            </select>
            <button type="button" class="se-btn se-btn--p" onclick="loadCand(1)">Search</button>
            <button type="button" class="se-btn" onclick="qs('dq').value='';loadCand(1)">Reset</button>
        </div>
        <div class="se-meta" id="dMeta">Loading&hellip;</div>
        <div class="se-tblwrap">
            <table class="se-tbl"><thead><tr><th>Student</th><th>Student No.</th><th>Programme</th><th>Campus</th><th>Year</th><th>Paid</th><th></th></tr></thead><tbody id="dBody"></tbody></table>
        </div>
        <div class="se-empty" id="dEmpty" style="display:none;">No students match this filter.</div>
        <div class="se-pager" id="dPager"></div>
    </div>

    <!-- Complaints -->
    <div id="paneC" style="display:none;">
        <div class="se-toolbar">
            <select id="cStatus" class="se-sel" onchange="loadComplaints()">
                <option value="">Open complaints</option>
                <option value="SUBMITTED">Submitted</option>
                <option value="UNDER_REVIEW">Under review</option>
                <option value="RESPONDED">Responded</option>
                <option value="RESOLVED">Resolved</option>
                <option value="CLOSED">Closed</option>
            </select>
        </div>
        <div class="se-tblwrap">
            <table class="se-tbl"><thead><tr><th>Student</th><th>Category</th><th>Details</th><th>Status</th><th>Created</th><th></th></tr></thead><tbody id="cBody"></tbody></table>
        </div>
        <div class="se-empty" id="cEmpty" style="display:none;">No complaints.</div>
    </div>

    <!-- ============ Batch & Google Workspace ============ -->
    <div id="paneB" style="display:none;">
        <div class="bx-msg" id="bxMsg"></div>

        <!-- Where the intake actually is. Every action on this tab moves students left to
             right, and each step says how many are waiting in it. -->
        <div class="bx-flow" id="bxFlow">
            <div class="bx-flow__s" onclick="expOpen()" title="Allocate addresses and build the Google sheet">
                <div class="bx-flow__v" id="flowPending">–</div>
                <div class="bx-flow__l">Pending</div>
                <div class="bx-flow__h">generated, no address yet &mdash; export to allocate</div>
            </div>
            <div class="bx-flow__a">&rarr;</div>
            <div class="bx-flow__s" onclick="impOpen()" title="Import the Google export to confirm these accounts">
                <div class="bx-flow__v" id="flowCreate">–</div>
                <div class="bx-flow__l">Awaiting Google</div>
                <div class="bx-flow__h">address reserved, not yet the student's</div>
            </div>
            <div class="bx-flow__a">&rarr;</div>
            <div class="bx-flow__s" onclick="setTab('pipe')" title="See these students in the pipeline">
                <div class="bx-flow__v" id="flowGoogle">–</div>
                <div class="bx-flow__l">Confirmed &amp; issued</div>
                <div class="bx-flow__h">live in Google, student notified</div>
            </div>
        </div>

        <div class="bx-cards">
            <div class="bx-card">
                <div class="bx-card__i">1</div>
                <div class="bx-card__t">Export new accounts to Google</div>
                <div class="bx-card__d">Allocates <b>surname + first 3 letters of the other name + intake year</b> for every
                    pending student and hands back the 28-column upload sheet. The addresses are reserved so nobody else can
                    be given them &mdash; but they are <b>not the student's yet</b>, and the student is told nothing.</div>
                <button type="button" class="bx-btn bx-btn--p" onclick="expOpen()">Build the Google sheet</button>
                <button type="button" class="bx-btn" onclick="dl('template')">Blank template</button>
            </div>
            <div class="bx-card">
                <div class="bx-card__i">2</div>
                <div class="bx-card__t">Import back from Google</div>
                <div class="bx-card__d">Upload the Google export after the accounts exist. <b>This is what sets the address on
                    the student's account</b> &mdash; confirmed rows move the student to Ready for collection and notify them.
                    Addresses created outside the system are adopted, and external accounts recorded.</div>
                <button type="button" class="bx-btn bx-btn--p" onclick="impOpen()">Upload Google sheet</button>
                <button type="button" class="bx-btn bx-btn--d" onclick="releaseUnconfirmed()"
                    title="Clear addresses that were allocated and exported but that Google never created">Release unconfirmed</button>
            </div>
            <div class="bx-card">
                <div class="bx-card__i">3</div>
                <div class="bx-card__t">Address directory</div>
                <div class="bx-card__d">Every address known on the domain, and any that has been issued twice. This is the
                    list the allocator checks &mdash; if it is here, nobody else can be given it.</div>
                <button type="button" class="bx-btn bx-btn--p" onclick="dirOpen()">Open directory</button>
                <button type="button" class="bx-btn" onclick="dl('credentials')">Credentials sheet</button>
            </div>
        </div>

        <div class="bx-sec">Recent batches</div>
        <div class="se-tblwrap">
            <table class="se-tbl"><thead><tr>
                <th>Reference</th><th>Type</th><th>Status</th><th>Rows</th><th>Applied</th><th>Skipped</th><th>Failed</th><th>By</th><th>When</th><th></th>
            </tr></thead><tbody id="bBody"></tbody></table>
        </div>
        <div class="se-empty" id="bEmpty" style="display:none;">No batches yet — start with “Create addresses in bulk”.</div>
    </div>
</div>

<!-- Create email modal -->
<div class="se-ov" id="ov" onclick="closeM()"></div>
<div class="se-modal" id="mCreate" role="dialog" aria-modal="true">
    <div class="se-modal__h"><span class="se-modal__t">Create University Email</span><button type="button" class="se-modal__x" onclick="closeM()">&times;</button></div>
    <div class="se-modal__b">
        <div class="se-msg" id="mMsg"></div>
        <div id="mWho" style="font-size:12.5px;color:#64748b;margin-bottom:6px;"></div>

        <!-- Suggested addresses, built from the student's own name + intake year.
             Click the address (or "Use") to drop it into the field; the icon copies it. -->
        <div class="se-sug" id="mSug">
            <div class="se-sug__h">Suggested addresses</div>
            <div class="se-sug__r">
                <span class="se-sug__v" id="sug1" title="Click to use this address" onclick="useSug(1)">&mdash;</span>
                <button type="button" class="se-use" onclick="useSug(1)">Use</button>
                <button type="button" class="se-cp" id="cp1" title="Copy" onclick="copyVal('sug1','cp1')"></button>
            </div>
            <div class="se-sug__r">
                <span class="se-sug__v" id="sug2" title="Click to use this address" onclick="useSug(2)">&mdash;</span>
                <button type="button" class="se-use" onclick="useSug(2)">Use</button>
                <button type="button" class="se-cp" id="cp2" title="Copy" onclick="copyVal('sug2','cp2')"></button>
            </div>
        </div>

        <label class="se-fl">University email address</label>
        <input type="text" id="mEmail" class="se-fi" placeholder="e.g. jdoe26@mru.ac.ug" autocomplete="off" />
        <label class="se-fl">Temporary password</label>
        <div class="se-fi--row">
            <input type="text" id="mPw" class="se-fi" autocomplete="off" />
            <button type="button" class="se-cp" id="cpPw" title="Copy password" onclick="copyVal('mPw','cpPw')"></button>
        </div>
        <label class="se-fl">Notes (optional)</label>
        <input type="text" id="mNotes" class="se-fi" autocomplete="off" />
    </div>
    <div class="se-modal__f">
        <button type="button" class="se-btn" onclick="closeM()">Cancel</button>
        <button type="button" class="se-btn se-btn--p" id="mSave" onclick="saveCreate()">Create &amp; make ready</button>
    </div>
</div>

<!-- Complaint respond modal -->
<div class="se-modal" id="mResp" role="dialog" aria-modal="true">
    <div class="se-modal__h"><span class="se-modal__t">Respond to complaint</span><button type="button" class="se-modal__x" onclick="closeM()">&times;</button></div>
    <div class="se-modal__b">
        <div class="se-msg" id="rMsg"></div>
        <div id="rWho" style="font-size:12.5px;color:#64748b;margin-bottom:6px;"></div>
        <label class="se-fl">Status</label>
        <select id="rStatus" class="se-fi">
            <option value="UNDER_REVIEW">Under review</option>
            <option value="RESPONDED">Responded</option>
            <option value="RESOLVED">Resolved</option>
            <option value="CLOSED">Closed</option>
        </select>
        <label class="se-fl">Response to student</label>
        <textarea id="rText" class="se-fi" rows="3"></textarea>
    </div>
    <div class="se-modal__f"><button type="button" class="se-btn" onclick="closeM()">Cancel</button><button type="button" class="se-btn se-btn--p" onclick="saveResp()">Send update</button></div>
</div>

<!-- 360 Manage modal -->
<div class="se-modal se-modal--wide" id="mManage" role="dialog" aria-modal="true">
    <div class="se-modal__h"><span class="se-modal__t" id="gTitle">Manage student</span><button type="button" class="se-modal__x" onclick="closeM()">&times;</button></div>
    <div class="se-modal__b" id="gBody" style="max-height:72vh;overflow:auto;"><div style="text-align:center;color:#94a3b8;padding:30px">Loading&hellip;</div></div>
</div>

<style>
.se-modal--wide{max-width:640px;}
.se-badge{display:inline-block;padding:2px 8px;font-size:10px;font-weight:700;border-radius:0;}
.se-b--pending{background:#fff3cd;color:#92610a;}.se-b--ready{background:#e8f0fe;color:#1d4ed8;}.se-b--done{background:#e6f4ea;color:#15803d;}.se-b--muted{background:#f1f5f9;color:#94a3b8;}.se-b--verified{background:#e6f4ea;color:#15803d;}
.se-pay{display:inline-block;padding:1px 7px;font-size:11px;font-weight:700;font-variant-numeric:tabular-nums;}
.se-pay--full{background:#e6f4ea;color:#15803d;}.se-pay--part{background:#fff3cd;color:#b45309;}.se-pay--none{background:#f1f5f9;color:#94a3b8;}
.g-grid{display:grid;grid-template-columns:1fr 1fr;gap:6px 14px;margin:0 0 14px;}
.g-grid div{font-size:12px;padding:5px 0;border-bottom:1px dashed #eef2f7;}
.g-grid span{color:#94a3b8;text-transform:uppercase;font-size:9.5px;font-weight:700;letter-spacing:.3px;display:block;}
.g-grid b{color:#05275C;font-weight:700;word-break:break-word;}
.g-sec{margin:14px 0 8px;font-size:11px;font-weight:800;color:#05275C;text-transform:uppercase;letter-spacing:.4px;padding-bottom:5px;border-bottom:1px solid #eef2f7;}
.g-tl{list-style:none;padding:0;margin:0 0 8px;}
.g-tl li{font-size:12px;padding:4px 0;color:#475569;display:flex;justify-content:space-between;gap:10px;}
.g-tl li b{color:#05275C;}
.g-act{display:flex;flex-wrap:wrap;gap:7px;margin:10px 0 4px;}
.g-abtn{border:1px solid #dde1e6;background:#fff;padding:7px 11px;font-size:12px;font-weight:700;cursor:pointer;color:#374151;}
.g-abtn:hover{border-color:#174DA4;color:#174DA4;}
.g-abtn--p{background:#05275C;color:#fff;border-color:#05275C;}.g-abtn--p:hover{background:#174DA4;color:#fff;}
.g-abtn--d{color:#b91c1c;border-color:#fbc4c4;}.g-abtn--d:hover{background:#b91c1c;color:#fff;}
.g-log{max-height:150px;overflow:auto;border:1px solid #eef2f7;}
.g-log div{font-size:11px;padding:5px 9px;border-bottom:1px solid #f5f7fa;color:#475569;}
.g-log div b{color:#05275C;}.g-log div span{color:#94a3b8;float:right;}
</style>

<script type="text/javascript">
(function(){
'use strict';
var st={q:'',stage:'',verif:'',prog:'',campus:'',page:1,ps:25};
function qs(id){return document.getElementById(id);}
function esc(s){return s?String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'):'';}
function fmt(n){return (parseInt(n,10)||0).toLocaleString('en-US');}
function ajax(m,p,cb){var x=new XMLHttpRequest();x.open('POST','StudentEmailController.aspx/'+m,true);x.setRequestHeader('Content-Type','application/json; charset=utf-8');x.onload=function(){try{var o=JSON.parse(x.responseText);cb(typeof o.d==='string'?JSON.parse(o.d):o.d);}catch(e){cb({success:false,message:'Parse error'});}};x.onerror=function(){cb({success:false,message:'Network error'});};x.send(JSON.stringify(p||{}));}
function topMsg(m,ok){var e=qs('topMsg');e.textContent=m;e.className='se-msg '+(ok?'se-msg--ok':'se-msg--err');e.style.display='block';setTimeout(function(){e.style.display='none';},5000);}

// URL state
var PAGE_SIZES=[25,50,100,200];   // 200 is the server-side ceiling in SemsAdmin.Search

function readUrl(){
    var u=new URLSearchParams(location.search||'');
    st.q=u.get('q')||'';st.stage=u.get('stage')||'';st.verif=u.get('verif')||'';
    st.prog=u.get('prog')||'';st.campus=u.get('campus')||'';
    st.page=Math.max(1,parseInt(u.get('page'),10)||1);
    var ps=parseInt(u.get('ps'),10)||25;
    st.ps=(PAGE_SIZES.indexOf(ps)>=0)?ps:25;
}

// Build a real, complete URL for any state — this is what the pager anchors point at, so
// every page is a genuine GET address: bookmarkable, shareable, and openable in a new tab.
function buildUrl(o){
    o=o||{};
    function pick(k){return (k in o)?o[k]:st[k];}
    var u=new URLSearchParams();
    var q=pick('q'),stage=pick('stage'),verif=pick('verif'),prog=pick('prog'),campus=pick('campus'),
        page=pick('page'),ps=pick('ps');
    if(q)u.set('q',q); if(stage)u.set('stage',stage); if(verif)u.set('verif',verif);
    if(prog)u.set('prog',prog); if(campus)u.set('campus',campus);
    if(page>1)u.set('page',page);
    if(ps&&ps!==25)u.set('ps',ps);
    var s=u.toString();
    return location.pathname+(s?('?'+s):'');
}

// pushState (not replaceState) so Back genuinely walks the pages the user visited.
function syncUrl(push){
    var url=buildUrl({});
    if(url===location.pathname+location.search) return;      // nothing changed; don't stack duplicates
    try{ push?history.pushState(null,'',url):history.replaceState(null,'',url); }catch(e){}
}

// Back / Forward re-render from the URL alone, which is the real test of GET-driven state.
window.addEventListener('popstate',function(){
    readUrl();
    qs('fq').value=st.q;qs('fStage').value=st.stage;qs('fVerif').value=st.verif;
    qs('fProg').value=st.prog;qs('fCampus').value=st.campus;
    var sel=qs('fPs'); if(sel) sel.value=String(st.ps);
    runSearch(false);
});

function loadKpis(){ajax('Stats',{},function(r){if(!r||!r.success)return;var k=[['eligible','Eligible (100k+)','info'],['partial','Paid < 100k','warn'],['total','In Pipeline',''],['pending','Pending','warn'],['ready','Ready','ready'],['quiz','Quiz Passed','info'],['activated','Activated','ok'],['completed','Completed','ok'],['complaints','Open Complaints','warn'],['successRate','Success %','ok']];var h='';k.forEach(function(x){var raw=r[x[0]];var disp=(x[0]==='successRate')?((raw||0)+'%'):fmt(raw);h+='<div class="se-kpi'+(x[2]?' se-kpi--'+x[2]:'')+'"><div class="se-kpi__v">'+disp+'</div><div class="se-kpi__l">'+x[1]+'</div></div>';});qs('kpis').innerHTML=h;renderCampuses(r.campuses);});}

// Per-campus cards: headline total, the four stages that matter, and completion progress.
function renderCampuses(list){
    var el=qs('camps'); if(!el) return;
    if(!list||!list.length){el.innerHTML='';return;}
    var h='';
    list.forEach(function(c){
        var pct=parseFloat(c.successRate)||0;
        h+='<div class="se-camp" data-code="'+esc(c.code)+'" onclick="pickCampus(\''+esc(c.code)+'\')" title="Filter the pipeline to '+esc(c.name)+'">'
          +  '<div class="se-camp__h"><span class="se-camp__n">'+esc(c.name)+'</span><span class="se-camp__t">'+fmt(c.total)+'</span></div>'
          +  '<div class="se-camp__g">'
          +    '<div class="se-camp__s"><div class="se-camp__sv">'+fmt(c.pending)+'</div><div class="se-camp__sl">Pending</div></div>'
          +    '<div class="se-camp__s"><div class="se-camp__sv">'+fmt(c.ready)+'</div><div class="se-camp__sl">Ready</div></div>'
          +    '<div class="se-camp__s"><div class="se-camp__sv">'+fmt(c.verified)+'</div><div class="se-camp__sl">Verified</div></div>'
          +    '<div class="se-camp__s"><div class="se-camp__sv">'+fmt(c.completed)+'</div><div class="se-camp__sl">Done</div></div>'
          +  '</div>'
          +  '<div class="se-camp__bar"><div class="se-camp__fill" style="width:'+pct+'%"></div></div>'
          +  '<div class="se-camp__pct">'+pct+'% completed</div>'
          +'</div>';
    });
    el.innerHTML=h;
    markActiveCampus();
}
function payBadge(p){p=parseInt(p,10)||0;var c=p>=100000?'full':(p>0?'part':'none');return '<span class="se-pay se-pay--'+c+'">'+(p>0?('UGX '+fmt(p)):'—')+'</span>';}

function stageBadge(s){var m={PENDING_CREATION:['pending','Pending creation'],READY_FOR_COLLECTION:['ready','Ready for collection'],EMAIL_CREATED:['ready','Email created'],COMPLETED:['done','Completed']};var x=m[s]||['muted',s||'-'];return '<span class="se-badge se-b--'+x[0]+'">'+esc(x[1])+'</span>';}

// Reads the filter controls, then searches. Called by the Search button and the filters.
window.doSearch=function(page){
    st.q=qs('fq').value.trim();st.stage=qs('fStage').value;st.verif=qs('fVerif').value;
    st.prog=qs('fProg').value;st.campus=qs('fCampus').value;
    var sel=qs('fPs'); if(sel) st.ps=parseInt(sel.value,10)||25;
    st.page=page||1;
    runSearch(true);
};

// Jump to a page without re-reading the controls (used by the pager anchors).
window.goPage=function(p){ st.page=Math.max(1,p||1); runSearch(true); return false; };

// Only the newest in-flight search may paint. Without this, typing quickly could let a
// slower earlier response land last and overwrite the results for what you actually typed.
var _seq=0;

function runSearch(push){
    syncUrl(push);
    markActiveCampus();
    var mine=++_seq;
    qs('pMeta').textContent='Loading…';
    ajax('Search',{q:st.q,stage:st.stage,campus:st.campus,programme:st.prog,year:'',verification:st.verif,page:st.page,pageSize:st.ps},function(r){
        if(mine!==_seq) return;                       // a newer search has already been issued
        if(!r||!r.success){qs('pMeta').textContent=(r&&r.message)||'Error';return;}
        st.page=r.page;                               // server clamps out-of-range pages
        var from=r.total?((r.page-1)*r.pageSize+1):0, to=Math.min(r.page*r.pageSize,r.total);
        qs('pMeta').textContent=r.total
            ? ('Showing '+fmt(from)+'–'+fmt(to)+' of '+fmt(r.total)+' student'+(r.total===1?'':'s')+(r.pageCount>1?(' · page '+r.page+' of '+r.pageCount):''))
            : 'No students match these filters.';
        var b=qs('pBody');b.innerHTML='';qs('pEmpty').style.display=r.rows.length?'none':'block';
        r.rows.forEach(function(x){var canCreate=(x.stage==='PENDING_CREATION');var rg=x.regno.replace(/'/g,"");var tr=document.createElement('tr');
         tr.innerHTML='<td><strong>'+esc(x.name||'-')+'</strong></td><td>'+esc(x.regno)+'</td><td>'+esc(x.programme||'-')+'</td><td>'+esc(x.campus)+'</td><td>'+esc(x.year)+'</td><td>'+payBadge(x.paid)+'</td><td>'+(x.email?esc(x.email):'<span style="color:#cbd5e1">—</span>')+'</td><td>'+stageBadge(x.stage)+'</td><td>'+(x.verification==='VERIFIED'?'<span class="se-badge se-b--verified">Verified</span>':'<span class="se-badge se-b--muted">—</span>')+'</td><td style="color:#94a3b8">'+esc(x.updated||x.created)+'</td><td style="white-space:nowrap">'+(canCreate?'<span class="se-act" onclick="openCreate(\''+rg+'\',\''+esc(x.name).replace(/'/g,"")+'\',\''+esc(x.year)+'\')">Create</span> · ':'')+'<span class="se-act" onclick="openManage(\''+rg+'\')">Manage</span></td>';
         b.appendChild(tr);});
        renderPager(r.page,r.pageCount);
    });
}

// Pager built from real anchors, so every page number is a working link (middle-click,
// open-in-new-tab, copy-address all behave) while a left-click stays on the AJAX path.
// Window is 9 wide with First/Last, rather than the previous 5 with no jump to the ends.
function renderPager(page,pc){
    var el=qs('pPager');el.innerHTML='';
    if(pc<=1)return;
    function link(label,pg,disabled,active,title){
        if(disabled){var s=document.createElement('button');s.textContent=label;s.disabled=true;el.appendChild(s);return;}
        var a=document.createElement('a');
        a.href=buildUrl({page:pg});
        a.textContent=label;
        if(title)a.title=title;
        if(active)a.className='active';
        a.onclick=function(ev){
            if(ev.metaKey||ev.ctrlKey||ev.shiftKey||ev.button===1) return true;  // let the browser open it
            ev.preventDefault();goPage(pg);return false;
        };
        el.appendChild(a);
    }
    link('« First',1,page<=1,false,'First page');
    link('‹ Prev',page-1,page<=1,false,'Previous page');
    var span=9, half=Math.floor(span/2);
    var f=Math.max(1,page-half), t=Math.min(pc,f+span-1);
    if(t-f+1<span) f=Math.max(1,t-span+1);
    if(f>1) el.appendChild(document.createTextNode('…'));
    for(var i=f;i<=t;i++) link(String(i),i,false,i===page);
    if(t<pc) el.appendChild(document.createTextNode('…'));
    link('Next ›',page+1,page>=pc,false,'Next page');
    link('Last »',pc,page>=pc,false,'Last page');
}
window.resetF=function(){qs('fq').value='';qs('fStage').value='';qs('fVerif').value='';qs('fProg').value='';qs('fCampus').value='';var sel=qs('fPs');if(sel)sel.value='25';st={q:'',stage:'',verif:'',prog:'',campus:'',page:1,ps:25};runSearch(true);};

// Clicking a campus card is just another way to set the filter — it drives the same
// dropdown and the same search, so the two can never disagree.
window.pickCampus=function(code){
    qs('fCampus').value=(st.campus===code)?'':code;   // click the active card again to clear
    doSearch(1);
    var t=qs('paneP'); if(t&&t.scrollIntoView) t.scrollIntoView({behavior:'smooth',block:'nearest'});
};
function markActiveCampus(){
    var cards=document.querySelectorAll('.se-camp');
    for(var i=0;i<cards.length;i++)
        cards[i].className='se-camp'+(cards[i].getAttribute('data-code')===st.campus&&st.campus?' active':'');
}

window.genEligible=function(){var btn=qs('btnGen');btn.disabled=true;btn.textContent='Generating…';ajax('GenerateEligible',{},function(r){btn.disabled=false;btn.innerHTML='<svg xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 5v14M5 12h14"/></svg> Generate Eligible Students';if(r&&r.success){topMsg(r.message,true);loadKpis();doSearch(1);}else topMsg((r&&r.message)||'Failed',false);});};

// ── Create modal ────────────────────────────────────────────────────────────
var _cReg='';
// The one password every student account is created with. Google is told to force a change
// at first sign-in, so it survives exactly one login — which is what makes it safe to print
// on the notice board and say out loud at the ICT desk.
var DEFAULT_PW='mru12345';

// Strip anything that can't live in the local part of an address, and lowercase it.
function slug(s){return String(s||'').toLowerCase().replace(/[^a-z]/g,'');}

// Two house formats, both suffixed with the last two digits of the intake year:
//   1. firstname + first letter of surname      e.g. wilberforces26@mru.ac.ug
//   2. surname   + first letter of first name   e.g. ssentongow26@mru.ac.ug
// Names are stored as "FIRSTNAME SURNAME", so the first token is the given name and the
// last token is the family name. Middle names are ignored.
function buildSug(name,year){
    var parts=String(name||'').trim().split(/\s+/).filter(Boolean);
    var first=slug(parts[0]), last=slug(parts.length>1?parts[parts.length-1]:'');
    var yy=String(year||'').replace(/\D/g,'');
    yy=yy.length>=2?yy.slice(-2):yy;
    if(!first&&!last) return ['',''];
    // With only one usable name token, fall back to that token + year rather than
    // inventing an initial from nothing.
    if(!last)  return [first+yy+'@mru.ac.ug', first+yy+'@mru.ac.ug'];
    if(!first) return [last+yy+'@mru.ac.ug',  last+yy+'@mru.ac.ug'];
    return [first+last.charAt(0)+yy+'@mru.ac.ug', last+first.charAt(0)+yy+'@mru.ac.ug'];
}

var ICON_COPY='<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>';
var ICON_OK  ='<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>';

// Copy helper. execCommand is the fallback because navigator.clipboard is unavailable
// on plain-HTTP origins, and this console is reached over http internally.
function copyText(t){
    if(!t) return false;
    if(navigator.clipboard&&window.isSecureContext){navigator.clipboard.writeText(t);return true;}
    var ta=document.createElement('textarea');
    ta.value=t;ta.setAttribute('readonly','');ta.style.position='fixed';ta.style.top='-1000px';
    document.body.appendChild(ta);ta.select();
    var ok=false;try{ok=document.execCommand('copy');}catch(e){ok=false;}
    document.body.removeChild(ta);return ok;
}
window.copyVal=function(srcId,btnId){
    var el=qs(srcId);if(!el)return;
    var val=(el.tagName==='INPUT')?el.value:el.textContent;
    val=String(val||'').trim();
    if(!val||val==='—')return;
    var b=qs(btnId);
    if(copyText(val)){
        b.innerHTML=ICON_OK;b.classList.add('ok');b.title='Copied';
        setTimeout(function(){b.innerHTML=ICON_COPY;b.classList.remove('ok');b.title='Copy';},1400);
    }
};
window.useSug=function(n){
    var v=qs('sug'+n).textContent.trim();
    if(v&&v!=='—'){qs('mEmail').value=v;qs('mEmail').focus();}
};

window.openCreate=function(reg,name,year){
    _cReg=reg;
    qs('mWho').textContent=name+' · '+reg;
    qs('mEmail').value='';
    qs('mPw').value=DEFAULT_PW;                  // pre-filled, still editable
    qs('mNotes').value='';
    qs('mMsg').style.display='none';
    qs('cp1').innerHTML=ICON_COPY;qs('cp2').innerHTML=ICON_COPY;qs('cpPw').innerHTML=ICON_COPY;
    qs('cp1').classList.remove('ok');qs('cp2').classList.remove('ok');qs('cpPw').classList.remove('ok');
    var s=buildSug(name,year);
    qs('sug1').textContent=s[0]||'—';
    qs('sug2').textContent=s[1]||'—';
    qs('mSug').style.display=(s[0]||s[1])?'block':'none';
    qs('ov').style.display='block';qs('mCreate').style.display='block';
};
window.saveCreate=function(){var em=qs('mEmail').value.trim(),pw=qs('mPw').value.trim();if(!em||!pw){modMsg('mMsg','Email and temporary password are required.',false);return;}qs('mSave').disabled=true;ajax('CreateEmail',{regno:_cReg,email:em,tempPw:pw,notes:qs('mNotes').value.trim()},function(r){qs('mSave').disabled=false;if(r&&r.success){closeM();topMsg(r.message,true);loadKpis();doSearch(st.page);}else modMsg('mMsg',(r&&r.message)||'Failed',false);});};
function modMsg(id,m,ok){var e=qs(id);e.textContent=m;e.className='se-msg '+(ok?'se-msg--ok':'se-msg--err');e.style.display='block';}
window.closeM=function(){qs('ov').style.display='none';qs('mCreate').style.display='none';qs('mResp').style.display='none';qs('mManage').style.display='none';};

// Tabs
window.setTab=function(t){qs('tabPipe').classList.toggle('active',t==='pipe');qs('tabCand').classList.toggle('active',t==='cand');qs('tabComp').classList.toggle('active',t==='comp');qs('tabBatch').classList.toggle('active',t==='batch');qs('paneP').style.display=t==='pipe'?'block':'none';qs('paneD').style.display=t==='cand'?'block':'none';qs('paneC').style.display=t==='comp'?'block':'none';qs('paneB').style.display=t==='batch'?'block':'none';if(t==='comp')loadComplaints();if(t==='cand')loadCand(1);if(t==='batch'&&window.bxLoadBatches)bxLoadBatches();};

// ── Candidates (2026+ not in pipeline, with payments) ──
var _dPage=1;
window.loadCand=function(page){_dPage=page||1;qs('dMeta').textContent='Loading…';
 ajax('Candidates',{q:qs('dq').value.trim(),payFilter:qs('dPay').value,page:_dPage,pageSize:25},function(r){
  if(!r||!r.success){qs('dMeta').textContent=(r&&r.message)||'Error';return;}
  qs('dMeta').textContent=fmt(r.total)+' student'+(r.total===1?'':'s')+(r.pageCount>1?(' · page '+r.page+' of '+r.pageCount):'');
  var b=qs('dBody');b.innerHTML='';qs('dEmpty').style.display=r.rows.length?'none':'block';
  r.rows.forEach(function(x){var rg=x.regno.replace(/'/g,"");var nm=esc(x.name).replace(/'/g,"");var tr=document.createElement('tr');
   tr.innerHTML='<td><strong>'+esc(x.name||'-')+'</strong></td><td>'+esc(x.regno)+'</td><td>'+esc(x.programme||'-')+'</td><td>'+esc(x.campus)+'</td><td>'+esc(x.year)+'</td><td>'+payBadge(x.paid)+'</td><td style="white-space:nowrap"><span class="se-act" onclick="addCand(\''+rg+'\',\''+nm+'\')">+ Add to pipeline</span></td>';
   b.appendChild(tr);});
  var el=qs('dPager');el.innerHTML='';if(r.pageCount>1){function bb(t,pg,dis,act){var y=document.createElement('button');y.textContent=t;if(act)y.className='active';if(dis)y.disabled=true;else y.onclick=function(){loadCand(pg);};el.appendChild(y);}bb('‹',r.page-1,r.page<=1);var f=Math.max(1,r.page-2),tt=Math.min(r.pageCount,r.page+2);for(var i=f;i<=tt;i++)bb(String(i),i,false,i===r.page);bb('›',r.page+1,r.page>=r.pageCount);}
 });};
window.addCand=function(reg,name){if(!confirm('Add '+name+' ('+reg+') to the email pipeline?'))return;ajax('AddToPipeline',{regno:reg,note:'Added from Candidates'},function(r){if(r&&r.success){topMsg(r.message,true);loadCand(_dPage);loadKpis();}else topMsg((r&&r.message)||'Failed',false);});};

// ── 360 Manage ──
var _gReg='';
window.openManage=function(reg){_gReg=reg;qs('gTitle').textContent='Manage · '+reg;qs('gBody').innerHTML='<div style="text-align:center;color:#94a3b8;padding:30px">Loading…</div>';qs('ov').style.display='block';qs('mManage').style.display='block';
 ajax('Detail',{regno:reg},function(r){if(!r||!r.success){qs('gBody').innerHTML='<div class="se-msg se-msg--err" style="display:block">'+esc((r&&r.message)||'Could not load')+'</div>';return;}renderDetail(r);});};
function tl(label,v){return v?('<li>'+label+' <b>'+esc(v)+'</b></li>'):('<li style="opacity:.5">'+label+' <b>—</b></li>');}
function renderDetail(r){var d=r.record;var h='';
 h+='<div class="g-grid">'
  +'<div><span>Name</span><b>'+esc(d.name||'-')+'</b></div><div><span>Student No.</span><b>'+esc(d.regno)+'</b></div>'
  +'<div><span>Programme</span><b>'+esc(d.programme||'-')+'</b></div><div><span>Campus · Year</span><b>'+esc(d.campus)+' · '+esc(d.year)+'</b></div>'
  +'<div><span>Paid</span><b>'+payBadge(d.paid)+'</b></div><div><span>Stage</span><b>'+stageBadge(d.stage)+'</b></div>'
  +'<div><span>Email</span><b>'+(d.email?esc(d.email):'—')+'</b></div><div><span>Temp password</span><b>'+(d.pw?esc(d.pw):'—')+'</b></div>'
  +'<div><span>Verification</span><b>'+(d.verification==='VERIFIED'?'<span class="se-badge se-b--verified">Verified</span>':esc(d.verification||'—'))+'</b></div><div><span>Password changed</span><b>'+esc(d.pwChanged)+'</b></div>'
  +'</div>';
 if(d.notes)h+='<div style="font-size:12px;background:#f8fafc;border:1px solid #eef2f7;padding:8px 10px;margin-bottom:10px"><b>Notes:</b> '+esc(d.notes)+'</div>';
 h+='<div class="g-act">'
  +(d.stage==='PENDING_CREATION'?'<button type="button" class="g-abtn g-abtn--p" onclick="closeM();openCreate(\''+_gReg+'\',\''+esc(d.name).replace(/\x27/g,"")+'\',\''+esc(d.year)+'\')">Create email</button>':'')
  +'<button type="button" class="g-abtn" onclick="gStage()">Change stage</button>'
  +'<button type="button" class="g-abtn" onclick="gPw()">Reset password</button>'
  +'<button type="button" class="g-abtn g-abtn--d" onclick="gDel()">Remove</button>'
  +'</div>';
 h+='<div class="g-sec">Journey timeline</div><ul class="g-tl">'
  +tl('Added to pipeline',d.t_created)+tl('Email created',d.t_email)+tl('Learn done',d.t_edu)+tl('Gmail guide done',d.t_gmail)+tl('Quiz passed',d.t_quiz)+tl('Credentials viewed',d.t_viewed)+tl('Verified (Active Student)',d.t_verified)+tl('Completed',d.t_completed)+'</ul>';
 if(r.complaints&&r.complaints.length){h+='<div class="g-sec">Complaints</div>';r.complaints.forEach(function(c){h+='<div style="font-size:12px;padding:5px 0;border-bottom:1px dashed #eef2f7"><b>'+esc(c.category)+'</b> — '+esc(c.status)+' <span style="color:#94a3b8">'+esc(c.at)+'</span>'+(c.response?'<br><span style="color:#0b5c3a">'+esc(c.response)+'</span>':'')+'</div>';});}
 h+='<div class="g-sec">Activity log</div><div class="g-log">';
 (r.activity||[]).forEach(function(a){h+='<div><b>'+esc((a.action||'').replace(/_/g,' '))+'</b>'+(a.detail?(' — '+esc(a.detail)):'')+' <span>'+esc(a.at)+' · '+esc(a.actor)+'</span></div>';});
 if(!(r.activity||[]).length)h+='<div style="color:#94a3b8">No activity yet.</div>';
 h+='</div>';
 qs('gBody').innerHTML=h;}
window.gStage=function(){var s=prompt('Set stage to one of:\nPENDING_CREATION, READY_FOR_COLLECTION, EMAIL_CREATED, COMPLETED, SUSPENDED','READY_FOR_COLLECTION');if(!s)return;var note=prompt('Reason / note (optional):','')||'';ajax('SetStatus',{regno:_gReg,stage:s.trim().toUpperCase(),note:note},function(r){if(r&&r.success){topMsg(r.message,true);openManage(_gReg);loadKpis();doSearch(st.page);}else topMsg((r&&r.message)||'Failed',false);});};
window.gPw=function(){var p=prompt('New temporary password for this student:',DEFAULT_PW);if(!p||!p.trim())return;ajax('SetPassword',{regno:_gReg,tempPw:p.trim()},function(r){if(r&&r.success){topMsg(r.message,true);openManage(_gReg);}else topMsg((r&&r.message)||'Failed',false);});};
window.gDel=function(){if(!confirm('Remove '+_gReg+' from the email pipeline? This deletes the record.'))return;var note=prompt('Reason (optional):','')||'';ajax('DeleteRecord',{regno:_gReg,note:note},function(r){if(r&&r.success){closeM();topMsg(r.message,true);loadKpis();doSearch(st.page);}else topMsg((r&&r.message)||'Failed',false);});};

// Complaints
var _rId=0;
window.loadComplaints=function(){ajax('Complaints',{status:qs('cStatus').value},function(r){var b=qs('cBody');b.innerHTML='';if(!r||!r.success){return;}qs('cEmpty').style.display=r.complaints.length?'none':'block';r.complaints.forEach(function(c){var tr=document.createElement('tr');tr.innerHTML='<td><strong>'+esc(c.regno)+'</strong></td><td>'+esc(c.category)+'</td><td style="max-width:280px">'+esc(c.description||'')+'</td><td><span class="se-badge se-b--'+(c.status==='RESOLVED'||c.status==='CLOSED'?'done':'ready')+'">'+esc(c.status)+'</span></td><td style="color:#94a3b8">'+esc(c.created)+'</td><td><span class="se-act" onclick="openResp('+c.id+',\''+esc(c.regno)+'\',\''+esc(c.category)+'\')">Respond</span></td>';b.appendChild(tr);});});};
window.openResp=function(id,reg,cat){_rId=id;qs('rWho').textContent=reg+' · '+cat;qs('rText').value='';qs('rMsg').style.display='none';qs('ov').style.display='block';qs('mResp').style.display='block';};
window.saveResp=function(){ajax('RespondComplaint',{id:_rId,status:qs('rStatus').value,response:qs('rText').value.trim()},function(r){if(r&&r.success){closeM();loadComplaints();loadKpis();}else modMsg('rMsg',(r&&r.message)||'Failed',false);});};

// init
(function(){readUrl();qs('fq').value=st.q;qs('fStage').value=st.stage;qs('fVerif').value=st.verif;
 qs('fPs').value=String(st.ps);
 qs('fq').addEventListener('keydown',function(e){if(e.key==='Enter'){e.preventDefault();doSearch(1);}});
 // Type-to-search, debounced. The stale-response guard in runSearch means a slow earlier
 // reply can never overwrite the results for what is currently in the box.
 var _t=null;
 qs('fq').addEventListener('input',function(){clearTimeout(_t);_t=setTimeout(function(){doSearch(1);},350);});
 // Changing any filter starts again at page 1 — staying on page 7 of a different result
 // set is never what someone means.
 qs('fCampus').addEventListener('change',function(){doSearch(1);});
 qs('fStage').addEventListener('change',function(){doSearch(1);});
 qs('fVerif').addEventListener('change',function(){doSearch(1);});
 qs('fProg').addEventListener('change',function(){doSearch(1);});
 qs('fPs').addEventListener('change',function(){doSearch(1);});
 loadKpis();

 // The results and the filter option-lists are independent, so they are fetched in
 // PARALLEL. Previously the first search waited for Filters to come back, which put two
 // sequential round trips in front of the very first row the user sees.
 //
 // runSearch (not doSearch) is used here deliberately: doSearch reads the dropdowns, and
 // at this instant fProg/fCampus are still empty — reading them would wipe the programme
 // and campus that came in on the URL. runSearch works from the parsed state instead.
 runSearch(false);

 ajax('Filters',{},function(r){
  if(!r||!r.success) return;
  var s=qs('fProg');(r.programmes||[]).forEach(function(p){var o=document.createElement('option');o.value=p.code;o.textContent=p.name;s.appendChild(o);});
  s.value=st.prog;
  // Campuses come from the pipeline itself, with counts, so the list can never offer an
  // option that returns nothing — and picks up a third campus automatically if one appears.
  var cs=qs('fCampus');(r.campuses||[]).forEach(function(cp){var o=document.createElement('option');o.value=cp.code;o.textContent=cp.name+' ('+fmt(cp.count)+')';cs.appendChild(o);});
  cs.value=st.campus;
 });
})();
})();
</script>

<!-- =====================================================================
     Batch creation + Google Workspace sync.
     Kept in its own overlay/module so it cannot disturb the existing
     single-student modals: separate ids, separate close handler.
     ===================================================================== -->
<style>
.bx-flow{display:flex;align-items:stretch;gap:0;margin-bottom:16px;flex-wrap:wrap;}
.bx-flow__s{flex:1 1 180px;background:#fff;border:1px solid #e0e5ed;border-top:3px solid #05275C;padding:12px 14px;cursor:pointer;min-width:0;}
.bx-flow__s:hover{border-color:#174DA4;box-shadow:0 2px 10px rgba(5,39,92,.09);}
.bx-flow__v{font-size:24px;font-weight:800;color:#05275C;line-height:1;font-variant-numeric:tabular-nums;}
.bx-flow__l{font-size:11px;font-weight:800;text-transform:uppercase;letter-spacing:.4px;color:#334155;margin-top:6px;}
.bx-flow__h{font-size:10px;color:#94a3b8;margin-top:3px;}
.bx-flow__a{display:flex;align-items:center;padding:0 10px;color:#cbd5e1;font-size:18px;font-weight:700;}
@media(max-width:640px){.bx-flow__a{display:none;}}
.bx-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:12px;margin-bottom:20px;}
.bx-card{background:#fff;border:1px solid #e0e5ed;border-top:3px solid #05275C;padding:16px 16px 14px;display:flex;flex-direction:column;}
.bx-card__i{width:22px;height:22px;background:#05275C;color:#fff;font-size:11px;font-weight:800;display:flex;align-items:center;justify-content:center;margin-bottom:9px;}
.bx-card__t{font-size:14px;font-weight:800;color:#05275C;margin-bottom:6px;}
.bx-card__d{font-size:11.5px;color:#64748b;line-height:1.55;flex:1 1 auto;margin-bottom:12px;}
.bx-card__d b{color:#334155;}
.bx-btn{padding:8px 13px;border:1px solid #cbd5e1;background:#fff;color:#334155;font-size:12px;font-weight:700;cursor:pointer;margin-right:6px;font-family:inherit;}
.bx-btn:hover{border-color:#174DA4;color:#174DA4;}
.bx-btn--p{background:#05275C;border-color:#05275C;color:#fff;}.bx-btn--p:hover{background:#174DA4;color:#fff;}
.bx-btn--d{color:#b91c1c;border-color:#fbc4c4;}.bx-btn--d:hover{background:#b91c1c;color:#fff;}
.bx-btn:disabled{opacity:.5;cursor:default;}
.bx-sec{font-size:11px;font-weight:800;color:#05275C;text-transform:uppercase;letter-spacing:.4px;margin:18px 0 8px;padding-bottom:6px;border-bottom:1px solid #e0e5ed;}
.bx-ov{display:none;position:fixed;inset:0;background:rgba(5,39,92,.6);z-index:1100;}
.bx-modal{display:none;position:fixed;z-index:1101;top:50%;left:50%;transform:translate(-50%,-50%);width:96%;max-width:1120px;max-height:92vh;background:#fff;box-shadow:0 24px 70px rgba(0,0,0,.4);flex-direction:column;}
.bx-modal.show{display:flex;}
.bx-modal--sm{max-width:560px;}
.bx-h{display:flex;justify-content:space-between;align-items:center;padding:14px 18px;border-bottom:1px solid #eef2f7;flex:0 0 auto;}
.bx-h__t{font-size:16px;font-weight:800;color:#05275C;}
.bx-h__x{border:0;background:none;font-size:24px;color:#94a3b8;cursor:pointer;line-height:1;}
.bx-b{padding:16px 18px;overflow:auto;flex:1 1 auto;}
.bx-f{padding:12px 18px;border-top:1px solid #eef2f7;display:flex;justify-content:space-between;align-items:center;gap:8px;flex:0 0 auto;background:#fbfcfe;}
.bx-steps{display:flex;gap:0;margin-bottom:16px;border:1px solid #e0e5ed;}
.bx-step{flex:1 1 0;padding:9px 10px;font-size:11px;font-weight:700;color:#94a3b8;text-align:center;border-right:1px solid #e0e5ed;background:#f9fafc;}
.bx-step:last-child{border-right:0;}
.bx-step.on{background:#05275C;color:#fff;}
.bx-step.done{background:#e6f4ec;color:#0b5c3a;}
.bx-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:12px 16px;}
.bx-fl{display:block;font-size:11px;font-weight:700;color:#374151;margin:0 0 4px;}
.bx-hint{font-size:10.5px;color:#94a3b8;margin-top:3px;line-height:1.45;}
.bx-in,.bx-sel{width:100%;box-sizing:border-box;padding:8px 10px;border:1px solid #cbd5e1;font-size:13px;background:#fff;color:#1a1a2e;font-family:inherit;}
.bx-in:focus,.bx-sel:focus{border-color:#174DA4;outline:none;}
.bx-fld{margin-bottom:12px;}
.bx-chk{display:flex;align-items:flex-start;gap:7px;font-size:12px;color:#334155;margin:7px 0;cursor:pointer;}
.bx-chk input{margin-top:2px;}
.bx-eg{background:#f7faff;border:1px solid #dbe6f5;padding:11px 13px;margin:12px 0;font-size:12px;color:#334155;}
.bx-eg code{font-family:Consolas,"Courier New",monospace;font-weight:700;color:#05275C;font-size:13px;}
.bx-chips{display:flex;flex-wrap:wrap;gap:6px;margin-bottom:10px;}
.bx-chip{padding:5px 11px;border:1px solid #cbd5e1;background:#fff;font-size:11.5px;font-weight:700;cursor:pointer;color:#475569;}
.bx-chip.on{background:#05275C;border-color:#05275C;color:#fff;}
.bx-chip b{font-variant-numeric:tabular-nums;}
.bx-tblwrap{border:1px solid #e0e5ed;overflow:auto;max-height:52vh;}
.bx-tbl{width:100%;border-collapse:collapse;font-size:11.5px;min-width:900px;}
.bx-tbl th{position:sticky;top:0;background:#f9fafc;text-align:left;padding:8px 9px;font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;color:#64748b;border-bottom:2px solid #e0e5ed;white-space:nowrap;z-index:1;}
.bx-tbl td{padding:6px 9px;border-bottom:1px solid #f0f3f7;vertical-align:middle;}
.bx-tbl tbody tr:hover td{background:#f9fbff;}
.bx-tbl tr.off td{opacity:.4;text-decoration:line-through;}
.bx-em{width:100%;box-sizing:border-box;padding:5px 7px;border:1px solid #dbe6f5;font-size:11.5px;font-family:Consolas,"Courier New",monospace;font-weight:700;color:#05275C;background:#fbfdff;}
.bx-em:focus{border-color:#174DA4;outline:none;background:#fff;}
.bx-em.bad{border-color:#dc2626;background:#fef2f2;color:#b91c1c;}
.bx-em.good{border-color:#16a34a;background:#f0fdf4;}
.bx-pw{font-family:Consolas,"Courier New",monospace;font-size:11px;color:#475569;}
.bx-sev{display:inline-block;padding:1px 7px;font-size:9.5px;font-weight:800;text-transform:uppercase;letter-spacing:.3px;white-space:nowrap;}
.bx-sev--OK{background:#e6f4ec;color:#0b5c3a;}
.bx-sev--WARN{background:#fff7ed;color:#9a3412;}
.bx-sev--ERROR{background:#fef2f2;color:#b91c1c;}
.bx-sev--SKIP{background:#f1f5f9;color:#475569;}
.bx-msg{font-size:12.5px;padding:10px 12px;margin-bottom:12px;display:none;}
.bx-msg--ok{background:#e6f4ec;color:#0b5c3a;border:1px solid #b5dcc5;}
.bx-msg--err{background:#fef2f2;color:#b91c1c;border:1px solid #fecaca;}
.bx-msg--warn{background:#fff7ed;color:#9a3412;border:1px solid #fed7aa;}
.bx-msg--info{background:#eff6ff;color:#1e40af;border:1px solid #bfdbfe;}
.bx-drop{border:2px dashed #cbd5e1;background:#f9fbff;padding:28px 18px;text-align:center;cursor:pointer;}
.bx-drop.over{border-color:#174DA4;background:#eff6ff;}
.bx-drop__t{font-size:13px;font-weight:700;color:#05275C;margin-bottom:4px;}
.bx-drop__d{font-size:11.5px;color:#64748b;}
.bx-stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(110px,1fr));gap:8px;margin-bottom:14px;}
.bx-stat{background:#fff;border:1px solid #e0e5ed;border-left:3px solid #174DA4;padding:9px 11px;}
.bx-stat__v{font-size:19px;font-weight:800;color:#05275C;line-height:1;font-variant-numeric:tabular-nums;}
.bx-stat__l{font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;color:#94a3b8;margin-top:4px;}
.bx-stat--ok{border-left-color:#16a34a;}.bx-stat--warn{border-left-color:#ea580c;}.bx-stat--err{border-left-color:#dc2626;}
.bx-spin{display:inline-block;width:13px;height:13px;border:2px solid #cbd5e1;border-top-color:#05275C;border-radius:50%;animation:bxspin .7s linear infinite;vertical-align:-2px;margin-right:6px;}
@keyframes bxspin{to{transform:rotate(360deg);}}
@media(max-width:700px){.bx-modal{width:100%;max-width:100%;height:100%;max-height:100%;top:0;left:0;transform:none;}}
</style>

<div class="bx-ov" id="bxOv" onclick="bxClose()"></div>

<!-- ── Batch creation wizard ── -->
<div class="bx-modal" id="bxWiz" role="dialog" aria-modal="true">
    <div class="bx-h">
        <span class="bx-h__t" id="wizTitle">Create university email addresses</span>
        <button type="button" class="bx-h__x" onclick="wizCancel()">&times;</button>
    </div>
    <div class="bx-b">
        <div class="bx-steps">
            <div class="bx-step on" id="wizS1">1 &middot; Who</div>
            <div class="bx-step" id="wizS2">2 &middot; Address rule</div>
            <div class="bx-step" id="wizS3">3 &middot; Review &amp; fix</div>
            <div class="bx-step" id="wizS4">4 &middot; Apply</div>
        </div>
        <div class="bx-msg" id="wizMsg"></div>

        <!-- step 1 -->
        <div id="wizP1">
            <div class="bx-grid">
                <div class="bx-fld">
                    <label class="bx-fl">Stage</label>
                    <select id="wStage" class="bx-sel">
                        <option value="PENDING_CREATION">Pending creation (no address yet)</option>
                        <option value="">Any stage without an address</option>
                    </select>
                    <div class="bx-hint">Students who already hold an address are never included.</div>
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Campus</label>
                    <select id="wCampus" class="bx-sel"><option value="">All campuses</option></select>
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Programme</label>
                    <select id="wProg" class="bx-sel"><option value="">All programmes</option></select>
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Intake year</label>
                    <input type="text" id="wYear" class="bx-in" placeholder="e.g. 2026 — blank for all" />
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Name / student number contains</label>
                    <input type="text" id="wQ" class="bx-in" placeholder="optional" />
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Maximum students in this batch</label>
                    <input type="number" id="wLimit" class="bx-in" value="500" min="1" max="2000" />
                    <div class="bx-hint">A batch is capped at 2,000 so a review stays reviewable. Run several.</div>
                </div>
            </div>
            <div class="bx-msg bx-msg--info" id="wizCount" style="display:block">Choose a scope, then continue.</div>
        </div>

        <!-- step 2 -->
        <div id="wizP2" style="display:none;">
            <div class="bx-eg" id="wizEg">
                Format preview: <code id="egOut">&mdash;</code>
                <div class="bx-hint" id="egWho"></div>
            </div>
            <div class="bx-grid">
                <div class="bx-fld">
                    <label class="bx-fl">Letters taken from the other name</label>
                    <input type="number" id="wOther" class="bx-in" value="3" min="1" max="12" />
                    <div class="bx-hint">House rule is 3 &mdash; surname + 3 letters + year.</div>
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Which field is the surname?</label>
                    <select id="wOrder" class="bx-sel">
                        <option value="OTHER_IS_SURNAME">Other Name column (correct for the 2026 intake)</option>
                        <option value="FIRST_IS_SURNAME">First Name column</option>
                    </select>
                    <div class="bx-hint">Older records are inconsistent &mdash; check the review step.</div>
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Domain</label>
                    <input type="text" id="wDomain" class="bx-in" value="mru.ac.ug" />
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Google org unit path</label>
                    <input type="text" id="wOrg" class="bx-in" value="/" />
                    <div class="bx-hint">Must already exist in Google. <b>/</b> is the root and always works.</div>
                </div>
                <div class="bx-fld">
                    <label class="bx-fl">Temporary password</label>
                    <select id="wPwMode" class="bx-sel" onchange="wizPwMode()">
                        <option value="default">House default &mdash; mru12345 (recommended)</option>
                        <option value="fixed">A different shared password</option>
                        <option value="unique">Unique per student</option>
                    </select>
                    <input type="text" id="wPwFixed" class="bx-in" style="display:none;margin-top:6px" placeholder="at least 8 characters" />
                    <div class="bx-hint" id="wPwHint"></div>
                </div>
            </div>
            <label class="bx-chk"><input type="checkbox" id="wChangePw" checked /> <span>Force a password change at first sign-in (written into the Google sheet). <b>Leave this on</b> &mdash; it is what makes a shared password safe.</span></label>
            <div class="bx-msg bx-msg--info" style="display:block">
                These addresses are <b>reserved, not issued</b>. Students stay Pending and are told nothing until the Google
                export is imported back and the accounts are confirmed to exist.
            </div>
        </div>

        <!-- step 3 -->
        <div id="wizP3" style="display:none;">
            <div class="bx-stats" id="wizStats"></div>
            <div class="bx-chips" id="wizChips"></div>
            <div class="bx-hint" style="margin-bottom:7px">
                Any address can be edited before it is applied &mdash; it is re-checked against the whole domain as you type.
                Untick a student to leave them out; their reserved address is released.
            </div>
            <div class="bx-tblwrap">
                <table class="bx-tbl"><thead><tr>
                    <th style="width:26px"><input type="checkbox" id="wizAll" checked onclick="wizToggleAll(this)" /></th>
                    <th>Student</th><th>Student No.</th><th>Surname</th><th>Other name</th><th>Yr</th>
                    <th style="min-width:230px">University address</th><th>Password</th><th>Org unit</th><th>Check</th>
                </tr></thead><tbody id="wizBody"></tbody></table>
            </div>
            <div class="se-empty" id="wizEmpty" style="display:none;">Nothing in this view.</div>
        </div>

        <!-- step 4 -->
        <div id="wizP4" style="display:none;">
            <div id="wizDone"></div>
        </div>
    </div>
    <div class="bx-f">
        <div style="font-size:11.5px;color:#64748b" id="wizFoot"></div>
        <div>
            <button type="button" class="bx-btn" id="wizBack" onclick="wizGo(-1)" style="display:none">Back</button>
            <button type="button" class="bx-btn bx-btn--p" id="wizNext" onclick="wizGo(1)">Continue</button>
        </div>
    </div>
</div>

<!-- ── Export modal ── -->
<div class="bx-modal bx-modal--sm" id="bxExp" role="dialog" aria-modal="true">
    <div class="bx-h"><span class="bx-h__t">Export to Google Workspace</span><button type="button" class="bx-h__x" onclick="bxClose()">&times;</button></div>
    <div class="bx-b">
        <div class="bx-msg" id="expMsg"></div>

        <div class="bx-fld">
            <label class="bx-fl">What to export</label>
            <label class="bx-chk"><input type="radio" name="expMode" value="pending" checked onchange="expSync()" />
                <span><b>New accounts to create</b> &mdash; pending students. Addresses are allocated now, reserved, and put in
                    the sheet with passwords. <b id="expNPending" class="bx-hint"></b></span></label>
            <label class="bx-chk"><input type="radio" name="expMode" value="awaiting" onchange="expSync()" />
                <span><b>Re-download a sheet already built</b> &mdash; allocated but not yet confirmed by Google. Same addresses,
                    same passwords. <b id="expNAwaiting" class="bx-hint"></b></span></label>
            <label class="bx-chk"><input type="radio" name="expMode" value="update" onchange="expSync()" />
                <span><b>Update accounts already in Google</b> &mdash; <b>Password column left blank</b> so nobody is locked out of
                    live mail. <b id="expNUpdate" class="bx-hint"></b></span></label>
        </div>
        <div class="bx-grid">
            <div class="bx-fld"><label class="bx-fl">Campus</label><select id="eCampus" class="bx-sel"><option value="">All</option></select></div>
            <div class="bx-fld"><label class="bx-fl">Intake year</label><input type="text" id="eYear" class="bx-in" placeholder="all" /></div>
            <div class="bx-fld">
                <label class="bx-fl">Org unit path</label>
                <input type="text" id="eOrg" class="bx-in" value="/" />
                <div class="bx-hint">Must <b>already exist</b> in Google &mdash; an upload never creates one, and a missing
                    org unit fails every row with <code>OU_INVALID</code>. <b>/</b> is the root and always works.</div>
            </div>
        </div>
        <div class="bx-hint" id="expReview" style="margin:-4px 0 10px">
            Allocation is automatic and collision-checked. If you would rather see the addresses and correct any before they
            go to Google, <a href="javascript:void(0)" onclick="expToWizard()" style="color:#174DA4;font-weight:700">review them first</a>.
        </div>
        <label class="bx-chk"><input type="checkbox" id="eChangePw" checked /> <span>Set “Change Password at Next Sign-In” for new accounts. <b>Leave this on</b> &mdash; it is what makes a shared password safe.</span></label>

        <div class="bx-msg bx-msg--info" style="display:block">
            The sheet carries only what Google needs to create the account &mdash; <b>first name, last name, address,
            password, org unit</b> &mdash; plus the student number in Employee ID, which is how the file is matched back on
            import. Every other column is left blank on purpose: each one was a way for the upload to fail on data the
            university already holds.
            <div style="margin-top:7px">Every new account is created with the house password <b>mru12345</b>, and Google is
            told to force a change at first sign-in. Students who have already been handed credentials keep the password
            they were given &mdash; theirs is not overwritten.</div>
        </div>
        <div class="bx-msg bx-msg--info" style="display:block;margin-top:10px">
            <b>What happens next.</b> Upload this file in the Google Admin console
            (<b>Directory &rarr; Users &rarr; Bulk update users</b>), then come back and
            <b>Import back from Google</b>. Nothing reaches a student until that import confirms the account exists —
            the addresses in this sheet are reserved, not issued. Employee ID carries the student number, which is how
            the sheet finds its way home.
        </div>
    </div>
    <div class="bx-f">
        <div style="font-size:11.5px;color:#64748b" id="expFoot"></div>
        <div>
            <button type="button" class="bx-btn" onclick="bxClose()">Cancel</button>
            <button type="button" class="bx-btn bx-btn--p" id="expBtn" onclick="expDownload()">Download sheet</button>
        </div>
    </div>
</div>

<!-- ── Import wizard ── -->
<div class="bx-modal" id="bxImp" role="dialog" aria-modal="true">
    <div class="bx-h"><span class="bx-h__t">Import from Google Workspace</span><button type="button" class="bx-h__x" onclick="impCancel()">&times;</button></div>
    <div class="bx-b">
        <div class="bx-msg" id="impMsg"></div>
        <div id="impP1">
            <div class="bx-drop" id="impDrop" onclick="document.getElementById('impFile').click()">
                <div class="bx-drop__t">Drop the Google export here, or click to choose</div>
                <div class="bx-drop__d">CSV on the Google Workspace template. In the Admin console use
                    <b>Users &rarr; Download users</b>, or upload a sheet built on this page.</div>
                <input type="file" id="impFile" accept=".csv,.tsv,.txt" style="display:none" onchange="impPick(this)" />
            </div>
            <div class="bx-msg bx-msg--info" style="display:block;margin-top:12px">
                Nothing is changed by uploading. The file is parsed, every row is matched to a student and classified,
                and you choose what to apply.
            </div>
        </div>
        <div id="impP2" style="display:none;">
            <div class="bx-stats" id="impStats"></div>
            <div class="bx-fld">
                <label class="bx-fl">Apply these</label>
                <label class="bx-chk"><input type="checkbox" id="iConfirm" checked /> <span><b>Confirm</b> — mark accounts as live in Google.</span></label>
                <label class="bx-chk"><input type="checkbox" id="iAdopt" checked /> <span><b>Adopt</b> — take an address Google has for a student who has none on file.</span></label>
                <label class="bx-chk"><input type="checkbox" id="iChange" /> <span><b>Address changed</b> — overwrite the system address with Google's. Leave off unless you know Google is right.</span></label>
                <label class="bx-chk"><input type="checkbox" id="iSuspend" checked /> <span><b>Suspended</b> — record accounts Google reports as suspended.</span></label>
                <label class="bx-chk"><input type="checkbox" id="iOrphan" checked /> <span><b>External accounts</b> — record addresses with no student, so they are never re-issued.</span></label>
            </div>
            <div class="bx-chips" id="impChips"></div>
            <div class="bx-tblwrap">
                <table class="bx-tbl"><thead><tr>
                    <th>Row</th><th>Name</th><th>Address in Google</th><th>Employee ID</th><th>Matched student</th><th>Org unit</th><th>Class</th><th>What it means</th>
                </tr></thead><tbody id="impBody"></tbody></table>
            </div>
            <div class="se-empty" id="impEmpty" style="display:none;">Nothing in this view.</div>
        </div>
        <div id="impP3" style="display:none;"><div id="impDone"></div></div>
    </div>
    <div class="bx-f">
        <div style="font-size:11.5px;color:#64748b" id="impFoot"></div>
        <div>
            <button type="button" class="bx-btn" onclick="impCancel()">Cancel</button>
            <button type="button" class="bx-btn bx-btn--p" id="impApply" onclick="impDoApply()" style="display:none">Apply selected</button>
        </div>
    </div>
</div>

<!-- ── Directory modal ── -->
<div class="bx-modal" id="bxDir" role="dialog" aria-modal="true">
    <div class="bx-h"><span class="bx-h__t">Address directory</span><button type="button" class="bx-h__x" onclick="bxClose()">&times;</button></div>
    <div class="bx-b">
        <div class="bx-stats" id="dirStats"></div>
        <div class="bx-sec" style="margin-top:4px">Addresses issued more than once</div>
        <div class="bx-hint" style="margin-bottom:8px">
            Found on student records, not created here. They are listed rather than repaired automatically —
            deciding who keeps an address is a human call. New allocations can no longer produce these.
        </div>
        <div class="bx-tblwrap" style="max-height:38vh">
            <table class="bx-tbl"><thead><tr><th>Address</th><th>Held by</th><th>Student numbers</th></tr></thead><tbody id="dirDup"></tbody></table>
        </div>
        <div class="se-empty" id="dirNoDup" style="display:none;">No duplicated addresses. </div>
    </div>
    <div class="bx-f"><span></span><button type="button" class="bx-btn" onclick="bxClose()">Close</button></div>
</div>

<!-- ── Batch detail modal ── -->
<div class="bx-modal" id="bxDet" role="dialog" aria-modal="true">
    <div class="bx-h"><span class="bx-h__t" id="detTitle">Batch</span><button type="button" class="bx-h__x" onclick="bxClose()">&times;</button></div>
    <div class="bx-b" id="detBody"></div>
    <div class="bx-f"><div id="detFoot" style="font-size:11.5px;color:#64748b"></div><button type="button" class="bx-btn" onclick="bxClose()">Close</button></div>
</div>

<script type="text/javascript">
(function () {
'use strict';
function qs(id) { return document.getElementById(id); }
function esc(s) { return s ? String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;') : ''; }
function fmt(n) { return (parseInt(n, 10) || 0).toLocaleString('en-US'); }
function ajax(m, p, cb) {
    var x = new XMLHttpRequest();
    x.open('POST', 'StudentEmailController.aspx/' + m, true);
    x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
    x.onload = function () {
        try { var o = JSON.parse(x.responseText); cb(typeof o.d === 'string' ? JSON.parse(o.d) : o.d); }
        catch (e) { cb({ success: false, message: 'The server returned an unexpected response.' }); }
    };
    x.onerror = function () { cb({ success: false, message: 'Network error — check the connection and try again.' }); };
    x.send(JSON.stringify(p || {}));
}
function msg(id, text, kind) {
    var e = qs(id); if (!e) return;
    if (!text) { e.style.display = 'none'; return; }
    e.className = 'bx-msg bx-msg--' + (kind || 'err'); e.textContent = text; e.style.display = 'block';
}
function show(id) { qs('bxOv').style.display = 'block'; qs(id).classList.add('show'); }
window.bxClose = function () {
    qs('bxOv').style.display = 'none';
    ['bxWiz', 'bxExp', 'bxImp', 'bxDir', 'bxDet'].forEach(function (i) { qs(i).classList.remove('show'); });
};
document.addEventListener('keydown', function (e) { if (e.key === 'Escape') bxClose(); });

// =====================================================================
//  Batch creation wizard
// =====================================================================
var wz = { step: 1, batchRef: '', rows: [], filter: 'ALL', excluded: {}, busy: false };

window.wizOpen = function () {
    wz = { step: 1, batchRef: '', rows: [], filter: 'ALL', excluded: {}, busy: false };
    msg('wizMsg', '');
    qs('wizP4').innerHTML = '';
    copyFilterOptions('wCampus', 'fCampus'); copyFilterOptions('wProg', 'fProg');
    qs('wPwMode').value = 'default'; qs('wPwFixed').value = ''; wizPwMode();
    wizPaint();
    show('bxWiz');
    wizEstimate();
};

// The wizard's campus/programme lists are the page's own, so they can never drift apart.
function copyFilterOptions(dstId, srcId) {
    var d = qs(dstId), s = qs(srcId);
    if (!d || !s || d.options.length > 1) return;
    for (var i = 1; i < s.options.length; i++) {
        var o = document.createElement('option');
        o.value = s.options[i].value; o.textContent = s.options[i].textContent;
        d.appendChild(o);
    }
}

function wizPaint() {
    for (var i = 1; i <= 4; i++) {
        var s = qs('wizS' + i);
        s.className = 'bx-step' + (i === wz.step ? ' on' : (i < wz.step ? ' done' : ''));
        qs('wizP' + i).style.display = (i === wz.step ? 'block' : 'none');
    }
    qs('wizBack').style.display = (wz.step > 1 && wz.step < 4) ? 'inline-block' : 'none';
    var n = qs('wizNext');
    n.style.display = wz.step < 4 ? 'inline-block' : 'none';
    n.textContent = wz.step === 1 ? 'Continue' : wz.step === 2 ? 'Build the list' : 'Reserve these addresses';
    qs('wizTitle').textContent = wz.step === 3 ? 'Review before anything is written' : 'Allocate addresses for the Google sheet';
}

function wizOptions() {
    return {
        scope: 'filter',
        stage: qs('wStage').value,
        campus: qs('wCampus').value,
        programme: qs('wProg').value,
        year: qs('wYear').value.trim(),
        q: qs('wQ').value.trim(),
        limit: parseInt(qs('wLimit').value, 10) || 500,
        otherLen: parseInt(qs('wOther').value, 10) || 3,
        domain: qs('wDomain').value.trim() || 'mru.ac.ug',
        nameOrder: qs('wOrder').value,
        orgUnit: qs('wOrg').value.trim() || '/Students/{year}',
        pwMode: qs('wPwMode').value,
        pwFixed: qs('wPwFixed').value,
        changePwNext: qs('wChangePw').checked,
        notify: false            // nobody is told anything until Google confirms
    };
}

window.wizPwMode = function () {
    var m = qs('wPwMode').value;
    qs('wPwFixed').style.display = m === 'fixed' ? 'block' : 'none';
    qs('wPwHint').innerHTML = m === 'default'
        ? 'Every account in this batch is created with <b>' + DEFAULT_PW + '</b> — the password the onboarding guide and the ICT desk already give out. Students change it the first time they sign in.'
        : (m === 'fixed'
            ? 'At least 8 characters — Google rejects anything shorter. Anyone handing out addresses has to be told this password separately.'
            : 'A different password for each student. Nobody can hand one out from memory — every student needs their own slip, or the credentials sheet.');
};

// Step 1 shows how many students the scope actually covers, using the same search the
// pipeline list uses — so the number on screen is the number that will be processed.
function wizEstimate() {
    var o = wizOptions();
    qs('wizCount').textContent = 'Counting…';
    ajax('Search', { q: o.q, stage: o.stage, campus: o.campus, programme: o.programme, year: o.year, verification: '', page: 1, pageSize: 1 },
    function (r) {
        if (!r || !r.success) { qs('wizCount').textContent = 'Could not count that scope.'; return; }
        var n = r.total, lim = o.limit;
        qs('wizCount').textContent = n === 0
            ? 'No students match this scope.'
            : (fmt(n) + ' student' + (n === 1 ? '' : 's') + ' match this scope' +
               (n > lim ? (' — this batch will take the first ' + fmt(lim) + '. Run it again for the rest.') : '.'));
    });
}
['wStage', 'wCampus', 'wProg', 'wYear', 'wQ', 'wLimit'].forEach(function (id) {
    var el = qs(id); if (!el) return;
    el.addEventListener('change', wizEstimate);
    el.addEventListener('keyup', function (e) { if (e.key === 'Enter') wizEstimate(); });
});

// Live example of the rule, so the format is understood before 500 addresses use it.
function wizExample() {
    var n = parseInt(qs('wOther').value, 10) || 3;
    var sample = wz.rows.length ? wz.rows[0] : null;
    var sur = sample ? sample.surname : 'AKAMPURIRA', oth = sample ? sample.given : 'VINCENT',
        yr = sample ? sample.year : '2026';
    if (qs('wOrder').value === 'FIRST_IS_SURNAME' && !sample) { sur = 'VINCENT'; oth = 'AKAMPURIRA'; }
    var slug = function (s) { return String(s || '').toLowerCase().replace(/[^a-z]/g, ''); };
    var first = String(oth || '').trim().split(/\s+/)[0] || '';
    var local = slug(sur) + slug(first).substring(0, n) + String(yr || '').replace(/\D/g, '').slice(-2);
    qs('egOut').textContent = local + '@' + (qs('wDomain').value.trim() || 'mru.ac.ug');
    qs('egWho').textContent = 'surname “' + (sur || '—') + '” + ' + n + ' letters of “' + (first || '—') + '” + year ' + (yr || '—') +
        (sample ? ' (' + sample.name + ')' : ' (example)');
}
['wOther', 'wOrder', 'wDomain'].forEach(function (id) {
    var el = qs(id); if (el) { el.addEventListener('input', wizExample); el.addEventListener('change', wizExample); }
});

window.wizGo = function (dir) {
    if (wz.busy) return;
    if (dir < 0) { wz.step = Math.max(1, wz.step - 1); wizPaint(); return; }
    if (wz.step === 1) { wz.step = 2; wizPaint(); wizExample(); return; }
    if (wz.step === 2) { wizBuild(); return; }
    if (wz.step === 3) { wizCommit(); return; }
};

function wizBuild() {
    var o = wizOptions();
    if (o.pwMode === 'fixed' && (o.pwFixed || '').trim().length < 8) { msg('wizMsg', 'A shared password must be at least 8 characters — Google rejects anything shorter.', 'err'); return; }
    wz.busy = true; msg('wizMsg', '');
    var n = qs('wizNext'); n.disabled = true; n.innerHTML = '<span class="bx-spin"></span>Allocating addresses…';
    ajax('BatchPreview', { options: JSON.stringify(o) }, function (r) {
        wz.busy = false; n.disabled = false;
        if (!r || !r.success) { wizPaint(); msg('wizMsg', (r && r.message) || 'Could not build the list.', 'err'); return; }
        wz.batchRef = r.batchRef; wz.rows = r.rows || []; wz.excluded = {}; wz.filter = 'ALL';
        // Rows that cannot be created are excluded up front — they are shown, but never applied.
        wz.rows.forEach(function (x) { if (x.severity === 'ERROR' || x.severity === 'SKIP') wz.excluded[x.regno] = 1; });
        wz.step = 3; wizPaint(); wizRender();
    });
}

// Renders from wz.rows alone, so re-filtering never needs another round trip.
function wizRender() {
    var counts = { ALL: wz.rows.length, OK: 0, WARN: 0, ERROR: 0, SKIP: 0 };
    wz.rows.forEach(function (x) { counts[x.severity] = (counts[x.severity] || 0) + 1; });
    qs('wizStats').innerHTML =
        stat(counts.ALL, 'In this batch', '') +
        stat(counts.OK, 'Clean', 'ok') +
        stat(counts.WARN, 'With a warning', 'warn') +
        stat(counts.ERROR, 'Cannot create', 'err') +
        stat(counts.SKIP, 'Already had one', '');
    var chips = [['ALL', 'All'], ['OK', 'Clean'], ['WARN', 'Warnings'], ['ERROR', 'Problems'], ['SKIP', 'Skipped']];
    qs('wizChips').innerHTML = chips.map(function (c) {
        return '<span class="bx-chip' + (wz.filter === c[0] ? ' on' : '') + '" onclick="wizFilter(\'' + c[0] + '\')">' +
               c[1] + ' <b>' + fmt(counts[c[0]] || 0) + '</b></span>';
    }).join('');
    wizRows();
    qs('wizFoot').innerHTML = 'Draft <b>' + esc(wz.batchRef) + '</b> — addresses reserved, nothing written yet.';
}
function stat(v, l, k) {
    return '<div class="bx-stat' + (k ? ' bx-stat--' + k : '') + '"><div class="bx-stat__v">' + fmt(v) + '</div><div class="bx-stat__l">' + l + '</div></div>';
}

window.wizFilter = function (f) { wz.filter = f; wizRender(); };

function wizRows() {
    var list = wz.rows.filter(function (x) { return wz.filter === 'ALL' || x.severity === wz.filter; });
    var b = qs('wizBody'); b.innerHTML = '';
    qs('wizEmpty').style.display = list.length ? 'none' : 'block';
    var html = list.slice(0, 800).map(function (x) {
        var off = wz.excluded[x.regno] ? ' class="off"' : '';
        var canEdit = x.severity !== 'SKIP';
        return '<tr' + off + ' id="wr_' + esc(x.regno) + '">' +
            '<td><input type="checkbox" ' + (wz.excluded[x.regno] ? '' : 'checked ') +
                'onclick="wizToggle(\'' + esc(x.regno) + '\',this)" /></td>' +
            '<td><strong>' + esc(x.name || '-') + '</strong></td>' +
            '<td>' + esc(x.regno) + '</td>' +
            '<td>' + esc(x.surname || '—') + '</td>' +
            '<td>' + esc(x.given || '—') + '</td>' +
            '<td>' + esc(x.year || '—') + '</td>' +
            '<td>' + (canEdit
                ? '<input class="bx-em" id="we_' + esc(x.regno) + '" value="' + esc(x.email) + '" ' +
                  'onchange="wizSetEmail(\'' + esc(x.regno) + '\')" onblur="wizSetEmail(\'' + esc(x.regno) + '\')" />'
                : '<span class="bx-pw">' + esc(x.email) + '</span>') + '</td>' +
            '<td class="bx-pw">' + esc(x.password || '—') + '</td>' +
            '<td class="bx-pw">' + esc(x.orgUnit || '—') + '</td>' +
            '<td><span class="bx-sev bx-sev--' + esc(x.severity) + '">' + esc(x.severity) + '</span>' +
                (x.message ? '<div class="bx-hint" style="max-width:230px">' + esc(x.message) + '</div>' : '') + '</td>' +
            '</tr>';
    }).join('');
    b.innerHTML = html;
    if (list.length > 800) b.insertAdjacentHTML('beforeend',
        '<tr><td colspan="10" style="text-align:center;color:#94a3b8;padding:10px">Showing the first 800 of ' + fmt(list.length) + ' — use the filters above.</td></tr>');
}

window.wizToggle = function (regno, cb) {
    if (cb.checked) delete wz.excluded[regno]; else wz.excluded[regno] = 1;
    var tr = qs('wr_' + regno); if (tr) tr.className = cb.checked ? '' : 'off';
    wizCountFoot();
};
window.wizToggleAll = function (cb) {
    var list = wz.rows.filter(function (x) { return wz.filter === 'ALL' || x.severity === wz.filter; });
    list.forEach(function (x) {
        if (x.severity === 'ERROR' || x.severity === 'SKIP') return;   // never selectable
        if (cb.checked) delete wz.excluded[x.regno]; else wz.excluded[x.regno] = 1;
    });
    wizRows(); wizCountFoot();
};
function wizCountFoot() {
    var n = wz.rows.filter(function (x) { return !wz.excluded[x.regno]; }).length;
    qs('wizFoot').innerHTML = 'Draft <b>' + esc(wz.batchRef) + '</b> — <b>' + fmt(n) + '</b> selected for creation.';
}

// Editing an address goes straight back to the server, which re-checks it against every
// address on the domain and moves the reservation. A red box means it was refused.
window.wizSetEmail = function (regno) {
    var el = qs('we_' + regno); if (!el) return;
    var row = null;
    for (var i = 0; i < wz.rows.length; i++) if (wz.rows[i].regno === regno) { row = wz.rows[i]; break; }
    var v = (el.value || '').trim().toLowerCase();
    if (!row || v === row.email) { el.className = 'bx-em'; return; }
    el.disabled = true;
    ajax('BatchSetEmail', { batchRef: wz.batchRef, regno: regno, email: v }, function (r) {
        el.disabled = false;
        if (r && r.success) { row.email = r.email; el.value = r.email; el.className = 'bx-em good'; msg('wizMsg', ''); }
        else { el.className = 'bx-em bad'; el.value = row.email; msg('wizMsg', (r && r.message) || 'That address was refused.', 'err'); }
    });
};

function wizCommit() {
    var keep = wz.rows.filter(function (x) { return !wz.excluded[x.regno]; });
    if (keep.length === 0) { msg('wizMsg', 'Nothing is selected.', 'err'); return; }
    if (!confirm('Reserve ' + keep.length + ' address(es) for the Google sheet?\n\nThey are recorded and locked so nobody else can be given them. No student is told anything until the Google export is imported back.')) return;
    wz.busy = true;
    var n = qs('wizNext'); n.disabled = true; n.innerHTML = '<span class="bx-spin"></span>Reserving…';
    var exclude = Object.keys(wz.excluded);
    ajax('BatchCommit', { batchRef: wz.batchRef, exclude: JSON.stringify(exclude) }, function (r) {
        wz.busy = false; n.disabled = false;
        if (!r || !r.success) { wizPaint(); msg('wizMsg', (r && r.message) || 'The batch could not be applied.', 'err'); return; }
        wz.step = 4; wizPaint();
        var fails = (r.failures || []).map(function (f) { return '<li>' + esc(f.regno) + ' — ' + esc(f.message) + '</li>'; }).join('');
        qs('wizDone').innerHTML =
            '<div class="bx-msg bx-msg--' + (r.failed ? 'warn' : 'ok') + '" style="display:block">' + esc(r.message) + '</div>' +
            '<div class="bx-stats">' + stat(r.created, 'Reserved', 'ok') + stat(r.skipped, 'Skipped', '') + stat(r.failed, 'Failed', r.failed ? 'err' : '') + '</div>' +
            (fails ? '<div class="bx-sec">Rows that failed</div><ul style="font-size:12px;color:#b91c1c;margin:0 0 12px 18px">' + fails + '</ul>' : '') +
            '<div class="bx-sec">Next step</div>' +
            '<div class="bx-hint" style="margin-bottom:10px">Download the Google sheet, upload it in the Admin console, then bring the Google export back here. <b>That import is what gives each student their address</b> and notifies them.</div>' +
            '<button type="button" class="bx-btn bx-btn--p" onclick="dl(\'export\',{batchRef:\'' + esc(wz.batchRef) + '\',mode:\'create\'},\'wizMsg\')">Google sheet for this batch</button>' +
            '<button type="button" class="bx-btn" onclick="dl(\'credentials\',{batchRef:\'' + esc(wz.batchRef) + '\'},\'wizMsg\')">Credentials sheet</button>' +
            '<button type="button" class="bx-btn" onclick="bxClose();location.reload();">Done</button>';
        qs('wizFoot').innerHTML = 'Batch <b>' + esc(wz.batchRef) + '</b> — ' + esc(r.status);
        bxLoadBatches();
    });
}

// Closing an un-applied draft frees every address it was holding, so an abandoned wizard
// never quietly locks names away.
window.wizCancel = function () {
    if (wz.batchRef && wz.step === 3) {
        if (!confirm('Discard this draft? The addresses it reserved will be released.')) return;
        ajax('BatchCancel', { batchRef: wz.batchRef }, function () { bxClose(); bxLoadBatches(); });
        return;
    }
    bxClose();
};

// =====================================================================
//  Export
// =====================================================================
var expCounts = null;

window.expOpen = function () {
    msg('expMsg', '');
    copyFilterOptions('eCampus', 'fCampus');
    expCounts = null;
    qs('expFoot').textContent = 'Counting…';
    qs('expBtn').disabled = true;
    show('bxExp');
    ajax('ExportCount', { scope: '{}' }, function (r) {
        if (!r || !r.success) { qs('expFoot').textContent = ''; msg('expMsg', (r && r.message) || 'Could not read the pipeline.', 'err'); return; }
        expCounts = r;
        qs('expNPending').textContent = fmt(r.pending) + ' student(s)';
        qs('expNAwaiting').textContent = fmt(r.awaiting) + ' student(s)';
        qs('expNUpdate').textContent = fmt(r.update) + ' student(s)';
        // Default to whichever step the intake is actually at, so the first click works.
        if (r.pending === 0) setExpMode(r.awaiting > 0 ? 'awaiting' : 'update');
        expSync();
    });
};
function setExpMode(v) {
    var rs = document.getElementsByName('expMode');
    for (var i = 0; i < rs.length; i++) rs[i].checked = (rs[i].value === v);
}
function expMode() {
    var rs = document.getElementsByName('expMode');
    for (var i = 0; i < rs.length; i++) if (rs[i].checked) return rs[i].value;
    return 'create';
}

// An empty selection is stated up front and the button is disabled, rather than letting
// the download run and hand back a file with nothing in it.
window.expSync = function () {
    if (!expCounts) return;
    var m = expMode();
    var n = m === 'pending' ? expCounts.pending : (m === 'awaiting' ? expCounts.awaiting : expCounts.update);
    qs('expBtn').disabled = (n === 0);
    qs('expBtn').textContent = m === 'pending' ? 'Allocate & download sheet' : 'Download sheet';
    if (n === 0) {
        qs('expFoot').textContent = 'No students at this step.';
        msg('expMsg', m === 'pending'
            ? 'No pending students — every generated student already has an address allocated.'
            : (m === 'awaiting'
                ? 'Nothing is waiting on Google. Start with “New accounts to create”.'
                : 'No accounts are confirmed in Google yet — import a Google export first.'), 'warn');
    } else {
        qs('expFoot').innerHTML = '<b>' + fmt(n) + '</b> student(s) will be in the sheet.';
        msg('expMsg', m === 'pending'
            ? 'Addresses are allocated when you download. They are reserved immediately, and stay unissued until a Google import confirms them.'
            : '', m === 'pending' ? 'info' : 'err');
    }
};

// The optional review path: same allocation, shown before it is committed, with every
// address editable. Ends in the same sheet.
window.expToWizard = function () { bxClose(); wizOpen(); };

window.expDownload = function () {
    if (qs('expBtn').disabled) return;
    var m = expMode();
    if (m === 'pending' && !confirm('Allocate addresses for ' + fmt(expCounts.pending) + ' pending student(s) and download the Google sheet?\n\nThe addresses are reserved so nobody else can be given them. No student is told anything until you import the Google export back.')) return;
    dl('export', { mode: m, campus: qs('eCampus').value, year: qs('eYear').value.trim(),
                   orgUnit: qs('eOrg').value.trim() || '/', changePwNext: qs('eChangePw').checked }, 'expMsg');
    if (m === 'pending') setTimeout(bxLoadBatches, 2500);
};

// Downloads go through the file handler — a PageMethod cannot stream a file.
//
// Fetched as a blob rather than by navigating the browser. Navigating means EVERY failure
// — an expired session, an empty selection, a server fault — arrives as a downloaded file,
// which is how {"success":false,"message":"..."} ended up being opened in Excel. Here the
// response is inspected first: JSON is shown as a message, only a real sheet is saved.
window.dl = function (action, params, msgId) {
    msgId = msgId || 'bxMsg';
    var u = 'SemsFile.ashx?action=' + encodeURIComponent(action);
    params = params || {};
    for (var k in params) if (params.hasOwnProperty(k) && params[k] !== '' && params[k] != null)
        u += '&' + encodeURIComponent(k) + '=' + encodeURIComponent(params[k]);

    var target = msgId;
    msg(target, 'Building the sheet…', 'info');

    var x = new XMLHttpRequest();
    x.open('GET', u, true);
    x.responseType = 'blob';
    x.onload = function () {
        var type = (x.getResponseHeader('Content-Type') || '').toLowerCase();
        if (x.status !== 200 || type.indexOf('json') >= 0) {
            // Read the JSON back out of the blob so the reason reaches the screen.
            var fr = new FileReader();
            fr.onload = function () {
                var m = 'The sheet could not be built.';
                try { var o = JSON.parse(fr.result); if (o && o.message) m = o.message; } catch (e) { }
                if (x.status === 403) m = 'Your session has expired — sign in again and retry.';
                msg(target, m, 'err');
            };
            fr.onerror = function () { msg(target, 'The sheet could not be built.', 'err'); };
            fr.readAsText(x.response);
            return;
        }
        // Filename from the handler, so the sheet is named for what it contains.
        var name = 'export.csv';
        var cd = x.getResponseHeader('Content-Disposition') || '';
        var m2 = /filename="?([^";]+)"?/i.exec(cd);
        if (m2) name = m2[1];
        var url = URL.createObjectURL(x.response);
        var a = document.createElement('a');
        a.href = url; a.download = name;
        document.body.appendChild(a); a.click(); document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 4000);
        msg(target, 'Downloaded ' + name, 'ok');
    };
    x.onerror = function () { msg(target, 'Network error — the sheet was not downloaded.', 'err'); };
    x.send();
};

// =====================================================================
//  Import
// =====================================================================
var im = { ref: '', filter: 'ALL', counts: {}, busy: false };

window.impOpen = function () {
    im = { ref: '', filter: 'ALL', counts: {}, busy: false };
    msg('impMsg', '');
    qs('impP1').style.display = 'block'; qs('impP2').style.display = 'none'; qs('impP3').style.display = 'none';
    qs('impApply').style.display = 'none'; qs('impFoot').textContent = '';
    qs('impFile').value = '';
    show('bxImp');
};

(function wireDrop() {
    var d = qs('impDrop'); if (!d) return;
    ['dragenter', 'dragover'].forEach(function (e) {
        d.addEventListener(e, function (ev) { ev.preventDefault(); ev.stopPropagation(); d.classList.add('over'); });
    });
    ['dragleave', 'drop'].forEach(function (e) {
        d.addEventListener(e, function (ev) { ev.preventDefault(); ev.stopPropagation(); d.classList.remove('over'); });
    });
    d.addEventListener('drop', function (ev) {
        if (ev.dataTransfer && ev.dataTransfer.files && ev.dataTransfer.files.length) impSend(ev.dataTransfer.files[0]);
    });
})();

window.impPick = function (input) { if (input.files && input.files.length) impSend(input.files[0]); };

function impSend(file) {
    if (im.busy) return;
    im.busy = true;
    msg('impMsg', 'Reading ' + file.name + '…', 'info');
    var fd = new FormData();
    fd.append('action', 'import');
    fd.append('file', file);
    var x = new XMLHttpRequest();
    x.open('POST', 'SemsFile.ashx?action=import', true);
    x.onload = function () {
        im.busy = false;
        var r; try { r = JSON.parse(x.responseText); } catch (e) { r = { success: false, message: 'The server returned an unexpected response.' }; }
        if (!r.success) { msg('impMsg', r.message || 'The file could not be read.', 'err'); return; }
        im.ref = r.importRef; im.counts = r; im.filter = 'ALL';
        msg('impMsg', '');
        qs('impP1').style.display = 'none'; qs('impP2').style.display = 'block';
        qs('impApply').style.display = 'inline-block';
        qs('impStats').innerHTML =
            stat(r.total, 'Rows in file', '') +
            stat(r.confirm, 'Confirm', 'ok') +
            stat(r.adopt, 'Adopt', 'warn') +
            stat(r.change, 'Address changed', 'warn') +
            stat(r.suspend, 'Suspended', 'warn') +
            stat(r.orphan, 'No student', 'warn') +
            ((r.failed || 0) ? stat(r.failed, 'Refused by Google', 'err') : '') +
            stat(r.error, 'Errors', r.error ? 'err' : '');
        // Google's own reason for each refusal, grouped — "you ran out of licences" is a
        // different problem from "that address already exists", and the operator needs to know which.
        var why = (r.reasons || []).map(function (x) { return esc(x.reason) + ' × ' + fmt(x.count); }).join(' · ');
        qs('impFoot').innerHTML = esc(r.fileName || 'sheet') + ' — '
            + (r.format ? esc(r.format) : (r.columns + ' columns, ' + (r.delimiter === 'tab' ? 'tab' : 'comma') + '-separated'))
            + '. Import <b>' + esc(r.importRef) + '</b>'
            + (why ? '<br><span style="color:#b91c1c">' + why + '</span>' : '');
        impChips(); impRows();
    };
    x.onerror = function () { im.busy = false; msg('impMsg', 'Upload failed — check the connection.', 'err'); };
    x.send(fd);
}

function impChips() {
    var c = im.counts;
    var chips = [['ALL', 'All', c.total], ['CONFIRM', 'Confirm', c.confirm], ['ADOPT', 'Adopt', c.adopt],
                 ['UPDATE_EMAIL', 'Changed', c.change], ['SUSPEND', 'Suspended', c.suspend],
                 ['ORPHAN', 'No student', c.orphan], ['FAILED', 'Refused by Google', c.failed || 0],
                 ['ERROR', 'Errors', c.error]];
    qs('impChips').innerHTML = chips.map(function (x) {
        return '<span class="bx-chip' + (im.filter === x[0] ? ' on' : '') + '" onclick="impFilter(\'' + x[0] + '\')">' +
               x[1] + ' <b>' + fmt(x[2] || 0) + '</b></span>';
    }).join('');
}
window.impFilter = function (f) { im.filter = f; impChips(); impRows(); };

function impRows() {
    ajax('ImportRows', { importRef: im.ref, action: im.filter === 'ALL' ? '' : im.filter, page: 1, pageSize: 300 }, function (r) {
        var b = qs('impBody'); b.innerHTML = '';
        if (!r || !r.success) { msg('impMsg', (r && r.message) || 'Could not read the parsed rows.', 'err'); return; }
        qs('impEmpty').style.display = r.rows.length ? 'none' : 'block';
        b.innerHTML = r.rows.map(function (x) {
            var sev = x.severity === 'ERROR' ? 'ERROR' : (x.action === 'CONFIRM' ? 'OK' : 'WARN');
            return '<tr>' +
                '<td>' + x.rowNo + '</td>' +
                '<td>' + esc(((x.first || '') + ' ' + (x.last || '')).trim() || '—') + '</td>' +
                '<td class="bx-pw">' + esc(x.newPrimary || x.email || '—') + '</td>' +
                '<td>' + esc(x.employeeId || '—') + '</td>' +
                '<td>' + (x.regno ? esc(x.regno) + ' <span class="bx-hint">(' + esc(x.matchType.toLowerCase()) + ')</span>' : '<span style="color:#cbd5e1">—</span>') + '</td>' +
                '<td class="bx-pw">' + esc(x.orgUnit || '—') + '</td>' +
                '<td><span class="bx-sev bx-sev--' + sev + '">' + esc((x.action || '').replace('_', ' ')) + '</span></td>' +
                '<td class="bx-hint" style="max-width:280px">' + esc(x.message || '') + (x.applied ? ' <b>· applied</b>' : '') + '</td>' +
                '</tr>';
        }).join('');
        if (r.total > r.rows.length) b.insertAdjacentHTML('beforeend',
            '<tr><td colspan="8" style="text-align:center;color:#94a3b8;padding:10px">Showing ' + fmt(r.rows.length) + ' of ' + fmt(r.total) + ' rows in this class.</td></tr>');
    });
}

window.impDoApply = function () {
    if (im.busy) return;
    var o = {
        confirm: qs('iConfirm').checked, adopt: qs('iAdopt').checked, changeEmail: qs('iChange').checked,
        suspend: qs('iSuspend').checked, orphan: qs('iOrphan').checked
    };
    if (!o.confirm && !o.adopt && !o.changeEmail && !o.suspend && !o.orphan) { msg('impMsg', 'Tick at least one class to apply.', 'err'); return; }
    if (o.changeEmail && !confirm('Overwrite system addresses with the addresses in this file?\n\nOnly do this when you know Google is the correct side.')) return;
    im.busy = true;
    var b = qs('impApply'); b.disabled = true; b.innerHTML = '<span class="bx-spin"></span>Applying…';
    ajax('ImportApply', { importRef: im.ref, options: JSON.stringify(o) }, function (r) {
        im.busy = false; b.disabled = false; b.textContent = 'Apply selected';
        if (!r || !r.success) { msg('impMsg', (r && r.message) || 'The import could not be applied.', 'err'); return; }
        qs('impP2').style.display = 'none'; qs('impP3').style.display = 'block'; b.style.display = 'none';
        var fails = (r.failures || []).map(function (f) { return '<li>' + esc(f.regno || f.email) + ' — ' + esc(f.message) + '</li>'; }).join('');
        qs('impDone').innerHTML =
            '<div class="bx-msg bx-msg--' + (r.failed ? 'warn' : 'ok') + '" style="display:block">' + esc(r.message) + '</div>' +
            '<div class="bx-stats">' + stat(r.confirmed, 'Confirmed', 'ok') + stat(r.adopted, 'Adopted', 'ok') +
            stat(r.changed, 'Addresses changed', 'warn') + stat(r.suspended, 'Suspended', 'warn') +
            stat(r.orphans, 'External recorded', '') + stat(r.failed, 'Failed', r.failed ? 'err' : '') + '</div>' +
            (fails ? '<div class="bx-sec">Rows that failed</div><ul style="font-size:12px;color:#b91c1c;margin:0 0 12px 18px">' + fails + '</ul>' : '') +
            '<button type="button" class="bx-btn bx-btn--p" onclick="bxClose();location.reload();">Done</button>';
        bxLoadBatches();
    });
};

window.impCancel = function () {
    if (im.ref && qs('impP2').style.display === 'block') {
        ajax('ImportDiscard', { importRef: im.ref }, function () { bxClose(); bxLoadBatches(); });
        return;
    }
    bxClose();
};

// =====================================================================
//  Directory + batch history
// =====================================================================
window.dirOpen = function () {
    qs('dirStats').innerHTML = '<div class="bx-hint">Loading…</div>';
    qs('dirDup').innerHTML = '';
    show('bxDir');
    ajax('DirectoryStats', {}, function (r) {
        if (!r || !r.success) { qs('dirStats').innerHTML = '<div class="bx-msg bx-msg--err" style="display:block">' + esc((r && r.message) || 'Could not load') + '</div>'; return; }
        qs('dirStats').innerHTML =
            stat(r.total, 'Addresses known', '') + stat(r.students, 'Students', 'ok') + stat(r.staff, 'Staff', '') +
            stat(r.google, 'Seen in Google', '') + stat(r.reserved, 'Reserved by drafts', 'warn') + stat(r.system, 'System reserved', '');
        var d = r.duplicates || [];
        qs('dirNoDup').style.display = d.length ? 'none' : 'block';
        qs('dirDup').innerHTML = d.map(function (x) {
            return '<tr><td class="bx-pw">' + esc(x.email) + '</td><td><span class="bx-sev bx-sev--ERROR">' + x.count + ' students</span></td><td>' + esc(x.regnos) + '</td></tr>';
        }).join('');
    });
};

// Gives back every address Google never created. Confirmed accounts are untouched — those are
// live mailboxes — and the surrendered address is written to the activity log first, so a
// cleared field is never a lost record.
window.releaseUnconfirmed = function () {
    var n = qs('flowCreate') ? qs('flowCreate').textContent : '?';
    if (!confirm('Release the ' + n + ' address(es) that were exported but never confirmed by Google?\n\n'
        + 'Those students go back to Pending with no address and no password. Accounts Google DID create are not touched.\n\n'
        + 'Re-export when you are ready to try again.')) return;
    msg('bxMsg', 'Releasing…', 'info');
    ajax('ReleaseUnconfirmed', { note: 'released from the console' }, function (r) {
        if (r && r.success) { msg('bxMsg', r.message, 'ok'); bxLoadBatches(); }
        else msg('bxMsg', (r && r.message) || 'Could not release them.', 'err');
    });
};

window.bxLoadBatches = function () {
    // The pipeline strip first — it is the thing that tells the operator what to do next.
    ajax('ExportCount', { scope: '{}' }, function (r) {
        if (!r || !r.success) return;
        qs('flowPending').textContent = fmt(r.pending);
        qs('flowCreate').textContent = fmt(r.awaiting);
        qs('flowGoogle').textContent = fmt(r.update);
    });
    ajax('BatchList', { limit: 30 }, function (r) {
        var b = qs('bBody'); if (!b) return;
        b.innerHTML = '';
        if (!r || !r.success) return;
        qs('bEmpty').style.display = r.batches.length ? 'none' : 'block';
        b.innerHTML = r.batches.map(function (x) {
            var cls = x.status === 'APPLIED' ? 'done' : x.status === 'DRAFT' ? 'pending' : x.status === 'CANCELLED' ? 'muted' : 'ready';
            return '<tr>' +
                '<td><strong>' + esc(x.batchRef) + '</strong></td>' +
                '<td>' + esc(x.type) + '</td>' +
                '<td><span class="se-badge se-b--' + cls + '">' + esc(x.status) + '</span></td>' +
                '<td>' + fmt(x.total) + '</td><td>' + fmt(x.ok) + '</td><td>' + fmt(x.skipped) + '</td>' +
                '<td>' + (x.failed ? '<b style="color:#b91c1c">' + fmt(x.failed) + '</b>' : '0') + '</td>' +
                '<td>' + esc(x.who) + '</td><td style="color:#94a3b8">' + esc(x.at) + '</td>' +
                '<td style="white-space:nowrap"><span class="se-act" onclick="detOpen(\'' + esc(x.batchRef) + '\')">Open</span>' +
                (x.type === 'CREATE' && x.ok ? ' · <span class="se-act" onclick="dl(\'export\',{batchRef:\'' + esc(x.batchRef) + '\',mode:\'create\'})">Sheet</span>' : '') +
                '</td></tr>';
        }).join('');
    });
};

window.detOpen = function (ref) {
    qs('detTitle').textContent = 'Batch ' + ref;
    qs('detBody').innerHTML = '<div class="bx-hint">Loading…</div>';
    show('bxDet');
    ajax('BatchDetail', { batchRef: ref, limit: 800 }, function (r) {
        if (!r || !r.success) { qs('detBody').innerHTML = '<div class="bx-msg bx-msg--err" style="display:block">' + esc((r && r.message) || 'Could not load') + '</div>'; return; }
        var h = '<div class="bx-stats">' + stat(r.batch.total, 'Rows', '') + stat(r.batch.ok, 'Applied', 'ok') +
                stat(r.batch.skipped, 'Skipped', '') + stat(r.batch.failed, 'Failed', r.batch.failed ? 'err' : '') + '</div>';
        h += '<div class="bx-tblwrap" style="max-height:56vh"><table class="bx-tbl"><thead><tr><th>#</th><th>Student</th><th>Student No.</th><th>Address</th><th>Password</th><th>Action</th><th>Result</th><th>Note</th></tr></thead><tbody>';
        h += (r.rows || []).map(function (x) {
            var cls = x.result === 'OK' ? 'OK' : x.result === 'FAILED' ? 'ERROR' : x.result === 'PENDING' ? 'WARN' : 'SKIP';
            return '<tr><td>' + x.rowNo + '</td><td>' + esc(x.name || '—') + '</td><td>' + esc(x.regno) + '</td>' +
                '<td class="bx-pw">' + esc(x.email || '—') + '</td><td class="bx-pw">' + esc(x.password || '—') + '</td>' +
                '<td>' + esc(x.action) + '</td><td><span class="bx-sev bx-sev--' + cls + '">' + esc(x.result) + '</span></td>' +
                '<td class="bx-hint" style="max-width:260px">' + esc(x.message || '') + '</td></tr>';
        }).join('');
        h += '</tbody></table></div>';
        qs('detBody').innerHTML = h;
        qs('detFoot').innerHTML = esc(r.batch.type) + ' · ' + esc(r.batch.status) + ' · by ' + esc(r.batch.who) + ' on ' + esc(r.batch.at);
    });
};
})();
</script>
</asp:Content>
