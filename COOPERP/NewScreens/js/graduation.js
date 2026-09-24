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

    function openStudent(page, regno, buildFooter) {
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
        });
    }

    function currentStudent() { return current; }

    function cell(label, val) {
        return '<div><span>' + esc(label) + '</span><b>' + esc(val) + '</b></div>';
    }

    function renderStudent(d) {
        var g = d.student, h = '', i;

        qs('gModalName').textContent = g.name + '  ·  ' + g.regno;
        qs('gModalSub').textContent = (g.progname || g.progcode) + '  ·  intake ' + (g.entryyear || '–') +
            '  ·  ' + (g.firstYear ? g.firstYear + ' to ' + g.lastYear : 'no results on record');

        if (g.graduatedYear)
            h += '<div class="g-note g-note--info"><b>Already on the ' + esc(g.graduatedYear) +
                 ' graduation list.</b>' + (g.clearedActor ? ' Cleared by ' + esc(g.clearedActor) +
                 (g.clearedAt ? ' on ' + esc(g.clearedAt) : '') + '.' : '') + '</div>';
        if (g.holdReason)
            h += '<div class="g-note g-note--warn"><b>Held by ' + esc(g.holdActor) + ' on ' +
                 esc(g.holdAt) + '.</b><br>' + esc(g.holdReason) + '</div>';

        h += '<div class="g-grid">' +
            cell('Credits earned', n0(g.cuEarned) + ' CU') +
            cell('Credits required', g.cuSource === 'NONE' ? 'Not assessable' : (n0(g.cuRequired) + ' CU')) +
            cell('CGPA', g.cgpa ? n2(g.cgpa) : '–') +
            cell('Class of award', g.degClass || '–') +
            cell('Courses on record', String(g.coursesTaken)) +
            cell('Reached year', g.maxStudyYear + ' of ' + g.progLength) +
            '</div>';

        h += '<div class="g-note g-note--info" style="font-size:10.5px;"><b>Where the credit bar comes from:</b> ' +
             esc(g.cuSourceLabel || 'not established') + '.' +
             (g.specIsPlaceholder ? ' This student carries no real specialisation — the value on their record is a ' +
              'placeholder — so their courses cannot be matched to a curriculum.' : '') + '</div>';

        h += '<div class="g-sec">What the checks say</div>';
        for (i = 0; i < (g.findings || []).length; i++) {
            var f = g.findings[i];
            h += '<div class="g-find g-find--' + esc(f.level) + '">' +
                 '<div class="g-find__n">' + esc(f.name) + '</div>' +
                 '<div class="g-find__d">' + esc(f.detail) + '</div></div>';
        }

        if (d.structure && d.structure.length) {
            h += '<div class="g-sec">Against the programme structure</div>' +
                 '<div class="g-wrap" style="max-height:220px;"><table class="g-tbl g-struct"><thead><tr>' +
                 '<th>Yr</th><th>Sem</th><th>Course</th><th class="g-num">CU</th><th>Result</th></tr></thead><tbody>';
            for (i = 0; i < d.structure.length; i++) {
                var c = d.structure[i];
                var cls = c.score === null ? 'miss' : (c.score > 0 && c.score < 50 ? 'fail' : '');
                var txt = c.score === null ? 'no result' : (c.score + '%');
                h += '<tr><td>' + esc(c.sy) + '</td><td>' + esc(c.sem) + '</td>' +
                     '<td>' + esc(c.code) + '<div class="g-sub">' + esc(c.name) + '</div></td>' +
                     '<td class="g-num">' + n0(c.cu) + '</td><td class="' + cls + '">' + esc(txt) + '</td></tr>';
            }
            h += '</tbody></table></div>';
        }

        h += '<div class="g-sec">Results on record (' + (d.results || []).length + ')</div>' +
             '<div class="g-wrap" style="max-height:240px;"><table class="g-tbl"><thead><tr>' +
             '<th>Year</th><th>Yr/Sem</th><th>Course</th><th class="g-num">CU</th>' +
             '<th class="g-num">Score</th><th>Grade</th></tr></thead><tbody>';
        for (i = 0; i < (d.results || []).length; i++) {
            var x = d.results[i];
            var bad = (x.score !== null && x.score > 0 && x.score < 50), zero = (x.score === 0);
            h += '<tr><td class="g-sub">' + esc(x.acad) + '</td><td class="g-sub">' + esc(x.sy) + '/' + esc(x.sem) + '</td>' +
                 '<td>' + esc(x.code) + '<div class="g-sub">' + esc(x.name) + '</div></td>' +
                 '<td class="g-num">' + n0(x.cu) + '</td>' +
                 '<td class="g-num"' + (bad ? ' style="color:#8c2019;font-weight:700"'
                                            : (zero ? ' style="color:#92400e;font-weight:700"' : '')) + '>' +
                 (x.score === null ? '–' : x.score) + '</td><td>' + esc(x.grade) + '</td></tr>';
        }
        h += '</tbody></table></div>';

        if (d.history && d.history.length) {
            h += '<div class="g-sec">Decision history</div>';
            for (i = 0; i < d.history.length; i++) {
                var e = d.history[i];
                h += '<div class="g-find g-find--' +
                     (e.verdict === 'HELD' ? 'WARN' : (e.verdict === 'CLEARED' ? 'PASS' : 'NA')) + '">' +
                     '<div class="g-find__n">' + esc(e.verdict) + (e.inForce ? ' · in force' : '') + '</div>' +
                     '<div class="g-find__d">' + esc(e.reason || '(no note)') +
                     '<div class="g-sub">' + esc(e.actor) + (e.role ? ' (' + esc(e.role) + ')' : '') +
                     ' · ' + esc(e.at) + (e.year && e.year !== '-' ? ' · ' + esc(e.year) : '') +
                     '</div></div></div>';
            }
        }

        qs('gModalBody').innerHTML = h;
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
    function serverExport(page, format, cfgJson) {
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
        document.body.appendChild(f);
        f.submit();
        setTimeout(function () { document.body.removeChild(f); }, 1500);
    }

    return {
        qs: qs, esc: esc, n0: n0, n1: n1, n2: n2,
        ajax: ajax, toast: toast,
        photo: photo, photoCell: photoCell, chip: chip, credits: credits,
        readUrl: readUrl, writeUrl: writeUrl,
        fill: fill, cascade: cascade,
        mount: mount, openModal: openModal, closeModal: closeModal, wireModal: wireModal,
        openStudent: openStudent, currentStudent: currentStudent,
        csv: csv, serverExport: serverExport
    };
})();
