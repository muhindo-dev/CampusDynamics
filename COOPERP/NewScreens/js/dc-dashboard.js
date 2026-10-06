/* Student Disciplinary module: dashboard. Tiles, attention list and charts; every figure opens Disciplinary Records filtered to it. ES5. */
(function () {
    'use strict';
    var B = window.DC_BOOT || {}, PAGE = 'DisciplinaryDashboard.aspx', esc = FA.esc, qs = FA.qs, charts = {};
    var NAVY = '#05275C', ACCENT = '#174DA4', GOLD = '#D4A017', RED = '#b42318', GREY = '#9aa3b2';
    var PALETTE = [NAVY, ACCENT, GOLD, '#4b7bc7', '#7a8cab', '#2f6f4f', '#b06f00', '#5b2a86', '#8aa4cf', '#c9a227', '#3d5a80', '#98c1d9'];

    FA.fill('fCampus', B.campuses, { all: 'All campuses' });
    FA.fill('fFac', B.faculties, { all: 'All faculties' });
    FA.fill('fYear', (B.years || []).map(function (y) { return { id: y, name: y }; }), { all: 'All years' });
    FA.fill('fType', B.types, { all: 'All types' });
    var u = FA.readUrl();
    FA.set('fCampus', u.campus || ''); FA.set('fFac', u.faculty || ''); FA.set('fYear', u.year || ''); FA.set('fType', u.type || '');
    FA.set('fFrom', u.from || B.from); FA.set('fTo', u.to || B.today);

    function filt() { return { campus: FA.val('fCampus'), faculty: FA.val('fFac'), year: FA.val('fYear'), type: FA.val('fType'), from: FA.val('fFrom'), to: FA.val('fTo') }; }
    function recUrl(extra) {
        var f = filt(), o = { campus: f.campus, faculty: f.faculty, year: f.year, type: f.type };
        Object.keys(extra || {}).forEach(function (k) { o[k] = extra[k]; });
        var parts = []; Object.keys(o).forEach(function (k) { if (o[k]) parts.push(encodeURIComponent(k) + '=' + encodeURIComponent(o[k])); });
        return 'DisciplinaryRecords.aspx' + (parts.length ? '?' + parts.join('&') : '');
    }
    qs('btnApply').onclick = load;
    qs('btnReset').onclick = function () { ['fCampus', 'fFac', 'fYear', 'fType'].forEach(function (i) { FA.set(i, ''); }); FA.set('fFrom', B.from); FA.set('fTo', B.today); load(); };

    function tile(label, value, sub, href, warn) {
        return '<a class="fa-kpi' + (warn ? ' fa-kpi--warn' : '') + '" href="' + href + '"><div class="fa-kpi__label">' + esc(label) + '</div><div class="fa-kpi__value">' + esc(value) + '</div>' +
               (sub ? '<div class="fa-kpi__sub">' + esc(sub) + '</div>' : '') + '</a>';
    }

    function load() {
        FA.writeUrl(filt());
        FA.call(PAGE, 'GetDashboard', { filterJson: JSON.stringify(filt()) }, function (r) {
            if (!r.success) { qs('dTiles').innerHTML = '<div class="fa-notice fa-notice--bad">' + esc(r.message) + '</div>'; return; }
            var t = r.tiles, a = r.attention;
            qs('dTiles').innerHTML =
                tile('Open cases', FA.money(t.open), t.restrictedOpen ? t.restrictedOpen + ' restricted' : '', recUrl({ status: 'open' })) +
                tile('Awaiting hearing', FA.money(t.awaitingHearing), 'Reported to summoned', recUrl({ status: 'awaiting_hearing' })) +
                tile('Awaiting decision', FA.money(t.awaitingDecision), 'Heard', recUrl({ status: 'awaiting_decision' })) +
                tile('Under appeal', FA.money(t.underAppeal), '', recUrl({ status: 'UNDER_APPEAL' })) +
                tile('Reported in period', FA.money(t.opened), '', recUrl({})) +
                tile('Closed in period', FA.money(t.closed), '', recUrl({ status: 'CLOSED' })) +
                tile('Days to decision', t.avgDays === null ? 'None' : String(t.avgDays), 'Average, decided in period', recUrl({ status: 'DECIDED' }));
            qs('dAttention').innerHTML =
                tile('No update in ' + a.staleDays + ' days', FA.money(a.stale), 'Open cases', recUrl({ flag: 'overdue' }), a.stale > 0) +
                tile('Hearing date passed', FA.money(a.hearingPassed), 'No outcome recorded', recUrl({ flag: 'hearing_passed' }), a.hearingPassed > 0) +
                tile('Appeal window closing', FA.money(a.appealClosing), 'Within ' + a.appealDays + ' days', recUrl({ flag: 'appeal_closing' }), a.appealClosing > 0) +
                tile('Hearings this week', FA.money(a.hearingsWeek), '', recUrl({ sort: 'hearing', status: 'awaiting_hearing' })) +
                tile('Marks action owed', FA.money(a.followMarks), 'Cancellations to carry out', recUrl({ flag: 'follow_marks' }), a.followMarks > 0) +
                tile('Fees action owed', FA.money(a.followFees), 'Fines and restitution to bill', recUrl({ flag: 'follow_fees' }), a.followFees > 0) +
                tile('Notices not read', FA.money(a.unread), 'After 7 days', recUrl({ flag: 'unread' }), a.unread > 0) +
                tile('Emails not delivered', FA.money(a.emailFailed), 'Failed or no address', recUrl({ flag: 'email_failed' }), a.emailFailed > 0) +
                (a.unrecorded ? tile('Endings not yet recorded', FA.money(a.unrecorded), 'Recorded on the next nightly run', recUrl({}), true) : '');

            bar('cType', r.byType, true, function (p) { return recUrl({ type: p.key, from: '', to: '' }); });
            bar('cStatus', r.byStatus, true, function (p) { return recUrl({ status: p.key }); });
            line('cMonth', r.perMonth);
            bar('cYear', r.perYear, false, function (p) { return recUrl({ year: p.key }); });
            doughnut('cSev', r.bySeverity, { MINOR: GREY, SERIOUS: GOLD, GROSS: RED }, function (p) { return recUrl({ severity: p.key }); });
            doughnut('cCampus', r.byCampus, null, function (p) { return p.key && p.key !== '0' ? recUrl({ campus: p.key }) : null; });
            bar('cFac', r.byFaculty, true, function (p) { return p.key ? recUrl({ faculty: p.key }) : null; });

            qs('dEffects').textContent = (r.effects || []).map(function (e) { return e.label + ' ' + e.value; }).join(', ');
            qs('dSanctions').innerHTML = r.underSanction.length ? r.underSanction.map(function (s) {
                return '<tr class="is-click" data-href="' + DC.caseLink(s.id) + '"><td><b>' + esc(s.name) + '</b><span class="fa-sub">' + esc(s.regno) + ', ' + esc(s.caseNo) + '</span></td><td>' + esc(s.sanction) + '</td><td>' + esc(s.from) + '</td><td>' + esc(s.to) + '</td></tr>';
            }).join('') : '<tr><td colspan="4" class="fa-empty">No student is under a restriction.</td></tr>';
            qs('dHearings').innerHTML = r.upcoming.length ? r.upcoming.map(function (h) {
                return '<tr class="is-click" data-href="' + DC.caseLink(h.id) + '"><td>' + esc(h.at) + '</td><td><b>' + esc(h.name) + '</b><span class="fa-sub">' + esc(h.caseNo) + '</span></td><td>' + esc(h.venue) + '</td><td>' + esc(h.statusText) + '</td></tr>';
            }).join('') : '<tr><td colspan="4" class="fa-empty">No hearing is scheduled.</td></tr>';
            Array.prototype.forEach.call(document.querySelectorAll('tr[data-href]'), function (tr) { tr.onclick = function () { location.href = tr.getAttribute('data-href'); }; });
        });
    }

    function kill(id) { if (charts[id]) { charts[id].destroy(); delete charts[id]; } }
    function empty(id, data) {
        var cv = qs(id), box = cv.parentNode, note = box.querySelector('.fa-empty');
        if (!data || !data.length) { cv.style.display = 'none'; if (!note) { note = document.createElement('div'); note.className = 'fa-empty'; note.textContent = 'No cases.'; box.appendChild(note); } return true; }
        cv.style.display = ''; if (note) box.removeChild(note); return false;
    }
    function click(data, href) {
        return function (e, els) { if (!els.length || !href) return; var u = href(data[els[0].index]); if (u) location.href = u; };
    }
    function bar(id, data, horizontal, href) {
        kill(id); if (empty(id, data) || !window.Chart) return;
        charts[id] = new Chart(qs(id), {
            type: 'bar',
            data: { labels: data.map(function (d) { return d.label; }), datasets: [{ data: data.map(function (d) { return d.value; }), backgroundColor: NAVY, hoverBackgroundColor: ACCENT, borderRadius: 0 }] },
            options: { indexAxis: horizontal ? 'y' : 'x', maintainAspectRatio: false, plugins: { legend: { display: false } }, onClick: click(data, href),
                       scales: { x: { ticks: { precision: 0 }, grid: { color: '#eef1f5' } }, y: { ticks: { precision: 0, autoSkip: false, font: { size: 10 } }, grid: { display: false } } } }
        });
    }
    function line(id, data) {
        kill(id); if (empty(id, data) || !window.Chart) return;
        charts[id] = new Chart(qs(id), {
            type: 'line',
            data: { labels: data.map(function (d) { return d.label; }), datasets: [{ data: data.map(function (d) { return d.value; }), borderColor: NAVY, backgroundColor: NAVY, pointRadius: 3, tension: 0 }] },
            options: { maintainAspectRatio: false, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true, ticks: { precision: 0 } } } }
        });
    }
    function doughnut(id, data, colours, href) {
        kill(id); if (empty(id, data) || !window.Chart) return;
        charts[id] = new Chart(qs(id), {
            type: 'doughnut',
            data: { labels: data.map(function (d) { return d.label; }), datasets: [{ data: data.map(function (d) { return d.value; }), borderWidth: 1, borderColor: '#fff',
                    backgroundColor: data.map(function (d, i) { return colours && colours[d.key] ? colours[d.key] : PALETTE[i % PALETTE.length]; }) }] },
            options: { maintainAspectRatio: false, cutout: '55%', plugins: { legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10 } } } }, onClick: click(data, href) }
        });
    }

    load();
})();
