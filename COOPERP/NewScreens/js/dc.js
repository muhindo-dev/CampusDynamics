/* =====================================================================
   Student Disciplinary module: helpers shared by its screens (window.DC).
   Builds on the shared toolkit in fa.js (calls, modal, reason dialog,
   pager, typeahead, export dialog). ES5 only.
   ===================================================================== */
(function () {
    'use strict';
    var DC = {}, esc = FA.esc;

    var ICONS = {
        lock: '<rect x="3" y="11" width="18" height="11"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/>',
        eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
        eyeOff: '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94"/><path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19"/><line x1="1" y1="1" x2="23" y2="23"/>',
        paperclip: '<path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/>',
        mail: '<path d="M4 4h16v16H4z"/><polyline points="22 6 12 13 2 6"/>',
        calendar: '<rect x="3" y="4" width="18" height="18"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>',
        gavel: '<path d="M14 13l-7.5 7.5a2.12 2.12 0 0 1-3-3L11 10"/><path d="M16 16l6-6"/><path d="M8 8l6-6"/><path d="M9 7l8 8"/><path d="M21 11l-8-8"/>',
        flag: '<path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"/><line x1="4" y1="22" x2="4" y2="15"/>',
        user: '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
        file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/>'
    };
    DC.icon = function (name, size) {
        var s = size || 14;
        return ICONS[name] ? '<svg xmlns="http://www.w3.org/2000/svg" width="' + s + '" height="' + s + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + ICONS[name] + '</svg>' : FA.icon(name, size);
    };

    DC.statusKind = function (s) {
        switch (s) {
            case 'REPORTED': case 'UNDER_INVESTIGATION': return 'info';
            case 'HEARING_SCHEDULED': case 'SUMMONED': case 'HEARD': case 'UNDER_APPEAL': return 'warn';
            case 'DECIDED': case 'APPEAL_DECIDED': return 'ok';
            default: return 'neutral';
        }
    };
    DC.status = function (code, text) { return '<span class="fa-badge fa-badge--' + DC.statusKind(code) + '">' + esc(text || code) + '</span>'; };
    DC.sev = function (code, text) { return '<span class="dc-sev dc-sev--' + esc(code) + '"></span>' + esc(text || code); };
    DC.restricted = function () { return '<span class="dc-restrict" title="Restricted case: visible only to authorised officers">' + DC.icon('lock', 11) + ' Restricted</span>'; };
    DC.photo = function (regno, size) {
        return '<img src="StudentThumb.ashx?r=' + encodeURIComponent(regno) + '&s=' + (size || 112) + '" alt="" loading="lazy" onerror="this.style.visibility=\'hidden\'"/>';
    };
    DC.caseLink = function (id) { return 'DisciplinaryCase.aspx?id=' + encodeURIComponent(id); };
    DC.lines = function (s) { return esc(s || '').replace(/\n/g, '<br/>'); };

    DC.STATUSES = [['REPORTED', 'Reported'], ['UNDER_INVESTIGATION', 'Under investigation'], ['HEARING_SCHEDULED', 'Hearing scheduled'], ['SUMMONED', 'Summoned'],
                   ['HEARD', 'Heard'], ['DECIDED', 'Decided'], ['UNDER_APPEAL', 'Under appeal'], ['APPEAL_DECIDED', 'Appeal decided'], ['CLOSED', 'Closed'], ['WITHDRAWN', 'Withdrawn']];
    DC.EFFECTS = [['PORTAL_BLOCK', 'Portal access blocked'], ['RESULTS_WITHHELD', 'Results withheld'], ['SUSPENSION', 'Suspended'], ['EXPULSION', 'Expelled'],
                  ['GRADUATION_BAR', 'Barred from graduation'], ['CANCEL_PAPER', 'Paper result cancelled'], ['CANCEL_SEMESTER', 'Semester results cancelled'],
                  ['FINE', 'Fine'], ['RESTITUTION', 'Restitution']];
    DC.SEVERITIES = [['MINOR', 'Minor'], ['SERIOUS', 'Serious'], ['GROSS', 'Gross']];
    DC.pairs = function (l) { return l.map(function (p) { return { id: p[0], name: p[1] }; }); };

    /** Typeahead sources over a page's search methods. */
    DC.studentSource = function (page) {
        return function (q, cb) { FA.call(page, 'SearchStudents', { q: q }, function (r) { cb(r.success ? r.rows : []); }); };
    };
    DC.studentRender = function (s) {
        return '<b>' + esc(s.name) + '</b><small>' + esc([s.regno, s.programme, s.campus].filter(Boolean).join(', ')) + '</small>' +
               (s.cases && s.cases.length ? '<small style="color:#8a6500">' + s.cases.length + ' case' + (s.cases.length === 1 ? '' : 's') + '</small>' : '');
    };
    DC.staffSource = function (page) {
        return function (q, cb) { FA.call(page, 'SearchStaff', { q: q }, function (r) { cb(r.success ? r.rows : []); }); };
    };
    DC.staffRender = function (s) { return esc(s.name) + '<small>' + esc([s.username, s.department].filter(Boolean).join(', ')) + '</small>'; };

    /** A one-off id so a double click cannot save the same action twice. */
    DC.op = function () { return FA.uuid().replace(/-/g, '').substring(0, 32); };

    /** Runs a write PageMethod with a busy button, and reloads the case or list on success. */
    DC.save = function (page, method, args, btn, done) {
        FA.busy(btn, true);
        FA.call(page, method, args, function (r) {
            FA.busy(btn, false);
            if (!r.success) { done(r.message || 'The change was not saved.', r); return; }
            done(null, r);
        });
    };

    window.DC = DC;
})();
