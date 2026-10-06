/* Student Disciplinary module: Record Updates. A feed of case-file entries across the cases the user may see. ES5. */
(function () {
    'use strict';
    var B = window.DC_BOOT || {}, PAGE = 'DisciplinaryUpdates.aspx', esc = FA.esc, qs = FA.qs, page = 1, size = 50;
    FA.fill('fEntry', B.entryTypes, { all: 'All kinds' });
    FA.fill('fType', B.types, { all: 'All types' });
    FA.fill('fFac', B.faculties, { all: 'All faculties' });
    var u = FA.readUrl();
    FA.set('fFrom', u.from || B.from); FA.set('fTo', u.to || B.today); FA.set('fEntry', u.entryType || ''); FA.set('fType', u.type || ''); FA.set('fFac', u.faculty || '');
    FA.set('fQ', u.q || ''); FA.set('fBy', u.officer || ''); FA.set('fSys', u.system === 'true');

    function cfg() { return { q: FA.val('fQ'), from: FA.val('fFrom'), to: FA.val('fTo'), entryType: FA.val('fEntry'), type: FA.val('fType'), faculty: FA.val('fFac'), officer: FA.val('fBy'), system: FA.val('fSys') }; }
    ['fFrom', 'fTo', 'fEntry', 'fType', 'fFac', 'fSys'].forEach(function (i) { qs(i).addEventListener('change', function () { page = 1; load(); }); });
    ['fQ', 'fBy'].forEach(function (i) { qs(i).addEventListener('input', FA.debounce(function () { page = 1; load(); }, 350)); });
    qs('btnReset').onclick = function () { FA.set('fQ', ''); FA.set('fBy', ''); FA.set('fEntry', ''); FA.set('fType', ''); FA.set('fFac', ''); FA.set('fSys', false); FA.set('fFrom', B.from); FA.set('fTo', B.today); page = 1; load(); };
    qs('btnExport').innerHTML = FA.icon('download') + ' Export';
    qs('btnExport').onclick = function () {
        FA.exportDialog({ page: PAGE, report: 'updates', title: 'Record updates (current filters)', cfg: cfg(), countMethod: 'CountExport',
            cols: [{ k: 'when', t: 'Recorded', on: true }, { k: 'case_no', t: 'Case no', on: true }, { k: 'student', t: 'Student', on: true }, { k: 'regno', t: 'Reg no', on: true },
                   { k: 'entry', t: 'Entry', on: true }, { k: 'title', t: 'Title', on: true }, { k: 'by', t: 'Recorded by', on: true }, { k: 'visible', t: 'Student sees', on: true }] });
    };

    function load() {
        var c = cfg(); FA.writeUrl(c);
        qs('uBody').innerHTML = '<tr><td colspan="5" class="fa-loading">Loading</td></tr>';
        FA.call(PAGE, 'GetFeed', { configJson: JSON.stringify(c), page: page, size: size }, function (r) {
            if (!r.success) { qs('uBody').innerHTML = '<tr><td colspan="5" class="fa-empty">' + esc(r.message) + '</td></tr>'; return; }
            qs('uCount').textContent = FA.plural(r.total, 'entry', 'entries');
            qs('uBody').innerHTML = r.rows.length ? r.rows.map(function (x) {
                return '<tr class="is-click" data-id="' + x.caseId + '"><td style="white-space:nowrap">' + esc(x.when) + '</td>' +
                    '<td><span class="fa-code">' + esc(x.caseNo) + '</span><span class="fa-sub">' + esc(x.name) + ', ' + esc(x.regno) + '</span><span class="fa-sub">' + esc(x.caseType) + ', ' + esc(x.statusText) + '</span></td>' +
                    '<td><span class="dc-entry__type">' + esc(x.typeText) + '</span><b>' + esc(x.title) + '</b>' + (x.body ? '<span class="fa-sub" style="white-space:pre-wrap">' + esc(x.body) + '</span>' : '') + '</td>' +
                    '<td>' + esc(x.by) + '<span class="fa-sub">' + (x.via === 'EPORTAL' ? 'student portal' : x.via === 'SYSTEM' ? 'automatic' : 'eadmin') + '</span></td>' +
                    '<td>' + (x.visible ? '<span class="dc-vis dc-vis--student">' + DC.icon('eye', 11) + ' Yes</span>' : '<span class="dc-vis dc-vis--internal">' + DC.icon('eyeOff', 11) + ' No</span>') + '</td></tr>';
            }).join('') : '<tr><td colspan="5" class="fa-empty">Nothing was recorded in this period.</td></tr>';
            Array.prototype.forEach.call(qs('uBody').querySelectorAll('tr[data-id]'), function (tr) { tr.onclick = function () { location.href = DC.caseLink(tr.getAttribute('data-id')); }; });
            FA.pager('uPager', { total: r.total, page: page, size: size }, function (p) { page = p; load(); });
        });
    }
    load();
})();
