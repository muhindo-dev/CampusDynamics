/* ===========================================================================
   Student Course Rearrangement — the workspace.

   Nothing here writes to the database. Every change is held client-side until
   the officer reviews and confirms, and the server then re-validates all of it
   from scratch: this file's job is to make the intent clear and hard to get
   wrong, not to be trusted.

   Two ways to move a course, deliberately. Drag and drop is the fast path on a
   mouse; the "Move to" select on every row is the real path on a tablet, on a
   laptop with a poor trackpad, and for anyone working by keyboard. Neither is
   a second-class citizen.

   Vanilla JS, no dependencies, no build step — same as every other NewScreens
   page.
   =========================================================================== */
(function () {
'use strict';

var PAGE = location.pathname;
var DATA = null;          // last workspace payload from the server
var SESSION = 0;
var CHECKSUM = '';
var SAVING = false;
var OP_ID = null;         // one id per save attempt, so a retry cannot double-apply

/* The pending set. Keyed by registration id so a second edit to the same row
   replaces the first rather than stacking. */
var PEND = { moves: {}, marks: {}, deletes: {}, adds: [], regsems: [] };
var TEMP = -1;            // negative ids for not-yet-saved additions

/* ── plumbing ─────────────────────────────────────────────────────────── */
function qs(id) { return document.getElementById(id); }
function esc(s) {
    return s === null || s === undefined ? '' : String(s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}
function call(method, params, cb) {
    var x = new XMLHttpRequest();
    x.open('POST', PAGE + '/' + method, true);
    x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
    x.timeout = 290000;
    x.ontimeout = function () { cb({ success: false, timedOut: true, message: 'The request took longer than expected. Reload to see whether it completed.' }); };
    x.onload = function () {
        try { var o = JSON.parse(x.responseText); cb(typeof o.d === 'string' ? JSON.parse(o.d) : o.d); }
        catch (e) {
            // A permission redirect or a session timeout arrives as HTML, not JSON.
            cb({ success: false, message: x.status === 401 || x.status === 403
                 ? 'You are not permitted to do that.'
                 : 'The server response could not be read. Your session may have expired — reload the page.' });
        }
    };
    x.onerror = function () { cb({ success: false, message: 'Network error. Nothing was saved.' }); };
    x.send(JSON.stringify(params || {}));
}
function toast(msg, isErr) {
    var t = document.createElement('div');
    t.className = 'rx-toast' + (isErr ? ' rx-toast--err' : '');
    t.textContent = msg;
    document.body.appendChild(t);
    setTimeout(function () {
        t.style.transition = 'opacity .4s'; t.style.opacity = '0';
        setTimeout(function () { if (t.parentNode) t.parentNode.removeChild(t); }, 400);
    }, isErr ? 6000 : 3200);
}
function openModal(id) { qs(id).classList.add('is-open'); }
function closeModal(id) { qs(id).classList.remove('is-open'); }
function uuid() {
    return 'rx-' + Date.now().toString(36) + '-' +
           Math.random().toString(36).slice(2, 10) + Math.random().toString(36).slice(2, 6);
}

document.addEventListener('click', function (e) {
    var c = e.target.getAttribute && e.target.getAttribute('data-close');
    if (c) closeModal(c);
});

/* ── GET state ────────────────────────────────────────────────────────────
   The sitting is in the URL, so a reload resumes it instead of dropping the
   officer back to the gate with the record gone. Pending edits are held
   client-side and are genuinely lost on a reload — beforeunload warns about
   that, and resuming re-reads the record from the database rather than
   pretending to restore work that was never saved. */
function urlParam(name) {
    try { return new URLSearchParams(location.search).get(name) || ''; } catch (e) { return ''; }
}
function setSessionUrl(id, replace) {
    var url = location.pathname + (id ? '?session=' + encodeURIComponent(id) : '');
    try {
        if (replace) history.replaceState(null, '', url);
        else history.pushState(null, '', url);
    } catch (e) { }
}

/* ── click-to-fill suggestions ────────────────────────────────────────────
   These are the reasons this module is actually opened for. Offering them as
   one-click fills is not only faster; it makes the log searchable, because the
   same correction stops being described six different ways by six officers. */
var REASONS = {
    session: [
        'Courses recorded against the wrong semester after the 2025 migration.',
        'Duplicate registration created during online registration.',
        'Student sat the semester but the registration was never created.',
        'Marks were entered against the wrong course registration.',
        'Correcting placement so the transcript reflects the semester actually taught.'
    ],
    mark: [
        'Marks entry error confirmed by the lecturer.',
        'Coursework total corrected after remarking.',
        'Exam script recount confirmed by the department.',
        'Mark was captured against the wrong component.'
    ],
    remove: [
        'Registered in error, confirmed with the faculty.',
        'Duplicate of a registration held in another term.',
        'Student never took this course.'
    ],
    add: [
        'Student sat this course but was never registered for it.',
        'Registration lost during the migration.',
        'Course was taken as a retake and not captured.'
    ],
    regsem: [
        'Student sat the semester but was never registered.',
        'Registration lost during the migration.',
        'Semester needed so the courses below can be placed correctly.'
    ],
    move: [
        'Course belongs in this semester per the programme curriculum.',
        'Recorded against the wrong semester at registration.',
        'Correcting the term the course was actually taught in.'
    ]
};

function fillChips(containerId, kind, targetId, onPick) {
    var box = qs(containerId);
    if (!box) return;
    var list = REASONS[kind] || [];
    var h = '';
    for (var i = 0; i < list.length; i++)
        h += '<button type="button" class="rx-chip" data-fill="' + esc(list[i]) + '">' +
             esc(list[i]) + '</button>';
    box.innerHTML = h;
    var bs = box.querySelectorAll('[data-fill]');
    for (var j = 0; j < bs.length; j++) {
        bs[j].addEventListener('click', function (e) {
            var t = qs(targetId);
            t.value = e.currentTarget.getAttribute('data-fill');
            t.focus();
            // Anything watching the field for validity has to hear about this.
            var ev = document.createEvent('HTMLEvents'); ev.initEvent('input', true, true);
            t.dispatchEvent(ev);
            if (onPick) onPick();
        });
    }
}

/* ── the gate ─────────────────────────────────────────────────────────── */
var MIN_REASON = 30;
var PICKED = null;          // the student chosen from the list, if one was
var stuTimer = null, stuRows = [], stuIdx = -1;

function gateCheck() {
    var rg = qs('rx-regno').value.trim();
    var rs = qs('rx-reason').value.trim();
    var ak = qs('rx-ack').checked;
    var hint = qs('rx-reason-hint');

    if (rs.length === 0) { hint.className = 'rx-hint'; hint.textContent = 'At least ' + MIN_REASON + ' characters.'; }
    else if (rs.length < MIN_REASON) {
        hint.className = 'rx-hint rx-hint--bad';
        hint.textContent = (MIN_REASON - rs.length) + ' more character(s) needed.';
    } else { hint.className = 'rx-hint rx-hint--ok'; hint.textContent = 'Reason accepted (' + rs.length + ' characters).'; }

    qs('rx-open').disabled = !(rg.length > 0 && rs.length >= MIN_REASON && ak);
}

/* Officers know students by name at least as often as by number, and were having to
   look the number up somewhere else before they could even open a session. */
function studentSearch() {
    clearTimeout(stuTimer);
    var q = qs('rx-regno').value.trim();
    PICKED = null;
    qs('rx-picked').style.display = 'none';
    if (q.length < 2) { closeStuList(); return; }
    stuTimer = setTimeout(function () {
        call('SearchStudents', { q: q }, function (d) {
            if (!d || !d.success || !d.rows) { closeStuList(); return; }
            stuRows = d.rows; stuIdx = -1;
            if (!stuRows.length) {
                qs('rx-stu-list').innerHTML =
                    '<div class="rx-sugg__none">No student in your scope matches that.</div>';
                qs('rx-stu-list').classList.add('is-open');
                qs('rx-regno').setAttribute('aria-expanded', 'true');
                return;
            }
            var h = '';
            for (var i = 0; i < stuRows.length; i++) {
                var r = stuRows[i];
                h += '<button type="button" class="rx-sugg__row" role="option" data-i="' + i + '">' +
                     '<span class="rx-sugg__main">' + esc(r.name || r.regno) + '</span>' +
                     '<span class="rx-sugg__sub">' + esc(r.entryno) + ' \u00b7 ' + esc(r.regno) +
                     ' \u00b7 ' + esc(r.prog) + ' \u00b7 ' + r.registrations + ' registration' +
                     (r.registrations === 1 ? '' : 's') + '</span></button>';
            }
            qs('rx-stu-list').innerHTML = h;
            qs('rx-stu-list').classList.add('is-open');
            qs('rx-regno').setAttribute('aria-expanded', 'true');
            var rows = qs('rx-stu-list').querySelectorAll('[data-i]');
            for (var j = 0; j < rows.length; j++)
                rows[j].addEventListener('click', function (e) {
                    pickStudent(+e.currentTarget.getAttribute('data-i'));
                });
        });
    }, 220);
}

function pickStudent(i) {
    var r = stuRows[i];
    if (!r) return;
    PICKED = r;
    qs('rx-regno').value = r.regno;
    closeStuList();
    qs('rx-picked').innerHTML =
        '<b>' + esc(r.name) + '</b> \u00b7 ' + esc(r.entryno) + '<br />' +
        esc(r.progName) + ' (' + esc(r.prog) + ')' +
        (r.status ? ' \u00b7 ' + esc(r.status) : '') +
        ' \u00b7 ' + r.registrations + ' course registration' + (r.registrations === 1 ? '' : 's');
    qs('rx-picked').style.display = '';
    gateCheck();
}

function closeStuList() {
    qs('rx-stu-list').classList.remove('is-open');
    qs('rx-stu-list').innerHTML = '';
    qs('rx-regno').setAttribute('aria-expanded', 'false');
    stuIdx = -1;
}

function stuKey(e) {
    var open = qs('rx-stu-list').classList.contains('is-open');
    if (!open) return;
    var rows = qs('rx-stu-list').querySelectorAll('[data-i]');
    if (!rows.length) return;
    if (e.key === 'ArrowDown' || e.keyCode === 40) { e.preventDefault(); stuIdx = Math.min(stuIdx + 1, rows.length - 1); }
    else if (e.key === 'ArrowUp' || e.keyCode === 38) { e.preventDefault(); stuIdx = Math.max(stuIdx - 1, 0); }
    else if (e.key === 'Enter' || e.keyCode === 13) {
        if (stuIdx >= 0) { e.preventDefault(); pickStudent(stuIdx); }
        return;
    } else if (e.key === 'Escape' || e.keyCode === 27) { closeStuList(); return; }
    else return;
    for (var i = 0; i < rows.length; i++) rows[i].classList.toggle('is-on', i === stuIdx);
    if (rows[stuIdx]) rows[stuIdx].scrollIntoView({ block: 'nearest' });
}

document.addEventListener('click', function (e) {
    if (!qs('rx-stu-list')) return;
    if (e.target.closest && e.target.closest('#rx-stu-list, #rx-regno')) return;
    closeStuList();
});

function openSession() {
    var btn = qs('rx-open');
    btn.disabled = true; btn.textContent = 'Opening…';
    qs('rx-gate-msg').textContent = '';
    call('OpenSession', {
        regno: qs('rx-regno').value.trim(),
        reason: qs('rx-reason').value.trim(),
        acknowledged: qs('rx-ack').checked
    }, function (d) {
        btn.textContent = 'Load student record';
        if (!d || !d.success) {
            btn.disabled = false;
            qs('rx-gate-msg').innerHTML = '<span class="rx-hint rx-hint--bad">' + esc(d && d.message || 'Could not open the session.') + '</span>';
            return;
        }
        SESSION = d.sessionId;
        setSessionUrl(SESSION, false);
        qs('rx-gate').style.display = 'none';
        qs('rx-boot').style.display = '';
        loadWorkspace();
    });
}

/* ── load ─────────────────────────────────────────────────────────────── */
function loadWorkspace() {
    // One call, not several. These PageMethods hold an exclusive ASP.NET session
    // lock, so parallel requests would only queue behind each other anyway.
    call('LoadWorkspace', { sessionId: SESSION }, function (d) {
        qs('rx-boot').style.display = 'none';
        if (!d || !d.success) {
            // A stale or someone else's session link should land on the gate, not a dead end.
            qs('rx-gate').style.display = '';
            qs('rx-workspace').style.display = 'none';
            qs('rx-boot').style.display = 'none';
            SESSION = 0; setSessionUrl(0, true);
            qs('rx-gate-msg').innerHTML = '<span class="rx-hint rx-hint--bad">' +
                esc(d && d.message || 'That session could not be opened.') +
                ' Open a new session below.</span>';
            gateCheck();
            return;
        }
        DATA = d; CHECKSUM = d.checksum;
        qs('rx-actor').textContent = d.actor.name || d.actor.user;
        qs('rx-role').textContent = d.actor.role || 'no role';
        qs('rx-workspace').style.display = '';
        renderStudent();
        render();
    });
}

function renderStudent() {
    var s = DATA.student;
    var photo = s.photo
        ? '<img class="rx-student__photo" src="../StudentInfo/photos/' + esc(s.photo) + '" alt="" ' +
          'onerror="this.outerHTML=&quot;<div class=\\&quot;rx-student__ph\\&quot;>' + esc((s.name || '?').charAt(0)) + '</div>&quot;" />'
        : '<div class="rx-student__ph">' + esc((s.name || '?').charAt(0)) + '</div>';
    qs('rx-student').innerHTML =
        photo +
        '<div style="flex:1 1 260px">' +
          '<div class="rx-student__name">' + esc(s.name) + '</div>' +
          '<div class="rx-student__meta">' +
            '<b>' + esc(s.entryno) + '</b> &middot; ' + esc(s.regno) + '</div>' +
          '<div class="rx-student__meta">' + esc(s.progName) + ' (' + esc(s.prog) + ')' +
            (s.status ? ' &middot; ' + esc(s.status) : '') +
            (s.entryYear ? ' &middot; entry ' + esc(s.entryYear) : '') + '</div>' +
        '</div>' +
        '<div style="font-size:10px;color:#64748b;text-align:right">' +
          'Session <b>' + esc(DATA.session.sref) + '</b><br />opened ' + esc(DATA.session.openedAt) + '</div>';
}

/* ── the model: what a row looks like after pending changes ───────────── */
function effective(c) {
    var m = PEND.moves[c.regId];
    return {
        year: m ? m.toYear : c.studyYear,
        sem: m ? m.toSem : c.semester
    };
}
function allRows() {
    var rows = DATA.courses.slice();
    for (var i = 0; i < PEND.adds.length; i++) {
        var a = PEND.adds[i];
        rows.push({
            regId: a.tempId, course: a.course, title: a.title, acadYear: '', semester: a.toSem,
            studyYear: a.toYear, courseStatus: 'Normal', markStage: '', cw: null, exam: null, total: null,
            resultId: 0, grade: '', creditUnits: a.creditUnits, isRetake: false,
            lockStatus: 'DRAFT', locked: false, curriculum: a.curriculum || { year: 0, semester: 0 },
            _isNew: true
        });
    }
    return rows;
}
function pendingCount() {
    var n = PEND.adds.length + PEND.regsems.length;
    for (var k in PEND.moves) if (PEND.moves.hasOwnProperty(k)) n++;
    for (var k2 in PEND.deletes) if (PEND.deletes.hasOwnProperty(k2)) n++;
    for (var k3 in PEND.marks) if (PEND.marks.hasOwnProperty(k3)) {
        var m = PEND.marks[k3];
        if (m.cw) n++; if (m.exam) n++;
    }
    return n;
}

/* ── render ───────────────────────────────────────────────────────────── */
function render() {
    var rows = allRows();

    // Every (year, semester) the student holds, plus any the pending set introduces.
    var slots = {};
    function slot(y, s) {
        var k = y + '/' + s;
        if (!slots[k]) slots[k] = { year: y, sem: s, rows: [], acadYear: '', closed: false, registered: false };
        return slots[k];
    }
    for (var i = 0; i < DATA.semesters.length; i++) {
        var sm = DATA.semesters[i];
        var sl = slot(sm.studyYear, sm.semester);
        sl.acadYear = sl.acadYear || sm.acadYear;
        sl.registered = true;
    }
    for (var j = 0; j < PEND.regsems.length; j++) {
        var rsm = PEND.regsems[j];
        var sl2 = slot(rsm.toYear, rsm.toSem);
        sl2.acadYear = rsm.acadYear; sl2.registered = true; sl2.isNew = true;
    }
    for (var k = 0; k < rows.length; k++) {
        var eff = effective(rows[k]);
        slot(eff.year, eff.sem).rows.push(rows[k]);
    }
    for (var ci = 0; ci < DATA.closedSemesters.length; ci++) {
        var ck = DATA.closedSemesters[ci];
        if (slots[ck]) slots[ck].closed = true;
    }

    var keys = Object.keys(slots).sort(function (a, b) {
        var pa = a.split('/'), pb = b.split('/');
        return (+pa[0] - +pb[0]) || (+pa[1] - +pb[1]);
    });

    // group by year
    var years = {};
    for (var y = 0; y < keys.length; y++) {
        var sl3 = slots[keys[y]];
        (years[sl3.year] = years[sl3.year] || []).push(sl3);
    }

    var h = '';
    var yearKeys = Object.keys(years).sort(function (a, b) { return +a - +b; });
    if (yearKeys.length === 0) {
        h = '<div class="rx-card"><div class="rx-state"><div class="rx-state__t">No academic record</div>' +
            'This student has no semester registrations and no course registrations yet.<br />' +
            'Use <b>Register a new semester</b> to start one.</div></div>';
    }
    for (var yi = 0; yi < yearKeys.length; yi++) {
        var yk = yearKeys[yi], sems = years[yk];
        h += '<div class="rx-year">';
        h += '<div class="rx-year__hd">Year ' + esc(yk) +
             '<span class="rx-year__gpa">' + esc(sems[0].acadYear || '') + '</span></div>';
        h += '<div class="rx-sems">';
        for (var si = 0; si < sems.length; si++) h += renderSem(sems[si]);
        h += '</div></div>';
    }
    qs('rx-groups').innerHTML = h;

    var n = pendingCount();
    var pill = qs('rx-pending');
    pill.textContent = n === 0 ? 'No pending changes' : n + ' pending change' + (n === 1 ? '' : 's');
    pill.className = 'rx-pending' + (n === 0 ? ' rx-pending--zero' : '');
    qs('rx-save').disabled = n === 0 || SAVING;
    qs('rx-discard').disabled = n === 0 || SAVING;

    var rows2 = allRows(), shutCount = 0;
    for (var q = 0; q < rows2.length; q++) if (!OPEN[rows2[q].regId]) shutCount++;
    var eb = qs('rx-expand');
    if (eb) eb.textContent = shutCount > 0 ? 'Expand all' : 'Collapse all';

    wire();
}

/* Open courses and shut semesters. render() rebuilds the markup wholesale, so this state
   lives out here or every redraw would fold everything back up mid-edit. */
var OPEN = {};          // regId -> true
var SHUT = {};          // "year/sem" -> true  (semesters are open by default)

function chev(open) {
    return '<svg class="rx-chev' + (open ? ' is-open' : '') + '" xmlns="http://www.w3.org/2000/svg" ' +
           'width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" ' +
           'stroke-width="3" stroke-linecap="round" stroke-linejoin="round">' +
           '<polyline points="9 18 15 12 9 6"></polyline></svg>';
}

function renderSem(sl) {
    var key = sl.year + '/' + sl.sem;
    var shut = !!SHUT[key];

    // Count what is pending in here, so a folded semester still says something happened inside.
    var changed = 0, misplaced = 0;
    for (var n = 0; n < sl.rows.length; n++) {
        var r = sl.rows[n], id = r.regId;
        if (PEND.moves[id] || PEND.deletes[id] || r._isNew ||
            (PEND.marks[id] && (PEND.marks[id].cw || PEND.marks[id].exam))) changed++;
        var cy = r.curriculum ? r.curriculum.year : 0, cs = r.curriculum ? r.curriculum.semester : 0;
        if (cy > 0 && cs > 0 && (cy !== sl.year || cs !== sl.sem)) misplaced++;
    }

    var h = '<div class="rx-sem' + (shut ? ' is-shut' : '') + '" data-year="' + sl.year +
            '" data-sem="' + sl.sem + '"' + (sl.registered ? '' : ' data-unreg="1"') + '>';

    h += '<div class="rx-sem__hd" data-semtoggle="' + key + '" role="button" tabindex="0" ' +
         'aria-expanded="' + (shut ? 'false' : 'true') + '">' +
         chev(!shut) +
         '<span class="rx-sem__name">Semester ' + esc(sl.sem) + '</span>' +
         (sl.isNew ? '<span class="rx-badge rx-badge--REGISTER_SEMESTER">new</span>' : '') +
         (sl.closed ? '<span class="rx-sem__closed" title="Holds finally published results">closed</span>' : '') +
         (!sl.registered ? '<span class="rx-offcur">not registered</span>' : '') +
         '<span class="rx-sem__count">' + sl.rows.length +
             (sl.rows.length === 1 ? ' course' : ' courses') + '</span>' +
         (misplaced ? '<span class="rx-sem__mis" title="' + misplaced +
             ' course(s) here are placed somewhere else by the curriculum">' + misplaced +
             ' misplaced</span>' : '') +
         (changed ? '<span class="rx-sem__chg">' + changed + ' changed</span>' : '') +
         '</div>';

    h += '<div class="rx-sem__body">';
    if (sl.rows.length === 0) h += '<div class="rx-sem__empty">Drop a course here</div>';
    for (var i = 0; i < sl.rows.length; i++) h += renderCourse(sl.rows[i], sl);
    h += '<button type="button" class="rx-addbtn" data-add-year="' + sl.year +
         '" data-add-sem="' + sl.sem + '">+ Add course</button>';
    h += '</div></div>';
    return h;
}

function renderCourse(c, sl) {
    var moved = !!PEND.moves[c.regId];
    var del = !!PEND.deletes[c.regId];
    var mk = PEND.marks[c.regId];
    var markChanged = !!(mk && (mk.cw || mk.exam));
    var isNew = !!c._isNew;
    var open = !!OPEN[c.regId];

    var cls = 'rx-course';
    if (del) cls += ' rx-is-deleted';
    else if (isNew) cls += ' rx-is-added';
    else if (moved) cls += ' rx-is-moved';
    else if (markChanged) cls += ' rx-is-mark';
    if (open) cls += ' is-open';

    var cw = mk && mk.cw ? mk.cw.to : c.cw;
    var ex = mk && mk.exam ? mk.exam.to : c.exam;
    var hasMarks = (cw !== null && cw !== undefined) || (ex !== null && ex !== undefined);
    var tot = (cw === null || cw === undefined ? 0 : +cw) + (ex === null || ex === undefined ? 0 : +ex);

    // Where the programme curriculum puts this course. Both parts have to be present for
    // the answer to be usable — a mapping with a blank year or semester says nothing.
    var curY = c.curriculum ? c.curriculum.year : 0;
    var curS = c.curriculum ? c.curriculum.semester : 0;
    var mapped = curY > 0 && curS > 0;
    var offCur = mapped && (curY !== sl.year || curS !== sl.sem);

    var h = '<div class="' + cls + '" draggable="' + (del ? 'false' : 'true') + '" data-reg="' + c.regId + '" ' +
            'data-year="' + sl.year + '" data-sem="' + sl.sem + '">';

    /* ── the line you always see ── */
    h += '<div class="rx-course__hd" data-toggle="' + c.regId + '" role="button" tabindex="0" ' +
         'aria-expanded="' + (open ? 'true' : 'false') + '">';
    h += chev(open);
    h += '<span class="rx-course__code">' + esc(c.course) + '</span>';
    h += '<span class="rx-course__title" title="' + esc(c.title) + '">' + esc(c.title) + '</span>';
    if (c.isRetake) h += '<span class="rx-retake" title="Retake \u2014 never changed by a move">RT</span>';
    if (c.locked) h += '<span class="rx-lock' + (c.lockStatus === 'FINAL_PUBLISHED' ? ' rx-lock--final' : '') +
                       '" title="Results status: ' + esc(c.lockStatus) + '">' +
                       (c.lockStatus === 'FINAL_PUBLISHED' ? 'FINAL' : 'LOCKED') + '</span>';
    // Name the destination rather than only flagging a problem: the officer can see where
    // the course belongs without opening the row.
    if (offCur) h += '<span class="rx-offcur" title="The programme curriculum places ' + esc(c.course) +
                     ' in Year ' + curY + ' Semester ' + curS + '. It is sitting in Year ' + sl.year +
                     ' Semester ' + sl.sem + '.">\u2192 Y' + curY + ' S' + curS + '</span>';
    else if (!mapped) h += '<span class="rx-nocur" title="' + esc(c.course) +
                     ' is not mapped to a year and semester on this programme\u2019s curriculum, ' +
                     'so there is nothing to compare its placement against.">no curriculum</span>';
    h += '<span class="rx-course__sp"></span>';
    if (hasMarks) h += '<span class="rx-course__tot">' + tot + '</span>';
    if (c.grade) h += '<span class="rx-grade' + (c.grade === 'F' ? ' rx-grade--f' : '') + '">' + esc(c.grade) + '</span>';
    if (c.creditUnits) h += '<span class="rx-course__cu">' + esc(c.creditUnits) + '</span>';
    h += '</div>';

    /* ── a folded row still says what is pending inside it ── */
    var notes = [];
    if (moved) notes.push('Moved from Y' + PEND.moves[c.regId].fromYear + ' S' + PEND.moves[c.regId].fromSem);
    if (isNew) notes.push('New registration');
    if (del) notes.push('To be removed \u2014 ' + PEND.deletes[c.regId].reason);
    if (mk && mk.cw) notes.push('CW ' + fmt(mk.cw.from) + ' \u2192 ' + fmt(mk.cw.to));
    if (mk && mk.exam) notes.push('Exam ' + fmt(mk.exam.from) + ' \u2192 ' + fmt(mk.exam.to));
    if (!open && notes.length) h += '<div class="rx-course__mini">' + esc(notes.join(' \u00b7 ')) + '</div>';

    /* ── everything you act with, revealed on open ── */
    if (open) {
        h += '<div class="rx-course__bd">';
        h += '<div class="rx-course__marks">' +
             '<span class="rx-mark">CW <input type="number" class="rx-mark__in" data-mark="cw" data-reg="' + c.regId + '" ' +
                 'min="0" max="40" value="' + (cw === null || cw === undefined ? '' : esc(cw)) + '"' +
                 (del || isNew ? ' disabled="disabled"' : '') + ' /></span>' +
             '<span class="rx-mark">Exam <input type="number" class="rx-mark__in" data-mark="exam" data-reg="' + c.regId + '" ' +
                 'min="0" max="60" value="' + (ex === null || ex === undefined ? '' : esc(ex)) + '"' +
                 (del || isNew ? ' disabled="disabled"' : '') + ' /></span>' +
             (hasMarks ? '<span class="rx-mark">Total <b>' + tot + '</b></span>' : '') +
             '</div>';

        h += '<div class="rx-course__acts">';
        h += '<label class="rx-lbl" style="margin:0 3px 0 0;font-size:9px">Move to</label>';
        h += '<select class="rx-moveto" data-reg="' + c.regId + '"' + (del ? ' disabled="disabled"' : '') + '>';
        h += '<option value="">\u2014 stay \u2014</option>';
        var opts = moveTargets();
        for (var i = 0; i < opts.length; i++) {
            var o = opts[i];
            var sel = (o.year === sl.year && o.sem === sl.sem) ? ' selected="selected"' : '';
            h += '<option value="' + o.year + '/' + o.sem + '"' + sel + '>Y' + o.year + ' S' + o.sem +
                 (o.acadYear ? ' (' + esc(o.acadYear) + ')' : '') + '</option>';
        }
        h += '</select>';
        if (del) h += '<button type="button" class="rx-btn rx-btn--ghost rx-btn--sm" data-undel="' + c.regId + '">Keep</button>';
        else if (isNew) h += '<button type="button" class="rx-x" data-unadd="' + c.regId + '">Remove</button>';
        else h += '<button type="button" class="rx-x" data-del="' + c.regId + '">Remove\u2026</button>';
        h += '</div>';

        if (notes.length) h += '<div class="rx-note">' + esc(notes.join('\n')).split('\n').join('<br />') + '</div>';
        h += '</div>';
    }

    h += '</div>';
    return h;
}

function fmt(v) { return (v === null || v === undefined || v === '') ? 'blank' : v; }

function moveTargets() {
    var out = [], seen = {};
    for (var i = 0; i < DATA.semesters.length; i++) {
        var s = DATA.semesters[i], k = s.studyYear + '/' + s.semester;
        if (seen[k]) continue; seen[k] = 1;
        out.push({ year: s.studyYear, sem: s.semester, acadYear: s.acadYear });
    }
    for (var j = 0; j < PEND.regsems.length; j++) {
        var r = PEND.regsems[j], k2 = r.toYear + '/' + r.toSem;
        if (seen[k2]) continue; seen[k2] = 1;
        out.push({ year: r.toYear, sem: r.toSem, acadYear: r.acadYear });
    }
    out.sort(function (a, b) { return (a.year - b.year) || (a.sem - b.sem); });
    return out;
}

/* ── interaction ──────────────────────────────────────────────────────── */
var dragReg = null, ghostEl = null;

function wire() {
    // Toggling is a click or a keypress: these headers are focusable, so the whole
    // workspace stays operable without a mouse.
    bindAll('[data-toggle]', 'click', function (e) {
        var id = +e.currentTarget.getAttribute('data-toggle');
        if (OPEN[id]) delete OPEN[id]; else OPEN[id] = true;
        render();
    });
    bindAll('[data-semtoggle]', 'click', function (e) {
        var k = e.currentTarget.getAttribute('data-semtoggle');
        if (SHUT[k]) delete SHUT[k]; else SHUT[k] = true;
        render();
    });
    bindAll('[data-toggle],[data-semtoggle]', 'keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ' && e.keyCode !== 13 && e.keyCode !== 32) return;
        e.preventDefault(); e.currentTarget.click();
    });

    var courses = document.querySelectorAll('.rx-course[draggable="true"]');
    for (var i = 0; i < courses.length; i++) {
        courses[i].addEventListener('dragstart', onDragStart);
        courses[i].addEventListener('dragend', onDragEnd);
    }
    var sems = document.querySelectorAll('.rx-sem');
    for (var j = 0; j < sems.length; j++) {
        sems[j].addEventListener('dragover', onDragOver);
        sems[j].addEventListener('dragleave', onDragLeave);
        sems[j].addEventListener('drop', onDrop);
    }
    bindAll('.rx-moveto', 'change', function (e) {
        var v = e.target.value; if (!v) { render(); return; }
        var p = v.split('/');
        requestMove(+e.target.getAttribute('data-reg'), +p[0], +p[1]);
    });
    bindAll('.rx-mark__in', 'change', function (e) {
        onMarkEdit(+e.target.getAttribute('data-reg'), e.target.getAttribute('data-mark'), e.target.value);
    });
    bindAll('[data-del]', 'click', function (e) { requestDelete(+e.target.getAttribute('data-del')); });
    bindAll('[data-undel]', 'click', function (e) {
        delete PEND.deletes[+e.target.getAttribute('data-undel')]; render();
    });
    bindAll('[data-unadd]', 'click', function (e) {
        var id = +e.target.getAttribute('data-unadd');
        PEND.adds = PEND.adds.filter(function (a) { return a.tempId !== id; });
        render();
    });
    bindAll('[data-add-year]', 'click', function (e) {
        openAdd(+e.target.getAttribute('data-add-year'), +e.target.getAttribute('data-add-sem'));
    });
}
function bindAll(sel, ev, fn) {
    var els = document.querySelectorAll(sel);
    for (var i = 0; i < els.length; i++) els[i].addEventListener(ev, fn);
}

function onDragStart(e) {
    var el = e.currentTarget;
    dragReg = +el.getAttribute('data-reg');
    el.classList.add('is-dragging');
    // A ghost the officer can actually read — the default translucent snapshot of a
    // dense row is unreadable, and ambiguity here costs student records.
    ghostEl = document.createElement('div');
    ghostEl.className = 'rx-ghost';
    ghostEl.textContent = el.querySelector('.rx-course__code').textContent;
    document.body.appendChild(ghostEl);
    try { e.dataTransfer.setDragImage(ghostEl, 12, 12); } catch (x) { }
    e.dataTransfer.effectAllowed = 'move';
    try { e.dataTransfer.setData('text/plain', String(dragReg)); } catch (x2) { }
}
function onDragEnd(e) {
    e.currentTarget.classList.remove('is-dragging');
    if (ghostEl && ghostEl.parentNode) ghostEl.parentNode.removeChild(ghostEl);
    ghostEl = null; dragReg = null;
    clearTargets();
}
function clearTargets() {
    var s = document.querySelectorAll('.rx-sem');
    for (var i = 0; i < s.length; i++) s[i].classList.remove('is-drop-target', 'is-drop-invalid');
}
function onDragOver(e) {
    if (dragReg === null) return;
    e.preventDefault();
    var sem = e.currentTarget;
    var src = document.querySelector('.rx-course[data-reg="' + dragReg + '"]');
    var same = src && src.getAttribute('data-year') === sem.getAttribute('data-year') &&
                     src.getAttribute('data-sem') === sem.getAttribute('data-sem');
    sem.classList.add(same ? 'is-drop-invalid' : 'is-drop-target');
    e.dataTransfer.dropEffect = same ? 'none' : 'move';
}
function onDragLeave(e) { e.currentTarget.classList.remove('is-drop-target', 'is-drop-invalid'); }
function onDrop(e) {
    e.preventDefault();
    var sem = e.currentTarget;
    clearTargets();
    if (dragReg === null) return;
    var y = +sem.getAttribute('data-year'), sm = +sem.getAttribute('data-sem');
    delete SHUT[y + '/' + sm];      // dropping into a folded semester opens it
    requestMove(dragReg, y, sm);
}

function findCourse(regId) {
    var rows = allRows();
    for (var i = 0; i < rows.length; i++) if (rows[i].regId === regId) return rows[i];
    return null;
}

function requestMove(regId, toYear, toSem) {
    var c = findCourse(regId);
    if (!c) return;
    if (c._isNew) {
        for (var i = 0; i < PEND.adds.length; i++)
            if (PEND.adds[i].tempId === regId) { PEND.adds[i].toYear = toYear; PEND.adds[i].toSem = toSem; }
        render(); settle(regId); return;
    }
    var eff = effective(c);
    if (eff.year === toYear && eff.sem === toSem) { render(); return; }

    var from = { y: c.studyYear, s: c.semester };
    function commit(overrideReason) {
        if (toYear === from.y && toSem === from.s) delete PEND.moves[regId];
        else PEND.moves[regId] = { toYear: toYear, toSem: toSem, fromYear: from.y, fromSem: from.s,
                                   overrideReason: overrideReason || '' };
        render(); settle(regId);
    }

    if (c.locked) {
        if (!DATA.canOverrideLock) {
            toast('These results are at ' + c.lockStatus.replace(/_/g, ' ') + ' and are locked. ' +
                  'Your role may not override a results lock.', true);
            render(); return;
        }
        askReason({
            title: 'Override a results lock', kind: 'move',
            context: '<div class="rx-err"><b>' + esc(c.course) + '</b> is at status <b>' +
                     esc(c.lockStatus.replace(/_/g, ' ')) + '</b>.' +
                     (c.lockStatus === 'FINAL_PUBLISHED'
                        ? '<br /><br />These results have been <b>finally published by Senate</b>. Moving this course ' +
                          'changes a published academic record and the student\'s semester GPA. Do not proceed unless ' +
                          'you are certain and have the authority.'
                        : '<br /><br />Moving it changes results that are no longer open for editing.') + '</div>',
            min: DATA.minOverrideReason,
            hint: 'At least ' + DATA.minOverrideReason + ' characters. Recorded as an override and reportable.'
        }, commit);
        return;
    }
    commit('');
}

function settle(regId) {
    var el = document.querySelector('.rx-course[data-reg="' + regId + '"]');
    if (!el) return;
    el.classList.add('is-settling');
    setTimeout(function () { el.classList.remove('is-settling'); }, 300);
}

function onMarkEdit(regId, field, raw) {
    var c = findCourse(regId);
    if (!c || c._isNew) return;
    var was = field === 'cw' ? c.cw : c.exam;
    var val = raw === '' ? null : parseInt(raw, 10);
    if (raw !== '' && (isNaN(val) || val < 0 || val > (field === 'cw' ? 40 : 60))) {
        toast((field === 'cw' ? 'Coursework' : 'Exam') + ' must be between 0 and ' + (field === 'cw' ? 40 : 60) + '.', true);
        render(); return;
    }
    if (String(was === null || was === undefined ? '' : was) === String(val === null ? '' : val)) {
        if (PEND.marks[regId]) { delete PEND.marks[regId][field]; }
        render(); return;
    }

    function commit(reason, overrideReason) {
        PEND.marks[regId] = PEND.marks[regId] || {};
        PEND.marks[regId][field] = { from: was, to: val, reason: reason, overrideReason: overrideReason || '' };
        render(); settle(regId);
    }

    if (c.locked) {
        if (!DATA.canOverrideLock) {
            toast('These results are at ' + c.lockStatus.replace(/_/g, ' ') + ' and are locked. ' +
                  'Your role may not override a results lock.', true);
            render(); return;
        }
        askReason({
            title: 'Change a locked mark', kind: 'mark',
            context: '<div class="rx-err"><b>' + esc(c.course) + '</b> is at status <b>' +
                     esc(c.lockStatus.replace(/_/g, ' ')) + '</b>.' +
                     (c.lockStatus === 'FINAL_PUBLISHED'
                        ? '<br /><br />This mark has been <b>finally published by Senate</b>. Changing it alters a ' +
                          'published result, the semester GPA and possibly the classification.'
                        : '') + '</div>' +
                     '<div class="rx-warn">' + esc(field === 'cw' ? 'Coursework' : 'Exam') + ': <b>' +
                     fmt(was) + '</b> → <b>' + fmt(val) + '</b></div>',
            min: DATA.minOverrideReason,
            hint: 'At least ' + DATA.minOverrideReason + ' characters. Recorded as an override.'
        }, function (ov) {
            askReason({
                title: 'Reason for this mark change', kind: 'mark',
                context: '<div class="rx-warn">' + esc(c.course) + ' — ' + esc(field === 'cw' ? 'coursework' : 'exam') +
                         ' <b>' + fmt(was) + '</b> → <b>' + fmt(val) + '</b></div>',
                min: DATA.minOpReason,
                hint: 'Every mark change carries its own reason.'
            }, function (rsn) { commit(rsn, ov); });
        });
        return;
    }

    askReason({
        title: 'Reason for this mark change', kind: 'mark',
        context: '<div class="rx-warn">' + esc(c.course) + ' — ' + esc(field === 'cw' ? 'coursework' : 'exam') +
                 ' <b>' + fmt(was) + '</b> → <b>' + fmt(val) + '</b></div>',
        min: DATA.minOpReason,
        hint: 'Every mark change carries its own reason. At least ' + DATA.minOpReason + ' characters.'
    }, function (rsn) { commit(rsn, ''); }, function () { render(); });
}

function requestDelete(regId) {
    var c = findCourse(regId);
    if (!c) return;
    var warn = '';
    if (c.resultId) warn = '<div class="rx-err">' + esc(c.course) + ' has a <b>published result</b> (grade ' +
                           esc(c.grade || '—') + '). Removing the registration leaves that result orphaned.</div>';
    askReason({
        title: 'Remove this course registration', kind: 'remove',
        context: warn + '<div class="rx-warn">The registration is <b>archived, not destroyed</b>. ' +
                 'It can be restored in full from the Rearrangement Logs at any time.</div>',
        min: DATA.minOpReason,
        hint: 'At least ' + DATA.minOpReason + ' characters.'
    }, function (rsn) {
        PEND.deletes[regId] = { reason: rsn };
        delete PEND.moves[regId]; delete PEND.marks[regId];
        render();
    });
}

/* ── reason prompt ────────────────────────────────────────────────────── */
var reasonCb = null, reasonCancel = null, reasonMin = 10;
function askReason(opts, cb, onCancel) {
    reasonCb = cb; reasonCancel = onCancel || null; reasonMin = opts.min || 10;
    fillChips('rx-prompt-chips', opts.kind || 'session', 'rx-reason-text');
    qs('rx-reason-title').textContent = opts.title;
    qs('rx-reason-context').innerHTML = opts.context || '';
    qs('rx-reason-text').value = '';
    qs('rx-reason-hint2').className = 'rx-hint';
    qs('rx-reason-hint2').textContent = opts.hint || '';
    qs('rx-reason-ok').disabled = true;
    openModal('rx-reason-modal');
    setTimeout(function () { qs('rx-reason-text').focus(); }, 60);
}
qs('rx-reason-text').addEventListener('input', function () {
    var v = this.value.trim();
    qs('rx-reason-ok').disabled = v.length < reasonMin;
    var h = qs('rx-reason-hint2');
    if (v.length === 0) { h.className = 'rx-hint'; }
    else if (v.length < reasonMin) { h.className = 'rx-hint rx-hint--bad'; h.textContent = (reasonMin - v.length) + ' more character(s) needed.'; }
    else { h.className = 'rx-hint rx-hint--ok'; h.textContent = 'Reason accepted.'; }
});
qs('rx-reason-ok').addEventListener('click', function () {
    var v = qs('rx-reason-text').value.trim();
    if (v.length < reasonMin) return;
    closeModal('rx-reason-modal');
    var cb = reasonCb; reasonCb = null; reasonCancel = null;
    if (cb) cb(v);
});
qs('rx-reason-modal').addEventListener('click', function (e) {
    if (e.target.getAttribute && e.target.getAttribute('data-close')) {
        var c = reasonCancel; reasonCb = null; reasonCancel = null;
        if (c) c();
    }
});

/* ── add a course ─────────────────────────────────────────────────────── */
var addYear = 0, addSem = 0, addAcad = '', addTimer = null;

function acadYearOfSlot(y, s) {
    var t = moveTargets();
    for (var i = 0; i < t.length; i++) if (t[i].year === y && t[i].sem === s) return t[i].acadYear || '';
    return '';
}

function openAdd(y, s) {
    addYear = y; addSem = s; addAcad = acadYearOfSlot(y, s);
    qs('rx-add-title').textContent = 'Add a course to Year ' + y + ' Semester ' + s +
                                     (addAcad ? ' (' + addAcad + ')' : '');
    qs('rx-add-q').value = ''; qs('rx-add-reason').value = '';
    qs('rx-add-results').innerHTML = '<div class="rx-state" style="padding:18px">Type to search the catalogue.</div>';
    fillChips('rx-add-chips', 'add', 'rx-add-reason');
    openModal('rx-add-modal');
    setTimeout(function () { qs('rx-add-q').focus(); }, 60);
}
qs('rx-add-q').addEventListener('input', function () {
    clearTimeout(addTimer);
    var q = this.value.trim();
    addTimer = setTimeout(function () {
        call('SearchCourses', {
            q: q, progId: DATA.student.prog, regno: DATA.student.regno,
            acadYear: addAcad, semester: addSem
        }, function (d) {
            if (!d || !d.success) { qs('rx-add-results').innerHTML = '<div class="rx-err">' + esc(d && d.message || 'Search failed') + '</div>'; return; }
            if (!d.rows.length) { qs('rx-add-results').innerHTML = '<div class="rx-state" style="padding:18px">No matching course.</div>'; return; }
            var h = '';
            for (var i = 0; i < d.rows.length; i++) {
                var r = d.rows[i];
                // A course the student already holds in this term cannot be added: the unique
                // key would reject it. Show it greyed with the reason rather than hiding it,
                // which would only leave the officer wondering where it went.
                var bits = [];
                if (r.creditUnits) bits.push(r.creditUnits + ' CU');
                bits.push(r.onCurriculum
                    ? 'curriculum: Year ' + r.curriculum.year + ' Semester ' + r.curriculum.semester
                    : 'not on this programme\'s curriculum');
                if (r.heldHere) bits.push('already registered in this semester');
                else if (r.heldElsewhere) bits.push('held in ' + r.heldElsewhere + ' other term' +
                                                    (r.heldElsewhere === 1 ? '' : 's'));

                h += '<div class="rx-pickrow' + (r.heldHere ? ' is-held' : '') +
                     (r.onCurriculum && !r.heldHere ? ' is-oncur' : '') + '"' +
                     (r.heldHere ? '' :
                        ' data-pick="' + esc(r.course) + '" data-title="' + esc(r.title) +
                        '" data-cu="' + r.creditUnits + '" data-cy="' + r.curriculum.year +
                        '" data-cs="' + r.curriculum.semester + '"') + '>' +
                     '<div style="flex:1"><b>' + esc(r.course) + '</b> — ' + esc(r.title) +
                     '<div class="rx-sub2">' + esc(bits.join(' · ')) + '</div></div>' +
                     (r.heldHere ? '<span class="rx-pickrow__no">unavailable</span>'
                                 : '<span class="rx-pickrow__go">Add</span>') + '</div>';
            }
            qs('rx-add-results').innerHTML = h;
            bindAll('[data-pick]', 'click', pickCourse);
        });
    }, 220);
});
function pickCourse(e) {
    var el = e.currentTarget;
    if (el.classList.contains('is-held')) return;
    var code = el.getAttribute('data-pick');
    // Also refuse a course already queued for this term in the pending set — the server has
    // not seen those yet, so only the client can catch that one.
    for (var p = 0; p < PEND.adds.length; p++)
        if (PEND.adds[p].course === code && PEND.adds[p].toYear === addYear && PEND.adds[p].toSem === addSem) {
            toast(code + ' is already in your pending changes for this semester.', true);
            return;
        }
    var reason = qs('rx-add-reason').value.trim();
    if (reason.length < (DATA.minOpReason || 10)) {
        toast('Type a reason for adding the course first (at least ' + (DATA.minOpReason || 10) + ' characters).', true);
        qs('rx-add-reason').focus(); return;
    }
    var cy = +el.getAttribute('data-cy'), cs = +el.getAttribute('data-cs');
    if (cy > 0 && (cy !== addYear || cs !== addSem)) {
        if (!confirm('The curriculum places ' + el.getAttribute('data-pick') + ' in Year ' + cy +
                     ' Semester ' + cs + ', but you are adding it to Year ' + addYear + ' Semester ' + addSem +
                     '.\n\nAdd it anyway? The deviation is recorded.')) return;
    }
    PEND.adds.push({
        tempId: TEMP--, course: el.getAttribute('data-pick'), title: el.getAttribute('data-title'),
        creditUnits: +el.getAttribute('data-cu'), toYear: addYear, toSem: addSem, reason: reason,
        curriculum: { year: cy, semester: cs }
    });
    closeModal('rx-add-modal');
    render();
}

/* ── register a semester ──────────────────────────────────────────────── */
/* Fill the three dropdowns from what the institution actually runs, and default to the
   next semester after the student's latest — which is what is being registered nine times
   out of ten. */
function fillRegsemForm() {
    var ay = qs('rx-rs-acad'), yr = qs('rx-rs-year'), sm = qs('rx-rs-sem');
    var years = (DATA && DATA.acadYears) ? DATA.acadYears : [];
    var h = '';
    for (var i = 0; i < years.length; i++) h += '<option value="' + esc(years[i]) + '">' + esc(years[i]) + '</option>';
    ay.innerHTML = h || '<option value="">no academic years found</option>';

    var maxY = (DATA && DATA.maxStudyYear) || 8, maxS = (DATA && DATA.maxSemester) || 3;
    var hy = ''; for (var y = 1; y <= maxY; y++) hy += '<option value="' + y + '">' + y + '</option>';
    yr.innerHTML = hy;
    var hs = ''; for (var t = 1; t <= maxS; t++) hs += '<option value="' + t + '">' + t + '</option>';
    sm.innerHTML = hs;

    // Suggest the term after the student's latest registration.
    var last = null;
    for (var k = 0; k < DATA.semesters.length; k++) {
        var s2 = DATA.semesters[k];
        if (!last || s2.studyYear > last.studyYear ||
            (s2.studyYear === last.studyYear && s2.semester > last.semester)) last = s2;
    }
    if (last) {
        var ny = last.studyYear, ns = last.semester + 1;
        if (ns > maxS) { ns = 1; ny = Math.min(ny + 1, maxY); }
        yr.value = ny; sm.value = ns;
        if (years.indexOf(last.acadYear) >= 0) ay.value = last.acadYear;
    }
    regsemCheck();
}

/* Live duplicate check. The server refuses a duplicate anyway; saying so here means the
   officer never builds a batch around one. */
function regsemCheck() {
    var box = qs('rx-rs-check');
    var acad = qs('rx-rs-acad').value, y = +qs('rx-rs-year').value, s2 = +qs('rx-rs-sem').value;
    var msg = '', cls = 'rx-check rx-check--ok', ok = true;

    var existing = null;
    for (var i = 0; i < DATA.semesters.length; i++) {
        var r = DATA.semesters[i];
        if (r.acadYear === acad && r.semester === s2 && r.studyYear === y) { existing = r; break; }
    }
    var pending = null;
    for (var j = 0; j < PEND.regsems.length; j++) {
        var p = PEND.regsems[j];
        if (p.acadYear === acad && p.toSem === s2 && p.toYear === y) { pending = p; break; }
    }
    // Same year of study on a different academic year is nearly always a mistake.
    var clash = null;
    for (var k = 0; k < DATA.semesters.length; k++) {
        var c2 = DATA.semesters[k];
        if (c2.studyYear === y && c2.semester === s2 && c2.acadYear !== acad) { clash = c2; break; }
    }

    if (existing) {
        ok = false; cls = 'rx-check rx-check--bad';
        msg = 'Already registered: ' + esc(acad) + ' Year ' + y + ' Semester ' + s2 +
              ' exists on this record (' + esc(existing.status) + ').';
    } else if (pending) {
        ok = false; cls = 'rx-check rx-check--bad';
        msg = 'Already in your pending changes for this sitting.';
    } else if (clash) {
        cls = 'rx-check rx-check--warn';
        msg = 'Year ' + y + ' Semester ' + s2 + ' already exists on this record under ' +
              esc(clash.acadYear) + '. Adding it under ' + esc(acad) +
              ' will give the student two of the same term.';
    } else {
        msg = 'Year ' + y + ' Semester ' + s2 + ' under ' + esc(acad) + ' is free on this record.';
    }
    box.className = cls;
    box.innerHTML = msg;
    qs('rx-rs-add').disabled = !ok;
    return ok;
}

qs('rx-regsem').addEventListener('click', function () {
    qs('rx-rs-reason').value = ''; qs('rx-rs-bill').checked = false;
    fillRegsemForm();
    fillChips('rx-rs-chips', 'regsem', 'rx-rs-reason');
    openModal('rx-regsem-modal');
});
['rx-rs-acad', 'rx-rs-year', 'rx-rs-sem'].forEach(function (id) {
    qs(id).addEventListener('change', regsemCheck);
});
qs('rx-rs-add').addEventListener('click', function () {
    var acad = qs('rx-rs-acad').value.trim();
    var y = +qs('rx-rs-year').value, s = +qs('rx-rs-sem').value;
    var reason = qs('rx-rs-reason').value.trim();
    if (!acad) { toast('Choose the academic year.', true); return; }
    if (!regsemCheck()) { toast('That semester is already on the record.', true); return; }
    if (reason.length < (DATA.minOpReason || 10)) { toast('Type a reason (at least ' + (DATA.minOpReason || 10) + ' characters).', true); return; }
    PEND.regsems.push({ acadYear: acad, toYear: y, toSem: s, bill: qs('rx-rs-bill').checked, reason: reason });
    closeModal('rx-regsem-modal');
    render();
});

/* ── review & save ────────────────────────────────────────────────────── */
function buildOps() {
    var ops = [];
    for (var i = 0; i < PEND.regsems.length; i++) {
        var r = PEND.regsems[i];
        ops.push({ op: 'REGSEM', toYear: r.toYear, toSem: r.toSem, acadYear: r.acadYear, bill: r.bill, reason: r.reason });
    }
    for (var j = 0; j < PEND.adds.length; j++) {
        var a = PEND.adds[j];
        ops.push({ op: 'ADD', course: a.course, toYear: a.toYear, toSem: a.toSem, reason: a.reason });
    }
    for (var k in PEND.moves) if (PEND.moves.hasOwnProperty(k)) {
        var m = PEND.moves[k];
        ops.push({ op: 'MOVE', regId: +k, toYear: m.toYear, toSem: m.toSem,
                   reason: 'Moved from Year ' + m.fromYear + ' Semester ' + m.fromSem,
                   overrideReason: m.overrideReason || '' });
    }
    for (var k2 in PEND.marks) if (PEND.marks.hasOwnProperty(k2)) {
        var mm = PEND.marks[k2];
        if (mm.cw) ops.push({ op: 'MARK', regId: +k2, field: 'cw', value: mm.cw.to, reason: mm.cw.reason, overrideReason: mm.cw.overrideReason || '' });
        if (mm.exam) ops.push({ op: 'MARK', regId: +k2, field: 'exam', value: mm.exam.to, reason: mm.exam.reason, overrideReason: mm.exam.overrideReason || '' });
    }
    for (var k3 in PEND.deletes) if (PEND.deletes.hasOwnProperty(k3))
        ops.push({ op: 'DELETE', regId: +k3, reason: PEND.deletes[k3].reason });
    return ops;
}

function plain(op) {
    var c;
    switch (op.op) {
        case 'REGSEM':
            return 'Register the student into ' + op.acadYear + ' Year ' + op.toYear + ' Semester ' + op.toSem +
                   (op.bill ? ' — and create fee billing' : ' — without creating fee billing');
        case 'ADD':
            return op.course + ' registered into Year ' + op.toYear + ' Semester ' + op.toSem;
        case 'MOVE':
            c = findCourse(op.regId);
            return (c ? c.course : 'Course ' + op.regId) + ' moved from Year ' + PEND.moves[op.regId].fromYear +
                   ' Semester ' + PEND.moves[op.regId].fromSem + ' to Year ' + op.toYear + ' Semester ' + op.toSem;
        case 'MARK':
            c = findCourse(op.regId);
            var mk = PEND.marks[op.regId][op.field];
            return (c ? c.course : 'Course ' + op.regId) + ' ' + (op.field === 'cw' ? 'coursework' : 'exam') +
                   ' mark changed from ' + fmt(mk.from) + ' to ' + fmt(mk.to) + ', reason: ' + op.reason;
        case 'DELETE':
            c = findCourse(op.regId);
            return (c ? c.course : 'Course ' + op.regId) + ' registration removed (archived and reversible), reason: ' + op.reason;
    }
    return op.op;
}
var CLS = { MOVE: 'is-moved', ADD: 'is-added', DELETE: 'is-deleted', MARK: 'is-mark', REGSEM: 'is-regsem' };
var GROUP = { REGSEM: 'Semester registrations', ADD: 'Courses added', MOVE: 'Courses moved',
              MARK: 'Mark changes', DELETE: 'Registrations removed' };

qs('rx-save').addEventListener('click', function () {
    var ops = buildOps();
    if (!ops.length) return;

    var h = '<div class="rx-review__reason"><b>Reason for this sitting</b>' + esc(DATA.session.reason) + '</div>';

    var warns = [];
    for (var i = 0; i < ops.length; i++) {
        if (ops[i].overrideReason) {
            var cc = findCourse(ops[i].regId);
            warns.push('<b>' + esc(cc ? cc.course : '') + '</b> overrides a results lock' +
                       (cc && cc.lockStatus === 'FINAL_PUBLISHED' ? ' on <b>finally published</b> results' : '') +
                       ' — ' + esc(ops[i].overrideReason));
        }
        if (ops[i].op === 'REGSEM' && ops[i].bill) warns.push('A semester registration will <b>create fee billing</b>.');
    }
    if (warns.length) h += '<div class="rx-warn"><b>Warnings</b><br />• ' + warns.join('<br />• ') + '</div>';

    var order = ['REGSEM', 'ADD', 'MOVE', 'MARK', 'DELETE'], n = 0;
    for (var g = 0; g < order.length; g++) {
        var kind = order[g];
        var items = ops.filter(function (o) { return o.op === kind; });
        if (!items.length) continue;
        h += '<div class="rx-review__group"><div class="rx-review__gh">' + GROUP[kind] + ' (' + items.length + ')</div>';
        for (var m = 0; m < items.length; m++) {
            n++;
            h += '<div class="rx-review__item ' + CLS[kind] + '"><span class="rx-review__n">' + n + '</span>' +
                 '<span>' + esc(plain(items[m])) + '</span></div>';
        }
        h += '</div>';
    }
    h += '<div class="rx-warn" style="background:#f8fafc;border-color:#e0e5ed;border-left-color:#05275C;color:#1a1a2e">' +
         'Saving applies all ' + ops.length + ' change(s) in a single database transaction. ' +
         'If any one of them fails, <b>none</b> is written. Semester GPA and CGPA are recalculated afterwards ' +
         'and the old and new values are recorded in the log.</div>';

    qs('rx-review-body').innerHTML = h;
    qs('rx-confirm').disabled = false;
    qs('rx-confirm').textContent = 'Confirm and save ' + ops.length + ' change(s)';
    OP_ID = uuid();      // one id per review; a retry of THIS save cannot double-apply
    openModal('rx-review-modal');
});

qs('rx-confirm').addEventListener('click', function () {
    if (SAVING) return;
    SAVING = true;
    var btn = this; btn.disabled = true; btn.textContent = 'Saving…';
    call('Save', {
        sessionId: SESSION, clientOpId: OP_ID,
        opsJson: JSON.stringify(buildOps()), checksum: CHECKSUM
    }, function (d) {
        SAVING = false; btn.disabled = false;
        if (!d || !d.success) {
            qs('rx-review-body').insertAdjacentHTML('afterbegin',
                '<div class="rx-err">' + esc(d && d.message || 'The save failed. Nothing was written.') + '</div>');
            qs('rx-review-body').scrollTop = 0;
            btn.textContent = 'Try again';
            return;
        }
        closeModal('rx-review-modal');
        PEND = { moves: {}, marks: {}, deletes: {}, adds: [], regsems: [] };
        var msg = d.message;
        if (d.recalculated && d.recalculated.cgpaBefore !== d.recalculated.cgpaAfter)
            msg += ' CGPA ' + d.recalculated.cgpaBefore + ' → ' + d.recalculated.cgpaAfter + '.';
        toast(msg);
        qs('rx-boot').style.display = ''; qs('rx-workspace').style.display = 'none';
        loadWorkspace();
    });
});

qs('rx-expand').addEventListener('click', function () {
    var rows = allRows(), any = false;
    for (var i = 0; i < rows.length; i++) if (!OPEN[rows[i].regId]) { any = true; break; }
    OPEN = {};
    if (any) for (var j = 0; j < rows.length; j++) OPEN[rows[j].regId] = true;
    SHUT = {};
    render();
});

qs('rx-discard').addEventListener('click', function () {
    if (!confirm('Discard all pending changes?\n\nNothing has been written to the database, so this simply clears the screen.')) return;
    PEND = { moves: {}, marks: {}, deletes: {}, adds: [], regsems: [] };
    render();
});
qs('rx-reload').addEventListener('click', function () {
    if (pendingCount() > 0 && !confirm('You have unsaved changes. Reloading from the database will discard them.\n\nContinue?')) return;
    PEND = { moves: {}, marks: {}, deletes: {}, adds: [], regsems: [] };
    qs('rx-boot').style.display = ''; qs('rx-workspace').style.display = 'none';
    loadWorkspace();
});

window.addEventListener('beforeunload', function (e) {
    if (pendingCount() === 0 || SAVING) return;
    e.preventDefault(); e.returnValue = '';
    return '';
});

/* ── boot ─────────────────────────────────────────────────────────────── */
/* Resume the sitting named in the URL; otherwise offer the gate, prefilled if asked. */
(function () {
    var sid = parseInt(urlParam('session') || '0', 10);
    if (sid > 0) {
        SESSION = sid;
        qs('rx-gate').style.display = 'none';
        qs('rx-boot').style.display = '';
        loadWorkspace();
    } else {
        var rg = urlParam('regno');
        if (rg) qs('rx-regno').value = rg;
    }
})();

window.addEventListener('popstate', function () {
    var sid = parseInt(urlParam('session') || '0', 10);
    if (sid === SESSION) return;
    if (pendingCount() > 0 &&
        !confirm('You have unsaved changes. Leaving this sitting will discard them.\n\nContinue?')) {
        setSessionUrl(SESSION, true);
        return;
    }
    PEND = { moves: {}, marks: {}, deletes: {}, adds: [], regsems: [] };
    if (sid > 0) {
        SESSION = sid;
        qs('rx-gate').style.display = 'none';
        qs('rx-workspace').style.display = 'none';
        qs('rx-boot').style.display = '';
        loadWorkspace();
    } else {
        SESSION = 0;
        qs('rx-workspace').style.display = 'none';
        qs('rx-boot').style.display = 'none';
        qs('rx-gate').style.display = '';
        gateCheck();
    }
});

qs('rx-regno').addEventListener('input', function () { gateCheck(); studentSearch(); });
qs('rx-regno').addEventListener('keydown', stuKey);
fillChips('rx-reason-chips', 'session', 'rx-reason', gateCheck);
qs('rx-reason').addEventListener('input', gateCheck);
qs('rx-ack').addEventListener('change', gateCheck);
qs('rx-open').addEventListener('click', openSession);
qs('rx-regno').addEventListener('keydown', function (e) {
    if (e.key !== 'Enter' && e.keyCode !== 13) return;
    if (qs('rx-stu-list').classList.contains('is-open')) return;   // the list handles Enter
    if (!qs('rx-open').disabled) { e.preventDefault(); openSession(); }
});
gateCheck();

})();
