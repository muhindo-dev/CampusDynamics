<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true" CodeFile="ExamConfiguration.aspx.cs" Inherits="COOPERP_NewScreens_ExamConfiguration" Title="Examination Configuration - Campus Dynamics" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
<style>
:root{--brand:#174DA4;--brand-dk:#05275C;--danger:#dc2626;--ok:#1c7a45;--warn:#8a6100;--surf:#f5f7fa;--bdr:#e0e5ed;--txt:#1a1a2e;--muted:#64748b;}

.xc-head{background:#fff;border-bottom:1px solid var(--bdr);padding:20px 28px;}
.xc-head h1{font-size:18px;font-weight:700;color:var(--txt);margin:0;}
.xc-head p{font-size:12px;color:var(--muted);margin:3px 0 0;max-width:820px;line-height:1.55;}
.xc-wrap{padding:20px 28px 44px;max-width:1180px;}

.xc-note{display:flex;gap:10px;align-items:flex-start;background:#eef4ff;border:1px solid #c5d8f7;border-left:4px solid var(--brand);
    padding:11px 13px;font-size:12px;color:#1a4da4;line-height:1.55;margin:0 0 18px;}
.xc-note svg{width:16px;height:16px;flex:0 0 auto;margin-top:1px;}
.xc-note b{color:var(--brand-dk);}

/* ── a labelled field, used everywhere ─────────────────────────────────── */
.xc-fld{display:flex;flex-direction:column;gap:3px;min-width:0;}
.xc-fld label{font-size:10px;color:var(--muted);text-transform:uppercase;letter-spacing:.3px;font-weight:600;}
.xc-fld select,.xc-fld input{border:1px solid var(--bdr);height:32px;font-size:12px;padding:0 8px;font-family:inherit;background:#fff;color:var(--txt);}
.xc-fld select:focus,.xc-fld input:focus{outline:none;border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.12);}
.xc-fld select[disabled],.xc-fld input[disabled]{background:var(--surf);color:var(--muted);cursor:not-allowed;}

/* ── the scope form: which rules am I editing ──────────────────────────── */
.xc-scope{background:#fff;border:1px solid var(--bdr);border-radius:4px;margin:0 0 18px;}
.xc-scope__h{padding:12px 16px 0;}
.xc-scope__h h2{font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.5px;color:var(--brand-dk);margin:0;}
.xc-scope__h p{font-size:11.5px;color:var(--muted);margin:3px 0 0;line-height:1.5;}
.xc-scope__b{padding:12px 16px 14px;display:flex;gap:12px;flex-wrap:wrap;align-items:flex-end;}
.xc-scope__b .xc-fld{flex:1 1 170px;}
.xc-scope__b .xc-fld--sem{flex:0 0 130px;}
.xc-scope__now{padding:9px 16px;background:var(--surf);border-top:1px solid var(--bdr);font-size:11.5px;color:var(--muted);
    display:flex;gap:8px;align-items:center;flex-wrap:wrap;}
.xc-scope__now b{color:var(--brand-dk);}

/* ── the settings form ─────────────────────────────────────────────────── */
.xc-sec{background:#fff;border:1px solid var(--bdr);border-radius:4px;margin:0 0 16px;}
.xc-sec__h{padding:11px 16px;border-bottom:1px solid var(--bdr);background:var(--surf);}
.xc-sec__h h2{font-size:11.5px;font-weight:800;text-transform:uppercase;letter-spacing:.5px;color:var(--brand-dk);margin:0;}
.xc-sec__b{padding:2px 16px 8px;}

.xc-f{display:grid;grid-template-columns:minmax(0,1fr) 290px;gap:18px;align-items:start;padding:13px 0;border-bottom:1px solid #f1f5f9;}
.xc-f:last-child{border-bottom:none;}
.xc-f--wide{grid-template-columns:minmax(0,1fr) 430px;}
.xc-f--dirty{background:#fffdf4;box-shadow:inset 3px 0 0 #e0a800;padding-left:9px;margin-left:-12px;padding-right:3px;}
.xc-f__lbl{font-size:12.5px;font-weight:600;color:var(--txt);}
.xc-f__help{font-size:11.5px;color:var(--muted);line-height:1.5;margin-top:3px;}
.xc-f__ctl{min-width:0;}
.xc-f__ctl>select,.xc-f__ctl>input{width:100%;border:1px solid var(--bdr);height:32px;font-size:12px;padding:0 8px;font-family:inherit;background:#fff;color:var(--txt);}
.xc-f__ctl>select:focus,.xc-f__ctl>input:focus{outline:none;border-color:var(--brand);box-shadow:0 0 0 2px rgba(23,77,164,.12);}
.xc-f__meta{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-top:6px;}

.xc-tag{font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:.3px;padding:2px 6px;white-space:nowrap;}
.xc-tag--inh{background:var(--surf);color:var(--muted);border:1px solid var(--bdr);}
.xc-tag--own{background:#eef4ff;color:var(--brand);border:1px solid #c5d8f7;}
.xc-tag--mix{background:#fff8e1;color:#8a6100;border:1px solid #f0dfa8;}
.xc-link{background:none;border:0;padding:0;font-size:11px;color:var(--brand);cursor:pointer;font-family:inherit;text-decoration:underline;}
.xc-link--d{color:#b42318;}

/* the From/To pair */
.xc-win{display:flex;gap:10px;align-items:flex-end;}
.xc-win .xc-fld{flex:1 1 0;}
.xc-win .xc-fld input{width:100%;}
.xc-win__s{font-size:11.5px;margin-top:6px;line-height:1.5;}
.xc-win__s--ok{color:var(--ok);} .xc-win__s--none{color:var(--muted);} .xc-win__s--bad{color:#b42318;font-weight:600;}

/* ── buttons ───────────────────────────────────────────────────────────── */
.xc-btn{display:inline-flex;align-items:center;gap:6px;border:0;padding:8px 14px;font-size:12px;font-weight:600;cursor:pointer;font-family:inherit;}
.xc-btn--p{background:var(--brand-dk);color:#fff;} .xc-btn--p:hover{background:var(--brand);}
.xc-btn--p[disabled]{background:#c3cbd8;cursor:not-allowed;}
.xc-btn--g{background:#fff;color:var(--brand-dk);border:1px solid var(--bdr);}
.xc-btn--g:hover{border-color:var(--brand);}
.xc-btn--g[disabled]{color:#b6bfcc;cursor:not-allowed;}
.xc-mini{background:#eef1f6;border:1px solid var(--bdr);color:var(--brand-dk);font-size:11px;font-weight:600;padding:0 10px;height:32px;cursor:pointer;font-family:inherit;white-space:nowrap;}
.xc-mini:hover{border-color:var(--brand);}

/* ── the save bar ──────────────────────────────────────────────────────── */
.xc-save{position:sticky;bottom:0;background:#fff;border:1px solid var(--bdr);border-top:2px solid var(--brand-dk);
    padding:11px 16px;display:flex;gap:14px;align-items:center;flex-wrap:wrap;z-index:50;box-shadow:0 -3px 14px rgba(8,15,30,.08);margin-bottom:20px;}
.xc-save__n{font-size:12px;font-weight:600;color:var(--muted);flex:0 0 auto;}
.xc-save__n.on{color:var(--warn);}
.xc-save__r{flex:1 1 240px;min-width:180px;}
.xc-save__r input{width:100%;border:1px solid var(--bdr);height:32px;font-size:12px;padding:0 8px;font-family:inherit;}
.xc-save__a{display:flex;gap:8px;flex:0 0 auto;}

/* ── diagnostic + rules table ──────────────────────────────────────────── */
.xc-eff{background:#fff;border:1px solid var(--bdr);border-radius:4px;margin:0 0 16px;}
.xc-eff__h{padding:12px 16px 0;}
.xc-eff__h h2{font-size:11.5px;font-weight:800;text-transform:uppercase;letter-spacing:.5px;color:var(--brand-dk);margin:0;}
.xc-eff__h p{font-size:11.5px;color:var(--muted);margin:3px 0 0;line-height:1.5;}
.xc-eff__b{padding:12px 16px 14px;display:flex;gap:12px;flex-wrap:wrap;align-items:flex-end;}
.xc-eff__b .xc-fld{flex:1 1 170px;}
.xc-eff__out{padding:0 16px 14px;display:none;}
.xc-eff__row{display:flex;justify-content:space-between;gap:12px;padding:6px 0;border-bottom:1px solid #f1f5f9;font-size:12px;}
.xc-eff__row:last-child{border-bottom:none;}
.xc-eff__src{font-size:10.5px;color:var(--muted);}
.xc-on{color:var(--ok);font-weight:700;} .xc-off{color:var(--danger);font-weight:700;}

.xc-tbl{width:100%;border-collapse:collapse;font-size:11.5px;}
.xc-tbl th{text-align:left;font-size:10px;text-transform:uppercase;letter-spacing:.3px;color:var(--muted);
    padding:7px 10px;border-bottom:1px solid var(--bdr);background:var(--surf);font-weight:700;white-space:nowrap;}
.xc-tbl td{padding:7px 10px;border-bottom:1px solid #f1f5f9;color:var(--txt);vertical-align:top;}
.xc-tbl tr:last-child td{border-bottom:none;}
.xc-scroll{overflow-x:auto;}

.xc-toast{display:none;position:fixed;bottom:22px;right:22px;z-index:3000;background:#fff;border:1px solid var(--bdr);border-left:4px solid var(--ok);
    padding:12px 17px;font-size:12px;box-shadow:0 4px 18px rgba(0,0,0,.14);max-width:360px;line-height:1.5;}
.xc-toast.on{display:block;}
.xc-toast--err{border-left-color:var(--danger);}
.xc-empty{font-size:12px;color:var(--muted);padding:18px 16px;}

@media(max-width:900px){
  .xc-f,.xc-f--wide{grid-template-columns:minmax(0,1fr);gap:8px;}
  .xc-f--dirty{margin-left:0;padding-left:9px;}
  .xc-wrap{padding:16px 14px 40px;}
  .xc-head{padding:16px 14px;}
}
</style>
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<div class="xc-head">
    <h1>Examination Configuration</h1>
    <p>When mark entry runs, and what lecturers and students may do in the portal. Choose who the rules apply to, fill in the form, and save. A rule set for the whole university applies everywhere until a campus, faculty or programme is given one of its own.</p>
</div>

<div class="xc-wrap">

    <%-- The distinction that makes this screen worth having. Without it an operator
         will look here for deadlines, not find them, and set a date somewhere else. --%>
    <div class="xc-note">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg>
        <span><b>Three things can close mark entry, and the strictest one wins.</b>
        The <em>window</em> below is the published schedule &mdash; when entry opens and closes.
        The <em>switch</em> closes it by hand at any time, whatever the dates say.
        The older per-campus <em>deadline</em> on the Deadlines screen still applies as well.
        Lecturers are told which of the three is stopping them, so nobody is sent to the registry about the wrong one.</span>
    </div>

    <%-- Step one: who the rules being edited apply to. Everything below follows from
         this, so it sits at the top and states itself in words underneath. --%>
    <div class="xc-scope">
        <div class="xc-scope__h">
            <h2>1. Who these settings apply to</h2>
            <p>Start with the whole university. Narrow it only where a campus, faculty, programme or study session genuinely differs &mdash; every rule you add here is one more place to look when something behaves unexpectedly.</p>
            <p><strong>Study session</strong> targets a delivery mode (in-service, day, weekend) across every programme that runs it. Use it when the distinction is <em>how</em> a cohort is taught rather than <em>what</em> they study &mdash; in-service cannot be selected as a programme, because every in-service programme also has day or weekend students. A session rule beats the university-wide rule and is beaten by a campus, faculty or programme rule.</p>
        </div>
        <div class="xc-scope__b">
            <div class="xc-fld"><label for="scScope">Applies to</label>
                <select id="scScope">
                    <option value="GLOBAL">The whole university</option>
                    <option value="CAMPUS">One campus</option>
                    <option value="FACULTY">One faculty</option>
                    <option value="PROGRAMME">One programme</option>
                    <option value="SESSION">One study session (e.g. in-service)</option>
                </select>
            </div>
            <div class="xc-fld" id="scWhichWrap"><label for="scValue">Which one</label>
                <select id="scValue" disabled="disabled"><option value="">&mdash;</option></select>
            </div>
            <div class="xc-fld"><label for="scYear">Academic year</label>
                <select id="scYear"><option value="">Every year</option></select>
            </div>
            <div class="xc-fld xc-fld--sem"><label for="scSem">Semester</label>
                <select id="scSem"><option value="0">Every semester</option></select>
            </div>
            <button type="button" class="xc-btn xc-btn--p" id="scGo">Load settings</button>
        </div>
        <div class="xc-scope__now" id="scNow">Loading&hellip;</div>
    </div>

    <div id="xcForm">
        <div class="xc-empty">Loading settings&hellip;</div>
    </div>

    <div class="xc-save" id="scSaveBar" style="display:none;">
        <span class="xc-save__n" id="sbCount">No changes</span>
        <div class="xc-save__r"><input type="text" id="sbNotes" placeholder="Why (optional, recorded in the log)" autocomplete="off" maxlength="240" /></div>
        <div class="xc-save__a">
            <button type="button" class="xc-btn xc-btn--g" id="sbDiscard" disabled="disabled">Discard</button>
            <button type="button" class="xc-btn xc-btn--p" id="sbSave" disabled="disabled">Save changes</button>
        </div>
    </div>

    <%-- Answers "why can this lecturer not enter marks" without reading the table. --%>
    <div class="xc-eff">
        <div class="xc-eff__h">
            <h2>What a lecturer is actually working under</h2>
            <p>Pick a programme to see the rules in force there, and which setting decided each one. This follows the same fallback chain the portal uses.</p>
        </div>
        <div class="xc-eff__b">
            <div class="xc-fld"><label for="efProg">Programme</label><select id="efProg"><option value="">Any</option></select></div>
            <div class="xc-fld"><label for="efCampus">Campus</label><select id="efCampus"><option value="">Any</option></select></div>
            <div class="xc-fld"><label for="efYear">Academic year</label><select id="efYear"><option value="">Every year</option></select></div>
            <div class="xc-fld xc-fld--sem"><label for="efSem">Semester</label><select id="efSem"><option value="0">Every semester</option></select></div>
            <button type="button" class="xc-btn xc-btn--g" id="efGo">Check</button>
        </div>
        <div class="xc-eff__out" id="efOut"></div>
    </div>

    <div class="xc-eff">
        <div class="xc-eff__h" style="padding-bottom:12px;">
            <h2>Every rule currently stored</h2>
            <p>The complete list, so a rule set months ago for one faculty is never a surprise. The university-wide values are the floor everything else falls back to and cannot be removed &mdash; change them above instead.</p>
        </div>
        <div class="xc-scroll" id="xcRules"><div class="xc-empty">Loading&hellip;</div></div>
    </div>

</div>

<div class="xc-toast" id="xcToast"></div>

<script type="text/javascript">
(function () {
    "use strict";

    /* Built with createElement and addEventListener throughout, never by concatenating
       HTML with an inline onclick. A handler written into a string has to survive two
       levels of quoting, and when it does not the whole script stops parsing and the
       page loads as a dead form. */

    var SCOPES = { campuses: [], faculties: [], programmes: [], sessions: [], years: [], currentYear: "" };
    var SETTINGS = [], WINDOWS = [], ALLRULES = [];
    var DIRTY = {};                 // key -> the value the operator has typed
    var BASELINE = {};              // key -> what was loaded, so Discard is exact
    var CURSCOPE = { scopeType: "GLOBAL", scopeValue: "", acadYear: "", semester: 0 };
    var IS_BASE = true, IS_ALL = false, N_TARGETS = 1, LOADING = false;

    // ── small helpers ───────────────────────────────────────────────────────
    function $(id) { return document.getElementById(id); }

    function E(tag, attrs, kids) {
        var n = document.createElement(tag), k;
        if (attrs) for (k in attrs) {
            if (!Object.prototype.hasOwnProperty.call(attrs, k)) continue;
            var v = attrs[k];
            if (v === null || v === undefined) continue;
            if (k === "class") n.className = v;
            else if (k === "text") n.textContent = v;
            else if (k === "html") n.innerHTML = v;          // static markup only
            else if (k.substring(0, 2) === "on") n.addEventListener(k.substring(2), v);
            else n.setAttribute(k, v);
        }
        if (kids) for (var i = 0; i < kids.length; i++) if (kids[i]) n.appendChild(kids[i]);
        return n;
    }
    function clear(n) { while (n.firstChild) n.removeChild(n.firstChild); return n; }

    function opt(value, label, selected) {
        var o = document.createElement("option");
        o.value = value; o.textContent = label;
        if (selected) o.selected = true;
        return o;
    }

    function post(action, body, cb) {
        var x = new XMLHttpRequest();
        x.open("POST", window.location.pathname + "?action=" + action, true);
        x.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        x.onreadystatechange = function () {
            if (x.readyState !== 4) return;
            if (x.status === 200) {
                try { cb(JSON.parse(x.responseText)); }
                catch (e) { cb({ success: false, message: "The server sent something unexpected. Reload the page." }); }
            } else cb({ success: false, message: "Server error (" + x.status + ")." });
        };
        x.onerror = function () { cb({ success: false, message: "Network error. Check your connection." }); };
        x.send(body);
    }
    function form(o) {
        var p = [];
        for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k))
            p.push(encodeURIComponent(k) + "=" + encodeURIComponent(o[k] === null || o[k] === undefined ? "" : o[k]));
        return p.join("&");
    }
    function toast(m, err) {
        var t = $("xcToast");
        t.textContent = m;
        t.className = "xc-toast on" + (err ? " xc-toast--err" : "");
        clearTimeout(t._t);
        t._t = setTimeout(function () { t.className = "xc-toast"; }, err ? 6000 : 3600);
    }

    /* Stored as "yyyy-MM-dd HH:mm"; datetime-local wants a T. Converted at the edge so
       the database keeps exactly one format and nothing downstream has to guess. */
    function toLocal(v) { v = (v || "").trim(); return v === "" ? "" : v.replace(" ", "T").substring(0, 16); }
    function fromLocal(v) { v = (v || "").trim(); return v === "" ? "" : v.replace("T", " ").substring(0, 16); }

    function labelFor(type, value) {
        if (type === "BOOL") return value === "1" ? "On" : "Off";
        if (value === "") return "no limit";
        return value;
    }
    function scopeWords(st, sv) {
        if (st === "GLOBAL") return "The whole university";
        if (sv === ALL) return "All " + pluralOf(st);
        var list = st === "CAMPUS" ? SCOPES.campuses : (st === "FACULTY" ? SCOPES.faculties : (st === "SESSION" ? SCOPES.sessions : SCOPES.programmes));
        for (var i = 0; i < list.length; i++) if (String(list[i].v) === String(sv)) return list[i].t;
        return st.charAt(0) + st.substring(1).toLowerCase() + " " + sv;
    }
    function periodWords(y, s) {
        var a = y ? y : "every year";
        var b = (s && Number(s) > 0) ? "semester " + s : "every semester";
        return a + ", " + b;
    }

    // ── the scope form ──────────────────────────────────────────────────────
    function fillScopePickers() {
        var y = $("scYear"), ey = $("efYear");
        clear(y).appendChild(opt("", "Every year", false));
        clear(ey).appendChild(opt("", "Every year", false));
        (SCOPES.years || []).forEach(function (o) {
            y.appendChild(opt(o.v, o.t, false));
            ey.appendChild(opt(o.v, o.t, false));
        });

        var p = $("efProg"), c = $("efCampus");
        clear(p).appendChild(opt("", "Any", false));
        (SCOPES.programmes || []).forEach(function (o) { p.appendChild(opt(o.v, o.t, false)); });
        clear(c).appendChild(opt("", "Any", false));
        (SCOPES.campuses || []).forEach(function (o) { c.appendChild(opt(o.v, o.t, false)); });

        scopeTypeChanged();
        semestersFor($("scYear").value, $("scSem"));
        semestersFor("", $("efSem"));
    }

    /* Only the semesters that year actually runs. Offering "semester 3" for a two-
       semester year would let somebody store a rule that can never match anything. */
    function semestersFor(year, sel) {
        var n = 3, i;
        for (i = 0; i < (SCOPES.years || []).length; i++)
            if (SCOPES.years[i].v === year) { n = SCOPES.years[i].semesters || 2; break; }
        var keep = sel.value;
        clear(sel).appendChild(opt("0", "Every semester", false));
        for (i = 1; i <= n; i++) sel.appendChild(opt(String(i), "Semester " + i, false));
        sel.value = keep;
        if (!sel.value) sel.value = "0";
    }

    var ALL = "__ALL__";
    function pluralOf(t) { return t === "CAMPUS" ? "campuses" : (t === "FACULTY" ? "faculties" : (t === "SESSION" ? "study sessions" : "programmes")); }

    function scopeTypeChanged() {
        var t = $("scScope").value, sel = $("scValue");
        clear(sel);
        if (t === "GLOBAL") {
            sel.appendChild(opt("", "— not needed —", false));
            sel.disabled = true;
            return;
        }
        sel.disabled = false;
        var list = t === "CAMPUS" ? SCOPES.campuses : (t === "FACULTY" ? SCOPES.faculties : (t === "SESSION" ? SCOPES.sessions : SCOPES.programmes));
        sel.appendChild(opt("", "Choose one…", false));

        /* Applying to every faculty is NOT the same as applying university-wide.
           Faculty rules beat campus rules, so this is the only way to override a
           campus-level rule everywhere at once. It writes one real row per target. */
        sel.appendChild(opt(ALL, "All " + pluralOf(t) + " (" + (list || []).length + ")", false));

        (list || []).forEach(function (o) { sel.appendChild(opt(o.v, o.t, false)); });
    }

    function describeScope() {
        var n = $("scNow");
        clear(n);
        n.appendChild(E("span", { text: "You are editing: " }));
        n.appendChild(E("b", { text: scopeWords(CURSCOPE.scopeType, CURSCOPE.scopeValue) }));
        n.appendChild(E("span", { text: " · " + periodWords(CURSCOPE.acadYear, CURSCOPE.semester) }));
        if (!IS_BASE)
            n.appendChild(E("span", {
                class: "xc-tag xc-tag--own",
                text: "override"
            }));
        else
            n.appendChild(E("span", { class: "xc-tag xc-tag--inh", text: "base values" }));

        // Say plainly that a save here is a bulk write, before it happens rather than after.
        if (IS_ALL)
            n.appendChild(E("span", {
                style: "font-size:11px;color:#8a6100;",
                text: "Saving writes a separate rule for each of the " + N_TARGETS + " "
                    + pluralOf(CURSCOPE.scopeType) + "."
            }));
    }

    // ── loading a scope ─────────────────────────────────────────────────────
    function loadScope() {
        var st = $("scScope").value, sv = st === "GLOBAL" ? "" : $("scValue").value;
        if (st !== "GLOBAL" && sv === "") { toast("Choose which one this applies to.", true); return; }

        if (countDirty() > 0 && !confirm("You have unsaved changes.\n\nLoad a different scope and lose them?")) return;

        CURSCOPE = { scopeType: st, scopeValue: sv, acadYear: $("scYear").value, semester: Number($("scSem").value) || 0 };
        DIRTY = {}; BASELINE = {};
        LOADING = true;
        $("xcForm").innerHTML = '<div class="xc-empty">Loading settings…</div>';

        post("Scoped", form(CURSCOPE), function (d) {
            LOADING = false;
            if (!d || !d.success) {
                clear($("xcForm")).appendChild(E("div", { class: "xc-empty", text: (d && d.message) || "Could not load." }));
                $("scSaveBar").style.display = "none";
                return;
            }
            SETTINGS = d.settings || [];
            WINDOWS = d.windows || [];
            IS_BASE = !!d.isBase;
            IS_ALL = !!d.isAll;
            N_TARGETS = Number(d.targets) || 1;
            SETTINGS.forEach(function (s) { BASELINE[s.key] = s.value; });
            describeScope();
            renderForm();
            $("scSaveBar").style.display = "flex";
            updateSaveBar();
        });
    }

    // ── rendering the form ──────────────────────────────────────────────────
    function settingByKey(k) {
        for (var i = 0; i < SETTINGS.length; i++) if (SETTINGS[i].key === k) return SETTINGS[i];
        return null;
    }
    function currentValue(k) {
        if (Object.prototype.hasOwnProperty.call(DIRTY, k)) return DIRTY[k];
        var s = settingByKey(k);
        return s ? s.value : "";
    }
    function countDirty() {
        var n = 0;
        for (var k in DIRTY) if (Object.prototype.hasOwnProperty.call(DIRTY, k)) n++;
        return n;
    }
    function markDirty(key, value) {
        if (String(value) === String(BASELINE[key] === undefined ? "" : BASELINE[key])) delete DIRTY[key];
        else DIRTY[key] = value;
        updateSaveBar();
        repaintDirty();
    }

    /* Repainted by walking the rows rather than looking one up by key: a window row owns
       TWO keys, and highlighting by the key that happened to change would leave the row
       unmarked whenever the closing date was the one edited. */
    function repaintDirty() {
        var rows = document.querySelectorAll("[data-keys]");
        for (var i = 0; i < rows.length; i++) {
            var keys = (rows[i].getAttribute("data-keys") || "").split(","), dirty = false;
            for (var j = 0; j < keys.length; j++)
                if (Object.prototype.hasOwnProperty.call(DIRTY, keys[j])) { dirty = true; break; }
            rows[i].className = "xc-f"
                + (rows[i].getAttribute("data-wide") ? " xc-f--wide" : "")
                + (dirty ? " xc-f--dirty" : "");
        }
    }

    /* Three states, not two. Under "all …" the targets can disagree, and showing one of
       their values as though it were the answer would tell the operator something untrue
       about the rest. A mixed field is left alone unless it is deliberately changed. */
    function metaRow(s) {
        var meta = E("div", { class: "xc-f__meta" });

        if (s.mixed) {
            meta.appendChild(E("span", { class: "xc-tag xc-tag--mix", text: "mixed" }));
            meta.appendChild(E("span", {
                style: "font-size:11px;color:#8a6100;",
                text: "set on " + s.setOn + " of " + s.targets + " — change this field to give them all the same value"
            }));
        } else if (s.isOwn) {
            meta.appendChild(E("span", { class: "xc-tag xc-tag--own", text: "set here" }));
            if (IS_ALL && s.targets > 1)
                meta.appendChild(E("span", { style: "font-size:11px;color:#64748b;", text: "on all " + s.targets }));
        } else {
            meta.appendChild(E("span", { class: "xc-tag xc-tag--inh", text: "inherited" }));
            meta.appendChild(E("span", {
                style: "font-size:11px;color:#64748b;",
                text: s.source ? "from " + s.source : "using the built-in default"
            }));
        }

        if ((s.isOwn || s.mixed) && !IS_BASE)
            meta.appendChild(E("button", {
                type: "button", class: "xc-link xc-link--d", text: "Remove this rule",
                onclick: function () { removeRule(s); }
            }));
        return meta;
    }

    function fieldRow(s) {
        var ctl;
        if (s.type === "BOOL") {
            ctl = E("select", {
                onchange: function () { markDirty(s.key, this.value); }
            }, [opt("1", "On", currentValue(s.key) === "1"), opt("0", "Off", currentValue(s.key) !== "1")]);
        } else if (s.type === "INT") {
            ctl = E("input", {
                type: "number", min: "0", max: "1000", step: "1", value: currentValue(s.key),
                onchange: function () { markDirty(s.key, this.value); },
                oninput: function () { markDirty(s.key, this.value); }
            });
        } else {
            ctl = E("input", {
                type: "text", value: currentValue(s.key),
                onchange: function () { markDirty(s.key, this.value); }
            });
        }

        return E("div", { class: "xc-f", "data-keys": s.key }, [
            E("div", {}, [
                E("div", { class: "xc-f__lbl", text: s.title }),
                E("div", { class: "xc-f__help", text: s.help })
            ]),
            E("div", { class: "xc-f__ctl" }, [ctl, metaRow(s)])
        ]);
    }

    function windowRow(w) {
        var so = settingByKey(w.opens), sc = settingByKey(w.closes);
        if (!so || !sc) return null;

        var status = E("div", { class: "xc-win__s" });

        var inOpen = E("input", {
            type: "datetime-local", value: toLocal(currentValue(w.opens)),
            onchange: function () { markDirty(w.opens, fromLocal(this.value)); paintStatus(); }
        });
        var inClose = E("input", {
            type: "datetime-local", value: toLocal(currentValue(w.closes)),
            onchange: function () { markDirty(w.closes, fromLocal(this.value)); paintStatus(); }
        });

        function paintStatus() {
            var o = fromLocal(inOpen.value), c = fromLocal(inClose.value);
            clear(status);
            if (o && c && c <= o) {                       // both are yyyy-MM-dd HH:mm, so string order is time order
                status.className = "xc-win__s xc-win__s--bad";
                status.textContent = "This would close before it opens. Entry would never be possible.";
            } else if (!o && !c) {
                status.className = "xc-win__s xc-win__s--none";
                status.textContent = "No limits set — entry is open whenever the switch below allows it.";
            } else {
                status.className = "xc-win__s xc-win__s--ok";
                status.textContent = (o ? "Opens " + o : "No opening limit") + "  ·  " + (c ? "closes " + c : "no closing limit");
            }
        }
        paintStatus();

        var clearBtn = E("button", {
            type: "button", class: "xc-mini", text: "Clear both",
            title: "Remove both limits, so this window places no restriction",
            onclick: function () {
                inOpen.value = ""; inClose.value = "";
                markDirty(w.opens, ""); markDirty(w.closes, "");
                paintStatus();
            }
        });

        // The pair is one row on screen, so its state is the state of both ends together.
        var meta = E("div", { class: "xc-f__meta" });
        if (so.mixed || sc.mixed) {
            meta.appendChild(E("span", { class: "xc-tag xc-tag--mix", text: "mixed" }));
            meta.appendChild(E("span", {
                style: "font-size:11px;color:#8a6100;",
                text: "the " + pluralOf(CURSCOPE.scopeType) + " do not all have the same dates — set both to give them one window"
            }));
        } else if (so.isOwn || sc.isOwn) {
            meta.appendChild(E("span", { class: "xc-tag xc-tag--own", text: "set here" }));
            if (IS_ALL && so.targets > 1)
                meta.appendChild(E("span", { style: "font-size:11px;color:#64748b;", text: "on all " + so.targets }));
        } else {
            meta.appendChild(E("span", { class: "xc-tag xc-tag--inh", text: "inherited" }));
            meta.appendChild(E("span", {
                style: "font-size:11px;color:#64748b;",
                text: so.source ? "from " + so.source : "using the built-in default"
            }));
        }
        if ((so.isOwn || sc.isOwn || so.mixed || sc.mixed) && !IS_BASE)
            meta.appendChild(E("button", {
                type: "button", class: "xc-link xc-link--d", text: "Remove this rule",
                onclick: function () { removeRule(so, sc); }
            }));

        var row = E("div", { class: "xc-f xc-f--wide", "data-keys": w.opens + "," + w.closes, "data-wide": "1" }, [
            E("div", {}, [
                E("div", { class: "xc-f__lbl", text: w.title }),
                E("div", { class: "xc-f__help", text: w.help })
            ]),
            E("div", { class: "xc-f__ctl" }, [
                E("div", { class: "xc-win" }, [
                    E("div", { class: "xc-fld" }, [E("label", { text: "Opens" }), inOpen]),
                    E("div", { class: "xc-fld" }, [E("label", { text: "Closes" }), inClose]),
                    clearBtn
                ]),
                status, meta
            ])
        ]);
        return row;
    }

    function renderForm() {
        var host = clear($("xcForm"));
        var inWindow = {};
        WINDOWS.forEach(function (w) { inWindow[w.opens] = 1; inWindow[w.closes] = 1; });

        // group in catalogue order
        var order = [], byGroup = {};
        SETTINGS.forEach(function (s) {
            if (!byGroup[s.group]) { byGroup[s.group] = []; order.push(s.group); }
            byGroup[s.group].push(s);
        });

        var n = 2;
        order.forEach(function (g) {
            var body = E("div", { class: "xc-sec__b" });
            var placed = 0;

            // the windows belong to the schedule group and are drawn as From/To pairs
            WINDOWS.forEach(function (w) {
                var s = settingByKey(w.opens);
                if (!s || s.group !== g) return;
                var r = windowRow(w);
                if (r) { body.appendChild(r); placed++; }
            });

            byGroup[g].forEach(function (s) {
                if (inWindow[s.key]) return;
                body.appendChild(fieldRow(s)); placed++;
            });

            if (!placed) return;
            host.appendChild(E("div", { class: "xc-sec" }, [
                E("div", { class: "xc-sec__h" }, [E("h2", { text: (n++) + ". " + g })]),
                body
            ]));
        });
    }

    // ── save / discard / remove ─────────────────────────────────────────────
    function updateSaveBar() {
        var n = countDirty();
        var lbl = $("sbCount");
        lbl.textContent = n === 0 ? "No changes" : (n === 1 ? "1 unsaved change" : n + " unsaved changes");
        lbl.className = "xc-save__n" + (n ? " on" : "");
        $("sbSave").disabled = (n === 0);
        $("sbDiscard").disabled = (n === 0);
    }

    function saveAll() {
        var items = [], k;
        for (k in DIRTY) if (Object.prototype.hasOwnProperty.call(DIRTY, k)) items.push({ key: k, value: DIRTY[k] });
        if (!items.length) return;

        var btn = $("sbSave");
        btn.disabled = true; btn.textContent = "Saving…";
        var body = form({
            scopeType: CURSCOPE.scopeType, scopeValue: CURSCOPE.scopeValue,
            acadYear: CURSCOPE.acadYear, semester: CURSCOPE.semester,
            notes: $("sbNotes").value, items: JSON.stringify(items)
        });
        post("SaveMany", body, function (d) {
            btn.textContent = "Save changes";
            if (d && d.success) {
                toast(d.message || "Saved.");
                $("sbNotes").value = "";
                DIRTY = {};
                loadScope();
                loadRules();
            } else {
                btn.disabled = false;
                toast((d && d.message) || "Could not save.", true);
            }
        });
    }

    /* Removes by scope, not by row id: "all faculties" is one row PER faculty, and an
       id-based remove would silently clear only the first of them. */
    function removeRule(s, also) {
        if (!s) return;
        var where = scopeWords(CURSCOPE.scopeType, CURSCOPE.scopeValue);
        if (!confirm("Remove this rule?\n\n\"" + s.title + "\" will fall back to the wider setting for " + where + ".")) return;

        var keys = [s.key];
        if (also && also.key) keys.push(also.key);

        var left = keys.length, failed = null;
        keys.forEach(function (k) {
            post("RemoveScoped", form({
                key: k, scopeType: CURSCOPE.scopeType, scopeValue: CURSCOPE.scopeValue,
                acadYear: CURSCOPE.acadYear, semester: CURSCOPE.semester
            }), function (d) {
                if (!d || !d.success) failed = (d && d.message) || "Could not remove.";
                if (--left === 0) {
                    if (failed) toast(failed, true); else toast("Rule removed. The wider setting applies again.");
                    loadScope(); loadRules();
                }
            });
        });
    }

    // ── the diagnostic panel ────────────────────────────────────────────────
    function showEffective() {
        var body = form({
            programme: $("efProg").value, campus: $("efCampus").value,
            acadYear: $("efYear").value, semester: $("efSem").value
        });
        post("Effective", body, function (d) {
            var out = $("efOut");
            out.style.display = "block";
            clear(out);
            if (!d || !d.success) {
                out.appendChild(E("div", { style: "color:#b42318;font-size:12px;", text: (d && d.message) || "Could not check." }));
                return;
            }
            if (d.faculty)
                out.appendChild(E("div", { style: "font-size:11px;color:#64748b;margin-bottom:7px;", text: "Faculty resolved from the programme: " + d.faculty }));
            (d.settings || []).forEach(function (s) {
                var right = E("span", { style: "text-align:right;" });
                if (s.type === "BOOL")
                    right.appendChild(E("span", { class: s.value === "1" ? "xc-on" : "xc-off", text: s.value === "1" ? "On" : "Off" }));
                else
                    right.appendChild(E("span", { text: s.value === "" ? "—" : s.value }));
                right.appendChild(E("div", { class: "xc-eff__src", text: s.source || "not configured" }));
                out.appendChild(E("div", { class: "xc-eff__row" }, [E("span", { text: s.title }), right]));
            });
        });
    }

    // ── every rule stored ───────────────────────────────────────────────────
    function loadRules() {
        post("List", "", function (d) {
            var host = clear($("xcRules"));
            if (!d || !d.success) {
                host.appendChild(E("div", { class: "xc-empty", text: (d && d.message) || "Could not load." }));
                return;
            }
            SCOPES = d.scopes || SCOPES;
            var rows = [];
            (d.settings || []).forEach(function (s) {
                (s.rules || []).forEach(function (r) { rows.push({ s: s, r: r }); });
            });
            if (!rows.length) { host.appendChild(E("div", { class: "xc-empty", text: "Nothing stored yet." })); return; }

            // overrides first: they are the ones that surprise people
            rows.sort(function (a, b) {
                var ra = a.r.scopeType === "GLOBAL" ? 1 : 0, rb = b.r.scopeType === "GLOBAL" ? 1 : 0;
                if (ra !== rb) return ra - rb;
                return a.s.title < b.s.title ? -1 : (a.s.title > b.s.title ? 1 : 0);
            });

            var tb = E("tbody");
            rows.forEach(function (x) {
                var isBaseRow = x.r.scopeType === "GLOBAL" && !x.r.acadYear && !Number(x.r.semester);
                var act = E("td");
                if (isBaseRow) act.appendChild(E("span", { style: "font-size:10.5px;color:#64748b;", text: "base value" }));
                else act.appendChild(E("button", {
                    type: "button", class: "xc-link xc-link--d", text: "Remove",
                    onclick: function () {
                        if (!confirm("Remove this rule?\n\n\"" + x.s.title + "\" for " + scopeWords(x.r.scopeType, x.r.scopeValue) + " will fall back to the wider setting.")) return;
                        post("Delete", form({ id: x.r.id }), function (d2) {
                            toast((d2 && d2.message) || "Done.", !(d2 && d2.success));
                            if (d2 && d2.success) { loadRules(); loadScope(); }
                        });
                    }
                }));

                tb.appendChild(E("tr", {}, [
                    E("td", { text: x.s.title }),
                    E("td", { text: scopeWords(x.r.scopeType, x.r.scopeValue) }),
                    E("td", { text: periodWords(x.r.acadYear, x.r.semester) }),
                    E("td", { text: labelFor(x.s.type, x.r.value) }),
                    E("td", { text: (x.r.updatedBy || "") + (x.r.updatedAt ? " · " + x.r.updatedAt : "") }),
                    act
                ]));
            });

            host.appendChild(E("table", { class: "xc-tbl" }, [
                E("thead", {}, [E("tr", {}, [
                    E("th", { text: "Setting" }), E("th", { text: "Applies to" }), E("th", { text: "Period" }),
                    E("th", { text: "Value" }), E("th", { text: "Last changed" }), E("th", { text: "" })
                ])]),
                tb
            ]));
        });
    }

    // ── wiring ──────────────────────────────────────────────────────────────
    $("scScope").addEventListener("change", scopeTypeChanged);
    $("scYear").addEventListener("change", function () { semestersFor(this.value, $("scSem")); });
    $("efYear").addEventListener("change", function () { semestersFor(this.value, $("efSem")); });
    $("scGo").addEventListener("click", loadScope);
    $("sbSave").addEventListener("click", saveAll);
    $("sbDiscard").addEventListener("click", function () {
        if (!countDirty()) return;
        if (!confirm("Discard your unsaved changes?")) return;
        DIRTY = {}; renderForm(); updateSaveBar();
    });
    $("efGo").addEventListener("click", showEffective);

    // A half-typed rule that closes mark entry for a faculty is worth one question.
    window.addEventListener("beforeunload", function (e) {
        if (countDirty() > 0) { e.preventDefault(); e.returnValue = ""; return ""; }
    });

    // ── start ───────────────────────────────────────────────────────────────
    post("List", "", function (d) {
        if (!d || !d.success) {
            clear($("xcForm")).appendChild(E("div", { class: "xc-empty", text: (d && d.message) || "Could not load." }));
            $("scNow").textContent = (d && d.message) || "Could not load.";
            return;
        }
        SCOPES = d.scopes || SCOPES;
        fillScopePickers();

        /* The form opens on the BASE values — the whole university, every year, every
           semester — because that is the row the seeded settings live in and the one an
           operator almost always means to change. Defaulting to the current year would
           look helpful and be a trap: the scope would no longer be the base row, every
           field would read "inherited", and the first save would quietly create a second
           set of year-specific rules shadowing the ones already there.

           The diagnostic panel below is a different question — "what applies to a real
           lecturer right now" — so that one does start on the current year. */
        if (SCOPES.currentYear) {
            $("efYear").value = SCOPES.currentYear;
            semestersFor(SCOPES.currentYear, $("efSem"));
        }
        loadScope();
        loadRules();
    });
})();
</script>
</asp:Content>
