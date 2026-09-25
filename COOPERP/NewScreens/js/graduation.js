/* =====================================================================
   Graduation module, shared behaviour for the four pages.

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
            cb({ success: false, message: 'That took too long, narrow the filters and try again.' });
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

    /* A cached thumbnail, not the 469KB original. See StudentThumb.ashx, which renders only
       72, 144 and 480: 72 for a row, 144 so the modal's avatar stays sharp on a high-density
       screen, 480 for the full view. */
    function photo(regno, size) {
        return 'StudentThumb.ashx?r=' + encodeURIComponent(regno || '') +
               (size ? '&s=' + size : '');
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
        // A combo sitting over this select shows the option TEXT, which has just been replaced.
        if (el.getAttribute('data-combo')) el.dispatchEvent(new Event('g-refill'));
    }

    /* ── A list you can type into ──────────────────────────────────────
       There are 130 programmes. A native <select> makes you hunt through all of them with the
       keyboard's first-letter jump, which is why the programme filter has always been the
       slowest control on these screens.

       This is a text box over a filtered list. It matches on ANY word boundary, not just the
       start of the string, because people type "information" for "Bachelor of Information
       Technology" and "BIT" for the same thing; both have to work. Matching is
       accent-and-case-insensitive and ignores punctuation, so "b.ed" finds "BED".

       It renders over a real <select>, which stays in the DOM and keeps its value. Everything
       that reads the filter keeps reading the select, so nothing else on the page has to know
       this exists. */
    function norm(t) {
        return String(t == null ? '' : t).toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
    }

    /// True when every word typed appears at the start of some word in the option.
    function matches(hay, words) {
        for (var i = 0; i < words.length; i++) {
            var w = words[i], ok = false;
            var parts = hay.split(' ');
            for (var j = 0; j < parts.length; j++)
                if (parts[j].indexOf(w) === 0) { ok = true; break; }
            // A long token still matches inside a word, so "formation" finds "Information".
            if (!ok && w.length >= 4 && hay.indexOf(w) >= 0) ok = true;
            if (!ok) return false;
        }
        return true;
    }

    function combo(selectId, placeholder) {
        var sel = qs(selectId);
        if (!sel || sel.getAttribute('data-combo')) return;
        sel.setAttribute('data-combo', '1');

        var box = document.createElement('div');
        box.className = 'g-cb';
        var input = document.createElement('input');
        input.type = 'text';
        input.className = 'g-cb__in';
        input.autocomplete = 'off';
        input.placeholder = placeholder || 'Type to search…';
        var list = document.createElement('div');
        list.className = 'g-cb__l';
        box.appendChild(input);
        box.appendChild(list);
        sel.parentNode.insertBefore(box, sel);
        sel.classList.add('g-cb__hidden');

        var open = false, active = -1, shown = [];

        function label() {
            var o = sel.options[sel.selectedIndex];
            return o ? o.text.replace(/\s+/g, ' ').trim() : '';
        }
        function sync() { input.value = label(); input.classList.toggle('is-set', !!sel.value); }

        function draw(q) {
            var words = norm(q).split(' ').filter(function (w) { return w !== ''; });
            shown = [];
            var h = '', i;
            for (i = 0; i < sel.options.length; i++) {
                var o = sel.options[i];
                if (o.hidden) continue;                       // respects the cascade
                if (words.length && !matches(norm(o.text + ' ' + o.value), words)) continue;
                shown.push(i);
            }
            if (!shown.length) {
                list.innerHTML = '<div class="g-cb__none">Nothing matches “' + esc(q) + '”.</div>';
                return;
            }
            for (i = 0; i < shown.length; i++) {
                var op = sel.options[shown[i]];
                h += '<button type="button" class="g-cb__o' +
                     (i === active ? ' is-active' : '') +
                     (op.index === sel.selectedIndex ? ' is-sel' : '') +
                     '" data-i="' + shown[i] + '">' + esc(op.text) + '</button>';
            }
            list.innerHTML = h;
            var b = list.querySelectorAll('.g-cb__o');
            for (i = 0; i < b.length; i++)
                b[i].addEventListener('mousedown', function (e) {
                    e.preventDefault();
                    pick(+this.getAttribute('data-i'));
                });
        }

        function show() { open = true; box.classList.add('is-open'); active = -1; draw(''); input.select(); }
        function hide() { open = false; box.classList.remove('is-open'); sync(); }

        function pick(i) {
            sel.selectedIndex = i;
            hide();
            // A real change event, so every cascade and reload already listening still fires.
            sel.dispatchEvent(new Event('change', { bubbles: true }));
        }

        input.addEventListener('focus', show);
        input.addEventListener('input', function () { active = -1; draw(input.value); });
        input.addEventListener('blur', function () { setTimeout(hide, 120); });
        input.addEventListener('keydown', function (e) {
            if (!open && (e.key === 'ArrowDown' || e.key === 'Enter')) { show(); return; }
            if (e.key === 'ArrowDown') { active = Math.min(active + 1, shown.length - 1); draw(input.value); scroll(); e.preventDefault(); }
            else if (e.key === 'ArrowUp') { active = Math.max(active - 1, 0); draw(input.value); scroll(); e.preventDefault(); }
            else if (e.key === 'Enter') { if (shown.length) pick(shown[active < 0 ? 0 : active]); e.preventDefault(); }
            else if (e.key === 'Escape') { hide(); input.blur(); }
        });

        function scroll() {
            var el = list.querySelector('.is-active');
            if (el && el.scrollIntoView) el.scrollIntoView({ block: 'nearest' });
        }

        // The cascade rewrites the option list; the box has to follow it.
        sel.addEventListener('change', sync);
        sel.addEventListener('g-refill', sync);
        sync();
        return { sync: sync };
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
        // The cascade can clear a selection; anything drawn over these selects must follow.
        if (dep && dep.getAttribute('data-combo')) dep.dispatchEvent(new Event('g-refill'));
        if (prog && prog.getAttribute('data-combo')) prog.dispatchEvent(new Event('g-refill'));
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
                  '<button type="button" class="g-phbtn" id="gModalPhotoBtn" ' +
                    'title="See the photograph full size">' +
                    '<img class="g-ph" id="gModalPhoto" alt="" src="" />' +
                    '<svg viewBox="0 0 24 24" width="9" height="9" fill="none" stroke="currentColor" ' +
                    'stroke-width="3" stroke-linecap="round"><path d="M15 3h6v6M9 21H3v-6M21 3l-7 7' +
                    'M3 21l7-7"/></svg>' +
                  '</button>' +
                  '<div class="g-modal__t"><b id="gModalName">&nbsp;</b><span id="gModalSub">&nbsp;</span></div>' +
                  '<button type="button" class="g-modal__x" id="gModalX" aria-label="Close">&times;</button>' +
                '</div>' +
                '<div class="g-modal__b" id="gModalBody"></div>' +
                '<div class="g-modal__f" id="gModalFoot"></div>' +
              '</div>' +
            '</div>' +
            '<div class="g-toast" id="gToast"></div>' +
            // The full-size view. Its own overlay, above the evidence modal, so opening it does
            // not disturb what the reviewer was reading.
            '<div class="g-lb" id="gLb">' +
              '<figure class="g-lb__f">' +
                '<img id="gLbImg" alt="" src="" />' +
                '<figcaption id="gLbCap"></figcaption>' +
              '</figure>' +
              '<button type="button" class="g-lb__x" id="gLbX" aria-label="Close">&times;</button>' +
            '</div>';
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

    /* A student photograph is how a reviewer confirms they have the right person, and a 48px
       avatar is not enough for that on its own. Opening it costs no room on the panel: the
       photograph is already loaded small, and the large one is fetched only when asked for. */
    function openPhoto(regno, caption) {
        var lb = qs('gLb');
        if (!lb) return;
        qs('gLbImg').src = photo(regno, 480);
        qs('gLbCap').textContent = caption || regno;
        lb.classList.add('is-open');
    }

    function closePhoto() {
        var lb = qs('gLb');
        if (lb) lb.classList.remove('is-open');
    }

    function wirePhoto() {
        var lb = qs('gLb');
        if (!lb || lb.getAttribute('data-wired')) return;
        lb.setAttribute('data-wired', '1');
        lb.addEventListener('click', closePhoto);
        document.addEventListener('keydown', function (e) {
            // Escape closes the photograph first, and only then the modal underneath it.
            if (e.key === 'Escape' && lb.classList.contains('is-open')) {
                e.stopPropagation();
                closePhoto();
            }
        }, true);
        var b = qs('gModalPhotoBtn');
        if (b) b.addEventListener('click', function () {
            var c = currentStudent();
            if (!c) return;
            openPhoto(c.student.regno, c.student.name + '  \u00b7  ' + c.student.regno);
        });
    }

    function wireModal(afterClose) {
        wirePhoto();
        onClose = afterClose || null;
        var ov = qs('gOv');
        if (!ov) return;
        // Only a click on the backdrop itself closes it, not a click that happened to
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
    /// with setTimeout(fn, 0) wires nothing, the timer fires long before the response lands.
    /// onReady is called after the footer exists, which is the only moment the buttons are
    /// there to be wired.
    function openStudent(page, regno, buildFooter, onReady) {
        current = null;
        // Where this candidate sits in the queue, so the walker in the header means something.
        for (var qi = 0; qi < Q.ids.length; qi++) if (Q.ids[qi] === regno) { Q.idx = qi; break; }
        qs('gModalName').textContent = regno;
        qs('gModalSub').textContent = 'Reading the record…';
        showPhoto(true);
        qs('gModalPhoto').src = photo(regno, 144);
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
            drawWalker();
            qs('gModalFoot').innerHTML = buildFooter ? buildFooter(d.student) : '';
            if (onReady) onReady(d.student);
        });
    }

    function currentStudent() { return current; }

    /// The avatar is a button wrapping an image, so a caller that opens the shared modal for
    /// something other than a student has to hide the whole control, not just the picture.
    function showPhoto(on) {
        var b = qs('gModalPhotoBtn');
        if (b) b.style.display = on ? '' : 'none';
    }

    /// Position in the queue, and arrows to walk it without deciding anything.
    function drawWalker() {
        var host = qs('gModalSub');
        if (!host) return;
        var old = qs('gWalk');
        if (old && old.parentNode) old.parentNode.removeChild(old);
        if (Q.idx < 0 || Q.ids.length < 2) return;

        var w = document.createElement('div');
        w.className = 'g-walk';
        w.id = 'gWalk';
        w.innerHTML =
            '<button type="button" class="g-walk__b" id="gWprev" title="Previous candidate"' +
            (prevIn(Q.idx) < 0 ? ' disabled' : '') + '>&lsaquo;</button>' +
            '<span class="g-walk__t">' + esc(queuePos()) + '</span>' +
            '<button type="button" class="g-walk__b" id="gWnext" title="Next candidate"' +
            (nextIn(Q.idx) < 0 && Q.page >= Q.pages ? ' disabled' : '') + '>&rsaquo;</button>';
        host.parentNode.appendChild(w);

        if (qs('gWprev')) qs('gWprev').addEventListener('click', function () {
            var i = prevIn(Q.idx);
            if (i >= 0 && walkTo) walkTo(Q.ids[i]);
        });
        if (qs('gWnext')) qs('gWnext').addEventListener('click', function () {
            var i = nextIn(Q.idx);
            if (i >= 0 && walkTo) walkTo(Q.ids[i]);
            else if (Q.page < Q.pages && Q.nextPage) Q.nextPage(Q.page + 1);
        });
    }

    /// The page supplies how to open a candidate, since only it knows its own footer.
    var walkTo = null;
    function onWalk(fn) { walkTo = fn; }

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
            // Code and name on ONE line. Two lines per course doubled the height of every
            // table for a title most readers skim; the code is what they look for.
            h += '<tr><td class="g-sem__course" title="' + esc(x.code + ' - ' + x.name) + '">' +
                 '<b>' + esc(x.code) + '</b><i>&ndash;</i>' + esc(x.name) + '</td>' +
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

        // A year of study whose semesters sit in different academic years is real and common,
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
        //  is how a degree is read, not as one long list sorted by academic year.
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
        //  What is blocking them stays on the face of it, that is the decision. What merely
        //  wants a look is folded away, because on this data most candidates carry several
        //  warnings and an open list of them buries the two lines that decide the case.
        var kind = blocks.length ? 'bad' : (warns.length ? 'warn' : 'ok');
        var head = blocks.length
            ? (blocks.length === 1 ? 'One thing is blocking this candidate' : blocks.length + ' things are blocking this candidate')
            : (warns.length
                ? (warns.length === 1 ? 'Ready, with one thing to look at' : 'Ready, with ' + warns.length + ' things to look at')
                : 'Ready, nothing outstanding');

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

        // ── the way out of a problem, not just a description of it ──────────
        //  Most of what blocks a candidate is a records fault: a mark in the wrong semester, a
        //  course registered twice, an unmarked paper. The reviewer who finds it is the one who
        //  should be able to act on it, so the link carries the student and a reason composed
        //  from this very screen, and the rearrangement session opens already started.
        if (d.canRearrange) {
            var why = 'Opened from the Graduation Centre while reviewing ' + g.regno +
                      (g.progcode ? ' (' + g.progcode + ')' : '') + ' for graduation. ' +
                      (blocks.length
                         ? 'Blocked: ' + blocks[0].detail
                         : (warns.length ? 'Flagged: ' + warns[0].detail
                                         : 'Checking the record before clearing.'));
            h += '<a class="g-fix" target="_blank" rel="noopener" href="StudentRearrangeManage.aspx?auto=1' +
                 '&regno=' + encodeURIComponent(g.regno) +
                 '&reason=' + encodeURIComponent(why) + '">' +
                 '<svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" ' +
                 'stroke-width="2" stroke-linecap="round"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 ' +
                 '1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 ' +
                 '7.94-7.94l-3.76 3.76z"/></svg>' +
                 '<span><b>Fix this student\u2019s record</b>' +
                 'Opens a rearrangement session in a new tab, already started against ' + esc(g.regno) +
                 ' with the reason filled in.</span></a>';
        }

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
                      '<td class="g-sem__course" title="' + esc(c.code + ' - ' + c.name) + '">' +
                      '<b>' + esc(c.code) + '</b><i>&ndash;</i>' + esc(c.name) + '</td>' +
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
    /// value, then forced any default-open section back open whenever it was stored closed,
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
                 'title="' + esc(live[i].label + ': ' + live[i].value) + ', click to remove">' +
                 '<span class="g-fchip__k">' + esc(live[i].label) + '</span>' +
                 '<span class="g-fchip__v">' + esc(live[i].value) + '</span>' +
                 '<em>&times;</em></button>';
        h += '<button type="button" class="g-fchip g-fchip--all" data-k="*">Clear all</button>';
        el.innerHTML = h;

        var b = el.querySelectorAll('.g-fchip');
        for (i = 0; i < b.length; i++)
            b[i].addEventListener('click', function () { onRemove(this.getAttribute('data-k')); });
    }

    /* ── Pagination ───────────────────────────────────────────────────
       "Page 2 of 20" tells a reviewer nothing they can act on. Which records am I looking at,
       and how many are there altogether, that is the question a queue raises, so the range and
       the total lead, and the page number follows as a secondary fact.

       First and Last matter here because the queues are long: 996 candidates is 20 pages, and
       getting to the end by pressing Next is not a thing anyone should have to do. */
    function pager(id, o, onGo) {
        var el = qs(id);
        if (!el) return;

        var total = +o.total || 0,
            size  = +o.size || 50,
            page  = +o.page || 1,
            shown = o.shown === undefined ? size : +o.shown,
            pages = Math.max(1, Math.ceil(total / size));

        if (page > pages) page = pages;
        if (page < 1) page = 1;

        // Nothing to page and nothing to count: the bar itself goes, rather than sitting
        // under an empty table as a stray strip of border.
        if (total === 0) { el.innerHTML = ''; el.className = 'g-pager g-pager--none'; return; }

        var from = (page - 1) * size + 1;
        var to = from + shown - 1;
        if (to > total) to = total;
        if (shown === 0) { from = 0; to = 0; }

        var count = '<span class="g-pager__n">' +
            (total === shown && pages === 1
                ? ('<b>' + total + '</b> ' + (total === 1 ? 'record' : 'records'))
                : ('<b>' + from + '&ndash;' + to + '</b> of <b>' + total + '</b>')) +
            '</span>';

        var nav = '';
        if (pages > 1) {
            nav = '<span class="g-pager__b">' +
                  btn('pgFirst', '&laquo;', page <= 1, 'First page') +
                  btn('pgPrev', 'Previous', page <= 1, '') +
                  '<span class="g-pager__p">page <b>' + page + '</b> of <b>' + pages + '</b></span>' +
                  btn('pgNext', 'Next', page >= pages, '') +
                  btn('pgLast', '&raquo;', page >= pages, 'Last page') +
                  '</span>';
        }

        el.className = 'g-pager' + (pages > 1 ? '' : ' g-pager--one');
        el.innerHTML = count + nav;

        function go(n) { if (n >= 1 && n <= pages && n !== page && onGo) onGo(n); }
        if (qs('pgFirst')) qs('pgFirst').addEventListener('click', function () { go(1); });
        if (qs('pgPrev')) qs('pgPrev').addEventListener('click', function () { go(page - 1); });
        if (qs('pgNext')) qs('pgNext').addEventListener('click', function () { go(page + 1); });
        if (qs('pgLast')) qs('pgLast').addEventListener('click', function () { go(pages); });
    }

    function btn(id, label, off, title) {
        return '<button type="button" class="g-btn g-btn--sm" id="' + id + '"' +
               (off ? ' disabled' : '') + (title ? ' title="' + esc(title) + '"' : '') +
               '>' + label + '</button>';
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

        // ── what's included ────────────────────────────────────────────────
        //  These were a read-out of the screen's filter. They are now the filter: a reviewer
        //  who wants one programme's list should not have to close this, change the page, and
        //  open it again. They start where the screen is, and they cascade - narrowing the
        //  faculty narrows the departments and the programmes under it - so the combinations
        //  offered are always ones that can return rows.
        var h = '<div class="g-xs"><div class="g-xs__h">What\u2019s included' +
                '<button type="button" class="g-xlink" id="gXmatch" ' +
                'title="Put these back to what the screen is showing">match the screen</button></div>' +
                '<div class="g-xf">';

        if (XOPT.filters) {
            h += xField('gXyear', 'Graduation year') +
                 (XOPT.filters.focus ? xField('gXfocus', 'Population') : '') +
                 xField('gXfac', 'Faculty') +
                 xField('gXdep', 'Department') +
                 xField('gXprog', 'Programme');
        }
        h += '</div>';

        // Anything the dialog does not control still has to be visible, or the count is a
        // mystery: a search term or a readiness filter can be doing most of the narrowing.
        var fs = XOPT.filterSummary || [];
        if (fs.length) {
            // Labelled and separated, so it is obvious these narrow the file too but are not
            // editable here - sitting unlabelled among the dropdowns, they read as broken ones.
            h += '<div class="g-xalso"><span class="g-xalso__h">Also narrowing this export, ' +
                 'from the screen</span><div class="g-xkv">';
            for (i = 0; i < fs.length; i++)
                h += '<div><span>' + esc(fs[i].label) + '</span><b>' + esc(fs[i].value) + '</b></div>';
            h += '</div></div>';
        }
        h += '<div class="g-xcount" id="gXcount">Counting rows\u2026</div></div>';

        // ── rows ──
        var rowMode = saved.rows || 'all';
        h += '<div class="g-xs"><div class="g-xs__h">Rows</div><div class="g-xr">' +
             xRadio('gxr', 'all', 'Everything that matches these filters', rowMode === 'all') +
             xRadio('gxr', 'page', 'Only the ' + (XOPT.pageRows || 0) + ' on screen now', rowMode === 'page') +
             '</div></div>';

        // ── format. PDF first and by default: what leaves this module is usually a document
        //    for a meeting, not data for a spreadsheet. ──
        var fmt = saved.fmt || 'pdf';
        h += '<div class="g-xs"><div class="g-xs__h">Format</div><div class="g-xr">' +
             xRadio('gxf', 'pdf', 'PDF \u2014 the formal document: crest, certification block, ' +
                                  'grouped by programme, signature block', fmt === 'pdf') +
             xRadio('gxf', 'xls', 'Excel workbook \u2014 cover sheet, frozen headings, extra summary tabs', fmt === 'xls') +
             xRadio('gxf', 'csv', 'CSV \u2014 one flat sheet, for loading elsewhere', fmt === 'csv') +
             '</div></div>';

        // ── columns, folded. Almost nobody changes these, and thirty checkboxes between the
        //    reader and the Export button is thirty checkboxes of scrolling. ──
        if (cols.length) {
            var groups = [], seen = {};
            for (i = 0; i < cols.length; i++) {
                if (!seen[cols[i].g]) { seen[cols[i].g] = []; groups.push(cols[i].g); }
                seen[cols[i].g].push(cols[i]);
            }
            var nOn = 0;
            for (i = 0; i < cols.length; i++)
                if (chosen ? (chosen.indexOf(cols[i].k) >= 0) : !!cols[i].on) nOn++;

            h += xFold('cols', 'Columns', '<span id="gXcolsN">' + nOn + ' of ' + cols.length + '</span>',
                       (function () {
                var x = '<div class="g-xs__h g-xs__h--sub">Choose what the file carries' +
                        '<button type="button" class="g-xlink" id="gXall">all</button>' +
                        '<button type="button" class="g-xlink" id="gXnone">none</button>' +
                        '<button type="button" class="g-xlink" id="gXdef">default</button></div>';
                for (var q = 0; q < groups.length; q++) {
                    x += '<div class="g-xg"><div class="g-xg__h">' + esc(groups[q]) + '</div><div class="g-xg__b">';
                    var g = seen[groups[q]];
                    for (var j = 0; j < g.length; j++) {
                        var cc = g[j];
                        var on = chosen ? (chosen.indexOf(cc.k) >= 0) : !!cc.on;
                        x += '<label class="g-ck"><input type="checkbox" class="gxc" value="' + esc(cc.k) + '"' +
                             (on ? ' checked' : '') + ' data-def="' + (cc.on ? 1 : 0) + '" /><span>' +
                             esc(cc.t) + '</span></label>';
                    }
                    x += '</div></div>';
                }
                return x;
            })());
        }

        // ── advanced, folded: order, grouping, and the extra sheets ──
        var sheets = XOPT.sheets || [];
        var adv = '';
        if (XOPT.sorts && XOPT.sorts.length) {
            var curSort = saved.sort || XOPT.sortDefault || XOPT.sorts[0].k;
            adv += '<div class="g-xg"><div class="g-xg__h">Order the rows by</div><div class="g-xr">';
            for (i = 0; i < XOPT.sorts.length; i++)
                adv += xRadio('gxo', XOPT.sorts[i].k, XOPT.sorts[i].t, curSort === XOPT.sorts[i].k);
            adv += '</div></div>';
        }
        if (XOPT.groups && XOPT.groups.length) {
            var curGrp = saved.group === undefined ? (XOPT.groupDefault || 'prog') : saved.group;
            adv += '<div class="g-xg"><div class="g-xg__h">Break the document into sections by</div><div class="g-xr">';
            for (i = 0; i < XOPT.groups.length; i++)
                adv += xRadio('gxg', XOPT.groups[i].k, XOPT.groups[i].t, curGrp === XOPT.groups[i].k);
            adv += '</div></div>';
        }
        if (sheets.length) {
            var ss = saved.sheets && saved.sheets.length ? saved.sheets : null;
            adv += '<div class="g-xg"><div class="g-xg__h">Extra sheets <em>(workbook only)</em></div>' +
                   '<div class="g-xg__b">';
            for (i = 0; i < sheets.length; i++) {
                var son = ss ? (ss.indexOf(sheets[i].k) >= 0) : !!sheets[i].on;
                adv += '<label class="g-ck"><input type="checkbox" class="gxs" value="' + esc(sheets[i].k) + '"' +
                       (son ? ' checked' : '') + ' /><span>' + esc(sheets[i].t) +
                       (sheets[i].d ? '<small>' + esc(sheets[i].d) + '</small>' : '') + '</span></label>';
            }
            adv += '</div><div class="g-xnote" id="gXcsvnote" style="display:none;">' +
                   'A CSV is a single sheet, and a PDF is a document \u2014 these are included ' +
                   'in the Excel workbook only.</div></div>';
        }
        if (adv !== '') h += xFold('adv', 'Advanced', '', adv);

        qs('gXbody').innerHTML = h;

        xWireFolds();
        xWireFilters();

        // Only the workbook has tabs. Saying so, and disabling them, beats producing a file that
        // quietly lost half of what was asked for.
        function fmtChanged() {
            var notXls = xVal('gxf') !== 'xls';
            var b = document.querySelectorAll('.gxs');
            for (var k = 0; k < b.length; k++) {
                b[k].disabled = notXls;
                b[k].parentNode.classList.toggle('is-off', notXls);
            }
            var note = qs('gXcsvnote');
            if (note) note.style.display = notXls ? 'block' : 'none';
            echo();
        }

        var boxes = qs('gXbody').querySelectorAll('.gxc, .gxs');
        for (i = 0; i < boxes.length; i++) boxes[i].addEventListener('change', echo);
        var radios = qs('gXbody').querySelectorAll('input[name="gxf"]');
        for (i = 0; i < radios.length; i++) radios[i].addEventListener('change', fmtChanged);
        radios = qs('gXbody').querySelectorAll('input[name="gxr"], input[name="gxo"], input[name="gxg"]');
        for (i = 0; i < radios.length; i++) radios[i].addEventListener('change', echo);

        if (qs('gXall')) qs('gXall').addEventListener('click', function () { xSet(1); });
        if (qs('gXnone')) qs('gXnone').addEventListener('click', function () { xSet(0); });
        if (qs('gXdef')) qs('gXdef').addEventListener('click', function () { xSet(-1); });

        fmtChanged();
        qs('gXov').classList.add('is-open');
        document.body.style.overflow = 'hidden';

        // The honest row count, from the server, before the dialog can be used in anger. The
        // same path runs again whenever a filter above changes.
        if (XOPT.countMethod) xRecount();
        else {
            XOPT.total = XOPT.pageRows || 0;
            qs('gXcount').textContent = '';
            echo();
        }

        function xSet(mode) {
            var b = document.querySelectorAll('.gxc');
            for (var k = 0; k < b.length; k++)
                b[k].checked = mode === 1 ? true : mode === 0 ? false : b[k].getAttribute('data-def') === '1';
            echo();
        }
    }

    /// A collapsed section inside the export dialog. Its open/closed state is remembered per
    /// section, so someone who works with columns every day is not folding them open every time.
    function xFold(key, title, badge, body) {
        var open = false;
        try { open = sessionStorage.getItem('gxf_' + key) === '1'; } catch (e) { }
        return '<div class="g-xs g-xs--fold' + (open ? ' is-open' : '') + '" data-xfold="' + key + '">' +
               '<button type="button" class="g-xs__h g-xs__h--btn">' +
                 '<svg viewBox="0 0 24 24" width="10" height="10" fill="none" stroke="currentColor" ' +
                 'stroke-width="3"><polyline points="9 18 15 12 9 6"></polyline></svg>' +
                 '<span>' + esc(title) + '</span>' +
                 (badge ? '<em>' + badge + '</em>' : '') +
               '</button><div class="g-xs__b">' + body + '</div></div>';
    }

    function xWireFolds() {
        var f = qs('gXbody').querySelectorAll('[data-xfold] .g-xs__h--btn');
        for (var i = 0; i < f.length; i++)
            f[i].addEventListener('click', function () {
                var box = this.parentNode, k = box.getAttribute('data-xfold');
                var now = !box.classList.contains('is-open');
                box.classList.toggle('is-open', now);
                try { sessionStorage.setItem('gxf_' + k, now ? '1' : '0'); } catch (e) { }
            });
    }

    function xField(id, label) {
        return '<div class="g-f"><label for="' + id + '">' + esc(label) + '</label>' +
               '<select id="' + id + '"></select></div>';
    }

    /*
       The dialog's own copy of the page filter.

       It is seeded from XOPT.filters, which the page fills from its bootstrap - the same years,
       faculties, departments and programmes, resolved through the same scope, so nothing here
       can offer a combination the server would refuse. Changing one re-counts against the
       server, because a row count nobody can trust is worse than no row count.
    */
    function xWireFilters() {
        var f = XOPT.filters;
        if (!f || !qs('gXyear')) return;

        fill('gXyear', (f.years || []).map(function (y) { return { v: y, t: y }; }),
             f.allowAllYears === false ? null : 'All years');
        if (qs('gXfocus'))
            fill('gXfocus', [{ v: 'cycle', t: 'This cycle \u2014 finishing now' },
                             { v: 'all', t: 'Everyone not yet graduated' }], null);
        fill('gXfac', f.faculties || [], 'All faculties');
        fill('gXdep', f.departments || [], 'All departments');
        fill('gXprog', f.programmes || [], 'All programmes');

        qs('gXyear').value = f.current.acadYear || '';
        if (qs('gXfocus')) qs('gXfocus').value = f.current.focus || 'cycle';
        qs('gXfac').value = f.current.faculty || '';
        qs('gXdep').value = f.current.department || '';
        qs('gXprog').value = f.current.programme || '';
        cascade('gXfac', 'gXdep', 'gXprog');

        // 130 programmes is not a list anyone should scroll.
        combo('gXprog', 'Type a code or part of the name\u2026');
        combo('gXdep', 'Type a department\u2026');
        combo('gXfac', 'Type a faculty\u2026');

        var recount = debounce(xRecount, 220);
        ['gXyear', 'gXfocus', 'gXprog'].forEach(function (id) {
            if (qs(id)) qs(id).addEventListener('change', recount);
        });
        ['gXfac', 'gXdep'].forEach(function (id) {
            qs(id).addEventListener('change', function () {
                cascade('gXfac', 'gXdep', 'gXprog');
                recount();
            });
        });

        qs('gXmatch').addEventListener('click', function () {
            qs('gXyear').value = f.current.acadYear || '';
            if (qs('gXfocus')) qs('gXfocus').value = f.current.focus || 'cycle';
            qs('gXfac').value = f.current.faculty || '';
            qs('gXdep').value = f.current.department || '';
            qs('gXprog').value = f.current.programme || '';
            cascade('gXfac', 'gXdep', 'gXprog');
            xRecount();
        });
    }

    /// The filter the file will actually use: the page's, with the dialog's fields on top.
    function xConfig() {
        var cfg = XOPT.cfg;
        if (!qs('gXyear')) return cfg;
        try {
            var o = JSON.parse(cfg);
            o.acadYear = qs('gXyear').value;
            if (qs('gXfocus')) o.focus = qs('gXfocus').value;
            o.faculty = qs('gXfac').value;
            o.department = qs('gXdep').value;
            o.programme = qs('gXprog').value;
            return JSON.stringify(o);
        } catch (e) { return cfg; }
    }

    function xRecount() {
        var cnt = qs('gXcount');
        if (!cnt || !XOPT.countMethod) { echo(); return; }
        XOPT.total = undefined;
        cnt.textContent = 'Counting rows\u2026';
        echo();
        var mine = ++XCOUNT;
        ajax(XOPT.page, XOPT.countMethod, { configJson: xConfig() }, function (d) {
            if (mine !== XCOUNT) return;      // a later change has already asked again
            if (!d || !d.success) {
                cnt.textContent = 'Could not count the rows; the export will still run.';
                XOPT.total = undefined; echo(); return;
            }
            XOPT.total = d.total;
            cnt.innerHTML = '<b>' + d.total + '</b> row' + (d.total === 1 ? '' : 's') + ' match' +
                (d.total === 1 ? 'es' : '') + ' this selection.' +
                (d.note ? ' <span class="g-xwarn">' + esc(d.note) + '</span>' : '') +
                (d.capped ? ' <span class="g-xwarn">The file stops at ' + d.capped + '.</span>' : '');
            echo();
        });
    }
    var XCOUNT = 0;

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
        if (XOPT.columns && XOPT.columns.length) {
            parts.push(cols + ' column' + (cols === 1 ? '' : 's'));
            var badge = qs('gXcolsN');
            if (badge) badge.textContent = cols + ' of ' + XOPT.columns.length;
        }
        var sh = xPicked('gxs').length;
        if (sh && fmt === 'xls') parts.push(sh + ' extra sheet' + (sh === 1 ? '' : 's'));
        var ord = xVal('gxo');
        if (ord && XOPT.sorts) {
            for (var z = 0; z < XOPT.sorts.length; z++)
                if (XOPT.sorts[z].k === ord) { parts.push('by ' + XOPT.sorts[z].t.toLowerCase()); break; }
        }
        parts.push(fmt === 'csv' ? 'CSV' : fmt === 'xls' ? 'Excel workbook' : 'PDF document');
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
            fmt = xVal('gxf'), mode = xVal('gxr'),
            ord = xVal('gxo'), grp = xVal('gxg');
        xSave(XKEY, { cols: cols, sheets: sheets, fmt: fmt, rows: mode, sort: ord, group: grp });

        // Order and grouping travel inside the filter, so the server applies them to the rows
        // themselves, "sort by performance" means CGPA descending in the PDF, the workbook and
        // the CSV alike, not a label on one of them.
        var cfg = xConfig();
        try {
            var o = JSON.parse(cfg);
            if (ord) o.orderBy = ord;
            o.groupBy = grp || '';
            cfg = JSON.stringify(o);
        } catch (e) { }

        var extra = {
            gradCols: cols.join(','),
            gradSheets: sheets.join(','),
            gradRows: mode,
            gradPageSize: String(XOPT.pageRows || 0)
        };
        post(XOPT.page, fmt, cfg, extra);
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

    /* ── The reason dialog ────────────────────────────────────────────
       prompt() is gone. It cannot be styled, cannot validate as you type, cannot show whom you
       are about to hold, and on a batch of forty it gives no clue what the forty are.

       The suggestions come from the server, chosen for THIS candidate out of the findings the
       engine has just produced, so a student with an unpublished mark is offered a sentence
       about unpublished marks, not a dropdown of everything anyone has ever written. Clicking
       one fills the box rather than submitting, because the reviewer should add the specifics:
       which paper, which document. */
    var RD = null;

    function reasonMount() {
        if (qs('gRov')) return;
        var d = document.createElement('div');
        d.innerHTML =
            '<div class="g-ov" id="gRov">' +
              '<div class="g-modal g-modal--r" role="dialog" aria-modal="true" aria-labelledby="gRtitle">' +
                '<div class="g-modal__h">' +
                  '<div class="g-modal__t"><b id="gRtitle">Hold</b><span id="gRsub"></span></div>' +
                  '<button type="button" class="g-modal__x" id="gRx" aria-label="Close">&times;</button>' +
                '</div>' +
                '<div class="g-modal__b" id="gRbody"></div>' +
                '<div class="g-modal__f">' +
                  '<span class="g-hint" id="gRcount"></span>' +
                  '<button type="button" class="g-btn" id="gRcancel">Cancel</button>' +
                  '<button type="button" class="g-btn g-btn--p" id="gRgo" disabled="disabled">Hold</button>' +
                '</div>' +
              '</div>' +
            '</div>';
        while (d.firstChild) document.body.appendChild(d.firstChild);

        var ov = qs('gRov');
        ov.addEventListener('mousedown', function (e) { if (e.target === ov) reasonClose(); });
        qs('gRx').addEventListener('click', reasonClose);
        qs('gRcancel').addEventListener('click', reasonClose);
        qs('gRgo').addEventListener('click', reasonSubmit);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && ov.classList.contains('is-open')) reasonClose();
        });
    }

    function reasonClose() {
        var ov = qs('gRov');
        if (!ov) return;
        ov.classList.remove('is-open');
        // The evidence modal may still be open underneath; only the last one restores scrolling.
        if (!qs('gOv') || !qs('gOv').classList.contains('is-open')) document.body.style.overflow = '';
    }

    function reasonSubmit() {
        if (!RD) return;
        var v = qs('gRtext').value.trim();
        if (v.length < RD.min) return;
        var cb = RD.onSubmit;
        reasonClose();
        if (cb) cb(v);
    }

    /// The one call a page makes to ask for a reason.
    function reasonDialog(o) {
        reasonMount();
        RD = o || {};
        RD.min = RD.min === undefined ? 10 : RD.min;

        qs('gRtitle').textContent = RD.title || 'Hold this candidate';
        qs('gRsub').textContent = RD.subtitle || '';
        qs('gRgo').textContent = RD.verb || 'Hold';

        qs('gRbody').innerHTML =
            (RD.warn ? '<div class="g-note g-note--warn">' + esc(RD.warn) + '</div>' : '') +
            '<div class="g-rs" id="gRsug"><div class="g-load">Finding the likely reasons\u2026</div></div>' +
            '<label class="g-rl" for="gRtext">The reason, in your own words</label>' +
            '<textarea id="gRtext" class="g-rt" rows="3" spellcheck="true" ' +
            'placeholder="Whoever picks this up next has only this sentence to go on."></textarea>' +
            '<div class="g-rh" id="gRhint"></div>';

        var ta = qs('gRtext');
        ta.value = RD.initial || '';

        function grade() {
            var n = ta.value.trim().length;
            var ok = n >= RD.min;
            qs('gRgo').disabled = !ok;
            qs('gRhint').className = 'g-rh' + (ok ? ' is-ok' : '');
            qs('gRhint').textContent = ok
                ? 'That will be recorded against your name.'
                : (RD.min - n) + ' more character' + (RD.min - n === 1 ? '' : 's') + ' needed.';
            qs('gRcount').textContent = RD.count > 1
                ? (RD.count + ' candidates will be held under this reason.') : '';
        }
        ta.addEventListener('input', grade);
        // Ctrl+Enter submits: a reviewer working a queue keeps their hands on the keyboard.
        ta.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); reasonSubmit(); }
        });
        grade();

        qs('gRov').classList.add('is-open');
        document.body.style.overflow = 'hidden';
        setTimeout(function () { try { ta.focus(); } catch (e) { } }, 30);

        ajax(RD.page, 'HoldReasons', { regno: RD.regno || '' }, function (d) {
            var box = qs('gRsug');
            if (!box) return;
            if (!d || !d.success || !d.suggestions || !d.suggestions.length) { box.innerHTML = ''; return; }
            var h = '<div class="g-rs__h">Likely reasons' +
                    (RD.regno ? ' for this candidate' : '') + '</div>';
            for (var i = 0; i < d.suggestions.length; i++)
                h += '<button type="button" class="g-sug" data-i="' + i + '">' +
                     '<b>' + esc(d.suggestions[i].text) + '</b>' +
                     (d.suggestions[i].why ? '<span>' + esc(d.suggestions[i].why) + '</span>' : '') +
                     '</button>';
            box.innerHTML = h;
            var b = box.querySelectorAll('.g-sug');
            for (i = 0; i < b.length; i++)
                b[i].addEventListener('click', function () {
                    // Fill, do not submit. The specifics are what make a reason useful.
                    ta.value = d.suggestions[+this.getAttribute('data-i')].text;
                    grade();
                    try { ta.focus(); ta.setSelectionRange(ta.value.length, ta.value.length); } catch (e) { }
                });
        });
    }

    /* ── Bringing somebody in by hand ─────────────────────────────────
       The candidacy rule is deliberately narrow and it is right about 1,254 of 1,254 graduands
       on record. But a rule that is right almost always still has to be overrulable by a person
       with the evidence in front of them, because the cases it misses are exactly the ones with
       a broken record: a study year never written, a semester registered under the wrong
       programme, results sitting on an entry number.

       Nothing here bends the engine. It finds a student and opens the SAME review panel, which
       re-assesses them from live marks and demands a written justification before anyone blocked
       can be cleared. The override is a decision by a named person, on the record. */
    var FD = null;

    function findMount() {
        if (qs('gFov')) return;
        var d = document.createElement('div');
        d.innerHTML =
            '<div class="g-ov" id="gFov">' +
              '<div class="g-modal g-modal--f" role="dialog" aria-modal="true" aria-labelledby="gFtitle">' +
                '<div class="g-modal__h">' +
                  '<div class="g-modal__t"><b id="gFtitle">Add a student to the queue</b>' +
                    '<span>Anyone in your scope, whether or not the engine sees them as a candidate.</span></div>' +
                  '<button type="button" class="g-modal__x" id="gFx" aria-label="Close">&times;</button>' +
                '</div>' +
                '<div class="g-modal__b">' +
                  '<input type="text" class="g-fq" id="gFq" autocomplete="off" ' +
                    'placeholder="Student number, entry number, or name\u2026" />' +
                  '<div class="g-fr" id="gFr"><div class="g-empty">Type at least two characters.</div></div>' +
                '</div>' +
              '</div>' +
            '</div>';
        while (d.firstChild) document.body.appendChild(d.firstChild);

        var ov = qs('gFov');
        ov.addEventListener('mousedown', function (e) { if (e.target === ov) findClose(); });
        qs('gFx').addEventListener('click', findClose);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && ov.classList.contains('is-open')) findClose();
        });
    }

    function findClose() {
        var ov = qs('gFov');
        if (!ov) return;
        ov.classList.remove('is-open');
        if (!qs('gOv') || !qs('gOv').classList.contains('is-open')) document.body.style.overflow = '';
    }

    /// The page says what to do with the student that gets picked.
    function findStudent(o) {
        findMount();
        FD = o || {};
        qs('gFq').value = '';
        qs('gFr').innerHTML = '<div class="g-empty">Type at least two characters.</div>';
        qs('gFov').classList.add('is-open');
        document.body.style.overflow = 'hidden';
        setTimeout(function () { try { qs('gFq').focus(); } catch (e) { } }, 30);

        var seq = 0;
        var run = debounce(function () {
            var q = qs('gFq').value.trim();
            if (q.length < 2) {
                qs('gFr').innerHTML = '<div class="g-empty">Type at least two characters.</div>';
                return;
            }
            qs('gFr').innerHTML = '<div class="g-load">Searching\u2026</div>';
            var mine = ++seq;
            ajax(FD.page, 'FindStudent', { q: q, acadYear: FD.acadYear || '' }, function (d) {
                if (mine !== seq) return;           // a later keystroke has already asked
                if (!d || !d.success) {
                    qs('gFr').innerHTML = '<div class="g-note g-note--bad">' +
                        esc((d && d.message) || 'The search failed.') + '</div>';
                    return;
                }
                var rows = d.rows || [];
                if (!rows.length) {
                    qs('gFr').innerHTML = '<div class="g-empty">Nobody in your scope matches \u201c' +
                        esc(q) + '\u201d.</div>';
                    return;
                }
                var h = '';
                for (var i = 0; i < rows.length; i++) {
                    var r = rows[i];
                    h += '<button type="button" class="g-fo" data-reg="' + esc(r.regno) + '">' +
                         '<img class="g-ph" loading="lazy" alt="" src="' + photo(r.regno) + '" />' +
                         '<span class="g-fo__t"><b>' + esc(r.name) + '</b>' +
                         '<span>' + esc(r.regno) + '  \u00b7  ' + esc(r.progname || r.progcode) +
                         (r.entryyear ? '  \u00b7  intake ' + esc(r.entryyear) : '') + '</span></span>' +
                         '<span class="g-fo__n' +
                           (r.onList ? ' is-listed' : (r.isCandidate ? ' is-cand' : ' is-new')) + '">' +
                           esc(r.note) + '</span></button>';
                }
                qs('gFr').innerHTML = h;
                var b = qs('gFr').querySelectorAll('.g-fo');
                for (i = 0; i < b.length; i++)
                    b[i].addEventListener('click', function () {
                        var reg = this.getAttribute('data-reg');
                        findClose();
                        if (FD.onPick) FD.onPick(reg);
                    });
            });
        }, 250);

        qs('gFq').addEventListener('input', run);
        qs('gFq').addEventListener('keydown', function (e) {
            if (e.key !== 'Enter') return;
            var first = qs('gFr').querySelector('.g-fo');
            if (first) first.click();
        });
    }

    /* ── The queue ────────────────────────────────────────────────────
       A reviewer with 996 candidates should decide and be handed the next one, not be returned
       to a table to find their place again.

       The list is deliberately NOT reloaded between decisions. A cleared candidate leaves the
       pending queue, so reloading would renumber the rows underneath and make "next" mean
       something different every time. The decided row is marked in place and the list refreshes
       when the modal finally closes. */
    var Q = { ids: [], idx: -1, total: 0, page: 1, pages: 1, size: 50, nextPage: null, decided: {} };

    function queue(o) {
        Q.ids = o.ids || [];
        Q.total = o.total || Q.ids.length;
        Q.page = o.page || 1;
        Q.pages = o.pages || 1;
        Q.size = o.size || 50;
        Q.nextPage = o.nextPage || null;
        Q.idx = -1;
    }

    function autoAdvance() {
        try { return sessionStorage.getItem('gauto') !== '0'; } catch (e) { return true; }
    }
    function setAutoAdvance(on) {
        try { sessionStorage.setItem('gauto', on ? '1' : '0'); } catch (e) { }
    }

    /// Marks a candidate as decided so the queue can skip it and the row can show it.
    function markDecided(regno, label) {
        Q.decided[regno] = label || 'done';
        var tr = document.querySelector('#gBody tr[data-reg="' + (window.CSS && CSS.escape
                    ? CSS.escape(regno) : regno) + '"]');
        if (tr) {
            tr.classList.add('is-decided');
            var chip = tr.querySelector('.g-chip');
            if (chip) { chip.className = 'g-chip g-chip--listed'; chip.textContent = label || 'done'; }
            var ck = tr.querySelector('.ck');
            if (ck) { ck.checked = false; ck.disabled = true; }
        }
    }

    /// The next candidate after this one that has not already been decided in this run.
    function nextIn(from) {
        for (var i = from + 1; i < Q.ids.length; i++)
            if (!Q.decided[Q.ids[i]]) return i;
        return -1;
    }
    function prevIn(from) {
        for (var i = from - 1; i >= 0; i--)
            if (!Q.decided[Q.ids[i]]) return i;
        return -1;
    }

    function queuePos() {
        if (Q.idx < 0 || !Q.ids.length) return '';
        var onPage = (Q.idx + 1) + ' of ' + Q.ids.length + ' on this page';
        if (Q.total > Q.ids.length) {
            var overall = (Q.page - 1) * Q.size + Q.idx + 1;
            return onPage + '  \u00b7  ' + overall + ' of ' + Q.total + ' in the queue';
        }
        return onPage;
    }

    return {
        qs: qs, esc: esc, n0: n0, n1: n1, n2: n2,
        ajax: ajax, toast: toast,
        photo: photo, photoCell: photoCell, chip: chip, credits: credits,
        openPhoto: openPhoto,
        readUrl: readUrl, writeUrl: writeUrl,
        fill: fill, cascade: cascade, combo: combo,
        mount: mount, openModal: openModal, closeModal: closeModal, wireModal: wireModal,
        openStudent: openStudent, currentStudent: currentStudent, onWalk: onWalk,
        showPhoto: showPhoto,
        csv: csv, serverExport: serverExport, exportDialog: exportDialog,
        debounce: debounce, freshness: freshness, chips: chips, pager: pager,
        reasonDialog: reasonDialog, findStudent: findStudent,
        queue: queue, markDecided: markDecided,
        autoAdvance: autoAdvance, setAutoAdvance: setAutoAdvance,
        queueNext: function () { return nextIn(Q.idx); },
        queuePrev: function () { return prevIn(Q.idx); },
        queueAt: function (i) { return Q.ids[i]; },
        queueIndex: function () { return Q.idx; },
        queueSetIndex: function (i) { Q.idx = i; },
        queueIds: function () { return Q.ids; },
        queueHasMorePages: function () { return Q.page < Q.pages; },
        queueLoadNextPage: function () { if (Q.nextPage) Q.nextPage(Q.page + 1); }
    };
})();
