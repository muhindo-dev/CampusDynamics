/* General Ledger: Accounts Reports screen. Parameters come from each report's
   definition; values come from the address, then the last run, then defaults. */
(function () {
    'use strict';
    var FA = window.FA, GL = window.GL, BOOT = window.GL_BOOT || {}, PAGE = 'AccountsReports.aspx';
    var reports = BOOT.reports || [], lastUsed = BOOT.lastUsed || {}, saved = BOOT.saved || [];
    var cur = null, acct = null, state = { page: 1, size: 50, sort: -1, desc: false, search: '' }, lastRes = null;

    if (BOOT.error) { FA.qs('rErr').textContent = BOOT.error; }

    // ── Report list ──────────────────────────────────────────────────
    function drawList() {
        var groups = [], h = '';
        reports.forEach(function (r) { if (groups.indexOf(r.group) < 0) groups.push(r.group); });
        groups.forEach(function (g) {
            h += '<div class="gl-group">' + FA.esc(g) + '</div><ul class="fa-reports">';
            reports.filter(function (r) { return r.group === g; }).forEach(function (r) {
                h += '<li><button type="button" data-r="' + r.code + '"' + (cur && cur.code === r.code ? ' class="is-on"' : '') + '><b><span class="gl-code">' + r.code + '</span> ' + FA.esc(r.title) + '</b><span>' + FA.esc(r.description) + '</span></button></li>';
            });
            h += '</ul>';
        });
        FA.qs('rList').innerHTML = h;
        Array.prototype.forEach.call(FA.qs('rList').querySelectorAll('[data-r]'), function (b) { b.onclick = function () { choose(b.getAttribute('data-r'), null, false); }; });
    }

    function find(code) { for (var i = 0; i < reports.length; i++) if (reports[i].code === code) return reports[i]; return null; }

    // ── Parameter form ───────────────────────────────────────────────
    function choose(code, values, run) {
        cur = find(code);
        if (!cur) return;
        state = { page: 1, size: state.size || 50, sort: -1, desc: false, search: '' };
        drawList();
        FA.qs('rTitle').textContent = cur.code + ' ' + cur.title;
        FA.qs('rDesc').textContent = cur.description;
        FA.qs('rErr').textContent = '';
        FA.qs('rOutCard').style.display = 'none'; FA.qs('rChecks').innerHTML = ''; FA.qs('rExport').disabled = true; lastRes = null;
        var v = values || lastUsed[code] || {};
        var h = '';
        cur.params.forEach(function (p) {
            var id = 'p_' + p.key, val = v[p.key] !== undefined && v[p.key] !== null ? String(v[p.key]) : p.dflt;
            if (p.role === 'year' && (values || lastUsed[code]) && v[p.key] === undefined) val = '';
            h += '<div class="fa-field' + (p.type === 'account' ? ' fa-full' : '') + '"><label class="fa-label" for="' + id + '">' + FA.esc(p.label) + (p.required ? ' <span class="fa-req">*</span>' : '') + '</label>';
            if (p.type === 'select') h += '<select class="fa-select" id="' + id + '">' + (p.options || []).map(function (o) { return '<option value="' + FA.esc(o.v) + '"' + (String(o.v) === val ? ' selected' : '') + '>' + FA.esc(o.t) + '</option>'; }).join('') + '</select>';
            else if (p.type === 'date') h += '<input type="date" class="fa-input" id="' + id + '" value="' + FA.esc(val || '') + '"/>';
            else if (p.type === 'bool') h += '<label class="fa-check-line" style="height:32px"><input type="checkbox" id="' + id + '"' + (val === '1' || val === 'true' ? ' checked' : '') + '/> Yes</label>';
            else if (p.type === 'account') h += '<div id="' + id + '"></div>';
            else h += '<input type="text" class="fa-input" id="' + id + '" value="' + FA.esc(val || '') + '"/>';
            if (p.help) h += '<span class="fa-hint">' + FA.esc(p.help) + '</span>';
            h += '</div>';
        });
        FA.qs('rParams').innerHTML = h;
        acct = null;
        cur.params.forEach(function (p) {
            var el = FA.qs('p_' + p.key);
            if (p.type === 'account') {
                var key = v[p.key] || '';
                acct = FA.typeahead(el, {
                    placeholder: 'Type a code or name',
                    source: function (q, cb) { FA.call(PAGE, 'SearchAccounts', { q: q }, function (r) { cb(r.success ? r.rows : []); }); },
                    render: function (a) { return '<b>' + FA.esc(a.code) + '</b> ' + FA.esc(a.title) + '<small>' + FA.esc(a.kind + (a.used ? '' : ', never used')) + '</small>'; },
                    pickedText: function (a) { return a.name; },
                    value: key ? { id: key, name: v[p.key + '_name'] || key } : null
                });
            }
            if (p.role === 'year') {
                el.onchange = function () {
                    if (!el.value) return;
                    var parts = el.value.split('|');
                    if (FA.qs('p_from')) FA.qs('p_from').value = parts[0];
                    if (FA.qs('p_to')) FA.qs('p_to').value = parts[1] > FA.today() ? FA.today() : parts[1];
                };
                if (!values && !lastUsed[code] && el.value) el.onchange();
            }
            if (p.role === 'from' || p.role === 'to') el.addEventListener('change', function () { var y = cur.params.filter(function (q) { return q.role === 'year'; })[0]; if (y) FA.qs('p_' + y.key).value = ''; });
        });
        drawSaved();
        FA.qs('rLast').textContent = lastUsed[code] && !values ? 'Filled with the values you last used.' : '';
        if (run) go();
    }

    function values() {
        var o = {};
        cur.params.forEach(function (p) {
            if (p.type === 'account') { var a = acct ? acct.get() : null; o[p.key] = a ? a.id : ''; o[p.key + '_name'] = a ? a.name : ''; }
            else if (p.type === 'bool') o[p.key] = FA.qs('p_' + p.key).checked ? '1' : '0';
            else o[p.key] = FA.val('p_' + p.key);
        });
        return o;
    }

    function missing(v) {
        for (var i = 0; i < cur.params.length; i++) { var p = cur.params[i]; if (p.required && !v[p.key]) return p.label; }
        return null;
    }

    // ── Saved filters ────────────────────────────────────────────────
    function drawSaved() {
        var mine = saved.filter(function (s) { return cur && s.report === cur.code; });
        var sel = FA.qs('rSaved');
        sel.innerHTML = '<option value="">' + (mine.length ? 'Saved filters (' + mine.length + ')' : 'No saved filters') + '</option>' +
            mine.map(function (s) { return '<option value="' + s.id + '">' + FA.esc(s.name) + '</option>'; }).join('') +
            (mine.length ? '<option value="-1">Remove a saved filter</option>' : '');
        sel.onchange = function () {
            var id = parseInt(sel.value, 10);
            if (id === -1) { removeSaved(mine); sel.value = ''; return; }
            var s = mine.filter(function (x) { return x.id === id; })[0];
            if (s) { choose(cur.code, s.params || {}, true); FA.qs('rLast').textContent = 'Saved filter: ' + s.name; }
        };
        var can = BOOT.rights && BOOT.rights.reports;
        FA.qs('rSave').style.display = can ? '' : 'none';
    }

    function removeSaved(mine) {
        var m = FA.modal({ title: 'Remove a saved filter', body: '<div class="fa-field"><label class="fa-label" for="glRm">Filter</label><select class="fa-select" id="glRm">' + mine.map(function (s) { return '<option value="' + s.id + '">' + FA.esc(s.name) + '</option>'; }).join('') + '</select></div><div class="fa-error" id="glRmErr" style="margin-top:8px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--danger" data-ok>Remove</button>' });
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        m.foot.querySelector('[data-ok]').onclick = function () {
            FA.call(PAGE, 'RemoveFilter', { id: parseInt(FA.val('glRm'), 10) }, function (r) {
                if (!r.success) { FA.qs('glRmErr').textContent = r.message; return; }
                saved = r.saved; drawSaved(); m.close(); FA.toast('Filter removed.');
            });
        };
    }

    FA.qs('rSave').onclick = function () {
        if (!cur) return;
        var m = FA.modal({ title: 'Save these filters', sub: cur.code + ' ' + cur.title,
            body: '<div class="fa-field"><label class="fa-label" for="glSfName">Name <span class="fa-req">*</span></label><input class="fa-input" id="glSfName" maxlength="100" placeholder="For example: Monthly trial balance, chart only"/></div><div class="fa-error" id="glSfErr" style="margin-top:8px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Save</button>' });
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        m.foot.querySelector('[data-ok]').onclick = function () {
            FA.call(PAGE, 'SaveFilter', { report: cur.code, name: FA.val('glSfName'), paramsJson: JSON.stringify(values()) }, function (r) {
                if (!r.success) { FA.qs('glSfErr').textContent = r.message; return; }
                saved = r.saved; drawSaved(); m.close(); FA.toast('Filter saved. Choose it from the list to run it again.');
            });
        };
        setTimeout(function () { FA.qs('glSfName').focus(); }, 30);
    };

    // ── Running ──────────────────────────────────────────────────────
    function go() {
        if (!cur) return;
        var v = values(), miss = missing(v);
        FA.qs('rErr').textContent = miss ? 'Fill in ' + miss + '.' : '';
        if (miss) return;
        var btn = FA.qs('rRun'); FA.busy(btn, true, 'Running');
        var url = { r: cur.code, run: 1 };
        Object.keys(v).forEach(function (k) { if (!/_name$/.test(k)) url[k] = v[k]; });
        FA.writeUrl(url);
        FA.call(PAGE, 'RunReport', { report: cur.code, paramsJson: JSON.stringify(v), page: state.page, size: state.size, sort: state.sort, desc: state.desc, search: state.search }, function (r) {
            FA.busy(btn, false);
            if (!r.success) { FA.qs('rErr').textContent = r.message || 'The report could not be produced.'; return; }
            lastUsed[cur.code] = v; lastRes = r;
            FA.qs('rLast').textContent = '';
            FA.qs('rOutCard').style.display = '';
            FA.qs('rOutTitle').textContent = r.title;
            FA.qs('rOutSub').textContent = r.subtitle || '';
            GL.checks('rChecks', r.checks);
            GL.result('rOut', r, {
                sort: state.sort, desc: state.desc, search: state.search,
                onSort: function (i) { if (state.sort === i) state.desc = !state.desc; else { state.sort = i; state.desc = false; } state.page = 1; go(); },
                onSearch: function (q) { state.search = q; state.page = 1; go(); },
                onPage: function (p) { state.page = p; go(); },
                onSize: function (n) { state.size = n; state.page = 1; go(); }
            });
            FA.qs('rExport').disabled = false;
            var q = FA.qs('rOut').querySelector('[data-q]');
            if (q && state.search) { q.focus(); q.setSelectionRange(q.value.length, q.value.length); }
        });
    }

    FA.qs('rRun').onclick = function () { state.page = 1; go(); };

    FA.qs('rExport').onclick = function () {
        if (!cur || !lastRes) return;
        GL.exportDialog({
            page: PAGE, title: cur.code + ' ' + cur.title, total: lastRes.total,
            cols: lastRes.cols.map(function (c) { return { k: c.k, t: c.t }; }),
            fields: { glReport: cur.code, glParams: JSON.stringify(values()), glSearch: state.search, glSort: state.sort >= 0 ? state.sort : '', glDesc: state.desc ? '1' : '0' }
        });
    };

    FA.qs('rRefresh').onclick = function () {
        var b = FA.qs('rRefresh'); FA.busy(b, true, 'Reading the ledger');
        FA.call(PAGE, 'LoadFreshFigures', {}, function (r) {
            FA.busy(b, false);
            if (!r.success) { FA.toast(r.message, true); return; }
            FA.toast('Figures read again from the ledger (' + (r.ms / 1000).toFixed(1) + ' s).');
            if (lastRes) go();
        });
    };

    // ── Start ────────────────────────────────────────────────────────
    drawList();
    var u = FA.readUrl();
    if (u.r && find(u.r)) {
        var vals = {}, has = false;
        find(u.r).params.forEach(function (p) { if (u[p.key] !== undefined) { vals[p.key] = u[p.key]; has = true; } });
        if (u.account) vals.account_name = u.account;
        choose(u.r, has ? vals : null, u.run === '1');
    } else if (reports.length) choose(reports[0].code, null, false);
})();
