/* =====================================================================
   Graduation module — shared behaviour for the four pages.

   Everything here is on window.G. Each page loads this once, then adds
   only its own logic. Nothing in here knows which page it is on: the
   page passes its own name to G.ajax, so a page never accidentally calls
   a sibling's endpoint.

   Deliberately no framework. The whole module is four screens of tables
   and one modal; a build step would cost more than it saves.
   ===================================================================== */
window.G = (function () {
    'use strict';

    function qs(id) { return document.getElementById(id); }

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (m) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m];
        });
    }

    function n0(v) { return String(Math.round(+v || 0)); }
    function n1(v) { return (Math.round((+v || 0) * 10) / 10).toFixed(1); }
    function n2(v) { return (Math.round((+v || 0) * 100) / 100).toFixed(2); }

    /* The page name is explicit so a page can only ever call its own methods. */
    function ajax(page, method, params, cb) {
        var x = new XMLHttpRequest();
        x.open('POST', page + '/' + method, true);
        x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
        x.timeout = 180000;
        x.onload = function () {
            try {
                var o = JSON.parse(x.responseText);
                cb(typeof o.d === 'string' ? JSON.parse(o.d) : o.d);
            } catch (e) {
                cb({ success: false, message: 'The server did not return usable data.' });
            }
        };
        x.onerror = function () { cb({ success: false, message: 'Network error.' }); };
        x.ontimeout = function () {
            cb({ success: false, message: 'That took too long — narrow the filters and try again.' });
        };
        x.send(JSON.stringify(params || {}));
    }

    var toastTimer = null;
    function toast(text, good) {
        var t = qs('gToast');
        if (!t) return;
        t.className = 'g-toast g-toast--' + (good ? 'ok' : 'bad');
        t.textContent = text;
        t.style.display = 'block';
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { t.style.display = 'none'; }, good ? 4200 : 7000);
    }

    /* A cached 72px square, not the 469KB original. See StudentThumb.ashx. */
    function photo(regno) {
        return 'StudentThumb.ashx?r=' + encodeURIComponent(regno || '');
    }

    function photoCell(regno, name, sub) {
        return '<div class="g-who">' +
            '<img class="g-ph" loading="lazy" alt="" src="' + photo(regno) + '" />' +
            '<div class="g-who__t"><b>' + esc(regno) + '</b>' +
            '<span>' + esc(name || '') + (sub ? ' · ' + esc(sub) : '') + '</span></div></div>';
    }

    function chip(g) {
        if (g.graduatedYear) return '<span class="g-chip g-chip--listed">On ' + esc(g.graduatedYear) + '</span>';
        if (g.holdReason) return '<span class="g-chip g-chip--held">Held</span>';
        if (g.readiness === 'BLOCKED') return '<span class="g-chip g-chip--blocked">Blocked</span>';
        if (g.readiness === 'WARN') return '<span class="g-chip g-chip--warn">Needs a look</span>';
        return '<span class="g-chip g-chip--ready">Ready</span>';
    }

    function credits(g) {
        if (g.cuSource === 'NONE') {
            return '<div class="g-bar2"><div class="g-bar2__t"><div class="g-bar2__f is-none" style="width:100%"></div></div>' +
                '<div class="g-bar2__v">' + n0(g.cuEarned) + ' · no bar</div></div>';
        }
        var pct = g.cuRequired > 0 ? Math.min(100, Math.round(g.cuEarned * 100 / g.cuRequired)) : 0;
        var short = g.cuEarned < g.cuRequired;
        return '<div class="g-bar2"><div class="g-bar2__t"><div class="g-bar2__f' + (short ? ' is-short' : '') +
            '" style="width:' + pct + '%"></div></div>' +
            '<div class="g-bar2__v">' + n0(g.cuEarned) + '/' + n0(g.cuRequired) + '</div></div>';
    }

    /* ── URL state. A view has to be linkable; the sidebar links into these. ── */
    function readUrl() {
        var p = new URLSearchParams(location.search), o = {};
        p.forEach(function (v, k) { o[k] = v; });
        return o;
    }

    function writeUrl(obj) {
        var p = new URLSearchParams();
        for (var k in obj) {
            if (obj.hasOwnProperty(k) && obj[k] !== '' && obj[k] != null) p.set(k, obj[k]);
        }
        var q = p.toString();
        history.replaceState(null, '', location.pathname + (q ? '?' + q : ''));
    }

    /* ── Filter helpers, shared so the four pages cascade identically. ── */
    function fill(id, items, allLabel, keep) {
        var el = qs(id);
        if (!el) return;
        var cur = keep ? el.value : '';
        var h = allLabel != null ? '<option value="">' + esc(allLabel) + '</option>' : '';
        for (var i = 0; i < items.length; i++) {
            h += '<option value="' + esc(items[i].v) + '"' +
                (items[i].fac !== undefined ? ' data-fac="' + esc(items[i].fac) + '"' : '') +
                (items[i].dep !== undefined ? ' data-dep="' + esc(items[i].dep) + '"' : '') +
                '>' + esc(items[i].t) + '</option>';
        }
        el.innerHTML = h;
        if (cur) el.value = cur;
    }

    function cascade(facId, depId, progId) {
        var fac = qs(facId) ? qs(facId).value : '';
        var dep = qs(depId), prog = qs(progId), i, o;
        if (dep) {
            for (i = 0; i < dep.options.length; i++) {
                o = dep.options[i];
                o.hidden = !(!o.value || !fac || o.getAttribute('data-fac') === fac);
            }
            if (dep.selectedIndex > -1 && dep.options[dep.selectedIndex].hidden) dep.value = '';
        }
        if (prog) {
            var d = dep ? dep.value : '';
            for (i = 0; i < prog.options.length; i++) {
                o = prog.options[i];
                var okF = (!fac || o.getAttribute('data-fac') === fac);
                var okD = (!d || o.getAttribute('data-dep') === d);
                o.hidden = !(!o.value || (okF && okD));
            }
            if (prog.selectedIndex > -1 && prog.options[prog.selectedIndex].hidden) prog.value = '';
        }
    }

    /* Injects the one modal and the one toast every page shares, so no page carries a copy
       of markup it does not own. Called once, at the top of each page's own script. */
    function mount() {
        if (qs('gOv')) return;
        var d = document.createElement('div');
        d.innerHTML =
            '<div class="g-ov" id="gOv">' +
              '<div class="g-modal" role="dialog" aria-modal="true" aria-labelledby="gModalName">' +
                '<div class="g-modal__h">' +
                  '<img class="g-ph" id="gModalPhoto" alt="" src="" />' +
                  '<div class="g-modal__t"><b id="gModalName">&nbsp;</b><span id="gModalSub">&nbsp;</span></div>' +
                  '<button type="button" class="g-modal__x" id="gModalX" aria-label="Close">&times;</button>' +
                '</div>' +
                '<div class="g-modal__b" id="gModalBody"></div>' +
                '<div class="g-modal__f" id="gModalFoot"></div>' +
              '</div>' +
            '</div>' +
            '<div class="g-toast" id="gToast"></div>';
        while (d.firstChild) document.body.appendChild(d.firstChild);
    }

    /* ── The modal: centred at the top, closed by Escape, the backdrop, or the X. ── */
    var onClose = null;
    function openModal() {
        var ov = qs('gOv');
        if (!ov) return;
        ov.classList.add('is-open');
        document.body.style.overflow = 'hidden';
    }

    function closeModal() {
        var ov = qs('gOv');
        if (!ov) return;
        ov.classList.remove('is-open');
        document.body.style.overflow = '';
        if (onClose) onClose();
    }

    function wireModal(afterClose) {
        onClose = afterClose || null;
        var ov = qs('gOv');
        if (!ov) return;
        // Only a click on the backdrop itself closes it — not a click that happened to
        // start inside the card and drifted out while selecting text.
        ov.addEventListener('mousedown', function (e) { if (e.target === ov) closeModal(); });
        var x = qs('gModalX');
        if (x) x.addEventListener('click', closeModal);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && ov.classList.contains('is-open')) closeModal();
        });
    }

    /* ── The evidence panel ───────────────────────────────────────────
       Identical wherever a student is opened, because a Registrar must not have to learn two
       layouts for the same question. The page supplies only the buttons, since what you may do
       with a candidate differs from what you may do with a name already on a list.

       The student is re-read and re-assessed on the server for every open: a graduation
       decision is not taken on the numbers a list rendered some minutes ago.                */
    var current = null;

    /// The footer is written inside the AJAX callback, so a caller that wires its buttons
    /// with setTimeout(fn, 0) wires nothing — the timer fires long before the response lands.
    /// onReady is called after the footer exists, which is the only moment the buttons are
    /// there to be wired.
    function openStudent(page, regno, buildFooter, onReady) {
        current = null;
        qs('gModalName').textContent = regno;
        qs('gModalSub').textContent = 'Reading the record…';
        qs('gModalPhoto').src = photo(regno);
        qs('gModalBody').innerHTML = '<div class="g-load">Loading…</div>';
        qs('gModalFoot').innerHTML = '';
        openModal();

        ajax(page, 'GetStudent', { regno: regno }, function (d) {
            if (!d || !d.success) {
                qs('gModalBody').innerHTML =
                    '<div class="g-note g-note--bad">' + esc((d && d.message) || 'Could not load that student.') + '</div>';
                return;
            }
            current = d;
            renderStudent(d);
            qs('gModalFoot').innerHTML = buildFooter ? buildFooter(d.student) : '';
            if (onReady) onReady(d.student);
        });
    }

    function currentStudent() { return current; }

    function cell(label, val) {
        return '<div><span>' + esc(label) + '</span><b>' + esc(val) + '</b></div>';
    }

    /// One semester's results, as its own table.
    ///
    /// The shape is borrowed from StudentRearrangeManage, which lays a student's record out the
    /// same way: a year heading, then one table per semester, two to a row. A reviewer moving
    /// between the two screens should not have to learn a second way of reading the same record.
    function semPanel(sem, list, showAcad) {
        var i, cu = 0, fails = 0, gaps = 0;
        for (i = 0; i < list.length; i++) {
            var r = list[i];
            if (r.score !== null && r.score >= 50) cu += (+r.cu || 0);
            if (r.score !== null && r.score > 0 && r.score < 50) fails++;
            if (r.score === 0 || r.score === null) gaps++;
        }

        list.sort(function (a, b) { return String(a.code).localeCompare(String(b.code)); });

        var h = '<div class="g-sem' + (fails ? ' has-fail' : '') + '">' +
                '<div class="g-sem__hd">' +
                  '<span class="g-sem__n">' + esc(sem ? 'Semester ' + sem : 'Semester not recorded') + '</span>' +
                  (showAcad ? '<span class="g-sem__a">' + esc(showAcad) + '</span>' : '') +
                  (fails ? '<span class="g-sem__f">' + fails + ' failed</span>' : '') +
                  (gaps ? '<span class="g-sem__z">' + gaps + ' unmarked</span>' : '') +
                  '<span class="g-sem__c">' + list.length +
                    (list.length === 1 ? ' course' : ' courses') + '  ·  ' + n0(cu) + ' CU</span>' +
                '</div><table class="g-tbl g-sem__t"><tbody>';

        for (i = 0; i < list.length; i++) {
            var x = list[i];
            var bad = (x.score !== null && x.score > 0 && x.score < 50);
            var zero = (x.score === 0 || x.score === null);
            var cls = bad ? ' is-fail' : (zero ? ' is-zero' : '');
            h += '<tr><td class="g-sem__course"><b>' + esc(x.code) + '</b>' +
                 '<span>' + esc(x.name) + '</span></td>' +
                 '<td class="g-num g-sub">' + n0(x.cu) + '</td>' +
                 '<td class="g-num g-sem__s' + cls + '">' + (x.score === null ? '&ndash;' : esc(x.score)) + '</td>' +
                 '<td class="g-sem__g' + cls + '">' + esc(x.grade || '') + '</td></tr>';
        }
        return h + '</tbody></table></div>';
    }

    /// One year of study: a heading, then its semesters two to a row.
    function yearBlock(yr, sems, semKeys) {
        var i, j, courses = 0, cu = 0, fails = 0, acads = [], multi;

        for (i = 0; i < semKeys.length; i++) {
            var list = sems[semKeys[i]];
            for (j = 0; j < list.length; j++) {
                var r = list[j];
                courses++;
                if (r.score !== null && r.score >= 50) cu += (+r.cu || 0);
                if (r.score !== null && r.score > 0 && r.score < 50) fails++;
                if (r.acad && acads.indexOf(r.acad) === -1) acads.push(r.acad);
            }
        }
        acads.sort();

        // A year of study whose semesters sit in different academic years is real and common —
        // the rearrangement screen found 704 students like it. Say so, rather than printing one
        // of them and hiding the rest.
        multi = acads.length > 1;

        var meta = courses + ' course' + (courses === 1 ? '' : 's') + '  ·  ' + n0(cu) + ' CU';
        if (fails) meta += '  ·  ' + fails + ' failed';

        var h = '<div class="g-year">' +
                '<div class="g-year__hd"><b>' + esc(yr === 0 ? 'Not placed in a year' : 'Year ' + yr) + '</b>' +
                (acads.length
                    ? (multi
                        ? '<span class="g-year__split" title="This year of study spans more than one academic year">' +
                          esc(acads.join(' + ')) + '</span>'
                        : '<span class="g-year__a">' + esc(acads[0]) + '</span>')
                    : '') +
                '<span class="g-year__m">' + esc(meta) + '</span></div>' +
                '<div class="g-sems">';

        for (i = 0; i < semKeys.length; i++) {
            // The academic year is repeated on the semester only when the year straddles two,
            // because that is the only time it tells the reader something the heading did not.
            var sl = sems[semKeys[i]];
            var sa = '';
            if (multi) {
                var seen = [];
                for (j = 0; j < sl.length; j++)
                    if (sl[j].acad && seen.indexOf(sl[j].acad) === -1) seen.push(sl[j].acad);
                sa = seen.sort().join(', ');
            }
            h += semPanel(semKeys[i], sl, sa);
        }
        return h + '</div></div>';
    }

    function renderStudent(d) {
        var g = d.student, h = '', i;

        qs('gModalName').textContent = g.name + '  ·  ' + g.regno;
        qs('gModalSub').textContent = (g.progname || g.progcode) + '  ·  intake ' + (g.entryyear || '–') +
            '  ·  ' + (g.firstYear ? g.firstYear + ' to ' + g.lastYear : 'no results on record');

        var blocks = [], warns = [], passes = [];
        for (i = 0; i < (g.findings || []).length; i++) {
            if (g.findings[i].level === 'BLOCK') blocks.push(g.findings[i]);
            else if (g.findings[i].level === 'WARN' || g.findings[i].level === 'NA') warns.push(g.findings[i]);
            else if (g.findings[i].level === 'PASS') passes.push(g.findings[i]);
        }

        // ── What changes what you may do, before anything else ──────────────
        //  Two short lines. A student already on a list, or held, cannot be acted on the same
        //  way, so this is not something to find further down.
        if (g.graduatedYear)
            h += '<div class="g-note g-note--info"><b>Already on the ' + esc(g.graduatedYear) +
                 ' graduation list.</b>' + (g.clearedActor ? ' Cleared by ' + esc(g.clearedActor) +
                 (g.clearedAt ? ' on ' + esc(g.clearedAt) : '') + '.' : '') + '</div>';
        if (g.holdReason)
            h += '<div class="g-note g-note--warn"><b>Held by ' + esc(g.holdActor) + ' on ' +
                 esc(g.holdAt) + '.</b><br>' + esc(g.holdReason) + '</div>';

        // ── The results, first ──────────────────────────────────────────────
        //  What the student actually did is the evidence; everything else on this panel is a
        //  conclusion drawn from it. Laid out by study year, two years to a row, because that
        //  is how a degree is read — not as one long list sorted by academic year.
        var byYear = {}, years = [];
        for (i = 0; i < (d.results || []).length; i++) {
            var r = d.results[i];
            var y = +r.sy || 0, sm = +r.sem || 0;
            if (!byYear[y]) { byYear[y] = { sems: {}, keys: [] }; years.push(y); }
            if (!byYear[y].sems[sm]) { byYear[y].sems[sm] = []; byYear[y].keys.push(sm); }
            byYear[y].sems[sm].push(r);
        }
        years.sort(function (a, b) {
            if (a === 0) return 1;          // unplaced results last, never first
            if (b === 0) return -1;
            return a - b;
        });

        if (years.length) {
            for (i = 0; i < years.length; i++) {
                var yb = byYear[years[i]];
                yb.keys.sort(function (a, b) {
                    if (a === 0) return 1;  // "semester not recorded" after the real ones
                    if (b === 0) return -1;
                    return a - b;
                });
                h += yearBlock(years[i], yb.sems, yb.keys);
            }
        } else {
            h += '<div class="g-note g-note--warn">No results on record for this student.</div>';
        }

        // ── Then the verdict ────────────────────────────────────────────────
        //  What is blocking them stays on the face of it — that is the decision. What merely
        //  wants a look is folded away, because on this data most candidates carry several
        //  warnings and an open list of them buries the two lines that decide the case.
        var kind = blocks.length ? 'bad' : (warns.length ? 'warn' : 'ok');
        var head = blocks.length
            ? (blocks.length === 1 ? 'One thing is blocking this candidate' : blocks.length + ' things are blocking this candidate')
            : (warns.length
                ? (warns.length === 1 ? 'Ready, with one thing to look at' : 'Ready, with ' + warns.length + ' things to look at')
                : 'Ready — nothing outstanding');

        h += '<div class="g-verdict g-verdict--' + kind + '">' +
             '<div class="g-verdict__h">' + esc(head) + '</div>';
        if (blocks.length) {
            h += '<ul class="g-verdict__l">';
            for (i = 0; i < blocks.length; i++)
                h += '<li class="is-block"><b>' + esc(blocks[i].name) + '</b> ' + esc(blocks[i].detail) + '</li>';
            h += '</ul>';
        }
        if (warns.length) {
            var wl = '<ul class="g-verdict__l">';
            for (i = 0; i < warns.length; i++)
                wl += '<li class="is-warn"><b>' + esc(warns[i].name) + '</b> ' + esc(warns[i].detail) + '</li>';
            wl += '</ul>';
            h += fold('warns', warns.length === 1 ? 'One thing to look at' : 'Things to look at',
                      warns.length, wl, false, 'g-fold--in');
        }
        h += '</div>';

        // ── The numbers behind it ───────────────────────────────────
        var pct = g.cuRequired > 0 ? Math.min(100, Math.round(g.cuEarned * 100 / g.cuRequired)) : 0;
        h += '<div class="g-facts">' +
            fact('Credits', g.cuSource === 'NONE'
                    ? (n0(g.cuEarned) + ' earned')
                    : (n0(g.cuEarned) + ' of ' + n0(g.cuRequired)),
                 g.cuSource === 'NONE' ? 'no requirement recorded'
                    : '<div class="g-bar2" style="margin-top:3px"><div class="g-bar2__t"><div class="g-bar2__f' +
                      (g.cuEarned < g.cuRequired ? ' is-short' : '') + '" style="width:' + pct + '%"></div></div></div>', true) +
            fact('CGPA', g.cgpa ? n2(g.cgpa) : '–', g.degClass || 'no class mapped') +
            fact('Year reached', g.maxStudyYear + ' of ' + g.progLength, 'programme length') +
            fact('Courses', String(g.coursesTaken), 'on record') +
            fact('Failed papers', String(g.failedPapers), g.failedPapers ? 'marks of 1–49' : 'none') +
            fact('Zero / unmarked', (g.zeroMarks + g.missingScores) + '', 'usually not yet marked') +
            '</div>';

        h += '<div class="g-src">Credit requirement: ' + esc(g.cuSourceLabel || 'not established') +
             (g.specIsPlaceholder
                ? '. This student carries no real specialisation, so their courses cannot be matched to a curriculum.'
                : '.') + '</div>';

        // ── Everything else, folded away until wanted ───────────────────────
        if (d.structure && d.structure.length) {
            var miss = 0;
            for (i = 0; i < d.structure.length; i++) if (d.structure[i].score === null) miss++;
            var st = '<div class="g-wrap" style="max-height:260px;"><table class="g-tbl g-struct"><thead><tr>' +
                     '<th>Yr</th><th>Sem</th><th>Course</th><th class="g-num">CU</th><th>Result</th></tr></thead><tbody>';
            for (i = 0; i < d.structure.length; i++) {
                var c = d.structure[i];
                var cls2 = c.score === null ? 'miss' : (c.score > 0 && c.score < 50 ? 'fail' : '');
                st += '<tr><td>' + esc(c.sy) + '</td><td>' + esc(c.sem) + '</td>' +
                      '<td>' + esc(c.code) + '<div class="g-sub">' + esc(c.name) + '</div></td>' +
                      '<td class="g-num">' + n0(c.cu) + '</td><td class="' + cls2 + '">' +
                      (c.score === null ? 'no result' : c.score + '%') + '</td></tr>';
            }
            st += '</tbody></table></div>';
            h += fold('struct', 'Against the programme structure',
                      miss ? (miss + ' of ' + d.structure.length + ' with no result') : d.structure.length, st);
        }

        if (passes.length) {
            h += fold('checks', 'Checks that passed', passes.length, (function () {
                var x = '';
                for (var j = 0; j < passes.length; j++)
                    x += '<div class="g-find g-find--PASS"><div class="g-find__n">' + esc(passes[j].name) +
                         '</div><div class="g-find__d">' + esc(passes[j].detail) + '</div></div>';
                return x;
            })());
        }

        if (d.history && d.history.length) {
            var hi = '';
            for (i = 0; i < d.history.length; i++) {
                var e = d.history[i];
                hi += '<div class="g-find g-find--' +
                      (e.verdict === 'HELD' ? 'WARN' : (e.verdict === 'CLEARED' ? 'PASS' : 'NA')) + '">' +
                      '<div class="g-find__n">' + esc(e.verdict) + (e.inForce ? ' · in force' : '') + '</div>' +
                      '<div class="g-find__d">' + esc(e.reason || '(no note)') +
                      '<div class="g-sub">' + esc(e.actor) + (e.role ? ' (' + esc(e.role) + ')' : '') +
                      ' · ' + esc(e.at) + (e.year && e.year !== '-' ? ' · ' + esc(e.year) : '') +
                      '</div></div></div>';
            }
            h += fold('history', 'Decision history', d.history.length, hi, true);
        }

        qs('gModalBody').innerHTML = h;
        wireFolds();
    }

    function fact(label, value, sub, rawSub) {
        return '<div class="g-fact"><span>' + esc(label) + '</span><b>' + esc(value) + '</b>' +
               (sub ? '<small>' + (rawSub ? sub : esc(sub)) + '</small>' : '') + '</div>';
    }

    /// A collapsed section. Open it and it stays open for the rest of the session, because a
    /// reviewer who wants to see results for one student usually wants them for the next.
    function fold(key, title, count, body, openByDefault, cls) {
        var open = foldState(key, openByDefault);
        return '<div class="g-fold' + (open ? ' is-open' : '') + (cls ? ' ' + cls : '') +
               '" data-fold="' + key + '">' +
               '<button type="button" class="g-fold__h">' +
                 '<svg viewBox="0 0 24 24" width="11" height="11" fill="none" stroke="currentColor" ' +
                 'stroke-width="3"><polyline points="9 18 15 12 9 6"></polyline></svg>' +
                 '<span>' + esc(title) + '</span><em>' + esc(String(count)) + '</em>' +
               '</button>' +
               '<div class="g-fold__b">' + body + '</div></div>';
    }

    var folds = {};
    /// A remembered fold beats its default, in BOTH directions. The old form read the stored
    /// value, then forced any default-open section back open whenever it was stored closed —
    /// so a section you deliberately collapsed reopened on the next student, every time.
    function foldState(key, def) {
        if (folds[key] === undefined) {
            var v = null;
            try { v = sessionStorage.getItem('gfold_' + key); } catch (e) { }
            folds[key] = (v === null || v === undefined) ? !!def : (v === '1');
        }
        return folds[key];
    }
    function wireFolds() {
        var els = qs('gModalBody').querySelectorAll('.g-fold__h');
        for (var i = 0; i < els.length; i++) {
            els[i].addEventListener('click', function () {
                var box = this.parentNode, key = box.getAttribute('data-fold');
                var now = !box.classList.contains('is-open');
                box.classList.toggle('is-open', now);
                folds[key] = now;
                try { sessionStorage.setItem('gfold_' + key, now ? '1' : '0'); } catch (e) { }
            });
        }
    }

    /* ── Small shared helpers the four pages kept reinventing ────────── */

    /// Waits for typing to stop. Search used to need Enter, which meant a filter that looked
    /// applied but was not until you remembered to press a key.
    function debounce(fn, ms) {
        var t = null;
        return function () {
            var self = this, a = arguments;
            clearTimeout(t);
            t = setTimeout(function () { fn.apply(self, a); }, ms || 350);
        };
    }

    /// How old the counts are. Every page ships window.G_AGE and, until now, no page showed it:
    /// the Centre's Refresh button rebuilt a summary whose staleness was invisible.
    function freshness(id) {
        var el = qs(id || 'gAge');
        if (!el) return;
        var a = window.G_AGE || '';
        el.textContent = a ? ('counts ' + a) : '';
        el.title = a ? 'Candidate counts read a stored summary, rebuilt ' + a +
                       '. Every graduation decision is re-checked against live marks at the moment it is taken.'
                     : '';
    }

    /* ── The active-filter strip ──────────────────────────────────────
       With eight controls in the toolbar it is easy to leave one set and then not understand why
       a screen is empty. Each narrowing filter shows as a chip that says what it is, and clicking
       it removes that one filter. "No candidates match" stops being mystifying. */
    function chips(id, items, onRemove) {
        var el = qs(id);
        if (!el) return;
        var live = [];
        for (var i = 0; i < items.length; i++)
            if (items[i] && items[i].value !== '' && items[i].value != null) live.push(items[i]);

        if (!live.length) { el.innerHTML = ''; el.style.display = 'none'; return; }
        el.style.display = 'flex';
        var h = '<span class="g-chips__l">Filtered by</span>';
        for (i = 0; i < live.length; i++)
            h += '<button type="button" class="g-fchip" data-k="' + esc(live[i].k) + '" ' +
                 'title="' + esc(live[i].label + ': ' + live[i].value) + ' — click to remove">' +
                 '<span class="g-fchip__k">' + esc(live[i].label) + '</span>' +
                 '<span class="g-fchip__v">' + esc(live[i].value) + '</span>' +
                 '<em>&times;</em></button>';
        h += '<button type="button" class="g-fchip g-fchip--all" data-k="*">Clear all</button>';
        el.innerHTML = h;

        var b = el.querySelectorAll('.g-fchip');
        for (i = 0; i < b.length; i++)
            b[i].addEventListener('click', function () { onRemove(this.getAttribute('data-k')); });
    }

    /* ── The export dialog ────────────────────────────────────────────
       Export used to be a button that guessed: it took the current filter, chose the columns for
       you, and produced a file. Worse, it silently produced fifty rows of it. Now it is a short
       conversation - what is included, how many rows that really is, which columns, which extra
       sheets, and in what format - and the footer says exactly what is about to happen before
       anything is downloaded. */
    var XKEY = null, XOPT = null;

    function xLoad(page) {
        try { return JSON.parse(sessionStorage.getItem('gexp_' + page) || '{}') || {}; }
        catch (e) { return {}; }
    }
    function xSave(page, o) {
        try { sessionStorage.setItem('gexp_' + page, JSON.stringify(o)); } catch (e) { }
    }

    function xMount() {
        if (qs('gXov')) return;
        var d = document.createElement('div');
        d.innerHTML =
            '<div class="g-ov" id="gXov">' +
              '<div class="g-modal g-modal--x" role="dialog" aria-modal="true" aria-labelledby="gXtitle">' +
                '<div class="g-modal__h">' +
                  '<div class="g-modal__t"><b id="gXtitle">Export</b>' +
                    '<span id="gXsub">Choose what goes into the file.</span></div>' +
                  '<button type="button" class="g-modal__x" id="gXx" aria-label="Close">&times;</button>' +
                '</div>' +
                '<div class="g-modal__b" id="gXbody"></div>' +
                '<div class="g-modal__f">' +
                  '<span class="g-hint" id="gXecho">&nbsp;</span>' +
                  '<button type="button" class="g-btn" id="gXcancel">Cancel</button>' +
                  '<button type="button" class="g-btn g-btn--p" id="gXgo">Export</button>' +
                '</div>' +
              '</div>' +
            '</div>';
        while (d.firstChild) document.body.appendChild(d.firstChild);

        var ov = qs('gXov');
        ov.addEventListener('mousedown', function (e) { if (e.target === ov) xClose(); });
        qs('gXx').addEventListener('click', xClose);
        qs('gXcancel').addEventListener('click', xClose);
        qs('gXgo').addEventListener('click', xRun);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && ov.classList.contains('is-open')) xClose();
        });
    }

    function xClose() {
        var ov = qs('gXov');
        if (!ov) return;
        ov.classList.remove('is-open');
        if (!qs('gOv') || !qs('gOv').classList.contains('is-open')) document.body.style.overflow = '';
    }

    /// The one call a page makes. Everything else in here is this dialog's own business.
    function exportDialog(opts) {
        xMount();
        XOPT = opts || {};
        XKEY = XOPT.page || 'x';
        var saved = xLoad(XKEY);

        qs('gXtitle').textContent = XOPT.title || 'Export';
        qs('gXsub').textContent = XOPT.subtitle || 'Choose what goes into the file.';

        var cols = XOPT.columns || [];
        var chosen = saved.cols && saved.cols.length ? saved.cols : null;
        var i, c;

        // ── what's included ──
        var h = '<div class="g-xs"><div class="g-xs__h">What\u2019s included</div>' +
                '<div class="g-xkv">';
        var fs = XOPT.filterSummary || [];
        if (!fs.length) h += '<div><span>Scope</span><b>Everything you can see</b></div>';
        for (i = 0; i < fs.length; i++)
            h += '<div><span>' + esc(fs[i].label) + '</span><b>' + esc(fs[i].value) + '</b></div>';
        h += '</div><div class="g-xcount" id="gXcount">Counting rows\u2026</div></div>';

        // ── rows ──
        var rowMode = saved.rows || 'all';
        h += '<div class="g-xs"><div class="g-xs__h">Rows</div><div class="g-xr">' +
             xRadio('gxr', 'all', 'Everything that matches these filters', rowMode === 'all') +
             xRadio('gxr', 'page', 'Only the ' + (XOPT.pageRows || 0) + ' on screen now', rowMode === 'page') +
             '</div></div>';

        // ── columns, grouped ──
        if (cols.length) {
            var groups = [], seen = {};
            for (i = 0; i < cols.length; i++) {
                if (!seen[cols[i].g]) { seen[cols[i].g] = []; groups.push(cols[i].g); }
                seen[cols[i].g].push(cols[i]);
            }
            h += '<div class="g-xs"><div class="g-xs__h">Columns' +
                 '<button type="button" class="g-xlink" id="gXall">all</button>' +
                 '<button type="button" class="g-xlink" id="gXnone">none</button>' +
                 '<button type="button" class="g-xlink" id="gXdef">default</button></div>';
            for (i = 0; i < groups.length; i++) {
                h += '<div class="g-xg"><div class="g-xg__h">' + esc(groups[i]) + '</div><div class="g-xg__b">';
                var g = seen[groups[i]];
                for (var j = 0; j < g.length; j++) {
                    c = g[j];
                    var on = chosen ? (chosen.indexOf(c.k) >= 0) : !!c.on;
                    h += '<label class="g-ck"><input type="checkbox" class="gxc" value="' + esc(c.k) + '"' +
                         (on ? ' checked' : '') + ' data-def="' + (c.on ? 1 : 0) + '" /><span>' +
                         esc(c.t) + '</span></label>';
                }
                h += '</div></div>';
            }
            h += '</div>';
        }

        // ── extra sheets ──
        var sheets = XOPT.sheets || [];
        if (sheets.length) {
            var ss = saved.sheets && saved.sheets.length ? saved.sheets : null;
            h += '<div class="g-xs"><div class="g-xs__h">Extra sheets</div><div class="g-xg__b">';
            for (i = 0; i < sheets.length; i++) {
                var son = ss ? (ss.indexOf(sheets[i].k) >= 0) : !!sheets[i].on;
                h += '<label class="g-ck"><input type="checkbox" class="gxs" value="' + esc(sheets[i].k) + '"' +
                     (son ? ' checked' : '') + ' /><span>' + esc(sheets[i].t) +
                     (sheets[i].d ? '<small>' + esc(sheets[i].d) + '</small>' : '') + '</span></label>';
            }
            h += '</div></div>';
        }

        // ── format ──
        var fmt = saved.fmt || 'xls';
        h += '<div class="g-xs"><div class="g-xs__h">Format</div><div class="g-xr">' +
             xRadio('gxf', 'xls', 'Excel workbook \u2014 branded, cover sheet, frozen headings', fmt === 'xls') +
             xRadio('gxf', 'csv', 'CSV \u2014 one flat sheet, for loading elsewhere', fmt === 'csv') +
             '</div></div>';

        qs('gXbody').innerHTML = h;

        // CSV cannot carry extra tabs. Saying so, and disabling them, beats producing a file
        // that quietly lost half of what was asked for.
        function fmtChanged() {
            var csv = xVal('gxf') === 'csv';
            var b = document.querySelectorAll('.gxs');
            for (var k = 0; k < b.length; k++) {
                b[k].disabled = csv;
                b[k].parentNode.classList.toggle('is-off', csv);
            }
            var note = qs('gXcsvnote');
            if (note) note.style.display = csv ? 'block' : 'none';
            echo();
        }
        if (sheets.length) {
            var sec = qs('gXbody').querySelectorAll('.g-xs');
            var host = sec[sec.length - 2];
            var n = document.createElement('div');
            n.className = 'g-xnote'; n.id = 'gXcsvnote'; n.style.display = 'none';
            n.textContent = 'A CSV is a single sheet, so these are not included in that format.';
            host.appendChild(n);
        }

        var boxes = qs('gXbody').querySelectorAll('.gxc, .gxs');
        for (i = 0; i < boxes.length; i++) boxes[i].addEventListener('change', echo);
        var radios = qs('gXbody').querySelectorAll('input[name="gxf"]');
        for (i = 0; i < radios.length; i++) radios[i].addEventListener('change', fmtChanged);
        radios = qs('gXbody').querySelectorAll('input[name="gxr"]');
        for (i = 0; i < radios.length; i++) radios[i].addEventListener('change', echo);

        if (qs('gXall')) qs('gXall').addEventListener('click', function () { xSet(1); });
        if (qs('gXnone')) qs('gXnone').addEventListener('click', function () { xSet(0); });
        if (qs('gXdef')) qs('gXdef').addEventListener('click', function () { xSet(-1); });

        fmtChanged();
        qs('gXov').classList.add('is-open');
        document.body.style.overflow = 'hidden';

        // The honest row count, from the server, before the dialog can be used in anger.
        var cnt = qs('gXcount');
        if (XOPT.countMethod) {
            ajax(XOPT.page, XOPT.countMethod, { configJson: XOPT.cfg }, function (d) {
                if (!d || !d.success) { cnt.textContent = 'Could not count the rows; the export will still run.'; echo(); return; }
                XOPT.total = d.total;
                cnt.innerHTML = '<b>' + d.total + '</b> row' + (d.total === 1 ? '' : 's') + ' match' +
                    (d.total === 1 ? 'es' : '') + ' these filters.' +
                    (d.note ? ' <span class="g-xwarn">' + esc(d.note) + '</span>' : '') +
                    (d.capped ? ' <span class="g-xwarn">The file stops at ' + d.capped + '.</span>' : '');
                echo();
            });
        } else {
            XOPT.total = XOPT.pageRows || 0;
            cnt.textContent = '';
            echo();
        }

        function xSet(mode) {
            var b = document.querySelectorAll('.gxc');
            for (var k = 0; k < b.length; k++)
                b[k].checked = mode === 1 ? true : mode === 0 ? false : b[k].getAttribute('data-def') === '1';
            echo();
        }
    }

    function xRadio(name, val, label, on) {
        return '<label class="g-ck"><input type="radio" name="' + name + '" value="' + val + '"' +
               (on ? ' checked' : '') + ' /><span>' + esc(label) + '</span></label>';
    }

    function xVal(name) {
        var b = document.querySelectorAll('input[name="' + name + '"]');
        for (var i = 0; i < b.length; i++) if (b[i].checked) return b[i].value;
        return '';
    }

    function xPicked(cls) {
        var out = [], b = document.querySelectorAll('.' + cls);
        for (var i = 0; i < b.length; i++) if (b[i].checked && !b[i].disabled) out.push(b[i].value);
        return out;
    }

    /// The footer restates the decision in one line. Nobody should have to press Export to find
    /// out how big the file is or what is in it.
    function echo() {
        var e = qs('gXecho');
        if (!e) return;
        var cols = xPicked('gxc').length, fmt = xVal('gxf'), mode = xVal('gxr');
        var n = mode === 'page' ? (XOPT.pageRows || 0)
                                : (XOPT.total === undefined ? null : XOPT.total);
        var parts = [];
        parts.push(n === null ? 'rows: counting…' : (n + ' row' + (n === 1 ? '' : 's')));
        if (XOPT.columns && XOPT.columns.length) parts.push(cols + ' column' + (cols === 1 ? '' : 's'));
        var sh = xPicked('gxs').length;
        if (sh && fmt !== 'csv') parts.push(sh + ' extra sheet' + (sh === 1 ? '' : 's'));
        parts.push(fmt === 'csv' ? 'CSV' : 'Excel workbook');
        e.textContent = parts.join('  ·  ');

        var go = qs('gXgo');
        if (go) {
            var none = (XOPT.columns && XOPT.columns.length && cols === 0);
            go.disabled = none || n === 0;
            go.title = none ? 'Pick at least one column.' : (n === 0 ? 'Nothing matches these filters.' : '');
        }
    }

    function xRun() {
        if (!XOPT) return;
        var cols = xPicked('gxc'), sheets = xPicked('gxs'),
            fmt = xVal('gxf'), mode = xVal('gxr');
        xSave(XKEY, { cols: cols, sheets: sheets, fmt: fmt, rows: mode });

        var extra = {
            gradCols: cols.join(','),
            gradSheets: sheets.join(','),
            gradRows: mode,
            gradPageSize: String(XOPT.pageRows || 0)
        };
        post(XOPT.page, fmt, XOPT.cfg, extra);
        xClose();
        toast('Building the file\u2026 it will download when it is ready.', true);
    }

    /* ── CSV, for the small exports the browser can do honestly. Anything that
         needs a cover sheet and branding is built on the server instead. ── */
    function csv(rows, filename) {
        var lines = rows.map(function (r) {
            return r.map(function (v) {
                v = String(v == null ? '' : v);
                return /[",\n\r]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v;
            }).join(',');
        });
        var blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(a.href); }, 2000);
    }

    /* A server-built export: post the current filter to a page's export handler and let the
       browser save what comes back. A form post rather than fetch, so the download lands in
       the normal place with the filename the server chose. */
    function post(page, format, cfgJson, extra) {
        var f = document.createElement('form');
        f.method = 'post';
        f.action = page;
        f.style.display = 'none';
        function add(n, v) {
            var i = document.createElement('input');
            i.type = 'hidden'; i.name = n; i.value = v;
            f.appendChild(i);
        }
        add('gradExport', format);
        add('gradConfig', cfgJson);
        if (extra) for (var k in extra) if (extra.hasOwnProperty(k)) add(k, extra[k]);
        document.body.appendChild(f);
        f.submit();
        setTimeout(function () { document.body.removeChild(f); }, 1500);
    }

    function serverExport(page, format, cfgJson) { post(page, format, cfgJson, null); }

    return {
        qs: qs, esc: esc, n0: n0, n1: n1, n2: n2,
        ajax: ajax, toast: toast,
        photo: photo, photoCell: photoCell, chip: chip, credits: credits,
        readUrl: readUrl, writeUrl: writeUrl,
        fill: fill, cascade: cascade,
        mount: mount, openModal: openModal, closeModal: closeModal, wireModal: wireModal,
        openStudent: openStudent, currentStudent: currentStudent,
        csv: csv, serverExport: serverExport, exportDialog: exportDialog,
        debounce: debounce, freshness: freshness, chips: chips
    };
})();
