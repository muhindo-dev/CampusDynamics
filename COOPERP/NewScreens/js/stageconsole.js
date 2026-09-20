/* Shared client for the staged-marks consoles (Capture / Approve / Publish).
   Page-agnostic: AJAX targets location.pathname so one file serves all three.
   The stage ACTION is a modal wizard (params → preview → commit). The page itself
   only browses the queue + funnel + session history (each session is click-to-view). */
(function () {
'use strict';
var STAGE = { canAct: false, name: '', fromStage: '', fromLabel: '', toLabel: '' };
var PAGE = 1, REC = 0, FAILED_ONLY = false;

function qs(id) { return document.getElementById(id); }
function esc(s) { return s ? String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;') : ''; }
function nz(v) { return (v === null || v === undefined || v === '') ? '—' : v; }
function num(v) { return (v === null || v === undefined || v === '') ? '—' : Number(v).toLocaleString(); }
function toast(m, err) {
    var t = document.createElement('div'); t.textContent = m;
    t.style.cssText = 'position:fixed;top:18px;right:18px;z-index:11000;padding:10px 16px;font-size:13px;font-weight:600;color:#fff;border-radius:0;box-shadow:0 4px 16px rgba(0,0,0,.2);background:' + (err ? '#dc3545' : '#16a34a');
    document.body.appendChild(t); setTimeout(function () { t.style.transition = 'opacity .4s'; t.style.opacity = '0'; setTimeout(function () { t.remove(); }, 400); }, 2800);
}
function call(method, params, cb, opts) {
    opts = opts || {};
    var x = new XMLHttpRequest();
    x.open('POST', location.pathname + '/' + method, true);
    x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
    // A long publish batch can outlive the default socket behaviour. Give callers an
    // explicit budget and a distinguishable 'timeout' outcome, so the UI can fall back to
    // polling the server for the real state instead of guessing that the work failed.
    if (opts.timeoutMs) x.timeout = opts.timeoutMs;
    x.ontimeout = function () { cb({ success: false, timedOut: true, message: 'The request took longer than expected.' }); };
    x.onload = function () { try { var o = JSON.parse(x.responseText); cb(typeof o.d === 'string' ? JSON.parse(o.d) : o.d); } catch (e) { cb({ success: false, message: 'Parse error' }); } };
    x.onerror = function () { cb({ success: false, networkError: true, message: 'Network error' }); };
    x.send(JSON.stringify(params || {}));
}
function show(m) { m.style.display = 'flex'; } function hide(m) { m.style.display = 'none'; }

// ── GET-driven filters: the year/sem/programme/search/failed/page state lives in the URL query
// string, so the view is bookmarkable, shareable and survives a reload / back-forward. The server
// (Browse) already filters on exactly these params. ──
function scReadUrl() {
    var p; try { p = new URLSearchParams(location.search); } catch (e) { return; }
    var y = qs('sc-year'), sm = qs('sc-sem'), pr = qs('sc-prog'), q = qs('sc-q');
    if (y)  y.value  = p.get('year') || '';
    if (sm) sm.value = p.get('sem')  || '';
    if (pr) pr.value = p.get('prog') || '';
    if (q)  q.value  = p.get('q')    || '';
    FAILED_ONLY = (p.get('failed') === '1');
    PAGE = parseInt(p.get('page') || '1', 10) || 1;
    var tb = qs('sc-failed-toggle');
    if (tb) {
        if (FAILED_ONLY) { tb.classList.add('sc-btn--danger'); tb.innerHTML = '&#10003; Showing failed (F) only'; }
        else { tb.classList.remove('sc-btn--danger'); tb.innerHTML = '&#9873; Show failed (F) only'; }
    }
}
function scPushUrl() {
    var parts = [];
    function add(k, v) { if (v !== undefined && v !== null && v !== '') parts.push(encodeURIComponent(k) + '=' + encodeURIComponent(v)); }
    add('year', qs('sc-year') ? qs('sc-year').value : '');
    add('sem',  qs('sc-sem')  ? qs('sc-sem').value  : '');
    add('prog', qs('sc-prog') ? qs('sc-prog').value : '');
    add('q',    qs('sc-q')    ? qs('sc-q').value.trim() : '');
    if (FAILED_ONLY) add('failed', '1');
    if (PAGE > 1) add('page', PAGE);
    try { history.pushState(null, '', location.pathname + (parts.length ? ('?' + parts.join('&')) : '')); } catch (e) {}
}
window.onpopstate = function () { scReadUrl(); loadStats(); loadBrowse(PAGE); };

function boot() {
    var qbox = qs('sc-q');
    if (qbox && !qbox._scEnter) { qbox._scEnter = 1; qbox.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.keyCode === 13) { e.preventDefault(); SC.apply(); } }); }
    call('Init', {}, function (d) {
        if (!d || !d.success) { qs('sc-scope').textContent = (d && d.message) || 'Failed to initialise.'; return; }
        STAGE.canAct = d.canAct; STAGE.name = d.stage; STAGE.fromStage = d.fromStage; STAGE.fromLabel = d.fromLabel; STAGE.toLabel = d.toLabel;
        qs('sc-scope').innerHTML = 'Scope: <strong>' + esc(d.scope.label) + '</strong>';
        var chip = qs('sc-role'); chip.textContent = d.scope.isAdmin ? 'Administrator' : (d.scope.role || 'Scoped');
        setText('sc-from-label', d.fromLabel); setText('sc-to-label', d.toLabel);
        var arr = document.querySelectorAll('.sc-to-label'); for (var i=0;i<arr.length;i++) arr[i].textContent = d.toLabel;
        fillSelect('sc-year', d.years, 'All years', true);
        fillSelect('sc-prog', d.programmes, 'All programmes', false);
        fillSelect('sc-p-prog', d.programmes, 'All programmes in my scope', false);
        fillSelect('sc-p-year', d.years, 'All years', true);   // wizard academic-year dropdown
        var launch = qs('sc-launch');
        if (!d.canAct && launch) { launch.disabled = true; var n = qs('sc-cantact'); if (n) n.style.display = 'inline-block'; }
        var adv = qs('sc-advance-btn');
        if (adv) { adv.innerHTML = '&#10003; ' + (VERB[d.stage] || 'Advance') + ' selected'; if (!d.canAct) adv.style.display = 'none'; }
        scReadUrl();               // apply any year/sem/prog/q/failed/page from the URL (after selects are populated)
        loadBrowse(PAGE); loadStats();   // queue first - the funnel is only context, and
                                         // the session lock serialises these two anyway
    });
}
function setText(id, v) { var e = qs(id); if (e) e.textContent = v; }
function fillSelect(id, items, allText, plain) {
    var el = qs(id); if (!el) return; var h = '<option value="">' + allText + '</option>';
    (items || []).forEach(function (it) { if (plain) h += '<option value="' + esc(it) + '">' + esc(it) + '</option>';
        else h += '<option value="' + esc(it.value) + '">' + esc(it.text) + '</option>'; });
    el.innerHTML = h;
}
function loadStats() {
    call('Stats', {}, function (d) { if (!d || !d.success) return;
        set('sc-not', d.notEntered); set('sc-entered', d.entered); set('sc-captured', d.captured);
        set('sc-approved', d.approved); set('sc-published', d.published); set('sc-actionable', d.actionable); });
}
function set(id, v) { var e = qs(id); if (e) e.textContent = (v || 0).toLocaleString(); }

function loadBrowse(p) {
    PAGE = p || 1;
    var b = qs('sc-body'); b.innerHTML = '<tr><td colspan="10" class="sc-empty">Loading…</td></tr>';
    call('Browse', { year: qs('sc-year').value, sem: qs('sc-sem').value, prog: qs('sc-prog').value, q: qs('sc-q').value, page: PAGE, pageSize: 50, failedOnly: FAILED_ONLY }, function (d) {
        if (!d || !d.success) { b.innerHTML = '<tr><td colspan="10" class="sc-empty" style="color:#b42318">' + esc((d && d.message) || 'Error') + '</td></tr>'; return; }
        if (!d.rows.length) { b.innerHTML = '<tr><td colspan="10" class="sc-empty">' + (FAILED_ONLY ? 'No failed (F) marks' : 'No marks') + ' at the ' + esc(STAGE.fromLabel) + ' stage in your scope.</td></tr>'; qs('sc-pager').textContent=''; return; }
        b.innerHTML = d.rows.map(function (r) {
            return '<tr' + (r.failed ? ' class="sc-row--f"' : '') + '>' +
                '<td><input type="checkbox" class="sc-chk" value="' + r.id + '"></td>' +
                '<td><span class="sc-code">' + esc(r.studentNo || r.regno) + '</span><div class="sc-sub">' + esc(r.name) + '</div></td>' +
                '<td>' + esc(r.course) + '<div class="sc-sub">' + esc(r.courseName) + '</div></td>' +
                '<td>' + (r.lecturer ? esc(r.lecturer) : '<span class="sc-sub" style="opacity:.6">— unassigned —</span>') + '</td>' +
                '<td>' + esc(r.progName) + '</td>' +
                '<td class="sc-c">' + esc(r.acadYear) + ' / S' + esc(r.semester) + '</td>' +
                '<td class="sc-c">' + nz(r.cw) + '</td>' + '<td class="sc-c">' + nz(r.exam) + '</td>' +
                '<td class="sc-c"><strong>' + nz(r.total) + '</strong></td>' +
                '<td class="sc-c">' + (r.grade ? ('<span class="sc-grade' + (r.failed ? ' sc-grade--f' : '') + '">' + esc(r.grade) + '</span>') : '—') + '</td>' +
                '<td class="sc-track">' + trackerCell(r) + '</td>' +
            '</tr>'; }).join('');
        qs('sc-pager').innerHTML = pager(d.page, d.pages, d.total);
    });
}
/* -- Tracker -----------------------------------------------------------------
   Who did what to this mark. Two independent facts, both already recorded on the
   row and neither previously shown anywhere in these consoles:

     markBy  - the last change to the MARKS themselves, from the trigger-written
               provisional-marks audit: the lecturer who entered or edited them,
               when, and from which screen.
     stageBy - who moved the mark into the stage it is sitting at now, i.e. who
               captured / approved / published it.

   A row can carry either, both, or neither. Neither is the honest answer for marks
   that predate the audit trigger and the staged workflow, so the cell says so rather
   than sitting empty and looking like a rendering fault. */
var MARK_ACT   = { INSERT: 'Marks entered', UPDATE: 'Marks edited', MIGRATE: 'Migrated in' };
var STAGE_VERB = { ENTERED: 'Entered', CAPTURED: 'Captured', APPROVED: 'Approved',
                   PUBLISHED: 'Published', NOT_ENTERED: 'Cleared' };

/* Attribution is written by several different paths and arrives in several shapes:
   "Lecturer: Nabbira Jackline (nabbiiraj@mru.ac.ug)", "ndagirei@mru.ac.ug", "Muhindo
   mubaraka". Show the person, not the packaging. */
function who(v) {
    if (!v) return '';
    var t = String(v).replace(/^\s*Lecturer\s*:\s*/i, '').replace(/\s*\([^)]*\)\s*$/, '').trim();
    return t || String(v).trim();
}
function viaLabel(v) {
    if (!v) return '';
    if (v.indexOf('Portal:') === 0) return 'portal';
    if (v.indexOf('ODEL') === 0)    return 'ODEL';
    if (v.indexOf('eadmin') === 0)  return 'eadmin';
    return v;
}
function trackerCell(r) {
    var h = '';
    if (r.markBy) {
        h += '<span class="sc-code">' + esc(who(r.markBy)) + '</span>' +
             '<div class="sc-sub">' + esc(MARK_ACT[r.markAct] || 'Marks changed') +
             (r.markAt ? ' · ' + esc(r.markAt) : '') +
             (viaLabel(r.markVia) ? ' · ' + esc(viaLabel(r.markVia)) : '') + '</div>';
    }
    if (r.stageBy) {
        var verb = STAGE_VERB[STAGE.fromStage] || 'Moved here';
        // With no marks-audit entry this is all the row knows, so lead with it.
        h += r.markBy
            ? '<div class="sc-sub">' + esc(verb) + ' by ' + esc(who(r.stageBy)) +
              (r.stageAt ? ' · ' + esc(r.stageAt) : '') + '</div>'
            : '<span class="sc-code">' + esc(who(r.stageBy)) + '</span><div class="sc-sub">' +
              esc(verb) + (r.stageAt ? ' · ' + esc(r.stageAt) : '') + '</div>';
    }
    if (!h) h = '<span class="sc-track__none">not recorded</span>';
    return h;
}
function pager(p, pages, total) {
    return '<span class="sc-pginfo">' + total.toLocaleString() + ' mark(s) · page ' + p + ' / ' + pages + '</span><span style="margin-left:auto"></span>' +
        '<button type="button" class="sc-btn sc-btn--ghost" ' + (p <= 1 ? 'disabled' : '') + ' onclick="SC.go(' + (p - 1) + ')">&laquo; Prev</button>' +
        '<button type="button" class="sc-btn sc-btn--ghost" ' + (p >= pages ? 'disabled' : '') + ' onclick="SC.go(' + (p + 1) + ')">Next &raquo;</button>';
}

/* ── Wizard: params → preview → commit (all in a modal) ── */
function openWizard() {
    if (!STAGE.canAct) { toast('Your role cannot perform this stage.', true); return; }
    // reset to params step
    qs('sc-p-year').value = ''; qs('sc-p-sem').value = ''; qs('sc-p-yos').value = '';
    qs('sc-p-prog').value = ''; qs('sc-p-all').checked = false; qs('sc-p-notes').value = '';
    var ff = qs('sc-p-fail'); if (ff) ff.value = 'INCLUDE';
    qs('sc-wiz-params').style.display = ''; qs('sc-wiz-preview').style.display = 'none'; qs('sc-wiz-preview').innerHTML = '';
    qs('sc-btn-preview').style.display = ''; qs('sc-btn-back').style.display = 'none'; qs('sc-commit-btn').style.display = 'none';
    REC = 0; show(qs('sc-modal'));
}
function doPreview() {
    var pv = qs('sc-wiz-preview'); pv.innerHTML = 'Preparing preview…';
    qs('sc-wiz-params').style.display = 'none'; pv.style.display = '';
    qs('sc-btn-preview').style.display = 'none'; qs('sc-btn-back').style.display = '';
    call('CreateAndPreview', {
        prog: qs('sc-p-prog').value, yos: parseInt(qs('sc-p-yos').value || '0', 10),
        acadYear: qs('sc-p-year').value, sem: parseInt(qs('sc-p-sem').value || '0', 10),
        all: qs('sc-p-all').checked, notes: qs('sc-p-notes').value,
        failMode: (qs('sc-p-fail') && qs('sc-p-fail').value) ? qs('sc-p-fail').value : 'INCLUDE'
    }, function (d) {
        if (!d || !d.success || !d.preview || !d.preview.success) { pv.innerHTML = '<div style="color:#b42318">' + esc((d && (d.message || (d.preview && d.preview.message))) || 'Preview failed.') + '</div>'; return; }
        var p = d.preview, st = p.stats || {}; REC = p.recordId;
        if (!p.count) { pv.innerHTML = '<div class="sc-empty">No marks match these parameters at the ' + esc(STAGE.fromLabel) + ' stage.</div>'; qs('sc-commit-btn').style.display = 'none'; return; }
        pv.innerHTML = '<div class="sc-pv-hd">Move <b>' + p.count.toLocaleString() + '</b> mark(s): <b>' + esc(STAGE.fromLabel) + '</b> &rarr; <b>' + esc(STAGE.toLabel) + '</b></div>' +
            statsBlock(st) + breakdownTbl(p.breakdown);
        var cb = qs('sc-commit-btn'); cb.style.display = ''; cb.textContent = 'Commit ' + p.count.toLocaleString() + ' mark(s)';
    });
}
function wizBack() {
    if (COMMITTING) return;                       // can't rewind a run that is under way
    if (REC) { call('Cancel', { recordId: REC }, function () {}); REC = 0; }
    stopPoll();
    qs('sc-wiz-preview').style.display = 'none'; qs('sc-wiz-params').style.display = '';
    qs('sc-btn-preview').style.display = ''; qs('sc-btn-back').style.display = 'none'; qs('sc-commit-btn').style.display = 'none';
}
/* ── Commit ──────────────────────────────────────────────────────────────────
   Publishing is a batch that can run for a while. The server commits it in small
   chunks and records progress against the session, so the client's job is to (a)
   never let the operator fire it twice, (b) show what is actually happening, and
   (c) recover sensibly if the HTTP request dies before the batch does — the work
   already committed survives, and re-running the session picks up the remainder. */
var COMMITTING = false, POLL = null;

function stopPoll() { if (POLL) { clearInterval(POLL); POLL = null; } }

function setCommitBusy(busy, label) {
    COMMITTING = busy;
    var cb = qs('sc-commit-btn'), bk = qs('sc-btn-back'), x = document.querySelector('#sc-modal .sc-modal__x');
    if (cb) { cb.disabled = busy; if (label) cb.textContent = label; }
    if (bk) bk.style.display = busy ? 'none' : '';
    if (x) x.style.opacity = busy ? '.35' : '';
}

function renderProgress(p) {
    var pv = qs('sc-wiz-preview'); if (!pv) return;
    var bar = qs('sc-prog-bar'), txt = qs('sc-prog-txt');
    if (!bar) {
        pv.insertAdjacentHTML('afterbegin',
            '<div class="sc-prog" id="sc-prog">' +
            '  <div class="sc-prog__track"><div class="sc-prog__fill" id="sc-prog-bar"></div></div>' +
            '  <div class="sc-prog__txt" id="sc-prog-txt">Starting…</div>' +
            '</div>');
        bar = qs('sc-prog-bar'); txt = qs('sc-prog-txt');
    }
    var pct = p.total > 0 ? Math.min(100, Math.round((p.done / p.total) * 100)) : 0;
    bar.style.width = pct + '%';
    txt.textContent = p.total > 0
        ? (p.done.toLocaleString() + ' of ' + p.total.toLocaleString() + ' mark(s) published — ' + pct + '%')
        : (p.done.toLocaleString() + ' mark(s) published…');
}

function startPoll() {
    stopPoll();
    if (!REC) return;
    POLL = setInterval(function () {
        if (!REC) { stopPoll(); return; }
        call('Progress', { recordId: REC }, function (p) {
            if (!p || !p.success) return;
            renderProgress(p);
            // The batch finished server-side even though our Commit request may still be
            // in flight (or already gave up). Settle the UI from the authoritative state.
            if (p.status === 'COMMITTED' || p.status === 'FAILED' || p.stalled) {
                stopPoll();
                if (COMMITTING) finishCommit(p);
            }
        });
    }, 2000);
}

function finishCommit(p) {
    setCommitBusy(false, 'Commit');
    stopPoll();
    if (p.status === 'COMMITTED') {
        toast((p.affected || p.done || 0).toLocaleString() + ' mark(s) advanced to ' + STAGE.toLabel + '.');
        REC = 0; hide(qs('sc-modal')); loadStats(); loadBrowse(1); return;
    }
    if (p.stalled) {
        toast('The publish run stopped unexpectedly after ' + (p.done || 0).toLocaleString() +
              ' mark(s). Nothing was lost — press Resume to continue.', true);
        qs('sc-commit-btn').textContent = 'Resume publish';
        return;
    }
    toast(p.error || 'Publish failed.', true);
    qs('sc-commit-btn').textContent = (p.done > 0) ? 'Resume publish' : 'Retry commit';
}

function commit() {
    if (!REC || COMMITTING) return;              // hard guard against a double-click
    setCommitBusy(true, 'Publishing…');
    renderProgress({ done: 0, total: 0 });
    startPoll();

    // Generous budget: the server's own executionTimeout is the real ceiling. If we do
    // time out, we do NOT report failure — the batch is very likely still running, so we
    // keep polling and let the session's own status decide the outcome.
    call('Commit', { recordId: REC }, function (d) {
        if (d && (d.timedOut || d.networkError)) {
            toast('Still working — following the run’s progress.', false);
            startPoll();
            return;
        }
        stopPoll();
        setCommitBusy(false, 'Commit');

        if (!d || !d.success) {
            // A session someone else is already committing: watch it rather than double-run.
            if (d && d.alreadyRunning) { setCommitBusy(true, 'Publishing…'); startPoll(); toast(d.message, true); return; }
            toast((d && d.message) || 'Commit failed.', true);
            qs('sc-commit-btn').textContent = (d && d.partial) ? 'Resume publish' : 'Retry commit';
            if (d && d.partial) { loadStats(); loadBrowse(1); }
            return;
        }
        toast(d.message || ((d.affected || 0) + ' mark(s) advanced to ' + STAGE.toLabel + '.'));
        if (d.skipped > 0) reportSkips(d);
        REC = 0;
        hide(qs('sc-modal')); loadStats(); loadBrowse(1);
    }, { timeoutMs: 290000 });
}

/* A shortfall between "previewed" and "published" is never silent any more — say how
   many were skipped and why, so the operator can act on it. */
function reportSkips(d) {
    var reasons = d.skipReasons || {}, lines = [];
    for (var k in reasons) if (reasons.hasOwnProperty(k)) lines.push('• ' + reasons[k] + ' × ' + k);
    if (!lines.length) return;
    setTimeout(function () {
        alert(d.skipped + ' of ' + (d.expected || '?') + ' mark(s) could not be published:\n\n' +
              lines.join('\n') + '\n\nEverything else was published successfully.');
    }, 350);
}

function closeWizard() {
    // Never cancel a session that is mid-flight — Cancel only applies to a DRAFT, and
    // silently discarding a running publish would be alarming. Closing just hides the
    // dialog; the batch continues and can be reviewed in Session history.
    if (COMMITTING) {
        if (!confirm('A publish is still running.\n\nClosing this window will not stop it — progress is saved and you can follow it in Session history.\n\nClose anyway?')) return;
        stopPoll(); COMMITTING = false; hide(qs('sc-modal')); loadStats(); loadBrowse(1); return;
    }
    if (REC) { call('Cancel', { recordId: REC }, function () {}); REC = 0; }
    hide(qs('sc-modal'));
}

function statsBlock(st) {
    return '<div class="sc-pv-stats">' + kpi('Marks', num(st.marks)) + kpi('Students', num(st.students)) +
        kpi('Programmes', num(st.programmes)) + kpi('Avg total', nz(st.avgTotal)) +
        kpi('Pass rate', (st.passRate || 0) + '%') + kpiFail('Fails (F)', num(st.fails)) +
        kpi('Distinctions', num(st.distinctions)) + '</div>';
}
function kpi(l, v) { return '<div class="sc-kpi"><div class="sc-kpi__v">' + v + '</div><div class="sc-kpi__l">' + l + '</div></div>'; }
function kpiFail(l, v) { return '<div class="sc-kpi sc-kpi--f"><div class="sc-kpi__v">' + v + '</div><div class="sc-kpi__l">' + l + '</div></div>'; }
function breakdownTbl(bd) {
    if (!bd || !bd.length) return '';
    var h = '<table class="sc-pv-tbl"><thead><tr><th>Programme</th><th>Year/Sem</th><th style="text-align:right">Marks</th><th style="text-align:right">Students</th><th style="text-align:right">Fails (F)</th></tr></thead><tbody>';
    bd.forEach(function (g) { h += '<tr><td>' + esc(g.prog_name) + '</td><td>' + esc(g.acad_year) + ' / S' + esc(g.semester) + '</td><td style="text-align:right">' + g.count + '</td><td style="text-align:right">' + g.students + '</td><td style="text-align:right' + ((g.fails||0) > 0 ? ';color:#b42318;font-weight:800' : '') + '">' + (g.fails || 0) + '</td></tr>'; });
    return h + '</tbody></table>';
}
function toggleFailed() {
    FAILED_ONLY = !FAILED_ONLY;
    var btn = qs('sc-failed-toggle');
    if (btn) { if (FAILED_ONLY) btn.classList.add('sc-btn--danger'); else btn.classList.remove('sc-btn--danger'); btn.innerHTML = FAILED_ONLY ? '&#10003; Showing failed (F) only' : '&#9873; Show failed (F) only'; }
    PAGE = 1; scPushUrl(); loadBrowse(1);
}

/* ── Advance selected (forward) ── */
var VERB = { CAPTURE: 'Capture', APPROVE: 'Approve', PUBLISH: 'Publish' };
function advanceSelected() {
    if (!STAGE.canAct) { toast('Your role cannot perform this stage.', true); return; }
    var ids = []; var ch = document.querySelectorAll('.sc-chk:checked'); for (var i=0;i<ch.length;i++) ids.push(parseInt(ch[i].value,10));
    if (!ids.length) { toast('Select one or more marks first.', true); return; }
    var verb = VERB[STAGE.name] || 'Advance';
    var extra = (STAGE.name === 'PUBLISH') ? ' and publish them to results' : '';
    if (!confirm(verb + ' ' + ids.length + ' selected mark(s)?\n\nThis moves them to "' + STAGE.toLabel + '"' + extra + '.')) return;
    var btn = qs('sc-advance-btn');
    if (btn) { if (btn.disabled) return; btn.disabled = true; btn.textContent = 'Working…'; }
    call('AdvanceSelected', { ids: ids, notes: '' }, function (d) {
        if (btn) { btn.disabled = false; btn.innerHTML = '&#10003; Advance selected'; }
        if (d && d.timedOut) { toast('Still working — refresh in a moment to see the result.', true); loadStats(); loadBrowse(PAGE); return; }
        if (!d || !d.success) {
            toast((d && d.message) || 'Advance failed.', true);
            if (d && d.partial) { loadStats(); loadBrowse(PAGE); }   // keep committed chunks visible
            return;
        }
        toast(d.message);
        if (d.skipped > 0) reportSkips({ skipped: d.skipped, expected: ids.length, skipReasons: d.skipReasons });
        loadStats(); loadBrowse(PAGE);
    }, { timeoutMs: 290000 });
}

/* ── Send-back ── */
function returnSelected() {
    var ids = []; var ch = document.querySelectorAll('.sc-chk:checked'); for (var i=0;i<ch.length;i++) ids.push(parseInt(ch[i].value,10));
    if (!ids.length) { toast('Select one or more marks first.', true); return; }
    var reason = prompt('Reason for sending these ' + ids.length + ' mark(s) back one stage:');
    if (!reason) return;
    call('ReturnMarks', { ids: ids, reason: reason }, function (d) {
        if (!d || !d.success) { toast((d && d.message) || 'Return failed.', true); return; }
        toast(d.message); loadStats(); loadBrowse(PAGE);
    });
}

/* ── History (click a row to view full details) ── */
/* A partially-completed run must not look like an ordinary draft in the history —
   RUNNING and FAILED are new states the chunked engine can leave behind, and FAILED in
   particular means "some marks published, the rest still waiting". */
function pillFor(status) {
    if (status === 'COMMITTED') return 'ok';
    if (status === 'CANCELLED') return 'no';
    if (status === 'FAILED')    return 'err';
    if (status === 'RUNNING')   return 'run';
    return 'draft';
}
function loadHistory() {
    var b = qs('sc-hist-body'); b.innerHTML = '<tr><td colspan="7" class="sc-empty">Loading…</td></tr>';
    call('Records', { page: 1 }, function (d) {
        if (!d || !d.success) { b.innerHTML = '<tr><td colspan="7" class="sc-empty" style="color:#b42318">' + esc((d&&d.message)||'Error') + '</td></tr>'; return; }
        if (!d.records.length) { b.innerHTML = '<tr><td colspan="7" class="sc-empty">No sessions yet.</td></tr>'; return; }
        b.innerHTML = d.records.map(function (r) {
            var badge = r.isMigration ? '<span class="sc-pill sc-pill--mig">migration</span>' : '<span class="sc-pill sc-pill--' + pillFor(r.status) + '">' + esc(r.status) + '</span>';
            var scopeTxt = r.all ? 'All in scope' : [r.prog, r.acadYear, r.semester ? 'S' + r.semester : '', r.yearOfStudy ? 'Y' + r.yearOfStudy : ''].filter(Boolean).join(' · ');
            return '<tr class="sc-rowclick" onclick="SC.viewRec(' + r.id + ')">' +
                '<td>#' + r.id + '</td><td>' + badge + '</td><td>' + esc(r.byName || r.by) + '<div class="sc-sub">' + esc(r.role) + '</div></td>' +
                '<td>' + esc(scopeTxt || '—') + '</td><td class="sc-c">' + nz(r.affected) + '</td>' +
                '<td>' + esc(r.committedAt || r.createdAt) + '</td>' +
                '<td><span class="sc-link">View &rsaquo;</span></td></tr>';
        }).join('');
    });
}
function viewRec(id) {
    var body = qs('sc-detail-body'); body.innerHTML = 'Loading…'; show(qs('sc-detail'));
    call('RecordDetail', { recordId: id }, function (d) {
        if (!d || !d.success) { body.innerHTML = '<div style="color:#b42318">' + esc((d&&d.message)||'Error') + '</div>'; return; }
        var r = d.record, st = d.stats || {}, sm = d.summary || {};
        var meta = '<table class="sc-meta"><tbody>' +
            row('Session', '#' + r.id + (r.isMigration ? ' (migration)' : '')) +
            row('Stage', esc(r.fromLabel) + ' &rarr; ' + esc(r.toLabel)) +
            row('Status', esc(r.status)) +
            row('Performed by', esc(r.byName || r.by) + (r.role ? ' (' + esc(r.role) + ')' : '')) +
            row('Created', esc(r.createdAt)) + row('Committed', esc(r.committedAt || '—')) +
            row('Scope / params', esc(r.all ? 'Everything in scope' : ([r.prog, r.acadYear, r.semester ? 'S' + r.semester : '', r.yearOfStudy ? 'Y' + r.yearOfStudy : ''].filter(Boolean).join(' · ') || '—'))) +
            row('Marks affected', num(r.affected)) +
            (r.notes ? row('Notes', esc(r.notes)) : '') + '</tbody></table>';
        var snap = (st && st.marks) ? ('<div class="sc-det-h">Performance snapshot (at commit)</div>' + statsBlock(st)) : '';
        var bd = (sm && sm.breakdown) ? ('<div class="sc-det-h">Breakdown</div>' + breakdownTbl(sm.breakdown)) : '';
        body.innerHTML = meta + outcomeBlock(r, sm) + snap + bd;
        var csv = qs('sc-detail-csv');
        if (r.status === 'COMMITTED' && r.affected && !r.isMigration) { csv.style.display = ''; csv.href = location.pathname + '?export=csv&rid=' + r.id; }
        else csv.style.display = 'none';
    });
}
/* Explains any gap between what a session was asked to move and what it actually moved.
   Previously a run that fell short just showed a smaller number with no reason given —
   which is exactly how the old LIMIT-5000 truncation stayed invisible for so long. */
function outcomeBlock(r, sm) {
    if (!sm) return '';
    var bits = '';
    if (sm.error) {
        bits += '<div class="sc-warn"><b>This run stopped before finishing.</b><br>' + esc(sm.error) +
                '<br><br>Marks already published were kept. Re-running the session continues with whatever is still awaiting publication.</div>';
    }
    var reasons = sm.skipReasons;
    if (reasons) {
        var rows = '';
        for (var k in reasons) if (reasons.hasOwnProperty(k))
            rows += '<tr><td>' + esc(k) + '</td><td style="text-align:right">' + reasons[k] + '</td></tr>';
        if (rows) {
            bits += '<div class="sc-det-h">Not advanced (' + num(sm.skipped) + ')</div>' +
                    '<table class="sc-pv-tbl"><thead><tr><th>Reason</th><th style="text-align:right">Marks</th></tr></thead><tbody>' +
                    rows + '</tbody></table>';
        }
    }
    if (sm.expected && sm.advanced !== undefined && sm.expected !== sm.advanced && !sm.error && !reasons) {
        bits += '<div class="sc-warn">Matched ' + num(sm.expected) + ' but advanced ' + num(sm.advanced) + '.</div>';
    }
    return bits;
}
function row(k, v) { return '<tr><th>' + k + '</th><td>' + v + '</td></tr>'; }
function closeDetail() { hide(qs('sc-detail')); }

function tab(which) {
    qs('sc-view-queue').style.display = which === 'queue' ? '' : 'none';
    qs('sc-view-hist').style.display = which === 'hist' ? '' : 'none';
    qs('sc-tab-queue').className = 'sc-tab' + (which === 'queue' ? ' active' : '');
    qs('sc-tab-hist').className = 'sc-tab' + (which === 'hist' ? ' active' : '');
    if (which === 'hist') loadHistory();
}

window.SC = { go: function(p){ PAGE=p; scPushUrl(); loadBrowse(p); }, openWizard: openWizard, doPreview: doPreview, wizBack: wizBack, commit: commit,
    closeWizard: closeWizard, returnSelected: returnSelected, advanceSelected: advanceSelected, tab: tab, viewRec: viewRec, closeDetail: closeDetail, toggleFailed: toggleFailed,
    apply: function(){ PAGE=1; scPushUrl(); loadBrowse(1); },
    reset: function(){ qs('sc-year').value=''; qs('sc-sem').value=''; qs('sc-prog').value=''; qs('sc-q').value=''; FAILED_ONLY=false; var tb=qs('sc-failed-toggle'); if(tb){ tb.classList.remove('sc-btn--danger'); tb.innerHTML='&#9873; Show failed (F) only'; } PAGE=1; scPushUrl(); loadBrowse(1); },
    selectAll: function(cb){ var ch=document.querySelectorAll('.sc-chk'); for(var i=0;i<ch.length;i++) ch[i].checked=cb.checked; } };

if (document.readyState !== 'loading') boot(); else document.addEventListener('DOMContentLoaded', boot);
})();
