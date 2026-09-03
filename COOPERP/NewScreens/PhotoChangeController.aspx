<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="PhotoChangeController.aspx.cs" Inherits="COOPERP_NewScreens_PhotoChangeController" Title="Official Photograph Approvals - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
.pc-wrap{padding:6px 2px 40px;}
.pc-head h1{font-size:20px;font-weight:700;color:#05275C;margin:0 0 3px;}
.pc-head p{font-size:12.5px;color:#5b6472;margin:0 0 16px;}
.pc-bar{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:12px;}
.pc-tabs{display:flex;gap:6px;flex-wrap:wrap;}
.pc-tab{font-size:12px;font-weight:700;color:#5b6472;text-decoration:none;padding:7px 13px;border:1px solid #e0e5ed;background:#fff;white-space:nowrap;}
.pc-tab em{font-style:normal;color:#9aa4b5;font-weight:600;}
.pc-tab:hover{border-color:#174DA4;color:#174DA4;}
.pc-tab--on{background:#05275C;border-color:#05275C;color:#fff;} .pc-tab--on em{color:#c9d5ea;}
.pc-search{display:flex;align-items:center;gap:6px;}
.pc-search__in{border:1px solid #e0e5ed;padding:7px 10px;font-size:12px;font-family:inherit;min-width:210px;}
.pc-clear{font-size:11px;color:#8b93a3;text-decoration:none;}
.pc-btn{border:0;padding:8px 14px;font-size:12px;font-weight:700;cursor:pointer;font-family:inherit;background:#eef1f6;color:#05275C;}
.pc-btn--sm{padding:7px 12px;}
.pc-btn--ok{background:#1c7a45;color:#fff;} .pc-btn--ok:hover{background:#166534;}
.pc-btn--danger{background:#b3261e;color:#fff;} .pc-btn--danger:hover{background:#8f1c16;}
.pc-btn--del{background:#fff;color:#8b93a3;border:1px solid #e0e5ed;} .pc-btn--del:hover{background:#fff6f6;color:#b3261e;border-color:#f0b4b4;}
.pc-btn:disabled{opacity:.5;cursor:not-allowed;}
.pc-batch{display:flex;align-items:center;gap:10px;background:#f5f7fa;border:1px solid #e0e5ed;padding:9px 12px;margin-bottom:14px;flex-wrap:wrap;}
.pc-selall{font-size:12px;color:#3a4250;font-weight:600;display:flex;align-items:center;gap:6px;cursor:pointer;}
.pc-batch__spacer{flex:1;}
.pc-selcount{font-size:11px;color:#8b93a3;}
.pc-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:14px;}
.pc-card{border:1px solid #e0e5ed;background:#fff;display:flex;flex-direction:column;}
.pc-card__h{display:flex;align-items:center;gap:8px;padding:10px 12px;border-bottom:1px solid #eef1f6;}
.pc-card__id{min-width:0;flex:1;display:flex;flex-direction:column;line-height:1.25;}
.pc-card__id b{font-size:12.5px;color:#1a1a2e;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.pc-card__id span{font-size:10.5px;color:#174DA4;font-weight:600;}
.pc-st{font-size:9px;font-weight:800;text-transform:uppercase;letter-spacing:.3px;padding:2px 7px;white-space:nowrap;}
.pc-st--pending{background:#fff8e1;color:#7a5c00;} .pc-st--approved{background:#e9f7ef;color:#1c7a45;}
.pc-st--rejected{background:#fdecec;color:#b3261e;} .pc-st--deleted{background:#eef1f6;color:#5b6472;}
.pc-st--banned{background:#5b0d0d;color:#fff;} .pc-st--unbanned{background:#e9f7ef;color:#1c7a45;}
.pc-st--live{background:#e6f0ff;color:#174DA4;}
.pc-badges{display:flex;flex-wrap:wrap;gap:4px;align-items:center;}
.pc-card--banned{border-color:#f0b4b4;}
.pc-imgs--one .pc-img--new{width:110px;height:147px;}
.pc-clickimg{cursor:zoom-in;}
.pc-banchk{display:flex;gap:8px;align-items:flex-start;margin-top:10px;padding:9px 11px;background:#fdecec;border:1px solid #f4c2c2;font-size:12px;color:#7a1f1a;cursor:pointer;}
.pc-banchk input{width:15px;height:15px;margin-top:1px;accent-color:#b3261e;flex:0 0 auto;}
.pc-lightbox{display:none;position:fixed;inset:0;background:rgba(0,0,0,.82);z-index:100000;align-items:center;justify-content:center;padding:24px;cursor:zoom-out;}
.pc-lightbox img{max-width:92vw;max-height:92vh;border:3px solid #fff;box-shadow:0 20px 60px rgba(0,0,0,.5);}
.pc-lightbox__x{position:absolute;top:14px;right:22px;color:#fff;font-size:34px;line-height:1;cursor:pointer;font-weight:300;}
.pc-imgs{display:flex;gap:8px;padding:12px;justify-content:center;background:#fafbfc;}
.pc-img{position:relative;background:#eef1f6;border:1px solid #e0e5ed;overflow:hidden;}
.pc-img--new{width:132px;height:176px;} .pc-img--old{width:78px;height:104px;align-self:flex-end;opacity:.85;}
.pc-img img{width:100%;height:100%;object-fit:cover;display:block;}
.pc-img__none{width:100%;height:100%;display:flex;align-items:center;justify-content:center;font-size:10px;color:#9aa4b5;text-align:center;}
.pc-img__lbl{position:absolute;bottom:0;left:0;right:0;background:rgba(5,39,92,.72);color:#fff;font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;text-align:center;padding:2px;}
.pc-meta{font-size:10.5px;color:#6b7280;padding:3px 12px;line-height:1.45;}
.pc-meta i{color:#8b6f00;}
.pc-acts{display:flex;gap:8px;padding:10px 12px;border-top:1px solid #eef1f6;margin-top:auto;}
.pc-acts .pc-btn{flex:1;}
.pc-del{padding:0 12px 11px;}
.pc-del a{display:inline-flex;align-items:center;font-size:11px;font-weight:600;color:#9aa4b5;text-decoration:none;}
.pc-del a:hover{color:#b3261e;}
.pc-empty{text-align:center;padding:44px 20px;color:#8b93a3;font-size:13px;background:#fff;border:1px solid #e0e5ed;}
.pc-pager{display:flex;align-items:center;justify-content:space-between;margin-top:14px;font-size:11px;color:#6b7280;}
.pc-pg{border:1px solid #e0e5ed;background:#fff;padding:5px 11px;font-size:11px;font-weight:700;color:#05275C;text-decoration:none;}
.pc-pg.off{opacity:.4;pointer-events:none;}
.pc-toast{position:fixed;bottom:22px;left:50%;transform:translateX(-50%);background:#05275C;color:#fff;padding:11px 18px;font-size:12.5px;font-weight:600;z-index:9999;box-shadow:0 6px 22px rgba(5,39,92,.3);display:none;}
.pc-toast--err{background:#b3261e;}
.pc-bar__right{display:flex;gap:8px;align-items:center;flex-wrap:wrap;}
.pc-btn--nav{background:#05275C;color:#fff;} .pc-btn--nav:hover{background:#0a3a82;}
/* admin set-status modal */
.pc-mov{position:fixed;inset:0;background:rgba(5,39,92,.5);z-index:9998;display:none;align-items:flex-start;justify-content:center;padding:50px 16px;overflow:auto;}
.pc-modal{background:#fff;max-width:440px;width:100%;box-shadow:0 18px 50px rgba(5,39,92,.3);}
.pc-modal__h{display:flex;align-items:center;justify-content:space-between;padding:13px 18px;background:#05275C;color:#fff;}
.pc-modal__h b{font-size:14px;font-weight:700;}
.pc-modal__x{background:none;border:0;color:#fff;font-size:22px;line-height:1;cursor:pointer;}
.pc-modal__b{padding:18px;}
.pc-fld{margin-bottom:14px;}
.pc-fld label{display:block;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;color:#5b6472;margin-bottom:5px;}
.pc-fld input,.pc-fld select,.pc-fld textarea{width:100%;box-sizing:border-box;border:1px solid #e0e5ed;padding:9px 11px;font-size:13px;font-family:inherit;}
.pc-fld textarea{min-height:64px;resize:vertical;}
.pc-hint{font-size:11px;color:#8b93a3;margin-top:5px;line-height:1.45;}
.pc-modal__f{display:flex;gap:8px;justify-content:flex-end;padding:12px 18px;background:#f8f9fb;border-top:1px solid #e0e5ed;}
/* ── Admin upload-for-a-student wizard ──────────────────────────────────────
   Two steps on purpose: find the person, THEN send the file. See the header
   comment on HandleAdminUpload for why the confirmation step is not optional. */
.pc-btn--up{background:#1c7a45;color:#fff;} .pc-btn--up:hover{background:#166534;}
.pc-modal--up{max-width:560px;}
.pc-step{display:none;} .pc-step--on{display:block;}
/* the student we are about to change, shown before anything can be uploaded */
.pu-who{display:flex;gap:12px;align-items:center;background:#f5f7fa;border:1px solid #e0e5ed;padding:11px 12px;margin-bottom:13px;}
.pu-who__pic{width:56px;height:75px;flex:0 0 auto;background:#eef1f6;border:1px solid #e0e5ed;overflow:hidden;}
.pu-who__pic img{width:100%;height:100%;object-fit:cover;display:block;}
.pu-who__pic div{width:100%;height:100%;display:flex;align-items:center;justify-content:center;font-size:9px;color:#9aa4b5;text-align:center;padding:3px;box-sizing:border-box;}
.pu-who__txt{min-width:0;flex:1;}
.pu-who__txt b{display:block;font-size:14px;color:#05275C;line-height:1.3;}
.pu-who__txt span{display:block;font-size:11.5px;color:#5b6472;margin-top:2px;}
.pu-warn{display:flex;gap:8px;align-items:flex-start;font-size:11.5px;line-height:1.5;padding:8px 11px;margin-bottom:10px;border:1px solid;}
.pu-warn--ban{background:#fdecec;border-color:#f4c2c2;color:#7a1f1a;}
.pu-warn--pend{background:#fff8e1;border-color:#f0e0a8;color:#6b5200;}
/* drop zone: click, drag, or paste */
.pu-drop{display:block;border:2px dashed #c7d0de;background:#fafbfc;padding:22px 14px;text-align:center;cursor:pointer;transition:all .13s;}
.pu-drop:hover,.pu-drop--over{border-color:#174DA4;background:#f2f6fd;}
.pu-drop b{display:block;font-size:13px;color:#05275C;margin-top:7px;}
.pu-drop span{display:block;font-size:11px;color:#8b93a3;margin-top:3px;}
.pu-file{position:absolute;width:1px;height:1px;opacity:0;pointer-events:none;}
/* before / after, in the exact 3:4 frame the system stores */
.pu-cmp{display:flex;gap:14px;justify-content:center;align-items:flex-start;margin:14px 0 4px;}
.pu-cmp__c{text-align:center;}
.pu-cmp__f{width:120px;height:160px;background:#eef1f6;border:1px solid #e0e5ed;overflow:hidden;}
.pu-cmp__f img{width:100%;height:100%;object-fit:cover;display:block;}
.pu-cmp__f div{width:100%;height:100%;display:flex;align-items:center;justify-content:center;font-size:10px;color:#9aa4b5;}
.pu-cmp__l{font-size:10px;font-weight:800;text-transform:uppercase;letter-spacing:.4px;color:#8b93a3;margin-top:5px;}
.pu-cmp__c--new .pu-cmp__l{color:#1c7a45;}
.pu-cmp__ar{align-self:center;color:#c7d0de;font-size:20px;line-height:1;padding-top:14px;}
.pu-size{font-size:11px;color:#1c7a45;text-align:center;margin-top:6px;}
.pu-msg{font-size:12px;padding:9px 11px;margin-top:12px;line-height:1.5;display:none;}
.pu-msg--err{background:#fdecec;color:#b3261e;border:1px solid #f4c2c2;}
.pu-msg--info{background:#eef4ff;color:#174DA4;border:1px solid #cfe0ff;}
@media(max-width:520px){.pu-cmp{gap:8px;}.pu-cmp__f{width:96px;height:128px;}}
/* reject-reason chips */
.pc-chips{display:flex;flex-wrap:wrap;gap:6px;margin:2px 0 12px;}
.pc-chip{font-size:11.5px;border:1px solid #e0e5ed;background:#fff;color:#3a4250;padding:6px 11px;border-radius:14px;cursor:pointer;transition:all .12s;user-select:none;}
.pc-chip:hover{border-color:#b3261e;color:#b3261e;}
.pc-chip--on{background:#b3261e;border-color:#b3261e;color:#fff;}
.pc-reject-who{font-size:12px;color:#5b6472;background:#f5f7fa;border:1px solid #e0e5ed;padding:8px 11px;margin-bottom:12px;}
.pc-reject-who b{color:#05275C;}
</style>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
<div class="pc-wrap">
    <div class="pc-head">
        <h1>Official Photograph Approvals</h1>
        <p>Every student official-photograph change starts as <strong>Pending</strong>. <strong>Approving</strong> a photograph makes it the student&rsquo;s official photo and automatically clears every other version they submitted. Rejecting removes the photograph, and the student must upload a new one. When a student submits several photographs, just approve the good one &mdash; or use <strong>Delete this version</strong> to clear an individual extra. The student&rsquo;s current live photograph is badged <strong>Current</strong>.</p>
    </div>
    <asp:Literal ID="litBody" runat="server" />
</div>
<!-- Admin: initiate a record / set a student's photo status (any -> any) -->
<div class="pc-mov" id="pcInitOv" onclick="if(event.target===this)pcCloseInit()">
    <div class="pc-modal">
        <div class="pc-modal__h"><b>Set a student's official-photograph status</b><button type="button" class="pc-modal__x" onclick="pcCloseInit()">&times;</button></div>
        <div class="pc-modal__b">
            <div class="pc-fld">
                <label>Registration number</label>
                <input type="text" id="piReg" placeholder="e.g. MRU2024001234" autocomplete="off" />
            </div>
            <div class="pc-fld">
                <label>Set status to</label>
                <select id="piStatus">
                    <option value="APPROVED">APPROVED — photograph is valid &amp; visible</option>
                    <option value="PENDING">PENDING — awaiting review (photograph stays visible)</option>
                    <option value="REJECTED">REJECTED — block &amp; remove the photograph (student must re-upload)</option>
                </select>
            </div>
            <div class="pc-fld">
                <label>Note / reason <span style="font-weight:400;text-transform:none;color:#8b93a3;">(optional &mdash; tap to add)</span></label>
                <div class="pc-chips" id="piChips"></div>
                <textarea id="piComment" placeholder="Shown to the student if you reject."></textarea>
                <div class="pc-hint">Creates a photo-change record for this student and sets their status. <b>REJECTED</b> also removes the current photo, so the student is prompted to upload a new one.</div>
            </div>
            <div class="pc-msg" id="piMsg" style="display:none;font-size:12px;padding:8px 10px;"></div>
        </div>
        <div class="pc-modal__f">
            <button type="button" class="pc-btn" onclick="pcCloseInit()">Cancel</button>
            <button type="button" class="pc-btn pc-btn--nav" id="piGo" onclick="pcSubmitInit()">Apply</button>
        </div>
    </div>
</div>

<!-- Admin: upload a photograph on a student's behalf (counter case) -->
<div class="pc-mov" id="pcUpOv" onclick="if(event.target===this)pcCloseUp()">
    <div class="pc-modal pc-modal--up">
        <div class="pc-modal__h"><b>Upload a photograph for a student</b><button type="button" class="pc-modal__x" onclick="pcCloseUp()">&times;</button></div>
        <div class="pc-modal__b">

            <%-- STEP 1 — who is this for? Nothing can be uploaded until a real student
                 is on screen, because the one mistake that matters here is putting a
                 photograph on the wrong record, and it is invisible afterwards. --%>
            <div class="pc-step pc-step--on" id="puStep1">
                <div class="pc-fld">
                    <label>Student registration number</label>
                    <input type="text" id="puReg" placeholder="e.g. MRU2024001234" autocomplete="off" spellcheck="false" />
                    <div class="pc-hint">Use this when a student cannot upload a photograph themselves &mdash; they have come to the office with a printed or digital photo.</div>
                </div>
            </div>

            <%-- STEP 2 — confirm the person, then send the file. --%>
            <div class="pc-step" id="puStep2">
                <div class="pu-who">
                    <div class="pu-who__pic" id="puWhoPic"></div>
                    <div class="pu-who__txt">
                        <b id="puWhoName"></b>
                        <span id="puWhoMeta"></span>
                    </div>
                </div>
                <div class="pu-warn pu-warn--ban" id="puWarnBan" style="display:none;"></div>
                <div class="pu-warn pu-warn--pend" id="puWarnPend" style="display:none;"></div>

                <label class="pu-drop" id="puDrop" for="puFile">
                    <svg xmlns="http://www.w3.org/2000/svg" width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#174DA4" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="17 8 12 3 7 8"></polyline><line x1="12" y1="3" x2="12" y2="15"></line></svg>
                    <b>Choose the photograph</b>
                    <span>Click here, drag a file in, or press Ctrl+V to paste a copied image</span>
                </label>
                <input type="file" id="puFile" class="pu-file" accept="image/*" />

                <div class="pu-cmp">
                    <div class="pu-cmp__c">
                        <div class="pu-cmp__f" id="puOldFrame"></div>
                        <div class="pu-cmp__l">Now on file</div>
                    </div>
                    <div class="pu-cmp__ar">&rarr;</div>
                    <div class="pu-cmp__c pu-cmp__c--new">
                        <div class="pu-cmp__f" id="puNewFrame"><div>nothing chosen</div></div>
                        <div class="pu-cmp__l">Will become official</div>
                    </div>
                </div>
                <div class="pu-size" id="puSize"></div>

                <div class="pc-fld" style="margin-top:14px;">
                    <label>Note for the record <span style="font-weight:400;text-transform:none;color:#8b93a3;">(optional)</span></label>
                    <input type="text" id="puNote" placeholder="e.g. Student brought a studio photo to Kirumba front desk" autocomplete="off" />
                </div>
            </div>

            <div class="pu-msg" id="puMsg"></div>
        </div>
        <div class="pc-modal__f">
            <button type="button" class="pc-btn" id="puBack" onclick="pcUpBack()" style="display:none;">Change student</button>
            <button type="button" class="pc-btn" onclick="pcCloseUp()">Cancel</button>
            <button type="button" class="pc-btn pc-btn--nav" id="puFind" onclick="pcUpFind()">Find student</button>
            <button type="button" class="pc-btn pc-btn--ok" id="puGo" onclick="pcUpSubmit()" style="display:none;" disabled>Choose a photo first</button>
        </div>
    </div>
</div>

<!-- Reject reason modal (per-row & batch) with clickable common reasons -->
<div class="pc-mov" id="pcRejOv" onclick="if(event.target===this)pcRejClose()">
    <div class="pc-modal">
        <div class="pc-modal__h"><b>Reject official photograph</b><button type="button" class="pc-modal__x" onclick="pcRejClose()">&times;</button></div>
        <div class="pc-modal__b">
            <div class="pc-reject-who" id="pcRejWho"></div>
            <div class="pc-fld">
                <label>Common reasons <span style="font-weight:400;text-transform:none;color:#8b93a3;">(tap to add)</span></label>
                <div class="pc-chips" id="pcChips"></div>
            </div>
            <div class="pc-fld">
                <label>Reason shown to the student</label>
                <textarea id="pcRejReason" placeholder="Pick from above or type your own reason..."></textarea>
                <div class="pc-hint">This message is shown to the student, and their photograph is removed &mdash; they must upload a new one before they can use their dashboard.</div>
            </div>
            <label class="pc-banchk"><input type="checkbox" id="pcRejBan"/> <span>Also <b>ban</b> this student from uploading (they must visit the admin office to be unbanned)</span></label>
            <div class="pc-hint" style="margin-top:2px;">Students are banned automatically after 3 rejections &mdash; tick this to ban immediately.</div>
        </div>
        <div class="pc-modal__f">
            <button type="button" class="pc-btn" onclick="pcRejClose()">Cancel</button>
            <button type="button" class="pc-btn pc-btn--danger" id="pcRejGo" onclick="pcRejConfirm()">Reject photograph</button>
        </div>
    </div>
</div>

<div class="pc-toast" id="pcToast"></div>

<!-- Full-size photo lightbox -->
<div class="pc-lightbox" id="pcLightbox" onclick="if(event.target===this)pcCloseView()">
    <span class="pc-lightbox__x" onclick="pcCloseView()">&times;</span>
    <img id="pcLightboxImg" src="" alt="Full size photograph" />
</div>

<script type="text/javascript">
(function () {
    // Search navigates to a real query-string URL rather than submitting anything.
    // The search markup is rendered inside the master page's server-side form element, and
    // a nested GET form there is discarded by the browser: its fields end up on the outer
    // ASP.NET form, which POSTs, so the typed term never reached the query string that
    // BuildList reads. Navigating directly keeps search a true GET, so the URL carries the
    // state and can be bookmarked, shared, and walked with Back.
    // (Deliberately no literal form tags in this comment — the ASPX page parser scans this
    //  block and would treat them as real server tags, breaking the page.)
    window.pcSearch = function () {
        var qEl = document.getElementById("pcQ");
        var sEl = document.getElementById("pcStatus");
        var q = qEl ? qEl.value.replace(/^\s+|\s+$/g, "") : "";
        var st = sEl ? sEl.value : "PENDING";
        var url = "PhotoChangeController.aspx?status=" + encodeURIComponent(st);
        if (q) url += "&q=" + encodeURIComponent(q);
        // Any new search starts at page 1 — keeping ?page=4 from the previous result set
        // would land on an empty page.
        window.location.href = url;
    };

    // Enter inside the search box searches, and must not be allowed to submit the
    // surrounding server form (which is what made the box appear to clear itself).
    document.addEventListener("keydown", function (e) {
        if (e.key !== "Enter" && e.keyCode !== 13) return;
        var t = e.target;
        if (!t || t.id !== "pcQ") return;
        e.preventDefault();
        window.pcSearch();
    }, true);

    window.pcOpenInit = function () {
        document.getElementById("piReg").value = "";
        document.getElementById("piStatus").value = "APPROVED";
        document.getElementById("piComment").value = "";
        var m = document.getElementById("piMsg"); m.style.display = "none";
        if (window.pcBuildChips) window.pcBuildChips("piChips", "piComment");
        document.getElementById("pcInitOv").style.display = "flex";
        document.getElementById("piReg").focus();
    };
    window.pcCloseInit = function () { document.getElementById("pcInitOv").style.display = "none"; };
    window.pcSubmitInit = function () {
        var reg = document.getElementById("piReg").value.trim();
        var st = document.getElementById("piStatus").value;
        var c = document.getElementById("piComment").value.trim();
        var m = document.getElementById("piMsg");
        if (!reg) { m.style.display = "block"; m.style.background = "#fdecec"; m.style.color = "#b3261e"; m.textContent = "Enter a registration number."; return; }
        if (st === "REJECTED" && !confirm("Reject and REMOVE " + reg + "'s photograph? They will be blocked from the dashboard until they re-upload.")) return;
        var go = document.getElementById("piGo"); go.disabled = true;
        fetch("PhotoChangeController.aspx", {
            method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded", "X-Requested-With": "XMLHttpRequest" },
            body: "action=admininit&regno=" + encodeURIComponent(reg) + "&status=" + encodeURIComponent(st) + "&comment=" + encodeURIComponent(c)
        }).then(function (r) { return r.json(); }).then(function (d) {
            go.disabled = false;
            if (d && d.success) { pcCloseInit(); pcToast(d.message || "Done"); setTimeout(function () { window.location.reload(); }, 800); }
            else { m.style.display = "block"; m.style.background = "#fdecec"; m.style.color = "#b3261e"; m.textContent = (d && d.message) ? d.message : "Failed."; }
        }).catch(function () { go.disabled = false; m.style.display = "block"; m.style.background = "#fdecec"; m.style.color = "#b3261e"; m.textContent = "Request failed."; });
    };
    /* ================= Admin: upload a photograph for a student =================
       Deliberately two steps. The registration number is resolved to a person and
       that person is shown - name, programme, and the photograph currently on file -
       before any file can be sent. Putting a photograph on the wrong record is the
       one mistake here that leaves no trace: the record looks entirely normal, just
       with somebody else's face on it, and it flows on to the ID card. */
    var puWho = null;      // the confirmed student, or null while none is chosen
    var puBlob = null;     // the photograph to send, already shrunk
    var puUrl = null;      // object URL for the preview, revoked when replaced

    function puEl(id) { return document.getElementById(id); }
    function puMsg(kind, text) {
        var m = puEl("puMsg");
        m.className = "pu-msg pu-msg--" + kind;
        m.innerHTML = text;
        m.style.display = "block";
    }
    function puClearMsg() { var m = puEl("puMsg"); m.style.display = "none"; m.innerHTML = ""; }
    function puEsc(s) {
        return String(s == null ? "" : s).replace(/&/g, "&amp;").replace(/</g, "&lt;")
            .replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    }
    function puBytes(n) {
        return n >= 1048576 ? (n / 1048576).toFixed(1) + " MB" : Math.max(1, Math.round(n / 1024)) + " KB";
    }
    function puDropPreview() {
        if (puUrl) { try { URL.revokeObjectURL(puUrl); } catch (e) { } puUrl = null; }
    }

    window.pcOpenUp = function () {
        puWho = null; puBlob = null; puDropPreview();
        puEl("puReg").value = ""; puEl("puNote").value = ""; puEl("puFile").value = "";
        puEl("puStep1").className = "pc-step pc-step--on";
        puEl("puStep2").className = "pc-step";
        puEl("puFind").style.display = ""; puEl("puGo").style.display = "none";
        puEl("puBack").style.display = "none";
        puEl("puNewFrame").innerHTML = "<div>nothing chosen</div>";
        puEl("puSize").textContent = "";
        puClearMsg();
        puEl("pcUpOv").style.display = "flex";
        puEl("puReg").focus();
    };
    window.pcCloseUp = function () { puDropPreview(); puEl("pcUpOv").style.display = "none"; };
    window.pcUpBack = function () {
        puWho = null; puBlob = null; puDropPreview();
        puEl("puStep1").className = "pc-step pc-step--on";
        puEl("puStep2").className = "pc-step";
        puEl("puFind").style.display = ""; puEl("puGo").style.display = "none";
        puEl("puBack").style.display = "none";
        puClearMsg();
        puEl("puReg").focus(); puEl("puReg").select();
    };

    // ---- step 1: resolve the registration number to a real person ----
    window.pcUpFind = function () {
        var reg = puEl("puReg").value.replace(/^\s+|\s+$/g, "");
        if (!reg) { puMsg("err", "Enter a registration number."); return; }
        var b = puEl("puFind"); b.disabled = true; b.textContent = "Looking up...";
        puMsg("info", "Looking up " + puEsc(reg) + "&hellip;");
        post("action=lookupstudent&regno=" + encodeURIComponent(reg)).then(function (d) {
            b.disabled = false; b.textContent = "Find student";
            if (!d || !d.success) { puMsg("err", (d && d.message) ? puEsc(d.message) : "Could not find that student."); return; }
            puWho = d;
            puClearMsg();

            puEl("puWhoName").textContent = d.name || d.regno;
            puEl("puWhoMeta").textContent = d.regno + (d.programme ? "  ·  " + d.programme : "")
                + (d.status ? "  ·  photo " + d.status.toLowerCase() : "");
            puEl("puWhoPic").innerHTML = d.photoUrl
                ? "<img src='" + puEsc(d.photoUrl) + "' alt='current'/>"
                : "<div>no photo</div>";
            puEl("puOldFrame").innerHTML = d.photoUrl
                ? "<img src='" + puEsc(d.photoUrl) + "' alt='current'/>"
                : "<div>no photo</div>";

            // Say up front what else this upload will do, rather than letting the
            // administrator discover it in the result message afterwards.
            var wb = puEl("puWarnBan");
            if (d.banned) {
                wb.innerHTML = "<span><b>This student is banned from uploading.</b> "
                    + (d.banReason ? "Reason: " + puEsc(d.banReason) + ". " : "")
                    + "Uploading for them here will also lift the ban.</span>";
                wb.style.display = "flex";
            } else { wb.style.display = "none"; }

            var wp = puEl("puWarnPend");
            if (d.pending > 0) {
                wp.innerHTML = "<span>This student has <b>" + d.pending + "</b> photograph"
                    + (d.pending === 1 ? "" : "s") + " waiting in the review queue. "
                    + "Uploading here replaces " + (d.pending === 1 ? "it" : "them") + ".</span>";
                wp.style.display = "flex";
            } else { wp.style.display = "none"; }

            puEl("puStep1").className = "pc-step";
            puEl("puStep2").className = "pc-step pc-step--on";
            puEl("puFind").style.display = "none";
            puEl("puBack").style.display = "";
            puEl("puGo").style.display = "";
            puSetGo();
        }).catch(function () {
            b.disabled = false; b.textContent = "Find student";
            puMsg("err", "Could not reach the server. Please try again.");
        });
    };

    function puSetGo() {
        var g = puEl("puGo");
        if (!puBlob) { g.disabled = true; g.textContent = "Choose a photo first"; return; }
        g.disabled = false;
        // The button names the student. An administrator cannot commit the change
        // without reading whose record it lands on.
        var nm = (puWho && (puWho.name || puWho.regno)) || "this student";
        g.textContent = "Set as " + nm + "'s official photo";
    }

    /* Shrink in the browser before sending, exactly as the student's page does: an
       office scan is often 8-10 MB, and the server keeps a 300x400 thumbnail either
       way. Drawing to a canvas also normalises HEIC and anything else the browser
       can decode but the server cannot. */
    function puShrink(file, done, cannot) {
        var MAX_EDGE = 1400, MAX_BYTES = 5 * 1024 * 1024;
        function draw(src) {
            try {
                var w = src.width, h = src.height;
                if (!w || !h) { cannot("That file could not be opened as a picture."); return; }
                var sc = Math.min(1, MAX_EDGE / Math.max(w, h));
                var cw = Math.max(1, Math.round(w * sc)), ch = Math.max(1, Math.round(h * sc));
                var cv = document.createElement("canvas");
                cv.width = cw; cv.height = ch;
                var cx = cv.getContext("2d");
                if (!cx) { cannot("This browser cannot resize the picture."); return; }
                cx.fillStyle = "#fff"; cx.fillRect(0, 0, cw, ch);   // or a transparent PNG flattens to black
                cx.drawImage(src, 0, 0, cw, ch);
                if (src.close) { try { src.close(); } catch (e) { } }
                cv.toBlob(function (b) {
                    if (!b) { cannot("The picture could not be prepared."); return; }
                    if (b.size > MAX_BYTES) { cannot("Even after resizing, this picture is " + puBytes(b.size) + "."); return; }
                    done(b);
                }, "image/jpeg", 0.9);
            } catch (e) { cannot("Something went wrong while preparing the picture."); }
        }
        if (window.createImageBitmap) {
            createImageBitmap(file, { imageOrientation: "from-image" }).then(draw, function () { viaImg(); });
        } else { viaImg(); }
        function viaImg() {
            var u = URL.createObjectURL(file), im = new Image();
            im.onload = function () { try { draw(im); } finally { URL.revokeObjectURL(u); } };
            im.onerror = function () { URL.revokeObjectURL(u); cannot("That file could not be opened as a picture."); };
            im.src = u;
        }
    }

    function puTake(file) {
        if (!file) return;
        if (file.type && file.type.indexOf("image/") !== 0) {
            puMsg("err", "That is not a picture. Choose a photograph file.");
            return;
        }
        puMsg("info", "Preparing the picture&hellip;");
        puEl("puGo").disabled = true;
        puShrink(file, function (blob) {
            puDropPreview();
            puBlob = blob;
            puUrl = URL.createObjectURL(blob);
            // The frame is 3:4 with object-fit:cover, which is exactly the centre-crop
            // the server applies - so this preview IS what gets stored, not an approximation.
            puEl("puNewFrame").innerHTML = "<img src='" + puUrl + "' alt='new'/>";
            var note = "Ready to send — " + puBytes(blob.size) + ".";
            if (file.size > blob.size * 1.5) note = "Resized from " + puBytes(file.size) + " to " + puBytes(blob.size) + ".";
            puEl("puSize").textContent = note;
            puClearMsg();
            puSetGo();
        }, function (reason) {
            puBlob = null;
            puEl("puNewFrame").innerHTML = "<div>nothing chosen</div>";
            puEl("puSize").textContent = "";
            puMsg("err", puEsc(reason) + " Try a JPG or PNG copy of the photograph.");
            puSetGo();
        });
    }

    // ---- step 2: choose the file, by click, drag, or paste ----
    (function wireUpPicker() {
        /* Everything on this page lives in one outer IIFE, so anything thrown here
           would stop pcToast, pcReview, pcRestore and the rest from ever being
           defined — a missing element in this dialog would silently disable approve
           and reject for the whole screen. Hence the guard and the try. */
        try {
            var f = puEl("puFile"), dz = puEl("puDrop"), rg = puEl("puReg");
            if (!f || !dz || !rg) return;
        f.addEventListener("change", function () { puTake(f.files && f.files[0]); });
        ["dragenter", "dragover"].forEach(function (ev) {
            dz.addEventListener(ev, function (e) { e.preventDefault(); e.stopPropagation(); dz.classList.add("pu-drop--over"); });
        });
        ["dragleave", "drop"].forEach(function (ev) {
            dz.addEventListener(ev, function (e) { e.preventDefault(); e.stopPropagation(); dz.classList.remove("pu-drop--over"); });
        });
        dz.addEventListener("drop", function (e) {
            puTake(e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0]);
        });
        // Paste: the photograph is often already on the clipboard, straight from a
        // scanner, a WhatsApp window, or a snip. Only listens while the dialog is open.
        document.addEventListener("paste", function (e) {
            if (puEl("pcUpOv").style.display !== "flex") return;
            var items = e.clipboardData && e.clipboardData.items; if (!items) return;
            for (var i = 0; i < items.length; i++) {
                if (items[i].type && items[i].type.indexOf("image/") === 0) {
                    var blob = items[i].getAsFile();
                    if (blob) { e.preventDefault(); puTake(blob); return; }
                }
            }
        });
            // Enter in the registration box looks the student up, instead of submitting
            // the surrounding server-side form (which would reload and lose the dialog).
            rg.addEventListener("keydown", function (e) {
                if (e.key === "Enter" || e.keyCode === 13) { e.preventDefault(); window.pcUpFind(); }
            });
        } catch (e) { /* the dialog degrades; the rest of the screen keeps working */ }
    })();

    window.pcUpSubmit = function () {
        if (!puWho || !puBlob) { puMsg("err", "Choose a student and a photograph first."); return; }
        var nm = puWho.name || puWho.regno;
        if (!confirm("Set this photograph as the official photo for:\n\n" + nm + "\n" + puWho.regno
            + "\n\nIt becomes their official photo straight away. The photograph currently on file is kept and can be restored later.")) return;

        var g = puEl("puGo"); g.disabled = true; g.textContent = "Uploading...";
        puMsg("info", "Uploading&hellip;");

        var fd = new FormData();
        fd.append("action", "adminupload");
        fd.append("regno", puWho.regno);
        fd.append("comment", puEl("puNote").value.replace(/^\s+|\s+$/g, ""));
        // A canvas blob has no filename, and multipart needs one. The content is JPEG.
        fd.append("photoFile", puBlob, "adminphoto.jpg");

        // Read the reply as text before parsing: going straight to .json() throws on an
        // error page and lands in .catch(), where a server fault reads as "no connection".
        fetch("PhotoChangeController.aspx", { method: "POST", body: fd, headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (r) { return r.text().then(function (t) { return { ok: r.ok, status: r.status, text: t }; }); })
            .then(function (res) {
                var d = null;
                try { d = JSON.parse(res.text); } catch (e) { }
                if (d && d.success) {
                    pcCloseUp();
                    pcToast(d.message || "Photograph saved.");
                    setTimeout(function () { window.location.reload(); }, 900);
                    return;
                }
                g.disabled = false; puSetGo();
                if (d && d.message) puMsg("err", puEsc(d.message));
                else if (!res.ok) puMsg("err", "The server could not save it (error " + res.status + "). Please try again.");
                else puMsg("err", "The reply from the server could not be read. Please try again.");
            })
            .catch(function () {
                g.disabled = false; puSetGo();
                puMsg("err", "Could not reach the server. Check the connection and try again.");
            });
    };

    window.pcToast = function (text, err) {
        var t = document.getElementById("pcToast");
        t.textContent = text; t.className = "pc-toast" + (err ? " pc-toast--err" : "");
        t.style.display = "block"; clearTimeout(t._t); t._t = setTimeout(function () { t.style.display = "none"; }, 3200);
    };
    function post(data) {
        return fetch("PhotoChangeController.aspx", {
            method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded", "X-Requested-With": "XMLHttpRequest" }, body: data
        }).then(function (r) { return r.json(); });
    }
    // ---- reject-reason modal (per-row & batch) with clickable common reasons ----
    var REASONS = [
        "Photo is blurry or out of focus",
        "Not taken straight-on (face at an angle)",
        "Face is not clearly visible",
        "Background is not plain (busy or dark)",
        "Wearing a hat, cap or sunglasses",
        "This is a selfie, not a passport-style photo",
        "Does not appear to be the actual student",
        "Image quality too low / too small",
        "Other people or objects in the frame",
        "Photo is not appropriate for official records"
    ];
    var _rejTarget = null; // {mode:'single', id} | {mode:'batch', ids:[]}

    window.pcRejClose = function () { document.getElementById("pcRejOv").style.display = "none"; };

    window.pcBuildChips = function (containerId, taId) {
        var wrap = document.getElementById(containerId); if (!wrap) return; wrap.innerHTML = "";
        REASONS.forEach(function (r) {
            var b = document.createElement("span");
            b.className = "pc-chip"; b.textContent = r;
            b.onclick = function () { toggleChip(b, r, taId); };
            wrap.appendChild(b);
        });
    };
    function openReject(target, whoHtml) {
        _rejTarget = target;
        document.getElementById("pcRejWho").innerHTML = whoHtml;
        document.getElementById("pcRejReason").value = "";
        var bc = document.getElementById("pcRejBan"); if (bc) bc.checked = false;
        window.pcBuildChips("pcChips", "pcRejReason");
        document.getElementById("pcRejOv").style.display = "flex";
    }
    function toggleChip(el, text, taId) {
        var ta = document.getElementById(taId);
        var parts = ta.value.split(/;\s*/).map(function (s) { return s.trim(); }).filter(Boolean);
        var i = parts.indexOf(text);
        if (i >= 0) { parts.splice(i, 1); el.classList.remove("pc-chip--on"); }
        else { parts.push(text); el.classList.add("pc-chip--on"); }
        ta.value = parts.join("; ");
    }
    window.pcRejConfirm = function () {
        var reason = document.getElementById("pcRejReason").value.trim();
        if (!reason && !confirm("Reject without a reason? The student will not be told why.")) return;
        var ban = (document.getElementById("pcRejBan") && document.getElementById("pcRejBan").checked) ? "1" : "0";
        var body = _rejTarget.mode === "single"
            ? "action=review&id=" + encodeURIComponent(_rejTarget.id) + "&decision=reject&ban=" + ban + "&comment=" + encodeURIComponent(reason)
            : "action=batch&ids=" + encodeURIComponent(_rejTarget.ids.join(",")) + "&decision=reject&ban=" + ban + "&comment=" + encodeURIComponent(reason);
        var go = document.getElementById("pcRejGo"); go.disabled = true;
        post(body).then(function (d) {
            go.disabled = false;
            if (d && d.success) { pcRejClose(); pcToast(d.message || "Done"); setTimeout(reloadKeep, 800); }
            else { pcToast((d && d.message) || "Failed.", true); }
        }).catch(function () { go.disabled = false; pcToast("Request failed.", true); });
    };

    window.pcReview = function (id, approve) {
        if (!approve) { openReject({ mode: "single", id: id }, "Rejecting <b>1</b> photograph &mdash; it will be removed and the student asked to re-upload."); return; }
        // Approving asks for no confirmation: it is the ordinary outcome of a review the
        // reviewer has already made by looking at the photograph, and it is reversible from
        // the version history. A prompt on every approval is pure friction on a queue that is
        // worked through in bulk. Rejection and deletion still confirm, because those remove
        // the submitted image.
        post("action=review&id=" + encodeURIComponent(id) + "&decision=approve&comment=")
            .then(function (d) { pcToast(d.message || "Done", !d.success); if (d.success) setTimeout(reloadKeep, 700); })
            .catch(function () { pcToast("Request failed.", true); });
    };
    window.pcBatch = function (approve) {
        var ids = [];
        document.querySelectorAll(".pc-chk:checked").forEach(function (c) { ids.push(c.value); });
        if (ids.length === 0) { pcToast("Select at least one photo first.", true); return; }
        if (!approve) { openReject({ mode: "batch", ids: ids }, "Rejecting <b>" + ids.length + "</b> selected photograph(s) &mdash; each will be removed and the students asked to re-upload."); return; }
        post("action=batch&ids=" + encodeURIComponent(ids.join(",")) + "&decision=approve&comment=")
            .then(function (d) { pcToast(d.message || "Done", !d.success); if (d.success) setTimeout(reloadKeep, 800); })
            .catch(function () { pcToast("Request failed.", true); });
    };
    // ---- revert the student to an earlier photograph ----
    window.pcRestore = function (id) {
        var why = prompt("Make this earlier photograph the student's official one again.\n\nOptional note for the record:", "");
        if (why === null) return;
        post("action=restoreversion&id=" + encodeURIComponent(id) + "&comment=" + encodeURIComponent(why.trim()))
            .then(function (d) { pcToast(d.message || "Done", !d.success); if (d.success) setTimeout(reloadKeep, 700); })
            .catch(function () { pcToast("Request failed.", true); });
    };
    // ---- delete a photo version (extra / unwanted submission; never the live photo) ----
    window.pcDelete = function (id) {
        if (!confirm("Delete this photo version?\n\nIt is removed from the review queue and its image file cleaned up. The student's current live photograph is not affected.")) return;
        post("action=deleteversion&id=" + encodeURIComponent(id))
            .then(function (d) { pcToast(d.message || "Done", !d.success); if (d.success) setTimeout(reloadKeep, 700); })
            .catch(function () { pcToast("Request failed.", true); });
    };
    window.pcDeleteBatch = function () {
        var ids = [];
        document.querySelectorAll(".pc-chk:checked").forEach(function (c) { ids.push(c.value); });
        if (ids.length === 0) { pcToast("Select at least one version first.", true); return; }
        if (!confirm("Delete " + ids.length + " selected version(s)?\n\nAny that are a student's current live photograph are skipped automatically.")) return;
        post("action=deleteversion&ids=" + encodeURIComponent(ids.join(",")) + "&comment=")
            .then(function (d) { pcToast(d.message || "Done", !d.success); if (d.success) setTimeout(reloadKeep, 800); })
            .catch(function () { pcToast("Request failed.", true); });
    };
    window.pcUnban = function (regno) {
        if (!confirm("Lift the photo-upload ban for " + regno + "? They will be able to upload a new photograph again.")) return;
        post("action=unban&regno=" + encodeURIComponent(regno))
            .then(function (d) { pcToast(d.message || "Done", !d.success); if (d.success) setTimeout(reloadKeep, 800); })
            .catch(function () { pcToast("Request failed.", true); });
    };
    // Full-size photo viewer (lightbox)
    window.pcView = function (url) {
        if (!url) return;
        var ov = document.getElementById("pcLightbox"), im = document.getElementById("pcLightboxImg");
        if (!ov || !im) return;
        im.src = url; ov.style.display = "flex";
    };
    window.pcCloseView = function () { var ov = document.getElementById("pcLightbox"); if (ov) { ov.style.display = "none"; document.getElementById("pcLightboxImg").src = ""; } };
    document.addEventListener("keydown", function (e) { if (e.key === "Escape") window.pcCloseView(); });

    window.pcToggleAll = function (cb) {
        document.querySelectorAll(".pc-chk").forEach(function (c) { c.checked = cb.checked; });
        pcCount();
    };
    window.pcCount = function () {
        var n = document.querySelectorAll(".pc-chk:checked").length;
        var el = document.getElementById("pcSelCount"); if (el) el.textContent = n + " selected";
    };
    function reloadKeep() { window.location.reload(); }
})();
</script>
</asp:Content>
