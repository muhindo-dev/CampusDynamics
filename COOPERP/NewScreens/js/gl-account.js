/* General Ledger: account card. */
(function () {
    'use strict';
    var FA = window.FA, GL = window.GL, B = window.GL_BOOT || {}, PAGE = 'AccountsAccount.aspx';
    if (B.error) { FA.qs('aErr').innerHTML = '<div class="fa-notice fa-notice--bad">' + FA.esc(B.error) + ' <a class="gl-link" href="AccountsReports.aspx?r=R20&run=1">Open the chart of accounts</a></div>'; return; }
    var a = B.account, st = { tab: 'statement', page: 1, size: 50, search: '', sort: -1, desc: false };
    FA.qs('aMain').style.display = '';

    // Header and facts
    FA.qs('aTitle').innerHTML = '<span class="fa-code" style="font-size:14px">' + FA.esc(a.code) + '</span> ' + FA.esc(a.name) + (B.member ? ' <span class="fa-muted">/ ' + FA.esc(B.member) + (B.memberName ? ' ' + FA.esc(B.memberName) : '') + '</span>' : '');
    FA.qs('aSub').textContent = a.kindText + (a.contra ? '. Contra account: a credit balance is expected.' : '');
    var facts = [['Category', a.category], ['Sub-category', a.subcategory || 'None'], ['Normal balance', a.category === 'Unclassified' || a.category === 'Suspense' ? 'Not classified' : (a.contra ? 'Credit (contra)' : a.debitNatural ? 'Debit' : 'Credit')]];
    if (a.kind === 'SUBLEDGER') facts.push(['Control account', a.control ? a.control + ' ' + a.controlName : 'None']);
    else facts.push(['Main account', a.main ? a.main + ' ' + a.mainName : 'Not in the chart']);
    FA.qs('aFacts').innerHTML = facts.map(function (f) { return '<div><dt>' + FA.esc(f[0]) + '</dt><dd>' + (f[0] === 'Control account' && a.control ? '<a class="gl-link" href="AccountsAccount.aspx?code=' + encodeURIComponent(a.control) + '">' + FA.esc(f[1]) + '</a>' : FA.esc(f[1])) + '</dd></div>'; }).join('');

    // Period
    FA.fill('aYear', [{ v: '', t: 'Custom dates' }].concat(B.years), { key: 'v', text: 't' });
    FA.qs('aFrom').value = B.from; FA.qs('aTo').value = B.to;
    var match = B.years.filter(function (y) { return y.v.split('|')[0] === B.from; })[0];
    FA.qs('aYear').value = match ? match.v : '';
    FA.qs('aYear').onchange = function () { var v = FA.qs('aYear').value; if (!v) return; var p = v.split('|'); FA.qs('aFrom').value = p[0]; FA.qs('aTo').value = p[1] > FA.today() ? FA.today() : p[1]; };
    FA.qs('aGo').onclick = function () {
        location.href = PAGE + '?code=' + encodeURIComponent(a.key) + (B.member ? '&m=' + encodeURIComponent(B.member) : '') + '&from=' + FA.val('aFrom') + '&to=' + FA.val('aTo');
    };

    // Summary
    var s = B.summary;
    FA.qs('aSummary').innerHTML = [['Opening ' + fmtDate(B.from), s.opening], ['Debit', s.dr], ['Credit', s.cr], ['Closing ' + fmtDate(B.to), s.closing + (s.side ? ' <small style="font-size:11px;font-weight:400">' + s.side + '</small>' : '')], ['Lines', s.lines]]
        .map(function (x) { return '<div><span>' + FA.esc(x[0]) + '</span><b>' + x[1] + '</b></div>'; }).join('');

    function fmtDate(iso) { var d = new Date(iso + 'T00:00:00'); return isNaN(d) ? iso : d.getDate() + ' ' + ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][d.getMonth()] + ' ' + d.getFullYear(); }

    // Mapping (codes missing from the chart)
    if (a.kind === 'PROVISIONAL' || a.kind === 'UNMAPPED') {
        var m = B.mapping, can = B.rights && B.rights.manage;
        var h = '<div class="fa-notice ' + (m && !m.provisional ? 'fa-notice--ok' : 'fa-notice--warn') + '"><b>' + FA.esc(a.code) + ' is not in the chart of accounts.</b> ';
        if (m) h += (m.provisional ? 'It is shown under a provisional mapping: ' : 'Its mapping was confirmed by ' + FA.esc(m.confirmedBy) + ' on ' + FA.esc(m.confirmedAt) + ': ') + '<b>' + FA.esc(m.category) + (m.subcategory ? ', ' + FA.esc(m.subcategory) : '') + '</b>, named "' + FA.esc(m.name) + '". ' + (m.basis ? '<span class="fa-muted">Basis: ' + FA.esc(m.basis) + '</span> ' : '');
        else h += 'It is not mapped, so every statement shows it as Unclassified. ';
        h += 'The mapping changes only how reports present it; the chart itself is not changed.';
        if (can) h += ' <button type="button" class="fa-btn fa-btn--link" id="aMapBtn">' + (m && m.provisional ? 'Confirm or change the mapping' : 'Change the mapping') + '</button>';
        h += '</div>';
        FA.qs('aMapping').innerHTML = h;
        if (can) FA.qs('aMapBtn').onclick = mapDialog;
    }

    function mapDialog() {
        var m = B.mapping || { name: a.name, category: 'Unclassified', subcategory: '' };
        var subs = B.subcategories.map(function (x) { return x.s; }).filter(function (x, i, arr) { return x && arr.indexOf(x) === i; });
        var dlg = FA.modal({
            title: 'Mapping for ' + a.code, sub: 'How reports present a code missing from the chart', size: 'mid',
            body: '<div class="fa-form"><div class="fa-field fa-full"><label class="fa-label" for="mpName">Name <span class="fa-req">*</span></label><input class="fa-input" id="mpName" maxlength="150"/></div>' +
                '<div class="fa-field"><label class="fa-label" for="mpCat">Category <span class="fa-req">*</span></label><select class="fa-select" id="mpCat">' + B.categories.map(function (c) { return '<option>' + c + '</option>'; }).join('') + '</select></div>' +
                '<div class="fa-field"><label class="fa-label" for="mpSub">Sub-category</label><input class="fa-input" id="mpSub" list="mpSubs" maxlength="100"/><datalist id="mpSubs">' + subs.map(function (x) { return '<option value="' + FA.esc(x) + '">'; }).join('') + '</datalist></div>' +
                '<div class="fa-field fa-full"><label class="fa-check-line"><input type="checkbox" id="mpConfirm" checked/> Confirm this mapping (it is then no longer marked provisional)</label></div>' +
                '<div class="fa-field fa-full"><label class="fa-label" for="mpWhy">Reason <span class="fa-req">*</span></label><textarea class="fa-textarea" id="mpWhy" rows="3" maxlength="1000" placeholder="For example: AC6007 receives Functional Fees from billing item 52 since July 2024."></textarea></div></div>' +
                '<div class="fa-notice" style="margin-top:12px">Adding the code to the chart itself is a separate decision (open question Q2). This changes only how the General Ledger reports classify it, and it is recorded in the audit trail.</div><div class="fa-error" id="mpErr" style="margin-top:8px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Save mapping</button>'
        });
        FA.set('mpName', m.name); FA.set('mpCat', m.category); FA.set('mpSub', m.subcategory);
        dlg.foot.querySelector('[data-x]').onclick = function () { dlg.close(); };
        var ok = dlg.foot.querySelector('[data-ok]');
        ok.onclick = function () {
            FA.busy(ok, true);
            FA.call(PAGE, 'SaveMapping', { code: a.code, name: FA.val('mpName'), category: FA.val('mpCat'), subcategory: FA.val('mpSub'), confirm: FA.qs('mpConfirm').checked, reason: FA.val('mpWhy') }, function (r) {
                FA.busy(ok, false);
                if (!r.success) { FA.qs('mpErr').textContent = r.message; return; }
                FA.toast(r.message); dlg.close(); setTimeout(function () { location.reload(); }, 700);
            });
        };
    }

    // Warnings on the account
    if (B.warnings && B.warnings.length) {
        FA.qs('aWarnings').innerHTML = '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Finance Warnings on this account</div></div><div class="fa-card__body" style="padding:6px 14px">' +
            B.warnings.map(function (w) { return '<div class="fa-row" style="padding:6px 0;border-bottom:1px solid var(--fa-border)">' + GL.sev(w.severity) + ' <a class="gl-link" href="AccountsWarnings.aspx?id=' + w.id + '">' + FA.esc(w.rule + ' ' + w.title) + '</a><span class="fa-spacer"></span>' + GL.status(w.status) + '<b style="margin-left:10px">' + FA.esc(w.amount) + '</b></div>'; }).join('') + '</div></div>';
    }

    // Tabs: statement, months, members
    var tabs = [['statement', 'Statement'], ['months', 'By month']];
    if (a.kind === 'SUBLEDGER' && !B.member) tabs.push(['members', 'Members']);
    function drawTabs() {
        FA.qs('aTabs').innerHTML = tabs.map(function (t) { return '<button type="button" class="fa-subtab' + (st.tab === t[0] ? ' is-active' : '') + '" data-t="' + t[0] + '">' + t[1] + '</button>'; }).join('') +
            '<span class="fa-spacer"></span><a class="fa-btn fa-btn--secondary fa-btn--sm" href="AccountsReports.aspx?r=R02&account=' + encodeURIComponent(a.key) + (B.member ? '&member=' + encodeURIComponent(B.member) : '') + '&from=' + B.from + '&to=' + B.to + '&run=1">' + FA.icon('download') + ' Export the statement</a>';
        FA.qs('aTabs').className = 'fa-subtabs fa-row';
        Array.prototype.forEach.call(FA.qs('aTabs').querySelectorAll('[data-t]'), function (b) { b.onclick = function () { st.tab = b.getAttribute('data-t'); st.page = 1; st.search = ''; st.sort = -1; drawTabs(); load(); }; });
    }

    function load() {
        var body = FA.qs('aPanelBody');
        if (st.tab === 'months') {
            if (!B.months.length) { body.innerHTML = '<div class="fa-empty">No movement in the period.</div>'; return; }
            body.innerHTML = '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Month</th><th class="fa-num">Lines</th><th class="fa-num">Debit</th><th class="fa-num">Credit</th><th class="fa-num">Net</th><th class="fa-num">Balance at month end</th></tr></thead><tbody>' +
                B.months.map(function (m) { return '<tr class="is-click" data-f="' + m.from + '" data-t="' + m.to + '"><td>' + m.m + '</td><td class="fa-num">' + m.n + '</td><td class="fa-num">' + m.dr + '</td><td class="fa-num">' + m.cr + '</td><td class="fa-num">' + m.net + '</td><td class="fa-num">' + m.bal + '</td></tr>'; }).join('') + '</tbody></table></div>';
            Array.prototype.forEach.call(body.querySelectorAll('tr[data-f]'), function (tr) {
                tr.onclick = function () { location.href = PAGE + '?code=' + encodeURIComponent(a.key) + (B.member ? '&m=' + encodeURIComponent(B.member) : '') + '&from=' + tr.getAttribute('data-f') + '&to=' + tr.getAttribute('data-t'); };
            });
            return;
        }
        body.innerHTML = '<div class="fa-loading">Loading</div>';
        var method = st.tab === 'members' ? 'LoadMembers' : 'LoadStatement';
        var args = st.tab === 'members' ? { code: a.key, to: B.to, page: st.page, size: st.size, sort: st.sort, desc: st.desc, search: st.search }
                                         : { code: a.key, member: B.member || '', from: B.from, to: B.to, page: st.page, size: st.size, search: st.search };
        FA.call(PAGE, method, args, function (r) {
            if (!r.success) { body.innerHTML = '<div class="fa-empty">' + FA.esc(r.message) + '</div>'; return; }
            GL.result(body, r, {
                sort: st.sort, desc: st.desc, search: st.search,
                onSort: function (i) { if (st.sort === i) st.desc = !st.desc; else { st.sort = i; st.desc = true; } st.page = 1; load(); },
                onSearch: function (q) { st.search = q; st.page = 1; load(); },
                onPage: function (p) { st.page = p; load(); },
                onSize: function (n) { st.size = n; st.page = 1; load(); }
            });
            if (st.tab === 'statement' && r.checks && r.checks.length) {
                var holder = document.createElement('div'); holder.style.padding = '10px 14px 0';
                body.insertBefore(holder, body.firstChild); GL.checks(holder, r.checks, { collapsed: true });
            }
            var q = body.querySelector('[data-q]'); if (q && st.search) { q.focus(); q.setSelectionRange(q.value.length, q.value.length); }
        });
    }

    drawTabs(); load();
})();
