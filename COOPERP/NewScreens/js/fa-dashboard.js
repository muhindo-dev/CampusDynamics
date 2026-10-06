/* Fixed Assets: dashboard. Figures and charts; every figure opens the register filtered to it. ES5. */
(function () {
    'use strict';
    var B = window.FA_BOOT || {}, PAGE = 'AssetsDashboard.aspx', esc = FA.esc, money = FA.money, qs = FA.qs;
    var CATS = B.tree || [], charts = {};
    var NAVY = '#05275C', ACCENT = '#174DA4', GOLD = '#D4A017';
    if (window.Chart) { Chart.defaults.animation = false; Chart.defaults.font.family = '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'; Chart.defaults.color = '#4b5363'; }
    var STATUS_COLOURS = { IN_USE: '#05275C', IN_STORE: '#174DA4', UNDER_REPAIR: '#D4A017', LOST: '#b42318', DISPOSED: '#9aa3b2', WRITTEN_OFF: '#6b7280' };

    function fillSubs() {
        var cat = qs('fCat').value, items = [];
        CATS.forEach(function (c) { if (cat && String(c.id) !== cat) return; (c.children || []).forEach(function (s) { items.push({ id: s.id, name: (cat ? '' : c.code + ' / ') + s.name }); }); });
        FA.fill('fSub', items, { all: 'All sub-categories' });
    }
    FA.fill('fCampus', B.campuses, { all: 'All campuses' });
    FA.fill('fCat', CATS, { all: 'All categories' });
    var u = FA.readUrl();
    if (u.campus) qs('fCampus').value = u.campus;
    if (u.cat) qs('fCat').value = u.cat;
    fillSubs(); if (u.sub) qs('fSub').value = u.sub;
    qs('fFrom').value = u.from || B.fyStart; qs('fTo').value = u.to || B.today; qs('fTo').max = B.today;
    qs('fCat').onchange = function () { fillSubs(); load(); };
    ['fCampus', 'fSub', 'fFrom', 'fTo'].forEach(function (id) { qs(id).onchange = load; });
    qs('btnApply').onclick = load;
    qs('btnReset').onclick = function () { qs('fCampus').value = ''; qs('fCat').value = ''; fillSubs(); qs('fFrom').value = B.fyStart; qs('fTo').value = B.today; load(); };

    /** Link to the register with the dashboard's filters plus extras. */
    function link(extra) {
        var p = { campus: qs('fCampus').value, cat: qs('fCat').value, sub: qs('fSub').value };
        Object.keys(extra || {}).forEach(function (k) { p[k] = extra[k]; });
        var parts = [];
        Object.keys(p).forEach(function (k) { if (p[k] !== '' && p[k] !== undefined && p[k] !== null) parts.push(k + '=' + encodeURIComponent(p[k])); });
        return 'Assets.aspx' + (parts.length ? '?' + parts.join('&') : '');
    }

    function tile(label, value, sub, href, warn) {
        return '<a class="fa-kpi' + (warn ? ' fa-kpi--warn' : '') + '" href="' + esc(href) + '"><div class="fa-kpi__label">' + esc(label) + '</div>' +
               '<div class="fa-kpi__value" title="' + esc(value) + '">' + esc(value) + '</div><div class="fa-kpi__sub">' + esc(sub) + '</div></a>';
    }

    function chart(id, cfg) {
        if (charts[id]) charts[id].destroy();
        var el = qs(id); if (!el || !window.Chart) return;
        charts[id] = new Chart(el, cfg);
    }
    var axisMoney = { ticks: { callback: function (v) { return FA.short(v); } }, grid: { color: '#eef1f5' } };
    var tipMoney = function (c) { return (c.dataset.label ? c.dataset.label + ': ' : '') + 'UGX ' + money(c.parsed.x !== undefined && c.chart.config.options.indexAxis === 'y' ? c.parsed.x : c.parsed.y); };

    function load() {
        var f = { campusId: qs('fCampus').value, categoryId: qs('fCat').value, subCategoryId: qs('fSub').value, from: qs('fFrom').value, to: qs('fTo').value };
        FA.writeUrl({ campus: f.campusId, cat: f.categoryId, sub: f.subCategoryId, from: f.from === B.fyStart ? '' : f.from, to: f.to === B.today ? '' : f.to });
        FA.call(PAGE, 'GetDashboard', { filterJson: JSON.stringify(f) }, function (r) {
            if (!r.success) { qs('dTiles').innerHTML = '<div class="fa-notice fa-notice--bad">' + esc(r.message) + '</div>'; return; }
            render(r);
        });
    }

    function render(r) {
        var t = r.tiles;
        qs('dNote').innerHTML = r.historic ? '<div class="fa-notice">Values are as at ' + esc(r.asAt) + ', worked out from the records on that date.</div>'
            : (t.count === 0 ? '<div class="fa-notice">The register is empty. Add assets on the Assets screen, or import the opening register from Excel.</div>' : '');
        qs('dTiles').innerHTML =
            tile('Assets held', money(t.count), 'Excluding disposed and void', link({ status: 'OPEN' })) +
            tile('Original cost', 'UGX ' + FA.short(t.cost), money(t.cost), link({ status: 'OPEN', sort: 'cost', dir: 'desc' })) +
            tile('Book value', 'UGX ' + FA.short(t.value), money(t.value) + (t.cost ? ', ' + (t.changePct >= 0 ? 'up ' : 'down ') + Math.abs(t.changePct) + '% on cost' : ''), link({ status: 'OPEN', sort: 'value', dir: 'desc' })) +
            tile('Accumulated depreciation', 'UGX ' + FA.short(t.accumDep), money(t.accumDep), 'AssetRecords.aspx?view=dep') +
            tile('Depreciation in period', 'UGX ' + FA.short(t.depreciation), r.from + ' to ' + r.asAt, 'AssetRecords.aspx') +
            tile('Acquired in period', money(t.additionsCount), 'UGX ' + money(t.additionsCost), link({ bfrom: qs('fFrom').value, bto: qs('fTo').value, status: 'ALL' })) +
            tile('Disposed in period', money(t.disposedCount), 'Proceeds UGX ' + money(t.disposedProceeds), link({ status: 'CLOSED' }), t.disposedCount > 0) +
            tile('Revaluation surplus', 'UGX ' + FA.short(t.revalSurplus), money(t.revalSurplus), link({ status: 'OPEN' }));

        // Book value by category (horizontal bar)
        chart('cCat', {
            type: 'bar',
            data: { labels: r.byCategory.map(function (c) { return c.name; }),
                    datasets: [{ label: 'Book value', data: r.byCategory.map(function (c) { return c.value; }), backgroundColor: NAVY, borderWidth: 0, barThickness: 16 },
                               { label: 'Cost', data: r.byCategory.map(function (c) { return c.cost; }), backgroundColor: '#c9d6ea', borderWidth: 0, barThickness: 16 }] },
            options: { indexAxis: 'y', responsive: true, maintainAspectRatio: false,
                       plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } }, tooltip: { callbacks: { label: function (c) { return c.dataset.label + ': UGX ' + money(c.parsed.x); } } } },
                       scales: { x: axisMoney, y: { grid: { display: false } } },
                       onClick: function (e, el) { if (el.length) location.href = link({ cat: r.byCategory[el[0].index].id, sub: '', status: 'OPEN' }); } }
        });
        // Status (doughnut)
        var st = r.byStatus.filter(function (s) { return s.count > 0; });
        chart('cStatus', {
            type: 'doughnut',
            data: { labels: st.map(function (s) { return s.label + ' (' + money(s.count) + ')'; }),
                    datasets: [{ data: st.map(function (s) { return s.count; }), backgroundColor: st.map(function (s) { return STATUS_COLOURS[s.status]; }), borderWidth: 1, borderColor: '#fff' }] },
            options: { responsive: true, maintainAspectRatio: false, cutout: '58%', plugins: { legend: { position: 'right', labels: { boxWidth: 12 } } },
                       onClick: function (e, el) { if (el.length) location.href = link({ status: st[el[0].index].status }); } }
        });
        // Years: bars of acquisitions, line of book value at year end
        chart('cYear', {
            type: 'bar',
            data: { labels: r.byYear.map(function (y) { return y.finYear + (y.partial ? ' (to date)' : ''); }),
                    datasets: [{ type: 'line', label: 'Book value at year end', data: r.byYear.map(function (y) { return y.bookValue; }), borderColor: GOLD, backgroundColor: GOLD, borderWidth: 2, pointRadius: 3, tension: 0, yAxisID: 'y' },
                               { type: 'bar', label: 'Acquired (cost)', data: r.byYear.map(function (y) { return y.cost; }), backgroundColor: ACCENT, borderWidth: 0, yAxisID: 'y' }] },
            options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } }, tooltip: { callbacks: { label: function (c) { return c.dataset.label + ': UGX ' + money(c.parsed.y); } } } },
                       scales: { y: axisMoney, x: { grid: { display: false } } },
                       onClick: function (e, el) { if (el.length) location.href = link({ fy: r.byYear[el[0].index].finYear }); } }
        });
        // Campus
        chart('cCampus', {
            type: 'bar',
            data: { labels: r.byCampus.map(function (c) { return c.name; }),
                    datasets: [{ label: 'Book value', data: r.byCampus.map(function (c) { return c.value; }), backgroundColor: NAVY, borderWidth: 0, maxBarThickness: 60, yAxisID: 'y' },
                               { label: 'Assets', data: r.byCampus.map(function (c) { return c.count; }), backgroundColor: '#c9d6ea', borderWidth: 0, maxBarThickness: 60, yAxisID: 'y2' }] },
            options: { responsive: true, maintainAspectRatio: false,
                       plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } }, tooltip: { callbacks: { label: function (c) { return c.datasetIndex === 0 ? 'Book value: UGX ' + money(c.parsed.y) : 'Assets: ' + money(c.parsed.y); } } } },
                       scales: { y: axisMoney, y2: { position: 'right', grid: { display: false }, ticks: { precision: 0 } }, x: { grid: { display: false } } },
                       onClick: function (e, el) { if (el.length) location.href = link({ campus: r.byCampus[el[0].index].id, status: 'OPEN' }); } }
        });

        // Action lists
        var order = ['no_custodian', 'not_tagged', 'revaluation_due', 'life_ended', 'not_verified'];
        qs('dActions').innerHTML = order.map(function (k) {
            var a = r.actions[k];
            return '<div class="fa-action"><div class="fa-action__head"><span class="fa-action__title">' + esc(a.label) + '</span><span class="fa-action__count' + (a.count ? ' is-warn' : '') + '">' + money(a.count) + '</span></div>' +
                '<ul>' + (a.rows.length ? a.rows.map(function (x) { return '<li><a href="Assets.aspx?open=' + x.id + '&flag=' + k + '"><span class="fa-code">' + esc(x.assetNo) + '</span> ' + esc(x.name) + '</a></li>'; }).join('')
                                        : '<li><span style="display:block;padding:6px 12px;color:#6b7280">None</span></li>') + '</ul>' +
                (a.count ? '<div class="fa-action__foot"><a href="' + esc(link({ flag: k, status: 'OPEN' })) + '">View all ' + money(a.count) + '</a></div>' : '') + '</div>';
        }).join('');

        qs('dTop').innerHTML = r.top.length ? r.top.map(function (x, i) {
            return '<tr class="is-click" data-id="' + x.id + '"><td>' + (i + 1) + '</td><td class="fa-code">' + esc(x.assetNo) + '</td><td>' + esc(x.name) + '</td><td>' + esc(x.category) + '</td><td class="fa-num">' + money(x.value) + '</td></tr>';
        }).join('') : '<tr><td colspan="5" class="fa-empty">No assets.</td></tr>';
        Array.prototype.forEach.call(qs('dTop').querySelectorAll('tr[data-id]'), function (tr) { tr.onclick = function () { location.href = 'Assets.aspx?open=' + tr.getAttribute('data-id'); }; });
    }

    load();
})();
