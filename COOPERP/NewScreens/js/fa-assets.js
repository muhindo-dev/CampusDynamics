/* Fixed Assets: Assets screen (register list, detail, form, records). ES5. */
(function () {
    'use strict';
    var B = window.FA_BOOT || {}, R = B.rights || {}, PAGE = 'Assets.aspx';
    var esc = FA.esc, money = FA.money, qs = FA.qs;

    // ── Lookups ───────────────────────────────────────────────────────
    var CATS = [], SUBS = {}, CATBYID = {};
    (B.tree || []).forEach(function (c) {
        CATS.push(c); CATBYID[c.id] = c;
        (c.children || []).forEach(function (s) { s.parent = c; SUBS[s.id] = s; });
    });
    function campusName(id) { var n = ''; (B.campuses || []).forEach(function (c) { if (c.id === id) n = c.name; }); return n; }
    function deptName(id) { var n = ''; (B.departments || []).forEach(function (d) { if (d.id === id) n = d.name; }); return n; }
    var FLAGS = { no_custodian: 'No responsible person', not_tagged: 'Not yet tagged', revaluation_due: 'Revaluation due', life_ended: 'Useful life ended', not_verified: 'Not verified in 12 months', warranty_soon: 'Warranty ends within 90 days' };

    // ── State ─────────────────────────────────────────────────────────
    var S = { page: 1, size: 50, sort: 'asset_no', dir: 'asc', flag: '', cus: null, bfrom: '', bto: '' };
    var selected = {}, lastRows = [], lastTotal = 0;

    function cfg(withPage) {
        var c = {
            campusId: qs('fCampus').value, categoryId: qs('fCat').value, subCategoryId: qs('fSub').value, status: qs('fStatus').value,
            departmentId: qs('fDept').value, custodianEmpId: S.cus ? S.cus.id : '', finYear: qs('fFy').value, q: qs('fQ').value.trim(),
            flag: S.flag, sort: qs('fSort').value, dir: qs('fDir').value
        };
        if (S.bfrom || S.bto) { c.dateField = 'purchase'; c.from = S.bfrom; c.to = S.bto; }
        if (withPage) { c.page = S.page; c.size = S.size; }
        return c;
    }

    // ── Filters ───────────────────────────────────────────────────────
    function fillSubs() {
        var cat = qs('fCat').value, items = [];
        CATS.forEach(function (c) {
            if (cat && String(c.id) !== cat) return;
            (c.children || []).forEach(function (s) { items.push({ id: s.id, name: (cat ? '' : c.name + ' / ') + s.name }); });
        });
        FA.fill('fSub', items, { all: 'All sub-categories' });
    }
    var cusPicker;
    function initFilters() {
        FA.fill('fCampus', B.campuses, { all: 'All campuses' });
        FA.fill('fCat', CATS, { all: 'All categories' });
        FA.fill('fDept', B.departments, { all: 'All departments' });
        FA.fill('fFy', (B.finYears || []).map(function (y) { return { id: y, name: y }; }), { all: 'Any year' });
        var u = FA.readUrl();
        if (u.campus) qs('fCampus').value = u.campus;
        if (u.cat) qs('fCat').value = u.cat;
        fillSubs();
        if (u.sub) qs('fSub').value = u.sub;
        if (u.status !== undefined) qs('fStatus').value = u.status;
        if (u.dept) qs('fDept').value = u.dept;
        if (u.fy) qs('fFy').value = u.fy;
        if (u.q) qs('fQ').value = u.q;
        if (u.sort) qs('fSort').value = u.sort;
        if (u.dir) qs('fDir').value = u.dir;
        if (u.flag) S.flag = u.flag;
        if (u.bfrom) S.bfrom = u.bfrom;
        if (u.bto) S.bto = u.bto;
        if (u.page) S.page = parseInt(u.page, 10) || 1;
        if (u.cus) S.cus = { id: parseInt(u.cus, 10), name: u.cusName || 'Selected person' };
        cusPicker = FA.typeahead('fCus', { source: FA.staffSource, render: FA.staffRender, placeholder: 'Name or staff number', value: S.cus,
                                           onPick: function (it) { S.cus = it; S.page = 1; load(); } });
        qs('fCat').onchange = function () { fillSubs(); S.page = 1; load(); };
        ['fCampus', 'fSub', 'fStatus', 'fDept', 'fFy', 'fSort', 'fDir'].forEach(function (id) { qs(id).onchange = function () { S.page = 1; load(); }; });
        qs('fQ').addEventListener('input', FA.debounce(function () { S.page = 1; load(); }, 400));
        qs('btnApply').onclick = function () { S.page = 1; load(); };
        qs('btnReset').onclick = function () {
            ['fCampus', 'fCat', 'fDept', 'fFy', 'fStatus'].forEach(function (id) { qs(id).value = ''; });
            fillSubs(); qs('fQ').value = ''; S.flag = ''; S.bfrom = ''; S.bto = ''; S.cus = null; cusPicker.set(null); S.page = 1; load();
        };
    }

    function chips() {
        var c = [];
        if (S.flag) c.push({ k: 'flag', t: FLAGS[S.flag] || S.flag });
        if (S.bfrom || S.bto) c.push({ k: 'bought', t: 'Bought ' + (S.bfrom ? 'from ' + S.bfrom + ' ' : '') + (S.bto ? 'to ' + S.bto : '') });
        var h = c.map(function (x) { return '<span class="fa-chip">' + esc(x.t) + '<button type="button" data-k="' + x.k + '" aria-label="Remove">&times;</button></span>'; }).join('');
        qs('faChips').innerHTML = h;
        qs('faChips').style.display = h ? '' : 'none';
        Array.prototype.forEach.call(qs('faChips').querySelectorAll('button'), function (b) {
            b.onclick = function () { var k = b.getAttribute('data-k'); if (k === 'flag') S.flag = ''; if (k === 'bought') { S.bfrom = ''; S.bto = ''; } S.page = 1; load(); };
        });
    }

    // ── List ──────────────────────────────────────────────────────────
    function load() {
        var c = cfg(true);
        FA.writeUrl({ campus: c.campusId, cat: c.categoryId, sub: c.subCategoryId, status: c.status, dept: c.departmentId,
                      cus: S.cus ? S.cus.id : '', cusName: S.cus ? S.cus.name : '', fy: c.finYear, q: c.q, flag: S.flag, bfrom: S.bfrom, bto: S.bto,
                      sort: c.sort === 'asset_no' ? '' : c.sort, dir: c.dir === 'asc' ? '' : c.dir, page: S.page > 1 ? S.page : '' });
        chips();
        qs('faBody').innerHTML = '<tr><td colspan="12" class="fa-loading">Loading</td></tr>';
        FA.call(PAGE, 'GetAssets', { configJson: JSON.stringify(c) }, function (r) {
            if (!r.success) { qs('faBody').innerHTML = '<tr><td colspan="12" class="fa-empty">' + esc(r.message) + '</td></tr>'; return; }
            lastRows = r.rows; lastTotal = r.total;
            qs('faCount').textContent = FA.plural(r.total, 'asset', 'assets');
            if (!r.rows.length) {
                qs('faBody').innerHTML = '<tr><td colspan="12" class="fa-empty">No assets match these filters.' +
                    (R.edit && !c.q && !c.campusId ? ' Add the first one with New asset, or import a list.' : '') + '</td></tr>';
            } else {
                qs('faBody').innerHTML = r.rows.map(function (a) {
                    return '<tr class="is-click' + (selected[a.id] ? ' is-selected' : '') + '" data-id="' + a.id + '">' +
                        '<td class="fa-check"><input type="checkbox" data-ck="' + a.id + '"' + (selected[a.id] ? ' checked' : '') + ' aria-label="Select ' + esc(a.assetNo) + '"/></td>' +
                        '<td><span class="fa-code">' + esc(a.assetNo) + '</span>' + (a.tagNo ? '<span class="fa-sub">Tag ' + esc(a.tagNo) + '</span>' : '') + '</td>' +
                        '<td><strong>' + esc(a.name) + '</strong>' + (a.serialNo || a.make ? '<span class="fa-sub">' + esc([a.make, a.model, a.serialNo ? 'S/N ' + a.serialNo : ''].filter(Boolean).join(', ')) + '</span>' : '') + '</td>' +
                        '<td>' + esc(a.subCategory) + '<span class="fa-sub">' + esc(a.category) + '</span></td>' +
                        '<td>' + esc(a.location) + '</td><td>' + esc(a.department) + '</td>' +
                        '<td>' + (a.custodian ? esc(a.custodian) : '<span class="fa-muted">None</span>') + '</td>' +
                        '<td style="white-space:nowrap">' + esc(a.purchaseDate) + '</td>' +
                        '<td class="fa-num">' + money(a.cost) + '</td><td class="fa-num"><strong>' + money(a.value) + '</strong></td>' +
                        '<td style="white-space:nowrap">' + FA.change(a.changePct) + '</td>' +
                        '<td>' + FA.badge(a.status, a.statusLabel) + '</td></tr>';
                }).join('');
            }
            qs('faFoot').innerHTML = r.total ? '<tr><td></td><td colspan="7">Total of ' + FA.plural(r.total, 'asset', 'assets') + ' matching the filters</td>' +
                '<td class="fa-num">' + money(r.totals.cost) + '</td><td class="fa-num">' + money(r.totals.value) + '</td><td colspan="2"></td></tr>' : '';
            FA.pager('faPager', { page: r.page, size: r.size, total: r.total }, function (p) { S.page = p; load(); window.scrollTo(0, 0); });
            wireRows(); syncBatch();
        });
    }

    function wireRows() {
        Array.prototype.forEach.call(qs('faBody').querySelectorAll('tr[data-id]'), function (tr) {
            tr.onclick = function (e) {
                if (e.target && e.target.getAttribute && e.target.getAttribute('data-ck')) return;
                openAsset(parseInt(tr.getAttribute('data-id'), 10));
            };
        });
        Array.prototype.forEach.call(qs('faBody').querySelectorAll('input[data-ck]'), function (ck) {
            ck.onclick = function (e) {
                e.stopPropagation();
                var id = parseInt(ck.getAttribute('data-ck'), 10);
                if (ck.checked) selected[id] = true; else delete selected[id];
                ck.closest('tr').className = 'is-click' + (ck.checked ? ' is-selected' : '');
                syncBatch();
            };
        });
        qs('ckAll').checked = lastRows.length > 0 && lastRows.every(function (a) { return selected[a.id]; });
    }
    qs('ckAll').onclick = function () {
        var on = qs('ckAll').checked;
        lastRows.forEach(function (a) { if (on) selected[a.id] = true; else delete selected[a.id]; });
        load();
    };

    function selIds() { return Object.keys(selected).map(function (k) { return parseInt(k, 10); }); }

    function syncBatch() {
        var n = selIds().length, bar = qs('faBatch');
        if (!n) { bar.className = 'fa-batch'; bar.innerHTML = ''; return; }
        bar.className = 'fa-batch is-on';
        bar.innerHTML = '<strong>' + FA.plural(n, 'asset', 'assets') + ' selected</strong>' +
            (lastTotal > n && lastTotal <= 300 ? '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" data-a="all">Select all ' + money(lastTotal) + '</button>' : '') +
            '<span class="fa-spacer"></span>' +
            (R.transfer ? '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" data-a="transfer">Transfer</button>' +
                          '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" data-a="status">Change status</button>' +
                          '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" data-a="verify">Record verification</button>' : '') +
            '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" data-a="tags">' + FA.icon('tag') + ' Print tags</button>' +
            '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" data-a="export">' + FA.icon('download') + ' Export selected</button>' +
            '<button type="button" class="fa-btn fa-btn--inverse fa-btn--sm" data-a="clear">Clear</button>';
        Array.prototype.forEach.call(bar.querySelectorAll('button[data-a]'), function (b) {
            b.onclick = function () {
                var a = b.getAttribute('data-a');
                if (a === 'clear') { selected = {}; load(); }
                else if (a === 'all') FA.call(PAGE, 'GetIds', { configJson: JSON.stringify(cfg(false)) }, function (r) { if (r.success) { r.ids.slice(0, 300).forEach(function (i) { selected[i] = true; }); load(); } });
                else if (a === 'tags') FA.download(PAGE, { faReport: 'labels', faFormat: 'pdf', faConfig: JSON.stringify({ ids: selIds(), status: 'ALL' }) });
                else if (a === 'export') exportDialog({ ids: selIds(), status: 'ALL' }, 'Selected assets');
                else batchForm(a);
            };
        });
    }

    // ── Header actions and export ─────────────────────────────────────
    function headerActions() {
        var h = '';
        if (R.edit) h += '<button type="button" class="fa-btn fa-btn--inverse" id="hNew">' + FA.icon('plus') + ' New asset</button>';
        if (R['import']) h += '<a class="fa-btn fa-btn--inverse" href="AssetImport.aspx">' + FA.icon('upload') + ' Import</a>';
        h += '<button type="button" class="fa-btn fa-btn--inverse" id="hTags">' + FA.icon('tag') + ' Print tags</button>';
        qs('faHeaderActions').innerHTML = h;
        if (qs('hNew')) qs('hNew').onclick = function () { assetForm(null); };
        qs('hTags').onclick = function () {
            FA.confirm({ title: 'Print asset tags', message: 'Print tags for the ' + FA.plural(Math.min(lastTotal, 2000), 'asset', 'assets') + ' matching the current filters, on A4 label sheets of 24 (3 by 8, 70 by 37 mm)?', ok: 'Print tags' },
                function () { FA.download(PAGE, { faReport: 'labels', faFormat: 'pdf', faConfig: JSON.stringify(cfg(false)) }); });
        };
        qs('btnExport').innerHTML = FA.icon('download') + ' Export';
        qs('btnExport').onclick = function () { exportDialog(cfg(false), 'Fixed Asset Register'); };
    }

    function exportDialog(c, title) {
        FA.exportDialog({
            page: PAGE, report: 'register', title: title, cfg: c, cols: B.registerCols, countMethod: 'CountExport',
            groups: [{ k: 'category', t: 'Category' }, { k: 'subcategory', t: 'Sub-category' }, { k: 'campus', t: 'Campus' }, { k: 'department', t: 'Department' },
                     { k: 'custodian', t: 'Responsible person' }, { k: 'status', t: 'Status' }, { k: 'none', t: 'No grouping' }], group: 'category'
        });
    }

    // ── Batch actions ─────────────────────────────────────────────────
    function placementFields(prefix) {
        return '<div class="fa-form">' +
            '<div class="fa-field"><label class="fa-label" for="' + prefix + 'Campus">Campus</label><select id="' + prefix + 'Campus" class="fa-select"><option value="">Keep as it is</option></select></div>' +
            '<div class="fa-field"><label class="fa-label" for="' + prefix + 'Dept">Department</label><select id="' + prefix + 'Dept" class="fa-select"><option value="">Keep as it is</option></select></div>' +
            '<div class="fa-field"><label class="fa-label" for="' + prefix + 'Bld">Building or block</label><input id="' + prefix + 'Bld" class="fa-input" maxlength="120" placeholder="Keep as it is"/></div>' +
            '<div class="fa-field"><label class="fa-label" for="' + prefix + 'Room">Room or office</label><input id="' + prefix + 'Room" class="fa-input" maxlength="120" placeholder="Keep as it is"/></div>' +
            '<div class="fa-field fa-full"><label class="fa-label">Responsible person</label><div id="' + prefix + 'Cus"></div>' +
            '<label class="fa-check-line" style="margin-top:6px"><input type="checkbox" id="' + prefix + 'CusClear"/> Remove the responsible person</label></div></div>';
    }

    function batchForm(kind) {
        var ids = selIds();
        var title = kind === 'transfer' ? 'Transfer assets' : kind === 'status' ? 'Change status' : 'Record verification';
        var extra = '<div class="fa-field"><label class="fa-label" for="bDate">Date <span class="fa-req">*</span></label><input type="date" id="bDate" class="fa-input" value="' + B.today + '" max="' + B.today + '" style="max-width:180px"/></div><div style="height:12px"></div>';
        if (kind === 'transfer') extra += placementFields('b');
        if (kind === 'status') extra += '<div class="fa-field"><span class="fa-label">New status <span class="fa-req">*</span></span><div class="fa-radios">' +
            '<label><input type="radio" name="bSt" value="IN_USE" checked/> In use</label><label><input type="radio" name="bSt" value="IN_STORE"/> In store</label>' +
            '<label><input type="radio" name="bSt" value="UNDER_REPAIR"/> Under repair</label><label><input type="radio" name="bSt" value="LOST"/> Lost</label></div></div>';
        if (kind === 'verify') extra += '<div class="fa-form"><div class="fa-field"><span class="fa-label">Result</span><div class="fa-radios"><label><input type="radio" name="bFound" value="1" checked/> Found</label><label><input type="radio" name="bFound" value="0"/> Not found</label></div></div>' +
            '<div class="fa-field"><label class="fa-label" for="bCond">Condition</label><select id="bCond" class="fa-select"><option value="GOOD">Good</option><option value="FAIR">Fair</option><option value="POOR">Poor</option><option value="UNSERVICEABLE">Unserviceable</option></select></div>' +
            '<div class="fa-field fa-full"><label class="fa-label" for="bBy">Verified by</label><input id="bBy" class="fa-input" maxlength="100"/></div></div>';
        var bCus = null;
        var m = FA.reason({ title: title, sub: FA.plural(ids.length, 'asset', 'assets'), size: 'mid', extra: extra, label: kind === 'verify' ? 'Remarks' : 'Reason', min: kind === 'verify' ? 0 : 5, ok: title },
            function (reason, modal, done) {
                var d = { ids: ids, type: kind === 'transfer' ? 'TRANSFER' : kind === 'status' ? 'STATUS' : 'VERIFICATION', date: FA.val('bDate'), reason: reason, clientOpId: FA.uuid() };
                if (kind === 'transfer') {
                    var to = {};
                    if (FA.val('bCampus')) to.campusId = FA.val('bCampus');
                    if (FA.val('bDept')) to.departmentId = FA.val('bDept');
                    if (FA.val('bBld')) to.building = FA.val('bBld');
                    if (FA.val('bRoom')) to.room = FA.val('bRoom');
                    if (FA.val('bCusClear')) to.custodianEmpId = ''; else if (bCus) to.custodianEmpId = bCus.id;
                    if (!Object.keys(to).length) { done('Choose at least one thing to change.'); return; }
                    d.to = to;
                }
                if (kind === 'status') d.status = FA.radio('bSt');
                if (kind === 'verify') { d.found = FA.radio('bFound') === '1'; d.condition = FA.val('bCond'); d.verifiedBy = FA.val('bBy'); }
                FA.call(PAGE, 'Batch', { json: JSON.stringify(d) }, function (r) {
                    if (!r.success) { done(r.message); return; }
                    done();
                    if (r.skipped && r.skipped.length) {
                        FA.modal({ title: 'Some assets were skipped', size: 'mid',
                            body: '<p>' + esc(r.message) + '</p><table class="fa-table"><thead><tr><th>Asset no</th><th>Why</th></tr></thead><tbody>' +
                                  r.skipped.map(function (s) { return '<tr><td class="fa-code">' + esc(s.assetNo) + '</td><td>' + esc(s.why) + '</td></tr>'; }).join('') + '</tbody></table>' });
                    } else FA.toast(r.message);
                    selected = {}; load();
                });
            });
        if (kind === 'transfer') {
            FA.fill('bCampus', B.campuses, { all: 'Keep as it is' });
            FA.fill('bDept', B.departments, { all: 'Keep as it is' });
            FA.typeahead('bCus', { source: FA.staffSource, render: FA.staffRender, placeholder: 'Keep as it is, or search', onPick: function (it) { bCus = it; } });
        }
        return m;
    }

    // ── Asset detail ──────────────────────────────────────────────────
    var cur = null, curModal = null, curTab = 'overview', chart = null;

    function openAsset(id, tab) {
        FA.call(PAGE, 'GetAsset', { id: id }, function (r) {
            if (!r.success) { FA.toast(r.message, true); return; }
            cur = r.data;
            if (curModal) { curModal.close(); }
            curModal = FA.modal({ size: 'wide', onClose: function () { curModal = null; if (chart) { chart.destroy(); chart = null; } } });
            renderAsset(tab || 'overview');
        });
    }
    function reloadAsset(tab) { if (cur) openAsset(cur.asset.id, tab || curTab); load(); }

    function dl(items) {
        return '<dl class="fa-dl">' + items.map(function (x) { return '<div><dt>' + esc(x[0]) + '</dt><dd>' + (x[2] ? x[1] : esc(x[1] || '')) + (x[1] ? '' : '<span class="fa-muted">Not recorded</span>') + '</dd></div>'; }).join('') + '</dl>';
    }

    function renderAsset(tab) {
        curTab = tab;
        var a = cur.asset, m = curModal;
        m.title(a.assetNo + ', ' + a.name, a.category + ' / ' + a.subCategory + '. ' + a.statusLabel);
        var tabs = [['overview', 'Overview'], ['records', 'Records (' + cur.records.length + ')'], ['files', 'Attachments (' + cur.attachments.length + ')'], ['history', 'Change history']];
        var h = '<div class="fa-subtabs">' + tabs.map(function (t) { return '<button type="button" class="fa-subtab' + (t[0] === tab ? ' is-active' : '') + '" data-t="' + t[0] + '">' + t[1] + '</button>'; }).join('') + '</div>';
        if (tab === 'overview') h += overview(a);
        else if (tab === 'records') h += recordsTab();
        else if (tab === 'files') h += filesTab(a);
        else h += historyTab();
        m.body.innerHTML = h;
        Array.prototype.forEach.call(m.body.querySelectorAll('[data-t]'), function (b) { b.onclick = function () { renderAsset(b.getAttribute('data-t')); }; });
        footer(a);
        if (tab === 'overview') drawChart();
        if (tab === 'records') wireRecords();
        if (tab === 'files') wireFiles(a);
    }

    function overview(a) {
        var trendCls = cur.trend.direction === 'Appreciating' ? 'fa-up' : cur.trend.direction === 'Depreciating' ? 'fa-down' : 'fa-muted';
        var h = '';
        if (a.status === 'LOST') h += '<div class="fa-notice fa-notice--warn">This asset is recorded as lost. Record it as found with a status change, or write it off with a disposal.</div>';
        if (a.status === 'VOID') h += '<div class="fa-notice">This asset was entered in error and is excluded from every total.</div>';
        h += '<div class="fa-value"><div><span class="fa-label">Book value (UGX)</span><b>' + money(a.currentValue) + '</b></div>' +
             '<div><span class="fa-label">Cost (UGX)</span><b>' + money(a.costBasis) + '</b></div>' +
             '<div><span class="fa-label">Accumulated depreciation</span><b>' + money(a.accumDep) + '</b></div>' +
             '<div><span class="fa-label">Since acquisition</span><b class="' + trendCls + '">' + esc(cur.trend.direction) + '</b>' +
             '<span class="fa-sub">' + (cur.trend.change === 0 ? 'No change in value' : (cur.trend.change > 0 ? 'Up ' : 'Down ') + 'UGX ' + money(Math.abs(cur.trend.change)) + ' (' + Math.abs(cur.trend.pct).toFixed(1) + '%)') + '</span></div></div>';
        h += '<div class="fa-grid-main"><div>';
        h += '<div class="fa-section">Identity</div>' + dl([['Asset number', a.assetNo], ['Tag or barcode', a.tagNo], ['Serial number', a.serialNo], ['Make or brand', a.make], ['Model', a.model], ['Quantity', a.quantity > 1 ? String(a.quantity) : '1']]);
        if (a.description) h += '<p style="margin:10px 0 0;line-height:1.5">' + esc(a.description) + '</p>';
        h += '<div class="fa-section">Location and custody</div>' + dl([['Campus', a.campus], ['Building and room', [a.building, a.room].filter(Boolean).join(', ')], ['Department', a.department],
             ['Responsible person', a.custodian ? a.custodian + (a.custodianCode ? ' (' + a.custodianCode + ')' : '') : ''], ['Responsible since', a.custodianSince], ['Last verified', a.lastVerified]]);
        h += '<div class="fa-section">Acquisition</div>' + dl([['Purchase date', a.purchaseDateLabel], ['Supplier', a.supplierName], ['Invoice', a.invoiceRef], ['Order or LPO', a.orderRef],
             ['Requisition', a.requisitionRef], ['Funding source', a.fundingSource], ['Warranty ends', a.warrantyExpiryLabel], ['Original cost (UGX)', money(a.originalCost)]]);
        h += '<div class="fa-section">Depreciation</div>' + dl([['Method', a.methodLabel], ['Useful life', a.method === 'SL' && a.lifeYears ? a.lifeYears + ' years' : ''],
             ['Rate', a.method === 'RB' && a.ratePct ? a.ratePct + '% a year' : (a.method === 'SL' && a.ratePct ? Number(a.ratePct).toFixed(2) + '% a year' : '')],
             ['Residual value (UGX)', money(a.residualValue)], ['Charged from', a.depStartLabel], ['Charged to', a.depreciatedTo], ['Last valuation', a.lastValuation]]);
        if (a.notes) h += '<div class="fa-section">Notes</div><p style="margin:0;white-space:pre-wrap">' + esc(a.notes) + '</p>';
        h += '</div><div><div class="fa-section">Value over time</div><div class="fa-chart"><canvas id="faValChart"></canvas></div>' +
             '<p class="fa-hint">Each point is a value record: acquisition, depreciation, revaluation, improvement or disposal.</p>' +
             '<div class="fa-section">Recorded</div>' + dl([['Created by', a.createdBy], ['Created', a.createdAt]]) + '</div></div>';
        return h;
    }

    function drawChart() {
        var el = qs('faValChart'); if (!el || !window.Chart) return;
        if (chart) chart.destroy();
        Chart.defaults.animation = false;
        var pts = cur.series;
        chart = new Chart(el, {
            type: 'line',
            data: { labels: pts.map(function (p) { return p.label; }), datasets: [{ data: pts.map(function (p) { return p.value; }), borderColor: '#174DA4', backgroundColor: '#174DA4', borderWidth: 2, pointRadius: 3, tension: 0, fill: false, stepped: false }] },
            options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: false }, tooltip: { callbacks: { label: function (c) { return 'UGX ' + money(c.parsed.y); } } } },
                       scales: { y: { beginAtZero: true, ticks: { callback: function (v) { return FA.short(v); } }, grid: { color: '#eef1f5' } }, x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 6 } } } }
        });
    }

    function recordsTab() {
        if (!cur.records.length) return '<div class="fa-empty">No records.</div>';
        return '<div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Date</th><th>Type</th><th>Details</th><th class="fa-num">Before</th><th class="fa-num">Change</th><th class="fa-num">After</th><th>Reason and reference</th><th>Recorded by</th><th></th></tr></thead><tbody>' +
            cur.records.map(function (x) {
                var ch = x.valueRecord ? '<span class="' + (x.change > 0 ? 'fa-up' : x.change < 0 ? 'fa-down' : '') + '">' + (x.change > 0 ? '+' : '') + money(x.change) + '</span>' : '';
                return '<tr class="' + (x.reversed ? 'is-reversed' : '') + '"><td style="white-space:nowrap">' + esc(x.date) + '<span class="fa-sub">' + esc(x.finYear) + '</span></td>' +
                    '<td><strong>' + esc(x.typeLabel) + '</strong>' + (x.reversed ? '<span class="fa-sub">Reversed</span>' : '') + '</td><td>' + esc(x.details) + '</td>' +
                    '<td class="fa-num">' + (x.valueRecord ? money(x.before) : '') + '</td><td class="fa-num">' + ch + '</td><td class="fa-num">' + (x.valueRecord ? money(x.after) : '') + '</td>' +
                    '<td>' + esc(x.reason) + (x.reference ? '<span class="fa-sub">Ref ' + esc(x.reference) + '</span>' : '') + (x.approvalRef ? '<span class="fa-sub">Approval ' + esc(x.approvalRef) + '</span>' : '') + '</td>' +
                    '<td>' + esc(x.by) + '<span class="fa-sub">' + esc(x.at) + '</span></td>' +
                    '<td class="fa-keep">' + (x.canReverse ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-rev="' + x.id + '" data-label="' + esc(x.typeLabel) + '">Reverse</button>' : '') + '</td></tr>';
            }).join('') + '</tbody></table></div><p class="fa-hint" style="margin-top:8px">Records cannot be edited or deleted. A mistake is corrected by reversing the record, which keeps both on file.</p>';
    }

    function wireRecords() {
        Array.prototype.forEach.call(curModal.body.querySelectorAll('[data-rev]'), function (b) {
            b.onclick = function () {
                var id = parseInt(b.getAttribute('data-rev'), 10);
                FA.reason({ title: 'Reverse ' + b.getAttribute('data-label').toLowerCase(), message: 'The record stays on file, marked as reversed, and its effect on the asset is undone.', warn: true, ok: 'Reverse', danger: true },
                    function (reason, m, done) {
                        FA.call(PAGE, 'ReverseRecord', { recordId: id, reason: reason, clientOpId: FA.uuid() }, function (r) {
                            if (!r.success) { done(r.message); return; }
                            done(); FA.toast('Record reversed.'); reloadAsset('records');
                        });
                    });
            };
        });
    }

    function filesTab(a) {
        var h = '';
        if (R.edit && a.status !== 'VOID') {
            h += '<div class="fa-row" style="margin-bottom:12px"><select id="upKind" class="fa-select" style="width:auto"><option value="PHOTO">Photo</option><option value="INVOICE">Invoice</option>' +
                 '<option value="WARRANTY">Warranty</option><option value="VALUATION">Valuation report</option><option value="DISPOSAL">Disposal approval</option><option value="OTHER">Other document</option></select>' +
                 '<input type="file" id="upFile" accept=".pdf,.jpg,.jpeg,.png,.docx,.xlsx" class="fa-input" style="width:auto;padding:4px"/>' +
                 '<button type="button" class="fa-btn fa-btn--primary fa-btn--sm" id="upGo">' + FA.icon('upload') + ' Attach</button><span class="fa-hint">PDF, JPG, PNG, Word or Excel, up to 10 MB.</span></div>';
        }
        if (!cur.attachments.length) return h + '<div class="fa-empty">No attachments.</div>';
        var photos = cur.attachments.filter(function (x) { return x.kind === 'PHOTO'; });
        if (photos.length) h += '<div class="fa-row" style="margin-bottom:12px">' + photos.map(function (p) { return '<a href="FaFile.ashx?id=' + p.id + '&view=1" target="_blank" rel="noopener"><img src="FaFile.ashx?id=' + p.id + '&view=1" alt="' + esc(p.name) + '" style="height:110px;border:1px solid #E0E5ED"/></a>'; }).join('') + '</div>';
        h += '<table class="fa-table"><thead><tr><th>File</th><th>Kind</th><th class="fa-num">Size</th><th>Added</th><th></th></tr></thead><tbody>' +
            cur.attachments.map(function (x) {
                return '<tr><td><a href="FaFile.ashx?id=' + x.id + '" class="fa-btn--link">' + esc(x.name) + '</a></td><td>' + esc(kindLabel(x.kind)) + '</td><td class="fa-num">' + Math.max(1, Math.round(x.size / 1024)) + ' KB</td>' +
                    '<td>' + esc(x.by) + '<span class="fa-sub">' + esc(x.at) + '</span></td>' +
                    '<td>' + (R.edit ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-rm="' + x.id + '">Remove</button>' : '') + '</td></tr>';
            }).join('') + '</tbody></table>';
        return h;
    }
    function kindLabel(k) { return { PHOTO: 'Photo', INVOICE: 'Invoice', WARRANTY: 'Warranty', VALUATION: 'Valuation report', DISPOSAL: 'Disposal approval', OTHER: 'Other' }[k] || k; }

    function upload(assetId, recordId, kind, file, cb) {
        var fd = new FormData();
        fd.append('assetId', assetId); if (recordId) fd.append('recordId', recordId); fd.append('kind', kind); fd.append('file', file);
        var x = new XMLHttpRequest();
        x.open('POST', 'FaUpload.ashx', true);
        x.setRequestHeader('X-CSRF-Token', FA.token());
        x.onreadystatechange = function () {
            if (x.readyState !== 4) return;
            var r; try { r = JSON.parse(x.responseText); } catch (e) { r = { success: false, message: 'The file could not be uploaded.' }; }
            cb(r);
        };
        x.send(fd);
    }

    function wireFiles(a) {
        if (qs('upGo')) qs('upGo').onclick = function () {
            var f = qs('upFile').files[0];
            if (!f) { FA.toast('Choose a file first.', true); return; }
            var b = qs('upGo'); FA.busy(b, true, 'Uploading');
            upload(a.id, null, qs('upKind').value, f, function (r) {
                FA.busy(b, false);
                if (!r.success) { FA.toast(r.message, true); return; }
                FA.toast('File attached.'); reloadAsset('files');
            });
        };
        Array.prototype.forEach.call(curModal.body.querySelectorAll('[data-rm]'), function (b) {
            b.onclick = function () {
                var id = parseInt(b.getAttribute('data-rm'), 10);
                FA.reason({ title: 'Remove attachment', message: 'The file is hidden from the asset but kept on the server.', min: 5, ok: 'Remove', danger: true },
                    function (reason, m, done) {
                        FA.call(PAGE, 'RemoveAttachment', { id: id, reason: reason }, function (r) { if (!r.success) { done(r.message); return; } done(); reloadAsset('files'); });
                    });
            };
        });
    }

    var FIELD_LABELS = { asset_no: 'Asset number', tag_no: 'Tag', name: 'Name', description: 'Description', serial_no: 'Serial number', model: 'Model', make: 'Make',
        category_id: 'Sub-category', asset_type: 'Type', quantity: 'Quantity', supplier_name: 'Supplier', supplier_id: 'Supplier record', purchase_date: 'Purchase date',
        invoice_ref: 'Invoice', order_ref: 'Order', requisition_ref: 'Requisition', funding_source: 'Funding source', warranty_expiry: 'Warranty ends',
        original_cost: 'Original cost', dep_method: 'Method', useful_life_years: 'Useful life', dep_rate_pct: 'Rate', residual_value: 'Residual value', dep_start_date: 'Depreciation start',
        notes: 'Notes', status: 'Status', current_value: 'Book value', cost_basis: 'Cost', accum_depreciation: 'Accumulated depreciation', reval_surplus: 'Revaluation surplus',
        base_value: 'Depreciation base', base_date: 'Base month', base_remaining_months: 'Remaining months', depreciated_to: 'Depreciated to', last_valuation_date: 'Last valuation',
        last_verified_date: 'Last verified', closed_on: 'Closed on', campus_id: 'Campus', building: 'Building', room: 'Room', room_id: 'Teaching room', department_id: 'Department',
        custodian_emp_id: 'Responsible person', custodian_since: 'Responsible since', type: 'Type', date: 'Date', before: 'Before', after: 'After', change: 'Change', reference: 'Reference', approvalRef: 'Approval' };
    var ACTIONS = { CREATE: 'Created', UPDATE: 'Edited', POST: 'Values updated from its records', UPLOAD: 'File attached', REMOVE: 'File removed', REVERSE: 'Record reversed' };

    function historyTab() {
        if (!cur.audit.length) return '<div class="fa-empty">No changes recorded.</div>';
        return '<table class="fa-table"><thead><tr><th>When</th><th>Who</th><th>What</th><th>Changes</th></tr></thead><tbody>' +
            cur.audit.map(function (x) {
                var ch = x.changes.filter(function (c) { return FIELD_LABELS[c.field]; }).map(function (c) {
                    return '<div><span class="fa-muted">' + esc(FIELD_LABELS[c.field]) + ':</span> ' + (c.before ? esc(c.before) + ' to ' : '') + '<strong>' + esc(c.after || 'none') + '</strong></div>';
                }).join('');
                return '<tr><td style="white-space:nowrap">' + esc(x.at) + '</td><td>' + esc(x.actor) + '</td><td>' + esc(ACTIONS[x.action] || FA_typeLabel(x.action)) +
                       (x.reason ? '<span class="fa-sub">' + esc(x.reason) + '</span>' : '') + '</td><td style="font-size:11.5px">' + ch + '</td></tr>';
            }).join('') + '</tbody></table>';
    }
    function FA_typeLabel(t) { var m = { ACQUISITION: 'Acquisition', DEPRECIATION: 'Depreciation', REVALUATION: 'Revaluation', APPRECIATION: 'Appreciation', TRANSFER: 'Transfer', STATUS: 'Status change', MAINTENANCE: 'Maintenance', VERIFICATION: 'Verification', ESTIMATE: 'Change of estimate', DISPOSAL: 'Disposal', VOID: 'Void' }; return m[t] || t; }

    function footer(a) {
        var m = curModal, h = '';
        h += '<button type="button" class="fa-btn fa-btn--secondary" id="dHist">' + FA.icon('print') + ' History statement</button>';
        h += '<span class="fa-spacer"></span>';
        if (a.open) {
            var menu = [];
            if (R.transfer) menu.push(['TRANSFER', 'Transfer'], ['STATUS', 'Change status'], ['VERIFICATION', 'Verification'], ['MAINTENANCE', 'Maintenance or repair']);
            else if (R.value) menu.push(['MAINTENANCE', 'Maintenance or repair']);
            if (R.value) menu.push(['DEPRECIATION', 'Manual depreciation'], ['REVALUATION', 'Revaluation'], ['APPRECIATION', 'Appreciation'], ['ESTIMATE', 'Change of estimate']);
            if (R.dispose) { menu.push(['DISPOSAL', 'Disposal or write-off']); if (!a.hasValueHistory) menu.push(['VOID', 'Void (entered in error)']); }
            if (menu.length) h += '<select id="dRec" class="fa-select" style="width:auto"><option value="">Add a record</option>' + menu.map(function (x) { return '<option value="' + x[0] + '">' + x[1] + '</option>'; }).join('') + '</select>';
        }
        if (R.edit && a.status !== 'VOID') h += '<button type="button" class="fa-btn fa-btn--primary" id="dEdit">' + FA.icon('edit') + ' Edit</button>';
        m.foot.innerHTML = h; m.foot.style.display = '';
        qs('dHist').onclick = function () { FA.download(PAGE, { faReport: 'history', faFormat: 'pdf', faConfig: JSON.stringify({ assetId: a.id }) }); FA.toast('Preparing the history statement.'); };
        if (qs('dEdit')) qs('dEdit').onclick = function () { assetForm(cur); };
        if (qs('dRec')) qs('dRec').onchange = function () { var t = this.value; this.value = ''; if (t) recordForm(t); };
    }

    // ── Record forms ──────────────────────────────────────────────────
    var TITLES = { TRANSFER: 'Transfer', STATUS: 'Change status', VERIFICATION: 'Record verification', MAINTENANCE: 'Maintenance or repair', DEPRECIATION: 'Manual depreciation',
                   REVALUATION: 'Revaluation', APPRECIATION: 'Appreciation', ESTIMATE: 'Change of estimate', DISPOSAL: 'Disposal or write-off', VOID: 'Void the asset' };

    function recordForm(type) {
        var a = cur.asset, rCus = null;
        var date = '<div class="fa-field"><label class="fa-label" for="rDate">Date <span class="fa-req">*</span></label><input type="date" id="rDate" class="fa-input" value="' + B.today + '" max="' + B.today + '"/></div>';
        var body = '<div class="fa-notice">Book value now UGX ' + money(a.currentValue) + (a.depreciatedTo ? '. Depreciation charged to ' + esc(a.depreciatedTo) + '.' : '.') + '</div><div class="fa-form">';
        var reasonLabel = 'Reason', min = 5, danger = false;
        switch (type) {
            case 'TRANSFER':
                body += date + '<div></div></div><div style="height:12px"></div>' + placementFields('r') + '<div>';
                break;
            case 'STATUS':
                body += date + '<div class="fa-field"><span class="fa-label">New status <span class="fa-req">*</span></span><div class="fa-radios">' +
                    ['IN_USE', 'IN_STORE', 'UNDER_REPAIR', 'LOST'].filter(function (s) { return s !== a.status; }).map(function (s, i) {
                        return '<label><input type="radio" name="rSt" value="' + s + '"' + (i === 0 ? ' checked' : '') + '/> ' + { IN_USE: 'In use', IN_STORE: 'In store', UNDER_REPAIR: 'Under repair', LOST: 'Lost' }[s] + '</label>';
                    }).join('') + '</div></div>';
                break;
            case 'VERIFICATION':
                body += date + '<div class="fa-field"><span class="fa-label">Result</span><div class="fa-radios"><label><input type="radio" name="rFound" value="1" checked/> Found</label><label><input type="radio" name="rFound" value="0"/> Not found</label></div></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rCond">Condition</label><select id="rCond" class="fa-select"><option value="GOOD">Good</option><option value="FAIR">Fair</option><option value="POOR">Poor</option><option value="UNSERVICEABLE">Unserviceable</option></select></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rBy">Verified by</label><input id="rBy" class="fa-input" maxlength="100"/></div>';
                reasonLabel = 'Remarks'; min = 0;
                break;
            case 'MAINTENANCE':
                body += date + '<div class="fa-field"><label class="fa-label" for="rCost">Cost (UGX) <span class="fa-req">*</span></label><input id="rCost" class="fa-input fa-num-input" inputmode="numeric" value="0"/></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rSup">Supplier or technician</label><input id="rSup" class="fa-input" maxlength="150"/></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rRef">Invoice or job card</label><input id="rRef" class="fa-input" maxlength="150"/></div>' +
                    (R.value ? '<div class="fa-field fa-full"><label class="fa-check-line"><input type="checkbox" id="rCap"/> Capital improvement: adds to the asset\'s value (for example an upgrade that extends its life)</label></div>' : '');
                reasonLabel = 'Work done';
                break;
            case 'DEPRECIATION':
                body += '<div class="fa-field"><label class="fa-label" for="rDate">Charge up to the end of <span class="fa-req">*</span></label><input type="date" id="rDate" class="fa-input" value="' + B.today + '" max="' + B.today + '"/><span class="fa-hint">The charge covers the months after the last one charged, within one financial year.</span></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rAmt">Amount (UGX) <span class="fa-req">*</span></label><input id="rAmt" class="fa-input fa-num-input" inputmode="numeric"/></div>';
                min = 10;
                break;
            case 'REVALUATION':
            case 'APPRECIATION':
                body += date + '<div class="fa-field"><label class="fa-label" for="rVal">New value (UGX) <span class="fa-req">*</span></label><input id="rVal" class="fa-input fa-num-input" inputmode="numeric"/></div>' +
                    (type === 'REVALUATION' ? '<div class="fa-field"><label class="fa-label" for="rValuer">Valuer or committee <span class="fa-req">*</span></label><input id="rValuer" class="fa-input" maxlength="150"/></div>' : '<div></div>') +
                    '<div class="fa-field"><label class="fa-label" for="rRef">Valuation report reference <span class="fa-req">*</span></label><input id="rRef" class="fa-input" maxlength="150"/></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rApr">Approval reference</label><input id="rApr" class="fa-input" maxlength="150"/></div>' +
                    (a.method === 'SL' ? '<div class="fa-field"><label class="fa-label" for="rRem">Remaining useful life (years)</label><input id="rRem" class="fa-input fa-num-input" inputmode="decimal"/><span class="fa-hint">Leave blank to keep the remaining life unchanged.</span></div>' : '');
                min = 10;
                break;
            case 'ESTIMATE':
                body += date + '<div class="fa-field"><span class="fa-label">Method <span class="fa-req">*</span></span><div class="fa-radios">' +
                    '<label><input type="radio" name="rM" value="SL"' + (a.method === 'SL' ? ' checked' : '') + '/> Straight line</label>' +
                    '<label><input type="radio" name="rM" value="RB"' + (a.method === 'RB' ? ' checked' : '') + '/> Reducing balance</label>' +
                    '<label><input type="radio" name="rM" value="NONE"' + (a.method === 'NONE' ? ' checked' : '') + '/> Not depreciated</label></div></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rLife">Total useful life (years)</label><input id="rLife" class="fa-input fa-num-input" value="' + esc(a.lifeYears || '') + '"/><span class="fa-hint">From the start of depreciation, not from today.</span></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rRate">Annual rate (%)</label><input id="rRate" class="fa-input fa-num-input" value="' + esc(a.method === 'RB' ? a.ratePct : '') + '"/></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rRes">Residual value (UGX)</label><input id="rRes" class="fa-input fa-num-input" value="' + esc(a.residualValue) + '"/></div>';
                min = 10;
                break;
            case 'DISPOSAL':
                body += date + '<div class="fa-field"><label class="fa-label" for="rDm">Method <span class="fa-req">*</span></label><select id="rDm" class="fa-select"><option value="SALE">Sale</option><option value="DONATION">Donation</option>' +
                    '<option value="SCRAP">Scrapping</option><option value="TRADE_IN">Trade-in</option><option value="TRANSFER_OUT">Transfer out of the University</option><option value="WRITE_OFF">Write-off (lost or destroyed)</option></select></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rProc">Proceeds (UGX)</label><input id="rProc" class="fa-input fa-num-input" value="0" inputmode="numeric"/></div>' +
                    '<div class="fa-field"><label class="fa-label" for="rApr">Approval reference <span class="fa-req">*</span></label><input id="rApr" class="fa-input" maxlength="150" placeholder="Board of Survey or Council minute"/></div>' +
                    '<div class="fa-field fa-full"><span class="fa-hint" id="rGain"></span></div>';
                min = 10; danger = true;
                break;
            case 'VOID':
                body += '<div class="fa-full fa-notice fa-notice--warn" style="margin:0">Use this only for an asset entered in error, for example a duplicate. The asset stays on file, marked void, and is left out of every total.</div>';
                min = 10; danger = true; reasonLabel = 'Why it was entered in error';
                break;
        }
        body += '</div>';
        if (type === 'DISPOSAL' || type === 'REVALUATION' || type === 'APPRECIATION') body += '<div class="fa-field" style="margin-top:12px"><label class="fa-label" for="rFile">Supporting document</label><input type="file" id="rFile" class="fa-input" style="padding:4px" accept=".pdf,.jpg,.jpeg,.png,.docx,.xlsx"/></div>';

        var modal = FA.reason({ title: TITLES[type], sub: a.assetNo + ', ' + a.name, size: 'mid', extra: body, label: reasonLabel, min: min, ok: TITLES[type], danger: danger },
            function (reason, m, done) { submit(false); function submit(confirmCatchUp) {
                var d = { assetId: a.id, type: type, date: FA.val('rDate') || B.today, reason: reason, clientOpId: modal.op || (modal.op = FA.uuid()), confirmCatchUp: confirmCatchUp };
                switch (type) {
                    case 'TRANSFER':
                        var to = {};
                        if (FA.val('rCampus')) to.campusId = FA.val('rCampus');
                        if (FA.val('rDept')) to.departmentId = FA.val('rDept');
                        if (FA.val('rBld')) to.building = FA.val('rBld');
                        if (FA.val('rRoom')) to.room = FA.val('rRoom');
                        if (FA.val('rCusClear')) to.custodianEmpId = ''; else if (rCus) to.custodianEmpId = rCus.id;
                        if (!Object.keys(to).length) { done('Choose at least one thing to change.'); return; }
                        d.to = to; break;
                    case 'STATUS': d.status = FA.radio('rSt'); break;
                    case 'VERIFICATION': d.found = FA.radio('rFound') === '1'; d.condition = FA.val('rCond'); d.verifiedBy = FA.val('rBy'); break;
                    case 'MAINTENANCE': d.cost = FA.val('rCost'); d.supplier = FA.val('rSup'); d.reference = FA.val('rRef'); d.isCapital = !!FA.val('rCap'); break;
                    case 'DEPRECIATION': d.amount = FA.val('rAmt'); break;
                    case 'REVALUATION': case 'APPRECIATION': d.newValue = FA.val('rVal'); d.valuer = FA.val('rValuer'); d.reference = FA.val('rRef'); d.approvalRef = FA.val('rApr'); d.remainingYears = FA.val('rRem'); break;
                    case 'ESTIMATE': d.method = FA.radio('rM'); d.lifeYears = FA.val('rLife'); d.ratePct = FA.val('rRate'); d.residualValue = FA.val('rRes'); break;
                    case 'DISPOSAL': d.disposalMethod = FA.val('rDm'); d.proceeds = FA.val('rProc'); d.approvalRef = FA.val('rApr'); break;
                }
                FA.call(PAGE, 'AddRecord', { json: JSON.stringify(d) }, function (r) {
                    if (r.needsCatchUp) {
                        FA.confirm({ title: 'Depreciation is due first', message: r.message + '\nIt will be posted together with this record, so the value is right on the date of the ' + TITLES[type].toLowerCase() + '.', ok: 'Post both' },
                            function () { submit(true); });
                        done('Confirm the depreciation to continue.');
                        return;
                    }
                    if (!r.success) { done(r.message); return; }
                    var f = qs('rFile') && qs('rFile').files[0];
                    var finish = function () {
                        done();
                        FA.toast(TITLES[type] + ' recorded.' + (r.warnings && r.warnings.length ? ' ' + r.warnings.join(' ') : ''), false);
                        reloadAsset('records');
                    };
                    if (f) upload(a.id, r.recordId, type === 'DISPOSAL' ? 'DISPOSAL' : 'VALUATION', f, function (u) { if (!u.success) FA.toast('Saved, but the document was not attached: ' + u.message, true); finish(); });
                    else finish();
                });
            } });
        if (type === 'TRANSFER') {
            FA.fill('rCampus', B.campuses, { all: 'Keep as it is' });
            FA.fill('rDept', B.departments, { all: 'Keep as it is' });
            FA.typeahead('rCus', { source: FA.staffSource, render: FA.staffRender, placeholder: 'Keep as it is, or search', onPick: function (it) { rCus = it; } });
        }
        if (type === 'DISPOSAL') {
            var calc = function () {
                var p = Number((FA.val('rProc') || '0').replace(/,/g, '')) || 0, g = p - a.currentValue;
                qs('rGain').innerHTML = 'Carrying amount UGX ' + money(a.currentValue) + '. ' + (g >= 0 ? 'Gain' : 'Loss') + ' on disposal UGX ' + money(Math.abs(g)) +
                    ' (depreciation due up to last month is posted first and reduces the carrying amount).';
            };
            qs('rProc').addEventListener('input', calc); calc();
        }
        return modal;
    }

    // ── Asset form ────────────────────────────────────────────────────
    function catOptions(selected, activeOnly) {
        return '<option value="">Choose</option>' + CATS.filter(function (c) { return !activeOnly || c.effectiveActive; }).map(function (c) {
            var kids = (c.children || []).filter(function (s) { return !activeOnly || s.effectiveActive || s.id === selected; });
            if (!kids.length) return '';
            return '<optgroup label="' + esc(c.code + ' ' + c.name) + '">' + kids.map(function (s) {
                return '<option value="' + s.id + '"' + (s.id === selected ? ' selected' : '') + '>' + esc(s.fullCode + '  ' + s.name) + '</option>';
            }).join('') + '</optgroup>';
        }).join('');
    }

    function assetForm(detail) {
        var a = detail ? detail.asset : null, edit = !!a;
        var lockVal = edit && a.hasValueHistory;
        var f = function (id, label, req, input, hint, full) {
            return '<div class="fa-field' + (full ? ' fa-full' : '') + '"><label class="fa-label" for="' + id + '">' + label + (req ? ' <span class="fa-req">*</span>' : '') + '</label>' + input + (hint ? '<span class="fa-hint">' + hint + '</span>' : '') + '</div>';
        };
        var inp = function (id, val, attrs) { return '<input id="' + id + '" class="fa-input" value="' + esc(val == null ? '' : val) + '" ' + (attrs || '') + '/>'; };
        var h = '<div class="fa-section">Identity</div><div class="fa-form fa-form--3">' +
            f('aNo', 'Asset number', true, inp('aNo', a ? a.assetNo : '', 'maxlength="40"' + (edit && a.assetNoEdits >= 1 ? ' readonly' : '')),
              edit ? (a.assetNoEdits >= 1 ? 'Already changed once; it cannot change again.' : 'Can be changed once, for example to match an engraved number.') : 'Generated from the sub-category. You may change it once.') +
            f('aName', 'Name', true, inp('aName', a ? a.name : '', 'maxlength="200"'), '', false) +
            f('aTag', 'Tag or barcode', false, inp('aTag', a ? a.tagNo : '', 'maxlength="60"'), 'A tag already fixed to the asset, if any.') +
            f('aSerial', 'Serial number', false, inp('aSerial', a ? a.serialNo : '', 'maxlength="100"')) +
            f('aMake', 'Make or brand', false, inp('aMake', a ? a.make : '', 'maxlength="100"')) +
            f('aModel', 'Model', false, inp('aModel', a ? a.model : '', 'maxlength="100"')) +
            f('aDesc', 'Description', false, '<textarea id="aDesc" class="fa-textarea" rows="2" maxlength="1000">' + esc(a ? a.description : '') + '</textarea>', '', true) +
            '</div><div class="fa-section">Classification</div><div class="fa-form fa-form--3">' +
            f('aSub', 'Sub-category', true, '<select id="aSub" class="fa-select">' + catOptions(a ? a.categoryId : 0, true) + '</select>') +
            '<div class="fa-field"><span class="fa-label">Type</span><div class="fa-radios"><label><input type="radio" name="aType" value="TANGIBLE"' + (!a || a.assetType !== 'INTANGIBLE' ? ' checked' : '') + '/> Tangible</label>' +
            '<label><input type="radio" name="aType" value="INTANGIBLE"' + (a && a.assetType === 'INTANGIBLE' ? ' checked' : '') + '/> Intangible</label></div></div>' +
            f('aQty', 'Quantity', false, inp('aQty', a ? a.quantity : 1, 'type="number" min="1" step="1"'), 'More than 1 for a lot, for example 120 chairs.') + '</div>';

        h += '<div class="fa-section">Location and custody</div>';
        if (edit) {
            h += '<div class="fa-notice">' + esc([a.campus, a.building, a.room, a.department].filter(Boolean).join(', ')) + (a.custodian ? '. Responsible person: ' + esc(a.custodian) : '. No responsible person') +
                 '. To change the location or responsible person, record a transfer.</div>';
        } else {
            h += '<div class="fa-form fa-form--3">' + f('aCampus', 'Campus', true, '<select id="aCampus" class="fa-select"></select>') +
                f('aBld', 'Building or block', false, inp('aBld', '', 'maxlength="120"')) +
                f('aRoom', 'Room or office', false, inp('aRoom', '', 'maxlength="120" list="aRooms"') + '<datalist id="aRooms"></datalist>') +
                f('aDept', 'Department', false, '<select id="aDept" class="fa-select"></select>') +
                '<div class="fa-field"><label class="fa-label">Responsible person</label><div id="aCus"></div></div>' +
                '<div class="fa-field"><span class="fa-label">Status <span class="fa-req">*</span></span><div class="fa-radios"><label><input type="radio" name="aSt" value="IN_USE" checked/> In use</label><label><input type="radio" name="aSt" value="IN_STORE"/> In store</label></div></div></div>';
        }

        h += '<div class="fa-section">Acquisition</div><div class="fa-form fa-form--3">' +
            f('aPd', 'Purchase date', true, inp('aPd', a ? a.purchaseDate : '', 'type="date" max="' + B.today + '"' + (lockVal ? ' readonly' : ''))) +
            f('aCost', 'Original purchase value (UGX)', true, inp('aCost', a ? money(a.originalCost) : '', 'inputmode="numeric" class="fa-input fa-num-input"' + (lockVal ? ' readonly' : '')), lockVal ? 'Fixed once the asset has value records.' : '') +
            '<div class="fa-field"><label class="fa-label">Supplier</label><div id="aSup"></div></div>' +
            f('aInv', 'Invoice reference', false, inp('aInv', a ? a.invoiceRef : '', 'maxlength="100"')) +
            f('aOrd', 'Order or LPO reference', false, inp('aOrd', a ? a.orderRef : '', 'maxlength="100"')) +
            f('aReq', 'Requisition number', false, inp('aReq', a ? a.requisitionRef : '', 'maxlength="40"')) +
            f('aFund', 'Funding source', false, inp('aFund', a ? a.fundingSource : '', 'maxlength="120" list="aFunds"') + '<datalist id="aFunds"><option value="University funds"><option value="Donation"><option value="Government grant"><option value="Development partner"></datalist>') +
            f('aWar', 'Warranty ends', false, inp('aWar', a ? a.warrantyExpiry : '', 'type="date"')) + '<div></div></div>';

        h += '<div class="fa-section">Depreciation</div>' + (lockVal ? '<div class="fa-notice">Depreciation has been posted, so these settings are changed with a Change of estimate record, not here.</div>' : '') +
            '<div class="fa-form fa-form--3"><div class="fa-field fa-full"><span class="fa-label">Method</span><div class="fa-radios">' +
            '<label><input type="radio" name="aM" value="SL"/> Straight line</label><label><input type="radio" name="aM" value="RB"/> Reducing balance</label><label><input type="radio" name="aM" value="NONE"/> Not depreciated</label></div>' +
            '<span class="fa-inherit" id="aDefaults"></span></div>' +
            f('aLife', 'Useful life (years)', false, inp('aLife', a ? a.lifeYears : '', 'inputmode="decimal" class="fa-input fa-num-input"' + (lockVal ? ' readonly' : ''))) +
            f('aRate', 'Annual rate (%)', false, inp('aRate', a && a.method === 'RB' ? a.ratePct : '', 'inputmode="decimal" class="fa-input fa-num-input"' + (lockVal ? ' readonly' : ''))) +
            f('aRes', 'Residual value (UGX)', false, inp('aRes', a ? money(a.residualValue) : '', 'inputmode="numeric" class="fa-input fa-num-input"' + (lockVal ? ' readonly' : ''))) +
            f('aDs', 'Depreciation starts', false, inp('aDs', a ? a.depStartDate : '', 'type="date"' + (lockVal ? ' readonly' : '')), 'Blank means the purchase date. Later if the asset was not yet in use.') + '</div>';

        if (!edit) {
            h += '<div class="fa-section">Opening balance</div><label class="fa-check-line"><input type="checkbox" id="aOpen"/> Bought before the register started (enter its value as at the opening date)</label>' +
                 '<div class="fa-form fa-form--3" id="aOpenBox" style="display:none;margin-top:10px">' +
                 f('aOd', 'Opening date', true, inp('aOd', '', 'type="date" max="' + B.today + '"'), 'Normally 31 July, the end of the last closed year.') +
                 '<div class="fa-field"><span class="fa-label">Depreciation to the opening date</span><div class="fa-radios"><label><input type="radio" name="aOc" value="calc" checked/> Calculate it</label><label><input type="radio" name="aOc" value="typed"/> Enter it</label></div></div>' +
                 f('aOa', 'Accumulated depreciation (UGX)', false, inp('aOa', '', 'inputmode="numeric" class="fa-input fa-num-input" disabled')) + '</div>';
        }
        h += '<div class="fa-section">Notes</div><textarea id="aNotes" class="fa-textarea" rows="2">' + esc(a ? a.notes : '') + '</textarea>';
        if (edit) h += '<div class="fa-field" style="margin-top:12px"><label class="fa-label" for="aReason">Reason for the change <span class="fa-req">*</span></label><input id="aReason" class="fa-input" maxlength="500"/></div>';
        h += '<div class="fa-error" id="aErr" style="margin-top:10px"></div>';

        var m = FA.modal({ title: edit ? 'Edit ' + a.assetNo : 'New asset', sub: edit ? a.name : 'Required fields are marked', size: 'wide', sticky: true, body: h,
                           foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn fa-btn--primary" data-ok>' + (edit ? 'Save changes' : 'Create asset') + '</button>' });
        var method = a ? a.method : '';
        function setMethod(v) { Array.prototype.forEach.call(document.querySelectorAll('input[name=aM]'), function (r) { r.checked = r.value === v; r.disabled = lockVal; }); syncMethod(); }
        function syncMethod() {
            var v = FA.radio('aM');
            qs('aLife').closest('.fa-field').style.display = v === 'SL' ? '' : 'none';
            qs('aRate').closest('.fa-field').style.display = v === 'RB' ? '' : 'none';
            qs('aRes').closest('.fa-field').style.display = v === 'NONE' ? 'none' : '';
        }
        Array.prototype.forEach.call(document.querySelectorAll('input[name=aM]'), function (r) { r.onchange = syncMethod; });
        var supPick = FA.typeahead('aSup', { source: FA.supplierSource, free: true, placeholder: 'Search or type a name', value: a && a.supplierName ? { id: a.supplierId || 0, name: a.supplierName } : null });
        var cusPick = null;
        if (!edit) {
            FA.fill('aCampus', B.campuses, { all: 'Choose' });
            FA.fill('aDept', B.departments, { all: 'None' });
            cusPick = FA.typeahead('aCus', { source: FA.staffSource, render: FA.staffRender, placeholder: 'Name or staff number' });
            qs('aCampus').onchange = function () {
                var cp = parseInt(qs('aCampus').value, 10);
                qs('aRooms').innerHTML = (B.rooms || []).filter(function (r) { return !cp || r.campusId === cp; }).map(function (r) { return '<option value="' + esc(r.name) + '">'; }).join('');
            };
            qs('aOpen').onchange = function () { qs('aOpenBox').style.display = qs('aOpen').checked ? '' : 'none'; };
            Array.prototype.forEach.call(document.querySelectorAll('input[name=aOc]'), function (r) { r.onchange = function () { qs('aOa').disabled = FA.radio('aOc') !== 'typed'; }; });
        }
        function applyDefaults(sub, fill) {
            if (!sub) { qs('aDefaults').textContent = ''; return; }
            var e = sub.effective;
            qs('aDefaults').textContent = 'Sub-category default: ' + e.methodLabel + (e.method === 'SL' && e.lifeYears ? ', ' + e.lifeYears + ' years' : '') +
                (e.method === 'RB' && e.ratePct ? ', ' + e.ratePct + '% a year' : '') + (e.residualPct ? ', residual ' + e.residualPct + '% of cost' : '') +
                (e.capThreshold ? '. Assets below UGX ' + money(e.capThreshold) + ' are normally expensed.' : '.');
            if (fill) {
                setMethod(e.method);
                qs('aLife').value = e.lifeYears || '';
                qs('aRate').value = e.method === 'RB' ? (e.ratePct || '') : '';
                document.querySelector('input[name=aType][value=' + (e.assetType || 'TANGIBLE') + ']').checked = true;
            }
        }
        qs('aSub').onchange = function () {
            var s = SUBS[qs('aSub').value];
            applyDefaults(s, !edit || !lockVal);
            if (!edit && s) FA.call(PAGE, 'NextAssetNo', { subCategoryId: s.id }, function (r) { if (r.success) qs('aNo').value = r.assetNo; });
        };
        if (a) { setMethod(a.method); applyDefaults(SUBS[a.categoryId], false); } else setMethod('SL');
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        var op = FA.uuid();
        m.foot.querySelector('[data-ok]').onclick = function () {
            var btn = this;
            var num = function (id) { return FA.val(id).replace(/,/g, ''); };
            var d = {
                id: a ? a.id : 0, rowVersion: a ? a.rowVersion : 0, clientOpId: op, assetNo: FA.val('aNo'), name: FA.val('aName'), tagNo: FA.val('aTag'),
                serialNo: FA.val('aSerial'), make: FA.val('aMake'), model: FA.val('aModel'), description: FA.val('aDesc'), categoryId: FA.val('aSub'),
                assetType: FA.radio('aType'), quantity: FA.val('aQty'), purchaseDate: FA.val('aPd'), originalCost: num('aCost'),
                supplierName: supPick.get() ? supPick.get().name : '', supplierId: supPick.get() ? supPick.get().id : '',
                invoiceRef: FA.val('aInv'), orderRef: FA.val('aOrd'), requisitionRef: FA.val('aReq'), fundingSource: FA.val('aFund'), warrantyExpiry: FA.val('aWar'),
                method: FA.radio('aM'), lifeYears: num('aLife'), ratePct: num('aRate'), residualValue: num('aRes'), depStartDate: FA.val('aDs'), notes: FA.val('aNotes'),
                reason: edit ? FA.val('aReason') : ''
            };
            if (!edit) {
                d.campusId = FA.val('aCampus'); d.building = FA.val('aBld'); d.room = FA.val('aRoom'); d.departmentId = FA.val('aDept');
                d.custodianEmpId = cusPick.get() ? cusPick.get().id : ''; d.status = FA.radio('aSt');
                if (FA.val('aOpen')) { d.opening = true; d.openingDate = FA.val('aOd'); d.openingAccumDep = FA.radio('aOc') === 'calc' ? 'CALC' : num('aOa'); }
            }
            var miss = [];
            if (!d.name) miss.push('name'); if (!d.categoryId) miss.push('sub-category'); if (!edit && !d.campusId) miss.push('campus');
            if (!d.purchaseDate) miss.push('purchase date'); if (!d.originalCost) miss.push('purchase value'); if (edit && d.reason.length < 5) miss.push('reason for the change');
            if (miss.length) { qs('aErr').textContent = 'Fill in: ' + miss.join(', ') + '.'; return; }
            FA.busy(btn, true);
            FA.call(PAGE, 'SaveAsset', { json: JSON.stringify(d) }, function (r) {
                FA.busy(btn, false);
                if (!r.success) { qs('aErr').textContent = r.message; return; }
                m.close();
                FA.toast((edit ? 'Changes saved.' : 'Asset ' + r.assetNo + ' created.') + (r.warnings && r.warnings.length ? ' ' + r.warnings.join(' ') : ''), r.warnings && r.warnings.length > 0);
                load();
                openAsset(r.id);
            });
        };
        setTimeout(function () { qs(edit ? 'aName' : 'aSub').focus(); }, 50);
    }

    // ── Start ─────────────────────────────────────────────────────────
    initFilters();
    headerActions();
    load();
    var u = FA.readUrl();
    if (u.open) openAsset(parseInt(u.open, 10));
    if (u['new'] === '1' && R.edit) assetForm(null);
})();
