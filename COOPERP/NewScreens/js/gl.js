/* =====================================================================
   General Ledger screens: shared front-end (window.GL), on top of
   window.FA (fa.js). Renders report results, the Checks panel and the
   export dialog the same way on every screen. ES5 only.
   ===================================================================== */
(function () {
    'use strict';
    var FA = window.FA, GL = {};

    var ICONS = {
        check: '<polyline points="20 6 9 17 4 12"/>',
        alert: '<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>',
        info: '<circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/>',
        refresh: '<polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>',
        play: '<polygon points="5 3 19 12 5 21 5 3"/>',
        star: '<polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>',
        trash: '<polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>',
        ext: '<path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/>',
        user: '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
        note: '<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>',
        wrench: '<path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>'
    };
    GL.icon = function (name, size) {
        var s = size || 14;
        if (!ICONS[name]) return FA.icon(name, size);
        return '<svg xmlns="http://www.w3.org/2000/svg" width="' + s + '" height="' + s + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + ICONS[name] + '</svg>';
    };

    /** 1,234,567 and (1,234,567) for negatives: the same rule as the server and the files. */
    GL.money = function (n) {
        if (n === null || n === undefined || n === '') return '';
        var v = Math.round(Number(n)); if (isNaN(v)) return '';
        var s = String(Math.abs(v)).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        return v < 0 ? '(' + s + ')' : s;
    };
    GL.short = function (n) {
        var v = Number(n) || 0, a = Math.abs(v), s;
        if (a >= 1e9) s = (a / 1e9).toFixed(a >= 1e10 ? 1 : 2) + 'bn';
        else if (a >= 1e6) s = (a / 1e6).toFixed(a >= 1e7 ? 1 : 2) + 'm';
        else if (a >= 1e3) s = Math.round(a / 1e3) + 'k';
        else s = String(Math.round(a));
        return v < 0 ? '(' + s + ')' : s;
    };

    /** A fix target: a report code, a URL with a query, or a page. */
    GL.fixUrl = function (fix) {
        if (!fix || !fix.target) return '';
        if (fix.kind === 'report' && /^R\d\d$/.test(fix.target)) return 'AccountsReports.aspx?r=' + fix.target + '&run=1';
        return fix.target;
    };

    // ── Checks panel ─────────────────────────────────────────────────
    GL.checks = function (el, checks, o) {
        if (typeof el === 'string') el = FA.qs(el);
        o = o || {};
        checks = checks || [];
        if (!checks.length) { el.innerHTML = ''; return; }
        var fail = checks.filter(function (c) { return c.status === 'fail'; }).length;
        var h = '<div class="gl-checks"><div class="gl-checks__head" data-tog>' +
            '<span class="gl-checks__title">Checks</span>' +
            (fail ? '<span class="fa-badge fa-badge--bad">' + fail + ' need' + (fail === 1 ? 's' : '') + ' attention</span>' : '<span class="fa-badge fa-badge--ok">All passed</span>') +
            '<span class="fa-muted">' + checks.length + ' check' + (checks.length === 1 ? '' : 's') + '</span>' + FA.icon('chevron') + '</div><div data-body' + (o.collapsed && !fail ? ' style="display:none"' : '') + '>';
        checks.forEach(function (c) {
            var ic = c.status === 'pass' ? 'check' : c.status === 'fail' ? 'alert' : 'info';
            h += '<div class="gl-check gl-check--' + c.status + '"><div class="gl-check__icon">' + GL.icon(ic, 14) + '</div>' +
                '<div><div class="gl-check__title">' + FA.esc(c.title) + '</div>' +
                (c.cause ? '<div class="gl-check__cause">' + FA.esc(c.cause) + '</div>' : '') +
                (c.fix && GL.fixUrl(c.fix) ? '<div class="gl-check__fix"><a class="gl-link" href="' + FA.esc(GL.fixUrl(c.fix)) + '">' + GL.icon('wrench', 12) + ' ' + FA.esc(c.fix.label) + '</a></div>' : '') + '</div>' +
                '<div class="gl-check__amt">' + (c.amount ? GL.money(c.amount) : '') + (c.count ? '<small>' + FA.money(c.count) + '</small>' : '') + '</div>';
            if (c.parts && c.parts.length) {
                var sum = 0;
                h += '<div class="gl-parts"><table>';
                c.parts.forEach(function (p) {
                    sum += Number(p.amount) || 0;
                    h += '<tr><td><b>' + FA.esc(p.title) + '</b><div class="fa-sub">' + FA.esc(p.cause || '') + '</div>' +
                        (p.fix && GL.fixUrl(p.fix) ? '<a class="gl-link" href="' + FA.esc(GL.fixUrl(p.fix) + (p.code && GL.fixUrl(p.fix).indexOf('R18') >= 0 ? '&pattern=' + p.code : '')) + '">List them</a>' : '') + '</td>' +
                        '<td class="fa-num">' + FA.money(p.count) + '</td><td class="fa-num">' + GL.money(p.amount) + '</td></tr>';
                });
                h += '<tr><td>Total of the causes</td><td></td><td class="fa-num">' + GL.money(sum) + '</td></tr></table></div>';
            }
            h += '</div>';
        });
        h += '</div></div>';
        el.innerHTML = h;
        el.querySelector('[data-tog]').onclick = function () { var b = el.querySelector('[data-body]'); b.style.display = b.style.display === 'none' ? '' : 'none'; };
    };

    // ── Result table ─────────────────────────────────────────────────
    /** o: { onSort(idx), sort, desc, onPage(p), onSearch(q), search, sizes, onSize } */
    GL.result = function (el, res, o) {
        if (typeof el === 'string') el = FA.qs(el);
        o = o || {};
        var h = '';
        if (res.basis) h += '<div class="gl-basis"><b>What the figures include.</b> ' + FA.esc(res.basis) + '</div>';
        if (!res.structured) {
            h += '<div class="gl-toolbar"><input type="search" class="fa-input" data-q placeholder="Search these rows" value="' + FA.esc(o.search || '') + '"/>' +
                '<span class="fa-spacer"></span><span class="fa-muted">' + FA.plural(res.total, res.noun || 'row', res.nounPlural || 'rows') + (res.ms !== undefined ? ', ' + (res.ms / 1000).toFixed(2) + ' s' : '') + '</span>' +
                '<select class="fa-select" data-size>' + [25, 50, 100, 250].map(function (n) { return '<option' + (n === res.size ? ' selected' : '') + '>' + n + '</option>'; }).join('') + '</select></div>';
        }
        h += '<div class="fa-table-wrap"><table class="fa-table"><thead><tr>';
        res.cols.forEach(function (c) {
            var num = c.kind === 'money' || c.kind === 'count' || c.kind === 'pct';
            var arrow = o.sort === c.idx ? '<span class="gl-arrow">' + (o.desc ? '&#9660;' : '&#9650;') + '</span>' : '';
            h += '<th class="' + (num ? 'fa-num' : '') + (c.sortable ? ' is-sort' : '') + '" data-c="' + c.idx + '">' + FA.esc(c.t) + arrow + '</th>';
        });
        h += '</tr></thead><tbody>';
        if (!res.rows.length) h += '<tr><td colspan="' + res.cols.length + '" class="fa-empty">Nothing to show for these parameters.</td></tr>';
        res.rows.forEach(function (r) {
            var cls = (r.k ? 'gl-' + r.k : '') + (r.l ? ' is-click' : '');
            h += '<tr class="' + cls + '"' + (r.l ? ' data-l="' + FA.esc(r.l) + '"' : '') + '>';
            r.c.forEach(function (v, i) {
                var c = res.cols[i], num = c.kind === 'money' || c.kind === 'count' || c.kind === 'pct';
                var neg = num && /^\(/.test(v);
                h += '<td class="' + (num ? 'fa-num' : '') + (neg ? ' gl-neg' : '') + (c.kind === 'code' ? ' fa-code' : '') + (c.kind === 'date' ? ' gl-nowrap' : '') + '">' + FA.esc(v) + '</td>';
            });
            h += '</tr>';
        });
        h += '</tbody>';
        if (res.totals && res.totals.some(function (t) { return t !== ''; })) {
            var labelled = false;
            h += '<tfoot><tr>' + res.totals.map(function (t, i) {
                var c = res.cols[i], num = c.kind === 'money' || c.kind === 'count' || c.kind === 'pct';
                if (t === '' && !labelled && !num) { labelled = true; return '<td>Total</td>'; }
                return '<td class="' + (num ? 'fa-num' : '') + '">' + FA.esc(t) + '</td>';
            }).join('') + '</tr></tfoot>';
        }
        h += '</table></div>';
        if (res.paged && !res.structured) h += '<div class="fa-card__foot"><div data-pager style="flex:1"></div></div>';
        el.innerHTML = h;

        Array.prototype.forEach.call(el.querySelectorAll('tr[data-l]'), function (tr) {
            tr.onclick = function (e) { var u = tr.getAttribute('data-l'); if (e.ctrlKey || e.metaKey) window.open(u, '_blank'); else location.href = u; };
        });
        Array.prototype.forEach.call(el.querySelectorAll('th.is-sort'), function (th) {
            th.onclick = function () { if (o.onSort) o.onSort(parseInt(th.getAttribute('data-c'), 10)); };
        });
        var q = el.querySelector('[data-q]');
        if (q) q.addEventListener('input', FA.debounce(function () { if (o.onSearch) o.onSearch(q.value.trim()); }, 400));
        var sz = el.querySelector('[data-size]');
        if (sz) sz.onchange = function () { if (o.onSize) o.onSize(parseInt(sz.value, 10)); };
        var pg = el.querySelector('[data-pager]');
        if (pg) FA.pager(pg, { page: res.page, size: res.size, total: res.total }, function (p) { if (o.onPage) o.onPage(p); });
    };

    // ── Export dialog ────────────────────────────────────────────────
    /** o: { page, title, cols:[{k,t}], fields:{...}, total } posts glFormat and glCols with the fields. */
    GL.exportDialog = function (o) {
        var cols = (o.cols || []).map(function (c) { return '<label class="fa-check-line"><input type="checkbox" value="' + FA.esc(c.k) + '" checked/> ' + FA.esc(c.t) + '</label>'; }).join('');
        var big = o.total > 20000;
        var m = FA.modal({
            title: 'Export', sub: o.title, size: 'mid',
            body: '<div class="fa-field"><span class="fa-label">Format</span><div class="fa-radios">' +
                '<label><input type="radio" name="glXFmt" value="pdf"' + (big ? ' disabled' : ' checked') + '/> PDF document' + (big ? ' (up to 20,000 rows)' : '') + '</label>' +
                '<label><input type="radio" name="glXFmt" value="xls"' + (big ? ' checked' : '') + '/> Excel workbook</label>' +
                '<label><input type="radio" name="glXFmt" value="csv"/> CSV</label></div></div>' +
                (cols ? '<div class="fa-section" style="margin-top:16px">Columns</div><div class="fa-cols" id="glXCols">' + cols + '</div>' : '') +
                '<div class="fa-notice" style="margin-top:14px">The file holds the same rows, totals and checks as the screen' + (o.total !== undefined ? ': ' + FA.plural(o.total, 'row', 'rows') : '') +
                ', with the parameters on its cover. Search and sort are kept.</div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>' + FA.icon('download') + ' Download</button>'
        });
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        m.foot.querySelector('[data-ok]').onclick = function () {
            var fmt = (m.body.querySelector('input[name=glXFmt]:checked') || {}).value || 'xls';
            var keep = Array.prototype.map.call(m.body.querySelectorAll('#glXCols input:checked'), function (i) { return i.value; });
            var f = {}; Object.keys(o.fields || {}).forEach(function (k) { f[k] = o.fields[k]; });
            f.glFormat = fmt; f.glCols = keep.join(',');
            FA.download(o.page, f);
            FA.toast('Preparing the file. It will download in a moment.');
            m.close();
        };
    };

    GL.sev = function (s) { var t = { CRITICAL: 'Critical', HIGH: 'High', MEDIUM: 'Medium', INFO: 'Information' }[s] || s; return '<span class="gl-sev gl-sev--' + FA.esc(s) + '">' + t + '</span>'; };
    GL.status = function (s) { var t = { OPEN: 'Open', ACKNOWLEDGED: 'Acknowledged', FIXED: 'Fixed', REAPPEARED: 'Reappeared' }[s] || s; return '<span class="gl-status gl-status--' + FA.esc(s) + '">' + t + '</span>'; };

    /** Health ring: an SVG arc for the score. */
    GL.ring = function (score) {
        var s = Math.max(0, Math.min(100, Number(score) || 0)), r = 26, c = 2 * Math.PI * r, off = c * (1 - s / 100);
        var col = s >= 80 ? '#166534' : s >= 50 ? '#D4A017' : '#b42318';
        return '<svg class="gl-health__ring" viewBox="0 0 64 64"><circle cx="32" cy="32" r="' + r + '" fill="none" stroke="#E0E5ED" stroke-width="8"/>' +
            '<circle cx="32" cy="32" r="' + r + '" fill="none" stroke="' + col + '" stroke-width="8" stroke-dasharray="' + c.toFixed(1) + '" stroke-dashoffset="' + off.toFixed(1) + '" transform="rotate(-90 32 32)"/></svg>';
    };

    /** Small line chart from a list of numbers. */
    GL.spark = function (vals, color) {
        if (!vals || vals.length < 2) return '';
        var max = Math.max.apply(null, vals), min = Math.min.apply(null, vals), w = 200, h = 40, span = (max - min) || 1;
        var pts = vals.map(function (v, i) { return (i * w / (vals.length - 1)).toFixed(1) + ',' + (h - 3 - (v - min) * (h - 6) / span).toFixed(1); }).join(' ');
        return '<svg class="gl-spark" viewBox="0 0 ' + w + ' ' + h + '" preserveAspectRatio="none"><polyline points="' + pts + '" fill="none" stroke="' + (color || '#174DA4') + '" stroke-width="2"/></svg>';
    };

    window.GL = GL;
})();
