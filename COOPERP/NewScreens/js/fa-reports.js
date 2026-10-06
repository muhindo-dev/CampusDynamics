/* Fixed Assets: Reports screen. Choose a report, set its filters, preview it, export it. ES5. */
(function () {
    'use strict';
    var B = window.FA_BOOT || {}, PAGE = 'AssetReports.aspx', esc = FA.esc, qs = FA.qs;
    var REPORTS = B.reports || [], CATS = B.tree || [], cur = null, pickers = {};

    qs('rList').innerHTML = REPORTS.map(function (r) {
        return '<li><button type="button" data-k="' + esc(r.key) + '"><b>' + esc(r.title) + '</b><span>' + esc(r.description) + '</span></button></li>';
    }).join('');
    Array.prototype.forEach.call(qs('rList').querySelectorAll('[data-k]'), function (b) { b.onclick = function () { choose(b.getAttribute('data-k')); }; });
    qs('rExport').innerHTML = FA.icon('download') + ' Export';

    function field(id, label, input, hint) {
        return '<div class="fa-field"><label class="fa-label" for="' + id + '">' + label + '</label>' + input + (hint ? '<span class="fa-hint">' + hint + '</span>' : '') + '</div>';
    }

    function choose(key) {
        cur = null; REPORTS.forEach(function (r) { if (r.key === key) cur = r; });
        if (!cur) return;
        FA.writeUrl({ report: key });
        Array.prototype.forEach.call(qs('rList').querySelectorAll('[data-k]'), function (b) { b.className = b.getAttribute('data-k') === key ? 'is-on' : ''; });
        qs('rTitle').textContent = cur.title; qs('rDesc').textContent = cur.description;
        qs('rOut').innerHTML = ''; qs('rErr').textContent = '';
        var f = cur.filters, h = '';
        if (f.indexOf('asset') >= 0) h += '<div class="fa-field fa-full"><label class="fa-label">Asset</label><div id="pAsset"></div></div>';
        if (f.indexOf('finYear') >= 0) h += field('pFy', 'Financial year', '<select id="pFy" class="fa-select"></select>');
        if (f.indexOf('run') >= 0) h += field('pRun', 'Or one depreciation run', '<select id="pRun" class="fa-select"></select>');
        if (f.indexOf('period') >= 0) h += field('pFrom', 'From', '<input type="date" id="pFrom" class="fa-input" value="' + B.fyStart + '"/>') + field('pTo', 'To', '<input type="date" id="pTo" class="fa-input" value="' + B.today + '"/>');
        if (f.indexOf('asOf') >= 0) h += field('pAsOf', 'As at', '<input type="date" id="pAsOf" class="fa-input" value="' + B.today + '" max="' + B.today + '"/>');
        if (f.indexOf('campus') >= 0) h += field('pCampus', 'Campus', '<select id="pCampus" class="fa-select"></select>');
        if (f.indexOf('category') >= 0) h += field('pCat', 'Category', '<select id="pCat" class="fa-select"></select>') + field('pSub', 'Sub-category', '<select id="pSub" class="fa-select"></select>');
        if (f.indexOf('department') >= 0) h += field('pDept', 'Department', '<select id="pDept" class="fa-select"></select>');
        if (f.indexOf('custodian') >= 0) h += '<div class="fa-field"><label class="fa-label">Responsible person</label><div id="pCus"></div></div>';
        if (f.indexOf('status') >= 0) h += field('pStatus', 'Status', '<select id="pStatus" class="fa-select"><option value="OPEN">Still held</option><option value="">All except void</option><option value="IN_USE">In use</option><option value="IN_STORE">In store</option><option value="UNDER_REPAIR">Under repair</option><option value="LOST">Lost</option><option value="CLOSED">Disposed or written off</option></select>');
        if (f.indexOf('recordType') >= 0) h += field('pType', 'Record type', '<select id="pType" class="fa-select"><option value="">All types</option><option value="ACQUISITION">Acquisition</option><option value="OPENING">Opening balance</option><option value="DEPRECIATION">Depreciation</option><option value="REVALUATION">Revaluation</option><option value="APPRECIATION">Appreciation</option><option value="TRANSFER">Transfer</option><option value="STATUS">Status change</option><option value="MAINTENANCE">Maintenance</option><option value="VERIFICATION">Verification</option><option value="ESTIMATE">Change of estimate</option><option value="DISPOSAL">Disposal</option><option value="VOID">Void</option></select>');
        if (f.indexOf('search') >= 0) h += field('pQ', 'Search', '<input id="pQ" class="fa-input" placeholder="Asset number, name or serial"/>');
        if (cur.groups.length > 1) h += field('pGroup', 'Group by', '<select id="pGroup" class="fa-select">' + cur.groups.map(function (g) { return '<option value="' + esc(g.k) + '">' + esc(g.t) + '</option>'; }).join('') + '</select>');
        qs('rFilters').innerHTML = h || '<p class="fa-hint">This report has no filters.</p>';

        if (qs('pFy')) {
            var years = B.finYears.slice();
            FA.fill('pFy', years.map(function (y) { return { id: y, name: y }; }), {});
            qs('pFy').value = cur.key === 'depreciation' && years.indexOf(B.lastYear) >= 0 ? B.lastYear : years[0];
        }
        if (qs('pRun')) FA.fill('pRun', (B.runs || []).filter(function (r) { return r.status === 'POSTED'; }).map(function (r) { return { id: r.id, name: 'Run ' + r.id + ', to ' + r.periodEnd + ' (' + r.finYear + ')' }; }), { all: 'Whole financial year' });
        if (qs('pCampus')) FA.fill('pCampus', B.campuses, { all: 'All campuses' });
        if (qs('pCat')) {
            FA.fill('pCat', CATS, { all: 'All categories' });
            var subs = function () {
                var cat = qs('pCat').value, items = [];
                CATS.forEach(function (c) { if (cat && String(c.id) !== cat) return; (c.children || []).forEach(function (s) { items.push({ id: s.id, name: (cat ? '' : c.code + ' / ') + s.name }); }); });
                FA.fill('pSub', items, { all: 'All sub-categories' });
            };
            qs('pCat').onchange = subs; subs();
        }
        if (qs('pDept')) FA.fill('pDept', B.departments, { all: 'All departments' });
        pickers = {};
        if (qs('pCus')) pickers.cus = FA.typeahead('pCus', { source: FA.staffSource, render: FA.staffRender, placeholder: 'Anyone' });
        if (qs('pAsset')) {
            var u = FA.readUrl();
            pickers.asset = FA.typeahead('pAsset', { placeholder: 'Asset number or name',
                source: function (q, cb) { FA.call(PAGE, 'SearchAssets', { q: q }, function (r) { cb(r.success ? r.rows : []); }); },
                render: function (a) { return '<span class="fa-code">' + esc(a.assetNo) + '</span> ' + esc(a.title); } });
        }
    }

    function cfg() {
        var c = {};
        if (qs('pFy')) c.finYear = FA.val('pFy');
        if (qs('pRun') && FA.val('pRun')) c.runId = FA.val('pRun');
        if (qs('pFrom')) { c.from = FA.val('pFrom'); c.to = FA.val('pTo'); }
        if (qs('pAsOf')) c.asOf = FA.val('pAsOf');
        if (qs('pCampus')) c.campusId = FA.val('pCampus');
        if (qs('pCat')) { c.categoryId = FA.val('pCat'); c.subCategoryId = FA.val('pSub'); }
        if (qs('pDept')) c.departmentId = FA.val('pDept');
        if (pickers.cus && pickers.cus.get()) c.custodianEmpId = pickers.cus.get().id;
        if (qs('pStatus')) c.status = FA.val('pStatus');
        if (qs('pType')) c.recordType = FA.val('pType');
        if (qs('pQ')) c.q = FA.val('pQ');
        if (pickers.asset && pickers.asset.get()) c.assetId = pickers.asset.get().id;
        return c;
    }
    function group() { return qs('pGroup') ? FA.val('pGroup') : cur.groups[0].k; }
    function check() {
        qs('rErr').textContent = '';
        if (cur.filters.indexOf('asset') >= 0 && !(pickers.asset && pickers.asset.get())) { qs('rErr').textContent = 'Choose the asset.'; return false; }
        return true;
    }

    qs('rPreview').onclick = function () {
        if (!cur || !check()) return;
        var b = this; FA.busy(b, true, 'Working');
        FA.call(PAGE, 'PreviewReport', { report: cur.key, configJson: JSON.stringify(cfg()), groupBy: group() }, function (r) {
            FA.busy(b, false);
            if (!r.success) { qs('rErr').textContent = r.message; return; }
            var lastG = null, span = r.cols.length;
            var body = r.rows.map(function (x) {
                var gh = '';
                if (r.grouped && x.g !== lastG) { gh = '<tr><td colspan="' + span + '" style="background:#e9eef6;font-weight:700;color:#05275C">' + esc(x.g) + '</td></tr>'; lastG = x.g; }
                return gh + '<tr>' + x.c.map(function (v, i) { return '<td' + (r.cols[i].num ? ' class="fa-num"' : '') + '>' + esc(v) + '</td>'; }).join('') + '</tr>';
            }).join('');
            qs('rOut').innerHTML = '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">' + esc(r.title) + ' <span class="fa-card__meta">' + esc(r.subtitle || '') + '</span></div>' +
                '<span class="fa-card__meta">' + FA.plural(r.total, 'row', 'rows') + (r.total > 100 ? ', first 100 shown' : '') + '</span></div>' +
                '<div class="fa-table-wrap"><table class="fa-table"><thead><tr>' + r.cols.map(function (c) { return '<th' + (c.num ? ' class="fa-num"' : '') + '>' + esc(c.t) + '</th>'; }).join('') + '</tr></thead>' +
                '<tbody>' + (body || '<tr><td colspan="' + span + '" class="fa-empty">Nothing to report for these filters.</td></tr>') + '</tbody>' +
                (r.totals && r.total ? '<tfoot><tr>' + r.totals.map(function (v, i) { return '<td' + (r.cols[i].num ? ' class="fa-num"' : '') + '>' + (i === 0 && !v ? 'Total' : esc(v)) + '</td>'; }).join('') + '</tr></tfoot>' : '') +
                '</table></div></div>';
        });
    };

    qs('rExport').onclick = function () {
        if (!cur || !check()) return;
        FA.exportDialog({ page: PAGE, report: cur.key, title: cur.title, cfg: cfg(), cols: cur.cols, groups: cur.groups.length > 1 ? cur.groups : null, group: group(), countMethod: 'CountExport' });
    };

    var u = FA.readUrl();
    choose(u.report && REPORTS.some(function (r) { return r.key === u.report; }) ? u.report : 'register');
})();
