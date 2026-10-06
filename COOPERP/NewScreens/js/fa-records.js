/* Fixed Assets: Asset Records screen (ledger, depreciation runs, year locks). ES5. */
(function () {
    'use strict';
    var B = window.FA_BOOT || {}, R = B.rights || {}, PAGE = 'AssetRecords.aspx';
    var esc = FA.esc, money = FA.money, qs = FA.qs;
    var CATS = (B.tree || []);

    // ── Views ─────────────────────────────────────────────────────────
    var view = FA.readUrl().view || 'ledger';
    function show(v) {
        view = v;
        Array.prototype.forEach.call(document.querySelectorAll('#faViews [data-v]'), function (b) { b.className = 'fa-subtab' + (b.getAttribute('data-v') === v ? ' is-active' : ''); });
        qs('vLedger').style.display = v === 'ledger' ? '' : 'none';
        qs('vDep').style.display = v === 'dep' ? '' : 'none';
        qs('vLocks').style.display = v === 'locks' ? '' : 'none';
        if (v === 'ledger') loadLedger(); else if (v === 'dep') loadRuns(); else loadLocks();
    }
    Array.prototype.forEach.call(document.querySelectorAll('#faViews [data-v]'), function (b) {
        b.onclick = function () { show(b.getAttribute('data-v')); FA.writeUrl({ view: b.getAttribute('data-v') === 'ledger' ? '' : b.getAttribute('data-v') }); };
    });

    // ── Ledger ────────────────────────────────────────────────────────
    var lPage = 1;
    qs('lFrom').value = B.fyStart; qs('lTo').value = B.today;
    FA.fill('lCampus', B.campuses, { all: 'All campuses' });
    FA.fill('lCat', CATS, { all: 'All categories' });
    ['lFrom', 'lTo', 'lType', 'lCampus', 'lCat', 'lRev'].forEach(function (id) { qs(id).onchange = function () { lPage = 1; loadLedger(); }; });
    qs('lQ').addEventListener('input', FA.debounce(function () { lPage = 1; loadLedger(); }, 400));
    function lcfg() {
        return { from: FA.val('lFrom'), to: FA.val('lTo'), recordType: FA.val('lType'), campusId: FA.val('lCampus'), categoryId: FA.val('lCat'),
                 q: FA.val('lQ'), includeReversals: qs('lRev').checked, page: lPage };
    }
    function loadLedger() {
        qs('lBody').innerHTML = '<tr><td colspan="9" class="fa-loading">Loading</td></tr>';
        FA.call(PAGE, 'GetRecords', { configJson: JSON.stringify(lcfg()) }, function (r) {
            if (!r.success) { qs('lBody').innerHTML = '<tr><td colspan="9" class="fa-empty">' + esc(r.message) + '</td></tr>'; return; }
            qs('lCount').textContent = FA.plural(r.total, 'record', 'records');
            qs('lKpis').innerHTML =
                '<div class="fa-kpi"><div class="fa-kpi__label">Additions</div><div class="fa-kpi__value">' + money(r.totals.additions) + '</div><div class="fa-kpi__sub">Cost of assets added, UGX</div></div>' +
                '<div class="fa-kpi"><div class="fa-kpi__label">Depreciation</div><div class="fa-kpi__value">' + money(r.totals.depreciation) + '</div><div class="fa-kpi__sub">Charged in the period, UGX</div></div>' +
                '<div class="fa-kpi"><div class="fa-kpi__label">Revaluation</div><div class="fa-kpi__value">' + money(r.totals.revaluation) + '</div><div class="fa-kpi__sub">Net change, UGX</div></div>' +
                '<div class="fa-kpi"><div class="fa-kpi__label">Disposals</div><div class="fa-kpi__value">' + money(r.totals.disposals) + '</div><div class="fa-kpi__sub">Carrying amount removed, UGX</div></div>';
            qs('lBody').innerHTML = !r.rows.length ? '<tr><td colspan="9" class="fa-empty">No records in this period.</td></tr>' : r.rows.map(function (x) {
                var ch = x.valueRecord ? '<span class="' + (x.change > 0 ? 'fa-up' : x.change < 0 ? 'fa-down' : '') + '">' + (x.change > 0 ? '+' : '') + money(x.change) + '</span>' : '';
                return '<tr class="is-click' + (x.reversed ? ' is-reversed' : '') + '" data-a="' + x.assetId + '"><td style="white-space:nowrap">' + esc(x.date) + '<span class="fa-sub">' + esc(x.finYear) + '</span></td>' +
                    '<td><span class="fa-code">' + esc(x.assetNo) + '</span><span class="fa-sub">' + esc(x.name) + '</span></td>' +
                    '<td><strong>' + esc(x.typeLabel) + '</strong>' + (x.reversed ? '<span class="fa-sub">Reversed</span>' : '') + '</td><td>' + esc(x.details) + '</td>' +
                    '<td class="fa-num">' + (x.valueRecord ? money(x.before) : '') + '</td><td class="fa-num">' + ch + '</td><td class="fa-num">' + (x.valueRecord ? money(x.after) : '') + '</td>' +
                    '<td>' + esc(x.reason) + (x.reference ? '<span class="fa-sub">Ref ' + esc(x.reference) + '</span>' : '') + '</td><td>' + esc(x.by) + '</td></tr>';
            }).join('');
            Array.prototype.forEach.call(qs('lBody').querySelectorAll('tr[data-a]'), function (tr) {
                tr.onclick = function () { location.href = 'Assets.aspx?open=' + tr.getAttribute('data-a'); };
            });
            FA.pager('lPager', { page: r.page, size: r.size, total: r.total }, function (p) { lPage = p; loadLedger(); });
        });
    }
    qs('lExport').innerHTML = FA.icon('download') + ' Export';
    qs('lExport').onclick = function () {
        FA.exportDialog({ page: PAGE, report: 'ledger', title: 'Asset Records Ledger', cfg: lcfg(), cols: B.ledgerCols, countMethod: 'CountExport',
                          groups: [{ k: 'none', t: 'No grouping' }, { k: 'type', t: 'Record type' }, { k: 'category', t: 'Category' }], group: 'none' });
    };

    // ── Depreciation ──────────────────────────────────────────────────
    qs('dEnd').value = B.lastYearEnd; qs('dEnd').max = B.today;
    FA.fill('dCampus', B.campuses, { all: 'All campuses' });
    FA.fill('dCat', CATS, { all: 'All categories' });
    var preview = null;
    qs('dPreview').onclick = function () {
        var btn = this; FA.busy(btn, true, 'Working');
        qs('dResult').innerHTML = '<div class="fa-card"><div class="fa-loading">Working out the charges</div></div>';
        var args = { periodEnd: FA.val('dEnd'), campusId: FA.val('dCampus'), categoryId: FA.val('dCat') };
        FA.call(PAGE, 'PreviewDepreciation', { json: JSON.stringify(args) }, function (r) {
            FA.busy(btn, false);
            if (!r.success) { qs('dResult').innerHTML = '<div class="fa-notice fa-notice--bad">' + esc(r.message) + '</div>'; return; }
            preview = { args: args, r: r, op: FA.uuid() };
            renderPreview(r);
        });
    };
    function renderPreview(r) {
        var h = '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Preview to ' + esc(r.periodEnd) + ' <span class="fa-card__meta">' + FA.plural(r.count, 'asset', 'assets') + ' to charge</span></div>' +
            (r.count && R.value ? '<button type="button" class="fa-btn fa-btn--primary" id="dPost">Post UGX ' + money(r.total) + '</button>' : '') + '</div>';
        if (!r.count) h += '<div class="fa-empty">Nothing is due for this period.' + (r.skipped.length ? ' ' + FA.plural(r.skipped.length, 'asset was', 'assets were') + ' skipped; see below.' : '') + '</div>';
        else {
            h += '<div class="fa-card__body"><table class="fa-table"><thead><tr><th>Category</th><th class="fa-num">Assets</th><th class="fa-num">Charge (UGX)</th></tr></thead><tbody>' +
                r.byCategory.map(function (c) { return '<tr><td>' + esc(c.name) + '</td><td class="fa-num">' + money(c.count) + '</td><td class="fa-num">' + money(c.charge) + '</td></tr>'; }).join('') +
                '</tbody><tfoot><tr><td>Total</td><td class="fa-num">' + money(r.count) + '</td><td class="fa-num">' + money(r.total) + '</td></tr></tfoot></table></div>';
            h += '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Asset</th><th>Method</th><th>Financial year</th><th>Months</th><th class="fa-num">Before</th><th class="fa-num">Charge</th><th class="fa-num">After</th></tr></thead><tbody>' +
                r.rows.map(function (x) {
                    return '<tr><td><span class="fa-code">' + esc(x.assetNo) + '</span><span class="fa-sub">' + esc(x.name) + '</span></td><td>' + esc(x.method) + '</td><td>' + esc(x.finYear) + '</td>' +
                        '<td>' + esc(x.from) + ' to ' + esc(x.to) + '<span class="fa-sub">' + FA.plural(x.months, 'month', 'months') + '</span></td>' +
                        '<td class="fa-num">' + money(x.before) + '</td><td class="fa-num"><strong>' + money(x.charge) + '</strong></td><td class="fa-num">' + money(x.after) + '</td></tr>';
                }).join('') + '</tbody></table></div>' +
                (r.rowsTotal > r.rowsShown ? '<div class="fa-card__foot">Showing the first ' + money(r.rowsShown) + ' of ' + money(r.rowsTotal) + ' lines. The totals cover all of them.</div>' : '');
        }
        if (r.skipped.length) {
            h += '<details style="padding:10px 14px;border-top:1px solid #E0E5ED"><summary style="cursor:pointer;font-weight:600">' + FA.plural(r.skipped.length, 'asset not charged', 'assets not charged') + '</summary>' +
                 '<table class="fa-table" style="margin-top:8px"><tbody>' + r.skipped.slice(0, 300).map(function (s) { return '<tr><td class="fa-code">' + esc(s.assetNo) + '</td><td>' + esc(s.name) + '</td><td>' + esc(s.why) + '</td></tr>'; }).join('') + '</tbody></table></details>';
        }
        h += '</div>';
        qs('dResult').innerHTML = h;
        if (qs('dPost')) qs('dPost').onclick = post;
    }
    function post() {
        var r = preview.r;
        FA.confirm({ title: 'Post depreciation', message: 'Post depreciation of UGX ' + money(r.total) + ' on ' + FA.plural(r.count, 'asset', 'assets') + ' up to ' + r.periodEnd +
                     '?\nEach asset gets one record per financial year covered. A run can be reversed only while it is the newest.', ok: 'Post' }, function () {
            var b = qs('dPost'); FA.busy(b, true, 'Posting');
            var a = preview.args;
            FA.call(PAGE, 'PostDepreciation', { json: JSON.stringify({ periodEnd: a.periodEnd, campusId: a.campusId, categoryId: a.categoryId, previewHash: r.previewHash, clientOpId: preview.op }) }, function (x) {
                FA.busy(b, false);
                if (!x.success) { FA.toast(x.message, true); return; }
                FA.toast(x.message);
                qs('dResult').innerHTML = '';
                preview = null;
                loadRuns();
            });
        });
    }

    function loadRuns() {
        FA.call(PAGE, 'GetRuns', {}, function (r) {
            if (!r.success) { qs('dRuns').innerHTML = '<tr><td colspan="8" class="fa-empty">' + esc(r.message) + '</td></tr>'; return; }
            qs('dRuns').innerHTML = !r.rows.length ? '<tr><td colspan="8" class="fa-empty">No depreciation has been posted yet.</td></tr>' : r.rows.map(function (x) {
                return '<tr' + (x.status === 'REVERSED' ? ' class="is-reversed"' : '') + '><td><strong>' + x.id + '</strong></td><td>' + esc(x.periodEnd) + '<span class="fa-sub">' + esc(x.finYear) + '</span></td>' +
                    '<td>' + esc(x.scope) + '</td><td class="fa-num">' + money(x.assets) + '</td><td class="fa-num">' + money(x.total) + '</td>' +
                    '<td>' + esc(x.postedBy) + '<span class="fa-sub">' + esc(x.postedAt) + '</span></td>' +
                    '<td class="fa-keep">' + (x.status === 'POSTED' ? '<span class="fa-badge fa-badge--ok">Posted</span>' : '<span class="fa-badge fa-badge--neutral">Reversed</span><span class="fa-sub">' + esc(x.reverseReason) + '</span>') + '</td>' +
                    '<td class="fa-keep" style="white-space:nowrap">' + (x.status === 'POSTED' ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-j="' + x.id + '">Journal</button> ' : '') +
                    (x.canReverse && R.value ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-rv="' + x.id + '">Reverse</button>' : '') + '</td></tr>';
            }).join('');
            Array.prototype.forEach.call(qs('dRuns').querySelectorAll('[data-j]'), function (b) { b.onclick = function () { journal(parseInt(b.getAttribute('data-j'), 10), ''); }; });
            Array.prototype.forEach.call(qs('dRuns').querySelectorAll('[data-rv]'), function (b) {
                b.onclick = function () {
                    var id = parseInt(b.getAttribute('data-rv'), 10);
                    FA.reason({ title: 'Reverse run ' + id, message: 'Every charge in this run is reversed. The run and its charges stay on file, marked reversed.', warn: true, ok: 'Reverse run', danger: true },
                        function (reason, m, done) {
                            FA.call(PAGE, 'ReverseRun', { runId: id, reason: reason, clientOpId: FA.uuid() }, function (r) { if (!r.success) { done(r.message); return; } done(); FA.toast('Run ' + id + ' reversed.'); loadRuns(); });
                        });
                };
            });
        });
    }

    function journal(runId, finYear) {
        FA.call(PAGE, 'GetJournal', { runId: runId || 0, finYear: finYear || '' }, function (r) {
            if (!r.success) { FA.toast(r.message, true); return; }
            var dr = 0, cr = 0;
            r.lines.forEach(function (l) { dr += l.debit; cr += l.credit; });
            var m = FA.modal({
                title: 'Depreciation journal', sub: runId ? 'Run ' + runId : 'Financial year ' + finYear, size: 'mid',
                body: '<p class="fa-hint" style="font-size:12px;margin-top:0">Post this through Journal Entries. The register does not post to the general ledger itself.</p>' +
                      (r.lines.length ? '<table class="fa-table"><thead><tr><th>Account</th><th>Name</th><th class="fa-num">Debit</th><th class="fa-num">Credit</th></tr></thead><tbody>' +
                      r.lines.map(function (l) { return '<tr><td class="fa-code">' + esc(l.account) + '</td><td>' + esc(l.accountName) + '<span class="fa-sub">' + esc(l.note) + '</span></td><td class="fa-num">' + (l.debit ? money(l.debit) : '') + '</td><td class="fa-num">' + (l.credit ? money(l.credit) : '') + '</td></tr>'; }).join('') +
                      '</tbody><tfoot><tr><td colspan="2">Total</td><td class="fa-num">' + money(dr) + '</td><td class="fa-num">' + money(cr) + '</td></tr></tfoot></table>' : '<div class="fa-empty">No depreciation.</div>'),
                foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Close</button><button type="button" class="fa-btn fa-btn--primary" data-dl>' + FA.icon('download') + ' Download</button>'
            });
            m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
            m.foot.querySelector('[data-dl]').onclick = function () {
                FA.exportDialog({ page: PAGE, report: 'journal', title: 'Depreciation Journal Summary', cfg: { runId: runId || 0, finYear: finYear || '' }, countMethod: 'CountExport' });
            };
        });
    }
    qs('dJournalYear').textContent = 'Journal for ' + B.currentFinYear;
    qs('dJournalYear').onclick = function () {
        var m = FA.modal({ title: 'Journal for a financial year', size: '',
            body: '<div class="fa-field"><label class="fa-label" for="jFy">Financial year</label><select id="jFy" class="fa-select"></select></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>Show</button>' });
        var years = (B.finYears || []).slice(); if (years.indexOf(B.currentFinYear) < 0) years.unshift(B.currentFinYear);
        FA.fill('jFy', years.map(function (y) { return { id: y, name: y }; }), {});
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        m.foot.querySelector('[data-ok]').onclick = function () { var y = FA.val('jFy'); m.close(); journal(0, y); };
    };
    qs('dCheck').onclick = function () {
        var b = this; FA.busy(b, true, 'Checking');
        FA.call(PAGE, 'CheckIntegrity', {}, function (r) {
            FA.busy(b, false);
            if (!r.success) { FA.toast(r.message, true); return; }
            if (!r.problems.length) { FA.toast('Every asset agrees with its records.'); return; }
            FA.modal({ title: 'Register check', sub: FA.plural(r.problems.length, 'asset differs', 'assets differ') + ' from its records', size: 'mid',
                body: '<table class="fa-table"><tbody>' + r.problems.map(function (p) { return '<tr><td class="fa-code">' + esc(p.assetNo) + '</td><td>' + esc(p.problems) + '</td></tr>'; }).join('') + '</tbody></table><p class="fa-hint">Tell MIS. Nothing has been changed.</p>' });
        });
    };
    if (!R.value) qs('dCheck').style.display = 'none';

    // ── Locks ─────────────────────────────────────────────────────────
    function loadLocks() {
        FA.call(PAGE, 'GetLocks', {}, function (r) {
            if (!r.success) { qs('kBody').innerHTML = '<tr><td colspan="6" class="fa-empty">' + esc(r.message) + '</td></tr>'; return; }
            qs('kBody').innerHTML = r.rows.map(function (x) {
                return '<tr><td><strong>' + esc(x.finYear) + '</strong>' + (x.current ? '<span class="fa-sub">Current year</span>' : '') + '</td><td class="fa-num">' + money(x.records) + '</td>' +
                    '<td>' + (x.locked ? '<span class="fa-badge fa-badge--warn">Locked</span>' : '<span class="fa-badge fa-badge--ok">Open</span>') + '</td>' +
                    '<td>' + esc(x.locked ? x.lockedBy : x.unlockedBy) + '<span class="fa-sub">' + esc(x.locked ? x.lockedAt : x.unlockedAt) + '</span></td><td>' + esc(x.reason) + '</td>' +
                    '<td>' + (R.yearLock ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-y="' + esc(x.finYear) + '" data-l="' + (x.locked ? 0 : 1) + '">' + (x.locked ? FA.icon('unlock') + ' Unlock' : FA.icon('lock') + ' Lock') + '</button>' : '') + '</td></tr>';
            }).join('');
            Array.prototype.forEach.call(qs('kBody').querySelectorAll('[data-y]'), function (b) {
                b.onclick = function () {
                    var y = b.getAttribute('data-y'), lock = b.getAttribute('data-l') === '1';
                    FA.reason({ title: (lock ? 'Lock ' : 'Unlock ') + y, message: lock ? 'No record can be dated in, posted into or reversed from ' + y + ' while it is locked.' : 'Records can again be dated in ' + y + '.', warn: !lock, ok: lock ? 'Lock year' : 'Unlock year', danger: !lock },
                        function (reason, m, done) {
                            FA.call(PAGE, 'SetLock', { finYear: y, locked: lock, reason: reason }, function (r) { if (!r.success) { done(r.message); return; } done(); FA.toast(y + (lock ? ' locked.' : ' unlocked.')); loadLocks(); });
                        });
                };
            });
        });
    }

    show(view);
})();
