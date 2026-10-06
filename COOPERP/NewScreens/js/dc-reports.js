/* Student Disciplinary module: Reports. Choose a report, set its filters, preview it, export it. ES5. */
(function () {
    'use strict';
    var B = window.DC_BOOT || {}, PAGE = 'DisciplinaryReports.aspx', esc = FA.esc, qs = FA.qs;
    var REPORTS = B.reports || [], cur = null, casePick = null;

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
        var u = FA.readUrl();
        FA.writeUrl({ report: key, caseId: u.caseId || '' });
        Array.prototype.forEach.call(qs('rList').querySelectorAll('[data-k]'), function (b) { b.className = b.getAttribute('data-k') === key ? 'is-on' : ''; });
        qs('rTitle').textContent = cur.title; qs('rDesc').textContent = cur.description;
        qs('rOut').innerHTML = ''; qs('rErr').textContent = '';
        var f = cur.filters, h = '';
        if (f.indexOf('case') >= 0) h += '<div class="fa-field fa-full"><label class="fa-label">Case</label><div id="pCase"></div></div>';
        if (f.indexOf('version') >= 0) h += field('pVer', 'Copy', '<select id="pVer" class="fa-select"><option value="committee">Committee copy (every entry)</option><option value="student">Student copy</option></select>');
        if (f.indexOf('year') >= 0) h += field('pYear', 'Academic year', '<select id="pYear" class="fa-select"></select>');
        if (f.indexOf('period') >= 0) h += field('pFrom', 'Reported from', '<input type="date" id="pFrom" class="fa-input"/>') + field('pTo', 'To', '<input type="date" id="pTo" class="fa-input"/>');
        if (f.indexOf('hearingRange') >= 0) h += field('pFrom', 'Hearings from', '<input type="date" id="pFrom" class="fa-input" value="' + B.today + '"/>') + field('pTo', 'To', '<input type="date" id="pTo" class="fa-input" value="' + B.in30 + '"/>');
        if (f.indexOf('date') >= 0) h += field('pDate', 'Hearing day', '<input type="date" id="pDate" class="fa-input" value="' + B.today + '"/>');
        if (f.indexOf('campus') >= 0) h += field('pCampus', 'Campus', '<select id="pCampus" class="fa-select"></select>');
        if (f.indexOf('faculty') >= 0) h += field('pFac', 'Faculty', '<select id="pFac" class="fa-select"></select>');
        if (f.indexOf('type') >= 0) h += field('pType', 'Case type', '<select id="pType" class="fa-select"></select>');
        if (f.indexOf('severity') >= 0) h += field('pSev', 'Severity', '<select id="pSev" class="fa-select"></select>');
        if (f.indexOf('status') >= 0) h += field('pStatus', 'Status', '<select id="pStatus" class="fa-select"></select>');
        if (f.indexOf('effect') >= 0) h += field('pEff', 'Restriction', '<select id="pEff" class="fa-select"></select>');
        if (f.indexOf('kind') >= 0) h += field('pKind', 'Owed by', '<select id="pKind" class="fa-select"><option value="">Marks office and Bursar</option><option value="marks">Marks office</option><option value="fees">Bursar</option></select>');
        if (cur.groups.length > 1) h += field('pGroup', 'Group by', '<select id="pGroup" class="fa-select">' + cur.groups.map(function (g) { return '<option value="' + esc(g.k) + '">' + esc(g.t) + '</option>'; }).join('') + '</select>');
        qs('rFilters').innerHTML = h || '<p class="fa-hint">This report has no filters.</p>';
        if (qs('pYear')) { FA.fill('pYear', (B.years || []).map(function (y) { return { id: y, name: y }; }), cur.key === 'senate' ? {} : { all: 'All years' }); if (cur.key === 'senate') FA.set('pYear', B.year); }
        if (qs('pCampus')) FA.fill('pCampus', B.campuses, { all: 'All campuses' });
        if (qs('pFac')) FA.fill('pFac', B.faculties, { all: 'All faculties' });
        if (qs('pType')) FA.fill('pType', B.types, { all: 'All types' });
        if (qs('pSev')) FA.fill('pSev', DC.pairs(DC.SEVERITIES), { all: 'All severities' });
        if (qs('pStatus')) FA.fill('pStatus', [{ id: 'open', name: 'Open cases' }].concat(DC.pairs(DC.STATUSES)), { all: 'All statuses' });
        if (qs('pEff')) FA.fill('pEff', DC.pairs(DC.EFFECTS.slice(0, 5)), { all: 'Any restriction' });
        casePick = null;
        if (qs('pCase')) {
            casePick = FA.typeahead('pCase', {
                source: function (q, cb) { FA.call(PAGE, 'SearchCases', { q: q }, function (r) { cb(r.success ? r.rows : []); }); },
                render: function (c) { return esc(c.name) + '<small>' + esc(c.sub) + '</small>'; }, placeholder: 'Case number or student',
                value: u.caseId ? { id: u.caseId, name: 'Case ' + u.caseId } : null
            });
        }
    }

    function cfg() {
        var o = {};
        [['pYear', 'year'], ['pFrom', 'from'], ['pTo', 'to'], ['pDate', 'date'], ['pCampus', 'campus'], ['pFac', 'faculty'], ['pType', 'type'], ['pSev', 'severity'],
         ['pStatus', 'status'], ['pEff', 'effect'], ['pKind', 'kind'], ['pVer', 'version']].forEach(function (p) { if (qs(p[0])) o[p[1]] = FA.val(p[0]); });
        if (casePick) { var c = casePick.get(); o.caseId = c ? c.id : ''; }
        return o;
    }
    function group() { return qs('pGroup') ? qs('pGroup').value : ''; }
    function ready() {
        if (!cur) { qs('rErr').textContent = 'Choose a report.'; return false; }
        if (cur.key === 'statement' && !cfg().caseId) { qs('rErr').textContent = 'Choose the case.'; return false; }
        qs('rErr').textContent = ''; return true;
    }

    qs('rPreview').onclick = function () {
        if (!ready()) return;
        qs('rOut').innerHTML = '<div class="fa-card"><div class="fa-loading">Preparing the preview</div></div>';
        FA.call(PAGE, 'PreviewReport', { report: cur.key, configJson: JSON.stringify(cfg()), groupBy: group() }, function (r) {
            if (!r.success) { qs('rOut').innerHTML = ''; qs('rErr').textContent = r.message; return; }
            var h = '<div class="fa-card"><div class="fa-card__head"><div><div class="fa-card__title">' + esc(r.title) + (r.subtitle ? ', ' + esc(r.subtitle) : '') + '</div>' +
                '<div class="fa-hint">' + r.cover.map(function (c) { return esc(c.Key) + ': ' + esc(c.Value); }).join('. ') + '</div></div><span class="fa-card__meta">' + FA.plural(r.total, 'row', 'rows') +
                (r.total > r.rows.length ? ', first ' + r.rows.length + ' shown' : '') + '</span></div>';
            if (r.prose && r.prose.length) h += '<div class="fa-card__body dc-prose">' + r.prose.map(function (p) { return '<p>' + esc(p) + '</p>'; }).join('') + '</div>';
            h += '<div class="fa-table-wrap"><table class="fa-table"><thead><tr>' + r.cols.map(function (c) { return '<th' + (c.num ? ' class="fa-num"' : '') + '>' + esc(c.t) + '</th>'; }).join('') + '</tr></thead><tbody>';
            var lastG = null;
            r.rows.forEach(function (row) {
                if (r.grouped && row.g !== lastG) { lastG = row.g; h += '<tr><td colspan="' + r.cols.length + '" style="background:#e6ecf5;font-weight:700;color:#05275C">' + esc(row.g) + '</td></tr>'; }
                h += '<tr>' + row.c.map(function (v, i) { return '<td' + (r.cols[i].num ? ' class="fa-num"' : '') + ' style="white-space:pre-wrap">' + esc(v) + '</td>'; }).join('') + '</tr>';
            });
            if (!r.rows.length) h += '<tr><td colspan="' + r.cols.length + '" class="fa-empty">Nothing matches these filters.</td></tr>';
            h += '</tbody>' + (r.totals ? '<tfoot><tr>' + r.totals.map(function (t, i) { return '<td' + (r.cols[i].num ? ' class="fa-num"' : '') + '>' + (i === 0 && !t ? 'Total' : esc(t)) + '</td>'; }).join('') + '</tr></tfoot>' : '') + '</table></div></div>';
            qs('rOut').innerHTML = h;
        });
    };

    qs('rExport').onclick = function () {
        if (!ready()) return;
        FA.exportDialog({ page: PAGE, report: cur.key, title: cur.title, cfg: cfg(), cols: cur.cols, groups: cur.groups, group: group(), countMethod: 'CountExport' });
    };

    var start = FA.readUrl().report || (REPORTS[0] && REPORTS[0].key);
    if (start) choose(start);
})();
