/* =====================================================================
   Fixed Assets module: shared front-end toolkit (window.FA).
   Same shape as the Graduation toolkit (calls, modal, reason dialog,
   pager, URL state, export dialog) without its wording. ES5 only.
   ===================================================================== */
(function () {
    'use strict';
    var FA = {};

    // ── Basics ────────────────────────────────────────────────────────
    FA.qs = function (id) { return document.getElementById(id); };
    FA.esc = function (s) {
        return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };
    FA.money = function (n) {
        if (n === null || n === undefined || n === '') return '';
        var v = Math.round(Number(n));
        if (isNaN(v)) return '';
        var neg = v < 0; v = Math.abs(v);
        var s = String(v).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        return (neg ? '-' : '') + s;
    };
    FA.short = function (n) {
        var v = Number(n) || 0, a = Math.abs(v);
        if (a >= 1e9) return (v / 1e9).toFixed(a >= 1e10 ? 1 : 2) + 'bn';
        if (a >= 1e6) return (v / 1e6).toFixed(a >= 1e7 ? 1 : 2) + 'm';
        if (a >= 1e3) return Math.round(v / 1e3) + 'k';
        return String(Math.round(v));
    };
    FA.plural = function (n, one, many) { return FA.money(n) + ' ' + (Number(n) === 1 ? one : many); };
    FA.uuid = function () {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0; return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
        });
    };
    FA.debounce = function (fn, ms) { var t; return function () { var a = arguments, s = this; clearTimeout(t); t = setTimeout(function () { fn.apply(s, a); }, ms || 300); }; };
    FA.token = function () { var m = document.querySelector('meta[name="csrf-token"]'); return m ? m.getAttribute('content') : ''; };
    FA.today = function () { var d = new Date(); return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2); };

    var ICONS = {
        plus: '<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>',
        download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>',
        upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/>',
        x: '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>',
        edit: '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z"/>',
        grip: '<circle cx="9" cy="6" r="1"/><circle cx="15" cy="6" r="1"/><circle cx="9" cy="12" r="1"/><circle cx="15" cy="12" r="1"/><circle cx="9" cy="18" r="1"/><circle cx="15" cy="18" r="1"/>',
        chevron: '<polyline points="6 9 12 15 18 9"/>',
        right: '<polyline points="9 18 15 12 9 6"/>',
        print: '<polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/>',
        tag: '<path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/>',
        file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/>',
        box: '<path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/>',
        chart: '<line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/>',
        list: '<line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/>',
        layers: '<polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/>',
        book: '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>',
        lock: '<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>',
        unlock: '<rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 9.9-1"/>',
        undo: '<polyline points="1 4 1 10 7 10"/><path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"/>',
        eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
        search: '<circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>',
        check: '<polyline points="20 6 9 17 4 12"/>'
    };
    FA.icon = function (name, size) {
        var s = size || 14;
        return '<svg xmlns="http://www.w3.org/2000/svg" width="' + s + '" height="' + s + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + (ICONS[name] || '') + '</svg>';
    };

    FA.statusKind = function (s) {
        switch (s) {
            case 'IN_USE': return 'ok';
            case 'IN_STORE': return 'info';
            case 'UNDER_REPAIR': return 'warn';
            case 'LOST': case 'WRITTEN_OFF': return 'bad';
            default: return 'neutral';
        }
    };
    FA.badge = function (status, label) { return '<span class="fa-badge fa-badge--' + FA.statusKind(status) + '">' + FA.esc(label) + '</span>'; };
    FA.change = function (pct) {
        var p = Number(pct) || 0;
        if (p === 0) return '<span class="fa-muted">No change</span>';
        return '<span class="' + (p > 0 ? 'fa-up' : 'fa-down') + '">' + (p > 0 ? 'Up ' : 'Down ') + Math.abs(p).toFixed(1) + '%</span>';
    };

    // ── Server calls ──────────────────────────────────────────────────
    FA.call = function (page, method, args, cb) {
        var x = new XMLHttpRequest();
        x.open('POST', page + '/' + method, true);
        x.setRequestHeader('Content-Type', 'application/json; charset=utf-8');
        x.setRequestHeader('X-CSRF-Token', FA.token());
        x.timeout = 300000;
        x.onreadystatechange = function () {
            if (x.readyState !== 4) return;
            var res;
            if (x.status === 200) {
                try { var o = JSON.parse(x.responseText); res = o.d !== undefined ? o.d : o; if (typeof res === 'string') res = JSON.parse(res); }
                catch (e) { res = { success: false, message: 'The server sent an unexpected reply. Reload the page and try again.' }; }
            } else if (x.status === 401 || x.status === 403) {
                res = { success: false, denied: true, message: 'Your session has ended. Sign in again.' };
            } else {
                res = { success: false, message: x.status === 0 ? 'No connection to the server. Check your network and try again.' : 'The server could not complete the request. Try again.' };
            }
            if (res && res.denied) FA.toast(res.message, true);
            cb(res || { success: false, message: 'No reply.' });
        };
        x.ontimeout = function () { cb({ success: false, message: 'The request took too long. Try again with fewer items.' }); };
        x.send(JSON.stringify(args || {}));
    };

    // ── Toast ─────────────────────────────────────────────────────────
    var toastTimer;
    FA.toast = function (msg, bad) {
        var t = FA.qs('faToast');
        if (!t) { t = document.createElement('div'); t.id = 'faToast'; t.setAttribute('role', 'status'); document.body.appendChild(t); }
        t.className = 'fa-toast is-on' + (bad ? ' fa-toast--err' : '');
        t.textContent = msg;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { t.className = 'fa-toast'; }, bad ? 7000 : 4000);
    };

    // ── Modal ─────────────────────────────────────────────────────────
    var openModals = [];
    FA.modal = function (o) {
        o = o || {};
        var wrap = document.createElement('div');
        wrap.className = 'fa-modal is-open';
        wrap.setAttribute('role', 'dialog'); wrap.setAttribute('aria-modal', 'true');
        wrap.innerHTML = '<div class="fa-modal__box' + (o.size ? ' fa-modal__box--' + o.size : '') + '">' +
            '<div class="fa-modal__head"><div><div class="fa-modal__title"></div><div class="fa-modal__sub"></div></div>' +
            '<button type="button" class="fa-modal__close" aria-label="Close">' + FA.icon('x', 18) + '</button></div>' +
            '<div class="fa-modal__body"></div><div class="fa-modal__foot"></div></div>';
        var m = {
            el: wrap,
            body: wrap.querySelector('.fa-modal__body'),
            foot: wrap.querySelector('.fa-modal__foot'),
            title: function (t, s) { wrap.querySelector('.fa-modal__title').textContent = t || ''; wrap.querySelector('.fa-modal__sub').textContent = s || ''; },
            close: function () {
                if (o.beforeClose && o.beforeClose() === false) return;
                if (wrap.parentNode) wrap.parentNode.removeChild(wrap);
                var i = openModals.indexOf(m); if (i >= 0) openModals.splice(i, 1);
                if (o.onClose) o.onClose();
            }
        };
        m.title(o.title, o.sub);
        if (o.body) m.body.innerHTML = o.body;
        if (o.foot) m.foot.innerHTML = o.foot; else m.foot.style.display = 'none';
        wrap.querySelector('.fa-modal__close').onclick = function () { m.close(); };
        wrap.addEventListener('mousedown', function (e) { if (e.target === wrap && !o.sticky) m.close(); });
        document.body.appendChild(wrap);
        openModals.push(m);
        return m;
    };
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && openModals.length) openModals[openModals.length - 1].close();
    });

    FA.confirm = function (o, onOk) {
        var m = FA.modal({
            title: o.title || 'Please confirm', body: '<p style="margin:0;line-height:1.6">' + FA.esc(o.message || '').replace(/\n/g, '<br/>') + '</p>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button>' +
                  '<button type="button" class="fa-btn ' + (o.danger ? 'fa-btn--danger' : 'fa-btn--primary') + '" data-ok>' + FA.esc(o.ok || 'Confirm') + '</button>'
        });
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        var ok = m.foot.querySelector('[data-ok]');
        ok.onclick = function () { m.close(); onOk(); };
        ok.focus();
        return m;
    };

    /** A dialog that asks for a reason (and optional extra fields). onSubmit(reason, modal, done) */
    FA.reason = function (o, onSubmit) {
        var min = o.min === undefined ? 10 : o.min;
        var m = FA.modal({
            title: o.title, sub: o.sub, size: o.size,
            body: (o.message ? '<div class="fa-notice' + (o.warn ? ' fa-notice--warn' : '') + '">' + FA.esc(o.message) + '</div>' : '') + (o.extra || '') +
                  '<div class="fa-field" style="margin-top:12px"><label class="fa-label" for="faReasonText">' + FA.esc(o.label || 'Reason') + ' <span class="fa-req">*</span></label>' +
                  '<textarea id="faReasonText" class="fa-textarea" rows="3" maxlength="1000"></textarea>' +
                  '<span class="fa-hint" id="faReasonHint"></span></div><div class="fa-error" id="faReasonErr" style="margin-top:8px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button>' +
                  '<button type="button" class="fa-btn ' + (o.danger ? 'fa-btn--danger' : 'fa-btn--primary') + '" data-ok disabled>' + FA.esc(o.ok || 'Save') + '</button>'
        });
        var ta = FA.qs('faReasonText'), ok = m.foot.querySelector('[data-ok]'), hint = FA.qs('faReasonHint');
        ta.value = o.initial || '';
        function grade() {
            var n = ta.value.trim().length;
            ok.disabled = n < min;
            hint.textContent = n < min ? (min - n) + ' more character' + (min - n === 1 ? '' : 's') + ' needed.' : 'This will be recorded against your name.';
        }
        ta.addEventListener('input', grade); grade();
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        ok.onclick = function () {
            ok.disabled = true; ok.textContent = 'Saving';
            FA.qs('faReasonErr').textContent = '';
            onSubmit(ta.value.trim(), m, function (err) {
                if (err) { FA.qs('faReasonErr').textContent = err; ok.disabled = false; ok.textContent = o.ok || 'Save'; }
                else m.close();
            });
        };
        setTimeout(function () { ta.focus(); }, 30);
        return m;
    };

    // ── Selects, pager, URL state ─────────────────────────────────────
    FA.fill = function (sel, items, o) {
        o = o || {};
        if (typeof sel === 'string') sel = FA.qs(sel);
        if (!sel) return;
        var keep = o.value !== undefined ? String(o.value) : sel.value;
        var h = o.all !== undefined ? '<option value="">' + FA.esc(o.all) + '</option>' : '';
        (items || []).forEach(function (it) {
            var v = it[o.key || 'id'], t = it[o.text || 'name'];
            h += '<option value="' + FA.esc(v) + '">' + FA.esc(t) + '</option>';
        });
        sel.innerHTML = h;
        if (keep !== undefined) sel.value = keep;
        if (sel.value !== keep && o.all !== undefined) sel.value = '';
    };

    FA.pager = function (el, p, onGo) {
        if (typeof el === 'string') el = FA.qs(el);
        var pages = Math.max(1, Math.ceil((p.total || 0) / (p.size || 50)));
        var cur = Math.min(Math.max(1, p.page || 1), pages);
        var from = p.total === 0 ? 0 : (cur - 1) * p.size + 1, to = Math.min(p.total, cur * p.size);
        var h = '<span class="fa-muted">' + FA.money(from) + ' to ' + FA.money(to) + ' of ' + FA.money(p.total) + '</span><span class="fa-spacer"></span>';
        h += '<button type="button" data-p="' + (cur - 1) + '"' + (cur <= 1 ? ' disabled' : '') + '>Previous</button>';
        var start = Math.max(1, cur - 2), end = Math.min(pages, start + 4); start = Math.max(1, end - 4);
        for (var i = start; i <= end; i++) h += '<button type="button" data-p="' + i + '"' + (i === cur ? ' class="is-on"' : '') + '>' + i + '</button>';
        h += '<button type="button" data-p="' + (cur + 1) + '"' + (cur >= pages ? ' disabled' : '') + '>Next</button>';
        el.innerHTML = h;
        el.className = 'fa-pager';
        Array.prototype.forEach.call(el.querySelectorAll('button[data-p]'), function (b) {
            b.onclick = function () { onGo(parseInt(b.getAttribute('data-p'), 10)); };
        });
    };

    FA.readUrl = function () {
        var o = {}, q = location.search.replace(/^\?/, '');
        if (!q) return o;
        q.split('&').forEach(function (kv) {
            var p = kv.split('='); if (!p[0]) return;
            o[decodeURIComponent(p[0])] = decodeURIComponent((p[1] || '').replace(/\+/g, ' '));
        });
        return o;
    };
    FA.writeUrl = function (o) {
        var parts = [];
        Object.keys(o).forEach(function (k) { var v = o[k]; if (v !== '' && v !== null && v !== undefined && v !== 0 && v !== '0') parts.push(encodeURIComponent(k) + '=' + encodeURIComponent(v)); });
        var url = location.pathname + (parts.length ? '?' + parts.join('&') : '');
        try { history.replaceState(null, '', url); } catch (e) { }
    };

    // ── Typeahead (staff, suppliers) ──────────────────────────────────
    /** o: { source(q, cb), render(item) -> html, pickedText(item), onPick(item|null), free: bool, placeholder, value:{id,name} } */
    FA.typeahead = function (host, o) {
        if (typeof host === 'string') host = FA.qs(host);
        host.className = (host.className + ' fa-ta').trim();
        var state = { item: o.value || null };
        function draw() {
            if (state.item && !o.free) {
                host.innerHTML = '<div class="fa-ta__picked"><span>' + FA.esc(o.pickedText ? o.pickedText(state.item) : state.item.name) + '</span>' +
                    '<button type="button" aria-label="Clear">' + FA.icon('x', 14) + '</button></div>';
                host.querySelector('button').onclick = function () { state.item = null; draw(); if (o.onPick) o.onPick(null); host.querySelector('input').focus(); };
                return;
            }
            host.innerHTML = '<input type="text" class="fa-input" autocomplete="off" placeholder="' + FA.esc(o.placeholder || 'Type to search') + '"/><div class="fa-ta__list"></div>';
            var inp = host.querySelector('input'), list = host.querySelector('.fa-ta__list');
            if (o.free && state.item) inp.value = state.item.name || '';
            var items = [], hot = -1;
            var search = FA.debounce(function () {
                var q = inp.value.trim();
                if (o.free) { state.item = q ? { id: 0, name: q } : null; if (o.onPick) o.onPick(state.item); }
                if (q.length < 2) { list.className = 'fa-ta__list'; return; }
                o.source(q, function (res) {
                    items = res || []; hot = -1;
                    list.innerHTML = items.length ? items.map(function (it, i) { return '<div class="fa-ta__item" data-i="' + i + '">' + (o.render ? o.render(it) : FA.esc(it.name)) + '</div>'; }).join('')
                                                  : '<div class="fa-ta__item fa-muted">No match' + (o.free ? '. The text you typed will be kept.' : '') + '</div>';
                    list.className = 'fa-ta__list is-open';
                    Array.prototype.forEach.call(list.querySelectorAll('[data-i]'), function (d) {
                        d.onmousedown = function (e) { e.preventDefault(); pick(items[parseInt(d.getAttribute('data-i'), 10)]); };
                    });
                });
            }, 250);
            function pick(it) { state.item = it; list.className = 'fa-ta__list'; if (o.free) inp.value = it.name; else draw(); if (o.onPick) o.onPick(it); }
            inp.addEventListener('input', search);
            inp.addEventListener('blur', function () { setTimeout(function () { list.className = 'fa-ta__list'; }, 150); });
            inp.addEventListener('keydown', function (e) {
                var n = list.querySelectorAll('[data-i]');
                if (e.key === 'ArrowDown' && n.length) { hot = Math.min(n.length - 1, hot + 1); e.preventDefault(); }
                else if (e.key === 'ArrowUp' && n.length) { hot = Math.max(0, hot - 1); e.preventDefault(); }
                else if (e.key === 'Enter' && hot >= 0 && items[hot]) { e.preventDefault(); pick(items[hot]); return; }
                Array.prototype.forEach.call(n, function (d, i) { d.className = 'fa-ta__item' + (i === hot ? ' is-hot' : ''); });
            });
        }
        draw();
        return { get: function () { return state.item; }, set: function (it) { state.item = it; draw(); } };
    };

    FA.staffSource = function (q, cb) { FA.call('Assets.aspx', 'SearchStaff', { q: q }, function (r) { cb(r.success ? r.rows : []); }); };
    FA.staffRender = function (s) { return FA.esc(s.name) + '<small>' + FA.esc([s.code, s.department].filter(Boolean).join(', ')) + '</small>'; };
    FA.supplierSource = function (q, cb) { FA.call('Assets.aspx', 'SearchSuppliers', { q: q }, function (r) { cb(r.success ? r.rows : []); }); };

    // ── Export dialog ─────────────────────────────────────────────────
    /** o: { page, report, title, cfg (object), cols:[{k,t,on}], groups:[{k,t}], group, countMethod, note } */
    FA.exportDialog = function (o) {
        var colsHtml = (o.cols || []).map(function (c) {
            return '<label class="fa-check-line"><input type="checkbox" value="' + FA.esc(c.k) + '"' + (c.on ? ' checked' : '') + '/> ' + FA.esc(c.t) + '</label>';
        }).join('');
        var groupsHtml = (o.groups && o.groups.length > 1)
            ? '<div class="fa-field" style="margin-top:14px"><label class="fa-label" for="faXGroup">Group by</label><select id="faXGroup" class="fa-select">' +
              o.groups.map(function (g) { return '<option value="' + FA.esc(g.k) + '"' + (g.k === o.group ? ' selected' : '') + '>' + FA.esc(g.t) + '</option>'; }).join('') + '</select></div>' : '';
        var m = FA.modal({
            title: 'Export', sub: o.title, size: 'mid',
            body: '<div class="fa-field"><span class="fa-label">Format</span><div class="fa-radios">' +
                  '<label><input type="radio" name="faXFmt" value="pdf" checked/> PDF document</label>' +
                  '<label><input type="radio" name="faXFmt" value="xls"/> Excel workbook</label>' +
                  '<label><input type="radio" name="faXFmt" value="csv"/> CSV</label></div></div>' + groupsHtml +
                  (colsHtml ? '<div class="fa-section" style="margin-top:16px">Columns</div><div class="fa-cols" id="faXCols">' + colsHtml + '</div>' : '') +
                  (o.note ? '<div class="fa-notice" style="margin-top:14px">' + FA.esc(o.note) + '</div>' : '') +
                  '<div class="fa-hint" id="faXCount" style="margin-top:12px">Counting rows</div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>' + FA.icon('download') + ' Download</button>'
        });
        function group() { var g = FA.qs('faXGroup'); return g ? g.value : (o.group || ''); }
        function count() {
            if (!o.countMethod) { FA.qs('faXCount').textContent = ''; return; }
            FA.call(o.page, o.countMethod, { report: o.report || '', configJson: JSON.stringify(o.cfg || {}), groupBy: group() }, function (r) {
                var el = FA.qs('faXCount'); if (!el) return;
                el.textContent = r.success ? FA.plural(r.count, 'row', 'rows') + ' will be exported.' : (r.message || '');
            });
        }
        count();
        if (FA.qs('faXGroup')) FA.qs('faXGroup').onchange = count;
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        m.foot.querySelector('[data-ok]').onclick = function () {
            var fmt = (m.body.querySelector('input[name=faXFmt]:checked') || {}).value || 'pdf';
            var cols = Array.prototype.map.call(m.body.querySelectorAll('#faXCols input:checked'), function (i) { return i.value; });
            FA.download(o.page, { faReport: o.report || '', faFormat: fmt, faConfig: JSON.stringify(o.cfg || {}), faCols: cols.join(','), faGroup: group() });
            FA.toast('Preparing the file. It will download in a moment.');
            m.close();
        };
    };

    /** Posts a hidden form so the server can stream a file back. */
    FA.download = function (page, fields) {
        var f = document.createElement('form');
        f.method = 'POST'; f.action = page; f.style.display = 'none';
        fields.__csrf = FA.token();
        Object.keys(fields).forEach(function (k) {
            var i = document.createElement('input'); i.type = 'hidden'; i.name = k; i.value = fields[k] == null ? '' : fields[k]; f.appendChild(i);
        });
        document.body.appendChild(f); f.submit();
        setTimeout(function () { if (f.parentNode) f.parentNode.removeChild(f); }, 4000);
    };

    // ── Small form helpers ────────────────────────────────────────────
    FA.val = function (id) { var e = FA.qs(id); return e ? (e.type === 'checkbox' ? e.checked : String(e.value || '').trim()) : ''; };
    FA.set = function (id, v) { var e = FA.qs(id); if (!e) return; if (e.type === 'checkbox') e.checked = !!v; else e.value = v == null ? '' : v; };
    FA.radio = function (name) { var r = document.querySelector('input[name="' + name + '"]:checked'); return r ? r.value : ''; };
    FA.busy = function (btn, on, label) {
        if (!btn) return;
        if (on) { btn.setAttribute('data-label', btn.innerHTML); btn.disabled = true; btn.textContent = label || 'Saving'; }
        else { btn.disabled = false; if (btn.getAttribute('data-label')) btn.innerHTML = btn.getAttribute('data-label'); }
    };

    window.FA = FA;
})();
