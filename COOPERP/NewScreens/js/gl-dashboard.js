/* General Ledger: Accounts Dashboard. Every figure links to the report it comes from. */
(function () {
    'use strict';
    var FA = window.FA, GL = window.GL, B = window.GL_BOOT || {};
    if (B.error) { FA.qs('dErr').innerHTML = '<div class="fa-notice fa-notice--bad">' + FA.esc(B.error) + '</div>'; FA.qs('dNote').style.display = 'none'; return; }

    FA.qs('dNote').innerHTML = 'Figures for ' + FA.esc(B.period.label) + ' (' + FA.esc(B.period.from) + ' to ' + FA.esc(B.period.to) + '), read from the ledger at ' + FA.esc(B.built) +
        '. Student fees owed use the canonical balance, the figure on student statements. Nothing here changes the ledger.';

    FA.qs('dTiles').innerHTML = B.tiles.map(function (t) {
        return '<a class="fa-kpi' + (t.warn ? ' fa-kpi--warn' : '') + '" href="' + FA.esc(t.link) + '"><div class="fa-kpi__label">' + FA.esc(t.label) + '</div>' +
            '<div class="fa-kpi__value' + (/^\(/.test(t.value) ? ' gl-neg' : '') + '">' + FA.esc(t.value) + '</div><div class="fa-kpi__sub">' + FA.esc(t.sub) + '</div></a>';
    }).join('');

    // Monthly bars: income and expenditure side by side
    var max = 1;
    B.months.forEach(function (m) { max = Math.max(max, Math.abs(m.income), Math.abs(m.expense)); });
    FA.qs('dMonthsSub').textContent = 'Scale up to ' + GL.short(max);
    FA.qs('dMonths').innerHTML = !B.months.length ? '<div class="fa-empty">No months yet.</div>' :
        '<div class="gl-bars">' + B.months.map(function (m) {
            var hi = Math.max(0, m.income) * 100 / max, he = Math.max(0, m.expense) * 100 / max;
            return '<a class="gl-bars__col" href="' + FA.esc(m.link) + '" title="' + FA.esc(m.label + ': income ' + m.incomeText + ', expenditure ' + m.expenseText) + '" style="text-decoration:none">' +
                '<div class="gl-bars__pair"><span class="gl-bars__in" style="height:' + hi.toFixed(1) + '%"></span><span class="gl-bars__out" style="height:' + he.toFixed(1) + '%"></span></div>' +
                '<div class="gl-bars__lbl">' + FA.esc(m.m) + '</div></a>';
        }).join('') + '</div><div class="gl-legend"><span><i class="gl-bars__in"></i>Income</span><span><i class="gl-bars__out"></i>Expenditure</span></div>';

    FA.qs('dSpend').innerHTML = !B.spend.length ? '<div class="fa-empty">No spending yet.</div>' : B.spend.map(function (s) {
        return '<div class="gl-hbar"><a href="' + FA.esc(s.link) + '">' + FA.esc(s.name) + '</a><b class="fa-num">' + FA.esc(s.amount) + '</b><span class="fa-bar"><span style="width:' + Math.max(1, s.share) + '%"></span></span></div>';
    }).join('');

    FA.qs('dTop').innerHTML = '<table class="fa-table"><tbody>' + B.topExpense.map(function (x) {
        return '<tr class="is-click" data-l="' + FA.esc(x.link) + '"><td><span class="fa-code">' + FA.esc(x.code) + '</span> ' + FA.esc(x.name) + '</td><td class="fa-num">' + FA.esc(x.amount) + '</td></tr>';
    }).join('') + '</tbody></table>';

    FA.qs('dBanks').innerHTML = !B.banks.length ? '<div class="fa-empty">No balances.</div>' : '<table class="fa-table"><tbody>' + B.banks.map(function (b) {
        return '<tr class="is-click" data-l="' + FA.esc(b.link) + '"><td><span class="fa-code">' + FA.esc(b.code) + '</span> ' + FA.esc(b.name) + '</td><td class="fa-num' + (b.raw < 0 ? ' gl-neg' : '') + '">' + FA.esc(b.bal) + '</td></tr>';
    }).join('') + '</tbody></table>';

    var w = '';
    if (B.health >= 0) w += '<div class="gl-health" style="margin-bottom:12px">' + GL.ring(B.health) + '<div><div class="gl-health__num">' + B.health + '<span style="font-size:13px;color:var(--fa-muted)"> / 100</span></div><div class="gl-health__sub">Health score. Last checked ' + FA.esc(B.lastRun) + '.</div></div></div>';
    if (B.trend && B.trend.length > 1) w += '<div class="fa-label">Open warnings over the last ' + B.trend.length + ' runs</div>' + GL.spark(B.trend.map(function (x) { return x.open; }), '#b42318');
    w += (B.warnings || []).map(function (x) {
        return '<div class="fa-row" style="padding:7px 0;border-bottom:1px solid var(--fa-border);flex-wrap:nowrap">' + GL.sev(x.severity) + '<a class="gl-link" style="flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap" href="AccountsWarnings.aspx?id=' + x.id + '">' + FA.esc(x.title) + '</a><b class="fa-num">' + FA.esc(x.amount) + '</b></div>';
    }).join('');
    FA.qs('dWarn').innerHTML = w || '<div class="fa-muted">No open warnings.</div>';

    Array.prototype.forEach.call(document.querySelectorAll('tr[data-l]'), function (tr) { tr.onclick = function () { location.href = tr.getAttribute('data-l'); }; });
})();
