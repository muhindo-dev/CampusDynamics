/* Student Disciplinary module: the case file. Header, status steps, timeline and tabs, and every action the case allows. ES5. */
(function () {
    'use strict';
    var B = window.DC_BOOT || {}, PAGE = 'DisciplinaryCase.aspx', esc = FA.esc, qs = FA.qs;
    var D = null, tab = 'timeline';

    var FLOW = [['REPORTED', 'Reported'], ['UNDER_INVESTIGATION', 'Investigation'], ['HEARING_SCHEDULED', 'Hearing scheduled'], ['SUMMONED', 'Summoned'],
                ['HEARD', 'Heard'], ['DECIDED', 'Decided'], ['UNDER_APPEAL', 'Appeal'], ['APPEAL_DECIDED', 'Appeal decided'], ['CLOSED', 'Closed']];
    var KEY_TYPES = { CASE_OPENED: 1, DECISION: 1, APPEAL_DECISION: 1, SUMMON: 1, HEARING_HELD: 1, CASE_CLOSED: 1, CASE_WITHDRAWN: 1, BLOCK_APPLIED: 1, INTERIM_MEASURE: 1 };

    function load(keepTab) {
        if (!B.id) { qs('cMain').innerHTML = '<div class="fa-card"><div class="fa-empty">No case was chosen. Open one from Disciplinary records.</div></div>'; return; }
        FA.call(PAGE, 'GetCase', { id: B.id }, function (r) {
            if (!r.success) { qs('cMain').innerHTML = '<div class="fa-card"><div class="fa-empty">' + esc(r.message) + ' <a href="DisciplinaryRecords.aspx">Back to the records</a></div></div>'; return; }
            D = r; if (!keepTab) tab = FA.readUrl().tab || 'timeline';
            render();
        });
    }

    // ── Layout ────────────────────────────────────────────────────────
    function render() {
        var c = D['case'], i = D.incident;
        document.title = c.caseNo + ' - Disciplinary Case';
        var force = D.sanctions.filter(function (s) { return s.inForce; });
        var h = '<div class="dc-casehead"><div class="dc-student">' + DC.photo(c.regno, 128) + '<div style="min-width:0">' +
            '<div class="fa-row"><span class="dc-casehead__no">' + esc(c.caseNo) + '</span>' + DC.status(c.status, c.statusText) + (c.restricted ? DC.restricted() : '') + '</div>' +
            '<div class="dc-student__name" style="margin-top:4px">' + esc(c.name) + '</div>' +
            '<div class="dc-student__meta">' + esc(c.regno) + (c.programme ? ', ' + esc(c.programme) : '') + (c.campus ? ', ' + esc(c.campus) : '') +
            (c.studyYear ? ', year ' + c.studyYear : '') + '<br/>' + esc(c.type) + ' &middot; ' + DC.sev(c.severity, c.severityText) + ' &middot; opened ' + esc(c.opened) + '</div>' +
            (force.length ? '<div class="dc-force" style="margin-top:8px">' + force.map(function (s) { return '<span' + (s.source === 'INTERIM' ? ' class="is-interim"' : '') + '>' + esc(s.name) + (s.to ? ' until ' + esc(s.to) : '') + (s.source === 'INTERIM' ? ' (interim)' : '') + '</span>'; }).join('') + '</div>' : '') +
            '</div></div><div class="dc-casehead__aside">' +
            (c.hearing && (c.status === 'HEARING_SCHEDULED' || c.status === 'SUMMONED') ? '<div>' + DC.icon('calendar', 12) + ' Hearing <b>' + esc(c.hearing) + '</b><br/>' + esc(c.venue) + '</div>' : '') +
            (c.status === 'DECIDED' && c.appealDeadline ? '<div>Appeal window ' + (c.appealOpen ? 'open until <b>' + esc(c.appealDeadline) + '</b>' : 'closed on ' + esc(c.appealDeadline)) + '</div>' : '') +
            '<div>Case officer: <b>' + esc(c.officer || 'not assigned') + '</b></div><div>Reported by ' + esc(c.reporter) + '</div>' +
            (c.restrictions ? '<div style="color:#b42318">Student restrictions now: ' + esc(c.restrictions) + '</div>' : '') +
            '<div class="fa-row" style="justify-content:flex-end"><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="btnStatement">' + FA.icon('print') + ' Statement</button></div>' +
            '</div></div>';
        h += steps(c.status);
        h += '<div class="fa-grid-main"><div>' +
            '<div class="fa-subtabs" id="cTabs">' + [['timeline', 'Case file'], ['sanctions', 'Sanctions (' + D.sanctions.length + ')'], ['hearings', 'Hearings (' + D.hearings.length + ')'],
                ['letters', 'Letters (' + D.letters.length + ')'], ['files', 'Files (' + D.attachments.filter(function (a) { return a.active; }).length + ')'],
                ['appeals', 'Appeals (' + D.appeals.length + ')'], ['notices', 'Notices (' + D.notices.length + ')'], ['audit', 'Audit']]
                .map(function (t) { return '<button type="button" class="fa-subtab' + (t[0] === tab ? ' is-active' : '') + '" data-t="' + t[0] + '">' + t[1] + '</button>'; }).join('') + '</div>' +
            '<div id="cTab"></div></div><div>' + actionsCard() + incidentCard() + relatedCard() + '</div></div>';
        qs('cMain').innerHTML = h;
        Array.prototype.forEach.call(qs('cTabs').querySelectorAll('[data-t]'), function (b) {
            b.onclick = function () { tab = b.getAttribute('data-t'); FA.writeUrl({ id: B.id, tab: tab === 'timeline' ? '' : tab }); render(); };
        });
        qs('btnStatement').onclick = statement;
        drawTab();
        wireActions();
    }

    function steps(st) {
        var idx = -1; FLOW.forEach(function (f, i) { if (f[0] === st) idx = i; });
        if (st === 'WITHDRAWN') return '<ul class="dc-steps"><li class="is-done">Reported</li><li class="is-off">Withdrawn</li></ul>';
        return '<ul class="dc-steps">' + FLOW.map(function (f, i) {
            if ((f[0] === 'UNDER_APPEAL' || f[0] === 'APPEAL_DECIDED') && ['UNDER_APPEAL', 'APPEAL_DECIDED'].indexOf(st) < 0 && !(st === 'CLOSED' && D.appeals.length)) return '';
            return '<li class="' + (i === idx ? 'is-now' : i < idx ? 'is-done' : '') + '">' + esc(f[1]) + '</li>';
        }).join('') + '</ul>';
    }

    function incidentCard() {
        var i = D.incident;
        return '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Incident <span class="fa-card__meta">' + esc(i.no) + '</span></div></div><div class="fa-card__body">' +
            '<dl class="fa-dl fa-dl--2"><div><dt>When</dt><dd>' + esc(i.occurred) + '</dd></div><div><dt>Where</dt><dd>' + esc(i.place || 'Not recorded') + (i.campus ? ', ' + esc(i.campus) : '') + '</dd></div>' +
            '<div><dt>Reported by</dt><dd>' + esc(i.reporter) + '</dd></div>' +
            (i.course ? '<div><dt>Examination</dt><dd>' + esc(i.course) + ', ' + esc(i.examYear) + ' semester ' + esc(i.examSemester) + (i.invigilator ? '<br/>Invigilator: ' + esc(i.invigilator) : '') + '</dd></div>' : '') +
            (i.ticketId ? '<div><dt>Complaint</dt><dd>#' + i.ticketId + ' ' + esc(i.ticket) + ' (' + esc(i.ticketStatus) + ')</dd></div>' : '') +
            '</dl><div class="fa-section" style="margin-top:12px">Description</div><div class="dc-entry__body" style="margin:0">' + DC.lines(i.description) + '</div>' +
            (i.witnesses ? '<div class="fa-section">Witnesses</div><div class="dc-entry__body" style="margin:0">' + DC.lines(i.witnesses) + '</div>' : '') +
            '</div></div>';
    }

    function relatedCard() {
        if (!D.linked.length && !D.history.length) return '';
        var h = '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Related cases</div></div><div class="fa-card__body">';
        if (D.linked.length) h += '<div class="fa-section">Same incident</div>' + D.linked.map(function (l) { return '<div><a href="' + DC.caseLink(l.id) + '" class="fa-btn--link">' + esc(l.caseNo) + '</a> ' + esc(l.name) + ' <span class="fa-muted">' + esc(l.statusText) + '</span></div>'; }).join('');
        if (D.history.length) h += '<div class="fa-section">Other cases of this student</div>' + D.history.map(function (l) { return '<div><a href="' + DC.caseLink(l.id) + '" class="fa-btn--link">' + esc(l.caseNo) + '</a> ' + esc(l.type) + ' <span class="fa-muted">' + esc(l.statusText) + '</span></div>'; }).join('');
        return h + '</div></div>';
    }

    function actionsCard() {
        var a = D.actions, c = D['case'], b = [];
        function btn(id, icon, label, primary) { return '<button type="button" class="fa-btn ' + (primary ? 'fa-btn--primary' : 'fa-btn--secondary') + '" data-a="' + id + '">' + (icon ? DC.icon(icon) + ' ' : '') + esc(label) + '</button>'; }
        var g1 = [], g2 = [], g3 = [], g4 = [];
        if (a.addNote) g1.push(btn('entry', 'edit', 'Add a note, statement or evidence'));
        if (a.investigate) g1.push(btn('investigate', 'search', 'Place under investigation', true));
        if (a.officer) g1.push(btn('officer', 'user', c.officer ? 'Change case officer' : 'Assign case officer'));
        if (a.schedule) g2.push(btn('schedule', 'calendar', 'Schedule a hearing', !a.investigate));
        if (a.summon && c.status === 'HEARING_SCHEDULED') g2.push(btn('summon', 'mail', 'Summon the student', true));
        if (a.adjourn) g2.push(btn('adjourn', 'calendar', 'Adjourn the hearing'));
        if (a.recordHearing) g2.push(btn('hearing', 'check', 'Record the hearing', true));
        if (a.decide) g2.push(btn('decide', 'gavel', 'Record the decision', true));
        if (a.lodgeAppeal) g3.push(btn('appeal', 'flag', 'Lodge an appeal for the student'));
        if (a.decideAppeal) g3.push(btn('decideAppeal', 'gavel', 'Decide the appeal', true));
        if (a.measure) g3.push(btn('measure', 'lock', 'Apply an interim measure'));
        if (a.recommend) g3.push(btn('recommend', 'lock', 'Recommend an interim measure'));
        if (a.letter) g4.push(btn('letter', 'file', 'Issue a letter'));
        if (a.close) g4.push(btn('close', 'check', 'Close the case'));
        if (a.withdraw) g4.push(btn('withdraw', 'x', 'Withdraw the case'));
        if (a.setRestricted) g4.push(btn('restricted', 'lock', c.restricted ? 'Remove the restriction' : 'Mark as restricted'));
        var h = '';
        if (g1.length) h += '<div class="dc-actions__group">Case</div>' + g1.join('');
        if (g2.length) h += '<div class="dc-actions__group">Hearing and decision</div>' + g2.join('');
        if (g3.length) h += '<div class="dc-actions__group">Appeals and measures</div>' + g3.join('');
        if (g4.length) h += '<div class="dc-actions__group">Letters and closing</div>' + g4.join('');
        if (!h) h = '<p class="fa-hint" style="margin:0">' + (c.status === 'CLOSED' || c.status === 'WITHDRAWN' ? 'This case is ' + esc(c.statusText.toLowerCase()) + '. Its record is kept as it is.' : 'You can read this case. Actions need further permission.') + '</p>';
        return '<div class="fa-card"><div class="fa-card__head"><div class="fa-card__title">Actions</div></div><div class="fa-card__body"><div class="dc-actions">' + h + '</div></div></div>';
    }

    // ── Tabs ──────────────────────────────────────────────────────────
    function drawTab() {
        var el = qs('cTab'), h = '';
        if (tab === 'timeline') h = timeline();
        else if (tab === 'sanctions') h = sanctionsTab();
        else if (tab === 'hearings') h = table(['When', 'Venue', 'Panel', 'Status', 'Student'], D.hearings.map(function (x) {
            return [esc(x.at), esc(x.venue), esc(x.panel), esc(x.status.charAt(0) + x.status.slice(1).toLowerCase()), esc(x.attended)];
        }), 'No hearing has been scheduled.');
        else if (tab === 'letters') h = table(['Letter', 'Subject', 'Issued', 'Student copy', ''], D.letters.map(function (l) {
            return ['<span class="fa-code">' + esc(l.no) + '</span>', esc(l.subject), esc(l.issued) + '<span class="fa-sub">' + esc(l.by) + '</span>', l.visible ? 'Yes' : 'No',
                    '<a class="fa-btn fa-btn--secondary fa-btn--sm" href="DcFile.ashx?l=' + l.id + '&view=1" target="_blank" rel="noopener">Open</a>'];
        }), 'No letter has been issued.');
        else if (tab === 'files') h = filesTab();
        else if (tab === 'appeals') h = D.appeals.length ? D.appeals.map(function (a) {
            return '<div class="fa-card"><div class="fa-card__body"><div class="fa-row"><b>Lodged ' + esc(a.lodged) + '</b><span class="fa-muted">via ' + (a.via === 'EPORTAL' ? 'student portal' : 'the office') + '</span>' +
                (a.withinWindow ? '' : '<span class="fa-badge fa-badge--warn">Late, accepted</span>') + '<span class="fa-spacer"></span>' + '<span class="fa-badge fa-badge--' + (a.status === 'LODGED' ? 'warn' : 'neutral') + '">' + esc(a.status === 'LODGED' ? 'Pending' : a.status.toLowerCase()) + '</span></div>' +
                '<div class="dc-entry__body">' + DC.lines(a.grounds) + '</div>' + (a.lateReason ? '<div class="fa-hint">Late: ' + esc(a.lateReason) + '</div>' : '') +
                (a.decision ? '<div class="fa-section">Decision of ' + esc(a.decided) + '</div><div class="dc-entry__body">' + DC.lines(a.decision) + '</div>' : '') + '</div></div>';
        }).join('') : '<div class="fa-card"><div class="fa-empty">No appeal has been lodged.</div></div>';
        else if (tab === 'notices') h = table(['Notice', 'Sent', 'Read on portal', 'Email', ''], D.notices.map(function (n) {
            return [esc(n.title), esc(n.at), n.read ? esc(n.read) : '<span class="fa-muted">Not yet</span>',
                    esc(n.email === 'SENT' ? 'Sent' : n.email === 'FAILED' ? 'Failed' : n.email === 'NO_ADDRESS' ? 'No address' : n.email === 'PENDING' ? 'Waiting' : n.email) + (n.emailTo ? '<span class="fa-sub">' + esc(n.emailTo) + '</span>' : '') + (n.emailError ? '<span class="fa-sub" style="color:#b42318">' + esc(n.emailError) + '</span>' : ''),
                    D.actions.notify ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-notify="' + n.id + '">Send again</button>' : ''];
        }), 'No notice has been sent.');
        else if (tab === 'audit') h = table(['When', 'Who', 'What', 'Before', 'After', 'Reason'], D.audit.map(function (a) {
            return [esc(a.at), esc(a.actor) + '<span class="fa-sub">' + esc(a.role) + (a.ip ? ', ' + esc(a.ip) : '') + '</span>', esc(a.entity.toLowerCase() + ' ' + a.action.toLowerCase().replace(/_/g, ' ')),
                    '<div class="dc-json">' + esc(a.before) + '</div>', '<div class="dc-json">' + esc(a.after) + '</div>', esc(a.reason)];
        }), 'No changes recorded.');
        el.innerHTML = h;
        Array.prototype.forEach.call(el.querySelectorAll('[data-correct]'), function (b) { b.onclick = function () { entryDialog(parseInt(b.getAttribute('data-correct'), 10)); }; });
        Array.prototype.forEach.call(el.querySelectorAll('[data-lift]'), function (b) { b.onclick = function () { liftDialog(sanction(b.getAttribute('data-lift'))); }; });
        Array.prototype.forEach.call(el.querySelectorAll('[data-vary]'), function (b) { b.onclick = function () { varyDialog(sanction(b.getAttribute('data-vary'))); }; });
        Array.prototype.forEach.call(el.querySelectorAll('[data-follow]'), function (b) { b.onclick = function () { followDialog(sanction(b.getAttribute('data-follow'))); }; });
        Array.prototype.forEach.call(el.querySelectorAll('[data-notify]'), function (b) {
            b.onclick = function () { FA.confirm({ title: 'Send the notice again', message: 'The student gets the same notice again on the portal and by email.', ok: 'Send' }, function () { act('notify', { noticeId: b.getAttribute('data-notify') }, null, function () { FA.toast('Notice sent again.'); }); }); };
        });
        Array.prototype.forEach.call(el.querySelectorAll('[data-rmfile]'), function (b) {
            b.onclick = function () {
                FA.reason({ title: 'Hide this file', sub: 'The file is kept but no longer shown', label: 'Reason', ok: 'Hide', danger: true }, function (reason, m, done) {
                    act('removeFile', { attachmentId: b.getAttribute('data-rmfile'), reason: reason }, null, function (err) { done(err); if (!err) FA.toast('File hidden.'); }, true);
                });
            };
        });
    }
    function sanction(id) { var s = null; D.sanctions.forEach(function (x) { if (String(x.id) === String(id)) s = x; }); return s; }

    function table(heads, rows, empty) {
        if (!rows.length) return '<div class="fa-card"><div class="fa-empty">' + esc(empty) + '</div></div>';
        return '<div class="fa-card"><div class="fa-table-wrap"><table class="fa-table"><thead><tr>' + heads.map(function (h) { return '<th>' + esc(h) + '</th>'; }).join('') +
            '</tr></thead><tbody>' + rows.map(function (r) { return '<tr>' + r.map(function (c) { return '<td>' + c + '</td>'; }).join('') + '</tr>'; }).join('') + '</tbody></table></div></div>';
    }

    function timeline() {
        if (!D.entries.length) return '<div class="fa-card"><div class="fa-empty">The case file is empty.</div></div>';
        var showSys = FA.readUrl().sys === '1';
        var list = D.entries.filter(function (e) { return showSys || e.type !== 'STUDENT_NOTIFIED'; });
        var hidden = D.entries.length - list.length;
        return '<div class="fa-row" style="margin-bottom:8px"><span class="fa-hint">' + FA.plural(D.entries.length, 'entry', 'entries') + '. Entries are never edited or removed; a correction is a new entry.</span><span class="fa-spacer"></span>' +
            (hidden || showSys ? '<a class="fa-btn--link" href="?id=' + B.id + (showSys ? '' : '&sys=1') + '">' + (showSys ? 'Hide' : 'Show') + ' notification entries</a>' : '') + '</div>' +
            '<ul class="dc-timeline">' + list.slice().reverse().map(function (e) {
                var cls = 'dc-entry' + (KEY_TYPES[e.type] ? ' dc-entry--key' : '') + (e.via === 'SYSTEM' ? ' dc-entry--system' : '') + (e.type === 'CORRECTION' ? ' dc-entry--correction' : '') + (e.visible ? '' : ' dc-entry--internal');
                return '<li class="' + cls + '"><span class="dc-entry__dot"></span><div class="dc-entry__card">' +
                    '<div class="dc-entry__top"><div><span class="dc-entry__type">' + esc(e.typeText) + '</span><span class="dc-entry__title">' + esc(e.title) + '</span></div><span class="dc-entry__when">' + esc(e.at) + '</span></div>' +
                    (e.body ? '<div class="dc-entry__body">' + esc(e.body) + '</div>' : '') +
                    (e.attachments.length ? '<div class="dc-entry__files">' + e.attachments.map(fileLink).join('') + '</div>' : '') +
                    '<div class="dc-entry__meta">' + (e.visible ? '<span class="dc-vis dc-vis--student">' + DC.icon('eye', 11) + ' Visible to student</span>' : '<span class="dc-vis dc-vis--internal">' + DC.icon('eyeOff', 11) + ' Internal</span>') +
                    '<span>' + esc(e.by) + (e.role ? ' (' + esc(e.role) + ')' : '') + '</span><span>' + (e.via === 'EPORTAL' ? 'student portal' : e.via === 'SYSTEM' ? 'automatic' : 'eadmin') + (e.ip ? ', ' + esc(e.ip) : '') + '</span>' +
                    (e.recorded !== e.at ? '<span>recorded ' + esc(e.recorded) + '</span>' : '') +
                    (e.corrects ? '<span>corrects entry ' + e.corrects + '</span>' : '') +
                    (D.actions.addNote && e.type !== 'STUDENT_NOTIFIED' ? '<span class="fa-spacer"></span><button type="button" class="fa-btn fa-btn--link" data-correct="' + e.id + '">Add a correction</button>' : '') +
                    '</div></div></li>';
            }).join('') + '</ul>';
    }

    function fileLink(a) {
        return '<a class="dc-file" href="DcFile.ashx?a=' + a.id + '&view=1" target="_blank" rel="noopener" title="' + esc(a.description || a.name) + '">' + DC.icon('paperclip', 12) + esc(a.name) + '</a>';
    }

    function filesTab() {
        var act = D.attachments.filter(function (a) { return a.active; }), gone = D.attachments.filter(function (a) { return !a.active; });
        var h = (D.actions.addNote ? '<div class="fa-row" style="margin-bottom:8px"><span class="fa-spacer"></span><button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" id="btnAddFile">' + FA.icon('upload') + ' Add a file</button></div>' : '');
        h += table(['File', 'Kind', 'Added', 'Student can see', ''], act.map(function (a) {
            return [fileLink(a) + (a.description ? '<span class="fa-sub">' + esc(a.description) + '</span>' : ''), esc(a.kind.toLowerCase()), esc(a.at) + '<span class="fa-sub">' + esc(a.by) + (a.via === 'EPORTAL' ? ', student portal' : '') + '</span>',
                    a.visible ? 'Yes' : 'No', D.actions.followUp ? '<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-rmfile="' + a.id + '">Hide</button>' : ''];
        }), 'No files attached.');
        if (gone.length) h += '<div class="fa-section">Hidden files</div>' + table(['File', 'Hidden because'], gone.map(function (a) { return [fileLink(a), esc(a.removedReason)]; }), '');
        setTimeout(function () { var b = qs('btnAddFile'); if (b) b.onclick = function () { fileDialog(null); }; }, 0);
        return h;
    }

    function sanctionsTab() {
        if (!D.sanctions.length) return '<div class="fa-card"><div class="fa-empty">No sanction or interim measure has been recorded.</div></div>';
        return '<div class="fa-card"><div class="fa-table-wrap"><table class="fa-table"><thead><tr><th>Sanction</th><th>Basis</th><th>Period</th><th>Effects</th><th>Status</th><th>Follow-up</th><th></th></tr></thead><tbody>' +
            D.sanctions.slice().reverse().map(function (s) {
                var acts = [];
                if (s.status === 'ACTIVE' && D.actions.lift) acts.push('<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-lift="' + s.id + '">Lift</button>');
                if (s.status === 'ACTIVE' && D.actions.vary) acts.push('<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-vary="' + s.id + '">Vary</button>');
                if (s.followUp === 'PENDING' && D.actions.followUp && (s.status === 'ACTIVE' || s.status === 'EXPIRED')) acts.push('<button type="button" class="fa-btn fa-btn--secondary fa-btn--sm" data-follow="' + s.id + '">Record action done</button>');
                return '<tr><td><b>' + esc(s.name) + '</b>' + (s.amount ? '<span class="fa-sub">UGX ' + esc(s.amount) + '</span>' : '') + (s.course ? '<span class="fa-sub">' + esc(s.course) + '</span>' : '') +
                    (s.year ? '<span class="fa-sub">' + esc(s.year) + ' semester ' + s.semester + '</span>' : '') + (s.terms ? '<span class="fa-sub">' + esc(s.terms) + '</span>' : '') + '</td>' +
                    '<td>' + esc(s.sourceText) + '</td><td>' + esc(s.from) + (s.to ? ' to ' + esc(s.to) : s.effects ? ', open' : '') + '</td><td>' + esc(s.effectsText || 'None') + '</td>' +
                    '<td><span class="fa-badge fa-badge--' + (s.inForce ? 'bad' : s.pending ? 'warn' : 'neutral') + '">' + esc(s.inForce ? 'In force' : s.pending ? 'Starts ' + s.from : s.statusText) + '</span>' +
                    (s.ended ? '<span class="fa-sub">' + esc(s.ended) + ', ' + esc(s.endedBy) + '</span><span class="fa-sub">' + esc(s.endedReason) + '</span>' : '') + '</td>' +
                    '<td>' + (s.followUp === 'PENDING' ? '<span class="fa-badge fa-badge--warn">Owed</span>' : s.followUp === 'DONE' ? 'Done<span class="fa-sub">' + esc(s.followRef) + '</span>' : '<span class="fa-muted">None</span>') + '</td>' +
                    '<td><div class="fa-row">' + acts.join('') + '</div></td></tr>';
            }).join('') + '</tbody></table></div></div>';
    }

    // ── Actions ───────────────────────────────────────────────────────
    function wireActions() {
        Array.prototype.forEach.call(qs('cMain').querySelectorAll('[data-a]'), function (b) {
            b.onclick = function () {
                var a = b.getAttribute('data-a');
                ({ entry: function () { entryDialog(null); }, investigate: investigateDialog, officer: officerDialog, schedule: function () { hearingDialog('schedule'); },
                   summon: summonDialog, adjourn: function () { hearingDialog('adjourn'); }, hearing: recordHearingDialog, decide: function () { decisionDialog('decide'); },
                   appeal: appealDialog, decideAppeal: function () { decisionDialog('appeal'); }, measure: function () { measureDialog(false); }, recommend: function () { measureDialog(true); },
                   letter: letterDialog, close: closeDialog, withdraw: withdrawDialog, restricted: restrictedDialog })[a]();
            };
        });
    }

    /** Posts an action; on success reloads the case. done(err) lets dialogs show the error. */
    function act(action, args, btn, done, keepOpen) {
        args.caseId = B.id; args.version = D['case'].version;
        if (!args.opId) args.opId = DC.op();
        DC.save(PAGE, 'Act', { action: action, json: JSON.stringify(args) }, btn, function (err, r) {
            if (done) done(err, r);
            if (!err) load(true);
        });
    }

    function dialog(o, submit) {
        var m = FA.modal({ title: o.title, sub: o.sub, size: o.size || 'mid', sticky: true, body: o.body + '<div class="fa-error" id="dlgErr" style="margin-top:10px"></div>',
            foot: '<button type="button" class="fa-btn fa-btn--secondary" data-x>Cancel</button><button type="button" class="fa-btn ' + (o.danger ? 'fa-btn--danger' : 'fa-btn--primary') + '" data-ok>' + esc(o.ok || 'Save') + '</button>' });
        m.foot.querySelector('[data-x]').onclick = function () { m.close(); };
        var ok = m.foot.querySelector('[data-ok]');
        ok.onclick = function () {
            qs('dlgErr').textContent = '';
            var v = submit(m);
            if (v === false) return;
            if (typeof v === 'string') { qs('dlgErr').textContent = v; return; }
            act(v.action, v.args, ok, function (err, r) {
                if (err) { qs('dlgErr').textContent = err; return; }
                m.close(); FA.toast(v.toast || 'Saved.');
                if (v.after) v.after(r);
            });
        };
        if (o.onOpen) o.onOpen(m);
        return m;
    }

    function field(label, input, hint, full, req) {
        return '<div class="fa-field' + (full ? ' fa-full' : '') + '"><label class="fa-label">' + esc(label) + (req ? ' <span class="fa-req">*</span>' : '') + '</label>' + input + (hint ? '<span class="fa-hint">' + hint + '</span>' : '') + '</div>';
    }

    function entryDialog(correctsId) {
        var orig = null; if (correctsId) D.entries.forEach(function (e) { if (e.id === correctsId) orig = e; });
        var canFinding = D.actions.addFinding;
        var types = correctsId ? '' : field('Kind of entry', '<select id="eType" class="fa-select"><option value="NOTE">Note</option><option value="STATEMENT">Statement</option><option value="EVIDENCE">Evidence</option>' +
            (canFinding ? '<option value="FINDING">Finding</option>' : '') + '</select>', '', false, true);
        dialog({
            title: correctsId ? 'Add a correction' : 'Add to the case file', sub: correctsId ? 'Corrects: ' + (orig ? orig.title : '') : D['case'].caseNo,
            body: (correctsId ? '<div class="fa-notice">The original entry stays as it is. Your correction is added after it and linked to it, with the same visibility to the student.</div>' : '') +
                '<div class="fa-form">' + types + (correctsId ? '' : field('Date it happened', '<input type="date" id="eDate" class="fa-input" max="' + B.today + '" value="' + B.today + '"/>')) +
                field('Title', '<input id="eTitle" class="fa-input" maxlength="250"/>', 'Optional. A short heading.', true) +
                field(correctsId ? 'Correction' : 'Text', '<textarea id="eBody" class="fa-textarea" rows="5" maxlength="20000"></textarea>', '', true, true) +
                (correctsId ? '' : field('Files', '<input type="file" id="eFiles" multiple accept=".pdf,.jpg,.jpeg,.png,.docx,.xlsx"/>', 'Attached to this entry.', true) +
                    '<div class="fa-field fa-full"><label class="fa-check-line"><input type="checkbox" id="eVisible"/> The student may see this entry and its files</label>' +
                    '<label class="fa-check-line" style="margin-top:6px"><input type="checkbox" id="eNotify" disabled/> Notify the student</label></div>') + '</div>',
            onOpen: function () { var v = qs('eVisible'); if (v) v.onchange = function () { qs('eNotify').disabled = !v.checked; if (!v.checked) qs('eNotify').checked = false; }; },
            ok: 'Add'
        }, function () {
            if (FA.val('eBody').length < 5) return 'Write at least 5 characters.';
            var files = qs('eFiles') ? Array.prototype.slice.call(qs('eFiles').files || []) : [];
            return {
                action: 'entry', args: { type: correctsId ? 'CORRECTION' : FA.val('eType'), correctsId: correctsId || '', title: FA.val('eTitle'), body: FA.val('eBody'),
                                         date: FA.val('eDate'), visible: !!FA.val('eVisible'), notify: !!FA.val('eNotify') },
                toast: 'Added to the case file.',
                after: function (r) { if (files.length) upload(files, r.entryId, FA.val('eVisible') ? 'true' : 'false', 'EVIDENCE', ''); }
            };
        });
    }

    function upload(files, entryId, visible, kind, description, cb) {
        var list = files.slice(), fails = [];
        (function next() {
            var f = list.shift();
            if (!f) { if (fails.length) FA.toast('Not attached: ' + fails.join('; '), true); load(true); if (cb) cb(); return; }
            var fd = new FormData(); fd.append('caseId', B.id); if (entryId) fd.append('entryId', entryId); fd.append('kind', kind); fd.append('visible', visible); fd.append('description', description || ''); fd.append('file', f);
            var x = new XMLHttpRequest(); x.open('POST', 'DcUpload.ashx', true); x.setRequestHeader('X-CSRF-Token', FA.token());
            x.onreadystatechange = function () {
                if (x.readyState !== 4) return;
                var r = {}; try { r = JSON.parse(x.responseText); } catch (e) { }
                if (!r.success) fails.push(f.name + (r.message ? ': ' + r.message : ''));
                next();
            };
            x.send(fd);
        })();
    }

    function fileDialog() {
        dialog({
            title: 'Add a file', sub: D['case'].caseNo,
            body: '<div class="fa-form">' + field('File', '<input type="file" id="fFile" accept=".pdf,.jpg,.jpeg,.png,.docx,.xlsx"/>', 'PDF, JPG, PNG, Word or Excel, up to 15 MB.', true, true) +
                field('Kind', '<select id="fKind" class="fa-select"><option value="EVIDENCE">Evidence</option><option value="STATEMENT">Statement</option><option value="PHOTO">Photo</option><option value="SCRIPT">Answer script</option>' +
                      '<option value="SCREENSHOT">Screenshot</option><option value="MINUTES">Minutes</option><option value="OTHER">Other</option></select>') +
                field('Description', '<input id="fDesc" class="fa-input" maxlength="300"/>') +
                '<div class="fa-field fa-full"><label class="fa-check-line"><input type="checkbox" id="fVisible"/> The student may see this file</label></div></div>',
            ok: 'Upload'
        }, function (m) {
            var f = qs('fFile').files[0];
            if (!f) return 'Choose a file.';
            var btn = m.foot.querySelector('[data-ok]'); FA.busy(btn, true, 'Uploading');
            upload([f], null, FA.val('fVisible') ? 'true' : 'false', FA.val('fKind'), FA.val('fDesc'), function () { m.close(); });
            return false;
        });
    }

    function staffPicker(host, value) {
        return FA.typeahead(host, { source: DC.staffSource(PAGE), render: DC.staffRender, placeholder: 'Type a name', value: value || null,
            pickedText: function (s) { return s.name + (s.username ? ' (' + s.username + ')' : ''); } });
    }

    function investigateDialog() {
        var p;
        dialog({ title: 'Place under investigation', sub: D['case'].caseNo,
            body: '<div class="fa-form">' + field('What will be investigated', '<textarea id="iNote" class="fa-textarea" rows="3" maxlength="2000"></textarea>', '', true, true) +
                  field('Case officer', '<div id="iOfficer"></div>', 'The officer who will follow the case. Needs an eadmin login.', true) + '</div>',
            onOpen: function () { p = staffPicker('iOfficer'); }, ok: 'Save'
        }, function () {
            var o = p.get(); if (o && !o.username) return 'That staff member has no eadmin login. Choose someone who has one.';
            return { action: 'investigate', args: { note: FA.val('iNote'), officer: o ? o.username : '' }, toast: 'The case is under investigation.' };
        });
    }

    function officerDialog() {
        var p;
        dialog({ title: 'Case officer', sub: D['case'].caseNo,
            body: '<div class="fa-form">' + field('Officer', '<div id="oOfficer"></div>', '', true, true) + field('Note', '<input id="oNote" class="fa-input" maxlength="500"/>', '', true) + '</div>',
            onOpen: function () { p = staffPicker('oOfficer'); }
        }, function () {
            var o = p.get(); if (!o) return 'Choose the officer.'; if (!o.username) return 'That staff member has no eadmin login.';
            return { action: 'officer', args: { officer: o.username, officerName: o.name, note: FA.val('oNote') }, toast: 'Case officer saved.' };
        });
    }

    function restrictedDialog() {
        var on = !D['case'].restricted;
        FA.reason({ title: on ? 'Mark as restricted' : 'Remove the restriction', sub: D['case'].caseNo, label: 'Reason', ok: on ? 'Restrict' : 'Remove',
            message: on ? 'Only officers allowed to see restricted cases will see this case, in every list, count and report. Each opening is logged.' : 'Every officer whose role covers this case will see it again.' },
            function (reason, m, done) { act('restricted', { restricted: on, reason: reason }, null, function (err) { done(err); if (!err) FA.toast('Saved.'); }); });
    }

    function committeeButton(target) {
        FA.call(PAGE, 'Committee', {}, function (r) {
            var b = qs(target + 'Fill'); if (!b) return;
            if (!r.success || !r.rows.length) { b.style.display = 'none'; return; }
            b.onclick = function () { qs(target).value = r.rows.map(function (x) { return x.name + ' (' + x.role.toLowerCase() + ')'; }).join('; '); };
        });
    }

    function hearingDialog(mode) {
        var c = D['case'], adj = mode === 'adjourn';
        dialog({
            title: adj ? 'Adjourn the hearing' : 'Schedule a hearing', sub: c.caseNo + (adj ? ', now ' + c.hearing : ''),
            body: '<div class="fa-form">' + field('New date and time', '<input type="datetime-local" id="hAt" class="fa-input" min="' + B.now + '"/>', '', false, true).replace('New date', adj ? 'New date' : 'Date') +
                field('Venue', '<input id="hVenue" class="fa-input" maxlength="200" value="' + esc(adj ? c.venue : '') + '"/>', '', false, true) +
                field('Panel', '<input id="hPanel" class="fa-input" maxlength="1000"/>', '<button type="button" class="fa-btn fa-btn--link" id="hPanelFill">Fill with this year\'s committee</button>', true) +
                (adj ? field('Reason for the adjournment', '<textarea id="hReason" class="fa-textarea" rows="2" maxlength="1000"></textarea>', c.status === 'SUMMONED' ? 'The student was summoned, so a new summons letter is issued for the new date.' : '', true, true) :
                       '<div class="fa-field fa-full"><label class="fa-check-line"><input type="checkbox" id="hSummon" checked/> Summon the student now (issues the summons letter and notifies the student)</label></div>') +
                field('Reason for short notice', '<input id="hShort" class="fa-input" maxlength="300"/>', 'Needed when the hearing is less than ' + B.noticeDays + ' days away.', true) + '</div>',
            onOpen: function () { committeeButton('hPanel'); }, ok: adj ? 'Adjourn' : 'Schedule'
        }, function () {
            if (!FA.val('hAt')) return 'Give the date and time.';
            if (!FA.val('hVenue')) return 'Give the venue.';
            return { action: adj ? 'adjourn' : 'schedule', args: { at: FA.val('hAt'), venue: FA.val('hVenue'), panel: FA.val('hPanel'), summon: !adj && !!FA.val('hSummon'),
                     reason: FA.val('hReason'), shortNoticeReason: FA.val('hShort') }, toast: adj ? 'Hearing adjourned.' : 'Hearing scheduled.' };
        });
    }

    function summonDialog() {
        var c = D['case'];
        dialog({ title: 'Summon the student', sub: c.caseNo,
            body: '<p style="margin:0 0 10px">The summons letter for the hearing on <b>' + esc(c.hearing) + '</b> in ' + esc(c.venue) + ' is issued, and the student is notified on the portal and by email.</p>' +
                  field('Reason for short notice', '<input id="sShort" class="fa-input" maxlength="300"/>', 'Needed when the hearing is less than ' + B.noticeDays + ' days away.', true),
            ok: 'Summon'
        }, function () { return { action: 'summon', args: { shortNoticeReason: FA.val('sShort') }, toast: 'The student has been summoned.' }; });
    }

    function recordHearingDialog() {
        var c = D['case'];
        dialog({ title: 'Record the hearing', sub: c.caseNo + ', ' + c.hearing,
            body: '<div class="fa-form">' + field('The student was', '<div class="fa-radios"><label><input type="radio" name="hAtt" value="PRESENT"/> Present</label><label><input type="radio" name="hAtt" value="REPRESENTED"/> Represented</label><label><input type="radio" name="hAtt" value="ABSENT"/> Absent</label></div>', '', true, true) +
                  field('Panel members present', '<input id="hPanel" class="fa-input" maxlength="1000"/>', '<button type="button" class="fa-btn fa-btn--link" id="hPanelFill">Fill with this year\'s committee</button>', true, true) +
                  field('Summary of the hearing', '<textarea id="hMin" class="fa-textarea" rows="6" maxlength="20000"></textarea>', 'What was presented, said and found. This entry is internal.', true, true) +
                  field('Minutes', '<input type="file" id="hFile" accept=".pdf,.docx"/>', 'Optional: the signed minutes.', true) + '</div>',
            onOpen: function () { committeeButton('hPanel'); }, ok: 'Record'
        }, function () {
            var att = FA.radio('hAtt'); if (!att) return 'Say whether the student attended.';
            var f = qs('hFile').files[0];
            return { action: 'hearing', args: { attendance: att, panel: FA.val('hPanel'), minutes: FA.val('hMin') }, toast: 'Hearing recorded.',
                     after: function (r) { if (f) upload([f], r.entryId, 'false', 'MINUTES', 'Minutes of the hearing'); } };
        });
    }

    // Sanction picker used by decisions and appeal variations.
    function sanctionPicker(host) {
        var all = D.sanctionTypes || [], showAll = false;
        function draw() {
            var list = all.filter(function (t) { return showAll || t.isDefault; });
            host.innerHTML = list.map(function (t) {
                var f = '';
                if (t.needsAmount) f += field('Amount (UGX)', '<input class="fa-input fa-num-input" data-f="amount" inputmode="numeric"/>', '', false, true);
                if (t.needsDates !== 'NONE') f += field('From', '<input type="date" class="fa-input" data-f="from" value="' + B.today + '"/>', '', false, true) +
                    (t.needsDates === 'FROM_TO' ? field('To', '<input type="date" class="fa-input" data-f="to"/>', '', false, true) : field('To (optional)', '<input type="date" class="fa-input" data-f="to"/>'));
                if (t.needsCourse) f += field('Course code', '<input class="fa-input" data-f="course" maxlength="25" value="' + esc(D.incident.course || '') + '"/>', '', false, true);
                if (t.needsSemester) f += field('Academic year', '<input class="fa-input" data-f="acadYear" placeholder="2026/2027" value="' + esc(D.incident.examYear || B.year) + '"/>', '', false, true) +
                    field('Semester', '<input type="number" min="1" max="3" class="fa-input" data-f="semester" value="' + esc(D.incident.examSemester || '') + '"/>', '', false, true);
                f += field('Terms', '<input class="fa-input" data-f="terms" maxlength="1000"/>', 'Anything the student must do or know, for example hours of service.', true);
                return '<div class="dc-sanction" data-id="' + t.id + '" data-outcome="' + t.outcome + '"><label class="dc-sanction__head"><input type="checkbox"/><span class="dc-sanction__name">' + esc(t.name) + '</span>' +
                    (t.isDefault ? '<span class="dc-default">Usual</span>' : '') + '<span class="dc-sanction__fx">' + esc(t.effectsText || (t.outcome === 'SANCTION' ? 'Recorded only' : 'No sanction')) + '</span></label>' +
                    '<div class="dc-sanction__body">' + (t.description ? '<p class="fa-hint" style="margin:0 0 8px">' + esc(t.description) + '</p>' : '') + '<div class="fa-form">' + f + '</div></div></div>';
            }).join('') + '<button type="button" class="fa-btn fa-btn--link" id="spAll">' + (showAll ? 'Show only the usual sanctions for this type' : 'Show every sanction') + '</button>';
            Array.prototype.forEach.call(host.querySelectorAll('.dc-sanction'), function (s) {
                var cb = s.querySelector('input[type=checkbox]');
                cb.onchange = function () {
                    if (cb.checked && s.getAttribute('data-outcome') !== 'SANCTION')
                        Array.prototype.forEach.call(host.querySelectorAll('.dc-sanction'), function (o) { if (o !== s) { o.querySelector('input').checked = false; o.className = 'dc-sanction'; } });
                    if (cb.checked && s.getAttribute('data-outcome') === 'SANCTION')
                        Array.prototype.forEach.call(host.querySelectorAll('.dc-sanction[data-outcome=DISMISSAL], .dc-sanction[data-outcome=ACQUITTAL]'), function (o) { o.querySelector('input').checked = false; o.className = 'dc-sanction'; });
                    s.className = 'dc-sanction' + (cb.checked ? ' is-on' : '');
                };
            });
            qs('spAll').onclick = function () { var keep = read(); showAll = !showAll; draw(); };
        }
        function read() {
            return Array.prototype.map.call(host.querySelectorAll('.dc-sanction.is-on'), function (s) {
                var o = { typeId: s.getAttribute('data-id') };
                Array.prototype.forEach.call(s.querySelectorAll('[data-f]'), function (i) { o[i.getAttribute('data-f')] = i.value.trim(); });
                return o;
            });
        }
        draw();
        return { read: read };
    }

    function decisionDialog(mode) {
        var c = D['case'], appeal = mode === 'appeal', picker;
        dialog({
            title: appeal ? 'Decide the appeal' : 'Record the decision', sub: c.caseNo + ', ' + c.name, size: 'wide',
            body: (appeal ? field('Outcome', '<div class="fa-radios"><label><input type="radio" name="apOut" value="DISMISSED"/> Dismissed: the decision stands</label>' +
                        '<label><input type="radio" name="apOut" value="UPHELD"/> Upheld: the decision is set aside</label><label><input type="radio" name="apOut" value="VARIED"/> Varied: new sanctions replace the old</label>' +
                        '<label><input type="radio" name="apOut" value="WITHDRAWN"/> Withdrawn by the student</label></div>', '', true, true) :
                    '<div class="fa-notice">The decision, its sanctions and the appeal route are shown to the student, a decision letter is issued, and the appeal window of ' + B.appealDays +
                    ' days starts today. Interim measures end and are replaced by what you decide here.</div>') +
                '<div class="fa-form">' + (appeal ? '' : field('Findings', '<textarea id="dFind" class="fa-textarea" rows="4" maxlength="20000"></textarea>', 'What the Committee found to have happened, and why.', true, true) +
                    field('Decision date', '<input type="date" id="dDate" class="fa-input" max="' + B.today + '" value="' + B.today + '"/>', '', false, true)) +
                field(appeal ? 'Decision on the appeal' : 'Decision', '<textarea id="dText" class="fa-textarea" rows="4" maxlength="20000"></textarea>', 'Written to the student in the letter.', true, true) + '</div>' +
                '<div id="dSancWrap"' + (appeal ? ' style="display:none"' : '') + '><div class="fa-section">' + (appeal ? 'New sanctions' : 'Sanctions') + '</div><div id="dSanc"></div></div>',
            onOpen: function () {
                picker = sanctionPicker(qs('dSanc'));
                if (appeal) Array.prototype.forEach.call(document.querySelectorAll('input[name=apOut]'), function (r) { r.onchange = function () { qs('dSancWrap').style.display = FA.radio('apOut') === 'VARIED' ? '' : 'none'; }; });
            },
            ok: appeal ? 'Record the appeal decision' : 'Record the decision'
        }, function (m) {
            var s = picker.read(), out = appeal ? FA.radio('apOut') : '';
            if (appeal && !out) return 'Choose the outcome.';
            if (!appeal && FA.val('dFind').length < 20) return 'Record the findings (at least 20 characters).';
            if (FA.val('dText').length < 20) return 'Write the decision (at least 20 characters).';
            if (!appeal && !s.length) return 'Choose at least one sanction, or dismissal or acquittal.';
            if (appeal && out === 'VARIED' && !s.length) return 'Choose the sanctions that replace the old ones.';
            var args = appeal ? { outcome: out, decision: FA.val('dText'), sanctions: out === 'VARIED' ? s : [] } : { findings: FA.val('dFind'), decision: FA.val('dText'), decidedOn: FA.val('dDate'), sanctions: s };
            var names = Array.prototype.map.call(m.body.querySelectorAll('.dc-sanction.is-on .dc-sanction__name'), function (n) { return n.textContent; });
            if (!confirm((appeal ? 'Record the appeal decision' : 'Record the decision') + (names.length ? ' with: ' + names.join(', ') : '') + '?\n\nIt cannot be edited afterwards; any change is made by lifting or varying a sanction, or on appeal.')) return false;
            return { action: appeal ? 'decideAppeal' : 'decide', args: args, toast: appeal ? 'Appeal decided and the letter issued.' : 'Decision recorded and the letter issued.' };
        });
    }

    function measureDialog(recommend) {
        var c = D['case'];
        var opts = (D.sanctionTypes || []).filter(function (t) { return t.interim; });
        dialog({
            title: recommend ? 'Recommend an interim measure' : 'Apply an interim measure', sub: c.caseNo + ', ' + c.name,
            body: (recommend ? '<div class="fa-notice fa-notice--warn">An officer with permission to apply interim measures must confirm your recommendation before it takes effect.</div>' :
                    '<div class="fa-notice fa-notice--warn">The measure takes effect at once (a portal block within a minute), is shown to the student with your reason, and ends when the case is decided or on the date you give.</div>') +
                '<div class="fa-form">' + field('Measure', '<select id="mCode" class="fa-select">' + opts.map(function (t) { return '<option value="' + esc(t.code) + '">' + esc(t.name) + ' (' + esc(t.effectsText) + ')</option>'; }).join('') + '</select>', '', true, true) +
                (recommend ? '' : field('From', '<input type="date" id="mFrom" class="fa-input" value="' + B.today + '"/>', '', false, true) + field('Until (optional)', '<input type="date" id="mTo" class="fa-input" min="' + B.today + '"/>')) +
                field('Reason', '<textarea id="mReason" class="fa-textarea" rows="3" maxlength="1000"></textarea>', 'Shown to the student.', true, true) + '</div>',
            ok: recommend ? 'Recommend' : 'Apply', danger: !recommend
        }, function () {
            if (FA.val('mReason').length < 10) return 'Give the reason (at least 10 characters).';
            if (recommend) {
                var name = qs('mCode').options[qs('mCode').selectedIndex].text;
                return { action: 'entry', args: { type: 'NOTE', title: 'Recommended: ' + name + ' pending a decision', body: FA.val('mReason') + '\nThis is a recommendation for an officer with permission to apply interim measures.', visible: false }, toast: 'Recommendation recorded.' };
            }
            return { action: 'measure', args: { code: FA.val('mCode'), from: FA.val('mFrom'), to: FA.val('mTo'), reason: FA.val('mReason') }, toast: 'Interim measure applied.' };
        });
    }

    function liftDialog(s) {
        dialog({ title: 'Lift ' + s.name.toLowerCase(), sub: D['case'].caseNo, danger: true,
            body: '<div class="fa-form">' + field('Reason', '<textarea id="lReason" class="fa-textarea" rows="3" maxlength="1000"></textarea>', 'Shown to the student.', true, true) +
                  '<div class="fa-field fa-full"><label class="fa-check-line"><input type="checkbox" id="lLetter" checked/> Issue a lifting letter</label></div></div>', ok: 'Lift'
        }, function () {
            if (FA.val('lReason').length < 10) return 'Give the reason (at least 10 characters).';
            return { action: 'lift', args: { sanctionId: s.id, reason: FA.val('lReason'), letter: !!FA.val('lLetter') }, toast: s.name + ' lifted.' };
        });
    }

    function varyDialog(s) {
        dialog({ title: 'Vary ' + s.name.toLowerCase(), sub: 'Now: ' + s.from + (s.to ? ' to ' + s.to : ', open') + (s.amount ? ', UGX ' + s.amount : ''),
            body: '<div class="fa-notice">The current sanction is ended as varied and a new one is recorded with the new terms. Both stay in the record.</div><div class="fa-form">' +
                field('From', '<input type="date" id="vFrom" class="fa-input" value="' + esc(s.fromIso) + '"/>') + field('To', '<input type="date" id="vTo" class="fa-input" value="' + esc(s.toIso) + '"/>') +
                '<div class="fa-field"><label class="fa-check-line" style="margin-top:20px"><input type="checkbox" id="vOpen"/> No end date</label></div>' +
                (s.amount ? field('Amount (UGX)', '<input id="vAmount" class="fa-input fa-num-input" value="' + esc(s.amount.replace(/,/g, '')) + '"/>') : '') +
                field('Terms', '<input id="vTerms" class="fa-input" maxlength="1000" value="' + esc(s.terms) + '"/>', '', true) +
                field('Reason', '<textarea id="vReason" class="fa-textarea" rows="3" maxlength="1000"></textarea>', 'Shown to the student.', true, true) + '</div>', ok: 'Vary'
        }, function () {
            if (FA.val('vReason').length < 10) return 'Give the reason (at least 10 characters).';
            return { action: 'vary', args: { sanctionId: s.id, from: FA.val('vFrom'), to: FA.val('vOpen') ? '' : FA.val('vTo'), openEnded: !!FA.val('vOpen'), amount: FA.val('vAmount'), terms: FA.val('vTerms'), reason: FA.val('vReason') }, toast: 'Sanction varied.' };
        });
    }

    function followDialog(s) {
        var marks = /CANCEL/.test(s.effects);
        dialog({ title: marks ? 'Results cancelled as decided' : 'Billed as decided', sub: s.name + (s.amount ? ', UGX ' + s.amount : '') + (s.course ? ', ' + s.course : ''),
            body: '<div class="fa-notice">' + (marks ? 'Cancel the result in the marks screens first, giving case ' + esc(D['case'].caseNo) + ' as the reason. Then record it here.' :
                    'Raise the bill in the fees screens first, quoting case ' + esc(D['case'].caseNo) + '. Then record it here.') + '</div><div class="fa-form">' +
                field('Reference', '<input id="fuRef" class="fa-input" maxlength="150"/>', marks ? 'For example the marks change or approval reference.' : 'For example the invoice or bill number.', true, true) +
                field('Note', '<input id="fuNote" class="fa-input" maxlength="500"/>', '', true) + '</div>', ok: 'Record'
        }, function () {
            if (FA.val('fuRef').length < 3) return 'Give the reference.';
            return { action: 'followup', args: { sanctionId: s.id, reference: FA.val('fuRef'), note: FA.val('fuNote') }, toast: 'Recorded.' };
        });
    }

    function appealDialog() {
        var c = D['case'], late = !c.appealOpen;
        dialog({ title: 'Lodge an appeal', sub: c.caseNo + ', on the student\'s behalf',
            body: (late ? '<div class="fa-notice fa-notice--warn">The appeal window closed on ' + esc(c.appealDeadline) + '. Only the appellate authority can accept a late appeal, with a reason.</div>' : '') +
                '<div class="fa-form">' + field('Grounds of appeal', '<textarea id="aGr" class="fa-textarea" rows="5" maxlength="20000"></textarea>', 'As given by the student.', true, true) +
                (late ? field('Why a late appeal is accepted', '<textarea id="aLate" class="fa-textarea" rows="2" maxlength="1000"></textarea>', '', true, true) : '') +
                field('The student\'s written appeal', '<input type="file" id="aFile" accept=".pdf,.jpg,.jpeg,.png,.docx"/>', 'Optional.', true) + '</div>', ok: 'Lodge'
        }, function () {
            if (FA.val('aGr').length < 20) return 'Give the grounds (at least 20 characters).';
            var f = qs('aFile').files[0];
            return { action: 'appeal', args: { grounds: FA.val('aGr'), lateReason: FA.val('aLate') }, toast: 'Appeal lodged.', after: function () { if (f) upload([f], null, 'true', 'APPEAL', 'Written appeal'); } };
        });
    }

    function letterDialog() {
        var t = D.templates || [];
        dialog({ title: 'Issue a letter', sub: D['case'].caseNo, size: 'wide',
            body: '<div class="fa-form">' + field('Letter', '<select id="ltT" class="fa-select">' + t.map(function (x) { return '<option value="' + esc(x.code) + '">' + esc(x.name) + '</option>'; }).join('') + '</select>') +
                '<div id="ltExtra" class="fa-full"></div></div><div class="fa-section">Preview</div><div id="ltPrev" class="dc-letter"><span class="fa-muted">Loading</span></div><div id="ltBlank" class="fa-hint" style="margin-top:6px"></div>',
            onOpen: function () {
                var extra = function () {
                    var code = FA.val('ltT');
                    qs('ltExtra').innerHTML = code === 'LIFTING' ? '<div class="fa-form">' + field('Sanction lifted', '<input id="ltSl" class="fa-input"/>') + field('Reason', '<input id="ltLr" class="fa-input"/>') + '</div>' : '';
                    Array.prototype.forEach.call(qs('ltExtra').querySelectorAll('input'), function (i) { i.oninput = FA.debounce(preview, 400); });
                    preview();
                };
                qs('ltT').onchange = extra; extra();
            }, ok: 'Issue the letter'
        }, function () {
            return { action: 'letter', args: { template: FA.val('ltT'), fields: fields() }, toast: 'Letter issued.' };
        });
        function fields() { var f = {}; if (qs('ltSl')) { f.sanction_lifted = FA.val('ltSl'); f.lift_reason = FA.val('ltLr'); } return f; }
        function preview() {
            FA.call(PAGE, 'PreviewLetter', { caseId: B.id, template: FA.val('ltT'), fieldsJson: JSON.stringify(fields()) }, function (r) {
                if (!r.success) { qs('ltPrev').textContent = r.message; return; }
                qs('ltPrev').innerHTML = '<h4>RE: ' + esc(r.subject) + '</h4>' + r.body.split(/\n\s*\n/).map(function (p) { return '<p>' + esc(p.trim()) + '</p>'; }).join('');
                qs('ltBlank').textContent = (r.blanks.length ? 'Blank in this letter: ' + r.blanks.join(', ').replace(/_/g, ' ') + '. ' : '') + (r.studentCopy ? 'The student gets a copy on the portal.' : 'Internal letter: not shown to the student.');
            });
        }
    }

    function closeDialog() {
        var c = D['case'], early = c.status === 'DECIDED' && c.appealOpen;
        dialog({ title: 'Close the case', sub: c.caseNo,
            body: (early ? '<div class="fa-notice fa-notice--warn">The appeal window is open until ' + esc(c.appealDeadline) + '. Close now only if the student has waived the appeal, and record how.</div>' : '') +
                  '<p style="margin:0 0 10px">Sanctions with end dates stay in force until those dates and then end on their own.</p>' +
                  field(early ? 'Why the case is closed now' : 'Closing note (optional)', '<textarea id="clNote" class="fa-textarea" rows="3" maxlength="2000"></textarea>', '', true, early), ok: 'Close the case'
        }, function () {
            if (early && FA.val('clNote').length < 10) return 'Record why the case is closed before the appeal window ends.';
            return { action: 'close', args: { note: FA.val('clNote') }, toast: 'Case closed.' };
        });
    }

    function withdrawDialog() {
        FA.reason({ title: 'Withdraw the case', sub: D['case'].caseNo, label: 'Reason', ok: 'Withdraw', danger: true,
            message: 'The case is withdrawn, any interim measure is lifted, scheduled hearings are cancelled and the student is told. The record is kept.' },
            function (reason, m, done) { act('withdraw', { reason: reason }, null, function (err) { done(err); if (!err) FA.toast('Case withdrawn.'); }); });
    }

    function statement() {
        FA.exportDialog({ page: PAGE, report: 'statement', title: 'Case statement, ' + D['case'].caseNo, cfg: { caseId: B.id, version: 'committee' },
            note: 'Choose the student copy below for a statement that shows only what the student may see.' });
        setTimeout(function () {
            var box = document.querySelector('.fa-modal.is-open .fa-modal__body'); if (!box) return;
            var div = document.createElement('div'); div.className = 'fa-field'; div.style.marginTop = '14px';
            div.innerHTML = '<span class="fa-label">Copy</span><div class="fa-radios"><label><input type="radio" name="stV" value="committee" checked/> Committee copy (every entry)</label>' +
                '<label><input type="radio" name="stV" value="student"/> Student copy (what the student may see)</label></div>';
            box.insertBefore(div, box.children[1] || null);
            var ok = document.querySelector('.fa-modal.is-open [data-ok]');
            var orig = ok.onclick;
            ok.onclick = function () {
                var fmt = (box.querySelector('input[name=faXFmt]:checked') || {}).value || 'pdf';
                FA.download(PAGE, { faReport: 'statement', faFormat: fmt, faConfig: JSON.stringify({ caseId: B.id, version: FA.radio('stV') }), faCols: '', faGroup: '' });
                FA.toast('Preparing the statement.');
                document.querySelector('.fa-modal.is-open .fa-modal__close').click();
            };
        }, 0);
    }

    load();
})();
